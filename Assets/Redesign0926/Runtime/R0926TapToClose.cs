using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 시트 바깥(어두운 배경)을 누르면 기존 닫기 버튼을 대신 눌러 준다 — 닫는 로직은 원래 버튼 것을 그대로 쓴다.
/// 시트 안쪽을 누른 경우는 무시한다(클릭이 부모로 올라오기 때문에 실제로 맞은 오브젝트로 구분).
/// </summary>
public class R0926TapToClose : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Button closeButton;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (closeButton == null || !closeButton.isActiveAndEnabled) return;
        if (eventData.pointerPressRaycast.gameObject != gameObject) return;
        closeButton.onClick.Invoke();
    }
}
