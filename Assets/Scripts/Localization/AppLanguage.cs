using UnityEngine;

/// <summary>
/// 앱 전체가 같은 기준으로 언어를 고르게 하는 한 곳 (ko / en / ja / zh / es).
/// LocalizationManager 설정을 먼저 보고, 아직 없으면 기기 언어로 정한다.
/// 예전엔 화면마다 systemLanguage · CultureInfo · LocalizationManager 를 섞어 써서
/// 중국어 기기(아이폰은 '간체 중국어'로 알려 줌)에서 영어가 나오는 등 화면마다 언어가 달랐다.
/// </summary>
public static class AppLanguage
{
#if UNITY_EDITOR
    /// <summary>에디터 시험용 기기 언어 (ko/en/ja/zh/es) — 플레이 시험 'lang' 이 플레이를 켜기 전에 넣는다 (도메인 다시 읽기가 꺼져 있어 플레이 안까지 남는다)</summary>
    public static string EditorOverride;
#endif

    public static string Code
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorOverride)) return EditorOverride;
#endif
            if (LocalizationManager.Instance != null)
            {
                string l = LocalizationManager.Instance.GetCurrentLanguage();
                if (!string.IsNullOrEmpty(l)) return l;
            }
            return FromSystem(Application.systemLanguage);
        }
    }

    public static string FromSystem(SystemLanguage s)
    {
        switch (s)
        {
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return "zh";
            case SystemLanguage.Spanish: return "es";
            default: return "en";
        }
    }

    /// <summary>SystemLanguage 로 된 번역표용. 중국어는 표마다 키가 달라(Chinese / ChineseSimplified) 고를 수 있게 한다.</summary>
    public static SystemLanguage AsSystemLanguage(bool chineseSimplified = false)
    {
        switch (Code)
        {
            case "ko": return SystemLanguage.Korean;
            case "ja": return SystemLanguage.Japanese;
            case "zh": return chineseSimplified ? SystemLanguage.ChineseSimplified : SystemLanguage.Chinese;
            case "es": return SystemLanguage.Spanish;
            default: return SystemLanguage.English;
        }
    }
}
