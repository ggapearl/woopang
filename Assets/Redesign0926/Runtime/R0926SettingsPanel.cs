using System.Collections;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 목록 시트 '설정' 탭 — 하늘 화면에 무엇을, 언제 띄울지.
///  · 하늘 화면 켜기(전체) · 날씨 · 동네 이벤트·게임
///  · 보이는 각도(조금만 들어도 / 보통 / 똑바로 위) · 누워서 쓸 땐 숨기기 · 처음엔 접어 두기
///  · 숨긴 장소 수 (인디케이터 X — 앱을 다시 켜면 다시 보인다) · 숨긴 게 있으면 '모두 다시 보이기'
///  · 보물찾기 참여 · 새 게임 알림 · 가까워지면 진동 — 계정 설정이라 서버(/api/hunts/prefs)에 있다. 로그인 전엔 흐리게
///  · 화면 프레임 30 / 60 (기본 30)
///  · 멀리 있는 오브젝트 흐리게 — 끄기 / 약하게 / 보통 / 강하게 (기본 보통)
/// 이벤트는 아직 보낼 내용이 없어 '곧' 표시 — 켜고 끄는 값은 지금부터 저장된다.
/// </summary>
public class R0926SettingsPanel : MonoBehaviour
{
    [Header("보물찾기 (서버 설정)")]
    [SerializeField] private Image huntSwitch;            // 보물찾기 참여
    [SerializeField] private Image huntNewSwitch;         // 새 게임 알림
    [SerializeField] private Image huntNearSwitch;        // 가까워지면 진동
    [SerializeField] private CanvasGroup huntGroup;       // '참여' 칸 — 로그인 전엔 흐리다
    [SerializeField] private CanvasGroup huntAlertGroup;  // 알림 두 줄 칸 — 로그인 전·참여를 끄면 흐리다
    [SerializeField] private Text huntNote;               // 두 칸 아래 한 줄 — 로그인 안내 · 못 불러옴 (아무 일 없으면 숨김)

    [SerializeField] private Image masterSwitch;
    [SerializeField] private Image weatherSwitch;
    [SerializeField] private Image eventsSwitch;
    [SerializeField] private Image lyingSwitch;
    [SerializeField] private Image collapsedSwitch;
    [SerializeField] private CanvasGroup channelGroup;   // 전체를 끄면 아래 항목이 흐려진다
    [SerializeField] private Image[] angleTabs;
    [SerializeField] private Text[] angleLabels;
    [SerializeField] private Image[] fpsTabs;             // 30 FPS / 60 FPS
    [SerializeField] private Text[] fpsLabels;
    [SerializeField] private Image removeSwitch;          // 오브젝트 삭제 기능 (장소 박스 X)
    [SerializeField] private Image[] fadeTabs;            // 멀리 있는 오브젝트 흐리게 — 끄기 / 약하게 / 보통 / 강하게
    [SerializeField] private Text[] fadeLabels;
    [SerializeField] private Text hiddenText;
    [SerializeField] private GameObject showAllRow;       // 모두 다시 보이기 (숨긴 장소가 있을 때만)
    [SerializeField] private Sprite switchOn;
    [SerializeField] private Sprite switchOff;

    private void OnEnable()
    {
        // 패널이 켜진 채로 숨기거나 되돌려도 수가 맞게
        HiddenPlaces.Hidden += OnHiddenChanged;
        HiddenPlaces.Restored += Refresh;
        R0926HuntPrefs.Changed += Refresh;
        huntBusy = false;
        huntFailed = huntLogin = false;
        Refresh();
        if (R0926HuntApi.CanCall) StartCoroutine(LoadHunt());
    }

    private void OnDisable()
    {
        HiddenPlaces.Hidden -= OnHiddenChanged;
        HiddenPlaces.Restored -= Refresh;
        R0926HuntPrefs.Changed -= Refresh;
    }

    private void OnHiddenChanged(string uniqueId) { Refresh(); }

    public void ToggleMaster() { R0926SkySettings.Enabled = !R0926SkySettings.Enabled; Refresh(); }
    public void ToggleWeather() { R0926SkySettings.Weather = !R0926SkySettings.Weather; Refresh(); }
    public void ToggleEvents() { R0926SkySettings.Events = !R0926SkySettings.Events; Refresh(); }
    public void ToggleLying() { R0926SkySettings.HideWhenLying = !R0926SkySettings.HideWhenLying; Refresh(); }
    public void ToggleCollapsed() { R0926SkySettings.StartCollapsed = !R0926SkySettings.StartCollapsed; Refresh(); }
    public void SetAngle(int step) { R0926SkySettings.Angle = step; Refresh(); }
    public void SetFps(int step) { R0926FrameRate.Fps = step == 1 ? 60 : 30; Refresh(); }
    public void ToggleRemove() { R0926PlaceSettings.RemoveButton = !R0926PlaceSettings.RemoveButton; Refresh(); }
    public void SetFarFade(int step) { R0926PlaceSettings.FarFade = step; Refresh(); }
    public void ShowAllHidden() { HiddenPlaces.ShowAll(); Refresh(); }
    public void ToggleHunt() => ToggleHuntPref("enabled");
    public void ToggleHuntNew() => ToggleHuntPref("notify_new");
    public void ToggleHuntNear() => ToggleHuntPref("notify_near");

    // ── 보물찾기 — 서버 설정 ─────────────────────────────────
    private bool huntBusy, huntFailed, huntLogin;   // 못 불러옴 · 로그인이 풀림(401 — 토큰 만료)

    private IEnumerator LoadHunt()
    {
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Get("/api/hunts/prefs", (c, r) => { code = c; j = r; });
        huntFailed = code != 200 && code != 401;
        huntLogin = code == 401;
        if (code == 200) R0926HuntPrefs.Take(j["prefs"]);   // Changed → Refresh
        else Refresh();
    }

    private void ToggleHuntPref(string key)
    {
        if (huntBusy || !R0926HuntApi.CanCall || huntLogin || !isActiveAndEnabled) return;
        StartCoroutine(SaveHunt(key));
    }

    private IEnumerator SaveHunt(string key)
    {
        huntBusy = true;
        bool before = R0926HuntPrefs.Get(key);
        R0926HuntPrefs.Set(key, !before);   // 누르자마자 바뀐 것처럼 — 서버가 못 받으면 되돌린다
        var body = new JSONObject();
        body[key] = !before;
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Post("/api/hunts/prefs", body, (c, r) => { code = c; j = r; });
        huntBusy = false;
        if (code == 200) { huntFailed = false; R0926HuntPrefs.Take(j["prefs"]); yield break; }
        if (code == 401) huntLogin = true;
        R0926HuntPrefs.Set(key, before);
        if (ToastManager.Instance != null)
            ToastManager.Instance.ShowError(code == 401 ? R0926HuntApi.ErrorText("login", R0926LocalizedText.Lang())
                : L("설정을 바꾸지 못했어요 — 잠시 뒤 다시 해 주세요", "Couldn't save — please try again shortly",
                    "保存できませんでした — しばらくしてからもう一度", "未能保存 — 请稍后再试", "No se pudo guardar — inténtalo más tarde"));
    }

    private void RefreshHunt()
    {
        bool can = R0926HuntApi.CanCall && !huntLogin;
        Sw(huntSwitch, R0926HuntPrefs.Enabled);
        Sw(huntNewSwitch, R0926HuntPrefs.NotifyNew);
        Sw(huntNearSwitch, R0926HuntPrefs.NotifyNear);
        Dim(huntGroup, can);
        Dim(huntAlertGroup, can && R0926HuntPrefs.Enabled);
        if (huntNote == null) return;
        string note = !can ? L("로그인하면 보물찾기 설정을 바꿀 수 있어요", "Log in to change treasure hunt settings",
                               "ログインすると宝探しの設定を変更できます", "登录后即可更改寻宝设置", "Inicia sesión para cambiar los ajustes")
                    : huntFailed ? L("설정을 불러오지 못했어요 — 잠시 뒤 다시 열어 주세요", "Couldn't load settings — reopen in a moment",
                               "設定を読み込めませんでした — 少ししてから開き直してください", "未能加载设置 — 请稍后重新打开", "No se pudieron cargar — vuelve a abrir en un momento")
                    : null;
        huntNote.text = note ?? "";
        if (huntNote.gameObject.activeSelf != (note != null)) huntNote.gameObject.SetActive(note != null);
    }

    private static void Dim(CanvasGroup g, bool on)
    {
        if (g == null) return;
        g.alpha = on ? 1f : 0.4f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    private void Refresh()
    {
        RefreshHunt();
        Sw(masterSwitch, R0926SkySettings.Enabled);
        Sw(weatherSwitch, R0926SkySettings.Weather);
        Sw(eventsSwitch, R0926SkySettings.Events);
        Sw(lyingSwitch, R0926SkySettings.HideWhenLying);
        Sw(collapsedSwitch, R0926SkySettings.StartCollapsed);
        Sw(removeSwitch, R0926PlaceSettings.RemoveButton);
        if (channelGroup != null)
        {
            channelGroup.alpha = R0926SkySettings.Enabled ? 1f : 0.4f;
            channelGroup.interactable = R0926SkySettings.Enabled;
            channelGroup.blocksRaycasts = R0926SkySettings.Enabled;
        }
        Seg(angleTabs, angleLabels, R0926SkySettings.Angle);
        Seg(fpsTabs, fpsLabels, R0926FrameRate.Fps == 60 ? 1 : 0);
        Seg(fadeTabs, fadeLabels, R0926PlaceSettings.FarFade);
        if (hiddenText != null)
        {
            int n = HiddenPlaces.Count;
            hiddenText.text = n == 0
                ? L("숨긴 장소 없음 · 장소 박스의 X 로 숨길 수 있어요", "No hidden places · tap X on a place box to hide it",
                    "非表示の場所なし · マーカーのXで隠せます", "没有隐藏的地点 · 点标记上的 X 可隐藏", "Sin lugares ocultos · toca la X de un marcador")
                : string.Format(L("숨긴 장소 {0}곳 · 앱을 다시 켜면 다시 보여요", "{0} hidden · they come back when you restart",
                    "非表示 {0}か所 · アプリを再起動すると戻ります", "已隐藏 {0} 个 · 重启应用后恢复", "{0} ocultos · vuelven al reiniciar"), n);
        }
        if (showAllRow != null) showAllRow.SetActive(HiddenPlaces.Count > 0);
    }

    private void Sw(Image img, bool on)
    {
        if (img != null) img.sprite = on ? switchOn : switchOff;
    }

    private static void Seg(Image[] tabs, Text[] labels, int sel)
    {
        if (tabs != null)
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i] != null) tabs[i].color = new Color(1f, 1f, 1f, i == sel ? 0.14f : 0f);
        if (labels != null)
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] != null) labels[i].color = i == sel ? new Color(0.953f, 0.961f, 0.965f) : new Color(0.549f, 0.584f, 0.616f);
    }

    private static string L(string ko, string en, string ja, string zh, string es)
    {
        switch (R0926LocalizedText.Lang())
        {
            case "ko": return ko;
            case "ja": return ja;
            case "zh": return zh;
            case "es": return es;
            default: return en;
        }
    }
}
