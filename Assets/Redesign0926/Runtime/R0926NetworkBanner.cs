using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인터넷이 끊기면 위쪽에 안내를 띄운다. 기기(iOS·안드로이드)마다 확인할 설정 위치가 달라 문구를 나눴다.
/// iOS 는 앱별 '셀룰러 데이터' 스위치가 꺼져 있는 경우가 흔해 그 위치를 먼저 안내한다.
/// 다시 연결되면 '다시 연결됐어요'를 잠깐 보여 주고 사라진다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class R0926NetworkBanner : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Text body;
    [SerializeField] private Image dot;
    [Tooltip("끊긴 상태가 이 시간(초) 이상 이어져야 띄운다 — 순간 끊김에 깜빡이지 않게")]
    [SerializeField] private float showAfter = 2f;

    private CanvasGroup group;
    private float offlineFor;
    private float backOnlineUntil;
    private bool showingOffline;
    private bool showingServer;      // 인터넷은 되는데 우팡 서버가 응답하지 않을 때 (ServerHealth)
    private float nextCheck;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.5f;
            bool offline = Application.internetReachability == NetworkReachability.NotReachable;
            offlineFor = offline ? offlineFor + 0.5f : 0f;

            if (offline && offlineFor >= showAfter && !showingOffline)
            {
                showingOffline = true;
                showingServer = false;
                SetTexts(true);
            }
            else if (!offline && showingOffline)
            {
                showingOffline = false;
                backOnlineUntil = Time.unscaledTime + 1.8f;
                SetTexts(false);
            }
            else if (!offline && !showingOffline)
            {
                bool down = ServerHealth.Down;
                if (down && !showingServer) { showingServer = true; SetServerTexts(); }
                else if (!down && showingServer) { showingServer = false; backOnlineUntil = Time.unscaledTime + 1.8f; SetTexts(false); }
            }
        }

        bool visible = showingOffline || showingServer || Time.unscaledTime < backOnlineUntil;
        float target = visible ? 1f : 0f;
        if (!Mathf.Approximately(group.alpha, target))
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 4f);
    }

    // 인터넷은 되는데 서버만 안 될 때 — 사용자가 설정을 뒤질 필요가 없다는 걸 분명히
    private void SetServerTexts()
    {
        string lang = R0926LocalizedText.Lang();
        if (dot != null) dot.color = new Color(0.45f, 0.62f, 1f, 1f);
        title.text = lang switch
        {
            "ko" => "우팡 서버와 연결이 잠시 끊겼어요",
            "ja" => "WOOPANGサーバーに一時的に接続できません",
            "zh" => "暂时无法连接 WOOPANG 服务器",
            "es" => "Sin conexión con el servidor de WOOPANG",
            _ => "Can't reach WOOPANG right now"
        };
        body.text = lang switch
        {
            "ko" => "점검 중이거나 인터넷이 불안정해요. 따로 하실 건 없어요 — 다시 이어지면 저절로 불러와요.",
            "ja" => "メンテナンス中か、通信が不安定です。そのままお待ちください。つながると自動で読み込みます。",
            "zh" => "服务器维护中或网络不稳定。无需操作，恢复后会自动加载。",
            "es" => "Puede estar en mantenimiento o tu red es inestable. No tienes que hacer nada; se recargará solo.",
            _ => "We may be under maintenance or your connection is unstable. Nothing to do — it reloads automatically."
        };
    }

    private void SetTexts(bool offline)
    {
        string lang = R0926LocalizedText.Lang();
        bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
        if (dot != null) dot.color = offline ? new Color(1f, 0.72f, 0.2f, 1f) : new Color(0.2f, 0.78f, 0.48f, 1f);

        if (!offline)
        {
            title.text = lang switch { "ko" => "다시 연결됐어요", "ja" => "再接続しました", "zh" => "已重新连接", "es" => "Conexión restablecida", _ => "Back online" };
            body.text = "";
            return;
        }

        title.text = lang switch
        {
            "ko" => "인터넷에 연결되어 있지 않아요",
            "ja" => "インターネットに接続されていません",
            "zh" => "网络未连接",
            "es" => "Sin conexión a internet",
            _ => "You're offline"
        };
        body.text = ios
            ? lang switch
            {
                "ko" => "Wi-Fi를 확인하거나, 설정 › 셀룰러에서 우팡의 데이터 사용을 켜 주세요. 주변 장소와 메시지를 불러오지 못합니다.",
                "ja" => "Wi-Fiを確認するか、［設定］›［モバイル通信］でWOOPANGのデータ通信をオンにしてください。周辺の場所やメッセージを読み込めません。",
                "zh" => "请检查 Wi-Fi，或在“设置 › 蜂窝网络”中允许 WOOPANG 使用数据。附近地点和消息无法加载。",
                "es" => "Revisa el Wi-Fi o permite que WOOPANG use datos en Ajustes › Datos móviles. No se pueden cargar lugares ni mensajes.",
                _ => "Check Wi-Fi, or allow WOOPANG to use cellular data in Settings › Cellular. Nearby places and messages can't load."
            }
            : lang switch
            {
                "ko" => "Wi-Fi나 모바일 데이터가 켜져 있는지 확인해 주세요 (설정 › 네트워크 및 인터넷). 주변 장소와 메시지를 불러오지 못합니다.",
                "ja" => "Wi-Fiまたはモバイルデータがオンか確認してください（設定 › ネットワークとインターネット）。周辺の場所やメッセージを読み込めません。",
                "zh" => "请确认已开启 Wi-Fi 或移动数据（设置 › 网络和互联网）。附近地点和消息无法加载。",
                "es" => "Comprueba que el Wi-Fi o los datos móviles estén activados (Ajustes › Redes e Internet). No se pueden cargar lugares ni mensajes.",
                _ => "Check that Wi-Fi or mobile data is on (Settings › Network & internet). Nearby places and messages can't load."
            };
    }
}
