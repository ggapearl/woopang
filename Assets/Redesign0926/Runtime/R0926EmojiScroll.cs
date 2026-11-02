using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 이모지 창 목록 — 아래로 굴리며 본다. 누른 칸은 밝게, 떼면 그 이모지를 칸에 넣는다(굴리기 시작하면 취소).
/// 맨 위에서 아래로 시작한 끌기는 목록 대신 창을 끌어내린다 (11-02 대표님 「최상단에서 아래로 스와이프하면 해당 창이 내려가도록」).
/// </summary>
public class R0926EmojiScroll : ScrollRect, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private R0926EmojiPanel panel;

    private bool sheetDrag;

    public override void OnBeginDrag(PointerEventData e)
    {
        panel.Unpress();
        Vector2 d = e.position - e.pressPosition;
        if (e.button == PointerEventData.InputButton.Left && content != null && content.anchoredPosition.y <= 1f
            && d.y < 0f && -d.y > Mathf.Abs(d.x))
        {
            sheetDrag = true;
            StopMovement();
            panel.BeginSheetDrag(e);
            return;
        }
        sheetDrag = false;
        base.OnBeginDrag(e);
    }

    public override void OnDrag(PointerEventData e)
    {
        if (sheetDrag) panel.SheetDrag(e);
        else base.OnDrag(e);
    }

    public override void OnEndDrag(PointerEventData e)
    {
        if (sheetDrag)
        {
            sheetDrag = false;
            panel.EndSheetDrag(e);
        }
        else base.OnEndDrag(e);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) panel.Press(e.position, e.pressEventCamera);
    }

    public void OnPointerUp(PointerEventData e) => panel.Unpress();

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && !e.dragging) panel.Pick(e.position, e.pressEventCamera);
    }

    protected override void OnDisable()
    {
        sheetDrag = false;
        base.OnDisable();
    }
}
