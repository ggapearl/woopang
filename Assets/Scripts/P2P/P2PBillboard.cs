/**
 * P2PBillboard.cs
 * 3D 오브젝트를 항상 카메라 방향으로 향하게 하는 스크립트
 * - 말풍선, 사용자 정보 UI 등에 사용
 * - Utils/Billboard.cs와 이름 충돌을 피하기 위해 P2PBillboard로 명명
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-01
 */

using UnityEngine;

public class P2PBillboard : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("카메라 참조 (null이면 메인 카메라 자동 탐색)")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("오브젝트 위치 오프셋")]
    [SerializeField] private Vector3 offset = Vector3.zero;

    [Tooltip("Y축만 회전 (평면 회전)")]
    [SerializeField] private bool lockX = false;
    [SerializeField] private bool lockY = false;
    [SerializeField] private bool lockZ = false;

    [Tooltip("역방향 (카메라 반대쪽 향함)")]
    [SerializeField] private bool reverse = false;

    void Start()
    {
        // 카메라 자동 탐색
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // 오프셋 적용
        if (offset != Vector3.zero)
        {
            transform.localPosition += offset;
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        // 카메라 방향 계산
        Vector3 directionToCamera = reverse
            ? transform.position - targetCamera.transform.position
            : targetCamera.transform.position - transform.position;

        // 축 잠금 처리
        if (lockX) directionToCamera.x = 0;
        if (lockY) directionToCamera.y = 0;
        if (lockZ) directionToCamera.z = 0;

        // 방향이 0이 아닐 때만 회전
        if (directionToCamera.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
            transform.rotation = targetRotation;
        }
    }
}
