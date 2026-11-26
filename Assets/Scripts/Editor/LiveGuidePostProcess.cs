using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif

namespace Editor
{
#if UNITY_IOS
    // 길안내 실시간 현황(Live Activity) — 홈으로 나가도 아일랜드 · 잠금 화면에 남은 거리 · 방향 (11-26 대표님 10번 ②)
    // · PostProcessBuildUtils(999) 다음에 돈다 — 위젯 확장 타깃(WoopangGuideWidget)을 만들고 native/ios/WoopangGuideWidget 를 넣는다.
    // · 맥에서 위젯 확장 때문에 빌드 · 서명이 막히면 WOOPANG_NO_LIVE_ACTIVITY=1 로 유니티를 띄워 다시 뽑는다
    //   → 확장을 만들지 않고 앱 쪽 Swift 두 파일도 빌드에서 뺀다 (WoopangLiveGuide.mm 은 남아 아무것도 하지 않는다 · 앱은 그대로).
    public static class LiveGuidePostProcess
    {
        private const string WidgetName = "WoopangGuideWidget";
        private const string WidgetBundleSuffix = ".GuideWidget";
        private const string WidgetDeploymentTarget = "16.1";   // 실시간 현황이 iOS 16.1 부터 — 앱은 15.0 그대로
        private const string TeamId = "DDX8R79VU2";
        private const string SharedSwift = "Libraries/Plugins/iOS/WoopangGuideAttributes.swift";
        private const string AppSwift = "Libraries/Plugins/iOS/WoopangLiveGuide.swift";

        [PostProcessBuild(1000)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
        {
            if (buildTarget != BuildTarget.iOS) return;
            try
            {
                if (Environment.GetEnvironmentVariable("WOOPANG_NO_LIVE_ACTIVITY") == "1") Skip(path);
                else Apply(path);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[WOOPANG] 길안내 실시간 현황 설정 실패: " + e);
            }
        }

        private static void Skip(string path)
        {
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string framework = project.GetUnityFrameworkTargetGuid();
            foreach (var file in new[] { SharedSwift, AppSwift })
            {
                string guid = project.FindFileGuidByProjectPath(file);
                if (!string.IsNullOrEmpty(guid)) project.RemoveFileFromBuild(framework, guid);
            }
            project.WriteToFile(projectPath);
            UnityEngine.Debug.Log("[WOOPANG] 길안내 실시간 현황 끔 (WOOPANG_NO_LIVE_ACTIVITY=1) — 위젯 확장 없이 빌드");
        }

        private static void Apply(string path)
        {
            string source = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName, "native", "ios", WidgetName);
            if (!File.Exists(Path.Combine(source, WidgetName + ".swift")))
            {
                UnityEngine.Debug.LogError("[WOOPANG] 길안내 위젯 원본이 없습니다: " + source);
                return;
            }

            // 1. 위젯 원본을 Xcode 프로젝트 옆으로
            string dest = Path.Combine(path, WidgetName);
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            }

            // 2. 앱 Info.plist — 실시간 현황 허용 · 백그라운드 위치(길안내 중에만 켠다)
            string appPlistPath = Path.Combine(path, "Info.plist");
            var appPlist = new PlistDocument();
            appPlist.ReadFromFile(appPlistPath);
            PlistElementDict root = appPlist.root;
            root.SetBoolean("NSSupportsLiveActivities", true);
            PlistElementArray modes = root.values.ContainsKey("UIBackgroundModes") ? root["UIBackgroundModes"].AsArray() : root.CreateArray("UIBackgroundModes");
            bool hasLocation = false;
            foreach (var v in modes.values)
                if (v.AsString() == "location") hasLocation = true;
            if (!hasLocation) modes.AddString("location");
            appPlist.WriteToFile(appPlistPath);

            // 3. 위젯 Info.plist 버전 = 앱 버전 (다르면 App Store 업로드에서 걸린다)
            string version = root.values.ContainsKey("CFBundleShortVersionString") ? root["CFBundleShortVersionString"].AsString() : PlayerSettings.bundleVersion;
            string build = root.values.ContainsKey("CFBundleVersion") ? root["CFBundleVersion"].AsString() : PlayerSettings.iOS.buildNumber;
            string widgetPlistPath = Path.Combine(dest, "Info.plist");
            var widgetPlist = new PlistDocument();
            widgetPlist.ReadFromFile(widgetPlistPath);
            widgetPlist.root.SetString("CFBundleShortVersionString", version);
            widgetPlist.root.SetString("CFBundleVersion", build);
            widgetPlist.WriteToFile(widgetPlistPath);

            // 4. 위젯 확장 타깃
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string main = project.GetUnityMainTargetGuid();
            string framework = project.GetUnityFrameworkTargetGuid();
            string appId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);

            string widget = null;
            try { widget = project.TargetGuidByName(WidgetName); } catch { }
            if (string.IsNullOrEmpty(widget))
                widget = project.AddAppExtension(main, WidgetName, appId + WidgetBundleSuffix, WidgetName + "/Info.plist");

            // AddFile · AddFileToBuild 는 이미 있으면 그대로 둔다 (「Append」 로 다시 뽑아도 두 번 들어가지 않는다)
            string plistRef = WidgetName + "/Info.plist";
            project.AddFile(plistRef, plistRef);
            string swiftRef = WidgetName + "/" + WidgetName + ".swift";
            project.AddFileToBuild(widget, project.AddFile(swiftRef, swiftRef));

            string sharedGuid = project.FindFileGuidByProjectPath(SharedSwift);
            if (string.IsNullOrEmpty(sharedGuid))
            {
                UnityEngine.Debug.LogError("[WOOPANG] 길안내 실시간 현황: " + SharedSwift + " 가 프로젝트에 없습니다 (Plugins/iOS 설정 확인)");
                return;
            }
            project.AddFileToBuild(widget, sharedGuid);   // 데이터 모양은 앱과 같은 파일을 같이 컴파일

            foreach (var fw in new[] { "WidgetKit.framework", "SwiftUI.framework", "ActivityKit.framework" })
                if (!project.ContainsFramework(widget, fw)) project.AddFrameworkToProject(widget, fw, false);

            // 앱(UnityFramework)은 iOS 15 기기에서도 떠야 하므로 ActivityKit 을 '있으면 쓰는'(Optional) 연결로
            if (!project.ContainsFramework(framework, "ActivityKit.framework"))
                project.AddFrameworkToProject(framework, "ActivityKit.framework", true);

            project.SetBuildProperty(widget, "IPHONEOS_DEPLOYMENT_TARGET", WidgetDeploymentTarget);
            project.SetBuildProperty(widget, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(widget, "TARGETED_DEVICE_FAMILY", "1");   // 앱과 같게 iPhone 만
            project.SetBuildProperty(widget, "DEVELOPMENT_TEAM", TeamId);
            project.SetBuildProperty(widget, "CODE_SIGN_STYLE", "Automatic");
            project.SetBuildProperty(widget, "PRODUCT_BUNDLE_IDENTIFIER", appId + WidgetBundleSuffix);
            project.SetBuildProperty(widget, "INFOPLIST_FILE", WidgetName + "/Info.plist");
            project.SetBuildProperty(widget, "GENERATE_INFOPLIST_FILE", "NO");
            project.SetBuildProperty(widget, "SKIP_INSTALL", "YES");
            project.SetBuildProperty(widget, "APPLICATION_EXTENSION_API_ONLY", "YES");
            project.SetBuildProperty(widget, "CLANG_ENABLE_MODULES", "YES");
            project.SetBuildProperty(widget, "ENABLE_BITCODE", "NO");
            project.SetBuildProperty(widget, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks @executable_path/../../Frameworks");
            foreach (var key in new[] { "SUPPORTS_XR_DESIGNED_FOR_IPHONE_IPAD", "SUPPORTS_MAC_DESIGNED_FOR_IPHONE_IPAD", "SUPPORTS_MACCATALYST" })
                project.SetBuildProperty(widget, key, "NO");
            project.SetBuildProperty(widget, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

            project.WriteToFile(projectPath);
            UnityEngine.Debug.Log($"[WOOPANG] 길안내 실시간 현황 위젯 확장 준비 완료 ({appId}{WidgetBundleSuffix} · iOS {WidgetDeploymentTarget}+ · 버전 {version} ({build}))");
        }
    }
#endif
}
