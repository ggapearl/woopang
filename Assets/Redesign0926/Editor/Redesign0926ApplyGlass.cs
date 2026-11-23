using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 물방울메뉴 (11-08 대표님 「하단버튼 메뉴 배경 … '물방울' 모양의 (iOS 앱 로직) 식으로」 · 「다른 반투명 배경으로 들어가는 바에 있어서는 다 적용해주고, 설정에서 선택」).
    /// AR 화면 위에 뜨는 반투명 알약들에 R0926Glass 를 붙인다 — 설정이 기본메뉴면 아무것도 바뀌지 않는다.
    /// 붙이지 않는 것: 하늘 날씨판 · 옆 장(월드 캔버스 — 뒤 화면이 카메라 그림이 아니다) · 지도 위 단추 · 창 안의 입력줄 · 장소 수정 화면의 동그란 단추.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string GlassMatPath = "Assets/Redesign0926/Materials/R0926LiquidGlass.mat";
        private const string GlassBlurPath = "Assets/Redesign0926/Shaders/R0926GlassBlur.shader";

        // 이 창이 열려 있으면 뒤가 AR 화면이 아니다 — 비침 없이 테두리 빛만.
        // 늘 켜져 있는 틀은 그 안의 켜졌다 꺼지는 것으로 (보물 축하 화면은 틀이 늘 켜져 있고 Panel 이 켜진다)
        private static readonly string[] GlassCovers =
        {
            "FullScreenPanel", "ListPanel", "MessagePanel", "ChatRoomPanel", "FullProfilePanel", "FollowPanel", "UploadPage", "Fixpage", "FirstTimeGuidePanel",
            "PhotoCrop0926", "AskAISheet0926", "HuntReview0926", "HuntCelebrate0926/Panel", "ChatPhotoView0926", "Splash0926",
        };

        // 하단 탭(도크)으로 여는 창 — 도크 위에서 끝나 도크 뒤는 그대로 AR 화면이다
        // (추가 · 보물찾기는 UploadPage 를 나눠 쓴다 · 대화방은 메시지 칸 · 팔로우 목록은 프로필 칸이 X '닫기').
        // 11-20 대표님 S5 「탭을 열었을 때와 닫았을 때 하단 버튼 배경 물방울 효과가 다르다 → 똑같이 · X(닫기)만 다르게」 —
        // 도크만 이 창들로는 가려지지 않고(v114), 이 창들 뒤 어두운 바탕도 도크 바로 아래 한 장(DockDim0926)이 대신 그린다(v115)
        private static readonly string[] DockTabPanels = { "ListPanel", "UploadPage", "MessagePanel", "ChatRoomPanel", "FullProfilePanel", "FollowPanel" };

        private static void ApplyGlass(Transform root, List<string> log)
        {
            DockDim(root, log);   // 기본메뉴에서도 — 탭 창이 열려도 도크가 어두워지지 않는다
            var mat = GlassMaterial(log);
            var blur = AssetDatabase.LoadAssetAtPath<Shader>(GlassBlurPath);
            if (mat == null || blur == null) { log.Add("물방울 유리: 재질/흐림 셰이더 없음"); return; }

            var covers = new List<GameObject>();
            var dockCovers = new List<GameObject>();
            foreach (var path in GlassCovers)
            {
                int cut = path.IndexOf('/');
                var top = Find(root, cut < 0 ? path : path.Substring(0, cut));
                var c = top != null && cut >= 0 ? top.transform.Find(path.Substring(cut + 1))?.gameObject : top;
                if (c == null) { log.Add("물방울 유리: 덮는 창 없음 " + path); continue; }
                covers.Add(c);
                if (System.Array.IndexOf(DockTabPanels, path) < 0) dockCovers.Add(c);
            }

            var oc = FindInScene<ObjectCountUI>(root.gameObject);
            var targets = new (string what, Transform t)[]
            {
                ("도크", Find(root, "Dock0926")?.transform),
                ("위치 칩", Find(root, "LocationManagerPanel")?.transform),
                ("하늘 칩", Find(root, "SkyChip0926")?.transform),
                ("주변 장소 알약", oc != null ? oc.transform.Find("Back") : null),
                ("불러오는 중 알약", Find(root, "PillBg0926")?.transform),
                ("보물 알림", Find(root, "HuntToast0926")?.transform.Find("Pill")),
                ("토스트", root.Find("Toast0926/Stack/Template")),   // 캔버스 바로 아래 것 — 보물찾기 카드 안에도 같은 이름이 있다
            };
            int n = 0;
            foreach (var (what, t) in targets)
            {
                var img = t != null ? t.GetComponent<Image>() : null;
                if (img == null) { log.Add("물방울 유리: " + what + " 없음"); continue; }
                if (img.material != null && img.material.shader == mat.shader) img.material = null;   // 혹시 재질이 저장돼 있었으면 예전 그림으로
                var g = Ensure<R0926Glass>(t.gameObject);
                var so = new SerializedObject(g);
                so.FindProperty("glass").objectReferenceValue = mat;
                so.FindProperty("blur").objectReferenceValue = blur;
                so.FindProperty("tint").floatValue = Mathf.Max(img.color.a, 0.05f);
                var mine = what == "도크" ? dockCovers : covers;
                var cv = so.FindProperty("covers");
                cv.arraySize = mine.Count;
                for (int i = 0; i < mine.Count; i++) cv.GetArrayElementAtIndex(i).objectReferenceValue = mine[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(img);
                n++;
            }
            log.Add("물방울 유리 " + n + "곳");
        }

        /// <summary>
        /// 탭 창 뒤 어두운 바탕을 도크 바로 아래 한 장으로 (R0926DockDim) — 예전엔 창 뿌리의 바탕(검정 0.35)이 도크 위에 덮여
        /// 창이 열리면 도크(유리 · 아이콘 · 글자)가 통째로 어두워졌다. 창 쪽 바탕은 그대로 손가락을 받고(R0926DockPassThrough) 그리지만 않는다(R0926DimSource).
        /// </summary>
        private static void DockDim(Transform root, List<string> log)
        {
            var dock = Find(root, "Dock0926");
            if (dock == null || dock.transform.parent != root) { log.Add("도크 그늘: 도크 없음"); return; }
            var go = FindOrCreate(root, "DockDim0926");
            int d = dock.transform.GetSiblingIndex(), m = go.transform.GetSiblingIndex();
            if (m != d - 1) go.transform.SetSiblingIndex(m < d ? d - 1 : d);
            Stretch(RT(go));
            var img = Img(go, null, new Color(0, 0, 0, 0), Image.Type.Simple);
            img.raycastTarget = false;
            img.enabled = false;   // 창이 열렸을 때만 켠다

            var sources = new List<Graphic>();
            foreach (var name in DockTabPanels)
            {
                var p = Find(root, name);
                var g = p != null ? p.GetComponent<Image>() : null;
                if (g == null) { log.Add("도크 그늘: 창 바탕 없음 " + name); continue; }
                Ensure<R0926DimSource>(p);
                sources.Add(g);
            }
            var dd = Ensure<R0926DockDim>(go);
            var so = new SerializedObject(dd);
            so.FindProperty("dock").objectReferenceValue = RT(dock);
            var arr = so.FindProperty("sources");
            arr.arraySize = sources.Count;
            for (int i = 0; i < sources.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = sources[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(go);
            log.Add("도크 그늘: 창 바탕 " + sources.Count + "개를 도크 아래 한 장으로");
        }

        private static Material GlassMaterial(List<string> log)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GlassMatPath);
            if (mat != null) return mat;
            var sh = Shader.Find("Hidden/R0926/LiquidGlass");
            if (sh == null) { log.Add("물방울 유리: LiquidGlass 셰이더 없음"); return null; }
            Directory.CreateDirectory(Path.GetDirectoryName(GlassMatPath));
            mat = new Material(sh) { name = "R0926LiquidGlass" };
            AssetDatabase.CreateAsset(mat, GlassMatPath);
            log.Add("물방울 유리: 재질 만듦");
            return mat;
        }
    }
}
