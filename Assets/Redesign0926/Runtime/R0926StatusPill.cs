using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 노치 아래 작은 알약 — 'GPS 불러오는 중' · '주변 장소 불러오는 중' · '주변에서 N곳을 찾았어요'.
/// 불러오는 동안은 원호가 돌고, 찾으면 분홍 원이 톡 튀어나오며 체크가 그려지고 빛이 한 번 퍼진다.
/// 글자는 원래 코드(ObjectCountUI · LoadingManager)가 그대로 쓰고, 이건 모양·크기·움직임만 맡는다.
/// </summary>
public class R0926StatusPill : MonoBehaviour
{
    [Tooltip("비워 두면 늘 '불러오는 중' 모양 (LoadingManager 패널)")]
    [SerializeField] private ObjectCountUI source;
    [SerializeField] private Text text;
    [SerializeField] private RectTransform pill;
    [SerializeField] private RectTransform icon;
    [SerializeField] private Image spinner;
    [SerializeField] private Image okCircle;
    [SerializeField] private Image okCheck;
    [SerializeField] private Image okGlow;
    [SerializeField] private float padLeft = 26f;
    [SerializeField] private float padRight = 50f;
    [SerializeField] private float gap = 26f;

    private int lastCount = -1;
    private float okT = -1f;
    private string lastText;
    private bool lastIcon;

    private void OnEnable()
    {
        lastCount = -1;
        lastText = null;
    }

    private void LateUpdate()
    {
        if (text == null || pill == null) return;
        int count = source != null ? source.CurrentCount : 0;
        bool noData = source != null && source.ShowingNoData;
        bool loading = count == 0 && !noData;

        if (count > 0 && lastCount <= 0) okT = 0f;   // 막 찾았다 — 체크를 그린다
        lastCount = count;

        if (spinner != null)
        {
            if (spinner.enabled != loading) spinner.enabled = loading;
            if (loading) spinner.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 400f);
        }
        bool ok = count > 0;
        if (okCircle != null && okCircle.enabled != ok) okCircle.enabled = ok;
        if (okCheck != null && okCheck.enabled != ok) okCheck.enabled = ok;
        if (okGlow != null && okGlow.enabled != (ok && okT >= 0f)) okGlow.enabled = ok && okT >= 0f;
        if (ok && okT >= 0f)
        {
            okT += Time.unscaledDeltaTime;
            float pop = Mathf.Clamp01(okT / 0.5f);
            float s = pop >= 1f ? 1f : 0.4f + 0.6f * BackOut(pop);
            okCircle.rectTransform.localScale = Vector3.one * s;
            okCircle.color = new Color(okCircle.color.r, okCircle.color.g, okCircle.color.b, Mathf.Clamp01(pop * 2f));
            okCheck.fillAmount = Mathf.Clamp01((okT - 0.22f) / 0.42f);
            float g = Mathf.Clamp01((okT - 0.35f) / 1.1f);
            okGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.75f, 1f - (1f - g) * (1f - g));
            var gc = okGlow.color; gc.a = 0.65f * (1f - g); okGlow.color = gc;
            if (okT > 1.5f) { okT = -1f; okGlow.enabled = false; }
        }
        else if (ok)
        {
            okCircle.rectTransform.localScale = Vector3.one;
            okCheck.fillAmount = 1f;
        }

        bool hasIcon = loading || ok;
        // 끝의 점(... 이 하나씩 늘어나는 애니메이션)은 빼고 비교 — 점마다 알약이 들썩이지 않게
        string core = text.text.TrimEnd('.', ' ');
        if (core == lastText && hasIcon == lastIcon) return;
        lastText = core;
        lastIcon = hasIcon;
        if (icon != null && icon.gameObject.activeSelf != hasIcon) icon.gameObject.SetActive(hasIcon);
        // 알약 폭을 글자에 맞춘다 — [여백][그림][틈][글자][여백]
        float iw = hasIcon && icon != null ? icon.sizeDelta.x + gap : 0f;
        bool dots = source == null || core.Length != text.text.Length;
        float tw = text.cachedTextGeneratorForLayout.GetPreferredWidth(dots ? core + "..." : core, text.GetGenerationSettings(Vector2.zero)) / text.pixelsPerUnit;
        float left = hasIcon ? padLeft : padRight;
        float total = left + iw + tw + padRight;
        pill.sizeDelta = new Vector2(total, pill.sizeDelta.y);
        // 알약·그림·글자는 형제(같은 앵커) — 알약 왼쪽 끝을 기준으로 놓는다 (프리팹 인스턴스라 자식으로 옮길 수 없다)
        float x0 = pill.anchoredPosition.x - total * pill.pivot.x;
        float y = pill.anchoredPosition.y + (0.5f - pill.pivot.y) * pill.sizeDelta.y;
        var trt = text.rectTransform;
        trt.anchorMin = pill.anchorMin; trt.anchorMax = pill.anchorMax;
        trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(x0 + left + iw, y);
        trt.sizeDelta = new Vector2(tw + 4f, pill.sizeDelta.y);
        if (icon != null)
        {
            icon.anchorMin = pill.anchorMin; icon.anchorMax = pill.anchorMax;
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(x0 + padLeft + icon.sizeDelta.x / 2f, y);
        }
    }

    private static float BackOut(float x)
    {
        const float c1 = 2.2f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
