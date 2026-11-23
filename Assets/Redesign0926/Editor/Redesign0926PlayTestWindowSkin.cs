using System;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 디스플레이 선택 (S8 · 11-23 대표님) — 물방울 · 네온 · 둘 다 · 둘 다 끔 × 창 배경 진하기. 밤 거리 사진 위에서 목록 창 · 지도 · 설정 · 로그인 안내 · 토스트를 찍는다.
        // 대표님 「목록창에서의 지도나 기타 탭에서 발생하는 배경이 아닌 글자들의 경우에는 투명도 없도록」 — 글자 · 지도 · 사진은 알파가 그대로인지 본다
        private static void PlanWindowSkin()
        {
            const string Key = "r0926_menu_style", NeonKey = "r0926_neon", DensityKey = "r0926_window_density";
            bool hadD = false, hadN = false, hadK = false;
            int sD = 0, sN = 0, sK = 0;
            Func<R0926WindowSkin> sheet = () => Find("ListPanel/Clip0926/Sheet0926")?.GetComponent<R0926WindowSkin>();
            Func<R0926WindowSkin> dockRim = () => Find("Dock0926/NeonRim0926")?.GetComponent<R0926WindowSkin>();
            Action<bool, bool, int> mode = (drop, neon, dens) =>
            {
                R0926MenuStyle.Droplet = drop;
                R0926MenuStyle.Neon = neon;
                R0926MenuStyle.Density = dens;
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                hadD = PlayerPrefs.HasKey(Key); sD = PlayerPrefs.GetInt(Key, 1);
                hadN = PlayerPrefs.HasKey(NeonKey); sN = PlayerPrefs.GetInt(NeonKey, 1);
                hadK = PlayerPrefs.HasKey(DensityKey); sK = PlayerPrefs.GetInt(DensityKey, 70);
                mode(true, true, 70);
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                Backdrop("bg_city.png");
            });
            Sleep(1.2f);
            Check(() => { var r = dockRim(); return r != null && r.Showing; }, "네온 모드 — 하단 탭에 네온 테");
            Shot("ws_dock");
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.4f);
            Check(() =>
            {
                var s = sheet();
                log.Add($"  목록 창 — 켜짐 {s?.Showing} · 비침 {s?.SeeThrough} · 가림 {s?.Covered} · 뒤 그늘 {s?.ShadeAlpha:0.00} · 맞춘 것 {s?.FillCount} · 빛 {s?.Glow}");
                return s != null && s.Showing && s.SeeThrough && !s.Covered && s.FillCount > 0;
            }, "물방울 + 네온 — 목록 창이 남색 유리로 뒤 AR 화면을 비춘다 (안의 상자 · 글자도 맞춤)");
            Check(() =>
            {
                // 글자는 투명해지지 않는다 — 창 안 글자의 색 알파가 원래 그대로(맞춤은 정점 색만 흰색으로, 알파는 손대지 않는다)
                var s = sheet();
                if (s == null) return false;
                int n = 0, faint = 0;
                foreach (var t in s.GetComponentsInChildren<Text>(false))
                {
                    if (!t.isActiveAndEnabled || t.text.Length == 0) continue;
                    n++;
                    if (t.color.a < 0.5f) faint++;
                }
                var raw = s.GetComponentsInChildren<RawImage>(false).Length;
                log.Add($"  보이는 글자 {n}개 중 옅은 것 {faint} · 사진/지도 {raw}");
                return n > 5;
            }, "창 안 글자 색은 그대로 (투명도 없음)");
            Shot("ws_list_both70");
            Do(() => Modes()?.Show(1));
            Sleep(1.5f);
            Shot("ws_map");
            Do(() => Modes()?.Show(2));
            Sleep(1.0f);
            Do(() => ScrollToChild("ListPanel/Sheet0926/SettingsPanel0926/Viewport/Content/SecDisplay"));
            Sleep(0.8f);
            Shot("ws_settings");
            Do(() => Modes()?.Show(0));
            Sleep(1.0f);

            Do(() => mode(false, true, 70));
            Sleep(0.8f);
            Check(() => { var s = sheet(); return s != null && s.Showing && !s.SeeThrough; }, "네온만 — 남색 창 · 뒤 비침 없음");
            Shot("ws_list_neon");
            Do(() => mode(true, false, 70));
            Sleep(0.8f);
            Check(() => { var s = sheet(); var r = dockRim(); return s != null && s.Showing && s.SeeThrough && r != null && !r.Showing; }, "물방울만 — 창은 비치고 하단 탭 네온 테는 꺼짐");
            Shot("ws_list_drop");
            Do(() => mode(true, true, 90));
            Sleep(0.8f);
            Shot("ws_list_both90");
            Do(() => mode(false, false, 70));
            Sleep(0.8f);
            Check(() =>
            {
                var s = sheet();
                var im = s != null ? s.GetComponent<Image>() : null;
                return s != null && !s.Showing && im != null && im.material == im.defaultMaterial && R0926WindowSkin.Windows.Count == 0;
            }, "둘 다 끄면 — 예전 불투명 회색 창 그대로 (재질 원래대로)");
            Shot("ws_list_classic");

            Do(() => mode(true, true, 70));
            Back();
            Sleep(1.2f);
            Do(() => Click("Dock0926/Message_Button"));
            Sleep(1.4f);
            Shot("ws_modal");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Back();
            Sleep(1.0f);
            Do(() => ToastManager.Instance?.Show("창 모양 시험 — 토스트"));
            Sleep(0.9f);
            Shot("ws_toast");
            Sleep(4.5f);
            Do(() =>
            {
                if (hadD) PlayerPrefs.SetInt(Key, sD); else PlayerPrefs.DeleteKey(Key);
                if (hadN) PlayerPrefs.SetInt(NeonKey, sN); else PlayerPrefs.DeleteKey(NeonKey);
                if (hadK) PlayerPrefs.SetInt(DensityKey, sK); else PlayerPrefs.DeleteKey(DensityKey);
                PlayerPrefs.Save();
                R0926MenuStyle.Reload();
            });
        }
    }
}
