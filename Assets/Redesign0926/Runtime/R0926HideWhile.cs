using UnityEngine;

/// <summary>지정한 패널 중 하나라도 열려 있으면 이 요소를 투명하게 숨긴다 (위치 칩이 상세 화면과 겹치지 않게).</summary>
[RequireComponent(typeof(CanvasGroup))]
public class R0926HideWhile : MonoBehaviour
{
    [SerializeField] private GameObject[] panels;
    [Tooltip("숨을 땐 곧장 — 같은 자리에 다른 게 바로 뜰 때 (확대/축소 표시). 다시 나올 땐 그대로 천천히")]
    [SerializeField] private bool hideAtOnce;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    private void LateUpdate()
    {
        bool hide = false;
        foreach (var p in panels)
        {
            if (p != null && p.activeInHierarchy) { hide = true; break; }
        }
        float target = hide ? 0f : 1f;
        if (!Mathf.Approximately(group.alpha, target))
        {
            group.alpha = hide && hideAtOnce ? 0f : Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 6f);
            group.blocksRaycasts = !hide;
        }
    }
}
