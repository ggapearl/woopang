using UnityEngine;

public class DistanceBasedVisibility : MonoBehaviour
{
    public Transform player;
    public float hideDistance = 50f; // 동적으로 설정
    public float minDistance = 2f;   // 최소 거리 (이 거리 이내면 숨김)
    private bool isVisible = true;
    private float checkInterval = 0.5f; // 거리 체크 간격 (0.5초)
    private float lastCheckTime;
    private MeshRenderer[] meshRenderers; // 모든 MeshRenderer 배열
    private Collider[] colliders; // 모든 Collider 배열
    private CustomARGeospatialCreatorAnchor anchor;

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
        anchor = GetComponentInParent<CustomARGeospatialCreatorAnchor>();

        // 초기 거리 체크
        isVisible = InRange();
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

        bool shouldBeVisible = InRange();

        if (shouldBeVisible && !isVisible)
        {
            SetVisibility(true);
        }
        else if (!shouldBeVisible && isVisible)
        {
            SetVisibility(false);
        }
    }

    /// <summary>
    /// 너무 가깝지도(카메라가 물체 안) 멀지도 않다. 먼 쪽은 땅 위 거리로 재고, 3D 로 띄우는 반경(FilterManager) 안에서는 숨기지 않는다 —
    /// 높이를 올린 '우팡산' 이 직선거리로 400 m 를 넘어 숨김 · 인디케이터 끔이 엇갈리던 것 (11-18 H3)
    /// </summary>
    private bool InRange()
    {
        Vector3 d = transform.position - player.position;
        if (d.magnitude < minDistance) return false;
        d.y = 0f;
        return d.magnitude <= Mathf.Max(hideDistance, FilterManager.FullRadiusNow);
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
        // 앵커가 아직 자리를 못 잡았으면(원점에 있다) 거리 안이어도 켜지 않는다 — 앵커가 자리를 잡으면 스스로 켠다 (11-18 H3)
        bool show = visible && (anchor == null || anchor.MayShow);
        // 모든 MeshRenderer 비활성화/활성화
        foreach (var renderer in meshRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = show;
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