using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 장소 상세 위 버튼 줄에 「길안내」 (11-16 대표님 「더블탭하고 나서 발생하는 화면에 메뉴가 여러개 있잖아? 그때 거기에 화살표 버튼을 만들어서 안내하도록」).
    /// 번역과 「···」 사이 — 연한 분홍(40%) 동그라미에 흰 길안내 화살표, 「···」는 한 칸 오른쪽으로.
    /// 11-17 대표님 F1 「핑크색이 진하면 안되고 투명도 40정도로 해서 우측에 있는 ... 과 비슷하게 동그라미가 있도록 해줘. (동그라미 사이즈도 동일하게)
    /// 그리고 이거 지금 동그라미 중앙이 안맞거든?」 — 「···」처럼 비치는 동그라미(같은 그림 · 같은 크기) · 화살표 모양을 동그라미 가운데에.
    /// 줄이 한 칸 길어진 만큼 줄 전체(TopUIPanel)를 왼쪽으로 옮겨 다 보일 때 화면 가운데 (DetailRowX — 세로 ApplyDetailLift · 가로 ApplyFadeAndLandscape).
    /// 보이기 · 숨기기 · 누르기는 R0926DetailGuide.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        // 줄 가운데 맞춤 — 맨 왼쪽(애견 그림 -333 - 90)부터 맨 오른쪽(「···」 690 + 65)까지의 가운데가 166
        private const float DetailRowX = -166f;
        private const float DetailGuideX = 520f, DetailMoreX = 690f, DetailBtnY = -360f, DetailBtnSize = 130f;

        private static void ApplyDetailGuide(Transform root, List<string> log)
        {
            var full = Find(root, "FullScreenPanel");
            var top = full != null ? Find(full.transform, "TopUIPanel") : null;
            if (top == null) { log.Add("상세 길안내: TopUIPanel 없음"); return; }
            var c = new Vector2(0.5f, 1f);

            var go = FindOrCreate(top.transform, "GuideButton0926");
            SetRect(RT(go), c, c, c, new Vector2(DetailGuideX, DetailBtnY), new Vector2(DetailBtnSize, DetailBtnSize));
            var img = Img(go, Spr("r0926_circle"), new Color(Pink.r, Pink.g, Pink.b, 0.4f), Image.Type.Simple);   // 「···」(어두운 반투명)처럼 비치게 — 분홍 40%
            img.raycastTarget = true;
            var btn = Ensure<Button>(go);
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            EditorUtility.SetDirty(btn);

            // 화살표 — 위를 가리키는 그림을 오른쪽 위로 45° (길안내 표시). 돌리면 꼬리 두 끝이 왼쪽 · 아래로 뻗어 모양 가운데가 그림 칸 가운데보다
            // 왼쪽 아래로 간다(테두리 가운데 약 8.4 · 무게 가운데 약 2.7) — 두 가운데의 중간(5.5)만큼 오른쪽 위로 (전엔 거꾸로 왼쪽 아래로 3 더 내려 어긋나 보였다)
            var icon = FindOrCreate(go.transform, "Icon0926");
            var ii = Img(icon, Spr("r0926_i_navarrow"), Color.white, Image.Type.Simple);
            ii.raycastTarget = false;
            ii.preserveAspect = true;
            SetRect(RT(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(5.5f, 5.5f), new Vector2(72f, 72f));
            RT(icon).localEulerAngles = new Vector3(0f, 0f, -45f);
            EditorUtility.SetDirty(RT(icon));

            var cg = Ensure<CanvasGroup>(go);
            cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;
            EditorUtility.SetDirty(cg);
            var dg = Ensure<R0926DetailGuide>(go);
            var so = new SerializedObject(dg);
            so.FindProperty("button").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 「···」(와 펼쳤을 때의 닫기) 한 칸 오른쪽 — 길안내는 그 앞
            int moved = 0;
            GameObject fix = null;
            foreach (Transform ch in top.transform)
            {
                if (ch.name != "FixButton" && ch.name != "FixButton_Opposite") continue;
                RT(ch.gameObject).anchoredPosition = new Vector2(DetailMoreX, DetailBtnY);
                EditorUtility.SetDirty(ch);
                moved++;
                if (ch.name == "FixButton") fix = ch.gameObject;
            }
            if (fix != null && go.transform.GetSiblingIndex() > fix.transform.GetSiblingIndex()) go.transform.SetSiblingIndex(fix.transform.GetSiblingIndex());
            if (moved < 2) log.Add("상세 길안내: 「···」 버튼 " + moved + "개만 찾음");
            log.Add("detail guide ok (「···」 " + moved + "개 옮김)");
        }
    }
}
