using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 분류 고르기 창 층 — 위 · 아래로 밀면 닫힌다 (11-26 대표님 37번 「아래나 위로 스와이프 하거나 모달 밖을 터치했을 때는 해당 모달이 닫히면 돼」).
/// 끌기는 R0926CategoryMenu 로 넘긴다. 칸(버튼)은 끌기를 받지 않아 칸 위에서 시작해도 이리로 오고, 끌기가 시작되면 그 칸은 눌리지 않는다.
/// </summary>
public class R0926CategorySwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private R0926CategoryMenu menu;

    public void OnBeginDrag(PointerEventData e) { if (menu != null) menu.DragBegin(e); }
    public void OnDrag(PointerEventData e) { if (menu != null) menu.DragMove(e); }
    public void OnEndDrag(PointerEventData e) { if (menu != null) menu.DragEnd(e); }
}
