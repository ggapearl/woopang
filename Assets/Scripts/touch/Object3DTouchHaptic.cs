using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class Object3DTouchHaptic : MonoBehaviour
{
    [Header("Haptic Settings")]
    [SerializeField, Range(0f, 1f)] private float hapticIntensity = 0.7f;

    [SerializeField] private AudioClip touchSound;
    [SerializeField] private float soundVolume = 1.0f;

    private AudioSource audioSource;
    private bool isProcessingTouch = false;
    private Collider objectCollider;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        objectCollider = GetComponent<Collider>();

        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.volume = soundVolume;
    }

    void Update()
    {
        // 전체화면 UI가 열려있으면 AR 터치 차단
        if (TouchManager.IsFullscreenUIOpen()) return;

        bool inputDetected = false;
        Vector2 inputPosition = Vector2.zero;

        // Touch Input
        if (Touch.activeTouches.Count > 0 && Touch.activeTouches[0].phase == TouchPhase.Began && !isProcessingTouch)
        {
            inputDetected = true;
            inputPosition = Touch.activeTouches[0].screenPosition;
            
            // Check UI
            if (IsOverUIOrIndicator(inputPosition))
            {
                return;
            }
        }
        // Mouse Input
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !isProcessingTouch)
        {
            inputDetected = true;
            inputPosition = Mouse.current.position.ReadValue();
            
            // Check UI
            if (IsOverUIOrIndicator(inputPosition))
            {
                return;
            }
        }
        
        // Processing
        if (inputDetected)
        {
            isProcessingTouch = true;
            Ray ray = Camera.main.ScreenPointToRay(inputPosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit) && hit.collider == objectCollider)
            {
                TriggerFeedback();
            }
        }
        
        // Reset Processing
        bool isTouching = Touch.activeTouches.Count > 0;
        bool isClicking = Mouse.current != null && Mouse.current.leftButton.isPressed;
        
        if (!isTouching && !isClicking)
        {
            isProcessingTouch = false;
        }
    }

    private bool IsOverUIOrIndicator(Vector2 screenPosition)
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
            return false;

        UnityEngine.EventSystems.PointerEventData pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        pointerData.position = screenPosition;

        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            GameObject hitObject = result.gameObject;
            
            if (IsIndicatorRelated(hitObject))
            {
                // Debug.Log($"Indicator related object ignored: {hitObject.name}");
                continue;
            }

            if (hitObject.layer == 5)
            {
                // Debug.Log($"Real UI detected: {hitObject.name} - blocking touch");
                return true;
            }
        }

        return false;
    }

    private bool IsIndicatorRelated(GameObject obj)
    {
        Transform current = obj.transform;
        while (current != null)
        {
            string name = current.name;
            
            // Log for debugging
            // Debug.Log($"Checking object: {name}, Parent: {(current.parent ? current.parent.name : "null")}");
            
            // Check for indicator related objects
            if ((name.Contains("Indicator") && !name.Contains("Button")) || 
                (name.Contains("Arrow") && !name.Contains("Button")) || 
                (name.Contains("Box") && !name.Contains("Button")) ||
                name.Contains("OffScreen") ||
                (name == "Text" && (current.parent?.name.Contains("Indicator") == true)) ||
                current.GetComponent<Indicator>() != null)
            {
                // Debug.Log($"Found indicator related object: {name}");
                return true;
            }
            
            current = current.parent;
        }
        
        // Debug.Log($"Object {obj.name} is NOT indicator related");
        return false;
    }

    void OnMouseDown()
    {
        // Update handles logic
        return;
    }

    private void TriggerFeedback()
    {
        PlaySound();
        TriggerHaptic();
    }

    private void PlaySound()
    {
        if (touchSound != null)
        {
            audioSource.PlayOneShot(touchSound, soundVolume);
        }
    }

    private void TriggerHaptic()
    {
#if UNITY_IOS
        // Unity 6+ uses Handheld.Vibrate() for iOS haptic feedback
        Handheld.Vibrate();
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
                    
                    if (hapticIntensity <= 0.33f)
                    {
                        effectType = vibrationEffectClass.GetStatic<int>("EFFECT_TICK");
                    }
                    else if (hapticIntensity <= 0.66f)
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
                    long duration = (long)(30 + (hapticIntensity * 70));
                    vibrator.Call("vibrate", duration);
                }
            }
            else
            {
                Handheld.Vibrate();
            }
        }
        catch (System.Exception)
        {
            Handheld.Vibrate();
        }
#else
        if (hapticIntensity > 0.1f)
        {
            Handheld.Vibrate();
        }
#endif
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