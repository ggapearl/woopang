using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보물찾기 알림(hunt_new · start · found · message · extend · end)을 누르면 — '추가' 창 보물찾기 카드의 그 게임 상세로.
/// 끝 알림에 review=1(별점 자격)이 붙어 있으면 주최자 프로필로.
/// 알림은 시작화면보다 먼저 올 수 있다 — 받아 두었다가 시작화면·첫 안내가 걷히고 로그인이 돌아오면 연다.
/// 위에 덮인 창(목록·메시지·프로필 …)은 뒤로가기 순서대로 닫고 연다 (추가 창은 그대로 둔다).
/// FirebaseNotification 이 부른다 — 안드로이드: 알림바에서 눌러 열림 · 앱을 보는 중 띄운 알림을 누름 / 아이폰: 눌러 열림 · 보는 중 뜬 배너를 누름.
/// 추가 창이 켜진 채 연 프로필(카드의 주최자 · 별점 알림)은 추가 창 위로 올린다 — 프로필 창이 캔버스에서 추가 창보다 앞 순서라 뒤에 가려졌다.
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
    private static string lastKey;
    private static float lastAt = -99f;

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
        pending = pendingProfile = false;
        pendingHunt = pendingHost = lastKey = null;
        lastAt = -99f;
    }

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
        string hunt = pendingHunt, host = pendingHost;
        bool toProfile = pendingProfile;
        yield return CloseCovering();
        if (toProfile)
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
            if (card.isActiveAndEnabled) card.StartCoroutine(card.OpenDetail(id));
        }
        running = false;
    }

    // 위에 덮인 창을 한 장씩 닫는다 — 닫히는 움직임이 끝날 때까지 기다리고, 추가 창이나 안 닫히는 창에서 멈춘다
    private IEnumerator CloseCovering()
    {
        var back = BackButtonHandler.Instance;
        if (back == null) yield break;
        for (int i = 0; i < 8; i++)
        {
            var top = back.Top;
            // 추가 창에서 멈춘다 — 창 자신만이 아니라 도크 X 덮개(ClosePx0926)도 뒤로가기에 올라 있다
            if (top == null || (uploadPage != null && top.transform.IsChildOf(uploadPage.transform))) yield break;
            back.HandleBackPress();
            float until = Time.unscaledTime + 1.5f;
            while (back.Top == top && Time.unscaledTime < until) yield return null;
            if (back.Top == top) yield break;
        }
    }
}
