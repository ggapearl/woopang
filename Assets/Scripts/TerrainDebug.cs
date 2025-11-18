using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Google.XR.ARCoreExtensions; // <-- ARGeospatialAnchor를 위한 네임스페이스 추가

public class TerrainDebug : MonoBehaviour
{
    private ARGeospatialAnchor anchor;

    void Start()
    {
        anchor = GetComponent<ARGeospatialAnchor>();

        if (anchor != null)
        {
            Debug.Log($"📍 현재 위도 (Latitude): {anchor.transform.position.x}");
            Debug.Log($"📍 현재 경도 (Longitude): {anchor.transform.position.z}");
            Debug.Log($"📍 현재 고도 (Altitude): {anchor.transform.position.y}");
        }
        else
        {
            Debug.LogWarning("⚠ ARGeospatialAnchor를 찾을 수 없습니다!");
        }
    }
}
