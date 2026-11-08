using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사진 묶음 말풍선 — 11-09 대표님 「사진 여러개 보냈을 때 하나씩 가는데 묶어서 보내지고」.
/// 한 메시지에 사진 2~10장(주소가 줄마다 하나 — R0926ChatPhoto.TryParseAlbum). 글 말풍선 프리팹을 그대로 쓰고 글 대신 칸을 깐다.
/// 줄마다 두세 칸 — 앞 사진일수록 큰 칸: 2 · 3 · 2+2 · 2+3 · 3+3 · 2+2+3 · 2+3+3 · 3+3+3 · 2+2+3+3. 두 칸 줄은 조금 납작하게, 세 칸 줄은 정사각. 칸은 가운데를 잘라 채운다.
/// 작은 사진(512)은 화면 가까이 왔을 때 받고(대화방 전체에서 한 번에 셋까지 — 사진 말풍선과 같이 센다), 멀어지면 내려놓는다 — 다시 오면 기기에 받아 둔 것을 쓴다.
/// 한 번 누르면 누른 칸부터 크게 보기(옆으로 넘겨 다른 사진 — R0926PhotoView) · 두 번 누르면 글 말풍선처럼 좋아요. 못 받은 칸을 누르면 다시 받는다.
/// 내가 보내는 동안은 고른 사진을 줄인 사본을 칸마다 바로 보여 주고, 아직 안 올라간 칸은 어둡게 + 가운데 몇 % (R0926ChatPhoto.SendAlbum)
/// </summary>
public class R0926ChatAlbumBubble : MonoBehaviour
{
    public const float W = 760f, Gap = 6f, Row2H = 300f;
    private const float MaxH = 900f, CheckEvery = 0.2f, LoadWithin = 0.5f, DropBeyond = 3f;
    private const int TileLoads = 3;   // 한 묶음 안에서 한 번에 받는 칸
    private static readonly Color Placeholder = new Color(1f, 1f, 1f, 0.05f), TileBack = new Color(1f, 1f, 1f, 0.07f);
    private static readonly Vector3[] Mine = new Vector3[4], View = new Vector3[4];

    private sealed class Tile
    {
        public RectTransform rt;
        public RawImage img;
        public Image shade;
        public Text mark;
        public Texture2D tex;
        public bool failed;
    }

    private RectTransform bubble, grid;
    private LayoutElement gridSize;
    private readonly List<Tile> tiles = new List<Tile>();
    private R0926ChatPhoto.Ref[] refs;
    private R0926PhotoView view;
    private ScrollRect scroll;
    private Canvas canvas;
    private Text label, contentText;
    private float[] splits;
    private float aspect1 = 4f / 3f;   // 칸이 하나만 남았을 때(나머지를 못 읽음)의 비율
    private bool loading;
    private float nextCheck;

    public int Count => tiles.Count;

    /// <summary>줄마다 칸 수 — 앞 사진일수록 큰 칸</summary>
    public static int[] Rows(int n)
    {
        switch (n)
        {
            case 1: return new[] { 1 };
            case 2: return new[] { 2 };
            case 3: return new[] { 3 };
            case 4: return new[] { 2, 2 };
            case 5: return new[] { 2, 3 };
            case 6: return new[] { 3, 3 };
            case 7: return new[] { 2, 2, 3 };
            case 8: return new[] { 2, 3, 3 };
            case 9: return new[] { 3, 3, 3 };
            default: return new[] { 2, 2, 3, 3 };
        }
    }

    public static R0926ChatAlbumBubble Attach(GameObject item, RectTransform bubble, Text content, R0926ChatPhoto.Ref[] refs, R0926PhotoView view)
    {
        var ui = item.AddComponent<R0926ChatAlbumBubble>();
        ui.Build(bubble, content, refs.Length);
        ui.refs = refs;
        ui.view = view;
        return ui;
    }

    /// <summary>내가 고른 사진 N장 — 칸은 R0926ChatPhoto 가 사진을 읽는 대로 PutLocal 로 채운다</summary>
    public static R0926ChatAlbumBubble AttachPending(GameObject item, RectTransform bubble, Text content, int count, R0926PhotoView view)
    {
        var ui = item.AddComponent<R0926ChatAlbumBubble>();
        ui.Build(bubble, content, count);
        ui.view = view;
        ui.Progress(0f);
        return ui;
    }

    private void Build(RectTransform rt, Text content, int count)
    {
        bubble = rt;
        contentText = content;
        scroll = GetComponentInParent<ScrollRect>(true);
        canvas = GetComponentInParent<Canvas>(true);
        content.gameObject.SetActive(false);
        R0926ChatPhotoBubble.StopTextSizing(rt);

        var vlg = rt.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset();
        vlg.spacing = 0f;
        rt.GetComponent<Image>().color = Placeholder;   // 칸 사이 틈 — 분홍 말풍선이 비치지 않게
        rt.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        var g = new GameObject("Album0926", typeof(RectTransform), typeof(LayoutElement));
        g.layer = gameObject.layer;
        g.transform.SetParent(rt, false);
        grid = (RectTransform)g.transform;
        gridSize = g.GetComponent<LayoutElement>();

        for (int i = 0; i < count; i++) tiles.Add(NewTile(i));

        var t = new GameObject("Label0926", typeof(RectTransform), typeof(Text), typeof(Shadow));
        t.layer = gameObject.layer;
        t.transform.SetParent(grid, false);
        var lrt = (RectTransform)t.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(24f, 0f);
        lrt.offsetMax = new Vector2(-24f, 0f);
        label = t.GetComponent<Text>();
        label.font = content.font;
        label.fontSize = 46;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        t.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        t.SetActive(false);
        Layout();
    }

    private Tile NewTile(int i)
    {
        var go = new GameObject("Tile0926_" + i, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(grid, false);
        var back = go.GetComponent<Image>();
        back.color = TileBack;
        back.raycastTarget = false;
        var tile = new Tile { rt = (RectTransform)go.transform };

        var p = new GameObject("Photo0926", typeof(RectTransform), typeof(RawImage));
        p.layer = go.layer;
        p.transform.SetParent(go.transform, false);
        Fill((RectTransform)p.transform);
        tile.img = p.GetComponent<RawImage>();
        tile.img.raycastTarget = false;
        tile.img.enabled = false;

        var s = new GameObject("Shade0926", typeof(RectTransform), typeof(Image));
        s.layer = go.layer;
        s.transform.SetParent(go.transform, false);
        Fill((RectTransform)s.transform);
        tile.shade = s.GetComponent<Image>();
        tile.shade.color = new Color(0f, 0f, 0f, 0.45f);
        tile.shade.raycastTarget = false;
        s.SetActive(false);

        var m = new GameObject("Mark0926", typeof(RectTransform), typeof(Text));
        m.layer = go.layer;
        m.transform.SetParent(go.transform, false);
        Fill((RectTransform)m.transform);
        tile.mark = m.GetComponent<Text>();
        tile.mark.font = contentText.font;
        tile.mark.fontSize = 64;
        tile.mark.color = new Color(1f, 1f, 1f, 0.7f);
        tile.mark.alignment = TextAnchor.MiddleCenter;
        tile.mark.raycastTarget = false;
        tile.mark.text = "!";   // 앱 글꼴에 ↻ 가 없다
        m.SetActive(false);
        return tile;
    }

    private static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // 칸 자리 — 왼쪽 위부터 줄마다. 하나만 남으면 그 사진 비율 그대로(사진 말풍선처럼)
    private void Layout()
    {
        int n = tiles.Count;
        float width = W, height = 0f;
        if (n == 1)
        {
            float a = Mathf.Clamp(aspect1, 0.5f, 2f);
            width = Mathf.Min(W, MaxH * a);
            height = width / a;
            Place(tiles[0], 0f, 0f, width, height);
        }
        else if (n > 1)
        {
            int k = 0;
            foreach (int c in Rows(n))
            {
                float tw = (W - Gap * (c - 1)) / c, th = c == 2 ? Row2H : tw;
                for (int i = 0; i < c && k < n; i++, k++) Place(tiles[k], i * (tw + Gap), height, tw, th);
                height += th + Gap;
            }
            height -= Gap;
        }
        gridSize.minWidth = gridSize.preferredWidth = width;
        gridSize.minHeight = gridSize.preferredHeight = height;
        foreach (var t in tiles) Crop(t);
        if (bubble != null) LayoutRebuilder.MarkLayoutForRebuild(bubble);
    }

    private static void Place(Tile t, float x, float y, float w, float h)
    {
        t.rt.anchorMin = t.rt.anchorMax = new Vector2(0f, 1f);
        t.rt.pivot = new Vector2(0f, 1f);
        t.rt.anchoredPosition = new Vector2(x, -y);
        t.rt.sizeDelta = new Vector2(w, h);
    }

    // 칸 비율을 벗어난 부분은 가운데를 잘라 보여 준다
    private static void Crop(Tile t)
    {
        if (t.tex == null) return;
        Vector2 box = t.rt.sizeDelta;
        if (box.x <= 0f || box.y <= 0f) return;
        float ta = t.tex.width / (float)t.tex.height, ba = box.x / box.y;
        t.img.uvRect = ta > ba ? new Rect((1f - ba / ta) * 0.5f, 0f, ba / ta, 1f) : new Rect(0f, (1f - ta / ba) * 0.5f, 1f, ta / ba);
    }

    private void Put(Tile t, Texture2D tex)
    {
        if (t.tex != null && t.tex != tex) Destroy(t.tex);
        t.tex = tex;
        t.failed = false;
        t.mark.gameObject.SetActive(false);
        t.img.texture = tex;
        t.img.enabled = tex != null;
        if (tiles.Count == 1 && tex != null && Mathf.Abs(aspect1 - tex.width / (float)tex.height) > 0.01f)
        {
            aspect1 = tex.width / (float)tex.height;
            Layout();
        }
        else Crop(t);
    }

    // ── 보내는 동안 (R0926ChatPhoto.SendAlbum) ──────────────
    /// <summary>slot 번째 칸에 고른 사진을 줄인 사본 — 칸이 가져간다 (말풍선이 없어질 때 같이 지운다)</summary>
    public void PutLocal(int slot, Texture2D small)
    {
        if (slot < 0 || slot >= tiles.Count) { Destroy(small); return; }
        Put(tiles[slot], small);
        tiles[slot].shade.gameObject.SetActive(true);
    }

    /// <summary>읽지 못한 사진 — 그 칸을 빼고 다시 놓는다</summary>
    public void RemoveSlot(int slot)
    {
        if (slot < 0 || slot >= tiles.Count) return;
        var t = tiles[slot];
        tiles.RemoveAt(slot);
        if (t.tex != null) Destroy(t.tex);
        Destroy(t.rt.gameObject);
        for (int i = 0; i < tiles.Count; i++) tiles[i].rt.name = "Tile0926_" + i;
        Layout();
    }

    /// <summary>사진마다 다 올라가는 몫 (0~1, 차례대로) — 그 몫을 넘으면 그 칸의 그늘을 걷는다</summary>
    public void Splits(float[] s) => splits = s;

    public void Progress(float p)
    {
        label.gameObject.SetActive(true);
        label.transform.SetAsLastSibling();
        label.text = Mathf.RoundToInt(p * 100f) + "%";
        for (int i = 0; i < tiles.Count; i++)
            tiles[i].shade.gameObject.SetActive(splits == null || i >= splits.Length || p < splits[i]);
    }

    /// <summary>올리기 끝 — 이제 서버의 사진이다. 칸의 작은 사본은 그대로 쓴다(이미 작다)</summary>
    public void Sent(R0926ChatPhoto.Ref[] sent)
    {
        refs = sent;
        label.gameObject.SetActive(false);
        foreach (var t in tiles) t.shade.gameObject.SetActive(false);
        while (tiles.Count > sent.Length) RemoveSlot(tiles.Count - 1);
        while (tiles.Count < sent.Length) tiles.Add(NewTile(tiles.Count));
        if (tiles.Count == 1) aspect1 = sent[0].w / (float)sent[0].h;
        Layout();
        nextCheck = 0f;
    }

    // ── 누르기 ─────────────────────────────────────────────
    public void Tap()
    {
        if (refs == null) return;   // 아직 올리는 중
        var tap = GetComponent<DoubleTouchLikeHandler>();
        int i = TileAt(tap != null ? tap.LastTap : Vector2.zero);
        if (i < 0) return;
        if (tiles[i].failed || label.gameObject.activeSelf)   // 못 받은 칸 — 다시 받는다
        {
            foreach (var t in tiles)
            {
                t.failed = false;
                t.mark.gameObject.SetActive(false);
            }
            label.gameObject.SetActive(false);
            nextCheck = 0f;
            return;
        }
        if (view != null) view.OpenAlbum(refs, i, Thumbs());
    }

    /// <summary>화면의 이 자리에 있는 칸 — 틈을 눌렀으면 가장 가까운 칸</summary>
    public int TileAt(Vector2 screen)
    {
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < tiles.Count; i++)
        {
            var rt = tiles[i].rt;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screen, cam)) return i;
            float d = (RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center)) - screen).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    public Texture[] Thumbs()
    {
        var a = new Texture[tiles.Count];
        for (int i = 0; i < a.Length; i++) a[i] = tiles[i].tex;
        return a;
    }

    // ── 받기 · 내려놓기 ─────────────────────────────────────
    private void Update()
    {
        if (refs == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckEvery;
        if (!Near(DropBeyond)) { Drop(); return; }
        if (!loading && Missing() && R0926ChatPhotoBubble.LoadingNow < R0926ChatPhotoBubble.MaxLoads && Near(LoadWithin)) StartCoroutine(Load());
    }

    private bool Missing()
    {
        foreach (var t in tiles) if (t.tex == null && !t.failed) return true;
        return false;
    }

    private bool Near(float screens)
    {
        if (scroll == null) return true;
        var frame = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        bubble.GetWorldCorners(Mine);
        frame.GetWorldCorners(View);
        float reach = (View[1].y - View[0].y) * screens;
        return Mine[0].y < View[1].y + reach && Mine[1].y > View[0].y - reach;
    }

    private IEnumerator Load()
    {
        loading = true;
        R0926ChatPhotoBubble.LoadingNow++;
        int running = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].tex != null || tiles[i].failed) continue;
            while (running >= TileLoads) yield return null;
            running++;
            StartCoroutine(LoadTile(tiles[i], refs[i], () => running--));
        }
        while (running > 0) yield return null;
        loading = false;
        R0926ChatPhotoBubble.LoadingNow--;

        bool any = false;
        foreach (var t in tiles) any |= t.tex != null;
        if (!any)
        {
            label.text = R0926LocalizedText.Say("사진을 불러오지 못했어요", "Couldn't load photos", "写真を読み込めません", "无法加载照片", "No se pudieron cargar las fotos");
            label.gameObject.SetActive(true);
            label.transform.SetAsLastSibling();
            foreach (var t in tiles) t.mark.gameObject.SetActive(false);
        }
    }

    private IEnumerator LoadTile(Tile t, R0926ChatPhoto.Ref r, Action done)
    {
        byte[] bytes = null;
        yield return R0926ChatPhoto.Fetch(r.ThumbOf(R0926ChatPhoto.TileSize), r.Key("s"), b => bytes = b);
        Texture2D got = R0926ChatPhoto.Decode(bytes);
        if (got == null)   // 작은 사진을 못 만들었으면 원본을 받아 줄인다
        {
            yield return R0926ChatPhoto.Fetch(r.url, r.Key("f"), b => bytes = b);
            Texture2D big = R0926ChatPhoto.Decode(bytes);
            if (big != null)
            {
                got = R0926ChatPhoto.Shrink(big, R0926ChatPhoto.TileSize);
                Destroy(big);
            }
        }
        if (t.rt == null) { if (got != null) Destroy(got); }   // 그사이 칸이 없어졌다
        else if (got != null) Put(t, got);
        else
        {
            t.failed = true;
            t.mark.gameObject.SetActive(true);
        }
        done();
    }

    private void Drop()
    {
        foreach (var t in tiles)
        {
            if (t.tex == null) continue;
            t.img.enabled = false;
            t.img.texture = null;
            Destroy(t.tex);
            t.tex = null;
        }
    }

    // 꺼지면 코루틴이 멈춘다 — 다시 켜지면 처음부터 받는다
    private void OnDisable()
    {
        if (!loading) return;
        loading = false;
        R0926ChatPhotoBubble.LoadingNow--;
    }

    private void OnDestroy()
    {
        foreach (var t in tiles) if (t.tex != null) Destroy(t.tex);
    }
}
