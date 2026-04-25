using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using System.Reflection;

/// <summary>
/// FirstTimeGuide 씬 자동 구성:
/// 1) 각 페이지(01~06) GameObject를 빈 컨테이너로 변환
///    - 기존 Image 컴포넌트 제거 → 자식 Image 추가
///    - 05/06은 자식 2장 (xx-2 뒤, xx-1 앞)
/// 2) Background 자식 생성 + back.png 연결
/// 3) FirstTimeGuide.backgroundImage / pageHighlights 자동 연결
///
/// 새 이미지 경로: Assets/sou/UI/FirstTime/2026/0425/
///   01.png, 02.png, 03.png, 04.png, 05-1.png, 05-2.png, 06-1.png, 06-2.png, back.png
///
/// 펄스 대상: 01, 02, 03, 05-1, 06-1 (04 / 05-2 / 06-2는 그대로)
/// </summary>
[InitializeOnLoad]
public class FirstTimeGuideSceneSetup
{
    private const string IMG_DIR = "Assets/sou/UI/FirstTime/2026/0425";

    static FirstTimeGuideSceneSetup()
    {
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += Setup;
        EditorApplication.delayCall += Setup;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += Setup;
    }

    [MenuItem("Tools/SetupFirstTimeGuide")]
    public static void RunNow()
    {
        Debug.Log("[FirstTimeGuideSceneSetup] RunNow 진입");
        Setup();
        Debug.Log("[FirstTimeGuideSceneSetup] 수동 실행 완료");
    }

    private static void Setup()
    {
        if (Application.isPlaying) return;

        FirstTimeGuide guide = Object.FindFirstObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
        if (guide == null) return;

        SerializedObject so = new SerializedObject(guide);
        SerializedProperty pagesProp = so.FindProperty("guidePages");
        SerializedProperty bgProp = so.FindProperty("backgroundImage");
        SerializedProperty highlightsProp = so.FindProperty("pageHighlights");
        SerializedProperty panelProp = so.FindProperty("guidePanel");

        if (pagesProp == null || bgProp == null || highlightsProp == null || panelProp == null) return;
        if (pagesProp.arraySize != 6) return;

        GameObject panel = panelProp.objectReferenceValue as GameObject;
        if (panel == null) return;

        bool dirty = false;

        // 1) Background 처리 — guidePanel 자식 'Background' 찾거나 생성
        Sprite backSprite = LoadSprite("back.png");
        Image bgImage = SetupBackground(panel, backSprite, ref dirty);
        if (bgImage != null && bgProp.objectReferenceValue != bgImage)
        {
            bgProp.objectReferenceValue = bgImage;
            dirty = true;
        }

        // 2) 각 페이지 — Image 컴포넌트 제거 + 자식 이미지 구성
        // 페이지 인덱스 → 자식 이미지 파일명 (뒤에서 앞 순서)
        // 0: ["01.png"]  1: ["02.png"]  2: ["03.png"]  3: ["04.png"]
        // 4: ["05-2.png", "05-1.png"]  ← Hierarchy 위(뒤) → 아래(앞)
        // 5: ["06-2.png", "06-1.png"]
        var pageConfigs = new[] {
            new[] { "01.png" },
            new[] { "02.png" },
            new[] { "03.png" },
            new[] { "04.png" },
            new[] { "05-2.png", "05-1.png" },
            new[] { "06-2.png", "06-1.png" }
        };

        Image[] pulseTargetImages = new Image[6];
        // 펄스 대상: 0,1,2 → 자식[0],   4 → 자식[1] (=05-1),   5 → 자식[1] (=06-1)
        // pageIndex=3(04)는 펄스 없음

        for (int i = 0; i < 6; i++)
        {
            GameObject pageGO = pagesProp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (pageGO == null) continue;

            // 기존 Image 제거 (페이지를 빈 컨테이너로 변환)
            Image existingImg = pageGO.GetComponent<Image>();
            if (existingImg != null)
            {
                Object.DestroyImmediate(existingImg, true);
                dirty = true;
            }

            // 자식 이미지 구성
            string[] files = pageConfigs[i];
            for (int c = 0; c < files.Length; c++)
            {
                string childName = "Image_" + System.IO.Path.GetFileNameWithoutExtension(files[c]).Replace("-", "_");
                Transform existing = pageGO.transform.Find(childName);
                Image childImg;
                if (existing != null)
                {
                    childImg = existing.GetComponent<Image>();
                }
                else
                {
                    GameObject childGO = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    childGO.transform.SetParent(pageGO.transform, false);

                    RectTransform crt = childGO.GetComponent<RectTransform>();
                    crt.anchorMin = Vector2.zero;
                    crt.anchorMax = Vector2.one;
                    crt.offsetMin = Vector2.zero;
                    crt.offsetMax = Vector2.zero;
                    crt.localScale = Vector3.one;

                    childImg = childGO.GetComponent<Image>();
                    childImg.raycastTarget = false;
                    childImg.preserveAspect = true;
                    dirty = true;
                }

                Sprite s = LoadSprite(files[c]);
                if (s != null && childImg.sprite != s)
                {
                    childImg.sprite = s;
                    dirty = true;
                }
            }

            // 자식 Hierarchy 순서 정정 — pageConfigs 배열 순서대로 SetSiblingIndex
            for (int c = 0; c < files.Length; c++)
            {
                string childName = "Image_" + System.IO.Path.GetFileNameWithoutExtension(files[c]).Replace("-", "_");
                Transform child = pageGO.transform.Find(childName);
                if (child != null) child.SetSiblingIndex(c);
            }

            // 펄스 대상 결정
            int pulseChildIdx = -1;
            if (i == 0 || i == 1 || i == 2) pulseChildIdx = 0;       // 01, 02, 03
            else if (i == 4) pulseChildIdx = 1;                       // 05-1 (자식 인덱스 1)
            else if (i == 5) pulseChildIdx = 1;                       // 06-1
            // i == 3 (04)는 펄스 없음

            if (pulseChildIdx >= 0 && pulseChildIdx < files.Length)
            {
                string childName = "Image_" + System.IO.Path.GetFileNameWithoutExtension(files[pulseChildIdx]).Replace("-", "_");
                Transform t = pageGO.transform.Find(childName);
                if (t != null) pulseTargetImages[i] = t.GetComponent<Image>();
            }
        }

        // 3) pageHighlights 배열 자동 연결 — 펄스 있는 5개만
        var pulseEntries = new System.Collections.Generic.List<(int idx, Image img)>();
        for (int i = 0; i < 6; i++)
        {
            if (pulseTargetImages[i] != null)
                pulseEntries.Add((i, pulseTargetImages[i]));
        }

        if (highlightsProp.arraySize != pulseEntries.Count)
        {
            highlightsProp.arraySize = pulseEntries.Count;
            dirty = true;
        }

        for (int i = 0; i < pulseEntries.Count; i++)
        {
            SerializedProperty elem = highlightsProp.GetArrayElementAtIndex(i);
            SerializedProperty pageIdx = elem.FindPropertyRelative("pageIndex");
            SerializedProperty graphic = elem.FindPropertyRelative("targetGraphic");
            SerializedProperty scale = elem.FindPropertyRelative("scaleAmplitude");
            SerializedProperty glow = elem.FindPropertyRelative("glowAmplitude");
            SerializedProperty speed = elem.FindPropertyRelative("pulseSpeed");

            if (pageIdx.intValue != pulseEntries[i].idx) { pageIdx.intValue = pulseEntries[i].idx; dirty = true; }
            if (graphic.objectReferenceValue != pulseEntries[i].img) { graphic.objectReferenceValue = pulseEntries[i].img; dirty = true; }
            // 기본값이 0인 경우만 채움 (사용자 수정 보존)
            if (scale.floatValue <= 0.0001f) { scale.floatValue = 0.07f; dirty = true; }
            if (glow.floatValue <= 0.0001f) { glow.floatValue = 0.35f; dirty = true; }
            if (speed.floatValue <= 0.0001f) { speed.floatValue = 2.4f; dirty = true; }
        }

        if (dirty)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(guide);
            EditorSceneManager.MarkSceneDirty(guide.gameObject.scene);
        }
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = IMG_DIR + "/" + fileName;

        // textureType이 Sprite가 아니면 자동 변환
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }

        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[FirstTimeGuideSceneSetup] sprite 로드 실패: {path}");
        return s;
    }

    private static Image SetupBackground(GameObject panel, Sprite backSprite, ref bool dirty)
    {
        Transform existingT = panel.transform.Find("Background");
        Image bgImg;
        if (existingT == null)
        {
            GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(panel.transform, false);

            RectTransform brt = bgGO.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            brt.localScale = Vector3.one;

            bgImg = bgGO.GetComponent<Image>();
            bgImg.raycastTarget = false;
            bgImg.preserveAspect = false; // 배경은 풀스트레치
            bgGO.transform.SetAsFirstSibling();
            dirty = true;
        }
        else
        {
            bgImg = existingT.GetComponent<Image>();
            if (bgImg == null)
            {
                bgImg = existingT.gameObject.AddComponent<Image>();
                dirty = true;
            }
            // 위치/크기 — 풀스트레치 보장
            RectTransform brt = existingT.GetComponent<RectTransform>();
            if (brt.anchorMin != Vector2.zero || brt.anchorMax != Vector2.one
                || brt.offsetMin != Vector2.zero || brt.offsetMax != Vector2.zero)
            {
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                dirty = true;
            }
            existingT.SetAsFirstSibling();
        }

        if (backSprite != null && bgImg.sprite != backSprite)
        {
            bgImg.sprite = backSprite;
            dirty = true;
        }

        return bgImg;
    }
}
