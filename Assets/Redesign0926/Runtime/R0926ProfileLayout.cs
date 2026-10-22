using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프로필 카드 배치 (0930 시안) — 보이는 항목만 위에서부터 차례로 쌓고 카드 높이를 내용에 맞춘다.
/// 내 프로필/남의 프로필에 따라 ProfileManager 가 버튼·SNS 줄을 켜고 끄고 위치도 옮기는데,
/// 예전엔 좌표가 박혀 있어 빈칸이 그대로 남았다. 여기서 매 프레임 끝에 다시 놓아 늘 촘촘하게.
///  · 공개 상태: 글자 색(분홍/노랑/회색) 그대로 옅은 알약 + 점
///  · 팔로잉·팔로워: "12\n팔로잉" → 숫자는 크고 굵게, 이름은 작고 옅게
///  · 보물찾기 펼침처럼 길어지는 칸은 카드가 화면(위 안전 영역 ~ 도크 위)을 넘지 않게 그 칸만 줄인다 — 그 안은 스크롤
/// </summary>
public class R0926ProfileLayout : MonoBehaviour
{
    [Serializable]
    public class Item
    {
        public RectTransform rt;
        [Tooltip("이 오브젝트가 꺼져 있으면 건너뛴다 (비우면 rt)")]
        public GameObject activeIf;
        public float height;
        [Tooltip("0 이하면 지금 폭 그대로")]
        public float width;
        public float gapBefore;
        [Tooltip("같은 가운데에 함께 놓을 것 (예: 사진 위 테두리)")]
        public RectTransform companion;
        [Tooltip("켜져 있으면 오른쪽에 나란히 — 두 칸이 폭을 반씩 (예: '팔로우 중 | 메세지 보내기')")]
        public RectTransform pair;
        [NonSerialized] public float extra;   // 실행 중 더한 높이 (SetExtra)
    }

    [SerializeField] private RectTransform card;
    [SerializeField] private RectTransform[] stretchParents;   // TopPanel·BottomPanel — 카드와 같은 크기로
    [SerializeField] private Item[] items;
    [SerializeField] private float padTop = 80f;
    [SerializeField] private float padBottom = 74f;
    [SerializeField] private float minHeight = 900f;
    [SerializeField] private float pairGap = 24f;

    [Header("공개 상태 알약")]
    [SerializeField] private Text visText;
    [SerializeField] private Image visPill;
    [SerializeField] private Image visDot;

    [Header("팔로잉 · 팔로워")]
    [SerializeField] private Text[] countTexts;
    [SerializeField] private int countSize = 88;
    [SerializeField] private int labelSize = 46;

    [Header("화면에 맞추기 — 카드가 화면보다 길면 이 항목만 줄인다 (그 안은 스스로 스크롤)")]
    [SerializeField] private RectTransform shrinkItem;
    [Tooltip("이 위로만 — 도크 자리 (비우면 화면 아래 끝)")]
    [SerializeField] private RectTransform floor;
    [Tooltip("카드 가운데 자리 — 부모 가운데에서 위로")]
    [SerializeField] private float homeY = 90f;
    [SerializeField] private float margin = 40f;
    [SerializeField] private float minShrink = 360f;

    private Canvas rootCanvas;
    private static readonly Vector3[] corners = new Vector3[4];

    /// <summary>항목 높이에 실행 중 더한다 (0 이면 원래대로) — 숫자 줄에 '주최' 칸이 붙을 때 · 보물찾기 펼침 칸</summary>
    public void SetExtra(RectTransform rt, float extra)
    {
        if (items == null) return;
        foreach (var it in items) if (it != null && it.rt == rt) it.extra = extra;
    }

    private void LateUpdate()
    {
        if (card == null || items == null) return;
        if (stretchParents != null)
            foreach (var p in stretchParents)
            {
                if (p == null) continue;
                if (p.anchorMin != Vector2.zero || p.anchorMax != Vector2.one || p.offsetMin != Vector2.zero || p.offsetMax != Vector2.zero)
                {
                    p.anchorMin = Vector2.zero; p.anchorMax = Vector2.one;
                    p.offsetMin = Vector2.zero; p.offsetMax = Vector2.zero;
                }
            }

        StyleVisibility();
        StyleCounts();

        // 화면보다 길면 줄일 항목(보물찾기 펼침)에서 넘치는 만큼 덜어 낸다
        float cut = 0f;
        float room = Room();
        if (room > 0f)
        {
            float over = Stack(false, 0f) + padBottom - room;
            foreach (var it in items)
                if (over > 0f && it != null && it.rt == shrinkItem && Visible(it))
                    cut = Mathf.Min(over, Mathf.Max(0f, it.height + it.extra - minShrink));
        }
        float h = Mathf.Max(minHeight, Stack(true, cut) + padBottom);
        if (!Mathf.Approximately(card.sizeDelta.y, h)) card.sizeDelta = new Vector2(card.sizeDelta.x, h);
    }

    // 위에서부터 쌓는다 — 카드 위 끝이 0, 아래로 음수. 쌓은 높이를 돌려준다
    private float Stack(bool place, float cut)
    {
        float y = padTop;
        bool first = true;
        foreach (var it in items)
        {
            if (!Visible(it)) continue;
            if (!first) y += it.gapBefore;
            first = false;
            float ih = it.height + it.extra - (it.rt == shrinkItem ? cut : 0f);
            if (place)
            {
                float w = it.width > 0f ? it.width : it.rt.sizeDelta.x;
                if (it.pair != null && it.pair.gameObject.activeSelf)
                {
                    float half = (w - pairGap) / 2f;
                    Place(it.rt, y, half, ih, -(half + pairGap) / 2f);
                    Place(it.pair, y, half, ih, (half + pairGap) / 2f);
                }
                else Place(it.rt, y, w, ih);
                if (it.companion != null)
                {
                    // 같은 가운데에 (크기는 그대로)
                    var c = it.companion;
                    c.anchorMin = c.anchorMax = new Vector2(0.5f, 1f);
                    c.pivot = new Vector2(0.5f, 0.5f);
                    c.anchoredPosition = new Vector2(0f, -(y + ih / 2f));
                }
            }
            y += ih;
        }
        return y;
    }

    private static bool Visible(Item it)
    {
        if (it == null || it.rt == null) return false;
        // 부모(TopPanel·BottomPanel)는 늘 켜져 있다 — 자기 자신이 켜졌는지만 본다
        var gate = it.activeIf != null ? it.activeIf : it.rt.gameObject;
        if (!gate.activeSelf) return false;
        // 비어 있는 글(소개 없는 계정)은 자리를 접는다 — 이름과 숫자 사이가 휑하게 비었다
        var tx = it.rt.GetComponent<Text>();
        return tx == null || !string.IsNullOrWhiteSpace(tx.text);
    }

    // 카드가 쓸 수 있는 높이 — 가운데 자리(homeY)는 그대로, 위(안전 영역 아래)와 아래(도크 위) 중 가까운 쪽까지 같은 만큼.
    // 끌어 내려 닫는 동안 카드가 움직여도 바뀌지 않게 카드 위치는 보지 않는다
    private float Room()
    {
        var parent = card.parent as RectTransform;
        if (shrinkItem == null || parent == null) return 0f;
        if (rootCanvas == null) { var c = GetComponentInParent<Canvas>(); rootCanvas = c != null ? c.rootCanvas : null; }
        float scale = rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;
        Rect r = parent.rect, safe = Screen.safeArea;
        float top = r.yMax - (Screen.height - safe.yMax) / scale - margin;
        float bottom = r.yMin + safe.yMin / scale + margin;
        if (floor != null && floor.gameObject.activeInHierarchy)
        {
            floor.GetWorldCorners(corners);
            bottom = Mathf.Max(bottom, parent.InverseTransformPoint(corners[1]).y + margin);
        }
        float center = r.center.y + homeY;
        return 2f * Mathf.Min(top - center, center - bottom);
    }

    private static void Place(RectTransform rt, float top, float w, float h, float x = 0f)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -top);
        rt.sizeDelta = new Vector2(w, h);
    }

    private void StyleVisibility()
    {
        if (visText == null || visPill == null) return;
        // 글자는 알약의 자식 — 알약을 꺼도 글자 자신의 켜짐(내 프로필일 때만)은 따로 본다
        bool on = visText.gameObject.activeSelf && !string.IsNullOrEmpty(visText.text);
        if (visPill.gameObject.activeSelf != on) visPill.gameObject.SetActive(on);
        if (!on) return;
        Color c = visText.color;
        float tw = visText.preferredWidth;
        float dot = visDot != null ? visDot.rectTransform.sizeDelta.x : 0f;
        float pad = 36f, gap = dot > 0f ? 16f : 0f;
        float w = pad + dot + gap + tw + pad;
        visPill.rectTransform.sizeDelta = new Vector2(w, visPill.rectTransform.sizeDelta.y);
        visPill.color = new Color(c.r, c.g, c.b, 0.13f);
        // 글자는 알약 안 (알약과 같은 가운데에서 점만큼 오른쪽)
        var trt = visText.rectTransform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2((dot + gap) / 2f, 0f);
        trt.sizeDelta = new Vector2(tw + 4f, visPill.rectTransform.sizeDelta.y);
        if (visDot != null)
        {
            visDot.color = c;
            var drt = visDot.rectTransform;
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(-w / 2f + pad + dot / 2f, 0f);
        }
    }

    private void StyleCounts()
    {
        if (countTexts == null) return;
        foreach (var t in countTexts)
        {
            if (t == null || string.IsNullOrEmpty(t.text) || t.text.IndexOf('<') >= 0) continue;
            int k = t.text.IndexOf('\n');
            if (k < 0) continue;
            string num = t.text.Substring(0, k).Trim(), label = t.text.Substring(k + 1).Trim();
            t.supportRichText = true;
            t.text = "<size=" + countSize + "><b>" + num + "</b></size>\n<size=" + labelSize + "><color=#8C959D>" + label + "</color></size>";
        }
    }
}
