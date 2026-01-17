/**
 * P2PUserInfo.cs
 * Displays user information on P2P user avatars
 * Shows username, distance, avatar image, and status
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-01
 */

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class P2PUserInfo : MonoBehaviour
{
    [Header("User Data")]
    public string userId;
    public string username;
    public string avatarUrl;
    public string bio;
    public float distance; // Distance in meters

    [Header("UI References")]
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI distanceText;
    public Image avatarImage;
    public GameObject statusIndicator; // Green dot for online status
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public float maxVisibleDistance = 500f; // Fade out after this distance
    public bool alwaysShowUsername = true;
    public bool showDistance = true;

    [Header("3D Avatar Renderers")]
    [Tooltip("아바타 메시 렌더러 (P2P_Avatar 프리팹용)")]
    [SerializeField] private MeshRenderer avatarRenderer;
    [Tooltip("펄스 이펙트 렌더러 (P2P_Avatar 프리팹용)")]
    [SerializeField] private MeshRenderer pulseRenderer;

    [Header("Avatar Colors")]
    [SerializeField] private Color defaultAvatarColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color followingAvatarColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.2f, 1f);

    private Color originalAvatarColor;
    private bool isFollowing = false;

    [Header("Billboard Settings")]
    public bool enableBillboard = true; // Always face camera
    public Vector3 billboardOffset = new Vector3(0, 2.5f, 0); // Above avatar

    [Header("OffScreen Indicator")]
    [Tooltip("오프스크린 인디케이터 사용 여부")]
    public bool enableOffScreenIndicator = true;
    [Tooltip("인디케이터 색상 - P2P 사용자 색상 #E95383")]
    public Color indicatorColor = new Color(0.914f, 0.325f, 0.514f, 1f); // #E95383

    private Target targetComponent;

    [Header("Auto-Generated Avatar")]
    [Tooltip("프로필 사진 기반 큐브 아바타 자동 생성")]
    public bool autoCreateCubeAvatar = true;
    [Tooltip("큐브 아바타 크기")]
    public float cubeAvatarSize = 1.5f;
    [Tooltip("큐브 아바타 Y 오프셋")]
    public float cubeAvatarYOffset = 1f;

    private GameObject generatedCubeAvatar;
    private MeshRenderer cubeRenderer;
    private Material cubeMaterial;

    [Header("Touch Settings")]
    public bool enableTouch = true;
    public float touchRadius = 1.5f; // Collider radius for touch detection
    public float doubleTapThreshold = 0.4f; // 더블탭 인식 시간 (초)

    private Camera mainCamera;
    private Collider touchCollider;
    private Transform billboardTransform;
    private bool isInitialized = false;

    // 더블탭 감지
    private float lastTapTime = 0f;
    private bool waitingForSecondTap = false;

    void Awake()
    {
        mainCamera = Camera.main;

        // 원래 아바타 색상 저장
        originalAvatarColor = defaultAvatarColor;
        if (avatarRenderer != null)
        {
            avatarRenderer.material.color = originalAvatarColor;
        }

        // Create billboard parent for UI elements (UI가 있는 경우에만)
        if (usernameText != null || distanceText != null || avatarImage != null)
        {
            GameObject billboardObj = new GameObject("Billboard");
            billboardObj.transform.SetParent(transform);
            billboardObj.transform.localPosition = billboardOffset;
            billboardTransform = billboardObj.transform;

            // Move UI elements to billboard
            if (usernameText) usernameText.transform.SetParent(billboardTransform, false);
            if (distanceText) distanceText.transform.SetParent(billboardTransform, false);
            if (avatarImage) avatarImage.transform.SetParent(billboardTransform, false);
        }

        // Setup touch collider for AR touch detection
        SetupTouchCollider();

        // Setup offscreen indicator (색상 강제 적용)
        SetupOffScreenIndicator();

        // 큐브 아바타 자동 생성
        SetupCubeAvatar();

        // UI 자동 생성 (usernameText, distanceText)
        SetupAutoUI();
    }

    /// <summary>
    /// 오프스크린 인디케이터용 Target 컴포넌트 설정
    /// Target 색상을 #E95383 (핑크)으로 강제 설정
    /// </summary>
    private void SetupOffScreenIndicator()
    {
        if (!enableOffScreenIndicator) return;

        targetComponent = GetComponent<Target>();
        if (targetComponent == null)
        {
            targetComponent = gameObject.AddComponent<Target>();
        }

        // 인디케이터 색상을 #E95383 (핑크)으로 강제 설정
        // 프리팹에서 설정된 하늘색(0.3, 0.7, 1)을 덮어씀
        indicatorColor = new Color(0.914f, 0.325f, 0.514f, 1f); // #E95383 강제 적용
        targetComponent.TargetColor = indicatorColor;
        targetComponent.PlaceName = username;

        Debug.Log($"[P2PUserInfo] Target color set to #E95383 for user: {username}");
    }

    /// <summary>
    /// Setup collider for touch detection in AR
    /// </summary>
    private void SetupTouchCollider()
    {
        if (!enableTouch) return;

        // Add sphere collider if not exists
        touchCollider = GetComponent<Collider>();
        if (touchCollider == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = touchRadius;
            sphere.center = new Vector3(0, 1f, 0); // Center at user height
            sphere.isTrigger = true;
            touchCollider = sphere;
        }

        // Ensure layer is set for raycast detection
        gameObject.layer = LayerMask.NameToLayer("P2PUser") != -1
            ? LayerMask.NameToLayer("P2PUser")
            : LayerMask.NameToLayer("Default");
    }

    /// <summary>
    /// 프로필 사진 기반 큐브 아바타 자동 생성 (0000_Cube.prefab 스타일)
    /// </summary>
    private void SetupCubeAvatar()
    {
        if (!autoCreateCubeAvatar) return;

        // 기존 아바타 렌더러가 있으면 비활성화
        if (avatarRenderer != null)
        {
            avatarRenderer.enabled = false;
        }

        // 큐브 아바타 생성
        generatedCubeAvatar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        generatedCubeAvatar.name = "ProfileCubeAvatar";
        generatedCubeAvatar.transform.SetParent(transform);
        generatedCubeAvatar.transform.localPosition = new Vector3(0, cubeAvatarYOffset, 0);
        generatedCubeAvatar.transform.localScale = Vector3.one * cubeAvatarSize;

        // 콜라이더 제거 (터치 콜라이더는 별도로 관리)
        var cubeCollider = generatedCubeAvatar.GetComponent<Collider>();
        if (cubeCollider != null)
        {
            Destroy(cubeCollider);
        }

        // 렌더러 및 머티리얼 설정
        cubeRenderer = generatedCubeAvatar.GetComponent<MeshRenderer>();
        if (cubeRenderer != null)
        {
            // Unlit 머티리얼 생성 (조명 영향 안받음)
            cubeMaterial = new Material(Shader.Find("Unlit/Texture"));
            if (cubeMaterial.shader == null)
            {
                cubeMaterial = new Material(Shader.Find("Standard"));
            }
            cubeMaterial.color = indicatorColor; // 기본 색상은 핑크
            cubeRenderer.material = cubeMaterial;
        }

        Debug.Log($"[P2PUserInfo] Cube avatar created for user: {username}");
    }

    /// <summary>
    /// 프로필 사진을 큐브 아바타에 적용
    /// </summary>
    private void ApplyProfileTextureToAvatar(Texture2D texture)
    {
        if (cubeRenderer == null || cubeMaterial == null) return;

        cubeMaterial.mainTexture = texture;
        cubeMaterial.color = Color.white; // 텍스처 적용 시 흰색으로 변경

        Debug.Log($"[P2PUserInfo] Profile texture applied to cube avatar for user: {username}");
    }

    /// <summary>
    /// UI 요소 자동 생성 (usernameText, distanceText 등)
    /// </summary>
    private void SetupAutoUI()
    {
        // UI가 이미 할당되어 있으면 스킵
        if (usernameText != null && distanceText != null) return;

        // World Space Canvas 생성
        GameObject canvasObj = new GameObject("UserInfoCanvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = billboardOffset;
        canvasObj.transform.localScale = Vector3.one * 0.01f; // 작은 스케일로 월드 공간에 배치

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // Canvas Group 추가
        canvasGroup = canvasObj.AddComponent<CanvasGroup>();

        // Canvas Scaler 설정
        var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        // Billboard로 사용할 transform 업데이트
        if (billboardTransform == null)
        {
            billboardTransform = canvasObj.transform;
        }

        // Username Text 생성
        if (usernameText == null)
        {
            GameObject usernameObj = new GameObject("UsernameText");
            usernameObj.transform.SetParent(canvasObj.transform);
            usernameObj.transform.localPosition = new Vector3(0, 50, 0);
            usernameObj.transform.localScale = Vector3.one;

            usernameText = usernameObj.AddComponent<TMPro.TextMeshProUGUI>();
            usernameText.text = username;
            usernameText.fontSize = 36;
            usernameText.alignment = TMPro.TextAlignmentOptions.Center;
            usernameText.color = Color.white;

            // RectTransform 설정
            var rectTransform = usernameText.rectTransform;
            rectTransform.sizeDelta = new Vector2(300, 50);
        }

        // Distance Text 생성
        if (distanceText == null)
        {
            GameObject distanceObj = new GameObject("DistanceText");
            distanceObj.transform.SetParent(canvasObj.transform);
            distanceObj.transform.localPosition = new Vector3(0, 0, 0);
            distanceObj.transform.localScale = Vector3.one;

            distanceText = distanceObj.AddComponent<TMPro.TextMeshProUGUI>();
            distanceText.text = "0m";
            distanceText.fontSize = 28;
            distanceText.alignment = TMPro.TextAlignmentOptions.Center;
            distanceText.color = indicatorColor; // 핑크색

            // RectTransform 설정
            var rectTransform = distanceText.rectTransform;
            rectTransform.sizeDelta = new Vector2(200, 40);
        }

        Debug.Log($"[P2PUserInfo] Auto UI created for user: {username}");
    }

    void Update()
    {
        if (!isInitialized) return;

        // Billboard effect - always face camera
        if (enableBillboard && mainCamera != null && billboardTransform != null)
        {
            billboardTransform.rotation = Quaternion.LookRotation(
                billboardTransform.position - mainCamera.transform.position
            );
        }

        // Update distance-based alpha
        UpdateDistanceBasedAlpha();

        // Handle touch input for profile opening
        if (enableTouch)
        {
            HandleTouchInput();
        }
    }

    /// <summary>
    /// Handle touch/click input to open user profile
    /// </summary>
    private void HandleTouchInput()
    {
        // Check for touch (mobile) or mouse click (editor)
        bool hasTouchInput = false;
        Vector2 touchPosition = Vector2.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            hasTouchInput = true;
            touchPosition = Input.mousePosition;
        }
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                hasTouchInput = true;
                touchPosition = touch.position;
            }
        }
#endif

        if (!hasTouchInput) return;

        // Skip if touching UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Raycast from touch position
        Ray ray = mainCamera.ScreenPointToRay(touchPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f))
        {
            // Check if this object was hit
            if (hit.collider.gameObject == gameObject || hit.transform.IsChildOf(transform))
            {
                OnUserTapped();
            }
        }
    }

    /// <summary>
    /// 싱글탭 처리 - 더블탭 확인
    /// </summary>
    private void OnUserTapped()
    {
        float currentTime = Time.time;

        if (waitingForSecondTap && (currentTime - lastTapTime) <= doubleTapThreshold)
        {
            // 더블탭 인식됨 - 프로필 열기
            waitingForSecondTap = false;
            OnUserDoubleTapped();
        }
        else
        {
            // 첫 번째 탭 - 하이라이트만
            waitingForSecondTap = true;
            lastTapTime = currentTime;
            SetHighlight(true);

            // 일정 시간 후 하이라이트 해제 (더블탭 안 된 경우)
            StartCoroutine(ResetHighlightAfterDelay(doubleTapThreshold + 0.1f));
        }
    }

    /// <summary>
    /// 더블탭 시 프로필 열기
    /// </summary>
    public void OnUserDoubleTapped()
    {
        Debug.Log($"[P2PUserInfo] User double-tapped: {username} (ID: {userId})");

        // Highlight this user
        SetHighlight(true);

        // Open profile using ProfileManager
        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.ShowProfile(userId);
        }
        else
        {
            Debug.LogWarning("[P2PUserInfo] ProfileManager not found. Cannot show profile.");
        }

        // Reset highlight after delay
        StartCoroutine(ResetHighlightAfterDelay(0.5f));
    }

    /// <summary>
    /// 외부에서 강제로 프로필 열기 (리스트에서 선택 시 등)
    /// </summary>
    public void OnUserTouched()
    {
        OnUserDoubleTapped();
    }

    private System.Collections.IEnumerator ResetHighlightAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetHighlight(false);
    }

    /// <summary>
    /// Initialize user information display
    /// </summary>
    public void Initialize(string uid, string uname, string avatar, string userBio, float dist)
    {
        userId = uid;
        username = uname;
        avatarUrl = avatar;
        bio = userBio;
        distance = dist;

        UpdateUI();
        isInitialized = true;

        // 오프스크린 인디케이터 업데이트 (이름 및 색상)
        if (targetComponent != null)
        {
            targetComponent.PlaceName = username;
            targetComponent.TargetColor = indicatorColor; // #E95383
        }

        // Load avatar image if URL provided
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            StartCoroutine(LoadAvatarImage(avatarUrl));
        }
    }

    /// <summary>
    /// Update distance value (called by P2PManager during position updates)
    /// </summary>
    public void UpdateDistance(float newDistance)
    {
        distance = newDistance;
        UpdateDistanceText();
        UpdateDistanceBasedAlpha();
    }

    /// <summary>
    /// Update all UI elements
    /// </summary>
    private void UpdateUI()
    {
        // Update username
        if (usernameText != null)
        {
            usernameText.text = username;
            usernameText.gameObject.SetActive(alwaysShowUsername);
        }

        // Update distance
        UpdateDistanceText();

        // Show online status indicator
        if (statusIndicator != null)
        {
            statusIndicator.SetActive(true);
        }
    }

    /// <summary>
    /// Update distance text with formatted string
    /// </summary>
    private void UpdateDistanceText()
    {
        if (distanceText != null && showDistance)
        {
            if (distance < 1000f)
            {
                distanceText.text = $"{Mathf.RoundToInt(distance)}m";
            }
            else
            {
                distanceText.text = $"{(distance / 1000f):F1}km";
            }
            distanceText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Fade UI based on distance (distant users become more transparent)
    /// </summary>
    private void UpdateDistanceBasedAlpha()
    {
        if (canvasGroup == null) return;

        float alpha = 1f;

        // Start fading at 70% of max distance
        float fadeStartDistance = maxVisibleDistance * 0.7f;

        if (distance > fadeStartDistance)
        {
            float fadeRange = maxVisibleDistance - fadeStartDistance;
            float fadeProgress = (distance - fadeStartDistance) / fadeRange;
            alpha = Mathf.Lerp(1f, 0.3f, fadeProgress); // Fade to 30% opacity
        }

        canvasGroup.alpha = alpha;
    }

    /// <summary>
    /// Load avatar image from URL (supports PNG/JPG)
    /// 프로필 이미지를 다운로드하여 UI 및 큐브 아바타에 적용
    /// </summary>
    private System.Collections.IEnumerator LoadAvatarImage(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest www =
            UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(www);

                // UI Image에 적용 (할당된 경우)
                if (avatarImage != null)
                {
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    avatarImage.sprite = sprite;
                }

                // 큐브 아바타에 프로필 텍스처 적용
                ApplyProfileTextureToAvatar(texture);

                Debug.Log($"[P2PUserInfo] Profile image loaded for user: {username}");
            }
            else
            {
                Debug.LogWarning($"[P2PUserInfo] Failed to load avatar image: {url}");
            }
        }
    }

    /// <summary>
    /// Show/hide user information
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Highlight user (e.g., when selected or hovered)
    /// </summary>
    public void SetHighlight(bool highlighted)
    {
        if (usernameText != null)
        {
            usernameText.color = highlighted ? Color.yellow : Color.white;
            usernameText.fontStyle = highlighted ? FontStyles.Bold : FontStyles.Normal;
        }

        // 3D 아바타 하이라이트
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

    /// <summary>
    /// 팔로잉 여부 설정 (아바타 색상 변경)
    /// </summary>
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
    /// 사용자 ID 가져오기
    /// </summary>
    public string GetUserId()
    {
        return userId;
    }

    /// <summary>
    /// Get user info for profile display
    /// </summary>
    public (string userId, string username, string bio, float distance) GetUserInfo()
    {
        return (userId, username, bio, distance);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Draw distance sphere in editor
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxVisibleDistance);
    }
#endif
}
