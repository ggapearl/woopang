using UnityEngine;
using System;

[DefaultExecutionOrder(0)]
public class Target : MonoBehaviour
{
    /// <summary>
    /// 인디케이터(Box/Arrow) 터치 시 호출되는 콜백
    /// P2P 사용자의 경우 프로필 열기 등에 사용
    /// </summary>
    [HideInInspector] public Action OnIndicatorTapped;

    [Tooltip("This color will be set by the server data")]
    [SerializeField] private Color targetColor = Color.white;

    [Tooltip("Select if box indicator is required for this target")]
    [SerializeField] private bool needBoxIndicator = true;

    [Tooltip("Select if arrow indicator is required for this target")]
    [SerializeField] private bool needArrowIndicator = true;

    [Tooltip("Select if distance text is required for this target")]
    [SerializeField] private bool needDistanceText = true;

    [Tooltip("This color will be set by the server data, matching targetColor")]
    [SerializeField] private Color distanceTextColor = Color.white;

    [Tooltip("Default size of the box indicator for this target")]
    [SerializeField] private float defaultBoxSize = 10f; // 추가: 박스 기본 사이즈

    [Tooltip("Maximum size of the box indicator for this target")]
    [SerializeField] private float maxBoxSize = 15f; // 추가: 박스 최대 사이즈

    [Tooltip("Default size of the arrow indicator for this target")]
    [SerializeField] private float defaultArrowSize = 1f;

    [Tooltip("Maximum size of the arrow indicator for this target")]
    [SerializeField] private float maxArrowSize = 2f;

    [Tooltip("Minimum distance (in meters) for size scaling")]
    [SerializeField] private float minDistance = 5f; // 추가: 최소 거리

    [Tooltip("Maximum distance (in meters) for size scaling")]
    [SerializeField] private float maxDistance = 50f; // 추가: 최대 거리

    [Tooltip("Name of the place, set by DataManager")]
    [SerializeField] private string placeName = "";

    [HideInInspector] public float gpsLatitude;
    [HideInInspector] public float gpsLongitude;

    [HideInInspector] public Indicator indicator;

    // Geospatial 앵커 캐시 (앵커 미생성 시 OffScreenIndicator에서 표시 생략용)
    private CustomARGeospatialCreatorAnchor _cachedGeoAnchor;
    private bool _geoAnchorSearched = false;

    /// <summary>
    /// 이 타겟이 Geospatial 앵커를 사용하는 경우, 앵커가 실제로 생성되었는지 반환.
    /// 앵커 컴포넌트가 없으면(P2P 등) true 반환 (필터링 안 함).
    /// </summary>
    public bool IsAnchorReady
    {
        get
        {
            if (!_geoAnchorSearched)
            {
                _cachedGeoAnchor = GetComponentInParent<CustomARGeospatialCreatorAnchor>();
                _geoAnchorSearched = true;
            }
#if UNITY_EDITOR
            // 에디터에서는 AR 앵커가 생성되지 않으므로 항상 ready로 처리
            return true;
#else
            return _cachedGeoAnchor == null || _cachedGeoAnchor.IsAnchorCreated;
#endif
        }
    }

    public Color TargetColor
    {
        get => targetColor;
        set
        {
            targetColor = value;
            distanceTextColor = value;
        }
    }

    public bool NeedBoxIndicator
    {
        get => needBoxIndicator;
        set => needBoxIndicator = value;
    }

    public bool NeedArrowIndicator
    {
        get => needArrowIndicator;
        set => needArrowIndicator = value;
    }

    public bool NeedDistanceText
    {
        get => needDistanceText;
        set => needDistanceText = value;
    }

    public Color DistanceTextColor => distanceTextColor;

    public float DefaultBoxSize => defaultBoxSize; // 추가: 박스 기본 사이즈 프로퍼티

    public float MaxBoxSize => maxBoxSize; // 추가: 박스 최대 사이즈 프로퍼티

    public float DefaultArrowSize => defaultArrowSize;

    public float MaxArrowSize => maxArrowSize;

    public float MinDistance => minDistance; // 추가: 최소 거리 프로퍼티

    public float MaxDistance => maxDistance; // 추가: 최대 거리 프로퍼티

    public string PlaceName
    {
        get => placeName;
        set => placeName = value;
    }

    private void OnEnable()
    {
        if (OffScreenIndicator.TargetStateChanged != null)
        {
            OffScreenIndicator.TargetStateChanged.Invoke(this, true);
        }
    }

    private void OnDisable()
    {
        if (OffScreenIndicator.TargetStateChanged != null)
        {
            OffScreenIndicator.TargetStateChanged.Invoke(this, false);
        }
    }

    public float GetDistanceFromCamera(Vector3 cameraPosition)
    {
        float distanceFromCamera = Vector3.Distance(cameraPosition, transform.position);
        return distanceFromCamera;
    }

    /// <summary>
    /// GPS 좌표 기반 거리 계산 (Haversine) — fallback 모드에서 비활성 타겟 거리 표시용
    /// </summary>
    public float GetGPSDistance(float userLat, float userLon)
    {
        if (gpsLatitude == 0f && gpsLongitude == 0f) return -1f;
        const float R = 6371000f;
        float dLat = Mathf.Deg2Rad * (gpsLatitude - userLat);
        float dLon = Mathf.Deg2Rad * (gpsLongitude - userLon);
        float a = Mathf.Sin(dLat / 2f) * Mathf.Sin(dLat / 2f) +
                  Mathf.Cos(Mathf.Deg2Rad * userLat) * Mathf.Cos(Mathf.Deg2Rad * gpsLatitude) *
                  Mathf.Sin(dLon / 2f) * Mathf.Sin(dLon / 2f);
        float c = 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1f - a));
        return R * c;
    }
}