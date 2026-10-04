using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 목록 시트 '설정' 탭 — 하늘 화면에 무엇을, 언제 띄울지.
///  · 하늘 화면 켜기(전체) · 날씨 · 동네 이벤트·게임 · 라이브 방송 · 광고·제휴
///  · 보이는 각도(조금만 들어도 / 보통 / 똑바로 위) · 누워서 쓸 땐 숨기기 · 처음엔 접어 두기
///  · 숨긴 장소 수 (인디케이터 X — 앱을 다시 켜면 다시 보인다)
/// 이벤트·라이브·광고는 아직 보낼 내용이 없어 '곧' 표시 — 켜고 끄는 값은 지금부터 저장된다.
/// </summary>
public class R0926SettingsPanel : MonoBehaviour
{
    [SerializeField] private Image masterSwitch;
    [SerializeField] private Image weatherSwitch;
    [SerializeField] private Image eventsSwitch;
    [SerializeField] private Image liveSwitch;
    [SerializeField] private Image adsSwitch;
    [SerializeField] private Image lyingSwitch;
    [SerializeField] private Image collapsedSwitch;
    [SerializeField] private CanvasGroup channelGroup;   // 전체를 끄면 아래 항목이 흐려진다
    [SerializeField] private Image[] angleTabs;
    [SerializeField] private Text[] angleLabels;
    [SerializeField] private Image removeSwitch;          // 오브젝트 삭제 기능 (장소 박스 X)
    [SerializeField] private Text hiddenText;
    [SerializeField] private Sprite switchOn;
    [SerializeField] private Sprite switchOff;

    private void OnEnable() { Refresh(); }

    public void ToggleMaster() { R0926SkySettings.Enabled = !R0926SkySettings.Enabled; Refresh(); }
    public void ToggleWeather() { R0926SkySettings.Weather = !R0926SkySettings.Weather; Refresh(); }
    public void ToggleEvents() { R0926SkySettings.Events = !R0926SkySettings.Events; Refresh(); }
    public void ToggleLive() { R0926SkySettings.Live = !R0926SkySettings.Live; Refresh(); }
    public void ToggleAds() { R0926SkySettings.Ads = !R0926SkySettings.Ads; Refresh(); }
    public void ToggleLying() { R0926SkySettings.HideWhenLying = !R0926SkySettings.HideWhenLying; Refresh(); }
    public void ToggleCollapsed() { R0926SkySettings.StartCollapsed = !R0926SkySettings.StartCollapsed; Refresh(); }
    public void SetAngle(int step) { R0926SkySettings.Angle = step; Refresh(); }
    public void ToggleRemove() { R0926PlaceSettings.RemoveButton = !R0926PlaceSettings.RemoveButton; Refresh(); }

    private void Refresh()
    {
        Sw(masterSwitch, R0926SkySettings.Enabled);
        Sw(weatherSwitch, R0926SkySettings.Weather);
        Sw(eventsSwitch, R0926SkySettings.Events);
        Sw(liveSwitch, R0926SkySettings.Live);
        Sw(adsSwitch, R0926SkySettings.Ads);
        Sw(lyingSwitch, R0926SkySettings.HideWhenLying);
        Sw(collapsedSwitch, R0926SkySettings.StartCollapsed);
        Sw(removeSwitch, R0926PlaceSettings.RemoveButton);
        if (channelGroup != null)
        {
            channelGroup.alpha = R0926SkySettings.Enabled ? 1f : 0.4f;
            channelGroup.interactable = R0926SkySettings.Enabled;
            channelGroup.blocksRaycasts = R0926SkySettings.Enabled;
        }
        int a = R0926SkySettings.Angle;
        if (angleTabs != null)
            for (int i = 0; i < angleTabs.Length; i++)
                if (angleTabs[i] != null) angleTabs[i].color = new Color(1f, 1f, 1f, i == a ? 0.14f : 0f);
        if (angleLabels != null)
            for (int i = 0; i < angleLabels.Length; i++)
                if (angleLabels[i] != null) angleLabels[i].color = i == a ? new Color(0.953f, 0.961f, 0.965f) : new Color(0.549f, 0.584f, 0.616f);
        if (hiddenText != null)
        {
            int n = HiddenPlaces.Count;
            hiddenText.text = n == 0
                ? L("숨긴 장소 없음 · 장소 박스의 X 로 숨길 수 있어요", "No hidden places · tap X on a place box to hide it",
                    "非表示の場所なし · マーカーのXで隠せます", "没有隐藏的地点 · 点标记上的 X 可隐藏", "Sin lugares ocultos · toca la X de un marcador")
                : string.Format(L("숨긴 장소 {0}곳 · 앱을 다시 켜면 다시 보여요", "{0} hidden · they come back when you restart",
                    "非表示 {0}か所 · アプリを再起動すると戻ります", "已隐藏 {0} 个 · 重启应用后恢复", "{0} ocultos · vuelven al reiniciar"), n);
        }
    }

    private void Sw(Image img, bool on)
    {
        if (img != null) img.sprite = on ? switchOn : switchOff;
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
