using UnityEngine;

/// <summary>
/// 목록 창 네온 빛 = 고른 칩 색 (S8 · 11-23). 분류를 고르면 그 분류 색 · 애견동반만이면 노랑 · 축제 칩이면 축제 색 ·
/// 일반 칩(공공데이터 · 공공기관 · 지하철 · 터미널/기차)이 하나만 켜져 있으면 그 칩 색 · 일반 칩을 다 끄고 사람만 켜면 분홍 · 나머지는 기본 푸른 빛.
/// 지도 · 설정 쪽을 보는 동안은 기본 푸른 빛.
/// </summary>
[RequireComponent(typeof(R0926WindowSkin))]
public class R0926ListGlow : MonoBehaviour
{
    private static readonly Color PetGold = new Color(1f, 193f / 255f, 7f / 255f, 1f);         // #FFC107
    private static readonly Color Festival = new Color(1f, 107f / 255f, 87f / 255f, 1f);       // #FF6B57
    private static readonly Color People = new Color(233f / 255f, 83f / 255f, 131f / 255f, 1f); // #E95383

    [SerializeField] private R0926WindowSkin skin;
    [SerializeField] private R0926SheetModes modes;
    [SerializeField] private R0926FilterChip[] chips;

    private FilterManager fm;

    private void LateUpdate()
    {
        if (skin == null || !skin.Showing || !R0926MenuStyle.Neon) return;
        skin.Glow = Pick();
    }

    public Color Pick()
    {
        if (modes != null && modes.Current != 0) return R0926WindowSkin.Blue;
        string cat = FilterManager.ActiveCategoryNow;
        if (!string.IsNullOrEmpty(cat)) return Opaque(DataManager.GetCategoryColor(cat));
        if (FilterManager.PetOnlyNow) return PetGold;
        if (chips == null) return R0926WindowSkin.Blue;
        int simpleOn = 0;
        R0926FilterChip one = null;
        foreach (var c in chips)
        {
            if (c == null || c.ChipKind != R0926FilterChip.Kind.Simple || !c.IsOn) continue;
            if (c.ModeChip || c.name == "FestivalToggle") return Festival;
            simpleOn++;
            one = c;
        }
        if (simpleOn == 1) return Opaque(one.ChipColor);
        if (simpleOn == 0)
        {
            if (fm == null) fm = Object.FindFirstObjectByType<FilterManager>(FindObjectsInactive.Include);
            if (fm != null && fm.P2PState != FilterManager.P2PFilterState.None) return People;
        }
        return R0926WindowSkin.Blue;
    }

    private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
}
