using UnityEngine;

/// <summary>
/// 시작화면(R0926Splash)을 앱이 새로 켜질 때 한 번만 켠다.
/// 시작화면은 씬에 꺼진 채 저장돼 있어 편집할 때 화면을 가리지 않는다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class R0926SplashBoot : MonoBehaviour
{
    [SerializeField] private GameObject splash;
    private static bool shown;

    private void Awake()
    {
        if (shown || splash == null) return;
        shown = true;
        splash.SetActive(true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch() => shown = false;
}
