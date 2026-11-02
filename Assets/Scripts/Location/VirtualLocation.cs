using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

/// <summary>
/// 에디터 모드에서 사용할 가상 GPS 좌표를 중앙에서 관리하는 싱글톤
/// XR Origin을 시뮬레이션된 GPS 위치로 이동시켜 실제 해당 위치에 있는 것처럼 동작
/// </summary>
public class VirtualLocation : MonoBehaviour
{
    private static VirtualLocation instance;
    public static VirtualLocation Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<VirtualLocation>();
                if (instance == null)
                {
                    GameObject go = new GameObject("VirtualLocation");
                    instance = go.AddComponent<VirtualLocation>();
                    Debug.Log("[VirtualLocation] 자동으로 생성되었습니다.");
                }
            }
            return instance;
        }
    }

    [Header("Editor Mock GPS Coordinates")]
    [Tooltip("에디터에서 사용할 가상 위도 (예: 서울 36.6361, 도쿄 35.6762)")]
    [SerializeField] private float mockLatitude = 36.6361f;

    [Tooltip("에디터에서 사용할 가상 경도 (예: 서울 126.8280, 도쿄 139.6503)")]
    [SerializeField] private float mockLongitude = 126.8280f;

    [Tooltip("에디터에서 사용할 가상 고도 (미터)")]
    [SerializeField] private float mockAltitude = 0f;

    [Header("XR Origin Control")]
    [Tooltip("XR Origin을 가상 GPS 위치로 이동시킬지 여부 (에디터 전용)")]
    [SerializeField] private bool moveXROriginToLocation = true;

    [Tooltip("XR Origin 참조 (null이면 자동 검색)")]
    [SerializeField] private Transform xrOrigin;

    private Vector3 lastKnownPosition;
    private float lastLatitude;
    private float lastLongitude;
    private float lastAltitude;

    [Header("Preset Locations")]
    [Tooltip("빠른 위치 변경을 위한 프리셋")]
    public LocationPreset[] presets = new LocationPreset[]
    {
        new LocationPreset { name = "청주 (기본)", latitude = 36.6361f, longitude = 126.8280f, altitude = 0f },
        new LocationPreset { name = "서울 강남", latitude = 37.4979f, longitude = 127.0276f, altitude = 0f },
        new LocationPreset { name = "부산 해운대", latitude = 35.1586f, longitude = 129.1603f, altitude = 0f },
        new LocationPreset { name = "제주도", latitude = 33.4996f, longitude = 126.5312f, altitude = 0f },
        new LocationPreset { name = "도쿄", latitude = 35.6762f, longitude = 139.6503f, altitude = 0f }
    };

    public float Latitude => mockLatitude;
    public float Longitude => mockLongitude;
    public float Altitude => mockAltitude;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        // XR Origin 자동 검색
        if (xrOrigin == null)
        {
            XROrigin xrOriginComponent = FindFirstObjectByType<XROrigin>();
            if (xrOriginComponent != null)
            {
                xrOrigin = xrOriginComponent.transform;
            }
            else
            {
                // XROrigin이 없으면 "XR Origin" 이름으로 검색
                GameObject xrObj = GameObject.Find("XR Origin");
                if (xrObj != null)
                    xrOrigin = xrObj.transform;
            }
        }

        // 초기 위치 설정
        lastLatitude = mockLatitude;
        lastLongitude = mockLongitude;
        lastAltitude = mockAltitude;
        UpdateXROriginPosition();
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR
        // 좌표가 변경되었는지 감지
        if (moveXROriginToLocation && (lastLatitude != mockLatitude || lastLongitude != mockLongitude || lastAltitude != mockAltitude))
        {
            lastLatitude = mockLatitude;
            lastLongitude = mockLongitude;
            lastAltitude = mockAltitude;
            UpdateXROriginPosition();
        }
#endif
    }

    /// <summary>
    /// 프리셋 위치 적용
    /// </summary>
    public void ApplyPreset(int index)
    {
        if (index >= 0 && index < presets.Length)
        {
            mockLatitude = presets[index].latitude;
            mockLongitude = presets[index].longitude;
            mockAltitude = presets[index].altitude;
            Debug.Log($"[VirtualLocation] 프리셋 적용: {presets[index].name} ({mockLatitude}, {mockLongitude}, {mockAltitude}m)");
        }
    }

    /// <summary>
    /// 직접 좌표 설정
    /// </summary>
    public void SetCoordinates(float lat, float lon, float alt = 0f)
    {
        mockLatitude = lat;
        mockLongitude = lon;
        mockAltitude = alt;
        Debug.Log($"[VirtualLocation] 좌표 변경: ({mockLatitude}, {mockLongitude}, {mockAltitude})");
#if UNITY_EDITOR
        UpdateXROriginPosition();
#endif
    }

    /// <summary>
    /// XR Origin을 시뮬레이션된 GPS 위치로 이동
    /// CustomARGeospatialCreatorAnchor와 같은 좌표계 사용
    /// </summary>
    private void UpdateXROriginPosition()
    {
        if (!moveXROriginToLocation || xrOrigin == null)
            return;

        // 기준점 (청주) 좌표
        double originLatitude = 36.6361;
        double originLongitude = 126.8280;

        // GPS 좌표를 미터 단위로 변환 (CustomARGeospatialCreatorAnchor와 동일한 방식)
        double deltaLat = mockLatitude - originLatitude;
        double deltaLon = mockLongitude - originLongitude;

        // 위도 1도 ≈ 111,319m, 경도는 위도에 따라 다름
        double metersPerDegreeLat = 111319.0;
        double metersPerDegreeLon = metersPerDegreeLat * System.Math.Cos(originLatitude * System.Math.PI / 180.0);

        float x = (float)(deltaLon * metersPerDegreeLon);
        float z = (float)(deltaLat * metersPerDegreeLat);

        // XR Origin을 해당 위치로 이동 (카메라가 실제 위치에 있도록)
        // 기존에는 -x, -z로 반대 방향이었으나, CustomAnchor와 일치시키기 위해 정방향으로 수정
        Vector3 newPosition = new Vector3(x, mockAltitude, z);
        xrOrigin.position = newPosition;
        lastKnownPosition = newPosition;
    }

#if UNITY_EDITOR
    [ContextMenu("청주로 이동")]
    private void MoveToCheongju() => ApplyPreset(0);

    [ContextMenu("서울 강남으로 이동")]
    private void MoveToGangnam() => ApplyPreset(1);

    [ContextMenu("부산 해운대로 이동")]
    private void MoveToBusan() => ApplyPreset(2);

    [ContextMenu("제주도로 이동")]
    private void MoveToJeju() => ApplyPreset(3);

    [ContextMenu("도쿄로 이동")]
    private void MoveToTokyo() => ApplyPreset(4);
#endif
}

[System.Serializable]
public class LocationPreset
{
    public string name;
    public float latitude;
    public float longitude;
    public float altitude;
}
