using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class StyleDistanceSlider : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Style Selected Slider (Force)")]
    public static void ApplyStyleToSelected()
    {
        GameObject target = Selection.activeGameObject;

        if (target == null)
        {
            EditorUtility.DisplayDialog("오류", "Hierarchy에서 DistanceSliderUI를 선택하고 실행해주세요.", "확인");
            return;
        }

        Slider slider = target.GetComponent<Slider>();
        if (slider == null)
        {
            // 부모나 자식일 수 있으니 찾아봄
            slider = target.GetComponentInParent<Slider>();
            if (slider == null) slider = target.GetComponentInChildren<Slider>();
        }

        if (slider == null)
        {
            EditorUtility.DisplayDialog("오류", "선택한 오브젝트에 Slider 컴포넌트가 없습니다.", "확인");
            return;
        }

        GameObject sliderObj = slider.gameObject;
        Undo.RecordObject(sliderObj, "Style Slider");

        // 1. Background (높이 80)
        Transform bg = sliderObj.transform.Find("Background");
        if (bg != null)
        {
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(1, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(0, 80f);
            bgRect.anchoredPosition = Vector2.zero;
        }

        // 2. Fill Area (높이 80)
        Transform fillArea = sliderObj.transform.Find("Fill Area");
        if (fillArea != null)
        {
            RectTransform areaRect = fillArea.GetComponent<RectTransform>();
            areaRect.anchorMin = new Vector2(0, 0.5f);
            areaRect.anchorMax = new Vector2(1, 0.5f);
            areaRect.pivot = new Vector2(0.5f, 0.5f);
            areaRect.sizeDelta = new Vector2(-20f, 80f);
            areaRect.anchoredPosition = Vector2.zero;

            Transform fill = fillArea.Find("Fill");
            if (fill != null)
            {
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                fillRect.sizeDelta = Vector2.zero; // 꽉 채우기
            }
        }

        // 3. Handle (120x120)
        Transform handleArea = sliderObj.transform.Find("Handle Slide Area");
        if (handleArea != null)
        {
            RectTransform areaRect = handleArea.GetComponent<RectTransform>();
            areaRect.anchorMin = new Vector2(0, 0.5f);
            areaRect.anchorMax = new Vector2(1, 0.5f);
            areaRect.pivot = new Vector2(0.5f, 0.5f);
            areaRect.sizeDelta = new Vector2(-20f, 80f); // 슬라이드 영역 높이도 맞춤
            areaRect.anchoredPosition = Vector2.zero;

            Transform handle = handleArea.Find("Handle");
            if (handle != null)
            {
                RectTransform handleRect = handle.GetComponent<RectTransform>();
                handleRect.sizeDelta = new Vector2(120f, 120f);

                Image handleImage = handle.GetComponent<Image>();
                if (handleImage != null)
                {
                    handleImage.color = new Color(1f, 0.2f, 0.2f, 1f);
                }
            }
        }

        // 강제 갱신 (Dirty 설정)
        EditorUtility.SetDirty(sliderObj);
        
        EditorUtility.DisplayDialog("완료", string.Format("{0} 스타일 적용 완료!\n(높이 80, 핸들 120)", sliderObj.name), "확인");
    }
#endif
}
