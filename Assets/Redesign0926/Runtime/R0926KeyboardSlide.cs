using UnityEngine;

/// <summary>
/// 안드로이드 키보드를 아이폰처럼 미끄러지듯 내린다 (2026-11-08 대표님 「iOS환경에서는 괜찮은데 안드로이드 환경의 경우 키패드가 사라질 때
/// 슬라이드로 사라지지 않고 컷아웃으로 사라지거든?」).
/// 실제 일은 자바 com.que.woopang.KeyboardSlide 가 한다 — 유니티가 키보드를 닫을 때 키보드를 먼저 내리고, 다 내려간 뒤 키보드 창을 닫는다
/// (유니티는 키보드 창을 바로 닫아 키보드가 잘려 사라졌다). 키보드는 뜰 때마다 새로 만들어지므로, 떠 있는 동안 그 키보드를 감싸 두기만 한다.
/// 뒤로 · 완료 · ✓ · 바깥 누르기 · 창 닫기 · 쓸어내리기 모두 같은 길로 닫혀 한 곳에서 된다. 안드로이드 11+ 기기에서만 — 장면에 둘 것 없음.
/// </summary>
public sealed class R0926KeyboardSlide : MonoBehaviour
{
#if UNITY_ANDROID
    private AndroidJavaClass slide;
    private AndroidJavaObject activity;
    private float next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor) return;
        var go = new GameObject(nameof(R0926KeyboardSlide));
        DontDestroyOnLoad(go);
        go.AddComponent<R0926KeyboardSlide>();
    }

    private void Update()
    {
        if (!TouchScreenKeyboard.visible || Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.1f;   // 뜨자마자 감싸 두면 된다 — 닫을 때 쓰인다
        try
        {
            if (slide == null)
            {
                slide = new AndroidJavaClass("com.que.woopang.KeyboardSlide");
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    activity = up.GetStatic<AndroidJavaObject>("currentActivity");
            }
            slide.CallStatic("hook", activity);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[KeyboardSlide] " + e.Message);
            enabled = false;   // 예전처럼 닫힌다
        }
    }

    private void OnDestroy()
    {
        slide?.Dispose();
        activity?.Dispose();
    }
#endif
}
