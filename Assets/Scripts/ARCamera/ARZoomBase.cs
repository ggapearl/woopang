using UnityEngine;

/// <summary>
/// 핀치 줌의 기준 크기 — 오브젝트 맨 위가 처음 가졌던 크기 (프리팹 맨 위가 2 면 2).
/// 줌은 이 크기에 배율을 곱한다. ARObjectZoomController.Fit 이 처음 맞출 때 붙인다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ARZoomBase : MonoBehaviour
{
    private Vector3 scale;
    private bool known;

    public Vector3 Scale
    {
        get
        {
            if (!known) { scale = transform.localScale; known = true; }
            return scale;
        }
    }
}
