using UnityEngine;

/// <summary>
/// 목록 시트 필터 칩 칸 — 4칸 격자를 자식 순서대로 채우고, 이름이 긴 칩('터미널 / 기차')은 두 칸을 쓴다.
/// 위 [전체 ∨][반려견][공공데이터][3D] · 아래 [지하철][터미널 / 기차][사람] — 두 줄의 칸 경계가 위아래로 맞는다 (10-18 대표님 '위에 4칸, 아래 3칸').
/// 칸 너비는 실제 폭에 맞춘다 — 화면비가 달라 캔버스 폭이 바뀌어도 칩이 넘치거나 비지 않게. 꺼진 칩은 자리를 차지하지 않는다.
/// </summary>
[ExecuteAlways]
public class R0926ChipGrid : MonoBehaviour
{
    [SerializeField] private int columns = 4;
    [SerializeField] private float gap = 18f;
    [SerializeField] private float rowHeight = 112f;
    [Tooltip("두 칸을 쓰는 칩")]
    [SerializeField] private RectTransform[] wide;

    private float lastW = -1f;
    private int lastHash;

    private void OnEnable() { lastW = -1f; Place(); }
    private void OnRectTransformDimensionsChange() => Place();
    private void OnTransformChildrenChanged() { lastW = -1f; Place(); }
    private void LateUpdate() => Place();   // 칩을 켜고 끌 때 (자식 수는 그대로라 위 알림이 안 온다)

    private void Place()
    {
        float w = ((RectTransform)transform).rect.width;
        if (w <= 0f) return;
        int hash = ActiveHash();
        if (Mathf.Approximately(w, lastW) && hash == lastHash) return;
        lastW = w;
        lastHash = hash;

        int cols = Mathf.Max(1, columns);
        float cell = (w - gap * (cols - 1)) / cols;
        int col = 0, row = 0;
        foreach (Transform t in transform)
        {
            var c = t as RectTransform;
            if (c == null || !c.gameObject.activeSelf) continue;
            int span = Mathf.Min(IsWide(c) ? 2 : 1, cols);
            if (col + span > cols) { col = 0; row++; }
            c.anchorMin = c.anchorMax = new Vector2(0f, 1f);
            c.pivot = new Vector2(0f, 1f);
            c.anchoredPosition = new Vector2(Mathf.Round(col * (cell + gap)), -row * (rowHeight + gap));
            c.sizeDelta = new Vector2(Mathf.Round(cell * span + gap * (span - 1)), rowHeight);
            col += span;
            if (col >= cols) { col = 0; row++; }
        }
    }

    private bool IsWide(RectTransform c)
    {
        if (wide == null) return false;
        foreach (var x in wide) if (x == c) return true;
        return false;
    }

    private int ActiveHash()
    {
        int h = transform.childCount;
        for (int i = 0; i < transform.childCount; i++)
            if (transform.GetChild(i).gameObject.activeSelf) h = h * 31 + i + 1;
        return h;
    }
}
