using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggle 상태에 따라 Background 이미지를 자동으로 변경하는 컴포넌트
/// 체크됨/안됨 2가지 이미지를 간단하게 설정 가능
/// </summary>
[RequireComponent(typeof(Toggle))]
public class ToggleImageController : MonoBehaviour
{
    [Header("Background Images")]
    [SerializeField] private Sprite uncheckedSprite; // 체크 안됨 이미지
    [SerializeField] private Sprite checkedSprite;   // 체크됨 이미지

    [Header("References")]
    [SerializeField] private Image backgroundImage;  // Background Image 컴포넌트

    private Toggle toggle;

    void Awake()
    {
        toggle = GetComponent<Toggle>();

        // Background 자동 찾기
        if (backgroundImage == null)
        {
            Transform bgTransform = transform.Find("Background");
            if (bgTransform != null)
            {
                backgroundImage = bgTransform.GetComponent<Image>();
            }
        }

        // 초기 이미지 설정
        UpdateImage(toggle.isOn);

        // Toggle 값 변경 리스너 등록
        toggle.onValueChanged.AddListener(UpdateImage);
    }

    void OnDestroy()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(UpdateImage);
        }
    }

    /// <summary>
    /// Toggle 상태에 따라 Background 이미지 업데이트
    /// </summary>
    private void UpdateImage(bool isOn)
    {
        if (backgroundImage == null) return;

        backgroundImage.sprite = isOn ? checkedSprite : uncheckedSprite;

        Debug.Log($"[ToggleImageController] {gameObject.name} 이미지 변경: {(isOn ? "체크됨" : "체크 안됨")}");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Inspector에서 값 변경 시 즉시 반영 (에디터 전용)
    /// </summary>
    void OnValidate()
    {
        if (Application.isPlaying) return;

        if (toggle == null)
            toggle = GetComponent<Toggle>();

        if (backgroundImage == null)
        {
            Transform bgTransform = transform.Find("Background");
            if (bgTransform != null)
            {
                backgroundImage = bgTransform.GetComponent<Image>();
            }
        }

        if (toggle != null && backgroundImage != null)
        {
            UpdateImage(toggle.isOn);
        }
    }
#endif
}
