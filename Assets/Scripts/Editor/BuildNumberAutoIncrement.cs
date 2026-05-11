using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 빌드 직전 buildNumber/bundleVersionCode를 YYMMDD+순번 형식으로 자동 증가.
    /// 형식: YYMMDDsss (예: 260511001 = 26년 5월 11일 첫 빌드)
    /// - 같은 날짜 빌드: 순번 +1
    /// - 새 날짜 빌드: 순번 001로 리셋
    /// AssetDatabase.SaveAssets로 ProjectSettings.asset 디스크 저장 강제 →
    /// 사용자 수동 입력 후 디스크에 저장 안 되던 문제 해결.
    /// </summary>
    public class BuildNumberAutoIncrement : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0; // 다른 preprocess가 buildNumber 읽기 전에 먼저 실행

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            string datePrefix = DateTime.Now.ToString("yyMMdd");

            if (target == BuildTarget.iOS)
            {
                string oldNumber = PlayerSettings.iOS.buildNumber ?? "0";
                int nextSerial = ComputeNextSerial(oldNumber, datePrefix);
                string newNumber = $"{datePrefix}{nextSerial:D3}";
                PlayerSettings.iOS.buildNumber = newNumber;
                AssetDatabase.SaveAssets();
                Debug.Log($"[WOOPANG] iOS buildNumber: {oldNumber} → {newNumber}");
            }
            else if (target == BuildTarget.Android)
            {
                int oldCode = PlayerSettings.Android.bundleVersionCode;
                int nextSerial = ComputeNextSerial(oldCode.ToString(), datePrefix);
                int newCode = int.Parse($"{datePrefix}{nextSerial:D3}");
                PlayerSettings.Android.bundleVersionCode = newCode;
                AssetDatabase.SaveAssets();
                Debug.Log($"[WOOPANG] Android bundleVersionCode: {oldCode} → {newCode}");
            }
        }

        // 현재 값이 같은 날짜 prefix면 순번 +1, 아니면 1로 리셋
        private int ComputeNextSerial(string current, string datePrefix)
        {
            if (!string.IsNullOrEmpty(current) && current.StartsWith(datePrefix) && current.Length >= datePrefix.Length + 1)
            {
                string serialPart = current.Substring(datePrefix.Length);
                if (int.TryParse(serialPart, out int serial))
                {
                    return serial + 1;
                }
            }
            return 1;
        }
    }
}
