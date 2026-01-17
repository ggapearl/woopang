/**
 * CleanupCustomScripts.cs
 * 임의로 추가한 Woopang 에디터 스크립트들을 삭제하는 유틸리티
 *
 * 사용법: Tools > Cleanup Custom Woopang Scripts 실행
 */

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class CleanupCustomScripts : EditorWindow
{
    private static readonly string[] scriptsToDelete = new string[]
    {
        // Scripts/Editor 폴더
        "Assets/Scripts/Editor/P2PAvatarPrefabCreator.cs",
        "Assets/Scripts/Editor/P2PAutoSetup.cs",
        "Assets/Scripts/Editor/P2PUserFilterSetup.cs",
        "Assets/Scripts/Editor/ProfileUISetup.cs",
        "Assets/Scripts/Editor/StyleCommentItem.cs",
        "Assets/Scripts/Editor/VirtualLocationEditor.cs",
        // Editor 폴더
        "Assets/Editor/WoopangUISetupTool.cs",
        "Assets/Editor/ApplyCommentStyle.cs",
        "Assets/Editor/AuthPanelImprover.cs",
        "Assets/Editor/CreateSpinnerIcon.cs",
        "Assets/Editor/CreateUserProfileUI.cs",
        "Assets/Editor/HeartIconGenerator.cs",
        "Assets/Editor/PhotoDialogPrefabCreator.cs",
        "Assets/Editor/SetupListPanelProfile.cs",
        "Assets/Editor/SwapImageControllers.cs"
        // 이 스크립트(CleanupCustomScripts.cs)는 삭제하지 않음
    };

    [MenuItem("Tools/Cleanup Custom Woopang Scripts")]
    public static void ShowWindow()
    {
        // 삭제할 파일 목록 확인
        List<string> existingFiles = new List<string>();

        foreach (string script in scriptsToDelete)
        {
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), script);
            if (File.Exists(fullPath))
            {
                existingFiles.Add(script);
            }
        }

        if (existingFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("정리 완료",
                "삭제할 커스텀 스크립트가 없습니다.\n\n" +
                "모든 Woopang 에디터 스크립트가 이미 정리되었습니다.",
                "확인");
            return;
        }

        string fileList = string.Join("\n• ", existingFiles);

        if (EditorUtility.DisplayDialog("커스텀 스크립트 삭제",
            $"다음 {existingFiles.Count}개의 스크립트를 삭제하시겠습니까?\n\n• {fileList}\n\n" +
            "이 작업은 되돌릴 수 없습니다.",
            "삭제", "취소"))
        {
            DeleteScripts(existingFiles);
        }
    }

    private static void DeleteScripts(List<string> files)
    {
        int deletedCount = 0;

        foreach (string script in files)
        {
            if (AssetDatabase.DeleteAsset(script))
            {
                Debug.Log($"[Cleanup] 삭제됨: {script}");
                deletedCount++;
            }
            else
            {
                Debug.LogWarning($"[Cleanup] 삭제 실패: {script}");
            }
        }

        AssetDatabase.Refresh();

        Debug.Log($"[Cleanup] 총 {deletedCount}개 스크립트 삭제 완료");
    }
}
