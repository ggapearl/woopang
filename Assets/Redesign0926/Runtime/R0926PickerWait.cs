using UnityEngine;

/// <summary>
/// 사진·카메라·파일 고르기 창의 답을 기다리는 표. 답이 끝내 안 오면(창이 못 떴거나 앱 전환으로 답을 잃었다) 칸을 다시 눌러 풀 수 있게 한다.
/// 10-21 대표님 「glb 칸을 눌러도 · 사진 칸을 눌러도 아무것도 안 뜬다」 — '처리 중'이 답 없는 창에 묶여 앱을 다시 켤 때까지 칸이 죽어 있었다.
/// 고르기 창이 떠 있는 동안엔 앱 화면을 누를 수 없으니, 기다리는 중에 칸이 눌렸다면 창은 화면에 없다.
/// </summary>
public sealed class R0926PickerWait
{
    public enum Source { Gallery, Camera, File }

    private const float Settle = 1.5f;        // 막 열었거나 앱으로 막 돌아온 직후엔 답이 오는 중일 수 있다
    private const float PluginGrace = 15f;    // 플러그인이 아직 일한다고 하면(iCloud 사진 받기 등) 연 뒤 이만큼은 더 기다린다
    private static float focusAt;
    private static readonly float[] calledAt = new float[3];

    private int ticket;
    private float since;
    private Source source;

    public bool Waiting { get; private set; }

    public static void NoteFocus(bool focused)
    {
        if (focused) focusAt = Time.realtimeSinceStartup;
    }

    public int Begin(Source s)
    {
        source = s;
        Waiting = true;
        since = calledAt[(int)s] = Time.realtimeSinceStartup;
        return ++ticket;
    }

    // 이 표로 아직 기다리는 중인가 — 답이 왔거나 표를 버렸으면 false
    public bool Holds(int t) => Waiting && t == ticket;

    // 답이 왔다 — 버린 표에 늦게 온 답이면 false
    public bool End(int t)
    {
        if (t != ticket || !Waiting) return false;
        Waiting = false;
        return true;
    }

    // 기다리는 중에 칸이 다시 눌렸다 — 창이 화면에 없다고 볼 수 있으면 이 표를 버리고 true
    public bool TryAbandon()
    {
        if (!Waiting) return true;
        float now = Time.realtimeSinceStartup;
        if (now - since < Settle || now - focusAt < Settle) return false;
        if (PluginBusy(source) && now - since < PluginGrace) return false;
        Waiting = false;
        ticket++;
        ReleasePlugin(source);
        return true;
    }

    // 창을 열기 직전 — 플러그인이 아직 '사용 중'이면 false. 연 지 오래면 앞선 답을 잃은 것이라 풀고 true
    public static bool PluginFree(Source s)
    {
        if (!PluginBusy(s)) return true;
        if (Time.realtimeSinceStartup - calledAt[(int)s] < PluginGrace) return false;
        ReleasePlugin(s);
        return true;
    }

    public static string BusyText => R0926LocalizedText.Say(
        "앞서 연 선택 창을 정리하는 중이에요. 잠시 뒤 다시 눌러 주세요.",
        "Still closing the previous picker. Try again in a moment.",
        "前の選択画面を閉じています。少し待ってからもう一度どうぞ。",
        "正在关闭上一个选择窗口，请稍后再试。",
        "Cerrando el selector anterior. Inténtalo de nuevo en un momento.");

    private static bool PluginBusy(Source s)
    {
        switch (s)
        {
            case Source.Gallery: return NativeGallery.IsMediaPickerBusy();
            case Source.Camera: return NativeCamera.IsCameraBusy();
            default: return NativeFilePicker.IsFilePickerBusy();
        }
    }

    // iOS 플러그인은 답을 못 받으면 '사용 중'에 머물러 다음 창도 안 연다 — 취소 답을 대신 넣어 푼다 (안드로이드·에디터엔 이 오브젝트가 없다)
    private static void ReleasePlugin(Source s)
    {
        switch (s)
        {
            case Source.Gallery:
                Send("NGMediaReceiveCallbackiOS", "OnMediaReceived");
                Send("NGMediaReceiveCallbackiOS", "OnMultipleMediaReceived");
                break;
            case Source.Camera:
                Send("NCCameraCallbackiOS", "OnMediaReceived");
                break;
            default:
                Send("FPResultCallbackiOS", "OnOperationCancelled");
                break;
        }
    }

    private static void Send(string target, string method)
    {
        var go = GameObject.Find(target);
        if (go != null) go.SendMessage(method, "", SendMessageOptions.DontRequireReceiver);
    }
}
