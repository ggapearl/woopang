using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 추가 사진 칸 — 사진을 꾹 눌러 들어 올린 채 끌면 보이는 순서가 바뀐다 (장소 추가 · 3D 모델 추가 · 장소 정보 수정이 같이 쓴다).
/// 10-26 대표님 「필요없는 사진이나 보이는 순서를 조정하고 싶을 때는 … 사진들을 옮겨서 … 보이는 순서를 조절」.
/// 칸은 제자리에 두고 사진(매니저의 목록)만 옮긴다 — 들고 있는 사진이 다른 사진 칸 위로 가면 그 자리에 끼워 넣고(사이 사진은 한 칸씩 밀림),
/// 들고 있는 사진의 자리는 흐리게 비워 둔다. 손을 떼면 떠 있던 사진이 그 칸으로 내려앉는다.
/// 터치를 직접 읽는다(R0926SwipeDismiss 와 같은 방식) — 오른쪽 위 x 위에서 꾹 눌러도 들리고, 그때 x 는 눌리지 않는다(Engaged).
/// 꾹 누르기 전에 손가락이 움직이면 들지 않는다(시트 밀어 닫기·옆으로 넘기기 몫) — 들고 있는 동안엔 그 둘이 이 손가락을 놓아준다(Holding).
/// 칸 목록·옮기기는 매니저가 Bind 로 넘기고, 떠 있는 사진(ghost)은 Redesign0926Apply 가 만들어 꺼 둔다
/// </summary>
public class R0926PhotoReorder : MonoBehaviour
{
    [SerializeField] private Image ghost;   // 들고 있는 사진 — 칸 위에 떠서 손가락을 따라간다

    private const float HoldTime = 0.35f;
    private const float SlopPx = 12f;       // 들기 전에 이만큼(화면 픽셀) 움직이면 끌기·넘기기 — 시트들이 방향을 정하는 14px 보다 먼저 물러난다
    private const float LiftScale = 1.1f;
    private const float DimAlpha = 0.25f;
    private const float SettleTime = 0.16f;

    private static R0926PhotoReorder holder;
    /// <summary>사진을 들고 있다 — 시트 밀어 닫기·옆으로 넘기기는 이 손가락을 놓아준다</summary>
    public static bool Holding => holder != null && holder.held >= 0;

    /// <summary>들고 있거나 막 내려놓은 참 — 그동안 칸의 x 는 눌려도 사진을 빼지 않는다</summary>
    public bool Engaged => held >= 0 || settleT >= 0f || Time.frameCount <= quietUntil;

    private IList<Image> cells;
    private Func<int> count;
    private Action<int, int> move;
    private Func<bool> busy;

    private Canvas root;
    private Pointer tracked;          // 이번에 누른 장치 — 터치·마우스가 섞여도 끝까지 그것만 본다
    private int pressIndex = -1;
    private Vector2 pressPos;
    private float pressAt;
    private int held = -1;            // 들고 있는 사진의 지금 자리
    private int heldCount;            // 들었을 때 사진 수 — 그사이 바뀌면(불러오기·초기화) 바로 내려놓는다
    private Vector2 grab;             // 손가락 → 들고 있는 사진 가운데 (화면 픽셀)
    private int dimmed = -1;
    private readonly List<GameObject> hiddenKids = new List<GameObject>();   // 비워 둔 칸의 x
    private int quietUntil = -1;
    private float settleT = -1f, settleScale = 1f;
    private Vector3 settleFrom, settleTo;

    private static readonly List<RaycastResult> hits = new List<RaycastResult>();

    /// <param name="photoCells">사진 칸 (사진 순서대로)</param>
    /// <param name="photoCount">지금 사진 수 — 앞에서부터 그만큼이 사진 칸</param>
    /// <param name="movePhoto">from 자리 사진을 to 자리로 (사이는 한 칸씩 밀림) — 칸을 다시 그리는 것까지</param>
    /// <param name="isBusy">불러오는 중·올리는 중 — 들지 않는다</param>
    public void Bind(IList<Image> photoCells, Func<int> photoCount, Action<int, int> movePhoto, Func<bool> isBusy)
    {
        cells = photoCells;
        count = photoCount;
        move = movePhoto;
        busy = isBusy;
    }

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>(true);
        root = c != null ? c.rootCanvas : null;
    }

    private void OnDisable() => Drop(true);

    private void Update()
    {
        if (settleT >= 0f) Settle();
        if (cells == null || count == null) return;
        bool has = ReadPointer(out bool down, out bool pressedNow, out Vector2 pos);
        if (held >= 0)
        {
            if (!has || !down) { Drop(false); return; }
            if (count() != heldCount || (busy != null && busy())) { Drop(true); return; }
            Follow(pos);
            return;
        }
        if (pressIndex >= 0)
        {
            if (!has || !down || (pos - pressPos).sqrMagnitude > SlopPx * SlopPx || R0926SwipeDismiss.AnyDragging) { pressIndex = -1; return; }
            if (Time.unscaledTime - pressAt >= HoldTime) Lift(pos);
            return;
        }
        if (has && down && pressedNow && settleT < 0f && (busy == null || !busy())) Press(pos);
    }

    private bool ReadPointer(out bool down, out bool pressedNow, out Vector2 pos)
    {
#if UNITY_EDITOR
        if (fed) { down = fedDown; pressedNow = fedPressed; fedPressed = false; pos = fedPos; return true; }
#endif
        if (pressIndex < 0 && held < 0)
        {
            tracked = null;
            foreach (var d in InputSystem.devices)
                if (d is Pointer p && d.enabled && p.press.wasPressedThisFrame) { tracked = p; break; }
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
    public void EditorFeed(bool down, Vector2 pos) { fed = true; if (down && !fedDown) fedPressed = true; fedDown = down; fedPos = pos; }
    public void EditorFeedEnd() { fed = false; fedDown = false; }
    public bool EditorGhostShown => ghost != null && ghost.gameObject.activeSelf;
    public int EditorDimmed => dimmed;
#endif

    // 맨 위에 맞은 것이 사진 칸(또는 그 칸의 x)일 때만 — 다른 창에 가려져 있으면 들지 않는다
    private void Press(Vector2 pos)
    {
        var es = EventSystem.current;
        if (es == null) return;
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = pos }, hits);
        if (hits.Count == 0) return;
        var top = hits[0].gameObject.transform;
        int n = Mathf.Min(count(), cells.Count);
        for (int i = 0; i < n; i++)
        {
            if (cells[i] == null || !cells[i].gameObject.activeInHierarchy || !top.IsChildOf(cells[i].transform)) continue;
            pressIndex = i;
            pressPos = pos;
            pressAt = Time.unscaledTime;
            return;
        }
    }

    private void Lift(Vector2 pos)
    {
        held = pressIndex;
        pressIndex = -1;
        heldCount = count();
        holder = this;
        var cell = cells[held].rectTransform;
        grab = (Vector2)RectTransformUtility.WorldToScreenPoint(Cam, cell.TransformPoint(cell.rect.center)) - pos;
        if (ghost != null)
        {
            var g = ghost.rectTransform;
            var space = (RectTransform)g.parent;
            ghost.sprite = cells[held].sprite;
            g.SetAsLastSibling();
            g.sizeDelta = SizeIn(cell, space);
            g.localScale = Vector3.one;
            g.localPosition = CenterIn(cell, space);
            ghost.gameObject.SetActive(true);
        }
        Dim(held);
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
    }

    private void Follow(Vector2 pos)
    {
        Vector2 at = pos + grab;   // 들고 있는 사진의 가운데
        if (ghost != null)
        {
            var g = ghost.rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)g.parent, at, Cam, out var local);
            g.localPosition = local;
            float s = Mathf.Lerp(g.localScale.x, LiftScale, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            g.localScale = new Vector3(s, s, 1f);
        }
        int to = Over(at);
        if (to < 0 || to == held) return;
        move(held, to);
        held = to;
        Dim(to);
    }

    // 그 자리의 사진 칸 (칸 사이 틈 · 빈 칸이면 -1 — 그대로 둔다)
    private int Over(Vector2 screen)
    {
        int n = Mathf.Min(count(), cells.Count);
        for (int i = 0; i < n; i++)
            if (cells[i] != null && cells[i].gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(cells[i].rectTransform, screen, Cam)) return i;
        return -1;
    }

    // 손을 뗐다 — 떠 있던 사진이 그 칸으로 내려앉는다 (now: 창이 닫힘·사진 수가 바뀜 — 바로 치운다)
    private void Drop(bool now)
    {
        pressIndex = -1;
        if (held >= 0)
        {
            int at = held;
            held = -1;
            quietUntil = Time.frameCount + 2;   // UI 가 손 뗌을 처리한 뒤까지 (Update 순서는 정해져 있지 않다)
            if (!now && ghost != null && at < cells.Count && cells[at] != null)
            {
                var g = ghost.rectTransform;
                settleFrom = g.localPosition;
                settleTo = CenterIn(cells[at].rectTransform, (RectTransform)g.parent);
                settleScale = g.localScale.x;
                settleT = 0f;
            }
        }
        if (holder == this) holder = null;
        if (now || settleT < 0f) { settleT = -1f; Hide(); }
    }

    private void Settle()
    {
        settleT += Time.unscaledDeltaTime / SettleTime;
        float k = Mathf.Clamp01(settleT), e = 1f - (1f - k) * (1f - k);
        var g = ghost.rectTransform;
        g.localPosition = Vector3.LerpUnclamped(settleFrom, settleTo, e);
        float s = Mathf.Lerp(settleScale, 1f, e);
        g.localScale = new Vector3(s, s, 1f);
        if (k < 1f) return;
        settleT = -1f;
        quietUntil = Time.frameCount + 2;
        Hide();
    }

    private void Hide()
    {
        if (ghost != null)
        {
            ghost.gameObject.SetActive(false);
            ghost.sprite = null;
        }
        Dim(-1);
    }

    // 들고 있는 사진의 자리 — 흐리게 비우고 x 를 감춘다 (-1 = 되돌림)
    private void Dim(int i)
    {
        if (dimmed == i) return;
        if (cells != null && dimmed >= 0 && dimmed < cells.Count && cells[dimmed] != null) SetAlpha(cells[dimmed], 1f);
        foreach (var k in hiddenKids) if (k != null) k.SetActive(true);
        hiddenKids.Clear();
        dimmed = i;
        if (cells == null || i < 0 || i >= cells.Count || cells[i] == null) return;
        SetAlpha(cells[i], DimAlpha);
        foreach (Transform k in cells[i].transform)
            if (k.gameObject.activeSelf) { hiddenKids.Add(k.gameObject); k.gameObject.SetActive(false); }
    }

    private static void SetAlpha(Image img, float a)
    {
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    private Camera Cam => root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

    private static Vector3 CenterIn(RectTransform rt, RectTransform space) => space.InverseTransformPoint(rt.TransformPoint(rt.rect.center));

    private static Vector2 SizeIn(RectTransform rt, RectTransform space)
    {
        Vector3 a = space.InverseTransformPoint(rt.TransformPoint(rt.rect.min));
        Vector3 b = space.InverseTransformPoint(rt.TransformPoint(rt.rect.max));
        return new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
    }
}
