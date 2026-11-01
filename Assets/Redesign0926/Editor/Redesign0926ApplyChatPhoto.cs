using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v64: 10-30 대표님 「메세지창 앞부분에 +버튼 하나 만들어서 사진 공유 · 사진 클릭해서 볼 수 있도록 · 본 앱 레이아웃에 어울리는 다운로드, 공유버튼 ·
    /// 공유하는 창은 '팔로잉' 하고 있는 친구에게」.
    ///  · 입력줄 왼쪽 + 버튼(PlusButton0926) — 보내기 버튼과 같은 크기의 둥근 칸. 사진 고르기 시트(촬영 · 앨범 · 취소)를 그대로 쓴다
    ///  · 크게 보기(ChatPhotoView0926) — 어두운 바탕 + 아래 한 줄 큰 세 칸 '닫기 / 다운로드 / 공유' (자르기 화면과 같은 카드 · 칸 · 물방울 소리)
    ///  · 공유 시트(ShareSheet0926) — 신고 시트와 같은 모양, 내용은 팔로잉 목록 + 줄마다 '보내기'
    /// Apply8(끌어 닫기 · 뒤로가기)보다 뒤에 돌므로 그 단계의 일을 여기서 직접 부른다
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float ChatPlusLeft = 40f, ChatPlusSize = 136f, ChatInputLeft = 196f;   // R0926ChatPhoto 와 같은 값
        private const float PhotoShareHead = 150f, PhotoShareRowH = 168f;                              // R0926PhotoShare 와 같은 값
        private static readonly string[] CloseTileWords = { "닫기", "Close", "閉じる", "关闭", "Cerrar" };

        private static void ApplyChatPhoto(Transform root, List<string> log)
        {
            var chat = FindInScene<MessagePanelManager>(root.gameObject);
            var room = root.Find("ChatRoomPanel");
            var area = root.Find("ChatRoomPanel/Clip0926/Background/InputArea") ?? root.Find("ChatRoomPanel/Background/InputArea");
            var input = area != null ? area.Find("ChatInput") : null;
            if (chat == null || area == null || input == null) { log.Add("대화방 사진: 입력줄 없음"); return; }

            var view = BuildPhotoView(root, room.gameObject, log);
            var share = BuildPhotoShare(root, view.transform, chat, log);
            var vso = new SerializedObject(view);
            vso.FindProperty("share").objectReferenceValue = share;
            vso.ApplyModifiedPropertiesWithoutUndo();

            var plus = BuildChatPlus(root, area, input as RectTransform, chat);
            var cso = new SerializedObject(chat);
            cso.FindProperty("chatPhoto").objectReferenceValue = plus;
            cso.FindProperty("photoView").objectReferenceValue = view;
            cso.ApplyModifiedPropertiesWithoutUndo();

            view.gameObject.SetActive(false);
            log.Add("chat photo ok");
        }

        // ── 입력줄 + 버튼 ─────────────────────────────────────
        private static R0926ChatPhoto BuildChatPlus(Transform root, Transform area, RectTransform input, MessagePanelManager chat)
        {
            var plus = FindOrCreate(area, "PlusButton0926");
            var left = new Vector2(0, 0.5f);
            SetRect(RT(plus), left, left, left, new Vector2(ChatPlusLeft, 0), new Vector2(ChatPlusSize, ChatPlusSize));
            var bg = Img(plus, Spr("r0926_circle"), SendOff, Image.Type.Simple);
            bg.raycastTarget = true;
            bg.preserveAspect = true;
            var b = Ensure<Button>(plus);
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var ic = FindOrCreate(plus.transform, "Icon0926");
            var center = new Vector2(0.5f, 0.5f);
            Img(ic, Spr("r0926_i_plus"), Ink, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(ic), center, center, center, Vector2.zero, new Vector2(64, 64));

            input.offsetMin = new Vector2(ChatInputLeft, input.offsetMin.y);
            EditorUtility.SetDirty(input);

            var logic = Ensure<R0926ChatPhoto>(plus);
            ResetListeners(b);
            UnityEventTools.AddPersistentListener(b.onClick, logic.OnPlus);
            EditorUtility.SetDirty(b);

            var dialog = Find(root, "PhotoSourceDialog");
            var so = new SerializedObject(logic);
            so.FindProperty("chat").objectReferenceValue = chat;
            so.FindProperty("sourceDialog").objectReferenceValue = dialog != null ? dialog.GetComponent<PhotoSourceDialog>() : null;
            so.FindProperty("input").objectReferenceValue = input;
            so.ApplyModifiedPropertiesWithoutUndo();
            return logic;
        }

        // ── 크게 보기 ─────────────────────────────────────────
        private static R0926PhotoView BuildPhotoView(Transform root, GameObject room, List<string> log)
        {
            var go = FindOrCreate(root, "ChatPhotoView0926");
            go.SetActive(true);
            Stretch(RT(go));
            var cv = Ensure<Canvas>(go);
            var cvso = new SerializedObject(cv);   // 꺼진 오브젝트엔 overrideSorting 대입이 안 먹는다 — 직렬화 값으로
            cvso.FindProperty("m_OverrideSorting").boolValue = true;
            cvso.FindProperty("m_SortingOrder").intValue = 9200;   // 창·도크 위, 토스트(9500) 아래
            cvso.ApplyModifiedPropertiesWithoutUndo();
            Ensure<GraphicRaycaster>(go);

            // 바탕이 손가락을 받는다 → 위의 R0926PhotoView 가 끌기·두 번 톡·휠을 처리
            var bg = FindOrCreate(go.transform, "Bg0926");
            Stretch(RT(bg));
            var backdrop = Img(bg, null, CropBg, Image.Type.Simple);
            backdrop.raycastTarget = true;

            var center = new Vector2(0.5f, 0.5f);
            var frame = FindOrCreate(go.transform, "Stage0926");
            SetRect(RT(frame), Vector2.zero, Vector2.zero, center, new Vector2(720, 1800), new Vector2(1440, 2700));
            var ph = FindOrCreate(frame.transform, "Photo0926");
            var photo = Ensure<RawImage>(ph);
            photo.color = Color.white;
            photo.raycastTarget = false;
            EditorUtility.SetDirty(photo);
            SetRect(RT(ph), center, center, center, Vector2.zero, new Vector2(1000, 1000));
            var spin = PillPart(frame.transform, "Spin0926", "r0926_arc", Pink2, 120, Image.Type.Simple);

            // 아래 — 한 줄 큰 세 칸 (자르기 화면과 같은 카드 · 칸)
            var bar = FindOrCreate(go.transform, "Bar0926");
            Img(bar, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(bar), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(-CardSide * 2, PhotoTileH + 48));
            var row = FindOrCreate(bar.transform, "Row0926");
            var rrt = RT(row);
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.pivot = center;
            rrt.offsetMin = new Vector2(24, 24); rrt.offsetMax = new Vector2(-24, -24);
            EditorUtility.SetDirty(rrt);

            var view = Ensure<R0926PhotoView>(go);
            var dockList = Find(root, "Dock0926")?.transform.Find("List_Button");
            var dockFw = dockList != null ? dockList.GetComponent<UITouchForwarder>() : null;
            var dockClip = dockFw != null ? new SerializedObject(dockFw).FindProperty("customSound").objectReferenceValue : null;
            if (dockClip == null) log.Add("사진 보기: 도크 버튼 소리 못 찾음 — 기본 소리");

            var quiet = Color.Lerp(Card, Color.white, 0.035f);
            var plain = Color.Lerp(Card, Color.white, 0.08f);
            var close = CropTile(row.transform, "Close0926", 0, "r0926_i_close", quiet, Muted, CloseTileWords, dockClip, view.Close);
            var down = CropTile(row.transform, "Download0926", 1, "r0926_i_download", plain, Ink, new[] { "다운로드", "Download", "保存", "下载", "Descargar" }, dockClip, view.Download);
            var share = CropTile(row.transform, "Share0926", 2, "r0926_i_share", Pink, Dark, new[] { "공유", "Share", "共有", "分享", "Compartir" }, dockClip, view.Share);
            Ensure<ClickButtonOnBack>(close.gameObject);   // 뒤로가기 = 닫기

            var so = new SerializedObject(view);
            so.FindProperty("frame").objectReferenceValue = RT(frame);
            so.FindProperty("photo").objectReferenceValue = photo;
            so.FindProperty("backdrop").objectReferenceValue = backdrop;
            so.FindProperty("bar").objectReferenceValue = RT(bar);
            SetArray(so.FindProperty("tiles"), new Object[] { close, down, share });
            so.FindProperty("spin").objectReferenceValue = RT(spin.gameObject);
            so.FindProperty("chatRoom").objectReferenceValue = room;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ── 공유 시트 (팔로잉 목록) ──────────────────────────────
        private static R0926PhotoShare BuildPhotoShare(Transform root, Transform view, MessagePanelManager chat, List<string> log)
        {
            var sheet = FindOrCreate(view, "ShareSheet0926");
            sheet.SetActive(true);
            sheet.transform.SetAsLastSibling();
            Img(sheet, null, new Color(0, 0, 0, 0.5f), Image.Type.Simple).raycastTarget = true;
            var center = new Vector2(0.5f, 0.5f);
            SetRect(RT(sheet), Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            var logic = Ensure<R0926PhotoShare>(sheet);

            const float bottom = 40f, cancelH = 150f, gap = 16f;
            var cancel = FindOrCreate(sheet.transform, "Cancel0926");
            Img(cancel, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(cancel), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(-CardSide * 2, cancelH));
            Ensure<R0926SafeInset>(cancel).SetEdge(R0926SafeInset.Edge.Bottom);
            var cBtn = Ensure<Button>(cancel);
            ButtonLabel(cancel, CloseTileWords, Ink);
            ResetListeners(cBtn);
            UnityEventTools.AddPersistentListener(cBtn.onClick, logic.Close);

            var card = FindOrCreate(sheet.transform, "Card0926");
            Img(card, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(card), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom + cancelH + gap), new Vector2(-CardSide * 2, PhotoShareHead + PhotoShareRowH * 2));
            Ensure<R0926SafeInset>(card).SetEdge(R0926SafeInset.Edge.Bottom);
            Slide(card, 260f);
            Slide(cancel, 260f);

            var title = FindOrCreate(card.transform, "Title0926");
            Txt(title, "팔로잉에게 공유", 40, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(title), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-80, PhotoShareHead));
            Loc(title, "팔로잉에게 공유", "Share with people you follow", "フォロー中の人に共有", "分享给关注的人", "Compartir con quienes sigues");

            // 목록 — 카드 높이는 친구 수에 맞춰 R0926PhotoShare 가 정한다
            var list = FindOrCreate(card.transform, "List0926");
            var lrt = RT(list);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.pivot = center;
            lrt.offsetMin = new Vector2(0, 24); lrt.offsetMax = new Vector2(0, -PhotoShareHead);
            EditorUtility.SetDirty(lrt);
            Ensure<RectMask2D>(list);
            var body = FindOrCreate(list.transform, "Content0926");
            var brt = RT(body);
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(brt);
            var vlg = Ensure<VerticalLayoutGroup>(body);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            EditorUtility.SetDirty(vlg);
            var csf = Ensure<ContentSizeFitter>(body);
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            EditorUtility.SetDirty(csf);
            var scroll = Ensure<ScrollRect>(list);
            scroll.horizontal = false; scroll.vertical = true;
            scroll.viewport = lrt; scroll.content = brt;
            EditorUtility.SetDirty(scroll);
            // 빈 자리도 끌리게 — RectMask2D 는 그림이 아니라서 투명한 바탕이 필요하다
            Img(list, null, new Color(1, 1, 1, 0), Image.Type.Simple).raycastTarget = true;

            var tpl = PhotoShareRow(body.transform);
            var st = FindOrCreate(list.transform, "Status0926");
            var status = Txt(st, "", 44, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            status.raycastTarget = false;
            var srt = RT(st);
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.pivot = center;
            srt.offsetMin = new Vector2(60, 0); srt.offsetMax = new Vector2(-60, 0);
            EditorUtility.SetDirty(srt);

            var so = new SerializedObject(logic);
            so.FindProperty("chat").objectReferenceValue = chat;
            so.FindProperty("card").objectReferenceValue = RT(card);
            so.FindProperty("content").objectReferenceValue = brt;
            so.FindProperty("rowTemplate").objectReferenceValue = tpl;
            so.FindProperty("status").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 바깥(어두운 바탕) 누르기 · 아래로 끌기 · 뒤로가기 = 닫기 — 신고 시트와 같은 길
            var tap = new SerializedObject(Ensure<R0926TapToClose>(sheet));
            tap.FindProperty("closeButton").objectReferenceValue = cBtn;
            tap.ApplyModifiedPropertiesWithoutUndo();
            BackCloses(cancel.transform, "사진 공유", log);
            string cardPath = AnimationUtility.CalculateTransformPath(card.transform, root);
            string cancelPath = AnimationUtility.CalculateTransformPath(cancel.transform, root);
            Swipe(root, cardPath, cancelPath, new[] { cancelPath }, log);
            Proxy(root, card.GetComponent<R0926SwipeDismiss>(), cancelPath, log);

            sheet.SetActive(false);
            return logic;
        }

        // 팔로잉 한 줄 — 사진 · 이름 · 보내기 (팔로우 목록 줄과 같은 크기). 꺼 둔 틀을 R0926PhotoShare 가 복사해 쓴다
        private static GameObject PhotoShareRow(Transform body)
        {
            var tpl = FindOrCreate(body, "RowTemplate0926");
            tpl.SetActive(true);
            var le = Ensure<LayoutElement>(tpl);
            le.minHeight = le.preferredHeight = PhotoShareRowH;
            EditorUtility.SetDirty(le);

            var mid = new Vector2(0, 0.5f);
            var av = FindOrCreate(tpl.transform, "Avatar0926");
            SetRect(RT(av), mid, mid, mid, new Vector2(40, 0), new Vector2(FollowAvatar, FollowAvatar));

            var send = FindOrCreate(tpl.transform, "Send0926");
            var pill = Img(send, Spr("r0926_pill"), Pink, Image.Type.Sliced, 64f / 46f);
            pill.raycastTarget = true;
            var right = new Vector2(1, 0.5f);
            SetRect(RT(send), right, right, right, new Vector2(-40, 0), new Vector2(FollowBtnW, FollowBtnH));
            var b = Ensure<Button>(send);
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            EditorUtility.SetDirty(b);
            var lb = FindOrCreate(send.transform, "Label0926");
            Txt(lb, "보내기", 40, Dark, TextAnchor.MiddleCenter, FontStyle.Bold).raycastTarget = false;
            var lbrt = RT(lb);
            lbrt.anchorMin = Vector2.zero; lbrt.anchorMax = Vector2.one;
            lbrt.offsetMin = Vector2.zero; lbrt.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(lbrt);

            var nm = FindOrCreate(tpl.transform, "Name0926");
            var name = Txt(nm, "", 44, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            name.raycastTarget = false;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            var nrt = RT(nm);
            nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1); nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.offsetMin = new Vector2(40 + FollowAvatar + 28, 30); nrt.offsetMax = new Vector2(-(40 + FollowBtnW + 24), -30);
            EditorUtility.SetDirty(nrt);

            tpl.SetActive(false);
            return tpl;
        }
    }
}
