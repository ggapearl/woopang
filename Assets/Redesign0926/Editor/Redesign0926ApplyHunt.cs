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
            // 글자는 장소·3D모델 카드와 같은 크기로 · 카드 위아래 여백은 줄인다 (10-17 대표님)
            var hero = HButton(hub, "Hero", 206, HuntGold, "지금 이 자리에 보물 숨기기\n<size=40>보물은 지금 서 있는 자리에 · 처음엔 주최 수칙부터</size>",
                "Hide a treasure right here\n<size=40>Placed where you stand · host rules first</size>", 56, HuntDark, FontStyle.Bold, out _);
            S("heroButton", hero);
            S("rulesLink", HButton(hub, "RulesLink", 112, new Color(1, 1, 1, 0.07f), "주최 수칙 보기  ›", "Host rules  ›", 44, Soft, FontStyle.Bold, out _));
            var jr = HRow(hub, "JoinCodeRow", 132);
            var jci = HInput(jr, "JoinCodeInput", 132, "참가 코드 6자리로 참가", "Join with a 6-character code", false, 6);
            Ensure<LayoutElement>(jci.gameObject).flexibleWidth = 1;
            S("joinCodeInput", jci);
            var jcb = HButton(jr, "JoinCodeButton", 132, new Color(1, 1, 1, 0.12f), "참가", "Join", 46, Ink, FontStyle.Bold, out _);
            var jle = Ensure<LayoutElement>(jcb.gameObject); jle.preferredWidth = 220; jle.flexibleWidth = 0;
            S("joinCodeButton", jcb);
            S("nearHead", HLabel(hub, "NearHead", 80, "근처 보물찾기", null, 44, Muted, FontStyle.Bold));
            S("nearList", HList(hub, "NearList"));
            S("nearEmpty", HLabel(hub, "NearEmpty", 0, "근처에 열린 보물찾기가 없어요 — 첫 보물을 숨겨 보세요", null, 40, Muted, FontStyle.Normal));
            S("mineHead", HLabel(hub, "MineHead", 80, "내 게임", "My games", 44, Muted, FontStyle.Bold).gameObject);
            S("mineList", HList(hub, "MineList"));
            var login = HList(hub, "LoginBox");
            HLabel(login, "Text", 0, "로그인하면 보물을 숨기고 찾을 수 있어요", "Log in to hide and find treasures", 42, Soft, FontStyle.Normal);
            S("loginButton", HButton(login, "LoginButton", 132, Pink, "로그인", "Log in", 46, Ink, FontStyle.Bold, out _));
            S("loginBox", login.gameObject);
            S("offBox", HLabel(hub, "OffBox", 0, "설정에서 보물찾기 참여를 꺼 두었어요 — 목록 ▸ 설정에서 켤 수 있어요",
                "Treasure hunts are off — turn them on in List ▸ Settings", 40, HuntAmber, FontStyle.Normal).gameObject);

            // ── 숨기기 양식 ──
            var form = HuntScroll(body.transform, "Form", out var formView, out var formSr);
            S("form", formView); S("formScroll", formSr);
            S("formBack", HBack(form, "‹  돌아가기", "‹  Back"));
            S("gpsText", HLabel(form, "Gps", 0, "", null, 40, Muted, FontStyle.Bold));
            HLabel(form, "LTitle", 72, "제목", "Title", 44, Muted, FontStyle.Bold);
            var tIn = HInput(form, "TitleInput", 132, "예: 시청 앞 가을 보물", "e.g. Autumn treasure at City Hall", false, 40); S("titleInput", tIn);
            HLabel(form, "LHint", 72, "힌트 (선택)", "Hint (optional)", 44, Muted, FontStyle.Bold);
            var hIn = HInput(form, "HintInput", 220, "찾는 사람에게 줄 힌트", "A hint for the finders", true, 300); S("hintInput", hIn);
            HLabel(form, "LMode", 72, "찾는 방식", "How to win", 44, Muted, FontStyle.Bold);
            S("modeChips", HChips(form, "ModeChips"));
            var codeBox = HList(form, "CodeBox");
            HLabel(codeBox, "LCode", 68, "코드 — 보물 앞에서 넣을 답", "Code — the answer to enter at the treasure", 40, Muted, FontStyle.Bold);
            var cIn = HInput(codeBox, "CodeInput", 132, "예: 우팡123", "e.g. woopang123", false, 30); S("codeInput", cIn);
            HLabel(codeBox, "LQuestion", 68, "문제 (선택)", "Question (optional)", 40, Muted, FontStyle.Bold);
            var qIn = HInput(codeBox, "QuestionInput", 132, "예: 우리 앱 이름 + 123 은?", "e.g. Our app's name + 123?", false, 120); S("questionInput", qIn);
            S("codeBox", codeBox.gameObject);
            HLabel(form, "LWinners", 72, "당첨 인원", "Winners", 44, Muted, FontStyle.Bold);
            S("winnerChips", HChips(form, "WinnerChips"));
            var lPlayers = HLabel(form, "LPlayers", 72, "참가 인원 — 이만큼 차면 마감돼요", "Max players — closes when full", 44, Muted, FontStyle.Bold);
            var playerChips = HChips(form, "PlayerChips"); S("playerChips", playerChips);
            // 이미 있던 양식엔 맨 끝에 붙으므로 당첨 인원 바로 아래로 (2026-10-12 추가)
            int wi = form.Find("WinnerChips").GetSiblingIndex();
            lPlayers.transform.SetSiblingIndex(wi + 1);
            playerChips.SetSiblingIndex(wi + 2);
            HLabel(form, "LPrize", 72, "상품 (선택)", "Prize (optional)", 44, Muted, FontStyle.Bold);
            var pIn = HInput(form, "PrizeInput", 132, "예: 커피 쿠폰", "e.g. Coffee coupon", false, 80); S("prizeInput", pIn);
            HLabel(form, "LVisible", 72, "보이는 거리 — 이 안에 와야 보물이 보여요", "Visible within — get this close to see it", 44, Muted, FontStyle.Bold);
            S("visibleChips", HChips(form, "VisibleChips"));
            HLabel(form, "LRadius", 72, "참가 범위", "Who can join (distance)", 44, Muted, FontStyle.Bold);
            S("radiusChips", HChips(form, "RadiusChips"));
            HLabel(form, "LCoord", 72, "위치 공개", "Show location", 44, Muted, FontStyle.Bold);
            S("coordChips", HChips(form, "CoordChips"));
            HLabel(form, "LAudience", 72, "누구에게", "Open to", 44, Muted, FontStyle.Bold);
            S("audienceChips", HChips(form, "AudienceChips"));
            HLabel(form, "LStart", 72, "시작", "Starts", 44, Muted, FontStyle.Bold);
            S("startChips", HChips(form, "StartChips"));
            HLabel(form, "LDuration", 72, "진행 시간", "Duration", 44, Muted, FontStyle.Bold);
            S("durationChips", HChips(form, "DurationChips"));
            S("submitButton", HButton(form, "Submit", 156, HuntGold, "이 자리에 보물 숨기기", "Hide the treasure here", 52, HuntDark, FontStyle.Bold, out var submitLabel));
            S("submitLabel", submitLabel);
            S("formMsg", HLabel(form, "Msg", 0, "", null, 40, HuntAmber, FontStyle.Normal));

            // ── 주최 수칙 ──
            var rules = HuntScroll(body.transform, "Rules", out var rulesView, out var rulesSr);
            S("rules", rulesView); S("rulesScroll", rulesSr);
            S("rulesBack", HBack(rules, "‹  돌아가기", "‹  Back"));
            S("rulesTitle", HLabel(rules, "Title", 0, "보물을 숨기기 전에 꼭 읽어 주세요", null, 52, Ink, FontStyle.Bold));
            S("rulesList", HList(rules, "List"));
            var agree = FindOrCreate(rules, "Agree");
            Ensure<LayoutElement>(agree).preferredHeight = 128;
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
            S("agreeLabel", Txt(al, "위 내용을 확인했고 지킬게요", 44, Ink, TextAnchor.MiddleLeft, FontStyle.Bold));
            S("agreeRow", agree.GetComponent<Button>()); S("agreeBox", boxImg); S("agreeCheck", chk);
            var ab = HButton(rules, "AgreeButton", 150, new Color(1, 1, 1, 0.1f), "동의하고 보물 숨기기", "Agree and hide a treasure", 50, Muted, FontStyle.Bold, out _);
            S("agreeButton", ab); S("agreeButtonBg", ab.GetComponent<Image>());

            // ── 상세 ──
            var det = HuntScroll(body.transform, "Detail", out var detView, out var detSr);
            S("detail", detView); S("detailScroll", detSr);
            S("detailBack", HBack(det, "‹  보물찾기", "‹  Treasure hunt"));
            S("dTitle", HLabel(det, "Title", 0, "", null, 58, Ink, FontStyle.Bold));
            S("dStatus", HLabel(det, "Status", 0, "", null, 44, Muted, FontStyle.Bold));
            S("dHost", HButton(det, "Host", 116, new Color(1, 1, 1, 0.06f), "", null, 42, Soft, FontStyle.Normal, out var hostText));
            hostText.alignment = TextAnchor.MiddleLeft;
            RT(hostText.gameObject).offsetMin = new Vector2(36, 0); RT(hostText.gameObject).offsetMax = new Vector2(-36, 0);
            S("dHostText", hostText);
            S("dMine", HLabel(det, "Mine", 0, "", null, 42, HuntGold, FontStyle.Bold));
            S("dNear", HLabel(det, "Near", 0, "", null, 46, Ink, FontStyle.Bold));
            // '보물 열기' 버튼은 없앴다 — 보물(AR 금빛 상자)을 두 번 톡톡 눌러 연다 (10-14 대표님)
            var oldOpen = det.Find("Open");
            if (oldOpen != null) Object.DestroyImmediate(oldOpen.gameObject);
            var ccb = HList(det, "ClaimCodeBox");
            HLabel(ccb, "L", 68, "코드", "Code", 40, Muted, FontStyle.Bold);
            // 코드 칸 옆에 '열기' — 입력줄의 보내기(엔터)로도 바로 연다
            var codeRow = HRow(ccb, "CodeRow", 132);
            var oldCc = ccb.Find("ClaimCodeInput");
            if (oldCc != null) oldCc.SetParent(codeRow, false);
            var ccIn = HInput(codeRow, "ClaimCodeInput", 132, "보물에 걸린 코드를 넣어 주세요", "Enter the treasure's code", false, 30);
            Ensure<LayoutElement>(ccIn.gameObject).flexibleWidth = 1;
            var ccOk = HButton(codeRow, "CodeOk", 132, HuntGold, "열기", "Open", 46, HuntDark, FontStyle.Bold, out _);
            var ccle = Ensure<LayoutElement>(ccOk.gameObject); ccle.preferredWidth = 220; ccle.flexibleWidth = 0;
            S("claimCodeInput", ccIn); S("claimCodeButton", ccOk); S("claimCodeBox", ccb.gameObject);
            ccb.gameObject.SetActive(false);   // 근접 확인이 켠다
            S("dInfo", HLabel(det, "Info", 0, "", null, 42, Soft, FontStyle.Normal));
            S("joinButton", HButton(det, "Join", 144, HuntGold, "참가하기", "Join", 50, HuntDark, FontStyle.Bold, out _));
            S("profileButton", HButton(det, "Profile", 124, new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0.16f), "주최자 프로필에서 별점 남기기", "Rate on the host's profile", 44, HuntGold, FontStyle.Bold, out _));
            S("leaveButton", HButton(det, "Leave", 124, new Color(1, 1, 1, 0.08f), "나가기", "Leave", 44, Soft, FontStyle.Bold, out _));
            S("giveupButton", HButton(det, "GiveUp", 124, new Color(1, 1, 1, 0.08f), "못 찾았어요", "Couldn't find it", 44, Soft, FontStyle.Bold, out _));
            // 주최자 늘리기 — '1시간 연장하기 · 1일 연장하기' 한 줄, 누르면 남은 시간이 바로 바뀐다 (10-14 대표님)
            var exRow = HRow(det, "ExtendRow", 124);
            var oldEx = det.Find("Extend");
            if (oldEx != null) oldEx.SetParent(exRow, false);
            exRow.SetSiblingIndex(det.Find("GiveUp").GetSiblingIndex() + 1);
            var exH = HButton(exRow, "Extend", 124, new Color(1, 1, 1, 0.08f), "1시간 연장하기", "Extend 1 hour", 44, Soft, FontStyle.Bold, out _);
            var exD = HButton(exRow, "ExtendDay", 124, new Color(1, 1, 1, 0.08f), "1일 연장하기", "Extend 1 day", 44, Soft, FontStyle.Bold, out _);
            foreach (var eb in new[] { exH, exD }) { var le = Ensure<LayoutElement>(eb.gameObject); le.preferredWidth = 0; le.flexibleWidth = 1; }
            S("extendRow", exRow.gameObject); S("extendButton", exH); S("extendDayButton", exD);
            S("endButton", HButton(det, "End", 124, new Color(1f, 0.42f, 0.42f, 0.14f), "게임 끝내기", "End the game", 44, new Color(1f, 0.55f, 0.55f, 1f), FontStyle.Bold, out _));
            S("revealButton", HButton(det, "Reveal", 124, new Color(1, 1, 1, 0.08f), "보물 위치 공개", "Reveal the location", 44, Soft, FontStyle.Bold, out _));
            var mb = HList(det, "MsgBox");
            HLabel(mb, "L", 68, "참가자·주최자에게 한마디", "Message the players and host", 40, Muted, FontStyle.Bold);
            var mIn = HInput(mb, "MsgInput", 176, "예: 상품 받으러 갈게요!", "e.g. I'll come get the prize!", true, 300);
            S("msgInput", mIn);
            S("msgSend", HButton(mb, "Send", 124, Pink, "보내기", "Send", 46, Ink, FontStyle.Bold, out _));
            S("msgBox", mb.gameObject);
            S("feedHead", HLabel(det, "FeedHead", 80, "소식", "Updates", 44, Muted, FontStyle.Bold));
            S("feedList", HList(det, "Feed"));

            // ── 알림 (카드 아래쪽 알약) ──
            var toast = FindOrCreate(card.transform, "Toast0926");
            toast.transform.SetAsLastSibling();
            SetRect(RT(toast), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(-UP * 2, 132));
            Img(toast, Spr("r0926_pill"), new Color(0.05f, 0.06f, 0.07f, 0.96f), Image.Type.Sliced, 64f / 60f).raycastTarget = false;
            var tt = FindOrCreate(toast.transform, "Text");
            Stretch(RT(tt)); RT(tt).offsetMin = new Vector2(40, 0); RT(tt).offsetMax = new Vector2(-40, 0);
            var toastText = Txt(tt, "", 42, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            toastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            toast.SetActive(false);
            S("toast", toastText);
            var mirror = page.GetComponentInChildren<UploadInputMirror>(true);
            S("inputMirror", mirror);   // 한마디·코드 칸 — 입력줄의 보내기(엔터)로 바로 보낸다
            so.ApplyModifiedPropertiesWithoutUndo();

            hubView.SetActive(true); formView.SetActive(false); rulesView.SetActive(false); detView.SetActive(false);

            // 키보드 위 입력줄 — 이 카드의 입력칸도 같은 줄로 (입력줄은 칸 목록이 비었을 때만 스스로 모은다)
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

        // 보물찾기 글자의 일본어·중국어·스페인어 — 영어 글자로 찾는다 (10-21 — 그전엔 세 언어 모두 영어가 떴다)
        private static readonly Dictionary<string, string[]> HuntJaZhEs = new Dictionary<string, string[]>
        {
            ["Hide a treasure right here\n<size=40>Placed where you stand · host rules first</size>"] = new[] { "ここに宝を隠す\n<size=40>今立っている場所に · はじめに主催ルールを確認</size>", "在这里藏宝\n<size=40>就藏在你站的位置 · 先看主办规则</size>", "Esconde un tesoro aquí\n<size=40>Donde estás ahora · primero, las normas</size>" },
            ["Host rules  ›"] = new[] { "主催ルールを見る  ›", "查看主办规则  ›", "Normas para organizar  ›" },
            ["Join with a 6-character code"] = new[] { "6桁の参加コードで参加", "用 6 位参加代码参加", "Únete con un código de 6 caracteres" },
            ["Join"] = new[] { "参加", "参加", "Unirse" },
            ["My games"] = new[] { "自分のゲーム", "我的游戏", "Mis juegos" },
            ["Log in to hide and find treasures"] = new[] { "ログインすると宝を隠したり探したりできます", "登录后即可藏宝和寻宝", "Inicia sesión para esconder y buscar tesoros" },
            ["Log in"] = new[] { "ログイン", "登录", "Iniciar sesión" },
            ["Treasure hunts are off — turn them on in List ▸ Settings"] = new[] { "宝探しはオフになっています — 一覧 ▸ 設定でオンにできます", "寻宝已关闭 — 可在 列表 ▸ 设置 中开启", "Las búsquedas están desactivadas — actívalas en Lista ▸ Ajustes" },
            ["‹  Back"] = new[] { "‹  戻る", "‹  返回", "‹  Volver" },
            ["Title"] = new[] { "タイトル", "标题", "Título" },
            ["e.g. Autumn treasure at City Hall"] = new[] { "例：市役所前の秋の宝", "例：市政厅前的秋日宝藏", "p. ej. Tesoro de otoño en el ayuntamiento" },
            ["Hint (optional)"] = new[] { "ヒント（任意）", "提示（选填）", "Pista (opcional)" },
            ["A hint for the finders"] = new[] { "探す人へのヒント", "给寻宝者的提示", "Una pista para quienes buscan" },
            ["How to win"] = new[] { "見つけ方", "寻宝方式", "Cómo se gana" },
            ["Code — the answer to enter at the treasure"] = new[] { "コード — 宝の前で入力する答え", "代码 — 在宝藏前输入的答案", "Código — la respuesta que se introduce en el tesoro" },
            ["e.g. woopang123"] = new[] { "例：woopang123", "例：woopang123", "p. ej. woopang123" },
            ["Question (optional)"] = new[] { "問題（任意）", "问题（选填）", "Pregunta (opcional)" },
            ["e.g. Our app's name + 123?"] = new[] { "例：このアプリの名前 + 123 は？", "例：我们 App 的名字 + 123 是？", "p. ej. ¿El nombre de la app + 123?" },
            ["Winners"] = new[] { "当選人数", "中奖人数", "Ganadores" },
            ["Max players — closes when full"] = new[] { "参加人数 — 埋まると締め切り", "参加人数 — 满员即截止", "Máx. de jugadores — se cierra al llenarse" },
            ["Prize (optional)"] = new[] { "賞品（任意）", "奖品（选填）", "Premio (opcional)" },
            ["e.g. Coffee coupon"] = new[] { "例：コーヒーのクーポン", "例：咖啡券", "p. ej. Cupón de café" },
            ["Visible within — get this close to see it"] = new[] { "見える距離 — ここまで近づくと宝が見えます", "可见距离 — 走到这个范围内才能看到宝藏", "Visible a — acércate hasta aquí para verlo" },
            ["Who can join (distance)"] = new[] { "参加範囲（距離）", "参加范围（距离）", "Quién puede unirse (distancia)" },
            ["Show location"] = new[] { "位置の公開", "位置公开", "Mostrar ubicación" },
            ["Open to"] = new[] { "参加できる人", "参加对象", "Abierto a" },
            ["Starts"] = new[] { "開始", "开始", "Empieza" },
            ["Duration"] = new[] { "開催時間", "进行时间", "Duración" },
            ["Hide the treasure here"] = new[] { "ここに宝を隠す", "在这里藏宝", "Esconder el tesoro aquí" },
            ["Agree and hide a treasure"] = new[] { "同意して宝を隠す", "同意并藏宝", "Aceptar y esconder un tesoro" },
            ["‹  Treasure hunt"] = new[] { "‹  宝探し", "‹  寻宝", "‹  Búsqueda del tesoro" },
            ["Code"] = new[] { "コード", "代码", "Código" },
            ["Enter the treasure's code"] = new[] { "宝のコードを入力してください", "请输入宝藏的代码", "Introduce el código del tesoro" },
            ["Open"] = new[] { "開ける", "打开", "Abrir" },
            ["Rate on the host's profile"] = new[] { "主催者のプロフィールで評価する", "在主办者资料中评分", "Valora en el perfil del organizador" },
            ["Leave"] = new[] { "退出する", "退出", "Salir" },
            ["Couldn't find it"] = new[] { "見つけられなかった", "没找到", "No lo encontré" },
            ["Extend 1 hour"] = new[] { "1時間延長", "延长 1 小时", "Ampliar 1 hora" },
            ["Extend 1 day"] = new[] { "1日延長", "延长 1 天", "Ampliar 1 día" },
            ["End the game"] = new[] { "ゲームを終了", "结束游戏", "Terminar el juego" },
            ["Reveal the location"] = new[] { "宝の位置を公開", "公开宝藏位置", "Revelar la ubicación" },
            ["Message the players and host"] = new[] { "参加者・主催者にひとこと", "给参加者和主办者留言", "Mensaje a jugadores y organizador" },
            ["e.g. I'll come get the prize!"] = new[] { "例：賞品を受け取りに行きます！", "例：我会去领奖品！", "p. ej. ¡Voy a por el premio!" },
            ["Send"] = new[] { "送信", "发送", "Enviar" },
            ["Updates"] = new[] { "お知らせ", "动态", "Novedades" },
            ["Hosting record"] = new[] { "主催記録", "主办记录", "Historial como organizador" },
            ["Player ratings"] = new[] { "参加者の評価", "参加者评分", "Valoraciones de jugadores" },
            ["One-line review (optional)"] = new[] { "ひとことレビュー（任意）", "一句话评价（选填）", "Reseña breve (opcional)" },
            ["Only players who found it or got within 1km can rate · The host can't see who wrote it"] = new[] { "宝を見つけたか、ゲーム中に1km以内まで近づいた参加者だけが評価できます · 書いた人は主催者に表示されません", "只有找到宝藏或游戏中到过 1km 范围内的参加者才能评分 · 主办者看不到是谁写的", "Solo pueden valorar quienes lo encontraron o se acercaron a menos de 1 km · Quien organiza no ve quién lo escribió" },
            ["Player reviews"] = new[] { "参加者のレビュー", "参加者评价", "Reseñas de jugadores" },
            ["You can report only if you played this host's games · Blocking hides their hunts from your list and alerts"] = new[] { "この人のゲームに参加したことがある場合のみ通報できます · ブロックするとこの人のゲームが一覧・通知から消えます", "只有参加过此人的游戏才能举报 · 屏蔽后此人的游戏将从列表和通知中消失", "Solo puedes denunciar si jugaste a sus juegos · Al bloquear, sus búsquedas desaparecen de tu lista y avisos" },
            ["Cancel"] = new[] { "キャンセル", "取消", "Cancelar" },
        };

        private static void HLoc(GameObject g, string ko, string en)
        {
            if (!HuntJaZhEs.TryGetValue(en, out var t)) { Debug.LogWarning("[Redesign0926] 보물찾기 번역 없음: " + en); t = new[] { en, en, en }; }
            Loc(g, ko, en, t[0], t[1], t[2]);
        }

        private static Text HLabel(Transform parent, string name, float h, string ko, string en, float size, Color c, FontStyle st)
        {
            var g = FindOrCreate(parent, name);
            var le = Ensure<LayoutElement>(g);
            le.preferredHeight = h > 0 ? h : -1; le.minHeight = h > 0 ? h : 44;
            var t = Txt(g, ko, size, c, TextAnchor.MiddleLeft, st);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            if (en != null) HLoc(g, ko, en);
            return t;
        }

        // 돌아가기 — 왼쪽 위 알약 (10-14 대표님: '‹ 보물찾기' 접기 버튼이 작아 누르기 어렵다). 예전 'Back'(꽉 찬 투명 줄)은 이 줄 안으로 옮긴다
        private static Button HBack(Transform parent, string ko, string en)
        {
            var row = HRow(parent, "BackRow", 112);
            var old = parent.Find("Back");
            if (old != null) { row.SetSiblingIndex(old.GetSiblingIndex()); old.SetParent(row, false); }
            var b = HButton(row, "Back", 112, new Color(1, 1, 1, 0.08f), ko, en, 44, Ink, FontStyle.Bold, out _);
            var le = Ensure<LayoutElement>(b.gameObject); le.preferredWidth = 420; le.flexibleWidth = 0;
            return b;
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
            gl.cellSize = new Vector2(212, 108); gl.spacing = new Vector2(14, 14);
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
            if (en != null) HLoc(lg, ko, en);
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
            var t = Txt(tg, null, 46, Ink, a, FontStyle.Normal);
            t.supportRichText = false;
            t.horizontalOverflow = multi ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            var pg = FindOrCreate(g.transform, "Placeholder");
            Stretch(RT(pg)); RT(pg).offsetMin = new Vector2(36, 16); RT(pg).offsetMax = new Vector2(-36, -16);
            var p = Txt(pg, phKo, 42, Muted, a, FontStyle.Normal);
            p.horizontalOverflow = HorizontalWrapMode.Wrap;
            HLoc(pg, phKo, phEn);
            f.textComponent = t; f.placeholder = p;
            f.lineType = multi ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            f.characterLimit = limit;
            f.targetGraphic = img;
            f.transition = Selectable.Transition.None;
            EditorUtility.SetDirty(f);
            return f;
        }

        // 한 줄 글 — 길면 칸에 맞게 줄어든다 (넘쳐서 옆 글자와 겹치지 않게)
        private static Text OneLine(Text t, int min)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = t.fontSize;
            EditorUtility.SetDirty(t);
            return t;
        }

        private static GameObject HuntRowTemplate(Transform tpl)
        {
            var row = FindOrCreate(tpl, "Row");
            Ensure<LayoutElement>(row).preferredHeight = 168;
            var img = Img(row, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 46f);
            img.raycastTarget = true;
            var b = Ensure<Button>(row); b.transition = Selectable.Transition.ColorTint; b.targetGraphic = img;
            var dot = FindOrCreate(row.transform, "Dot");
            SetRect(RT(dot), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54, 0), new Vector2(30, 30));
            Img(dot, Spr("r0926_circle"), HuntGold, Image.Type.Simple).raycastTarget = false;
            var title = FindOrCreate(row.transform, "Title");
            SetRect(RT(title), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(96, 2), new Vector2(-96 - 250, 66));
            OneLine(Txt(title, "", 50, Ink, TextAnchor.LowerLeft, FontStyle.Bold), 36);
            var sub = FindOrCreate(row.transform, "Sub");
            SetRect(RT(sub), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(96, -6), new Vector2(-96 - 250, 54));
            OneLine(Txt(sub, "", 40, Soft, TextAnchor.UpperLeft, FontStyle.Normal), 30);
            var badge = FindOrCreate(row.transform, "Badge");
            SetRect(RT(badge), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(210, 80));
            Txt(badge, "", 42, HuntGold, TextAnchor.MiddleRight, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            return row;
        }

        private static GameObject HuntChipTemplate(Transform tpl)
        {
            var c = FindOrCreate(tpl, "Chip");
            var img = Img(c, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / 50f);
            img.raycastTarget = true;
            var b = Ensure<Button>(c); b.transition = Selectable.Transition.None; b.targetGraphic = img;
            var l = FindOrCreate(c.transform, "Label");
            SetRect(RT(l), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20, 60));
            OneLine(Txt(l, "", 40, Muted, TextAnchor.MiddleCenter, FontStyle.Normal), 26);
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
            Txt(num, "1", 34, HuntDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            var t = FindOrCreate(r.transform, "Title");
            Txt(t, "", 44, Ink, TextAnchor.MiddleLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Wrap;
            var body = FindOrCreate(r.transform, "Body");
            var bt = Txt(body, "", 38, Soft, TextAnchor.UpperLeft, FontStyle.Normal);
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
            Txt(t, "", 40, Soft, TextAnchor.UpperLeft, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Wrap;
            var tm = FindOrCreate(r.transform, "Time");
            Ensure<LayoutElement>(tm).preferredWidth = 210;
            Txt(tm, "", 32, Muted, TextAnchor.UpperRight, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Overflow;
            return r;
        }
    }
}
