using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v12: 창이 뜨고 닫히는 길 정리.
    ///  · 아래로 끌어 닫기(R0926SwipeDismiss) — 목록·메시지·대화방·프로필·AI·신고·더보기·팔로우·춤·사진 고르기
    ///  · 안드로이드 뒤로가기(ClickButtonOnBack) — 뒤로가기로 안 닫히던 창들에 닫기 버튼 등록.
    ///    열린 창이 없으면 BackButtonHandler 가 앱을 뒤로 보낸다(moveTaskToBack)
    ///  · 대화방 도크 X — 정리(CloseChatRoom) 없이 끄고 자기 자신까지 꺼 두 번째부터 안 보이던 것 → 정리 후 메인으로
    ///  · 목록 버튼이 목록을 켜고·끄고·켜던 중복 호출, 대상이 사라진 버튼 호출 청소
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string FixPanel = "FullScreenPanel/FullScreenGuide/FixButtonPanel/";

        private static void ApplySheetClosing(Transform root, List<string> log)
        {
            // (움직이는 시트, 다 내렸을 때 누를 닫기 버튼, 함께 내려갈 형제)
            foreach (var d in Sheets) Swipe(root, d.sheet, d.swipeClose, d.followers, log);

            // 뒤로가기로 닫혀야 하는데 등록이 없던 닫기·취소 버튼
            foreach (var p in new[]
            {
                "AskAISheet0926/Cancel0926",
                "FullScreenPanel/ReportSheet0926/Cancel0926",
                FixPanel + "MoreCancel0926",
                "FollowPanel/Background/Header/BackButton",
                "FollowPanel/UnfollowConfirmDialog/DialogBox/ButtonContainer/CancelButton",
                "DanceAnimPanel/CancelButton",
                "ARPreviewPanel/MessageContainer/ButtonContainer/CancelButton",
                "RemoveRequestPanel/Box0926/Buttons0926/Remove_NO",
                "UpdateChecker/Box0926/Buttons0926/Update_NO",          // 강제 업데이트 땐 이 버튼이 숨겨져 등록되지 않는다
                "PhotoSourceDialog/DialogPanel/BottomContainer/ContentCard/ButtonRow/CancelButton",
                "ContinueCaptureDialog/DialogPanel/BottomContainer/ContentCard/ButtonRow/NoButton",   // 바깥을 눌렀을 때와 같은 '아니오'
                "Fixpage/XButton_Panel/XButton_FixUpload",
                "LocationPermissionPanel/Box/Buttons/Btn_Close",
                "FirstTimeGuidePanel/check",
            })
                BackCloses(root.Find(p), p, log);

            // 댓글 창 — 보이는 닫기 버튼이 없다(끌어내리기·바깥 누르기로 닫는다). 뒤로가기 등록용 보이지 않는 버튼을 단다.
            // 등록돼 있으면 CommentManager 는 자체 Esc 처리를 멈춘다 (둘 다 받으면 아래 사진 창까지 닫혔다)
            var cm = FindInScene<CommentManager>(root.gameObject);
            if (cm != null && cm.commentPanel != null)
            {
                var bc = FindOrCreate(cm.commentPanel.transform, "BackClose0926");
                RT(bc).sizeDelta = Vector2.zero;
                var btn = Ensure<Button>(bc);
                btn.transition = Selectable.Transition.None;
                ResetListeners(btn);
                UnityEventTools.AddPersistentListener(btn.onClick, cm.ClosePanel);
                BackCloses(bc.transform, "댓글", log);
            }
            else log.Add("뒤로가기: 댓글 창 없음");

            // 끌어내릴 때 도크의 X(DockMirror)가 시트 위로 비치지 않게 — 시트보다 아래(먼저) 그린다. 평소엔 둘이 겹치지 않는다
            foreach (var (panel, sheet) in new[] { ("ListPanel", "Sheet0926"), ("MessagePanel", "Background"), ("ChatRoomPanel", "Background") })
            {
                var mirror = root.Find(panel + "/DockMirror0926");
                var s = root.Find(panel + "/" + sheet);
                if (mirror != null && s != null && mirror.GetSiblingIndex() > s.GetSiblingIndex())
                    mirror.SetSiblingIndex(s.GetSiblingIndex());
            }

            // '주변에서 N개 발견' 안내는 AR 화면 위 안내 — 목록·사진 등 창이 열리면 그 아래로 (인디케이터 바로 위)
            var count = root.Find("ObjectCountUI ");
            var ind = root.Find("OffScreenIndicator Panel");
            if (count != null && ind != null && count.GetSiblingIndex() != ind.GetSiblingIndex() + 1)
            {
                count.SetSiblingIndex(ind.GetSiblingIndex() + 1);
                log.Add("개수 안내를 창들 아래로");
            }

            FixChatClose(root, log);
            DedupeListButton(root, log);
            RemoveDeadCalls(root, log);
            RemoveStrayOverlay(root, log);

            // 로그인 시험용 딥링크에 실제 토큰(JWT)이 저장돼 있었다 — 공개 저장소에 올라가지 않게 비운다 (0529 에서 복사돼 온 값)
            var lm = FindInScene<LoginManager>(root.gameObject);
            if (lm != null)
            {
                var lso = new SerializedObject(lm);
                var dl = lso.FindProperty("testDeepLinkUrl");
                if (dl != null && dl.stringValue.Contains("token=ey"))
                {
                    dl.stringValue = "";
                    lso.ApplyModifiedPropertiesWithoutUndo();
                    log.Add("LoginManager 시험 딥링크의 토큰 비움");
                }
            }
            // 닫기 버튼·뒤로가기·바깥 누르기도 아래로 미끄러져 사라지게 — 뒤로가기 등록을 다 옮긴 뒤 맨 마지막에
            foreach (var d in Sheets)
            {
                var sd = root.Find(d.sheet)?.GetComponent<R0926SwipeDismiss>();
                if (sd == null) continue;
                foreach (var b in d.buttons) Proxy(root, sd, b, log);
            }
            log.Add("sheet closing ok");
        }

        private struct SheetDef { public string sheet, swipeClose; public string[] buttons, followers; }
        private static SheetDef S(string sheet, string swipeClose, string[] buttons, string[] followers = null)
            => new SheetDef { sheet = sheet, swipeClose = swipeClose, buttons = buttons, followers = followers };

        private static readonly SheetDef[] Sheets =
        {
            S("ListPanel/Sheet0926", "ListPanel/DockMirror0926/XButton_List", new[] { "ListPanel/DockMirror0926/XButton_List" }),
            S("MessagePanel/Background", "MessagePanel/DockMirror0926/CloseButton", new[] { "MessagePanel/DockMirror0926/CloseButton" }),
            // ← 와 같게: 목록으로. 도크 X 는 정리 후 메인으로 (둘 다 내려간 뒤)
            S("ChatRoomPanel/Background", "ChatRoomPanel/Background/Header/BackButton",
              new[] { "ChatRoomPanel/Background/Header/BackButton", "ChatRoomPanel/DockMirror0926/CloseButton" }),
            S("FullProfilePanel/Content", "FullProfilePanel/Content/CloseButton", new[] { "FullProfilePanel/Content/CloseButton" }),
            S("AskAISheet0926/Card0926", "AskAISheet0926/Cancel0926", new[] { "AskAISheet0926/Cancel0926" }, new[] { "AskAISheet0926/Cancel0926" }),
            S("FullScreenPanel/ReportSheet0926/Card0926", "FullScreenPanel/ReportSheet0926/Cancel0926",
              new[] { "FullScreenPanel/ReportSheet0926/Cancel0926" }, new[] { "FullScreenPanel/ReportSheet0926/Cancel0926" }),
            S(FixPanel + "MoreSheet0926", FixPanel + "MoreCancel0926",
              new[] { FixPanel + "MoreCancel0926", "FullScreenPanel/FullScreenGuide/TopUIPanel/FixButton_Opposite" }, new[] { FixPanel + "MoreCancel0926" }),
            S("FollowPanel/Background", "FollowPanel/Background/Header/BackButton", new[] { "FollowPanel/Background/Header/BackButton" }),
            S("DanceAnimPanel", "DanceAnimPanel/CancelButton", new[] { "DanceAnimPanel/CancelButton" }),
            S("PhotoSourceDialog/DialogPanel/BottomContainer", "PhotoSourceDialog/DialogPanel/BottomContainer/ContentCard/ButtonRow/CancelButton",
              new[] { "PhotoSourceDialog/DialogPanel/BottomContainer/ContentCard/ButtonRow/CancelButton" }),
        };

        // 원래 닫기 버튼 위에 투명한 누름 영역을 덮는다 — 누르면 시트가 내려간 뒤 원래 버튼을 누른다.
        // 뒤로가기 등록(ClickButtonOnBack)과 바깥 누르기(R0926TapToClose)도 이 덮개로 옮긴다
        private static void Proxy(Transform root, R0926SwipeDismiss sd, string realPath, List<string> log)
        {
            var t = root.Find(realPath);
            var real = t != null ? t.GetComponent<Button>() : null;
            if (real == null) { log.Add("닫기 덮개: 버튼 없음 " + realPath); return; }
            var px = FindOrCreate(t, "ClosePx0926");
            px.transform.SetAsLastSibling();
            var prt = RT(px);
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            prt.localScale = Vector3.one;
            var img = Ensure<Image>(px);
            img.sprite = null; img.color = new Color(1f, 1f, 1f, 0f); img.raycastTarget = true;
            var b = Ensure<Button>(px);
            b.transition = real.transition; b.colors = real.colors; b.targetGraphic = real.targetGraphic;
            var cp = Ensure<R0926CloseProxy>(px);
            var so = new SerializedObject(cp);
            so.FindProperty("sheet").objectReferenceValue = sd;
            so.FindProperty("real").objectReferenceValue = real;
            so.ApplyModifiedPropertiesWithoutUndo();
            ResetListeners(b);
            UnityEventTools.AddPersistentListener(b.onClick, cp.Run);

            var back = t.GetComponent<ClickButtonOnBack>();
            if (back != null) { Object.DestroyImmediate(back); Ensure<ClickButtonOnBack>(px); }
            foreach (var tap in root.GetComponentsInChildren<R0926TapToClose>(true))
            {
                var tso = new SerializedObject(tap);
                var cb = tso.FindProperty("closeButton");
                if (cb.objectReferenceValue == real) { cb.objectReferenceValue = b; tso.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        // Canvas 맨 아래의 꺼진 4444x4444 반투명 분홍 이미지 — 아무 데서도 참조·이름 검색이 없는 옛 시험용 덮개
        private static void RemoveStrayOverlay(Transform root, List<string> log)
        {
            var t = root.Find("Image");
            if (t == null || t.gameObject.activeSelf || t.childCount > 0) return;
            var rt = t as RectTransform;
            if (rt == null || rt.sizeDelta.x < 4000f || t.GetComponents<Component>().Length > 3) return;   // RectTransform·CanvasRenderer·Image 뿐일 때만
            Object.DestroyImmediate(t.gameObject);
            log.Add("안 쓰는 덮개 이미지(Canvas/Image) 삭제");
        }

        private static void Swipe(Transform root, string sheetPath, string closePath, string[] followerPaths, List<string> log)
        {
            var sheet = root.Find(sheetPath);
            var close = root.Find(closePath);
            var btn = close != null ? close.GetComponent<Button>() : null;
            if (sheet == null || btn == null) { log.Add("끌어 닫기: 없음 " + (sheet == null ? sheetPath : closePath)); return; }
            var sd = Ensure<R0926SwipeDismiss>(sheet.gameObject);
            var so = new SerializedObject(sd);
            so.FindProperty("closeButton").objectReferenceValue = btn;
            var fol = so.FindProperty("followers");
            fol.arraySize = followerPaths != null ? followerPaths.Length : 0;
            for (int i = 0; i < fol.arraySize; i++) fol.GetArrayElementAtIndex(i).objectReferenceValue = root.Find(followerPaths[i]) as RectTransform;
            // 창 뒤 어두운 바탕 — 시트의 부모가 반투명 그늘일 때만 (내려가는 만큼 옅어진다)
            Graphic dim = null;
            if (sheet.parent != root)
            {
                var pimg = sheet.parent.GetComponent<Image>();
                if (pimg != null && pimg.color.a > 0.05f && pimg.color.a < 0.9f) dim = pimg;
            }
            so.FindProperty("backdrop").objectReferenceValue = dim;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BackCloses(Transform t, string label, List<string> log)
        {
            if (t == null || t.GetComponent<Button>() == null) { log.Add("뒤로가기: 버튼 없음 " + label); return; }
            if (t.GetComponent<ClickButtonOnBack>() == null) t.gameObject.AddComponent<ClickButtonOnBack>();
        }

        private static void FixChatClose(Transform root, List<string> log)
        {
            var chat = root.Find("ChatRoomPanel");
            var x = root.Find("ChatRoomPanel/DockMirror0926/CloseButton");
            var msgX = root.Find("MessagePanel/DockMirror0926/CloseButton");
            var menu = chat != null ? chat.GetComponent<R0926ChatMenu>() : null;
            var b = x != null ? x.GetComponent<Button>() : null;
            if (menu == null || b == null || msgX == null) { log.Add("대화방 X: 대상 없음"); return; }

            ResetListeners(b);
            UnityEventTools.AddPersistentListener(b.onClick, menu.CloseToMain);
            // 뒤로가기는 ← (Header/BackButton → CloseChatRoom → 메시지 목록) 가 맡는다. X 까지 등록돼 있으면 X 가 먼저 불렸다
            var back = x.GetComponent<ClickButtonOnBack>();
            if (back != null) Object.DestroyImmediate(back);
            var mso = new SerializedObject(menu);
            mso.FindProperty("messageClose").objectReferenceValue = msgX.GetComponent<Button>();
            mso.ApplyModifiedPropertiesWithoutUndo();
        }

        // 목록 버튼: ListPanel 켜기 → 끄기 → 켜기 (열 때마다 목록을 두 번 새로 그렸다) → 한 번만 켠다
        private static void DedupeListButton(Transform root, List<string> log)
        {
            var btnGo = root.Find("Dock0926/List_Button");
            var list = root.Find("ListPanel");
            var b = btnGo != null ? btnGo.GetComponent<Button>() : null;
            if (b == null || list == null) return;
            bool seen = false;
            int removed = 0;
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            {
                if (b.onClick.GetPersistentTarget(i) != list.gameObject || b.onClick.GetPersistentMethodName(i) != "SetActive") continue;
                if (!seen) { seen = true; continue; }
                UnityEventTools.RemovePersistentListener(b.onClick, i);
                i--;
                removed++;
            }
            if (removed > 0) { EditorUtility.SetDirty(b); log.Add("목록 버튼 중복 호출 " + removed + "개 제거"); }
        }

        // 대상 오브젝트가 지워져 아무것도 안 하는 버튼 호출 (예: 대화방 ← 의 SetActive(없음))
        private static void RemoveDeadCalls(Transform root, List<string> log)
        {
            var names = new List<string>();
            foreach (var r in root.gameObject.scene.GetRootGameObjects())
                foreach (var b in r.GetComponentsInChildren<Button>(true))
                    for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                        if (b.onClick.GetPersistentTarget(i) == null)
                        {
                            UnityEventTools.RemovePersistentListener(b.onClick, i);
                            EditorUtility.SetDirty(b);
                            names.Add(b.name);
                        }
            if (names.Count > 0) log.Add("빈 버튼 호출 " + names.Count + "개 제거: " + string.Join(", ", names));
        }
    }
}
