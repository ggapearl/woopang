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
    // 버블 설정 - MessagePanelManager 인스펙터에서 조절 가능
    // 기본값은 MessagePanelManager가 없을 때 사용됨 (폰트 60 기준)
    private const float DEFAULT_MAX_BUBBLE_WIDTH_RATIO = 0.82f;
    private const float DEFAULT_MIN_BUBBLE_WIDTH = 160f;
    private const float DEFAULT_BUBBLE_PADDING_H = 36f;
    private const float DEFAULT_BUBBLE_PADDING_V = 28f;
    private const int DEFAULT_FONT_SIZE = 60;
    private const float DEFAULT_LINE_HEIGHT = 78f;
    private const float DEFAULT_MIN_BUBBLE_HEIGHT = 0f; // 텍스트 높이에 맞게 자동 조절

    // 행 레이아웃 기본값
    private const float DEFAULT_ROW_SPACING = 8f;
    private const int DEFAULT_MY_PADDING_LEFT = 50;
    private const int DEFAULT_MY_PADDING_RIGHT = 14;
    private const int DEFAULT_OTHER_PADDING_LEFT = 14;
    private const int DEFAULT_OTHER_PADDING_RIGHT = 50;
    private const int DEFAULT_ROW_PADDING_VERTICAL = 4;

    // 버블 설정값 (프리팹에서 직접 설정, 여기는 기본값만 사용)
    public static float MAX_BUBBLE_WIDTH_RATIO =>
        MessagePanelManager.Instance != null ? MessagePanelManager.Instance.maxBubbleWidthRatio : DEFAULT_MAX_BUBBLE_WIDTH_RATIO;
    public static float MIN_BUBBLE_WIDTH =>
        MessagePanelManager.Instance != null ? MessagePanelManager.Instance.minBubbleWidth : DEFAULT_MIN_BUBBLE_WIDTH;
    public static float BUBBLE_PADDING_H => DEFAULT_BUBBLE_PADDING_H;
    public static float BUBBLE_PADDING_V => DEFAULT_BUBBLE_PADDING_V;
    public static int FONT_SIZE => DEFAULT_FONT_SIZE;
    public static float LINE_HEIGHT => DEFAULT_LINE_HEIGHT;
    public static float MIN_BUBBLE_HEIGHT => DEFAULT_MIN_BUBBLE_HEIGHT;

    // 행 레이아웃 (프리팹에서 직접 설정)
    public static float ROW_SPACING => DEFAULT_ROW_SPACING;
    public static int MY_PADDING_LEFT => DEFAULT_MY_PADDING_LEFT;
    public static int MY_PADDING_RIGHT => DEFAULT_MY_PADDING_RIGHT;
    public static int OTHER_PADDING_LEFT => DEFAULT_OTHER_PADDING_LEFT;
    public static int OTHER_PADDING_RIGHT => DEFAULT_OTHER_PADDING_RIGHT;
    public static int ROW_PADDING_VERTICAL => DEFAULT_ROW_PADDING_VERTICAL;

    // 채팅 Content 레이아웃 기본값
    private const float DEFAULT_CHAT_CONTENT_SPACING = 4f;
    private const int DEFAULT_CHAT_CONTENT_PADDING_VERTICAL = 10;

    // 채팅 Content 레이아웃 (프리팹에서 직접 설정)
    public static float CHAT_CONTENT_SPACING => DEFAULT_CHAT_CONTENT_SPACING;
    public static int CHAT_CONTENT_PADDING_VERTICAL => DEFAULT_CHAT_CONTENT_PADDING_VERTICAL;

    // TimeArea 내부 배치 설정 (Instagram DM 스타일)
    public static bool TIME_INSIDE_BUBBLE => true;
    public static float TIME_INSIDE_MARGIN_RIGHT => 8f;
    public static float TIME_INSIDE_MARGIN_BOTTOM => 4f;

    /// <summary>
    /// 메시지 버블 생성 및 크기 자동 조절
    /// ContentSizeFitter + LayoutElement 조합으로 텍스트 길이에 맞게 버블 크기 조절
    /// 참고: fontSize, lineSpacing 등 스타일은 프리팹 값을 유지, 텍스트 내용만 설정
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
            // 텍스트 내용만 설정 (fontSize, lineSpacing 등은 프리팹 값 유지)
            contentText.text = content;

            // 텍스트 선호 너비 계산
            float preferredWidth = contentText.preferredWidth + (BUBBLE_PADDING_H * 2);
            float actualWidth = Mathf.Clamp(preferredWidth, MIN_BUBBLE_WIDTH, maxWidth);

            // LayoutElement 설정 (너비만, 높이는 프리팹 값 유지)
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

        // 강제 레이아웃 업데이트
        LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
    }

    /// <summary>
    /// 메시지 버블 컨테이너 설정
    /// (버블 + 시간 + 읽음 표시를 포함하는 전체 행)
    /// 모든 값은 MessagePanelManager 인스펙터에서 조절 가능
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
        hlg.spacing = ROW_SPACING; // 인스펙터에서 조절 가능

        // 패딩 - 인스펙터에서 조절 가능
        hlg.padding = isMine
            ? new RectOffset(MY_PADDING_LEFT, MY_PADDING_RIGHT, ROW_PADDING_VERTICAL, ROW_PADDING_VERTICAL)
            : new RectOffset(OTHER_PADDING_LEFT, OTHER_PADDING_RIGHT, ROW_PADDING_VERTICAL, ROW_PADDING_VERTICAL);

        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;  // 자식 너비 제어 비활성화 (BubbleContainer의 ContentSizeFitter가 결정)
        hlg.childControlHeight = true;

        // 행 자체는 부모 너비에 맞춤 (내부 정렬용)
        // ContentSizeFitter 제거 (행은 부모 너비 사용, 내부 버블만 컨텐츠에 맞춤)
        ContentSizeFitter csf = rowObj.GetComponent<ContentSizeFitter>();
        if (csf != null)
            Object.DestroyImmediate(csf);

        // LayoutElement - 행은 부모 너비에 맞추고, 높이는 자동
        LayoutElement le = rowObj.GetComponent<LayoutElement>();
        if (le == null)
            le = rowObj.AddComponent<LayoutElement>();

        le.flexibleWidth = 1;  // 행은 부모 너비에 맞춤
        le.preferredWidth = -1;
        le.minHeight = -1;
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
    /// 이미 VLG가 있으면 씬에서 설정한 값을 존중함 (spacing, padding 덮어쓰지 않음)
    /// </summary>
    public static void SetupScrollContent(RectTransform content)
    {
        if (content == null) return;

        // VerticalLayoutGroup 설정 - 이미 있으면 기존 spacing/padding 유지
        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            // VLG가 없을 때만 추가하고 기본값 설정
            vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = CHAT_CONTENT_SPACING;
            vlg.padding = new RectOffset(0, 0, CHAT_CONTENT_PADDING_VERTICAL, CHAT_CONTENT_PADDING_VERTICAL);
        }
        // 이미 VLG가 있으면 spacing, padding은 씬 값 유지 (덮어쓰지 않음)

        // 레이아웃 동작 설정만 적용
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        // ContentSizeFitter 설정 - 이미 있으면 유지
        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // 앵커 설정 (상단 스트레치)
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
    }

    /// <summary>
    /// 대화 목록 아이템 레이아웃 설정
    /// </summary>
    public static void SetupConversationItem(RectTransform itemRect, float height = 140f) // 폰트 60 기준
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
    /// 대화 목록용 폰트 사이즈 (폰트 60 기준 조정)
    /// </summary>
    public const int CONVERSATION_TITLE_FONT_SIZE = 42;    // 사용자명
    public const int CONVERSATION_PREVIEW_FONT_SIZE = 36;  // 메시지 미리보기
    public const int CONVERSATION_TIME_FONT_SIZE = 28;     // 시간

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
