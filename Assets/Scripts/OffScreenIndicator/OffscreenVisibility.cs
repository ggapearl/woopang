using UnityEngine;

public class OffscreenVisibility : MonoBehaviour
{
    private Target targetScript;
    public float distanceThreshold = 400f; // ����� ���� �Ÿ��� ���� (��: 400m)

    // ⭐ UpdateThrottle로 성능 최적화 (0.5초마다 체크)
    private UpdateThrottle updateThrottle;

    // �α� ���ø� ���� ����
    private float lastLogTime = 0f;
    private float logInterval = 1f; // �α� ��� ���� (1��)

    // �α� �׷�ȭ ���� ����
    private System.Collections.Generic.List<string> logMessages = new System.Collections.Generic.List<string>();
    private float lastGroupLogTime = 0f;
    private float groupLogInterval = 1f; // �׷� �α� ��� ���� (1��)

    void Start()
    {
        targetScript = GetComponent<Target>();
        updateThrottle = new UpdateThrottle(0.5f); // 0.5초마다만 거리 체크
    }

    void Update()
    {
        // ⭐ Null 체크 추가
        if (updateThrottle == null) return;
        if (Camera.main == null) return;

        // ⭐ UpdateThrottle 사용: 0.5초마다만 실행
        if (!updateThrottle.ShouldUpdate())
            return;

        float distanceToCamera = Vector3.Distance(transform.position, Camera.main.transform.position);

        // �α� �޽��� �߰� (�� ������ ���)
        logMessages.Add($"[OffscreenVisibility] Distance to Camera: {distanceToCamera:F2}m, Threshold: {distanceThreshold}m, GameObject: {gameObject.name}");

        // ���� �������� �α� ��� (���ø�)
        if (Time.time - lastLogTime >= logInterval)
        {
            lastLogTime = Time.time;
        }

        // ���� �������� �׷�ȭ�� �α� ���
        if (Time.time - lastGroupLogTime >= groupLogInterval)
        {
            if (logMessages.Count > 0)
            {
                logMessages.Clear();
            }
            lastGroupLogTime = Time.time;
        }

        // Target ��ũ��Ʈ Ȱ��ȭ/��Ȱ��ȭ ����
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