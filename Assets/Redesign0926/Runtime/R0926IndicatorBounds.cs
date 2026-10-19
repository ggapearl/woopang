using UnityEngine;

/// <summary>
/// 화살표 인디케이터가 놓이는 구역을 기기·화면 방향마다 맞춘다 —
/// 위는 노치(상태바) 바로 아래, 아래는 도크 윗선 위, 가로 화면에선 오른쪽 도크 막대 왼쪽까지, 옆은 안전영역 안.
/// 예전엔 위쪽만 맞추고 아래는 100px 고정이라 세로에선 아래쪽 화살표가 도크에 겹쳤고,
/// 가로에선 오른쪽 막대 밑에 놓였다 (가로에서 거의 안 뜨던 것은 OffScreenIndicator 가 가운데를 한 번만 재던 탓 — 함께 고침).
/// </summary>
public class R0926IndicatorBounds : MonoBehaviour
{
    [SerializeField] private OffScreenIndicator indicator;
    [Tooltip("도크 — 세로: 아래 막대(윗선 위로) · 가로: 오른쪽 막대(왼선 왼쪽으로). 비어 있으면 이름(Dock0926)으로 찾는다")]
    [SerializeField] private RectTransform dock;
    [Tooltip("노치·도크·화면 끝에서 띄울 간격 (캔버스 단위) — 화살표 반쯤 + 이름 글자")]
    [SerializeField] private float margin = 110f;

    private Canvas root;
    private float nextDockLook;
    private readonly Vector3[] corners = new Vector3[4];

    private void LateUpdate()
    {
        if (indicator == null) return;
        if (root == null) { var c = indicator.GetComponentInParent<Canvas>(); root = c != null ? c.rootCanvas : null; }
        if (dock == null && Time.unscaledTime >= nextDockLook)
        {
            nextDockLook = Time.unscaledTime + 1f;
            var d = GameObject.Find("Dock0926");
            if (d != null) dock = d.transform as RectTransform;
        }
        float m = margin * (root != null ? root.scaleFactor : 1f);
        Rect safe = Screen.safeArea;

        // 바라는 구역 (화면 픽셀, 왼쪽 아래 기준)
        float top = safe.yMax - m, bottom = safe.yMin, left = safe.xMin + m, right = safe.xMax;
        if (dock != null && dock.gameObject.activeInHierarchy && DockScreenRect(out Rect dr))
        {
            if (dr.width >= dr.height) bottom = Mathf.Max(bottom, dr.yMax);   // 세로 — 아래 막대
            else right = Mathf.Min(right, dr.xMin);                           // 가로 — 오른쪽 막대
        }
        bottom += m;
        right -= m;

        // 한계 = 가운데 ± 가운데×비율 ∓ 추가값 → 추가값이 바라는 구역이 되게 (화면 끝 쪽으로는 넓히지 않는다)
        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        float ox = indicator.ScreenBoundOffsetX, oy = indicator.ScreenBoundOffsetY;
        indicator.AdditionalBoundOffsetTop = Mathf.Max(0f, cy * (1f + oy) - top);
        indicator.AdditionalBoundOffsetBottom = Mathf.Max(0f, bottom - cy * (1f - oy));
        indicator.AdditionalBoundOffsetLeft = Mathf.Max(0f, left - cx * (1f - ox));
        indicator.AdditionalBoundOffsetRight = Mathf.Max(0f, cx * (1f + ox) - right);
    }

    // 도크가 화면에서 차지하는 사각형 (픽셀) — 창이 열려 화면 밖으로 내려가 있으면 바깥 값이 나와 구역을 줄이지 않는다
    private bool DockScreenRect(out Rect r)
    {
        dock.GetWorldCorners(corners);
        Camera cam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        return r.width > 1f && r.height > 1f;
    }
}
