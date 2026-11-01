using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장소 이름 아래에 로마자 읽는 법을 한 줄 더 보여 준다 (기기 언어가 한국어가 아닐 때만).
/// 원래 이름 글자는 건드리지 않는다 — 수정 요청 등이 그 글자를 다시 읽기 때문.
/// </summary>
[RequireComponent(typeof(Text))]
public class R0926RomanLine : MonoBehaviour
{
    [SerializeField] private Text source;

    private Text self;
    private string last;
    private bool lastLand;
    private bool korean;

    private void Awake()
    {
        self = GetComponent<Text>();
        korean = R0926LocalizedText.Lang() == "ko";
        self.text = "";
    }

    private void LateUpdate()
    {
        if (korean || source == null) return;
        string t = source.text;
        bool land = R0926Orientation.IsLandscape;
        if (t == last && land == lastLand) return;
        last = t;
        lastLand = land;
        string roman = R0926Romanizer.Romanize(t);
        // 세로 화면의 이름 칸은 높고 가운데 맞춤이라 이 줄이 이름 위에 겹친다 — 이름을 칸 위쪽으로 올려 비켜 주고, 두 줄 이름은 자리가 없어 로마자를 뺀다
        if (!land && source.preferredHeight > source.fontSize * 1.6f) roman = null;
        source.alignment = !land && roman != null ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter;
        self.text = roman ?? "";
    }
}
