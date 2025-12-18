using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 요소의 외곽에 그림자 형태로 반짝이는 펄스 효과를 적용 (단일 Shadow 사용)
/// </summary>
public class OutlineGlowEffect : MonoBehaviour
{
    [Header("Glow Settings")]
    [Tooltip("펄스 속도 (낮을수록 느림)")]
    [Range(0.1f, 10f)]
    public float pulseSpeed = 2f;

    [Tooltip("최소 밝기 (0~1)")]
    [Range(0f, 1f)]
    public float minIntensity = 0.3f;

    [Tooltip("최대 밝기 (0~1)")]
    [Range(0f, 1f)]
    public float maxIntensity = 1f;

    [Tooltip("그림자 색상")]
    public Color glowColor = new Color(1f, 1f, 1f, 0.8f);

    [Tooltip("그림자 거리 (최소)")]
    [Range(0f, 20f)]
    public float minShadowDistance = 3f;

    [Tooltip("그림자 거리 (최대)")]
    [Range(0f, 20f)]
    public float maxShadowDistance = 10f;

    [Header("Animation Type")]
    [Tooltip("펄스 애니메이션 타입")]
    public PulseType pulseType = PulseType.Smooth;

    private Shadow shadowComponent;
    private float currentTime = 0f;

    void Awake()
    {
        CreateShadow();
    }

    void CreateShadow()
    {
        // 기존 Shadow 컴포넌트 정리
        Shadow[] existingShadows = GetComponents<Shadow>();
        foreach (Shadow s in existingShadows)
        {
            Destroy(s);
        }

        // 단일 Shadow 컴포넌트 생성
        shadowComponent = gameObject.AddComponent<Shadow>();
        shadowComponent.effectColor = glowColor;
        shadowComponent.effectDistance = new Vector2(minShadowDistance, minShadowDistance);
    }

    void Update()
    {
        ApplyGlowEffect();
    }

    void ApplyGlowEffect()
    {
        if (shadowComponent == null) return;

        currentTime += Time.deltaTime * pulseSpeed;

        // 펄스 계산 (0~1 사이)
        float pulse = CalculatePulse();

        // 거리 조절 (대각선 방향으로 확장)
        float distance = Mathf.Lerp(minShadowDistance, maxShadowDistance, pulse);
        shadowComponent.effectDistance = new Vector2(distance, distance);

        // 알파값 조절
        float alpha = Mathf.Lerp(minIntensity, maxIntensity, pulse);
        Color color = glowColor;
        color.a = glowColor.a * alpha;
        shadowComponent.effectColor = color;
    }

    float CalculatePulse()
    {
        float pulse = 0f;

        switch (pulseType)
        {
            case PulseType.Smooth:
                // 부드러운 사인파
                pulse = (Mathf.Sin(currentTime) + 1f) * 0.5f;
                break;

            case PulseType.Sharp:
                // 날카로운 삼각파
                pulse = Mathf.PingPong(currentTime, 1f);
                break;

            case PulseType.Blink:
                // 깜빡임 (빠른 전환)
                pulse = Mathf.Round(Mathf.PingPong(currentTime, 1f));
                break;

            case PulseType.Ease:
                // Ease In-Out
                float t = Mathf.PingPong(currentTime, 1f);
                pulse = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                break;
        }

        return pulse;
    }

    /// <summary>
    /// 펄스 효과 리셋
    /// </summary>
    public void ResetPulse()
    {
        currentTime = 0f;
    }

    /// <summary>
    /// 그림자 색상 변경
    /// </summary>
    public void SetGlowColor(Color newColor)
    {
        glowColor = newColor;
        if (shadowComponent != null)
        {
            Color color = newColor;
            color.a = newColor.a * Mathf.Lerp(minIntensity, maxIntensity, CalculatePulse());
            shadowComponent.effectColor = color;
        }
    }

    void OnDestroy()
    {
        // Shadow 컴포넌트는 자동으로 정리됨
    }
}

/// <summary>
/// 펄스 애니메이션 타입
/// </summary>
public enum PulseType
{
    Smooth,  // 부드러운 사인파
    Sharp,   // 날카로운 삼각파
    Blink,   // 깜빡임
    Ease     // Ease In-Out
}
