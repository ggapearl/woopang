using UnityEngine.Networking;

/// <summary>
/// 우팡 서버가 응답하는지 — 따로 핑을 보내지 않고, 앱이 원래 주기적으로 보내는 요청 결과로 판단한다
/// (메시지 안읽음 확인 10초 · 근처 사용자 위치 5초). 사용자 수가 많아도 서버에 추가 부담이 없다.
/// 연결 실패나 5xx 가 3번 연속이면 Down. 어떤 응답이든(4xx 포함) 받으면 바로 정상으로 돌아온다.
/// 기기 자체가 오프라인인지는 Application.internetReachability 로 따로 본다 (화면 안내는 R0926NetworkBanner).
/// </summary>
public static class ServerHealth
{
    private const int FailThreshold = 3;
    private static int consecutiveFailures;

    public static bool Down => consecutiveFailures >= FailThreshold;

    public static void Report(UnityWebRequest req)
    {
        if (req == null) return;
        bool serverFailed = req.result == UnityWebRequest.Result.ConnectionError
                            || (req.result == UnityWebRequest.Result.ProtocolError && req.responseCode >= 500);
        if (serverFailed) consecutiveFailures++;
        else if (req.result == UnityWebRequest.Result.Success || req.result == UnityWebRequest.Result.ProtocolError) consecutiveFailures = 0;
    }
}
