using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 장소 추가 ▸ AI 작업 — 사진 효과 (투명하게 · 만화톤 · 색연필).
/// 11-12 대표님 「'투명하게' … 해당 사진의 외곽선을 따라서 키를 따서 외곽선 밖의 투명도를 날리도록 … 로고나 디자인이 명확하게 있는 경우에는 그렇게하고,
/// 명확한 로고가 아닌 경우에는 '동그라미' 형태로 잘라서」 · 「02.만화톤 : 카툰처럼 … 03.색연필 : 색연필로 그린 듯한」.
/// 무료 · 폰 안에서 — 학습된 모델 없이 영상처리로 (512px 한 장에 1초 안팎). 나중에 외부 AI 를 붙이면 R0926AiWork 에서 바꿔 끼운다.
/// 배경 스레드에서 돈다 — Unity API 를 쓰지 않는다(Color32[] 만 오간다). 줄 순서는 Unity 텍스처와 같다(0 = 아래).
/// </summary>
public static class R0926AiImage
{
    public const int Side = 512;

    public sealed class Img
    {
        public readonly int W, H;
        public readonly float[] R, G, B, A;
        public Img(int w, int h)
        {
            W = w; H = h;
            int n = w * h;
            R = new float[n]; G = new float[n]; B = new float[n]; A = new float[n];
        }
        public int N => W * H;
    }

    public enum ClearMode { None, Photo, Logo, Alpha }

    public static Img FromColors(Color32[] px, int w, int h)
    {
        var img = new Img(w, h);
        const float k = 1f / 255f;
        for (int i = 0; i < img.N; i++)
        {
            img.R[i] = px[i].r * k; img.G[i] = px[i].g * k; img.B[i] = px[i].b * k; img.A[i] = px[i].a * k;
        }
        return img;
    }

    public static Color32[] ToColors(Img img)
    {
        var px = new Color32[img.N];
        for (int i = 0; i < img.N; i++)
            px[i] = new Color32(B8(img.R[i]), B8(img.G[i]), B8(img.B[i]), B8(img.A[i]));
        return px;
    }

    private static byte B8(float v) => (byte)(Mathf.Clamp01(v) * 255f + 0.5f);

    /// <summary>테두리 사진(투명 바깥)이 하나라도 있으면 true</summary>
    public static bool HasAlpha(Img img)
    {
        for (int i = 0; i < img.N; i++) if (img.A[i] < 0.98f) return true;
        return false;
    }

    // ── 만화톤 ─────────────────────────────────────────────
    /// <summary>색 면을 고르게 펴고(밝기 몇 단) 먹선을 긋는다. 로고처럼 이미 평평한 그림은 선 없이 색만 또렷하게</summary>
    public static Img Toon(Img src)
    {
        int n = src.N;
        bool flatImg = Flatness(src) > 0.7f;
        var s = Bilateral(src, 3, 2.5f, 0.10f);
        s = Bilateral(s, 3, 2.5f, 0.10f);
        s = Bilateral(s, 2, 2.0f, 0.08f);
        var o = new Img(src.W, src.H);
        Array.Copy(src.A, o.A, n);
        if (flatImg)
        {
            for (int i = 0; i < n; i++)
            {
                RgbToHsv(s.R[i], s.G[i], s.B[i], out float h, out float sat, out float v);
                HsvToRgb(h, Mathf.Min(sat * 1.15f, 1f), Mathf.Min(v * 1.02f, 1f), out o.R[i], out o.G[i], out o.B[i]);
            }
            return o;
        }
        var g = Lum(s);
        var ink = XDoG(g, s.W, s.H, 1.0f, 1.6f, 0.975f, 0.01f, 60f);
        var inkB = Blur(ink, s.W, s.H, 0.7f);
        for (int i = 0; i < n; i++)
        {
            float e = Mathf.Min(ink[i], inkB[i] + 0.1f);
            RgbToHsv(s.R[i], s.G[i], s.B[i], out float h, out float sat, out float v);
            float v2 = Mathf.Clamp01(SoftQuant(v, 5, 0.18f) * 0.9f + 0.1f * v + 0.06f);
            float sat2 = Mathf.Min(sat * 1.35f, 1f);
            HsvToRgb(h, sat2, v2, out float fr, out float fg, out float fb);
            HsvToRgb(h, Mathf.Min(sat2 * 1.1f, 1f), Mathf.Clamp(v2 * 0.28f, 0.05f, 0.2f), out float lr, out float lg, out float lb);   // 선은 바탕색을 짙게 — 검정 대신
            o.R[i] = fr * e + lr * (1f - e);
            o.G[i] = fg * e + lg * (1f - e);
            o.B[i] = fb * e + lb * (1f - e);
        }
        return o;
    }

    // ── 색연필 ─────────────────────────────────────────────
    /// <summary>종이 위에 한 방향으로 긁은 결(진한 곳은 엇갈려 한 번 더) · 연한 윤곽 · 종이 알갱이</summary>
    public static Img Pencil(Img src)
    {
        int w = src.W, h = src.H, n = src.N;
        var s = Bilateral(src, 2, 2.0f, 0.12f);
        var g = Lum(s);
        var inv = new float[n];
        for (int i = 0; i < n; i++) inv[i] = 1f - g[i];
        var b = Blur(inv, w, h, 4.0f);
        var st1 = Streak(w, h, 62f * Mathf.Deg2Rad, 3, 10);
        var st2 = Streak(w, h, -30f * Mathf.Deg2Rad, 5, 10);
        var grain = ValueNoise(w, h, 2, 11);
        var o = new Img(w, h);
        Array.Copy(src.A, o.A, n);
        const float pr = 0.985f, pg = 0.975f, pb = 0.95f;   // 종이
        for (int i = 0; i < n; i++)
        {
            float sketch = Mathf.Pow(Mathf.Clamp01(g[i] / Mathf.Max(1e-3f, 1f - b[i])), 1.6f);   // 닷지 — 윤곽만
            RgbToHsv(s.R[i], s.G[i], s.B[i], out float hh, out float sat, out float v);
            float tone = Mathf.Clamp01(0.22f + 0.85f * Mathf.Pow(1f - g[i], 0.85f) + 0.25f * sat);
            float c1 = Mathf.Clamp01((tone - st1[i]) * 5f + 0.5f);
            float tone2 = Mathf.Clamp01((tone - 0.55f) * 1.6f);   // 진한 곳만 한 번 더 겹쳐 칠한다
            float c2 = Mathf.Clamp01((tone2 - st2[i]) * 5f + 0.5f);
            float cover = 1f - (1f - c1 * 0.9f) * (1f - c2 * 0.7f);
            HsvToRgb(hh, Mathf.Min(sat * 1.2f, 1f), Mathf.Clamp01(v * 0.95f), out float cr, out float cg, out float cb);
            float r = pr * (1f - cover) + cr * cover, gg = pg * (1f - cover) + cg * cover, bb = pb * (1f - cover) + cb * cover;
            HsvToRgb(hh, sat, Mathf.Clamp01(v * 0.45f), out float er, out float eg, out float eb);
            float e = (1f - sketch) * 0.8f;
            float gr = (grain[i] - 0.5f) * 0.06f;
            o.R[i] = Mathf.Clamp01(r * (1f - e) + er * e + gr);
            o.G[i] = Mathf.Clamp01(gg * (1f - e) + eg * e + gr);
            o.B[i] = Mathf.Clamp01(bb * (1f - e) + eb * e + gr);
        }
        return o;
    }

    // ── 투명하게 ───────────────────────────────────────────
    /// <summary>
    /// 로고처럼 바탕이 한 가지 색이면 바탕을 걷어 내고 모양 둘레에 흰 테두리(스티커) — 가운데로 키워 맞춘다.
    /// 이미 투명한 사진은 그 모양 그대로 스티커로. 그 밖(보통 사진)은 동그랗게 잘라 흰 테·민트 테를 두른 배지(우팡이네집 대표 사진처럼).
    /// orig 로 모양을 찾고 styled(만화톤·색연필을 거친 것 또는 원본)를 그 모양에 담는다.
    /// </summary>
    public static Img Clear(Img orig, Img styled, out ClearMode mode)
    {
        int w = orig.W, h = orig.H, n = orig.N;
        bool[] fg = null;
        int trans = 0;
        for (int i = 0; i < n; i++) if (orig.A[i] < 0.5f) trans++;
        if (trans > n * 0.05f)
        {
            fg = new bool[n];
            for (int i = 0; i < n; i++) fg[i] = orig.A[i] >= 0.5f;
            mode = ClearMode.Alpha;
        }
        else
        {
            mode = ClearMode.Photo;
            BorderStats(orig, out float mr, out float mg, out float mb, out float frac, out float std, out bool[] ring);
            if (frac >= 0.8f)
            {
                float tol = 0.10f + Mathf.Min(0.12f, std * 2f);
                var bg = Flood(orig, ring, mr, mg, mb, tol);
                int c = 0;
                for (int i = 0; i < n; i++) if (bg[i]) c++;
                float cover = c / (float)n;
                if (cover >= 0.15f && cover <= 0.9f)
                {
                    fg = new bool[n];
                    for (int i = 0; i < n; i++) fg[i] = !bg[i];
                    mode = ClearMode.Logo;
                }
            }
        }
        return mode == ClearMode.Photo ? Badge(styled) : Sticker(styled, fg);
    }

    private static readonly Color Accent = new Color(0.18f, 0.66f, 0.66f, 1f);

    private static Img Badge(Img styled)
    {
        int w = styled.W, h = styled.H;
        int S = Mathf.Min(w, h);
        var o = new Img(S, S);
        float c = (S - 1) * 0.5f;
        float R = S * 0.5f - 1.5f, ringW = S * 0.045f, accW = S * 0.03f;
        float Rp = R - ringW - accW;
        int ox = (w - S) / 2, oy = (h - S) / 2;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float pa = Mathf.Clamp01(Rp - r + 0.5f), wa = Mathf.Clamp01(Rp + ringW - r + 0.5f), aa = Mathf.Clamp01(R - r + 0.5f);
                int j = (y + oy) * w + (x + ox), i = y * S + x;
                float cr = Accent.r * (1f - wa) + wa, cg = Accent.g * (1f - wa) + wa, cb = Accent.b * (1f - wa) + wa;
                o.R[i] = cr * (1f - pa) + styled.R[j] * pa;
                o.G[i] = cg * (1f - pa) + styled.G[j] * pa;
                o.B[i] = cb * (1f - pa) + styled.B[j] * pa;
                o.A[i] = aa;
            }
        return o;
    }

    private static Img Sticker(Img styled, bool[] fg)
    {
        int w = styled.W, h = styled.H, n = styled.N;
        int S = Mathf.Min(w, h);
        var dist = Edt(fg, w, h);
        float border = S * 0.028f;
        var t = new Img(w, h);
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float d = dist[i];
                float a = Mathf.Clamp01(border - d + 0.5f);
                float soft = Mathf.Clamp01(1f - d);   // 모양 경계 1px 부드럽게 — 그 밖은 흰 테두리
                t.R[i] = (1f - soft) + styled.R[i] * soft;
                t.G[i] = (1f - soft) + styled.G[i] * soft;
                t.B[i] = (1f - soft) + styled.B[i] * soft;
                t.A[i] = a;
                if (a > 0f) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            }
        if (x1 < 0) return t;   // 모양이 없다 — 그대로
        float bw = x1 - x0 + 1, bh = y1 - y0 + 1;
        float side = Mathf.Max(bw, bh) / 0.94f;
        float cx = (x0 + x1 + 1) * 0.5f, cy = (y0 + y1 + 1) * 0.5f;
        return Resample(t, cx - side * 0.5f, cy - side * 0.5f, side, S);
    }

    /// <summary>(sx, sy) 에서 한 변 side 인 정사각형을 S×S 로 — 바깥은 투명, 미리 곱한 알파로 섞어 가장자리가 검게 번지지 않게</summary>
    private static Img Resample(Img src, float sx, float sy, float side, int S)
    {
        var o = new Img(S, S);
        float k = side / S;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float fx = sx + (x + 0.5f) * k - 0.5f, fy = sy + (y + 0.5f) * k - 0.5f;
                int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                float tx = fx - ix, ty = fy - iy;
                float r = 0, g = 0, b = 0, a = 0;
                for (int q = 0; q < 4; q++)
                {
                    int px = ix + (q & 1), py = iy + (q >> 1);
                    float wgt = ((q & 1) == 1 ? tx : 1f - tx) * ((q >> 1) == 1 ? ty : 1f - ty);
                    if (px < 0 || py < 0 || px >= src.W || py >= src.H || wgt <= 0f) continue;
                    int j = py * src.W + px;
                    float sa = src.A[j] * wgt;
                    r += src.R[j] * sa; g += src.G[j] * sa; b += src.B[j] * sa; a += sa;
                }
                int i = y * S + x;
                if (a > 1e-5f) { o.R[i] = r / a; o.G[i] = g / a; o.B[i] = b / a; }
                else { o.R[i] = o.G[i] = o.B[i] = 1f; }
                o.A[i] = a;
            }
        return o;
    }

    private static void BorderStats(Img img, out float mr, out float mg, out float mb, out float frac, out float std, out bool[] ring)
    {
        int w = img.W, h = img.H;
        int bw = Mathf.Max(2, (int)(h * 0.03f));
        ring = new bool[img.N];
        int cnt = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (y < bw || y >= h - bw || x < bw || x >= w - bw) { ring[y * w + x] = true; cnt++; }
        var rs = new float[cnt]; var gs = new float[cnt]; var bs = new float[cnt];
        int k = 0;
        for (int i = 0; i < img.N; i++)
            if (ring[i]) { rs[k] = img.R[i]; gs[k] = img.G[i]; bs[k] = img.B[i]; k++; }
        Array.Sort(rs); Array.Sort(gs); Array.Sort(bs);
        mr = rs[cnt / 2]; mg = gs[cnt / 2]; mb = bs[cnt / 2];
        int near = 0;
        double sum = 0, sum2 = 0;
        for (int i = 0; i < img.N; i++)
        {
            if (!ring[i]) continue;
            float dr = img.R[i] - mr, dg = img.G[i] - mg, db = img.B[i] - mb;
            float d = Mathf.Sqrt(dr * dr + dg * dg + db * db);
            if (d < 0.12f) near++;
            sum += d; sum2 += d * d;
        }
        frac = near / (float)cnt;
        double mean = sum / cnt;
        std = (float)Math.Sqrt(Math.Max(0, sum2 / cnt - mean * mean));
    }

    private static bool[] Flood(Img img, bool[] seed, float mr, float mg, float mb, float tol)
    {
        int w = img.W, h = img.H, n = img.N;
        var near = new bool[n];
        float t2 = tol * tol;
        for (int i = 0; i < n; i++)
        {
            float dr = img.R[i] - mr, dg = img.G[i] - mg, db = img.B[i] - mb;
            near[i] = dr * dr + dg * dg + db * db < t2;
        }
        var outp = new bool[n];
        var stack = new int[n];
        int sp = 0;
        for (int i = 0; i < n; i++)
            if (seed[i] && near[i]) { outp[i] = true; stack[sp++] = i; }
        while (sp > 0)
        {
            int i = stack[--sp];
            int x = i % w, y = i / w;
            if (x > 0) Push(i - 1); if (x < w - 1) Push(i + 1);
            if (y > 0) Push(i - w); if (y < h - 1) Push(i + w);
        }
        return outp;

        void Push(int j)
        {
            if (outp[j] || !near[j]) return;
            outp[j] = true; stack[sp++] = j;
        }
    }

    /// <summary>앞(참) 화소까지 거리 — Felzenszwalb 1D 를 세로 · 가로로</summary>
    private static float[] Edt(bool[] mask, int w, int h)
    {
        const float INF = 1e10f;
        var g = new float[w * h];
        for (int i = 0; i < g.Length; i++) g[i] = mask[i] ? 0f : INF;
        int m = Mathf.Max(w, h);
        var f = new float[m]; var d = new float[m]; var v = new int[m]; var z = new float[m + 1];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++) f[y] = g[y * w + x];
            Dt1(f, h, d, v, z);
            for (int y = 0; y < h; y++) g[y * w + x] = d[y];
        }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) f[x] = g[y * w + x];
            Dt1(f, w, d, v, z);
            for (int x = 0; x < w; x++) g[y * w + x] = Mathf.Sqrt(d[x]);
        }
        return g;
    }

    private static void Dt1(float[] f, int n, float[] d, int[] v, float[] z)
    {
        const float INF = 1e20f;
        int k = 0;
        v[0] = 0; z[0] = -INF; z[1] = INF;
        for (int q = 1; q < n; q++)
        {
            float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
            while (s <= z[k])
            {
                k--;
                s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
            }
            k++; v[k] = q; z[k] = s; z[k + 1] = INF;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            d[q] = (q - v[k]) * (q - v[k]) + f[v[k]];
        }
    }

    // ── 바탕 연산 ──────────────────────────────────────────
    private static float[] Lum(Img img)
    {
        var o = new float[img.N];
        for (int i = 0; i < img.N; i++) o[i] = img.R[i] * 0.299f + img.G[i] * 0.587f + img.B[i] * 0.114f;
        return o;
    }

    /// <summary>밝기가 거의 안 변하는 화소의 비율 — 로고 · 그림이면 높다</summary>
    private static float Flatness(Img img)
    {
        var g = Lum(img);
        int w = img.W, h = img.H, flat = 0;
        for (int y = 0; y < h - 1; y++)
            for (int x = 0; x < w - 1; x++)
            {
                int i = y * w + x;
                if (Mathf.Abs(g[i + 1] - g[i]) + Mathf.Abs(g[i + w] - g[i]) < 0.01f) flat++;
            }
        return flat / (float)((w - 1) * (h - 1));
    }

    private static float[] Gauss(float sigma, out int r)
    {
        r = Mathf.Max(1, Mathf.CeilToInt(sigma * 3f));
        var k = new float[2 * r + 1];
        float s = 0f;
        for (int i = -r; i <= r; i++) { k[i + r] = Mathf.Exp(-(i * i) / (2f * sigma * sigma)); s += k[i + r]; }
        for (int i = 0; i < k.Length; i++) k[i] /= s;
        return k;
    }

    private static float[] Blur(float[] src, int w, int h, float sigma)
    {
        var k = Gauss(sigma, out int r);
        var tmp = new float[w * h];
        var dst = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = 0f;
                for (int t = -r; t <= r; t++) a += k[t + r] * src[y * w + Mathf.Clamp(x + t, 0, w - 1)];
                tmp[y * w + x] = a;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = 0f;
                for (int t = -r; t <= r; t++) a += k[t + r] * tmp[Mathf.Clamp(y + t, 0, h - 1) * w + x];
                dst[y * w + x] = a;
            }
        return dst;
    }

    /// <summary>윤곽은 지키고 면만 고르게 — 줄마다 나눠 여러 코어로</summary>
    private static Img Bilateral(Img src, int radius, float ss, float sr)
    {
        int w = src.W, h = src.H, d = 2 * radius + 1;
        var sp = new float[d * d];
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                sp[(dy + radius) * d + dx + radius] = Mathf.Exp(-(dx * dx + dy * dy) / (2f * ss * ss));
        const int Lut = 2048;
        const float MaxD2 = 3f;
        var rl = new float[Lut];
        for (int i = 0; i < Lut; i++) rl[i] = Mathf.Exp(-(i * MaxD2 / (Lut - 1)) / (2f * sr * sr));
        const float Scale = (Lut - 1) / MaxD2;
        var o = new Img(w, h);
        Array.Copy(src.A, o.A, src.N);
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float r0 = src.R[i], g0 = src.G[i], b0 = src.B[i];
                float ar = 0, ag = 0, ab = 0, ws = 0;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int yy = Mathf.Clamp(y + dy, 0, h - 1) * w;
                    int row = (dy + radius) * d + radius;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int j = yy + Mathf.Clamp(x + dx, 0, w - 1);
                        float cr = src.R[j], cg = src.G[j], cb = src.B[j];
                        float dr = cr - r0, dg = cg - g0, db = cb - b0;
                        int li = (int)((dr * dr + dg * dg + db * db) * Scale);
                        float wgt = sp[row + dx] * rl[li < Lut ? li : Lut - 1];
                        ar += cr * wgt; ag += cg * wgt; ab += cb * wgt; ws += wgt;
                    }
                }
                o.R[i] = ar / ws; o.G[i] = ag / ws; o.B[i] = ab / ws;
            }
        });
        return o;
    }

    private static float[] XDoG(float[] gray, int w, int h, float sigma, float k, float tau, float eps, float phi)
    {
        var g1 = Blur(gray, w, h, sigma);
        var g2 = Blur(gray, w, h, sigma * k);
        var e = new float[w * h];
        for (int i = 0; i < e.Length; i++)
        {
            float dd = g1[i] - tau * g2[i];
            e[i] = Mathf.Clamp01(dd >= eps ? 1f : 1f + (float)Math.Tanh(phi * (dd - eps)));
        }
        return e;
    }

    private static float SoftQuant(float v, int levels, float soft)
    {
        float x = v * levels;
        float b = Mathf.Floor(x);
        float st = Mathf.Clamp01((x - b - (1f - soft)) / soft);   // 칸 끝에서만 살짝 이어 계단 경계가 거칠지 않게
        st = st * st * (3f - 2f * st);
        return Mathf.Clamp01((b + st) / levels);
    }

    /// <summary>한 방향으로 길게 끌린 잡음 — 연필이 종이 결을 긁고 지나간 자국 (평균 0.5)</summary>
    private static float[] Streak(int w, int h, float angle, int seed, int len)
    {
        var rnd = new System.Random(seed);
        int pw = w + 2 * len + 2, ph = h + 2 * len + 2;
        var nz = new float[pw * ph];
        for (int i = 0; i < nz.Length; i++) nz[i] = (float)rnd.NextDouble();
        float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
        int taps = 2 * len + 1;
        var ox = new int[taps]; var oy = new int[taps];
        for (int t = 0; t < taps; t++) { ox[t] = Mathf.RoundToInt((t - len) * ca); oy[t] = Mathf.RoundToInt((t - len) * sa); }
        var acc = new float[w * h];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                float a = 0f;
                for (int t = 0; t < taps; t++) a += nz[(len + 1 + y + oy[t]) * pw + len + 1 + x + ox[t]];
                acc[y * w + x] = a / taps;
            }
        });
        double sum = 0, sum2 = 0;
        for (int i = 0; i < acc.Length; i++) { sum += acc[i]; sum2 += acc[i] * acc[i]; }
        float mean = (float)(sum / acc.Length);
        float std = (float)Math.Sqrt(Math.Max(1e-12, sum2 / acc.Length - mean * mean));
        for (int i = 0; i < acc.Length; i++) acc[i] = Mathf.Clamp01(0.5f + (acc[i] - mean) / std * 0.2f);
        return acc;
    }

    private static float[] ValueNoise(int w, int h, int cell, int seed)
    {
        var rnd = new System.Random(seed);
        int gw = w / cell + 2, gh = h / cell + 2;
        var grid = new float[gw * gh];
        for (int i = 0; i < grid.Length; i++) grid[i] = (float)rnd.NextDouble();
        var o = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            float fy = y / (float)cell;
            int y0 = (int)fy;
            float ty = fy - y0; ty = ty * ty * (3f - 2f * ty);
            for (int x = 0; x < w; x++)
            {
                float fx = x / (float)cell;
                int x0 = (int)fx;
                float tx = fx - x0; tx = tx * tx * (3f - 2f * tx);
                float a = grid[y0 * gw + x0], b = grid[y0 * gw + x0 + 1], c = grid[(y0 + 1) * gw + x0], d = grid[(y0 + 1) * gw + x0 + 1];
                o[y * w + x] = a * (1 - tx) * (1 - ty) + b * tx * (1 - ty) + c * (1 - tx) * ty + d * tx * ty;
            }
        }
        return o;
    }

    public static void RgbToHsv(float r, float g, float b, out float h, out float s, out float v)
    {
        float mx = Mathf.Max(r, Mathf.Max(g, b)), mn = Mathf.Min(r, Mathf.Min(g, b)), d = mx - mn;
        v = mx;
        s = mx > 1e-5f ? d / mx : 0f;
        if (d <= 1e-5f) { h = 0f; return; }
        if (mx == r) h = ((g - b) / d) % 6f;
        else if (mx == g) h = (b - r) / d + 2f;
        else h = (r - g) / d + 4f;
        h /= 6f;
        if (h < 0f) h += 1f;
    }

    public static void HsvToRgb(float h, float s, float v, out float r, out float g, out float b)
    {
        float hh = (h - Mathf.Floor(h)) * 6f;
        int i = (int)hh % 6;
        float f = hh - Mathf.Floor(hh);
        float p = v * (1f - s), q = v * (1f - f * s), t = v * (1f - (1f - f) * s);
        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
    }
}
