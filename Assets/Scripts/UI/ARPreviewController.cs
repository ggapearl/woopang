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
    [SerializeField] private float spinnerRotationSpeed = 30f; // 스피너 회전 속도 (도/초)
    [SerializeField] private float spinnerMinDuration = 1f; // 스피너 최소 표시 시간 (회전)
    [SerializeField] private float spinnerFadeDuration = 0.5f; // 스피너 페이드인/아웃 시간
    [SerializeField] private float cubeFadeInDuration = 1f; // 큐브 디졸브(페이드인) 시간

    private GameObject spawnedSpinner;
    private GameObject spawnedCube;
    private Action onConfirm;
    private Action onCancel;
    private Camera arCamera;

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

        // 초기 상태: 비활성화
        if (previewPanel != null)
            previewPanel.SetActive(false);
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

        // Cube 생성 및 배치
        SpawnCubeInFrontOfCamera(mainPhotoTexture);

        // UI 텍스트 업데이트
        UpdateLocalizedTexts();

        if (previewPanel != null)
            previewPanel.SetActive(true);
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
    /// </summary>
    private IEnumerator SpawnWithLoadingSpinner(Vector3 position, Quaternion rotation, Texture2D mainPhotoTexture)
    {
        // 1. 로딩 스피너 생성 (있는 경우)
        if (loadingSpinnerPrefab != null)
        {
            spawnedSpinner = Instantiate(loadingSpinnerPrefab, position, rotation);
            SetObjectAlpha(spawnedSpinner, 0f);

            // 스피너 페이드인
            yield return StartCoroutine(FadeObject(spawnedSpinner, 0f, 1f, spinnerFadeDuration));

            // 스피너 회전 (최소 1초)
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

            // 스피너 페이드아웃
            yield return StartCoroutine(FadeObject(spawnedSpinner, 1f, 0f, spinnerFadeDuration));
            CleanupSpinner();
        }
        else
        {
            // 스피너가 없으면 1초 대기
            yield return new WaitForSeconds(spinnerMinDuration);
        }

        // 2. 큐브 생성 및 페이드인
        spawnedCube = Instantiate(cubePrefab, position, rotation);

        // 텍스처 적용
        ApplyTextureToCube(mainPhotoTexture);

        // 큐브 초기 알파 0으로 설정
        SetObjectAlpha(spawnedCube, 0f);

        // 큐브 디졸브 페이드인
        yield return StartCoroutine(FadeObject(spawnedCube, 0f, 1f, cubeFadeInDuration));

        Debug.Log($"[ARPreviewController] Cube 생성 완료 - 위치: {position}, 회전: Y={spawnRotationY}");
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
                Color color = renderer.material.color;
                color.a = alpha;
                renderer.material.color = color;

                // _BaseColor도 설정 (URP 셰이더용)
                if (renderer.material.HasProperty("_BaseColor"))
                {
                    Color baseColor = renderer.material.GetColor("_BaseColor");
                    baseColor.a = alpha;
                    renderer.material.SetColor("_BaseColor", baseColor);
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
                {
                    newMat.SetTexture("_MainTex", mainPhotoTexture);
                }

                renderer.material = newMat;

                Debug.Log($"[ARPreviewController] 텍스처 적용 완료 - {renderer.gameObject.name}");
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
        // 메시지 텍스트: "이곳에 오브젝트를 추가하시겠습니까?"
        if (messageText != null)
        {
            messageText.text = GetLocalizedText("confirm_add_object");
        }
        // 버튼은 이미지로 사용하므로 텍스트 업데이트 불필요
    }

    private string GetLocalizedText(string key)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.GetText(key);

        // Fallback (LocalizationManager가 없을 때)
        switch (key)
        {
            case "confirm_add_object": return "Would you like to add an object here?";
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

        // 오브젝트 정리
        CleanupSpinner();
        CleanupCube();
    }
}
