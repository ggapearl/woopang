using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using Google.XR.ARCoreExtensions;

namespace Redesign0926
{
    /// <summary>
    /// 나침반 모드 (11-06 대표님 「'나침반 모드' 로 해서 어두운환경에서는 자연스럽게 이 모드로 반영해서 오브젝트가 위치를 잡도록」).
    /// LoadingManager 오브젝트에 R0926CompassMode 를 붙이고 카메라 · 카메라 추적(TrackedPoseDriver) · AR 세션 · 지구 추적을 잇는다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static void ApplyCompass(Scene scene, List<string> log)
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

            var lm = Find<LoadingManager>();
            if (lm == null) { log.Add("나침반 모드: LoadingManager 없음"); return; }
            Camera cam = null;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                if (c.gameObject.scene == scene && c.CompareTag("MainCamera")) { cam = c; break; }
            if (cam == null) { log.Add("나침반 모드: Main Camera 없음"); return; }
            Behaviour pd = null;
            foreach (var b in cam.GetComponents<Behaviour>())
                if (b.GetType().Name.Contains("PoseDriver")) { pd = b; break; }
            var session = Find<ARSession>();
            var earth = Find<AREarthManager>();

            var cm = lm.GetComponent<R0926CompassMode>();
            if (cm == null) cm = lm.gameObject.AddComponent<R0926CompassMode>();
            var so = new SerializedObject(cm);
            so.FindProperty("arCamera").objectReferenceValue = cam;
            so.FindProperty("poseDriver").objectReferenceValue = pd;
            so.FindProperty("session").objectReferenceValue = session;
            so.FindProperty("earth").objectReferenceValue = earth;
            so.FindProperty("loading").objectReferenceValue = lm;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cm);
            log.Add("나침반 모드: " + lm.name + " · 카메라 " + cam.name + " · 추적 " + (pd != null ? pd.GetType().Name : "없음")
                + " · 세션 " + (session != null ? "있음" : "없음") + " · 지구 " + (earth != null ? "있음" : "없음"));
        }
    }
}
