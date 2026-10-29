using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 메인(대표) 사진 자르기 — 가운데 정사각형 틀은 그대로 두고 사진을 끌어 옮기고, 두 손가락으로 키우고 줄인다(두 번 톡 = 크게 / 다시 맞춤).
/// 아래 한 줄 큰 세 칸 '취소 / 회전 / 완료' — 사진 고르기 시트와 같은 모양 · 하단 메뉴와 같은 물방울 소리. 완료를 누르면 '자르는 중…'.
/// 10-20 대표님 「사진 크롭이 온전하지 않은데 버튼이랑 효과음 잘 정리해서 만들어볼래? 지금 껀 옛날에 어디서 따와서 쓴 건데 쓰기가 여간 불편」
/// — 예전 ImageCropper(yasirkula)는 작은 네모를 모서리 손잡이로 줄이고 옮기는 방식(사진 확대 없음)에 위쪽 작은 아이콘 버튼, 소리 없음이었다.
/// 장소 추가·장소 수정·프로필 사진이 같이 쓴다(프로필은 제목만 '프로필 사진'). 화면은 Redesign0926ApplyCrop 이 만들어 꺼 둔다
/// </summary>
public class R0926PhotoCrop : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
{
    [SerializeField] private RectTransform frame;      // 정사각형 틀 — 사진은 이 틀의 자식(가운데 기준)
    [SerializeField] private RawImage photo;
    [SerializeField] private CanvasGroup grid;         // 만지는 동안만 보이는 3x3 줄
    [SerializeField] private RectTransform header;     // 제목 + 안내
    [SerializeField] private R0926LocalizedText title;
    [SerializeField] private RectTransform bar;        // 취소 · 회전 · 완료 카드
    [SerializeField] private RectTransform[] tiles = new RectTransform[0];   // 카드 안 칸 (왼쪽부터 — 가로 화면에선 위부터)
    [SerializeField] private R0926Busy busy;

    // 캔버스 단위 (1pt ≈ 3.7)
    private const float Side = 60f, Top = 24f, HeaderH = 230f, Gap = 40f;
    private const float BarH = 348f, BarBottom = 40f, BarSide = 24f, BarW = 420f, TileGap = 20f;
    private const float MaxZoom = 6f, Resist = 0.35f, SettleTime = 0.2f, MinBusy = 0.35f;
    private const int MaxOut = 2048;
    private const int NoPointer = int.MinValue;
    private static readonly string[] MainTitle = { "메인 사진", "Main photo", "メイン写真", "主照片", "Foto principal" };

    private Texture2D tex;
    private Action<Texture2D> onDone;
    private Action onCancel;
    private int rot;              // 왼쪽으로 90° 돌린 횟수 (0~3)
    private float angle;          // 지금 보이는 각도 — 돌리는 동안 따라간다
    private float scale = 1f;     // 원본 1화소 = 캔버스 몇 단위
    private Vector2 pos;          // 틀 가운데 → 사진 가운데
    private Coroutine anim;
    private int dragId = NoPointer;
    private bool pinching, cropping;
    private float prevDist, gridTarget;
    private Vector2 prevMid, laidFor;
    private Rect laidSafe;
    private Canvas rootCanvas;

    public bool IsOpen => gameObject.activeSelf;

    /// <param name="done">자른 사진 (실패하면 null)</param>
    /// <param name="cancel">취소 — 고른 사진은 넣지 않는다</param>
    public void Open(Texture2D texture, Action<Texture2D> done, Action cancel) => Open(texture, done, cancel, null);

    /// <param name="titleWords">제목 (ko·en·ja·zh·es) — 없으면 '메인 사진'</param>
    public void Open(Texture2D texture, Action<Texture2D> done, Action cancel, string[] titleWords)
    {
        var w = titleWords ?? MainTitle;
        if (title != null) title.Set(w[0], w[1], w[2], w[3], w[4]);
        tex = texture;
        onDone = done;
        onCancel = cancel;
        rot = 0; angle = 0f; pos = Vector2.zero;
        dragId = NoPointer; pinching = false; cropping = false;
        gridTarget = 0f;
        if (grid != null) grid.alpha = 0f;
        gameObject.SetActive(true);
        photo.texture = tex;
        Layout();
        scale = Cover;
        Refresh();
    }

    public void Cancel()
    {
        if (cropping || tex == null) return;
        var cb = onCancel;
        Close();
        cb?.Invoke();
    }

    public void Rotate()
    {
        if (cropping || tex == null) return;
        StopAnim();
        rot = (rot + 1) & 3;
        anim = StartCoroutine(Tween(Cover, Vector2.zero, angle + 90f));   // 돌린 모양에 다시 꽉 차게
    }

    public void Done()
    {
        if (cropping || tex == null) return;
        StartCoroutine(Crop());
    }

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>(true);
        rootCanvas = c != null ? c.rootCanvas : null;
    }

    private void Update()
    {
        if (tex == null) return;
        if (laidFor != ((RectTransform)transform).rect.size || laidSafe != Screen.safeArea) Relayout();
        Pinch();
        if (grid != null && grid.alpha != gridTarget) grid.alpha = Mathf.MoveTowards(grid.alpha, gridTarget, Time.unscaledDeltaTime * 5f);
    }

    // ── 자르기 ─────────────────────────────────────────────
    private IEnumerator Crop()
    {
        cropping = true;
        StopAnim();
        scale = Mathf.Clamp(scale, Cover, Cover * MaxZoom);   // 틀 밖으로 밀려 있던 건 맞춰서
        pos = Clamp(pos, scale);
        Refresh();
        if (busy != null) busy.Show(R0926LocalizedText.Say("자르는 중…", "Cropping…", "切り抜き中…", "裁剪中…", "Recortando…"));
        yield return null;   // 안내가 먼저 그려지게 — 자르기(ReadPixels)는 그동안 화면을 멈춘다
        float t0 = Time.unscaledTime;
        Texture2D result = null;
        try { result = CropNow(); }
        catch (Exception e) { Debug.LogException(e); }
        while (Time.unscaledTime - t0 < MinBusy) yield return null;   // 금방 끝나도 안내가 깜빡이고 사라지지 않게
        var cb = onDone;
        cropping = false;
        try { cb?.Invoke(result); }
        finally
        {
            Close();
            if (busy != null) busy.Hide();
        }
    }

    // 틀 안에 보이는 그대로 — 원본에서 틀 자리를 잘라(GPU) 읽고, 돌려 놓았으면 화소를 돌린다
    private Texture2D CropNow()
    {
        float side = F / scale;   // 틀 한 변 = 원본 몇 화소
        Vector2 c = Turn(-pos / scale, -rot) + new Vector2(tex.width, tex.height) * 0.5f;
        var r = new Rect(c.x - side * 0.5f, c.y - side * 0.5f, side, side);
        r.x = Mathf.Clamp(r.x, 0f, Mathf.Max(0f, tex.width - side));
        r.y = Mathf.Clamp(r.y, 0f, Mathf.Max(0f, tex.height - side));
        int n = Mathf.Clamp(Mathf.RoundToInt(side), 1, MaxOut);

        var wrap = tex.wrapMode;
        tex.wrapMode = TextureWrapMode.Clamp;   // 가장자리에 반대쪽 화소가 번지지 않게
        var rt = RenderTexture.GetTemporary(n, n, 0);
        var prev = RenderTexture.active;
        Texture2D result;
        try
        {
            Graphics.Blit(tex, rt, new Vector2(r.width / tex.width, r.height / tex.height), new Vector2(r.x / tex.width, r.y / tex.height));
            RenderTexture.active = rt;
            result = new Texture2D(n, n, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, n, n), 0, 0, false);
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            tex.wrapMode = wrap;
        }
        if (rot != 0) TurnPixels(result, rot);
        result.Apply(false, true);   // 예전 자르기처럼 읽기 전용 — 올릴 땐 RenderTexture 로 줄여 쓴다
        return result;
    }

    // 정사각형 화소를 왼쪽으로 90° × turns
    private static void TurnPixels(Texture2D t, int turns)
    {
        int n = t.width;
        var src = t.GetPixels32();
        var dst = new Color32[src.Length];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int nx, ny;
                switch (turns)
                {
                    case 1: nx = n - 1 - y; ny = x; break;
                    case 2: nx = n - 1 - x; ny = n - 1 - y; break;
                    default: nx = y; ny = n - 1 - x; break;
                }
                dst[ny * n + nx] = src[y * n + x];
            }
        t.SetPixels32(dst);
    }

    private static Vector2 Turn(Vector2 v, int turns)
    {
        switch (turns & 3)
        {
            case 1: return new Vector2(-v.y, v.x);
            case 2: return -v;
            case 3: return new Vector2(v.y, -v.x);
            default: return v;
        }
    }

    private void Close()
    {
        StopAnim();
        photo.texture = null;
        tex = null;
        onDone = null;
        onCancel = null;
        dragId = NoPointer;
        pinching = false;
        gameObject.SetActive(false);
    }

    // ── 맞춤 ───────────────────────────────────────────────
    private float F => frame.rect.width;
    private Vector2 Disp => (rot & 1) == 0 ? new Vector2(tex.width, tex.height) : new Vector2(tex.height, tex.width);   // 돌린 뒤 가로·세로 (원본 화소)
    private float Cover => Mathf.Max(F / Disp.x, F / Disp.y);   // 틀을 빈틈없이 덮는 가장 작은 배율
    private float CanvasScale => rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;

    private Vector2 Limit(float sc)
    {
        var d = Disp * sc;
        return new Vector2(Mathf.Max(0f, (d.x - F) * 0.5f), Mathf.Max(0f, (d.y - F) * 0.5f));
    }

    private Vector2 Clamp(Vector2 p, float sc)
    {
        var l = Limit(sc);
        return new Vector2(Mathf.Clamp(p.x, -l.x, l.x), Mathf.Clamp(p.y, -l.y, l.y));
    }

    private void Refresh()
    {
        var rt = photo.rectTransform;
        rt.sizeDelta = new Vector2(tex.width, tex.height) * scale;
        rt.anchoredPosition = pos;
        rt.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    // 손을 떼면 틀을 덮는 범위 안으로 돌아간다
    private void Release()
    {
        gridTarget = 0f;
        float ts = Mathf.Clamp(scale, Cover, Cover * MaxZoom);
        Vector2 tp = Clamp(pos * (ts / scale), ts);
        if (ts != scale || tp != pos) anim = StartCoroutine(Tween(ts, tp, angle));
    }

    private IEnumerator Tween(float ts, Vector2 tp, float ta)
    {
        float s0 = scale, a0 = angle;
        Vector2 p0 = pos;
        for (float t = 0f; t < SettleTime; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / SettleTime, 3f);
            scale = Mathf.Lerp(s0, ts, k);
            pos = Vector2.Lerp(p0, tp, k);
            angle = Mathf.Lerp(a0, ta, k);
            Refresh();
            yield return null;
        }
        scale = ts; pos = tp; angle = Mathf.Repeat(ta, 360f);
        Refresh();
        anim = null;
    }

    private void StopAnim()
    {
        if (anim != null) { StopCoroutine(anim); anim = null; }
        angle = 90f * rot;   // 돌리던 중이면 끝 각도로
    }

    // ── 손가락 ─────────────────────────────────────────────
    // 아래 카드(버튼 칸 사이 포함)에서 시작한 건 사진을 움직이지 않는다
    private bool OnStage(PointerEventData e)
    {
        var go = e.pointerPressRaycast.gameObject;
        return go != null && (bar == null || !go.transform.IsChildOf(bar));
    }

    private Vector2 Local(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screen, null, out var v);
        return v;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (cropping || tex == null || !OnStage(e)) return;
        StopAnim();
        gridTarget = 1f;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (dragId == NoPointer && !pinching) gridTarget = 0f;   // 끌지 않고 뗐다
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (cropping || tex == null || pinching || dragId != NoPointer || !OnStage(e)) return;
        dragId = e.pointerId;
        StopAnim();
        gridTarget = 1f;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != dragId || pinching || tex == null) return;
        Vector2 d = e.delta / CanvasScale;
        var l = Limit(scale);
        if ((pos.x > l.x && d.x > 0f) || (pos.x < -l.x && d.x < 0f)) d.x *= Resist;   // 틀 밖으로는 뻑뻑하게
        if ((pos.y > l.y && d.y > 0f) || (pos.y < -l.y && d.y < 0f)) d.y *= Resist;
        pos += d;
        Refresh();
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (e.pointerId != dragId) return;
        dragId = NoPointer;
        if (!pinching && tex != null && !cropping) Release();
    }

    public void OnScroll(PointerEventData e)
    {
        if (cropping || tex == null) return;
        StopAnim();
        // 휠 한 칸 = 15% — 입력 모듈은 한 칸을 scrollDeltaPerTick(장면 6) 로 보낸다
        float ticks = e.scrollDelta.y;
        if (EventSystem.current != null && EventSystem.current.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule m0 && Mathf.Abs(m0.scrollDeltaPerTick) > 0.0001f)
            ticks /= m0.scrollDeltaPerTick;
        float ns = Mathf.Clamp(scale * Mathf.Pow(1.15f, ticks), Cover, Cover * MaxZoom);
        Vector2 m = Local(e.position);
        pos = Clamp(m + (pos - m) * (ns / scale), ns);
        scale = ns;
        Refresh();
    }

    // 두 번 톡 — 맞춤이면 그 자리를 크게, 키워 둔 상태면 다시 맞춤
    public void OnPointerClick(PointerEventData e)
    {
        if (cropping || tex == null || e.clickCount != 2 || e.dragging || !OnStage(e)) return;
        StopAnim();
        float cover = Cover;
        if (scale > cover * 1.4f) { anim = StartCoroutine(Tween(cover, Vector2.zero, angle)); return; }
        float ts = cover * 2.2f;
        Vector2 m = Local(e.position);
        anim = StartCoroutine(Tween(ts, Clamp(m + (pos - m) * (ts / scale), ts), angle));
    }

    // 새 입력 시스템만 켜져 있다 — 예전 Input.touchCount 는 기기에서 예외가 난다 (R0926FriendsMap 과 같게)
    private void Pinch()
    {
        if (cropping) return;
        int n = Touches(out Vector2 a, out Vector2 b);
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
            if (bar != null && (RectTransformUtility.RectangleContainsScreenPoint(bar, a, null) || RectTransformUtility.RectangleContainsScreenPoint(bar, b, null))) return;
            pinching = true;
            StopAnim();
            gridTarget = 1f;
        }
        else if (prevDist > 1f && dist > 1f)
        {
            float k = dist / prevDist;
            float lo = Cover, hi = Cover * MaxZoom;
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

    private static int Touches(out Vector2 a, out Vector2 b)
    {
        a = b = default;
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts == null) return 0;
        int n = 0;
        foreach (var t in ts.touches)
        {
            if (!t.isInProgress) continue;
            if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue();
            n++;
        }
        return n;
    }

    // ── 배치 (세로 · 가로 · 안전 영역) ──────────────────────
    private void Relayout()
    {
        float oldF = F;
        Layout();
        float k = oldF > 1f ? F / oldF : 1f;
        scale = Mathf.Clamp(scale * k, Cover, Cover * MaxZoom);
        pos = Clamp(pos * k, scale);
        Refresh();
    }

    // 세로: 위 제목 · 가운데 틀 · 아래 한 줄 세 칸. 가로: 오른쪽에 세 칸을 세로로, 왼쪽 남은 자리에 틀
    private void Layout()
    {
        var root = (RectTransform)transform;
        Vector2 size = root.rect.size;
        laidFor = size;
        laidSafe = Screen.safeArea;
        if (size.x <= 1f || size.y <= 1f) return;
        float s = CanvasScale;
        Rect sa = laidSafe;
        float insT = Mathf.Max(0f, Screen.height - sa.yMax) / s, insB = Mathf.Max(0f, sa.yMin) / s;
        float insL = Mathf.Max(0f, sa.xMin) / s, insR = Mathf.Max(0f, Screen.width - sa.xMax) / s;
        bool land = size.x > size.y;
        float headTop = insT + Top;
        Rect area;   // 틀이 들어갈 자리 (왼쪽 아래 기준)
        if (!land)
        {
            Stretch(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(insL + Side, -headTop - HeaderH), new Vector2(-insR - Side, -headTop));
            Stretch(bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(insL + BarSide, insB + BarBottom), new Vector2(-insR - BarSide, insB + BarBottom + BarH));
            float bottom = insB + BarBottom + BarH + Gap, top = size.y - headTop - HeaderH - Gap;
            area = Rect.MinMaxRect(insL + Side, bottom, size.x - insR - Side, top);
        }
        else
        {
            float right = insR + BarSide + BarW;
            Stretch(bar, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-right, insB + BarSide), new Vector2(-insR - BarSide, -insT - BarSide));
            Stretch(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(insL + Side, -headTop - HeaderH), new Vector2(-right - Gap, -headTop));
            area = Rect.MinMaxRect(insL + Side, insB + Gap, size.x - right - Gap, size.y - headTop - HeaderH - Gap * 0.5f);
        }
        // 칸 사이 간격이 같게 — i 번째 칸은 앞쪽을 i·g/n, 뒤쪽을 (n-1-i)·g/n 만큼 줄인다
        int n = tiles.Length;
        for (int i = 0; i < n; i++)
        {
            var t = tiles[i];
            if (t == null) continue;
            float a = i / (float)n, b = (i + 1) / (float)n;
            float lead = i * TileGap / n, trail = (n - 1 - i) * TileGap / n;
            if (!land) Stretch(t, new Vector2(a, 0), new Vector2(b, 1), new Vector2(lead, 0), new Vector2(-trail, 0));
            else Stretch(t, new Vector2(0, 1 - b), new Vector2(1, 1 - a), new Vector2(0, trail), new Vector2(0, -lead));
        }
        float side = Mathf.Max(200f, Mathf.Min(area.width, area.height));
        frame.anchorMin = frame.anchorMax = Vector2.zero;
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = area.center;
        frame.sizeDelta = new Vector2(side, side);
    }

    private static void Stretch(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        if (rt == null) return;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
    }
}
