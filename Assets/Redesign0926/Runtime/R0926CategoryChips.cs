using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장소 추가의 분류를 칩으로 바로 고르게 한다.
/// 기존 방식(토글을 누를 때마다 다음 분류로 순환)은 그대로 두고, 칩을 누르면 그 분류까지 토글을 대신 눌러 준다.
/// 지금 분류는 업로드 매니저의 currentCategoryIndex 를 읽는다.
/// ⚠ 시안용: 확정되면 매니저에 SetCategory(int) 공개 메서드를 두고 리플렉션을 걷어낼 것.
/// </summary>
public class R0926CategoryChips : MonoBehaviour
{
    [SerializeField] private Toggle cycleToggle;
    [SerializeField] private MonoBehaviour manager;
    [Tooltip("매니저의 categoryValues 와 같은 순서 (0번은 미선택 \"\")")]
    [SerializeField] private string[] values;
    [Tooltip("values[1..] 순서의 칩")]
    [SerializeField] private Image[] chipBodies;
    [SerializeField] private Text[] chipLabels;

    private FieldInfo indexField;
    private int shown = -1;

    private void Awake()
    {
        if (manager != null)
            indexField = manager.GetType().GetField("currentCategoryIndex", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private int Current()
    {
        return indexField != null && manager != null ? (int)indexField.GetValue(manager) : 0;
    }

    /// <summary>칩 클릭 — 이미 고른 칩을 다시 누르면 선택 해제(미선택)</summary>
    public void Select(int valueIndex)
    {
        if (cycleToggle == null || values == null || values.Length == 0) return;
        int n = values.Length;
        int cur = Current();
        int target = valueIndex == cur ? 0 : valueIndex;
        int steps = ((target - cur) % n + n) % n;
        for (int i = 0; i < steps; i++) cycleToggle.isOn = !cycleToggle.isOn;
    }

    private void Update()
    {
        int cur = Current();
        if (cur == shown) return;
        shown = cur;
        for (int i = 0; i < chipBodies.Length; i++)
        {
            bool on = i + 1 == cur;
            Color c = DataManager.GetCategoryColor(values[i + 1]);
            if (chipBodies[i] != null)
                chipBodies[i].color = on ? new Color(c.r, c.g, c.b, 0.32f) : new Color(1f, 1f, 1f, 0.07f);
            if (chipLabels != null && i < chipLabels.Length && chipLabels[i] != null)
                chipLabels[i].color = on ? Color.white : new Color(0.867f, 0.886f, 0.902f, 1f);
        }
    }
}
