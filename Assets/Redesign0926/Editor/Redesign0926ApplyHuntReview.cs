using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 보물 숨기기 확인 카드 (10-26 대표님 — 올리기 전에 카드로 한 번 보고 '숨기기', 숨긴 뒤엔 같은 카드에 참가 코드 · 공유하기).
    /// 캔버스 맨 위 층에 두어 추가 창·도크·위 탭까지 덮는다. 줄을 채우고 움직이는 건 R0926HuntCard.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static void HuntReviewCard(Transform root, SerializedObject so, List<string> log)
        {
            void S(string prop, Object v) { var p = so.FindProperty(prop); if (p != null) p.objectReferenceValue = v; else log.Add("보물찾기 확인 카드: 칸 없음 " + prop); }

            var go = FindOrCreate(root, "HuntReview0926");
            Stretch(RT(go));
            var cv = Ensure<Canvas>(go);
            cv.overrideSorting = true;
            cv.sortingOrder = 30005;   // 창·도크·토스트(9500) 위 — 자르기(30000)와 처리 중 카드(30010) 사이
            Ensure<GraphicRaycaster>(go);
            Ensure<R0926HuntReview>(go);   // 보물찾기 카드가 꺼지면 같이 닫힌다

            // 바깥을 누르거나 뒤로가기 — 고치기(숨긴 뒤면 확인)와 같다. 올리는 중엔 삼킨다
            var dim = FindOrCreate(go.transform, "Dim0926");
            Stretch(RT(dim));
            Img(dim, null, new Color(0f, 0f, 0f, 0.62f), Image.Type.Simple).raycastTarget = true;
            var db = Ensure<Button>(dim);
            db.transition = Selectable.Transition.None;
            db.navigation = new Navigation { mode = Navigation.Mode.None };
            ResetListeners(db);
            Ensure<ClickButtonOnBack>(dim);
            S("review", go); S("reviewDim", db);

            var sheet = FindOrCreate(go.transform, "Sheet0926");
            SetRect(RT(sheet), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 0));
            Img(sheet, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;   // 카드 안을 눌러도 닫히지 않게
            var vl = Ensure<VerticalLayoutGroup>(sheet);
            vl.padding = new RectOffset(64, 64, 72, 60); vl.spacing = 28;
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            var fit = Ensure<ContentSizeFitter>(sheet);
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var head = HLabel(sheet.transform, "Head", 0, "", null, 60, Ink, FontStyle.Bold);
            head.alignment = TextAnchor.MiddleCenter;
            var sub = HLabel(sheet.transform, "Sub", 0, "", null, 40, Soft, FontStyle.Normal);
            sub.alignment = TextAnchor.MiddleCenter;
            S("reviewHead", head); S("reviewSub", sub);

            // 고른 것 한 줄씩 — 왼쪽 이름 · 오른쪽 값 (값이 길면 줄을 바꾼다)
            var rows = FindOrCreate(sheet.transform, "Rows");
            Img(rows, Spr("r0926_pill"), new Color(1f, 1f, 1f, 0.05f), Image.Type.Sliced, 64f / 40f).raycastTarget = false;
            var rvl = Ensure<VerticalLayoutGroup>(rows);
            rvl.padding = new RectOffset(44, 44, 36, 36); rvl.spacing = 18;
            rvl.childControlWidth = true; rvl.childControlHeight = true; rvl.childForceExpandWidth = true; rvl.childForceExpandHeight = false;
            var row = FindOrCreate(rows.transform, "RowTemplate");
            var hl = Ensure<HorizontalLayoutGroup>(row);
            hl.spacing = 28; hl.childAlignment = TextAnchor.UpperLeft;
            hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var lab = FindOrCreate(row.transform, "Label");
            var lle = Ensure<LayoutElement>(lab); lle.minWidth = 270; lle.preferredWidth = 270; lle.flexibleWidth = 0;
            Txt(lab, "", 40, Muted, TextAnchor.UpperLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Wrap;
            var val = FindOrCreate(row.transform, "Value");
            Ensure<LayoutElement>(val).flexibleWidth = 1;
            var vt = Txt(val, "", 42, Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            vt.horizontalOverflow = HorizontalWrapMode.Wrap; vt.verticalOverflow = VerticalWrapMode.Overflow; vt.lineSpacing = 1.1f;
            row.SetActive(false);
            S("reviewRows", RT(rows)); S("reviewRowTemplate", row);

            // 숨긴 뒤 — 참가 코드 (참가 코드 게임만)
            var codeBox = FindOrCreate(sheet.transform, "CodeBox");
            Img(codeBox, Spr("r0926_pill"), new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0.12f), Image.Type.Sliced, 64f / 40f).raycastTarget = false;
            var cvl = Ensure<VerticalLayoutGroup>(codeBox);
            cvl.padding = new RectOffset(40, 40, 30, 34); cvl.spacing = 6; cvl.childAlignment = TextAnchor.UpperCenter;
            cvl.childControlWidth = true; cvl.childControlHeight = true; cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;
            var cl = FindOrCreate(codeBox.transform, "L");
            Txt(cl, "참가 코드", 40, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            HLoc(cl, "참가 코드", "Join code");
            var cc = FindOrCreate(codeBox.transform, "Code");
            var codeText = Txt(cc, "", 108, HuntGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            codeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            codeBox.SetActive(false);
            S("reviewCodeBox", codeBox); S("reviewCode", codeText);

            var note = HLabel(sheet.transform, "Note", 0, "", null, 38, Muted, FontStyle.Normal);
            note.alignment = TextAnchor.MiddleCenter;
            var msg = HLabel(sheet.transform, "Msg", 0, "", null, 40, HuntAmber, FontStyle.Bold);
            msg.alignment = TextAnchor.MiddleCenter;
            msg.gameObject.SetActive(false);
            S("reviewNote", note); S("reviewMsg", msg);

            // 확인할 땐 [고치기 | 숨기기], 숨긴 뒤엔 [확인 | 공유하기] — 왼쪽은 물러나기, 오른쪽(금색)은 나아가기
            var bar = HRow(sheet.transform, "Buttons", 148);
            var edit = HButton(bar, "Edit", 148, new Color(1f, 1f, 1f, 0.1f), "고치기", "Edit", 48, Ink, FontStyle.Bold, out _);
            var done = HButton(bar, "Done", 148, new Color(1f, 1f, 1f, 0.1f), "확인", "OK", 48, Ink, FontStyle.Bold, out _);
            var goB = HButton(bar, "Go", 148, HuntGold, "숨기기", null, 50, HuntDark, FontStyle.Bold, out var goLabel);
            var share = HButton(bar, "Share", 148, HuntGold, "공유하기", "Share", 50, HuntDark, FontStyle.Bold, out _);
            int bi = 0;
            foreach (var b in new[] { edit, done, goB, share })
            {
                b.transform.SetSiblingIndex(bi++);
                var le = Ensure<LayoutElement>(b.gameObject); le.preferredWidth = 0; le.flexibleWidth = 1;
            }
            done.gameObject.SetActive(false); share.gameObject.SetActive(false);
            S("reviewEdit", edit); S("reviewGo", goB); S("reviewGoLabel", goLabel); S("reviewShare", share); S("reviewDone", done);

            int si = 0;
            foreach (var t in new Transform[] { head.transform, sub.transform, rows.transform, codeBox.transform, note.transform, msg.transform, bar }) t.SetSiblingIndex(si++);
            go.SetActive(false);
        }
    }
}
