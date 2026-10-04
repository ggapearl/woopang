using UnityEngine;

/// <summary>
/// 화살표 인디케이터의 위쪽 한계를 기기마다 노치(상태바) 바로 아래까지 올린다.
/// 예전엔 고정 값이라 노치가 작은 폰에서도 위쪽에 빈 띠가 남았다.
/// </summary>
public class R0926IndicatorBounds : MonoBehaviour
{
    [SerializeField] private OffScreenIndicator indicator;
    [Tooltip("노치 아래로 띄울 간격 (캔버스 단위) — 화살표 반쯤 + 여유")]
    [SerializeField] private float margin = 110f;

    private Canvas root;
    private Rect lastSafe;
    private int lastH;
    private float lastScale = -1f;

    private void LateUpdate()
    {
        if (indicator == null) return;
        if (root == null) { var c = indicator.GetComponentInParent<Canvas>(); root = c != null ? c.rootCanvas : null; }
        float scale = root != null ? root.scaleFactor : 1f;
        if (Screen.safeArea == lastSafe && Screen.height == lastH && Mathf.Approximately(scale, lastScale)) return;
        lastSafe = Screen.safeArea;
        lastH = Screen.height;
        lastScale = scale;
        // 위쪽 한계(아래에서 잰 픽셀) = 가운데 + 가운데×비율 − 추가값 → 안전영역 윗선 − 간격이 되게
        float cy = Screen.height / 2f;
        float want = lastSafe.yMax - margin * scale;
        indicator.AdditionalBoundOffsetTop = Mathf.Max(0f, cy * (1f + indicator.ScreenBoundOffsetY) - want);
    }
}
