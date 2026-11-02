using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PanelSlideIn : MonoBehaviour
{
    public Button openButton;
    public RectTransform panelRect;
    public float slideDuration = 0.5f;
    public Vector3 startPosition;
    public Vector3 endPosition;

    void Start()
    {
        openButton.onClick.AddListener(SlideInPanel);
        panelRect.localPosition = startPosition;
        panelRect.gameObject.SetActive(false);
    }

    void SlideInPanel()
    {
        panelRect.gameObject.SetActive(true);
        StartCoroutine(SlideInCoroutine());
    }

    IEnumerator SlideInCoroutine()
    {
        float elapsed = 0f;
        Vector3 startPos = panelRect.localPosition;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;

            // Ease.OutQuad 효과 구현
            t = 1f - (1f - t) * (1f - t);

            panelRect.localPosition = Vector3.Lerp(startPos, endPosition, t);
            yield return null;
        }

        panelRect.localPosition = endPosition;
    }
}
