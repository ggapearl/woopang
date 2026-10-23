using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 보물 모양 미리보기 (10-24 대표님 — 숨기기 전에 고른 모양·사진·3D 모델을 먼저 본다).
/// AR 보물과 같은 프리팹의 보물(R0926HuntChest)을 화면 밖 무대에 두고, 이 칸 전용 카메라로 찍어 RawImage 에 띄운다.
///  · 앵커는 쓰지 않는다 — 프리팹의 'Chest' 만 꺼내고 화살표(Target)·누르기 상자(Collider)는 뗀다
///  · 무대의 렌더러는 이 카메라가 찍는 동안만 켠다 — AR 화면에는 안 보인다
///  · 칸이 꺼지면 카메라를 멈추고 붙인 사진·모델을 내린다 (다시 켜지면 고른 대로 다시 붙인다)
/// </summary>
public class R0926HuntShapePreview : MonoBehaviour
{
    [SerializeField] private GameObject treasurePrefab;   // HuntTreasure0926
    [SerializeField] private RawImage view;
    [SerializeField] private Color background = new Color(0.055f, 0.063f, 0.075f, 1f);

    private const int MaxSide = 1024;
    private static readonly Vector3 StagePos = new Vector3(0f, -2000f, 0f);
    private static int modelSeq;

    private GameObject stage;
    private Camera cam;
    private RenderTexture rt;
    private readonly List<Renderer> renderers = new List<Renderer>();

    private string lookKind = "chest";
    private Texture lookPhoto;
    private byte[] lookModel;
    private string lookModelUrl;

    /// <summary>무대의 보물 (칸이 처음 켜지기 전엔 null)</summary>
    public R0926HuntChest Chest { get; private set; }
    /// <summary>3D 모델을 불러오는 중</summary>
    public bool Loading => Chest != null && Chest.Loading;
    /// <summary>고른 3D 모델을 열지 못해 보물상자로 돌아갔다</summary>
    public bool ModelFailed => lookKind == "model" && lookModel != null && Chest != null && stage != null && stage.activeSelf && !Chest.Loading && Chest.Kind != "model";

    /// <summary>코드로 만든 모양 (사진·3D 모델 칸은 빈 액자·보물상자로)</summary>
    public void Show(string kind)
    {
        if (lookKind == kind && lookPhoto == null && lookModel == null) return;
        lookKind = kind; lookPhoto = null; lookModel = null; lookModelUrl = null;
        Apply();
    }

    public void ShowPhoto(Texture tex)
    {
        if (lookKind == "photo" && lookPhoto == tex) return;
        lookKind = "photo"; lookPhoto = tex; lookModel = null; lookModelUrl = null;
        Apply();
    }

    /// <summary>고른 GLB — 내려받지 않게 GLBModelLoader 캐시에 넣고 붙인다</summary>
    public void ShowModel(byte[] glb)
    {
        if (lookKind == "model" && ReferenceEquals(lookModel, glb)) return;
        lookKind = "model"; lookPhoto = null; lookModel = glb;
        lookModelUrl = "local://hunt-preview/" + (++modelSeq) + ".glb";
        Apply();
    }

    private void Apply()
    {
        if (Chest == null || stage == null || !stage.activeSelf) return;   // 칸이 켜질 때 고른 대로 붙인다
        if (lookKind == "photo")
        {
            if (Chest.Wear("photo") && lookPhoto != null) Chest.ShowPhoto(lookPhoto);
        }
        else if (lookKind == "model" && lookModel != null)
        {
            GLBModelLoader.PreloadCache(lookModelUrl, lookModel);   // 다른 모델들이 캐시에서 밀어냈을 수 있다
            if (Chest.Wear("model")) Chest.ShowModel(lookModelUrl);
        }
        else if (lookKind == "model" || !Chest.Wear(lookKind)) Chest.Wear("chest");   // 3D 모델을 아직 안 골랐으면 — 못 열 때 보이는 보물상자
        stage.GetComponentsInChildren(true, renderers);
        Hide();
    }

    private void OnEnable()
    {
        Build();
        if (stage == null) return;
        stage.SetActive(true);
        Apply();
        RenderPipelineManager.beginCameraRendering += OnBegin;
        RenderPipelineManager.endCameraRendering += OnEnd;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBegin;
        RenderPipelineManager.endCameraRendering -= OnEnd;
        if (cam != null) cam.enabled = false;
        if (Chest != null) Chest.Wear("chest");   // 붙인 사진·모델을 내린다 (모델은 메모리를 많이 쓴다)
        if (stage != null) stage.SetActive(false);
    }

    private void OnDestroy()
    {
        if (stage != null) Destroy(stage);
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    private void Build()
    {
        if (stage != null || treasurePrefab == null) return;
        var src = treasurePrefab.transform.Find("Chest");
        if (src == null) { Debug.LogError("[HuntShapePreview] 보물 프리팹에 Chest 없음"); return; }
        stage = new GameObject("HuntShapePreviewStage");
        stage.SetActive(false);   // 떼어 낼 것을 떼기 전에 깨지 않게
        stage.transform.position = StagePos;
        var go = Instantiate(src.gameObject, stage.transform, false);
        go.name = "Chest";
        foreach (var t in go.GetComponentsInChildren<Target>(true)) DestroyImmediate(t);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        Chest = go.GetComponent<R0926HuntChest>();

        var cg = new GameObject("PreviewCamera");
        cg.transform.SetParent(stage.transform, false);
        cam = cg.AddComponent<Camera>();
        cam.enabled = false;   // 그릴 판(RenderTexture)이 생기기 전엔 화면에 그리지 않게
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 12f;
        cam.allowHDR = false;
        cam.useOcclusionCulling = false;
        // 사람이 AR 보물을 내려다보는 높이에서 — 별빛까지 칸에 들어오게
        Vector3 c0 = go.transform.position;
        cg.transform.position = c0 + new Vector3(0f, 0.62f, -3.3f);
        cg.transform.LookAt(c0 + new Vector3(0f, 0.1f, 0f));

        stage.SetActive(true);   // 보물이 깨며 둘레 별빛(Twinkle)을 무대에 만든다
        foreach (var ps in stage.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;   // 렌더러를 꺼 두는 사이에도 반짝임이 이어지게
        }
        stage.GetComponentsInChildren(true, renderers);
        Hide();
    }

    private void LateUpdate()
    {
        if (cam == null || view == null) return;
        var canvas = view.canvas;
        float k = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        Vector2 px = view.rectTransform.rect.size * k;
        float fit = Mathf.Min(1f, MaxSide / Mathf.Max(1f, Mathf.Max(px.x, px.y)));
        int w = Mathf.RoundToInt(px.x * fit), h = Mathf.RoundToInt(px.y * fit);
        if (w < 16 || h < 16) return;   // 칸 크기가 아직 안 정해졌다
        if (rt == null || Mathf.Abs(rt.width - w) > 4 || Mathf.Abs(rt.height - h) > 4)
        {
            cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "HuntShapePreview", antiAliasing = 4 };
            rt.Create();
            cam.targetTexture = rt;
            view.texture = rt;
        }
        else if (!rt.IsCreated()) rt.Create();
        view.enabled = true;
        cam.enabled = true;
    }

    private void OnBegin(ScriptableRenderContext ctx, Camera c)
    {
        if (c != cam || stage == null) return;
        stage.GetComponentsInChildren(true, renderers);   // 받는 중인 모델·로딩 고리도
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
    /// <summary>플레이 시험 — 칸이 찍은 그림</summary>
    public RenderTexture EditorTexture => rt;
#endif
}
