using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 채팅 버블 레이아웃 헬퍼
/// Instagram/WhatsApp 스타일의 메시지 버블 자동 크기 조절
///
/// 참고:
/// - WChatPanel (github.com/warmtrue/WChatPanel)
/// - Unity UI Extensions (unity-ui-extensions.github.io)
/// - Stream Chat SDK (getstream.io/chat/sdk/unity/)
/// </summary>
public static class ChatBubbleLayoutHelper
{
    // 버블 설정 상수
    public const float MAX_BUBBLE_WIDTH_RATIO = 0.7f; // 화면 너비의 70%
    public const float MIN_BUBBLE_WIDTH = 60f;
    public const float BUBBLE_PADDING_H = 14f; // 좌우 패딩
    public const float BUBBLE_PADDING_V = 10f; // 상하 패딩
    public const int FONT_SIZE = 15;
    public const float LINE_HEIGHT = 22f;

    /// <summary>
    /// 메시지 버블 생성 및 크기 자동 조절
    /// ContentSizeFitter + LayoutElement 조합으로 텍스트 길이에 맞게 버블 크기 조절
    /// </summary>
    public static void SetupBubble(GameObject bubbleObj, string content, bool isMine, float screenWidth)
    {
        RectTransform bubbleRect = bubbleObj.GetComponent<RectTransform>();
        if (bubbleRect == null) return;

        // 최대 너비 계산
        float maxWidth = screenWidth * MAX_BUBBLE_WIDTH_RATIO;

        // 텍스트 컴포넌트 찾기
        Text contentText = bubbleObj.GetComponentInChildren<Text>();
        if (contentText == null)
        {
            contentText = FindChildByName<Text>(bubbleObj.transform, "ContentText");
        }

        if (contentText != null)
        {
            contentText.text = content;

            // 텍스트 설정
            contentText.fontSize = FONT_SIZE;
            contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;

            // 텍스트 선호 너비 계산
            float preferredWidth = contentText.preferredWidth + (BUBBLE_PADDING_H * 2);
            float actualWidth = Mathf.Clamp(preferredWidth, MIN_BUBBLE_WIDTH, maxWidth);

            // LayoutElement 설정
            LayoutElement layoutElement = contentText.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = contentText.gameObject.AddComponent<LayoutElement>();

            layoutElement.preferredWidth = actualWidth - (BUBBLE_PADDING_H * 2);
        }

        // 정렬 설정
        HorizontalLayoutGroup hlg = bubbleObj.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childAlignment = isMine ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        // ContentSizeFitter가 버블 크기 자동 조절
        ContentSizeFitter csf = bubbleObj.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = bubbleObj.AddComponent<ContentSizeFitter>();

        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 강제 레이아웃 업데이트
        LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
    }

    /// <summary>
    /// 메시지 버블 컨테이너 설정
    /// (버블 + 시간 + 읽음 표시를 포함하는 전체 행)
    /// </summary>
    public static void SetupMessageRow(GameObject rowObj, bool isMine)
    {
        RectTransform rowRect = rowObj.GetComponent<RectTransform>();
        if (rowRect == null) return;

        // 전체 행 설정
        HorizontalLayoutGroup hlg = rowObj.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
            hlg = rowObj.AddComponent<HorizontalLayoutGroup>();

        hlg.childAlignment = isMine ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        hlg.spacing = 8f;
        hlg.padding = isMine
            ? new RectOffset(80, 12, 2, 2)
            : new RectOffset(12, 80, 2, 2);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;

        // 높이 자동 조절
        ContentSizeFitter csf = rowObj.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = rowObj.AddComponent<ContentSizeFitter>();

        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 너비는 부모에 맞춤
        LayoutElement le = rowObj.GetComponent<LayoutElement>();
        if (le == null)
            le = rowObj.AddComponent<LayoutElement>();

        le.flexibleWidth = 1;
    }

    /// <summary>
    /// 시간 표시 포맷
    /// </summary>
    public static string FormatTime(string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return "";
        if (!System.DateTime.TryParse(dateStr, out System.DateTime date)) return "";

        // 오늘이면 시간만, 아니면 날짜+시간
        if (date.Date == System.DateTime.Today)
        {
            return date.ToString("tt h:mm").Replace("AM", "오전").Replace("PM", "오후");
        }
        else if (date.Date == System.DateTime.Today.AddDays(-1))
        {
            return "어제 " + date.ToString("tt h:mm").Replace("AM", "오전").Replace("PM", "오후");
        }
        else
        {
            return date.ToString("M/d tt h:mm").Replace("AM", "오전").Replace("PM", "오후");
        }
    }

    /// <summary>
    /// 상대 시간 표시 (대화 목록용)
    /// </summary>
    public static string GetRelativeTime(string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return "";
        if (!System.DateTime.TryParse(dateStr, out System.DateTime date)) return dateStr;

        System.TimeSpan diff = System.DateTime.Now - date;

        if (diff.TotalMinutes < 1) return "방금";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}분 전";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}시간 전";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}일 전";
        if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}주 전";

        return date.ToString("M월 d일");
    }

    /// <summary>
    /// 스크롤을 맨 아래로 이동 (새 메시지 입력 후)
    /// </summary>
    public static void ScrollToBottom(ScrollRect scrollRect)
    {
        if (scrollRect == null) return;

        Canvas.ForceUpdateCanvases();
        scrollRect.normalizedPosition = Vector2.zero;
    }

    /// <summary>
    /// 스크롤 영역 Content 레이아웃 설정
    /// </summary>
    public static void SetupScrollContent(RectTransform content)
    {
        if (content == null) return;

        // VerticalLayoutGroup 설정
        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
            vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.spacing = 4f;
        vlg.padding = new RectOffset(0, 0, 10, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        // ContentSizeFitter 설정
        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = content.gameObject.AddComponent<ContentSizeFitter>();

        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 앵커 설정 (상단 스트레치)
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
    }

    /// <summary>
    /// 대화 목록 아이템 레이아웃 설정
    /// </summary>
    public static void SetupConversationItem(RectTransform itemRect, float height = 76f)
    {
        if (itemRect == null) return;

        LayoutElement le = itemRect.GetComponent<LayoutElement>();
        if (le == null)
            le = itemRect.gameObject.AddComponent<LayoutElement>();

        le.preferredHeight = height;
        le.minHeight = height;
        le.flexibleWidth = 1;
    }

    /// <summary>
    /// 자식 이름으로 컴포넌트 찾기
    /// </summary>
    public static T FindChildByName<T>(Transform parent, string name) where T : Component
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                T comp = child.GetComponent<T>();
                if (comp != null) return comp;
            }

            // 재귀 탐색
            T found = FindChildByName<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 하트 좋아요 애니메이션 생성
    /// </summary>
    public static void ShowHeartAnimation(RectTransform parent, Color heartColor)
    {
        // 하트 오브젝트 생성
        GameObject heartObj = new GameObject("HeartAnim");
        heartObj.transform.SetParent(parent, false);

        RectTransform heartRect = heartObj.AddComponent<RectTransform>();
        heartRect.anchorMin = new Vector2(0.5f, 0.5f);
        heartRect.anchorMax = new Vector2(0.5f, 0.5f);
        heartRect.sizeDelta = new Vector2(40, 40);

        Text heartText = heartObj.AddComponent<Text>();
        heartText.text = "♥";
        heartText.fontSize = 32;
        heartText.fontStyle = FontStyle.Bold;
        heartText.color = heartColor;
        heartText.alignment = TextAnchor.MiddleCenter;
        heartText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 애니메이션 시작 (2초 후 삭제)
        var animator = heartObj.AddComponent<HeartAnimator>();
        animator.StartAnimation();
    }
}

/// <summary>
/// 하트 애니메이션 컴포넌트
/// </summary>
public class HeartAnimator : MonoBehaviour
{
    private float duration = 1.5f;
    private float elapsed = 0f;
    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private Vector3 startScale = Vector3.zero;
    private Vector3 endScale = Vector3.one * 1.2f;

    public void StartAnimation()
    {
        rect = GetComponent<RectTransform>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        rect.localScale = startScale;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        if (t < 0.3f)
        {
            // Pop in (0-0.3)
            float popT = t / 0.3f;
            rect.localScale = Vector3.Lerp(startScale, endScale, EaseOutBack(popT));
            canvasGroup.alpha = popT;
        }
        else if (t < 0.7f)
        {
            // Hold (0.3-0.7)
            rect.localScale = endScale;
            canvasGroup.alpha = 1f;
        }
        else if (t < 1f)
        {
            // Fade out (0.7-1.0)
            float fadeT = (t - 0.7f) / 0.3f;
            canvasGroup.alpha = 1f - fadeT;
            rect.localScale = Vector3.Lerp(endScale, endScale * 0.8f, fadeT);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
