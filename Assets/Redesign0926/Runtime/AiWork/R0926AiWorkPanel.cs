using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장소 추가 ▸ 「이곳에 오브젝트를 추가하시겠습니까?」 ▸ 가운데 「AI 작업」 (11-12 대표님).
/// 11-14 대표님 「'AI 작업' 을 누르면 여기 창 내에서 작업을 하도록 … 뒤로가기 누르면 다시 '취소 / AI작업 / 추가하기' 창이 발생 … 이 버튼 하단에도 확인취소」 —
/// 누르면 그 카드가 그 자리에서 AI 화면이 된다: ‹ 뒤로 · 안내 한 줄 · 네 칸 · (04) 요청사항 + 만들기 · 맨 아래 취소 | 확인.
/// 카드는 윗선을 그대로 두고 아래로 자란다(올려다본 큐브를 가리지 않게) — 도크 윗선 위까지만 (예전엔 카드 아래에 판이 따로 붙어 도크 아이콘을 덮었다).
/// ‹ · 안드로이드 뒤로 · 확인 = 만든 것을 두고 세 버튼으로 (AI 작업 버튼이 분홍으로 남는다) · 취소 = 원래 사진으로 돌리고 세 버튼으로.
/// 네 칸 — 01 투명하게 · 02 만화톤 · 03 색연필 · 04 3D 캐릭터. 02~04 는 하나만, 01 은 02·03 과만 겹친다 (11-13).
/// 고르는 즉시 「작업 중..」 → 돌고 있는 미리보기 큐브에 바로 입힌다. 캐릭터는 큐브 자리에 서서 큐브와 같이 돈다(큐브는 숨긴다).
/// 04 — 요청사항을 비우면 정말 무작위 캐릭터(R0926AiCharacter.RandomRequest). 3초 안에 옆으로 두 바퀴 넘게 빠르게 돌리면 다른 캐릭터로,
/// 천천히 돌리는 건 구경이라 그대로 (ARPreviewController.FastSpin · 11-14).
/// 「추가하기」를 누르면 지금 보이는 그대로 CubeUploadManager 가 가져간다(Take) — 투명하면 PNG, 캐릭터면 3D 모델 길로. 미리보기 취소면 버린다(Discard).
/// 무료 · 폰 안에서 (R0926AiWork — 외부 AI 는 Provider 만 바꾼다). ARPreviewPanel 에 붙는다 — Redesign0926ApplyAiWork 가 만들고 연결.
/// </summary>
public class R0926AiWorkPanel : MonoBehaviour
{
    [SerializeField] private ARPreviewController preview;
    [SerializeField] private Button aiButton;
    [SerializeField] private Image aiButtonBg;
    [SerializeField] private Image aiButtonIcon;
    [SerializeField] private Text aiButtonLabel;
    [SerializeField] private Button confirmButton;
    [Tooltip("미리보기 카드 (MessageContainer) — AI 화면이 이 안에서 열린다")]
    [SerializeField] private RectTransform card;
    [Tooltip("카드의 원래 글 · 세 버튼 줄 — AI 화면 동안 숨긴다")]
    [SerializeField] private GameObject cardText;
    [SerializeField] private GameObject cardButtons;
    [Tooltip("AI 화면 (AiOptions0926) — 카드를 꽉 채운다")]
    [SerializeField] private RectTransform options;
    [Tooltip("‹ — 안드로이드 뒤로가기도 이것")]
    [SerializeField] private Button back;
    [Tooltip("맨 아래 취소 | 확인")]
    [SerializeField] private Button viewCancel;
    [SerializeField] private Button viewConfirm;
    [SerializeField] private Text status;
    [Tooltip("01 투명하게 · 02 만화톤 · 03 색연필 · 04 3D 캐릭터")]
    [SerializeField] private Button[] chips = new Button[4];
    [SerializeField] private Image[] chipBgs = new Image[4];
    [SerializeField] private Image[] chipIcons = new Image[4];
    [SerializeField] private Text[] chipLabels = new Text[4];
    [Tooltip("04 를 고르면 — 요청사항 칸 + 만들기")]
    [SerializeField] private GameObject ask;
    [Tooltip("04 를 고르면 — '빠르게 두 바퀴 돌리면 다른 캐릭터' 한 줄")]
    [SerializeField] private GameObject hint;
    [SerializeField] private InputField request;
    [SerializeField] private Button make;
    [Tooltip("요청사항은 키보드 위 입력줄로 쓴다 — 키보드 '보내기' 도 만들기")]
    [SerializeField] private UploadInputMirror mirror;
    [Tooltip("AI 화면일 때 카드 높이 (04 를 고르지 않았을 때)")]
    [SerializeField] private float optionsHeight = 566f;
    [Tooltip("04 를 고르면 늘어나는 높이 — 요청사항 줄 + 안내 한 줄")]
    [SerializeField] private float askHeight = 210f;
    [Tooltip("카드 아래 끝과 도크 윗선 사이 (캔버스 단위)")]
    [SerializeField] private float dockGap = 24f;
    [Tooltip("캐릭터 키 = 큐브 한 변 x 이 배")]
    [SerializeField] private float modelScale = 1.1f;
    [Tooltip("작업이 이보다 오래 걸리면 접고 원래 사진으로 (초)")]
    [SerializeField] private float timeout = 25f;

    private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f, 1f);
    private static readonly Color Ink = new Color(0.953f, 0.961f, 0.965f, 1f);
    private static readonly Color ChipOff = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color AiOff = new Color(0.914f, 0.325f, 0.514f, 0.2f);

    private bool open, clear, working;
    private R0926AiWork.Style style;
    private int gen, variant;
    private string lastRequest = "";
    private float workStart;
    private Coroutine work;
    private Transform modelPivot;     // 캐릭터 — 큐브의 자식이라 큐브와 같이 돌고 같이 사라진다
    private string modelUrl;
    private MeshRenderer[] hiddenCube;

    // 카드 원래 자리 — AI 화면을 닫으면 그대로 되돌린다
    private bool cardSaved;
    private Vector2 cardMin, cardMax, cardPivot, cardPos, cardSize;
    private Canvas rootCanvas;
    private RectTransform dock;
    private float nextDockLook;
    private readonly Vector3[] corners = new Vector3[4];

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
        if (aiButton != null) aiButton.onClick.AddListener(OpenView);
        if (back != null) back.onClick.AddListener(() => CloseView(true));
        if (viewConfirm != null) viewConfirm.onClick.AddListener(() => CloseView(true));
        if (viewCancel != null) viewCancel.onClick.AddListener(() => CloseView(false));
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
        if (preview != null) preview.FastSpin += OnFastSpin;
    }

    private void OnDisable()
    {
        if (preview != null) preview.FastSpin -= OnFastSpin;
        // 취소 · 추가하기 — 큐브는 컨트롤러가 이미 치웠다. 결과(held)는 남겨 둔다 (추가하기 콜백이 이 뒤에 가져간다). 카드는 원래 모양으로
        ResetState();
    }

    private void ResetState()
    {
        gen++;
        if (work != null) StopCoroutine(work);
        work = null;
        working = false;
        RemoveModel();
        ShowView(false);
        clear = false;
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
        if (open) Layout();
        if (modelPivot == null || hiddenCube == null) return;
        foreach (var r in hiddenCube) if (r != null && r.enabled) r.enabled = false;
    }

    // 'AI 작업' — 카드가 그 자리에서 AI 화면으로
    private void OpenView()
    {
        if (open || preview == null || !preview.CubeReady) return;
        ShowView(true);
        if (!working && held == null && status != null)
            status.text = R0926LocalizedText.Say("원하는 효과를 고르세요 — 고르면 바로 바뀌어요", "Pick an effect — it changes right away",
                "効果を選んでください — すぐに変わります", "选择效果 — 立即生效", "Elige un efecto — cambia al instante");
        Paint();
    }

    // ‹ · 뒤로 · 확인 = 만든 것을 두고 · 취소 = 원래 사진으로 — 둘 다 세 버튼으로 돌아간다 (만드는 중에 확인이면 다 만든 뒤 큐브에 입혀진다)
    private void CloseView(bool keep)
    {
        if (!open) return;
        if (!keep)
        {
            gen++;
            if (work != null) StopCoroutine(work);
            work = null;
            working = false;
            Restore();
            clear = false;
            style = R0926AiWork.Style.None;
            variant = 0;
            lastRequest = "";
            if (request != null) request.text = "";
            if (status != null) status.text = "";
        }
        if (request != null && request.isFocused) request.DeactivateInputField();
        ShowView(false);
        Paint();
    }

    private void ShowView(bool on)
    {
        open = on;
        if (on && card != null && !cardSaved)
        {
            cardSaved = true;
            cardMin = card.anchorMin; cardMax = card.anchorMax; cardPivot = card.pivot;
            cardPos = card.anchoredPosition; cardSize = card.sizeDelta;
        }
        if (cardText != null && cardText.activeSelf == on) cardText.SetActive(!on);
        if (cardButtons != null && cardButtons.activeSelf == on) cardButtons.SetActive(!on);
        if (options != null && options.gameObject.activeSelf != on) options.gameObject.SetActive(on);
        if (on) Layout();
        else if (cardSaved)
        {
            cardSaved = false;
            if (card != null)
            {
                card.anchorMin = cardMin; card.anchorMax = cardMax; card.pivot = cardPivot;
                card.sizeDelta = cardSize; card.anchoredPosition = cardPos;
            }
        }
    }

    // 카드 — 윗선은 원래 카드 그대로 두고 높이만큼 아래로 (큐브를 가리지 않게). 아래 끝이 도크에 닿으면 그만큼 위로
    private void Layout()
    {
        if (card == null || !cardSaved) return;
        float h = optionsHeight + (style == R0926AiWork.Style.Character ? askHeight : 0f);
        float top = cardPos.y + cardSize.y * (1f - cardPivot.y);   // 원래 카드 윗선 — anchorMax.y 줄에서
        var min = new Vector2(cardMin.x, cardMax.y);
        var piv = new Vector2(cardPivot.x, 1f);
        var size = new Vector2(cardSize.x, h);
        if (card.anchorMin != min) card.anchorMin = min;
        if (card.anchorMax != cardMax) card.anchorMax = cardMax;
        if (card.pivot != piv) card.pivot = piv;
        if (card.sizeDelta != size) card.sizeDelta = size;
        var pos = new Vector2(cardPos.x, top + Lift(top, h));
        if ((card.anchoredPosition - pos).sqrMagnitude > 0.01f) card.anchoredPosition = pos;
    }

    // 도크 윗선(가로 화면 · 도크가 없으면 화면 아래 안전선) + 간격보다 카드 아래 끝이 낮으면 — 모자란 만큼 (부모 좌표)
    private float Lift(float top, float h)
    {
        var parent = card.parent as RectTransform;
        if (rootCanvas == null) { var c = card.GetComponentInParent<Canvas>(); rootCanvas = c != null ? c.rootCanvas : null; }
        if (parent == null || rootCanvas == null) return 0f;
        var root = (RectTransform)rootCanvas.transform;
        if (dock == null && Time.unscaledTime >= nextDockLook)
        {
            nextDockLook = Time.unscaledTime + 1f;
            var d = GameObject.Find("Dock0926");
            if (d != null) dock = d.transform as RectTransform;
        }
        Rect pr = parent.rect;
        float bottomLocal = pr.yMin + pr.height * cardMax.y + top - h;
        float bottom = root.InverseTransformPoint(parent.TransformPoint(new Vector3(0f, bottomLocal, 0f))).y;
        root.GetWorldCorners(corners);
        float floor = root.InverseTransformPoint(corners[0]).y + Screen.safeArea.yMin / Mathf.Max(0.01f, rootCanvas.scaleFactor);
        if (dock != null && dock.gameObject.activeInHierarchy)
        {
            dock.GetWorldCorners(corners);
            Vector3 a = root.InverseTransformPoint(corners[0]), b = root.InverseTransformPoint(corners[2]);
            if (Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y)) floor = Mathf.Max(floor, Mathf.Max(a.y, b.y));   // 세로 — 아래 막대의 윗선 (가로는 오른쪽 막대라 상관없다)
        }
        float need = floor + dockGap - bottom;
        if (need <= 0f) return 0f;
        return parent.lossyScale.y > 1e-6f ? need * root.lossyScale.y / parent.lossyScale.y : need;
    }

    private void Pick(int i)
    {
        if (preview == null || !preview.CubeReady) return;
        if (i == 0)
        {
            clear = !clear;
            if (clear && style == R0926AiWork.Style.Character) style = R0926AiWork.Style.None;   // 01 을 고르면 캐릭터는 내린다
        }
        else
        {
            var s = (R0926AiWork.Style)i;   // 1 만화톤 · 2 색연필 · 3 3D 캐릭터 — 하나만
            style = style == s ? R0926AiWork.Style.None : s;
            if (style == R0926AiWork.Style.Character)
            {
                clear = false;   // 캐릭터는 투명하게가 필요 없다 — 01 은 02·03 과만
                lastRequest = RequestText();
                variant = 0;
            }
        }
        Paint();
        Run();
    }

    /// <summary>요청사항으로 다시 만든다 — 같은 요청이면 다른 모습 · 비었으면 새 무작위 캐릭터</summary>
    private void Make()
    {
        if (style != R0926AiWork.Style.Character || preview == null || !preview.CubeReady) return;
        string req = RequestText();
        variant = req == lastRequest ? variant + 1 : 0;
        lastRequest = req;
        Run();
    }

    // 11-14 대표님 「몇초 이내에 2바퀴이상 옆으로 돌리면 (스와이프를 많이 하면) 해당 사항은 다른 캐릭터로 바꿔달라는 요청으로 이해하고 바꾸도록 … 천천히 돌리는 건 그냥 관찰하기 위한거니까 그냥 두고」
    private void OnFastSpin()
    {
        if (!open || style != R0926AiWork.Style.Character || working || preview == null || !preview.CubeReady) return;
        preview.Settle();   // 알아들었다 — 돌던 것을 멈추고 새 캐릭터를 세운다
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect(0.6f);
        Make();
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

        // 요청사항이 비었으면 정말 무작위 — 사진 · 차례로 정하던 때는 매번 같은 차례로 나왔다 (11-14)
        if (s == R0926AiWork.Style.Character && string.IsNullOrWhiteSpace(req)) req = R0926AiCharacter.RandomRequest();

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
        if (ask != null && ask.activeSelf != askOn) ask.SetActive(askOn);
        if (hint != null && hint.activeSelf != askOn) hint.SetActive(askOn);
        bool lit = held != null || working;   // 만든 것이 있다 — 세 버튼 줄로 돌아와도 AI 작업이 켜져 있는 게 보이게
        if (aiButtonBg != null) aiButtonBg.color = lit ? Pink : AiOff;
        if (aiButtonIcon != null) aiButtonIcon.color = lit ? Ink : Pink;
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
        if (open) Layout();
    }
}
