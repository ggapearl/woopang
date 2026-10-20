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

    private string Pick(string lang) => Say(ko, en, ja, zh, es, lang);

    /// <summary>코드에서 바꿔 넣는 문구 (자르는 중… · 올리는 중… 등) — 언어는 위와 같은 순서로 고른다</summary>
    public static string Say(string ko, string en, string ja, string zh, string es) => Say(ko, en, ja, zh, es, Lang());

    private static string Say(string ko, string en, string ja, string zh, string es, string lang)
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
