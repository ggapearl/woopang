using System.Collections; // 추가: IEnumerator를 사용하기 위해 필요
using UnityEngine;
using UnityEngine.Networking; // UnityWebRequest를 사용하기 위해 필요

public class TextureToMaterialLoader : MonoBehaviour
{
    [Tooltip("Enter the URL of the texture you want to load.")]
    public string textureURL = "https://example.com/your_texture.png";

    [Tooltip("Target Renderer to apply the material with the downloaded texture.")]
    public Renderer targetRenderer;

    private Material newMaterial; // 새로 생성할 Material

    private void Start()
    {
        // Start loading the texture
        StartCoroutine(LoadTextureAndApplyMaterial(textureURL));
    }

    private IEnumerator LoadTextureAndApplyMaterial(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            Debug.Log("Downloading texture from: " + url);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 텍스처 다운로드 성공
                Texture2D downloadedTexture = DownloadHandlerTexture.GetContent(request);
                Debug.Log("Texture downloaded successfully!");

                // 새 Material 생성 및 텍스처 적용
                newMaterial = new Material(Shader.Find("Standard")); // Standard Shader 사용
                newMaterial.mainTexture = downloadedTexture;

                // 큐브에 Material 적용
                if (targetRenderer != null)
                {
                    targetRenderer.material = newMaterial;
                    Debug.Log("Material applied to the target Renderer.");
                }
            }
            else
            {
                Debug.LogError("Failed to download texture: " + request.error);
            }
        }
    }
}
