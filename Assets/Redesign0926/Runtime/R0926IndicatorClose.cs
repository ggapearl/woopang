using UnityEngine;

/// <summary>
/// 0926 인디케이터 박스 — 이 씬에서만 켠다.
///  · 거리는 박스 위, 이름은 아래
///  · 장소 박스 오른쪽 위 꺾쇠 자리에 박스 색 X → 누르면 '삭제'(언어별 짧게), 3초 안에 한 번 더 누르면 이 기기에서 숨김(HiddenPlaces)
///    백그라운드에 다녀와도 유지, 앱을 완전히 껐다 켜면 다시 보인다
///  · 설정 '오브젝트 삭제 기능'(R0926PlaceSettings)을 끄면 X 없이 꺾쇠 넷 그대로
/// </summary>
public class R0926IndicatorClose : MonoBehaviour
{
    [SerializeField] private Sprite icon;      // 흰 X
    [SerializeField] private Sprite boxCut;    // 오른쪽 위 꺾쇠를 뺀 박스
    [Tooltip("'삭제' 알약 (9-slice)")]
    [SerializeField] private Sprite pill;
    [SerializeField] private bool showToast = true;

    private void Awake()
    {
        Indicator.CloseIcon = icon;
        Indicator.CornerCutBox = boxCut;
        Indicator.ClosePill = pill;
        Indicator.LabelsSwapped = true;
        Indicator.CloseButtonEnabled = R0926PlaceSettings.RemoveButton;
        HiddenPlaces.Hidden += OnHidden;
    }

    private void OnDestroy()
    {
        Indicator.CloseButtonEnabled = false;
        Indicator.LabelsSwapped = false;
        HiddenPlaces.Hidden -= OnHidden;
    }

    private void Update()
    {
        // 설정을 바꾸거나 앱 안에서 언어를 바꿔도 곧바로 맞게
        Indicator.CloseButtonEnabled = R0926PlaceSettings.RemoveButton;
        Indicator.CloseConfirmLabel = L("삭제", "DEL", "削除", "删除", "SUPR");
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
