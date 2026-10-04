using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 첫 사용 안내의 모양 — 화면 전체를 어둡게 하고 해당 도크 버튼만 동그랗게 비춘다.
/// 버튼 바로 위에 짧은 화살표가 까딱이고, 그 위에 '다음', 안내 문구, 쪽 점이 차례로 선다. '건너뛰기'는 오른쪽 위.
/// 쪽 넘기기·닫기·첫 실행 판정은 FirstTimeGuide 그대로 (다음 = 왼쪽으로 민 것과 같다, 시작하기·건너뛰기 = 확인 버튼).
/// 보이는 순서는 FirstTimeGuide 가 정하고, pages·targets 는 쪽 오브젝트와 비출 칸의 짝이다.
/// 다음·시작하기는 AR 오브젝트를 터치할 때와 같은 소리·진동을 낸다 (건너뛰기·뒤로가기는 조용히).
/// </summary>
public class R0926GuideOverlay : MonoBehaviour
{
    [SerializeField] private FirstTimeGuide guide;
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform container;   // 화면 전체를 덮도록 매 프레임 맞춘다
    [SerializeField] private Image[] dims;              // 구멍 둘레 네 조각 (위·아래·왼쪽·오른쪽)
    [SerializeField] private RectTransform hole;
    [SerializeField] private Image ring;
    [SerializeField] private RectTransform arrow;
    [SerializeField] private Text text;
    [SerializeField] private RectTransform next;
    [SerializeField] private Text nextLabel;
    [SerializeField] private Button confirm;
    [SerializeField] private GameObject[] pages;
    [SerializeField] private RectTransform[] targets;    // 쪽마다 비출 도크 칸
    [SerializeField] private float holeSize = 300f;
    [SerializeField] private float dimAlpha = 0.74f;
    [Tooltip("다음·시작하기 누름 소리 — 비우면 UIFeedbackManager 기본 소리(Touch, AR 오브젝트 터치와 같은 소리)")]
    [SerializeField] private AudioClip tapSound;
    [Tooltip("누름 진동 세기 — AR 오브젝트(큐브) 터치와 같게")]
    [SerializeField, Range(0f, 1f)] private float tapHaptic = 0.3f;

    private Canvas root;
    private RectTransform dots;
    private readonly Vector3[] c4 = new Vector3[4];
    private Vector2 holePos;
    private bool placed;

    private void OnEnable() { placed = false; }

    public void Next()
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect(tapHaptic, tapSound);
        if (IsLast()) { if (confirm != null) confirm.onClick.Invoke(); }
        else if (guide != null) guide.OnSwipe(-10000f);   // 왼쪽으로 민 것과 같다
    }

    public void Skip()
    {
        if (confirm != null) confirm.onClick.Invoke();
    }

    private int Current()
    {
        if (pages == null) return 0;
        for (int i = 0; i < pages.Length; i++) if (pages[i] != null && pages[i].activeInHierarchy) return i;
        return 0;
    }

    // 마지막 쪽인가 — 순서는 FirstTimeGuide 기준 (pages 배열 순서와 달라도 맞게)
    private bool IsLast()
    {
        if (guide != null && guide.PageCount > 0) return guide.CurrentPage >= guide.PageCount - 1;
        return pages != null && Current() >= pages.Length - 1;
    }

    private void LateUpdate()
    {
        if (container == null || panel == null) return;
        if (root == null) { var c = GetComponentInParent<Canvas>(); root = c != null ? c.rootCanvas : null; }
        if (root == null) return;

        // 안내 패널은 화면보다 작다 — 덮개는 화면 전체에 맞춘다
        ((RectTransform)root.transform).GetWorldCorners(c4);
        Vector3 bl = panel.InverseTransformPoint(c4[0]), tr = panel.InverseTransformPoint(c4[2]);
        container.anchorMin = container.anchorMax = container.pivot = new Vector2(0.5f, 0.5f);
        container.localPosition = (bl + tr) / 2f;
        container.sizeDelta = new Vector2(tr.x - bl.x, tr.y - bl.y);
        container.localScale = Vector3.one;
        float W = container.sizeDelta.x, H = container.sizeDelta.y;

        // 옛 전체 디밍·스와이프 힌트는 쓰지 않는다 (FirstTimeGuide 가 실행 중에 만든다)
        foreach (Transform ch in panel)
        {
            if (ch.name == "BackgroundDim") { var im = ch.GetComponent<Image>(); if (im != null && im.color.a > 0f) im.color = new Color(0f, 0f, 0f, 0f); }
            else if (ch.name == "SwipeHint" && ch.gameObject.activeSelf) ch.gameObject.SetActive(false);
            else if (ch.name == "DotIndicator" && dots == null) { dots = (RectTransform)ch; dots.SetParent(container, false); }
        }

        int p = Current();
        var target = targets != null && p < targets.Length ? targets[p] : null;
        if (target == null) return;

        // 비출 칸의 가운데 (덮개 기준, 아래 가운데가 원점)
        target.GetWorldCorners(c4);
        Vector3 cw = (c4[0] + c4[2]) / 2f;
        Vector3 cl = container.InverseTransformPoint(cw);
        Vector2 want = new Vector2(cl.x, cl.y + H / 2f);
        float slotTop = container.InverseTransformPoint(c4[1]).y + H / 2f;
        holePos = placed ? Vector2.Lerp(holePos, want, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)) : want;
        placed = true;

        float r = holeSize / 2f;
        Place(hole, holePos, new Vector2(holeSize, holeSize));
        if (dims != null && dims.Length >= 4)
        {
            // 위 · 아래 · 왼쪽 · 오른쪽 — 구멍 그림 가장자리와 겹치게 1씩 들인다
            Place(dims[0].rectTransform, new Vector2(0f, (holePos.y + r - 1f + H) / 2f), new Vector2(W, H - (holePos.y + r - 1f)));
            Place(dims[1].rectTransform, new Vector2(0f, (holePos.y - r + 1f) / 2f), new Vector2(W, holePos.y - r + 1f));
            Place(dims[2].rectTransform, new Vector2((-W / 2f + holePos.x - r + 1f) / 2f, holePos.y), new Vector2(holePos.x - r + 1f + W / 2f, holeSize - 2f));
            Place(dims[3].rectTransform, new Vector2((W / 2f + holePos.x + r - 1f) / 2f, holePos.y), new Vector2(W / 2f - (holePos.x + r - 1f), holeSize - 2f));
            foreach (var d in dims) d.color = new Color(0.016f, 0.024f, 0.031f, dimAlpha);
        }
        var hi = hole.GetComponent<Image>();
        if (hi != null) hi.color = new Color(0.016f, 0.024f, 0.031f, dimAlpha);

        float t = Time.unscaledTime;
        if (ring != null)
        {
            float k = Mathf.Repeat(t / 2.2f, 1f);
            Place(ring.rectTransform, holePos, new Vector2(holeSize * 0.9f, holeSize * 0.9f));
            ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.3f, 1f - (1f - k) * (1f - k));
            ring.color = new Color(1f, 1f, 1f, 0.55f * (1f - k));
        }

        // 화살표 → 다음 → 글자 → 점 (아래에서 위로)
        float arrowY = slotTop + 120f;
        if (arrow != null) Place(arrow, new Vector2(want.x, arrowY + 22f * Mathf.Sin(t * Mathf.PI * 2f / 1.25f)), arrow.sizeDelta);
        float nextY = arrowY + 150f;
        if (next != null)
        {
            if (nextLabel != null)
            {
                bool last = IsLast();
                string lab = last ? L("시작하기", "Get started", "はじめる", "开始", "Empezar") : L("다음", "Next", "次へ", "下一步", "Siguiente");
                if (nextLabel.text != lab) nextLabel.text = lab;
                next.sizeDelta = new Vector2(nextLabel.preferredWidth + 150f, next.sizeDelta.y);
            }
            Place(next, new Vector2(0f, nextY + next.sizeDelta.y / 2f), next.sizeDelta);
        }
        float textBottom = nextY + (next != null ? next.sizeDelta.y : 0f) + 64f;
        if (text != null)
        {
            var trt = text.rectTransform;
            float th = text.preferredHeight;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.sizeDelta = new Vector2(W - 140f, th + 10f);
            // 글자는 안내 패널의 자식이고 패널은 화면보다 작고 아래로 치우쳐 있다 — 덮개(화면) 기준 자리로 옮겨 놓는다.
            // 예전엔 패널 기준으로 놓아 글자가 50 쯤 내려가 '다음' 버튼과 겹쳤다
            trt.position = container.TransformPoint(new Vector3(0f, textBottom - H / 2f, 0f));
            if (dots != null)
            {
                dots.anchorMin = dots.anchorMax = new Vector2(0.5f, 0.5f);
                dots.pivot = new Vector2(0.5f, 0.5f);
                dots.localPosition = new Vector3(0f, textBottom + th + 60f - H / 2f, 0f);
            }
        }
    }

    // 덮개 기준(아래 가운데 원점) 위치에 놓는다
    private void Place(RectTransform rt, Vector2 bottomCenterPos, Vector2 size)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = bottomCenterPos;
        rt.sizeDelta = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
    }

    private static string L(string ko, string en, string ja, string zh, string es)
    {
        switch (R0926LocalizedText.Lang())
        {
            case "ko": return ko;
            case "ja": return ja;
            case "zh": return zh;
            case "es": return es;
            default: return en;
        }
    }
}
