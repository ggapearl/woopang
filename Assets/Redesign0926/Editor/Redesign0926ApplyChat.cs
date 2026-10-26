using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 대화창 — 말풍선·날짜·불러오는 중 뼈대·제목줄을 0926 모양으로 (2026-10-10 대표님: "대화창 레이아웃이 예전 그대로").
    /// 말풍선 모양은 프리팹(Assets/Prefabs/DM)이 정하고 MessagePanelManager 는 글·폭만 넣는다 → 프리팹을 고친다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static readonly Color MineBubble = new Color(0.84f, 0.25f, 0.44f, 1f);   // #D6406F — 흰 글이 읽히게 브랜드 분홍보다 한 톤 깊게
        private static readonly Color DateBg = new Color(0.118f, 0.133f, 0.149f, 1f);    // #1E2226
        private const string DmDir = "Assets/Prefabs/DM/";
        private const float BubbleRadius = 46f;
        private const int BubbleFont = 48;
        private const float ChatAvatar = 88f;
        private const float ChatHeaderEdge = 30f, ChatHeaderHit = 132f;

        private static void ApplyChatLook(Transform root, List<string> log)
        {
            EditPrefab("MyMessageBubble", go => ChatBubble(go, true, false), log);
            EditPrefab("OtherMessageBubble", go => ChatBubble(go, false, false), log);
            EditPrefab("AdminMessageBubble", go => ChatBubble(go, false, true), log);
            EditPrefab("DateSeparator", DateChip, log);
            EditPrefab("ChatSkeletonLoading", ChatSkeleton, log);

            MessageListArea(root, log);
            var chat = Find(root, "ChatRoomPanel");
            if (chat == null) { log.Add("대화창 없음"); return; }
            ChatHeader(chat.transform, log);
            ChatArea(chat.transform, log);

            // 말풍선 사이 — 예전 40 은 한 사람이 이어 보낸 말도 뚝뚝 떨어져 보였다
            var area = Find(chat.transform, "MessageArea");
            var content = area != null ? Find(area.transform, "Content") : null;
            var vlg = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
            if (vlg != null)
            {
                vlg.spacing = 14f;
                vlg.padding = new RectOffset(0, 0, 8, 28);
                EditorUtility.SetDirty(vlg);
            }
            else log.Add("대화창: 말풍선 목록 없음");
            ChatSound(root, log);
            log.Add("chat look ok");
        }

        // 메세지 주고받는 소리 — 대표님 10-13 「너무 크다, 지금의 80%」. 받는 소리 0.2 → 0.16,
        // 보내기는 다른 버튼과 같은 누름 소리(0.2)라 그 버튼에만 0.8배를 건다 (다른 버튼 소리는 그대로)
        private const float ChatReceiveVolume = 0.16f, ChatSendVolumeScale = 0.8f;
        private static void ChatSound(Transform root, List<string> log)
        {
            var mpm = FindInScene<MessagePanelManager>(root.gameObject);
            if (mpm == null) { log.Add("대화 소리: MessagePanelManager 없음"); return; }
            var so = new SerializedObject(mpm);
            so.FindProperty("chatReceiveSfxVolume").floatValue = ChatReceiveVolume;
            so.ApplyModifiedPropertiesWithoutUndo();
            var fw = mpm.sendButton != null ? mpm.sendButton.GetComponent<UITouchForwarder>() : null;
            if (fw == null) { log.Add("대화 소리: 보내기 버튼 누름 소리 없음"); return; }
            var fso = new SerializedObject(fw);
            fso.FindProperty("volumeScale").floatValue = ChatSendVolumeScale;
            fso.ApplyModifiedPropertiesWithoutUndo();
        }

        // 말풍선 칸 — 제목줄(창 위 50~200) 아래 · 입력칸(창 아래 0~176 — 10-25 에 150→176) 위에 고정 여백으로 (2026-10-18 대표님: 가로에서 말풍선이 제목·입력칸을 가림).
        // 예전엔 창 높이의 10~90% 비율이라 창이 낮은 가로(1339)에선 위아래로 삐져나가 제목줄·입력칸을 덮었다 — 세로도 짧은 폰은 제목줄에 닿았다.
        // 제목줄·입력칸은 창 끝에 붙어 다니므로 같은 여백이면 세로·가로·키보드 어느 높이에서도 그 사이에 든다 (입력칸이 커지면 AutoExpandInputField 가 아래 여백을 늘린다)
        private const float ChatAreaSide = 75f, ChatAreaTop = 216f, ChatAreaBottom = 220f;
        private static void ChatArea(Transform chat, List<string> log)
        {
            var area = Find(chat, "MessageArea");
            var vp = area != null ? area.transform.Find("Viewport") : null;
            if (vp == null) { log.Add("대화창: 말풍선 칸 없음"); return; }
            SetRect(RT(area), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RT(area).offsetMin = new Vector2(ChatAreaSide, ChatAreaBottom);
            RT(area).offsetMax = new Vector2(-ChatAreaSide, -ChatAreaTop);
            SetRect(RT(vp.gameObject), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            // 키보드가 떠도 제목줄·입력칸은 창 끝에 붙어 같이 움직이므로 말풍선 칸 여백을 따로 줄이지 않는다
            var mkh = chat.GetComponent<MobileKeyboardHandler>();
            if (mkh != null)
            {
                var so = new SerializedObject(mkh);
                so.FindProperty("expandedViewportTop").floatValue = 0f;
                so.FindProperty("expandedViewportBottom").floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 메시지 목록 — 검색칸(창 위 210~360) 아래에 고정 여백으로. 비율이던 예전 자리는 가로에서 목록 윗줄이 검색칸을 86 덮었다
        // (세로 값은 대표님 아이폰에서 보이던 그대로: 위 384 · 아래 67 · 양옆 19)
        private static void MessageListArea(Transform root, List<string> log)
        {
            var list = root.Find("MessagePanel/Clip0926/Background/ConversationList");
            if (list == null) { log.Add("메시지 목록 칸 없음"); return; }
            SetRect(RT(list.gameObject), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RT(list.gameObject).offsetMin = new Vector2(19f, 67f);
            RT(list.gameObject).offsetMax = new Vector2(-19f, -384f);

            // 목록 자르기 — 옛 둥근 카드 그림(Square_Sero_white)을 마스크로 써서 맨 위·맨 아래 줄 '삭제'의 오른쪽 모서리가
            // 둥글게 깎였다 (2026-10-17 대표님: 기존 레이아웃의 잔재) → 반듯한 사각 자르기. 바탕 그림은 줄 사이 끌기만 받는다
            var vp = list.Find("Viewport");
            if (vp == null) { log.Add("메시지 목록: Viewport 없음"); return; }
            var mask = vp.GetComponent<Mask>();
            if (mask != null) UnityEngine.Object.DestroyImmediate(mask);
            Ensure<RectMask2D>(vp.gameObject);
            var img = vp.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = null;
                img.color = new Color(0, 0, 0, 0);
                img.raycastTarget = true;
                EditorUtility.SetDirty(img);
            }
        }

        private static void EditPrefab(string name, Action<GameObject> edit, List<string> log)
        {
            string path = DmDir + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add("대화창: 프리팹 없음 " + name); return; }
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        // 내 말: 분홍 · 상대·우팡: 카드색 — 둥근 모서리, 흰 글. 사진·하트는 말풍선 아래 끝에 맞춘다
        private static void ChatBubble(GameObject row, bool mine, bool admin)
        {
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.padding = new RectOffset(16, 16, 4, 4);
                hlg.spacing = 18f;
                hlg.childAlignment = mine ? TextAnchor.LowerRight : TextAnchor.LowerLeft;
                EditorUtility.SetDirty(hlg);
            }
            var box = row.transform.Find("BubbleContainer");
            if (box != null)
            {
                Img(box.gameObject, Spr("r0926_pill"), mine ? MineBubble : Card, Image.Type.Sliced, 63f / BubbleRadius).raycastTarget = true;   // 두 번 눌러 좋아요
                var vlg = box.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.padding = new RectOffset(36, 36, 24, 26);
                    EditorUtility.SetDirty(vlg);
                }
                var ct = box.Find("ContentText");
                if (ct != null) Txt(ct.gameObject, null, BubbleFont, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            }
            var av = row.transform.Find("AvatarContainer");
            if (av != null)
            {
                var art = RT(av.gameObject);
                art.sizeDelta = new Vector2(ChatAvatar, ChatAvatar);
                var le = av.GetComponent<LayoutElement>();
                if (le != null) { le.preferredWidth = ChatAvatar; le.preferredHeight = ChatAvatar; EditorUtility.SetDirty(le); }
                EditorUtility.SetDirty(art);
                // 우팡 사자 — 예전 노란 물을 빼고 그림 그대로
                var face = admin ? av.Find("AvatarImage")?.GetComponent<Image>() : null;
                if (face != null) { face.color = Color.white; EditorUtility.SetDirty(face); }
            }
            // 좋아요 하트 — 예전엔 그림 없는 16px 분홍 네모가 말풍선 안에 있어서 코드(줄 바로 밑에서 찾는다)가 못 찾았다
            // → 좋아요한 말에 하트가 안 떴고, 두 번 누르면 줄 밑에 분홍 네모를 새로 만들었다. 줄 바로 밑으로 옮겨 말풍선 바깥쪽 아래에
            var heart = row.transform.Find("HeartIcon") ?? (box != null ? box.Find("HeartIcon") : null);
            if (heart != null)
            {
                heart.SetParent(row.transform, false);
                Img(heart.gameObject, Spr("r0926_re_heart"), Color.white, Image.Type.Simple).raycastTarget = true;   // 눌러 좋아요 취소
                var hle = Ensure<LayoutElement>(heart.gameObject);
                hle.preferredWidth = 44f; hle.preferredHeight = 44f;
                EditorUtility.SetDirty(hle);
                RT(heart.gameObject).sizeDelta = new Vector2(44f, 44f);
                // 내 말은 하트가 말풍선 왼쪽, 상대 말은 오른쪽
                if (mine) heart.SetAsFirstSibling();
                else heart.SetAsLastSibling();
            }
        }

        // 날짜 — 가운데 작은 알약 (DateText 를 알약 안으로 — 코드는 자식 Text 하나만 찾는다)
        private static void DateChip(GameObject sep)
        {
            var le = Ensure<LayoutElement>(sep);
            le.preferredHeight = 100f;
            EditorUtility.SetDirty(le);
            var pill = FindOrCreate(sep.transform, "Pill0926");
            var date = sep.transform.Find("DateText") ?? pill.transform.Find("DateText");
            Img(pill, Spr("r0926_pill"), DateBg, Image.Type.Sliced, 63f / 30f).raycastTarget = false;
            SetRect(RT(pill), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 60f));
            var h = Ensure<HorizontalLayoutGroup>(pill);
            h.padding = new RectOffset(30, 30, 0, 0);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            EditorUtility.SetDirty(h);
            var fit = Ensure<ContentSizeFitter>(pill);
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            EditorUtility.SetDirty(fit);
            if (date != null)
            {
                date.SetParent(pill.transform, false);
                var t = Txt(date.gameObject, null, 32, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        // 불러오는 중 뼈대 — 옅은 회색 네모 대신 말풍선과 같은 둥근 카드색
        private static void ChatSkeleton(GameObject sk)
        {
            foreach (var img in sk.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject.name == "Avatar")
                {
                    Img(img.gameObject, Spr("r0926_circle"), Card, Image.Type.Simple).raycastTarget = false;
                    var le = img.GetComponent<LayoutElement>();
                    if (le != null) { le.preferredWidth = ChatAvatar; le.preferredHeight = ChatAvatar; EditorUtility.SetDirty(le); }
                    RT(img.gameObject).sizeDelta = new Vector2(ChatAvatar, ChatAvatar);
                }
                else if (img.gameObject.name == "Bubble")
                    Img(img.gameObject, Spr("r0926_pill"), Card, Image.Type.Sliced, 63f / BubbleRadius).raycastTarget = false;
            }
        }

        // 제목줄 — ‹ · 이름(흰 글, 우팡 공식 방은 분홍 체크) · ⋯. 예전 « 그림과 금색 제목을 걷는다
        private static void ChatHeader(Transform chat, List<string> log)
        {
            var header = Find(chat, "Header");
            if (header == null) { log.Add("대화창: 제목줄 없음"); return; }
            var back = header.transform.Find("BackButton");
            if (back != null)
            {
                var bImg = back.GetComponent<Image>();
                if (bImg != null) { bImg.sprite = null; bImg.color = new Color(1, 1, 1, 0); bImg.raycastTarget = true; EditorUtility.SetDirty(bImg); }
                SetRect(RT(back.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(ChatHeaderEdge, 0), new Vector2(ChatHeaderHit, ChatHeaderHit));
                var ic = FindOrCreate(back, "Icon0926");
                ic.transform.SetAsFirstSibling();   // 누름은 위의 ClosePx0926 이 받는다
                Img(ic, Spr("r0926_i_back"), Ink, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));
            }
            var title = header.transform.Find("ChatTitle");
            if (title != null)
            {
                Txt(title.gameObject, null, 58, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                var badge = FindOrCreate(title, "Badge0926");
                Img(badge, Spr("r0926_circle"), Pink, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(badge), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(200, 0), new Vector2(46, 46));
                var chk = FindOrCreate(badge.transform, "Check");
                Img(chk, Spr("r0926_i_check"), Color.white, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(chk), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
                badge.SetActive(false);
            }
            else log.Add("대화창: 제목 없음");
        }
    }
}
