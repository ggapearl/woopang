using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 목록 줄을 누르면 묻는 창 「○○으로 안내할까요?」 (11-16 대표님 D4 「위치안내를 시작하기 전에 팝업이 발생해서 'ㅇㅇㅇ으로 안내할까요?' 라는 안내가
    /// 발생한 다음에 확인을 누르면 안내하도록」) — '삭제 요청' 묻는 창과 같은 카드(StyleDialog). 목록 바로 위 층 (도크 · 목록을 어둡게 덮는다).
    /// 왼쪽 취소(뒤로가기도) · 오른쪽 확인 · 바깥 누르기는 취소
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static void ApplyGuideAsk(Transform root, List<string> log)
        {
            var list = root.Find("ListPanel");
            var rows = root.GetComponentInChildren<R0926PlaceRows>(true);
            if (list == null || rows == null) { log.Add("안내 묻는 창: 목록 없음"); return; }

            var go = FindOrCreate(root, "GuideAsk0926");
            if (go.transform.GetSiblingIndex() != list.GetSiblingIndex() + 1)
                go.transform.SetSiblingIndex(go.transform.GetSiblingIndex() > list.GetSiblingIndex() ? list.GetSiblingIndex() + 1 : list.GetSiblingIndex());
            var rt = RT(go);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
            var dim = Img(go, null, new Color(0f, 0f, 0f, 0.5f), Image.Type.Simple);
            dim.raycastTarget = true;

            var box = FindOrCreate(go.transform, "Box0926");
            SetRect(RT(box), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040, 560));
            var text = FindOrCreate(box.transform, "Text");
            Txt(text, "이곳으로 안내할까요?", 44, Ink, TextAnchor.MiddleCenter, FontStyle.Normal).supportRichText = true;   // 장소 이름은 굵게 (R0926GuideAsk)
            Loc(text, "이곳으로 안내할까요?", "Guide to this place?", "ここへ案内しますか？", "要导航到这里吗？", "¿Guiar hasta aquí?");
            var buttons = FindOrCreate(box.transform, "Buttons0926");
            var no = FindOrCreate(buttons.transform, "Guide_NO");
            var yes = FindOrCreate(buttons.transform, "Guide_YES");
            Ensure<Image>(no);
            Ensure<Image>(yes);
            var nb = Ensure<Button>(no);
            var yb = Ensure<Button>(yes);
            StyleDialog(box, text, buttons, yes, no, new[] { "확인", "OK", "OK", "确定", "Aceptar" }, CancelWords, false);
            nb.targetGraphic = no.GetComponent<Image>();
            yb.targetGraphic = yes.GetComponent<Image>();
            Ensure<UITouchForwarder>(no);   // 다른 버튼과 같은 누름 소리
            Ensure<UITouchForwarder>(yes);
            Ensure<ClickButtonOnBack>(no);   // 뒤로가기 = 취소

            var ask = Ensure<R0926GuideAsk>(go);
            var aso = new SerializedObject(ask);
            aso.FindProperty("ask").objectReferenceValue = text.GetComponent<R0926LocalizedText>();
            aso.ApplyModifiedPropertiesWithoutUndo();
            ResetListeners(yb);
            UnityEventTools.AddPersistentListener(yb.onClick, ask.Yes);
            ResetListeners(nb);
            UnityEventTools.AddPersistentListener(nb.onClick, ask.No);
            var tap = Ensure<R0926TapToClose>(go);   // 바깥(어두운 곳)을 누르면 취소
            var tso = new SerializedObject(tap);
            tso.FindProperty("closeButton").objectReferenceValue = nb;
            tso.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);
            EditorUtility.SetDirty(go);

            var rso = new SerializedObject(rows);
            rso.FindProperty("guideAsk").objectReferenceValue = ask;
            rso.ApplyModifiedPropertiesWithoutUndo();
            log.Add("안내 묻는 창 (GuideAsk0926)");
        }
    }
}
