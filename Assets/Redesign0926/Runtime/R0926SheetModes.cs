using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 목록 시트 제목줄 오른쪽 '목록 | 지도 | 설정'. 지도·설정은 목록 위를 덮는 패널이다 (목록 = 패널 없음).
/// 시트를 닫았다 열어도 마지막에 보던 탭을 유지한다.
/// </summary>
public class R0926SheetModes : MonoBehaviour
{
    [Tooltip("탭 순서대로 — 목록 칸은 비워 둔다")]
    [SerializeField] private GameObject[] panels;
    [SerializeField] private Image[] tabs;
    [SerializeField] private Text[] labels;

    private static readonly Color On = new Color(0.953f, 0.961f, 0.965f);
    private static readonly Color Off = new Color(0.549f, 0.584f, 0.616f);
    private int current;

    public int Current => current;

    public void Show(int index)
    {
        current = Mathf.Clamp(index, 0, tabs != null ? tabs.Length - 1 : 0);
        if (panels != null)
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] != null && panels[i].activeSelf != (i == current)) panels[i].SetActive(i == current);
        if (tabs != null)
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i] != null) tabs[i].color = new Color(1f, 1f, 1f, i == current ? 0.14f : 0f);
        if (labels != null)
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] != null) labels[i].color = i == current ? On : Off;
    }
}
