using UnityEngine;

/// <summary>
/// 켜질 때 아래에서 위로 부드럽게 올라온다 (+ 투명 → 불투명).
/// 위치는 증분으로만 더하고 빼서, 안전영역 보정(R0926SafeInset)과 겹쳐도 원래 자리로 정확히 돌아온다.
/// 닫힘은 기존 코드가 SetActive(false) 로 바로 끄므로 애니메이션이 없다.
/// </summary>
public class R0926SlideIn : MonoBehaviour
{
    [Tooltip("시작할 때 아래로 내려가 있는 거리 (캔버스 단위)")]
    [SerializeField] private float distance = 220f;
    [Tooltip("올라오는 시간 (초)")]
    [SerializeField] private float duration = 0.32f;
    [Tooltip("켜면 자기 높이만큼 아래(도크 윗선 뒤)에서 통째로 올라온다 — 투명도는 건드리지 않는다")]
    [SerializeField] private bool fullHeight = false;

    private RectTransform rt;
    private CanvasGroup group;
    private float applied;   // 지금 더해 둔 아래쪽 오프셋 (음수)
    private float t;
    private bool running;
    private bool skipNext;

    /// <summary>다음에 켜질 때 한 번은 올라오지 않고 제자리에 바로 선다 — 메시지 목록 ↔ 대화방은 옆으로 넘어가서</summary>
    public void SkipNext() => skipNext = true;
    public void ClearSkip() => skipNext = false;

    private void Awake()
    {
        rt = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (skipNext)
        {
            skipNext = false;
            running = false;
            Set(0f);
            if (group != null) group.alpha = 1f;
            return;
        }
        if (fullHeight && rt != null) distance = rt.rect.height + 60f;
        t = 0f;
        running = true;
        Step();
    }

    private void OnDisable()
    {
        // 중간에 꺼지면 더해 둔 만큼 되돌려 다음에 켤 때 자리가 틀어지지 않게
        Set(0f);
        if (group != null) group.alpha = 1f;
        running = false;
    }

    private void Update()
    {
        if (!running) return;
        t += Time.unscaledDeltaTime;
        Step();
    }

    private void Step()
    {
        float p = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
        float e = 1f - Mathf.Pow(1f - p, fullHeight ? 4f : 3f);   // ease-out
        Set(-distance * (1f - e));
        if (group != null) group.alpha = fullHeight ? 1f : Mathf.Lerp(0f, 1f, Mathf.Clamp01(p * 1.6f));
        if (p >= 1f) running = false;
    }

    private void Set(float offset)
    {
        if (rt == null) return;
        float delta = offset - applied;
        if (Mathf.Approximately(delta, 0f)) return;
        rt.anchoredPosition += new Vector2(0f, delta);
        applied = offset;
    }
}
