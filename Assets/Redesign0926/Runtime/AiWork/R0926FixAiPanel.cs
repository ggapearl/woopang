using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장소 정보 수정 · 장소수정 요청 ▸ 로고 칸 「AI 작업」 (11-18 I1 대표님 「정보수정 요청, 장소정보 수정(다른 사람 요청 및 본인 요청, 우팡M 포함)
/// 다들 각각 투명하게 만드는 거랑 AI 이용한 것들에 대한 옵션이 해당 요청에 있어서도 할 수 있도록」).
/// 장소 추가의 AI 작업(R0926AiWorkPanel)과 같은 네 칸 — 01 투명하게 · 02 만화톤 · 03 색연필 · 04 3D 캐릭터. 02~04 는 하나만, 01 은 02·03 과만.
/// 누르면 수정 카드가 그 자리에서 AI 화면이 된다: ‹ · 안내 한 줄 · 큰 미리보기 · 네 칸 · (04) 요청사항 + 만들기 · 맨 아래 취소 | 확인.
/// 고르는 즉시 「작업 중..」 → 미리보기에 바로 (투명한 곳은 바둑판). 캐릭터는 미리보기 칸에서 천천히 좌우로 돈다 (R0926ProfileModelView).
/// ‹ · 뒤로가기 · 확인 = 로고 칸에 넣는다 (CubeDataFixManager.ApplyAi — 투명하면 PNG, 캐릭터면 정지 사진 + 3D 모델로 저장 · 요청). 취소 = 그대로.
/// 넣기 전 사진은 매니저가 지킨다 — 다시 열어 다 끄고 확인하면 원래 사진으로. 무료 · 폰 안에서 (외부 AI 는 R0926AiWork.Provider 만 바꾼다).
/// 수정 카드(FixUploadPage)에 붙는다 — Redesign0926ApplyFixAi 가 만들고 연결.
/// </summary>
public class R0926FixAiPanel : MonoBehaviour
{
    [SerializeField] private CubeDataFixManager manager;
    [Tooltip("로고 칸 아래쪽 「AI 작업」")]
    [SerializeField] private Button aiButton;
    [SerializeField] private Image aiButtonBg;
    [SerializeField] private Image aiButtonIcon;
    [Tooltip("AI 화면 (FixAi0926) — 수정 카드를 꽉 채운다")]
    [SerializeField] private RectTransform view;
    [Tooltip("‹ — 안드로이드 뒤로가기도 이것 (확인과 같다)")]
    [SerializeField] private Button back;
    [SerializeField] private Text status;
    [Tooltip("큰 미리보기 칸 — 머리줄과 네 칸 사이를 채운다 (안의 정사각형이 사진 · 캐릭터)")]
    [SerializeField] private RectTransform stage;
    [SerializeField] private RawImage checker;
    [SerializeField] private RawImage photo;
    [SerializeField] private R0926ProfileModelView model;
    [Tooltip("01 투명하게 · 02 만화톤 · 03 색연필 · 04 3D 캐릭터")]
    [SerializeField] private RectTransform tiles;
    [SerializeField] private Button[] chips = new Button[4];
    [SerializeField] private Image[] chipBgs = new Image[4];
    [SerializeField] private Image[] chipIcons = new Image[4];
    [SerializeField] private Text[] chipLabels = new Text[4];
    [Tooltip("04 를 고르면 — 요청사항 칸 + 만들기")]
    [SerializeField] private RectTransform ask;
    [SerializeField] private InputField request;
    [SerializeField] private Button make;
    [Tooltip("맨 아래 취소 | 확인")]
    [SerializeField] private RectTransform foot;
    [SerializeField] private Button viewCancel;
    [SerializeField] private Button viewConfirm;
    [Tooltip("요청사항은 키보드 위 입력줄로 쓴다 — 키보드 '보내기' 도 만들기")]
    [SerializeField] private UploadInputMirror mirror;
    [SerializeField] private float pad = 24f;
    [SerializeField] private float headH = 100f;
    [SerializeField] private float gap = 24f;
    [Tooltip("작업이 이보다 오래 걸리면 접는다 (초)")]
    [SerializeField] private float timeout = 25f;

    private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f, 1f);
    private static readonly Color Ink = new Color(0.953f, 0.961f, 0.965f, 1f);
    private static readonly Color ChipOff = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color AiOff = new Color(0.04f, 0.045f, 0.05f, 0.72f);   // 사진 위에 얹혀도 글이 보이게 — 어두운 유리
    private static readonly Color Dim = new Color(1f, 1f, 1f, 0.38f);              // 사진이 없을 때 01~03

    private bool open, clear, working, applyWhenDone;
    private R0926AiWork.Style style;
    private int gen, variant;
    private string lastRequest = "";
    private float workStart;
    private Coroutine work;

    // 미리보기에 만든 것 — 아직 로고 칸에 넣지 않았다 (넣으면 텍스처는 매니저 것)
    private R0926AiWork.Result made;
    private bool madeNone;   // 다 껐다 — 확인하면 원래 사진으로

    // 로고 칸에 넣은 것의 옵션 — 다시 열면 그대로 골라져 있다
    private bool keptClear;
    private R0926AiWork.Style keptStyle;
    private int keptVariant;
    private string keptRequest = "";

    private string modelUrl;
    private static int seq;
    private Texture2D checkerTex;

    // 요청사항을 키보드 위 입력줄로 쓰는 동안 — 요청사항 · 네 칸을 올린 높이 (보기 아래 끝부터 · 보기 좌표). 입력줄이 닫히면 부드럽게 0 으로
    private float kbCover, kbWant;
    private int kbFrame = -1;
    private readonly Vector3[] corners = new Vector3[4];
    private const float KbGap = 28f, KbSpeed = 12f, MinStage = 200f;

    public bool Open => open;
    public bool Working => working;
    public bool Clear => clear;
    public R0926AiWork.Style Style => style;
    public R0926AiWork.Result Made => made;

    private void Awake()
    {
        if (aiButton != null) aiButton.onClick.AddListener(OpenView);
        if (back != null) back.onClick.AddListener(Keep);
        if (viewConfirm != null) viewConfirm.onClick.AddListener(Keep);
        if (viewCancel != null) viewCancel.onClick.AddListener(Cancel);
        for (int i = 0; i < chips.Length; i++)
        {
            int k = i;
            if (chips[i] != null) chips[i].onClick.AddListener(() => Pick(k));
        }
        if (make != null) make.onClick.AddListener(Make);
        if (checker != null)
        {
            // 투명한 곳 — 바둑판 (2x2 를 반복해 칸마다 8 줄)
            checkerTex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "FixAiChecker", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var a = new Color32(52, 55, 60, 255);
            var b = new Color32(70, 73, 79, 255);
            checkerTex.SetPixels32(new[] { a, b, b, a });
            checkerTex.Apply(false, true);
            checker.texture = checkerTex;
            checker.uvRect = new Rect(0f, 0f, 8f, 8f);
        }
    }

    private void OnEnable()
    {
        if (mirror != null && request != null) mirror.SetSubmitAction(request, Make);
        if (manager != null) manager.AiChanged += Paint;
        ShowView(false);
        Paint();
    }

    private void OnDisable()
    {
        if (manager != null) manager.AiChanged -= Paint;
        // 수정 카드가 닫혔다 — 만들던 것 · 넣지 않은 것은 버린다 (로고 칸에 넣은 것은 매니저가 정리한다)
        StopWork();
        applyWhenDone = false;
        Drop(ref made);
        madeNone = false;
        ShowView(false);
    }

    private void OnDestroy()
    {
        if (checkerTex != null) Destroy(checkerTex);
        ForgetModel();
    }

    private void Update()
    {
        if (aiButton != null)
        {
            bool show = manager != null && !manager.Busy;   // 사진을 불러오는 동안(빈 칸 + 원호) · 보내는 동안은 숨긴다
            if (aiButton.gameObject.activeSelf != show) aiButton.gameObject.SetActive(show);
        }
        if (!working) return;
        if (Time.realtimeSinceStartup - workStart > timeout)
        {
            Debug.LogError("[R0926FixAiPanel] 작업 시간 초과");
            Fail();
            return;
        }
        if (status != null && open)
        {
            int n = 1 + (int)(Time.unscaledTime * 3f) % 3;   // 작업 중. → .. → ... (자리는 그대로)
            status.text = R0926LocalizedText.Say("작업 중", "Working", "作業中", "处理中", "Trabajando")
                + new string('.', n) + "<color=#00000000>" + new string('.', 3 - n) + "</color>";
        }
    }

    // 'AI 작업' — 수정 카드가 그 자리에서 AI 화면으로. 로고 칸에 넣어 둔 것이 있으면 그 옵션이 골라진 채로
    private void OpenView()
    {
        if (open || manager == null || manager.Busy) return;
        if (!manager.AiOn)
        {
            keptClear = false; keptStyle = R0926AiWork.Style.None; keptVariant = 0; keptRequest = "";
        }
        clear = keptClear;
        style = keptStyle;
        variant = keptVariant;
        lastRequest = keptRequest;
        if (request != null) request.text = keptRequest;
        Drop(ref made);
        madeNone = false;
        applyWhenDone = false;
        ShowView(true);
        ShowCurrent();
        if (status != null && !working)
            status.text = manager.AiSource == null && !manager.AiOn
                ? R0926LocalizedText.Say("로고 사진이 없어요 — 3D 캐릭터는 사진 없이도 만들어요", "No logo photo — a 3D character needs none",
                    "ロゴ写真がありません — 3Dキャラは写真なしで作れます", "没有标志照片 — 3D角色不需要照片", "Sin foto de logo — el personaje 3D no la necesita")
                : R0926LocalizedText.Say("원하는 효과를 고르세요 — 고르면 바로 바뀌어요", "Pick an effect — it changes right away",
                    "効果を選んでください — すぐに変わります", "选择效果 — 立即生效", "Elige un efecto — cambia al instante");
        Paint();
    }

    // ‹ · 뒤로 · 확인 — 만든 것을 로고 칸에 넣고 닫는다. 아직 만드는 중이면 다 만든 뒤에 넣는다
    private void Keep()
    {
        if (!open) return;
        if (working) applyWhenDone = true;
        else Apply();
        CloseView();
    }

    // 취소 — 로고 칸은 그대로
    private void Cancel()
    {
        if (!open) return;
        StopWork();
        applyWhenDone = false;
        Drop(ref made);
        madeNone = false;
        CloseView();
    }

    private void Apply()
    {
        if (manager == null) return;
        if (made != null)
        {
            if (manager.ApplyAi(made.Tex, made.Alpha, made.Style == R0926AiWork.Style.Character ? made.Glb : null))
            {
                made.Tex = null;   // 이제 매니저 것
                keptClear = clear; keptStyle = style; keptVariant = variant; keptRequest = lastRequest;
            }
            Drop(ref made);   // 보내는 중이라 못 넣었으면 버린다
        }
        else if (madeNone)
        {
            manager.ClearAi();
            keptClear = false; keptStyle = R0926AiWork.Style.None; keptVariant = 0; keptRequest = "";
        }
        madeNone = false;
        Paint();
    }

    private void CloseView()
    {
        if (request != null && request.isFocused) request.DeactivateInputField();
        ShowView(false);
        Paint();
    }

    private void ShowView(bool on)
    {
        open = on;
        if (view == null) return;
        if (view.gameObject.activeSelf != on) view.gameObject.SetActive(on);
        if (on)
        {
            view.SetAsLastSibling();
            Layout();
        }
        else
        {
            kbCover = kbWant = 0f;
            if (model != null) model.Show("");
            ForgetModel();
        }
    }

    private void LateUpdate()
    {
        if (open) Layout();
    }

    // 맨 아래부터 취소|확인 · (04) 요청사항 · 네 칸 — 남은 위쪽을 미리보기가 채운다.
    // 11-24 대표님 「키패드가 올라와서 타이핑이 어떻게 입력되는 지 안보이거든」 — 요청사항을 키보드 위 입력줄로 쓰는 동안은
    // 요청사항 · 네 칸을 입력줄 위로 올린다 (취소|확인은 키보드 뒤에 둔다 · 미리보기가 그만큼 줄어든다)
    private void Layout()
    {
        if (!open) return;
        if (kbFrame != Time.frameCount)
        {
            kbFrame = Time.frameCount;
            kbWant = KeyboardCover();
            kbCover = Mathf.Abs(kbWant - kbCover) < 1f ? kbWant : Mathf.Lerp(kbCover, kbWant, Mathf.Clamp01(Time.unscaledDeltaTime * KbSpeed));
        }
        float y0 = Stack(foot, pad);
        bool askOn = ask != null && ask.gameObject.activeSelf;
        float y = askOn ? Stack(ask, Mathf.Max(y0, kbCover)) : y0;
        Stack(tiles, y);
        if (stage != null && view != null)
        {
            // 미리보기는 다 올라갔을 때의 크기로 한 번에 — 움직이는 동안 캐릭터 그림을 매번 다시 만들지 않게. 아주 작은 화면에서도 칸은 남긴다
            float cut = pad + headH + gap * 0.5f;
            float end = Next(tiles, askOn ? Next(ask, Mathf.Max(y0, kbWant)) : y0);
            var min = new Vector2(stage.offsetMin.x, Mathf.Min(end, Mathf.Max(y0, view.rect.height - cut - MinStage)));
            var max = new Vector2(stage.offsetMax.x, -cut);
            if (stage.offsetMin != min) stage.offsetMin = min;
            if (stage.offsetMax != max) stage.offsetMax = max;
        }
    }

    // 요청사항을 키보드 위 입력줄로 쓰는 동안 — 입력줄 윗선 + 간격 (보기 아래 끝부터 · 보기 좌표). 아니면 0
    private float KeyboardCover()
    {
        var bar = mirror != null && request != null && mirror.ActiveSource == request ? mirror.PlacedBar : null;
        if (bar == null || view == null) return 0f;
        bar.GetWorldCorners(corners);
        return Mathf.Max(0f, view.InverseTransformPoint(corners[1]).y - view.rect.yMin + KbGap);
    }

    private float Stack(RectTransform rt, float y)
    {
        if (rt == null) return y;
        var p = new Vector2(rt.anchoredPosition.x, y);
        if (rt.anchoredPosition != p) rt.anchoredPosition = p;
        return Next(rt, y);
    }

    private float Next(RectTransform rt, float y) => rt == null ? y : y + rt.sizeDelta.y + gap;

    private void Pick(int i)
    {
        if (!open || manager == null) return;
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
        // 01~03 은 사진을 손본다 — 로고 사진이 없으면 고르지 않는다
        if (manager.AiSource == null && (clear || style == R0926AiWork.Style.Toon || style == R0926AiWork.Style.Pencil))
        {
            clear = false;
            if (style != R0926AiWork.Style.Character) style = R0926AiWork.Style.None;
            if (status != null)
                status.text = R0926LocalizedText.Say("먼저 로고 사진을 넣어 주세요 — 3D 캐릭터는 사진 없이도 돼요", "Add a logo photo first — a 3D character needs none",
                    "先にロゴ写真を入れてください — 3Dキャラは写真なしでもOK", "请先添加标志照片 — 3D角色不需要照片", "Primero añade una foto de logo — el personaje 3D no la necesita");
            Paint();
            return;
        }
        Paint();
        Run();
    }

    /// <summary>요청사항으로 다시 만든다 — 같은 요청이면 다른 모습 · 비었으면 새 무작위 캐릭터</summary>
    private void Make()
    {
        if (!open || style != R0926AiWork.Style.Character) return;
        string req = RequestText();
        variant = req == lastRequest ? variant + 1 : 0;
        lastRequest = req;
        Run();
    }

    private string RequestText() => request != null ? request.text.Trim() : "";

    private void Run()
    {
        StopWork();
        gen++;
        work = StartCoroutine(Work(gen, clear, style, lastRequest, variant));
    }

    private void StopWork()
    {
        gen++;
        if (work != null) StopCoroutine(work);
        work = null;
        working = false;
    }

    private IEnumerator Work(int g, bool c, R0926AiWork.Style s, string req, int v)
    {
        if (!c && s == R0926AiWork.Style.None)   // 다 껐다 — 원래 사진으로
        {
            working = false;
            Drop(ref made);
            madeNone = true;
            ShowImage(manager.AiSource);
            if (status != null)
                status.text = R0926LocalizedText.Say("원래 사진으로 돌렸어요", "Back to the original photo", "元の写真に戻しました", "已恢复原图", "Volvió a la foto original");
            Paint();
            work = null;
            yield break;
        }

        working = true;
        workStart = Time.realtimeSinceStartup;
        Paint();

        // 요청사항이 비었으면 정말 무작위 (11-14)
        if (s == R0926AiWork.Style.Character && string.IsNullOrWhiteSpace(req)) req = R0926AiCharacter.RandomRequest();

        R0926AiWork.Result r = null;
        var src = manager.AiSource;
        if (src != null) yield return R0926AiWork.Run(src, c, s, req, v, x => r = x);
        else yield return R0926AiWork.Character(req, v, x => r = x);   // 사진 없이 캐릭터만 (Pick 이 01~03 을 막았다)
        if (g != gen) { Drop(ref r); yield break; }
        if (r == null || (s == R0926AiWork.Style.Character && r.Glb == null)) { Drop(ref r); Fail(); yield break; }

        if (s == R0926AiWork.Style.Character)
        {
            // 로고 = 캐릭터 정지 사진 (3D 장소는 목록 오른쪽에 모델 모습 — 11-13). 못 찍으면 캐릭터가 든 사진으로
            byte[] png = null;
            yield return R0926ModelSnapshot.Capture(r.Glb, 512, b => png = b);
            if (g != gen) { Drop(ref r); yield break; }
            if (png != null)
            {
                var still = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "FixAiCharacterStill", wrapMode = TextureWrapMode.Clamp };
                if (still.LoadImage(png)) { if (r.Tex != null) Destroy(r.Tex); r.Tex = still; r.Alpha = true; }
                else Destroy(still);
            }
            if (r.Tex == null) { Drop(ref r); Fail(); yield break; }
        }

        Drop(ref made);
        made = r;
        madeNone = false;
        working = false;
        work = null;
        if (open) ShowResult(r);
        if (status != null) status.text = Done(r);
        if (applyWhenDone)
        {
            applyWhenDone = false;
            Apply();
        }
        Paint();
    }

    // 못 만들었다 — 고른 것을 로고 칸에 든 그대로 되돌린다 (보이는 것과 고른 칸이 어긋나지 않게)
    private void Fail()
    {
        StopWork();
        applyWhenDone = false;
        Drop(ref made);
        madeNone = false;
        clear = keptClear; style = keptStyle; variant = keptVariant; lastRequest = keptRequest;
        if (open) ShowCurrent();
        if (status != null)
            status.text = R0926LocalizedText.Say("이번엔 만들지 못했어요 — 다시 골라 주세요", "Couldn't make it this time — try again",
                "今回は作れませんでした — もう一度選んでください", "这次没能完成 — 请重新选择", "Esta vez no se pudo — elige otra vez");
        Paint();
    }

    // 로고 칸에 든 그대로 — AI 를 넣어 둔 캐릭터면 모델, 아니면 사진
    private void ShowCurrent()
    {
        if (manager == null) return;
        if (manager.AiGlb != null) ShowModel(manager.AiGlb);
        else ShowImage(manager.MainPhoto);
    }

    private void ShowResult(R0926AiWork.Result r)
    {
        if (r.Style == R0926AiWork.Style.Character && r.Glb != null) ShowModel(r.Glb);
        else ShowImage(r.Tex);
    }

    private void ShowImage(Texture tex)
    {
        if (model != null) model.Show("");
        ForgetModel();
        if (photo != null)
        {
            photo.texture = tex;
            photo.enabled = tex != null;
        }
        if (checker != null) checker.enabled = true;   // 투명한 곳 · 사진이 없을 때 빈 칸
    }

    private void ShowModel(byte[] glb)
    {
        if (photo != null) { photo.texture = null; photo.enabled = false; }
        if (checker != null) checker.enabled = false;
        if (model == null || glb == null) return;
        string url = "local://fix-ai/" + (++seq) + ".glb";
        GLBModelLoader.PreloadCache(url, glb);
        model.Show(url);
        ForgetModel();   // 바로 앞 모델의 캐시
        modelUrl = url;
    }

    private void ForgetModel()
    {
        if (modelUrl == null) return;
        GLBModelLoader.ClearCacheForUrl(modelUrl);
        modelUrl = null;
    }

    private static void Drop(ref R0926AiWork.Result r)
    {
        if (r != null && r.Tex != null) Destroy(r.Tex);
        r = null;
    }

    private static string Done(R0926AiWork.Result r)
    {
        if (r.Style == R0926AiWork.Style.Character)
            return R0926LocalizedText.Say("3D 캐릭터" + (string.IsNullOrEmpty(r.Label) ? "" : " · " + r.Label) + " — 확인을 누르면 넣어요",
                "3D character — tap OK to use it", "3Dキャラ — 確認で入れます", "3D角色 — 点确认使用", "Personaje 3D — pulsa Aceptar");
        var parts = new System.Collections.Generic.List<string>();
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

    private void Paint()
    {
        bool askOn = open && style == R0926AiWork.Style.Character;
        if (ask != null && ask.gameObject.activeSelf != askOn) ask.gameObject.SetActive(askOn);
        bool lit = manager != null && manager.AiOn || applyWhenDone;   // 로고 칸에 AI 가 들어 있다 (또는 다 만들면 들어간다)
        if (aiButtonBg != null) aiButtonBg.color = lit ? Pink : AiOff;
        if (aiButtonIcon != null) aiButtonIcon.color = lit ? Ink : Pink;
        bool hasPhoto = manager != null && manager.AiSource != null;
        for (int i = 0; i < 4; i++)
        {
            bool on = i == 0 ? clear : (int)style == i;
            bool dim = i < 3 && !hasPhoto;
            if (i < chipBgs.Length && chipBgs[i] != null) chipBgs[i].color = on ? Pink : ChipOff;
            if (i < chipIcons.Length && chipIcons[i] != null) chipIcons[i].color = dim ? Dim : Ink;
            if (i < chipLabels.Length && chipLabels[i] != null)
            {
                chipLabels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                chipLabels[i].color = dim ? Dim : Ink;
            }
        }
        if (make != null) make.interactable = style == R0926AiWork.Style.Character;
        Layout();
    }
}
