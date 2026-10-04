using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v5: 장소 추가 전면 개편(웹 시안 수준), 상태 화면(업데이트·AR 미리보기·사진 선택·배너·경고),
    /// 첫 실행 안내(도크 기준), 인터넷 끊김 안내, 상세 화면 로마자 병기.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float UP = 56f;          // 카드 안쪽 여백
        private const float ChipRowH = 112f;   // 분류 칩 한 줄 (2026-10-04 키움 — 아래 빈 공간이 남았다)
        private const float SwitchH = 172f;    // 스위치 한 줄 (인스타 ID 칸이 켜질 128 은 남긴다)
        private const float UploadMargin = 52f; // 카드 좌우 — 옆 카드가 살짝 보이되 겹치지 않는 폭 (SwipePanelController 미리보기 80 기준)

        private static readonly Color InputBg = new Color(0.09f, 0.106f, 0.118f, 1f);

        // ============================================================
        // 장소 추가 / 3D 모델 추가
        // ============================================================
        private static void ApplyUploadFull(Transform root, List<string> log)
        {
            var page = Find(root, "UploadPage");
            if (page == null) { log.Add("UploadPage 없음"); return; }
            var cube = Find(page.transform, "CubeUploadPage");
            var model = Find(page.transform, "ModelUploadPage");
            var swipe = FindInScene<SwipePanelController>(page);

            // 두 카드를 감싸는 틀 — 도크 위 영역 + 아래에서 올라오는 움직임은 틀이 맡는다
            // (SwipePanelController 가 카드 위치를 매 프레임 (x,0) 으로 고정하므로 카드에는 애니메이션을 걸 수 없다)
            var wrap = Find(page.transform, "UploadSheet0926");
            if (wrap == null) { wrap = NewUI("UploadSheet0926", page.transform); }
            wrap.transform.SetSiblingIndex(0);
            var wrt = RT(wrap);
            wrt.anchorMin = new Vector2(0, 0); wrt.anchorMax = new Vector2(1, 0.92f); wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.offsetMin = new Vector2(0, DockBottom + DockHeight + 24); wrt.offsetMax = Vector2.zero;
            Ensure<R0926SafeInset>(wrap).SetEdge(R0926SafeInset.Edge.Bottom, R0926SafeInset.Mode.Stretch);
            Slide(wrap, 240f);

            if (cube != null) UploadCard(cube, wrap.transform, swipe, 0, false, log);
            if (model != null) UploadCard(model, wrap.transform, swipe, 1, true, log);
            log.Add("upload full ok");
        }

        private static void UploadCard(GameObject card, Transform wrap, SwipePanelController swipe, int index, bool isModel, List<string> log)
        {
            card.transform.SetParent(wrap, false);
            var old = card.GetComponent<R0926SlideIn>();
            if (old != null) Object.DestroyImmediate(old);   // v4 에서 붙였던 것 — 스와이프와 충돌
            var cg = card.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;

            Img(card, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius).raycastTarget = true;
            var crt = RT(card);
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(-UploadMargin * 2, 0);

            // 제목
            var title = card.transform.Find("TITLE");
            if (title != null)
            {
                var t = Txt(title.gameObject, null, 56, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                Row(title.gameObject, -44, 84);
            }

            // 장소 / 3D 모델 탭
            var seg = FindOrCreate(card.transform, "Seg0926");
            Img(seg, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / 48f).raycastTarget = true;
            Row(seg, -144, 96);
            var tabs = new Image[2];
            var tabLabels = new Text[2];
            string[][] tabWords = { new[] { "장소", "Place", "場所", "地点", "Lugar" }, new[] { "3D모델", "3D model", "3Dモデル", "3D模型", "Modelo 3D" } };
            for (int i = 0; i < 2; i++)
            {
                var tab = FindOrCreate(seg.transform, "Tab" + i);
                tabs[i] = Img(tab, Spr("r0926_pill"), new Color(1, 1, 1, i == index ? 0.14f : 0f), Image.Type.Sliced, 64f / 42f);
                tabs[i].raycastTarget = true;
                var trt = RT(tab);
                trt.anchorMin = new Vector2(i * 0.5f, 0); trt.anchorMax = new Vector2(i * 0.5f + 0.5f, 1); trt.pivot = new Vector2(0.5f, 0.5f);
                trt.offsetMin = new Vector2(6, 6); trt.offsetMax = new Vector2(-6, -6);
                var lb = FindOrCreate(tab.transform, "Label0926");
                tabLabels[i] = Txt(lb, tabWords[i][0], 36, i == index ? Ink : Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                var lrt = RT(lb); lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                Loc(lb, tabWords[i][0], tabWords[i][1], tabWords[i][2], tabWords[i][3], tabWords[i][4]);
                var b = Ensure<Button>(tab);
                b.targetGraphic = tabs[i];
                ResetListeners(b);
                if (swipe != null) UnityEventTools.AddIntPersistentListener(b.onClick, swipe.SwitchToPanel, i);
            }
            var segState = Ensure<R0926SegmentState>(seg);
            var sso = new SerializedObject(segState);
            sso.FindProperty("swipe").objectReferenceValue = swipe;
            SetArray(sso.FindProperty("tabs"), tabs);
            SetArray(sso.FindProperty("labels"), tabLabels);
            sso.ApplyModifiedPropertiesWithoutUndo();

            var panel = card.transform.Find("Panel");
            if (panel == null) { log.Add(card.name + ": Panel 없음"); return; }
            var prt = RT(panel.gameObject);
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero; prt.sizeDelta = Vector2.zero;
            Transform P(string n) => panel.Find(n);

            // ── 사진 ──
            const float photoY = -362f, tile = 430f, sub = 203f, gap = 24f;
            SectionLabel(isModel ? P("Add_File") : P("Add_Logo"), UP, -276, 400);
            SectionLabel(P("Add_Imgs"), UP + tile + gap, -276, 700);
            var cap = P("glb_gltf_fbx_obj");
            if (cap != null)
            {
                var ct = Txt(cap.gameObject, null, 26, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
                ct.horizontalOverflow = HorizontalWrapMode.Overflow;
                At(cap.gameObject, UP, photoY - tile + 76, tile, 54);
                cap.SetAsLastSibling();
            }

            foreach (var n in isModel ? new[] { "Select_Button", "Selected_File_Button" } : new[] { "MainPhoto_Button" })
            {
                var b = P(n);
                if (b == null) continue;
                Tile(b.gameObject, UP, photoY, tile, tile, n == "Selected_File_Button" ? "r0926_i_check" : isModel ? "r0926_i_file" : "r0926_i_cam", 124);
            }
            var display = P("MainPhotoDisplay");
            if (display != null)
            {
                At(display.gameObject, UP, photoY, tile, tile);
                var di = display.GetComponent<Image>(); di.preserveAspect = false; di.type = Image.Type.Simple;
            }
            var subBtn = P("SubPhoto_Button");
            if (subBtn != null) Tile(subBtn.gameObject, UP + tile + gap, photoY, sub, sub, "r0926_i_plus", 80);
            var reset = P("SubPhoto_Reset");
            if (reset != null) Tile(reset.gameObject, UP + tile + gap, photoY - sub - gap, sub, sub, "r0926_i_reset", 72);
            var subs = P("SubPhotoContainer");
            if (subs != null)
            {
                var srt = RT(subs.gameObject);
                srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0, 1);
                srt.offsetMin = new Vector2(UP + tile + gap + sub + 16, photoY - tile);
                srt.offsetMax = new Vector2(-UP, photoY);
                var grid = subs.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.cellSize = new Vector2(sub, sub);
                    grid.spacing = new Vector2(16, gap);
                    grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                    grid.childAlignment = TextAnchor.UpperLeft;
                    grid.constraint = GridLayoutGroup.Constraint.Flexible;
                    EditorUtility.SetDirty(grid);
                }
            }

            // ── 이름 ──
            const float nameY = photoY - tile - 62;   // -854
            SectionLabel(P("Name"), UP, nameY, 600);
            var name = P("NameInput");
            if (name != null)
            {
                Img(name.gameObject, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / 34f);
                Row(name.gameObject, nameY - 66, 140, UP);
                StyleInputTexts(name.gameObject, 48, 44);
                // 안내 글 — 예전엔 'Enter text' (키보드 위 입력줄에도 이 글이 그대로 뜬다)
                var nf = name.GetComponent<InputField>();
                if (nf != null && nf.placeholder != null)
                    Loc(nf.placeholder.gameObject, isModel ? "모델 이름" : "장소 이름", isModel ? "Model name" : "Place name",
                        isModel ? "モデル名" : "場所の名前", isModel ? "模型名称" : "地点名称", isModel ? "Nombre del modelo" : "Nombre del lugar");
            }

            // ── 위치 ──
            const float locY = nameY - 250;   // -1094
            SectionLabel(P("Coordinate"), UP, locY, 600);
            var loc = P("LocationInput");
            if (loc != null)
            {
                var cardBg = FindOrCreate(panel, "LocCard0926");
                cardBg.transform.SetSiblingIndex(loc.GetSiblingIndex());
                Img(cardBg, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / 34f).raycastTarget = false;
                Row(cardBg, locY - 66, 140, UP);
                var pin = FindOrCreate(cardBg.transform, "Icon0926");
                Img(pin, Spr("r0926_i_pin"), new Color(0.384f, 0.827f, 0.616f, 1f), Image.Type.Simple).raycastTarget = false;
                SetRect(RT(pin), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(66, 0), new Vector2(62, 62));
                var coord = FindOrCreate(cardBg.transform, "Coord0926");
                var cTxt = Txt(coord, "", 44, new Color(1f, 1f, 1f, 0.62f), TextAnchor.MiddleLeft, FontStyle.Normal);
                cTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                var crt0 = RT(coord); crt0.anchorMin = Vector2.zero; crt0.anchorMax = Vector2.one; crt0.offsetMin = new Vector2(120, 0); crt0.offsetMax = new Vector2(-36, 0);
                var cl = Ensure<R0926CoordLine>(cardBg);
                var clso = new SerializedObject(cl);
                clso.FindProperty("text").objectReferenceValue = cTxt;
                clso.FindProperty("source").objectReferenceValue = loc.GetComponent<InputField>();
                clso.ApplyModifiedPropertiesWithoutUndo();
                var lcg = Ensure<CanvasGroup>(loc.gameObject);   // 원래 칸: 값은 계속 채워지되 보이지도 눌리지도 않게
                lcg.alpha = 0f; lcg.blocksRaycasts = false; lcg.interactable = false;

                Row(loc.gameObject, locY - 66, 140, UP);
                loc.GetComponent<Image>().color = new Color(1, 1, 1, 0);
                foreach (var t in loc.GetComponentsInChildren<Text>(true))
                {
                    t.fontSize = 32; t.color = Soft; t.alignment = TextAnchor.MiddleLeft; t.fontStyle = FontStyle.Normal;
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    if (font != null) t.font = font;
                    var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                    trt.offsetMin = new Vector2(112, 0); trt.offsetMax = new Vector2(-32, 0);
                    EditorUtility.SetDirty(t);
                }
            }

            // ── 분류 칩 ──
            const float catY = locY - 250;   // -1334
            var catLabel = FindOrCreate(panel, "CatLabel0926");
            SectionLabelNew(catLabel, UP, catY, new[] { "분류", "Category", "分類", "分类", "Categoría" });
            var catToggle = P("CategoryToggle");
            string[] values = isModel ? new[] { "", "shop", "food", "cafe", "park", "toilet" }
                                      : new[] { "", "shop", "food", "cafe", "park", "toilet", "sport", "landmark", "etc" };
            if (catToggle != null)
            {
                // 원래 순환 토글은 숨기고(동작은 칩이 대신 누른다) 칩으로 바로 고른다
                var tcg = Ensure<CanvasGroup>(catToggle.gameObject);
                tcg.alpha = 0f; tcg.blocksRaycasts = false; tcg.interactable = false;
                BuildChips(panel, catToggle.GetComponent<Toggle>(), FindUploadManager(card, isModel), values, catY - 66);
            }

            // ── 스위치 줄 ──
            float y = catY - 66 - (ChipRowH * 2 + 16) - 40;   // 칩 2줄 아래
            foreach (var n in new[] { "PetFriendlyToggle", "SeparateRestroomsToggle", "InstagramToggle" })
            {
                var tg = P(n);
                if (tg == null) continue;
                SwitchRow(tg.gameObject, y);
                y -= SwitchH;
            }
            var insta = P("InstagramToggle");
            var instaInput = insta != null ? Find(insta, "InstagramAccountInput") : null;
            if (instaInput != null)
            {
                Img(instaInput, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / 30f);
                var irt = RT(instaInput);
                irt.anchorMin = new Vector2(0, 1); irt.anchorMax = new Vector2(1, 1); irt.pivot = new Vector2(0.5f, 1);
                irt.anchoredPosition = new Vector2(0, -SwitchH - 8); irt.sizeDelta = new Vector2(0, 120);
                StyleInputTexts(instaInput, 42, 40);
                var inf = instaInput.GetComponent<InputField>();
                if (inf != null && inf.placeholder != null)
                    Loc(inf.placeholder.gameObject, "인스타그램 ID", "Instagram ID", "インスタグラム ID", "Instagram ID", "ID de Instagram");
            }

            // ── 등록 ──
            var submit = P("SubmitButton");
            if (submit != null)
            {
                PillButton(submit.gameObject, true);
                var srt = RT(submit.gameObject);
                srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(0.5f, 0);
                srt.anchoredPosition = new Vector2(0, 48); srt.sizeDelta = new Vector2(-UP * 2, 150);
                ButtonLabel(submit.gameObject, new[] { "등록하기", "Submit", "登録する", "提交", "Enviar" }, Dark);
                submit.SetAsLastSibling();
            }
        }

        private static MonoBehaviour FindUploadManager(GameObject anyInScene, bool isModel)
        {
            if (isModel) return FindInScene<ModelUploadManager>(anyInScene);
            return FindInScene<CubeUploadManager>(anyInScene);
        }

        private static void BuildChips(Transform panel, Toggle cycle, MonoBehaviour manager, string[] values, float top)
        {
            var box = FindOrCreate(panel, "Chips0926");
            var brt = RT(box);
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1);
            brt.anchoredPosition = new Vector2(0, top); brt.sizeDelta = new Vector2(-UP * 2, ChipRowH * 2 + 16);
            var grid = Ensure<GridLayoutGroup>(box);
            grid.cellSize = new Vector2(290, ChipRowH); grid.spacing = new Vector2(16, 16);
            Ensure<R0926GridFit>(box);   // 네 칸이 카드 폭에 꼭 맞게
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperLeft;

            var chips = Ensure<R0926CategoryChips>(box);
            var bodies = new List<Image>();
            var labels = new List<Text>();
            var words = new Dictionary<string, string[]>
            {
                { "shop", new[] { "샵", "Shop", "ショップ", "商店", "Tienda" } },
                { "food", new[] { "음식점", "Food", "飲食店", "餐厅", "Comida" } },
                { "cafe", new[] { "카페", "Cafe", "カフェ", "咖啡", "Café" } },
                { "park", new[] { "공원", "Park", "公園", "公园", "Parque" } },
                { "toilet", new[] { "화장실", "Restroom", "トイレ", "卫生间", "Baño" } },
                { "sport", new[] { "스포츠", "Sports", "スポーツ", "运动", "Deporte" } },
                { "landmark", new[] { "랜드마크", "Landmark", "名所", "地标", "Lugar" } },
                { "etc", new[] { "기타", "Other", "その他", "其他", "Otro" } },
            };
            // 이전 실행에서 남은 칩 정리
            for (int i = box.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(box.transform.GetChild(i).gameObject);

            for (int i = 1; i < values.Length; i++)
            {
                var w = words[values[i]];
                var chip = NewUI("Chip_" + values[i], box.transform);
                var body = Img(chip, Spr("r0926_pill"), new Color(1, 1, 1, 0.07f), Image.Type.Sliced, 64f / (ChipRowH / 2f));
                body.raycastTarget = true;
                var dot = NewUI("Dot", chip.transform);
                Img(dot, Spr("r0926_circle"), DataManager.GetCategoryColor(values[i]), Image.Type.Simple).raycastTarget = false;
                SetRect(RT(dot), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(42, 0), new Vector2(24, 24));
                var lb = NewUI("Label0926", chip.transform);
                var t = Txt(lb, w[0], 44, Soft, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 24; t.resizeTextMaxSize = 40;
                var lrt = RT(lb); lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(70, 0); lrt.offsetMax = new Vector2(-12, 0);
                Loc(lb, w[0], w[1], w[2], w[3], w[4]);
                var b = Ensure<Button>(chip);
                b.targetGraphic = body;
                UnityEventTools.AddIntPersistentListener(b.onClick, chips.Select, i);
                bodies.Add(body); labels.Add(t);
            }
            var so = new SerializedObject(chips);
            so.FindProperty("cycleToggle").objectReferenceValue = cycle;
            so.FindProperty("manager").objectReferenceValue = manager;
            var vp = so.FindProperty("values");
            vp.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) vp.GetArrayElementAtIndex(i).stringValue = values[i];
            SetArray(so.FindProperty("chipBodies"), bodies.ToArray());
            SetArray(so.FindProperty("chipLabels"), labels.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SwitchRow(GameObject toggle, float y)
        {
            // 원본은 루트 4배 · 글자 0.25배 식으로 배율이 엇갈려 있다 → 모두 1배로
            foreach (var t in toggle.GetComponentsInChildren<Transform>(true)) t.localScale = Vector3.one;
            var rt = RT(toggle);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(-UP * 2, SwitchH);

            var line = FindOrCreate(toggle.transform, "Line0926");
            line.transform.SetSiblingIndex(0);
            Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));

            var bg = toggle.transform.Find("Background");
            if (bg != null)
            {
                Img(bg.gameObject, Spr("r0926_sw_off"), Color.white, Image.Type.Simple).raycastTarget = true;
                SetRect(RT(bg.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(0, -SwitchH / 2f), new Vector2(132, 80));
                var ck = bg.Find("Checkmark");
                if (ck != null)
                {
                    Img(ck.gameObject, Spr("r0926_sw_on"), Color.white, Image.Type.Simple).raycastTarget = false;
                    var crt = RT(ck.gameObject); crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                }
            }
            var label = toggle.transform.Find("Label");
            if (label != null)
            {
                var t = label.GetComponent<Text>();
                t.fontSize = 50; t.color = Ink; t.alignment = TextAnchor.MiddleLeft; t.fontStyle = FontStyle.Normal;
                t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
                t.raycastTarget = true;   // 글자를 눌러도 켜지게 (누름 영역 확대)
                if (font != null) t.font = font;
                var lrt = t.rectTransform;
                lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(0.5f, 1);
                lrt.anchoredPosition = new Vector2(-80, 0); lrt.sizeDelta = new Vector2(-160, SwitchH);
                EditorUtility.SetDirty(t);
            }
        }

        private static void Tile(GameObject go, float x, float y, float w, float h, string icon, float iconSize)
        {
            go.transform.localScale = Vector3.one;
            At(go, x, y, w, h);
            var own = go.GetComponent<Graphic>();
            if (own is RawImage raw)
            {
                // RawImage 버튼(예전 업로드 화살표 그림) — 누름 영역만 남기고 투명하게, 둥근 바탕은 자식으로
                raw.texture = null; raw.color = new Color(1, 1, 1, 0); raw.raycastTarget = true;
                EditorUtility.SetDirty(raw);
                var bgc = FindOrCreate(go.transform, "TileBg0926");
                bgc.transform.SetSiblingIndex(0);
                Img(bgc, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / 34f).raycastTarget = false;
                var brt = RT(bgc); brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            }
            else
            {
                var img = Img(go, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / 34f);
                img.raycastTarget = true;
                img.preserveAspect = false;
            }
            // 원래 아이콘 글자(TMP '+' 등)는 숨긴다
            foreach (var g in go.GetComponentsInChildren<Graphic>(true))
                if (g.gameObject != go && g.name != "Icon0926" && g.name != "TileBg0926") g.enabled = false;
            var ic = FindOrCreate(go.transform, "Icon0926");
            Img(ic, Spr(icon), icon == "r0926_i_check" ? new Color(0.384f, 0.827f, 0.616f, 1f) : Soft, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
        }

        private static void SectionLabel(Transform label, float x, float y, float w)
        {
            if (label == null) return;
            var t = Txt(label.gameObject, null, 44, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            At(label.gameObject, x, y, w, 60);
        }

        private static void SectionLabelNew(GameObject go, float x, float y, string[] words)
        {
            var t = Txt(go, words[0], 44, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            At(go, x, y, 600, 60);
            Loc(go, words[0], words[1], words[2], words[3], words[4]);
        }

        private static void StyleInputTexts(GameObject input, int textSize, int placeholderSize)
        {
            var field = input.GetComponent<InputField>();
            foreach (var t in input.GetComponentsInChildren<Text>(true))
            {
                bool ph = field != null && field.placeholder == t;
                t.fontSize = ph ? placeholderSize : textSize;
                t.color = ph ? Muted : Ink;
                t.alignment = TextAnchor.MiddleLeft;
                if (font != null) t.font = font;
                var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(36, 0); trt.offsetMax = new Vector2(-36, 0);
                EditorUtility.SetDirty(t);
            }
        }

        /// <summary>카드 위쪽 기준, 좌우 여백을 둔 한 줄</summary>
        private static void Row(GameObject go, float y, float h, float side = UP)
        {
            go.transform.localScale = Vector3.one;
            var rt = RT(go);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(-side * 2, h);
            EditorUtility.SetDirty(rt);
        }

        /// <summary>카드 왼쪽 위 기준 고정 크기</summary>
        private static void At(GameObject go, float x, float y, float w, float h)
        {
            go.transform.localScale = Vector3.one;
            SetRect(RT(go), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(w, h));
        }

        private static void SetArray<T>(SerializedProperty prop, T[] items) where T : Object
        {
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ============================================================
        // 상태 화면 — 업데이트 안내 · AR 미리보기 · 사진 선택 · 연속 촬영 · 새 메시지 배너 · 경고
        // ============================================================
        private static void ApplyStatusScreens(Transform root, List<string> log)
        {
            var upd = Find(root, "UpdateChecker");
            if (upd != null)
            {
                upd.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
                var box = FindOrCreate(upd.transform, "Box0926");
                box.transform.SetSiblingIndex(0);
                SetRect(RT(box), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040, 560));
                var text = Find(upd.transform, "Text (Legacy)");
                if (text != null && text.transform.parent != box.transform) text.transform.SetParent(box.transform, false);
                var buttons = FindOrCreate(box.transform, "Buttons0926");
                var yes = Find(upd.transform, "Update_YES");
                var no = Find(upd.transform, "Update_NO");
                if (yes != null) yes.transform.SetParent(buttons.transform, false);
                if (no != null) no.transform.SetParent(buttons.transform, false);
                StyleDialog(box, text, buttons, yes, no,
                    new[] { "업데이트", "Update", "アップデート", "更新", "Actualizar" },
                    new[] { "나중에", "Later", "後で", "稍后", "Más tarde" }, false);
            }

            var prev = Find(root, "ARPreviewPanel");
            var mc = prev != null ? Find(prev.transform, "MessageContainer") : null;
            if (mc != null)
            {
                Img(mc, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 0.98f), Image.Type.Sliced, 64f / 56f);
                var mt = Find(mc.transform, "MessageText");
                if (mt != null) { var t = mt.GetComponent<Text>(); t.fontSize = 44; t.color = Ink; if (font != null) t.font = font; }
                var bc = Find(mc.transform, "ButtonContainer");
                var confirm = bc != null ? Find(bc.transform, "ConfirmButton") : null;
                var cancel = bc != null ? Find(bc.transform, "CancelButton") : null;
                if (bc != null)
                {
                    var hlg = bc.GetComponent<HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.childControlWidth = true; hlg.childControlHeight = true;
                        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
                        hlg.spacing = 24; hlg.padding = new RectOffset(0, 0, 10, 10);
                        EditorUtility.SetDirty(hlg);
                    }
                    if (cancel != null) cancel.transform.SetSiblingIndex(0);   // 취소 왼쪽 · 확인 오른쪽
                }
                if (confirm != null) { PillButton(confirm, true); ButtonLabel(confirm, new[] { "추가하기", "Add here", "追加する", "添加", "Añadir" }, Dark); }
                if (cancel != null) { PillButton(cancel, false); ButtonLabel(cancel, CancelWords, Ink); }
                Slide(mc, 140f);
            }

            // 사진 찍기 / 앨범 — 아래 시트 (스크립트가 BottomContainer 를 아래에서 올리는 움직임은 그대로)
            var photo = Find(root, "PhotoSourceDialog");
            var pCard = photo != null ? Find(photo.transform, "ContentCard") : null;
            if (pCard != null)
            {
                Img(pCard, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f);
                var crt = RT(pCard);
                crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(1, 0); crt.pivot = new Vector2(0.5f, 0);
                crt.anchoredPosition = new Vector2(0, 40); crt.sizeDelta = new Vector2(-CardSide * 2, 150 * 3);
                Ensure<R0926SafeInset>(pCard).SetEdge(R0926SafeInset.Edge.Bottom);
                var row = Find(pCard.transform, "ButtonRow");
                if (row != null)
                {
                    var rrt = RT(row); rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
                }
                SheetRowFromIconButton(Find(pCard.transform, "CameraButton"), 0, "r0926_i_cam", null);
                SheetRowFromIconButton(Find(pCard.transform, "GalleryButton"), 1, "r0926_i_image", null);
                SheetRowFromIconButton(Find(pCard.transform, "CancelButton"), 2, "r0926_i_close", CancelWords);
            }

            var cont = Find(root, "ContinueCaptureDialog");
            var cCard = cont != null ? Find(cont.transform, "ContentCard") : null;
            if (cCard != null)
            {
                Img(cCard, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f);
                var crt = RT(cCard);
                crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(1, 0); crt.pivot = new Vector2(0.5f, 0);
                crt.anchoredPosition = new Vector2(0, 40); crt.sizeDelta = new Vector2(-CardSide * 2, 460);
                Ensure<R0926SafeInset>(cCard).SetEdge(R0926SafeInset.Edge.Bottom);
                var title = Find(cCard.transform, "TitleText");
                if (title != null) { var t = title.GetComponent<Text>(); t.fontSize = 46; t.fontStyle = FontStyle.Bold; t.color = Ink; }
                var msg = Find(cCard.transform, "MessageText");
                if (msg != null) { var t = msg.GetComponent<Text>(); t.fontSize = 38; t.color = Muted; }
                var row = Find(cCard.transform, "ButtonRow");
                if (row != null) SetRect(RT(row), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(-112, 132));
                var noB = Find(cCard.transform, "NoButton");
                var yesB = Find(cCard.transform, "YesButton");
                foreach (var (b, primary) in new[] { (noB, false), (yesB, true) })
                {
                    if (b == null) continue;
                    PillButton(b, primary);
                    var brt = RT(b);
                    brt.anchorMin = new Vector2(primary ? 0.5f : 0, 0); brt.anchorMax = new Vector2(primary ? 1 : 0.5f, 1); brt.pivot = new Vector2(0.5f, 0.5f);
                    brt.offsetMin = new Vector2(primary ? 12 : 0, 0); brt.offsetMax = new Vector2(primary ? 0 : -12, 0);
                    foreach (var t in b.GetComponentsInChildren<Text>(true))
                    {
                        var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
                        t.alignment = TextAnchor.MiddleCenter; t.fontSize = 42;
                    }
                    // 원래 아이콘 스프라이트 대신 글자 버튼
                }
            }

            // 새 메시지 배너 — 코드가 위아래로 움직이는 겉틀은 투명하게, 안에 카드
            var banner = Find(root, "NotificationBanner");
            if (banner != null)
            {
                banner.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                RT(banner).sizeDelta = new Vector2(0, 480);
                var card = FindOrCreate(banner.transform, "Card0926");
                card.transform.SetSiblingIndex(0);
                Img(card, Spr("r0926_pill"), new Color(Card.r, Card.g, Card.b, 0.97f), Image.Type.Sliced, 64f / 48f).raycastTarget = false;
                SetRect(RT(card), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(-CardSide * 2, 200));
                Ensure<R0926SafeInset>(card);
                var mask = Find(banner.transform, "AvatarMask");
                if (mask != null)
                {
                    mask.transform.SetParent(card.transform, false);
                    SetRect(RT(mask), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(120, 120));
                }
                var content = banner.transform.Find("Content") ?? card.transform.Find("Content");
                if (content != null)
                {
                    content.SetParent(card.transform, false);
                    var crt = RT(content.gameObject); crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
                    crt.offsetMin = new Vector2(186, 24); crt.offsetMax = new Vector2(-36, -24);
                    var title = Find(content, "TitleText");
                    if (title != null) { var t = title.GetComponent<Text>(); t.fontSize = 42; t.fontStyle = FontStyle.Bold; t.color = Ink; }
                    var body = Find(content, "BodyText");
                    if (body != null) { var t = body.GetComponent<Text>(); t.fontSize = 36; t.color = Soft; }
                }
            }

            var warn = Find(root, "WarningText");
            if (warn != null)
            {
                Img(warn, Spr("r0926_pill"), new Color(Card.r, Card.g, Card.b, 0.96f), Image.Type.Sliced, 64f / 60f);
                RT(warn).sizeDelta = new Vector2(960, 150);
                foreach (var t in warn.GetComponentsInChildren<Text>(true))
                {
                    t.fontSize = 38; t.color = Ink; t.alignment = TextAnchor.MiddleCenter;
                    var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(40, 0); trt.offsetMax = new Vector2(-40, 0);
                }
            }
            log.Add("status screens ok");
        }

        private static void SheetRowFromIconButton(GameObject btn, int index, string icon, string[] labelWords)
        {
            if (btn == null) return;
            btn.transform.localScale = Vector3.one;
            const float h = 150f;
            SetRect(RT(btn), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -h * index), new Vector2(0, h));
            var img = btn.GetComponent<Image>();
            img.sprite = null; img.color = new Color(1, 1, 1, 0); img.raycastTarget = true;
            if (index > 0)
            {
                var line = FindOrCreate(btn.transform, "Line0926");
                Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-80, 2));
            }
            var ic = FindOrCreate(btn.transform, "Icon0926");
            Img(ic, Spr(icon), labelWords != null ? Muted : Ink, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(ic), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90, 0), new Vector2(60, 60));
            foreach (var t in btn.GetComponentsInChildren<Text>(true))
            {
                if (t.name == "Label0926") continue;
                t.fontSize = 44; t.color = Ink; t.alignment = TextAnchor.MiddleLeft; t.fontStyle = FontStyle.Bold;
                if (font != null) t.font = font;
                var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(150, 0); trt.offsetMax = new Vector2(-40, 0);
                EditorUtility.SetDirty(t);
            }
            if (labelWords != null)
            {
                var lb = FindOrCreate(btn.transform, "Label0926");
                Txt(lb, labelWords[0], 44, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                var lrt = RT(lb); lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(150, 0); lrt.offsetMax = new Vector2(-40, 0);
                Loc(lb, labelWords[0], labelWords[1], labelWords[2], labelWords[3], labelWords[4]);
            }
        }

        // ============================================================
        // 첫 실행 안내 · 인터넷 끊김 안내 · 상세 로마자
        // ============================================================
        // 첫 안내 — 보이는 순서대로 (쪽 오브젝트, 비출 도크 버튼). 쪽 오브젝트는 01=추가 · 02=목록 · 03=메시지
        private static readonly (string page, string slot)[] GuideOrder =
            { ("02", "List_Button"), ("01", "PlusButton"), ("03", "Message_Button") };

        private static GameObject[] GuideOrderPages(Transform panel)
        {
            var r = new GameObject[GuideOrder.Length];
            for (int i = 0; i < r.Length; i++) r[i] = Find(panel, GuideOrder[i].page);
            return r;
        }

        private static void ApplyGuideNetRoman(Transform root, List<string> log)
        {
            // 첫 실행 안내 1~3장 — 화살표 그림을 도크 칸 기준으로, 문구는 '아래 추가/목록/메세지'
            var guide = FindInScene<FirstTimeGuide>(root.gameObject);
            var panel = Find(root, "FirstTimeGuidePanel");
            if (guide != null && panel != null)
            {
                string[] pages = { "01", "02", "03" };
                var pageObjs = new GameObject[3];
                for (int i = 0; i < 3; i++)
                {
                    pageObjs[i] = Find(panel.transform, pages[i]);
                    var img = Find(panel.transform, "Image_" + pages[i]);
                    var sp = Spr("r0926_guide_" + pages[i]);
                    if (img != null && sp != null) { var im = img.GetComponent<Image>(); im.sprite = sp; im.preserveAspect = false; EditorUtility.SetDirty(im); }
                }
                var gso = new SerializedObject(guide);
                var gText = gso.FindProperty("guideText").objectReferenceValue as Text;
                var gt = Ensure<R0926GuideText>(panel);
                var so = new SerializedObject(gt);
                so.FindProperty("guideText").objectReferenceValue = gText;
                SetArray(so.FindProperty("pages"), pageObjs);   // 문구 짝 — 01·02·03 그대로 (보이는 순서와 무관)
                so.ApplyModifiedPropertiesWithoutUndo();

                // 안내를 3장으로 — FirstTimeGuide 는 guidePages 길이로 쪽수·점·확인 버튼을 정한다
                // 보이는 순서: 목록 → 추가 → 메시지 (GuideOrder). 펄스 대상의 pageIndex 도 그림이 든 쪽의 새 자리로
                if (System.Array.TrueForAll(pageObjs, p => p != null))
                {
                    var ordered = GuideOrderPages(panel.transform);
                    SetArray(gso.FindProperty("guidePages"), ordered);
                    var order = gso.FindProperty("pageOrder");
                    order.arraySize = GuideOrder.Length;
                    for (int i = 0; i < GuideOrder.Length; i++) order.GetArrayElementAtIndex(i).stringValue = GuideOrder[i].page;
                    var hl = gso.FindProperty("pageHighlights");
                    for (int i = 0; i < hl.arraySize; i++)
                    {
                        var e = hl.GetArrayElementAtIndex(i);
                        var g = e.FindPropertyRelative("targetGraphic").objectReferenceValue as Component;
                        int k = g == null ? -1 : System.Array.FindIndex(ordered, p => p != null && g.transform.IsChildOf(p.transform));
                        if (k >= 0) e.FindPropertyRelative("pageIndex").intValue = k;
                    }
                    gso.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var extra in new[] { "04", "05", "06" })
                    {
                        var p = Find(panel.transform, extra);
                        if (p != null && p.activeSelf) p.SetActive(false);
                    }
                }
            }

            // 인터넷 끊김 안내 — 맨 위 레이어
            var net = FindOrCreate(root, "NetBanner0926");
            net.transform.SetAsLastSibling();
            SetRect(RT(net), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(-CardSide * 2, 220));
            Ensure<R0926SafeInset>(net);
            Img(net, Spr("r0926_pill"), new Color(Card.r, Card.g, Card.b, 0.97f), Image.Type.Sliced, 64f / 48f).raycastTarget = false;
            OnTop(net);
            var cg = Ensure<CanvasGroup>(net);
            cg.alpha = 0f;
            var ic = FindOrCreate(net.transform, "Icon0926");
            Img(ic, Spr("r0926_i_wifioff"), Soft, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(ic), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(82, 0), new Vector2(72, 72));
            var dot = FindOrCreate(net.transform, "Dot0926");
            var dImg = Img(dot, Spr("r0926_circle"), new Color(1f, 0.72f, 0.2f, 1f), Image.Type.Simple);
            dImg.raycastTarget = false;
            SetRect(RT(dot), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(112, 28), new Vector2(22, 22));
            var title = FindOrCreate(net.transform, "Title0926");
            var tt = Txt(title, "인터넷에 연결되어 있지 않아요", 40, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(RT(title), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(60, -28), new Vector2(-240, 56));
            var body = FindOrCreate(net.transform, "Body0926");
            var bt = Txt(body, "Wi-Fi나 모바일 데이터가 켜져 있는지 확인해 주세요 (설정 › 네트워크 및 인터넷). 주변 장소와 메시지를 불러오지 못합니다.", 30, Soft, TextAnchor.UpperLeft, FontStyle.Normal);
            bt.horizontalOverflow = HorizontalWrapMode.Wrap; bt.lineSpacing = 1.1f;
            SetRect(RT(body), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(60, -88), new Vector2(-240, 120));
            var nb = Ensure<R0926NetworkBanner>(net);
            var nso = new SerializedObject(nb);
            nso.FindProperty("title").objectReferenceValue = tt;
            nso.FindProperty("body").objectReferenceValue = bt;
            nso.FindProperty("dot").objectReferenceValue = dImg;
            nso.ApplyModifiedPropertiesWithoutUndo();

            // 상세 화면 — 이름 아래 로마자 (한국어 기기에선 비어 있음)
            var full = Find(root, "FullScreenPanel");
            var placeName = full != null ? Find(full.transform, "PlaceName") : null;
            if (placeName != null)
            {
                var roman = FindOrCreate(placeName.transform.parent, "Roman0926");
                roman.transform.SetSiblingIndex(placeName.transform.GetSiblingIndex() + 1);
                var rt = Txt(roman, "", 40, Muted, TextAnchor.UpperCenter, FontStyle.Normal);
                rt.horizontalOverflow = HorizontalWrapMode.Wrap;
                SetRect(RT(roman), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -680), new Vector2(1400, 60));
                var rl = Ensure<R0926RomanLine>(roman);
                var rso = new SerializedObject(rl);
                rso.FindProperty("source").objectReferenceValue = placeName.GetComponent<Text>();
                rso.ApplyModifiedPropertiesWithoutUndo();
                foreach (var n in new[] { "CreatedByText", "TourAPI_Description" })
                {
                    var g = Find(full.transform, n);
                    if (g != null) RT(g).anchoredPosition = new Vector2(RT(g).anchoredPosition.x, -750);
                }
            }
            log.Add("guide/net/roman ok");
        }
    }
}
