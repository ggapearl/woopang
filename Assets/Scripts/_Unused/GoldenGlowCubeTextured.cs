using UnityEngine;

/// <summary>
/// 이미지/텍스처를 적용해도 모서리 빛 번짐 효과가 유지되는 황금빛 큐브
/// Golden Glow Cube with texture support - edge glow maintained even with images
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class GoldenGlowCubeTextured : MonoBehaviour
{
    [Header("Texture Settings")]
    [SerializeField] private Texture2D faceTexture;
    [SerializeField] [Range(0f, 1f)] private float textureBlend = 1f;
    [SerializeField] [Range(0f, 1f)] private float goldTintStrength = 0.2f;

    [Header("Edge Glow Settings")]
    [SerializeField] private Color edgeGlowColor = new Color(1f, 0.9f, 0.5f, 1f);
    [SerializeField] [Range(0f, 5f)] private float edgeIntensity = 2f;
    [SerializeField] [Range(0.1f, 10f)] private float edgePower = 2.5f;
    [SerializeField] [Range(0f, 0.5f)] private float edgeWidth = 0.15f;

    [Header("Emission Settings")]
    [SerializeField] private Color emissionColor = new Color(0.83f, 0.69f, 0.22f, 1f);
    [SerializeField] [Range(0f, 3f)] private float emissionIntensity = 0.5f;

    [Header("Animation")]
    [SerializeField] private bool enablePulse = true;
    [SerializeField] [Range(0f, 5f)] private float pulseSpeed = 1f;
    [SerializeField] [Range(0f, 2f)] private float pulseMin = 0.8f;
    [SerializeField] [Range(1f, 5f)] private float pulseMax = 1.3f;

    [Header("Rotation Settings")]
    [SerializeField] private bool autoRotate = true;
    [SerializeField] private float rotateSpeedY = 15f;
    [SerializeField] private float rotateSpeedX = 10f;

    [Header("Float Animation")]
    [SerializeField] private bool enableFloat = true;
    [SerializeField] private float floatAmplitude = 0.15f;
    [SerializeField] private float floatSpeed = 1.5f;

    // Components
    private MeshRenderer meshRenderer;
    private Material material;
    private Vector3 initialPosition;
    private float floatOffset;
    private float glowPhaseOffset; // 랜덤 glow 타이밍 오프셋

    // Shader property IDs
    private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
    private static readonly int TextureBlendID = Shader.PropertyToID("_TextureBlend");
    private static readonly int TintStrengthID = Shader.PropertyToID("_TintStrength");
    private static readonly int EdgeColorID = Shader.PropertyToID("_EdgeColor");
    private static readonly int EdgeIntensityID = Shader.PropertyToID("_EdgeIntensity");
    private static readonly int EdgePowerID = Shader.PropertyToID("_EdgePower");
    private static readonly int EdgeWidthID = Shader.PropertyToID("_EdgeWidth");
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionIntensityID = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int PulseSpeedID = Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseMinID = Shader.PropertyToID("_PulseMin");
    private static readonly int PulseMaxID = Shader.PropertyToID("_PulseMax");
    private static readonly int PulsePhaseOffsetID = Shader.PropertyToID("_PulsePhaseOffset");

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        initialPosition = transform.localPosition;
        floatOffset = Random.Range(0f, Mathf.PI * 2f);
        glowPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Start()
    {
        SetupMaterial();
        ApplyMaterialSettings();
    }

    private void SetupMaterial()
    {
        // 커스텀 셰이더 찾기
        Shader customShader = Shader.Find("Woopang/GoldenGlowCubeTextured");

        if (customShader != null)
        {
            material = new Material(customShader);
        }
        else
        {
            // Fallback to standard shader with emission
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null)
            {
                material = new Material(urpLit);
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material = new Material(Shader.Find("Standard"));
                material.EnableKeyword("_EMISSION");
            }
            Debug.LogWarning("GoldenGlowCubeTextured shader not found. Using fallback.");
        }

        meshRenderer.material = material;
    }

    private void ApplyMaterialSettings()
    {
        if (material == null) return;

        // Texture
        if (faceTexture != null)
        {
            material.SetTexture(MainTexID, faceTexture);
        }
        material.SetFloat(TextureBlendID, textureBlend);
        material.SetFloat(TintStrengthID, goldTintStrength);

        // Edge glow
        material.SetColor(EdgeColorID, edgeGlowColor);
        material.SetFloat(EdgeIntensityID, edgeIntensity);
        material.SetFloat(EdgePowerID, edgePower);
        material.SetFloat(EdgeWidthID, edgeWidth);

        // Emission
        material.SetColor(EmissionColorID, emissionColor);
        material.SetFloat(EmissionIntensityID, emissionIntensity);

        // Pulse animation
        if (enablePulse)
        {
            material.SetFloat(PulseSpeedID, pulseSpeed);
            material.SetFloat(PulseMinID, pulseMin);
            material.SetFloat(PulseMaxID, pulseMax);
        }
        else
        {
            material.SetFloat(PulseSpeedID, 0f);
            material.SetFloat(PulseMinID, 1f);
            material.SetFloat(PulseMaxID, 1f);
        }

        // 랜덤 위상 오프셋 — 같은 프리팹이라도 반짝이는 타이밍이 다르게
        material.SetFloat(PulsePhaseOffsetID, glowPhaseOffset);
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
    }

    /// <summary>
    /// 런타임에 텍스처 변경
    /// </summary>
    public void SetTexture(Texture2D texture)
    {
        faceTexture = texture;
        if (material != null && texture != null)
        {
            material.SetTexture(MainTexID, texture);
        }
    }

    /// <summary>
    /// 텍스처 블렌드 조절 (0 = 기본 색상만, 1 = 텍스처만)
    /// </summary>
    public void SetTextureBlend(float blend)
    {
        textureBlend = Mathf.Clamp01(blend);
        if (material != null)
        {
            material.SetFloat(TextureBlendID, textureBlend);
        }
    }

    /// <summary>
    /// 모서리 글로우 강도 조절
    /// </summary>
    public void SetEdgeGlowIntensity(float intensity)
    {
        edgeIntensity = Mathf.Max(0, intensity);
        if (material != null)
        {
            material.SetFloat(EdgeIntensityID, edgeIntensity);
        }
    }

    /// <summary>
    /// 모서리 글로우 색상 변경
    /// </summary>
    public void SetEdgeGlowColor(Color color)
    {
        edgeGlowColor = color;
        if (material != null)
        {
            material.SetColor(EdgeColorID, edgeGlowColor);
        }
    }

    /// <summary>
    /// 모서리 글로우 두께 조절
    /// </summary>
    public void SetEdgeWidth(float width)
    {
        edgeWidth = Mathf.Clamp(width, 0f, 0.5f);
        if (material != null)
        {
            material.SetFloat(EdgeWidthID, edgeWidth);
        }
    }

    /// <summary>
    /// 회전 일시정지/재개
    /// </summary>
    public void SetRotationPaused(bool paused)
    {
        autoRotate = !paused;
    }

    /// <summary>
    /// 전체 효과 강도 조절
    /// </summary>
    public void SetOverallIntensity(float intensity)
    {
        if (material == null) return;

        material.SetFloat(EdgeIntensityID, edgeIntensity * intensity);
        material.SetFloat(EmissionIntensityID, emissionIntensity * intensity);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 값 변경 시 즉시 반영
        if (Application.isPlaying && material != null)
        {
            ApplyMaterialSettings();
        }
    }

    [ContextMenu("Apply Settings")]
    private void EditorApplySettings()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer.sharedMaterial != null)
        {
            material = meshRenderer.sharedMaterial;
            ApplyMaterialSettings();
        }
    }
#endif
}
