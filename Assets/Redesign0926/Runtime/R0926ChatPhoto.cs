using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 대화방 사진 — 입력줄 앞 + 버튼 · 사진 고르기 · 올리기 · 사진 받아 두기.
/// 10-30 대표님 「메세지창 앞부분에 +버튼 하나 만들어서 사진 공유할 수 있도록」.
/// 사진은 메시지 내용에 주소로 들어간다(서버 /api/dm/image) — 말풍선은 주소를 알아보고 사진으로 그린다(R0926ChatPhotoBubble).
/// 파일 이름 끝의 가로x세로 덕분에 사진을 받기 전에 말풍선 크기가 정해져, 사진이 뒤늦게 와도 대화가 밀리지 않는다.
/// 올리는 동안은 대화방을 나가도 끊기지 않게 코루틴을 MessagePanelManager 에서 돌린다
/// </summary>
public class R0926ChatPhoto : MonoBehaviour
{
    [SerializeField] private MessagePanelManager chat;
    [SerializeField] private PhotoSourceDialog sourceDialog;
    [SerializeField] private RectTransform input;   // 입력칸 — 공지 방엔 + 가 없어 왼쪽으로 넓힌다

    private const float InputLeftPlus = 196f, InputLeftAlone = 40f;   // Redesign0926ApplyChatPhoto 와 같은 값
    private const long CacheMaxBytes = 200L * 1024 * 1024, CacheKeepBytes = 150L * 1024 * 1024;
    private static readonly Regex FileName = new Regex(@"^[0-9a-f]{32}_(\d{1,5})x(\d{1,5})\.jpg\z");
    private const string PushPreview = "📷 사진";   // 서버가 넣는 미리보기(언어를 모른다) — 앱에선 기기 언어로

    private static bool cacheTrimmed;
    private readonly R0926PickerWait pick = new R0926PickerWait();

    public sealed class Ref
    {
        public string url, file;
        public int w, h;
        public string Thumb => MessagePanelManager.SearchBase + "/thumb/768/uploads/dm/" + file;
        public string Key(string kind) => kind + "_" + file;
    }

    private static string Prefix => MessagePanelManager.SearchBase + "/uploads/dm/";
    private static string CacheDir => Path.Combine(Application.temporaryCachePath, "chatphoto");
    private static string Title => R0926LocalizedText.Say("보낼 사진 고르기", "Choose a photo to send", "送る写真を選ぶ", "选择要发送的照片", "Elige una foto para enviar");
    private static string BadPhoto => R0926LocalizedText.Say("이 사진은 보낼 수 없어요. 다른 사진을 골라 주세요", "This photo can't be sent. Please choose another", "この写真は送れません。別の写真を選んでください", "无法发送这张照片，请选择其他照片", "No se puede enviar esta foto. Elige otra");
    private static string SendFail => R0926LocalizedText.Say("사진을 보내지 못했어요. 다시 보내 주세요", "Photo not sent. Please try again", "写真を送れませんでした。もう一度送ってください", "照片未发送，请重试", "No se envió la foto. Inténtalo de nuevo");
    private static string TooMany => R0926LocalizedText.Say("사진을 너무 많이 보냈어요. 잠시 뒤에 다시 보내 주세요", "Too many photos. Please try again later", "写真を送りすぎです。しばらくしてからもう一度送ってください", "发送的照片过多，请稍后再试", "Demasiadas fotos. Inténtalo más tarde");

    /// <summary>이 메시지가 사진이면 그 정보 — 우리 서버의 사진 주소 모양일 때만 (아무 주소나 받아 오지 않는다)</summary>
    public static Ref TryParse(string content)
    {
        string t = content != null ? content.Trim() : "";
        string prefix = Prefix;
        if (!t.StartsWith(prefix, StringComparison.Ordinal)) return null;
        string file = t.Substring(prefix.Length);
        var m = FileName.Match(file);
        if (!m.Success) return null;
        int w = int.Parse(m.Groups[1].Value), h = int.Parse(m.Groups[2].Value);
        return w > 0 && h > 0 ? new Ref { url = t, file = file, w = w, h = h } : null;
    }

    /// <summary>대화 목록 미리보기 — 사진이면 주소 대신 「사진」</summary>
    public static string Preview(string text)
    {
        return text == PushPreview || TryParse(text) != null
            ? R0926LocalizedText.Say("사진", "Photo", "写真", "照片", "Foto")
            : text;
    }

    // ── 받아 두기 ───────────────────────────────────────
    /// <summary>주소의 파일을 받는다 — 한 번 받은 것은 기기에 두고 다시 쓴다. 못 받으면 null</summary>
    public static IEnumerator Fetch(string url, string key, Action<byte[]> done)
    {
        string path = Path.Combine(CacheDir, key);
        byte[] bytes = null;
        try { if (File.Exists(path)) bytes = File.ReadAllBytes(path); }
        catch (Exception) { }
        if (bytes != null && bytes.Length > 0) { done(bytes); yield break; }

        bytes = null;
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 30;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) bytes = req.downloadHandler.data;
        }
        if (bytes != null && bytes.Length > 0) Store(path, bytes);
        done(bytes);
    }

    private static void Store(string path, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            if (!cacheTrimmed) { cacheTrimmed = true; TrimCache(); }
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);   // 쓰다 끊겨도 깨진 파일이 남지 않게
            File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception) { }   // 못 두어도 다시 받으면 된다
    }

    // 켤 때마다 한 번 — 오래된 것부터 지운다
    private static void TrimCache()
    {
        var files = new List<FileInfo>(new DirectoryInfo(CacheDir).GetFiles());
        long total = 0;
        foreach (var f in files) total += f.Length;
        if (total <= CacheMaxBytes) return;
        files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
        foreach (var f in files)
        {
            if (total <= CacheKeepBytes) break;
            total -= f.Length;
            f.Delete();
        }
    }

    public static Texture2D Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!tex.LoadImage(bytes, true)) { Destroy(tex); return null; }
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // ── + 버튼 ──────────────────────────────────────────
    private void OnApplicationFocus(bool hasFocus) => R0926PickerWait.NoteFocus(hasFocus);

    /// <summary>공지 방은 보낼 수 없다 — + 를 숨기고 입력칸을 왼쪽으로 넓힌다</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
        if (input != null) input.offsetMin = new Vector2(on ? InputLeftPlus : InputLeftAlone, input.offsetMin.y);
    }

    public void OnPlus()
    {
        string peer = chat != null ? chat.PhotoPeer() : null;
        if (peer == null) return;
        if (pick.Waiting && !pick.TryAbandon()) { Warn(R0926PickerWait.BusyText); return; }
        if (sourceDialog == null) { chat.StartCoroutine(FromGallery(peer)); return; }
        sourceDialog.Show("", () => chat.StartCoroutine(FromCamera(peer)), () => chat.StartCoroutine(FromGallery(peer)));
    }

    private IEnumerator FromGallery(string peer)
    {
        if (!R0926PickerWait.PluginFree(R0926PickerWait.Source.Gallery)) { Warn(R0926PickerWait.BusyText); yield break; }
        bool answered = false;
        string path = null;
        int t = pick.Begin(R0926PickerWait.Source.Gallery);
        try
        {
            NativeGallery.GetImageFromGallery(p =>
            {
                if (!pick.End(t)) return;
                path = p;
                answered = true;
            }, Title, "image/*");
        }
        catch (Exception)
        {
            pick.End(t);
            answered = true;
        }
        yield return new WaitUntil(() => answered || !pick.Holds(t));
        if (!answered || string.IsNullOrEmpty(path)) yield break;

        yield return null;   // 고르기 창이 닫힌 화면이 먼저 그려지게 — 사진 읽기는 그동안 화면을 멈춘다
        Texture2D tex = null;
        try { tex = NativeGallery.LoadImageAtPath(path, maxSize: 2048, markTextureNonReadable: false); }
        catch (Exception) { tex = null; }
        yield return Send(peer, tex);
    }

    private IEnumerator FromCamera(string peer)
    {
        if (!R0926PickerWait.PluginFree(R0926PickerWait.Source.Camera)) { Warn(R0926PickerWait.BusyText); yield break; }
        bool answered = false;
        string path = null;
        int t = pick.Begin(R0926PickerWait.Source.Camera);
        NativeCamera.TakePicture(p =>
        {
            if (!pick.End(t)) return;
            path = p;
            answered = true;
        }, maxSize: 2048);
        yield return new WaitUntil(() => answered || !pick.Holds(t));
        if (!answered) yield break;
        if (string.IsNullOrEmpty(path))
        {
            // 찍지 않고 닫아도 경로가 비어 온다 — 권한이 없을 때만 알린다
            if (!NativeCamera.CheckPermission(true))
                Warn(R0926LocalizedText.Say("카메라 권한이 필요합니다.", "Camera permission is required.", "カメラの許可が必要です。", "需要相机权限。", "Se requiere permiso de cámara."));
            yield break;
        }

        yield return null;
        Texture2D tex = null;
        try { tex = NativeCamera.LoadImageAtPath(path, maxSize: 2048, markTextureNonReadable: false); }
        catch (Exception) { tex = null; }
        yield return Send(peer, tex);
    }

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 사진첩 대신 이 사진을 고른 것처럼</summary>
    public void EditorPick(byte[] bytes)
    {
        string peer = chat != null ? chat.PhotoPeer() : null;
        if (peer == null) return;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes)) { Destroy(tex); tex = null; }
        chat.StartCoroutine(Send(peer, tex));
    }
#endif

    // ── 올리기 ──────────────────────────────────────────
    private IEnumerator Send(string peer, Texture2D tex)
    {
        if (tex == null || tex.width < 8 || tex.height < 8)
        {
            if (tex != null) Destroy(tex);
            Warn(BadPhoto);
            yield break;
        }

        // 내 말풍선을 바로 그린다 — 이 사진은 말풍선이 가져간다 (말풍선이 없어질 때 같이 지운다)
        R0926ChatPhotoBubble bubble = chat.AddPendingPhotoBubble(tex);
        yield return null;   // 말풍선이 먼저 그려지게 — JPEG 만들기는 그동안 화면을 멈춘다

        byte[] jpg = null;
        try
        {
            jpg = tex.EncodeToJPG(88);
            tex.Apply(false, true);   // 메모리에 남은 화소 사본은 이제 필요 없다
        }
        catch (Exception e) { Debug.LogException(e); }
        if (jpg == null || jpg.Length == 0) { Fail(bubble, BadPhoto); yield break; }

        string me = LoginManager.Instance != null && LoginManager.Instance.CurrentUser != null ? LoginManager.Instance.CurrentUser.id : null;
        if (string.IsNullOrEmpty(me)) { Fail(bubble, SendFail); yield break; }

        var parts = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("sender_id", me),
            new MultipartFormDataSection("recipient_id", peer),
            new MultipartFormFileSection("photo", jpg, "photo.jpg", "image/jpeg"),
        };
        using (var req = UnityWebRequest.Post(MessagePanelManager.SearchBase + "/api/dm/image", parts))
        {
            req.timeout = 120;
            LoginManager.ApplyAuth(req);
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (bubble != null) bubble.Progress(Mathf.Min(req.uploadProgress, 0.99f));
                yield return null;
            }
            LoginManager.CheckUnauthorized(req);

            if (req.result != UnityWebRequest.Result.Success)
            {
                long code = req.responseCode;
                Fail(bubble, code == 401 || code == 403 ? MessagePanelManager.SendFailReason(req)
                    : code == 429 ? TooMany
                    : code == 400 || code == 413 ? BadPhoto
                    : SendFail);
                yield break;
            }

            var j = JSON.Parse(req.downloadHandler.text);
            var sent = j != null ? TryParse(j["url"].Value) : null;
            if (sent == null) { Fail(bubble, SendFail); yield break; }
            chat.FinishPendingPhoto(bubble, j["message_id"].AsInt, sent);
        }
    }

    private static void Fail(R0926ChatPhotoBubble bubble, string why)
    {
        if (bubble != null) Destroy(bubble.gameObject);
        Warn(why);
    }

    private static void Warn(string text)
    {
        if (ToastManager.Instance != null) ToastManager.Instance.ShowWarning(text);
    }
}
