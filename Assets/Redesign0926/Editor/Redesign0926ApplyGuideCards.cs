using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 첫 사용 안내 카드뉴스 (11-10 대표님 「카드뉴스 작업한 것들 … 베이스로 옆으로 스와이프해서 넘겨보면서 안내 … 화살표 안내와 함께 … 가볍게 모달로」).
    /// FirstTimeGuidePanel 맨 위에 자기 캔버스(9450)를 가진 Cards0926 — 카드 9장 · 쪽 점 · 다음/시작하기 · 건너뛰기 · ‹ › · 어둡게 + 구멍 + 화살표.
    /// 자리와 크기는 R0926GuideCards 가 화면마다 정한다 — 여기선 모양과 연결만. 휴대폰 화면은 Guide0926 폴더의 JPG(.bytes).
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string GuideCardDir = "Assets/Redesign0926/Guide0926/";
        private const int GuideCardCount = 9;
        // 카드마다 비출 하단 버튼 (없으면 null) — 주변 · 누르면 · 목록 · 지도 · 추가 · 짜잔 · 보물 숨기기 · 보물 찾음 · 사람
        private static readonly string[] GuideCardTargets = { null, null, "List_Button", "List_Button", "PlusButton", "PlusButton", "Hunt_Button0926", "Hunt_Button0926", null };

        private static void ApplyGuideCards(Transform root, List<string> log)
        {
            var guide = FindInScene<FirstTimeGuide>(root.gameObject);
            var panel = Find(root, "FirstTimeGuidePanel");
            if (guide == null || panel == null) { log.Add("카드 안내: 안내 없음"); return; }
            var dockGo = Find(root, "Dock0926");

            var go = FindOrCreate(panel.transform, "Cards0926");
            go.transform.SetAsLastSibling();
            Stretch(RT(go));
            var cv = Ensure<Canvas>(go);
            cv.overrideSorting = true;
            cv.sortingOrder = 9450;   // 창 · 도크 위, 토스트(9500) · 노치(9600) 아래
            EditorUtility.SetDirty(cv);
            Ensure<GraphicRaycaster>(go);
            var group = Ensure<CanvasGroup>(go);
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;   // 열릴 때 R0926GuideCards 가 켠다
            EditorUtility.SetDirty(group);
            go.SetActive(true);

            int order = 0;
            // 뒤 화면을 누르지 않게 + 끌기를 받는 판 (하단 버튼 자리는 R0926GuideCards 가 비켜 준다)
            var catcher = FindOrCreate(go.transform, "Catch0926");
            catcher.transform.SetSiblingIndex(order++);
            Stretch(RT(catcher));
            Img(catcher, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;

            var dimColor = new Color(0.016f, 0.024f, 0.031f, 0.8f);
            var dims = new Image[4];
            string[] dn = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
            for (int i = 0; i < 4; i++)
            {
                var d = FindOrCreate(go.transform, dn[i]);
                d.transform.SetSiblingIndex(order++);
                dims[i] = Img(d, null, dimColor, Image.Type.Simple);
                dims[i].raycastTarget = false;
            }
            var hole = FindOrCreate(go.transform, "Hole0926");
            hole.transform.SetSiblingIndex(order++);
            Img(hole, Spr("r0926_hole"), dimColor, Image.Type.Simple).raycastTarget = false;
            var ring = FindOrCreate(go.transform, "Ring0926");
            ring.transform.SetSiblingIndex(order++);
            var ringImg = Img(ring, Spr("r0926_ring_big"), new Color(1, 1, 1, 0.55f), Image.Type.Simple);
            ringImg.raycastTarget = false;
            var arrow = FindOrCreate(go.transform, "Arrow0926");
            arrow.transform.SetSiblingIndex(order++);
            SetRect(RT(arrow), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 106));
            var arrowImg = Img(arrow, Spr("r0926_arrow_short"), Color.white, Image.Type.Simple);   // 그림은 아래를 가리킨다
            arrowImg.raycastTarget = false;

            // 카드 — 둥근 판(마스크) 안에 바탕 · 브랜드 · 제목 · 설명 · 휴대폰(마스크) · 테두리
            var track = FindOrCreate(go.transform, "Track0926");
            track.transform.SetSiblingIndex(order++);
            SetRect(RT(track), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 2400));
            Ensure<RectMask2D>(track);
            var cardGos = new GameObject[GuideCardCount];
            for (int i = 0; i < GuideCardCount; i++)
            {
                var card = FindOrCreate(track.transform, "Card" + (i + 1));
                card.transform.SetSiblingIndex(i);
                cardGos[i] = card;
                SetRect(RT(card), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1140, 2000));
                Img(card, Spr("r0926_pill"), Color.white, Image.Type.Sliced, 64f / 56f).raycastTarget = false;
                Ensure<Mask>(card).showMaskGraphic = false;
                Ensure<CanvasGroup>(card);
                int k = 0;

                var bg = FindOrCreate(card.transform, "Bg");
                bg.transform.SetSiblingIndex(k++);
                Stretch(RT(bg));
                var bgRaw = Ensure<RawImage>(bg);
                bgRaw.color = Color.white; bgRaw.raycastTarget = false;
                EditorUtility.SetDirty(bgRaw);

                var dot = FindOrCreate(card.transform, "BrandDot");
                dot.transform.SetSiblingIndex(k++);
                Img(dot, Spr("r0926_circle"), Pink, Image.Type.Simple).raycastTarget = false;
                var brand = FindOrCreate(card.transform, "Brand");
                brand.transform.SetSiblingIndex(k++);
                Txt(brand, "W O O P A N G", 30, new Color(1, 1, 1, 0.78f), TextAnchor.MiddleCenter, FontStyle.Normal);

                var head = FindOrCreate(card.transform, "Head");
                head.transform.SetSiblingIndex(k++);
                var ht = Txt(head, "", 88, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
                ht.supportRichText = true;
                var sh = Ensure<Shadow>(head);
                sh.effectColor = new Color(0, 0, 0, 0.35f); sh.effectDistance = new Vector2(0, -4);
                EditorUtility.SetDirty(sh);

                var sub = FindOrCreate(card.transform, "Sub");
                sub.transform.SetSiblingIndex(k++);
                Txt(sub, "", 40, new Color(0.902f, 0.918f, 0.941f, 0.82f), TextAnchor.UpperCenter, FontStyle.Normal);

                var phone = FindOrCreate(card.transform, "Phone");
                phone.transform.SetSiblingIndex(k++);
                Img(phone, Spr("r0926_pill"), Color.black, Image.Type.Sliced, 1f).raycastTarget = false;
                Ensure<Mask>(phone).showMaskGraphic = true;
                var shot = FindOrCreate(phone.transform, "Shot");
                Stretch(RT(shot));
                var shotRaw = Ensure<RawImage>(shot);
                shotRaw.color = Color.white; shotRaw.raycastTarget = false;
                EditorUtility.SetDirty(shotRaw);

                var stroke = FindOrCreate(card.transform, "Stroke");
                stroke.transform.SetSiblingIndex(k++);
                Img(stroke, Spr("r0926_pill_ring"), new Color(1, 1, 1, 0.16f), Image.Type.Sliced, 1f).raycastTarget = false;
            }

            // 양옆 ‹ › — 카드 가장자리에 반쯤 걸친 동그라미
            var prev = GuideRound(go.transform, "Prev0926", order++, false);
            var next = GuideRound(go.transform, "Next0926", order++, true);

            // 쪽 점
            var dots = FindOrCreate(go.transform, "Dots0926");
            dots.transform.SetSiblingIndex(order++);
            SetRect(RT(dots), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 18));
            var dotImgs = new Image[GuideCardCount];
            for (int i = 0; i < GuideCardCount; i++)
            {
                var d = FindOrCreate(dots.transform, "Dot" + (i + 1));
                d.transform.SetSiblingIndex(i);
                SetRect(RT(d), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
                dotImgs[i] = Img(d, Spr("r0926_pill"), new Color(1, 1, 1, 0.35f), Image.Type.Sliced, 64f / 9f);
                dotImgs[i].raycastTarget = false;
            }

            // 다음 / 시작하기 (마지막 카드에서 분홍)
            var primary = FindOrCreate(go.transform, "Primary0926");
            primary.transform.SetSiblingIndex(order++);
            SetRect(RT(primary), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 128));
            var primaryImg = Img(primary, Spr("r0926_pill"), new Color(1, 1, 1, 0.16f), Image.Type.Sliced, 1f);
            primaryImg.raycastTarget = true;
            var pl = FindOrCreate(primary.transform, "Label0926");
            Stretch(RT(pl));
            var plt = Txt(pl, "다음", 50, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            plt.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 건너뛰기 — 세로는 위 오른쪽, 가로는 카드 안 위 오른쪽
            var skip = FindOrCreate(go.transform, "Skip0926");
            skip.transform.SetSiblingIndex(order++);
            SetRect(RT(skip), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 96));
            Img(skip, Spr("r0926_pill"), new Color(1, 1, 1, 0.12f), Image.Type.Sliced, 64f / 48f).raycastTarget = true;
            var sl = FindOrCreate(skip.transform, "Label0926");
            Stretch(RT(sl));
            var slt = Txt(sl, "건너뛰기", 42, Soft, TextAnchor.MiddleCenter, FontStyle.Bold);
            slt.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 안드로이드 뒤로가기 — 보이지 않는 버튼
            var back = FindOrCreate(go.transform, "Back0926");
            back.transform.SetSiblingIndex(order++);
            SetRect(RT(back), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var backBtn = Ensure<Button>(back);
            backBtn.transition = Selectable.Transition.None;
            Ensure<ClickButtonOnBack>(back);

            // ── 연결 ──
            var cards = Ensure<R0926GuideCards>(go);
            var so = new SerializedObject(cards);
            so.FindProperty("guide").objectReferenceValue = guide;
            so.FindProperty("group").objectReferenceValue = group;
            var cp = so.FindProperty("cards");
            cp.arraySize = GuideCardCount;
            for (int i = 0; i < GuideCardCount; i++)
            {
                var c = cardGos[i].transform;
                var e = cp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = RT(cardGos[i]);
                e.FindPropertyRelative("plate").objectReferenceValue = cardGos[i].GetComponent<Image>();
                e.FindPropertyRelative("group").objectReferenceValue = cardGos[i].GetComponent<CanvasGroup>();
                e.FindPropertyRelative("bg").objectReferenceValue = c.Find("Bg").GetComponent<RawImage>();
                e.FindPropertyRelative("brandDot").objectReferenceValue = (RectTransform)c.Find("BrandDot");
                e.FindPropertyRelative("brand").objectReferenceValue = c.Find("Brand").GetComponent<Text>();
                e.FindPropertyRelative("head").objectReferenceValue = c.Find("Head").GetComponent<Text>();
                e.FindPropertyRelative("sub").objectReferenceValue = c.Find("Sub").GetComponent<Text>();
                e.FindPropertyRelative("phone").objectReferenceValue = c.Find("Phone").GetComponent<Image>();
                e.FindPropertyRelative("shot").objectReferenceValue = c.Find("Phone/Shot").GetComponent<RawImage>();
                e.FindPropertyRelative("stroke").objectReferenceValue = c.Find("Stroke").GetComponent<Image>();
            }
            var tp = so.FindProperty("targets");
            tp.arraySize = GuideCardCount;
            for (int i = 0; i < GuideCardCount; i++)
            {
                var name = i < GuideCardTargets.Length ? GuideCardTargets[i] : null;
                var t = name != null && dockGo != null ? Find(dockGo.transform, name) : null;
                if (name != null && t == null) log.Add("카드 안내: 비출 버튼 없음 " + name);
                tp.GetArrayElementAtIndex(i).objectReferenceValue = t != null ? RT(t) : null;
            }
            so.FindProperty("dock").objectReferenceValue = dockGo != null ? RT(dockGo) : null;   // 안내 중에도 하단 버튼은 눌린다 (10-19)
            var dp = so.FindProperty("dims");
            dp.arraySize = 4;
            for (int i = 0; i < 4; i++) dp.GetArrayElementAtIndex(i).objectReferenceValue = dims[i];
            so.FindProperty("hole").objectReferenceValue = RT(hole);
            so.FindProperty("ring").objectReferenceValue = ringImg;
            so.FindProperty("arrow").objectReferenceValue = arrowImg;
            so.FindProperty("track").objectReferenceValue = RT(track);
            so.FindProperty("prev").objectReferenceValue = RT(prev);
            so.FindProperty("next").objectReferenceValue = RT(next);
            so.FindProperty("dots").objectReferenceValue = RT(dots);
            var dip = so.FindProperty("dotImages");
            dip.arraySize = GuideCardCount;
            for (int i = 0; i < GuideCardCount; i++) dip.GetArrayElementAtIndex(i).objectReferenceValue = dotImgs[i];
            so.FindProperty("primary").objectReferenceValue = RT(primary);
            so.FindProperty("primaryImage").objectReferenceValue = primaryImg;
            so.FindProperty("primaryLabel").objectReferenceValue = plt;
            so.FindProperty("skip").objectReferenceValue = RT(skip);
            so.FindProperty("skipLabel").objectReferenceValue = slt;

            // 휴대폰 화면 — 한국어는 _ko, 나머지 언어는 _en (1 · 2번은 스토어 사진 그대로 하나)
            var bgAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(GuideCardDir + "gc_bg.bytes");
            if (bgAsset == null) log.Add("카드 안내: 바탕 그림 없음");
            so.FindProperty("background").objectReferenceValue = bgAsset;
            var ko = so.FindProperty("shotsKo");
            var en = so.FindProperty("shotsEn");
            ko.arraySize = GuideCardCount; en.arraySize = GuideCardCount;
            int missing = 0;
            for (int i = 0; i < GuideCardCount; i++)
            {
                string n = (i + 1).ToString("00");
                bool shared = i < 2;
                var a = AssetDatabase.LoadAssetAtPath<TextAsset>(GuideCardDir + "gc_" + n + (shared ? "" : "_ko") + ".bytes");
                var b = AssetDatabase.LoadAssetAtPath<TextAsset>(GuideCardDir + "gc_" + n + (shared ? "" : "_en") + ".bytes");
                if (a == null) missing++;
                if (b == null) missing++;
                ko.GetArrayElementAtIndex(i).objectReferenceValue = a;
                en.GetArrayElementAtIndex(i).objectReferenceValue = b;
            }
            if (missing > 0) log.Add("카드 안내: 휴대폰 화면 " + missing + "장 없음");
            so.FindProperty("holeSize").floatValue = 260f;
            so.FindProperty("dimAlpha").floatValue = 0.8f;

            // 넘기기 · 시작하기 · 건너뛰기 누름 — 하단 메뉴 버튼과 같은 소리 · AR 오브젝트 터치와 같은 진동 (예전 안내와 같다)
            var dockList = dockGo != null ? dockGo.transform.Find("List_Button") : null;
            var dockFw = dockList != null ? dockList.GetComponent<UITouchForwarder>() : null;
            var dockClip = dockFw != null ? new SerializedObject(dockFw).FindProperty("customSound").objectReferenceValue : null;
            if (dockClip != null) so.FindProperty("tapSound").objectReferenceValue = dockClip;
            else log.Add("카드 안내: 도크 버튼 소리 못 찾음 — 기본 소리");
            var dm = FindInScene<DataManager>(root.gameObject);
            var touch = dm != null && dm.cubePrefab != null ? dm.cubePrefab.GetComponentInChildren<Object3DTouchHaptic>(true) : null;
            if (touch != null) so.FindProperty("tapHaptic").floatValue = new SerializedObject(touch).FindProperty("hapticIntensity").floatValue;
            so.ApplyModifiedPropertiesWithoutUndo();

            GuideWire(prev, cards.Prev);
            GuideWire(next, cards.Next);
            GuideWire(primary, cards.Primary);
            GuideWire(skip, cards.Skip);
            GuideWire(back, cards.Back);

            var gso = new SerializedObject(guide);
            gso.FindProperty("cards").objectReferenceValue = cards;
            gso.ApplyModifiedPropertiesWithoutUndo();
            log.Add("카드 안내 ok (카드 " + GuideCardCount + " · 그림 " + (GuideCardCount * 2 - missing) + "/" + GuideCardCount * 2 + ")");
        }

        // 동그란 ‹ › 버튼 — 크기는 R0926GuideCards 가 카드에 맞춘다
        private static GameObject GuideRound(Transform parent, string name, int sibling, bool flip)
        {
            var b = FindOrCreate(parent, name);
            b.transform.SetSiblingIndex(sibling);
            SetRect(RT(b), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104, 104));
            Img(b, Spr("r0926_circle"), new Color(0.03f, 0.035f, 0.05f, 0.45f), Image.Type.Simple).raycastTarget = true;
            var icon = FindOrCreate(b.transform, "Icon0926");
            SetRect(RT(icon), new Vector2(0.24f, 0.24f), new Vector2(0.76f, 0.76f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var ii = Img(icon, Spr("r0926_i_back"), Color.white, Image.Type.Simple);
            ii.raycastTarget = false; ii.preserveAspect = true;
            icon.transform.localRotation = Quaternion.Euler(0f, 0f, flip ? 180f : 0f);
            return b;
        }

        private static void GuideWire(GameObject go, UnityEngine.Events.UnityAction action)
        {
            var b = Ensure<Button>(go);
            ResetListeners(b);
            UnityEventTools.AddPersistentListener(b.onClick, action);
            EditorUtility.SetDirty(b);
        }
    }
}
