using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 토스트 메시지 — 화면 위쪽(상태 알약 아래)에 잠깐 떴다 사라지는 어두운 알약.
/// 2026-10: 예전 ToastManager 는 씬 어디에도 붙어 있지 않아 Instance 가 늘 null 이었다 — 앱 곳곳의 오류·안내가 한 번도 뜨지 않았다.
/// 이제 Redesign0926 적용이 Canvas 아래 'Toast0926'(창·도크 위에 그림)에 붙이고 견본 알약을 연결한다.
/// </summary>
public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [Header("설정")]
    public float defaultDuration = 2.5f;
    public float fadeInDuration = 0.2f;
    public float fadeOutDuration = 0.3f;
    public int maxToasts = 3;

    [Header("0926 알약 — 적용(Redesign0926)이 연결한다")]
    [SerializeField] private RectTransform container;   // 화면 위쪽 · 새 알약이 아래로 쌓인다
    [SerializeField] private GameObject template;        // 꺼 둔 견본 — Dot(Image) · Text(Text)

    [Header("종류별 점 색")]
    public Color successColor = new Color(0.30f, 0.78f, 0.47f, 1f);
    public Color errorColor = new Color(0.94f, 0.36f, 0.40f, 1f);
    public Color warningColor = new Color(0.98f, 0.75f, 0.27f, 1f);
    public Color infoColor = new Color(0.55f, 0.58f, 0.62f, 1f);

    public enum ToastType { Success, Error, Warning, Info }

    private class Live
    {
        public GameObject go;
        public CanvasGroup cg;
        public string message;
        public float until;
        public bool leaving;
    }

    private readonly List<Live> live = new List<Live>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (template != null) template.SetActive(false);
    }

    void OnDisable()
    {
        foreach (var t in live) if (t.go != null) Destroy(t.go);
        live.Clear();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 토스트 메시지 표시
    /// </summary>
    public void Show(string message, ToastType type = ToastType.Info, float duration = 0f)
    {
        if (string.IsNullOrEmpty(message) || !isActiveAndEnabled) return;
        if (container == null || template == null)
        {
            Debug.LogError("[Toast] 견본 알약이 연결되지 않음 — Redesign0926 적용 필요");
            return;
        }
        if (duration <= 0f) duration = defaultDuration + Mathf.Min(message.Length, 60) * 0.03f;   // 긴 글은 조금 더 오래

        // 같은 말이 떠 있으면 하나 더 띄우지 않고 시간만 늘린다 (같은 오류가 연달아 와도 하나만)
        foreach (var t in live)
        {
            if (t.leaving || t.message != message) continue;
            t.until = Mathf.Max(t.until, Time.unscaledTime + duration);
            return;
        }

        // 많으면 가장 오래된 것부터 걷는다 — 새 소식이 기다리지 않게
        int showing = 0;
        foreach (var t in live) if (!t.leaving) showing++;
        for (int i = 0; i < live.Count && showing >= maxToasts; i++)
        {
            if (live[i].leaving) continue;
            live[i].leaving = true;
            showing--;
        }

        var go = Instantiate(template, container);
        go.name = "Toast";
        var text = go.transform.Find("Text")?.GetComponent<Text>();
        if (text != null) text.text = message;
        var dot = go.transform.Find("Dot")?.GetComponent<Image>();
        if (dot != null) dot.color = ColorOf(type);
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        go.SetActive(true);

        var toast = new Live { go = go, cg = cg, message = message, until = Time.unscaledTime + fadeInDuration + duration };
        live.Add(toast);
        StartCoroutine(Run(toast));
    }

    // 편의 메서드들
    public void ShowSuccess(string message) => Show(message, ToastType.Success);
    public void ShowError(string message) => Show(message, ToastType.Error);
    public void ShowWarning(string message) => Show(message, ToastType.Warning);
    public void ShowInfo(string message) => Show(message, ToastType.Info);

    private IEnumerator Run(Live t)
    {
        var rt = (RectTransform)t.go.transform;
        for (float e = 0f; e < fadeInDuration; e += Time.unscaledDeltaTime)
        {
            if (t.go == null) yield break;
            float k = e / fadeInDuration;
            t.cg.alpha = k;
            rt.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k);
            yield return null;
        }
        if (t.go == null) yield break;
        t.cg.alpha = 1f;
        rt.localScale = Vector3.one;

        while (!t.leaving && Time.unscaledTime < t.until) yield return null;
        t.leaving = true;

        for (float e = 0f; e < fadeOutDuration; e += Time.unscaledDeltaTime)
        {
            if (t.go == null) break;
            t.cg.alpha = 1f - e / fadeOutDuration;
            yield return null;
        }
        live.Remove(t);
        if (t.go != null) Destroy(t.go);
    }

    private Color ColorOf(ToastType type)
    {
        switch (type)
        {
            case ToastType.Success: return successColor;
            case ToastType.Error: return errorColor;
            case ToastType.Warning: return warningColor;
            default: return infoColor;
        }
    }
}
