using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대화방 제목 — 누구든 같은 흰 글. 우팡 공식 방은 이름 옆에 분홍 체크 배지 (예전엔 제목을 노랗게 칠했다).
/// 배지(Badge0926)는 적용 단계가 ChatTitle 밑에 만든다.
/// </summary>
public static class R0926ChatTitle
{
    private static readonly Color Ink = new Color(0.953f, 0.961f, 0.965f, 1f);

    public static void Paint(Text title, bool official)
    {
        if (title == null) return;
        title.color = Ink;
        var badge = title.transform.Find("Badge0926") as RectTransform;
        if (badge == null) return;
        badge.gameObject.SetActive(official);
        if (!official) return;
        // 제목은 가운데 정렬 — 글 오른쪽 끝 바로 옆에
        float w = Mathf.Min(title.preferredWidth, title.rectTransform.rect.width);
        badge.anchoredPosition = new Vector2(w / 2f + 18f + badge.sizeDelta.x / 2f, badge.anchoredPosition.y);
    }
}
