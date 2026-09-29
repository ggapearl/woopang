using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이 기기에서 사용자가 인디케이터의 X 로 숨긴 장소.
/// 메모리에만 둔다 — 백그라운드에 다녀와도 유지되고, 앱을 완전히 껐다 켜면 초기화된다.
/// 숨긴 장소는 FilterManager 배분과 각 매니저의 직접 스폰에서 모두 빠진다(3D 오브젝트·박스·화살표 전부).
/// </summary>
public static class HiddenPlaces
{
    private static readonly HashSet<string> ids = new HashSet<string>();

    /// <summary>새로 숨겨졌을 때 (매니저 고유 ID: "dm_12", "tour_345", "subway_…")</summary>
    public static event Action<string> Hidden;

    public static int Count => ids.Count;

    public static bool IsHidden(string uniqueId) => !string.IsNullOrEmpty(uniqueId) && ids.Contains(uniqueId);

    public static bool Hide(string uniqueId)
    {
        if (string.IsNullOrEmpty(uniqueId) || !ids.Add(uniqueId)) return false;
        Hidden?.Invoke(uniqueId);
        return true;
    }

    /// <summary>인디케이터가 가리키는 Target 을 숨긴다. 장소가 아니면(P2P 사용자 등) false.</summary>
    public static bool HideTarget(Target target)
    {
        return Hide(ResolveUniqueId(target));
    }

    /// <summary>Target → 매니저 고유 ID. 스폰된 3D 오브젝트·경량 인디케이터 오브젝트 양쪽을 찾는다.</summary>
    public static string ResolveUniqueId(Target target)
    {
        if (target == null) return null;
        Transform t = target.transform;
        string id;
        var dm = DataManager.Instance;
        if (dm != null && ((id = Find(dm.GetSpawnedObjects(), "dm_", t)) != null || (id = Find(dm.GetIndicatorOnlyObjects(), "dm_", t)) != null)) return id;
        var tour = TourAPIManager.Instance;
        if (tour != null && ((id = Find(tour.GetSpawnedObjects(), "tour_", t)) != null || (id = Find(tour.GetIndicatorOnlyObjects(), "tour_", t)) != null)) return id;
        var subway = SubwayManager.Instance;
        if (subway != null && ((id = Find(subway.GetSpawnedObjects(), "subway_", t)) != null || (id = Find(subway.GetIndicatorOnlyObjects(), "subway_", t)) != null)) return id;
        var train = TrainStationManager.Instance;
        if (train != null && ((id = Find(train.GetSpawnedObjects(), "train_", t)) != null || (id = Find(train.GetIndicatorOnlyObjects(), "train_", t)) != null)) return id;
        var terminal = TerminalManager.Instance;
        if (terminal != null && ((id = Find(terminal.GetSpawnedObjects(), "terminal_", t)) != null || (id = Find(terminal.GetIndicatorOnlyObjects(), "terminal_", t)) != null)) return id;
        return null;
    }

    private static string Find<K>(IEnumerable<KeyValuePair<K, GameObject>> objects, string prefix, Transform t)
    {
        if (objects == null) return null;
        foreach (var kv in objects)
            if (kv.Value != null && t.IsChildOf(kv.Value.transform)) return prefix + kv.Key;
        return null;
    }

    // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 '앱을 새로 켠 것'과 같게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        ids.Clear();
        Hidden = null;
    }
}
