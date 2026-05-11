using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 빌드 직전 buildNumber/bundleVersionCode를 YYMMDD+순번 형식으로 자동 관리.
    /// 형식: YYMMDDsss (예: 260511001 = 26년 5월 11일 첫 빌드)
    ///
    /// 동작 규칙:
    /// 1. 사용자가 PlayerSettings에서 직접 수정한 경우 → 그 값 그대로 빌드 사용 (사용자 의도 우선)
    /// 2. 사용자가 손 안 대고 자동 흐름으로 진행한 경우 → 같은 날짜면 +1, 새 날짜면 001 리셋
    ///
    /// 수동/자동 구분 방법: EditorPrefs에 마지막 자동 세팅 값을 기억해두고
    /// 빌드 시점 값이 그것과 다르면 → 사용자가 손댄 거라 그대로 사용
    ///
    /// 사용 예:
    /// - PlayerSettings에 "260511003" 입력 후 빌드 → 260511003 그대로 빌드
    /// - 그 다음 빌드 (수동 입력 없음) → 자동 +1 = 260511004
    /// - 새 날짜 빌드 → 260512001 자동 리셋
    /// </summary>
    public class BuildNumberAutoIncrement : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const string PREF_IOS_LAST_AUTO = "WOOPANG.BuildNumber.iOS.LastAutoSet";
        private const string PREF_ANDROID_LAST_AUTO = "WOOPANG.BuildNumber.Android.LastAutoSet";

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            string datePrefix = DateTime.Now.ToString("yyMMdd");

            if (target == BuildTarget.iOS)
            {
                string current = PlayerSettings.iOS.buildNumber ?? "0";
                string lastAuto = EditorPrefs.GetString(PREF_IOS_LAST_AUTO, "");
                string resolved = ResolveBuildNumber(current, lastAuto, datePrefix);

                PlayerSettings.iOS.buildNumber = resolved;
                EditorPrefs.SetString(PREF_IOS_LAST_AUTO, resolved);
                AssetDatabase.SaveAssets();
                Debug.Log($"[WOOPANG] iOS buildNumber: {current} → {resolved}");
            }
            else if (target == BuildTarget.Android)
            {
                int currentCode = PlayerSettings.Android.bundleVersionCode;
                string current = currentCode.ToString();
                string lastAuto = EditorPrefs.GetString(PREF_ANDROID_LAST_AUTO, "");
                string resolved = ResolveBuildNumber(current, lastAuto, datePrefix);

                int newCode = int.Parse(resolved);
                PlayerSettings.Android.bundleVersionCode = newCode;
                EditorPrefs.SetString(PREF_ANDROID_LAST_AUTO, resolved);
                AssetDatabase.SaveAssets();
                Debug.Log($"[WOOPANG] Android bundleVersionCode: {currentCode} → {newCode}");
            }
        }

        /// <summary>
        /// 우선순위:
        /// 1. current가 오늘 prefix인데 lastAuto와 다르면 → 사용자 수동 수정, 그대로 사용
        /// 2. current가 오늘 prefix이고 lastAuto와 같으면 → 자동 +1
        /// 3. 그 외 (다른 날짜, 0, 빈 값) → 오늘 001로 리셋
        /// </summary>
        private string ResolveBuildNumber(string current, string lastAuto, string datePrefix)
        {
            bool sameDate = !string.IsNullOrEmpty(current) && current.StartsWith(datePrefix);

            if (sameDate)
            {
                if (current != lastAuto)
                {
                    return current;
                }

                string serialPart = current.Substring(datePrefix.Length);
                if (int.TryParse(serialPart, out int serial))
                {
                    return $"{datePrefix}{(serial + 1):D3}";
                }
            }

            return $"{datePrefix}001";
        }
    }
}
