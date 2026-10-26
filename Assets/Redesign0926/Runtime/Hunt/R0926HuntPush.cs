using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보물찾기 알림(hunt_new · start · found · message · extend · end)을 누르면 — '추가' 창 보물찾기 카드의 그 게임 상세로.
/// 끝 알림에 review=1(별점 자격)이 붙어 있으면 주최자 프로필로.
/// 알림은 시작화면보다 먼저 올 수 있다 — 받아 두었다가 시작화면·첫 안내가 걷히고 로그인이 돌아오면 연다.
/// 위에 덮인 창은 닫고 연다 — 프로필·팔로우 목록·대화방·메시지 목록은 바로, 나머지(목록 …)는 뒤로가기 순서대로 (추가 창은 그대로 둔다).
/// 메시지 탭의 보물찾기 말풍선을 눌렀을 때도 같은 길로 연다(OpenHunt).
/// FirebaseNotification 이 부른다 — 안드로이드: 알림바에서 눌러 열림 · 앱을 보는 중 띄운 알림을 누름 / 아이폰: 눌러 열림 · 보는 중 뜬 배너를 누름.
/// 추가 창이 켜진 채 연 프로필(카드의 주최자 · 별점 알림)은 추가 창 위로 올린다 — 프로필 창이 캔버스에서 추가 창보다 앞 순서라 뒤에 가려졌다.
/// 새 팔로워 알림(notification_type=follow)도 같은 길로 — 덮인 창을 추가 창까지 닫고 내 팔로워 탭을 연다. 그 사람은 맨 위에 두고 잠깐 밝힌다 (B8, 10-18).
/// 공유 링크(woopang://hunt?id=…&amp;c=… — woopang.com/hunt/&lt;번호&gt; 의 '우팡 앱에서 열기')로 열려도 같은 길로 — 코드가 있으면 바로 참가 (10-26).
/// </summary>
public class R0926HuntPush : MonoBehaviour
{
    [SerializeField] private Button plusButton;                 // 도크 '추가' — 누른 것처럼 연다 (여는 로직은 버튼 것 그대로)
    [SerializeField] private GameObject uploadPage;              // 추가 창 — 위에 덮인 창을 닫다가 여기서 멈춘다
    [SerializeField] private SwipePanelController swipe;
    [SerializeField] private R0926HuntCard card;

    private static bool pending;
    private static string pendingHunt, pendingHost;
    private static bool pendingProfile;
    private static bool pendingFollow;      // 보물찾기 대신 내 팔로워 탭으로
    private static string pendingFollower;  // 맨 위에 둘 새 팔로워 아이디
    private static string lastKey;
    private static float lastAt = -99f;
    private static bool pendingLink;        // 공유 링크로 열림 — 상세 대신 R0926HuntCard.OpenLink (코드가 있으면 바로 참가)
    private static string pendingLinkCode;

    private bool running;
    private float readySince = -1f;
    private FirstTimeGuide guide;
    private Transform profileNext;   // 추가 창 위로 올린 프로필을 닫히면 돌려 둘 자리 (원래 바로 뒤에 있던 형제)

    public static bool Is(string msgType) => !string.IsNullOrEmpty(msgType) && msgType.StartsWith("hunt_");

    /// <summary>알림 데이터(msg_type · hunt_id · host_id · review)로 연다. 보물찾기 알림이 아니면 false</summary>
    public static bool Open(IDictionary<string, string> data)
    {
        if (data == null || !data.TryGetValue("msg_type", out var type) || !Is(type)) return false;
        data.TryGetValue("hunt_id", out var hunt);
        data.TryGetValue("host_id", out var host);
        Open(hunt, host, data.TryGetValue("review", out var review) && review == "1");
        return true;
    }

    /// <summary>알림을 누름 — 같은 알림이 두 길로 와도(FCM 콜백 · 앱을 연 인텐트) 한 번만 연다</summary>
    public static void Open(string huntId, string hostId, bool review)
    {
        bool toProfile = review && !string.IsNullOrEmpty(hostId);
        if (!toProfile && !int.TryParse(huntId, out _)) return;
        string key = huntId + "|" + hostId + "|" + toProfile;
        if (key == lastKey && Time.realtimeSinceStartup - lastAt < 10f) return;
        lastKey = key;
        lastAt = Time.realtimeSinceStartup;
        pendingHunt = huntId;
        pendingHost = hostId;
        pendingProfile = toProfile;
        pendingFollow = false;
        pendingLink = false;
        pending = true;
    }

    /// <summary>보물찾기 공유 링크로 앱이 열렸다 (woopang://hunt?id=…&amp;c=… · https://woopang.com/hunt/&lt;번호&gt;?c=…) — 그 게임으로, 코드가 있으면 바로 참가.
    /// 보물찾기 링크가 아니면 false. 같은 링크가 두 길로 와도(앱을 연 주소 · 링크 알림) 한 번만</summary>
    public static bool OpenLink(string url)
    {
        if (!TryParseLink(url, out int id, out string code)) return false;
        string key = "link|" + id + "|" + code;
        if (key == lastKey && Time.realtimeSinceStartup - lastAt < 10f) return true;
        lastKey = key;
        lastAt = Time.realtimeSinceStartup;
        pendingHunt = id.ToString();
        pendingHost = null;
        pendingProfile = false;
        pendingFollow = false;
        pendingLink = true;
        pendingLinkCode = code;
        pending = true;
        return true;
    }

    /// <summary>공유 링크에서 게임 번호와 참가 코드(영문·숫자만 · 대문자 · 12자까지)를 꺼낸다</summary>
    public static bool TryParseLink(string url, out int id, out string code)
    {
        id = 0; code = null;
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        string scheme = uri.Scheme.ToLowerInvariant(), host = uri.Host.ToLowerInvariant(), idText;
        if (scheme == "woopang" && host == "hunt") idText = Query(uri.Query, "id");
        else if ((scheme == "https" || scheme == "http") && (host == "woopang.com" || host == "www.woopang.com") && uri.AbsolutePath.StartsWith("/hunt/"))
            idText = uri.AbsolutePath.Substring("/hunt/".Length).TrimEnd('/');
        else return false;
        if (!int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0) { id = 0; return false; }
        var sb = new StringBuilder();
        foreach (char ch in Query(uri.Query, "c") ?? "")
            if (sb.Length < 12 && ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))) sb.Append(char.ToUpperInvariant(ch));
        code = sb.Length > 0 ? sb.ToString() : null;
        return true;
    }

    private static string Query(string query, string key)
    {
        foreach (var part in (query ?? "").TrimStart('?').Split('&'))
        {
            int eq = part.IndexOf('=');
            if ((eq >= 0 ? part.Substring(0, eq) : part) == key) return eq >= 0 ? Uri.UnescapeDataString(part.Substring(eq + 1).Replace('+', ' ')) : "";
        }
        return null;
    }

    /// <summary>새 팔로워 알림 데이터(notification_type=follow · from_username)면 내 팔로워 탭으로. 아니면 false</summary>
    public static bool OpenFollow(IDictionary<string, string> data)
    {
        if (data == null || !data.TryGetValue("notification_type", out var type) || type != "follow") return false;
        data.TryGetValue("from_username", out var from);
        OpenFollowers(from, false);
        return true;
    }

    /// <summary>내 팔로워 탭을 열고 그 사람을 맨 위에. 알림은 두 길로 와도 한 번만 · 메세지 탭의 알림 줄(again)은 누를 때마다</summary>
    public static void OpenFollowers(string fromUsername, bool again)
    {
        string key = "follow|" + fromUsername;
        if (!again && key == lastKey && Time.realtimeSinceStartup - lastAt < 10f) return;
        lastKey = key;
        lastAt = Time.realtimeSinceStartup;
        pendingFollower = fromUsername;
        pendingFollow = true;
        pendingLink = false;
        pending = true;
    }

    /// <summary>메세지 탭의 보물찾기 말풍선을 누름 — 알림과 같은 길로 (덮인 대화창·메세지창을 닫고 그 게임 상세). 같은 게임을 다시 눌러도 연다</summary>
    public static void OpenHunt(int huntId)
    {
        pendingHunt = huntId.ToString();
        pendingHost = null;
        pendingProfile = false;
        pendingFollow = false;
        pendingLink = false;
        pending = true;
    }

    /// <summary>앱을 보는 중 그 게임 소식이 오면 — 그 게임 상세를 보고 있으면 바로 다시 받고 true (알림은 띄우지 않는다)</summary>
    public static bool RefreshShowing(IDictionary<string, string> data)
    {
        if (data == null || !data.TryGetValue("hunt_id", out var hunt) || !int.TryParse(hunt, out int id)) return false;
        var c = FindAnyObjectByType<R0926HuntCard>();   // 켜진 것만 — 추가 창이 닫혀 있으면 없다
        return c != null && c.RefreshIfShowing(id);
    }

    // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 '앱을 새로 켠 것'과 같게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        pending = pendingProfile = pendingFollow = pendingLink = false;
        pendingHunt = pendingHost = pendingFollower = pendingLinkCode = lastKey = null;
        lastAt = -99f;
    }

    // 공유 링크 — 꺼진 앱이 링크로 켜지면 Application.absoluteURL, 켜진 앱이면 deepLinkActivated (로그인 링크 woopang://auth 는 LoginManager)
    private void OnEnable() => Application.deepLinkActivated += OnDeepLink;
    private void OnDisable() => Application.deepLinkActivated -= OnDeepLink;
    private void Start() => OnDeepLink(Application.absoluteURL);
    private static void OnDeepLink(string url) => OpenLink(url);

    private void Update()
    {
        if (!pending || running) return;
        if (!Ready()) { readySince = -1f; return; }
        if (readySince < 0f) readySince = Time.unscaledTime;
        if (Time.unscaledTime - readySince < 0.5f) return;                                // 시작화면이 걷히는 중
        if (!R0926HuntApi.CanCall && Time.unscaledTime - readySince < 8f) return;        // 자동 로그인이 돌아오는 중 — 끝내 안 되면 카드가 로그인 안내를 띄운다
        StartCoroutine(Route());
    }

    // 그리기 직전에 맞춘다 — 같은 프레임에 켜진 프로필도 가려진 채 한 번 그려지지 않게. 닫히면 제자리로 (다른 데서 여는 프로필은 예전 순서 그대로)
    private void LateUpdate()
    {
        var pm = ProfileManager.Instance;
        var profile = pm != null && pm.fullProfilePanel != null ? pm.fullProfilePanel.transform : null;
        if (profile == null || uploadPage == null) return;
        var page = uploadPage.transform;
        if (profileNext == null)
        {
            if (!profile.gameObject.activeInHierarchy || !uploadPage.activeInHierarchy || profile.parent != page.parent) return;
            int at = profile.GetSiblingIndex();
            if (at > page.GetSiblingIndex()) return;
            profileNext = profile.parent.GetChild(at + 1);
            profile.SetSiblingIndex(page.GetSiblingIndex());
        }
        else if (!profile.gameObject.activeSelf)
        {
            if (profileNext.parent == profile.parent)
            {
                int to = profileNext.GetSiblingIndex();
                if (profile.GetSiblingIndex() < to) to--;
                profile.SetSiblingIndex(to);
            }
            profileNext = null;
        }
    }

    private bool Ready()
    {
        if (BootOverlay.Showing) return false;
        if (guide == null) guide = FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
        return guide == null || !guide.Showing;
    }

    private IEnumerator Route()
    {
        running = true;
        pending = false;
        string hunt = pendingHunt, host = pendingHost, follower = pendingFollower;
        bool toProfile = pendingProfile, toFollowers = pendingFollow, link = pendingLink;
        string linkCode = pendingLinkCode;
        pendingFollow = pendingLink = false;
        pendingLinkCode = null;
        // 프로필·팔로우 목록·대화방은 열어 준 곳으로 돌아가지 않고 바로 닫는다 — 팔로우 목록 → 프로필, 대화방 → 메시지 목록이
        // 닫히며 앞 창을 되살려 상세 위에 겹쳤다 (10-13 대표님). 나머지 창은 뒤로가기 순서대로
        if (FollowManager.Instance != null) FollowManager.Instance.CloseAway();
        if (ProfileManager.Instance != null) ProfileManager.Instance.CloseAway();
        if (MessagePanelManager.Instance != null) MessagePanelManager.Instance.CloseAway();
        yield return CloseCovering(!toFollowers);   // 팔로워 탭은 추가 창까지 닫고 연다 (닫기를 누르면 메인으로)
        if (toFollowers)
        {
            var me = LoginManager.Instance != null ? LoginManager.Instance.CurrentUser : null;
#if UNITY_EDITOR
            if (me == null && !string.IsNullOrEmpty(R0926HuntApi.EditorTestUser)) me = new UserData { id = R0926HuntApi.EditorTestUser, username = R0926HuntApi.EditorTestUser };   // 플레이 시험 — 로그인 없이 시험 아이디로
#endif
            if (me != null && FollowManager.Instance != null) FollowManager.Instance.ShowNewFollower(me.id, me.username, follower);
        }
        else if (toProfile)
        {
            // 별점 알림 — 주최자 프로필의 보물찾기 칸을 펼쳐 별점 남기기까지 내린다
            R0926HuntProfile.ExpandNext(host, true, hunt);
            if (ProfileManager.Instance != null) ProfileManager.Instance.ShowProfile(host);
        }
        else if (card != null && int.TryParse(hunt, out int id))
        {
            if (uploadPage != null && !uploadPage.activeInHierarchy && plusButton != null) plusButton.onClick.Invoke();
            yield return null;
            yield return null;   // 추가 창이 켜지며 첫 장으로 돌아간 뒤에 넘긴다
            if (swipe != null) swipe.SwitchToPanel(2);
            float until = Time.unscaledTime + 3f;
            while (!card.isActiveAndEnabled && Time.unscaledTime < until) yield return null;
            if (card.isActiveAndEnabled) card.StartCoroutine(link ? card.OpenLink(id, linkCode) : card.OpenDetail(id));
        }
        running = false;
    }

    // 위에 덮인 창을 한 장씩 닫는다 — 닫히는 움직임이 끝날 때까지 기다리고, 추가 창(stopAtUpload)이나 안 닫히는 창에서 멈춘다
    private IEnumerator CloseCovering(bool stopAtUpload)
    {
        var back = BackButtonHandler.Instance;
        if (back == null) yield break;
        for (int i = 0; i < 8; i++)
        {
            var top = back.Top;
            // 추가 창에서 멈춘다 — 창 자신만이 아니라 도크 X 덮개(ClosePx0926)도 뒤로가기에 올라 있다
            if (top == null || (stopAtUpload && uploadPage != null && top.transform.IsChildOf(uploadPage.transform))) yield break;
            back.HandleBackPress();
            float until = Time.unscaledTime + 1.5f;
            while (back.Top == top && Time.unscaledTime < until) yield return null;
            if (back.Top == top) yield break;
        }
    }
}
