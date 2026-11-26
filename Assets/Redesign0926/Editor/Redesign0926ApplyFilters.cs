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
    ///  · 11-18 대표님 N8a 「지금 '공공데이터'(관광공사) → 이름 '공공기관' · 자리는 지금 '3D' 칩 자리 (3D 칩은 없앰) ·
    ///    새 가게 DB(소상공인 상가정보) → 이름 '공공데이터' · 자리는 지금 '공공데이터' 칩 자리 (3번째)」
    ///    → 위 [전체 ∨][애견동반][공공데이터][축제] · 아래 [지하철][터미널 / 기차][공공기관][사람]
    ///    새 칩(StoresToggle)은 공공데이터 칩을 본떠 만들고 FilterManager.storesToggle 에 · 3D 칩은 지우고 3D 는 늘 보인다 (FilterManager)
    ///  · 분류 목록은 전체 · 음식점 · 카페 · 샵 · 숙소 · 공원 · 스포츠 · 화장실 · 랜드마크 순, 줄 앞은 분류 그림 (r0926_c_*)
    ///    (11-19 L1 '숙소' — 침대 그림 r0926_c_stay)
    ///  · 11-26 대표님 37번 「'전체버튼' 을 눌렀을 때는 적당히 모달 발생해서 좀 큼직하게 버튼 … 아래나 위로 스와이프 하거나 모달 밖을 터치했을 때는
    ///    해당 모달이 닫히면 돼」 → 칩 밑 펼침 목록 대신 화면 가운데 3×3 큰 칸 창 (R0926CategoryMenu · 밀기 R0926CategorySwipe) ·
    ///    분류 칩 · 고른 칸에 천천히 숨 쉬는 테 (R0926ChipPulse — 네온이 아니면 R0926PulseRing · 네온이면 네온 줄)
    /// </summary>
    public static partial class Redesign0926Apply
    {
        // 칸 순서 — 위 4칸 · 아래 4칸
        private static readonly string[] ChipOrder = { "CategoryToggle", "PetFriendlyToggle", "StoresToggle", "FestivalToggle", "MetroToggle", "TerminalToggle", "PublicDataToggle", "P2PUserToggle" };
        // 분류 그림 r0926_c_<이름> — FilterManager.CategoryFilterState 순서
        private static readonly string[] CatIconKeys = { "all", "shop", "food", "cafe", "park", "toilet", "sport", "landmark", "stay" };
        // 분류 고르기 창 (11-26 대표님 37번) — 화면 가운데 카드 · 3×3 큰 칸 · 칩 모서리 반지름 34
        private const float CatCardW = 1300f, CatTop = 88f, CatPad = 48f, CatGap = 28f, CatTileH = 272f, CatTileR = 44f, CatChipR = 34f;
        private const string CategoryCardName = "CategoryCard0926";   // 창 모양이 이름으로 불투명 칸(WindowSolid)을 고른다 — 다른 Card0926 과 겹치지 않게
        private static readonly Color MenuCard = new Color(0.125f, 0.141f, 0.161f, 1f);

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
            Remove3DChip(filters);
            StoresChip(filters);
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

        // 3D 칩 — 11-18 대표님 「3D 칩은 없앰」. 3D 는 늘 보인다 (FilterManager — object3DToggle 이 비면 켠 채로)
        private static void Remove3DChip(GameObject filters)
        {
            var chip = filters.transform.Find("3DObjectToggle");
            if (chip == null) return;
            var gone = chip.GetComponent<Toggle>();
            var fm = FindInScene<FilterManager>(filters);
            if (fm != null)
            {
                var so = new SerializedObject(fm);
                var prop = so.FindProperty("object3DToggle");
                if (prop != null && prop.objectReferenceValue == gone) prop.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Object.DestroyImmediate(chip.gameObject);
        }

        // 「공공데이터」 칩 — 근처 가게 (소상공인 상가정보 · StoreManager). 공공데이터(이제 「공공기관」) 칩을 본떠 만들고 FilterManager.storesToggle 에 잇는다
        private static void StoresChip(GameObject filters)
        {
            var stores = filters.transform.Find("StoresToggle");
            if (stores == null)
            {
                var src = filters.transform.Find("PublicDataToggle");
                if (src == null) return;
                stores = Object.Instantiate(src.gameObject, filters.transform, false).transform;
                stores.name = "StoresToggle";
                var t = stores.GetComponent<Toggle>();
                for (int k = t.onValueChanged.GetPersistentEventCount() - 1; k >= 0; k--)
                    UnityEventTools.RemovePersistentListener(t.onValueChanged, k);
                t.group = null;
                t.SetIsOnWithoutNotify(true);   // 처음엔 켠 채 (FilterManager 저장값 기본 1)
            }
            var fm = FindInScene<FilterManager>(filters);
            if (fm == null) return;
            var so = new SerializedObject(fm);
            so.FindProperty("storesToggle").objectReferenceValue = stores.GetComponent<Toggle>();
            so.ApplyModifiedPropertiesWithoutUndo();
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

        // 0930 틀(ListPage0926)이 씌워진 뒤에
        private static void ApplyListFilters(Transform root, List<string> log)
        {
            var sheet = Find(root, "Sheet0926");
            var page = sheet != null ? sheet.transform.Find(ListPage) : null;
            var filters = sheet != null ? Find(sheet.transform, "FilterButtonPanel") : null;
            var chip = filters != null ? filters.transform.Find("CategoryToggle") : null;   // 장소 추가 카드에도 같은 이름이 있다
            var list = root.Find("ListPanel");
            if (page == null || chip == null || list == null) { log.Add("목록 필터: 목록 쪽·분류 칩 없음"); return; }
            var modes = sheet.GetComponentInChildren<R0926SheetModes>(true);
            var neonMat = WindowSkinMaterial(log);

            // ── 분류 고르기 창 ── 11-26 대표님 37번 「'전체버튼' 을 눌렀을 때는 적당히 모달 발생해서 좀 큼직하게 버튼 만들어주면 돼」
            // 화면 맨 위 층 (목록 바로 위 — 목록 · 하단 탭까지 어둡게 덮는다). 예전엔 목록 쪽 안에서 칩 밑으로 펼쳤다 (지난번 것은 거기 있다)
            var found = Find(root, "CategoryMenu0926");
            var layer = found != null ? found : NewUI("CategoryMenu0926", root);
            if (layer.transform.parent != root) layer.transform.SetParent(root, false);
            int li = list.GetSiblingIndex();
            if (layer.transform.GetSiblingIndex() != li + 1)
                layer.transform.SetSiblingIndex(layer.transform.GetSiblingIndex() > li ? li + 1 : li);
            Stretch(RT(layer));
            RT(layer).localScale = Vector3.one;
            var lcg = Ensure<CanvasGroup>(layer);
            lcg.alpha = 0f; lcg.blocksRaycasts = false; lcg.interactable = false;
            EditorUtility.SetDirty(lcg);

            // 바깥 — 누르면 닫힘 (뒤가 어두워져 창에 눈이 간다)
            var shade = FindOrCreate(layer.transform, "Shade0926");
            shade.transform.SetSiblingIndex(0);
            Stretch(RT(shade));
            Img(shade, null, new Color(0, 0, 0, 0.5f), Image.Type.Simple).raycastTarget = true;
            var sb = Ensure<Button>(shade);
            sb.transition = Selectable.Transition.None;
            var snav = sb.navigation; snav.mode = Navigation.Mode.None; sb.navigation = snav;
            EditorUtility.SetDirty(sb);

            // 카드 — 불투명 (11-26 대표님 5번 「이 때 발생하는 배경은 투명하면 안될 것 같아」). 물방울 · 네온 모드에선 다른 창처럼 남색 + 네온 테
            // (창 모양이 이름으로 고른다 — WindowSolid · 목록 위라 뒤가 비치지 않는다)
            var cardT = layer.transform.Find(CategoryCardName);
            if (cardT == null)
            {
                cardT = layer.transform.Find("Card0926");
                if (cardT != null) cardT.name = CategoryCardName;
            }
            var card = cardT != null ? cardT.gameObject : NewUI(CategoryCardName, layer.transform);
            card.transform.SetSiblingIndex(1);
            float cardH = CatTop + CatTileH * 3f + CatGap * 2f + CatPad;
            SetRect(RT(card), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(CatCardW, cardH));
            RT(card).localScale = Vector3.one;
            Img(card, Spr("r0926_pill"), MenuCard, Image.Type.Sliced, 1f).raycastTarget = true;   // 카드 여백을 눌러도 닫히지 않게
            Ensure<R0926KeepSolid>(card);   // 칸 · 글자는 물방울 모드에서도 제 색 그대로
            var oldScroll = card.GetComponent<ScrollRect>();
            if (oldScroll != null) Object.DestroyImmediate(oldScroll);   // 칸이 다 보인다 — 끌기는 층(R0926CategorySwipe)이 받아 위아래로 밀면 닫는다

            var vp = FindOrCreate(card.transform, "Viewport0926");
            vp.transform.SetSiblingIndex(0);
            var vrt = RT(vp);
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.pivot = new Vector2(0.5f, 0.5f);
            vrt.offsetMin = new Vector2(CatPad, CatPad); vrt.offsetMax = new Vector2(-CatPad, -CatTop);
            EditorUtility.SetDirty(vrt);
            var oldMask = vp.GetComponent<RectMask2D>();
            if (oldMask != null) Object.DestroyImmediate(oldMask);   // 칸 둘레 숨 쉬는 테가 잘리지 않게
            var content = FindOrCreate(vp.transform, "Content0926");
            Stretch(RT(content));

            // 손잡이 — 위아래로 밀어 닫을 수 있다는 표시
            var handle = FindOrCreate(card.transform, "Handle0926");
            SetRect(RT(handle), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -CatTop / 2f), new Vector2(120, 12));
            Img(handle, Spr("r0926_pill"), new Color(1, 1, 1, 0.22f), Image.Type.Sliced, 64f / 6f).raycastTarget = false;

            // 카드 테두리 — 어두운 바탕 위에서 카드 가장자리가 보이게
            var ring = FindOrCreate(card.transform, "Ring0926");
            ring.transform.SetAsLastSibling();
            Stretch(RT(ring));
            Img(ring, Spr("r0926_pill_ring"), new Color(1, 1, 1, 0.09f), Image.Type.Sliced, 1f).raycastTarget = false;

            // 칸 — 3×3 (그림 위 · 이름 아래 · 고른 칸은 오른쪽 위 체크)
            var menu = Ensure<R0926CategoryMenu>(chip.gameObject);
            int n = R0926FilterChip.CatNames.Length;
            float tileW = (CatCardW - CatPad * 2f - CatGap * 2f) / 3f;
            var rows = new Object[n];
            var labels = new Object[n];
            var checks = new Object[n];
            for (int i = 0; i < n; i++)
            {
                int ci = i < R0926FilterChip.CatOrder.Length ? R0926FilterChip.CatOrder[i] : i;   // 이 칸의 분류 (CategoryFilterState)
                int col = i % 3, line = i / 3;
                var row = FindOrCreate(content.transform, "Row" + i);
                row.transform.SetSiblingIndex(i);
                SetRect(RT(row), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f),
                        new Vector2(col * (tileW + CatGap) + tileW / 2f, -(line * (CatTileH + CatGap) + CatTileH / 2f)), new Vector2(tileW, CatTileH));
                var bg = Img(row, Spr("r0926_pill"), Color.white, Image.Type.Sliced, 64f / CatTileR);
                bg.raycastTarget = true;
                var b = Ensure<Button>(row);
                b.transition = Selectable.Transition.ColorTint;
                b.targetGraphic = bg;
                var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                ResetListeners(b);
                UnityEventTools.AddIntPersistentListener(b.onClick, menu.Pick, ci);

                // 고른 칸의 숨 쉬는 테 · 네온 줄 (맨 뒤)
                var pulse = CatPulse(row.transform, CatTileR, 4f, 5f, 16f, 4f);
                var neon = CatNeon(row.transform, CatTileR, neonMat);
                var cp = Ensure<R0926ChipPulse>(row);
                var cso = new SerializedObject(cp);
                cso.FindProperty("ring").objectReferenceValue = pulse;
                cso.FindProperty("neon").objectReferenceValue = neon;
                cso.FindProperty("category").intValue = ci;
                cso.FindProperty("gate").objectReferenceValue = lcg;
                cso.ApplyModifiedPropertiesWithoutUndo();

                var dot = row.transform.Find("Dot");
                if (dot != null) Object.DestroyImmediate(dot.gameObject);
                var icon = FindOrCreate(row.transform, "Icon");
                SetRect(RT(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 42), new Vector2(120, 120));
                var iImg = Img(icon, Spr("r0926_c_" + CatIconKeys[ci]), Color.white, Image.Type.Simple);
                iImg.raycastTarget = false;
                iImg.preserveAspect = true;

                var label = FindOrCreate(row.transform, "Label");
                SetRect(RT(label), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -73), new Vector2(tileW - 24f, 64));
                var t = Txt(label, R0926FilterChip.CatNames[ci][0], 50, Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;

                var check = FindOrCreate(row.transform, "Check");
                SetRect(RT(check), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-42, -42), new Vector2(44, 44));
                var cImg = Img(check, Spr("r0926_i_check"), Ink, Image.Type.Simple);
                cImg.raycastTarget = false;
                cImg.preserveAspect = true;
                cImg.enabled = ci == 0;

                rows[i] = b; labels[i] = t; checks[i] = cImg;
            }
            for (int i = content.transform.childCount - 1; i >= n; i--)   // 분류가 줄면 남는 칸
                Object.DestroyImmediate(content.transform.GetChild(i).gameObject);

            ResetListeners(sb);
            UnityEventTools.AddPersistentListener(sb.onClick, menu.Close);
            var swipe = Ensure<R0926CategorySwipe>(layer);
            var wso = new SerializedObject(swipe);
            wso.FindProperty("menu").objectReferenceValue = menu;
            wso.ApplyModifiedPropertiesWithoutUndo();

            var mso = new SerializedObject(menu);
            mso.FindProperty("layer").objectReferenceValue = lcg;
            mso.FindProperty("card").objectReferenceValue = RT(card);
            mso.FindProperty("shade").objectReferenceValue = sb;
            SetArray(mso.FindProperty("rows"), rows);
            SetArray(mso.FindProperty("labels"), labels);
            SetArray(mso.FindProperty("checks"), checks);
            mso.FindProperty("modes").objectReferenceValue = modes;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // ── 분류 칩 — 숨 쉬는 테 (11-26 대표님 37번 「'전체' 로 있는 버전 내에는 … 해당버튼 외곽에서 어울릴만한 색으로 적당히 천천히 펄스효과를
            // 외곽선이 있도록 … '카페' 를 선택했다면 … 겉에 적당히 해당 톤앤매너에 맞게 살짝의 펄스효과」). 네온이 아니면 그림 없는 테 · 네온이면 네온 줄
            // (「'네온사인' 설정했을 때는 적절히 해당 카테고리 분류버튼이 잘 보이도록」). 칩 사이가 18 이라 빛은 그 안에서 옅어지게 ──
            var chipRing = CatPulse(chip, CatChipR, 3f, 4f, 10f, 3f);
            var chipNeon = CatNeon(chip, CatChipR, neonMat);
            var chp = Ensure<R0926ChipPulse>(chip.gameObject);
            var pso = new SerializedObject(chp);
            pso.FindProperty("ring").objectReferenceValue = chipRing;
            pso.FindProperty("neon").objectReferenceValue = chipNeon;
            pso.FindProperty("category").intValue = -1;
            pso.FindProperty("gate").objectReferenceValue = null;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // ── 'AI에게 묻기' 숨김 — 집계 줄은 다시 시트 폭 그대로 ──
            var ask = Find(sheet, "AskAI0926");
            if (ask != null && ask.activeSelf) ask.SetActive(false);
            var summary = Find(sheet, "Summary0926");
            if (summary != null) RT(summary).sizeDelta = new Vector2(-SheetPad * 2, 48);

            log.Add("list filters ok (칩 " + filters.transform.childCount + "개 · 분류 " + n + "칸 3×3 · 고르기 창 " + layer.transform.parent.name + " 밑 "
                    + layer.transform.GetSiblingIndex() + "번째" + (ask != null ? " · AI에게 묻기 숨김" : "") + ")");
        }

        // 숨 쉬는 테 (R0926PulseRing) — 칸에 꽉 차게 · 맨 뒤 · 누름은 안 받는다. 켜고 끄기 · 색 · 숨은 R0926ChipPulse
        private static R0926PulseRing CatPulse(Transform host, float radius, float gap, float stroke, float glow, float spread)
        {
            var go = FindOrCreate(host, "Pulse0926");
            go.transform.SetSiblingIndex(0);
            Stretch(RT(go));
            var le = Ensure<LayoutElement>(go);
            le.ignoreLayout = true;
            EditorUtility.SetDirty(le);
            var pr = Ensure<R0926PulseRing>(go);
            pr.raycastTarget = false;
            EditorUtility.SetDirty(pr);
            var so = new SerializedObject(pr);
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("gap").floatValue = gap;
            so.FindProperty("stroke").floatValue = stroke;
            so.FindProperty("glow").floatValue = glow;
            so.FindProperty("spread").floatValue = spread;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pr;
        }

        // 네온 모드 분류 테 — 목록 강조 줄과 같은 네온 줄 (흰 심 · 그 색 빛 · 네온 모드에서만 그린다). 세기 · 색은 R0926ChipPulse
        private static Image CatNeon(Transform host, float radius, Material mat)
        {
            var go = FindOrCreate(host, "Neon0926");
            go.transform.SetSiblingIndex(1);
            Stretch(RT(go));
            var le = Ensure<LayoutElement>(go);
            le.ignoreLayout = true;
            EditorUtility.SetDirty(le);
            var img = Img(go, Spr("r0926_pill"), Color.white, Image.Type.Sliced, 64f / radius);
            img.raycastTarget = false;
            var sk = Ensure<R0926WindowSkin>(go);
            var so = new SerializedObject(sk);
            so.FindProperty("kind").enumValueIndex = (int)R0926WindowSkin.Kind.Row;
            so.FindProperty("skin").objectReferenceValue = mat;
            so.FindProperty("tint").floatValue = 1f;
            so.FindProperty("glow").colorValue = R0926WindowSkin.Blue;
            so.FindProperty("shapeFrom").objectReferenceValue = null;
            so.FindProperty("fillContent").boolValue = false;
            so.FindProperty("covers").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(go);
            return img;
        }

        private static GameObject Find(GameObject under, string name) => Find(under.transform, name);
    }
}
