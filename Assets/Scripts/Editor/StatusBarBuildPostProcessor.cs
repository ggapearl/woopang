#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class StatusBarBuildPostProcessor
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(path, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // UIViewControllerBasedStatusBarAppearance를 false로 설정
        // → UIApplication.setStatusBarStyle: API가 작동하도록 함
        plist.root.SetBoolean("UIViewControllerBasedStatusBarAppearance", false);

        plist.WriteToFile(plistPath);
    }
}
#endif
