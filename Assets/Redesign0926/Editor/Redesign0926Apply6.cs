using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v9: 친구 지도 — 목록 시트 제목줄 오른쪽 '목록 | 지도' + 지도 패널.
    /// 지도 그림은 Resources/Maps0926 (압축·밉맵) — 실행 때 지도를 처음 열 때만 불러온다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string MapDir = "Assets/Redesign0926/Resources/Maps0926/";
        private static readonly Color SeaColor = new Color(0.055f, 0.071f, 0.082f, 1f);   // 지도 바다색과 같게
        private static readonly Color Green = new Color(0.384f, 0.827f, 0.616f, 1f);
        private const float ShareRowH = 116f;
        private const float MapSplit = 0.42f;   // 패널 아래 42% 는 친구 목록

        private static void ApplyFriendsMap(Transform root, List<string> log)
        {
            EnsureMapImports(log);
            var panel = Find(root, "ListPanel");
            var sheet = panel != null ? Find(panel.transform, "Sheet0926") : null;
            if (sheet == null) { log.Add("friends map: Sheet0926 없음"); return; }

            // ── 거리 숫자는 슬라이더 줄 오른쪽으로 (제목줄 오른쪽은 '목록 | 지도') ──
            var sliderUI = Find(sheet.transform, "DistanceSliderUI");
            if (sliderUI != null)
            {
                var slider = Find(sliderUI.transform, "DistanceSlider");
                if (slider != null) SetRect(RT(slider), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(-190, 56));
                var val = Find(sliderUI.transform, "DistanceValueText");
                if (val != null) SetRect(RT(val), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(172, 56));
            }

            // ── 지도 패널 ──
            var mp = FindOrCreate(sheet.transform, "MapPanel0926");
            mp.transform.SetAsLastSibling();
            var mrt = RT(mp);
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.offsetMin = Vector2.zero; mrt.offsetMax = new Vector2(0, HeaderY - 72 - 4);   // 거리 슬라이더 줄까지 덮는다 (손잡이가 삐져나왔다)
            Img(mp, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius).raycastTarget = true;

            // 지도 창 (둥근 마스크)
            var view = FindOrCreate(mp.transform, "MapView0926");
            var vrt = RT(view);
            vrt.anchorMin = new Vector2(0, MapSplit); vrt.anchorMax = Vector2.one; vrt.pivot = new Vector2(0.5f, 0.5f);
            vrt.offsetMin = new Vector2(SheetPad - 8, 0); vrt.offsetMax = new Vector2(-(SheetPad - 8), -4);
            Img(view, Spr("r0926_pill"), SeaColor, Image.Type.Sliced, 64f / 44f).raycastTarget = true;
            Ensure<Mask>(view).showMaskGraphic = true;

            var content = FindOrCreate(view.transform, "Content0926");
            SetRect(RT(content), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1000, 557));
            var world = FindOrCreate(content.transform, "World");
            Stretch(RT(world));
            var wImg = Ensure<RawImage>(world); wImg.texture = null; wImg.raycastTarget = false; wImg.color = Color.white;
            var detail = FindOrCreate(content.transform, "Detail");
            var dRt = RT(detail);
            Vector2 n1 = R0926FriendsMap.Norm(R0926FriendsMap.DetailT, R0926FriendsMap.DetailL);
            Vector2 n2 = R0926FriendsMap.Norm(R0926FriendsMap.DetailB, R0926FriendsMap.DetailR);
            dRt.anchorMin = new Vector2(n1.x, 1f - n2.y); dRt.anchorMax = new Vector2(n2.x, 1f - n1.y);
            dRt.offsetMin = Vector2.zero; dRt.offsetMax = Vector2.zero;
            var dImg = Ensure<RawImage>(detail); dImg.texture = null; dImg.raycastTarget = false; dImg.color = Color.white;

            var pins = FindOrCreate(view.transform, "Pins0926");
            Stretch(RT(pins));
            RT(pins).pivot = new Vector2(0, 1);

            // 내 위치
            var me = FindOrCreate(pins.transform, "Me0926");
            SetRect(RT(me), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
            var pulse = FindOrCreate(me.transform, "Pulse");
            SetRect(RT(pulse), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            var pulseImg = Img(pulse, Spr("r0926_circle"), new Color(Pink.r, Pink.g, Pink.b, 0.35f), Image.Type.Simple); pulseImg.raycastTarget = false;
            var ring = FindOrCreate(me.transform, "Ring");
            SetRect(RT(ring), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            Img(ring, Spr("r0926_circle"), Color.white, Image.Type.Simple).raycastTarget = false;
            var dot = FindOrCreate(me.transform, "Dot");
            SetRect(RT(dot), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            Img(dot, Spr("r0926_circle"), Pink, Image.Type.Simple).raycastTarget = false;

            var pinTpl = BuildPinTemplate(pins.transform);
            var clusterTpl = BuildClusterTemplate(pins.transform);

            // 공유 수 칩
            var chip = FindOrCreate(view.transform, "Chip0926");
            SetRect(RT(chip), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -26), new Vector2(560, 66));
            var chipBg = Img(chip, Spr("r0926_pill"), new Color(0.055f, 0.067f, 0.078f, 0.86f), Image.Type.Sliced, 64f / 33f); chipBg.raycastTarget = false;
            var chipH = Ensure<HorizontalLayoutGroup>(chip);
            chipH.padding = new RectOffset(26, 26, 0, 0); chipH.childAlignment = TextAnchor.MiddleCenter;
            chipH.childControlWidth = true; chipH.childControlHeight = true; chipH.childForceExpandWidth = false; chipH.childForceExpandHeight = true;
            Ensure<ContentSizeFitter>(chip).horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var chipTxt = FindOrCreate(chip.transform, "Text");
            var chipT = Txt(chipTxt, "", 30, Soft, TextAnchor.MiddleCenter, FontStyle.Bold);
            chipT.horizontalOverflow = HorizontalWrapMode.Overflow;

            // +, −, 내 위치
            var ctrls = FindOrCreate(view.transform, "Ctrls0926");
            SetRect(RT(ctrls), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-26, 26), new Vector2(96, 96 * 3 + 16 * 2));
            var cv = Ensure<VerticalLayoutGroup>(ctrls);
            cv.spacing = 16; cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true; cv.childForceExpandHeight = true;
            var zin = CtrlButton(ctrls.transform, "ZoomIn", "+", null);
            var zout = CtrlButton(ctrls.transform, "ZoomOut", "−", null);
            var loc = CtrlButton(ctrls.transform, "Locate", null, "r0926_i_pin");

            // 동의 · 안내 카드
            var dim = FindOrCreate(view.transform, "Dim0926");
            Stretch(RT(dim));
            Img(dim, null, new Color(0.043f, 0.051f, 0.059f, 0.66f), Image.Type.Simple).raycastTarget = true;
            var card = FindOrCreate(view.transform, "Consent0926");
            SetRect(RT(card), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-64, 0));
            Img(card, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 48f).raycastTarget = true;
            var cvl = Ensure<VerticalLayoutGroup>(card);
            cvl.padding = new RectOffset(44, 44, 44, 40); cvl.spacing = 22;
            cvl.childControlWidth = true; cvl.childControlHeight = true; cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;
            Ensure<ContentSizeFitter>(card).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var ic = FindOrCreate(card.transform, "Icon");
            var icLe = Ensure<LayoutElement>(ic); icLe.preferredHeight = 88; icLe.preferredWidth = 88;
            var icBox = FindOrCreate(ic.transform, "Box");
            SetRect(RT(icBox), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(88, 88));
            Img(icBox, Spr("r0926_pill"), new Color(Pink.r, Pink.g, Pink.b, 0.16f), Image.Type.Sliced, 64f / 28f).raycastTarget = false;
            var icG = FindOrCreate(icBox.transform, "Glyph");
            SetRect(RT(icG), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
            Img(icG, Spr("r0926_i_pin"), Pink, Image.Type.Simple).raycastTarget = false;
            var ctitle = FindOrCreate(card.transform, "Title");
            Txt(ctitle, "친구 지도", 42, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var cbody = FindOrCreate(card.transform, "Body");
            var cbT = Txt(cbody, "", 32, Soft, TextAnchor.UpperLeft, FontStyle.Normal); cbT.lineSpacing = 1.15f; cbT.horizontalOverflow = HorizontalWrapMode.Wrap;
            var srow = FindOrCreate(card.transform, "SwitchRow");
            Ensure<LayoutElement>(srow).preferredHeight = 76;
            var srowLabel = FindOrCreate(srow.transform, "Label");
            SetRect(RT(srowLabel), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(-130, 0));
            Txt(srowLabel, "내 위치 공유", 34, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Loc(srowLabel, "내 위치 공유", "Share my location", "位置を共有", "共享我的位置", "Compartir ubicación");
            var sw = FindOrCreate(srow.transform, "Switch");
            SetRect(RT(sw), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(104, 60));
            var swImg = Img(sw, Spr("r0926_sw_off"), Color.white, Image.Type.Simple); swImg.raycastTarget = true;
            var swBtn = Ensure<Button>(sw); swBtn.transition = Selectable.Transition.None;
            var note = FindOrCreate(card.transform, "Note");
            var noteT = Txt(note, "", 27, Muted, TextAnchor.UpperLeft, FontStyle.Normal); noteT.horizontalOverflow = HorizontalWrapMode.Wrap;

            // ── 친구 목록 ──
            var fl = FindOrCreate(mp.transform, "FriendList0926");
            var frt = RT(fl);
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(1, MapSplit); frt.pivot = new Vector2(0.5f, 0.5f);
            frt.offsetMin = new Vector2(SheetPad - 16, ShareRowH); frt.offsetMax = new Vector2(-(SheetPad - 16), -14);
            var sr = Ensure<ScrollRect>(fl);
            sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30;
            var flVp = FindOrCreate(fl.transform, "Viewport");
            Stretch(RT(flVp));
            Ensure<RectMask2D>(flVp);
            var flImg = Img(flVp, null, new Color(0, 0, 0, 0), Image.Type.Simple); flImg.raycastTarget = true;
            var flContent = FindOrCreate(flVp.transform, "Content");
            SetRect(RT(flContent), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            var fvl = Ensure<VerticalLayoutGroup>(flContent);
            fvl.spacing = 4; fvl.padding = new RectOffset(0, 0, 4, 12);
            fvl.childControlWidth = true; fvl.childControlHeight = true; fvl.childForceExpandWidth = true; fvl.childForceExpandHeight = false;
            Ensure<ContentSizeFitter>(flContent).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = RT(flVp); sr.content = RT(flContent);
            var rowTpl = BuildFriendRowTemplate(flContent.transform);
            // 안내 한 줄은 목록 맨 위 칸으로 (예전엔 목록 위에 겹쳐 떠서 광고 카드와 겹쳤다)
            var hint = Find(fl.transform, "Hint0926") ?? NewUI("Hint0926", flContent.transform);
            if (hint.transform.parent != flContent.transform) hint.transform.SetParent(flContent.transform, false);
            hint.transform.SetAsFirstSibling();
            Ensure<LayoutElement>(hint).preferredHeight = 64;
            Txt(hint, "", 30, Muted, TextAnchor.MiddleLeft, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Wrap;
            var ad = BuildAdSlot(flContent.transform, "friends_map");

            // ── 공유 줄 ──
            var share = FindOrCreate(mp.transform, "ShareRow0926");
            SetRect(RT(share), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, ShareRowH));
            var line = FindOrCreate(share.transform, "Line");
            SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-SheetPad * 2, 2));
            Img(line, null, new Color(1, 1, 1, 0.07f), Image.Type.Simple).raycastTarget = false;
            var shareLabel = FindOrCreate(share.transform, "Label");
            SetRect(RT(shareLabel), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(SheetPad, 0), new Vector2(-SheetPad * 2 - 140, 0));
            Txt(shareLabel, "", 31, Soft, TextAnchor.MiddleLeft, FontStyle.Normal);
            var ssw = FindOrCreate(share.transform, "Switch");
            SetRect(RT(ssw), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-SheetPad, 0), new Vector2(104, 60));
            var sswImg = Img(ssw, Spr("r0926_sw_off"), Color.white, Image.Type.Simple); sswImg.raycastTarget = true;
            var sswBtn = Ensure<Button>(ssw); sswBtn.transition = Selectable.Transition.None;

            // ── 설정 패널 (하늘 화면 등) ──
            var settings = BuildSettingsPanel(sheet);

            // ── 제목줄 오른쪽 '목록 | 지도 | 설정' ──
            var seg = FindOrCreate(sheet.transform, "ModeSeg0926");
            SetRect(RT(seg), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-SheetPad, HeaderY), new Vector2(12 + SegTabW * 3, 72));
            Img(seg, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / 36f).raycastTarget = true;
            var tabList = SegTab(seg.transform, "TabList", 0, "목록", new[] { "목록", "List", "リスト", "列表", "Lista" });
            var tabMap = SegTab(seg.transform, "TabMap", 1, "지도", new[] { "지도", "Map", "地図", "地图", "Mapa" });
            var tabSet = SegTab(seg.transform, "TabSettings", 2, "설정", new[] { "설정", "Settings", "設定", "设置", "Ajustes" });
            var modes = Ensure<R0926SheetModes>(seg);
            var mso = new SerializedObject(modes);
            SetArray(mso.FindProperty("panels"), null, mp, settings);
            SetArray(mso.FindProperty("tabs"), tabList.GetComponent<Image>(), tabMap.GetComponent<Image>(), tabSet.GetComponent<Image>());
            SetArray(mso.FindProperty("labels"), tabList.transform.Find("Text").GetComponent<Text>(), tabMap.transform.Find("Text").GetComponent<Text>(), tabSet.transform.Find("Text").GetComponent<Text>());
            mso.ApplyModifiedPropertiesWithoutUndo();
            var tabs = new[] { tabList, tabMap, tabSet };
            for (int i = 0; i < tabs.Length; i++)
            {
                var b = tabs[i].GetComponent<Button>();
                ResetListeners(b);
                UnityEventTools.AddIntPersistentListener(b.onClick, modes.Show, i);
                EditorUtility.SetDirty(b);
            }

            // ── 연결 ──
            var fm = Ensure<R0926FriendsMap>(view);
            var so = new SerializedObject(fm);
            so.FindProperty("worldImage").objectReferenceValue = wImg;
            so.FindProperty("detailImage").objectReferenceValue = dImg;
            so.FindProperty("content").objectReferenceValue = RT(content);
            so.FindProperty("pinLayer").objectReferenceValue = RT(pins);
            so.FindProperty("pinTemplate").objectReferenceValue = pinTpl;
            so.FindProperty("clusterTemplate").objectReferenceValue = clusterTpl;
            so.FindProperty("meDot").objectReferenceValue = RT(me);
            so.FindProperty("mePulse").objectReferenceValue = pulseImg;
            so.FindProperty("chipText").objectReferenceValue = chipT;
            so.FindProperty("dim").objectReferenceValue = dim;
            so.FindProperty("consentCard").objectReferenceValue = card;
            so.FindProperty("consentTitle").objectReferenceValue = ctitle.GetComponent<Text>();
            so.FindProperty("consentBody").objectReferenceValue = cbT;
            so.FindProperty("consentSwitchRow").objectReferenceValue = srow;
            so.FindProperty("consentSwitch").objectReferenceValue = swImg;
            so.FindProperty("consentNote").objectReferenceValue = noteT;
            so.FindProperty("listContent").objectReferenceValue = RT(flContent);
            so.FindProperty("rowTemplate").objectReferenceValue = rowTpl;
            so.FindProperty("listHint").objectReferenceValue = hint.GetComponent<Text>();
            so.FindProperty("adSlot").objectReferenceValue = RT(ad);
            so.FindProperty("shareText").objectReferenceValue = shareLabel.GetComponent<Text>();
            so.FindProperty("shareSwitch").objectReferenceValue = sswImg;
            so.FindProperty("switchOn").objectReferenceValue = Spr("r0926_sw_on");
            so.FindProperty("switchOff").objectReferenceValue = Spr("r0926_sw_off");
            so.ApplyModifiedPropertiesWithoutUndo();

            Wire(zin.GetComponent<Button>(), fm.ZoomIn);
            Wire(zout.GetComponent<Button>(), fm.ZoomOut);
            Wire(loc.GetComponent<Button>(), fm.LocateMe);
            Wire(swBtn, fm.ToggleShare);
            Wire(sswBtn, fm.ToggleShare);

            mp.SetActive(false);   // 처음엔 목록
            settings.SetActive(false);
            ApplySkyChipToggle(root);
            log.Add("friends map + settings ok");
        }

        private const float SegTabW = 132f;

        private static void SetArray(SerializedProperty arr, params Object[] items)
        {
            arr.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ── 하늘 칩: 판이 떠 있으면 '하늘 접기 ▲', 접혔으면 '맑음 19° ▼' — 누르면 접고 편다 ──
        private static void ApplySkyChipToggle(Transform root)
        {
            var chip = Find(root, "SkyChip0926");
            var marker = Find(root, "Redesign0926Marker");
            var sky = marker != null ? marker.GetComponent<R0926SkyWeather>() : null;
            if (chip == null || sky == null) return;
            chip.GetComponent<Image>().raycastTarget = true;
            var b = Ensure<Button>(chip); b.transition = Selectable.Transition.None;
            var arrow = FindOrCreate(chip.transform, "Arrow0926");
            SetRect(RT(arrow), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-46, 0), new Vector2(34, 34));
            Img(arrow, Spr("r0926_i_arrowdown"), Soft, Image.Type.Simple).raycastTarget = false;
            var label = Find(chip.transform, "Label0926");
            if (label != null) RT(label).offsetMax = new Vector2(-70, 0);
            var so = new SerializedObject(sky);
            so.FindProperty("chipArrow").objectReferenceValue = RT(arrow);
            so.FindProperty("chipRect").objectReferenceValue = RT(chip);
            so.ApplyModifiedPropertiesWithoutUndo();
            Wire(b, sky.ToggleCollapse);
        }

        // ── 설정 탭: 하늘 화면 ─────────────────────────────────────
        private static GameObject BuildSettingsPanel(GameObject sheet)
        {
            var sp = FindOrCreate(sheet.transform, "SettingsPanel0926");
            sp.transform.SetAsLastSibling();
            var srt = RT(sp);
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.pivot = new Vector2(0.5f, 0.5f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = new Vector2(0, HeaderY - 72 - 4);
            Img(sp, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius).raycastTarget = true;

            var sr = Ensure<ScrollRect>(sp);
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 30;
            var vp = FindOrCreate(sp.transform, "Viewport");
            Stretch(RT(vp));
            RT(vp).offsetMin = new Vector2(0, 24);
            Ensure<RectMask2D>(vp);
            Img(vp, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            var content = FindOrCreate(vp.transform, "Content");
            SetRect(RT(content), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            var vl = Ensure<VerticalLayoutGroup>(content);
            vl.padding = new RectOffset((int)SheetPad - 8, (int)SheetPad - 8, 8, 40); vl.spacing = 18;
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            Ensure<ContentSizeFitter>(content).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = RT(vp); sr.content = RT(content);

            SectionLabel(content.transform, "SecSky", new[] { "하늘 화면", "Sky", "空の表示", "天空显示", "Cielo" });
            var g1 = Group(content.transform, "GroupMaster");
            var master = SettingRow(g1.transform, "RowMaster", new[] { "하늘에 띄우기", "Show in the sky", "空に表示", "在天空显示", "Mostrar en el cielo" },
                new[] { "휴대폰을 하늘로 들면 먼 하늘에 정보가 떠요", "Lift your phone to the sky to see info", "スマホを空に向けると情報が浮かびます", "把手机举向天空即可看到信息", "Levanta el móvil al cielo para ver información" }, false);

            var g2 = Group(content.transform, "GroupChannels");
            var cg = Ensure<CanvasGroup>(g2);
            var weather = SettingRow(g2.transform, "RowWeather", new[] { "날씨", "Weather", "天気", "天气", "Tiempo" },
                new[] { "기온 · 미세먼지 · 일몰", "Temperature · air · sunset", "気温 · PM10 · 日没", "气温 · 空气 · 日落", "Temperatura · aire · atardecer" }, false);
            var events = SettingRow(g2.transform, "RowEvents", new[] { "동네 이벤트 · 게임", "Local events · games", "イベント · ゲーム", "活动 · 游戏", "Eventos · juegos" },
                new[] { "보물찾기 · 숨바꼭질 같은 이벤트", "Treasure hunts, hide-and-seek", "宝探し・かくれんぼなど", "寻宝、捉迷藏等活动", "Búsquedas del tesoro y más" }, true);
            var live = SettingRow(g2.transform, "RowLive", new[] { "라이브 방송", "Live", "ライブ配信", "直播", "En vivo" },
                new[] { "제휴사 라이브를 하늘 화면에서", "Partner live streams in the sky", "提携先のライブを空で", "合作方直播", "Directos de socios en el cielo" }, true);
            var ads = SettingRow(g2.transform, "RowAds", new[] { "광고 · 제휴 소식", "Ads · partner news", "広告 · お知らせ", "广告 · 合作消息", "Anuncios · novedades" },
                new[] { "동네 가게 소식이 날씨 옆에 한 장", "Local shop news next to the weather", "近所のお店情報を天気の横に", "附近店铺消息显示在天气旁", "Novedades de tiendas cercanas" }, true);
            var angleRow = AngleRow(g2.transform, out var angleTabs, out var angleLabels);
            var lying = SettingRow(g2.transform, "RowLying", new[] { "누워서 쓸 땐 숨기기", "Hide when lying down", "寝転んで使う時は隠す", "躺着使用时隐藏", "Ocultar al estar acostado" },
                new[] { "천장을 보고 가만히 있으면 저절로 접어요", "Folds away when you face the ceiling", "天井を向いて静止すると自動でたたみます", "对着天花板静止时自动收起", "Se pliega si miras al techo" }, false);
            var collapsed = SettingRow(g2.transform, "RowCollapsed", new[] { "처음엔 접어 두기", "Start folded", "最初はたたんでおく", "默认收起", "Empezar plegado" },
                new[] { "하늘을 봐도 위쪽 작은 칩으로만 보여요", "Only a small chip at the top", "上の小さなチップだけ表示", "只显示顶部小标签", "Solo una pequeña etiqueta arriba" }, false);

            SectionLabel(content.transform, "SecPlaces", new[] { "장소", "Places", "場所", "地点", "Lugares" });
            var g3 = Group(content.transform, "GroupHidden");
            // 오브젝트 삭제 기능 — 기본 켜짐. 끄면 장소 박스에 X 가 없다 (거리 위·이름 아래는 그대로)
            var removeX = SettingRow(g3.transform, "RowRemoveX", new[] { "오브젝트 삭제 기능", "Remove button (X)", "削除ボタン (X)", "删除按钮 (X)", "Botón de borrar (X)" },
                new[] { "장소 박스 오른쪽 위 X 로 이 기기에서 숨겨요", "Hide a place on this device with the X on its box", "マーカー右上のXでこの端末から隠せます", "点标记右上角的 X 可在本机隐藏", "Oculta un lugar con la X de su marcador" }, false);
            removeX.transform.parent.SetSiblingIndex(0);
            var hidden = FindOrCreate(g3.transform, "HiddenText");
            Ensure<LayoutElement>(hidden).preferredHeight = 104;
            var ht = Txt(hidden, "숨긴 장소 없음", 30, Soft, TextAnchor.MiddleLeft, FontStyle.Normal);
            ht.horizontalOverflow = HorizontalWrapMode.Wrap;

            var panel = Ensure<R0926SettingsPanel>(sp);
            var so = new SerializedObject(panel);
            so.FindProperty("masterSwitch").objectReferenceValue = master;
            so.FindProperty("weatherSwitch").objectReferenceValue = weather;
            so.FindProperty("eventsSwitch").objectReferenceValue = events;
            so.FindProperty("liveSwitch").objectReferenceValue = live;
            so.FindProperty("adsSwitch").objectReferenceValue = ads;
            so.FindProperty("lyingSwitch").objectReferenceValue = lying;
            so.FindProperty("collapsedSwitch").objectReferenceValue = collapsed;
            so.FindProperty("channelGroup").objectReferenceValue = cg;
            SetArray(so.FindProperty("angleTabs"), angleTabs);
            SetArray(so.FindProperty("angleLabels"), angleLabels);
            so.FindProperty("hiddenText").objectReferenceValue = ht;
            so.FindProperty("removeSwitch").objectReferenceValue = removeX;
            so.FindProperty("switchOn").objectReferenceValue = Spr("r0926_sw_on");
            so.FindProperty("switchOff").objectReferenceValue = Spr("r0926_sw_off");
            so.ApplyModifiedPropertiesWithoutUndo();

            Wire(master.transform.parent.GetComponent<Button>(), panel.ToggleMaster);
            Wire(weather.transform.parent.GetComponent<Button>(), panel.ToggleWeather);
            Wire(events.transform.parent.GetComponent<Button>(), panel.ToggleEvents);
            Wire(live.transform.parent.GetComponent<Button>(), panel.ToggleLive);
            Wire(ads.transform.parent.GetComponent<Button>(), panel.ToggleAds);
            Wire(lying.transform.parent.GetComponent<Button>(), panel.ToggleLying);
            Wire(collapsed.transform.parent.GetComponent<Button>(), panel.ToggleCollapsed);
            Wire(removeX.transform.parent.GetComponent<Button>(), panel.ToggleRemove);
            for (int i = 0; i < angleTabs.Length; i++)
            {
                var b = angleTabs[i].GetComponent<Button>();
                ResetListeners(b);
                UnityEventTools.AddIntPersistentListener(b.onClick, panel.SetAngle, i);
                EditorUtility.SetDirty(b);
            }
            return sp;
        }

        private static void SectionLabel(Transform parent, string name, string[] words)
        {
            var l = FindOrCreate(parent, name);
            Ensure<LayoutElement>(l).preferredHeight = 56;
            Txt(l, words[0], 28, Muted, TextAnchor.LowerLeft, FontStyle.Bold);
            Loc(l, words[0], words[1], words[2], words[3], words[4]);
        }

        private static GameObject Group(Transform parent, string name)
        {
            var g = FindOrCreate(parent, name);
            Img(g, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 40f).raycastTarget = false;
            var v = Ensure<VerticalLayoutGroup>(g);
            v.padding = new RectOffset(30, 30, 6, 6); v.spacing = 0;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            Ensure<ContentSizeFitter>(g).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return g;
        }

        /// <summary>제목 + 설명 + 오른쪽 스위치 한 줄 (줄 전체가 버튼). 스위치 Image 를 돌려준다</summary>
        private static Image SettingRow(Transform parent, string name, string[] title, string[] sub, bool soon)
        {
            var row = FindOrCreate(parent, name);
            Ensure<LayoutElement>(row).preferredHeight = 136;
            Img(row, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            Ensure<Button>(row).transition = Selectable.Transition.None;
            var t = FindOrCreate(row.transform, "Title");
            SetRect(RT(t), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(0, 4), new Vector2(-150, 48));
            var tt = Txt(t, title[0], 34, Ink, TextAnchor.LowerLeft, FontStyle.Bold);
            tt.horizontalOverflow = HorizontalWrapMode.Overflow;
            Loc(t, title[0], title[1], title[2], title[3], title[4]);
            var s = FindOrCreate(row.transform, "Sub");
            SetRect(RT(s), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(0, -2), new Vector2(-150, 42));
            var st = Txt(s, sub[0], 27, Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            st.horizontalOverflow = HorizontalWrapMode.Wrap; st.verticalOverflow = VerticalWrapMode.Truncate;
            Loc(s, sub[0], sub[1], sub[2], sub[3], sub[4]);
            var soonTag = row.transform.Find("Soon");
            if (soon)
            {
                var b = soonTag != null ? soonTag.gameObject : NewUI("Soon", row.transform);
                SetRect(RT(b), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-122, 0), new Vector2(66, 38));
                Img(b, Spr("r0926_pill"), new Color(Pink.r, Pink.g, Pink.b, 0.16f), Image.Type.Sliced, 64f / 19f).raycastTarget = false;
                var bt = FindOrCreate(b.transform, "Text");
                Stretch(RT(bt));
                Txt(bt, "곧", 23, Pink, TextAnchor.MiddleCenter, FontStyle.Bold);
                Loc(bt, "곧", "Soon", "近日", "即将", "Pronto");
            }
            else if (soonTag != null) Object.DestroyImmediate(soonTag.gameObject);
            var sw = FindOrCreate(row.transform, "Switch");
            SetRect(RT(sw), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(104, 60));
            var img = Img(sw, Spr("r0926_sw_on"), Color.white, Image.Type.Simple);
            img.raycastTarget = false;   // 줄 전체가 버튼
            return img;
        }

        private static GameObject AngleRow(Transform parent, out Image[] tabs, out Text[] labels)
        {
            var row = FindOrCreate(parent, "RowAngle");
            Ensure<LayoutElement>(row).preferredHeight = 210;
            var t = FindOrCreate(row.transform, "Title");
            SetRect(RT(t), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -22), new Vector2(0, 48));
            Txt(t, "보이는 각도", 34, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Loc(t, "보이는 각도", "When to show", "表示する角度", "显示角度", "Cuándo mostrar");
            var s = FindOrCreate(row.transform, "Sub");
            SetRect(RT(s), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -70), new Vector2(0, 40));
            Txt(s, "휴대폰을 얼마나 들어야 하늘 화면이 뜰지", 27, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);
            Loc(s, "휴대폰을 얼마나 들어야 하늘 화면이 뜰지", "How far to tilt your phone up", "どれだけ上に向けたら表示するか", "手机抬高多少时显示", "Cuánto inclinar el móvil");
            var seg = FindOrCreate(row.transform, "Seg");
            SetRect(RT(seg), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(0, 72));
            Img(seg, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / 36f).raycastTarget = true;
            var h = Ensure<HorizontalLayoutGroup>(seg);
            h.padding = new RectOffset(6, 6, 6, 6); h.spacing = 4;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            string[][] words =
            {
                new[] { "조금만 들어도", "A little", "少し", "稍微", "Un poco" },
                new[] { "보통", "Normal", "ふつう", "一般", "Normal" },
                new[] { "똑바로 위", "Straight up", "真上", "正上方", "Hacia arriba" },
            };
            tabs = new Image[3]; labels = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var tab = FindOrCreate(seg.transform, "Step" + i);
                tabs[i] = Img(tab, Spr("r0926_pill"), new Color(1, 1, 1, i == 1 ? 0.14f : 0f), Image.Type.Sliced, 64f / 30f);
                tabs[i].raycastTarget = true;
                Ensure<Button>(tab).transition = Selectable.Transition.None;
                var lt = FindOrCreate(tab.transform, "Text");
                Stretch(RT(lt));
                labels[i] = Txt(lt, words[i][0], 28, i == 1 ? Ink : Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                Loc(lt, words[i][0], words[i][1], words[i][2], words[i][3], words[i][4]);
            }
            return row;
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b == null) return;
            ResetListeners(b);
            UnityEventTools.AddPersistentListener(b.onClick, action);
            EditorUtility.SetDirty(b);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(rt);
        }

        private static GameObject SegTab(Transform seg, string name, int index, string label, string[] words)
        {
            var tab = FindOrCreate(seg, name);
            SetRect(RT(tab), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6 + index * SegTabW, 0), new Vector2(SegTabW, 60));
            Img(tab, Spr("r0926_pill"), new Color(1, 1, 1, index == 0 ? 0.14f : 0f), Image.Type.Sliced, 64f / 30f).raycastTarget = true;
            Ensure<Button>(tab).transition = Selectable.Transition.None;
            var t = FindOrCreate(tab.transform, "Text");
            Stretch(RT(t));
            Txt(t, label, 30, index == 0 ? Ink : Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            Loc(t, words[0], words[1], words[2], words[3], words[4]);
            return tab;
        }

        private static GameObject CtrlButton(Transform parent, string name, string glyph, string icon)
        {
            var b = FindOrCreate(parent, name);
            Img(b, Spr("r0926_circle"), new Color(0.055f, 0.067f, 0.078f, 0.9f), Image.Type.Simple).raycastTarget = true;
            Ensure<Button>(b).transition = Selectable.Transition.None;
            var g = FindOrCreate(b.transform, "Glyph");
            if (icon != null)
            {
                SetRect(RT(g), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
                Img(g, Spr(icon), Ink, Image.Type.Simple).raycastTarget = false;
            }
            else
            {
                Stretch(RT(g));
                Txt(g, glyph, 48, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            return b;
        }

        // 아바타: Avatar(묶음) > Face(원형 마스크) > Photo · Initial,  Avatar > Live(초록 점)
        private static GameObject BuildAvatar(Transform parent, float size, bool ring)
        {
            var av = FindOrCreate(parent, "Avatar");
            Ensure<CanvasGroup>(av);
            if (ring)
            {
                var r = FindOrCreate(av.transform, "Ring");
                SetRect(RT(r), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size + 10, size + 10));
                Img(r, Spr("r0926_circle"), Color.white, Image.Type.Simple).raycastTarget = false;
                r.transform.SetAsFirstSibling();
            }
            var face = FindOrCreate(av.transform, "Face");
            SetRect(RT(face), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            Img(face, Spr("r0926_circle"), new Color(0.97f, 0.78f, 0.45f), Image.Type.Simple).raycastTarget = true;
            Ensure<Mask>(face).showMaskGraphic = true;
            var photo = FindOrCreate(face.transform, "Photo");
            Stretch(RT(photo));
            var ph = Ensure<RawImage>(photo); ph.raycastTarget = false; ph.enabled = false;
            var ini = FindOrCreate(face.transform, "Initial");
            Stretch(RT(ini));
            Txt(ini, "M", size * 0.42f, Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
            ini.transform.SetAsFirstSibling();   // 사진이 있으면 글자를 덮는다
            var live = FindOrCreate(av.transform, "Live");
            SetRect(RT(live), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(4, -4), new Vector2(size * 0.34f, size * 0.34f));
            Img(live, Spr("r0926_circle"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Simple).raycastTarget = false;
            var liveIn = FindOrCreate(live.transform, "In");
            SetRect(RT(liveIn), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.22f, size * 0.22f));
            Img(liveIn, Spr("r0926_circle"), Green, Image.Type.Simple).raycastTarget = false;
            return av;
        }

        private static GameObject BuildPinTemplate(Transform pins)
        {
            var pin = FindOrCreate(pins, "PinTemplate0926");
            SetRect(RT(pin), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(200, 150));
            Img(pin, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            Ensure<Button>(pin).transition = Selectable.Transition.None;
            var av = BuildAvatar(pin.transform, 84, true);
            SetRect(RT(av), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(94, 94));
            var name = FindOrCreate(pin.transform, "Name");
            SetRect(RT(name), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(120, 42));
            Img(name, Spr("r0926_pill"), new Color(0.055f, 0.067f, 0.078f, 0.88f), Image.Type.Sliced, 64f / 21f).raycastTarget = false;
            var nh = Ensure<HorizontalLayoutGroup>(name);
            nh.padding = new RectOffset(16, 16, 0, 0); nh.childAlignment = TextAnchor.MiddleCenter;
            nh.childControlWidth = true; nh.childControlHeight = true; nh.childForceExpandWidth = false; nh.childForceExpandHeight = true;
            Ensure<ContentSizeFitter>(name).horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var nt = FindOrCreate(name.transform, "Text");
            Txt(nt, "name", 26, Soft, TextAnchor.MiddleCenter, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            pin.SetActive(false);
            return pin;
        }

        private static GameObject BuildClusterTemplate(Transform pins)
        {
            var c = FindOrCreate(pins, "ClusterTemplate0926");
            SetRect(RT(c), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132, 132));
            Img(c, Spr("r0926_circle"), new Color(Ink.r, Ink.g, Ink.b, 0.16f), Image.Type.Simple).raycastTarget = true;
            Ensure<Button>(c).transition = Selectable.Transition.None;
            var inner = FindOrCreate(c.transform, "Inner");
            SetRect(RT(inner), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(98, 98));
            Img(inner, Spr("r0926_circle"), Ink, Image.Type.Simple).raycastTarget = false;
            var t = FindOrCreate(inner.transform, "Text");
            Stretch(RT(t));
            Txt(t, "3", 42, Dark, TextAnchor.MiddleCenter, FontStyle.Bold);
            c.SetActive(false);
            return c;
        }

        private static GameObject BuildFriendRowTemplate(Transform content)
        {
            var row = FindOrCreate(content, "RowTemplate0926");
            Ensure<LayoutElement>(row).preferredHeight = 118;
            Img(row, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            Ensure<Button>(row).transition = Selectable.Transition.None;
            var sel = FindOrCreate(row.transform, "Sel");
            Stretch(RT(sel));
            Img(sel, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / 36f).raycastTarget = false;
            sel.SetActive(false);
            var av = BuildAvatar(row.transform, 84, false);
            SetRect(RT(av), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(84, 84));
            var name = FindOrCreate(row.transform, "Name");
            SetRect(RT(name), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(132, 2), new Vector2(-150, 48));
            Txt(name, "@name", 36, Ink, TextAnchor.LowerLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            var sub = FindOrCreate(row.transform, "Sub");
            SetRect(RT(sub), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(132, -4), new Vector2(-150, 40));
            Txt(sub, "", 29, Muted, TextAnchor.UpperLeft, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Overflow;
            row.SetActive(false);
            return row;
        }

        // 광고 한 칸: [썸네일] 제목 · 두 줄 문구 · '광고' 표시 · [버튼]
        private static GameObject BuildAdSlot(Transform parent, string placement)
        {
            var card = FindOrCreate(parent, "AdSlot0926");
            Ensure<LayoutElement>(card).preferredHeight = 184;
            Img(card, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 40f).raycastTarget = true;
            Ensure<Button>(card).transition = Selectable.Transition.None;

            var thumb = FindOrCreate(card.transform, "Thumb");
            SetRect(RT(thumb), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(128, 128));
            Img(thumb, Spr("r0926_pill"), new Color(Pink.r, Pink.g, Pink.b, 0.16f), Image.Type.Sliced, 64f / 32f).raycastTarget = false;
            Ensure<Mask>(thumb).showMaskGraphic = true;
            var photo = FindOrCreate(thumb.transform, "Photo");
            Stretch(RT(photo));
            var ph = Ensure<RawImage>(photo); ph.raycastTarget = false; ph.enabled = false;
            var icon = FindOrCreate(thumb.transform, "Icon");
            SetRect(RT(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            Img(icon, Spr("r0926_i_megaphone"), Pink, Image.Type.Simple).raycastTarget = false;

            var badge = FindOrCreate(card.transform, "Badge");
            SetRect(RT(badge), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(178, -30), new Vector2(76, 38));
            Img(badge, Spr("r0926_pill"), new Color(1, 1, 1, 0.1f), Image.Type.Sliced, 64f / 19f).raycastTarget = false;
            var bt = FindOrCreate(badge.transform, "Text");
            Stretch(RT(bt));
            Txt(bt, "광고", 23, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            Loc(bt, "광고", "Ad", "広告", "广告", "Anuncio");

            var title = FindOrCreate(card.transform, "Title");
            SetRect(RT(title), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(266, -24), new Vector2(-266 - 230, 50));
            var tt = Txt(title, "이 자리에 가게를 알려 보세요", 33, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            tt.horizontalOverflow = HorizontalWrapMode.Wrap; tt.verticalOverflow = VerticalWrapMode.Truncate;
            var body = FindOrCreate(card.transform, "Body");
            SetRect(RT(body), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(178, -80), new Vector2(-178 - 230, 80));
            var bdy = Txt(body, "동네 반경 안의 우팡 사용자에게만 보여요", 28, Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            bdy.horizontalOverflow = HorizontalWrapMode.Wrap; bdy.verticalOverflow = VerticalWrapMode.Truncate; bdy.lineSpacing = 1.05f;

            var ctaBox = FindOrCreate(card.transform, "Cta");
            SetRect(RT(ctaBox), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(186, 68));
            Img(ctaBox, Spr("r0926_pill"), new Color(Pink.r, Pink.g, Pink.b, 0.16f), Image.Type.Sliced, 64f / 34f).raycastTarget = false;
            var ct = FindOrCreate(ctaBox.transform, "Text");
            Stretch(RT(ct));
            Txt(ct, "광고 문의", 28, Pink, TextAnchor.MiddleCenter, FontStyle.Bold);

            var slot = Ensure<R0926AdSlot>(card);
            var so = new SerializedObject(slot);
            so.FindProperty("placement").stringValue = placement;
            so.FindProperty("photo").objectReferenceValue = ph;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("title").objectReferenceValue = tt;
            so.FindProperty("body").objectReferenceValue = bdy;
            so.FindProperty("cta").objectReferenceValue = ct.GetComponent<Text>();
            so.ApplyModifiedPropertiesWithoutUndo();
            Wire(card.GetComponent<Button>(), slot.Click);
            card.transform.SetAsLastSibling();
            return card;
        }

        private static void EnsureMapImports(List<string> log)
        {
            foreach (var (file, max) in new[] { ("r0926_worldmap", 4096), ("r0926_worldmap_asia", 2048) })
            {
                string path = MapDir + file + ".png";
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
                if (ti == null) { log.Add("지도 그림 없음: " + path); continue; }
                bool dirty = ti.textureType != TextureImporterType.Default || !ti.mipmapEnabled || ti.maxTextureSize != max
                             || ti.textureCompression != TextureImporterCompression.CompressedHQ || ti.wrapMode != TextureWrapMode.Clamp
                             || ti.alphaSource != TextureImporterAlphaSource.None;
                if (!dirty) continue;
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Trilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = max;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
                log.Add("import " + file);
            }
        }
    }
}
