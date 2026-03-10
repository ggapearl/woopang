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

    // Sparkle 효과를 한 번만 재생하기 위한 플래그
    [HideInInspector] public bool hasPlayedSparkle = false;

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

        // Target이 완전히 비활성화되면 Sparkle 플래그 리셋
        // (다시 활성화될 때 Sparkle 재생)
        hasPlayedSparkle = false;
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