using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 추가 사진 칸 (10-26) — 사진을 꾹 눌러 끌어 순서 바꾸기(R0926PhotoReorder) · 칸마다 x.
    /// 장소 추가 · 3D 모델 추가(Apply3 UploadCard) · 장소 정보 수정(Apply10 FixPageOwner) 이 같이 부른다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
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

        // 추가 사진 칸의 x — 칸 크기를 따라간다(오른쪽 위 1/4 칸). 사진이 열 장이면 칸이 88 까지 줄어드는데
        // 예전 x(누름 108 고정)는 칸을 통째로 덮어 사진 어디를 눌러도 빠졌다 — 이제 나머지는 꾹 눌러 옮기는 자리 (10-26)
        private static Button SubRemoveX(Transform parent, string name)
        {
            var b = RemoveX(parent, name, 108, 46, 6);
            AnchorBox(RT(b.gameObject), new Vector2(0.5f, 0.5f), Vector2.one);
            var dot = b.transform.Find("Dot0926");
            AnchorBox(RT(dot.gameObject), new Vector2(0.44f, 0.44f), new Vector2(0.94f, 0.94f));
            AnchorBox(RT(dot.Find("Icon0926").gameObject), new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f));
            return b;
        }

        private static void AnchorBox(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(rt);
        }
    }
}
