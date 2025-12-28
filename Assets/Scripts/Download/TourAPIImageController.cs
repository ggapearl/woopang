using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class TourAPIImageController : MonoBehaviour
{
    [SerializeField] private DoubleTap3D doubleTap3DScript;

    private readonly List<Sprite> loadedSprites = new List<Sprite>(100);
    private readonly List<Sprite> spriteList = new List<Sprite>(10);
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(100);

    private void Awake()
    {
        if (doubleTap3DScript == null)
        {
            enabled = false;
            return;
        }
    }

    public void SetTourImages(List<string> imageUrls)
    {
        if (imageUrls != null && imageUrls.Count > 0)
        {
            StartCoroutine(LoadTourImages(imageUrls));
        }
    }

    private IEnumerator LoadTourImages(List<string> imageUrls)
    {
        spriteList.Clear();

        foreach (string url in imageUrls)
        {
            if (string.IsNullOrEmpty(url)) continue;

            if (spriteCache.ContainsKey(url))
            {
                spriteList.Add(spriteCache[url]);
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
                        spriteCache[url] = sprite;
                    }
                }
            }
        }

        if (doubleTap3DScript != null)
        {
            doubleTap3DScript.SetImageSprites(spriteList);
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
    }
}