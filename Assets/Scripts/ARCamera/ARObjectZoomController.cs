using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch; // New Input System 추가
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch; // New Input System Touch 클래스 사용
using System.Collections.Generic;

/// <summary>
/// AR 환경에서 작동하는 줌 컨트롤러
/// 카메라 FOV 대신 AR 오브젝트들의 스케일을 조절하여 줌 효과 구현
/// New Input System (EnhancedTouch) 적용됨
/// </summary>
public class ARObjectZoomController : MonoBehaviour
{
    [Header("Zoom Settings")]
    [SerializeField] private float defaultZoom = 1f; // 기본 줌 (1.0)
    [SerializeField] private float minZoom = 0.5f; // 최소 줌 (축소 - 오브젝트가 작아짐)
    [SerializeField] private float maxZoom = 3f; // 최대 줌 (확대 - 오브젝트가 커짐)
    [SerializeField] private float zoomSpeed = 0.01f; // 줌 속도

    [Header("1.0x 스냅")]
    [Tooltip("1.0x 에서 벗어나려면 손가락을 이만큼(dp) 더 벌리거나 좁혀야 한다 — 1.0x 로 쓰도록 유도")]
    [SerializeField] private float snapTravelDp = 30f;

    [Header("Zoom Indicator")]
    [SerializeField] private GameObject zoomIndicatorObject; // 줌 인디케이터 GameObject
    private ZoomIndicator zoomIndicator; // 줌 인디케이터 컴포넌트

    [Header("AR Object Managers")]
    [SerializeField] private DataManager dataManager; // 우팡 데이터 매니저
    [SerializeField] private TourAPIManager tourAPIManager; // 공공데이터 매니저
    [SerializeField] private SubwayManager subwayManager; // 지하철
    [SerializeField] private TerminalManager terminalManager; // 버스 터미널
    [SerializeField] private TrainStationManager trainStationManager; // 기차역

    private float currentZoom = 1f;
    private static ARObjectZoomController current;   // Fit — 새로 뜨는 오브젝트도 지금 배율로
    private float previousTouchDistance = 0f;
    private bool isPinching = false;
    private float rawZoom = 1f;   // 손가락이 실제로 간 만큼 — 1.0x 에 붙는 칸까지 포함
    private bool snapArmed;       // 1.0x 에서 충분히 떨어졌다 — 다시 붙을 때 진동 (칸 경계에서 손이 떨려 거듭 울리지 않게)

    private const float SnapZoom = 1f;
    private const float SnapNear = 0.05f;   // 이보다 가까우면 표시가 '1.0x'

#if UNITY_EDITOR
    public static int EditorSnaps;   // 플레이 시험이 센다 (에디터엔 진동이 없다)
#endif

    void OnEnable()
    {
        EnhancedTouchSupport.Enable(); // EnhancedTouch 활성화
        current = this;
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable(); // EnhancedTouch 비활성화
    }

    void Start()
    {
        // DataManager Singleton 사용
        if (dataManager == null)
        {
            dataManager = DataManager.Instance;
            if (dataManager == null)
            {
                Debug.LogWarning("[ARObjectZoomController] DataManager를 찾을 수 없습니다.");
            }
        }

        // TourAPIManager Singleton 사용
        if (tourAPIManager == null)
        {
            tourAPIManager = TourAPIManager.Instance;
            if (tourAPIManager == null)
            {
                Debug.LogWarning("[ARObjectZoomController] TourAPIManager를 찾을 수 없습니다.");
            }
        }

        // 공공교통 매니저들도 Singleton에서 자동 fallback
        if (subwayManager == null) subwayManager = SubwayManager.Instance;
        if (terminalManager == null) terminalManager = TerminalManager.Instance;
        if (trainStationManager == null) trainStationManager = TrainStationManager.Instance;

        // ZoomIndicator 연결 로직 개선
        if (zoomIndicatorObject != null)
        {
            zoomIndicator = zoomIndicatorObject.GetComponent<ZoomIndicator>();
        }
        
        // Inspector에 할당되지 않았거나 컴포넌트를 못 찾은 경우 씬에서 탐색
        if (zoomIndicator == null)
        {
            zoomIndicator = FindFirstObjectByType<ZoomIndicator>(FindObjectsInactive.Include);
            if (zoomIndicator != null)
            {
                zoomIndicatorObject = zoomIndicator.gameObject;
            }
        }

        // 기본 줌 설정
        currentZoom = defaultZoom;
    }

    void Update()
    {
#if UNITY_EDITOR
        return; // 에디터에서는 핀치 줌 비활성화 (실기기 전용)
#endif

        // 터치 입력이 2개일 때 (핀치 제스처)
        if (Touch.activeTouches.Count == 2)
        {
            Touch touch0 = Touch.activeTouches[0];
            Touch touch1 = Touch.activeTouches[1];

            // 두 터치 사이의 거리 계산
            float currentTouchDistance = Vector2.Distance(touch0.screenPosition, touch1.screenPosition);

            // 핀치 시작
            if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Began || touch1.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                previousTouchDistance = currentTouchDistance;
                isPinching = true;
                SyncRaw();
            }
            // 핀치 진행 중
            else if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch1.phase == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                if (isPinching && previousTouchDistance > 0)
                {
                    // 거리 차이로 줌 계산 (거리가 멀어지면 확대, 가까워지면 축소)
                    PinchBy(currentTouchDistance - previousTouchDistance);
                    previousTouchDistance = currentTouchDistance;
                }
            }
        }
        // 핀치 종료
        else
        {
            if (isPinching)
            {
                isPinching = false;
                previousTouchDistance = 0f;
                EndPinch();

                // 줌 인디케이터 숨김 (5초 딜레이)
                if (zoomIndicator != null)
                {
                    zoomIndicator.HideAfterDelay(5f);
                }
            }
        }

    }

    // 손가락 사이가 distanceDelta(px)만큼 벌어지면(+) 확대, 좁혀지면(-) 축소 — 1.0x 근처에선 1.0x 에 붙는다
    // (10-26 대표님 「1.0x 가 되었을 때 핸드폰에서 진동이 살짝 울리면서 snap … 1.0x 로 사용하도록 유도」)
    private void PinchBy(float distanceDelta)
    {
        float band = SnapBand();
        rawZoom = Mathf.Clamp(rawZoom + distanceDelta * zoomSpeed, minZoom - band, maxZoom + band);
        float zoom = rawZoom > SnapZoom + band ? rawZoom - band : rawZoom < SnapZoom - band ? rawZoom + band : SnapZoom;
        if (zoom == SnapZoom && currentZoom != SnapZoom && snapArmed) { SnapBuzz(); snapArmed = false; }
        else if (Mathf.Abs(zoom - SnapZoom) >= SnapNear) snapArmed = true;
        currentZoom = zoom;
        ApplyZoomToARObjects();
        if (zoomIndicator != null) zoomIndicator.UpdateZoom(currentZoom);
    }

    // 지금 배율에서 이어서 — 1.0x 에서 시작하면 붙는 칸 한가운데부터 (매번 같은 만큼 움직여야 벗어난다)
    private void SyncRaw()
    {
        float band = SnapBand();
        rawZoom = currentZoom > SnapZoom ? currentZoom + band : currentZoom < SnapZoom ? currentZoom - band : SnapZoom;
        snapArmed = Mathf.Abs(currentZoom - SnapZoom) >= SnapNear;
    }

    // 손을 뗐을 때 '1.0x' 로 보이는 배율은 정확히 1.0x 로 — 멀리서 다가온 거면 진동도
    private void EndPinch()
    {
        if (currentZoom == SnapZoom || Mathf.Abs(currentZoom - SnapZoom) >= SnapNear) return;
        if (snapArmed) SnapBuzz();
        currentZoom = SnapZoom;
        SyncRaw();
        ApplyZoomToARObjects();
        if (zoomIndicator != null) zoomIndicator.UpdateZoom(currentZoom);
    }

    // 1.0x 에 붙는 칸 (배율 단위) — 화면 밀도가 달라도 손끝에선 같은 거리
    private float SnapBand() => snapTravelDp * (Screen.dpi > 0 ? Screen.dpi : 400f) / 160f * zoomSpeed;

    private static void SnapBuzz()
    {
#if UNITY_EDITOR
        EditorSnaps++;
#else
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
#endif
    }

    /// <summary>
    /// AR 오브젝트들의 스케일을 조절하여 줌 효과 적용 — 맨 위 크기 = 본 크기 × 배율
    /// (11-15: 예전엔 1 × 배율로 덮어, 본 크기가 2 인 사진 상자가 핀치를 조금만 해도 절반이 됐다)
    /// </summary>
    private void ApplyZoomToARObjects()
    {
        if (dataManager != null) ApplyZoomToManager(dataManager.GetSpawnedObjects());   // 우팡 데이터
        if (tourAPIManager != null) ApplyZoomToManager(tourAPIManager.GetSpawnedObjects());   // 공공데이터

        // 공공교통 (지하철·기차역·터미널) — 이전엔 빠져있어 zoom 안 됐음
        ApplyZoomToManager(subwayManager?.GetSpawnedObjects());
        ApplyZoomToManager(trainStationManager?.GetSpawnedObjects());
        ApplyZoomToManager(terminalManager?.GetSpawnedObjects());
        ApplyZoomToManager(R0926HuntTreasure.SpawnedObjects);   // 보물찾기 AR 보물
        if (P2PManager.Instance != null) P2PManager.Instance.ApplyZoom(currentZoom);   // 사람(P2P) — 예전엔 빠져 있어 확대·축소가 안 됐다
    }

    // 꺼 둔 오브젝트(필터·거리·백그라운드)도 맞춰 둔다 — 다시 켜질 때 지난 배율로 나오지 않게
    private void ApplyZoomToManager<TKey>(Dictionary<TKey, GameObject> spawned)
    {
        if (spawned == null) return;
        foreach (var kvp in spawned) Fit(kvp.Value, currentZoom);
    }

    /// <summary>
    /// 맨 위 크기를 본 크기(처음 크기 — 프리팹 맨 위가 2 면 2) × 지금 배율로. 새로 띄우거나 풀에서 다시 꺼낸 오브젝트에 매니저가 부른다
    /// (사람(P2P)은 P2PManager 가 본 크기 × 배율로 따로 맞춘다)
    /// </summary>
    public static void Fit(GameObject obj)
    {
        if (current == null) current = FindFirstObjectByType<ARObjectZoomController>();
        Fit(obj, current != null ? current.currentZoom : 1f);
    }

    private static void Fit(GameObject obj, float zoom)
    {
        if (obj == null) return;
        if (!obj.TryGetComponent(out ARZoomBase b)) b = obj.AddComponent<ARZoomBase>();
        obj.transform.localScale = b.Scale * zoom;
    }

    /// <summary>
    /// 현재 줌 레벨 반환 (1.0 = 기본, 2.0 = 2배 확대)
    /// </summary>
    public float GetZoomLevel()
    {
        return currentZoom;
    }

    /// <summary>
    /// 줌을 기본값으로 리셋
    /// </summary>
    public void ResetZoom()
    {
        currentZoom = defaultZoom;
        SyncRaw();
        ApplyZoomToARObjects();

        if (zoomIndicator != null)
        {
            zoomIndicator.UpdateZoom(1.0f);
            zoomIndicator.HideAfterDelay(1f);
        }

    }
}
