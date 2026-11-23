using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 K3 — 물방울 유리가 바로 뒤 화면을 비추는지 (대표님 「살짝 확대된 채로 있는 건 좋은데 아예 다른 부분이 확대, 물방울 효과 적용되서 배경으로 발생하고 있는 게 어색」).
    ///  · glassmap : 자리마다 색이 다른 배경(왼→오 빨강 · 아래→위 초록)을 카메라 맨 뒤에 깔고, 유리에 얹는 색을 거의 빼서(비침만 남김) 찍은 뒤
    ///    유리 가운데 줄의 색이 화면 어느 자리의 배경색인지 되짚어 — 유리 자리와 몇 픽셀 어긋나는지 잰다. 글자 · 그림이 얹힌 점은 색이 안 맞아 빠진다.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private const float GmBlue = 0.25f;   // 배경의 파랑 (자리와 상관없이 같음)

        private static void PlanGlassMap()
        {
            const string Key = "r0926_menu_style";
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var tintF = typeof(R0926Glass).GetField("tint", F);
            bool hadPref = false;
            int savedPref = 1;
            GameObject bd = null;
            Texture2D grad = null;
            var tints = new Dictionary<R0926Glass, float>();
            var spots = new List<(string name, Rect r)>();
            var shifts = new List<(string name, float dx, float dy, int n, int all)>();

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                hadPref = PlayerPrefs.HasKey(Key);
                savedPref = PlayerPrefs.GetInt(Key, 1);
                grad = GmGradient();
                bd = GmBackdrop(grad);
                R0926MenuStyle.Droplet = true;
            });
            Sleep(1.2f);
            Do(() =>
            {
                var root = CanvasRoot();
                if (root == null || tintF == null) return;
                foreach (var g in root.GetComponentsInChildren<R0926Glass>(false))
                {
                    if (!g.SeeThrough) continue;
                    tints[g] = (float)tintF.GetValue(g);
                    tintF.SetValue(g, 0.002f);   // 얹는 색을 거의 빼서 비친 뒤 화면만 (사라지는 비율 계산엔 영향 없음)
                    g.GetComponent<Graphic>().SetVerticesDirty();
                }
            });
            Sleep(0.6f);
            Do(() =>
            {
                spots.Clear();
                var wc = new Vector3[4];
                var hidden = new List<string>();
                foreach (var g in tints.Keys)
                {
                    if (g == null || !g.SeeThrough) continue;
                    // 숨어 있는 유리(캔버스 그룹 투명)는 못 잰다 — 하늘 칩은 상태 알약이 떠 있는 동안 같은 자리에서 숨는다
                    float a = 1f;
                    for (var t = g.transform; t != null; t = t.parent)
                    {
                        var cg = t.GetComponent<CanvasGroup>();
                        if (cg == null) continue;
                        a *= cg.alpha;
                        if (cg.ignoreParentGroups) break;
                    }
                    if (a < 0.95f) { hidden.Add(GmName(g.transform)); continue; }
                    ((RectTransform)g.transform).GetWorldCorners(wc);   // 화면 오버레이 캔버스 — 월드 좌표가 곧 화면 픽셀 (아래 왼쪽이 0)
                    spots.Add((GmName(g.transform), Rect.MinMaxRect(wc[0].x, wc[0].y, wc[2].x, wc[2].y)));
                }
                if (hidden.Count > 0) log.Add("  숨어 있어 안 잰 유리: " + string.Join(" · ", hidden));
                var sc = Shader.GetGlobalVector("_R0926GlassScreen");
                var cv = CanvasRoot()?.GetComponentInParent<Canvas>()?.rootCanvas;
                log.Add($"  화면 {Screen.width}x{Screen.height} · 유리가 아는 화면 {sc.x:0}x{sc.y:0} · 흐린 화면 {R0926GlassBlur.Size.x}x{R0926GlassBlur.Size.y} ({Time.frameCount - R0926GlassBlur.LastFrame}프레임 전)"
                        + $" · 캔버스 배율 {(cv != null ? cv.scaleFactor : 0f):0.###} · 비추는 유리 {spots.Count}곳");
            });
            Shot("glassmap");
            Do(() =>
            {
                shifts.Clear();
                var path = Path.Combine(dir, "test_glassmap.png");
                if (!File.Exists(path)) { log.Add("  찍은 화면 없음"); return; }
                var shot = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                shot.LoadImage(File.ReadAllBytes(path));
                var table = GmTable(33);
                foreach (var (name, r) in spots)
                {
                    GmShift(shot, table, r, out float dx, out float dy, out int n, out int all);
                    shifts.Add((name, dx, dy, n, all));
                    log.Add($"  {name}: 자리 {r.width:0}x{r.height:0} · 맞는 점 {n}/{all} · 비친 곳이 오른쪽으로 {dx:0.#}px · 위로 {dy:0.#}px");
                }
                UnityEngine.Object.Destroy(shot);
            });
            Check(() => shifts.Count >= 3 && shifts.TrueForAll(s => s.n >= 6), "보이는 유리 세 곳 넘게 · 곳마다 배경색이 맞는 점 6개 넘게 (재기 가능)");
            Check(() => shifts.Count > 0 && shifts.TrueForAll(s => s.n < 6 || Mathf.Abs(s.dx) <= 12f && Mathf.Abs(s.dy) <= 12f),
                "유리 가운데는 바로 뒤 화면을 비춘다 (어긋남 12px 안 — 다른 자리를 끌어오지 않는다)");

            Do(() =>
            {
                foreach (var kv in tints)
                    if (kv.Key != null) { tintF.SetValue(kv.Key, kv.Value); kv.Key.GetComponent<Graphic>().SetVerticesDirty(); }
                tints.Clear();
                if (hadPref) PlayerPrefs.SetInt(Key, savedPref); else PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
                R0926MenuStyle.Reload();   // 값을 한 번 읽어 두므로 되돌린 값을 다시 읽게 (11-23 S8)
                if (bd != null) UnityEngine.Object.Destroy(bd);
                if (grad != null) UnityEngine.Object.Destroy(grad);
            });
        }

        private static string GmName(Transform t) => t.parent != null ? t.parent.name + "/" + t.name : t.name;

        // 자리마다 다른 색 — 빨강 = 가로(왼 0 → 오 1) · 초록 = 세로(아래 0 → 위 1) · 파랑은 같게 (감마 값)
        private static Texture2D GmGradient()
        {
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "GlassMapGradient" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    px[y * N + x] = new Color32((byte)x, (byte)y, (byte)Mathf.RoundToInt(GmBlue * 255f), 255);
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        // 카메라 맨 뒤(먼 면 가까이)에 화면을 꽉 채우는 판 — 메인 카메라가 그리므로 유리의 흐린 화면에도 들어간다
        private static GameObject GmBackdrop(Texture tex)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var go = new GameObject("GlassMapBackdrop", typeof(RectTransform), typeof(Canvas));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = cam.farClipPlane * 0.9f;
            c.sortingOrder = -1000;
            var img = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
            img.transform.SetParent(go.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var raw = img.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            return go;
        }

        // 유리가 그리는 색 — 비친 배경(선형)을 1.25배 또렷하게 (셰이더와 같은 셈) · 감마로
        private static Color GmGlassColor(float u, float v)
        {
            float r = Mathf.GammaToLinearSpace(u), g = Mathf.GammaToLinearSpace(v), b = Mathf.GammaToLinearSpace(GmBlue);
            float lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            r = Mathf.Clamp01(lum + 1.25f * (r - lum));
            g = Mathf.Clamp01(lum + 1.25f * (g - lum));
            b = Mathf.Clamp01(lum + 1.25f * (b - lum));
            return new Color(Mathf.LinearToGammaSpace(r), Mathf.LinearToGammaSpace(g), Mathf.LinearToGammaSpace(b), 1f);
        }

        private static Color[,] GmTable(int n)
        {
            var t = new Color[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    t[i, j] = GmGlassColor(i / (n - 1f), j / (n - 1f));
            return t;
        }

        private static float GmDist(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        // 이 색이 화면 어느 자리(u, v)의 배경색인지 — 거친 표에서 가장 가까운 칸을 찾고 그 둘레를 1/256 로 좁혀 본다. 맞는 색이 없으면 false
        private static bool GmLocate(Color[,] table, Color c, out float u, out float v)
        {
            int n = table.GetLength(0);
            int bi = 0, bj = 0;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float d = GmDist(table[i, j], c);
                    if (d < best) { best = d; bi = i; bj = j; }
                }
            float step = 1f / (n - 1f), fine = 1f / 256f;
            u = bi * step; v = bj * step;
            best = float.MaxValue;
            for (float a = -step; a <= step + 1e-6f; a += fine)
                for (float b = -step; b <= step + 1e-6f; b += fine)
                {
                    float uu = Mathf.Clamp01(bi * step + a), vv = Mathf.Clamp01(bj * step + b);
                    float d = GmDist(GmGlassColor(uu, vv), c);
                    if (d < best) { best = d; u = uu; v = vv; }
                }
            return best < 0.035f;
        }

        // 유리 자리 r(화면 픽셀 · 아래 왼쪽 0) 가운데 줄과 그 위아래 두 줄 — 가장자리 렌즈 띠를 피해 가로 25~75% 만. 어긋남은 가운데값
        private static void GmShift(Texture2D shot, Color[,] table, Rect r, out float dx, out float dy, out int n, out int all)
        {
            float sx = shot.width / (float)Mathf.Max(1, Screen.width), sy = shot.height / (float)Mathf.Max(1, Screen.height);
            var raw = shot.GetPixels32();   // 찍은 그대로의 바이트 (감마)
            var xs = new List<float>();
            var ys = new List<float>();
            all = 0;
            foreach (float fy in new[] { 0.5f, 0.42f, 0.58f })
            {
                float py = r.yMin + r.height * fy;
                for (float fx = 0.25f; fx <= 0.75f; fx += 0.025f)
                {
                    float px = r.xMin + r.width * fx;
                    int ix = Mathf.Clamp(Mathf.RoundToInt(px * sx), 0, shot.width - 1), iy = Mathf.Clamp(Mathf.RoundToInt(py * sy), 0, shot.height - 1);
                    all++;
                    if (!GmLocate(table, raw[iy * shot.width + ix], out float u, out float v)) continue;
                    // 배경 그림의 값 → 화면 자리 (256칸 그림을 화면에 늘린 것 — 칸 가운데가 그 값)
                    float pu = (u * 255f + 0.5f) / 256f, pv = (v * 255f + 0.5f) / 256f;
                    xs.Add(pu * Screen.width - px);
                    ys.Add(pv * Screen.height - py);
                }
            }
            n = xs.Count;
            dx = n > 0 ? GmMedian(xs) : 0f;
            dy = n > 0 ? GmMedian(ys) : 0f;
        }

        private static float GmMedian(List<float> a)
        {
            a.Sort();
            int m = a.Count / 2;
            return a.Count % 2 == 1 ? a[m] : 0.5f * (a[m - 1] + a[m]);
        }
    }
}
