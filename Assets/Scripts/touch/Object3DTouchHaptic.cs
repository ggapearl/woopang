using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class Object3DTouchHaptic : MonoBehaviour
{
    [Header("Haptic Settings")]
    [Tooltip("진동 강도 (0~1). UIFeedbackManager를 통해 iOS/Android 동일하게 적용")]
    [SerializeField, Range(0f, 1f)] private float hapticIntensity = 0.7f;

    [Header("Sound Settings")]
    [Tooltip("1번째 터치(싱글탭) 사운드")]
    [SerializeField] private AudioClip touchSound;

    [Tooltip("2번째 터치(더블탭 확정) 사운드 — 미설정 시 touchSound 사용")]
    [SerializeField] private AudioClip doubleTapSound;

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
        bool inputDetected = false;
        Vector2 inputPosition = Vector2.zero;

        // Touch Input
        if (Touch.activeTouches.Count > 0 && Touch.activeTouches[0].phase == TouchPhase.Began && !isProcessingTouch)
        {
            inputDetected = true;
            inputPosition = Touch.activeTouches[0].screenPosition;

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

            if (IsOverUIOrIndicator(inputPosition))
            {
                return;
            }
        }

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
                continue;
            }

            if (hitObject.layer == 5)
            {
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

            if ((name.Contains("Indicator") && !name.Contains("Button")) ||
                (name.Contains("Arrow") && !name.Contains("Button")) ||
                (name.Contains("Box") && !name.Contains("Button")) ||
                name.Contains("OffScreen") ||
                (name == "Text" && (current.parent?.name.Contains("Indicator") == true)) ||
                current.GetComponent<Indicator>() != null)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    void OnMouseDown()
    {
        return;
    }

    private void TriggerFeedback()
    {
        // UIFeedbackManager를 통해 소리/진동 통합 처리
        // 무음 모드면 진동만, 소리 나면 소리만 (hapticOnlyWhenSilent 설정에 따라)
        if (UIFeedbackManager.Instance != null)
        {
            UIFeedbackManager.Instance.HandleTouchFeedbackDirect(hapticIntensity, touchSound);
        }
        else
        {
            if (touchSound != null && audioSource != null)
                audioSource.PlayOneShot(touchSound, soundVolume);
        }
    }

    /// <summary>
    /// DoubleTap3D에서 더블탭 확정 시 호출 — 2nd 터치 사운드 + 햅틱
    /// </summary>
    public void PlayDoubleTapFeedback()
    {
        AudioClip clip = doubleTapSound != null ? doubleTapSound : touchSound;

        if (UIFeedbackManager.Instance != null)
        {
            UIFeedbackManager.Instance.HandleTouchFeedbackDirect(hapticIntensity, clip);
        }
        else
        {
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip, soundVolume);
        }
    }
}
