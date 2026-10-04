using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v2: 도크 위 카드(목록·메시지), 공통 대화상자, 장소 '···' 메뉴 + 신고, 대화방 신고·차단, 프로필.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static readonly Color Card = new Color(0.137f, 0.157f, 0.176f, 1f);   // #23282D
        private static readonly Color Danger = new Color(1f, 0.42f, 0.42f, 1f);        // #FF6B6B
        private const float CardRadius = 70f;
        private const float CardSide = 24f;

        // ── 도크 위에 뜨는 카드 (아래는 도크 위까지, 위는 화면 비율) ──────────────
        private static void CardAboveDock(GameObject card, float topAnchor)
        {
            Img(card, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius).raycastTarget = true;
            var rt = RT(card);
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, topAnchor); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(CardSide, DockBottom + DockHeight + 24);
            rt.offsetMax = new Vector2(-CardSide, 0);
            Ensure<R0926SafeInset>(card).SetEdge(R0926SafeInset.Edge.Bottom, R0926SafeInset.Mode.Stretch);
            EditorUtility.SetDirty(rt);
        }

        private static void NoInset(GameObject go)
        {
            var c = go.GetComponent<R0926SafeInset>();
            if (c != null) Object.DestroyImmediate(c);
        }

        /// <summary>
        /// 패널의 닫기 버튼을 도크의 해당 칸(목록=0.19, 메시지=0.81) 자리에 놓는다.
        /// 도크와 같은 크기의 투명 틀(DockMirror0926)을 패널 안에 만들어 정확히 겹친다.
        /// </summary>
        private static void DockSlotClose(GameObject btn, Transform panel, float x)
        {
            var mirror = FindOrCreate(panel, "DockMirror0926");
            mirror.transform.SetAsLastSibling();
            SetRect(RT(mirror), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(0, DockBottom), new Vector2(-Side * 2, DockHeight));
            Ensure<R0926SafeInset>(mirror).SetEdge(R0926SafeInset.Edge.Bottom);

            btn.transform.SetParent(mirror.transform, false);
            btn.transform.localScale = Vector3.one;
            SetRect(RT(btn), new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, DockHeight));
            var img = btn.GetComponent<Image>();
            img.sprite = null; img.color = new Color(1, 1, 1, 0); img.raycastTarget = true;
            foreach (var t in btn.GetComponentsInChildren<Text>(true))
                if (t.name != "Label0926") t.enabled = false;   // 예전 "X" 글자

            var ic = FindOrCreate(btn.transform, "Icon0926");
            var icImg = Img(ic, Spr("r0926_i_close"), Ink, Image.Type.Simple);
            icImg.raycastTarget = false;
            SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockIconY), new Vector2(DockIcon, DockIcon));
            var lb = FindOrCreate(btn.transform, "Label0926");
            Txt(lb, "닫기", DockLabel, Soft, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(lb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockLabelY), new Vector2(280, DockLabelBox));
            Loc(lb, "닫기", "Close", "閉じる", "关闭", "Cerrar");
            var b = btn.GetComponent<Button>();
            if (b != null) { b.targetGraphic = icImg; EditorUtility.SetDirty(b); }
        }

        // ============================================================
        // 공통 대화상자 — 어두운 카드 + 글자 버튼 (취소 왼쪽 · 확인 오른쪽)
        // ============================================================
        private static void ApplyDialogs(Transform root, List<string> log)
        {
            var login = Find(root, "LoginPromptPanel");
            if (login != null)
            {
                var box = Find(login.transform, "Box");
                StyleDialog(box, Find(box.transform, "Label"), Find(box.transform, "Buttons"),
                    Find(box.transform, "Btn_Yes"), Find(box.transform, "Btn_No"),
                    new[] { "로그인", "Sign in", "ログイン", "登录", "Iniciar sesión" }, CancelWords, false);
            }

            var perm = Find(root, "LocationPermissionPanel");
            if (perm != null)
            {
                var box = Find(perm.transform, "Box");
                if (box != null)
                    StyleDialog(box, Find(box.transform, "Label"), Find(box.transform, "Buttons"),
                        Find(box.transform, "Btn_Settings"), Find(box.transform, "Btn_Close"),
                        new[] { "설정 열기", "Open Settings", "設定を開く", "打开设置", "Abrir ajustes" },
                        new[] { "닫기", "Close", "閉じる", "关闭", "Cerrar" }, false);
            }

            var remove = Find(root, "RemoveRequestPanel");
            if (remove != null)
            {
                remove.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
                var box = FindOrCreate(remove.transform, "Box0926");
                box.transform.SetSiblingIndex(0);
                SetRect(RT(box), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040, 560));
                var text = Find(remove.transform, "Text");
                if (text != null) text.transform.SetParent(box.transform, false);
                var buttons = FindOrCreate(box.transform, "Buttons0926");
                var yes = Find(remove.transform, "Remove_YES");
                var no = Find(remove.transform, "Remove_NO");
                if (yes != null) yes.transform.SetParent(buttons.transform, false);
                if (no != null) no.transform.SetParent(buttons.transform, false);
                StyleDialog(box, text, buttons, yes, no,
                    new[] { "삭제 요청", "Request removal", "削除をリクエスト", "申请删除", "Solicitar eliminación" }, CancelWords, true);
                if (text != null) Loc(text, "이 장소의 삭제를 요청할까요?", "Request removal of this place?", "この場所の削除をリクエストしますか？", "要申请删除这个地点吗？", "¿Solicitar la eliminación de este lugar?");
            }

            // 3D 받기 — 카드 모양만 맞추고 버튼 글자는 코드가 쓰는 그대로 둔다
            var dance = Find(root, "DanceAnimPanel");
            if (dance != null)
            {
                Img(dance, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 0.98f), Image.Type.Sliced, 64f / 56f);
                RT(dance).sizeDelta = new Vector2(1040, 600);
                Slide(dance, 140f);
                var title = Find(dance.transform, "Title");
                if (title != null) { var t = title.GetComponent<Text>(); t.fontSize = 50; t.fontStyle = FontStyle.Bold; t.color = Ink; }
                var info = Find(dance.transform, "SizeInfo");
                if (info != null) info.GetComponent<Text>().color = Muted;
                var confirm = Find(dance.transform, "ConfirmButton");
                var cancel = Find(dance.transform, "CancelButton");
                if (confirm != null)
                {
                    PillButton(confirm, true);
                    SetRect(RT(confirm), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(10, 56), new Vector2(438, 128));
                }
                if (cancel != null)
                {
                    PillButton(cancel, false);
                    SetRect(RT(cancel), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-10, 56), new Vector2(438, 128));
                }
            }
            log.Add("dialogs ok");
        }

        private static readonly string[] CancelWords = { "취소", "Cancel", "キャンセル", "取消", "Cancelar" };

        private static void StyleDialog(GameObject box, GameObject label, GameObject buttons, GameObject yes, GameObject no,
            string[] yesWords, string[] noWords, bool danger)
        {
            if (box == null) return;
            Img(box, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 0.98f), Image.Type.Sliced, 64f / 56f);
            Slide(box, 140f);
            RT(box).sizeDelta = new Vector2(1040, 560);

            if (label != null)
            {
                var t = label.GetComponent<Text>();
                t.fontSize = 44; t.color = Ink; t.alignment = TextAnchor.MiddleCenter; t.lineSpacing = 1.15f;
                t.fontStyle = FontStyle.Normal;
                t.resizeTextForBestFit = false;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                if (font != null) t.font = font;
                var lrt = RT(label);
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.offsetMin = new Vector2(70, 210); lrt.offsetMax = new Vector2(-70, -50);
                EditorUtility.SetDirty(t);
            }
            if (buttons != null)
            {
                SetRect(RT(buttons), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(-112, 128));
            }
            if (no != null)
            {
                PillButton(no, false);
                var rt = RT(no);
                rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(-12, 0);
                ButtonLabel(no, noWords, Ink);
            }
            if (yes != null)
            {
                PillButton(yes, true);
                var rt = RT(yes);
                rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(12, 0); rt.offsetMax = Vector2.zero;
                ButtonLabel(yes, yesWords, danger ? new Color(0.8f, 0.2f, 0.2f, 1f) : Dark);
            }
        }

        private static void PillButton(GameObject btn, bool primary)
        {
            btn.transform.localScale = Vector3.one;
            var img = Img(btn, Spr("r0926_pill"), primary ? Ink : new Color(1, 1, 1, 0.1f), Image.Type.Sliced, 64f / 64f);
            img.preserveAspect = false;
            img.raycastTarget = true;
            foreach (var t in btn.GetComponentsInChildren<Text>(true))
            {
                t.color = primary ? Dark : Ink;
                t.fontStyle = FontStyle.Bold;
                t.fontSize = 42;
                if (font != null) t.font = font;
                EditorUtility.SetDirty(t);
            }
        }

        private static void ButtonLabel(GameObject btn, string[] words, Color color)
        {
            var lb = FindOrCreate(btn.transform, "Label0926");
            var t = Txt(lb, words[0], 42, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            var rt = RT(lb);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            Loc(lb, words[0], words[1], words[2], words[3], words[4]);
        }

        // ============================================================
        // 장소 상세 '···' — 정보 수정 요청 · 삭제 요청 · 신고하기
        // ============================================================
        private static void ApplyPlaceMenu(Transform root, List<string> log)
        {
            var full = Find(root, "FullScreenPanel");
            var fixBtn = full != null ? Find(full.transform, "FixButton") : null;
            var fixOpp = full != null ? Find(full.transform, "FixButton_Opposite") : null;
            var panel = full != null ? Find(full.transform, "FixButtonPanel") : null;
            if (fixBtn == null || fixOpp == null || panel == null) { log.Add("장소 메뉴 없음"); return; }

            foreach (var b in new[] { fixBtn, fixOpp })
            {
                Img(b, Spr("r0926_circle"), Glass, Image.Type.Simple).raycastTarget = true;
                RT(b).sizeDelta = new Vector2(130, 130);
                var ic = FindOrCreate(b.transform, "Icon0926");
                Img(ic, Spr("r0926_i_more"), Ink, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
            }
            RT(fixOpp).anchoredPosition = RT(fixBtn).anchoredPosition;

            // 바탕 어둡게 + 바깥 누르면 닫기
            Img(panel, null, new Color(0, 0, 0, 0.45f), Image.Type.Simple).raycastTarget = true;
            var tap = Ensure<R0926TapToClose>(panel);
            var tso = new SerializedObject(tap);
            tso.FindProperty("closeButton").objectReferenceValue = fixOpp.GetComponent<Button>();
            tso.ApplyModifiedPropertiesWithoutUndo();

            const float rowH = 150f, cancelH = 150f, gap = 16f, bottom = 40f;
            var cancel = FindOrCreate(panel.transform, "MoreCancel0926");
            Img(cancel, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(cancel), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(-CardSide * 2, cancelH));
            Ensure<R0926SafeInset>(cancel).SetEdge(R0926SafeInset.Edge.Bottom);
            var cBtn = Ensure<Button>(cancel);
            ButtonLabel(cancel, CancelWords, Ink);
            ResetListeners(cBtn);
            AddSetActive(cBtn, panel, false);
            AddSetActive(cBtn, fixBtn, true);
            AddSetActive(cBtn, fixOpp, false);

            var sheet = FindOrCreate(panel.transform, "MoreSheet0926");
            Img(sheet, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(sheet), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom + cancelH + gap), new Vector2(-CardSide * 2, rowH * 3));
            Ensure<R0926SafeInset>(sheet).SetEdge(R0926SafeInset.Edge.Bottom);
            Slide(sheet, 260f);
            Slide(cancel, 260f);

            var fixPlace = Find(panel.transform, "FixPlaceButton");
            var removePlace = Find(panel.transform, "RemovePlaceButton");
            SheetRow(fixPlace, sheet.transform, 0, rowH, "r0926_i_edit", Ink,
                new[] { "정보 수정 요청", "Suggest an edit", "情報の修正をリクエスト", "申请修改信息", "Sugerir un cambio" });
            SheetRow(removePlace, sheet.transform, 1, rowH, "r0926_i_trash", Ink,
                new[] { "삭제 요청", "Request removal", "削除をリクエスト", "申请删除", "Solicitar eliminación" });

            var report = Find(sheet.transform, "ReportButton0926") ?? NewUI("ReportButton0926", sheet.transform);
            Ensure<Image>(report);
            var rBtn = Ensure<Button>(report);
            SheetRow(report, sheet.transform, 2, rowH, "r0926_i_flag", Danger,
                new[] { "신고하기", "Report", "報告する", "举报", "Denunciar" });

            OnTop(panel);   // 상세 화면은 도크 아래에 그려지는 구조라, 시트만 맨 위로 올린다
            var reportSheet = BuildReportSheet(full.transform);
            OnTop(reportSheet);

            // 댓글 미리보기를 도크 위로 — 겹치지 않게
            var preview = Find(full.transform, "CommentPreviewPanel");
            if (preview != null) RT(preview).anchoredPosition = new Vector2(RT(preview).anchoredPosition.x, DockBottom + DockHeight + 24);
            ResetListeners(rBtn);
            AddSetActive(rBtn, panel, false);
            AddSetActive(rBtn, fixBtn, true);
            AddSetActive(rBtn, fixOpp, false);
            AddSetActive(rBtn, reportSheet, true);
            log.Add("place menu ok");
        }

        private static void SheetRow(GameObject row, Transform sheet, int index, float rowH, string icon, Color color, string[] words)
        {
            if (row == null) return;
            row.transform.SetParent(sheet, false);
            row.transform.localScale = Vector3.one;
            row.transform.SetSiblingIndex(index);
            SetRect(RT(row), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -rowH * index), new Vector2(0, rowH));
            var img = row.GetComponent<Image>();
            img.sprite = null; img.color = new Color(1, 1, 1, 0); img.raycastTarget = true;
            foreach (var t in row.GetComponentsInChildren<Text>(true))
                if (t.name != "Label0926") t.enabled = false;   // 예전 "Fix Place" 글자

            if (index > 0)
            {
                var line = FindOrCreate(row.transform, "Line0926");
                Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-80, 2));
            }
            var ic = FindOrCreate(row.transform, "Icon0926");
            Img(ic, Spr(icon), color, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(ic), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90, 0), new Vector2(60, 60));
            var lb = FindOrCreate(row.transform, "Label0926");
            Txt(lb, words[0], 44, color, TextAnchor.MiddleLeft, FontStyle.Bold);
            var lrt = RT(lb);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(150, 0); lrt.offsetMax = new Vector2(-40, 0);
            Loc(lb, words[0], words[1], words[2], words[3], words[4]);
        }

        private static GameObject BuildReportSheet(Transform full)
        {
            var root = FindOrCreate(full, "ReportSheet0926");
            root.transform.SetAsLastSibling();
            Img(root, null, new Color(0, 0, 0, 0.5f), Image.Type.Simple).raycastTarget = true;
            SetRect(RT(root), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var logic = Ensure<R0926ReportSheet>(root);

            const float rowH = 140f, head = 150f, bottom = 40f, cancelH = 150f, gap = 16f;
            string[][] reasons =
            {
                new[] { "spam", "스팸·광고", "Spam or ads", "スパム・広告", "垃圾广告", "Spam" },
                new[] { "inappropriate_photo", "부적절한 사진", "Inappropriate photo", "不適切な写真", "不当图片", "Foto inapropiada" },
                new[] { "wrong_info", "없는 장소·잘못된 정보", "Wrong or fake place", "存在しない場所・誤情報", "错误或虚假地点", "Lugar falso o erróneo" },
                new[] { "other", "기타", "Other", "その他", "其他", "Otro" },
            };

            var cancel = FindOrCreate(root.transform, "Cancel0926");
            Img(cancel, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(cancel), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(-CardSide * 2, cancelH));
            Ensure<R0926SafeInset>(cancel).SetEdge(R0926SafeInset.Edge.Bottom);
            var cBtn = Ensure<Button>(cancel);
            ButtonLabel(cancel, CancelWords, Ink);
            ResetListeners(cBtn);
            AddSetActive(cBtn, root, false);

            var card = FindOrCreate(root.transform, "Card0926");
            Img(card, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(card), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom + cancelH + gap), new Vector2(-CardSide * 2, head + rowH * reasons.Length));
            Ensure<R0926SafeInset>(card).SetEdge(R0926SafeInset.Edge.Bottom);
            Slide(card, 260f);
            Slide(cancel, 260f);

            var title = FindOrCreate(card.transform, "Title0926");
            Txt(title, "이 장소를 신고하는 이유", 40, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(title), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-80, head));
            Loc(title, "이 장소를 신고하는 이유", "Why are you reporting this place?", "報告する理由", "举报原因", "¿Por qué lo denuncias?");

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
            root.SetActive(false);
            return root;
        }

        // ============================================================
        // 메시지·대화방 — 도크 위 카드 + 닫기는 도크 '메시지' 자리 + 대화방 '···'(신고·차단)
        // ============================================================
        private static void ApplyMessages(Transform root, List<string> log)
        {
            foreach (var name in new[] { "MessagePanel", "ChatRoomPanel" })
            {
                var panel = Find(root, name);
                if (panel == null) continue;
                var pImg = panel.GetComponent<Image>();
                if (pImg != null) pImg.color = new Color(0, 0, 0, 0.35f);
                var bg = Find(panel.transform, "Background");
                if (bg != null) { CardAboveDock(bg, 0.86f); Slide(bg, 240f); }
                var close = panel.transform.Find("CloseButton") ?? panel.transform.Find("DockMirror0926/CloseButton");
                if (close != null) DockSlotClose(close.gameObject, panel.transform, SlotMsg);
            }

            var chat = Find(root, "ChatRoomPanel");
            var header = chat != null ? Find(chat.transform, "Header") : null;
            if (header != null)
            {
                var menu = Ensure<R0926ChatMenu>(chat);
                var more = FindOrCreate(header.transform, "MoreButton0926");
                Img(more, Spr("r0926_circle"), new Color(1, 1, 1, 0.08f), Image.Type.Simple).raycastTarget = true;
                SetRect(RT(more), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-50, 0), new Vector2(110, 110));
                var ic = FindOrCreate(more.transform, "Icon0926");
                Img(ic, Spr("r0926_i_more"), Ink, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
                var mBtn = Ensure<Button>(more);
                ResetListeners(mBtn);
                UnityEventTools.AddPersistentListener(mBtn.onClick, menu.TogglePopover);

                var bg = Find(chat.transform, "Background");
                var pop = FindOrCreate(bg.transform, "Popover0926");
                pop.transform.SetAsLastSibling();
                Img(pop, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 40f).raycastTarget = true;
                SetRect(RT(pop), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -200), new Vector2(520, 280));
                PopRow(pop.transform, "Report0926", 0, "r0926_i_flag", Danger, menu.Report,
                    new[] { "신고하기", "Report", "報告する", "举报", "Denunciar" });
                PopRow(pop.transform, "Block0926", 1, "r0926_i_block", Ink, menu.Block,
                    new[] { "차단하기", "Block", "ブロック", "屏蔽", "Bloquear" });
                pop.SetActive(false);

                var mso = new SerializedObject(menu);
                mso.FindProperty("manager").objectReferenceValue = FindInScene<MessagePanelManager>(chat);
                mso.FindProperty("popover").objectReferenceValue = pop;
                mso.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("messages ok");
        }

        private static T FindInScene<T>(GameObject anyInScene) where T : Component
        {
            foreach (var r in anyInScene.scene.GetRootGameObjects())
            {
                var c = r.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static void PopRow(Transform pop, string name, int index, string icon, Color color, UnityAction action, string[] words)
        {
            const float h = 140f;
            var row = FindOrCreate(pop, name);
            Img(row, null, new Color(1, 1, 1, 0), Image.Type.Simple).raycastTarget = true;
            SetRect(RT(row), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -h * index), new Vector2(0, h));
            if (index > 0)
            {
                var line = FindOrCreate(row.transform, "Line0926");
                Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-60, 2));
            }
            var ic = FindOrCreate(row.transform, "Icon0926");
            Img(ic, Spr(icon), color, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(ic), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(76, 0), new Vector2(54, 54));
            var lb = FindOrCreate(row.transform, "Label0926");
            Txt(lb, words[0], 42, color, TextAnchor.MiddleLeft, FontStyle.Bold);
            var lrt = RT(lb); lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(130, 0); lrt.offsetMax = new Vector2(-30, 0);
            Loc(lb, words[0], words[1], words[2], words[3], words[4]);
            var b = Ensure<Button>(row);
            ResetListeners(b);
            UnityEventTools.AddPersistentListener(b.onClick, action);
        }

        // ============================================================
        // 프로필 — 카드 · 버튼 모양 통일, 로그아웃 옆 '계정 삭제'
        // ============================================================
        private static void ApplyProfile(Transform root, List<string> log)
        {
            var panel = Find(root, "FullProfilePanel");
            if (panel == null) { log.Add("FullProfilePanel 없음"); return; }
            var pImg = panel.GetComponent<Image>();
            if (pImg != null) pImg.color = new Color(0, 0, 0, 0.35f);
            var content = Find(panel.transform, "Content");
            if (content != null)
            {
                Img(content, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius);
                // 0930: 프로필은 아래에서 올라오지 않고 제자리에서 옅게 나타난다 (R0926FadePanel — Apply9)
                var slide = content.GetComponent<R0926SlideIn>();
                if (slide != null) Object.DestroyImmediate(slide);

                // 색 테마 버튼은 뺀다 — 테마가 칠하던 상단 띠·로고가 0926 에는 없다 (ApplyTop 참고)
                var theme = content.transform.Find("Theme0926");
                if (theme != null) Object.DestroyImmediate(theme.gameObject);
            }

            var close = content != null ? content.transform.Find("CloseButton") : null;
            if (close != null)
            {
                Img(close.gameObject, Spr("r0926_circle"), new Color(1, 1, 1, 0.1f), Image.Type.Simple).raycastTarget = true;
                RT(close.gameObject).sizeDelta = new Vector2(110, 110);
                foreach (var t in close.GetComponentsInChildren<Text>(true)) t.enabled = false;
                var ic = FindOrCreate(close, "Icon0926");
                Img(ic, Spr("r0926_i_close"), Soft, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            }

            foreach (var n in new[] { "FollowingBtn", "FollowersBtn" })
            {
                var b = Find(panel.transform, n);
                if (b == null) continue;
                var img = b.GetComponent<Image>();
                img.sprite = null; img.color = new Color(1, 1, 1, 0);
                foreach (var t in b.GetComponentsInChildren<Text>(true)) { t.color = Ink; t.fontSize = Mathf.Max(t.fontSize, 44); }
            }
            var follow = Find(panel.transform, "FollowButton");
            if (follow != null) { PillButton(follow, true); RT(follow).sizeDelta = new Vector2(560, 120); }
            var followed = Find(panel.transform, "FollowedButton");
            if (followed != null) { PillButton(followed, false); RT(followed).sizeDelta = new Vector2(560, 120); }
            var edit = Find(panel.transform, "EditProfileButton");
            if (edit != null) { PillButton(edit, false); RT(edit).sizeDelta = new Vector2(720, 120); }

            var logout = Find(panel.transform, "LogoutButton");
            if (logout != null)
            {
                var img = logout.GetComponent<Image>();
                img.sprite = null; img.color = new Color(1, 1, 1, 0);
                foreach (var t in logout.GetComponentsInChildren<Text>(true))
                    if (t.name != "Label0926") { t.color = Soft; t.fontSize = 40; t.fontStyle = FontStyle.Bold; }
                RT(logout).anchoredPosition = new Vector2(0, RT(logout).anchoredPosition.y);

                // 0930: 계정 삭제는 카드에서 뺀다 — 프로필 편집(웹)에 있다
                var del = logout.transform.Find("DeleteAccount0926");
                if (del != null) Object.DestroyImmediate(del.gameObject);
            }
            log.Add("profile ok");
        }

        /// <summary>켜질 때 아래에서 올라오게 (모든 카드·시트·대화상자 공통)</summary>
        private static void Slide(GameObject go, float distance)
        {
            var s = Ensure<R0926SlideIn>(go);
            var so = new SerializedObject(s);
            so.FindProperty("distance").floatValue = distance;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>중첩 캔버스로 정렬 순서만 올린다(클릭도 받도록 레이캐스터 포함).</summary>
        private static void OnTop(GameObject go)
        {
            // 루트 캔버스가 3333 — 그보다 위. 비활성 오브젝트에선 overrideSorting 세터가 무시돼 직렬화 값으로 넣는다
            var c = Ensure<Canvas>(go);
            var so = new SerializedObject(c);
            so.FindProperty("m_OverrideSorting").boolValue = true;
            so.FindProperty("m_SortingOrder").intValue = 4000;
            so.ApplyModifiedPropertiesWithoutUndo();
            Ensure<GraphicRaycaster>(go);
        }

        // ============================================================
        // 올리기 — 카드·입력창·등록 버튼만 가볍게 (좌표 배치·현지화·입력 미러는 그대로)
        // ============================================================
        private static void ApplyUpload(Transform root, List<string> log)
        {
            var page = Find(root, "UploadPage");
            if (page == null) { log.Add("UploadPage 없음"); return; }
            var pImg = page.GetComponent<Image>();
            if (pImg != null) pImg.color = new Color(0, 0, 0, 0.35f);

            foreach (var name in new[] { "CubeUploadPage", "ModelUploadPage" })
            {
                var card = page.transform.Find(name) ?? page.transform.Find("UploadSheet0926/" + name);
                if (card == null) continue;
                Img(card.gameObject, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius);

                var title = card.Find("TITLE");
                if (title != null) { var t = title.GetComponent<Text>(); t.fontSize = 72; t.fontStyle = FontStyle.Bold; t.color = Ink; EditorUtility.SetDirty(t); }

                var panel = card.Find("Panel");
                if (panel == null) continue;
                foreach (var n in new[] { "Name", "Coordinate", "Add_Logo", "Add_Imgs", "Add_File" })
                {
                    var l = panel.Find(n);
                    if (l == null) continue;
                    var t = l.GetComponent<Text>();
                    t.fontSize = 56; t.fontStyle = FontStyle.Bold; t.color = Ink;
                    EditorUtility.SetDirty(t);
                }
                var input = panel.Find("NameInput");
                if (input != null)
                {
                    Img(input.gameObject, Spr("r0926_pill"), new Color(0.09f, 0.106f, 0.118f, 1f), Image.Type.Sliced, 64f / 28f);
                    var field = input.GetComponent<InputField>();
                    if (field != null)
                    {
                        if (field.textComponent != null) { field.textComponent.color = Ink; EditorUtility.SetDirty(field.textComponent); }
                        if (field.placeholder is Text ph) { ph.color = Muted; EditorUtility.SetDirty(ph); }
                    }
                }
                var submit = panel.Find("SubmitButton");
                if (submit != null)
                {
                    PillButton(submit.gameObject, true);
                    RT(submit.gameObject).sizeDelta = new Vector2(760, 132);
                    ButtonLabel(submit.gameObject, new[] { "등록하기", "Submit", "登録する", "提交", "Enviar" }, Dark);
                }
            }

            // 닫기 X — 도크 '추가' 칸에, 추가 버튼과 같은 흰 타일 모양으로. + 는 누르면 스스로 숨는다
            var x = Find(page.transform, "XButton_Upload");
            if (x != null)
            {
                var mirror = FindOrCreate(page.transform, "DockMirror0926");
                mirror.transform.SetAsLastSibling();
                SetRect(RT(mirror), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, DockBottom), new Vector2(-Side * 2, DockHeight));
                Ensure<R0926SafeInset>(mirror).SetEdge(R0926SafeInset.Edge.Bottom);
                // v3 에서 만든 흰 원 아이콘 정리
                var oldIcon = x.transform.Find("Icon0926");
                if (oldIcon != null && oldIcon.Find("Glyph0926") == null) Object.DestroyImmediate(oldIcon.gameObject);
                AddButtonStyle(x, mirror.transform, SlotAdd, "r0926_i_close", new[] { "닫기", "Close", "閉じる", "关闭", "Cerrar" });
            }
            log.Add("upload ok");
        }

        // ── 영구 리스너 헬퍼 (인스펙터 OnClick 에 보이게) ─────────────────
        private static void ResetListeners(Button b)
        {
            for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(b.onClick, i);
            EditorUtility.SetDirty(b);
        }

        private static void AddSetActive(Button b, GameObject target, bool value)
        {
            UnityEventTools.AddBoolPersistentListener(b.onClick, target.SetActive, value);
            EditorUtility.SetDirty(b);
        }
    }
}
