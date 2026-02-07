using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

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
    [SerializeField] private float spawnDistance = 4f; // 카메라로부터 거리 (m)
    [SerializeField] private float spawnHeightOffset = -1f; // 카메라 기준 높이 오프셋 (m)
    [SerializeField] private float spawnRotationY = 150f; // Y축 회전값 (살짝 옆으로)

    [Header("Loading Spinner")]
    [SerializeField] private GameObject loadingSpinnerPrefab; // 3D 로딩 스피너 프리팹
    [SerializeField] private float spinnerScale = 1.5f; // 스피너 스케일 (기본 1.5배)
    [SerializeField] private float spinnerRotationSpeed = 30f; // 스피너 회전 속도 (도/초)
    [SerializeField] private float spinnerMinDuration = 2f; // 스피너 최소 표시 시간 (회전)
    [SerializeField] private float spinnerFadeDuration = 1.5f; // 스피너 페이드아웃 시간 (큐브와 겹침)
    [SerializeField] private float cubeFadeInDuration = 1.5f; // 큐브 디졸브(페이드인) 시간
    [SerializeField] private float overlapDuration = 1.5f; // 스피너↔큐브 겹치는 시간

    [Header("Transition Effect")]
    [SerializeField] private Sprite transitionSprite; // 스피너 → 큐브 전환 시 표시할 스프라이트 (Inspector에서 연결)
    [SerializeField] private float transitionScaleMultiplier = 2f; // 전환 이미지 커지는 배율
    [SerializeField] private float transitionDuration = 0.6f; // 전환 애니메이션 시간

    private Image transitionImage; // 런타임 생성되는 전환 이미지

    [Header("Loading Text Animation")]
    [SerializeField] private float dotAnimationSpeed = 0.4f; // 점 애니메이션 속도 (초)

    private GameObject spawnedSpinner;
    private GameObject spawnedCube;
    private Action onConfirm;
    private Action onCancel;
    private Camera arCamera;
    private Coroutine loadingTextCoroutine;
    private bool isLoadingComplete = false;

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        // AR Camera 찾기
        arCamera = Camera.main;
        if (arCamera == null)
        {
            Debug.LogWarning("[ARPreviewController] Main Camera를 찾을 수 없습니다.");
        }

        // 전환 이미지 동적 생성 (스프라이트가 있는 경우)
        CreateTransitionImage();

        // 초기 상태: 비활성화
        if (previewPanel != null)
            previewPanel.SetActive(false);
    }

    /// <summary>
    /// 전환 스프라이트를 위한 Image 오브젝트 동적 생성
    /// </summary>
    private void CreateTransitionImage()
    {
        if (transitionSprite == null || previewPanel == null) return;

        // Canvas 찾기
        Canvas canvas = previewPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }
        if (canvas == null) return;

        // 전환 이미지 GameObject 생성
        GameObject transitionObj = new GameObject("TransitionImage");
        transitionObj.transform.SetParent(canvas.transform, false);

        // RectTransform 설정 (화면 중앙)
        RectTransform rectTransform = transitionObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;

        // 스프라이트 크기에 맞게 설정
        rectTransform.sizeDelta = new Vector2(transitionSprite.rect.width, transitionSprite.rect.height);

        // Image 컴포넌트 추가
        transitionImage = transitionObj.AddComponent<Image>();
        transitionImage.sprite = transitionSprite;
        transitionImage.preserveAspect = true;
        transitionImage.raycastTarget = false;

        // 초기 상태: 숨김
        transitionObj.SetActive(false);

        Debug.Log($"[ARPreviewController] 전환 이미지 생성 완료: {transitionSprite.name}");
    }

    /// <summary>
    /// AR Preview 모드 시작 (확인/취소 콜백 포함)
    /// </summary>
    /// <param name="mainPhotoTexture">적용할 Main 사진 텍스처</param>
    /// <param name="onConfirmCallback">확인 버튼 클릭 시 콜백 (실제 업로드 진행)</param>
    /// <param name="onCancelCallback">취소 버튼 클릭 시 콜백 (UploadPage 복귀)</param>
    public void StartPreview(Texture2D mainPhotoTexture, Action onConfirmCallback, Action onCancelCallback = null)
    {
        onConfirm = onConfirmCallback;
        onCancel = onCancelCallback;
        isLoadingComplete = false;

        // 전환 이미지 초기화 (숨김)
        if (transitionImage != null)
        {
            transitionImage.gameObject.SetActive(false);
            transitionImage.transform.localScale = Vector3.one;
            SetImageAlpha(transitionImage, 1f);
        }

        // 로딩 텍스트 애니메이션 시작
        if (loadingTextCoroutine != null)
            StopCoroutine(loadingTextCoroutine);
        loadingTextCoroutine = StartCoroutine(AnimateLoadingText());

        if (previewPanel != null)
            previewPanel.SetActive(true);

        // Cube 생성 및 배치
        SpawnCubeInFrontOfCamera(mainPhotoTexture);
    }

    /// <summary>
    /// 카메라 정면에 Cube 생성 (로딩 스피너 → 큐브 전환)
    /// </summary>
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

        // 기존 오브젝트 제거
        CleanupSpinner();
        CleanupCube();

        // 카메라 정면 위치 계산
        Vector3 cameraForward = arCamera.transform.forward;
        cameraForward.y = 0; // 수평 방향만 유지 (Y축 제거)
        cameraForward.Normalize();

        Vector3 spawnPosition = arCamera.transform.position
            + cameraForward * spawnDistance
            + Vector3.up * spawnHeightOffset;

        Quaternion spawnRotation = Quaternion.Euler(0f, spawnRotationY, 0f);

        // 로딩 스피너 → 큐브 전환 코루틴 시작
        StartCoroutine(SpawnWithLoadingSpinner(spawnPosition, spawnRotation, mainPhotoTexture));
    }

    /// <summary>
    /// 로딩 스피너 표시 후 큐브로 전환하는 코루틴
    /// 스피너와 큐브가 겹치면서 자연스럽게 디졸브 전환
    /// </summary>
    private IEnumerator SpawnWithLoadingSpinner(Vector3 position, Quaternion rotation, Texture2D mainPhotoTexture)
    {
        // 1. 로딩 스피너 생성 (있는 경우)
        if (loadingSpinnerPrefab != null)
        {
            spawnedSpinner = Instantiate(loadingSpinnerPrefab, position, rotation);
            spawnedSpinner.SetActive(true);

            // 스피너 스케일 적용
            spawnedSpinner.transform.localScale = Vector3.one * spinnerScale;
            SetObjectAlpha(spawnedSpinner, 0f);

            // 스피너 페이드인 (빠르게)
            yield return StartCoroutine(FadeObject(spawnedSpinner, 0f, 1f, 0.5f));

            // 스피너 회전 (최소 시간)
            float spinTime = 0f;
            while (spinTime < spinnerMinDuration)
            {
                if (spawnedSpinner != null)
                {
                    spawnedSpinner.transform.Rotate(Vector3.up, spinnerRotationSpeed * Time.deltaTime);
                }
                spinTime += Time.deltaTime;
                yield return null;
            }

            // 2. 큐브 미리 생성 (투명 상태)
            spawnedCube = Instantiate(cubePrefab, position, rotation);
            spawnedCube.SetActive(true);
            ApplyTextureToCube(mainPhotoTexture);
            SetObjectAlpha(spawnedCube, 0f);

            // 3. 스피너 페이드아웃 + 큐브 페이드인 동시 진행 (겹침 효과)
            StartCoroutine(FadeObject(spawnedSpinner, 1f, 0f, overlapDuration));
            StartCoroutine(ContinueSpinnerRotation(overlapDuration)); // 페이드아웃 중에도 회전 유지

            // 전환 이미지 효과 (커지면서 페이드아웃)
            if (transitionImage != null)
            {
                StartCoroutine(PlayTransitionEffect());
            }

            yield return StartCoroutine(FadeObject(spawnedCube, 0f, 1f, cubeFadeInDuration));

            // 스피너 정리
            CleanupSpinner();
        }
        else
        {
            // 스피너가 없으면 대기 후 큐브만 생성
            yield return new WaitForSeconds(spinnerMinDuration);

            spawnedCube = Instantiate(cubePrefab, position, rotation);
            spawnedCube.SetActive(true);
            ApplyTextureToCube(mainPhotoTexture);
            SetObjectAlpha(spawnedCube, 0f);

            yield return StartCoroutine(FadeObject(spawnedCube, 0f, 1f, cubeFadeInDuration));
        }

        // 4. 로딩 완료 → 텍스트 전환
        isLoadingComplete = true;
        yield return StartCoroutine(TransitionToConfirmText());
    }

    /// <summary>
    /// 페이드아웃 중에도 스피너 회전 유지
    /// </summary>
    private IEnumerator ContinueSpinnerRotation(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && spawnedSpinner != null)
        {
            spawnedSpinner.transform.Rotate(Vector3.up, spinnerRotationSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 로딩 텍스트 점 애니메이션 ("AR 오브젝트 생성 중." → ".." → "...")
    /// </summary>
    private IEnumerator AnimateLoadingText()
    {
        string baseText = GetLocalizedText("creating_ar_object"); // "AR 오브젝트 생성 중"
        int dotCount = 0;

        while (!isLoadingComplete)
        {
            dotCount = (dotCount % 3) + 1;
            string dots = new string('.', dotCount);

            if (messageText != null)
            {
                messageText.text = baseText + dots;
            }

            yield return new WaitForSeconds(dotAnimationSpeed);
        }
    }

    /// <summary>
    /// 로딩 완료 후 확인 텍스트로 전환 (효과 포함)
    /// </summary>
    private IEnumerator TransitionToConfirmText()
    {
        if (messageText == null) yield break;

        // 텍스트 페이드아웃
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

        // 텍스트 변경
        messageText.text = GetLocalizedText("confirm_add_object");

        // 약간의 스케일 효과
        Vector3 originalScale = messageText.transform.localScale;
        messageText.transform.localScale = originalScale * 0.8f;

        // 텍스트 페이드인 + 스케일 업
        float fadeInDuration = 0.4f;
        elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;
            // EaseOutBack 효과
            float easeT = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);

            textCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            messageText.transform.localScale = Vector3.Lerp(originalScale * 0.8f, originalScale, easeT);
            yield return null;
        }

        textCanvasGroup.alpha = 1f;
        messageText.transform.localScale = originalScale;
    }

    /// <summary>
    /// 전환 이미지 효과 재생 (커지면서 페이드아웃)
    /// </summary>
    private IEnumerator PlayTransitionEffect()
    {
        if (transitionImage == null) yield break;

        // 초기 상태 설정
        transitionImage.gameObject.SetActive(true);
        transitionImage.transform.localScale = Vector3.one;
        SetImageAlpha(transitionImage, 1f);

        float elapsed = 0f;
        Vector3 startScale = Vector3.one;
        Vector3 endScale = Vector3.one * transitionScaleMultiplier;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            // EaseOutQuad 곡선
            float easeT = 1f - (1f - t) * (1f - t);

            // 스케일 증가
            transitionImage.transform.localScale = Vector3.Lerp(startScale, endScale, easeT);

            // 알파 감소 (후반부에 더 빠르게)
            float alphaT = Mathf.Pow(t, 0.7f);
            SetImageAlpha(transitionImage, 1f - alphaT);

            yield return null;
        }

        // 완료 후 숨김
        transitionImage.gameObject.SetActive(false);
        transitionImage.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Image 알파값 설정
    /// </summary>
    private void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    /// <summary>
    /// 오브젝트의 알파값 설정
    /// </summary>
    private void SetObjectAlpha(GameObject obj, float alpha)
    {
        if (obj == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                // _Color 속성이 있는 경우에만 설정 (Standard 셰이더 등)
                if (renderer.material.HasProperty("_Color"))
                {
                    Color color = renderer.material.color;
                    color.a = alpha;
                    renderer.material.color = color;
                }

                // _BaseColor도 설정 (URP 셰이더용)
                if (renderer.material.HasProperty("_BaseColor"))
                {
                    Color baseColor = renderer.material.GetColor("_BaseColor");
                    baseColor.a = alpha;
                    renderer.material.SetColor("_BaseColor", baseColor);
                }

                // _Alpha 속성이 있는 경우 (커스텀 셰이더용)
                if (renderer.material.HasProperty("_Alpha"))
                {
                    renderer.material.SetFloat("_Alpha", alpha);
                }
            }
        }
    }

    /// <summary>
    /// 오브젝트 페이드 애니메이션
    /// </summary>
    private IEnumerator FadeObject(GameObject obj, float fromAlpha, float toAlpha, float duration)
    {
        if (obj == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentAlpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            SetObjectAlpha(obj, currentAlpha);
            yield return null;
        }

        SetObjectAlpha(obj, toAlpha);
    }

    /// <summary>
    /// 스피너 정리
    /// </summary>
    private void CleanupSpinner()
    {
        if (spawnedSpinner != null)
        {
            Destroy(spawnedSpinner);
            spawnedSpinner = null;
        }
    }

    /// <summary>
    /// Cube에 Main 사진 텍스처 적용
    /// </summary>
    private void ApplyTextureToCube(Texture2D mainPhotoTexture)
    {
        if (spawnedCube == null || mainPhotoTexture == null)
            return;

        // Cube의 MeshRenderer 찾기
        MeshRenderer[] renderers = spawnedCube.GetComponentsInChildren<MeshRenderer>();

        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                // Material 복사 (원본 Material 보호)
                Material newMat = new Material(renderer.material);

                // T5EdgeLine 셰이더용 텍스처 적용
                if (newMat.HasProperty("_BaseMap"))
                {
                    newMat.SetTexture("_BaseMap", mainPhotoTexture);
                }

                // 기본 텍스처도 적용 (fallback)
                if (newMat.HasProperty("_MainTex"))
                    newMat.SetTexture("_MainTex", mainPhotoTexture);

                renderer.material = newMat;
            }
        }
    }

    private void OnConfirmButtonClicked()
    {
        // Cube 제거
        CleanupCube();

        // UI 숨김
        if (previewPanel != null)
            previewPanel.SetActive(false);

        // 확인 콜백 실행 (실제 업로드 진행)
        onConfirm?.Invoke();
        ClearCallbacks();
    }

    private void OnCancelButtonClicked()
    {
        // Cube 제거
        CleanupCube();

        // UI 숨김
        if (previewPanel != null)
            previewPanel.SetActive(false);

        // 취소 콜백 실행 (UploadPage 복귀)
        onCancel?.Invoke();
        ClearCallbacks();
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

    private void UpdateLocalizedTexts()
    {
        // 로딩 시작 시에는 로딩 텍스트로 시작 (AnimateLoadingText에서 처리)
        // 완료 후 confirm_add_object로 전환됨
    }

    private string GetLocalizedText(string key)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.GetText(key);

        // Fallback (LocalizationManager가 없을 때)
        switch (key)
        {
            case "confirm_add_object": return "Would you like to add an object here?";
            case "creating_ar_object": return "Creating AR object";
            case "confirm": return "Confirm";
            case "cancel": return "Cancel";
            default: return key;
        }
    }

    /// <summary>
    /// 외부에서 Cube Prefab 설정
    /// </summary>
    public void SetCubePrefab(GameObject prefab)
    {
        cubePrefab = prefab;
    }

#if UNITY_EDITOR
    [Header("Editor Test")]
    [SerializeField] private Texture2D testTexture; // 테스트용 텍스처

    /// <summary>
    /// 에디터에서 AR Preview 테스트 (Inspector 컨텍스트 메뉴)
    /// </summary>
    [ContextMenu("Test AR Preview (Editor)")]
    public void TestARPreviewInEditor()
    {
        if (cubePrefab == null)
        {
            Debug.LogError("[ARPreviewController] Cube Prefab이 할당되지 않았습니다!");
            return;
        }

        // Scene View 카메라 또는 Main Camera 사용
        Camera testCamera = UnityEditor.SceneView.lastActiveSceneView?.camera;
        if (testCamera == null)
        {
            testCamera = Camera.main;
        }

        if (testCamera == null)
        {
            Debug.LogError("[ARPreviewController] 테스트용 카메라를 찾을 수 없습니다!");
            return;
        }

        // 기존 큐브 제거
        CleanupCube();

        // 카메라 정면에 큐브 생성
        Vector3 cameraForward = testCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 spawnPosition = testCamera.transform.position
            + cameraForward * spawnDistance
            + Vector3.up * spawnHeightOffset;

        // 고정 Y축 회전 적용
        Quaternion spawnRotation = Quaternion.Euler(0f, spawnRotationY, 0f);
        spawnedCube = Instantiate(cubePrefab, spawnPosition, spawnRotation);

        // 테스트 텍스처 적용
        if (testTexture != null)
        {
            ApplyTextureToCube(testTexture);
        }

        Debug.Log($"<color=#00FF00>[ARPreviewController] 에디터 테스트 - 큐브 생성 위치: {spawnPosition}</color>");
        Debug.Log($"<color=#00FF00>  - 거리: {spawnDistance}m, 높이: {spawnHeightOffset}m, 회전Y: {spawnRotationY}</color>");

        // Selection 변경하여 Scene View에서 확인
        UnityEditor.Selection.activeGameObject = spawnedCube;
        UnityEditor.SceneView.lastActiveSceneView?.Frame(new Bounds(spawnPosition, Vector3.one * 2f), false);
    }

    /// <summary>
    /// 에디터에서 생성된 테스트 큐브 제거
    /// </summary>
    [ContextMenu("Clear Test Cube")]
    public void ClearTestCube()
    {
        CleanupCube();
        Debug.Log("[ARPreviewController] 테스트 큐브 제거됨");
    }
#endif

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);

        // 코루틴 정리
        if (loadingTextCoroutine != null)
            StopCoroutine(loadingTextCoroutine);

        // 오브젝트 정리
        CleanupSpinner();
        CleanupCube();

        // 동적 생성된 전환 이미지 정리
        if (transitionImage != null)
            Destroy(transitionImage.gameObject);
    }
}
