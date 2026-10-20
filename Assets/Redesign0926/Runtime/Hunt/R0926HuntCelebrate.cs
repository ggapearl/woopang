using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보물찾기 — 보물을 찾았을 때 축하 화면 (10-14 대표님: 더블터치로 열면 '보물을 찾았습니다' 와 함께 축하 애니메이션이 살짝,
/// 그 게임의 상품 안내가 그림과 함께).
///  · 선물 상자가 튀어나와 들썩이다 뚜껑이 날아가고 — 빛줄기가 돌고 꽃가루가 잠깐 터진다
///  · '보물을 찾았습니다!' · 몇 번째 · 상품(선물 그림) · 주최자에게 '찾았다'는 메세지가 갔다는 안내 (메세지는 서버가 찾은 사람 이름으로 보낸다)
///  · '확인'(안드로이드 뒤로가기도)을 누르면 그 게임 상세로 — 결과 · 주최자에게 한마디
/// 이 컴포넌트는 늘 켜진 빈 틀(HuntCelebrate0926)에 있고 보이는 것은 panel 아래 — Apply(Redesign0926ApplyHuntTreasure)가 만든다.
/// </summary>
public class R0926HuntCelebrate : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform content;        // 가로 화면에선 통째로 줄인다
    [SerializeField] private RectTransform rays, rays2, glow;
    [SerializeField] private RectTransform giftBox, giftLid;
    [SerializeField] private RectTransform stage;          // 상자 자리 — 꽃가루·별빛도 여기에
    [SerializeField] private CanvasGroup info;             // 글·상품·버튼 — 뚜껑이 열린 뒤 떠오른다
    [SerializeField] private Text title, rankText, prizeLabel, prizeText, noteText, okLabel;
    [SerializeField] private Button okButton;
    [SerializeField] private Sprite confettiRound;         // 동그란 꽃가루 (비어 있으면 네모만)
    [SerializeField] private Sprite glintSprite;

    private const float NeedHeight = 1900f;                // content 가 다 들어가는 높이 (캔버스 단위)
    private static readonly Color Gold = new Color(0.910f, 0.749f, 0.310f, 1f);
    private static readonly Color[] Paper =
    {
        new Color(0.910f, 0.749f, 0.310f), new Color(1f, 0.702f, 0.278f), new Color(0.914f, 0.325f, 0.514f),
        new Color(1f, 1f, 1f), new Color(0.44f, 0.85f, 0.72f), new Color(0.45f, 0.70f, 1f),
    };
    private static readonly Vector2[] GlintSpots =
        { new Vector2(-330f, 170f), new Vector2(300f, 250f), new Vector2(-250f, -60f), new Vector2(340f, -40f), new Vector2(40f, 380f) };

    private static R0926HuntCelebrate instance;

    private class Piece { public RectTransform rt; public Image img; public Vector2 v; public float spin, flip, age, life; }
    private readonly List<Piece> pieces = new List<Piece>();
    private RectTransform[] glints;
    private Image[] glintImgs;
    private Vector2 lidRest;
    private bool opened;
    private int huntId;
    private string huntTitle, prize, hostName;
    private int rank;
    private Coroutine playCo, fetchCo;

    private static string T(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

    public static bool Showing => instance != null && instance.panel != null && instance.panel.activeSelf;

    /// <summary>찾음 — 축하 화면을 띄운다. prize 가 null 이면(앱을 다시 켜 아직 모름) 서버에서 받아 채운다. 띄울 화면이 없으면 false</summary>
    public static bool Show(int id, string title, int rank, string prize, string hostName)
    {
        if (instance == null || instance.panel == null) return false;
        R0926HuntTreasure.HideToast();   // 남아 있던 '조금만 더 가까이' 같은 알약이 축하 화면 뒤로 비치지 않게
        instance.Begin(id, title, rank, prize, hostName);
        return true;
    }

    private void Awake()
    {
        instance = this;
        if (giftLid != null) lidRest = giftLid.anchoredPosition;
        if (okButton != null) okButton.onClick.AddListener(Close);
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Begin(int id, string t, int r, string p, string host)
    {
        huntId = id; huntTitle = t; rank = r; prize = p; hostName = host;
        transform.SetAsLastSibling();
        panel.SetActive(true);
        Paint();
        if (playCo != null) StopCoroutine(playCo);
        playCo = StartCoroutine(Play());
        if (fetchCo != null) StopCoroutine(fetchCo);
        fetchCo = prize == null && R0926HuntApi.CanCall ? StartCoroutine(Fetch(id)) : null;
    }

    private void Paint()
    {
        if (title != null) title.text = T("보물을 찾았습니다!", "You found the treasure!", "宝を見つけました！", "你找到宝藏了！", "¡Encontraste el tesoro!");
        if (rankText != null)
        {
            string r = rank == 1 ? T("가장 먼저 찾았어요", "You were the first!", "一番乗りです！", "你是第一个找到的！", "¡Lo encontraste primero!")
                     : rank > 1 ? T(rank + "번째로 찾았어요", "You're #" + rank, rank + "番目に見つけました", "你是第 " + rank + " 个找到的", "Puesto #" + rank)
                     : "";
            if (!string.IsNullOrEmpty(huntTitle)) r = string.IsNullOrEmpty(r) ? huntTitle : r + "  ·  " + huntTitle;
            rankText.text = r;
        }
        if (prizeLabel != null) prizeLabel.text = T("상품", "Prize", "賞品", "奖品", "Premio");
        if (prizeText != null)
            prizeText.text = prize == null ? T("상품 안내를 불러오는 중…", "Loading the prize…", "賞品情報を読み込んでいます…", "正在加载奖品信息…", "Cargando el premio…")
                           : prize.Length > 0 ? prize
                           : T("따로 정한 상품은 없어요", "No prize was set for this game", "このゲームには賞品が設定されていません", "此游戏没有设置奖品", "Este juego no tiene premio");
        if (noteText != null)
        {
            string who = string.IsNullOrEmpty(hostName) ? T("주최자", "the host", "主催者", "主办者", "quien organiza") : T("주최자 " + hostName + "님", "the host (" + hostName + ")", "主催者 " + hostName + " さん", "主办者 " + hostName, hostName);
            string note = T(who + "에게 찾았다는 메세지를 보냈어요", "We let " + who + " know you found it", who + "に見つけたことをメッセージで送りました", "已发消息通知" + who + "：你找到了宝藏", "Avisamos a " + who + " de que lo encontraste");
            if (!string.IsNullOrEmpty(prize)) note += "\n" + T("상품은 메세지로 주최자와 이야기해 보세요", "Message the host about the prize", "賞品については主催者とメッセージで相談してください", "关于奖品，请通过消息与主办者沟通", "Escribe a quien organiza para hablar del premio");
            noteText.text = note;
        }
        if (okLabel != null) okLabel.text = T("확인", "OK", "OK", "确定", "Aceptar");
    }

    private IEnumerator Fetch(int id)
    {
        long code = 0; JSONNode j = null;
        yield return R0926HuntApi.Get("/api/hunts/" + id, (c, r) => { code = c; j = r; });
        fetchCo = null;
        if (id != huntId || !Showing) yield break;
        var h = code == 200 && j != null ? j["hunt"] : null;
        prize = h != null && h.HasKey("prize") ? (string)h["prize"] : "";
        if (h != null && string.IsNullOrEmpty(hostName)) hostName = h["host"]["username"];
        Paint();
    }

    // ── 연출 ────────────────────────────────────────────────
    private IEnumerator Play()
    {
        opened = false;
        group.alpha = 0f;
        group.blocksRaycasts = true;   // 뒤의 AR·도크를 누르지 않게
        info.alpha = 0f;
        info.blocksRaycasts = false;
        info.interactable = false;
        foreach (var p in pieces) p.rt.gameObject.SetActive(false);
        SetGlints(false);
        Set(rays, 0f, 0.6f); Set(rays2, 0f, 0.6f); Set(glow, 0f, 0.5f);
        giftBox.localScale = Vector3.zero;
        giftLid.localScale = Vector3.zero;
        giftLid.anchoredPosition = lidRest;
        giftLid.localEulerAngles = Vector3.zero;
        Alpha(giftLid, 1f);
        var infoRt = (RectTransform)info.transform;
        Vector2 infoAt = infoRt.anchoredPosition;

        yield return Wait(0.25f);   // AR 상자가 빙글 돌며 사라지는 것을 잠깐 보이고
        yield return Tween(0.25f, k => group.alpha = k);

        // 선물 상자가 튀어나온다
        yield return Tween(0.45f, k =>
        {
            float s = BackOut(k);
            giftBox.localScale = giftLid.localScale = Vector3.one * s;
            Set(glow, 0.45f * k, 0.5f + 0.4f * k);
        });
        // 뚜껑이 들썩인다
        yield return Tween(0.45f, k =>
        {
            float a = Mathf.Sin(k * Mathf.PI * 6f) * 7f * (1f - k);
            giftLid.localEulerAngles = new Vector3(0f, 0f, a);
            giftLid.anchoredPosition = lidRest + new Vector2(0f, Mathf.Abs(a) * 2f);
        });

        // 펑 — 뚜껑이 날아가고 빛줄기·꽃가루
        opened = true;
        Burst(new Vector2(0f, giftBox.anchoredPosition.y + giftBox.rect.height * 0.5f), 40);
        SetGlints(true);
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect();
        float infoFrom = infoAt.y - 50f;
        yield return Tween(0.55f, k =>
        {
            float e = 1f - (1f - k) * (1f - k);
            giftLid.anchoredPosition = lidRest + new Vector2(170f * e, 380f * e);
            giftLid.localEulerAngles = new Vector3(0f, 0f, -28f * e);
            Alpha(giftLid, 1f - Mathf.Clamp01((k - 0.4f) / 0.6f));
            float squash = k < 0.3f ? 1f - 0.08f * Mathf.Sin(k / 0.3f * Mathf.PI) : 1f;
            giftBox.localScale = new Vector3(2f - squash, squash, 1f);
            Set(rays, 0.85f * e, 0.6f + 0.4f * e);
            Set(rays2, 0.5f * e, 0.6f + 0.4f * e);
            Set(glow, 0.45f + 0.25f * e, 0.9f + 0.2f * e);
            float ki = Mathf.Clamp01((k - 0.35f) / 0.65f);
            info.alpha = ki;
            infoRt.anchoredPosition = new Vector2(infoAt.x, Mathf.Lerp(infoFrom, infoAt.y, 1f - (1f - ki) * (1f - ki)));
        });
        giftBox.localScale = Vector3.one;
        info.alpha = 1f;
        infoRt.anchoredPosition = infoAt;
        info.blocksRaycasts = true;
        info.interactable = true;
        playCo = null;
    }

    private void Update()
    {
        if (!Showing) return;
        float dt = Time.unscaledDeltaTime;
        Fit();
        if (opened)
        {
            if (rays != null) rays.Rotate(0f, 0f, 12f * dt);
            if (rays2 != null) rays2.Rotate(0f, 0f, -8f * dt);
        }
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (!p.rt.gameObject.activeSelf) continue;
            p.age += dt;
            if (p.age >= p.life) { p.rt.gameObject.SetActive(false); continue; }
            p.v.y -= 2400f * dt;
            p.v *= 1f - 1.1f * dt;   // 종이라 공기에 걸려 느려진다
            p.rt.anchoredPosition += p.v * dt;
            p.rt.localEulerAngles = new Vector3(0f, 0f, p.rt.localEulerAngles.z + p.spin * dt);
            p.rt.localScale = new Vector3(Mathf.Cos(p.age * p.flip), 1f, 1f);   // 뒤집히며 팔랑
            var c = p.img.color;
            c.a = Mathf.Clamp01((p.life - p.age) / 0.45f);
            p.img.color = c;
        }
        if (glints != null && glints[0].gameObject.activeSelf)
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < glints.Length; i++)
            {
                float ph = Mathf.Repeat(t / 1.6f + i * 0.37f, 1f);
                float k = Mathf.Sin(ph * Mathf.PI);
                k *= k;
                glints[i].localScale = Vector3.one * (0.4f + 0.6f * k);
                glints[i].localEulerAngles = new Vector3(0f, 0f, 30f * ph - 15f);
                glintImgs[i].color = new Color(1f, 0.94f, 0.7f, k);
            }
        }
    }

    // 가로 화면·작은 화면 — 다 들어가게 통째로 줄인다
    private void Fit()
    {
        if (content == null) return;
        float h = ((RectTransform)panel.transform).rect.height;
        float s = h > 0f ? Mathf.Min(1f, h / NeedHeight) : 1f;
        if (Mathf.Abs(content.localScale.x - s) > 0.001f) content.localScale = new Vector3(s, s, 1f);
    }

    private void Burst(Vector2 from, int n)
    {
        for (int i = 0; i < n; i++)
        {
            var p = i < pieces.Count ? pieces[i] : NewPiece();
            bool round = confettiRound != null && Random.value < 0.35f;
            p.img.sprite = round ? confettiRound : null;
            p.rt.sizeDelta = round ? Vector2.one * Random.Range(18f, 26f) : new Vector2(Random.Range(14f, 22f), Random.Range(26f, 40f));
            p.img.color = Paper[Random.Range(0, Paper.Length)];
            p.rt.anchoredPosition = from + Random.insideUnitCircle * 40f;
            p.rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            float ang = Random.Range(-70f, 70f) * Mathf.Deg2Rad;
            p.v = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * Random.Range(900f, 1750f);
            p.spin = Random.Range(-520f, 520f);
            p.flip = Random.Range(5f, 11f);
            p.age = 0f;
            p.life = Random.Range(1.3f, 2.1f);
            p.rt.gameObject.SetActive(true);
            p.rt.SetAsLastSibling();
        }
    }

    private Piece NewPiece()
    {
        var go = new GameObject("Paper", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(stage, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        var p = new Piece { rt = rt, img = img };
        pieces.Add(p);
        return p;
    }

    private void SetGlints(bool on)
    {
        if (glintSprite == null || stage == null) return;
        if (glints == null)
        {
            if (!on) return;
            glints = new RectTransform[GlintSpots.Length];
            glintImgs = new Image[GlintSpots.Length];
            for (int i = 0; i < glints.Length; i++)
            {
                var go = new GameObject("Glint" + i, typeof(RectTransform), typeof(Image));
                glints[i] = (RectTransform)go.transform;
                glints[i].SetParent(stage, false);
                glints[i].anchoredPosition = GlintSpots[i];
                glints[i].sizeDelta = Vector2.one * (i % 2 == 0 ? 110f : 76f);
                glintImgs[i] = go.GetComponent<Image>();
                glintImgs[i].sprite = glintSprite;
                glintImgs[i].raycastTarget = false;
                glintImgs[i].color = new Color(1f, 0.94f, 0.7f, 0f);
            }
        }
        foreach (var g in glints) g.gameObject.SetActive(on);
    }

    private void Close()
    {
        if (!Showing) return;
        StartCoroutine(CloseCo());
    }

    private IEnumerator CloseCo()
    {
        if (playCo != null) { StopCoroutine(playCo); playCo = null; }
        info.interactable = false;
        int id = huntId;
        yield return Tween(0.2f, k => group.alpha = 1f - k);
        panel.SetActive(false);
        R0926HuntPush.Open(id.ToString(), null, false);   // 그 게임 상세 — 결과 · 주최자에게 한마디
    }

    // ── 작은 도구 ───────────────────────────────────────────
    private static IEnumerator Tween(float secs, System.Action<float> step)
    {
        for (float t = 0f; t < secs; t += Time.unscaledDeltaTime)
        {
            step(t / secs);
            yield return null;
        }
        step(1f);
    }

    private static IEnumerator Wait(float secs)
    {
        for (float t = 0f; t < secs; t += Time.unscaledDeltaTime) yield return null;
    }

    private static float BackOut(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static void Set(RectTransform rt, float alpha, float scale)
    {
        if (rt == null) return;
        rt.localScale = Vector3.one * scale;
        Alpha(rt, alpha);
    }

    private static void Alpha(RectTransform rt, float a)
    {
        var img = rt.GetComponent<Image>();
        if (img == null) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }
}
