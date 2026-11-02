using UnityEngine;

/// <summary>
/// 3D 구형 로딩 인디케이터
/// 작은 구가 천천히 위아래로 떠다니며 회전하는 애니메이션
/// </summary>
public class LoadingIndicator3D : MonoBehaviour
{
    [Header("Float Animation")]
    [Tooltip("위아래로 떠다니는 속도")]
    [SerializeField] private float floatSpeed = 1.0f;

    [Tooltip("위아래로 떠다니는 높이 (미터)")]
    [SerializeField] private float floatHeight = 0.3f;

    [Header("Rotation Animation")]
    [Tooltip("회전 속도 (도/초)")]
    [SerializeField] private float rotationSpeed = 90f;

    [Tooltip("회전 축 (X, Y, Z)")]
    [SerializeField] private Vector3 rotationAxis = new Vector3(0.3f, 1f, 0.2f);

    [Header("Pulse Animation")]
    [Tooltip("크기 변화 활성화")]
    [SerializeField] private bool enablePulse = true;

    [Tooltip("펄스 속도")]
    [SerializeField] private float pulseSpeed = 2.0f;

    [Tooltip("최소 크기 비율")]
    [SerializeField] private float minScale = 0.9f;

    [Tooltip("최대 크기 비율")]
    [SerializeField] private float maxScale = 1.1f;

    private Vector3 startPosition;
    private Vector3 baseScale;

    void Start()
    {
        startPosition = transform.position;
        baseScale = transform.localScale;
    }

    void Update()
    {
        // 1. 위아래 떠다니는 애니메이션
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);

        // 2. 회전 애니메이션
        transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.World);

        // 3. 펄스 애니메이션 (크기 변화)
        if (enablePulse)
        {
            float scale = Mathf.Lerp(minScale, maxScale, (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f);
            transform.localScale = baseScale * scale;
        }
    }

    /// <summary>
    /// 위치 초기화 (새 위치로 이동했을 때)
    /// </summary>
    public void ResetPosition(Vector3 newPosition)
    {
        startPosition = newPosition;
        transform.position = newPosition;
    }
}
