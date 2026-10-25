using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 장소 상세(오브젝트 두 번 톡) 세로 배치를 위로 — 10-24 대표님 「각 정보값이 레이아웃을 좀 줄이다보니까 위쪽으로 여백이 큰데
    /// 더블터치해서 발생하는 버튼이나 서브사진들 및 각 레이아웃들을 전체적으로 좀 올려야」.
    ///  · 아이콘 줄 · 이름 · 로마자 · 만든 사람 줄은 위 기준이라 그대로 DetailLift 만큼 (아이콘 줄 위끝 420 → 255 — 노치가 가장 큰 아이폰 16 Pro 안전선 225 아래)
    ///  · 사진 · 정보 글 · 넘김 화살표는 앵커만 옮긴다 — DoubleTap3D 가 처음 열 때 사진 자리(0,-200)를 기억해 넘김에 쓴다 (가로 배치와 같은 방식)
    ///  · X 는 사진과 같은 앵커로 사진 아래끝에 걸친다 (예전엔 화면 아래 기준이라 사진만 올리면 떨어진다)
    ///  · 댓글 미리보기는 도크처럼 아래 안전영역만큼 오른다 (예전엔 도크만 올라 아이폰에서 글이 도크 윗선에 붙었다)
    ///    · 미리보기의 투명한 누름 칸이 X 아래쪽 절반을 덮어 그 자리를 누르면 닫히지 않고 댓글 창이 열렸다 — 누름 칸 위쪽을 줄인다
    ///  · 노치 아래 상태 알약('주변에서 N곳을 찾았어요')은 상세가 열려 있는 동안 숨긴다 — 올린 아이콘 줄과 같은 높이다 (ApplyTopSlot)
    /// 가로 배치는 그대로 — ApplyFadeAndLandscape 가 여기서 정한 세로 값을 세로 자리로 다시 담는다
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float DetailLift = 165f;
        private const float DetailCanvasH = 3158f;   // 요즘 폰(19.5:9 ~ 20:9)의 캔버스 높이 — 앵커로 옮기는 칸이 위 기준 칸과 같은 만큼 오르게
        private const float PreviewTapTrim = 60f;    // 댓글 미리보기 누름 칸에서 뺄 위쪽 (글은 칸 가운데 · 하트는 따로 누름)

        private static void ApplyDetailLift(Transform root, List<string> log)
        {
            var full = Find(root, "FullScreenPanel");
            if (full == null) { log.Add("상세 올리기: FullScreenPanel 없음"); return; }
            var ft = full.transform;
            float ay = 0.5f + DetailLift / DetailCanvasH;

            void Top(string n, Vector2 pos)
            {
                var go = Find(ft, n);
                if (go == null) { log.Add("상세 올리기: " + n + " 없음"); return; }
                RT(go).anchoredPosition = pos;
                EditorUtility.SetDirty(RT(go));
            }
            void Mid(string n, float ax)
            {
                var go = Find(ft, n);
                if (go == null) { log.Add("상세 올리기: " + n + " 없음"); return; }
                RT(go).anchorMin = RT(go).anchorMax = new Vector2(ax, ay);
                EditorUtility.SetDirty(RT(go));
            }

            Top("TopUIPanel", new Vector2(-100, -100 + DetailLift));
            Top("PlaceName", new Vector2(0, -600 + DetailLift));
            Top("Roman0926", new Vector2(0, -680 + DetailLift));
            Top("CreatedByText", new Vector2(0, -750 + DetailLift));
            Top("TourAPI_Description", new Vector2(0, -750 + DetailLift));

            Mid("FullScreenImage", 0.5f);
            Mid("PlaceInfoTextPanel", 0.5f);
            Mid("Button_Previous", 0f);
            Mid("Button_Next", 1f);

            var close = Find(ft, "Button_Close");
            if (close != null)   // 사진 가운데에서 -200(사진 자리) - 900(사진 높이 절반) = 사진 아래끝
                SetRect(RT(close), new Vector2(0.5f, ay), new Vector2(0.5f, ay), new Vector2(0.5f, 0.5f), new Vector2(0, -1100), new Vector2(150, 150));

            var preview = Find(ft, "CommentPreviewPanel");
            if (preview != null)
            {
                var inset = Ensure<R0926SafeInset>(preview);
                inset.SetEdge(R0926SafeInset.Edge.Bottom);
                EditorUtility.SetDirty(inset);
                var img = preview.GetComponent<Image>();
                if (img != null) { img.raycastPadding = new Vector4(0, 0, 0, PreviewTapTrim); EditorUtility.SetDirty(img); }
            }
            log.Add("detail lift ok");
        }
    }
}
