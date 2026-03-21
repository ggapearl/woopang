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
    public bool IsAnchorCreated => _anchorCreated;
    private int _retryCount = 0;
    private const int MAX_RETRIES = 120; // 최대 120회 (약 2분) - Earth 초기화 대기 충분
    private Coroutine retryCoroutine;

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

        transform.position = new Vector3(x, (float)altitude, z);
#else
        // 앵커 생성 전까지 렌더러 숨김 (Vector3.zero에 보이는 문제 방지)
        SetVisible(false);

        _lat = latitude;
        _lon = longitude;
        _alt = altitude;
        _anchorCreated = false;
        _retryCount = 0;

        // 기존 재시도 코루틴 중단
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        // 즉시 시도 후, 실패하면 코루틴으로 재시도
        if (!TryCreateAnchor())
        {
            retryCoroutine = StartCoroutine(RetryCreateAnchor());
        }
#endif
    }

    /// <summary>
    /// 백그라운드 복귀 시 기존 앵커를 해제하고 재생성
    /// 기존 좌표(_lat, _lon, _alt)를 그대로 사용하므로 서버 재요청 불필요
    /// </summary>
    public void RecreateAnchor()
    {
#if UNITY_EDITOR
        return;
#else
        // 재시도 코루틴 중단
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        // 기존 앵커 부모 해제 (앵커 자체는 ARCore가 관리)
        if (_anchorCreated && transform.parent != null)
        {
            Transform oldAnchorTransform = transform.parent;
            transform.SetParent(oldAnchorTransform.parent, true);

            // 기존 앵커 오브젝트 파괴 (ARCore에서 생성한 앵커)
            ARGeospatialAnchor oldAnchor = oldAnchorTransform.GetComponent<ARGeospatialAnchor>();
            if (oldAnchor != null)
                Destroy(oldAnchorTransform.gameObject);
        }

        // 렌더러 숨기고 재생성 시작
        SetVisible(false);
        _anchorCreated = false;
        _retryCount = 0;

        if (!TryCreateAnchor())
        {
            retryCoroutine = StartCoroutine(RetryCreateAnchor());
        }
#endif
    }

    private bool TryCreateAnchor()
    {
        if (anchorManager == null)
            anchorManager = FindFirstObjectByType<ARAnchorManager>();

        if (anchorManager == null)
        {
            Debug.LogError($"[{gameObject.name}] ARAnchorManager 없음");
            return false;
        }

        // EarthManager 상태 확인
        var earthManager = FindFirstObjectByType<AREarthManager>();
        if (earthManager == null || earthManager.EarthTrackingState != TrackingState.Tracking)
        {
            return false;
        }

        ARGeospatialAnchor anchor = anchorManager.AddAnchor(_lat, _lon, _alt, Quaternion.identity);

        if (anchor != null)
        {
            transform.SetParent(anchor.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _anchorCreated = true;

            // 앵커 생성 성공 → 1프레임 대기 후 렌더러 표시
            // (앵커 위치가 월드 좌표에 반영되기까지 1프레임 필요 — 즉시 표시 시 원점 플래시 발생)
            StartCoroutine(ShowAfterFrame());

            return true;
        }

        return false;
    }

    private IEnumerator RetryCreateAnchor()
    {
        while (!_anchorCreated && _retryCount < MAX_RETRIES)
        {
            yield return new WaitForSeconds(1f);
            _retryCount++;

            if (TryCreateAnchor())
            {
                retryCoroutine = null;
                yield break;
            }
        }

        retryCoroutine = null;

        if (!_anchorCreated)
        {
            // 앵커 실패 → 렌더러만 숨김 유지 (오브젝트는 살려둬서 다음 RecreateAnchor에서 재시도 가능)
            SetVisible(false);
        }
    }

    /// <summary>
    /// 앵커 위치가 월드 좌표에 반영된 후 렌더러 표시 (1프레임 대기)
    /// </summary>
    private IEnumerator ShowAfterFrame()
    {
        yield return null; // 1프레임 대기 — 앵커 Transform이 실제 GPS 위치로 업데이트됨
        if (_anchorCreated)
        {
            SetVisible(true);
        }
    }

    /// <summary>
    /// 자신 + 자식의 모든 Renderer on/off (앵커 생성 전후 가시성 제어)
    /// </summary>
    private void SetVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.enabled = visible;
    }
}
