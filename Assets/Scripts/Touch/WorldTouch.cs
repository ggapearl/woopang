using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 손가락 아래에 화면 UI 가 있나 — 3D 오브젝트(장소 · 사람 · 보물 · 큐브 돌리기 · 누름 진동)가 손가락을 받기 전에 묻는 한 곳.
/// 11-18 대표님 N2 「하단탭이 이미 가리고 있는 상태인데 해당 오브젝트 우선순위가 좀 꼬여있는 것 같은데」 — 저마다 따로 묻던 것을
/// (IsPointerOverGameObject 는 손가락이 닿은 첫 프레임엔 그 손가락을 아직 모를 때가 있고 · 이름으로 거르던 것도 있었다) 그 자리 레이캐스트 하나로.
/// </summary>
public static class WorldTouch
{
    private static readonly List<RaycastResult> hits = new List<RaycastResult>();
    private static int memoFrame = -1;
    private static Vector2 memoPos;
    private static GameObject memoTop;

    /// <summary>pos 에서 맨 위에 있는 화면 UI — 세상 속 판(월드 공간 캔버스) · 인디케이터 그림은 덮개가 아니다. 없으면 null.</summary>
    public static GameObject TopUI(Vector2 pos)
    {
        if (memoFrame == Time.frameCount && memoPos == pos) return memoTop;   // 오브젝트마다 묻는다 — 한 프레임에 한 번만 쏜다
        memoFrame = Time.frameCount;
        memoPos = pos;
        memoTop = null;
        var es = EventSystem.current;
        if (es == null) return null;
        es.RaycastAll(new PointerEventData(es) { position = pos }, hits);
        foreach (var h in hits)
        {
            if (h.gameObject == null || !(h.module is GraphicRaycaster)) continue;
            var cv = h.module.GetComponent<Canvas>();
            if (cv != null && cv.renderMode == RenderMode.WorldSpace) continue;
            if (IndicatorArt(h.gameObject.transform)) continue;
            memoTop = h.gameObject;
            break;
        }
        hits.Clear();
        return memoTop;
    }

    /// <summary>3D 오브젝트가 이 손가락을 받아도 되나 — 덮은 UI 가 없고 · 첫 사용 안내 중이 아니고 · 댓글 창이 닫혀 있다.</summary>
    public static bool Free(Vector2 pos)
    {
        if (R0926GuideOverlay.BlocksWorld) return false;
        if (CommentManager.Instance != null && CommentManager.Instance.IsPanelOpen) return false;
        return TopUI(pos) == null;
    }

    // 오브젝트를 둘러싼 인디케이터의 그림 · 글씨 — 그 안의 버튼(× · '삭제')만 덮개로 친다
    private static bool IndicatorArt(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.GetComponent<Selectable>() != null) return false;
            if (t.GetComponent<Indicator>() != null) return true;
        }
        return false;
    }
}
