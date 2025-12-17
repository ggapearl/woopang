using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Automatically applies rounded corners to an Image component.
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteInEditMode]
public class RoundedImageHelper : MonoBehaviour
{
    [SerializeField]
    [Range(0f, 200f)]
    [Tooltip("Radius of the rounded corners in pixels")]
    private float cornerRadius = 30f;

    private Image image;
    private Material roundedMaterial;
    private static Shader roundedShader;

    void Start()
    {
        ApplyRoundedCorners();
    }

    void OnValidate()
    {
        if (Application.isPlaying || !enabled)
            return;

        ApplyRoundedCorners();
    }

    private void ApplyRoundedCorners()
    {
        image = GetComponent<Image>();
        if (image == null)
            return;

        // Load the rounded corners shader if not already loaded
        if (roundedShader == null)
        {
            roundedShader = Shader.Find("UI/RoundedCorners");
            if (roundedShader == null)
            {
                return;
            }
        }

        // Create a new material if needed
        if (roundedMaterial == null || roundedMaterial.shader != roundedShader)
        {
            roundedMaterial = new Material(roundedShader);
        }

        // Set the corner radius
        roundedMaterial.SetFloat("_CornerRadius", cornerRadius);

        // Apply the material to the image
        image.material = roundedMaterial;
    }

    /// <summary>
    /// Sets the corner radius dynamically at runtime
    /// </summary>
    /// <param name="radius">Corner radius in pixels</param>
    public void SetCornerRadius(float radius)
    {
        cornerRadius = Mathf.Clamp(radius, 0f, 200f);
        if (roundedMaterial != null)
        {
            roundedMaterial.SetFloat("_CornerRadius", cornerRadius);
        }
    }

    /// <summary>
    /// Gets the current corner radius
    /// </summary>
    public float GetCornerRadius()
    {
        return cornerRadius;
    }

    void OnDestroy()
    {
        // Clean up the material
        if (roundedMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(roundedMaterial);
            }
            else
            {
                DestroyImmediate(roundedMaterial);
            }
        }
    }
}
