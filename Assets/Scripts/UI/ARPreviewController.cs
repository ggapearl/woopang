using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// AR 환경에서 Main 사진이 적용된 Cube를 미리보기하는 컨트롤러
/// </summary>
public class ARPreviewController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Text confirmButtonText;

    [Header("AR Settings")]
    [SerializeField] private GameObject cubePrefab; // 0000_Cube.prefab
    [SerializeField] private float spawnDistance = 4f; // 카메라로부터 거리 (m)
    [SerializeField] private float spawnHeightOffset = -0.5f; // 카메라 기준 높이 오프셋 (m)

    private GameObject spawnedCube;
    private Action onConfirm;
    private Camera arCamera;

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);

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
    /// AR Preview 모드 시작
    /// </summary>
    /// <param name="mainPhotoTexture">적용할 Main 사진 텍스처</param>
    /// <param name="onConfirmCallback">확인 버튼 클릭 시 콜백</param>
    public void StartPreview(Texture2D mainPhotoTexture, Action onConfirmCallback)
    {
        onConfirm = onConfirmCallback;

        // Cube 생성 및 배치
        SpawnCubeInFrontOfCamera(mainPhotoTexture);

        // UI 표시
        UpdateLocalizedTexts();

        if (previewPanel != null)
            previewPanel.SetActive(true);
    }

    /// <summary>
    /// 카메라 정면에 Cube 생성
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
            Debug.LogError("[ARPreviewController] AR Camera를 찾을 수 없습니다!");
            return;
        }

        // 기존 Cube 제거
        if (spawnedCube != null)
        {
            Destroy(spawnedCube);
        }

        // 카메라 정면 위치 계산 (약간 아래)
        Vector3 cameraForward = arCamera.transform.forward;
        cameraForward.y = 0; // 수평 방향만 유지 (Y축 제거)
        cameraForward.Normalize();

        Vector3 spawnPosition = arCamera.transform.position
            + cameraForward * spawnDistance
            + Vector3.up * spawnHeightOffset;

        // Cube 인스턴스화
        spawnedCube = Instantiate(cubePrefab, spawnPosition, Quaternion.identity);

        // Cube가 카메라를 향하도록 회전
        Vector3 lookDirection = arCamera.transform.position - spawnedCube.transform.position;
        lookDirection.y = 0; // 수평 회전만
        if (lookDirection != Vector3.zero)
        {
            spawnedCube.transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        // Main 사진 텍스처 적용
        ApplyTextureToCube(mainPhotoTexture);

        Debug.Log($"[ARPreviewController] Cube 생성 완료 - 위치: {spawnPosition}");
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
                // Material의 mainTexture에 사진 적용
                renderer.material.mainTexture = mainPhotoTexture;

                // 필요 시 Material 복사 (원본 Material 보호)
                renderer.material = new Material(renderer.material);
                renderer.material.mainTexture = mainPhotoTexture;

                Debug.Log($"[ARPreviewController] 텍스처 적용 완료 - {renderer.gameObject.name}");
            }
        }
    }

    private void OnConfirmButtonClicked()
    {
        // Cube 제거
        if (spawnedCube != null)
        {
            Destroy(spawnedCube);
            spawnedCube = null;
        }

        // UI 숨김
        if (previewPanel != null)
            previewPanel.SetActive(false);

        // 콜백 실행 (UploadPage 복귀)
        onConfirm?.Invoke();
        onConfirm = null;
    }

    private void UpdateLocalizedTexts()
    {
        if (confirmButtonText != null)
        {
            confirmButtonText.text = GetLocalizedText("confirm");
        }
    }

    private string GetLocalizedText(string key)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.GetText(key);

        // Fallback
        switch (key)
        {
            case "confirm": return "확인";
            default: return key;
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);

        // Cube 정리
        if (spawnedCube != null)
        {
            Destroy(spawnedCube);
        }
    }
}
