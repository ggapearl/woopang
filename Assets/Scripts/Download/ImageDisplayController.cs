using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ImageDisplayController : MonoBehaviour
{
    public Renderer cubeRenderer;
    public DoubleTap3D doubleTap3DScript;
    public GameObject loadingSpinnerPrefab;
    public float spinnerDuration = 10f;
    
    [Header("Debug")]
    public bool testLoadOnStart = false;
    public string testImageUrl = "uploads/20250220_115747_집/main.jpg";
    
    private List<Sprite> loadedSprites = new List<Sprite>();
    private Texture2D baseMapTexture;
    private GameObject currentSpinner;
    private Vector3 originalScale; // 원래 크기 저장
    
    private Coroutine currentBaseMapCoroutine;
    private Coroutine currentSubPhotoCoroutine;

    void Start()
    {
        // ⭐ originalScale 초기화 (PopUpAnimation용)
        originalScale = transform.localScale;
        Debug.Log($"[SPINNER] Start() - originalScale 초기화: {originalScale}");

        // GLB 모델 등 동적으로 렌더러가 생성되는 경우를 위해 에러 처리 완화
        if (cubeRenderer == null)
        {
            Debug.LogWarning("[ImageDisplayController] cubeRenderer가 할당되지 않았습니다!");
        }

        if (doubleTap3DScript == null)
        {
            Debug.LogError("[ImageDisplayController] doubleTap3DScript가 할당되지 않았습니다 - Sample_Prefab에 DoubleTap3D 추가 필요!");
        }

        if (loadingSpinnerPrefab == null)
        {
            Debug.LogWarning("[ImageDisplayController] loadingSpinnerPrefab이 할당되지 않았습니다!");
        }

        if (testLoadOnStart)
        {
            Debug.Log("[ImageDisplayController] 테스트 로딩 시작");
            SetBaseMap(testImageUrl);
        }
    }

    // 메인 사진 설정
    public void SetBaseMap(string imageUrl)
    {
        if (!enabled) return;

        ShowSpinner(true);
        StartCoroutine(LoadBaseMapTexture(imageUrl));
    }

    private IEnumerator LoadBaseMapTexture(string imageUrl)
    {
        float startTime = Time.time;
        string fullUrl = imageUrl.StartsWith("http") ? imageUrl : "https://woopang.com/" + imageUrl.Replace("\\", "/");

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
        {
            request.timeout = 20;

            try
            {
                yield return request.SendWebRequest();

                float elapsed = Time.time - startTime;
                if (elapsed < spinnerDuration) yield return new WaitForSeconds(spinnerDuration - elapsed);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D newTexture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                    if (newTexture != null)
                    {
                        if (baseMapTexture != null) Destroy(baseMapTexture);
                        baseMapTexture = newTexture;

                        if (cubeRenderer != null)
                        {
                            if (cubeRenderer.material.HasProperty("_BaseMap")) cubeRenderer.material.SetTexture("_BaseMap", baseMapTexture);
                            else if (cubeRenderer.material.HasProperty("_MainTex")) cubeRenderer.material.SetTexture("_MainTex", baseMapTexture);
                        }
                    }
                }
                else
                {
                    Debug.LogError($"[ImageDisplayController] 로딩 실패: {request.error} ({fullUrl})");
                }
            }
            finally
            {
                ShowSpinner(false);
                currentBaseMapCoroutine = null;
            }
        }
    }

    private void ShowSpinner(bool show)
    {
        Debug.Log($"[SPINNER] ShowSpinner({show}) - prefab={loadingSpinnerPrefab != null}, cubeRenderer={cubeRenderer != null}");

        // 스피너 생성
        if (show && currentSpinner == null && loadingSpinnerPrefab != null)
        {
            currentSpinner = Instantiate(loadingSpinnerPrefab, transform);
            currentSpinner.transform.localPosition = Vector3.zero;
            Debug.Log($"[SPINNER] 스피너 생성 완료: {currentSpinner.name}");
        }
        else if (show && loadingSpinnerPrefab == null)
        {
            Debug.LogWarning($"[SPINNER] loadingSpinnerPrefab이 null입니다!");
        }

        // cubeRenderer만 제어
        if (cubeRenderer != null)
        {
            cubeRenderer.enabled = !show;
            Debug.Log($"[SPINNER] cubeRenderer.enabled = {cubeRenderer.enabled} (GameObject active={cubeRenderer.gameObject.activeSelf})");
        }
        else
        {
            Debug.LogWarning($"[SPINNER] cubeRenderer가 null입니다. Cube 자식을 찾습니다.");
            // fallback: Cube 자식 찾기
            Transform cubeChild = transform.Find("Cube");
            if (cubeChild != null)
            {
                MeshRenderer meshRenderer = cubeChild.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = !show;
                    Debug.Log($"[SPINNER] Cube MeshRenderer.enabled = {meshRenderer.enabled}");
                }
                else
                {
                    Debug.LogWarning($"[SPINNER] Cube에 MeshRenderer가 없습니다!");
                }
            }
            else
            {
                Debug.LogWarning($"[SPINNER] Cube 자식을 찾을 수 없습니다!");
            }
        }

        // 스피너 켜기/끄기
        if (currentSpinner != null)
        {
            currentSpinner.SetActive(show);
            Debug.Log($"[SPINNER] 스피너 활성화 = {show}");
        }

        // 로딩 완료 시 등장 애니메이션
        if (!show)
        {
            Debug.Log($"[SPINNER] 팝업 애니메이션 시작");
            StartCoroutine(PopUpAnimation());
        }
    }

    private IEnumerator PopUpAnimation()
    {
        float duration = 0.6f; // 0.4 → 0.6초로 증가 (더 눈에 띄게)
        float elapsed = 0f;

        // 시작: 아주 작은 크기에서 시작 (0.1 배)
        transform.localScale = originalScale * 0.1f;
        Debug.Log($"[SPINNER] 애니메이션 시작 - 초기 스케일: {transform.localScale}");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Elastic Back Out Easing (통통 튀는 효과)
            float s = 1.70158f * 1.525f; // 더 강한 오버슈트
            float scale;

            if (t < 0.5f)
            {
                // 전반부: 빠르게 커짐
                float t2 = t * 2f;
                scale = 0.1f + (t2 * t2 * 0.9f);
            }
            else
            {
                // 후반부: 살짝 튀어나왔다가 원래 크기로
                float t2 = (t - 0.5f) * 2f;
                scale = 1.0f + ((t2 - 1) * t2 * ((s + 1) * (t2 - 1) + s));
            }

            if (scale < 0.1f) scale = 0.1f; // 최소 크기 보장

            transform.localScale = originalScale * scale;
            yield return null;
        }

        transform.localScale = originalScale;
        Debug.Log($"[SPINNER] 애니메이션 완료 - 최종 스케일: {transform.localScale}");
    }

    // 서브 사진 설정
    public void SetSubPhotos(List<string> subPhotoUrls)
    {
        if (!enabled) return;

        if (subPhotoUrls == null || subPhotoUrls.Count == 0)
        {
            Debug.LogWarning("[ImageDisplayController] 서브 사진 URL이 비어 있습니다. 기본 이미지 적용");
            if (doubleTap3DScript != null)
            {
                Sprite defaultSprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 100, 100), new Vector2(0.5f, 0.5f));
                doubleTap3DScript.SetImageSprites(new List<Sprite> { defaultSprite });
                loadedSprites.Add(defaultSprite); // 해제를 위해 저장
            }
            return;
        }

        StartCoroutine(LoadSubPhotos(subPhotoUrls));
    }

    private IEnumerator LoadSubPhotos(List<string> subPhotoUrls)
    {
        // 기존 스프라이트 정리
        ClearSubPhotos();

        List<Sprite> spriteList = new List<Sprite>();

        foreach (string photoUrl in subPhotoUrls)
        {
            string fullUrl = photoUrl.StartsWith("http") ? photoUrl : "https://woopang.com/" + photoUrl.Replace("\\", "/");

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                    if (texture != null)
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        if (sprite != null)
                        {
                            spriteList.Add(sprite);
                            loadedSprites.Add(sprite);
                        }
                        else
                        {
                            Debug.LogWarning($"[ImageDisplayController] 스프라이트 생성 실패: {fullUrl}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ImageDisplayController] 텍스처 로드 실패: {fullUrl}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ImageDisplayController] 서브 사진 로드 실패: {fullUrl}, 오류: {request.error}");
                }
            }
        }

        if (spriteList.Count > 0)
        {
            if (doubleTap3DScript != null)
            {
                doubleTap3DScript.SetImageSprites(spriteList);
            }
        }
        else
        {
            Debug.LogWarning("[ImageDisplayController] 서브 사진 로드 실패, 기본 이미지 적용");
            if (doubleTap3DScript != null)
            {
                Sprite defaultSprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 100, 100), new Vector2(0.5f, 0.5f));
                doubleTap3DScript.SetImageSprites(new List<Sprite> { defaultSprite });
                loadedSprites.Add(defaultSprite); // 해제를 위해 저장
            }
        }
    }

    // 서브 사진만 정리
    private void ClearSubPhotos()
    {
        foreach (var sprite in loadedSprites)
        {
            if (sprite != null)
            {
                if (sprite.texture != null && sprite.texture != Texture2D.blackTexture)
                {
                    Destroy(sprite.texture);
                }
                Destroy(sprite);
            }
        }
        loadedSprites.Clear();

        if (doubleTap3DScript != null)
        {
            doubleTap3DScript.SetImageSprites(new List<Sprite>());
        }
    }

    // 모든 텍스처 해제
    public void ClearImages()
    {
        StopAllCoroutines();
        ShowSpinner(false);

        if (cubeRenderer != null && cubeRenderer.material.HasProperty("_MainTex"))
        {
            cubeRenderer.material.SetTexture("_MainTex", null);
        }

        if (baseMapTexture != null && baseMapTexture != Texture2D.blackTexture)
        {
            Destroy(baseMapTexture);
            baseMapTexture = null;
        }

        ClearSubPhotos();
    }

    void OnDestroy()
    {
        ClearImages(); // 컴포넌트 파괴 시 메모리 정리
    }
}