using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif


namespace Editor
{
#if UNITY_IOS

    public static class PostProcessBuildUtils
    {
        public static bool enableBitcode = false;
        private const string ARCoreSwiftPackageName = "ARCoreGeospatial";
        private const string ARCoreSwiftPackageURL = "https://github.com/google-ar/arcore-ios-sdk";
        private const string ARCoreSwiftPackageVersion = "1.38.0";

        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
        {
            if (buildTarget != BuildTarget.iOS)
            {
                return;
            }

            AddImagesXcAssetsToBuildPhases(path);
            SetupBitcode(path);
            AddRemotePackage(path, ARCoreSwiftPackageName, ARCoreSwiftPackageURL, ARCoreSwiftPackageVersion);
            SetupInfoPlist(path);
            SetupPushNotificationEntitlements(path);
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

            // Push Notification capability 추가
            project.AddCapability(mainTargetGuid, PBXCapabilityType.PushNotifications);
            project.AddCapability(mainTargetGuid, PBXCapabilityType.BackgroundModes);

            project.WriteToFile(projectPath);

            // Entitlements 파일 생성
            string entitlementsPath = Path.Combine(path, "Unity-iPhone", "Unity-iPhone.entitlements");
            if (!File.Exists(entitlementsPath))
            {
                var entitlements = new PlistDocument();
                entitlements.root.SetString("aps-environment", "development");
                entitlements.WriteToFile(entitlementsPath);

                // 프로젝트에 entitlements 파일 추가
                project.ReadFromFile(projectPath);
                string fileGuid = project.AddFile(
                    "Unity-iPhone/Unity-iPhone.entitlements",
                    "Unity-iPhone.entitlements");
                project.AddFileToBuild(mainTargetGuid, fileGuid);
                project.SetBuildProperty(mainTargetGuid, "CODE_SIGN_ENTITLEMENTS",
                    "Unity-iPhone/Unity-iPhone.entitlements");
                project.WriteToFile(projectPath);
            }

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

        private static void AddRemotePackage(string pathToBuildProject, string packageName, string packageUrl, string version)
        {
            string projectPath = PBXProject.GetPBXProjectPath(pathToBuildProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string packageGuid =
                project.AddRemotePackageReferenceAtVersionUpToNextMajor(url: packageUrl, version: version);
            project.AddRemotePackageFrameworkToProject(targetGuid: project.GetUnityMainTargetGuid(), name: packageName,
                packageGuid: packageGuid, weak: false);

            project.WriteToFile(projectPath);
        }
    }
#endif
}
