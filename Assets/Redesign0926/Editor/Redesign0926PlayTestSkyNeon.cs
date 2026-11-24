using System;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-24 대표님 「하늘 위쪽으로 고정햇을 때 네온모드 작업해줘」 — 고정한 하늘 판 유리(Glass0926) 둘레의 네온 테(NeonRim0926)
        //  하늘에 걸린 동안은 안 보이고 · 위쪽에 고정하면(밀기 · 노치) 유리와 같이 나타나고 · 네온 모드에서만 (물방울만 · 둘 다 끔이면 없음) · 풀면 사라진다.
        //  디스플레이 설정(물방울 · 네온 · 진하기) · 하늘 설정 저장값은 되돌린다. 하늘 옆 장의 서버 주소는 닿지 않는 주소로 (운영 서버에 묻지 않게).
        private static bool snRun;
        private static string snNearby, snHunt, snFriends;
        private static int snStyle = -1, snNeon = -1, snDensity = -1;   // -1 = 저장값 없었음
        private const string SnStyleKey = "r0926_menu_style", SnNeonKey = "r0926_neon", SnDensityKey = "r0926_window_density";

        private static int SnRead(string k) => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : -1;
        private static void SnWrite(string k, int v) { if (v < 0) PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, v); }

        // 끝에서 한 번, 오류로 끊겨도 실행기 끝에서 한 번
        private static void SnFinish()
        {
            if (!snRun) return;
            snRun = false;
            var sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
            if (sky != null && sky.Pinned) sky.SetPinned(false);
            R0926Nearby.EditorBase = snNearby;
            R0926HuntApi.EditorBase = snHunt;
            R0926FriendsMap.EditorBase = snFriends;
            SnWrite(SnStyleKey, snStyle);
            SnWrite(SnNeonKey, snNeon);
            SnWrite(SnDensityKey, snDensity);
            PlayerPrefs.Save();
            R0926MenuStyle.Reload();
            RestoreSkyPrefs();
        }

        private static void PlanSkyNeon()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            R0926SkyWeather sky = null;
            R0926SkyPages pg = null;
            RectTransform frame = null, glass = null;
            Transform pinLayer = null;
            Image glassImg = null, rimImg = null;
            R0926WindowSkin rim = null;
            R0926Glass drop = null;
            Color glassFull = Color.clear;
            Func<bool> showing = () => sky != null && (bool)typeof(R0926SkyWeather).GetField("skyShow", F).GetValue(sky);
            Func<float> skyAlpha = () => sky != null && typeof(R0926SkyWeather).GetField("skyGroup", F).GetValue(sky) is CanvasGroup g ? g.alpha : 0f;
            Func<float> rimA = () => rimImg != null ? rimImg.color.a : -1f;
            Func<float> glassK = () => glassImg != null && glassFull.a > 0f ? glassImg.color.a / glassFull.a : -1f;
            Func<bool> pinnedTop = () => sky != null && sky.PinnedTop && frame != null && frame.parent == pinLayer;
            Action<bool, bool> style = (d, n) => { R0926MenuStyle.Droplet = d; R0926MenuStyle.Neon = n; R0926MenuStyle.Density = 70; };
            Func<string> state = () =>
            {
                var c = rim != null ? rim.GetComponentInParent<Canvas>() : null;
                return $"테 {(rim != null && rim.Showing ? "그림" : "안 그림")} · 테 짙기 {rimA():0.00} · 유리 짙기 {glassK():0.00} · 물방울 유리 {(drop != null && drop.Showing ? "켬" : "끔")}"
                       + $" · 캔버스 {(c == null ? "-" : c.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? "화면" : "하늘(월드)")}";
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
                snRun = true;
                foreach (var k in new[] { "Sky_Enabled", "Sky_Weather", "Sky_Events" }) PlayerPrefs.SetInt(k, 1);
                PlayerPrefs.SetInt("Sky_Angle", 1);   // 28°
                PlayerPrefs.SetInt("Sky_StartCollapsed", 0);
                foreach (var k in new[] { "Sky_Swiped", "Sky_PinUsed" }) PlayerPrefs.SetInt(k, 1);   // 처음 안내 토스트 없이 (판 모양만 본다)
                R0926Nearby.EditorBase = NmDeadStub;
                R0926HuntApi.EditorBase = NmDeadStub;
                R0926FriendsMap.EditorBase = NmDeadStub;
                sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                pg = UnityEngine.Object.FindAnyObjectByType<R0926SkyPages>(FindObjectsInactive.Include);
                if (sky == null || pg == null) return;
                typeof(R0926SkyWeather).GetField("collapsed", F)?.SetValue(sky, false);
                frame = pg.EditorFrame;
                glass = frame != null ? frame.Find("Glass0926") as RectTransform : null;
                glassImg = glass != null ? glass.GetComponent<Image>() : null;
                drop = glass != null ? glass.GetComponent<R0926Glass>() : null;
                var rt = glass != null ? glass.Find("NeonRim0926") : null;
                rim = rt != null ? rt.GetComponent<R0926WindowSkin>() : null;
                rimImg = rt != null ? rt.GetComponent<Image>() : null;
                glassFull = (Color)typeof(R0926SkyWeather).GetField("glassColor", F).GetValue(sky);
                pinLayer = Find("SkyPin0926");
                foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude)) t.gameObject.SetActive(false);
                InjectWeather();
                style(false, true);
            });
            Check(() => sky != null && pg != null && frame != null && glassImg != null && pinLayer != null && rim != null && rimImg != null,
                "준비: 하늘 판 · 틀 · 유리 · 고정 자리 · 유리 둘레 네온 테(NeonRim0926)");
            Check(() =>
            {
                var wired = typeof(R0926SkyWeather).GetField("glassRim", F)?.GetValue(sky) as Image;
                log.Add($"  테: 종류 {rim.SkinKind} · 모양 {(typeof(R0926WindowSkin).GetField("shapeFrom", F).GetValue(rim) as Image)?.name} · 누름 {(rimImg.raycastTarget ? "받음" : "안 받음")} · 차례 {rim.transform.GetSiblingIndex()}");
                return wired == rimImg && rim.SkinKind == R0926WindowSkin.Kind.Rim && !rimImg.raycastTarget
                       && (typeof(R0926WindowSkin).GetField("shapeFrom", F).GetValue(rim) as Image) == glassImg;
            }, "장면: 테는 유리 모양을 따르는 네온 테 · 누름은 유리가 그대로 받음 · 하늘 판(R0926SkyWeather.glassRim)에 연결");

            // 1) 하늘에 걸린 동안 — 테 안 보임 (유리도 투명)
            Do(() => AimCamera(-45f, 0f));
            Wait(() => showing() && skyAlpha() > 0.9f, 10f, "하늘을 보면 판");
            Sleep(0.8f);
            Shot("skn_sky");
            Check(() => { log.Add("  하늘: " + state()); return !sky.Pinned && rimA() < 0.01f && glassK() < 0.01f; },
                "하늘에 걸린 판에는 테가 안 보인다 (유리처럼 투명 · 고정했을 때만)");

            // 2) 위쪽에 고정 — 네온 모드: 유리와 같이 테가 나타남
            Do(() => sky.SetPinned(true, true));
            Wait(() => pinnedTop() && glassK() > 0.99f, 4f, "위쪽에 고정 — 유리가 다 짙어짐");
            Sleep(0.6f);
            Shot("skn_pinned_neon");
            Check(() => { log.Add("  고정 · 네온: " + state()); return rim.Showing && rimA() > 0.99f && !drop.Showing; },
                "네온 모드 — 위쪽에 고정한 판 유리 둘레에 네온 테 (유리와 같이 다 짙어짐)");
            Check(() =>
            {
                var g = SkyScreenRect(glass);
                var r = SkyScreenRect((RectTransform)rim.transform);
                log.Add($"  유리 x {g?.xMin:0}~{g?.xMax:0} · y {g?.yMin:0}~{g?.yMax:0} · 테 x {r?.xMin:0}~{r?.xMax:0} · y {r?.yMin:0}~{r?.yMax:0} · 화면 {Screen.width}x{Screen.height}");
                return g.HasValue && r.HasValue && Mathf.Abs(g.Value.xMin - r.Value.xMin) < 1f && Mathf.Abs(g.Value.yMax - r.Value.yMax) < 1f
                       && g.Value.xMin >= 0f && g.Value.xMax <= Screen.width && g.Value.yMax <= Screen.height;
            }, "테가 유리와 같은 자리 · 크기 (판이 화면 안)");

            // 3) 디스플레이 선택을 바꾸면 — 둘 다 끔 · 물방울만이면 테 없음, 둘 다면 물방울 유리 + 테
            Do(() => style(false, false));
            Sleep(0.6f);
            Shot("skn_pinned_classic");
            Check(() => { log.Add("  고정 · 둘 다 끔: " + state()); return !rim.Showing && !drop.Showing && pinnedTop(); }, "둘 다 끔 — 테 없음 (예전 어두운 유리 그대로)");
            Do(() => style(true, false));
            Sleep(0.6f);
            Shot("skn_pinned_droplet");
            Check(() => { log.Add("  고정 · 물방울만: " + state()); return !rim.Showing && drop.Showing; }, "물방울만 — 물방울 유리 · 테 없음");
            Do(() => style(true, true));
            Sleep(0.6f);
            Shot("skn_pinned_both");
            Check(() => { log.Add("  고정 · 둘 다: " + state()); return rim.Showing && drop.Showing && rimA() > 0.99f; }, "둘 다 — 물방울 유리 위에 네온 테");
            Do(() => style(false, true));
            Sleep(0.4f);

            // 4) 아래로 풀면 — 하늘로 돌아가며 테도 사라짐
            Do(() => sky.SetPinned(false));
            Wait(() => frame.parent != pinLayer, 4f, "풀면 하늘 자리로");
            Sleep(0.4f);
            Check(() => { log.Add("  풀고 하늘: " + state()); return !sky.Pinned && rimA() < 0.01f && glassK() < 0.01f; }, "풀면 하늘 판에 테가 안 보인다");

            // 5) 하늘을 안 보고 노치를 눌러 위쪽에 편 판도 — 네온 테
            Do(() => AimCamera(0f, 0f));
            Sleep(1.5f);
            Do(() => sky.UnfoldFromNotch(new Vector2(Screen.width * 0.5f, Screen.height - 40f)));
            Wait(() => pinnedTop() && glassK() > 0.99f, 4f, "노치에서 나와 위쪽에 고정");
            Sleep(0.6f);
            Shot("skn_notch_neon");
            Check(() => { log.Add("  노치에서 편 판: " + state()); return rim.Showing && rimA() > 0.99f; }, "노치를 눌러 위쪽에 편 판도 네온 테");
            Do(() => sky.SetPinned(false));
            Wait(() => frame.parent != pinLayer, 4f, "아래로 풀면 사라짐");
            Check(() => rimA() < 0.01f, "사라질 때 테도 같이 사라짐");

            Do(SnFinish);
        }
    }
}
