using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;

public class CustomARGeospatialCreatorAnchor : MonoBehaviour
{
    private ARAnchorManager anchorManager;

    // 좌표 설정 및 앵커 생성 메서드
    public void SetCoordinatesAndCreateAnchor(double latitude, double longitude, double altitude)
    {
#if UNITY_EDITOR
        // 에디터에서는 앵커 생성 대신 대략적인 상대 위치 계산하여 배치
        // 기준: 사용자 위치 (36.6361, 126.8280)
        double userLat = 36.6361;
        double userLon = 126.8280;

        double dLat = latitude - userLat;
        double dLon = longitude - userLon;

        // 대략적인 미터 환산 (위도 36.6361도 기준)
        double metersPerLat = 111319.9;
        double metersPerLon = 111319.9 * System.Math.Cos(userLat * (System.Math.PI / 180.0));

        float z = (float)(dLat * metersPerLat);
        float x = (float)(dLon * metersPerLon);

        // 카메라가 (0,0,0)에 있다고 가정하고 상대 위치 배치
        transform.position = new Vector3(x, 0, z); // y는 0으로 고정 (또는 altitude 반영)
        
        Debug.Log($"[CustomAnchor] (에디터) 앵커 시뮬레이션 배치: {latitude}, {longitude} -> Pos({x:F1}, 0, {z:F1})");
        return;
#endif

        if (anchorManager == null) anchorManager = FindFirstObjectByType<ARAnchorManager>();

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