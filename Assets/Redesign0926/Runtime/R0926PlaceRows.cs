using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// PlaceListManager 가 한 덩어리로 쓰는 목록 글자("&lt;color=#..&gt;이름 - 42m&lt;/color&gt;")를 읽어
/// 한 줄에 한 장소(색 점 · 이름 · 방향 · 거리)로 다시 그린다. PlaceListManager 의 글자는 건드리지 않는다.
/// 원본 Text 는 숨긴 채 계속 쓰이게 두고, 바뀔 때만 다시 그린다.
/// 줄을 누르면 그 장소를 AR 에서 찾게 돕는다 — AR 에 떠 있으면 그 줄이 잠깐 분홍으로 밝혀졌다가 목록이 내려가고
/// AR 의 그 박스·화살표 글자 뒤에 분홍 스티커가 붙는다. 아직 없으면 목록은 그대로, 그 줄이 분홍으로 밝혀지고
/// 거리 칸에 '아직 AR화면 내에 없음'. 줄 ↔ 장소는 PlaceListManager.ShownEntries(글자와 같은 순서)로 잇는다.
/// 줄 앞 표시: 장소는 색 점 · 근처 사람은 사람 모양(핑크) · 보물찾기는 금빛 상자 — 보물찾기 줄을 누르면 그 게임 상세가 열린다.
/// 보물찾기 줄은 은은한 금빛 바탕 · 가는 금빛 테두리에 가끔 반짝인다 (상자 옆 별 · 지나가는 빛 띠).
/// 축제는 축제 분위기 그림(불꽃·등불·꽃…), 보호소는 강아지 — 누르면 그 축제를 찾은 쪽 · 카카오맵 장소 쪽이 열린다.
/// 줄 오른쪽 끝은 그 장소의 대표 사진(오브젝트 앞면과 같은 것) — 사진을 누르면 그 장소를 두 번 누른 것과 같은 정보 창이 열린다
/// (AR 에 아직 없는 장소도 · R0926PlaceInfo). 창이 떠 있는 동안 목록은 숨었다가 창을 닫으면 돌아온다. 사람은 프로필.
/// </summary>
public class R0926PlaceRows : MonoBehaviour
{
    [Header("원본 (PlaceListManager.listText)")]
    [SerializeField] private Text source;

    [Header("표시")]
    [SerializeField] private RectTransform rowContainer;
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private Text countText;
    [SerializeField] private Text summaryText;
    [Tooltip("한 번에 그리는 최대 줄 수 — 넘치면 가까운 순으로 자른다")]
    [SerializeField] private int maxRows = 150;

    [Header("불러오는 동안 (스켈레톤)")]
    [SerializeField] private Sprite barSprite;
    [SerializeField] private int skeletonRows = 7;
    [SerializeField] private float rowHeight = 84f;
    [Tooltip("이 시간이 지나도 아무것도 없으면 스켈레톤을 거둔다 (초)")]
    [SerializeField] private float skeletonTimeout = 12f;
    private GameObject skeleton;
    private readonly List<Image> skeletonBars = new List<Image>();
    private float openedAt;
    private bool loading;

    [Header("줄 누르기 · 방향")]
    [Tooltip("비워 두면 source 를 쓰는 PlaceListManager 를 찾는다")]
    [SerializeField] private PlaceListManager places;
    [Tooltip("비워 두면 장면에서 찾는다")]
    [SerializeField] private OffScreenIndicator indicators;
    [Tooltip("AR 로 돌아갈 때 내릴 시트 — 비워 두면 부모에서 찾는다 (목록 시트의 R0926SwipeDismiss)")]
    [SerializeField] private R0926Closer sheet;
    [Tooltip("누르는 동안 줄 바탕 밝기")]
    [SerializeField] private float pressAlpha = 0.06f;
    [SerializeField] private bool showDirection = true;
    [Tooltip("방향 글자 자리 — 거리 칸을 이만큼 넓히고 이름 칸은 그만큼 좁힌다")]
    [SerializeField] private float directionWidth = 130f;
    [SerializeField] private int directionFontSize = 27;
    [Tooltip("방향을 다시 계산하는 간격 (초)")]
    [SerializeField] private float directionInterval = 0.25f;
    [Tooltip("AR 에 아직 없는 장소를 눌렀을 때 그 줄을 밝히고 거리 칸에 '아직 AR화면 내에 없음' 을 보여 주는 시간 (초)")]
    [SerializeField] private float notInArNoteDuration = 2.5f;
    [Tooltip("AR 에 떠 있는 장소를 누르면 그 줄을 이만큼(초) 밝혔다가 목록을 내린다")]
    [SerializeField] private float tapFlashDuration = 0.35f;
    private static readonly Color FlashPink = new Color(0.914f, 0.325f, 0.514f, 0.26f);
    private const float FlashFadeIn = 0.08f, FlashFadeOut = 0.35f;
    private float flashEnd, closeAt = -1f;

    [Header("줄 앞 표시 — 장소는 분류 그림 (분류가 없으면 템플릿의 색 점)")]
    [SerializeField] private Sprite personSprite;
    [SerializeField] private Sprite huntSprite;
    [Tooltip("비밀번호를 건 보물 — 보물 그림 + 자물쇠 배지")]
    [SerializeField] private Sprite huntLockSprite;
    [Tooltip("그림 크기 — 그림마다 둘레 여백이 달라 따로 둔다 (보이는 크기를 이름 글자에 맞춘다)")]
    [SerializeField] private float personIconSize = 48f;
    [SerializeField] private float huntIconSize = 44f;
    [Tooltip("축제·보호소 그림 (r0926_f_<이름> — 서버가 축제마다 고른 이름, 보호소는 shelter)")]
    [SerializeField] private Sprite[] kindIcons;
    [SerializeField] private float kindIconSize = 50f;
    [Tooltip("장소 분류 그림 (r0926_c_<분류> — 필터 칩·올리기 칩과 같은 그림 · 공공데이터 분류 · 교통 · 관광 · 3D · 기타). 크기는 kindIconSize")]
    [SerializeField] private Sprite[] catIcons;
    [Tooltip("그림 가운데 — 줄 왼쪽 끝에서")]
    [SerializeField] private float iconCenterX = 26f;

    [Header("줄 오른쪽 사진 — 그 장소의 대표 사진, 누르면 두 번 누른 것과 같은 정보 창 (11-12 대표님 「우측 끝에는 각 오브젝트의 '메인사진'」)")]
    [Tooltip("한 번에 받는 사진 수")]
    [SerializeField] private int photoParallel = 3;
    [Tooltip("받아 둔 사진을 이만큼까지 둔다 — 넘으면 오래 안 본 것부터 놓는다. 보이는 줄과 위아래 한 화면만 받는다 (10-23 아이폰 메모리)")]
    [SerializeField] private int photoCacheSize = 40;
    [Tooltip("받지 못한 사진·정보는 이만큼(초) 뒤에 다시")]
    [SerializeField] private float photoRetry = 30f;
    [Tooltip("AR 에 아직 없는 우팡 장소 — 사진을 누른 뒤 정보를 받는 동안 기다리는 최대 시간 (초)")]
    [SerializeField] private float infoWait = 8f;
    private const float PhotoInterval = 0.2f, PhotoFadeIn = 0.2f;
    private const int PhotoThumb = 256;   // 우팡 서버 사진은 작은 사본 (/thumb) — 칸 160 은 기기에서 약 140px
    private class Pic { public Texture2D tex; public bool owned; public float used; }
    private readonly Dictionary<string, Pic> pics = new Dictionary<string, Pic>();
    private readonly Dictionary<string, float> picFailed = new Dictionary<string, float>();
    private readonly HashSet<string> picLoading = new HashSet<string>();
    private readonly List<UnityWebRequest> picRequests = new List<UnityWebRequest>();
    private int picGen;            // 목록을 닫으면 올린다 — 그 뒤에 도착한 사진은 버린다
    private float nextPhotoAt;
    private ScrollRect scroll;
    private readonly Vector3[] corners = new Vector3[4];
    // AR 에 아직 없는 우팡 장소 — 가벼운 목록에는 사진 주소가 없어 보이는 줄만 상세를 받는다 (/locations_detail, 한 번에 50곳까지)
    private readonly Dictionary<int, PlaceData> details = new Dictionary<int, PlaceData>();
    private readonly HashSet<int> detailWant = new HashSet<int>();
    private readonly Dictionary<int, float> detailFailed = new Dictionary<int, float>();
    private UnityWebRequest detailRequest;
    private Coroutine infoRoutine;

    [Header("보물찾기 줄 — 은은한 금빛 바탕 · 테두리 · 가끔 반짝 (10-27 대표님 「'보물찾기' 리스트의 경우 살짝 하이라이트, 반짝임 등이 있어서 좀 구분되서 보이도록」)")]
    [Tooltip("금빛 테두리 (r0926_pill_ring — barSprite 와 같은 9-slice)")]
    [SerializeField] private Sprite huntRingSprite;
    [Tooltip("줄을 가로질러 지나가는 빛 띠 (r0926_shine)")]
    [SerializeField] private Sprite shineSprite;
    [Tooltip("상자 오른쪽 위에서 반짝이는 별 (r0926_re_spark)")]
    [SerializeField] private Sprite twinkleSprite;
    [Tooltip("반짝임 간격 (초) — 한 번에 별이 반짝이고 빛 띠가 왼쪽에서 오른쪽으로 지나간다")]
    [SerializeField] private float huntShinePeriod = 3.2f;
    private static readonly Color HuntTint = new Color(0.910f, 0.749f, 0.310f, 0.05f);   // 지도 보물 핀 테두리와 같은 금빛 (선형 색 공간이라 낮은 값도 꽤 진하다)
    private static readonly Color HuntRing = new Color(0.910f, 0.749f, 0.310f, 0.32f);
    private static readonly Color ShineColor = new Color(1f, 0.93f, 0.75f, 0.24f);
    private const float ShineStart = 0.15f, ShineTime = 0.9f, TwinkleTime = 0.5f, HuntStagger = 0.22f, ShineEdge = 72f;
    private int huntRows;

    private const float Hysteresis = 10f;        // 경계에서 방향 글자가 깜빡이지 않게 (도)
    private const float GpsMinDistance = 10f;   // GPS 오차 안쪽은 나침반 방향을 쓰지 않는다 (m)

    private float nextDirAt;
    private bool hasCam, hasHeading;
    private Vector3 camPos, camFlat;
    private float heading, userLat, userLon;
    private bool compassOwned, compassFailed;

    private static readonly Regex RowRx =
        new Regex(@"^<color=#([0-9A-Fa-f]{6,8})>(.*) - (\d+)m</color>$", RegexOptions.Compiled);
    private static readonly Regex StatRx = new Regex(@"^(.+?):\s*(\d+)$", RegexOptions.Compiled);
    private static readonly Regex EnglishNamed = new Regex(@"[A-Za-z].*\([가-힣0-9\s]+\)\s*$", RegexOptions.Compiled);

    private readonly List<Row> rows = new List<Row>();
    private Dictionary<string, Sprite> kindIconMap;
    private const string KindIconPrefix = "r0926_f_";
    private Dictionary<string, Sprite> catIconMap;
    /// <summary>애견동반 되는 장소의 이름 — 애견동반 칩과 같은 노랑 (10-27 대표님 「'애견동반' 의 경우 목록에서 글자색이 흰색으로 발생하지 않고 노란색으로」)</summary>
    public static readonly Color PetName = new Color32(0xFF, 0xC1, 0x07, 0xFF);
    private const string CatIconPrefix = "r0926_c_";

    public struct Entry { public string name; public string distance; }
    private readonly List<Entry> entries = new List<Entry>();
    /// <summary>지금 목록에 보이는 장소 (가까운 순) — AI에게 묻기 등에서 쓴다</summary>
    public IReadOnlyList<Entry> Entries => entries;
    private readonly StringBuilder sb = new StringBuilder(128);
    private string last;
    private bool korean = true;
    private int nearVer = -1;   // 근처 데이터 판 — 광고 줄 이모지를 받으면 다시 그린다

    private class Row
    {
        public GameObject go;
        public Image dot;
        public Text name;
        public Text dist;
        public int index;          // 목록 글자에서 몇 번째 장소 줄인지 (= ShownEntries 순번)
        public string key;         // 글자 줄의 이름 그대로 — ShownEntries 와 맞춰 본다
        public string distLabel;   // "42m"
        public string shownLabel;  // 지금 거리 칸에 쓴 거리
        public int dir = -2;       // 지금 보이는 방향: 0 앞 · 1 오른쪽 · 2 뒤 · 3 왼쪽 · -1 없음 · -2 다시 그리기
        public float noteUntil;    // 이 시각까지 거리 칸에 '아직 AR화면 내에 없음'
        public Image flash;        // 누른 줄 분홍 바탕
        public float flashFrom, flashUntil;
        public bool noDirection;   // 방향을 쓰지 않는 줄 — 비공개 보물찾기 · 대략 공개 원 안
        public PlaceListManager.EntryKind kind;   // 지금 줄 앞 표시 (템플릿 = 장소)
        public Sprite dotSprite;   // 템플릿의 색 점 — 장소로 돌아올 때
        public Vector2 dotPos, dotSize;
        public Color nameColor;    // 템플릿의 이름 색 — 애견동반이 아닌 줄
        public Image huntGlow;     // 보물찾기 줄 금빛 바탕 — 빛 띠를 그 안에서만 보이게 담는다
        public Image huntShine, huntTwinkle;
        public int huntOrder = -1; // 보물찾기 줄 중 몇 번째 (차례로 반짝) · 보물찾기가 아니면 -1
        public RectTransform photoBox;   // 줄 오른쪽 사진 칸 (템플릿 Photo) — 옛 템플릿이면 null
        public RawImage photo;
        public Image photoIcon;          // 사진이 없거나 받는 동안 — 줄 앞 그림을 옅게
        public string photoKey;          // 이 줄 장소의 사진: 주소 · "res:" 앱 그림 · "" 없음 · null 아직 모름 (상세를 받는 중)
        public string photoShown;        // 칸에 올라가 있는 사진
        public int near;                 // 0 화면 안 · 1 위아래 한 화면 안 (미리 받는다) · 2 그 밖
    }

    private void Awake()
    {
        if (source != null)
        {
            // 원본은 보이지 않게만 — PlaceListManager·스켈레톤이 계속 참조한다
            source.enabled = false;
            var le = source.GetComponent<LayoutElement>();
            if (le == null) le = source.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
        if (rowTemplate != null) rowTemplate.SetActive(false);
        korean = R0926LocalizedText.Lang() == "ko";
        if (summaryText != null)
        {
            summaryText.resizeTextMaxSize = summaryText.fontSize;
            summaryText.resizeTextMinSize = 26;
        }
        ResolveRefs();
    }

    // 적용 스크립트가 잇지 않은 참조는 장면에서 찾는다
    private void ResolveRefs()
    {
        if (places == null)
        {
            foreach (var m in FindObjectsByType<PlaceListManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (m.listText == source) { places = m; break; }
            if (places == null) places = FindFirstObjectByType<PlaceListManager>(FindObjectsInactive.Include);
        }
        if (indicators == null) indicators = FindFirstObjectByType<OffScreenIndicator>(FindObjectsInactive.Include);
        if (sheet == null) sheet = GetComponentInParent<R0926Closer>(true);
    }

    private void OnEnable()
    {
        openedAt = Time.unscaledTime;
        last = null;   // 다시 열면 한 번 새로 그린다 (스켈레톤 판정 포함)
        nextDirAt = 0f;
        closeAt = -1f;
        flashEnd = 0f;
        foreach (var r in rows)
        {
            r.flashUntil = 0f;
            if (r.flash != null) r.flash.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // 목록이 열려 있는 동안만 나침반을 쓴다 (우리가 켠 경우에만 끈다)
        if (compassOwned) { Input.compass.enabled = false; compassOwned = false; }
        closeAt = -1f;
        EndCover();
        infoRoutine = null;   // 코루틴은 꺼지며 멈췄다
        ReleasePhotos();
    }

    private void LateUpdate()
    {
        UpdateCover();
        if (source == null) return;
        string t = source.text;
        if (t != last || nearVer != R0926Nearby.Version)
        {
            last = t;
            nearVer = R0926Nearby.Version;
            Rebuild(t);
        }
        else if (Time.unscaledTime >= nextDirAt) RefreshDirections();
        if (Time.unscaledTime >= nextPhotoAt) UpdatePhotos();
        if (flashEnd > 0f)
        {
            UpdateFlashes();
            if (Time.unscaledTime > flashEnd) flashEnd = 0f;
        }
        if (huntRows > 0) UpdateHuntShine();
        if (closeAt > 0f && Time.unscaledTime >= closeAt)
        {
            closeAt = -1f;
            if (sheet != null) sheet.AnimateClose(null);   // X 와 같은 길: 시트가 내려간 뒤 닫기 버튼
        }
        if (loading)
        {
            if (Time.unscaledTime - openedAt > skeletonTimeout) SetSkeleton(false);
            else
            {
                float a = 0.05f + 0.035f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f));
                foreach (var b in skeletonBars) { var c = b.color; c.a = a; b.color = c; }
            }
        }
    }

    // 목록이 비어 있고 안내 문구도 없으면 '불러오는 중' — 회색 줄이 은은히 깜빡인다
    private void SetSkeleton(bool on)
    {
        loading = on;
        if (!on) { if (skeleton != null && skeleton.activeSelf) skeleton.SetActive(false); return; }
        if (skeleton == null) BuildSkeleton();
        if (skeleton == null) return;
        skeleton.transform.SetAsLastSibling();
        if (!skeleton.activeSelf) skeleton.SetActive(true);
    }

    private void BuildSkeleton()
    {
        if (rowContainer == null) return;
        skeleton = new GameObject("Skeleton0926", typeof(RectTransform), typeof(LayoutElement));
        skeleton.layer = gameObject.layer;
        skeleton.transform.SetParent(rowContainer, false);
        skeleton.GetComponent<LayoutElement>().preferredHeight = skeletonRows * rowHeight;
        float[] widths = { 0.46f, 0.62f, 0.38f, 0.55f, 0.7f, 0.42f, 0.5f, 0.6f };
        // 줄 오른쪽 사진 칸 — 템플릿(Photo)과 같은 자리 · 크기
        var ph = rowTemplate != null ? rowTemplate.transform.Find("Photo") as RectTransform : null;
        float photoW = ph != null ? ph.sizeDelta.x : 0f, photoRight = ph != null ? -ph.anchoredPosition.x : 0f;
        float distRight = ph != null ? photoRight + photoW + 24f : 24f;
        // 그림 · 이름 자리 — 템플릿과 같게 (11-13 줄 앞 그림 · 글자를 키웠다)
        var dt = rowTemplate != null ? rowTemplate.transform.Find("Dot") as RectTransform : null;
        var nt = rowTemplate != null ? rowTemplate.transform.Find("Name") as RectTransform : null;
        float iconX = dt != null ? Mathf.Max(dt.anchoredPosition.x, iconCenterX) : 35f;
        float iconS = Mathf.Max(22f, Mathf.Min(kindIconSize, rowHeight - 24f) * 0.8f);
        float nameLeft = nt != null ? nt.offsetMin.x : 64f;
        var nameText = nt != null ? nt.GetComponent<Text>() : null;
        float nameBarH = nameText != null ? Mathf.Round(nameText.fontSize * 0.55f) : 26f;
        for (int i = 0; i < skeletonRows; i++)
        {
            float y = -(i + 0.5f) * rowHeight;
            Bar(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(iconX, y), new Vector2(iconS, iconS), 0f, iconS * 0.5f);   // 그림 자리
            Bar(new Vector2(0f, 1f), new Vector2(widths[i % widths.Length], 1f), new Vector2(0f, y), new Vector2(0f, nameBarH), nameLeft); // 이름
            Bar(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-distRight - 50f, y), new Vector2(100f, 22f));           // 거리
            if (ph != null)
                Bar(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-photoRight - photoW * 0.5f, y), new Vector2(photoW, photoW), 0f, 28f);   // 사진
        }
    }

    private void Bar(Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, float leftInset = 0f, float radius = 11f)
    {
        var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(skeleton.transform, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
        if (leftInset > 0f)
        {
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(leftInset, pos.y);
            rt.sizeDelta = new Vector2(-leftInset, size.y);
        }
        else { rt.anchoredPosition = pos; rt.sizeDelta = size; }
        var img = go.GetComponent<Image>();
        img.sprite = barSprite;
        img.type = barSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = 64f / radius;
        img.color = new Color(1f, 1f, 1f, 0.06f);
        img.raycastTarget = false;
        skeletonBars.Add(img);
    }

    private void Rebuild(string text)
    {
        int used = 0;
        huntRows = 0;
        sb.Length = 0;
        entries.Clear();
        string message = null;

        foreach (var raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            var m = RowRx.Match(line);
            if (m.Success)
            {
                if (used >= maxRows) continue;
                int index = used++;
                Row r = GetRow(index);
                r.index = index;
                string key = m.Groups[2].Value;
                bool known = FindEntry(index, key, out var e);
                // 근처 사람은 이름이 @아이디 — 장소 정보와 아직 못 맞춘 프레임에도 사람 모양으로
                var kind = known ? e.kind : key.StartsWith("@") ? PlaceListManager.EntryKind.Person : PlaceListManager.EntryKind.Place;
                SetIcon(r, kind, ParseHex(m.Groups[1].Value), known ? e.icon : null, known ? e.emojiUrl : null);
                SetHuntGlow(r, kind == PlaceListManager.EntryKind.Hunt ? huntRows++ : -1);
                SetPhoto(r, known, e);
                if (known && e.promo) R0926Nearby.AdView(this, e.adId, "festival");   // 광고 줄은 축제 칩에만 온다
                string label = key;
                // 한국어가 아닌 기기: 한글 이름 옆에 로마자 읽는 법을 옅게 (번역이 아니라 읽기 도움) — 'Seoul (Station) (서울역)' 처럼 영어 이름이 이미 있으면 그대로
                string roman = korean || EnglishNamed.IsMatch(label) ? null : R0926Romanizer.Romanize(label);
                R0926EmojiText.Set(r.name, roman == null ? label : label + "  <color=#6F7880>" + roman + "</color>");   // 이름 속 이모지는 우팡 이모지 그림으로
                r.name.color = known && e.pet ? PetName : r.nameColor;
                if (r.key != key) { r.key = key; r.dir = -2; r.noteUntil = 0f; r.flashUntil = 0f; }   // 다른 장소가 이 줄로 왔다 — 방향을 새로
                // 보물찾기는 게임의 공개 설정대로 ('약 300m' · '범위 안' · '1km 이내')
                r.distLabel = known && e.distanceText != null ? e.distanceText : FormatDistance(int.Parse(m.Groups[3].Value));
                r.noDirection = known && e.noDirection;
                entries.Add(new Entry { name = label, distance = r.distLabel });
                continue;
            }

            var s = StatRx.Match(line);
            if (s.Success)
            {
                if (sb.Length > 0) sb.Append("  ·  ");
                sb.Append(s.Groups[1].Value.Trim()).Append(' ').Append(s.Groups[2].Value);
                continue;
            }

            // 안내 문구(데이터 없음 등)
            message = message == null ? line : message + "\n" + line;
        }

        for (int i = used; i < rows.Count; i++) rows[i].go.SetActive(false);
        RefreshDirections();   // 거리 칸은 여기서 쓴다 (방향과 함께)

        if (countText != null) countText.text = used > 0 ? used.ToString() : "";
        if (summaryText != null)
        {
            summaryText.resizeTextForBestFit = message == null;   // 종류가 많은 날 뒤쪽이 줄 밖으로 사라지지 않게 — 넘칠 때만 글자를 줄인다
            summaryText.text = message ?? sb.ToString();
        }
        SetSkeleton(used == 0 && message == null && Application.isPlaying && Time.unscaledTime - openedAt <= skeletonTimeout);
    }

#if UNITY_EDITOR
    // 에디터 캡처용 미리보기 — 끝나면 반드시 EditorPreviewClear 로 원상복구
    private bool previewSourceEnabled;

    public void EditorPreview(string sample)
    {
        if (source != null) { previewSourceEnabled = source.enabled; source.enabled = false; }
        if (rowTemplate != null) rowTemplate.SetActive(false);
        Rebuild(sample);
    }

    public void EditorPreviewClear()
    {
        foreach (var r in rows) if (r.go != null) DestroyImmediate(r.go);
        rows.Clear();
        last = null;
        if (source != null) source.enabled = previewSourceEnabled;
        if (countText != null) countText.text = "";
        if (summaryText != null) summaryText.text = "";
    }
#endif

    private Row GetRow(int i)
    {
        while (rows.Count <= i)
        {
            var go = Instantiate(rowTemplate, rowContainer);
            go.name = "Row";
            var nr = new Row
            {
                go = go,
                dot = go.transform.Find("Dot").GetComponent<Image>(),
                name = go.transform.Find("Name").GetComponent<Text>(),
                dist = go.transform.Find("Dist").GetComponent<Text>(),
            };
            nr.dotSprite = nr.dot.sprite;
            nr.dotPos = nr.dot.rectTransform.anchoredPosition;
            nr.dotSize = nr.dot.rectTransform.sizeDelta;
            nr.nameColor = nr.name.color;
            var pb = go.transform.Find("Photo") as RectTransform;
            if (pb != null)
            {
                nr.photoBox = pb;
                var pic = pb.Find("Pic");
                var ic = pb.Find("Icon");
                nr.photo = pic != null ? pic.GetComponent<RawImage>() : null;
                nr.photoIcon = ic != null ? ic.GetComponent<Image>() : null;
                if (nr.photo == null) nr.photoBox = null;
            }
            SetupRow(nr);
            rows.Add(nr);
        }
        var row = rows[i];
        if (!row.go.activeSelf) row.go.SetActive(true);
        return row;
    }

    // 새 줄 준비 — 템플릿이 꺼져 있어 줄도 꺼진 채 만들어진다 (켜질 때 버튼 색이 바로 맞는다)
    private void SetupRow(Row row)
    {
        // 줄 전체를 누를 수 있게: 투명 바탕 + 버튼 (누르는 동안만 살짝 밝아진다). 끌기는 그대로 목록 스크롤·시트 내리기로 간다
        var go = row.go;
        var bg = go.GetComponent<Image>();
        if (bg == null) bg = go.AddComponent<Image>();
        bg.color = Color.white;
        bg.raycastTarget = true;
        bg.canvasRenderer.cullTransparentMesh = false;   // 투명해도 눌리게
        bg.canvasRenderer.SetAlpha(0f);                  // 버튼이 색을 맞추기 전에도 보이지 않게 (에디터 캡처 포함)
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.ColorTint;
        var clear = new Color(1f, 1f, 1f, 0f);
        var cb = ColorBlock.defaultColorBlock;
        cb.normalColor = clear;
        cb.highlightedColor = clear;
        cb.selectedColor = clear;
        cb.disabledColor = clear;
        cb.pressedColor = new Color(1f, 1f, 1f, pressAlpha);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(() => OnRowTap(row));

        // 누른 줄 분홍 바탕 — 글자 뒤 (첫 자식), 평소엔 꺼 둔다
        var fl = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        fl.layer = go.layer;
        var frt = (RectTransform)fl.transform;
        frt.SetParent(go.transform, false);
        frt.SetAsFirstSibling();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(6f, 6f); frt.offsetMax = new Vector2(-6f, -6f);
        row.flash = fl.GetComponent<Image>();
        row.flash.sprite = barSprite;
        row.flash.type = barSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        if (barSprite != null) row.flash.pixelsPerUnitMultiplier = barSprite.border.x / 22f;   // 모서리 반지름 ≈ 22
        row.flash.color = FlashPink;
        row.flash.raycastTarget = false;
        fl.SetActive(false);

        // 보물찾기 줄 금빛 바탕 — 분홍 바탕보다도 뒤 (첫 자식). 빛 띠는 이 바탕 안에서만 보인다. 보물찾기 줄일 때만 켠다
        var hg = new GameObject("HuntGlow", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        hg.layer = go.layer;
        var hrt = (RectTransform)hg.transform;
        hrt.SetParent(go.transform, false);
        hrt.SetAsFirstSibling();
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
        hrt.offsetMin = new Vector2(-6f, 6f); hrt.offsetMax = new Vector2(6f, -6f);   // 좌우로 조금 넓게 — 상자 그림·거리 글자가 테두리에 붙지 않게 (목록 칸 여백 8 안)
        row.huntGlow = hg.GetComponent<Image>();
        row.huntGlow.sprite = barSprite;
        row.huntGlow.type = row.flash.type;
        row.huntGlow.pixelsPerUnitMultiplier = row.flash.pixelsPerUnitMultiplier;
        row.huntGlow.color = HuntTint;
        row.huntGlow.raycastTarget = false;
        if (huntRingSprite != null)
        {
            var ring = Decor("Ring", hrt, huntRingSprite, HuntRing);
            var rrt = ring.rectTransform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            ring.type = row.huntGlow.type;
            ring.pixelsPerUnitMultiplier = row.huntGlow.pixelsPerUnitMultiplier;
        }
        if (shineSprite != null)
        {
            // 살짝 기운 빛 띠 — 둥근 모서리에 걸리지 않게 곧은 가운데 구간만 지나간다
            row.huntShine = Decor("Shine", hrt, shineSprite, ShineColor);
            var srt = row.huntShine.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(64f, 124f * Mathf.Max(1f, rowHeight / 96f));   // 줄이 높아지면 띠도 길게 (11-12 두 배 높이)
            srt.localRotation = Quaternion.Euler(0f, 0f, -18f);
            row.huntShine.gameObject.SetActive(false);
        }
        hg.SetActive(false);
        if (twinkleSprite != null)
        {
            // 상자 오른쪽 위 작은 별 — 줄 앞 그림을 따라간다
            row.huntTwinkle = Decor("Twinkle", row.dot.rectTransform, twinkleSprite, Color.white);
            var trt = row.huntTwinkle.rectTransform;
            trt.anchorMin = trt.anchorMax = Vector2.one;
            trt.anchoredPosition = new Vector2(-2f, -4f);
            float tw = Mathf.Max(28f, Mathf.Round(huntIconSize * 0.4f));   // 상자 그림이 커지면 별도 같이 (11-13)
            trt.sizeDelta = new Vector2(tw, tw);
            row.huntTwinkle.preserveAspect = true;
            row.huntTwinkle.gameObject.SetActive(false);
        }

        if (showDirection && directionWidth > 0f)
        {
            // 방향 글자 자리 — 거리 칸(오른쪽 기준)을 왼쪽으로 넓히고 이름 칸은 그만큼 좁힌다. 길어도 거리가 잘리지 않게 한 줄로
            row.dist.rectTransform.sizeDelta += new Vector2(directionWidth, 0f);
            row.name.rectTransform.offsetMax -= new Vector2(directionWidth, 0f);
            row.dist.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        if (row.photoBox != null)
        {
            // 사진을 누르면 그 장소의 정보 창 (줄을 누르면 AR 에서 찾기 — 그대로). 누르는 동안 사진이 살짝 어두워진다 (Press)
            var press = row.photoBox.Find("Press");
            var pg = press != null ? press.GetComponent<Graphic>() : null;
            var pbtn = row.photoBox.GetComponent<Button>();
            if (pbtn == null) pbtn = row.photoBox.gameObject.AddComponent<Button>();
            pbtn.targetGraphic = pg != null ? pg : row.photoBox.GetComponent<Graphic>();
            pbtn.transition = pg != null ? Selectable.Transition.ColorTint : Selectable.Transition.None;
            var pcb = ColorBlock.defaultColorBlock;
            pcb.normalColor = clear;
            pcb.highlightedColor = clear;
            pcb.selectedColor = clear;
            pcb.disabledColor = clear;
            pcb.pressedColor = Color.white;
            pcb.colorMultiplier = 1f;
            pcb.fadeDuration = 0.08f;
            pbtn.colors = pcb;
            pbtn.navigation = new Navigation { mode = Navigation.Mode.None };
            pbtn.onClick.AddListener(() => OnPhotoTap(row));
            if (pg != null) pg.canvasRenderer.SetAlpha(0f);
            row.photo.enabled = false;
        }
    }

    // 줄 앞 표시 — 장소는 분류 그림(10-27 대표님 「'전체' 탭을 눌렀을 때 발생하는 각 이모지가 '목록' 에서도」), 사람은 사람 모양(핑크 그대로),
    // 보물찾기는 금빛 상자 · 축제·보호소는 제 그림 · 광고 줄은 고른 이모지. 그림은 제 색 그대로 — 그림이 없으면(분류 없는 장소) 줄 색 점
    private void SetIcon(Row r, PlaceListManager.EntryKind kind, Color color, string icon, string emojiUrl)
    {
        Sprite s = kind == PlaceListManager.EntryKind.Person ? personSprite
                 : kind == PlaceListManager.EntryKind.Hunt ? (icon == "lock" && huntLockSprite != null ? huntLockSprite : huntSprite)
                 : kind == PlaceListManager.EntryKind.Festival || kind == PlaceListManager.EntryKind.Shelter ? KindIcon(icon)
                 : CatIcon(icon);
        var emoji = R0926Nearby.Emoji(emojiUrl, this);   // 받는 동안은 축제 기본 그림
        if (emoji != null) s = emoji;
        r.dot.color = s == null || kind == PlaceListManager.EntryKind.Person ? color : Color.white;
        if (kind == r.kind && r.dot.sprite == (s != null ? s : r.dotSprite)) return;   // 줄은 다시 쓰인다 — 그림 있던 줄이 점으로 돌아가는 것도 본다
        r.kind = kind;
        var rt = r.dot.rectTransform;
        if (s == null)
        {
            r.dot.sprite = r.dotSprite;
            r.dot.preserveAspect = false;
            rt.anchoredPosition = r.dotPos;
            rt.sizeDelta = r.dotSize;
            return;
        }
        float size = kind == PlaceListManager.EntryKind.Person ? personIconSize
                   : kind == PlaceListManager.EntryKind.Hunt ? huntIconSize : kindIconSize;
        r.dot.sprite = s;
        r.dot.preserveAspect = true;
        rt.anchoredPosition = new Vector2(iconCenterX, r.dotPos.y);
        rt.sizeDelta = new Vector2(size, size);
    }

    // 장소 분류 그림 — 분류 이름으로 (모르는 분류·분류 없음은 null → 색 점)
    private Sprite CatIcon(string key)
    {
        if (string.IsNullOrEmpty(key) || catIcons == null || catIcons.Length == 0) return null;
        if (catIconMap == null)
        {
            catIconMap = new Dictionary<string, Sprite>();
            foreach (var sp in catIcons)
                if (sp != null) catIconMap[sp.name.StartsWith(CatIconPrefix) ? sp.name.Substring(CatIconPrefix.Length) : sp.name] = sp;
        }
        return catIconMap.TryGetValue(key, out var s) ? s : null;
    }

    // 축제·보호소 그림 — 이름으로 (모르는 이름은 축제 기본 그림)
    private Sprite KindIcon(string key)
    {
        if (kindIcons == null || kindIcons.Length == 0) return null;
        if (kindIconMap == null)
        {
            kindIconMap = new Dictionary<string, Sprite>();
            foreach (var sp in kindIcons)
                if (sp != null) kindIconMap[sp.name.StartsWith(KindIconPrefix) ? sp.name.Substring(KindIconPrefix.Length) : sp.name] = sp;
        }
        if (!string.IsNullOrEmpty(key) && kindIconMap.TryGetValue(key, out var s)) return s;
        return kindIconMap.TryGetValue("party", out s) ? s : null;
    }

    // ── 줄 누르기 ─────────────────────────────────────────

    private void OnRowTap(Row r)
    {
        if (places == null || !TryGetEntry(r, out var e)) return;
        if (e.kind == PlaceListManager.EntryKind.Hunt) { R0926HuntPush.OpenHunt(e.huntId); return; }   // 그 게임 상세 (목록은 뒤로가기 순서대로 닫힌다)
        if (e.kind == PlaceListManager.EntryKind.Festival || e.kind == PlaceListManager.EntryKind.Shelter)
        {
            // AR 에 띄우지 않는 줄 — 축제는 이름으로 찾은 쪽(날짜·장소·포스터), 보호소는 카카오맵 장소 쪽(전화·길찾기), 광고 줄은 업체 링크(클릭을 센다)
            if (string.IsNullOrEmpty(e.link)) return;
            Flash(r, tapFlashDuration);
            if (e.promo) R0926Nearby.AdClick(this, e.adId, "festival", e.link);
            else R0926Nearby.Open(e.link);
            return;
        }
        Target t = places.FindTarget(e);
        // 노치가 있는 아이폰 — 노치에 화살표 · 거리로 그 장소까지 안내 (멀어서 AR 에 없어도 · 11-09 대표님 「노치 화면에 화살표로 방향과 거리를」)
        bool guide = !e.noDirection && R0926Notch.Guide(e.lat, e.lon, t, e.name);
        if (t != null && indicators != null && indicators.Focus(t, tapFlashDuration + 0.1f))
        {
            // AR 에 떠 있다 — 그 줄을 잠깐 밝혔다가 목록을 내리고, 내려가는 동안 AR 의 그 박스·화살표 글자 뒤에 분홍 스티커
            Flash(r, tapFlashDuration);
            closeAt = Time.unscaledTime + tapFlashDuration;
            return;
        }
        if (guide)
        {
            // 아직 AR 에 없어도 — 줄을 잠깐 밝히고 목록을 내린다. 안내는 노치가 이어 간다 (처음 3초는 이름)
            Flash(r, tapFlashDuration);
            closeAt = Time.unscaledTime + tapFlashDuration;
            return;
        }
        // 아직 AR 에 없다 (멀거나 불러오는 중) — 목록은 그대로, 그 줄을 밝히고 거리 칸에 알려 준다 (방향·거리는 줄에 이미 보인다).
        // 토스트가 아니라 그 줄에 — 어느 장소 얘기인지 바로 보인다
        Flash(r, notInArNoteDuration);
        r.noteUntil = Time.unscaledTime + notInArNoteDuration;
        r.dir = -2;   // 안내가 끝나면 방향·거리를 다시 그린다
        r.dist.text = "<size=" + NoteFontSize + "><color=#FFFFFF>"
                    + L("아직 AR화면 내에 없음", "Not in AR view yet", "まだAR画面にありません", "尚未出现在AR画面中", "Aún no está en la vista AR")
                    + "</color></size>";
    }

    // ── 줄 오른쪽 사진 ─────────────────────────────────────

    // 장소가 바뀐 줄은 받은 사진을 내리고 (받는 동안 옅은 분류 그림) 다음 사진 차례에 새로 올린다
    private void SetPhoto(Row r, bool known, PlaceListManager.ListEntry e)
    {
        if (r.photoBox == null) return;
        string key = known ? PhotoKey(e) : "";
        if (key != r.photoKey)
        {
            r.photoKey = key;
            if (r.photoShown != null && r.photoShown != key) ClearPhoto(r);
            nextPhotoAt = 0f;
        }
        if (r.photoIcon != null)
        {
            Sprite s = r.dot.sprite != r.dotSprite ? r.dot.sprite : null;
            if (r.photoIcon.sprite != s) r.photoIcon.sprite = s;
            bool on = s != null && r.photoShown == null;
            if (r.photoIcon.enabled != on) r.photoIcon.enabled = on;
        }
    }

    // 그 장소의 사진 — 오브젝트 앞면과 같은 것. "" 없음 · null 아직 모름 (AR 에 없는 우팡 장소 — 상세를 받아야 안다)
    private string PhotoKey(PlaceListManager.ListEntry e)
    {
        switch (e.data)
        {
            case PlaceData p: return PlacePhoto(p);
            case CachedPlaceData c:
                if (!int.TryParse(e.id, out int id)) return "";
                if (FullPlace(id, out PlaceData full)) return PlacePhoto(full);
                string icon = DataManager.FaceIconPath(c.category, c.filterKey == "publicData");
                if (icon != null) return "res:" + icon;
                return detailFailed.TryGetValue(id, out float at) && Time.unscaledTime - at < photoRetry ? "" : null;
            case TourPlaceData tp: return PhotoUrl(!string.IsNullOrEmpty(tp.firstimage2) ? tp.firstimage2 : tp.firstimage);   // 관광공사 작은 대표 사진 (300x200)
            case FacilityData fd: return PhotoUrl(fd.main_photo);
            case NearbyUserData u: return PhotoUrl(u.avatar_url);
            case R0926Nearby.Festival fe: return PhotoUrl(fe.image);
        }
        return "";
    }

    // 기관이 등록한 공공데이터는 앱 그림 (큐브 앞면과 같은 판단 — DataManager.SetupCubeObject), 그 밖은 대표 사진
    private static string PlacePhoto(PlaceData p)
    {
        string icon = DataManager.FaceIconPath(p.category, FilterManager.IsPublicSource(p.is_public, p.category));
        return icon != null ? "res:" + icon : PhotoUrl(p.main_photo);
    }

    // 받을 주소 — http 는 https 로 (앱은 http 를 받지 않는다 · 관광공사 사진도 https 로 열린다), 우팡 서버 사진은 작은 사본으로
    private static string PhotoUrl(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        path = path.Trim().Replace('\\', '/');
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) path = "https://" + path.Substring(7);
        if (path.StartsWith(ApiConfig.MAIN_SERVER + "/uploads/", StringComparison.OrdinalIgnoreCase)) path = path.Substring(ApiConfig.MAIN_SERVER.Length + 1);
        if (path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return path;
        if (path.Contains("://")) return "";
        return ApiConfig.MAIN_SERVER + "/thumb/" + PhotoThumb + "/" + path.TrimStart('/');
    }

    private bool FullPlace(int id, out PlaceData p)
    {
        var dm = DataManager.Instance;
        var map = dm != null ? dm.GetPlaceDataMap() : null;
        if (map != null && map.TryGetValue(id, out p)) return true;
        return details.TryGetValue(id, out p);
    }

    // 보이는 줄의 사진을 올리고, 아직 없는 것은 받는다 — 보이는 줄 먼저, 그다음 위아래 한 화면. 멀리 지나간 줄은 사진을 놓는다
    private void UpdatePhotos()
    {
        nextPhotoAt = Time.unscaledTime + PhotoInterval;
        bool view = ViewRange(out float lo, out float hi);
        foreach (var r in rows)
        {
            if (r.photoBox == null || !r.go.activeSelf) continue;
            r.near = view ? Nearness(r, lo, hi) : 0;
            if (r.near == 2) { if (r.photoShown != null) ClearPhoto(r); continue; }
            if (r.photoKey == null && TryGetEntry(r, out var e))
            {
                r.photoKey = PhotoKey(e);   // 상세가 막 왔을 수 있다
                if (r.photoKey == null && int.TryParse(e.id, out int id)) WantDetail(id);
            }
            if (string.IsNullOrEmpty(r.photoKey)) { if (r.photoShown != null) ClearPhoto(r); continue; }
            if (pics.TryGetValue(r.photoKey, out var p)) ShowPhoto(r, r.photoKey, p);
        }
        for (int pass = 0; pass < 2; pass++)
            foreach (var r in rows)
            {
                if (picLoading.Count >= photoParallel) break;
                if (r.photoBox == null || !r.go.activeSelf || r.near != pass || string.IsNullOrEmpty(r.photoKey) || r.photoShown == r.photoKey) continue;
                string key = r.photoKey;
                if (pics.ContainsKey(key) || picLoading.Contains(key)) continue;
                if (picFailed.TryGetValue(key, out float at) && Time.unscaledTime - at < photoRetry) continue;
                RequestPic(key);
                if (pics.TryGetValue(key, out var rp)) ShowPhoto(r, key, rp);   // 앱 그림은 바로
            }
        PumpDetails();
    }

    private bool ViewRange(out float lo, out float hi)
    {
        lo = hi = 0f;
        if (scroll == null && rowContainer != null) scroll = rowContainer.GetComponentInParent<ScrollRect>(true);
        if (scroll == null) return false;
        var view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        view.GetWorldCorners(corners);
        lo = corners[0].y;
        hi = corners[1].y;
        return hi > lo;
    }

    private int Nearness(Row r, float lo, float hi)
    {
        ((RectTransform)r.go.transform).GetWorldCorners(corners);
        float a = corners[0].y, b = corners[1].y;
        if (b >= lo && a <= hi) return 0;
        float h = hi - lo;
        return b >= lo - h && a <= hi + h ? 1 : 2;
    }

    private void ShowPhoto(Row r, string key, Pic p)
    {
        p.used = Time.unscaledTime;
        if (r.photoShown == key) return;
        var tex = p.tex;
        r.photoShown = key;
        r.photo.texture = tex;
        float w = Mathf.Max(1, tex.width), h = Mathf.Max(1, tex.height);
        r.photo.uvRect = w > h ? new Rect((1f - h / w) * 0.5f, 0f, h / w, 1f) : new Rect(0f, (1f - w / h) * 0.5f, 1f, w / h);   // 가운데를 정사각형으로
        r.photo.enabled = true;
        r.photo.canvasRenderer.SetAlpha(0f);
        r.photo.CrossFadeAlpha(1f, PhotoFadeIn, true);
        if (r.photoIcon != null) r.photoIcon.enabled = false;
    }

    private void ClearPhoto(Row r)
    {
        r.photoShown = null;
        if (r.photo != null) { r.photo.texture = null; r.photo.enabled = false; }
        if (r.photoIcon != null) r.photoIcon.enabled = r.photoIcon.sprite != null;
    }

    private void RequestPic(string key)
    {
        if (key.StartsWith("res:"))
        {
            // 앱 그림 (공공데이터 분류) — 함께 쓰는 에셋이라 지우지 않는다
            var t = Resources.Load<Texture2D>(key.Substring(4));
            if (t != null) pics[key] = new Pic { tex = t, owned = false, used = Time.unscaledTime };
            else picFailed[key] = float.MaxValue;
            return;
        }
        UnityWebRequest req = null;
        try
        {
            req = UnityWebRequestTexture.GetTexture(key, true);   // 읽기 전용 — CPU 쪽 사본을 남기지 않는다
            req.timeout = 15;
            picLoading.Add(key);
            picRequests.Add(req);
            int gen = picGen;
            var q = req;
            req.SendWebRequest().completed += _ => PicDone(key, q, gen);
        }
        catch (Exception)
        {
            // 주소가 이상하다 — 그 사진만 건너뛴다
            picLoading.Remove(key);
            if (req != null) { picRequests.Remove(req); req.Dispose(); }
            picFailed[key] = Time.unscaledTime;
        }
    }

    private void PicDone(string key, UnityWebRequest req, int gen)
    {
        picRequests.Remove(req);
        Texture2D tex = null;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { tex = DownloadHandlerTexture.GetContent(req); }
            catch (Exception) { tex = null; }
        }
        req.Dispose();
        if (this == null || gen != picGen) { if (tex != null) Destroy(tex); return; }   // 목록을 닫았다
        picLoading.Remove(key);
        if (tex == null) { picFailed[key] = Time.unscaledTime; return; }
        tex.wrapMode = TextureWrapMode.Clamp;
        if (pics.TryGetValue(key, out var old) && old.owned && old.tex != null && old.tex != tex) Destroy(old.tex);
        pics[key] = new Pic { tex = tex, owned = true, used = Time.unscaledTime };
        nextPhotoAt = 0f;
        TrimPics();
    }

    // 받아 둔 사진이 많으면 오래 안 본 것부터 놓는다 (칸에 올라가 있는 것은 그대로)
    private void TrimPics()
    {
        if (pics.Count <= photoCacheSize) return;
        var keep = new HashSet<string>();
        foreach (var r in rows) if (r.photoShown != null) keep.Add(r.photoShown);
        var order = new List<KeyValuePair<string, Pic>>(pics);
        order.Sort((a, b) => a.Value.used.CompareTo(b.Value.used));
        foreach (var kv in order)
        {
            if (pics.Count <= photoCacheSize) break;
            if (keep.Contains(kv.Key)) continue;
            pics.Remove(kv.Key);
            if (kv.Value.owned && kv.Value.tex != null) Destroy(kv.Value.tex);
        }
    }

    // 목록을 닫으면 받은 사진을 모두 놓는다 — 다시 열면 보이는 줄만 새로 받는다
    private void ReleasePhotos()
    {
        picGen++;
        var reqs = picRequests.ToArray();
        picRequests.Clear();
        foreach (var q in reqs) q.Abort();
        picLoading.Clear();
        if (detailRequest != null) { var d = detailRequest; detailRequest = null; d.Abort(); }
        foreach (var r in rows) if (r.photoBox != null) ClearPhoto(r);
        foreach (var p in pics.Values) if (p.owned && p.tex != null) Destroy(p.tex);
        pics.Clear();
    }

    private void WantDetail(int id)
    {
        if (details.ContainsKey(id)) return;
        if (detailFailed.TryGetValue(id, out float at) && Time.unscaledTime - at < photoRetry) return;
        detailWant.Add(id);
    }

    private void PumpDetails()
    {
        if (detailRequest != null || detailWant.Count == 0) return;
        var ids = new List<int>(Mathf.Min(detailWant.Count, 50));
        foreach (int id in detailWant) { ids.Add(id); if (ids.Count >= 50) break; }   // 서버는 한 번에 50곳까지
        foreach (int id in ids) detailWant.Remove(id);
        UnityWebRequest req = null;
        try
        {
            req = UnityWebRequest.Get(ApiConfig.LOCATIONS_DETAIL + "?ids=" + string.Join(",", ids));
            req.timeout = 15;
            detailRequest = req;
            int gen = picGen;
            var q = req;
            req.SendWebRequest().completed += _ => DetailDone(q, ids, gen);
        }
        catch (Exception)
        {
            if (req != null) req.Dispose();
            detailRequest = null;
            foreach (int id in ids) detailFailed[id] = Time.unscaledTime;
        }
    }

    private void DetailDone(UnityWebRequest req, List<int> ids, int gen)
    {
        List<PlaceData> got = null;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { got = JsonConvert.DeserializeObject<List<PlaceData>>(req.downloadHandler.text); }
            catch (Exception) { got = null; }
        }
        req.Dispose();
        if (this == null) return;
        if (detailRequest == req) detailRequest = null;
        if (got != null)
        {
            if (details.Count > 400) details.Clear();
            foreach (var p in got) if (p != null) details[p.id] = p;
        }
        if (gen != picGen) return;   // 목록을 닫으며 그만둔 것 — 받지 못한 것으로 치지 않는다
        float now = Time.unscaledTime;
        foreach (int id in ids) if (!details.ContainsKey(id)) detailFailed[id] = now;   // 지워졌거나 받지 못했다
        nextPhotoAt = 0f;
    }

    // ── 사진 누르기 — 그 장소의 정보 창 (두 번 누른 것과 같은 DoubleTap3D 창) ─────────────

    private void OnPhotoTap(Row r)
    {
        if (places == null || !TryGetEntry(r, out var e)) return;
        if (e.kind == PlaceListManager.EntryKind.Person)
        {
            // 근처 사람 — 그 사람 프로필 (뜬 뒤에 목록을 숨기고, 프로필을 닫으면 목록으로)
            var pm = ProfileManager.Instance;
            if (pm == null || string.IsNullOrEmpty(e.id)) { OnRowTap(r); return; }
            Flash(r, tapFlashDuration);
            pm.ShowProfile(e.id, shown => { if (shown && this != null && isActiveAndEnabled) BeginCover(); });
            return;
        }
        if (e.kind != PlaceListManager.EntryKind.Place || e.data == null) { OnRowTap(r); return; }   // 보물찾기 상세 · 축제·보호소 쪽 — 줄을 누른 것과 같게
        if (infoRoutine != null) return;   // 여는 중
        infoRoutine = StartCoroutine(OpenPlaceInfo(r, e));
    }

    // AR 에 떠 있으면 그 오브젝트의 창 · 아직 없으면 대리 오브젝트에 그 장소를 채워 연다 (우팡 장소는 상세를 받아서)
    private IEnumerator OpenPlaceInfo(Row r, PlaceListManager.ListEntry e)
    {
        Flash(r, infoWait);   // 여는 동안 그 줄이 밝다
        DoubleTap3D dt = R0926PlaceInfo.Spawned(places, e);
        bool ok = dt != null && dt.OpenInfo();
        if (!ok)
        {
            object data = e.data;
            if (data is CachedPlaceData && int.TryParse(e.id, out int id))
            {
                float tapped = Time.unscaledTime;
                detailFailed.Remove(id);
                PlaceData full;
                while (!FullPlace(id, out full) && Time.unscaledTime - tapped < infoWait)
                {
                    WantDetail(id);
                    PumpDetails();
                    if (detailFailed.TryGetValue(id, out float at) && at >= tapped) break;   // 받지 못했다
                    yield return null;
                }
                data = full;
                dt = R0926PlaceInfo.Spawned(places, e);   // 기다리는 사이 AR 에 떴을 수 있다
                ok = dt != null && dt.OpenInfo();
            }
            if (!ok && data != null)
            {
                dt = R0926PlaceInfo.Host();
                float until = Time.unscaledTime + 2f;
                while (dt != null && !dt.IsReady && Time.unscaledTime < until) yield return null;   // 처음 만든 대리 오브젝트는 한 프레임 뒤에 준비된다
                ok = dt != null && dt.IsReady && R0926PlaceInfo.Fill(dt, data) && dt.OpenInfo();
            }
        }
        infoRoutine = null;
        if (!ok) { InfoFailed(r); yield break; }
        r.flashUntil = Mathf.Min(r.flashUntil, Time.unscaledTime);   // 목록이 돌아올 때 분홍이 남지 않게
        BeginCover();
    }

    private void InfoFailed(Row r)
    {
        Flash(r, notInArNoteDuration);
        r.noteUntil = Time.unscaledTime + notInArNoteDuration;
        r.dir = -2;
        r.dist.text = "<size=" + NoteFontSize + "><color=#FFFFFF>"
                    + L("정보를 불러오지 못했어요", "Couldn't load the info", "情報を読み込めませんでした", "无法加载信息", "No se pudo cargar la información")
                    + "</color></size>";
    }

    // ── 사진으로 연 창이 떠 있는 동안 목록을 숨긴다 ─────────────────────
    // 목록(ListPanel)은 정보 창 · 프로필 · 고치기 화면보다 위에 그려진다 — 그 창들이 떠 있는 동안 목록을 투명하게 · 누름 없이 두었다가
    // 모두 닫히면 다시 보인다 (목록에서 연 창을 닫으면 목록으로 돌아온다). 창에서 이어 연 고치기 · 사진 자르기 · 프로필도 함께 본다
    private static readonly string[] CoverPanels = { "FullScreenPanel", "FullProfilePanel", "UploadPage", "Fixpage", "PhotoCrop0926" };
    private const float CoverFade = 0.18f, CoverGrace = 0.3f, CoverOpenWait = 2f;
    private CanvasGroup coverGroup;
    private readonly List<GameObject> coverWatch = new List<GameObject>();
    private int coverState;   // 0 없음 · 1 숨김 · 2 다시 보이는 중
    private bool coverSeen, coverSavedBlocks, coverSavedInteract;
    private float coverAlpha = 1f, coverSavedAlpha = 1f, coverSince, coverClearAt;

    private void BeginCover()
    {
        Transform panel = CoverRoot();
        if (panel == null) return;
        if (coverState == 0)
        {
            coverGroup = panel.GetComponent<CanvasGroup>();
            if (coverGroup == null) coverGroup = panel.gameObject.AddComponent<CanvasGroup>();
            coverSavedAlpha = coverGroup.alpha;
            coverSavedBlocks = coverGroup.blocksRaycasts;
            coverSavedInteract = coverGroup.interactable;
            coverAlpha = 1f;
        }
        coverState = 1;
        coverGroup.blocksRaycasts = false;
        coverGroup.interactable = false;
        coverSeen = false;
        coverSince = Time.unscaledTime;
        coverClearAt = -1f;
        coverWatch.Clear();
        foreach (var n in CoverPanels)
        {
            var t = panel.parent.Find(n);
            if (t != null) coverWatch.Add(t.gameObject);
        }
    }

    // 목록이 든 맨 위 패널 (뿌리 캔버스 바로 아래)
    private Transform CoverRoot()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        Transform root = canvas.rootCanvas.transform;
        for (Transform t = transform; t != null; t = t.parent)
            if (t.parent == root) return t;
        return null;
    }

    private void UpdateCover()
    {
        if (coverState == 0) return;
        float now = Time.unscaledTime;
        if (coverState == 1)
        {
            bool open = false;
            foreach (var g in coverWatch)
                if (g != null && g.activeInHierarchy) { open = true; break; }
            if (open) { coverSeen = true; coverClearAt = -1f; }
            else if (coverSeen || now - coverSince > CoverOpenWait)
            {
                if (coverClearAt < 0f) coverClearAt = now;
                if (!coverSeen || now - coverClearAt >= CoverGrace) Uncover();   // 창이 모두 닫혔다 (넘어가는 틈은 기다린다)
            }
        }
        coverAlpha = Mathf.MoveTowards(coverAlpha, coverState == 1 ? 0f : 1f, Time.unscaledDeltaTime / CoverFade);
        if (coverGroup != null) coverGroup.alpha = coverSavedAlpha * coverAlpha;
        if (coverState == 2 && coverAlpha >= 1f) EndCover();
    }

    // 누름은 바로 돌려주고 목록은 서서히 보인다
    private void Uncover()
    {
        coverState = 2;
        if (coverGroup == null) return;
        coverGroup.blocksRaycasts = coverSavedBlocks;
        coverGroup.interactable = coverSavedInteract;
    }

    private void EndCover()
    {
        if (coverState == 0) return;
        coverState = 0;
        coverWatch.Clear();
        if (coverGroup == null) return;
        coverGroup.alpha = coverSavedAlpha;
        coverGroup.blocksRaycasts = coverSavedBlocks;
        coverGroup.interactable = coverSavedInteract;
    }

    private Image Decor(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 보물찾기 줄만 금빛 바탕 · 반짝임 (order = 보물찾기 줄 중 몇 번째, 아니면 -1)
    private void SetHuntGlow(Row r, int order)
    {
        r.huntOrder = order;
        bool on = order >= 0;
        if (r.huntGlow != null && r.huntGlow.gameObject.activeSelf != on) r.huntGlow.gameObject.SetActive(on);
        if (!on && r.huntTwinkle != null && r.huntTwinkle.gameObject.activeSelf) r.huntTwinkle.gameObject.SetActive(false);
    }

    // 한 주기에 한 번 — 상자 옆 별이 반짝이고, 이어서 빛 띠가 줄을 왼쪽에서 오른쪽으로 지나간다. 아래 줄일수록 조금씩 늦게.
    // 쉬는 동안은 아무것도 움직이지 않는다 (목록 화면을 매 프레임 다시 그리지 않게)
    private void UpdateHuntShine()
    {
        float now = Time.unscaledTime - openedAt - 0.35f;   // 목록이 다 올라온 뒤 첫 반짝
        foreach (var r in rows)
        {
            if (r.huntOrder < 0 || !r.go.activeSelf) continue;
            float t = Mathf.Repeat(now - r.huntOrder * HuntStagger, huntShinePeriod);
            if (r.huntShine != null)
            {
                float s = (t - ShineStart) / ShineTime;
                bool on = s > 0f && s < 1f;
                if (r.huntShine.gameObject.activeSelf != on) r.huntShine.gameObject.SetActive(on);
                if (on)
                {
                    float w = r.huntGlow.rectTransform.rect.width;
                    r.huntShine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(ShineEdge, w - ShineEdge, Mathf.SmoothStep(0f, 1f, s)), 0f);
                    r.huntShine.canvasRenderer.SetAlpha(Mathf.Sin(s * Mathf.PI));
                }
            }
            if (r.huntTwinkle != null)
            {
                float k = t / TwinkleTime;
                bool on = k < 1f;
                if (r.huntTwinkle.gameObject.activeSelf != on) r.huntTwinkle.gameObject.SetActive(on);
                if (on)
                {
                    var trt = r.huntTwinkle.rectTransform;
                    trt.localScale = Vector3.one * Mathf.Sin(k * Mathf.PI);
                    trt.localRotation = Quaternion.Euler(0f, 0f, -40f * k);
                }
            }
        }
    }

    private void Flash(Row r, float hold)
    {
        if (r.flash == null) return;
        r.flashFrom = Time.unscaledTime;
        r.flashUntil = Time.unscaledTime + hold;
        flashEnd = Mathf.Max(flashEnd, r.flashUntil + FlashFadeOut);
        UpdateFlashes();
    }

    // 분홍 바탕 — 빠르게 켜지고, 시간이 지나면 천천히 꺼진다
    private void UpdateFlashes()
    {
        float now = Time.unscaledTime;
        foreach (var r in rows)
        {
            if (r.flash == null) continue;
            float a = 0f;
            if (r.flashUntil > 0f)
            {
                float fin = Mathf.Clamp01((now - r.flashFrom) / FlashFadeIn);
                float fout = 1f - Mathf.Clamp01((now - r.flashUntil) / FlashFadeOut);
                a = Mathf.Min(Mathf.Max(fin, 0.35f), fout);
            }
            bool on = a > 0.001f && r.go.activeSelf;
            if (r.flash.gameObject.activeSelf != on) r.flash.gameObject.SetActive(on);
            if (on) r.flash.canvasRenderer.SetAlpha(a);
        }
    }

    private bool TryGetEntry(Row r, out PlaceListManager.ListEntry e) => FindEntry(r.index, r.key, out e);

    // 줄 → 장소 정보. 보통 순번이 같고, 어긋났으면(목록이 막 바뀐 프레임 등) 이름으로 찾는다
    private bool FindEntry(int index, string key, out PlaceListManager.ListEntry e)
    {
        if (places != null)
        {
            var list = places.ShownEntries;
            if (index < list.Count && list[index].name == key) { e = list[index]; return true; }
            for (int i = 0; i < list.Count; i++)
                if (list[i].name == key) { e = list[i]; return true; }
        }
        e = default;
        return false;
    }

    // ── 방향 (앞·오른쪽·뒤·왼쪽) ─────────────────────────────

    private void RefreshDirections()
    {
        nextDirAt = Time.unscaledTime + directionInterval;
        IReadOnlyList<PlaceListManager.ListEntry> list = null;
        if (showDirection && Application.isPlaying && places != null && ReadPose()) list = places.ShownEntries;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (!r.go.activeSelf) continue;
            int dir = -1;
            if (list != null && !r.noDirection && r.index < list.Count && list[r.index].name == r.key) dir = Direction(list[r.index], r.dir);
            ShowDistance(r, dir);
        }
    }

    // 줄에 잠깐 띄우는 안내('아직 AR화면 내에 없음' 등)는 방향 글자보다 작게 — 방향 글자를 키워도(11-13) 긴 안내가 이름을 덮지 않게
    private int NoteFontSize => Mathf.Min(directionFontSize, 34);

    private void ShowDistance(Row r, int dir)
    {
        if (Time.unscaledTime < r.noteUntil) return;   // '아직 AR화면 내에 없음' 을 보여 주는 중
        if (dir == r.dir && r.distLabel == r.shownLabel) return;
        r.dir = dir;
        r.shownLabel = r.distLabel;
        r.dist.text = dir < 0 ? r.distLabel : "<size=" + directionFontSize + ">" + DirWord(dir) + "</size>  " + r.distLabel;
    }

    // 이번 계산에 쓸 기기 방향 — AR 카메라(AR 에 떠 있는 장소)와 나침반(그 밖의 장소)
    private bool ReadPose()
    {
        hasCam = false;
        Camera cam = indicators != null ? indicators.ViewCamera : null;
        if (cam == null) cam = Camera.main;
        if (cam != null && (indicators == null || !indicators.IsFallbackMode))
        {
            // 세워 들면 렌즈 쪽, 눕혀 들면 화면 위쪽이 '앞' — 둘의 수평 성분을 더하면 어느 기울기에서도 같은 쪽이다
            Transform ct = cam.transform;
            Vector3 f = ct.forward, u = ct.up;
            camFlat = new Vector3(f.x + u.x, 0f, f.z + u.z);
            if (camFlat.sqrMagnitude < 0.01f) camFlat = new Vector3(f.x, 0f, f.z);
            camPos = ct.position;
            hasCam = camFlat.sqrMagnitude > 0.0001f;
        }
        hasHeading = Input.location.status == LocationServiceStatus.Running && ReadCompass(out heading);
        if (hasHeading)
        {
            userLat = Input.location.lastData.latitude;
            userLon = Input.location.lastData.longitude;
        }
        return hasCam || hasHeading;
    }

    private bool ReadCompass(out float deg)
    {
        deg = 0f;
        if (compassFailed) return false;
        try
        {
            if (!Input.compass.enabled) { Input.compass.enabled = true; compassOwned = true; return false; }   // 켠 직후엔 아직 값이 없다
            if (Input.compass.timestamp <= 0) return false;
            deg = Input.compass.trueHeading;
            if (deg < 0f) deg = Input.compass.magneticHeading;
            return true;
        }
        catch (InvalidOperationException)
        {
            compassFailed = true;   // 이 입력 설정에서 나침반을 못 읽으면 다시 시도하지 않는다
            return false;
        }
    }

    // 기기가 향한 쪽 기준 장소의 방향 — 0 앞 · 1 오른쪽 · 2 뒤 · 3 왼쪽 · -1 모름
    private int Direction(PlaceListManager.ListEntry e, int current)
    {
        float a;
        Target t = e.target;
        if (hasCam && t != null && t.isActiveAndEnabled && t.IsAnchorReady)
        {
            // AR 에 떠 있는 장소 — 카메라와 오브젝트 사이 (가장 정확)
            Vector3 v = t.transform.position - camPos;
            v.y = 0f;
            if (v.sqrMagnitude < 1f) return -1;
            a = Vector3.SignedAngle(camFlat, v, Vector3.up);
        }
        else if (hasHeading && e.distance >= GpsMinDistance)
        {
            // 그 밖 — 나침반 방향과 장소 방위 (가까운 거리라 평면 근사)
            float dx = (e.lon - userLon) * Mathf.Cos(userLat * Mathf.Deg2Rad);
            float dy = e.lat - userLat;
            a = Mathf.DeltaAngle(heading, Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
        }
        else return -1;
        if (current >= 0 && Mathf.Abs(Mathf.DeltaAngle(a, current * 90f)) <= 45f + Hysteresis) return current;
        return (Mathf.RoundToInt(a / 90f) % 4 + 4) % 4;
    }

    private static string DirWord(int dir)
    {
        switch (dir)
        {
            case 0: return L("앞", "Ahead", "前", "前方", "Delante");
            case 1: return L("오른쪽", "Right", "右", "右侧", "Derecha");
            case 2: return L("뒤", "Behind", "後ろ", "后方", "Detrás");
            default: return L("왼쪽", "Left", "左", "左侧", "Izquierda");
        }
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

    private static string FormatDistance(int m)
    {
        return m >= 1000 ? (m / 1000f).ToString("0.0") + "km" : m + "m";
    }

    private static Color ParseHex(string hex)
    {
        return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.white;
    }
}
