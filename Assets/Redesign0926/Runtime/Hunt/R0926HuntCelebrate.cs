using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보물찾기 — 보물을 찾았을 때 축하 화면 (10-14 대표님: '보물을 찾았습니다' 와 함께 축하 애니메이션 · 상품 안내 / 10-22: 열리는 애니메이션과 함께 화면 가득 /
/// 10-26: 보물상자가 살짝 떨리며 두근두근하다 3~4초 뒤 짜잔 — 열리면서 카드처럼 상품·몇 번째·정보, 외부 말고 기기 GPU 로).
///  1) 찾은 AR 보물이 그 자리에서 화면 가운데로 날아온다 — R0926HuntReveal 이 그 보물을 그대로 3D 로 찍는다 (사진·3D 모델도 입은 그대로)
///  2) 두근두근 약 3.2초 — 심장처럼 쿵-쿵 부풀며 진동, 점점 세게 떨고, 뚜껑이 두 번 들썩이며 틈으로 빛이 샌다. 화면을 누르면 바로 연다
///  3) 짜잔 — 번쩍 · 뚜껑이 활짝(뚜껑 없는 모양은 한 바퀴) · 빛줄기 · 금화·꽃가루 · 강한 진동
///  4) 상자에서 카드가 솟으며 뒤집혀 앞면 — 금박 카드: 순위 메달(1등은 왕관) · '보물을 찾았습니다!' · 몇 번째 · 게임 이름 · 상품 ·
///     주최 · 찾은 시각 · 순위 · 주최자에게 메세지가 갔다는 안내. 반짝임이 훑고 '확인'(안드로이드 뒤로가기도) → 그 게임 상세
/// 이 컴포넌트는 늘 켜진 빈 틀(HuntCelebrate0926)에 있고 보이는 것은 panel 아래 — Apply(Redesign0926ApplyHuntTreasure)가 만든다.
/// </summary>
public class R0926HuntCelebrate : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image dim;
    [SerializeField] private Button skipButton;            // 어둠 — 두근두근 동안 누르면 바로 연다
    [SerializeField] private RectTransform content;        // 가로 화면·작은 화면에선 통째로 줄인다
    [SerializeField] private R0926HuntReveal reveal;
    [SerializeField] private RectTransform rays, rays2, glow;
    [SerializeField] private RectTransform seamGlow;       // 틈으로 새는 빛
    [SerializeField] private Text caption;                 // 두근두근… · 짜잔!
    [SerializeField] private RectTransform card, cardFront, cardBack;
    [SerializeField] private RectTransform shine;          // 카드를 훑는 반짝임 (RectMask2D 안)
    [SerializeField] private RectTransform seal, crown;
    [SerializeField] private Text sealNum;
    [SerializeField] private Text title, rankText, huntText, prizeLabel, prizeText, noteText, okLabel;
    [SerializeField] private Text[] metaKeys, metaValues;  // 주최 · 찾은 시각 · 순위
    [SerializeField] private CanvasGroup okGroup;
    [SerializeField] private Button okButton;
    [SerializeField] private RectTransform fx;             // 금화·꽃가루·별빛
    [SerializeField] private Image flash;
    [SerializeField] private Sprite confettiRound;         // 동그란 꽃가루 (비어 있으면 네모만)
    [SerializeField] private Sprite glintSprite;
    [SerializeField] private Sprite coinSprite;            // 꽃가루에 섞이는 금화 (팔랑이면 동전이 도는 것처럼)

    private const float NeedHeight = 1800f, NeedWidth = 1300f;   // content 가 다 들어가는 크기 (캔버스 단위)
    private const float ChestY = 70f, CardY = 0f, CardTop = 640f;   // CardTop = 카드 가운데에서 메달까지
    private const float Present = 20f;                    // 보여 주는 각도 — 살짝 비스듬히 (옆면까지)
    private const float LidOpen = 112f;
    private const float Suspense = 3.2f, Crescendo = 2.75f, Dub = 0.17f;
    private static readonly float[] Beats = { 0.05f, 0.95f, 1.65f, 2.2f, 2.6f };   // 쿵 (뒤따르는 쿵은 Dub 뒤) — 점점 빨라진다
    private static readonly float[] Peeks = { 1.3f, 2.4f };                         // 뚜껑이 들썩 (뚜껑 없는 모양은 폴짝)
    private static readonly Color SealGold = new Color(1f, 0.82f, 0.42f, 1f);
    private static readonly Color[] Paper =
    {
        new Color(0.910f, 0.749f, 0.310f), new Color(1f, 0.702f, 0.278f), new Color(0.914f, 0.325f, 0.514f),
        new Color(1f, 1f, 1f), new Color(0.44f, 0.85f, 0.72f), new Color(0.45f, 0.70f, 1f),
    };
    private static readonly Vector2[] ChestSpots =
        { new Vector2(-360f, 330f), new Vector2(340f, 400f), new Vector2(-330f, -60f), new Vector2(380f, 40f), new Vector2(40f, 520f) };
    private static readonly Vector2[] CardSpots =
        { new Vector2(-605f, 640f), new Vector2(615f, 540f), new Vector2(-622f, -270f), new Vector2(618f, -500f), new Vector2(370f, 790f) };

    private enum Phase { None, Arrive, Suspense, Open, Card, Done }

    private static R0926HuntCelebrate instance;

    private class Piece { public RectTransform rt; public Image img; public Vector2 v; public float spin, flip, age, life; }
    private readonly List<Piece> pieces = new List<Piece>();
    private RectTransform[] glints;
    private Image[] glintImgs;
    private Phase phase;
    private bool skip, closing, hasFrom;
    private Vector2 fromPos;
    private float fromHeight, fromYaw, flashT = 9f, shineT;
    private int huntId, rank, total;
    private bool ended;
    private string huntTitle, prize, hostName;
    private System.DateTime foundAt;
    private Coroutine playCo, fetchCo;

    private static string T(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

    public static bool Showing => instance != null && instance.panel != null && instance.panel.activeSelf;
    /// <summary>찾은 AR 보물을 넘겨받을 수 있다 (R0926HuntTreasure 가 앵커에서 떼어 넘긴다)</summary>
    public static bool Ready => instance != null && instance.panel != null && instance.reveal != null;

    /// <summary>찾음 — 축하 화면을 띄운다. chest = AR 에서 떼어 낸 그 보물 (없으면 kind·url 로 새로 만든다).
    /// prize 가 null 이면(앱을 다시 켜 아직 모름) 서버에서 받아 채운다. total 은 찾을 수 있는 사람 수 (0 = 제한 없음). 띄울 화면이 없으면 false</summary>
    public static bool Show(int id, string title, int rank, int total, bool ended, string prize, string hostName, string kind, string url, R0926HuntChest chest = null)
    {
        if (instance == null || instance.panel == null) return false;
        R0926HuntTreasure.HideToast();   // 남아 있던 '조금만 더 가까이' 같은 알약이 축하 화면 뒤로 비치지 않게
        instance.Begin(id, title, rank, total, ended, prize, hostName, kind, url, chest);
        return true;
    }

    private void Awake()
    {
        instance = this;
        if (okButton != null) okButton.onClick.AddListener(Close);
        if (skipButton != null) skipButton.onClick.AddListener(Skip);
        if (panel != null) panel.SetActive(false);
    }

    private void OnDisable()
    {
        if (panel != null && panel.activeSelf) Finish();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Begin(int id, string t, int r, int n, bool last, string p, string host, string kind, string url, R0926HuntChest chest)
    {
        if (Showing) Finish();
        huntId = id; huntTitle = t; rank = r; total = n; ended = last; prize = p; hostName = host;
        foundAt = System.DateTime.Now;
        closing = false;
        transform.SetAsLastSibling();
        panel.SetActive(true);
        Fit();
        hasFrom = chest != null && Source(chest);   // AR 화면의 자리·크기·방향 — 무대로 옮기기 전에
        bool staged = reveal != null && reveal.Begin(chest, kind, url);
        Paint();
#if UNITY_EDITOR
        EditorBuzz = 0;
        EditorOpenedAfter = -1f;
        editorShownAt = Time.unscaledTime;
#endif
        if (playCo != null) StopCoroutine(playCo);
        playCo = StartCoroutine(Play(staged));
        if (fetchCo != null) StopCoroutine(fetchCo);
        fetchCo = prize == null && R0926HuntApi.CanCall ? StartCoroutine(Fetch(id)) : null;
    }

    // AR 보물이 화면에 보이던 자리(content 좌표)·높이·이쪽에서 본 방향
    private bool Source(R0926HuntChest chest)
    {
        var cam = Camera.main;
        var body = chest.GetComponent<Renderer>();
        if (cam == null || body == null || !body.enabled) return false;   // 복구 중(숨김) — 가운데서 튀어나온다
        Transform t = chest.transform;
        Vector3 c = cam.WorldToScreenPoint(t.position);
        if (c.z < 0.1f || c.x < 0f || c.y < 0f || c.x > Screen.width || c.y > Screen.height) return false;
        Vector3 top = cam.WorldToScreenPoint(t.TransformPoint(0f, 0.5f, 0f));
        Vector3 bottom = cam.WorldToScreenPoint(t.TransformPoint(0f, -0.42f, 0f));
        var canvas = content.GetComponentInParent<Canvas>();
        Camera ui = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, c, ui, out fromPos)) return false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, top, ui, out Vector2 a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, bottom, ui, out Vector2 b);
        fromHeight = (a - b).magnitude;
        Vector3 d = t.position - cam.transform.position;
        d.y = 0f;
        float look = d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d).eulerAngles.y : t.eulerAngles.y;
        fromYaw = Mathf.DeltaAngle(look, t.eulerAngles.y);   // 0 = 앞(자물쇠)이 이쪽
        return true;
    }

    private void Paint()
    {
        if (title != null) title.text = T("보물을 찾았습니다!", "You found the treasure!", "宝を見つけました！", "你找到宝藏了！", "¡Encontraste el tesoro!");
        if (rankText != null)
            rankText.text = rank == 1 ? T("가장 먼저 찾았어요", "You were the first!", "一番乗りです！", "你是第一个找到的！", "¡Lo encontraste primero!")
                          : rank > 1 && ended ? T("마지막 보물을 찾았어요", "You found the last one!", "最後の宝を見つけました", "你找到了最后一个宝藏", "¡Encontraste el último!")
                          : rank > 1 ? T(rank + "번째로 찾았어요", "You're #" + rank, rank + "番目に見つけました", "你是第 " + rank + " 个找到的", "Puesto #" + rank)
                          : "";
        if (huntText != null) OneLine(huntText, huntTitle ?? "");
        if (prizeLabel != null) prizeLabel.text = T("상품", "Prize", "賞品", "奖品", "Premio");
        if (prizeText != null)
            prizeText.text = prize == null ? T("상품 안내를 불러오는 중…", "Loading the prize…", "賞品情報を読み込んでいます…", "正在加载奖品信息…", "Cargando el premio…")
                           : prize.Length > 0 ? prize
                           : T("따로 정한 상품은 없어요", "No prize was set for this game", "このゲームには賞品が設定されていません", "此游戏没有设置奖品", "Este juego no tiene premio");
        int row = 0;
        Meta(ref row, T("주최", "Host", "主催", "主办", "Organiza"),
             string.IsNullOrEmpty(hostName) ? null : T(hostName + "님", hostName, hostName + " さん", hostName, hostName));
        Meta(ref row, T("찾은 시각", "Found at", "見つけた時刻", "找到时间", "Hora"), When(foundAt));
        Meta(ref row, T("순위", "Rank", "順位", "名次", "Puesto"),
             rank <= 0 ? null
             : total > 0 ? T(rank + " / " + total + "명", rank + " of " + total, rank + " / " + total + "人", rank + " / " + total + " 人", rank + " de " + total)
             : T(rank + "번째", "#" + rank, rank + "番目", "第 " + rank + " 个", "#" + rank));
        for (int i = row; metaKeys != null && i < metaKeys.Length; i++)
            if (metaKeys[i] != null) metaKeys[i].transform.parent.gameObject.SetActive(false);
        if (noteText != null)
        {
            string note = T("주최자에게 찾았다는 메세지를 보냈어요", "We let the host know you found it", "主催者に見つけたことをメッセージで送りました", "已发消息通知主办者：你找到了宝藏", "Avisamos a quien organiza de que lo encontraste");
            if (!string.IsNullOrEmpty(prize)) note += "\n" + T("상품은 메세지로 주최자와 이야기해 보세요", "Message the host about the prize", "賞品については主催者とメッセージで相談してください", "关于奖品，请通过消息与主办者沟通", "Escribe a quien organiza para hablar del premio");
            noteText.text = note;
        }
        if (okLabel != null) okLabel.text = T("확인", "OK", "OK", "确定", "Aceptar");
        if (sealNum != null)
        {
            sealNum.text = rank > 0 ? rank.ToString() : "★";
            sealNum.fontSize = rank >= 100 ? 72 : rank >= 10 ? 104 : 134;
        }
        if (crown != null) crown.gameObject.SetActive(rank == 1);
    }

    private void Meta(ref int row, string key, string value)
    {
        if (string.IsNullOrEmpty(value) || metaKeys == null || row >= metaKeys.Length || metaKeys[row] == null) return;
        metaKeys[row].transform.parent.gameObject.SetActive(true);
        metaKeys[row].text = key;
        OneLine(metaValues[row], value);
        row++;
    }

    // 한 줄에 안 들어가면 끝을 … 로 (게임 이름 · 주최자 이름 — 둘째 줄이 잘려 사라지지 않게)
    private static void OneLine(Text t, string s)
    {
        t.text = s;
        float w = t.rectTransform.rect.width;
        if (w <= 0f || t.preferredWidth <= w) return;
        for (int n = s.Length - 1; n > 0; n--)
        {
            t.text = s.Substring(0, n).TrimEnd() + "…";
            if (t.preferredWidth <= w) return;
        }
    }

    private static string When(System.DateTime d)
    {
        string hm = d.ToString("HH:mm", CultureInfo.InvariantCulture);
        int h12 = d.Hour % 12 == 0 ? 12 : d.Hour % 12;
        string[] es = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sept", "oct", "nov", "dic" };
        return T(d.Month + "월 " + d.Day + "일 " + (d.Hour < 12 ? "오전 " : "오후 ") + h12 + ":" + d.ToString("mm", CultureInfo.InvariantCulture),
                 d.ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture),
                 d.Month + "月" + d.Day + "日 " + hm,
                 d.Month + "月" + d.Day + "日 " + hm,
                 d.Day + " " + es[d.Month - 1] + " · " + hm);
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
        if (h != null && total <= 0) total = h["winners_max"].AsInt;
        Paint();
    }

    // ── 연출 ────────────────────────────────────────────────
    private IEnumerator Play(bool staged)
    {
        group.alpha = 1f;               // 어둠만 서서히 — 날아오는 보물은 처음부터 또렷이 (AR 에서 사라지는 순간 이어지게)
        group.blocksRaycasts = true;    // 뒤의 AR·도크를 누르지 않게
        Dim(0f);
        okGroup.alpha = 0f;
        okGroup.interactable = false;
        okGroup.blocksRaycasts = false;
        foreach (var p in pieces) p.rt.gameObject.SetActive(false);
        SetGlints(false, null);
        Set(rays, 0f, 0.6f); Set(rays2, 0f, 0.6f); Set(glow, 0f, 0.7f); Set(seamGlow, 0f, 1f);
        Caption("", 0f, 1f);
        card.gameObject.SetActive(false);
        flashT = 9f;
        Alpha(flash, 0f);
        skip = false;

        var view = reveal != null ? reveal.View : null;
        bool chest = staged && view != null && reveal.Chest != null;
        bool lidded = chest && reveal.Chest.HasLid;
        float seam = chest ? reveal.Chest.Seam : 0.1f;
        if (view != null) { view.anchoredPosition = new Vector2(0f, ChestY); view.localScale = Vector3.one; Alpha(view, 1f); }

        if (chest)
        {
            // 1) 날아온다 — 찾은 AR 보물이 그 자리·크기·방향에서 가운데로 (AR 에서 안 보였으면 가운데서 톡 튀어나온다)
            phase = Phase.Arrive;
            Vector2 p0 = new Vector2(0f, ChestY), p1 = p0;
            float s0 = 0f, yaw0 = Present - 150f;
            if (hasFrom)
            {
                reveal.Pose(fromYaw, 0f, 0f, Vector3.zero, Vector3.one);
                Vector2 mid = reveal.ViewPoint(Vector3.zero);
                float h = (reveal.ViewPoint(new Vector3(0f, 0.5f, 0f)) - reveal.ViewPoint(new Vector3(0f, -0.42f, 0f))).magnitude;
                s0 = Mathf.Clamp(fromHeight / Mathf.Max(1f, h), 0.06f, 1.4f);
                p0 = fromPos - mid * s0;
                yaw0 = fromYaw;
            }
            float dur = hasFrom ? 0.55f : 0.5f;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / dur);
                Dim(Mathf.Clamp01(t / 0.3f));
                if (hasFrom)
                {
                    float e = k * k * (3f - 2f * k);
                    view.anchoredPosition = Vector2.Lerp(p0, p1, e);
                    view.localScale = Vector3.one * Mathf.Lerp(s0, 1f, e);
                    reveal.Pose(yaw0 + Mathf.DeltaAngle(yaw0, Present) * e, 0f, 0f, Vector3.zero, Vector3.one);
                }
                else
                {
                    view.localScale = Vector3.one * Mathf.Max(0f, BackOut(k));
                    reveal.Pose(Mathf.Lerp(yaw0, Present, 1f - (1f - k) * (1f - k)), 0f, 0f, Vector3.zero, Vector3.one);
                }
                Set(glow, 0.2f * k, 0.7f + 0.2f * k);
                if (k >= 1f) break;
                yield return null;
            }

            // 2) 두근두근 — 쿵-쿵 · 점점 세게 떨고 · 뚜껑이 들썩이며 틈으로 빛 · 끝에 몰아치듯
            phase = Phase.Suspense;
            string second = lidded ? T("무엇이 들어 있을까요?", "What's inside?", "何が入っているかな？", "里面会是什么呢？", "¿Qué habrá dentro?")
                                   : T("과연 어떤 보물일까요?", "What could it be?", "どんな宝かな？", "会是什么宝藏呢？", "¿Qué será?");
            Caption(T("두근두근…", "Ba-dum, ba-dum…", "ドキドキ…", "怦怦心跳…", "Bum, bum…"), 0f, 1f);
            int lub = 0, dub = 0, slam = 0;
            float tick = Crescendo, said = 0f;
            bool swapped = false;
            for (float s = 0f; s < Suspense; s += Time.unscaledDeltaTime)
            {
                if (skip)
                {
                    skip = false;
                    if (s < Crescendo)
                    {
                        s = Crescendo;
                        lub = dub = Count(Beats, Crescendo);
                        slam = Count(Peeks, Crescendo);
                    }
                }
                Dim(1f);
                while (lub < Beats.Length && s >= Beats[lub]) { Buzz(1); lub++; }
                while (dub < Beats.Length && s >= Beats[dub] + Dub) { Buzz(0); dub++; }
                while (slam < Peeks.Length && s >= Peeks[slam] + 0.2f) { Buzz(0); slam++; }
                if (s >= Crescendo && s >= tick) { Buzz(0); tick += 0.085f; }

                float beat = 0f;
                foreach (float b in Beats) beat = Mathf.Max(beat, Bump(s - b, 0.15f) + 0.5f * Bump(s - b - Dub, 0.13f));
                float grow = s < Crescendo ? 0f : Mathf.SmoothStep(0f, 1f, (s - Crescendo) / (Suspense - Crescendo));
                float amp = s < Crescendo ? Mathf.Lerp(0.35f, 1.8f, Mathf.Pow(s / Crescendo, 1.5f)) : Mathf.Lerp(1.8f, 6f, grow);
                float tt = Time.unscaledTime;
                float roll = amp * (0.6f * Mathf.Sin(tt * 33f) + 0.4f * Mathf.Sin(tt * 51f + 1.3f));
                float yaw = Present + amp * 0.5f * Mathf.Sin(tt * 27f + 0.7f);
                float hop = 0.012f * beat, lid = 0f, leak = 0f;
                for (int i = 0; i < Peeks.Length; i++)
                {
                    float pk = Peek(s - Peeks[i]);
                    if (lidded) lid += (i == 0 ? 8f : 13f) * pk;
                    else hop += (i == 0 ? 0.05f : 0.08f) * pk;
                    leak = Mathf.Max(leak, Mathf.Clamp01(pk));
                }
                if (grow > 0f)
                {
                    if (lidded) lid += grow * (2f + 2.5f * Mathf.Abs(Mathf.Sin(tt * 38f)));
                    leak = Mathf.Max(leak, 0.85f * grow);
                }
                reveal.Pose(yaw, 0f, roll, new Vector3(0f, hop, 0f), Vector3.one * (1f + 0.07f * beat + 0.07f * grow));
                reveal.Lid(lid);
                seamGlow.anchoredPosition = InContent(view, reveal.ViewPoint(new Vector3(0f, seam, 0f)));
                Set(seamGlow, leak, 0.8f + 0.6f * leak);
                Set(glow, 0.2f + 0.2f * beat + 0.1f * s / Suspense + 0.3f * grow, 0.9f + 0.08f * beat + 0.2f * grow);

                if (!swapped && s >= 1.75f) { swapped = true; said = 0f; Caption(second, 0f, 1f); }
                said += Time.unscaledDeltaTime;
                Caption(null, Mathf.Clamp01(said / 0.3f), 1f + 0.05f * beat);
                yield return null;
            }

            // 3) 짜잔 — 번쩍 · 뚜껑이 활짝(뚜껑 없는 모양은 한 바퀴) · 빛줄기 · 금화·꽃가루
            phase = Phase.Open;
#if UNITY_EDITOR
            EditorOpenedAfter = Time.unscaledTime - editorShownAt;
#endif
            flashT = 0f;
            if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect(1f);
#if UNITY_EDITOR
            EditorBuzz++;
#endif
            Caption(T("짜잔!", "Ta-da!", "じゃーん！", "当当！", "¡Tachán!"), 1f, 1.5f);
            Vector2 mouth = InContent(view, reveal.ViewPoint(new Vector3(0f, seam, 0f)));
            Burst(mouth, 44);
            SetGlints(true, ChestSpots);
            float lid0 = reveal.Chest.LidAngle;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.5f);
                if (lidded) reveal.Lid(Mathf.LerpUnclamped(lid0, LidOpen, BackOut(Mathf.Clamp01(t / 0.32f))));
                float spin = lidded ? 0f : 360f * (1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.6f), 3f));
                float hop = lidded ? 0f : 0.16f * Mathf.Sin(k * Mathf.PI);
                float sq = t < 0.12f ? Mathf.Sin(t / 0.12f * Mathf.PI) : 0f;   // 펑 — 잠깐 납작
                float big = Mathf.Lerp(1.07f, 1f, k);
                reveal.Pose(Present + spin, 0f, 0f, new Vector3(0f, hop, 0f), new Vector3(big + 0.05f * sq, big - 0.1f * sq, big + 0.05f * sq));
                float e = 1f - (1f - k) * (1f - k);
                Set(rays, 0.85f * e, 0.6f + 0.4f * e);
                Set(rays2, 0.5f * e, 0.6f + 0.4f * e);
                Set(glow, 0.45f + 0.3f * e, 1f + 0.15f * e);
                seamGlow.anchoredPosition = InContent(view, reveal.ViewPoint(new Vector3(0f, seam, 0f)));
                Set(seamGlow, 1f - 0.4f * e, 1.4f + 1.4f * e);
                Caption(null, 1f, 1f + 0.5f * (1f - Mathf.Clamp01(BackOut(Mathf.Clamp01(t / 0.3f)))));
                if (k >= 1f) break;
                yield return null;
            }
        }
        else
        {
            // 보물을 무대에 못 올렸다 — 어둠 · 번쩍 뒤 카드만
            phase = Phase.Open;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) { Dim(t / 0.3f); yield return null; }
            Dim(1f);
            flashT = 0f;
            if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.HandleTouchFeedbackDirect(1f);
            Burst(new Vector2(0f, CardY), 44);
            Set(rays, 0.85f, 1f); Set(rays2, 0.5f, 1f); Set(glow, 0.7f, 1.1f);
        }

        // 4) 카드 — 상자에서 솟아오르며 뒤집혀 앞면
        phase = Phase.Card;
        Vector2 from = chest ? InContent(view, reveal.ViewPoint(new Vector3(0f, seam, 0f))) : new Vector2(0f, CardY);
        Vector2 to = new Vector2(0f, CardY);
        card.gameObject.SetActive(true);
        cardBack.gameObject.SetActive(true);
        cardFront.gameObject.SetActive(false);
        seal.localScale = Vector3.zero;
        if (crown != null) { crown.localScale = Vector3.one; Alpha(crown, 0f); }
        shine.anchoredPosition = new Vector2(-2000f, 0f);
        for (float t = 0f; ; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / 0.7f);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            card.anchoredPosition = Vector2.Lerp(from, to, e) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 170f;
            float sc = Mathf.LerpUnclamped(0.1f, 1f, BackOut(k));
            float flip = 180f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.85f, k));
            bool front = flip > 90f;
            if (cardFront.gameObject.activeSelf != front) { cardFront.gameObject.SetActive(front); cardBack.gameObject.SetActive(!front); }
            card.localScale = new Vector3(sc * Mathf.Max(0.02f, Mathf.Abs(Mathf.Cos(flip * Mathf.Deg2Rad))), sc, 1f);
            card.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-14f, 0f, e));
            if (view != null) Alpha(view, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.7f, k)));
            Caption(null, 1f - Mathf.Clamp01(k * 2f), 1f);
            Set(seamGlow, 0.6f * (1f - k), 2.8f);
            if (k >= 1f) break;
            yield return null;
        }
        card.anchoredPosition = to;
        card.localScale = Vector3.one;
        card.localEulerAngles = Vector3.zero;
        if (reveal != null) reveal.End();   // 카드가 상자를 다 가렸다 — 카메라를 멈춘다

        // 내려앉음 — 메달이 찍히고(1등은 왕관) · 반짝임이 훑고 · '확인'
        phase = Phase.Done;
        Buzz(1);
        SetGlints(true, CardSpots);
        Burst(new Vector2(0f, CardY + CardTop), 22);
        shineT = 3.85f;   // 곧 첫 반짝임
        var okRt = (RectTransform)okGroup.transform;
        Vector2 okAt = okRt.anchoredPosition;
        for (float t = 0f; ; t += Time.unscaledDeltaTime)
        {
            float ks = Mathf.Clamp01(t / 0.35f);
            seal.localScale = Vector3.one * Mathf.Max(0f, BackOut(ks));
            if (crown != null && crown.gameObject.activeSelf)
            {
                float kc = Mathf.Clamp01((t - 0.12f) / 0.3f);
                crown.localScale = Vector3.one * (1f + 0.25f * (1f - kc));
                Alpha(crown, kc);
            }
            float ko = Mathf.Clamp01((t - 0.3f) / 0.3f);
            okGroup.alpha = ko;
            okRt.anchoredPosition = okAt + Vector2.down * 30f * (1f - ko) * (1f - ko);
            if (t >= 0.6f) break;
            yield return null;
        }
        okRt.anchoredPosition = okAt;
        okGroup.alpha = 1f;
        okGroup.interactable = true;
        okGroup.blocksRaycasts = true;
        playCo = null;
    }

    private void Skip()
    {
        if (phase == Phase.Suspense) skip = true;
    }

    private void Update()
    {
        if (!Showing) return;
        float dt = Time.unscaledDeltaTime;
        Fit();
        if (phase >= Phase.Open)
        {
            if (rays != null) rays.Rotate(0f, 0f, 12f * dt);
            if (rays2 != null) rays2.Rotate(0f, 0f, -8f * dt);
        }
        if (flashT < 0.45f)
        {
            flashT += dt;
            float f = 1f - Mathf.Clamp01(flashT / 0.45f);
            Alpha(flash, 0.9f * f * f);
        }
        if (phase == Phase.Done && shine != null)
        {
            shineT += dt;
            float c = shineT % 4f;   // 4초마다 한 번 훑는다
            shine.anchoredPosition = new Vector2(c < 0.8f ? Mathf.Lerp(-920f, 920f, Mathf.SmoothStep(0f, 1f, c / 0.8f)) : -2000f, 0f);
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
        var r = ((RectTransform)panel.transform).rect;
        float s = r.height > 0f && r.width > 0f ? Mathf.Min(1f, Mathf.Min(r.height / NeedHeight, r.width / NeedWidth)) : 1f;
        if (Mathf.Abs(content.localScale.x - s) > 0.001f) content.localScale = new Vector3(s, s, 1f);
    }

    private void Burst(Vector2 from, int n)
    {
        int used = 0;
        for (int i = 0; i < pieces.Count && used < n; i++) if (!pieces[i].rt.gameObject.activeSelf) { Launch(pieces[i], from); used++; }
        for (; used < n; used++) Launch(NewPiece(), from);
    }

    private void Launch(Piece p, Vector2 from)
    {
        float roll = Random.value;
        bool coin = coinSprite != null && roll < 0.3f;
        bool round = !coin && confettiRound != null && roll < 0.55f;
        p.img.sprite = coin ? coinSprite : round ? confettiRound : null;
        p.rt.sizeDelta = coin ? Vector2.one * Random.Range(44f, 62f) : round ? Vector2.one * Random.Range(18f, 26f) : new Vector2(Random.Range(14f, 22f), Random.Range(26f, 40f));
        p.img.color = coin ? Color.white : Paper[Random.Range(0, Paper.Length)];
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

    private Piece NewPiece()
    {
        var go = new GameObject("Paper", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(fx, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        var p = new Piece { rt = rt, img = img };
        pieces.Add(p);
        return p;
    }

    private void SetGlints(bool on, Vector2[] spots)
    {
        if (glintSprite == null || fx == null) return;
        if (glints == null)
        {
            if (!on) return;
            glints = new RectTransform[ChestSpots.Length];
            glintImgs = new Image[ChestSpots.Length];
            for (int i = 0; i < glints.Length; i++)
            {
                var go = new GameObject("Glint" + i, typeof(RectTransform), typeof(Image));
                glints[i] = (RectTransform)go.transform;
                glints[i].SetParent(fx, false);
                glints[i].sizeDelta = Vector2.one * (i % 2 == 0 ? 110f : 76f);
                glintImgs[i] = go.GetComponent<Image>();
                glintImgs[i].sprite = glintSprite;
                glintImgs[i].raycastTarget = false;
                glintImgs[i].color = new Color(1f, 0.94f, 0.7f, 0f);
            }
        }
        for (int i = 0; i < glints.Length; i++)
        {
            if (spots != null) glints[i].anchoredPosition = spots[i];
            glints[i].gameObject.SetActive(on);
        }
    }

    private void Close()
    {
        if (!Showing || closing) return;
        StartCoroutine(CloseCo());
    }

    private IEnumerator CloseCo()
    {
        closing = true;
        if (playCo != null) { StopCoroutine(playCo); playCo = null; }
        okGroup.interactable = false;
        int id = huntId;
        yield return Tween(0.2f, k => group.alpha = 1f - k);
        Finish();
        closing = false;
        R0926HuntPush.Open(id.ToString(), null, false);   // 그 게임 상세 — 결과 · 주최자에게 한마디
    }

    // 다 치운다 — 무대의 보물 · 카메라 · 꽃가루 · 별빛
    private void Finish()
    {
        if (playCo != null) { StopCoroutine(playCo); playCo = null; }
        if (fetchCo != null) { StopCoroutine(fetchCo); fetchCo = null; }
        if (reveal != null) reveal.End();
        foreach (var p in pieces) p.rt.gameObject.SetActive(false);
        SetGlints(false, null);
        phase = Phase.None;
        panel.SetActive(false);
    }

    // ── 작은 도구 ───────────────────────────────────────────
    private void Dim(float k)
    {
        if (dim == null) return;
        var c = dim.color;
        c.a = 0.85f * k;
        dim.color = c;
    }

    // text 가 null 이면 글은 그대로 두고 알파·크기만
    private void Caption(string text, float alpha, float scale)
    {
        if (caption == null) return;
        if (text != null) caption.text = text;
        var c = caption.color;
        c.a = alpha;
        caption.color = c;
        caption.rectTransform.localScale = Vector3.one * scale;
    }

    // view 의 로컬 좌표 → content 좌표
    private Vector2 InContent(RectTransform view, Vector2 local)
    {
        return content.InverseTransformPoint(view.TransformPoint(local));
    }

    private static int Count(float[] times, float before)
    {
        int n = 0;
        foreach (float t in times) if (t < before) n++;
        return n;
    }

    // 0 → 1 → 0 (w 초 동안 — 앞이 빠르게 솟는다)
    private static float Bump(float x, float w)
    {
        if (x <= 0f || x >= w) return 0f;
        return Mathf.Sin(Mathf.Sqrt(x / w) * Mathf.PI);
    }

    // 뚜껑이 휙 들렸다 탁 닫히고 살짝 튄다 (0.32초)
    private static float Peek(float x)
    {
        if (x <= 0f || x >= 0.32f) return 0f;
        if (x < 0.1f) return Mathf.Sin(x / 0.1f * Mathf.PI * 0.5f);
        if (x < 0.2f) return Mathf.Cos((x - 0.1f) / 0.1f * Mathf.PI * 0.5f);
        return 0.18f * Mathf.Sin((x - 0.2f) / 0.12f * Mathf.PI);
    }

    private static void Buzz(int level)
    {
#if UNITY_EDITOR
        EditorBuzz++;
#endif
        var f = UIFeedbackManager.Instance;
        if (f == null) return;
        if (level >= 1) f.TriggerMediumHaptic();
        else f.TriggerLightHaptic();
    }

    private static IEnumerator Tween(float secs, System.Action<float> step)
    {
        for (float t = 0f; t < secs; t += Time.unscaledDeltaTime)
        {
            step(t / secs);
            yield return null;
        }
        step(1f);
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

    private static void Alpha(Component c, float a)
    {
        var g = c != null ? c.GetComponent<Graphic>() : null;
        if (g == null) return;
        var col = g.color;
        col.a = a;
        g.color = col;
    }

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 진동 횟수 (쿵·쿵 · 들썩 · 몰아침 · 짜잔 · 카드)</summary>
    public static int EditorBuzz;
    /// <summary>플레이 시험 — 뜬 뒤 열리기까지 (초, 아직이면 -1)</summary>
    public static float EditorOpenedAfter = -1f;
    private static float editorShownAt;
    public static string EditorPhase => instance != null ? instance.phase.ToString() : "";
    public static void EditorSkip() { if (instance != null) instance.Skip(); }
#endif
}
