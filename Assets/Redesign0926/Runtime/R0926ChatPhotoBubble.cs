using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사진 말풍선 — 글 말풍선 프리팹을 그대로 쓰고 글 대신 사진을 넣는다(둥근 모서리는 말풍선 그림으로 가린다).
/// 크기는 주소의 가로·세로로 먼저 정해져 있고, 사진은 화면 가까이 왔을 때 받는다(한 번에 셋까지). 멀어지면 내려놓아 메모리를 비운다 — 다시 오면 기기에 받아 둔 것을 쓴다.
/// 한 번 누르면 크게 보기, 두 번 누르면 글 말풍선처럼 좋아요. 못 받았으면 눌러서 다시 받는다
/// </summary>
public class R0926ChatPhotoBubble : MonoBehaviour
{
    private const float MaxW = 760f, MaxH = 900f, MinAspect = 0.5f, MaxAspect = 2f, CheckEvery = 0.2f;
    private const float LoadWithin = 0.5f, DropBeyond = 3f;   // 화면 높이의 몇 배 — 스크롤 앞쪽을 미리 받고, 멀어진 건 내려놓는다
    internal const int MaxLoads = 3;   // 대화방 전체에서 한 번에 받는 사진 — 묶음 말풍선(R0926ChatAlbumBubble)도 같이 센다
    private static readonly Color Placeholder = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Vector3[] Mine = new Vector3[4], View = new Vector3[4];
    internal static int LoadingNow;

    private RectTransform bubble;
    private RawImage photo;
    private Image shade;
    private Text label;
    private ScrollRect scroll;
    private R0926PhotoView view;
    private R0926ChatPhoto.Ref reference;
    private Texture2D tex;
    private Vector2 box;
    private bool loading, failed, local;
    private float nextCheck;

    public static R0926ChatPhotoBubble Attach(GameObject item, RectTransform bubble, Text content, R0926ChatPhoto.Ref reference, R0926PhotoView view)
    {
        var ui = item.AddComponent<R0926ChatPhotoBubble>();
        ui.Build(bubble, content, reference.w, reference.h);
        ui.reference = reference;
        ui.view = view;
        return ui;
    }

    /// <summary>내가 고른 사진 — 올리는 동안 이 사진을 그대로 보여 준다</summary>
    public static R0926ChatPhotoBubble AttachLocal(GameObject item, RectTransform bubble, Text content, Texture2D picked, R0926PhotoView view)
    {
        var ui = item.AddComponent<R0926ChatPhotoBubble>();
        ui.Build(bubble, content, picked.width, picked.height);
        ui.view = view;
        ui.local = true;
        ui.Put(picked);
        ui.Progress(0f);
        return ui;
    }

    /// <summary>글 말풍선의 BubbleAutoSizer 는 숨긴 주소 글자의 높이를 말풍선 높이로 걸어 둔다 — 사진 칸만으로 크기를 정하게 끈다
    /// (11-09: 주소 세 줄인 묶음이 칸보다 300 넘게 길어졌다)</summary>
    internal static void StopTextSizing(RectTransform bubble)
    {
        var sizer = bubble.GetComponent<BubbleAutoSizer>();
        if (sizer != null) sizer.enabled = false;
        var le = bubble.GetComponent<LayoutElement>();
        if (le != null) le.preferredHeight = -1f;
    }

    private void Build(RectTransform rt, Text content, int w, int h)
    {
        bubble = rt;
        scroll = GetComponentInParent<ScrollRect>(true);
        content.gameObject.SetActive(false);
        StopTextSizing(rt);

        var vlg = rt.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset();
        vlg.spacing = 0f;
        var bg = rt.GetComponent<Image>();
        bg.color = Placeholder;   // 받기 전에 분홍 말풍선이 비치지 않게
        rt.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        float aspect = Mathf.Clamp(w / (float)h, MinAspect, MaxAspect);
        float width = Mathf.Min(MaxW, MaxH * aspect);
        box = new Vector2(width, width / aspect);

        var go = new GameObject("Photo0926", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
        go.layer = gameObject.layer;
        go.transform.SetParent(rt, false);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = box.x;
        le.minHeight = le.preferredHeight = box.y;
        photo = go.GetComponent<RawImage>();
        photo.raycastTarget = false;
        photo.enabled = false;

        var s = new GameObject("Shade0926", typeof(RectTransform), typeof(Image));
        s.layer = gameObject.layer;
        s.transform.SetParent(go.transform, false);
        Fill((RectTransform)s.transform);
        shade = s.GetComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.4f);
        shade.raycastTarget = false;
        s.SetActive(false);

        var t = new GameObject("Label0926", typeof(RectTransform), typeof(Text));
        t.layer = gameObject.layer;
        t.transform.SetParent(go.transform, false);
        Fill((RectTransform)t.transform);
        ((RectTransform)t.transform).offsetMin = new Vector2(24f, 0f);
        ((RectTransform)t.transform).offsetMax = new Vector2(-24f, 0f);
        label = t.GetComponent<Text>();
        label.font = content.font;
        label.fontSize = 42;
        label.color = content.color;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        t.SetActive(false);
    }

    private static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public void Progress(float p)
    {
        if (label == null) return;
        shade.gameObject.SetActive(true);
        label.gameObject.SetActive(true);
        label.text = Mathf.RoundToInt(p * 100f) + "%";
    }

    /// <summary>올리기 끝 — 이제 서버의 사진이다. 작은 사진을 받으면 큰 원본(최대 2048)을 내려놓는다</summary>
    public void Sent(R0926ChatPhoto.Ref sent)
    {
        reference = sent;
        shade.gameObject.SetActive(false);
        label.gameObject.SetActive(false);
        nextCheck = 0f;
    }

    public void Tap()
    {
        if (reference == null) return;   // 아직 올리는 중
        if (failed)
        {
            failed = false;
            label.gameObject.SetActive(false);
            nextCheck = 0f;
            return;
        }
        if (view != null) view.Open(reference, tex);
    }

    private void Update()
    {
        if (reference == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckEvery;
        if (tex == null || local)
        {
            if (!loading && !failed && LoadingNow < MaxLoads && Near(LoadWithin)) StartCoroutine(Load());
        }
        else if (!Near(DropBeyond)) Drop();
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
        LoadingNow++;
        byte[] bytes = null;
        yield return R0926ChatPhoto.Fetch(reference.Thumb, reference.Key("t"), b => bytes = b);
        Texture2D got = R0926ChatPhoto.Decode(bytes);
        if (got == null)   // 작은 사진을 못 만들었으면 원본으로
        {
            yield return R0926ChatPhoto.Fetch(reference.url, reference.Key("f"), b => bytes = b);
            got = R0926ChatPhoto.Decode(bytes);
        }
        loading = false;
        LoadingNow--;

        if (got != null) { local = false; Put(got); }
        else if (local) local = false;   // 내가 고른 사진이 보이고 있다 — 그대로 둔다
        else
        {
            failed = true;
            label.text = R0926LocalizedText.Say("사진을 불러오지 못했어요", "Couldn't load photo", "写真を読み込めません", "无法加载照片", "No se pudo cargar la foto");
            label.gameObject.SetActive(true);
        }
    }

    private void Put(Texture2D t)
    {
        if (tex != null && tex != t) Destroy(tex);
        tex = t;
        photo.texture = t;
        photo.enabled = true;
        // 말풍선 비율을 벗어난 사진(아주 긴 사진)은 가운데를 보여 준다
        float ta = t.width / (float)t.height, ba = box.x / box.y;
        photo.uvRect = ta > ba ? new Rect((1f - ba / ta) * 0.5f, 0f, ba / ta, 1f) : new Rect(0f, (1f - ta / ba) * 0.5f, 1f, ta / ba);
    }

    private void Drop()
    {
        photo.enabled = false;
        photo.texture = null;
        Destroy(tex);
        tex = null;
    }

    // 꺼지면 코루틴이 멈춘다 — 다시 켜지면 처음부터 받는다
    private void OnDisable()
    {
        if (!loading) return;
        loading = false;
        LoadingNow--;
    }

    private void OnDestroy()
    {
        if (tex != null) Destroy(tex);
    }
}
