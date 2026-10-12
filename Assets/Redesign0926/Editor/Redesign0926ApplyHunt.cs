using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 보물찾기 — '추가' 창 세 번째 카드 (2026-10-06 대표님: 주최는 장소 추가·3D모델 추가 옆에서. 메세지 안 탭은 찾기 어렵다).
    /// 틀(처음 화면 · 숨기기 양식 · 주최 수칙 · 상세 · 본보기 · 알림)만 만들고, 움직임은 R0926HuntCard.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static readonly Color HuntGold = new Color(0.910f, 0.749f, 0.310f, 1f);
        private static readonly Color HuntDark = new Color(0.102f, 0.078f, 0.020f, 1f);   // 금색 위 글
        private static readonly Color HuntAmber = new Color(1f, 0.702f, 0.278f, 1f);

        private static void ApplyHunt(Transform root, List<string> log)
        {
            var page = Find(root, "UploadPage");
            var wrapGo = page != null ? Find(page.transform, "UploadSheet0926") : null;
            if (wrapGo == null) { log.Add("보물찾기: 추가 창 틀 없음"); return; }
            var swipe = FindInScene<SwipePanelController>(page);

            var card = FindOrCreate(wrapGo.transform, "HuntUploadPage");
            var model = Find(wrapGo.transform, "ModelUploadPage");
            if (model != null) card.transform.SetSiblingIndex(model.transform.GetSiblingIndex() + 1);
            Img(card, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius).raycastTarget = true;
            var crt = RT(card);
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(-194f, 0);

            var title = FindOrCreate(card.transform, "TITLE");
            Txt(title, "보물찾기", 56, Ink, TextAnchor.MiddleLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            Loc(title, "보물찾기", "Treasure hunt", "宝探し", "寻宝", "Búsqueda del tesoro");
            Row(title, -44, 84);
            UploadSeg(card, swipe, 2);
            Grab(card);
            if (swipe != null)
            {
                var sso = new SerializedObject(swipe);
                sso.FindProperty("panel3").objectReferenceValue = crt;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }

            var body = FindOrCreate(card.transform, "Body0926");
            Stretch(RT(body));
            RT(body).offsetMin = new Vector2(0, 24); RT(body).offsetMax = new Vector2(0, -264);

            var hc = Ensure<R0926HuntCard>(card);
            var so = new SerializedObject(hc);
            void S(string prop, Object v) { var p = so.FindProperty(prop); if (p != null) p.objectReferenceValue = v; else log.Add("보물찾기: 칸 없음 " + prop); }

            // ── 본보기 (꺼 둔 채) ──
            var tpl = FindOrCreate(body.transform, "Templates0926");
            Stretch(RT(tpl));
            tpl.SetActive(false);
            S("rowTemplate", HuntRowTemplate(tpl.transform));
            S("chipTemplate", HuntChipTemplate(tpl.transform));
            S("ruleTemplate", HuntRuleTemplate(tpl.transform));
            S("feedTemplate", HuntFeedTemplate(tpl.transform));

            // ── 처음 화면 ──
            var hub = HuntScroll(body.transform, "Hub", out var hubView, out var hubSr);
            S("hub", hubView); S("hubScroll", hubSr);
            var hero = HButton(hub, "Hero", 240, HuntGold, "지금 이 자리에 보물 숨기기\n<size=34>보물은 지금 서 있는 자리에 · 처음엔 주최 수칙부터</size>",
                "Hide a treasure right here\n<size=34>Placed where you stand · host rules first</size>", 48, HuntDark, FontStyle.Bold, out _);
            S("heroButton", hero);
            S("rulesLink", HButton(hub, "RulesLink", 84, new Color(0, 0, 0, 0), "주최 수칙 보기  ›", "Host rules  ›", 34, Muted, FontStyle.Normal, out _));
            var jr = HRow(hub, "JoinCodeRow", 120);
            var jci = HInput(jr, "JoinCodeInput", 120, "참가 코드 6자리로 참가", "Join with a 6-character code", false, 6);
            Ensure<LayoutElement>(jci.gameObject).flexibleWidth = 1;
            S("joinCodeInput", jci);
            var jcb = HButton(jr, "JoinCodeButton", 120, new Color(1, 1, 1, 0.12f), "참가", "Join", 38, Ink, FontStyle.Bold, out _);
            var jle = Ensure<LayoutElement>(jcb.gameObject); jle.preferredWidth = 220; jle.flexibleWidth = 0;
            S("joinCodeButton", jcb);
            S("nearHead", HLabel(hub, "NearHead", 72, "근처 보물찾기", null, 36, Muted, FontStyle.Bold));
            S("nearList", HList(hub, "NearList"));
            S("nearEmpty", HLabel(hub, "NearEmpty", 0, "근처에 열린 보물찾기가 없어요 — 첫 보물을 숨겨 보세요", null, 34, Muted, FontStyle.Normal));
            S("mineHead", HLabel(hub, "MineHead", 72, "내 게임", "My games", 36, Muted, FontStyle.Bold).gameObject);
            S("mineList", HList(hub, "MineList"));
            var login = HList(hub, "LoginBox");
            HLabel(login, "Text", 0, "로그인하면 보물을 숨기고 찾을 수 있어요", "Log in to hide and find treasures", 36, Soft, FontStyle.Normal);
            S("loginButton", HButton(login, "LoginButton", 120, Pink, "로그인", "Log in", 40, Ink, FontStyle.Bold, out _));
            S("loginBox", login.gameObject);
            S("offBox", HLabel(hub, "OffBox", 0, "설정에서 보물찾기 참여를 꺼 두었어요 — 목록 ▸ 설정에서 켤 수 있어요",
                "Treasure hunts are off — turn them on in List ▸ Settings", 34, HuntAmber, FontStyle.Normal).gameObject);

            // ── 숨기기 양식 ──
            var form = HuntScroll(body.transform, "Form", out var formView, out var formSr);
            S("form", formView); S("formScroll", formSr);
            S("formBack", HButton(form, "Back", 90, new Color(0, 0, 0, 0), "‹  돌아가기", "‹  Back", 36, Muted, FontStyle.Normal, out _));
            S("gpsText", HLabel(form, "Gps", 0, "", null, 34, Muted, FontStyle.Bold));
            HLabel(form, "LTitle", 60, "제목", "Title", 36, Muted, FontStyle.Bold);
            var tIn = HInput(form, "TitleInput", 120, "예: 시청 앞 가을 보물", "e.g. Autumn treasure at City Hall", false, 40); S("titleInput", tIn);
            HLabel(form, "LHint", 60, "힌트 (선택)", "Hint (optional)", 36, Muted, FontStyle.Bold);
            var hIn = HInput(form, "HintInput", 220, "찾는 사람에게 줄 힌트", "A hint for the finders", true, 300); S("hintInput", hIn);
            HLabel(form, "LMode", 60, "찾는 방식", "How to win", 36, Muted, FontStyle.Bold);
            S("modeChips", HChips(form, "ModeChips"));
            var codeBox = HList(form, "CodeBox");
            HLabel(codeBox, "LCode", 60, "코드 — 보물 앞에서 넣을 답", "Code — the answer to enter at the treasure", 34, Muted, FontStyle.Bold);
            var cIn = HInput(codeBox, "CodeInput", 120, "예: 우팡123", "e.g. woopang123", false, 30); S("codeInput", cIn);
            HLabel(codeBox, "LQuestion", 60, "문제 (선택)", "Question (optional)", 34, Muted, FontStyle.Bold);
            var qIn = HInput(codeBox, "QuestionInput", 120, "예: 우리 앱 이름 + 123 은?", "e.g. Our app's name + 123?", false, 120); S("questionInput", qIn);
            S("codeBox", codeBox.gameObject);
            HLabel(form, "LWinners", 60, "당첨 인원", "Winners", 36, Muted, FontStyle.Bold);
            S("winnerChips", HChips(form, "WinnerChips"));
            var lPlayers = HLabel(form, "LPlayers", 60, "참가 인원 — 이만큼 차면 마감돼요", "Max players — closes when full", 36, Muted, FontStyle.Bold);
            var playerChips = HChips(form, "PlayerChips"); S("playerChips", playerChips);
            // 이미 있던 양식엔 맨 끝에 붙으므로 당첨 인원 바로 아래로 (2026-10-12 추가)
            int wi = form.Find("WinnerChips").GetSiblingIndex();
            lPlayers.transform.SetSiblingIndex(wi + 1);
            playerChips.SetSiblingIndex(wi + 2);
            HLabel(form, "LPrize", 60, "상품 (선택)", "Prize (optional)", 36, Muted, FontStyle.Bold);
            var pIn = HInput(form, "PrizeInput", 120, "예: 커피 쿠폰", "e.g. Coffee coupon", false, 80); S("prizeInput", pIn);
            HLabel(form, "LVisible", 60, "보이는 거리 — 이 안에 와야 보물이 보여요", "Visible within — get this close to see it", 36, Muted, FontStyle.Bold);
            S("visibleChips", HChips(form, "VisibleChips"));
            HLabel(form, "LRadius", 60, "참가 범위", "Who can join (distance)", 36, Muted, FontStyle.Bold);
            S("radiusChips", HChips(form, "RadiusChips"));
            HLabel(form, "LCoord", 60, "위치 공개", "Show location", 36, Muted, FontStyle.Bold);
            S("coordChips", HChips(form, "CoordChips"));
            HLabel(form, "LAudience", 60, "누구에게", "Open to", 36, Muted, FontStyle.Bold);
            S("audienceChips", HChips(form, "AudienceChips"));
            HLabel(form, "LStart", 60, "시작", "Starts", 36, Muted, FontStyle.Bold);
            S("startChips", HChips(form, "StartChips"));
            HLabel(form, "LDuration", 60, "진행 시간", "Duration", 36, Muted, FontStyle.Bold);
            S("durationChips", HChips(form, "DurationChips"));
            S("submitButton", HButton(form, "Submit", 150, HuntGold, "이 자리에 보물 숨기기", "Hide the treasure here", 46, HuntDark, FontStyle.Bold, out var submitLabel));
            S("submitLabel", submitLabel);
            S("formMsg", HLabel(form, "Msg", 0, "", null, 34, HuntAmber, FontStyle.Normal));

            // ── 주최 수칙 ──
            var rules = HuntScroll(body.transform, "Rules", out var rulesView, out var rulesSr);
            S("rules", rulesView); S("rulesScroll", rulesSr);
            S("rulesBack", HButton(rules, "Back", 90, new Color(0, 0, 0, 0), "‹  돌아가기", "‹  Back", 36, Muted, FontStyle.Normal, out _));
            S("rulesTitle", HLabel(rules, "Title", 0, "보물을 숨기기 전에 꼭 읽어 주세요", null, 46, Ink, FontStyle.Bold));
            S("rulesList", HList(rules, "List"));
            var agree = FindOrCreate(rules, "Agree");
            Ensure<LayoutElement>(agree).preferredHeight = 120;
            Img(agree, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            Ensure<Button>(agree).transition = Selectable.Transition.None;
            var box = FindOrCreate(agree.transform, "Box");
            SetRect(RT(box), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(64, 64));
            var boxImg = Img(box, Spr("r0926_pill"), new Color(1, 1, 1, 0.26f), Image.Type.Sliced, 64f / 18f);
            boxImg.raycastTarget = false;
            var chk = FindOrCreate(box.transform, "Check");
            SetRect(RT(chk), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            var chkImg = Img(chk, Spr("r0926_i_check"), HuntDark, Image.Type.Simple);
            chkImg.raycastTarget = false; chkImg.preserveAspect = true;
            chk.SetActive(false);
            var al = FindOrCreate(agree.transform, "Label");
            SetRect(RT(al), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(92, 0), new Vector2(-92, 0));
            S("agreeLabel", Txt(al, "위 내용을 확인했고 지킬게요", 38, Ink, TextAnchor.MiddleLeft, FontStyle.Bold));
            S("agreeRow", agree.GetComponent<Button>()); S("agreeBox", boxImg); S("agreeCheck", chk);
            var ab = HButton(rules, "AgreeButton", 140, new Color(1, 1, 1, 0.1f), "동의하고 보물 숨기기", "Agree and hide a treasure", 44, Muted, FontStyle.Bold, out _);
            S("agreeButton", ab); S("agreeButtonBg", ab.GetComponent<Image>());

            // ── 상세 ──
            var det = HuntScroll(body.transform, "Detail", out var detView, out var detSr);
            S("detail", detView); S("detailScroll", detSr);
            S("detailBack", HButton(det, "Back", 90, new Color(0, 0, 0, 0), "‹  보물찾기", "‹  Treasure hunt", 36, Muted, FontStyle.Normal, out _));
            S("dTitle", HLabel(det, "Title", 0, "", null, 52, Ink, FontStyle.Bold));
            S("dStatus", HLabel(det, "Status", 0, "", null, 36, Muted, FontStyle.Bold));
            S("dHost", HButton(det, "Host", 104, new Color(1, 1, 1, 0.06f), "", null, 36, Soft, FontStyle.Normal, out var hostText));
            hostText.alignment = TextAnchor.MiddleLeft;
            RT(hostText.gameObject).offsetMin = new Vector2(36, 0); RT(hostText.gameObject).offsetMax = new Vector2(-36, 0);
            S("dHostText", hostText);
            S("dMine", HLabel(det, "Mine", 0, "", null, 36, HuntGold, FontStyle.Bold));
            S("dNear", HLabel(det, "Near", 0, "", null, 40, Ink, FontStyle.Bold));
            S("openButton", HButton(det, "Open", 160, HuntGold, "보물 열기", "Open the treasure", 50, HuntDark, FontStyle.Bold, out var openLabel));
            S("openLabel", openLabel);
            var ccb = HList(det, "ClaimCodeBox");
            HLabel(ccb, "L", 60, "코드", "Code", 34, Muted, FontStyle.Bold);
            var ccIn = HInput(ccb, "ClaimCodeInput", 120, "보물에 걸린 코드를 넣어 주세요", "Enter the treasure's code", false, 30);
            S("claimCodeInput", ccIn); S("claimCodeBox", ccb.gameObject);
            Find(det, "Open").SetActive(false);   // 근접 확인이 켠다
            ccb.gameObject.SetActive(false);
            S("dInfo", HLabel(det, "Info", 0, "", null, 36, Soft, FontStyle.Normal));
            S("joinButton", HButton(det, "Join", 130, HuntGold, "참가하기", "Join", 44, HuntDark, FontStyle.Bold, out _));
            S("profileButton", HButton(det, "Profile", 110, new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0.16f), "주최자 프로필에서 별점 남기기", "Rate on the host's profile", 38, HuntGold, FontStyle.Bold, out _));
            S("leaveButton", HButton(det, "Leave", 110, new Color(1, 1, 1, 0.08f), "나가기", "Leave", 38, Soft, FontStyle.Bold, out _));
            S("giveupButton", HButton(det, "GiveUp", 110, new Color(1, 1, 1, 0.08f), "못 찾았어요", "Couldn't find it", 38, Soft, FontStyle.Bold, out _));
            S("extendButton", HButton(det, "Extend", 110, new Color(1, 1, 1, 0.08f), "30분 늘리기", "Extend 30 min", 38, Soft, FontStyle.Bold, out _));
            S("endButton", HButton(det, "End", 110, new Color(1f, 0.42f, 0.42f, 0.14f), "게임 끝내기", "End the game", 38, new Color(1f, 0.55f, 0.55f, 1f), FontStyle.Bold, out _));
            S("revealButton", HButton(det, "Reveal", 110, new Color(1, 1, 1, 0.08f), "보물 위치 공개", "Reveal the location", 38, Soft, FontStyle.Bold, out _));
            var mb = HList(det, "MsgBox");
            HLabel(mb, "L", 60, "참가자·주최자에게 한마디", "Message the players and host", 34, Muted, FontStyle.Bold);
            var mIn = HInput(mb, "MsgInput", 160, "예: 상품 받으러 갈게요!", "e.g. I'll come get the prize!", true, 300);
            S("msgInput", mIn);
            S("msgSend", HButton(mb, "Send", 110, Pink, "보내기", "Send", 40, Ink, FontStyle.Bold, out _));
            S("msgBox", mb.gameObject);
            S("feedHead", HLabel(det, "FeedHead", 72, "소식", "Updates", 36, Muted, FontStyle.Bold));
            S("feedList", HList(det, "Feed"));

            // ── 알림 (카드 아래쪽 알약) ──
            var toast = FindOrCreate(card.transform, "Toast0926");
            toast.transform.SetAsLastSibling();
            SetRect(RT(toast), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(-UP * 2, 120));
            Img(toast, Spr("r0926_pill"), new Color(0.05f, 0.06f, 0.07f, 0.96f), Image.Type.Sliced, 64f / 60f).raycastTarget = false;
            var tt = FindOrCreate(toast.transform, "Text");
            Stretch(RT(tt)); RT(tt).offsetMin = new Vector2(40, 0); RT(tt).offsetMax = new Vector2(-40, 0);
            var toastText = Txt(tt, "", 36, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            toastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            toast.SetActive(false);
            S("toast", toastText);
            so.ApplyModifiedPropertiesWithoutUndo();

            hubView.SetActive(true); formView.SetActive(false); rulesView.SetActive(false); detView.SetActive(false);

            // 키보드 위 입력줄 — 이 카드의 입력칸도 같은 줄로 (입력줄은 칸 목록이 비었을 때만 스스로 모은다)
            var mirror = page.GetComponentInChildren<UploadInputMirror>(true);
            if (mirror != null)
            {
                var mso = new SerializedObject(mirror);
                var src = mso.FindProperty("sourceInputs");
                var have = new HashSet<Object>();
                for (int i = 0; i < src.arraySize; i++) have.Add(src.GetArrayElementAtIndex(i).objectReferenceValue);
                foreach (var f in new[] { tIn, hIn, cIn, qIn, pIn, jci, ccIn, mIn })
                {
                    if (have.Contains(f)) continue;
                    src.arraySize++;
                    src.GetArrayElementAtIndex(src.arraySize - 1).objectReferenceValue = f;
                }
                mso.ApplyModifiedPropertiesWithoutUndo();
            }
            else log.Add("보물찾기: 키보드 입력줄 없음");

            // 보물찾기 알림을 누르면 이 카드의 그 게임으로 — 캔버스에 두어 추가 창이 닫혀 있어도 받는다
            var push = Ensure<R0926HuntPush>(root.gameObject);
            var pso = new SerializedObject(push);
            var plus = Find(root, "PlusButton");
            if (plus == null) log.Add("보물찾기 알림: 추가 버튼 없음");
            pso.FindProperty("plusButton").objectReferenceValue = plus != null ? plus.GetComponent<Button>() : null;
            pso.FindProperty("uploadPage").objectReferenceValue = page;
            pso.FindProperty("swipe").objectReferenceValue = swipe;
            pso.FindProperty("card").objectReferenceValue = hc;
            pso.ApplyModifiedPropertiesWithoutUndo();
            log.Add("hunt card ok");
        }

        // ── 부품 ────────────────────────────────────────────
        private static Transform HuntScroll(Transform parent, string name, out GameObject view, out ScrollRect sr)
        {
            view = FindOrCreate(parent, name);
            Stretch(RT(view));
            sr = Ensure<ScrollRect>(view);
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 30;
            var vp = FindOrCreate(view.transform, "Viewport");
            Stretch(RT(vp));
            Ensure<RectMask2D>(vp);
            Img(vp, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            var content = FindOrCreate(vp.transform, "Content");
            SetRect(RT(content), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            var vl = Ensure<VerticalLayoutGroup>(content);
            vl.padding = new RectOffset((int)UP, (int)UP, 8, 200); vl.spacing = 22;
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            Ensure<ContentSizeFitter>(content).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = RT(vp); sr.content = RT(content);
            return content.transform;
        }

        private static Text HLabel(Transform parent, string name, float h, string ko, string en, float size, Color c, FontStyle st)
        {
            var g = FindOrCreate(parent, name);
            var le = Ensure<LayoutElement>(g);
            le.preferredHeight = h > 0 ? h : -1; le.minHeight = h > 0 ? h : 44;
            var t = Txt(g, ko, size, c, TextAnchor.MiddleLeft, st);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            if (en != null) Loc(g, ko, en, en, en, en);
            return t;
        }

        private static RectTransform HList(Transform parent, string name)
        {
            var g = FindOrCreate(parent, name);
            var vl = Ensure<VerticalLayoutGroup>(g);
            vl.padding = new RectOffset(0, 0, 0, 0); vl.spacing = 14;
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            return RT(g);
        }

        private static Transform HRow(Transform parent, string name, float h)
        {
            var g = FindOrCreate(parent, name);
            Ensure<LayoutElement>(g).preferredHeight = h;
            var hl = Ensure<HorizontalLayoutGroup>(g);
            hl.spacing = 16; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            return g.transform;
        }

        private static RectTransform HChips(Transform parent, string name)
        {
            var g = FindOrCreate(parent, name);
            var gl = Ensure<GridLayoutGroup>(g);
            gl.cellSize = new Vector2(212, 100); gl.spacing = new Vector2(14, 14);
            gl.startAxis = GridLayoutGroup.Axis.Horizontal; gl.childAlignment = TextAnchor.UpperLeft;
            gl.constraint = GridLayoutGroup.Constraint.Flexible;
            return RT(g);
        }

        private static Button HButton(Transform parent, string name, float h, Color bg, string ko, string en, float size, Color fg, FontStyle st, out Text label)
        {
            var g = FindOrCreate(parent, name);
            Ensure<LayoutElement>(g).preferredHeight = h;
            var img = Img(g, Spr("r0926_pill"), bg, Image.Type.Sliced, 64f / Mathf.Min(70f, h / 2f));
            img.raycastTarget = true;
            var b = Ensure<Button>(g);
            b.transition = Selectable.Transition.ColorTint; b.targetGraphic = img;
            var lg = FindOrCreate(g.transform, "Label");
            Stretch(RT(lg));
            label = Txt(lg, ko, size, fg, TextAnchor.MiddleCenter, st);
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.supportRichText = true; label.lineSpacing = 1.05f;
            if (en != null) Loc(lg, ko, en, en, en, en);
            return b;
        }

        private static InputField HInput(Transform parent, string name, float h, string phKo, string phEn, bool multi, int limit)
        {
            var g = FindOrCreate(parent, name);
            Ensure<LayoutElement>(g).preferredHeight = h;
            var img = Img(g, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / Mathf.Min(48f, h / 2f));
            img.raycastTarget = true;
            var f = Ensure<InputField>(g);
            TextAnchor a = multi ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            var tg = FindOrCreate(g.transform, "Text");
            Stretch(RT(tg)); RT(tg).offsetMin = new Vector2(36, 16); RT(tg).offsetMax = new Vector2(-36, -16);
            var t = Txt(tg, null, 40, Ink, a, FontStyle.Normal);
            t.supportRichText = false;
            t.horizontalOverflow = multi ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            var pg = FindOrCreate(g.transform, "Placeholder");
            Stretch(RT(pg)); RT(pg).offsetMin = new Vector2(36, 16); RT(pg).offsetMax = new Vector2(-36, -16);
            var p = Txt(pg, phKo, 40, Muted, a, FontStyle.Normal);
            p.horizontalOverflow = HorizontalWrapMode.Wrap;
            Loc(pg, phKo, phEn, phEn, phEn, phEn);
            f.textComponent = t; f.placeholder = p;
            f.lineType = multi ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            f.characterLimit = limit;
            f.targetGraphic = img;
            f.transition = Selectable.Transition.None;
            EditorUtility.SetDirty(f);
            return f;
        }

        private static GameObject HuntRowTemplate(Transform tpl)
        {
            var row = FindOrCreate(tpl, "Row");
            Ensure<LayoutElement>(row).preferredHeight = 176;
            var img = Img(row, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 46f);
            img.raycastTarget = true;
            var b = Ensure<Button>(row); b.transition = Selectable.Transition.ColorTint; b.targetGraphic = img;
            var dot = FindOrCreate(row.transform, "Dot");
            SetRect(RT(dot), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54, 0), new Vector2(26, 26));
            Img(dot, Spr("r0926_circle"), HuntGold, Image.Type.Simple).raycastTarget = false;
            var title = FindOrCreate(row.transform, "Title");
            SetRect(RT(title), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(96, 4), new Vector2(-96 - 230, 60));
            Txt(title, "", 42, Ink, TextAnchor.LowerLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            var sub = FindOrCreate(row.transform, "Sub");
            SetRect(RT(sub), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(96, -4), new Vector2(-96 - 230, 52));
            Txt(sub, "", 31, Muted, TextAnchor.UpperLeft, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Overflow;
            var badge = FindOrCreate(row.transform, "Badge");
            SetRect(RT(badge), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(200, 80));
            Txt(badge, "", 34, HuntGold, TextAnchor.MiddleRight, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            return row;
        }

        private static GameObject HuntChipTemplate(Transform tpl)
        {
            var c = FindOrCreate(tpl, "Chip");
            var img = Img(c, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / 50f);
            img.raycastTarget = true;
            var b = Ensure<Button>(c); b.transition = Selectable.Transition.None; b.targetGraphic = img;
            var l = FindOrCreate(c.transform, "Label");
            Stretch(RT(l));
            Txt(l, "", 34, Muted, TextAnchor.MiddleCenter, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Overflow;
            return c;
        }

        private static GameObject HuntRuleTemplate(Transform tpl)
        {
            var r = FindOrCreate(tpl, "Rule");
            Img(r, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 40f).raycastTarget = false;
            var vl = Ensure<VerticalLayoutGroup>(r);
            vl.padding = new RectOffset(112, 32, 22, 22); vl.spacing = 6;
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            var icon = FindOrCreate(r.transform, "Icon");
            Ensure<LayoutElement>(icon).ignoreLayout = true;
            SetRect(RT(icon), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -24), new Vector2(60, 60));
            Img(icon, Spr("r0926_circle"), HuntGold, Image.Type.Simple).raycastTarget = false;
            var num = FindOrCreate(icon.transform, "Num");
            Stretch(RT(num));
            Txt(num, "1", 32, HuntDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            var t = FindOrCreate(r.transform, "Title");
            Txt(t, "", 38, Ink, TextAnchor.MiddleLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Wrap;
            var body = FindOrCreate(r.transform, "Body");
            var bt = Txt(body, "", 32, Soft, TextAnchor.UpperLeft, FontStyle.Normal);
            bt.horizontalOverflow = HorizontalWrapMode.Wrap; bt.lineSpacing = 1.1f;
            return r;
        }

        private static GameObject HuntFeedTemplate(Transform tpl)
        {
            var r = FindOrCreate(tpl, "Feed");
            var le = Ensure<LayoutElement>(r); le.minHeight = 64; le.preferredHeight = -1;
            var hl = Ensure<HorizontalLayoutGroup>(r);
            hl.spacing = 20; hl.childAlignment = TextAnchor.UpperLeft;
            hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var t = FindOrCreate(r.transform, "Text");
            Ensure<LayoutElement>(t).flexibleWidth = 1;
            Txt(t, "", 33, Soft, TextAnchor.UpperLeft, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Wrap;
            var tm = FindOrCreate(r.transform, "Time");
            Ensure<LayoutElement>(tm).preferredWidth = 170;
            Txt(tm, "", 26, Muted, TextAnchor.UpperRight, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Overflow;
            return r;
        }
    }
}
