using System.Collections; // �߰�: IEnumerator�� ����ϱ� ���� �ʿ�
using UnityEngine;
using UnityEngine.Networking; // UnityWebRequest�� ����ϱ� ���� �ʿ�

public class TextureToMaterialLoader : MonoBehaviour
{
    [Tooltip("Enter the URL of the texture you want to load.")]
    public string textureURL = "https://example.com/your_texture.png";

    [Tooltip("Target Renderer to apply the material with the downloaded texture.")]
    public Renderer targetRenderer;

    private Material newMaterial; // ���� ������ Material

    private void Start()
    {
        // Start loading the texture
        StartCoroutine(LoadTextureAndApplyMaterial(textureURL));
    }

    private IEnumerator LoadTextureAndApplyMaterial(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // �ؽ�ó �ٿ�ε� ����
                Texture2D downloadedTexture = DownloadHandlerTexture.GetContent(request);

                // �� Material ���� �� �ؽ�ó ����
                newMaterial = new Material(Shader.Find("Standard")); // Standard Shader ���
                newMaterial.mainTexture = downloadedTexture;

                // ť�꿡 Material ����
                if (targetRenderer != null)
                {
                    targetRenderer.material = newMaterial;
                }
            }
            else
            {
                Debug.LogError("Failed to download texture: " + request.error);
            }
        }
    }
}
