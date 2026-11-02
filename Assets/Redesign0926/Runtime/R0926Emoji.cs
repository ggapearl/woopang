using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 우팡 이모지 (N-U · 11-02 대표님 「너무 뻔한 거 말고 … 이모지 사이즈 너무 작지 않게」) — Microsoft Fluent Emoji 3D(MIT) 1,595개.
/// 글 속에서는 사용자 영역 글자 하나(U+E000+i)가 이모지 i 다. 본문 글꼴의 대체 글꼴 WoopangEmoji 가 그 글자에 1.18em 빈 자리를 주고
/// R0926EmojiText 가 그 자리에 그림을 얹는다 — 예전 방식 Text 는 컬러 이모지를 못 그려 빈칸·네모로 나왔다.
/// 서버·다른 사람에게는 늘 진짜 이모지가 간다: 보낼 때 ToReal 로 되돌린다.
/// 자료 Resources/Emoji — emoji_index.txt(줄 i = 이모지 i 의 코드포인트 ; 분류 1~7) · emoji_s.png(글 속) · emoji_p0~6.png(키패드 — 열 때만)
/// </summary>
public static class R0926Emoji
{
    public const char Base = '';
    public const int PageSize = 256, PageCols = 16;
    public const int Categories = 7;   // 스마일리 및 사람 · 동물 및 자연 · 음식 및 음료 · 활동 · 여행 및 장소 · 사물 · 기호
    public const int RecentMax = 21;   // 키패드 세 줄

    private const int SmallCols = 64, SmallRows = 32;
    private const string RecentKey = "r0926_emoji_recent";
    private static readonly string[] RecentSeed =
        { "1f602", "2764-fe0f", "1f60d", "1f44d", "1f62d", "1f64f", "1f60a", "1f525", "1f970", "1f606", "2728", "1f389" };

    private class Node
    {
        public Dictionary<int, Node> next;
        public int emoji = -1;
        public bool needsVS;   // 글자로도 쓰는 BMP 한 글자(❤ ☺ © ✔) — 뒤에 이모지 모양(FE0F)이 붙었을 때만 이모지
    }

    private static bool loaded;
    private static string[] real = new string[0];
    private static string[] keys = new string[0];   // "1f602" · "2764-fe0f" — 최근 쓴 것을 저장하는 이름 (그림을 다시 만들어도 그대로)
    private static byte[] cats = new byte[0];
    private static Node root = new Node();
    private static readonly Dictionary<string, int> byKey = new Dictionary<string, int>();
    private static Texture2D small;

    public static int Count { get { Load(); return real.Length; } }
    public static int PageCount => (Count + PageSize - 1) / PageSize;
    public static char Pua(int i) => (char)(Base + i);
    public static int IndexOf(char c) { Load(); int i = c - Base; return i >= 0 && i < real.Length ? i : -1; }
    public static int CategoryOf(int i) { Load(); return i >= 0 && i < cats.Length ? cats[i] : 0; }
    public static string RealOf(int i) { Load(); return i >= 0 && i < real.Length ? real[i] : ""; }
    /// <summary>이름("1f602" · "2764-fe0f")으로 찾는다 — 없으면 -1</summary>
    public static int Find(string key) { Load(); return key != null && byKey.TryGetValue(key, out int i) ? i : -1; }

    public static bool HasPua(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        Load();
        int end = Base + real.Length;
        foreach (char c in s)
            if (c >= Base && c < end) return true;
        return false;
    }

    /// <summary>글 속에 쓰는 그림 한 장 (64칸 · 밉맵)</summary>
    public static Texture2D Small
    {
        get
        {
            if (small == null) small = Resources.Load<Texture2D>("Emoji/emoji_s");
            return small;
        }
    }

    public static string PagePath(int page) => "Emoji/emoji_p" + page;

    public static Rect SmallUV(int i) => new Rect(
        (i % SmallCols) / (float)SmallCols, 1f - (i / SmallCols + 1) / (float)SmallRows, 1f / SmallCols, 1f / SmallRows);

    public static Rect PageUV(int i)
    {
        int k = i % PageSize;
        return new Rect((k % PageCols) / (float)PageCols, 1f - (k / PageCols + 1) / (float)PageCols, 1f / PageCols, 1f / PageCols);
    }

    /// <summary>분류 cat(1~7)의 이모지 — 목록이 분류 순서로 놓여 있어 한 구간이다</summary>
    public static void Range(int cat, out int start, out int count)
    {
        Load();
        start = count = 0;
        for (int i = 0; i < cats.Length; i++)
        {
            if (cats[i] != cat) continue;
            if (count == 0) start = i;
            count++;
        }
    }

    // ── 글 바꾸기 ───────────────────────────────────────────

    /// <summary>보이는 글 — 아는 이모지는 우팡 이모지 글자로, 모르는 이모지 조각은 지운다(네모로 나온다). 바뀐 게 없으면 같은 글을 돌려준다</summary>
    public static string ToDisplay(string s) => Convert(s, true);

    /// <summary>입력칸 글 — 아는 이모지만 바꾸고 모르는 것은 그대로 둔다 (보낼 때 되돌릴 수 없으니 지우지 않는다)</summary>
    public static string ToInput(string s) => Convert(s, false);

    /// <summary>보낼 글 — 우팡 이모지 글자를 진짜 이모지로</summary>
    public static string ToReal(string s)
    {
        if (!HasPua(s)) return s;
        var sb = new StringBuilder(s.Length + 16);
        foreach (char c in s)
        {
            int i = IndexOf(c);
            if (i >= 0) sb.Append(real[i]);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static string Convert(string s, bool strip)
    {
        if (string.IsNullOrEmpty(s)) return s;
        Load();
        StringBuilder sb = null;
        int i = 0, n = s.Length;
        while (i < n)
        {
            char c = s[i];
            if (c >= 0x23 && root.next != null)   // 공백·제어 글자는 이모지가 아니다 (# 부터 — 키캡)
            {
                int e = Match(s, i, out int len);
                if (e >= 0)
                {
                    if (sb == null) sb = new StringBuilder(s, 0, i, n);
                    sb.Append(Pua(e));
                    i += len;
                    continue;
                }
                if (strip)
                {
                    int cp = CodePoint(s, i, out int w);
                    if (Leftover(cp))
                    {
                        if (sb == null) sb = new StringBuilder(s, 0, i, n);
                        i += w;
                        continue;
                    }
                }
            }
            sb?.Append(c);
            i++;
        }
        return sb != null ? sb.ToString() : s;
    }

    // s[i] 에서 시작하는 가장 긴 이모지 — 피부색은 기본 하나뿐이라 건너뛴다
    private static int Match(string s, int i, out int len)
    {
        len = 0;
        int best = -1, at = i, n = s.Length;
        var node = root;
        while (at < n && node.next != null)
        {
            int cp = CodePoint(s, at, out int w);
            if (!node.next.TryGetValue(cp, out var child)) break;
            node = child;
            at += w;
            bool vs = false;
            while (at < n)
            {
                int m = CodePoint(s, at, out int mw);
                if (m == 0xFE0F) vs = true;
                else if (m != 0xFE0E && (m < 0x1F3FB || m > 0x1F3FF)) break;
                at += mw;
            }
            if (node.emoji >= 0 && (!node.needsVS || vs))
            {
                best = node.emoji;
                len = at - i;
            }
        }
        return best;
    }

    // 짝을 못 찾은 이모지 조각 — 모양 고르기 · 잇기(ZWJ) · 키캡 · 그림 문자 · 깃발 꼬리표
    private static bool Leftover(int cp) =>
        cp == 0xFE0F || cp == 0xFE0E || cp == 0x200D || cp == 0x20E3
        || (cp >= 0x1F000 && cp <= 0x1FAFF) || (cp >= 0xE0000 && cp <= 0xE007F);

    private static int CodePoint(string s, int i, out int width)
    {
        char c = s[i];
        if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
        {
            width = 2;
            return char.ConvertToUtf32(c, s[i + 1]);
        }
        width = 1;
        return c;
    }

    // ── 최근 쓴 것 ───────────────────────────────────────────

    public static List<int> Recents()
    {
        Load();
        var list = new List<int>(RecentMax);
        string saved = PlayerPrefs.GetString(RecentKey, "");
        var names = saved.Length > 0 ? saved.Split(',') : RecentSeed;
        foreach (var name in names)
        {
            if (!byKey.TryGetValue(name, out int i) || list.Contains(i)) continue;
            list.Add(i);
            if (list.Count >= RecentMax) break;
        }
        return list;
    }

    public static void Used(int i)
    {
        if (i < 0 || i >= Count) return;
        var list = Recents();
        list.Remove(i);
        list.Insert(0, i);
        if (list.Count > RecentMax) list.RemoveRange(RecentMax, list.Count - RecentMax);
        var sb = new StringBuilder();
        foreach (int k in list)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(keys[k]);
        }
        PlayerPrefs.SetString(RecentKey, sb.ToString());
    }

    // ── 읽기 ───────────────────────────────────────────────

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        var ta = Resources.Load<TextAsset>("Emoji/emoji_index");
        if (ta == null) { Debug.LogError("[R0926Emoji] Resources/Emoji/emoji_index 없음"); return; }
        var r = new List<string>(1600);
        var k = new List<string>(1600);
        var c = new List<byte>(1600);
        var cps = new List<int>(8);
        foreach (var raw in ta.text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int semi = line.IndexOf(';');
            if (semi <= 0) continue;
            cps.Clear();
            foreach (var h in line.Substring(0, semi).Split(' '))
                if (h.Length > 0) cps.Add(System.Convert.ToInt32(h, 16));
            var sb = new StringBuilder(cps.Count * 2);
            foreach (int cp in cps) sb.Append(char.ConvertFromUtf32(cp));
            Insert(cps, r.Count);
            r.Add(sb.ToString());
            k.Add(line.Substring(0, semi).Replace(' ', '-'));
            byte.TryParse(line.Substring(semi + 1), out byte cat);
            c.Add(cat);
        }
        real = r.ToArray();
        keys = k.ToArray();
        cats = c.ToArray();
        for (int i = 0; i < keys.Length; i++) byKey[keys[i]] = i;
        Resources.UnloadAsset(ta);
    }

    // 열쇠는 이모지 모양(FE0F)을 뺀 코드포인트 — 모양 글자가 있든 없든 같은 이모지로 찾는다
    private static void Insert(List<int> cps, int index)
    {
        var node = root;
        foreach (int cp in cps)
        {
            if (cp == 0xFE0F) continue;
            if (node.next == null) node.next = new Dictionary<int, Node>();
            if (!node.next.TryGetValue(cp, out var child)) node.next[cp] = child = new Node();
            node = child;
        }
        if (node.emoji >= 0) return;
        node.emoji = index;
        node.needsVS = cps.Count == 2 && cps[1] == 0xFE0F && cps[0] < 0x10000;
    }
}
