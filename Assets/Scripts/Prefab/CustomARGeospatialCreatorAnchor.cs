using UnityEngine;
using System.Collections;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class CustomARGeospatialCreatorAnchor : MonoBehaviour
{
    private ARAnchorManager anchorManager;
    private ARGeospatialAnchor currentAnchor;
    private double _lat, _lon, _alt;
    private bool _anchorCreated = false;
    public bool IsAnchorCreated => _anchorCreated;
    public bool IsRetrying => retryCoroutine != null;
    private int _retryCount = 0;
    private const int MAX_RETRIES = 120; // 최대 120회 (약 2분) — Earth 초기화 대기 충분
    private Coroutine retryCoroutine;

    // 외부에서 렌더러를 강제로 숨기는 플래그 (fallback 모드 등)
    private bool _forceHideRenderers = false;

    // trackingState 기반 가시성 관리
    private bool _hasBeenTracking = false;           // 최초 Tracking 도달 여부 (최초 표시 판정)
    private bool _isFrozen = false;                  // Limited 중 world pos 고정 상태인지
    private float _limitedSince = -1f;               // Limited 진입 시각 (10s 버퍼 판정용)
    private const float LIMITED_BUFFER = 10f;        // 10s 이내 Tracking 복귀 시 freeze 유지
    private const float REATTACH_LERP_DURATION = 0.3f; // 재부착 후 부드러운 보정 시간
    private Coroutine reattachLerpCoroutine;

    // 진단용 — Update 로그 throttle (모든 프레임 로그하면 폭주)
    private float _lastDiagLogTime = -10f;
    private TrackingState _lastLoggedState = TrackingState.None;

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
        // 풀에서 재사용된 오브젝트가 이전 fallback 모드의 _forceHideRenderers=true를 그대로
        // 들고 있으면, 앵커 생성 후에도 SetVisible(true)가 막혀 영영 안 보이는 고착 발생.
        // 새 좌표로 시작할 때 반드시 리셋.
        _forceHideRenderers = false;

        // 앵커 생성 전까지 렌더러 숨김
        SetVisible(false);

        _lat = latitude;
        _lon = longitude;
        _alt = altitude;
        _anchorCreated = false;
        _hasBeenTracking = false;
        _isFrozen = false;
        _limitedSince = -1f;
        _retryCount = 0;

        if (retryCoroutine != null) { StopCoroutine(retryCoroutine); retryCoroutine = null; }
        if (reattachLerpCoroutine != null) { StopCoroutine(reattachLerpCoroutine); reattachLerpCoroutine = null; }

        // 풀에서 재사용될 때 이전 앵커가 남아있으면 정리
        if (currentAnchor != null)
        {
            if (transform.parent == currentAnchor.transform)
                transform.SetParent(currentAnchor.transform.parent, true);
            Destroy(currentAnchor.gameObject);
            currentAnchor = null;
        }

        if (!TryCreateAnchor())
        {
            retryCoroutine = StartCoroutine(RetryCreateAnchor());
        }
#endif
    }

    /// <summary>
    /// 백그라운드 복귀 시 기존 앵커를 해제하고 재생성
    /// </summary>
    public void RecreateAnchor()
    {
#if UNITY_EDITOR
        return;
#else
        Debug.Log($"[BG-iOS-DBG] {gameObject.name} RecreateAnchor 호출 (anchorCreated={_anchorCreated}, forceHide={_forceHideRenderers}, hasBeenTracking={_hasBeenTracking})");
        if (retryCoroutine != null) { StopCoroutine(retryCoroutine); retryCoroutine = null; }
        if (reattachLerpCoroutine != null) { StopCoroutine(reattachLerpCoroutine); reattachLerpCoroutine = null; }

        // 기존 앵커 정리 — freeze로 detach 됐을 수도 있으므로 currentAnchor 기준으로 파괴
        if (currentAnchor != null)
        {
            if (transform.parent == currentAnchor.transform)
                transform.SetParent(currentAnchor.transform.parent, true);
            Destroy(currentAnchor.gameObject);
            currentAnchor = null;
        }

        _forceHideRenderers = false;
        SetVisible(false);
        _anchorCreated = false;
        _hasBeenTracking = false;
        _isFrozen = false;
        _limitedSince = -1f;
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

        var earthManager = FindFirstObjectByType<AREarthManager>();
        if (earthManager == null || earthManager.EarthTrackingState != TrackingState.Tracking)
        {
            return false;
        }

        ARGeospatialAnchor anchor = anchorManager.AddAnchor(_lat, _lon, _alt, Quaternion.identity);

        if (anchor != null)
        {
            currentAnchor = anchor;
            transform.SetParent(anchor.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _anchorCreated = true;
            Debug.Log($"[BG-iOS-DBG] {gameObject.name} AddAnchor 성공 retry={_retryCount}");
            // 렌더러 표시는 Update()가 anchor.trackingState == Tracking 시점에 처리
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
            SetVisible(false);
        }
    }

    /// <summary>
    /// 앵커의 trackingState를 매 프레임 감시하여 가시성·freeze·10s timeout 관리
    /// Tracking → 정상 표시 (앵커 자식)
    /// Limited/None (10s 이내) → 마지막 world pos로 freeze (detach, 계속 표시)
    /// Limited/None (10s 초과) → 앵커 포기, _anchorCreated=false로 재앵커링 유도
    /// </summary>
    private void Update()
    {
        if (!_anchorCreated) return;
        if (currentAnchor == null)
        {
#if !UNITY_EDITOR
            Debug.LogWarning($"[BG-iOS-DBG] {gameObject.name} currentAnchor null → 재앵커링 유도, SetVisible(false)");
#endif
            // 앵커가 외부에서 파괴됨 → 재앵커링 유도
            _anchorCreated = false;
            _isFrozen = false;
            _limitedSince = -1f;
            SetVisible(false);
            return;
        }

        TrackingState state = currentAnchor.trackingState;

#if !UNITY_EDITOR
        // 트래킹 상태 변경 시 로그 (state 전환 시 1회)
        if (state != _lastLoggedState)
        {
            Debug.Log($"[BG-iOS-DBG] {gameObject.name} TrackingState 변경 {_lastLoggedState}→{state} (frozen={_isFrozen}, hasBeenTracking={_hasBeenTracking}, forceHide={_forceHideRenderers})");
            _lastLoggedState = state;
        }
#endif

        if (state == TrackingState.Tracking)
        {
            // Limited에서 복귀 — freeze 해제 + 앵커에 재부착
            if (_isFrozen)
            {
                _isFrozen = false;
                _limitedSince = -1f;

                // world position 유지하며 재부착 (순간이동 방지)
                transform.SetParent(currentAnchor.transform, true);

                // 로컬 좌표가 (0,0,0)이 아닐 수 있음 → 0.3s Lerp로 부드럽게 보정
                if (reattachLerpCoroutine != null) StopCoroutine(reattachLerpCoroutine);
                reattachLerpCoroutine = StartCoroutine(LerpToAnchorOrigin());
            }

            // 최초 Tracking 도달 시 렌더러 표시
            if (!_hasBeenTracking)
            {
                _hasBeenTracking = true;
                Debug.Log($"[BG-iOS-DBG] {gameObject.name} 최초 Tracking 도달 (forceHide={_forceHideRenderers}) → SetVisible({!_forceHideRenderers})");
                if (!_forceHideRenderers) SetVisible(true);
            }
        }
        else // Limited or None
        {
            // Tracking → Limited 전환: 현재 world pos 스냅샷 + 앵커에서 분리
            // (앵커 transform이 원점으로 drift해도 이 오브젝트는 제자리 유지)
            if (!_isFrozen && _hasBeenTracking)
            {
                _isFrozen = true;
                _limitedSince = Time.realtimeSinceStartup;

                if (reattachLerpCoroutine != null)
                {
                    StopCoroutine(reattachLerpCoroutine);
                    reattachLerpCoroutine = null;
                }

                // worldPositionStays=true → 현재 world 좌표 그대로 유지
                transform.SetParent(null, true);
            }

            // Limited 10s 초과 → 앵커 포기, RetryFailedAnchors(FilterManager 2s tick)가 재생성
            if (_isFrozen && _limitedSince > 0f &&
                Time.realtimeSinceStartup - _limitedSince > LIMITED_BUFFER)
            {
#if !UNITY_EDITOR
                Debug.LogWarning($"[BG-iOS-DBG] {gameObject.name} Limited 10s 초과 → 앵커 포기 + SetVisible(false)");
#endif
                _anchorCreated = false;
                _isFrozen = false;
                _limitedSince = -1f;
                SetVisible(false);
            }
        }
    }

    /// <summary>
    /// 재부착 후 localPosition을 0으로 Lerp — AR 권위 좌표로 부드럽게 수렴
    /// </summary>
    private IEnumerator LerpToAnchorOrigin()
    {
        Vector3 startLocal = transform.localPosition;
        Quaternion startLocalRot = transform.localRotation;
        float t = 0f;

        while (t < REATTACH_LERP_DURATION)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / REATTACH_LERP_DURATION);
            transform.localPosition = Vector3.Lerp(startLocal, Vector3.zero, k);
            transform.localRotation = Quaternion.Slerp(startLocalRot, Quaternion.identity, k);
            yield return null;
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        reattachLerpCoroutine = null;
    }

    /// <summary>
    /// 외부에서 렌더러 강제 숨김/해제 (fallback 모드, 백그라운드 복구 시 사용)
    /// </summary>
    public void SetForceHideRenderers(bool forceHide)
    {
        _forceHideRenderers = forceHide;
        if (forceHide)
        {
            SetVisible(false);
        }
        else if (_anchorCreated && _hasBeenTracking)
        {
            // freeze 중이든 아니든 이미 world pos가 유효하므로 즉시 복원
            Debug.Log($"[BG-iOS-DBG] {gameObject.name} SetForceHideRenderers(false) → SetVisible(true) (anchorCreated={_anchorCreated}, hasBeenTracking={_hasBeenTracking})");
            SetVisible(true);
        }
        else
        {
            // anchor가 아직 준비 안 됨 — Update에서 Tracking 도달 시 자동으로 켜짐
            Debug.LogWarning($"[BG-iOS-DBG] {gameObject.name} SetForceHideRenderers(false) 호출됐지만 anchor 미준비 (anchorCreated={_anchorCreated}, hasBeenTracking={_hasBeenTracking}) → SetVisible 보류");
        }
    }

    /// <summary>
    /// 자신 + 자식의 모든 Renderer on/off
    /// </summary>
    private void SetVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
#if !UNITY_EDITOR
        Debug.Log($"[BG-iOS-DBG] {gameObject.name} SetVisible({visible}) renderers={renderers.Length} active={gameObject.activeInHierarchy} pos={transform.position}");
#endif
        foreach (var r in renderers)
            r.enabled = visible;
    }

    void OnEnable()
    {
#if !UNITY_EDITOR
        Debug.Log($"[BG-iOS-DBG] {gameObject.name} OnEnable anchorCreated={_anchorCreated} hasBeenTracking={_hasBeenTracking} forceHide={_forceHideRenderers}");
#endif
    }

    void OnDisable()
    {
#if !UNITY_EDITOR
        Debug.Log($"[BG-iOS-DBG] {gameObject.name} OnDisable anchorCreated={_anchorCreated} hasBeenTracking={_hasBeenTracking}");
#endif
    }
}
