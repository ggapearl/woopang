using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 첫 사용 안내의 모양 — 화면 전체를 어둡게 하고 해당 도크 버튼만 동그랗게 비춘다.
/// 버튼 바로 위에 짧은 화살표가 까딱이고, 안내 글(쪽 점 · 문구 · '다음')은 화면 가운데 조금 위에 크게 선다 (10-20 대표님 「위쪽으로 · 크게」).
/// 글과 버튼 사이는 칸 쪽으로 흘러가는 점선 길이 잇는다. '건너뛰기'는 오른쪽 위.
/// 가로 화면(도크가 오른쪽 세로 막대)에선 화살표가 칸 왼쪽에서 칸을 가리키고, 글은 남은 왼쪽 자리 가운데에 선다.
/// 쪽 넘기기·닫기·첫 실행 판정은 FirstTimeGuide 그대로 (다음 = 왼쪽으로 민 것과 같다, 시작하기·건너뛰기 = 확인 버튼).
/// 보이는 순서는 FirstTimeGuide 가 정하고, pages·targets 는 쪽 오브젝트와 비출 칸의 짝이다.
/// 다음·시작하기는 AR 오브젝트를 터치할 때와 같은 소리·진동을 낸다 (건너뛰기·뒤로가기는 조용히).
/// 안내 중에도 하단 버튼(도크)은 눌린다 — 누르면 그 버튼 일을 하고 안내는 닫힌다. AR 오브젝트는 안내가 끝난 뒤부터 (10-19 대표님).
/// </summary>
public class R0926GuideOverlay : MonoBehaviour, ICanvasRaycastFilter
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
    [SerializeField] private RectTransform dock;         // 안내 막이 비켜 주는 자리 (하단 버튼 줄)
    [SerializeField] private RectTransform skip;         // 오른쪽 위 '건너뛰기' — 안내 글은 이 아래까지만
    [SerializeField] private R0926GuidePath path;        // 안내 글 → 화살표 점선 길
    [SerializeField] private float holeSize = 300f;
    [SerializeField] private float dimAlpha = 0.74f;
    [Tooltip("다음·시작하기 누름 소리 — 비우면 UIFeedbackManager 기본 소리(Touch, AR 오브젝트 터치와 같은 소리)")]
    [SerializeField] private AudioClip tapSound;
    [Tooltip("누름 진동 세기 — AR 오브젝트(큐브) 터치와 같게")]
    [SerializeField, Range(0f, 1f)] private float tapHaptic = 0.3f;

    private Canvas root;
    private RectTransform dots;
    private readonly Vector3[] c4 = new Vector3[4];
    private readonly Vector3[] s4 = new Vector3[4];
    private Vector2 holePos;
    private bool placed;

    private Button[] dockButtons;
    private bool closing;

    /// <summary>첫 사용 안내가 떠 있는 동안 — AR 오브젝트 터치(두 번 톡톡·회전·소리·보물·사람)는 이걸 보고 손을 뗀다</summary>
    public static bool BlocksWorld { get; private set; }

    private void OnEnable()
    {
        placed = false;
        closing = false;
        BlocksWorld = true;
        dockButtons = dock != null ? dock.GetComponentsInChildren<Button>(true) : null;
        if (dockButtons != null) foreach (var b in dockButtons) b.onClick.AddListener(DockTapped);
    }

    private void OnDisable()
    {
        BlocksWorld = false;
        if (dockButtons != null) foreach (var b in dockButtons) if (b != null) b.onClick.RemoveListener(DockTapped);
        dockButtons = null;
    }

    // 도크 자리에서는 안내 막(어둡게·구멍·끌기 판)이 손가락을 받지 않는다 — 아래 도크 버튼이 그대로 눌린다
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        return dock == null || !dock.gameObject.activeInHierarchy || !RectTransformUtility.RectangleContainsScreenPoint(dock, sp, eventCamera);
    }

    // 하단 버튼을 눌렀다 — 버튼 일(창 열기)은 버튼이 하고, 안내는 조용히 닫는다 (목록·추가·프로필 창은 안내 막 아래에 열린다)
    private void DockTapped()
    {
        if (closing || confirm == null) return;
        closing = true;
        confirm.onClick.Invoke();
    }

    public void Next()
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect(tapHaptic, tapSound);
        if (IsLast()) { if (confirm != null) confirm.onClick.Invoke(); }
        else if (guide != null) guide.OnSwipe(-10000f);   // 왼쪽으로 민 것과 같다
    }

    public void Skip()
    {
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect(tapHaptic, tapSound);
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

        // 화살표 — 비춘 칸 바깥에서 칸을 가리키며 까딱인다 (구멍을 따라 움직인다)
        bool land = R0926Orientation.IsLandscape && W > H;
        Vector2 aSize = arrow != null ? arrow.sizeDelta : new Vector2(84f, 106f);
        const float Bob = 24f;
        float reach = r + 20f + aSize.y / 2f + Bob;                        // 구멍 가운데 → 화살표 가운데 (까딱임 가운데)
        float bob = Bob * Mathf.Sin(t * Mathf.PI * 2f / 1.25f);
        Vector2 arrowAt = land ? new Vector2(holePos.x - reach, holePos.y) : new Vector2(holePos.x, holePos.y + reach);
        if (arrow != null)
        {
            Place(arrow, arrowAt + (land ? new Vector2(-bob, 0f) : new Vector2(0f, bob)), aSize);
            arrow.localRotation = Quaternion.Euler(0f, 0f, land ? 90f : 0f);   // 그림은 아래를 가리킨다 — 가로에선 오른쪽(칸)으로
        }

        // 안내 글이 설 자리 — 위는 '건너뛰기' 아래, 아래(가로는 오른쪽)는 화살표 바깥
        float top = H - 340f;
        if (skip != null && skip.gameObject.activeInHierarchy)
        {
            skip.GetWorldCorners(s4);
            top = container.InverseTransformPoint(s4[0]).y + H / 2f - 50f;
        }
        float arrowEdge = reach + aSize.y / 2f + Bob;                      // 구멍 가운데 → 화살표 바깥 끝 (가장 멀리 까딱일 때)
        float left = -W / 2f + 140f, right = W / 2f - 140f, bottom = 60f;
        if (land) right = Mathf.Min(right, holePos.x - arrowEdge - 90f);
        else bottom = holePos.y + arrowEdge + 90f;
        float textW = land ? Mathf.Clamp(right - left, 400f, 1300f) : W - 160f;
        float blockX = land ? (left + right) / 2f : 0f;

        // 크기 — 점 · 문구 · 다음 (위에서 아래로)
        float nextH = 0f, nextW = 0f;
        if (next != null)
        {
            if (nextLabel != null)
            {
                bool last = IsLast();
                string lab = last ? L("시작하기", "Get started", "はじめる", "开始", "Empezar") : L("다음", "Next", "次へ", "下一步", "Siguiente");
                if (nextLabel.text != lab) nextLabel.text = lab;
                next.sizeDelta = new Vector2(Mathf.Max(440f, nextLabel.preferredWidth + 200f), next.sizeDelta.y);
            }
            nextH = next.sizeDelta.y;
            nextW = next.sizeDelta.x;
        }
        float th = 0f;
        if (text != null)
        {
            var trt = text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.sizeDelta = new Vector2(textW, trt.sizeDelta.y);   // 폭을 먼저 — 줄바꿈 높이가 이 폭으로 잰다
            th = text.preferredHeight;
            trt.sizeDelta = new Vector2(textW, th + 10f);
        }
        const float DotsH = 46f, DotsGap = 60f, NextGap = 84f;
        float blockH = DotsH + DotsGap + th + NextGap + nextH;
        float lo = bottom + blockH / 2f, up = top - blockH / 2f;
        float cy = lo <= up ? Mathf.Clamp(H * (land ? 0.5f : 0.54f), lo, up) : (bottom + top) / 2f;
        float blockBottom = cy - blockH / 2f;

        if (next != null) Place(next, new Vector2(blockX, blockBottom + nextH / 2f), next.sizeDelta);
        float textBottom = blockBottom + nextH + NextGap;
        if (text != null)
        {
            // 글자는 안내 패널의 자식이고 패널은 화면보다 작고 아래로 치우쳐 있다 — 덮개(화면) 기준 자리로 옮겨 놓는다
            text.rectTransform.position = container.TransformPoint(new Vector3(blockX, textBottom - H / 2f, 0f));
        }
        if (dots != null)
        {
            dots.anchorMin = dots.anchorMax = new Vector2(0.5f, 0.5f);
            dots.pivot = new Vector2(0.5f, 0.5f);
            dots.localPosition = new Vector3(blockX, textBottom + th + DotsGap + DotsH / 2f - H / 2f, 0f);
        }

        // 점선 길 — '다음' 아래(가로는 오른쪽)에서 화살표까지. 세로·가로 모두 끝은 화살표 방향으로 들어간다
        if (path != null)
        {
            Vector2 a, b, a1, b1;
            if (land)
            {
                a = new Vector2(blockX + nextW / 2f + 40f, blockBottom + nextH / 2f);
                b = new Vector2(holePos.x - arrowEdge - 26f, holePos.y);
                float dx = b.x - a.x;
                a1 = a + new Vector2(0.45f * dx, 0f); b1 = b - new Vector2(0.45f * dx, 0f);
                path.show = dx > 160f;
            }
            else
            {
                a = new Vector2(blockX, blockBottom - 40f);
                b = new Vector2(holePos.x, holePos.y + arrowEdge + 26f);
                float dy = a.y - b.y;
                a1 = a - new Vector2(0f, 0.45f * dy); b1 = b + new Vector2(0f, 0.45f * dy);
                path.show = dy > 160f;
            }
            var half = new Vector2(0f, H / 2f);   // 길의 좌표는 덮개 가운데 기준
            path.p0 = a - half; path.p1 = a1 - half; path.p2 = b1 - half; path.p3 = b - half;
            path.Refresh();
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
