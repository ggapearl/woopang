using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대화 글 속 웹 주소 — 찾아서 색을 입히고, 그 글자를 누르면 브라우저로 연다.
/// 10-30 대표님 「웹링크의 경우 브라우저로 바로 이동될 수 있도록 정리해줘」.
/// https 주소 · www · 흔한 도메인(woopang.com · naver.me/abc) · 한글 도메인(농민.com) 을 알아본다. 메일 주소는 링크가 아니다.
/// 말풍선을 눌러도 주소 글자 근처가 아니면 아무 일도 없다 (두 번 눌러 좋아요와 같이 쓴다 — MessagePanelManager.SetupMessageBubble)
/// </summary>
public static class R0926ChatLinks
{
    public struct Link
    {
        public int start, length;
        public string url;
    }

    private const string Tail = @"(?::\d{2,5})?(?:[/?#][^\s<>""]*)?";
    private const string Tlds = "com|net|org|kr|io|app|ai|tv|me|gg|ly|be|co|to|do|gl|la";
    private const string TrimEnd = ".,;:!?'\"…。、，！？：；」』）】》〉ㅋㅎㅠㅜ";
    private const float Slop = 44f;   // 누른 자리에서 이만큼(캔버스 단위) 안의 글자만 본다 — 손가락은 글자보다 크다

    private static readonly Regex Pattern = new Regex(
        @"(?<![A-Za-z0-9])https?://(?:[A-Za-z0-9가-힣\-]+\.)+[A-Za-z]{2,}" + Tail
        + @"|(?<![A-Za-z0-9@.\-/])www\.(?:[A-Za-z0-9\-]+\.)+[A-Za-z]{2,}" + Tail
        + @"|(?<![A-Za-z0-9@.\-/])(?:[A-Za-z0-9\-]+\.)+(?:" + Tlds + @")(?![A-Za-z0-9\-@])" + Tail
        + @"|(?<![\w@.\-/])[가-힣]+\.(?:com|net|kr)(?![A-Za-z0-9\-@])" + Tail,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

#if UNITY_EDITOR
    public static Action<string> EditorOpened;   // 플레이 시험 — 브라우저를 열지 않고 주소만 받는다
#endif

    public static List<Link> Find(string text)
    {
        var found = new List<Link>();
        if (string.IsNullOrEmpty(text)) return found;
        foreach (Match m in Pattern.Matches(text))
        {
            int len = Trimmed(m.Value);
            if (len > 0) found.Add(new Link { start = m.Index, length = len, url = Normalize(m.Value.Substring(0, len)) });
        }
        return found;
    }

    // 주소 뒤에 붙은 문장 부호 · 웃음은 뺀다 — "woopang.com/a." · "(woopang.com/a)" · "woopang.com/aㅋㅋ"
    private static int Trimmed(string s)
    {
        int n = s.Length;
        while (n > 0)
        {
            char c = s[n - 1];
            if (TrimEnd.IndexOf(c) >= 0) n--;
            else if ((c == ')' || c == ']') && s.IndexOf(c == ')' ? '(' : '[', 0, n) < 0) n--;
            else break;
        }
        return n;
    }

    public static string Style(string text, List<Link> links, bool mine)
    {
        string open = mine ? "<color=#FFE7A3>" : "<color=#8CCBFF>";
        var sb = new StringBuilder(text);
        for (int i = links.Count - 1; i >= 0; i--)
        {
            sb.Insert(links[i].start + links[i].length, "</color>");
            sb.Insert(links[i].start, open);
        }
        return sb.ToString();
    }

    /// <summary>화면의 이 자리에 있는 주소 — 없으면 null. plain 은 색을 입히기 전 글이다 (색 글자는 자리를 바꾸지 않는다)</summary>
    public static string At(Text label, string plain, List<Link> links, Vector2 screen)
    {
        if (label == null || links == null || links.Count == 0) return null;
        var rt = label.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, null, out var p)) return null;

        var g = new TextGenerator(plain.Length + 1);
        g.PopulateWithErrors(plain, label.GetGenerationSettings(rt.rect.size), label.gameObject);
        var chars = g.characters;
        var lines = g.lines;
        float ppu = label.pixelsPerUnit;

        // 가장 가까운 글자가 주소의 글자일 때만 — 주소 바로 아랫줄 글을 눌렀는데 브라우저가 뜨면 안 된다
        int nearest = -1, l = 0;
        float bestD = Slop;
        for (int i = 0; i < plain.Length && i < chars.Count; i++)
        {
            while (l + 1 < lines.Count && lines[l + 1].startCharIdx <= i) l++;
            if (chars[i].charWidth <= 0f) continue;   // 줄바꿈 · 공지 제목의 <b>
            float left = chars[i].cursorPos.x / ppu, right = left + chars[i].charWidth / ppu;
            float top = lines[l].topY / ppu, bottom = top - lines[l].height / ppu;
            float dx = Mathf.Max(left - p.x, 0f, p.x - right), dy = Mathf.Max(bottom - p.y, 0f, p.y - top);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < bestD) { bestD = d; nearest = i; }
        }
        foreach (var link in links)
            if (nearest >= link.start && nearest < link.start + link.length) return link.url;
        return null;
    }

    public static void Open(string url)
    {
#if UNITY_EDITOR
        if (EditorOpened != null) { EditorOpened(url); return; }
#endif
        Application.OpenURL(url);
    }

    // https:// 를 붙이고, 한글 도메인은 punycode 로 · 한글 경로는 %XX 로 — 안드로이드 브라우저가 그대로는 못 여는 주소가 있다
    public static string Normalize(string raw)
    {
        string url = raw.IndexOf("://", StringComparison.Ordinal) >= 0 ? raw : "https://" + raw;
        int hostAt = url.IndexOf("://", StringComparison.Ordinal) + 3;
        int hostEnd = url.IndexOfAny(new[] { ':', '/', '?', '#' }, hostAt);
        if (hostEnd < 0) hostEnd = url.Length;
        string host = url.Substring(hostAt, hostEnd - hostAt);
        try { host = new IdnMapping().GetAscii(host); }
        catch (ArgumentException) { }
        string rest = Regex.Replace(url.Substring(hostEnd), @"[^\x00-\x7F]+", m => Uri.EscapeDataString(m.Value));
        return url.Substring(0, hostAt) + host + rest;
    }
}
