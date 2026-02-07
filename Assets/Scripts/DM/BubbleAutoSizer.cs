using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BubbleContainer 높이를 텍스트에 맞게 자동 계산
///
/// Unity의 VLG+HLG 조합에서 Text.preferredHeight가 잘못된 폭으로 계산되는
/// 프리팹 에디터 레이아웃 버그를 우회합니다.
///
/// 작동 방식:
/// 1. sizeDelta.x (BubbleContainer 폭)에서 VLG padding을 빼서 텍스트 폭 계산
/// 2. TextGenerator.GetPreferredHeight로 정확한 텍스트 높이 계산
/// 3. LayoutElement.preferredHeight에 설정
/// 4. Root HLG (childControlHeight=true)가 이 값으로 BubbleContainer 높이 결정
///
/// [ExecuteAlways]로 에디터/런타임 모두에서 작동
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class BubbleAutoSizer : MonoBehaviour
{
    private RectTransform rectTransform;
    private LayoutElement layoutElement;
    private Text contentText;
    private VerticalLayoutGroup vlg;
    private string cachedText;
    private float cachedWidth;

    void OnEnable()
    {
        CacheComponents();
        RecalculateHeight();
    }

    void Update()
    {
        if (contentText == null) CacheComponents();
        if (contentText == null || rectTransform == null || layoutElement == null) return;

        float currentWidth = rectTransform.sizeDelta.x;
        string currentText = contentText.text;

        if (currentText != cachedText || Mathf.Abs(currentWidth - cachedWidth) > 1f)
        {
            RecalculateHeight();
        }
    }

    private void CacheComponents()
    {
        rectTransform = GetComponent<RectTransform>();
        layoutElement = GetComponent<LayoutElement>();
        vlg = GetComponent<VerticalLayoutGroup>();
        contentText = GetComponentInChildren<Text>();
    }

    private void RecalculateHeight()
    {
        if (rectTransform == null || contentText == null || layoutElement == null) return;
        if (string.IsNullOrEmpty(contentText.text)) return;

        float width = rectTransform.sizeDelta.x;
        if (width <= 0) return;

        if (vlg == null) vlg = GetComponent<VerticalLayoutGroup>();
        float padL = vlg != null ? vlg.padding.left : 16;
        float padR = vlg != null ? vlg.padding.right : 16;
        float padT = vlg != null ? vlg.padding.top : 8;
        float padB = vlg != null ? vlg.padding.bottom : 8;

        float textWidth = width - padL - padR;
        if (textWidth <= 0) return;

        // TextGenerator로 정확한 높이 계산 (올바른 폭 기준)
        // GetGenerationSettings가 scaleFactor = pixelsPerUnit로 자동 설정
        // (에디터: pixelsPerUnit=1, 런타임: Canvas scaleFactor 반영)
        TextGenerationSettings settings = contentText.GetGenerationSettings(
            new Vector2(textWidth, 0f));
        float textHeight = contentText.cachedTextGenerator.GetPreferredHeight(
            contentText.text, settings) / contentText.pixelsPerUnit;

        float targetHeight = textHeight + padT + padB;

        // LayoutElement.preferredHeight 업데이트
        if (Mathf.Abs(layoutElement.preferredHeight - targetHeight) > 0.5f)
        {
            layoutElement.preferredHeight = targetHeight;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        cachedText = contentText.text;
        cachedWidth = width;
    }
}
