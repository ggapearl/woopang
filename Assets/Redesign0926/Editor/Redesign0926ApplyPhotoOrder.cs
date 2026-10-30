using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 추가 사진 칸 — 두 줄로 옆으로 밀리는 줄(10-27) · 사진을 꾹 눌러 끌어 순서 바꾸기(R0926PhotoReorder) · 칸마다 x (10-26).
    /// 장소 추가 · 3D 모델 추가(Apply3 UploadCard) · 장소 정보 수정(Apply10 FixPageOwner) 이 같이 부른다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        /// <summary>
        /// 추가 사진 줄 — 대표 사진 오른쪽부터 카드 오른쪽 끝까지. 두 줄을 위에서 아래로 채우고(1 3 5 7 9 / 2 4 6 8 10) 옆으로 넘친다 —
        /// 다음 칸이 살짝 보이고 밀어서 본다(R0926PhotoStrip). 칸은 대표 사진 높이의 반 그대로 (예전 R0926FitGrid 는 열 장이면 88 까지 줄여 세 줄이 됐다).
        /// '+' 는 그리드 안 맨 끝 칸 — 사진이 없으면 1번, 한 장이면 2번 자리. '초기화' 와 그 '+' 가 켜던 'Loading images..' 는 없앤다.
        /// 10-27 대표님 「x버튼이 너무 작은데 … 서브사진들의 사진을 키워야 (혹은 옆으로 드래그 해서 스크롤로) … 두줄만 … '초기화' 버튼은 삭제 …
        /// 'Loading images..' 가 계속 발생 … 사진을 실제로 불러올 때만 loading spinner 가 해당 위치에」. 다시 불러도 같은 모양이 된다
        /// </summary>
        /// <returns>추가 사진 그리드 (SubPhotoContainer)</returns>
        private static Transform SubPhotoStrip(Transform panel, float x0, float y, float tile, List<string> log)
        {
            var strip = FindOrCreate(panel, "SubStrip0926");
            var subs = panel.Find("SubPhotoContainer") ?? strip.transform.Find("SubPhotoContainer");
            if (subs == null) { log.Add(panel.parent.name + ": 추가 사진 그리드 없음"); return null; }
            float cell = Mathf.Floor((tile - 16f) / 2f);

            // 줄 — 옆으로 밀리는 창 (칸 사이 틈에서 밀어도 받게 투명 바탕 · 밖으로 나간 칸은 잘라 낸다)
            if (subs.parent == panel) strip.transform.SetSiblingIndex(subs.GetSiblingIndex());
            strip.transform.localScale = Vector3.one;
            var vrt = RT(strip);
            vrt.anchorMin = new Vector2(0, 1); vrt.anchorMax = new Vector2(1, 1); vrt.pivot = new Vector2(0, 1);
            vrt.offsetMin = new Vector2(x0, y - tile); vrt.offsetMax = new Vector2(0, y);
            Img(strip, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            Ensure<RectMask2D>(strip);

            subs.SetParent(strip.transform, false);
            subs.localScale = Vector3.one;
            var crt = RT(subs.gameObject);
            crt.anchorMin = Vector2.zero; crt.anchorMax = new Vector2(0, 1); crt.pivot = new Vector2(0, 1);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = Vector2.zero;
            var grid = subs.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(16, 16);
            grid.padding = new RectOffset(0, (int)UP, 0, 0);   // 끝까지 밀면 마지막 칸이 카드 오른쪽 여백에 맞는다
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Vertical;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 2;
            EditorUtility.SetDirty(grid);
            var fit = Ensure<ContentSizeFitter>(subs.gameObject);
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(subs.gameObject);   // 지운 R0926FitGrid

            var add = panel.Find("SubPhoto_Button") ?? subs.Find("SubPhoto_Button");
            if (add != null)
            {
                Tile(add.gameObject, 0, 0, cell, cell, "r0926_i_plus", 76);
                add.SetParent(subs, false);
                add.SetAsLastSibling();
                // 누르면 '초기화'·'Loading images..' 를 켜던 장면 연결 — 둘 다 없앤다
                var b = add.GetComponent<Button>();
                for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    var t = b.onClick.GetPersistentTarget(i);
                    if (t == null || t.name == "SubPhoto_Reset" || t.name == "SubPhoto_LoadingText") UnityEventTools.RemovePersistentListener(b.onClick, i);
                }
            }
            else log.Add(panel.parent.name + ": 추가 사진 '+' 없음");
            foreach (var n in new[] { "SubPhoto_Reset", "SubPhoto_LoadingText" })
            {
                var old = panel.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var sr = Ensure<R0926PhotoStrip>(strip);
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.inertia = true;
            sr.viewport = vrt;
            sr.content = crt;
            var so = new SerializedObject(sr);
            so.FindProperty("addTile").objectReferenceValue = add;
            so.FindProperty("maxTiles").intValue = 10;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add(panel.parent.name + ": 추가 사진 두 줄 · 옆으로 밀기 · '+' 다음 칸 (칸 " + cell + ") ok");
            return subs;
        }

        /// <param name="withX">칸마다 넣을 x 본도 여기서 (정보 수정은 FixPageOwner 가 만든다)</param>
        private static void SubPhotoTools(Transform panel, Transform subs, MonoBehaviour manager, bool withX, List<string> log)
        {
            if (panel == null || subs == null || manager == null) { log.Add("추가 사진 옮기기 — 칸·매니저 없음"); return; }
            var reorder = Ensure<R0926PhotoReorder>(subs.gameObject);

            // 들고 있는 사진 — 카드 맨 위에 떠서 손가락을 따라간다 (평소엔 꺼 둔다)
            var g = FindOrCreate(panel, "PhotoGhost0926");
            g.transform.localScale = Vector3.one;
            g.transform.SetAsLastSibling();
            var img = Img(g, null, Color.white, Image.Type.Simple);
            img.raycastTarget = false;
            img.preserveAspect = true;
            SetRect(RT(g), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(188, 188));
            var shadow = Ensure<Shadow>(g);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -14f);
            EditorUtility.SetDirty(shadow);
            g.SetActive(false);

            var rso = new SerializedObject(reorder);
            rso.FindProperty("ghost").objectReferenceValue = img;
            rso.ApplyModifiedPropertiesWithoutUndo();

            var mso = new SerializedObject(manager);
            var p = mso.FindProperty("subReorder");
            if (p == null) { log.Add(manager.GetType().Name + ": subReorder 칸 없음"); return; }
            p.objectReferenceValue = reorder;
            if (withX)
            {
                var x = SubRemoveX(panel, "SubRemoveX0926_Template");
                x.gameObject.SetActive(false);
                var xp = mso.FindProperty("subPhotoRemoveTemplate");
                if (xp == null) log.Add(manager.GetType().Name + ": subPhotoRemoveTemplate 칸 없음");
                else xp.objectReferenceValue = x;
            }
            mso.ApplyModifiedPropertiesWithoutUndo();
            log.Add(manager.GetType().Name + ": 추가 사진 꾹 눌러 옮기기" + (withX ? " · 칸마다 x" : "") + " ok");
        }

        // 추가 사진 칸의 x — 칸이 더는 줄지 않으니(두 줄 · 옆으로 밀기) 크기를 고정해 키운다 (10-27 대표님 「x버튼이 너무 작은데 … 거의 터치가 불가능」).
        // 예전엔 칸 크기를 따라가(오른쪽 위 1/4) 열 장이면 점이 22 까지 작아졌다. 나머지 자리는 꾹 눌러 옮기는 곳
        private static Button SubRemoveX(Transform parent, string name) => RemoveX(parent, name, 116, 72, 10);
    }
}
