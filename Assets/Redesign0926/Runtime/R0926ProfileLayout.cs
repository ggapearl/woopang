using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프로필 카드 배치 (0930 시안) — 보이는 항목만 위에서부터 차례로 쌓고 카드 높이를 내용에 맞춘다.
/// 내 프로필/남의 프로필에 따라 ProfileManager 가 버튼·SNS 줄을 켜고 끄고 위치도 옮기는데,
/// 예전엔 좌표가 박혀 있어 빈칸이 그대로 남았다. 여기서 매 프레임 끝에 다시 놓아 늘 촘촘하게.
///  · 공개 상태: 글자 색(분홍/노랑/회색) 그대로 옅은 알약 + 점
///  · 팔로잉·팔로워: "12\n팔로잉" → 숫자는 크고 굵게, 이름은 작고 옅게
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
    }

    [SerializeField] private RectTransform card;
    [SerializeField] private RectTransform[] stretchParents;   // TopPanel·BottomPanel — 카드와 같은 크기로
    [SerializeField] private Item[] items;
    [SerializeField] private float padTop = 80f;
    [SerializeField] private float padBottom = 74f;
    [SerializeField] private float minHeight = 900f;

    [Header("공개 상태 알약")]
    [SerializeField] private Text visText;
    [SerializeField] private Image visPill;
    [SerializeField] private Image visDot;

    [Header("팔로잉 · 팔로워")]
    [SerializeField] private Text[] countTexts;
    [SerializeField] private int countSize = 88;
    [SerializeField] private int labelSize = 46;

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

        // 위에서부터 쌓는다 — 카드 위 끝이 0, 아래로 음수
        float y = padTop;
        bool first = true;
        foreach (var it in items)
        {
            if (it == null || it.rt == null) continue;
            // 부모(TopPanel·BottomPanel)는 늘 켜져 있다 — 자기 자신이 켜졌는지만 본다
            var gate = it.activeIf != null ? it.activeIf : it.rt.gameObject;
            if (!gate.activeSelf) continue;
            // 비어 있는 글(소개 없는 계정)은 자리를 접는다 — 이름과 숫자 사이가 휑하게 비었다
            var tx = it.rt.GetComponent<Text>();
            if (tx != null && string.IsNullOrWhiteSpace(tx.text)) continue;
            if (!first) y += it.gapBefore;
            first = false;
            float w = it.width > 0f ? it.width : it.rt.sizeDelta.x;
            Place(it.rt, y, w, it.height);
            if (it.companion != null)
            {
                // 같은 가운데에 (크기는 그대로)
                var c = it.companion;
                c.anchorMin = c.anchorMax = new Vector2(0.5f, 1f);
                c.pivot = new Vector2(0.5f, 0.5f);
                c.anchoredPosition = new Vector2(0f, -(y + it.height / 2f));
            }
            y += it.height;
        }
        float h = Mathf.Max(minHeight, y + padBottom);
        if (!Mathf.Approximately(card.sizeDelta.y, h)) card.sizeDelta = new Vector2(card.sizeDelta.x, h);
    }

    private static void Place(RectTransform rt, float top, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
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
