using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 M2 — 대표님 「하단 메뉴 바로 위 '장소 표시' 칩 — 기본은 항상 지역명(이모지 그대로) · 누르면 좌표로 자연스럽게 바뀜」.
    ///  · locchip : 가상 위치를 서울시청(공개 자리)으로 옮겨 칩에 지역 이름이 뜨면 — 기본은 지역 이름뿐(좌표 없음) · 초록 핀 그대로 · 칩 전체를 누를 수 있다 ·
    ///    가상 손가락으로 칩을 누르면(진짜 누르기 길 — 이벤트 시스템) 글자가 흐려졌다가 칩 폭이 여러 프레임에 걸쳐 바뀌고 좌표가 또렷해진다(폭이 바뀌는 동안 글자는 숨어 있다) ·
    ///    5초쯤 뒤 지역 이름으로 돌아온다 · 좌표일 때 또 누르면 바로 지역 이름 · 바뀌는 중에 또 눌러도 흐린 채로 남지 않는다 ·
    ///    앱으로 돌아오면 지역 이름부터 · 주소를 못 찾았을 땐 좌표뿐이고 눌러도 그대로.
    ///    칩 글자(지역 이름 · 좌표)는 기록에 남기지 않는다 — 맞는지(참/거짓)만. 서버에 쓰는 것 없음 (주소 찾기는 우팡 서버 공개 GET)
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanLocChip()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            const string Hall = "37.5665, 126.9780";   // 서울시청 — 공개 자리
            var coordPat = new Regex(@"^-?\d{1,3}\.\d{4}, -?\d{1,3}\.\d{4}$");
            LocationManager lm = null;
            Text txt = null;
            Image pin = null;
            RectTransform chip = null;
            CanvasGroup chipGroup = null;
            float tapAt = -1f, backAt = -1f, minAlpha = 1f, w0 = 0f;
            int lastFrame = -1;
            var samples = new List<(float w, float a)>();

            T Get<T>(string name) => lm != null && typeof(LocationManager).GetField(name, NP)?.GetValue(lm) is T v ? v : default;
            string Shown() => txt != null ? txt.text : "";
            string Region() => Get<string>("chipRegion");
            string Coords() => Get<string>("chipCoords");
            bool Morphing() => Get<Coroutine>("chipMorph") != null;
            bool CoordsMode() => Get<bool>("chipShowCoords");
            bool Live() => Get<bool>("chipLive");
            float Alpha() => txt != null ? txt.canvasRenderer.GetAlpha() : 0f;
            float Held() => txt != null && txt.TryGetComponent(out LayoutElement le) ? le.preferredWidth : -1f;
            bool Settled() => !Morphing() && Alpha() >= 0.999f;
            bool ShowsRegion() => Region() != null && Shown() == Region() && !coordPat.IsMatch(Shown()) && Settled();
            bool ShowsCoords() => Coords() != null && Shown() == Coords() && coordPat.IsMatch(Shown()) && Settled();
            bool PinAsIs() => pin != null && pin.gameObject.activeInHierarchy && pin.sprite != null && pin.sprite == Get<Sprite>("successSprite");

            // 가상 손가락으로 칩 가운데를 톡 — 누르고 두 프레임 뒤에 뗀다 (이벤트 시스템이 받아 칩의 Button 으로)
            void TapChip()
            {
                int f = -1;
                Vector2 p = default;
                steps.Enqueue(() =>
                {
                    if (f < 0)
                    {
                        if (ptTouch == null || chip == null || !chip.gameObject.activeInHierarchy) { log.Add("  누를 칩 없음"); return true; }
                        p = ScreenRect(chip).center;
                        InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Began, position = p });
                        f = Time.frameCount;
                    }
                    if (Time.frameCount < f + 2) return false;
                    InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = p });
                    tapAt = Time.unscaledTime;
                    return true;
                });
            }

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                lm = UnityEngine.Object.FindAnyObjectByType<LocationManager>(FindObjectsInactive.Include);
                txt = lm != null ? typeof(LocationManager).GetField("infoText", NP)?.GetValue(lm) as Text : null;
                pin = lm != null ? typeof(LocationManager).GetField("statusImage", NP)?.GetValue(lm) as Image : null;
                chip = Find("LocationManagerPanel") as RectTransform;
                chipGroup = chip != null ? chip.GetComponent<CanvasGroup>() : null;
            });
            Wait(() => Live(), 20f, "칩에 처음 위치가 뜸");
            Do(() =>
            {
                VirtualLocation.Instance?.SetCoordinates(37.5665f, 126.9780f);   // 칩 사진에 개인 위치가 찍히지 않게
                lm?.RequestLocationUpdate();
                ptFocusWas = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;   // 에디터 창이 뒤에 있어도
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Wait(() => Live() && Coords() == Hall && ShowsRegion(), 20f, "칩에 서울시청 지역 이름이 뜸 (우팡 서버 주소 찾기)");
            Wait(() => chipGroup == null || chipGroup.alpha > 0.99f, 5f, "칩 보임");
            Sleep(0.4f);

            // 1) 기본 — 지역 이름뿐 · 초록 핀 그대로 · 칩 전체를 누를 수 있다
            Check(() =>
            {
                var b = chip != null ? chip.GetComponent<Button>() : null;
                var img = chip != null ? chip.GetComponent<Image>() : null;
                bool wired = b != null && b.onClick.GetPersistentEventCount() == 1 && b.onClick.GetPersistentMethodName(0) == "ToggleChipCoords" && b.onClick.GetPersistentTarget(0) == lm;
                bool hit = img != null && img.raycastTarget;
                log.Add($"  칩: 지역 이름 {ShowsRegion()} · 좌표 꼴 {coordPat.IsMatch(Shown())} · 핀 그대로 {PinAsIs()} · 누르기 연결 {wired} · 누를 수 있음 {hit}");
                return ShowsRegion() && PinAsIs() && wired && hit;
            }, "기본은 지역 이름뿐(좌표 없음) · 초록 핀 그대로 · 칩 전체를 누를 수 있음");
            Shot("locchip_region");

            // 2) 누르면 좌표 — 흐려짐 → 폭 → 또렷 (한 번에 툭 바뀌지 않음)
            Do(() => { w0 = chip.rect.width; samples.Clear(); minAlpha = 1f; lastFrame = -1; });
            TapChip();
            steps.Enqueue(() =>
            {
                if (Time.frameCount == lastFrame) return false;   // 한 프레임에 한 번
                lastFrame = Time.frameCount;
                minAlpha = Mathf.Min(minAlpha, Alpha());
                samples.Add((chip.rect.width, Alpha()));
                return (ShowsCoords() && samples.Count > 3) || Time.unscaledTime - tapAt > 3f;
            });
            Check(() =>
            {
                float w1 = chip.rect.width;
                bool Mid(float w) => Mathf.Abs(w - w0) > 1f && Mathf.Abs(w - w1) > 1f;   // 처음 폭도 끝 폭도 아닌 프레임
                int mid = samples.Count(s => Mid(s.w));
                int spill = samples.Count(s => Mid(s.w) && s.a > 0.05f);
                log.Add($"  누른 뒤 {Time.unscaledTime - tapAt:0.00}초 ({samples.Count}프레임): 좌표 꼴 {ShowsCoords()} · 가장 흐릴 때 {minAlpha:0.00} · 칩 폭 {w0:0} → {w1:0} · 사이 폭 {mid}프레임(그때 글자 보임 {spill}) · 핀 그대로 {PinAsIs()}");
                return ShowsCoords() && minAlpha < 0.05f && mid >= 2 && spill == 0 && Mathf.Abs(w1 - w0) > 20f && PinAsIs();
            }, "한 번 누르면 좌표로 — 글자가 흐려졌다가 칩 폭이 여러 프레임에 걸쳐 바뀌고 좌표가 또렷해진다 (핀 그대로 · 폭이 바뀌는 동안 글자는 숨음)");
            Check(() => !Active("FullScreenPanel") && !Active("ListPanel"), "칩을 눌러도 다른 창은 열리지 않음");
            Shot("locchip_coords");

            // 3) 5초쯤 뒤 저절로 지역 이름
            Do(() => backAt = -1f);
            Wait(() => { if (backAt < 0f && !CoordsMode()) backAt = Time.unscaledTime; return ShowsRegion(); }, 9f, "몇 초 뒤 지역 이름으로 돌아옴");
            Check(() =>
            {
                float shown = backAt - tapAt;
                log.Add($"  좌표를 보인 시간 {shown:0.00}초 (5초 뒤 돌아오게)");
                return ShowsRegion() && shown > 4.6f && shown < 5.8f;
            }, "좌표는 5초쯤 뒤 저절로 지역 이름으로 돌아온다 (기본은 늘 지역 이름)");

            // 4) 좌표일 때 또 누르면 바로 지역 이름
            TapChip();
            Wait(() => ShowsCoords(), 3f, "다시 누름 → 좌표");
            Sleep(0.5f);
            TapChip();
            Wait(() => ShowsRegion(), 3f, "좌표일 때 또 누름 → 지역 이름");
            Check(() =>
            {
                float after = Time.unscaledTime - tapAt;
                log.Add($"  또 누른 뒤 {after:0.00}초에 지역 이름");
                return ShowsRegion() && after < 1.5f;
            }, "좌표일 때 또 누르면 기다리지 않고 바로 지역 이름으로");

            // 5) 바뀌는 중에 또 누름 — 흐린 채로 남지 않는다
            Sleep(0.4f);
            TapChip();
            Sleep(0.05f);
            TapChip();
            Wait(() => !Morphing(), 3f, "바뀌는 중에 또 누름 → 끝남");
            Sleep(0.2f);
            Check(() =>
            {
                log.Add($"  끝: 지역 이름 {ShowsRegion()} · 또렷함 {Alpha():0.00} · 폭 잡기 풀림 {Held() < 0f}");
                return ShowsRegion() && Alpha() >= 0.999f && Held() < 0f;
            }, "바뀌는 중에 또 눌러도 흐린 채로 남지 않고 지역 이름으로 (칩 폭도 글자에 맞게 풀림)");

            // 6) 좌표를 보던 중 앱을 나갔다 오면 지역 이름부터
            Sleep(0.4f);
            TapChip();
            Wait(() => ShowsCoords(), 3f, "누름 → 좌표");
            Do(() => typeof(LocationManager).GetMethod("OnApplicationFocus", NP)?.Invoke(lm, new object[] { true }));
            Wait(() => Live() && ShowsRegion(), 10f, "앱으로 돌아옴 → 지역 이름");
            Check(() => ShowsRegion() && !CoordsMode(), "좌표를 보던 중 앱을 나갔다 오면 다시 지역 이름부터");

            // 7) 주소를 못 찾았을 땐 좌표뿐 · 눌러도 그대로
            Do(() => typeof(LocationManager).GetMethod("ShowCoordsOnly", NP)?.Invoke(lm, new object[] { 37.5665f, 126.9780f }));
            Wait(() => ShowsCoords(), 3f, "주소 못 찾음 → 좌표뿐");
            TapChip();
            Sleep(0.6f);
            Check(() => ShowsCoords() && !CoordsMode() && !Morphing(), "주소를 못 찾았을 땐 좌표뿐이고 눌러도 그대로");
            Do(() => lm?.RequestLocationUpdate());
            Wait(() => Live() && ShowsRegion(), 10f, "다시 묻기 → 지역 이름");
            Check(() => ShowsRegion(), "다시 주소를 찾으면 지역 이름으로");

            Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
            });
        }
    }
}
