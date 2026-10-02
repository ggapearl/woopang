using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v6: 거리별 흐림(큐브·GLB) + 가로 화면 배치.
    /// 가로 배치값은 여기서 계산해 R0926Orientation 에 넣는다 (세로 배치값 = 지금 씬 값). 반드시 다른 단계 뒤에 실행.
    /// 가로 캔버스 ≈ 3160 × 1458.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float RailW = DockHeight;   // 가로 도크 막대 폭
        private const float RailH = 920f;
        private const float RailMargin = 40f;
        private const float LandCardW = 1500f;
        private const float LandSheetW = 1400f;
        // 가로에서 세로 막대 칸 위치 (위→아래: 목록 · 추가 · 메시지 · 프로필)
        private static float LandSlotY(float portraitX) => 1f - portraitX;

        private static void ApplyFadeAndLandscape(Transform root, List<string> log)
        {
            // ── 거리별 흐림 + 대기 원근 ──
            var marker = Find(root, "Redesign0926Marker");
            if (marker != null) Ensure<R0926DistanceFade>(marker);   // 실행 때 FilterManager 반경(500m)에 맞춰 흐림·안개·카메라 거리(650m)를 다시 잡는다

            // ── 인디케이터 박스 X (이 기기에서 숨기기) ──
            if (marker != null)
            {
                var close = Ensure<R0926IndicatorClose>(marker);
                var closeSo = new SerializedObject(close);
                closeSo.FindProperty("icon").objectReferenceValue = Spr("r0926_x_glyph");     // 박스 색으로 칠하는 흰 X
                closeSo.FindProperty("boxCut").objectReferenceValue = Spr("r0926_box_cut");   // 오른쪽 위 꺾쇠를 뺀 박스
                closeSo.ApplyModifiedPropertiesWithoutUndo();
            }
            // 씬 안개 켜기 — RenderSettings 는 '활성 씬' 것만 바뀌므로 0926 을 잠깐 활성으로 (빌드 때 안개 변형이 남도록)
            var scene = root.gameObject.scene;
            var prevActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene))
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 60f;
                RenderSettings.fogEndDistance = 1200f;   // = 3D 오브젝트 반경 500m × 2.4 (R0926DistanceFade 와 같은 값)
                RenderSettings.fogColor = new Color(0.77f, 0.81f, 0.85f, 1f);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(prevActive);
            }

            // ── 가로 배치 ──
            var host = marker != null ? marker : root.gameObject;
            var orient = Ensure<R0926Orientation>(host);
            var slots = new List<R0926Orientation.Slot>();

            void Add(GameObject go, R0926Orientation.Snap land, float fitHeight = 0f,
                     R0926SafeInset.Edge pEdge = R0926SafeInset.Edge.Bottom, R0926SafeInset.Edge lEdge = R0926SafeInset.Edge.Bottom)
            {
                if (go == null) return;
                var rt = RT(go);
                slots.Add(new R0926Orientation.Slot
                {
                    target = rt,
                    portrait = SnapOf(rt),
                    landscape = land,
                    fitHeight = fitHeight,
                    inset = go.GetComponent<R0926SafeInset>(),
                    portraitEdge = pEdge,
                    landscapeEdge = lEdge,
                });
            }

            R0926Orientation.Snap Rail() => S(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-RailMargin, 0), new Vector2(RailW, RailH));
            R0926Orientation.Snap RailSlot(float px) => S(new Vector2(0.5f, LandSlotY(px)), new Vector2(0.5f, LandSlotY(px)), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RailW, 220));
            R0926Orientation.Snap LeftCard() => S(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(LandCardW, -80));
            R0926Orientation.Snap BottomSheet(float h, float y) => S(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, y), new Vector2(LandSheetW, h));

            // 도크 + 버튼
            Add(Find(root, "Dock0926"), Rail(), 0, R0926SafeInset.Edge.Bottom, R0926SafeInset.Edge.Right);
            Add(Find(root, "List_Button"), RailSlot(SlotList));
            Add(Find(root, "PlusButton"), RailSlot(SlotAdd));
            Add(Find(root, "Message_Button"), RailSlot(SlotMsg));
            Add(Find(root, "MiniProfile"), RailSlot(SlotProfile));

            // 패널별 도크 거울 + 닫기 버튼
            foreach (var (panelName, closeName, slot) in new[]
            {
                ("ListPanel", "XButton_List", SlotList),
                ("MessagePanel", "CloseButton", SlotMsg),
                ("ChatRoomPanel", "CloseButton", SlotMsg),
                ("UploadPage", "XButton_Upload", SlotAdd),
                ("FullProfilePanel", "ProfileToggle0926", SlotProfile),
            })
            {
                var panel = Find(root, panelName);
                var mirror = panel != null ? panel.transform.Find("DockMirror0926") : null;
                if (mirror == null) continue;
                Add(mirror.gameObject, Rail(), 0, R0926SafeInset.Edge.Bottom, R0926SafeInset.Edge.Right);
                var close = mirror.Find(closeName);
                if (close != null) Add(close.gameObject, RailSlot(slot));
            }

            // 위치 칩 → 왼쪽 아래
            Add(Find(root, "LocationManagerPanel"),
                S(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 90), new Vector2(600, 100)),
                0, R0926SafeInset.Edge.Bottom, R0926SafeInset.Edge.Left);

            // 카드 → 왼쪽 열
            Add(Find(root, "Sheet0926"), LeftCard(), 0, R0926SafeInset.Edge.Bottom, R0926SafeInset.Edge.Left);
            foreach (var n in new[] { "MessagePanel", "ChatRoomPanel" })
            {
                var p = Find(root, n);
                var bg = p != null ? (p.transform.Find("Background") ?? p.transform.Find("Clip0926/Background")) : null;
                if (bg != null) Add(bg.gameObject, LeftCard(), 0, R0926SafeInset.Edge.Bottom, R0926SafeInset.Edge.Left);
            }
            // 0930 도크 윗선 틀 — 가로에서는 도크가 오른쪽 막대라 아래를 비워 둘 필요가 없다
            foreach (var n in new[] { "ListPanel", "MessagePanel", "ChatRoomPanel", "UploadPage" })
            {
                var p = Find(root, n);
                var clip = p != null ? p.transform.Find("Clip0926") : null;
                if (clip != null) Add(clip.gameObject, S(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(0, -40)));
            }
            // 프로필 카드 — 가운데 그대로, 높이에 맞춰 축소
            var prof = Find(root, "FullProfilePanel");
            var content = prof != null ? prof.transform.Find("Content") : null;
            if (content != null) Add(content.gameObject, SnapOf(RT(content.gameObject)), 1800f);

            // 아래 시트들 → 가운데 폭 제한
            var more = Find(root, "MoreSheet0926");
            if (more != null) Add(more, BottomSheet(450, 40 + 150 + 16));
            var moreCancel = Find(root, "MoreCancel0926");
            if (moreCancel != null) Add(moreCancel, BottomSheet(150, 40));
            var rep = Find(root, "ReportSheet0926");
            if (rep != null)
            {
                var card = rep.transform.Find("Card0926");
                var cancel = rep.transform.Find("Cancel0926");
                if (card != null) Add(card.gameObject, BottomSheet(RT(card.gameObject).sizeDelta.y, 40 + 150 + 16));
                if (cancel != null) Add(cancel.gameObject, BottomSheet(150, 40));
            }
            var askSheet = Find(root, "AskAISheet0926");
            if (askSheet != null)
            {
                var card = askSheet.transform.Find("Card0926");
                var cancel = askSheet.transform.Find("Cancel0926");
                if (card != null) Add(card.gameObject, BottomSheet(RT(card.gameObject).sizeDelta.y, 40 + 150 + 16));
                if (cancel != null) Add(cancel.gameObject, BottomSheet(150, 40));
            }
            foreach (var n in new[] { "PhotoSourceDialog", "ContinueCaptureDialog" })
            {
                var d = Find(root, n);
                var cc = d != null ? Find(d.transform, "ContentCard") : null;
                if (cc != null) Add(cc, BottomSheet(RT(cc).sizeDelta.y, 40));
            }
            var net = Find(root, "NetBanner0926");
            if (net != null) Add(net, S(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(LandSheetW, 220)), 0,
                R0926SafeInset.Edge.Top, R0926SafeInset.Edge.Top);

            // 장소 상세(FullScreenPanel) — 가로: 사진은 왼쪽 30% 자리, 글·버튼은 오른쪽 열 (도크 막대와 겹치지 않게)
            // 사진은 anchoredPosition(0,-200)을 DoubleTap3D 가 처음 열 때 기억해 넘김 애니메이션에 쓰므로 '앵커'만 옮긴다.
            // 앵커 y 0.637 = 가운데 + 200/1458 → 사진 중심이 화면 가운데.
            var full = Find(root, "FullScreenPanel");
            if (full != null)
            {
                const float colX = 0.72f;
                var photoSnap = S(new Vector2(0.3f, 0.637f), new Vector2(0.3f, 0.637f), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(1000, 1300));
                Add(Find(full.transform, "FullScreenImage"), photoSnap);
                Add(Find(full.transform, "PlaceInfoTextPanel"), photoSnap);
                Add(Find(full.transform, "Button_Previous"), S(new Vector2(0.3f, 0.5f), new Vector2(0.3f, 0.5f), new Vector2(1, 0.5f), new Vector2(-510, 0), new Vector2(150, 150)));
                Add(Find(full.transform, "Button_Next"), S(new Vector2(0.3f, 0.5f), new Vector2(0.3f, 0.5f), new Vector2(0, 0.5f), new Vector2(510, 0), new Vector2(150, 150)));
                Add(Find(full.transform, "TopUIPanel"), S(new Vector2(colX, 1), new Vector2(colX, 1), new Vector2(0.5f, 1), new Vector2(-100, 230), new Vector2(1400, 1000)));
                Add(Find(full.transform, "PlaceName"), S(new Vector2(colX, 1), new Vector2(colX, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(1000, 200)));
                Add(Find(full.transform, "Roman0926"), S(new Vector2(colX, 1), new Vector2(colX, 1), new Vector2(0.5f, 1), new Vector2(0, -480), new Vector2(1000, 60)));
                var byText = S(new Vector2(colX, 1), new Vector2(colX, 1), new Vector2(0.5f, 1), new Vector2(0, -540), new Vector2(1000, 100));
                Add(Find(full.transform, "CreatedByText"), byText);
                Add(Find(full.transform, "TourAPI_Description"), byText);
                Add(Find(full.transform, "CommentPreviewPanel"), S(new Vector2(0.56f, 0), new Vector2(0.88f, 0), new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(0, 200)));
                Add(Find(full.transform, "Button_Close"), S(new Vector2(colX, 0), new Vector2(colX, 0), new Vector2(0.5f, 0), new Vector2(0, 420), new Vector2(150, 150)));

                // 가로 배치가 생겼으니 '세로로 돌려 주세요' 안내는 뺀다 (스마트 안경은 돌릴 수가 없다)
                var ph = full.GetComponent<R0926PortraitHint>();
                if (ph != null) Object.DestroyImmediate(ph);
                var hintObj = full.transform.Find("PortraitHint0926");
                if (hintObj != null) Object.DestroyImmediate(hintObj.gameObject);
            }

            var so = new SerializedObject(orient);
            var arr = so.FindProperty("slots");
            arr.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var sl = slots[i];
                e.FindPropertyRelative("target").objectReferenceValue = sl.target;
                WriteSnap(e.FindPropertyRelative("portrait"), sl.portrait);
                WriteSnap(e.FindPropertyRelative("landscape"), sl.landscape);
                e.FindPropertyRelative("fitHeight").floatValue = sl.fitHeight;
                e.FindPropertyRelative("inset").objectReferenceValue = sl.inset;
                e.FindPropertyRelative("portraitEdge").enumValueIndex = (int)sl.portraitEdge;
                e.FindPropertyRelative("landscapeEdge").enumValueIndex = (int)sl.landscapeEdge;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 가로에서 쓰기 어려운 화면 — '세로로 돌려 주세요'
            foreach (var n in new[] { "UploadPage", "Fixpage" })
                PortraitHint(Find(root, n));

            log.Add($"fade + landscape ok (slots {slots.Count})");
        }

        private static void PortraitHint(GameObject panel)
        {
            if (panel == null) return;
            var hint = FindOrCreate(panel.transform, "PortraitHint0926");
            hint.transform.SetAsLastSibling();
            Img(hint, null, new Color(0, 0, 0, 0.72f), Image.Type.Simple).raycastTarget = true;
            SetRect(RT(hint), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            OnTop(hint);
            var card = FindOrCreate(hint.transform, "Card0926");
            Img(card, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 0.98f), Image.Type.Sliced, 64f / 56f).raycastTarget = false;
            SetRect(RT(card), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 360));
            var t = FindOrCreate(card.transform, "Label0926");
            Txt(t, "이 화면은 세로에서 쓰기 편해요\n휴대폰을 세로로 돌려 주세요", 44, Ink, TextAnchor.MiddleCenter, FontStyle.Bold).lineSpacing = 1.25f;
            var lrt = RT(t); lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(60, 40); lrt.offsetMax = new Vector2(-60, -40);
            Loc(t, "이 화면은 세로에서 쓰기 편해요\n휴대폰을 세로로 돌려 주세요",
                "This screen works best upright\nPlease rotate your phone",
                "この画面は縦向きが使いやすいです\nスマホを縦にしてください",
                "此页面竖屏使用更方便\n请将手机竖过来",
                "Esta pantalla funciona mejor en vertical\nGira el teléfono");
            hint.SetActive(false);
            var ph = Ensure<R0926PortraitHint>(panel);
            var so = new SerializedObject(ph);
            so.FindProperty("hint").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static R0926Orientation.Snap SnapOf(RectTransform rt) => new R0926Orientation.Snap
        {
            anchorMin = rt.anchorMin, anchorMax = rt.anchorMax, pivot = rt.pivot,
            anchoredPosition = rt.anchoredPosition, sizeDelta = rt.sizeDelta,
        };

        private static R0926Orientation.Snap S(Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size) => new R0926Orientation.Snap
        {
            anchorMin = aMin, anchorMax = aMax, pivot = pivot, anchoredPosition = pos, sizeDelta = size,
        };

        private static void WriteSnap(SerializedProperty p, R0926Orientation.Snap s)
        {
            p.FindPropertyRelative("anchorMin").vector2Value = s.anchorMin;
            p.FindPropertyRelative("anchorMax").vector2Value = s.anchorMax;
            p.FindPropertyRelative("pivot").vector2Value = s.pivot;
            p.FindPropertyRelative("anchoredPosition").vector2Value = s.anchoredPosition;
            p.FindPropertyRelative("sizeDelta").vector2Value = s.sizeDelta;
        }
    }
}
