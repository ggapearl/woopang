using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ImageDisplayController : MonoBehaviour
{
    public Renderer cubeRenderer;
    public DoubleTap3D doubleTap3DScript;
    private List<Sprite> loadedSprites = new List<Sprite>();
    private Texture2D baseMapTexture;

    void Start()
    {
        if (cubeRenderer == null)
        {
            Debug.LogError("[ImageDisplayController] cubeRenderer가 할당되지 않았습니다!");
            enabled = false;
            return;
        }
        if (doubleTap3DScript == null)
        {
            Debug.LogError("[ImageDisplayController] doubleTap3DScript가 할당되지 않았습니다 - Sample_Prefab에 DoubleTap3D 추가 필요!");
        }
    }

    // 메인 사진 설정
    public void SetBaseMap(string imageUrl)
    {
        if (!enabled) return;
        StartCoroutine(LoadBaseMapTexture(imageUrl));
    }

    private IEnumerator LoadBaseMapTexture(string imageUrl)
    {
        string fullUrl = "https://woopang.com/" + imageUrl;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D newTexture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                if (newTexture != null)
                {
                    // 기존 텍스처 해제
                    if (baseMapTexture != null)
                    {
                        Destroy(baseMapTexture);
                    }
                    baseMapTexture = newTexture;

                    // T5EdgeLine 셰이더는 _BaseMap 사용, 기본 셰이더는 _MainTex 사용
                    if (cubeRenderer != null)
                    {
                        if (cubeRenderer.material.HasProperty("_BaseMap"))
                        {
                            cubeRenderer.material.SetTexture("_BaseMap", baseMapTexture);
                            Debug.Log($"[ImageDisplayController] _BaseMap 텍스처 설정 완료: {imageUrl}");
                        }
                        else if (cubeRenderer.material.HasProperty("_MainTex"))
                        {
                            cubeRenderer.material.SetTexture("_MainTex", baseMapTexture);
                            Debug.Log($"[ImageDisplayController] _MainTex 텍스처 설정 완료: {imageUrl}");
                        }
                        else
                        {
                            Debug.LogError("[ImageDisplayController] _BaseMap/_MainTex 속성이 없습니다!");
                        }
                    }
                }
                else
                {
                    Debug.LogError("[ImageDisplayController] 메인 사진 텍스처가 null입니다.");
                }
            }
            else
            {
                Debug.LogError($"[ImageDisplayController] 메인 사진 로딩 실패: {fullUrl}, 오류: {request.error}");
                if (cubeRenderer != null)
                {
                    if (cubeRenderer.material.HasProperty("_BaseMap"))
                    {
                        cubeRenderer.material.SetTexture("_BaseMap", Texture2D.blackTexture);
                    }
                    else if (cubeRenderer.material.HasProperty("_MainTex"))
                    {
                        cubeRenderer.material.SetTexture("_MainTex", Texture2D.blackTexture);
                    }
                }
            }
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
        if (cubeRenderer != null)
        {
            if (cubeRenderer.material.HasProperty("_BaseMap"))
            {
                cubeRenderer.material.SetTexture("_BaseMap", null);
            }
            else if (cubeRenderer.material.HasProperty("_MainTex"))
            {
                cubeRenderer.material.SetTexture("_MainTex", null);
            }
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