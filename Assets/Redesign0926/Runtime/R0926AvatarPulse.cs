using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도크 프로필 사진의 테두리. 색은 ProfileManager 가 공개 상태에 따라 칠한다(분홍 = 전체공개).
/// 전체공개일 땐 테두리가 늘 숨쉬듯 은은하게 밝아졌다 어두워지고(4초), 밝아질 때 작은 물결이 한 번 퍼진다.
/// 다른 상태에선 가만히 있다.
/// </summary>
public class R0926AvatarPulse : MonoBehaviour
{
    [SerializeField] private Image outline;   // ProfileManager 가 색을 칠하는 테두리
    [SerializeField] private Image ripple;    // 퍼지는 물결 (얇은 고리)
    [SerializeField] private float period = 4f;
    [SerializeField] private float breathScale = 0.04f;

    private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f);

    private void OnDisable()
    {
        if (outline != null) outline.rectTransform.localScale = Vector3.one;
        if (ripple != null) ripple.enabled = false;
    }

    private void LateUpdate()
    {
        if (outline == null) return;
        Color c = outline.color;
        bool pub = Mathf.Abs(c.r - Pink.r) < 0.12f && Mathf.Abs(c.g - Pink.g) < 0.12f && Mathf.Abs(c.b - Pink.b) < 0.12f;
        float ph = Mathf.Repeat(Time.unscaledTime, period) / period;
        if (!pub)
        {
            outline.rectTransform.localScale = Vector3.one;
            if (ripple != null && ripple.enabled) ripple.enabled = false;
            return;
        }
        // 숨쉬기: 0→1→0 부드럽게
        float breath = 0.5f - 0.5f * Mathf.Cos(ph * Mathf.PI * 2f);
        outline.rectTransform.localScale = Vector3.one * (1f + breathScale * breath);
        if (ripple != null)
        {
            // 가장 밝아질 무렵(0.35~0.95)에 물결 한 번
            float k = Mathf.InverseLerp(0.35f, 0.95f, ph);
            bool on = ph >= 0.35f && ph < 0.95f;
            if (ripple.enabled != on) ripple.enabled = on;
            if (on)
            {
                ripple.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.5f, 1f - Mathf.Pow(1f - k, 2f));
                var rc = c; rc.a = 0.7f * (1f - k);
                ripple.color = rc;
            }
        }
    }
}
