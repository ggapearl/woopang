using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-24 N4 대표님 「아래에서 위로 스와이프하면 근처 장소 발생하는것처럼, 상단에서 아래로 스와이프하면 바로 하늘보기 발생하도록 해줄래?」
        //  · 맨 AR 화면을 위→아래로 밀면 (화면 높이 12% · 1초 안) 하늘 판이 화면 위쪽에 고정돼 펼쳐진다 (R0926NearModal → R0926SkyWeather.ShowAtTop) · 처음 한 번 치우는 법 안내
        //  · 위로 밀면 그대로 「근처 장소」 · 짧게 · 옆으로 · 위로(아래로 밀기로는) · 천천히 · UI(도크) · 오브젝트 위 · 안내 중 · 하늘을 올려다보는 중엔 안 받는다
        //  · 하늘을 껐으면 「하늘 화면이 꺼져 있어요」 · 보일 것이 아직이면 「불러오는 중」 — 판은 안 뜬다 · 이미 위쪽에 고정돼 있으면 그대로
        //  가상 손가락(Touchscreen)으로 진짜 미는 것처럼 민다. 하늘 설정 · 안내 키 · 하늘 옆 장 서버 주소(닿지 않는 주소로 — 운영 서버에 묻지 않게)는 되돌린다.
        private static bool swRun;
        private static string swNearby, swHunt, swFriends;
        private static int swHint = -1;
        private static bool swQuiet;
        private static InputSettings.EditorInputBehaviorInPlayMode swFocusWas;
        private const string SwHintKey = "SkyNotchUnfoldHint2";   // R0926SkyWeather.NotchHintKey
        private static readonly float[] SwDownYs = { 0.62f, 0.66f, 0.58f, 0.7f, 0.55f, 0.5f, 0.45f, 0.4f };   // 위→아래로 미는 첫 자리 (화면 아래가 0)
        private static readonly float[] SwUpYs = { 0.3f, 0.35f, 0.25f, 0.4f, 0.2f, 0.45f };

        // 끝에서 한 번, 오류로 끊겨도 실행기 끝에서 한 번
        private static void SwFinish()
        {
            if (!swRun) return;
            swRun = false;
            var sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
            if (sky != null && sky.Pinned) sky.SetPinned(false);
            R0926Nearby.EditorBase = swNearby;
            R0926HuntApi.EditorBase = swHunt;
            R0926FriendsMap.EditorBase = swFriends;
            R0926NearModal.EditorQuiet = swQuiet;
            InputSystem.settings.editorInputBehaviorInPlayMode = swFocusWas;
            if (swHint < 0) PlayerPrefs.DeleteKey(SwHintKey); else PlayerPrefs.SetInt(SwHintKey, swHint);
            PlayerPrefs.Save();
            RestoreSkyPrefs();
        }

        // ok 를 만족하는 맨 AR 자리 — 가운데부터 · ys 순서로. 없으면 (-1, -1)
        private static Vector2 SwFree(Func<Vector2, bool> ok, float[] ys)
        {
            Physics.SyncTransforms();
            float[] xs = { 0.5f, 0.35f, 0.65f, 0.2f, 0.8f };
            foreach (float fy in ys)
                foreach (float fx in xs)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    if (ok(p)) return p;
                }
            return new Vector2(-1f, -1f);
        }

        // UI 가 없고 AR 오브젝트에 닿는 화면 자리 (카드에 띄운 것도 AR 화면에 그대로 — 11-26). 없으면 (-1, -1)
        // cube = true 면 돌리는 정육면체(CubeTouchRotator) 위 · false 면 그 밖의 AR 오브젝트(이름표 등) 위
        private static Vector2 SwOnObject(bool cube)
        {
            var cam = Camera.main;
            if (cam == null) return new Vector2(-1f, -1f);
            Physics.SyncTransforms();
            for (float fy = 0.2f; fy <= 0.8f; fy += 0.04f)
                for (float fx = 0.08f; fx <= 0.92f; fx += 0.04f)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    if (WorldTouch.TopUI(p) != null) continue;
                    var c = WorldTouch.FirstHit(p);
                    if (c != null && (c.GetComponentInParent<CubeTouchRotator>() != null) == cube) return p;
                }
            return new Vector2(-1f, -1f);
        }

        private static void PlanSkySwipe()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            R0926SkyWeather sky = null;
            R0926NearModal nm = null;
            RectTransform frame = null;
            Transform pinLayer = null;
            Vector2 downP = new Vector2(-1f, -1f), upP = new Vector2(-1f, -1f), slowP = default;
            Func<bool> showing = () => sky != null && (bool)typeof(R0926SkyWeather).GetField("skyShow", F).GetValue(sky);
            Func<float> frameAlpha = () => frame != null && frame.GetComponent<CanvasGroup>() is CanvasGroup g ? g.alpha : 0f;
            Func<bool> pinnedTop = () => sky != null && sky.PinnedTop && frame != null && frame.parent == pinLayer;
            Func<Vector2?> downAt = () => { var p = SwFree(q => nm.EditorCanSwipeDown(q), SwDownYs); return p.x >= 0f ? p : (Vector2?)null; };
            Func<Vector2?> upAt = () => { var p = SwFree(q => nm.EditorCanSwipeUp(q), SwUpYs); return p.x >= 0f ? p : (Vector2?)null; };
            Func<string> state = () => $"고정 {(sky.Pinned ? (sky.PinnedTop ? "위쪽" : "아래쪽") : "안 함")} · 하늘에 뜸 {showing()} · 틀 짙기 {frameAlpha():0.00} · 근처 카드 {nm.EditorMode}";

            Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                swNearby = R0926Nearby.EditorBase; swHunt = R0926HuntApi.EditorBase; swFriends = R0926FriendsMap.EditorBase;
                swHint = PlayerPrefs.HasKey(SwHintKey) ? PlayerPrefs.GetInt(SwHintKey) : -1;
                swQuiet = R0926NearModal.EditorQuiet;
                swFocusWas = InputSystem.settings.editorInputBehaviorInPlayMode;
                StashSkyPrefs();
                swRun = true;
                foreach (var k in new[] { "Sky_Enabled", "Sky_Weather", "Sky_Events" }) PlayerPrefs.SetInt(k, 1);
                PlayerPrefs.SetInt("Sky_Angle", 1);   // 28°
                PlayerPrefs.SetInt("Sky_StartCollapsed", 0);
                foreach (var k in new[] { "Sky_Swiped", "Sky_PinUsed" }) PlayerPrefs.SetInt(k, 1);   // 다른 처음 안내는 없이
                PlayerPrefs.SetInt(SwHintKey, 0);   // 위쪽 고정 안내 — 처음 한 번 뜨는지 본다
                R0926Nearby.EditorBase = NmDeadStub;
                R0926HuntApi.EditorBase = NmDeadStub;
                R0926FriendsMap.EditorBase = NmDeadStub;
                // 에디터의 가상 사용자(근처 사람 — 걸어 다닌다)가 4m 안에 들어와 「발견한 장소」 카드가 끼지 않게
                var vt = UnityEngine.Object.FindAnyObjectByType<P2PVirtualUserTester>();
                if (vt != null) { vt.StopSimulation(); vt.StopAllCoroutines(); }
                if (P2PManager.Instance != null) P2PManager.Instance.RemoveTestUserAvatar("3");
                HidePrivatePlaces();
                sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                nm = UnityEngine.Object.FindAnyObjectByType<R0926NearModal>(FindObjectsInactive.Include);
                var pg = UnityEngine.Object.FindAnyObjectByType<R0926SkyPages>(FindObjectsInactive.Include);
                if (sky == null || nm == null || pg == null) return;
                typeof(R0926SkyWeather).GetField("collapsed", F)?.SetValue(sky, false);
                frame = pg.EditorFrame;
                pinLayer = Find("SkyPin0926");
                InjectWeather();
                R0926NearModal.EditorQuiet = false;   // 진짜 손가락 처리(Swipe)를 켠다 — 시작 「근처 장소」는 띄운 셈 치고
                nm.EditorStartShown = true;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;   // 에디터 창이 뒤에 있어도
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Do(() => AimCamera(8f, 0f));   // 앞을 본다 (하늘이 아니라)
            Sleep(1.0f);
            Check(() => sky != null && nm != null && frame != null && pinLayer != null && ptTouch != null, "준비: 하늘 판 · 틀 · 고정 자리 · 근처 카드 · 가상 손가락");
            Check(() => { log.Add("  처음: " + state() + $" · 보일 것 {sky.Ready}"); return sky.Ready && !sky.Pinned && !showing(); }, "처음엔 하늘 판이 안 보임 (앞을 보는 중 · 날씨는 받음)");

            // 1) 받는 자리 · 안 받는 것 — 판단만 (손가락 없이)
            Do(() =>
            {
                downP = SwFree(q => nm.EditorCanSwipeDown(q), SwDownYs);
                log.Add($"  아래로 밀 첫 자리 {(downP.x >= 0f ? $"({downP.x / Screen.width:0.00}, {downP.y / Screen.height:0.00})" : "없음")}");
            });
            Check(() => downP.x >= 0f, "맨 AR 화면(UI · 오브젝트 없는 곳)은 아래로 밀기를 받는다");
            Check(() =>
            {
                var dock = Find("Dock0926") as RectTransform;
                var c = dock != null ? NmRect(dock).center : new Vector2(-1f, -1f);
                return dock != null && !nm.EditorCanSwipeDown(c) && !nm.EditorSwipeDown(c, c - NmUp(0.25f)) && !sky.Pinned;
            }, "도크(UI) 위에서 민 것은 안 받는다");
            // 11-28 실기기 — 오브젝트가 화면 곳곳에 떠 있어 아래로 밀기가 아예 안 먹었다. 이제 돌리는 정육면체(위아래로 끌면 돈다) 위만 막는다
            // 이 배경엔 AR 오브젝트가 없어 시험용 둘을 잠깐 띄운다 — 이름표 같은 오브젝트(왼쪽) · 돌리는 정육면체(오른쪽)
            var probes = new System.Collections.Generic.List<GameObject>();
            Do(() =>
            {
                var cam = Camera.main;
                if (cam == null) return;
                foreach (var (fx, cube) in new[] { (0.3f, false), (0.7f, true) })
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = cube ? "PT_SwipeCube" : "PT_SwipeLabel";
                    go.transform.position = cam.ScreenToWorldPoint(new Vector3(Screen.width * fx, Screen.height * 0.42f, 6f));
                    go.transform.localScale = Vector3.one * 1.6f;
                    if (cube) go.AddComponent<CubeTouchRotator>();
                    probes.Add(go);
                }
                Physics.SyncTransforms();
            });
            Sleep(0.3f);
            Do(() =>
            {
                var cp = SwOnObject(true);
                if (cp.x < 0f) { log.Add("  화면에 손에 닿는 정육면체가 없어 '정육면체 위' 확인은 건너뜀"); return; }
                log.Add($"  정육면체 자리 ({cp.x / Screen.width:0.00}, {cp.y / Screen.height:0.00}) · 받음 {nm.EditorCanSwipeDown(cp)}");
                log.Add((!nm.EditorCanSwipeDown(cp) && !nm.EditorSwipeDown(cp, cp - NmUp(0.25f)) && !sky.Pinned ? "PASS " : "FAIL ")
                        + "돌리는 정육면체 위에서 민 것은 안 받는다 (위아래 돌리기와 겹치지 않게)");
            });
            Do(() =>
            {
                var op = SwOnObject(false);
                if (op.x < 0f) { log.Add("  화면에 정육면체 아닌 AR 오브젝트가 없어 '오브젝트 위' 확인은 건너뜀"); return; }
                log.Add($"  정육면체 아닌 AR 오브젝트 자리 ({op.x / Screen.width:0.00}, {op.y / Screen.height:0.00}) · 받음 {nm.EditorCanSwipeDown(op)}");
                log.Add((nm.EditorCanSwipeDown(op) ? "PASS " : "FAIL ")
                        + "정육면체 아닌 오브젝트(이름표 등) 위에서 시작해도 아래로 밀기를 받는다 (오브젝트가 많아도 날씨 판이 뜬다)");
            });
            Do(() => { foreach (var go in probes) if (go != null) UnityEngine.Object.Destroy(go); probes.Clear(); });
            Check(() => downP.x >= 0f
                        && !nm.EditorSwipeDown(downP, downP - NmUp(0.05f))
                        && !nm.EditorSwipeDown(downP, downP + new Vector2(Screen.height * 0.3f, -Screen.height * 0.2f))
                        && !nm.EditorSwipeDown(downP, downP + NmUp(0.25f))
                        && !sky.Pinned && nm.EditorMode == "None", "짧게 · 옆으로 · 위로 민 것은 아래로 밀기로 안 받는다 (판이 안 뜸)");

            // 2) 진짜 손가락 — 위에서 아래로 화면 높이 25% 를 몇 프레임에
            Finger(downAt, new[] { -NmUp(0.25f) }, 6, null, null);
            Wait(() => pinnedTop() && frameAlpha() > 0.99f, 4f, "아래로 밀면 위쪽 고정 판");
            Sleep(0.3f);
            Shot("skw_down");
            Check(() => { log.Add("  아래로 민 뒤: " + state()); return pinnedTop() && frameAlpha() > 0.99f && !showing(); },
                "AR 화면을 위에서 아래로 밀면 — 휴대폰을 하늘로 들지 않아도 하늘 판이 화면 위쪽에 고정돼 펼쳐짐");
            Check(() => PsToast("위쪽에 고정했어요") && PlayerPrefs.GetInt(SwHintKey, 0) == 1, "처음 한 번 — 「위쪽에 고정했어요 · …」 치우는 법 안내");

            // 3) 이미 위쪽에 있으면 그대로 (판 아래 맨 AR 자리에서)
            Check(() =>
            {
                var p = SwFree(q => nm.EditorCanSwipeDown(q), new[] { 0.42f, 0.38f, 0.34f, 0.3f, 0.46f });
                bool again = p.x >= 0f && nm.EditorSwipeDown(p, p - NmUp(0.25f));
                log.Add($"  판 아래 자리 {(p.x >= 0f ? $"({p.x / Screen.width:0.00}, {p.y / Screen.height:0.00})" : "없음")} · 다시 받음 {again}");
                return p.x >= 0f && !again && pinnedTop();
            }, "이미 위쪽에 고정돼 있으면 다시 밀어도 그대로");

            // 4) 판을 치우고 — 위로 밀기는 그대로 「근처 장소」
            Do(() => sky.SetPinned(false));
            Wait(() => !sky.Pinned && frame.parent != pinLayer, 4f, "판을 치움");
            Sleep(1.2f);
            Finger(upAt, new[] { NmUp(0.25f) }, 6, null, null);
            Sleep(0.8f);
            Check(() =>
            {
                bool none = PsToast("근처에 보여 줄 장소가 없어요");
                log.Add($"  위로 민 뒤: {state()} · '장소 없음' 알림 {none}");
                return (nm.EditorMode == "Nearby" || none) && !sky.Pinned;
            }, "위로 밀면 그대로 「근처 장소」 (하늘 판은 안 뜸)");
            Shot("skw_up");
            Do(() => { if (nm.EditorMode == "Nearby") nm.EditorDismiss(); });
            Wait(() => nm.EditorMode == "None" && !nm.EditorCardOn, 4f, "근처 카드 닫힘");
            Sleep(2.5f);   // 알림이 걷히게

            // 5) 천천히(누르고 1초 넘게 있다가) 민 것은 안 받는다
            Do(() =>
            {
                var p = downAt();
                if (p == null || ptTouch == null) { slowP = new Vector2(-1f, -1f); return; }
                slowP = p.Value;
                InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Began, position = slowP });
            });
            Sleep(1.3f);
            Do(() => { if (slowP.x >= 0f) InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = slowP - NmUp(0.12f) }); });
            Sleep(0.05f);
            Do(() => { if (slowP.x >= 0f) InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = slowP - NmUp(0.25f) }); });
            Sleep(0.1f);
            Do(() => { if (slowP.x >= 0f) InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = slowP - NmUp(0.25f) }); });
            Sleep(0.8f);
            Check(() => slowP.x >= 0f && !sky.Pinned, "천천히(누르고 1초 넘게 있다가) 민 것은 안 받는다");

            // 6) 하늘을 껐으면 — 알려 주고 판은 안 뜸
            Do(() => R0926SkySettings.Enabled = false);
            Sleep(0.4f);
            Finger(downAt, new[] { -NmUp(0.25f) }, 6, null, null);
            Sleep(0.6f);
            Check(() => PsToast("하늘 화면이 꺼져 있어요") && !sky.Pinned, "하늘을 껐으면 「하늘 화면이 꺼져 있어요 · 목록 ▸ 설정에서 켤 수 있어요」 · 판은 안 뜸");
            Shot("skw_off");
            Do(() => R0926SkySettings.Enabled = true);
            Sleep(2.5f);

            // 7) 보일 것이 아직이면 (날씨를 켰는데 아직 못 받음) — 알려 주고 판은 안 뜸
            Do(() => typeof(R0926SkyWeather).GetField("hasData", F).SetValue(sky, false));
            Sleep(0.3f);
            Finger(downAt, new[] { -NmUp(0.25f) }, 6, null, null);
            Sleep(0.6f);
            Check(() => !sky.Ready && PsToast("하늘 정보를 불러오는 중이에요") && !sky.Pinned, "날씨를 아직 못 받았으면 「하늘 정보를 불러오는 중이에요」 · 판은 안 뜸");
            Do(() => typeof(R0926SkyWeather).GetField("hasData", F).SetValue(sky, true));
            Sleep(2.5f);

            // 8) 처음 안내가 떠 있는 동안 · 하늘을 올려다보는 동안엔 안 받는다
            Do(() => UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include)?.ForceShowGuide());
            Sleep(1.0f);
            Check(() => R0926GuideOverlay.BlocksWorld && downP.x >= 0f && !nm.EditorCanSwipeDown(downP), "처음 안내가 떠 있는 동안엔 안 받는다");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip());
            Sleep(0.9f);
            Do(() => AimCamera(-45f, 0f));
            Wait(() => showing(), 10f, "하늘을 보면 판");
            Check(() => showing() && downP.x >= 0f && !nm.EditorCanSwipeDown(downP) && !sky.Pinned, "하늘을 올려다봐 판이 하늘에 떠 있는 동안엔 안 받는다 (이미 보인다)");
            Do(() => AimCamera(8f, 0f));
            Wait(() => !showing(), 6f, "앞을 보면 하늘 판이 사라짐");
            Sleep(1.0f);

            // 9) 다시 아래로 밀면 또 펴진다 (안내는 처음 한 번만이라 이번엔 없음)
            Finger(downAt, new[] { -NmUp(0.25f) }, 6, null, null);
            Wait(() => pinnedTop() && frameAlpha() > 0.99f, 4f, "다시 아래로 밀면 위쪽 고정 판");
            Check(() => pinnedTop() && !PsToast("위쪽에 고정했어요"), "다시 아래로 밀어도 펴짐 · 치우는 법 안내는 처음 한 번만");
            Shot("skw_again");

            Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                SwFinish();
            });
        }
    }
}
