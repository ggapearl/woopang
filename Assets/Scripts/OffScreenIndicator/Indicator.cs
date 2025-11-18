using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class Indicator : MonoBehaviour
{
    [SerializeField] private IndicatorType indicatorType;
    private Image indicatorImage;
    private Text distanceText;

    public bool Active
    {
        get
        {
            return transform.gameObject.activeInHierarchy;
        }
    }

    public IndicatorType Type
    {
        get
        {
            return indicatorType;
        }
    }

    void Awake()
    {
        indicatorImage = transform.GetComponent<Image>();
        distanceText = transform.GetComponentInChildren<Text>();
    }

    public void SetImageColor(Color color)
    {
        indicatorImage.color = color;
    }

    public void SetDistanceText(float value, Color textColor, string placeName)
    {
        if (distanceText != null)
        {
            if (indicatorType == IndicatorType.ARROW)
            {
                // 화살표 인디케이터: 한글 2글자, 영어 1글자로 계산하여 16글자 제한
                int length = 0;
                int charIndex = 0;
                string truncatedName = placeName;

                // 글자 수 계산
                foreach (char c in placeName)
                {
                    length += (c >= '\uAC00' && c <= '\uD7A3') ? 2 : 1; // 한글은 2, 영어는 1
                    charIndex++;
                    if (length > 16)
                    {
                        // 16글자 초과 시 잘라내고 ".." 추가
                        truncatedName = placeName.Substring(0, charIndex - 1) + "..";
                        break;
                    }
                }

                // 텍스트 설정: 두 줄 (이름 + 거리), 사이즈 동일, 굵기 효과 없음
                distanceText.text = string.IsNullOrEmpty(placeName) ? 
                    (value >= 0 ? $"{Mathf.Floor(value)}m" : "") : 
                    $"{truncatedName}\n{Mathf.Floor(value)}m";
            }
            else
            {
                // 박스 인디케이터: 글자 수 제한 없이 전체 이름 표시, 모두 굵게
                distanceText.text = string.IsNullOrEmpty(placeName) ? 
                    (value >= 0 ? $"<b>{Mathf.Floor(value)}m</b>" : "") : 
                    $"<b>{placeName}\n{Mathf.Floor(value)}m</b>";
            }
            distanceText.color = textColor; // 서버에서 가져온 색상 적용
        }
    }

    public void SetTextRotation(Quaternion rotation)
    {
        if (distanceText != null)
        {
            distanceText.rectTransform.rotation = rotation;
        }
    }

    public void Activate(bool value)
    {
        transform.gameObject.SetActive(value);
    }

    public void SetScale(Vector3 scale)
    {
        transform.localScale = scale;
    }
}

public enum IndicatorType
{
    BOX,
    ARROW
}