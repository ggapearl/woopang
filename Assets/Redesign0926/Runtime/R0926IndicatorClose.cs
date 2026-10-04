using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 0926 인디케이터 박스 — 이 씬에서만 켠다.
///  · 거리는 박스 위, 이름은 아래
///  · 장소 박스 오른쪽 위 꺾쇠 자리에 박스 색 X → 누르면 '삭제'(언어별 짧게), 3초 안에 한 번 더 누르면 이 기기에서 숨김(HiddenPlaces)
///    백그라운드에 다녀와도 유지, 앱을 완전히 껐다 켜면 다시 보인다
///  · 숨긴 뒤 위치 칩 위에 '이 장소를 숨겼어요 · 되돌리기' 가 몇 초 떠 있다 (설정 '모두 다시 보이기'로도 되살린다)
///  · 설정 '오브젝트 삭제 기능'(R0926PlaceSettings)을 끄면 X 없이 꺾쇠 넷 그대로
/// </summary>
public class R0926IndicatorClose : MonoBehaviour
{
    [SerializeField] private Sprite icon;      // 흰 X
    [SerializeField] private Sprite boxCut;    // 오른쪽 위 꺾쇠를 뺀 박스
    [Tooltip("'삭제' 알약 (9-slice)")]
    [SerializeField] private Sprite pill;
    [SerializeField] private bool showToast = true;

    [Header("숨긴 뒤 '되돌리기' 알림 (Apply 가 만든다 — 비어 있으면 예전 토스트)")]
    [SerializeField] private CanvasGroup undoBar;
    [SerializeField] private Text undoText;
    [SerializeField] private Text undoLabel;
    [Tooltip("'되돌리기'를 누를 수 있는 시간(초)")]
    [SerializeField] private float undoSeconds = 4f;
    [SerializeField] private float undoFade = 0.2f;

    private string undoId;
    private float undoUntil;

    private void Awake()
    {
        Indicator.CloseIcon = icon;
        Indicator.CornerCutBox = boxCut;
        Indicator.ClosePill = pill;
        Indicator.LabelsSwapped = true;
        Indicator.CloseButtonEnabled = R0926PlaceSettings.RemoveButton;
        HiddenPlaces.Hidden += OnHidden;
        HiddenPlaces.Restored += OnRestored;
        if (undoBar != null)
        {
            undoBar.alpha = 0f;
            undoBar.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        Indicator.CloseButtonEnabled = false;
        Indicator.LabelsSwapped = false;
        HiddenPlaces.Hidden -= OnHidden;
        HiddenPlaces.Restored -= OnRestored;
    }

    private void Update()
    {
        // 설정을 바꾸거나 앱 안에서 언어를 바꿔도 곧바로 맞게
        Indicator.CloseButtonEnabled = R0926PlaceSettings.RemoveButton;
        Indicator.CloseConfirmLabel = L("삭제", "DEL", "削除", "删除", "SUPR");
        UpdateUndoBar();
    }

    private void UpdateUndoBar()
    {
        if (undoBar == null || !undoBar.gameObject.activeSelf) return;
        if (undoId != null && Time.unscaledTime >= undoUntil) undoId = null;
        bool show = undoId != null;
        undoBar.blocksRaycasts = show;
        undoBar.interactable = show;
        undoBar.alpha = Mathf.MoveTowards(undoBar.alpha, show ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, undoFade));
        if (!show && undoBar.alpha <= 0f) undoBar.gameObject.SetActive(false);
    }

    /// <summary>알림의 '되돌리기' — 방금 숨긴 장소를 다시 보이게 한다</summary>
    public void UndoHide()
    {
        string id = undoId;
        undoId = null;
        if (id != null) HiddenPlaces.Unhide(id);
    }

    private void OnHidden(string uniqueId)
    {
        if (!showToast) return;
        if (undoBar != null)
        {
            // 마지막으로 숨긴 장소만 되돌린다 — 앞서 숨긴 것은 설정 '모두 다시 보이기'로
            undoId = uniqueId;
            undoUntil = Time.unscaledTime + undoSeconds;
            if (undoText != null) undoText.text = L("이 장소를 숨겼어요", "Place hidden", "この場所を非表示にしました", "已隐藏此地点", "Lugar oculto");
            if (undoLabel != null) undoLabel.text = L("되돌리기", "Undo", "元に戻す", "撤销", "Deshacer");
            undoBar.gameObject.SetActive(true);
            return;
        }
        if (ToastManager.Instance == null) return;
        ToastManager.Instance.ShowSuccess(L(
            "이 장소를 숨겼어요 · 앱을 다시 켜면 다시 보여요",
            "Place hidden · it comes back when you restart the app",
            "この場所を非表示にしました · アプリを再起動すると戻ります",
            "已隐藏此地点 · 重新启动应用后会再次显示",
            "Lugar oculto · volverá al reiniciar la app"));
    }

    // 설정 '모두 다시 보이기'로 이미 되살아났으면 알림도 거둔다
    private void OnRestored()
    {
        if (undoId != null && !HiddenPlaces.IsHidden(undoId)) undoId = null;
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
