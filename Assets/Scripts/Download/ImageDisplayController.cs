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
        // GLB 모델 등 동적으로 렌더러가 생성되는 경우를 위해 에러 처리 완화
        if (cubeRenderer == null)
        {
            // Debug.LogWarning("[ImageDisplayController] cubeRenderer가 할당되지 않았습니다!");
        }
        
        if (doubleTap3DScript == null)
        {
            Debug.LogError("[ImageDisplayController] doubleTap3DScript가 할당되지 않았습니다 - Sample_Prefab에 DoubleTap3D 추가 필요!");
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
        
        // 로딩 시작: 스피너 표시, 큐브 숨김
        ShowSpinner(true); // ⭐ 스피너 활성화

        StartCoroutine(LoadBaseMapTexture(imageUrl));
    }

    private IEnumerator LoadBaseMapTexture(string imageUrl)
    {
        float startTime = Time.time;
        string fullUrl = imageUrl.StartsWith("http") ? imageUrl : "https://woopang.com/" + imageUrl.Replace("\\", "/");
        // Debug.Log($"[DEBUG_IMAGE] Loading BaseMap: {fullUrl}");

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
        {
            request.timeout = 20; // 20초 타임아웃
            
            try
            {
                yield return request.SendWebRequest();

                float elapsed = Time.time - startTime;
                Debug.Log($"[DEBUG_SPINNER] 로딩 완료. 경과: {elapsed:F2}s, 목표: {spinnerDuration}s, 추가 대기: {Mathf.Max(0, spinnerDuration - elapsed):F2}s");
                
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
                // 코루틴이 어떻게 끝나든 (성공, 실패, 중단) 스피너는 꺼야 함
                Debug.Log("[DEBUG_SPINNER] 로딩 코루틴 종료 -> 스피너 끔 (finally)");
                ShowSpinner(false); // ⭐ 스피너 비활성화
                currentBaseMapCoroutine = null;
            }
        }
    }

    private void ShowSpinner(bool show)
    {
        Debug.Log($"[DEBUG_SPINNER] ShowSpinner({show})");

        // 스피너 생성 (없으면)
        if (show && currentSpinner == null && loadingSpinnerPrefab != null)
        {
            currentSpinner = Instantiate(loadingSpinnerPrefab, transform);
            currentSpinner.transform.localPosition = Vector3.zero;
        }

        // 모든 렌더러 끄기/켜기 (스피너 제외)
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            // 스피너의 렌더러는 건너뜀
            if (currentSpinner != null && r.transform.IsChildOf(currentSpinner.transform)) continue;
            
            Debug.Log($"[DEBUG_SPINNER] Renderer {r.name} 상태 변경: {!show}");
            
            // 최상위 오브젝트(나 자신)라면 Renderer만 끄고, 자식이면 오브젝트를 끔
            if (r.gameObject == this.gameObject)
            {
                r.enabled = !show;
            }
            else
            {
                r.gameObject.SetActive(!show);
            }
        }

        // 스피너 켜기/끄기
        if (currentSpinner != null) currentSpinner.SetActive(show);
        
        // 로딩 완료 시(show == false) 등장 애니메이션
        if (!show)
        {
            StartCoroutine(PopUpAnimation());
        }
    }

    private IEnumerator PopUpAnimation()
    {
        float duration = 0.4f;
        float elapsed = 0f;
        
        // 시작: 크기 0
        transform.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // BackOut Easing (살짝 튀어나오는 효과)
            float s = 1.70158f;
            float scale = ((t - 1) * t * ((s + 1) * (t - 1) + s) + 1);
            
            if (scale < 0) scale = 0; // 초반에 음수 방지
            
            transform.localScale = originalScale * scale;
            yield return null;
        }
        transform.localScale = originalScale;
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
        StopAllCoroutines(); // 중요: 진행 중인 로딩 중지
        
        // 코루틴이 중단되면 ShowSpinner(false)가 호출되지 않아 큐브가 꺼진 채로 남을 수 있음.
        // 따라서 여기서 강제로 초기화해줘야 함.
        ShowSpinner(false); // ⭐ 스피너 강제 비활성화

        if (cubeRenderer != null)
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