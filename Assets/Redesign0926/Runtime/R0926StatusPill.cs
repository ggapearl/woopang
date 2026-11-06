using System.Collections.Generic;
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
    private string lastRaw;
    private string lastText;
    private bool lastIcon;
    private CanvasGroup[] groups;

    // 지금 화면에 보이는 알약 — 하늘 칩(R0926SkyWeather)이 같은 자리(노치 아래)라 알약이 걷힐 때까지 칩이 기다린다
    private static readonly HashSet<R0926StatusPill> showing = new HashSet<R0926StatusPill>();
    private static readonly System.Predicate<R0926StatusPill> gone = p => p == null;
    public static bool AnyShowing { get { showing.RemoveWhere(gone); return showing.Count > 0; } }

    // 노치 아래 자리는 하나 — 두 알약(주변 장소 · AR/GPS 안내)이 같이 뜨면 한쪽만 보인다 (11-05 대표님 「'AR세션 확인중..' 과 '주변에서 ○○을 찾았습니다' 가 겹쳐서」).
    // '찾았어요'·'없어요' 같은 소식이 잠깐 앞에 서고, 그동안 AR·GPS 안내는 물러나 있다가 소식이 걷히면 다시 나온다.
    // '주변 장소 불러오는 중' 은 AR·GPS 안내에 자리를 내준다. 그림 조각은 R0926HideWhile 이 엇갈려 페이드한다
    private static readonly HashSet<R0926StatusPill> live = new HashSet<R0926StatusPill>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { showing.Clear(); live.Clear(); }

    /// <summary>보이려는 중인데 더 앞선 알약에 자리를 내줬다</summary>
    public bool Yielding
    {
        get
        {
            if (!Wants) return false;
            int me = Rank;
            foreach (var p in live) if (p != null && p != this && p.Rank > me && p.Wants) return true;
            return false;
        }
    }

    /// <summary>물러나는 알약이 아직 다 걷히지 않았다 — 다 걷힌 뒤에 나온다 (두 글자가 반투명으로 겹쳐 보이지 않게)</summary>
    public bool WaitingForSlot
    {
        get
        {
            foreach (var p in live) if (p != null && p != this && p.partAlpha > 0.02f && p.Yielding) return true;
            return false;
        }
    }

    internal float partAlpha;   // 그림 조각(R0926HideWhile)이 지금 보이는 정도

    private bool Wants => isActiveAndEnabled && Alpha() > 0.05f;
    private int Rank => source == null ? 1 : source.CurrentCount > 0 || source.ShowingNoData ? 2 : 0;

    private void OnEnable()
    {
        lastCount = -1;
        lastRaw = null;
        lastText = null;
        groups = null;
        live.Add(this);
    }

    private void OnDisable() { showing.Remove(this); live.Remove(this); }

    private void LateUpdate()
    {
        if (Alpha() > 0.05f) showing.Add(this); else showing.Remove(this);
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
        string raw = text.text;
        if (raw == lastRaw && hasIcon == lastIcon) return;
        lastRaw = raw;
        // 끝의 점(... 이 하나씩 늘어나는 애니메이션)은 빼고 비교 — 점마다 알약이 들썩이지 않게.
        // LoadingManager 는 아직 안 나온 점을 투명 색 태그로 감싸 보낸다 → 태그를 걷어야 끝의 점이 떨어진다 (2026-10-06: 점 셋일 때만 알약이 줄던 것)
        string plain = StripTags(raw);
        string core = plain.TrimEnd('.', ' ');
        if (core == lastText && hasIcon == lastIcon) return;
        lastText = core;
        lastIcon = hasIcon;
        if (icon != null && icon.gameObject.activeSelf != hasIcon) icon.gameObject.SetActive(hasIcon);
        // 알약 폭을 글자에 맞춘다 — [여백][그림][틈][글자][여백]
        float iw = hasIcon && icon != null ? icon.sizeDelta.x + gap : 0f;
        bool dots = source == null || core.Length != plain.Length;
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

    // 부모 CanvasGroup 까지 곱한 실제 투명도 — ObjectCountUI 는 끄지 않고 alpha 로 숨긴다
    private float Alpha()
    {
        if (groups == null) groups = GetComponentsInParent<CanvasGroup>(true);
        float a = 1f;
        foreach (var g in groups)
        {
            if (g == null || !g.enabled) continue;
            a *= g.alpha;
            if (g.ignoreParentGroups) break;
        }
        return a;
    }

    private static string StripTags(string s)
    {
        if (s.IndexOf('<') < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        bool inTag = false;
        foreach (char c in s)
        {
            if (c == '<') inTag = true;
            else if (c == '>' && inTag) inTag = false;
            else if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }

    private static float BackOut(float x)
    {
        const float c1 = 2.2f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
