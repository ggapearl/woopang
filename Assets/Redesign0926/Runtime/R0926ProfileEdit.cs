using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 앱 안 '프로필 편집' (2026-10-30) — 대표님 10-27 「안드로이드는 '사진설정'을 하려 해도 '사진첩'·'갤러리'가 아니라 '내 파일'로 안내」
/// 「아예 웹으로 이동하지 않고 앱 내에서 수정할 수 있으면 더 좋을 것 같긴 해」.
/// 예전엔 내 프로필의 '편집'이 웹(/profile/edit)을 열었고, 웹의 파일 고르기는 안드로이드에서 '내 파일'로 열렸다.
/// 이제 이 시트에서 바로 고친다 — 사진은 사진첩(NativeGallery, 안드로이드 ACTION_PICK)에서 골라 장소 사진과 같은 자르기 화면으로 정사각형,
/// 아이디·소개·전화번호·SNS 는 키보드 위 입력줄(UploadInputMirror)로. 비밀번호·계정 삭제만 웹으로(맨 아래 줄).
/// 서버: GET /api/user/profile (본인이면 연락처·아이디를 다시 바꿀 수 있을 때까지 남은 날) · POST /api/user/profile (바꾼 칸만) · POST /api/user/avatar.
/// 저장·사진 올리기는 ProfileManager 위에서 돌려 시트를 닫아도 끝까지 간다.
/// </summary>
public class R0926ProfileEdit : MonoBehaviour
{
    [SerializeField] private CanvasGroup form;          // 불러오는 동안·저장하는 동안엔 칸을 못 누르게
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Image avatar;
    [SerializeField] private InputField usernameInput;
    [SerializeField] private Text usernameNote;
    [SerializeField] private GameObject usernameLock;   // 아이디를 아직 못 바꿀 때 칸 위 덮개 — 누르면 남은 날
    [SerializeField] private InputField bioInput;
    [SerializeField] private InputField phoneInput;
    [SerializeField] private InputField instagramInput;
    [SerializeField] private InputField facebookInput;
    [SerializeField] private InputField xInput;
    [SerializeField] private Text message;              // 저장 버튼 위 한 줄 — 잘못 쓴 칸·서버 답
    [SerializeField] private Button saveButton;
    [SerializeField] private Text saveLabel;
    [SerializeField] private R0926PhotoCrop photoCrop;
    [SerializeField] private R0926Busy busy;
    [SerializeField] private R0926Closer closer;        // 저장하면 끌어 닫을 때처럼 아래로 내려가며 닫힌다
    [SerializeField] private Button closeButton;

    private enum State { Loading, Failed, Ready, Saving }

    private static readonly string[] CropTitle = { "프로필 사진", "Profile photo", "プロフィール写真", "头像", "Foto de perfil" };
    private static readonly string[] Keys = { "username", "bio", "phone", "instagram_id", "facebook_id", "x_id" };
    private static readonly Regex UserRe = new Regex(@"^[a-z0-9._]{1,20}$");
    private static readonly Regex DigitsDots = new Regex(@"^[0-9]+(\.[0-9]+)+$");   // 서버가 기관(공공데이터) 아이디 모양으로 막는다
    private static readonly Regex PhoneRe = new Regex(@"^[0-9+\-() ]*$");
    private const int AvatarMax = 512;   // 서버가 512 정사각형으로 다시 저장한다

    private State state;
    private int session;
    private string me;
    private int waitDays;
    private bool saving, uploading;
    private readonly string[] shown = new string[6];   // 불러와 칸에 넣은 글 — 손대지 않은 칸은 보내지 않는다
    private readonly R0926PickerWait pick = new R0926PickerWait();

    private InputField[] Fields => new[] { usernameInput, bioInput, phoneInput, instagramInput, facebookInput, xInput };
    private MonoBehaviour Host => ProfileManager.Instance != null ? ProfileManager.Instance : (MonoBehaviour)this;

    private static string Base
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(ProfileManager.EditorBase)) return ProfileManager.EditorBase;
#endif
            return ApiConfig.MAIN_SERVER;
        }
    }

    /// <summary>내 프로필 '편집' (ProfileManager) — 보이던 공개 정보를 먼저 넣고 서버에서 내 정보(전화번호 포함)를 받는다</summary>
    public void Open(ProfileData profile)
    {
        var user = LoginManager.Instance != null ? LoginManager.Instance.CurrentUser : null;
        if (user == null || string.IsNullOrEmpty(user.id)) { Toast(false, ReloginText); return; }
        me = user.id;
        session++;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        if (profile != null && profile.id == me)
        {
            Fill(profile.username, profile.bio, "", profile.instagram_id, profile.facebook_id, profile.x_id);
            ProfileManager.LoadAvatarAsync(me, profile.avatar_url, avatar, profile.username);
        }
        else
        {
            Fill(user.username, "", "", "", "", "");
            ProfileManager.LoadAvatarAsync(me, null, avatar, user.username);
        }
        waitDays = 0;
        ApplyLock();
        Say("");
        SetState(State.Loading);
        StartCoroutine(Load(session));
    }

    public void Close() => gameObject.SetActive(false);

    private void OnDisable() => session++;

    private void OnApplicationFocus(bool hasFocus) => R0926PickerWait.NoteFocus(hasFocus);

    // ── 불러오기 ─────────────────────────────────────────
    private IEnumerator Load(int s)
    {
        string url = $"{Base}/api/user/profile?user_id={UnityWebRequest.EscapeURL(me)}&_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        long code;
        JSONNode j;
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 15;
            LoginManager.ApplyAuth(req);
            yield return req.SendWebRequest();
            code = req.result == UnityWebRequest.Result.ConnectionError ? 0 : req.responseCode;
            j = Parse(req);
        }
        if (s != session) yield break;

        // 본인으로 알아봤을 때만 남은 날이 온다 — 없으면 토큰이 죽었거나 다른 사람 것
        if (code == 200 && j["id"].Value == me && j.HasKey("username_wait_days"))
        {
            Fill(j["username"].Value, j["bio"].Value, j["phone"].Value, j["instagram_id"].Value, j["facebook_id"].Value, j["x_id"].Value);
            waitDays = j["username_wait_days"].AsInt;
            ApplyLock();
            ProfileManager.LoadAvatarAsync(me, j["avatar_url"].Value, avatar, j["username"].Value);
            SetState(State.Ready);
            yield break;
        }
        Say(code == 200 || code == 401 ? ReloginText : code == 0 ? NetworkText : code == 404 ? NotFoundText : GenericText);
        SetState(State.Failed);
    }

    private void Fill(params string[] values)
    {
        var fs = Fields;
        for (int i = 0; i < fs.Length; i++)
        {
            Put(fs[i], values[i]);
            shown[i] = fs[i] != null ? fs[i].text : "";   // 칸이 줄인 글(글자 수·한 줄) 그대로 — 손대지 않으면 같다
        }
    }

    // 키보드 위 입력줄로 쓰는 칸은 꺼져 있다 — 잠깐 켜고 넣어야 화면에 그려진다 (UploadInputMirror 와 같은 방법)
    private static void Put(InputField f, string v)
    {
        if (f == null) return;
        bool was = f.enabled;
        if (!was) f.enabled = true;
        f.text = v ?? "";
        f.ForceLabelUpdate();
        if (!was) f.enabled = false;
    }

    private void SetState(State s)
    {
        state = s;
        bool ready = s == State.Ready;
        if (form != null)
        {
            form.alpha = s == State.Loading || s == State.Failed ? 0.45f : 1f;
            form.interactable = ready;
            form.blocksRaycasts = ready;   // 입력칸은 누르는 순간 입력줄이 뜬다(EventTrigger) — interactable 만으론 못 막는다
        }
        if (saveButton != null) saveButton.interactable = ready || s == State.Failed;
        if (saveLabel != null)
            saveLabel.text = s == State.Loading ? T("불러오는 중…", "Loading…", "読み込み中…", "正在加载…", "Cargando…")
                : s == State.Failed ? T("다시 불러오기", "Try again", "もう一度読み込む", "重新加载", "Reintentar")
                : s == State.Saving ? T("저장하는 중…", "Saving…", "保存中…", "正在保存…", "Guardando…")
                : T("저장하기", "Save", "保存する", "保存", "Guardar");
    }

    private void ApplyLock()
    {
        bool locked = waitDays > 0;
        if (usernameLock != null) usernameLock.SetActive(locked);
        if (usernameNote != null)
        {
            usernameNote.text = locked ? LockedText(waitDays)
                : T("영문 소문자·숫자·마침표(.)·밑줄(_) 20자까지 · 바꾸면 30일 뒤에 다시 바꿀 수 있어요",
                    "Lowercase letters, numbers, . and _ (up to 20) · once changed, you can change it again after 30 days",
                    "英小文字・数字・ピリオド(.)・アンダーバー(_)で20文字まで · 変更すると30日間は再変更できません",
                    "小写英文字母、数字、句点(.)、下划线(_)，最多20个字符 · 修改后30天内不能再改",
                    "Minúsculas, números, punto (.) y guion bajo (_), hasta 20 · tras cambiarlo, podrás volver a hacerlo en 30 días");
        }
    }

    /// <summary>아이디 칸 덮개를 눌렀다 — 아직 못 바꾼다</summary>
    public void OnUsernameLocked() => Say(LockedText(waitDays));

    // ── 저장 ────────────────────────────────────────────
    public void OnSave()
    {
        if (state == State.Failed)
        {
            Say("");
            SetState(State.Loading);
            StartCoroutine(Load(session));
            return;
        }
        if (state != State.Ready || saving) return;

        var fs = Fields;
        var body = new JSONObject();
        string newName = null;
        for (int i = 0; i < fs.Length; i++)
        {
            if (fs[i] == null || fs[i].text == shown[i]) continue;
            string v = fs[i].text.Trim();
            if (i == 0)
            {
                v = v.ToLowerInvariant();
                if (!UserRe.IsMatch(v)) { Say(InvalidNameText); return; }
                if (DigitsDots.IsMatch(v)) { Say(T("숫자와 마침표만으로 된 아이디는 쓸 수 없어요", "Usernames can't be only numbers and dots", "数字とピリオドだけのユーザー名は使えません", "用户名不能只由数字和句点组成", "El nombre de usuario no puede tener solo números y puntos")); return; }
                newName = v;
            }
            else if (i == 2 && !PhoneRe.IsMatch(v)) { Say(PhoneText); return; }
            body[Keys[i]] = v;
        }
        if (body.Count == 0) { Close(); return; }
        body["user_id"] = me;
        Say("");
        SetState(State.Saving);
        Host.StartCoroutine(Save(session, me, body, newName));
    }

    private IEnumerator Save(int s, string uid, JSONObject body, string newName)
    {
        saving = true;
        long code;
        JSONNode j;
        using (var req = new UnityWebRequest(Base + "/api/user/profile", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body.ToString()));
            req.SetRequestHeader("Content-Type", "application/json");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 20;
            LoginManager.ApplyAuth(req);
            yield return req.SendWebRequest();
            LoginManager.CheckUnauthorized(req);
            code = req.result == UnityWebRequest.Result.ConnectionError ? 0 : req.responseCode;
            j = Parse(req);
        }
        saving = false;
        bool here = s == session && isActiveAndEnabled;

        if (code == 200)
        {
            var user = LoginManager.Instance != null ? LoginManager.Instance.CurrentUser : null;
            if (newName != null && user != null && user.id == uid) LoginManager.Instance.SetUsername(newName);
            if (ProfileManager.Instance != null)
            {
                ProfileManager.Instance.LoadMyProfile();
                ProfileManager.Instance.RefreshCurrentProfile();
            }
            Toast(true, T("프로필을 저장했어요", "Profile saved", "プロフィールを保存しました", "个人资料已保存", "Perfil guardado"));
            if (here)
            {
                if (closer != null && closeButton != null) closer.AnimateClose(closeButton);
                else Close();
            }
            yield break;
        }

        string err = j["error"].Value, text;
        if (code == 0) text = NetworkText;
        else if (code == 401) text = ReloginText;
        else if (code == 404) text = NotFoundText;
        else if (err == "invalid_username") text = InvalidNameText;
        else if (err == "username_taken") text = T("이미 쓰는 아이디예요", "That username is already taken", "このユーザー名はすでに使われています", "该用户名已被使用", "Ese nombre de usuario ya está en uso");
        else if (err == "username_locked")
        {
            text = LockedText(j["days"].AsInt);
            if (here) { waitDays = j["days"].AsInt; ApplyLock(); }
        }
        else if (err == "invalid_phone") text = PhoneText;
        else if (err == "too_long") text = string.Format(T("글자가 너무 길어요 ({0}자까지)", "Too long (up to {0} characters)", "文字数が多すぎます（{0}文字まで）", "内容太长（最多{0}个字）", "Demasiado largo (hasta {0} caracteres)"), j["max"].AsInt);
        else text = GenericText;

        if (here) { Say(text); SetState(State.Ready); }
        else Toast(false, text);
    }

    // ── 사진 ────────────────────────────────────────────
    /// <summary>사진 칸 — 사진첩에서 골라 정사각형으로 잘라 바로 올린다</summary>
    public void PickPhoto()
    {
        if (state != State.Ready || uploading) return;
        if (pick.Waiting && !pick.TryAbandon()) { Say(R0926PickerWait.BusyText); return; }
        Say("");
        StartCoroutine(PickRoutine());
    }

    private IEnumerator PickRoutine()
    {
        if (!R0926PickerWait.PluginFree(R0926PickerWait.Source.Gallery)) { Say(R0926PickerWait.BusyText); yield break; }
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
            }, T("프로필 사진 고르기", "Choose a profile photo", "プロフィール写真を選ぶ", "选择头像", "Elige una foto de perfil"), "image/*");
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
        try { tex = NativeGallery.LoadImageAtPath(path, maxSize: 2048); }
        catch (Exception) { tex = null; }
        Crop(tex);
    }

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 사진첩 대신 이 사진(JPG·PNG)을 고른 것처럼</summary>
    public void EditorPick(byte[] bytes)
    {
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(bytes)) { Destroy(tex); tex = null; }
        Crop(tex);
    }
#endif

    // 고른 사진 → 자르기 화면 → 올리기
    private void Crop(Texture2D tex)
    {
        if (tex == null || tex.width < 8 || tex.height < 8 || photoCrop == null)
        {
            if (tex != null) Destroy(tex);
            Say(PhotoFailText);
            return;
        }

        int s = session;
        string uid = me, name = shown[0];
        photoCrop.Open(tex, cropped =>
        {
            Destroy(tex);
            if (cropped == null) { Toast(false, PhotoFailText); return; }
            Host.StartCoroutine(Upload(s, uid, name, cropped));
        }, () => Destroy(tex), CropTitle);
    }

    private IEnumerator Upload(int s, string uid, string name, Texture2D cropped)
    {
        uploading = true;
        yield return null;   // 자르기 화면이 '자르는 중' 카드를 걷은 다음에 '올리는 중'을 띄운다
        if (busy != null) busy.Show(UploadText(0f), 0f);
        byte[] jpg = null;
        try { jpg = Jpeg(cropped, AvatarMax); }
        catch (Exception e) { Debug.LogException(e); }
        finally { Destroy(cropped); }
        if (jpg == null)
        {
            uploading = false;
            if (busy != null) busy.Hide();
            Toast(false, PhotoFailText);
            yield break;
        }

        long code;
        JSONNode j;
        var parts = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("user_id", uid),
            new MultipartFormFileSection("avatar", jpg, "avatar.jpg", "image/jpeg"),
        };
        using (var req = UnityWebRequest.Post(Base + "/api/user/avatar", parts))
        {
            req.timeout = 60;
            LoginManager.ApplyAuth(req);
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                float p = Mathf.Min(req.uploadProgress, 0.99f);   // 다 보내고 답을 기다리는 동안 100% 에 멈춰 보이지 않게 (장소 올리기와 같이)
                if (busy != null) busy.Show(UploadText(p), p);   // 부르는 동안은 저절로 걷히지 않는다
                yield return null;
            }
            LoginManager.CheckUnauthorized(req);
            code = req.result == UnityWebRequest.Result.ConnectionError ? 0 : req.responseCode;
            j = Parse(req);
        }
        uploading = false;
        if (busy != null) busy.Hide();

        string url = j["avatar_url"].Value;
        if (code == 200 && !string.IsNullOrEmpty(url))
        {
            var user = LoginManager.Instance != null ? LoginManager.Instance.CurrentUser : null;
            if (user != null && user.id == uid) user.avatar_url = url;
            if (ProfileManager.Instance != null)
            {
                ProfileManager.Instance.ClearAvatarCacheForUser(uid);
                ProfileManager.Instance.LoadMyProfile();
                ProfileManager.Instance.RefreshCurrentProfile();
            }
            if (s == session && isActiveAndEnabled) ProfileManager.LoadAvatarAsync(uid, url, avatar, name);
            Toast(true, T("프로필 사진을 바꿨어요", "Profile photo updated", "プロフィール写真を変更しました", "头像已更新", "Foto de perfil actualizada"));
            yield break;
        }
        string err = j["error"].Value;
        Toast(false, code == 0 ? NetworkText : code == 401 ? ReloginText : code == 404 ? NotFoundText
            : err == "not_image" || err == "too_large" ? PhotoFailText : GenericText);
    }

    // 자른 사진(읽기 전용)을 max 이하 정사각형 JPEG 로 — 업로드와 같은 RenderTexture 줄이기
    private static byte[] Jpeg(Texture2D src, int max)
    {
        int n = Mathf.Min(max, Mathf.Min(src.width, src.height));
        var rt = RenderTexture.GetTemporary(n, n);
        var prev = RenderTexture.active;
        var t = new Texture2D(n, n, TextureFormat.RGB24, false);
        try
        {
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            t.ReadPixels(new Rect(0, 0, n, n), 0, 0, false);
            t.Apply(false);
            return t.EncodeToJPG(90);
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(t);
        }
    }

    /// <summary>맨 아래 줄 — 비밀번호 변경·계정 삭제는 웹에서 (일회용 코드로 연다)</summary>
    public void OpenWeb()
    {
        Close();
        if (ProfileManager.Instance != null) ProfileManager.Instance.OpenEditProfileWebPage();
    }

    // ── 글 ──────────────────────────────────────────────
    private void Say(string text)
    {
        if (message == null) return;
        message.text = text ?? "";
        message.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private static void Toast(bool ok, string msg)
    {
        if (ToastManager.Instance == null) return;
        if (ok) ToastManager.Instance.ShowSuccess(msg);
        else ToastManager.Instance.ShowError(msg);
    }

    private static JSONNode Parse(UnityWebRequest req)
    {
        JSONNode j = null;
        try { j = JSON.Parse(req.downloadHandler != null ? req.downloadHandler.text : ""); }
        catch (Exception) { j = null; }
        return j ?? new JSONObject();
    }

    private static string T(string ko, string en, string ja, string zh, string es) => R0926LocalizedText.Say(ko, en, ja, zh, es);

    private static string LockedText(int d) => d <= 1
        ? T("아이디는 내일 다시 바꿀 수 있어요", "You can change your username again tomorrow", "ユーザー名は明日から再変更できます", "明天可以再次修改用户名", "Podrás cambiar tu nombre de usuario mañana")
        : string.Format(T("아이디는 {0}일 뒤에 다시 바꿀 수 있어요", "You can change your username again in {0} days", "ユーザー名は{0}日後に再変更できます", "{0}天后可以再次修改用户名", "Podrás cambiar tu nombre de usuario en {0} días"), d);

    private static string UploadText(float p) => string.Format(T("사진 올리는 중… {0}%", "Uploading photo… {0}%", "写真をアップロード中… {0}%", "正在上传照片… {0}%", "Subiendo foto… {0}%"), Mathf.FloorToInt(Mathf.Clamp01(p) * 100f));

    private static string ReloginText => T("다시 로그인한 뒤 고칠 수 있어요", "Please sign in again to edit your profile", "もう一度ログインすると編集できます", "请重新登录后再编辑", "Vuelve a iniciar sesión para editar tu perfil");
    private static string NetworkText => T("인터넷 연결을 확인해 주세요", "Check your internet connection", "インターネット接続を確認してください", "请检查网络连接", "Revisa tu conexión a internet");
    private static string NotFoundText => T("회원 정보를 찾을 수 없어요", "We couldn't find your account", "アカウント情報が見つかりません", "找不到你的账户信息", "No encontramos tu cuenta");
    private static string GenericText => T("잠시 뒤 다시 해 주세요", "Please try again in a moment", "しばらくしてからもう一度お試しください", "请稍后再试", "Inténtalo de nuevo en un momento");
    private static string InvalidNameText => T("아이디는 영문 소문자·숫자·마침표(.)·밑줄(_)로 20자까지 쓸 수 있어요", "Usernames can use lowercase letters, numbers, . and _ (up to 20)", "ユーザー名は英小文字・数字・ピリオド(.)・アンダーバー(_)で20文字までです", "用户名只能使用小写英文字母、数字、句点(.)和下划线(_)，最多20个字符", "El nombre de usuario admite minúsculas, números, . y _ (hasta 20)");
    private static string PhoneText => T("전화번호는 숫자와 + - ( ) 만 쓸 수 있어요", "Phone numbers can only use digits and + - ( )", "電話番号は数字と + - ( ) のみ使えます", "电话号码只能使用数字和 + - ( )", "El teléfono solo admite números y + - ( )");
    private static string PhotoFailText => T("사진을 열지 못했어요. 다른 사진을 골라 주세요", "Couldn't open that photo. Please pick another one", "写真を開けませんでした。別の写真を選んでください", "无法打开这张照片，请换一张", "No se pudo abrir la foto. Elige otra");
}
