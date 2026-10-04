using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 아래로 끌어 내려 닫는 시트. 손가락을 따라 내려가다가 충분히(시트 높이의 ¼, 또는 빠르게 튕기면) 내리면
/// 끝까지 내려간 뒤 원래 닫기 버튼을 눌러 준다 — 닫는 로직은 그 버튼 것을 그대로 쓴다. 덜 내리면 제자리로 돌아온다.
///
/// 터치를 직접 읽는다(이벤트 시스템의 끌기는 목록 줄·스와이프 삭제가 먼저 가져가서 시트까지 오지 않는다):
///   · 제목·빈 곳에서 시작 → 바로 시트가 따라 내려온다
///   · 세로 스크롤 목록 위에서 시작 → 목록이 맨 위일 때만 넘겨받고, 그동안 목록은 멈춘다
///   · 지도·슬라이더·입력창처럼 스스로 끄는 것 위에서 시작 → 건드리지 않는다
/// 닫기 버튼·뒤로가기·바깥 누르기로 닫을 때도 R0926CloseProxy 가 AnimateClose 를 불러 아래로 미끄러져 사라진다.
/// 위치는 증분으로만 더하고 빼서 R0926SlideIn·R0926SafeInset 과 겹쳐도 자리가 틀어지지 않는다.
/// </summary>
public class R0926SwipeDismiss : R0926Closer
{
    [SerializeField] private Button closeButton;
    [SerializeField] private float dismissFraction = 0.25f;   // 시트 높이 대비
    [SerializeField] private float minDismiss = 200f;          // 캔버스 단위
    [SerializeField] private float flickSpeed = 1600f;         // 캔버스 단위/초 — 빠르게 튕기면 짧게 내려도 닫힘
    [Tooltip("시트와 함께 내려갈 형제 (예: 따로 떠 있는 취소 버튼)")]
    [SerializeField] private RectTransform[] followers;
    [Tooltip("내려가는 만큼 옅어질 어두운 바탕 (창 뒤 그늘)")]
    [SerializeField] private Graphic backdrop;

    private const float DecidePx = 14f;   // 이만큼 움직여야 방향을 정한다 (화면 픽셀)

    private enum Phase { Idle, Pending, Dragging, Ignored }

    private RectTransform rt;
    private Canvas root;
    private Phase phase;
    private Pointer tracked;            // 이번 끌기를 시작한 장치
    private Vector2 startPos, lastPos;
    private float lastTime, velocity;
    private ScrollRect scroll;          // 시작한 곳의 스크롤 목록 (없으면 null)
    private bool scrollHorizontalOnly;
    private ScrollRect frozen;          // 끄는 동안 멈춰 둔 목록 — 손을 뗀 다음 프레임에 되살린다
    private int unfreezeFrame = -1;

    private float applied;              // 지금 더해 둔 아래쪽 이동 (양수 = 아래로)
    private float target;
    private bool animating, closing;
    private Button pending;             // 다 내려간 뒤 누를 버튼
    private float backdropAlpha = -1f;

    private static readonly List<RaycastResult> hits = new List<RaycastResult>();

    private void Awake()
    {
        rt = (RectTransform)transform;
        var c = GetComponentInParent<Canvas>(true);
        root = c != null ? c.rootCanvas : null;
        if (backdrop != null) backdropAlpha = backdrop.color.a;
    }

    /// <summary>시트를 아래로 내려 보낸 뒤 then(없으면 closeButton)을 누른다. 이미 닫는 중이면 무시.</summary>
    public override void AnimateClose(Button then)
    {
        if (!isActiveAndEnabled) { if (then != null) then.onClick.Invoke(); return; }
        if (closing) return;
        pending = then != null ? then : closeButton;
        Unfreeze();
        phase = Phase.Idle;
        closing = true;
        target = rt.rect.height + 400f;
        animating = true;
    }

    private void OnDisable()
    {
        Set(0f);
        Unfreeze();
        phase = Phase.Idle;
        animating = closing = false;
    }

    private void Update()
    {
        if (frozen != null && Time.frameCount >= unfreezeFrame && phase != Phase.Dragging) Unfreeze();

        if (animating)
        {
            float next = Mathf.Lerp(applied, target, 1f - Mathf.Exp(-(closing ? 22f : 16f) * Time.unscaledDeltaTime));
            if (Mathf.Abs(next - target) < 1.5f) next = target;
            Set(next);
            if (next == target)
            {
                animating = false;
                if (closing) Close();
            }
        }

        if (closing) return;
        bool has = ReadPointer(out bool down, out bool pressedNow, out Vector2 pos);
        if (phase == Phase.Idle)
        {
            if (has && down && pressedNow) Begin(pos);
            return;
        }
        if (!has || !down)
        {
            if (phase == Phase.Dragging) Release();
            phase = Phase.Idle;
            return;
        }
        if (phase == Phase.Pending) Decide(pos);
        if (phase == Phase.Dragging) Follow(pos);
    }

    // 누른 장치를 끝까지 따라간다 — 터치·마우스·펜이 섞여 있어도 Pointer.current 가 중간에 바뀌지 않게
    private bool ReadPointer(out bool down, out bool pressedNow, out Vector2 pos)
    {
#if UNITY_EDITOR
        if (fed) { down = fedDown; pressedNow = fedPressed; fedPressed = false; pos = fedPos; return true; }
#endif
        if (phase == Phase.Idle)
        {
            tracked = null;
            foreach (var d in InputSystem.devices)
                if (d is Pointer pp && d.enabled && pp.press.wasPressedThisFrame) { tracked = pp; break; }
        }
        if (tracked == null || !tracked.added) { down = pressedNow = false; pos = default; return false; }
        down = tracked.press.isPressed;
        pressedNow = tracked.press.wasPressedThisFrame;
        pos = tracked.position.ReadValue();
        return true;
    }

#if UNITY_EDITOR
    // 플레이 모드 시험(Redesign0926PlayTest)이 손가락 대신 넣는다
    private bool fed, fedDown, fedPressed;
    private Vector2 fedPos;
    public void EditorFeed(bool down, Vector2 pos) { fed = true; if (down && !fedDown) fedPressed = true; fedDown = down; fedPos = pos; }   // 누름은 읽힐 때까지 남긴다
    public void EditorFeedEnd() { fed = false; fedDown = false; }
#endif

    private void Begin(Vector2 pos)
    {
        phase = Phase.Ignored;
        var es = EventSystem.current;
        if (es == null) return;
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = pos }, hits);
        if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(transform)) return;   // 시트 위가 아니거나 다른 창에 가려짐

        scroll = null;
        scrollHorizontalOnly = false;
        var handler = ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject);
        if (handler != null && handler.transform.IsChildOf(transform))
        {
            var sr = handler.GetComponent<ScrollRect>();
            if (sr == null && handler.GetComponent<SwipeToDeleteHandler>() != null) sr = handler.GetComponentInParent<ScrollRect>();
            if (sr == null) return;                                  // 지도·슬라이더·입력창 등 — 그쪽 끌기
            scroll = sr;
            scrollHorizontalOnly = !sr.vertical;
        }
        startPos = lastPos = pos;
        lastTime = Time.unscaledTime;
        velocity = 0f;
        phase = Phase.Pending;
    }

    private void Decide(Vector2 pos)
    {
        Vector2 d = pos - startPos;
        if (d.magnitude < DecidePx) return;
        bool downward = d.y < 0f && Mathf.Abs(d.y) > Mathf.Abs(d.x) * 1.2f;
        if (!downward || (scroll != null && !scrollHorizontalOnly && !AtTop(scroll)))
        {
            phase = Phase.Ignored;   // 이번 손가락은 목록·가로 넘기기 몫
            return;
        }
        if (scroll != null)
        {
            frozen = scroll;
            frozen.enabled = false;  // 끄는 동안 목록은 그 자리에 (다시 켜면 끌기 상태도 초기화된다)
        }
        animating = false;
        phase = Phase.Dragging;
        startPos = pos;              // 여기서부터 따라 내려온다 (판단 거리만큼 튀지 않게)
        lastPos = pos;
    }

    private void Follow(Vector2 pos)
    {
        float scale = root != null ? root.scaleFactor : 1f;
        float next = Mathf.Max(0f, (startPos.y - pos.y) / scale);
        float dt = Time.unscaledTime - lastTime;
        if (dt > 0.0001f) velocity = Mathf.Lerp(velocity, (lastPos.y - pos.y) / scale / dt, 0.5f);
        lastPos = pos;
        lastTime = Time.unscaledTime;
        Set(next);
    }

    private void Release()
    {
        unfreezeFrame = Time.frameCount + 1;
        float h = rt.rect.height;
        bool dismiss = applied > Mathf.Max(minDismiss, h * dismissFraction) || (velocity > flickSpeed && applied > 40f);
        closing = dismiss && closeButton != null;
        pending = closeButton;
        target = closing ? h + 400f : 0f;
        animating = true;
    }

    private void Close()
    {
        closing = false;
        Unfreeze();
        var b = pending != null ? pending : closeButton;
        pending = null;
        if (b != null) b.onClick.Invoke();
        if (isActiveAndEnabled) Set(0f);   // 버튼이 이 시트를 끄지 않았으면 제자리로
    }

    private void Unfreeze()
    {
        if (frozen != null) frozen.enabled = true;
        frozen = null;
    }

    private static bool AtTop(ScrollRect sr)
    {
        var content = sr.content;
        if (content == null) return true;
        var view = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
        if (content.rect.height <= view.rect.height + 1f) return true;   // 스크롤할 게 없다
        return sr.verticalNormalizedPosition >= 0.995f;
    }

    private void Set(float offset)
    {
        if (rt == null) return;
        float delta = offset - applied;
        if (Mathf.Approximately(delta, 0f)) return;
        rt.anchoredPosition -= new Vector2(0f, delta);
        if (followers != null)
            foreach (var f in followers)
                if (f != null) f.anchoredPosition -= new Vector2(0f, delta);
        applied = offset;
        if (backdrop != null && backdropAlpha >= 0f)
        {
            var c = backdrop.color;
            c.a = backdropAlpha * (1f - Mathf.Clamp01(offset / Mathf.Max(1f, rt.rect.height * 0.9f)));
            backdrop.color = c;
        }
    }
}
