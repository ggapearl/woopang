using UnityEngine;

/// <summary>
/// 상단(상태바·노치) / 하단(홈 인디케이터) 안전영역만큼 요소를 안쪽으로 민다.
/// Move: 위치를 민다. Stretch: 늘어나는 영역(카드 등)의 해당 가장자리만 안으로 당긴다.
/// 값은 "지난번에 더한 만큼 빼고 새로 더하는" 증분 방식 — 슬라이드 애니메이션(R0926SlideIn)과 겹쳐도 어긋나지 않는다.
/// 에디터 배치값은 안전영역 0 기준 — 실제 기기에서만 인셋이 더해진다.
/// </summary>
public class R0926SafeInset : MonoBehaviour
{
    public enum Edge { Top, Bottom, Left, Right }
    public enum Mode { Move, Stretch }

    [SerializeField] private Edge edge = Edge.Top;
    [SerializeField] private Mode mode = Mode.Move;

    private RectTransform rt;
    private Canvas root;
    private float applied;
    private Rect lastSafe;
    private float lastScale = -1f;
    private readonly Vector3[] corners = new Vector3[4];

    public void SetEdge(Edge e, Mode m = Mode.Move)
    {
        edge = e;
        mode = m;
    }

    /// <summary>배치를 통째로 바꾼 뒤(가로/세로 전환) 인셋을 처음부터 다시 더한다.</summary>
    public void Reapply(Edge e)
    {
        if (rt == null) rt = (RectTransform)transform;
        edge = e;
        applied = 0f;
        lastScale = -1f;
        Apply();
    }

    private void Awake()
    {
        rt = (RectTransform)transform;
        Apply();
    }

    private void Update()
    {
        if (root == null || Screen.safeArea != lastSafe || !Mathf.Approximately(root.scaleFactor, lastScale)) Apply();
    }

    private void Apply()
    {
        if (root == null)
        {
            var c = GetComponentInParent<Canvas>(true);
            if (c == null) return;
            root = c.rootCanvas;
        }
        lastSafe = Screen.safeArea;
        lastScale = root.scaleFactor;
        float scale = lastScale > 0f ? lastScale : 1f;

        // 오버레이 캔버스의 월드 좌표 = 화면 픽셀 → 캔버스가 실제로 덮는 영역과 안전영역의 차이가 인셋
        ((RectTransform)root.transform).GetWorldCorners(corners);
        float inset;
        switch (edge)
        {
            case Edge.Top: inset = Mathf.Max(0f, corners[1].y - lastSafe.yMax); break;
            case Edge.Bottom: inset = Mathf.Max(0f, lastSafe.yMin - corners[0].y); break;
            case Edge.Left: inset = Mathf.Max(0f, lastSafe.xMin - corners[0].x); break;
            default: inset = Mathf.Max(0f, corners[2].x - lastSafe.xMax); break;
        }
        float d = inset / scale;
        float delta = d - applied;
        if (Mathf.Approximately(delta, 0f)) return;
        applied = d;

        if (mode == Mode.Move)
        {
            switch (edge)
            {
                case Edge.Top: rt.anchoredPosition += new Vector2(0f, -delta); break;
                case Edge.Bottom: rt.anchoredPosition += new Vector2(0f, delta); break;
                case Edge.Left: rt.anchoredPosition += new Vector2(delta, 0f); break;
                default: rt.anchoredPosition += new Vector2(-delta, 0f); break;
            }
        }
        else
        {
            switch (edge)
            {
                case Edge.Top: rt.offsetMax -= new Vector2(0f, delta); break;
                case Edge.Bottom: rt.offsetMin += new Vector2(0f, delta); break;
                case Edge.Left: rt.offsetMin += new Vector2(delta, 0f); break;
                default: rt.offsetMax -= new Vector2(delta, 0f); break;
            }
        }
    }
}
