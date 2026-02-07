using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스와이프 페이지들의 크기를 viewport 너비에 맞춰 동적으로 조절
/// 가로 스크롤 컨테이너의 Content에 추가
/// </summary>
public class SwipePageSizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("상위 ScrollRect의 Viewport")]
    public RectTransform viewport;

    private RectTransform contentRect;
    private LayoutElement[] pageLayouts;
    private float lastViewportWidth;

    void Start()
    {
        contentRect = GetComponent<RectTransform>();
        CachePageLayouts();
        UpdatePageSizes();
    }

    void Update()
    {
        // Viewport 크기 변경 감지
        if (viewport != null && Mathf.Abs(viewport.rect.width - lastViewportWidth) > 1f)
        {
            UpdatePageSizes();
        }
    }

    private void CachePageLayouts()
    {
        pageLayouts = new LayoutElement[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            pageLayouts[i] = transform.GetChild(i).GetComponent<LayoutElement>();
            if (pageLayouts[i] == null)
            {
                pageLayouts[i] = transform.GetChild(i).gameObject.AddComponent<LayoutElement>();
            }
        }
    }

    private void UpdatePageSizes()
    {
        if (viewport == null) return;

        float viewportWidth = viewport.rect.width;
        lastViewportWidth = viewportWidth;

        // 각 페이지의 너비를 viewport 너비로 설정
        foreach (var layout in pageLayouts)
        {
            if (layout != null)
            {
                layout.minWidth = viewportWidth;
                layout.preferredWidth = viewportWidth;
            }
        }

        // Content 전체 너비 설정
        if (contentRect != null)
        {
            contentRect.sizeDelta = new Vector2(viewportWidth * transform.childCount, contentRect.sizeDelta.y);
        }

        // 레이아웃 강제 재빌드
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    public void Refresh()
    {
        CachePageLayouts();
        UpdatePageSizes();
    }
}
