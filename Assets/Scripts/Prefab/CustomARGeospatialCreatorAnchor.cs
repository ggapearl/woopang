using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;

public class CustomARGeospatialCreatorAnchor : MonoBehaviour
{
    private ARAnchorManager anchorManager;

    // 좌표 설정 및 앵커 생성 메서드
    public void SetCoordinatesAndCreateAnchor(double latitude, double longitude, double altitude)
    {
        if (anchorManager == null) anchorManager = FindObjectOfType<ARAnchorManager>();

        if (anchorManager != null)
        {
            // 회전값 (기본값)
            Quaternion rotation = Quaternion.identity;

            // ARGeospatialAnchor 생성 (Runtime API 사용)
            // 주의: ARAnchorManager.AddAnchor는 Geospatial 기능이 활성화된 상태에서만 동작함
            ARGeospatialAnchor anchor = anchorManager.AddAnchor(latitude, longitude, altitude, rotation);

            if (anchor != null)
            {
                // 생성된 앵커의 자식으로 설정하여 위치 고정
                transform.SetParent(anchor.transform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                
                Debug.Log($"[CustomAnchor] 앵커 생성 성공: {latitude}, {longitude}");
            }
            else
            {
                Debug.LogError($"[CustomAnchor] 앵커 생성 실패: {latitude}, {longitude}");
            }
        }
        else
        {
            Debug.LogError("[CustomAnchor] ARAnchorManager를 찾을 수 없습니다.");
        }
    }
}