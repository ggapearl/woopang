using UnityEngine;

public class BillboardText : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main; // AR 환경에서는 ARCamera가 메인 카메라일 가능성이 높음
    }

    void LateUpdate()
    {
        // 텍스트가 카메라를 바라보도록 회전
        transform.LookAt(transform.position + mainCamera.transform.forward, mainCamera.transform.up);
    }
}