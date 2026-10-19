using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 목록 시트 '전체'(분류) 칩을 누르면 칩 바로 아래로 펼쳐지는 분류 고르기 목록.
/// 10-17 대표님: 누를 때마다 다음 분류로 넘어가면 지금이 '전체'인지, 몇 번째가 샵·음식점인지 계속 눌러 봐야 안다 → 펼쳐 놓고 고른다.
/// 칸(분류 8줄)은 Apply 가 만들어 두고 여기서는 열고 닫기 · 지금 분류 표시 · 고르기만 한다.
/// 분류 칩에 붙는다 — 칩이 켜져 있는(시트가 열린) 동안만 FilterManager 에 고르기 목록을 건다.
/// </summary>
public class R0926CategoryMenu : MonoBehaviour
{
    [Tooltip("목록 쪽 전체를 덮는 층 — 보이고 숨기기는 CanvasGroup 으로 (시트가 꺼지는 중에 SetActive 하면 오류가 난다)")]
    [SerializeField] private CanvasGroup layer;
    [SerializeField] private RectTransform card;
    [SerializeField] private ScrollRect scroll;
    [Tooltip("분류 순서대로 (FilterManager.CategoryFilterState) — 누르면 Pick(번호)")]
    [SerializeField] private Button[] rows;
    [SerializeField] private Image[] dots;
    [SerializeField] private Text[] labels;
    [SerializeField] private Image[] checks;
    [SerializeField] private R0926SheetModes modes;
    [SerializeField] private float rowHeight = 112f;
    [SerializeField] private float pad = 12f;
    [SerializeField] private float minWidth = 520f;

    private FilterManager fm;
    private bool open;
    private float openAt;

    public bool IsOpen => open;

    private void OnEnable()
    {
        if (fm == null) fm = FindAnyObjectByType<FilterManager>();
        if (fm != null) fm.CategoryPicker = Open;
        Hide();
    }

    private void OnDisable()
    {
        if (fm != null && fm.CategoryPicker == (System.Action)Open) fm.CategoryPicker = null;
        Hide();
    }

    public void Open()
    {
        if (layer == null || card == null) return;
        open = true;
        openAt = Time.unscaledTime;
        Refresh();
        Fit();
        layer.blocksRaycasts = true;
        layer.interactable = true;
        Animate(0f);
    }

    public void Close()
    {
        if (open) Hide();
    }

    public void Pick(int index)
    {
        if (fm == null) fm = FindAnyObjectByType<FilterManager>();
        if (fm != null) fm.SetCategory((FilterManager.CategoryFilterState)Mathf.Clamp(index, 0, R0926FilterChip.CatNames.Length - 1));
        Hide();
    }

    private void Hide()
    {
        open = false;
        if (layer == null) return;
        layer.alpha = 0f;
        layer.blocksRaycasts = false;
        layer.interactable = false;
    }

    private void Update()
    {
        if (!open) return;
        // 지도·설정 쪽으로 넘어가면 닫는다 (목록 쪽과 함께 옆으로 밀려 나간 채 남지 않게)
        if (modes != null && modes.Current != 0) { Hide(); return; }
        Animate(Mathf.Clamp01((Time.unscaledTime - openAt) / 0.14f));
    }

    private void Animate(float t)
    {
        float k = 1f - (1f - t) * (1f - t);
        layer.alpha = k;
        card.localScale = new Vector3(1f, Mathf.Lerp(0.94f, 1f, k), 1f);
    }

    /// <summary>줄마다 이름·색 점 — 지금 고른 분류는 체크 · 그 분류 색 바탕 · 굵은 글자</summary>
    private void Refresh()
    {
        int cur = fm != null ? (int)fm.CategoryState : 0;
        for (int i = 0; rows != null && i < rows.Length; i++)
        {
            int ci = Mathf.Min(i, R0926FilterChip.CatNames.Length - 1);
            Color c = R0926FilterChip.CatColors[ci];
            bool sel = i == cur;
            if (labels != null && i < labels.Length && labels[i] != null)
            {
                labels[i].text = R0926FilterChip.Pick(R0926FilterChip.CatNames[ci]);
                labels[i].color = sel ? Color.white : new Color(1f, 1f, 1f, 0.82f);
                labels[i].fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
            }
            if (dots != null && i < dots.Length && dots[i] != null) dots[i].color = c;
            if (checks != null && i < checks.Length && checks[i] != null) checks[i].enabled = sel;

            var b = rows[i];
            if (b == null) continue;
            // 바탕은 버튼 색으로 — 고른 줄은 그 분류 색이 옅게, 누르는 동안 조금 더 밝게
            if (b.targetGraphic != null) b.targetGraphic.color = sel ? new Color(c.r, c.g, c.b, 1f) : Color.white;
            float a = sel ? 0.16f : 0f;
            var cb = b.colors;
            cb.normalColor = cb.highlightedColor = cb.selectedColor = cb.disabledColor = new Color(1f, 1f, 1f, a);
            cb.pressedColor = new Color(1f, 1f, 1f, a + 0.1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.06f;
            b.colors = cb;
        }
    }

    /// <summary>칩 왼쪽 아래 모서리 바로 밑에 — 목록 쪽 아래 끝을 넘으면 줄여서 스크롤 (고른 줄이 보이게)</summary>
    private void Fit()
    {
        var lrt = (RectTransform)layer.transform;
        var corners = new Vector3[4];
        ((RectTransform)transform).GetWorldCorners(corners);   // 0 왼쪽 아래
        Vector2 bl = lrt.InverseTransformPoint(corners[0]);
        Rect r = lrt.rect;
        float top = bl.y - r.yMax - 12f;
        int n = rows != null ? rows.Length : 0;
        float full = n * rowHeight + pad * 2f;
        float avail = r.height + top - 40f;
        float h = Mathf.Min(full, Mathf.Max(avail, rowHeight * 3.5f + pad * 2f));
        float w = Mathf.Max(minWidth, ((RectTransform)transform).rect.width);
        float x = Mathf.Clamp(bl.x - r.xMin, 24f, Mathf.Max(24f, r.width - w - 24f));

        card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
        card.pivot = new Vector2(0f, 1f);
        card.anchoredPosition = new Vector2(x, top);
        card.sizeDelta = new Vector2(w, h);

        if (scroll == null) return;
        float over = full - h;
        scroll.StopMovement();
        scroll.vertical = over > 0f;
        int cur = fm != null ? (int)fm.CategoryState : 0;
        scroll.verticalNormalizedPosition = over <= 0f ? 1f : 1f - Mathf.Clamp01((pad + cur * rowHeight - (h - rowHeight) / 2f) / over);
    }
}
