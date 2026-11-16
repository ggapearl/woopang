using UnityEngine;

/// <summary>
/// 장소 오브젝트 만들기는 한 프레임에 하나 — 시작화면 동안은 아무것도 (11-13 제안 · 11-14 대표님 「시작할 때 오브젝트 하나씩 불러오는 건 … 제안한대로 작업해줘」).
/// 예전엔 배분 한 번에 3D 수십 개와 화살표를 한 프레임에 만들어 시작화면이 걷힌 직후 · 필터를 바꿀 때 화면이 멈칫했다.
/// FilterManager 배분 · DataManager 상세 응답 · 교통/관광 매니저의 직접 생성이 모두 이 문을 지난다
/// </summary>
public static class SpawnPace
{
    private static int frame = -1;

    /// <summary>지금 하나 만들어도 되는지 — 시작화면이 걷혔고 이번 프레임에 아직 만든 것이 없다</summary>
    public static bool Ready => !BootOverlay.Showing && Time.frameCount != frame;

    /// <summary>미리 만들어 두기(교통 풀) — 시작화면이 걷혔고 두 프레임 넘게 아무것도 안 만들었을 때만. 장소가 하나씩 뜨는 차례를 늦추지 않는다 (11-15 오프닝 ④)</summary>
    public static bool Idle => !BootOverlay.Showing && Time.frameCount - frame > 2;

    /// <summary>이번 프레임 몫을 썼다</summary>
    public static void Took() => frame = Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch() => frame = -1;
}
