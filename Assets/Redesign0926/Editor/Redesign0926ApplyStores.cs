using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Redesign0926
{
    /// <summary>
    /// 근처 가게 매니저 — 11-18 대표님 N8a (소상공인 상가정보 · 「공공데이터」 칩).
    ///  · 씬 맨 바깥에 StoreManager 하나 — 오브젝트 본(samplePrefab) · 화살표만 본(indicatorOnlyPrefab)은 관광공사 매니저와 같은 것
    ///  · 칩(FilterManager) · 목록(PlaceListManager) · 확대/축소(ARObjectZoomController)에 잇는다 (docs/claude/data-managers.md 연결 체크리스트)
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static void ApplyStoreManager(Scene scene, List<string> log)
        {
            T Find<T>() where T : Component
            {
                foreach (var go in scene.GetRootGameObjects())
                {
                    var c = go.GetComponentInChildren<T>(true);
                    if (c != null) return c;
                }
                return null;
            }

            var tour = Find<TourAPIManager>();
            if (tour == null) { log.Add("가게 매니저: TourAPIManager 없음"); return; }
            var store = Find<StoreManager>();
            if (store == null)
            {
                var go = new GameObject("StoreManager");
                SceneManager.MoveGameObjectToScene(go, scene);
                store = go.AddComponent<StoreManager>();
            }

            var tso = new SerializedObject(tour);
            var so = new SerializedObject(store);
            var sample = tso.FindProperty("samplePrefab").objectReferenceValue;
            var indicator = tso.FindProperty("indicatorOnlyPrefab").objectReferenceValue;
            so.FindProperty("samplePrefab").objectReferenceValue = sample;
            so.FindProperty("indicatorOnlyPrefab").objectReferenceValue = indicator;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(store);

            int wired = 0;
            void Wire(Component c)
            {
                if (c == null) return;
                var cso = new SerializedObject(c);
                var p = cso.FindProperty("storeManager");
                if (p == null) return;
                p.objectReferenceValue = store;
                cso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(c);
                wired++;
            }
            Wire(Find<FilterManager>());
            Wire(Find<PlaceListManager>());
            Wire(Find<ARObjectZoomController>());
            log.Add("가게 매니저: 본 " + (sample != null ? sample.name : "없음") + " · 화살표 " + (indicator != null ? indicator.name : "없음") + " · 연결 " + wired + "/3");
        }
    }
}
