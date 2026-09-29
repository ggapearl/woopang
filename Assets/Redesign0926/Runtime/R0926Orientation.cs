using System;
using UnityEngine;

/// <summary>
/// 세로 ↔ 가로 배치 전환 (스마트 안경·가로 거치 대비).
/// 0926 씬에서만 가로 회전을 허용한다 — ProjectSettings(0529 빌드)는 그대로 세로 전용.
/// 가로: 도크 → 오른쪽 세로 막대, 카드 → 왼쪽 열, 위치 칩 → 왼쪽 아래. 큰 카드는 화면 높이에 맞춰 축소.
/// 각 요소의 세로/가로 배치값은 에디터 도구가 미리 넣어 둔다.
/// </summary>
public class R0926Orientation : MonoBehaviour
{
    [Serializable]
    public struct Snap
    {
        public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
    }

    [Serializable]
    public class Slot
    {
        public RectTransform target;
        public Snap portrait;
        public Snap landscape;
        [Tooltip("가로에서 화면 높이에 맞춰 줄일 때의 원래 높이 (0 이면 안 줄임)")]
        public float fitHeight;
        public R0926SafeInset inset;
        public R0926SafeInset.Edge portraitEdge;
        public R0926SafeInset.Edge landscapeEdge;
    }

    [SerializeField] private bool allowLandscape = true;
    [SerializeField] private Slot[] slots = new Slot[0];

    public static bool IsLandscape { get; private set; }
    private int state = -1;

    private void Awake()
    {
        if (!allowLandscape) return;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    private void Update()
    {
        int land = Screen.width > Screen.height ? 1 : 0;
        if (land == state) return;
        state = land;
        IsLandscape = land == 1;
        ApplyAll(IsLandscape);
    }

    public void ApplyAll(bool landscape, float canvasHeight = -1f)
    {
        var canvas = GetComponentInParent<Canvas>();
        float canvasH = canvasHeight > 0f ? canvasHeight
            : canvas != null ? ((RectTransform)canvas.rootCanvas.transform).rect.height : 1458f;
        foreach (var s in slots)
        {
            if (s == null || s.target == null) continue;
            var sn = landscape ? s.landscape : s.portrait;
            var rt = s.target;
            rt.anchorMin = sn.anchorMin; rt.anchorMax = sn.anchorMax; rt.pivot = sn.pivot;
            rt.anchoredPosition = sn.anchoredPosition; rt.sizeDelta = sn.sizeDelta;
            float k = landscape && s.fitHeight > 0f ? Mathf.Min(1f, (canvasH - 80f) / s.fitHeight) : 1f;
            rt.localScale = new Vector3(k, k, 1f);
            if (s.inset != null) s.inset.Reapply(landscape ? s.landscapeEdge : s.portraitEdge);

            // 장소 상세 사진의 넘김용 짝(실행 중에 생긴다) — 앵커·크기를 같이 옮겨야 넘길 때 제자리에 들어온다
            if (rt.name == "FullScreenImage" && rt.parent != null && rt.parent.Find("NextFullscreenImage") is RectTransform next)
            {
                next.anchorMin = rt.anchorMin; next.anchorMax = rt.anchorMax; next.pivot = rt.pivot;
                next.sizeDelta = rt.sizeDelta; next.localScale = rt.localScale;
            }
        }
    }
}
