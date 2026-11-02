using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections;

public class AutoARSessionReset : MonoBehaviour
{
    public ARSession arSession;
    public float resetInterval = 5f; // AR 세션을 주기적으로 리셋하는 시간 (초)

    private Coroutine autoResetCoroutine;

    void Start()
    {
        if (arSession == null)
        {
            arSession = FindFirstObjectByType<ARSession>(); // ARSession 자동 탐색
        }

        // 5초 주기 자동 Reset은 anchor 깨뜨리는 부작용이 커서 비활성화.
        // ARSession.Reset은 LoadingManager.HandleSlowdownRefresh / HandleBackgroundRecovery
        // 같은 명시적 트리거에서만 호출.
        // autoResetCoroutine = StartCoroutine(AutoResetARSession());
    }

    /// <summary>
    /// 외부에서 명시적으로 호출하는 ARSession 재시작.
    /// (SlowdownRefresh / BG 복구에서 drift 누적 제거 목적)
    /// </summary>
    public void ResetNow()
    {
        if (arSession != null) arSession.Reset();
    }

    void OnDestroy()
    {
        if (autoResetCoroutine != null)
        {
            StopCoroutine(autoResetCoroutine);
            autoResetCoroutine = null;
        }
    }

    IEnumerator AutoResetARSession()
    {
        while (true) // 무한 루프 (게임이 종료될 때까지 실행)
        {
            yield return new WaitForSeconds(resetInterval); // 설정한 시간만큼 대기
            ResetARSession();
        }
    }

    void ResetARSession()
    {
        if (arSession != null)
        {
            arSession.Reset();
        }
        else
        {
            Debug.LogWarning("⚠ ARSession을 찾을 수 없습니다.");
        }
    }
}
