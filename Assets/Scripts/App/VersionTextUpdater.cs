using UnityEngine;
using UnityEngine.UI; // Required for legacy Text component

public class VersionTextUpdater : MonoBehaviour
{
    [SerializeField] private Text versionText; // Reference to the legacy Text component
    [SerializeField] private Text versionText2; // 두 번째 버전 텍스트

    void Start()
    {
        string version = Application.version;

        if (versionText != null)
        {
            versionText.text = version;
        }

        if (versionText2 != null)
        {
            versionText2.text = version;
        }
    }
}