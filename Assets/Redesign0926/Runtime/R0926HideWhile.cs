using UnityEngine;

/// <summary>지정한 패널 중 하나라도 열려 있으면 이 요소를 투명하게 숨긴다 (위치 칩이 상세 화면과 겹치지 않게).</summary>
[RequireComponent(typeof(CanvasGroup))]
public class R0926HideWhile : MonoBehaviour
{
    [SerializeField] private GameObject[] panels;
    [Tooltip("숨을 땐 곧장 — 같은 자리에 다른 게 바로 뜰 때 (확대/축소 표시). 다시 나올 땐 그대로 천천히")]
    [SerializeField] private bool hideAtOnce;
    [Tooltip("노치 아래 알약의 그림 조각 — 이 알약이 다른 알약에 자리를 내준 동안도 숨는다 (둘이 겹치지 않고 엇갈려 페이드)")]
    [SerializeField] private R0926StatusPill pill;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    // 알약 조각은 켜질 때 서서히 — 로딩 패널이 켜지는 순간 다른 알약 위에 한 프레임이라도 겹쳐 보이지 않게
    private void OnEnable()
    {
        if (pill != null && group != null) group.alpha = 0f;
    }

    private void LateUpdate()
    {
        bool hide = false;
        foreach (var p in panels)
        {
            if (p != null && p.activeInHierarchy) { hide = true; break; }
        }
        bool yielding = !hide && pill != null && (pill.Yielding || pill.WaitingForSlot);
        float target = hide || yielding ? 0f : 1f;
        if (!Mathf.Approximately(group.alpha, target))
        {
            group.alpha = hide && hideAtOnce ? 0f : Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 6f);
            group.blocksRaycasts = !(hide || yielding);
        }
        if (pill != null) pill.partAlpha = group.alpha;
    }
}
