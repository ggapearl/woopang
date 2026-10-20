using System;

/// <summary>
/// 시간 글자 (방금 전 · 5분 전 · 오후 3:05 · 10월 21일) 를 앱 언어(ko/en/ja/zh/es)로.
/// 예전엔 댓글·대화 목록·친구 기기마다 한국어로만 적어, 외국 기기에서도 「5분 전」「오후 3:05」가 떴다 (10-21).
/// </summary>
public static class AppTimeText
{
    private static string Say(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

    public static string JustNow => Say("방금 전", "Just now", "たった今", "刚刚", "Ahora");
    public static string Yesterday => Say("어제", "Yesterday", "昨日", "昨天", "Ayer");

    /// <summary>
    /// 지난 시간. dateAfterDays 일이 넘으면 「10월 21일」 처럼 날짜로 — 0 이면 끝까지 (주 · 달 · 년 전).
    /// now 와 date 는 같은 기준(둘 다 현지 또는 둘 다 UTC)이어야 한다.
    /// </summary>
    public static string Ago(DateTime date, DateTime now, int dateAfterDays = 0)
    {
        TimeSpan d = now - date;
        if (d.TotalMinutes < 1) return JustNow;
        if (d.TotalMinutes < 60) return Minutes((int)d.TotalMinutes);
        if (d.TotalHours < 24) return Hours((int)d.TotalHours);
        if (dateAfterDays > 0 && d.TotalDays >= dateAfterDays) return MonthDay(date);
        if (d.TotalDays < 7) return Days((int)d.TotalDays);
        if (d.TotalDays < 30) return Weeks((int)(d.TotalDays / 7));
        if (d.TotalDays < 365) return Months((int)(d.TotalDays / 30));
        return Years((int)(d.TotalDays / 365));
    }

    public static string Minutes(int n) => Say($"{n}분 전", n == 1 ? "1 min ago" : $"{n} mins ago", $"{n}分前", $"{n}分钟前", $"hace {n} min");
    public static string Hours(int n) => Say($"{n}시간 전", n == 1 ? "1 hour ago" : $"{n} hours ago", $"{n}時間前", $"{n}小时前", n == 1 ? "hace 1 hora" : $"hace {n} horas");
    public static string Days(int n) => Say($"{n}일 전", n == 1 ? "1 day ago" : $"{n} days ago", $"{n}日前", $"{n}天前", n == 1 ? "hace 1 día" : $"hace {n} días");
    public static string Weeks(int n) => Say($"{n}주 전", n == 1 ? "1 week ago" : $"{n} weeks ago", $"{n}週間前", $"{n}周前", n == 1 ? "hace 1 semana" : $"hace {n} semanas");
    public static string Months(int n) => Say($"{n}달 전", n == 1 ? "1 month ago" : $"{n} months ago", $"{n}か月前", $"{n}个月前", n == 1 ? "hace 1 mes" : $"hace {n} meses");
    public static string Years(int n) => Say($"{n}년 전", n == 1 ? "1 year ago" : $"{n} years ago", $"{n}年前", $"{n}年前", n == 1 ? "hace 1 año" : $"hace {n} años");

    /// <summary>오후 3:05 · 3:05 PM · 午後3:05 · 下午3:05 · 15:05</summary>
    public static string Clock(DateTime t)
    {
        int h12 = t.Hour % 12 == 0 ? 12 : t.Hour % 12;
        bool pm = t.Hour >= 12;
        string hm = $"{h12}:{t.Minute:D2}";
        return Say($"{(pm ? "오후" : "오전")} {hm}", $"{hm} {(pm ? "PM" : "AM")}", $"{(pm ? "午後" : "午前")}{hm}", $"{(pm ? "下午" : "上午")}{hm}", $"{t.Hour:D2}:{t.Minute:D2}");
    }

    private static readonly string[] MonEn = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    private static readonly string[] MonEs = { "", "ene.", "feb.", "mar.", "abr.", "may.", "jun.", "jul.", "ago.", "sep.", "oct.", "nov.", "dic." };

    /// <summary>10월 21일 · Oct 21 · 10月21日 · 21 oct.</summary>
    public static string MonthDay(DateTime t)
        => Say($"{t.Month}월 {t.Day}일", $"{MonEn[t.Month]} {t.Day}", $"{t.Month}月{t.Day}日", $"{t.Month}月{t.Day}日", $"{t.Day} {MonEs[t.Month]}");

    /// <summary>11월 3일 오후 7:41 · Nov 3, 7:41 PM · 11月3日 午後7:41 · 3 nov. 19:41</summary>
    public static string MonthDayClock(DateTime t) => MonthDay(t) + (AppLanguage.Code == "en" ? ", " : " ") + Clock(t);
}
