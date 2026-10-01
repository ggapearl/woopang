using UnityEngine;

/// <summary>
/// 인디케이터 박스 오른쪽 아래의 X — 이 씬(0926)에서만 켠다.
/// 누르면 그 장소의 3D 오브젝트·박스·화살표가 이 기기에서 사라지고(HiddenPlaces),
/// 백그라운드에 다녀와도 유지, 앱을 완전히 껐다 켜면 다시 보인다.
/// </summary>
public class R0926IndicatorClose : MonoBehaviour
{
    [SerializeField] private Sprite icon;
    [SerializeField] private Sprite background;
    [Tooltip("X → '삭제' 로 늘어나는 알약 (9-slice)")]
    [SerializeField] private Sprite pill;
    [Tooltip("보이는 원 크기 (캔버스 단위, 1440 기준). 누르는 영역은 1.9배")]
    [SerializeField] private float size = 96f;
    [SerializeField] private bool showToast = true;

    private void Awake()
    {
        Indicator.CloseIcon = icon;
        Indicator.CloseBackground = background;
        Indicator.ClosePill = pill;
        Indicator.CloseButtonSize = size;
        Indicator.CloseButtonEnabled = true;
        HiddenPlaces.Hidden += OnHidden;
    }

    private void Update()
    {
        // 앱 안에서 언어를 바꿔도 곧바로 맞게
        Indicator.CloseConfirmLabel = L("삭제", "Remove", "削除", "删除", "Quitar");
    }

    private void OnDestroy()
    {
        Indicator.CloseButtonEnabled = false;
        HiddenPlaces.Hidden -= OnHidden;
    }

    private void OnHidden(string uniqueId)
    {
        if (!showToast || ToastManager.Instance == null) return;
        ToastManager.Instance.ShowSuccess(L(
            "이 장소를 숨겼어요 · 앱을 다시 켜면 다시 보여요",
            "Place hidden · it comes back when you restart the app",
            "この場所を非表示にしました · アプリを再起動すると戻ります",
            "已隐藏此地点 · 重新启动应用后会再次显示",
            "Lugar oculto · volverá al reiniciar la app"));
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
