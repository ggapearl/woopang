using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도크로 여는 창의 어두운 바탕 — 그림은 도크 바로 아래 한 장(R0926DockDim)이 대신 그린다. 이 그림은 손가락만 받는다
/// (밀어 닫기 · 디졸브가 바꾸는 색 · 투명도는 그대로 — R0926DockDim 이 읽는다). 대신 그리는 한 장이 없으면 예전처럼 직접 그린다.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class R0926DimSource : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper vh)
    {
        if (IsActive() && Application.isPlaying && R0926DockDim.Live) vh.Clear();
    }
}
