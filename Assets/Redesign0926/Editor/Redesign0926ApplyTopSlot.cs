using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Redesign0926
{
    /// <summary>
    /// 노치 아래 한 줄 — 상태 알약(주변 장소 개수 · 로딩 안내) · 하늘 칩 · 확대/축소 표시가 같은 자리를 쓴다. 한 번에 하나만 보이게.
    ///  · 확대/축소 표시를 노치 바로 아래 알약 줄로 — 10-24 대표님 「화면확대하거나 축소할 때 발생하는 확대축소 레이아웃도 조금 올려줘.
    ///    아이폰의 경우 노치랑 겹치지만 않으면 돼. 안드로이드도 잘 그렇게 검토해서」. 예전엔 화면 위에서 400 고정(안전영역을 안 봤다) —
    ///    이제 안전영역(아이폰 노치 · 다이내믹 아일랜드, 안드로이드 상태바) 바로 아래, 상태 알약과 같은 가운데 줄
    ///  · 확대/축소 표시가 떠 있는 동안 상태 알약 · 하늘 칩은 숨는다 — 장소 상세가 열려 있는 동안도 (상세 아이콘 줄을 그 바로 아래로 올렸다 — ApplyDetailLift)
    ///    표시는 핀치하자마자 뜨므로 숨는 것도 그 프레임에 곧장 (서서히 사라지면 0.2초쯤 글자가 겹쳤다). 다시 나올 땐 천천히
    /// 상태 알약 · 로딩 패널은 0930 단계(StatusPills)가 만든 것을 쓰므로 그 뒤에 돈다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float ZoomBox = 120f;   // 확대/축소 표시 — 틀 그림과 배율 글자가 같은 칸에 겹친다

        private static void ApplyTopSlot(Transform root, List<string> log)
        {
            var zoom = FindInScene<ZoomIndicator>(root.gameObject);
            if (zoom != null)
            {
                foreach (Transform c in zoom.transform)   // ZoomIcon · ZoomText
                {
                    SetRect((RectTransform)c, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(0, -(14 + (PillH - ZoomBox) / 2f)), new Vector2(ZoomBox, ZoomBox));
                    var inset = Ensure<R0926SafeInset>(c.gameObject);
                    inset.SetEdge(R0926SafeInset.Edge.Top);
                    EditorUtility.SetDirty(inset);
                }
            }
            else log.Add("노치 아래 줄: ZoomIndicator 없음");

            var full = Find(root, "FullScreenPanel");
            var waitFor = new[] { zoom != null ? zoom.gameObject : null, full };

            // 상태 알약 — 자기 CanvasGroup 알파로 나타나고 사라지므로(ObjectCountUI · R0926StatusPill) 겹치지 않게 그림 조각마다 따로 숨긴다
            var oc = FindInScene<ObjectCountUI>(root.gameObject);
            if (oc != null) foreach (Transform c in oc.transform) HideWhile(c.gameObject, waitFor);   // Back · Text · Ic0926
            else log.Add("노치 아래 줄: ObjectCountUI 없음");
            var lp = Find(root, "LoadingPanel");
            if (lp != null)
                foreach (var n in new[] { "PillBg0926", "LoadingText ", "LoadingSpinner " })
                {
                    var g = Find(lp.transform, n);
                    if (g != null) HideWhile(g, waitFor);
                }

            var marker = Find(root, "Redesign0926Marker");
            var sky = marker != null ? marker.GetComponent<R0926SkyWeather>() : null;
            if (sky != null)
            {
                var so = new SerializedObject(sky);
                var arr = so.FindProperty("chipWaitFor");
                arr.arraySize = waitFor.Length;
                for (int i = 0; i < waitFor.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = waitFor[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("top slot ok");
        }

        private static void HideWhile(GameObject go, GameObject[] panels)
        {
            var so = new SerializedObject(Ensure<R0926HideWhile>(go));
            so.FindProperty("hideAtOnce").boolValue = true;
            var arr = so.FindProperty("panels");
            arr.arraySize = panels.Length;
            for (int i = 0; i < panels.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
