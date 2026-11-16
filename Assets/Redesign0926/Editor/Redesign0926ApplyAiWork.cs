using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926Apply
    {
        // ============================================================
        // 장소 추가 ▸ 「이곳에 오브젝트를 추가하시겠습니까?」 ▸ 가운데 「AI 작업」 (11-12 대표님)
        // 취소 · AI 작업 · 추가하기 한 줄. 11-14 대표님 「'AI 작업' 을 누르면 여기 창 내에서 작업 … 뒤로가기 누르면 다시 '취소 / AI작업 / 추가하기' …
        // 이 버튼 하단에도 확인취소」 — 누르면 그 카드가 AI 화면이 된다 (예전엔 카드 아래에 판이 따로 붙어 키보드를 닫으면 도크 아이콘을 덮었다):
        // ‹ 뒤로 + 안내(작업 중..) · 네 칸 · (3D 캐릭터면) 요청사항 + 만들기 · '빠르게 두 바퀴' 한 줄 · 맨 아래 취소 | 확인.
        // 카드 높이 · 자리는 R0926AiWorkPanel 이 연다 (윗선 그대로 아래로 · 도크 위까지).
        // 요청사항은 키보드 위 입력줄(UploadInputMirror — 보물찾기 리뷰 칸과 같은 줄)로 쓴다 — 칸이 화면 가운데라 키보드에 가린다
        // ============================================================
        private const float AiPad = 24f, AiHeadH = 100f, AiGap = 14f, AiTileH = 240f, AiAskH = 130f, AiMakeW = 230f, AiHintH = 52f, AiFootH = 150f, AiBackW = 88f;
        private static float AiBaseH => AiPad + AiHeadH + AiGap + AiTileH + AiGap + AiFootH + AiPad;
        private static float AiCharH => AiAskH + AiGap + AiHintH + AiGap;

        private static void ApplyAiWork(Transform root, List<string> log)
        {
            var prev = Find(root, "ARPreviewPanel");
            var mc = prev != null ? Find(prev.transform, "MessageContainer") : null;
            var bc = mc != null ? Find(mc.transform, "ButtonContainer") : null;
            var confirm = bc != null ? Find(bc.transform, "ConfirmButton") : null;
            var cancel = bc != null ? Find(bc.transform, "CancelButton") : null;
            if (confirm == null || cancel == null) { log.Add("AI 작업: 미리보기 카드 없음"); return; }

            // ── 가운데 버튼 — 취소 · 추가하기와 같은 폭 ──
            var ai = FindOrCreate(bc.transform, "AiButton0926");
            cancel.transform.SetSiblingIndex(0);
            ai.transform.SetSiblingIndex(1);
            confirm.transform.SetSiblingIndex(2);
            ai.transform.localScale = Vector3.one;
            var aiBg = Img(ai, Spr("r0926_pill"), new Color(Pink.r, Pink.g, Pink.b, 0.2f), Image.Type.Sliced, 64f / 64f);
            aiBg.raycastTarget = true;
            var aiBtn = Ensure<Button>(ai);
            aiBtn.transition = Selectable.Transition.ColorTint;
            aiBtn.targetGraphic = aiBg;
            var cb = aiBtn.colors;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            aiBtn.colors = cb;
            EditorUtility.SetDirty(aiBtn);
            Ensure<UITouchForwarder>(ai);
            foreach (var b in new[] { cancel, ai, confirm })   // 글 길이와 상관없이 줄을 셋으로 똑같이 나눈다
            {
                var le = Ensure<LayoutElement>(b);
                le.minWidth = 0; le.preferredWidth = 0; le.flexibleWidth = 1;
                EditorUtility.SetDirty(le);
            }
            var row = Ensure<HorizontalLayoutGroup>(ai);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 12;
            row.padding = new RectOffset(16, 16, 0, 0);
            row.childControlWidth = true; row.childControlHeight = false;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            EditorUtility.SetDirty(row);
            var aiIcon = FindOrCreate(ai.transform, "Icon");
            aiIcon.transform.SetSiblingIndex(0);
            RT(aiIcon).sizeDelta = new Vector2(50, 50);
            var aiIconImg = Img(aiIcon, Spr("r0926_i_ai"), Pink, Image.Type.Simple);
            aiIconImg.raycastTarget = false; aiIconImg.preserveAspect = true;
            var ile = Ensure<LayoutElement>(aiIcon);
            ile.minWidth = 50; ile.preferredWidth = 50; ile.preferredHeight = 50; ile.flexibleWidth = 0;
            EditorUtility.SetDirty(ile);
            var aiLabelGo = FindOrCreate(ai.transform, "Label0926");
            aiLabelGo.transform.SetSiblingIndex(1);
            RT(aiLabelGo).sizeDelta = new Vector2(RT(aiLabelGo).sizeDelta.x, 120);
            var aiLabel = Txt(aiLabelGo, "AI 작업", 42, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            aiLabel.horizontalOverflow = HorizontalWrapMode.Wrap; aiLabel.verticalOverflow = VerticalWrapMode.Overflow;
            Loc(aiLabelGo, "AI 작업", "AI edit", "AI加工", "AI 处理", "Editar con IA");

            // ── AI 화면 — 카드를 꽉 채운다 (카드 배경 그대로 · 판 배경은 없앤다) ──
            var opt = FindOrCreate(mc.transform, "AiOptions0926");
            Stretch(RT(opt));
            var optImg = opt.GetComponent<Image>();
            if (optImg != null) Object.DestroyImmediate(optImg);

            // ‹ — 세 버튼 줄로 (만든 것은 둔다) · 안드로이드 뒤로가기도 이것
            var back = FindOrCreate(opt.transform, "Back0926");
            back.transform.SetSiblingIndex(0);
            SetRect(RT(back), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(AiPad, -AiPad), new Vector2(AiBackW, AiHeadH));
            Img(back, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            var backBtn = Ensure<Button>(back);
            backBtn.transition = Selectable.Transition.None;
            EditorUtility.SetDirty(backBtn);
            Ensure<ClickButtonOnBack>(back);
            Ensure<UITouchForwarder>(back);
            var backIc = FindOrCreate(back.transform, "Icon0926");
            SetRect(RT(backIc), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            Img(backIc, Spr("r0926_i_back"), Ink, Image.Type.Simple).raycastTarget = false;

            var st = FindOrCreate(opt.transform, "Status");
            SetRect(RT(st), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -AiPad), new Vector2(-(AiPad * 2 + (AiBackW + 12) * 2), AiHeadH));
            var status = Txt(st, "", 40, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            status.supportRichText = true;   // 작업 중. → .. → ... 를 투명 점으로 자리 지키기
            status.horizontalOverflow = HorizontalWrapMode.Wrap; status.verticalOverflow = VerticalWrapMode.Truncate;
            // 3D 캐릭터 안내('노란 고양이 · 빨간 모자 · 손 인사 · 아직 모르는 말: …')가 길면 두 줄로 줄여 칸 안에 다 보이게
            status.resizeTextForBestFit = true; status.resizeTextMinSize = 24; status.resizeTextMaxSize = 40;

            var tiles = FindOrCreate(opt.transform, "Tiles");
            SetRect(RT(tiles), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -(AiPad + AiHeadH + AiGap)), new Vector2(-AiPad * 2, AiTileH));
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
                Loc(lbl, words[i][0], words[i][1], words[i][2], words[i][3], words[i][4]);
                chips[i] = b; chipBgs[i] = bg; chipIcons[i] = icImg; chipLabels[i] = lt;
            }

            // ── 04 3D 캐릭터 — 네 칸 아래 요청사항 + 만들기 (카드가 그만큼 아래로 길어진다) ──
            float askTop = AiPad + AiHeadH + AiGap + AiTileH + AiGap;
            var ask = FindOrCreate(opt.transform, "Ask");
            SetRect(RT(ask), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -askTop), new Vector2(-AiPad * 2, AiAskH));
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
            Txt(mkl, "만들기", 42, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Loc(mkl, "만들기", "Make", "作る", "生成", "Crear");
            ask.SetActive(false);

            // '빠르게 두 바퀴 돌리면 다른 캐릭터' — 04 를 고르면 요청사항 아래 한 줄 (ARPreviewController.FastSpin)
            var hint = FindOrCreate(opt.transform, "Hint");
            SetRect(RT(hint), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -(askTop + AiAskH + AiGap)), new Vector2(-AiPad * 2, AiHintH));
            var ht = Txt(hint, "옆으로 빠르게 두 바퀴 돌리면 다른 캐릭터", 38, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            ht.horizontalOverflow = HorizontalWrapMode.Wrap; ht.verticalOverflow = VerticalWrapMode.Overflow;
            Loc(hint, "옆으로 빠르게 두 바퀴 돌리면 다른 캐릭터", "Spin it fast twice for a new character",
                "横に速く2回転で別のキャラに", "横向快速转两圈换角色", "Gíralo rápido dos veces: otro personaje");
            hint.SetActive(false);

            // ── 맨 아래 취소 | 확인 — 세 버튼 줄과 같은 알약 (취소 = 원래 사진으로 · 확인 = 만든 것을 두고) ──
            var foot = FindOrCreate(opt.transform, "Foot");
            SetRect(RT(foot), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, AiPad), new Vector2(-AiPad * 2, AiFootH));
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
            opt.SetActive(false);

            // ── 키보드 위 입력줄 — 요청사항 (보물찾기 리뷰 칸과 같은 줄) ──
            var bar = prev.transform.Find("UploadInputMirror");
            if (bar == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UploadInputMirror.prefab");
                if (prefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, prev.transform);
                    inst.name = "UploadInputMirror";
                    bar = inst.transform;
                }
            }
            UploadInputMirror mirror = null;
            if (bar == null) log.Add("AI 작업: 입력줄 프리팹 없음 — 요청사항은 칸에서 바로 친다");
            else
            {
                bar.SetAsLastSibling();
                bar.gameObject.SetActive(false);
                mirror = Ensure<UploadInputMirror>(prev);
                var mso = new SerializedObject(mirror);
                SetArray(mso.FindProperty("sourceInputs"), field);
                var page = Find(root, "UploadPage");
                var refM = page != null ? page.GetComponent<UploadInputMirror>() : null;
                if (refM != null)
                {
                    var rso = new SerializedObject(refM);
                    foreach (var p in new[] { "keyboardTopPadding", "lerpSpeed", "createBackdrop" })
                    {
                        var src = rso.FindProperty(p);
                        if (src != null && mso.FindProperty(p) != null) mso.CopyFromSerializedProperty(src);
                    }
                }
                mso.ApplyModifiedPropertiesWithoutUndo();
                InputBar(bar.gameObject, "TextInput", "SendButton", true, log);
                WireMirror(mirror, bar, log);
            }

            // ── 연결 ──
            ARPreviewController preview = null;
            foreach (var g in root.gameObject.scene.GetRootGameObjects())
            {
                preview = g.GetComponentInChildren<ARPreviewController>(true);
                if (preview != null) break;
            }
            if (preview == null) log.Add("AI 작업: ARPreviewController 없음");
            else
            {
                var pso = new SerializedObject(preview);
                var pp = pso.FindProperty("previewPanel").objectReferenceValue as GameObject;
                if (pp != prev) log.Add("AI 작업: 미리보기 창이 ARPreviewPanel 이 아님 — " + (pp != null ? pp.name : "없음"));
            }
            var panel = Ensure<R0926AiWorkPanel>(prev);
            var so = new SerializedObject(panel);
            so.FindProperty("preview").objectReferenceValue = preview;
            so.FindProperty("aiButton").objectReferenceValue = aiBtn;
            so.FindProperty("aiButtonBg").objectReferenceValue = aiBg;
            so.FindProperty("aiButtonIcon").objectReferenceValue = aiIconImg;
            so.FindProperty("aiButtonLabel").objectReferenceValue = aiLabel;
            so.FindProperty("confirmButton").objectReferenceValue = confirm.GetComponent<Button>();
            so.FindProperty("card").objectReferenceValue = RT(mc);
            so.FindProperty("cardText").objectReferenceValue = Find(mc.transform, "MessageText");
            so.FindProperty("cardButtons").objectReferenceValue = bc;
            so.FindProperty("options").objectReferenceValue = RT(opt);
            so.FindProperty("back").objectReferenceValue = backBtn;
            so.FindProperty("viewCancel").objectReferenceValue = viewCancel;
            so.FindProperty("viewConfirm").objectReferenceValue = viewConfirm;
            so.FindProperty("hint").objectReferenceValue = hint;
            so.FindProperty("status").objectReferenceValue = status;
            SetArray(so.FindProperty("chips"), chips);
            SetArray(so.FindProperty("chipBgs"), chipBgs);
            SetArray(so.FindProperty("chipIcons"), chipIcons);
            SetArray(so.FindProperty("chipLabels"), chipLabels);
            so.FindProperty("ask").objectReferenceValue = ask;
            so.FindProperty("request").objectReferenceValue = field;
            so.FindProperty("make").objectReferenceValue = mkBtn;
            so.FindProperty("mirror").objectReferenceValue = mirror;
            so.FindProperty("optionsHeight").floatValue = AiBaseH;
            so.FindProperty("askHeight").floatValue = AiCharH;
            so.FindProperty("dockGap").floatValue = 24f;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (Find(mc.transform, "MessageText") == null) log.Add("AI 작업: 카드 글(MessageText) 없음");
            log.Add("AI 작업 ok — 카드 안 화면 · ‹ · 4칸 · 요청사항 · 취소/확인" + (mirror != null ? " (키보드 위 입력줄)" : ""));
        }
    }
}
