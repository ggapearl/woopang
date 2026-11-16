using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가까이 모달(R0926NearModal)이 카드에 담은 물체에 붙인다 — 렌더러를 빈 레이어(28~31)로 옮겨 AR 카메라에서 빼고,
/// 놓거나 꺼지면(풀로 돌아감 · 지움) 원래 레이어로 되돌린다. 물체에서 떨어져 나간 렌더러(축하 화면이 데려간 보물)는 그 자리에서 되돌린다.
/// </summary>
[DisallowMultipleComponent]
public sealed class R0926NearHold : MonoBehaviour
{
    /// <summary>카드에 담긴 물체의 화면 표시 대상 — 화면 밖 화살표 · 박스(OffScreenIndicator)가 건너뛴다 (물체가 AR 화면에 없다)</summary>
    public static readonly HashSet<Target> Held = new HashSet<Target>();

    private readonly Dictionary<GameObject, int> was = new Dictionary<GameObject, int>();
    private readonly List<Renderer> found = new List<Renderer>();
    private readonly List<GameObject> gone = new List<GameObject>();
    private Target target;

    /// <summary>지금 옮겨 둔 레이어 (-1 = 원래대로)</summary>
    public int Layer { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Held.Clear();

    public void Take(int layer)
    {
        if (!isActiveAndEnabled) return;   // 꺼진 물체(풀)에 걸어 두면 다시 꺼낼 때 AR 에 안 보인다
        if (Layer < 0)
        {
            target = GetComponentInChildren<Target>(true);
            if (target != null) Held.Add(target);
        }
        Layer = layer;
        Scan();
    }

    /// <summary>새로 생긴 렌더러(받는 중이던 3D 모델)도 옮기고, 물체 밖으로 나간 것은 되돌린다</summary>
    public void Scan()
    {
        if (Layer < 0) return;
        GetComponentsInChildren(true, found);
        foreach (var r in found)
        {
            var g = r.gameObject;
            if (!was.ContainsKey(g)) was[g] = g.layer;
            if (g.layer != Layer) g.layer = Layer;
        }
        gone.Clear();
        foreach (var kv in was)
            if (kv.Key == null || !kv.Key.transform.IsChildOf(transform)) gone.Add(kv.Key);
        foreach (var g in gone)
        {
            if (g != null) g.layer = was[g];
            was.Remove(g);
        }
    }

    public void Release()
    {
        foreach (var kv in was)
            if (kv.Key != null) kv.Key.layer = kv.Value;
        was.Clear();
        if (target != null) Held.Remove(target);
        target = null;
        Layer = -1;
    }

    private void OnDisable() => Release();
}
