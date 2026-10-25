using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 하늘 판(SkyWeather0926)에 닿은 손가락을 R0926SkyPages 로 넘긴다.
/// 판은 보일 때만 켜져 있어서 장을 넘기는 본체는 늘 켜진 표식에 두고, 여기는 받기만 한다.
/// </summary>
public class R0926SkyBoardInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private R0926SkyPages pages;

    public void OnPointerDown(PointerEventData e) { if (pages != null) pages.PointerDown(); }
    public void OnPointerUp(PointerEventData e) { if (pages != null) pages.PointerUp(); }
    public void OnBeginDrag(PointerEventData e) { if (pages != null) pages.BeginDrag(e); }
    public void OnDrag(PointerEventData e) { if (pages != null) pages.Drag(e); }
    public void OnEndDrag(PointerEventData e) { if (pages != null) pages.EndDrag(e); }
    public void OnPointerClick(PointerEventData e) { if (pages != null) pages.Click(e); }
}
