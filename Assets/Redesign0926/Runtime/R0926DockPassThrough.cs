using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도크로 여는 창(목록 · 추가 · 메세지 · 대화 · 프로필 · 팔로우)의 어두운 바탕은 도크 위에 덮인다 — 그대로면 창이 열린 동안
/// 도크의 다른 칸이 손가락에 닿지 않았다 (칸 바꾸기 시험은 버튼을 직접 불러 몰랐다).
/// 11-08 대표님 「추가창이 열린 상태에서 → 목록버튼 터치」 · 11-09 보물찾기 칸 ↔ '추가' 칸.
/// 보이는 도크 칸 자리에서 이 창에 바탕 말고 맞을 것이 없으면 통과시킨다 — 손가락은 아래 도크 칸으로 (칸 바꾸기는 R0926DockSlotSwap).
/// 열린 창 자기 칸은 숨어 있어 통과시키지 않는다(그 자리엔 창의 X). 창 안의 확인 창 · 편집 화면처럼 위에 덮인 것이 있으면 그것이 받는다.
/// </summary>
public class R0926DockPassThrough : MonoBehaviour, ICanvasRaycastFilter
{
    private static readonly List<Graphic> graphics = new List<Graphic>();
    private bool busy;
    private int cachedFrame = -1;
    private Vector2 cachedAt;
    private bool cachedValid;

    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        // 창 안의 것을 맞혀 볼 때도 이 거름망을 거친다 — 그동안은 막지 않는다
        if (busy || !isActiveAndEnabled) return true;
        if (cachedFrame == Time.frameCount && cachedAt == sp) return cachedValid;
        cachedFrame = Time.frameCount;
        cachedAt = sp;
        cachedValid = !R0926DockSlotSwap.SlotAt(sp, eventCamera) || Covered(sp, eventCamera);
        return cachedValid;
    }

    // 바탕(이 오브젝트) 말고 그 점에 맞을 것이 이 창에 있는지
    private bool Covered(Vector2 sp, Camera cam)
    {
        busy = true;
        try
        {
            GetComponentsInChildren(false, graphics);
            foreach (var g in graphics)
            {
                if (g.gameObject == gameObject || !g.raycastTarget || g.canvasRenderer.cull) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, sp, cam) && g.Raycast(sp, cam)) return true;
            }
            return false;
        }
        finally
        {
            graphics.Clear();
            busy = false;
        }
    }
}
