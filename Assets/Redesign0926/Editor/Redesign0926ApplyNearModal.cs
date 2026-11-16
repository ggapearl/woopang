using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 가까이 모달 (11-15 대표님 (C) — 2m 안에 들어온 오브젝트를 화면 가운데 투명 카드에 「○○○ / 바로 근처에 있습니다」 두 줄 · × · 여러 개면 옆으로 넘긴다).
    /// 부품만 만든다 — 고르기 · 찍기 · 넘기기 · 누르기는 R0926NearModal.
    /// 물방울메뉴면 카드 바탕과 고정한 하늘 카드(판 유리 · 📌 · 지역 · 예보 · 옆 장의 머리 · 줄 · 방송사 칸)도 물방울 유리 (시안 C4). 하늘에 걸린 판(월드 캔버스)은 그대로
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static readonly Color NearCardColor = new Color(0.055f, 0.067f, 0.078f, 0.62f);
        private const float NearHit = 150f;   // × · ‹ › 누르는 자리 (그림보다 넓게)

        private static void ApplyNearModal(Transform root, List<string> log)
        {
            var c = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);

            // 자리 — 화면에 꽉 찬 빈 틀. 고정한 하늘 카드(SkyPin0926) · 도크 바로 앞, 화면 밖 화살표 · 장소 표시 위
            var host = FindOrCreate(root, "NearModal0926");
            Stretch(RT(host));
            var pin = root.Find("SkyPin0926");
            if (pin != null)
            {
                int hi = host.transform.GetSiblingIndex(), pi = pin.GetSiblingIndex();
                host.transform.SetSiblingIndex(hi < pi ? pi - 1 : pi);
            }

            // 카드 — 높이는 R0926NearModal.Arrange 가 (하나 1375 · 여럿 1446)
            var card = FindOrCreate(host.transform, "Card");
            SetRect(RT(card), c, c, c, Vector2.zero, new Vector2(1240, 1375));
            Img(card, Spr("r0926_pill"), NearCardColor, Image.Type.Sliced, 63f / 100f).raycastTarget = true;
            var cardGroup = Ensure<CanvasGroup>(card);

            var close = NearButton(card.transform, "Close", new Vector2(1, 1), new Vector2(-97, -97), 100f, 0.22f, 0.4f, "r0926_i_close", 52f, 0f);

            // 머리 — 거리 알약 · 제목 (보는 오브젝트가 바뀌면 살짝 바꿔 끼운다)
            var head = FindOrCreate(card.transform, "Head");
            Stretch(RT(head));
            var headGroup = Ensure<CanvasGroup>(head);
            headGroup.blocksRaycasts = false; headGroup.interactable = false;

            var tag = FindOrCreate(head.transform, "Tag");
            SetRect(RT(tag), top, top, c, new Vector2(0, -121), new Vector2(260, 94));
            Img(tag, Spr("r0926_pill"), new Color(1, 1, 1, 0.2f), Image.Type.Sliced, 63f / 47f).raycastTarget = false;
            NearRing(tag.transform, 0.35f, 47f);
            var tagPin = FindOrCreate(tag.transform, "Pin");
            var tagPinImg = Img(tagPin, Spr("r0926_i_pin"), Color.white, Image.Type.Simple);
            tagPinImg.raycastTarget = false; tagPinImg.preserveAspect = true;
            SetRect(RT(tagPin), new Vector2(0, 0.5f), new Vector2(0, 0.5f), c, new Vector2(34f + 47f / 2f, 0), new Vector2(47, 47));
            var dist = FindOrCreate(tag.transform, "Distance");
            var distText = Txt(dist, "1.2m", 45, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            distText.horizontalOverflow = HorizontalWrapMode.Overflow;
            distText.verticalOverflow = VerticalWrapMode.Overflow;
            var drt = RT(dist);
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.pivot = c;
            drt.offsetMin = new Vector2(34f + 47f + 16f, 0); drt.offsetMax = new Vector2(-40f, 0);
            EditorUtility.SetDirty(drt);
            AddShadow(dist);

            var title = FindOrCreate(head.transform, "Title");
            SetRect(RT(title), top, top, c, new Vector2(0, -296), new Vector2(1080, 196));
            var titleText = Txt(title, "우팡이네집\n바로 근처에 있습니다", 70, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            titleText.supportRichText = true;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;
            titleText.lineSpacing = 1f;
            AddShadow(title);

            // 무대 — 그림자 · 세 칸(가운데 · 양옆 미리보기, 맨 앞은 가운데 칸) · ‹ ›
            var stage = FindOrCreate(card.transform, "Stage");
            SetRect(RT(stage), top, top, c, new Vector2(0, -814), new Vector2(1240, 840));
            var shadowGo = FindOrCreate(stage.transform, "Shadow");
            var shadowImg = Img(shadowGo, Spr("r0926_glow"), new Color(0, 0, 0, 0.25f), Image.Type.Simple);
            shadowImg.raycastTarget = false;
            SetRect(RT(shadowGo), new Vector2(0.5f, 0), new Vector2(0.5f, 0), c, new Vector2(0, 97), new Vector2(504, 74));
            shadowGo.transform.SetSiblingIndex(0);
            var viewsGo = FindOrCreate(stage.transform, "Views");
            Stretch(RT(viewsGo));
            viewsGo.transform.SetSiblingIndex(1);
            var premul = PremulMaterial(log);
            var views = new RawImage[3];
            for (int i = 0; i < views.Length; i++)
            {
                var v = FindOrCreate(viewsGo.transform, "View" + i);
                SetRect(RT(v), c, c, c, Vector2.zero, new Vector2(600, 690));
                var raw = Ensure<RawImage>(v);
                raw.material = premul;   // 투명 배경으로 찍은 판 — 미리 곱한 알파
                raw.texture = null;
                raw.color = Color.white;
                raw.raycastTarget = true;   // 옆 칸을 누르면 그쪽으로
                raw.enabled = false;
                EditorUtility.SetDirty(raw);
                views[i] = raw;
            }
            var prev = NearButton(stage.transform, "Prev", new Vector2(0, 0.5f), new Vector2(104, 0), 114f, 0.18f, 0.35f, "r0926_i_back", 54f, 0f);
            var next = NearButton(stage.transform, "Next", new Vector2(1, 0.5f), new Vector2(-104, 0), 114f, 0.18f, 0.35f, "r0926_i_back", 54f, 180f);
            prev.transform.SetAsLastSibling();
            next.transform.SetAsLastSibling();
            prev.gameObject.SetActive(false);
            next.gameObject.SetActive(false);

            // 점 — 여러 개일 때만 (보는 칸은 길쭉하게 — R0926NearModal.Dots)
            var dots = FindOrCreate(card.transform, "Dots");
            SetRect(RT(dots), top, top, c, new Vector2(0, -1269.5f), new Vector2(600, 24));
            var dotImgs = new Image[6];
            for (int i = 0; i < dotImgs.Length; i++)
            {
                var d = FindOrCreate(dots.transform, "Dot" + i);
                SetRect(RT(d), c, c, c, new Vector2(-120f + i * 48f, 0), new Vector2(24, 24));
                dotImgs[i] = Img(d, Spr("r0926_pill"), new Color(1, 1, 1, 0.45f), Image.Type.Sliced, 63f / 12f);
                dotImgs[i].raycastTarget = false;
            }
            dots.SetActive(false);

            // 안내 — 톡톡 그림 + 글 (가운데 맞춤은 R0926NearModal.SetHint)
            var hintGo = FindOrCreate(card.transform, "Hint");
            SetRect(RT(hintGo), top, top, c, new Vector2(0, -1271), new Vector2(1240, 74));
            var hintGroup = Ensure<CanvasGroup>(hintGo);
            hintGroup.blocksRaycasts = false; hintGroup.interactable = false;
            var tap = FindOrCreate(hintGo.transform, "TapIcon");
            SetRect(RT(tap), c, c, c, new Vector2(-330, 0), new Vector2(74, 74));
            NearTapPart(tap.transform, "Ring2", "r0926_ring_mini", 73f, 0.45f, 0);
            NearTapPart(tap.transform, "Ring1", "r0926_ring_mini", 48f, 0.8f, 1);
            NearTapPart(tap.transform, "Dot", "r0926_circle", 20f, 1f, 2);
            var hintText = FindOrCreate(hintGo.transform, "Text");
            SetRect(RT(hintText), c, c, c, new Vector2(50, 0), new Vector2(900, 74));
            var hint = Txt(hintText, "두 번 톡톡 누르면 자세히 볼 수 있어요", 49, new Color(1, 1, 1, 0.95f), TextAnchor.MiddleCenter, FontStyle.Normal);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.verticalOverflow = VerticalWrapMode.Overflow;
            AddShadow(hintText);

            // 덮는 창 — 물방울 유리와 같은 목록 + AR 미리보기 카드. 이것들이 열려 있으면 카드가 숨고 물체는 AR 로 돌아간다
            var covers = NearCovers(root, log);
            var preview = FindInScene<ARPreviewController>(root.gameObject);
            if (preview != null)
            {
                var pp = new SerializedObject(preview).FindProperty("previewPanel");
                var panel = pp != null ? pp.objectReferenceValue as GameObject : null;
                if (panel != null && !covers.Contains(panel)) covers.Add(panel);
                else if (panel == null) log.Add("가까이 모달: AR 미리보기 카드 없음");
            }

            // 비켜 설 자리 — 고정한 하늘 카드의 유리 · 주소줄 · 도크
            var marker = Find(root, "Redesign0926Marker");
            var sky = marker != null ? marker.GetComponent<R0926SkyWeather>() : null;
            GameObject board = null;
            foreach (var r in root.gameObject.scene.GetRootGameObjects()) if (r.name == "SkyWeather0926") board = r;
            var frame = board != null ? board.transform.Find("Frame0926") : null;
            var skyCard = frame != null ? frame.Find("Glass0926") as RectTransform : null;
            if (sky == null || skyCard == null) log.Add("가까이 모달: 하늘 카드 없음 — 비켜 서지 않는다");
            var floor = new List<Object>();
            foreach (var n in new[] { "LocationManagerPanel", "Dock0926" })
            {
                var f = Find(root, n);
                if (f != null) floor.Add(RT(f));
            }

            var nm = Ensure<R0926NearModal>(host);
            var so = new SerializedObject(nm);
            so.FindProperty("card").objectReferenceValue = RT(card);
            so.FindProperty("cardGroup").objectReferenceValue = cardGroup;
            so.FindProperty("close").objectReferenceValue = close;
            so.FindProperty("head").objectReferenceValue = headGroup;
            so.FindProperty("distTag").objectReferenceValue = RT(tag);
            so.FindProperty("distText").objectReferenceValue = distText;
            so.FindProperty("title").objectReferenceValue = titleText;
            so.FindProperty("stage").objectReferenceValue = RT(stage);
            SetArray(so.FindProperty("views"), views);
            so.FindProperty("shadow").objectReferenceValue = shadowImg;
            so.FindProperty("prevButton").objectReferenceValue = prev;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("dots").objectReferenceValue = RT(dots);
            SetArray(so.FindProperty("dotImages"), dotImgs);
            so.FindProperty("hintGroup").objectReferenceValue = hintGroup;
            so.FindProperty("tapIcon").objectReferenceValue = RT(tap);
            so.FindProperty("hint").objectReferenceValue = hint;
            SetArray(so.FindProperty("covers"), covers.ToArray());
            so.FindProperty("sky").objectReferenceValue = sky;
            so.FindProperty("skyCard").objectReferenceValue = skyCard;
            SetArray(so.FindProperty("floor"), floor.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();

            // 물방울 유리 — 카드 바탕 + 고정한 하늘 카드 (하늘에 걸린 동안은 월드 캔버스라 R0926Glass 가 스스로 쉰다)
            int glassN = 0;
            var mat = GlassMaterial(log);
            var blur = AssetDatabase.LoadAssetAtPath<Shader>(GlassBlurPath);
            if (mat == null || blur == null) log.Add("가까이 모달: 물방울 재질/흐림 셰이더 없음");
            else
            {
                glassN += NearGlass(card.transform, mat, blur, covers);
                if (frame != null)
                {
                    foreach (var p in new[] { "Glass0926", "PinBtn0926", "Weather0926/Region0926", "Weather0926/Forecast0926" })
                        glassN += NearGlass(frame.Find(p), mat, blur, covers);
                    foreach (var page in new[] { "Hunt0926", "Festival0926", "Hot0926", "Friends0926", "Live0926" })
                    {
                        var pt = frame.Find(page);
                        if (pt == null) continue;
                        foreach (Transform ch in pt)
                            if (ch.name == "Head0926" || ch.name.StartsWith("Row") || ch.name.StartsWith("Chip"))
                                glassN += NearGlass(ch, mat, blur, covers);
                    }
                }
            }

            card.SetActive(false);   // 2m 안에 들어오면 R0926NearModal 이 켠다
            log.Add("near modal ok (덮는 창 " + covers.Count + " · 바닥 " + floor.Count + " · 물방울 " + glassN + ")");
        }

        // ×  · ‹ › — 넓은 투명 자리 + 동그란 유리 + 테두리 + 그림
        private static Button NearButton(Transform parent, string name, Vector2 anchor, Vector2 pos, float disc, float discAlpha, float ringAlpha, string icon, float iconSize, float iconAngle)
        {
            var c = new Vector2(0.5f, 0.5f);
            var go = FindOrCreate(parent, name);
            SetRect(RT(go), anchor, anchor, c, pos, new Vector2(NearHit, NearHit));
            var hit = Img(go, null, new Color(1, 1, 1, 0), Image.Type.Simple);
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = false;   // 투명해도 눌리게
            var b = Ensure<Button>(go);
            b.transition = Selectable.Transition.None;
            b.targetGraphic = hit;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            EditorUtility.SetDirty(b);

            var d = FindOrCreate(go.transform, "Disc");
            SetRect(RT(d), c, c, c, Vector2.zero, new Vector2(disc, disc));
            Img(d, Spr("r0926_circle"), new Color(1, 1, 1, discAlpha), Image.Type.Simple).raycastTarget = false;
            NearRing(d.transform, ringAlpha, disc / 2f);
            var ic = FindOrCreate(d.transform, "Icon");
            SetRect(RT(ic), c, c, c, Vector2.zero, new Vector2(iconSize, iconSize));
            RT(ic).localEulerAngles = new Vector3(0, 0, iconAngle);
            var ii = Img(ic, Spr(icon), Color.white, Image.Type.Simple);
            ii.raycastTarget = false; ii.preserveAspect = true;
            return b;
        }

        // 알약 · 동그라미 테두리 — 늘린 알약 그림(모서리 63px)을 반지름에 맞춘다
        private static void NearRing(Transform parent, float alpha, float radius)
        {
            var ring = FindOrCreate(parent, "Ring");
            Stretch(RT(ring));
            Img(ring, Spr("r0926_pill_ring"), new Color(1, 1, 1, alpha), Image.Type.Sliced, 63f / radius).raycastTarget = false;
            ring.transform.SetSiblingIndex(0);
        }

        private static void NearTapPart(Transform parent, string name, string sprite, float size, float alpha, int index)
        {
            var c = new Vector2(0.5f, 0.5f);
            var go = FindOrCreate(parent, name);
            SetRect(RT(go), c, c, c, Vector2.zero, new Vector2(size, size));
            Img(go, Spr(sprite), new Color(1, 1, 1, alpha), Image.Type.Simple).raycastTarget = false;
            go.transform.SetSiblingIndex(index);
        }

        // 물방울 유리와 같은 덮는 창 목록 ('틀/안' 은 늘 켜진 틀 안의 켜졌다 꺼지는 것)
        private static List<GameObject> NearCovers(Transform root, List<string> log)
        {
            var covers = new List<GameObject>();
            foreach (var path in GlassCovers)
            {
                int cut = path.IndexOf('/');
                var t = Find(root, cut < 0 ? path : path.Substring(0, cut));
                var cv = t != null && cut >= 0 ? t.transform.Find(path.Substring(cut + 1))?.gameObject : t;
                if (cv != null) covers.Add(cv); else log.Add("가까이 모달: 덮는 창 없음 " + path);
            }
            return covers;
        }

        private static int NearGlass(Transform t, Material mat, Shader blur, List<GameObject> covers)
        {
            var img = t != null ? t.GetComponent<Image>() : null;
            if (img == null) return 0;
            if (img.material != null && img.material.shader == mat.shader) img.material = null;   // 혹시 재질이 저장돼 있었으면 예전 그림으로
            var g = Ensure<R0926Glass>(t.gameObject);
            var so = new SerializedObject(g);
            so.FindProperty("glass").objectReferenceValue = mat;
            so.FindProperty("blur").objectReferenceValue = blur;
            so.FindProperty("tint").floatValue = Mathf.Max(img.color.a, 0.05f);
            var cv = so.FindProperty("covers");
            cv.arraySize = covers.Count;
            for (int i = 0; i < covers.Count; i++) cv.GetArrayElementAtIndex(i).objectReferenceValue = covers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(img);
            return 1;
        }
    }
}
