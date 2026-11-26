using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가까이 모달(R0926NearModal)의 칸 카메라가 그 물체를 찍는 그 순간만 빌린다 — 11-26 대표님 「기존 발생한 오브젝트들은 근처모달로 가져오지 않고
/// 원래 AR화면 내에서 그대로 있고 근처모달에서는 해당 근처 오브젝트를 복사만 해오도록」.
/// 찍기 직전(beginCameraRendering) 렌더러를 그 칸의 레이어(29~31)로 옮기고, 멀리 흐리게(R0926DistanceFade)로 옅어진 값은 가까이에서 보는 값으로 바꿔 둔다.
/// 다 찍으면(endCameraRendering) 바로 되돌린다 — 그래서 AR 화면 · 화면 밖 화살표 · 박스 · 누르기(콜라이더)는 내내 원래대로다.
/// (1.2.70 까지는 카드에 담는 동안 물체를 빈 레이어로 옮겨 AR 화면에서 빼고 화살표 · 박스도 껐다 — 담긴 물체의 인디케이터가 안 뜬 까닭)
/// </summary>
public sealed class R0926NearHold
{
    private readonly List<Renderer> rends = new List<Renderer>();
    private readonly List<GameObject> gos = new List<GameObject>();
    private readonly List<int> layers = new List<int>();
    private readonly List<Material> mats = new List<Material>();
    private readonly List<MaterialPropertyBlock> pool = new List<MaterialPropertyBlock>();
    private readonly List<Renderer> blockOf = new List<Renderer>();
    private readonly List<int> slotOf = new List<int>();
    private MaterialPropertyBlock scratch;

    /// <summary>지금 빌려 둔 것이 있다 (찍는 중)</summary>
    public bool Lent { get; private set; }

    /// <summary>root 아래 켜진 렌더러를 layer 로 — plain 이면 멀리 흐리게 값도 가까이에서 보는 값으로 (다섯 매니저의 장소)</summary>
    public void Lend(GameObject root, int layer, bool plain)
    {
        if (Lent) Return();
        if (root == null) return;
        Lent = true;
        root.GetComponentsInChildren(false, rends);
        foreach (var r in rends)
        {
            if (r == null) continue;
            var g = r.gameObject;
            gos.Add(g);
            layers.Add(g.layer);
            if (g.layer != layer) g.layer = layer;
            if (!plain) continue;
            r.GetSharedMaterials(mats);
            for (int i = 0; i < mats.Count; i++)
            {
                if (mats[i] == null) continue;
                if (blockOf.Count >= pool.Count) pool.Add(new MaterialPropertyBlock());
                var keep = pool[blockOf.Count];
                r.GetPropertyBlock(keep, i);   // 원래 값 (멀리 흐리게 · 다른 코드가 넣은 값)
                blockOf.Add(r);
                slotOf.Add(i);
            }
            R0926DistanceFade.NearLook(r, scratch ??= new MaterialPropertyBlock(), mats);
        }
        rends.Clear();
        mats.Clear();
    }

    /// <summary>빌린 것을 그대로 되돌린다 (찍기가 끝나면 바로)</summary>
    public void Return()
    {
        if (!Lent) return;
        Lent = false;
        for (int i = 0; i < blockOf.Count; i++)
            if (blockOf[i] != null) blockOf[i].SetPropertyBlock(pool[i], slotOf[i]);
        for (int i = 0; i < gos.Count; i++)
            if (gos[i] != null && gos[i].layer != layers[i]) gos[i].layer = layers[i];
        blockOf.Clear();
        slotOf.Clear();
        gos.Clear();
        layers.Clear();
    }
}
