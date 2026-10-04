using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlaceListManager 가 한 덩어리로 쓰는 목록 글자("&lt;color=#..&gt;이름 - 42m&lt;/color&gt;")를 읽어
/// 한 줄에 한 장소(색 점 · 이름 · 방향 · 거리)로 다시 그린다. PlaceListManager 의 글자는 건드리지 않는다.
/// 원본 Text 는 숨긴 채 계속 쓰이게 두고, 바뀔 때만 다시 그린다.
/// 줄을 누르면 그 장소를 AR 에서 찾게 돕는다 — AR 에 떠 있으면 목록을 내리고 그 박스·화살표를 잠깐 강조,
/// 아직 없으면 방향·거리를 알려 준다. 줄 ↔ 장소는 PlaceListManager.ShownEntries(글자와 같은 순서)로 잇는다.
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
    [Tooltip("AR 에 아직 없는 장소를 눌렀을 때 거리 칸에 '아직 AR 에 없어요' 를 보여 주는 시간 (초)")]
    [SerializeField] private float notInArNoteDuration = 2.5f;

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

    private readonly List<Row> rows = new List<Row>();

    public struct Entry { public string name; public string distance; }
    private readonly List<Entry> entries = new List<Entry>();
    /// <summary>지금 목록에 보이는 장소 (가까운 순) — AI에게 묻기 등에서 쓴다</summary>
    public IReadOnlyList<Entry> Entries => entries;
    private readonly StringBuilder sb = new StringBuilder(128);
    private string last;
    private bool korean = true;

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
        public float noteUntil;    // 이 시각까지 거리 칸에 '아직 AR 에 없어요'
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
    }

    private void OnDisable()
    {
        // 목록이 열려 있는 동안만 나침반을 쓴다 (우리가 켠 경우에만 끈다)
        if (compassOwned) { Input.compass.enabled = false; compassOwned = false; }
    }

    private void LateUpdate()
    {
        if (source == null) return;
        string t = source.text;
        if (t != last)
        {
            last = t;
            Rebuild(t);
        }
        else if (Time.unscaledTime >= nextDirAt) RefreshDirections();
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
        for (int i = 0; i < skeletonRows; i++)
        {
            float y = -(i + 0.5f) * rowHeight;
            Bar(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f + 11f, y), new Vector2(22f, 22f));                 // 색 점 자리
            Bar(new Vector2(0f, 1f), new Vector2(widths[i % widths.Length], 1f), new Vector2(0f, y), new Vector2(0f, 26f), 64f); // 이름
            Bar(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f - 50f, y), new Vector2(100f, 22f));                 // 거리
        }
    }

    private void Bar(Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, float leftInset = 0f)
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
        img.pixelsPerUnitMultiplier = 64f / 11f;
        img.color = new Color(1f, 1f, 1f, 0.06f);
        img.raycastTarget = false;
        skeletonBars.Add(img);
    }

    private void Rebuild(string text)
    {
        int used = 0;
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
                r.dot.color = ParseHex(m.Groups[1].Value);
                string key = m.Groups[2].Value;
                string label = key;
                if (label.StartsWith("👤 ")) label = label.Substring(3);   // 근처 사용자 — 색 점(핑크)으로 구분
                // 한국어가 아닌 기기: 한글 이름 옆에 로마자 읽는 법을 옅게 (번역이 아니라 읽기 도움)
                string roman = korean ? null : R0926Romanizer.Romanize(label);
                r.name.text = roman == null ? label : label + "  <color=#6F7880>" + roman + "</color>";
                if (r.key != key) { r.key = key; r.dir = -2; r.noteUntil = 0f; }   // 다른 장소가 이 줄로 왔다 — 방향을 새로
                r.distLabel = FormatDistance(int.Parse(m.Groups[3].Value));
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
        if (summaryText != null) summaryText.text = message ?? sb.ToString();
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

        if (showDirection && directionWidth > 0f)
        {
            // 방향 글자 자리 — 거리 칸(오른쪽 기준)을 왼쪽으로 넓히고 이름 칸은 그만큼 좁힌다. 길어도 거리가 잘리지 않게 한 줄로
            row.dist.rectTransform.sizeDelta += new Vector2(directionWidth, 0f);
            row.name.rectTransform.offsetMax -= new Vector2(directionWidth, 0f);
            row.dist.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }

    // ── 줄 누르기 ─────────────────────────────────────────

    private void OnRowTap(Row r)
    {
        if (places == null || !TryGetEntry(r, out var e)) return;
        Target t = places.FindTarget(e);
        if (t != null && indicators != null && indicators.Focus(t))
        {
            // AR 에 떠 있다 — 그 박스·화살표를 잠깐 강조하고 목록을 내린다 (X 와 같은 길: 시트가 내려간 뒤 닫기 버튼)
            if (sheet != null) sheet.AnimateClose(null);
            return;
        }
        // 아직 AR 에 없다 (멀거나 불러오는 중) — 그 줄 거리 칸에 잠깐 알려 준다 (방향·거리는 줄에 이미 보인다).
        // ToastManager 는 0926 씬에 없어 쓰지 않는다
        r.noteUntil = Time.unscaledTime + notInArNoteDuration;
        r.dir = -2;   // 안내가 끝나면 방향·거리를 다시 그린다
        r.dist.text = "<size=" + directionFontSize + ">" + L("아직 AR에 없어요", "Not in AR yet", "まだARにありません", "尚未在AR中", "Aún no en AR") + "</size>";
    }

    // 줄 → 장소 정보. 보통 순번이 같고, 어긋났으면(목록이 막 바뀐 프레임 등) 이름으로 찾는다
    private bool TryGetEntry(Row r, out PlaceListManager.ListEntry e)
    {
        var list = places.ShownEntries;
        if (r.index < list.Count && list[r.index].name == r.key) { e = list[r.index]; return true; }
        for (int i = 0; i < list.Count; i++)
            if (list[i].name == r.key) { e = list[i]; return true; }
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
            if (list != null && r.index < list.Count && list[r.index].name == r.key) dir = Direction(list[r.index], r.dir);
            ShowDistance(r, dir);
        }
    }

    private void ShowDistance(Row r, int dir)
    {
        if (Time.unscaledTime < r.noteUntil) return;   // '아직 AR 에 없어요' 를 보여 주는 중
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
