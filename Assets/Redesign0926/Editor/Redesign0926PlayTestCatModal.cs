using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 대표님 37번 분류 고르기 창 — 분류 칩 · 고른 칸의 숨 쉬는 테 · 「전체」를 누르면 화면 가운데 큰 칸 창 ·
        // 위아래로 밀기 · 바깥 누르기 · 뒤로가기로 닫힘 · '카페'를 고르면 칩이 '카페' · 네온에서는 테 대신 네온 줄이 숨 쉰다.
        // 손가락은 가상 터치 화면으로 — 이벤트 시스템 · 칸 버튼 · 밀기(R0926CategorySwipe)가 진짜 손가락처럼 받는다
        private const string CmKey = "r0926_menu_style", CmNeonKey = "r0926_neon", CmCatKey = "Filter_Category_V1";
        private static bool cmRun, cmHadD, cmHadN, cmHadC;
        private static int cmD, cmN, cmC;
        private static FilterManager.CategoryFilterState cmCat;
        private static float cmMaxOff;

        private static FilterManager CmFm() => UnityEngine.Object.FindAnyObjectByType<FilterManager>();
        private static R0926CategoryMenu CmMenu() => UnityEngine.Object.FindAnyObjectByType<R0926CategoryMenu>();
        private static R0926ChipPulse CmChip() => Chip("CategoryToggle")?.GetComponent<R0926ChipPulse>();
        private static RectTransform CmTile(int i)
        {
            var m = CmMenu();
            return m != null && m.Card != null ? m.Card.Find("Viewport0926/Content0926/Row" + i) as RectTransform : null;
        }
        private static R0926ChipPulse CmTileP(int i) => CmTile(i)?.GetComponent<R0926ChipPulse>();
        private static R0926WindowSkin CmNeonOf(R0926ChipPulse p) => p != null && p.Neon != null ? p.Neon.GetComponent<R0926WindowSkin>() : null;
        private static R0926WindowSkin CmCardSkin() { var m = CmMenu(); return m != null && m.Card != null ? m.Card.GetComponent<R0926WindowSkin>() : null; }
        private static bool CmSame(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
        private static bool CmIs(FilterManager.CategoryFilterState s) { var fm = CmFm(); return fm != null && fm.CategoryState == s; }

        // 오버레이 캔버스 — 월드 좌표가 곧 화면 좌표
        private static Vector2? CmMid(RectTransform rt)
        {
            if (rt == null) return null;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return new Vector2((c[0].x + c[2].x) * 0.5f, (c[0].y + c[2].y) * 0.5f);
        }
        private static Rect CmScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }
        // 카드 위 어두운 곳 — 카드 위쪽 가장자리와 화면 위 사이 가운데
        private static Vector2? CmOutside()
        {
            var m = CmMenu();
            if (m == null || m.Card == null) return null;
            var r = CmScreenRect(m.Card);
            return new Vector2(r.center.x, (r.yMax + Screen.height) * 0.5f);
        }
        private static float CmCardH() { var m = CmMenu(); return m != null && m.Card != null ? CmScreenRect(m.Card).height : 0f; }
        private static Vector2? CmChipMid() => CmMid(Chip("CategoryToggle"));
        private static void CmTap(Func<Vector2?> at) => Finger(at, new Vector2[0], 1, null, null);
        private static void CmWatch() { var m = CmMenu(); if (m != null) cmMaxOff = Mathf.Max(cmMaxOff, Mathf.Abs(m.Offset)); }

        // 네 번 (0.45초 간격) 읽어 숨 쉬는지 — 범위 안에서 0.1 넘게 바뀐다
        private static void CmBreath(Func<float> read, float lo, float hi, string what)
        {
            var got = new List<float>();
            Do(() => got.Clear());
            for (int i = 0; i < 4; i++)
            {
                Do(() => got.Add(read()));
                if (i < 3) Sleep(0.45f);
            }
            Check(() =>
            {
                float mn = 9f, mx = -9f;
                foreach (var v in got) { mn = Mathf.Min(mn, v); mx = Mathf.Max(mx, v); }
                log.Add("  숨 — " + string.Join(" · ", got.ConvertAll(v => v.ToString("0.00"))));
                return got.Count == 4 && mn >= lo - 0.01f && mx <= hi + 0.01f && mx - mn > 0.1f;
            }, what);
        }

        // 그 자리를 누르면 무엇이 받나 (위에서부터 넷) — 이름 경로 · 캔버스
        private static string CmHitsAt(Vector2? p)
        {
            var es = EventSystem.current;
            if (es == null || p == null) return "없음";
            var hits = new List<RaycastResult>();
            es.RaycastAll(new PointerEventData(es) { position = p.Value }, hits);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hits.Count && i < 4; i++)
            {
                var t = hits[i].gameObject.transform;
                string path = t.name;
                for (var a = t.parent; a != null && a.parent != null; a = a.parent) path = a.name + "/" + path;
                var cv = t.GetComponentInParent<Canvas>();
                sb.Append(i).Append(") ").Append(path).Append(" [").Append(cv != null ? cv.rootCanvas.name + " " + cv.rootCanvas.sortingOrder : "-").Append("] ");
            }
            return hits.Count == 0 ? "아무것도 없음" : sb.ToString();
        }
        // 창 층 뒤로 이어지는 캔버스 바로 밑 칸들 — 창보다 나중에 그려지면 창 위에 뜬다
        private static string CmAfterLayer()
        {
            var m = CmMenu();
            var layer = m != null && m.Layer != null ? m.Layer.transform : null;
            var root = layer != null ? layer.parent : null;
            if (root == null) return "-";
            var sb = new System.Text.StringBuilder();
            for (int i = layer.GetSiblingIndex() + 1; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                sb.Append(i).Append(' ').Append(c.name).Append(c.gameObject.activeInHierarchy ? "" : "(꺼짐)").Append(" · ");
            }
            return sb.Length == 0 ? "없음 (창이 맨 위)" : sb.ToString();
        }

        // 하단 탭 자리를 누르면 무엇이 받나 — 창이 열려 있으면 창 바깥(Shade0926)
        private static bool CmDockCovered()
        {
            var es = EventSystem.current;
            var p = CmMid(Find("Dock0926/List_Button") as RectTransform);
            if (es == null || p == null) { log.Add("  하단 탭 자리 없음"); return false; }
            var hits = new List<RaycastResult>();
            es.RaycastAll(new PointerEventData(es) { position = p.Value }, hits);
            string top = hits.Count > 0 ? hits[0].gameObject.name : "없음";
            log.Add("  하단 탭 자리 맨 위: " + top);
            return hits.Count > 0 && hits[0].gameObject.name == "Shade0926";
        }

        private static bool CmTilesBig()
        {
            float minW = 9999f, minH = 9999f, minIcon = 9999f;
            int minFont = 999, n = 0;
            for (int i = 0; i < 9; i++)
            {
                var t = CmTile(i);
                var ic = t != null ? t.Find("Icon") as RectTransform : null;
                var lb = t != null ? t.Find("Label")?.GetComponent<Text>() : null;
                if (ic == null || lb == null) { log.Add("  칸 · 그림 · 이름 없음 " + i); return false; }
                minW = Mathf.Min(minW, t.rect.width); minH = Mathf.Min(minH, t.rect.height);
                minIcon = Mathf.Min(minIcon, ic.rect.width); minFont = Mathf.Min(minFont, lb.fontSize);
                n++;
            }
            var m = CmMenu();
            float share = m != null && m.Card != null ? CmScreenRect(m.Card).width / Mathf.Max(1f, Screen.width) : 0f;
            log.Add($"  칸 {n}개 — 가장 작은 칸 {minW:0}×{minH:0} · 그림 {minIcon:0} · 이름 글자 {minFont} · 카드 폭 = 화면의 {share * 100f:0}%");
            return n == 9 && minW >= 300f && minH >= 240f && minIcon >= 100f && minFont >= 48 && share >= 0.8f;
        }

        // 3줄 3칸 — 같은 칸은 같은 x, 같은 줄은 같은 y · 왼→오른 · 위→아래
        private static bool CmGrid3x3()
        {
            var c = new Vector2[9];
            for (int i = 0; i < 9; i++)
            {
                var p = CmMid(CmTile(i));
                if (p == null) return false;
                c[i] = p.Value;
            }
            for (int i = 0; i < 9; i++)
            {
                int r = i / 3, k = i % 3;
                if (Mathf.Abs(c[i].x - c[k].x) > 2f || Mathf.Abs(c[i].y - c[r * 3].y) > 2f) { log.Add("  칸 자리 어긋남 " + i); return false; }
                if (k > 0 && c[i].x <= c[i - 1].x + 10f) return false;
                if (r > 0 && c[i].y >= c[i - 3].y - 10f) return false;
            }
            return true;
        }

        // 고른 칸만 테 (네온이 아닐 때) — 그 분류 색을 조금 밝게
        private static bool CmTileRings(int selected)
        {
            for (int i = 0; i < 9; i++)
            {
                var p = CmTileP(i);
                var r = p != null ? p.Ring : null;
                if (r == null) { log.Add("  칸 테 없음 " + i); return false; }
                bool want = i == selected;
                if (r.enabled != want) { log.Add("  칸 테 켜짐 다름 " + i); return false; }
                if (want && !CmSame(r.color, R0926ChipPulse.ToneOf(p.Category))) { log.Add("  칸 테 색 다름 " + i + " " + ColorUtility.ToHtmlStringRGB(r.color)); return false; }
            }
            return true;
        }

        private static bool CmCardOpaque(bool skinned)
        {
            var m = CmMenu();
            var im = m != null && m.Card != null ? m.Card.GetComponent<Image>() : null;
            var sk = CmCardSkin();
            log.Add($"  카드 — 알파 {(im != null ? im.color.a : -1f):0.00} · 창 모양 {sk?.Showing} · 비침 {sk?.SeeThrough} · 가림 {sk?.Covered} · 층 알파 {(m != null && m.Layer != null ? m.Layer.alpha : -1f):0.00}");
            if (im == null || im.color.a < 0.99f) return false;
            return skinned ? sk != null && sk.Showing && sk.Covered && !sk.SeeThrough : sk == null || !sk.Showing;
        }

        private static void CmFinish()
        {
            if (!cmRun) return;
            cmRun = false;
            CmMenu()?.Close();
            var fm = CmFm();
            if (fm != null) fm.SetCategory(cmCat);
            if (cmHadC) PlayerPrefs.SetInt(CmCatKey, cmC); else PlayerPrefs.DeleteKey(CmCatKey);
            if (cmHadD) PlayerPrefs.SetInt(CmKey, cmD); else PlayerPrefs.DeleteKey(CmKey);
            if (cmHadN) PlayerPrefs.SetInt(CmNeonKey, cmN); else PlayerPrefs.DeleteKey(CmNeonKey);
            PlayerPrefs.Save();
            R0926MenuStyle.Reload();
            if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
            ptTouch = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
        }

        private static void PlanCatModal()
        {
            const FilterManager.CategoryFilterState All = FilterManager.CategoryFilterState.All, Cafe = FilterManager.CategoryFilterState.Cafe;

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                cmHadD = PlayerPrefs.HasKey(CmKey); cmD = PlayerPrefs.GetInt(CmKey, 1);
                cmHadN = PlayerPrefs.HasKey(CmNeonKey); cmN = PlayerPrefs.GetInt(CmNeonKey, 1);
                cmHadC = PlayerPrefs.HasKey(CmCatKey); cmC = PlayerPrefs.GetInt(CmCatKey, 0);
                var fm = CmFm();
                cmCat = fm != null ? fm.CategoryState : All;
                ptFocusWas = InputSystem.settings.editorInputBehaviorInPlayMode;
                cmRun = true;
                R0926MenuStyle.Droplet = true;   // 물방울만 — 칩 · 칸 둘레는 테(R0926PulseRing)
                R0926MenuStyle.Neon = false;
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                Backdrop("bg_city.png");
            });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Sleep(0.6f);
            Do(() =>
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;   // 에디터 창이 뒤에 있어도
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Do(() => CmFm()?.SetCategory(All));
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.4f);

            // 1) '전체' 칩 — 푸른 테가 천천히 숨 쉰다
            Check(() =>
            {
                var p = CmChip();
                var r = p != null ? p.Ring : null;
                log.Add($"  칩 테 — 붙음 {p != null} · 켜짐 {r != null && r.enabled} · 색 {(r != null ? ColorUtility.ToHtmlStringRGB(r.color) : "-")} · 빛이 칩 밖으로 {(r != null ? r.Reach : 0f):0.#}");
                return p != null && p.Category < 0 && r != null && r.enabled && CmSame(r.color, R0926WindowSkin.Blue);
            }, "'전체' 칩 둘레에 푸른 테 — 목록 창 네온과 같은 빛 (11-26 대표님 37번)");
            CmBreath(() => { var p = CmChip(); return p != null && p.Ring != null ? p.Ring.Breath : -1f; }, 0f, 1f, "'전체' 칩 테가 천천히 숨 쉰다 (2.8초에 한 번 진해졌다 옅어짐)");
            Shot("cm_chip_all");

            // 2) 손가락으로 칩을 누르면 화면 가운데 큰 칸 창
            CmTap(CmChipMid);
            Sleep(0.5f);
            Check(() => { var m = CmMenu(); return m != null && m.IsOpen; }, "'전체' 칩을 손가락으로 누르면 분류 고르기 창이 뜬다");
            Check(() => CmIs(All), "띄우기만 해서는 분류가 바뀌지 않음");
            Check(() =>
            {
                var m = CmMenu();
                var layer = m != null ? m.Layer : null;
                var cv = layer != null ? layer.GetComponentInParent<Canvas>() : null;
                var root = cv != null ? cv.rootCanvas.transform : null;
                var list = Find("ListPanel");
                bool top = layer != null && root != null && list != null && layer.transform.parent == root && list.parent == root
                           && layer.transform.GetSiblingIndex() > list.GetSiblingIndex();
                log.Add($"  층 — {(layer != null && layer.transform.parent != null ? layer.transform.parent.name : "-")} 밑 {(layer != null ? layer.transform.GetSiblingIndex() : -1)}번째 · 목록 {(list != null ? list.GetSiblingIndex() : -1)}번째 · 알파 {(layer != null ? layer.alpha : -1f):0.00}");
                return top && layer.alpha > 0.99f && layer.blocksRaycasts;
            }, "창은 화면 맨 위 층 (목록 위) · 다 떠오르면 또렷이");
            Check(() => CmDockCovered(), "열려 있는 동안 하단 탭 자리도 창 바깥(어두운 곳)이 받는다");
            Check(() => CmTilesBig(), "3×3 큰 칸 9개 — 칸 300×240 이상 · 그림 100 이상 · 이름 48 이상 · 카드가 화면 폭의 80% 이상 (「좀 큼직하게」)");
            Check(() => CmGrid3x3(), "칸이 3줄 3칸으로 반듯하게");
            Do(() => log.Add("  칸 순서: " + MenuOrder()));
            Check(() => MenuOrder() == "전체·음식점·카페·샵·숙소·공원·스포츠·화장실·랜드마크" || MenuOrder() == "All·Food·Cafe·Shop·Stays·Park·Sports·Toilet·Landmark",
                  "칸 순서 — 전체·음식점·카페 / 샵·숙소·공원 / 스포츠·화장실·랜드마크");
            Check(() => MenuIconsOk(), "칸마다 분류 그림");
            Check(() => CmCardOpaque(true), "물방울 — 카드는 불투명 남색 창 (뒤 목록이 비치지 않음 · 5번 「배경은 투명하면 안될 것 같아」)");
            Check(() => CmTileRings(0), "고른 칸('전체')에만 숨 쉬는 푸른 테");
            Shot("cm_modal_drop");

            // 3) '카페' 칸을 손가락으로 — 칩이 '카페' · 모카색 테
            CmTap(() => CmMid(CmTile(2)));
            Sleep(0.5f);
            Check(() => { var m = CmMenu(); return CmIs(Cafe) && m != null && !m.Showing; }, "'카페' 칸을 누르면 카페로 · 창 닫힘");
            Check(() => ChipLabel("CategoryToggle") == "카페" || ChipLabel("CategoryToggle") == "Cafe", "칩 이름이 '전체' 대신 '카페' (37번)");
            Check(() =>
            {
                var r = CmChip()?.Ring;
                log.Add($"  카페 칩 테 — 켜짐 {r != null && r.enabled} · 색 {(r != null ? ColorUtility.ToHtmlStringRGB(r.color) : "-")}");
                return r != null && r.enabled && CmSame(r.color, R0926ChipPulse.ToneOf((int)Cafe));
            }, "'카페' 칩 둘레 테는 모카색을 조금 밝게 (어두운 시트 위에서도 보이게)");
            CmBreath(() => { var p = CmChip(); return p != null && p.Ring != null ? p.Ring.Breath : -1f; }, 0f, 1f, "'카페' 칩 테도 천천히 숨 쉰다");
            Shot("cm_chip_cafe");

            // 4) 다시 열면 '카페' 칸이 골라져 있다
            CmTap(CmChipMid);
            Sleep(0.5f);
            Check(() =>
            {
                var m = CmMenu();
                var ck = CmTile(2)?.Find("Check")?.GetComponent<Image>();
                var ck0 = CmTile(0)?.Find("Check")?.GetComponent<Image>();
                return m != null && m.IsOpen && ck != null && ck.enabled && ck0 != null && !ck0.enabled && CmTileRings(2);
            }, "다시 열면 '카페' 칸에 체크 · 숨 쉬는 모카 테 ('전체' 칸은 꺼짐)");
            Shot("cm_modal_cafe");

            // 5) 아래로 밀기 — 칸(숙소) 위에서 시작해도 그 칸은 눌리지 않는다
            var down = new Vector2[1];
            Do(() => { down[0] = new Vector2(0f, -CmCardH() * 0.35f); cmMaxOff = 0f; });
            Finger(() => CmMid(CmTile(4)), down, 10, CmWatch, "cm_swipe_down");
            Sleep(0.15f);
            Check(() => { var m = CmMenu(); log.Add($"  카드가 따라 내려온 만큼 {cmMaxOff:0}"); return m != null && !m.IsOpen && cmMaxOff > 100f; },
                  "아래로 밀면 카드가 손가락을 따라 내려가다 닫힌다");
            Sleep(0.4f);
            Check(() => { var m = CmMenu(); return m != null && !m.Showing && m.Layer.alpha < 0.01f && !m.Layer.blocksRaycasts && CmIs(Cafe); },
                  "닫힌 뒤 창이 다 사라져 뒤 목록을 누를 수 있음 · 칸 위에서 시작한 밀기는 그 칸을 고르지 않음 (카페 그대로)");

            // 6) 위로 밀기
            CmTap(CmChipMid);
            Sleep(0.5f);
            var up = new Vector2[1];
            Do(() => { up[0] = new Vector2(0f, CmCardH() * 0.35f); cmMaxOff = 0f; });
            Finger(() => CmMid(CmTile(4)), up, 10, CmWatch, null);
            Sleep(0.15f);
            Check(() => { var m = CmMenu(); log.Add($"  카드가 따라 올라간 만큼 {cmMaxOff:0}"); return m != null && !m.IsOpen && cmMaxOff > 100f && CmIs(Cafe); },
                  "위로 밀어도 닫힌다");
            Sleep(0.4f);

            // 7) 조금만 밀고 놓으면 제자리로
            CmTap(CmChipMid);
            Sleep(0.5f);
            var small = new Vector2[1];
            Do(() => { small[0] = new Vector2(0f, -CmCardH() * 0.06f); cmMaxOff = 0f; });
            Finger(() => CmMid(CmTile(4)), small, 16, CmWatch, null);
            Sleep(0.6f);
            Check(() =>
            {
                var m = CmMenu();
                log.Add($"  조금 민 만큼 {cmMaxOff:0} · 놓은 뒤 {(m != null ? m.Offset : -1f):0.#}");
                return m != null && m.IsOpen && cmMaxOff > 10f && Mathf.Abs(m.Offset) < 1f && CmIs(Cafe);
            }, "조금만 밀고 놓으면 카드가 제자리로 돌아오고 창은 그대로 (칸도 안 눌림)");

            // 8) 바깥(카드 위 어두운 곳)을 누르면 닫힘
            Do(() =>
            {
                var p = CmOutside();
                log.Add($"  카드 위 바깥 자리 — 화면 위에서 {(p != null ? Screen.height - p.Value.y : -1f):0} · 받는 것: {CmHitsAt(p)}");
                log.Add("  창 층 뒤의 칸: " + CmAfterLayer());
            });
            Shot("cm_outside");
            CmTap(CmOutside);
            Sleep(0.4f);
            Check(() => { var m = CmMenu(); return m != null && !m.Showing && CmIs(Cafe); }, "창 바깥(어두운 곳)을 누르면 닫힘 · 분류 그대로");
            Do(() => { var m = CmMenu(); if (m != null && m.Showing) { log.Add("  (안 닫혀서 바깥 버튼으로 닫음)"); ClickShade(); } });
            Sleep(0.4f);

            // 9) 바깥에서 시작해 조금 끌다 놓아도 닫힘 (끌기가 되면 바깥 버튼은 눌리지 않는다)
            CmTap(CmChipMid);
            Sleep(0.5f);
            Check(() => { var m = CmMenu(); return m != null && m.IsOpen; }, "(다시 열림)");
            var outDrag = new Vector2[1];
            Do(() => outDrag[0] = new Vector2(0f, -CmCardH() * 0.05f));
            Finger(CmOutside, outDrag, 8, null, null);
            Sleep(0.4f);
            Check(() => { var m = CmMenu(); return m != null && !m.Showing; }, "바깥에서 시작해 조금 끌다 놓아도 닫힘");

            // 10) 하단 탭 자리를 눌러도 창만 닫힌다
            CmTap(CmChipMid);
            Sleep(0.5f);
            Check(() => { var m = CmMenu(); return m != null && m.IsOpen; }, "(다시 열림)");
            CmTap(() => CmMid(Find("Dock0926/List_Button") as RectTransform));
            Sleep(0.5f);
            Check(() => { var m = CmMenu(); var md = Modes(); return m != null && !m.Showing && Active("ListPanel") && md != null && md.Current == 0; },
                  "열려 있을 때 하단 탭 자리를 누르면 창만 닫히고 목록은 그대로");

            // 11) 뒤로가기 — 창만 닫힌다
            CmTap(CmChipMid);
            Sleep(0.5f);
            Check(() => { var m = CmMenu(); return m != null && m.IsOpen; }, "(다시 열림)");
            Back();
            Sleep(0.5f);
            Check(() => { var m = CmMenu(); return m != null && !m.Showing && Active("ListPanel"); }, "뒤로가기 — 창만 닫히고 목록은 그대로");

            // 12) 네온 — 칩 · 칸 둘레는 그 분류 색 네온 줄이 숨 쉰다 (「'네온사인' 설정했을 때는 적절히 해당 카테고리 분류버튼이 잘 보이도록」)
            Do(() => { R0926MenuStyle.Droplet = true; R0926MenuStyle.Neon = true; });
            Sleep(0.8f);
            Check(() =>
            {
                var p = CmChip();
                var sk = CmNeonOf(p);
                log.Add($"  네온 칩 — 테 {p?.Ring?.enabled} · 네온 줄 {sk?.Showing} · 빛 {(sk != null ? ColorUtility.ToHtmlStringRGB(sk.Glow) : "-")} · 세기 {(p != null && p.Neon != null ? p.Neon.color.a : -1f):0.00}");
                return p != null && p.Ring != null && !p.Ring.enabled && sk != null && sk.Showing && CmSame(sk.Glow, R0926ChipPulse.ToneOf((int)Cafe));
            }, "네온 — '카페' 칩 둘레는 테 대신 카페 색 네온 줄 (두 겹이 아님)");
            CmBreath(() => { var p = CmChip(); return p != null && p.Neon != null ? p.Neon.color.a : -1f; }, 0.5f, 1f, "네온 줄도 천천히 숨 쉰다 (세기 0.5 ~ 1)");
            Shot("cm_chip_neon");
            CmTap(CmChipMid);
            Sleep(0.6f);
            Check(() =>
            {
                for (int i = 0; i < 9; i++)
                {
                    var p = CmTileP(i);
                    var sk = CmNeonOf(p);
                    if (p == null || sk == null || !sk.Showing) { log.Add("  칸 네온 꺼짐 " + i); return false; }
                    if (p.Ring != null && p.Ring.enabled) { log.Add("  칸 테가 같이 켜짐 " + i); return false; }
                    if (!CmSame(sk.Glow, R0926ChipPulse.ToneOf(p.Category))) { log.Add("  칸 네온 색 다름 " + i + " " + ColorUtility.ToHtmlStringRGB(sk.Glow)); return false; }
                    float a = p.Neon.color.a;
                    bool ok = p.Category == (int)Cafe ? a >= 0.49f && a <= 1.01f : Mathf.Abs(a - 0.42f) < 0.02f;
                    if (!ok) { log.Add($"  칸 네온 세기 다름 {i} {a:0.00}"); return false; }
                }
                return true;
            }, "네온 — 칸마다 그 분류 색 네온 줄 (고른 '카페' 칸은 숨 쉬고 나머지는 옅게) · 테는 꺼짐");
            Check(() => CmCardOpaque(true), "네온 — 카드는 불투명 남색 창");
            Shot("cm_modal_neon");
            Do(() => ClickShade());
            Sleep(0.4f);

            // 13) 둘 다 끄면 — 불투명 짙은 회색 카드 · 테
            Do(() => { R0926MenuStyle.Droplet = false; R0926MenuStyle.Neon = false; });
            Sleep(0.6f);
            Check(() => { var p = CmChip(); var sk = CmNeonOf(p); return p != null && p.Ring != null && p.Ring.enabled && sk != null && !sk.Showing; },
                  "둘 다 끄면 — 칩은 다시 테 (네온 줄은 안 그림)");
            Shot("cm_chip_classic");
            CmTap(CmChipMid);
            Sleep(0.6f);
            Check(() => CmCardOpaque(false), "둘 다 끄면 — 카드는 불투명 짙은 회색 (창 모양 없음)");
            Check(() =>
            {
                for (int i = 0; i < 9; i++) { var sk = CmNeonOf(CmTileP(i)); if (sk == null || sk.Showing) return false; }
                return CmTileRings(2);
            }, "둘 다 끄면 — 칸 네온 줄은 안 그리고 고른 '카페' 칸에 테");
            Shot("cm_modal_classic");
            Do(() => ClickShade());
            Sleep(0.4f);

            Do(CmFinish);
        }
    }
}
