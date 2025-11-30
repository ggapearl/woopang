using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class CreateFilterToggle : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create AR Object Toggle")]
    public static void CreateToggle()
    {
        GameObject parent = Selection.activeGameObject;
        if (parent == null || parent.GetComponent<RectTransform>() == null)
        {
            Debug.LogError("Canvas나 Panel을 선택하고 실행해주세요.");
            return;
        }

        // 1. 토글 오브젝트 생성
        GameObject toggleObj = new GameObject("Toggle_AR_Objects");
        toggleObj.transform.SetParent(parent.transform, false);
        
        Toggle toggle = toggleObj.AddComponent<Toggle>();
        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(160, 50);

        // 2. 배경 이미지 (Background)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(toggleObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.9f, 0.9f, 0.9f, 1f); // 연한 회색
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // 둥근 모서리 (선택사항 - Sprite가 없으면 네모)
        // bgImage.sprite = ...; 

        // 3. 체크마크 (Checkmark)
        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(bgObj.transform, false);
        Image checkImage = checkObj.AddComponent<Image>();
        checkImage.color = new Color(0.2f, 0.8f, 0.2f, 1f); // 녹색
        RectTransform checkRect = checkObj.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.1f, 0.1f);
        checkRect.anchorMax = new Vector2(0.9f, 0.9f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;

        // 4. 라벨 (Label)
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(toggleObj.transform, false);
        Text labelText = labelObj.AddComponent<Text>();
        labelText.text = "3D 오브젝트";
        labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.black;
        labelText.fontSize = 24;
        labelText.resizeTextForBestFit = true;
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(0, 0);
        labelRect.offsetMax = new Vector2(0, 0);

        // 토글 컴포넌트 연결
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = true;

        // FilterManager 연결을 위한 태그 설정 등은 수동으로 해야 함
        // 대신 FilterManager의 인스펙터에 넣을 수 있게 선택 상태 유지
        Selection.activeGameObject = toggleObj;

        Debug.Log("AR 오브젝트 토글이 생성되었습니다. FilterManager의 필터 목록에 연결해주세요.");
    }
#endif
}
