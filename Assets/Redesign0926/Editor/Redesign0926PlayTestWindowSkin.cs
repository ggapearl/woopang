using System;
using System.Collections.Generic;
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
                mode(true, true, R0926MenuStyle.DefaultDensity);
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
            Shot("ws_list_both75");
            // 5번 (11-26 대표님 「메뉴버튼에서 분류 선택할 때 '전체' 버튼을 누르면 해당 분류 아래로 나오는 것들이 투명하게 나와서 오히려 겹쳐보이거든? 이 때 발생하는 배경은 투명하면 안될 것 같아」)
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(1.0f);
            Shot("ws_catmenu");
            Check(() =>
            {
                R0926CategoryMenu cm = null;
                foreach (var x in UnityEngine.Object.FindObjectsByType<R0926CategoryMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (x.IsOpen) cm = x;
                var card = cm != null ? typeof(R0926CategoryMenu).GetField("card", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(cm) as RectTransform : null;
                var ci = card != null ? card.GetComponent<Image>() : null;
                var names = new List<string>();
                int nb = 0;
                int fd = card != null ? WsFaded(card, names, out nb) : -1;
                float a = ci != null ? WsDrawnAlpha(ci) : 0f;
                bool keep = card != null && card.GetComponent<R0926KeepSolid>() != null;
                var sk = ci != null ? ci.GetComponent<R0926WindowSkin>() : null;
                var s = sheet();
                // 37번부터 분류 고르기는 화면 맨 위 층의 가운데 카드 — 카드가 제 창 모양(목록 위라 늘 불투명 남색)이고 안의 칸은 칸 불투명
                log.Add($"  분류 고르기 창 — 열림 {cm != null} · 층 {(cm != null && cm.Layer != null ? cm.Layer.transform.parent.name : "-")} · 카드 알파 {(ci != null ? ci.color.a : 0f):0.00} ({a:0.00})"
                        + $" · 창 모양 {(sk == null ? "없음" : sk.Showing ? "켜짐" : "꺼짐")} · 비침 {sk?.SeeThrough} · 가림(불투명 남색) {sk?.Covered} · 칸 불투명 {keep}"
                        + $" · 안의 상자 {nb}개 중 옅어진 것 {fd}{(names.Count > 0 ? " (" + string.Join(", ", names) + ")" : "")} · 목록 창 비침 {s?.SeeThrough}");
                return cm != null && ci != null && ci.color.a >= 0.95f && a >= 0.95f && sk != null && sk.Showing && !sk.SeeThrough && sk.Covered
                       && nb >= 9 && fd == 0 && keep && s != null && s.SeeThrough;
            }, "물방울 모드 — 분류 고르기 창 카드는 불투명 남색 · 안의 칸 9개도 옅어지지 않음 (뒤 목록 줄이 비쳐 겹치지 않게) · 목록 창 바탕은 그대로 비침 (11-26 대표님 5번 · 37번)");
            Do(() =>
            {
                foreach (var x in UnityEngine.Object.FindObjectsByType<R0926CategoryMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (x.IsOpen) x.Close();
            });
            Sleep(0.6f);
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

            Do(() => mode(false, true, R0926MenuStyle.DefaultDensity));
            Sleep(0.8f);
            Check(() => { var s = sheet(); return s != null && s.Showing && !s.SeeThrough; }, "네온만 — 남색 창 · 뒤 비침 없음");
            Shot("ws_list_neon");
            Do(() => mode(true, false, R0926MenuStyle.DefaultDensity));
            Sleep(0.8f);
            Check(() => { var s = sheet(); var r = dockRim(); return s != null && s.Showing && s.SeeThrough && r != null && !r.Showing; }, "물방울만 — 창은 비치고 하단 탭 네온 테는 꺼짐");
            Shot("ws_list_drop");
            Do(() => mode(true, true, 90));
            Sleep(0.8f);
            Shot("ws_list_both90");
            Do(() => mode(false, false, R0926MenuStyle.DefaultDensity));
            Sleep(0.8f);
            Check(() =>
            {
                var s = sheet();
                var im = s != null ? s.GetComponent<Image>() : null;
                return s != null && !s.Showing && im != null && im.material == im.defaultMaterial && R0926WindowSkin.Windows.Count == 0;
            }, "둘 다 끄면 — 예전 불투명 회색 창 그대로 (재질 원래대로)");
            Shot("ws_list_classic");

            Do(() => mode(true, true, R0926MenuStyle.DefaultDensity));
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

        // 물방울 모드에서 상자가 실제로 그려지는 알파 — 맞춤(R0926WindowFill.Box)을 정점 하나에 돌려 본다 (쪽 = 안 그림 · 글자 · 아이콘 = 그대로)
        private static float WsDrawnAlpha(Graphic g)
        {
            if (g == null) return 0f;
            var f = g.GetComponent<R0926WindowFill>();
            if (f == null || f.FillMode == R0926WindowFill.Mode.Ink) return g.color.a;
            if (f.FillMode == R0926WindowFill.Mode.Page) return 0f;
            var vh = new VertexHelper();
            var v = UIVertex.simpleVert;
            v.color = g.color;
            vh.AddVert(v);
            typeof(R0926WindowFill).GetMethod("Box", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(f, new object[] { vh, R0926MenuStyle.Droplet });
            vh.PopulateUIVertex(ref v, 0);
            vh.Dispose();
            return v.color.a / 255f;
        }

        // 원래 진한(알파 .5 이상) 상자 중 맞춤으로 옅어진 것 — 보이는 것만 센다
        private static int WsFaded(Transform root, List<string> names, out int boxes)
        {
            boxes = 0;
            if (root == null) return -1;
            int faded = 0;
            foreach (var f in root.GetComponentsInChildren<R0926WindowFill>(false))
            {
                var g = f.GetComponent<Graphic>();
                if (g == null || !g.isActiveAndEnabled || f.FillMode != R0926WindowFill.Mode.Box || g.color.a < 0.5f) continue;
                boxes++;
                float a = WsDrawnAlpha(g);
                if (a >= 0.9f * g.color.a) continue;
                faded++;
                if (names != null && names.Count < 6) names.Add(g.name + " " + a.ToString("0.00"));
            }
            return faded;
        }
    }
}
