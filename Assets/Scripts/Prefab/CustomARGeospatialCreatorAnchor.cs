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

    // 외부에서 렌더러를 강제로 숨기는 플래그 (fallback 모드 등)
    // true인 동안 앵커 생성 성공해도 렌더러를 켜지 않음
    private bool _forceHideRenderers = false;

    // ShowAfterFrame 세대 번호 — 앵커 재생성 시 이전 코루틴 무효화
    private int _showGeneration = 0;

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
        // 이전 ShowAfterFrame 코루틴 무효화
        _showGeneration++;

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

        // 이전 ShowAfterFrame 코루틴 무효화
        _showGeneration++;

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
        // forceHide 해제 — RecreateAnchor 성공 시 ShowAfterFrame에서 개별 복원
        _forceHideRenderers = false;
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

            // 앵커 생성 성공 → 10프레임 대기 후 렌더러 표시
            // (앵커 위치가 월드 좌표에 안정적으로 반영되기까지 여유 필요 — 즉시 표시 시 원점 플래시 발생)
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
    /// 앵커 위치가 월드 좌표에 반영된 후 렌더러 표시 (10프레임 대기)
    /// </summary>
    private IEnumerator ShowAfterFrame()
    {
        int myGeneration = _showGeneration;

        // 10프레임 대기 — 앵커 Transform이 GPS 좌표로 안정적으로 반영될 때까지 여유 확보
        for (int i = 0; i < 10; i++)
        {
            yield return null;
            // 대기 중 앵커가 재생성되었으면 이 코루틴은 무효
            if (myGeneration != _showGeneration) yield break;
        }

        if (_anchorCreated && !_forceHideRenderers)
        {
            SetVisible(true);
        }
    }

    /// <summary>
    /// 외부에서 렌더러 강제 숨김/해제 (fallback 모드, 백그라운드 복구 시 사용)
    /// forceHide=true: 렌더러 즉시 끄고, 앵커 생성 성공해도 켜지지 않음
    /// forceHide=false: 앵커가 이미 생성된 경우 10프레임 대기 후 렌더러 복원
    ///                  (앵커 Transform이 GPS 좌표로 안정적으로 반영되기까지 여유 필요)
    /// </summary>
    public void SetForceHideRenderers(bool forceHide)
    {
        _forceHideRenderers = forceHide;
        if (forceHide)
        {
            SetVisible(false);
        }
        else if (_anchorCreated)
        {
            // 즉시 표시하지 않고 1프레임 대기 후 표시 (원점 플래시 방지)
            StartCoroutine(ShowAfterFrame());
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
