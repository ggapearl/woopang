using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
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

    // 안드로이드는 오디오 버퍼가 기본값(가장 큼)이라 버튼을 누른 뒤 소리가 한 박자 늦게, 뭉개져 들렸다 — 아이폰처럼 누르는 순간 나게
    // 가장 짧은 버퍼로 (10-20 대표님 「안드로이드 효과음이 아이폰과 다르다」). 소리를 내기 전(장면을 읽기 전)에 한 번만 — Reset 은 나던 소리를 끊는다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LowLatencyAudio()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var cfg = AudioSettings.GetConfiguration();
        if (cfg.dspBufferSize <= 256) return;
        cfg.dspBufferSize = 256;
        if (!AudioSettings.Reset(cfg)) Debug.LogWarning("[UIFeedback] 오디오 버퍼를 줄이지 못함");
#endif
    }

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
    [Tooltip("진동 강도 (0~1). iOS: Medium/Heavy, Android: TICK/CLICK/HEAVY_CLICK")]
    [SerializeField, Range(0f, 1f)] private float hapticIntensity = 0.5f;

    private AudioSource audioSource;
    private AudioClip cachedButtonSound;

    // 누를 수 있는 것은 모두 설정 탭처럼 소리·진동 (10-30 대표님) — 줄·핀·말풍선처럼 실행 중에 생기는 버튼도 있어, 켜진 버튼을 훑어 붙인다
    private const float ScanEvery = 0.25f;
    // 버튼이 아니라 시트 바깥 어두운 곳 · 입력 막이
    private static readonly HashSet<string> Backdrops = new HashSet<string> { "Dim", "Dim0926", "Shade0926", "BackClose0926", "CommentPanel_HQ", "UploadPage", "DialogPanel" };
    private Selectable[] selectables = new Selectable[256];
    private int lastSelectableCount = -1;
    private float nextScan;
    private int lastFeedbackFrame = -1;

    public bool FedBackThisFrame => lastFeedbackFrame == Time.frameCount;
    public static bool IsBackdrop(string objectName) => Backdrops.Contains(objectName);
#if UNITY_EDITOR
    public static int EditorFeedbacks;
#endif

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

    void Update()
    {
        int count = Selectable.allSelectableCount;
        if (count == lastSelectableCount && Time.unscaledTime < nextScan) return;   // 창이 열려 개수가 바뀌면 바로, 같은 개수로 바뀐 화면은 곧
        lastSelectableCount = count;
        nextScan = Time.unscaledTime + ScanEvery;

        if (selectables.Length < count) selectables = new Selectable[count * 2];
        int n = Selectable.AllSelectablesNoAlloc(selectables);
        for (int i = 0; i < n; i++)
        {
            var s = selectables[i];
            if (!(s is Button || s is Toggle) || s.TryGetComponent<UITouchForwarder>(out _) || Backdrops.Contains(s.name)) continue;
            s.gameObject.AddComponent<UITouchForwarder>();
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

    public void HandleTouchFeedbackDirect(AudioClip customSound = null) => HandleTouchFeedbackDirect(customSound, 1f);

    // volumeScale — 그 버튼만 소리를 줄일 때 (메세지 보내기 0.8)
    public void HandleTouchFeedbackDirect(AudioClip customSound, float volumeScale)
    {
        AudioClip clipToPlay = customSound ?? cachedButtonSound;

        if (clipToPlay == null || !clipToPlay)
        {
            cachedButtonSound = Resources.Load<AudioClip>("Audio/Touch");
            clipToPlay = cachedButtonSound;
        }

        ExecuteFeedback(hapticIntensity, clipToPlay, volumeScale);
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
    public float GetCurrentHapticIntensity() => hapticIntensity;

    // ============================================================
    // 핵심 로직
    // ============================================================

    private void ExecuteFeedback(float intensity, AudioClip clip, float volumeScale = 1f)
    {
        lastFeedbackFrame = Time.frameCount;
#if UNITY_EDITOR
        EditorFeedbacks++;
#endif
        PlaySound(clip, volumeScale);
        TriggerHaptic(intensity);
    }

    // ============================================================
    // 진동 (iOS: Taptic Engine, Android: VibrationEffect)
    // ============================================================

    private void TriggerHaptic(float intensity = 0.5f)
    {
        intensity = Mathf.Clamp01(intensity);

#if UNITY_IOS
        try
        {
            // iOS Taptic Engine
            // 0~0.3   → Light(0)
            // 0.31~0.6 → Medium(1)
            // 0.61~1.0 → Heavy(2)
            int style;
            if (intensity <= 0.3f)
                style = 0; // Light
            else if (intensity <= 0.6f)
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

    private void PlaySound(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || audioSource == null) return;
        float volume = soundVolume * volumeScale;
        if (AudioListener.volume <= 0.01f || volume <= 0.01f) return;

        audioSource.PlayOneShot(clip, volume);
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
