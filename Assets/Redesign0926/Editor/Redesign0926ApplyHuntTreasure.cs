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
                gso.FindProperty("floatAmplitude").floatValue = 0.12f;
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
