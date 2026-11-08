using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 사진 크게 보기 — 대화방 사진 · 사진 묶음 · 보물찾기 상품 사진(쿠폰·QR — 11-06 OpenPrize).
/// 10-30 대표님 「해당 사진 클릭해서 사진을 볼 수 있도록 하고 본 앱 레이아웃에 어울리는 다운로드, 공유버튼」 →
/// 11-09 대표님 「'닫기 / 다운로드 / 공유' 버튼이 불필요하게 너무 큰데 적절한 사이즈로 좀 줄여서」 · 「사진이 여러개가 있는 경우에는 좌우 스와이프」 · 「다운로드할 때도 묶어서」:
///  · 왼쪽 위 둥근 ✕ · 가운데 위 「2 / 5」(묶음만) · 아래 가운데 작은 알약 하나에 [다운로드] [모두 다운로드(묶음만)] [공유(대화 사진만)] — 높이 44pt.
///    알약이 화면보다 넓으면(긴 언어) 글자를 빼고 그림만
///  · 묶음은 맞춤 크기에서 옆으로 밀면 다음 · 앞 사진(끝에서는 뻑뻑하게). 옆 사진의 원본은 미리 받아 둔다 — 원본은 지금 사진과 앞뒤 한 장씩만 들고 있다
///  · 모두 다운로드는 차례대로 한 장씩 사진첩에 넣고 알림은 한 번 (아이폰은 하나가 끝나야 다음을 받는다). 창을 닫아도 넣던 건 끝까지 넣는다
///  · 말풍선의 작은 사진을 바로 보여 주고 원본을 받아 바꿔 끼운다. 두 손가락·두 번 톡·휠로 키우고, 맞춤 크기에서 위아래로 끌면 닫힌다(뒤로가기도)
///  · 대화방이 닫히면 같이 닫힌다(알림을 눌러 다른 방으로 옮길 때). 화면은 Redesign0926ApplyChatPhoto 가 만들어 꺼 둔다
/// </summary>
public class R0926PhotoView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
{
    [SerializeField] private RectTransform frame;        // 사진이 놓이는 자리 — 사진은 이 틀의 가운데 기준
    [SerializeField] private RawImage photo;
    [SerializeField] private RawImage peek;              // 옆으로 넘기는 동안 따라 들어오는 앞 · 뒤 사진
    [SerializeField] private Image backdrop;
    [SerializeField] private RectTransform closeButton;  // 왼쪽 위 ✕
    [SerializeField] private RectTransform counter;      // 「2 / 5」 — 묶음만
    [SerializeField] private Text counterText;
    [SerializeField] private RectTransform bar;          // 아래 알약
    [SerializeField] private RectTransform[] buttons = new RectTransform[0];   // 알약 안 칸 (왼쪽부터) — 그림 Icon0926 · 글자 Label0926
    [SerializeField] private RectTransform allButton;    // 모두 다운로드 — 묶음만
    [SerializeField] private RectTransform shareButton;  // 공유 — 대화 사진만 (상품 사진은 뺀다)
    [SerializeField] private RectTransform spin;
    [SerializeField] private R0926PhotoShare share;
    [SerializeField] private GameObject chatRoom;

    // 캔버스 단위 (아이폰 3배 화면에서 1pt = 3)
    private const float Edge = 32f, TopGap = 24f, CloseSize = 132f, CounterH = 88f, CounterPad = 34f;
    private const float BarH = 140f, BarPad = 10f, BarBottom = 40f, BtnPad = 34f, IconSize = 52f, IconGap = 12f, Gap = 24f;
    private const float MaxZoom = 6f, Resist = 0.35f, SettleTime = 0.2f, PullClose = 260f, PullFade = 700f, SpinSpeed = 360f;
    private const float PageGap = 48f, PageTurn = 0.2f, PageFlick = 1600f, PageMin = 60f, PageTime = 0.22f;
    private const int NoPointer = int.MinValue;

    private enum Drag { None, Pan, Pull, Page }

    private R0926ChatPhoto.Ref[] refs = new R0926ChatPhoto.Ref[0];
    private Texture[] thumbs = new Texture[0];
    private int index;
    private bool roomBound;                              // 대화방 사진 — 방이 닫히면 같이 닫는다
    private readonly Dictionary<int, Texture2D> fulls = new Dictionary<int, Texture2D>();
    private readonly Dictionary<int, byte[]> bytesOf = new Dictionary<int, byte[]>();
    private readonly HashSet<int> failed = new HashSet<int>();
    private Coroutine loader, anim;
    private int saveWant = -1;
    private bool loading, gathering, pinching, turning;
    private Drag drag;
    private float scale = 1f, prevDist, pager, pageVel, laidBar;
    private Vector2 pos, prevMid, laidFor, area;
    private Rect laidSafe;
    private int dragId = NoPointer;
    private Canvas rootCanvas;

    private static bool saving;                          // 사진첩에 넣는 중 — 창을 닫아도 이어진다
    private static float savingSince;

#if UNITY_EDITOR
    public static byte[] EditorSaved;                    // 플레이 시험 — 마지막으로 사진첩에 넘긴 내용
    public static List<byte[]> EditorSavedAll = new List<byte[]>();   // 마지막 저장에서 넘긴 것 전부
#endif

    public bool IsOpen => gameObject.activeSelf;
    public int Index => index;
    public int Count => refs.Length;
    private R0926ChatPhoto.Ref cur => index >= 0 && index < refs.Length ? refs[index] : null;

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>(true);
        rootCanvas = c != null ? c.rootCanvas : null;
    }

    /// <param name="thumb">말풍선에 보이던 작은 사진 — 원본이 올 때까지 보여 준다 (없으면 도는 표시만)</param>
    public void Open(R0926ChatPhoto.Ref reference, Texture thumb) => Show(new[] { reference }, new[] { thumb }, 0, true);

    /// <summary>사진 묶음(11-09) — at 번째 사진부터. 옆으로 넘겨 다른 사진</summary>
    public void OpenAlbum(R0926ChatPhoto.Ref[] album, int at, Texture[] small) => Show(album, small, at, true);

    /// <summary>보물찾기 상품 사진(쿠폰·QR — 11-06) — 받아 둔 사진 그대로 크게. 다운로드만 (공유 칸은 뺀다)</summary>
    public void OpenPrize(string url, Texture tex)
    {
        if (tex == null || string.IsNullOrEmpty(url)) return;
        Show(new[] { new R0926ChatPhoto.Ref { url = R0926HuntApi.Url(url), file = R0926HuntApi.PrizeFile(url), w = tex.width, h = tex.height } }, new[] { tex }, 0, false);
    }

    private void Show(R0926ChatPhoto.Ref[] album, Texture[] small, int at, bool chat)
    {
        if (IsOpen || album == null || album.Length == 0 || Array.IndexOf(album, null) >= 0) return;
        refs = album;
        thumbs = new Texture[album.Length];
        if (small != null) Array.Copy(small, thumbs, Mathf.Min(small.Length, thumbs.Length));
        index = Mathf.Clamp(at, 0, album.Length - 1);
        roomBound = chat;
        bool many = album.Length > 1;
        if (shareButton != null) shareButton.gameObject.SetActive(chat);
        if (allButton != null) allButton.gameObject.SetActive(many);
        if (counter != null) counter.gameObject.SetActive(many);
        gameObject.SetActive(true);
        bar.gameObject.SetActive(true);
        Dim(1f);
        dragId = NoPointer;
        drag = Drag.None;
        pinching = turning = gathering = false;
        saveWant = -1;
        peek.enabled = false;
        ShowPage();
        Kick();
    }

    public void Close()
    {
        if (cur == null) return;
        StopAnim();
        if (share != null) share.Close();
        photo.texture = null;
        peek.texture = null;
        peek.enabled = false;
        foreach (var t in fulls.Values) if (t != null) Destroy(t);
        fulls.Clear();
        bytesOf.Clear();
        failed.Clear();
        refs = new R0926ChatPhoto.Ref[0];
        thumbs = new Texture[0];
        loader = null;
        saveWant = -1;
        loading = gathering = turning = false;
        gameObject.SetActive(false);
    }

    // 지금 사진을 그린다 — 원본이 있으면 원본, 없으면 말풍선의 작은 사진
    private void ShowPage()
    {
        var tex = Best(index);
        photo.texture = tex;
        photo.enabled = tex != null;
        if (counterText != null) counterText.text = (index + 1) + " / " + refs.Length;
        pager = 0f;
        pos = Vector2.zero;
        Layout();   // 「9 / 10」 → 「10 / 10」 폭이 바뀐다
        scale = Fit;
        Refresh();
        UpdateSpin();
    }

    private Texture Best(int i)
    {
        if (fulls.TryGetValue(i, out var f) && f != null) return f;
        var t = i >= 0 && i < thumbs.Length ? thumbs[i] : null;
        return t != null ? t : null;   // 말풍선이 내려놓은 작은 사진은 null
    }

    private void UpdateSpin()
    {
        loading = cur != null && !fulls.ContainsKey(index) && !failed.Contains(index);
        spin.gameObject.SetActive(loading || gathering);
    }

    // ── 원본 받기 — 지금 사진 먼저, 그다음 뒤 · 앞 ─────────────
    private void Kick()
    {
        if (loader == null && isActiveAndEnabled) loader = StartCoroutine(LoadAround());
    }

    private int NextToLoad()
    {
        for (int k = 0; k < 3; k++)
        {
            int i = k == 0 ? index : k == 1 ? index + 1 : index - 1;
            if (i >= 0 && i < refs.Length && !fulls.ContainsKey(i) && !failed.Contains(i)) return i;
        }
        return -1;
    }

    private IEnumerator LoadAround()
    {
        for (int i = NextToLoad(); i >= 0; i = NextToLoad())
        {
            var r = refs[i];
            byte[] bytes = null;
            yield return R0926ChatPhoto.Fetch(r.url, r.Key("f"), b => bytes = b);
            Texture2D t = R0926ChatPhoto.Decode(bytes);
            if (t == null)
            {
                failed.Add(i);
                if (i == index)
                {
                    if (saveWant == i) saveWant = -1;
                    Toast(false, LoadFail);
                }
                UpdateSpin();
                continue;
            }
            fulls[i] = t;
            bytesOf[i] = bytes;
            Evict();
            if (i == index)
            {
                photo.texture = t;
                photo.enabled = true;
            }
            UpdateSpin();
            if (saveWant == i)
            {
                saveWant = -1;
                SaveChain(new List<byte[]> { bytes }, 1);
            }
        }
        loader = null;
    }

    // 원본은 지금 사진과 앞뒤 한 장씩만 — 10장을 다 들고 있지 않게
    private void Evict()
    {
        List<int> drop = null;
        foreach (int k in fulls.Keys)
            if (Mathf.Abs(k - index) > 1) (drop ??= new List<int>()).Add(k);
        if (drop == null) return;
        foreach (int k in drop)
        {
            if (photo.texture == fulls[k]) photo.texture = null;
            if (peek.texture == fulls[k]) peek.texture = null;
            Destroy(fulls[k]);
            fulls.Remove(k);
            bytesOf.Remove(k);
        }
    }

    // ── 다운로드 · 공유 ─────────────────────────────────────
    private static bool Busy => saving && Time.unscaledTime - savingSince < 60f;   // 사진첩이 끝내 답하지 않으면 1분 뒤 풀린다

    public void Download()
    {
        if (cur == null || Busy || gathering) return;
        if (bytesOf.TryGetValue(index, out var b)) { SaveChain(new List<byte[]> { b }, 1); return; }
        saveWant = index;   // 원본이 오면 넣는다
        if (failed.Remove(index)) Kick();   // 못 받았던 원본 — 다시 받아 본다
        UpdateSpin();
    }

    /// <summary>묶음 전부 — 원본을 다 모은 뒤(기기에 받아 둔 건 그대로) 차례대로 사진첩에</summary>
    public void DownloadAll()
    {
        if (cur == null || Busy || gathering || refs.Length < 2) return;
        StartCoroutine(GatherAll());
    }

    private IEnumerator GatherAll()
    {
        gathering = true;
        UpdateSpin();
        var list = new List<byte[]>();
        for (int i = 0; i < refs.Length; i++)
        {
            byte[] b;
            if (!bytesOf.TryGetValue(i, out b))
                yield return R0926ChatPhoto.Fetch(refs[i].url, refs[i].Key("f"), x => b = x);
            if (b != null && b.Length > 0) list.Add(b);
        }
        gathering = false;
        UpdateSpin();
        if (list.Count == 0) { Toast(false, LoadFail); yield break; }
        SaveChain(list, refs.Length);
    }

    public void Share()
    {
        if (cur != null && share != null) share.Open(cur.url);
    }

    private static string LoadFail => R0926LocalizedText.Say("사진을 불러오지 못했어요", "Couldn't load photo", "写真を読み込めません", "无法加载照片", "No se pudo cargar la foto");
    private static string SaveFail => R0926LocalizedText.Say("사진을 저장하지 못했어요. 사진 접근 권한을 확인해 주세요", "Couldn't save the photo. Please check photo access", "写真を保存できませんでした。写真へのアクセスを確認してください", "无法保存照片，请检查照片权限", "No se pudo guardar la foto. Revisa el acceso a fotos");
    private static string Saved(int n) => n == 1
        ? R0926LocalizedText.Say("사진첩에 저장했어요", "Saved to your photos", "写真に保存しました", "已保存到相册", "Guardada en tus fotos")
        : R0926LocalizedText.Say($"사진 {n}장을 사진첩에 저장했어요", $"Saved {n} photos to your photos", $"写真{n}枚を保存しました", $"已将{n}张照片保存到相册", $"Se guardaron {n} fotos");
    private static string SavedSome(int ok, int n) => R0926LocalizedText.Say($"사진 {n}장 중 {ok}장만 저장했어요", $"Saved {ok} of {n} photos", $"{n}枚中{ok}枚を保存しました", $"{n}张中已保存{ok}张", $"Se guardaron {ok} de {n} fotos");

    // 한 장씩 차례로 — 앞 사진의 답이 와야 다음을 넘긴다(아이폰 사진첩은 답을 하나만 기다린다). 창이 닫혀도 끝까지 간다
    private static void SaveChain(List<byte[]> list, int asked)
    {
        saving = true;
        savingSince = Time.unscaledTime;
#if UNITY_EDITOR
        EditorSavedAll = new List<byte[]>();
#endif
        SaveNext(list, 0, 0, DateTime.Now.ToString("yyyyMMdd_HHmmss"), asked);
    }

    private static void SaveNext(List<byte[]> list, int i, int ok, string stamp, int asked)
    {
        if (i >= list.Count)
        {
            saving = false;
            if (ok == asked) Toast(true, Saved(asked));
            else Toast(false, ok > 0 ? SavedSome(ok, asked) : SaveFail);
            return;
        }
        string name = "Woopang_" + stamp + (list.Count > 1 ? "_" + (i + 1) : "") + ".jpg";
#if UNITY_EDITOR
        EditorSaved = list[i];
        EditorSavedAll.Add(list[i]);
#endif
        try
        {
            NativeGallery.SaveImageToGallery(list[i], "WOOPANG", name, (good, path) => SaveNext(list, i + 1, ok + (good ? 1 : 0), stamp, asked));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SaveNext(list, i + 1, ok, stamp, asked);
        }
    }

    private static void Toast(bool ok, string text)
    {
        if (ToastManager.Instance == null) return;
        if (ok) ToastManager.Instance.ShowSuccess(text);
        else ToastManager.Instance.ShowWarning(text);
    }

    private void Update()
    {
        if (cur == null) return;
        if (roomBound && chatRoom != null && !chatRoom.activeInHierarchy) { Close(); return; }
        if (laidFor != ((RectTransform)transform).rect.size || laidSafe != Screen.safeArea || Mathf.Abs(BarWidth(true) - laidBar) > 0.5f) Relayout();   // 글자는 칸이 처음 켜질 때(Start) 언어대로 바뀐다
        if (bar.gameObject.activeSelf == ShareOpen) bar.gameObject.SetActive(!ShareOpen);   // 공유 시트 틈으로 알약이 비치지 않게
        if (photo.enabled && photo.texture == null) photo.enabled = false;   // 말풍선이 작은 사진을 내려놓았다 — 원본이 올 때까지
        if (peek.enabled && peek.texture == null) peek.enabled = false;
        if (spin.gameObject.activeSelf) spin.Rotate(0f, 0f, -SpinSpeed * Time.unscaledDeltaTime);
        Pinch();
    }

    // ── 맞춤 ───────────────────────────────────────────────
    private float Fit => FitOf(index);   // 화면에 다 들어오는 배율 (원본 1화소 = 캔버스 몇 단위)
    private float FitOf(int i) => i >= 0 && i < refs.Length ? Mathf.Min(area.x / refs[i].w, area.y / refs[i].h) : 1f;
    private float CanvasScale => rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;
    private bool ShareOpen => share != null && share.IsOpen;

    private Vector2 Limit(float sc)
    {
        return new Vector2(Mathf.Max(0f, (cur.w * sc - area.x) * 0.5f), Mathf.Max(0f, (cur.h * sc - area.y) * 0.5f));
    }

    private Vector2 Clamp(Vector2 p, float sc)
    {
        var l = Limit(sc);
        return new Vector2(Mathf.Clamp(p.x, -l.x, l.x), Mathf.Clamp(p.y, -l.y, l.y));
    }

    private void Refresh()
    {
        var rt = photo.rectTransform;
        rt.sizeDelta = new Vector2(cur.w, cur.h) * scale;
        rt.anchoredPosition = pos + new Vector2(pager, 0f);
    }

    // 옆으로 넘기는 동안 — 미는 쪽의 이웃 사진이 한 화면 옆에서 따라 들어온다
    private void RefreshPeek()
    {
        int j = pager < 0f ? index + 1 : pager > 0f ? index - 1 : -1;
        if (j < 0 || j >= refs.Length) { peek.enabled = false; return; }
        var t = Best(j);
        peek.texture = t;
        peek.enabled = t != null;
        var rt = peek.rectTransform;
        rt.sizeDelta = new Vector2(refs[j].w, refs[j].h) * FitOf(j);
        rt.anchoredPosition = new Vector2(pager + (pager < 0f ? 1f : -1f) * (area.x + PageGap), 0f);
    }

    private void Dim(float alpha)
    {
        var c = backdrop.color;
        c.a = alpha;
        backdrop.color = c;
    }

    // 손을 떼면 화면에 맞는 범위 안으로 돌아간다
    private void Release()
    {
        float ts = Mathf.Clamp(scale, Fit, Fit * MaxZoom);
        Vector2 tp = Clamp(pos * (ts / scale), ts);
        if (ts != scale || tp != pos) anim = StartCoroutine(Tween(ts, tp));
        else Dim(1f);
    }

    private IEnumerator Tween(float ts, Vector2 tp)
    {
        float s0 = scale, a0 = backdrop.color.a;
        Vector2 p0 = pos;
        for (float t = 0f; t < SettleTime; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / SettleTime, 3f);
            scale = Mathf.Lerp(s0, ts, k);
            pos = Vector2.Lerp(p0, tp, k);
            Dim(Mathf.Lerp(a0, 1f, k));
            Refresh();
            yield return null;
        }
        scale = ts;
        pos = tp;
        Dim(1f);
        Refresh();
        anim = null;
    }

    // dir +1 다음 · -1 앞 · 0 제자리. 다 넘어가면 이웃 사진이 지금 사진이 된다 (같은 자리 · 같은 크기라 바꿔 끼워도 티가 안 난다)
    private IEnumerator Turn(int dir)
    {
        turning = true;
        float p0 = pager, target = dir == 0 ? 0f : -dir * (area.x + PageGap);
        for (float t = 0f; t < PageTime; t += Time.unscaledDeltaTime)
        {
            pager = Mathf.Lerp(p0, target, 1f - Mathf.Pow(1f - t / PageTime, 3f));
            Refresh();
            RefreshPeek();
            yield return null;
        }
        anim = null;
        turning = false;
        peek.enabled = false;
        if (dir == 0)
        {
            pager = 0f;
            Refresh();
            yield break;
        }
        index += dir;
        failed.Remove(index);   // 미리 받다 못 받은 사진 — 이제 보니 다시 받아 본다
        ShowPage();
        Evict();
        Kick();
    }

    private void StopAnim()
    {
        if (anim == null) return;
        StopCoroutine(anim);
        anim = null;
        if (!turning) return;
        turning = false;   // 넘기다 멈췄다 (닫기) — 제자리
        pager = 0f;
        peek.enabled = false;
    }

    // ── 손가락 ─────────────────────────────────────────────
    // 아래 알약 · ✕ · 공유 시트에서 시작한 건 사진을 움직이지 않는다
    private bool OnStage(PointerEventData e)
    {
        var go = e.pointerPressRaycast.gameObject;
        return cur != null && !ShareOpen && go != null && !go.transform.IsChildOf(bar) && !go.transform.IsChildOf(closeButton);
    }

    private Vector2 Local(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screen, null, out var v);
        return v;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (pinching || turning || dragId != NoPointer || !OnStage(e)) return;
        dragId = e.pointerId;
        StopAnim();
        if (scale > Fit * 1.01f) drag = Drag.Pan;
        else
        {
            Vector2 d = e.position - e.pressPosition;   // 처음 민 쪽으로 정한다 — 옆이면 넘기기, 위아래면 끌어 닫기
            drag = refs.Length > 1 && Mathf.Abs(d.x) > Mathf.Abs(d.y) ? Drag.Page : Drag.Pull;
        }
        pageVel = 0f;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != dragId || pinching || cur == null) return;
        Vector2 d = e.delta / CanvasScale;
        switch (drag)
        {
            case Drag.Pull:
                pos.y += d.y;
                Dim(1f - Mathf.Clamp01(Mathf.Abs(pos.y) / PullFade) * 0.6f);
                break;
            case Drag.Page:
                float dx = d.x;
                bool outward = (index == 0 && dx > 0f && pager + dx > 0f) || (index == refs.Length - 1 && dx < 0f && pager + dx < 0f);
                if (outward) dx *= Resist;   // 첫 장 · 끝 장에서는 뻑뻑하게
                pager += dx;
                pageVel = Mathf.Lerp(pageVel, d.x / Mathf.Max(Time.unscaledDeltaTime, 1f / 120f), 0.6f);
                RefreshPeek();
                break;
            default:
                var l = Limit(scale);
                if ((pos.x > l.x && d.x > 0f) || (pos.x < -l.x && d.x < 0f)) d.x *= Resist;   // 사진 끝을 넘으면 뻑뻑하게
                if ((pos.y > l.y && d.y > 0f) || (pos.y < -l.y && d.y < 0f)) d.y *= Resist;
                pos += d;
                break;
        }
        Refresh();
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (e.pointerId != dragId) return;
        dragId = NoPointer;
        var was = drag;
        drag = Drag.None;
        if (was == Drag.Pull && Mathf.Abs(pos.y) > PullClose) { Close(); return; }
        if (was == Drag.Page)
        {
            float turnAt = area.x * PageTurn;
            int dir = 0;
            if ((pager < -turnAt || (pageVel < -PageFlick && pager < -PageMin)) && index < refs.Length - 1) dir = 1;
            else if ((pager > turnAt || (pageVel > PageFlick && pager > PageMin)) && index > 0) dir = -1;
            anim = StartCoroutine(Turn(dir));
            return;
        }
        if (!pinching && cur != null) Release();
    }

    public void OnScroll(PointerEventData e)
    {
        if (cur == null || ShareOpen || turning) return;
        StopAnim();
        // 휠 한 칸 = 15% — 입력 모듈은 한 칸을 scrollDeltaPerTick 로 보낸다
        float ticks = e.scrollDelta.y;
        if (EventSystem.current != null && EventSystem.current.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule m0 && Mathf.Abs(m0.scrollDeltaPerTick) > 0.0001f)
            ticks /= m0.scrollDeltaPerTick;
        ZoomAt(Mathf.Clamp(scale * Mathf.Pow(1.15f, ticks), Fit, Fit * MaxZoom), Local(e.position));
    }

    private void ZoomAt(float ns, Vector2 at)
    {
        pos = Clamp(at + (pos - at) * (ns / scale), ns);
        scale = ns;
        Refresh();
    }

    // 두 번 톡 — 맞춤이면 그 자리를 크게, 키워 둔 상태면 다시 맞춤
    public void OnPointerClick(PointerEventData e)
    {
        if (e.clickCount != 2 || e.dragging || turning || !OnStage(e)) return;
        StopAnim();
        float fit = Fit;
        if (scale > fit * 1.4f) { anim = StartCoroutine(Tween(fit, Vector2.zero)); return; }
        float ts = fit * 2.5f;
        Vector2 m = Local(e.position);
        anim = StartCoroutine(Tween(ts, Clamp(m + (pos - m) * (ts / scale), ts)));
    }

    private void Pinch()
    {
        Vector2 a = default, b = default;
        int n = ShareOpen || turning ? 0 : R0926PhotoCrop.Touches(out a, out b);
        if (n < 2)
        {
            if (pinching)
            {
                pinching = false;
                if (dragId == NoPointer) Release();
            }
            return;
        }
        float dist = Vector2.Distance(a, b);
        Vector2 mid = (a + b) * 0.5f;
        if (!pinching)
        {
            if (OnControls(a) || OnControls(b)) return;
            pinching = true;
            if (drag == Drag.Page || drag == Drag.Pull)   // 넘기기 · 끌어 닫기를 하다 손가락이 하나 더 — 키우기로
            {
                drag = Drag.Pan;
                pager = 0f;
                peek.enabled = false;
            }
            StopAnim();
        }
        else if (prevDist > 1f && dist > 1f)
        {
            float k = dist / prevDist;
            float lo = Fit, hi = Fit * MaxZoom;
            if ((scale < lo && k < 1f) || (scale > hi && k > 1f)) k = Mathf.Pow(k, Resist);   // 끝에서는 뻑뻑하게
            float ns = Mathf.Clamp(scale * k, lo * 0.5f, hi * 1.5f);
            Vector2 m = Local(mid), pm = Local(prevMid);
            pos = m + (pos - pm) * (ns / scale);   // 두 손가락 사이에 있던 곳이 손가락을 따라오게
            scale = ns;
            Refresh();
        }
        prevDist = dist;
        prevMid = mid;
    }

    private bool OnControls(Vector2 screen)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(bar, screen, null) || RectTransformUtility.RectangleContainsScreenPoint(closeButton, screen, null);
    }

    // ── 배치 (세로 · 가로 · 안전 영역) ──────────────────────
    private void Relayout()
    {
        float before = Fit;
        Layout();
        float after = Fit;
        scale = Mathf.Clamp(scale * (before > 0f ? after / before : 1f), after, after * MaxZoom);
        pos = Clamp(pos, scale);
        Refresh();
    }

    // 위: 왼쪽 ✕ · 가운데 「2 / 5」. 아래: 가운데 알약. 사진은 그 사이 — 가로 화면도 같다(여백만 좁게)
    private void Layout()
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        laidFor = size;
        laidSafe = Screen.safeArea;
        if (size.x <= 1f || size.y <= 1f) return;
        float s = CanvasScale;
        Rect sa = laidSafe;
        float insT = Mathf.Max(0f, Screen.height - sa.yMax) / s, insB = Mathf.Max(0f, sa.yMin) / s;
        float insL = Mathf.Max(0f, sa.xMin) / s, insR = Mathf.Max(0f, Screen.width - sa.xMax) / s;
        bool land = size.x > size.y;
        float top = insT + (land ? 16f : TopGap), bottom = insB + (land ? 24f : BarBottom), gap = land ? 16f : Gap;

        R0926PhotoCrop.Stretch(closeButton, new Vector2(0, 1), new Vector2(0, 1), new Vector2(insL + Edge, -top - CloseSize), new Vector2(insL + Edge + CloseSize, -top));
        if (counter != null && counter.gameObject.activeSelf)
        {
            float w = Mathf.Max(CounterH * 1.6f, Mathf.Ceil(counterText.preferredWidth) + CounterPad * 2f), cy = top + CloseSize * 0.5f;
            R0926PhotoCrop.Stretch(counter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-w * 0.5f, -cy - CounterH * 0.5f), new Vector2(w * 0.5f, -cy + CounterH * 0.5f));
        }

        // 알약 — 보이는 칸만 글자 폭대로. 화면보다 넓으면(긴 언어 · 좁은 화면) 글자를 빼고 그림만
        laidBar = BarWidth(true);
        bool words = laidBar <= size.x - insL - insR - Edge * 2f;
        float total = BarWidth(words), x = BarPad;
        foreach (var b in buttons)
        {
            if (b == null || !b.gameObject.activeSelf) continue;
            float w = ButtonWidth(b, words);
            R0926PhotoCrop.Stretch(b, new Vector2(0, 0), new Vector2(0, 1), new Vector2(x, BarPad), new Vector2(x + w, -BarPad));
            if (b.Find("Icon0926") is RectTransform icon)
            {
                icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(words ? 0f : 0.5f, 0.5f);
                icon.anchoredPosition = new Vector2(words ? BtnPad : 0f, 0f);
                icon.sizeDelta = new Vector2(IconSize, IconSize);
            }
            if (b.Find("Label0926") is RectTransform label)
            {
                label.gameObject.SetActive(words);
                float lx = BtnPad + IconSize + IconGap;
                R0926PhotoCrop.Stretch(label, new Vector2(0, 0), new Vector2(0, 1), new Vector2(lx, 0f), new Vector2(lx + LabelWidth(b), 0f));
            }
            x += w;
        }
        R0926PhotoCrop.Stretch(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-total * 0.5f, bottom), new Vector2(total * 0.5f, bottom + BarH));

        Rect r = Rect.MinMaxRect(insL, bottom + BarH + gap, size.x - insR, size.y - top - CloseSize - gap);
        area = r.size;
        frame.anchorMin = frame.anchorMax = Vector2.zero;
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = r.center;
        frame.sizeDelta = r.size;
    }

    private static float LabelWidth(RectTransform b)
    {
        var t = b.Find("Label0926")?.GetComponent<Text>();
        return t != null ? Mathf.Ceil(t.preferredWidth) : 0f;
    }

    private static float ButtonWidth(RectTransform b, bool words) => words ? BtnPad * 2f + IconSize + IconGap + LabelWidth(b) : BarH - BarPad * 2f;

    private float BarWidth(bool words)
    {
        float w = BarPad * 2f;
        foreach (var b in buttons)
            if (b != null && b.gameObject.activeSelf) w += ButtonWidth(b, words);
        return w;
    }
}
