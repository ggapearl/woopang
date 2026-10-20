using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 추가 사진 칸 — 장수가 늘면 칸을 줄여 정해진 자리(대표 사진 높이) 안에 모두 들어가게.
/// 예전엔 네 장(2×2)을 넘으면 그리드가 아래로 넘쳐 '이름' 칸을 덮었다 (10-20).
/// 불러오는 중인 빈 칸(R0926PhotoLoading)도 한 장으로 센다
/// </summary>
[RequireComponent(typeof(GridLayoutGroup))]
public class R0926FitGrid : MonoBehaviour
{
    [SerializeField] private float maxCell = 203f;

    private GridLayoutGroup grid;
    private RectTransform rt;
    private Vector2 baseSpacing;
    private int lastCount = -1;
    private Vector2 lastSize;

    private void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        rt = (RectTransform)transform;
        baseSpacing = grid.spacing;
    }

    private void LateUpdate()
    {
        int n = 0;
        foreach (Transform c in transform)
            if (c.gameObject.activeSelf) n++;
        Vector2 size = rt.rect.size;
        if (n == lastCount && size == lastSize) return;
        lastCount = n;
        lastSize = size;

        // 열 수를 늘려 가며 칸이 가장 크게 나오는 배치를 고른다 (네 장까지는 그대로 2×2)
        float cell = maxCell;
        int cols = Mathf.Max(1, Mathf.FloorToInt((size.x + baseSpacing.x) / (maxCell + baseSpacing.x)));
        if (n > 0)
        {
            float best = 0f;
            for (int c = 1; c <= n; c++)
            {
                int rows = Mathf.CeilToInt(n / (float)c);
                float w = (size.x - baseSpacing.x * (c - 1)) / c;
                float h = (size.y - baseSpacing.y * (rows - 1)) / rows;
                float s = Mathf.Min(maxCell, Mathf.Min(w, h));
                if (s > best + 0.5f) { best = s; cols = c; }
            }
            cell = Mathf.Floor(best);
            if (cell >= maxCell) cols = Mathf.Max(cols, Mathf.FloorToInt((size.x + baseSpacing.x) / (maxCell + baseSpacing.x)));
        }
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
        grid.cellSize = new Vector2(cell, cell);
    }
}
