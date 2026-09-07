using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 커맨드라인 원커맨드 빌드 진입점.
    ///
    ///   Unity.exe -batchmode -quit -projectPath C:\woopang ^
    ///     -executeMethod Editor.BuildPipelineRunner.BuildAndroid ^
    ///     -logFile C:\woopang\build.log
    ///
    /// Unity를 GUI로 열 필요가 없고, 빌드 설정이 코드로 고정되어
    /// "어제는 됐는데" 류의 환경 차이가 사라진다.
    ///
    /// 빌드 직전 BuildValidator를 자동 실행하며, 문제가 있으면 빌드하지 않고 종료한다.
    /// 배치 모드에서는 exit code 1로 끝나므로 CI에서도 그대로 쓸 수 있다.
    ///
    /// 옵션 (커맨드라인 인자):
    ///   -woopangOutput &lt;경로&gt;   결과물 경로 지정 (기본: Builds/ 아래 자동 생성)
    ///   -woopangDev              development build (프로파일러 연결용)
    ///   -woopangSkipValidate     검증 건너뛰기 (권장하지 않음)
    /// </summary>
    public static class BuildPipelineRunner
    {
        private const string DefaultOutputDir = "Builds";

        [MenuItem("WOOPANG/빌드 ▸ Android (검증 포함)", priority = 20)]
        public static void BuildAndroidMenu() => RunAndroid(interactive: true);

        [MenuItem("WOOPANG/빌드 ▸ iOS Xcode 프로젝트 (검증 포함)", priority = 21)]
        public static void BuildIOSMenu() => RunIOS(interactive: true);

        /// <summary>배치모드 진입점 — Android APK</summary>
        public static void BuildAndroid() => RunAndroid(interactive: false);

        /// <summary>배치모드 진입점 — iOS Xcode 프로젝트 익스포트</summary>
        public static void BuildIOS() => RunIOS(interactive: false);

        // ── 실행 ───────────────────────────────────────────────────
        private static void RunAndroid(bool interactive)
        {
            string output = ResolveOutput(interactive, BuildTarget.Android);
            if (string.IsNullOrEmpty(output)) return;
            Execute(BuildTarget.Android, BuildTargetGroup.Android, output, interactive);
        }

        private static void RunIOS(bool interactive)
        {
            string output = ResolveOutput(interactive, BuildTarget.iOS);
            if (string.IsNullOrEmpty(output)) return;
            Execute(BuildTarget.iOS, BuildTargetGroup.iOS, output, interactive);
        }

        private static void Execute(BuildTarget target, BuildTargetGroup group,
                                    string output, bool interactive)
        {
            // 1) 검증 — 실패하면 빌드 자체를 시작하지 않는다
            if (!HasArg("-woopangSkipValidate"))
            {
                var issues = BuildValidator.Validate();
                if (issues.Count > 0)
                {
                    string msg = "빌드 전 검증에서 문제가 발견되어 중단합니다.\n\n"
                               + string.Join("\n\n", issues);
                    Debug.LogError("[BuildPipelineRunner] " + msg);
                    if (interactive)
                        EditorUtility.DisplayDialog("빌드 중단", msg, "확인");
                    else
                        EditorApplication.Exit(1);
                    return;
                }
                Debug.Log("[BuildPipelineRunner] 빌드 검증 통과");
            }

            // 2) 플랫폼 전환 (배치모드에서 타깃이 다를 수 있음)
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.Log($"[BuildPipelineRunner] 플랫폼 전환: {EditorUserBuildSettings.activeBuildTarget} → {target}");
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            }

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                targetGroup = group,
                options = HasArg("-woopangDev")
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None,
            };

            Debug.Log($"[BuildPipelineRunner] 빌드 시작 — {target}\n" +
                      $"  출력: {output}\n" +
                      $"  씬 {scenes.Length}개: {string.Join(", ", scenes)}\n" +
                      $"  버전: {PlayerSettings.bundleVersion} " +
                      $"(Android code {PlayerSettings.Android.bundleVersionCode})");

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                double mb = summary.totalSize / 1024.0 / 1024.0;
                Debug.Log($"[BuildPipelineRunner] 빌드 성공 — {mb:F1} MB / " +
                          $"{summary.totalTime.TotalMinutes:F1}분\n  {summary.outputPath}");
                if (!interactive) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[BuildPipelineRunner] 빌드 실패 — {summary.result} " +
                               $"(오류 {summary.totalErrors}건)");
                if (!interactive) EditorApplication.Exit(1);
            }
        }

        // ── 출력 경로 ──────────────────────────────────────────────
        private static string ResolveOutput(bool interactive, BuildTarget target)
        {
            string fromArg = GetArgValue("-woopangOutput");
            if (!string.IsNullOrEmpty(fromArg)) return fromArg;

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            string ver = PlayerSettings.bundleVersion;

            if (target == BuildTarget.Android)
            {
                Directory.CreateDirectory(DefaultOutputDir);
                return Path.Combine(DefaultOutputDir, $"woopang_{ver}_{stamp}.apk");
            }

            // iOS는 폴더(Xcode 프로젝트)로 나온다
            string dir = Path.Combine(DefaultOutputDir, $"iOS_{ver}_{stamp}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ── 커맨드라인 인자 ────────────────────────────────────────
        private static bool HasArg(string name) =>
            Environment.GetCommandLineArgs().Any(a => a == name);

        private static string GetArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
