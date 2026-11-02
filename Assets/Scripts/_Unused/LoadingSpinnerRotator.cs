using UnityEngine;

/// <summary>
/// LoadingSpinner를 회전시키는 간단한 스크립트
/// </summary>
public class LoadingSpinnerRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 200f;

    void Update()
    {
        transform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
    }
}
