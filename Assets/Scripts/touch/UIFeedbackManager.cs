using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Runtime.InteropServices;

public class UIFeedbackManager : MonoBehaviour
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _WoopangTriggerHaptic(int style);

    [DllImport("__Internal")]
    private static extern void _WoopangTriggerSelectionHaptic();
#endif

    public static UIFeedbackManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("UIFeedbackManager");
            go.AddComponent<UIFeedbackManager>();
        }
    }

    [Header("Audio Settings")]
    [SerializeField] private AudioClip defaultButtonSound;
    [SerializeField] private float soundVolume = 1.0f;

    [Header("Haptic Settings")]
    [Tooltip("진동 강도 (0~1). iOS: Light/Medium/Heavy, Android: TICK/CLICK/HEAVY_CLICK")]
    [SerializeField, Range(0f, 1f)] private float hapticIntensity = 0.5f;
    [SerializeField] private bool enableHaptics = true;

    [Header("Haptic Mode")]
    [Tooltip("true: 무음(매너모드)일 때만 진동 / false: 항상 진동")]
    [SerializeField] private bool hapticOnlyWhenSilent = true;

    private AudioSource audioSource;
    private AudioClip cachedButtonSound;

    public enum HapticIntensity
    {
        Light = 0,
        Medium = 1,
        Heavy = 2
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = soundVolume;

            if (defaultButtonSound != null)
            {
                cachedButtonSound = defaultButtonSound;
            }
            else
            {
                cachedButtonSound = Resources.Load<AudioClip>("Audio/Touch");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ============================================================
    // 공개 API
    // ============================================================

    public void HandleTouchFeedback(AudioClip customSound = null)
    {
        if (IsValidUIEvent())
        {
            AudioClip fallbackSound = defaultButtonSound ?? cachedButtonSound;
            ExecuteFeedback(hapticIntensity, customSound ?? fallbackSound);
        }
    }

    public void HandleTouchFeedbackDirect(AudioClip customSound = null)
    {
        AudioClip clipToPlay = customSound ?? cachedButtonSound;

        if (clipToPlay == null || !clipToPlay)
        {
            cachedButtonSound = Resources.Load<AudioClip>("Audio/Touch");
            clipToPlay = cachedButtonSound;
        }

        ExecuteFeedback(hapticIntensity, clipToPlay);
    }

    /// <summary>
    /// IsValidUIEvent 체크 없이 직접 실행 (3D 오브젝트 터치 등 non-UI 컨텍스트용)
    /// </summary>
    public void HandleTouchFeedbackDirect(float intensity, AudioClip customSound = null)
    {
        AudioClip clipToPlay = customSound ?? cachedButtonSound;

        if (clipToPlay == null || !clipToPlay)
        {
            cachedButtonSound = Resources.Load<AudioClip>("Audio/Touch");
            clipToPlay = cachedButtonSound;
        }

        ExecuteFeedback(intensity, clipToPlay);
    }

    public void HandleTouchFeedback(float intensity, AudioClip customSound = null)
    {
        if (IsValidUIEvent())
        {
            AudioClip fallbackSound = defaultButtonSound ?? cachedButtonSound;
            ExecuteFeedback(intensity, customSound ?? fallbackSound);
        }
    }

    public void HandleTouchFeedback(HapticIntensity intensityType, AudioClip customSound = null)
    {
        float intensity = GetIntensityFromType(intensityType);
        HandleTouchFeedback(intensity, customSound);
    }

    public void HandleKeyInput(string newText)
    {
        if (!string.IsNullOrEmpty(newText))
        {
            AudioClip fallbackSound = defaultButtonSound ?? cachedButtonSound;
            ExecuteFeedback(hapticIntensity * 0.7f, fallbackSound);
        }
    }

    public void TriggerLightHaptic() => TriggerHaptic(0.25f);
    public void TriggerMediumHaptic() => TriggerHaptic(0.5f);
    public void TriggerHeavyHaptic() => TriggerHaptic(1.0f);

    public void SetHapticIntensity(float intensity) => hapticIntensity = Mathf.Clamp01(intensity);
    public void SetHapticsEnabled(bool enabled) => enableHaptics = enabled;
    public float GetCurrentHapticIntensity() => hapticIntensity;
    public bool IsHapticsEnabled() => enableHaptics;

    // ============================================================
    // 핵심 로직: 소리가 나면 진동 생략, 무음이면 진동 발생
    // ============================================================

    private void ExecuteFeedback(float intensity, AudioClip clip)
    {
        bool soundPlayed = PlaySound(clip);

        if (hapticOnlyWhenSilent)
        {
            // 소리가 실제로 재생되지 않았을 때만 진동
            if (!soundPlayed)
            {
                TriggerHaptic(intensity);
            }
        }
        else
        {
            // 항상 진동
            TriggerHaptic(intensity);
        }
    }

    // ============================================================
    // 진동 (iOS: Taptic Engine, Android: VibrationEffect)
    // ============================================================

    private void TriggerHaptic(float intensity = 0.5f)
    {
        if (!enableHaptics) return;

        intensity = Mathf.Clamp01(intensity);

#if UNITY_IOS
        try
        {
            // iOS Taptic Engine — intensity에 따라 Light(0)/Medium(1)/Heavy(2)
            int style;
            if (intensity <= 0.33f)
                style = 0; // Light
            else if (intensity <= 0.66f)
                style = 1; // Medium
            else
                style = 2; // Heavy

#if !UNITY_EDITOR
            _WoopangTriggerHaptic(style);
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"iOS Haptic Failed: {e.Message}");
        }
#elif UNITY_ANDROID
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (vibrator != null)
            {
                if (AndroidVersion() >= 26)
                {
                    AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");

                    int effectType;
                    if (intensity <= 0.33f)
                        effectType = vibrationEffectClass.GetStatic<int>("EFFECT_TICK");
                    else if (intensity <= 0.66f)
                        effectType = vibrationEffectClass.GetStatic<int>("EFFECT_CLICK");
                    else
                        effectType = vibrationEffectClass.GetStatic<int>("EFFECT_HEAVY_CLICK");

                    AndroidJavaObject vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createPredefined", effectType);
                    vibrator.Call("vibrate", vibrationEffect);
                }
                else
                {
                    long duration = (long)(50 + (intensity * 100)); // 50-150ms
                    vibrator.Call("vibrate", duration);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Android Vibrate Failed: {e.Message}");
        }
#else
#if !UNITY_EDITOR
        if (intensity > 0.1f)
        {
            Handheld.Vibrate();
        }
#endif
#endif
    }

    // ============================================================
    // 사운드 재생 — 실제로 소리가 들리는지 여부 반환
    // ============================================================

    private bool PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return false;

        // Unity 앱 내부 볼륨 체크
        if (AudioListener.volume <= 0.01f || soundVolume <= 0.01f) return false;

        // 시스템 볼륨 체크 — 기기 볼륨이 0이면 소리가 안 들림
        if (IsSystemVolumeMuted())
        {
            // 소리는 재생하되 (볼륨 올리면 들릴 수 있도록) "안 들린다"고 반환
            audioSource.PlayOneShot(clip, soundVolume);
            return false;
        }

        audioSource.PlayOneShot(clip, soundVolume);
        return true;
    }

    /// <summary>
    /// 시스템 미디어 볼륨이 0인지 체크 (Android: AudioManager, iOS: outputVolume)
    /// </summary>
    private bool IsSystemVolumeMuted()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject audioManager = activity.Call<AndroidJavaObject>("getSystemService", "audio");
            if (audioManager != null)
            {
                // STREAM_MUSIC = 3
                int musicVolume = audioManager.Call<int>("getStreamVolume", 3);
                // 벨소리 모드: 0=Normal, 1=Silent, 2=Vibrate
                int ringerMode = audioManager.Call<int>("getRingerMode");
                // 미디어 볼륨 0이거나 진동/무음 모드이면 소리가 안 들림
                return musicVolume == 0 || ringerMode != 0;
            }
        }
        catch (System.Exception) { }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS: 시스템 볼륨은 정확히 체크할 수 없으나
        // 사일런트 스위치 ON 시 Unity 사운드도 무음이 되므로
        // 보수적으로 항상 소리가 난다고 판단 (Taptic Engine은 사일런트에서도 동작)
        return false;
#endif
        return false;
    }

    // ============================================================
    // 유틸리티
    // ============================================================

    private bool IsValidUIEvent()
    {
        return EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null;
    }

    private float GetIntensityFromType(HapticIntensity intensityType)
    {
        switch (intensityType)
        {
            case HapticIntensity.Light: return 0.25f;
            case HapticIntensity.Medium: return 0.5f;
            case HapticIntensity.Heavy: return 1.0f;
            default: return 0.5f;
        }
    }

    private int AndroidVersion()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass buildVersion = new AndroidJavaClass("android.os.Build$VERSION");
            return buildVersion.GetStatic<int>("SDK_INT");
        }
        catch
        {
            return 1;
        }
#else
        return 0;
#endif
    }
}
