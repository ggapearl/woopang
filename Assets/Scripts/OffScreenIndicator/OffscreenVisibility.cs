using UnityEngine;

public class OffscreenVisibility : MonoBehaviour
{
    private Target targetScript;
    public float distanceThreshold = 400f; // 사용자 설정 거리로 변경 (예: 400m)

    // 로그 샘플링 관련 변수
    private float lastLogTime = 0f;
    private float logInterval = 1f; // 로그 출력 간격 (1초)

    // 로그 그룹화 관련 변수
    private System.Collections.Generic.List<string> logMessages = new System.Collections.Generic.List<string>();
    private float lastGroupLogTime = 0f;
    private float groupLogInterval = 1f; // 그룹 로그 출력 간격 (1초)

    void Start()
    {
        targetScript = GetComponent<Target>();
    }

    void Update()
    {
        float distanceToCamera = Vector3.Distance(transform.position, Camera.main.transform.position);

        // 로그 메시지 추가 (매 프레임 기록)
        logMessages.Add($"[OffscreenVisibility] Distance to Camera: {distanceToCamera:F2}m, Threshold: {distanceThreshold}m, GameObject: {gameObject.name}");

        // 일정 간격으로 로그 출력 (샘플링)
        if (Time.time - lastLogTime >= logInterval)
        {
            lastLogTime = Time.time;
        }

        // 일정 간격으로 그룹화된 로그 출력
        if (Time.time - lastGroupLogTime >= groupLogInterval)
        {
            if (logMessages.Count > 0)
            {
                logMessages.Clear();
            }
            lastGroupLogTime = Time.time;
        }

        // Target 스크립트 활성화/비활성화 로직
        if (distanceToCamera <= distanceThreshold)
        {
            if (targetScript != null && !targetScript.enabled)
            {
                targetScript.enabled = true;
            }
        }
        else
        {
            if (targetScript != null && targetScript.enabled)
            {
                targetScript.enabled = false;
            }
        }
    }
}