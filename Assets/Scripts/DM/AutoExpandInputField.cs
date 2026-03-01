using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InputField 자동 확장 헬퍼
/// 입력 내용에 따라 InputField 높이가 자동으로 늘어남 (카카오톡/인스타그램 스타일)
/// - InputField 자체 + InputArea 높이 연동 확장
/// - ScrollView bottom offset 조정으로 메시지/댓글을 위로 밀어올림
/// - maxHeight 초과 시 텍스트 내부 스크롤 가능
/// </summary>
public class AutoExpandInputField : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("최소 높이 (픽셀) — 0이면 원래 높이 사용")]
    public float minHeight = 0f;

    [Tooltip("최대 높이 (픽셀)")]
    public float maxHeight = 200f;

    [Tooltip("패딩 (상하 여백)")]
    public float padding = 16f;

    [Header("연동 대상 (자동 감지)")]
    [Tooltip("InputArea RectTransform (InputField의 부모 또는 조부모)")]
    public RectTransform inputAreaRect;

    [Tooltip("연동할 ScrollRect (확장 시 bottom offset 조정 + 스크롤 최하단 유지)")]
    public ScrollRect scrollRect;

    private InputField inputField;
    private RectTransform rectTransform;
    private Text textComponent;

    // 원래 높이 저장
    private float originalInputFieldHeight;
    private float originalInputAreaHeight;
    private float originalScrollViewBottomOffset;
    private float lastHeightDelta;

    private bool isInitialized;

    void Awake()
    {
        Initialize();
    }

    void Start()
    {
        // Start에서 한번 더 텍스트 설정 강제 적용
        // (Awake 이후 Unity가 직렬화 값으로 덮어쓸 수 있음)
        ApplyTextSettings();
    }

    void OnDestroy()
    {
        if (inputField != null)
            inputField.onValueChanged.RemoveListener(OnTextChanged);
    }

    /// <summary>
    /// 텍스트 컴포넌트 설정 강제 적용 (Wrap + MultiLine)
    /// </summary>
    private void ApplyTextSettings()
    {
        if (inputField == null) return;

        inputField.lineType = InputField.LineType.MultiLineNewline;

        textComponent = inputField.textComponent;
        if (textComponent != null)
        {
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Truncate;
            textComponent.supportRichText = false;
        }
    }

    private void Initialize()
    {
        if (isInitialized) return;

        inputField = GetComponent<InputField>();
        rectTransform = GetComponent<RectTransform>();

        if (inputField == null)
        {
            enabled = false;
            return;
        }

        textComponent = inputField.textComponent;
        ApplyTextSettings();

        originalInputFieldHeight = rectTransform.sizeDelta.y;

        if (minHeight <= 0 || minHeight < originalInputFieldHeight)
            minHeight = originalInputFieldHeight;

        lastHeightDelta = 0f;

        if (inputAreaRect == null)
            inputAreaRect = FindInputArea();

        if (inputAreaRect != null)
            originalInputAreaHeight = inputAreaRect.sizeDelta.y;

        if (scrollRect == null)
            scrollRect = FindScrollRect();

        if (scrollRect != null)
        {
            RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
                originalScrollViewBottomOffset = scrollRectTransform.offsetMin.y;
        }

        inputField.onValueChanged.RemoveListener(OnTextChanged);
        inputField.onValueChanged.AddListener(OnTextChanged);

        isInitialized = true;
    }

    private void Reinitialize()
    {
        if (minHeight <= 0 || minHeight < originalInputFieldHeight)
            minHeight = originalInputFieldHeight;

        lastHeightDelta = 0f;
    }

    private void OnTextChanged(string text)
    {
        // 텍스트 설정이 올바른지 확인 (Unity가 덮어쓸 수 있음)
        if (textComponent != null && textComponent.horizontalOverflow != HorizontalWrapMode.Wrap)
            ApplyTextSettings();

        UpdateHeight();
    }

    public void UpdateHeight()
    {
        if (textComponent == null || rectTransform == null) return;

        if (textComponent.horizontalOverflow != HorizontalWrapMode.Wrap)
            ApplyTextSettings();

        // TextGenerator를 사용하여 정확한 높이 계산
        float textRectWidth = textComponent.rectTransform.rect.width;
        float preferredHeight;

        if (textRectWidth > 0)
        {
            TextGenerationSettings settings = textComponent.GetGenerationSettings(
                new Vector2(textRectWidth, 0f));
            settings.generateOutOfBounds = false;

            TextGenerator generator = textComponent.cachedTextGeneratorForLayout;
            preferredHeight = generator.GetPreferredHeight(
                inputField.text, settings) / textComponent.pixelsPerUnit;
            preferredHeight += padding;
        }
        else
        {
            preferredHeight = textComponent.preferredHeight + padding;
        }

        float targetHeight = Mathf.Clamp(preferredHeight, minHeight, maxHeight);
        float newHeightDelta = targetHeight - minHeight;

        if (Mathf.Abs(newHeightDelta - lastHeightDelta) < 0.5f) return;

        lastHeightDelta = newHeightDelta;

        // 1. InputField 자체 높이 업데이트
        Vector2 sizeDelta = rectTransform.sizeDelta;
        sizeDelta.y = targetHeight;
        rectTransform.sizeDelta = sizeDelta;

        // 2. InputArea 높이도 연동 확장
        if (inputAreaRect != null)
        {
            Vector2 areaSizeDelta = inputAreaRect.sizeDelta;
            areaSizeDelta.y = originalInputAreaHeight + newHeightDelta;
            inputAreaRect.sizeDelta = areaSizeDelta;
        }

        // 3. ScrollView bottom offset 조정 (메시지/댓글을 위로 밀어올림)
        if (scrollRect != null)
        {
            RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
            {
                Vector2 scrollOffsetMin = scrollRectTransform.offsetMin;
                scrollOffsetMin.y = originalScrollViewBottomOffset + newHeightDelta;
                scrollRectTransform.offsetMin = scrollOffsetMin;
            }
        }

        // 4. 레이아웃 강제 업데이트
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        if (rectTransform.parent != null)
        {
            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        // 5. 스크롤 최하단 유지
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.normalizedPosition = Vector2.zero;
        }
    }

    public void ResetHeight()
    {
        lastHeightDelta = 0f;

        if (rectTransform != null)
        {
            Vector2 sizeDelta = rectTransform.sizeDelta;
            sizeDelta.y = minHeight;
            rectTransform.sizeDelta = sizeDelta;
        }

        if (inputAreaRect != null)
        {
            Vector2 areaSizeDelta = inputAreaRect.sizeDelta;
            areaSizeDelta.y = originalInputAreaHeight;
            inputAreaRect.sizeDelta = areaSizeDelta;
        }

        if (scrollRect != null)
        {
            RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
            {
                Vector2 scrollOffsetMin = scrollRectTransform.offsetMin;
                scrollOffsetMin.y = originalScrollViewBottomOffset;
                scrollRectTransform.offsetMin = scrollOffsetMin;
            }
        }
    }

    private RectTransform FindInputArea()
    {
        Transform parent = rectTransform.parent;
        for (int i = 0; i < 3 && parent != null; i++)
        {
            if (parent.name == "InputArea")
                return parent.GetComponent<RectTransform>();
            parent = parent.parent;
        }

        if (rectTransform.parent != null)
            return rectTransform.parent.GetComponent<RectTransform>();

        return null;
    }

    private ScrollRect FindScrollRect()
    {
        Transform searchRoot = inputAreaRect != null ? inputAreaRect.parent : rectTransform.parent;
        if (searchRoot == null) return null;

        for (int i = 0; i < searchRoot.childCount; i++)
        {
            ScrollRect sr = searchRoot.GetChild(i).GetComponent<ScrollRect>();
            if (sr != null) return sr;
        }

        return searchRoot.GetComponentInChildren<ScrollRect>(true);
    }

    public static AutoExpandInputField Setup(InputField inputField, float minH = 0f, float maxH = 200f)
    {
        if (inputField == null) return null;

        AutoExpandInputField autoExpand = inputField.GetComponent<AutoExpandInputField>();
        if (autoExpand == null)
            autoExpand = inputField.gameObject.AddComponent<AutoExpandInputField>();

        autoExpand.minHeight = minH;
        autoExpand.maxHeight = maxH;
        autoExpand.Reinitialize();
        autoExpand.ApplyTextSettings();

        return autoExpand;
    }
}
