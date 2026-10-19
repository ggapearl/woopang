using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 세로 목록이 엉뚱한 손가락을 따라 내려가지 않게 (2026-10-15·16 대표님: "옆으로 스와이프하다가 아래로 내리면 '최상단'에
/// 있음에도 불구하고 해당 내용들이 아래로 내려가" — 보물찾기 카드·메시지·팔로잉/팔로워 … 전부).
///  · 옆으로 시작한 손가락 — 손을 뗄 때까지 그 목록을 멈춘다 (카드 넘기기·줄 밀어 삭제·그냥 옆으로 문지르기 모두)
///  · 맨 위에서 아래로 시작한 손가락 — 목록 내용이 늘어나 내려오지 않는다 (밀어 닫는 시트면 시트가 내려간다 — R0926SwipeDismiss)
/// 위로 시작한 손가락은 예전처럼 목록을 굴린다. 방향은 다른 끌기들(R0926SwipeDismiss·R0926SheetModes·SwipePanelController)과
/// 같은 순간(14px)에 정한다. 멈추는 방법도 같다 — 목록을 잠깐 끈다(끄면 끌기 상태가 지워지고 다시 켜도 이 손가락은 안 받는다).
/// 캔버스에 하나 — 적용 단계(Redesign0926Apply8)가 붙인다.
/// </summary>
public class R0926ScrollLock : MonoBehaviour
{
    private const float DecidePx = 14f;

    private enum Phase { Idle, Pending, Holding, Ignored }

    private Phase phase;
    private Pointer tracked;
    private Vector2 startPos;
    private ScrollRect target, frozen;
    private float startY;               // 누를 때 목록 위치 — 방향을 정하기 전에 조금 끌려온 것을 되돌린다
    private int unfreezeFrame = -1;

    private static readonly List<RaycastResult> hits = new List<RaycastResult>();

    private void OnDisable()
    {
        Unfreeze();
        phase = Phase.Idle;
    }

    private void Update()
    {
        if (frozen != null && phase != Phase.Holding && Time.frameCount >= unfreezeFrame) Unfreeze();

        if (phase == Phase.Idle)
        {
            tracked = null;
            foreach (var d in InputSystem.devices)
                if (d is Pointer pp && d.enabled && pp.press.wasPressedThisFrame) { tracked = pp; break; }
            if (tracked != null) Begin(tracked.position.ReadValue());
            return;
        }
        if (tracked == null || !tracked.added || !tracked.press.isPressed)
        {
            if (phase == Phase.Holding) unfreezeFrame = Time.frameCount + 1;   // UI 가 손 뗌을 처리한 뒤에
            phase = Phase.Idle;
            return;
        }
        if (phase == Phase.Pending) Decide(tracked.position.ReadValue());
    }

    private void Begin(Vector2 at)
    {
        phase = Phase.Ignored;
        target = null;
        var es = EventSystem.current;
        if (es == null) return;
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = at }, hits);
        if (hits.Count == 0) return;

        // 이 손가락을 받을 것 — 목록 자신이거나, 줄 밀어 삭제처럼 세로 끌기를 목록에 넘겨주는 것
        var handler = ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject);
        if (handler == null) return;
        var sr = handler.GetComponent<ScrollRect>();
        if (sr == null) sr = handler.GetComponentInParent<ScrollRect>();
        if (sr == null || !sr.vertical || sr.horizontal || sr.content == null || !sr.enabled) return;   // 가로 칩 줄·지도는 그쪽 몫

        target = sr;
        startY = sr.content.anchoredPosition.y;
        startPos = at;
        phase = Phase.Pending;
    }

    private void Decide(Vector2 now)
    {
        Vector2 d = now - startPos;
        if (d.magnitude < DecidePx) return;
        if (target == null) { phase = Phase.Ignored; return; }   // 그새 창이 닫혔다
        bool sideways = Mathf.Abs(d.x) > Mathf.Abs(d.y);
        bool pullDownAtTop = d.y < 0f && AtTop(target);
        if (!sideways && !pullDownAtTop)
        {
            phase = Phase.Ignored;   // 위로 굴리기 — 목록 몫
            return;
        }
        if (frozen != null && frozen != target) Unfreeze();
        frozen = target;
        var p = frozen.content.anchoredPosition;
        if (!Mathf.Approximately(p.y, startY)) frozen.content.anchoredPosition = new Vector2(p.x, startY);
        frozen.velocity = Vector2.zero;
        frozen.enabled = false;
        phase = Phase.Holding;
    }

    private void Unfreeze()
    {
        if (frozen != null) frozen.enabled = true;
        frozen = null;
    }

    // R0926SwipeDismiss 와 같은 기준 — 스크롤할 게 없거나 맨 위
    private static bool AtTop(ScrollRect sr)
    {
        var view = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
        if (sr.content.rect.height <= view.rect.height + 1f) return true;
        return sr.verticalNormalizedPosition >= 0.995f;
    }
}
