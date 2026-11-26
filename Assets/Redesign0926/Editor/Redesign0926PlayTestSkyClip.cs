using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 대표님 16번 「'하늘보기' 탭에서 좌우로 스와이프했을 때 배경탭이 발생한 경우에는 (화면고정) 해당 배경탭 내부에 발생한 내용이 좌우스와이프했을 때
        //  해당 네온배경(하늘보기탭) 내에서 좌우로 스와이프해야 하는데 지금은 이를 벗어나서 스와이프되고 있거든?」
        //  · 위쪽에 고정한 판 — 옆 장을 넘기는 중에도 장은 유리 좌우 안에서만 보인다 (틀 Frame0926 의 R0926PageClip)
        //  · 하늘에 걸린 동안은 자르지 않는다 (유리가 투명) · 유리 · 네온 테는 잘리지 않는다 · 📌 는 유리 위로 나와도 다 보인다
        //  · 유리 가장자리 · 📌 윗부분도 손가락을 그대로 받는다 (자르기가 누름을 막지 않는다)
        //  넘기는 중 = 장 자리(pos)를 사이 값으로 붙잡아 둔다. 디스플레이 · 하늘 설정 · 하늘 옆 장 서버 주소는 하늘 네온 시험과 같이 되돌린다 (SnFinish).
        private static void PlanSkyClip()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            R0926SkyWeather sky = null;
            R0926SkyPages pg = null;
            R0926SkyPages.Page[] pages = null;
            RectTransform frame = null, glass = null, pin = null;
            Transform pinLayer = null;
            Image glassImg = null, rimImg = null;
            R0926PageClip clip = null;
            Color glassFull = Color.clear;
            var fPos = typeof(R0926SkyPages).GetField("pos", F);
            var fVel = typeof(R0926SkyPages).GetField("vel", F);
            var fTarget = typeof(R0926SkyPages).GetField("target", F);
            var fDrag = typeof(R0926SkyPages).GetField("dragging", F);
            var fShown = typeof(R0926SkyPages).GetField("shown", F);
            var fClip = typeof(R0926SkyWeather).GetField("pageClip", F);
            Func<bool> showing = () => sky != null && (bool)typeof(R0926SkyWeather).GetField("skyShow", F).GetValue(sky);
            Func<float> skyAlpha = () => sky != null && typeof(R0926SkyWeather).GetField("skyGroup", F).GetValue(sky) is CanvasGroup g ? g.alpha : 0f;
            Func<float> glassK = () => glassImg != null && glassFull.a > 0f ? glassImg.color.a / glassFull.a : -1f;
            Func<bool> pinnedTop = () => sky != null && sky.PinnedTop && frame != null && frame.parent == pinLayer;
            Func<int> shownCount = () => pg != null && fShown.GetValue(pg) is List<int> l ? l.Count : 0;
            Action<float> hold = p => { fDrag.SetValue(pg, true); fPos.SetValue(pg, p); fVel.SetValue(pg, 0f); };
            Action release = () => { if (pg == null) return; fDrag.SetValue(pg, false); fTarget.SetValue(pg, 0); fPos.SetValue(pg, 0f); fVel.SetValue(pg, 0f); };

            // 캔버스 공간 좌우 — 자르는 칸(RectMask2D 의 칸 + padding) · 유리
            Func<RectTransform, Vector2> span = t =>
            {
                var c = t.GetComponentInParent<Canvas>();
                var w = new Vector3[4];
                t.GetWorldCorners(w);
                float a = float.MaxValue, b = float.MinValue;
                foreach (var p in w) { float x = c.rootCanvas.transform.InverseTransformPoint(p).x; a = Mathf.Min(a, x); b = Mathf.Max(b, x); }
                return new Vector2(a, b);
            };
            Func<Vector2> clipSpan = () => new Vector2(clip.canvasRect.xMin + clip.padding.x, clip.canvasRect.xMax - clip.padding.z);
            // 장 안의 그림 · 글자 중 지금 보이는 것 — 잘림이 걸렸는지
            Func<int, string> pageClipState = i =>
            {
                if (pages == null || i < 0 || i >= pages.Length || pages[i].root == null || !pages[i].root.gameObject.activeInHierarchy) return "꺼짐";
                int n = 0, on = 0;
                foreach (var g in pages[i].root.GetComponentsInChildren<Graphic>())
                {
                    if (!g.enabled || g.canvasRenderer.cull) continue;
                    n++;
                    if (g.canvasRenderer.hasRectClipping) on++;
                }
                return $"{on}/{n}";
            };
            Func<Vector2, GameObject> hitAt = sp =>
            {
                var es = EventSystem.current;
                if (es == null) return null;
                var res = new List<RaycastResult>();
                es.RaycastAll(new PointerEventData(es) { position = sp }, res);
                return res.Count > 0 ? res[0].gameObject : null;
            };
            Action<string> where = name =>
            {
                var g = SkyScreenRect(glass);
                if (!g.HasValue) return;
                log.Add($"  자르기 {name} 유리 x {g.Value.xMin:0}~{g.Value.xMax:0} · y(위부터) {Screen.height - g.Value.yMax:0}~{Screen.height - g.Value.yMin:0} (화면 {Screen.width}x{Screen.height})");
            };

            Do(() => { Backdrop("bg_sky.png"); DeviceFov(); });
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
                snStyle = SnRead(SnStyleKey); snNeon = SnRead(SnNeonKey); snDensity = SnRead(SnDensityKey);
                snNearby = R0926Nearby.EditorBase; snHunt = R0926HuntApi.EditorBase; snFriends = R0926FriendsMap.EditorBase;
                StashSkyPrefs();
                snRun = true;   // 되돌리기는 하늘 네온 시험과 같다 (SnFinish — 끝에서 · 오류로 끊겨도 실행기 끝에서)
                foreach (var k in new[] { "Sky_Enabled", "Sky_Weather", "Sky_Events" }) PlayerPrefs.SetInt(k, 1);
                PlayerPrefs.SetInt("Sky_Angle", 1);   // 28°
                PlayerPrefs.SetInt("Sky_StartCollapsed", 0);
                foreach (var k in new[] { "Sky_Swiped", "Sky_PinUsed" }) PlayerPrefs.SetInt(k, 1);   // 처음 안내 토스트 없이
                R0926Nearby.EditorBase = NmDeadStub;
                R0926HuntApi.EditorBase = NmDeadStub;
                R0926FriendsMap.EditorBase = NmDeadStub;
                sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                pg = UnityEngine.Object.FindAnyObjectByType<R0926SkyPages>(FindObjectsInactive.Include);
                if (sky == null || pg == null) return;
                typeof(R0926SkyWeather).GetField("collapsed", F)?.SetValue(sky, false);
                pages = typeof(R0926SkyPages).GetField("pages", F).GetValue(pg) as R0926SkyPages.Page[];
                pin = typeof(R0926SkyPages).GetField("pinButton", F).GetValue(pg) as RectTransform;
                frame = pg.EditorFrame;
                glass = frame != null ? frame.Find("Glass0926") as RectTransform : null;
                glassImg = glass != null ? glass.GetComponent<Image>() : null;
                rimImg = glass != null ? glass.Find("NeonRim0926")?.GetComponent<Image>() : null;
                clip = frame != null ? frame.GetComponent<R0926PageClip>() : null;
                glassFull = (Color)typeof(R0926SkyWeather).GetField("glassColor", F).GetValue(sky);
                pinLayer = Find("SkyPin0926");
                foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude)) t.gameObject.SetActive(false);
                InjectWeather();
                R0926MenuStyle.Droplet = false; R0926MenuStyle.Neon = true; R0926MenuStyle.Density = R0926MenuStyle.DefaultDensity;   // 네온 배경
            });
            Check(() => sky != null && pg != null && pages != null && frame != null && glassImg != null && rimImg != null && pin != null && pinLayer != null,
                "준비: 하늘 판 · 틀 · 유리 · 네온 테 · 📌 · 고정 자리");
            Check(() =>
            {
                log.Add($"  틀 자르기 {(clip != null ? clip.GetType().Name : "없음")} · 칸 {(clip != null && clip.Area != null ? clip.Area.name : "-")} · 하늘 판 연결 {(fClip.GetValue(sky) as R0926PageClip == clip && clip != null)} · 켜짐 {clip?.enabled}");
                return clip != null && clip.Area == glass && fClip.GetValue(sky) as R0926PageClip == clip;
            }, "장면: 틀(Frame0926)에 자르기(R0926PageClip) · 자르는 칸 = 유리 · 하늘 판(R0926SkyWeather.pageClip)에 연결");

            // 1) 하늘에 걸린 동안 — 자르지 않는다 (유리 투명 · 넘기면 판 밖으로 미끄러져 사라지는 예전 그대로)
            Do(() => AimCamera(-45f, 0f));
            Wait(() => showing() && skyAlpha() > 0.9f, 10f, "하늘을 보면 판");
            Wait(() => shownCount() >= 2, 6f, "옆 장 2장 이상 (날씨 · 라이브)");
            Sleep(0.4f);
            Check(() => { log.Add($"  하늘: 자르기 켜짐 {clip.enabled} · 유리 짙기 {glassK():0.00} · 장 {shownCount()}"); return !clip.enabled && glassK() < 0.01f; },
                "하늘에 걸린 판은 자르지 않는다 (유리가 투명한 동안)");

            // 2) 위쪽에 고정 — 유리(네온 배경)가 보이면 자르기 켬
            Do(() => sky.SetPinned(true, true));
            Wait(() => pinnedTop() && glassK() > 0.99f, 4f, "위쪽에 고정 — 유리가 다 짙어짐");
            Sleep(0.6f);
            Do(() => where("rest"));
            Shot("skc_rest");
            Check(() =>
            {
                var c = clipSpan();
                var g = span(glass);
                float k = (span(frame).y - span(frame).x) / Mathf.Max(1f, frame.rect.width);
                log.Add($"  자르기 켜짐 {clip.enabled} · 자르는 칸 x {c.x:0.0}~{c.y:0.0} · 유리 x {g.x:0.0}~{g.y:0.0} (캔버스) · 안쪽 {(c.x - g.x) / k:0.0} · {(g.y - c.y) / k:0.0} (틀 단위) · 위아래 padding {clip.padding.y:0} · {clip.padding.w:0} · 흐림 {clip.softness.x}");
                return clip.enabled && Mathf.Abs((c.x - g.x) / k - 14f) < 1f && Mathf.Abs((g.y - c.y) / k - 14f) < 1f && clip.padding.y < -1000f && clip.padding.w < -1000f;
            }, "고정하면 자르기 켬 — 자르는 칸은 유리 좌우에서 안쪽으로 14 · 위아래는 열어 둠");
            Check(() =>
            {
                log.Add($"  유리 잘림 대상 {glassImg.maskable} · 잘림 {glassImg.canvasRenderer.hasRectClipping} · 테 잘림 대상 {rimImg.maskable} · 잘림 {rimImg.canvasRenderer.hasRectClipping}");
                return !glassImg.maskable && !rimImg.maskable && !glassImg.canvasRenderer.hasRectClipping && !rimImg.canvasRenderer.hasRectClipping;
            }, "유리 · 네온 테는 자르지 않는다 (가장자리가 같이 옅어지지 않게)");
            Check(() =>
            {
                var c = clipSpan();
                var p = span(pin);
                var pr = SkyScreenRect(pin);
                var gr = SkyScreenRect(glass);
                log.Add($"  📌 x {p.x:0.0}~{p.y:0.0} (자르는 칸 {c.x:0.0}~{c.y:0.0}) · 📌 위 끝이 유리 위로 {(pr.HasValue && gr.HasValue ? pr.Value.yMax - gr.Value.yMax : 0f):0} px");
                return p.x >= c.x && p.y <= c.y;
            }, "📌 는 자르는 칸 좌우 안 — 유리 위로 나온 부분도 다 보인다 (위아래는 안 자름)");

            // 3) 넘기는 중 (장 자리 0.35 — 날씨 장은 왼쪽으로 · 라이브 장이 오른쪽에서) — 장은 유리 안에서만
            Do(() => hold(0.35f));
            Sleep(0.4f);
            Do(() => where("drag"));
            Shot("skc_drag");
            Check(() =>
            {
                int a = -1, b = -1;
                if (fShown.GetValue(pg) is List<int> l && l.Count >= 2) { a = l[0]; b = l[1]; }
                string sa = pageClipState(a), sb = pageClipState(b);
                log.Add($"  넘기는 중: 첫 장({(a >= 0 ? pages[a].kind.ToString() : "-")}) 잘림 {sa} · 둘째 장({(b >= 0 ? pages[b].kind.ToString() : "-")}) 잘림 {sb}");
                bool all(string s) { var q = s.Split('/'); return q.Length == 2 && q[0] == q[1] && q[1] != "0"; }
                return a >= 0 && b >= 0 && all(sa) && all(sb);
            }, "넘기는 중 — 두 장의 그림 · 글자 모두 자르기가 걸린다 (유리 밖은 안 보임)");
            Check(() =>
            {
                var g = SkyScreenRect(glass);
                var pr = SkyScreenRect(pin);
                if (!g.HasValue || !pr.HasValue) return false;
                var edge = hitAt(new Vector2(g.Value.xMin + 4f, g.Value.center.y));
                var edgeR = hitAt(new Vector2(g.Value.xMax - 4f, g.Value.center.y));
                var top = hitAt(new Vector2(pr.Value.center.x, pr.Value.yMax - 4f));
                log.Add($"  누름: 유리 왼쪽 끝 {(edge != null ? edge.name : "없음")} · 오른쪽 끝 {(edgeR != null ? edgeR.name : "없음")} · 📌 위 끝 {(top != null ? top.name : "없음")}");
                return edge != null && edge.transform.IsChildOf(frame) && edgeR != null && edgeR.transform.IsChildOf(frame)
                       && top != null && top.transform.IsChildOf(pin);
            }, "자르기가 누름을 막지 않는다 — 유리 좌우 끝 · 유리 위로 나온 📌 윗부분");

            // (견주기) 고치기 전 — 자르기 없이 같은 자리
            Do(() => { fClip.SetValue(sky, null); clip.enabled = false; });
            Sleep(0.3f);
            Shot("skc_drag_old");
            Do(() => fClip.SetValue(sky, clip));
            Wait(() => clip.enabled, 2f, "(견주기 끝) 자르기 다시 켜짐");

            // 4) 손을 떼면 제자리 · 고정을 풀면 자르기 끔
            Do(() => release());
            Sleep(0.5f);
            Do(() => sky.SetPinned(false));
            Wait(() => !sky.Pinned && glassK() < 0.01f, 4f, "고정 풂 — 유리가 사라짐");
            Sleep(0.3f);
            Check(() => { log.Add($"  풀고 나서: 자르기 켜짐 {clip.enabled} · 유리 짙기 {glassK():0.00}"); return !clip.enabled; }, "고정을 풀면 자르기 끔 (하늘에선 예전 그대로)");
            Do(() => { release(); SnFinish(); });
        }
    }
}
