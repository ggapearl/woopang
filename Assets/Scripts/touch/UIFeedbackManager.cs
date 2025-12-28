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
    private AudioClip cachedButtonSound; // 레퍼런스 손실 방지용 캐시

    // ��ƽ ���� ������
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

            // AudioSource 초기화
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = soundVolume;

            // AudioClip을 Resources에서 로드하여 씬과 독립적으로 관리
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

    // �⺻ ��ġ �ǵ�� (���� ���� ���)
    public void HandleTouchFeedback(AudioClip customSound = null)
    {
        if (IsValidUIEvent())
        {
            AudioClip fallbackSound = defaultButtonSound ?? cachedButtonSound;
            TriggerHaptic(hapticIntensity);
            PlaySound(customSound ?? fallbackSound);
        }
    }

    // UITouchForwarder에서 직접 호출 (IsValidUIEvent 체크 생략)
    public void HandleTouchFeedbackDirect(AudioClip customSound = null)
    {
        AudioClip clipToPlay = customSound ?? cachedButtonSound;

        // 만약 cachedButtonSound도 손실되었다면 재로드
        if (clipToPlay == null || !clipToPlay)
        {
            cachedButtonSound = Resources.Load<AudioClip>("Audio/Touch");
            clipToPlay = cachedButtonSound;
        }

        TriggerHaptic(hapticIntensity);
        PlaySound(clipToPlay);
    }

    // Ư�� ������ ��ġ �ǵ��
    public void HandleTouchFeedback(float intensity, AudioClip customSound = null)
    {
        if (IsValidUIEvent())
        {
            AudioClip fallbackSound = defaultButtonSound ?? cachedButtonSound;
            TriggerHaptic(intensity);
            PlaySound(customSound ?? fallbackSound);
        }
    }

    // �������� �̿��� ��ġ �ǵ��
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
            TriggerHaptic(hapticIntensity * 0.7f);
            PlaySound(fallbackSound);
        }
    }

    public void SetHapticIntensity(float intensity)
    {
        hapticIntensity = Mathf.Clamp01(intensity);
    }

    public void SetHapticsEnabled(bool enabled)
    {
        enableHaptics = enabled;
    }

    private void TriggerHaptic(float intensity = 0.5f)
    {
        if (!enableHaptics) return;

        intensity = Mathf.Clamp01(intensity);

#if UNITY_IOS
        try
        {
            // iOS�� Medium Haptic�� ��� (���� ���� ����)
            if (intensity > 0.1f) // 0.1 �̻��̸� Medium Haptic
            {
                // iOS Medium Haptic ���� (����Ƽ�� ���)
                TriggerIOSMediumHaptic();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"iOS Medium Haptic Failed: {e.Message}");
            // Fallback�� ���� ���� (Medium Haptic�� �����̹Ƿ�)
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
                    
                    // ������ ���� ���� ���� ����
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
                    // Android API 25 ���� - �ð� ��� ����
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
        // ��Ÿ �÷����� �⺻ ����
        if (intensity > 0.1f)
        {
            Handheld.Vibrate();
        }
#endif
    }

    // iOS Medium Haptic ����
    private void TriggerIOSMediumHaptic()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // ����Ƽ�� iOS Medium Haptic ȣ��
        _TriggerMediumHaptic();
#else
        // �����ͳ� �ٸ� ȯ�濡���� �⺻ ����
        Handheld.Vibrate();
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    // iOS ����Ƽ�� Medium Haptic �Լ� ����
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _TriggerMediumHaptic();
#endif

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, soundVolume);
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
            return 1; // �⺻��
        }
    }

    // ���� �޼���� - �ܺο��� ȣ�� ����
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

    // ���� ���� Ȯ�ο�
    public float GetCurrentHapticIntensity()
    {
        return hapticIntensity;
    }

    public bool IsHapticsEnabled()
    {
        return enableHaptics;
    }
}