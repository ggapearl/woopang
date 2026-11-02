using UnityEngine;

public class BillboardQuad : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main; // ARCamera가 메인 카메라일 거야
    }

    void LateUpdate()
    {
        // Quad가 카메라를 바라보도록 회전
        transform.LookAt(transform.position + mainCamera.transform.forward, mainCamera.transform.up);
    }
}