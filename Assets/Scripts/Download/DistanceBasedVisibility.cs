using UnityEngine;

public class DistanceBasedVisibility : MonoBehaviour
{
    public Transform player;
    public float hideDistance = 50f; // 동적으로 설정
    private bool isVisible = true;
    private float checkInterval = 0.5f; // 거리 체크 간격 (0.5초)
    private float lastCheckTime;
    private MeshRenderer[] meshRenderers; // 모든 MeshRenderer 배열
    private Collider[] colliders; // 모든 Collider 배열

    private void Start()
    {
        // 플레이어 동적 설정 (예: 메인 카메라)
        if (player == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                player = mainCamera.transform;
            }
            else
            {
                Debug.LogError($"🚨 {gameObject.name} - 메인 카메라를 찾을 수 없습니다! 플레이어를 수동으로 설정해 주세요.");
                enabled = false;
                return;
            }
        }

        // 모든 MeshRenderer와 Collider 찾기
        UpdateComponentReferences();

        // 초기 거리 체크
        float initialDistance = Vector3.Distance(player.position, transform.position);
        isVisible = initialDistance <= hideDistance;
        SetVisibility(isVisible);
    }

    private void Update()
    {
        // 일정 간격으로 거리 체크
        if (Time.time - lastCheckTime < checkInterval)
            return;

        lastCheckTime = Time.time;

        if (player == null)
        {
            Debug.LogError($"🚨 {gameObject.name} - 플레이어가 설정되지 않았습니다!");
            return;
        }

        float distance = Vector3.Distance(player.position, transform.position);

        if (distance > hideDistance && isVisible)
        {
            SetVisibility(false);
        }
        else if (distance <= hideDistance && !isVisible)
        {
            SetVisibility(true);
        }
    }

    public void UpdateComponentReferences()
    {
        meshRenderers = GetComponentsInChildren<MeshRenderer>(true); // 비활성화된 오브젝트 포함
        colliders = GetComponentsInChildren<Collider>(true); // 비활성화된 오브젝트 포함
    }

    private void SetVisibility(bool visible)
    {
        // 참조 갱신 (디바이스에서 동적으로 생성된 경우 대비)
        UpdateComponentReferences();

        isVisible = visible;
        // 모든 MeshRenderer 비활성화/활성화
        foreach (var renderer in meshRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
        // 모든 Collider 비활성화/활성화
        foreach (var collider in colliders)
        {
            if (collider != null)
            {
                collider.enabled = visible;
            }
        }
    }
}