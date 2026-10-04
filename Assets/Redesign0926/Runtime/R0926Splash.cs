using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 시작화면 — "SEE THE UNSEEN". 어두운 화면을 렌즈가 훑고 지나가면 그 나라의 숨은 문양이 드러나고,
/// 강아지 엠블럼 · WOOPANG · SEE THE UNSEEN · 그 나라 말 한 줄이 차례로 뜬다 (약 3.7초).
/// 그동안 뒤에서는 원래대로 AR·데이터가 준비된다. 끝나면 사라지고, 아직 준비 중이면 원래 로딩 화면이 이어진다.
/// 앱을 새로 켤 때 한 번만 뜬다 (R0926SplashBoot 가 켠다). 0.8초 뒤부터는 눌러서 건너뛸 수 있다.
/// 문양은 기기 언어에 맞는 한 장만 Resources 에서 불러오고, 끝나면 메모리에서 내린다.
/// </summary>
public class R0926Splash : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private CanvasGroup root;
    [SerializeField] private Image background;
    [SerializeField] private RectTransform lens;      // 원형 마스크 — 안쪽만 문양이 보인다
    [SerializeField] private RawImage motif;          // 렌즈의 자식이지만 화면에 고정
    [SerializeField] private RawImage motifFaint;     // 렌즈 밖 — 같은 문양을 옅게 (완전한 검정 대신)
    [SerializeField] private Image ring;              // 렌즈 테두리 (흰색)
    [SerializeField] private Image dimmer;
    [SerializeField] private CanvasGroup emblem;
    [SerializeField] private CanvasGroup wordmark;
    [SerializeField] private CanvasGroup copy;
    [SerializeField] private Text copyText;
    [SerializeField] private RectTransform bar;
    [SerializeField] private Image barImage;
    [SerializeField] private CanvasGroup line;
    [SerializeField] private Text lineText;
    [SerializeField] private float motifAspect = 1080f / 2340f;
    // 켜면 기기 언어별 문양·문구. 꺼 두면 모두 영어(우주) 한 가지 (2026-09-29 대표님 지시로 통일).
    // 켤 땐 Splash0926_other 의 motif_kr/jp/cn/es.jpg 를 Resources/Splash0926 로 다시 옮길 것.
    [Tooltip("켜면 기기 언어별 문양·문구, 끄면 모두 영어(우주) 한 가지")]
    [SerializeField] private bool perLanguage = false;
    [SerializeField] private float endAt = 3.7f;
    [SerializeField] private float fadeOut = 0.45f;
    [Tooltip("마지막 장면에서 화면이 고르게 돌 때까지 기다리는 최대 시간 — 로딩 멈칫 중에 사라지면 뚝 끊겨 보였다")]
    [SerializeField] private float maxWait = 4f;
    [SerializeField] private Image nebula;            // 숨쉬듯 밝아졌다 어두워지는 성운 (별은 R0926StarField 가 스스로)

    private struct Variant { public string key, accent, bg, text; public float dim, faint; }

    // 시안(우팡_0926_로고_시작화면_시안.html)과 같은 값. faint = 렌즈가 지나가기 전 렌즈 밖에 옅게 비치는 정도
    // (UI 가 선형 색공간에서 섞여 알파가 눈에 더 밝게 보인다 — 0.06~0.09 가 약 30% 밝기)
    private static Variant Pick(string lang)
    {
        switch (lang)
        {
            case "ko": return new Variant { key = "kr", accent = "#86B8F0", bg = "#0B0D0F", dim = 0.64f, faint = 0.06f, text = "보이지 않던 곳이, 보인다" };
            case "ja": return new Variant { key = "jp", accent = "#D0452F", bg = "#0B0D0F", dim = 0.62f, faint = 0.08f, text = "見えなかったものが、見えてくる" };
            case "zh": return new Variant { key = "cn", accent = "#D4A73A", bg = "#0B0D0F", dim = 0.62f, faint = 0.08f, text = "看见，未曾看见的" };
            case "es": return new Variant { key = "es", accent = "#3B7BD4", bg = "#0B0D0F", dim = 0.62f, faint = 0.08f, text = "Descubre lo que no se ve" };
            default: return new Variant { key = "en", accent = "#D9B24A", bg = "#110D1C", dim = 0.22f, faint = 0.09f, text = "Find what’s hidden around you" };
        }
    }

    private Variant v;
    private Font defaultLineFont;
    private static Font zhFont;
    private float t;
    private bool skipping;
    private float skipFrom;
    private float fadeStart = -1f;   // 사라지기 시작한 시각 (-1 = 아직)
    private int smoothFrames;
    private float waited;
    private bool started;
    private int calmFrames;
    private float waitedStart;

    private void OnEnable()
    {
        BootOverlay.Showing = true;
        Setup(perLanguage ? R0926LocalizedText.Lang() : "en");
        t = 0f;
        skipping = false;
        fadeStart = -1f;
        smoothFrames = 0;
        waited = 0f;
        started = false;
        calmFrames = 0;
        waitedStart = 0f;
        Pose(0f);
    }

    private void OnDisable() => BootOverlay.Showing = false;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (!started)
        {
            // 앱이 막 켜진 몇 프레임은 초기화(파이어베이스·장소 캐시 읽기)로 무겁다 — 그동안은 우주 배경과 별만 두고,
            // 화면이 고르게 돌기 시작하면(또는 1.2초가 지나면) 렌즈를 움직인다. 예전엔 그 멈칫에 렌즈가 끊겨 보였다
            waitedStart += dt;
            calmFrames = dt < 0.034f ? calmFrames + 1 : 0;
            if (calmFrames < 4 && waitedStart < 1.2f) { Pose(0f); return; }
            started = true;
        }
        t += Mathf.Min(dt, 0.05f);   // 첫 프레임 로딩 멈춤이 애니메이션을 건너뛰지 않게
        if (fadeStart < 0f)
        {
            if (skipping) fadeStart = t;
            else if (t >= endAt)
            {
                // 마지막 장면에서 프레임이 고르게(0.045초 안쪽) 12번 이어질 때 사라지기 시작한다
                smoothFrames = dt < 0.045f ? smoothFrames + 1 : 0;
                waited += dt;
                if (smoothFrames >= 12 || waited >= maxWait) fadeStart = t;
            }
        }
        Pose(t);
        if (fadeStart >= 0f && t >= fadeStart + fadeOut) Finish();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (skipping || t < 0.8f) return;
        skipping = true;
        skipFrom = Mathf.Max(t, 0f);
    }

    private void Setup(string lang)
    {
        v = Pick(lang);
        if (background != null) background.color = Hex(v.bg);
        Color acc = Hex(v.accent);
        if (ring != null) ring.color = Color.white;
        if (barImage != null) barImage.color = acc;
        if (copyText != null)   // 자간 넓게 (legacy Text 에는 자간 설정이 없어 글자 사이를 띄운다)
            copyText.text = "S E E   T H E   <color=" + v.accent + ">U N S E E N</color>";
        if (lineText != null)
        {
            if (defaultLineFont == null) defaultLineFont = lineText.font;
            // 간체 '见' 은 앱 글꼴에 없어 그 글자만 기기 글꼴로 섞여 보인다 → 중국어 줄은 통째로 기기 중국어 글꼴로
            lineText.font = v.key == "cn" ? ZhFont() : defaultLineFont;
            lineText.text = v.text;
        }
        if (motif != null && motif.texture == null)
            motif.texture = Resources.Load<Texture2D>("Splash0926/motif_" + v.key)
                            ?? Resources.Load<Texture2D>("Splash0926/motif_en");   // 그 나라 문양이 빠져 있으면 영어(우주)
        if (motifFaint != null) motifFaint.texture = motif != null ? motif.texture : null;
    }

    private void Finish()
    {
        var tex = motif != null ? motif.texture : null;
        if (motif != null) motif.texture = null;
        if (motifFaint != null) motifFaint.texture = null;
        if (tex != null && Application.isPlaying) Resources.UnloadAsset(tex);
        gameObject.SetActive(false);
    }

    // ── 시간표 ──────────────────────────────────────────────
    // 0.0 어둠 · 0.3~1.4 렌즈가 왼→오른쪽으로 훑음 · 1.4~1.9 렌즈가 화면 밖으로 퍼짐
    // 1.5 엠블럼 · 1.9 WOOPANG · 2.3 SEE THE UNSEEN · 2.9 나라 말 한 줄 · 3.7 사라짐
    private void Pose(float time)
    {
        var rt = (RectTransform)transform;
        Vector2 size = rt.rect.size;
        float w = size.x, h = size.y;

        // 렌즈 — 지름은 화면 폭의 0.9. 훑는 동안 살짝 위아래로 흔들리며 지나간다
        float d0 = w * 0.9f;
        float sweep = Ease(Mathf.InverseLerp(0.3f, 1.4f, time));
        float grow = Ease(Mathf.InverseLerp(1.4f, 1.95f, time));
        Vector2 sweepPos = new Vector2(Mathf.Lerp(-w * 0.5f - d0 * 0.5f, w * 0.18f, sweep),
                                       h * 0.06f + Mathf.Sin(sweep * Mathf.PI) * h * 0.035f);
        Vector2 pos = Vector2.Lerp(sweepPos, Vector2.zero, grow);
        float diameter = Mathf.Lerp(d0, Mathf.Sqrt(w * w + h * h) * 1.15f, grow);
        if (lens != null)
        {
            lens.anchoredPosition = pos;
            lens.sizeDelta = new Vector2(diameter, diameter);
        }
        if (ring != null)
        {
            // 테두리는 마스크 밖 형제 — 안에 두면 바깥 절반이 잘린다
            ring.rectTransform.anchoredPosition = pos;
            ring.rectTransform.sizeDelta = new Vector2(diameter, diameter) * 1.03f;
            var c = ring.color;
            c.a = Mathf.Clamp01(Mathf.InverseLerp(0.25f, 0.45f, time)) * (1f - grow);
            ring.color = c;
        }

        // 문양은 화면에 붙어 있고(렌즈가 움직여도 제자리) 아주 천천히 떠다닌다 — 확대·이동
        float coverH = Mathf.Max(h, w / motifAspect);
        float ks = 1.07f + 0.025f * Mathf.Sin(time * 0.35f);
        var coverSize = new Vector2(coverH * motifAspect, coverH) * ks;
        Vector3 drift = rt.TransformVector(new Vector3(37f * Mathf.Sin(time * 0.21f), 26f * Mathf.Cos(time * 0.17f), 0f));
        if (motif != null)
        {
            motif.rectTransform.sizeDelta = coverSize;
            motif.rectTransform.position = rt.position + drift;
        }
        if (nebula != null)
        {
            var nc = nebula.color;
            nc.a = 0.28f + 0.3f * (0.5f + 0.5f * Mathf.Sin(time * 0.9f));
            nebula.color = nc;
            nebula.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(time * 0.45f));
        }
        if (motifFaint != null)
        {
            // 렌즈 밖도 완전한 검정이 아니라 문양이 옅게 — 첫 순간은 어둡게 두고 곧 스며 나온다
            motifFaint.rectTransform.sizeDelta = coverSize;
            motifFaint.rectTransform.position = rt.position + drift;
            var fc = motifFaint.color;
            fc.a = v.faint * Ease(Mathf.InverseLerp(0.05f, 0.5f, time));
            motifFaint.color = fc;
        }

        if (dimmer != null)
        {
            var c = dimmer.color;
            c.a = v.dim * Ease(Mathf.InverseLerp(1.5f, 2.2f, time));
            dimmer.color = c;
        }

        Appear(emblem, time, 1.5f, 0.5f, 0.9f);
        Appear(wordmark, time, 1.9f, 0.45f, 0.96f);
        Appear(copy, time, 2.3f, 0.5f, 1f);
        if (copy != null)
        {
            // 자간이 벌어지며 들어오는 느낌 — 가로로만 살짝 벌어진다
            float k = Ease(Mathf.InverseLerp(2.3f, 2.9f, time));
            copy.transform.localScale = new Vector3(Mathf.Lerp(0.86f, 1f, k), 1f, 1f);
        }
        if (bar != null) bar.localScale = new Vector3(Ease(Mathf.InverseLerp(2.5f, 2.95f, time)), 1f, 1f);
        Appear(line, time, 2.9f, 0.45f, 1f);

        if (root != null)
        {
            root.alpha = fadeStart < 0f ? 1f : 1f - Ease(Mathf.InverseLerp(fadeStart, fadeStart + fadeOut, time));
            root.blocksRaycasts = true;
        }
    }

    private static void Appear(CanvasGroup g, float time, float start, float dur, float fromScale)
    {
        if (g == null) return;
        float k = Ease(Mathf.InverseLerp(start, start + dur, time));
        g.alpha = k;
        if (fromScale < 1f) g.transform.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, k);
    }

    // iOS: PingFang · 안드로이드: Noto CJK · 에디터(윈도우): YaHei. 이름이 하나도 없으면 기기 기본 글꼴이 대신한다
    private static Font ZhFont()
    {
        if (zhFont == null)
            zhFont = Font.CreateDynamicFontFromOSFont(new[] { "PingFang SC", "Noto Sans CJK SC", "NotoSansCJK-Regular", "Noto Sans SC", "Droid Sans Fallback", "Microsoft YaHei" }, 44);
        return zhFont;
    }

    private static float Ease(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.black;

#if UNITY_EDITOR
    /// <summary>캡처용 — 그 언어·그 시각의 한 장면</summary>
    public void EditorPreview(string lang, float time)
    {
        if (motif != null) motif.texture = null;
        Setup(perLanguage ? lang : "en");   // 실제로 뜨는 것과 같게
        skipping = false;
        Pose(time);
    }

    public void EditorPreviewClear()
    {
        if (motif != null) motif.texture = null;
        if (motifFaint != null) motifFaint.texture = null;
        // 중국어 미리보기의 기기 글꼴은 씬에 저장되면 안 된다 (실행 중에 만든 글꼴이라 저장하면 빈 참조가 된다)
        if (lineText != null && defaultLineFont != null) lineText.font = defaultLineFont;
    }
#endif
}
