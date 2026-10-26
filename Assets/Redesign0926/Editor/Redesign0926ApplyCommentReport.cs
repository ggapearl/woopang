using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v49: 10-24 대표님 「상품 댓글 이나 이런 것에 있어서도 해당 댓글도 '신고' 할 수 있도록 하고 10개정도 신고 들어가면 그냥 삭제」
    ///  — 댓글 창 위에 뜨는 신고 사유 시트(CommentReportSheet0926). 남의 댓글을 왼쪽으로 밀면 나오는 '신고'가 연다(CommentManager).
    ///  모양은 장소 신고 시트(ReportSheet0926)와 같다 — 사유 넷 + 아래 취소 알약 · 끌어내리기 · 뒤로가기 · 바깥 누르기.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static void CommentReportSheet(Transform root, List<string> log)
        {
            var cm = FindInScene<CommentManager>(root.gameObject);
            var panel = cm != null ? cm.commentPanel : null;
            if (panel == null) { log.Add("댓글 신고 시트: 댓글 창 없음"); return; }

            var sheet = FindOrCreate(panel.transform, "CommentReportSheet0926");
            sheet.transform.SetAsLastSibling();
            Img(sheet, null, new Color(0, 0, 0, 0.5f), Image.Type.Simple).raycastTarget = true;
            SetRect(RT(sheet), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var logic = Ensure<R0926CommentReportSheet>(sheet);

            const float rowH = 140f, head = 150f, bottom = 40f, cancelH = 150f, gap = 16f;
            string[][] reasons =
            {
                new[] { "spam", "스팸·광고", "Spam or ads", "スパム・広告", "垃圾广告", "Spam" },
                new[] { "abuse", "욕설·비방", "Harassment or insults", "誹謗中傷", "辱骂诽谤", "Acoso o insultos" },
                new[] { "sexual_hate", "음란·혐오", "Sexual or hateful", "わいせつ・ヘイト", "色情或仇恨", "Sexual u odio" },
                new[] { "other", "기타", "Other", "その他", "其他", "Otro" },
            };

            var cancel = FindOrCreate(sheet.transform, "Cancel0926");
            Img(cancel, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(cancel), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(-CardSide * 2, cancelH));
            Ensure<R0926SafeInset>(cancel).SetEdge(R0926SafeInset.Edge.Bottom);
            var cBtn = Ensure<Button>(cancel);
            ButtonLabel(cancel, CancelWords, Ink);
            ResetListeners(cBtn);
            UnityEventTools.AddPersistentListener(cBtn.onClick, logic.Close);

            var card = FindOrCreate(sheet.transform, "Card0926");
            Img(card, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(card), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom + cancelH + gap), new Vector2(-CardSide * 2, head + rowH * reasons.Length));
            Ensure<R0926SafeInset>(card).SetEdge(R0926SafeInset.Edge.Bottom);
            Slide(card, 260f);
            Slide(cancel, 260f);

            var title = FindOrCreate(card.transform, "Title0926");
            Txt(title, "이 댓글을 신고하는 이유", 40, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(title), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-80, head));
            Loc(title, "이 댓글을 신고하는 이유", "Why are you reporting this comment?", "報告する理由", "举报原因", "¿Por qué lo denuncias?");

            for (int i = 0; i < reasons.Length; i++)
            {
                var r = reasons[i];
                var row = FindOrCreate(card.transform, "Reason_" + r[0]);
                Img(row, null, new Color(1, 1, 1, 0), Image.Type.Simple).raycastTarget = true;
                SetRect(RT(row), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -head - rowH * i), new Vector2(0, rowH));
                var line = FindOrCreate(row.transform, "Line0926");
                Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-80, 2));
                var lb = FindOrCreate(row.transform, "Label0926");
                Txt(lb, r[1], 44, Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
                var lrt = RT(lb); lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                Loc(lb, r[1], r[2], r[3], r[4], r[5]);
                var b = Ensure<Button>(row);
                b.targetGraphic = lb.GetComponent<Text>();
                ResetListeners(b);
                UnityEventTools.AddStringPersistentListener(b.onClick, logic.SendReason, r[0]);
                EditorUtility.SetDirty(b);
            }

            // 바깥(어두운 바탕) 누르기 · 아래로 끌기 · 뒤로가기 = 취소 — 장소 신고 시트와 같은 길 (Apply8 은 이 단계보다 먼저 돈다)
            var tap = new SerializedObject(Ensure<R0926TapToClose>(sheet));
            tap.FindProperty("closeButton").objectReferenceValue = cBtn;
            tap.ApplyModifiedPropertiesWithoutUndo();
            BackCloses(cancel.transform, "댓글 신고", log);
            string cardPath = AnimationUtility.CalculateTransformPath(card.transform, root);
            string cancelPath = AnimationUtility.CalculateTransformPath(cancel.transform, root);
            Swipe(root, cardPath, cancelPath, new[] { cancelPath }, log);
            Proxy(root, card.GetComponent<R0926SwipeDismiss>(), cancelPath, log);

            sheet.SetActive(false);
            log.Add("comment report sheet ok");
        }
    }
}
