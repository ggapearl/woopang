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

        autoResetCoroutine = StartCoroutine(AutoResetARSession());
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
