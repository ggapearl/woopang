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
        }
        else
        {
            Debug.LogWarning("⚠ ARGeospatialAnchor를 찾을 수 없습니다!");
        }
    }
}
