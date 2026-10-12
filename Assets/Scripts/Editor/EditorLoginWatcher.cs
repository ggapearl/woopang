using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 에디터 로그인 — 플레이 중 앱의 '로그인'으로 브라우저에 나갔다가 그 로그인을 에디터로 가져온다.
/// 웹 로그인의 마지막 단계는 woopang://auth?token=… 로 앱을 부르는데, PC 에는 그 주소를 받을 앱이 없어 거기서 멈췄다.
/// 로그인 페이지를 연 뒤 10분 동안 클립보드를 보고, '앱으로 돌아가기' 링크가 복사되면 LoginManager 에 넘긴다 → 서버 확인 → 로그인.
/// (브라우저의 '앱으로 돌아가기' 버튼 오른쪽 클릭 → 링크 주소 복사)
/// </summary>
[InitializeOnLoad]
public static class EditorLoginWatcher
{
    private const string WaitKey = "EditorLoginWatcher.WaitUntil";
    private const double WaitSeconds = 600;
    private static double nextPoll;

    static EditorLoginWatcher()
    {
        LoginManager.EditorBeforeWebLogin = OnWebLogin;
        EditorApplication.update += Poll;
    }

    /// <summary>앱의 로그인 버튼(또는 인스펙터 Web Login)이 브라우저를 열기 직전</summary>
    public static void OnWebLogin()
    {
        SessionState.SetString(WaitKey, (EditorApplication.timeSinceStartup + WaitSeconds).ToString(CultureInfo.InvariantCulture));
        Debug.Log("[에디터 로그인] 브라우저에서 로그인한 뒤 '앱으로 돌아가기' 버튼을 오른쪽 클릭 → 링크 주소 복사하면 에디터가 로그인합니다");
    }

    public static bool Waiting
    {
        get
        {
            double.TryParse(SessionState.GetString(WaitKey, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double until);
            return EditorApplication.timeSinceStartup < until;
        }
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (!Waiting || !EditorApplication.isPlaying || LoginManager.Instance == null) return;
        string s = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(s) || !s.StartsWith("woopang://auth", StringComparison.Ordinal)) return;
        EditorGUIUtility.systemCopyBuffer = "";   // 로그인 토큰을 클립보드에 남기지 않는다
        SessionState.EraseString(WaitKey);
        LoginManager.Instance.SimulateDeepLink(s.Trim());
    }
}
