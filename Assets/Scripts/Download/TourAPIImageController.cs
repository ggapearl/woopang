using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Text;

public class TourAPIImageController : MonoBehaviour
{
    [SerializeField] private DoubleTap3D doubleTap3DScript;
    
    public GameObject loadingSpinnerPrefab;
    public float spinnerDuration = 10f;
    private GameObject currentSpinner;
    
    private readonly List<Sprite> loadedSprites = new List<Sprite>(100); // 리스트 재사용
    private readonly List<Sprite> spriteList = new List<Sprite>(10); // 리스트 재사용
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(100); // 스프라이트 캐싱
    private StringBuilder debugMessages = new StringBuilder(); // 문자열 최적화
    private bool enableDebug = true; // 디버그 로그 기본 활성화

    private void Awake()
    {
        if (doubleTap3DScript == null)
        {
            LogDebug("[TourAPIImageController] DoubleTap3DScript가 설정되지 않음!");
            enabled = false;
            return;
        }
    }

    // 기존 SetTourImages 유지
    public void SetTourImages(List<string> imageUrls)
    {
        if (imageUrls != null && imageUrls.Count > 0)
        {
            LogDebug($"[TourAPIImageController] SetTourImages 호출, URL 수: {imageUrls.Count}");
            StartCoroutine(LoadTourImages(imageUrls));
        }
        else
        {
            LogDebug("[TourAPIImageController] SetTourImages: imageUrls가 비어 있음");
        }
    }

    private IEnumerator LoadTourImages(List<string> imageUrls)
    {
        spriteList.Clear(); // 리스트 재사용
        LogDebug($"[TourAPIImageController] 이미지 로드 시작, URL 수: {imageUrls.Count}");

        foreach (string url in imageUrls)
        {
            if (string.IsNullOrEmpty(url))
            {
                LogDebug("[TourAPIImageController] 이미지 URL이 비어 있음");
                continue;
            }

            // 캐시 확인
            if (spriteCache.TryGetValue(url, out Sprite cachedSprite))
            {
                spriteList.Add(cachedSprite);
                LogDebug($"[TourAPIImageController] 캐시에서 이미지 로드: {url}");
                continue;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                    if (texture != null)
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        spriteList.Add(sprite);
                        loadedSprites.Add(sprite);
                        spriteCache[url] = sprite; // 캐시 추가
                        LogDebug($"[TourAPIImageController] 이미지 로드 성공: {url}");
                    }
                    else
                    {
                        LogDebug($"[TourAPIImageController] 이미지 텍스처 null: {url}");
                    }
                }
                else
                {
                    LogDebug($"[TourAPIImageController] 이미지 로드 실패: {request.error}, URL: {url}");
                }
            }
        }

        if (doubleTap3DScript != null)
        {
            doubleTap3DScript.SetImageSprites(spriteList);
            LogDebug($"[TourAPIImageController] 이미지 로드 완료, 스프라이트 수: {spriteList.Count}");
        }
        else
        {
            LogDebug("[TourAPIImageController] DoubleTap3DScript가 null임");
        }
    }

    public void ClearImages()
    {
        foreach (var sprite in loadedSprites)
        {
            if (sprite != null && sprite.texture != null)
            {
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }
        loadedSprites.Clear();
        spriteCache.Clear();
        if (doubleTap3DScript != null)
            doubleTap3DScript.SetImageSprites(new List<Sprite>());
        LogDebug("[TourAPIImageController] 이미지 정리 완료");
    }

    private void LogDebug(string message)
    {
        if (!enableDebug) return;
        Debug.Log(message);
        debugMessages.AppendLine(message);
        if (debugMessages.Length > 10000)
            debugMessages.Remove(0, debugMessages.Length - 5000);
    }
}