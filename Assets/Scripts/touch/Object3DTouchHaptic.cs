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

    // 누를 때마다 살짝 커졌다 돌아온다 — 보물상자(GoldenGlowCube 1.15배 · 0.16초 · 속도 5)와 같은 느낌
    private const float NudgeScale = 1.15f, NudgeHold = 0.16f, NudgeSpeed = 5f;
    private Coroutine nudging;
    private Vector3 restScale;
    private float nudge = 1f;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        if (nudging != null)
        {
            StopCoroutine(nudging);
            EndNudge();
        }
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
        // 첫 사용 안내 중에는 오브젝트를 누르지 않는다 — 안내가 끝난 뒤부터
        if (R0926GuideOverlay.BlocksWorld) return;

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
        Nudge();

        // UIFeedbackManager를 통해 소리/진동 통합 처리
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
        Nudge();   // 같은 프레임에 TriggerFeedback 이 먼저 불렀어도 이어서 한 번만 커진다
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

    private void Nudge()
    {
        if (!isActiveAndEnabled) return;
        if (nudging != null) StopCoroutine(nudging);
        else restScale = transform.localScale;
        nudging = StartCoroutine(NudgeCo());
    }

    private IEnumerator NudgeCo()
    {
        for (float t = 0f; t < NudgeHold || nudge > 1.001f; t += Time.unscaledDeltaTime)
        {
            nudge = Mathf.Lerp(nudge, t < NudgeHold ? NudgeScale : 1f, Time.unscaledDeltaTime * NudgeSpeed);
            transform.localScale = restScale * nudge;
            yield return null;
        }
        EndNudge();
    }

    private void EndNudge()
    {
        nudge = 1f;
        transform.localScale = restScale;
        nudging = null;
    }
}
