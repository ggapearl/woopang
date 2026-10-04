using System;
using UnityEngine;

/// <summary>
/// 창이 열려 있는 동안 도크의 그 칸 버튼(아이콘·이름·배지)을 감춘다 — 그 자리에는 창 쪽의 X '닫기'만 보이게.
/// 예전엔 메시지 칸에서 봉투 아이콘·'메시지' 위에 X·'닫기'가 겹쳐 '메닫기'로 보였다.
/// 버튼을 끄지 않고 투명하게만 한다 (MessagePanelManager 가 이름으로 버튼을 찾고 배지를 칠한다).
/// </summary>
public class R0926DockSlotSwap : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public CanvasGroup button;
        public GameObject[] panels;
    }

    [SerializeField] private Slot[] slots;

    private void LateUpdate()
    {
        if (slots == null) return;
        foreach (var s in slots)
        {
            if (s == null || s.button == null || s.panels == null) continue;
            bool open = false;
            foreach (var p in s.panels)
                if (p != null && p.activeInHierarchy) { open = true; break; }
            float a = open ? 0f : 1f;
            if (s.button.alpha != a)
            {
                s.button.alpha = a;
                s.button.blocksRaycasts = !open;
                s.button.interactable = !open;
            }
        }
    }
}
