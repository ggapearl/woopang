using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v15 (2026-10-04 아이폰 1.2.53 확인 후):
    ///  · 도크 — 창이 열려 있으면 그 칸 버튼(봉투·'메시지')은 감추고 창 쪽 X '닫기'만 (겹쳐 '메닫기'로 보였다)
    ///  · 창 위 손잡이 바 — 목록처럼 메시지·대화·장소 추가·3D모델·팔로우·AI·신고·더보기에도 (프로필 제외 · 추가는 10-04 에 뺐다가 v19 에 다시)
    ///  · 프로필 카드 — 시안대로 촘촘하게 (보이는 것만 차례로 쌓기 · 공개 상태 알약 · 숫자 크게)
    ///  · 채팅·키보드 위 입력줄 — 댓글 입력줄과 같은 둥근 유리 칸 + 분홍 보내기
    ///  · 메시지 검색칸 — 같은 유리 칸
    ///  · 하늘 날씨 — 본판 80%, 3시간·6시간·내일은 그 아래 작게(60%)
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static readonly Color CardBg = new Color(0.082f, 0.094f, 0.110f, 1f);   // #15181C
        private static readonly Color Glass7 = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color SendOff = new Color(0.227f, 0.251f, 0.275f, 1f);

        private static void Apply1004(Transform root, List<string> log)
        {
            DockSlots(root, log);
            Grabs(root, log);
            ProfileCard(root, log);
            InputBars(root, log);
            MessageSearch(root, log);
            MessageTitleSub(root, log);
            WeatherSizes(root, log);
            FixPageCard(root, log);
            UpdateCard(root, log);
            ChatNavWire(root, log);
            log.Add("1004 ok");
        }

        // ── 메시지 목록 ↔ 대화방을 옆으로 넘기기 ─────────────────────────
        // ← · 뒤로가기는 대화방을 아래로 내리는 대신 옆으로 빼고 목록으로 (R0926ChatNav),
        // 대화방을 아래로 밀면 메시지 전체를 닫는다(도크 X 와 같게) — 목록이 아래에서 다시 올라오지 않게
        private static void ChatNavWire(Transform root, List<string> log)
        {
            var mpm = FindInScene<MessagePanelManager>(root.gameObject);
            var msg = root.Find("MessagePanel");
            var chat = root.Find("ChatRoomPanel");
            var msgSheet = root.Find("MessagePanel/Clip0926/Background") as RectTransform;
            var chatSheet = root.Find("ChatRoomPanel/Clip0926/Background") as RectTransform;
            var back = root.Find("ChatRoomPanel/Clip0926/Background/Header/BackButton");
            var dockX = root.Find("ChatRoomPanel/DockMirror0926/CloseButton");
            if (mpm == null || msg == null || chat == null || msgSheet == null || chatSheet == null || back == null)
            {
                log.Add("대화 넘기기: 대상 없음");
                return;
            }
            var nav = Ensure<R0926ChatNav>(mpm.gameObject);
            var so = new SerializedObject(nav);
            so.FindProperty("messagePanel").objectReferenceValue = msg.gameObject;
            so.FindProperty("chatPanel").objectReferenceValue = chat.gameObject;
            so.FindProperty("messageSheet").objectReferenceValue = msgSheet;
            so.FindProperty("chatSheet").objectReferenceValue = chatSheet;
            so.FindProperty("messageSlide").objectReferenceValue = msgSheet.GetComponent<R0926SlideIn>();
            so.FindProperty("chatSlide").objectReferenceValue = chatSheet.GetComponent<R0926SlideIn>();
            so.FindProperty("backButton").objectReferenceValue = back.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var px = back.Find("ClosePx0926");
            var pb = px != null ? px.GetComponent<Button>() : null;
            if (pb != null)
            {
                ResetListeners(pb);
                UnityEventTools.AddPersistentListener(pb.onClick, nav.Back);
                EditorUtility.SetDirty(pb);
            }
            else log.Add("대화 넘기기: ← 덮개 없음");

            var sd = chatSheet.GetComponent<R0926SwipeDismiss>();
            var xb = dockX != null ? dockX.GetComponent<Button>() : null;
            if (sd != null && xb != null)
            {
                var sso = new SerializedObject(sd);
                sso.FindProperty("closeButton").objectReferenceValue = xb;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("chat nav ok" + (mpm.gameObject.activeInHierarchy ? "" : " (매니저 꺼짐!)"));
        }

        // ── 업데이트 안내 — 프로필 카드와 같은 모양 ─────────────────
        // 예전: 가운데 작은 대화상자에 글 한 덩어리. 강제일 땐 '제목\n\n…N초 후 이동' 을 한 칸에 몰아 썼다.
        // 판단·이동은 AutoUpdateChecker 그대로 — 카드(R0926UpdateCard)는 글과 모양만.
        private static void UpdateCard(Transform root, List<string> log)
        {
            var upd = Find(root, "UpdateChecker");
            var box = upd != null ? upd.transform.Find("Box0926") : null;
            if (box == null) { log.Add("업데이트 안내 없음"); return; }
            AutoUpdateChecker checker = null;
            foreach (var r in root.gameObject.scene.GetRootGameObjects())
            {
                checker = r.GetComponentInChildren<AutoUpdateChecker>(true);
                if (checker != null) break;
            }

            var dim = upd.GetComponent<Image>();
            if (dim != null) { dim.color = new Color(0.016f, 0.024f, 0.031f, 0.72f); EditorUtility.SetDirty(dim); }
            const float W = 1160f, Inner = 1052f;
            Img(box.gameObject, Spr("r0926_pill"), CardBg, Image.Type.Sliced, 64f / 104f).raycastTarget = true;
            var brt = RT(box.gameObject);
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(W, 900);

            // 강아지 마크 — 분홍 테두리 원 (프로필 사진 자리와 같은 느낌)
            var emblem = FindOrCreate(box, "Emblem0926");
            Img(emblem, Spr("r0926_circle"), new Color(Pink.r, Pink.g, Pink.b, 0.16f), Image.Type.Simple).raycastTarget = false;
            RT(emblem).sizeDelta = new Vector2(210, 210);
            var ring = FindOrCreate(emblem.transform, "Ring0926");
            Img(ring, Spr("r0926_ring"), Pink, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(ring), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210, 210));
            var mark = FindOrCreate(emblem.transform, "Mark0926");
            var mImg = Img(mark, Spr("r0926_emblem"), Color.white, Image.Type.Simple);
            mImg.raycastTarget = false; mImg.preserveAspect = true;
            SetRect(RT(mark), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124, 124));

            var title = FindOrCreate(box, "Title0926");
            var tTxt = Txt(title, "새 버전이 나왔어요", 62, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            tTxt.horizontalOverflow = HorizontalWrapMode.Overflow; tTxt.raycastTarget = false;
            RT(title).sizeDelta = new Vector2(Inner, 90);

            var body = Find(box, "Text (Legacy)");
            Text bTxt = null;
            if (body != null)
            {
                bTxt = body.GetComponent<Text>();
                bTxt.fontSize = 44; bTxt.color = Soft; bTxt.alignment = TextAnchor.MiddleCenter; bTxt.fontStyle = FontStyle.Normal;
                bTxt.lineSpacing = 1.15f; bTxt.raycastTarget = false;
                bTxt.horizontalOverflow = HorizontalWrapMode.Wrap; bTxt.verticalOverflow = VerticalWrapMode.Overflow;
                if (font != null) bTxt.font = font;
                EditorUtility.SetDirty(bTxt);
                RT(body).sizeDelta = new Vector2(Inner, 124);
            }

            var vPill = FindOrCreate(box, "Version0926");
            Img(vPill, Spr("r0926_pill"), new Color(1, 1, 1, 0.07f), Image.Type.Sliced, 64f / 38f).raycastTarget = false;
            RT(vPill).sizeDelta = new Vector2(420, 76);
            var vText = FindOrCreate(vPill.transform, "Label0926");
            var vTxt = Txt(vText, "1.2.52   →   1.2.54", 38, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            vTxt.horizontalOverflow = HorizontalWrapMode.Overflow; vTxt.raycastTarget = false;
            var vrt = RT(vText); vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;

            // 강제일 때 — 스토어로 넘어가기까지 줄어드는 분홍 막대
            var cd = FindOrCreate(box, "Countdown0926");
            Img(cd, Spr("r0926_pill"), new Color(1, 1, 1, 0.08f), Image.Type.Sliced, 64f / 5f).raycastTarget = false;
            RT(cd).sizeDelta = new Vector2(560, 10);
            var fill = FindOrCreate(cd.transform, "Fill0926");
            Img(fill, Spr("r0926_pill"), Pink, Image.Type.Sliced, 64f / 5f).raycastTarget = false;
            var frt = RT(fill); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.pivot = new Vector2(0, 0.5f); frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            cd.SetActive(false);

            // 버튼 — 업데이트(흰 알약, 꽉 차게) · 나중에(글자만)
            var yes = Find(upd.transform, "Update_YES");
            var no = Find(upd.transform, "Update_NO");
            if (yes != null)
            {
                yes.transform.SetParent(box, false);
                PillButton(yes, true);
                ButtonLabel(yes, new[] { "업데이트", "Update", "アップデート", "更新", "Actualizar" }, Dark);
                RT(yes).sizeDelta = new Vector2(Inner, 150);
            }
            if (no != null)
            {
                no.transform.SetParent(box, false);
                var nImg = Img(no, null, new Color(1, 1, 1, 0), Image.Type.Simple);
                nImg.raycastTarget = true;
                foreach (var t in no.GetComponentsInChildren<Text>(true)) if (t.name != "Label0926") t.enabled = false;
                ButtonLabel(no, new[] { "나중에", "Later", "後で", "稍后", "Más tarde" }, Muted);
                RT(no).sizeDelta = new Vector2(420, 96);
                BackCloses(no.transform, "업데이트 나중에", log);
            }
            var oldButtons = box.Find("Buttons0926");
            if (oldButtons != null) oldButtons.gameObject.SetActive(false);

            var card = Ensure<R0926UpdateCard>(upd);
            var so = new SerializedObject(card);
            so.FindProperty("card").objectReferenceValue = brt;
            var items = new List<RectTransform> { RT(emblem), RT(title) };
            var gaps = new List<float> { 0f, 40f };
            if (body != null) { items.Add(RT(body)); gaps.Add(10f); }
            items.Add(RT(vPill)); gaps.Add(26f);
            items.Add(RT(cd)); gaps.Add(44f);
            if (yes != null) { items.Add(RT(yes)); gaps.Add(48f); }
            if (no != null) { items.Add(RT(no)); gaps.Add(12f); }
            var st = so.FindProperty("stack"); st.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) st.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            var gp = so.FindProperty("gaps"); gp.arraySize = gaps.Count;
            for (int i = 0; i < gaps.Count; i++) gp.GetArrayElementAtIndex(i).floatValue = gaps[i];
            so.FindProperty("title").objectReferenceValue = tTxt;
            so.FindProperty("body").objectReferenceValue = bTxt;
            so.FindProperty("version").objectReferenceValue = vTxt;
            so.FindProperty("versionPill").objectReferenceValue = RT(vPill);
            so.FindProperty("countdown").objectReferenceValue = cd;
            so.FindProperty("countdownFill").objectReferenceValue = frt;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (checker != null)
            {
                var cso = new SerializedObject(checker);
                cso.FindProperty("card").objectReferenceValue = card;
                cso.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("update card ok" + (checker == null ? " (검사기 없음)" : ""));
        }

        // ── 장소 수정 — 장소 추가 카드와 같은 모양으로 ───────────────
        // 예전 화면: 흰 테두리·흰 입력칸, 분류 토글엔 이름표가 없고 반려동물 등 글자가 한 칸씩 어긋난 체크박스 옆에 붙어 있었다.
        // 동작은 CubeDataFixManager 그대로 — 자리·모양만 바꾸고 분류는 장소 추가처럼 칩이 순환 토글을 대신 누른다.
        private static void FixPageCard(Transform root, List<string> log)
        {
            var page = Find(root, "Fixpage");
            var card = page != null ? page.transform.Find("FixUploadPage") : null;
            var panel = card != null ? card.Find("Panel") : null;
            if (panel == null) { log.Add("장소 수정 없음"); return; }
            Transform P(string n) => panel.Find(n);

            Img(card.gameObject, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius).raycastTarget = true;
            var crt = RT(card.gameObject);
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.pivot = new Vector2(0.5f, 0.5f);
            crt.offsetMin = new Vector2(UploadMargin, 390); crt.offsetMax = new Vector2(-UploadMargin, -250);   // 장소 추가 카드와 같은 자리 (도크 위)
            foreach (var o in card.GetComponents<Outline>()) Object.DestroyImmediate(o);
            var prt = RT(panel.gameObject);
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero; prt.sizeDelta = Vector2.zero;
            var pImg = panel.GetComponent<Image>();
            if (pImg != null) { pImg.color = new Color(0, 0, 0, 0); EditorUtility.SetDirty(pImg); }

            var title = card.Find("TITLE");
            if (title != null)
            {
                var t = Txt(title.gameObject, null, 56, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                Row(title.gameObject, -44, 84);
            }

            // 닫기 — 위 큰 흰 X 를 카드 오른쪽 위 작은 원으로
            var x = Find(page.transform, "XButton_FixUpload");
            if (x != null)
            {
                x.transform.SetParent(card, false);
                x.transform.localScale = Vector3.one;
                SetRect(RT(x), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -30), new Vector2(110, 110));
                var own = x.GetComponent<Graphic>();
                if (own is RawImage raw) { raw.texture = null; raw.color = new Color(1, 1, 1, 0.12f); }
                else Img(x, Spr("r0926_circle"), new Color(1, 1, 1, 0.12f), Image.Type.Simple).raycastTarget = true;
                foreach (var g in x.GetComponentsInChildren<Graphic>(true)) if (g.gameObject != x && g.name != "Icon0926" && g.name != "ClosePx0926") g.enabled = false;   // 밀어 닫기 덮개(v21)는 남긴다
                var ic = FindOrCreate(x.transform, "Icon0926");
                Img(ic, Spr("r0926_i_close"), Ink, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
                var xp = page.transform.Find("XButton_Panel");
                if (xp != null) { var xi = xp.GetComponent<Graphic>(); if (xi != null) xi.enabled = false; }
                BackCloses(x.transform, "장소 수정 X", log);
            }

            // 이름 · 설명
            float y = -170f;
            foreach (var (label, input, ph) in new[]
            {
                ("Changed Name", "NameInput", new[] { "장소 이름", "Place name", "場所の名前", "地点名称", "Nombre del lugar" }),
                ("Description", "DescriptionInput", new[] { "장소 설명", "Description", "説明", "地点说明", "Descripción" }),
            })
            {
                SectionLabel(P(label), UP, y, 800);
                var inp = P(input);
                if (inp != null)
                {
                    Img(inp.gameObject, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / 34f);
                    foreach (var o in inp.GetComponents<Outline>()) Object.DestroyImmediate(o);
                    Row(inp.gameObject, y - 66, 140, UP);
                    StyleInputTexts(inp.gameObject, 48, 44);
                    var f = inp.GetComponent<InputField>();
                    if (f != null && f.placeholder != null) Loc(f.placeholder.gameObject, ph[0], ph[1], ph[2], ph[3], ph[4]);
                }
                y -= 250f;
            }

            // 사진 — 로고 한 칸 + 추가 사진
            const float tile = 400f, sub = 188f, gap = 24f;
            float photoY = y - 66f;
            SectionLabel(P("Add_Logo"), UP, y, 400);
            SectionLabel(P("Add_Imgs"), UP + tile + gap, y, 700);
            var main = P("MainPhoto_Button");
            if (main != null) Tile(main.gameObject, UP, photoY, tile, tile, "r0926_i_cam", 116);
            var display = P("MainPhotoDisplay");
            if (display != null)
            {
                At(display.gameObject, UP, photoY, tile, tile);
                var di = display.GetComponent<Image>(); if (di != null) { di.preserveAspect = false; di.type = Image.Type.Simple; }
            }
            var subBtn = P("SubPhoto_Button");
            if (subBtn != null) Tile(subBtn.gameObject, UP + tile + gap, photoY, sub, sub, "r0926_i_plus", 76);
            var reset = P("SubPhoto_Reset");
            if (reset != null) Tile(reset.gameObject, UP + tile + gap, photoY - sub - gap, sub, sub, "r0926_i_reset", 68);
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
                    grid.cellSize = new Vector2(sub, sub); grid.spacing = new Vector2(16, 16);
                    grid.startCorner = GridLayoutGroup.Corner.UpperLeft; grid.childAlignment = TextAnchor.UpperLeft;
                    grid.constraint = GridLayoutGroup.Constraint.Flexible;
                    EditorUtility.SetDirty(grid);
                    FitGrid(grid, sub);
                }
            }
            PhotoLoadingSkeleton(panel, display, UP, photoY, tile, subs, sub, FindInScene<CubeDataFixManager>(card.gameObject), log);
            var loading = P("SubPhoto_LoadingText");
            if (loading != null)
            {
                Txt(loading.gameObject, null, 36, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
                At(loading.gameObject, UP + tile + gap, photoY - tile - 8, 700, 50);
            }

            // 분류 칩
            float catY = photoY - tile - 60f;
            var catLabel = FindOrCreate(panel, "CatLabel0926");
            SectionLabelNew(catLabel, UP, catY, new[] { "분류", "Category", "分類", "分类", "Categoría" });
            var catToggle = P("CategoryToggle");
            if (catToggle != null)
            {
                var tcg = Ensure<CanvasGroup>(catToggle.gameObject);
                tcg.alpha = 0f; tcg.blocksRaycasts = false; tcg.interactable = false;
                BuildChips(panel, catToggle.GetComponent<Toggle>(), FindInScene<CubeDataFixManager>(card.gameObject),
                    new[] { "", "shop", "food", "cafe", "park", "toilet", "sport", "landmark", "etc" }, catY - 66);
            }

            // 스위치 줄 — 이름표(따로 놓여 있던 글)를 각 토글 안으로 옮겨 같은 줄에 세운다
            float sy = catY - 66 - (ChipRowH * 2 + 16) - 40;
            foreach (var (tName, lName) in new[] { ("PetFriendlyToggle", "PetFriendly"), ("SeparateRestroomsToggle", "SeparatedRestroom"), ("InstagramToggle", "InstagramID") })
            {
                var tg = P(tName);
                if (tg == null) continue;
                SwitchRow(tg.gameObject, sy);
                var own = tg.Find("Label");
                if (own != null) { var ot = own.GetComponent<Text>(); if (ot != null) ot.enabled = false; }
                var lb = P(lName);
                if (lb != null)
                {
                    lb.SetParent(tg, false);
                    lb.localScale = Vector3.one;
                    var t = lb.GetComponent<Text>();
                    if (t != null)
                    {
                        t.fontSize = 50; t.color = Ink; t.alignment = TextAnchor.MiddleLeft; t.fontStyle = FontStyle.Normal;
                        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
                        t.raycastTarget = true;
                        if (font != null) t.font = font;
                        EditorUtility.SetDirty(t);
                    }
                    var lrt = RT(lb.gameObject);
                    lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(0.5f, 1);
                    lrt.anchoredPosition = new Vector2(-80, 0); lrt.sizeDelta = new Vector2(-160, SwitchH);
                }
                sy -= SwitchH;
            }
            var insta = P("InstagramAccountInput");
            if (insta != null)
            {
                Img(insta.gameObject, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / 30f);
                foreach (var o in insta.GetComponents<Outline>()) Object.DestroyImmediate(o);
                Row(insta.gameObject, sy - 8, 120, UP);
                StyleInputTexts(insta.gameObject, 42, 40);
                var inf = insta.GetComponent<InputField>();
                if (inf != null && inf.placeholder != null)
                    Loc(inf.placeholder.gameObject, "인스타그램 ID", "Instagram ID", "インスタグラム ID", "Instagram ID", "ID de Instagram");
            }

            // 저장
            var submit = P("SubmitButton");
            if (submit != null)
            {
                foreach (var g in submit.GetComponentsInChildren<Graphic>(true)) if (g.gameObject != submit.gameObject && g.name != "Label0926") g.enabled = false;
                var rawOld = submit.GetComponent<RawImage>();   // 예전 체크 그림 버튼 — Image 와 같이 둘 수 없다
                if (rawOld != null) Object.DestroyImmediate(rawOld);
                PillButton(submit.gameObject, true);
                var sb = submit.GetComponent<Button>();
                if (sb != null) { sb.targetGraphic = submit.GetComponent<Image>(); EditorUtility.SetDirty(sb); }
                var srt = RT(submit.gameObject);
                srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(0.5f, 0);
                srt.anchoredPosition = new Vector2(0, 48); srt.sizeDelta = new Vector2(-UP * 2, 150);
                // 남의 장소는 바로 고쳐지지 않는다 — '수정 요청' (내 장소면 CubeDataFixManager 가 '수정하기'로 바꾼다)
                ButtonLabel(submit.gameObject, new[] { "수정 요청", "Send request", "修正を依頼", "提交申请", "Enviar solicitud" }, Dark);
                submit.SetAsLastSibling();
            }
            FixPageOwner(root, card, panel, log);
            log.Add("fix page ok (" + (-sy + 128) + " / card)");
        }

        // ── 내가 올린 장소 고치기 (2026-10-26) ───────────────────────────
        // 대표님 (10-25): 「'정보 수정' 버튼을 눌러서 수정할 때는 기존에 있던 것들을 그대로 불러와서 각 사진 우측 위에 (메인사진포함)
        // x버튼을 적당히 작게」 · 「본인이 올린 것에 한해서는 직접 삭제」 · 「위치가 좀 바뀌었다면 … 수정한 사람이 수정하는 경우 바로 적용」
        // 위치 줄·삭제는 내 장소일 때만 CubeDataFixManager 가 켠다 (남의 장소는 예전처럼 '수정할 내용' 칸)
        private static void FixPageOwner(Transform root, Transform card, Transform panel, List<string> log)
        {
            var mgr = FindInScene<CubeDataFixManager>(card.gameObject);
            if (mgr == null) { log.Add("내 장소 고치기 — 매니저 없음"); return; }

            // 위치 — '수정할 내용' 자리에 (둘 중 하나만 보인다)
            var locLabel = FindOrCreate(panel, "LocLabel0926");
            SectionLabelNew(locLabel, UP, -420, new[] { "위치", "Location", "位置", "位置", "Ubicación" });
            locLabel.SetActive(false);
            var row = FindOrCreate(panel, "LocRow0926");
            Img(row, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / 34f).raycastTarget = false;
            Row(row, -486, 140, UP);
            var coords = FindOrCreate(row.transform, "Coords0926");
            var ct = Txt(coords, "—", 44, Ink, TextAnchor.LowerLeft, FontStyle.Bold);
            ct.horizontalOverflow = HorizontalWrapMode.Overflow;
            var crt = RT(coords);
            crt.anchorMin = new Vector2(0, 0.5f); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0, 0.5f);
            crt.offsetMin = new Vector2(36, 0); crt.offsetMax = new Vector2(-420, -12);
            var note = FindOrCreate(row.transform, "Note0926");
            var nt = Txt(note, "", 34, Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            nt.horizontalOverflow = HorizontalWrapMode.Wrap; nt.verticalOverflow = VerticalWrapMode.Truncate;
            nt.resizeTextForBestFit = true; nt.resizeTextMinSize = 24; nt.resizeTextMaxSize = 34;
            var nrt = RT(note);
            nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 0.5f); nrt.pivot = new Vector2(0, 0.5f);
            nrt.offsetMin = new Vector2(36, 8); nrt.offsetMax = new Vector2(-420, 0);

            // '여기로 옮기기' — 켜고 저장하면 지금 내 위치로 (기본 꺼짐 · 글자를 눌러도 켜진다)
            var mv = FindOrCreate(row.transform, "MoveHere0926");
            mv.transform.localScale = Vector3.one;
            var mrt = RT(mv);
            mrt.anchorMin = new Vector2(1, 0); mrt.anchorMax = new Vector2(1, 1); mrt.pivot = new Vector2(1, 0.5f);
            mrt.anchoredPosition = new Vector2(-28, 0); mrt.sizeDelta = new Vector2(380, 0);
            Img(mv, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            var sw = FindOrCreate(mv.transform, "Background");
            var swImg = Img(sw, Spr("r0926_sw_off"), Color.white, Image.Type.Simple);
            swImg.raycastTarget = false;
            SetRect(RT(sw), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(132, 80));
            var ck = FindOrCreate(sw.transform, "Checkmark");
            var ckImg = Img(ck, Spr("r0926_sw_on"), Color.white, Image.Type.Simple);
            ckImg.raycastTarget = false;
            var ckrt = RT(ck); ckrt.anchorMin = Vector2.zero; ckrt.anchorMax = Vector2.one; ckrt.offsetMin = Vector2.zero; ckrt.offsetMax = Vector2.zero;
            var mvl = FindOrCreate(mv.transform, "Label0926");
            var mt = Txt(mvl, "여기로 옮기기", 34, Soft, TextAnchor.MiddleRight, FontStyle.Bold);
            mt.resizeTextForBestFit = true; mt.resizeTextMinSize = 24; mt.resizeTextMaxSize = 34;
            var mlr = RT(mvl); mlr.anchorMin = Vector2.zero; mlr.anchorMax = Vector2.one; mlr.offsetMin = Vector2.zero; mlr.offsetMax = new Vector2(-152, 0);
            Loc(mvl, "여기로 옮기기", "Move here", "ここに移動", "移到这里", "Mover aquí");
            var tg = Ensure<Toggle>(mv);
            tg.targetGraphic = swImg; tg.graphic = ckImg;
            tg.transition = Selectable.Transition.None;
            tg.toggleTransition = Toggle.ToggleTransition.Fade;
            tg.isOn = false;
            EditorUtility.SetDirty(tg);
            row.SetActive(false);

            // 사진 x — 대표 사진은 사진 위, 추가 사진은 칸마다 복제해 넣는 본 (CubeDataFixManager 가 풀을 만들 때)
            var display = panel.Find("MainPhotoDisplay");
            Button mainX = display != null ? RemoveX(display, "RemoveX0926", 120, 60, 10) : null;
            Button subX = SubRemoveX(panel, "SubRemoveX0926_Template");   // 칸 크기를 따라가는 x (10-26)
            subX.gameObject.SetActive(false);
            SubPhotoTools(panel, panel.Find("SubPhotoContainer"), mgr, false, log);   // 꾹 눌러 옮기기 (10-26)

            // 삭제 — 카드 오른쪽 위, 닫기 X 왼쪽 (내 장소일 때만 보인다)
            var del = FindOrCreate(card, "DeletePlace0926");
            del.transform.localScale = Vector3.one;
            SetRect(RT(del), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-170, -30), new Vector2(250, 110));
            Img(del, Spr("r0926_pill"), new Color(Danger.r, Danger.g, Danger.b, 0.14f), Image.Type.Sliced, 64f / 55f).raycastTarget = true;
            var dIc = FindOrCreate(del.transform, "Icon0926");
            Img(dIc, Spr("r0926_i_trash"), Danger, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(dIc), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54, 0), new Vector2(48, 48));
            var dLb = FindOrCreate(del.transform, "Label0926");
            var dt = Txt(dLb, "삭제", 42, Danger, TextAnchor.MiddleLeft, FontStyle.Bold);
            dt.resizeTextForBestFit = true; dt.resizeTextMinSize = 28; dt.resizeTextMaxSize = 42;
            var dlr = RT(dLb); dlr.anchorMin = Vector2.zero; dlr.anchorMax = Vector2.one; dlr.offsetMin = new Vector2(92, 0); dlr.offsetMax = new Vector2(-20, 0);
            Loc(dLb, "삭제", "Delete", "削除", "删除", "Eliminar");
            var delBtn = Ensure<Button>(del);
            delBtn.targetGraphic = del.GetComponent<Image>();
            ResetListeners(delBtn);
            del.SetActive(false);

            var rr = FindInScene<RemoveRequest>(card.gameObject);
            var open = Find(root, "FixPlaceButton");
            var desc = panel.Find("Description");
            var so = new SerializedObject(mgr);
            void Wire(SerializedObject o, string prop, Object v)
            {
                var p = o.FindProperty(prop);
                if (p == null) { log.Add(o.targetObject.GetType().Name + ": " + prop + " 칸 없음"); return; }
                p.objectReferenceValue = v;
                if (v == null) log.Add(o.targetObject.GetType().Name + ": " + prop + " 찾지 못함");
            }
            Wire(so, "openButton", open != null ? open.GetComponent<Button>() : null);
            Wire(so, "mainPhotoRemove", mainX);
            Wire(so, "subPhotoRemoveTemplate", subX);
            Wire(so, "descriptionLabel", desc != null ? desc.gameObject : null);
            Wire(so, "locationLabel", locLabel);
            Wire(so, "locationRow", row);
            Wire(so, "locationCoords", ct);
            Wire(so, "locationNote", nt);
            Wire(so, "moveHereToggle", tg);
            Wire(so, "deletePlaceButton", delBtn);
            Wire(so, "localizer", card.GetComponentInChildren<FixPlaceLocalizer>(true));
            Wire(so, "removeRequest", rr);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 장소 '···' 의 '삭제 요청' 칸 · 묻는 창 — 내 장소면 RemoveRequest 가 '삭제'·'지우기'로 바꿔 끼운다
            if (rr != null)
            {
                var remPanel = Find(root, "RemoveRequestPanel");
                R0926LocalizedText LT(GameObject go, string child)
                {
                    var t = go != null ? go.transform.Find(child) : null;
                    return t != null ? t.GetComponent<R0926LocalizedText>() : null;
                }
                var rso = new SerializedObject(rr);
                Wire(rso, "menuLabel", LT(open != null ? Find(open.transform.parent, "RemovePlaceButton") : null, "Label0926"));
                Wire(rso, "dialogText", LT(remPanel, "Box0926/Text"));
                Wire(rso, "yesLabel", LT(remPanel != null ? Find(remPanel.transform, "Remove_YES") : null, "Label0926"));
                rso.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("내 장소 고치기 ok");
        }

        // 사진 오른쪽 위 작은 x — 누름 영역은 동그라미보다 넓게 (사진 칸 자체는 누를 일이 없다)
        private static Button RemoveX(Transform parent, string name, float hit, float dot, float inset)
        {
            var go = FindOrCreate(parent, name);
            go.transform.localScale = Vector3.one;
            go.transform.SetAsLastSibling();
            SetRect(RT(go), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(hit, hit));
            Img(go, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            var d = FindOrCreate(go.transform, "Dot0926");
            var di = Img(d, Spr("r0926_circle"), new Color(0.05f, 0.06f, 0.07f, 0.78f), Image.Type.Simple);
            di.raycastTarget = false;
            SetRect(RT(d), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-inset, -inset), new Vector2(dot, dot));
            var ic = FindOrCreate(d.transform, "Icon0926");
            Img(ic, Spr("r0926_i_close"), Ink, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(dot * 0.5f, dot * 0.5f));
            var b = Ensure<Button>(go);
            b.targetGraphic = di;
            ResetListeners(b);
            return b;
        }

        // ── 도크 칸 감추기 ──────────────────────────────────────
        private static void DockSlots(Transform root, List<string> log)
        {
            var dock = Find(root, "Dock0926");
            if (dock == null) { log.Add("도크 없음"); return; }
            var defs = new (string button, string[] panels)[]
            {
                ("Message_Button", new[] { "MessagePanel", "ChatRoomPanel" }),
                ("List_Button", new[] { "ListPanel" }),
                ("PlusButton", new[] { "UploadPage" }),
                ("MiniProfile", new[] { "FullProfilePanel", "FollowPanel" }),   // v19: 프로필 칸도 열리면 X '닫기' 만 (B7: 팔로우 창도)
            };
            var swap = Ensure<R0926DockSlotSwap>(dock);
            var so = new SerializedObject(swap);
            var arr = so.FindProperty("slots");
            arr.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
            {
                var b = Find(root, defs[i].button);
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = b != null ? Ensure<CanvasGroup>(b) : null;
                var ps = e.FindPropertyRelative("panels");
                ps.arraySize = defs[i].panels.Length;
                for (int k = 0; k < defs[i].panels.Length; k++) ps.GetArrayElementAtIndex(k).objectReferenceValue = Find(root, defs[i].panels[k]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // v26: 메시지 목록의 도크 X 가 누르면 자기 자신도 끄고 있었다 — 목록이 OpenMessagePanel 말고 다른 길(대화방 ← 의 옆 넘기기)로
            // 다시 켜지면 X·뒤로가기(덮개가 X 안에 있다)가 없어 닫히지 않고 보물찾기 상세 위에 남았다 (10-13 대표님)
            NoSelfOff(FindP(root, "MessagePanel/DockMirror0926/CloseButton"));
            log.Add("dock slots ok");
        }

        // ── 창 위 손잡이 바 ─────────────────────────────────────
        private static void Grabs(Transform root, List<string> log)
        {
            int n = 0;
            foreach (var p in new[]
            {
                "MessagePanel/Clip0926/Background", "ChatRoomPanel/Clip0926/Background",
                "FollowPanel/Clip0926/Background", "AskAISheet0926/Card0926", "FullScreenPanel/ReportSheet0926/Card0926",
            })
            {
                var t = root.Find(p);
                if (t == null) { log.Add("손잡이: 없음 " + p); continue; }
                Grab(t.gameObject); n++;
            }
            // 추가 카드 — v19 부터 아래로 밀어 닫는다 (좌우 넘기기와는 같은 방향 잠금) → 손잡이 다시
            // 장소 '···'(MoreSheet0926)는 v30 부터 사진 고르기 시트처럼 손잡이 없이 큰 칸만
            foreach (var name in new[] { "CubeUploadPage", "ModelUploadPage" })   // 이름으로 (틀 안으로 옮겨져 경로가 바뀐다)
            {
                var g = Find(root, name);
                if (g != null) { Grab(g); n++; }
            }
            log.Add("grabs " + n);
        }

        private static void Grab(GameObject sheet)
        {
            var g = FindOrCreate(sheet.transform, "Grab0926");
            g.transform.SetAsLastSibling();
            Img(g, Spr("r0926_pill"), new Color(1, 1, 1, 0.22f), Image.Type.Sliced, 64f / 6f).raycastTarget = false;
            SetRect(RT(g), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(110, 12));
        }

        // ── 프로필 카드 ─────────────────────────────────────────
        private static void ProfileCard(Transform root, List<string> log)
        {
            var panel = Find(root, "FullProfilePanel");
            var content = panel != null ? panel.transform.Find("Content") : null;
            if (content == null) { log.Add("프로필 카드 없음"); return; }
            var c = content.gameObject;
            Img(c, Spr("r0926_pill"), CardBg, Image.Type.Sliced, 64f / 104f).raycastTarget = true;
            var crt = RT(c);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0, 90); crt.sizeDelta = new Vector2(1200, 1500);

            var top = content.Find("TopPanel"); var bottom = content.Find("BottomPanel");
            Transform T(string n) => Find(content, n)?.transform;
            var outline = T("AvatarOutline"); var mask = T("AvatarMask");
            var vis = T("VisibilityStatusText"); var uname = T("UsernameText"); var bio = T("BioText");
            var stats = T("FollowStats"); var follow = T("FollowButton"); var followed = T("FollowedButton");
            var sns = T("SnsIconsContainer"); var edit = T("EditProfileButton"); var logout = T("LogoutButton");
            var close = content.Find("CloseButton");

            // 닫기 X — 오른쪽 위 작은 원
            if (close != null)
            {
                SetRect(RT(close.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-52, -52), new Vector2(133, 133));
                Img(close.gameObject, Spr("r0926_circle"), new Color(1, 1, 1, 0.12f), Image.Type.Simple).raycastTarget = true;
                var ic = close.Find("Icon0926");
                if (ic != null) RT(ic.gameObject).sizeDelta = new Vector2(56, 56);
            }

            // 사진 — 90pt 사진 + 매끈한 테두리
            if (mask != null)
            {
                RT(mask.gameObject).sizeDelta = new Vector2(333, 333);
                // 자르는 원: 유니티 기본 Knob(32px, 안쪽 여백) → 우팡 원(256px, 가장자리까지 꽉).
                // 작은 원을 늘려 자르면 가장자리가 계단처럼 깨지고, 여백만큼 사진이 작게 잘려 분홍 테두리와 틈이 보였다
                var mImg = mask.GetComponent<Image>();
                if (mImg != null) { mImg.sprite = Spr("r0926_circle"); mImg.type = Image.Type.Simple; mImg.preserveAspect = false; EditorUtility.SetDirty(mImg); }
            }
            // 테두리 안쪽 가장자리(그림의 0.82)가 사진 가장자리를 6 덮게 — 자른 자리의 계단을 테두리가 가린다
            float ringSize = (333f / 2f - 6f) / 0.82f * 2f;
            if (outline != null) RT(outline.gameObject).sizeDelta = new Vector2(ringSize, ringSize);

            // 공개 상태 — 글자 색 그대로 옅은 알약 + 점
            Image pillImg = null, dotImg = null;
            Text visText = null;
            if (vis != null && top != null)
            {
                var pill = Find(content, "VisPill0926") ?? NewUI("VisPill0926", top);
                if (pill.transform.parent != top) pill.transform.SetParent(top, false);
                pillImg = Img(pill, Spr("r0926_pill"), new Color(Pink.r, Pink.g, Pink.b, 0.13f), Image.Type.Sliced, 64f / 48f);
                pillImg.raycastTarget = false;
                RT(pill).sizeDelta = new Vector2(500, 96);
                vis.SetParent(pill.transform, false);
                visText = vis.GetComponent<Text>();
                visText.fontSize = 46; visText.fontStyle = FontStyle.Bold; visText.alignment = TextAnchor.MiddleCenter;
                visText.horizontalOverflow = HorizontalWrapMode.Overflow; visText.verticalOverflow = VerticalWrapMode.Overflow;
                visText.resizeTextForBestFit = false;
                if (font != null) visText.font = font;
                EditorUtility.SetDirty(visText);
                var dot = FindOrCreate(pill.transform, "Dot0926");
                dotImg = Img(dot, Spr("r0926_circle"), Pink, Image.Type.Simple);
                dotImg.raycastTarget = false;
                RT(dot).sizeDelta = new Vector2(26, 26);
            }
            void Style(Transform t, int size, Color col, FontStyle st)
            {
                if (t == null) return;
                var tx = t.GetComponent<Text>();
                if (tx == null) return;
                tx.fontSize = size; tx.color = col; tx.fontStyle = st; tx.alignment = TextAnchor.MiddleCenter;
                tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.verticalOverflow = VerticalWrapMode.Overflow;
                tx.resizeTextForBestFit = false;
                if (font != null) tx.font = font;
                EditorUtility.SetDirty(tx);
            }
            Style(uname, 88, Ink, FontStyle.Bold);
            Style(bio, 50, Muted, FontStyle.Normal);

            // 팔로잉 · 팔로워 — 두 칸, 숫자 크게(런타임에 다시 칠한다)
            var countTexts = new List<Text>();
            if (stats != null)
            {
                var hl = stats.GetComponent<HorizontalLayoutGroup>();
                if (hl != null)
                {
                    hl.spacing = 190; hl.childAlignment = TextAnchor.MiddleCenter;
                    hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
                    EditorUtility.SetDirty(hl);
                }
                foreach (Transform b in stats)
                {
                    RT(b.gameObject).sizeDelta = new Vector2(330, 190);
                    foreach (var tx in b.GetComponentsInChildren<Text>(true))
                    {
                        tx.fontSize = 46; tx.color = Ink; tx.alignment = TextAnchor.MiddleCenter; tx.lineSpacing = 1.0f;
                        tx.supportRichText = true; tx.resizeTextForBestFit = false;
                        tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.verticalOverflow = VerticalWrapMode.Overflow;
                        if (font != null) tx.font = font;
                        var trt = tx.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
                        EditorUtility.SetDirty(tx);
                        countTexts.Add(tx);
                    }
                }
            }

            // '팔로우 중' 옆 '메세지 보내기' — 팔로우 중인 사람 프로필에서만 켠다 (ProfileManager) · 둘이 한 줄을 반씩 (10-21 대표님)
            Transform message = null;
            if (followed != null)
            {
                var mgo = FindOrCreate(followed.parent, "MessageButton0926");
                mgo.transform.SetSiblingIndex(followed.GetSiblingIndex() + 1);
                var lb = FindOrCreate(mgo.transform, "Text");
                Txt(lb, "메세지 보내기", 54, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                var lrt = RT(lb);
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                Loc(lb, "메세지 보내기", "Message", "メッセージ", "发消息", "Mensaje");
                PillButton(mgo, false);
                var mb = Ensure<Button>(mgo);
                var fb = followed.GetComponent<Button>();
                if (fb != null) { mb.transition = fb.transition; mb.colors = fb.colors; }
                mb.targetGraphic = mgo.GetComponent<Image>();
                EditorUtility.SetDirty(mb);
                mgo.SetActive(false);
                message = mgo.transform;
            }
            foreach (var b in new[] { followed, message, edit })
                if (b != null) Ensure<UITouchForwarder>(b.gameObject);   // 누름 소리 — '팔로우' 버튼과 같이

            // 버튼 — 카드 폭에 맞춘 알약
            const float BtnW = 1052f, BtnH = 170f;
            foreach (var b in new[] { follow, followed, message, edit })
            {
                if (b == null) continue;
                foreach (var tx in b.GetComponentsInChildren<Text>(true))
                {
                    tx.fontSize = 54; tx.fontStyle = FontStyle.Bold;
                    if (font != null) tx.font = font;
                    EditorUtility.SetDirty(tx);
                }
            }
            if (edit != null) Img(edit.gameObject, Spr("r0926_pill"), new Color(1, 1, 1, 0.12f), Image.Type.Sliced, 64f / (BtnH / 2f));
            if (logout != null)
                foreach (var tx in logout.GetComponentsInChildren<Text>(true))
                {
                    tx.fontSize = 50; tx.color = Muted; tx.fontStyle = FontStyle.Bold;
                    if (font != null) tx.font = font;
                    EditorUtility.SetDirty(tx);
                }
            if (sns != null)
            {
                var hl = sns.GetComponent<HorizontalLayoutGroup>();
                if (hl != null) { hl.spacing = 44; hl.childAlignment = TextAnchor.MiddleCenter; EditorUtility.SetDirty(hl); }
                foreach (Transform icon in sns)
                {
                    RT(icon.gameObject).sizeDelta = new Vector2(163, 163);
                    var le = icon.GetComponent<LayoutElement>();
                    if (le != null) { le.preferredWidth = 163; le.preferredHeight = 163; EditorUtility.SetDirty(le); }
                }
            }

            var lay = Ensure<R0926ProfileLayout>(c);
            var lso = new SerializedObject(lay);
            lso.FindProperty("card").objectReferenceValue = crt;
            SetArray(lso.FindProperty("stretchParents"), top as RectTransform, bottom as RectTransform);
            var items = new (Transform rt, GameObject activeIf, float h, float w, float gap, Transform companion, Transform pair)[]
            {
                (outline, null, ringSize, ringSize, 0, mask, null),
                (vis != null ? vis.parent : null, vis != null ? vis.gameObject : null, 96, 0, 44, null, null),
                (uname, null, 110, 1052, 34, null, null),
                (bio, null, 66, 1052, 6, null, null),
                (stats, null, 190, 900, 60, null, null),
                (follow, null, BtnH, BtnW, 60, null, null),
                (followed, null, BtnH, BtnW, 60, null, message),
                (sns, null, 163, 900, 60, null, null),
                (edit, null, BtnH, BtnW, 60, null, null),
                (logout, null, 90, 600, 36, null, null),
            };
            var arr = lso.FindProperty("items");
            arr.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("rt").objectReferenceValue = items[i].rt as RectTransform;
                e.FindPropertyRelative("activeIf").objectReferenceValue = items[i].activeIf;
                e.FindPropertyRelative("height").floatValue = items[i].h;
                e.FindPropertyRelative("width").floatValue = items[i].w;
                e.FindPropertyRelative("gapBefore").floatValue = items[i].gap;
                e.FindPropertyRelative("companion").objectReferenceValue = items[i].companion as RectTransform;
                e.FindPropertyRelative("pair").objectReferenceValue = items[i].pair as RectTransform;
            }
            lso.FindProperty("padTop").floatValue = 80f;
            lso.FindProperty("padBottom").floatValue = 70f;
            lso.FindProperty("minHeight").floatValue = 900f;
            lso.FindProperty("visText").objectReferenceValue = visText;
            lso.FindProperty("visPill").objectReferenceValue = pillImg;
            lso.FindProperty("visDot").objectReferenceValue = dotImg;
            SetArray(lso.FindProperty("countTexts"), countTexts.ToArray());
            lso.ApplyModifiedPropertiesWithoutUndo();

            var pm = Object.FindAnyObjectByType<ProfileManager>(FindObjectsInactive.Include);
            if (pm != null)
            {
                var pso = new SerializedObject(pm);
                pso.FindProperty("messageButton").objectReferenceValue = message != null ? message.GetComponent<Button>() : null;
                pso.FindProperty("listSheetClose").objectReferenceValue = Find(root, "XButton_List")?.GetComponent<Button>();
                pso.FindProperty("listSheetOpen").objectReferenceValue = Find(root, "List_Button")?.GetComponent<Button>();
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("profile card ok" + (message == null ? " (메세지 버튼 없음)" : ""));
        }

        // ── 입력줄 (채팅 · 키보드 위) ───────────────────────────
        private static void InputBars(Transform root, List<string> log)
        {
            var chatArea = root.Find("ChatRoomPanel/Clip0926/Background/InputArea") ?? root.Find("ChatRoomPanel/Background/InputArea");
            if (chatArea != null) InputBar(chatArea.gameObject, "ChatInput", "SendButton", false, log);
            else log.Add("채팅 입력줄 없음");
            // 키보드 위 입력줄 — 장소 추가(UploadPage)·장소 수정(Fixpage) 두 곳. 컴포넌트는 창에, 줄 자체는 그 창의 자식 'UploadInputMirror'
            int n = 0;
            foreach (var m in root.GetComponentsInChildren<UploadInputMirror>(true))
            {
                var bar = m.transform.Find("UploadInputMirror");
                if (bar == null) { log.Add("키보드 위 입력줄 없음: " + m.name); continue; }
                InputBar(bar.gameObject, "TextInput", "SendButton", true, log);
                WireMirror(m, bar, log);
                n++;
            }
            if (n == 0) log.Add("키보드 위 입력줄 없음");
        }

        // 예전 연결은 자리표시 글을 '장소 카드 이름칸'의 글로 잡고 있었다 → 입력줄을 열 때마다 그 칸 글을 덮어쓰고
        // 입력줄엔 '메시지 입력...'이 남았다. 장소 수정 쪽은 연결이 통째로 비어 있었다.
        private static void WireMirror(UploadInputMirror m, Transform bar, List<string> log)
        {
            var input = bar.Find("TextInput") != null ? bar.Find("TextInput").GetComponent<InputField>() : null;
            var send = bar.Find("SendButton") != null ? bar.Find("SendButton").GetComponent<Button>() : null;
            var so = new SerializedObject(m);
            so.FindProperty("mirrorPanel").objectReferenceValue = bar.gameObject;
            so.FindProperty("mirrorInput").objectReferenceValue = input;
            so.FindProperty("mirrorPlaceholder").objectReferenceValue = input != null ? input.placeholder as Text : null;
            so.FindProperty("closeButton").objectReferenceValue = send;
            // 보내기 칸(보물찾기 한마디)에선 ✓ 대신 보내기 화살표로 바꿔 그린다
            var icon = send != null ? send.transform.Find("Icon0926") : null;
            so.FindProperty("closeIcon").objectReferenceValue = icon != null ? icon.GetComponent<Image>() : null;
            so.FindProperty("sendSprite").objectReferenceValue = Spr("r0926_arrow_short");
            so.FindProperty("mirrorRect").objectReferenceValue = (RectTransform)bar;
            var src = so.FindProperty("sourceInputs");
            bool empty = src.arraySize == 0;
            for (int i = 0; i < src.arraySize && !empty; i++) empty = src.GetArrayElementAtIndex(i).objectReferenceValue == null;
            if (empty)
            {
                // 창 안의 입력칸 전부 (입력줄 자신은 빼고)
                var list = new List<InputField>();
                foreach (var f in m.GetComponentsInChildren<InputField>(true))
                    if (!f.transform.IsChildOf(bar)) list.Add(f);
                src.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++) src.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var rt = (RectTransform)bar;
            rt.sizeDelta = new Vector2(0, rt.sizeDelta.y);   // 예전엔 화면보다 88 넓어 보내기 버튼이 오른쪽 끝에서 잘렸다
            log.Add("mirror wired: " + m.name + " (sources " + src.arraySize + ")");
        }

        private static void InputBar(GameObject area, string inputName, string sendName, bool isMirror, List<string> log)
        {
            const float Pad = 40f, SendSize = 116f;
            // 바탕: 채팅은 창 안이라 투명(위에 가는 줄), 키보드 위 입력줄은 창 색으로 꽉
            var bg = area.GetComponent<Image>();
            if (bg != null) { bg.sprite = null; bg.color = isMirror ? CardBg : new Color(0, 0, 0, 0); EditorUtility.SetDirty(bg); }
            var line = FindOrCreate(area.transform, "Line0926");
            line.transform.SetSiblingIndex(0);
            Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
            // 채팅 입력줄은 카드보다 넓어(좌우 15) 줄이 카드 둥근 끝 밖으로 삐져나왔다 → 입력칸 양 끝에 맞춘다
            SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(isMirror ? 0 : -2 * Pad, 2));
            var art = RT(area);
            art.sizeDelta = new Vector2(art.sizeDelta.x, 150);

            var input = area.transform.Find(inputName);
            InputField field = null;
            if (input != null)
            {
                var irt = RT(input.gameObject);
                irt.anchorMin = new Vector2(0, 0.5f); irt.anchorMax = new Vector2(1, 0.5f); irt.pivot = new Vector2(0.5f, 0.5f);
                irt.offsetMin = new Vector2(Pad, -55); irt.offsetMax = new Vector2(-(Pad + SendSize + 24), 55);
                Img(input.gameObject, Spr("r0926_pill"), Glass7, Image.Type.Sliced, 64f / 55f).raycastTarget = true;
                field = input.GetComponent<InputField>();
                foreach (var t in input.GetComponentsInChildren<Text>(true))
                {
                    bool ph = field != null && field.placeholder == t;
                    t.fontSize = ph ? 44 : 46; t.color = ph ? Muted : Ink; t.alignment = TextAnchor.MiddleLeft;
                    t.fontStyle = FontStyle.Normal;
                    if (font != null) t.font = font;
                    var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                    trt.offsetMin = new Vector2(40, 0); trt.offsetMax = new Vector2(-40, 0);
                    EditorUtility.SetDirty(t);
                }
                foreach (var o in input.GetComponents<Outline>()) Object.DestroyImmediate(o);
            }
            var send = area.transform.Find(sendName);
            Image sendBg = null;
            if (send != null)
            {
                SetRect(RT(send.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-Pad, 0), new Vector2(SendSize, SendSize));
                sendBg = Img(send.gameObject, Spr("r0926_circle"), SendOff, Image.Type.Simple);
                sendBg.raycastTarget = true; sendBg.preserveAspect = true;
                var b = send.GetComponent<Button>();
                if (b != null) { b.transition = Selectable.Transition.None; EditorUtility.SetDirty(b); }
                var ic = FindOrCreate(send.transform, "Icon0926");
                var icImg = Img(ic, Spr(isMirror ? "r0926_check" : "r0926_arrow_short"), Color.white, Image.Type.Simple);
                icImg.raycastTarget = false; icImg.preserveAspect = true;
                SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, isMirror ? new Vector2(58, 58) : new Vector2(50, 62));
                RT(ic).localEulerAngles = isMirror ? Vector3.zero : new Vector3(0, 0, 180);   // 아래 화살표를 뒤집어 보내기(위)
                foreach (var t in send.GetComponentsInChildren<Text>(true)) t.enabled = false;
            }
            var bar = Ensure<R0926InputBar>(area);
            var so = new SerializedObject(bar);
            so.FindProperty("input").objectReferenceValue = field;
            so.FindProperty("sendBg").objectReferenceValue = sendBg;
            so.FindProperty("alwaysOn").boolValue = isMirror;   // 키보드 위 입력줄의 버튼은 '완료' — 늘 켜 둔다
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("input bar ok: " + area.name);
        }

        // ── 메시지 검색칸 ───────────────────────────────────────
        private static void MessageSearch(Transform root, List<string> log)
        {
            var area = root.Find("MessagePanel/Clip0926/Background/SearchArea");
            if (area == null) { log.Add("검색칸 없음"); return; }
            var aImg = area.GetComponent<Image>();
            if (aImg != null) { aImg.color = new Color(0, 0, 0, 0); EditorUtility.SetDirty(aImg); }
            var input = area.Find("SearchInput");
            if (input != null)
            {
                Img(input.gameObject, Spr("r0926_pill"), Glass7, Image.Type.Sliced, 64f / 50f).raycastTarget = true;
                foreach (var o in input.GetComponents<Outline>()) Object.DestroyImmediate(o);
                var field = input.GetComponent<InputField>();
                foreach (var t in input.GetComponentsInChildren<Text>(true))
                {
                    bool ph = field != null && field.placeholder == t;
                    t.fontSize = ph ? 42 : 44; t.color = ph ? Muted : Ink; t.fontStyle = FontStyle.Normal;
                    if (font != null) t.font = font;
                    EditorUtility.SetDirty(t);
                }
            }
            log.Add("message search ok");
        }

        // ── 메시지 창 제목 아래 작은 '(친구찾기)' ───────────────────────
        // 이 창의 검색칸에서 아이디로 친구를 찾아 팔로우한다. 도크 칸엔 넣지 않는다 — 창이 열렸을 때만 보이게 (10-19 대표님)
        // 제목을 18 올리고 그 아래에 작게 · 검색칸과 목록 윗선은 20 내린다 (값은 늘 같은 자리로 — 다시 적용해도 그대로)
        private const float MsgTitleY = 18f, MsgSubY = -110f, MsgSearchBottom = -380f, MsgListTop = -403.5f;

        private static void MessageTitleSub(Transform root, List<string> log)
        {
            var bg = root.Find("MessagePanel/Clip0926/Background");
            var header = bg != null ? bg.Find("Header") : null;
            var title = header != null ? header.Find("Title") : null;
            if (title == null) { log.Add("메시지 제목 없음"); return; }
            var trt = RT(title.gameObject);
            trt.anchoredPosition = new Vector2(trt.anchoredPosition.x, MsgTitleY);
            EditorUtility.SetDirty(trt);
            Loc(title.gameObject, "메시지", "Messages", "メッセージ", "消息", "Mensajes");

            var sub = FindOrCreate(header, "Sub0926");
            sub.transform.SetSiblingIndex(title.GetSiblingIndex() + 1);
            var t = Txt(sub, "(친구찾기)", MinFont, Soft, TextAnchor.MiddleCenter, FontStyle.Normal);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = false;
            SetRect(RT(sub), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, MsgSubY), new Vector2(600, 52));
            Loc(sub, "(친구찾기)", "(Find friends)", "(友だち検索)", "(找朋友)", "(Buscar amigos)");

            var area = bg.Find("SearchArea");
            if (area != null)
            {
                var art = RT(area.gameObject);
                art.anchoredPosition = new Vector2(art.anchoredPosition.x, MsgSearchBottom);
                EditorUtility.SetDirty(art);
            }
            var list = bg.Find("ConversationList");
            if (list != null)
            {
                var lrt = RT(list.gameObject);
                lrt.offsetMax = new Vector2(lrt.offsetMax.x, MsgListTop);
                EditorUtility.SetDirty(lrt);
            }
            log.Add("message title sub ok");
        }

        // ── 하늘 날씨 크기 ─────────────────────────────────────
        private static void WeatherSizes(Transform root, List<string> log)
        {
            var marker = Find(root, "Redesign0926Marker");
            var sky = marker != null ? marker.GetComponent<R0926SkyWeather>() : null;
            GameObject board = null;
            foreach (var r in root.gameObject.scene.GetRootGameObjects()) if (r.name == "SkyWeather0926") board = r;
            if (sky == null || board == null) { log.Add("날씨판 없음"); return; }
            var so = new SerializedObject(sky);
            so.FindProperty("boardFov").floatValue = 42f * 0.8f;   // 본판 80%
            so.ApplyModifiedPropertiesWithoutUndo();
            var fc = WeatherPage(board).Find("Forecast0926");
            if (fc != null)
            {
                // 예보 줄은 본판의 75% → 화면에서 예전의 60%. 본판 바로 아래에 담백하게
                var frt = RT(fc.gameObject);
                frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 1); frt.pivot = new Vector2(0.5f, 1);
                frt.anchoredPosition = new Vector2(0, -536);
                frt.localScale = Vector3.one * 0.75f;
                var fImg = fc.GetComponent<Image>();
                if (fImg != null) { fImg.color = new Color(0.094f, 0.118f, 0.149f, 0.22f); EditorUtility.SetDirty(fImg); }
            }
            RT(board).sizeDelta = new Vector2(1000, 760);
            log.Add("weather sizes ok");
        }
    }
}
