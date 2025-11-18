using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class AttachToFixedAltitude : MonoBehaviour
{
    private ARAnchorManager anchorManager;
    private AREarthManager earthManager;
    private ARGeospatialAnchor anchor;
    private Transform objectTransform;

    public double fixedAltitude = 1.0; // ✅ 항상 고도 1m 유지

    void Start()
    {
        anchorManager = FindObjectOfType<ARAnchorManager>();
        earthManager = FindObjectOfType<AREarthManager>();
        objectTransform = transform;

        if (earthManager == null || anchorManager == null)
        {
            Debug.LogError("⚠ AR 시스템이 올바르게 설정되지 않았습니다.");
            return;
        }

        if (earthManager.EarthTrackingState != TrackingState.Tracking)
        {
            Debug.LogWarning("⚠ AR Earth Tracking이 활성화되지 않았습니다.");
            return;
        }

        // 🌍 현재 오브젝트의 위도/경도를 가져오기
        GeospatialPose pose = earthManager.CameraGeospatialPose;
        double latitude = pose.Latitude;
        double longitude = pose.Longitude;

        // 🛰 오브젝트가 배치될 때 고도를 1m로 강제
        anchor = anchorManager.AddAnchor(latitude, longitude, fixedAltitude, Quaternion.identity);

        if (anchor != null)
        {
            objectTransform.SetParent(anchor.transform);
            Debug.Log($"📍 고도 무시: {gameObject.name} 오브젝트가 (Lat: {latitude}, Lon: {longitude}, Alt: {fixedAltitude})에 배치됨.");
        }
        else
        {
            Debug.LogError("⚠ 오브젝트를 고도 1m에 배치하는 Anchor 생성 실패.");
        }
    }
}
