using UnityEngine;

/// <summary>
/// Advanced Golden Glow Cube with particle light bleeding effects
/// 파티클 빛 번짐 효과가 있는 고급 황금빛 큐브
/// </summary>
public class GoldenGlowCubeAdvanced : MonoBehaviour
{
    [Header("Cube Settings")]
    [SerializeField] private GoldenGlowCube baseCube;

    [Header("Particle Glow Effect")]
    [SerializeField] private bool enableParticleGlow = true;
    [SerializeField] private int particleCount = 20;
    [SerializeField] private float particleRadius = 0.8f;
    [SerializeField] private Color particleColor = new Color(1f, 0.85f, 0.4f, 0.3f);
    [SerializeField] private float particleSize = 0.05f;
    [SerializeField] private float particleLifetime = 2f;

    [Header("Sparkle Effect")]
    [SerializeField] private bool enableSparkle = true;
    [SerializeField] private int sparkleCount = 5;
    [SerializeField] private float sparkleIntensity = 2f;

    [Header("Light")]
    [SerializeField] private bool enablePointLight = true;
    [SerializeField] private Color lightColor = new Color(0.83f, 0.69f, 0.22f, 1f);
    [SerializeField] private float lightIntensity = 1f;
    [SerializeField] private float lightRange = 2f;
    [SerializeField] private bool animateLight = true;

    // Components
    private ParticleSystem glowParticles;
    private ParticleSystem sparkleParticles;
    private Light pointLight;
    private float baseLightIntensity;

    private void Awake()
    {
        // Get or add base cube component
        if (baseCube == null)
        {
            baseCube = GetComponent<GoldenGlowCube>();
            if (baseCube == null)
            {
                baseCube = gameObject.AddComponent<GoldenGlowCube>();
            }
        }
    }

    private void Start()
    {
        if (enableParticleGlow)
        {
            SetupGlowParticles();
        }

        if (enableSparkle)
        {
            SetupSparkleParticles();
        }

        if (enablePointLight)
        {
            SetupPointLight();
        }
    }

    private void Update()
    {
        if (animateLight && pointLight != null)
        {
            // Animate light intensity
            float pulse = (Mathf.Sin(Time.time * 2f) + 1f) * 0.5f;
            pointLight.intensity = baseLightIntensity * (0.8f + pulse * 0.4f);
        }
    }

    private void SetupGlowParticles()
    {
        // Create particle system for ambient glow
        GameObject particleObj = new GameObject("GlowParticles");
        particleObj.transform.SetParent(transform);
        particleObj.transform.localPosition = Vector3.zero;

        glowParticles = particleObj.AddComponent<ParticleSystem>();

        // Main module
        var main = glowParticles.main;
        main.loop = true;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0.1f;
        main.startSize = particleSize;
        main.startColor = particleColor;
        main.maxParticles = particleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        // Emission
        var emission = glowParticles.emission;
        emission.rateOverTime = particleCount / particleLifetime;

        // Shape - sphere around the cube
        var shape = glowParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = particleRadius;
        shape.radiusThickness = 0.3f;

        // Color over lifetime - fade out
        var colorOverLifetime = glowParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(particleColor, 0f),
                new GradientColorKey(particleColor, 0.5f),
                new GradientColorKey(particleColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.5f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // Size over lifetime - pulse
        var sizeOverLifetime = glowParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0.5f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Renderer
        var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // Try to use additive material
        Material particleMat = new Material(Shader.Find("Particles/Standard Unlit"));
        if (particleMat != null)
        {
            particleMat.SetColor("_Color", particleColor);
            particleMat.SetInt("_BlendMode", 1); // Additive
            renderer.material = particleMat;
        }
    }

    private void SetupSparkleParticles()
    {
        // Create sparkle particles for star-like glints
        GameObject sparkleObj = new GameObject("SparkleParticles");
        sparkleObj.transform.SetParent(transform);
        sparkleObj.transform.localPosition = Vector3.zero;

        sparkleParticles = sparkleObj.AddComponent<ParticleSystem>();

        // Main module
        var main = sparkleParticles.main;
        main.loop = true;
        main.startLifetime = 0.5f;
        main.startSpeed = 0f;
        main.startSize = 0.1f;
        main.startColor = new Color(1f, 0.95f, 0.7f, 1f);
        main.maxParticles = sparkleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        // Emission - bursts
        var emission = sparkleParticles.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, (short)sparkleCount, (short)sparkleCount, 1, 0.8f)
        });

        // Shape - surface of cube
        var shape = sparkleParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = Vector3.one * 0.6f;

        // Color over lifetime - flash
        var colorOverLifetime = sparkleParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // Size over lifetime - flash burst
        var sizeOverLifetime = sparkleParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 1f),
            new Keyframe(0.3f, 0.3f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Renderer
        var renderer = sparkleObj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Material sparkleMat = new Material(Shader.Find("Particles/Standard Unlit"));
        if (sparkleMat != null)
        {
            sparkleMat.SetColor("_Color", Color.white * sparkleIntensity);
            sparkleMat.SetInt("_BlendMode", 1); // Additive
            renderer.material = sparkleMat;
        }
    }

    private void SetupPointLight()
    {
        // Create point light for real-time lighting
        GameObject lightObj = new GameObject("PointLight");
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = Vector3.zero;

        pointLight = lightObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = lightColor;
        pointLight.intensity = lightIntensity;
        pointLight.range = lightRange;
        pointLight.shadows = LightShadows.Soft;

        baseLightIntensity = lightIntensity;
    }

    /// <summary>
    /// Enable/disable particle effects
    /// </summary>
    public void SetParticlesEnabled(bool enabled)
    {
        if (glowParticles != null)
        {
            if (enabled) glowParticles.Play();
            else glowParticles.Stop();
        }

        if (sparkleParticles != null)
        {
            if (enabled) sparkleParticles.Play();
            else sparkleParticles.Stop();
        }
    }

    /// <summary>
    /// Trigger sparkle burst effect (for interaction feedback)
    /// </summary>
    public void TriggerSparkleBurst()
    {
        if (sparkleParticles != null)
        {
            sparkleParticles.Emit(sparkleCount * 2);
        }
    }

    /// <summary>
    /// Set overall intensity of all effects
    /// </summary>
    public void SetEffectIntensity(float intensity)
    {
        if (pointLight != null)
        {
            baseLightIntensity = lightIntensity * intensity;
        }

        if (glowParticles != null)
        {
            var main = glowParticles.main;
            Color newColor = particleColor;
            newColor.a *= intensity;
            main.startColor = newColor;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Setup All Effects")]
    private void EditorSetupEffects()
    {
        // Remove existing child effects
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "GlowParticles" || child.name == "SparkleParticles" || child.name == "PointLight")
            {
                DestroyImmediate(child.gameObject);
            }
        }

        Start();
    }
#endif
}
