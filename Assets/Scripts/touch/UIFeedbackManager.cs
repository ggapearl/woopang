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
    [Tooltip("true: 무음(매너모드)일 때만 진동 / false: 항상 진동 (권장)")]
    [SerializeField] private bool hapticOnlyWhenSilent = false;

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
    // 핵심 로직
    // ============================================================

    private void ExecuteFeedback(float intensity, AudioClip clip)
    {
        PlaySound(clip);

        if (hapticOnlyWhenSilent)
        {
            // 무음 모드일 때만 진동 (Android만 정확히 감지 가능)
            if (IsSystemVolumeMuted())
            {
                TriggerHaptic(intensity);
            }
        }
        else
        {
            // 항상 진동 (권장 — 소리/진동은 시스템이 관리)
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
            // iOS Taptic Engine
            // 0~0.5  → Medium(1)
            // 0.5~1.0 → Heavy(2)
            int style;
            if (intensity <= 0.5f)
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
                    long duration = (long)(50 + (intensity * 100));
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
    // 사운드 재생
    // ============================================================

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        if (AudioListener.volume <= 0.01f || soundVolume <= 0.01f) return;

        audioSource.PlayOneShot(clip, soundVolume);
    }

    /// <summary>
    /// 시스템이 무음/진동 모드인지 체크 (Android만 지원)
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
                int musicVolume = audioManager.Call<int>("getStreamVolume", 3);
                int ringerMode = audioManager.Call<int>("getRingerMode");
                return musicVolume == 0 || ringerMode != 0;
            }
        }
        catch (System.Exception) { }
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
