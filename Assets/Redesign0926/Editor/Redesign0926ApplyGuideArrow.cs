using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// AR 화면 아래쪽 3D 길안내 (11-15 대표님 (B) · 시안 B1 「화살표」) — 땅에 누운 분홍 화살표(R0926GuideArrowMesh) · 위 알약 「이름 거리 · 방향」 · 알약 오른쪽 끝 × (11-16) · 알약 위 작은 안내 줄.
    /// 부품만 만든다 — 방향 · 거리 · 숨기기 · 끝내기는 R0926GuideArrow. 알약은 물방울메뉴면 물방울 유리, 기본메뉴면 다른 알약과 같은 진한 반투명.
    /// 자리는 가까이 모달(NearModal0926) 바로 앞 — 가까이 카드가 위에 그려진다 (카드가 떠 있는 동안 화살표는 숨는다).
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static void ApplyGuideArrow(Transform root, List<string> log)
        {
            var c = new Vector2(0.5f, 0.5f);
            var bottom = new Vector2(0.5f, 0f);

            var host = FindOrCreate(root, "GuideArrow0926");
            Stretch(RT(host));
            var nearHost = root.Find("NearModal0926");
            if (nearHost != null)
            {
                int hi = host.transform.GetSiblingIndex(), ni = nearHost.GetSiblingIndex();
                host.transform.SetSiblingIndex(hi < ni ? ni - 1 : ni);
            }
            else log.Add("길안내 화살표: 가까이 모달 없음 — 맨 뒤에 둠");

            // 무리 — 판 가운데가 기준점 (높이 · 배율은 R0926GuideArrow.Place 가 바닥 위로)
            var group = FindOrCreate(host.transform, "Group");
            SetRect(RT(group), bottom, bottom, c, new Vector2(0, 662), new Vector2(1440, 1100));
            var groupCg = Ensure<CanvasGroup>(group);
            groupCg.alpha = 0f;

            // 화살표 — 매 프레임 다시 그리므로 제 캔버스에 (큰 캔버스는 다시 묶지 않게)
            var arrowGo = FindOrCreate(group.transform, "Arrow");
            SetRect(RT(arrowGo), c, c, c, Vector2.zero, new Vector2(1440, 900));
            Ensure<Canvas>(arrowGo);
            var mesh = Ensure<R0926GuideArrowMesh>(arrowGo);
            mesh.color = Color.white;
            mesh.raycastTarget = false;
            EditorUtility.SetDirty(mesh);

            // 알약 — 「<b>우팡이네집</b>  290m · 오른쪽 앞」 (폭은 글에 맞춘다)
            var pillGo = FindOrCreate(group.transform, "Pill");
            SetRect(RT(pillGo), c, c, c, new Vector2(0, 442), new Vector2(760, 112));
            var pillImg = Img(pillGo, Spr("r0926_pill"), Glass, Image.Type.Sliced, 63f / 56f);
            pillImg.raycastTarget = true;
            var btn = Ensure<Button>(pillGo);
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = pillImg;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            EditorUtility.SetDirty(btn);
            var labelGo = FindOrCreate(pillGo.transform, "Label");
            Stretch(RT(labelGo));
            var label = Txt(labelGo, "<b>우팡이네집</b>  <size=42><color=#FFFFFFCC>290m · 오른쪽 앞</color></size>", 50, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            AddShadow(labelGo);

            // × — 알약 오른쪽 끝(둥근 끝과 같은 가운데), 한 번에 안내 끝 (11-16 대표님 「길안내의 경우 하다가 취소할 수 있도록」).
            // 가까이 카드 × 와 같은 동그란 유리 · 누르는 자리는 그림보다 넓게. 글 자리는 R0926GuideArrow 가 × 를 비켜 잡는다 (CloseRoom 114)
            var closeBtn = NearButton(pillGo.transform, "Close", new Vector2(1, 0.5f), new Vector2(-56, 0), 76f, 0.22f, 0.4f, "r0926_i_close", 34f, 0f);

            // 안내 줄 — 처음 3초 「누르면 안내를 끝내요」
            var hintGo = FindOrCreate(group.transform, "Hint");
            SetRect(RT(hintGo), c, c, c, new Vector2(0, 442 + 56 + 50), new Vector2(1100, 60));
            var hintCg = Ensure<CanvasGroup>(hintGo);
            hintCg.alpha = 0f; hintCg.blocksRaycasts = false; hintCg.interactable = false;
            var hint = Txt(hintGo, "누르면 안내를 끝내요", 38, new Color(1, 1, 1, 0.92f), TextAnchor.MiddleCenter, FontStyle.Normal);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.verticalOverflow = VerticalWrapMode.Overflow;
            AddShadow(hintGo);

            // 덮는 창 — 가까이 카드와 같은 목록 (물방울 유리 덮는 창 + AR 미리보기 카드)
            var covers = NearCovers(root, log);
            var preview = FindInScene<ARPreviewController>(root.gameObject);
            if (preview != null)
            {
                var pp = new SerializedObject(preview).FindProperty("previewPanel");
                var panel = pp != null ? pp.objectReferenceValue as GameObject : null;
                if (panel != null && !covers.Contains(panel)) covers.Add(panel);
            }

            var marker = Find(root, "Redesign0926Marker");
            var sky = marker != null ? marker.GetComponent<R0926SkyWeather>() : null;
            GameObject board = null;
            foreach (var r in root.gameObject.scene.GetRootGameObjects()) if (r.name == "SkyWeather0926") board = r;
            var frame = board != null ? board.transform.Find("Frame0926") : null;
            var skyCard = frame != null ? frame.Find("Glass0926") as RectTransform : null;
            if (sky == null || skyCard == null) log.Add("길안내 화살표: 하늘 카드 없음 — 비켜 서지 않는다");
            var floor = new List<Object>();
            foreach (var n in new[] { "LocationManagerPanel", "Dock0926" })
            {
                var f = Find(root, n);
                if (f != null) floor.Add(RT(f));
                else log.Add("길안내 화살표: 바닥 없음 " + n);
            }
            var near = nearHost != null ? nearHost.GetComponent<R0926NearModal>() : null;

            var ga = Ensure<R0926GuideArrow>(host);
            var so = new SerializedObject(ga);
            so.FindProperty("group").objectReferenceValue = RT(group);
            so.FindProperty("groupAlpha").objectReferenceValue = groupCg;
            so.FindProperty("arrow").objectReferenceValue = mesh;
            so.FindProperty("pill").objectReferenceValue = btn;
            so.FindProperty("pillRect").objectReferenceValue = RT(pillGo);
            so.FindProperty("close").objectReferenceValue = closeBtn;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("hintGroup").objectReferenceValue = hintCg;
            so.FindProperty("hint").objectReferenceValue = hint;
            SetArray(so.FindProperty("covers"), covers.ToArray());
            so.FindProperty("sky").objectReferenceValue = sky;
            so.FindProperty("skyCard").objectReferenceValue = skyCard;
            so.FindProperty("near").objectReferenceValue = near;
            SetArray(so.FindProperty("floor"), floor.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();

            // 물방울 유리 — 알약 (기본메뉴면 R0926Glass 가 아무것도 바꾸지 않는다)
            int glassN = 0;
            var mat = GlassMaterial(log);
            var blur = AssetDatabase.LoadAssetAtPath<Shader>(GlassBlurPath);
            if (mat != null && blur != null) glassN = NearGlass(pillGo.transform, mat, blur, covers);
            else log.Add("길안내 화살표: 물방울 재질/흐림 셰이더 없음");

            group.SetActive(false);   // 안내를 시작하면 R0926GuideArrow 가 켠다
            log.Add("guide arrow ok (덮는 창 " + covers.Count + " · 바닥 " + floor.Count + " · 가까이 카드 " + (near != null ? "연결" : "없음") + " · 물방울 " + glassN + ")");
        }
    }
}
