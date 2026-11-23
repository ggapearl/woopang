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

        // 11-18 H3 — 땅 위 거리로 잰다 (11-16 「이동거리는 평면거리로만」). 높이를 올린 '우팡산' 이 직선거리로 400 m 를 넘어
        // Target 만 꺼져, 3D 는 떠 있는데 화살표 · 박스가 없었다. 3D 로 띄우는 반경 안에서는 끄지 않는다
        Vector3 camPos = Camera.main.transform.position;
        float distanceToCamera = targetScript != null
            ? targetScript.GetDistanceFromCamera(camPos)
            : Vector3.Distance(transform.position, camPos);
        float limit = Mathf.Max(distanceThreshold, FilterManager.FullRadiusNow);

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
        if (distanceToCamera <= limit)
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