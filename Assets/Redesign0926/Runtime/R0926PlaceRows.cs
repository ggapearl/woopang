using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlaceListManager 가 한 덩어리로 쓰는 목록 글자("&lt;color=#..&gt;이름 - 42m&lt;/color&gt;")를 읽어
/// 한 줄에 한 장소(색 점 · 이름 · 거리)로 다시 그린다. PlaceListManager 는 건드리지 않는다.
/// 원본 Text 는 숨긴 채 계속 쓰이게 두고, 바뀔 때만 다시 그린다.
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
    }

    private void Awake()
    {
        if (source != null)
        {
            // 원본은 보이지 않게만 — PlaceListManager·스켈레톤이 계속 참조한다
            source.enabled = false;
            var le = source.GetComponent<LayoutElement>() ?? source.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
        if (rowTemplate != null) rowTemplate.SetActive(false);
        korean = R0926LocalizedText.Lang() == "ko";
    }

    private void OnEnable()
    {
        openedAt = Time.unscaledTime;
        last = null;   // 다시 열면 한 번 새로 그린다 (스켈레톤 판정 포함)
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
                Row r = GetRow(used++);
                r.dot.color = ParseHex(m.Groups[1].Value);
                string label = m.Groups[2].Value;
                if (label.StartsWith("👤 ")) label = label.Substring(3);   // 근처 사용자 — 색 점(핑크)으로 구분
                // 한국어가 아닌 기기: 한글 이름 옆에 로마자 읽는 법을 옅게 (번역이 아니라 읽기 도움)
                string roman = korean ? null : R0926Romanizer.Romanize(label);
                r.name.text = roman == null ? label : label + "  <color=#6F7880>" + roman + "</color>";
                r.dist.text = FormatDistance(int.Parse(m.Groups[3].Value));
                entries.Add(new Entry { name = label, distance = r.dist.text });
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
            rows.Add(new Row
            {
                go = go,
                dot = go.transform.Find("Dot").GetComponent<Image>(),
                name = go.transform.Find("Name").GetComponent<Text>(),
                dist = go.transform.Find("Dist").GetComponent<Text>(),
            });
        }
        var row = rows[i];
        if (!row.go.activeSelf) row.go.SetActive(true);
        return row;
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
