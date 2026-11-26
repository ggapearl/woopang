using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장소 상세(두 번 톡 · 목록 사진으로 연 창) 위 버튼 줄의 「길안내」 — 누르면 그 장소로 노치 · AR 화면 아래 3D 화살표 안내를 시작하고 상세를 닫는다.
/// 목록에서 연 창이면 목록도, 「근처 장소」 카드에서 연 창이면 카드도 내린다 (줄을 눌러 안내할 때와 같은 AR 화면 · 카드는 11-26). 11-16 대표님 「더블탭하고 나서 발생하는 화면에 메뉴가 여러개 있잖아?
/// 그때 거기에 화살표 버튼을 만들어서 안내하도록」.
///  · 자리를 아는 장소에만 보인다 — AR 오브젝트는 그 자리(Target · 앵커), AR 밖 장소(목록 사진으로 연 창)는 목록 줄의 자리(R0926PlaceInfo.Mark)
///  · 사람(프로필)은 이 창을 쓰지 않는다 — 사람에게는 길안내를 하지 않는다 (11-16 「개인 프로필의 경우 따라가기 하지말자(스토킹 위험 방지)」)
///  · 「발견한 장소」 카드가 그 장소를 띄우고 있으면(바로 앞) 숨는다 — 안내를 시작해도 카드가 바로 이어 받아 끝난다
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class R0926DetailGuide : MonoBehaviour
{
    [SerializeField] private Button button;

    private CanvasGroup group;
    private int shownState = -1;   // -1 모름 · 0 숨김 · 1 보임

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnTap);
    }

    private void OnEnable()
    {
        shownState = -1;
        Refresh();
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        int want = Resolve(out _, out _, out _, out _) ? 1 : 0;
        if (want == shownState || group == null) return;
        shownState = want;
        group.alpha = want;
        group.blocksRaycasts = want == 1;
        group.interactable = want == 1;
    }

    private void OnTap()
    {
        if (!Resolve(out float lat, out float lon, out Target target, out string name)) return;
        bool guide = R0926Notch.Guide(lat, lon, target, name);   // 노치가 있는 아이폰은 노치에도
        if (R0926GuideArrow.Guide(lat, lon, target, name)) guide = true;
        if (!guide) return;
        if (UIFeedbackManager.Instance != null) UIFeedbackManager.Instance.TriggerLightHaptic();
        DoubleTap3D.CloseShown();
        var rows = FindAnyObjectByType<R0926PlaceRows>();   // 목록(· 지도)에서 연 창 — 목록도 내린다
        if (rows != null) rows.CloseForGuide();
        R0926NearModal.CloseForGuide();   // 「근처 장소」 카드에서 연 창 — 카드도 함께 (11-26 대표님)
    }

    /// <summary>지금 상세가 보여 주는 장소의 자리 · 이름 · AR 오브젝트 — 자리를 모르거나 바로 앞(가까이 카드)이면 false</summary>
    public static bool Resolve(out float lat, out float lon, out Target target, out string name)
    {
        lat = lon = 0f;
        target = null;
        name = null;
        var dt = DoubleTap3D.Shown;
        if (dt == null) return false;
        if (R0926PlaceInfo.IsHost(dt)) return R0926PlaceInfo.HostPlace(out lat, out lon, out name);
        target = dt.GetComponentInChildren<Target>(true);
        if (target == null) target = dt.GetComponentInParent<Target>();
        name = dt.GetName();
        if (target != null && (target.gpsLatitude != 0 || target.gpsLongitude != 0))
        {
            lat = (float)target.gpsLatitude;
            lon = (float)target.gpsLongitude;
        }
        else
        {
            // 관광공사 · 교통 오브젝트는 자리를 앵커에만 둔다
            var anchor = dt.GetComponentInParent<CustomARGeospatialCreatorAnchor>();
            if (anchor == null) anchor = dt.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true);
            if (anchor != null) { lat = (float)anchor.Latitude; lon = (float)anchor.Longitude; }
        }
        if (string.IsNullOrEmpty(name) && target != null) name = target.PlaceName;
        if (lat == 0f && lon == 0f) return false;
        return target == null || !R0926NearModal.FoundHas(target);
    }
}
