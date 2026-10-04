using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 칸 수가 정해진 격자의 칸 너비를 실제 폭에 맞춘다 — 화면비가 달라 캔버스 폭이 바뀌어도 칩이 넘치거나 비지 않게.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
public class R0926GridFit : MonoBehaviour
{
    private GridLayoutGroup grid;
    private float lastW = -1f;

    private void OnEnable() { grid = GetComponent<GridLayoutGroup>(); Fit(); }
    private void OnRectTransformDimensionsChange() => Fit();

    private void Fit()
    {
        if (grid == null) grid = GetComponent<GridLayoutGroup>();
        int cols = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? Mathf.Max(1, grid.constraintCount) : 1;
        float w = ((RectTransform)transform).rect.width - grid.padding.horizontal;
        if (w <= 0f || Mathf.Approximately(w, lastW)) return;
        lastW = w;
        float cell = Mathf.Floor((w - grid.spacing.x * (cols - 1)) / cols);
        if (!Mathf.Approximately(grid.cellSize.x, cell)) grid.cellSize = new Vector2(cell, grid.cellSize.y);
    }
}
