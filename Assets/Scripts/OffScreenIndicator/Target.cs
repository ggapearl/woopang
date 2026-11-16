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

    /// <summary>인디케이터 둘레에 별빛이 반짝인다 (보물찾기 보물)</summary>
    [HideInInspector] public bool Sparkle;

    /// <summary>늘 강조 — 이름 뒤 분홍 스티커 · 다른 인디케이터 위 · 거리/개수 제한 없음 (장소 추가 미리보기 '생성 중...' · '신규 오브젝트', 11-14)</summary>
    [HideInInspector] public bool Pinned;

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

    [HideInInspector] public string placeId = "";
    [HideInInspector] public double gpsLatitude;   // 장소 좌표 그대로 (double)
    [HideInInspector] public double gpsLongitude;

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

    /// <summary>다른 타겟과 같은 인디케이터로 (색·박스/화살표·크기·거리 구간) — 장소 추가 '생성 중' 자리 안내 (10-22)</summary>
    public void CopyLook(Target from)
    {
        targetColor = from.targetColor;
        distanceTextColor = from.distanceTextColor;
        needBoxIndicator = from.needBoxIndicator;
        needArrowIndicator = from.needArrowIndicator;
        needDistanceText = from.needDistanceText;
        defaultBoxSize = from.defaultBoxSize;
        maxBoxSize = from.maxBoxSize;
        defaultArrowSize = from.defaultArrowSize;
        maxArrowSize = from.maxArrowSize;
        minDistance = from.minDistance;
        maxDistance = from.maxDistance;
    }

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

    /// <summary>카메라에서 이 오브젝트까지 땅 위 거리 — 높이는 보지 않는다 (11-16 대표님 「이동거리는 평면거리로만」. 화살표 글 · 범위 · 정렬 모두)</summary>
    public float GetDistanceFromCamera(Vector3 cameraPosition)
    {
        Vector3 p = transform.position;
        float dx = p.x - cameraPosition.x, dz = p.z - cameraPosition.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>
    /// GPS 좌표 기반 거리 계산 (Haversine) — fallback 모드에서 비활성 타겟 거리 표시용
    /// </summary>
    public float GetGPSDistance(double userLat, double userLon)
    {
        if (gpsLatitude == 0 && gpsLongitude == 0) return -1f;
        return (float)GeoHere.Meters(userLat, userLon, gpsLatitude, gpsLongitude);
    }
}