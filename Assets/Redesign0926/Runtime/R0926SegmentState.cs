using UnityEngine;
using UnityEngine.UI;

/// <summary>장소 / 3D 모델 탭 — 지금 보이는 페이지(SwipePanelController)를 따라 탭 모양을 바꾼다.</summary>
public class R0926SegmentState : MonoBehaviour
{
    [SerializeField] private SwipePanelController swipe;
    [SerializeField] private Image[] tabs;
    [SerializeField] private Text[] labels;

    private int shown = -1;

    private void Update()
    {
        if (swipe == null) return;
        int cur = swipe.GetCurrentPanel();
        if (cur == shown) return;
        shown = cur;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool on = i == cur;
            if (tabs[i] != null) tabs[i].color = on ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0f);
            if (labels != null && i < labels.Length && labels[i] != null)
                labels[i].color = on ? Color.white : new Color(0.549f, 0.584f, 0.616f, 1f);
        }
    }
}
