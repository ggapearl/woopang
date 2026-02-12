using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using TMPro;
using UnityEditor.SceneManagement;

/// <summary>
/// 모든 Text와 TextMeshPro 컴포넌트의 폰트를 AppleSDGothicNeoM으로 자동 설정
/// </summary>
public static class FontFixer
{
    private const string APPLE_FONT_TTF_PATH = "Assets/TextMesh Pro/Fonts/AppleSDGothicNeoM.ttf";
    private const string APPLE_FONT_SDF_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/AppleSDGothicNeoM.asset";

    [MenuItem("Tools/Fix All Fonts to AppleSDGothicNeoM")]
    public static void FixAllFonts()
    {
        // Load font assets
        Font appleFont = AssetDatabase.LoadAssetAtPath<Font>(APPLE_FONT_TTF_PATH);
        TMP_FontAsset appleFontSDF = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(APPLE_FONT_SDF_PATH);

        if (appleFont == null)
        {
            Debug.LogError($"[FontFixer] Could not load font at {APPLE_FONT_TTF_PATH}");
            return;
        }

        if (appleFontSDF == null)
        {
            Debug.LogError($"[FontFixer] Could not load TMP font at {APPLE_FONT_SDF_PATH}");
            return;
        }

        int legacyTextFixed = 0;
        int tmpTextFixed = 0;

        // Fix all Legacy Text components
        Text[] allTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Text text in allTexts)
        {
            if (text.font == null || text.font != appleFont)
            {
                Undo.RecordObject(text, "Fix Font");
                text.font = appleFont;
                EditorUtility.SetDirty(text);
                legacyTextFixed++;
                Debug.Log($"[FontFixer] Fixed Legacy Text on: {GetGameObjectPath(text.gameObject)}");
            }
        }

        // Fix all TextMeshPro components
        TextMeshProUGUI[] allTMPTexts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TextMeshProUGUI tmpText in allTMPTexts)
        {
            if (tmpText.font == null || tmpText.font != appleFontSDF)
            {
                Undo.RecordObject(tmpText, "Fix TMP Font");
                tmpText.font = appleFontSDF;
                EditorUtility.SetDirty(tmpText);
                tmpTextFixed++;
                Debug.Log($"[FontFixer] Fixed TextMeshPro on: {GetGameObjectPath(tmpText.gameObject)}");
            }
        }

        // Save scene
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[FontFixer] ✅ Complete! Fixed {legacyTextFixed} Legacy Text + {tmpTextFixed} TextMeshPro components");
        EditorUtility.DisplayDialog(
            "Font Fix Complete",
            $"Successfully fixed fonts:\n\n" +
            $"• Legacy Text: {legacyTextFixed} objects\n" +
            $"• TextMeshPro: {tmpTextFixed} objects\n\n" +
            $"All fonts set to AppleSDGothicNeoM",
            "OK"
        );
    }

    [MenuItem("Tools/Check Font Status")]
    public static void CheckFontStatus()
    {
        Font appleFont = AssetDatabase.LoadAssetAtPath<Font>(APPLE_FONT_TTF_PATH);
        TMP_FontAsset appleFontSDF = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(APPLE_FONT_SDF_PATH);

        int legacyCorrect = 0, legacyMissing = 0, legacyWrong = 0;
        int tmpCorrect = 0, tmpMissing = 0, tmpWrong = 0;

        // Check Legacy Text
        Text[] allTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Text text in allTexts)
        {
            if (text.font == null)
            {
                legacyMissing++;
                Debug.LogWarning($"[FontFixer] Missing font on: {GetGameObjectPath(text.gameObject)}", text);
            }
            else if (text.font == appleFont)
            {
                legacyCorrect++;
            }
            else
            {
                legacyWrong++;
                Debug.LogWarning($"[FontFixer] Wrong font '{text.font.name}' on: {GetGameObjectPath(text.gameObject)}", text);
            }
        }

        // Check TextMeshPro
        TextMeshProUGUI[] allTMPTexts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TextMeshProUGUI tmpText in allTMPTexts)
        {
            if (tmpText.font == null)
            {
                tmpMissing++;
                Debug.LogWarning($"[FontFixer] Missing TMP font on: {GetGameObjectPath(tmpText.gameObject)}", tmpText);
            }
            else if (tmpText.font == appleFontSDF)
            {
                tmpCorrect++;
            }
            else
            {
                tmpWrong++;
                Debug.LogWarning($"[FontFixer] Wrong TMP font '{tmpText.font.name}' on: {GetGameObjectPath(tmpText.gameObject)}", tmpText);
            }
        }

        string report = $"=== Font Status Report ===\n\n" +
                       $"Legacy Text (UI.Text):\n" +
                       $"  ✅ Correct: {legacyCorrect}\n" +
                       $"  ❌ Missing: {legacyMissing}\n" +
                       $"  ⚠️  Wrong: {legacyWrong}\n\n" +
                       $"TextMeshPro:\n" +
                       $"  ✅ Correct: {tmpCorrect}\n" +
                       $"  ❌ Missing: {tmpMissing}\n" +
                       $"  ⚠️  Wrong: {tmpWrong}";

        Debug.Log(report);
        EditorUtility.DisplayDialog("Font Status", report, "OK");
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    // ⭐ CLAUDE.md 가이드라인: 자동 실행 트리거 3개
    [InitializeOnLoad]
    public class AutoFontFixer
    {
        static AutoFontFixer()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += AutoFixFontsSilent;
        }

        // 스크립트 컴파일 완료 시 자동 실행 (필수!)
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += AutoFixFontsSilent;
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            EditorApplication.delayCall += AutoFixFontsSilent;
        }

        // 자동으로 폰트 수정 (조용하게)
        private static void AutoFixFontsSilent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Font appleFont = AssetDatabase.LoadAssetAtPath<Font>(APPLE_FONT_TTF_PATH);
            TMP_FontAsset appleFontSDF = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(APPLE_FONT_SDF_PATH);

            if (appleFont == null || appleFontSDF == null) return;

            int legacyFixed = 0;
            int tmpFixed = 0;
            bool sceneModified = false;

            // Fix Legacy Text
            Text[] allTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Text text in allTexts)
            {
                if (text.font == null || text.font != appleFont)
                {
                    Undo.RecordObject(text, "Auto-fix Font");
                    text.font = appleFont;
                    EditorUtility.SetDirty(text);
                    legacyFixed++;
                    sceneModified = true;
                }
            }

            // Fix TextMeshPro
            TextMeshProUGUI[] allTMPTexts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TextMeshProUGUI tmpText in allTMPTexts)
            {
                if (tmpText.font == null || tmpText.font != appleFontSDF)
                {
                    Undo.RecordObject(tmpText, "Auto-fix TMP Font");
                    tmpText.font = appleFontSDF;
                    EditorUtility.SetDirty(tmpText);
                    tmpFixed++;
                    sceneModified = true;
                }
            }

            if (sceneModified)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"[FontFixer] ✅ Auto-fixed fonts: {legacyFixed} Legacy Text + {tmpFixed} TextMeshPro");
            }
        }
    }
}
