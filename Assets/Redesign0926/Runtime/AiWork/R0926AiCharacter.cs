using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// AI 작업 ▸ 3D 캐릭터 생성 — 요청사항 한 줄을 읽어 귀여운 2등신 캐릭터를 코드로 빚고 GLB 로 굽는다 (폰 안에서 · 무료 · 1~2초).
/// 11-12 대표님 「3D캐릭터를 선택한 경우 로컬에서 작업이 가능한 선에서 '프롬프트' 를 직접 기입할 수 있도록 '요청사항' 이 기입되면
/// 해당 내용이 무리하지 않는 선에서 (적당한 텍스쳐의 요청이 반영된 오브젝트 생성/ 로컬에서 빠르게 가능한 선에서) 바로 발생」.
///  · 학습된 3D 모델 없이 — 동물 10종 · 사람 · 로봇 + 색 · 표정 · 모자/왕관/리본/안경/목도리/꽃/가방 · 무늬 · 직업(농부·요리사·경찰…) · 움직임(손 흔들기·춤·점프)
///  · 모양은 매끈한 매개변수 곡면(타원체 · 둥근 상자 · 고리), 그늘은 그림에 굽는다 (앱은 조명 없이 그림 색 그대로 그린다)
///  · 'Idle' 한 동작(2초 반복 — 숨쉬기 · 갸웃 · 꼬리 · 깜빡임)을 넣는다 — GLBModelLoader 가 Idle 이 든 동작을 골라 튼다
///  · 사진(대표 사진)을 주면 오른손에 팻말로 든다 — 장소의 얼굴 (투명한 사진이면 스티커 모양 그대로)
/// 나중에 외부 3D AI 를 붙이면 R0926AiWork 에서 이 자리만 바꾼다. 배경 스레드에서 돈다(Unity 그래픽 API 없음).
/// </summary>
public static class R0926AiCharacter
{
    public enum Kind { Dog, Cat, Rabbit, Bear, Panda, Fox, Pig, Chick, Penguin, Frog, Person, Robot }
    public enum Face { Smile, Happy, Angry, Sad, Sleepy, Surprised, Wink, Calm }
    public enum Move { Idle, Wave, Dance, Jump }
    public enum Hat { None, Beanie, Cap, Straw, Chef, Santa, Helmet }
    public enum Job { None, Farmer, Chef, Police, Firefighter, Doctor, Princess, Prince, Santa }
    public enum Pattern { None, Spots, Stripes }
    [Flags] public enum Extra { None = 0, Crown = 1, Bow = 2, Glasses = 4, Sunglasses = 8, Scarf = 16, Flower = 32, Backpack = 64, Heart = 128, Star = 256, Tie = 512, Buns = 1024 }

    public sealed class Spec
    {
        public Kind Kind;
        public bool KindGiven;
        public Job Job;
        public Color Main, Second, Detail;
        public bool MainGiven;
        public string MainName;
        public Color Skin = new Color(1f, 0.85f, 0.74f), Hair = new Color(0.26f, 0.17f, 0.11f);
        public Face Face = Face.Smile;
        public Move Move = Move.Idle;
        public Hat Hat;
        public Color HatColor;
        public Extra Extras;
        public readonly Dictionary<Extra, Color> ExtraColor = new Dictionary<Extra, Color>();
        public Pattern Pattern;
        public float Size = 1f, Chub = 1f;
        public bool Baby;
        public int Seed;
        public string Label = "";
    }

    // ── 요청사항 읽기 ──────────────────────────────────────
    private struct Hit { public int Start, End; public Color C; public string Name; }

    private static readonly (Kind kind, string[] words)[] KindWords =
    {
        (Kind.Frog, new[] { "개구리", "frog" }),
        (Kind.Panda, new[] { "판다", "팬더", "panda" }),
        (Kind.Penguin, new[] { "펭귄", "penguin" }),
        (Kind.Chick, new[] { "병아리", "닭", "참새", "앵무새", "오리", "짹짹", "chick", "bird", "chicken", "duck" }),
        (Kind.Rabbit, new[] { "토끼", "바니", "rabbit", "bunny" }),
        (Kind.Fox, new[] { "여우", "fox" }),
        (Kind.Pig, new[] { "돼지", "꿀꿀", "pig", "piggy" }),
        (Kind.Cat, new[] { "고양이", "냥이", "야옹", "키티", "cat", "kitty", "kitten" }),
        (Kind.Bear, new[] { "곰", "테디", "bear", "teddy" }),
        (Kind.Dog, new[] { "강아지", "멍멍", "댕댕", "퍼피", "푸들", "시바", "리트리버", "말티즈", "비숑", "코기", "진돗개", "마스티프", "달마시안", "허스키", "치와와", "dog", "puppy", "doggy" }),
        (Kind.Robot, new[] { "로봇", "로보트", "안드로이드", "robot" }),
        (Kind.Person, new[] { "사람", "소년", "소녀", "남자", "여자", "어린이", "학생", "농부", "농민", "요리사", "셰프", "쉐프", "의사", "간호사", "경찰", "소방관",
                              "할머니", "할아버지", "엄마", "아빠", "공주", "왕자", "산타", "person", "boy", "girl", "man", "woman", "kid", "farmer", "chef",
                              "police", "doctor", "nurse", "princess", "prince", "santa" }),
    };

    private static readonly (string[] words, Color c, string name)[] Colors =
    {
        (new[] { "하늘색", "하늘빛", "sky blue", "skyblue" }, new Color(0.53f, 0.80f, 0.97f), "하늘색"),
        (new[] { "남색", "네이비", "navy" }, new Color(0.17f, 0.22f, 0.45f), "남색"),
        (new[] { "연두", "lime" }, new Color(0.62f, 0.86f, 0.32f), "연두색"),
        (new[] { "민트", "mint" }, new Color(0.56f, 0.90f, 0.80f), "민트색"),
        (new[] { "빨간", "빨강", "빨갛", "붉은", "레드", "red" }, new Color(0.92f, 0.22f, 0.22f), "빨간"),
        (new[] { "주황", "오렌지", "orange" }, new Color(1.00f, 0.56f, 0.16f), "주황"),
        (new[] { "노란", "노랑", "노랗", "옐로", "yellow" }, new Color(1.00f, 0.85f, 0.22f), "노란"),
        (new[] { "초록", "녹색", "그린", "green" }, new Color(0.30f, 0.70f, 0.34f), "초록"),
        (new[] { "파란", "파랑", "파랗", "블루", "blue" }, new Color(0.26f, 0.50f, 0.95f), "파란"),
        (new[] { "보라", "퍼플", "자주", "purple", "violet" }, new Color(0.60f, 0.42f, 0.86f), "보라"),
        (new[] { "분홍", "핑크", "pink" }, new Color(1.00f, 0.62f, 0.76f), "분홍"),
        (new[] { "갈색", "브라운", "밤색", "brown" }, new Color(0.58f, 0.38f, 0.22f), "갈색"),
        (new[] { "베이지", "크림", "beige", "cream" }, new Color(0.94f, 0.88f, 0.76f), "베이지"),
        (new[] { "검은", "검정", "까만", "까망", "블랙", "black" }, new Color(0.14f, 0.14f, 0.16f), "검은"),
        (new[] { "흰", "하얀", "하양", "화이트", "white" }, new Color(0.97f, 0.97f, 0.96f), "하얀"),
        (new[] { "회색", "그레이", "잿빛", "gray", "grey" }, new Color(0.62f, 0.62f, 0.64f), "회색"),
        (new[] { "금색", "골드", "황금", "금빛", "gold" }, new Color(1.00f, 0.80f, 0.26f), "금색"),
        (new[] { "은색", "실버", "silver" }, new Color(0.80f, 0.82f, 0.86f), "은색"),
    };

    private static readonly Kind[] Defaults = { Kind.Dog, Kind.Cat, Kind.Bear, Kind.Rabbit, Kind.Panda, Kind.Fox, Kind.Chick, Kind.Penguin, Kind.Pig, Kind.Frog };
    private static readonly string[] KindNames = { "강아지", "고양이", "토끼", "곰", "판다", "여우", "돼지", "병아리", "펭귄", "개구리", "사람", "로봇" };
    private static readonly string[] JobNames = { "", "농부", "요리사", "경찰", "소방관", "의사", "공주", "왕자", "산타" };

    private static List<(int s, int e)> FindAll(string text, string w)
    {
        var res = new List<(int, int)>();
        bool ascii = w[0] < 128;
        int i = 0;
        while ((i = text.IndexOf(w, i, StringComparison.Ordinal)) >= 0)
        {
            int e = i + w.Length;
            bool ok = !ascii || ((i == 0 || !char.IsLetter(text[i - 1])) && (e >= text.Length || !char.IsLetter(text[e])));
            if (ok) res.Add((i, e));
            i = e;
        }
        return res;
    }

    private static bool Any(string text, params string[] words)
    {
        foreach (var w in words) if (FindAll(text, w).Count > 0) return true;
        return false;
    }

    private static int First(string text, params string[] words)
    {
        int best = -1;
        foreach (var w in words)
            foreach (var h in FindAll(text, w))
                if (best < 0 || h.s < best) best = h.s;
        return best;
    }

    public static Spec Parse(string request, int seed)
    {
        string t = (request ?? "").Trim().ToLowerInvariant();
        var s = new Spec { Seed = seed };
        var rnd = new System.Random(seed);

        // 무엇
        foreach (var (kind, words) in KindWords)
            if (Any(t, words)) { s.Kind = kind; s.KindGiven = true; break; }
        if (!s.KindGiven && Regex.IsMatch(t, "(^|[^가-힣])개($|[^가-힣]|[가를는야랑와의도])")) { s.Kind = Kind.Dog; s.KindGiven = true; }
        if (!s.KindGiven && Regex.IsMatch(t, "(^|[^가-힣])새($|[^가-힣]|[가를는야랑와의도])")) { s.Kind = Kind.Chick; s.KindGiven = true; }
        if (!s.KindGiven) s.Kind = Defaults[rnd.Next(Defaults.Length)];

        // 직업 — 옷 · 모자
        if (Any(t, "산타", "크리스마스", "santa", "christmas")) s.Job = Job.Santa;
        else if (Any(t, "농부", "농민", "농사", "farmer")) s.Job = Job.Farmer;
        else if (Any(t, "요리사", "셰프", "쉐프", "chef", "cook")) s.Job = Job.Chef;
        else if (Any(t, "소방관", "소방", "firefighter")) s.Job = Job.Firefighter;
        else if (Any(t, "경찰", "police")) s.Job = Job.Police;
        else if (Any(t, "의사", "간호사", "doctor", "nurse")) s.Job = Job.Doctor;
        else if (Any(t, "공주", "princess")) s.Job = Job.Princess;
        else if (Any(t, "왕자", "임금", "prince", "king")) s.Job = Job.Prince;

        // 색 (나온 자리와 함께)
        var hits = new List<Hit>();
        foreach (var (words, c, name) in Colors)
            foreach (var w in words)
                foreach (var h in FindAll(t, w))
                    if (!hits.Exists(x => x.Start < h.e && h.s < x.End)) hits.Add(new Hit { Start = h.s, End = h.e, C = c, Name = name });
        if (Any(t, "금발", "blonde")) s.Hair = new Color(0.98f, 0.82f, 0.38f);
        hits.Sort((a, b) => a.Start.CompareTo(b.Start));
        var used = new bool[hits.Count];
        // 바로 앞(일곱 글자 안)의 색은 그 소품 · 머리카락 · 무늬의 색
        Func<int, Color?> bind = at =>
        {
            int best = -1;
            for (int i = 0; i < hits.Count; i++)
                if (!used[i] && hits[i].End <= at && at - hits[i].End <= 7 && (best < 0 || hits[i].End > hits[best].End)) best = i;
            if (best < 0) return null;
            used[best] = true;
            return hits[best].C;
        };

        // 모자 — 모양은 낱말(밀짚 · 헬멧 · 캡)이 먼저, 없으면 직업, 그것도 없으면 '모자' 한 마디에 털모자
        int hatAt = First(t, "모자", "비니", "털모자", "캡", "야구모자", "밀짚", "헬멧", "안전모", "hat", "cap", "beanie", "helmet");
        Hat style = Any(t, "밀짚", "straw") ? Hat.Straw
                  : Any(t, "헬멧", "안전모", "helmet") ? Hat.Helmet
                  : Any(t, "캡", "야구모자", "cap") ? Hat.Cap
                  : s.Job == Job.Santa ? Hat.Santa
                  : s.Job == Job.Chef ? Hat.Chef
                  : s.Job == Job.Farmer ? Hat.Straw
                  : s.Job == Job.Police ? Hat.Cap
                  : s.Job == Job.Firefighter ? Hat.Helmet
                  : hatAt >= 0 ? Hat.Beanie : Hat.None;
        if (style != Hat.None)
        {
            s.Hat = style;
            s.HatColor = (hatAt >= 0 ? bind(hatAt) : null) ?? HatDefault(style, s.Job, rnd);
        }
        if (s.Job == Job.Princess || s.Job == Job.Prince) s.Extras |= Extra.Crown;

        // 소품
        void Ex(Extra e, Color def, params string[] words)
        {
            int at = First(t, words);
            if (at < 0) return;
            s.Extras |= e;
            s.ExtraColor[e] = bind(at) ?? def;
        }
        Ex(Extra.Sunglasses, new Color(0.1f, 0.1f, 0.12f), "선글라스", "썬글라스", "sunglasses");
        if ((s.Extras & Extra.Sunglasses) == 0) Ex(Extra.Glasses, new Color(0.16f, 0.13f, 0.12f), "안경", "glasses");
        Ex(Extra.Crown, new Color(1f, 0.8f, 0.26f), "왕관", "crown");
        Ex(Extra.Bow, new Color(1f, 0.42f, 0.58f), "리본", "머리핀", "머리띠", "bow", "ribbon");
        Ex(Extra.Scarf, new Color(0.9f, 0.25f, 0.25f), "목도리", "스카프", "머플러", "scarf", "muffler");
        Ex(Extra.Flower, new Color(1f, 0.55f, 0.7f), "꽃", "flower");
        Ex(Extra.Backpack, new Color(0.95f, 0.6f, 0.2f), "가방", "배낭", "백팩", "backpack", "bag");
        Ex(Extra.Tie, new Color(0.85f, 0.2f, 0.25f), "넥타이", "necktie", "tie");
        Ex(Extra.Heart, new Color(1f, 0.35f, 0.45f), "하트", "사랑", "heart", "love");
        if (Regex.IsMatch(t, "(^|[^가-힣])별") || Any(t, "star")) { s.Extras |= Extra.Star; s.ExtraColor[Extra.Star] = bind(Math.Max(0, First(t, "별", "star"))) ?? new Color(1f, 0.82f, 0.25f); }
        if (s.Extras.HasFlag(Extra.Crown) && !s.ExtraColor.ContainsKey(Extra.Crown)) s.ExtraColor[Extra.Crown] = new Color(1f, 0.8f, 0.26f);

        // 머리카락 색 · 무늬 색
        int hairAt = First(t, "머리카락", "머리", "헤어", "hair");
        if (hairAt >= 0) { var hc = bind(hairAt); if (hc.HasValue) s.Hair = hc.Value; }
        if (Any(t, "점박이", "얼룩", "땡땡이", "물방울무늬", "달마시안", "spot", "spots", "dotted", "dalmatian")) s.Pattern = Pattern.Spots;
        else if (Any(t, "줄무늬", "호랑이무늬", "스트라이프", "stripe", "stripes", "striped")) s.Pattern = Pattern.Stripes;
        if (s.Pattern != Pattern.None)
        {
            var pc = bind(First(t, "점박이", "얼룩", "땡땡이", "무늬", "spot", "stripe", "dalmatian"));
            s.Detail = pc ?? new Color(0.2f, 0.17f, 0.16f);
        }

        // 남은 색 — 처음 것은 몸(털 · 옷), 다음은 배 · 바지
        var free = new List<Hit>();
        for (int i = 0; i < hits.Count; i++) if (!used[i]) free.Add(hits[i]);
        SpeciesColors(s, rnd);
        if (free.Count > 0) { s.Main = free[0].C; s.MainGiven = true; s.MainName = free[0].Name; }
        if (free.Count > 1) s.Second = free[1].C;
        if (Any(t, "달마시안", "dalmatian") && !s.MainGiven) { s.Main = new Color(0.97f, 0.97f, 0.96f); s.Detail = new Color(0.12f, 0.12f, 0.13f); }
        if (Any(t, "할머니", "할아버지", "grandma", "grandpa")) s.Hair = new Color(0.82f, 0.82f, 0.84f);
        if (s.Kind == Kind.Person && Any(t, "소녀", "여자", "공주", "엄마", "할머니", "여학생", "girl", "woman", "princess", "mom")) s.Extras |= Extra.Buns;

        // 표정 · 움직임 · 몸집
        if (Any(t, "윙크", "wink")) s.Face = Face.Wink;
        else if (Any(t, "화난", "화가", "뿔난", "성난", "삐진", "angry", "mad")) s.Face = Face.Angry;
        else if (Any(t, "슬픈", "우는", "눈물", "sad", "cry", "crying")) s.Face = Face.Sad;
        else if (Any(t, "졸린", "자는", "잠자", "꿀잠", "sleepy", "sleeping")) s.Face = Face.Sleepy;
        else if (Any(t, "놀란", "깜짝", "surprised", "shocked")) s.Face = Face.Surprised;
        else if (Any(t, "시크", "무표정", "차분", "cool", "calm")) s.Face = Face.Calm;
        else if (Any(t, "신나", "행복", "기쁜", "기뻐", "활짝", "happy", "excited", "joy")) s.Face = Face.Happy;

        if (Any(t, "춤", "댄스", "dance", "dancing")) s.Move = Move.Dance;
        else if (Any(t, "점프", "뛰는", "뛰어", "폴짝", "깡총", "jump", "jumping", "hop")) s.Move = Move.Jump;
        else if (Any(t, "손 흔", "손흔", "인사", "안녕", "반가", "하이", "wave", "waving", "hello", "hi")) s.Move = Move.Wave;

        if (Any(t, "아기", "꼬마", "새끼", "미니", "baby", "mini", "little", "tiny")) { s.Baby = true; s.Size = 0.88f; }
        if (Any(t, "거대", "커다란", "대왕", "큰", "big", "giant", "huge")) s.Size = 1.25f;
        if (Any(t, "뚱뚱", "통통", "포동", "토실", "뚱", "chubby", "fat")) s.Chub = 1.2f;
        else if (Any(t, "날씬", "마른", "slim", "skinny")) s.Chub = 0.88f;

        Vary(s, rnd);
        s.Label = MakeLabel(s);
        return s;
    }

    // 같은 요청을 '만들기'로 다시 — 요청에 적힌 것(무엇 · 색 · 모자 · 소품 · 표정 · 움직임 · 몸집)은 그대로, 적히지 않은 것만 바꾼다 (11-13)
    // 표정 · 털 무늬(털 있는 동물) · 몸집 · 같은 색 안의 짙기. 씨앗(사진 · 요청 · 차례)이 같으면 늘 같은 모습
    private static readonly Face[] Moods = { Face.Smile, Face.Happy, Face.Wink, Face.Calm };

    private static void Vary(Spec s, System.Random rnd)
    {
        if (s.Face == Face.Smile) s.Face = Moods[rnd.Next(Moods.Length)];
        bool furry = s.Kind == Kind.Dog || s.Kind == Kind.Cat || s.Kind == Kind.Bear || s.Kind == Kind.Fox || s.Kind == Kind.Pig || s.Kind == Kind.Rabbit;
        if (furry && s.Pattern == Pattern.None && s.Job == Job.None && rnd.Next(3) == 0)
        {
            s.Pattern = rnd.Next(2) == 0 ? Pattern.Spots : Pattern.Stripes;
            s.Detail = Color.Lerp(s.Main, new Color(0.2f, 0.17f, 0.16f), 0.55f);
        }
        if (s.Chub == 1f) s.Chub = 0.92f + (float)rnd.NextDouble() * 0.18f;
        if (s.Size == 1f) s.Size = 0.95f + (float)rnd.NextDouble() * 0.1f;
        Color.RGBToHSV(s.Main, out float h, out float sat, out float v);
        s.Main = Color.HSVToRGB(h, Mathf.Clamp01(sat * (0.86f + (float)rnd.NextDouble() * 0.22f)), Mathf.Clamp01(v * (0.9f + (float)rnd.NextDouble() * 0.14f)));
    }

    private static Color HatDefault(Hat h, Job job, System.Random rnd)
    {
        switch (h)
        {
            case Hat.Straw: return new Color(0.95f, 0.82f, 0.52f);
            case Hat.Chef: return Color.white;
            case Hat.Santa: return new Color(0.86f, 0.15f, 0.17f);
            case Hat.Helmet: return job == Job.Firefighter ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.95f, 0.78f, 0.2f);
            case Hat.Cap: if (job == Job.Police) return new Color(0.15f, 0.2f, 0.4f); break;
        }
        var pal = new[] { new Color(0.93f, 0.3f, 0.32f), new Color(0.3f, 0.55f, 0.95f), new Color(1f, 0.78f, 0.25f), new Color(0.45f, 0.78f, 0.45f), new Color(0.62f, 0.45f, 0.9f) };
        return pal[rnd.Next(pal.Length)];
    }

    private static void SpeciesColors(Spec s, System.Random rnd)
    {
        switch (s.Kind)
        {
            case Kind.Dog: s.Main = new Color(0.87f, 0.67f, 0.43f); s.Second = new Color(0.99f, 0.94f, 0.86f); break;
            case Kind.Cat: s.Main = rnd.Next(2) == 0 ? new Color(0.98f, 0.66f, 0.32f) : new Color(0.64f, 0.64f, 0.68f); s.Second = new Color(0.99f, 0.97f, 0.95f); break;
            case Kind.Rabbit: s.Main = new Color(0.98f, 0.97f, 0.96f); s.Second = new Color(1f, 0.76f, 0.82f); break;
            case Kind.Bear: s.Main = new Color(0.62f, 0.42f, 0.26f); s.Second = new Color(0.9f, 0.77f, 0.6f); break;
            case Kind.Panda: s.Main = new Color(0.98f, 0.98f, 0.97f); s.Second = new Color(0.13f, 0.13f, 0.15f); break;
            case Kind.Fox: s.Main = new Color(0.96f, 0.52f, 0.2f); s.Second = new Color(0.99f, 0.97f, 0.94f); break;
            case Kind.Pig: s.Main = new Color(1f, 0.74f, 0.77f); s.Second = new Color(0.97f, 0.57f, 0.64f); break;
            case Kind.Chick: s.Main = new Color(1f, 0.87f, 0.32f); s.Second = new Color(1f, 0.6f, 0.16f); break;
            case Kind.Penguin: s.Main = new Color(0.18f, 0.22f, 0.34f); s.Second = new Color(0.99f, 0.99f, 0.98f); break;
            case Kind.Frog: s.Main = new Color(0.46f, 0.79f, 0.36f); s.Second = new Color(0.87f, 0.95f, 0.62f); break;
            case Kind.Robot: s.Main = new Color(0.8f, 0.82f, 0.87f); s.Second = new Color(0.36f, 0.47f, 0.62f); break;
            case Kind.Person:
                var pal = new[] { new Color(0.95f, 0.45f, 0.45f), new Color(0.38f, 0.62f, 0.95f), new Color(1f, 0.8f, 0.3f), new Color(0.5f, 0.8f, 0.55f), new Color(0.7f, 0.55f, 0.92f) };
                s.Main = pal[rnd.Next(pal.Length)];
                s.Second = new Color(0.3f, 0.4f, 0.62f);
                switch (s.Job)
                {
                    case Job.Santa: s.Main = new Color(0.86f, 0.15f, 0.17f); s.Second = new Color(0.86f, 0.15f, 0.17f); break;
                    case Job.Farmer: s.Main = new Color(0.93f, 0.42f, 0.36f); s.Second = new Color(0.3f, 0.45f, 0.72f); break;
                    case Job.Chef: case Job.Doctor: s.Main = new Color(0.98f, 0.98f, 0.98f); s.Second = new Color(0.35f, 0.38f, 0.45f); break;
                    case Job.Police: s.Main = new Color(0.18f, 0.24f, 0.45f); s.Second = new Color(0.15f, 0.18f, 0.3f); break;
                    case Job.Firefighter: s.Main = new Color(0.96f, 0.66f, 0.18f); s.Second = new Color(0.3f, 0.3f, 0.32f); break;
                    case Job.Princess: s.Main = new Color(1f, 0.66f, 0.8f); s.Second = new Color(1f, 0.66f, 0.8f); break;
                    case Job.Prince: s.Main = new Color(0.3f, 0.45f, 0.9f); s.Second = new Color(0.95f, 0.95f, 0.95f); break;
                }
                break;
        }
    }

    private static string MakeLabel(Spec s)
    {
        var sb = new StringBuilder();
        if (s.MainGiven && s.Kind != Kind.Person) sb.Append(s.MainName).Append(' ');
        if (s.Baby) sb.Append("아기 ");
        sb.Append(s.Job != Job.None && s.Kind == Kind.Person ? JobNames[(int)s.Job] : (s.Job != Job.None ? JobNames[(int)s.Job] + " " + KindNames[(int)s.Kind] : KindNames[(int)s.Kind]));
        var bits = new List<string>();
        switch (s.Hat)
        {
            case Hat.Beanie: case Hat.Cap: case Hat.Helmet: bits.Add("모자"); break;
            case Hat.Straw: if (s.Job != Job.Farmer) bits.Add("밀짚모자"); break;
        }
        if (s.Extras.HasFlag(Extra.Crown) && s.Job != Job.Princess && s.Job != Job.Prince) bits.Add("왕관");
        if (s.Extras.HasFlag(Extra.Bow)) bits.Add("리본");
        if (s.Extras.HasFlag(Extra.Glasses)) bits.Add("안경");
        if (s.Extras.HasFlag(Extra.Sunglasses)) bits.Add("선글라스");
        if (s.Extras.HasFlag(Extra.Scarf)) bits.Add("목도리");
        if (s.Extras.HasFlag(Extra.Flower)) bits.Add("꽃");
        if (s.Extras.HasFlag(Extra.Backpack)) bits.Add("가방");
        if (s.Extras.HasFlag(Extra.Tie)) bits.Add("넥타이");
        if (s.Extras.HasFlag(Extra.Heart)) bits.Add("하트");
        if (s.Extras.HasFlag(Extra.Star)) bits.Add("별");
        if (s.Pattern == Pattern.Spots) bits.Add("점박이");
        if (s.Pattern == Pattern.Stripes) bits.Add("줄무늬");
        switch (s.Move)
        {
            case Move.Wave: bits.Add("손 인사"); break;
            case Move.Dance: bits.Add("춤"); break;
            case Move.Jump: bits.Add("폴짝"); break;
        }
        if (bits.Count > 0) sb.Append(" · ").Append(string.Join(" · ", bits));
        return sb.ToString();
    }

    // ── 모양 ──────────────────────────────────────────────
    private sealed class Part
    {
        public string Name;
        public int Group;
        public bool Torus;
        public Vector3 C;
        public Quaternion Q = Quaternion.identity;
        public Vector3 Rad = Vector3.one * 0.1f;
        public float E1 = 1f, E2 = 1f, Taper, Ring = 0.3f;
        public Vector3 Bend;
        public float V0 = 0f, V1 = 1f;
        public int Nu = 18, Nv = 10;
        public float Density = 1f;
        public Func<Sample, Color> Paint;
        public bool Unshaded, Matte = true;
        public float Sign = 1f;
        public int Rx, Ry, Rw, Rh;
    }

    private struct Sample
    {
        public float U, V, Lon, Lat;
        public Vector3 D, N, P;   // D: 단위 모양 위 방향(칠하기) · N, P: 캐릭터 공간 법선 · 위치
    }

    private sealed class Group
    {
        public int Node;
        public Vector3 Pos;       // 캐릭터 공간 (기본 자세)
        public Quaternion Rot;
        public Vector3 LocalPos;
        public Quaternion LocalRot;
    }

    private static float Sp(float c, float e)
    {
        if (e == 1f) return c;
        return (c < 0f ? -1f : 1f) * Mathf.Pow(Mathf.Abs(c), e);
    }

    private static Vector3 Eval(Part p, float u, float v, out Vector3 d, out float lon, out float lat)
    {
        float vv = p.V0 + (p.V1 - p.V0) * v;
        lon = (u * 2f - 1f) * Mathf.PI;   // 0 = 앞(+Z) · + 는 +X 쪽
        float sLon = Mathf.Sin(lon), cLon = Mathf.Cos(lon);
        Vector3 q;
        if (p.Torus)
        {
            float phi = vv * 2f * Mathf.PI;
            lat = phi;
            float cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
            d = new Vector3(sLon * cp, sp, cLon * cp);
            float rr = 1f + p.Ring * cp;
            q = new Vector3(rr * sLon * p.Rad.x, p.Ring * sp * p.Rad.y, rr * cLon * p.Rad.z);
        }
        else
        {
            lat = (vv * 2f - 1f) * Mathf.PI * 0.5f;
            float a = Sp(Mathf.Cos(lat), p.E1);
            d = new Vector3(a * Sp(sLon, p.E2), Sp(Mathf.Sin(lat), p.E1), a * Sp(cLon, p.E2));
            float m = Mathf.Max(0.03f, 1f + p.Taper * (d.y + 1f) * 0.5f);
            q = new Vector3(d.x * m * p.Rad.x, d.y * p.Rad.y, d.z * m * p.Rad.z);
            float b = (d.y + 1f) * 0.5f;
            q += p.Bend * (b * b);
        }
        return p.C + p.Q * q;
    }

    private static Vector3 Pos(Part p, float u, float v) => Eval(p, u, v, out _, out _, out _);

    private static Vector3 RawNormal(Part p, float u, float v)
    {
        const float e = 0.004f;
        float vc = p.Torus ? v : Mathf.Clamp(v, 0.012f, 0.988f);
        Vector3 du = Pos(p, u + e, vc) - Pos(p, u - e, vc);
        Vector3 dv = Pos(p, u, vc + e) - Pos(p, u, vc - e);
        return Vector3.Cross(dv, du);
    }

    private static Vector3 Normal(Part p, float u, float v)
    {
        Vector3 n = RawNormal(p, u, v) * p.Sign;
        return n.sqrMagnitude > 1e-24f ? n.normalized : p.Q * Vector3.up;
    }

    private static void Orient(Part p)
    {
        Eval(p, 0.5f, 0.5f, out Vector3 d, out _, out _);
        p.Sign = Vector3.Dot(RawNormal(p, 0.5f, 0.5f), p.Q * d) < 0f ? -1f : 1f;
    }

    // ── 칠하기 도구 ────────────────────────────────────────
    private static readonly Vector3 Light = new Vector3(0.35f, 0.8f, 0.5f).normalized;
    private static readonly Vector3 Half = (new Vector3(0.35f, 0.8f, 0.5f).normalized + Vector3.forward).normalized;

    private static float SmoothStep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    /// <summary>타원 안 (가장자리 부드럽게) — 경위도(라디안) 단위</summary>
    private static float Ell(float x, float y, float cx, float cy, float rx, float ry, float soft = 0.014f)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        float q = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01(0.5f + (1f - q) * Mathf.Min(rx, ry) / soft);
    }

    /// <summary>호(원둘레의 일부) — 굵기 th, from~to 는 각도(라디안, 0 = 오른쪽, 아래가 -π/2)</summary>
    private static float Arc(float x, float y, float cx, float cy, float r, float th, float from, float to, float soft = 0.01f)
    {
        float dx = x - cx, dy = y - cy;
        float a = Mathf.Atan2(dy, dx);
        if (a < from || a > to) return 0f;
        float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r);
        return Mathf.Clamp01(0.5f + (th * 0.5f - d) / soft);
    }

    private static float Line(float x, float y, float x0, float y0, float x1, float y1, float th, float soft = 0.01f)
    {
        float vx = x1 - x0, vy = y1 - y0;
        float t = Mathf.Clamp01(((x - x0) * vx + (y - y0) * vy) / (vx * vx + vy * vy));
        float dx = x - (x0 + vx * t), dy = y - (y0 + vy * t);
        return Mathf.Clamp01(0.5f + (th * 0.5f - Mathf.Sqrt(dx * dx + dy * dy)) / soft);
    }

    private static Color Mix(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
    private static Color Dark(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
    private static Color Light2(Color c, float k) => Mix(c, Color.white, k);

    private static bool InStar(float x, float y)
    {
        // 다섯 꼭짓점 별 (바깥 1 · 안 0.42) — 짝홀 규칙
        bool inside = false;
        for (int i = 0, j = 9; i < 10; j = i++)
        {
            float ai = Mathf.PI * 0.5f + i * Mathf.PI / 5f, aj = Mathf.PI * 0.5f + j * Mathf.PI / 5f;
            float ri = i % 2 == 0 ? 1f : 0.42f, rj = j % 2 == 0 ? 1f : 0.42f;
            float xi = Mathf.Cos(ai) * ri, yi = Mathf.Sin(ai) * ri, xj = Mathf.Cos(aj) * rj, yj = Mathf.Sin(aj) * rj;
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    private static bool InHeart(float x, float y)
    {
        y = y * 1.15f + 0.15f;
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y <= 0f;
    }

    // ── 굽기 ──────────────────────────────────────────────
    private sealed class Builder
    {
        public readonly Spec S;
        public readonly R0926GlbWriter W = new R0926GlbWriter();
        public readonly List<Part> Parts = new List<Part>();
        public readonly List<Group> Groups = new List<Group>();
        public readonly System.Random Rnd;
        public Vector3[] SpotDirs;
        public float[] SpotSize;
        public readonly Rig R = new Rig();
        public int TopNode = -1;   // 맨 위 묶음(Character)이 매달릴 노드
        public Builder(Spec s) { S = s; Rnd = new System.Random(s.Seed * 7919 + 13); }

        public int AddGroup(string name, int parent, Vector3 localPos, Quaternion localRot)
        {
            int node = W.AddNode(name, localPos, localRot, Vector3.one, -1, parent < 0 ? TopNode : Groups[parent].Node);
            var g = new Group { Node = node, LocalPos = localPos, LocalRot = localRot };
            if (parent < 0) { g.Pos = localPos; g.Rot = localRot; }
            else { var pg = Groups[parent]; g.Pos = pg.Pos + pg.Rot * localPos; g.Rot = pg.Rot * localRot; }
            Groups.Add(g);
            return Groups.Count - 1;
        }

        public Part Add(string name, int group, Vector3 c, Vector3 rad, Func<Sample, Color> paint, int nu = 18, int nv = 10)
        {
            var p = new Part { Name = name, Group = group, C = c, Rad = rad, Paint = paint, Nu = nu, Nv = nv };
            Parts.Add(p);
            return p;
        }

        public Color Fur(Sample s, Color c, bool face = false)
        {
            if (S.Pattern == Pattern.Spots && SpotDirs != null)
            {
                Vector3 d = s.D.normalized;
                for (int i = 0; i < SpotDirs.Length; i++)
                {
                    float dot = Vector3.Dot(d, SpotDirs[i]);
                    float edge = (dot - Mathf.Cos(SpotSize[i])) / 0.02f;
                    if (edge > -0.5f && !(face && Mathf.Abs(s.Lon) < 0.95f && s.Lat < 0.35f && s.Lat > -0.6f)) c = Mix(c, S.Detail, Mathf.Clamp01(0.5f + edge));
                }
            }
            else if (S.Pattern == Pattern.Stripes && !(face && Mathf.Abs(s.Lon) < 0.95f && s.Lat < 0.4f))
            {
                float band = Mathf.Sin(s.P.y * 34f + 1.4f * Mathf.Sin(s.Lon * 2f + s.P.y * 6f));
                c = Mix(c, S.Detail, SmoothStep(0.45f, 0.62f, band));
            }
            return c;
        }
    }

    private static Color Shade(Part p, Sample s, Color c)
    {
        if (p.Unshaded) return c;
        float ndl = Vector3.Dot(s.N, Light);
        float lit = 0.7f + 0.3f * SmoothStep(-0.35f, 0.65f, ndl);
        float ao = Mathf.Lerp(0.84f, 1f, Mathf.Clamp01(s.N.y * 0.5f + 0.8f));
        float sheen = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(s.N, Half)), p.Matte ? 22f : 60f) * (p.Matte ? 0.07f : 0.5f);
        float k = lit * ao;
        return new Color(Mathf.Clamp01(c.r * k + sheen), Mathf.Clamp01(c.g * k + sheen), Mathf.Clamp01(c.b * k + sheen), 1f);
    }

    /// <summary>요청사항 → GLB. sign 을 주면 오른손에 팻말(대표 사진)을 든다</summary>
    public static byte[] Build(Spec spec, Color32[] sign = null, int signSide = 0, bool signAlpha = false)
    {
        var b = new Builder(spec);
        if (spec.Pattern == Pattern.Spots)
        {
            int n = 16;
            b.SpotDirs = new Vector3[n]; b.SpotSize = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 v;
                do v = new Vector3((float)b.Rnd.NextDouble() * 2f - 1f, (float)b.Rnd.NextDouble() * 2f - 1f, (float)b.Rnd.NextDouble() * 2f - 1f); while (v.sqrMagnitude > 1f || v.sqrMagnitude < 0.05f);
                b.SpotDirs[i] = v.normalized;
                b.SpotSize[i] = 0.16f + (float)b.Rnd.NextDouble() * 0.16f;
            }
        }

        // Root(로더가 크기 · 자리를 덮어쓴다 — 그대로 둔다) → Model(발바닥을 0에 · 키 맞춤) → Character(움직임) → 몸 · 머리 · 팔 …
        int root = b.W.AddNode("Root", Vector3.zero, Quaternion.identity, Vector3.one);
        int model = b.W.AddNode("Model", Vector3.zero, Quaternion.identity, Vector3.one, -1, root);
        b.TopNode = model;
        b.AddGroup("Character", -1, Vector3.zero, Quaternion.identity);
        BuildBody(b, sign != null, signAlpha);

        // 캐릭터 크기 — 팻말 빼고 키 1.2m (아기 · 큰 캐릭터는 그 배율)
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in b.Parts)
        {
            if (p.Name.StartsWith("Sign", StringComparison.Ordinal)) continue;
            var g = b.Groups[p.Group];
            for (int j = 0; j <= 6; j++)
                for (int i = 0; i < 12; i++)
                {
                    Vector3 w = g.Pos + g.Rot * Pos(p, i / 12f, j / 6f);
                    minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
                }
        }
        float scale = 1.2f * spec.Size / Mathf.Max(0.2f, maxY - minY);

        // 그림판
        const int AW = 1024, AH = 1024, Gut = 4;   // 틈 4칸 — 멀리서(밉맵) 봐도 이웃 조각 색이 번지지 않게
        float dens = 380f;
        while (!Pack(b.Parts, AW, AH, dens, Gut)) dens *= 0.88f;
        var px = new byte[AW * AH * 3];
        for (int i = 0; i < px.Length; i += 3) { px[i] = 200; px[i + 1] = 200; px[i + 2] = 200; }
        foreach (var p in b.Parts) Orient(p);
        foreach (var p in b.Parts) Bake(b, p, px, AW, AH, Gut);
        byte[] jpg = ImageConversion.EncodeArrayToJPG(px, GraphicsFormat.R8G8B8_SRGB, AW, AH, 0, 88);
        int tex = b.W.AddTexture(jpg, "image/jpeg");
        int mat = b.W.AddMaterial("Character", tex, false, true);

        foreach (var p in b.Parts) AddMesh(b, p, mat, AW, AH);
        if (sign != null) AddSignPhoto(b, sign, signSide, signAlpha);

        Animate(b);
        b.W.SetNode(model, new Vector3(0f, -minY * scale, 0f), Quaternion.identity, Vector3.one * scale);
        return b.W.Build(root, "Idle");
    }

    // 몸 · 머리 · 팔다리 · 귀 · 꼬리 · 소품
    private sealed class Rig
    {
        public int Body, Head, ArmL, ArmR, Tail = -1, EarL = -1, EarR = -1, EyeL = -1, EyeR = -1, Sign = -1;
        public Part HeadPart;
        public Quaternion ArmLBind, ArmRBind;
        public float Hs, Bs, Chub;
    }

    private static void BuildBody(Builder b, bool holdSign, bool signAlpha)
    {
        var s = b.S;
        var r = b.R;
        r.Hs = s.Baby ? 1.12f : 1f;
        r.Bs = s.Baby ? 0.9f : 1f;
        r.Chub = s.Chub;
        float hs = r.Hs, bs = r.Bs, ch = s.Chub;
        Kind k = s.Kind;
        bool person = k == Kind.Person, robot = k == Kind.Robot;
        Color main = s.Main, second = s.Second;

        // 발
        Color footC = k == Kind.Panda ? second : (k == Kind.Chick || k == Kind.Penguin) ? new Color(1f, 0.6f, 0.18f) : person ? new Color(0.25f, 0.2f, 0.2f) : robot ? Dark(s.Second, 0.8f) : main;
        for (int side = -1; side <= 1; side += 2)
        {
            var foot = b.Add(side < 0 ? "FootL" : "FootR", 0, new Vector3(side * 0.105f * ch * bs, 0.055f * bs, 0.025f), new Vector3(0.075f, 0.062f, 0.092f) * bs, null, 16, 8);
            Color fc = footC;
            foot.Paint = sm => k == Kind.Panda || person || robot || k == Kind.Chick || k == Kind.Penguin ? fc : b.Fur(sm, fc);
            if (robot) { foot.E1 = 0.5f; foot.E2 = 0.5f; }
        }

        // 몸
        r.Body = b.AddGroup("Body", 0, new Vector3(0f, 0.055f * bs, 0f), Quaternion.identity);
        var body = b.Add("BodyMesh", r.Body, new Vector3(0f, 0.215f * bs, 0f), new Vector3(0.215f * ch, 0.215f, 0.19f * ch) * bs, null, 28, 16);
        if (robot) { body.E1 = 0.35f; body.E2 = 0.35f; body.Rad = new Vector3(0.2f * ch, 0.2f, 0.17f * ch) * bs; }
        body.Paint = sm => PaintBody(b, sm);

        // 머리
        r.Head = b.AddGroup("Head", 0, new Vector3(0f, 0.44f * bs, 0f), Quaternion.identity);
        var head = r.HeadPart = b.Add("HeadMesh", r.Head, new Vector3(0f, 0.25f * hs, 0f), new Vector3(0.3f, 0.27f, 0.27f) * hs, null, 34, 20);
        head.Density = 1.35f;
        if (robot) { head.E1 = 0.3f; head.E2 = 0.3f; head.Rad = new Vector3(0.28f, 0.24f, 0.24f) * hs; }
        if (person) head.Rad = new Vector3(0.28f, 0.28f, 0.27f) * hs;
        head.Paint = sm => PaintHead(b, sm);

        // 팔
        float sx = 0.175f * ch * bs + 0.01f;
        r.ArmLBind = Quaternion.Euler(0f, 0f, -28f);
        r.ArmRBind = holdSign ? Quaternion.Euler(0f, 0f, 150f) : Quaternion.Euler(0f, 0f, 28f);
        r.ArmL = b.AddGroup("ArmL", 0, new Vector3(-sx, 0.39f * bs, 0f), r.ArmLBind);
        r.ArmR = b.AddGroup("ArmR", 0, new Vector3(sx, 0.39f * bs, 0f), r.ArmRBind);
        foreach (int g in new[] { r.ArmL, r.ArmR })
        {
            Vector3 rad = new Vector3(0.055f, 0.115f, 0.055f) * bs;
            if (k == Kind.Penguin) rad = new Vector3(0.028f, 0.13f, 0.065f) * bs;
            if (k == Kind.Chick) rad = new Vector3(0.03f, 0.095f, 0.075f) * bs;
            var arm = b.Add(g == r.ArmL ? "ArmLMesh" : "ArmRMesh", g, new Vector3(0f, -0.1f * bs, 0f), rad, null, 14, 10);
            if (robot) { arm.E1 = 0.6f; arm.E2 = 0.6f; }
            arm.Paint = sm => PaintArm(b, sm);
        }

        // 꼬리
        AddTail(b);
        // 귀 · 눈 · 코 · 입 둘레
        AddFace(b);
        // 소품
        AddHat(b);
        AddExtras(b);
        if (holdSign) AddSign(b, signAlpha);
    }

    private static Color PaintBody(Builder b, Sample sm)
    {
        var s = b.S;
        Kind k = s.Kind;
        Color c = s.Main;
        float lon = sm.Lon, lat = sm.Lat;
        switch (k)
        {
            case Kind.Person:
                c = lat < -0.32f ? s.Second : s.Main;
                if (Mathf.Abs(lat + 0.32f) < 0.035f) c = Dark(s.Second, 0.6f);   // 허리띠
                if (s.Job == Job.Farmer && Mathf.Abs(lon) < 0.55f && lat > -0.34f && lat < 0.42f) c = s.Second;   // 멜빵바지 가슴판
                if (s.Job == Job.Farmer && lat > 0.3f && (Mathf.Abs(lon - 0.4f) < 0.09f || Mathf.Abs(lon + 0.4f) < 0.09f)) c = s.Second;
                if ((s.Job == Job.Chef || s.Job == Job.Doctor) && Mathf.Abs(lon) < 0.5f && lat > -0.3f)
                    foreach (float by in new[] { 0.25f, 0.05f, -0.15f }) c = Mix(c, new Color(0.75f, 0.75f, 0.78f), Ell(lon, lat, 0f, by, 0.045f, 0.045f));
                if (s.Job == Job.Santa) { if (Mathf.Abs(lat + 0.32f) < 0.07f) c = new Color(0.15f, 0.12f, 0.12f); if (Mathf.Abs(lon) < 0.06f && lat > -0.25f) c = Color.white; }
                if (s.Job == Job.Police && InStar((lon - 0.35f) / 0.09f, (lat - 0.3f) / 0.09f)) c = new Color(1f, 0.82f, 0.3f);
                if (s.Job == Job.Princess && Mathf.Abs(lat + 0.32f) < 0.06f) c = new Color(1f, 0.85f, 0.4f);
                break;
            case Kind.Robot:
                c = s.Main;
                float panel = Ell(lon, lat, 0f, 0.0f, 0.52f, 0.42f, 0.02f);
                c = Mix(c, Dark(s.Second, 0.6f), panel);
                c = Mix(c, new Color(1f, 0.3f, 0.3f), Ell(lon, lat, -0.22f, 0.12f, 0.08f, 0.08f));
                c = Mix(c, new Color(1f, 0.85f, 0.25f), Ell(lon, lat, 0f, 0.12f, 0.08f, 0.08f));
                c = Mix(c, new Color(0.35f, 0.95f, 0.5f), Ell(lon, lat, 0.22f, 0.12f, 0.08f, 0.08f));
                c = Mix(c, new Color(0.55f, 0.85f, 1f), Line(lon, lat, -0.3f, -0.15f, 0.3f, -0.15f, 0.04f));
                break;
            case Kind.Penguin:
                c = Mix(s.Main, s.Second, Ell(lon, lat, 0f, -0.1f, 0.95f, 0.95f, 0.03f));
                break;
            case Kind.Panda:
                c = lat > 0.42f ? s.Second : s.Main;
                break;
            case Kind.Chick:
            case Kind.Pig:
                c = b.Fur(sm, s.Main);
                break;
            default:
                c = b.Fur(sm, s.Main);
                c = Mix(c, s.Second, Ell(lon, lat, 0f, -0.15f, 0.72f, 0.62f, 0.03f));
                break;
        }
        if (s.Extras.HasFlag(Extra.Heart) && InHeart(lon / 0.24f, (lat + 0.05f) / 0.22f)) c = s.ExtraColor[Extra.Heart];
        else if (s.Extras.HasFlag(Extra.Star) && InStar(lon / 0.26f, (lat + 0.05f) / 0.26f)) c = s.ExtraColor[Extra.Star];
        return c;
    }

    private static Color PaintArm(Builder b, Sample sm)
    {
        var s = b.S;
        switch (s.Kind)
        {
            case Kind.Person: return sm.D.y < -0.55f ? s.Skin : s.Main;
            case Kind.Panda: return s.Second;
            case Kind.Robot: return sm.D.y < -0.6f ? Dark(s.Second, 0.8f) : s.Main;
            default: return b.Fur(sm, s.Main);
        }
    }

    private static Color PaintHead(Builder b, Sample sm)
    {
        var s = b.S;
        Kind k = s.Kind;
        float lon = sm.Lon, lat = sm.Lat;
        Color c = k == Kind.Person ? s.Skin : s.Main;
        if (k != Kind.Person && k != Kind.Robot) c = b.Fur(sm, c, true);

        switch (k)
        {
            case Kind.Person:
            {
                float hairline = -0.45f + 0.77f * (0.5f + 0.5f * Mathf.Cos(lon));   // 앞 0.32 · 옆 0 · 뒤 -0.45
                if (Mathf.Abs(lon) < 0.75f) hairline = Mathf.Min(hairline, 0.2f + 0.035f * Mathf.Cos(lon * 16f));   // 앞머리 끝
                if (lat > hairline) c = Mix(s.Hair, Dark(s.Hair, 0.82f), 0.5f + 0.5f * Mathf.Sin(lon * 40f));
                break;
            }
            case Kind.Robot:
            {
                float screen = Mathf.Abs(lon) < 0.78f && lat > -0.5f && lat < 0.36f ? 1f : 0f;
                if (screen > 0f)
                {
                    c = new Color(0.1f, 0.13f, 0.2f);
                    Color glow = new Color(0.35f, 0.95f, 1f);
                    float eyes = s.Face == Face.Sleepy ? Line(lon, lat, -0.4f, 0.02f, -0.18f, 0.02f, 0.05f) + Line(lon, lat, 0.18f, 0.02f, 0.4f, 0.02f, 0.05f)
                                                      : Ell(lon, lat, -0.3f, 0.04f, 0.09f, 0.12f) + Ell(lon, lat, 0.3f, 0.04f, 0.09f, 0.12f);
                    float mouth = Arc(lon, lat, 0f, -0.12f, 0.18f, 0.05f, -2.6f, -0.5f);
                    c = Mix(c, glow, Mathf.Clamp01(eyes + mouth));
                }
                return c;
            }
            case Kind.Panda:
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float ex = lon - side * 0.37f, ey = lat + 0.02f;
                    float rot = side * -0.45f;
                    float rx = ex * Mathf.Cos(rot) - ey * Mathf.Sin(rot), ry = ex * Mathf.Sin(rot) + ey * Mathf.Cos(rot);
                    c = Mix(c, s.Second, Ell(rx, ry, 0f, 0f, 0.15f, 0.11f));
                }
                break;
            }
            case Kind.Fox:
                if (lat < -0.02f && Mathf.Abs(lon) < 0.95f) c = Mix(c, s.Second, Mathf.Clamp01((-0.02f - lat) / 0.06f));
                break;
            case Kind.Penguin:
                for (int side = -1; side <= 1; side += 2) c = Mix(c, s.Second, Ell(lon, lat, side * 0.3f, -0.08f, 0.3f, 0.36f, 0.03f));
                break;
            case Kind.Cat:
                if (s.Main.grayscale < 0.9f) c = Mix(c, s.Second, Ell(lon, lat, 0f, -0.3f, 0.36f, 0.2f, 0.03f));
                for (int side = -1; side <= 1; side += 2)
                    for (int w = 0; w < 3; w++)
                        c = Mix(c, new Color(0.35f, 0.28f, 0.27f), Line(lon, lat, side * 0.26f, -0.26f - w * 0.05f, side * 0.6f, -0.2f - w * 0.09f, 0.012f, 0.006f));
                break;
        }

        // 볼 · 눈썹 · 입 (입은 주둥이 · 부리가 없는 얼굴에만 — 주둥이는 주둥이에 그린다)
        bool muzzle = k == Kind.Dog || k == Kind.Bear || k == Kind.Panda || k == Kind.Fox || k == Kind.Pig || k == Kind.Rabbit;
        for (int side = -1; side <= 1; side += 2)
            c = Mix(c, new Color(1f, 0.55f, 0.6f), 0.55f * Ell(lon, lat, side * (k == Kind.Frog ? 0.62f : 0.56f), -0.2f, 0.1f, 0.065f, 0.04f));
        if (s.Face == Face.Angry || s.Face == Face.Sad)
            for (int side = -1; side <= 1; side += 2)
            {
                float inner = s.Face == Face.Angry ? 0.2f : 0.3f, outer = s.Face == Face.Angry ? 0.3f : 0.2f;
                c = Mix(c, Dark(k == Kind.Person ? s.Hair : s.Main, 0.35f), Line(lon, lat, side * 0.22f, inner, side * 0.5f, outer, 0.035f));
            }
        if (!muzzle && k != Kind.Chick && k != Kind.Penguin) c = Mix(c, MouthColor(s), Mouth(s, lon, k == Kind.Frog ? lat + 0.06f : lat + 0.05f, k == Kind.Frog ? 1.8f : 1f));
        return c;
    }

    private static Color MouthColor(Spec s) => new Color(0.36f, 0.12f, 0.13f);

    /// <summary>입 — (0, -0.3) 둘레 (경위도 단위 · w 는 가로 배율)</summary>
    private static float Mouth(Spec s, float x, float y, float w)
    {
        x /= w;
        float cy = -0.3f;
        switch (s.Face)
        {
            case Face.Happy:
                return Mathf.Max(Ell(x, y, 0f, cy + 0.02f, 0.085f, 0.075f) * (y < cy + 0.02f ? 1f : 0f), Line(x, y, -0.085f, cy + 0.02f, 0.085f, cy + 0.02f, 0.02f));
            case Face.Surprised:
                return Ell(x, y, 0f, cy - 0.01f, 0.045f, 0.06f);
            case Face.Sad:
                return Arc(x, y, 0f, cy - 0.08f, 0.07f, 0.02f, 0.5f, 2.64f);
            case Face.Calm:
            case Face.Angry:
                return Line(x, y, -0.05f, cy, 0.05f, cy, 0.02f);
            default:
                return Arc(x, y, 0f, cy + 0.05f, 0.075f, 0.022f, -2.5f, -0.64f);
        }
    }

    private static void AddFace(Builder b)
    {
        var s = b.S;
        var r = b.R;
        Kind k = s.Kind;
        var head = r.HeadPart;
        Vector3 HeadPoint(float lon, float lat, float lift, out Vector3 n)
        {
            var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
            if (k == Kind.Robot) d = new Vector3(Sp(Mathf.Cos(lat), 0.3f) * Sp(Mathf.Sin(lon), 0.3f), Sp(Mathf.Sin(lat), 0.3f), Sp(Mathf.Cos(lat), 0.3f) * Sp(Mathf.Cos(lon), 0.3f));
            n = new Vector3(d.x / head.Rad.x, d.y / head.Rad.y, d.z / head.Rad.z).normalized;
            return head.C + Vector3.Scale(head.Rad, d) + n * lift;
        }

        // 귀
        Color inner = k == Kind.Bear ? s.Second : new Color(1f, 0.72f, 0.78f);
        switch (k)
        {
            case Kind.Dog:
                Ears(b, HeadPoint, 1.05f, 0.42f, new Vector3(0.36f, -1f, 0.12f), new Vector3(0.085f, 0.16f, 0.04f), 0.15f, Vector3.zero, sm => b.Fur(sm, Dark(s.Main, 0.82f)));
                break;
            case Kind.Cat:
                Ears(b, HeadPoint, 0.62f, 0.72f, new Vector3(0.35f, 1f, -0.05f), new Vector3(0.1f, 0.12f, 0.045f), -0.88f, Vector3.zero, sm => sm.D.z > 0.2f && Mathf.Abs(sm.D.x) < 0.55f && sm.D.y < 0.55f ? inner : b.Fur(sm, s.Main));
                break;
            case Kind.Fox:
                Ears(b, HeadPoint, 0.6f, 0.7f, new Vector3(0.3f, 1f, -0.03f), new Vector3(0.11f, 0.15f, 0.05f), -0.9f, Vector3.zero,
                    sm => sm.D.y > 0.55f ? new Color(0.2f, 0.15f, 0.14f) : sm.D.z > 0.2f && Mathf.Abs(sm.D.x) < 0.55f ? s.Second : s.Main);
                break;
            case Kind.Rabbit:
                Ears(b, HeadPoint, 0.28f, 0.86f, new Vector3(0.18f, 1f, -0.06f), new Vector3(0.068f, 0.21f, 0.035f), -0.3f, new Vector3(0f, 0f, -0.03f),
                    sm => sm.D.z > 0.15f && Mathf.Abs(sm.D.x) < 0.6f && Mathf.Abs(sm.D.y) < 0.8f ? inner : b.Fur(sm, s.Main));
                break;
            case Kind.Bear:
            case Kind.Panda:
                Ears(b, HeadPoint, 0.78f, 0.7f, new Vector3(0.6f, 1f, 0f), new Vector3(0.085f, 0.08f, 0.045f), 0f, Vector3.zero,
                    sm => k == Kind.Panda ? s.Second : sm.D.z > 0.3f && sm.D.y < 0.75f ? inner : b.Fur(sm, s.Main));
                break;
            case Kind.Pig:
                Ears(b, HeadPoint, 0.75f, 0.62f, new Vector3(0.5f, 0.6f, 0.62f), new Vector3(0.08f, 0.1f, 0.03f), -0.8f, Vector3.zero, sm => s.Second);
                break;
            case Kind.Chick:
                for (int i = -1; i <= 1; i++)
                {
                    var tuft = b.Add("Tuft" + (i + 1), r.Head, HeadPoint(i * 0.12f, 1.35f, -0.01f, out _), new Vector3(0.025f, 0.07f, 0.025f), _ => b.S.Main, 10, 6);
                    tuft.Taper = -0.92f;
                    tuft.Q = Quaternion.Euler(0f, 0f, -i * 25f);
                    tuft.C += tuft.Q * new Vector3(0f, 0.05f, 0f);
                }
                break;
            case Kind.Person:
                for (int side = -1; side <= 1; side += 2)
                {
                    var ear = b.Add(side < 0 ? "EarL" : "EarR", r.Head, HeadPoint(side * 1.52f, 0.02f, -0.012f, out _), new Vector3(0.035f, 0.055f, 0.03f), _ => Dark(b.S.Skin, 0.95f), 10, 6);
                    ear.Q = Quaternion.identity;
                }
                if (s.Extras.HasFlag(Extra.Buns))
                    for (int side = -1; side <= 1; side += 2)
                        b.Add(side < 0 ? "BunL" : "BunR", r.Head, HeadPoint(side * 1.25f, 0.62f, 0.03f, out _), new Vector3(0.09f, 0.09f, 0.09f), sm => Mix(b.S.Hair, Dark(b.S.Hair, 0.8f), 0.5f + 0.5f * Mathf.Sin(sm.Lon * 30f)), 14, 8);
                {
                    var hair = b.Add("Hair", r.Head, head.C + new Vector3(0f, 0.012f, -0.012f), head.Rad * 1.06f, sm => Mix(b.S.Hair, Dark(b.S.Hair, 0.82f), 0.5f + 0.5f * Mathf.Sin(sm.Lon * 40f)), 30, 10);
                    hair.V0 = 0.58f; hair.Q = Quaternion.Euler(-22f, 0f, 0f);
                }
                break;
            case Kind.Robot:
                for (int side = -1; side <= 1; side += 2)
                {
                    var bolt = b.Add(side < 0 ? "BoltL" : "BoltR", r.Head, HeadPoint(side * Mathf.PI * 0.5f, 0f, 0f, out _), new Vector3(0.05f, 0.03f, 0.05f), _ => Dark(b.S.Second, 0.9f), 14, 6);
                    bolt.E1 = 0.2f; bolt.Q = Quaternion.Euler(0f, 0f, 90f);
                }
                {
                    Vector3 top = HeadPoint(0f, Mathf.PI * 0.5f, 0f, out _);
                    var stem = b.Add("Antenna", r.Head, top + new Vector3(0f, 0.06f, 0f), new Vector3(0.012f, 0.07f, 0.012f), _ => new Color(0.5f, 0.52f, 0.56f), 10, 6);
                    stem.E1 = 0.2f;
                    var ball = b.Add("AntennaBall", r.Head, top + new Vector3(0f, 0.15f, 0f), new Vector3(0.035f, 0.035f, 0.035f), _ => new Color(1f, 0.35f, 0.35f), 12, 8);
                    ball.Matte = false;
                }
                break;
        }

        // 눈 (로봇은 화면에 그린다)
        if (k != Kind.Robot)
        {
            float elon = k == Kind.Person ? 0.33f : k == Kind.Frog ? 0.42f : 0.37f;
            float elat = k == Kind.Person ? -0.02f : 0.03f;
            float eScale = (s.Baby ? 1.15f : 1f) * (k == Kind.Person ? 0.82f : 1f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pos, n;
                if (k == Kind.Frog)
                {
                    Vector3 bc = HeadPoint(side * 0.42f, 0.62f, -0.03f, out _);
                    b.Add(side < 0 ? "BumpL" : "BumpR", r.Head, bc, new Vector3(0.1f, 0.1f, 0.1f), sm => b.Fur(sm, b.S.Main), 16, 10);
                    n = new Vector3(side * 0.25f, 0.15f, 1f).normalized;
                    pos = bc + n * 0.085f;
                }
                else pos = HeadPoint(side * elon, elat, -0.012f, out n);
                int g = b.AddGroup(side < 0 ? "EyeL" : "EyeR", r.Head, pos, Quaternion.LookRotation(n, Vector3.up));
                if (side < 0) r.EyeL = g; else r.EyeR = g;
                var eye = b.Add(side < 0 ? "EyeLMesh" : "EyeRMesh", g, Vector3.zero, new Vector3(0.048f, 0.062f, 0.03f) * eScale, null, 16, 10);
                eye.Unshaded = true;
                eye.Paint = sm =>
                {
                    Vector3 d = sm.D.normalized;
                    Color dark = new Color(0.07f, 0.05f, 0.06f), warm = new Color(0.26f, 0.17f, 0.15f);
                    Color c = Mix(dark, warm, Mathf.Clamp01(-d.y * 0.9f) * Mathf.Clamp01(d.z));
                    if (Vector3.Dot(d, new Vector3(-0.38f, 0.48f, 0.79f).normalized) > 0.93f) c = Color.white;
                    if (Vector3.Dot(d, new Vector3(0.32f, -0.3f, 0.9f).normalized) > 0.986f) c = new Color(0.92f, 0.92f, 0.95f);
                    return c;
                };
            }
        }

        // 주둥이 · 코 · 부리
        Color noseC = new Color(0.16f, 0.12f, 0.12f);
        switch (k)
        {
            case Kind.Dog: Muzzle(b, HeadPoint, new Vector3(0.11f, 0.078f, 0.075f), s.Second, noseC, 0.04f); break;
            case Kind.Bear: Muzzle(b, HeadPoint, new Vector3(0.1f, 0.075f, 0.07f), s.Second, new Color(0.25f, 0.15f, 0.12f), 0.038f); break;
            case Kind.Panda: Muzzle(b, HeadPoint, new Vector3(0.1f, 0.072f, 0.066f), s.Main, noseC, 0.036f); break;
            case Kind.Fox: Muzzle(b, HeadPoint, new Vector3(0.085f, 0.065f, 0.1f), s.Second, noseC, 0.03f); break;
            case Kind.Rabbit: Muzzle(b, HeadPoint, new Vector3(0.08f, 0.06f, 0.055f), Light2(s.Main, 0.4f), new Color(1f, 0.5f, 0.6f), 0.025f); break;
            case Kind.Cat:
            {
                var nose = b.Add("Nose", r.Head, HeadPoint(0f, -0.14f, 0.006f, out Vector3 nn), new Vector3(0.03f, 0.022f, 0.02f), _ => new Color(1f, 0.55f, 0.62f), 10, 6);
                nose.Q = Quaternion.LookRotation(nn, Vector3.up); nose.Matte = false;
                break;
            }
            case Kind.Person:
            {
                var nose = b.Add("Nose", r.Head, HeadPoint(0f, -0.13f, 0.004f, out Vector3 nn), new Vector3(0.022f, 0.025f, 0.018f), _ => Dark(b.S.Skin, 0.93f), 10, 6);
                nose.Q = Quaternion.LookRotation(nn, Vector3.up);
                break;
            }
            case Kind.Pig:
            {
                Vector3 p = HeadPoint(0f, -0.16f, -0.01f, out Vector3 nn);
                var snout = b.Add("Snout", r.Head, p + nn * 0.03f, new Vector3(0.09f, 0.045f, 0.065f), null, 18, 8);
                snout.E1 = 0.35f; snout.Q = Quaternion.FromToRotation(Vector3.up, nn);
                snout.Paint = sm =>
                {
                    Color c = b.S.Second;
                    if (sm.D.y > 0.6f) for (int side = -1; side <= 1; side += 2) c = Mix(c, new Color(0.55f, 0.25f, 0.3f), Ell(sm.D.x, sm.D.z, side * 0.38f, 0f, 0.13f, 0.22f, 0.04f));
                    return c;
                };
                break;
            }
            case Kind.Chick:
            case Kind.Penguin:
            {
                Vector3 p = HeadPoint(0f, -0.1f, -0.02f, out Vector3 nn);
                var beak = b.Add("Beak", r.Head, p + nn * 0.045f, new Vector3(0.055f, 0.075f, 0.042f), _ => new Color(1f, 0.62f, 0.15f), 14, 8);
                beak.Taper = -0.9f; beak.Q = Quaternion.FromToRotation(Vector3.up, (nn + Vector3.down * 0.15f).normalized);
                beak.Matte = false;
                break;
            }
        }
    }

    private delegate Vector3 HeadPointFn(float lon, float lat, float lift, out Vector3 n);

    private static void Ears(Builder b, HeadPointFn hp, float lon, float lat, Vector3 dir, Vector3 rad, float taper, Vector3 bend, Func<Sample, Color> paint)
    {
        var r = b.R;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 at = hp(side * lon, lat, -0.015f, out _);
            int g = b.AddGroup(side < 0 ? "EarL" : "EarR", r.Head, at, Quaternion.identity);
            if (side < 0) r.EarL = g; else r.EarR = g;
            Vector3 d = new Vector3(dir.x * side, dir.y, dir.z).normalized;
            var ear = b.Add(side < 0 ? "EarLMesh" : "EarRMesh", g, Vector3.zero, rad * (b.S.Baby ? 1.05f : 1f), paint, 16, 10);
            ear.Q = Quaternion.FromToRotation(Vector3.up, d) * Quaternion.Euler(0f, side * -8f, 0f);
            ear.C = ear.Q * new Vector3(0f, rad.y * 0.82f, 0f);
            ear.Taper = taper;
            ear.Bend = new Vector3(bend.x * side, bend.y, bend.z);
        }
    }

    private static void Muzzle(Builder b, HeadPointFn hp, Vector3 rad, Color col, Color nose, float noseR)
    {
        var r = b.R;
        var s = b.S;
        Vector3 p = hp(0f, -0.24f, -0.03f, out Vector3 n);
        var mz = b.Add("Muzzle", r.Head, p + n * 0.02f, rad, null, 20, 12);
        mz.Q = Quaternion.LookRotation(n, Vector3.up);
        mz.Density = 2.2f;
        mz.Paint = sm =>
        {
            Color c = b.Fur(sm, col, true);
            // 콧등에서 입으로 — 가운데 세로 줄과 웃는 입 (주둥이 경위도 기준)
            float x = sm.Lon, y = sm.Lat;
            float a = Line(x, y, 0f, 0.28f, 0f, 0.02f, 0.06f, 0.03f);
            float m;
            switch (s.Face)
            {
                case Face.Happy:
                    m = Ell(x, y, 0f, -0.18f, 0.42f, 0.38f, 0.03f) * (y < 0.02f ? 1f : 0f);
                    if (m > 0f) { Color tongue = new Color(1f, 0.5f, 0.56f); c = Mix(c, Mix(new Color(0.5f, 0.15f, 0.18f), tongue, Ell(x, y, 0f, -0.42f, 0.25f, 0.18f, 0.03f)), m); }
                    return Mix(c, MouthColor(s), a);
                case Face.Surprised: m = Ell(x, y, 0f, -0.25f, 0.2f, 0.25f, 0.03f); break;
                case Face.Sad: m = Arc(x, y, -0.2f, -0.42f, 0.22f, 0.07f, 0.4f, 2.4f, 0.03f) + Arc(x, y, 0.2f, -0.42f, 0.22f, 0.07f, 0.74f, 2.74f, 0.03f); break;
                case Face.Calm: case Face.Angry: m = Line(x, y, -0.28f, -0.12f, 0.28f, -0.12f, 0.07f, 0.03f); break;
                default: m = Arc(x, y, -0.2f, 0.06f, 0.2f, 0.07f, -3.1f, -0.2f, 0.03f) + Arc(x, y, 0.2f, 0.06f, 0.2f, 0.07f, -2.94f, -0.04f, 0.03f); break;
            }
            return Mix(c, MouthColor(s), Mathf.Clamp01(a + m));
        };
        var ns = b.Add("Nose", r.Head, mz.C + mz.Q * new Vector3(0f, rad.y * 0.45f, rad.z * 0.82f), new Vector3(noseR * 1.25f, noseR * 0.9f, noseR * 0.85f), _ => nose, 12, 8);
        ns.Q = mz.Q; ns.Matte = false;
    }

    private static void AddTail(Builder b)
    {
        var s = b.S;
        var r = b.R;
        Kind k = s.Kind;
        if (k == Kind.Person || k == Kind.Robot || k == Kind.Frog) return;
        float bs = r.Bs, ch = s.Chub;
        r.Tail = b.AddGroup("Tail", 0, new Vector3(0f, 0.2f * bs, -0.18f * ch * bs), Quaternion.identity);
        Part t;
        switch (k)
        {
            case Kind.Dog:
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.034f, 0.1f, 0.034f), sm => b.Fur(sm, s.Main), 12, 8);
                t.Taper = -0.6f; t.Q = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, 0.75f, -0.66f)); t.Bend = new Vector3(0f, 0f, 0.05f);
                break;
            case Kind.Cat:
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.028f, 0.2f, 0.028f), sm => b.Fur(sm, s.Main), 12, 12);
                t.Taper = -0.35f; t.Q = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, 0.45f, -0.9f)); t.Bend = new Vector3(0f, 0.0f, 0.16f);
                break;
            case Kind.Fox:
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.08f, 0.17f, 0.08f), sm => sm.D.y > 0.55f ? s.Second : s.Main, 14, 10);
                t.Taper = -0.7f; t.Q = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, 0.4f, -0.92f)); t.Bend = new Vector3(0f, 0f, 0.1f);
                break;
            case Kind.Pig:
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f), _ => s.Second, 16, 8);
                t.Torus = true; t.Ring = 0.3f; t.Q = Quaternion.Euler(90f, 0f, 0f);
                break;
            case Kind.Chick:
            case Kind.Penguin:
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.05f, 0.055f, 0.03f), _ => s.Main, 10, 6);
                t.Taper = -0.85f; t.Q = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, 0.2f, -1f));
                break;
            default:
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.055f, 0.055f, 0.055f), sm => k == Kind.Panda ? s.Second : b.Fur(sm, s.Main), 12, 8);
                break;
        }
        if (!t.Torus && t.Taper != 0f) t.C = t.Q * new Vector3(0f, t.Rad.y * 0.8f, 0f);
    }

    private static void AddHat(Builder b)
    {
        var s = b.S;
        var r = b.R;
        var head = r.HeadPart;
        if (s.Hat == Hat.None) return;
        Color hc = s.HatColor;
        Part Dome(float scale, float v0, float tilt, Func<Sample, Color> paint)
        {
            var d = b.Add("Hat", r.Head, head.C + new Vector3(0f, 0.01f, -0.01f), head.Rad * scale, paint, 30, 10);
            d.V0 = v0; d.Q = Quaternion.Euler(tilt, 0f, 0f);
            return d;
        }
        switch (s.Hat)
        {
            case Hat.Beanie:
            {
                var d = Dome(1.08f, 0.6f, -14f, sm => sm.V < 0.22f ? Dark(hc, 0.85f) : (Mathf.Sin(sm.V * 18f) > 0.6f ? Light2(hc, 0.35f) : hc));
                b.Add("Pompom", r.Head, Pos(d, 0.5f, 1f) + d.Q * new Vector3(0f, 0.035f, 0f), new Vector3(0.065f, 0.06f, 0.065f), _ => Light2(hc, 0.75f), 14, 8);
                break;
            }
            case Hat.Cap:
            {
                var d = Dome(1.07f, 0.58f, -10f, sm => s.Job == Job.Police && InStar((sm.Lon) / 0.18f, (sm.Lat - 0.75f) / 0.16f) ? new Color(1f, 0.82f, 0.3f) : hc);
                var visor = b.Add("Visor", r.Head, Pos(d, 0.5f, 0f) + new Vector3(0f, -0.008f, 0.09f), new Vector3(0.17f, 0.014f, 0.13f), _ => Dark(hc, 0.85f), 18, 6);
                visor.Q = Quaternion.Euler(-8f, 0f, 0f);
                break;
            }
            case Hat.Straw:
            {
                var d = Dome(1.07f, 0.6f, -8f, sm => sm.V < 0.25f ? new Color(0.85f, 0.25f, 0.25f) : Mix(hc, Dark(hc, 0.88f), 0.5f + 0.5f * Mathf.Sin(sm.V * 60f)));
                float edge = head.Rad.y * 1.07f * Mathf.Sin((d.V0 * 2f - 1f) * Mathf.PI * 0.5f);   // 둥근 부분의 아래 끝 높이
                var brim = b.Add("Brim", r.Head, d.C + d.Q * new Vector3(0f, edge, 0f),
                    new Vector3(0.44f, 0.012f, 0.42f) * r.Hs, sm => Mix(hc, Dark(hc, 0.9f), 0.5f + 0.5f * Mathf.Sin(sm.Lat * 80f)), 30, 8);
                brim.Q = d.Q;
                break;
            }
            case Hat.Helmet:
            {
                var d = Dome(1.12f, 0.52f, -8f, _ => hc);
                d.Matte = false;
                var ridge = b.Add("Ridge", r.Head, d.C + d.Q * new Vector3(0f, head.Rad.y * 1.12f, 0f), new Vector3(0.03f, 0.03f, 0.3f) * r.Hs, _ => Dark(hc, 0.85f), 10, 12);
                ridge.Q = d.Q;
                break;
            }
            case Hat.Chef:
            {
                Vector3 top = head.C + new Vector3(0f, head.Rad.y * 0.82f, -0.01f);
                var band = b.Add("Band", r.Head, top, new Vector3(head.Rad.x * 0.88f, 0.055f, head.Rad.z * 0.88f), _ => Dark(hc, 0.96f), 26, 6);
                band.E1 = 0.2f;
                b.Add("Puff", r.Head, top + new Vector3(0f, 0.17f, 0f), new Vector3(0.27f, 0.17f, 0.27f) * r.Hs, sm => Mix(hc, Dark(hc, 0.92f), 0.5f + 0.5f * Mathf.Sin(sm.Lon * 6f)), 24, 12);
                break;
            }
            case Hat.Santa:
            {
                Vector3 baseC = head.C + new Vector3(0f, head.Rad.y * 0.62f, -0.02f);
                var cone = b.Add("Hat", r.Head, baseC + new Vector3(0f, 0.28f, 0f), new Vector3(head.Rad.x * 1.0f, 0.3f, head.Rad.z * 1.0f), _ => hc, 26, 12);
                cone.Taper = -0.93f; cone.Bend = new Vector3(0.14f, -0.06f, -0.08f); cone.Q = Quaternion.Euler(-10f, 0f, 0f);
                var band = b.Add("Fur", r.Head, baseC + new Vector3(0f, 0.0f, 0f), new Vector3(head.Rad.x * 0.98f, head.Rad.x * 0.98f, head.Rad.z * 0.98f), _ => new Color(0.98f, 0.98f, 0.97f), 26, 8);
                band.Torus = true; band.Ring = 0.2f; band.Q = cone.Q;
                b.Add("Pompom", r.Head, Pos(cone, 0.5f, 1f), new Vector3(0.06f, 0.06f, 0.06f), _ => new Color(0.98f, 0.98f, 0.97f), 12, 8);
                break;
            }
        }
    }

    private static void AddExtras(Builder b)
    {
        var s = b.S;
        var r = b.R;
        var head = r.HeadPart;
        Kind k = s.Kind;
        float bs = r.Bs, ch = s.Chub;
        Vector3 HP(float lon, float lat, float lift, out Vector3 n)
        {
            var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
            n = new Vector3(d.x / head.Rad.x, d.y / head.Rad.y, d.z / head.Rad.z).normalized;
            return head.C + Vector3.Scale(head.Rad, d) + n * lift;
        }
        float elon = k == Kind.Person ? 0.33f : 0.37f;

        if (s.Extras.HasFlag(Extra.Crown))
        {
            Color gc = s.ExtraColor[Extra.Crown];
            Vector3 top = head.C + new Vector3(0f, head.Rad.y * (s.Hat == Hat.None ? 0.9f : 1.25f), 0f);
            var band = b.Add("Crown", r.Head, top, new Vector3(0.15f, 0.045f, 0.15f) * r.Hs, sm => Mathf.Abs(sm.Lat) < 0.6f && Mathf.Abs(Mathf.Repeat(sm.Lon + Mathf.PI * 0.2f, Mathf.PI * 0.4f) - Mathf.PI * 0.2f) < 0.12f
                ? (Mathf.Repeat(sm.Lon, 1.26f) < 0.63f ? new Color(0.9f, 0.2f, 0.3f) : new Color(0.25f, 0.45f, 0.95f)) : gc, 26, 6);
            band.E1 = 0.15f; band.Matte = false;
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var spike = b.Add("Spike" + i, r.Head, top + new Vector3(Mathf.Sin(a) * 0.135f * r.Hs, 0.07f, Mathf.Cos(a) * 0.135f * r.Hs), new Vector3(0.032f, 0.06f, 0.032f), _ => gc, 10, 6);
                spike.Taper = -0.92f; spike.Matte = false;
            }
        }
        if (s.Extras.HasFlag(Extra.Bow))
        {
            Color bc = s.ExtraColor[Extra.Bow];
            Vector3 p = HP(-0.6f, 0.8f, 0.02f, out Vector3 n);
            Quaternion q = Quaternion.LookRotation(n, Vector3.up);
            for (int side = -1; side <= 1; side += 2)
            {
                var loop = b.Add(side < 0 ? "BowL" : "BowR", r.Head, p + q * new Vector3(side * 0.062f, 0f, 0f), new Vector3(0.065f, 0.045f, 0.026f), _ => bc, 14, 8);
                loop.Q = q * Quaternion.Euler(0f, 0f, side * 18f);
            }
            b.Add("BowKnot", r.Head, p + n * 0.01f, new Vector3(0.028f, 0.03f, 0.024f), _ => Dark(bc, 0.85f), 10, 6);
        }
        if (s.Extras.HasFlag(Extra.Flower))
        {
            Color fc = s.ExtraColor[Extra.Flower];
            Vector3 p = HP(0.7f, 0.62f, 0.02f, out Vector3 n);
            Quaternion q = Quaternion.LookRotation(n, Vector3.up);
            for (int i = 0; i < 5; i++)
            {
                Quaternion qi = q * Quaternion.Euler(0f, 0f, i * 72f);
                var petal = b.Add("Petal" + i, r.Head, p + qi * new Vector3(0f, 0.045f, 0f), new Vector3(0.034f, 0.05f, 0.014f), _ => fc, 10, 6);
                petal.Q = qi;
            }
            b.Add("FlowerCenter", r.Head, p + n * 0.01f, new Vector3(0.026f, 0.026f, 0.016f), _ => new Color(1f, 0.85f, 0.25f), 10, 6).Q = q;
        }
        if (s.Extras.HasFlag(Extra.Glasses) || s.Extras.HasFlag(Extra.Sunglasses))
        {
            bool sun = s.Extras.HasFlag(Extra.Sunglasses);
            Color gc = s.ExtraColor[sun ? Extra.Sunglasses : Extra.Glasses];
            Vector3 pl = Vector3.zero, pr = Vector3.zero;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = HP(side * elon, 0.03f, 0.03f, out Vector3 n);
                if (side < 0) pl = p; else pr = p;
                if (sun)
                {
                    var lens = b.Add(side < 0 ? "LensL" : "LensR", r.Head, p, new Vector3(0.088f, 0.068f, 0.018f), _ => gc, 16, 8);
                    lens.Q = Quaternion.LookRotation(n, Vector3.up); lens.Matte = false;
                }
                else
                {
                    var rim = b.Add(side < 0 ? "RimL" : "RimR", r.Head, p, new Vector3(0.08f, 0.08f, 0.08f), _ => gc, 22, 8);
                    rim.Torus = true; rim.Ring = 0.13f; rim.Q = Quaternion.FromToRotation(Vector3.up, n);
                }
            }
            var bridge = b.Add("Bridge", r.Head, (pl + pr) * 0.5f + new Vector3(0f, 0.012f, 0.008f), new Vector3(0.012f, Mathf.Max(0.02f, (pr - pl).magnitude * 0.5f - 0.07f), 0.012f), _ => gc, 8, 4);
            bridge.E1 = 0.3f; bridge.Q = Quaternion.Euler(0f, 0f, 90f);
        }
        if (s.Extras.HasFlag(Extra.Scarf))
        {
            Color sc = s.ExtraColor[Extra.Scarf];
            var ring = b.Add("Scarf", 0, new Vector3(0f, 0.465f * bs, 0f), new Vector3(0.18f * ch, 0.18f, 0.17f * ch) * bs, sm => Mathf.Sin(sm.Lon * 8f) > 0.3f ? Light2(sc, 0.5f) : sc, 26, 10);
            ring.Torus = true; ring.Ring = 0.3f;
            var tail = b.Add("ScarfTail", 0, new Vector3(0.09f * ch, 0.36f * bs, 0.17f * ch * bs), new Vector3(0.05f, 0.1f, 0.022f), sm => Mathf.Sin(sm.Lat * 9f) > 0.3f ? Light2(sc, 0.5f) : sc, 10, 8);
            tail.Q = Quaternion.Euler(10f, 0f, 12f);
        }
        if (s.Extras.HasFlag(Extra.Tie))
        {
            Color tc = s.ExtraColor[Extra.Tie];
            var tie = b.Add("Tie", 0, new Vector3(0f, 0.36f * bs, 0.19f * ch * bs), new Vector3(0.038f, 0.085f, 0.014f), _ => tc, 10, 8);
            tie.Taper = -0.55f; tie.Q = Quaternion.Euler(-14f, 0f, 180f);
            b.Add("TieKnot", 0, new Vector3(0f, 0.45f * bs, 0.165f * ch * bs), new Vector3(0.028f, 0.024f, 0.02f), _ => Dark(tc, 0.85f), 8, 6);
        }
        if (s.Extras.HasFlag(Extra.Backpack))
        {
            Color pc = s.ExtraColor[Extra.Backpack];
            var bag = b.Add("Backpack", 0, new Vector3(0f, 0.3f * bs, -0.2f * ch * bs), new Vector3(0.15f, 0.15f, 0.07f) * bs, sm => sm.D.z < -0.3f && sm.D.y < 0.1f && Mathf.Abs(sm.D.x) < 0.55f ? Dark(pc, 0.85f) : pc, 18, 10);
            bag.E1 = 0.4f; bag.E2 = 0.4f;
        }
    }

    private const float SignH = 0.6f;   // 팻말 가운데 높이 (손에서)

    // 오른손 팻말 — 막대 + (투명 사진이면 사진 모양 그대로, 아니면 흰 판에 붙인 사진)
    private static void AddSign(Builder b, bool alpha)
    {
        var r = b.R;
        r.Sign = b.AddGroup("Sign", r.ArmR, new Vector3(0f, -0.2f * r.Bs, 0f), Quaternion.Inverse(r.ArmRBind));
        Color wood = new Color(0.72f, 0.52f, 0.32f), wood2 = new Color(0.62f, 0.43f, 0.26f);
        var stick = b.Add("SignStick", r.Sign, new Vector3(0f, 0.2f, -0.02f), new Vector3(0.013f, 0.24f, 0.013f), sm => Mix(wood, wood2, 0.5f + 0.5f * Mathf.Sin(sm.Lat * 20f)), 10, 6);
        stick.E1 = 0.25f;
        if (alpha) return;
        var board = b.Add("SignBoard", r.Sign, new Vector3(0f, SignH, 0f), new Vector3(0.215f, 0.215f, 0.014f), _ => new Color(0.98f, 0.97f, 0.95f), 20, 12);
        board.E1 = 0.16f; board.E2 = 0.16f; board.Density = 0.3f;
    }

    private static void AddSignPhoto(Builder b, Color32[] px, int side, bool alpha)
    {
        var r = b.R;
        byte[] img;
        if (alpha)
        {
            var raw = new byte[side * side * 4];
            for (int i = 0; i < px.Length; i++) { raw[i * 4] = px[i].r; raw[i * 4 + 1] = px[i].g; raw[i * 4 + 2] = px[i].b; raw[i * 4 + 3] = px[i].a; }
            img = ImageConversion.EncodeArrayToPNG(raw, GraphicsFormat.R8G8B8A8_SRGB, (uint)side, (uint)side);
        }
        else
        {
            var raw = new byte[side * side * 3];
            for (int i = 0; i < px.Length; i++) { raw[i * 3] = px[i].r; raw[i * 3 + 1] = px[i].g; raw[i * 3 + 2] = px[i].b; }
            img = ImageConversion.EncodeArrayToJPG(raw, GraphicsFormat.R8G8B8_SRGB, (uint)side, (uint)side, 0, 88);
        }
        int tex = b.W.AddTexture(img, alpha ? "image/png" : "image/jpeg");
        int mat = b.W.AddMaterial("SignPhoto", tex, alpha);
        int g = b.Groups[r.Sign].Node;
        // 앞뒤 모두 사진 — 돌아가도 보이게 (뒷면도 글자가 바로 읽히게 붙인다)
        float size = alpha ? 0.44f : 0.36f, z = alpha ? 0.001f : 0.016f;
        Quad(b, g, "SignPhotoFront", new Vector3(0f, SignH, z), size, false, mat);
        Quad(b, g, "SignPhotoBack", new Vector3(0f, SignH, -z), size, true, mat);
    }

    private static void Quad(Builder b, int parentNode, string name, Vector3 c, float size, bool back, int mat)
    {
        float h = size * 0.5f, sx = back ? -1f : 1f;
        // 보는 쪽에서 왼쪽 아래 → 오른쪽 아래 → 오른쪽 위 → 왼쪽 위 (반시계 = 앞면). 뒤에서 보면 보는 사람의 오른쪽이 -X
        var pos = new[] { c + new Vector3(-h * sx, -h, 0f), c + new Vector3(h * sx, -h, 0f), c + new Vector3(h * sx, h, 0f), c + new Vector3(-h * sx, h, 0f) };
        Vector3 n = back ? Vector3.back : Vector3.forward;
        var nrm = new[] { n, n, n, n };
        var uv = new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f) };   // glTF: t=0 이 그림 위
        int mesh = b.W.AddMesh(name, pos, nrm, uv, new ushort[] { 0, 1, 2, 0, 2, 3 }, mat);
        b.W.AddNode(name, Vector3.zero, Quaternion.identity, Vector3.one, mesh, parentNode);
    }

    private static bool Pack(List<Part> parts, int W, int H, float dens, int G)
    {
        foreach (var p in parts)
        {
            float circ, mer;
            if (p.Torus) { circ = 2f * Mathf.PI * p.Rad.x * (1f + p.Ring); mer = 2f * Mathf.PI * p.Ring * Mathf.Max(p.Rad.x, p.Rad.y); }
            else
            {
                float hr = Mathf.Max(p.Rad.x, p.Rad.z);
                circ = 2f * Mathf.PI * hr;
                mer = Mathf.PI * (p.Rad.y + hr) * 0.5f * (p.V1 - p.V0);
            }
            p.Rw = Mathf.Clamp(Mathf.RoundToInt(circ * dens * p.Density), 8, W - 2 * G);
            p.Rh = Mathf.Clamp(Mathf.RoundToInt(mer * dens * p.Density), 6, H - 2 * G);
        }
        var order = new List<Part>(parts);
        order.Sort((a, c) => c.Rh.CompareTo(a.Rh));
        int x = G, y = G, shelf = 0;
        foreach (var p in order)
        {
            if (x + p.Rw + G > W) { x = G; y += shelf + 2 * G; shelf = 0; }
            if (y + p.Rh + G > H) return false;
            p.Rx = x; p.Ry = y;
            x += p.Rw + 2 * G;
            shelf = Mathf.Max(shelf, p.Rh);
        }
        return true;
    }

    private static void Bake(Builder b, Part p, byte[] px, int W, int H, int G)
    {
        var g = b.Groups[p.Group];
        // 줄마다 따로 (조각끼리 · 줄끼리 칸이 겹치지 않는다)
        System.Threading.Tasks.Parallel.For(Math.Max(0, p.Ry - G), Math.Min(H, p.Ry + p.Rh + G), y =>
        {
            float v = Mathf.Clamp01((y - p.Ry) / (float)(p.Rh - 1));
            for (int x = p.Rx - G; x < p.Rx + p.Rw + G; x++)
            {
                if (x < 0 || x >= W) continue;
                float u = Mathf.Clamp01((x - p.Rx) / (float)(p.Rw - 1));
                Vector3 lp = Eval(p, u, v, out Vector3 d, out float lon, out float lat);
                var sm = new Sample { U = u, V = v, Lon = lon, Lat = lat, D = d, N = g.Rot * Normal(p, u, v), P = g.Pos + g.Rot * lp };
                Color c = Shade(p, sm, p.Paint != null ? p.Paint(sm) : Color.white);
                int i = (y * W + x) * 3;
                px[i] = (byte)(Mathf.Clamp01(c.r) * 255f + 0.5f);
                px[i + 1] = (byte)(Mathf.Clamp01(c.g) * 255f + 0.5f);
                px[i + 2] = (byte)(Mathf.Clamp01(c.b) * 255f + 0.5f);
            }
        });
    }

    private static void AddMesh(Builder b, Part p, int mat, int W, int H)
    {
        int nu = p.Nu, nv = p.Nv;
        int cnt = (nu + 1) * (nv + 1);
        var pos = new Vector3[cnt]; var nrm = new Vector3[cnt]; var uv = new Vector2[cnt];
        for (int j = 0; j <= nv; j++)
            for (int i = 0; i <= nu; i++)
            {
                float u = i / (float)nu, v = j / (float)nv;
                int k = j * (nu + 1) + i;
                pos[k] = Pos(p, u, v);
                nrm[k] = Normal(p, u, v);
                uv[k] = new Vector2((p.Rx + 0.5f + u * (p.Rw - 1)) / W, 1f - (p.Ry + 0.5f + v * (p.Rh - 1)) / H);
            }
        // 감기 방향 — 가운데 칸의 면 법선이 바깥(계산한 법선)을 보게
        int mi = nu / 2, mj = nv / 2;
        Vector3 a = pos[mj * (nu + 1) + mi], bb = pos[mj * (nu + 1) + mi + 1], c = pos[(mj + 1) * (nu + 1) + mi + 1];
        bool flip = Vector3.Dot(Vector3.Cross(bb - a, c - a), nrm[mj * (nu + 1) + mi]) < 0f;
        var idx = new List<ushort>(nu * nv * 6);
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                ushort i0 = (ushort)(j * (nu + 1) + i), i1 = (ushort)(i0 + 1), i2 = (ushort)(i0 + nu + 2), i3 = (ushort)(i0 + nu + 1);
                if (!flip) { idx.Add(i0); idx.Add(i1); idx.Add(i2); idx.Add(i0); idx.Add(i2); idx.Add(i3); }
                else { idx.Add(i0); idx.Add(i2); idx.Add(i1); idx.Add(i0); idx.Add(i3); idx.Add(i2); }
            }
        int mesh = b.W.AddMesh(p.Name, pos, nrm, uv, idx.ToArray(), mat);
        b.W.AddNode(p.Name, Vector3.zero, Quaternion.identity, Vector3.one, mesh, b.Groups[p.Group].Node);
    }

    // ── 움직임 (Idle · 2초 반복) ─────────────────────────────
    private static void Animate(Builder b)
    {
        var s = b.S;
        var r = b.R;
        const int N = 31;
        const float T = 2f;
        var times = new float[N];
        for (int i = 0; i < N; i++) times[i] = i * T / (N - 1);
        float w = Mathf.PI * 2f / T;
        var gC = b.Groups[0];

        // 몸 전체 — 춤은 좌우로 돌고 통통, 점프는 두 번 폴짝
        var cRot = new Quaternion[N]; var cPos = new Vector3[N];
        var bodyS = new Vector3[N]; var headR = new Quaternion[N];
        var armL = new Quaternion[N]; var armR = new Quaternion[N];
        var eyeS = new Vector3[N]; var winkS = new Vector3[N];
        var tail = new Quaternion[N]; var earL = new Quaternion[N]; var earR = new Quaternion[N];
        for (int i = 0; i < N; i++)
        {
            float t = times[i];
            float sw = Mathf.Sin(w * t), sw2 = Mathf.Sin(2f * w * t);
            float breathe = 0.5f + 0.5f * sw2;
            Vector3 cp = Vector3.zero;
            Quaternion cr = Quaternion.Euler(0f, 0f, 2f * sw);
            Vector3 bsc = new Vector3(1f + 0.012f * breathe, 1f + 0.03f * breathe, 1f + 0.012f * breathe);
            float aL = -28f - 6f * sw, aR = 28f + 6f * sw;
            switch (s.Move)
            {
                case Move.Dance:
                    cr = Quaternion.Euler(0f, 20f * sw, 6f * sw2);
                    cp = new Vector3(0f, 0.045f * Mathf.Abs(sw2), 0f);
                    aL = -(125f + 25f * Mathf.Sin(2f * w * t)); aR = 125f + 25f * Mathf.Sin(2f * w * t + Mathf.PI);
                    break;
                case Move.Jump:
                {
                    float hop = Mathf.Max(0f, sw2);
                    cp = new Vector3(0f, 0.13f * hop, 0f);
                    float land = Mathf.Max(0f, -sw2);
                    bsc = new Vector3(1f + 0.08f * land, 1f - 0.1f * land + 0.04f * hop, 1f + 0.08f * land);
                    aL = -28f - 70f * hop; aR = 28f + 70f * hop;
                    break;
                }
                case Move.Wave:
                    aL = -(135f + 22f * Mathf.Sin(3f * w * t));
                    break;
            }
            if (r.Sign >= 0) aR = 150f + 5f * sw;   // 팻말을 든 손은 살짝만
            cRot[i] = cr; cPos[i] = cp;
            bodyS[i] = bsc;
            headR[i] = Quaternion.Euler(2.5f * sw2, 0f, -4f * Mathf.Sin(w * t + 0.6f));
            armL[i] = Quaternion.Euler(0f, 0f, aL);
            armR[i] = Quaternion.Euler(0f, 0f, aR);
            float blink = 1f - 0.9f * Mathf.Exp(-Mathf.Pow((t - 1.4f) / 0.045f, 2f));
            float sleepy = s.Face == Face.Sleepy ? 0.14f : 1f;
            eyeS[i] = new Vector3(1f, s.Face == Face.Sleepy ? sleepy : blink, 1f);
            winkS[i] = new Vector3(1f, s.Face == Face.Wink ? 0.14f : (s.Face == Face.Sleepy ? sleepy : blink), 1f);
            tail[i] = Quaternion.Euler(0f, (s.Kind == Kind.Dog ? 32f : 18f) * Mathf.Sin(2f * w * t), 0f);
            earL[i] = Quaternion.Euler(6f * Mathf.Sin(2f * w * t + 1f), 0f, 0f);
            earR[i] = Quaternion.Euler(6f * Mathf.Sin(2f * w * t + 1.6f), 0f, 0f);
        }
        b.W.AddRotation(gC.Node, times, cRot);
        b.W.AddTranslation(gC.Node, times, cPos);
        b.W.AddScale(b.Groups[r.Body].Node, times, bodyS);
        var hp = new Vector3[N];
        for (int i = 0; i < N; i++) hp[i] = b.Groups[r.Head].LocalPos + new Vector3(0f, 0.006f * (0.5f + 0.5f * Mathf.Sin(2f * w * times[i])), 0f);
        b.W.AddTranslation(b.Groups[r.Head].Node, times, hp);
        b.W.AddRotation(b.Groups[r.Head].Node, times, headR);
        b.W.AddRotation(b.Groups[r.ArmL].Node, times, armL);
        b.W.AddRotation(b.Groups[r.ArmR].Node, times, armR);
        if (r.EyeL >= 0) b.W.AddScale(b.Groups[r.EyeL].Node, times, eyeS);
        if (r.EyeR >= 0) b.W.AddScale(b.Groups[r.EyeR].Node, times, winkS);
        if (r.Tail >= 0) b.W.AddRotation(b.Groups[r.Tail].Node, times, tail);
        if (r.EarL >= 0) b.W.AddRotation(b.Groups[r.EarL].Node, times, earL);
        if (r.EarR >= 0) b.W.AddRotation(b.Groups[r.EarR].Node, times, earR);
    }
}
