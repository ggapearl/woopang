using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
namespace Woopang.Editor.MCP
{
    /// <summary>
    /// MCP Fast Play Mode Settings
    /// MCP 서버가 Play 모드에서도 동작할 수 있도록 설정하는 확장
    ///
    /// 사용법:
    /// 1. WOOPANG > MCP > Settings > Enable Fast Play Mode 클릭
    /// 2. 또는 Project Settings > Editor > Enter Play Mode Settings 에서 직접 설정
    ///
    /// Fast Play Mode가 활성화되면:
    /// - Domain Reload가 비활성화되어 MCP 서버가 Play 모드에서도 유지됨
    /// - 단, static 변수가 초기화되지 않으므로 주의 필요
    ///
    /// 참고: Play 모드 제어 메뉴는 PlayModeController.cs에서 제공
    /// </summary>
    [InitializeOnLoad]
    public static class McpFastPlayModeSettings
    {
        private static bool hasShownTip = false;

        static McpFastPlayModeSettings()
        {
            // 첫 번째 에디터 업데이트에서 팁 표시 (한 번만)
            EditorApplication.delayCall += ShowTipOnce;
        }

        private static void ShowTipOnce()
        {
            if (hasShownTip) return;
            hasShownTip = true;

            // Fast Play Mode가 비활성화되어 있고 MCP가 설치되어 있을 때만 팁 표시
            if (!EditorSettings.enterPlayModeOptionsEnabled)
            {
                // 조용히 로그만 남김 (팝업 없음)
                Debug.Log("[MCP Settings] TIP: Play 모드에서 MCP 서버를 유지하려면 WOOPANG > MCP > Settings > Enable Fast Play Mode");
            }
        }

        // ========================================
        // Settings 메뉴 (Play Mode 제어와 분리)
        // ========================================

        [MenuItem("WOOPANG/MCP/Settings/Enable Fast Play Mode", false, 100)]
        public static void EnableFastPlayMode()
        {
            if (EditorSettings.enterPlayModeOptionsEnabled &&
                EditorSettings.enterPlayModeOptions == EnterPlayModeOptions.DisableDomainReload)
            {
                Debug.Log("[MCP Settings] Fast Play Mode가 이미 활성화되어 있습니다.");
                return;
            }

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            Debug.Log("[MCP Settings] Fast Play Mode 활성화됨");
            Debug.Log("  - Domain Reload: 비활성화 (MCP 서버 유지됨)");
            Debug.Log("  - Scene Reload: 활성화 (씬 상태 초기화됨)");

            EditorUtility.DisplayDialog(
                "MCP Fast Play Mode",
                "Fast Play Mode가 활성화되었습니다.\n\n" +
                "이제 Play 모드에서도 MCP 서버가 유지됩니다.\n\n" +
                "주의: static 변수가 Play 모드 진입 시 초기화되지 않습니다.\n" +
                "문제가 발생하면 Disable Fast Play Mode로 복원하세요.",
                "확인"
            );
        }

        [MenuItem("WOOPANG/MCP/Settings/Disable Fast Play Mode", false, 101)]
        public static void DisableFastPlayMode()
        {
            if (!EditorSettings.enterPlayModeOptionsEnabled)
            {
                Debug.Log("[MCP Settings] Fast Play Mode가 이미 비활성화되어 있습니다.");
                return;
            }

            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            Debug.Log("[MCP Settings] Fast Play Mode 비활성화됨 (기본 설정 복원)");

            EditorUtility.DisplayDialog(
                "MCP Fast Play Mode",
                "기본 Play Mode 설정으로 복원되었습니다.\n\n" +
                "Play 모드 진입 시 Domain Reload가 발생하여\n" +
                "MCP 서버가 재시작됩니다.",
                "확인"
            );
        }

        [MenuItem("WOOPANG/MCP/Settings/Check Current Settings", false, 200)]
        public static void CheckCurrentSettings()
        {
            string fastPlayStatus = EditorSettings.enterPlayModeOptionsEnabled
                ? $"활성화 (옵션: {EditorSettings.enterPlayModeOptions})"
                : "비활성화 (기본 모드)";

            string mcpStatus = "MCP 서버 상태를 확인하려면 Unity Console을 확인하세요.";

            string message = $"Fast Play Mode: {fastPlayStatus}\n\n" +
                           $"현재 상태:\n" +
                           $"  - isPlaying: {EditorApplication.isPlaying}\n" +
                           $"  - isPaused: {EditorApplication.isPaused}\n" +
                           $"  - isCompiling: {EditorApplication.isCompiling}\n\n" +
                           mcpStatus;

            Debug.Log($"[MCP Settings] {message.Replace("\n\n", " | ").Replace("\n", ", ")}");

            EditorUtility.DisplayDialog("MCP 설정 상태", message, "확인");
        }

        // ========================================
        // Validation (체크 표시용)
        // ========================================

        [MenuItem("WOOPANG/MCP/Settings/Enable Fast Play Mode", true)]
        private static bool ValidateEnableFastPlayMode()
        {
            // 이미 활성화되어 있으면 비활성화
            Menu.SetChecked("WOOPANG/MCP/Settings/Enable Fast Play Mode",
                EditorSettings.enterPlayModeOptionsEnabled &&
                EditorSettings.enterPlayModeOptions == EnterPlayModeOptions.DisableDomainReload);
            return true;
        }

        [MenuItem("WOOPANG/MCP/Settings/Disable Fast Play Mode", true)]
        private static bool ValidateDisableFastPlayMode()
        {
            // 이미 비활성화되어 있으면 비활성화
            Menu.SetChecked("WOOPANG/MCP/Settings/Disable Fast Play Mode",
                !EditorSettings.enterPlayModeOptionsEnabled);
            return true;
        }
    }
}
#endif
