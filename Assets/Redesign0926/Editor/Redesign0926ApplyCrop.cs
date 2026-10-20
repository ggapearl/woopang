using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 대표 사진 자르기 화면(PhotoCrop0926) · 처리 중 카드(Busy0926) — 10-20 대표님 「메인사진 올릴 때 '업로드 중..' '크롭 중..' 안내나 로딩스피너 ·
    /// 사진 크롭이 온전하지 않은데 버튼이랑 효과음 잘 정리해서」.
    ///  · 자르기: 검은 바탕 · 가운데 정사각형 틀(밖은 어둡게 · 만지는 동안 3x3 줄) · 위 제목/안내 · 아래 한 줄 큰 세 칸 '취소 / 회전 / 완료'
    ///    (사진 고르기 시트와 같은 칸 · 하단 메뉴와 같은 물방울 소리 · 취소는 뒤로가기로도). 세로·가로·안전 영역 배치는 R0926PhotoCrop 이 한다
    ///  · 처리 중: 화면을 어둡게 덮고 가운데 카드 — 도는 분홍 원호 + 한 줄(자르는 중… · 업로드 중… N% · 보내는 중… N%) + 막대
    /// 예전 ImageCropper(yasirkula 패키지 · Resources/ImageCropper 프리팹)는 더 쓰지 않는다 (지우지는 않았다)
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static readonly Color CropBg = new Color(0.035f, 0.04f, 0.045f, 1f);

        private static void ApplyCrop(Transform root, List<string> log)
        {
            var busy = BuildBusy(root);
            var crop = BuildCrop(root, busy, log);
            WireCrop<CubeUploadManager>(root, crop, busy, log);
            WireCrop<CubeDataFixManager>(root, crop, busy, log);
            WireCrop<ModelUploadManager>(root, null, busy, log);   // 3D 모델은 사진을 자르지 않는다 — 올리는 중 카드만
            log.Add("crop ok");
        }

        private static void WireCrop<T>(Transform root, R0926PhotoCrop crop, R0926Busy busy, List<string> log) where T : Component
        {
            var m = FindInScene<T>(root.gameObject);
            if (m == null) { log.Add("자르기: " + typeof(T).Name + " 없음"); return; }
            var so = new SerializedObject(m);
            if (crop != null) so.FindProperty("photoCrop").objectReferenceValue = crop;
            so.FindProperty("busy").objectReferenceValue = busy;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 처리 중 카드 ─────────────────────────────────────
        private static R0926Busy BuildBusy(Transform root)
        {
            var go = FindOrCreate(root, "Busy0926");
            Stretch(RT(go));
            var cv = Ensure<Canvas>(go);
            cv.overrideSorting = true;
            cv.sortingOrder = 30010;   // 자르기 화면(30000) 위
            Ensure<GraphicRaycaster>(go);
            var group = Ensure<CanvasGroup>(go);

            // 뒤를 덮어 그동안 다른 곳이 안 눌린다 — 뒤로가기도 여기서 삼킨다(올리는 도중 창이 닫혀 요청만 남지 않게)
            var dim = FindOrCreate(go.transform, "Dim0926");
            Stretch(RT(dim));
            Img(dim, null, new Color(0f, 0f, 0f, 0.55f), Image.Type.Simple).raycastTarget = true;
            var db = Ensure<Button>(dim);
            db.transition = Selectable.Transition.None;
            db.navigation = new Navigation { mode = Navigation.Mode.None };
            ResetListeners(db);
            Ensure<ClickButtonOnBack>(dim);

            var card = FindOrCreate(go.transform, "Card0926");
            Img(card, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = false;
            SetRect(RT(card), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 400));

            var spin = PillPart(card.transform, "Spin0926", "r0926_arc", Pink2, 120, Image.Type.Simple);
            RT(spin.gameObject).anchoredPosition = new Vector2(0, 56);

            var lb = FindOrCreate(card.transform, "Label0926");
            var label = Txt(lb, "", 46, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetRect(RT(lb), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(-48, 70));

            // 올리기만 — 보낸 비율 막대 (자르기엔 없음)
            var bar = FindOrCreate(card.transform, "Bar0926");
            Img(bar, Spr("r0926_pill"), new Color(1, 1, 1, 0.1f), Image.Type.Sliced, 64f / 6f).raycastTarget = false;
            SetRect(RT(bar), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -142), new Vector2(520, 12));
            var fill = FindOrCreate(bar.transform, "Fill0926");
            Img(fill, Spr("r0926_pill"), Pink, Image.Type.Sliced, 64f / 6f).raycastTarget = false;
            var frt = RT(fill);
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0, 1); frt.pivot = new Vector2(0, 0.5f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(frt);
            bar.SetActive(false);

            var busy = Ensure<R0926Busy>(go);
            var so = new SerializedObject(busy);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("spin").objectReferenceValue = RT(spin.gameObject);
            so.FindProperty("bar").objectReferenceValue = bar;
            so.FindProperty("barFill").objectReferenceValue = frt;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);
            return busy;
        }

        // ── 자르기 화면 ───────────────────────────────────────
        private static R0926PhotoCrop BuildCrop(Transform root, R0926Busy busy, List<string> log)
        {
            var go = FindOrCreate(root, "PhotoCrop0926");
            Stretch(RT(go));
            var cv = Ensure<Canvas>(go);
            cv.overrideSorting = true;
            cv.sortingOrder = 30000;   // 예전 자르기 화면과 같은 자리 — 창·도크·토스트(9500) 위
            Ensure<GraphicRaycaster>(go);

            // 바탕이 손가락을 받는다 → 위의 R0926PhotoCrop 이 끌기·두 번 톡·휠을 처리
            var bg = FindOrCreate(go.transform, "Bg0926");
            Stretch(RT(bg));
            Img(bg, null, CropBg, Image.Type.Simple).raycastTarget = true;

            var frame = FindOrCreate(go.transform, "Frame0926");
            SetRect(RT(frame), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(720, 1600), new Vector2(1300, 1300));
            var ph = FindOrCreate(frame.transform, "Photo0926");
            ph.transform.SetSiblingIndex(0);
            var photo = Ensure<RawImage>(ph);
            photo.color = Color.white;
            photo.raycastTarget = false;
            EditorUtility.SetDirty(photo);
            SetRect(RT(ph), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 1300));

            // 틀 밖은 어둡게 (사진은 틀보다 크게 깔린다)
            const float far = 5000f;
            var shade = new Color(0f, 0f, 0f, 0.72f);
            CropPart(frame.transform, "DimT0926", shade, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-far, 0), new Vector2(far, far));
            CropPart(frame.transform, "DimB0926", shade, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-far, -far), new Vector2(far, 0));
            CropPart(frame.transform, "DimL0926", shade, new Vector2(0, 0), new Vector2(0, 1), new Vector2(-far, 0), Vector2.zero);
            CropPart(frame.transform, "DimR0926", shade, new Vector2(1, 0), new Vector2(1, 1), Vector2.zero, new Vector2(far, 0));

            // 3x3 줄 — 만지는 동안만 (R0926PhotoCrop 이 CanvasGroup 을 올리고 내린다)
            var gridGo = FindOrCreate(frame.transform, "Grid0926");
            Stretch(RT(gridGo));
            var grid = Ensure<CanvasGroup>(gridGo);
            grid.alpha = 0f; grid.blocksRaycasts = false; grid.interactable = false;
            var gl = new Color(1f, 1f, 1f, 0.35f);
            for (int i = 1; i <= 2; i++)
            {
                float k = i / 3f;
                CropPart(gridGo.transform, "V" + i, gl, new Vector2(k, 0), new Vector2(k, 1), new Vector2(-1.5f, 0), new Vector2(1.5f, 0));
                CropPart(gridGo.transform, "H" + i, gl, new Vector2(0, k), new Vector2(1, k), new Vector2(0, -1.5f), new Vector2(0, 1.5f));
            }

            // 틀 테두리 + 네 모서리 ㄱ자
            var edge = Color.white;
            const float line = 4f, cornerLen = 72f, cornerW = 10f;
            CropPart(frame.transform, "EdgeT0926", new Color(1, 1, 1, 0.85f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -line), Vector2.zero);
            CropPart(frame.transform, "EdgeB0926", new Color(1, 1, 1, 0.85f), new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, line));
            CropPart(frame.transform, "EdgeL0926", new Color(1, 1, 1, 0.85f), new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(line, 0));
            CropPart(frame.transform, "EdgeR0926", new Color(1, 1, 1, 0.85f), new Vector2(1, 0), new Vector2(1, 1), new Vector2(-line, 0), Vector2.zero);
            foreach (var (nm, c) in new[] { ("TL", new Vector2(0, 1)), ("TR", new Vector2(1, 1)), ("BL", new Vector2(0, 0)), ("BR", new Vector2(1, 0)) })
            {
                // 기준점을 모서리에 두어 틀 안쪽으로 뻗는다
                SetRect(CropPart(frame.transform, "Corner" + nm + "H", edge, c, c, Vector2.zero, Vector2.zero), c, c, c, Vector2.zero, new Vector2(cornerLen, cornerW));
                SetRect(CropPart(frame.transform, "Corner" + nm + "V", edge, c, c, Vector2.zero, Vector2.zero), c, c, c, Vector2.zero, new Vector2(cornerW, cornerLen));
            }

            // 위 — 제목 · 안내
            var head = FindOrCreate(go.transform, "Head0926");
            SetRect(RT(head), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(-120, 230));
            var title = FindOrCreate(head.transform, "Title0926");
            Txt(title, "메인 사진", 60, Ink, TextAnchor.MiddleCenter, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            SetRect(RT(title), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 84));
            Loc(title, "메인 사진", "Main photo", "メイン写真", "主照片", "Foto principal");
            var hint = FindOrCreate(head.transform, "Hint0926");
            var ht = Txt(hint, "사진을 끌어 옮기고 두 손가락으로 크기를 맞춰 주세요", 42, Soft, TextAnchor.UpperCenter, FontStyle.Normal);
            ht.horizontalOverflow = HorizontalWrapMode.Wrap; ht.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(RT(hint), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(0, 120));
            Loc(hint, "사진을 끌어 옮기고 두 손가락으로 크기를 맞춰 주세요", "Drag to move · pinch to zoom",
                "ドラッグで移動・2本指で拡大縮小", "拖动移动，双指缩放", "Arrastra para mover · pellizca para ampliar");

            // 아래 — 한 줄 큰 세 칸 (사진 고르기 시트와 같은 카드 · 칸)
            var bar = FindOrCreate(go.transform, "Bar0926");
            Img(bar, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(bar), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(-CardSide * 2, PhotoTileH + 48));
            var row = FindOrCreate(bar.transform, "Row0926");
            var rrt = RT(row);
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.offsetMin = new Vector2(24, 24); rrt.offsetMax = new Vector2(-24, -24);
            EditorUtility.SetDirty(rrt);

            var crop = Ensure<R0926PhotoCrop>(go);
            var dockList = Find(root, "Dock0926")?.transform.Find("List_Button");
            var dockFw = dockList != null ? dockList.GetComponent<UITouchForwarder>() : null;
            var dockClip = dockFw != null ? new SerializedObject(dockFw).FindProperty("customSound").objectReferenceValue : null;
            if (dockClip == null) log.Add("자르기: 도크 버튼 소리 못 찾음 — 기본 소리");

            var quiet = Color.Lerp(Card, Color.white, 0.035f);
            var plain = Color.Lerp(Card, Color.white, 0.08f);
            var cancel = CropTile(row.transform, "Cancel0926", 0, "r0926_i_close", quiet, Muted, CancelWords, dockClip, crop.Cancel);
            var rotate = CropTile(row.transform, "Rotate0926", 1, "r0926_i_reset", plain, Ink, new[] { "회전", "Rotate", "回転", "旋转", "Girar" }, dockClip, crop.Rotate);
            var done = CropTile(row.transform, "Done0926", 2, "r0926_i_check", Pink, Dark, new[] { "완료", "Done", "完了", "完成", "Listo" }, dockClip, crop.Done);
            Ensure<ClickButtonOnBack>(cancel.gameObject);   // 뒤로가기 = 취소

            var so = new SerializedObject(crop);
            so.FindProperty("frame").objectReferenceValue = RT(frame);
            so.FindProperty("photo").objectReferenceValue = photo;
            so.FindProperty("grid").objectReferenceValue = grid;
            so.FindProperty("header").objectReferenceValue = RT(head);
            so.FindProperty("bar").objectReferenceValue = RT(bar);
            SetArray(so.FindProperty("tiles"), new Object[] { cancel, rotate, done });
            so.FindProperty("busy").objectReferenceValue = busy;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);
            return crop;
        }

        private static RectTransform CropPart(Transform parent, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = FindOrCreate(parent, name);
            Img(go, null, c, Image.Type.Simple).raycastTarget = false;
            var rt = RT(go);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = oMin; rt.offsetMax = oMax;
            EditorUtility.SetDirty(rt);
            return rt;
        }

        // 사진 고르기 시트의 칸과 같은 모양 — 둥근 칸 + 큰 그림 + 아래 굵은 글자. 누르면 살짝 어두워지고 물방울 소리
        private static RectTransform CropTile(Transform row, string name, int index, string icon, Color bg, Color fg, string[] words,
            Object clip, UnityEngine.Events.UnityAction action)
        {
            var go = FindOrCreate(row, name);
            go.transform.SetSiblingIndex(index);
            var rt = RT(go);
            rt.anchorMin = new Vector2(index / 3f, 0); rt.anchorMax = new Vector2((index + 1) / 3f, 1); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(index * 20f / 3f, 0); rt.offsetMax = new Vector2(-(2 - index) * 20f / 3f, 0);   // R0926PhotoCrop.Layout 과 같은 간격
            EditorUtility.SetDirty(rt);
            var img = Img(go, Spr("r0926_pill"), bg, Image.Type.Sliced, 64f / 48f);
            img.raycastTarget = true;

            var b = Ensure<Button>(go);
            b.transition = Selectable.Transition.ColorTint;
            b.targetGraphic = img;
            var cb = ColorBlock.defaultColorBlock;
            cb.highlightedColor = Color.white; cb.selectedColor = Color.white;
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            cb.fadeDuration = 0.06f;
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            ResetListeners(b);
            UnityEventTools.AddPersistentListener(b.onClick, action);
            EditorUtility.SetDirty(b);

            var ic = FindOrCreate(go.transform, "Icon0926");
            Img(ic, Spr(icon), fg, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 44), new Vector2(108, 108));
            var lb = FindOrCreate(go.transform, "Label0926");
            Txt(lb, words[0], 50, fg, TextAnchor.MiddleCenter, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            SetRect(RT(lb), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(-24, 72));
            Loc(lb, words[0], words[1], words[2], words[3], words[4]);

            var fw = Ensure<UITouchForwarder>(go);
            if (clip != null)
            {
                var fso = new SerializedObject(fw);
                fso.FindProperty("customSound").objectReferenceValue = clip;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }
            return rt;
        }
    }
}
