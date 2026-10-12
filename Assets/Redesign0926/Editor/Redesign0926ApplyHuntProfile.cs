using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 보물찾기 — 프로필 카드의 '주최' 칸과 펼침 (2026-10-05 대표님 · 시안 우팡_보물찾기_프로필_1005).
    /// 숫자 줄 끝에 '주최' 칸, 버튼 아래(로그아웃 위)에 펼침 칸, 리뷰 한 줄은 키보드 위 입력줄로.
    /// 움직임은 R0926HuntProfile, 높이·화면 맞추기는 R0926ProfileLayout.
    /// ProfileCard(Apply10)가 배치 항목과 숫자 칸 모양을 매번 새로 깔므로 그 뒤(ApplyHunt 다음)에 돈다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static readonly Color HuntTeal = new Color(0.247f, 0.757f, 0.690f, 1f);
        private static readonly Color HuntRed = new Color(1f, 0.42f, 0.42f, 1f);
        private static readonly Color HuntRedText = new Color(1f, 0.58f, 0.58f, 1f);
        private static readonly Color HuntBox = new Color(1f, 1f, 1f, 0.045f);
        private const float HuntSectionW = 1052f;

        private static void ApplyHuntProfile(Transform root, List<string> log)
        {
            var panel = Find(root, "FullProfilePanel");
            var content = panel != null ? panel.transform.Find("Content") : null;
            var bottom = content != null ? content.Find("BottomPanel") : null;
            var statsGo = content != null ? Find(content, "FollowStats") : null;
            var lay = content != null ? content.GetComponent<R0926ProfileLayout>() : null;
            if (bottom == null || statsGo == null || lay == null) { log.Add("보물찾기 프로필: 프로필 카드 없음"); return; }
            var stats = statsGo.transform;
            var center = new Vector2(0.5f, 0.5f);

            // ── 숫자 줄 끝 '주최' 칸 — 주최한 적이 있는 사람만 (R0926HuntProfile 이 켠다) ──
            var col = FindOrCreate(stats, "HuntStat0926");
            col.transform.SetAsLastSibling();
            RT(col).sizeDelta = new Vector2(330, 250);
            var hit = Img(col, null, new Color(0, 0, 0, 0), Image.Type.Simple);
            hit.raycastTarget = true;
            var colBtn = Ensure<Button>(col);
            colBtn.transition = Selectable.Transition.None; colBtn.targetGraphic = hit;
            var box = FindOrCreate(col.transform, "Box");
            box.transform.SetAsFirstSibling();
            SetRect(RT(box), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 12), new Vector2(300, 268));
            var boxImg = Img(box, Spr("r0926_pill"), new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0.12f), Image.Type.Sliced, 64f / 44f);
            boxImg.raycastTarget = false;
            var main = FindOrCreate(col.transform, "Main");
            SetRect(RT(main), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 190));
            var mainT = Txt(main, "", 46, Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
            mainT.supportRichText = true; mainT.lineSpacing = 1f; mainT.resizeTextForBestFit = false;
            mainT.horizontalOverflow = HorizontalWrapMode.Overflow; mainT.verticalOverflow = VerticalWrapMode.Overflow;
            var lab = FindOrCreate(col.transform, "Label");
            SetRect(RT(lab), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -176), new Vector2(0, 64));
            var labT = Txt(lab, "주최", 46, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            labT.horizontalOverflow = HorizontalWrapMode.Overflow; labT.verticalOverflow = VerticalWrapMode.Overflow; labT.resizeTextForBestFit = false;
            col.SetActive(false);
            var shl = stats.GetComponent<HorizontalLayoutGroup>();
            if (shl != null) { shl.padding = new RectOffset(0, 0, 0, 0); EditorUtility.SetDirty(shl); }

            // ── 펼침 칸 (버튼 아래 · 로그아웃 위) — 길면 카드 배치가 줄이고 그 안을 스크롤 ──
            var sec = FindOrCreate(bottom, "HuntSection0926");
            var logout = bottom.Find("LogoutButton");
            if (logout != null) sec.transform.SetSiblingIndex(logout.GetSiblingIndex());
            var srt = RT(sec);
            SetRect(srt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1400), new Vector2(HuntSectionW, 0));
            var scroll = HuntScroll(sec.transform, "Scroll", out var view, out var sr);
            RT(view).offsetMax = new Vector2(0, -2);
            var svl = scroll.GetComponent<VerticalLayoutGroup>();
            svl.padding = new RectOffset(0, 0, 34, 30); svl.spacing = 24;
            EditorUtility.SetDirty(svl);
            var line = FindOrCreate(sec.transform, "Line0926");
            line.transform.SetAsLastSibling();
            SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));
            Img(line, null, new Color(1, 1, 1, 0.08f), Image.Type.Simple).raycastTarget = false;

            var tpl = FindOrCreate(sec.transform, "Templates0926");
            Stretch(RT(tpl));
            tpl.SetActive(false);
            var openTpl = HuntOpenRowTemplate(tpl.transform);
            var chipTpl = HuntChipTemplate(tpl.transform);
            var reviewTpl = HuntReviewRowTemplate(tpl.transform);
            var reasonTpl = HuntReasonTemplate(tpl.transform);

            // 머리 — 금색 열쇠 + 보물찾기 · 오른쪽 작은 글
            var head = FindOrCreate(scroll, "Head");
            var hle = Ensure<LayoutElement>(head); hle.preferredHeight = 92; hle.minHeight = 92;
            var icon = FindOrCreate(head.transform, "Icon");
            SetRect(RT(icon), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(80, 80));
            Img(icon, Spr("r0926_pill"), HuntGold, Image.Type.Sliced, 64f / 22f).raycastTarget = false;
            var glyph = FindOrCreate(icon.transform, "Glyph");
            SetRect(RT(glyph), center, center, center, Vector2.zero, new Vector2(54, 54));
            var gi = Img(glyph, Spr("r0926_i_key"), HuntDark, Image.Type.Simple);
            gi.raycastTarget = false; gi.preserveAspect = true;
            var ht = FindOrCreate(head.transform, "Title");
            HuntFill(ht, 106, 0, 300, 0);
            Txt(ht, "보물찾기", 54, Ink, TextAnchor.MiddleLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            Loc(ht, "보물찾기", "Treasure hunts", "宝探し", "寻宝", "Búsqueda del tesoro");
            var hr = FindOrCreate(head.transform, "Right");
            HuntFill(hr, 420, 0, 0, 0);
            var headRight = Txt(hr, "", 36, Muted, TextAnchor.MiddleRight, FontStyle.Normal);
            headRight.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 주최 정지 — 본인에게만 보인다
            var sus = HuntBoxV(scroll, "SuspendBox", new Color(HuntRed.r, HuntRed.g, HuntRed.b, 0.10f), 30, 0);
            var susText = HLabel(sus, "Text", 0, "", null, 36, HuntRedText, FontStyle.Normal);

            // 주최 기록 — 네 숫자 + 처음 주최·횟수
            var rec = HuntBoxV(scroll, "RecordBox", HuntBox, 30, 10);
            HLabel(rec, "L", 50, "주최 기록", "Hosting record", 34, Muted, FontStyle.Bold);
            var nums = FindOrCreate(rec, "Nums");
            var nle = Ensure<LayoutElement>(nums); nle.preferredHeight = 128; nle.minHeight = 128;
            var nhl = Ensure<HorizontalLayoutGroup>(nums);
            nhl.spacing = 0; nhl.childAlignment = TextAnchor.MiddleCenter;
            nhl.childControlWidth = true; nhl.childControlHeight = true; nhl.childForceExpandWidth = true; nhl.childForceExpandHeight = true;
            var recordNums = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                var n = FindOrCreate(nums.transform, "N" + i);
                var le = Ensure<LayoutElement>(n); le.preferredWidth = 1; le.flexibleWidth = 1;   // 네 칸 같은 폭
                var t = Txt(n, "", 60, Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
                t.supportRichText = true; t.lineSpacing = 1f;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                recordNums[i] = t;
            }
            var foot = FindOrCreate(rec, "Foot");
            var fle = Ensure<LayoutElement>(foot); fle.preferredHeight = 56; fle.minHeight = 56;
            var fl = FindOrCreate(foot.transform, "Left");
            HuntFill(fl, 0, 0, 0, 0);
            var recordFoot = Txt(fl, "", 34, Soft, TextAnchor.MiddleLeft, FontStyle.Bold);
            recordFoot.horizontalOverflow = HorizontalWrapMode.Overflow;
            var fr = FindOrCreate(foot.transform, "Right");
            HuntFill(fr, 0, 0, 0, 0);
            Txt(fr, "참가자가 있던 게임만 셈", 30, Muted, TextAnchor.MiddleRight, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Overflow;
            Loc(fr, "참가자가 있던 게임만 셈", "Only games with players", "参加者がいたゲームのみ", "仅计有参与者的游戏", "Solo juegos con jugadores");

            // 지금 열린 게임
            var openList = HList(scroll, "OpenList");

            // 참여자 평가 — 큰 숫자 + 별 + 몇 명
            var rate = HuntBoxV(scroll, "RateBox", HuntBox, 30, 4);
            HLabel(rate, "L", 50, "참여자 평가", "Player ratings", 34, Muted, FontStyle.Bold);
            var rr = FindOrCreate(rate, "Row");
            var rle = Ensure<LayoutElement>(rr); rle.preferredHeight = 150; rle.minHeight = 150;
            var rn = FindOrCreate(rr.transform, "Num");
            SetRect(RT(rn), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(250, 150));
            var rateNum = Txt(rn, "", 120, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            rateNum.horizontalOverflow = HorizontalWrapMode.Overflow; rateNum.verticalOverflow = VerticalWrapMode.Overflow;
            var rs = FindOrCreate(rr.transform, "Stars");
            SetRect(RT(rs), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(250, -4), new Vector2(-250 - 300, 84));
            var rateStars = Txt(rs, "", 64, HuntGold, TextAnchor.LowerLeft, FontStyle.Normal);
            rateStars.supportRichText = true; rateStars.horizontalOverflow = HorizontalWrapMode.Overflow; rateStars.verticalOverflow = VerticalWrapMode.Overflow;
            var rc = FindOrCreate(rr.transform, "Count");
            SetRect(RT(rc), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(254, -8), new Vector2(-254 - 300, 52));
            var rateCount = Txt(rc, "", 34, Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            rateCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rnote = FindOrCreate(rr.transform, "Note");
            SetRect(RT(rnote), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(300, 110));
            Txt(rnote, "한 사람 한 표\n3명부터 보임", 30, Muted, TextAnchor.MiddleRight, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Overflow;
            Loc(rnote, "한 사람 한 표\n3명부터 보임", "One vote each\nShown from 3", "1人1票\n3人から表示", "一人一票\n3人起显示", "Un voto cada uno\nDesde 3");

            // 별점 남기기 — 자격 있는 참가자만 (끝나고 7일 안)
            var form = HuntBoxV(scroll, "Form", new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0.09f), 34, 18);
            var formTitle = HLabel(form, "Title", 0, "", null, 42, Ink, FontStyle.Normal);
            var games = HChips(form, "Games");
            var ggl = games.GetComponent<GridLayoutGroup>();
            ggl.cellSize = new Vector2(450, 92); ggl.spacing = new Vector2(16, 14);
            EditorUtility.SetDirty(ggl);
            var starsRow = FindOrCreate(form, "Stars");
            var stle = Ensure<LayoutElement>(starsRow); stle.preferredHeight = 132; stle.minHeight = 132;
            var sthl = Ensure<HorizontalLayoutGroup>(starsRow);
            sthl.spacing = 6; sthl.childAlignment = TextAnchor.MiddleLeft;
            sthl.childControlWidth = false; sthl.childControlHeight = false; sthl.childForceExpandWidth = false; sthl.childForceExpandHeight = false;
            var starButtons = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                var s = FindOrCreate(starsRow.transform, "S" + (i + 1));
                RT(s).sizeDelta = new Vector2(128, 128);
                var simg = Img(s, null, new Color(0, 0, 0, 0), Image.Type.Simple);
                simg.raycastTarget = true;
                var sb = Ensure<Button>(s);
                sb.transition = Selectable.Transition.None; sb.targetGraphic = simg;
                var sg = FindOrCreate(s.transform, "Glyph");
                Stretch(RT(sg));
                var stx = Txt(sg, "★", 108, new Color(1, 1, 1, 0.16f), TextAnchor.MiddleCenter, FontStyle.Normal);
                stx.horizontalOverflow = HorizontalWrapMode.Overflow; stx.verticalOverflow = VerticalWrapMode.Overflow;
                starButtons[i] = sb;
            }
            var inRow = HRow(form, "InputRow", 120);
            var ihl = inRow.GetComponent<HorizontalLayoutGroup>(); ihl.spacing = 18;
            EditorUtility.SetDirty(ihl);
            var rin = HInput(inRow, "ReviewInput", 120, "한 줄 리뷰 (선택)", "One-line review (optional)", false, 80);
            Ensure<LayoutElement>(rin.gameObject).flexibleWidth = 1;
            foreach (var nm in new[] { "Text", "Placeholder" })
            {
                var t = rin.transform.Find(nm);
                if (t != null) RT(t.gameObject).offsetMax = new Vector2(-170, -16);   // 오른쪽은 글자 수 자리
            }
            var cnt = FindOrCreate(rin.transform, "Count");
            SetRect(RT(cnt), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-34, 0), new Vector2(140, 60));
            var reviewCount = Txt(cnt, "0/80", 30, Muted, TextAnchor.MiddleRight, FontStyle.Normal);
            reviewCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            var send = HButton(inRow, "Send", 120, HuntGold, "남기기", null, 42, HuntDark, FontStyle.Bold, out var sendLabel);
            var sle = Ensure<LayoutElement>(send.gameObject); sle.preferredWidth = 230; sle.flexibleWidth = 0;
            HLabel(form, "Note", 0, "보물을 찾았거나 게임 중 1km 안까지 간 참가자만 남길 수 있어요 · 쓴 사람은 주최자에게 보이지 않아요",
                "Only players who found it or got within 1km can rate · The host can't see who wrote it", 31, Muted, FontStyle.Normal);
            var notPlayed = HLabel(scroll, "NotPlayed", 0, "", null, 32, HuntAmber, FontStyle.Normal);

            // 참여자 리뷰 — 쓴 사람은 보이지 않는다
            var rvb = HuntBoxV(scroll, "ReviewsBox", HuntBox, 30, 24);
            HLabel(rvb, "L", 50, "참여자 리뷰", "Player reviews", 34, Muted, FontStyle.Bold);
            var reviewList = HList(rvb, "List");
            reviewList.GetComponent<VerticalLayoutGroup>().spacing = 30;
            var more = HButton(rvb, "More", 84, new Color(0, 0, 0, 0), "", null, 36, Soft, FontStyle.Bold, out _);

            // 신고 · 차단
            var act = FindOrCreate(scroll, "Actions");
            var avl = Ensure<VerticalLayoutGroup>(act);
            avl.padding = new RectOffset(0, 0, 6, 0); avl.spacing = 16;
            avl.childControlWidth = true; avl.childControlHeight = true; avl.childForceExpandWidth = true; avl.childForceExpandHeight = false;
            var arow = HRow(act.transform, "Row", 120);
            var ahl = arow.GetComponent<HorizontalLayoutGroup>(); ahl.spacing = 24; ahl.childForceExpandWidth = true;
            EditorUtility.SetDirty(ahl);
            var redBg = new Color(HuntRed.r, HuntRed.g, HuntRed.b, 0.12f);
            var report = HButton(arow, "Report", 120, redBg, "주최자 신고", null, 42, HuntRedText, FontStyle.Bold, out _);
            Ensure<LayoutElement>(report.gameObject).flexibleWidth = 1;
            var block = HButton(arow, "Block", 120, redBg, "차단", null, 42, HuntRedText, FontStyle.Bold, out _);
            Ensure<LayoutElement>(block.gameObject).flexibleWidth = 1;
            var anote = HLabel(act.transform, "Note", 0, "신고는 이 사람 게임에 참가한 적이 있어야 할 수 있어요 · 차단하면 이 사람 게임이 목록·알림에서 사라져요",
                "You can report only if you played this host's games · Blocking hides their hunts from your list and alerts", 31, Muted, FontStyle.Normal);
            anote.alignment = TextAnchor.UpperCenter;

            // 신고 사유 고르기
            var rsn = HuntBoxV(scroll, "ReasonBox", new Color(HuntRed.r, HuntRed.g, HuntRed.b, 0.07f), 32, 16);
            var reasonTitle = HLabel(rsn, "Title", 0, "", null, 38, Ink, FontStyle.Bold);
            var reasonList = HList(rsn, "List");
            var cancel = HButton(rsn, "Cancel", 90, new Color(0, 0, 0, 0), "취소", "Cancel", 36, Muted, FontStyle.Bold, out _);
            rsn.gameObject.SetActive(false);

            sec.SetActive(false);

            // ── 움직임 연결 ──
            var hp = Ensure<R0926HuntProfile>(content.gameObject);
            var so = new SerializedObject(hp);
            void S(string prop, Object v) { var p = so.FindProperty(prop); if (p != null) p.objectReferenceValue = v; else log.Add("보물찾기 프로필: 칸 없음 " + prop); }
            S("layout", lay);
            S("stats", stats as RectTransform); S("statsRow", shl);
            S("column", colBtn); S("columnBox", boxImg); S("columnMain", mainT); S("columnLabel", labT);
            S("section", srt); S("scroll", sr); S("content", scroll as RectTransform);
            S("headRight", headRight); S("suspendBox", sus.gameObject); S("suspendText", susText);
            S("recordBox", rec.gameObject); SetArray(so.FindProperty("recordNums"), recordNums); S("recordFoot", recordFoot);
            S("openList", openList); S("openTemplate", openTpl);
            S("rateBox", rate.gameObject); S("rateNum", rateNum); S("rateStars", rateStars); S("rateCount", rateCount);
            S("form", form.gameObject); S("formTitle", formTitle); S("formGames", games); S("chipTemplate", chipTpl);
            SetArray(so.FindProperty("starButtons"), starButtons);
            S("reviewInput", rin); S("reviewCount", reviewCount); S("sendButton", send); S("sendLabel", sendLabel); S("notPlayed", notPlayed);
            S("reviewsBox", rvb.gameObject); S("reviewList", reviewList); S("reviewTemplate", reviewTpl); S("moreButton", more);
            S("actions", act); S("reportButton", report); S("blockButton", block);
            S("reasonBox", rsn.gameObject); S("reasonTitle", reasonTitle); S("reasonList", reasonList); S("reasonTemplate", reasonTpl); S("reasonCancel", cancel);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── 카드 배치 — 펼침 칸을 로그아웃 앞에, 숫자 줄은 세 칸이 들어가게 넓게, 화면에 맞추기 ──
            var lso = new SerializedObject(lay);
            var arr = lso.FindProperty("items");
            int at = -1, logoutAt = -1;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var o = e.FindPropertyRelative("rt").objectReferenceValue;
                if (o == srt) at = i;
                if (logout != null && o == logout) logoutAt = i;
                if (o == stats) e.FindPropertyRelative("width").floatValue = HuntSectionW;   // 두 칸 850 · 세 칸 1050
            }
            if (at < 0)
            {
                // 새 칸은 로그아웃 자리에 — 로그아웃은 한 칸 뒤로 (아래에서 모든 값을 다시 쓴다)
                if (logoutAt >= 0) { arr.InsertArrayElementAtIndex(logoutAt); at = logoutAt; }
                else { arr.arraySize++; at = arr.arraySize - 1; }
            }
            var it = arr.GetArrayElementAtIndex(at);
            it.FindPropertyRelative("rt").objectReferenceValue = srt;
            it.FindPropertyRelative("activeIf").objectReferenceValue = null;
            it.FindPropertyRelative("height").floatValue = 0f;
            it.FindPropertyRelative("width").floatValue = HuntSectionW;
            it.FindPropertyRelative("gapBefore").floatValue = 50f;
            it.FindPropertyRelative("companion").objectReferenceValue = null;
            lso.FindProperty("shrinkItem").objectReferenceValue = srt;
            var dock = panel.transform.Find("DockMirror0926");
            lso.FindProperty("floor").objectReferenceValue = dock as RectTransform;
            lso.FindProperty("homeY").floatValue = RT(content.gameObject).anchoredPosition.y;
            lso.FindProperty("margin").floatValue = 40f;
            lso.FindProperty("minShrink").floatValue = 360f;
            // 숫자 칸 다시 칠하기는 팔로잉·팔로워만 ('주최' 칸은 R0926HuntProfile 이 칠한다)
            var counts = new List<Object>();
            foreach (Transform b in stats)
            {
                if (b == col.transform) continue;
                foreach (var tx in b.GetComponentsInChildren<Text>(true)) counts.Add(tx);
            }
            SetArray(lso.FindProperty("countTexts"), counts.ToArray());
            lso.ApplyModifiedPropertiesWithoutUndo();
            if (dock == null) log.Add("보물찾기 프로필: 도크 자리 없음 — 화면 아래 끝까지로 맞춤");

            // ── 키보드 위 입력줄 — 리뷰 한 줄 (장소 추가 창과 같은 줄) ──
            var bar = panel.transform.Find("UploadInputMirror");
            if (bar == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UploadInputMirror.prefab");
                if (prefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel.transform);
                    inst.name = "UploadInputMirror";
                    bar = inst.transform;
                }
            }
            if (bar == null) { log.Add("보물찾기 프로필: 입력줄 프리팹 없음"); return; }
            bar.gameObject.SetActive(false);
            var m = Ensure<UploadInputMirror>(panel);
            var mso = new SerializedObject(m);
            SetArray(mso.FindProperty("sourceInputs"), rin);
            // 장소 추가 창의 입력줄과 같은 설정으로
            var page = Find(root, "UploadPage");
            var refM = page != null ? page.GetComponent<UploadInputMirror>() : null;
            if (refM != null)
            {
                var rso = new SerializedObject(refM);
                foreach (var p in new[] { "keyboardTopPadding", "lerpSpeed", "createBackdrop" })
                {
                    var src = rso.FindProperty(p);
                    if (src != null && mso.FindProperty(p) != null) mso.CopyFromSerializedProperty(src);
                }
            }
            mso.ApplyModifiedPropertiesWithoutUndo();
            InputBar(bar.gameObject, "TextInput", "SendButton", true, log);
            WireMirror(m, bar, log);
            log.Add("hunt profile ok");
        }

        // ── 부품 ────────────────────────────────────────────
        private static void HuntFill(GameObject g, float l, float b, float r, float t)
        {
            var rt = RT(g);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            EditorUtility.SetDirty(rt);
        }

        // 둥근 상자 + 세로 쌓기
        private static Transform HuntBoxV(Transform parent, string name, Color bg, int padV, float spacing)
        {
            var g = FindOrCreate(parent, name);
            Img(g, Spr("r0926_pill"), bg, Image.Type.Sliced, 64f / 40f).raycastTarget = false;
            var vl = Ensure<VerticalLayoutGroup>(g);
            vl.padding = new RectOffset(40, 40, padV, padV); vl.spacing = spacing;
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            EditorUtility.SetDirty(vl);
            return g.transform;
        }

        private static GameObject HuntOpenRowTemplate(Transform tpl)
        {
            var row = FindOrCreate(tpl, "OpenRow");
            Ensure<LayoutElement>(row).preferredHeight = 156;
            var img = Img(row, Spr("r0926_pill"), HuntBox, Image.Type.Sliced, 64f / 40f);
            img.raycastTarget = true;
            var b = Ensure<Button>(row); b.transition = Selectable.Transition.ColorTint; b.targetGraphic = img;
            var dot = FindOrCreate(row.transform, "Dot");
            SetRect(RT(dot), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54, 0), new Vector2(28, 28));
            Img(dot, Spr("r0926_circle"), HuntTeal, Image.Type.Simple).raycastTarget = false;
            var title = FindOrCreate(row.transform, "Title");
            SetRect(RT(title), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(96, 2), new Vector2(-96 - 200, 58));
            Txt(title, "", 42, Ink, TextAnchor.LowerLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            var sub = FindOrCreate(row.transform, "Sub");
            SetRect(RT(sub), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(96, -6), new Vector2(-96 - 200, 50));
            Txt(sub, "", 31, Muted, TextAnchor.UpperLeft, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Overflow;
            var badge = FindOrCreate(row.transform, "Badge");
            SetRect(RT(badge), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-36, 0), new Vector2(150, 78));
            Img(badge, Spr("r0926_pill"), new Color(HuntTeal.r, HuntTeal.g, HuntTeal.b, 0.16f), Image.Type.Sliced, 64f / 39f).raycastTarget = false;
            var bl = FindOrCreate(badge.transform, "Label");
            Stretch(RT(bl));
            Txt(bl, "보기", 34, HuntTeal, TextAnchor.MiddleCenter, FontStyle.Bold);
            Loc(bl, "보기", "View", "見る", "查看", "Ver");
            return row;
        }

        private static GameObject HuntReviewRowTemplate(Transform tpl)
        {
            var row = FindOrCreate(tpl, "ReviewRow");
            var hl = Ensure<HorizontalLayoutGroup>(row);
            hl.spacing = 24; hl.childAlignment = TextAnchor.UpperLeft; hl.padding = new RectOffset(0, 0, 0, 0);
            hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var av = FindOrCreate(row.transform, "Avatar");
            var ale = Ensure<LayoutElement>(av); ale.minWidth = ale.preferredWidth = 76; ale.minHeight = ale.preferredHeight = 76;
            Img(av, Spr("r0926_circle"), new Color(1, 1, 1, 0.08f), Image.Type.Simple).raycastTarget = false;
            var q = FindOrCreate(av.transform, "Q");
            Stretch(RT(q));
            Txt(q, "?", 36, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            var body = FindOrCreate(row.transform, "Body");
            Ensure<LayoutElement>(body).flexibleWidth = 1;
            var bvl = Ensure<VerticalLayoutGroup>(body);
            bvl.spacing = 6; bvl.padding = new RectOffset(0, 0, 4, 0);
            bvl.childControlWidth = true; bvl.childControlHeight = true; bvl.childForceExpandWidth = true; bvl.childForceExpandHeight = false;
            var hd = FindOrCreate(body.transform, "Head");
            var hdt = Txt(hd, "", 31, Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            hdt.supportRichText = true; hdt.horizontalOverflow = HorizontalWrapMode.Wrap; hdt.verticalOverflow = VerticalWrapMode.Overflow;
            var tx = FindOrCreate(body.transform, "Text");
            var txt = Txt(tx, "", 40, Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            txt.supportRichText = false; txt.lineSpacing = 1.1f;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap; txt.verticalOverflow = VerticalWrapMode.Overflow;
            var rep = FindOrCreate(row.transform, "Report");
            var rle = Ensure<LayoutElement>(rep); rle.minWidth = rle.preferredWidth = 116; rle.minHeight = rle.preferredHeight = 64;
            var rimg = Img(rep, Spr("r0926_pill"), new Color(1, 1, 1, 0.07f), Image.Type.Sliced, 64f / 32f);
            rimg.raycastTarget = true;
            var rb = Ensure<Button>(rep); rb.transition = Selectable.Transition.ColorTint; rb.targetGraphic = rimg;
            var rl = FindOrCreate(rep.transform, "Label");
            Stretch(RT(rl));
            Txt(rl, "신고", 30, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            Loc(rl, "신고", "Report", "通報", "举报", "Denunciar");
            return row;
        }

        private static GameObject HuntReasonTemplate(Transform tpl)
        {
            var r = FindOrCreate(tpl, "Reason");
            Ensure<LayoutElement>(r).preferredHeight = 110;
            var img = Img(r, Spr("r0926_pill"), new Color(1, 1, 1, 0.07f), Image.Type.Sliced, 64f / 44f);
            img.raycastTarget = true;
            var b = Ensure<Button>(r); b.transition = Selectable.Transition.ColorTint; b.targetGraphic = img;
            var l = FindOrCreate(r.transform, "Label");
            HuntFill(l, 44, 0, 44, 0);
            Txt(l, "", 40, Ink, TextAnchor.MiddleLeft, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Overflow;
            return r;
        }
    }
}
