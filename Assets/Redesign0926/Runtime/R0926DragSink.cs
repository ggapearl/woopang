using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 끌기를 받아 그냥 둔다 — 이 아래(자식)에서 시작한 끌기가 위 창의 「아래로 밀어 닫기」(R0926SwipeDismiss)로 가지 않게.
/// 11-18 I1 정보 수정 카드 안 AI 화면: 칩·미리보기를 짚고 내려도 수정 카드째 닫히지 않는다 (‹ · 취소 · 확인으로 닫는다).
/// </summary>
public class R0926DragSink : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public void OnBeginDrag(PointerEventData e) { }
    public void OnDrag(PointerEventData e) { }
    public void OnEndDrag(PointerEventData e) { }
}
