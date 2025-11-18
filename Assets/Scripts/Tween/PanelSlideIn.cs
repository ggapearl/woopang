using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PanelSlideIn : MonoBehaviour
{
    public Button openButton;      // 패널을 열 버튼
    public RectTransform panelRect; // 슬라이드 인할 패널의 RectTransform
    public float slideDuration = 0.5f; // 슬라이드 애니메이션 지속 시간
    public Vector3 startPosition;    // 패널의 시작 위치 (화면 밖)
    public Vector3 endPosition;      // 패널의 목표 위치 (화면 안)

    void Start()
    {
        // 버튼 클릭 이벤트에 슬라이드 인 메서드 연결
        openButton.onClick.AddListener(SlideInPanel);

        // 패널을 시작 위치로 설정하고 비활성화 (필요 시)
        panelRect.localPosition = startPosition;
        panelRect.gameObject.SetActive(false);
    }

    void SlideInPanel()
    {
        // 패널 활성화
        panelRect.gameObject.SetActive(true);

        // 패널을 시작 위치에서 목표 위치로 슬라이드 인
        panelRect.DOLocalMove(endPosition, slideDuration)
            .SetEase(Ease.OutQuad) // 부드러운 감속 효과
            .OnComplete(() => Debug.Log("패널 슬라이드 인 완료"));
    }
}