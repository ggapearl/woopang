using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 목록 필터 (2026-10-17·10-18 대표님).
    ///  · "'전체' 버튼을 눌렀을 때는 다음 카테고리 버튼으로 바뀌는 게 아니라 각 메뉴가 적절하게 스크롤로 발생해서 선택하도록"
    ///    → 분류 칩을 누르면 칩 바로 아래로 분류 8개가 펼쳐진다 (R0926CategoryMenu) · 고르거나 바깥을 누르면 닫힘
    ///  · "'터미널, 기차' 는 … 합쳐서 '터미널 / 기차' … (위아래 레이아웃 / 위에 4칸, 아래 3칸)"
    ///    → 위 [전체 ∨][애견동반][공공데이터][3D] · 아래 [지하철][터미널 / 기차 (두 칸)][사람] (R0926ChipGrid)
    ///    기차역 칩은 지우고 터미널 칩 하나가 둘을 함께 켜고 끈다 (FilterManager)
    ///    ⚠ 기차역 칩은 FilterManager 의 '주류' 토글로도 잘못 이어져 있었다 — 기차역을 끄면 술 파는 가게까지 숨었다. 같이 끊는다
    ///  · "'AI에게 묻기' 버튼은 우선 주석처리" → 버튼을 숨기고 만드는 코드는 Apply5 에 주석으로 남김
    ///  · 10-23 대표님: "3D를 '사람' 좌측에 … '터미널/기차' 는 '지하철' 우측에 … '공공데이터' 우측에 (현재 3D 자리) '축제'"
    ///    → 위 [전체 ∨][애견동반][공공데이터][축제] · 아래 [지하철][터미널 / 기차][3D][사람] — 4칸씩, 두 칸 쓰는 칩 없음
    ///    축제 칩은 공공데이터 칩을 본떠 만들고 FilterManager.festivalToggle 에 잇는다
    ///  · 분류 목록은 전체 · 음식점 · 카페 · 샵 · 공원 · 스포츠 · 화장실 · 랜드마크 순, 줄 앞은 분류 그림 (r0926_c_*)
    /// </summary>
    public static partial class Redesign0926Apply
    {
        // 칸 순서 — 위 4칸 · 아래 4칸
        private static readonly string[] ChipOrder = { "CategoryToggle", "PetFriendlyToggle", "PublicDataToggle", "FestivalToggle", "MetroToggle", "TerminalToggle", "3DObjectToggle", "P2PUserToggle" };
        // 분류 그림 r0926_c_<이름> — FilterManager.CategoryFilterState 순서
        private static readonly string[] CatIconKeys = { "all", "shop", "food", "cafe", "park", "toilet", "sport", "landmark" };
        private const float MenuRowH = 112f, MenuPad = 12f, MenuW = 560f;
        private static readonly Color MenuCard = new Color(0.125f, 0.141f, 0.161f, 0.985f);

        // ApplyListSheet 에서 — 칩 칸 틀 (예전 GridLayoutGroup 4×2)
        private static void ChipGrid0926(GameObject filters)
        {
            var fit = filters.GetComponent<R0926GridFit>();
            if (fit != null) Object.DestroyImmediate(fit);   // GridLayoutGroup 을 요구해 먼저 뗀다
            var grid = filters.GetComponent<GridLayoutGroup>();
            if (grid != null) Object.DestroyImmediate(grid);

            var train = filters.transform.Find("TrainStationToggle");
            if (train != null)
            {
                var gone = train.GetComponent<Toggle>();
                var fm = FindInScene<FilterManager>(filters);
                if (fm != null)
                {
                    var so = new SerializedObject(fm);
                    foreach (var p in new[] { "trainToggle", "alcoholToggle" })
                    {
                        var prop = so.FindProperty(p);
                        if (prop != null && prop.objectReferenceValue == gone) prop.objectReferenceValue = null;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Object.DestroyImmediate(train.gameObject);
            }
            FestivalChip(filters);
            for (int i = 0; i < ChipOrder.Length; i++)
            {
                var c = filters.transform.Find(ChipOrder[i]);
                if (c != null) c.SetSiblingIndex(i);
            }

            var cg = Ensure<R0926ChipGrid>(filters);
            var cso = new SerializedObject(cg);
            cso.FindProperty("columns").intValue = 4;
            cso.FindProperty("gap").floatValue = ChipGap;
            cso.FindProperty("rowHeight").floatValue = ChipH;
            cso.FindProperty("wide").arraySize = 0;   // 예전엔 터미널 / 기차가 두 칸
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        // 축제 칩 — 공공데이터 칩을 본떠 (모양·누름 소리 그대로) 만들고 FilterManager 에 잇는다. 이미 있으면 잇기만
        private static void FestivalChip(GameObject filters)
        {
            var fest = filters.transform.Find("FestivalToggle");
            if (fest == null)
            {
                var src = filters.transform.Find("PublicDataToggle");
                if (src == null) return;
                fest = Object.Instantiate(src.gameObject, filters.transform, false).transform;
                fest.name = "FestivalToggle";
                var t = fest.GetComponent<Toggle>();
                for (int k = t.onValueChanged.GetPersistentEventCount() - 1; k >= 0; k--)
                    UnityEventTools.RemovePersistentListener(t.onValueChanged, k);
                t.group = null;
                t.SetIsOnWithoutNotify(false);
            }
            var fm = FindInScene<FilterManager>(filters);
            if (fm == null) return;
            var so = new SerializedObject(fm);
            so.FindProperty("festivalToggle").objectReferenceValue = fest.GetComponent<Toggle>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 0930 틀(ListPage0926)이 씌워진 뒤에 — 고르기 목록은 목록 쪽과 함께 옆으로 밀린다
        private static void ApplyListFilters(Transform root, List<string> log)
        {
            var sheet = Find(root, "Sheet0926");
            var page = sheet != null ? sheet.transform.Find(ListPage) : null;
            var filters = sheet != null ? Find(sheet.transform, "FilterButtonPanel") : null;
            var chip = filters != null ? filters.transform.Find("CategoryToggle") : null;   // 장소 추가 카드에도 같은 이름이 있다
            if (page == null || chip == null) { log.Add("목록 필터: 목록 쪽·분류 칩 없음"); return; }
            var modes = sheet.GetComponentInChildren<R0926SheetModes>(true);

            // ── 분류 고르기 목록 ── 지난번 틀을 벗기면 시트로 나와 있다
            var found = Find(sheet, "CategoryMenu0926");
            var layer = found != null ? found : NewUI("CategoryMenu0926", page);
            layer.transform.SetParent(page, false);
            layer.transform.SetAsLastSibling();
            Stretch(RT(layer));
            var lcg = Ensure<CanvasGroup>(layer);
            lcg.alpha = 0f; lcg.blocksRaycasts = false; lcg.interactable = false;
            EditorUtility.SetDirty(lcg);

            // 바깥 — 누르면 닫힘 (목록이 살짝 어두워져 펼친 목록에 눈이 간다)
            var shade = FindOrCreate(layer.transform, "Shade0926");
            shade.transform.SetSiblingIndex(0);
            Stretch(RT(shade));
            Img(shade, null, new Color(0, 0, 0, 0.28f), Image.Type.Simple).raycastTarget = true;
            var sb = Ensure<Button>(shade);
            sb.transition = Selectable.Transition.None;

            var card = FindOrCreate(layer.transform, "Card0926");
            card.transform.SetSiblingIndex(1);
            float full = MenuRowH * R0926FilterChip.CatNames.Length + MenuPad * 2;
            SetRect(RT(card), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(SheetPad, ChipsY - ChipH - 12f), new Vector2(MenuW, full));
            Img(card, Spr("r0926_pill"), MenuCard, Image.Type.Sliced, 64f / 40f).raycastTarget = true;   // 카드 여백을 눌러도 닫히지 않게
            var sr = Ensure<ScrollRect>(card);
            sr.horizontal = false; sr.vertical = false;   // 넘칠 때만 R0926CategoryMenu 가 켠다
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            sr.inertia = true;

            var vp = FindOrCreate(card.transform, "Viewport0926");
            vp.transform.SetSiblingIndex(0);
            Stretch(RT(vp));
            Ensure<RectMask2D>(vp);
            var content = FindOrCreate(vp.transform, "Content0926");
            SetRect(RT(content), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, full));
            sr.viewport = RT(vp);
            sr.content = RT(content);
            EditorUtility.SetDirty(sr);

            // 카드 테두리 — 어두운 시트 위에서 카드 가장자리가 보이게
            var ring = FindOrCreate(card.transform, "Ring0926");
            ring.transform.SetAsLastSibling();
            Stretch(RT(ring));
            Img(ring, Spr("r0926_pill_ring"), new Color(1, 1, 1, 0.09f), Image.Type.Sliced, 64f / 40f).raycastTarget = false;

            var menu = Ensure<R0926CategoryMenu>(chip.gameObject);
            int n = R0926FilterChip.CatNames.Length;
            var rows = new Object[n];
            var labels = new Object[n];
            var checks = new Object[n];
            for (int i = 0; i < n; i++)
            {
                int ci = i < R0926FilterChip.CatOrder.Length ? R0926FilterChip.CatOrder[i] : i;   // 이 줄의 분류 (CategoryFilterState)
                var row = FindOrCreate(content.transform, "Row" + i);
                row.transform.SetSiblingIndex(i);
                var rrt = RT(row);
                rrt.anchorMin = new Vector2(0, 1); rrt.anchorMax = new Vector2(1, 1); rrt.pivot = new Vector2(0.5f, 1);
                rrt.offsetMin = new Vector2(MenuPad, -(MenuPad + (i + 1) * MenuRowH));
                rrt.offsetMax = new Vector2(-MenuPad, -(MenuPad + i * MenuRowH));
                EditorUtility.SetDirty(rrt);
                var bg = Img(row, Spr("r0926_pill"), Color.white, Image.Type.Sliced, 64f / 28f);
                bg.raycastTarget = true;
                var b = Ensure<Button>(row);
                b.transition = Selectable.Transition.ColorTint;
                b.targetGraphic = bg;
                var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                ResetListeners(b);
                UnityEventTools.AddIntPersistentListener(b.onClick, menu.Pick, ci);

                // 줄 앞 — 예전 색 점 대신 분류 그림
                var dot = row.transform.Find("Dot");
                if (dot != null) Object.DestroyImmediate(dot.gameObject);
                var icon = FindOrCreate(row.transform, "Icon");
                SetRect(RT(icon), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54, 0), new Vector2(56, 56));
                var iImg = Img(icon, Spr("r0926_c_" + CatIconKeys[ci]), Color.white, Image.Type.Simple);
                iImg.raycastTarget = false;
                iImg.preserveAspect = true;

                var label = FindOrCreate(row.transform, "Label");
                var lrt = RT(label);
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.offsetMin = new Vector2(104, 0); lrt.offsetMax = new Vector2(-90, 0);
                var t = Txt(label, R0926FilterChip.CatNames[ci][0], 44, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;

                var check = FindOrCreate(row.transform, "Check");
                SetRect(RT(check), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-50, 0), new Vector2(48, 48));
                var cImg = Img(check, Spr("r0926_i_check"), Ink, Image.Type.Simple);
                cImg.raycastTarget = false;
                cImg.preserveAspect = true;
                cImg.enabled = ci == 0;

                rows[i] = b; labels[i] = t; checks[i] = cImg;
            }
            for (int i = content.transform.childCount - 1; i >= n; i--)   // 분류가 줄면 남는 줄
                Object.DestroyImmediate(content.transform.GetChild(i).gameObject);

            ResetListeners(sb);
            UnityEventTools.AddPersistentListener(sb.onClick, menu.Close);

            var mso = new SerializedObject(menu);
            mso.FindProperty("layer").objectReferenceValue = lcg;
            mso.FindProperty("card").objectReferenceValue = RT(card);
            mso.FindProperty("scroll").objectReferenceValue = sr;
            SetArray(mso.FindProperty("rows"), rows);
            mso.FindProperty("dots").arraySize = 0;   // 줄 앞은 분류 그림 — 색 점 없음
            SetArray(mso.FindProperty("labels"), labels);
            SetArray(mso.FindProperty("checks"), checks);
            mso.FindProperty("modes").objectReferenceValue = modes;
            mso.FindProperty("rowHeight").floatValue = MenuRowH;
            mso.FindProperty("pad").floatValue = MenuPad;
            mso.FindProperty("minWidth").floatValue = MenuW;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // ── 'AI에게 묻기' 숨김 — 집계 줄은 다시 시트 폭 그대로 ──
            var ask = Find(sheet, "AskAI0926");
            if (ask != null && ask.activeSelf) ask.SetActive(false);
            var summary = Find(sheet, "Summary0926");
            if (summary != null) RT(summary).sizeDelta = new Vector2(-SheetPad * 2, 48);

            log.Add("list filters ok (칩 " + filters.transform.childCount + "개 · 분류 " + n + "줄" + (ask != null ? " · AI에게 묻기 숨김" : "") + ")");
        }

        private static GameObject Find(GameObject under, string name) => Find(under.transform, name);
    }
}
