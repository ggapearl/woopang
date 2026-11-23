using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926Apply
    {
        // ============================================================
        // 장소 정보 수정 · 장소수정 요청 ▸ 로고 칸 「AI 작업」 (11-18 I1 대표님 「정보수정 요청, 장소정보 수정(다른 사람 요청 및 본인 요청, 우팡M 포함)
        // 다들 각각 투명하게 만드는 거랑 AI 이용한 것들에 대한 옵션이 해당 요청에 있어서도 할 수 있도록」)
        // 로고 칸 아래쪽에 'AI 작업' 알약 — 누르면 수정 카드가 그 자리에서 AI 화면이 된다 (장소 추가의 AI 작업과 같은 네 칸 · 같은 그림):
        // ‹ + 안내 · 큰 미리보기(투명한 곳은 바둑판 · 캐릭터는 천천히 돈다) · 네 칸 · (3D 캐릭터면) 요청사항 + 만들기 · 맨 아래 취소 | 확인.
        // 요청사항은 수정 카드의 키보드 위 입력줄(Fixpage 의 UploadInputMirror)로 쓴다. 동작은 R0926FixAiPanel · 넣기/되돌리기는 CubeDataFixManager.
        // ============================================================
        private const float FixAiPad = 24f, FixAiHeadH = 100f, FixAiGap = 24f, FixAiTileH = 240f, FixAiAskH = 130f, FixAiFootH = 150f;

        private static void ApplyFixAi(Transform root, List<string> log)
        {
            var page = Find(root, "Fixpage");
            var card = page != null ? page.transform.Find("FixUploadPage") : null;
            var panel = card != null ? card.Find("Panel") : null;
            var mgr = card != null ? FindInScene<CubeDataFixManager>(card.gameObject) : null;
            if (panel == null || mgr == null) { log.Add("수정 AI 작업: 수정 카드 · 매니저 없음"); return; }
            var display = panel.Find("MainPhotoDisplay");
            var tileBtn = panel.Find("MainPhoto_Button");
            if (display == null || tileBtn == null) { log.Add("수정 AI 작업: 로고 칸 없음"); return; }

            // ── 로고 칸 아래쪽 'AI 작업' — 사진 위에 얹혀도 보이게 어두운 유리 (넣어 두면 분홍) ──
            var trt = RT(tileBtn.gameObject);
            Vector2 tilePos = trt.anchoredPosition, tileSize = trt.sizeDelta;
            const float BtnH = 84f, BtnIn = 16f;
            var ai = FindOrCreate(panel, "AiWork0926");
            At(ai, tilePos.x + BtnIn, tilePos.y - tileSize.y + BtnIn + BtnH, tileSize.x - BtnIn * 2, BtnH);
            ai.transform.SetSiblingIndex(display.GetSiblingIndex() + 1);   // 대표 사진 위 · 그 x 와는 겹치지 않는다(오른쪽 위)
            var aiBg = Img(ai, Spr("r0926_pill"), new Color(0.04f, 0.045f, 0.05f, 0.72f), Image.Type.Sliced, 64f / 42f);
            aiBg.raycastTarget = true;
            var aiBtn = Ensure<Button>(ai);
            aiBtn.transition = Selectable.Transition.ColorTint;
            aiBtn.targetGraphic = aiBg;
            EditorUtility.SetDirty(aiBtn);
            Ensure<UITouchForwarder>(ai);
            var row = Ensure<HorizontalLayoutGroup>(ai);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 10;
            row.padding = new RectOffset(12, 12, 0, 0);
            row.childControlWidth = false; row.childControlHeight = false;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            EditorUtility.SetDirty(row);
            var aiIcon = FindOrCreate(ai.transform, "Icon");
            aiIcon.transform.SetSiblingIndex(0);
            RT(aiIcon).sizeDelta = new Vector2(46, 46);
            var aiIconImg = Img(aiIcon, Spr("r0926_i_ai"), Pink, Image.Type.Simple);
            aiIconImg.raycastTarget = false; aiIconImg.preserveAspect = true;
            var aiLabelGo = FindOrCreate(ai.transform, "Label0926");
            aiLabelGo.transform.SetSiblingIndex(1);
            RT(aiLabelGo).sizeDelta = new Vector2(tileSize.x - BtnIn * 2 - 46 - 10 - 24, BtnH);
            var aiLabel = Txt(aiLabelGo, "AI 작업", 38, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            aiLabel.horizontalOverflow = HorizontalWrapMode.Overflow; aiLabel.verticalOverflow = VerticalWrapMode.Overflow;
            aiLabel.raycastTarget = false;
            Loc(aiLabelGo, "AI 작업", "AI edit", "AI加工", "AI 处理", "Editar con IA");

            // ── AI 화면 — 수정 카드를 꽉 채운다 (카드와 같은 판 · 맨 위 층 — 카드 X 도 덮는다) ──
            var view = FindOrCreate(card, "FixAi0926");
            Stretch(RT(view));
            view.transform.SetAsLastSibling();
            var cardImg = card.GetComponent<Image>();
            var vImg = Img(view, cardImg != null ? cardImg.sprite : Spr("r0926_pill"), cardImg != null ? cardImg.color : new Color(Sheet.r, Sheet.g, Sheet.b, 1f),
                Image.Type.Sliced, cardImg != null ? cardImg.pixelsPerUnitMultiplier : 64f / CardRadius);
            vImg.raycastTarget = true;   // 아래 카드 칸을 누르지 않게
            Ensure<R0926DragSink>(view);   // 칩·미리보기를 짚고 내려도 수정 카드째 닫히지 않게 (R0926SwipeDismiss)

            // ‹ — 수정 카드로 (만든 것은 로고 칸에 넣는다) · 안드로이드 뒤로가기도 이것
            var back = FindOrCreate(view.transform, "Back0926");
            SetRect(RT(back), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(FixAiPad, -FixAiPad), new Vector2(AiBackW, FixAiHeadH));
            Img(back, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            var backBtn = Ensure<Button>(back);
            backBtn.transition = Selectable.Transition.None;
            EditorUtility.SetDirty(backBtn);
            Ensure<ClickButtonOnBack>(back);
            Ensure<UITouchForwarder>(back);
            var backIc = FindOrCreate(back.transform, "Icon0926");
            SetRect(RT(backIc), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            Img(backIc, Spr("r0926_i_back"), Ink, Image.Type.Simple).raycastTarget = false;

            var st = FindOrCreate(view.transform, "Status");
            SetRect(RT(st), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -FixAiPad), new Vector2(-(FixAiPad * 2 + (AiBackW + 12) * 2), FixAiHeadH));
            var status = Txt(st, "", 40, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            status.supportRichText = true;   // 작업 중. → .. → ... 를 투명 점으로 자리 지키기
            status.horizontalOverflow = HorizontalWrapMode.Wrap; status.verticalOverflow = VerticalWrapMode.Truncate;
            status.resizeTextForBestFit = true; status.resizeTextMinSize = 24; status.resizeTextMaxSize = 40;
            status.raycastTarget = false;

            // 큰 미리보기 — 머리줄과 네 칸 사이 (높이는 R0926FixAiPanel.Layout 이 맞춘다) · 안의 정사각형 둥근 칸
            var stage = FindOrCreate(view.transform, "Stage");
            var srt = RT(stage);
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.pivot = new Vector2(0.5f, 0.5f);
            srt.offsetMin = new Vector2(FixAiPad, FixAiPad + FixAiFootH + FixAiGap + FixAiTileH + FixAiGap);
            srt.offsetMax = new Vector2(-FixAiPad, -(FixAiPad + FixAiHeadH + FixAiGap * 0.5f));
            EditorUtility.SetDirty(srt);
            var sq = FindOrCreate(stage.transform, "Square");
            Stretch(RT(sq));
            var fit = Ensure<AspectRatioFitter>(sq);
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            EditorUtility.SetDirty(fit);
            Img(sq, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / 40f).raycastTarget = false;
            var mask = Ensure<Mask>(sq);
            mask.showMaskGraphic = true;
            EditorUtility.SetDirty(mask);
            RawImage Layer(string name)
            {
                var g = FindOrCreate(sq.transform, name);
                Stretch(RT(g));
                var raw = g.GetComponent<RawImage>();
                if (raw == null)
                {
                    var other = g.GetComponent<Image>();
                    if (other != null) Object.DestroyImmediate(other);
                    raw = g.AddComponent<RawImage>();
                }
                raw.color = Color.white;
                raw.raycastTarget = false;
                EditorUtility.SetDirty(raw);
                return raw;
            }
            var checker = Layer("Checker");
            checker.transform.SetSiblingIndex(0);
            var photo = Layer("Photo");
            photo.transform.SetSiblingIndex(1);
            photo.texture = null; photo.enabled = false;
            var modelRaw = Layer("Model");
            modelRaw.transform.SetSiblingIndex(2);
            modelRaw.texture = null; modelRaw.enabled = false;
            var model = Ensure<R0926ProfileModelView>(modelRaw.gameObject);
            var mso = new SerializedObject(model);
            mso.FindProperty("view").objectReferenceValue = modelRaw;
            mso.FindProperty("background").colorValue = InputBg;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // ── 네 칸 — 장소 추가의 AI 작업과 같은 그림 · 같은 말 ──
            var tiles = FindOrCreate(view.transform, "Tiles");
            SetRect(RT(tiles), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, FixAiPad + FixAiFootH + FixAiGap), new Vector2(-FixAiPad * 2, FixAiTileH));
            var hl = Ensure<HorizontalLayoutGroup>(tiles);
            hl.spacing = 16; hl.padding = new RectOffset(0, 0, 0, 0);
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            EditorUtility.SetDirty(hl);
            string[] icons = { "r0926_i_ai_clear", "r0926_i_ai_toon", "r0926_i_ai_pencil", "r0926_i_ai_char" };
            string[][] words =
            {
                new[] { "투명하게", "Transparent", "透明に", "透明", "Transparente" },
                new[] { "만화톤", "Cartoon", "マンガ風", "漫画风", "Cómic" },
                new[] { "색연필", "Pencil", "色鉛筆", "彩铅", "Lápices" },
                new[] { "3D 캐릭터", "3D character", "3Dキャラ", "3D角色", "Personaje 3D" },
            };
            var chips = new Object[4];
            var chipBgs = new Object[4];
            var chipIcons = new Object[4];
            var chipLabels = new Object[4];
            for (int i = 0; i < 4; i++)
            {
                var t = FindOrCreate(tiles.transform, "Chip" + (i + 1));
                t.transform.SetSiblingIndex(i);
                t.transform.localScale = Vector3.one;
                var bg = Img(t, Spr("r0926_pill"), new Color(1f, 1f, 1f, 0.08f), Image.Type.Sliced, 64f / 40f);
                bg.raycastTarget = true;
                var b = Ensure<Button>(t);
                b.transition = Selectable.Transition.ColorTint;
                b.targetGraphic = bg;
                EditorUtility.SetDirty(b);
                Ensure<UITouchForwarder>(t);
                var ic = FindOrCreate(t.transform, "Icon");
                SetRect(RT(ic), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(84, 84));
                var icImg = Img(ic, Spr(icons[i]), Ink, Image.Type.Simple);
                icImg.raycastTarget = false; icImg.preserveAspect = true;
                var lbl = FindOrCreate(t.transform, "Label");
                SetRect(RT(lbl), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(-16, 100));
                var lt = Txt(lbl, words[i][0], 38, Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
                lt.horizontalOverflow = HorizontalWrapMode.Wrap; lt.verticalOverflow = VerticalWrapMode.Truncate;
                lt.lineSpacing = 0.95f;
                lt.raycastTarget = false;
                Loc(lbl, words[i][0], words[i][1], words[i][2], words[i][3], words[i][4]);
                chips[i] = b; chipBgs[i] = bg; chipIcons[i] = icImg; chipLabels[i] = lt;
            }

            // ── 04 3D 캐릭터 — 네 칸 아래 요청사항 + 만들기 (미리보기가 그만큼 줄어든다) ──
            var ask = FindOrCreate(view.transform, "Ask");
            SetRect(RT(ask), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, FixAiPad + FixAiFootH + FixAiGap), new Vector2(-FixAiPad * 2, FixAiAskH));
            var req = FindOrCreate(ask.transform, "Request");
            var rrt = RT(req);
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = new Vector2(-(AiMakeW + 16), 0);
            EditorUtility.SetDirty(rrt);
            var reqBg = Img(req, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / 48f);
            reqBg.raycastTarget = true;
            var field = Ensure<InputField>(req);
            var tg = FindOrCreate(req.transform, "Text");
            Stretch(RT(tg)); RT(tg).offsetMin = new Vector2(36, 12); RT(tg).offsetMax = new Vector2(-36, -12);
            var tx = Txt(tg, null, 42, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            tx.supportRichText = false;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.verticalOverflow = VerticalWrapMode.Truncate;
            var pg = FindOrCreate(req.transform, "Placeholder");
            Stretch(RT(pg)); RT(pg).offsetMin = new Vector2(36, 8); RT(pg).offsetMax = new Vector2(-36, -8);
            var ph = Txt(pg, "요청사항 (비우면 무작위) — 예) 노란 고양이, 빨간 모자", 38, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);
            ph.horizontalOverflow = HorizontalWrapMode.Wrap; ph.verticalOverflow = VerticalWrapMode.Truncate; ph.lineSpacing = 0.95f;
            Loc(pg, "요청사항 (비우면 무작위) — 예) 노란 고양이, 빨간 모자", "Request (blank = random) — e.g. yellow cat, red hat",
                "リクエスト（空欄でランダム）— 例）黄色いネコ、赤い帽子", "要求（留空=随机）— 例：黄色小猫、红帽子", "Petición (vacía = al azar) — p. ej. gato amarillo, gorro rojo");
            field.textComponent = tx; field.placeholder = ph;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 60;
            field.targetGraphic = reqBg;
            field.transition = Selectable.Transition.None;
            EditorUtility.SetDirty(field);

            var mk = FindOrCreate(ask.transform, "Make");
            SetRect(RT(mk), new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(AiMakeW, 0));
            var mkBg = Img(mk, Spr("r0926_pill"), Pink, Image.Type.Sliced, 64f / 64f);
            mkBg.raycastTarget = true;
            var mkBtn = Ensure<Button>(mk);
            mkBtn.transition = Selectable.Transition.ColorTint; mkBtn.targetGraphic = mkBg;
            EditorUtility.SetDirty(mkBtn);
            Ensure<UITouchForwarder>(mk);
            var mkl = FindOrCreate(mk.transform, "Label");
            Stretch(RT(mkl));
            Txt(mkl, "만들기", 42, Ink, TextAnchor.MiddleCenter, FontStyle.Bold).raycastTarget = false;
            Loc(mkl, "만들기", "Make", "作る", "生成", "Crear");
            ask.SetActive(false);

            // ── 맨 아래 취소 | 확인 (취소 = 로고 칸 그대로 · 확인 = 만든 것을 로고 칸에) ──
            var foot = FindOrCreate(view.transform, "Foot");
            SetRect(RT(foot), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, FixAiPad), new Vector2(-FixAiPad * 2, FixAiFootH));
            var fl = Ensure<HorizontalLayoutGroup>(foot);
            fl.spacing = 24; fl.padding = new RectOffset(0, 0, 0, 0);
            fl.childAlignment = TextAnchor.MiddleCenter;
            fl.childControlWidth = true; fl.childControlHeight = true;
            fl.childForceExpandWidth = true; fl.childForceExpandHeight = true;
            EditorUtility.SetDirty(fl);
            Button FootButton(string name, int index, bool primary, string[] label)
            {
                var g = FindOrCreate(foot.transform, name);
                g.transform.SetSiblingIndex(index);
                PillButton(g, primary);
                ButtonLabel(g, label, primary ? Dark : Ink);
                var b = Ensure<Button>(g);
                b.transition = Selectable.Transition.ColorTint;
                b.targetGraphic = g.GetComponent<Image>();
                EditorUtility.SetDirty(b);
                Ensure<UITouchForwarder>(g);
                return b;
            }
            var viewCancel = FootButton("Cancel", 0, false, CancelWords);
            var viewConfirm = FootButton("Confirm", 1, true, new[] { "확인", "OK", "確認", "确认", "Aceptar" });
            view.SetActive(false);

            // ── 키보드 위 입력줄 — 요청사항도 이름 · 요청사항 칸과 같은 줄로 ──
            var mirror = page.GetComponent<UploadInputMirror>();
            if (mirror == null) log.Add("수정 AI 작업: 입력줄 없음 — 요청사항은 칸에서 바로 친다");
            else
            {
                var mrso = new SerializedObject(mirror);
                var src = mrso.FindProperty("sourceInputs");
                bool has = false;
                for (int i = 0; i < src.arraySize && !has; i++) has = src.GetArrayElementAtIndex(i).objectReferenceValue == field;
                if (!has)
                {
                    src.arraySize++;
                    src.GetArrayElementAtIndex(src.arraySize - 1).objectReferenceValue = field;
                }
                mrso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── 연결 ──
            var fx = Ensure<R0926FixAiPanel>(card.gameObject);
            var so = new SerializedObject(fx);
            so.FindProperty("manager").objectReferenceValue = mgr;
            so.FindProperty("aiButton").objectReferenceValue = aiBtn;
            so.FindProperty("aiButtonBg").objectReferenceValue = aiBg;
            so.FindProperty("aiButtonIcon").objectReferenceValue = aiIconImg;
            so.FindProperty("view").objectReferenceValue = RT(view);
            so.FindProperty("back").objectReferenceValue = backBtn;
            so.FindProperty("status").objectReferenceValue = status;
            so.FindProperty("stage").objectReferenceValue = RT(stage);
            so.FindProperty("checker").objectReferenceValue = checker;
            so.FindProperty("photo").objectReferenceValue = photo;
            so.FindProperty("model").objectReferenceValue = model;
            so.FindProperty("tiles").objectReferenceValue = RT(tiles);
            SetArray(so.FindProperty("chips"), chips);
            SetArray(so.FindProperty("chipBgs"), chipBgs);
            SetArray(so.FindProperty("chipIcons"), chipIcons);
            SetArray(so.FindProperty("chipLabels"), chipLabels);
            so.FindProperty("ask").objectReferenceValue = RT(ask);
            so.FindProperty("request").objectReferenceValue = field;
            so.FindProperty("make").objectReferenceValue = mkBtn;
            so.FindProperty("foot").objectReferenceValue = RT(foot);
            so.FindProperty("viewCancel").objectReferenceValue = viewCancel;
            so.FindProperty("viewConfirm").objectReferenceValue = viewConfirm;
            so.FindProperty("mirror").objectReferenceValue = mirror;
            so.FindProperty("pad").floatValue = FixAiPad;
            so.FindProperty("headH").floatValue = FixAiHeadH;
            so.FindProperty("gap").floatValue = FixAiGap;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("수정 AI 작업 ok — 로고 칸 'AI 작업' · 카드 안 화면 · 미리보기 · 4칸 · 요청사항 · 취소/확인" + (mirror != null ? " (키보드 위 입력줄)" : ""));
        }
    }
}
