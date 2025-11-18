using UnityEngine;
using System.Collections;

public class Object3DTouchHaptic : MonoBehaviour
{
    [Header("Haptic Settings")]
    [SerializeField, Range(0f, 1f)] private float hapticIntensity = 0.7f;

    [SerializeField] private AudioClip touchSound;
    [SerializeField] private float soundVolume = 1.0f;

    private AudioSource audioSource;
    private bool isProcessingTouch = false;
    private Collider objectCollider;

    void Start()
    {
        objectCollider = GetComponent<Collider>();
        if (objectCollider == null)
        {
            Debug.LogWarning($"Object3DTouchHaptic on {gameObject.name} requires a Collider component!");
        }

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
        bool inputDetected = false;
        Vector3 inputPosition = Vector3.zero;
        
        // 터치 입력
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began && !isProcessingTouch)
        {
            inputDetected = true;
            inputPosition = Input.GetTouch(0).position;
            
            // UI 터치 확인 (인디케이터 포함)
            if (IsOverUIOrIndicator())
            {
                return;
            }
        }
        // 마우스 입력 (에디터용)
        else if (Input.GetMouseButtonDown(0) && !isProcessingTouch)
        {
            inputDetected = true;
            inputPosition = Input.mousePosition;
            
            // UI 마우스 클릭 확인 (인디케이터 포함)
            if (IsOverUIOrIndicator())
            {
                return;
            }
        }
        
        // 입력 처리
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
        
        // 처리 상태 리셋
        if (Input.touchCount == 0 && !Input.GetMouseButton(0))
        {
            isProcessingTouch = false;
        }
    }

    private bool IsOverUIOrIndicator()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
            return false;

        UnityEngine.EventSystems.PointerEventData pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        pointerData.position = Input.mousePosition;
        if (Input.touchCount > 0)
        {
            pointerData.position = Input.GetTouch(0).position;
        }

        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            GameObject hitObject = result.gameObject;
            
            if (IsIndicatorRelated(hitObject))
            {
                Debug.Log($"Indicator related object ignored: {hitObject.name}");
                continue;
            }

            if (hitObject.layer == 5)
            {
                Debug.Log($"Real UI detected: {hitObject.name} - blocking touch");
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
            
            // 더 상세한 디버그 로그 추가
            Debug.Log($"Checking object: {name}, Parent: {(current.parent ? current.parent.name : "null")}");
            
            // 인디케이터 관련 오브젝트만 무시 (더 정확한 조건)
            if ((name.Contains("Indicator") && !name.Contains("Button")) || 
                (name.Contains("Arrow") && !name.Contains("Button")) || 
                (name.Contains("Box") && !name.Contains("Button")) ||
                name.Contains("OffScreen") ||
                (name == "Text" && (current.parent?.name.Contains("Indicator") == true)) || // Text는 부모가 Indicator일 때만
                current.GetComponent<Indicator>() != null)
            {
                Debug.Log($"Found indicator related object: {name}");
                return true;
            }
            
            current = current.parent;
        }
        
        Debug.Log($"Object {obj.name} is NOT indicator related");
        return false;
    }

    void OnMouseDown()
    {
        // Update()에서 처리하므로 무시
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
        try
        {
            // 0-1 범위를 iOS 햅틱 타입으로 변환
            if (hapticIntensity <= 0.33f)
            {
                UnityEngine.iOS.Device.GenerateHapticFeedback(UnityEngine.iOS.HapticFeedbackType.ImpactFeedback_Light);
            }
            else if (hapticIntensity <= 0.66f)
            {
                UnityEngine.iOS.Device.GenerateHapticFeedback(UnityEngine.iOS.HapticFeedbackType.ImpactFeedback_Medium);
            }
            else
            {
                UnityEngine.iOS.Device.GenerateHapticFeedback(UnityEngine.iOS.HapticFeedbackType.ImpactFeedback_Heavy);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"iOS Haptic Failed: {e.Message}");
            Handheld.Vibrate();
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
                    
                    // 0-1 범위를 Android 햅틱 타입으로 변환
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
                    // 구형 안드로이드 - 지속시간으로 강도 조절 (30-100ms)
                    long duration = (long)(30 + (hapticIntensity * 70));
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
        // 기타 플랫폼 - 강도에 따라 진동 여부 결정
        if (hapticIntensity > 0.1f)
        {
            Handheld.Vibrate();
        }
#endif
    }

    // Android API 버전 확인 헬퍼 메서드
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
}