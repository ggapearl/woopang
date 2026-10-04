using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 목록 시트의 필터 칩 모양 — 체크 동그라미 없이 칸 전체가 켜짐/꺼짐.
/// 켜짐: 그 분류 색으로 은은하게 · 꺼짐: 어둡게 · 반려견 '필수': 꽉 찬 노랑 + 앞의 점이 흰색으로 반짝 · 반려견 '제외': 어둡게 + 사선.
/// 필터 로직(FilterManager)은 그대로 두고, 그 상태를 읽어 매 프레임 끝에 모양·이름만 다시 그린다.
/// 길게 누르면 '이것만' — 같은 줄의 일반 칩(공공·교통·3D) 중 이것만 켠다.
/// </summary>
public class R0926FilterChip : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum Kind { Simple, Category, Pet, P2P }

    [SerializeField] private Kind kind;
    [SerializeField] private Color color = Color.white;
    [Tooltip("ko, en, ja, zh, es — Simple·Pet 칩의 짧은 이름")]
    [SerializeField] private string[] names;
    [SerializeField] private Toggle toggle;
    [SerializeField] private Image body;
    [SerializeField] private Text label;
    [SerializeField] private Image dot;
    [SerializeField] private Image strike;
    [Tooltip("분류 칩 — 누를 때마다 다음 분류로 넘어간다는 표시")]
    [SerializeField] private Image cycle;
    [SerializeField] private float longPress = 0.55f;

    private FilterManager fm;
    private string placedFor;
    private bool pressing, fired, suppress;
    private float pressAt;

    private static readonly string[][] CatNames =
    {
        new[] { "전체", "All", "すべて", "全部", "Todo" },
        new[] { "샵", "Shop", "ショップ", "商店", "Tienda" },
        new[] { "음식점", "Food", "飲食", "餐饮", "Comida" },
        new[] { "카페", "Cafe", "カフェ", "咖啡", "Café" },
        new[] { "공원", "Park", "公園", "公园", "Parque" },
        new[] { "화장실", "Toilet", "トイレ", "厕所", "Baño" },
        new[] { "스포츠", "Sports", "スポーツ", "运动", "Deporte" },
        new[] { "랜드마크", "Landmark", "名所", "地标", "Lugar" },
    };
    private static readonly Color[] CatColors =
    {
        new Color(0.91f, 0.93f, 0.945f), new Color(0.31f, 0.55f, 1f), new Color(1f, 0.54f, 0.24f), new Color(0.69f, 0.42f, 0.94f),
        new Color(0.22f, 0.85f, 0.54f), new Color(0.61f, 0.48f, 1f), new Color(0.18f, 0.77f, 0.77f), new Color(0.96f, 0.77f, 0.26f),
    };
    private static readonly string[] P2PAll = { "사람", "People", "ユーザー", "用户", "Personas" };
    private static readonly string[] P2PFollowing = { "팔로잉", "Following", "フォロー中", "关注", "Siguiendo" };
    private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f);

    private void Awake()
    {
        fm = FindAnyObjectByType<FilterManager>();
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (kind != Kind.Simple || toggle == null || !toggle.interactable) return;
        pressing = true;
        fired = false;
        pressAt = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData e) => pressing = false;
    public void OnPointerExit(PointerEventData e) => pressing = false;

    private void Update()
    {
        if (!pressing || fired || Time.unscaledTime - pressAt < longPress) return;
        fired = true;
        // 이것만 켠다 — 손을 뗄 때의 누름이 다시 끄지 않게 그동안 토글을 잠근다
        foreach (var chip in transform.parent.GetComponentsInChildren<R0926FilterChip>())
            if (chip.kind == Kind.Simple && chip.toggle != null && chip.toggle.interactable && chip.toggle.isOn != (chip == this))
                chip.toggle.isOn = chip == this;
        suppress = true;
        toggle.interactable = false;
    }

    private void LateUpdate()
    {
        if (suppress && !pressing) { suppress = false; if (toggle != null) toggle.interactable = true; }
        if (toggle == null || body == null || label == null) return;
        if (fm == null) fm = FindAnyObjectByType<FilterManager>();

        int st = toggle.isOn ? 1 : 0;   // 0 꺼짐 · 1 켜짐 · 2 강하게(반려견 필수)
        Color c = color;
        string text = Pick(names);
        bool struck = false;
        bool cycler = false;
        switch (kind)
        {
            case Kind.Category:
                int ci = fm != null ? Mathf.Clamp((int)fm.CategoryState, 0, CatNames.Length - 1) : 0;
                st = 1; c = CatColors[ci]; text = Pick(CatNames[ci]); cycler = true;
                break;
            case Kind.Pet:
                var ps = fm != null ? fm.PetState : FilterManager.PetFriendlyFilterState.All;
                st = ps == FilterManager.PetFriendlyFilterState.OnlyPetFriendly ? 2 : ps == FilterManager.PetFriendlyFilterState.All ? 1 : 0;
                struck = st == 0;
                break;
            case Kind.P2P:
                var p2 = fm != null ? fm.P2PState : FilterManager.P2PFilterState.All;
                if (p2 == FilterManager.P2PFilterState.None) { st = 0; c = Pink; text = Pick(P2PAll); }
                else if (p2 == FilterManager.P2PFilterState.FollowingOnly) { st = 1; c = Color.white; text = Pick(P2PFollowing); }
                else { st = 1; c = Pink; text = Pick(P2PAll); }
                break;
        }
        float dim = toggle.interactable || suppress ? 1f : 0.35f;   // 카테고리를 고르면 FilterManager 가 공공·사람 칸을 잠근다

        body.color = st == 2 ? new Color(c.r, c.g, c.b, dim)
                   : st == 1 ? new Color(c.r, c.g, c.b, 0.26f * dim)
                   : new Color(1f, 1f, 1f, 0.035f * dim);
        label.color = st == 0 ? new Color(1f, 1f, 1f, 0.34f * dim) : new Color(1f, 1f, 1f, dim);
        if (dot != null)
        {
            if (st == 2)
            {
                // 꽉 찬 칸 위에서 점이 묻히지 않게 — 흰 점이 반짝인다
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.9f);
                dot.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, k));
                dot.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.45f, k);
            }
            else
            {
                dot.color = st == 1 ? new Color(c.r, c.g, c.b, dim) : new Color(1f, 1f, 1f, 0.28f * dim);
                dot.rectTransform.localScale = Vector3.one;
            }
        }
        if (strike != null && strike.enabled != struck) strike.enabled = struck;
        if (cycle != null)
        {
            if (cycle.enabled != cycler) cycle.enabled = cycler;
            cycle.color = new Color(1f, 1f, 1f, 0.6f * dim);
        }

        // FilterManager 가 상태를 바꿀 때 예전 이름('반려견만' 등)을 써 넣는다 — 다르면 다시 짧은 이름으로
        if (label.text != text) label.text = text;
        if (placedFor != text && dot != null)
        {
            placedFor = text;
            // 점은 글자 바로 앞 — 점과 글자를 한 묶음으로 가운데에
            float w = label.preferredWidth;
            float gap = 18f, dw = dot.rectTransform.sizeDelta.x;
            float cw = cycler && cycle != null ? cycle.rectTransform.sizeDelta.x + 12f : 0f;
            float total = dw + gap + w + cw;
            float left = -total / 2f;
            dot.rectTransform.anchoredPosition = new Vector2(left + dw / 2f, 0f);
            label.rectTransform.anchoredPosition = new Vector2(left + dw + gap + w / 2f, 0f);
            if (cycle != null) cycle.rectTransform.anchoredPosition = new Vector2(left + dw + gap + w + 12f + (cw - 12f) / 2f, 0f);
        }
    }

    private static string Pick(string[] arr)
    {
        if (arr == null || arr.Length == 0) return "";
        int i;
        switch (R0926LocalizedText.Lang())
        {
            case "ko": i = 0; break;
            case "ja": i = 2; break;
            case "zh": i = 3; break;
            case "es": i = 4; break;
            default: i = 1; break;
        }
        return arr[Mathf.Min(i, arr.Length - 1)];
    }
}
