using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-18 N2 — 3D 오브젝트는 덮은 UI 밑에서 손가락을 받지 않는다 (대표님 「하단탭이 이미 가리고 있는 상태인데 해당 오브젝트 우선순위가 좀 꼬여있는 것 같은데」).
    ///  · worldtouch : 가상 손가락(PlayTestFinger)으로 실제처럼 —
    ///     평소 AR 화면의 맨 위 UI 지도(보이지 않는 덮개가 없나) · 오브젝트 두 번 톡톡 → 정보 창 ·
    ///     정보 창 위 밀기(옆 = 다음 장 · 위 = 댓글 · 아래 = 닫기)는 예전대로 ·
    ///     정보 창 위에 뜬 창(고치기) · 도크에서 시작한 밀기는 정보 창이 받지 않는다 ·
    ///     메세지 창(로그인 안 됐으면 로그인 안내 창)이 덮은 자리의 오브젝트는 두 번 톡톡해도 반응 없음 (사진 · 누름 기록 · 커지기).
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static Object3DTouchHaptic wtHap;
        private static DoubleTap3D wtDt;
        private static float wtRest = 1f, wtPeak, wtTapAt;

        private static Vector2 WtMid() => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        private static float WtRatio() => wtHap != null ? wtHap.transform.localScale.x / wtRest : 0f;
        private static bool WtOpen() => wtDt != null && DoubleTap3D.Shown == wtDt && wtDt.fullscreenCanvasGroup != null && wtDt.fullscreenCanvasGroup.gameObject.activeSelf;
        private static bool WtComments() => CommentManager.Instance != null && CommentManager.Instance.IsPanelOpen;
        private static float WtLastTap() => wtDt != null ? (float)DsField(wtDt, "lastTapTime") : 0f;
        private static int WtPage() => wtDt != null ? (int)DsField(wtDt, "imageIndex") : -99;

        private static Vector2 WtObj()
        {
            var c = wtHap != null ? wtHap.GetComponent<Collider>() : null;
            if (c == null || Camera.main == null) return new Vector2(-1f, -1f);
            var sp = Camera.main.WorldToScreenPoint(c.bounds.center);
            return new Vector2(sp.x, sp.y);
        }

        // 그 자리에서 물리 광선이 오브젝트에 닿나 (덮인 자리 시험이 뜻이 있으려면 — 창이 열려도 오브젝트는 그 자리에 있어야)
        private static bool WtReachable()
        {
            var c = wtHap != null ? wtHap.GetComponent<Collider>() : null;
            return c != null && c.enabled && Camera.main != null && Physics.Raycast(Camera.main.ScreenPointToRay(WtObj()), out var hit) && hit.collider == c;
        }

        private static Vector2 WtDock()
        {
            var rt = DlSlot("List_Button");
            if (rt == null) return new Vector2(Screen.width * 0.5f, 40f);
            var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
        }

        private static string WtPath(GameObject go)
        {
            if (go == null) return "없음";
            var s = go.name;
            for (var t = go.transform.parent; t != null && s.Length < 100; t = t.parent) s = t.name + "/" + s;
            return s;
        }

        private static string WtTop(Vector2 p) => WtPath(WorldTouch.TopUI(p));

        // 화면을 6 × 11 칸으로 나눠 칸 가운데마다 맨 위 UI — 보이지 않는데 손가락을 받는 것(투명 그림 · 빈 글씨)은 따로
        private static List<string> WtMap(string label)
        {
            var count = new Dictionary<string, int>();
            var ghosts = new List<string>();
            int free = 0, total = 0;
            for (int gy = 0; gy < 11; gy++)
                for (int gx = 0; gx < 6; gx++)
                {
                    total++;
                    var go = WorldTouch.TopUI(new Vector2((gx + 0.5f) / 6f * Screen.width, (gy + 0.5f) / 11f * Screen.height));
                    if (go == null) { free++; continue; }
                    string k = WtPath(go);
                    count[k] = count.TryGetValue(k, out var c) ? c + 1 : 1;
                    var g = go.GetComponent<Graphic>();
                    bool ghost = g != null && (g.color.a * g.canvasRenderer.GetInheritedAlpha() < 0.02f || (g is Text tx && string.IsNullOrEmpty(tx.text)));
                    if (ghost && !ghosts.Contains(k)) ghosts.Add(k);
                }
            log.Add($"  {label} — 맨 위 UI 지도 ({total}곳 중 덮개 없음 {free}곳):");
            foreach (var kv in count) log.Add($"    {kv.Value}곳 · {kv.Key}");
            if (ghosts.Count > 0) log.Add("    ↳ 보이지 않는데 손가락을 받는 것: " + string.Join(" · ", ghosts));
            return ghosts;
        }

        // 가상 손가락 한 번 — 누르고 두 프레임 뒤에 뗀다 (그동안 오브젝트 크기를 본다)
        private static void WtTap(Func<Vector2> at)
        {
            int f = -1;
            Vector2 p = default;
            steps.Enqueue(() =>
            {
                if (ptTouch == null) return true;
                if (f < 0)
                {
                    p = at();
                    InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Began, position = p });
                    f = Time.frameCount;
                    wtTapAt = Time.time;
                }
                wtPeak = Mathf.Max(wtPeak, WtRatio());
                if (Time.frameCount < f + 2) return false;
                InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = p });
                return true;
            });
        }

        private static void WtWatch(float sec)
        {
            double until = -1;
            steps.Enqueue(() =>
            {
                if (until < 0) until = EditorApplication.timeSinceStartup + sec;
                wtPeak = Mathf.Max(wtPeak, WtRatio());
                return EditorApplication.timeSinceStartup >= until;
            });
        }

        // 두 번 톡톡 — 두 번째는 첫 누름 0.15초 뒤 (DoubleTap3D 는 게임 시간 0.1~0.3초를 두 번으로 본다)
        private static void WtDoubleTap(Func<Vector2> at)
        {
            WtTap(at);
            float first = 0f;
            Do(() => first = wtTapAt);
            steps.Enqueue(() => { wtPeak = Mathf.Max(wtPeak, WtRatio()); return Time.time >= first + 0.15f; });
            WtTap(at);
            WtWatch(1.0f);
        }

        // 앞 단계가 (고치기 전 코드에서) 창을 닫아 버렸으면 다시 연다
        private static void WtEnsureOpen()
        {
            Do(() =>
            {
                if (WtOpen() || wtDt == null) return;
                typeof(DoubleTap3D).GetField("isCooldown", DsF).SetValue(wtDt, false);
                typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", DsF).Invoke(wtDt, null);
            });
            Wait(() => WtOpen(), 5f, "정보 창 (다시)");
            Sleep(0.6f);
        }

        private static void PlanWorldTouch()
        {
            nudgeName = "";
            DsBoot();
            Sleep(0.6f);
            Do(() =>
            {
                ptFocusWas = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;   // 에디터 창이 뒤에 있어도
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Wait(() => (wtHap = NudgePick()) != null, 25f, "누를 오브젝트");
            Do(() =>
            {
                wtDt = wtHap != null ? wtHap.GetComponent<DoubleTap3D>() : null;
                wtRest = wtHap != null ? wtHap.transform.localScale.x : 1f;
                log.Add(wtDt != null ? $"  누를 오브젝트 id {DsField(wtDt, "id")} · {wtHap.GetType().Name} + DoubleTap3D" : "  누를 오브젝트 없음");
            });
            Sleep(0.5f);

            // 0) 평소 AR 화면 — 덮개 없는 자리는 오브젝트가 받는다
            var ghosts0 = new List<string>();
            Do(() => ghosts0 = WtMap("평소 AR 화면"));
            Do(() => log.Add("  오브젝트 자리 맨 위 UI: " + WtTop(WtObj()) + (WtReachable() ? " · 광선 닿음" : " · 광선 안 닿음")));
            Check(() => WorldTouch.TopUI(WtObj()) == null && WtReachable(), "평소 AR 화면: 누를 오브젝트 자리를 덮은 UI 가 없다");
            Check(() => ghosts0.Count == 0, "평소 AR 화면에 보이지 않는데 손가락을 받는 덮개가 없다 (있으면 그 자리 오브젝트를 못 누른다)");
            Shot("wt_ar");

            // 1) 두 번 톡톡 → 정보 창 (예전대로)
            Do(() => wtPeak = 0f);
            WtDoubleTap(WtObj);
            Wait(() => WtOpen(), 5f, "정보 창");
            Check(() => WtOpen() && wtPeak > 1.03f, "평소엔 오브젝트를 두 번 톡톡하면 살짝 커지며 정보 창이 뜬다 (예전대로)");
            Sleep(0.8f);
            Do(() => log.Add("  정보 창 가운데 맨 위 UI: " + WtTop(WtMid())));

            // 2) 정보 창 위에서 옆으로 → 다음 장 (예전대로)
            int page0 = 0;
            Do(() => page0 = WtPage());
            Finger(() => WtMid(), new[] { new Vector2(-Screen.width * 0.45f, 0f) }, 8, null, null);
            Sleep(1.0f);
            Do(() => log.Add($"  옆으로 민 뒤 장: {page0} → {WtPage()}"));
            Check(() => WtOpen() && WtPage() != page0, "정보 창 위에서 옆으로 밀면 다음 장으로 (예전대로)");

            // 3) 위로 → 댓글 (예전대로)
            Finger(() => WtMid(), new[] { new Vector2(0f, 300f) }, 6, null, null);
            Wait(() => WtComments(), 3f, "댓글 창");
            Check(() => WtComments(), "정보 창 위에서 위로 밀면 댓글 창이 열린다 (예전대로)");
            Do(() => CommentManager.Instance?.ClosePanel());
            Wait(() => !WtComments(), 3f, "댓글 창 닫힘");
            Sleep(0.6f);
            Check(() => WtOpen(), "댓글 창을 닫으면 정보 창이 그대로");

            // 4) 정보 창 위에 뜬 창(고치기)에서 밀기 — 정보 창은 받지 않는다 (사진 14: 설정 시트 위에서 위로 밀었더니 정보 창이 댓글을 열었다)
            WtEnsureOpen();
            Do(() => { var fp = Find("Fixpage"); if (fp != null) fp.gameObject.SetActive(true); });
            Sleep(0.8f);
            Do(() => log.Add("  고치기 창 가운데 맨 위 UI: " + WtTop(WtMid())));
            Finger(() => WtMid(), new[] { new Vector2(0f, 300f) }, 6, null, "wt_fix_up");
            Sleep(0.8f);
            Check(() => !WtComments() && WtOpen(), "정보 창 위에 뜬 창(고치기)에서 위로 밀어도 댓글 창이 열리지 않는다 · 정보 창 그대로 (N2)");
            Do(() => { if (WtComments()) CommentManager.Instance.ClosePanel(); });
            Sleep(0.4f);
            WtEnsureOpen();
            Do(() => { var fp = Find("Fixpage"); if (fp != null && !fp.gameObject.activeSelf) fp.gameObject.SetActive(true); });
            Sleep(0.4f);
            Finger(() => WtMid(), new[] { new Vector2(0f, -300f) }, 6, null, null);
            Sleep(0.8f);
            Check(() => WtOpen(), "그 창에서 아래로 밀어도 뒤의 정보 창이 닫히지 않는다 (N2)");
            Do(() => { var fp = Find("Fixpage"); if (fp != null) fp.gameObject.SetActive(false); });
            Sleep(0.6f);

            // 5) 도크에서 시작한 밀기 — 도크가 덮은 자리라 정보 창은 받지 않는다
            WtEnsureOpen();
            Do(() => log.Add("  도크 「목록」 자리 맨 위 UI: " + WtTop(WtDock())));
            Finger(() => WtDock(), new[] { new Vector2(0f, 400f) }, 8, null, null);
            Sleep(0.8f);
            Check(() => !WtComments() && WtOpen(), "도크에서 시작해 위로 밀어도 댓글 창이 열리지 않는다 · 정보 창 그대로 (N2)");
            Do(() => { if (WtComments()) CommentManager.Instance.ClosePanel(); });
            Sleep(0.4f);

            // 6) 아래로 → 닫힘 (예전대로)
            WtEnsureOpen();
            Finger(() => WtMid(), new[] { new Vector2(0f, -300f) }, 6, null, null);
            Wait(() => !WtOpen(), 3f, "정보 창 닫힘");
            Check(() => !WtOpen(), "정보 창 위에서 아래로 밀면 닫힌다 (예전대로)");
            Sleep(0.8f);

            // 7) 메세지 창(로그인 안 됐으면 로그인 안내 창)이 덮은 자리의 오브젝트 — 두 번 톡톡해도 반응 없음
            Do(() => log.Add("  메세지 칸 누름: " + DlTap(DlSlot("Message_Button"))));
            Wait(() => Active("MessagePanel") || Active("LoginPromptPanel"), 3f, "메세지 창 (또는 로그인 안내 창)");
            Sleep(0.8f);
            Do(() => log.Add("  창이 덮은 오브젝트 자리 맨 위 UI: " + WtTop(WtObj()) + (WtReachable() ? " · 광선 닿음" : " · 광선 안 닿음")));
            float last0 = 0f;
            bool reach = false, covered = false;
            Do(() => { typeof(DoubleTap3D).GetField("isCooldown", DsF).SetValue(wtDt, false); last0 = WtLastTap(); wtPeak = 0f; reach = WtReachable(); covered = WorldTouch.TopUI(WtObj()) != null; });
            WtDoubleTap(WtObj);
            Do(() => log.Add($"  덮인 자리 두 번 톡톡: 정보 창 {(WtOpen() ? "뜸" : "안 뜸")} · 누름 기록 {(WtLastTap() != last0 ? "남음" : "없음")} · 가장 클 때 {wtPeak:0.000}배"));
            Check(() => reach && covered && !WtOpen() && WtLastTap() == last0 && wtPeak < 1.01f,
                "창이 덮은 자리의 오브젝트는 두 번 톡톡해도 반응 없음 — 정보 창 · 누름 기록 · 커지기 없음 (N2)");
            Do(() => { if (WtOpen()) wtDt.closeButton.onClick.Invoke(); });
            CloseAll();
            Sleep(0.6f);

            // 8) 창을 닫으면 그 자리 오브젝트는 다시 받는다 (예전대로)
            Wait(() => WorldTouch.TopUI(WtObj()) == null, 3f, "덮개 걷힘");
            Do(() => log.Add($"  닫은 뒤: 열린 창 [{OpenPanels()}] · 안내 막 {R0926GuideOverlay.BlocksWorld} · 댓글 {WtComments()} · 맨 위 UI {WtTop(WtObj())} · 광선 {(WtReachable() ? "닿음" : "안 닿음")} · 손가락 {UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count}"));
            Do(() => { typeof(DoubleTap3D).GetField("isCooldown", DsF).SetValue(wtDt, false); wtPeak = 0f; last0 = WtLastTap(); });
            WtDoubleTap(WtObj);
            Wait(() => WtOpen(), 5f, "정보 창");
            Do(() => log.Add($"  닫은 뒤 두 번 톡톡: 정보 창 {(WtOpen() ? "뜸" : "안 뜸")} · 누름 기록 {(WtLastTap() != last0 ? "남음" : "없음")} · 가장 클 때 {wtPeak:0.000}배"));
            Check(() => WtOpen() && wtPeak > 1.03f, "창을 닫으면 그 오브젝트를 두 번 톡톡해 다시 정보 창이 뜬다 · 누르면 살짝 커진다");
            Do(() => { if (WtOpen()) wtDt.closeButton.onClick.Invoke(); });
            Sleep(0.6f);

            Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
            });
        }
    }
}
