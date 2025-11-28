using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 이미지에 T5EdgeLine 효과를 적용하는 컴포넌트
/// 기존 3D 큐브의 T5EdgeLine 효과를 2D UI에 맞게 적용
/// </summary>
[RequireComponent(typeof(Image))]
public class T5EdgeLineEffect : MonoBehaviour
{
    [Header("Edge Line Settings")]
    [SerializeField] private Color edgeColor = new Color(1f, 0.95f, 0.8f, 1f);
    [SerializeField] [Range(0.001f, 0.1f)] private float edgeWidth = 0.015f;
    [SerializeField] [Range(0f, 10f)] private float edgeIntensity = 3.0f;
    [SerializeField] [Range(0.1f, 10f)] private float edgeSharpness = 2.0f;

    [Header("Animation")]
    [SerializeField] [Range(0f, 5f)] private float glowPulseSpeed = 1.0f;
    [SerializeField] [Range(0f, 1f)] private float minGlow = 0.8f;

    [Header("Corner")]
    [SerializeField] [Range(0f, 0.5f)] private float cornerRadius = 0.05f;

    private Material material;
    private Image image;
    private static Shader cachedShader;

    void Awake()
    {
        image = GetComponent<Image>();
        ApplyEffect();
    }

    void OnEnable()
    {
        if (material == null)
        {
            ApplyEffect();
        }
    }

    public void ApplyEffect()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        // T5EdgeLine_UI 셰이더 찾기
        if (cachedShader == null)
        {
            cachedShader = Shader.Find("UI/T5EdgeLine");
        }

        if (cachedShader != null)
        {
            material = new Material(cachedShader);
            image.material = material;
            UpdateMaterialProperties();
        }
        else
        {
            Debug.LogWarning("[T5EdgeLineEffect] UI/T5EdgeLine shader를 찾을 수 없습니다.");
        }
    }

    void Update()
    {
        if (material != null)
        {
            UpdateMaterialProperties();
        }
    }

    private void UpdateMaterialProperties()
    {
        if (material == null) return;

        material.SetColor("_EdgeColor", edgeColor);
        material.SetFloat("_EdgeWidth", edgeWidth);
        material.SetFloat("_EdgeIntensity", edgeIntensity);
        material.SetFloat("_EdgeSharpness", edgeSharpness);
        material.SetFloat("_GlowPulseSpeed", glowPulseSpeed);
        material.SetFloat("_MinGlow", minGlow);
        material.SetFloat("_CornerRadius", cornerRadius);
    }

    /// <summary>
    /// 효과 설정 변경
    /// </summary>
    public void SetSettings(Color color, float width, float intensity, float sharpness, float pulseSpeed, float minGlowValue, float radius)
    {
        edgeColor = color;
        edgeWidth = width;
        edgeIntensity = intensity;
        edgeSharpness = sharpness;
        glowPulseSpeed = pulseSpeed;
        minGlow = minGlowValue;
        cornerRadius = radius;
        UpdateMaterialProperties();
    }

    /// <summary>
    /// 효과 강도만 변경
    /// </summary>
    public void SetIntensity(float intensity)
    {
        edgeIntensity = intensity;
        if (material != null)
        {
            material.SetFloat("_EdgeIntensity", edgeIntensity);
        }
    }

    /// <summary>
    /// 라인 색상만 변경
    /// </summary>
    public void SetEdgeColor(Color color)
    {
        edgeColor = color;
        if (material != null)
        {
            material.SetColor("_EdgeColor", edgeColor);
        }
    }

    void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying && material != null)
        {
            UpdateMaterialProperties();
        }
    }
#endif
}
