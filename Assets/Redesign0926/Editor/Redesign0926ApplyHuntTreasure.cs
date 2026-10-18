using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 보물찾기 AR 보물 — 금빛 상자 프리팹(앵커 + 상자 + Target)과 상태 알약 아래 알림을 만들고 캔버스의 R0926HuntTreasure 에 잇는다.
    /// 상자는 홈페이지 히어로와 같은 금빛 큐브(GoldenGlowCube) — 장소 큐브(흰 테두리)와 한눈에 갈린다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string TreasurePrefabPath = "Assets/Redesign0926/Prefabs/HuntTreasure0926.prefab";
        private const string TreasureMatPath = "Assets/Materials/AR/GoldenGlowCube_Mat.mat";
        private const string PlaceCubePath = "Assets/Prefab/0000_Cube.prefab";

        private static void ApplyHuntTreasure(Transform root, List<string> log)
        {
            var prefab = TreasurePrefab(log);
            var tr = Ensure<R0926HuntTreasure>(root.gameObject);

            // ── 알림 — 상태 알약(노치 아래) 바로 밑 ──
            var box = FindOrCreate(root, "HuntToast0926");
            box.transform.SetAsLastSibling();   // 창들 위 — 창이 열리면 R0926HuntTreasure 가 걷는다 (covers)
            SetRect(RT(box), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14 - PillH - 20), new Vector2(1300, 156));
            Ensure<R0926SafeInset>(box).SetEdge(R0926SafeInset.Edge.Top);
            var cg = Ensure<CanvasGroup>(box);
            cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
            EditorUtility.SetDirty(cg);

            var pill = FindOrCreate(box.transform, "Pill");
            SetRect(RT(pill), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(900, 112));
            Img(pill, Spr("r0926_pill"), new Color(PillBg.r, PillBg.g, PillBg.b, 0.92f), Image.Type.Sliced, 63f / 56f).raycastTarget = false;

            var ic = FindOrCreate(pill.transform, "Ic");
            SetRect(RT(ic), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(88, 88));
            var icImg = Img(ic, Spr("r0926_circle"), HuntGold, Image.Type.Simple);
            icImg.raycastTarget = false; icImg.preserveAspect = true;
            var glyph = FindOrCreate(ic.transform, "Glyph");
            SetRect(RT(glyph), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
            var gImg = Img(glyph, Spr("r0926_i_key"), HuntDark, Image.Type.Simple);
            gImg.raycastTarget = false; gImg.preserveAspect = true;

            var title = FindOrCreate(pill.transform, "Title");
            SetRect(RT(title), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(136, 0), new Vector2(700, 56));
            var tText = Txt(title, "", 40, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            tText.horizontalOverflow = HorizontalWrapMode.Wrap; tText.verticalOverflow = VerticalWrapMode.Overflow;
            var sub = FindOrCreate(pill.transform, "Sub");
            SetRect(RT(sub), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(136, -28), new Vector2(700, 48));
            var sText = Txt(sub, "", 34, Soft, TextAnchor.MiddleLeft, FontStyle.Normal);
            sText.horizontalOverflow = HorizontalWrapMode.Wrap; sText.verticalOverflow = VerticalWrapMode.Overflow;
            box.SetActive(false);

            var so = new SerializedObject(tr);
            so.FindProperty("treasurePrefab").objectReferenceValue = prefab;
            so.FindProperty("toast").objectReferenceValue = cg;
            so.FindProperty("toastPill").objectReferenceValue = RT(pill);
            so.FindProperty("toastTitle").objectReferenceValue = tText;
            so.FindProperty("toastSub").objectReferenceValue = sText;
            var covers = so.FindProperty("covers");
            covers.arraySize = ArCovers.Length;
            int missing = 0;
            for (int i = 0; i < ArCovers.Length; i++)
            {
                var c = Find(root, ArCovers[i]);
                if (c == null) missing++;
                covers.GetArrayElementAtIndex(i).objectReferenceValue = c;
            }
            if (missing > 0) log.Add("보물찾기 AR 보물: 덮는 창 " + missing + "개 못 찾음");
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add(prefab != null ? "hunt treasure ok" : "보물찾기 AR 보물: 프리팹 없음");
            HuntCelebrate(root, log);
        }

        // ── 찾았을 때 축하 화면 — 캔버스 맨 위, 늘 켜진 빈 틀 + 꺼 둔 Panel (R0926HuntCelebrate 가 켠다) ──
        //    선물 상자(뚜껑이 날아감) · 빛줄기 둘 · 꽃가루 · '보물을 찾았습니다!' · 몇 번째 · 상품 카드(선물 그림) · 주최자 안내 · 확인
        private static void HuntCelebrate(Transform root, List<string> log)
        {
            var host = FindOrCreate(root, "HuntCelebrate0926");
            host.transform.SetAsLastSibling();
            Stretch(RT(host));
            var cv = Ensure<Canvas>(host);
            cv.overrideSorting = true;
            cv.sortingOrder = 9000;   // 창·도크·알림 위 (토스트 9500 · 시작화면 10000 은 그 위)
            EditorUtility.SetDirty(cv);
            Ensure<GraphicRaycaster>(host);
            var cel = Ensure<R0926HuntCelebrate>(host);

            var panel = FindOrCreate(host.transform, "Panel");
            Stretch(RT(panel));
            var group = Ensure<CanvasGroup>(panel);
            var dim = FindOrCreate(panel.transform, "Dim");
            Stretch(RT(dim));
            Img(dim, null, new Color(0.02f, 0.02f, 0.04f, 0.78f), Image.Type.Simple).raycastTarget = true;   // 뒤의 AR·도크를 누르지 않게

            var content = FindOrCreate(panel.transform, "Content");
            SetRect(RT(content), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1440, 1900));

            var stage = FindOrCreate(content.transform, "Stage");
            SetRect(RT(stage), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 430), new Vector2(900, 900));
            GameObject Layer(string name, Sprite sp, Color c, Vector2 pos, Vector2 size)
            {
                var g = FindOrCreate(stage.transform, name);
                SetRect(RT(g), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
                var im = Img(g, sp, c, Image.Type.Simple);
                im.raycastTarget = false; im.preserveAspect = true;
                return g;
            }
            var rays = Layer("Rays", Spr("r0926_rays"), new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0f), Vector2.zero, new Vector2(1500, 1500));
            var rays2 = Layer("Rays2", Spr("r0926_rays"), new Color(HuntAmber.r, HuntAmber.g, HuntAmber.b, 0f), Vector2.zero, new Vector2(1020, 1020));
            RT(rays2).localEulerAngles = new Vector3(0, 0, 13f);
            var glow = Layer("Glow", Spr("r0926_glow"), new Color(1f, 0.82f, 0.45f, 0f), new Vector2(0, -20), new Vector2(820, 820));   // 가장자리까지 0 으로 지는 번짐 (64px 별을 키웠더니 옅은 테두리가 계단처럼 보였다)
            var box = Layer("GiftBox", Spr("r0926_gift_box"), Color.white, new Vector2(0, -120), new Vector2(480, 360));
            var lid = Layer("GiftLid", Spr("r0926_gift_lid"), Color.white, new Vector2(0, 195), new Vector2(540, 330));

            var info = FindOrCreate(content.transform, "Info");
            SetRect(RT(info), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1240, 840));
            var infoGroup = Ensure<CanvasGroup>(info);
            var title = FindOrCreate(info.transform, "Title");
            SetRect(RT(title), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 116));
            var tt = Txt(title, "보물을 찾았습니다!", 84, HuntGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            tt.horizontalOverflow = HorizontalWrapMode.Wrap; tt.resizeTextForBestFit = true; tt.resizeTextMinSize = 56; tt.resizeTextMaxSize = 84;
            var rank = FindOrCreate(info.transform, "Rank");
            SetRect(RT(rank), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -126), new Vector2(0, 64));
            var rt = Txt(rank, "", 44, Soft, TextAnchor.MiddleCenter, FontStyle.Normal);
            rt.horizontalOverflow = HorizontalWrapMode.Wrap; rt.resizeTextForBestFit = true; rt.resizeTextMinSize = 32; rt.resizeTextMaxSize = 44;

            var card = FindOrCreate(info.transform, "PrizeCard");
            SetRect(RT(card), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -226), new Vector2(1180, 250));
            Img(card, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 44f).raycastTarget = false;
            var gift = FindOrCreate(card.transform, "Gift");
            SetRect(RT(gift), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(128, -4), new Vector2(180, 190));
            var gb = FindOrCreate(gift.transform, "Box");
            SetRect(RT(gb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(160, 120));
            var gbi = Img(gb, Spr("r0926_gift_box"), Color.white, Image.Type.Simple); gbi.raycastTarget = false; gbi.preserveAspect = true;
            var gl = FindOrCreate(gift.transform, "Lid");
            SetRect(RT(gl), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 41), new Vector2(180, 110));
            var gli = Img(gl, Spr("r0926_gift_lid"), Color.white, Image.Type.Simple); gli.raycastTarget = false; gli.preserveAspect = true;
            var plabel = FindOrCreate(card.transform, "Label");
            SetRect(RT(plabel), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(256, 34), new Vector2(-256 - 44, 52));
            Txt(plabel, "상품", 36, HuntGold, TextAnchor.LowerLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            var prize = FindOrCreate(card.transform, "Prize");
            SetRect(RT(prize), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(256, 26), new Vector2(-256 - 44, 124));
            var pt = Txt(prize, "", 46, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            pt.horizontalOverflow = HorizontalWrapMode.Wrap; pt.verticalOverflow = VerticalWrapMode.Truncate; pt.lineSpacing = 1.05f;
            pt.resizeTextForBestFit = true; pt.resizeTextMinSize = 34; pt.resizeTextMaxSize = 46;

            var note = FindOrCreate(info.transform, "Note");
            SetRect(RT(note), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -500), new Vector2(0, 120));
            var nt = Txt(note, "", 36, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            nt.horizontalOverflow = HorizontalWrapMode.Wrap; nt.lineSpacing = 1.15f;

            var ok = FindOrCreate(info.transform, "OK");
            SetRect(RT(ok), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -660), new Vector2(760, 150));
            var okImg = Img(ok, Spr("r0926_pill"), HuntGold, Image.Type.Sliced, 64f / 70f);
            okImg.raycastTarget = true;
            var okBtn = Ensure<Button>(ok);
            okBtn.transition = Selectable.Transition.ColorTint; okBtn.targetGraphic = okImg;
            Ensure<ClickButtonOnBack>(ok);   // 안드로이드 뒤로가기 = 확인
            var okl = FindOrCreate(ok.transform, "Label");
            Stretch(RT(okl));
            var okText = Txt(okl, "확인", 50, HuntDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            panel.SetActive(false);

            var so = new SerializedObject(cel);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("content").objectReferenceValue = RT(content);
            so.FindProperty("rays").objectReferenceValue = RT(rays);
            so.FindProperty("rays2").objectReferenceValue = RT(rays2);
            so.FindProperty("glow").objectReferenceValue = RT(glow);
            so.FindProperty("giftBox").objectReferenceValue = RT(box);
            so.FindProperty("giftLid").objectReferenceValue = RT(lid);
            so.FindProperty("stage").objectReferenceValue = RT(stage);
            so.FindProperty("info").objectReferenceValue = infoGroup;
            so.FindProperty("title").objectReferenceValue = tt;
            so.FindProperty("rankText").objectReferenceValue = rt;
            so.FindProperty("prizeLabel").objectReferenceValue = plabel.GetComponent<Text>();
            so.FindProperty("prizeText").objectReferenceValue = pt;
            so.FindProperty("noteText").objectReferenceValue = nt;
            so.FindProperty("okLabel").objectReferenceValue = okText;
            so.FindProperty("okButton").objectReferenceValue = okBtn;
            so.FindProperty("confettiRound").objectReferenceValue = Spr("r0926_circle");
            so.FindProperty("glintSprite").objectReferenceValue = Spr("r0926_glint");
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("hunt celebrate ok");
        }

        // 앵커(뿌리, 크기 1 — 줌이 뿌리 크기를 바꾼다) + 금빛 상자 1.2m (누르기 쉽게 충돌 상자는 조금 크게) + 박스·화살표(Target, 금색).
        // 한 번 만든 뒤엔 열어서 고쳐 저장한다 — 새로 저장하면 내부 ID 가 바뀌어 장면의 참조가 끊길 수 있다
        private static GameObject TreasurePrefab(List<string> log)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TreasureMatPath);
            if (mat == null) { log.Add("보물찾기 AR 보물: 재질 없음 " + TreasureMatPath); return null; }
            if (!File.Exists(TreasurePrefabPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TreasurePrefabPath));
                var fresh = new GameObject("HuntTreasure0926");
                try { PrefabUtility.SaveAsPrefabAsset(fresh, TreasurePrefabPath); }
                finally { Object.DestroyImmediate(fresh); }
            }

            var rootGo = PrefabUtility.LoadPrefabContents(TreasurePrefabPath);
            try
            {
                Ensure<CustomARGeospatialCreatorAnchor>(rootGo);
                rootGo.transform.localScale = Vector3.one;
                var chestT = rootGo.transform.Find("Chest");
                var chest = chestT != null ? chestT.gameObject : new GameObject("Chest");
                chest.transform.SetParent(rootGo.transform, false);
                chest.transform.localPosition = Vector3.zero;
                chest.transform.localRotation = Quaternion.identity;
                chest.transform.localScale = Vector3.one * 1.2f;
                Ensure<MeshFilter>(chest).sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                var mr = Ensure<MeshRenderer>(chest);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                var bc = Ensure<BoxCollider>(chest);
                bc.center = Vector3.zero;
                bc.size = Vector3.one * 1.4f;

                var glow = Ensure<GoldenGlowCube>(chest);
                var gso = new SerializedObject(glow);
                gso.FindProperty("rotateSpeedX").floatValue = 0f;     // 옆으로 구르지 않고 제자리에서 돈다
                gso.FindProperty("rotateSpeedY").floatValue = 24f;
                // 위아래로 떠다니지 않게 — 보물은 숨긴 자리에 가만히 (10-14 대표님: '위아래로 움직이지? 제자리에 가만히')
                gso.FindProperty("enableFloat").boolValue = false;
                gso.FindProperty("floatAmplitude").floatValue = 0f;
                gso.ApplyModifiedPropertiesWithoutUndo();

                var target = Ensure<Target>(chest);
                var dst = new SerializedObject(target);
                var placeCube = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceCubePath);
                var srcTarget = placeCube != null ? placeCube.GetComponentInChildren<Target>(true) : null;
                if (srcTarget != null)
                {
                    // 박스·화살표 크기는 장소 큐브와 같게
                    var src = new SerializedObject(srcTarget);
                    foreach (var p in new[] { "needBoxIndicator", "needArrowIndicator", "needDistanceText", "defaultBoxSize", "maxBoxSize",
                                              "defaultArrowSize", "maxArrowSize", "minDistance", "maxDistance" })
                    {
                        var sp = src.FindProperty(p);
                        if (sp != null) dst.CopyFromSerializedProperty(sp);
                    }
                }
                else log.Add("보물찾기 AR 보물: 장소 큐브 Target 없음 — 기본 크기로");
                dst.FindProperty("targetColor").colorValue = HuntGold;
                dst.FindProperty("distanceTextColor").colorValue = HuntGold;
                dst.FindProperty("placeName").stringValue = "";
                dst.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(rootGo, TreasurePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rootGo);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(TreasurePrefabPath);
        }
    }
}
