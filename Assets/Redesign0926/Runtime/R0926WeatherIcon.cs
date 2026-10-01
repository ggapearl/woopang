using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 담백한 날씨 그림 — 해(빛살이 천천히 돈다) · 달 · 구름(좌우로 흐른다) · 빗방울 · 눈송이를 조합한다. 얼굴 없음.
/// 크기는 이 RectTransform 의 높이 기준. 코드는 Open-Meteo WMO 코드.
/// </summary>
public class R0926WeatherIcon : MonoBehaviour
{
    [SerializeField] private Image sun;
    [SerializeField] private Image rays;
    [SerializeField] private Image moon;
    [SerializeField] private Image cloudBack;
    [SerializeField] private Image cloud;
    [SerializeField] private Image[] drops;
    [SerializeField] private Image[] flakes;

    private static readonly Color SunC = new Color(1f, 0.79f, 0.30f);
    private static readonly Color MoonC = new Color(0.96f, 0.91f, 0.72f);
    private static readonly Color CloudC = new Color(0.97f, 0.98f, 1f);
    private static readonly Color CloudBackC = new Color(0.80f, 0.84f, 0.89f);
    private static readonly Color DropC = new Color(0.49f, 0.77f, 1f);

    private Vector2 cloudHome, cloudBackHome, sunHome;
    private bool rain, snow;
    private float t, seed;

    public void Set(int code, bool day)
    {
        float s = ((RectTransform)transform).rect.height;
        if (s <= 0f) s = 100f;
        bool clear = code == 0, partly = code == 1 || code == 2;
        bool cloudy = !clear && !partly;
        rain = code >= 51 && code <= 67 || code >= 80 && code <= 82 || code >= 95;
        snow = code >= 71 && code <= 77 || code == 85 || code == 86;
        seed = Random.value * 10f;

        bool showSun = day && (clear || partly);
        bool showMoon = !day && (clear || partly);
        Show(sun, showSun); Show(rays, showSun); Show(moon, showMoon);
        Show(cloud, !clear); Show(cloudBack, cloudy && !rain && !snow);
        foreach (var d in drops) Show(d, rain);
        foreach (var f in flakes) Show(f, snow);

        if (showSun)
        {
            sunHome = clear ? Vector2.zero : new Vector2(-0.16f * s, 0.14f * s);
            float body = clear ? 0.5f * s : 0.44f * s;
            Place(sun, sunHome, body, body, SunC);
            Place(rays, sunHome, body * 1.9f, body * 1.9f, SunC);
        }
        if (showMoon)
        {
            sunHome = clear ? Vector2.zero : new Vector2(-0.16f * s, 0.14f * s);
            float m = clear ? 0.72f * s : 0.56f * s;
            Place(moon, sunHome, m, m, MoonC);
        }
        if (!clear)
        {
            // 비·눈이면 구름을 위로 올려 아래에 떨어질 자리를 둔다
            cloudHome = partly ? new Vector2(0.08f * s, -0.1f * s) : rain || snow ? new Vector2(0f, 0.14f * s) : new Vector2(0.03f * s, -0.04f * s);
            float cw = partly ? 0.8f * s : 0.9f * s;
            Place(cloud, cloudHome, cw, cw * 170f / 256f, CloudC);
            cloudBackHome = cloudHome + new Vector2(0.2f * s, 0.2f * s);
            Place(cloudBack, cloudBackHome, cw * 0.72f, cw * 0.72f * 170f / 256f, CloudBackC);
            if (cloudBack != null) cloudBack.transform.SetAsFirstSibling();
        }
        for (int i = 0; i < drops.Length; i++) Place(drops[i], Vector2.zero, 0.2f * s, 0.2f * s, DropC);
        for (int i = 0; i < flakes.Length; i++) Place(flakes[i], Vector2.zero, 0.16f * s, 0.16f * s, Color.white);
        Animate(0f);
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        Animate(t);
    }

    private void Animate(float time)
    {
        float s = ((RectTransform)transform).rect.height;
        if (rays != null && rays.enabled) rays.rectTransform.localEulerAngles = new Vector3(0f, 0f, -time * 360f / 14f);
        if (sun != null && sun.enabled) sun.rectTransform.anchoredPosition = sunHome + new Vector2(0f, 0.012f * s * Mathf.Sin(time * 1.96f));
        if (cloud != null && cloud.enabled) cloud.rectTransform.anchoredPosition = cloudHome + new Vector2(0.025f * s * Mathf.Sin(time * 1.25f + seed), 0f);
        if (cloudBack != null && cloudBack.enabled) cloudBack.rectTransform.anchoredPosition = cloudBackHome - new Vector2(0.02f * s * Mathf.Sin(time * 0.97f + seed), 0f);
        if (rain)
            for (int i = 0; i < drops.Length; i++)
            {
                float k = Mathf.Repeat(time / 1.1f + i / (float)drops.Length, 1f);
                Vector2 p = cloudHome + new Vector2((i - (drops.Length - 1) / 2f) * 0.22f * s - k * 0.05f * s, -0.28f * s - k * 0.3f * s);
                drops[i].rectTransform.anchoredPosition = p;
                drops[i].color = new Color(DropC.r, DropC.g, DropC.b, Mathf.Clamp01(k * 5f) * (1f - k));
            }
        if (snow)
            for (int i = 0; i < flakes.Length; i++)
            {
                float k = Mathf.Repeat(time / 2.4f + i / (float)flakes.Length, 1f);
                Vector2 p = cloudHome + new Vector2((i - (flakes.Length - 1) / 2f) * 0.24f * s + 0.04f * s * Mathf.Sin(k * 6.28f), -0.26f * s - k * 0.32f * s);
                flakes[i].rectTransform.anchoredPosition = p;
                flakes[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 5f) * (1f - k * 0.8f));
            }
    }

    private static void Show(Image i, bool on) { if (i != null && i.enabled != on) i.enabled = on; }

    private static void Place(Image i, Vector2 pos, float w, float h, Color c)
    {
        if (i == null) return;
        var rt = i.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);
        i.color = c;
    }
}
