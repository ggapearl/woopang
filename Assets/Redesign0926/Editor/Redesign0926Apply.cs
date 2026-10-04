using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 0926 시안 적용. 기존 오브젝트는 지우지 않고 옮기고·다시 칠한다(참조가 끊기지 않게).
    /// 여러 번 돌려도 같은 결과가 나오도록 "찾고 없으면 만든다 → 값을 다시 넣는다" 로만 쓴다.
    /// 캔버스 단위: 기준 1440x3200, iPhone 15 에서 1pt ≈ 3.7 단위.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        public const int Version = 18;  // v18: 첫 안내 목록→추가→메세지 순서·다음/시작하기 터치음 · 도크 '메세지' · 숨김 되돌리기 알림·설정 '모두 다시 보이기' · 날씨판 위 지역 이름 · 업데이트 카드 '반영 중' 상태 제거 · v17: 업데이트 안내를 프로필 카드 모양으로 · v16: 키보드 위 입력줄 연결 바로잡음(장소 칸 글 덮어쓰던 것·장소 수정 쪽 빈 연결)·폭 · 장소 추가 칩·스위치 키움 · v15: 10-04 아이폰 확인 — 채팅방이 안 열리던 것(키보드 처리기) · 대화 목록 두 벌 · 도크 메시지 칸 · 손잡이 · 프로필 카드 · 입력줄 · 장소 추가 크게·좌표 · 날씨 크기 · v14: 0930 시안 — 도크 여백·테두리 추가·한 줄 위치 · 창이 도크 윗선에서 오르내림 · 프로필 페이드 · 칩·목록 촘촘히 · 탭 옆으로 밀기 · 상태 알약 · 안내 스포트라이트 · 날씨 예보 · 댓글 입력 · X 두 번 눌러 삭제 · v13: 시작화면 영어(우주) 하나로 · 도크 버튼 키움 · 닫을 때도 아래로 미끄러짐 · v12: 시작화면 색동·금테 제거 · 렌즈 밖 옅은 문양 · 흰 렌즈 · 워드마크 간격 · 끌어 닫기 · 뒤로가기 정리 · v11: 시작화면을 맨 위 하나로 (예전 시작 이미지 제거 · Panel_Top 정리) · v10: 시작화면 SEE THE UNSEEN (v9: 친구 지도 · v8: 인디케이터 X)   // v6: 거리별 흐림 · 가로 화면 (v5: 장소 추가 개편 · 상태 화면 · 안내 · 끊김 배너 · 로마자)

        private const string SpriteDir = "Assets/Redesign0926/Sprites/";

        // ── 색 ─────────────────────────────────────────────
        private static readonly Color Glass = new Color(0.055f, 0.067f, 0.078f, 0.58f);
        private static readonly Color Sheet = new Color(0.078f, 0.090f, 0.102f, 0.97f);
        private static readonly Color Ink = new Color(0.953f, 0.961f, 0.965f, 1f);
        private static readonly Color Soft = new Color(0.867f, 0.886f, 0.902f, 1f);
        private static readonly Color Muted = new Color(0.549f, 0.584f, 0.616f, 1f);
        private static readonly Color Line = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color Pink = new Color(0.914f, 0.325f, 0.514f, 1f);
        private static readonly Color Dark = new Color(0.082f, 0.094f, 0.106f, 1f);

        // ── 크기 (캔버스 단위) ─────────────────────────────
        private const float Side = 60f;          // 좌우 여백 16pt
        private const float TopGap = 36f;        // 상태바 아래 여백
        private const float TopButton = 148f;    // 로고·프로필 원 40pt
        private const float DockHeight = 224f;   // 60pt — 2026-09-30 위아래 여백을 줄여 달라 하셔서 (아이콘 크기는 그대로)
        private const float DockBottom = 30f;    // 홈 인디케이터 위 8pt
        private const float PlusSize = 196f;     // 52pt
        private const float DockIcon = 106f;     // 29pt (예전 25pt)
        private const float DockLabel = 44f;     // 12pt (예전 10pt)
        private const float DockIconY = 26f, DockLabelY = -58f, DockLabelBox = 60f, AddGlyph = 76f;

        private static Font font;

        public static bool ApplyIfOutdated(Scene scene, List<string> log)
        {
            var canvas = Redesign0926Capture.FindMainCanvas(scene);
            if (canvas == null) return false;
            var marker = canvas.GetComponentInChildren<R0926Marker>(true);
            if (marker != null && marker.appliedVersion >= Version) return false;
            Apply(scene, log);
            return true;
        }

        public static void Apply(Scene scene, List<string> log)
        {
            EnsureSpriteImports(log);
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/AppleSDGothicNeoM.ttf");

            var canvas = Redesign0926Capture.FindMainCanvas(scene);
            if (canvas == null) { log.Add("Canvas 없음"); return; }
            Transform root = canvas.transform;
            Unwrap0930(root);                   // 지난번 틀(Clip0926·ListPage0926)을 벗기고 — 아래 단계들은 패널 기준으로 다시 잡는다

            ApplyTop(root, log);
            ApplyDock(root, log);
            ApplyLocationChip(root, log);
            ApplyListSheet(root, log);
            ApplyDialogs(root, log);
            ApplyPlaceMenu(root, log);
            ApplyMessages(root, log);
            ApplyProfile(root, log);
            ApplyUpload(root, log);
            ApplyUploadFull(root, log);
            ApplyStatusScreens(root, log);
            ApplyGuideNetRoman(root, log);
            ApplySkyAndAI(root, log);
            ApplyFriendsMap(root, log);
            ApplySheetClosing(root, log);       // 끌어 닫기 · 뒤로가기 — 시트·버튼이 다 자리잡은 뒤
            ApplySplash(root, log);             // 시작화면은 맨 위 — 마지막에
            Apply0930(root, log);               // 0930 시안 — 틀을 다시 씌우고 새 모양 (시작화면 별 포함)
            ApplyFadeAndLandscape(root, log);   // 가로 배치는 세로 값이 다 정해진 뒤에 (0930 틀 포함 — 맨 마지막)

            var marker = root.GetComponentInChildren<R0926Marker>(true);
            if (marker == null)
            {
                var mgo = new GameObject("Redesign0926Marker", typeof(RectTransform), typeof(R0926Marker));
                mgo.transform.SetParent(root, false);
                marker = mgo.GetComponent<R0926Marker>();
            }
            marker.appliedVersion = Version;
            EditorUtility.SetDirty(marker);
            log.Add("apply v" + Version + " 완료");
        }

        // ============================================================
        // 상단 — 검은 띠 대신 옅은 그늘 + 로고 엠블럼(누르면 색 테마) + 프로필(오른쪽)
        // ============================================================
        private static void ApplyTop(Transform root, List<string> log)
        {
            var top = Find(root, "Panel_Top");
            if (top == null) { log.Add("Panel_Top 없음"); return; }

            // 위쪽은 비운다 — 상태바 글자가 보이게 옅은 그늘만. 로고는 시작 화면에서만 보여 준다
            SetRect(RT(top), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 300));
            var bg = top.GetComponent<Image>();
            bg.sprite = Spr("r0926_scrim");
            bg.type = Image.Type.Simple;
            bg.color = new Color(0, 0, 0, 0.45f);
            bg.raycastTarget = false;

            // 로고가 없으니 로고·로고 받침은 지운다. 색 테마도 상단 띠만 칠하던 것이라 끈다 —
            // 남기면 저장된 테마색(분홍 등)이 상단을 통째로 덮는다. Panel_Top 은 상태바 글자용 그늘로만 남긴다
            foreach (var n in new[] { "TITLE_Woopang", "EmblemGlass0926" })
            {
                var old = Find(top.transform, n);
                if (old != null) { Object.DestroyImmediate(old); log.Add("top: " + n + " 삭제"); }
            }
            var changer = top.GetComponent<TopPanelColorChanger>();
            if (changer != null)
            {
                var cso = new SerializedObject(changer);
                cso.FindProperty("sceneColorOnly").boolValue = true;
                cso.FindProperty("logoImage").objectReferenceValue = null;
                cso.FindProperty("currentColorIndex").intValue = 0;
                cso.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("top ok");
        }

        // ============================================================
        // 하단 도크 — 목록 · 올리기 · 메시지를 한 줄로 (버튼 오브젝트는 그대로 옮긴다)
        // ============================================================
        // 도크 칸 위치 (도크 너비 기준 0~1)
        private const float SlotList = 0.125f, SlotAdd = 0.375f, SlotMsg = 0.625f, SlotProfile = 0.875f;
        private const float AddTile = 106f;
        private const float MiniAvatar = 92f;   // 도크 프로필 사진 — 테두리까지 다른 아이콘과 비슷한 크기

        private static void ApplyDock(Transform root, List<string> log)
        {
            var listBtn = Find(root, "List_Button");
            var plusBtn = Find(root, "PlusButton");
            var msgBtn = Find(root, "Message_Button");
            var mini = Find(root, "MiniProfile");
            if (listBtn == null || plusBtn == null || msgBtn == null) { log.Add("도크 버튼 없음"); return; }

            var dock = Find(root, "Dock0926");
            if (dock == null)
            {
                dock = NewUI("Dock0926", root);
                dock.transform.SetSiblingIndex(listBtn.transform.GetSiblingIndex());
            }
            Img(dock, Spr("r0926_pill"), Glass, Image.Type.Sliced, 64f / (DockHeight / 2)).raycastTarget = true;
            SetRect(RT(dock), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(0, DockBottom), new Vector2(-Side * 2, DockHeight));
            Ensure<R0926SafeInset>(dock).SetEdge(R0926SafeInset.Edge.Bottom);

            DockButton(listBtn, dock.transform, SlotList, "r0926_i_list", "목록", "List", "リスト", "列表", "Lista");
            DockButton(msgBtn, dock.transform, SlotMsg, "r0926_i_mail", "메세지", "Messages", "メッセージ", "消息", "Mensajes");   // 한국어는 '메세지' (2026-10 요청)

            // 안 읽음 표시 — 이름·부모 그대로 (MessagePanelManager 가 이름으로 찾는다)
            var unread = Find(msgBtn.transform, "UnreadMessageButtonImage");
            if (unread != null)
            {
                var u = unread.GetComponent<Image>();
                u.sprite = Spr("r0926_pill");   // 숫자가 두 자리가 되면 옆으로 늘어나는 알약 모양
                u.type = Image.Type.Sliced;
                u.pixelsPerUnitMultiplier = 64f / 29f;
                u.color = Pink;
                u.raycastTarget = false;
                SetRect(RT(unread), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(58, DockIconY + 42), new Vector2(60, 60));   // 아이콘 오른쪽 위 모서리
                // 검은 테두리 없이 (예전 버튼에서 따라온 Outline·Shadow)
                foreach (var fx in unread.GetComponentsInChildren<Shadow>(true)) Object.DestroyImmediate(fx);
                unread.transform.SetAsLastSibling();
                var cnt = FindOrCreate(unread.transform, "Count");   // MessagePanelManager 가 개수를 적는다
                var crt2 = RT(cnt); crt2.anchorMin = Vector2.zero; crt2.anchorMax = Vector2.one; crt2.offsetMin = Vector2.zero; crt2.offsetMax = Vector2.zero;
                var ctx = Txt(cnt, "3", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                ctx.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            // 추가 — 다른 칸과 같은 크기, 아이콘만 흰 사각 타일로 강조 + 아래 '추가'
            AddButtonStyle(plusBtn, dock.transform, SlotAdd, "r0926_i_plus", new[] { "추가", "Add", "追加", "添加", "Añadir" });

            // 프로필 — 오른쪽 끝 칸. 로그인 전엔 '로그인'
            if (mini != null)
            {
                mini.transform.SetParent(dock.transform, false);
                NoInset(mini);
                var mrt = RT(mini);
                mrt.localScale = Vector3.one;
                SetRect(mrt, new Vector2(SlotProfile, 0.5f), new Vector2(SlotProfile, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, DockHeight));
                var mImg = mini.GetComponent<Image>();
                if (mImg != null) { mImg.color = new Color(0, 0, 0, 0); mImg.raycastTarget = true; }
                var mask = Find(mini.transform, "AvatarMask");
                if (mask != null) SetRect(RT(mask), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockIconY), new Vector2(MiniAvatar, MiniAvatar));
                // 테두리는 사진 위에 — 안쪽 가장자리가 사진 가장자리를 살짝 덮어 계단을 가린다 (r0926_ring_mini: 안 0.68 · 진함 ~0.86 · 바깥으로 옅게)
                var ring = Find(mini.transform, "AvatarOutline");
                if (ring != null)
                {
                    float rs = (MiniAvatar / 2f - 3f) / 0.68f * 2f;
                    SetRect(RT(ring), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockIconY), new Vector2(rs, rs));
                    var ri = ring.GetComponent<Image>();
                    ri.sprite = Spr("r0926_ring_mini"); ri.type = Image.Type.Simple; ri.raycastTarget = false; ri.preserveAspect = true;
                    EditorUtility.SetDirty(ri);
                    if (mask != null) ring.transform.SetSiblingIndex(mask.transform.GetSiblingIndex() + 1);
                    // 전체공개면 은은하게 숨쉬고 4초마다 물결 한 번
                    var rip = FindOrCreate(mini.transform, "Ripple0926");
                    rip.transform.SetSiblingIndex(ring.transform.GetSiblingIndex());
                    SetRect(RT(rip), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockIconY), new Vector2(rs * 0.86f, rs * 0.86f));
                    var ripImg = Img(rip, Spr("r0926_ring_big"), new Color(Pink.r, Pink.g, Pink.b, 0f), Image.Type.Simple);
                    ripImg.raycastTarget = false;
                    ripImg.enabled = false;
                    var pulse = Ensure<R0926AvatarPulse>(mini);
                    var pso = new SerializedObject(pulse);
                    pso.FindProperty("outline").objectReferenceValue = ri;
                    pso.FindProperty("ripple").objectReferenceValue = ripImg;
                    pso.FindProperty("breathScale").floatValue = 0.035f;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                }
                var uname = Find(mini.transform, "Username");
                if (uname != null) uname.GetComponent<Text>().enabled = false;   // ProfileManager 는 계속 쓴다 — 보이지만 않게
                var lb = FindOrCreate(mini.transform, "Label0926");
                Txt(lb, "프로필", DockLabel, Soft, TextAnchor.MiddleCenter, FontStyle.Bold);
                SetRect(RT(lb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockLabelY), new Vector2(280, DockLabelBox));
                var old = lb.GetComponent<R0926LocalizedText>();
                if (old != null) Object.DestroyImmediate(old);
                Ensure<R0926ProfileLabel>(lb);
            }
            log.Add("dock ok");
        }

        private static void AddButtonStyle(GameObject btn, Transform parent, float x, string glyph, string[] words)
        {
            btn.transform.SetParent(parent, false);
            var rt = RT(btn);
            rt.localScale = Vector3.one;
            SetRect(rt, new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, DockHeight));
            var img = btn.GetComponent<Image>();
            img.sprite = null; img.color = new Color(1, 1, 1, 0); img.raycastTarget = true; img.preserveAspect = false;
            foreach (var t in btn.GetComponentsInChildren<Text>(true))
                if (t.name != "Label0926") t.enabled = false;

            // 0930: 채운 흰 타일 대신 흰 테두리 네모 + 흰 + (닫기는 다른 칸처럼 흰 X) — 아이콘 크기는 다른 칸과 같게
            var tile = FindOrCreate(btn.transform, "Icon0926");
            tile.transform.SetSiblingIndex(0);
            var tImg = Img(tile, Spr(glyph == "r0926_i_plus" ? "r0926_i_addbox" : glyph), Ink, Image.Type.Simple);
            tImg.raycastTarget = false;
            SetRect(RT(tile), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockIconY), new Vector2(DockIcon, DockIcon));
            var g = FindOrCreate(tile.transform, "Glyph0926");   // 예전 타일 속 글리프 — 남겨 두되 보이지 않게
            Img(g, Spr(glyph), Dark, Image.Type.Simple).raycastTarget = false;
            g.GetComponent<Image>().enabled = false;

            var lb = FindOrCreate(btn.transform, "Label0926");
            Txt(lb, words[0], DockLabel, Soft, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(lb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, DockLabelY), new Vector2(280, DockLabelBox));
            Loc(lb, words[0], words[1], words[2], words[3], words[4]);

            var b = btn.GetComponent<Button>();
            if (b != null) { b.targetGraphic = tImg; EditorUtility.SetDirty(b); }
        }

        private static void DockButton(GameObject btn, Transform dock, float x, string icon,
            string ko, string en, string ja, string zh, string es)
        {
            btn.transform.SetParent(dock, false);
            var rt = RT(btn);
            rt.localScale = Vector3.one;
            SetRect(rt, new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, DockHeight));

            // 버튼 자체는 투명한 누름 영역 — 모양은 아이콘·글자가 맡는다
            var img = btn.GetComponent<Image>();
            img.sprite = null;
            img.color = new Color(1, 1, 1, 0);
            img.raycastTarget = true;

            var ic = FindOrCreate(btn.transform, "Icon0926");
            ic.transform.SetSiblingIndex(0);
            var icImg = Img(ic, Spr(icon), Ink, Image.Type.Simple);
            icImg.raycastTarget = false;
            SetRect(RT(ic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, DockIconY), new Vector2(DockIcon, DockIcon));

            var lb = FindOrCreate(btn.transform, "Label0926");
            lb.transform.SetSiblingIndex(1);
            Txt(lb, ko, DockLabel, Soft, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(lb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, DockLabelY), new Vector2(280, DockLabelBox));
            Loc(lb, ko, en, ja, zh, es);

            var b = btn.GetComponent<Button>();
            if (b != null) { b.targetGraphic = icImg; EditorUtility.SetDirty(b); }
        }

        // ============================================================
        // 위치 상태 — 도크 위 작은 유리 칩
        // ============================================================
        private static void ApplyLocationChip(Transform root, List<string> log)
        {
            var panel = Find(root, "LocationManagerPanel");
            if (panel == null) { log.Add("LocationManagerPanel 없음"); return; }

            Img(panel, Spr("r0926_pill"), Glass, Image.Type.Sliced, 64f / 50f).raycastTarget = false;
            SetRect(RT(panel), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, DockBottom + DockHeight + 24), new Vector2(600, 84));
            Ensure<R0926SafeInset>(panel).SetEdge(R0926SafeInset.Edge.Bottom);

            var hlg = Ensure<HorizontalLayoutGroup>(panel);
            hlg.padding = new RectOffset(28, 38, 14, 14);
            hlg.spacing = 14;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            var csf = Ensure<ContentSizeFitter>(panel);
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var status = Find(panel.transform, "StatusImage");
            if (status != null)
            {
                var le = Ensure<LayoutElement>(status);
                le.preferredWidth = 38; le.preferredHeight = 38;
                status.GetComponent<Image>().preserveAspect = true;
            }
            var text = Find(panel.transform, "LocationText");
            if (text != null)
            {
                var t = text.GetComponent<Text>();
                t.fontSize = 36;
                t.lineSpacing = 1.05f;
                t.color = Soft;
                t.supportRichText = true;   // 좌표는 옅게
                t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                if (font != null) t.font = font;
                EditorUtility.SetDirty(t);
            }
            // 상세·목록·메시지 등이 열려 있으면 숨긴다 (특히 상세 화면은 칩보다 아래에 그려져 겹친다)
            Ensure<CanvasGroup>(panel);
            // 주소·좌표 한 줄 (LocationManager)
            var lm = FindInScene<LocationManager>(panel);
            if (lm != null)
            {
                var lso = new SerializedObject(lm);
                var sl = lso.FindProperty("singleLine");
                if (sl != null) { sl.boolValue = true; lso.ApplyModifiedPropertiesWithoutUndo(); }
            }
            var hide = Ensure<R0926HideWhile>(panel);
            var hso = new SerializedObject(hide);
            var arr = hso.FindProperty("panels");
            string[] watch = { "FullScreenPanel", "ListPanel", "MessagePanel", "ChatRoomPanel", "FullProfilePanel", "UploadPage", "Fixpage", "FirstTimeGuidePanel" };
            arr.arraySize = watch.Length;
            for (int i = 0; i < watch.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = Find(root, watch[i]);
            hso.ApplyModifiedPropertiesWithoutUndo();
            log.Add("location chip ok");
        }

        // ============================================================
        // 목록·필터 — 아래에서 올라오는 시트, 위아래 여백 최소
        // ============================================================
        private const float SheetPad = 48f;
        private const float HeaderY = -40f;
        private const float SliderBlock = 140f;   // 제목줄(72) + 슬라이더(56) + 틈
        private const float ChipsY = HeaderY - SliderBlock - 20f;   // -200
        private const float ChipH = 112f;   // 0930: 글자를 키운 칩 (예전 76)
        private const float ChipGap = 18f;
        private const float SummaryY = ChipsY - (ChipH * 2 + ChipGap) - 22f;   // -392
        private const float ListTop = SummaryY - 50f;                          // -442
        private const float FooterH = 64f;
        private const float RowH = 66f;    // 0930: 촘촘하게 (예전 84)
        private const float CloseSize = 72f;

        private static void ApplyListSheet(Transform root, List<string> log)
        {
            var panel = Find(root, "ListPanel");
            if (panel == null) { log.Add("ListPanel 없음"); return; }
            var pImg = panel.GetComponent<Image>();
            if (pImg != null) { pImg.color = new Color(0, 0, 0, 0.35f); }

            var sheet = Find(panel.transform, "Sheet0926");
            if (sheet == null) { sheet = NewUI("Sheet0926", panel.transform); }
            sheet.transform.SetSiblingIndex(0);
            CardAboveDock(sheet, 0.92f);   // 0930: 장소 추가 창과 같은 높이
            Slide(sheet, 240f);

            var grab = FindOrCreate(sheet.transform, "Grab0926");
            Img(grab, Spr("r0926_pill"), new Color(1, 1, 1, 0.22f), Image.Type.Sliced, 64f / 6f).raycastTarget = false;
            SetRect(RT(grab), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(110, 12));

            // 제목줄: "근처 장소 24" (기존 "ACTIVE LIST" 텍스트를 옮겨 쓴다)
            var header = FindOrCreate(sheet.transform, "Header0926");
            SetRect(RT(header), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(SheetPad, HeaderY), new Vector2(600, 72));
            var hl = Ensure<HorizontalLayoutGroup>(header);
            hl.spacing = 14; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            var back = Find(panel.transform, "Scroll View Back");
            var title = Find(panel.transform, "Title");
            if (title != null && title.transform.parent != header.transform && (back == null || title.transform.IsChildOf(back.transform)))
                title.transform.SetParent(header.transform, false);
            if (title != null)
            {
                title.transform.SetSiblingIndex(0);
                var t = Txt(title, "근처 장소", 52, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                Loc(title, "근처 장소", "Nearby", "近くの場所", "附近地点", "Cerca");
            }
            var count = FindOrCreate(header.transform, "Count0926");
            count.transform.SetSiblingIndex(1);
            var ct = Txt(count, "", 52, Pink, TextAnchor.MiddleLeft, FontStyle.Bold);
            ct.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 거리 슬라이더
            var sliderUI = Find(panel.transform, "DistanceSliderUI");
            if (sliderUI != null)
            {
                sliderUI.transform.SetParent(sheet.transform, false);
                var sImg = sliderUI.GetComponent<Image>();
                if (sImg != null) sImg.color = new Color(0, 0, 0, 0);
                SetRect(RT(sliderUI), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, HeaderY), new Vector2(0, SliderBlock));
                RT(sliderUI).offsetMin = new Vector2(SheetPad, RT(sliderUI).offsetMin.y);
                RT(sliderUI).offsetMax = new Vector2(-SheetPad, RT(sliderUI).offsetMax.y);

                var slider = Find(sliderUI.transform, "DistanceSlider");
                if (slider != null)
                {
                    SetRect(RT(slider), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(0, 56));
                    var sbg = Find(slider.transform, "Background");
                    if (sbg != null)
                    {
                        Img(sbg, Spr("r0926_pill"), new Color(1, 1, 1, 0.14f), Image.Type.Sliced, 64f / 5f);
                        SetRect(RT(sbg), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 10));
                    }
                    var fillArea = Find(slider.transform, "Fill Area");
                    if (fillArea != null)
                    {
                        SetRect(RT(fillArea), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 10));
                        var fill = Find(fillArea.transform, "Fill");
                        if (fill != null) Img(fill, Spr("r0926_pill"), Ink, Image.Type.Sliced, 64f / 5f);
                    }
                    var handle = Find(slider.transform, "Handle");
                    if (handle != null)
                    {
                        Img(handle, Spr("r0926_circle"), Color.white, Image.Type.Simple);
                        RT(handle).sizeDelta = new Vector2(56, 0);
                    }
                    var handleArea = Find(slider.transform, "Handle Slide Area");
                    if (handleArea != null) SetRect(RT(handleArea), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-56, 0));
                }
                var val = Find(sliderUI.transform, "DistanceValueText");
                if (val != null)
                {
                    var vt = Txt(val, null, 36, Soft, TextAnchor.MiddleRight, FontStyle.Normal);
                    SetRect(RT(val), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(300, 72));
                }
            }

            // 필터 칩 4x2
            var filters = Find(panel.transform, "FilterButtonPanel");
            if (filters != null)
            {
                filters.transform.SetParent(sheet.transform, false);
                filters.transform.localScale = Vector3.one;   // 원래 2배 확대가 걸려 있었다
                var fImg = filters.GetComponent<Image>();
                if (fImg != null) fImg.color = new Color(0, 0, 0, 0);
                var vlg = filters.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) Object.DestroyImmediate(vlg);
                var csf = filters.GetComponent<ContentSizeFitter>();
                if (csf != null) Object.DestroyImmediate(csf);
                var grid = Ensure<GridLayoutGroup>(filters);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                grid.spacing = new Vector2(ChipGap, ChipGap);
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.cellSize = new Vector2(318, ChipH);
                Ensure<R0926GridFit>(filters);   // 칸 너비는 실제 폭에 맞춘다
                SetRect(RT(filters), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, ChipsY), new Vector2(0, ChipH * 2 + ChipGap));
                RT(filters).offsetMin = new Vector2(SheetPad, RT(filters).offsetMin.y);
                RT(filters).offsetMax = new Vector2(-SheetPad, RT(filters).offsetMax.y);

                foreach (Transform chip in filters.transform) StyleChip(chip.gameObject);
            }

            // 집계 한 줄
            var summary = FindOrCreate(sheet.transform, "Summary0926");
            var sumT = Txt(summary, "", 30, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);
            sumT.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(RT(summary), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, SummaryY), new Vector2(-SheetPad * 2, 44));

            // 목록
            var scroll = Find(panel.transform, "Scroll View");
            if (scroll != null)
            {
                scroll.transform.SetParent(sheet.transform, false);
                var srt = RT(scroll);
                srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0.5f, 0.5f);
                srt.offsetMin = new Vector2(SheetPad - 8, FooterH + 18);
                srt.offsetMax = new Vector2(-(SheetPad - 8), ListTop);
                NoInset(scroll);   // 카드 자체가 안전영역만큼 올라간다

                var vp = Find(scroll.transform, "Viewport");
                if (vp != null)
                {
                    var vImg = vp.GetComponent<Image>();
                    if (vImg != null) vImg.color = new Color(0, 0, 0, 1);   // Mask 용 — showMaskGraphic false 로 안 보임
                    var mask = vp.GetComponent<Mask>();
                    if (mask != null) mask.showMaskGraphic = false;
                    var vrt = RT(vp); vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
                    vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
                }
                var sbar = Find(scroll.transform, "Scrollbar Vertical");
                if (sbar != null)
                {
                    var sr = scroll.GetComponent<ScrollRect>();
                    if (sr != null) { sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; EditorUtility.SetDirty(sr); }
                    var sbImg = sbar.GetComponent<Image>();
                    if (sbImg != null) sbImg.color = new Color(1, 1, 1, 0);
                    var handle = Find(sbar.transform, "Handle");
                    if (handle != null) Img(handle, Spr("r0926_pill"), new Color(1, 1, 1, 0.18f), Image.Type.Sliced, 64f / 4f);
                    SetRect(RT(sbar), new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(8, 0));
                }
                var content = Find(scroll.transform, "Content");
                var listText = content != null ? Find(content.transform, "ListText") : null;
                if (content != null)
                {
                    var crt = RT(content);
                    crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
                    crt.anchoredPosition = Vector2.zero;
                    crt.sizeDelta = new Vector2(0, crt.sizeDelta.y);
                    var vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.AddComponent<VerticalLayoutGroup>();
                    vlg.padding = new RectOffset(8, 8, 0, 0);
                    vlg.spacing = 0;
                    vlg.childControlWidth = true; vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

                    var tpl = BuildRowTemplate(content.transform);
                    var rows = Ensure<R0926PlaceRows>(content);
                    var so = new SerializedObject(rows);
                    so.FindProperty("source").objectReferenceValue = listText != null ? listText.GetComponent<Text>() : null;
                    so.FindProperty("rowContainer").objectReferenceValue = crt;
                    so.FindProperty("rowTemplate").objectReferenceValue = tpl;
                    so.FindProperty("countText").objectReferenceValue = count.GetComponent<Text>();
                    so.FindProperty("summaryText").objectReferenceValue = sumT;
                    so.FindProperty("barSprite").objectReferenceValue = Spr("r0926_pill");
                    so.FindProperty("rowHeight").floatValue = RowH;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (listText != null)
                {
                    // 데이터 없음 안내 등은 Summary 로 옮겨지지만, 원본도 읽기 좋게 맞춰 둔다
                    var lt = listText.GetComponent<Text>();
                    lt.fontSize = 40;
                    lt.lineSpacing = 1.1f;
                }
            }

            // 닫기 — 기존 XButton_List 를 도크의 '목록' 자리에 (누르면 그 자리에 X 가 생기는 원래 방식 유지)
            var close = Find(panel.transform, "XButton_List");
            if (close != null)
            {
                DockSlotClose(close, panel.transform, SlotList);
                // 바깥 누르기로는 닫지 않는다 (2026-10-04 대표님: X · 아래로 밀기로만) — 목록이 도크를 덮고 있어
                // 다른 도크 버튼 자리를 누르면 그 터치가 '바깥'으로 잡혀 목록이 닫혔다
                var tap = panel.GetComponent<R0926TapToClose>();
                if (tap != null) Object.DestroyImmediate(tap);
                if (pImg != null) pImg.raycastTarget = true;
            }

            // 예전 카드 배경은 숨기고, 저작권 이미지는 바닥줄로
            if (back != null)
            {
                var bImg = back.GetComponent<Image>();
                if (bImg != null) bImg.enabled = false;
                var copy = Find(back.transform, "copyright");
                if (copy != null)
                {
                    copy.transform.SetParent(sheet.transform, false);
                    copy.GetComponent<Image>().preserveAspect = true;
                    SetRect(RT(copy), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-SheetPad, 12), new Vector2(560, FooterH));
                    NoInset(copy);
                }
            }
            foreach (var vb in new[] { "VersionButton_Close", "VersionButton_Open" })
            {
                var v = Find(panel.transform, vb);
                if (v == null) continue;
                v.transform.SetParent(sheet.transform, false);
                SetRect(RT(v), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(SheetPad, 12), new Vector2(560, FooterH));
                NoInset(v);
                foreach (var t in v.GetComponentsInChildren<Text>(true))
                {
                    t.fontSize = 30; t.color = Muted;
                    var trt = t.rectTransform;
                    trt.sizeDelta = new Vector2(trt.sizeDelta.x, FooterH);
                    if (t.name == "version") trt.anchoredPosition = new Vector2(160, 0);
                    EditorUtility.SetDirty(t);
                }
            }
            log.Add("list sheet ok");
        }

        // (토글 이름, 종류, 색, 짧은 이름 ko/en/ja/zh/es)
        private static readonly (string name, R0926FilterChip.Kind kind, string hex, string[] words)[] ChipDefs =
        {
            ("CategoryToggle", R0926FilterChip.Kind.Category, "#E8EDF1", null),
            ("PetFriendlyToggle", R0926FilterChip.Kind.Pet, "#F5A623", new[] { "반려견", "Pets", "ペット", "宠物", "Mascotas" }),
            ("PublicDataToggle", R0926FilterChip.Kind.Simple, "#59B7FF", new[] { "공공", "Public", "公共", "公共", "Público" }),
            ("MetroToggle", R0926FilterChip.Kind.Simple, "#3FC1B0", new[] { "지하철", "Subway", "地下鉄", "地铁", "Metro" }),
            ("TerminalToggle", R0926FilterChip.Kind.Simple, "#7DDC54", new[] { "터미널", "Terminal", "ターミナル", "客运站", "Terminal" }),
            ("TrainStationToggle", R0926FilterChip.Kind.Simple, "#9BE15D", new[] { "기차역", "Train", "駅", "火车站", "Tren" }),
            ("3DObjectToggle", R0926FilterChip.Kind.Simple, "#FFFFFF", new[] { "3D", "3D", "3D", "3D", "3D" }),
            ("P2PUserToggle", R0926FilterChip.Kind.P2P, "#E95383", null),
        };

        private static void StyleChip(GameObject chip)
        {
            chip.transform.localScale = Vector3.one;
            var le = chip.GetComponent<LayoutElement>();
            if (le != null) { le.preferredWidth = -1; le.preferredHeight = -1; le.minWidth = -1; le.minHeight = -1; }

            var body = chip.GetComponent<Image>();
            if (body == null) body = chip.AddComponent<Image>();
            body.sprite = Spr("r0926_pill");
            body.type = Image.Type.Sliced;
            body.pixelsPerUnitMultiplier = 64f / 34f;   // 둥근 네모 (알약 아님)
            body.color = new Color(1, 1, 1, 0.07f);
            body.raycastTarget = true;

            // 체크 동그라미는 쓰지 않는다 — 칸 자체가 밝아지고 어두워진다 (토글의 graphic 참조는 그대로)
            var bg = Find(chip.transform, "Background");
            if (bg != null && bg.activeSelf) bg.SetActive(false);

            var dot = FindOrCreate(chip.transform, "Dot0926");
            var dImg = Img(dot, Spr("r0926_circle"), Color.white, Image.Type.Simple);
            dImg.raycastTarget = false;
            SetRect(RT(dot), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));

            Text t = null;
            var label = Find(chip.transform, "Label");
            if (label != null)
            {
                t = label.GetComponent<Text>();
                t.fontSize = 44;
                t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter;
                t.resizeTextForBestFit = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = true;
                if (font != null) t.font = font;
                var lrt = RT(label);
                lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                EditorUtility.SetDirty(t);
            }

            var strike = FindOrCreate(chip.transform, "Strike0926");
            var sImg = Img(strike, null, new Color(1, 1, 1, 0.35f), Image.Type.Simple);
            sImg.raycastTarget = false;
            sImg.enabled = false;
            SetRect(RT(strike), new Vector2(0.18f, 0.5f), new Vector2(0.82f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 4));
            RT(strike).localEulerAngles = new Vector3(0, 0, 8);

            var def = System.Array.Find(ChipDefs, d => d.name == chip.name);
            if (def.name == null) return;
            Image cyc = null;
            if (def.kind == R0926FilterChip.Kind.Category)
            {
                var c = FindOrCreate(chip.transform, "Cycle0926");
                cyc = Img(c, Spr("r0926_i_cycle"), new Color(1, 1, 1, 0.6f), Image.Type.Simple);
                cyc.raycastTarget = false;
                SetRect(RT(c), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            }
            var fc = Ensure<R0926FilterChip>(chip);
            var so = new SerializedObject(fc);
            so.FindProperty("kind").enumValueIndex = (int)def.kind;
            ColorUtility.TryParseHtmlString(def.hex, out var col);
            so.FindProperty("color").colorValue = col;
            var arr = so.FindProperty("names");
            arr.arraySize = def.words != null ? def.words.Length : 0;
            for (int i = 0; i < arr.arraySize; i++) arr.GetArrayElementAtIndex(i).stringValue = def.words[i];
            so.FindProperty("toggle").objectReferenceValue = chip.GetComponent<Toggle>();
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("label").objectReferenceValue = t;
            so.FindProperty("dot").objectReferenceValue = dImg;
            so.FindProperty("strike").objectReferenceValue = sImg;
            so.FindProperty("cycle").objectReferenceValue = cyc;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildRowTemplate(Transform content)
        {
            var tpl = Find(content, "RowTemplate0926");
            if (tpl == null) tpl = NewUI("RowTemplate0926", content);
            tpl.SetActive(false);
            var le = Ensure<LayoutElement>(tpl);
            le.preferredHeight = RowH; le.minHeight = RowH;

            var line = FindOrCreate(tpl.transform, "Line");
            Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));

            var dot = FindOrCreate(tpl.transform, "Dot");
            Img(dot, Spr("r0926_circle"), Color.white, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(dot), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20, 0), new Vector2(22, 22));

            var name = FindOrCreate(tpl.transform, "Name");
            var nt = Txt(name, "장소", 38, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            nt.horizontalOverflow = HorizontalWrapMode.Wrap;
            nt.verticalOverflow = VerticalWrapMode.Truncate;
            var nrt = RT(name);
            nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1); nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.offsetMin = new Vector2(52, 0); nrt.offsetMax = new Vector2(-200, 0);

            var dist = FindOrCreate(tpl.transform, "Dist");
            Txt(dist, "0m", 33, Muted, TextAnchor.MiddleRight, FontStyle.Normal);
            SetRect(RT(dist), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(190, RowH));
            return tpl;
        }

        // ============================================================
        // 스프라이트 가져오기 설정
        // ============================================================
        private static void EnsureSpriteImports(List<string> log)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Redesign0926/Sprites" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                Vector4 border = file == "r0926_pill" ? new Vector4(63, 63, 63, 63)
                               : file == "r0926_sheet" ? new Vector4(63, 1, 63, 63)
                               : Vector4.zero;
                bool dirty = ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled
                             || ti.spriteBorder != border || ti.textureCompression != TextureImporterCompression.Uncompressed
                             || ti.spriteImportMode != SpriteImportMode.Single;
                if (!dirty) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.spriteBorder = border;
                ti.SaveAndReimport();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                log.Add("import " + file);
            }
        }

        // ============================================================
        // 헬퍼
        // ============================================================
        private static Sprite Spr(string name)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");
            if (sp == null) Debug.LogWarning("[Redesign0926] 스프라이트 없음: " + name);
            return sp;
        }

        private static RectTransform RT(GameObject go) => (RectTransform)go.transform;

        public static GameObject Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject FindOrCreate(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t != null ? t.gameObject : NewUI(name, parent);
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            EditorUtility.SetDirty(rt);
        }

        private static Image Img(GameObject go, Sprite sp, Color c, Image.Type type, float ppuMul = 1f)
        {
            var img = Ensure<Image>(go);
            img.sprite = sp;
            img.type = type;
            img.color = c;
            img.pixelsPerUnitMultiplier = ppuMul;
            EditorUtility.SetDirty(img);
            return img;
        }

        private static Text Txt(GameObject go, string text, float size, Color c, TextAnchor align, FontStyle style)
        {
            var t = Ensure<Text>(go);
            if (text != null) t.text = text;
            t.fontSize = Mathf.RoundToInt(size);
            t.color = c;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            if (font != null) t.font = font;
            EditorUtility.SetDirty(t);
            return t;
        }

        private static void Loc(GameObject go, string ko, string en, string ja, string zh, string es)
        {
            var l = Ensure<R0926LocalizedText>(go);
            l.ko = ko; l.en = en; l.ja = ja; l.zh = zh; l.es = es;
            EditorUtility.SetDirty(l);
        }
    }
}
