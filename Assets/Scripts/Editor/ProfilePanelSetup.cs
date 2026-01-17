/**
 * ProfilePanelSetup.cs
 * Unity Editor Script - FullProfilePanel & MiniProfile 설정
 *
 * 기능:
 * 1. FullProfilePanel 현재 크기에서 2배 확대
 * 2. 아바타 테두리(Outline) 추가 - 공개상태에 따라 색상 변경
 * 3. 아바타 공개상태 텍스트 추가
 * 4. MiniProfile에 아바타 테두리 추가
 * 5. 모든 텍스트 AppleSDGothicNeoM 폰트 적용
 *
 * 공개상태 테두리 색상:
 * - 전체공개 (Public): 핑크색 #E95383
 * - 팔로잉에게만 (FollowingOnly): 노란색 #FFD700
 * - 비공개 (Private): 회색 #808080
 *
 * 사용법: Unity 메뉴 > Tools > Profile Setup
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public class ProfilePanelSetup
{
    private const string FULL_PROFILE_PATH = "Assets/Prefabs/FullProfilePanel.prefab";
    private const string MINI_PROFILE_PATH = "Assets/Prefabs/MiniProfile.prefab";
    private const string FONT_PATH = "Assets/Resources/Fonts/AppleSDGothicNeoM.ttf";
    private const string TMP_FONT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/AppleSDGothicNeoM.asset";

    // 공개상태 색상 정의
    public static readonly Color PUBLIC_COLOR = new Color(0.914f, 0.325f, 0.514f, 1f);      // #E95383 핑크
    public static readonly Color FOLLOWING_ONLY_COLOR = new Color(1f, 0.843f, 0f, 1f);      // #FFD700 금색/노란색
    public static readonly Color PRIVATE_COLOR = new Color(0.5f, 0.5f, 0.5f, 1f);           // #808080 회색

    // ============================================================
    // 메인 메뉴
    // ============================================================

    [MenuItem("Tools/Profile Setup/1. Setup All (Full Reset + 2x Scale)", false, 0)]
    public static void SetupAll()
    {
        bool success1 = SetupFullProfilePanelInternal(true); // force reset
        bool success2 = SetupMiniProfileInternal(true);

        if (success1 && success2)
        {
            EditorUtility.DisplayDialog("Setup Complete",
                "프로필 UI 설정이 완료되었습니다!\n\n" +
                "=== FullProfilePanel ===\n" +
                "- 현재 크기에서 2배 확대\n" +
                "- AvatarOutline 추가됨 (핑크 #E95383)\n" +
                "- VisibilityStatusText 추가됨\n" +
                "- AvatarMask에 Button 컴포넌트 추가됨\n" +
                "- 모든 텍스트 AppleSDGothicNeoM 폰트 적용\n\n" +
                "=== MiniProfile ===\n" +
                "- AvatarOutline 추가됨 (핑크 #E95383)\n\n" +
                "=== 중요: Inspector 연결 필요 ===\n" +
                "ProfileManager 컴포넌트에서:\n" +
                "1. avatarOutlineImage = FullProfilePanel > Content > AvatarOutline\n" +
                "2. miniAvatarOutlineImage = MiniProfile > AvatarOutline\n" +
                "3. visibilityStatusText = FullProfilePanel > Content > VisibilityStatusText\n" +
                "4. avatarButton = FullProfilePanel > Content > AvatarMask",
                "OK");
        }
    }

    [MenuItem("Tools/Profile Setup/2. Scale Current 2x (No Reset)", false, 10)]
    public static void ScaleOnly()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FULL_PROFILE_PATH);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Error", "FullProfilePanel.prefab을 찾을 수 없습니다.", "OK");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        try
        {
            Transform content = FindChildRecursive(instance.transform, "Content");
            if (content != null)
            {
                RectTransform contentRect = content.GetComponent<RectTransform>();
                if (contentRect != null)
                {
                    Vector2 currentSize = contentRect.sizeDelta;
                    contentRect.sizeDelta = currentSize * 2f;
                    ScaleAllChildren(content, 2.0f);
                    Debug.Log($"[ProfilePanelSetup] Scaled from {currentSize} to {contentRect.sizeDelta}");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(instance, FULL_PROFILE_PATH);
            Object.DestroyImmediate(instance);

            EditorUtility.DisplayDialog("Success", "FullProfilePanel이 2배로 확대되었습니다.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }

    [MenuItem("Tools/Profile Setup/3. Add Outline Only (No Scale)", false, 11)]
    public static void AddOutlineOnly()
    {
        bool success1 = AddOutlineToFullProfile();
        bool success2 = AddOutlineToMiniProfile();

        if (success1 || success2)
        {
            EditorUtility.DisplayDialog("Success",
                "AvatarOutline이 추가되었습니다.\n\n" +
                "색상: 핑크 #E95383 (기본값 = 전체공개)\n\n" +
                "Inspector 연결 필요:\n" +
                "- avatarOutlineImage\n" +
                "- miniAvatarOutlineImage",
                "OK");
        }
    }

    [MenuItem("Tools/Profile Setup/4. Apply Font Only", false, 12)]
    public static void ApplyFontOnly()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FULL_PROFILE_PATH);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Error", "FullProfilePanel.prefab을 찾을 수 없습니다.", "OK");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        try
        {
            Transform content = FindChildRecursive(instance.transform, "Content");
            if (content != null)
            {
                int count = ApplyFontToAllTexts(content);
                Debug.Log($"[ProfilePanelSetup] Applied font to {count} text components");
            }

            PrefabUtility.SaveAsPrefabAsset(instance, FULL_PROFILE_PATH);
            Object.DestroyImmediate(instance);

            EditorUtility.DisplayDialog("Success", "FullProfilePanel의 모든 텍스트에 AppleSDGothicNeoM 폰트가 적용되었습니다.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }

    [MenuItem("Tools/Profile Setup/5. Verify Setup", false, 30)]
    public static void VerifySetup()
    {
        string status = "=== Profile Setup Verification ===\n\n";

        // FullProfilePanel 확인
        GameObject fullPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FULL_PROFILE_PATH);
        if (fullPrefab != null)
        {
            status += "[FullProfilePanel.prefab]\n";
            Transform content = FindChildRecursive(fullPrefab.transform, "Content");
            if (content != null)
            {
                RectTransform rect = content.GetComponent<RectTransform>();
                status += $"  Content Size: {rect.sizeDelta.x} x {rect.sizeDelta.y}\n";
            }

            Transform outline = FindChildRecursive(fullPrefab.transform, "AvatarOutline");
            if (outline != null)
            {
                Image img = outline.GetComponent<Image>();
                string colorHex = img != null ? ColorUtility.ToHtmlStringRGB(img.color) : "N/A";
                RectTransform outlineRect = outline.GetComponent<RectTransform>();
                status += $"  AvatarOutline: OK (Color: #{colorHex}, Size: {outlineRect.sizeDelta})\n";
            }
            else
            {
                status += "  AvatarOutline: NOT FOUND\n";
            }

            Transform visibility = FindChildRecursive(fullPrefab.transform, "VisibilityStatusText");
            status += $"  VisibilityStatusText: {(visibility != null ? "OK" : "NOT FOUND")}\n";

            Transform avatarMask = FindChildRecursive(fullPrefab.transform, "AvatarMask");
            if (avatarMask != null)
            {
                Button btn = avatarMask.GetComponent<Button>();
                status += $"  AvatarMask Button: {(btn != null ? "OK" : "NOT FOUND")}\n";
                RectTransform maskRect = avatarMask.GetComponent<RectTransform>();
                status += $"  AvatarMask Size: {maskRect.sizeDelta}\n";
            }
        }
        else
        {
            status += "[FullProfilePanel.prefab] NOT FOUND\n";
        }

        // MiniProfile 확인
        status += "\n[MiniProfile.prefab]\n";
        GameObject miniPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MINI_PROFILE_PATH);
        if (miniPrefab != null)
        {
            Transform outline = FindChildRecursive(miniPrefab.transform, "AvatarOutline");
            if (outline != null)
            {
                Image img = outline.GetComponent<Image>();
                string colorHex = img != null ? ColorUtility.ToHtmlStringRGB(img.color) : "N/A";
                RectTransform outlineRect = outline.GetComponent<RectTransform>();
                status += $"  AvatarOutline: OK (Color: #{colorHex}, Size: {outlineRect.sizeDelta})\n";
            }
            else
            {
                status += "  AvatarOutline: NOT FOUND\n";
            }
        }
        else
        {
            status += "  NOT FOUND\n";
        }

        status += "\n=== 공개상태 색상 가이드 ===\n";
        status += "Public (전체공개): Pink #E95383\n";
        status += "FollowingOnly (팔로잉에게만): Yellow #FFD700\n";
        status += "Private (비공개): Gray #808080\n";

        status += "\n=== Inspector 연결 확인 ===\n";
        status += "ProfileManager 컴포넌트에서 다음 필드가 연결되어야 합니다:\n";
        status += "- avatarOutlineImage\n";
        status += "- miniAvatarOutlineImage\n";
        status += "- visibilityStatusText\n";
        status += "- avatarButton\n";

        EditorUtility.DisplayDialog("Verification Result", status, "OK");
    }

    [MenuItem("Tools/Profile Setup/6. Refresh Scene Instances", false, 35)]
    public static void RefreshSceneInstances()
    {
        int refreshedCount = 0;

        // FullProfilePanel 새로고침
        GameObject fullProfileInScene = FindInScene("FullProfilePanel");
        if (fullProfileInScene != null)
        {
            // 프리팹에서 AvatarOutline 가져오기
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FULL_PROFILE_PATH);
            if (prefab != null)
            {
                Transform prefabContent = FindChildRecursive(prefab.transform, "Content");
                Transform sceneContent = FindChildRecursive(fullProfileInScene.transform, "Content");

                if (prefabContent != null && sceneContent != null)
                {
                    // 씬의 기존 AvatarOutline 삭제
                    Transform existingOutline = FindChildRecursive(sceneContent, "AvatarOutline");
                    if (existingOutline != null)
                    {
                        Object.DestroyImmediate(existingOutline.gameObject);
                    }

                    // 프리팹에서 AvatarOutline 복사
                    Transform prefabOutline = FindChildRecursive(prefabContent, "AvatarOutline");
                    if (prefabOutline != null)
                    {
                        GameObject newOutline = Object.Instantiate(prefabOutline.gameObject, sceneContent);
                        newOutline.name = "AvatarOutline";

                        // AvatarMask 위치에 맞게 배치
                        Transform avatarMask = FindChildRecursive(sceneContent, "AvatarMask");
                        if (avatarMask != null)
                        {
                            RectTransform outlineRect = newOutline.GetComponent<RectTransform>();
                            RectTransform maskRect = avatarMask.GetComponent<RectTransform>();
                            outlineRect.anchoredPosition = maskRect.anchoredPosition;

                            // AvatarMask 바로 뒤에 배치
                            int maskIndex = avatarMask.GetSiblingIndex();
                            newOutline.transform.SetSiblingIndex(maskIndex);
                        }

                        refreshedCount++;
                        Debug.Log("[ProfilePanelSetup] FullProfilePanel AvatarOutline refreshed");
                    }

                    // VisibilityStatusText도 복사
                    Transform existingVisibility = FindChildRecursive(sceneContent, "VisibilityStatusText");
                    if (existingVisibility != null)
                    {
                        Object.DestroyImmediate(existingVisibility.gameObject);
                    }

                    Transform prefabVisibility = FindChildRecursive(prefabContent, "VisibilityStatusText");
                    if (prefabVisibility != null)
                    {
                        GameObject newVisibility = Object.Instantiate(prefabVisibility.gameObject, sceneContent);
                        newVisibility.name = "VisibilityStatusText";
                        refreshedCount++;
                        Debug.Log("[ProfilePanelSetup] FullProfilePanel VisibilityStatusText refreshed");
                    }

                    // AvatarMask에 Button 추가
                    Transform avatarMaskForBtn = FindChildRecursive(sceneContent, "AvatarMask");
                    if (avatarMaskForBtn != null)
                    {
                        Button existingBtn = avatarMaskForBtn.GetComponent<Button>();
                        if (existingBtn == null)
                        {
                            Button btn = avatarMaskForBtn.gameObject.AddComponent<Button>();
                            btn.transition = Selectable.Transition.ColorTint;
                            Image img = avatarMaskForBtn.GetComponent<Image>();
                            if (img != null) btn.targetGraphic = img;
                            Debug.Log("[ProfilePanelSetup] AvatarMask Button added");
                        }
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("[ProfilePanelSetup] FullProfilePanel not found in scene");
        }

        // MiniProfile 새로고침
        GameObject miniProfileInScene = FindInScene("MiniProfile");
        if (miniProfileInScene != null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MINI_PROFILE_PATH);
            if (prefab != null)
            {
                // 씬의 기존 AvatarOutline 삭제
                Transform existingOutline = FindChildRecursive(miniProfileInScene.transform, "AvatarOutline");
                if (existingOutline != null)
                {
                    Object.DestroyImmediate(existingOutline.gameObject);
                }

                // 프리팹에서 AvatarOutline 복사
                Transform prefabOutline = FindChildRecursive(prefab.transform, "AvatarOutline");
                if (prefabOutline != null)
                {
                    Transform avatarMask = FindChildRecursive(miniProfileInScene.transform, "AvatarMask");
                    Transform parent = avatarMask != null ? avatarMask.parent : miniProfileInScene.transform;

                    GameObject newOutline = Object.Instantiate(prefabOutline.gameObject, parent);
                    newOutline.name = "AvatarOutline";

                    if (avatarMask != null)
                    {
                        RectTransform outlineRect = newOutline.GetComponent<RectTransform>();
                        RectTransform maskRect = avatarMask.GetComponent<RectTransform>();
                        outlineRect.anchoredPosition = maskRect.anchoredPosition;

                        int maskIndex = avatarMask.GetSiblingIndex();
                        newOutline.transform.SetSiblingIndex(maskIndex);
                    }

                    refreshedCount++;
                    Debug.Log("[ProfilePanelSetup] MiniProfile AvatarOutline refreshed");
                }
            }
        }
        else
        {
            Debug.LogWarning("[ProfilePanelSetup] MiniProfile not found in scene");
        }

        if (refreshedCount > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        EditorUtility.DisplayDialog("Refresh Complete",
            $"씬 인스턴스 새로고침 완료!\n\n" +
            $"업데이트된 항목: {refreshedCount}개\n\n" +
            "이제 '7. Auto Connect Inspector'를 실행하세요.\n" +
            "그 후 씬을 저장하세요 (Ctrl+S)",
            "OK");
    }

    [MenuItem("Tools/Profile Setup/7. Auto Connect Inspector", false, 40)]
    public static void AutoConnectInspector()
    {
        // 씬에서 ProfileManager 찾기
        ProfileManager profileManager = Object.FindObjectOfType<ProfileManager>();
        if (profileManager == null)
        {
            EditorUtility.DisplayDialog("Error",
                "씬에서 ProfileManager를 찾을 수 없습니다.\n\n" +
                "ProfileManager가 있는 씬을 열고 다시 실행해주세요.", "OK");
            return;
        }

        int connectedCount = 0;
        string result = "=== Inspector 자동 연결 결과 ===\n\n";

        // FullProfilePanel에서 연결
        GameObject fullProfilePanel = FindInScene("FullProfilePanel");
        if (fullProfilePanel != null)
        {
            // avatarOutlineImage
            Transform avatarOutline = FindChildRecursive(fullProfilePanel.transform, "AvatarOutline");
            if (avatarOutline != null)
            {
                Image outlineImg = avatarOutline.GetComponent<Image>();
                if (outlineImg != null)
                {
                    SerializedObject so = new SerializedObject(profileManager);
                    SerializedProperty prop = so.FindProperty("avatarOutlineImage");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = outlineImg;
                        so.ApplyModifiedProperties();
                        result += "avatarOutlineImage: Connected\n";
                        connectedCount++;
                    }
                }
            }
            else
            {
                result += "avatarOutlineImage: AvatarOutline not found\n";
            }

            // visibilityStatusText
            Transform visibilityText = FindChildRecursive(fullProfilePanel.transform, "VisibilityStatusText");
            if (visibilityText != null)
            {
                Text txt = visibilityText.GetComponent<Text>();
                if (txt != null)
                {
                    SerializedObject so = new SerializedObject(profileManager);
                    SerializedProperty prop = so.FindProperty("visibilityStatusText");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = txt;
                        so.ApplyModifiedProperties();
                        result += "visibilityStatusText: Connected\n";
                        connectedCount++;
                    }
                }
            }
            else
            {
                result += "visibilityStatusText: VisibilityStatusText not found\n";
            }

            // avatarButton
            Transform avatarMask = FindChildRecursive(fullProfilePanel.transform, "AvatarMask");
            if (avatarMask != null)
            {
                Button btn = avatarMask.GetComponent<Button>();
                if (btn != null)
                {
                    SerializedObject so = new SerializedObject(profileManager);
                    SerializedProperty prop = so.FindProperty("avatarButton");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = btn;
                        so.ApplyModifiedProperties();
                        result += "avatarButton: Connected\n";
                        connectedCount++;
                    }
                }
                else
                {
                    result += "avatarButton: Button component not found on AvatarMask\n";
                }
            }
            else
            {
                result += "avatarButton: AvatarMask not found\n";
            }
        }
        else
        {
            result += "FullProfilePanel: NOT FOUND in scene\n";
        }

        // MiniProfile에서 연결
        GameObject miniProfile = FindInScene("MiniProfile");
        if (miniProfile != null)
        {
            Transform miniOutline = FindChildRecursive(miniProfile.transform, "AvatarOutline");
            if (miniOutline != null)
            {
                Image outlineImg = miniOutline.GetComponent<Image>();
                if (outlineImg != null)
                {
                    SerializedObject so = new SerializedObject(profileManager);
                    SerializedProperty prop = so.FindProperty("miniAvatarOutlineImage");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = outlineImg;
                        so.ApplyModifiedProperties();
                        result += "miniAvatarOutlineImage: Connected\n";
                        connectedCount++;
                    }
                }
            }
            else
            {
                result += "miniAvatarOutlineImage: AvatarOutline not found in MiniProfile\n";
            }
        }
        else
        {
            result += "MiniProfile: NOT FOUND in scene\n";
        }

        // 씬 저장 필요 표시
        if (connectedCount > 0)
        {
            EditorUtility.SetDirty(profileManager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(profileManager.gameObject.scene);
        }

        result += $"\n총 {connectedCount}개 필드 연결됨\n";
        result += "\n씬을 저장하세요! (Ctrl+S)";

        EditorUtility.DisplayDialog("Auto Connect Result", result, "OK");
    }

    private static GameObject FindInScene(string name)
    {
        // 모든 루트 오브젝트 검색
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            if (root.name == name) return root;

            Transform found = FindChildRecursive(root.transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    [MenuItem("Tools/Profile Setup/Remove All Outlines", false, 50)]
    public static void RemoveAllOutlines()
    {
        RemoveOutlineFromPrefab(FULL_PROFILE_PATH);
        RemoveOutlineFromPrefab(MINI_PROFILE_PATH);
        EditorUtility.DisplayDialog("Success", "모든 AvatarOutline이 제거되었습니다.", "OK");
    }

    [MenuItem("Tools/Profile Setup/RESET to Original Size (600x800)", false, 60)]
    public static void ResetToOriginalSize()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FULL_PROFILE_PATH);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Error", "FullProfilePanel.prefab을 찾을 수 없습니다.", "OK");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        try
        {
            Transform content = FindChildRecursive(instance.transform, "Content");
            if (content != null)
            {
                RectTransform contentRect = content.GetComponent<RectTransform>();
                Vector2 currentSize = contentRect.sizeDelta;

                // 현재 크기에서 원래 크기(600x800)로 돌아가기 위한 스케일 계산
                float scaleX = 600f / currentSize.x;
                float scaleY = 800f / currentSize.y;
                float scale = Mathf.Min(scaleX, scaleY); // 더 작은 비율 사용

                contentRect.sizeDelta = new Vector2(600, 800);
                ScaleAllChildren(content, scale);

                // 아웃라인, VisibilityStatusText 제거
                Transform outline = content.Find("AvatarOutline");
                if (outline != null) Object.DestroyImmediate(outline.gameObject);

                Transform visibility = FindChildRecursive(content, "VisibilityStatusText");
                if (visibility != null) Object.DestroyImmediate(visibility.gameObject);

                // AvatarMask에서 Button 제거
                Transform avatarMask = FindChildRecursive(content, "AvatarMask");
                if (avatarMask != null)
                {
                    Button btn = avatarMask.GetComponent<Button>();
                    if (btn != null) Object.DestroyImmediate(btn);
                }

                Debug.Log($"[ProfilePanelSetup] Reset from {currentSize} to (600, 800)");
            }

            PrefabUtility.SaveAsPrefabAsset(instance, FULL_PROFILE_PATH);
            Object.DestroyImmediate(instance);

            // MiniProfile 아웃라인도 제거
            RemoveOutlineFromPrefab(MINI_PROFILE_PATH);

            EditorUtility.DisplayDialog("Reset Complete",
                "FullProfilePanel이 원래 크기(600x800)로 복원되었습니다.\n\n" +
                "이제 'Tools > Profile Setup > 1. Setup All'을 실행하세요.",
                "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }

    // ============================================================
    // Internal Implementation
    // ============================================================

    private static bool SetupFullProfilePanelInternal(bool forceReset)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FULL_PROFILE_PATH);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Error", $"FullProfilePanel.prefab을 찾을 수 없습니다.\n경로: {FULL_PROFILE_PATH}", "OK");
            return false;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            EditorUtility.DisplayDialog("Error", "프리팹 인스턴스 생성 실패", "OK");
            return false;
        }

        try
        {
            Transform content = FindChildRecursive(instance.transform, "Content");
            if (content != null)
            {
                RectTransform contentRect = content.GetComponent<RectTransform>();

                if (forceReset && contentRect != null)
                {
                    // 현재 크기에서 2배 확대
                    Vector2 currentSize = contentRect.sizeDelta;
                    contentRect.sizeDelta = currentSize * 2f;
                    ScaleAllChildren(content, 2.0f);
                    Debug.Log($"[ProfilePanelSetup] Content scaled 2x: {currentSize} -> {contentRect.sizeDelta}");
                }

                // AvatarMask 찾기
                Transform avatarMask = FindChildRecursive(content, "AvatarMask");
                if (avatarMask != null)
                {
                    // 기존 아웃라인 삭제 후 새로 생성
                    Transform existingOutline = content.Find("AvatarOutline");
                    if (existingOutline != null)
                    {
                        Object.DestroyImmediate(existingOutline.gameObject);
                    }

                    // 아바타 테두리 추가
                    RectTransform maskRect = avatarMask.GetComponent<RectTransform>();
                    float borderWidth = maskRect.sizeDelta.x * 0.04f; // 4% of mask size
                    CreateAvatarOutline(content, avatarMask, "AvatarOutline", borderWidth);

                    // 기존 VisibilityStatusText 삭제 후 새로 생성
                    Transform existingVisibility = FindChildRecursive(content, "VisibilityStatusText");
                    if (existingVisibility != null)
                    {
                        Object.DestroyImmediate(existingVisibility.gameObject);
                    }

                    // 공개상태 텍스트 추가
                    CreateVisibilityStatusText(content, avatarMask);

                    // AvatarMask에 Button 컴포넌트 추가
                    AddButtonToAvatarMask(avatarMask);
                }

                // 모든 텍스트에 폰트 적용
                int fontCount = ApplyFontToAllTexts(content);
                Debug.Log($"[ProfilePanelSetup] Applied font to {fontCount} text components");
            }

            PrefabUtility.SaveAsPrefabAsset(instance, FULL_PROFILE_PATH);
            Object.DestroyImmediate(instance);

            Debug.Log("[ProfilePanelSetup] FullProfilePanel setup completed");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProfilePanelSetup] Error: {e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("Error", $"오류 발생: {e.Message}", "OK");
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
            return false;
        }
    }

    private static bool SetupMiniProfileInternal(bool forceReset)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MINI_PROFILE_PATH);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Error", $"MiniProfile.prefab을 찾을 수 없습니다.\n경로: {MINI_PROFILE_PATH}", "OK");
            return false;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            EditorUtility.DisplayDialog("Error", "프리팹 인스턴스 생성 실패", "OK");
            return false;
        }

        try
        {
            // AvatarMask 찾기
            Transform avatarMask = FindChildRecursive(instance.transform, "AvatarMask");
            if (avatarMask != null)
            {
                // 기존 아웃라인 삭제
                Transform existingOutline = avatarMask.parent.Find("AvatarOutline");
                if (existingOutline != null)
                {
                    Object.DestroyImmediate(existingOutline.gameObject);
                }

                // 테두리 추가
                RectTransform maskRect = avatarMask.GetComponent<RectTransform>();
                float borderWidth = maskRect.sizeDelta.x * 0.04f; // 4% of mask size
                CreateAvatarOutline(avatarMask.parent, avatarMask, "AvatarOutline", borderWidth);
            }

            PrefabUtility.SaveAsPrefabAsset(instance, MINI_PROFILE_PATH);
            Object.DestroyImmediate(instance);

            Debug.Log("[ProfilePanelSetup] MiniProfile setup completed");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProfilePanelSetup] Error: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"오류 발생: {e.Message}", "OK");
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
            return false;
        }
    }

    private static bool AddOutlineToFullProfile()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FULL_PROFILE_PATH);
        if (prefab == null) return false;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return false;

        try
        {
            Transform content = FindChildRecursive(instance.transform, "Content");
            Transform avatarMask = FindChildRecursive(content, "AvatarMask");

            if (avatarMask != null)
            {
                // 기존 아웃라인 삭제
                Transform existingOutline = content.Find("AvatarOutline");
                if (existingOutline != null)
                {
                    Object.DestroyImmediate(existingOutline.gameObject);
                }

                RectTransform maskRect = avatarMask.GetComponent<RectTransform>();
                float borderWidth = maskRect.sizeDelta.x * 0.04f;
                CreateAvatarOutline(content, avatarMask, "AvatarOutline", borderWidth);

                // VisibilityStatusText도 추가
                Transform existingVisibility = FindChildRecursive(content, "VisibilityStatusText");
                if (existingVisibility != null)
                {
                    Object.DestroyImmediate(existingVisibility.gameObject);
                }
                CreateVisibilityStatusText(content, avatarMask);

                // Button 추가
                AddButtonToAvatarMask(avatarMask);
            }

            PrefabUtility.SaveAsPrefabAsset(instance, FULL_PROFILE_PATH);
            Object.DestroyImmediate(instance);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
            if (instance != null) Object.DestroyImmediate(instance);
            return false;
        }
    }

    private static bool AddOutlineToMiniProfile()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MINI_PROFILE_PATH);
        if (prefab == null) return false;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return false;

        try
        {
            Transform avatarMask = FindChildRecursive(instance.transform, "AvatarMask");

            if (avatarMask != null)
            {
                // 기존 아웃라인 삭제
                Transform existingOutline = avatarMask.parent.Find("AvatarOutline");
                if (existingOutline != null)
                {
                    Object.DestroyImmediate(existingOutline.gameObject);
                }

                RectTransform maskRect = avatarMask.GetComponent<RectTransform>();
                float borderWidth = maskRect.sizeDelta.x * 0.04f;
                CreateAvatarOutline(avatarMask.parent, avatarMask, "AvatarOutline", borderWidth);
            }

            PrefabUtility.SaveAsPrefabAsset(instance, MINI_PROFILE_PATH);
            Object.DestroyImmediate(instance);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
            if (instance != null) Object.DestroyImmediate(instance);
            return false;
        }
    }

    private static void RemoveOutlineFromPrefab(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        try
        {
            Transform outline = FindChildRecursive(instance.transform, "AvatarOutline");
            if (outline != null)
            {
                Object.DestroyImmediate(outline.gameObject);
            }

            Transform visibility = FindChildRecursive(instance.transform, "VisibilityStatusText");
            if (visibility != null)
            {
                Object.DestroyImmediate(visibility.gameObject);
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// 아바타 테두리 생성 (원형 Outline)
    /// </summary>
    private static void CreateAvatarOutline(Transform parent, Transform avatarMask, string name, float borderWidth)
    {
        // 새 GameObject 생성
        GameObject outlineObj = new GameObject(name);
        outlineObj.transform.SetParent(parent, false);

        // AvatarMask 바로 뒤에 배치 (AvatarMask 아래에 깔리도록)
        int maskIndex = avatarMask.GetSiblingIndex();
        outlineObj.transform.SetSiblingIndex(maskIndex);

        // RectTransform 설정 (AvatarMask보다 약간 크게)
        RectTransform outlineRect = outlineObj.AddComponent<RectTransform>();
        RectTransform maskRect = avatarMask.GetComponent<RectTransform>();

        outlineRect.anchorMin = maskRect.anchorMin;
        outlineRect.anchorMax = maskRect.anchorMax;
        outlineRect.anchoredPosition = maskRect.anchoredPosition;
        outlineRect.pivot = maskRect.pivot;
        // 테두리 두께만큼 크게
        outlineRect.sizeDelta = maskRect.sizeDelta + new Vector2(borderWidth * 2, borderWidth * 2);

        // CanvasRenderer 추가
        outlineObj.AddComponent<CanvasRenderer>();

        // Image 컴포넌트 추가 (원형 스프라이트 사용)
        Image outlineImage = outlineObj.AddComponent<Image>();
        outlineImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        outlineImage.type = Image.Type.Simple;
        outlineImage.color = PUBLIC_COLOR; // 기본값: 전체공개 (핑크 #E95383)
        outlineImage.raycastTarget = false;

        Debug.Log($"[ProfilePanelSetup] {name} created with size {outlineRect.sizeDelta}, color #{ColorUtility.ToHtmlStringRGB(PUBLIC_COLOR)}");
    }

    /// <summary>
    /// 아바타 공개상태 텍스트 생성
    /// </summary>
    private static void CreateVisibilityStatusText(Transform content, Transform avatarMask)
    {
        GameObject visibilityObj = new GameObject("VisibilityStatusText");
        visibilityObj.transform.SetParent(content, false);

        RectTransform rect = visibilityObj.AddComponent<RectTransform>();
        RectTransform avatarRect = avatarMask.GetComponent<RectTransform>();

        // 아바타 바로 아래에 배치
        float avatarBottom = avatarRect.anchoredPosition.y - avatarRect.sizeDelta.y / 2;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, avatarBottom - 40); // 아바타 아래 40px
        rect.sizeDelta = new Vector2(avatarRect.sizeDelta.x * 2.5f, avatarRect.sizeDelta.y * 0.2f);

        visibilityObj.AddComponent<CanvasRenderer>();

        // AppleSDGothicNeoM 폰트 로드
        Font appleFont = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);

        Text text = visibilityObj.AddComponent<Text>();
        text.text = "Avatar Visibility: Public"; // 런타임에 다국어로 변경됨
        text.font = appleFont != null ? appleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = Mathf.RoundToInt(avatarRect.sizeDelta.y * 0.1f);
        text.alignment = TextAnchor.MiddleCenter;
        text.color = PUBLIC_COLOR;

        // UsernameText 위에 배치
        Transform usernameText = FindChildRecursive(content, "UsernameText");
        if (usernameText != null)
        {
            int usernameIndex = usernameText.GetSiblingIndex();
            visibilityObj.transform.SetSiblingIndex(usernameIndex);
        }

        Debug.Log($"[ProfilePanelSetup] VisibilityStatusText created at ({rect.anchoredPosition.x}, {rect.anchoredPosition.y})");
    }

    /// <summary>
    /// AvatarMask에 Button 컴포넌트 추가
    /// </summary>
    private static void AddButtonToAvatarMask(Transform avatarMask)
    {
        Button existingBtn = avatarMask.GetComponent<Button>();
        if (existingBtn != null)
        {
            Debug.Log("[ProfilePanelSetup] AvatarMask already has Button component");
            return;
        }

        Button btn = avatarMask.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;

        // Image가 있으면 targetGraphic으로 설정
        Image img = avatarMask.GetComponent<Image>();
        if (img != null)
        {
            btn.targetGraphic = img;
        }

        Debug.Log("[ProfilePanelSetup] Button component added to AvatarMask");
    }

    /// <summary>
    /// 모든 텍스트 컴포넌트에 AppleSDGothicNeoM 폰트 적용
    /// </summary>
    private static int ApplyFontToAllTexts(Transform parent)
    {
        int count = 0;

        // Legacy UI Text용 폰트 로드
        Font appleFont = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
        if (appleFont == null)
        {
            Debug.LogWarning($"[ProfilePanelSetup] Font not found at {FONT_PATH}");
            return 0;
        }

        // TMP용 폰트 로드
        TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMP_FONT_PATH);

        count += ApplyFontRecursive(parent, appleFont, tmpFont);

        return count;
    }

    private static int ApplyFontRecursive(Transform parent, Font legacyFont, TMP_FontAsset tmpFont)
    {
        int count = 0;

        // Legacy Text 처리
        Text legacyText = parent.GetComponent<Text>();
        if (legacyText != null && legacyFont != null)
        {
            legacyText.font = legacyFont;
            count++;
        }

        // TextMeshPro 처리
        TextMeshProUGUI tmpText = parent.GetComponent<TextMeshProUGUI>();
        if (tmpText != null && tmpFont != null)
        {
            tmpText.font = tmpFont;
            count++;
        }

        // 자식들 재귀 처리
        foreach (Transform child in parent)
        {
            count += ApplyFontRecursive(child, legacyFont, tmpFont);
        }

        return count;
    }

    /// <summary>
    /// 모든 자식 요소 크기/위치 확대
    /// </summary>
    private static void ScaleAllChildren(Transform parent, float scale)
    {
        foreach (Transform child in parent)
        {
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition *= scale;
                rect.sizeDelta *= scale;

                Text legacyText = child.GetComponent<Text>();
                if (legacyText != null)
                {
                    legacyText.fontSize = Mathf.RoundToInt(legacyText.fontSize * scale);
                }

                TextMeshProUGUI tmpText = child.GetComponent<TextMeshProUGUI>();
                if (tmpText != null)
                {
                    tmpText.fontSize *= scale;
                }

                ScaleAllChildren(child, scale);
            }
        }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;

        Transform found = parent.Find(name);
        if (found != null) return found;

        foreach (Transform child in parent)
        {
            found = FindChildRecursive(child, name);
            if (found != null) return found;
        }

        return null;
    }
}
