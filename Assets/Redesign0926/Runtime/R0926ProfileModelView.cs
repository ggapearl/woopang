using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 프로필 3D 모델 칸 (10-25 대표님 「glb 파일 업로드한 경우 프로필에서는 해당 glb 파일이 보이고」) — 둥근 사진 위에 얹는다.
/// 보물 모양 미리보기(R0926HuntShapePreview)처럼 화면 밖 무대의 모델을 이 칸 전용 카메라로 찍어 RawImage 에 띄운다.
///  · 받는 동안·못 받으면 칸을 비워 둔다 — 밑의 사진이 보인다
///  · 무대의 렌더러는 이 카메라가 찍는 동안만 켠다 — AR 화면에는 안 보인다
///  · 칸이 꺼지면 모델을 내린다 (다시 켜지면 같은 모델을 다시 붙인다)
/// </summary>
public class R0926ProfileModelView : MonoBehaviour
{
    [SerializeField] private RawImage view;
    [SerializeField] private Color background = new Color(0.055f, 0.063f, 0.075f, 1f);
    [SerializeField] private float cameraDistance = 2.4f;   // 가장 긴 변이 1 인 모델이 둥근 칸에 들어오는 거리 (화각 30도)
    [SerializeField] private float swayDegrees = 30f;       // 정면을 보며 천천히 좌우로 — 입체로 보이게
    [SerializeField] private float swaySeconds = 8f;

    private const int MaxSide = 640;
    private const float FieldOfView = 30f;
    private const float StageGap = 40f;   // 카메라가 닿는 거리보다 멀리 — 다른 칸의 무대가 찍히지 않게
    private static readonly Vector3 StagePos = new Vector3(0f, -2000f, 0f);
    private static readonly Quaternion Front = Quaternion.Euler(0f, 180f, 0f);   // 모델의 앞(+Z)이 카메라를 본다
    private static int stageSeq;

    private GameObject stage;
    private Transform pivot;
    private Camera cam;
    private RenderTexture rt;
    private GameObject holder;
    private Coroutine loading;
    private string url = "";
    private readonly List<Renderer> renderers = new List<Renderer>();

    /// <summary>모델이 칸에 떠 있다</summary>
    public bool Showing { get; private set; }
    /// <summary>받는 중</summary>
    public bool Loading => loading != null;

    /// <summary>이 모델을 띄운다 — 빈 값이면 칸을 비운다. 앞서 못 받은 모델은 다시 받아 본다</summary>
    public void Show(string modelUrl)
    {
        modelUrl = modelUrl ?? "";
        if (modelUrl == url && (Showing || loading != null)) return;
        Clear();
        url = modelUrl;
        if (url.Length > 0 && isActiveAndEnabled) loading = StartCoroutine(Load(url));
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBegin;
        RenderPipelineManager.endCameraRendering += OnEnd;
        if (url.Length > 0 && !Showing && loading == null) loading = StartCoroutine(Load(url));
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBegin;
        RenderPipelineManager.endCameraRendering -= OnEnd;
        Clear();
    }

    private void OnDestroy()
    {
        if (stage != null) Destroy(stage);
    }

    private IEnumerator Load(string modelUrl)
    {
        Build();
        holder = new GameObject("Model");
        holder.transform.SetParent(pivot, false);
        var loader = holder.AddComponent<GLBModelLoader>();
        loader.displayOnly = true;
        yield return null;   // 불러오기 칸(GLBContainer)은 GLBModelLoader.Start 가 만든다
        bool ok = false;
        yield return loader.LoadGLBModelCoroutine(modelUrl, 1f, r => ok = r);
        loading = null;
        if (!ok || !loader.HasLoadedModel()) { Drop(); yield break; }
        R0926HuntChest.Fit(holder.transform, loader.GetLoadedModel().transform, 1f);
        Hide();
        Showing = true;
    }

    private void Clear()
    {
        if (loading != null) { StopCoroutine(loading); loading = null; }
        Drop();
    }

    private void Drop()
    {
        Showing = false;
        if (holder != null) Destroy(holder);
        holder = null;
        if (cam != null) { cam.enabled = false; cam.targetTexture = null; }
        if (view != null) { view.enabled = false; view.texture = null; }
        if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
    }

    private void Build()
    {
        if (stage != null) return;
        stage = new GameObject("ProfileModelStage");
        stage.transform.position = StagePos + Vector3.right * (StageGap * ++stageSeq);
        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(stage.transform, false);
        pivot.localRotation = Front;

        var cg = new GameObject("ProfileModelCamera");
        cg.transform.SetParent(stage.transform, false);
        cg.transform.localPosition = new Vector3(0f, 0f, -cameraDistance);
        cam = cg.AddComponent<Camera>();
        cam.enabled = false;   // 그릴 판(RenderTexture)이 생기기 전엔 화면에 그리지 않게
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.fieldOfView = FieldOfView;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = cameraDistance + 2f;
        cam.allowHDR = false;
        cam.useOcclusionCulling = false;
    }

    private void LateUpdate()
    {
        if (!Showing || cam == null || view == null) return;
        if (swaySeconds > 0f)
            pivot.localRotation = Front * Quaternion.Euler(0f, Mathf.Sin(Time.unscaledTime / swaySeconds * Mathf.PI * 2f) * swayDegrees, 0f);

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
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "ProfileModelView", antiAliasing = 4 };
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
        stage.GetComponentsInChildren(true, renderers);
        foreach (var r in renderers) if (r != null) r.enabled = true;
    }

    private void OnEnd(ScriptableRenderContext ctx, Camera c)
    {
        if (c == cam) Hide();
    }

    private void Hide()
    {
        if (stage == null) return;
        stage.GetComponentsInChildren(true, renderers);
        foreach (var r in renderers) if (r != null) r.enabled = false;
    }

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 칸이 찍은 그림</summary>
    public RenderTexture EditorTexture => rt;
#endif
}
