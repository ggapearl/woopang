using UnityEngine;

/// <summary>
/// 사람 오브젝트를 기기 AR 에서 그 사람 자리에 둔다 (11-17 D1).
/// 예전엔 사람 오브젝트(P2P_Avatar)에 위치 앵커가 없어 기기에서는 모두 AR 을 시작한 자리(원점)에 놓였다 —
/// 에디터만 카메라 기준으로 옮겨(P2PManager.UpdateAvatarPositionInEditor) 플레이 시험에선 멀쩡해 보였다.
/// 처음 자리에 지오스페이셜 앵커(CustomARGeospatialCreatorAnchor)를 두고, 그 뒤 걸어 움직인 만큼은 원(P2PUserInfo)을 앵커 안에서 미끄러지듯 옮긴다
/// (앵커 축 x 동 · y 위 · z 북. 앵커를 새로 만들면 붙을 때까지 잠깐 숨는다 — 5초마다 오는 자리마다 깜빡이지 않게). 50m 넘게 가면 그 자리에 앵커를 새로.
/// 높이는 내 눈높이 — 지도 친구 자리엔 높이가 없고 휴대폰 GPS 높이는 수십 m 틀린다 (계단 · 언덕으로 내 높이가 3m 넘게 달라지면 다시 맞춘다).
/// 에디터는 예전대로 P2PManager 가 카메라 기준으로 놓는다
/// </summary>
[DisallowMultipleComponent]
public class P2PAvatarPlacer : MonoBehaviour
{
    public const float ReanchorMeters = 50f;
    public const float RelevelMeters = 3f;

    /// <summary>두 좌표 사이 (m · x 동 · z 북) — 수백 m 안에서는 평면으로 셈해도 1% 안</summary>
    public static Vector3 Enu(double lat0, double lon0, double lat, double lon)
    {
        const double M = 111320.0;
        return new Vector3((float)((lon - lon0) * M * System.Math.Cos(lat0 * System.Math.PI / 180.0)), 0f, (float)((lat - lat0) * M));
    }

    /// <summary>앵커 자리에서 이만큼이면 원만 옮긴다 — 넘으면 앵커를 새로</summary>
    public static bool Glide(Vector3 offset) => new Vector2(offset.x, offset.z).magnitude <= ReanchorMeters;

    /// <summary>원 높이 — 내 높이가 3m 넘게 달라졌을 때만 따라간다 (휴대폰을 들고 내리는 것엔 따라가지 않는다)</summary>
    public static float Level(float currentY, float wantY) => Mathf.Abs(wantY - currentY) > RelevelMeters ? wantY : currentY;

    /// <summary>지금 그 사람 자리(원) — 기기에선 앵커(처음 본 자리) 안에서 원을 옮기므로 오브젝트 자리와 다르다 (가까이 모달이 거리를 잴 때). 에디터는 오브젝트 자리 그대로</summary>
    public Vector3 Position
    {
        get
        {
#if !UNITY_EDITOR
            if (disc != null) return disc.position;
#endif
            return transform.position;
        }
    }

    /// <summary>이 사람 자리 — 처음 · 풀에서 다시 꺼낸 것 · 50m 넘게 간 것은 앵커를 새로, 아니면 원만 옮긴다. myAlt 는 내 높이(WGS84), 모르면 NaN</summary>
    public void Place(double lat, double lon, double myAlt)
    {
#if !UNITY_EDITOR
        if (!Ready()) return;
        if (!anchored)
        {
            if (double.IsNaN(myAlt)) { wantLat = lat; wantLon = lon; pending = true; return; }
            Anchor(lat, lon, myAlt);
            return;
        }
        Vector3 d = Enu(lat0, lon0, lat, lon);
        if (!Glide(d)) { Anchor(lat, lon, double.IsNaN(myAlt) ? alt0 + target.y : myAlt); return; }
        d.y = double.IsNaN(myAlt) ? target.y : Level(target.y, (float)(myAlt - alt0));
        target = d;
#endif
    }

    /// <summary>풀로 돌아갈 때 — 앵커를 지우고(따로 남지 않게) 매니저 밑으로 되돌린다</summary>
    public void Release(Transform home)
    {
#if !UNITY_EDITOR
        anchored = pending = false;
        target = current = Vector3.zero;
        if (disc != null) disc.localPosition = Vector3.zero;
        if (anchor != null) anchor.ReleaseAnchor();
#endif
        if (home != null && transform.parent != home) transform.SetParent(home, false);
    }

#if !UNITY_EDITOR
    private const float GlideTau = 0.6f;
    private const float TickEvery = 2f;

    private CustomARGeospatialCreatorAnchor anchor;
    private Transform disc;
    private double lat0, lon0, alt0;   // 앵커 자리
    private double wantLat, wantLon;   // 내 높이를 몰라 앵커를 미뤄 둔 자리
    private Vector3 target, current;   // 앵커에서 원까지 (m)
    private bool anchored, pending;
    private float tickAt;

    private bool Ready()
    {
        if (anchor == null && !TryGetComponent(out anchor)) anchor = gameObject.AddComponent<CustomARGeospatialCreatorAnchor>();
        if (disc == null)
        {
            var info = GetComponentInChildren<P2PUserInfo>(true);
            if (info != null) disc = info.transform;
        }
        return disc != null;
    }

    private void Anchor(double lat, double lon, double alt)
    {
        lat0 = lat; lon0 = lon; alt0 = alt;
        target = current = Vector3.zero;
        anchored = true;
        pending = false;
        disc.localPosition = Vector3.zero;
        anchor.SetCoordinatesAndCreateAnchor(lat, lon, alt);
    }

    private void Update()
    {
        if (!anchored && !pending) return;
        if (Time.unscaledTime >= tickAt) { tickAt = Time.unscaledTime + TickEvery; Tick(); }
        if (!anchored || disc == null) return;
        if (current != target)
        {
            current = Vector3.Lerp(current, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / GlideTau));
            if ((current - target).sqrMagnitude < 1e-4f) current = target;
        }
        float s = transform.localScale.x;   // 핀치 줌 배율이 곱해진 크기 — 원은 실제 m 만큼 떨어져 있게
        disc.localPosition = s > 1e-4f ? current / s : current;
    }

    private void Tick()
    {
        double myAlt = GeoHere.TryGet(out _, out _, out double a) ? a : double.NaN;
        if (pending)
        {
            if (!double.IsNaN(myAlt)) Anchor(wantLat, wantLon, myAlt);
            return;
        }
        if (!double.IsNaN(myAlt)) target.y = Level(target.y, (float)(myAlt - alt0));
        // 붙지 못했거나 놓친 앵커만 다시 — 붙어 있거나 다시 해 보는 중이면 건드리지 않는다 (ar-object-spawn.md 2.1)
        if (anchor != null && anchor.HasCoordinates && !anchor.IsAnchorCreated && !anchor.IsRetrying) anchor.RecreateAnchor();
    }
#endif
}
