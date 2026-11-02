using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 에디터 화면을 PNG로 저장한다. AI 에이전트가 편집 결과를 눈으로 확인하기 위한 도구.
    /// 수치(RectTransform 값)만으로는 "겹쳤는지 / 잘렸는지"를 알 수 없어서 필요하다.
    /// 런타임 빌드에는 포함되지 않는다(Editor 폴더).
    /// </summary>
    public static class DevViewCapture
    {
        private const int WIDTH = 900;
        private const int HEIGHT = 1600;   // 세로 앱이므로 세로 비율

        /// <summary>
        /// 트리거 파일이 있으면 캡처한다. 메뉴 대신 이 방식을 쓰는 이유:
        /// ExecuteMenuItem은 메뉴 등록 타이밍을 타고 모달 대화상자가 에디터를 멈춰
        /// 자동화에서 응답이 막힌다. 리컴파일은 항상 확실하게 걸린다.
        /// 사용법: 트리거 파일 생성 → 스크립트 리컴파일 → PNG 확인
        /// </summary>
        // 프로젝트 루트 기준 — Unity 프로세스의 임시폴더 환경변수에 의존하지 않는다
        private static string TriggerPath =>
            Path.Combine(Directory.GetCurrentDirectory(), ".woopang_capture_trigger");

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            bool armed = File.Exists(TriggerPath);
            Debug.Log($"[DevViewCapture] 훅 실행됨. trigger={TriggerPath} exists={armed}");
            if (!armed) return;

            string dir;
            try
            {
                dir = File.ReadAllText(TriggerPath).Trim();
                File.Delete(TriggerPath);   // 다음 리컴파일에서 또 돌지 않도록 먼저 지운다
            }
            catch (System.Exception e)
            {
                Debug.LogError("[DevViewCapture] 트리거 읽기 실패: " + e.Message);
                return;
            }

            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Path.GetTempPath(), "woopang_capture");

            var report = Capture(dir);
            Debug.Log("[DevViewCapture] " + string.Join(" | ", report));
        }

        /// <summary>지정 폴더에 캡처하고 결과 로그를 반환한다.</summary>
        public static List<string> Capture(string dir)
        {
            var log = new List<string>();
            Directory.CreateDirectory(dir);

            // 1) 씬에 어떤 캔버스가 있는지 — UI 캡처 가능 여부를 좌우한다
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases)
            {
                if (c.transform.parent != null) continue;   // 루트 캔버스만
                log.Add($"canvas: {c.name} / mode={c.renderMode} / active={c.gameObject.activeInHierarchy}");
            }

            // 2) 씬 뷰 카메라 렌더 — 3D 배치 확인용. 에디트 모드에서 가장 확실하게 동작한다.
            var sv = SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null)
            {
                string p = Path.Combine(dir, "sceneview.png");
                if (RenderCamera(sv.camera, p, out string err)) log.Add("saved: " + p);
                else log.Add("sceneview 실패: " + err);
            }
            else log.Add("SceneView 없음 — 씬 뷰 창이 열려 있어야 한다");

            // 3) 게임 뷰 카메라 렌더. Screen Space - Camera / World Space 캔버스는 여기 잡힌다.
            //    Screen Space - Overlay 는 카메라 렌더에 포함되지 않는다(Unity 구조상).
            var cam = Camera.main;
            if (cam == null)
            {
                var all = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                if (all.Length > 0) cam = all[0];
            }
            if (cam != null)
            {
                string p = Path.Combine(dir, "gamecam.png");
                if (RenderCamera(cam, p, out string err)) log.Add($"saved: {p}  (camera={cam.name})");
                else log.Add("gamecam 실패: " + err);
            }
            else log.Add("카메라 없음");

            // 4) UI 캡처.
            //    ScreenSpaceOverlay 캔버스는 어떤 카메라 렌더에도 잡히지 않는다(Unity 구조).
            //    임시 카메라를 만들어 ScreenSpaceCamera 로 잠깐 바꿔 찍고 즉시 되돌린다.
            CaptureOverlayUI(dir, log);

            AssetDatabase.Refresh();
            return log;
        }

        /// <summary>
        /// Overlay 캔버스를 임시로 ScreenSpaceCamera 로 전환해 렌더한다.
        /// 원래 값은 finally 에서 반드시 복구한다 — 실패해도 씬이 망가지면 안 된다.
        /// </summary>
        private static void CaptureOverlayUI(string dir, List<string> log)
        {
            var canvases = new List<Canvas>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                if (c.transform.parent == null && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    canvases.Add(c);

            if (canvases.Count == 0) { log.Add("overlay 캔버스 없음"); return; }

            var camGO = new GameObject("~DevCaptureCam");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            cam.orthographic = true;
            cam.cullingMask = ~0;

            var restore = new List<(Canvas c, RenderMode m, Camera w, float d)>();
            try
            {
                foreach (var c in canvases)
                {
                    restore.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 10f;
                }
                Canvas.ForceUpdateCanvases();

                string p = Path.Combine(dir, "ui.png");
                if (RenderCamera(cam, p, out string err)) log.Add("saved: " + p);
                else log.Add("ui 실패: " + err);
            }
            catch (System.Exception e) { log.Add("ui 예외: " + e.Message); }
            finally
            {
                foreach (var (c, m, w, d) in restore)
                {
                    if (c == null) continue;
                    c.renderMode = m; c.worldCamera = w; c.planeDistance = d;
                }
                Canvas.ForceUpdateCanvases();
                Object.DestroyImmediate(camGO);
            }
        }

        private static bool RenderCamera(Camera cam, string path, out string err)
        {
            err = null;
            RenderTexture rt = null;
            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            try
            {
                rt = new RenderTexture(WIDTH, HEIGHT, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return true;
            }
            catch (System.Exception e) { err = e.Message; return false; }
            finally
            {
                // 카메라를 원래대로 되돌리지 않으면 에디터 화면이 검게 남는다
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }
    }
}
