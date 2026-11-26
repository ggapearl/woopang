using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Collections;
using System.IO;

/// <summary>
/// 상세 사진 창의 번역 버튼 — 보고 있는 사진을 사진첩에 넣고 구글 번역(이미지)을 연다.
/// 11-26 대표님 21번 「캡쳐해서 번역기로 넘어갈 때 해당 화면 자체를 다 캡쳐하지말고 서브사진만 다운받아서 넘어가도록 해줘 …
/// 캡쳐하는 것처럼 지금 로직은 유지하고, 서브사진만 저장되도록 하면 될 것 같아」
///  · 확대 · 번쩍임은 그대로 — 저장하는 것만 화면이 아니라 사진 한 장 (버튼 · 글씨가 섞이지 않게)
///  · 우팡 서버 사진은 원본을 받는다(창에 띄운 건 줄인 1080) → 못 받으면 디스크에 받아 둔 그 사진 → 그것도 없으면 띄운 그림을 JPG 로
///  · 사진이 없는 쪽(정보 글)은 예전처럼 화면을 찍는다
/// </summary>
public class CaptureAndTranslate : MonoBehaviour
{
    public Image flashImage;
    public RectTransform captureArea;

    private const int OriginalTimeout = 8;   // 원본을 받는 시간(초) — 넘으면 받아 둔 사진으로
    private float busyUntil;                 // 받는 동안 또 눌러도 한 번만 (창을 닫아 끊겨도 잠시 뒤 풀린다)

#if UNITY_EDITOR
    /// <summary>시험용 — 사진첩에 넣는 파일을 받는다 (에디터의 NativeGallery 는 파일을 쓰지 않는다)</summary>
    public static Action<byte[], string> EditorSaved;
    /// <summary>시험용 — 브라우저를 여는 대신 주소를 받는다</summary>
    public static Action<string> EditorOpen;
#endif

    public void OnTranslateButtonClick()
    {
        if (Time.unscaledTime < busyUntil) return;
        StartCoroutine(CaptureWithZoomAndFlash());
    }

    private IEnumerator CaptureWithZoomAndFlash()
    {
        busyUntil = Time.unscaledTime + OriginalTimeout + 5f;

        // 0. 보고 있는 사진 — 확대 · 번쩍임 동안 미리 받는다
        Sprite photo = ShownPhoto();
        byte[] bytes = null;
        string ext = "png";
        bool ready = photo == null;
        if (photo != null) StartCoroutine(PhotoBytes(photo, (b, e) => { bytes = b; ext = e; ready = true; }));

        // 1. 화면 살짝 확대 애니메이션
        if (captureArea != null)
        {
            Vector3 originalScale = captureArea.localScale;
            float zoomDuration = 0.1f;
            float zoomScale = 1.1f;

            for (float t = 0; t < zoomDuration; t += Time.deltaTime)
            {
                captureArea.localScale = Vector3.Lerp(originalScale, originalScale * zoomScale, t / zoomDuration);
                yield return null;
            }

            yield return new WaitForSeconds(0.1f);

            for (float t = 0; t < zoomDuration; t += Time.deltaTime)
            {
                captureArea.localScale = Vector3.Lerp(originalScale * zoomScale, originalScale, t / zoomDuration);
                yield return null;
            }
            captureArea.localScale = originalScale;
        }

        // 2. 플래시 효과
        if (flashImage != null)
        {
            flashImage.gameObject.SetActive(true);
            flashImage.color = new Color(1, 1, 1, 0.8f);
            yield return new WaitForSeconds(0.2f);
            flashImage.color = new Color(1, 1, 1, 0);
        }

        // 3. 저장할 그림 — 사진이 떠 있으면 그 사진 한 장, 아니면(정보 글 쪽 · 사진을 못 얻음) 예전처럼 화면
        for (float cap = Time.unscaledTime + OriginalTimeout + 3f; !ready && Time.unscaledTime < cap;) yield return null;
        if (bytes == null)
        {
            yield return new WaitForEndOfFrame();
            bytes = ScreenBytes();
            ext = "png";
        }

        // 4. 사진첩에 저장 — NativeGallery 가 안드로이드 10+ 저장 방식·iOS 사진 권한을 처리한다
        //    (예전엔 안드로이드는 /storage/emulated/0/Pictures 에 직접 쓰고, iOS 는 앱 내부 폴더에 저장해
        //     번역 앱에서 사진을 고를 수 없었다)
        string fileName = "Woopang_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "." + ext;
#if UNITY_EDITOR
        EditorSaved?.Invoke(bytes, fileName);
#endif
        bool done = false;
        NativeGallery.SaveImageToGallery(bytes, "WOOPANG", fileName, (success, path) => done = true);
        float wait = 0f;
        while (!done && wait < 3f) { wait += Time.unscaledDeltaTime; yield return null; }

        // 5. 구글 번역 이미지 모드 — 기기 언어로 번역 (저장에 실패해도 번역 화면의 카메라로 쓸 수 있게 연다)
        string url = "https://translate.google.com/?sl=auto&tl=" + TargetLanguage() + "&op=images";
        busyUntil = 0f;
#if UNITY_EDITOR
        if (EditorOpen != null) { EditorOpen(url); yield break; }
#endif
        Application.OpenURL(url);
    }

    // 창에 떠 있는 상세 사진 (정보 글 쪽 · 아직 받는 중이면 null)
    private Sprite ShownPhoto()
    {
        var img = captureArea != null ? captureArea.GetComponent<Image>() : null;
        if (img == null || !img.enabled || !img.gameObject.activeInHierarchy) return null;
        var s = img.sprite;
        return s != null && s.texture != null && s.texture != Texture2D.blackTexture ? s : null;
    }

    // 사진 한 장의 파일 — 원본 → 디스크에 받아 둔 것 → 띄운 그림을 JPG 로. got 은 꼭 한 번 (못 얻으면 null)
    private IEnumerator PhotoBytes(Sprite s, Action<byte[], string> got)
    {
        if (SubPhotoCache.Source(s.texture, out string original, out string cached))
        {
            if (original != null && Application.internetReachability != NetworkReachability.NotReachable)
            {
                byte[] b = null;
                yield return Download(original, x => b = x);
                string e = ImageExt(b);
                if (e != null) { got(b, e); yield break; }
            }
            if (cached != null)
            {
                byte[] b = null;
                try { b = File.ReadAllBytes(cached); } catch (Exception) { }
                string e = ImageExt(b);
                if (e != null) { got(b, e); yield break; }
            }
        }
        got(Readback(s), "jpg");
    }

    private static IEnumerator Download(string url, Action<byte[]> got)
    {
        UnityWebRequest rq;
        try { rq = UnityWebRequest.Get(url); rq.timeout = OriginalTimeout; }
        catch (Exception) { yield break; }
        using (rq)
        {
            yield return rq.SendWebRequest();
            if (rq.result == UnityWebRequest.Result.Success) got(rq.downloadHandler.data);
        }
    }

    // 사진 파일인가 — 서버가 오류 글(HTML)을 돌려줘도 사진첩에 넣지 않는다. 번역에 올릴 수 있는 JPG · PNG 만
    private static string ImageExt(byte[] b)
    {
        if (b == null || b.Length < 8) return null;
        if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "jpg";
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "png";
        return null;
    }

    // 띄운 그림을 그대로 — 받은 사진은 읽기 전용이라 GPU 에서 옮겨 읽는다 (색은 sRGB 그대로)
    private static byte[] Readback(Sprite s)
    {
        if (s == null || s.texture == null) return null;   // 받는 동안 창이 사진을 놓아줬다
        Texture src = s.texture;
        Rect r = s.textureRect;
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        if (w < 1 || h < 1) return null;
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture prev = RenderTexture.active;
        Texture2D tex = null;
        try
        {
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
            tex.Apply(false);
            return tex.EncodeToJPG(95);
        }
        catch (Exception) { return null; }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (tex != null) Destroy(tex);
        }
    }

    // 예전 방식 — 화면을 찍는다 (사진이 없는 정보 글 쪽). 찍는 칸이 화면에 놓인 자리만, 화면 밖은 잘라 낸다
    // (예전엔 칸의 캔버스 크기 1300x1800 을 그대로 픽셀로 써서, 화면 배율이 1 이 아니면 칸보다 넓은 엉뚱한 자리를 찍었다)
    private byte[] ScreenBytes()
    {
        Rect area = ScreenArea();
        var shot = new Texture2D((int)area.width, (int)area.height, TextureFormat.RGB24, false);
        shot.ReadPixels(area, 0, 0);
        shot.Apply();
        byte[] bytes = shot.EncodeToPNG();
        Destroy(shot);
        return bytes;
    }

    private static readonly Vector3[] corners = new Vector3[4];

    /// <summary>찍는 칸의 화면 픽셀 자리 (화면 안으로 자름 · 칸이 없거나 너무 작으면 화면 전체)</summary>
    public Rect ScreenArea()
    {
        var all = new Rect(0, 0, Screen.width, Screen.height);
        if (captureArea == null) return all;
        var canvas = captureArea.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        captureArea.GetWorldCorners(corners);
        Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in corners)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, c);
            lo = Vector2.Min(lo, p);
            hi = Vector2.Max(hi, p);
        }
        int x0 = Mathf.Clamp(Mathf.FloorToInt(lo.x), 0, Screen.width), y0 = Mathf.Clamp(Mathf.FloorToInt(lo.y), 0, Screen.height);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(hi.x), 0, Screen.width), y1 = Mathf.Clamp(Mathf.CeilToInt(hi.y), 0, Screen.height);
        return x1 - x0 >= 16 && y1 - y0 >= 16 ? new Rect(x0, y0, x1 - x0, y1 - y0) : all;
    }

    private static string TargetLanguage()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified: return "zh-CN";
            case SystemLanguage.ChineseTraditional: return "zh-TW";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.German: return "de";
            case SystemLanguage.Vietnamese: return "vi";
            case SystemLanguage.Thai: return "th";
            case SystemLanguage.Indonesian: return "id";
            default: return "en";
        }
    }
}
