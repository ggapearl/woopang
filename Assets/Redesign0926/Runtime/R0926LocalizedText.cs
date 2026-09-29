using UnityEngine;
using UnityEngine.UI;

/// <summary>고정 문구 다국어 표시 (ko/en/ja/zh/es). 언어는 LocalizationManager → 기기 언어 순.</summary>
[RequireComponent(typeof(Text))]
public class R0926LocalizedText : MonoBehaviour
{
    public string ko;
    public string en;
    public string ja;
    public string zh;
    public string es;

    private void Start()
    {
        GetComponent<Text>().text = Pick(Lang());
    }

    public static string Lang() => AppLanguage.Code;

    private string Pick(string lang)
    {
        string s = lang switch
        {
            "ko" => ko,
            "ja" => ja,
            "zh" => zh,
            "es" => es,
            _ => en
        };
        return string.IsNullOrEmpty(s) ? en : s;
    }
}
