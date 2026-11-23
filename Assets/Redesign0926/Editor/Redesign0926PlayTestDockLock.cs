using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Redesign0926
{
    /// <summary>
    /// 11-16 B3 — 더블탭 정보 창이 떠 있는 동안 하단 도크 칸이 눌리지 않는다 (대표님 「하단 메뉴가 터치되면 안되는데 현재는 되고 있거든?」).
    ///  · docklock : 그 칸 자리를 실제 터치처럼 맞혀 보고(맨 위에 맞는 것) 눌러 본다 — 창이 떠 있으면 도크 바탕이 받고, 닫으면 다시 칸이 받는다.
    ///               칸 모양(투명도 · 눌림 막힘 색)은 그대로.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static readonly string[] DlSlots = { "List_Button", "PlusButton", "Hunt_Button0926", "Message_Button", "MiniProfile" };

        private static RectTransform DlSlot(string name)
        {
            var d = GameObject.Find("Dock0926");
            var t = d != null ? FindDeep(d.transform, name) : null;
            return t != null && t.gameObject.activeInHierarchy ? t as RectTransform : null;
        }

        private static Transform FindDeep(Transform p, string name)
        {
            if (p.name == name) return p;
            foreach (Transform c in p)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // 그 칸 가운데를 실제 터치처럼 — 맨 위에 맞는 것에 누름을 보낸다 (EventSystem 이 하는 것과 같게: 받을 것을 위로 찾아 올라감)
        private static string DlTap(RectTransform rt)
        {
            var es = EventSystem.current;
            if (rt == null || es == null) return "-";
            var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pe = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center)) };
            var hits = new List<RaycastResult>();
            es.RaycastAll(pe, hits);
            if (hits.Count == 0) return "없음";
            var go = hits[0].gameObject;
            pe.pointerCurrentRaycast = pe.pointerPressRaycast = hits[0];
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
            pe.pointerPress = handler;
            pe.rawPointerPress = go;
            pe.eligibleForClick = true;
            if (handler != null)
            {
                ExecuteEvents.Execute(handler, pe, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(handler, pe, ExecuteEvents.pointerClickHandler);
            }
            return go.name + (handler != null ? " → " + handler.name : " (누름 받는 것 없음)");
        }

        private static void PlanDockLock()
        {
            DsBoot();
            DsPick(1);
            Do(() => log.Add($"  정보 창 열 큐브: {(dsTargets.Count > 0 ? "id " + DsField(dsTargets[0], "id") : "없음")}"));

            HitCheck(() => DlSlot("List_Button"), false, "창이 없으면 도크 「목록」 칸이 손가락을 받는다 (시험 전 확인)");

            Do(() =>
            {
                dsCur = dsTargets.Count > 0 ? dsTargets[0] : null;
                if (dsCur == null) return;
                typeof(DoubleTap3D).GetField("isCooldown", DsF).SetValue(dsCur, false);
                typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", DsF).Invoke(dsCur, null);
            });
            Wait(() => DoubleTap3D.Shown != null, 5f, "정보 창 열림");
            Sleep(0.6f);
            foreach (var n in DlSlots)
            {
                string slot = n;
                Do(() => { if (DlSlot(slot) == null) log.Add($"  ({slot} 칸 안 보임 — 건너뜀)"); });
                HitCheck(() => DlSlot(slot) ?? DlSlot("List_Button"), true, $"정보 창이 떠 있으면 도크 「{slot}」 자리를 눌러도 그 칸이 아니라 도크 바탕이 받는다");
            }
            Check(() =>
            {
                foreach (var n in DlSlots)
                {
                    var rt = DlSlot(n);
                    var g = rt != null ? rt.GetComponent<CanvasGroup>() : null;
                    if (g != null && (g.alpha < 0.99f || !g.interactable)) return false;
                }
                return true;
            }, "칸 모양은 그대로 — 옅어지거나 눌림 막힘 색으로 바뀌지 않는다");

            Do(() => log.Add("  창 떠 있을 때 「목록」 자리 누름: " + DlTap(DlSlot("List_Button"))));
            Sleep(0.6f);
            Check(() => !Active("ListPanel") && DoubleTap3D.Shown != null, "그 자리를 눌러도 목록이 열리지 않고 정보 창은 그대로");
            Do(() => log.Add("  창 떠 있을 때 「메세지」 자리 누름: " + DlTap(DlSlot("Message_Button"))));
            Sleep(0.6f);
            Check(() => !Active("MessagePanel") && DoubleTap3D.Shown != null, "「메세지」 자리도 — 메세지 창이 열리지 않는다");
            Shot("docklock_open");

            Do(() => { if (dsCur != null) dsCur.closeButton.onClick.Invoke(); });
            Wait(() => DoubleTap3D.Shown == null, 3f, "정보 창 닫힘");
            Sleep(0.5f);
            HitCheck(() => DlSlot("List_Button"), false, "정보 창을 닫으면 도크 「목록」 칸이 다시 손가락을 받는다");
            HitCheck(() => DlSlot("Message_Button"), false, "「메세지」 칸도 다시");
            Do(() => log.Add("  닫은 뒤 「목록」 누름: " + DlTap(DlSlot("List_Button"))));
            Wait(() => Active("ListPanel"), 3f, "목록 열림");
            Check(() => Active("ListPanel"), "닫은 뒤에는 「목록」을 누르면 목록이 열린다 (예전대로)");
            CloseAll();
        }
    }
}
