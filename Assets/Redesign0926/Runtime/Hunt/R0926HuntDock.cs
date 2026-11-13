using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도크 '보물찾기' 칸 — 11-09 대표님 「'보물찾기' 기능을 하단 '추가' 버튼과 '메세지' 버튼 사이에 … 근처에 있는 '보물' 리스트가 적당히 보이도록 하고,
/// '보물숨기기' 기능도 '추가' 창 말고 여기에」 · 「'보물찾기' 버튼에는 좌측 위에 new 배지를 달아뒀다가 일주일 후에 안보이도록」.
/// 보물찾기 카드는 추가 창 안에 그대로 둔다(키보드 위 입력줄 · 알림으로 여는 길을 같이 쓴다) — 이 칸은 그 창을 '보물찾기 한 장'으로 연다.
/// 열려 있는 동안 도크 X 는 이 칸 자리에, '추가' 칸은 다시 보인다(누르면 같은 창이 장소 · 3D모델 두 장으로).
/// </summary>
public class R0926HuntDock : MonoBehaviour
{
    [SerializeField] private Button plusButton;            // 도크 '추가' — 창을 여는 로직(+ 숨기기 · X 켜기 · 메시지 창 닫기)은 그 버튼 것 그대로
    [SerializeField] private GameObject uploadPage;
    [SerializeField] private SwipePanelController swipe;
    [SerializeField] private RectTransform closeX;          // 추가 창 도크 거울의 X — 보물찾기로 열면 이 칸 자리로
    [SerializeField] private R0926SlideIn sheet;            // 추가 ↔ 보물찾기를 바꿀 때 카드 틀이 살짝 올라온다
    [SerializeField] private GameObject newBadge;
    [SerializeField] private float addSlot = 0.3f, huntSlot = 0.5f;

    private const string FirstSeenKey = "r0926_hunt_dock_seen";
    private const double NewDays = 7;
    private const float RiseDistance = 56f, RiseTime = 0.24f;

    public static R0926HuntDock Instance { get; private set; }

    private bool opening;

    private void Awake()
    {
        Instance = this;
        if (plusButton != null) plusButton.onClick.AddListener(PlusTapped);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable() => RefreshBadge();

    /// <summary>도크 '보물찾기' 칸을 누름 (버튼의 OnClick) — 닫혀 있으면 보물찾기 한 장으로 열고, 추가 두 장이 열려 있으면 보물찾기로 바꾼다</summary>
    public void Open()
    {
        if (swipe == null || uploadPage == null || plusButton == null) return;
        if (uploadPage.activeInHierarchy)
        {
            if (swipe.AloneShowing) return;
            swipe.HomePanel = 2;
            swipe.SwitchToPanel(2);
            Rise();
            return;
        }
        swipe.HomePanel = 2;   // 창이 켜지며 첫 장으로 되돌리는 곳들(UploadPageResetHelper · 카드 넘기기)이 이 장으로 간다
        opening = true;
        try { plusButton.onClick.Invoke(); }
        finally { opening = false; }
    }

    // '추가'를 누름 — 보물찾기 한 장으로 열려 있었으면 같은 창을 추가 두 장으로 (버튼 자신의 OnClick 이 + 를 다시 숨긴다)
    private void PlusTapped()
    {
        if (opening || swipe == null || uploadPage == null || !uploadPage.activeInHierarchy || !swipe.AloneShowing) return;
        swipe.HomePanel = 0;
        swipe.ResetToFirstPanel();
        Rise();
    }

    private void Rise()
    {
        if (sheet != null && sheet.isActiveAndEnabled) sheet.Rise(RiseDistance, RiseTime);
    }

    private void LateUpdate()
    {
        if (swipe == null || uploadPage == null) return;
        bool open = uploadPage.activeInHierarchy;
        if (!open) swipe.HomePanel = 0;   // 닫히면 다음엔 '추가'로 (카드 넘기기는 늘 켜진 SwipePanel 에 있어 스스로 모른다)
        bool hunt = open && swipe.AloneShowing;
        // 보물찾기로 열려 있는 동안 '추가' 칸은 보인다 (+ 는 창을 열 때 스스로 꺼진다)
        if (hunt && plusButton != null && !plusButton.gameObject.activeSelf) plusButton.gameObject.SetActive(true);
        if (closeX != null && open)
        {
            // 가로 화면이면 도크가 오른쪽 세로 막대 — 칸은 위에서부터 (R0926Orientation 이 거울 칸을 옮긴 뒤에도 이 칸 자리로)
            float slot = hunt ? huntSlot : addSlot;
            var a = R0926Orientation.IsLandscape ? new Vector2(0.5f, 1f - slot) : new Vector2(slot, 0.5f);
            if (closeX.anchorMin != a || closeX.anchorMax != a)
            {
                closeX.anchorMin = a;
                closeX.anchorMax = a;
            }
        }
    }

    // ── NEW 배지 — 처음 본 때부터 7일 ─────────────────────────
    private void RefreshBadge()
    {
        if (newBadge == null) return;
        newBadge.SetActive(IsNew());
    }

    public static bool IsNew()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long first = 0;
        try
        {
            string v = PlayerPrefs.GetString(FirstSeenKey, "");
            if (!long.TryParse(v, out first) || first <= 0)
            {
                first = now;
                PlayerPrefs.SetString(FirstSeenKey, now.ToString());
                PlayerPrefs.Save();
            }
        }
        catch (Exception) { return false; }
        return now - first < (long)(NewDays * 86400);
    }

#if UNITY_EDITOR
    /// <summary>플레이 시험 — 처음 본 때를 바꿔 본다 (0 이면 지움)</summary>
    public static void EditorSetFirstSeen(long unixSeconds)
    {
        if (unixSeconds <= 0) PlayerPrefs.DeleteKey(FirstSeenKey);
        else PlayerPrefs.SetString(FirstSeenKey, unixSeconds.ToString());
        if (Instance != null) Instance.RefreshBadge();
    }
#endif
}
