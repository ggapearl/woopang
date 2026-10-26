using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 축하 화면의 3D 보물 (10-26 대표님: 보물상자가 두근두근 떨리다 3~4초 뒤 짜잔 — 외부 말고 기기 GPU 로).
/// 찾은 그 보물(AR 에서 가져온 R0926HuntChest — 사진·3D 모델을 입은 그대로)을 화면 밖 무대에 두고, 전용 카메라로 투명 배경에 찍어 RawImage 에 띄운다.
///  · AR 보물이 없으면(상세에서 코드로 찾음 · 보이지 않던 보물) 같은 프리팹으로 새로 만들어 그 모양을 입힌다
///  · 흔들기·뚜껑·크기는 R0926HuntCelebrate 가 Pose · Lid 로 준다. 상자는 바닥 가운데를 축으로 기울어진다 (탁자 위에서 들썩이듯)
///  · 무대의 렌더러는 이 카메라가 찍는 동안만 켠다 — AR 화면에는 안 보인다 (R0926HuntShapePreview 와 같은 방식)
/// </summary>
public class R0926HuntReveal : MonoBehaviour
{
    [SerializeField] private GameObject treasurePrefab;   // HuntTreasure0926 — AR 보물이 없을 때
    [SerializeField] private RawImage view;               // 투명 배경 · 미리 곱한 알파 (R0926PremultipliedUI)

    private const int MaxSide = 1024;
    private const float Base = 0.5f;                      // 상자 원점에서 바닥까지 (1.2배 상자 · 몸통 아래 -0.42)
    private static readonly Vector3 StagePos = new Vector3(0f, -2600f, 0f);   // 모양 미리보기(-2000)와 떨어진 자리
    private static readonly Vector3 CamOffset = new Vector3(0f, 1.1f, -4.3f);   // 사람이 땅에 놓인 상자를 살짝 내려다보듯
    private static readonly Vector3 LookOffset = new Vector3(0f, 0.12f, 0f);

    private GameObject stage;
    private Transform pivot;
    private Camera cam;
    private RenderTexture rt;
    private bool hooked;
    private readonly List<Renderer> renderers = new List<Renderer>();

    /// <summary>무대의 보물 (보이는 동안만)</summary>
    public R0926HuntChest Chest { get; private set; }
    public RectTransform View => view != null ? view.rectTransform : null;

    /// <summary>보물을 무대에 올린다 — live 가 있으면 그것을(화살표·누르기 상자는 뗀다), 없으면 프리팹으로 새로 만들어 kind·url 을 입힌다</summary>
    public bool Begin(R0926HuntChest live, string kind, string url)
    {
        Build();
        Clear();
        if (stage == null || view == null) { if (live != null) Destroy(live.gameObject); return false; }
        stage.SetActive(true);
        var c = live;
        if (c != null)
        {
            foreach (var t in c.GetComponentsInChildren<Target>(true)) { t.enabled = false; Destroy(t); }   // 끄면서 박스·화살표가 내려간다
            foreach (var col in c.GetComponentsInChildren<Collider>(true)) Destroy(col);
            c.transform.SetParent(pivot, false);
        }
        else
        {
            var src = treasurePrefab != null ? treasurePrefab.transform.Find("Chest") : null;
            if (src == null) { Debug.LogError("[HuntReveal] 보물 프리팹에 Chest 없음"); return false; }
            pivot.gameObject.SetActive(false);   // 떼어 낼 것을 떼기 전에 깨지 않게
            var go = Instantiate(src.gameObject, pivot, false);
            go.name = "Chest";
            foreach (var t in go.GetComponentsInChildren<Target>(true)) DestroyImmediate(t);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);
            pivot.gameObject.SetActive(true);
            c = go.GetComponent<R0926HuntChest>();
            if (c == null) { Destroy(go); return false; }
            c.Dress(kind, url);
        }
        c.Hold();
        c.transform.localPosition = new Vector3(0f, Base, 0f);
        Chest = c;
        // 둘레 별빛 — 상자를 따라 떨지 않게 받침에 (AR 보물은 R0926HuntTreasure.Take 가 상자 아래로 옮겨 왔고, 새로 만든 보물은 Awake 가 받침에 만들었다)
        var tw = c.transform.Find("Twinkle");
        if (tw == null) tw = pivot.Find("Twinkle");
        if (tw != null)
        {
            tw.SetParent(pivot, false);
            tw.localPosition = new Vector3(0f, Base, 0f);
            tw.localRotation = Quaternion.identity;
            tw.localScale = Vector3.one;
        }
        foreach (var ps in stage.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;   // 렌더러를 꺼 두는 사이에도 반짝임이 이어지게
        }
        stage.GetComponentsInChildren(true, renderers);
        Hide();
        Pose(0f, 0f, 0f, Vector3.zero, Vector3.one);
        if (!hooked)
        {
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering += OnEnd;
            hooked = true;
        }
        Target();
        cam.enabled = true;
        view.enabled = true;
        return true;
    }

    /// <summary>yaw·pitch·roll(도)는 바닥 가운데를 축으로 · offset 은 미터 · scale 은 상자 크기 배율</summary>
    public void Pose(float yaw, float pitch, float roll, Vector3 offset, Vector3 scale)
    {
        if (pivot == null) return;
        pivot.localPosition = new Vector3(0f, -Base, 0f) + offset;
        pivot.localRotation = Quaternion.Euler(pitch, yaw, roll);
        pivot.localScale = scale;
    }

    public void Lid(float deg)
    {
        if (Chest != null) Chest.SetLid(deg);
    }

    /// <summary>상자 좌표의 점이 찍힌 자리 — view 의 로컬 좌표 (view 피벗 기준)</summary>
    public Vector2 ViewPoint(Vector3 chestLocal)
    {
        if (Chest == null || cam == null || view == null) return Vector2.zero;
        Vector3 vp = cam.WorldToViewportPoint(Chest.transform.TransformPoint(chestLocal));
        var r = view.rectTransform.rect;
        return new Vector2(r.x + vp.x * r.width, r.y + vp.y * r.height);
    }

    /// <summary>다 보였다 — 보물을 치우고 카메라를 멈춘다</summary>
    public void End()
    {
        if (hooked)
        {
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
            hooked = false;
        }
        if (cam != null) { cam.enabled = false; cam.targetTexture = null; }
        Clear();
        if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        if (view != null) { view.texture = null; view.enabled = false; }
        if (stage != null) stage.SetActive(false);
    }

    private void OnDisable()
    {
        End();
    }

    private void OnDestroy()
    {
        End();
        if (stage != null) Destroy(stage);
    }

    private void Clear()
    {
        Chest = null;
        renderers.Clear();
        if (pivot == null) return;
        for (int i = pivot.childCount - 1; i >= 0; i--) Destroy(pivot.GetChild(i).gameObject);
    }

    private void Build()
    {
        if (stage != null) return;
        stage = new GameObject("HuntRevealStage");
        stage.transform.position = StagePos;
        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(stage.transform, false);

        var cg = new GameObject("RevealCamera");
        cg.transform.SetParent(stage.transform, false);
        cam = cg.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // 투명 — 뒤의 어둠·빛줄기가 비친다
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 14f;
        cam.allowHDR = false;                               // HDR 판은 알파가 없다
        cam.useOcclusionCulling = false;
        cg.transform.position = StagePos + CamOffset;
        cg.transform.LookAt(StagePos + LookOffset);
    }

    // view 크기(화면 픽셀)에 맞춘 판 — 처음 프레임부터 ViewPoint 가 맞도록 Begin 에서 바로 만든다
    private void Target()
    {
        var canvas = view.canvas;
        float k = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        Vector2 px = view.rectTransform.rect.size * k;
        float fit = Mathf.Min(1f, MaxSide / Mathf.Max(1f, Mathf.Max(px.x, px.y)));
        int w = Mathf.Max(16, Mathf.RoundToInt(px.x * fit)), h = Mathf.Max(16, Mathf.RoundToInt(px.y * fit));
        if (rt == null || rt.width != w || rt.height != h)
        {
            cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "HuntReveal", antiAliasing = 4 };
            rt.Create();
        }
        else if (!rt.IsCreated()) rt.Create();
        cam.targetTexture = rt;
        cam.aspect = (float)w / h;
        view.texture = rt;
    }

    private void OnBegin(ScriptableRenderContext ctx, Camera c)
    {
        if (c != cam || stage == null) return;
        stage.GetComponentsInChildren(true, renderers);   // 받는 중인 사진·모델도
        foreach (var r in renderers) if (r != null) r.enabled = true;
    }

    private void OnEnd(ScriptableRenderContext ctx, Camera c)
    {
        if (c == cam) Hide();
    }

    private void Hide()
    {
        foreach (var r in renderers) if (r != null) r.enabled = false;
    }

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 찍은 판</summary>
    public RenderTexture EditorTexture => rt;
#endif
}
