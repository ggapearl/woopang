/**
 * P2PUserInfo.cs
 * P2P 사용자 아바타 - 터치(더블탭) → 프로필 열기, 프로필 사진 텍스처 적용
 * 0000_Cube.prefab의 DoubleTap3D 패턴을 참고하여 단순화
 */

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityEngine.Networking;
using System.Collections;

public class P2PUserInfo : MonoBehaviour
{
    // ============================================================
    // User Data (Initialize()로 설정됨)
    // ============================================================
    [HideInInspector] public string userId;
    [HideInInspector] public string username;
    [HideInInspector] public string avatarUrl;
    [HideInInspector] public string bio;
    [HideInInspector] public float distance;

    // ============================================================
    // 3D Avatar Renderer
    // ============================================================
    [Header("3D Avatar Renderer")]
    [Tooltip("큐브의 MeshRenderer (프리팹에서 연결)")]
    [SerializeField] private MeshRenderer avatarRenderer;

    // ============================================================
    // OffScreen Indicator
    // ============================================================
    [Header("OffScreen Indicator")]
    [Tooltip("오프스크린 인디케이터 사용 여부")]
    public bool enableOffScreenIndicator = true;
    [Tooltip("인디케이터 색상 - P2P 사용자 색상 #E95383")]
    public Color indicatorColor = new Color(0.914f, 0.325f, 0.514f, 1f);

    // ============================================================
    // Touch Settings
    // ============================================================
    [Header("Touch Settings")]
    public bool enableTouch = true;
    public float doubleTapThreshold = 0.4f;

    // ============================================================
    // Avatar Colors
    // ============================================================
    [Header("Avatar Colors")]
    [SerializeField] private Color defaultAvatarColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color followingAvatarColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.2f, 1f);

    // ============================================================
    // Private
    // ============================================================
    private Target targetComponent;
    private Camera mainCamera;
    private Collider touchCollider;
    private bool isInitialized = false;
    private Color originalAvatarColor;
    private bool isFollowing = false;
    private Coroutine downloadCoroutine;

    // 더블탭 감지
    private float lastTapTime = 0f;
    private bool waitingForSecondTap = false;

    void Awake()
    {
        mainCamera = Camera.main;

        // avatarRenderer 자동 연결
        if (avatarRenderer == null)
        {
            avatarRenderer = GetComponent<MeshRenderer>();
        }

        // 원래 아바타 색상 저장 및 적용
        originalAvatarColor = defaultAvatarColor;
        if (avatarRenderer != null)
        {
            avatarRenderer.material.color = originalAvatarColor;
        }

        // 터치 콜라이더 설정
        SetupTouchCollider();

        // 오프스크린 인디케이터 설정
        SetupOffScreenIndicator();
    }

    // ============================================================
    // Initialize — P2PManager에서 호출
    // ============================================================
    public void Initialize(string uid, string uname, string avatar, string userBio, float dist)
    {
        userId = uid;
        username = uname;
        avatarUrl = avatar;
        bio = userBio;
        distance = dist;
        isInitialized = true;

        // 오프스크린 인디케이터 업데이트
        if (targetComponent == null)
        {
            SetupOffScreenIndicator();
        }
        if (targetComponent != null)
        {
            targetComponent.PlaceName = username;
            targetComponent.placeId = userId;
            targetComponent.TargetColor = indicatorColor;
            targetComponent.OnIndicatorTapped = () =>
            {
                if (!string.IsNullOrEmpty(userId) && ProfileManager.Instance != null)
                {
                    ProfileManager.Instance.ShowProfile(userId);
                }
            };
        }

        // 텍스처 로딩은 OnEnable에서 처리 (오브젝트가 활성화될 때)
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();

        if (isInitialized)
        {
            LoadProfileTexture();
        }
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();

        if (downloadCoroutine != null)
        {
            StopCoroutine(downloadCoroutine);
            downloadCoroutine = null;
        }
    }

    // ============================================================
    // Profile Texture Loading
    // ============================================================

    /// <summary>
    /// 프로필 사진 로딩 → 큐브 MeshRenderer에 텍스처 적용
    /// avatarUrl이 비어있으면 서버에서 userId로 프로필 조회 후 avatar_url을 가져옴
    /// </summary>
    private void LoadProfileTexture()
    {
        if (avatarRenderer == null) return;
        if (!gameObject.activeInHierarchy) return;

        if (downloadCoroutine != null) StopCoroutine(downloadCoroutine);

        if (!string.IsNullOrEmpty(avatarUrl))
        {
            // avatar_url이 있으면 바로 다운로드
            downloadCoroutine = StartCoroutine(DownloadTexture(avatarUrl));
        }
        else if (!string.IsNullOrEmpty(userId))
        {
            // avatar_url이 없으면 서버에서 프로필 조회 후 다운로드
            downloadCoroutine = StartCoroutine(FetchAvatarUrlAndDownload());
        }
    }

    /// <summary>
    /// 서버에서 userId로 프로필 조회 → avatar_url을 가져와 텍스처 다운로드
    /// </summary>
    private IEnumerator FetchAvatarUrlAndDownload()
    {
        string profileUrl = $"{ApiConfig.USER_PROFILE}?user_id={userId}";

        using (UnityWebRequest request = UnityWebRequest.Get(profileUrl))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string fetchedUrl = null;
                try
                {
                    var json = JsonUtility.FromJson<ProfileResponse>(request.downloadHandler.text);
                    if (!string.IsNullOrEmpty(json.avatar_url))
                    {
                        fetchedUrl = json.avatar_url;
                        if (fetchedUrl.StartsWith("/"))
                        {
                            fetchedUrl = ApiConfig.MAIN_SERVER + fetchedUrl;
                        }
                    }
                }
                catch (System.Exception) { }

                if (!string.IsNullOrEmpty(fetchedUrl))
                {
                    avatarUrl = fetchedUrl;
                    yield return StartCoroutine(DownloadTexture(avatarUrl));
                }
            }
        }

        downloadCoroutine = null;
    }

    [System.Serializable]
    private class ProfileResponse
    {
        public string avatar_url;
    }

    private IEnumerator DownloadTexture(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture != null)
                {
                    ApplyProfileTextureToAvatar(texture);
                }
            }
        }

        downloadCoroutine = null;
    }

    /// <summary>
    /// 프로필 사진을 큐브 MeshRenderer에 적용
    /// </summary>
    private void ApplyProfileTextureToAvatar(Texture2D texture)
    {
        if (texture == null || avatarRenderer == null) return;

        Material mat = avatarRenderer.material;

        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", texture);
            mat.color = Color.white;
        }
        else if (mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", texture);
            mat.color = Color.white;
        }
        else
        {
            Material unlitMat = new Material(Shader.Find("Unlit/Texture"));
            if (unlitMat.shader == null || unlitMat.shader.name == "Hidden/InternalErrorShader")
            {
                unlitMat = new Material(Shader.Find("Standard"));
            }
            unlitMat.mainTexture = texture;
            unlitMat.color = Color.white;
            avatarRenderer.material = unlitMat;
        }
    }

    // ============================================================
    // Touch Input
    // ============================================================
    void Update()
    {
        if (!isInitialized) return;

        if (mainCamera == null) mainCamera = Camera.main;

        if (enableTouch)
        {
            HandleTouchInput();
        }
    }

    private void HandleTouchInput()
    {
        // EnhancedTouch 패턴 (DoubleTap3D와 동일)
        if (Touch.activeTouches.Count != 1) return;

        var touch = Touch.activeTouches[0];
        if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) return;

        Vector2 touchPosition = touch.screenPosition;

        // UI 터치 무시
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(touchPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f))
        {
            if (hit.collider.gameObject == gameObject || hit.transform.IsChildOf(transform))
            {
                OnUserTapped();
            }
        }
    }

    /// <summary>
    /// 싱글탭 → 더블탭 확인
    /// </summary>
    private void OnUserTapped()
    {
        float currentTime = Time.time;

        if (waitingForSecondTap && (currentTime - lastTapTime) <= doubleTapThreshold)
        {
            waitingForSecondTap = false;
            OnUserDoubleTapped();
        }
        else
        {
            waitingForSecondTap = true;
            lastTapTime = currentTime;
        }
    }

    /// <summary>
    /// 더블탭 → ProfileManager로 프로필 열기
    /// </summary>
    public void OnUserDoubleTapped()
    {
        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.ShowProfile(userId);
        }
    }

    /// <summary>
    /// 외부에서 강제로 프로필 열기
    /// </summary>
    public void OnUserTouched()
    {
        OnUserDoubleTapped();
    }

    // ============================================================
    // OffScreen Indicator
    // ============================================================

    private void SetupOffScreenIndicator()
    {
        if (!enableOffScreenIndicator) return;

        targetComponent = GetComponent<Target>();
        if (targetComponent == null)
        {
            targetComponent = gameObject.AddComponent<Target>();
        }

        indicatorColor = new Color(0.914f, 0.325f, 0.514f, 1f); // #E95383
        targetComponent.TargetColor = indicatorColor;
        targetComponent.PlaceName = username;
        targetComponent.placeId = userId;

        targetComponent.NeedBoxIndicator = true;
        targetComponent.NeedArrowIndicator = true;
        targetComponent.NeedDistanceText = true;

        targetComponent.OnIndicatorTapped = () =>
        {
            if (!string.IsNullOrEmpty(userId) && ProfileManager.Instance != null)
            {
                ProfileManager.Instance.ShowProfile(userId);
            }
        };
    }

    // ============================================================
    // Touch Collider
    // ============================================================

    private void SetupTouchCollider()
    {
        if (!enableTouch) return;

        touchCollider = GetComponent<Collider>();
        if (touchCollider == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.size = Vector3.one;
            box.center = Vector3.zero;
            box.isTrigger = false;
            touchCollider = box;
        }

        if (touchCollider is BoxCollider boxCol)
        {
            boxCol.isTrigger = false;
        }

        gameObject.layer = LayerMask.NameToLayer("P2PUser") != -1
            ? LayerMask.NameToLayer("P2PUser")
            : LayerMask.NameToLayer("Default");
    }

    // ============================================================
    // Visual State
    // ============================================================

    public void SetHighlight(bool highlighted)
    {
        if (avatarRenderer != null)
        {
            if (highlighted)
            {
                avatarRenderer.material.color = highlightColor;
            }
            else
            {
                avatarRenderer.material.color = originalAvatarColor;
            }
        }
    }

    public void SetFollowing(bool following)
    {
        isFollowing = following;
        originalAvatarColor = following ? followingAvatarColor : defaultAvatarColor;

        if (avatarRenderer != null)
        {
            avatarRenderer.material.color = originalAvatarColor;
        }
    }

    /// <summary>
    /// P2P 사용자 완전히 숨기기 (필터가 None일 때)
    /// </summary>
    public void HideCompletely()
    {
        if (avatarRenderer != null)
            avatarRenderer.enabled = false;

        if (targetComponent != null)
            targetComponent.enabled = false;

        if (touchCollider != null)
            touchCollider.enabled = false;
    }

    /// <summary>
    /// P2P 사용자 다시 표시 (필터 해제 시)
    /// </summary>
    public void ShowAgain()
    {
        if (targetComponent != null)
            targetComponent.enabled = true;

        if (touchCollider != null)
            touchCollider.enabled = true;

        if (avatarRenderer != null)
            avatarRenderer.enabled = true;
    }

    // ============================================================
    // Distance Update
    // ============================================================

    public void UpdateDistance(float newDistance)
    {
        distance = newDistance;
    }

    // ============================================================
    // Getters
    // ============================================================

    public string GetUserId()
    {
        return userId;
    }

    public (string userId, string username, string bio, float distance) GetUserInfo()
    {
        return (userId, username, bio, distance);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private IEnumerator ResetHighlightAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetHighlight(false);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, 500f);
    }
#endif
}
