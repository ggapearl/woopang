using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections;

public class AutoARSessionReset : MonoBehaviour
{
    public ARSession arSession;
    public float resetInterval = 5f; // AR 세션을 주기적으로 리셋하는 시간 (초)

    void Start()
    {
        if (arSession == null)
        {
            arSession = FindObjectOfType<ARSession>(); // ARSession 자동 탐색
        }

        StartCoroutine(AutoResetARSession());
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
            Debug.Log("🔄 AR Session이 자동으로 초기화되었습니다.");
        }
        else
        {
            Debug.LogWarning("⚠ ARSession을 찾을 수 없습니다.");
        }
    }
}
