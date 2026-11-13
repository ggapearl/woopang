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

        private static void ApplyGlass(Transform root, List<string> log)
        {
            var mat = GlassMaterial(log);
            var blur = AssetDatabase.LoadAssetAtPath<Shader>(GlassBlurPath);
            if (mat == null || blur == null) { log.Add("물방울 유리: 재질/흐림 셰이더 없음"); return; }

            var covers = new List<GameObject>();
            foreach (var path in GlassCovers)
            {
                int cut = path.IndexOf('/');
                var top = Find(root, cut < 0 ? path : path.Substring(0, cut));
                var c = top != null && cut >= 0 ? top.transform.Find(path.Substring(cut + 1))?.gameObject : top;
                if (c != null) covers.Add(c); else log.Add("물방울 유리: 덮는 창 없음 " + path);
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
                var cv = so.FindProperty("covers");
                cv.arraySize = covers.Count;
                for (int i = 0; i < covers.Count; i++) cv.GetArrayElementAtIndex(i).objectReferenceValue = covers[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(img);
                n++;
            }
            log.Add("물방울 유리 " + n + "곳");
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
