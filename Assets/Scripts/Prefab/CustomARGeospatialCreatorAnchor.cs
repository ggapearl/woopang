using UnityEngine;
using System.Collections;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class CustomARGeospatialCreatorAnchor : MonoBehaviour
{
    private ARAnchorManager anchorManager;
    private double _lat, _lon, _alt;
    private bool _anchorCreated = false;
    private int _retryCount = 0;
    private const int MAX_RETRIES = 30; // 최대 30회 (약 30초)

    // 좌표 설정 및 앵커 생성 메서드
    public void SetCoordinatesAndCreateAnchor(double latitude, double longitude, double altitude)
    {
#if UNITY_EDITOR
        // 에디터에서는 앵커 생성 대신 대략적인 상대 위치 계산하여 배치
        double userLat = 36.6361;
        double userLon = 126.8280;

        double dLat = latitude - userLat;
        double dLon = longitude - userLon;

        double metersPerLat = 111319.9;
        double metersPerLon = 111319.9 * System.Math.Cos(userLat * (System.Math.PI / 180.0));

        float z = (float)(dLat * metersPerLat);
        float x = (float)(dLon * metersPerLon);

        transform.position = new Vector3(x, 0, z);
#else
        _lat = latitude;
        _lon = longitude;
        _alt = altitude;
        _anchorCreated = false;
        _retryCount = 0;

        // 즉시 시도 후, 실패하면 코루틴으로 재시도
        if (!TryCreateAnchor())
        {
            StartCoroutine(RetryCreateAnchor());
        }
#endif
    }

    private bool TryCreateAnchor()
    {
        if (anchorManager == null)
            anchorManager = FindFirstObjectByType<ARAnchorManager>();

        if (anchorManager == null)
        {
            Debug.LogError("[CustomAnchor] ARAnchorManager를 찾을 수 없습니다.");
            return false;
        }

        // EarthManager 상태 확인
        var earthManager = FindFirstObjectByType<AREarthManager>();
        if (earthManager == null || earthManager.EarthTrackingState != TrackingState.Tracking)
        {
            // iOS 디버깅용 상세 로그
            var extensions = FindFirstObjectByType<ARCoreExtensions>();
            Debug.Log($"[CustomAnchor] Geospatial 준비 안 됨 | " +
                $"EarthManager={earthManager != null}, " +
                $"EarthState={earthManager?.EarthState}, " +
                $"TrackingState={earthManager?.EarthTrackingState}, " +
                $"ARSession.state={ARSession.state}, " +
                $"LocationStatus={Input.location.status}, " +
                $"LocationEnabled={Input.location.isEnabledByUser}, " +
                $"ARCoreExtensions={extensions != null}, " +
                $"GeospatialMode={extensions?.ARCoreExtensionsConfig?.GeospatialMode}");
            return false;
        }

        ARGeospatialAnchor anchor = anchorManager.AddAnchor(_lat, _lon, _alt, Quaternion.identity);

        if (anchor != null)
        {
            transform.SetParent(anchor.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _anchorCreated = true;
            Debug.Log($"[CustomAnchor] 앵커 생성 성공: {gameObject.name} (재시도 {_retryCount}회)");
            return true;
        }

        Debug.Log($"[CustomAnchor] AddAnchor null 반환: {gameObject.name}");
        return false;
    }

    private IEnumerator RetryCreateAnchor()
    {
        while (!_anchorCreated && _retryCount < MAX_RETRIES)
        {
            yield return new WaitForSeconds(1f);
            _retryCount++;

            if (TryCreateAnchor())
                yield break;
        }

        if (!_anchorCreated)
        {
            Debug.LogError($"[CustomAnchor] 앵커 생성 최종 실패: {gameObject.name} ({MAX_RETRIES}회 재시도 후)");
        }
    }
}
