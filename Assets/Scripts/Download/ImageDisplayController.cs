using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ImageDisplayController : MonoBehaviour
{
    public Renderer cubeRenderer;
    public DoubleTap3D doubleTap3DScript;
    public GameObject loadingSpinnerPrefab; // 로딩 스피너 프리팹
    
    private List<Sprite> loadedSprites = new List<Sprite>();
    private Texture2D baseMapTexture;
    private GameObject currentSpinner;

    void Start()
    {
        if (cubeRenderer == null)
        {
            // ...
        }
        // ...
    }

    // 메인 사진 설정
    public void SetBaseMap(string imageUrl)
    {
        if (!enabled) return;
        
        // 로딩 시작: 스피너 표시, 큐브 숨김
        ShowSpinner(true);
        
        StartCoroutine(LoadBaseMapTexture(imageUrl));
    }

    private IEnumerator LoadBaseMapTexture(string imageUrl)
    {
        float startTime = Time.time;
        string fullUrl = "https://woopang.com/" + imageUrl.Replace("\\", "/");

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
        {
            yield return request.SendWebRequest();

            // 최소 1초 대기 (로딩이 빨라도 1초간 스피너 보여줌)
            float elapsed = Time.time - startTime;
            if (elapsed < 1.0f) yield return new WaitForSeconds(1.0f - elapsed);

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
                Debug.LogError($"[ImageDisplayController] 로딩 실패: {fullUrl}");
                // 실패 시에도 일단 검은색(또는 기본값)으로 보여줌
                if (cubeRenderer != null)
                {
                     if (cubeRenderer.material.HasProperty("_BaseMap")) cubeRenderer.material.SetTexture("_BaseMap", Texture2D.blackTexture);
                     else if (cubeRenderer.material.HasProperty("_MainTex")) cubeRenderer.material.SetTexture("_MainTex", Texture2D.blackTexture);
                }
            }
        }
        
        // 로딩 완료: 스피너 숨김, 큐브 표시
        ShowSpinner(false);
    }

    private void ShowSpinner(bool show)
    {
        if (show)
        {
            if (currentSpinner == null && loadingSpinnerPrefab != null)
            {
                currentSpinner = Instantiate(loadingSpinnerPrefab, transform);
                currentSpinner.transform.localPosition = Vector3.zero;
            }
            if (currentSpinner != null) currentSpinner.SetActive(true);
            if (cubeRenderer != null) cubeRenderer.enabled = false;
        }
        else
        {
            if (currentSpinner != null) currentSpinner.SetActive(false);
            if (cubeRenderer != null) cubeRenderer.enabled = true;
        }
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
            string fullUrl = "https://woopang.com/" + photoUrl;

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