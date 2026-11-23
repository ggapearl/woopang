using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 창이 열려 있는 동안 도크의 그 칸 버튼(아이콘·이름·배지)을 감춘다 — 그 자리에는 창 쪽의 X '닫기'만 보이게.
/// 예전엔 메시지 칸에서 봉투 아이콘·'메시지' 위에 X·'닫기'가 겹쳐 '메닫기'로 보였다.
/// 버튼을 끄지 않고 투명하게만 한다 (MessagePanelManager 가 이름으로 버튼을 찾고 배지를 칠한다).
///
/// 창이 열린 채로 도크의 다른 칸을 누르면 — 두 창 모두 움직이지 않고 제자리에서 디졸브 (0.2초): 열려 있던 창은 옅어져 닫히고 새 창은 진해진다.
/// 대표님 11-08 「추가창이 열린 상태에서 -> 목록버튼 터치 … 기존창이 사라지고 해당 창이 발생」
///  + 11-18 H2 「그냥 자연스럽게 페이드인아웃(디졸브) 로 넘어가도록 … (너무 느리거나 잔상이 심하지 않게 / 슬라이드 없이)
///    슬라이드가 안에 조금 남아있어서 그런지 오히려 화면 좀 튀는 듯한 느낌」 — 예전(11-08)엔 새 창이 56 아래에서 올라오고 옛 창은 48 내려갔다.
/// 위에 그려지는 창만 0.2초 내내 바뀌고 아래 창은 위 창이 거의 덮은 동안에만 바뀐다 — 둘이 같이 반투명이면 그 사이로 AR 화면이 비쳐 깜박인다.
/// 뒤 어두운 바탕은 맨 아래 창 것 하나만 깐다 — 두 겹이 되면 바꾸는 동안 화면이 더 어두워졌다 밝아진다 (R0926Closer).
/// 예전엔 새 창이 열린 창 뒤(형제 순서가 아래)에서 올라와 안 보이거나, 위에 겹쳐 열려 닫아도 옛 창이 남았다.
/// '추가'와 '보물찾기' 칸은 추가 창 하나를 나눠 쓴다(보물찾기 = 세 번째 카드 한 장) — 열린 칸은 카드로 가린다 (11-09).
/// 더블탭 정보 창이 떠 있는 동안은 도크 칸이 손가락을 받지 않는다 — 도크 바탕이 받아 아무 일도 없다 (11-16 대표님 「하단 메뉴가 터치되면 안되는데」).
/// </summary>
public class R0926DockSlotSwap : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public CanvasGroup button;
        public GameObject[] panels;
        [Tooltip("추가 창처럼 두 칸이 한 창을 나눠 쓰면 — 그 창의 카드 넘기기. alone 이면 세 번째 카드 한 장(보물찾기)일 때만 이 칸의 창이 열린 것")]
        public SwipePanelController swipe;
        public bool alone;
    }

    [SerializeField] private Slot[] slots;

    private Slot switchTo;       // 다른 창이 열린 채로 누른 칸 — 그 창이 열리면 바꾼다
    private float switchUntil;   // 프로필처럼 받아 온 뒤 열리는 창을 기다리는 끝 (로그인 안내로 안 열리면 그냥 둔다)
    private static R0926DockSlotSwap live;

    /// <summary>
    /// 화면의 그 점에 손가락이 닿을 도크 칸(보이고 누를 수 있는 칸)이 있는지 — 창의 어두운 바탕이 그 자리만 통과시킨다 (R0926DockPassThrough).
    /// 열린 창 자기 칸은 숨어 있어(누를 수 없게) 여기 안 든다 — 그 자리는 창의 X 가 받는다.
    /// </summary>
    public static bool SlotAt(Vector2 screen, Camera cam)
    {
        if (live == null || live.slots == null || !live.isActiveAndEnabled) return false;
        foreach (var s in live.slots)
        {
            var hit = s != null && s.button != null ? s.button.GetComponent<Graphic>() : null;
            if (hit == null || !hit.raycastTarget || !hit.isActiveAndEnabled) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(hit.rectTransform, screen, cam) && hit.Raycast(screen, cam)) return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        if (live == this) live = null;
    }

    private void Awake()
    {
        live = this;
        if (slots == null) return;
        foreach (var s in slots)
        {
            var b = s != null && s.button != null ? s.button.GetComponent<Button>() : null;
            if (b == null) continue;
            var slot = s;
            b.onClick.AddListener(() => Tapped(slot));
        }
    }

    private void Tapped(Slot to)
    {
        switchTo = OthersOpen(to) ? to : null;
        switchUntil = Time.unscaledTime + 2f;
    }

    private void LateUpdate()
    {
        if (slots == null) return;
        if (switchTo != null)
        {
            if (!OthersOpen(switchTo) || Time.unscaledTime > switchUntil) switchTo = null;
            else if (IsOpen(switchTo)) { Switch(switchTo); switchTo = null; }
        }
        bool locked = DoubleTap3D.Shown != null;
        foreach (var s in slots)
        {
            if (s == null || s.button == null || s.panels == null) continue;
            bool open = IsOpen(s);
            float a = open ? 0f : 1f;
            if (s.button.alpha != a)
            {
                s.button.alpha = a;
                s.button.interactable = !open;
            }
            bool hit = !open && !locked;   // 모양은 그대로 (눌림 막힘 색으로 바뀌지 않게 interactable 은 두고) — 맞힘만 끈다
            if (s.button.blocksRaycasts != hit) s.button.blocksRaycasts = hit;
        }
    }

    // 같은 프레임 안 (그리기 전) — 새 창은 올라오지 않고 제자리에 서서 옅게 → 진하게, 열려 있던 창은 그 칸의 닫기(X)를 눌러 준다 (옅어진 뒤).
    // 닫는 로직은 각 창의 원래 닫기 버튼 그대로 (R0926CloseProxy → DissolveClose → 원래 버튼).
    private void Switch(Slot to)
    {
        var olds = new List<GameObject>();
        foreach (var s in slots)
        {
            if (s == null || s == to || s.panels == null || !IsOpen(s)) continue;
            foreach (var p in s.panels)
                if (p != null && p.activeInHierarchy && Array.IndexOf(to.panels, p) < 0 && !olds.Contains(p)) olds.Add(p);   // 같은 창을 나눠 쓰는 칸(추가 ↔ 보물찾기) — 창은 그대로, 카드만 바뀐다 (SwipePanelController)
        }
        var news = new List<GameObject>();
        foreach (var p in to.panels)
            if (p != null && p.activeInHierarchy && !news.Contains(p)) news.Add(p);
        if (olds.Count == 0 || news.Count == 0) return;

        var topOld = Top(olds).transform;
        var topNew = Top(news).transform;
        GameObject bottom = null;   // 뒤 그늘을 깔아 둘 창 — 맨 아래
        foreach (var p in olds) if (bottom == null || R0926Closer.DrawsAbove(bottom.transform, p.transform)) bottom = p;
        foreach (var p in news) if (R0926Closer.DrawsAbove(bottom.transform, p.transform)) bottom = p;

        foreach (var p in news)
        {
            foreach (var r in p.GetComponentsInChildren<R0926SlideIn>()) r.Stand();
            var dim = p == bottom ? R0926Closer.Dim.Keep : R0926Closer.Dim.Hide;
            bool top = R0926Closer.DrawsAbove(p.transform, topOld);
            foreach (var c in Closers(p)) c.DissolveIn(top, dim);
        }
        foreach (var p in olds)
        {
            var mirror = p.transform.Find("DockMirror0926");
            if (mirror == null) continue;
            var dim = p == bottom ? R0926Closer.Dim.Keep : R0926Closer.Dim.Hide;
            bool top = R0926Closer.DrawsAbove(p.transform, topNew);
            foreach (var c in mirror.GetComponentsInChildren<R0926CloseProxy>()) c.Dissolve(top, dim);
        }
    }

    private static GameObject Top(List<GameObject> panels)
    {
        GameObject t = null;
        foreach (var p in panels) if (t == null || R0926Closer.DrawsAbove(p.transform, t.transform)) t = p;
        return t;
    }

    // 창의 도크 거울 X 들이 닫는 시트 · 창 (같은 것은 한 번)
    private static List<R0926Closer> Closers(GameObject panel)
    {
        var list = new List<R0926Closer>();
        var mirror = panel.transform.Find("DockMirror0926");
        if (mirror == null) return list;
        foreach (var c in mirror.GetComponentsInChildren<R0926CloseProxy>(true))
            if (c.Sheet != null && !list.Contains(c.Sheet)) list.Add(c.Sheet);
        return list;
    }

    private bool OthersOpen(Slot except)
    {
        foreach (var s in slots)
            if (s != null && s != except && IsOpen(s)) return true;
        return false;
    }

    private static bool IsOpen(Slot s)
    {
        if (s?.panels == null) return false;
        if (s.swipe != null && s.swipe.AloneShowing != s.alone) return false;
        foreach (var p in s.panels)
            if (p != null && p.activeInHierarchy) return true;
        return false;
    }
}
