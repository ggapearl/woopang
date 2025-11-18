using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class UIFeedbackManager : MonoBehaviour
{
    public static UIFeedbackManager Instance { get; private set; }

    [Header("Audio Settings")]
    [SerializeField] private AudioClip defaultButtonSound;
    [SerializeField] private float soundVolume = 1.0f;

    [Header("Haptic Settings")]
    [SerializeField, Range(0f, 1f)] private float hapticIntensity = 0.5f;
    [SerializeField] private bool enableHaptics = true;

    private AudioSource audioSource;

    // 햅틱 강도 열거형
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
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = soundVolume;
        Debug.Log("UIFeedbackManager Initialized");
    }

    // 기본 터치 피드백 (기존 강도 사용)
    public void HandleTouchFeedback(AudioClip customSound = null)
    {
        if (IsValidUIEvent())
        {
            Debug.Log($"Button Clicked: {EventSystem.current.currentSelectedGameObject.name}, Custom Sound: {(customSound != null ? customSound.name : "None")}");
            TriggerHaptic(hapticIntensity);
            PlaySound(customSound ?? defaultButtonSound);
        }
        else
        {
            Debug.Log("Invalid UI Event Ignored");
        }
    }

    // 특정 강도로 터치 피드백
    public void HandleTouchFeedback(float intensity, AudioClip customSound = null)
    {
        if (IsValidUIEvent())
        {
            Debug.Log($"Button Clicked: {EventSystem.current.currentSelectedGameObject.name}, Intensity: {intensity}, Custom Sound: {(customSound != null ? customSound.name : "None")}");
            TriggerHaptic(intensity);
            PlaySound(customSound ?? defaultButtonSound);
        }
        else
        {
            Debug.Log("Invalid UI Event Ignored");
        }
    }

    // 열거형을 이용한 터치 피드백
    public void HandleTouchFeedback(HapticIntensity intensityType, AudioClip customSound = null)
    {
        float intensity = GetIntensityFromType(intensityType);
        HandleTouchFeedback(intensity, customSound);
    }

    public void HandleKeyInput(string newText)
    {
        if (!string.IsNullOrEmpty(newText))
        {
            Debug.Log("Key Input Detected");
            TriggerHaptic(hapticIntensity * 0.7f); // 키 입력은 조금 더 약하게
            PlaySound(defaultButtonSound);
        }
    }

    // 햅틱 강도를 동적으로 설정
    public void SetHapticIntensity(float intensity)
    {
        hapticIntensity = Mathf.Clamp01(intensity);
        Debug.Log($"Haptic intensity set to: {hapticIntensity}");
    }

    // 햅틱 활성화/비활성화
    public void SetHapticsEnabled(bool enabled)
    {
        enableHaptics = enabled;
        Debug.Log($"Haptics {(enabled ? "enabled" : "disabled")}");
    }

    private void TriggerHaptic(float intensity = 0.5f)
    {
        if (!enableHaptics) return;

        intensity = Mathf.Clamp01(intensity);
        Debug.Log($"Haptic Triggered with intensity: {intensity}");

#if UNITY_IOS
        try
        {
            // iOS용 Medium Haptic만 사용 (강도 조절 없이)
            if (intensity > 0.1f) // 0.1 이상이면 Medium Haptic
            {
                // iOS Medium Haptic 구현 (네이티브 방식)
                TriggerIOSMediumHaptic();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"iOS Medium Haptic Failed: {e.Message}");
            // Fallback은 하지 않음 (Medium Haptic이 목적이므로)
        }
#elif UNITY_ANDROID
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (vibrator != null)
            {
                // Android API 26+ (VibrationEffect)
                if (AndroidVersion() >= 26)
                {
                    AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    
                    // 강도에 따른 진동 패턴 선택
                    int effectType;
                    if (intensity <= 0.33f)
                    {
                        effectType = vibrationEffectClass.GetStatic<int>("EFFECT_TICK");
                    }
                    else if (intensity <= 0.66f)
                    {
                        effectType = vibrationEffectClass.GetStatic<int>("EFFECT_CLICK");
                    }
                    else
                    {
                        effectType = vibrationEffectClass.GetStatic<int>("EFFECT_HEAVY_CLICK");
                    }

                    AndroidJavaObject vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createPredefined", effectType);
                    vibrator.Call("vibrate", vibrationEffect);
                }
                else
                {
                    // Android API 25 이하 - 시간 기반 진동
                    long duration = (long)(50 + (intensity * 100)); // 50-150ms
                    vibrator.Call("vibrate", duration);
                }
            }
            else
            {
                Handheld.Vibrate();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Android Vibrate Failed: {e.Message}, Falling back to Handheld.Vibrate");
            Handheld.Vibrate();
        }
#else
        // 기타 플랫폼은 기본 진동
        if (intensity > 0.1f)
        {
            Handheld.Vibrate();
        }
#endif
    }

    // iOS Medium Haptic 구현
    private void TriggerIOSMediumHaptic()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // 네이티브 iOS Medium Haptic 호출
        _TriggerMediumHaptic();
#else
        // 에디터나 다른 환경에서는 기본 진동
        Handheld.Vibrate();
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    // iOS 네이티브 Medium Haptic 함수 선언
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _TriggerMediumHaptic();
#endif

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip, soundVolume);
            Debug.Log($"Playing Sound: {clip.name}");
        }
        else
        {
            Debug.LogWarning("No sound clip assigned!");
        }
    }

    private bool IsValidUIEvent()
    {
        bool isValid = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null;
        return isValid;
    }

    private float GetIntensityFromType(HapticIntensity intensityType)
    {
        switch (intensityType)
        {
            case HapticIntensity.Light:
                return 0.25f;
            case HapticIntensity.Medium:
                return 0.5f;
            case HapticIntensity.Heavy:
                return 1.0f;
            default:
                return 0.5f;
        }
    }

    private int AndroidVersion()
    {
        try
        {
            AndroidJavaClass buildVersion = new AndroidJavaClass("android.os.Build$VERSION");
            return buildVersion.GetStatic<int>("SDK_INT");
        }
        catch
        {
            return 1; // 기본값
        }
    }

    // 공개 메서드들 - 외부에서 호출 가능
    public void TriggerLightHaptic()
    {
        TriggerHaptic(0.25f);
    }

    public void TriggerMediumHaptic()
    {
        TriggerHaptic(0.5f);
    }

    public void TriggerHeavyHaptic()
    {
        TriggerHaptic(1.0f);
    }

    // 현재 설정 확인용
    public float GetCurrentHapticIntensity()
    {
        return hapticIntensity;
    }

    public bool IsHapticsEnabled()
    {
        return enableHaptics;
    }
}