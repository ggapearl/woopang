using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 분류 칩 · 분류 고르기 칸의 숨 쉬는 테 (11-26 대표님 37번 「'전체' 로 있는 버전 내에는 적당히 해당버튼 외곽에서 어울릴만한 색으로 적당히 천천히
/// 펄스효과를 외곽선이 있도록 … '카페' 를 선택했다면 '전체' 가 아닌 '카페' 라고 발생해있고 여기서도 마찬가지로 겉에 적당히 해당 톤앤매너에 맞게 살짝의 펄스효과」).
/// 천천히(2.8초에 한 번) 진해졌다 옅어진다. 색은 그 분류 색을 조금 밝게(어두운 모카도 어두운 시트 위에서 보이게) · '전체'는 목록 창 네온과 같은 푸른빛.
/// 네온이 아닐 때는 그림 없는 테(R0926PulseRing), 네온일 때는 네온 줄(R0926WindowSkin 줄 — 흰 심 · 그 색 빛)이 숨 쉰다
/// (「'네온사인' 설정했을 때는 적절히 해당 카테고리 분류버튼이 잘 보이도록」).
/// category -1 = 지금 고른 분류를 따른다 (칩) · 0 이상 = 고르기 창의 그 분류 칸 — 고른 칸일 때만 숨 쉬고, 네온에선 나머지 칸도 그 색으로 옅게 빛난다.
/// </summary>
public class R0926ChipPulse : MonoBehaviour
{
    [Tooltip("네온이 아닐 때 숨 쉬는 테")]
    [SerializeField] private R0926PulseRing ring;
    [Tooltip("네온 줄 (R0926WindowSkin 줄) — 네온 모드에서만 그려진다")]
    [SerializeField] private Image neon;
    [Tooltip("-1 = 지금 고른 분류 (칩) · 0 이상 = 이 분류 칸 (CategoryFilterState 값)")]
    [SerializeField] private int category = -1;
    [SerializeField] private float period = 2.8f;
    [Tooltip("네온 — 고르지 않은 칸의 세기")]
    [SerializeField] private float idleNeon = 0.42f;
    [Tooltip("이 층이 안 보이면(알파 0) 쉰다 — 고르기 창")]
    [SerializeField] private CanvasGroup gate;

    private FilterManager fm;
    private R0926WindowSkin skin;
    private float nextAt;

    public R0926PulseRing Ring => ring;
    public Image Neon => neon;
    public int Category => category;

    private void OnEnable() => nextAt = 0f;

    private void LateUpdate()
    {
        if (gate != null && gate.alpha <= 0.001f) { nextAt = 0f; return; }
        // 느린 숨이라 초당 30번이면 충분 — 그릴 때마다 캔버스를 다시 묶는다
        float now = Time.unscaledTime;
        if (now < nextAt) return;
        nextAt = now + 1f / 30f;
        if (fm == null) fm = FindAnyObjectByType<FilterManager>();
        int cur = fm != null ? (int)fm.CategoryState : 0;
        int ci = category < 0 ? cur : category;
        bool sel = category < 0 || ci == cur;
        bool neonMode = R0926MenuStyle.Neon;
        Color c = ToneOf(ci);
        float b = Breath(period);

        if (ring != null)
        {
            bool show = sel && !neonMode;   // 네온이면 네온 줄이 숨 쉰다 — 테가 두 겹이 되지 않게
            if (ring.enabled != show) ring.enabled = show;
            if (show)
            {
                if (ring.color != c) ring.color = c;
                ring.Breath = b;
            }
        }
        if (neon != null && neonMode)   // 네온이 아니면 네온 줄은 그리지 않는다 — 건드리지 않는다
        {
            if (skin == null) skin = neon.GetComponent<R0926WindowSkin>();
            if (skin != null) skin.Glow = c;
            float a = sel ? Mathf.Lerp(0.5f, 1f, b) : idleNeon;
            var nc = neon.color;
            if (Mathf.Abs(nc.a - a) > 0.004f) { nc.a = a; neon.color = nc; }
        }
    }

    /// <summary>숨 0(내쉼) ~ 1(들이쉼) — 칩과 고르기 칸이 같은 박자로</summary>
    public static float Breath(float period) => 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / Mathf.Max(0.5f, period)));

    /// <summary>분류 테 색 — 분류 색을 조금 밝게 · '전체'는 목록 창 네온 빛과 같은 푸른빛</summary>
    public static Color ToneOf(int category)
    {
        if (category <= 0 || category >= R0926FilterChip.CatColors.Length) return R0926WindowSkin.Blue;
        var c = R0926FilterChip.CatColors[category];
        return Color.Lerp(new Color(c.r, c.g, c.b, 1f), Color.white, 0.2f);
    }
}
