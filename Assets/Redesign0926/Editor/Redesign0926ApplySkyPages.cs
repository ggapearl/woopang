using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v38 하늘 옆 장 (2026-10-23 대표님 「'날씨' 를 옆으로 스와이프해서 … 근처 보물찾기 … '근처핫플' '근처축제' … '언론사' 라이브」).
    ///  · 날씨판(SkyWeather0926)의 기온 · 그림 · 지역 · 예보를 한 장(Weather0926)으로 묶고, 옆에 보물찾기 · 축제 · 핫플 · 친구 · 라이브 장
    ///  · 장 머리는 날씨 장의 지역 알약과 같은 자리 · 같은 유리 알약, 그 아래 세 줄 (라이브는 방송사 칸 여섯), 판 아래 점이 몇째 장인지
    ///  · 고르고 · 채우고 · 미는 건 표식의 R0926SkyPages, 판에 닿은 손가락은 판 틀의 R0926SkyBoardInput 이 넘긴다
    ///  · 10-25 대표님 「조금만 움직여도 … 사라질 수 있으니 … 고정」 — 판 안의 모든 것을 틀(Frame0926) 하나에 담아, 고정하면 틀째
    ///    화면 UI 의 고정 자리(SkyPin0926 — 도크 바로 앞)로 옮긴다. 머리 줄 오른쪽 끝 📌 · 판 전체 유리(고정했을 때만 보임)
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static readonly string[] WeatherParts = { "Temp0926", "Cond0926", "Detail0926", "Icon0926", "Region0926", "Forecast0926" };
        private const float SkyRowW = 920f, SkyRowH = 200f, SkyChipW = 440f;
        private static readonly float[] SkyRowY = { -160f, -386f, -612f };
        private static readonly Color SkyGlass = new Color(0.094f, 0.118f, 0.149f, 0.34f);   // 지역 알약과 같은 유리
        private static readonly Color SkyGold = new Color(1f, 0.82f, 0.48f, 1f);
        private static readonly Color PinGlass = new Color(0.06f, 0.08f, 0.11f, 0.55f);   // 고정한 판 바탕 — 카메라 화면 위에서도 글이 읽히게

        private class SkyRowRefs { public RectTransform root; public Image bg, icon, face; public RawImage photo; public Text initial, title, sub, right; }

        // 판의 틀 — 판 바로 아래 있던 것을 모두 담는다. 판에 꽉 차게 늘어나 판 크기가 바뀌어도(620 → 880 → 760) 안의 자리는 그대로.
        // 고정하면 이 틀째 화면 UI 로 옮겨 간다 (R0926SkyWeather.SetPinned) — 판 자체는 하늘 자리를 지키는 빈 캔버스로 남는다
        private static Transform SkyFrame(GameObject board)
        {
            var frame = FindOrCreate(board.transform, "Frame0926");
            var rt = RT(frame);
            Stretch(rt);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            var cg = Ensure<CanvasGroup>(frame);
            cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;
            var move = new List<Transform>();
            foreach (Transform c in board.transform) if (c != frame.transform) move.Add(c);
            foreach (var c in move) c.SetParent(frame.transform, false);
            return frame.transform;
        }

        // 날씨 장 — 날씨 조각들을 Weather0926 한 장으로 옮긴다 (틀과 같은 크기라 자리는 그대로)
        private static Transform WeatherPage(GameObject board)
        {
            var frame = SkyFrame(board);
            var page = FindOrCreate(frame, "Weather0926");
            Stretch(RT(page));
            Ensure<CanvasGroup>(page);
            var parts = new List<Transform>();
            foreach (var n in WeatherParts)
            {
                var t = frame.Find(n);
                if (t != null) parts.Add(t);
            }
            parts.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            foreach (var t in parts) t.SetParent(page.transform, false);
            return page.transform;
        }

        private static void ApplySkyPages(Transform root, List<string> log)
        {
            GameObject board = null;
            foreach (var r in root.gameObject.scene.GetRootGameObjects()) if (r.name == "SkyWeather0926") board = r;
            var marker = Find(root, "Redesign0926Marker");
            var sky = marker != null ? marker.GetComponent<R0926SkyWeather>() : null;
            if (board == null || sky == null) { log.Add("하늘 옆 장: 날씨판 없음"); return; }

            Ensure<GraphicRaycaster>(board);
            var frame = SkyFrame(board);
            var oldInput = board.GetComponent<R0926SkyBoardInput>();   // 손가락은 틀이 받는다 — 고정하면 틀이 판을 떠난다
            if (oldInput != null) Object.DestroyImmediate(oldInput);
            var oldHit = frame.Find("Hit0926");   // 예전 투명 판 — 유리(Glass0926)가 대신한다
            if (oldHit != null) Object.DestroyImmediate(oldHit.gameObject);
            // 판 전체(머리 알약 · 아래 점까지)에서 손가락을 받는 유리 — 맨 뒤. 하늘에선 투명, 고정하면 어두운 유리 (R0926SkyWeather.SetGlass)
            var glass = FindOrCreate(frame, "Glass0926");
            var gImg = Img(glass, Spr("r0926_pill"), PinGlass, Image.Type.Sliced, 64f / 60f);
            gImg.raycastTarget = true;
            gImg.canvasRenderer.cullTransparentMesh = false;   // 투명해도 눌리게
            SetRect(RT(glass), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 5), new Vector2(1060, 910));
            glass.transform.SetSiblingIndex(0);

            var weather = WeatherPage(board);
            weather.SetSiblingIndex(1);

            var kinds = new[] { R0926SkyPages.Kind.Hunt, R0926SkyPages.Kind.Festival, R0926SkyPages.Kind.Hot, R0926SkyPages.Kind.Friends, R0926SkyPages.Kind.Live };
            var names = new[] { "Hunt0926", "Festival0926", "Hot0926", "Friends0926", "Live0926" };
            var heads = new[] { "근처 보물찾기", "근처 축제", "근처 핫플", "근처 친구", "언론사 라이브" };
            var headIcons = new[] { "r0926_i_treasure", "r0926_f_party", "r0926_s_hot", "r0926_s_friends", "r0926_s_live" };

            var so = new SerializedObject(Ensure<R0926SkyPages>(marker));
            so.FindProperty("sky").objectReferenceValue = sky;
            var pagesProp = so.FindProperty("pages");
            pagesProp.arraySize = kinds.Length + 1;
            SkyPageProp(pagesProp.GetArrayElementAtIndex(0), R0926SkyPages.Kind.Weather, weather, null, null, null, null);

            RectTransform[] chips = null;
            for (int k = 0; k < kinds.Length; k++)
            {
                var page = SkyPage(frame, names[k], heads[k], headIcons[k], out var head, out var headIcon, out var headText);
                page.SetSiblingIndex(2 + k);
                SkyRowRefs[] rows = null;
                if (kinds[k] == R0926SkyPages.Kind.Live) chips = LiveChips(page);
                else
                {
                    rows = new SkyRowRefs[SkyRowY.Length];
                    for (int i = 0; i < rows.Length; i++) rows[i] = SkyListRow(page, i, kinds[k] == R0926SkyPages.Kind.Friends);
                }
                SkyPageProp(pagesProp.GetArrayElementAtIndex(k + 1), kinds[k], page, head, headIcon, headText, rows);
                page.gameObject.SetActive(false);   // 넣을 장은 R0926SkyPages 가 고른다
            }
            SetArray(so.FindProperty("liveChips"), chips);

            // 아래 점 — 몇째 장인지 (보는 장은 길쭉하게)
            var dots = FindOrCreate(frame, "Dots0926");
            SetRect(RT(dots), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -790), new Vector2(400, 18));
            dots.transform.SetAsLastSibling();
            var dotImgs = new Image[kinds.Length + 1];
            for (int i = 0; i < dotImgs.Length; i++)
            {
                var d = FindOrCreate(dots.transform, "Dot" + i);
                dotImgs[i] = Img(d, Spr("r0926_pill"), new Color(1, 1, 1, 0.42f), Image.Type.Sliced, 64f / 9f);
                dotImgs[i].raycastTarget = false;
                SetRect(RT(d), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170 + i * 34, 0), new Vector2(18, 18));
            }
            dots.SetActive(false);
            so.FindProperty("dots").objectReferenceValue = RT(dots);
            SetArray(so.FindProperty("dotImages"), dotImgs);

            // 📌 — 머리 알약 줄, 오른쪽 끝을 줄(카드)의 오른쪽 끝에 맞춘다 (하늘에선 화면 끝에 붙어 보였다). 안 꽂힌 핀은 흰색으로 비스듬히, 고정하면 금색으로 곧게 (R0926SkyPages.LayoutPin)
            var pin = FindOrCreate(frame, "PinBtn0926");
            Img(pin, Spr("r0926_circle"), SkyGlass, Image.Type.Simple).raycastTarget = true;
            SetRect(RT(pin), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(SkyRowW / 2f - 46f, 10), new Vector2(92, 92));
            var pinIc = FindOrCreate(pin.transform, "Icon");
            var pinImg = Img(pinIc, Spr("r0926_s_pin"), Color.white, Image.Type.Simple);
            pinImg.raycastTarget = false; pinImg.preserveAspect = true;
            SetRect(RT(pinIc), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            RT(pinIc).localEulerAngles = new Vector3(0, 0, 35f);
            pin.transform.SetAsLastSibling();
            so.FindProperty("pinButton").objectReferenceValue = RT(pin);
            so.FindProperty("pinIcon").objectReferenceValue = pinImg;
            so.FindProperty("board").objectReferenceValue = (RectTransform)frame;

            // 줄 그림 — 축제(r0926_f_*) · 장소 분류(r0926_c_*) · 하늘 옆 장(r0926_s_*) · 보물상자(비밀번호를 건 것은 자물쇠 배지)
            var icons = new List<Object>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite r0926_", new[] { SpriteDir.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("r0926_f_") && !file.StartsWith("r0926_c_") && !file.StartsWith("r0926_s_") && file != "r0926_i_treasure" && file != "r0926_i_treasure_lock") continue;
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) icons.Add(sp);
            }
            var lockIcon = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "r0926_i_treasure_lock.png");   // 막 들인 그림은 FindAssets 색인에 아직 없다
            if (lockIcon != null && !icons.Contains(lockIcon)) icons.Add(lockIcon);
            SetArray(so.FindProperty("icons"), icons.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();

            var input = new SerializedObject(Ensure<R0926SkyBoardInput>(frame.gameObject));
            input.FindProperty("pages").objectReferenceValue = marker.GetComponent<R0926SkyPages>();
            input.ApplyModifiedPropertiesWithoutUndo();

            // 고정 자리 — 화면 UI 의 도크 바로 앞(도크 · 창 아래, 화면 밖 화살표 · 장소 표시 위). 그림 없이 화면에 꽉 찬 빈 자리, 아래 가운데 기준
            var layer = FindOrCreate(root, "SkyPin0926");
            var lrt = RT(layer);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.pivot = new Vector2(0.5f, 0f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(lrt);
            var dock = Find(root, "Dock0926");
            if (dock != null && dock.transform.parent == layer.transform.parent)
            {
                int di = dock.transform.GetSiblingIndex(), li = layer.transform.GetSiblingIndex();
                layer.transform.SetSiblingIndex(li < di ? di - 1 : di);
            }
            var floor = new List<Object>();   // 고정한 판은 이것들 위에 — 주소줄 · 도크
            foreach (var n in new[] { "LocationManagerPanel", "Dock0926" })
            {
                var f = Find(root, n);
                if (f != null) floor.Add(RT(f));
            }
            var sw = new SerializedObject(sky);
            sw.FindProperty("frame").objectReferenceValue = (RectTransform)frame;
            sw.FindProperty("frameGroup").objectReferenceValue = frame.GetComponent<CanvasGroup>();
            sw.FindProperty("pinLayer").objectReferenceValue = lrt;
            sw.FindProperty("glass").objectReferenceValue = gImg;
            SetArray(sw.FindProperty("pinFloor"), floor.ToArray());
            sw.ApplyModifiedPropertiesWithoutUndo();
            log.Add("sky pages ok (" + icons.Count + " icons · floor " + floor.Count + ")");
        }

        private static Transform SkyPage(Transform frame, string name, string headKo, string headIcon, out RectTransform head, out Image icon, out Text label)
        {
            var page = FindOrCreate(frame, name);
            Stretch(RT(page));
            Ensure<CanvasGroup>(page);
            // 머리 — 날씨 장의 지역 알약 자리 · 크기 그대로 (폭은 글자에 맞춰 R0926SkyPages 가 잡는다)
            const float H = 80f, Box = 52f, PadL = 22f, Gap = 10f, PadR = 34f;
            var hd = FindOrCreate(page.transform, "Head0926");
            Img(hd, Spr("r0926_pill"), SkyGlass, Image.Type.Sliced, 64f / (H / 2f)).raycastTarget = false;
            SetRect(RT(hd), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(400, H));
            var ic = FindOrCreate(hd.transform, "Icon");
            icon = Img(ic, Spr(headIcon), Color.white, Image.Type.Simple);
            icon.raycastTarget = false; icon.preserveAspect = true;
            SetRect(RT(ic), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(PadL + Box / 2f, 0), new Vector2(Box, Box));
            var lb = FindOrCreate(hd.transform, "Label");
            label = Txt(lb, headKo, 44, new Color(1, 1, 1, 0.95f), TextAnchor.MiddleLeft, FontStyle.Bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var lrt = RT(lb);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.offsetMin = new Vector2(PadL + Box + Gap, 0); lrt.offsetMax = new Vector2(-PadR, 0);
            EditorUtility.SetDirty(lrt);
            head = RT(hd);
            return page.transform;
        }

        // 줄 — [그림 | 제목 / 둘째 줄 | 거리]. 친구 장은 그림 대신 둥근 얼굴(사진 · 첫 글자 — 친구 지도와 같은 꼴)
        private static SkyRowRefs SkyListRow(Transform page, int i, bool face)
        {
            var row = FindOrCreate(page, "Row" + i);
            SetRect(RT(row), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, SkyRowY[i]), new Vector2(SkyRowW, SkyRowH));
            var r = new SkyRowRefs { root = RT(row) };
            r.bg = Img(row, Spr("r0926_pill"), SkyGlass, Image.Type.Sliced, 64f / 52f);
            r.bg.raycastTarget = true;   // 누른 줄을 알아본다

            var ic = FindOrCreate(row.transform, "Icon");
            r.icon = Img(ic, null, Color.white, Image.Type.Simple);
            r.icon.raycastTarget = false; r.icon.preserveAspect = true;
            SetRect(RT(ic), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(94, 0), new Vector2(128, 128));
            if (face)
            {
                var fc = FindOrCreate(row.transform, "Face");
                SetRect(RT(fc), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(94, 0), new Vector2(128, 128));
                r.face = Img(fc, Spr("r0926_circle"), new Color(0.97f, 0.78f, 0.45f), Image.Type.Simple);
                r.face.raycastTarget = false;
                Ensure<Mask>(fc).showMaskGraphic = true;
                var ini = FindOrCreate(fc.transform, "Initial");
                Stretch(RT(ini));
                r.initial = Txt(ini, "?", 128 * 0.42f, Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
                ini.transform.SetAsFirstSibling();   // 사진이 있으면 글자를 덮는다
                var ph = FindOrCreate(fc.transform, "Photo");
                Stretch(RT(ph));
                r.photo = Ensure<RawImage>(ph); r.photo.raycastTarget = false; r.photo.enabled = false;
                fc.SetActive(false);
            }

            var tt = FindOrCreate(row.transform, "Title");
            r.title = Txt(tt, "", 58, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            r.title.horizontalOverflow = HorizontalWrapMode.Overflow; r.title.verticalOverflow = VerticalWrapMode.Overflow;
            var trt = RT(tt);
            trt.anchorMin = new Vector2(0, 0.5f); trt.anchorMax = new Vector2(1, 0.5f); trt.pivot = new Vector2(0, 0.5f);
            trt.offsetMin = new Vector2(188, 2); trt.offsetMax = new Vector2(-200, 76);
            EditorUtility.SetDirty(trt);
            AddShadow(tt);
            var st = FindOrCreate(row.transform, "Sub");
            r.sub = Txt(st, "", 42, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleLeft, FontStyle.Normal);
            r.sub.horizontalOverflow = HorizontalWrapMode.Overflow; r.sub.verticalOverflow = VerticalWrapMode.Overflow;
            var srt = RT(st);
            srt.anchorMin = new Vector2(0, 0.5f); srt.anchorMax = new Vector2(1, 0.5f); srt.pivot = new Vector2(0, 0.5f);
            srt.offsetMin = new Vector2(188, -66); srt.offsetMax = new Vector2(-200, -4);
            EditorUtility.SetDirty(srt);
            AddShadow(st);
            var rg = FindOrCreate(row.transform, "Right");
            r.right = Txt(rg, "", 50, SkyGold, TextAnchor.MiddleRight, FontStyle.Bold);
            r.right.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetRect(RT(rg), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-34, 0), new Vector2(240, 70));
            AddShadow(rg);
            row.SetActive(false);
            return r;
        }

        // 언론사 라이브 — 두 줄 세 칸, 누르면 그 방송사 유튜브 생방송 (R0926SkyPages 의 Handles 순서)
        private static RectTransform[] LiveChips(Transform page)
        {
            var names = new[]
            {
                new[] { "YTN", "YTN", "YTN", "YTN", "YTN" },
                new[] { "연합뉴스TV", "Yonhap TV", "聯合ニュースTV", "韩联社TV", "Yonhap TV" },
                new[] { "KBS", "KBS", "KBS", "KBS", "KBS" },
                new[] { "MBC", "MBC", "MBC", "MBC", "MBC" },
                new[] { "SBS", "SBS", "SBS", "SBS", "SBS" },
                new[] { "JTBC", "JTBC", "JTBC", "JTBC", "JTBC" },
            };
            var chips = new RectTransform[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var chip = FindOrCreate(page, "Chip" + i);
                SetRect(RT(chip), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f),
                        new Vector2(i % 2 == 0 ? -235f : 235f, SkyRowY[i / 2]), new Vector2(SkyChipW, SkyRowH));
                Img(chip, Spr("r0926_pill"), SkyGlass, Image.Type.Sliced, 64f / 52f).raycastTarget = true;
                var nm = FindOrCreate(chip.transform, "Name");
                var nt = Txt(nm, names[i][0], 58, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                nt.horizontalOverflow = HorizontalWrapMode.Overflow;
                var nrt = RT(nm);
                nrt.anchorMin = new Vector2(0, 0.5f); nrt.anchorMax = new Vector2(1, 0.5f); nrt.pivot = new Vector2(0, 0.5f);
                nrt.offsetMin = new Vector2(44, 2); nrt.offsetMax = new Vector2(-24, 76);
                EditorUtility.SetDirty(nrt);
                AddShadow(nm);
                if (names[i][0] != names[i][1]) Loc(nm, names[i][0], names[i][1], names[i][2], names[i][3], names[i][4]);
                var dot = FindOrCreate(chip.transform, "Dot");
                Img(dot, Spr("r0926_circle"), new Color(1f, 0.3f, 0.3f, 1f), Image.Type.Simple).raycastTarget = false;
                SetRect(RT(dot), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56, -35), new Vector2(22, 22));
                var sb = FindOrCreate(chip.transform, "Sub");
                var stx = Txt(sb, "생방송 보기", 40, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleLeft, FontStyle.Normal);
                stx.horizontalOverflow = HorizontalWrapMode.Overflow;
                var srt = RT(sb);
                srt.anchorMin = new Vector2(0, 0.5f); srt.anchorMax = new Vector2(1, 0.5f); srt.pivot = new Vector2(0, 0.5f);
                srt.offsetMin = new Vector2(80, -66); srt.offsetMax = new Vector2(-24, -4);
                EditorUtility.SetDirty(srt);
                AddShadow(sb);
                Loc(sb, "생방송 보기", "Watch live", "ライブを見る", "观看直播", "Ver en directo");
                chips[i] = RT(chip);
            }
            return chips;
        }

        private static void SkyPageProp(SerializedProperty p, R0926SkyPages.Kind kind, Transform page, RectTransform head, Image headIcon, Text headText, SkyRowRefs[] rows)
        {
            p.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            p.FindPropertyRelative("root").objectReferenceValue = page;
            p.FindPropertyRelative("group").objectReferenceValue = page.GetComponent<CanvasGroup>();
            p.FindPropertyRelative("head").objectReferenceValue = head;
            p.FindPropertyRelative("headIcon").objectReferenceValue = headIcon;
            p.FindPropertyRelative("headText").objectReferenceValue = headText;
            var rp = p.FindPropertyRelative("rows");
            rp.arraySize = rows != null ? rows.Length : 0;
            for (int i = 0; i < rp.arraySize; i++)
            {
                var e = rp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                e.FindPropertyRelative("bg").objectReferenceValue = rows[i].bg;
                e.FindPropertyRelative("icon").objectReferenceValue = rows[i].icon;
                e.FindPropertyRelative("face").objectReferenceValue = rows[i].face;
                e.FindPropertyRelative("photo").objectReferenceValue = rows[i].photo;
                e.FindPropertyRelative("initial").objectReferenceValue = rows[i].initial;
                e.FindPropertyRelative("title").objectReferenceValue = rows[i].title;
                e.FindPropertyRelative("sub").objectReferenceValue = rows[i].sub;
                e.FindPropertyRelative("right").objectReferenceValue = rows[i].right;
            }
        }
    }
}
