using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 지도의 좌표 (2026-10-15 대표님: "지도의 어딘가를 꾸욱 누르고 있으면 핀 발생하면서 위도랑 경도 발생해서 복사할 수 있도록 ·
    /// 지도에서 '검색' 기능이 있어서 '좌표'를 입력하면 해당 위치에 핀이 박히면서 적절한 사이즈로 지도가 확대").
    ///  · 꽂는 핀 — 분홍 물방울 + 위에 좌표 말풍선(복사 · 지우기)
    ///  · 오른쪽 위 돋보기 → 위쪽 검색 줄(좌표 칸 · 붙여넣기 · 닫기). 동의 카드가 지도를 덮을 땐 같이 덮인다
    /// 보던 자리 기억(같은 날 "다른창을 다녀오면 기존에 보던 곳이 리셋")은 R0926FriendsMap 이 한다 — 화면 없음.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float SearchPasteW = 196f, SearchCloseW = 96f;

        private static void ApplyMapCoord(Transform root, List<string> log)
        {
            var map = root.GetComponentInChildren<R0926FriendsMap>(true);
            if (map == null) { log.Add("지도(R0926FriendsMap) 없음"); return; }
            var view = map.transform;
            var so = new SerializedObject(map);
            var pins = so.FindProperty("pinLayer").objectReferenceValue as RectTransform;
            var dim = Find(view, "Dim0926");
            if (pins == null || dim == null) { log.Add("지도 핀 자리·덮개(Dim0926) 없음"); return; }

            var pin = BuildDropPin(pins, out var bubble, out var text, out var name, out var copy, out var clear);

            var sbtn = CtrlButton(view, "Search0926", null, "r0926_i_search");
            SetRect(RT(sbtn), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(CtrlSize, CtrlSize));
            sbtn.SetActive(true);
            PutBefore(sbtn.transform, dim.transform);
            var bar = BuildSearchBar(view, out var input, out var paste, out var close);
            PutBefore(bar.transform, dim.transform);
            var results = BuildSearchResults(view, out var rows);
            PutBefore(results.transform, dim.transform);

            so.FindProperty("dropPin").objectReferenceValue = RT(pin);
            so.FindProperty("dropBubble").objectReferenceValue = bubble;
            so.FindProperty("dropText").objectReferenceValue = text;
            so.FindProperty("dropName").objectReferenceValue = name;
            so.FindProperty("searchButton").objectReferenceValue = sbtn;
            so.FindProperty("searchBar").objectReferenceValue = bar;
            so.FindProperty("searchInput").objectReferenceValue = input;
            so.FindProperty("searchResults").objectReferenceValue = results;
            var rp = so.FindProperty("resultRows");
            rp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++) rp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            so.FindProperty("longPressSeconds").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Wire(copy, map.CopyDropPin);
            Wire(clear, map.ClearDropPin);
            Wire(sbtn.GetComponent<Button>(), map.OpenSearch);
            Wire(paste, map.PasteSearch);
            Wire(close, map.CloseSearch);
            for (int i = 0; i < rows.Length; i++)
            {
                ResetListeners(rows[i]);
                UnityEventTools.AddIntPersistentListener(rows[i].onClick, map.PickResult, i);
            }
            for (int i = input.onSubmit.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(input.onSubmit, i);
            UnityEventTools.AddPersistentListener(input.onSubmit, map.SearchCoord);
            for (int i = input.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(input.onValueChanged, i);
            UnityEventTools.AddPersistentListener(input.onValueChanged, map.SearchTyped);
            EditorUtility.SetDirty(input);
            log.Add("map coord ok (꾹 눌러 핀 + 주소 · 좌표·주소·장소 검색 + 결과 목록 " + rows.Length + "줄)");
        }

        // 덮개보다 앞(아래)에 — 이미 앞이면 그대로
        private static void PutBefore(Transform t, Transform anchor)
        {
            if (t.GetSiblingIndex() > anchor.GetSiblingIndex()) t.SetSiblingIndex(anchor.GetSiblingIndex());
        }

        // 분홍 물방울 핀 (128×168 그림 · 끝이 아래 4px) — 피벗이 핀 끝. 말풍선 자리는 실행 때 (R0926FriendsMap.PlaceDrop)
        private static GameObject BuildDropPin(Transform pins, out RectTransform bubble, out Text text, out Text name, out Button copy, out Button clear)
        {
            var c = new Vector2(0.5f, 0.5f);
            var tip = new Vector2(0.5f, 4f / 168f);
            var pin = FindOrCreate(pins, "DropPin0926");
            pin.transform.SetAsLastSibling();
            SetRect(RT(pin), new Vector2(0, 1), new Vector2(0, 1), tip, Vector2.zero, new Vector2(88, 116));
            var shadow = FindOrCreate(pin.transform, "Shadow");
            shadow.transform.SetAsFirstSibling();
            SetRect(RT(shadow), tip, tip, c, Vector2.zero, new Vector2(46, 16));
            Img(shadow, Spr("r0926_circle"), new Color(0, 0, 0, 0.35f), Image.Type.Simple).raycastTarget = false;
            var body = FindOrCreate(pin.transform, "Body");
            Stretch(RT(body));
            Img(body, Spr("r0926_droppin"), Pink, Image.Type.Simple).raycastTarget = false;
            var dot = FindOrCreate(pin.transform, "Dot");
            var dc = new Vector2(0.5f, 108f / 168f);   // 머리 원의 가운데
            SetRect(RT(dot), dc, dc, c, Vector2.zero, new Vector2(30, 30));
            Img(dot, Spr("r0926_circle"), Color.white, Image.Type.Simple).raycastTarget = false;

            var bub = FindOrCreate(pin.transform, "Bubble");
            SetRect(RT(bub), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(600, 96));
            Img(bub, Spr("r0926_pill"), new Color(0.055f, 0.067f, 0.078f, 0.94f), Image.Type.Sliced, 64f / 48f).raycastTarget = true;   // 말풍선을 눌러도 지도에 핀이 또 꽂히지 않게
            var h = Ensure<HorizontalLayoutGroup>(bub);
            h.padding = new RectOffset(36, 12, 10, 10); h.spacing = 14; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var fit = Ensure<ContentSizeFitter>(bub);
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 왼쪽 두 줄 — 위: 장소 이름·그 자리 주소(있을 때만) · 아래: 좌표 (10-19 브이월드 주소)
            var lines = FindOrCreate(bub.transform, "Lines");
            lines.transform.SetSiblingIndex(0);
            var v = Ensure<VerticalLayoutGroup>(lines);
            v.spacing = 0; v.childAlignment = TextAnchor.MiddleLeft;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            var nm = FindOrCreate(lines.transform, "Name");
            nm.transform.SetSiblingIndex(0);
            name = Txt(nm, "중구 세종대로 110", 38, Soft, TextAnchor.MiddleLeft, FontStyle.Normal);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            nm.SetActive(false);
            var old = bub.transform.Find("Text");   // 예전 말풍선은 좌표 한 줄만 바로 아래에 — 두 줄 칸으로 옮긴다
            var tx = old != null ? old.gameObject : FindOrCreate(lines.transform, "Text");
            if (tx.transform.parent != lines.transform) tx.transform.SetParent(lines.transform, false);
            tx.transform.SetSiblingIndex(1);
            text = Txt(tx, "37.566500, 126.978000", 40, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var cp = FindOrCreate(bub.transform, "Copy");
            Img(cp, Spr("r0926_pill"), Pink, Image.Type.Sliced, 64f / 38f).raycastTarget = true;
            copy = Ensure<Button>(cp); copy.transition = Selectable.Transition.None;
            var ch = Ensure<HorizontalLayoutGroup>(cp);
            ch.padding = new RectOffset(30, 30, 0, 0); ch.childAlignment = TextAnchor.MiddleCenter;
            ch.childControlWidth = true; ch.childControlHeight = true; ch.childForceExpandWidth = false; ch.childForceExpandHeight = true;
            var cle = Ensure<LayoutElement>(cp); cle.minHeight = 76; cle.preferredHeight = 76;
            var cpt = FindOrCreate(cp.transform, "Text");
            Txt(cpt, "복사", 38, Dark, TextAnchor.MiddleCenter, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            Loc(cpt, "복사", "Copy", "コピー", "复制", "Copiar");

            var cl = FindOrCreate(bub.transform, "Clear");
            Img(cl, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            clear = Ensure<Button>(cl); clear.transition = Selectable.Transition.None;
            var lle = Ensure<LayoutElement>(cl); lle.minWidth = 76; lle.preferredWidth = 76; lle.minHeight = 76; lle.preferredHeight = 76;
            var clg = FindOrCreate(cl.transform, "Glyph");
            SetRect(RT(clg), c, c, c, Vector2.zero, new Vector2(40, 40));
            Img(clg, Spr("r0926_i_close"), Soft, Image.Type.Simple).raycastTarget = false;

            bubble = RT(bub);
            pin.SetActive(false);
            return pin;
        }

        // 지도 위쪽 검색 줄 — 돋보기 · 좌표 칸 · 붙여넣기 · 닫기 (지도 버튼과 같은 높이·색)
        private static GameObject BuildSearchBar(Transform view, out InputField input, out Button paste, out Button close)
        {
            var c = new Vector2(0.5f, 0.5f);
            var bar = FindOrCreate(view, "SearchBar0926");
            SetRect(RT(bar), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(-60, CtrlSize));
            Img(bar, Spr("r0926_pill"), new Color(0.055f, 0.067f, 0.078f, 0.95f), Image.Type.Sliced, 64f / (CtrlSize / 2f)).raycastTarget = true;
            var icon = FindOrCreate(bar.transform, "Icon");
            SetRect(RT(icon), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(50, 50));
            Img(icon, Spr("r0926_i_search"), Muted, Image.Type.Simple).raycastTarget = false;

            var field = FindOrCreate(bar.transform, "Input");
            var frt = RT(field);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.pivot = c;
            frt.offsetMin = new Vector2(104, 8); frt.offsetMax = new Vector2(-(SearchPasteW + SearchCloseW + 26), -8);
            var fImg = Img(field, null, new Color(0, 0, 0, 0), Image.Type.Simple);
            fImg.raycastTarget = true;
            var txt = FindOrCreate(field.transform, "Text");
            Stretch(RT(txt));
            var t = Txt(txt, "", 42, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            t.supportRichText = false;
            var ph = FindOrCreate(field.transform, "Placeholder");
            Stretch(RT(ph));
            var pt = Txt(ph, "주소 · 장소 · 좌표", 40, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);
            Loc(ph, "주소 · 장소 · 좌표", "Address, place or coordinates", "住所・場所・座標", "地址・地点・坐标", "Dirección, lugar o coordenadas");
            input = Ensure<InputField>(field);
            input.textComponent = t;
            input.placeholder = pt;
            input.targetGraphic = fImg;
            input.transition = Selectable.Transition.None;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = InputField.ContentType.Standard;
            input.characterLimit = 200;
            input.shouldHideMobileInput = true;   // 지도 위쪽 칸 — 키보드에 가리지 않으니 칸 안에서 바로
            EditorUtility.SetDirty(input);

            var pb = FindOrCreate(bar.transform, "Paste");
            SetRect(RT(pb), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-(SearchCloseW + 14), 0), new Vector2(SearchPasteW, 80));
            Img(pb, Spr("r0926_pill"), new Color(1, 1, 1, 0.1f), Image.Type.Sliced, 64f / 40f).raycastTarget = true;
            paste = Ensure<Button>(pb); paste.transition = Selectable.Transition.None;
            var pbt = FindOrCreate(pb.transform, "Text");
            Stretch(RT(pbt));
            Txt(pbt, "붙여넣기", 38, Soft, TextAnchor.MiddleCenter, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            Loc(pbt, "붙여넣기", "Paste", "貼り付け", "粘贴", "Pegar");

            var cb = FindOrCreate(bar.transform, "Close");
            SetRect(RT(cb), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(SearchCloseW, SearchCloseW));
            Img(cb, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            close = Ensure<Button>(cb); close.transition = Selectable.Transition.None;
            var cg = FindOrCreate(cb.transform, "Glyph");
            SetRect(RT(cg), c, c, c, Vector2.zero, new Vector2(46, 46));
            Img(cg, Spr("r0926_i_close"), Soft, Image.Type.Simple).raycastTarget = false;

            bar.SetActive(false);
            return bar;
        }

        private const int ResultRows = 5;
        private const float ResultRowH = 150f;

        // 검색 줄 바로 아래 결과 목록 — 줄마다 이름(굵게) · 주소(흐리게), 많아야 다섯 (서버가 가까운 곳·같은 이름부터 준다)
        private static GameObject BuildSearchResults(Transform view, out Button[] rows)
        {
            var panel = FindOrCreate(view, "SearchResults0926");
            SetRect(RT(panel), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -(30 + CtrlSize + 14)), new Vector2(-60, 0));
            // 불투명 — 리니어 색공간이라 0.97 만 돼도 뒤의 핀 말풍선 글이 비쳐 보였다. 목록을 눌러도 지도에 핀이 꽂히지 않게 받는다
            Img(panel, Spr("r0926_pill"), new Color(0.055f, 0.067f, 0.078f, 1f), Image.Type.Sliced, 64f / 44f).raycastTarget = true;
            var vg = Ensure<VerticalLayoutGroup>(panel);
            vg.padding = new RectOffset(0, 0, 12, 12); vg.spacing = 0; vg.childAlignment = TextAnchor.UpperLeft;
            vg.childControlWidth = true; vg.childControlHeight = true; vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
            var fit = Ensure<ContentSizeFitter>(panel);
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            rows = new Button[ResultRows];
            for (int i = 0; i < ResultRows; i++)
            {
                var row = FindOrCreate(panel.transform, "Row" + i);
                row.transform.SetSiblingIndex(i);
                Img(row, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
                var b = Ensure<Button>(row);
                b.transition = Selectable.Transition.ColorTint;   // 누르는 동안 살짝 밝게
                var cb = b.colors;
                cb.normalColor = new Color(1, 1, 1, 0); cb.highlightedColor = new Color(1, 1, 1, 0); cb.selectedColor = new Color(1, 1, 1, 0);
                cb.pressedColor = new Color(1, 1, 1, 0.08f); cb.disabledColor = new Color(1, 1, 1, 0); cb.colorMultiplier = 1f; cb.fadeDuration = 0.05f;
                b.colors = cb;
                var le = Ensure<LayoutElement>(row);
                le.minHeight = ResultRowH; le.preferredHeight = ResultRowH;

                var title = FindOrCreate(row.transform, "Title");
                SetRect(RT(title), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(-96, 54));
                var tt = Txt(title, "서울역", 42, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                tt.horizontalOverflow = HorizontalWrapMode.Wrap; tt.verticalOverflow = VerticalWrapMode.Truncate;
                var addr = FindOrCreate(row.transform, "Address");
                SetRect(RT(addr), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(-96, 50));
                var at = Txt(addr, "서울특별시 용산구 한강대로 405", 38, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);
                at.horizontalOverflow = HorizontalWrapMode.Wrap; at.verticalOverflow = VerticalWrapMode.Truncate;

                // 줄 사이 가는 선 (첫 줄 위는 없음)
                var sep = FindOrCreate(row.transform, "Sep");
                SetRect(RT(sep), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-96, 2));
                Img(sep, null, Line, Image.Type.Simple).raycastTarget = false;
                sep.SetActive(i > 0);
                row.SetActive(false);
                rows[i] = b;
            }
            panel.SetActive(false);
            return panel;
        }
    }
}
