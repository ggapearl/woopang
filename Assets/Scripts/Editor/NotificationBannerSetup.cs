using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

/// <summary>
/// FirebaseNotification 인앱 알림 배너 UI 자동 생성
/// 씬 로드 시 자동으로 UI 생성 및 연결
/// </summary>
[InitializeOnLoad]
public class NotificationBannerSetup
{
    private const float BANNER_HEIGHT = 120f;
    private const string BANNER_NAME = "NotificationBanner";

    static NotificationBannerSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndSetupBanner;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndSetupBannerSilent;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckAndSetupBannerSilent;
    }

    private static void CheckAndSetupBanner()
    {
        CheckAndSetupBannerSilent();
    }

    private static void CheckAndSetupBannerSilent()
    {
        if (Application.isPlaying) return;

        FirebaseNotification firebaseNotification = Object.FindFirstObjectByType<FirebaseNotification>();
        if (firebaseNotification == null)
        {
            return;
        }

        // 이미 배너가 연결되어 있으면 스킵
        if (firebaseNotification.notificationBanner != null)
        {
            return;
        }

        // Canvas 찾기
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        // 기존 배너 찾기
        Transform existingBanner = canvas.transform.Find(BANNER_NAME);
        if (existingBanner != null)
        {
            // 기존 배너 연결
            ConnectBannerToFirebase(firebaseNotification, existingBanner.gameObject);
            return;
        }

        // 새 배너 생성
        CreateNotificationBanner(firebaseNotification, canvas);
    }

    private static void CreateNotificationBanner(FirebaseNotification firebaseNotification, Canvas canvas)
    {
        // 배너 패널 생성
        GameObject banner = new GameObject(BANNER_NAME);
        banner.transform.SetParent(canvas.transform, false);

        // RectTransform 설정 (화면 상단, SafeArea 고려)
        RectTransform bannerRect = banner.AddComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0, 1);
        bannerRect.anchorMax = new Vector2(1, 1);
        bannerRect.pivot = new Vector2(0.5f, 1);
        bannerRect.anchoredPosition = new Vector2(0, BANNER_HEIGHT); // 초기에는 숨김
        bannerRect.sizeDelta = new Vector2(0, BANNER_HEIGHT);

        // 배경 이미지
        Image bgImage = banner.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);

        // 버튼 (클릭 영역)
        Button bannerButton = banner.AddComponent<Button>();
        ColorBlock colors = bannerButton.colors;
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.28f, 1f);
        colors.pressedColor = new Color(0.25f, 0.25f, 0.35f, 1f);
        bannerButton.colors = colors;

        // 콘텐츠 컨테이너 (SafeArea 패딩용)
        GameObject contentContainer = new GameObject("Content");
        contentContainer.transform.SetParent(banner.transform, false);
        RectTransform contentRect = contentContainer.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(20, 15);
        contentRect.offsetMax = new Vector2(-20, -35); // 상단 SafeArea 여유

        // 제목 텍스트
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(contentContainer.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.fontSize = 30;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.text = "새 메시지";

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.55f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // 내용 텍스트
        GameObject bodyObj = new GameObject("BodyText");
        bodyObj.transform.SetParent(contentContainer.transform, false);
        Text bodyText = bodyObj.AddComponent<Text>();
        bodyText.fontSize = 24;
        bodyText.color = new Color(0.75f, 0.75f, 0.8f, 1f);
        bodyText.alignment = TextAnchor.MiddleLeft;
        bodyText.text = "메시지 내용이 여기에 표시됩니다.";

        RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0, 0);
        bodyRect.anchorMax = new Vector2(1, 0.5f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;

        // 폰트 로드 (AppleSDGothicNeoM)
        Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
        if (customFont == null)
        {
            customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/AppleSDGothicNeoM.ttf");
        }
        if (customFont != null)
        {
            titleText.font = customFont;
            bodyText.font = customFont;
        }
        else
        {
            // 기본 폰트 사용
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // 초기에는 숨김
        banner.SetActive(false);

        // 가장 앞에 표시
        banner.transform.SetAsLastSibling();

        // FirebaseNotification에 연결
        ConnectBannerToFirebase(firebaseNotification, banner);

        // 씬 저장 표시
        EditorUtility.SetDirty(firebaseNotification);
        EditorSceneManager.MarkSceneDirty(firebaseNotification.gameObject.scene);

        Debug.Log("[NotificationBannerSetup] 알림 배너 UI 생성 및 연결 완료");
    }

    private static void ConnectBannerToFirebase(FirebaseNotification firebaseNotification, GameObject banner)
    {
        firebaseNotification.notificationBanner = banner;
        firebaseNotification.bannerTitleText = banner.transform.Find("Content/TitleText")?.GetComponent<Text>();
        firebaseNotification.bannerBodyText = banner.transform.Find("Content/BodyText")?.GetComponent<Text>();
        firebaseNotification.bannerButton = banner.GetComponent<Button>();

        // 버튼 클릭 이벤트 연결
        if (firebaseNotification.bannerButton != null)
        {
            firebaseNotification.bannerButton.onClick.RemoveAllListeners();
            firebaseNotification.bannerButton.onClick.AddListener(firebaseNotification.OnNotificationBannerClicked);
        }

        EditorUtility.SetDirty(firebaseNotification);
    }

    [MenuItem("WOOPANG/Setup/Notification Banner 재생성")]
    private static void ForceRecreateNotificationBanner()
    {
        FirebaseNotification firebaseNotification = Object.FindFirstObjectByType<FirebaseNotification>();
        if (firebaseNotification == null)
        {
            EditorUtility.DisplayDialog("오류", "FirebaseNotification을 찾을 수 없습니다.", "확인");
            return;
        }

        // 기존 배너 삭제
        if (firebaseNotification.notificationBanner != null)
        {
            Object.DestroyImmediate(firebaseNotification.notificationBanner);
            firebaseNotification.notificationBanner = null;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "Canvas를 찾을 수 없습니다.", "확인");
            return;
        }

        // 기존 배너 오브젝트도 삭제
        Transform existingBanner = canvas.transform.Find(BANNER_NAME);
        if (existingBanner != null)
        {
            Object.DestroyImmediate(existingBanner.gameObject);
        }

        CreateNotificationBanner(firebaseNotification, canvas);
        EditorUtility.DisplayDialog("완료", "알림 배너 UI가 재생성되었습니다.", "확인");
    }
}
