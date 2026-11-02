using UnityEngine;

public class RotateObject : MonoBehaviour
{
    [SerializeField]
    [Tooltip("회전 속도 (도/초)")]
    private float rotationSpeed = 90f; // 기본 회전 속도: 90도/초

    [SerializeField]
    [Tooltip("시계방향 회전 여부 (체크 해제 시 반시계방향)")]
    private bool clockwise = true; // 시계방향 회전 여부

    void Update()
    {
        // 로컬 Z축을 기준으로 회전
        float direction = clockwise ? -1f : 1f; // 시계방향이면 음수, 반시계방향이면 양수
        transform.Rotate(Vector3.forward * direction * rotationSpeed * Time.deltaTime, Space.Self);
    }
}