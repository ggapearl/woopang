/**
 * P2PUserGesture.cs
 * Handles AR gesture animations for P2P user avatars
 * Displays visual effects when users send gestures (wave, point, thumbs up, etc.)
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-01
 */

using UnityEngine;
using System.Collections;

public class P2PUserGesture : MonoBehaviour
{
    [Header("Gesture Visual Effects")]
    public GameObject waveEffectPrefab;
    public GameObject pointEffectPrefab;
    public GameObject thumbsUpEffectPrefab;
    public GameObject heartEffectPrefab;
    public GameObject celebrateEffectPrefab;

    [Header("Effect Settings")]
    public Transform effectSpawnPoint; // Where effects appear (above avatar)
    public float effectDuration = 2f;
    public float effectScale = 1.5f;

    [Header("Animation Settings")]
    public bool useSimpleScale = true; // Simple scale animation for effects
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Audio")]
    public AudioClip waveSound;
    public AudioClip pointSound;
    public AudioClip thumbsUpSound;
    public AudioClip heartSound;
    public AudioClip celebrateSound;
    public AudioSource audioSource;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Coroutine currentGestureCoroutine;

    void Awake()
    {
        // Setup effect spawn point if not set
        if (effectSpawnPoint == null)
        {
            GameObject spawnObj = new GameObject("EffectSpawnPoint");
            spawnObj.transform.SetParent(transform);
            spawnObj.transform.localPosition = new Vector3(0, 2f, 0); // Above avatar
            effectSpawnPoint = spawnObj.transform;
        }

        // Setup audio source if not set
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.maxDistance = 50f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
        }
    }

    /// <summary>
    /// Play gesture animation based on type
    /// Called by P2PManager when receiving 'receive_gesture' event
    /// </summary>
    public void PlayGesture(string gestureType)
    {
        if (showDebugLogs)

        // Stop current gesture if playing
        if (currentGestureCoroutine != null)
        {
            StopCoroutine(currentGestureCoroutine);
        }

        // Play new gesture
        switch (gestureType.ToLower())
        {
            case "wave":
                currentGestureCoroutine = StartCoroutine(PlayGestureEffect(waveEffectPrefab, waveSound));
                break;

            case "point":
                currentGestureCoroutine = StartCoroutine(PlayGestureEffect(pointEffectPrefab, pointSound));
                break;

            case "thumbsup":
            case "thumbs_up":
                currentGestureCoroutine = StartCoroutine(PlayGestureEffect(thumbsUpEffectPrefab, thumbsUpSound));
                break;

            case "heart":
                currentGestureCoroutine = StartCoroutine(PlayGestureEffect(heartEffectPrefab, heartSound));
                break;

            case "celebrate":
                currentGestureCoroutine = StartCoroutine(PlayGestureEffect(celebrateEffectPrefab, celebrateSound));
                break;

            default:
                Debug.LogWarning($"[P2PUserGesture] Unknown gesture type: {gestureType}");
                // Play default wave gesture
                currentGestureCoroutine = StartCoroutine(PlayGestureEffect(waveEffectPrefab, waveSound));
                break;
        }
    }

    /// <summary>
    /// Play gesture effect with animation
    /// </summary>
    private IEnumerator PlayGestureEffect(GameObject effectPrefab, AudioClip sound)
    {
        GameObject effectInstance = null;

        // Instantiate effect if prefab is set
        if (effectPrefab != null && effectSpawnPoint != null)
        {
            effectInstance = Instantiate(effectPrefab, effectSpawnPoint.position, effectSpawnPoint.rotation);
            effectInstance.transform.SetParent(effectSpawnPoint);

            // Setup renderer for alpha animation
            Renderer renderer = effectInstance.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = effectInstance.GetComponentInChildren<Renderer>();
            }

            // Play sound
            if (sound != null && audioSource != null)
            {
                audioSource.PlayOneShot(sound);
            }

            // Animate effect
            float elapsed = 0f;
            Vector3 baseScale = effectInstance.transform.localScale;

            while (elapsed < effectDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / effectDuration;

                // Scale animation
                float scaleMultiplier = scaleCurve.Evaluate(t) * effectScale;
                effectInstance.transform.localScale = baseScale * scaleMultiplier;

                // Alpha animation (fade in/out)
                if (renderer != null && renderer.material != null)
                {
                    Color color = renderer.material.color;
                    color.a = alphaCurve.Evaluate(t);
                    renderer.material.color = color;
                }

                // Billboard effect - face camera
                if (Camera.main != null)
                {
                    effectInstance.transform.rotation = Quaternion.LookRotation(
                        effectInstance.transform.position - Camera.main.transform.position
                    );
                }

                yield return null;
            }

            // Cleanup
            if (effectInstance != null)
            {
                Destroy(effectInstance);
            }
        }
        else
        {
            // Fallback: Just play sound if no prefab
            if (sound != null && audioSource != null)
            {
                audioSource.PlayOneShot(sound);
            }

            // Simple scale animation on avatar itself
            if (useSimpleScale)
            {
                yield return StartCoroutine(SimpleScaleAnimation());
            }
            else
            {
                yield return new WaitForSeconds(effectDuration);
            }
        }

        currentGestureCoroutine = null;
    }

    /// <summary>
    /// Simple scale animation as fallback when no effect prefab is set
    /// </summary>
    private IEnumerator SimpleScaleAnimation()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.2f;

        float elapsed = 0f;
        float duration = effectDuration * 0.5f;

        // Scale up
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, scaleCurve.Evaluate(t));
            yield return null;
        }

        elapsed = 0f;

        // Scale down
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, scaleCurve.Evaluate(t));
            yield return null;
        }

        transform.localScale = originalScale;
    }

    /// <summary>
    /// Create simple emoji-like sprite effect (when effect prefabs not available)
    /// </summary>
    public void CreateSimpleEmojiEffect(string emoji)
    {
        // Create a simple sprite renderer with emoji texture
        GameObject emojiObj = new GameObject($"Emoji_{emoji}");
        emojiObj.transform.SetParent(effectSpawnPoint);
        emojiObj.transform.localPosition = Vector3.zero;

        // Add sprite renderer
        SpriteRenderer sr = emojiObj.AddComponent<SpriteRenderer>();
        // TODO: Load emoji sprite from Resources
        // sr.sprite = Resources.Load<Sprite>($"Emojis/{emoji}");

        // Animate
        StartCoroutine(AnimateSimpleEmoji(emojiObj, sr));
    }

    private IEnumerator AnimateSimpleEmoji(GameObject emojiObj, SpriteRenderer sr)
    {
        float elapsed = 0f;
        Vector3 startPos = emojiObj.transform.localPosition;
        Vector3 endPos = startPos + Vector3.up * 1.5f;

        while (elapsed < effectDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / effectDuration;

            // Move up
            emojiObj.transform.localPosition = Vector3.Lerp(startPos, endPos, t);

            // Fade out
            Color color = sr.color;
            color.a = alphaCurve.Evaluate(t);
            sr.color = color;

            // Billboard
            if (Camera.main != null)
            {
                emojiObj.transform.rotation = Quaternion.LookRotation(
                    emojiObj.transform.position - Camera.main.transform.position
                );
            }

            yield return null;
        }

        Destroy(emojiObj);
    }

    /// <summary>
    /// Quick test method for editor debugging
    /// </summary>
    [ContextMenu("Test Wave Gesture")]
    public void TestWaveGesture()
    {
        PlayGesture("wave");
    }

    [ContextMenu("Test Heart Gesture")]
    public void TestHeartGesture()
    {
        PlayGesture("heart");
    }

    [ContextMenu("Test Celebrate Gesture")]
    public void TestCelebrateGesture()
    {
        PlayGesture("celebrate");
    }
}
