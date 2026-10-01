using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// woopang_0926 씬 작업 도구 (에디터 전용 — 빌드에 안 들어간다. 2026-09-29 부터 0926 이 빌드 씬이라 함께 커밋).
    ///
    /// 트리거 파일(.redesign0926_trigger) 한 줄에 명령 하나:
    ///   apply                       → 시안 레이아웃 적용 (없으면 0529 를 복사해 씬부터 만든다)
    ///   capture:&lt;dir&gt;:main,list,...  → 상태별 UI 캡처 (끝나면 원상복구)
    /// 결과 로그는 .redesign0926_result 에 남는다.
    ///
    /// 사용 중인 0529 씬은 건드리지 않는다: 0926 을 Additive 로 열어 작업하고 저장 후 닫는다.
    /// </summary>
    [InitializeOnLoad]
    public static class Redesign0926Tool
    {
        public const string SrcScene = "Assets/Scenes/woopang_0529.unity";
        public const string DstScene = "Assets/Scenes/woopang_0926.unity";

        private static string Root => Directory.GetCurrentDirectory();
        private static string TriggerPath => Path.Combine(Root, ".redesign0926_trigger");
        private static string ResultPath => Path.Combine(Root, ".redesign0926_result");
        private static bool waitLogged;

        static Redesign0926Tool()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.update += PollTrigger;   // 스크립트가 다시 컴파일되지 않아도 트리거를 받는다
        }

        private static double nextPoll;

        private static void PollTrigger()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 3.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(TriggerPath)) RunTrigger();
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            // delayCall 은 에디터가 백그라운드일 때 돌지 않는다 → 리로드 직후 바로 실행 (DevViewCapture 와 같은 방식)
            if (File.Exists(TriggerPath)) RunTrigger();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            // 대표님이 0926 을 직접 열었을 때도 최신 시안이 적용돼 있도록
            if (scene.path != DstScene) return;
            EditorApplication.delayCall += () =>
            {
                if (!scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode) return;
                var log = new List<string>();
                if (Redesign0926Apply.ApplyIfOutdated(scene, log))
                    EditorSceneManager.MarkSceneDirty(scene);
            };
        }

        // ── 안드로이드 출시 빌드 (.aab) ─────────────────────────────
        // 서명 비밀번호는 코드·문서에 두지 않는다 — 에디터에 입력돼 있지 않으면 server/.env 의
        // ANDROID_KEYSTORE_PASS / ANDROID_KEYALIAS_PASS 에서만 읽고, 없으면 빌드하지 않는다 (CLAUDE.md 6.1).
        private static void BuildAndroidRelease(List<string> log)
        {
            var issues = global::Editor.BuildValidator.Validate();
            if (issues.Count > 0)
            {
                log.Add("빌드 중단: 검증 실패\n" + string.Join("\n", issues));
                return;
            }
            log.Add("빌드 검증 통과 (다음 버전 " + global::Editor.BuildNumberAutoIncrement.PredictBundleVersion() + ")");

            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass))
                PlayerSettings.Android.keystorePass = ReadEnv("ANDROID_KEYSTORE_PASS");
            if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
                PlayerSettings.Android.keyaliasPass = ReadEnv("ANDROID_KEYALIAS_PASS") ?? PlayerSettings.Android.keystorePass;
            if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(PlayerSettings.Android.keystorePass))
            {
                log.Add("빌드 중단: 서명 비밀번호 없음 — server/.env 에 ANDROID_KEYSTORE_PASS(와 ANDROID_KEYALIAS_PASS)를 넣어야 한다");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = true;   // Play 스토어는 .aab

            string output = global::Editor.BuildPipelineRunner.AndroidOutputPath("aab");   // D:\##WP_backup\apk_2026\<MMDD>\<HHmm>.aab
            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var sum = report.summary;
            log.Add(sum.result == UnityEditor.Build.Reporting.BuildResult.Succeeded
                ? $"빌드 성공: {output} ({sum.totalSize / 1048576.0:F1} MB, {sum.totalTime.TotalMinutes:F1}분) · 버전 {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}) · 씬 {string.Join(",", scenes)}"
                : $"빌드 실패: {sum.result} · 오류 {sum.totalErrors}건");
        }

        private static string ReadEnv(string key)
        {
            string path = Path.Combine(Root, "server", ".env");
            if (!File.Exists(path)) return null;
            foreach (var line in File.ReadAllLines(path))
            {
                var t = line.Trim();
                if (t.StartsWith(key + "=")) return t.Substring(key.Length + 1).Trim().Trim('"');
            }
            return null;
        }

        private static void DumpRect(Transform t, int depth, int maxDepth, List<string> log)
        {
            var rt = t as RectTransform;
            string r = rt == null ? "" : $" aMin{rt.anchorMin} aMax{rt.anchorMax} piv{rt.pivot} pos{rt.anchoredPosition} size{rt.sizeDelta} scale{rt.localScale}";
            log.Add(new string(' ', depth * 2) + (t.gameObject.activeSelf ? "" : "(off) ") + t.name + r);
            if (depth >= maxDepth) return;
            foreach (Transform c in t) DumpRect(c, depth + 1, maxDepth, log);
        }

        private static void RunTrigger()
        {
            if (!File.Exists(TriggerPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                if (!waitLogged)
                {
                    waitLogged = true;
                    Debug.Log($"[Redesign0926] 대기 — playing={EditorApplication.isPlayingOrWillChangePlaymode} compiling={EditorApplication.isCompiling}");
                }
                EditorApplication.delayCall += RunTrigger;
                return;
            }
            waitLogged = false;

            string[] cmds;
            try
            {
                cmds = File.ReadAllLines(TriggerPath);
                File.Delete(TriggerPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Redesign0926] 트리거 읽기 실패: " + e.Message);
                return;
            }

            var log = new List<string> { "run " + DateTime.Now.ToString("HH:mm:ss") };
            Scene scene = default;
            bool openedHere = false;
            try
            {
                if (!File.Exists(DstScene))
                {
                    if (!AssetDatabase.CopyAsset(SrcScene, DstScene))
                        throw new Exception("씬 복사 실패");
                    AssetDatabase.Refresh();
                    log.Add("copied " + SrcScene + " -> " + DstScene);
                }

                scene = SceneManager.GetSceneByPath(DstScene);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(DstScene, OpenSceneMode.Additive);
                    openedHere = true;
                }

                foreach (var raw in cmds)
                {
                    string cmd = raw.Trim();
                    if (cmd.Length == 0) continue;
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        // 플레이 모드로 들고 나는 중에는 씬을 저장할 수 없다 — 남은 명령은 다시 트리거 파일로 돌려놓는다
                        File.WriteAllLines(TriggerPath, cmds);
                        log.Add("플레이 모드 전환 중 — 나중에 다시 실행");
                        EditorApplication.delayCall += RunTrigger;
                        break;
                    }
                    if (cmd == "apply")
                    {
                        Redesign0926Apply.Apply(scene, log);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        log.Add("saved");
                    }
                    else if (cmd == "buildscene")
                    {
                        // 빌드에 들어가는 씬을 0926 하나로 (2026-09-29 대표님 지시)
                        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DstScene, true) };
                        AssetDatabase.SaveAssets();
                        log.Add("build scene → " + DstScene);
                    }
                    else if (cmd == "openscene")
                    {
                        // 끝나면 에디터에 0926 을 열어 둔다 (아래 finally 에서)
                    }
                    else if (cmd == "buildandroid")
                    {
                        BuildAndroidRelease(log);
                    }
                    else if (cmd.StartsWith("dump:"))
                    {
                        // dump:<이름> — 자식 배치값을 결과 파일에 적는다 (가로 배치 설계용)
                        var target = Redesign0926Capture.FindInCanvas(Redesign0926Capture.FindMainCanvas(scene).transform, cmd.Substring(5));
                        if (target == null) log.Add("dump: 없음 " + cmd.Substring(5));
                        else DumpRect(target.transform, 0, 3, log);
                    }
                    else if (cmd.StartsWith("capture:"))
                    {
                        // capture:<dir>:<state,state>  — 드라이브 문자(C:)의 콜론을 피하려고 마지막 콜론으로 자른다
                        string body = cmd.Substring("capture:".Length);
                        int cut = body.LastIndexOf(':');
                        string dir = body.Substring(0, cut);
                        string[] states = body.Substring(cut + 1).Split(',');
                        Redesign0926Capture.CaptureStates(scene, dir, states, log);
                    }
                    else log.Add("unknown: " + cmd);
                }
            }
            catch (Exception e)
            {
                log.Add("ERROR " + e);
            }
            finally
            {
                if (openedHere && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (Array.IndexOf(cmds, "openscene") >= 0 && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        // 컴파일 직후 콜백 안에서 씬을 열면 이후 컴파일이 멈추는 일이 있었다 → 한 박자 뒤에
                        EditorApplication.delayCall += () => EditorSceneManager.OpenScene(DstScene, OpenSceneMode.Single);
                        log.Add("editor scene → " + DstScene + " (곧 열림)");
                    }
                }
                else if (scene.IsValid() && scene.isLoaded && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // 에디터에 0926 이 열려 있던 경우: 캡처가 켜고 끈 흔적(*)까지 저장한 뒤 디스크에서 다시 연다.
                    // 안 그러면 에디터에 "modified externally — Reload?" 창이 떠서 에디터 전체가 멈춘다.
                    try
                    {
                        EditorSceneManager.SaveScene(scene);
                        if (SceneManager.sceneCount == 1)
                            EditorApplication.delayCall += () => EditorSceneManager.OpenScene(DstScene, OpenSceneMode.Single);   // 콜백 밖에서
                        log.Add("editor scene synced");
                    }
                    catch (Exception e) { log.Add("sync 실패 " + e.Message); }
                }
                File.WriteAllLines(ResultPath, log);
            }
        }
    }
}
