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
    private float runDistance, runTime;   // 이번 올라오기 (통째로면 자기 높이 + 60)
    private bool fade;                    // 이번엔 투명 → 불투명도 같이 (통째로 올라올 땐 투명도를 건드리지 않는다)

    /// <summary>다음에 켜질 때 한 번은 올라오지 않고 제자리에 바로 선다 — 메시지 목록 ↔ 대화방은 옆으로 넘어가서</summary>
    public void SkipNext() => skipNext = true;
    public void ClearSkip() => skipNext = false;

    /// <summary>도크에서 다른 창으로 바꿀 때 (R0926DockSlotSwap) — 올라오지 않고 제자리에 바로 선다. 옅게 → 진하게는 창 쪽(R0926Closer.DissolveIn).
    /// 11-18 대표님 H2 「슬라이드 없이」 — 예전엔 56 아래에서 옅게 올라왔다. 막 켜져 올라오기 시작한 때만</summary>
    public bool Stand()
    {
        if (!running || t > 0.1f) return false;
        running = false;
        Set(0f);
        if (group != null) group.alpha = 1f;
        return true;
    }

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
        runDistance = fullHeight && rt != null ? rt.rect.height + 60f : distance;
        runTime = duration;
        fade = !fullHeight;
        if (!fade && group != null) group.alpha = 1f;
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
        float p = runTime > 0f ? Mathf.Clamp01(t / runTime) : 1f;
        float e = 1f - Mathf.Pow(1f - p, fade ? 3f : 4f);   // ease-out
        Set(-runDistance * (1f - e));
        if (fade && group != null) group.alpha = Mathf.Clamp01(p * 1.6f);   // 통째로 올라오는 동안 디졸브로 닫히면 투명도는 그쪽 몫
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
