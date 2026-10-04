using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v14 (0930 시안):
    ///  · 목록·추가·메시지·대화 창이 도크 윗선에서 올라오고 도크 윗선 뒤로 내려간다 (Clip0926 틀 + 자기 높이만큼 미끄러짐)
    ///  · 프로필은 제자리에서 페이드 — 도크 프로필 칸을 다시 누르면 닫힌다 · 테두리 매끈하게 · 팔로잉 숫자 크게
    ///  · 장소 추가 옆 3D 모델 카드가 오른쪽 끝에 살짝 보이게 (겹치지 않게)
    ///  · 목록 | 지도 | 설정 옆으로 밀기 · 시작화면 별·성운 · 상태 알약 · 첫 안내 스포트라이트
    ///  · 하늘 칩·인디케이터 위쪽 한계를 노치 바로 아래로 · 날씨 그림·예보 · 댓글 입력줄 · X → '삭제'
    /// 틀(Clip0926·ListPage0926)은 매번 풀었다가(Unwrap0930 — 맨 처음) 다시 씌운다: 앞 단계들이 시트를 패널 기준으로 다시 잡기 때문
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string Clip = "Clip0926";
        private const string ListPage = "ListPage0926";
        private const float SheetGapTall = 250f;    // 목록·추가 — 예전 0.92 높이 (3120 기준)
        private const float SheetGapMsg = 437f;     // 메시지·대화 — 예전 0.86 높이
        private const float SheetOverDock = 14f;    // 틀 아랫선 위로 띄우는 간격 (도크 윗선 + 24)
        private const float BottomTrim = 80f;       // 아이폰에서 도크·창을 홈 막대 쪽으로 내리는 양

        // ── 맨 처음: 지난번에 씌운 틀을 벗긴다 ─────────────────────────
        private static void Unwrap0930(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || (t.name != Clip && t.name != ListPage)) continue;
                int at = t.GetSiblingIndex();
                var kids = new List<Transform>();
                foreach (Transform k in t) kids.Add(k);
                foreach (var k in kids) { k.SetParent(t.parent, false); k.SetSiblingIndex(at++); }
            }
        }

        private static void Apply0930(Transform root, List<string> log)
        {
            ClipSheets(root, log);
            ProfileFade(root, log);
            UploadPeek(root, log);
            ListPages(root, log);
            SplashStars(root, log);
            StatusPills(root, log);
            GuideOverlay(root, log);
            SkyTop(root, log);
            WeatherBoard(root, log);
            CommentBar(root, log);
            Apply1004(root, log);   // 10-04 아이폰 확인 뒤 — 도크 칸 · 손잡이 · 프로필 카드 · 입력줄 · 날씨 크기
            var marker = Find(root, "Redesign0926Marker");
            var close = marker != null ? marker.GetComponent<R0926IndicatorClose>() : null;
            if (close != null)
            {
                var so = new SerializedObject(close);
                so.FindProperty("pill").objectReferenceValue = Spr("r0926_pill");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // 아래쪽 안전영역: 아이폰은 홈 막대 쪽으로 조금 내린다 (도크·창·칩이 함께 — 사이 간격 유지)
            foreach (var si in root.GetComponentsInChildren<R0926SafeInset>(true))
            {
                if (si.CurrentEdge != R0926SafeInset.Edge.Bottom) continue;
                var so = new SerializedObject(si);
                so.FindProperty("trim").floatValue = BottomTrim;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("0930 ok");
        }

        // ============================================================
        // 창 — 도크 윗선 틀 안에서 오르내린다
        // ============================================================
        private static void ClipSheets(Transform root, List<string> log)
        {
            var defs = new (string panel, string sheet, float gap, float side)[]
            {
                ("ListPanel", "Sheet0926", SheetGapTall, CardSide),
                ("UploadPage", "UploadSheet0926", SheetGapTall, 0f),
                ("MessagePanel", "Background", SheetGapMsg, CardSide),
                ("ChatRoomPanel", "Background", SheetGapMsg, CardSide),
            };
            foreach (var d in defs)
            {
                var panel = Find(root, d.panel);
                var sheet = panel != null ? panel.transform.Find(d.sheet) : null;
                if (sheet == null) { log.Add("틀: 없음 " + d.panel + "/" + d.sheet); continue; }

                var clip = FindOrCreate(panel.transform, Clip);
                clip.transform.SetSiblingIndex(sheet.GetSiblingIndex());
                sheet.SetParent(clip.transform, false);
                var crt = RT(clip);
                crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.pivot = new Vector2(0.5f, 0.5f);
                crt.offsetMin = new Vector2(0, DockBottom + DockHeight + 10); crt.offsetMax = Vector2.zero;
                crt.localScale = Vector3.one;
                Ensure<RectMask2D>(clip);
                Ensure<R0926SafeInset>(clip).SetEdge(R0926SafeInset.Edge.Bottom, R0926SafeInset.Mode.Stretch);
                EditorUtility.SetDirty(crt);

                // 시트는 틀 기준으로 — 아래는 틀 아랫선 위, 위는 예전 높이
                foreach (var si in sheet.GetComponents<R0926SafeInset>()) Object.DestroyImmediate(si);
                var srt = (RectTransform)sheet;
                srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.pivot = new Vector2(0.5f, 0.5f);
                srt.offsetMin = new Vector2(d.side, SheetOverDock); srt.offsetMax = new Vector2(-d.side, -d.gap);
                EditorUtility.SetDirty(srt);

                // 자기 높이만큼 아래(도크 윗선 뒤)에서 통째로 올라온다
                var slide = Ensure<R0926SlideIn>(sheet.gameObject);
                var sso = new SerializedObject(slide);
                sso.FindProperty("fullHeight").boolValue = true;
                sso.FindProperty("duration").floatValue = 0.34f;
                sso.ApplyModifiedPropertiesWithoutUndo();

                // 끌어 내릴 때 옅어질 그늘은 패널 (틀에는 그림이 없다)
                var sd = sheet.GetComponent<R0926SwipeDismiss>();
                var pImg = panel.GetComponent<Image>();
                if (sd != null && pImg != null)
                {
                    var dso = new SerializedObject(sd);
                    dso.FindProperty("backdrop").objectReferenceValue = pImg;
                    dso.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            log.Add("sheet clips ok");
        }

        // ============================================================
        // 프로필 — 페이드 · 도크 칸 다시 누르면 닫힘 · 테두리 · 팔로잉 숫자
        // ============================================================
        private static void ProfileFade(Transform root, List<string> log)
        {
            var panel = Find(root, "FullProfilePanel");
            var content = panel != null ? panel.transform.Find("Content") : null;
            if (content == null) { log.Add("프로필 없음"); return; }

            var sd = content.GetComponent<R0926SwipeDismiss>();
            if (sd != null) Object.DestroyImmediate(sd);
            var slide = content.GetComponent<R0926SlideIn>();
            if (slide != null) Object.DestroyImmediate(slide);
            var cg = content.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;

            var fade = Ensure<R0926FadePanel>(panel);
            var fso = new SerializedObject(fade);
            fso.FindProperty("card").objectReferenceValue = (RectTransform)content;
            fso.ApplyModifiedPropertiesWithoutUndo();
            Proxy(root, fade, "FullProfilePanel/Content/CloseButton", log);

            // 도크의 프로필 칸을 다시 누르면 닫힌다 — 프로필 창이 도크보다 위라, 창 안에 도크와 똑같은 틀을 두고 그 칸에 투명 버튼
            var real = content.Find("CloseButton")?.GetComponent<Button>();
            if (real != null)
            {
                var mirror = FindOrCreate(panel.transform, "DockMirror0926");
                mirror.transform.SetAsLastSibling();
                SetRect(RT(mirror), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, DockBottom), new Vector2(-Side * 2, DockHeight));
                Ensure<R0926SafeInset>(mirror).SetEdge(R0926SafeInset.Edge.Bottom);
                var tog = FindOrCreate(mirror.transform, "ProfileToggle0926");
                SetRect(RT(tog), new Vector2(SlotProfile, 0.5f), new Vector2(SlotProfile, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, DockHeight));
                Img(tog, null, new Color(1, 1, 1, 0), Image.Type.Simple).raycastTarget = true;
                var gb = Ensure<Button>(tog);
                gb.transition = Selectable.Transition.None;
                var cp = Ensure<R0926CloseProxy>(tog);
                var cso = new SerializedObject(cp);
                cso.FindProperty("sheet").objectReferenceValue = fade;
                cso.FindProperty("real").objectReferenceValue = real;
                cso.ApplyModifiedPropertiesWithoutUndo();
                ResetListeners(gb);
                UnityEventTools.AddPersistentListener(gb.onClick, cp.Run);
            }

            // 큰 사진 테두리 — 사진 위에 매끈한 고리 (스텐실 마스크의 계단진 가장자리를 덮는다)
            var mask = Find(content, "AvatarMask");
            var outline = Find(content, "AvatarOutline");
            if (mask != null && outline != null)
            {
                float ms = RT(mask).sizeDelta.x;
                float rs = (ms / 2f - 4f) / 0.82f * 2f;
                var ort = RT(outline);
                ort.anchorMin = RT(mask).anchorMin; ort.anchorMax = RT(mask).anchorMax; ort.pivot = RT(mask).pivot;
                ort.anchoredPosition = RT(mask).anchoredPosition;
                ort.sizeDelta = new Vector2(rs, rs);
                var oi = outline.GetComponent<Image>();
                oi.sprite = Spr("r0926_ring_big"); oi.type = Image.Type.Simple; oi.preserveAspect = true; oi.raycastTarget = false;
                EditorUtility.SetDirty(oi);
                if (outline.transform.parent == mask.transform.parent)
                    outline.transform.SetSiblingIndex(mask.transform.GetSiblingIndex() + 1);
            }

            // 팔로잉 · 팔로워 — 글자·숫자를 키우고 위로
            var stats = Find(content, "FollowStats");
            if (stats != null)
            {
                var srt = RT(stats);
                if (srt.anchoredPosition.y < -140f) srt.anchoredPosition = new Vector2(srt.anchoredPosition.x, -120f);
                foreach (var n in new[] { "FollowingBtn", "FollowersBtn" })
                {
                    var b = Find(stats.transform, n);
                    if (b == null) continue;
                    foreach (var t in b.GetComponentsInChildren<Text>(true))
                    {
                        t.fontSize = 54; t.fontStyle = FontStyle.Bold; t.color = Ink;
                        t.verticalOverflow = VerticalWrapMode.Overflow; t.lineSpacing = 1.05f;
                        EditorUtility.SetDirty(t);
                    }
                }
            }
            log.Add("profile fade ok");
        }

        // ============================================================
        // 장소 추가 — 3D 모델 카드가 오른쪽 끝에 살짝 (겹치지 않게)
        //   왼쪽 여백 44 · 카드 사이 40 · 옆 카드 보이는 폭 110 → 카드 폭 = 전체 − 194, 넘기는 거리 = 전체 − 154
        // ============================================================
        private static void UploadPeek(Transform root, List<string> log)
        {
            var page = Find(root, "UploadPage");
            if (page == null) return;
            var swipe = FindInScene<SwipePanelController>(page);
            if (swipe != null)
            {
                var so = new SerializedObject(swipe);
                so.FindProperty("panelPreviewAmount").floatValue = 154f;
                so.FindProperty("baseOffsetX").floatValue = -53f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var n in new[] { "CubeUploadPage", "ModelUploadPage" })
            {
                var c = Find(page.transform, n);
                if (c == null) continue;
                RT(c).sizeDelta = new Vector2(-194f, RT(c).sizeDelta.y);
                EditorUtility.SetDirty(RT(c));
            }
            log.Add("upload peek ok");
        }

        // ============================================================
        // 목록 | 지도 | 설정 — 세 쪽을 옆으로 나란히
        // ============================================================
        private static void ListPages(Transform root, List<string> log)
        {
            var sheet = Find(root, "Sheet0926");
            var modes = sheet != null ? sheet.GetComponentInChildren<R0926SheetModes>(true) : null;
            if (sheet == null || modes == null) { log.Add("목록 쪽: 없음"); return; }
            var page = FindOrCreate(sheet.transform, ListPage);
            var slider = sheet.transform.Find("DistanceSliderUI");
            page.transform.SetSiblingIndex(slider != null ? slider.GetSiblingIndex() : 1);
            Stretch(RT(page));
            foreach (var n in new[] { "DistanceSliderUI", "FilterButtonPanel", "Summary0926", "AskAI0926", "Scroll View" })
            {
                var t = sheet.transform.Find(n);
                if (t != null) t.SetParent(page.transform, false);
            }
            Ensure<RectMask2D>(sheet);   // 옆으로 나간 쪽은 시트 밖에서 잘린다

            var so = new SerializedObject(modes);
            var map = sheet.transform.Find("MapPanel0926");
            var set = sheet.transform.Find("SettingsPanel0926");
            SetArray(so.FindProperty("pages"), RT(page), map as RectTransform, set as RectTransform);
            so.FindProperty("viewport").objectReferenceValue = RT(sheet);
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("list pages ok");
        }

        // ============================================================
        // 시작화면 — 반짝이는 별 · 별똥별 · 숨쉬는 성운
        // ============================================================
        private static void SplashStars(Transform root, List<string> log)
        {
            var sp = Find(root, "Splash0926");
            var splash = sp != null ? sp.GetComponent<R0926Splash>() : null;
            if (splash == null) { log.Add("시작화면 없음"); return; }
            var lens = sp.transform.Find("Lens0926");
            var dim = sp.transform.Find("Dimmer0926");

            var neb = FindOrCreate(sp.transform, "Nebula0926");
            if (lens != null) neb.transform.SetSiblingIndex(lens.GetSiblingIndex() + 1);
            SetRect(RT(neb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800, 3400));
            var nImg = Img(neb, Spr("r0926_nebula"), new Color(1, 1, 1, 0.4f), Image.Type.Simple);
            nImg.raycastTarget = false;

            var stars = FindOrCreate(sp.transform, "Stars0926");
            if (dim != null) stars.transform.SetSiblingIndex(dim.GetSiblingIndex() + 1);
            Stretch(RT(stars));
            var sf = Ensure<R0926StarField>(stars);
            var sfo = new SerializedObject(sf);
            sfo.FindProperty("starSprite").objectReferenceValue = Spr("r0926_star");
            sfo.FindProperty("m_RaycastTarget").boolValue = false;
            sfo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(splash);
            so.FindProperty("nebula").objectReferenceValue = nImg;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("splash stars ok");
        }

        // ============================================================
        // 상태 알약 — 노치 아래 (주변 장소 불러오는 중 · N곳을 찾았어요 · GPS 등 로딩 안내)
        // ============================================================
        private static readonly Color Pink2 = new Color(0.941f, 0.478f, 0.627f, 1f);   // #F07AA0
        private static readonly Color PillBg = new Color(0.047f, 0.055f, 0.067f, 0.8f);
        private const float PillH = 128f;

        private static void StatusPills(Transform root, List<string> log)
        {
            // ① 주변 장소 개수 (ObjectCountUI — 프리팹 인스턴스라 자식은 옮기지 않고 형제로 놓는다)
            var oc = FindInScene<ObjectCountUI>(root.gameObject);
            if (oc != null && oc.countText != null)
            {
                var ort = RT(oc.gameObject);
                SetRect(ort, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(1300, PillH));
                Ensure<R0926SafeInset>(oc.gameObject).SetEdge(R0926SafeInset.Edge.Top);
                var back = oc.transform.Find("Back");
                if (back != null)
                {
                    var bImg = Img(back.gameObject, Spr("r0926_pill"), PillBg, Image.Type.Sliced, 63f / (PillH / 2f));
                    bImg.raycastTarget = false;
                    SetRect(RT(back.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, PillH));
                }
                var t = oc.countText;
                t.fontSize = 44; t.fontStyle = FontStyle.Bold; t.color = Ink; t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = true; t.resizeTextForBestFit = false; t.raycastTarget = false;
                if (font != null) t.font = font;
                foreach (var fx in t.GetComponents<Shadow>()) Object.DestroyImmediate(fx);
                EditorUtility.SetDirty(t);

                var ic = FindOrCreate(oc.transform, "Ic0926");
                SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
                var spin = PillPart(ic.transform, "Spin0926", "r0926_arc", Pink2, 84, Image.Type.Simple);
                var glow = PillPart(ic.transform, "OkGlow0926", "r0926_circle", new Color(Pink.r, Pink.g, Pink.b, 0f), 84, Image.Type.Simple);
                var circ = PillPart(ic.transform, "OkCircle0926", "r0926_circle", Pink, 84, Image.Type.Simple);
                var chk = PillPart(ic.transform, "OkCheck0926", "r0926_check", Color.white, 58, Image.Type.Filled);
                chk.fillMethod = Image.FillMethod.Horizontal; chk.fillOrigin = (int)Image.OriginHorizontal.Left; chk.fillAmount = 1f;

                var pill = Ensure<R0926StatusPill>(oc.gameObject);
                var so = new SerializedObject(pill);
                so.FindProperty("source").objectReferenceValue = oc;
                so.FindProperty("text").objectReferenceValue = t;
                so.FindProperty("pill").objectReferenceValue = back != null ? RT(back.gameObject) : null;
                so.FindProperty("icon").objectReferenceValue = RT(ic);
                so.FindProperty("spinner").objectReferenceValue = spin;
                so.FindProperty("okCircle").objectReferenceValue = circ;
                so.FindProperty("okCheck").objectReferenceValue = chk;
                so.FindProperty("okGlow").objectReferenceValue = glow;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else log.Add("상태 알약: ObjectCountUI 없음");

            // ② 로딩 패널 (GPS·세션 준비·복구 안내) — 화면을 덜 가리고 노치 아래 알약으로
            var lp = Find(root, "LoadingPanel");
            if (lp != null)
            {
                var lImg = lp.GetComponent<Image>();
                if (lImg != null) { lImg.color = new Color(0, 0, 0, 0.28f); lImg.sprite = null; EditorUtility.SetDirty(lImg); }
                var text = Find(lp.transform, "LoadingText ")?.GetComponent<Text>();
                var spinT = Find(lp.transform, "LoadingSpinner ");
                if (text != null && spinT != null)
                {
                    var bg = FindOrCreate(lp.transform, "PillBg0926");
                    bg.transform.SetSiblingIndex(0);
                    SetRect(RT(bg), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -14 - PillH / 2f), new Vector2(700, PillH));
                    Img(bg, Spr("r0926_pill"), PillBg, Image.Type.Sliced, 63f / (PillH / 2f)).raycastTarget = false;
                    Ensure<R0926SafeInset>(bg).SetEdge(R0926SafeInset.Edge.Top);

                    SetRect(text.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, 0.5f), new Vector2(0, -14 - PillH / 2f), new Vector2(600, PillH));
                    text.fontSize = 42; text.fontStyle = FontStyle.Bold; text.color = Ink; text.alignment = TextAnchor.MiddleLeft;
                    text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
                    text.resizeTextForBestFit = false; text.raycastTarget = false;
                    if (font != null) text.font = font;
                    foreach (var fx in text.GetComponents<Shadow>()) Object.DestroyImmediate(fx);
                    EditorUtility.SetDirty(text);
                    Ensure<R0926SafeInset>(text.gameObject).SetEdge(R0926SafeInset.Edge.Top);

                    SetRect(RT(spinT), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -14 - PillH / 2f), new Vector2(70, 70));
                    var sImg = spinT.GetComponent<Image>();
                    if (sImg != null) { sImg.sprite = Spr("r0926_arc"); sImg.type = Image.Type.Simple; sImg.color = Pink2; sImg.preserveAspect = true; sImg.raycastTarget = false; EditorUtility.SetDirty(sImg); }
                    Ensure<R0926SafeInset>(spinT).SetEdge(R0926SafeInset.Edge.Top);

                    var pill = Ensure<R0926StatusPill>(lp);
                    var so = new SerializedObject(pill);
                    so.FindProperty("source").objectReferenceValue = null;
                    so.FindProperty("text").objectReferenceValue = text;
                    so.FindProperty("pill").objectReferenceValue = RT(bg);
                    so.FindProperty("icon").objectReferenceValue = RT(spinT);
                    so.FindProperty("spinner").objectReferenceValue = sImg;
                    so.FindProperty("padLeft").floatValue = 30f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else log.Add("로딩 패널: 글자·스피너 없음");
            }
            log.Add("status pills ok");
        }

        private static Image PillPart(Transform parent, string name, string sprite, Color c, float size, Image.Type type)
        {
            var go = FindOrCreate(parent, name);
            SetRect(RT(go), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var img = Img(go, Spr(sprite), c, type);
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        // ============================================================
        // 첫 사용 안내 — 화면을 어둡게, 도크 버튼만 동그랗게 비추고 짧은 화살표
        // ============================================================
        private static void GuideOverlay(Transform root, List<string> log)
        {
            var guide = FindInScene<FirstTimeGuide>(root.gameObject);
            var panel = Find(root, "FirstTimeGuidePanel");
            if (guide == null || panel == null) { log.Add("안내 없음"); return; }

            var gso = new SerializedObject(guide);
            gso.FindProperty("dotSize").floatValue = 24f;
            gso.FindProperty("dotSpacing").floatValue = 24f;
            gso.FindProperty("dotActiveScale").floatValue = 1.5f;
            gso.FindProperty("dotActiveYOffset").floatValue = 0f;
            gso.FindProperty("dotLineBgColor").colorValue = new Color(1, 1, 1, 0);
            gso.FindProperty("dotLineFillColor").colorValue = new Color(1, 1, 1, 0);
            gso.FindProperty("backgroundDimAlpha").floatValue = 0f;
            gso.ApplyModifiedPropertiesWithoutUndo();
            var text = gso.FindProperty("guideText").objectReferenceValue as Text;
            var confirm = gso.FindProperty("confirmButton").objectReferenceValue as Button;
            var bgImage = gso.FindProperty("backgroundImage").objectReferenceValue as Image;
            if (bgImage != null) { bgImage.enabled = false; EditorUtility.SetDirty(bgImage); }

            string[] pageNames = { "01", "02", "03" };
            var pages = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                pages[i] = Find(panel.transform, pageNames[i]);
                var img = Find(panel.transform, "Image_" + pageNames[i]);
                if (img != null) { var im = img.GetComponent<Image>(); im.enabled = false; EditorUtility.SetDirty(im); }
            }
            if (text != null)
            {
                text.fontSize = 58; text.fontStyle = FontStyle.Bold; text.color = Ink; text.alignment = TextAnchor.LowerCenter;
                text.lineSpacing = 1.2f; text.supportRichText = true;
                text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
                if (font != null) text.font = font;
                EditorUtility.SetDirty(text);
            }
            // 예전 확인(체크) 버튼 — 뒤로가기로 닫기 위해 남기되 보이지 않게 ('시작하기'가 대신한다)
            if (confirm != null)
            {
                foreach (var g in confirm.GetComponentsInChildren<Graphic>(true))
                {
                    var c = g.color; c.a = 0f; g.color = c; g.raycastTarget = false;
                    EditorUtility.SetDirty(g);
                }
            }

            var box = FindOrCreate(panel.transform, "Guide0926");
            box.transform.SetSiblingIndex(0);
            Stretch(RT(box));
            var dims = new Image[4];
            string[] dn = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
            for (int i = 0; i < 4; i++)
            {
                var d = FindOrCreate(box.transform, dn[i]);
                dims[i] = Img(d, null, new Color(0.016f, 0.024f, 0.031f, 0.74f), Image.Type.Simple);
                dims[i].raycastTarget = true;   // 안내 중에는 뒤 화면을 누르지 않게 (끌기는 안내 패널이 받는다)
            }
            var hole = FindOrCreate(box.transform, "Hole0926");
            Img(hole, Spr("r0926_hole"), new Color(0.016f, 0.024f, 0.031f, 0.74f), Image.Type.Simple).raycastTarget = true;
            var ring = FindOrCreate(box.transform, "Ring0926");
            var rImg = Img(ring, Spr("r0926_ring_big"), new Color(1, 1, 1, 0.55f), Image.Type.Simple);
            rImg.raycastTarget = false;
            var arrow = FindOrCreate(box.transform, "Arrow0926");
            SetRect(RT(arrow), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 80));
            Img(arrow, Spr("r0926_arrow_short"), Color.white, Image.Type.Simple).raycastTarget = false;

            var next = FindOrCreate(box.transform, "Next0926");
            SetRect(RT(next), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 120));
            Img(next, Spr("r0926_pill"), Color.white, Image.Type.Sliced, 63f / 60f).raycastTarget = true;
            var nl = FindOrCreate(next.transform, "Label0926");
            var nlt = Txt(nl, "다음", 44, Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
            nlt.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(RT(nl));

            var skip = FindOrCreate(box.transform, "Skip0926");
            SetRect(RT(skip), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -30), new Vector2(260, 100));
            Img(skip, Spr("r0926_pill"), new Color(1, 1, 1, 0.1f), Image.Type.Sliced, 63f / 50f).raycastTarget = true;
            Ensure<R0926SafeInset>(skip).SetEdge(R0926SafeInset.Edge.Top);
            var sl = FindOrCreate(skip.transform, "Label0926");
            Txt(sl, "건너뛰기", 40, Soft, TextAnchor.MiddleCenter, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(RT(sl));
            Loc(sl, "건너뛰기", "Skip", "スキップ", "跳过", "Omitir");

            var ov = Ensure<R0926GuideOverlay>(panel);
            var so = new SerializedObject(ov);
            so.FindProperty("guide").objectReferenceValue = guide;
            so.FindProperty("panel").objectReferenceValue = RT(panel);
            so.FindProperty("container").objectReferenceValue = RT(box);
            SetArray(so.FindProperty("dims"), dims[0], dims[1], dims[2], dims[3]);
            so.FindProperty("hole").objectReferenceValue = RT(hole);
            so.FindProperty("ring").objectReferenceValue = rImg;
            so.FindProperty("arrow").objectReferenceValue = RT(arrow);
            so.FindProperty("text").objectReferenceValue = text;
            so.FindProperty("next").objectReferenceValue = RT(next);
            so.FindProperty("nextLabel").objectReferenceValue = nlt;
            so.FindProperty("confirm").objectReferenceValue = confirm;
            SetArray(so.FindProperty("pages"), pages[0], pages[1], pages[2]);
            // 쪽 순서: 추가 · 목록 · 메시지 (R0926GuideText 와 같다)
            var tAdd = Find(root, "PlusButton"); var tList = Find(root, "List_Button"); var tMsg = Find(root, "Message_Button");
            SetArray(so.FindProperty("targets"), tAdd != null ? RT(tAdd) : null, tList != null ? RT(tList) : null, tMsg != null ? RT(tMsg) : null);
            so.ApplyModifiedPropertiesWithoutUndo();

            var nb = Ensure<Button>(next); ResetListeners(nb); UnityEventTools.AddPersistentListener(nb.onClick, ov.Next);
            var sb = Ensure<Button>(skip); ResetListeners(sb); UnityEventTools.AddPersistentListener(sb.onClick, ov.Skip);
            log.Add("guide overlay ok");
        }

        // ============================================================
        // 하늘 칩은 노치 바로 아래 · 인디케이터 위쪽 한계도 노치 바로 아래 (기기마다)
        // ============================================================
        private static void SkyTop(Transform root, List<string> log)
        {
            var chip = Find(root, "SkyChip0926");
            if (chip != null) { RT(chip).anchoredPosition = new Vector2(0, -8); EditorUtility.SetDirty(RT(chip)); }
            var marker = Find(root, "Redesign0926Marker");
            var osi = FindInScene<OffScreenIndicator>(root.gameObject);
            // 누움 판정은 거의 똑바로 위(75° 이상)일 때만 — 서서 하늘을 6초 넘게 보고 있으면 누운 걸로 봐서 날씨를 접었다
            var skyW = marker != null ? marker.GetComponent<R0926SkyWeather>() : null;
            if (skyW != null)
            {
                var wso = new SerializedObject(skyW);
                wso.FindProperty("lyingPitch").floatValue = 75f;
                wso.ApplyModifiedPropertiesWithoutUndo();
            }
            if (marker != null && osi != null)
            {
                var b = Ensure<R0926IndicatorBounds>(marker);
                var so = new SerializedObject(b);
                so.FindProperty("indicator").objectReferenceValue = osi;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("sky top ok");
        }

        // ============================================================
        // 하늘 날씨판 — 그림 + 기온 · 두 줄 · 3시간 후 / 6시간 후 / 내일
        // ============================================================
        private static void WeatherBoard(Transform root, List<string> log)
        {
            GameObject board = null;
            foreach (var r in root.gameObject.scene.GetRootGameObjects()) if (r.name == "SkyWeather0926") board = r;
            var marker = Find(root, "Redesign0926Marker");
            var sky = marker != null ? marker.GetComponent<R0926SkyWeather>() : null;
            if (board == null || sky == null) { log.Add("날씨판 없음"); return; }
            RT(board).sizeDelta = new Vector2(1000, 880);

            var icon = BuildWeatherIcon(board.transform, "Icon0926", 230f);
            SetRect(RT(icon.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-230, -180), new Vector2(230, 230));
            var det = board.transform.Find("Detail0926");
            if (det != null) SetRect(RT(det.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -452), new Vector2(1000, 70));

            var fc = FindOrCreate(board.transform, "Forecast0926");
            SetRect(RT(fc), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -560), new Vector2(780, 280));
            Img(fc, Spr("r0926_pill"), new Color(0.094f, 0.118f, 0.149f, 0.3f), Image.Type.Sliced, 63f / 64f).raycastTarget = false;
            var labels = new Text[3]; var temps = new Text[3]; var icons = new R0926WeatherIcon[3];
            for (int i = 0; i < 3; i++)
            {
                var col = FindOrCreate(fc.transform, "Fc" + i);
                var crt = RT(col);
                crt.anchorMin = new Vector2(i / 3f, 0); crt.anchorMax = new Vector2((i + 1) / 3f, 1); crt.pivot = new Vector2(0.5f, 0.5f);
                crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                if (i > 0)
                {
                    var line = FindOrCreate(col.transform, "Line0926");
                    SetRect(RT(line), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, -44));
                    Img(line, null, new Color(1, 1, 1, 0.18f), Image.Type.Simple).raycastTarget = false;
                }
                var lb = FindOrCreate(col.transform, "Label");
                SetRect(RT(lb), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(0, 54));
                labels[i] = Txt(lb, i == 2 ? "내일" : (i == 0 ? "3시간 후" : "6시간 후"), 38, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
                labels[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                icons[i] = BuildWeatherIcon(col.transform, "Icon", 96f);
                SetRect(RT(icons[i].gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -132), new Vector2(96, 96));
                var tp = FindOrCreate(col.transform, "Temp");
                SetRect(RT(tp), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(0, 64));
                temps[i] = Txt(tp, "19°", 52, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                temps[i].horizontalOverflow = HorizontalWrapMode.Overflow; temps[i].supportRichText = true;
            }
            fc.SetActive(false);   // 예보 값이 오면 켠다

            var so = new SerializedObject(sky);
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("forecast").objectReferenceValue = fc;
            SetArray(so.FindProperty("fcLabels"), labels[0], labels[1], labels[2]);
            SetArray(so.FindProperty("fcIcons"), icons[0], icons[1], icons[2]);
            SetArray(so.FindProperty("fcTemps"), temps[0], temps[1], temps[2]);
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("weather board ok");
        }

        private static R0926WeatherIcon BuildWeatherIcon(Transform parent, string name, float size)
        {
            var go = FindOrCreate(parent, name);
            RT(go).sizeDelta = new Vector2(size, size);
            Image Part(string n, string sprite)
            {
                var p = FindOrCreate(go.transform, n);
                var img = Img(p, Spr(sprite), Color.white, Image.Type.Simple);
                img.raycastTarget = false; img.preserveAspect = true;
                return img;
            }
            var rays = Part("Rays", "r0926_w_rays");
            var sun = Part("Sun", "r0926_circle");
            var moon = Part("Moon", "r0926_w_moon");
            var back = Part("CloudBack", "r0926_w_cloud");
            var cloud = Part("Cloud", "r0926_w_cloud");
            var drops = new[] { Part("Drop0", "r0926_w_drop"), Part("Drop1", "r0926_w_drop"), Part("Drop2", "r0926_w_drop") };
            var flakes = new[] { Part("Flake0", "r0926_w_flake"), Part("Flake1", "r0926_w_flake"), Part("Flake2", "r0926_w_flake") };
            var wi = Ensure<R0926WeatherIcon>(go);
            var so = new SerializedObject(wi);
            so.FindProperty("sun").objectReferenceValue = sun;
            so.FindProperty("rays").objectReferenceValue = rays;
            so.FindProperty("moon").objectReferenceValue = moon;
            so.FindProperty("cloudBack").objectReferenceValue = back;
            so.FindProperty("cloud").objectReferenceValue = cloud;
            SetArray(so.FindProperty("drops"), drops[0], drops[1], drops[2]);
            SetArray(so.FindProperty("flakes"), flakes[0], flakes[1], flakes[2]);
            so.ApplyModifiedPropertiesWithoutUndo();
            return wi;
        }

        // ============================================================
        // 댓글 입력줄 — 둥근 유리 입력칸 · 내 사진 · 분홍 보내기 · 빠른 반응
        // ============================================================
        private static void CommentBar(Transform root, List<string> log)
        {
            var cm = FindInScene<CommentManager>(root.gameObject);
            var panel = cm != null ? cm.commentPanel : null;
            var area = panel != null ? Find(panel.transform, "InputArea") : null;
            if (area == null) { log.Add("댓글 입력줄 없음"); return; }
            const float Edge = 50f + 40f;   // 시트가 화면보다 좌우 50씩 넓다
            const float QuickH = 124f;
            const float BarPad = 20f, FieldH = 110f;

            // 입력줄 바탕 — 반투명 시트 뒤로 도크가 비치지 않게 (빠른 반응 줄까지 덮는다)
            var bgBar = FindOrCreate(area.transform, "Bg0926");
            bgBar.transform.SetAsFirstSibling();
            var bbr = RT(bgBar);
            bbr.anchorMin = Vector2.zero; bbr.anchorMax = Vector2.one; bbr.pivot = new Vector2(0.5f, 0.5f);
            bbr.offsetMin = new Vector2(0, -240); bbr.offsetMax = new Vector2(0, QuickH);
            Img(bgBar, null, new Color(0.082f, 0.094f, 0.106f, 1f), Image.Type.Simple).raycastTarget = true;

            var scroll = Find(panel.transform, "Scroll View");
            if (scroll != null) { var srt = RT(scroll); srt.offsetMin = new Vector2(srt.offsetMin.x, 150 + QuickH); EditorUtility.SetDirty(srt); }

            var container = area.transform.Find("InputContainer");
            if (container != null)
            {
                // 아래 고정 + 높이는 sizeDelta.y — 댓글매니저가 붙이는 AutoExpandInputField 가 그렇게 가정한다 (여러 줄이면 위로 늘린다)
                var crt = (RectTransform)container;
                float l = Edge + 116 + 24, r = Edge + 128 + 24;
                crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(1, 0); crt.pivot = new Vector2(0.5f, 0);
                crt.anchoredPosition = new Vector2((l - r) / 2f, BarPad); crt.sizeDelta = new Vector2(-(l + r), FieldH);
                EditorUtility.SetDirty(crt);
                Img(container.gameObject, Spr("r0926_pill"), new Color(1, 1, 1, 0.07f), Image.Type.Sliced, 63f / 55f).raycastTarget = true;
                foreach (var n in new[] { "Text", "Placeholder" })
                {
                    var tt = container.Find(n)?.GetComponent<Text>();
                    if (tt == null) continue;
                    var trt = tt.rectTransform;
                    trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
                    trt.offsetMin = new Vector2(40, 0); trt.offsetMax = new Vector2(-40, 0);
                    tt.fontSize = 46; tt.alignment = TextAnchor.MiddleLeft;
                    tt.color = n == "Text" ? Ink : new Color(0.49f, 0.525f, 0.557f, 1f);
                    if (font != null) tt.font = font;
                    EditorUtility.SetDirty(tt);
                }
            }

            var btn = area.transform.Find("Button");
            Image sendBg = null;
            if (btn != null)
            {
                SetRect((RectTransform)btn, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0.5f), new Vector2(-Edge, BarPad + FieldH / 2f), new Vector2(128, 128));
                sendBg = Img(btn.gameObject, Spr("r0926_circle"), new Color(0.227f, 0.251f, 0.275f, 1f), Image.Type.Simple);
                sendBg.raycastTarget = true;
                var b = btn.GetComponent<Button>();
                if (b != null) { b.transition = Selectable.Transition.None; EditorUtility.SetDirty(b); }
                var send = btn.Find("Send");
                if (send != null)
                {
                    SetRect((RectTransform)send, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 66));
                    send.localEulerAngles = new Vector3(0, 0, 180);   // 아래 화살표를 뒤집어 보내기(위)
                    var si = send.GetComponent<Image>();
                    if (si != null) { si.sprite = Spr("r0926_arrow_short"); si.color = Color.white; si.preserveAspect = true; si.raycastTarget = false; EditorUtility.SetDirty(si); }
                }
                var spin = btn.Find("Spinner");
                if (spin != null) SetRect((RectTransform)spin, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100));
            }

            // 내 사진 (도크 프로필 사진을 그대로 쓴다)
            var av = FindOrCreate(area.transform, "Avatar0926");
            SetRect(RT(av), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(Edge + 58, BarPad + FieldH / 2f), new Vector2(112, 112));
            Img(av, Spr("r0926_circle"), Color.white, Image.Type.Simple).raycastTarget = false;
            Ensure<Mask>(av).showMaskGraphic = false;
            var photo = FindOrCreate(av.transform, "Photo");
            Stretch(RT(photo));
            var pImg = Img(photo, null, Color.white, Image.Type.Simple);
            pImg.raycastTarget = false; pImg.preserveAspect = false;
            var avRing = FindOrCreate(area.transform, "AvatarRing0926");
            avRing.transform.SetSiblingIndex(av.transform.GetSiblingIndex() + 1);
            SetRect(RT(avRing), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(Edge + 58, BarPad + FieldH / 2f), new Vector2(128, 128));
            Img(avRing, Spr("r0926_ring_big"), Pink, Image.Type.Simple).raycastTarget = false;
            var mini = Find(root, "MiniProfile");
            var src = mini != null ? Find(mini.transform, "Avatar") : null;

            // 빠른 반응 — 입력칸 바로 위 한 줄
            var bar = Ensure<R0926CommentBar>(area);
            var quick = FindOrCreate(area.transform, "Quick0926");
            SetRect(RT(quick), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, QuickH));
            var qline = FindOrCreate(quick.transform, "Line0926");
            SetRect(RT(qline), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));
            Img(qline, null, Line, Image.Type.Simple).raycastTarget = false;
            var hl = Ensure<HorizontalLayoutGroup>(quick);
            hl.padding = new RectOffset((int)Edge, 40, 10, 6); hl.spacing = 20; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var reacts = new (string sprite, string emoji)[]
            {
                ("r0926_re_heart", "❤️"), ("r0926_re_like", "👍"), ("r0926_re_laugh", "😂"),
                ("r0926_re_wow", "😮"), ("r0926_re_spark", "✨"), ("r0926_re_fire", "🔥"),
            };
            for (int i = 0; i < reacts.Length; i++)
            {
                var r = FindOrCreate(quick.transform, "Re" + i);
                RT(r).sizeDelta = new Vector2(140, 100);
                Img(r, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 63f / 54f).raycastTarget = true;
                var ic = FindOrCreate(r.transform, "Icon");
                SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
                var ii = Img(ic, Spr(reacts[i].sprite), Color.white, Image.Type.Simple);
                ii.raycastTarget = false; ii.preserveAspect = true;
                var rb = Ensure<Button>(r);
                rb.targetGraphic = r.GetComponent<Image>();
                ResetListeners(rb);
                UnityEventTools.AddStringPersistentListener(rb.onClick, bar.React, reacts[i].emoji);
            }

            var so = new SerializedObject(bar);
            so.FindProperty("input").objectReferenceValue = cm.commentInputField;
            so.FindProperty("sendBg").objectReferenceValue = sendBg;
            so.FindProperty("avatar").objectReferenceValue = pImg;
            so.FindProperty("avatarSource").objectReferenceValue = src != null ? src.GetComponent<Image>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("comment bar ok");
        }
    }
}
