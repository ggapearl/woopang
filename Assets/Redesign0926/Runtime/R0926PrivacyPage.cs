using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 › 앱 정보 › 개인정보처리방침 — 설정 쪽을 덮고 바로 보인다 (2026-10-23 대표님: 홈페이지로 보내지 말고 바로 보이게).
/// 글은 Text/privacy_ko.txt · privacy_en.txt (일본어·중국어·스페인어는 영어). 줄 머리: '## ' 제목 · '### ' 작은 제목 · '- ' 점 목록 ·
/// '  - ' 안쪽 목록 · '> ' 흐린 줄 · 빈 줄은 띄움. 오른쪽에서 밀려 들어오고, ‹ · 안드로이드 뒤로가기 · 설정 탭으로 닫는다.
/// </summary>
public class R0926PrivacyPage : MonoBehaviour
{
    [SerializeField] private TextAsset korean;
    [SerializeField] private TextAsset english;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private Text headTemplate;
    [SerializeField] private Text subTemplate;
    [SerializeField] private Text bodyTemplate;
    [SerializeField] private Text noteTemplate;

    private const float SlideTime = 0.22f;
    private RectTransform rt;
    private string builtLang;
    private float slide = 1f;   // 0 = 오른쪽 밖 · 1 = 제자리
    private bool closing;

    public void Open()
    {
        Build();
        closing = false;
        if (!gameObject.activeSelf)
        {
            slide = 0f;
            gameObject.SetActive(true);
        }
        scroll.StopMovement();
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);   // 맨 위부터
        Place();
    }

    public void Close()
    {
        if (!gameObject.activeInHierarchy) { gameObject.SetActive(false); return; }
        closing = true;
    }

    private void Update()
    {
        float target = closing ? 0f : 1f;
        if (slide != target)
        {
            slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime / SlideTime);
            Place();
        }
        if (closing && slide <= 0f)
        {
            closing = false;
            gameObject.SetActive(false);
        }
    }

    // 나가는 중에 설정 쪽이 꺼지면 다시 켜질 때 마저 닫힌다
    private void OnDisable()
    {
        slide = closing ? 0f : 1f;
        Place();
    }

    private void Place()
    {
        if (rt == null) rt = (RectTransform)transform;
        float e = 1f - Mathf.Pow(1f - slide, 3f);
        rt.anchoredPosition = new Vector2((1f - e) * rt.rect.width, rt.anchoredPosition.y);
    }

    private void Build()
    {
        string lang = R0926LocalizedText.Lang() == "ko" ? "ko" : "en";
        var src = lang == "ko" ? korean : english;
        if (builtLang == lang || src == null) return;
        builtLang = lang;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var c = content.GetChild(i);
            if (c != headTemplate.transform && c != subTemplate.transform && c != bodyTemplate.transform && c != noteTemplate.transform)
                Destroy(c.gameObject);
        }

        bool first = true;
        foreach (var raw in src.text.Replace("\r", "").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.Length == 0) Gap(14f);
            else if (line.StartsWith("## "))
            {
                if (!first) Gap(30f);
                Add(headTemplate, content, line.Substring(3));
            }
            else if (line.StartsWith("### ")) { Gap(8f); Add(subTemplate, content, line.Substring(4)); }
            else if (line.StartsWith("  - ")) Item(line.Substring(4), "–", 44);
            else if (line.StartsWith("- ")) Item(line.Substring(2), "·", 0);
            else if (line.StartsWith("> ")) Add(noteTemplate, content, line.Substring(2));
            else Add(bodyTemplate, content, line);
            first = false;
        }
    }

    private static Text Add(Text template, Transform parent, string text)
    {
        var t = Instantiate(template, parent);
        t.name = template.name.Replace("Tpl", "");
        t.text = text;
        t.gameObject.SetActive(true);
        return t;
    }

    private void Gap(float h)
    {
        var g = new GameObject("Gap", typeof(RectTransform), typeof(LayoutElement));
        g.layer = content.gameObject.layer;
        g.transform.SetParent(content, false);
        var le = g.GetComponent<LayoutElement>();
        le.minHeight = h; le.preferredHeight = h;
    }

    // 점 목록 한 줄 — 둘째 줄부터도 글 머리에 맞춰 들어간다
    private void Item(string text, string mark, int indent)
    {
        var row = new GameObject("Item", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.layer = content.gameObject.layer;
        row.transform.SetParent(content, false);
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(indent, 0, 0, 0);
        h.spacing = 6f;
        h.childAlignment = TextAnchor.UpperLeft;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var dot = Add(bodyTemplate, row.transform, mark);
        var dle = dot.gameObject.AddComponent<LayoutElement>();
        dle.minWidth = 34f; dle.preferredWidth = 34f;
        Add(bodyTemplate, row.transform, text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }
}
