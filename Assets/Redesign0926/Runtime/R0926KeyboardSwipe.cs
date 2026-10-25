using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 키보드가 떠 있을 때 입력칸을 아래로 쓸어내리면 키보드를 내린다 (2026-10-25 대표님: "키패드 발생했을 때 입력칸 아래로 스와이프하면 키패드 사라지도록").
/// 아이폰 기본 키보드엔 내리기 키가 없어, 대화·댓글 창에선 창을 닫는 것 말고는 키보드를 내릴 길이 없었다.
///   · 키보드 위 입력줄(UploadInputMirror — 장소·3D 모델·보물찾기·리뷰) — 입력줄이나 그 위 어디서든(입력줄이 떠 있는 동안 뒤 화면은 막혀 있다).
///     기기 키보드를 내린 것과 같게 글은 원래 칸에 옮기고 닫는다 — 보내기 칸이어도 보내지 않는다
///   · 대화·댓글 창(MobileKeyboardHandler) — 입력줄에서. 대화 목록을 굴리는 동안 키보드가 남는 건 그대로
///   · 그 밖에 칸 안에서 바로 쓰는 칸(검색 등) — 그 칸에서
/// 입력줄의 단추(보내기·✓·빠른 반응) 위에서 시작해도 같다 — 누르기만 하면 예전처럼 누른 것이다.
/// Unity 는 끌다가 떼도 같은 칸·단추 위면 누른 것으로 쳐서, 키보드를 다시 열거나 보내 버린다
/// (키보드가 내려가며 입력줄도 내려와 손가락 밑으로 다시 온다) — 손을 뗄 때까지 손 밑의 칸·단추를 꺼 둔다.
/// 아래로 밀어 닫는 시트(R0926SwipeDismiss — 대화창 · SwipeToClose — 댓글 창)는 이 손가락을 받지 않는다 (Claims).
/// 캔버스에 하나 — 적용 단계(Redesign0926ApplyReadable)가 붙이고 연결한다.
/// </summary>
public class R0926KeyboardSwipe : MonoBehaviour
{
    [SerializeField] private UploadInputMirror[] mirrors;
    [SerializeField] private MobileKeyboardHandler[] chats;
    [SerializeField] private float distance = 60f;   // 이만큼 내리면 키보드를 내린다 (캔버스 단위)

    private const float DecidePx = 14f;   // 이만큼 움직여야 방향을 정한다 (화면 픽셀 — 밀어 닫는 시트와 같다)

    private enum Kind { None, Mirror, Chat, Field }
    private enum Phase { Idle, Pending, Down, Done, Ignored }

    private Canvas root;
    private Phase phase;
    private Kind kind;
    private Pointer tracked;            // 이번 손가락을 누른 장치
    private Vector2 startPos;
    private Selectable under;           // 손가락 밑 칸·단추 — 내린 뒤 손을 뗄 때 눌리지 않게 꺼 둘 것
    private Selectable held;            // 꺼 둔 칸·단추
    private int releaseFrame = -1;

    private static readonly List<RaycastResult> hits = new List<RaycastResult>();
    private static readonly List<R0926KeyboardSwipe> live = new List<R0926KeyboardSwipe>();

    /// <summary>여기서 누른 손가락은 키보드 내리기 몫이다 — 아래로 밀어 닫는 시트는 받지 않는다</summary>
    public static bool Claims(Vector2 pos)
    {
        foreach (var s in live)
            if (s.ZoneAt(pos, out _) != Kind.None) return true;
        return false;
    }

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>(true);
        root = c != null ? c.rootCanvas : null;
    }

    private void OnEnable() => live.Add(this);

    private void OnDisable()
    {
        live.Remove(this);
        Unhold();
        phase = Phase.Idle;
    }

    private void Update()
    {
        if (held != null && releaseFrame >= 0 && Time.frameCount >= releaseFrame + 2) Unhold();   // UI 가 손 뗌을 처리한 뒤에 (Update 순서는 정해져 있지 않다)

        bool has = ReadPointer(out bool down, out bool pressedNow, out Vector2 pos);
        if (phase == Phase.Idle)
        {
            if (has && down && pressedNow) Begin(pos);
            return;
        }
        if (!has || !down)
        {
            phase = Phase.Idle;
            if (held != null && releaseFrame < 0) releaseFrame = Time.frameCount;
            return;
        }
        if (phase == Phase.Pending || phase == Phase.Down) Follow(pos);
    }

    // 누른 장치를 끝까지 따라간다 (R0926SwipeDismiss 와 같다)
    private bool ReadPointer(out bool down, out bool pressedNow, out Vector2 pos)
    {
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

    private void Begin(Vector2 pos)
    {
        kind = ZoneAt(pos, out under);
        phase = kind == Kind.None ? Phase.Ignored : Phase.Pending;
        startPos = pos;
    }

    private void Follow(Vector2 pos)
    {
        Vector2 d = pos - startPos;
        if (phase == Phase.Pending)
        {
            if (d.magnitude < DecidePx) return;
            bool downward = d.y < 0f && -d.y > Mathf.Abs(d.x) * 1.2f;
            phase = downward ? Phase.Down : Phase.Ignored;   // 옆·위로 시작한 손가락은 칸 몫 (커서 옮기기 등)
            if (!downward) return;
        }
        float scale = root != null ? root.scaleFactor : 1f;
        if (-d.y < distance * scale) return;
        phase = Phase.Done;
        Dismiss();
    }

    private void Dismiss()
    {
        switch (kind)
        {
            case Kind.Mirror:
                var mirror = ActiveMirror();
                if (mirror == null) return;   // 그사이 닫혔다
                mirror.CloseBySwipe();
                break;
            case Kind.Chat:
                var chat = ActiveChat();
                if (chat == null) return;
                chat.DismissBySwipe();
                break;
            case Kind.Field:
                var f = FocusedField();
                if (f == null) return;
                if (f is KeyboardPersistentInputField kp) kp.lockFocus = false;
                f.DeactivateInputField();
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject == f.gameObject) es.SetSelectedGameObject(null);
                break;
        }
        Hold(under);
    }

    // 손을 뗄 때까지 꺼 둔다 — 꺼진 칸·단추엔 손 뗌·누름이 가지 않는다
    private void Hold(Selectable s)
    {
        if (s == null || !s.enabled) return;
        var es = EventSystem.current;
        if (s is InputField f && es != null) f.OnEndDrag(new PointerEventData(es) { button = PointerEventData.InputButton.Left });   // 글 고르기 끌기를 멈춘다 (꺼진 칸엔 끌기 끝도 안 온다)
        s.enabled = false;
        held = s;
        releaseFrame = -1;
    }

    private void Unhold()
    {
        if (held != null) held.enabled = true;
        held = null;
        releaseFrame = -1;
    }

    // 지금 키보드를 쓰는 곳 중 이 손가락이 시작한 곳 — press 는 손 밑의 칸·단추
    private Kind ZoneAt(Vector2 pos, out Selectable press)
    {
        press = null;
        var es = EventSystem.current;
        if (es == null) return Kind.None;
        var mirror = ActiveMirror();
        var chat = mirror == null ? ActiveChat() : null;
        var field = mirror == null && chat == null ? FocusedField() : null;
        var kind = mirror != null ? Kind.Mirror : chat != null ? Kind.Chat : field != null ? Kind.Field : Kind.None;
        if (kind == Kind.None) return Kind.None;

        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = pos }, hits);
        if (hits.Count == 0 || !In(hits[0].gameObject.transform)) return Kind.None;
        var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        if (click != null && In(click.transform)) press = click.GetComponent<Selectable>();
        return kind;

        bool In(Transform t) => kind == Kind.Mirror ? mirror.Covers(t)
            : kind == Kind.Chat ? chat.inputAreaRect != null && t.IsChildOf(chat.inputAreaRect)
            : t.IsChildOf(field.transform) || Wraps(t, field.transform);
    }

    // 칸을 감싼 바탕(알약) — 칸의 부모·조부모 (팔로우 검색칸은 바탕이 부모)
    private static bool Wraps(Transform host, Transform f)
    {
        var p = f.parent;
        for (int i = 0; i < 2 && p != null; i++, p = p.parent)
            if (p == host) return true;
        return false;
    }

    private UploadInputMirror ActiveMirror()
    {
        if (mirrors == null) return null;
        foreach (var m in mirrors)
            if (m != null && m.isActiveAndEnabled && m.ActiveSource != null) return m;
        return null;
    }

    private MobileKeyboardHandler ActiveChat()
    {
        if (chats == null) return null;
        foreach (var c in chats)
            if (c != null && c.isActiveAndEnabled && c.KeyboardUp) return c;
        return null;
    }

    // 칸 안에서 바로 쓰는 칸 — 키보드 위 입력줄 자신은 입력줄 쪽에서 다룬다
    private InputField FocusedField()
    {
        var es = EventSystem.current;
        var go = es != null ? es.currentSelectedGameObject : null;
        var f = go != null ? go.GetComponent<InputField>() : null;
        if (f == null || !f.isFocused || !f.gameObject.activeInHierarchy) return null;
        if (mirrors != null)
            foreach (var m in mirrors)
                if (m != null && m.IsMirrorInput(f)) return null;
        return f;
    }
}
