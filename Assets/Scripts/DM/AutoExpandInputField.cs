using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InputField 자동 확장 헬퍼
/// 입력 내용에 따라 InputField 높이가 자동으로 늘어남 (카카오톡/인스타그램 스타일)
/// </summary>
public class AutoExpandInputField : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("최소 높이 (픽셀)")]
    public float minHeight = 50f;

    [Tooltip("최대 높이 (픽셀)")]
    public float maxHeight = 150f;

    [Tooltip("패딩 (상하 여백)")]
    public float padding = 16f;

    private InputField inputField;
    private RectTransform rectTransform;
    private RectTransform textRectTransform;
    private Text textComponent;
    private float originalHeight;
    private LayoutElement layoutElement;

    void Awake()
    {
        inputField = GetComponent<InputField>();
        rectTransform = GetComponent<RectTransform>();

        if (inputField == null)
        {
            Debug.LogError("[AutoExpandInputField] InputField 컴포넌트가 없습니다.");
            enabled = false;
            return;
        }

        // 여러 줄 입력 가능하도록 설정
        inputField.lineType = InputField.LineType.MultiLineNewline;

        // 텍스트 컴포넌트 찾기
        textComponent = inputField.textComponent;
        if (textComponent != null)
        {
            textRectTransform = textComponent.GetComponent<RectTransform>();

            // 텍스트 줄바꿈 설정
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        }

        // LayoutElement 추가 (없으면)
        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        originalHeight = rectTransform.sizeDelta.y;
        if (minHeight <= 0) minHeight = originalHeight;

        layoutElement.minHeight = minHeight;
        layoutElement.preferredHeight = minHeight;

        // 값 변경 이벤트 연결
        inputField.onValueChanged.AddListener(OnTextChanged);
    }

    void OnDestroy()
    {
        if (inputField != null)
            inputField.onValueChanged.RemoveListener(OnTextChanged);
    }

    private void OnTextChanged(string text)
    {
        UpdateHeight();
    }

    /// <summary>
    /// 텍스트 내용에 맞게 높이 업데이트
    /// </summary>
    public void UpdateHeight()
    {
        if (textComponent == null || rectTransform == null) return;

        // 텍스트의 선호 높이 계산
        float preferredHeight = textComponent.preferredHeight + padding;

        // 최소/최대 범위 내로 제한
        float targetHeight = Mathf.Clamp(preferredHeight, minHeight, maxHeight);

        // LayoutElement 높이 업데이트
        if (layoutElement != null)
        {
            layoutElement.preferredHeight = targetHeight;
        }

        // RectTransform 직접 업데이트 (LayoutElement가 동작하지 않는 경우)
        Vector2 sizeDelta = rectTransform.sizeDelta;
        if (Mathf.Abs(sizeDelta.y - targetHeight) > 0.5f)
        {
            sizeDelta.y = targetHeight;
            rectTransform.sizeDelta = sizeDelta;
        }

        // 레이아웃 강제 업데이트
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        // 부모 레이아웃도 업데이트
        if (rectTransform.parent != null)
        {
            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }
    }

    /// <summary>
    /// 높이 리셋 (텍스트 비움 후)
    /// </summary>
    public void ResetHeight()
    {
        if (layoutElement != null)
            layoutElement.preferredHeight = minHeight;

        if (rectTransform != null)
        {
            Vector2 sizeDelta = rectTransform.sizeDelta;
            sizeDelta.y = minHeight;
            rectTransform.sizeDelta = sizeDelta;
        }
    }

    /// <summary>
    /// 외부에서 InputField에 자동 확장 기능 추가
    /// </summary>
    public static AutoExpandInputField Setup(InputField inputField, float minHeight = 50f, float maxHeight = 150f)
    {
        if (inputField == null) return null;

        AutoExpandInputField autoExpand = inputField.GetComponent<AutoExpandInputField>();
        if (autoExpand == null)
            autoExpand = inputField.gameObject.AddComponent<AutoExpandInputField>();

        autoExpand.minHeight = minHeight;
        autoExpand.maxHeight = maxHeight;

        return autoExpand;
    }
}
