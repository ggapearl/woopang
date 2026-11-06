using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 대화방 사진 크게 보기 — 말풍선의 사진을 누르면 뜬다. 10-30 대표님 「해당 사진 클릭해서 사진을 볼 수 있도록 하고 본 앱 레이아웃에 어울리는 다운로드, 공유버튼」.
/// 아래 한 줄 큰 세 칸 '닫기 / 다운로드 / 공유' — 사진 자르기 화면과 같은 카드 · 칸 · 물방울 소리.
/// 말풍선의 작은 사진을 바로 보여 주고 원본을 받아 바꿔 끼운다. 두 손가락·두 번 톡·휠로 키우고, 맞춤 크기에서 위아래로 끌면 닫힌다(뒤로가기도).
/// 대화방이 닫히면 같이 닫힌다(알림을 눌러 다른 방으로 옮길 때). 화면은 Redesign0926ApplyChatPhoto 가 만들어 꺼 둔다
/// 보물찾기 상품 사진(쿠폰·QR — 11-06)도 이 화면으로 연다(OpenPrize) — 대화방과 상관없이 · 공유(팔로잉에게 대화로) 칸은 빼고 닫기 · 다운로드 두 칸
/// </summary>
public class R0926PhotoView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
{
    [SerializeField] private RectTransform frame;      // 사진이 놓이는 자리 — 사진은 이 틀의 가운데 기준
    [SerializeField] private RawImage photo;
    [SerializeField] private Image backdrop;
    [SerializeField] private RectTransform bar;        // 닫기 · 다운로드 · 공유 카드
    [SerializeField] private RectTransform[] tiles = new RectTransform[0];   // 카드 안 칸 (왼쪽부터 — 가로 화면에선 위부터)
    [SerializeField] private RectTransform shareTile;  // 공유 칸 — 대화 사진만 (상품 사진은 뺀다)
    [SerializeField] private RectTransform spin;
    [SerializeField] private R0926PhotoShare share;
    [SerializeField] private GameObject chatRoom;

    // 캔버스 단위 — 자르기 화면(R0926PhotoCrop)과 같은 카드
    private const float BarH = 348f, BarBottom = 40f, BarSide = 24f, BarW = 420f, TileGap = 20f, Gap = 24f;
    private const float MaxZoom = 6f, Resist = 0.35f, SettleTime = 0.2f, PullClose = 260f, PullFade = 700f, SpinSpeed = 360f;
    private const int NoPointer = int.MinValue;

    private R0926ChatPhoto.Ref cur;
    private bool roomBound;                            // 대화방 사진 — 방이 닫히면 같이 닫는다
    private Texture2D full;
    private byte[] fullBytes;
    private bool loading, saving, saveAfterLoad, pinching, pulling;
    private float scale = 1f, prevDist;
    private Vector2 pos, prevMid, laidFor, area;
    private Rect laidSafe;
    private int dragId = NoPointer;
    private Coroutine anim;
    private Canvas rootCanvas;

#if UNITY_EDITOR
    public static byte[] EditorSaved;   // 플레이 시험 — 사진첩으로 넘긴 내용
#endif

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>(true);
        rootCanvas = c != null ? c.rootCanvas : null;
    }

    /// <param name="thumb">말풍선에 보이던 작은 사진 — 원본이 올 때까지 보여 준다 (없으면 도는 표시만)</param>
    public void Open(R0926ChatPhoto.Ref reference, Texture thumb) => Open(reference, thumb, true);

    /// <summary>보물찾기 상품 사진(쿠폰·QR — 11-06) — 받아 둔 사진 그대로 크게. 다운로드는 사진첩으로, 공유 칸은 뺀다</summary>
    public void OpenPrize(string url, Texture tex)
    {
        if (tex == null || string.IsNullOrEmpty(url)) return;
        Open(new R0926ChatPhoto.Ref { url = R0926HuntApi.Url(url), file = R0926HuntApi.PrizeFile(url), w = tex.width, h = tex.height }, tex, false);
    }

    private void Open(R0926ChatPhoto.Ref reference, Texture thumb, bool chat)
    {
        if (IsOpen) return;
        cur = reference;
        roomBound = chat;
        if (shareTile != null) shareTile.gameObject.SetActive(chat);
        gameObject.SetActive(true);
        photo.texture = thumb;
        photo.enabled = thumb != null;
        Dim(1f);
        pos = Vector2.zero;
        dragId = NoPointer;
        pinching = pulling = false;
        Layout();
        scale = Fit;
        Refresh();
        loading = true;
        spin.gameObject.SetActive(true);
        StartCoroutine(LoadFull());
    }

    public void Close()
    {
        if (cur == null) return;
        StopAnim();
        if (share != null) share.Close();
        photo.texture = null;
        if (full != null) Destroy(full);
        full = null;
        fullBytes = null;
        cur = null;
        loading = saveAfterLoad = false;
        gameObject.SetActive(false);
    }

    public void Download()
    {
        if (cur == null || saving) return;
        if (loading) saveAfterLoad = true;
        else Save();
    }

    public void Share()
    {
        if (cur != null && share != null) share.Open(cur.url);
    }

    private IEnumerator LoadFull()
    {
        byte[] bytes = null;
        yield return R0926ChatPhoto.Fetch(cur.url, cur.Key("f"), b => bytes = b);
        Texture2D t = R0926ChatPhoto.Decode(bytes);
        loading = false;
        spin.gameObject.SetActive(false);
        if (t == null)
        {
            saveAfterLoad = false;
            Toast(false, LoadFail);
            yield break;
        }
        full = t;
        fullBytes = bytes;
        photo.texture = t;
        photo.enabled = true;
        if (saveAfterLoad)
        {
            saveAfterLoad = false;
            Save();
        }
    }

    private static string LoadFail => R0926LocalizedText.Say("사진을 불러오지 못했어요", "Couldn't load photo", "写真を読み込めません", "无法加载照片", "No se pudo cargar la foto");
    private static string SaveFail => R0926LocalizedText.Say("사진을 저장하지 못했어요. 사진 접근 권한을 확인해 주세요", "Couldn't save the photo. Please check photo access", "写真を保存できませんでした。写真へのアクセスを確認してください", "无法保存照片，请检查照片权限", "No se pudo guardar la foto. Revisa el acceso a fotos");

    private void Save()
    {
        if (fullBytes == null) { Toast(false, LoadFail); return; }
        saving = true;
#if UNITY_EDITOR
        EditorSaved = fullBytes;
#endif
        try
        {
            string name = "Woopang_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
            NativeGallery.SaveImageToGallery(fullBytes, "WOOPANG", name, (ok, path) =>
            {
                saving = false;
                Toast(ok, ok ? R0926LocalizedText.Say("사진첩에 저장했어요", "Saved to your photos", "写真に保存しました", "已保存到相册", "Guardada en tus fotos") : SaveFail);
            });
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            saving = false;
            Toast(false, SaveFail);
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
        if (laidFor != ((RectTransform)transform).rect.size || laidSafe != Screen.safeArea) Relayout();
        if (bar.gameObject.activeSelf == ShareOpen) bar.gameObject.SetActive(!ShareOpen);   // 공유 시트 틈으로 세 칸이 비치지 않게
        if (photo.enabled && photo.texture == null) photo.enabled = false;   // 말풍선이 작은 사진을 내려놓았다 — 원본이 올 때까지
        if (loading) spin.Rotate(0f, 0f, -SpinSpeed * Time.unscaledDeltaTime);
        Pinch();
    }

    // ── 맞춤 ───────────────────────────────────────────────
    private float Fit => Mathf.Min(area.x / cur.w, area.y / cur.h);   // 화면에 다 들어오는 배율 (원본 1화소 = 캔버스 몇 단위)
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
        rt.anchoredPosition = pos;
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

    private void StopAnim()
    {
        if (anim != null) { StopCoroutine(anim); anim = null; }
    }

    // ── 손가락 ─────────────────────────────────────────────
    // 아래 카드(칸 사이 포함)와 공유 시트에서 시작한 건 사진을 움직이지 않는다
    private bool OnStage(PointerEventData e)
    {
        var go = e.pointerPressRaycast.gameObject;
        return cur != null && !ShareOpen && go != null && !go.transform.IsChildOf(bar);
    }

    private Vector2 Local(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screen, null, out var v);
        return v;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (pinching || dragId != NoPointer || !OnStage(e)) return;
        dragId = e.pointerId;
        StopAnim();
        pulling = scale <= Fit * 1.01f;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != dragId || pinching || cur == null) return;
        Vector2 d = e.delta / CanvasScale;
        if (pulling)
        {
            pos.y += d.y;
            Dim(1f - Mathf.Clamp01(Mathf.Abs(pos.y) / PullFade) * 0.6f);
        }
        else
        {
            var l = Limit(scale);
            if ((pos.x > l.x && d.x > 0f) || (pos.x < -l.x && d.x < 0f)) d.x *= Resist;   // 사진 끝을 넘으면 뻑뻑하게
            if ((pos.y > l.y && d.y > 0f) || (pos.y < -l.y && d.y < 0f)) d.y *= Resist;
            pos += d;
        }
        Refresh();
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (e.pointerId != dragId) return;
        dragId = NoPointer;
        bool closing = pulling && Mathf.Abs(pos.y) > PullClose;
        pulling = false;
        if (closing) Close();
        else if (!pinching && cur != null) Release();
    }

    public void OnScroll(PointerEventData e)
    {
        if (cur == null || ShareOpen) return;
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
        if (e.clickCount != 2 || e.dragging || !OnStage(e)) return;
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
        int n = ShareOpen ? 0 : R0926PhotoCrop.Touches(out a, out b);
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
            if (RectTransformUtility.RectangleContainsScreenPoint(bar, a, null) || RectTransformUtility.RectangleContainsScreenPoint(bar, b, null)) return;
            pinching = true;
            pulling = false;
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

    // 세로: 아래 한 줄 세 칸 · 위 남은 자리에 사진. 가로: 오른쪽에 세 칸을 세로로 · 왼쪽 남은 자리에 사진
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
        Rect r;
        if (!land)
        {
            R0926PhotoCrop.Stretch(bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(insL + BarSide, insB + BarBottom), new Vector2(-insR - BarSide, insB + BarBottom + BarH));
            r = Rect.MinMaxRect(insL, insB + BarBottom + BarH + Gap, size.x - insR, size.y - insT - Gap);
        }
        else
        {
            float right = insR + BarSide + BarW;
            R0926PhotoCrop.Stretch(bar, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-right, insB + BarSide), new Vector2(-insR - BarSide, -insT - BarSide));
            r = Rect.MinMaxRect(insL, insB + Gap, size.x - right - Gap, size.y - insT - Gap);
        }
        // 칸 사이 간격이 같게 — i 번째 칸은 앞쪽을 i·g/n, 뒤쪽을 (n-1-i)·g/n 만큼 줄인다 (꺼 둔 칸은 빼고 — 상품 사진은 두 칸)
        int n = 0;
        foreach (var t in tiles) if (t != null && t.gameObject.activeSelf) n++;
        int i = -1;
        foreach (var t in tiles)
        {
            if (t == null || !t.gameObject.activeSelf) continue;
            i++;
            float a = i / (float)n, b = (i + 1) / (float)n;
            float lead = i * TileGap / n, trail = (n - 1 - i) * TileGap / n;
            if (!land) R0926PhotoCrop.Stretch(t, new Vector2(a, 0), new Vector2(b, 1), new Vector2(lead, 0), new Vector2(-trail, 0));
            else R0926PhotoCrop.Stretch(t, new Vector2(0, 1 - b), new Vector2(1, 1 - a), new Vector2(0, trail), new Vector2(0, -lead));
        }
        area = r.size;
        frame.anchorMin = frame.anchorMax = Vector2.zero;
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = r.center;
        frame.sizeDelta = r.size;
    }
}
