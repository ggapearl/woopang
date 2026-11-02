using UnityEngine;

/// <summary>
/// 홈페이지 히어로 섹션의 3D AR 오브젝트와 동일한 스타일의 황금빛 글로우 큐브
/// Golden Glow Cube - Website-style 3D AR Object with gold emission
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class GoldenGlowCube : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private bool autoRotate = true;
    [SerializeField] private float rotateSpeedY = 15f;     // Y축 회전 속도
    [SerializeField] private float rotateSpeedX = 10f;     // X축 회전 속도

    [Header("Float Animation")]
    [SerializeField] private bool enableFloat = true;
    [SerializeField] private float floatAmplitude = 0.15f; // 상하 움직임 폭
    [SerializeField] private float floatSpeed = 1.5f;      // 상하 움직임 속도

    [Header("Glow Settings")]
    [SerializeField] private Color baseColor = new Color(0.83f, 0.69f, 0.22f, 1f);  // #D4AF37 황금색
    [SerializeField] private Color glowColor = new Color(1f, 0.85f, 0.4f, 1f);       // 밝은 황금색
    [SerializeField] private float glowIntensity = 2f;
    [SerializeField] private float glowPulseSpeed = 1f;
    [SerializeField] private float glowPulseMin = 1.5f;
    [SerializeField] private float glowPulseMax = 3f;

    [Header("Edge Glow")]
#pragma warning disable CS0414 // Inspector 설정용 필드
    [SerializeField] private bool enableEdgeGlow = true;
#pragma warning restore CS0414
    [SerializeField] private Color edgeGlowColor = new Color(1f, 0.9f, 0.5f, 0.5f);
#pragma warning disable CS0414 // Inspector 설정용 필드
    [SerializeField] private float edgeWidth = 0.02f;
#pragma warning restore CS0414

    [Header("Interaction")]
    [SerializeField] private bool enableTouchResponse = true;
    [SerializeField] private float touchGlowMultiplier = 2f;
    [SerializeField] private float touchScaleMultiplier = 1.15f;
    [SerializeField] private float touchResponseSpeed = 5f;

    // Internal state
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 initialPosition;
    private Vector3 initialScale;
    private float floatOffset;
    private float glowPhaseOffset;  // 랜덤 glow 위상 오프셋 (타이밍 분산)
    private float currentGlowMultiplier = 1f;
    private float targetGlowMultiplier = 1f;
    private float currentScale = 1f;
    private float targetScale = 1f;
#pragma warning disable CS0414 // 터치 상태 추적용 (할당만 되지만 향후 사용 예정)
    private bool isTouched = false;
#pragma warning restore CS0414

    // Shader property IDs (for performance)
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int PulsePhaseOffsetID = Shader.PropertyToID("_PulsePhaseOffset");

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();
        initialPosition = transform.localPosition;
        initialScale = transform.localScale;
        floatOffset = Random.Range(0f, Mathf.PI * 2f); // 랜덤 float 시작 위치
        glowPhaseOffset = Random.Range(0f, Mathf.PI * 2f); // 랜덤 glow 타이밍
    }

    private void Start()
    {
        SetupMaterial();
    }

    private void SetupMaterial()
    {
        if (meshRenderer == null || meshRenderer.sharedMaterial == null) return;

        // Enable emission on material if possible
        meshRenderer.GetPropertyBlock(propertyBlock);

        // Set base color
        if (meshRenderer.sharedMaterial.HasProperty("_BaseColor"))
        {
            propertyBlock.SetColor(BaseColorID, baseColor);
        }
        else if (meshRenderer.sharedMaterial.HasProperty("_Color"))
        {
            propertyBlock.SetColor(ColorID, baseColor);
        }

        // 랜덤 위상 오프셋 — 같은 프리팹이라도 반짝이는 타이밍이 다르게
        propertyBlock.SetFloat(PulsePhaseOffsetID, glowPhaseOffset);

        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void Update()
    {
        // Auto rotation
        if (autoRotate)
        {
            transform.Rotate(Vector3.up, rotateSpeedY * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right, rotateSpeedX * Time.deltaTime, Space.Self);
        }

        // Float animation
        if (enableFloat)
        {
            float floatY = Mathf.Sin((Time.time + floatOffset) * floatSpeed) * floatAmplitude;
            transform.localPosition = initialPosition + Vector3.up * floatY;
        }

        // Glow pulse animation
        UpdateGlow();

        // Touch response interpolation
        UpdateTouchResponse();
    }

    private void UpdateGlow()
    {
        if (meshRenderer == null) return;

        // Calculate pulsing glow intensity
        float pulseValue = Mathf.Lerp(glowPulseMin, glowPulseMax,
            (Mathf.Sin(Time.time * glowPulseSpeed + glowPhaseOffset) + 1f) * 0.5f);

        float finalIntensity = glowIntensity * pulseValue * currentGlowMultiplier;

        // Apply emission color
        meshRenderer.GetPropertyBlock(propertyBlock);
        Color emission = glowColor * finalIntensity;

        if (meshRenderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            propertyBlock.SetColor(EmissionColorID, emission);
        }

        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateTouchResponse()
    {
        if (!enableTouchResponse) return;

        // Smooth interpolation for glow
        currentGlowMultiplier = Mathf.Lerp(currentGlowMultiplier, targetGlowMultiplier,
            Time.deltaTime * touchResponseSpeed);

        // Smooth interpolation for scale
        currentScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * touchResponseSpeed);
        transform.localScale = initialScale * currentScale;
    }

    /// <summary>
    /// Called when user touches/hovers over the cube
    /// </summary>
    public void OnTouchEnter()
    {
        if (!enableTouchResponse) return;
        isTouched = true;
        targetGlowMultiplier = touchGlowMultiplier;
        targetScale = touchScaleMultiplier;
    }

    /// <summary>
    /// Called when user stops touching/hovering
    /// </summary>
    public void OnTouchExit()
    {
        if (!enableTouchResponse) return;
        isTouched = false;
        targetGlowMultiplier = 1f;
        targetScale = 1f;
    }

    /// <summary>
    /// Pause/Resume rotation (for when user interacts)
    /// </summary>
    public void SetRotationPaused(bool paused)
    {
        autoRotate = !paused;
    }

    /// <summary>
    /// Set custom glow color at runtime
    /// </summary>
    public void SetGlowColor(Color color)
    {
        glowColor = color;
    }

    /// <summary>
    /// Set glow intensity at runtime
    /// </summary>
    public void SetGlowIntensity(float intensity)
    {
        glowIntensity = intensity;
    }

    #region Static Factory Methods

    /// <summary>
    /// Create a golden glow cube programmatically
    /// </summary>
    public static GameObject CreateGoldenGlowCube(Transform parent = null, Vector3? position = null)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "GoldenGlowCube";

        if (parent != null)
        {
            cube.transform.SetParent(parent);
        }

        cube.transform.localPosition = position ?? Vector3.zero;
        cube.transform.localScale = Vector3.one * 0.5f;

        // Add component
        GoldenGlowCube glowCube = cube.AddComponent<GoldenGlowCube>();

        // Setup material with emission
        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        if (mat != null)
        {
            // Set base properties
            mat.SetColor("_BaseColor", new Color(0.12f, 0.08f, 0.16f, 1f)); // 짙은 보라-갈색
            mat.SetFloat("_Metallic", 0.3f);
            mat.SetFloat("_Smoothness", 0.7f);

            // Enable emission
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.83f, 0.69f, 0.22f, 1f) * 2f);

            renderer.material = mat;
        }

        return cube;
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualize float range
        if (enableFloat)
        {
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.3f);
            Vector3 pos = Application.isPlaying ? initialPosition : transform.localPosition;
            Gizmos.DrawWireCube(transform.parent != null ?
                transform.parent.TransformPoint(pos + Vector3.up * floatAmplitude) : pos + Vector3.up * floatAmplitude,
                transform.lossyScale);
            Gizmos.DrawWireCube(transform.parent != null ?
                transform.parent.TransformPoint(pos - Vector3.up * floatAmplitude) : pos - Vector3.up * floatAmplitude,
                transform.lossyScale);
        }
    }
#endif
}
