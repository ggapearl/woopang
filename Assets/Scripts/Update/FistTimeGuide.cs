using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class FirstTimeGuide : MonoBehaviour
{
    [SerializeField] private GameObject guidePanel;      // 전체 안내 패널
    [SerializeField] private GameObject[] guidePages;    // 사용자가 디자인한 6개 페이지 (길이 6)
    [SerializeField] private Text guideText;            // 단일 텍스트 오브젝트
    [SerializeField] private Button nextButton;          // 다음 버튼
    [SerializeField] private Button previousButton;      // 이전 버튼
    [SerializeField] private Button confirmButton;       // 확인 버튼

    private const string FIRST_TIME_KEY = "IsFirstTime"; // PlayerPrefs 키
    private int currentPage = 0;                         // 현재 페이지 인덱스
    private float delayBeforeGuide = 6f;                // 시작 후 6초 대기

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
    }

    void Start()
    {
        if (guidePanel == null || guidePages == null || guidePages.Length != 6 || 
            guideText == null || nextButton == null || previousButton == null || 
            confirmButton == null)
        {
            Debug.LogError("FirstTimeGuide: UI 요소가 연결되지 않았습니다!");
            return;
        }

        // 버튼 리스너 추가
        nextButton.onClick.AddListener(OnNextButtonClicked);
        previousButton.onClick.AddListener(OnPreviousButtonClicked);
        confirmButton.onClick.AddListener(OnConfirmButtonClicked);

        StartCoroutine(StartGuideSequence());
    }

    private IEnumerator StartGuideSequence()
    {
        yield return new WaitForSeconds(delayBeforeGuide);

        if (IsFirstTime())
        {
            ShowGuide();
            SetFirstTimeFlag();
        }
        else
        {
            guidePanel.SetActive(false);
        }
    }

    private bool IsFirstTime()
    {
        bool isFirst = PlayerPrefs.GetInt(FIRST_TIME_KEY, 0) == 0;
        return isFirst;
    }

    private void SetFirstTimeFlag()
    {
        PlayerPrefs.SetInt(FIRST_TIME_KEY, 1);
        PlayerPrefs.Save();
    }

    private void ShowGuide()
    {
        guidePanel.SetActive(true);
        string languageCode = GetLanguageCode();

        guideText.text = guideTemplates[languageCode][currentPage];
        for (int i = 0; i < 6; i++)
        {
            guidePages[i].SetActive(i == currentPage);
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        previousButton.gameObject.SetActive(currentPage > 0);
        nextButton.gameObject.SetActive(currentPage < 5);
        confirmButton.gameObject.SetActive(currentPage == 5);
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
        }
    }

    private void OnConfirmButtonClicked()
    {
        guidePanel.SetActive(false);
    }

    // 가이드 강제 표시 (디버깅용)
    [ContextMenu("Force Show Guide (테스트)")]
    public void ForceShowGuide()
    {
        currentPage = 0;
        PlayerPrefs.DeleteKey(FIRST_TIME_KEY);
        PlayerPrefs.Save();
        ShowGuide();
    }

    // PlayerPrefs 초기화 (처음 실행 상태로 리셋)
    [ContextMenu("Reset First Time Flag (처음 상태로 리셋)")]
    public void ResetFirstTimeFlag()
    {
        PlayerPrefs.DeleteKey(FIRST_TIME_KEY);
        PlayerPrefs.Save();
        Debug.Log("[FirstTimeGuide] First time flag reset - 다음 실행 시 가이드가 표시됩니다.");
    }
}