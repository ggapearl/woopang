using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class FirstTimeGuide : MonoBehaviour
{
    [SerializeField] private GameObject guidePanel;      // 전체 안내 패널
    [SerializeField] private GameObject[] guidePages;    // 사용자가 디자인한 6개 페이지 (길이 6)
    [SerializeField] private Text guideText;            // 단일 텍스트 오브젝트
    [SerializeField] private Button nextButton;          // 다음 버튼
    [SerializeField] private Button previousButton;      // 이전 버튼
    [SerializeField] private Button confirmButton;       // 확인 버튼
    [SerializeField] private Text debugText;            // 디버깅용 텍스트 필드

    private const string FIRST_TIME_KEY = "IsFirstTime"; // PlayerPrefs 키
    private int currentPage = 0;                         // 현재 페이지 인덱스
    private float delayBeforeGuide = 6f;                // 시작 후 6초 대기
    private StringBuilder debugLog = new StringBuilder(); // 디버깅 로그 누적

    // 언어별 안내 템플릿 (6단계 배열, 영어 기본)
    private Dictionary<string, string[]> guideTemplates = new Dictionary<string, string[]>
    {
        { "en", new string[] {
            "You can register your current location by pressing the '+' button at the top(Server approval takes 1-2 hours)",
            "You can check nearby places by pressing the bottom-left button",
            "You can check messages by pressing the bottom-right button",
            "Follow the arrows that appear at the edges of the screen to discover AR models",
            "Touch the AR model objects to check information about that location",
            "Enjoy WOOPANG!" } },
        { "ko", new string[] {
            "상단 '+' 버튼을 눌러 현재 위치한 장소를 등록할 수 있어요(서버 승인 1-2시간 소요)",
            "좌측하단 버튼을 눌러 근처 장소를 확인할 수 있어요",
            "우측하단 버튼을 눌러 메세지를 확인할 수 있어요",
            "화면 모서리에 발생한 화살표를 따라가면 AR모형을 발견할 수 있어요",
            "AR모형의 오브젝트를 터치하여 해당 장소의 정보를 확인할 수 있어요",
            "즐거운 우팡하세요" } },
        { "ja", new string[] {
            "上部の「+」ボタンを押して現在地を登録できます（サーバー承認に1～2時間かかります）",
            "左下のボタンを押して近くの場所を確認できます",
            "右下のボタンを押してメッセージを確認できます",
            "画面の端に表示される矢印に従ってARモデルを発見できます",
            "ARモデルのオブジェクトをタッチしてその場所の情報を確認できます",
            "Woopangを楽しんでください！" } },
        { "zh", new string[] {
            "按顶部"+"按钮可以注册当前位置（服务器审批需要1-2小时）",
            "按左下角按钮可以查看附近地点",
            "按右下角按钮可以查看消息",
            "跟随屏幕边缘出现的箭头可以发现AR模型",
            "触摸AR模型对象可以查看该地点的信息",
            "享受Woopang吧！" } },
        { "es", new string[] {
            "Puedes registrar tu ubicación actual presionando el botón '+' en la parte superior(La aprobación del servidor toma 1-2 horas)",
            "Puedes verificar lugares cercanos presionando el botón inferior izquierdo",
            "Puedes verificar mensajes presionando el botón inferior derecho",
            "Sigue las flechas que aparecen en los bordes de la pantalla para descubrir modelos AR",
            "Toca los objetos del modelo AR para verificar información sobre esa ubicación",
            "¡Disfruta de WOOPANG!" } }
    };

    void Awake()
    {
        LogDebug("FirstTimeGuide: Awake 호출");
    }

    void Start()
    {
        LogDebug("FirstTimeGuide: Start 호출");
        LogDebug($"PlayerPrefs IsFirstTime 값: {PlayerPrefs.GetInt(FIRST_TIME_KEY, 0)}");
        LogDebug($"GameObject 활성화 상태: {gameObject.activeInHierarchy}");

        // UI 요소 검증
        if (guidePanel == null) LogDebug("guidePanel is null", true);
        if (guidePages == null) LogDebug("guidePages is null", true);
        if (guidePages != null) LogDebug($"guidePages 길이: {guidePages.Length}");
        if (guideText == null) LogDebug("guideText is null", true);
        if (nextButton == null) LogDebug("nextButton is null", true);
        if (previousButton == null) LogDebug("previousButton is null", true);
        if (confirmButton == null) LogDebug("confirmButton is null", true);
        if (debugText != null) LogDebug("debugText 연결됨");
        else LogDebug("debugText 연결되지 않음");

        if (guidePanel == null || guidePages == null || guidePages.Length != 6 || 
            guideText == null || nextButton == null || previousButton == null || 
            confirmButton == null)
        {
            LogDebug("FirstTimeGuide: UI 요소가 연결되지 않았습니다!", true);
            return;
        }

        // 버튼 리스너 추가
        nextButton.onClick.AddListener(OnNextButtonClicked);
        previousButton.onClick.AddListener(OnPreviousButtonClicked);
        confirmButton.onClick.AddListener(OnConfirmButtonClicked);

        LogDebug("FirstTimeGuide: 버튼 리스너 추가 완료");
        StartCoroutine(StartGuideSequence());
    }

    private IEnumerator StartGuideSequence()
    {
        LogDebug("StartGuideSequence: 시작");
        yield return new WaitForSeconds(delayBeforeGuide);
        LogDebug("StartGuideSequence: 6초 대기 완료");

        if (IsFirstTime())
        {
            LogDebug("StartGuideSequence: 최초 실행 감지");
            ShowGuide();
            SetFirstTimeFlag();
        }
        else
        {
            LogDebug("StartGuideSequence: 최초 실행 아님 - 가이드 스킵");
            guidePanel.SetActive(false);
        }
    }

    private bool IsFirstTime()
    {
        bool isFirst = PlayerPrefs.GetInt(FIRST_TIME_KEY, 0) == 0;
        LogDebug($"IsFirstTime: {isFirst}");
        return isFirst;
    }

    private void SetFirstTimeFlag()
    {
        PlayerPrefs.SetInt(FIRST_TIME_KEY, 1);
        PlayerPrefs.Save();
        LogDebug("SetFirstTimeFlag: PlayerPrefs 저장 완료");
    }

    private void ShowGuide()
    {
        LogDebug("ShowGuide: 가이드 표시 시작");
        guidePanel.SetActive(true);
        string languageCode = GetLanguageCode();
        LogDebug($"ShowGuide: 언어 코드 - {languageCode}");

        guideText.text = guideTemplates[languageCode][currentPage];
        for (int i = 0; i < 6; i++)
        {
            guidePages[i].SetActive(i == currentPage);
            LogDebug($"ShowGuide: guidePages[{i}] 활성화 상태 - {guidePages[i].activeSelf}");
        }

        UpdateButtons();
        LogDebug($"ShowGuide: 가이드 표시 완료 - 페이지: {currentPage}");
    }

    private void UpdateButtons()
    {
        previousButton.gameObject.SetActive(currentPage > 0);
        nextButton.gameObject.SetActive(currentPage < 5);
        confirmButton.gameObject.SetActive(currentPage == 5);
        LogDebug($"UpdateButtons: 이전 버튼 - {previousButton.gameObject.activeSelf}, " +
                 $"다음 버튼 - {nextButton.gameObject.activeSelf}, " +
                 $"확인 버튼 - {confirmButton.gameObject.activeSelf}");
    }

    private string GetLanguageCode()
    {
        string code;
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                code = "ko";
                break;
            case SystemLanguage.Japanese:
                code = "ja";
                break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                code = "zh";
                break;
            case SystemLanguage.Spanish:
                code = "es";
                break;
            case SystemLanguage.English:
            default:
                code = "en";
                break;
        }
        LogDebug($"GetLanguageCode: {code}");
        return code;
    }

    private void OnNextButtonClicked()
    {
        if (currentPage < 5)
        {
            guidePages[currentPage].SetActive(false);
            currentPage++;
            guidePages[currentPage].SetActive(true);
            guideText.text = guideTemplates[GetLanguageCode()][currentPage];
            UpdateButtons();
            LogDebug($"OnNextButtonClicked: 다음 페이지 - {currentPage}");
        }
    }

    private void OnPreviousButtonClicked()
    {
        if (currentPage > 0)
        {
            guidePages[currentPage].SetActive(false);
            currentPage--;
            guidePages[currentPage].SetActive(true);
            guideText.text = guideTemplates[GetLanguageCode()][currentPage];
            UpdateButtons();
            LogDebug($"OnPreviousButtonClicked: 이전 페이지 - {currentPage}");
        }
    }

    private void OnConfirmButtonClicked()
    {
        LogDebug("OnConfirmButtonClicked: 안내 확인 완료");
        guidePanel.SetActive(false);
    }

    // 가이드 강제 표시 (디버깅용)
    public void ForceShowGuide()
    {
        LogDebug("ForceShowGuide: 가이드 강제 표시");
        PlayerPrefs.DeleteKey(FIRST_TIME_KEY);
        PlayerPrefs.Save();
        ShowGuide();
    }

    // 디버깅 로그 출력 (콘솔과 debugText에 동시 출력)
    private void LogDebug(string message, bool isError = false)
    {
        if (isError)
            Debug.LogError(message);
        else
            Debug.Log(message);

        if (debugText != null)
        {
            debugLog.AppendLine($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            // 최대 10줄만 유지
            string[] lines = debugLog.ToString().Split('\n');
            if (lines.Length > 10)
            {
                debugLog.Clear();
                debugLog.Append(string.Join("\n", lines, lines.Length - 10, 10));
            }
            debugText.text = debugLog.ToString();
        }
    }
}