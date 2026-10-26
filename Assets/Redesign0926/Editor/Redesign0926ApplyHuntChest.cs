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
    /// 10-24 주최자가 고르는 모양(R0926HuntChest.Skin — 서버 hunts.object_kind 와 같은 이름)도 여기서: 선물상자 · 보물알(뚜껑이 열린다) ·
    /// 보석 · 별 · 하트(빛만 솟는다) · 사진 액자(사진은 런타임이 끼운다) · 3D 모델(빈 자리 — 런타임이 GLB 를 붙인다).
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
        private static readonly Vector3 ChestBoxCenter = new Vector3(0f, 0.01f, -0.01f), ChestBoxSize = new Vector3(1.3f, 1.12f, 0.95f);   // 누르기 쉽게 조금 크게

        // 선물상자 — 덮개가 몸통보다 조금 크다. 경첩 = 덮개 뒤 아래 모서리
        private const float GiftHalf = 0.40f, GiftBottom = -0.42f, GiftSeam = 0.12f, GiftCap = 0.43f, GiftCapH = 0.16f;
        private static readonly Vector3 GiftHinge = new Vector3(0f, GiftSeam, GiftCap);
        // 보물알 — 가운데 띠에서 위아래로 갈린다 (위가 조금 더 뾰족)
        private const float EggR = 0.36f, EggLow = 0.40f, EggHigh = 0.47f, EggSeam = -0.02f;
        private static readonly Vector3 EggHinge = new Vector3(0f, EggSeam, EggR);
        // 사진 액자 — 사진 칸은 R0926HuntChest.PhotoSide · PhotoY · PhotoZ 와 같다 (바꾸면 같이)
        private const float PhotoWin = 0.34f, PhotoMid = 0.12f, PhotoHalfW = 0.42f, PhotoTop = 0.54f, PhotoBot = -0.42f, PhotoDepth = 0.045f;
        private const float StarY = -0.04f, HeartScale = 0.0297f;
        // 비밀번호를 건 보물의 자물쇠 (10-26) — 원점 = 몸통 위 가운데(고리 다리가 들어가는 곳). 고리는 왼쪽 다리를 축으로 열린다
        private const string LockSteelMatPath = "Assets/Materials/AR/HuntLockSteel_Mat.mat";
        private const float LockHalfW = 0.09f, LockHalfH = 0.075f, LockHalfD = 0.03f;
        private const float ShackleR = 0.055f, ShackleTube = 0.016f, ShackleTop = 0.045f;   // 고리 반지름 · 굵기 · 반원이 시작되는 높이

        private class ChestParts
        {
            public Mesh body, lid, lockBody, lockShackle;
            public Material gold, trim, treasure, steel;
            public readonly List<R0926HuntChest.Skin> skins = new List<R0926HuntChest.Skin>();
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
            // 자물쇠 — 은빛 강철 몸통 · 금 동그라미 · 짙은 열쇠 구멍 (금·빨강·보라 모양 어디에 걸어도 갈려 보이게)
            p.steel = ChestMat(LockSteelMatPath, sh, new Color(0.72f, 0.75f, 0.80f), new Color(0.85f, 0.9f, 1f), 0.5f, new Color(0.95f, 0.97f, 1f), 2.4f, 1.2f, new Color(0.85f, 0.88f, 0.95f));
            p.lockBody = ShapeMesh("HuntPadlockBody", 3, PadlockBody);
            p.lockShackle = ShapeMesh("HuntPadlockShackle", 1, PadlockShackle);
            ChestSkins(p, sh, log);
            return p;
        }

        // 주최자가 고르는 모양들 — 보물상자도 하나로 넣어 두어야 다른 모양에서 돌아올 수 있다 (사진·모델을 못 받으면 보물상자)
        private static void ChestSkins(ChestParts p, Shader sh, List<string> log)
        {
            const string dir = "Assets/Materials/AR/";
            var red = ChestMat(dir + "HuntGiftRed_Mat.mat", sh, new Color(0.80f, 0.10f, 0.16f), new Color(1f, 0.30f, 0.32f), 0.8f, new Color(1f, 0.70f, 0.62f), 2.2f, 1.2f, new Color(0.95f, 0.35f, 0.35f));
            var shell = ChestMat(dir + "HuntEggShell_Mat.mat", sh, new Color(0.64f, 0.50f, 0.94f), new Color(0.78f, 0.66f, 1f), 0.7f, new Color(0.95f, 0.90f, 1f), 2f, 1.3f, new Color(0.75f, 0.62f, 1f));
            var gem = ChestMat(dir + "HuntGem_Mat.mat", sh, new Color(0.18f, 0.72f, 0.95f), new Color(0.45f, 0.90f, 1f), 1.1f, new Color(0.85f, 0.98f, 1f), 1.5f, 2f, new Color(0.6f, 0.95f, 1f));
            var star = ChestMat(dir + "HuntStar_Mat.mat", sh, new Color(1f, 0.80f, 0.22f), new Color(1f, 0.86f, 0.40f), 2.2f, new Color(1f, 0.95f, 0.65f), 2f, 1.6f);
            var rose = ChestMat(dir + "HuntHeart_Mat.mat", sh, new Color(0.96f, 0.30f, 0.48f), new Color(1f, 0.45f, 0.58f), 0.9f, new Color(1f, 0.82f, 0.88f), 2f, 1.4f, new Color(1f, 0.55f, 0.65f));
            var cream = ChestMat(dir + "HuntPhotoFrame_Mat.mat", sh, new Color(0.96f, 0.94f, 0.89f), new Color(1f, 0.95f, 0.85f), 0.35f, new Color(1f, 0.84f, 0.45f), 2.6f, 1.1f);

            // lockPos · lockTilt · lockScale = 비밀번호를 건 보물의 자물쇠 자리 — 앞(-Z)에 매단다 (뚜껑 있는 모양은 이음매 아래 · 기울기는 위가 상자 쪽으로)
            void Add(string kind, Mesh body, Mesh lid, Material[] bodyMats, Material[] lidMats, Vector3 hinge, float seam, Vector3 boxCenter, Vector3 boxSize,
                     Vector3 lockPos, float lockTilt, float lockScale) =>
                p.skins.Add(new R0926HuntChest.Skin { kind = kind, body = body, lid = lid, bodyMats = bodyMats, lidMats = lidMats, hinge = hinge, seam = seam, boxCenter = boxCenter, boxSize = boxSize,
                                                      lockPos = lockPos, lockTilt = lockTilt, lockScale = lockScale });
            var none = new Material[0];
            p.skins.Clear();
            Add("chest", p.body, p.lid, new[] { p.gold, p.trim, p.treasure }, new[] { p.gold, p.trim }, ChestHinge, ChestSeam, ChestBoxCenter, ChestBoxSize,
                new Vector3(0f, -0.10f, -0.397f), 10f, 1f);
            Add("gift", ShapeMesh("HuntGiftBody", 4, GiftBody), ShapeMesh("HuntGiftLid", 3, GiftLid), new[] { red, p.gold, p.trim, p.treasure }, new[] { red, p.gold, p.trim },
                GiftHinge, GiftSeam, new Vector3(0f, 0.01f, 0f), new Vector3(1.15f, 1.1f, 1.15f), new Vector3(0f, -0.03f, -0.441f), 10f, 1f);
            Add("egg", ShapeMesh("HuntEggBody", 4, EggBody), ShapeMesh("HuntEggLid", 3, EggLid), new[] { shell, p.gold, p.trim, p.treasure }, new[] { shell, p.gold, p.trim },
                EggHinge, EggSeam, new Vector3(0f, 0.03f, 0f), new Vector3(0.95f, 1.1f, 0.95f), new Vector3(0f, -0.14f, -0.407f), 6f, 0.9f);
            Add("gem", ShapeMesh("HuntGem", 1, Gem), null, new[] { gem }, none, Vector3.zero, 0.30f, new Vector3(0f, -0.07f, 0f), new Vector3(1.05f, 0.95f, 1.05f),
                new Vector3(0f, -0.02f, -0.473f), 10f, 0.9f);
            Add("star", ShapeMesh("HuntStar", 1, Star), null, new[] { star }, none, Vector3.zero, StarY, new Vector3(0f, StarY, 0f), new Vector3(1.15f, 1.1f, 0.7f),
                new Vector3(0f, -0.35f, 0f), 0f, 0.9f);
            Add("heart", ShapeMesh("HuntHeart", 3, Heart), null, new[] { rose, p.gold, p.trim }, none, Vector3.zero, 0f, Vector3.zero, new Vector3(1.15f, 1f, 0.7f),
                new Vector3(0f, -0.055f, -0.249f), 10f, 0.9f);
            Add("photo", ShapeMesh("HuntPhotoFrame", 2, PhotoFrame), null, new[] { cream, p.gold }, none, Vector3.zero, 0.30f, new Vector3(0f, 0.06f, 0f), new Vector3(1f, 1.1f, 0.5f),
                new Vector3(0f, -0.33f, -0.086f), 10f, 0.9f);
            Add("model", null, null, none, none, Vector3.zero, 0f, Vector3.zero, Vector3.one * 1.2f, new Vector3(0f, -0.30f, -0.45f), 0f, 1f);   // 모델을 붙이면 런타임이 모델 앞 아래로 옮긴다
            log.Add("보물 모양 " + p.skins.Count + "가지: " + string.Join(" · ", p.skins.ConvertAll(s => s.kind + (s.body != null ? " " + s.body.vertexCount : ""))));
        }

        private static Mesh ShapeMesh(string name, int subMeshes, System.Action<ChestMesh> build)
        {
            var k = new ChestMesh(subMeshes);
            build(k);
            return k.Save(ChestMeshDir + "/" + name + ".asset", name);
        }

        // tint = 윗면이 물드는 색·반짝임 색 (기본 금 — 금이 아닌 모양은 제 빛깔로)
        private static Material ChestMat(string path, Shader sh, Color baseC, Color glow, float glowI, Color edge, float edgeP, float edgeI, Color? tint = null)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = m == null;
            if (fresh) m = new Material(sh);
            m.shader = sh;
            m.SetColor("_BaseColor", baseC);
            m.SetColor("_GoldColor", tint ?? new Color(0.83f, 0.69f, 0.22f));
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
            Hoard(k, G, Tr, new Vector3(0f, seam + 0.004f, -0.02f), new Vector3(0.38f, 0.16f, 0.22f), 9, 7);
        }

        // 보물 더미(반 타원체) + 비스듬히 얹힌 금화 — dc 바닥 가운데 · dr 더미 반지름
        private static void Hoard(ChestMesh k, int coin, int heap, Vector3 dc, Vector3 dr, int coins, int seed)
        {
            k.Dome(heap, dc, dr, 6, 24);
            var rnd = new System.Random(seed);
            for (int i = 0; i < coins; i++)
            {
                float th = Mathf.Deg2Rad * (12f + 58f * (float)rnd.NextDouble());
                float ph = Mathf.PI * 2f * (i + 0.35f * (float)rnd.NextDouble()) / coins;
                var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                var n = new Vector3(u.x / dr.x, u.y / dr.y, u.z / dr.z).normalized;
                var tilt = new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 0.8f;
                var up = (n + tilt).normalized;
                k.Coin(coin, dc + Vector3.Scale(u, dr) + up * 0.002f, up, 0.05f, 0.008f, 16);
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

        // 선물상자 몸통 — 빨간 상자 + 네 옆면·바닥을 감싸는 금 리본 열십자 + 열면 보이는 속(짙게)과 보물 더미
        private static void GiftBody(ChestMesh k)
        {
            const int R = 0, G = 1, T = 2, Tr = 3;
            float h = GiftHalf, bot = GiftBottom, seam = GiftSeam;
            float midY = (bot + seam) / 2f, hh = (seam - bot) / 2f;
            k.RoundedBox(R, new Vector3(0f, midY, 0f), new Vector3(h, hh, h), 0.03f, 3);
            k.RoundedBox(G, new Vector3(0f, midY - 0.003f, 0f), new Vector3(0.06f, hh, h + 0.008f), 0.01f, 2);
            k.RoundedBox(G, new Vector3(0f, midY - 0.003f, 0f), new Vector3(h + 0.008f, hh, 0.06f), 0.01f, 2);
            k.RoundedBox(T, new Vector3(0f, seam + 0.002f, 0f), new Vector3(h - 0.04f, 0.002f, h - 0.04f), 0.002f, 1);
            Hoard(k, G, Tr, new Vector3(0f, seam + 0.004f, 0f), new Vector3(0.30f, 0.10f, 0.30f), 8, 11);   // 덮개(높이 0.16) 안에 들어간다
        }

        // 선물상자 덮개 — 빨간 덮개 + 리본 열십자 + 나비매듭(고리 둘 · 매듭) + 속(짙게). 원점 = 경첩
        private static void GiftLid(ChestMesh k)
        {
            const int R = 0, G = 1, T = 2;
            float c = GiftCap, hgt = GiftCapH, cz = -GiftCap, e = c - 0.01f;
            k.RoundedBox(R, new Vector3(0f, hgt / 2f, cz), new Vector3(c, hgt / 2f, c), 0.03f, 3);
            k.Plane(T, new Vector3(-e, -0.0015f, cz - e), new Vector3(e, -0.0015f, cz - e), new Vector3(e, -0.0015f, cz + e), new Vector3(-e, -0.0015f, cz + e), Vector3.down);
            k.RoundedBox(G, new Vector3(0f, hgt / 2f + 0.003f, cz), new Vector3(0.06f, hgt / 2f + 0.002f, c + 0.008f), 0.01f, 2);
            k.RoundedBox(G, new Vector3(0f, hgt / 2f + 0.003f, cz), new Vector3(c + 0.008f, hgt / 2f + 0.002f, 0.06f), 0.01f, 2);
            for (int s = -1; s <= 1; s += 2)
                k.Torus(G, new Vector3(s * 0.10f, hgt + 0.06f, cz), Quaternion.Euler(0f, 0f, s * 25f), new Vector2(0.10f, 0.055f), 0.026f, 24, 10);
            k.Ellipsoid(G, new Vector3(0f, hgt + 0.03f, cz), new Vector3(0.055f, 0.045f, 0.06f), Quaternion.identity, 8, 14);
        }

        // 보물알 아래쪽 — 보랏빛 껍데기(반 타원체) + 가운데 금 띠 + 금 점 + 열면 보이는 속(짙게)과 보물 더미
        private static void EggBody(ChestMesh k)
        {
            const int S = 0, G = 1, T = 2, Tr = 3;
            var c = new Vector3(0f, EggSeam, 0f);
            var rad = new Vector3(EggR, -EggLow, EggR);
            k.Dome(S, c, rad, 10, 36);
            k.RingY(G, c, EggR - 0.01f, EggR + 0.014f, -0.04f, -0.001f, 48);
            EggDots(k, G, c, rad, 58f, 7, 0f);
            k.Disc(T, c + Vector3.up * 0.002f, Vector3.up, EggR - 0.005f, 36);
            Hoard(k, G, Tr, c + Vector3.up * 0.004f, new Vector3(0.24f, 0.12f, 0.24f), 7, 5);
        }

        // 보물알 위쪽(뚜껑) — 껍데기 + 금 띠 + 금 점 + 꼭대기 금 구슬 + 속(짙게). 원점 = 경첩
        private static void EggLid(ChestMesh k)
        {
            const int S = 0, G = 1, T = 2;
            var c = new Vector3(0f, 0f, -EggR);
            var rad = new Vector3(EggR, EggHigh, EggR);
            k.Dome(S, c, rad, 10, 36);
            k.RingY(G, c, EggR - 0.01f, EggR + 0.014f, 0.001f, 0.04f, 48);
            EggDots(k, G, c, rad, 52f, 6, 0.5f);
            k.Ellipsoid(G, c + Vector3.up * (EggHigh + 0.012f), Vector3.one * 0.035f, Quaternion.identity, 8, 12);
            k.Disc(T, c + Vector3.down * 0.0015f, Vector3.down, EggR - 0.004f, 36);
        }

        // 껍데기 위 금 점 한 줄 — thDeg 극에서 잰 각도 (아래쪽은 rad.y 가 음수)
        private static void EggDots(ChestMesh k, int sub, Vector3 c, Vector3 rad, float thDeg, int count, float offset)
        {
            float th = thDeg * Mathf.Deg2Rad;
            for (int i = 0; i < count; i++)
            {
                float ph = 2f * Mathf.PI * (i + offset) / count;
                var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                var n = new Vector3(u.x / rad.x, u.y / rad.y, u.z / rad.z).normalized;
                k.Coin(sub, c + Vector3.Scale(u, rad) + n * 0.003f, n, 0.034f, 0.004f, 14);
            }
        }

        // 자물쇠 몸통 — 둥근 강철 몸통 + 앞뒤 금 동그라미 + 열쇠 구멍(짙게). 원점 = 몸통 위 가운데
        private static void PadlockBody(ChestMesh k)
        {
            const int S = 0, G = 1, T = 2;
            k.RoundedBox(S, new Vector3(0f, -LockHalfH, 0f), new Vector3(LockHalfW, LockHalfH, LockHalfD), 0.028f, 3);
            for (int s = -1; s <= 1; s += 2)
            {
                var face = s < 0 ? Vector3.back : Vector3.forward;
                float z = s * (LockHalfD + 0.002f);
                k.Coin(G, new Vector3(0f, -LockHalfH + 0.005f, z), face, 0.034f, 0.003f, 24);
                k.Coin(T, new Vector3(0f, -LockHalfH + 0.016f, z + s * 0.0035f), face, 0.011f, 0.0012f, 16);
                k.RoundedBox(T, new Vector3(0f, -LockHalfH - 0.002f, z + s * 0.0035f), new Vector3(0.0045f, 0.014f, 0.0012f), 0.002f, 1);
            }
        }

        // 자물쇠 고리 — 다리 둘 + 위 반원. 원점 = 왼쪽 다리 (열 때 이 다리를 축으로 돈다 — 그래서 더 길어 튀어 올라도 몸통 속에 남는다)
        private static void PadlockShackle(ChestMesh k)
        {
            for (int i = 0; i < 2; i++)
            {
                float low = i == 0 ? -0.06f : -0.02f;
                k.Coin(0, new Vector3(i * 2f * ShackleR, (low + ShackleTop) / 2f, 0f), Vector3.up, ShackleTube, (ShackleTop - low) / 2f, 14);
            }
            k.Torus(0, new Vector3(ShackleR, ShackleTop, 0f), Quaternion.identity, new Vector2(ShackleR, ShackleR), ShackleTube, 20, 12, 0f, Mathf.PI);
        }

        // 보석 — 브릴리언트 컷을 줄인 모양 (윗면 · 크라운 24면 · 거들 · 파빌리온 16면). 면마다 꼭짓점을 따로 두어 반짝임이 갈린다
        private static void Gem(ChestMesh k)
        {
            const int n = 8;   // 윗면 꼭짓점 — 거들은 두 배
            const float table = 0.24f, girdle = 0.44f, yTable = 0.30f, yTop = 0.10f, yLow = 0.065f, yCulet = -0.44f;
            var mid = new Vector3(0f, 0.05f, 0f);
            Vector3 At(float r, float y, int j) { float a = Mathf.PI * j / n; return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)); }
            void Face(Vector3 a, Vector3 b, Vector3 c) => k.Facet(0, a, b, c, (a + b + c) / 3f - mid);
            var top = new Vector3(0f, yTable, 0f);
            for (int i = 0; i < n; i++)
            {
                Vector3 t0 = At(table, yTable, 2 * i), t1 = At(table, yTable, 2 * i + 2);
                Face(top, t0, t1);
                Face(t0, At(girdle, yTop, 2 * i + 1), t1);                              // 별 면
                Face(t0, At(girdle, yTop, 2 * i - 1), At(girdle, yTop, 2 * i));        // 베젤 반쪽 둘
                Face(t0, At(girdle, yTop, 2 * i), At(girdle, yTop, 2 * i + 1));
            }
            var culet = new Vector3(0f, yCulet, 0f);
            for (int j = 0; j < 2 * n; j++)
            {
                Vector3 a = At(girdle, yTop, j), b = At(girdle, yTop, j + 1), c = At(girdle, yLow, j + 1), d = At(girdle, yLow, j);
                Face(a, b, c);
                Face(a, c, d);
                Face(d, c, culet);
            }
        }

        // 별 — 다섯 꼭짓점 별을 앞뒤로 뾰족하게 (면마다 반짝임) + 얇은 옆 띠
        private static void Star(ChestMesh k)
        {
            const float outer = 0.5f, inner = 0.21f, rim = 0.035f, apex = 0.17f;
            var c = new Vector3(0f, StarY, 0f);
            var pts = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? outer : inner;
                pts[i] = c + new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), 0f);
            }
            var z = new Vector3(0f, 0f, rim);
            for (int i = 0; i < 10; i++)
            {
                Vector3 a = pts[i], b = pts[(i + 1) % 10], m = (a + b) / 2f - c;
                for (int s = -1; s <= 1; s += 2)
                    k.Facet(0, c + new Vector3(0f, 0f, s * apex), a + z * s, b + z * s, m + z * (3f * s));
                k.Facet(0, a - z, b - z, b + z, m);
                k.Facet(0, a - z, b + z, a + z, m);
            }
        }

        // 하트 — 하트 곡선을 가운데 쪽으로 줄여 가며 앞뒤로 부풀린 쿠션 + 앞뒤 금 자물쇠(열쇠 구멍)
        private static void Heart(ChestMesh k)
        {
            const int H = 0, G = 1, T = 2;
            const float depth = 0.2f;
            var c = new Vector2(0f, 0.5f * HeartScale);   // 하트 곡선이 이 점에서 다 보인다 (오목한 위 가운데까지)
            k.Puff(H, HeartAt, c, depth, 72, 8);
            for (int s = -1; s <= 1; s += 2)
            {
                var face = new Vector3(0f, 0f, s);
                k.Coin(G, new Vector3(c.x, c.y, s * (depth - 0.004f)), face, 0.05f, 0.012f, 24);   // 볼록한 면에 박히게 두껍게
                k.Coin(T, new Vector3(c.x, c.y + 0.01f, s * (depth + 0.0095f)), face, 0.014f, 0.0015f, 16);
                k.RoundedBox(T, new Vector3(c.x, c.y - 0.012f, s * (depth + 0.0095f)), new Vector3(0.006f, 0.016f, 0.0015f), 0.0015f, 1);
            }
        }

        // 하트 곡선 (x = 16 sin³t · y = 13cos t − 5cos 2t − 2cos 3t − cos 4t) — 위아래 가운데를 0 에 맞춰 상자 크기로
        private static Vector2 HeartAt(float t)
        {
            float s = Mathf.Sin(t);
            float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
            return new Vector2(16f * s * s * s, y + 2.5f) * HeartScale;
        }

        // 사진 액자 — 폴라로이드처럼 아래가 넓은 크림색 테 + 사진 뒤 판 + 아래 테 앞뒤 금 장식. 사진은 R0926HuntChest.ShowPhoto 가 앞뒤로 끼운다
        private static void PhotoFrame(ChestMesh k)
        {
            const int F = 0, G = 1;
            float w = PhotoHalfW, d = PhotoDepth, win = PhotoWin, y0 = PhotoMid - win, y1 = PhotoMid + win;
            k.RoundedBox(F, new Vector3(0f, (y1 + PhotoTop) / 2f, 0f), new Vector3(w, (PhotoTop - y1) / 2f, d), 0.015f, 2);
            k.RoundedBox(F, new Vector3(0f, (PhotoBot + y0) / 2f, 0f), new Vector3(w, (y0 - PhotoBot) / 2f, d), 0.015f, 2);
            for (int s = -1; s <= 1; s += 2)
                k.RoundedBox(F, new Vector3(s * (win + w) / 2f, PhotoMid, 0f), new Vector3((w - win) / 2f, win + 0.015f, d), 0.015f, 2);
            k.RoundedBox(F, new Vector3(0f, PhotoMid, 0f), new Vector3(win + 0.01f, win + 0.01f, 0.012f), 0.004f, 1);   // 사진 뒤 판
            for (int s = -1; s <= 1; s += 2)
                k.Coin(G, new Vector3(0f, (PhotoBot + y0) / 2f, s * (d + 0.004f)), new Vector3(0f, 0f, s), 0.045f, 0.004f, 24);
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

            // 한 면짜리 원판 (보물알 속 바닥·뚜껑 속)
            public void Disc(int sub, Vector3 c, Vector3 normal, float r, int seg)
            {
                normal.Normalize();
                var t1 = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var t2 = Vector3.Cross(normal, t1);
                int ci = Vert(c, normal), st = v.Count;
                for (int i = 0; i <= seg; i++) { float f = 2f * Mathf.PI * i / seg; Vert(c + (t1 * Mathf.Cos(f) + t2 * Mathf.Sin(f)) * r, normal); }
                for (int i = 0; i < seg; i++) Tri(sub, ci, st + i, st + i + 1, normal);
            }

            // 평평한 삼각형 — 꼭짓점을 따로 두어 면마다 빛이 갈린다 (보석·별)
            public void Facet(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                var nn = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(nn, outward) < 0f) nn = -nn;
                Tri(sub, Vert(a, nn), Vert(b, nn), Vert(c, nn), nn);
            }

            // 타원체 (매듭·구슬) — rot 로 돌린다
            public void Ellipsoid(int sub, Vector3 c, Vector3 rad, Quaternion rot, int rings, int seg)
            {
                int start = v.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float th = Mathf.PI * i / rings;
                    for (int j = 0; j <= seg; j++)
                    {
                        float ph = 2f * Mathf.PI * j / seg;
                        var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                        Vert(c + rot * Vector3.Scale(u, rad), rot * new Vector3(u.x / rad.x, u.y / rad.y, u.z / rad.z).normalized);
                    }
                }
                for (int i = 0; i < rings; i++)
                    for (int j = 0; j < seg; j++)
                    {
                        int a = start + i * (seg + 1) + j, b = a + seg + 1;
                        Quad(sub, a, a + 1, b + 1, b, (v[a] + v[a + 1] + v[b] + v[b + 1]) * 0.25f - c);
                    }
            }

            // 타원 고리 (나비매듭) — 고리는 XY 면 (radii = 가로·세로 반지름) · r = 관 굵기 · rot 로 돌린다 · a0~a1 = 고리 일부만 (자물쇠 고리는 위 반원)
            public void Torus(int sub, Vector3 c, Quaternion rot, Vector2 radii, float r, int su, int sv, float a0 = 0f, float a1 = 2f * Mathf.PI)
            {
                int start = v.Count;
                for (int i = 0; i <= su; i++)
                {
                    float a = a0 + (a1 - a0) * i / su;
                    var p = new Vector3(radii.x * Mathf.Cos(a), radii.y * Mathf.Sin(a), 0f);
                    var d = new Vector3(radii.y * Mathf.Cos(a), radii.x * Mathf.Sin(a), 0f).normalized;   // 타원의 바깥 방향
                    for (int j = 0; j <= sv; j++)
                    {
                        float b = 2f * Mathf.PI * j / sv;
                        var nn = d * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                        Vert(c + rot * (p + nn * r), rot * nn);
                    }
                }
                for (int i = 0; i < su; i++)
                    for (int j = 0; j < sv; j++)
                    {
                        int a = start + i * (sv + 1) + j, b = a + sv + 1;
                        Quad(sub, a, a + 1, b + 1, b, n[a] + n[a + 1] + n[b] + n[b + 1]);
                    }
            }

            // 가로 금 띠 (보물알) — 바깥 옆면 + 위·아래 고리면 (안쪽은 껍데기 속). h0~h1 = c 에서 잰 높이
            public void RingY(int sub, Vector3 c, float ri, float ro, float h0, float h1, int seg)
            {
                int s0 = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float f = 2f * Mathf.PI * i / seg;
                    var d = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
                    Vert(c + d * ro + Vector3.up * h0, d);
                    Vert(c + d * ro + Vector3.up * h1, d);
                }
                for (int i = 0; i < seg; i++)
                {
                    int a = s0 + i * 2;
                    float f = 2f * Mathf.PI * (i + 0.5f) / seg;
                    Quad(sub, a, a + 1, a + 3, a + 2, new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)));
                }
                for (int e = 0; e < 2; e++)
                {
                    float h = e == 0 ? h0 : h1;
                    var nn = e == 0 ? Vector3.down : Vector3.up;
                    int st = v.Count;
                    for (int i = 0; i <= seg; i++)
                    {
                        float f = 2f * Mathf.PI * i / seg;
                        var d = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
                        Vert(c + d * ri + Vector3.up * h, nn);
                        Vert(c + d * ro + Vector3.up * h, nn);
                    }
                    for (int i = 0; i < seg; i++) { int a = st + i * 2; Quad(sub, a, a + 1, a + 3, a + 2, nn); }
                }
            }

            // 부푼 쿠션 (하트) — 윤곽 outline(t, 0~2π) 을 가운데 c 쪽으로 줄여 가며 앞뒤로 depth 만큼 부풀린다.
            // 어느 방향으로 잘라도 단면이 타원이라 둥글다. c 에서 윤곽 전체가 보여야 한다 (별 모양 영역)
            public void Puff(int sub, System.Func<float, Vector2> outline, Vector2 c, float depth, int seg, int rings)
            {
                var c3 = new Vector3(c.x, c.y, 0f);
                float dt = Mathf.PI / seg, dth = Mathf.PI / 4f / rings, top = Mathf.PI / 2f;   // 법선은 이웃 반 칸 차이로
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 P(float th, float t)
                    {
                        var q = c + (outline(t) - c) * Mathf.Cos(th);
                        return new Vector3(q.x, q.y, s * depth * Mathf.Sin(th));
                    }
                    int start = v.Count;
                    for (int i = 0; i <= rings; i++)
                    {
                        float th = top * i / rings;
                        for (int j = 0; j <= seg; j++)
                        {
                            float t = 2f * Mathf.PI * j / seg;
                            var p = P(th, t);
                            var nn = Vector3.Cross(P(th, t + dt) - P(th, t - dt), P(Mathf.Min(th + dth, top), t) - P(Mathf.Max(th - dth, 0f), t));
                            nn = nn.sqrMagnitude < 1e-12f ? new Vector3(0f, 0f, s) : nn.normalized;   // 꼭대기는 한 점
                            if (Vector3.Dot(nn, p - c3) < 0f) nn = -nn;
                            Vert(p, nn);
                        }
                    }
                    for (int i = 0; i < rings; i++)
                        for (int j = 0; j < seg; j++)
                        {
                            int a = start + i * (seg + 1) + j, b = a + seg + 1;
                            Quad(sub, a, a + 1, b + 1, b, (v[a] + v[a + 1] + v[b] + v[b + 1]) * 0.25f - c3);
                        }
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
