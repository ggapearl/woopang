using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 목록 시트의 필터 칩 모양 — 체크 동그라미 없이 칸 전체가 켜짐/꺼짐.
/// 켜짐: 그 분류 색으로 은은하게 · 흰 칩(전체)은 꽉 찬 흰색 · 3D 는 꽉 찬 보라 + 흰 글자 · 팔로잉만은 꽉 찬 옅은 분홍 · 꺼짐: 어둡게 · 애견동반: 켜면 꽉 찬 진한 노랑 + 어두운 글자(애견동반 되는 곳만), 끄면 다른 칩처럼 회색 — 두 가지뿐.
/// 칩마다 이름 앞에 그림 (10-27 대표님 「각 '필터' 에 있어서도 각각의 이모지가 … 동그라미가 아닌 이모지를 글자 앞에」) — 분류 칩은 고른 분류의 그림
/// (전체 네 칸 · 음식점 그릇 · 카페 모카 잔 …), 나머지는 칩마다 하나 (목록 줄·올리기 칩과 같은 r0926_c_* 표). 꺼진 칩의 그림은 흐리게. 사람 칩이 꺼지면 '주변 사람'.
/// 필터 로직(FilterManager)은 그대로 두고, 그 상태를 읽어 매 프레임 끝에 모양·이름만 다시 그린다.
/// 길게 누르면 '이것만' — 같은 칸의 일반 칩(공공데이터·교통·3D) 중 이것만 켠다. 목록 맨 위에 더하는 축제 / 보물 칩은 끼지 않는다.
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
    [Tooltip("켜지면 그 색으로 꽉 찬 칸 + 흰 글자 (3D — 10-23 대표님 '켜졌을 때 버튼배경 보라색')")]
    [SerializeField] private bool solidOn;
    [Tooltip("목록 맨 위에 더하는 칩(축제 / 보물) — 길게 누르기 '이것만'에 끼지 않는다")]
    [SerializeField] private bool modeChip;
    [Tooltip("분류 칩 — 분류마다 그림 (FilterManager.CategoryFilterState 순서, 0 = 전체)")]
    [SerializeField] private Sprite[] catIcons;
    [Tooltip("이름 앞 그림 (분류 칩 말고) — 없으면 예전 색 점")]
    [SerializeField] private Sprite icon;
    [Tooltip("한 색 그림 — 점처럼 칩 색으로 칠한다 (사람 모양). 끄면 그림 제 색 그대로")]
    [SerializeField] private bool tintIcon;
    [SerializeField] private float catIconSize = 46f;

    private FilterManager fm;
    private R0926CategoryMenu menu;
    private string placedFor;
    private float placedW = -1f;
    private int baseFont;
    private bool pressing, fired, suppress;
    private float pressAt;
    private Sprite dotSprite;
    private Vector2 dotSize;

    /// <summary>분류를 보여 주는 순서 (10-23 대표님) — 전체 · 음식점 · 카페 · 샵 · 공원 · 스포츠 · 화장실 · 랜드마크. 값은 CategoryFilterState</summary>
    public static readonly int[] CatOrder = { 0, 2, 3, 1, 4, 6, 5, 7 };

    // CategoryFilterState 순서 (보여 주는 순서는 CatOrder)
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
    // 분류 색은 DataManager.GetCategoryColor 하나 — AR 인디케이터·목록·올리기 칩과 같다 (10-25). 카페 모카 · 스포츠 빨강 (10-23). 전체는 옅은 흰색
    public static readonly Color[] CatColors =
    {
        new Color(0.91f, 0.93f, 0.945f), DataManager.GetCategoryColor("shop"), DataManager.GetCategoryColor("food"), DataManager.GetCategoryColor("cafe"),
        DataManager.GetCategoryColor("park"), DataManager.GetCategoryColor("toilet"), DataManager.GetCategoryColor("sport"), DataManager.GetCategoryColor("landmark"),
    };
    // 10-21 대표님 — '사람'은 '모든 사람', '팔로잉'은 '팔로잉만'(살짝 분홍) · 10-23 꺼지면 '주변 사람'
    private static readonly string[] P2PAll = { "모든 사람", "Everyone", "すべての人", "所有人", "Todos" };
    private static readonly string[] P2PNearby = { "주변 사람", "Nearby people", "近くの人", "附近的人", "Gente cerca" };
    private static readonly string[] P2PFollowing = { "팔로잉만", "Following only", "フォロー中のみ", "仅关注", "Solo seguidos" };
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
        if (dot != null) { dotSprite = dot.sprite; dotSize = dot.rectTransform.sizeDelta; }
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (kind != Kind.Simple || modeChip || toggle == null || !toggle.interactable) return;
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
            if (chip.kind == Kind.Simple && !chip.modeChip && chip.toggle != null && chip.toggle.interactable && chip.toggle.isOn != (chip == this))
                chip.toggle.isOn = chip == this;
        suppress = true;
        toggle.interactable = false;
    }

    private void LateUpdate()
    {
        if (suppress && !pressing) { suppress = false; if (toggle != null) toggle.interactable = true; }
        if (toggle == null || body == null || label == null) return;
        if (fm == null) fm = FindAnyObjectByType<FilterManager>();

        int st = toggle.isOn ? 1 : 0;   // 0 꺼짐 · 1 켜짐
        Color c = color;
        string text = Pick(names);
        bool arrow = false, solid = false;
        Sprite pic = icon;
        switch (kind)
        {
            case Kind.Category:
                int ci = fm != null ? Mathf.Clamp((int)fm.CategoryState, 0, CatNames.Length - 1) : 0;
                st = 1; c = CatColors[ci]; text = Pick(CatNames[ci]); arrow = true;
                if (catIcons != null && ci < catIcons.Length && catIcons[ci] != null) pic = catIcons[ci];
                break;
            case Kind.Pet:
                // 10-26 대표님 — '애견동반' 하나만: 꺼짐(회색 · 전부) / 켜짐(꽉 찬 진한 노랑 · 애견동반 되는 곳만). 이름은 그대로
                st = fm != null && fm.PetState == FilterManager.PetFriendlyFilterState.OnlyPetFriendly ? 1 : 0;
                solid = true;
                break;
            case Kind.P2P:
                var p2 = fm != null ? fm.P2PState : FilterManager.P2PFilterState.All;
                if (p2 == FilterManager.P2PFilterState.None) { st = 0; c = Pink; text = Pick(P2PNearby); }
                else if (p2 == FilterManager.P2PFilterState.FollowingOnly) { st = 1; c = FollowPink; text = Pick(P2PFollowing); solid = true; }
                else { st = 1; c = Pink; text = Pick(P2PAll); }
                break;
        }
        float dim = toggle.interactable || suppress ? 1f : 0.35f;   // 카테고리를 고르면 공공·사람 칸을 FilterManager 가 잠근다
        // 흰 칩(전체)이 켜지면 꽉 찬 흰색 — 반투명하면 회색으로 보여 켜진 건지 꺼진 건지 몰랐다 (10-17 대표님). 팔로잉만은 꽉 찬 옅은 분홍.
        // 3D 는 꽉 찬 보라에 흰 글자 (solidOn) · 애견동반은 꽉 찬 진한 노랑에 어두운 글자
        bool filled = st == 1 && dim >= 1f && (solid || solidOn || c.r > 0.85f && c.g > 0.85f && c.b > 0.85f);
        bool ink = filled && !solidOn;   // 밝은 바탕엔 어두운 글자·점

        body.color = filled ? new Color(c.r, c.g, c.b, solidOn ? 1f : 0.94f)
                   : st == 1 ? new Color(c.r, c.g, c.b, 0.26f * dim)
                   : new Color(1f, 1f, 1f, 0.035f * dim);
        label.color = ink ? InkOnWhite : filled ? Color.white
                    : st == 0 ? new Color(1f, 1f, 1f, 0.34f * dim) : new Color(1f, 1f, 1f, dim);
        if (dot != null)
        {
            SetDotSprite(pic);
            // 그림은 제 색 그대로 (꺼진 칩은 흐리게) — 같은 색으로 꽉 찬 칸(애견동반 켜짐 · 노랑 위 노란 발)에선 글자처럼 어두운 실루엣.
            // 점과 한 색 그림(사람)은 칩 색으로
            dot.color = pic != null && !tintIcon
                ? (ink && kind != Kind.Category ? InkOnWhite : new Color(1f, 1f, 1f, (st == 1 ? 1f : 0.42f) * dim))
                : ink ? InkOnWhite : filled ? Color.white : st == 1 ? new Color(c.r, c.g, c.b, dim) : new Color(1f, 1f, 1f, 0.28f * dim);
            dot.rectTransform.localScale = Vector3.one;
        }
        if (drop != null)
        {
            if (drop.enabled != arrow) drop.enabled = arrow;
            drop.color = ink ? new Color(InkOnWhite.r, InkOnWhite.g, InkOnWhite.b, 0.75f) : new Color(1f, 1f, 1f, 0.75f * dim);
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

    // 점 자리 — 그림이 있으면 그림(크게), 없으면 원래 점. 크기가 바뀌면 묶음을 다시 놓는다
    private void SetDotSprite(Sprite pic)
    {
        Sprite want = pic != null ? pic : dotSprite;
        if (want == null || dot.sprite == want) return;
        dot.sprite = want;
        dot.preserveAspect = pic != null;
        dot.rectTransform.sizeDelta = pic != null ? new Vector2(catIconSize, catIconSize) : dotSize;
        placedFor = null;
    }

    /// <summary>점과 글자(분류 칩은 ∨ 까지)를 한 묶음으로 가운데에 — 칸보다 길면(긴 언어) 글자를 앱 최소 크기까지 줄인다.
    /// 그림은 점보다 커서 글자와 사이를 좁힌다 (긴 중국어 '客运站 / 火车站'도 칸 안에)</summary>
    private void Place(bool arrow, float chipW)
    {
        float gap = dot.sprite != dotSprite ? 12f : 18f, dw = dot.rectTransform.sizeDelta.x;
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
