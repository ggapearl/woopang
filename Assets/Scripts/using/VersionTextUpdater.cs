using UnityEngine;
using UnityEngine.UI; // Required for legacy Text component

public class VersionTextUpdater : MonoBehaviour
{
    [SerializeField] private Text versionText; // Reference to the legacy Text component

    void Start()
    {
        // Retrieve the project version
        string version = Application.version;
        Debug.Log("Version retrieved: " + version); // Log the retrieved version

        // Update the Text component if assigned
        if (versionText != null)
        {
            versionText.text = version;
            Debug.Log("Text updated to: " + versionText.text); // Log the updated text
        }
        else
        {
            Debug.LogWarning("VersionTextUpdater: No Text component assigned!");
        }
    }
}