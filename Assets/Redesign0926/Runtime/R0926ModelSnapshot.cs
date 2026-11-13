using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D 모델의 정지 사진 (11-13 대표님 「3D모델로 하더라도 그 모델의 단순한 스틸 모습을 반영해서 가지고 있어야 … 목록 리스트 우측에」).
/// 화면 밖 무대에 GLB 를 띄워 이 무대 전용 카메라로 한 번 찍는다 — 바탕이 투명한 PNG (서버 /thumb 가 투명도를 지킨다).
///  · 무대의 렌더러는 카메라를 직접 돌리는 그 순간에만 켠다 — AR 화면에는 안 보인다
///  · 스르륵 드러나는 연출(재질을 투명에서 1초에 걸쳐 진하게)은 끈다 — 불러오자마자 찍으면 거의 빈 그림이 된다
///  · 뼈로 움직이는(스킨) 메시는 그 자세를 고정 메시로 구워 찍는다 — 꺼 둔 스킨 렌더러는 자세 계산을 건너뛴다
///  · 검은 바탕 · 흰 바탕으로 두 번 찍어 그 차이로 투명도를 구한다 — 렌더 파이프라인이 알파를 지키는지와 상관없이 가장자리까지 깨끗하게
///  · 카메라가 그림에 남기는 값이 감마인지 선형인지 먼저 잰다 — 모델과 같은 재질로 감마 0.5 회색 판을 찍어 본다.
///    에디터(D3D11)는 선형으로 남겨 색이 어둡게 나왔다(바탕색은 따로 바뀌어 바탕만으로는 못 잰다). 기기마다 달라 매번 잰다
///  · 살짝 내려다보는 비스듬한 앞모습 — 가장 긴 변이 칸에 들어오게 맞춘다
///  · 못 열거나 늦으면 null — 사진 없이 올린다. 찍던 쪽이 도중에 꺼져도 무대는 스스로 치운다
/// </summary>
public class R0926ModelSnapshot : MonoBehaviour
{
    private const float FieldOfView = 30f;
    private const float Distance = 2.3f;     // 가장 긴 변이 1 인 모델이 칸에 꽉 차지 않게 들어오는 거리 (화각 30도)
    private const float Tilt = 12f;          // 위에서 내려다보는 각
    private const float Turn = 20f;          // 앞에서 살짝 돌린 각 — 입체로 보이게
    private const float LoadTimeout = 15f;
    private const float StageLife = 40f;     // 이 시간이 지나면 무대를 무조건 치운다
    private const float StageGap = 40f;      // 다른 무대(프로필 모델 칸 등)가 찍히지 않게
    private static readonly Vector3 StagePos = new Vector3(0f, -3000f, 0f);
    private static readonly Quaternion Front = Quaternion.Euler(0f, 180f, 0f);   // 모델의 앞(+Z)이 카메라를 본다
    private static int seq;

    private Camera cam;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Mesh> baked = new List<Mesh>();

    /// <summary>마지막으로 못 찍은 까닭 (load · fit · empty) — 찍었으면 ""</summary>
    public static string LastFail { get; private set; } = "";
#if UNITY_EDITOR
    /// <summary>플레이 시험 — 마지막에 검은 바탕으로 찍은 그림 (빈 그림이어도)</summary>
    public static byte[] EditorLastRaw;
    /// <summary>플레이 시험 — 회색(감마 0.5) 판이 남긴 값 (감마로 남으면 128 · 선형이면 55 근처)</summary>
    public static int EditorProbe;
    /// <summary>플레이 시험 — 찍은 모델의 재질 색(_BaseColor) — 색만 입힌 재질은 AR 화면에 이 값 그대로 나온다</summary>
    public static readonly List<Color32> EditorBaseColors = new List<Color32>();
#endif

    /// <summary>GLB 바이트를 찍어 PNG(가로세로 side, 바탕 투명)로 돌려준다 — 못 찍으면 null</summary>
    public static IEnumerator Capture(byte[] glb, int side, Action<byte[]> done)
    {
        byte[] png = null;
        if (glb != null && glb.Length > 0)
        {
            int n = ++seq;
            var go = new GameObject("ModelSnapshotStage");
            go.transform.position = StagePos + Vector3.right * (StageGap * (n % 64));
            Destroy(go, StageLife);
            var snap = go.AddComponent<R0926ModelSnapshot>();
            bool finished = false;
            snap.StartCoroutine(snap.Shoot(glb, Mathf.Clamp(side, 64, 1024), "local://model-snapshot/" + n + ".glb", b => { png = b; finished = true; }));
            yield return new WaitUntil(() => finished || snap == null);
            if (snap != null) Destroy(snap.gameObject);
        }
        done?.Invoke(png);
    }

    private IEnumerator Shoot(byte[] glb, int side, string url, Action<byte[]> done)
    {
        var pivot = new GameObject("Pivot").transform;
        pivot.SetParent(transform, false);
        pivot.localRotation = Front * Quaternion.Euler(0f, Turn, 0f);
        var holder = new GameObject("Model");
        holder.transform.SetParent(pivot, false);
        holder.AddComponent<GLBSpawnEffect>().enableEffect = false;   // 불러오는 쪽이 이 연출을 찾아 쓴다 — 꺼 두면 바로 진하게
        var loader = holder.AddComponent<GLBModelLoader>();
        loader.displayOnly = true;
        GLBModelLoader.PreloadCache(url, glb);
        yield return null;   // 불러오기 칸(GLBContainer)은 GLBModelLoader.Start 가 만든다

        bool answered = false, ok = false;
        loader.StartCoroutine(loader.LoadGLBModelCoroutine(url, 1f, r =>
        {
            ok = r;
            answered = true;
            foreach (var rr in holder.GetComponentsInChildren<Renderer>(true)) rr.enabled = false;   // 다 불러온 그 프레임부터 AR 화면에 그리지 않는다
        }));
        float until = Time.realtimeSinceStartup + LoadTimeout;
        while (!answered && Time.realtimeSinceStartup < until) yield return null;
        GLBModelLoader.ClearCacheForUrl(url);   // 한 번 찍고 마는 모델이 캐시 자리를 차지하지 않게
        var model = ok && loader.HasLoadedModel() ? loader.GetLoadedModel() : null;
        if (model == null) { Fail("load", done); yield break; }
        Bake(model);
        if (R0926HuntChest.Fit(holder.transform, model.transform, 1f) == Vector3.zero) { Fail("fit", done); yield break; }
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (!(r is SkinnedMeshRenderer)) renderers.Add(r);
        Show(false);
        if (renderers.Count == 0) { Fail("fit", done); yield break; }
#if UNITY_EDITOR
        EditorBaseColors.Clear();
        foreach (var r in renderers)
            foreach (var m in r.sharedMaterials)
                if (m != null && m.HasProperty("_BaseColor"))
                {
                    Color32 c = m.GetColor("_BaseColor");
                    if (!EditorBaseColors.Exists(x => x.r == c.r && x.g == c.g && x.b == c.b)) EditorBaseColors.Add(c);
                }
#endif

        var cg = new GameObject("Camera");
        cg.transform.SetParent(transform, false);
        var look = Quaternion.Euler(Tilt, 0f, 0f);
        cg.transform.localRotation = look;
        cg.transform.localPosition = look * new Vector3(0f, 0f, -Distance);
        cam = cg.AddComponent<Camera>();
        cam.enabled = false;   // 매 프레임 찍지 않는다 — 아래에서 두 번만 직접 돌린다
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.fieldOfView = FieldOfView;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = Distance + 2f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.useOcclusionCulling = false;
        cam.stereoTargetEye = StereoTargetEyeMask.None;

        // 두 배로 찍어 반으로 줄인다 — 가장자리가 계단처럼 보이지 않게
        var big = new RenderTexture(side * 2, side * 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "ModelSnapshot" };
        big.Create();
        cam.targetTexture = big;
        bool storedLinear = StoresLinear(big, side);
        var onBlack = Grab(big, side, Color.black);
        var onWhite = Grab(big, side, Color.white);
        cam.targetTexture = null;
        big.Release();
        Destroy(big);
#if UNITY_EDITOR
        EditorLastRaw = onBlack.EncodeToPNG();
#endif
        byte[] png = Matte(onBlack, onWhite, storedLinear);
        Destroy(onBlack);
        Destroy(onWhite);
        if (png == null) { Fail("empty", done); yield break; }
        LastFail = "";
        done(png);
    }

    private static void Fail(string why, Action<byte[]> done)
    {
        LastFail = why;
        done(null);
    }

    // 스킨 메시 → 지금 자세의 고정 메시 (같은 자리 · 같은 재질). 원래 스킨 렌더러는 끈다
    private void Bake(GameObject model)
    {
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            var mesh = new Mesh { name = "ModelSnapshotBaked" };
            smr.BakeMesh(mesh, true);
            baked.Add(mesh);
            var go = new GameObject("Baked");
            go.transform.SetParent(smr.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
            smr.enabled = false;
        }
    }

    // 모델과 같은 재질(GLBFallbackUnlit · SetColor)로 감마 0.5 회색 판을 화면 가득 찍어 — 128 근처면 감마로 남는 길, 55 근처(선형 0.214)면 선형으로 남는 길.
    // 모델 픽셀과 같은 길(셰이더 · 줄이기 · 읽기)로 잰다
    private bool StoresLinear(RenderTexture big, int side)
    {
        var tpl = Resources.Load<Material>("GLBFallbackUnlit");
        if (tpl == null || QualitySettings.activeColorSpace != ColorSpace.Linear) return false;
        var mat = new Material(tpl);
        mat.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.5f, 1f));
        mat.SetFloat("_Cull", 0f);
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f) },
            triangles = new[] { 0, 2, 1, 2, 3, 1 },
        };
        var go = new GameObject("Probe");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var probe = Grab(big, side, Color.black, false);   // 판만 — 모델 렌더러는 꺼진 채
        int v = probe.GetPixelData<Color32>(0)[(side / 2) * side + side / 2].g;
        Destroy(probe);
        go.SetActive(false);   // 같은 프레임의 모델 사진에 섞이지 않게
        Destroy(go);
        Destroy(mesh);
        Destroy(mat);
#if UNITY_EDITOR
        EditorProbe = v;
#endif
        return v < 92;
    }

    // 바탕색 하나로 한 번 찍어 (side x side) 픽셀로 읽는다 — 렌더러는 이 안에서만 켜진다
    private Texture2D Grab(RenderTexture big, int side, Color bg, bool model = true)
    {
        cam.backgroundColor = bg;
        if (model) Show(true);
        try { cam.Render(); }
        finally { if (model) Show(false); }
        var small = RenderTexture.GetTemporary(side, side, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(big, small);
        var prev = RenderTexture.active;
        RenderTexture.active = small;
        var tex = new Texture2D(side, side, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, side, side), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(small);
        return tex;
    }

    // 검은 바탕 b · 흰 바탕 w → 투명도 a = 1 - (w - b), 색 = b / a (빛의 세기는 선형 공간에서 계산 · PNG 는 감마로 쓴다).
    // 모델이 거의 안 찍혔으면 null. 투명한 곳의 색은 흰색 — 투명을 모르는 곳(큐브 앞면 등)에선 흰 바탕으로 보인다
    private static byte[] Matte(Texture2D onBlack, Texture2D onWhite, bool storedLinear)
    {
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        var toLinear = new float[256];   // 그림에 남은 값 → 선형 세기
        var toPng = new byte[256];       // 그림에 남은 값 → PNG(감마) 값
        for (int i = 0; i < 256; i++)
        {
            float v = i / 255f;
            toLinear[i] = linear && !storedLinear ? Mathf.GammaToLinearSpace(v) : v;
            toPng[i] = storedLinear ? Encode(v, true) : (byte)i;
        }
        var b = onBlack.GetPixels32();
        var w = onWhite.GetPixels32();
        var o = new Color32[b.Length];
        int seen = 0;
        for (int i = 0; i < b.Length; i++)
        {
            Color32 pb = b[i], pw = w[i];
            if (pb.r == pw.r && pb.g == pw.g && pb.b == pw.b)
            {
                o[i] = new Color32(toPng[pb.r], toPng[pb.g], toPng[pb.b], 255);
                seen++;
                continue;
            }
            float lr = toLinear[pb.r], lg = toLinear[pb.g], lb = toLinear[pb.b];
            float a = 1f - ((toLinear[pw.r] - lr) + (toLinear[pw.g] - lg) + (toLinear[pw.b] - lb)) / 3f;
            if (a < 0.004f)
            {
                o[i] = new Color32(255, 255, 255, 0);
                continue;
            }
            if (a > 0.06f) seen++;
            o[i] = new Color32(Encode(lr / a, linear), Encode(lg / a, linear), Encode(lb / a, linear), (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
        }
        if (seen < b.Length / 500) return null;
        var tex = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, false);
        tex.SetPixels32(o);
        var png = tex.EncodeToPNG();
        Destroy(tex);
        return png;
    }

    private static byte Encode(float v, bool linear)
    {
        v = Mathf.Clamp01(v);
        if (linear) v = Mathf.LinearToGammaSpace(v);
        return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }

    private void Show(bool on)
    {
        foreach (var r in renderers) if (r != null) r.enabled = on;
    }

    private void OnDestroy()
    {
        foreach (var m in baked) if (m != null) Destroy(m);
        baked.Clear();
        if (cam != null && cam.targetTexture != null)
        {
            var t = cam.targetTexture;
            cam.targetTexture = null;
            t.Release();
            Destroy(t);
        }
    }
}
