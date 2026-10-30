using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 추가 사진 줄 — 두 줄로 위에서 아래, 왼쪽에서 오른쪽(1 3 5 7 9 / 2 4 6 8 10), 넘치면 옆으로 밀어 본다.
/// 10-27 대표님 「서브사진들의 사진을 키워야 … (혹은 옆으로 드래그 해서 스크롤로) … 현재 3줄로 보이는 로직을 두줄만 …
/// '+' 버튼은 사진을 업로드하지 않은 경우에는 1번 위치에, 1개를 업로드했을 때는 2번 위치에」.
/// 장소 추가 · 3D 모델 추가 · 장소 정보 수정이 같이 쓴다 (줄·'+' 는 Redesign0926Apply 가 그리드 안에 만들어 둔다).
/// '+' 는 늘 사진(·불러오는 빈 칸) 다음 칸이고 열 칸이 차면 숨는다 — '+' 를 누른 뒤 칸이 늘면 끝까지 밀어 새 칸을 보여 준다.
/// 꾹 눌러 든 사진을 옮기는 손가락(R0926PhotoReorder)·세로로 끄는 손가락·밀 게 없을 때는 받지 않는다 — 들고 가장자리로 가면 그쪽으로 민다(EdgeScroll)
/// </summary>
public class R0926PhotoStrip : ScrollRect
{
    [SerializeField] private RectTransform addTile;   // '+' — 그리드 맨 끝 칸
    [SerializeField] private int maxTiles = 10;

    private const float EdgeBand = 120f;    // 사진을 들고 가장자리 이만큼 안(캔버스 단위)으로 오면 민다
    private const float EdgeSpeed = 700f;
    private const float RevealSpeed = 10f;
    private const float RevealTime = 0.6f;

    private static readonly List<R0926PhotoStrip> live = new List<R0926PhotoStrip>();

    private Canvas root;
    private bool userDragging;
    private bool follow;          // '+' 를 누른 뒤 — 칸이 늘면 끝까지 밀어 보여 준다
    private float revealUntil;
    private int lastTiles = -1;
    private bool keepPlace;       // 시트를 끌어 내리는 동안 잠깐 꺼졌다(R0926SwipeDismiss) — 다시 켜져도 그 자리 그대로

    /// <summary>그 자리에 옆으로 밀리는 사진 줄이 있다 — 장소/3D/보물 카드 넘기기는 이 손가락을 놓아준다</summary>
    public static bool Covers(Vector2 screen)
    {
        foreach (var s in live)
            if (s != null && s.isActiveAndEnabled && s.Scrollable && s.Shows(screen)) return true;
        return false;
    }

    /// <summary>사진이 줄 폭을 넘었다 (옆으로 밀 게 있다)</summary>
    public bool Scrollable => content != null && content.rect.width > viewRect.rect.width + 1f;

    /// <summary>화면의 그 점이 줄 안(잘려 보이지 않는 칸 말고 보이는 자리)이다</summary>
    public bool Shows(Vector2 screen) => RectTransformUtility.RectangleContainsScreenPoint(viewRect, screen, Cam);

    protected override void Awake()
    {
        base.Awake();
        var c = GetComponentInParent<Canvas>(true);
        root = c != null ? c.rootCanvas : null;
        if (!Application.isPlaying || addTile == null) return;
        var b = addTile.GetComponent<Button>();
        if (b != null) b.onClick.AddListener(() => follow = true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!Application.isPlaying) return;
        if (!live.Contains(this)) live.Add(this);
        if (!keepPlace && content != null)
        {
            // 창을 새로 열면 첫 칸부터
            StopMovement();
            content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
        }
        keepPlace = false;
    }

    protected override void OnDisable()
    {
        if (Application.isPlaying)
        {
            keepPlace = gameObject.activeInHierarchy;
            live.Remove(this);
            follow = false;
            revealUntil = 0f;
            userDragging = false;
        }
        base.OnDisable();
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        // 받지 않으면 끌기 상태가 안 켜져 OnDrag 도 그대로 지나간다
        Vector2 d = eventData.position - eventData.pressPosition;
        if (R0926PhotoReorder.Holding || Mathf.Abs(d.y) > Mathf.Abs(d.x) || !Scrollable) return;
        userDragging = true;
        follow = false;
        revealUntil = 0f;
        base.OnBeginDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        userDragging = false;
        base.OnEndDrag(eventData);
    }

    protected override void LateUpdate()
    {
        if (Application.isPlaying && content != null) KeepAddTile();
        base.LateUpdate();
        if (!Application.isPlaying || content == null || userDragging || Time.unscaledTime > revealUntil) return;
        // 새 칸까지 밀어 보여 준다 (칸 폭은 한두 프레임 뒤에 맞춰지니 잠깐 동안 계속 따라간다)
        float target = Mathf.Min(0f, viewRect.rect.width - content.rect.width);
        var p = content.anchoredPosition;
        p.x = Mathf.Lerp(p.x, target, 1f - Mathf.Exp(-RevealSpeed * Time.unscaledDeltaTime));
        StopMovement();
        SetContentAnchoredPosition(p);
    }

    // '+' 는 사진·빈 칸 다음 — 열 칸이 차면 숨긴다
    private void KeepAddTile()
    {
        int tiles = 0;
        foreach (Transform c in content)
            if (c != addTile && c.gameObject.activeSelf) tiles++;
        if (addTile != null && addTile.parent == content)
        {
            bool show = tiles < maxTiles;
            if (addTile.gameObject.activeSelf != show) addTile.gameObject.SetActive(show);
            if (addTile.GetSiblingIndex() != content.childCount - 1) addTile.SetAsLastSibling();
        }
        if (tiles > lastTiles && lastTiles >= 0 && follow) revealUntil = Time.unscaledTime + RevealTime;
        lastTiles = tiles;
    }

    /// <summary>사진을 들고 줄 가장자리로 가면 그쪽으로 민다 — R0926PhotoReorder 가 들고 있는 동안 부른다</summary>
    public void EdgeScroll(Vector2 screen)
    {
        if (content == null || !Scrollable) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, screen, Cam, out var p)) return;
        var r = viewRect.rect;
        if (p.y < r.yMin - EdgeBand || p.y > r.yMax + EdgeBand) return;
        float dir = p.x < r.xMin + EdgeBand ? 1f : p.x > r.xMax - EdgeBand ? -1f : 0f;
        if (dir == 0f) return;
        StopMovement();
        var pos = content.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x + dir * EdgeSpeed * Time.unscaledDeltaTime, Mathf.Min(0f, r.width - content.rect.width), 0f);
        SetContentAnchoredPosition(pos);
    }

    private Camera Cam => root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
}
