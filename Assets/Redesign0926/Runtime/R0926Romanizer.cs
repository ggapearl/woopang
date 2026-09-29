using System.Text;

/// <summary>
/// 한글 → 로마자 (국어의 로마자 표기법 기본 규칙). 외국어 사용자가 한글 장소 이름을 읽을 수 있게 병기한다.
/// 번역이 아니라 '읽는 법'이라 무료·오프라인이고 틀려도 뜻을 바꾸지 않는다.
/// 받침 뒤 'ㅇ' 연음, 'ㄹㄹ→ll' 만 반영한 단순판 — 간판 읽기에는 충분하다.
/// </summary>
public static class R0926Romanizer
{
    private static readonly string[] Initial = { "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h" };
    private static readonly string[] Medial = { "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i" };
    private static readonly string[] Final = { "", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m", "l", "l", "l", "p", "l", "m", "p", "p", "t", "t", "ng", "t", "t", "k", "t", "p", "t" };
    // 받침이 다음 'ㅇ' 으로 넘어갈 때의 소리 (연음)
    private static readonly string[] FinalLinked = { "", "g", "kk", "ks", "n", "nj", "nh", "d", "r", "lg", "lm", "lb", "ls", "lt", "lp", "lh", "m", "b", "bs", "s", "ss", "ng", "j", "ch", "k", "t", "p", "h" };

    public static bool HasHangul(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s) if (c >= 0xAC00 && c <= 0xD7A3) return true;
        return false;
    }

    /// <summary>한글이 없으면 null.</summary>
    public static string Romanize(string s)
    {
        if (!HasHangul(s)) return null;
        var sb = new StringBuilder(s.Length * 3);
        bool wordStart = true;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c < 0xAC00 || c > 0xD7A3)
            {
                sb.Append(c);
                wordStart = c == ' ' || c == '-' || c == '(' || c == '·';
                continue;
            }
            int code = c - 0xAC00;
            int ini = code / 588, med = (code % 588) / 28, fin = code % 28;

            string part = Initial[ini] + Medial[med];
            bool nextIsSilent = false, nextIsR = false;
            if (i + 1 < s.Length && s[i + 1] >= 0xAC00 && s[i + 1] <= 0xD7A3)
            {
                int nIni = (s[i + 1] - 0xAC00) / 588;
                nextIsSilent = nIni == 11;
                nextIsR = nIni == 5;
            }
            if (fin > 0)
            {
                if (nextIsSilent) part += FinalLinked[fin];
                else if ((fin == 8 || fin == 4) && nextIsR) part += "l";   // ㄹ/ㄴ+ㄹ → ll (한라 Halla, 신라 Silla)
                else part += Final[fin];
            }
            if (i > 0 && (ini == 5 || ini == 2) && EndsWithL(sb)) part = "l" + part.Substring(1);   // ㄹ+ㄴ → ll (설날 Seollal)

            if (wordStart && part.Length > 0) part = char.ToUpper(part[0]) + part.Substring(1);
            sb.Append(part);
            wordStart = false;
        }
        return sb.ToString();
    }

    private static bool EndsWithL(StringBuilder sb) => sb.Length > 0 && sb[sb.Length - 1] == 'l';
}
