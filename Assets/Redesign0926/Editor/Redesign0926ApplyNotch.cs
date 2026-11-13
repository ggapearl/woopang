using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 노치 (11-09 대표님 「iOS환경에서 '하늘접기' 버튼을 눌럿을 때 … '노치' 에 반영」 · 「리스트에서 선택을 하면 아이폰의 경우 '노치' 화면에 화살표로 방향과 거리를」).
    /// 캔버스 맨 위에 화면을 덮는 빈 판(Notch0926 · 손가락은 검은 판만 받는다)과 그 안의 검은 판 · 날씨 그림 · 화살표 · 거리 글을 만든다.
    /// 크기와 자리는 기기마다 달라 R0926Notch 가 켜질 때 노치를 재서 정한다 — 여기선 모양과 연결만.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static void ApplyNotch(Transform root, List<string> log)
        {
            var marker = Find(root, "Redesign0926Marker");
            var sky = marker != null ? marker.GetComponent<R0926SkyWeather>() : null;
            if (sky == null) { log.Add("노치: 하늘 날씨 없음"); return; }

            var go = FindOrCreate(root, "Notch0926");
            go.transform.SetAsLastSibling();
            Stretch(RT(go));
            var cv = Ensure<Canvas>(go);
            cv.overrideSorting = true;
            cv.sortingOrder = 9600;   // 창 · 도크 · 토스트(9500) 위, 시작화면(10000) 아래 — 덮는 창이 뜨면 스스로 숨는다
            Ensure<GraphicRaycaster>(go);
            EditorUtility.SetDirty(cv);

            // 검은 판 — 위 가운데 기준 (크기는 R0926Notch)
            var shape = FindOrCreate(go.transform, "Shape0926");
            SetRect(RT(shape), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(700, 120));
            var shapeImg = Img(shape, Spr("r0926_notch"), Color.black, Image.Type.Sliced);
            shapeImg.raycastTarget = true;

            // 노치 양옆 칸 — 왼쪽 그림(날씨 · 화살표 · 도착 ✓), 오른쪽 글(기온 · 거리)
            var content = FindOrCreate(shape.transform, "Content0926");
            SetRect(RT(content), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            var ccg = Ensure<CanvasGroup>(content); ccg.alpha = 0f; ccg.blocksRaycasts = false; ccg.interactable = false;
            var left = FindOrCreate(content.transform, "Left0926");
            SetRect(RT(left), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-200, -60), new Vector2(80, 80));
            var icon = BuildWeatherIcon(left.transform, "Weather0926", 80f);
            SetRect(RT(icon.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
            var arrow = FindOrCreate(left.transform, "Arrow0926");
            SetRect(RT(arrow), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            Img(arrow, Spr("r0926_i_navarrow"), Color.white, Image.Type.Simple).raycastTarget = false;
            arrow.GetComponent<Image>().preserveAspect = true;
            arrow.SetActive(false);
            var check = FindOrCreate(left.transform, "Check0926");
            SetRect(RT(check), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            Img(check, Spr("r0926_i_check"), new Color(0.40f, 0.86f, 0.55f, 1f), Image.Type.Simple).raycastTarget = false;
            check.GetComponent<Image>().preserveAspect = true;
            check.SetActive(false);
            var right = FindOrCreate(content.transform, "Right0926");
            SetRect(RT(right), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(200, -60), new Vector2(160, 120));
            var rt = Txt(right, "19°", 58, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            rt.horizontalOverflow = HorizontalWrapMode.Overflow;
            rt.verticalOverflow = VerticalWrapMode.Overflow;

            // 아래로 늘어났을 때 — 장소 이름 · 작은 안내 줄
            var wide = FindOrCreate(shape.transform, "Wide0926");
            SetRect(RT(wide), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            var wcg = Ensure<CanvasGroup>(wide); wcg.alpha = 0f; wcg.blocksRaycasts = false; wcg.interactable = false;
            var title = FindOrCreate(wide.transform, "Title0926");
            SetRect(RT(title), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(900, 75));
            var tt = Txt(title, "", 53, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            tt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var hint = FindOrCreate(wide.transform, "Hint0926");
            SetRect(RT(hint), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(900, 58));
            var ht = Txt(hint, "", 41, new Color(1f, 1f, 1f, 0.62f), TextAnchor.MiddleCenter, FontStyle.Normal);
            ht.horizontalOverflow = HorizontalWrapMode.Overflow;
            shape.SetActive(false);

            // 덮는 창 — 물방울메뉴와 같은 목록 (보물 축하 화면은 틀이 늘 켜져 있고 Panel 이 켜진다)
            var covers = new List<GameObject>();
            foreach (var path in GlassCovers)
            {
                int cut = path.IndexOf('/');
                var top = Find(root, cut < 0 ? path : path.Substring(0, cut));
                var c = top != null && cut >= 0 ? top.transform.Find(path.Substring(cut + 1))?.gameObject : top;
                if (c != null) covers.Add(c); else log.Add("노치: 덮는 창 없음 " + path);
            }

            var notch = Ensure<R0926Notch>(go);
            var so = new SerializedObject(notch);
            so.FindProperty("shape").objectReferenceValue = RT(shape);
            so.FindProperty("shapeImage").objectReferenceValue = shapeImg;
            so.FindProperty("notchSprite").objectReferenceValue = Spr("r0926_notch");
            so.FindProperty("pillSprite").objectReferenceValue = Spr("r0926_pill");
            so.FindProperty("content").objectReferenceValue = ccg;
            so.FindProperty("leftSlot").objectReferenceValue = RT(left);
            so.FindProperty("rightSlot").objectReferenceValue = RT(right);
            so.FindProperty("weatherIcon").objectReferenceValue = icon;
            so.FindProperty("arrow").objectReferenceValue = RT(arrow);
            so.FindProperty("check").objectReferenceValue = RT(check);
            so.FindProperty("rightText").objectReferenceValue = rt;
            so.FindProperty("wide").objectReferenceValue = wcg;
            so.FindProperty("titleText").objectReferenceValue = tt;
            so.FindProperty("hintText").objectReferenceValue = ht;
            so.FindProperty("sky").objectReferenceValue = sky;
            var cp = so.FindProperty("covers");
            cp.arraySize = covers.Count;
            for (int i = 0; i < covers.Count; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = covers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("노치 ok (덮는 창 " + covers.Count + ")");
        }
    }
}
