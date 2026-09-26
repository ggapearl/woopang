using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 빌드 전 자동 검증. 여기 있는 항목은 전부 실제로 빌드를 망가뜨렸던 사례다.
    ///
    /// - 셰이더 누락 → GLB가 자홍색으로 렌더링 (2026-09, 시바견 모델)
    /// - 잘못된 빌드 씬 → DanceAnimController가 통째로 빠져 더블탭 무반응 (2026-06)
    /// - Resources 폴백 머터리얼 누락 → Shader.Find 실패 시 복구 불가
    ///
    /// 메뉴: WOOPANG ▸ 빌드 검증 (수동), 또는 BuildPipelineRunner가 빌드 직전 자동 호출.
    /// </summary>
    public static class BuildValidator
    {
        /// <summary>빌드에 반드시 포함돼야 하는 셰이더. 코드에서만 참조하는 셰이더는 stripped된다.</summary>
        private static readonly string[] RequiredShaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
        };

        /// <summary>런타임에 Resources.Load로 찾는 에셋. 없으면 조용히 실패한다.</summary>
        private static readonly string[] RequiredResources =
        {
            "GLBFallbackUnlit",
        };

        /// <summary>
        /// Google Play가 요구하는 최소 targetSdk.
        /// 2026-08-31부터 Android 16(API 36) 미만은 업데이트 업로드가 거부된다.
        /// (규칙: 최신 Android 출시로부터 1년 이내. 다음 상향 시 이 값만 올리면 된다)
        /// </summary>
        private const int RequiredAndroidTargetSdk = 36;

        private const string ExpectedMainScene = "Assets/Scenes/woopang_0529.unity";

        /// <summary>
        /// targetSdk를 요구치로 올린다. Play 요구사항은 매년 상향되므로 수동 조작 대신 메뉴로 둔다.
        /// PlayerSettings 경유로 설정해야 에디터가 열려 있어도 값이 안전하게 반영된다.
        /// </summary>
        [MenuItem("WOOPANG/Android targetSdk 요구치로 설정", priority = 11)]
        public static void FixAndroidTargetSdk()
        {
            int before = (int)PlayerSettings.Android.targetSdkVersion;
            if (before >= RequiredAndroidTargetSdk && before != 0)
            {
                EditorUtility.DisplayDialog("targetSdk",
                    $"이미 {before} 입니다. 변경하지 않았습니다.", "확인");
                return;
            }

            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)RequiredAndroidTargetSdk;
            AssetDatabase.SaveAssets();

            string msg = $"targetSdk {(before == 0 ? "Automatic" : before.ToString())} → {RequiredAndroidTargetSdk}";
            Debug.Log("[BuildValidator] " + msg);
            EditorUtility.DisplayDialog("targetSdk 변경", msg, "확인");
        }

        [MenuItem("WOOPANG/빌드 검증", priority = 10)]
        public static void ValidateMenu()
        {
            var issues = Validate();
            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("빌드 검증", "이상 없음. 빌드 가능합니다.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("빌드 검증 실패",
                string.Join("\n\n", issues), "확인");
        }

        /// <summary>
        /// 배치모드 진입점 — 빌드 없이 검증만. 문제가 있으면 exit code 1.
        ///   Unity.exe -batchmode -quit -executeMethod Editor.BuildValidator.ValidateBatch
        /// </summary>
        public static void ValidateBatch()
        {
            var issues = Validate();
            if (issues.Count == 0)
            {
                Debug.Log("[BuildValidator] 통과 — 검사 항목 전부 이상 없음");
                EditorApplication.Exit(0);
                return;
            }
            foreach (var i in issues) Debug.LogError("[BuildValidator] " + i);
            Debug.LogError($"[BuildValidator] 실패 — 문제 {issues.Count}건");
            EditorApplication.Exit(1);
        }

        /// <summary>문제 목록을 반환한다. 비어 있으면 통과.</summary>
        public static List<string> Validate()
        {
            var issues = new List<string>();
            CheckShaders(issues);
            CheckResources(issues);
            CheckBuildScenes(issues);
            CheckFirebaseConfig(issues);
            CheckAndroidTargetSdk(issues);
            CheckPreloadedAssets(issues);
            CheckNoCertificateBypass(issues);
            CheckReleaseNotes(issues);
            return issues;
        }

        // ── 셰이더가 빌드에 포함되는지 ──────────────────────────────
        private static void CheckShaders(List<string> issues)
        {
            // GraphicsSettings는 에셋으로 직접 로드되지 않아 SerializedObject로 접근한다
            var obj = Unsupported.GetSerializedAssetInterfaceSingleton("GraphicsSettings");
            var so = new SerializedObject(obj);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");

            var included = new HashSet<string>();
            for (int i = 0; i < arr.arraySize; i++)
            {
                var s = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (s != null) included.Add(s.name);
            }

            foreach (var name in RequiredShaders)
            {
                if (!included.Contains(name))
                {
                    issues.Add(
                        $"[셰이더 누락] '{name}' 이(가) Always Included Shaders 에 없습니다.\n" +
                        "  → 코드에서만 참조하는 셰이더는 빌드에서 제거되어 Shader.Find가 null을 반환하고,\n" +
                        "     GLB 모델이 자홍색(magenta)으로 렌더링됩니다.\n" +
                        "  → Project Settings ▸ Graphics ▸ Always Included Shaders 에 추가하세요.");
                }
            }
        }

        // ── Resources 폴백 에셋 ────────────────────────────────────
        private static void CheckResources(List<string> issues)
        {
            foreach (var res in RequiredResources)
            {
                var loaded = Resources.Load(res);
                if (loaded == null)
                {
                    issues.Add(
                        $"[리소스 누락] Resources/{res} 를 찾을 수 없습니다.\n" +
                        "  → GLBModelLoader가 셰이더를 확보하는 1순위 경로입니다.\n" +
                        "     이 에셋이 있어야 해당 셰이더가 빌드에 확실히 포함됩니다.");
                }
            }
        }

        // ── 빌드 씬 구성 ───────────────────────────────────────────
        private static void CheckBuildScenes(List<string> issues)
        {
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).ToList();

            if (enabled.Count == 0)
            {
                issues.Add("[빌드 씬 없음] 활성화된 씬이 하나도 없습니다.");
                return;
            }

            if (enabled[0].path != ExpectedMainScene)
            {
                issues.Add(
                    $"[빌드 씬 불일치] 첫 씬이 '{enabled[0].path}' 입니다.\n" +
                    $"  → 기대값: {ExpectedMainScene}\n" +
                    "  → 다른 씬이 들어가면 DanceAnimController 등 매니저가 통째로 빠져\n" +
                    "     기능이 조용히 동작하지 않습니다.");
            }

            foreach (var s in EditorBuildSettings.scenes)
            {
                if (!File.Exists(s.path))
                {
                    issues.Add($"[씬 파일 없음] 빌드 목록에 있으나 파일이 존재하지 않습니다: {s.path}");
                }
            }
        }

        // ── Firebase 설정 파일 ─────────────────────────────────────
        private static void CheckFirebaseConfig(List<string> issues)
        {
            if (!File.Exists("Assets/google-services.json"))
                issues.Add("[Firebase] Assets/google-services.json 없음 — Android 푸시가 동작하지 않습니다.");

            if (!File.Exists("Assets/GoogleService-Info.plist"))
                issues.Add("[Firebase] Assets/GoogleService-Info.plist 없음 — iOS 푸시가 동작하지 않습니다.");
        }

        // -- Google Play target API 요구사항 --------------------
        private static void CheckAndroidTargetSdk(List<string> issues)
        {
            int target = (int)PlayerSettings.Android.targetSdkVersion;

            // 0 = Automatic(설치된 최신). 무엇이 들어갈지 빌드 전에 알 수 없어 명시값을 요구한다.
            if (target == 0)
            {
                issues.Add(
                    "[targetSdk 자동] Player Settings의 Target API Level이 'Automatic'입니다.\n" +
                    "  → 설치된 SDK에 따라 값이 달라져 Play 심사 결과를 예측할 수 없습니다.\n" +
                    $"  → API {RequiredAndroidTargetSdk} 이상으로 명시하세요.");
                return;
            }

            if (target < RequiredAndroidTargetSdk)
            {
                issues.Add(
                    $"[targetSdk 미달] 현재 {target} — Google Play 최소 요구치는 {RequiredAndroidTargetSdk}입니다.\n" +
                    "  → 2026-08-31부터 이보다 낮으면 업데이트 업로드가 거부됩니다.\n" +
                    "     (기존 설치본은 유지되지만 새 버전을 올릴 수 없습니다)\n" +
                    "  → Player Settings ▸ Android ▸ Target API Level 을 올리세요.");
            }
        }

        // ── Preloaded Assets (AR 초기화) ──────────────────────────
        // 에디터가 ProjectSettings 를 다시 저장하면서 4개 중 3개가 빠진 적이 있다(2026-09).
        // 빠지면 AR 카메라 배경이 검게 나오거나 XR 이 아예 초기화되지 않는다.
        private static readonly (string guid, string what)[] RequiredPreloaded =
        {
            ("a71b000f0c8914c2a8307235f6bf3624", "Assets/XR/XRGeneralSettingsPerBuildTarget.asset"),
            ("f1d66e5450418a245a006782a2a5e5f3", "Assets/ExtensionsAssets/Runtime/RuntimeConfig.asset"),
            ("c9f956787b1d945e7b36e0516201fc76", "ARCore 배경 셰이더 (ARCoreBackground / AfterOpaques)"),
            ("0945859e5a1034c2cb6dce53cb4fb899", "ARCore 배경 셰이더 (ARCoreBackground / AfterOpaques)"),
        };

        private static void CheckPreloadedAssets(List<string> issues)
        {
            var present = new HashSet<string>();
            foreach (var obj in PlayerSettings.GetPreloadedAssets())
            {
                if (obj != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long _))
                    present.Add(guid);
            }

            var missing = RequiredPreloaded.Where(r => !present.Contains(r.guid)).Select(r => r.what).ToList();
            if (missing.Count > 0)
            {
                issues.Add(
                    "[Preloaded Assets 누락] Player Settings ▸ Other ▸ Preloaded Assets 에서 빠졌습니다:\n  - " +
                    string.Join("\n  - ", missing) + "\n" +
                    "  → 빠지면 AR 카메라 배경이 검게 나오거나 XR 이 초기화되지 않습니다.\n" +
                    "  → git 의 ProjectSettings/ProjectSettings.asset 에서 preloadedAssets 4줄을 되살리세요.");
            }
        }

        // ── 인증서 검증 우회 금지 ───────────────────────────────────
        // 예전엔 무조건 true 를 돌려주는 핸들러가 요청 29곳에 붙어 있어
        // 중간자가 로그인 토큰·DM 을 가로챌 수 있었다(2026-09-26 삭제).
        // 문자열을 쪼개 둔 건 이 파일 자신이 검사에 걸리지 않게 하려는 것
        private static readonly string[] ForbiddenTlsPatterns =
        {
            "override bool " + "ValidateCertificate",
            "ServerCertificate" + "ValidationCallback",
        };

        private static void CheckNoCertificateBypass(List<string> issues)
        {
            foreach (var path in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
            {
                string src = File.ReadAllText(path);
                foreach (var pat in ForbiddenTlsPatterns)
                {
                    if (!src.Contains(pat)) continue;
                    issues.Add(
                        $"[인증서 검증 우회] {path.Replace('\\', '/')} 에 '{pat}' 가 있습니다.\n" +
                        "  → TLS 검증을 끄면 같은 와이파이의 누구나 로그인 토큰·메시지를 가로챌 수 있습니다.\n" +
                        "  → 서버 인증서는 정상이므로 기본 검증을 그대로 쓰세요.");
                }
            }
        }

        // ── 출시노트 ──────────────────────────────────────────────
        // iOS 맥 세션의 자동 출시가 이 파일에서 What's New 를 읽는다. 항목이 없으면 빈 노트로 올라간다.
        private const string ReleaseNotesPath = "release_notes.json";

        private static void CheckReleaseNotes(List<string> issues)
        {
            string version = BuildNumberAutoIncrement.PredictBundleVersion();
            string hint = $"  → {ReleaseNotesPath} 의 versions 에 \"{version}\": {{ \"ko\": \"…\", \"en\": \"…\" }} 를 추가하세요.";

            if (!File.Exists(ReleaseNotesPath))
            {
                issues.Add($"[출시노트 없음] {ReleaseNotesPath} 파일이 없습니다.\n" + hint);
                return;
            }

            string json = File.ReadAllText(ReleaseNotesPath);
            var entry = Regex.Match(json, "\"" + Regex.Escape(version) + "\"\\s*:\\s*\\{([^}]*)\\}");
            if (!entry.Success || !entry.Groups[1].Value.Contains("\"ko\"") || !entry.Groups[1].Value.Contains("\"en\""))
            {
                issues.Add($"[출시노트 누락] 이번 빌드 버전 {version} 의 ko/en 출시노트가 없습니다.\n" + hint);
            }
        }
    }
}
