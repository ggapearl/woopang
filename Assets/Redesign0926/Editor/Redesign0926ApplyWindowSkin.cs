using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 디스플레이 선택 (S8 · 11-23 대표님) — 창 바탕 · 하단 탭 · 알림 알약에 R0926WindowSkin 을 붙인다.
    /// 「하단 탭 창(목록 · 추가 · 보물찾기 · 메세지 · 프로필)에 네온과 물방울」 + 「'하단탭' 의 경우 네온모드 설정시 같이 네온」
    /// + 「기타 알림이나 다른 모달, 메세지창 등에 있어서도 전체적으로 동일하게」.
    /// 창 = 크고 짙은 둥근 바탕 (아래 규칙으로 훑는다 — 바깥 창 안의 창은 바깥 창이 맞춘다) · 이미 물방울 유리(R0926Glass)인 바탕은 네온 테만 덧댄다.
    /// 둘 다 끄면(기본 모양) 아무것도 바뀌지 않는다. 고른 것 · 뺀 것은 Library/R0926WindowSkinAudit.txt 에 적는다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string WindowSkinMatPath = "Assets/Redesign0926/Materials/R0926WindowSkin.mat";
        private const string WindowSkinAuditPath = "Library/R0926WindowSkinAudit.txt";
        private const string NeonRimName = "NeonRim0926";

        // 창으로 보지 않는 곳 — 시작화면 · 첫 안내 · 사진 크게 보기 · 사진 자르기 (제 모양이 따로 있다)
        private static readonly string[] WindowSkip = { "Splash0926", "FirstTimeGuidePanel", "GuideCards0926", "ChatPhotoView0926", "PhotoCrop0926" };
        private static readonly string[] WindowBoxSprites = { "square", "Square_Sero_white", "UISprite", "Background" };

        private static void ApplyWindowSkin(Transform root, List<string> log)
        {
            var mat = WindowSkinMaterial(log);
            var blur = AssetDatabase.LoadAssetAtPath<Shader>(GlassBlurPath);
            if (mat == null || blur == null) { log.Add("창 모양: 재질/흐림 셰이더 없음"); return; }
            var audit = new StringBuilder();
            audit.AppendLine("창 모양 (S8) — " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Vector2 cs = ((RectTransform)root).rect.size;
            audit.AppendLine($"캔버스 {cs.x:0}x{cs.y:0}");

            // 1) 창 고르기
            var picks = new List<Image>();
            foreach (var im in root.GetComponentsInChildren<Image>(true))
            {
                string why = WindowReject(im, root, cs);
                if (why == null) picks.Add(im);
                else if (why.Length > 0) audit.AppendLine("  뺌 " + SkinPath(im.transform, root) + " — " + why);
            }
            var outer = new List<Image>();
            foreach (var im in picks)
            {
                Image host = null;
                foreach (var o in picks) if (o != im && im.transform.IsChildOf(o.transform)) { host = o; break; }
                if (host != null) audit.AppendLine("  안쪽 창 " + SkinPath(im.transform, root) + " — " + host.name + " 이 맞춘다");
                else outer.Add(im);
            }

            // 덮는 창 (GlassCovers) — 이 창보다 먼저 그려지면서 조상이 아닌 것
            var coverObjs = new List<GameObject>();
            foreach (var path in GlassCovers)
            {
                int cut = path.IndexOf('/');
                var top = Find(root, cut < 0 ? path : path.Substring(0, cut));
                var c = top != null && cut >= 0 ? top.transform.Find(path.Substring(cut + 1))?.gameObject : top;
                if (c != null) coverObjs.Add(c);
            }

            // 2) 붙이기 — 예전에 붙였는데 이번에 안 고른 것은 뗀다 (다시 적용해도 같게)
            var keep = new HashSet<R0926WindowSkin>();
            int nWin = 0, nRim = 0;
            audit.AppendLine("창:");
            foreach (var im in outer)
            {
                if (im.GetComponent<R0926Glass>() != null)
                {
                    keep.Add(AddNeonRim(im, mat, blur));
                    audit.AppendLine("  테 " + SkinPath(im.transform, root) + " — 물방울 유리 바탕이라 네온 테만");
                    nRim++;
                    continue;
                }
                var sk = Ensure<R0926WindowSkin>(im.gameObject);
                var mine = new List<GameObject>();
                foreach (var c in coverObjs)
                {
                    if (c == im.gameObject || im.transform.IsChildOf(c.transform) || c.transform.IsChildOf(im.transform)) continue;
                    if (EditorDrawnBefore(c.transform, im.transform)) mine.Add(c);
                }
                var so = new SerializedObject(sk);
                so.FindProperty("kind").enumValueIndex = (int)R0926WindowSkin.Kind.Window;
                so.FindProperty("skin").objectReferenceValue = mat;
                so.FindProperty("blur").objectReferenceValue = blur;
                so.FindProperty("tint").floatValue = Mathf.Max(im.color.a, 0.05f);
                so.FindProperty("glow").colorValue = R0926WindowSkin.Blue;
                so.FindProperty("shapeFrom").objectReferenceValue = null;
                so.FindProperty("fillContent").boolValue = true;
                var cv = so.FindProperty("covers");
                cv.arraySize = mine.Count;
                for (int i = 0; i < mine.Count; i++) cv.GetArrayElementAtIndex(i).objectReferenceValue = mine[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                if (im.material != null && im.material.shader == mat.shader) im.material = null;   // 혹시 재질이 저장돼 있었으면 예전 그림으로
                EditorUtility.SetDirty(im);
                keep.Add(sk);
                nWin++;
                audit.AppendLine("  창 " + SkinPath(im.transform, root) + " — " + Describe(im, root) + " · 덮는 창 " + mine.Count
                                 + (im.GetComponent<Mask>() != null ? " · ⚠마스크" : "") + OtherEffects(im));
            }

            // 3) 하단 탭 · 알림 알약 — 네온 테 (네온 모드에서만 그린다)
            var hunt = Find(root, "HuntUploadPage");
            var rims = new (string what, Transform t)[]
            {
                ("하단 탭", Find(root, "Dock0926")?.transform),
                ("토스트", root.Find("Toast0926/Stack/Template")),
                ("업로드 안내", root.Find("WarningText")),
                ("보물 알림", root.Find("HuntToast0926/Pill")),
                ("보물찾기 카드 알림", hunt != null ? hunt.transform.Find("Toast0926") : null),
                ("숨김 알림", root.Find("Undo0926")),
            };
            audit.AppendLine("테:");
            foreach (var (what, t) in rims)
            {
                var img = t != null ? t.GetComponent<Image>() : null;
                if (img == null) { log.Add("창 모양: " + what + " 없음"); audit.AppendLine("  없음 " + what); continue; }
                keep.Add(AddNeonRim(img, mat, blur));
                nRim++;
                audit.AppendLine("  테 " + what + " " + SkinPath(t, root) + (img.GetComponent<Mask>() != null ? " · ⚠마스크(빛이 잘림)" : "") + ClipAbove(t, root));
            }

            int removed = 0;
            foreach (var sk in root.GetComponentsInChildren<R0926WindowSkin>(true))
            {
                if (keep.Contains(sk)) continue;
                if (sk.SkinKind == R0926WindowSkin.Kind.Row) continue;   // 목록 줄은 실행 중에 생긴다 (씬에는 없다)
                audit.AppendLine("  뗌 " + SkinPath(sk.transform, root));
                if (sk.gameObject.name == NeonRimName) Object.DestroyImmediate(sk.gameObject);
                else Object.DestroyImmediate(sk);
                removed++;
            }
            foreach (var f in root.GetComponentsInChildren<R0926WindowFill>(true)) Object.DestroyImmediate(f);   // 실행 중에만 붙는다 — 혹시 저장돼 있으면

            // 4) 목록 창 빛 = 고른 칩 색 · 목록 강조 줄 네온
            var sheet = Find(root, "Sheet0926");
            var sheetSkin = sheet != null ? sheet.GetComponent<R0926WindowSkin>() : null;
            if (sheetSkin != null)
            {
                var lg = Ensure<R0926ListGlow>(sheet);
                var so = new SerializedObject(lg);
                so.FindProperty("skin").objectReferenceValue = sheetSkin;
                so.FindProperty("modes").objectReferenceValue = sheet.GetComponentInChildren<R0926SheetModes>(true);
                var chips = new List<R0926FilterChip>(sheet.GetComponentsInChildren<R0926FilterChip>(true));
                var arr = so.FindProperty("chips");
                arr.arraySize = chips.Count;
                for (int i = 0; i < chips.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = chips[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                audit.AppendLine($"목록 빛: 칩 {chips.Count}개 · 보기 바꾸기 {(lg != null && so.FindProperty("modes").objectReferenceValue != null ? "있음" : "없음")}");
            }
            else log.Add("창 모양: 목록 창(Sheet0926)에 창 모양이 안 붙음");
            int nRows = 0;
            foreach (var pr in root.GetComponentsInChildren<R0926PlaceRows>(true))
            {
                var so = new SerializedObject(pr);
                so.FindProperty("neonSkin").objectReferenceValue = mat;
                so.ApplyModifiedPropertiesWithoutUndo();
                nRows++;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(WindowSkinAuditPath));
            File.WriteAllText(WindowSkinAuditPath, audit.ToString(), new UTF8Encoding(false));
            log.Add($"창 모양: 창 {nWin}곳 · 네온 테 {nRim}곳 · 목록 줄 {nRows} · 뗌 {removed} (기록 {WindowSkinAuditPath})");
        }

        // 창이 아니면 이유 (조용히 뺄 것은 "") · 창이면 null
        private static string WindowReject(Image im, Transform root, Vector2 cs)
        {
            if (im == null || im.GetType() != typeof(Image)) return "";
            for (var t = im.transform; t != null && t != root; t = t.parent)
                if (System.Array.IndexOf(WindowSkip, t.name) >= 0) return "";
            var top = TopCanvas(im.transform);
            if (top == null || top.renderMode != RenderMode.ScreenSpaceOverlay) return "";
            var c = im.color;
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            Vector2 sz = CanvasSize(im.rectTransform, root);
            var sp = im.sprite;
            bool pill = sp != null && sp.name.StartsWith("r0926_pill") && !sp.name.EndsWith("_ring") && im.type == Image.Type.Sliced;
            bool glass = im.GetComponent<R0926Glass>() != null;
            // 조금 모자란 것도 적어 둔다 (빠진 창이 없는지 보려고) — 둥근 알약 그림 · 물방울 유리 바탕
            bool near = (pill || glass) && sz.x >= 0.5f * cs.x && sz.y >= 0.08f * cs.y;
            if (mx > 0.25f || c.a < 0.85f)
            {
                if (glass && near && mx <= 0.25f) return sz.x >= 0.6f * cs.x ? null : $"작은 유리 {sz.x:0}x{sz.y:0}";
                return near ? $"색 {c.r:0.00},{c.g:0.00},{c.b:0.00},{c.a:0.00} · {sz.x:0}x{sz.y:0}" : "";
            }
            if (sz.x < 0.5f * cs.x || sz.y < 0.1f * cs.y) return near ? $"작음 {sz.x:0}x{sz.y:0}" : "";   // 작은 모달(사진 고르기 · 「···」 메뉴 · 처리 중)까지
            if (mx <= 0.02f) return "검정 그늘";
            if (im.GetComponent<R0926DimSource>() != null || im.GetComponent<R0926DockDim>() != null) return "창 뒤 그늘";
            if (sp != null && im.type != Image.Type.Sliced && im.type != Image.Type.Tiled && System.Array.IndexOf(WindowBoxSprites, sp.name) < 0)
                return "그림 (" + sp.name + ")";
            if (im.type == Image.Type.Filled) return "채우기 그림";
            if (sz.x >= 0.97f * cs.x && sz.y >= 0.9f * cs.y) return $"전체 화면 쪽 {sz.x:0}x{sz.y:0}";
            // 창은 둥근 바탕 — 네모 바탕(입력줄 · 이모지 판처럼 화면 끝에 붙은 것)은 창 테두리가 어울리지 않는다
            if (!pill && !(sp != null && (im.type == Image.Type.Sliced || im.type == Image.Type.Tiled) && sp.border.x > 0f))
                return $"네모 바탕 {(sp != null ? sp.name : "그림 없음")} {sz.x:0}x{sz.y:0}";
            return null;
        }

        private static Canvas TopCanvas(Transform t)
        {
            Canvas top = null;
            foreach (var cv in t.GetComponentsInParent<Canvas>(true)) top = cv;   // 가까운 것부터 — 마지막이 맨 위
            return top;
        }

        private static readonly Vector3[] skinCorners = new Vector3[4];

        private static Vector2 CanvasSize(RectTransform rt, Transform root)
        {
            rt.GetWorldCorners(skinCorners);
            Vector3 a = root.InverseTransformPoint(skinCorners[0]), b = root.InverseTransformPoint(skinCorners[2]);
            return new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        // 에디터용 그리는 순서 — 꺼진 창도 위 캔버스를 찾는다 (실행 중에는 R0926WindowSkin.DrawnBefore)
        private static bool EditorDrawnBefore(Transform a, Transform b)
        {
            int sa = EditorSortOrder(a), sb = EditorSortOrder(b);
            if (sa != sb) return sa < sb;
            var pa = new List<int>();
            var pb = new List<int>();
            for (var t = a; t != null; t = t.parent) pa.Insert(0, t.GetSiblingIndex());
            for (var t = b; t != null; t = t.parent) pb.Insert(0, t.GetSiblingIndex());
            int n = Mathf.Min(pa.Count, pb.Count);
            for (int i = 0; i < n; i++) if (pa[i] != pb[i]) return pa[i] < pb[i];
            return pa.Count < pb.Count;
        }

        private static int EditorSortOrder(Transform t)
        {
            foreach (var cv in t.GetComponentsInParent<Canvas>(true))
                if (cv.overrideSorting || cv.transform.parent == null || cv.transform.parent.GetComponentInParent<Canvas>(true) == null) return cv.sortingOrder;
            return 0;
        }

        private static R0926WindowSkin AddNeonRim(Image host, Material mat, Shader blur)
        {
            var go = FindOrCreate(host.transform, NeonRimName);
            go.transform.SetAsFirstSibling();   // 바탕 바로 위 · 아이콘 · 글자 아래
            Stretch(RT(go));
            var le = Ensure<LayoutElement>(go);
            le.ignoreLayout = true;   // 토스트는 가로 배치 안 — 글자 폭 계산에 끼지 않게
            EditorUtility.SetDirty(le);
            var img = Img(go, host.sprite, Color.white, host.type == Image.Type.Filled ? Image.Type.Simple : host.type, host.pixelsPerUnitMultiplier);
            img.raycastTarget = false;
            var sk = Ensure<R0926WindowSkin>(go);
            var so = new SerializedObject(sk);
            so.FindProperty("kind").enumValueIndex = (int)R0926WindowSkin.Kind.Rim;
            so.FindProperty("skin").objectReferenceValue = mat;
            so.FindProperty("blur").objectReferenceValue = blur;
            so.FindProperty("tint").floatValue = 1f;
            so.FindProperty("glow").colorValue = R0926WindowSkin.Blue;
            so.FindProperty("shapeFrom").objectReferenceValue = host;
            so.FindProperty("fillContent").boolValue = false;
            so.FindProperty("covers").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(go);
            return sk;
        }

        private static Material WindowSkinMaterial(List<string> log)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WindowSkinMatPath);
            if (mat != null) return mat;
            var sh = Shader.Find("Hidden/R0926/WindowSkin");
            if (sh == null) { log.Add("창 모양: WindowSkin 셰이더 없음"); return null; }
            Directory.CreateDirectory(Path.GetDirectoryName(WindowSkinMatPath));
            mat = new Material(sh) { name = "R0926WindowSkin" };
            AssetDatabase.CreateAsset(mat, WindowSkinMatPath);
            log.Add("창 모양: 재질 만듦");
            return mat;
        }

        private static string SkinPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (; t != null && t != root; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        private static string Describe(Image im, Transform root)
        {
            var c = im.color;
            Vector2 sz = CanvasSize(im.rectTransform, root);
            return $"{sz.x:0}x{sz.y:0} · 색 {c.r:0.000},{c.g:0.000},{c.b:0.000},{c.a:0.00} · {(im.sprite != null ? im.sprite.name : "그림 없음")} {im.type}"
                   + (im.gameObject.activeInHierarchy ? "" : " · 꺼져 있음");
        }

        private static string OtherEffects(Image im)
        {
            var names = new List<string>();
            foreach (var e in im.GetComponents<BaseMeshEffect>()) if (!(e is R0926WindowSkin)) names.Add(e.GetType().Name);
            return names.Count > 0 ? " · ⚠다른 모양 효과 " + string.Join(",", names) : "";
        }

        private static string ClipAbove(Transform t, Transform root)
        {
            for (var p = t.parent; p != null && p != root; p = p.parent)
            {
                if (p.GetComponent<RectMask2D>() != null) return " · 위에 잘라내기 " + p.name;
                if (p.GetComponent<Mask>() != null) return " · 위에 마스크 " + p.name;
            }
            return "";
        }
    }
}
