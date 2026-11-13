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
        // 취소 · AI 작업 · 추가하기 한 줄. 누르면 카드 아래로 옵션 판 — 맨 위 한 줄 안내(작업 중..) · 네 칸 · (3D 캐릭터면) 요청사항 + 만들기.
        // 요청사항은 키보드 위 입력줄(UploadInputMirror — 보물찾기 리뷰 칸과 같은 줄)로 쓴다 — 칸이 화면 가운데라 키보드에 가린다
        // ============================================================
        private const float AiPad = 24f, AiStatusH = 64f, AiGap = 14f, AiTileH = 240f, AiAskH = 130f, AiMakeW = 230f;
        private static float AiBaseH => AiPad + AiStatusH + AiGap + AiTileH + AiPad;

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

            // ── 옵션 판 — 카드 아래 빈 곳에 붙어 아래로 자란다 (카드와 같이 올라온다 · 위에 두면 올려다본 큐브를 가린다).
            //    가로 화면처럼 아래가 모자라면 R0926AiWorkPanel 이 카드 위로 옮긴다 ──
            var opt = FindOrCreate(mc.transform, "AiOptions0926");
            SetRect(RT(opt), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(0, AiBaseH));
            Img(opt, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 0.98f), Image.Type.Sliced, 64f / 56f).raycastTarget = true;

            var st = FindOrCreate(opt.transform, "Status");
            SetRect(RT(st), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -AiPad), new Vector2(-AiPad * 2 - 24, AiStatusH));
            var status = Txt(st, "", 40, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            status.supportRichText = true;   // 작업 중. → .. → ... 를 투명 점으로 자리 지키기
            status.horizontalOverflow = HorizontalWrapMode.Wrap; status.verticalOverflow = VerticalWrapMode.Truncate;

            var tiles = FindOrCreate(opt.transform, "Tiles");
            SetRect(RT(tiles), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -(AiPad + AiStatusH + AiGap)), new Vector2(-AiPad * 2, AiTileH));
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

            // ── 04 3D 캐릭터 — 요청사항 + 만들기 (판 아래쪽, 판이 위로 자란다) ──
            var ask = FindOrCreate(opt.transform, "Ask");
            SetRect(RT(ask), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, AiPad), new Vector2(-AiPad * 2, AiAskH));
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
            var ph = Txt(pg, "요청사항 — 예) 노란 고양이, 빨간 모자, 손 흔들기", 38, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);
            ph.horizontalOverflow = HorizontalWrapMode.Wrap; ph.verticalOverflow = VerticalWrapMode.Truncate; ph.lineSpacing = 0.95f;
            Loc(pg, "요청사항 — 예) 노란 고양이, 빨간 모자, 손 흔들기", "Request — e.g. yellow cat, red hat, waving",
                "リクエスト — 例）黄色いネコ、赤い帽子、手を振る", "要求 — 例：黄色小猫、红帽子、挥手", "Petición — p. ej. gato amarillo, gorro rojo, saludando");
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
            so.FindProperty("options").objectReferenceValue = RT(opt);
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
            so.FindProperty("askHeight").floatValue = AiAskH + AiGap;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("AI 작업 ok — 버튼 · 옵션 4칸 · 요청사항" + (mirror != null ? " (키보드 위 입력줄)" : ""));
        }
    }
}
