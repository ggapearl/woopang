using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
namespace Woopang.Editor.MCP
{
    /// <summary>
    /// WOOPANG Play Mode Controller
    /// MCP의 execute_menu_item 도구와 함께 사용하여 Play 모드를 제어합니다.
    ///
    /// 사용법 (MCP execute_menu_item):
    /// - menuPath: "WOOPANG/MCP/Start Play Mode" → Play 시작
    /// - menuPath: "WOOPANG/MCP/Stop Play Mode" → Play 종료
    /// - menuPath: "WOOPANG/MCP/Toggle Play Mode" → Play 토글
    /// - menuPath: "WOOPANG/MCP/Pause Play Mode" → 일시정지 토글
    /// - menuPath: "WOOPANG/MCP/Build And Run" → 빌드 후 실행
    ///
    /// 이 스크립트는 MCP 패키지를 수정하지 않고도
    /// 기존 execute_menu_item 도구를 통해 Play 모드를 제어할 수 있게 합니다.
    /// </summary>
    public static class PlayModeController
    {
        // ========================================
        // Play Mode 제어
        // ========================================

        [MenuItem("WOOPANG/MCP/Start Play Mode")]
        public static void StartPlayMode()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning("[PlayModeController] 컴파일 중에는 Play 모드를 시작할 수 없습니다.");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
                Debug.Log("[PlayModeController] Play 모드 시작됨");
            }
            else
            {
                Debug.Log("[PlayModeController] 이미 Play 모드입니다");
            }
        }

        [MenuItem("WOOPANG/MCP/Stop Play Mode")]
        public static void StopPlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                Debug.Log("[PlayModeController] Play 모드 종료됨");
            }
            else
            {
                Debug.Log("[PlayModeController] Play 모드가 아닙니다");
            }
        }

        [MenuItem("WOOPANG/MCP/Toggle Play Mode")]
        public static void TogglePlayMode()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning("[PlayModeController] 컴파일 중에는 Play 모드를 전환할 수 없습니다.");
                return;
            }

            EditorApplication.isPlaying = !EditorApplication.isPlaying;
            Debug.Log($"[PlayModeController] Play 모드: {(EditorApplication.isPlaying ? "시작" : "종료")}");
        }

        [MenuItem("WOOPANG/MCP/Pause Play Mode")]
        public static void TogglePausePlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[PlayModeController] Play 모드가 아닐 때는 일시정지할 수 없습니다.");
                return;
            }

            EditorApplication.isPaused = !EditorApplication.isPaused;
            Debug.Log($"[PlayModeController] {(EditorApplication.isPaused ? "일시정지됨" : "재개됨")}");
        }

        [MenuItem("WOOPANG/MCP/Step Frame")]
        public static void StepFrame()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[PlayModeController] Play 모드에서만 프레임 스텝이 가능합니다.");
                return;
            }

            if (!EditorApplication.isPaused)
            {
                EditorApplication.isPaused = true;
            }

            EditorApplication.Step();
            Debug.Log("[PlayModeController] 1 프레임 진행됨");
        }

        // ========================================
        // 빌드 관련
        // ========================================

        [MenuItem("WOOPANG/MCP/Save Scene")]
        public static void SaveCurrentScene()
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[PlayModeController] 씬 저장됨");
            }
        }

        [MenuItem("WOOPANG/MCP/Build And Run")]
        public static void BuildAndRun()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning("[PlayModeController] 컴파일 중에는 빌드할 수 없습니다.");
                return;
            }

            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[PlayModeController] Play 모드를 먼저 종료해주세요.");
                return;
            }

            // 씬 저장
            UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            // Build And Run 실행
            bool success = EditorApplication.ExecuteMenuItem("File/Build And Run");
            if (success)
            {
                Debug.Log("[PlayModeController] Build And Run 시작됨");
            }
            else
            {
                Debug.LogError("[PlayModeController] Build And Run 실패");
            }
        }

        [MenuItem("WOOPANG/MCP/Open Build Settings")]
        public static void OpenBuildSettings()
        {
            EditorApplication.ExecuteMenuItem("File/Build Settings...");
            Debug.Log("[PlayModeController] Build Settings 열림");
        }

        // ========================================
        // 상태 확인
        // ========================================

        [MenuItem("WOOPANG/MCP/Log Status")]
        public static void LogStatus()
        {
            string status = $"[PlayModeController] 상태:\n" +
                           $"  - isPlaying: {EditorApplication.isPlaying}\n" +
                           $"  - isPaused: {EditorApplication.isPaused}\n" +
                           $"  - isCompiling: {EditorApplication.isCompiling}\n" +
                           $"  - isUpdating: {EditorApplication.isUpdating}";
            Debug.Log(status);
        }

        // ========================================
        // Menu Item Validation
        // ========================================

        [MenuItem("WOOPANG/MCP/Start Play Mode", true)]
        private static bool ValidateStartPlayMode()
        {
            return !EditorApplication.isPlaying && !EditorApplication.isCompiling;
        }

        [MenuItem("WOOPANG/MCP/Stop Play Mode", true)]
        private static bool ValidateStopPlayMode()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("WOOPANG/MCP/Pause Play Mode", true)]
        private static bool ValidatePausePlayMode()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("WOOPANG/MCP/Step Frame", true)]
        private static bool ValidateStepFrame()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("WOOPANG/MCP/Build And Run", true)]
        private static bool ValidateBuildAndRun()
        {
            return !EditorApplication.isPlaying && !EditorApplication.isCompiling;
        }
    }
}
#endif
