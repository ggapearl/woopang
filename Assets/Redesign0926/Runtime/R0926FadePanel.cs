using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 페이드인으로 뜨고 페이드아웃으로 사라지는 창 (프로필). 카드는 살짝 커지며(0.97→1) 나타난다.
/// 닫을 때는 R0926CloseProxy 가 AnimateClose 를 불러, 다 옅어진 뒤 원래 닫기 버튼을 눌러 준다.
/// 도크에서 다른 창으로 바꿀 때(11-18 H2)는 커지지 않고 카드만 제자리에서 디졸브 — 창 전체(뒤 그늘 포함)는 그대로 두고
/// 카드의 끌어 닫기(R0926SwipeDismiss)가 카드 투명도와 뒤 그늘을 맡는다 (그늘이 두 겹이 되지 않게).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class R0926FadePanel : R0926Closer
{
    [SerializeField] private RectTransform card;
    [SerializeField] private float fadeIn = 0.22f;
    [SerializeField] private float fadeOut = 0.18f;
    [SerializeField] private float fromScale = 0.97f;

    private CanvasGroup group;
    private float shown;      // 0 = 안 보임, 1 = 다 보임
    private int dir;          // +1 나타나는 중, -1 사라지는 중
    private Button pending;
    private float pop = 1f;   // 카드에 지금 곱해 둔 커지는 비율 — 가로 화면 축소(R0926Orientation 의 localScale)를 덮어쓰지 않게 나눴다 곱한다
    private R0926SwipeDismiss cardSheet;
    private bool dissolving;  // 디졸브로 닫는 중 — 그동안 다른 닫기는 무시

    private void Awake() => group = GetComponent<CanvasGroup>();

    private R0926SwipeDismiss CardSheet()
    {
        if (cardSheet == null && card != null) cardSheet = card.GetComponent<R0926SwipeDismiss>();
        return cardSheet;
    }

    // 통째로 옅게 커지던 것을 멈추고 바로 다 보이게 — 디졸브는 카드만
    private void Settle()
    {
        dir = 0;
        pending = null;
        shown = 1f;
        Pose();
    }

    public override void DissolveIn(bool top, Dim dim)
    {
        if (!isActiveAndEnabled || dissolving) return;
        Settle();
        var s = CardSheet();
        if (s != null) s.DissolveIn(top, dim);
    }

    public override void DissolveClose(Button then, bool top, Dim dim)
    {
        if (!isActiveAndEnabled) { if (then != null) then.onClick.Invoke(); return; }
        if (dir < 0 || dissolving) return;
        var s = CardSheet();
        if (s == null || !s.isActiveAndEnabled) { AnimateClose(then); return; }
        Settle();
        dissolving = true;
        group.blocksRaycasts = false;   // 사라지는 창은 누름을 받지 않는다 — 뒤 그늘(바깥 누르기)까지
        s.DissolveClose(then, top, dim);
    }

    private void OnEnable()
    {
        shown = 0f;
        dir = 1;
        pending = null;
        Pose();
    }

    private void OnDisable()
    {
        dissolving = false;
        Settle();
    }

    public bool Closing => dir < 0;

    public override void AnimateClose(Button then)
    {
        if (!isActiveAndEnabled) { if (then != null) then.onClick.Invoke(); return; }
        if (dir < 0 || dissolving) return;
        pending = then;
        dir = -1;
    }

    private void Update()
    {
        if (dissolving && (cardSheet == null || !cardSheet.Dissolving))
        {
            dissolving = false;   // 카드가 다 옅어져 닫기를 눌렀는데 창이 그대로면 — 다시 누를 수 있게
            Pose();
        }
        if (dir == 0) return;
        shown = Mathf.Clamp01(shown + dir * Time.unscaledDeltaTime / (dir > 0 ? fadeIn : fadeOut));
        Pose();
        if (dir > 0 && shown >= 1f) dir = 0;
        else if (dir < 0 && shown <= 0f)
        {
            dir = 0;
            var b = pending;
            pending = null;
            if (b != null) b.onClick.Invoke();
            if (isActiveAndEnabled) { shown = 1f; Pose(); }   // 버튼이 창을 끄지 않았으면 다시 보이게
        }
    }

    private void Pose()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        float e = shown * shown * (3f - 2f * shown);
        group.alpha = e;
        group.blocksRaycasts = dir >= 0;
        if (card != null)
        {
            float next = Mathf.Lerp(fromScale, 1f, e);
            card.localScale = card.localScale / pop * next;
            pop = next;
        }
    }
}
