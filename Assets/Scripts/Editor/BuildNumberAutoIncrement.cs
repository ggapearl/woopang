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

        // bundleVersion(마케팅 버전 1.2.x) 자동 관리용
        private const string PREF_VERSION_LAST_AUTO = "WOOPANG.BundleVersion.LastAutoSet";
        private const string PREF_VERSION_LAST_DATE = "WOOPANG.BundleVersion.LastAutoDate";

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            string datePrefix = DateTime.Now.ToString("yyMMdd");

            // ── bundleVersion(1.2.x) — 하루 한 번 patch +1 ──────────────
            // iOS/Android 를 같은 날 빌드하면 같은 버전이어야 하므로, 매 빌드가 아니라
            // "그 날 첫 자동 빌드"에만 올린다. 사용자가 직접 바꾼 값은 존중한다.
            AutoBumpBundleVersion();

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
        /// bundleVersion 을 하루 한 번 patch(마지막 숫자) +1 한다.
        ///  - 현재 값이 마지막 자동값과 다르면 → 사용자가 직접 수정한 것 → 그대로 두고 기록만 갱신
        ///  - 같으면(자동 흐름) → 오늘 아직 안 올렸을 때만 +1, 같은 날 재빌드는 유지
        ///    (그래야 iOS·Android 를 같은 날 빌드해도 같은 버전이 된다)
        /// </summary>
        private void AutoBumpBundleVersion()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            string current = PlayerSettings.bundleVersion ?? "";
            string lastAuto = EditorPrefs.GetString(PREF_VERSION_LAST_AUTO, "");
            string lastDate = EditorPrefs.GetString(PREF_VERSION_LAST_DATE, "");

            // 사용자가 직접 바꾼 값은 그대로 존중 (기록만 최신화)
            if (current != lastAuto)
            {
                EditorPrefs.SetString(PREF_VERSION_LAST_AUTO, current);
                EditorPrefs.SetString(PREF_VERSION_LAST_DATE, today);
                Debug.Log($"[WOOPANG] bundleVersion(수동값 존중): {current}");
                return;
            }

            // 오늘 이미 자동으로 올렸으면 유지
            if (lastDate == today)
            {
                Debug.Log($"[WOOPANG] bundleVersion(오늘 이미 증가): {current}");
                return;
            }

            string bumped = BumpPatch(current);
            PlayerSettings.bundleVersion = bumped;
            EditorPrefs.SetString(PREF_VERSION_LAST_AUTO, bumped);
            EditorPrefs.SetString(PREF_VERSION_LAST_DATE, today);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WOOPANG] bundleVersion: {current} → {bumped}");
        }

        /// <summary>
        /// "1.2.50" → "1.2.51". patch(마지막)가 99를 넘으면 minor +1 하고 patch 는 00 으로.
        /// 예: 1.2.99 → 1.3.00, 1.3.99 → 1.4.00. patch 는 항상 두 자리(00~99).
        /// major.minor.patch 3조각 정수 형식이 아니면 원본을 그대로 둔다.
        /// </summary>
        private string BumpPatch(string version)
        {
            if (string.IsNullOrEmpty(version)) return version;
            string[] parts = version.Split('.');
            if (parts.Length != 3) return version;
            if (!int.TryParse(parts[0], out int major) ||
                !int.TryParse(parts[1], out int minor) ||
                !int.TryParse(parts[2], out int patch))
                return version;

            patch++;
            if (patch > 99)
            {
                minor++;
                patch = 0;
            }
            return $"{major}.{minor}.{patch:D2}";
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
