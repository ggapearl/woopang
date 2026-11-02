using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

/// <summary>
/// 앱 안 브라우저 — 웹 로그인 · 계정 관리(비밀번호 변경·계정 삭제)를 앱을 떠나지 않고 연다
/// (10-26 대표님 「앱 내부에서 브라우저 발생하고 여기서 처리할 수 있으면」 · 11-02).
/// 아이폰: ASWebAuthenticationSession (Plugins/iOS/WoopangWeb.mm) — 페이지가 woopang://… 로 가면 시트가 닫히고 그 주소가 OnWebReturn 으로 온다.
/// 안드로이드: 크롬 맞춤 탭 — woopang://… 은 여느 딥링크처럼 오고, 앱(singleTask)이 앞으로 오며 탭이 닫힌다.
/// 앱 안에 웹뷰를 넣지 않는 건 구글 로그인이 웹뷰를 막아서다. 못 띄우면 바깥 브라우저로.
/// </summary>
public class R0926Web : MonoBehaviour
{
    private const string Scheme = "woopang";

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern bool _WoopangWebOpen(string url, string scheme, bool ephemeral);
    private static R0926Web receiver;
#endif

#if UNITY_EDITOR
    /// <summary>플레이 시험이 본다 — EditorQuiet 이면 브라우저를 띄우지 않고 연 주소만 남긴다</summary>
    public static string EditorLast;
    public static bool EditorLastShared;
    public static int EditorCount;
    public static bool EditorQuiet;
#endif

    /// <summary>
    /// shareLogin: 사파리에 남은 카카오·구글 로그인을 이어 쓴다 (아이폰 — 「…로 로그인하려고 합니다」 확인 창이 한 번 뜬다).
    /// 계정 관리처럼 로그인이 필요 없는 화면은 false — 확인 창 없이 뜨고, 닫으면 남는 게 없다.
    /// </summary>
    public static void Open(string url, bool shareLogin)
    {
        if (string.IsNullOrEmpty(url)) return;
#if UNITY_EDITOR
        EditorLast = url;
        EditorLastShared = shareLogin;
        EditorCount++;
        if (!EditorQuiet) Application.OpenURL(url);
#elif UNITY_IOS
        if (receiver == null)
        {
            var go = new GameObject("R0926Web");   // WoopangWeb.mm 이 이 이름으로 결과를 보낸다
            DontDestroyOnLoad(go);
            receiver = go.AddComponent<R0926Web>();
        }
        bool ok = false;
        try { ok = _WoopangWebOpen(url, Scheme, !shareLogin); } catch (Exception) { }
        if (!ok) Application.OpenURL(url);
#elif UNITY_ANDROID
        if (!OpenTab(url)) Application.OpenURL(url);
#else
        Application.OpenURL(url);
#endif
    }

    /// <summary>아이폰 — 시트가 닫혔다. woopang://… 로 끝났으면 그 주소, 사용자가 닫았으면 빈 글</summary>
    [UnityEngine.Scripting.Preserve]
    public void OnWebReturn(string url)
    {
        if (!string.IsNullOrEmpty(url) && LoginManager.Instance != null) LoginManager.Instance.HandleLink(url);
    }

    /// <summary>아이폰 — 시트를 못 띄웠다 → 사파리로</summary>
    [UnityEngine.Scripting.Preserve]
    public void OnWebFailed(string url)
    {
        if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // androidx.browser 없이 맞춤 탭 — CustomTabsIntent 가 하는 일과 같다: 세션 자리(빈 바인더)가 있으면 크롬이 맞춤 탭으로 연다.
    // 맞춤 탭을 모르는 브라우저는 그냥 연다. 빈 바인더는 AndroidJavaObject 로는 못 넘겨(null 의 형을 몰라) JNI 로 직접 넣는다.
    private static bool OpenTab(string url)
    {
        try
        {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var uriClass = new AndroidJavaClass("android.net.Uri"))
            using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", url))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", uri))
            using (var extras = new AndroidJavaObject("android.os.Bundle"))
            {
                IntPtr key = AndroidJNI.NewStringUTF("android.support.customtabs.extra.SESSION");
                IntPtr putBinder = AndroidJNI.GetMethodID(extras.GetRawClass(), "putBinder", "(Ljava/lang/String;Landroid/os/IBinder;)V");
                AndroidJNI.CallVoidMethod(extras.GetRawObject(), putBinder, new[] { new jvalue { l = key }, new jvalue { l = IntPtr.Zero } });
                AndroidJNI.DeleteLocalRef(key);
                intent.Call<AndroidJavaObject>("putExtras", extras).Dispose();
                intent.Call<AndroidJavaObject>("putExtra", "android.support.customtabs.extra.TOOLBAR_COLOR", unchecked((int)0xFF0E1013)).Dispose();   // 계정 관리 화면 바탕색
                intent.Call<AndroidJavaObject>("putExtra", "android.support.customtabs.extra.TITLE_VISIBILITY", 1).Dispose();
                activity.Call("startActivity", intent);
            }
            return true;
        }
        catch (Exception) { return false; }
    }
#endif
}
