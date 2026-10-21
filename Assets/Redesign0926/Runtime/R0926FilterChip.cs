using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 목록 시트의 필터 칩 모양 — 체크 동그라미 없이 칸 전체가 켜짐/꺼짐.
/// 켜짐: 그 분류 색으로 은은하게 · 흰 칩(3D·전체)은 꽉 찬 흰색 · 팔로잉만은 꽉 찬 옅은 분홍 · 꺼짐: 어둡게 · 반려견 '되는 곳만': 꽉 찬 노랑 + 앞의 점이 흰색으로 반짝.
/// 필터 로직(FilterManager)은 그대로 두고, 그 상태를 읽어 매 프레임 끝에 모양·이름만 다시 그린다.
/// 길게 누르면 '이것만' — 같은 칸의 일반 칩(공공데이터·교통·3D) 중 이것만 켠다.
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
    [Tooltip("분류 칩 — 누르면 고르기 목록이 펼쳐진다는 표시 (∨, 펼쳐지면 ∧)")]
    [FormerlySerializedAs("cycle")]
    [SerializeField] private Image drop;
    [SerializeField] private float longPress = 0.55f;

    private FilterManager fm;
    private R0926CategoryMenu menu;
    private string placedFor;
    private float placedW = -1f;
    private int baseFont;
    private bool pressing, fired, suppress;
    private float pressAt;

    public static readonly string[][] CatNames =
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
    public static readonly Color[] CatColors =
    {
        new Color(0.91f, 0.93f, 0.945f), new Color(0.31f, 0.55f, 1f), new Color(1f, 0.54f, 0.24f), new Color(0.69f, 0.42f, 0.94f),
        new Color(0.22f, 0.85f, 0.54f), new Color(0.61f, 0.48f, 1f), new Color(0.18f, 0.77f, 0.77f), new Color(0.96f, 0.77f, 0.26f),
    };
    // 10-21 대표님 — '사람'은 '모든 사람', '팔로잉'은 '팔로잉만'(살짝 분홍), 반려견 되는 곳만 볼 때는 '반려견만'
    private static readonly string[] P2PAll = { "모든 사람", "Everyone", "すべての人", "所有人", "Todos" };
    private static readonly string[] P2PFollowing = { "팔로잉만", "Following only", "フォロー中のみ", "仅关注", "Solo seguidos" };
    private static readonly string[] PetOnly = { "반려견만", "Pets only", "ペットのみ", "仅宠物", "Solo mascotas" };
    private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f);
    private static readonly Color FollowPink = new Color(1f, 0.80f, 0.87f);
    /// <summary>흰 칩 위 글자·점 — 시트 바탕색</summary>
    public static readonly Color InkOnWhite = new Color(0.078f, 0.090f, 0.102f);
    private const int MinFont = 38;   // 앱 최소 글자 (Redesign0926ApplyReadable.MinFont)

    private void Awake()
    {
        fm = FindAnyObjectByType<FilterManager>();
        menu = GetComponent<R0926CategoryMenu>();
        if (label != null) baseFont = label.fontSize;
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

        int st = toggle.isOn ? 1 : 0;   // 0 꺼짐 · 1 켜짐 · 2 강하게(반려견 되는 곳만)
        Color c = color;
        string text = Pick(names);
        bool arrow = false, solid = false;
        switch (kind)
        {
            case Kind.Category:
                int ci = fm != null ? Mathf.Clamp((int)fm.CategoryState, 0, CatNames.Length - 1) : 0;
                st = 1; c = CatColors[ci]; text = Pick(CatNames[ci]); arrow = true;
                break;
            case Kind.Pet:
                var ps = fm != null ? fm.PetState : FilterManager.PetFriendlyFilterState.All;
                st = ps == FilterManager.PetFriendlyFilterState.OnlyPetFriendly ? 2 : 1;
                if (st == 2) text = Pick(PetOnly);
                break;
            case Kind.P2P:
                var p2 = fm != null ? fm.P2PState : FilterManager.P2PFilterState.All;
                if (p2 == FilterManager.P2PFilterState.None) { st = 0; c = Pink; text = Pick(P2PAll); }
                else if (p2 == FilterManager.P2PFilterState.FollowingOnly) { st = 1; c = FollowPink; text = Pick(P2PFollowing); solid = true; }
                else { st = 1; c = Pink; text = Pick(P2PAll); }
                break;
        }
        float dim = toggle.interactable || suppress ? 1f : 0.35f;   // 카테고리를 고르면 FilterManager 가 공공·사람 칸을 잠근다
        // 흰 칩(3D·전체)이 켜지면 꽉 찬 흰색 — 반투명하면 회색으로 보여 켜진 건지 꺼진 건지 몰랐다 (10-17 대표님). 팔로잉만은 꽉 찬 옅은 분홍
        bool white = st == 1 && dim >= 1f && (solid || c.r > 0.85f && c.g > 0.85f && c.b > 0.85f);

        body.color = white ? new Color(c.r, c.g, c.b, 0.94f)
                   : st == 2 ? new Color(c.r, c.g, c.b, dim)
                   : st == 1 ? new Color(c.r, c.g, c.b, 0.26f * dim)
                   : new Color(1f, 1f, 1f, 0.035f * dim);
        label.color = white ? InkOnWhite
                    : st == 0 ? new Color(1f, 1f, 1f, 0.34f * dim) : new Color(1f, 1f, 1f, dim);
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
                dot.color = white ? InkOnWhite : st == 1 ? new Color(c.r, c.g, c.b, dim) : new Color(1f, 1f, 1f, 0.28f * dim);
                dot.rectTransform.localScale = Vector3.one;
            }
        }
        if (drop != null)
        {
            if (drop.enabled != arrow) drop.enabled = arrow;
            drop.color = white ? new Color(InkOnWhite.r, InkOnWhite.g, InkOnWhite.b, 0.75f) : new Color(1f, 1f, 1f, 0.75f * dim);
            bool open = menu != null && menu.IsOpen;
            drop.rectTransform.localEulerAngles = new Vector3(0f, 0f, open ? 180f : 0f);
        }

        // FilterManager 가 상태를 바꿀 때 예전 이름('애견동반(필수)' 등)을 써 넣는다 — 다르면 다시 짧은 이름으로
        if (label.text != text) label.text = text;
        float w = ((RectTransform)transform).rect.width;
        if ((placedFor != text || !Mathf.Approximately(placedW, w)) && dot != null)
        {
            placedFor = text;
            placedW = w;
            Place(arrow, w);
        }
    }

    /// <summary>점과 글자(분류 칩은 ∨ 까지)를 한 묶음으로 가운데에 — 칸보다 길면(긴 언어) 글자를 앱 최소 크기까지 줄인다</summary>
    private void Place(bool arrow, float chipW)
    {
        float gap = 18f, dw = dot.rectTransform.sizeDelta.x;
        float aw = arrow && drop != null ? drop.rectTransform.sizeDelta.x + 12f : 0f;
        if (baseFont > 0)
        {
            label.fontSize = baseFont;
            float room = chipW - 56f - dw - gap - aw;
            float pw = label.preferredWidth;
            if (room > 0f && pw > room) label.fontSize = Mathf.Max(MinFont, Mathf.FloorToInt(baseFont * room / pw));
        }
        float lw = label.preferredWidth;
        float left = -(dw + gap + lw + aw) / 2f;
        dot.rectTransform.anchoredPosition = new Vector2(left + dw / 2f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(left + dw + gap + lw / 2f, 0f);
        if (drop != null) drop.rectTransform.anchoredPosition = new Vector2(left + dw + gap + lw + 12f + (aw - 12f) / 2f, 0f);
    }

    public static string Pick(string[] arr)
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
