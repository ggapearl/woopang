using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

/// <summary>
/// 시스템 공유 창으로 글(링크)을 보낸다 — 카톡·문자·SNS 등 고르는 창 (10-26 보물찾기 '공유하기').
/// 안드로이드: ACTION_SEND 고르기 창 · 아이폰: UIActivityViewController (Plugins/iOS/WoopangShare.mm).
/// 창을 못 띄우면 클립보드에 복사하고 false — 부른 쪽이 '링크를 복사했어요'라고 알린다.
/// </summary>
public static class R0926Share
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _WoopangShareText(string text);
#endif

#if UNITY_EDITOR
    /// <summary>플레이 시험이 본다 — 에디터는 창을 띄우지 않고(클립보드도 건드리지 않는다) 보낸 글만 남긴다</summary>
    public static string EditorLast;
    public static int EditorCount;
#endif

    public static bool Text(string text, string chooserTitle)
    {
        if (string.IsNullOrEmpty(text)) return false;
#if UNITY_EDITOR
        EditorLast = text;
        EditorCount++;
        return true;
#elif UNITY_ANDROID
        try
        {
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND")).Dispose();
                intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, chooserTitle ?? ""))
                    activity.Call("startActivity", chooser);
            }
            return true;
        }
        catch (Exception) { GUIUtility.systemCopyBuffer = text; return false; }
#elif UNITY_IOS
        try { _WoopangShareText(text); return true; }
        catch (Exception) { GUIUtility.systemCopyBuffer = text; return false; }
#else
        GUIUtility.systemCopyBuffer = text;
        return false;
#endif
    }
}
