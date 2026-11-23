using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 그리지 않고 끌기만 받는 자리 — 받은 끌기는 붙인 쪽(onBegin · onDrag · onEnd)에 넘긴다.
/// 11-18 J4 길안내 화살표 「아래로 밀어 끝내기」: 화살표 그림은 제 캔버스라 누름을 받지 않아 그 둘레에 이 자리를 깐다.
/// 투명 Image 와 달리 그리지 않는다 (빈 메시). 화면 UI 라 이 자리에서 시작한 손가락은 그 밑 AR 오브젝트가 받지 않는다 (WorldTouch — 한 손가락은 한 곳).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class R0926DragZone : MaskableGraphic, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public System.Action<PointerEventData> onBegin, onDrag, onEnd;

    protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();

    public void OnBeginDrag(PointerEventData e) => onBegin?.Invoke(e);
    public void OnDrag(PointerEventData e) => onDrag?.Invoke(e);
    public void OnEndDrag(PointerEventData e) => onEnd?.Invoke(e);
}
