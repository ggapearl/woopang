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
        if (t == last) return;
        last = t;
        self.text = R0926Romanizer.Romanize(t) ?? "";
    }
}
