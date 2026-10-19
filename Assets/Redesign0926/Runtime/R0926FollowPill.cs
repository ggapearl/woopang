using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메세지 검색 줄 오른쪽 끝 팔로우 알약 (2026-10-18 대표님: 대화가 없는 사람도 아이디로 찾아 바로 팔로우).
/// 팔로우·맞팔로우 = 흰 알약 · 팔로잉 = 옅은 알약 — 팔로우 목록 줄(FollowListItem)과 같은 모양.
/// 알약(FollowPill0926)은 적용 단계가 대화 줄 프리팹에 꺼 둔 채 만들고, 검색 결과에서만 켠다.
/// </summary>
public static class R0926FollowPill
{
    public const string Name = "FollowPill0926";
    private static readonly Color Ink = new Color(0.953f, 0.961f, 0.965f, 1f);
    private static readonly Color Dark = new Color(0.082f, 0.094f, 0.106f, 1f);
    private static readonly Color Secondary = new Color(1f, 1f, 1f, 0.12f);

    public static void Paint(Transform pill, bool following, bool followsMe)
    {
        if (pill == null) return;
        pill.gameObject.SetActive(true);
        var img = pill.GetComponent<Image>();
        if (img != null) img.color = following ? Secondary : Ink;
        var t = pill.GetComponentInChildren<Text>(true);
        if (t == null) return;
        t.color = following ? Ink : Dark;
        t.text = Word(following ? 2 : followsMe ? 1 : 0);
    }

    // 0 팔로우 · 1 맞팔로우 · 2 팔로잉
    private static string Word(int k)
    {
        switch (AppLanguage.Code)
        {
            case "ko": return new[] { "팔로우", "맞팔로우", "팔로잉" }[k];
            case "ja": return new[] { "フォロー", "フォローバック", "フォロー中" }[k];
            case "zh": return new[] { "关注", "回关", "已关注" }[k];
            case "es": return new[] { "Seguir", "Seguir de vuelta", "Siguiendo" }[k];
            default: return new[] { "Follow", "Follow Back", "Following" }[k];
        }
    }
}
