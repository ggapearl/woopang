using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Redesign0926
{
    /// <summary>
    /// 보물찾기 AR 보물상자 모양 — 몸통·둥근 뚜껑 메시와 재질 셋(금 · 짙은 청동 띠 · 빛나는 보물)을 코드로 만들어 에셋으로 둔다.
    /// 상자 좌표: 폭 1 · 깊이 0.64 · 높이 약 0.87 (가운데 0 근처) · 앞 = -Z. 뚜껑은 몸통 뒤 위 모서리(경첩)가 원점이고 X 로 돌면 열린다.
    /// 다시 만들 땐 같은 에셋에 덮어써 프리팹 참조가 끊기지 않게 한다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string ChestMeshDir = "Assets/Redesign0926/Meshes";
        private const string ChestBodyPath = ChestMeshDir + "/HuntChestBody.asset";
        private const string ChestLidPath = ChestMeshDir + "/HuntChestLid.asset";
        private const string ChestGoldMatPath = "Assets/Materials/AR/HuntChestGold_Mat.mat";
        private const string ChestTrimMatPath = "Assets/Materials/AR/HuntChestTrim_Mat.mat";
        private const string ChestTreasureMatPath = "Assets/Materials/AR/HuntChestTreasure_Mat.mat";

        private const float ChestHalfW = 0.5f, ChestHalfD = 0.32f, ChestBottom = -0.42f, ChestSeam = 0.10f;   // 뚜껑 반지름 = 깊이의 반
        private static readonly Vector3 ChestHinge = new Vector3(0f, ChestSeam, ChestHalfD);

        private class ChestParts
        {
            public Mesh body, lid;
            public Material gold, trim, treasure;
        }

        private static ChestParts ChestAssets(List<string> log)
        {
            var sh = Shader.Find("Woopang/GoldenGlowCube");
            if (sh == null) { log.Add("보물상자: 셰이더 Woopang/GoldenGlowCube 없음"); return null; }
            Directory.CreateDirectory(ChestMeshDir);
            var p = new ChestParts
            {
                // 금 — 예전 금빛 큐브와 같은 색(GoldenGlowCube 가 렌더러에 덮어쓰던 바탕색을 재질에 그대로)
                gold = ChestMat(ChestGoldMatPath, sh, new Color(0.83f, 0.69f, 0.22f), new Color(1f, 0.85f, 0.4f), 2f, new Color(1f, 0.9f, 0.5f), 2f, 1.5f),
                // 띠·자물쇠 구멍·뚜껑 속 — 짙은 청동 (금과 한눈에 갈리게)
                trim = ChestMat(ChestTrimMatPath, sh, new Color(0.30f, 0.17f, 0.06f), new Color(1f, 0.62f, 0.28f), 0.45f, new Color(1f, 0.78f, 0.42f), 2.6f, 1.1f),
                // 열면 보이는 보물 더미 — 환하게
                treasure = ChestMat(ChestTreasureMatPath, sh, new Color(0.95f, 0.66f, 0.16f), new Color(1f, 0.74f, 0.26f), 1.4f, new Color(1f, 0.92f, 0.62f), 2f, 1.3f),   // 짙은 호박빛 금 — 위의 금화(몸통 금빛)가 도드라지게
            };
            var body = new ChestMesh(3);
            ChestBody(body);
            p.body = body.Save(ChestBodyPath, "HuntChestBody");
            var lid = new ChestMesh(2);
            ChestLid(lid);
            p.lid = lid.Save(ChestLidPath, "HuntChestLid");
            log.Add($"보물상자 모양: 몸통 {p.body.vertexCount} · 뚜껑 {p.lid.vertexCount} 꼭짓점");
            return p;
        }

        private static Material ChestMat(string path, Shader sh, Color baseC, Color glow, float glowI, Color edge, float edgeP, float edgeI)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = m == null;
            if (fresh) m = new Material(sh);
            m.shader = sh;
            m.SetColor("_BaseColor", baseC);
            m.SetColor("_GoldColor", new Color(0.83f, 0.69f, 0.22f));
            m.SetColor("_GlowColor", glow);
            m.SetFloat("_GlowIntensity", glowI);
            m.SetColor("_EdgeColor", edge);
            m.SetFloat("_EdgePower", edgeP);
            m.SetFloat("_EdgeIntensity", edgeI);
            m.SetColor("_InnerGlowColor", new Color(0.83f, 0.69f, 0.22f, 0.3f));
            m.SetFloat("_InnerGlowIntensity", 0.5f);
            m.SetFloat("_Metallic", 0.3f);
            m.SetFloat("_Smoothness", 0.7f);
            m.SetFloat("_PulseSpeed", 1f);
            m.SetFloat("_PulseMin", 0.8f);
            m.SetFloat("_PulseMax", 1.5f);
            if (fresh) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // 몸통 — 금 상자 + 위 테두리·아래 받침·세로 띠 둘·네 귀 보호대(짙은 청동) + 앞 자물쇠 + 열면 보이는 속(짙게)과 보물 더미
        private static void ChestBody(ChestMesh k)
        {
            const int G = 0, T = 1, Tr = 2;
            float hw = ChestHalfW, hd = ChestHalfD, bot = ChestBottom, seam = ChestSeam;
            float midY = (bot + seam) / 2f, hh = (seam - bot) / 2f;
            k.RoundedBox(G, new Vector3(0f, midY, 0f), new Vector3(hw, hh, hd), 0.035f, 3);

            const float rim = 0.02f;
            k.RoundedBox(T, new Vector3(0f, seam - 0.032f, 0f), new Vector3(hw + rim, 0.03f, hd + rim), 0.012f, 2);       // 위 테두리 (이음매 바로 아래)
            k.RoundedBox(T, new Vector3(0f, bot + 0.0275f, 0f), new Vector3(hw + rim, 0.0325f, hd + rim), 0.012f, 2);      // 아래 받침
            float top = seam - 0.002f, low = bot - 0.005f;
            for (int s = -1; s <= 1; s += 2)
                k.RoundedBox(T, new Vector3(s * 0.25f, (top + low) / 2f, 0f), new Vector3(0.0425f, (top - low) / 2f, hd + 0.026f), 0.012f, 2);   // 세로 띠 — 뚜껑의 띠와 이어진다
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    k.RoundedBox(T, new Vector3(sx * (hw - 0.035f), (top + bot - 0.002f) / 2f, sz * (hd - 0.035f)), new Vector3(0.06f, (top - bot + 0.002f) / 2f, 0.06f), 0.02f, 2);   // 귀 보호대

            // 자물쇠 — 금 판 + 열쇠 구멍(동그라미 + 아래 홈)
            float face = -(hd + 0.044f);
            k.RoundedBox(G, new Vector3(0f, 0f, -(hd + 0.022f)), new Vector3(0.075f, 0.085f, 0.022f), 0.02f, 2);
            k.Coin(T, new Vector3(0f, 0.018f, face - 0.002f), Vector3.back, 0.021f, 0.004f, 18);
            k.RoundedBox(T, new Vector3(0f, -0.014f, face - 0.002f), new Vector3(0.009f, 0.024f, 0.004f), 0.004f, 1);

            // 열면 보이는 속 — 짙은 바닥 + 환한 보물 더미 + 금화 (닫힌 뚜껑 안에 다 들어간다)
            k.RoundedBox(T, new Vector3(0f, seam + 0.002f, 0f), new Vector3(hw - 0.06f, 0.002f, hd - 0.06f), 0.002f, 1);
            var dc = new Vector3(0f, seam + 0.004f, -0.02f);
            var dr = new Vector3(0.38f, 0.16f, 0.22f);
            k.Dome(Tr, dc, dr, 6, 24);
            var rnd = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                float th = Mathf.Deg2Rad * (12f + 58f * (float)rnd.NextDouble());
                float ph = Mathf.PI * 2f * (i + 0.35f * (float)rnd.NextDouble()) / 9f;
                var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                var n = new Vector3(u.x / dr.x, u.y / dr.y, u.z / dr.z).normalized;
                var tilt = new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 0.8f;
                var up = (n + tilt).normalized;
                k.Coin(G, dc + Vector3.Scale(u, dr) + up * 0.002f, up, 0.05f, 0.008f, 16);
            }
        }

        // 뚜껑 — 둥근 금 뚜껑(반원통 + 양 끝 반원) + 속(짙게) + 아래 테두리 + 양 끝 띠 + 몸통 띠와 이어지는 띠 둘. 원점 = 경첩
        private static void ChestLid(ChestMesh k)
        {
            const int G = 0, T = 1;
            float hw = ChestHalfW, R = ChestHalfD, cz = -ChestHalfD;
            const int seg = 28;
            k.Barrel(G, -hw, hw, cz, R, seg);
            k.HalfDisc(G, -hw, -1f, cz, R, seg);
            k.HalfDisc(G, hw, 1f, cz, R, seg);
            k.Plane(T, new Vector3(-hw, 0f, 2f * cz), new Vector3(hw, 0f, 2f * cz), new Vector3(hw, 0f, 0f), new Vector3(-hw, 0f, 0f), Vector3.down);
            const float rim = 0.02f;
            k.RoundedBox(T, new Vector3(0f, 0.032f, cz), new Vector3(hw + rim, 0.03f, R + rim), 0.012f, 2);   // 몸통 위 테두리와 짝 — 사이 틈이 이음매
            for (int s = -1; s <= 1; s += 2)
            {
                k.Band(T, s * (hw - 0.022f), 0.054f, cz, R - 0.01f, R + 0.018f, seg);
                k.Band(T, s * 0.25f, 0.085f, cz, R - 0.01f, R + 0.026f, seg);
            }
        }

        /// <summary>코드로 짓는 메시 — 면마다 바깥을 알려 주면 감기는 방향을 맞춘다 (유니티는 Cross(b-a, c-a) 가 보는 쪽인 면이 앞)</summary>
        private class ChestMesh
        {
            private readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            private readonly List<int>[] tris;

            public ChestMesh(int subMeshes)
            {
                tris = new List<int>[subMeshes];
                for (int i = 0; i < subMeshes; i++) tris[i] = new List<int>();
            }

            private int Vert(Vector3 p, Vector3 normal) { v.Add(p); n.Add(normal); return v.Count - 1; }

            private void Tri(int sub, int a, int b, int c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), outward) < 0f) { int t = b; b = c; c = t; }
                tris[sub].Add(a); tris[sub].Add(b); tris[sub].Add(c);
            }

            private void Quad(int sub, int a, int b, int c, int d, Vector3 outward) { Tri(sub, a, b, c, outward); Tri(sub, a, c, d, outward); }

            public void Plane(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal) =>
                Quad(sub, Vert(a, normal), Vert(b, normal), Vert(c, normal), Vert(d, normal), normal);

            // 모서리가 둥근 상자 — 면마다 격자를 깔고 안쪽 상자 둘레로 반지름 r 만큼 감싼다. k = 모서리 반쪽(45°)을 나누는 수
            public void RoundedBox(int sub, Vector3 c, Vector3 h, float r, int k)
            {
                r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)));
                var ax = new[] { Steps(h.x, r, k), Steps(h.y, r, k), Steps(h.z, r, k) };
                Vector3 inner = h - Vector3.one * r;
                for (int a = 0; a < 3; a++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        int u = (a + 1) % 3, w = (a + 2) % 3;
                        var o = Vector3.zero;
                        o[a] = s;
                        int start = v.Count, cols = ax[w].Length;
                        for (int i = 0; i < ax[u].Length; i++)
                            for (int j = 0; j < cols; j++)
                            {
                                var p = Vector3.zero;
                                p[a] = s * h[a]; p[u] = ax[u][i]; p[w] = ax[w][j];
                                var q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                                var d = p - q;
                                var nn = d.sqrMagnitude > 1e-12f ? d.normalized : o;
                                Vert(c + q + nn * r, nn);
                            }
                        for (int i = 0; i < ax[u].Length - 1; i++)
                            for (int j = 0; j < cols - 1; j++)
                            {
                                int i00 = start + i * cols + j;
                                Quad(sub, i00, i00 + cols, i00 + cols + 1, i00 + 1, o);
                            }
                    }
            }

            // -h … -h+r · h-r … h — 둥근 부분은 각도가 고르게 (한 면이 45° 씩 맡는다)
            private static float[] Steps(float h, float r, int k)
            {
                var list = new List<float>();
                void Add(float x) { if (list.Count == 0 || x - list[list.Count - 1] > 1e-6f) list.Add(x); }
                if (r <= 0f || k <= 0) { Add(-h); Add(h); return list.ToArray(); }
                for (int i = 0; i <= k; i++) Add(-h + r * (1f - Mathf.Tan(Mathf.PI / 4f * (k - i) / k)));
                for (int i = k; i >= 0; i--) Add(h - r * (1f - Mathf.Tan(Mathf.PI / 4f * (k - i) / k)));
                return list.ToArray();
            }

            // 반원통 (축 X · 가운데 (y 0, z cz)) — φ 0 = 앞 아래(-Z) · 90° = 위 · 180° = 뒤 아래
            public void Barrel(int sub, float x0, float x1, float cz, float R, int seg)
            {
                int start = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    var d = Arc(Mathf.PI * i / seg);
                    Vert(new Vector3(x0, 0f, cz) + d * R, d);
                    Vert(new Vector3(x1, 0f, cz) + d * R, d);
                }
                for (int i = 0; i < seg; i++)
                {
                    int a = start + i * 2;
                    Quad(sub, a, a + 1, a + 3, a + 2, Arc(Mathf.PI * (i + 0.5f) / seg));
                }
            }

            public void HalfDisc(int sub, float x, float side, float cz, float R, int seg)
            {
                var nn = new Vector3(side, 0f, 0f);
                int c = Vert(new Vector3(x, 0f, cz), nn), start = v.Count;
                for (int i = 0; i <= seg; i++) Vert(new Vector3(x, 0f, cz) + Arc(Mathf.PI * i / seg) * R, nn);
                for (int i = 0; i < seg; i++) Tri(sub, c, start + i, start + i + 1, nn);
            }

            // 뚜껑을 감싸는 반원 쇠띠 — 가운데 xc · 폭 w · 반지름 ri~ro (안쪽 면은 뚜껑 속이라 안 만든다)
            public void Band(int sub, float xc, float w, float cz, float ri, float ro, int seg)
            {
                float x0 = xc - w / 2f, x1 = xc + w / 2f;
                Barrel(sub, x0, x1, cz, ro, seg);
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s < 0 ? x0 : x1;
                    var nn = new Vector3(s, 0f, 0f);
                    int start = v.Count;
                    for (int i = 0; i <= seg; i++)
                    {
                        var d = Arc(Mathf.PI * i / seg);
                        Vert(new Vector3(x, 0f, cz) + d * ri, nn);
                        Vert(new Vector3(x, 0f, cz) + d * ro, nn);
                    }
                    for (int i = 0; i < seg; i++) { int a = start + i * 2; Quad(sub, a, a + 1, a + 3, a + 2, nn); }
                }
                for (int e = -1; e <= 1; e += 2)   // 앞(-1)·뒤(+1) 끝 — 뚜껑 아랫면 높이
                    Plane(sub, new Vector3(x0, 0f, cz + e * ri), new Vector3(x1, 0f, cz + e * ri), new Vector3(x1, 0f, cz + e * ro), new Vector3(x0, 0f, cz + e * ro), Vector3.down);
            }

            // 위쪽 반 타원체 (보물 더미)
            public void Dome(int sub, Vector3 c, Vector3 rad, int rings, int seg)
            {
                int start = v.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float th = Mathf.PI / 2f * i / rings;
                    for (int j = 0; j <= seg; j++)
                    {
                        float ph = 2f * Mathf.PI * j / seg;
                        var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                        Vert(c + Vector3.Scale(u, rad), new Vector3(u.x / rad.x, u.y / rad.y, u.z / rad.z).normalized);
                    }
                }
                for (int i = 0; i < rings; i++)
                    for (int j = 0; j < seg; j++)
                    {
                        int a = start + i * (seg + 1) + j, b = a + seg + 1;
                        Quad(sub, a, a + 1, b + 1, b, (v[a + 1] + v[b]) * 0.5f - c);
                    }
            }

            // 납작한 원기둥 (금화 · 열쇠 구멍) — up 이 앞면
            public void Coin(int sub, Vector3 c, Vector3 up, float r, float halfT, int seg)
            {
                up.Normalize();
                var t1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var t2 = Vector3.Cross(up, t1);
                for (int s = -1; s <= 1; s += 2)
                {
                    var nn = up * s;
                    var cc = c + up * (halfT * s);
                    int ci = Vert(cc, nn), st = v.Count;
                    for (int i = 0; i <= seg; i++) { float f = 2f * Mathf.PI * i / seg; Vert(cc + (t1 * Mathf.Cos(f) + t2 * Mathf.Sin(f)) * r, nn); }
                    for (int i = 0; i < seg; i++) Tri(sub, ci, st + i, st + i + 1, nn);
                }
                int s0 = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float f = 2f * Mathf.PI * i / seg;
                    var d = t1 * Mathf.Cos(f) + t2 * Mathf.Sin(f);
                    Vert(c + d * r - up * halfT, d);
                    Vert(c + d * r + up * halfT, d);
                }
                for (int i = 0; i < seg; i++)
                {
                    int a = s0 + i * 2;
                    float f = 2f * Mathf.PI * (i + 0.5f) / seg;
                    Quad(sub, a, a + 1, a + 3, a + 2, t1 * Mathf.Cos(f) + t2 * Mathf.Sin(f));
                }
            }

            private static Vector3 Arc(float f) => new Vector3(0f, Mathf.Sin(f), -Mathf.Cos(f));

            // 같은 에셋에 덮어쓴다 — 새로 만들면 프리팹이 가리키던 메시가 끊긴다
            public Mesh Save(string path, string name)
            {
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool fresh = m == null;
                if (fresh) m = new Mesh();
                m.Clear();
                m.name = name;
                m.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, new Vector2[v.Count]);
                m.subMeshCount = tris.Length;
                for (int i = 0; i < tris.Length; i++) m.SetTriangles(tris[i], i);
                m.RecalculateBounds();
                if (fresh) AssetDatabase.CreateAsset(m, path);
                else EditorUtility.SetDirty(m);
                return m;
            }
        }
    }
}
