using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 토스트 메시지 매니저
/// 앱 전체에서 사용자에게 간단한 피드백 메시지 표시
/// </summary>
public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [Header("설정")]
    public float defaultDuration = 2.5f;
    public float fadeInDuration = 0.2f;
    public float fadeOutDuration = 0.3f;
    public int maxToasts = 3;

    [Header("스타일")]
    public Color successColor = new Color(0.2f, 0.7f, 0.4f, 0.95f);
    public Color errorColor = new Color(0.85f, 0.3f, 0.3f, 0.95f);
    public Color warningColor = new Color(0.9f, 0.7f, 0.2f, 0.95f);
    public Color infoColor = new Color(0.3f, 0.3f, 0.35f, 0.95f);

    private Canvas toastCanvas;
    private Transform toastContainer;
    private Queue<ToastData> pendingToasts = new Queue<ToastData>();
    private List<GameObject> activeToasts = new List<GameObject>();

    public enum ToastType { Success, Error, Warning, Info }

    private struct ToastData
    {
        public string message;
        public ToastType type;
        public float duration;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateToastUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void CreateToastUI()
    {
        // Canvas 생성
        GameObject canvasObj = new GameObject("ToastCanvas");
        canvasObj.transform.SetParent(transform);

        toastCanvas = canvasObj.AddComponent<Canvas>();
        toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        toastCanvas.sortingOrder = 9500;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 컨테이너 (하단 중앙)
        GameObject containerObj = new GameObject("ToastContainer");
        containerObj.transform.SetParent(canvasObj.transform, false);

        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0);
        containerRect.anchorMax = new Vector2(0.5f, 0);
        containerRect.pivot = new Vector2(0.5f, 0);
        containerRect.anchoredPosition = new Vector2(0, 150);
        containerRect.sizeDelta = new Vector2(800, 300);

        VerticalLayoutGroup vlg = containerObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.LowerCenter;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = true;

        toastContainer = containerObj.transform;
    }

    /// <summary>
    /// 토스트 메시지 표시
    /// </summary>
    public void Show(string message, ToastType type = ToastType.Info, float duration = 0f)
    {
        if (duration <= 0f) duration = defaultDuration;

        ToastData data = new ToastData
        {
            message = message,
            type = type,
            duration = duration
        };

        if (activeToasts.Count >= maxToasts)
        {
            pendingToasts.Enqueue(data);
            return;
        }

        CreateToast(data);
    }

    // 편의 메서드들
    public void ShowSuccess(string message) => Show(message, ToastType.Success);
    public void ShowError(string message) => Show(message, ToastType.Error);
    public void ShowWarning(string message) => Show(message, ToastType.Warning);
    public void ShowInfo(string message) => Show(message, ToastType.Info);

    private void CreateToast(ToastData data)
    {
        GameObject toastObj = new GameObject("Toast");
        toastObj.transform.SetParent(toastContainer, false);

        RectTransform toastRect = toastObj.AddComponent<RectTransform>();
        toastRect.sizeDelta = new Vector2(600, 50);

        // 배경
        Image bgImage = toastObj.AddComponent<Image>();
        bgImage.color = GetColorForType(data.type);

        // 둥근 모서리용 스프라이트가 없으면 기본 배경
        bgImage.raycastTarget = false;

        // CanvasGroup (페이드용)
        CanvasGroup cg = toastObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // 레이아웃
        HorizontalLayoutGroup hlg = toastObj.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 12, 12);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        ContentSizeFitter csf = toastObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 아이콘 (타입별)
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(toastObj.transform, false);

        Text iconText = iconObj.AddComponent<Text>();
        iconText.text = GetIconForType(data.type);
        iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconText.fontSize = 20;
        iconText.color = Color.white;
        iconText.alignment = TextAnchor.MiddleCenter;

        LayoutElement iconLE = iconObj.AddComponent<LayoutElement>();
        iconLE.preferredWidth = 30;
        iconLE.preferredHeight = 30;

        // 텍스트
        GameObject textObj = new GameObject("MessageText");
        textObj.transform.SetParent(toastObj.transform, false);

        Text messageText = textObj.AddComponent<Text>();
        messageText.text = data.message;
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        messageText.fontSize = 16;
        messageText.color = Color.white;
        messageText.alignment = TextAnchor.MiddleLeft;

        LayoutElement textLE = textObj.AddComponent<LayoutElement>();
        textLE.flexibleWidth = 1;
        textLE.preferredHeight = 26;

        activeToasts.Add(toastObj);
        StartCoroutine(AnimateToast(toastObj, cg, data.duration));
    }

    private IEnumerator AnimateToast(GameObject toast, CanvasGroup cg, float duration)
    {
        // 페이드 인
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            if (toast == null) yield break;
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            yield return null;
        }
        cg.alpha = 1f;

        // 대기
        yield return new WaitForSeconds(duration);

        // 페이드 아웃
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            if (toast == null) yield break;
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        RemoveToast(toast);
    }

    private void RemoveToast(GameObject toast)
    {
        if (toast == null) return;
        activeToasts.Remove(toast);
        Destroy(toast);

        // 대기 중인 토스트 처리
        if (pendingToasts.Count > 0)
        {
            CreateToast(pendingToasts.Dequeue());
        }
    }

    private Color GetColorForType(ToastType type)
    {
        switch (type)
        {
            case ToastType.Success: return successColor;
            case ToastType.Error: return errorColor;
            case ToastType.Warning: return warningColor;
            default: return infoColor;
        }
    }

    private string GetIconForType(ToastType type)
    {
        switch (type)
        {
            case ToastType.Success: return "O"; // 체크 대신
            case ToastType.Error: return "X";
            case ToastType.Warning: return "!";
            default: return "i";
        }
    }
}
