using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ImageDisplayController : MonoBehaviour
{
    public Renderer cubeRenderer;
    public DoubleTap3D doubleTap3DScript;

    [Header("텍스처 패딩 설정")]
    [Tooltip("텍스처 패딩 값 (0 = 패딩 없음, 0.05 = 5% 패딩)")]
    [Range(0f, 0.2f)]
    public float texturePadding = 0.05f;

    [Tooltip("패딩 영역 색상")]
    public Color paddingColor = new Color(0.05f, 0.05f, 0.05f, 1f);

    private List<Sprite> loadedSprites = new List<Sprite>();
    private Texture2D baseMapTexture;

    private Coroutine currentSubPhotoCoroutine;

    void Start()
    {
        // GLB 모델 등 동적으로 렌더러가 생성되는 경우를 위해 에러 처리 완화

        if (doubleTap3DScript == null)
        {
            Debug.LogError("[ImageDisplayController] doubleTap3DScript가 할당되지 않았습니다 - Sample_Prefab에 DoubleTap3D 추가 필요!");
        }
    }

    // 메인 사진 설정
    public void SetBaseMap(string imageUrl)
    {
        if (!enabled) return;

        // 큐브 숨기기 (로딩 중)
        if (cubeRenderer != null)
        {
            cubeRenderer.enabled = false;
        }

        StartCoroutine(LoadBaseMapTexture(imageUrl));
    }

    private IEnumerator LoadBaseMapTexture(string imageUrl)
    {
        string fullUrl = imageUrl.StartsWith("http") ? imageUrl : ApiConfig.MAIN_SERVER + "/" + imageUrl.Replace("\\", "/");

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
        {
            request.timeout = 20;

            yield return request.SendWebRequest();

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

                        // 패딩 설정 적용
                        ApplyPaddingSettings();

                        // 큐브 표시
                        cubeRenderer.enabled = true;
                    }
                }
            }
            else
            {
                Debug.LogError($"[ImageDisplayController] 로딩 실패: {request.error} ({fullUrl})");

                // 로딩 실패 시에도 큐브 표시
                if (cubeRenderer != null)
                {
                    cubeRenderer.enabled = true;
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
            if (doubleTap3DScript != null)
            {
                Sprite defaultSprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 100, 100), new Vector2(0.5f, 0.5f));
                doubleTap3DScript.SetImageSprites(new List<Sprite> { defaultSprite });
                loadedSprites.Add(defaultSprite);
            }
            return;
        }

        StartCoroutine(LoadSubPhotos(subPhotoUrls));
    }

    private IEnumerator LoadSubPhotos(List<string> subPhotoUrls)
    {
        ClearSubPhotos();

        List<Sprite> spriteList = new List<Sprite>();

        foreach (string photoUrl in subPhotoUrls)
        {
            string fullUrl = photoUrl.StartsWith("http") ? photoUrl : ApiConfig.MAIN_SERVER + "/" + photoUrl.Replace("\\", "/");

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
                    }
                }
            }
        }

        if (spriteList.Count > 0 && doubleTap3DScript != null)
        {
            doubleTap3DScript.SetImageSprites(spriteList);
        }
        else
        {
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

    // 패딩 설정을 머티리얼에 적용
    private void ApplyPaddingSettings()
    {
        if (cubeRenderer == null || cubeRenderer.material == null) return;

        Material mat = cubeRenderer.material;

        // 텍스처 패딩 값 적용
        if (mat.HasProperty("_TexturePadding"))
        {
            mat.SetFloat("_TexturePadding", texturePadding);
        }

        // 패딩 영역 색상 적용
        if (mat.HasProperty("_PaddingColor"))
        {
            mat.SetColor("_PaddingColor", paddingColor);
        }
    }

    // 런타임에서 패딩 값 변경
    public void SetTexturePadding(float padding)
    {
        texturePadding = Mathf.Clamp(padding, 0f, 0.2f);
        ApplyPaddingSettings();
    }

    // 런타임에서 패딩 색상 변경
    public void SetPaddingColor(Color color)
    {
        paddingColor = color;
        ApplyPaddingSettings();
    }
}