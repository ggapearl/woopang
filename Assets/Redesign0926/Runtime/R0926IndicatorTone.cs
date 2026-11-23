using UnityEngine;

/// <summary>
/// AR 인디케이터(박스 · 화살표 · 이름 · 거리 · X) 색 — 분류 색의 색조(hue)는 그대로 두고 짙은 색만 밝은 톤으로 올린다.
/// 11-18 대표님 「현재톤을 유지한 채로 기존의 밝은 색(흰색, 초록색 etc.) 은 유지하고 카페 같이 짙은 느낌은 좀 더 밝은 톤으로
/// … 셰도우가 어두우니까 오히려 색이 묻혀서 … 글자, 텍스트가 안보이는」.
/// 기준은 주황(음식 #FF8A3D) 밝기 — OKLCH 밝기가 그보다 낮으면 그 밝기까지 올리고, 화면이 낼 수 있는 색(sRGB)을 넘으면 채도만 줄인다.
/// 흰색 · 초록 · 금색 · 노랑 · 주황은 그대로. Target.TargetColor(목록 · 칩 · 3D 와 같은 값)는 바꾸지 않고 인디케이터가 그릴 때만 바꾼다.
/// </summary>
public static class R0926IndicatorTone
{
    /// <summary>이보다 어두우면 올린다 — 주황(음식) 의 OKLCH 밝기 0.750 바로 아래</summary>
    public const float MinLightness = 0.745f;

    public static Color Lift(Color c)
    {
        if (c.a <= 0f) return c;
        ToOklab(c, out float l, out float a, out float b);
        if (l >= MinLightness) return c;
        float chroma = Mathf.Sqrt(a * a + b * b);
        float ca = 1f, sa = 0f;
        if (chroma > 1e-5f) { ca = a / chroma; sa = b / chroma; }
        else chroma = 0f;
        if (!InGamut(MinLightness, chroma * ca, chroma * sa))
        {
            float lo = 0f, hi = chroma;
            for (int i = 0; i < 16; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (InGamut(MinLightness, mid * ca, mid * sa)) lo = mid; else hi = mid;
            }
            chroma = lo;
        }
        ToLinear(MinLightness, chroma * ca, chroma * sa, out float r, out float g, out float bl);
        return new Color(Encode(r), Encode(g), Encode(bl), c.a);
    }

    /// <summary>OKLCH 밝기 (0 검정 ~ 1 흰색) — 시험용</summary>
    public static float Lightness(Color c)
    {
        ToOklab(c, out float l, out _, out _);
        return l;
    }

    /// <summary>OKLCH 색조 (도) — 시험용. 채도가 거의 없으면 0</summary>
    public static float Hue(Color c)
    {
        ToOklab(c, out _, out float a, out float b);
        if (a * a + b * b < 1e-8f) return 0f;
        float h = Mathf.Atan2(b, a) * Mathf.Rad2Deg;
        return h < 0f ? h + 360f : h;
    }

    // Björn Ottosson 의 OKLab (sRGB 는 선형으로 풀어서)
    private static void ToOklab(Color c, out float l, out float a, out float b)
    {
        float r = Mathf.GammaToLinearSpace(Mathf.Clamp01(c.r));
        float g = Mathf.GammaToLinearSpace(Mathf.Clamp01(c.g));
        float bl = Mathf.GammaToLinearSpace(Mathf.Clamp01(c.b));
        float lm = Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * bl);
        float mm = Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * bl);
        float sm = Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * bl);
        l = 0.2104542553f * lm + 0.7936177850f * mm - 0.0040720468f * sm;
        a = 1.9779984951f * lm - 2.4285922050f * mm + 0.4505937099f * sm;
        b = 0.0259040371f * lm + 0.7827717662f * mm - 0.8086757660f * sm;
    }

    private static void ToLinear(float l, float a, float b, out float r, out float g, out float bl)
    {
        float lm = l + 0.3963377774f * a + 0.2158037573f * b;
        float mm = l - 0.1055613458f * a - 0.0638541728f * b;
        float sm = l - 0.0894841775f * a - 1.2914855480f * b;
        lm = lm * lm * lm; mm = mm * mm * mm; sm = sm * sm * sm;
        r = 4.0767416621f * lm - 3.3077115913f * mm + 0.2309699292f * sm;
        g = -1.2684380046f * lm + 2.6097574011f * mm - 0.3413193965f * sm;
        bl = -0.0041960863f * lm - 0.7034186147f * mm + 1.7076147010f * sm;
    }

    private static bool InGamut(float l, float a, float b)
    {
        ToLinear(l, a, b, out float r, out float g, out float bl);
        const float e = 1e-4f;
        return r >= -e && r <= 1f + e && g >= -e && g <= 1f + e && bl >= -e && bl <= 1f + e;
    }

    private static float Cbrt(float x) => x < 0f ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f);
    private static float Encode(float x) => Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Clamp01(x)));
}
