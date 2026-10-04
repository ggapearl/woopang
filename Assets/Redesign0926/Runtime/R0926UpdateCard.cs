using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 업데이트 안내 — 프로필 카드와 같은 모양 (강아지 마크 · 제목 · 한 줄 · 버전 알약 · 버튼).
/// 띄울지(스토어에 실제로 올라왔을 때만)·강제인지·스토어로 보내는 것은 AutoUpdateChecker, 이 카드는 글과 모양만 맡는다.
/// 꺼진 줄(강제일 때의 버튼 등)은 건너뛰고 위에서부터 쌓아 카드 높이를 맞춘다.
/// </summary>
public class R0926UpdateCard : MonoBehaviour
{
    [SerializeField] private RectTransform card;
    [Tooltip("위에서부터 쌓을 줄 — 꺼진 것은 건너뛴다")]
    [SerializeField] private RectTransform[] stack;
    [Tooltip("각 줄 위의 간격 (stack 과 같은 순서)")]
    [SerializeField] private float[] gaps;
    [SerializeField] private float padTop = 84f;
    [SerializeField] private float padBottom = 56f;
    [SerializeField] private Text title;
    [SerializeField] private Text body;
    [SerializeField] private Text version;
    [SerializeField] private RectTransform versionPill;
    [SerializeField] private GameObject countdown;
    [SerializeField] private RectTransform countdownFill;

    public void ShowNormal(string current, string latest)
    {
        Fill(false, current, latest);
        if (countdown != null) countdown.SetActive(false);
    }

    /// <param name="remain01">스토어로 넘어가기까지 남은 비율 (1 → 0)</param>
    public void ShowForce(string current, string latest, float remain01)
    {
        Fill(true, current, latest);
        if (countdown != null) countdown.SetActive(true);
        if (countdownFill != null) countdownFill.anchorMax = new Vector2(Mathf.Clamp01(remain01), 1f);
    }

    private void Fill(bool force, string current, string latest)
    {
        if (title != null)
            title.text = force
                ? L("업데이트가 필요해요", "Update required", "アップデートが必要です", "需要更新", "Actualización necesaria")
                : L("새 버전이 나왔어요", "A new version is here", "新しいバージョンが出ました", "新版本已推出", "Hay una nueva versión");
        if (body != null)
            body.text = force
                ? L("더 좋아진 우팡으로 바꿔 주세요\n잠시 후 스토어로 이동해요", "Please update to the latest WOOPANG\nTaking you to the store…",
                    "新しいWOOPANGに更新してください\nまもなくストアへ移動します", "请更新到最新版 WOOPANG\n即将前往商店", "Actualiza al nuevo WOOPANG\nTe llevamos a la tienda…")
                : L("더 편해진 우팡을 만나 보세요", "Update to get the latest WOOPANG", "もっと便利になったWOOPANGをどうぞ", "体验更好用的 WOOPANG", "Descubre el WOOPANG mejorado");
        // 새 버전 이름을 모르면(안드로이드 Play 는 주지 않는다) 버전 알약을 빼고 글만 — 꺼진 줄은 쌓기에서 건너뛴다
        bool known = !string.IsNullOrEmpty(latest);
        var pill = versionPill != null ? versionPill.gameObject : version != null ? version.gameObject : null;
        if (pill != null && pill.activeSelf != known) pill.SetActive(known);
        if (known && version != null)
        {
            string v = (string.IsNullOrEmpty(current) ? "" : current + "   →   ") + latest;
            if (version.text != v) version.text = v;
            if (versionPill != null) versionPill.sizeDelta = new Vector2(version.preferredWidth + 96f, versionPill.sizeDelta.y);
        }
    }

    private void LateUpdate()
    {
        if (card == null || stack == null) return;
        float y = padTop;
        bool first = true;
        for (int i = 0; i < stack.Length; i++)
        {
            var rt = stack[i];
            if (rt == null || !rt.gameObject.activeSelf) continue;
            if (!first) y += gaps != null && i < gaps.Length ? gaps[i] : 0f;
            first = false;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            y += rt.sizeDelta.y;
        }
        float h = y + padBottom;
        if (!Mathf.Approximately(card.sizeDelta.y, h)) card.sizeDelta = new Vector2(card.sizeDelta.x, h);
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
