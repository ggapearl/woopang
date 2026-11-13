using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 창이 열려 있는 동안 도크의 그 칸 버튼(아이콘·이름·배지)을 감춘다 — 그 자리에는 창 쪽의 X '닫기'만 보이게.
/// 예전엔 메시지 칸에서 봉투 아이콘·'메시지' 위에 X·'닫기'가 겹쳐 '메닫기'로 보였다.
/// 버튼을 끄지 않고 투명하게만 한다 (MessagePanelManager 가 이름으로 버튼을 찾고 배지를 칠한다).
///
/// 창이 열린 채로 도크의 다른 칸을 누르면 — 열려 있던 창은 옅어지며 조금 내려가 닫히고, 새 창은 조금 아래에서 옅게 올라온다 (동시에).
/// 대표님 11-08 「추가창이 열린 상태에서 -> 목록버튼 터치 … 기존창이 사라지고 해당 창이 발생」
///  + 「아예 슬라이드로 내려갔다 올라오려면 시간도 길어지고 좀 과해서 … 동시에 디졸브로 내려가면서 다른 창이 페이드인으로 올라오는 식」
/// 예전엔 새 창이 열린 창 뒤(형제 순서가 아래)에서 올라와 안 보이거나, 위에 겹쳐 열려 닫아도 옛 창이 남았다.
/// '추가'와 '보물찾기' 칸은 추가 창 하나를 나눠 쓴다(보물찾기 = 세 번째 카드 한 장) — 열린 칸은 카드로 가린다 (11-09).
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

    private const float RiseDistance = 56f, RiseTime = 0.24f;
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
        foreach (var s in slots)
        {
            if (s == null || s.button == null || s.panels == null) continue;
            bool open = IsOpen(s);
            float a = open ? 0f : 1f;
            if (s.button.alpha != a)
            {
                s.button.alpha = a;
                s.button.blocksRaycasts = !open;
                s.button.interactable = !open;
            }
        }
    }

    // 같은 프레임 안 (그리기 전) — 새 창은 짧게 옅게 올라오고, 열려 있던 창은 그 칸의 닫기(X)를 눌러 준다.
    // 닫는 로직은 각 창의 원래 닫기 버튼 그대로 (R0926CloseProxy → AnimateClose → 원래 버튼). 이 프레임의 닫기만 옅어지며 조금 내려간다.
    private void Switch(Slot to)
    {
        foreach (var p in to.panels)
            if (p != null && p.activeInHierarchy)
                foreach (var r in p.GetComponentsInChildren<R0926SlideIn>())
                    r.Rise(RiseDistance, RiseTime);
        R0926Closer.DissolveFrame = Time.frameCount;
        foreach (var s in slots)
        {
            if (s == null || s == to || s.panels == null || !IsOpen(s)) continue;
            foreach (var p in s.panels)
            {
                if (System.Array.IndexOf(to.panels, p) >= 0) continue;   // 같은 창을 나눠 쓰는 칸(추가 ↔ 보물찾기) — 창은 그대로, 카드만 바뀐다
                var mirror = p != null && p.activeInHierarchy ? p.transform.Find("DockMirror0926") : null;
                if (mirror == null) continue;
                foreach (var c in mirror.GetComponentsInChildren<R0926CloseProxy>()) c.Run();
            }
        }
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
