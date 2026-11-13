using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장소 추가 ▸ 「이곳에 오브젝트를 추가하시겠습니까?」 ▸ 가운데 「AI 작업」 (11-12 대표님).
/// 누르면 카드 아래에 옵션 네 칸 — 01 투명하게(겹쳐 고를 수 있다) · 02 만화톤 · 03 색연필 · 04 3D 캐릭터(요청사항) — 02~04 는 하나만.
/// 고르는 즉시 「작업 중..」 → 돌고 있는 미리보기 큐브에 바로 입힌다. 캐릭터는 큐브 자리에 서서 큐브와 같이 돈다(큐브는 숨긴다).
/// 「추가하기」를 누르면 지금 보이는 그대로 CubeUploadManager 가 가져간다(Take) — 투명하면 PNG, 캐릭터면 3D 모델 길로. 취소면 버린다(Discard).
/// 무료 · 폰 안에서 (R0926AiWork — 나중에 외부 AI 는 Provider 만 바꾼다). ARPreviewPanel 에 붙는다 — Redesign0926ApplyAiWork 가 만들고 연결.
/// </summary>
public class R0926AiWorkPanel : MonoBehaviour
{
    [SerializeField] private ARPreviewController preview;
    [SerializeField] private Button aiButton;
    [SerializeField] private Image aiButtonBg;
    [SerializeField] private Image aiButtonIcon;
    [SerializeField] private Text aiButtonLabel;
    [SerializeField] private Button confirmButton;
    [Tooltip("옵션 판 (AiOptions0926) — 카드 아래 빈 곳에 붙어 아래로 자란다 (아래가 모자라면 카드 위)")]
    [SerializeField] private RectTransform options;
    [SerializeField] private Text status;
    [Tooltip("01 투명하게 · 02 만화톤 · 03 색연필 · 04 3D 캐릭터")]
    [SerializeField] private Button[] chips = new Button[4];
    [SerializeField] private Image[] chipBgs = new Image[4];
    [SerializeField] private Image[] chipIcons = new Image[4];
    [SerializeField] private Text[] chipLabels = new Text[4];
    [Tooltip("04 를 고르면 — 요청사항 칸 + 만들기")]
    [SerializeField] private GameObject ask;
    [SerializeField] private InputField request;
    [SerializeField] private Button make;
    [Tooltip("요청사항은 키보드 위 입력줄로 쓴다 — 키보드 '보내기' 도 만들기")]
    [SerializeField] private UploadInputMirror mirror;
    [SerializeField] private float optionsHeight = 352f;
    [SerializeField] private float askHeight = 134f;
    [Tooltip("캐릭터 키 = 큐브 한 변 x 이 배")]
    [SerializeField] private float modelScale = 1.1f;
    [Tooltip("작업이 이보다 오래 걸리면 접고 원래 사진으로 (초)")]
    [SerializeField] private float timeout = 25f;

    private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f, 1f);
    private static readonly Color Ink = new Color(0.953f, 0.961f, 0.965f, 1f);
    private static readonly Color ChipOff = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color AiOff = new Color(0.914f, 0.325f, 0.514f, 0.2f);
    private const float Gap = 24f;   // 카드와 옵션 판 사이

    private bool open, clear, working, below = true, placed;
    private R0926AiWork.Style style;
    private int gen, variant;
    private string lastRequest = "";
    private float workStart;
    private Coroutine work;
    private Transform modelPivot;     // 캐릭터 — 큐브의 자식이라 큐브와 같이 돌고 같이 사라진다
    private string modelUrl;
    private MeshRenderer[] hiddenCube;

    // 지금 미리보기에 입힌 결과 — 패널이 꺼진 뒤 '추가하기' 콜백에서 가져가므로 패널 밖(정적)에 둔다
    private static R0926AiWork.Result held;
    private static int seq;

    /// <summary>미리보기에 입힌 AI 결과를 넘긴다 (없으면 null — 원래 사진 그대로). 받은 쪽이 텍스처 주인이 된다</summary>
    public static R0926AiWork.Result Take()
    {
        var r = held;
        held = null;
        return r;
    }

    /// <summary>미리보기 취소 — 만든 결과를 버린다</summary>
    public static void Discard()
    {
        if (held != null && held.Tex != null) Destroy(held.Tex);
        held = null;
    }

    public bool Open => open;
    public bool Working => working;
    public bool Clear => clear;
    public R0926AiWork.Style Style => style;
    public Transform Model => modelPivot;

    private void Awake()
    {
        if (aiButton != null) aiButton.onClick.AddListener(ToggleOptions);
        for (int i = 0; i < chips.Length; i++)
        {
            int k = i;
            if (chips[i] != null) chips[i].onClick.AddListener(() => Pick(k));
        }
        if (make != null) make.onClick.AddListener(Make);
    }

    private void OnEnable()
    {
        Discard();   // 지난 미리보기에서 남은 것
        ResetState();
        if (mirror != null && request != null) mirror.SetSubmitAction(request, Make);
    }

    private void OnDisable()
    {
        // 취소 · 추가하기 — 큐브는 컨트롤러가 이미 치웠다. 결과(held)는 남겨 둔다 (추가하기 콜백이 이 뒤에 가져간다)
        ResetState();
    }

    private void ResetState()
    {
        gen++;
        if (work != null) StopCoroutine(work);
        work = null;
        working = false;
        RemoveModel();
        open = clear = false;
        style = R0926AiWork.Style.None;
        variant = 0;
        lastRequest = "";
        if (request != null) request.text = "";
        if (status != null) status.text = "";
        if (confirmButton != null) confirmButton.interactable = true;
        Paint();
    }

    private void Update()
    {
        bool ready = preview != null && preview.CubeReady;
        if (aiButton != null && aiButton.interactable != ready) aiButton.interactable = ready;
        if (!working) return;
        if (Time.realtimeSinceStartup - workStart > timeout)
        {
            Debug.LogError("[R0926AiWorkPanel] 작업 시간 초과 — 원래 사진으로");
            Fail();
            return;
        }
        if (status != null)
        {
            int n = 1 + (int)(Time.unscaledTime * 3f) % 3;   // 작업 중. → .. → ... (자리는 그대로)
            status.text = R0926LocalizedText.Say("작업 중", "Working", "作業中", "处理中", "Trabajando")
                + new string('.', n) + "<color=#00000000>" + new string('.', 3 - n) + "</color>";
        }
    }

    // 캐릭터를 세운 동안 — 큐브가 거리에 따라 렌더러를 다시 켜도(DistanceBasedVisibility) 큐브는 숨긴 채로
    private void LateUpdate()
    {
        if (open) Place();
        if (modelPivot == null || hiddenCube == null) return;
        foreach (var r in hiddenCube) if (r != null && r.enabled) r.enabled = false;
    }

    // 옵션 판 자리 — 카드 아래 빈 곳 (위에 두면 올려다본 큐브의 아랫부분을 가린다). 가로 화면처럼 아래가 모자랄 때만 카드 위
    private void Place()
    {
        var card = options != null ? options.parent as RectTransform : null;
        var canvas = card != null ? card.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return;
        var root = (RectTransform)canvas.rootCanvas.transform;
        var c = new Vector3[4];
        card.GetWorldCorners(c);
        float cardBottom = root.InverseTransformPoint(c[0]).y;
        root.GetWorldCorners(c);
        float floor = root.InverseTransformPoint(c[0]).y + Screen.safeArea.yMin / Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor);
        bool fits = cardBottom - floor >= options.sizeDelta.y + Gap * 2f;
        if (placed && fits == below) return;
        placed = true;
        below = fits;
        options.anchorMin = new Vector2(0f, below ? 0f : 1f);
        options.anchorMax = new Vector2(1f, below ? 0f : 1f);
        options.pivot = new Vector2(0.5f, below ? 1f : 0f);
        options.anchoredPosition = new Vector2(0f, below ? -Gap : Gap);
    }

    private void ToggleOptions()
    {
        if (!open && (preview == null || !preview.CubeReady)) return;
        open = !open;
        if (open && !working && held == null && status != null)
            status.text = R0926LocalizedText.Say("원하는 효과를 고르세요 — 고르면 바로 바뀌어요", "Pick an effect — it changes right away",
                "効果を選んでください — すぐに変わります", "选择效果 — 立即生效", "Elige un efecto — cambia al instante");
        Paint();
    }

    private void Pick(int i)
    {
        if (preview == null || !preview.CubeReady) return;
        if (i == 0) clear = !clear;
        else
        {
            var s = (R0926AiWork.Style)i;   // 1 만화톤 · 2 색연필 · 3 3D 캐릭터 — 하나만
            style = style == s ? R0926AiWork.Style.None : s;
            if (style == R0926AiWork.Style.Character && s == R0926AiWork.Style.Character)
            {
                lastRequest = RequestText();
                variant = 0;
            }
        }
        Paint();
        Run();
    }

    /// <summary>요청사항으로 다시 만든다 — 같은 요청이면 다른 모습으로</summary>
    private void Make()
    {
        if (style != R0926AiWork.Style.Character || preview == null || !preview.CubeReady) return;
        string req = RequestText();
        variant = req == lastRequest ? variant + 1 : 0;
        lastRequest = req;
        Run();
    }

    private string RequestText() => request != null ? request.text.Trim() : "";

    private void Run()
    {
        gen++;
        if (work != null) StopCoroutine(work);
        work = StartCoroutine(Work(gen, clear, style, lastRequest, variant));
    }

    private IEnumerator Work(int g, bool c, R0926AiWork.Style s, string req, int v)
    {
        if (!c && s == R0926AiWork.Style.None)   // 다 껐다 — 원래 사진으로
        {
            working = false;
            Restore();
            if (status != null)
                status.text = R0926LocalizedText.Say("원래 사진으로 돌렸어요", "Back to the original photo", "元の写真に戻しました", "已恢复原图", "Volvió a la foto original");
            Paint();
            work = null;
            yield break;
        }

        working = true;
        workStart = Time.realtimeSinceStartup;
        Paint();

        R0926AiWork.Result r = null;
        yield return R0926AiWork.Run(preview.Photo, c, s, req, v, x => r = x);
        if (g != gen) { if (r != null && r.Tex != null) Destroy(r.Tex); yield break; }
        if (r == null) { Fail(); yield break; }

        if (s == R0926AiWork.Style.Character)
        {
            bool ok = false;
            yield return ShowModel(r.Glb, x => ok = x);
            if (g != gen) { Destroy(r.Tex); yield break; }
            if (!ok) { Destroy(r.Tex); Fail(); yield break; }
            // 대표 사진 = 캐릭터 정지 사진 (3D 장소는 목록 오른쪽에 모델 모습 — 11-13). 못 찍으면 캐릭터가 든 사진으로
            byte[] png = null;
            yield return R0926ModelSnapshot.Capture(r.Glb, 512, b => png = b);
            if (g != gen) { Destroy(r.Tex); yield break; }
            if (png != null)
            {
                var still = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "AiCharacterStill", wrapMode = TextureWrapMode.Clamp };
                if (still.LoadImage(png)) { Destroy(r.Tex); r.Tex = still; r.Alpha = true; }
                else Destroy(still);
            }
        }
        else
        {
            RemoveModel();
            preview.SetPreviewTexture(r.Tex);
        }

        var old = held;
        held = r;
        if (old != null && old.Tex != null && old.Tex != r.Tex) Destroy(old.Tex);
        working = false;
        work = null;
        if (status != null) status.text = Done(r);
        Paint();
    }

    // 못 만들었다 — 고른 것을 풀고 원래 사진으로 (보이는 것과 고른 칸이 어긋나지 않게)
    private void Fail()
    {
        gen++;
        if (work != null) StopCoroutine(work);
        work = null;
        working = false;
        Restore();
        clear = false;
        style = R0926AiWork.Style.None;
        if (status != null)
            status.text = R0926LocalizedText.Say("이번엔 만들지 못했어요 — 다시 골라 주세요", "Couldn't make it this time — try again",
                "今回は作れませんでした — もう一度選んでください", "这次没能完成 — 请重新选择", "Esta vez no se pudo — elige otra vez");
        Paint();
    }

    private void Restore()
    {
        RemoveModel();
        if (preview != null) preview.SetPreviewTexture(preview.Photo);
        Discard();
    }

    private static string Done(R0926AiWork.Result r)
    {
        if (r.Style == R0926AiWork.Style.Character)
            return R0926LocalizedText.Say("3D 캐릭터" + (string.IsNullOrEmpty(r.Label) ? "" : " · " + r.Label), "3D character", "3Dキャラ", "3D角色", "Personaje 3D");
        var parts = new List<string>();
        if (r.Style == R0926AiWork.Style.Toon) parts.Add(R0926LocalizedText.Say("만화톤", "Cartoon", "マンガ風", "漫画风", "Cómic"));
        if (r.Style == R0926AiWork.Style.Pencil) parts.Add(R0926LocalizedText.Say("색연필", "Colored pencil", "色鉛筆", "彩铅", "Lápices de color"));
        switch (r.Clear)
        {
            case R0926AiImage.ClearMode.Logo:
                parts.Add(R0926LocalizedText.Say("로고 모양대로 투명하게", "Transparent around the logo", "ロゴの形で透明に", "沿标志轮廓透明", "Transparente según el logo")); break;
            case R0926AiImage.ClearMode.Photo:
                parts.Add(R0926LocalizedText.Say("동그랗게 투명하게", "Round cutout, transparent", "丸く切り抜いて透明に", "圆形裁切 · 透明", "Recorte redondo transparente")); break;
            case R0926AiImage.ClearMode.Alpha:
                parts.Add(R0926LocalizedText.Say("투명한 모양 그대로", "Kept its transparent shape", "透明な形のまま", "保留透明形状", "Conserva su forma transparente")); break;
        }
        return string.Join(" · ", parts);
    }

    // 큐브 자리 · 크기에 캐릭터를 세운다 — 큐브의 자식이라 큐브의 자동 회전 · 끌어 돌리기 · 두근두근을 그대로 받는다
    private IEnumerator ShowModel(byte[] glb, System.Action<bool> done)
    {
        RemoveModel();
        var cube = preview != null ? preview.PreviewCube : null;
        var cubeRenderers = preview != null ? preview.CubeRenderers : null;
        if (cube == null || glb == null || glb.Length == 0) { done(false); yield break; }

        var cb = LocalBounds(cube.transform, cubeRenderers);
        var pivot = new GameObject("AiCharacter0926").transform;
        pivot.SetParent(cube.transform, false);
        pivot.localPosition = cb.center;
        var cam = Camera.main;
        if (cam != null)
        {
            var to = cam.transform.position - cube.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f) pivot.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);   // 앞(+Z)이 나를 보고 선다
        }
        modelPivot = pivot;
        var holder = new GameObject("Model");
        holder.transform.SetParent(pivot, false);
        var loader = holder.AddComponent<GLBModelLoader>();
        loader.displayOnly = true;
        modelUrl = "local://ai-character/" + (++seq) + ".glb";
        GLBModelLoader.PreloadCache(modelUrl, glb);
        yield return null;   // 불러오기 칸(GLBContainer)은 GLBModelLoader.Start 가 만든다

        bool answered = false, ok = false;
        if (loader != null)
            loader.StartCoroutine(loader.LoadGLBModelCoroutine(modelUrl, 1f, x =>
            {
                ok = x;
                answered = true;
                if (holder != null) foreach (var rr in holder.GetComponentsInChildren<Renderer>(true)) rr.enabled = false;   // 크기를 맞춘 뒤에 보인다
            }));
        float until = Time.realtimeSinceStartup + timeout;
        while (!answered && loader != null && Time.realtimeSinceStartup < until) yield return null;
        if (modelPivot != pivot) { done(false); yield break; }
        if (modelUrl != null) { GLBModelLoader.ClearCacheForUrl(modelUrl); modelUrl = null; }
        if (!ok || loader == null || !loader.HasLoadedModel() || cube == null) { RemoveModel(); done(false); yield break; }

        float side = Mathf.Max(cb.size.x, Mathf.Max(cb.size.y, cb.size.z));
        if (R0926HuntChest.Fit(holder.transform, loader.GetLoadedModel().transform, side * modelScale) == Vector3.zero) { RemoveModel(); done(false); yield break; }
        foreach (var rr in holder.GetComponentsInChildren<Renderer>(true)) rr.enabled = true;
        hiddenCube = cubeRenderers;
        if (hiddenCube != null) foreach (var r in hiddenCube) if (r != null) r.enabled = false;
        done(true);
    }

    private void RemoveModel()
    {
        if (hiddenCube != null) foreach (var r in hiddenCube) if (r != null) r.enabled = true;
        hiddenCube = null;
        if (modelPivot != null)
        {
            modelPivot.gameObject.SetActive(false);   // 지우는 건 프레임 끝 — 그 사이 큐브 렌더러를 찾는 쪽에 걸리지 않게
            Destroy(modelPivot.gameObject);
        }
        modelPivot = null;
        if (modelUrl != null) { GLBModelLoader.ClearCacheForUrl(modelUrl); modelUrl = null; }
    }

    // 큐브 렌더러들의 경계 (큐브 좌표) — 두근두근으로 커져도 큐브 좌표라 그대로
    private static Bounds LocalBounds(Transform root, MeshRenderer[] renderers)
    {
        bool any = false;
        var b = new Bounds(Vector3.zero, Vector3.one);
        if (renderers != null)
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var lb = r.localBounds;
                if (lb.extents == Vector3.zero) continue;
                var m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                    if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
                }
            }
        if (!any || b.size.sqrMagnitude < 1e-6f) b = new Bounds(Vector3.zero, Vector3.one);
        return b;
    }

    private void Paint()
    {
        bool askOn = open && style == R0926AiWork.Style.Character;
        if (options != null)
        {
            if (options.gameObject.activeSelf != open) options.gameObject.SetActive(open);
            options.sizeDelta = new Vector2(options.sizeDelta.x, optionsHeight + (askOn ? askHeight : 0f));
            if (open) Place();
        }
        if (ask != null && ask.activeSelf != askOn) ask.SetActive(askOn);
        if (aiButtonBg != null) aiButtonBg.color = open ? Pink : AiOff;
        if (aiButtonIcon != null) aiButtonIcon.color = open ? Ink : Pink;
        if (aiButtonLabel != null) aiButtonLabel.color = Ink;
        for (int i = 0; i < 4; i++)
        {
            bool on = i == 0 ? clear : (int)style == i;
            if (i < chipBgs.Length && chipBgs[i] != null) chipBgs[i].color = on ? Pink : ChipOff;
            if (i < chipIcons.Length && chipIcons[i] != null) chipIcons[i].color = Ink;
            if (i < chipLabels.Length && chipLabels[i] != null) chipLabels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
        }
        if (confirmButton != null) confirmButton.interactable = !working;
        if (make != null) make.interactable = !working || style == R0926AiWork.Style.Character;
    }
}
