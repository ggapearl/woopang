using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// AR 환경에서 Main 사진이 적용된 Cube를 미리보기하는 컨트롤러
/// Submit 버튼 클릭 시 확인 판넬과 함께 AR Preview 표시
/// </summary>
public class ARPreviewController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("AR Settings")]
    [SerializeField] private GameObject cubePrefab; // 0000_Cube.prefab
    [SerializeField] private float spawnDistance = 4f;
    [SerializeField] private float spawnHeightOffset = -1f;
    [SerializeField] private float spawnRotationY = 150f;

    [Header("Loading Spinner")]
    [SerializeField] private GameObject loadingSpinnerPrefab;
    [SerializeField] private float spinnerScale = 1.5f;
    [SerializeField] private float spinnerRotationSpeed = 30f;
    [SerializeField] private float spinnerMinDuration = 3f;   // 스피너가 없을 때만 — 있으면 아래 짜잔 순서(SpawnWithLoadingSpinner)
    [SerializeField] private float cubeFadeInDuration = 1.5f;

    [Header("Spawn Emphasis Effect")]
    [SerializeField] private float spawnEmphasisScale = 3f;
    [SerializeField] private bool enableSpawnEmphasis = true;

    [Header("Spinner Sparkle Effect")]
    [SerializeField] private float sparkleSpeed = 1.5f;
    [SerializeField] private float sparkleMinAlpha = 0.6f;
    [SerializeField] private float sparkleMaxAlpha = 1.0f;

    [Header("Loading Text Animation")]
    [SerializeField] private float dotAnimationSpeed = 0.4f;

    // ============================================================
    // Touch Drag Rotation (터치 드래그 회전)
    // ============================================================
    [Header("Touch Drag Rotation")]
    [Tooltip("터치 드래그 회전 활성화")]
    [SerializeField] private bool enableTouchRotation = true;
    [Tooltip("드래그 회전 감도")]
    [SerializeField] private float dragRotationSpeed = 0.3f;
    [Tooltip("드래그 관성 감쇠 (0에 가까울수록 빨리 멈춤)")]
    [SerializeField] private float dragInertiaDecay = 0.92f;

    // ============================================================
    // Auto Rotation (자동 회전)
    // ============================================================
    [Header("Auto Rotation")]
    [Tooltip("터치하지 않을 때 자동 회전")]
    [SerializeField] private bool enableAutoRotation = true;
    [Tooltip("자동 회전 속도 (도/초)")]
    [SerializeField] private float autoRotationSpeed = 15f;
    [Tooltip("터치 종료 후 자동 회전 복귀 대기 시간 (초)")]
    [SerializeField] private float autoRotationResumeDelay = 2f;

    // ============================================================
    // Background Dim (배경 딤 처리)
    // ============================================================
    [Header("Background Dim")]
    [Tooltip("배경 딤 처리 활성화")]
    [SerializeField] private bool enableBackgroundDim = true;
    [Tooltip("씬에 배치된 DimOverlay 오브젝트 (Canvas 하위, Image 컴포넌트 필요)")]
    [SerializeField] private GameObject dimOverlayObject;
    [Tooltip("딤 처리 색상")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.4f);
    [Tooltip("딤 페이드인 시간 (초)")]
    [SerializeField] private float dimFadeInDuration = 0.5f;
    [Tooltip("딤 페이드아웃 시간 (초)")]
    [SerializeField] private float dimFadeOutDuration = 0.3f;

    // ============================================================
    // Star Burst (큐브가 생길 때 퍼지는 별빛)
    // ============================================================
    [Header("Star Burst")]
    [Tooltip("큐브가 생길 때 둘레로 별빛이 퍼진다")]
    [SerializeField] private bool enableParticleBurst = true;
    [Tooltip("별빛 개수")]
    [SerializeField] private int starCount = 70;
    [Tooltip("별빛 수명 (초) — 별마다 60~100%")]
    [SerializeField] private float starLifetime = 2.8f;
    [Tooltip("별빛 크기 (m, 별 끝에서 끝까지) — 별마다 50~100%")]
    [SerializeField] private float starSize = 0.14f;

    // ============================================================
    // Spotlight Effect (3D 스포트라이트)
    // ============================================================
    [Header("Spotlight Effect")]
    [Tooltip("큐브 위에 스포트라이트 활성화 (Lit 셰이더 사용 오브젝트에만 효과 있음, Unlit 셰이더는 조명 무시)")]
    [SerializeField] private bool enableSpotlight = false;
    [Tooltip("스포트라이트 색상")]
    [SerializeField] private Color spotlightColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("스포트라이트 밝기")]
    [SerializeField] private float spotlightIntensity = 5f;
    [Tooltip("스포트라이트 범위")]
    [SerializeField] private float spotlightRange = 25f;
    [Tooltip("스포트라이트 각도")]
    [SerializeField] private float spotlightAngle = 60f;
    [Tooltip("스포트라이트 높이 오프셋 (큐브 위)")]
    [SerializeField] private float spotlightHeightOffset = 3f;
    [Tooltip("스포트라이트 페이드인 시간")]
    [SerializeField] private float spotlightFadeInDuration = 1f;

    // ============================================================
    // Private State
    // ============================================================
    private GameObject spawnedSpinner;
    private GameObject spawnedCube;
    private GameObject spawnGuide;
    private Coroutine spawnCoroutine;
    private Action onConfirm;
    private Action onCancel;
    private Camera arCamera;
    private Coroutine loadingTextCoroutine;
    private Coroutine spinnerFadeCoroutine;
    private bool isLoadingComplete = false;
    private bool isSpinnerFading = false;

    // Touch rotation state
    private bool isDragging = false;
    private Vector2 lastDragPosition;
    private float dragVelocityX = 0f;
    private float dragVelocityY = 0f;
    private float lastTouchTime = 0f;
    private bool isCubeReady = false;
    private Texture2D currentPhoto;        // 미리보기에 띄운 대표 사진 (CubeUploadManager 것 — 지우지 않는다)
    private MeshRenderer[] cubeRenderers;  // 사진을 입힌 큐브 렌더러 — AI 작업이 그림을 바꿔 끼우고, 캐릭터를 세울 땐 숨긴다

    // Background dim
    private Image dimOverlayImage;

    // Spotlight
    private Light spawnedSpotlight;
    private GameObject spotlightObj;

    private void Awake()
    {
        EnhancedTouchSupport.Enable();

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        arCamera = Camera.main;

        if (previewPanel != null)
            previewPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isCubeReady || spawnedCube == null) return;

        HandleTouchRotation();
        HandleAutoRotation();
        HandleDragInertia();
    }

    // ============================================================
    // Touch Drag Rotation
    // ============================================================
    private void HandleTouchRotation()
    {
        if (!enableTouchRotation) return;

        // Mouse (에디터 + 데스크톱)
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                isDragging = true;
                lastDragPosition = mouse.position.ReadValue();
                dragVelocityX = 0f;
                dragVelocityY = 0f;
            }
            else if (mouse.leftButton.isPressed && isDragging)
            {
                Vector2 currentPos = mouse.position.ReadValue();
                Vector2 delta = currentPos - lastDragPosition;

                float rotX = delta.y * dragRotationSpeed;
                float rotY = -delta.x * dragRotationSpeed;

                spawnedCube.transform.Rotate(Vector3.up, rotY, Space.World);
                spawnedCube.transform.Rotate(Vector3.right, rotX, Space.World);

                dragVelocityX = rotX;
                dragVelocityY = rotY;
                lastDragPosition = currentPos;
                lastTouchTime = Time.time;
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
                lastTouchTime = Time.time;
            }
        }

        // Touchscreen (모바일)
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && Touch.activeTouches.Count == 1)
        {
            Touch touch = Touch.activeTouches[0];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                isDragging = true;
                lastDragPosition = touch.screenPosition;
                dragVelocityX = 0f;
                dragVelocityY = 0f;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && isDragging)
            {
                Vector2 delta = touch.screenPosition - lastDragPosition;

                float rotX = delta.y * dragRotationSpeed;
                float rotY = -delta.x * dragRotationSpeed;

                spawnedCube.transform.Rotate(Vector3.up, rotY, Space.World);
                spawnedCube.transform.Rotate(Vector3.right, rotX, Space.World);

                dragVelocityX = rotX;
                dragVelocityY = rotY;
                lastDragPosition = touch.screenPosition;
                lastTouchTime = Time.time;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                     touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                isDragging = false;
                lastTouchTime = Time.time;
            }
        }
    }

    private void HandleDragInertia()
    {
        if (isDragging || spawnedCube == null) return;

        if (Mathf.Abs(dragVelocityX) > 0.01f || Mathf.Abs(dragVelocityY) > 0.01f)
        {
            spawnedCube.transform.Rotate(Vector3.up, dragVelocityY, Space.World);
            spawnedCube.transform.Rotate(Vector3.right, dragVelocityX, Space.World);

            dragVelocityX *= dragInertiaDecay;
            dragVelocityY *= dragInertiaDecay;
        }
    }

    private void HandleAutoRotation()
    {
        if (!enableAutoRotation || spawnedCube == null) return;
        if (isDragging) return;

        // 관성이 충분히 줄어들고 대기 시간이 지나면 자동 회전 시작
        bool inertiaSettled = Mathf.Abs(dragVelocityX) < 0.05f && Mathf.Abs(dragVelocityY) < 0.05f;
        bool waitComplete = Time.time - lastTouchTime > autoRotationResumeDelay;

        if (inertiaSettled && waitComplete)
        {
            spawnedCube.transform.Rotate(Vector3.up, autoRotationSpeed * Time.deltaTime, Space.World);
        }
    }

    // ============================================================
    // Background Dim
    // ============================================================
    private void InitDimOverlay()
    {
        if (!enableBackgroundDim) return;

        if (dimOverlayObject == null)
        {
            Debug.LogWarning("[ARPreviewController] dimOverlayObject가 할당되지 않았습니다! Inspector에서 연결 필요");
            return;
        }

        dimOverlayImage = dimOverlayObject.GetComponent<Image>();
        if (dimOverlayImage == null)
        {
            dimOverlayImage = dimOverlayObject.AddComponent<Image>();
            dimOverlayImage.raycastTarget = false;
        }

        dimOverlayImage.color = new Color(dimColor.r, dimColor.g, dimColor.b, 0f);
        dimOverlayObject.SetActive(false);
    }

    private IEnumerator FadeDimOverlay(bool fadeIn)
    {
        if (dimOverlayImage == null || dimOverlayObject == null) yield break;

        dimOverlayObject.SetActive(true);
        float duration = fadeIn ? dimFadeInDuration : dimFadeOutDuration;
        float startAlpha = dimOverlayImage.color.a;
        float endAlpha = fadeIn ? dimColor.a : 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            dimOverlayImage.color = new Color(dimColor.r, dimColor.g, dimColor.b, alpha);
            yield return null;
        }

        dimOverlayImage.color = new Color(dimColor.r, dimColor.g, dimColor.b, endAlpha);

        if (!fadeIn)
            dimOverlayObject.SetActive(false);
    }

    // ============================================================
    // Star Burst
    // ============================================================
    // 10-22 대표님 「퍼지는 입자가 크고 흰색이 아니라 어색 — 반짝이며 별빛이 나풀거리게」
    // 작은 흰 별빛이 큐브 겉에서 피어나 별마다 다르게 깜빡이며 천천히 흩날리다 사그라든다
    private void PlayParticleBurst(Vector3 position)
    {
        if (!enableParticleBurst || StarSparkle.Material == null) return;

        GameObject particleObj = new GameObject("ARPreview_StarBurst");
        particleObj.transform.position = position;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(starLifetime * 0.6f, starLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(starSize * 0.5f, starSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.01f;   // 아주 천천히 내려앉는다
        main.maxParticles = starCount;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, starCount / 2),
            new ParticleSystem.Burst(0.25f, starCount * 3 / 10),
            new ParticleSystem.Burst(0.5f, starCount / 5)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        shape.radiusThickness = 0.4f;

        // 피어날 때만 살짝 퍼지고 곧 느려진다 — 그다음은 흩날림이 움직인다
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.25f, 0.2f), new Keyframe(1f, 0.05f)));

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.7f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;

        var spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);   // 라디안/초 — 빛줄기가 천천히 돈다

        // 깜빡이는 횟수가 다른 두 곡선 사이에서 별마다 다르게
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, StarSparkle.TwinkleCurve(3), StarSparkle.TwinkleCurve(5));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        particleObj.GetComponent<ParticleSystemRenderer>().sharedMaterial = StarSparkle.Material;

        ps.Play();
        Destroy(particleObj, main.duration + starLifetime + 0.5f);
    }

    // ============================================================
    // Preview Start / Spawn
    // ============================================================
    public void StartPreview(Texture2D mainPhotoTexture, Action onConfirmCallback, Action onCancelCallback = null)
    {
        onConfirm = onConfirmCallback;
        onCancel = onCancelCallback;
        currentPhoto = mainPhotoTexture;
        isLoadingComplete = false;
        isCubeReady = false;
        isDragging = false;
        dragVelocityX = 0f;
        dragVelocityY = 0f;

        if (loadingTextCoroutine != null)
            StopCoroutine(loadingTextCoroutine);
        loadingTextCoroutine = StartCoroutine(AnimateLoadingText());

        if (previewPanel != null)
            previewPanel.SetActive(true);

        // 배경 딤 시작
        if (enableBackgroundDim)
        {
            if (dimOverlayImage == null)
                InitDimOverlay();
            StartCoroutine(FadeDimOverlay(true));
        }

        SpawnCubeInFrontOfCamera(mainPhotoTexture);
    }

    private void SpawnCubeInFrontOfCamera(Texture2D mainPhotoTexture)
    {
        if (cubePrefab == null)
        {
            Debug.LogError("[ARPreviewController] Cube Prefab이 할당되지 않았습니다!");
            return;
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null)
            {
                Debug.LogError("[ARPreviewController] AR Camera를 찾을 수 없습니다!");
                return;
            }
        }

        StopSpawn();
        CleanupSpinner();
        CleanupCube();

        CleanupSpotlight();

        Vector3 cameraForward = arCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 spawnPosition = arCamera.transform.position
            + cameraForward * spawnDistance
            + Vector3.up * spawnHeightOffset;

        Quaternion spawnRotation = Quaternion.Euler(0f, spawnRotationY, 0f);

        CreateSpawnGuide(spawnPosition);
        spawnCoroutine = StartCoroutine(SpawnWithLoadingSpinner(spawnPosition, spawnRotation, mainPhotoTexture));
    }

    // 10-20 대표님 「생성 중엔 뒤늦게 생겨도 괜찮으니 어디에 생기는지만」 → 생길 자리에 빈 타겟 하나, 둘레에 별빛.
    // 10-22 「큰 화살표만 발생하는데 '생성 중...' 이라고 — 기존 인디케이터 형식으로」 → 큐브 인디케이터와 같은 모양·크기
    // (화면 밖이면 화살표, 안이면 박스)에 이름만 '생성 중...'. 큐브가 다 생기면 큐브 자기 인디케이터('신규 오브젝트')로 넘긴다
    private void CreateSpawnGuide(Vector3 position)
    {
        CleanupSpawnGuide();
        spawnGuide = new GameObject("ARPreview_SpawnGuide");
        spawnGuide.SetActive(false);   // 다 정한 뒤 켜야 OffScreenIndicator 가 처음부터 이 모양으로 받는다
        spawnGuide.transform.position = position;
        Target t = spawnGuide.AddComponent<Target>();
        Target look = cubePrefab.GetComponentInChildren<Target>(true);
        if (look != null) t.CopyLook(look);
        t.PlaceName = GetLocalizedText("spawn_guide_creating");
        t.Sparkle = true;
        spawnGuide.SetActive(true);
    }

    // ============================================================
    // Spawn Sequence
    // ============================================================
    // 11-09 대표님 「보물찾기에서 보물 찾았을 때 나오는 것처럼 … 천천히 짜잔~ 하는 느낌으로 … 지금 로딩스피너는 너무 천천히 돌아가는데 재미가 없거든」
    // → 보물 '찾았다'(R0926HuntCelebrate)와 같은 결: 스피너가 톡 나타나 → 두근두근(쿵-쿵 · 점점 빨리 돌고 세게 떨다 · 끝에 몰아치듯)
    //   → 펑 흩어지며 큐브가 한 바퀴 돌아 톡 튀어나온다(살짝 넘쳤다 돌아옴 · 테두리 번쩍 · 별빛 · 진동). 그다음은 2초마다 심장 박동(R0926Heartbeat).
    // 예전엔 스피너가 0.5초 나타나 초당 45° 로 2초 돌고, 큐브가 2초 동안 서서히 나타나며 스피너가 4초에 걸쳐 사라졌다.
    private const float PopIn = 0.4f, Suspense = 2.6f, Crescendo = 2.2f, Dub = 0.16f, Reveal = 0.8f;
    private static readonly float[] Beats = { 0.05f, 0.8f, 1.35f, 1.8f, 2.12f };   // 쿵 (뒤따르는 쿵은 Dub 뒤) — 점점 빨라진다

    private IEnumerator SpawnWithLoadingSpinner(Vector3 position, Quaternion rotation, Texture2D mainPhotoTexture)
    {
        R0926Heartbeat heart;
        if (loadingSpinnerPrefab != null)
        {
            spawnedSpinner = Instantiate(loadingSpinnerPrefab, position, rotation);
            spawnedSpinner.SetActive(true);
            PrepareForAlphaFade(spawnedSpinner);
            SetObjectAlpha(spawnedSpinner, 0f);

            yield return StartCoroutine(SpinnerSuspense(rotation));

            // 짜잔 — 스피너는 펑 흩어지고 큐브가 튀어나온다
            spawnedCube = Instantiate(cubePrefab, position, rotation);
            spawnedCube.SetActive(true);
            ShowCubeIndicator(false);
            ApplyTextureToCube(mainPhotoTexture);
            SetObjectAlpha(spawnedCube, 0f);
            heart = spawnedCube.AddComponent<R0926Heartbeat>();
            heart.Flash(Reveal + 0.4f);

            isSpinnerFading = true;
            spinnerFadeCoroutine = StartCoroutine(BurstSpinner());
            if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect(1f);

            // 파티클 버스트 — 큐브가 튀어나오는 순간
            try { PlayParticleBurst(position); }
            catch (Exception e) { Debug.LogWarning($"[ARPreviewController] ParticleBurst: {e.Message}"); }

            yield return StartCoroutine(PopCube(rotation));

            // 다 튀어나온 뒤에도 스피너가 흩어지는 중이면 끝까지 기다린다
            if (isSpinnerFading && spinnerFadeCoroutine != null)
            {
                yield return spinnerFadeCoroutine;
            }
        }
        else
        {
            yield return new WaitForSeconds(spinnerMinDuration);

            spawnedCube = Instantiate(cubePrefab, position, rotation);
            spawnedCube.SetActive(true);
            ShowCubeIndicator(false);
            ApplyTextureToCube(mainPhotoTexture);
            SetObjectAlpha(spawnedCube, 0f);
            heart = spawnedCube.AddComponent<R0926Heartbeat>();

            yield return StartCoroutine(FadeObject(spawnedCube, 0f, 1f, cubeFadeInDuration));
        }

        // '생성 중' 안내 → 큐브 자기 인디케이터('신규 오브젝트')
        Target cubeTarget = spawnedCube.GetComponentInChildren<Target>();
        if (cubeTarget != null)
        {
            cubeTarget.PlaceName = GetLocalizedText("new_object");
        }
        CleanupSpawnGuide();
        ShowCubeIndicator(true);

        // 큐브 준비 완료 → 터치 회전 + 자동 회전 즉시 활성화
        isCubeReady = true;
        lastTouchTime = Time.time - autoRotationResumeDelay;
        heart.Beat(1f);   // 1초 뒤부터 2초마다 쿵-쿵

        // 스포트라이트
        try { CreateSpotlight(position); }
        catch (Exception e) { Debug.LogWarning($"[ARPreviewController] Spotlight: {e.Message}"); }

        // 스폰 강조 효과
        if (enableSpawnEmphasis)
        {
            try { PlaySpawnEmphasisEffect(position); }
            catch (Exception e) { Debug.LogWarning($"[ARPreviewController] SpawnEmphasis: {e.Message}"); }
        }

        // 텍스트 전환
        isLoadingComplete = true;
        yield return StartCoroutine(TransitionToConfirmText());
    }

    // '생성 중' 안내가 있는 동안은 큐브 자기 인디케이터를 끈다 — 같은 자리에 둘이 겹치지 않게
    private void ShowCubeIndicator(bool show)
    {
        Target t = spawnedCube.GetComponentInChildren<Target>();
        Target look = cubePrefab.GetComponentInChildren<Target>(true);
        if (t == null || look == null) return;
        t.NeedBoxIndicator = show && look.NeedBoxIndicator;
        t.NeedArrowIndicator = show && look.NeedArrowIndicator;
    }

    // 1) 톡 — 작은 점에서 튀어나오듯  2) 두근두근 — 쿵-쿵에 맞춰 커지고 · 점점 빨리 돌고 · 점점 세게 떨다 · 끝에 몰아치듯 (쿵마다 살짝 진동)
    private IEnumerator SpinnerSuspense(Quaternion rotation)
    {
        GameObject sp = spawnedSpinner;
        Transform t = sp.transform;
        Vector3 size = Vector3.one * spinnerScale;
        var rotator = sp.GetComponent<SpinnerRotator>();
        float spin0 = 2f * (spinnerRotationSpeed + (rotator != null ? rotator.speed : 0f));   // 예전 빠르기의 두 배에서 시작해 초당 세 바퀴까지
        if (rotator != null) rotator.enabled = false;   // 도는 건 여기서
        float yaw = 0f;
        for (float s = 0f; s < PopIn; s += Time.deltaTime)
        {
            if (sp == null || sp != spawnedSpinner) yield break;
            float k = s / PopIn;
            yaw += spin0 * Time.deltaTime;
            t.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * rotation;
            t.localScale = size * Mathf.Max(0f, BackOut(k));
            SetObjectAlpha(sp, Mathf.Clamp01(k * 2f));
            yield return null;
        }
        int lub = 0;
        float tick = Crescendo;
        for (float s = 0f; s < Suspense; s += Time.deltaTime)
        {
            if (sp == null || sp != spawnedSpinner) yield break;
            while (lub < Beats.Length && s >= Beats[lub]) { Buzz(); lub++; }
            if (s >= Crescendo && s >= tick) { Buzz(); tick += 0.085f; }

            float beat = 0f;
            foreach (float b in Beats) beat = Mathf.Max(beat, Bump(s - b, 0.15f) + 0.5f * Bump(s - b - Dub, 0.13f));
            float grow = s < Crescendo ? 0f : Mathf.SmoothStep(0f, 1f, (s - Crescendo) / (Suspense - Crescendo));
            float amp = s < Crescendo ? Mathf.Lerp(0.6f, 3f, Mathf.Pow(s / Crescendo, 1.5f)) : Mathf.Lerp(3f, 10f, grow);
            float tt = Time.time;
            Transform cam = arCamera != null ? arCamera.transform : null;
            Quaternion shake = Quaternion.AngleAxis(amp * (0.6f * Mathf.Sin(tt * 33f) + 0.4f * Mathf.Sin(tt * 51f + 1.3f)), cam != null ? cam.forward : Vector3.forward)
                             * Quaternion.AngleAxis(amp * 0.5f * Mathf.Sin(tt * 27f + 0.7f), cam != null ? cam.right : Vector3.right);
            yaw += Mathf.Lerp(spin0, 1080f, Mathf.Pow(s / Suspense, 2f)) * Time.deltaTime;
            t.rotation = shake * Quaternion.AngleAxis(yaw, Vector3.up) * rotation;
            t.localScale = size * (1f + 0.1f * beat + 0.12f * grow);
            float shimmer = 0.5f + 0.5f * Mathf.Sin(s * sparkleSpeed * Mathf.PI * 2f);
            SetObjectAlpha(sp, Mathf.Lerp(sparkleMinAlpha, sparkleMaxAlpha, Mathf.Max(Mathf.Max(shimmer, beat), grow)));
            yield return null;
        }
    }

    // 펑 — 스피너가 확 커지며 흩어진다
    private IEnumerator BurstSpinner()
    {
        const float Secs = 0.35f;
        GameObject sp = spawnedSpinner;
        if (sp != null)
        {
            Transform t = sp.transform;
            Vector3 from = t.localScale;
            for (float s = 0f; s < Secs && sp != null; s += Time.deltaTime)
            {
                float e = 1f - Mathf.Pow(1f - s / Secs, 3f);
                t.localScale = from * (1f + 0.8f * e);
                t.Rotate(Vector3.up, 1080f * (1f - e) * Time.deltaTime, Space.World);
                SetObjectAlpha(sp, 1f - e);
                yield return null;
            }
        }
        isSpinnerFading = false;
        spinnerFadeCoroutine = null;
        CleanupSpinner();
    }

    // 큐브 — 작게 시작해 한 바퀴 돌며 톡 튀어올라 살짝 넘쳤다 돌아온다 (처음 0.12초는 펑 — 잠깐 납작)
    private IEnumerator PopCube(Quaternion rotation)
    {
        GameObject cube = spawnedCube;
        Transform t = cube.transform;
        Vector3 size = t.localScale, at = t.position;
        for (float s = 0f; s < Reveal; s += Time.deltaTime)
        {
            if (cube == null) yield break;
            float sc = Mathf.LerpUnclamped(0.15f, 1f, BackOut(Mathf.Clamp01(s / 0.55f)));
            float sq = s < 0.12f ? Mathf.Sin(s / 0.12f * Mathf.PI) : 0f;
            t.localScale = Vector3.Scale(size, new Vector3(sc + 0.06f * sq, sc - 0.12f * sq, sc + 0.06f * sq));
            t.rotation = Quaternion.AngleAxis(-360f * Mathf.Pow(1f - Mathf.Clamp01(s / 0.7f), 3f), Vector3.up) * rotation;
            t.position = at + Vector3.up * (0.12f * size.y * Mathf.Sin(Mathf.Clamp01(s / 0.6f) * Mathf.PI));
            SetObjectAlpha(cube, Mathf.Clamp01(s / 0.3f));
            yield return null;
        }
        if (cube == null) yield break;
        t.localScale = size;
        t.rotation = rotation;
        t.position = at;
        SetObjectAlpha(cube, 1f);
    }

    private static void Buzz()
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
    }

    // 0 → 1 → 0 (w 초 동안 — 앞이 빠르게 솟는다) · 보물 축하 화면과 같은 곡선
    private static float Bump(float x, float w)
    {
        if (x <= 0f || x >= w) return 0f;
        return Mathf.Sin(Mathf.Sqrt(x / w) * Mathf.PI);
    }

    // 살짝 넘쳤다 돌아온다 (0 → 1.1 → 1)
    private static float BackOut(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    // ============================================================
    // Loading Text Animation
    // ============================================================
    private IEnumerator AnimateLoadingText()
    {
        string baseText = GetLocalizedText("creating_ar_object");
        int dotCount = 0;

        // Rich Text로 투명 점을 사용하여 텍스트 폭 고정 (중앙정렬 흔들림 방지)
        if (messageText != null)
            messageText.supportRichText = true;

        while (!isLoadingComplete)
        {
            dotCount = (dotCount % 3) + 1;
            string visible = new string('.', dotCount);
            string invisible = new string('.', 3 - dotCount);

            if (messageText != null)
            {
                if (invisible.Length > 0)
                    messageText.text = baseText + visible + "<color=#00000000>" + invisible + "</color>";
                else
                    messageText.text = baseText + visible;
            }

            yield return new WaitForSeconds(dotAnimationSpeed);
        }
    }

    private IEnumerator TransitionToConfirmText()
    {
        if (messageText == null) yield break;

        float fadeOutDuration = 0.3f;
        CanvasGroup textCanvasGroup = messageText.GetComponent<CanvasGroup>();
        if (textCanvasGroup == null)
            textCanvasGroup = messageText.gameObject.AddComponent<CanvasGroup>();

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            textCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        messageText.text = GetLocalizedText("confirm_add_object");

        Vector3 originalScale = messageText.transform.localScale;
        messageText.transform.localScale = originalScale * 0.8f;

        float fadeInDuration = 0.4f;
        elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;
            float easeT = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);

            textCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            messageText.transform.localScale = Vector3.Lerp(originalScale * 0.8f, originalScale, easeT);
            yield return null;
        }

        textCanvasGroup.alpha = 1f;
        messageText.transform.localScale = originalScale;
    }

    // ============================================================
    // Spawn Emphasis Effect
    // ============================================================
    private void PlaySpawnEmphasisEffect(Vector3 worldPosition)
    {
        if (arCamera == null) return;

        Vector3 screenPos = arCamera.WorldToScreenPoint(worldPosition);
        if (screenPos.z <= 0) return;

        IndicatorSparkleHelper sparkleHelper = FindFirstObjectByType<IndicatorSparkleHelper>();
        if (sparkleHelper == null) return;

        Vector2 originalSize = sparkleHelper.sparkleSize;
        float originalSpawnDelay = sparkleHelper.spawnDelay;
        sparkleHelper.sparkleSize = originalSize * spawnEmphasisScale;
        sparkleHelper.spawnDelay = 0f;

        IndicatorSparkleHelper.PlaySparkleForIndicator(screenPos, IndicatorType.ARROW);

        sparkleHelper.sparkleSize = originalSize;
        sparkleHelper.spawnDelay = originalSpawnDelay;
    }

    // ============================================================
    // Spotlight Effect
    // ============================================================
    private void CreateSpotlight(Vector3 cubePosition)
    {
        if (!enableSpotlight) return;

        spotlightObj = new GameObject("ARPreview_Spotlight");
        spotlightObj.transform.position = cubePosition + Vector3.up * spotlightHeightOffset;
        spotlightObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 아래를 향함

        spawnedSpotlight = spotlightObj.AddComponent<Light>();
        spawnedSpotlight.type = LightType.Spot;
        spawnedSpotlight.color = spotlightColor;
        spawnedSpotlight.intensity = 0f; // 페이드인 시작
        spawnedSpotlight.range = spotlightRange;
        spawnedSpotlight.spotAngle = spotlightAngle;
        spawnedSpotlight.shadows = LightShadows.None; // 성능 + 호환성
        spawnedSpotlight.innerSpotAngle = spotlightAngle * 0.5f;

        // URP Additional Light Data 추가 (URP에서 라이트 렌더링에 필요)
        var urpLightData = spotlightObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
        if (urpLightData != null)
        {
            urpLightData.usePipelineSettings = true;
        }

        StartCoroutine(FadeSpotlight(0f, spotlightIntensity, spotlightFadeInDuration));
    }

    private IEnumerator FadeSpotlight(float from, float to, float duration)
    {
        if (spawnedSpotlight == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // EaseOutQuad
            float easeT = 1f - (1f - t) * (1f - t);
            if (spawnedSpotlight != null)
                spawnedSpotlight.intensity = Mathf.Lerp(from, to, easeT);
            yield return null;
        }

        if (spawnedSpotlight != null)
            spawnedSpotlight.intensity = to;
    }

    private void CleanupSpotlight()
    {
        if (spotlightObj != null)
        {
            Destroy(spotlightObj);
            spotlightObj = null;
            spawnedSpotlight = null;
        }
    }

    // ============================================================
    // Object Alpha
    // ============================================================

    /// <summary>
    /// 알파 페이드가 안 되는 셰이더(Particles/Standard Unlit 등)를
    /// URP Unlit으로 교체하여 알파 블렌딩 보장
    /// </summary>
    private void PrepareForAlphaFade(GameObject obj)
    {
        if (obj == null) return;

        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpUnlit == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            if (rend == null || rend.material == null) continue;
            Material mat = rend.material;

            // _Surface나 _GlobalAlpha가 있으면 이미 알파 페이드 지원
            if (mat.HasProperty("_Surface") || mat.HasProperty("_GlobalAlpha"))
                continue;

            // 기존 색상/텍스처 보존 후 셰이더 교체
            Color color = mat.HasProperty("_Color") ? mat.color : Color.white;
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;

            mat.shader = urpUnlit;

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_BaseMap") && mainTex != null)
                mat.SetTexture("_BaseMap", mainTex);

        }
    }

    private void SetObjectAlpha(GameObject obj, float alpha)
    {
        if (obj == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                Material mat = renderer.material;

                bool blendModeHandled = false;

                // Standard 셰이더 (_Mode 프로퍼티)
                if (mat.HasProperty("_Mode"))
                {
                    blendModeHandled = true;
                    if (alpha < 1f)
                    {
                        mat.SetFloat("_Mode", 2f); // 2 = Fade
                        mat.SetOverrideTag("RenderType", "Transparent");
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    }
                }
                // URP Lit (_Surface 프로퍼티) — 항상 Transparent 모드 유지
                else if (mat.HasProperty("_Surface"))
                {
                    blendModeHandled = true;
                    mat.SetFloat("_Surface", 1f);
                    mat.SetFloat("_Blend", 0f);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                // T5EdgeLine 등 _GlobalAlpha 커스텀 셰이더
                else if (mat.HasProperty("_GlobalAlpha"))
                {
                    blendModeHandled = true;
                }

                // Fallback: _Mode/_Surface/_GlobalAlpha 모두 없는 셰이더
                // (Particles/Standard Unlit 등 — 키워드로 블렌드 모드 전환)
                if (!blendModeHandled && alpha < 1f)
                {
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.DisableKeyword("_ALPHAMODULATE_ON");
                    if (mat.HasProperty("_SrcBlend"))
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    if (mat.HasProperty("_DstBlend"))
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    if (mat.HasProperty("_ZWrite"))
                        mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }

                if (mat.HasProperty("_Color"))
                {
                    Color color = mat.color;
                    color.a = alpha;
                    mat.color = color;
                }

                if (mat.HasProperty("_BaseColor"))
                {
                    Color baseColor = mat.GetColor("_BaseColor");
                    baseColor.a = alpha;
                    mat.SetColor("_BaseColor", baseColor);
                }

                // Particle 셰이더용 _TintColor
                if (mat.HasProperty("_TintColor"))
                {
                    Color tint = mat.GetColor("_TintColor");
                    tint.a = alpha;
                    mat.SetColor("_TintColor", tint);
                }

                if (mat.HasProperty("_Alpha"))
                {
                    mat.SetFloat("_Alpha", alpha);
                }

                // T5EdgeLine 등 커스텀 셰이더의 글로벌 알파
                if (mat.HasProperty("_GlobalAlpha"))
                {
                    mat.SetFloat("_GlobalAlpha", alpha);
                }
            }
        }
    }

    private IEnumerator FadeObject(GameObject obj, float fromAlpha, float toAlpha, float duration)
    {
        if (obj == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (obj == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentAlpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            SetObjectAlpha(obj, currentAlpha);
            yield return null;
        }

        if (obj != null)
            SetObjectAlpha(obj, toAlpha);
    }

    // ============================================================
    // Texture
    // ============================================================
    private void ApplyTextureToCube(Texture2D mainPhotoTexture)
    {
        if (spawnedCube == null || mainPhotoTexture == null) return;

        MeshRenderer[] renderers = spawnedCube.GetComponentsInChildren<MeshRenderer>();
        cubeRenderers = renderers;
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                Material newMat = new Material(renderer.material);

                if (newMat.HasProperty("_BaseMap"))
                    newMat.SetTexture("_BaseMap", mainPhotoTexture);

                if (newMat.HasProperty("_MainTex"))
                    newMat.SetTexture("_MainTex", mainPhotoTexture);

                renderer.material = newMat;
            }
        }
    }

    // ============================================================
    // AI 작업 (R0926AiWorkPanel — 11-12 대표님 「작업된 오브젝트가 반영된 상태에서 … 3D오브젝트는 기존 발생하던 것처럼 돌고 있는 상황」)
    // ============================================================
    public Texture2D Photo => currentPhoto;
    public bool CubeReady => isCubeReady && spawnedCube != null;
    /// <summary>다 튀어나와 돌고 있는 미리보기 큐브 (아직이면 null)</summary>
    public GameObject PreviewCube => CubeReady ? spawnedCube : null;
    public MeshRenderer[] CubeRenderers => CubeReady ? cubeRenderers : null;

    /// <summary>손본 사진을 돌고 있는 큐브에 바로 입힌다 — 큐브 재질 복사본(ApplyTextureToCube)의 그림만 바꿔 끼운다</summary>
    public void SetPreviewTexture(Texture2D tex)
    {
        if (cubeRenderers == null || tex == null) return;
        foreach (var r in cubeRenderers)
        {
            var m = r != null ? r.material : null;
            if (m == null) continue;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }
    }

    // ============================================================
    // Buttons
    // ============================================================
    private void OnConfirmButtonClicked()
    {
        CleanupAll();

        if (previewPanel != null)
            previewPanel.SetActive(false);

        onConfirm?.Invoke();
        ClearCallbacks();
    }

    private void OnCancelButtonClicked()
    {
        CleanupAll();

        if (previewPanel != null)
            previewPanel.SetActive(false);

        onCancel?.Invoke();
        ClearCallbacks();
    }

    // ============================================================
    // Cleanup
    // ============================================================
    private void CleanupAll()
    {
        isCubeReady = false;
        currentPhoto = null;
        cubeRenderers = null;
        StopSpawn();
        if (loadingTextCoroutine != null)
        {
            StopCoroutine(loadingTextCoroutine);
            loadingTextCoroutine = null;
        }
        CleanupCube();
        CleanupSpinner();
        CleanupSpawnGuide();

        CleanupSpotlight();

        if (enableBackgroundDim && dimOverlayImage != null)
            StartCoroutine(FadeDimOverlay(false));
    }

    private void CleanupSpinner()
    {
        if (spinnerFadeCoroutine != null)
        {
            StopCoroutine(spinnerFadeCoroutine);
            spinnerFadeCoroutine = null;
            isSpinnerFading = false;
        }
        if (spawnedSpinner != null)
        {
            Destroy(spawnedSpinner);
            spawnedSpinner = null;
        }
    }

    // 생성 중에 취소·추가하기를 누르면 — 이 컨트롤러는 패널과 다른 오브젝트라 패널을 꺼도 생성 코루틴이 계속 돌아,
    // 몇 초 뒤 아무도 없는 자리에 큐브와 '신규 오브젝트' 인디케이터가 남았다 (10-20)
    private void StopSpawn()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private void CleanupSpawnGuide()
    {
        if (spawnGuide == null) return;
        spawnGuide.SetActive(false);   // 바로 내린다 — Destroy 는 프레임 끝이라 그 사이 '생성 중'과 '신규 오브젝트'가 한 프레임 겹친다
        Destroy(spawnGuide);
        spawnGuide = null;
    }

    private void CleanupCube()
    {
        if (spawnedCube != null)
        {
            Destroy(spawnedCube);
            spawnedCube = null;
        }
    }

    private void ClearCallbacks()
    {
        onConfirm = null;
        onCancel = null;
    }

    // ============================================================
    // Localization
    // ============================================================
    private void UpdateLocalizedTexts() { }

    private string GetLocalizedText(string key)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.GetText(key);

        switch (key)
        {
            case "confirm_add_object": return "Would you like to add an object here?";
            case "creating_ar_object": return "Creating AR object";
            case "spawn_guide_creating": return "Creating...";
            case "new_object": return "New Object";
            case "confirm": return "Confirm";
            case "cancel": return "Cancel";
            default: return key;
        }
    }

    public void SetCubePrefab(GameObject prefab)
    {
        cubePrefab = prefab;
    }

    // ============================================================
    // Editor Test
    // ============================================================
#if UNITY_EDITOR
    [Header("Editor Test")]
    [SerializeField] private Texture2D testTexture;

    [ContextMenu("Test AR Preview (Editor)")]
    public void TestARPreviewInEditor()
    {
        if (cubePrefab == null)
        {
            Debug.LogError("[ARPreviewController] Cube Prefab이 할당되지 않았습니다!");
            return;
        }

        Camera testCamera = UnityEditor.SceneView.lastActiveSceneView?.camera;
        if (testCamera == null)
            testCamera = Camera.main;

        if (testCamera == null)
        {
            Debug.LogError("[ARPreviewController] 테스트용 카메라를 찾을 수 없습니다!");
            return;
        }

        CleanupCube();

        Vector3 cameraForward = testCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 spawnPosition = testCamera.transform.position
            + cameraForward * spawnDistance
            + Vector3.up * spawnHeightOffset;

        Quaternion spawnRotation = Quaternion.Euler(0f, spawnRotationY, 0f);
        spawnedCube = Instantiate(cubePrefab, spawnPosition, spawnRotation);

        if (testTexture != null)
            ApplyTextureToCube(testTexture);

        UnityEditor.Selection.activeGameObject = spawnedCube;
        UnityEditor.SceneView.lastActiveSceneView?.Frame(new Bounds(spawnPosition, Vector3.one * 2f), false);
    }

    /// <summary>
    /// Play 모드에서 전체 프리뷰 시퀀스 테스트 (스피너→큐브→파티클→회전 등 모든 효과)
    /// </summary>
    public void TestFullPreviewInPlayMode()
    {
        if (cubePrefab == null)
        {
            Debug.LogError("[ARPreviewController] Cube Prefab이 할당되지 않았습니다!");
            return;
        }

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ARPreviewController] Play 모드에서만 전체 테스트 가능합니다!");
            return;
        }

        Texture2D tex = testTexture;
        if (tex == null)
        {
            // 테스트 텍스처 없으면 컬러 텍스처 생성
            tex = new Texture2D(256, 256);
            Color[] colors = new Color[256 * 256];
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    float r = (float)x / 256f;
                    float g = (float)y / 256f;
                    float b = 0.5f;
                    colors[y * 256 + x] = new Color(r, g, b, 1f);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
        }

        StartPreview(
            tex,
            onConfirmCallback: () => Debug.Log("[ARPreviewController] Test: Confirm clicked"),
            onCancelCallback: () => Debug.Log("[ARPreviewController] Test: Cancel clicked")
        );
    }

    [ContextMenu("Clear Test Cube")]
    public void ClearTestCube()
    {
        CleanupCube();

    }
#endif

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);

        if (loadingTextCoroutine != null)
            StopCoroutine(loadingTextCoroutine);

        CleanupSpinner();
        CleanupCube();
        CleanupSpawnGuide();

        CleanupSpotlight();

        EnhancedTouchSupport.Disable();

        // dimOverlayObject는 씬 오브젝트이므로 Destroy하지 않고 비활성화만
        if (dimOverlayObject != null)
            dimOverlayObject.SetActive(false);
    }
}
