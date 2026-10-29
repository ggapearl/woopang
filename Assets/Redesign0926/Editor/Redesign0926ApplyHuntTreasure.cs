using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 보물찾기 AR 보물 — 보물상자 프리팹(앵커 + 상자 + Target)과 상태 알약 아래 알림을 만들고 캔버스의 R0926HuntTreasure 에 잇는다.
    /// 상자는 예전 금빛 큐브와 같은 금빛에 짙은 청동 띠를 두른 보물상자(R0926HuntChest · 모양은 Redesign0926ApplyHuntChest) — 장소 큐브(흰 테두리)와 한눈에 갈린다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string TreasurePrefabPath = "Assets/Redesign0926/Prefabs/HuntTreasure0926.prefab";
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
            so.FindProperty("indicatorPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/IndicatorOnly.prefab");
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
            HuntCelebrate(root, prefab, log);
        }

        // ── 찾았을 때 축하 화면 — 캔버스 맨 위, 늘 켜진 빈 틀 + 꺼 둔 Panel (R0926HuntCelebrate 가 켠다) ──
        //    10-26: 찾은 그 보물을 3D 로(ChestView — R0926HuntReveal) 두근두근 떨다 짜잔 → 상자에서 금박 카드가 솟아 뒤집힌다
        //    카드: 순위 메달(1등은 왕관) · '보물을 찾았습니다!' · 몇 번째 · 게임 이름 · 상품(선물 그림) · 주최 · 찾은 시각 · 순위 · 주최자 안내 · 아래 '확인'
        private const string PremulMatPath = "Assets/Redesign0926/Materials/R0926PremultipliedUI.mat";
        private const float CelebrateChestY = 70f, CelebrateCardY = 0f;
        private static readonly Color SealGold = new Color(1f, 0.82f, 0.42f, 1f);
        private static readonly Color SealInk = new Color(0.27f, 0.18f, 0.04f, 1f);

        private static void HuntCelebrate(Transform root, GameObject treasurePrefab, List<string> log)
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
            var reveal = Ensure<R0926HuntReveal>(host);

            var panel = FindOrCreate(host.transform, "Panel");
            Stretch(RT(panel));
            var group = Ensure<CanvasGroup>(panel);
            var dim = FindOrCreate(panel.transform, "Dim");
            Stretch(RT(dim));
            var dimImg = Img(dim, null, new Color(0.02f, 0.02f, 0.04f, 0.85f), Image.Type.Simple);
            dimImg.raycastTarget = true;   // 뒤의 AR·도크를 누르지 않게 — 두근두근 동안 누르면 바로 연다
            var skip = Ensure<Button>(dim);
            skip.transition = Selectable.Transition.None;
            skip.navigation = new Navigation { mode = Navigation.Mode.None };
            EditorUtility.SetDirty(skip);

            var content = FindOrCreate(panel.transform, "Content");
            SetRect(RT(content), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 1800));
            // 10-26: 그림 상자(Stage 의 ChestBody·ChestLid) · 글 칸(Info) → 3D 보물(ChestView) + 카드(Card) / 두근두근·짜잔 글자(Caption) 없앰
            foreach (var oldName in new[] { "Stage", "Info", "Caption" })
            {
                var old = content.transform.Find(oldName);
                if (old != null) { Object.DestroyImmediate(old.gameObject); log.Add("축하 화면: 예전 " + oldName + " 지움"); }
            }
            GameObject Layer(Transform parent, string name, Sprite sp, Color c, Vector2 pos, Vector2 size)
            {
                var g = FindOrCreate(parent, name);
                SetRect(RT(g), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
                var im = Img(g, sp, c, Image.Type.Simple);
                im.raycastTarget = false; im.preserveAspect = true;
                return g;
            }
            Text Label(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, Color c, TextAnchor align, FontStyle style, int minSize = 0)
            {
                var g = FindOrCreate(parent, name);
                SetRect(RT(g), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
                var t = Txt(g, "", fontSize, c, align, style);
                t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
                t.resizeTextForBestFit = minSize > 0; t.resizeTextMinSize = minSize > 0 ? minSize : 10; t.resizeTextMaxSize = Mathf.RoundToInt(fontSize);
                return t;
            }

            // 뒤 — 빛줄기 둘 · 번짐 (열린 뒤 돈다)
            var back = FindOrCreate(content.transform, "Back");
            SetRect(RT(back), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), Vector2.zero);
            var rays = Layer(back.transform, "Rays", Spr("r0926_rays"), new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0f), Vector2.zero, new Vector2(1500, 1500));
            var rays2 = Layer(back.transform, "Rays2", Spr("r0926_rays"), new Color(HuntAmber.r, HuntAmber.g, HuntAmber.b, 0f), Vector2.zero, new Vector2(1020, 1020));
            RT(rays2).localEulerAngles = new Vector3(0, 0, 13f);
            var glow = Layer(back.transform, "Glow", Spr("r0926_glow"), new Color(1f, 0.82f, 0.45f, 0f), new Vector2(0, 30), new Vector2(900, 900));

            // 3D 보물 — 투명 배경에 찍은 판 (미리 곱한 알파로 띄운다)
            var chestView = FindOrCreate(content.transform, "ChestView");
            SetRect(RT(chestView), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, CelebrateChestY), new Vector2(1200, 1200));
            var raw = Ensure<RawImage>(chestView);
            raw.raycastTarget = false;
            raw.color = Color.white;
            raw.texture = null;
            raw.material = PremulMaterial(log);
            raw.enabled = false;
            EditorUtility.SetDirty(raw);
            var seamGlow = Layer(content.transform, "SeamGlow", Spr("r0926_glow"), new Color(1f, 0.88f, 0.55f, 0f), new Vector2(0, CelebrateChestY), new Vector2(360, 360));

            // 카드 — 금박 9분할 (가장자리 150px → 96 단위). 뒷면은 메달에 '?', 앞면이 결과. 캔버스 폭(기준 1440×3200 · 휴대폰 약 1460)의 84%
            var card = FindOrCreate(content.transform, "Card");
            SetRect(RT(card), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, CelebrateCardY), new Vector2(1220, 1260));
            var cardBack = FindOrCreate(card.transform, "BackFace");
            Stretch(RT(cardBack));
            Img(cardBack, Spr("r0926_card"), Color.white, Image.Type.Sliced, 1.5625f).raycastTarget = false;
            Layer(cardBack.transform, "Rays", Spr("r0926_rays"), new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0.22f), Vector2.zero, new Vector2(1000, 1000));
            var backSeal = Layer(cardBack.transform, "Seal", Spr("r0926_seal"), SealGold, Vector2.zero, new Vector2(380, 380));
            var mark = Label(backSeal.transform, "Mark", new Vector2(0, 6), new Vector2(340, 340), 190, SealInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            mark.text = "?";

            var front = FindOrCreate(card.transform, "Front");
            Stretch(RT(front));
            Img(front, Spr("r0926_card"), Color.white, Image.Type.Sliced, 1.5625f).raycastTarget = false;
            var clip = FindOrCreate(front.transform, "ShineClip");
            Stretch(RT(clip));
            RT(clip).offsetMin = new Vector2(26, 26); RT(clip).offsetMax = new Vector2(-26, -26);   // 둥근 모서리 밖으로 새지 않게 금박 안쪽까지만
            Ensure<RectMask2D>(clip);
            var shine = Layer(clip.transform, "Shine", Spr("r0926_shine"), new Color(1f, 1f, 1f, 0.3f), new Vector2(-2000, 0), new Vector2(210, 1900));
            RT(shine).localEulerAngles = new Vector3(0, 0, -22f);
            shine.GetComponent<Image>().preserveAspect = false;

            var title = Label(front.transform, "Title", new Vector2(0, 440), new Vector2(1100, 110), 84, HuntGold, TextAnchor.MiddleCenter, FontStyle.Bold, 56);
            var rank = Label(front.transform, "Rank", new Vector2(0, 345), new Vector2(1100, 70), 52, Ink, TextAnchor.MiddleCenter, FontStyle.Normal, 40);
            var hunt = Label(front.transform, "Hunt", new Vector2(0, 278), new Vector2(1100, 60), 42, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);   // 길면 … (R0926HuntCelebrate.OneLine)
            var line = FindOrCreate(front.transform, "Divider");
            SetRect(RT(line), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 215), new Vector2(900, 3));
            Img(line, null, new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0.35f), Image.Type.Simple).raycastTarget = false;
            var diamond = FindOrCreate(front.transform, "Diamond");
            SetRect(RT(diamond), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 215), new Vector2(20, 20));
            RT(diamond).localEulerAngles = new Vector3(0, 0, 45f);
            Img(diamond, null, HuntGold, Image.Type.Simple).raycastTarget = false;

            var prizeBox = FindOrCreate(front.transform, "PrizeBox");
            SetRect(RT(prizeBox), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1060, 260));
            Img(prizeBox, Spr("r0926_pill"), new Color(1f, 0.92f, 0.75f, 0.06f), Image.Type.Sliced, 64f / 44f).raycastTarget = false;
            var gift = FindOrCreate(prizeBox.transform, "Gift");
            SetRect(RT(gift), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-385, -4), new Vector2(180, 190));
            RT(gift).localScale = Vector3.one * 1.04f;
            var gb = FindOrCreate(gift.transform, "Box");
            SetRect(RT(gb), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(160, 120));
            var gbi = Img(gb, Spr("r0926_gift_box"), Color.white, Image.Type.Simple); gbi.raycastTarget = false; gbi.preserveAspect = true;
            var gl = FindOrCreate(gift.transform, "Lid");
            SetRect(RT(gl), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 41), new Vector2(180, 110));
            var gli = Img(gl, Spr("r0926_gift_lid"), Color.white, Image.Type.Simple); gli.raycastTarget = false; gli.preserveAspect = true;
            var plabel = FindOrCreate(prizeBox.transform, "Label");
            SetRect(RT(plabel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-250, 64), new Vector2(730, 56));
            var plt = Txt(plabel, "상품", 40, HuntGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            plt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var prize = FindOrCreate(prizeBox.transform, "Prize");
            SetRect(RT(prize), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(-250, 32), new Vector2(730, 150));
            var pt = Txt(prize, "", 56, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            pt.horizontalOverflow = HorizontalWrapMode.Wrap; pt.verticalOverflow = VerticalWrapMode.Truncate; pt.lineSpacing = 1.05f;
            pt.resizeTextForBestFit = true; pt.resizeTextMinSize = 40; pt.resizeTextMaxSize = 56;

            // 정보 줄 — 주최 · 찾은 시각 · 순위 (값이 없는 줄은 R0926HuntCelebrate 가 빼고 위로 당긴다)
            var keys = new Text[3];
            var values = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var row = FindOrCreate(front.transform, "Meta" + i);
                SetRect(RT(row), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -170 - 70 * i), new Vector2(1000, 62));
                var k = FindOrCreate(row.transform, "Key");
                Stretch(RT(k));
                keys[i] = Txt(k, "", 40, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);
                keys[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                var v = FindOrCreate(row.transform, "Value");
                Stretch(RT(v));
                RT(v).offsetMin = new Vector2(280, 0);
                values[i] = Txt(v, "", 42, Soft, TextAnchor.MiddleRight, FontStyle.Normal);   // 길면 … (R0926HuntCelebrate.OneLine)
                values[i].horizontalOverflow = HorizontalWrapMode.Wrap; values[i].verticalOverflow = VerticalWrapMode.Truncate;
                values[i].resizeTextForBestFit = false;
            }
            var note = Label(front.transform, "Note", new Vector2(0, -450), new Vector2(1080, 130), 40, Muted, TextAnchor.MiddleCenter, FontStyle.Normal, 38);
            note.lineSpacing = 1.15f;

            // 메달 — 카드 윗변에 걸친다 (1등은 위에 왕관)
            var seal = Layer(front.transform, "Seal", Spr("r0926_seal"), SealGold, new Vector2(0, 640), new Vector2(260, 260));
            var sealNum = Label(seal.transform, "Num", new Vector2(0, 5), new Vector2(230, 230), 134, SealInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            sealNum.text = "1";
            sealNum.verticalOverflow = VerticalWrapMode.Overflow;
            var crown = Layer(front.transform, "Crown", Spr("r0926_crown"), Color.white, new Vector2(0, 800), new Vector2(170, 112));
            card.SetActive(false);

            // 확인 — 카드 아래
            var ok = FindOrCreate(content.transform, "OK");
            SetRect(RT(ok), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, CelebrateCardY - 630 - 60 - 78), new Vector2(800, 156));
            var okGroup = Ensure<CanvasGroup>(ok);
            var okImg = Img(ok, Spr("r0926_pill"), HuntGold, Image.Type.Sliced, 64f / 70f);
            okImg.raycastTarget = true;
            var okBtn = Ensure<Button>(ok);
            okBtn.transition = Selectable.Transition.ColorTint; okBtn.targetGraphic = okImg;
            Ensure<ClickButtonOnBack>(ok);   // 안드로이드 뒤로가기 = 확인 (연출 중에도 — 바로 닫고 그 게임 상세)
            var okl = FindOrCreate(ok.transform, "Label");
            Stretch(RT(okl));
            var okText = Txt(okl, "확인", 54, HuntDark, TextAnchor.MiddleCenter, FontStyle.Bold);

            // 금화·꽃가루·별빛 (content 가운데 기준) · 짜잔 번쩍(화면 전체)
            var fx = FindOrCreate(content.transform, "Fx");
            SetRect(RT(fx), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            int order = 0;
            foreach (var g in new[] { back, chestView, seamGlow, card, ok, fx }) g.transform.SetSiblingIndex(order++);
            var flash = FindOrCreate(panel.transform, "Flash");
            Stretch(RT(flash));
            var flashImg = Img(flash, null, new Color(1f, 1f, 1f, 0f), Image.Type.Simple);
            flashImg.raycastTarget = false;
            flash.transform.SetAsLastSibling();
            panel.SetActive(false);

            var rso = new SerializedObject(reveal);
            rso.FindProperty("treasurePrefab").objectReferenceValue = treasurePrefab;
            rso.FindProperty("view").objectReferenceValue = raw;
            rso.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(cel);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("dim").objectReferenceValue = dimImg;
            so.FindProperty("skipButton").objectReferenceValue = skip;
            so.FindProperty("content").objectReferenceValue = RT(content);
            so.FindProperty("reveal").objectReferenceValue = reveal;
            so.FindProperty("rays").objectReferenceValue = RT(rays);
            so.FindProperty("rays2").objectReferenceValue = RT(rays2);
            so.FindProperty("glow").objectReferenceValue = RT(glow);
            so.FindProperty("seamGlow").objectReferenceValue = RT(seamGlow);
            so.FindProperty("card").objectReferenceValue = RT(card);
            so.FindProperty("cardFront").objectReferenceValue = RT(front);
            so.FindProperty("cardBack").objectReferenceValue = RT(cardBack);
            so.FindProperty("shine").objectReferenceValue = RT(shine);
            so.FindProperty("seal").objectReferenceValue = RT(seal);
            so.FindProperty("crown").objectReferenceValue = RT(crown);
            so.FindProperty("sealNum").objectReferenceValue = sealNum;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("rankText").objectReferenceValue = rank;
            so.FindProperty("huntText").objectReferenceValue = hunt;
            so.FindProperty("prizeLabel").objectReferenceValue = plt;
            so.FindProperty("prizeText").objectReferenceValue = pt;
            so.FindProperty("noteText").objectReferenceValue = note;
            so.FindProperty("okLabel").objectReferenceValue = okText;
            var mk = so.FindProperty("metaKeys");
            var mv = so.FindProperty("metaValues");
            mk.arraySize = keys.Length; mv.arraySize = values.Length;
            for (int i = 0; i < keys.Length; i++)
            {
                mk.GetArrayElementAtIndex(i).objectReferenceValue = keys[i];
                mv.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.FindProperty("okGroup").objectReferenceValue = okGroup;
            so.FindProperty("okButton").objectReferenceValue = okBtn;
            so.FindProperty("fx").objectReferenceValue = RT(fx);
            so.FindProperty("flash").objectReferenceValue = flashImg;
            so.FindProperty("confettiRound").objectReferenceValue = Spr("r0926_circle");
            so.FindProperty("glintSprite").objectReferenceValue = Spr("r0926_glint");
            so.FindProperty("coinSprite").objectReferenceValue = Spr("r0926_coin");
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add(raw.material != null && treasurePrefab != null ? "hunt celebrate ok" : "축하 화면: 재질·보물 프리팹 빠짐");
        }

        // 3D 보물 판(RawImage) 재질 — 장면이 참조하므로 셰이더가 빌드에 들어간다
        private static Material PremulMaterial(List<string> log)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PremulMatPath);
            if (mat != null) return mat;
            var sh = Shader.Find("Hidden/R0926/PremultipliedUI");
            if (sh == null) { log.Add("축하 화면: PremultipliedUI 셰이더 없음 — 기본 UI 재질로"); return null; }
            Directory.CreateDirectory(Path.GetDirectoryName(PremulMatPath));
            mat = new Material(sh) { name = "R0926PremultipliedUI" };
            AssetDatabase.CreateAsset(mat, PremulMatPath);
            log.Add("축하 화면: PremultipliedUI 재질 만듦");
            return mat;
        }

        // 앵커(뿌리, 크기 1 — 줌이 뿌리 크기를 바꾼다) + 보물상자 1.2배(폭 약 1.25m · 누르기 쉽게 충돌 상자는 조금 크게) + 뚜껑(경첩 원점) + 박스·화살표(Target, 금색).
        // 한 번 만든 뒤엔 열어서 고쳐 저장한다 — 새로 저장하면 내부 ID 가 바뀌어 장면의 참조가 끊길 수 있다
        private static GameObject TreasurePrefab(List<string> log)
        {
            var parts = ChestAssets(log);
            if (parts == null) return null;
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
                // 10-22 금빛 큐브 → 보물상자: 큐브를 돌리던 GoldenGlowCube 는 뗀다 (돌기·누름·열기·별빛은 R0926HuntChest)
                var cube = chest.GetComponent<GoldenGlowCube>();
                if (cube != null) { Object.DestroyImmediate(cube); log.Add("보물: 금빛 큐브(GoldenGlowCube) 떼어 냄 → 보물상자"); }
                Ensure<MeshFilter>(chest).sharedMesh = parts.body;
                var mr = Ensure<MeshRenderer>(chest);
                mr.sharedMaterials = new[] { parts.gold, parts.trim, parts.treasure };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                var bc = Ensure<BoxCollider>(chest);
                bc.center = ChestBoxCenter;
                bc.size = ChestBoxSize;

                var lidT = chest.transform.Find("Lid");
                var lid = lidT != null ? lidT.gameObject : new GameObject("Lid");
                lid.transform.SetParent(chest.transform, false);
                lid.transform.localPosition = ChestHinge;
                lid.transform.localRotation = Quaternion.identity;
                lid.transform.localScale = Vector3.one;
                Ensure<MeshFilter>(lid).sharedMesh = parts.lid;
                var lr = Ensure<MeshRenderer>(lid);
                lr.sharedMaterials = new[] { parts.gold, parts.trim };
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                // 비밀번호를 건 보물 — 앞에 매다는 자물쇠 (꺼 둔다 · R0926HuntChest.Lock 이 켜고 Unlock 이 풀어 떨군다)
                var padT = chest.transform.Find("Padlock");
                var pad = padT != null ? padT.gameObject : new GameObject("Padlock");
                pad.transform.SetParent(chest.transform, false);
                var first = parts.skins[0];
                pad.transform.localPosition = first.lockPos;
                pad.transform.localRotation = Quaternion.Euler(first.lockTilt, 0f, 0f);
                pad.transform.localScale = Vector3.one * first.lockScale;
                LockPart(pad.transform, "Body", parts.lockBody, new[] { parts.steel, parts.gold, parts.trim }, Vector3.zero);
                var shackle = LockPart(pad.transform, "Shackle", parts.lockShackle, new[] { parts.steel }, new Vector3(-ShackleR, 0f, 0f));
                pad.SetActive(false);

                // 제자리에서 천천히 돈다 — 위아래로 떠다니지 않게 (10-14 대표님: '위아래로 움직이지? 제자리에 가만히')
                var box = Ensure<R0926HuntChest>(chest);
                var cso = new SerializedObject(box);
                cso.FindProperty("lid").objectReferenceValue = lid.transform;
                cso.FindProperty("spinSpeed").floatValue = 24f;
                cso.FindProperty("padlock").objectReferenceValue = pad.transform;
                cso.FindProperty("shackle").objectReferenceValue = shackle;
                var skins = cso.FindProperty("skins");   // 주최자가 고르는 모양 (Redesign0926ApplyHuntChest.ChestSkins)
                skins.arraySize = parts.skins.Count;
                for (int i = 0; i < parts.skins.Count; i++)
                {
                    var s = parts.skins[i];
                    var e = skins.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("kind").stringValue = s.kind;
                    e.FindPropertyRelative("body").objectReferenceValue = s.body;
                    e.FindPropertyRelative("lid").objectReferenceValue = s.lid;
                    SetMaterials(e.FindPropertyRelative("bodyMats"), s.bodyMats);
                    SetMaterials(e.FindPropertyRelative("lidMats"), s.lidMats);
                    e.FindPropertyRelative("hinge").vector3Value = s.hinge;
                    e.FindPropertyRelative("seam").floatValue = s.seam;
                    e.FindPropertyRelative("boxCenter").vector3Value = s.boxCenter;
                    e.FindPropertyRelative("boxSize").vector3Value = s.boxSize;
                    e.FindPropertyRelative("lockPos").vector3Value = s.lockPos;
                    e.FindPropertyRelative("lockTilt").floatValue = s.lockTilt;
                    e.FindPropertyRelative("lockScale").floatValue = s.lockScale;
                }
                cso.ApplyModifiedPropertiesWithoutUndo();

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

        // 자물쇠 한 조각 — 누르기 상자는 없다 (보물의 누르기 상자가 받는다)
        private static Transform LockPart(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 pos)
        {
            var t = parent.Find(name);
            var go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Ensure<MeshFilter>(go).sharedMesh = mesh;
            var r = Ensure<MeshRenderer>(go);
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        private static void SetMaterials(SerializedProperty list, Material[] mats)
        {
            list.arraySize = mats.Length;
            for (int i = 0; i < mats.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = mats[i];
        }
    }
}
