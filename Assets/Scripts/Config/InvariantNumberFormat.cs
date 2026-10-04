using System.Globalization;
using System.Threading;
using UnityEngine;

/// <summary>
/// 숫자·좌표를 기기 언어와 상관없이 '.' 소수점으로 읽고 쓴다.
/// 스페인어처럼 소수점을 쉼표로 쓰는 기기에서는 서버 주소의 좌표가 "lat=36,6361" 로 찍혀
/// 주변 장소·지하철·날씨를 못 받아 왔고, 서버가 준 "36.6361" 을 숫자로 읽는 곳도 틀어졌다.
/// 화면 글의 언어는 앱 자체 번역(AppLanguage — Application.systemLanguage)을 쓰므로 영향이 없다.
/// </summary>
public static class InvariantNumberFormat
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Apply()
    {
        var inv = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = inv;   // 나중에 만들어지는 작업 스레드까지
        Thread.CurrentThread.CurrentCulture = inv;
    }
}
