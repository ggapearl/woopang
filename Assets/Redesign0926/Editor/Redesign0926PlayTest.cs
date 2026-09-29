using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 플레이 모드 동작 시험 (에디터 전용). .redesign0926_playtest 파일(1줄: 캡처 폴더)이 생기면 플레이를 켜고
    /// 가상 마우스·뒤로가기(Esc)로 창을 열고 닫아 본 뒤 결과를 .redesign0926_playtest_result 에 적고 플레이를 끈다.
    ///  · 목록: 크게 끌어내리면 닫힘 / 조금 끌면 제자리 / 뒤로가기로 닫힘
    ///  · 프로필(또는 로그인 안내): 뒤로가기로 닫힘
    ///  · 메시지(또는 로그인 안내): 크게 끌어내리면 닫힘
    /// </summary>
    [InitializeOnLoad]
    public static class Redesign0926PlayTest
    {
        private static string Root => Directory.GetCurrentDirectory();
        private static string PlanPath => Path.Combine(Root, ".redesign0926_playtest");
        private static string ResultPath => Path.Combine(Root, ".redesign0926_playtest_result");

        private static readonly List<string> log = new List<string>();
        private static readonly Queue<Func<bool>> steps = new Queue<Func<bool>>();   // true 를 돌려주면 다음 단계로
        private static double nextPoll, waitUntil = -1;
        private static bool running;
        private static string dir;
        private static InputSettings.EditorInputBehaviorInPlayMode savedBehavior;
        private static InputSettings.BackgroundBehavior savedBackground;

        static Redesign0926PlayTest()
        {
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(PlanPath)) Start(); };
            EditorApplication.update += Tick;
        }

        private static void Start()
        {
            dir = File.ReadAllLines(PlanPath)[0].Trim();
            Directory.CreateDirectory(dir);
            log.Clear();
            log.Add("playtest " + DateTime.Now.ToString("HH:mm:ss"));
            savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            steps.Clear();
            Plan();
            running = true;
        }

        // ── 시험 순서 ────────────────────────────────────────────
        private static void Plan()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.5f);

            // 목록 — 크게 끌어내리기
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Shot("list_open");
            Drag("ListPanel/Sheet0926", 0.45f, "list_drag");
            Sleep(1.0f);
            Check(() => !Active("ListPanel"), "목록: 크게 끌어내리면 닫힘");
            Shot("list_closed");

            // 목록 — 조금만 끌면 제자리
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => Remember("ListPanel/Sheet0926"));
            Drag("ListPanel/Sheet0926", 0.06f, null);
            Sleep(1.0f);
            Check(() => Active("ListPanel") && SamePlace("ListPanel/Sheet0926"), "목록: 조금 끌면 제자리로 돌아옴");

            // 목록 — 뒤로가기
            Back();
            Sleep(0.6f);
            Check(() => !Active("ListPanel"), "목록: 뒤로가기로 닫힘");

            // 목록 — 도크 X: 바로 꺼지지 않고 내려간 뒤 닫힘
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(0.05f);
            Shot("list_x_closing");
            Check(() => Active("ListPanel"), "목록: X 누른 직후엔 아직 보임(내려가는 중)");
            Sleep(0.8f);
            Check(() => !Active("ListPanel"), "목록: X 누르면 내려간 뒤 닫힘");

            // 프로필 (로그인 안 돼 있으면 로그인 안내)
            Do(() => Click("Dock0926/MiniProfile"));
            Sleep(1.0f);
            Shot("profile_open");
            Do(() => log.Add("  열린 창: " + OpenPanels()));
            Back();
            Sleep(0.6f);
            Check(() => OpenPanels() == "-", "프로필/로그인 안내: 뒤로가기로 닫힘");

            // 메시지 (로그인 안 돼 있으면 로그인 안내)
            Do(() => Click("Dock0926/Message_Button"));
            Sleep(1.2f);
            Do(() => log.Add("  열린 창: " + OpenPanels()));
            Drag("MessagePanel/Background", 0.45f, "msg_drag", () => Active("MessagePanel"));
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Sleep(1.2f);
            Check(() => OpenPanels() == "-", "메시지(또는 로그인 안내): 끌어내리기/뒤로가기로 닫힘");

            // 아무 창도 없을 때 뒤로가기 — 에디터에선 앱을 뒤로 보내지 못하니 창이 새로 열리지 않는지만
            Back();
            Sleep(0.6f);
            Check(() => OpenPanels() == "-", "메인 뒤로가기: 창이 열리지 않음 (기기에선 moveTaskToBack)");
            Shot("main_after_back");
        }

        // ── 단계 도우미 ──────────────────────────────────────────
        private static void Do(Action a) => steps.Enqueue(() => { a(); return true; });
        private static void Sleep(float s) => steps.Enqueue(() =>
        {
            if (waitUntil < 0) waitUntil = EditorApplication.timeSinceStartup + s;
            if (EditorApplication.timeSinceStartup < waitUntil) return false;
            waitUntil = -1;
            return true;
        });
        private static void Wait(Func<bool> cond, float max, string what)
        {
            double until = -1;
            steps.Enqueue(() =>
            {
                if (until < 0) until = EditorApplication.timeSinceStartup + max;
                if (cond()) { log.Add("  " + what); return true; }
                if (EditorApplication.timeSinceStartup > until) { log.Add("  시간 초과: " + what); return true; }
                return false;
            });
        }
        private static void Check(Func<bool> ok, string what) => Do(() => log.Add((ok() ? "PASS " : "FAIL ") + what));
        private static void Shot(string name) => Do(() => ScreenCapture.CaptureScreenshot(Path.Combine(dir, "test_" + name + ".png")));

        private static void Back()
        {
            Do(BackNow);
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
        }
        private static void BackNow() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape));

        // 시트 윗부분(제목 줄)을 잡고 시트 높이 × fraction 만큼 0.3초에 걸쳐 끌어내린다
        private static void Drag(string sheetPath, float fraction, string midShot, Func<bool> when = null)
        {
            Vector2 from = default, to = default;
            int i = 0;
            bool skip = false;
            R0926SwipeDismiss sd = null;
            int frame = -1;
            const int n = 12;
            steps.Enqueue(() =>
            {
                skip = when != null && !when();
                if (skip) return true;
                var rt = Find(sheetPath) as RectTransform;
                if (rt == null) { log.Add("  시트 없음 " + sheetPath); skip = true; return true; }
                var c = new Vector3[4];
                rt.GetWorldCorners(c);   // 오버레이 캔버스 — 월드 좌표가 곧 화면 좌표
                float h = c[1].y - c[0].y;
                from = new Vector2((c[1].x + c[2].x) * 0.5f, c[1].y - Mathf.Min(70f, h * 0.05f));
                to = from - new Vector2(0f, h * fraction);
                sd = rt.GetComponent<R0926SwipeDismiss>();
                if (sd == null) { log.Add("  끌어 닫기 없음 " + sheetPath); skip = true; return true; }
                sd.EditorFeed(true, from);
                i = 0;
                frame = Time.frameCount;
                return true;
            });
            steps.Enqueue(() =>
            {
                if (skip) return true;
                if (Time.frameCount == frame) return false;   // 한 프레임에 한 걸음 — 에디터 갱신이 게임 프레임보다 잦다
                frame = Time.frameCount;
                i++;
                sd.EditorFeed(true, Vector2.Lerp(from, to, (float)i / n));
                if (i == n / 2 && midShot != null) ScreenCapture.CaptureScreenshot(Path.Combine(dir, "test_" + midShot + ".png"));
                if (i == 3) Diagnose(sheetPath, from);
                return i >= n;
            });
            steps.Enqueue(() => skip || Time.frameCount > frame);   // 마지막 걸음이 읽힌 뒤에 놓는다
            Do(() => { if (!skip) sd.EditorFeed(false, to); });
            Sleep(0.1f);
            Do(() => { if (!skip && sd != null) sd.EditorFeedEnd(); });
        }
        private static void Diagnose(string sheetPath, Vector2 at)
        {
            var p = Pointer.current;
            log.Add($"  진단: pointer={(p == null ? "null" : p.GetType().Name)} pressed={(p != null && p.press.isPressed)} pos={(p != null ? p.position.ReadValue().ToString() : "-")} from={at} screen={Screen.width}x{Screen.height}");
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null)
            {
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = at }, hits);
                log.Add("  진단: 맨 위 " + (hits.Count > 0 ? hits[0].gameObject.name + " (" + hits[0].gameObject.transform.parent?.name + ")" : "없음") + " · 총 " + hits.Count);
                if (hits.Count > 0)
                {
                    var h = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IDragHandler>(hits[0].gameObject);
                    string comps = h == null ? "-" : string.Join(",", System.Array.ConvertAll(h.GetComponents<Component>(), c => c.GetType().Name));
                    log.Add("  진단: 끌기 받는 곳 " + (h == null ? "없음" : h.name + " [" + comps + "]"));
                }
            }
            var sd = Find(sheetPath)?.GetComponent<R0926SwipeDismiss>();
            var ph = typeof(R0926SwipeDismiss).GetField("phase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            log.Add("  진단: swipe=" + (sd == null ? "없음" : (sd.isActiveAndEnabled + " phase=" + ph?.GetValue(sd))));
        }


        // ── 씬 조회 ──────────────────────────────────────────────
        private static Transform CanvasRoot()
        {
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == "Canvas") return r.transform;
            return null;
        }
        private static Transform Find(string path) { var c = CanvasRoot(); return c != null ? c.Find(path) : null; }
        private static bool Active(string path) { var t = Find(path); return t != null && t.gameObject.activeInHierarchy; }
        private static void Click(string path)
        {
            var b = Find(path)?.GetComponent<Button>();
            if (b == null) { log.Add("  버튼 없음 " + path); return; }
            b.onClick.Invoke();
        }
        private static string OpenPanels()
        {
            var open = new List<string>();
            foreach (var p in new[] { "ListPanel", "MessagePanel", "ChatRoomPanel", "FullProfilePanel", "LoginPromptPanel", "FollowPanel", "AskAISheet0926" })
                if (Active(p)) open.Add(p);
            return open.Count == 0 ? "-" : string.Join(",", open);
        }
        private static Vector2 remembered;
        private static void Remember(string path) { var rt = Find(path) as RectTransform; if (rt != null) remembered = rt.anchoredPosition; }
        private static bool SamePlace(string path) { var rt = Find(path) as RectTransform; return rt != null && Vector2.Distance(rt.anchoredPosition, remembered) < 1f; }

        // ── 진행 ────────────────────────────────────────────────
        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                running = false;
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + 3.0;
                if (File.Exists(PlanPath) && !File.Exists(ResultPath + ".lock") && !EditorApplication.isCompiling
                    && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;
                return;
            }
            if (!running) return;
            try
            {
                while (steps.Count > 0 && steps.Peek()()) steps.Dequeue();
            }
            catch (Exception e)
            {
                log.Add("ERROR " + e.Message);
                steps.Clear();
            }
            if (steps.Count > 0) return;
            running = false;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedBehavior;
            InputSystem.settings.backgroundBehavior = savedBackground;
            try { File.Delete(PlanPath); } catch (Exception e) { log.Add("plan 삭제 실패 " + e.Message); }
            File.WriteAllLines(ResultPath, log);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;   // 마지막 캡처가 저장될 틈을 둔다
        }
    }
}
