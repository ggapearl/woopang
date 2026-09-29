using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v10: 시작화면 — 렌즈가 지나가면 나라별 숨은 문양 + 엠블럼 · WOOPANG · SEE THE UNSEEN.
    /// 씬에는 꺼진 채 저장하고 R0926SplashBoot(마커)가 앱을 켤 때 한 번 켠다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string SplashDir = "Assets/Redesign0926/Resources/Splash0926/";
        private const string WordmarkPath = "Assets/Resources/Textures/woopang_logo.png";

        private static void ApplySplash(Transform root, List<string> log)
        {
            EnsureFontFallback(log);
            EnsureSplashImports(log);
            RemoveOldSplash(root, log);

            var sp = FindOrCreate(root, "Splash0926");
            sp.transform.SetAsLastSibling();
            Stretch(RT(sp));
            var cv = Ensure<Canvas>(sp);
            var cso = new SerializedObject(cv);
            cso.FindProperty("m_OverrideSorting").boolValue = true;
            // 맨 위 (최댓값) — 토스트 9500 · 사진 창 30000 · 입력창 거울 32001 보다도 위. 도는 동안 오브젝트·인디케이터·도크가 비치면 안 된다
            cso.FindProperty("m_SortingOrder").intValue = 32767;
            cso.ApplyModifiedPropertiesWithoutUndo();
            Ensure<GraphicRaycaster>(sp);
            var group = Ensure<CanvasGroup>(sp);
            var bg = Img(sp, null, new Color(0.043f, 0.051f, 0.059f, 1f), Image.Type.Simple);
            bg.raycastTarget = true;   // 도는 동안 뒤 화면을 누르지 못하게

            // 렌즈 (원형 마스크) + 그 안의 문양
            var lens = FindOrCreate(sp.transform, "Lens0926");
            SetRect(RT(lens), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 900));
            Img(lens, Spr("r0926_circle"), Color.white, Image.Type.Simple).raycastTarget = false;
            Ensure<Mask>(lens).showMaskGraphic = false;
            var motif = FindOrCreate(lens.transform, "Motif");
            SetRect(RT(motif), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 3120));
            var mImg = Ensure<RawImage>(motif); mImg.texture = null; mImg.raycastTarget = false; mImg.color = Color.white;

            // 렌즈 밖 — 같은 문양을 옅게 깐다 (훑는 동안 바탕이 완전한 검정이 아니게). 렌즈보다 아래(먼저) 그린다
            var faint = FindOrCreate(sp.transform, "MotifFaint0926");
            SetRect(RT(faint), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 3120));
            var fImg = Ensure<RawImage>(faint); fImg.texture = null; fImg.raycastTarget = false; fImg.color = new Color(1f, 1f, 1f, 0f);
            faint.transform.SetSiblingIndex(0);

            var ring = FindOrCreate(sp.transform, "Ring0926");
            SetRect(RT(ring), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 920));
            var ringImg = Img(ring, Spr("r0926_ring"), Color.white, Image.Type.Simple);
            ringImg.raycastTarget = false;

            var dim = FindOrCreate(sp.transform, "Dimmer0926");
            Stretch(RT(dim));
            Img(dim, null, new Color(0.043f, 0.051f, 0.059f, 0f), Image.Type.Simple).raycastTarget = false;

            // 엠블럼 · WOOPANG · SEE THE UNSEEN · 나라 말 한 줄
            var emblem = FindOrCreate(sp.transform, "Emblem0926");
            SetRect(RT(emblem), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(340, 340));
            var eg = Ensure<CanvasGroup>(emblem);
            var eImg = Img(emblem, Spr("r0926_emblem"), Color.white, Image.Type.Simple);
            eImg.preserveAspect = true; eImg.raycastTarget = false;

            // 워드마크 — 엠블럼(아래 끝 y 160)과 너무 붙어 보여서 조금 작게(620→584) 하고 아래로 내려 틈을 둔다.
            // 그림(1000x340)의 위아래 여백 43px 을 빼면 글자 윗선이 y 112 — 엠블럼과 약 48 떨어진다
            var word = FindOrCreate(sp.transform, "Wordmark0926");
            SetRect(RT(word), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 38), new Vector2(584, 199));
            var wg = Ensure<CanvasGroup>(word);
            var wImg = Ensure<RawImage>(word);
            wImg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WordmarkPath);
            wImg.raycastTarget = false;
            if (wImg.texture == null) log.Add("woopang_logo 없음: " + WordmarkPath);

            var copy = FindOrCreate(sp.transform, "Copy0926");
            SetRect(RT(copy), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -112), new Vector2(1300, 80));
            var cg = Ensure<CanvasGroup>(copy);
            var ct = Txt(copy, "S E E   T H E   U N S E E N", 50, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            ct.supportRichText = true; ct.horizontalOverflow = HorizontalWrapMode.Overflow;
            TextShade(copy);

            var bar = FindOrCreate(sp.transform, "Bar0926");
            SetRect(RT(bar), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -172), new Vector2(120, 6));
            var barImg = Img(bar, Spr("r0926_pill"), Color.white, Image.Type.Sliced, 64f / 3f);
            barImg.raycastTarget = false;

            var line = FindOrCreate(sp.transform, "Line0926");
            SetRect(RT(line), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -237), new Vector2(1300, 70));
            var lg = Ensure<CanvasGroup>(line);
            var lt = Txt(line, "보이지 않던 곳이, 보인다", 44, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            lt.horizontalOverflow = HorizontalWrapMode.Overflow;
            TextShade(line);   // 밝은 한지 창살 위에서도 읽히게

            var splash = Ensure<R0926Splash>(sp);
            var so = new SerializedObject(splash);
            so.FindProperty("root").objectReferenceValue = group;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("lens").objectReferenceValue = RT(lens);
            so.FindProperty("motif").objectReferenceValue = mImg;
            so.FindProperty("motifFaint").objectReferenceValue = fImg;
            so.FindProperty("ring").objectReferenceValue = ringImg;
            so.FindProperty("dimmer").objectReferenceValue = dim.GetComponent<Image>();
            so.FindProperty("emblem").objectReferenceValue = eg;
            so.FindProperty("wordmark").objectReferenceValue = wg;
            so.FindProperty("copy").objectReferenceValue = cg;
            so.FindProperty("copyText").objectReferenceValue = ct;
            so.FindProperty("bar").objectReferenceValue = RT(bar);
            so.FindProperty("barImage").objectReferenceValue = barImg;
            so.FindProperty("line").objectReferenceValue = lg;
            so.FindProperty("lineText").objectReferenceValue = lt;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 씬에는 꺼진 채로 — 앱을 켤 때 마커의 R0926SplashBoot 가 켠다
            var marker = Find(root, "Redesign0926Marker");
            if (marker != null)
            {
                var boot = Ensure<R0926SplashBoot>(marker);
                var bso = new SerializedObject(boot);
                bso.FindProperty("splash").objectReferenceValue = sp;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }
            else log.Add("splash: 마커 없음 — 시작화면이 켜지지 않는다");
            sp.SetActive(false);
            log.Add("splash ok");
        }

        /// <summary>
        /// 예전 시작 이미지(INTRO_IMAGE, 2.5초)를 뺀다 — 시작화면이 두 번 뜨고, 그 스크립트(SplashImagePlayer)가
        /// 메인 Canvas 순서를 3333 → 10000 으로 올려 이 시작화면(과 순서 4000 인 창·배너)이 도크·인디케이터 아래로 깔렸다.
        /// 0529 씬과 프리팹·스크립트는 그대로 둔다.
        /// </summary>
        private static void RemoveOldSplash(Transform root, List<string> log)
        {
            foreach (var go in root.gameObject.scene.GetRootGameObjects())
                if (go.GetComponentInChildren<SplashImagePlayer>(true) != null)
                {
                    Object.DestroyImmediate(go);
                    log.Add("예전 시작 이미지(SplashImagePlayer) 삭제");
                }
            var raw = root.Find("SplashVideoRawImage");
            if (raw != null)
            {
                Object.DestroyImmediate(raw.gameObject);
                log.Add("예전 시작 이미지(SplashVideoRawImage) 삭제");
            }
        }

        /// <summary>
        /// 앱 글꼴(애플 SD 산돌고딕 Neo)에 없는 글자 — 스페인어 악센트(á é ñ), 중국어 간체 일부(见 这 图), 가운뎃점(·) 등 —
        /// 을 기기 글꼴에서 가져오도록 대체 글꼴 이름을 순서대로 적는다. (없으면 기기마다 정해진 기본값에 맡겨져 들쭉날쭉)
        /// iOS: Apple SD Gothic Neo → PingFang → Hiragino → Helvetica / 안드로이드: Noto CJK → Roboto → Droid Fallback
        /// </summary>
        private static void EnsureFontFallback(List<string> log)
        {
            const string path = "Assets/Resources/Fonts/AppleSDGothicNeoM.ttf";
            var ti = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (ti == null) { log.Add("글꼴 없음: " + path); return; }
            string[] want =
            {
                "AppleSDGothicNeoM00", "Apple SD Gothic Neo", "PingFang SC", "PingFang TC", "Hiragino Sans", "Helvetica Neue",
                "Noto Sans CJK KR", "Noto Sans CJK SC", "Noto Sans CJK JP", "NotoSansCJK-Regular", "Roboto", "Droid Sans Fallback",
            };
            // 속성(ti.fontNames)으로 넣으면 저장될 때 글꼴 자기 이름 하나로 되돌아갔다 → 직렬화 필드에 직접 쓴다
            var so = new SerializedObject(ti);
            var arr = so.FindProperty("fontNames") ?? so.FindProperty("m_FontNames");
            if (arr == null) { log.Add("글꼴: fontNames 속성 없음"); return; }
            bool same = arr.arraySize == want.Length;
            for (int i = 0; same && i < want.Length; i++) same = arr.GetArrayElementAtIndex(i).stringValue == want[i];
            if (same) return;
            arr.arraySize = want.Length;
            for (int i = 0; i < want.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = want[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            ti.SaveAndReimport();
            log.Add("font fallback set (" + want.Length + ")");
        }

        private static void TextShade(GameObject go)
        {
            var s = Ensure<Shadow>(go);
            s.effectColor = new Color(0f, 0f, 0f, 0.75f);
            s.effectDistance = new Vector2(0f, -3f);
            var o = Ensure<Outline>(go);
            o.effectColor = new Color(0f, 0f, 0f, 0.35f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        private static void EnsureSplashImports(List<string> log)
        {
            // 지금은 영어(우주) 한 장만 Resources 에 둔다 — 나머지는 Splash0926_other (빌드에 안 들어감)
            foreach (var key in new[] { "en", "kr", "jp", "cn", "es" })
            {
                string path = SplashDir + "motif_" + key + ".jpg";
                if (key != "en" && !System.IO.File.Exists(path)) continue;
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
                if (ti == null) { log.Add("문양 없음: " + path); continue; }
                bool dirty = ti.textureType != TextureImporterType.Default || ti.mipmapEnabled || ti.maxTextureSize != 2048
                             || ti.npotScale != TextureImporterNPOTScale.None || ti.textureCompression != TextureImporterCompression.CompressedHQ;
                if (!dirty) continue;
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = 2048;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
                log.Add("import motif_" + key);
            }
        }
    }
}
