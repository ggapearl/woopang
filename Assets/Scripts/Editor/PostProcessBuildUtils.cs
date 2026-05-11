using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif


namespace Editor
{
#if UNITY_IOS

    /// <summary>
    /// IOSSupportPreprocessBuild (callbackOrder=0) 이 매 빌드마다 CocoaPod XML 파일을 재생성하므로,
    /// 그 직후 (callbackOrder=1) 에 삭제하여 CocoaPods ARCore와 Swift Package ARCoreGeospatial 충돌 방지.
    /// Swift Package Manager를 통해 ARCore를 추가하므로 CocoaPods 버전은 불필요.
    /// </summary>
    public class RemoveARCoreCocoaPodXMLs : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1; // IOSSupportPreprocessBuild(0) 이후 실행

        private static readonly string[] _cocoaPodXmlFiles = new[]
        {
            "Assets/ExtensionsAssets/Editor/ARCoreiOSDependencies.xml",
            "Assets/ExtensionsAssets/Editor/ARCoreiOSGeospatialDependencies.xml",
        };

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            foreach (var relativePath in _cocoaPodXmlFiles)
            {
                string fullPath = Path.Combine(UnityEngine.Application.dataPath, "..", relativePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    string metaPath = fullPath + ".meta";
                    if (File.Exists(metaPath))
                        File.Delete(metaPath);
                    UnityEngine.Debug.Log($"[WOOPANG] CocoaPod XML 삭제 (SPM 사용): {relativePath}");
                }
            }

            AssetDatabase.Refresh();
        }
    }

    public static class PostProcessBuildUtils
    {
        public static bool enableBitcode = false;
        private const string ARCoreSwiftPackageName = "ARCoreGeospatial";
        private const string ARCoreSwiftPackageURL = "https://github.com/google-ar/arcore-ios-sdk";
        private const string ARCoreSwiftPackageVersion = "1.52.0";

        // ============================================================
        // Xcode 프로젝트 설정 (팀 ID 등)
        // ============================================================
        private const string DEVELOPMENT_TEAM_ID = "DDX8R79VU2"; // YOUNGHOON KIM 팀 ID
        private const string PROVISIONING_PROFILE_SPECIFIER = ""; // 프로비저닝 프로파일 이름 (선택사항, Xcode Managed로 자동 관리)
        private const string IOS_DEPLOYMENT_TARGET = "15.0"; // 최소 iOS 버전

        // ============================================================
        // 백업 파일 자동 복사 설정
        // ============================================================
        private const bool AUTO_COPY_BACKUP_FILES = true; // 자동 복사 활성화/비활성화
        private const string BACKUP_INFO_PLIST_PATH = "/Users/pdnom/Desktop/Info.plist";
        private const string BACKUP_UNITY_APP_CONTROLLER_PATH = "/Users/pdnom/Desktop/UnityAppController.mm";

        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
        {
            if (buildTarget != BuildTarget.iOS)
            {
                return;
            }

            // 1. PBXProject API 사용하는 작업을 먼저 실행
            AddImagesXcAssetsToBuildPhases(path);
            SetupBitcode(path);
            AddRemotePackage(path, ARCoreSwiftPackageName, ARCoreSwiftPackageURL, ARCoreSwiftPackageVersion);
            SetupXcodeProject(path);
            SetupPushNotificationEntitlements(path);

            // 2. 파일 복사/수정 작업
            if (AUTO_COPY_BACKUP_FILES)
            {
                CopyBackupFiles(path);
            }
            SetupInfoPlist(path);

            // 3. CocoaPods 정리는 맨 마지막 (pbxproj 직접 수정하므로 PBXProject API 이후에)
            RemoveCocoaPodsCompletely(path);
        }

        // ============================================================
        // 백업 파일 자동 복사
        // ============================================================
        private static void CopyBackupFiles(string buildPath)
        {
            bool infoPlistCopied = false;
            bool unityAppControllerCopied = false;

            // Info.plist 복사
            if (File.Exists(BACKUP_INFO_PLIST_PATH))
            {
                string targetInfoPlistPath = Path.Combine(buildPath, "Info.plist");
                try
                {
                    File.Copy(BACKUP_INFO_PLIST_PATH, targetInfoPlistPath, overwrite: true);
                    infoPlistCopied = true;
                    UnityEngine.Debug.Log($"[WOOPANG] ✅ Info.plist 백업 파일 자동 복사 완료: {BACKUP_INFO_PLIST_PATH} → {targetInfoPlistPath}");
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[WOOPANG] ❌ Info.plist 복사 실패: {ex.Message}");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[WOOPANG] ⚠️ Info.plist 백업 파일이 없습니다: {BACKUP_INFO_PLIST_PATH}");
            }

            // UnityAppController.mm 복사
            if (File.Exists(BACKUP_UNITY_APP_CONTROLLER_PATH))
            {
                string targetUnityAppControllerPath = Path.Combine(buildPath, "Classes", "UnityAppController.mm");
                try
                {
                    File.Copy(BACKUP_UNITY_APP_CONTROLLER_PATH, targetUnityAppControllerPath, overwrite: true);
                    unityAppControllerCopied = true;
                    UnityEngine.Debug.Log($"[WOOPANG] ✅ UnityAppController.mm 백업 파일 자동 복사 완료: {BACKUP_UNITY_APP_CONTROLLER_PATH} → {targetUnityAppControllerPath}");
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[WOOPANG] ❌ UnityAppController.mm 복사 실패: {ex.Message}");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[WOOPANG] ⚠️ UnityAppController.mm 백업 파일이 없습니다: {BACKUP_UNITY_APP_CONTROLLER_PATH}");
            }

            // 최종 요약
            if (infoPlistCopied && unityAppControllerCopied)
            {
                UnityEngine.Debug.Log("[WOOPANG] 🎉 백업 파일 자동 복사 완료! 이제 Xcode에서 바로 빌드하시면 됩니다.");
            }
            else if (!infoPlistCopied && !unityAppControllerCopied)
            {
                UnityEngine.Debug.LogWarning("[WOOPANG] ⚠️ 백업 파일을 찾을 수 없습니다. 수동으로 복사해주세요.");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[WOOPANG] ⚠️ 일부 백업 파일만 복사되었습니다. 나머지는 수동으로 복사해주세요.");
            }
        }

        // ============================================================
        // Xcode 프로젝트 자동 설정 (팀, 디바이스 등)
        // ============================================================
        private static void SetupXcodeProject(string path)
        {
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainTargetGuid = project.GetUnityMainTargetGuid();
            string unityFrameworkGuid = project.GetUnityFrameworkTargetGuid();

            // 팀 ID 설정 (자동 서명용)
            if (!string.IsNullOrEmpty(DEVELOPMENT_TEAM_ID))
            {
                // Main app target
                project.SetBuildProperty(mainTargetGuid, "DEVELOPMENT_TEAM", DEVELOPMENT_TEAM_ID);
                project.SetBuildProperty(mainTargetGuid, "CODE_SIGN_STYLE", "Automatic");

                // UnityFramework target
                project.SetBuildProperty(unityFrameworkGuid, "DEVELOPMENT_TEAM", DEVELOPMENT_TEAM_ID);
                project.SetBuildProperty(unityFrameworkGuid, "CODE_SIGN_STYLE", "Automatic");

                // notificationservice 타겟이 있다면 팀 ID 설정
                try
                {
                    string notificationServiceGuid = project.TargetGuidByName("notificationservice");
                    if (!string.IsNullOrEmpty(notificationServiceGuid))
                    {
                        project.SetBuildProperty(notificationServiceGuid, "DEVELOPMENT_TEAM", DEVELOPMENT_TEAM_ID);
                        project.SetBuildProperty(notificationServiceGuid, "CODE_SIGN_STYLE", "Automatic");
                        UnityEngine.Debug.Log($"[WOOPANG] Team ID 설정: notificationservice ({DEVELOPMENT_TEAM_ID})");
                    }
                }
                catch
                {
                    // notificationservice 타겟이 없으면 무시
                }

                UnityEngine.Debug.Log($"[WOOPANG] ✅ 모든 타겟에 Development Team ID 설정 완료: {DEVELOPMENT_TEAM_ID}");
            }

            // 프로비저닝 프로파일 설정 (선택사항)
            if (!string.IsNullOrEmpty(PROVISIONING_PROFILE_SPECIFIER))
            {
                project.SetBuildProperty(mainTargetGuid, "PROVISIONING_PROFILE_SPECIFIER", PROVISIONING_PROFILE_SPECIFIER);
            }

            // 디바이스 타겟: iPhone만 (iPad 제외) - 메인 타겟만
            project.SetBuildProperty(mainTargetGuid, "TARGETED_DEVICE_FAMILY", "1"); // 1 = iPhone only, 2 = iPad only, 1,2 = Universal

            // iOS 최소 버전 설정 - 모든 타겟
            project.SetBuildProperty(mainTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", IOS_DEPLOYMENT_TARGET);
            project.SetBuildProperty(unityFrameworkGuid, "IPHONEOS_DEPLOYMENT_TARGET", IOS_DEPLOYMENT_TARGET);

            // notificationservice 타겟에도 iOS 최소 버전 설정
            try
            {
                string notificationServiceGuid = project.TargetGuidByName("notificationservice");
                if (!string.IsNullOrEmpty(notificationServiceGuid))
                {
                    project.SetBuildProperty(notificationServiceGuid, "IPHONEOS_DEPLOYMENT_TARGET", IOS_DEPLOYMENT_TARGET);
                }
            }
            catch
            {
                // notificationservice 타겟이 없으면 무시
            }

            // Swift 버전 설정 (ARCore 등에 필요)
            project.SetBuildProperty(mainTargetGuid, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(unityFrameworkGuid, "SWIFT_VERSION", "5.0");

            // -ObjC 링커 플래그: ARCore 정적 xcframework의 ObjC 카테고리 로드에 필요
            project.AddBuildProperty(mainTargetGuid, "OTHER_LDFLAGS", "-ObjC");
            project.AddBuildProperty(unityFrameworkGuid, "OTHER_LDFLAGS", "-ObjC");

            // Bitcode 비활성화 (Unity 권장) - 모든 타겟
            project.SetBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
            project.SetBuildProperty(unityFrameworkGuid, "ENABLE_BITCODE", "NO");
            try
            {
                string notificationServiceGuid = project.TargetGuidByName("notificationservice");
                if (!string.IsNullOrEmpty(notificationServiceGuid))
                {
                    project.SetBuildProperty(notificationServiceGuid, "ENABLE_BITCODE", "NO");
                }
            }
            catch
            {
                // notificationservice 타겟이 없으면 무시
            }

            // === 모든 destination 비활성 (iPhone만 사용) ===
            // App Store Connect 경고: "UIRequiredDeviceCapabilities [arkit] not supported in visionOS"
            // Xcode General > Supported Destinations에 보이는 항목들을 코드로 강제 OFF
            // 적용 대상: mainTarget + UnityFramework + Tests + GameAssembly + notificationservice 등 모든 native target
            string[] destinationKeys = new[]
            {
                "SUPPORTS_XR_DESIGNED_FOR_IPHONE_IPAD",          // Apple Vision (Designed for iPad)
                "SUPPORTS_MAC_DESIGNED_FOR_IPHONE_IPAD",         // Mac (Designed for iPad)
                "SUPPORTS_MACCATALYST",                          // Mac (Mac Catalyst)
            };

            // 알려진 타깃 + 추가 타깃들 (Unity-iPhone Tests, GameAssembly 등) 모두 순회
            var allTargetGuids = new System.Collections.Generic.List<string> { mainTargetGuid, unityFrameworkGuid };
            foreach (var extraName in new[] { "Unity-iPhone Tests", "GameAssembly", "notificationservice" })
            {
                try
                {
                    string g = project.TargetGuidByName(extraName);
                    if (!string.IsNullOrEmpty(g)) allTargetGuids.Add(g);
                }
                catch { }
            }

            foreach (var tGuid in allTargetGuids)
            {
                foreach (var key in destinationKeys)
                {
                    project.SetBuildProperty(tGuid, key, "NO");
                }
                // === dSYM 생성 강제 ===
                project.SetBuildProperty(tGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");
                project.SetBuildProperty(tGuid, "STRIP_INSTALLED_PRODUCT", "NO");
                project.SetBuildProperty(tGuid, "COPY_PHASE_STRIP", "NO");
            }
            // UnityFramework는 archive 시점에 post-processing 활성화 (dSYM 동봉 보장)
            project.SetBuildProperty(unityFrameworkGuid, "DEPLOYMENT_POSTPROCESSING", "YES");

            project.WriteToFile(projectPath);
            UnityEngine.Debug.Log($"[WOOPANG] Xcode 프로젝트 설정 완료 (팀: DDX8R79VU2, 디바이스: iPhone만, iOS 15.0+, destination XR/Mac/Catalyst OFF, dSYM 강제) — {allTargetGuids.Count}개 타깃 적용");
        }

        // ============================================================
        // Info.plist 권한 설명 및 ATS 예외 설정
        // ============================================================
        private static void SetupInfoPlist(string path)
        {
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict rootDict = plist.root;

            // === 위치 권한 ===
            SetIfNotExists(rootDict, "NSLocationWhenInUseUsageDescription",
                "WOOPANG에서 주변 AR 콘텐츠를 찾고 사용자 위치를 표시하기 위해 위치 정보가 필요합니다.");
            SetIfNotExists(rootDict, "NSLocationAlwaysAndWhenInUseUsageDescription",
                "WOOPANG에서 근처 AR 콘텐츠 알림을 받기 위해 백그라운드 위치 정보가 필요합니다.");

            // === 카메라 권한 (NativeCamera가 없을 경우 fallback) ===
            SetIfNotExists(rootDict, "NSCameraUsageDescription",
                "WOOPANG에서 AR 체험과 사진 촬영을 위해 카메라 접근이 필요합니다.");

            // === 사진 라이브러리 권한 (NativeGallery가 없을 경우 fallback) ===
            SetIfNotExists(rootDict, "NSPhotoLibraryUsageDescription",
                "WOOPANG에서 프로필 사진 및 콘텐츠 업로드를 위해 사진 라이브러리 접근이 필요합니다.");
            SetIfNotExists(rootDict, "NSPhotoLibraryAddUsageDescription",
                "WOOPANG에서 촬영한 사진을 저장하기 위해 사진 라이브러리 접근이 필요합니다.");

            // === 마이크 권한 ===
            SetIfNotExists(rootDict, "NSMicrophoneUsageDescription",
                "WOOPANG에서 영상 촬영 시 마이크 접근이 필요합니다.");

            // === App Category 설정 ===
            SetIfNotExists(rootDict, "LSApplicationCategoryType",
                "public.app-category.lifestyle");

            // === 버전 번호 자동 업데이트 (Unity Player Settings에서 가져옴) ===
            string unityVersion = UnityEditor.PlayerSettings.bundleVersion;
            string unityBuildNumber = UnityEditor.PlayerSettings.iOS.buildNumber;

            if (!string.IsNullOrEmpty(unityVersion))
            {
                rootDict.SetString("CFBundleShortVersionString", unityVersion);
                UnityEngine.Debug.Log($"[WOOPANG] Version 설정: {unityVersion}");
            }

            if (!string.IsNullOrEmpty(unityBuildNumber))
            {
                rootDict.SetString("CFBundleVersion", unityBuildNumber);
                UnityEngine.Debug.Log($"[WOOPANG] Build Number 설정: {unityBuildNumber}");
            }

            // === Status Bar 설정 (화면 겹침 방지) ===
            rootDict.SetBoolean("UIStatusBarHidden", false); // Status bar 표시
            rootDict.SetString("UIStatusBarStyle", "UIStatusBarStyleLightContent"); // 흰색 아이콘
            rootDict.SetBoolean("UIViewControllerBasedStatusBarAppearance", false); // _SetStatusBarStyle API 사용 위해 false 필수
            rootDict.SetBoolean("UIRequiresFullScreen", true); // Safe Area 강제 적용

            // === URL Scheme 등록 (딥링크: woopang://auth?token=...) ===
            if (!rootDict.values.ContainsKey("CFBundleURLTypes"))
            {
                PlistElementArray urlTypes = rootDict.CreateArray("CFBundleURLTypes");
                PlistElementDict woopangScheme = urlTypes.AddDict();
                woopangScheme.SetString("CFBundleURLName", "com.woopang.app");
                PlistElementArray schemes = woopangScheme.CreateArray("CFBundleURLSchemes");
                schemes.AddString("woopang");
                UnityEngine.Debug.Log("[WOOPANG] URL Scheme 'woopang://' 등록 완료");
            }

            // === ATS(App Transport Security) 설정 ===
            // 모든 API가 HTTPS (woopang.com) 경유하므로 기본 ATS 정책 유지
            if (!rootDict.values.ContainsKey("NSAppTransportSecurity"))
            {
                PlistElementDict atsDict = rootDict.CreateDict("NSAppTransportSecurity");
                atsDict.SetBoolean("NSAllowsArbitraryLoads", false);
            }

            // === 백그라운드 모드 ===
            PlistElementArray bgModes;
            if (rootDict.values.ContainsKey("UIBackgroundModes"))
            {
                bgModes = rootDict["UIBackgroundModes"].AsArray();
            }
            else
            {
                bgModes = rootDict.CreateArray("UIBackgroundModes");
            }

            // 백그라운드 모드에 필요한 항목 추가
            AddUniqueToArray(bgModes, "remote-notification");
            AddUniqueToArray(bgModes, "location");
            AddUniqueToArray(bgModes, "fetch");

            plist.WriteToFile(plistPath);
            UnityEngine.Debug.Log("[WOOPANG] iOS Info.plist 설정 완료 (권한, ATS, 백그라운드 모드)");
        }

        private static void SetIfNotExists(PlistElementDict dict, string key, string value)
        {
            if (!dict.values.ContainsKey(key))
            {
                dict.SetString(key, value);
            }
        }

        private static void AddUniqueToArray(PlistElementArray array, string value)
        {
            // 이미 있는지 확인
            foreach (var item in array.values)
            {
                if (item is PlistElementString str && str.value == value)
                    return;
            }
            array.AddString(value);
        }

        // ============================================================
        // Push Notification Entitlements
        // ============================================================
        private static void SetupPushNotificationEntitlements(string path)
        {
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainTargetGuid = project.GetUnityMainTargetGuid();
            string relativeEntitlementsPath = "Unity-iPhone/Unity-iPhone.entitlements";
            string entitlementsPath = Path.Combine(path, relativeEntitlementsPath);

            // Entitlements 파일 먼저 생성 (AddCapability보다 먼저 있어야 함)
            if (!File.Exists(entitlementsPath))
            {
                // 디렉토리 확인
                string entitlementsDir = Path.GetDirectoryName(entitlementsPath);
                if (!Directory.Exists(entitlementsDir))
                    Directory.CreateDirectory(entitlementsDir);

                var entitlements = new PlistDocument();
                entitlements.root.SetString("aps-environment", "development");
                entitlements.WriteToFile(entitlementsPath);
            }

            // Entitlements 파일 경로와 함께 Capability 추가
            project.AddCapability(mainTargetGuid, PBXCapabilityType.PushNotifications, relativeEntitlementsPath);
            project.AddCapability(mainTargetGuid, PBXCapabilityType.BackgroundModes);

            // 프로젝트에 entitlements 파일 추가 및 빌드 설정
            project.SetBuildProperty(mainTargetGuid, "CODE_SIGN_ENTITLEMENTS", relativeEntitlementsPath);
            project.WriteToFile(projectPath);

            UnityEngine.Debug.Log("[WOOPANG] iOS Push Notification Entitlements 설정 완료");
        }

        // ============================================================
        // 기존 빌드 설정
        // ============================================================
        private static void AddImagesXcAssetsToBuildPhases(string path)
        {
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainGuid = project.GetUnityMainTargetGuid();
            project.AddFileToBuild(mainGuid, project.AddFile("Unity-iPhone/Images.xcassets", "Images.xcassets"));
            project.WriteToFile(projectPath);
        }

        private static void SetupBitcode(string pathToBuiltProject)
        {
            var project = new PBXProject();
            var pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            project.ReadFromFile(pbxPath);
            SetupBitcodeFramework(project);
            SetupBitcodeMain(project);
            project.WriteToFile(pbxPath);
        }

        private static void SetupBitcodeFramework(PBXProject project)
        {
            SetupBitcode(project, project.GetUnityFrameworkTargetGuid());
        }

        private static void SetupBitcodeMain(PBXProject project)
        {
            SetupBitcode(project, project.GetUnityMainTargetGuid());
        }

        private static void SetupBitcode(PBXProject project, string targetGUID)
        {
            project.SetBuildProperty(targetGUID, "ENABLE_BITCODE", enableBitcode ? "YES" : "NO");
        }

        /// <summary>
        /// CocoaPods 완전 제거.
        /// iOS에서 모든 pod 의존성이 비활성화되어 있으므로 (Firebase iOS는 주석 처리, ARCore는 SPM 사용),
        /// Podfile, Pods 디렉토리, xcworkspace, Pods 프레임워크 참조를 모두 제거.
        /// 주의: pbxproj를 직접 수정하므로 PBXProject API 사용 이후에 호출해야 함.
        /// </summary>
        public static void RemoveCocoaPodsCompletely(string buildPath)
        {
            bool anyCleanup = false;

            // 1. Podfile 삭제
            string podfilePath = Path.Combine(buildPath, "Podfile");
            if (File.Exists(podfilePath))
            {
                File.Delete(podfilePath);
                anyCleanup = true;
            }

            // 2. Podfile.lock 삭제
            string podfileLock = Path.Combine(buildPath, "Podfile.lock");
            if (File.Exists(podfileLock))
                File.Delete(podfileLock);

            // 3. Pods 디렉토리 전체 삭제
            string podsDir = Path.Combine(buildPath, "Pods");
            if (Directory.Exists(podsDir))
            {
                Directory.Delete(podsDir, true);
                anyCleanup = true;
            }

            // 4. CocoaPods가 생성한 xcworkspace 삭제 (xcodeproj만 사용)
            string xcworkspacePath = Path.Combine(buildPath, "Unity-iPhone.xcworkspace");
            if (Directory.Exists(xcworkspacePath))
            {
                Directory.Delete(xcworkspacePath, true);
                anyCleanup = true;
            }

            // 5. pbxproj에서 CocoaPods 참조 제거
            RemovePodsFromPbxproj(buildPath);

            if (anyCleanup)
                UnityEngine.Debug.Log("[WOOPANG] CocoaPods 완전 제거 완료 (Podfile, Pods, xcworkspace, pbxproj 참조)");
        }

        private static void RemovePodsFromPbxproj(string buildPath)
        {
            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            if (!File.Exists(projectPath))
                return;

            string content = File.ReadAllText(projectPath);
            if (!content.Contains("Pods"))
                return;

            string[] podsPatterns = new[]
            {
                "Pods_UnityFramework",
                "Pods_Unity_iPhone",
                "Pods-UnityFramework",
                "Pods-Unity-iPhone",
                "libPods-UnityFramework",
                "libPods-Unity-iPhone",
            };

            string[] lines = content.Split('\n');
            var cleanedLines = new System.Collections.Generic.List<string>();

            foreach (string line in lines)
            {
                bool shouldRemove = false;
                foreach (string pattern in podsPatterns)
                {
                    if (line.Contains(pattern))
                    {
                        shouldRemove = true;
                        break;
                    }
                }

                if (shouldRemove)
                    continue;

                cleanedLines.Add(line);
            }

            if (cleanedLines.Count != lines.Length)
            {
                File.WriteAllText(projectPath, string.Join("\n", cleanedLines));
                UnityEngine.Debug.Log($"[WOOPANG] pbxproj에서 CocoaPods 참조 {lines.Length - cleanedLines.Count}줄 제거");
            }
        }

        private static void AddRemotePackage(string pathToBuildProject, string packageName, string packageUrl, string version)
        {
            string projectPath = PBXProject.GetPBXProjectPath(pathToBuildProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string packageGuid =
                project.AddRemotePackageReferenceAtVersionUpToNextMajor(url: packageUrl, version: version);
            // UnityFramework 타겟에 링크 (C# 코드가 여기서 컴파일되므로 네이티브 심볼이 여기에 필요)
            project.AddRemotePackageFrameworkToProject(targetGuid: project.GetUnityFrameworkTargetGuid(), name: packageName,
                packageGuid: packageGuid, weak: false);

            project.WriteToFile(projectPath);
        }

    }

    /// <summary>
    /// EDM4U IOSResolver가 PostProcessBuild에서 pod install을 실행할 수 있으므로,
    /// 가장 마지막에 한 번 더 CocoaPods 잔여물을 정리.
    /// </summary>
    public class CocoaPodsFinalCleanup
    {
        [PostProcessBuild(int.MaxValue)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
        {
            if (buildTarget != BuildTarget.iOS)
                return;

            PostProcessBuildUtils.RemoveCocoaPodsCompletely(path);
        }
    }
#endif
}
