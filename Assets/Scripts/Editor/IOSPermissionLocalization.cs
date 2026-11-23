using System.IO;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace Editor
{
#if UNITY_IOS
    /// <summary>
    /// 아이폰 허락 창(위치 · 카메라 · 사진 · 마이크) 문구를 기기 언어로 — 우팡이 지원하는 6개 언어 (2026-11-17).
    ///
    /// 대표님 (11-16): 「특별히 언어를 나누지 않는 한, 권한요청, 사진촬영 등 기본 디바이스 언어에 따라 해당 언어로 … 디바이스 팝업이 발생하도록」.
    /// 1.2.68 전에는 lproj 가 없어 앱이 영어 하나로 잡혔다 — 한국어 아이폰에서도 허락 창 제목 · 버튼 · 사진 고르기 창이 영어였고,
    /// 문구는 영어 · 한국어가 섞여 있었으며 카메라 문구는 Unity 기본값 「For AR」 그대로였다.
    ///
    /// Xcode 프로젝트 폴더에 &lt;언어&gt;.lproj/InfoPlist.strings 를 쓰고 폴더 참조로 앱 타깃에 넣는다(앱 안 맨 위에 ko.lproj … 로 복사된다).
    /// 문구를 바꾸면 6개 언어를 같이 바꾼다. Info.plist 의 기본값(영어 · 개발 지역 en)은 PostProcessBuildUtils.SetupInfoPlist.
    /// 실패해도 빌드는 멈추지 않는다 — 허락 창이 예전처럼 영어로 뜰 뿐.
    /// </summary>
    public static class IOSPermissionLocalization
    {
        private const string Folder = "WoopangLocalization";

        private static readonly string[] Keys =
        {
            "NSLocationWhenInUseUsageDescription",
            "NSLocationAlwaysAndWhenInUseUsageDescription",
            "NSCameraUsageDescription",
            "NSPhotoLibraryUsageDescription",
            "NSPhotoLibraryAddUsageDescription",
            "NSMicrophoneUsageDescription",
        };

        // 언어 → Keys 순서대로. 영어의 위치 · 사진 두 줄은 심사를 거친 기존 문구 그대로
        private static readonly string[][] Texts =
        {
            new[] { "ko",
                "내 주변의 AR 장소와 오브젝트를 보여 주고 거리를 재려면 위치가 필요합니다.",
                "앱을 닫아 두어도 근처 장소 알림을 받으려면 위치가 필요합니다.",
                "AR 화면을 보여 주고 사진을 찍어 올리려면 카메라가 필요합니다.",
                "프로필 · 장소 사진을 사진첩에서 골라 올리려면 필요합니다.",
                "찍은 사진과 대화 속 사진을 사진첩에 저장하려면 필요합니다.",
                "영상을 찍을 때 소리를 함께 담으려면 마이크가 필요합니다." },
            new[] { "en",
                "Location used to show AR objects based on proximity to coordinates.",
                "Location is used in the background to alert you about nearby places even when the app is closed.",
                "Camera used to show the AR view and to take photos for upload.",
                "Photos used for AR object placement and capturing photos for upload.",
                "Saves photos from chats and photos you take to your photo library.",
                "Microphone used to record sound when you take videos." },
            new[] { "ja",
                "周辺のARスポットやオブジェクトを表示し、距離を測るために位置情報を使います。",
                "アプリを閉じていても近くのスポットのお知らせを受け取るために位置情報を使います。",
                "AR画面を表示し、写真を撮ってアップロードするためにカメラを使います。",
                "プロフィールやスポットの写真を写真ライブラリから選んでアップロードするために使います。",
                "撮影した写真やチャットの写真を写真ライブラリに保存するために使います。",
                "動画を撮るときに音声も録音するためにマイクを使います。" },
            new[] { "zh-Hans",
                "需要使用你的位置来显示周围的AR地点和物体，并测量距离。",
                "即使关闭应用，也需要使用位置信息来通知你附近的地点。",
                "需要使用相机来显示AR画面，以及拍摄并上传照片。",
                "需要访问照片，以便从图库中选择头像或地点照片并上传。",
                "需要访问照片，以便将拍摄的照片和聊天中的照片保存到图库。",
                "拍摄视频时需要使用麦克风录制声音。" },
            new[] { "zh-Hant",
                "需要使用你的位置來顯示周圍的AR地點和物件，並測量距離。",
                "即使關閉應用程式，也需要使用位置資訊來通知你附近的地點。",
                "需要使用相機來顯示AR畫面，以及拍攝並上傳照片。",
                "需要取用照片，以便從圖庫選擇大頭貼或地點照片並上傳。",
                "需要取用照片，以便將拍攝的照片和聊天中的照片儲存到圖庫。",
                "拍攝影片時需要使用麥克風錄製聲音。" },
            new[] { "es",
                "Tu ubicación se usa para mostrar los lugares y objetos AR que te rodean y medir distancias.",
                "La ubicación se usa en segundo plano para avisarte de lugares cercanos aunque la app esté cerrada.",
                "La cámara se usa para mostrar la vista AR y para tomar y subir fotos.",
                "El acceso a fotos se usa para elegir fotos de perfil o de lugares y subirlas.",
                "El acceso a fotos se usa para guardar en tu fototeca las fotos que tomas y las de los chats.",
                "El micrófono se usa para grabar el sonido cuando tomas videos." },
        };

        /// <summary>Info.plist 의 영어 기본 카메라 문구 — Unity Player Settings 의 「For AR」 대신 (SetupInfoPlist 가 쓴다)</summary>
        public static string EnglishCamera => Texts[1][3];

        public static void Apply(string buildPath)
        {
            try
            {
                string projectPath = PBXProject.GetPBXProjectPath(buildPath);
                var project = new PBXProject();
                project.ReadFromFile(projectPath);
                string mainGuid = project.GetUnityMainTargetGuid();
                int added = 0;
                foreach (string[] t in Texts)
                {
                    string lproj = t[0] + ".lproj";
                    string rel = Folder + "/" + lproj;
                    string dir = Path.Combine(buildPath, Folder, lproj);
                    Directory.CreateDirectory(dir);
                    var sb = new System.Text.StringBuilder();
                    sb.Append("/* woopang — iPhone permission prompts (").Append(t[0]).Append("). Assets/Scripts/Editor/IOSPermissionLocalization.cs */\n");
                    for (int i = 0; i < Keys.Length; i++)
                        sb.Append('"').Append(Keys[i]).Append("\" = \"").Append(t[i + 1].Replace("\"", "\\\"")).Append("\";\n");
                    File.WriteAllText(Path.Combine(dir, "InfoPlist.strings"), sb.ToString(), new System.Text.UTF8Encoding(false));

                    // 같은 폴더에 다시 빌드(Append)하면 이미 들어 있다 — 두 번 넣으면 Xcode 가 「Multiple commands produce」
                    if (string.IsNullOrEmpty(project.FindFileGuidByProjectPath(lproj)))
                    {
                        project.AddFileToBuild(mainGuid, project.AddFolderReference(rel, lproj));
                        added++;
                    }
                }
                project.WriteToFile(projectPath);
                UnityEngine.Debug.Log($"[WOOPANG] 허락 창 문구 {Texts.Length}개 언어 (lproj 새로 넣음 {added}개)");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("[WOOPANG] 허락 창 문구 언어별 넣기 실패 — 빌드는 계속 (영어로 뜸): " + e.Message);
            }
        }
    }
#endif
}
