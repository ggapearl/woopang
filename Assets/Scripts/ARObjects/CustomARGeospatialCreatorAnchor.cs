using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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

    // ── 나침반 모드 (R0926CompassMode, 11-06) — 어둡거나 하늘을 비춰 위치 추적이 멈춘 동안은 자리를 나침반 모드가 잡는다 ──
    /// <summary>켜져 있는 동안 앵커 만들기·포기(10초)를 멈춘다 — 위치 추적이 돌아오면 이어서</summary>
    public static bool CompassHold;
    /// <summary>켜져 있는 좌표 오브젝트 전부 (매니저 5개 · 보물찾기 보물)</summary>
    public static readonly HashSet<CustomARGeospatialCreatorAnchor> All = new HashSet<CustomARGeospatialCreatorAnchor>();
    private bool _hasCoords;
    private bool _compassPlaced;
    public bool HasCoordinates => _hasCoords;
    public double Latitude => _lat;
    public double Longitude => _lon;
    public double Altitude => _alt;
    public bool CompassPlaced => _compassPlaced;
    /// <summary>AR 이 이미 잡아 둔 자리가 있다 — 나침반 모드는 이건 그 자리에 두고 카메라만 돌린다</summary>
    public bool HoldsARPose => _anchorCreated && _hasBeenTracking && !_compassPlaced;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { All.Clear(); CompassHold = false; }

    private void OnEnable()
    {
        All.Add(this);
        if (_compassPlaced && !CompassHold) CompassRelease();   // 나침반 모드 동안 꺼져 있다가 끝난 뒤 다시 켜진 것 — 나침반 자리를 버린다
    }
    private void OnDisable() { All.Remove(this); }

    public void SetCoordinatesAndCreateAnchor(double latitude, double longitude, double altitude)
    {
        _lat = latitude;
        _lon = longitude;
        _alt = altitude;
        _hasCoords = true;
        _compassPlaced = false;
#if UNITY_EDITOR
        EditorPlace();
#else
        // 풀에서 재사용된 오브젝트가 이전 fallback 모드의 _forceHideRenderers=true를 그대로
        // 들고 있으면, 앵커 생성 후에도 SetVisible(true)가 막혀 영영 안 보이는 고착 발생.
        // 새 좌표로 시작할 때 반드시 리셋.
        _forceHideRenderers = false;

        // 앵커 생성 전까지 렌더러 숨김
        SetVisible(false);

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

#if UNITY_EDITOR
    // 에디터에서는 앵커 생성 대신 대략적인 상대 위치 계산하여 배치
    private void EditorPlace()
    {
        double userLat = 36.6361;
        double userLon = 126.8280;

        double dLat = _lat - userLat;
        double dLon = _lon - userLon;

        double metersPerLat = 111319.9;
        double metersPerLon = 111319.9 * System.Math.Cos(userLat * (System.Math.PI / 180.0));

        float z = (float)(dLat * metersPerLat);
        float x = (float)(dLon * metersPerLon);

        transform.position = new Vector3(x, (float)_alt, z);
    }
    private Quaternion _editorRot;   // 나침반 모드가 돌려 놓기 전 회전 — 끝나면 되돌린다
#endif

    /// <summary>
    /// 백그라운드 복귀 시 기존 앵커를 해제하고 재생성
    /// </summary>
    public void RecreateAnchor()
    {
#if UNITY_EDITOR
        return;
#else
        // 나침반 모드 동안은 앵커를 만들 수 없다 — 매니저의 2초 재시도가 나침반 자리를 숨겼다 켰다 하지 않게
        if (CompassHold) return;
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
        _compassPlaced = false;
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
        if (CompassHold) return false;   // 나침반 모드 — 위치 추적이 돌아오면 재시도 코루틴이 이어서 만든다

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
            if (CompassHold) continue;   // 나침반 모드 동안은 세지 않는다 (2분 한도가 어둠 속에서 다 닳지 않게)
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
    private static OffScreenIndicator _cachedOSI;
    private static int _osiCacheFrame = -1;
    private static OffScreenIndicator GetOSI()
    {
        if (_osiCacheFrame != Time.frameCount)
        {
            _cachedOSI = FindFirstObjectByType<OffScreenIndicator>();
            _osiCacheFrame = Time.frameCount;
        }
        return _cachedOSI;
    }

    private void Update()
    {
        if (CompassHold) return;   // 나침반 모드 — 얼려 둔 자리·나침반 자리를 그대로 (10초 포기도 멈춘다)

        // ★ Fallback 워치독 — fallback이 아닌데 forceHide=true이면 누군가 SetForceHideRenderers(false)를
        //   호출하지 않고 fallback OFF만 한 것. 자동 복원하여 영구 숨김 방지.
        if (_forceHideRenderers && _anchorCreated && _hasBeenTracking)
        {
            var osi = GetOSI();
            if (osi != null && !osi.IsFallbackMode)
            {
                _forceHideRenderers = false;
                SetVisible(true);
            }
        }

        if (!_anchorCreated) return;
        if (currentAnchor == null)
        {
            // 앵커가 외부에서 파괴됨 → 재앵커링 유도
            _anchorCreated = false;
            _isFrozen = false;
            _limitedSince = -1f;
            SetVisible(false);
            return;
        }

        TrackingState state = currentAnchor.trackingState;

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
        else if ((_anchorCreated && _hasBeenTracking) || _compassPlaced)
        {
            // freeze 중이든 아니든 이미 world pos가 유효하므로 즉시 복원 (나침반 자리도)
            SetVisible(true);
        }
        // 그 외: anchor 미준비 — Update에서 Tracking 도달 시 자동으로 켜짐
    }

    /// <summary>
    /// 나침반 모드 시작 — AR 이 잡아 둔 것은 지금 자리에 얼려 둔다 (앵커에서 떼어 월드 위치 그대로)
    /// </summary>
    public void CompassFreeze()
    {
        if (!HoldsARPose || _isFrozen) return;
        if (reattachLerpCoroutine != null) { StopCoroutine(reattachLerpCoroutine); reattachLerpCoroutine = null; }
        transform.SetParent(null, true);
        _isFrozen = true;
        _limitedSince = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// 나침반 모드 — AR 자리가 없는 것을 GPS 로 잰 자리에 놓고 보인다 (fallback 이 숨겨 둔 것도)
    /// </summary>
    public void CompassPlace(Vector3 position, Quaternion rotation)
    {
        if (currentAnchor != null && transform.parent == currentAnchor.transform) transform.SetParent(null, true);
#if UNITY_EDITOR
        if (!_compassPlaced) _editorRot = transform.rotation;
#endif
        transform.SetPositionAndRotation(position, rotation);
        if (_compassPlaced) return;
        _compassPlaced = true;
        _forceHideRenderers = false;
        SetVisible(true);
    }

    /// <summary>
    /// 나침반 모드 끝 — 나침반이 놓은 것은 숨기고 앵커를 다시 만들게 둔다(재시도 코루틴 · 매니저 RetryFailedAnchors).
    /// 얼려 둔 것은 앵커가 다시 잡히면 Update 가 제자리로 붙인다 — 10초 버퍼는 지금부터.
    /// </summary>
    public void CompassRelease()
    {
        if (_compassPlaced)
        {
            _compassPlaced = false;
#if UNITY_EDITOR
            transform.rotation = _editorRot;
            EditorPlace();
#else
            SetVisible(false);
            _anchorCreated = false;
            _hasBeenTracking = false;
            _isFrozen = false;
            _limitedSince = -1f;
#endif
            return;
        }
        if (_isFrozen) _limitedSince = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// 이 오브젝트를 지우기 전에 — 붙어 있던 지오스페이셜 앵커도 함께 지운다 (Destroy 만 하면 앵커 오브젝트가 따로 남는다).
    /// 월드 위치는 그대로 두고 떼어 낸다. 보물찾기 보물(R0926HuntTreasure)이 치울 때 쓴다.
    /// </summary>
    public void ReleaseAnchor()
    {
        if (retryCoroutine != null) { StopCoroutine(retryCoroutine); retryCoroutine = null; }
        if (reattachLerpCoroutine != null) { StopCoroutine(reattachLerpCoroutine); reattachLerpCoroutine = null; }
        _anchorCreated = false;
        if (currentAnchor == null) return;
        if (transform.parent == currentAnchor.transform)
            transform.SetParent(currentAnchor.transform.parent, true);
        Destroy(currentAnchor.gameObject);
        currentAnchor = null;
    }

    /// <summary>
    /// 자신 + 자식의 모든 Renderer on/off
    /// </summary>
    private void SetVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.enabled = visible;
    }
}
