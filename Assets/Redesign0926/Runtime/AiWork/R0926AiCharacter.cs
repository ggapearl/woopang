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
///  · 학습된 3D 모델 없이 — 동물 22꼴(+오리·햄스터·유니콘·용 같은 갈래) · 사람 · 로봇 + 색 · 표정 · 모자/왕관/리본/안경/목도리/꽃/가방/날개/뿔/망토/옷 · 무늬 · 직업(농부·요리사·경찰·마법사…) · 움직임(손 흔들기·춤·점프)
///  · 못 알아들은 낱말은 버리지 않고 안내(Label)에 '아직 모르는 말'로 적는다 (11-14)
///  · 모양은 매끈한 매개변수 곡면(타원체 · 둥근 상자 · 고리), 그늘은 그림에 굽는다 (앱은 조명 없이 그림 색 그대로 그린다)
///  · 'Idle' 한 동작(2초 반복 — 숨쉬기 · 갸웃 · 꼬리 · 깜빡임)을 넣는다 — GLBModelLoader 가 Idle 이 든 동작을 골라 튼다
///  · 사진(대표 사진)을 주면 오른손에 팻말로 든다 — 장소의 얼굴 (투명한 사진이면 스티커 모양 그대로)
/// 나중에 외부 3D AI 를 붙이면 R0926AiWork 에서 이 자리만 바꾼다. 배경 스레드에서 돈다(Unity 그래픽 API 없음).
/// </summary>
public static class R0926AiCharacter
{
    public enum Kind { Dog, Cat, Rabbit, Bear, Panda, Fox, Pig, Chick, Penguin, Frog, Person, Robot,
                       Lion, Tiger, Mouse, Koala, Monkey, Sheep, Cow, Elephant, Dino, Owl, Raccoon, Horse }
    public enum Face { Smile, Happy, Angry, Sad, Sleepy, Surprised, Wink, Calm }
    public enum Move { Idle, Wave, Dance, Jump }
    public enum Hat { None, Beanie, Cap, Straw, Chef, Santa, Helmet, Wizard, Party }
    public enum Job { None, Farmer, Chef, Police, Firefighter, Doctor, Princess, Prince, Santa, Wizard }
    public enum Pattern { None, Spots, Stripes }
    [Flags] public enum Extra { None = 0, Crown = 1, Bow = 2, Glasses = 4, Sunglasses = 8, Scarf = 16, Flower = 32, Backpack = 64, Heart = 128, Star = 256, Tie = 512, Buns = 1024,
                                Wings = 2048, Horn = 4096, Horns = 8192, Halo = 16384, Cape = 32768, Shirt = 65536, Pants = 131072, Bell = 262144 }

    public sealed class Spec
    {
        public Kind Kind;
        public bool KindGiven;
        public string Sub = "";      // 같은 꼴 안의 갈래 — hamster · squirrel · bat · unicorn · dragon · polar · black · bull · leopard · duck · swan · parrot · sparrow · pigeon · magpie · eagle
        public string KindWord;      // 요청에 쓰인 이름 그대로 ('푸들' · '오리') — 안내에 그대로
        public string RoleWord;      // '학생' · '마녀' · '셰프' — 안내에 그대로
        public Job Job;
        public Color Main, Second, Detail, Accent;   // Accent — 갈기 · 등 가시 · 눈가 탈 · 꼬리 술
        public bool MainGiven;
        public string MainName;
        public Color Skin = new Color(1f, 0.85f, 0.74f), Hair = new Color(0.26f, 0.17f, 0.11f);
        public Face Face = Face.Smile;
        public bool FaceGiven;
        public Move Move = Move.Idle;
        public Hat Hat;
        public bool HatGiven;        // 모자를 직접 적었다 (직업에 딸린 모자는 안내에 안 적는다)
        public Color HatColor;
        public string HatColorName;
        public Extra Extras, AutoExtras;   // AutoExtras — 동물에 딸려 온 것 (젖소 뿔 · 용 날개) · 안내에는 안 적는다
        public readonly Dictionary<Extra, Color> ExtraColor = new Dictionary<Extra, Color>();
        public readonly Dictionary<Extra, string> ExtraName = new Dictionary<Extra, string>();
        public Pattern Pattern;
        public bool Rainbow, Dress;
        public float Size = 1f, Chub = 1f;
        public bool Baby;
        public int Seed;
        public string Label = "";
        public readonly List<string> Unknown = new List<string>();   // 알아듣지 못한 낱말 — 안내에 그대로 알린다 (11-14)
    }

    // ── 요청사항 읽기 ──────────────────────────────────────
    // 11-14 대표님 「이런저런 내용을 요청사항을 했음에도 … 번갈아가면서 해당 요청사항에 있어서 반응하지 않고」 —
    // 모르는 동물이면 아무 동물이나 차례마다 바꿔 골랐고(번갈아), 모르는 낱말은 말없이 버렸다.
    // 이제: 동물 24 갈래 · 낱말을 크게 늘리고, 같은 글이면 늘 같은 동물, 못 알아들은 낱말은 안내에 그대로 적는다.
    private struct Hit { public int Start, End; public Color C; public string Name; }

    // 무엇 — 둘 이상 나오면 마지막에 나온 이름 (우리말은 꾸밈말이 앞에 온다 — '고양이 모자 쓴 강아지' 는 강아지)
    private static readonly (Kind kind, string sub, string[] words)[] KindWords =
    {
        (Kind.Frog, "", new[] { "개구리", "frog" }),
        (Kind.Panda, "", new[] { "판다", "팬더", "panda" }),
        (Kind.Penguin, "", new[] { "펭귄", "penguin" }),
        (Kind.Owl, "", new[] { "부엉이", "올빼미", "owl" }),
        (Kind.Chick, "", new[] { "병아리", "닭", "짹짹", "chick", "bird", "chicken" }),
        (Kind.Chick, "duck", new[] { "오리", "꽥꽥", "duck" }),
        (Kind.Chick, "swan", new[] { "거위", "백조", "goose", "swan" }),
        (Kind.Chick, "parrot", new[] { "앵무새", "앵무", "parrot" }),
        (Kind.Chick, "sparrow", new[] { "참새", "제비", "sparrow" }),
        (Kind.Chick, "pigeon", new[] { "비둘기", "갈매기", "pigeon", "dove" }),
        (Kind.Chick, "magpie", new[] { "까치", "까마귀", "magpie", "crow" }),
        (Kind.Chick, "eagle", new[] { "독수리", "eagle", "hawk" }),
        (Kind.Rabbit, "", new[] { "토끼", "바니", "rabbit", "bunny" }),
        (Kind.Fox, "", new[] { "여우", "fox" }),
        (Kind.Raccoon, "", new[] { "너구리", "라쿤", "raccoon" }),
        (Kind.Pig, "", new[] { "돼지", "꿀꿀", "pig", "piggy" }),
        (Kind.Cat, "", new[] { "고양이", "냥이", "야옹", "키티", "cat", "kitty", "kitten" }),
        (Kind.Tiger, "", new[] { "호랑이", "tiger" }),
        (Kind.Tiger, "leopard", new[] { "표범", "치타", "leopard", "cheetah" }),
        (Kind.Lion, "", new[] { "사자", "lion" }),
        (Kind.Bear, "", new[] { "곰", "테디", "bear", "teddy" }),
        (Kind.Bear, "polar", new[] { "북극곰", "polar bear" }),
        (Kind.Bear, "black", new[] { "반달곰", "반달가슴곰", "흑곰" }),
        (Kind.Koala, "", new[] { "코알라", "koala" }),
        (Kind.Monkey, "", new[] { "원숭이", "몽키", "고릴라", "침팬지", "monkey", "gorilla", "chimp" }),
        (Kind.Mouse, "", new[] { "생쥐", "들쥐", "mouse", "rat" }),
        (Kind.Mouse, "hamster", new[] { "햄스터", "햄찌", "hamster" }),
        (Kind.Mouse, "squirrel", new[] { "다람쥐", "청설모", "squirrel" }),
        (Kind.Mouse, "bat", new[] { "박쥐", "bat" }),
        (Kind.Sheep, "", new[] { "어린양", "아기양", "알파카", "sheep", "lamb", "alpaca" }),
        (Kind.Cow, "", new[] { "젖소", "얼룩소", "송아지", "cow", "calf" }),
        (Kind.Cow, "bull", new[] { "황소", "한우", "bull", "ox" }),
        (Kind.Elephant, "", new[] { "코끼리", "elephant" }),
        (Kind.Dino, "", new[] { "공룡", "티라노", "다이노", "dinosaur", "dino", "t-rex", "trex" }),
        (Kind.Dino, "dragon", new[] { "드래곤", "청룡", "흑룡", "황룡", "백룡", "dragon" }),
        (Kind.Horse, "", new[] { "조랑말", "망아지", "포니", "horse", "pony" }),
        (Kind.Horse, "unicorn", new[] { "유니콘", "unicorn" }),
        (Kind.Dog, "", new[] { "강아지", "멍멍", "댕댕", "퍼피", "푸들", "시바", "리트리버", "말티즈", "비숑", "코기", "진돗개", "마스티프", "달마시안", "허스키", "치와와", "포메", "dog", "puppy", "doggy" }),
        (Kind.Robot, "", new[] { "로봇", "로보트", "안드로이드", "robot" }),
        (Kind.Person, "", new[] { "사람", "소년", "소녀", "남자", "여자", "어린이", "할머니", "할아버지", "엄마", "아빠", "아저씨", "아줌마", "언니", "오빠", "누나",
                                  "person", "boy", "girl", "man", "woman", "kid", "human", "grandma", "grandpa" }),
    };

    // 한 글자 이름 — 다른 낱말 속('고양이'의 양 · '소방관'의 소 · '모양'의 양 · '말티즈'의 말)은 빼고, 따로 쓰였거나 토씨가 붙은 것만
    private static Regex ShortWord(string w)
    {
        string before = "(?:(?<![가-힣])|(?<=아기|꼬마|새끼|흰|하얀|검은|까만|숫|암))";
        if (w == "개") before = "(?<![0-9한두세네몇]\\s?)" + before;   // '모자 두 개' 의 개는 강아지가 아니다
        string after = "(?=$|[^가-힣]|[이가을를은는야랑와과의도]|처럼|모양|캐릭터|인형|한테|에게)";
        if (w == "새") after = "(?=$|[^가-힣\\s]|[이가을를은는야랑와과의도]|처럼|모양|캐릭터|인형|한테|에게|\\s+(?:캐릭터|인형|모양|한\\s?마리|$))";   // '새 옷' 의 새는 새가 아니다
        if (w == "말") after = "(?!랑)" + after;   // '말랑말랑'
        return new Regex(before + w + after, RegexOptions.CultureInvariant);
    }

    private static readonly (Kind kind, string sub, Regex re)[] ShortKinds =
    {
        (Kind.Dog, "", ShortWord("개")), (Kind.Chick, "", ShortWord("새")), (Kind.Sheep, "", ShortWord("양")), (Kind.Cow, "", ShortWord("소")),
        (Kind.Horse, "", ShortWord("말")), (Kind.Mouse, "", ShortWord("쥐")), (Kind.Dino, "dragon", ShortWord("용")), (Kind.Tiger, "", ShortWord("범")),
    };

    // 사람 일 — 동물 이름이 함께 오면 그 동물이 그 옷을 입는다 ('농부 강아지' · '강아지 경찰')
    private static readonly string[] RoleWords =
    {
        "농부", "농민", "요리사", "셰프", "쉐프", "의사", "간호사", "경찰", "소방관", "공주", "여왕", "왕자", "임금", "산타", "마법사", "마녀", "군인", "선생님", "회사원", "직장인", "학생", "남학생", "여학생",
        "farmer", "chef", "police", "doctor", "nurse", "princess", "queen", "prince", "king", "santa", "wizard", "witch", "soldier", "teacher", "student",
    };

    private static readonly Regex StarRe = new Regex("(^|[^가-힣])별", RegexOptions.CultureInvariant);
    private static readonly Regex HornRe = new Regex("뿔(?![난났나])", RegexOptions.CultureInvariant);   // '뿔난 고양이' 는 화난 고양이
    private static readonly Regex BellRe = new Regex("(?<!물)방울", RegexOptions.CultureInvariant);   // '물방울무늬' 는 방울이 아니다

    private static readonly (string[] words, Color c, string name)[] Colors =
    {
        // 겹치는 말은 긴 것이 먼저 ('연분홍' 이 '분홍' 보다)
        (new[] { "하늘색", "하늘빛", "sky blue", "skyblue" }, new Color(0.53f, 0.80f, 0.97f), "하늘색"),
        (new[] { "연분홍", "연핑크", "벚꽃", "light pink" }, new Color(1.00f, 0.80f, 0.86f), "연분홍"),
        (new[] { "연보라", "라벤더", "lavender" }, new Color(0.78f, 0.68f, 0.95f), "연보라"),
        (new[] { "청록", "에메랄드", "터키석", "teal", "turquoise", "emerald" }, new Color(0.16f, 0.68f, 0.66f), "청록색"),
        (new[] { "남색", "네이비", "navy" }, new Color(0.17f, 0.22f, 0.45f), "남색"),
        (new[] { "연두", "lime" }, new Color(0.62f, 0.86f, 0.32f), "연두색"),
        (new[] { "민트", "mint" }, new Color(0.56f, 0.90f, 0.80f), "민트색"),
        (new[] { "살구", "피치", "코랄", "peach", "coral" }, new Color(1.00f, 0.66f, 0.52f), "살구색"),
        (new[] { "와인", "버건디", "burgundy", "wine" }, new Color(0.55f, 0.13f, 0.22f), "와인색"),
        (new[] { "카키", "올리브", "khaki", "olive" }, new Color(0.50f, 0.52f, 0.30f), "카키색"),
        (new[] { "아이보리", "ivory" }, new Color(1.00f, 0.97f, 0.88f), "아이보리"),
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

    private static readonly Color[] RainbowPal =
    {
        new Color(1f, 0.55f, 0.62f), new Color(1f, 0.72f, 0.42f), new Color(1f, 0.9f, 0.42f),
        new Color(0.52f, 0.88f, 0.6f), new Color(0.5f, 0.75f, 1f), new Color(0.72f, 0.58f, 1f),
    };

    private static readonly Color[] Pal5 = { new Color(0.95f, 0.45f, 0.45f), new Color(0.38f, 0.62f, 0.95f), new Color(1f, 0.8f, 0.3f), new Color(0.5f, 0.8f, 0.55f), new Color(0.7f, 0.55f, 0.92f) };

    private static readonly Kind[] Defaults = { Kind.Dog, Kind.Cat, Kind.Bear, Kind.Rabbit, Kind.Panda, Kind.Fox, Kind.Chick, Kind.Penguin, Kind.Pig, Kind.Frog };
    private static readonly string[] KindNames = { "강아지", "고양이", "토끼", "곰", "판다", "여우", "돼지", "병아리", "펭귄", "개구리", "사람", "로봇",
                                                   "사자", "호랑이", "생쥐", "코알라", "원숭이", "양", "젖소", "코끼리", "공룡", "부엉이", "너구리", "말" };
    private static readonly Dictionary<string, string> SubNames = new Dictionary<string, string>
    {
        { "hamster", "햄스터" }, { "squirrel", "다람쥐" }, { "bat", "박쥐" }, { "unicorn", "유니콘" }, { "dragon", "용" }, { "polar", "북극곰" }, { "black", "반달곰" },
        { "bull", "황소" }, { "leopard", "표범" }, { "duck", "오리" }, { "swan", "백조" }, { "parrot", "앵무새" }, { "sparrow", "참새" }, { "pigeon", "비둘기" }, { "magpie", "까치" }, { "eagle", "독수리" },
    };
    // 소리 · 애칭은 안내에 그대로 쓰지 않는다 ('멍멍' → 강아지)
    private static readonly HashSet<string> NickWords = new HashSet<string> { "멍멍", "댕댕", "꿀꿀", "짹짹", "꽥꽥", "야옹", "냥이", "퍼피", "바니", "테디", "몽키", "햄찌", "다이노", "앵무", "로보트" };
    private static readonly string[] JobNames = { "", "농부", "요리사", "경찰", "소방관", "의사", "공주", "왕자", "산타", "마법사" };

    // 모르는 말로 치지 않는 낱말 — 꾸밈말 · '만들어줘' · 토씨 같은 것 (11-14)
    private static readonly HashSet<string> Filler = new HashSet<string>
    {
        "귀여운", "귀엽게", "귀엽고", "귀여워", "귀요미", "예쁜", "예쁘게", "이쁜", "이쁘게", "멋진", "멋있는", "멋지게", "사랑스러운", "깜찍한", "앙증맞은", "작고", "착한", "순한",
        "씩씩한", "용감한", "똑똑한", "친절한", "웃긴", "재밌는", "재미있는", "만들어줘", "만들어", "만들어주세요", "만들어줄래", "만들기", "만든", "해줘", "해주세요", "주세요", "줘",
        "부탁해", "부탁", "부탁해요", "원해", "원해요", "하는", "하고", "있는", "있고", "있어", "쓴", "쓰고", "쓰고있는", "입은", "입고", "입고있는", "든", "들고", "들고있는", "낀", "끼고",
        "끼고있는", "신은", "신고", "맨", "매고", "두른", "두르고", "단", "달고", "달린", "붙은", "가진", "가지고", "한", "하나", "한마리", "마리", "명", "캐릭터", "모양", "모습", "느낌",
        "스타일", "버전", "같은", "처럼", "닮은", "모델", "인형", "피규어", "그리고", "또", "및", "랑", "이랑", "하게", "아주", "엄청", "진짜", "완전", "너무", "좀", "조금", "살짝", "약간",
        "많이", "더", "제일", "가장", "색", "색깔", "컬러", "색상", "빛", "빛깔", "옷차림", "차림", "복장", "얼굴", "표정", "눈", "손", "발", "몸", "귀", "꼬리", "털", "그림", "사진",
        "우리", "내", "나의", "저", "제", "이", "그", "저런", "이런", "정말", "같이", "함께", "동물", "친구", "포즈", "자세", "짜리", "형태", "디자인", "3d", "glb", "있게", "보이게",
        "그려줘", "그려", "그려주세요", "생성", "생성해", "생성해줘", "생성해주세요", "만들어봐", "만들어볼래", "보여줘", "해봐", "특별한", "독특한", "새로운", "평범한", "기본",
        "안은", "안고", "타고", "탄", "먹는", "먹고", "보는", "보고", "앉은", "앉아", "앉아있는", "서있는", "서", "누운", "누워", "날고", "나는", "날아가는", "뛰고", "달리는", "걷는",
        "노는", "놀고", "부는", "치는", "켜는", "되는", "된", "생긴", "닮게", "비슷한", "느낌의", "분위기", "컨셉", "콘셉트",
        // 11-14 대표님 예문 「첨부한 사진을 녹색 캐릭터로 만드는데 신나서 웃고 있는 느낌으로 만들어줘」 같은 말
        "첨부", "첨부한", "첨부된", "첨부해", "올린", "보낸", "만드는데", "만들되", "만들고", "만들어서", "바꿔", "바꿔줘", "바꿔서", "변신", "되게",
        "이걸", "이거", "그걸", "저걸", "얘", "얘를", "느낌나게", "느낌나는", "멘", "메고",
        "a", "an", "the", "and", "with", "in", "on", "of", "to", "for", "is", "are", "wearing", "holding", "has", "have", "cute", "lovely", "pretty", "nice", "character",
        "model", "make", "please", "very", "so", "style", "version", "figure", "doll", "one", "my", "some",
    };
    private static readonly string[] Tails = { "스러운", "이랑", "에게", "한테", "처럼", "같은", "으로", "하고", "하게", "을", "를", "이", "가", "은", "는", "의", "와", "과", "도", "에", "로", "랑", "요", "한" };

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

    private static uint Fnv(string x)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in x) { h ^= c; h *= 16777619; }
            return h;
        }
    }

    // ── 빈 요청 = 정말 무작위 (11-14 대표님 「진짜 랜덤으로 … 매번 순서대로 나오지 않고」) ──
    // 예전엔 빈 요청이면 정해진 열 마리를 씨앗 차례대로 골라 매번 같은 차례로 나왔다.
    // 이제 켤 때마다 다른 소금으로 섞은 주사위 — 서른넷 갈래 중 최근 넷은 피하고, 가끔 표정 · 움직임 · 모자 · 소품 · 일 · 몸집 · 색을 붙인다.
    // 낱말은 모두 Parse 가 알아듣는 말만 (안내에 '아직 모르는 말' 이 뜨지 않게). 몸 색은 늘 동물 이름 바로 앞 — 소품 색으로 붙지 않게
    private static readonly System.Random Luck = new System.Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
    private static readonly List<string> Recent = new List<string>();
    private static readonly string[] LuckKinds =
    {
        "강아지", "고양이", "토끼", "곰", "판다", "여우", "돼지", "병아리", "펭귄", "개구리", "로봇", "사자", "호랑이", "생쥐", "코알라", "원숭이", "양",
        "젖소", "코끼리", "공룡", "부엉이", "너구리", "조랑말", "햄스터", "다람쥐", "유니콘", "용", "북극곰", "오리", "앵무새", "표범", "소녀", "소년", "박쥐",
    };
    private static readonly string[] LuckFaces = { "신나서 웃는", "활짝 웃는", "윙크하는", "졸린", "깜짝 놀란", "시크한", "행복한", "삐진" };
    private static readonly string[] LuckMoves = { "춤추는", "손 흔드는", "점프하는", "폴짝 뛰는", "인사하는" };
    private static readonly string[] LuckHats = { "털모자", "밀짚모자", "야구모자", "고깔모자", "헬멧" };
    private static readonly (string word, string verb, bool paint)[] LuckExtras =
    {
        ("안경", "쓴", false), ("선글라스", "낀", false), ("리본", "단", true), ("목도리", "두른", true), ("꽃", "든", true), ("가방", "든", true),
        ("넥타이", "맨", true), ("하트", "든", false), ("날개", "단", false), ("망토", "두른", true), ("왕관", "쓴", false), ("별", "든", false),
    };
    private static readonly string[] LuckJobs = { "산타", "요리사", "농부", "경찰", "소방관", "의사", "마법사", "공주", "왕자" };
    private static readonly string[] LuckSizes = { "아기", "통통한", "커다란" };
    private static readonly string[] LuckPaints = { "빨간", "주황", "노란", "초록", "파란", "보라", "분홍", "하늘색", "연분홍", "연보라", "민트색", "살구색", "하얀", "검은", "회색", "금색", "갈색", "연두색", "청록색" };
    private const int RecentKeep = 4;

    /// <summary>요청사항이 빈 3D 캐릭터 — 부를 때마다 새로 섞은 요청 한 줄 (예: '윙크하는 빨간 털모자 쓴 펭귄')</summary>
    public static string RandomRequest()
    {
        string P(string[] a) => a[Luck.Next(a.Length)];
        bool Roll(double p) => Luck.NextDouble() < p;
        var pool = new List<string>();
        foreach (var k in LuckKinds) if (!Recent.Contains(k)) pool.Add(k);
        string kind = pool[Luck.Next(pool.Count)];
        Recent.Add(kind);
        while (Recent.Count > RecentKeep) Recent.RemoveAt(0);

        var parts = new List<string>();
        if (Roll(0.3)) parts.Add(P(LuckFaces));
        if (Roll(0.35)) parts.Add(P(LuckMoves));
        if (Roll(0.3)) parts.Add((Roll(0.5) ? P(LuckPaints) + " " : "") + P(LuckHats) + " 쓴");
        if (Roll(0.35))
        {
            var e = LuckExtras[Luck.Next(LuckExtras.Length)];
            parts.Add((e.paint && Roll(0.5) ? P(LuckPaints) + " " : "") + e.word + " " + e.verb);
        }
        bool person = kind == "소녀" || kind == "소년";
        if (Roll(person ? 0.3 : 0.12)) parts.Add(P(LuckJobs));
        if (Roll(0.15)) parts.Add(P(LuckSizes));
        if (Roll(0.3)) parts.Add(Roll(0.08) ? "무지개색" : P(LuckPaints));
        parts.Add(kind);
        return string.Join(" ", parts);
    }

    public static Spec Parse(string request, int seed)
    {
        string t = (request ?? "").Trim().ToLowerInvariant();
        var s = new Spec { Seed = seed };
        var rnd = new System.Random(seed);
        var seen = new List<(int s, int e)>();   // 알아들은 자리 — 남은 낱말은 '아직 모르는 말'
        bool A(params string[] words)
        {
            bool hit = false;
            foreach (var w in words) foreach (var h in FindAll(t, w)) { seen.Add(h); hit = true; }
            return hit;
        }
        int F(params string[] words)
        {
            int best = -1;
            foreach (var w in words) foreach (var h in FindAll(t, w)) { seen.Add(h); if (best < 0 || h.s < best) best = h.s; }
            return best;
        }
        int RF(Regex re)
        {
            int best = -1;
            foreach (Match m in re.Matches(t)) { seen.Add((m.Index, m.Index + m.Length)); if (best < 0) best = m.Index + m.Length - 1; }
            return best;
        }
        T? Pick<T>(params (T v, string[] w)[] opts) where T : struct
        {
            T? r = null;
            foreach (var o in opts) if (A(o.w) && !r.HasValue) r = o.v;
            return r;
        }

        // 무엇
        int bestEnd = -1, bestLen = 0;
        bool panda = false, unicorn = false;
        void Cand(Kind kind, string sub, int st, int en)
        {
            seen.Add((st, en));
            if (kind == Kind.Panda) panda = true;
            if (sub == "unicorn") unicorn = true;
            if (en < bestEnd || (en == bestEnd && en - st <= bestLen)) return;
            bestEnd = en; bestLen = en - st;
            s.Kind = kind; s.Sub = sub; s.KindGiven = true;
            s.KindWord = t.Substring(st, en - st);
        }
        foreach (var (kind, sub, words) in KindWords)
            foreach (var w in words)
                foreach (var h in FindAll(t, w)) Cand(kind, sub, h.s, h.e);
        foreach (var (kind, sub, re) in ShortKinds)
            foreach (Match m in re.Matches(t)) Cand(kind, sub, m.Index, m.Index + m.Length);
        if (s.KindGiven && s.Kind == Kind.Bear && s.Sub == "" && panda) { s.Kind = Kind.Panda; s.KindWord = null; }   // '판다곰'
        {
            string role = null; int roleEnd = -1;
            foreach (var w in RoleWords)
                foreach (var h in FindAll(t, w))
                {
                    seen.Add(h);
                    if (h.e > roleEnd || (h.e == roleEnd && w.Length > role.Length)) { roleEnd = h.e; role = w; }
                }
            if (role != null && role[0] >= '가' && role[0] <= '힣') s.RoleWord = role;
            if (role != null && !s.KindGiven) { s.Kind = Kind.Person; s.KindGiven = true; }
        }
        // 무엇인지 없으면 — 빈 요청은 차례마다 다른 동물, 글이 있으면 그 글에 늘 같은 동물 (다시 만들어도 동물은 그대로 — 11-14)
        if (!s.KindGiven) s.Kind = Defaults[(int)((t.Length == 0 ? (uint)rnd.Next() : Fnv(t)) % (uint)Defaults.Length)];
        switch (s.Kind)   // 동물에 딸려 오는 것
        {
            case Kind.Cow: s.Extras |= Extra.Horns | (s.Sub == "bull" ? Extra.None : Extra.Bell); break;
            case Kind.Dino: if (s.Sub == "dragon") s.Extras |= Extra.Horns | Extra.Wings; break;
            case Kind.Mouse: if (s.Sub == "bat") s.Extras |= Extra.Wings; break;
        }
        s.AutoExtras = s.Extras;
        bool oneHorn = unicorn || A("외뿔");
        if (oneHorn)   // 유니콘 — 말이면 유니콘, 다른 동물이면 그 동물 이마에 뿔 하나
        {
            s.Extras |= Extra.Horn;
            if (s.Kind == Kind.Horse)
            {
                s.AutoExtras |= Extra.Horn;
                if (s.Sub != "unicorn") { s.Sub = "unicorn"; s.KindWord = null; }
            }
        }

        // 직업 — 옷 · 모자
        Job? job = Pick(
            (Job.Santa, new[] { "산타", "크리스마스", "santa", "christmas" }),
            (Job.Farmer, new[] { "농부", "농민", "농사", "farmer" }),
            (Job.Chef, new[] { "요리사", "셰프", "쉐프", "chef", "cook" }),
            (Job.Firefighter, new[] { "소방관", "소방", "firefighter" }),
            (Job.Police, new[] { "경찰", "police" }),
            (Job.Doctor, new[] { "의사", "간호사", "doctor", "nurse" }),
            (Job.Wizard, new[] { "마법사", "마녀", "위저드", "wizard", "witch" }),
            (Job.Princess, new[] { "공주", "여왕", "princess", "queen" }),
            (Job.Prince, new[] { "왕자", "임금", "prince", "king" }));
        if (job.HasValue) s.Job = job.Value;

        // 색 (나온 자리와 함께)
        var hits = new List<Hit>();
        foreach (var (words, c, name) in Colors)
            foreach (var w in words)
                foreach (var h in FindAll(t, w))
                {
                    seen.Add(h);
                    if (!hits.Exists(x => x.Start < h.e && h.s < x.End)) hits.Add(new Hit { Start = h.s, End = h.e, C = c, Name = name });
                }
        if (A("금발", "blonde")) s.Hair = new Color(0.98f, 0.82f, 0.38f);
        if (A("무지개", "rainbow")) s.Rainbow = true;
        hits.Sort((a, b) => a.Start.CompareTo(b.Start));
        var used = new bool[hits.Count];
        // 바로 앞(일곱 글자 안)의 색은 그 소품 · 머리카락 · 무늬 · 옷의 색
        int Bind(int at)
        {
            int best = -1;
            for (int i = 0; i < hits.Count; i++)
                if (!used[i] && hits[i].End <= at && at - hits[i].End <= 7 && (best < 0 || hits[i].End > hits[best].End)) best = i;
            if (best >= 0) used[best] = true;
            return best;
        }

        // 모자 — 모양은 낱말(밀짚 · 헬멧 · 고깔 · 캡)이 먼저, 없으면 직업, 그것도 없으면 '모자' 한 마디에 털모자
        int hatAt = F("모자", "비니", "털모자", "캡", "야구모자", "밀짚", "헬멧", "안전모", "고깔", "hat", "cap", "beanie", "helmet");
        bool straw = A("밀짚", "straw"), helmet = A("헬멧", "안전모", "helmet"), party = A("고깔", "생일", "파티", "party", "birthday"), cap = A("캡", "야구모자", "cap");
        bool soldier = A("군인", "soldier", "army");
        Hat style = straw ? Hat.Straw
                  : helmet ? Hat.Helmet
                  : party ? Hat.Party
                  : cap ? Hat.Cap
                  : s.Job == Job.Santa ? Hat.Santa
                  : s.Job == Job.Chef ? Hat.Chef
                  : s.Job == Job.Farmer ? Hat.Straw
                  : s.Job == Job.Police ? Hat.Cap
                  : s.Job == Job.Firefighter ? Hat.Helmet
                  : s.Job == Job.Wizard ? Hat.Wizard
                  : soldier ? Hat.Helmet
                  : hatAt >= 0 ? Hat.Beanie : Hat.None;
        if (style != Hat.None)
        {
            s.Hat = style;
            s.HatGiven = hatAt >= 0 || straw || helmet || party || cap;
            int hi = hatAt >= 0 ? Bind(hatAt) : -1;
            s.HatColor = hi >= 0 ? hits[hi].C : soldier && style == Hat.Helmet ? new Color(0.42f, 0.47f, 0.3f) : HatDefault(style, s.Job, rnd);
            if (hi >= 0) s.HatColorName = hits[hi].Name;
        }
        if (s.Job == Job.Princess || s.Job == Job.Prince) s.Extras |= Extra.Crown;

        // 소품
        void Set(Extra e, int at, Color def)
        {
            s.Extras |= e;
            s.AutoExtras &= ~e;
            int hi = at >= 0 ? Bind(at) : -1;
            s.ExtraColor[e] = hi >= 0 ? hits[hi].C : def;
            if (hi >= 0) s.ExtraName[e] = hits[hi].Name;
        }
        void Ex(Extra e, Color def, params string[] words)
        {
            int at = F(words);
            if (at >= 0) Set(e, at, def);
        }
        Ex(Extra.Sunglasses, new Color(0.1f, 0.1f, 0.12f), "선글라스", "썬글라스", "sunglasses");
        if ((s.Extras & Extra.Sunglasses) == 0) Ex(Extra.Glasses, new Color(0.16f, 0.13f, 0.12f), "안경", "glasses");
        else A("안경", "glasses");
        Ex(Extra.Crown, new Color(1f, 0.8f, 0.26f), "왕관", "티아라", "crown", "tiara");
        Ex(Extra.Bow, new Color(1f, 0.42f, 0.58f), "리본", "머리핀", "머리띠", "bow", "ribbon");
        Ex(Extra.Scarf, new Color(0.9f, 0.25f, 0.25f), "목도리", "스카프", "머플러", "scarf", "muffler");
        Ex(Extra.Flower, new Color(1f, 0.55f, 0.7f), "꽃", "flower");
        Ex(Extra.Backpack, new Color(0.95f, 0.6f, 0.2f), "가방", "배낭", "백팩", "책가방", "backpack", "bag");
        Ex(Extra.Tie, new Color(0.85f, 0.2f, 0.25f), "넥타이", "necktie", "tie");
        Ex(Extra.Heart, new Color(1f, 0.35f, 0.45f), "하트", "사랑", "heart", "love");
        Ex(Extra.Wings, new Color(0.98f, 0.98f, 1f), "날개", "천사", "엔젤", "wings", "wing", "angel");
        Ex(Extra.Halo, new Color(1f, 0.84f, 0.35f), "천사", "엔젤", "후광", "angel", "halo");
        Ex(Extra.Cape, new Color(0.9f, 0.2f, 0.24f), "망토", "케이프", "히어로", "슈퍼히어로", "cape", "hero");
        {
            int bellAt = RF(BellRe);
            if (bellAt < 0) bellAt = F("bell");
            if (bellAt >= 0) Set(Extra.Bell, bellAt, new Color(1f, 0.8f, 0.3f));
        }
        {
            int hornAt = RF(HornRe);
            bool devil = A("악마", "데빌", "devil");
            int hornEn = F("horns", "horn");
            if (!oneHorn && (hornAt >= 0 || devil || hornEn >= 0))
            {
                Set(Extra.Horns, hornAt >= 0 ? hornAt : hornEn, devil ? new Color(0.88f, 0.18f, 0.2f) : new Color(0.97f, 0.91f, 0.78f));
                if (devil && !s.ExtraName.ContainsKey(Extra.Horns)) s.ExtraName[Extra.Horns] = "악마";   // 안내 '악마 뿔'
            }
            else if (oneHorn && hornAt >= 0)   // '은색 뿔 유니콘'
            {
                int hi = Bind(hornAt);
                if (hi >= 0) { s.ExtraColor[Extra.Horn] = hits[hi].C; s.ExtraName[Extra.Horn] = hits[hi].Name; }
            }
        }
        if (F("원피스", "드레스", "한복", "dress") >= 0) s.Dress = true;
        Ex(Extra.Shirt, Pal5[rnd.Next(Pal5.Length)], "옷", "티셔츠", "셔츠", "원피스", "드레스", "한복", "조끼", "후드", "점퍼", "자켓", "재킷", "코트", "스웨터", "니트", "유니폼",
            "shirt", "dress", "hoodie", "jacket", "coat", "sweater", "uniform");
        Ex(Extra.Pants, A("청바지", "jeans") ? new Color(0.3f, 0.45f, 0.72f) : new Color(0.2f, 0.25f, 0.42f), "바지", "청바지", "반바지", "치마", "스커트", "pants", "jeans", "skirt", "shorts");
        {
            int starAt = RF(StarRe);
            if (starAt < 0) starAt = F("star");
            if (starAt >= 0) Set(Extra.Star, starAt, new Color(1f, 0.82f, 0.25f));
        }
        if (s.Extras.HasFlag(Extra.Crown) && !s.ExtraColor.ContainsKey(Extra.Crown)) s.ExtraColor[Extra.Crown] = new Color(1f, 0.8f, 0.26f);
        // 직업 같은 말 — 학생은 가방, 회사원은 넥타이, 선생님은 안경 (딸려 온 것이라 안내에는 안 적는다)
        void Implied(Extra e, Color c) { Set(e, -1, c); s.AutoExtras |= e; }
        if (A("학생", "student", "학교", "등교") && !s.Extras.HasFlag(Extra.Backpack)) Implied(Extra.Backpack, new Color(0.95f, 0.6f, 0.2f));
        if (A("회사원", "직장인", "사장님", "비즈니스", "business") && !s.Extras.HasFlag(Extra.Tie)) Implied(Extra.Tie, new Color(0.85f, 0.2f, 0.25f));
        if (A("선생님", "교수", "박사", "teacher", "professor") && (s.Extras & (Extra.Glasses | Extra.Sunglasses)) == 0) Implied(Extra.Glasses, new Color(0.16f, 0.13f, 0.12f));

        // 머리카락 색 · 무늬 색
        int hairAt = F("머리카락", "머리", "헤어", "hair");
        if (hairAt >= 0) { int hi = Bind(hairAt); if (hi >= 0) s.Hair = hits[hi].C; }
        bool spots = A("점박이", "얼룩", "땡땡이", "물방울무늬", "달마시안", "호피", "spot", "spots", "dotted", "dalmatian");
        bool stripes = A("줄무늬", "호랑이무늬", "스트라이프", "stripe", "stripes", "striped");
        if (spots) s.Pattern = Pattern.Spots;
        else if (stripes) s.Pattern = Pattern.Stripes;
        if (s.Pattern != Pattern.None)
        {
            int hi = Bind(F("점박이", "얼룩", "땡땡이", "무늬", "호피", "spot", "stripe", "dalmatian"));
            s.Detail = hi >= 0 ? hits[hi].C : new Color(0.2f, 0.17f, 0.16f);
        }

        // 남은 색 — 처음 것은 몸(털 · 사람은 옷), 다음은 배 · 바지
        var free = new List<Hit>();
        for (int i = 0; i < hits.Count; i++) if (!used[i]) free.Add(hits[i]);
        SpeciesColors(s, rnd);
        if (free.Count > 0) { s.Main = free[0].C; s.MainGiven = true; s.MainName = free[0].Name; }
        if (free.Count > 1) s.Second = free[1].C;
        if (A("달마시안", "dalmatian") && !s.MainGiven) { s.Main = new Color(0.97f, 0.97f, 0.96f); s.Detail = new Color(0.12f, 0.12f, 0.13f); }
        if (A("할머니", "할아버지", "grandma", "grandpa")) s.Hair = new Color(0.82f, 0.82f, 0.84f);
        if (s.Kind == Kind.Person)
        {
            if (A("소녀", "여자", "공주", "엄마", "할머니", "여학생", "언니", "누나", "아줌마", "girl", "woman", "princess", "mom", "grandma")) s.Extras |= Extra.Buns;
            if (s.Extras.HasFlag(Extra.Shirt)) s.Main = s.ExtraColor[Extra.Shirt];   // 사람은 옷이 곧 몸 색
            if (s.Extras.HasFlag(Extra.Pants)) s.Second = s.ExtraColor[Extra.Pants];
            if (s.Dress) s.Second = s.Main;
        }
        // 딸려 온 소품의 색
        void Def(Extra e, Color c) { if (s.Extras.HasFlag(e) && !s.ExtraColor.ContainsKey(e)) s.ExtraColor[e] = c; }
        Def(Extra.Horns, s.Kind == Kind.Dino ? Light2(s.Accent, 0.35f) : new Color(0.97f, 0.91f, 0.78f));
        Def(Extra.Wings, s.Kind == Kind.Dino ? s.Accent : s.Kind == Kind.Mouse ? Dark(s.Main, 0.8f) : new Color(0.98f, 0.98f, 1f));
        Def(Extra.Horn, new Color(1f, 0.84f, 0.42f));
        Def(Extra.Bell, new Color(1f, 0.8f, 0.3f));

        // 표정 · 움직임 · 몸집
        Face? face = Pick(
            (Face.Wink, new[] { "윙크", "wink" }),
            (Face.Angry, new[] { "화난", "화가", "뿔난", "뿔났", "성난", "삐진", "angry", "mad" }),
            (Face.Sad, new[] { "슬픈", "우는", "눈물", "울보", "sad", "cry", "crying" }),
            (Face.Sleepy, new[] { "졸린", "자는", "잠자", "꿀잠", "졸려", "sleepy", "sleeping" }),
            (Face.Surprised, new[] { "놀란", "깜짝", "surprised", "shocked" }),
            (Face.Calm, new[] { "시크", "무표정", "차분", "cool", "calm" }),
            (Face.Happy, new[] { "신나", "행복", "기쁜", "기뻐", "활짝", "웃는", "웃고", "웃음", "미소", "방긋", "happy", "excited", "joy", "smile", "smiling", "laughing" }));
        if (face.HasValue) { s.Face = face.Value; s.FaceGiven = true; }

        Move? move = Pick(
            (Move.Dance, new[] { "춤", "댄스", "dance", "dancing" }),
            (Move.Jump, new[] { "점프", "뛰는", "뛰어", "폴짝", "깡총", "깡충", "jump", "jumping", "hop" }),
            (Move.Wave, new[] { "손 흔", "손흔", "인사", "안녕", "반가", "wave", "waving", "hello", "hi", "bye" }));
        if (move.HasValue) s.Move = move.Value;

        if (A("아기", "꼬마", "새끼", "미니", "작은", "조그만", "쪼꼬미", "baby", "mini", "little", "tiny", "small")) { s.Baby = true; s.Size = 0.88f; }
        if (A("거대", "커다란", "대왕", "큰", "big", "giant", "huge")) s.Size = 1.25f;
        if (A("뚱뚱", "통통", "포동", "토실", "뚱", "동글", "chubby", "fat")) s.Chub = 1.2f;
        else if (A("날씬", "마른", "slim", "skinny")) s.Chub = 0.88f;
        if (s.Kind == Kind.Mouse && s.Sub == "hamster" && s.Chub == 1f) s.Chub = 1.12f;

        Vary(s, rnd);
        Leftovers(t, seen, s.Unknown);
        s.Label = MakeLabel(s);
        return s;
    }

    // 알아들은 자리에 걸치지 않은 낱말 — 꾸밈말 · 토씨 · '만들어줘' 같은 말은 빼고 남은 것만 (세 개까지)
    private static void Leftovers(string t, List<(int s, int e)> seen, List<string> unknown)
    {
        int i = 0;
        while (i < t.Length)
        {
            if (!char.IsLetterOrDigit(t[i])) { i++; continue; }
            int st = i;
            while (i < t.Length && (char.IsLetterOrDigit(t[i]) || t[i] == '-')) i++;
            int en = i;
            bool known = false, digits = true;
            foreach (var h in seen) if (h.s < en && st < h.e) { known = true; break; }
            if (known) continue;
            string w = t.Substring(st, en - st);
            foreach (char c in w) if (!char.IsDigit(c)) { digits = false; break; }
            if (digits) continue;
            string stem = w;
            foreach (var tail in Tails)
                if (stem.Length > tail.Length + 1 && stem.EndsWith(tail, StringComparison.Ordinal)) { stem = stem.Substring(0, stem.Length - tail.Length); break; }
            if (Filler.Contains(w) || Filler.Contains(stem)) continue;
            bool hangul = stem[0] >= '가' && stem[0] <= '힣';
            if (stem.Length < (hangul ? 2 : 3)) continue;
            if (!unknown.Contains(stem) && unknown.Count < 3) unknown.Add(stem);
        }
    }

    // 같은 요청을 '만들기'로 다시 — 요청에 적힌 것(무엇 · 색 · 모자 · 소품 · 표정 · 움직임 · 몸집)은 그대로, 적히지 않은 것만 바꾼다 (11-13)
    // 표정 · 털 무늬(털 있는 동물) · 몸집 · 같은 색 안의 짙기. 씨앗(사진 · 요청 · 차례)이 같으면 늘 같은 모습
    private static readonly Face[] Moods = { Face.Smile, Face.Happy, Face.Wink, Face.Calm };

    private static void Vary(Spec s, System.Random rnd)
    {
        if (s.Face == Face.Smile) s.Face = Moods[rnd.Next(Moods.Length)];
        bool furry = s.Kind == Kind.Dog || s.Kind == Kind.Cat || s.Kind == Kind.Bear || s.Kind == Kind.Fox || s.Kind == Kind.Pig || s.Kind == Kind.Rabbit;
        if (furry && s.Pattern == Pattern.None && s.Job == Job.None && !s.Rainbow && rnd.Next(3) == 0)
        {
            s.Pattern = rnd.Next(2) == 0 ? Pattern.Spots : Pattern.Stripes;
            s.Detail = Color.Lerp(s.Main, new Color(0.2f, 0.17f, 0.16f), 0.55f);
        }
        if (s.Chub == 1f) s.Chub = 0.92f + (float)rnd.NextDouble() * 0.18f;
        if (s.Size == 1f) s.Size = 0.95f + (float)rnd.NextDouble() * 0.1f;
        bool same = s.Second == s.Main;   // 원피스 · 산타 · 공주 — 위아래 한 색은 함께 짙기를 바꾼다
        Color.RGBToHSV(s.Main, out float h, out float sat, out float v);
        s.Main = Color.HSVToRGB(h, Mathf.Clamp01(sat * (0.86f + (float)rnd.NextDouble() * 0.22f)), Mathf.Clamp01(v * (0.9f + (float)rnd.NextDouble() * 0.14f)));
        if (same) s.Second = s.Main;
    }

    private static Color HatDefault(Hat h, Job job, System.Random rnd)
    {
        switch (h)
        {
            case Hat.Straw: return new Color(0.95f, 0.82f, 0.52f);
            case Hat.Chef: return Color.white;
            case Hat.Santa: return new Color(0.86f, 0.15f, 0.17f);
            case Hat.Helmet: return job == Job.Firefighter ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.95f, 0.78f, 0.2f);
            case Hat.Wizard: return new Color(0.42f, 0.3f, 0.72f);
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
            case Kind.Bear:
                if (s.Sub == "polar") { s.Main = new Color(0.97f, 0.97f, 0.95f); s.Second = new Color(0.9f, 0.9f, 0.88f); }
                else if (s.Sub == "black") { s.Main = new Color(0.2f, 0.18f, 0.18f); s.Second = new Color(0.93f, 0.91f, 0.86f); }
                else { s.Main = new Color(0.62f, 0.42f, 0.26f); s.Second = new Color(0.9f, 0.77f, 0.6f); }
                break;
            case Kind.Panda: s.Main = new Color(0.98f, 0.98f, 0.97f); s.Second = new Color(0.13f, 0.13f, 0.15f); break;
            case Kind.Fox: s.Main = new Color(0.96f, 0.52f, 0.2f); s.Second = new Color(0.99f, 0.97f, 0.94f); break;
            case Kind.Pig: s.Main = new Color(1f, 0.74f, 0.77f); s.Second = new Color(0.97f, 0.57f, 0.64f); break;
            case Kind.Chick:
                switch (s.Sub)
                {
                    case "duck": s.Main = new Color(1f, 0.88f, 0.34f); s.Second = new Color(1f, 0.96f, 0.74f); break;
                    case "swan": s.Main = new Color(0.98f, 0.98f, 0.97f); s.Second = new Color(0.95f, 0.95f, 0.97f); break;
                    case "parrot": s.Main = new Color(0.32f, 0.78f, 0.38f); s.Second = new Color(1f, 0.85f, 0.28f); break;
                    case "sparrow": s.Main = new Color(0.68f, 0.5f, 0.34f); s.Second = new Color(0.97f, 0.91f, 0.8f); break;
                    case "pigeon": s.Main = new Color(0.62f, 0.65f, 0.72f); s.Second = new Color(0.86f, 0.88f, 0.93f); break;
                    case "magpie": s.Main = new Color(0.17f, 0.18f, 0.22f); s.Second = new Color(0.97f, 0.97f, 0.97f); break;
                    case "eagle": s.Main = new Color(0.52f, 0.35f, 0.22f); s.Second = new Color(0.98f, 0.97f, 0.95f); break;
                    default: s.Main = new Color(1f, 0.87f, 0.32f); s.Second = new Color(1f, 0.6f, 0.16f); break;
                }
                break;
            case Kind.Penguin: s.Main = new Color(0.18f, 0.22f, 0.34f); s.Second = new Color(0.99f, 0.99f, 0.98f); break;
            case Kind.Frog: s.Main = new Color(0.46f, 0.79f, 0.36f); s.Second = new Color(0.87f, 0.95f, 0.62f); break;
            case Kind.Robot: s.Main = new Color(0.8f, 0.82f, 0.87f); s.Second = new Color(0.36f, 0.47f, 0.62f); break;
            case Kind.Lion: s.Main = new Color(0.97f, 0.76f, 0.4f); s.Second = new Color(0.99f, 0.93f, 0.8f); s.Accent = new Color(0.8f, 0.46f, 0.2f); break;
            case Kind.Tiger:
                bool leopard = s.Sub == "leopard";
                s.Main = leopard ? new Color(0.98f, 0.8f, 0.42f) : new Color(1f, 0.6f, 0.2f);
                s.Second = new Color(0.99f, 0.97f, 0.94f);
                if (s.Pattern == Pattern.None) { s.Pattern = leopard ? Pattern.Spots : Pattern.Stripes; s.Detail = leopard ? new Color(0.28f, 0.2f, 0.13f) : new Color(0.15f, 0.12f, 0.12f); }
                break;
            case Kind.Mouse:
                switch (s.Sub)
                {
                    case "hamster": s.Main = new Color(0.98f, 0.72f, 0.42f); s.Second = new Color(1f, 0.97f, 0.93f); break;
                    case "squirrel": s.Main = new Color(0.82f, 0.52f, 0.28f); s.Second = new Color(0.99f, 0.92f, 0.8f); break;
                    case "bat": s.Main = new Color(0.38f, 0.33f, 0.45f); s.Second = new Color(0.64f, 0.58f, 0.7f); break;
                    default: s.Main = new Color(0.72f, 0.72f, 0.76f); s.Second = new Color(0.96f, 0.94f, 0.94f); break;
                }
                break;
            case Kind.Koala: s.Main = new Color(0.66f, 0.68f, 0.73f); s.Second = new Color(0.96f, 0.96f, 0.96f); break;
            case Kind.Monkey: s.Main = new Color(0.56f, 0.37f, 0.23f); s.Second = new Color(0.98f, 0.84f, 0.68f); break;
            case Kind.Sheep: s.Main = new Color(0.98f, 0.97f, 0.95f); s.Second = new Color(0.99f, 0.86f, 0.76f); break;
            case Kind.Cow:
                if (s.Sub == "bull") { s.Main = new Color(0.66f, 0.42f, 0.24f); s.Second = new Color(0.95f, 0.75f, 0.68f); }
                else
                {
                    s.Main = new Color(0.98f, 0.98f, 0.97f); s.Second = new Color(1f, 0.76f, 0.8f);
                    if (s.Pattern == Pattern.None) { s.Pattern = Pattern.Spots; s.Detail = new Color(0.14f, 0.14f, 0.15f); }
                }
                s.Accent = new Color(0.25f, 0.2f, 0.18f);
                break;
            case Kind.Elephant: s.Main = new Color(0.67f, 0.71f, 0.8f); s.Second = new Color(1f, 0.78f, 0.84f); break;
            case Kind.Dino:
                if (s.Sub == "dragon") { s.Main = new Color(0.36f, 0.7f, 0.5f); s.Second = new Color(1f, 0.9f, 0.6f); s.Accent = new Color(1f, 0.78f, 0.3f); }
                else { s.Main = new Color(0.47f, 0.8f, 0.42f); s.Second = new Color(0.97f, 0.93f, 0.62f); s.Accent = new Color(1f, 0.6f, 0.28f); }
                break;
            case Kind.Owl: s.Main = new Color(0.64f, 0.46f, 0.3f); s.Second = new Color(0.97f, 0.91f, 0.78f); break;
            case Kind.Raccoon: s.Main = new Color(0.6f, 0.58f, 0.57f); s.Second = new Color(0.98f, 0.97f, 0.96f); s.Accent = new Color(0.22f, 0.2f, 0.2f); break;
            case Kind.Horse:
                if (s.Sub == "unicorn") { s.Main = new Color(0.99f, 0.98f, 0.99f); s.Second = new Color(1f, 0.88f, 0.92f); s.Accent = new Color(1f, 0.62f, 0.78f); }
                else { s.Main = new Color(0.66f, 0.43f, 0.26f); s.Second = new Color(0.95f, 0.87f, 0.78f); s.Accent = new Color(0.3f, 0.2f, 0.14f); }
                break;
            case Kind.Person:
                s.Main = Pal5[rnd.Next(Pal5.Length)];
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
                    case Job.Wizard: s.Main = new Color(0.42f, 0.3f, 0.72f); s.Second = new Color(0.42f, 0.3f, 0.72f); break;
                }
                break;
        }
    }

    private static string KindName(Spec s)
    {
        string w = s.KindWord;
        if (!string.IsNullOrEmpty(w) && w.Length >= 2 && w[0] >= '가' && w[0] <= '힣' && !NickWords.Contains(w)) return w;
        return SubNames.TryGetValue(s.Sub ?? "", out var n) ? n : KindNames[(int)s.Kind];
    }

    private static string MakeLabel(Spec s)
    {
        var sb = new StringBuilder();
        if (s.MainGiven && s.Kind != Kind.Person) sb.Append(s.MainName).Append(' ');
        if (s.Rainbow && s.Kind != Kind.Horse && s.Kind != Kind.Person) sb.Append("무지개 ");
        if (s.Size >= 1.2f) sb.Append("커다란 ");
        if (s.Baby) sb.Append("아기 ");
        // 이름 — '노란 고양이' · '마녀 고양이' · '요리사' · '학생'
        string kind = KindName(s);
        string role = s.RoleWord ?? (s.Job != Job.None ? JobNames[(int)s.Job] : null);
        sb.Append(role == null ? kind : s.Kind == Kind.Person && s.KindWord == null ? role : role + " " + kind);
        var bits = new List<string>();
        string Named(string name, string what) => string.IsNullOrEmpty(name) ? what : name + " " + what;
        string En(Extra e, string what) => Named(s.ExtraName.TryGetValue(e, out var n) ? n : null, what);
        bool Has(Extra e) => s.Extras.HasFlag(e) && !s.AutoExtras.HasFlag(e);
        if (s.Kind == Kind.Person && s.MainGiven && !Has(Extra.Shirt)) bits.Add(s.MainName + " 옷");
        if (s.Rainbow && s.Kind == Kind.Person) bits.Add("무지개 옷");
        if (s.Rainbow && s.Kind == Kind.Horse && s.Sub != "unicorn") bits.Add("무지개 갈기");
        if (s.HatGiven) bits.Add(Named(s.HatColorName, s.Hat == Hat.Straw ? "밀짚모자" : s.Hat == Hat.Party ? "고깔모자" : "모자"));
        if (Has(Extra.Crown) && s.Job != Job.Princess && s.Job != Job.Prince) bits.Add(En(Extra.Crown, "왕관"));
        if (Has(Extra.Bow)) bits.Add(En(Extra.Bow, "리본"));
        if (Has(Extra.Glasses)) bits.Add(En(Extra.Glasses, "안경"));
        if (Has(Extra.Sunglasses)) bits.Add(En(Extra.Sunglasses, "선글라스"));
        if (Has(Extra.Scarf)) bits.Add(En(Extra.Scarf, "목도리"));
        if (Has(Extra.Shirt)) bits.Add(En(Extra.Shirt, s.Dress ? "원피스" : "옷"));
        if (Has(Extra.Pants)) bits.Add(En(Extra.Pants, "바지"));
        if (Has(Extra.Cape)) bits.Add(En(Extra.Cape, "망토"));
        if (Has(Extra.Wings)) bits.Add(En(Extra.Wings, "날개"));
        if (Has(Extra.Halo)) bits.Add("후광");
        if (Has(Extra.Horn) && s.Kind != Kind.Horse) bits.Add("유니콘 뿔");
        if (Has(Extra.Horns)) bits.Add(En(Extra.Horns, "뿔"));
        if (Has(Extra.Flower)) bits.Add(En(Extra.Flower, "꽃"));
        if (Has(Extra.Backpack)) bits.Add(En(Extra.Backpack, "가방"));
        if (Has(Extra.Tie)) bits.Add(En(Extra.Tie, "넥타이"));
        if (Has(Extra.Bell)) bits.Add(En(Extra.Bell, "방울"));
        if (Has(Extra.Heart)) bits.Add(En(Extra.Heart, "하트"));
        if (Has(Extra.Star)) bits.Add(En(Extra.Star, "별"));
        bool natural = (s.Kind == Kind.Tiger && (s.Pattern == Pattern.Stripes) == (s.Sub != "leopard")) || (s.Kind == Kind.Cow && s.Pattern == Pattern.Spots);
        if (!natural && s.Pattern == Pattern.Spots) bits.Add("점박이");
        if (!natural && s.Pattern == Pattern.Stripes) bits.Add("줄무늬");
        if (s.Chub >= 1.19f) bits.Add("통통");
        else if (s.Chub <= 0.89f) bits.Add("날씬");
        if (s.FaceGiven)
            switch (s.Face)
            {
                case Face.Wink: bits.Add("윙크"); break;
                case Face.Angry: bits.Add("화난 얼굴"); break;
                case Face.Sad: bits.Add("우는 얼굴"); break;
                case Face.Sleepy: bits.Add("졸린 얼굴"); break;
                case Face.Surprised: bits.Add("놀란 얼굴"); break;
                case Face.Calm: bits.Add("시크한 얼굴"); break;
                case Face.Happy: bits.Add("활짝 웃는 얼굴"); break;
            }
        switch (s.Move)
        {
            case Move.Wave: bits.Add("손 인사"); break;
            case Move.Dance: bits.Add("춤"); break;
            case Move.Jump: bits.Add("폴짝"); break;
        }
        if (bits.Count > 0) sb.Append(" · ").Append(string.Join(" · ", bits));
        if (s.Unknown.Count > 0) sb.Append(" · 아직 모르는 말: ").Append(string.Join(", ", s.Unknown));
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
            // 무지개 털 — 높이를 따라 여섯 빛 (말은 갈기 · 꼬리만, 얼굴 가운데는 비운다)
            if (S.Rainbow && S.Kind != Kind.Horse && !(face && Mathf.Abs(s.Lon) < 0.95f && s.Lat < 0.4f))
            {
                float rb = Mathf.Repeat(s.P.y * 8f + s.P.x * 1.5f, 6f);
                int ri = (int)rb;
                c = Mix(RainbowPal[ri % 6], RainbowPal[(ri + 1) % 6], SmoothStep(0.82f, 1f, rb - ri));
            }
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
        public int Body, Head, ArmL, ArmR, Tail = -1, EarL = -1, EarR = -1, EyeL = -1, EyeR = -1, Sign = -1, WingL = -1, WingR = -1;
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

        // 발 (새는 주황 · 소 · 말 · 양은 발굽)
        bool bird = k == Kind.Chick || k == Kind.Penguin || k == Kind.Owl, hoof = Hoofed(s);
        Color footC = k == Kind.Panda ? second : bird ? new Color(1f, 0.6f, 0.18f) : hoof ? HoofColor(s) : k == Kind.Koala ? Dark(main, 0.55f)
                    : person ? new Color(0.25f, 0.2f, 0.2f) : robot ? Dark(s.Second, 0.8f) : main;
        bool flatFoot = k == Kind.Panda || person || robot || bird || hoof || k == Kind.Koala;
        for (int side = -1; side <= 1; side += 2)
        {
            var foot = b.Add(side < 0 ? "FootL" : "FootR", 0, new Vector3(side * 0.105f * ch * bs, 0.055f * bs, 0.025f), new Vector3(0.075f, 0.062f, 0.092f) * bs, null, 16, 8);
            Color fc = footC;
            foot.Paint = sm => flatFoot ? fc : b.Fur(sm, fc);
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
            if (k == Kind.Chick || k == Kind.Owl) rad = new Vector3(0.03f, 0.095f, 0.075f) * bs;
            var arm = b.Add(g == r.ArmL ? "ArmLMesh" : "ArmRMesh", g, new Vector3(0f, -0.1f * bs, 0f), rad, null, 14, 10);
            if (robot) { arm.E1 = 0.6f; arm.E2 = 0.6f; }
            arm.Paint = sm => PaintArm(b, sm);
        }

        // 꼬리
        AddTail(b);
        // 귀 · 눈 · 코 · 입 둘레
        AddFace(b);
        // 갈기 · 양털 · 등 가시 · 볼주머니
        AddKindParts(b);
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
                if (s.Job == Job.Wizard)
                    foreach (var (wx, wy) in Sparkles)
                        if (InStar((lon - wx) / 0.08f, (lat - wy) / 0.08f)) c = new Color(1f, 0.85f, 0.35f);
                if (s.Rainbow && lat > -0.29f) c = RainbowPal[(int)Mathf.Repeat((lat + 0.29f) * 7f, 6f)];   // 무지개 옷
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
                c = b.Fur(sm, s.Main);
                if (s.Sub != "" && s.Sub != "eagle") c = Mix(c, s.Second, Ell(lon, lat, 0f, -0.2f, 0.62f, 0.55f, 0.03f));   // 오리 · 참새 … 배
                break;
            case Kind.Pig:
            case Kind.Cow:
            case Kind.Elephant:
                c = b.Fur(sm, s.Main);
                break;
            case Kind.Sheep:
                c = Wool(sm, s.Main);
                break;
            case Kind.Owl:
            {
                c = b.Fur(sm, s.Main);
                float belly = Ell(lon, lat, 0f, -0.15f, 0.7f, 0.62f, 0.03f);
                c = Mix(c, s.Second, belly);
                float fe = 0f;   // 가슴 깃털 (작은 반달 무늬)
                for (int row = 0; row < 3; row++)
                    for (int col = -1; col <= 1; col++)
                        fe = Mathf.Max(fe, Arc(lon, lat, col * 0.24f + (row % 2) * 0.12f, 0.13f - row * 0.2f, 0.06f, 0.022f, -2.5f, -0.64f));
                c = Mix(c, Dark(s.Main, 0.9f), fe * belly);
                break;
            }
            default:
                c = b.Fur(sm, s.Main);
                if (k == Kind.Bear && s.Sub == "black")   // 반달가슴곰 — 가슴의 흰 반달
                {
                    c = Mix(c, s.Second, Mathf.Max(Line(lon, lat, -0.42f, 0.36f, 0f, 0.16f, 0.09f, 0.02f), Line(lon, lat, 0.42f, 0.36f, 0f, 0.16f, 0.09f, 0.02f)));
                    break;
                }
                float bl = Ell(lon, lat, 0f, -0.15f, 0.72f, 0.62f, 0.03f);
                c = Mix(c, s.Second, bl);
                if (k == Kind.Dino) c = Mix(c, Dark(s.Second, 0.86f), bl * SmoothStep(0.75f, 0.95f, Mathf.Sin(lat * 24f)));   // 공룡 배 주름
                break;
        }
        // 동물이 입은 옷 — 몸에 칠하고 팔에는 소매 (사람은 몸 색이 곧 옷)
        if (k != Kind.Person && k != Kind.Robot)
        {
            bool shirt = s.Extras.HasFlag(Extra.Shirt), pants = s.Extras.HasFlag(Extra.Pants);
            if (pants && lat < -0.3f) c = Cloth(sm, s.ExtraColor[Extra.Pants]);
            if (shirt)
            {
                float hem = s.Dress ? -0.74f + 0.05f * Mathf.Sin(lon * 9f) : -0.3f;
                if (lat > hem) c = Cloth(sm, s.ExtraColor[Extra.Shirt]);
                if (Mathf.Abs(lat - hem) < 0.028f) c = Dark(s.ExtraColor[Extra.Shirt], 0.8f);
            }
            else if (pants && Mathf.Abs(lat + 0.3f) < 0.028f) c = Dark(s.ExtraColor[Extra.Pants], 0.75f);
        }
        if (s.Extras.HasFlag(Extra.Heart) && InHeart(lon / 0.24f, (lat + 0.05f) / 0.22f)) c = s.ExtraColor[Extra.Heart];
        else if (s.Extras.HasFlag(Extra.Star) && InStar(lon / 0.26f, (lat + 0.05f) / 0.26f)) c = s.ExtraColor[Extra.Star];
        return c;
    }

    private static readonly (float x, float y)[] Sparkles = { (-0.35f, 0.15f), (0.4f, -0.1f), (-0.1f, -0.55f), (2.4f, 0.1f), (-2.2f, -0.3f), (1.3f, 0.35f) };

    private static bool Hoofed(Spec s) => s.Kind == Kind.Horse || s.Kind == Kind.Cow || s.Kind == Kind.Sheep;
    private static Color HoofColor(Spec s) => s.Sub == "unicorn" ? new Color(1f, 0.82f, 0.42f) : new Color(0.3f, 0.24f, 0.22f);

    private static Color PaintArm(Builder b, Sample sm)
    {
        var s = b.S;
        switch (s.Kind)
        {
            case Kind.Person: return sm.D.y < -0.55f ? s.Skin : s.Main;
            case Kind.Panda: return s.Second;
            case Kind.Robot: return sm.D.y < -0.6f ? Dark(s.Second, 0.8f) : s.Main;
        }
        if (s.Extras.HasFlag(Extra.Shirt) && sm.D.y > -0.3f) return Cloth(sm, s.ExtraColor[Extra.Shirt]);   // 소매
        if (Hoofed(s) && sm.D.y < -0.72f) return HoofColor(s);
        if (s.Kind == Kind.Sheep) return s.Second;
        return b.Fur(sm, s.Main);
    }

    // 옷감 — 잔 골이 진 천
    private static Color Cloth(Sample sm, Color c) => Mix(c, Dark(c, 0.93f), 0.5f + 0.5f * Mathf.Sin(sm.P.y * 120f));

    // 양털 — 동글동글 뭉게 무늬
    private static Color Wool(Sample sm, Color c)
    {
        Vector3 d = sm.D.normalized;
        float n = Mathf.Sin(d.x * 19f) * Mathf.Sin(d.y * 17f + 1.3f) * Mathf.Sin(d.z * 18f + 0.7f);
        return Mix(c, Dark(c, 0.87f), SmoothStep(0.15f, 0.6f, Mathf.Abs(n)));
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
            case Kind.Tiger:   // 흰 볼 · 볼 줄무늬
                for (int side = -1; side <= 1; side += 2)
                {
                    c = Mix(c, s.Second, Ell(lon, lat, side * 0.5f, -0.3f, 0.26f, 0.16f, 0.03f));
                    if (s.Sub != "leopard")
                        for (int w = 0; w < 2; w++)
                            c = Mix(c, s.Detail, Line(lon, lat, side * 0.98f, 0.12f - w * 0.16f, side * 0.72f, 0.06f - w * 0.14f, 0.04f));
                }
                break;
            case Kind.Mouse:   // 밝은 입가 · 수염 (박쥐는 수염 없이)
                c = Mix(c, s.Second, Ell(lon, lat, 0f, -0.28f, s.Sub == "hamster" ? 0.55f : 0.36f, 0.22f, 0.03f));
                if (s.Sub != "bat")
                    for (int side = -1; side <= 1; side += 2)
                        for (int w = 0; w < 2; w++)
                            c = Mix(c, new Color(0.35f, 0.3f, 0.3f), Line(lon, lat, side * 0.2f, -0.17f - w * 0.05f, side * 0.52f, -0.12f - w * 0.09f, 0.011f, 0.006f));
                break;
            case Kind.Monkey:   // 하트 모양 얼굴
                c = Mix(c, s.Second, Mathf.Max(Mathf.Max(Ell(lon, lat, -0.3f, 0.03f, 0.29f, 0.26f, 0.03f), Ell(lon, lat, 0.3f, 0.03f, 0.29f, 0.26f, 0.03f)), Ell(lon, lat, 0f, -0.28f, 0.45f, 0.27f, 0.03f)));
                break;
            case Kind.Sheep:   // 크림색 얼굴 · 나머지는 양털
                c = Mix(Wool(sm, s.Main), s.Second, Ell(lon, lat, 0f, -0.14f, 0.95f, 0.62f, 0.03f));
                break;
            case Kind.Owl:   // 눈 둘레 얼굴판
                for (int side = -1; side <= 1; side += 2) c = Mix(c, s.Second, Ell(lon, lat, side * 0.36f, 0.02f, 0.31f, 0.3f, 0.03f));
                break;
            case Kind.Raccoon:   // 흰 입가 · 눈가 검은 띠 · 흰 눈썹
                c = Mix(c, s.Second, Ell(lon, lat, 0f, -0.3f, 0.45f, 0.22f, 0.03f));
                for (int side = -1; side <= 1; side += 2)
                {
                    float ex = lon - side * 0.36f, ey = lat + 0.01f;
                    float rot = side * -0.3f;
                    float rx = ex * Mathf.Cos(rot) - ey * Mathf.Sin(rot), ry = ex * Mathf.Sin(rot) + ey * Mathf.Cos(rot);
                    c = Mix(c, s.Accent, Ell(rx, ry, 0f, 0f, 0.25f, 0.14f));
                    c = Mix(c, s.Second, Ell(lon, lat, side * 0.3f, 0.25f, 0.15f, 0.06f));
                }
                break;
            case Kind.Horse:   // 이마의 흰 줄
                if (s.Sub != "unicorn") c = Mix(c, s.Second, Ell(lon, lat, 0f, 0.3f, 0.07f, 0.3f, 0.03f));
                break;
            case Kind.Dino:   // 밝은 턱
                if (Mathf.Abs(lon) < 1.15f) c = Mix(c, s.Second, SmoothStep(-0.18f, -0.3f, lat));
                break;
            case Kind.Chick:
                if (s.Sub == "eagle") c = s.Second;   // 흰머리 독수리
                if (s.Sub == "sparrow" || s.Sub == "parrot")
                    for (int side = -1; side <= 1; side += 2) c = Mix(c, s.Second, Ell(lon, lat, side * 0.5f, -0.16f, 0.22f, 0.18f, 0.03f));
                break;
        }

        // 볼 · 눈썹 · 입 (입은 주둥이 · 부리가 없는 얼굴에만 — 주둥이는 주둥이에 그린다)
        bool muzzle = k == Kind.Dog || k == Kind.Bear || k == Kind.Panda || k == Kind.Fox || k == Kind.Pig || k == Kind.Rabbit
                   || k == Kind.Lion || k == Kind.Tiger || k == Kind.Monkey || k == Kind.Raccoon || k == Kind.Cow || k == Kind.Horse || k == Kind.Elephant;
        for (int side = -1; side <= 1; side += 2)
            c = Mix(c, new Color(1f, 0.55f, 0.6f), 0.55f * Ell(lon, lat, side * (k == Kind.Frog ? 0.62f : 0.56f), -0.2f, 0.1f, 0.065f, 0.04f));
        if (s.Face == Face.Angry || s.Face == Face.Sad)
            for (int side = -1; side <= 1; side += 2)
            {
                float inner = s.Face == Face.Angry ? 0.2f : 0.3f, outer = s.Face == Face.Angry ? 0.3f : 0.2f;
                c = Mix(c, Dark(k == Kind.Person ? s.Hair : s.Main, 0.35f), Line(lon, lat, side * 0.22f, inner, side * 0.5f, outer, 0.035f));
            }
        if (!muzzle && k != Kind.Chick && k != Kind.Penguin && k != Kind.Owl)
            c = Mix(c, MouthColor(s), Mouth(s, lon, lat + (k == Kind.Frog ? 0.06f : k == Kind.Koala ? 0.13f : 0.05f), k == Kind.Frog ? 1.8f : k == Kind.Dino ? 1.5f : 1f));
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
            case Kind.Lion:   // 귀 안쪽은 작게 · 둘레는 털 색 (앞면 전체가 크림색이면 회색 원판으로 보인다)
                Ears(b, HeadPoint, 0.8f, 0.68f, new Vector3(0.6f, 1f, 0f), new Vector3(0.07f, 0.065f, 0.04f), 0f, Vector3.zero,
                    sm => sm.D.z > 0.45f && sm.D.x * sm.D.x + (sm.D.y + 0.1f) * (sm.D.y + 0.1f) < 0.42f ? Mix(s.Main, s.Second, 0.6f) : b.Fur(sm, Dark(s.Main, 0.92f)));
                break;
            case Kind.Tiger:
                Ears(b, HeadPoint, 0.78f, 0.7f, new Vector3(0.6f, 1f, 0f), new Vector3(0.08f, 0.075f, 0.042f), 0f, Vector3.zero,
                    sm => sm.D.z > 0.45f && sm.D.x * sm.D.x + (sm.D.y + 0.1f) * (sm.D.y + 0.1f) < 0.42f ? s.Second : sm.D.z < -0.3f ? Dark(s.Main, 0.35f) : b.Fur(sm, s.Main));
                break;
            case Kind.Raccoon:
                Ears(b, HeadPoint, 0.72f, 0.72f, new Vector3(0.55f, 1f, 0f), new Vector3(0.075f, 0.09f, 0.04f), -0.5f, Vector3.zero,
                    sm => sm.D.z > 0.25f && sm.D.y < 0.7f ? s.Second : b.Fur(sm, s.Main));
                break;
            case Kind.Mouse:
                switch (s.Sub)
                {
                    case "hamster":
                        Ears(b, HeadPoint, 0.8f, 0.62f, new Vector3(0.6f, 1f, 0.1f), new Vector3(0.06f, 0.055f, 0.025f), 0f, Vector3.zero,
                            sm => sm.D.z > 0.2f && sm.D.y < 0.7f ? inner : b.Fur(sm, s.Main));
                        break;
                    case "squirrel":
                        Ears(b, HeadPoint, 0.55f, 0.75f, new Vector3(0.25f, 1f, -0.05f), new Vector3(0.06f, 0.11f, 0.035f), -0.85f, Vector3.zero,
                            sm => sm.D.y > 0.6f ? Dark(s.Main, 0.6f) : b.Fur(sm, s.Main));
                        break;
                    case "bat":
                        Ears(b, HeadPoint, 0.62f, 0.68f, new Vector3(0.5f, 1f, 0f), new Vector3(0.11f, 0.16f, 0.035f), -0.9f, Vector3.zero,
                            sm => sm.D.z > 0.2f && Mathf.Abs(sm.D.x) < 0.55f && sm.D.y < 0.55f ? Mix(s.Second, inner, 0.5f) : b.Fur(sm, s.Main));
                        break;
                    default:   // 생쥐 — 크고 둥근 귀
                        Ears(b, HeadPoint, 0.72f, 0.72f, new Vector3(0.7f, 1f, 0.05f), new Vector3(0.13f, 0.12f, 0.03f), 0f, Vector3.zero,
                            sm => sm.D.z > 0.2f && sm.D.y < 0.8f && Mathf.Abs(sm.D.x) < 0.8f ? inner : b.Fur(sm, s.Main));
                        break;
                }
                break;
            case Kind.Koala:
                Ears(b, HeadPoint, 1.2f, 0.45f, new Vector3(1f, 0.55f, 0f), new Vector3(0.14f, 0.12f, 0.06f), 0f, Vector3.zero,
                    sm => sm.D.z > 0.25f && Mathf.Abs(sm.D.x) < 0.7f && sm.D.y < 0.75f ? s.Second : b.Fur(sm, s.Main));
                break;
            case Kind.Monkey:
                Ears(b, HeadPoint, 1.45f, 0.08f, new Vector3(1f, 0.15f, 0f), new Vector3(0.075f, 0.07f, 0.03f), 0f, Vector3.zero,
                    sm => sm.D.z > 0.2f && sm.D.y < 0.7f ? s.Second : b.Fur(sm, s.Main));
                break;
            case Kind.Sheep:
                Ears(b, HeadPoint, 1.15f, 0.25f, new Vector3(1f, -0.25f, 0.15f), new Vector3(0.05f, 0.11f, 0.03f), -0.2f, Vector3.zero,
                    sm => sm.D.z > 0.2f && Mathf.Abs(sm.D.x) < 0.6f ? inner : s.Second);
                break;
            case Kind.Cow:
                Ears(b, HeadPoint, 1.1f, 0.3f, new Vector3(1f, -0.1f, 0.1f), new Vector3(0.06f, 0.12f, 0.03f), -0.3f, Vector3.zero,
                    sm => sm.D.z > 0.2f && Mathf.Abs(sm.D.x) < 0.6f ? s.Second : b.Fur(sm, s.Main));
                break;
            case Kind.Elephant:   // 크고 넓적한 귀 — 머리 옆에서 뒤로 비스듬히
                for (int side = -1; side <= 1; side += 2)
                {
                    int g = b.AddGroup(side < 0 ? "EarL" : "EarR", r.Head, HeadPoint(side * 1.3f, 0.1f, -0.03f, out _), Quaternion.identity);
                    if (side < 0) r.EarL = g; else r.EarR = g;
                    var ear = b.Add(side < 0 ? "EarLMesh" : "EarRMesh", g, Vector3.zero, new Vector3(0.17f, 0.2f, 0.025f), null, 18, 10);
                    ear.Q = Quaternion.Euler(0f, side * 30f, side * -8f);
                    ear.C = ear.Q * new Vector3(side * 0.15f, 0.01f, -0.02f);
                    ear.Paint = sm => sm.D.z > 0.25f && sm.D.x * sm.D.x + sm.D.y * sm.D.y < 0.5f ? Mix(s.Main, s.Second, 0.75f) : b.Fur(sm, s.Main);
                }
                break;
            case Kind.Owl:   // 깃털 귀
                Ears(b, HeadPoint, 0.55f, 0.8f, new Vector3(0.45f, 1f, 0f), new Vector3(0.05f, 0.09f, 0.03f), -0.85f, Vector3.zero, sm => b.Fur(sm, Dark(s.Main, 0.85f)));
                break;
            case Kind.Horse:
                Ears(b, HeadPoint, 0.5f, 0.82f, new Vector3(0.3f, 1f, -0.05f), new Vector3(0.05f, 0.1f, 0.035f), -0.8f, Vector3.zero,
                    sm => sm.D.z > 0.2f && Mathf.Abs(sm.D.x) < 0.55f && sm.D.y < 0.55f ? inner : b.Fur(sm, s.Main));
                break;
            case Kind.Chick:
                if (s.Sub != "" && s.Sub != "parrot") break;   // 오리 · 참새 … 는 머리깃 없이
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
            float eScale = (s.Baby ? 1.15f : 1f) * (k == Kind.Person ? 0.82f : k == Kind.Owl ? 1.25f : 1f);
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
            case Kind.Lion: Muzzle(b, HeadPoint, new Vector3(0.1f, 0.075f, 0.07f), s.Second, new Color(0.45f, 0.25f, 0.2f), 0.036f); break;
            case Kind.Tiger: Muzzle(b, HeadPoint, new Vector3(0.1f, 0.072f, 0.068f), s.Second, new Color(0.9f, 0.5f, 0.55f), 0.034f); break;
            case Kind.Monkey: Muzzle(b, HeadPoint, new Vector3(0.12f, 0.08f, 0.07f), s.Second, Dark(s.Main, 0.6f), 0.022f); break;
            case Kind.Raccoon: Muzzle(b, HeadPoint, new Vector3(0.09f, 0.065f, 0.085f), s.Second, noseC, 0.032f); break;
            case Kind.Cow:
            case Kind.Horse: Snout(b, HeadPoint); break;
            case Kind.Elephant: Trunk(b, HeadPoint); break;
            case Kind.Mouse:
            case Kind.Sheep:
            {
                Color nc = k == Kind.Sheep ? new Color(0.45f, 0.3f, 0.3f) : s.Sub == "bat" ? Dark(s.Main, 0.5f) : new Color(1f, 0.55f, 0.62f);
                var nose = b.Add("Nose", r.Head, HeadPoint(0f, -0.14f, 0.006f, out Vector3 nn), new Vector3(0.028f, 0.02f, 0.02f), _ => nc, 10, 6);
                nose.Q = Quaternion.LookRotation(nn, Vector3.up); nose.Matte = false;
                break;
            }
            case Kind.Koala:   // 크고 검은 코
            {
                var nose = b.Add("Nose", r.Head, HeadPoint(0f, -0.12f, 0f, out Vector3 nn), new Vector3(0.06f, 0.07f, 0.045f),
                    sm => sm.D.y > 0.45f && sm.D.x < -0.1f && sm.D.z > 0.3f ? new Color(0.42f, 0.42f, 0.47f) : new Color(0.17f, 0.16f, 0.18f), 14, 10);
                nose.Q = Quaternion.LookRotation(nn, Vector3.up); nose.Matte = false;
                break;
            }
            case Kind.Owl:   // 작은 갈고리 부리
            {
                Vector3 p = HeadPoint(0f, -0.08f, -0.01f, out Vector3 nn);
                var beak = b.Add("Beak", r.Head, p + nn * 0.02f, new Vector3(0.03f, 0.05f, 0.025f), _ => new Color(1f, 0.7f, 0.25f), 10, 6);
                beak.Taper = -0.9f; beak.Q = Quaternion.FromToRotation(Vector3.up, (nn * 0.4f + Vector3.down).normalized);
                beak.Matte = false;
                break;
            }
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
                Color bk = s.Sub == "parrot" ? new Color(0.27f, 0.26f, 0.28f) : s.Sub == "magpie" || s.Sub == "pigeon" ? new Color(0.32f, 0.3f, 0.32f)
                         : s.Sub == "eagle" ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.62f, 0.15f);
                if (s.Sub == "duck" || s.Sub == "swan")   // 넓적한 부리
                {
                    var bill = b.Add("Beak", r.Head, p + nn * 0.05f, new Vector3(0.1f, 0.03f, 0.075f), _ => bk, 16, 8);
                    bill.Q = Quaternion.LookRotation(nn, Vector3.up); bill.E1 = 0.6f; bill.Matte = false;
                    break;
                }
                var beak = b.Add("Beak", r.Head, p + nn * 0.045f, new Vector3(0.055f, 0.075f, 0.042f), _ => bk, 14, 8);
                beak.Taper = -0.9f; beak.Q = Quaternion.FromToRotation(Vector3.up, (nn + Vector3.down * 0.15f).normalized);
                if (s.Sub == "eagle" || s.Sub == "parrot") beak.Bend = Quaternion.Inverse(beak.Q) * new Vector3(0f, -0.045f, 0f);   // 갈고리
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

    // 소 · 말 — 앞으로 나온 넓은 코 (콧구멍 둘 · 입은 코 아래에 그린다)
    private static void Snout(Builder b, HeadPointFn hp)
    {
        var r = b.R;
        var s = b.S;
        bool horse = s.Kind == Kind.Horse;
        // 말 — 앞으로 길게 나온 주둥이에 비스듬한 작은 콧구멍 (둥근 원판에 큰 콧구멍 둘이면 돼지 코로 보인다)
        Vector3 rad = horse ? new Vector3(0.13f, 0.105f, 0.14f) : new Vector3(0.17f, 0.1f, 0.09f);
        Vector3 p = hp(0f, horse ? -0.22f : -0.26f, -0.03f, out Vector3 n);
        var sn = b.Add("Snout", r.Head, p + n * (horse ? 0.05f : 0.035f), rad, null, 22, 12);
        sn.Q = Quaternion.LookRotation(n, Vector3.up);
        sn.Density = 2f;
        Color col = horse ? Light2(s.Main, s.Sub == "unicorn" ? 0.12f : 0.25f) : s.Second;
        sn.Paint = sm =>
        {
            Vector3 d = sm.D;
            float front = d.z > 0.2f ? 1f : 0f;
            Color c = col;
            for (int side = -1; side <= 1; side += 2)
            {
                if (!horse) { c = Mix(c, Dark(col, 0.45f), front * Ell(d.x, d.y, side * 0.42f, 0.25f, 0.13f, 0.17f, 0.04f)); continue; }
                float ux = d.x - side * 0.5f, uy = d.y - 0.24f, a = side * 0.5f;
                c = Mix(c, Dark(col, 0.4f), front * Ell(ux * Mathf.Cos(a) - uy * Mathf.Sin(a), ux * Mathf.Sin(a) + uy * Mathf.Cos(a), 0f, 0f, 0.06f, 0.13f, 0.035f));
            }
            float m;
            switch (s.Face)
            {
                case Face.Happy:
                    m = Ell(d.x, d.y, 0f, -0.42f, 0.3f, 0.2f, 0.04f) * (d.y < -0.42f ? 1f : 0f);
                    if (m > 0f) c = Mix(c, Mix(new Color(0.5f, 0.15f, 0.18f), new Color(1f, 0.5f, 0.56f), Ell(d.x, d.y, 0f, -0.58f, 0.18f, 0.08f, 0.04f)), m * front);
                    return Mix(c, MouthColor(s), front * Line(d.x, d.y, -0.3f, -0.42f, 0.3f, -0.42f, 0.05f, 0.03f));
                case Face.Surprised: m = Ell(d.x, d.y, 0f, -0.45f, 0.12f, 0.15f, 0.04f); break;
                case Face.Sad: m = Arc(d.x, d.y, 0f, -0.78f, 0.3f, 0.06f, 0.74f, 2.4f, 0.03f); break;
                case Face.Calm: case Face.Angry: m = Line(d.x, d.y, -0.25f, -0.45f, 0.25f, -0.45f, 0.06f, 0.03f); break;
                default: m = Arc(d.x, d.y, 0f, -0.2f, 0.35f, 0.06f, -2.4f, -0.74f, 0.03f); break;
            }
            return Mix(c, MouthColor(s), front * m);
        };
        if (s.Sub == "bull")   // 황소 코뚜레
        {
            var ring = b.Add("NoseRing", r.Head, sn.C + sn.Q * new Vector3(0f, -0.035f, rad.z * 0.95f), new Vector3(0.035f, 0.035f, 0.035f), _ => new Color(1f, 0.8f, 0.3f), 14, 6);
            ring.Torus = true; ring.Ring = 0.22f; ring.Q = sn.Q * Quaternion.Euler(90f, 0f, 0f); ring.Matte = false;
        }
    }

    // 코끼리 코 — 얼굴 가운데서 앞아래로, 끝은 살짝 들린다
    private static void Trunk(Builder b, HeadPointFn hp)
    {
        var r = b.R;
        var s = b.S;
        const float L = 0.155f;
        Vector3 p = hp(0f, -0.12f, -0.03f, out Vector3 n);
        Vector3 dir = (n * 0.55f + Vector3.down).normalized;
        var t = b.Add("Trunk", r.Head, Vector3.zero, new Vector3(0.06f, L, 0.06f), null, 14, 12);
        t.Q = Quaternion.FromToRotation(Vector3.up, dir);
        t.C = p + dir * (L * 0.85f);
        t.Taper = -0.35f;
        t.Bend = Quaternion.Inverse(t.Q) * new Vector3(0f, 0.06f, 0.1f);
        t.Density = 1.6f;
        t.Paint = sm =>
        {
            Color c = b.Fur(sm, s.Main);
            c = Mix(c, Dark(c, 0.88f), SmoothStep(0.7f, 0.95f, Mathf.Sin(sm.V * 40f)));   // 주름
            if (sm.D.y > 0.93f) c = Dark(s.Second, 0.7f);   // 콧구멍
            return c;
        };
    }

    // 갈기 · 양털 · 등 가시 · 볼주머니 — 동물마다 하나 더 붙는 모양
    private static void AddKindParts(Builder b)
    {
        var s = b.S;
        var r = b.R;
        var head = r.HeadPart;
        float hs = r.Hs, bs = r.Bs, ch = s.Chub;
        Vector3 HP(float lon, float lat, float lift)
        {
            var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
            var nn = new Vector3(d.x / head.Rad.x, d.y / head.Rad.y, d.z / head.Rad.z).normalized;
            return head.C + Vector3.Scale(head.Rad, d) + nn * lift;
        }
        switch (s.Kind)
        {
            case Kind.Lion:   // 얼굴을 두른 갈기 — 열두 뭉치
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    Color mc = i % 2 == 0 ? s.Accent : Dark(s.Accent, 0.9f);
                    b.Add("Mane" + i, r.Head, head.C + new Vector3(Mathf.Sin(a) * 0.31f * hs, Mathf.Cos(a) * 0.29f * hs, -0.06f * hs), new Vector3(0.1f, 0.1f, 0.08f) * hs,
                        sm => Mix(mc, Dark(mc, 0.9f), 0.5f + 0.5f * Mathf.Sin(sm.Lat * 9f)), 14, 8);
                }
                break;
            case Kind.Sheep:   // 머리 위 양털 뭉치 (모자를 쓰면 모자 밑으로)
                if (s.Hat != Hat.None) break;
            {
                int n = 0;
                foreach (var (lon, lat, rr) in new[] { (0f, 1.25f, 0.1f), (0f, 0.85f, 0.085f), (-0.75f, 0.95f, 0.09f), (0.75f, 0.95f, 0.09f), (Mathf.PI, 0.9f, 0.1f), (-2.2f, 0.8f, 0.085f), (2.2f, 0.8f, 0.085f) })
                    b.Add("Wool" + n++, r.Head, HP(lon, lat, -0.01f), Vector3.one * rr * hs, sm => Wool(sm, s.Main), 14, 8);
                break;
            }
            case Kind.Dino:   // 머리와 등의 가시
            {
                Color sc = s.Accent;
                int n = 0;
                foreach (var (lon, lat, sz) in new[] { (0f, 1.25f, 0.06f), (Mathf.PI, 1.05f, 0.055f), (Mathf.PI, 0.55f, 0.045f) })
                {
                    var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
                    Spike(b, r.Head, "HeadSpike" + n++, HP(lon, lat, -0.01f), new Vector3(d.x / head.Rad.x, d.y / head.Rad.y, d.z / head.Rad.z), sz * hs, sc);
                }
                Vector3 bc = new Vector3(0f, 0.215f * bs, 0f), brad = new Vector3(0.215f * ch, 0.215f, 0.19f * ch) * bs;
                foreach (float lat in new[] { 0.6f, 0.2f, -0.2f })
                {
                    Vector3 at = bc + new Vector3(0f, brad.y * Mathf.Sin(lat), -brad.z * Mathf.Cos(lat));
                    Spike(b, r.Body, "BackSpike" + n++, at, new Vector3(0f, Mathf.Sin(lat) / brad.y, -Mathf.Cos(lat) / brad.z), 0.05f * bs, sc);
                }
                break;
            }
            case Kind.Mouse:   // 햄스터 볼주머니
                if (s.Sub != "hamster") break;
                for (int side = -1; side <= 1; side += 2)
                    b.Add(side < 0 ? "CheekL" : "CheekR", r.Head, HP(side * 0.62f, -0.28f, -0.03f), new Vector3(0.09f, 0.075f, 0.08f) * hs, sm => b.Fur(sm, Mix(s.Main, s.Second, 0.6f)), 14, 8);
                break;
            case Kind.Horse:   // 정수리에서 목덜미까지 갈기 (유니콘 · 무지개는 여섯 빛)
            {
                bool rainbow = s.Sub == "unicorn" || s.Rainbow;
                for (int i = 0; i < 7; i++)
                {
                    float th = 0.95f + i * 0.33f;
                    Color mc = rainbow ? RainbowPal[i % 6] : i % 2 == 0 ? s.Accent : Dark(s.Accent, 0.88f);
                    b.Add("Mane" + i, r.Head, head.C + new Vector3(0f, head.Rad.y * Mathf.Sin(th), head.Rad.z * Mathf.Cos(th)) * 1.02f, new Vector3(0.05f, 0.07f, 0.07f) * hs, _ => mc, 12, 8);
                }
                break;
            }
        }
    }

    private static void Spike(Builder b, int group, string name, Vector3 at, Vector3 dir, float size, Color c)
    {
        var sp = b.Add(name, group, at, new Vector3(size * 0.7f, size, size * 0.45f), _ => c, 10, 6);
        sp.Taper = -0.92f;
        sp.Q = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        sp.C = at + sp.Q * new Vector3(0f, size * 0.7f, 0f);
        sp.Matte = false;
    }

    private static void AddTail(Builder b)
    {
        var s = b.S;
        var r = b.R;
        Kind k = s.Kind;
        if (k == Kind.Person || k == Kind.Robot || k == Kind.Frog || k == Kind.Koala || s.Sub == "bat" || s.Extras.HasFlag(Extra.Cape)) return;   // 망토가 꼬리를 덮는다
        float bs = r.Bs, ch = s.Chub;
        r.Tail = b.AddGroup("Tail", 0, new Vector3(0f, 0.2f * bs, -0.18f * ch * bs), Quaternion.identity);
        Part t;
        // 꼬리 방향 dir 으로 세우고, 끝을 world 방향 bend 만큼 휘게
        void Aim(Part p, Vector3 dir, Vector3 bend) { p.Q = Quaternion.FromToRotation(Vector3.up, dir.normalized); p.Bend = Quaternion.Inverse(p.Q) * bend; }
        switch (k)
        {
            case Kind.Dog:
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.034f, 0.1f, 0.034f), sm => b.Fur(sm, s.Main), 12, 8);
                t.Taper = -0.6f; t.Q = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, 0.75f, -0.66f)); t.Bend = new Vector3(0f, 0f, 0.05f);
                break;
            case Kind.Cat:
            case Kind.Tiger:
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(k == Kind.Tiger ? 0.036f : 0.028f, 0.2f, k == Kind.Tiger ? 0.036f : 0.028f), sm => b.Fur(sm, s.Main), 12, 12);
                t.Taper = -0.35f; t.Q = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, 0.45f, -0.9f)); t.Bend = new Vector3(0f, 0.0f, 0.16f);
                break;
            case Kind.Lion:
            case Kind.Cow:
            case Kind.Elephant:   // 가는 꼬리 + 끝 술
            {
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.018f, 0.12f, 0.018f), sm => b.Fur(sm, s.Main), 10, 8);
                t.Q = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, -0.35f, -1f).normalized);
                t.C = t.Q * new Vector3(0f, 0.12f * 0.8f, 0f);
                Color tuft = k == Kind.Elephant ? Dark(s.Main, 0.6f) : s.Accent;
                b.Add("TailTuft", r.Tail, t.C + t.Q * new Vector3(0f, 0.12f, 0f), new Vector3(0.035f, 0.045f, 0.035f), _ => tuft, 10, 6);
                return;
            }
            case Kind.Mouse:
                switch (s.Sub)
                {
                    case "hamster":   // 작은 공
                        t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.03f, 0.03f, 0.03f), _ => s.Second, 10, 6);
                        break;
                    case "squirrel":   // 등 뒤로 말려 올라간 꼬리
                        t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.1f, 0.24f, 0.09f), sm => b.Fur(sm, sm.D.y > 0.7f ? Light2(s.Main, 0.25f) : s.Main), 16, 12);
                        t.Taper = -0.2f; Aim(t, new Vector3(0f, 0.75f, -0.66f), new Vector3(0f, 0.04f, 0.16f));
                        break;
                    default:   // 생쥐 — 가늘고 긴 분홍 꼬리
                        t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.014f, 0.17f, 0.014f), _ => new Color(1f, 0.72f, 0.76f), 10, 10);
                        t.Taper = -0.5f; Aim(t, new Vector3(0f, 0.15f, -1f), new Vector3(0.06f, 0.1f, 0f));
                        break;
                }
                break;
            case Kind.Monkey:   // 길게 말린 꼬리
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.022f, 0.2f, 0.022f), sm => b.Fur(sm, s.Main), 10, 12);
                t.Taper = -0.3f; Aim(t, new Vector3(0f, 0.3f, -1f), new Vector3(0f, 0.2f, 0.06f));
                break;
            case Kind.Raccoon:   // 줄무늬 굵은 꼬리
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.06f, 0.18f, 0.06f), sm => Mathf.Sin(sm.V * 30f) > 0.2f ? s.Accent : s.Main, 14, 12);
                t.Taper = -0.45f; Aim(t, new Vector3(0f, 0.35f, -1f), new Vector3(0f, 0.06f, 0f));
                break;
            case Kind.Dino:   // 굵고 끝이 가는 꼬리
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.09f, 0.2f, 0.09f), sm => b.Fur(sm, s.Main), 14, 12);
                t.Taper = -0.8f; Aim(t, new Vector3(0f, -0.3f, -1f), new Vector3(0f, 0.06f, 0f));
                break;
            case Kind.Horse:   // 흘러내리는 꼬리 (유니콘 · 무지개는 여섯 빛)
            {
                bool rainbow = s.Sub == "unicorn" || s.Rainbow;
                Color mc = s.Accent;
                t = b.Add("TailMesh", r.Tail, Vector3.zero, new Vector3(0.05f, 0.17f, 0.04f), sm => rainbow ? RainbowPal[Mathf.Clamp((int)(sm.V * 6f), 0, 5)] : mc, 14, 12);
                t.Taper = -0.5f; Aim(t, new Vector3(0f, -0.5f, -1f), new Vector3(0f, -0.08f, 0f));
                break;
            }
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
            case Kind.Owl:
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
            case Hat.Wizard:   // 넓은 챙 + 끝이 뒤로 꺾인 높은 고깔 · 금별
            {
                Color gold = new Color(1f, 0.84f, 0.35f);
                Vector3 baseC = head.C + new Vector3(0f, head.Rad.y * 0.68f, -0.01f);
                var brim = b.Add("Brim", r.Head, baseC, new Vector3(0.42f, 0.014f, 0.4f) * r.Hs, _ => Dark(hc, 0.85f), 30, 8);
                brim.Q = Quaternion.Euler(-8f, 0f, 0f);
                var cone = b.Add("Hat", r.Head, baseC + brim.Q * new Vector3(0f, 0.15f * r.Hs, 0f), new Vector3(head.Rad.x * 0.8f, 0.36f * r.Hs, head.Rad.z * 0.8f), null, 26, 14);
                cone.Taper = -0.95f; cone.Bend = new Vector3(0f, -0.05f, -0.16f) * r.Hs; cone.Q = brim.Q;
                cone.Paint = sm =>
                {
                    if (InStar(sm.Lon / 0.3f, (sm.Lat + 0.45f) / 0.3f)) return gold;
                    foreach (var (wx, wy) in Sparkles)
                        if (InStar((sm.Lon - wx * 1.1f) / 0.13f, (sm.Lat - wy - 0.2f) / 0.13f)) return Light2(gold, 0.3f);
                    return hc;
                };
                var band = b.Add("HatBand", r.Head, baseC + brim.Q * new Vector3(0f, 0.03f * r.Hs, 0f), new Vector3(0.16f, 0.16f, 0.16f) * r.Hs, _ => gold, 26, 6);
                band.Torus = true; band.Ring = 0.12f; band.Q = brim.Q; band.Matte = false;
                break;
            }
            case Hat.Party:   // 비스듬한 줄무늬 고깔 + 방울
            {
                Vector3 baseC = head.C + new Vector3(0.05f * r.Hs, head.Rad.y * 0.75f, 0f);
                var cone = b.Add("Hat", r.Head, Vector3.zero, new Vector3(0.12f, 0.2f, 0.12f) * r.Hs, sm => Mathf.Sin(sm.Lon * 3f + sm.Lat * 9f) > 0f ? hc : Light2(hc, 0.7f), 20, 10);
                cone.Taper = -0.96f; cone.Q = Quaternion.Euler(0f, 0f, -12f);
                cone.C = baseC + cone.Q * new Vector3(0f, 0.1f * r.Hs, 0f);
                b.Add("Pompom", r.Head, Pos(cone, 0.5f, 1f), new Vector3(0.045f, 0.045f, 0.045f) * r.Hs, _ => Light2(hc, 0.8f), 12, 8);
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
        if (s.Extras.HasFlag(Extra.Horns))   // 뿔 두 개 — 소는 옆으로 휘고, 나머지는 위로
        {
            Color hc = s.ExtraColor[Extra.Horns];
            bool cow = k == Kind.Cow;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = HP(side * (cow ? 0.6f : 0.35f), cow ? 0.85f : 1.0f, -0.01f, out Vector3 n);
                Vector3 dir = (n + new Vector3(side * (cow ? 0.9f : 0.15f), 0.4f, 0f)).normalized;
                float len = (cow ? 0.07f : 0.075f) * r.Hs;
                var horn = b.Add(side < 0 ? "HornL" : "HornR", r.Head, Vector3.zero, new Vector3(0.035f * r.Hs, len, 0.035f * r.Hs), sm => Mix(hc, Light2(hc, 0.4f), Mathf.Clamp01(sm.D.y)), 12, 8);
                horn.Taper = -0.85f;
                horn.Q = Quaternion.FromToRotation(Vector3.up, dir);
                horn.C = p + dir * (len * 0.75f);
                horn.Bend = Quaternion.Inverse(horn.Q) * (cow ? new Vector3(0f, 0.04f, 0f) : new Vector3(side * -0.01f, 0.01f, -0.03f));
                horn.Matte = false;
            }
        }
        if (s.Extras.HasFlag(Extra.Horn))   // 유니콘 뿔 — 이마에 하나, 꼬인 무늬
        {
            Color hc = s.ExtraColor[Extra.Horn];
            Vector3 p = HP(0f, 0.82f, -0.01f, out Vector3 n);
            Vector3 dir = (n + Vector3.up * 0.9f).normalized;
            float len = 0.12f * r.Hs;
            var horn = b.Add("Horn", r.Head, Vector3.zero, new Vector3(0.035f * r.Hs, len, 0.035f * r.Hs), sm => Mathf.Sin(sm.Lon * 2f + sm.V * 26f) > 0.3f ? Light2(hc, 0.45f) : hc, 14, 10);
            horn.Taper = -0.92f;
            horn.Q = Quaternion.FromToRotation(Vector3.up, dir);
            horn.C = p + dir * (len * 0.8f);
            horn.Matte = false;
        }
        if (s.Extras.HasFlag(Extra.Halo))   // 머리 위 금빛 고리
        {
            float hy = head.Rad.y * (s.Hat != Hat.None ? 1.75f : 1.32f);
            var halo = b.Add("Halo", r.Head, head.C + new Vector3(0f, hy, -0.03f), new Vector3(0.16f, 0.16f, 0.16f) * r.Hs, _ => s.ExtraColor[Extra.Halo], 24, 8);
            halo.Torus = true; halo.Ring = 0.13f; halo.Q = Quaternion.Euler(-12f, 0f, 0f); halo.Unshaded = true;
        }
        if (s.Extras.HasFlag(Extra.Wings))   // 등의 날개 — 천사는 깃털, 박쥐 · 용은 막
        {
            Color wc = s.ExtraColor[Extra.Wings];
            bool membrane = k == Kind.Dino || k == Kind.Mouse;
            for (int side = -1; side <= 1; side += 2)
            {
                int g = b.AddGroup(side < 0 ? "WingL" : "WingR", r.Body, new Vector3(side * 0.07f * ch * bs, 0.3f * bs, -0.17f * ch * bs), Quaternion.Euler(0f, side * 25f, 0f));
                if (side < 0) r.WingL = g; else r.WingR = g;
                Vector3 outDir = new Vector3(side, 0.45f, 0f).normalized;
                float len = 0.15f * bs;
                var wing = b.Add(side < 0 ? "WingLMesh" : "WingRMesh", g, Vector3.zero, new Vector3(0.085f * bs, len, 0.022f * bs), null, 16, 10);
                wing.Q = Quaternion.FromToRotation(Vector3.up, outDir);
                wing.C = wing.Q * new Vector3(0f, len * 0.85f, 0f);
                wing.Taper = -0.55f;
                wing.Bend = Quaternion.Inverse(wing.Q) * new Vector3(0f, 0.05f * bs, 0f);
                wing.Paint = sm =>
                {
                    Vector3 d = sm.D;
                    if (membrane)
                        return Mix(wc, Dark(wc, 0.6f), SmoothStep(0.9f, 0.98f, Mathf.Abs(Mathf.Cos(Mathf.Atan2(d.x, d.y + 1.2f) * 6f))));
                    return Mix(wc, Dark(wc, 0.88f), SmoothStep(0.75f, 0.95f, Mathf.Abs(Mathf.Sin(d.x * 7f + d.y * 2f))));
                };
            }
        }
        if (s.Extras.HasFlag(Extra.Cape))   // 어깨에서 등으로 내려오는 망토
        {
            Color cc = s.ExtraColor[Extra.Cape];
            var cape = b.Add("Cape", r.Body, new Vector3(0f, 0.2f * bs, -0.205f * ch * bs), new Vector3(0.2f * ch, 0.22f, 0.025f) * bs, sm => sm.D.z > 0f ? Dark(cc, 0.75f) : cc, 20, 12);
            cape.Taper = -0.4f; cape.Q = Quaternion.Euler(10f, 0f, 0f);
        }
        if (s.Extras.HasFlag(Extra.Bell))   // 목걸이 + 앞의 방울
        {
            Color bc = s.ExtraColor[Extra.Bell];
            var collar = b.Add("Collar", 0, new Vector3(0f, 0.43f * bs, 0f), new Vector3(0.15f * ch, 0.15f, 0.135f * ch) * bs, _ => new Color(0.85f, 0.25f, 0.27f), 26, 6);
            collar.Torus = true; collar.Ring = 0.16f;
            var bell = b.Add("Bell", 0, new Vector3(0f, 0.395f * bs, (0.135f * ch + 0.03f) * bs), new Vector3(0.036f, 0.036f, 0.036f) * bs,
                sm => Mathf.Max(Line(sm.D.x, sm.D.y, -0.7f, -0.3f, 0.7f, -0.3f, 0.12f, 0.04f), Ell(sm.D.x, sm.D.y, 0f, -0.62f, 0.14f, 0.14f, 0.04f)) > 0.5f && sm.D.z > 0f ? Dark(bc, 0.45f) : bc, 12, 8);
            bell.Matte = false;
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
        var wingL = new Quaternion[N]; var wingR = new Quaternion[N];
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
            float flap = 25f + 14f * Mathf.Sin(2f * w * t);   // 날갯짓 (기본 자세 25도 둘레)
            wingL[i] = Quaternion.Euler(0f, -flap, 0f);
            wingR[i] = Quaternion.Euler(0f, flap, 0f);
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
        if (r.WingL >= 0) b.W.AddRotation(b.Groups[r.WingL].Node, times, wingL);
        if (r.WingR >= 0) b.W.AddRotation(b.Groups[r.WingR].Node, times, wingR);
    }
}
