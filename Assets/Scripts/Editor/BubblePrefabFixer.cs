using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;

/// <summary>
/// 메시지 버블 프리팹 수정 에디터
/// - TimeArea/TimeText 제거 (DateSeparator로 대체됨)
/// - OtherMessageBubble에 아바타 원형 마스크 추가
/// - BubbleContainer CSF 제거 (Unity 레이아웃 그룹 자식 CSF 버그 대응)
/// - 레이아웃 설정 자동 복구 (HLG 깨진 설정 감지 및 수정)
/// </summary>
public static class BubblePrefabFixer
{
    private const string PREFAB_PATH = "Assets/Prefabs/DM/";

    // ============================================================
    // 스크립트 리로드 시 자동으로 프리팹 레이아웃 설정 검증 및 복구
    // ============================================================
    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += FixBrokenLayoutSettings;
    }

    /// <summary>
    /// 모든 버블 프리팹의 레이아웃 설정을 검증하고 복구
    /// - Root HLG: childControlWidth=false, childControlHeight=true
    /// - BubbleContainer CSF: 완전 제거 (Unity 레이아웃 그룹 자식 CSF 버그)
    /// - BubbleContainer: LayoutElement preferredWidth=400 (기본 폭), preferredHeight=-1 (VLG 자동)
    /// - AvatarContainer: sizeDelta 복구
    /// </summary>
    private static void FixBrokenLayoutSettings()
    {
        string[] prefabNames = { "AdminMessageBubble", "OtherMessageBubble", "MyMessageBubble" };
        bool anyFixed = false;

        foreach (string prefabName in prefabNames)
        {
            string path = PREFAB_PATH + prefabName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);
            bool modified = false;

            // 1. Root HLG 설정
            //    childControlWidth=false: 자식 폭은 sizeDelta/LayoutElement로 직접 관리
            //    childControlHeight=true: HLG가 VLG preferred height 기반으로 높이 자동 설정
            HorizontalLayoutGroup rootHLG = instance.GetComponent<HorizontalLayoutGroup>();
            if (rootHLG != null)
            {
                if (rootHLG.childControlWidth)
                {
                    rootHLG.childControlWidth = false;
                    modified = true;
                    Debug.Log($"[BubblePrefabFixer] {prefabName}: Root HLG childControlWidth → false");
                }
                if (!rootHLG.childControlHeight)
                {
                    rootHLG.childControlHeight = true;
                    modified = true;
                    Debug.Log($"[BubblePrefabFixer] {prefabName}: Root HLG childControlHeight → true");
                }
            }

            // 2. BubbleContainer 설정
            //    CSF 제거 (HLG 자식에서 높이 잘못 계산하는 Unity 버그)
            //    BubbleAutoSizer 추가 (TextGenerator로 정확한 높이 계산)
            //    LayoutElement + sizeDelta.x 기본 폭 설정
            Transform bubbleContainer = instance.transform.Find("BubbleContainer");
            if (bubbleContainer != null)
            {
                // CSF 제거
                ContentSizeFitter csf = bubbleContainer.GetComponent<ContentSizeFitter>();
                if (csf != null)
                {
                    Object.DestroyImmediate(csf);
                    modified = true;
                    Debug.Log($"[BubblePrefabFixer] {prefabName}: BubbleContainer CSF 제거됨");
                }

                // LayoutElement 확인 (preferredWidth=400 기본 폭)
                LayoutElement bubbleLE = bubbleContainer.GetComponent<LayoutElement>();
                if (bubbleLE == null)
                {
                    bubbleLE = bubbleContainer.gameObject.AddComponent<LayoutElement>();
                    modified = true;
                    Debug.Log($"[BubblePrefabFixer] {prefabName}: BubbleContainer LayoutElement 추가");
                }
                if (bubbleLE.preferredWidth < 0)
                {
                    bubbleLE.preferredWidth = 400f;
                    modified = true;
                }
                // preferredHeight는 BubbleAutoSizer가 동적으로 설정 (건드리지 않음)

                // BubbleAutoSizer 추가 (TextGenerator로 정확한 높이 계산)
                BubbleAutoSizer autoSizer = bubbleContainer.GetComponent<BubbleAutoSizer>();
                if (autoSizer == null)
                {
                    bubbleContainer.gameObject.AddComponent<BubbleAutoSizer>();
                    modified = true;
                    Debug.Log($"[BubblePrefabFixer] {prefabName}: BubbleAutoSizer 추가됨");
                }

                // sizeDelta.x 기본값 설정 (폭만, 높이는 HLG + BubbleAutoSizer가 제어)
                RectTransform bubbleRect = bubbleContainer.GetComponent<RectTransform>();
                if (bubbleRect != null && bubbleRect.sizeDelta.x < 10f)
                {
                    bubbleRect.sizeDelta = new Vector2(400f, bubbleRect.sizeDelta.y);
                    modified = true;
                    Debug.Log($"[BubblePrefabFixer] {prefabName}: BubbleContainer sizeDelta.x → 400 설정");
                }
            }

            // 3. AvatarContainer sizeDelta 복구 (childControlWidth=1 때 압축된 폭 복원)
            Transform avatarContainer = instance.transform.Find("AvatarContainer");
            if (avatarContainer != null)
            {
                RectTransform avatarRect = avatarContainer.GetComponent<RectTransform>();
                LayoutElement avatarLE = avatarContainer.GetComponent<LayoutElement>();
                if (avatarRect != null && avatarRect.sizeDelta.x < 50f)
                {
                    float targetSize = (avatarLE != null && avatarLE.preferredWidth > 0)
                        ? avatarLE.preferredWidth : 80f;
                    avatarRect.sizeDelta = new Vector2(targetSize, targetSize);
                    modified = true;
                    Debug.Log($"[BubblePrefabFixer] {prefabName}: AvatarContainer sizeDelta → ({targetSize}, {targetSize}) 복구");
                }
            }

            if (modified)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
                anyFixed = true;
            }

            PrefabUtility.UnloadPrefabContents(instance);
        }

        if (anyFixed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[BubblePrefabFixer] 프리팹 레이아웃 설정 복구 완료!");
        }
    }

    [MenuItem("WOOPANG/Prefab Fix/모든 버블 프리팹 수정")]
    public static void FixAllBubblePrefabs()
    {
        FixAdminMessageBubble();
        FixOtherMessageBubble();
        FixMyMessageBubble();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BubblePrefabFixer] 모든 버블 프리팹 수정 완료!");
        EditorUtility.DisplayDialog("완료", "모든 버블 프리팹이 수정되었습니다.\n\n- TimeArea/TimeText 제거됨\n- OtherMessageBubble에 아바타 추가됨", "확인");
    }

    [MenuItem("WOOPANG/Prefab Fix/AdminMessageBubble 수정")]
    public static void FixAdminMessageBubble()
    {
        string path = PREFAB_PATH + "AdminMessageBubble.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[BubblePrefabFixer] 프리팹을 찾을 수 없음: {path}");
            return;
        }

        // 프리팹 편집 시작
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);

        bool modified = false;

        // 1. TimeArea 제거
        Transform timeArea = instance.transform.Find("TimeArea");
        if (timeArea != null)
        {
            Object.DestroyImmediate(timeArea.gameObject);
            modified = true;
            Debug.Log("[BubblePrefabFixer] AdminMessageBubble: TimeArea 제거됨");
        }

        // 2. AvatarContainer 확인 및 원형 마스크 설정
        Transform avatarContainer = instance.transform.Find("AvatarContainer");
        if (avatarContainer != null)
        {
            // Mask 컴포넌트 확인
            Mask mask = avatarContainer.GetComponent<Mask>();
            if (mask == null)
            {
                mask = avatarContainer.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
                modified = true;
                Debug.Log("[BubblePrefabFixer] AdminMessageBubble: AvatarContainer에 Mask 추가됨");
            }

            // Image 컴포넌트 - 원형 스프라이트 설정
            Image avatarImg = avatarContainer.GetComponent<Image>();
            if (avatarImg != null)
            {
                // Knob 스프라이트 (Unity 기본 원형)
                avatarImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                avatarImg.preserveAspect = true;
            }

            // LayoutElement로 크기 고정
            LayoutElement le = avatarContainer.GetComponent<LayoutElement>();
            if (le == null)
                le = avatarContainer.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 100;
            le.preferredHeight = 100;
        }

        // 3. HorizontalLayoutGroup padding 조정 (TimeArea 제거로 인한 여백 조정)
        HorizontalLayoutGroup hlg = instance.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.padding.right = 16; // TimeArea 제거로 우측 패딩 축소
            modified = true;
        }

        if (modified)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            Debug.Log("[BubblePrefabFixer] AdminMessageBubble 저장됨");
        }

        PrefabUtility.UnloadPrefabContents(instance);
    }

    [MenuItem("WOOPANG/Prefab Fix/OtherMessageBubble 수정")]
    public static void FixOtherMessageBubble()
    {
        string path = PREFAB_PATH + "OtherMessageBubble.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[BubblePrefabFixer] 프리팹을 찾을 수 없음: {path}");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(prefab);
        GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);

        bool modified = false;

        // 1. TimeText 제거
        Transform timeText = instance.transform.Find("TimeText");
        if (timeText != null)
        {
            Object.DestroyImmediate(timeText.gameObject);
            modified = true;
            Debug.Log("[BubblePrefabFixer] OtherMessageBubble: TimeText 제거됨");
        }

        // 2. Avatar 컨테이너 추가 (없으면)
        Transform avatarContainer = instance.transform.Find("AvatarContainer");
        if (avatarContainer == null)
        {
            avatarContainer = instance.transform.Find("Avatar");
        }

        if (avatarContainer == null)
        {
            // AvatarContainer 생성
            GameObject avatarObj = new GameObject("AvatarContainer");
            avatarObj.transform.SetParent(instance.transform, false);
            avatarObj.transform.SetAsFirstSibling(); // 맨 앞에 배치

            RectTransform avatarRect = avatarObj.AddComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0, 0);
            avatarRect.anchorMax = new Vector2(0, 0);
            avatarRect.pivot = new Vector2(0.5f, 0.5f);
            avatarRect.sizeDelta = new Vector2(80, 80);

            // Image 추가 (원형 마스크용)
            Image avatarImg = avatarObj.AddComponent<Image>();
            avatarImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            avatarImg.preserveAspect = true;
            avatarImg.color = Color.white;

            // Mask 추가 (원형 클리핑)
            Mask mask = avatarObj.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // LayoutElement 추가
            LayoutElement le = avatarObj.AddComponent<LayoutElement>();
            le.preferredWidth = 80;
            le.preferredHeight = 80;

            avatarContainer = avatarObj.transform;
            modified = true;
            Debug.Log("[BubblePrefabFixer] OtherMessageBubble: AvatarContainer 생성됨 (원형 마스크 포함)");
        }
        else
        {
            // 기존 Avatar에 Mask 확인
            Mask mask = avatarContainer.GetComponent<Mask>();
            if (mask == null)
            {
                // Image가 먼저 있어야 함
                Image img = avatarContainer.GetComponent<Image>();
                if (img == null)
                {
                    img = avatarContainer.gameObject.AddComponent<Image>();
                    img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                    img.preserveAspect = true;
                }

                mask = avatarContainer.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
                modified = true;
                Debug.Log("[BubblePrefabFixer] OtherMessageBubble: Avatar에 Mask 추가됨");
            }
        }

        // 3. HorizontalLayoutGroup padding 조정
        HorizontalLayoutGroup hlg = instance.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.padding.right = 16; // TimeText 제거로 우측 패딩 축소
            modified = true;
        }

        if (modified)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            Debug.Log("[BubblePrefabFixer] OtherMessageBubble 저장됨");
        }

        PrefabUtility.UnloadPrefabContents(instance);
    }

    [MenuItem("WOOPANG/Prefab Fix/MyMessageBubble 수정")]
    public static void FixMyMessageBubble()
    {
        string path = PREFAB_PATH + "MyMessageBubble.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[BubblePrefabFixer] 프리팹을 찾을 수 없음: {path}");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(prefab);
        GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);

        bool modified = false;

        // 1. TimeText 제거
        Transform timeText = instance.transform.Find("TimeText");
        if (timeText != null)
        {
            Object.DestroyImmediate(timeText.gameObject);
            modified = true;
            Debug.Log("[BubblePrefabFixer] MyMessageBubble: TimeText 제거됨");
        }

        // 2. HorizontalLayoutGroup padding 조정
        HorizontalLayoutGroup hlg = instance.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.padding.left = 16; // TimeText 제거로 좌측 패딩 축소
            modified = true;
        }

        if (modified)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            Debug.Log("[BubblePrefabFixer] MyMessageBubble 저장됨");
        }

        PrefabUtility.UnloadPrefabContents(instance);
    }
}
