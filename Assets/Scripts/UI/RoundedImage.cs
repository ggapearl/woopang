using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class RoundedImage : MonoBehaviour
{
    [Range(0, 100)]
    public float cornerRadius = 20f;

    private Material material;
    private Image image;

    void Start()
    {
        image = GetComponent<Image>();

        // Rounded corner shader를 사용하는 Material 생성
        Shader shader = Shader.Find("UI/RoundedCorners");
        if (shader != null)
        {
            material = new Material(shader);
            image.material = material;
            UpdateCornerRadius();
        }
        else
        {
            Debug.LogWarning("[RoundedImage] UI/RoundedCorners shader를 찾을 수 없습니다. 기본 이미지로 표시됩니다.");
        }
    }

    void Update()
    {
        if (material != null)
        {
            UpdateCornerRadius();
        }
    }

    private void UpdateCornerRadius()
    {
        RectTransform rect = GetComponent<RectTransform>();
        Vector2 size = rect.rect.size;

        // 정규화된 corner radius 계산
        float normalizedRadius = cornerRadius / Mathf.Min(size.x, size.y);
        material.SetFloat("_CornerRadius", normalizedRadius);
    }
}
