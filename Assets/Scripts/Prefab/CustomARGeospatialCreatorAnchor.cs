using Google.XR.ARCoreExtensions.GeospatialCreator;
using UnityEngine;

public class CustomARGeospatialCreatorAnchor : ARGeospatialCreatorAnchor
{
    // 좌표 설정 및 앵커 생성 메서드 추가
    public void SetCoordinatesAndCreateAnchor(double latitude, double longitude, double altitude)
    {
        // 비보호된 내부 필드에 접근하기 위해 리플렉션 사용
        typeof(ARGeospatialCreatorAnchor)
            .GetField("_latitude", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(this, latitude);
        typeof(ARGeospatialCreatorAnchor)
            .GetField("_longitude", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(this, longitude);
        typeof(ARGeospatialCreatorAnchor)
            .GetField("_altitude", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(this, altitude);

        if (Application.isPlaying)
        {
            // 비보호된 내부 필드에 접근하기 위해 리플렉션 사용
            var anchorResolutionField = typeof(ARGeospatialCreatorAnchor)
                .GetField("_anchorResolution", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var anchorResolution = (AnchorResolutionState)anchorResolutionField.GetValue(this);

            if (anchorResolution == AnchorResolutionState.NotStarted)
            {
                // 비보호된 메서드 호출
                typeof(ARGeospatialCreatorAnchor)
                    .GetMethod("AddGeoAnchorAtRuntime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(this, null);
            }
        }
    }

    // AnchorResolutionState 열거형 정의 (원본 클래스에서 비공개로 정의되어 있으므로 재정의)
    private enum AnchorResolutionState
    {
        NotStarted,
        InProgress,
        Complete
    }
}