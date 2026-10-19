using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 팔로잉/팔로워 창 — 메시지·대화창과 같은 0926 모양으로 (2026-10-18 대표님: "각 레이아웃 탭 수정한 것처럼 · 팔로잉/메세지 버튼 좀 크게").
    /// 예전엔 흰 테두리 카드에 뒤가 비쳤고 버튼 120x56·글자 32, 아이디 42 였다. 가로에선 카드가 오른쪽 도크 막대를 가렸고,
    /// 투명한 창 바탕이 도크까지 덮어 도크를 눌러도 반응이 없었다 → 도크 프로필 칸 자리에 '닫기'(메인으로).
    /// 줄 모양은 프리팹(Assets/Prefabs/FollowListItem)이 정하고 FollowManager 는 글·색만 넣는다 → 프리팹을 고친다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string FollowItemPrefab = "Assets/Prefabs/FollowListItem.prefab";
        private const string ConvItemPrefab = "Assets/Prefabs/DM/ConversationItem.prefab";
        private const float FollowRowH = 168f, FollowAvatar = 128f, FollowBtnW = 212f, FollowBtnH = 92f;
        private const float FollowTabY = 212f, FollowTabH = 112f, FollowSearchY = 348f, FollowSearchH = 116f, FollowListTop = 484f;
        private static readonly Color FollowSecondary = new Color(1f, 1f, 1f, 0.12f);

        private static void ApplyFollowLook(Transform root, List<string> log)
        {
            var fp = root.Find("FollowPanel");
            var bg = FindP(root, "FollowPanel/Background");
            var fm = FindInScene<FollowManager>(root.gameObject);
            if (fp == null || bg == null || fm == null) { log.Add("팔로우 창 없음"); return; }

            var pImg = fp.GetComponent<Image>();
            if (pImg != null) { pImg.color = new Color(0, 0, 0, 0.35f); EditorUtility.SetDirty(pImg); }
            Img(bg.gameObject, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius).raycastTarget = true;

            FollowHeader(bg, log);
            FollowTabs(bg, log);
            FollowSearch(bg, log);
            FollowList(bg, log);
            FollowDialog(fp, log);
            FollowDockClose(root, fp, bg, fm, log);
            FollowColors(fm);
            EditFollowItem(log);
            EditSearchRowPill(log);
            log.Add("follow look ok");
        }

        // 제목줄 — 대화창과 같게: ‹ 아이콘 · 가운데 아이디 (예전 '이전쓰' 그림 · 글자 90)
        private static void FollowHeader(Transform bg, List<string> log)
        {
            var header = bg.Find("Header");
            if (header == null) { log.Add("팔로우: 제목줄 없음"); return; }
            var back = header.Find("BackButton");
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
            var title = header.Find("Title");
            if (title != null)
            {
                var t = Txt(title.gameObject, null, 58, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 36; t.resizeTextMaxSize = 58;
                t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
                var trt = RT(title.gameObject);
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
                trt.offsetMin = new Vector2(ChatHeaderEdge + ChatHeaderHit + 8, 0); trt.offsetMax = new Vector2(-(ChatHeaderEdge + ChatHeaderHit + 8), 0);
                EditorUtility.SetDirty(trt);
            }
        }

        // 팔로잉 | 팔로워 — 목록 창의 '목록 | 지도 | 설정' 과 같은 알약 칸. 고른 칸 바탕은 Line 그림이 맡는다 (FollowManager 가 색만 바꾼다)
        private static void FollowTabs(Transform bg, List<string> log)
        {
            var bar = bg.Find("TabBar");
            if (bar == null) { log.Add("팔로우: 탭 없음"); return; }
            Img(bar.gameObject, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / (FollowTabH / 2f)).raycastTarget = true;
            SetRect(RT(bar.gameObject), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -FollowTabY), new Vector2(-80, FollowTabH));
            var hlg = bar.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.padding = new RectOffset(6, 6, 6, 6);
                hlg.spacing = 0f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true; hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
                EditorUtility.SetDirty(hlg);
            }
            foreach (var (name, words) in new[]
            {
                ("FollowingTab", new[] { "팔로잉", "Following", "フォロー中", "关注", "Siguiendo" }),
                ("FollowersTab", new[] { "팔로워", "Followers", "フォロワー", "粉丝", "Seguidores" }),
            })
            {
                var tab = bar.Find(name);
                if (tab == null) { log.Add("팔로우: 탭 없음 " + name); continue; }
                var tImg = tab.GetComponent<Image>();
                if (tImg != null) { tImg.sprite = null; tImg.color = new Color(0, 0, 0, 0); tImg.raycastTarget = true; EditorUtility.SetDirty(tImg); }
                var line = tab.Find("Line");
                if (line != null)
                {
                    line.SetAsFirstSibling();
                    Img(line.gameObject, Spr("r0926_pill"), new Color(1, 1, 1, 0.14f), Image.Type.Sliced, 64f / ((FollowTabH - 12f) / 2f)).raycastTarget = false;
                    Stretch(RT(line.gameObject));
                }
                var text = tab.Find("Text");
                if (text != null)
                {
                    var t = Txt(text.gameObject, words[0], 48, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    Stretch(RT(text.gameObject));
                    Loc(text.gameObject, words[0], words[1], words[2], words[3], words[4]);
                }
            }
        }

        // 아이디 검색 — 메시지 창 검색칸과 같은 옅은 알약. 지우기 X 는 글을 쓰면 나타난다 (FollowManager)
        private static void FollowSearch(Transform bg, List<string> log)
        {
            var bar = bg.Find("SearchBar");
            if (bar == null) { log.Add("팔로우: 검색칸 없음"); return; }
            Img(bar.gameObject, Spr("r0926_pill"), Glass7, Image.Type.Sliced, 64f / (FollowSearchH / 2f)).raycastTarget = true;
            SetRect(RT(bar.gameObject), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -FollowSearchY), new Vector2(-80, FollowSearchH));
            var input = bar.Find("SearchInput");
            if (input != null)
            {
                var irt = RT(input.gameObject);
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.pivot = new Vector2(0.5f, 0.5f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = new Vector2(-110, 0);
                EditorUtility.SetDirty(irt);
            }
            var field = input != null ? input.GetComponent<InputField>() : null;
            foreach (var n in new[] { "Placeholder", "Text" })
            {
                var tt = bar.Find(n);
                if (tt == null) continue;
                bool ph = field != null && field.placeholder != null && field.placeholder.gameObject == tt.gameObject;
                Txt(tt.gameObject, null, ph ? 42 : 44, ph ? Muted : Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
                var trt = RT(tt.gameObject);
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
                trt.offsetMin = new Vector2(44, 0); trt.offsetMax = new Vector2(-120, 0);
                EditorUtility.SetDirty(trt);
            }
            var clear = bar.Find("SearchClearButton");
            if (clear != null)
            {
                SetRect(RT(clear.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(96, 96));
                var cImg = clear.GetComponent<Image>();
                if (cImg != null) { cImg.sprite = null; cImg.color = new Color(1, 1, 1, 0); cImg.raycastTarget = true; EditorUtility.SetDirty(cImg); }
                var ic = FindOrCreate(clear, "Icon0926");
                Img(ic, Spr("r0926_i_close"), Muted, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
                clear.gameObject.SetActive(false);
            }
            var x = bar.Find("X");   // 예전 '×' 글자 — 지우기 버튼 그림을 Icon0926 이 대신한다
            if (x != null) x.gameObject.SetActive(false);
        }

        // 목록 칸 — 검색칸 아래부터 카드 아래까지 고정 여백 (예전 비율 자리는 가로에서 검색칸을 덮었다)
        private static void FollowList(Transform bg, List<string> log)
        {
            var area = bg.Find("ScrollArea");
            var vp = area != null ? area.Find("Viewport") : null;
            var content = vp != null ? vp.Find("Content") : null;
            if (content == null) { log.Add("팔로우: 목록 칸 없음"); return; }
            Stretch(RT(area.gameObject));
            RT(area.gameObject).offsetMin = new Vector2(19, 20);
            RT(area.gameObject).offsetMax = new Vector2(-19, -FollowListTop);
            Stretch(RT(vp.gameObject));
            // 거의 투명한 흰 그림으로 자르던 Mask → 그림 없이 네모로 자르는 RectMask2D (줄마다의 동그란 사진 Mask 는 그대로)
            var mask = vp.GetComponent<Mask>();
            if (mask != null) Object.DestroyImmediate(mask);
            Ensure<RectMask2D>(vp.gameObject);
            var vImg = vp.GetComponent<Image>();
            if (vImg != null) { vImg.sprite = null; vImg.color = new Color(0, 0, 0, 0); vImg.raycastTarget = true; EditorUtility.SetDirty(vImg); }
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.padding = new RectOffset(0, 0, 8, 24);
                vlg.spacing = 0f;
                EditorUtility.SetDirty(vlg);
            }
        }

        // 팔로우 취소 확인 — 다른 대화상자와 같은 어두운 카드 + 알약 버튼 (취소 왼쪽 · 팔로우 취소 오른쪽)
        private static void FollowDialog(Transform fp, List<string> log)
        {
            var dlg = fp.Find("UnfollowConfirmDialog");
            var box = dlg != null ? dlg.Find("DialogBox") : null;
            if (box == null) { log.Add("팔로우: 취소 확인 없음"); return; }
            var dImg = dlg.GetComponent<Image>();
            if (dImg != null) { dImg.sprite = null; dImg.color = new Color(0, 0, 0, 0.5f); dImg.raycastTarget = true; EditorUtility.SetDirty(dImg); }
            Img(box.gameObject, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 0.98f), Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(box.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040, 560));
            Slide(box.gameObject, 140f);

            var text = box.Find("ConfirmText");   // 글은 FollowManager 가 '아이디\n팔로우를 취소하시겠습니까?' 로 쓴다
            if (text != null)
            {
                var t = Txt(text.gameObject, null, 44, Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
                t.lineSpacing = 1.15f;
                t.resizeTextForBestFit = false;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                var trt = RT(text.gameObject);
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
                trt.offsetMin = new Vector2(70, 210); trt.offsetMax = new Vector2(-70, -50);
                EditorUtility.SetDirty(trt);
            }
            var buttons = box.Find("ButtonContainer");
            if (buttons == null) return;
            SetRect(RT(buttons.gameObject), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(-112, 128));
            var hlg = buttons.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.padding = new RectOffset(0, 0, 0, 0);
                hlg.spacing = 24f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true; hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
                EditorUtility.SetDirty(hlg);
            }
            var cancel = buttons.Find("CancelButton");
            if (cancel != null)
            {
                PillButton(cancel.gameObject, false);
                var ct = cancel.Find("Text");
                if (ct != null) Loc(ct.gameObject, CancelWords[0], CancelWords[1], CancelWords[2], CancelWords[3], CancelWords[4]);
            }
            var confirm = buttons.Find("ConfirmButton");
            if (confirm != null)
            {
                PillButton(confirm.gameObject, true);
                var ct = confirm.Find("Text");
                if (ct != null)
                {
                    var t = ct.GetComponent<Text>();
                    if (t != null) { t.color = new Color(0.8f, 0.2f, 0.2f, 1f); EditorUtility.SetDirty(t); }
                    Loc(ct.gameObject, "팔로우 취소", "Unfollow", "フォロー解除", "取消关注", "Dejar de seguir");
                }
            }
        }

        // 도크 프로필 칸 자리에 X '닫기' — 누르면 창이 내려간 뒤 메인으로 (‹ 는 그대로 프로필로)
        private static void FollowDockClose(Transform root, Transform fp, Transform bg, FollowManager fm, List<string> log)
        {
            var dc = fp.Find("DockMirror0926/DockClose0926");
            var go = dc != null ? dc.gameObject : NewUI("DockClose0926", fp);
            Ensure<Image>(go);
            var b = Ensure<Button>(go);
            DockSlotClose(go, fp, SlotProfile);
            ResetListeners(b);
            UnityEventTools.AddPersistentListener(b.onClick, fm.CloseAway);
            EditorUtility.SetDirty(b);

            var sd = bg.GetComponent<R0926SwipeDismiss>();
            if (sd != null) Proxy(root, sd, "FollowPanel/DockMirror0926/DockClose0926", log);
            else log.Add("팔로우: 끌어 닫기 없음 — 도크 닫기 덮개 못 붙임");

            // 끌어내릴 때 도크 칸 X 가 카드 위로 비치지 않게 카드 틀보다 먼저 그린다 (메시지·대화와 같이). 취소 확인 창은 맨 위 그대로
            var mirror = fp.Find("DockMirror0926");
            var clip = fp.Find(Clip);
            if (mirror != null && clip != null && mirror.GetSiblingIndex() > clip.GetSiblingIndex())
                mirror.SetSiblingIndex(clip.GetSiblingIndex());
        }

        // FollowManager 가 버튼·탭을 칠할 색 — 팔로우: 흰 알약 · 팔로잉/메시지: 옅은 알약
        private static void FollowColors(FollowManager fm)
        {
            var so = new SerializedObject(fm);
            so.FindProperty("followButtonColor").colorValue = Ink;
            so.FindProperty("followingButtonColor").colorValue = FollowSecondary;
            so.FindProperty("followTextColor").colorValue = Dark;
            so.FindProperty("followingTextColor").colorValue = Ink;
            so.FindProperty("titleFontSize").intValue = 58;
            so.FindProperty("tabFontSize").intValue = 48;
            so.FindProperty("activeTabColor").colorValue = Ink;
            so.FindProperty("inactiveTabColor").colorValue = Muted;
            so.FindProperty("activeLineColor").colorValue = new Color(1, 1, 1, 0.14f);
            so.FindProperty("newFollowerColor").colorValue = new Color(1, 1, 1, 0.10f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EditFollowItem(List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FollowItemPrefab) == null) { log.Add("팔로우: 줄 프리팹 없음"); return; }
            var go = PrefabUtility.LoadPrefabContents(FollowItemPrefab);
            try
            {
                FollowRow(go);
                PrefabUtility.SaveAsPrefabAsset(go, FollowItemPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        // 한 줄 — 사진 128 · 아이디 52 굵게 · 알약 버튼 212x92 글자 40 (예전 사진 100 · 아이디 42 · 버튼 120x56 글자 32)
        private static void FollowRow(GameObject row)
        {
            var rrt = RT(row);
            rrt.sizeDelta = new Vector2(rrt.sizeDelta.x, FollowRowH);
            var le = row.GetComponent<LayoutElement>();
            if (le != null) { le.minHeight = FollowRowH; le.preferredHeight = FollowRowH; EditorUtility.SetDirty(le); }
            // 줄 바탕 — 평소엔 투명(누르는 자리), 새 팔로워 알림으로 열면 이 둥근 바탕만 잠깐 밝힌다 (FollowManager.FlashRow)
            var rowImg = Img(row, Spr("r0926_pill"), new Color(1, 1, 1, 0), Image.Type.Sliced, 64f / 44f);
            rowImg.preserveAspect = false; rowImg.raycastTarget = true;

            var av = row.transform.Find("Avatar");
            if (av != null)
            {
                SetRect(RT(av.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(FollowAvatar, FollowAvatar));
                // 자르는 원: 유니티 기본 Knob(32px) → 우팡 원(256px). 작은 원을 늘려 자르면 가장자리가 계단처럼 깨진다 (프로필 카드와 같이)
                var aImg = av.GetComponent<Image>();
                if (aImg != null) { aImg.sprite = Spr("r0926_circle"); aImg.type = Image.Type.Simple; aImg.preserveAspect = false; aImg.color = Card; EditorUtility.SetDirty(aImg); }
            }
            var name = row.transform.Find("Username");
            if (name != null)
            {
                var nrt = RT(name.gameObject);
                nrt.anchorMin = new Vector2(0, 0.5f); nrt.anchorMax = new Vector2(1, 0.5f); nrt.pivot = new Vector2(0, 0.5f);
                nrt.offsetMin = new Vector2(28 + FollowAvatar + 32, -44); nrt.offsetMax = new Vector2(-(28 + FollowBtnW + 16 + FollowBtnW + 28), 44);
                EditorUtility.SetDirty(nrt);
                var t = Txt(name.gameObject, null, 52, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 34; t.resizeTextMaxSize = 52;
                t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            }
            FollowButton(row.transform.Find("ActionButton"), -(28 + FollowBtnW + 16), Ink, Dark);
            FollowButton(row.transform.Find("MessageButton"), -28, FollowSecondary, Ink);
        }

        // 메세지 검색 줄 오른쪽 끝 팔로우 알약 — 대화가 없는 사람도 아이디로 찾아 바로 팔로우 (2026-10-18 대표님).
        // 대화 목록과 같은 줄 프리팹이라 평소엔 꺼 두고, 검색 결과에서만 MessagePanelManager 가 켠다 (R0926FollowPill)
        private static void EditSearchRowPill(List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ConvItemPrefab) == null) { log.Add("메세지 검색: 대화 줄 프리팹 없음"); return; }
            var go = PrefabUtility.LoadPrefabContents(ConvItemPrefab);
            try
            {
                var content = go.transform.Find("Content");
                if (content == null) { log.Add("메세지 검색: 대화 줄 Content 없음"); return; }
                var pill = FindOrCreate(content, R0926FollowPill.Name);
                pill.transform.SetAsLastSibling();
                Ensure<Button>(pill);
                Ensure<UITouchForwarder>(pill);
                Txt(FindOrCreate(pill.transform, "Text"), "팔로우", 40, Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
                FollowButton(pill.transform, -24f, Ink, Dark);
                pill.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(go, ConvItemPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        private static void FollowButton(Transform b, float x, Color fill, Color label)
        {
            if (b == null) return;
            SetRect(RT(b.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(x, 0), new Vector2(FollowBtnW, FollowBtnH));
            var img = Img(b.gameObject, Spr("r0926_pill"), fill, Image.Type.Sliced, 64f / (FollowBtnH / 2f));
            img.preserveAspect = false; img.raycastTarget = true;
            foreach (var t in b.GetComponentsInChildren<Text>(true))
            {
                Txt(t.gameObject, null, 40, label, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 28; t.resizeTextMaxSize = 40;
                t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
                var trt = RT(t.gameObject);
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
                trt.offsetMin = new Vector2(14, 0); trt.offsetMax = new Vector2(-14, 0);
                EditorUtility.SetDirty(trt);
            }
        }
    }
}
