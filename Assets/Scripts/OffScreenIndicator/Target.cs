using UnityEngine;

[DefaultExecutionOrder(0)]
public class Target : MonoBehaviour
{
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

    public bool NeedBoxIndicator => needBoxIndicator;

    public bool NeedArrowIndicator => needArrowIndicator;

    public bool NeedDistanceText => needDistanceText;

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
}