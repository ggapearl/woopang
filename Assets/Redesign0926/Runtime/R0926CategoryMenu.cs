using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 목록 시트 분류 칩('전체' · '카페' …)을 누르면 화면 가운데에 뜨는 분류 고르기 창.
/// 10-17 대표님: 누를 때마다 다음 분류로 넘어가면 지금이 '전체'인지 계속 눌러 봐야 안다 → 펼쳐 놓고 고른다.
/// 11-26 대표님 37번 「'전체버튼' 을 눌렀을 때는 적당히 모달 발생해서 좀 큼직하게 버튼 만들어주면 돼. 그리고 아래나 위로 스와이프 하거나
/// 모달 밖을 터치했을 때는 해당 모달이 닫히면 돼」 → 칩 밑으로 펼치던 목록 대신 3×3 큰 칸 창 (카드는 불투명 — 5번 「배경은 투명하면 안될 것 같아」).
/// 닫기: 바깥(어두운 곳) 누르기 · 위나 아래로 밀기 · 뒤로가기 · 분류 고르기.
/// 칸은 보여 주는 순서(R0926FilterChip.CatOrder)대로 Apply 가 만들어 두고 여기서는 열고 닫기 · 지금 분류 표시 · 고르기만 한다.
/// 분류 칩에 붙는다 — 칩이 켜져 있는(시트가 열린) 동안만 FilterManager 에 고르기 창을 건다. 층은 화면 맨 위(목록 바로 위)에 있다.
/// </summary>
public class R0926CategoryMenu : MonoBehaviour
{
    [Tooltip("화면 전체를 덮는 층 (목록 · 하단 탭 위) — 보이고 숨기기는 CanvasGroup 으로")]
    [SerializeField] private CanvasGroup layer;
    [SerializeField] private RectTransform card;
    [Tooltip("바깥 — 누르면 닫힘 · 뒤로가기도 이 버튼을 누른다")]
    [SerializeField] private Button shade;
    [Tooltip("보여 주는 순서대로 (R0926FilterChip.CatOrder) — 누르면 Pick(그 칸의 CategoryFilterState 값)")]
    [SerializeField] private Button[] rows;
    [SerializeField] private Text[] labels;
    [SerializeField] private Image[] checks;
    [SerializeField] private R0926SheetModes modes;
    [Tooltip("화면 가장자리와 카드 사이 — 좁은 화면이면 카드를 줄인다")]
    [SerializeField] private float margin = 40f;
    [Tooltip("이만큼(캔버스 단위) 밀고 놓으면 닫힌다")]
    [SerializeField] private float swipeClose = 150f;
    [Tooltip("이 빠르기(캔버스 단위/초)로 튕기면 조금만 밀어도 닫힌다")]
    [SerializeField] private float flickSpeed = 1500f;

    private const float OpenTime = 0.2f, CloseTime = 0.14f, FlyTime = 0.2f, FlyDistance = 1100f;

    private FilterManager fm;
    private Canvas canvas;
    private bool open, closing, dragging, fromShade, backOn, homeSet;
    private float openAt, closeAt, offset, velocity, lastMoveAt, fit = 1f;
    private float closeAlpha, closeScale, flyFrom;
    private int flyDir;
    private Vector2 home;

    /// <summary>열려 있다 (닫히는 중이면 아니다 — 칩 화살표가 바로 돌아간다)</summary>
    public bool IsOpen => open;
    /// <summary>화면에 보인다 (닫히는 중 포함)</summary>
    public bool Showing => open || closing;
    public RectTransform Card => card;
    public CanvasGroup Layer => layer;
    /// <summary>밀어서 옮겨진 만큼 (캔버스 단위 · 위 +)</summary>
    public float Offset => offset;

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
        Home();
        open = true; closing = false; dragging = false;
        offset = 0f; velocity = 0f;
        openAt = Time.unscaledTime;
        Refresh();
        Fit();
        layer.blocksRaycasts = true;
        layer.interactable = true;
        Back(true);
        Pose();
    }

    /// <summary>바깥 누르기 · 뒤로가기 — 살짝 작아지며 사라진다</summary>
    public void Close() => BeginClose(0);

    /// <summary>분류를 고른다 — 값은 CategoryFilterState (칸 순번이 아니다)</summary>
    public void Pick(int category)
    {
        if (fm == null) fm = FindAnyObjectByType<FilterManager>();
        if (fm != null) fm.SetCategory((FilterManager.CategoryFilterState)Mathf.Clamp(category, 0, R0926FilterChip.CatNames.Length - 1));
        Refresh();   // 사라지는 동안 고른 칸이 바로 보이게
        BeginClose(0);
    }

    // i 번째 칸의 분류 (CategoryFilterState 값)
    private static int CategoryAt(int i) =>
        i < R0926FilterChip.CatOrder.Length ? R0926FilterChip.CatOrder[i] : Mathf.Min(i, R0926FilterChip.CatNames.Length - 1);

    // dir 0 = 제자리에서 사라짐 · ±1 = 민 쪽(위 +)으로 날아가며 사라짐
    private void BeginClose(int dir)
    {
        if (!open || layer == null) return;
        open = false; closing = true; dragging = false;
        closeAt = Time.unscaledTime;
        flyDir = dir;
        flyFrom = offset;
        closeAlpha = layer.alpha;
        closeScale = card.localScale.y / Mathf.Max(0.001f, fit);
        layer.blocksRaycasts = false;   // 사라지는 동안 뒤 목록을 바로 누를 수 있게
        layer.interactable = false;
        Back(false);
    }

    private void Hide()
    {
        open = false; closing = false; dragging = false;
        offset = 0f; velocity = 0f;
        Back(false);
        if (layer == null) return;
        layer.alpha = 0f;
        layer.blocksRaycasts = false;
        layer.interactable = false;
        if (card != null)
        {
            card.anchoredPosition = Home();
            card.localScale = new Vector3(fit, fit, 1f);
        }
    }

    // 카드 제자리 — Apply 가 놓은 자리 (처음 한 번 읽어 둔다)
    private Vector2 Home()
    {
        if (!homeSet && card != null) { home = card.anchoredPosition; homeSet = true; }
        return home;
    }

    // 뒤로가기 — 열려 있는 동안만 바깥 버튼을 맨 위에 건다
    private void Back(bool on)
    {
        if (on == backOn || shade == null) return;
        var h = BackButtonHandler.Instance;
        if (h != null)
        {
            if (on) h.RegisterButton(shade); else h.UnregisterButton(shade);
        }
        backOn = on && h != null;
    }

    private void Update()
    {
        if (!open && !closing) return;
        // 지도·설정 쪽으로 넘어가면 닫는다
        if (modes != null && modes.Current != 0) { Hide(); return; }
        if (closing)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - closeAt) / (flyDir != 0 ? FlyTime : CloseTime));
            if (t >= 1f) { Hide(); return; }
            float k = 1f - (1f - t) * (1f - t) * (1f - t);
            layer.alpha = closeAlpha * (1f - k);
            float s = flyDir != 0 ? closeScale : Mathf.Lerp(closeScale, 0.96f, k);
            if (flyDir != 0) offset = flyFrom + flyDir * FlyDistance * k;
            card.localScale = new Vector3(fit * s, fit * s, 1f);
            card.anchoredPosition = Home() + new Vector2(0f, offset);
            return;
        }
        if (!dragging && offset != 0f)
        {
            // 덜 밀고 놓았으면 제자리로
            offset *= Mathf.Exp(-18f * Time.unscaledDeltaTime);
            if (Mathf.Abs(offset) < 0.5f) offset = 0f;
        }
        Pose();
    }

    // 열리는 중 · 열린 뒤 — 살짝 아래에서 커지며 떠오른다 · 미는 만큼 따라오고 바깥이 옅어진다
    private void Pose()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - openAt) / OpenTime);
        float k = 1f - (1f - t) * (1f - t) * (1f - t);
        float drag = 1f - 0.35f * Mathf.Clamp01(Mathf.Abs(offset) / 600f);
        layer.alpha = k * drag;
        float s = fit * Mathf.Lerp(0.92f, 1f, k);
        card.localScale = new Vector3(s, s, 1f);
        card.anchoredPosition = Home() + new Vector2(0f, offset - (1f - k) * 24f);
    }

    // ── 위 · 아래로 밀기 (R0926CategorySwipe 가 층에서 넘겨준다) ──
    public void DragBegin(PointerEventData e)
    {
        if (!open) return;
        dragging = true;
        velocity = 0f;
        lastMoveAt = Time.unscaledTime;
        // 카드 밖에서 시작한 끌기 = 바깥 누르기 (끌기가 되면 바깥 버튼은 눌리지 않는다) — 놓으면 닫는다
        fromShade = !RectTransformUtility.RectangleContainsScreenPoint(card, e.pressPosition, e.pressEventCamera);
    }

    public void DragMove(PointerEventData e)
    {
        if (!open || !dragging) return;
        if (canvas == null) canvas = layer.GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        float dy = e.delta.y / Mathf.Max(0.01f, scale);
        offset += dy;
        if (Mathf.Abs(dy) > 0.01f)
        {
            float v = dy / Mathf.Max(Time.unscaledDeltaTime, 1f / 240f);
            velocity = Mathf.Lerp(velocity, v, 0.6f);
            lastMoveAt = Time.unscaledTime;
        }
    }

    public void DragEnd(PointerEventData e)
    {
        if (!open || !dragging) return;
        dragging = false;
        if (Time.unscaledTime - lastMoveAt > 0.1f) velocity = 0f;   // 멈췄다가 놓았으면 튕긴 게 아니다
        bool flick = Mathf.Abs(velocity) > flickSpeed && Mathf.Abs(offset) > 30f && Mathf.Sign(velocity) == Mathf.Sign(offset);
        if (!fromShade && Mathf.Abs(offset) <= swipeClose && !flick) return;   // 제자리로 (Update)
        int dir = Mathf.Abs(offset) >= 8f ? (int)Mathf.Sign(offset) : Mathf.Abs(velocity) > 200f ? (int)Mathf.Sign(velocity) : 0;
        BeginClose(dir);
    }

    /// <summary>칸마다 이름 — 지금 고른 분류는 그 분류 색 바탕 · 굵은 글자 · 체크 ('전체'는 흰 바탕에 어두운 글자, 칩과 같게)</summary>
    private void Refresh()
    {
        int cur = fm != null ? (int)fm.CategoryState : 0;
        for (int i = 0; rows != null && i < rows.Length; i++)
        {
            int ci = CategoryAt(i);
            Color c = R0926FilterChip.CatColors[ci];
            bool sel = ci == cur;
            bool white = sel && ci == 0;
            Color ink = white ? R0926FilterChip.InkOnWhite : Color.white;
            if (labels != null && i < labels.Length && labels[i] != null)
            {
                labels[i].text = R0926FilterChip.Pick(R0926FilterChip.CatNames[ci]);
                labels[i].color = sel ? ink : new Color(1f, 1f, 1f, 0.86f);
                labels[i].fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
            }
            if (checks != null && i < checks.Length && checks[i] != null)
            {
                checks[i].enabled = sel;
                checks[i].color = ink;
            }

            var b = rows[i];
            if (b == null) continue;
            // 바탕은 버튼 색으로 — 고른 칸은 그 분류 색 ('전체'는 흰색), 나머지는 옅은 흰색 · 누르는 동안 조금 더 밝게
            if (b.targetGraphic != null) b.targetGraphic.color = sel ? new Color(c.r, c.g, c.b, 1f) : Color.white;
            float a = white ? 0.94f : sel ? 0.3f : 0.06f;
            var cb = b.colors;
            cb.normalColor = cb.highlightedColor = cb.selectedColor = cb.disabledColor = new Color(1f, 1f, 1f, a);
            cb.pressedColor = white ? new Color(0.86f, 0.86f, 0.86f, a) : new Color(1f, 1f, 1f, a + 0.12f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.06f;
            b.colors = cb;
        }
    }

    // 좁은 화면 — 카드가 화면(여백 빼고)보다 크면 줄인다
    private void Fit()
    {
        Rect r = ((RectTransform)layer.transform).rect;
        Vector2 size = card.rect.size;
        fit = Mathf.Min(1f, (r.width - margin * 2f) / Mathf.Max(1f, size.x), (r.height - margin * 2f) / Mathf.Max(1f, size.y));
        if (fit <= 0f) fit = 1f;
    }
}
