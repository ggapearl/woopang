using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>v7: 하늘 날씨(월드 공간 날씨판 + 오브젝트 우선 칩) · 'AI에게 묻기'(목록 시트).</summary>
    public static partial class Redesign0926Apply
    {
        private static void ApplySkyAndAI(Transform root, List<string> log)
        {
            // ── 하늘 날씨판: 씬 루트의 월드 공간 캔버스 (UI 캔버스와 별개) ──
            var scene = root.gameObject.scene;
            GameObject board = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == "SkyWeather0926") board = r;
            if (board == null)
            {
                board = new GameObject("SkyWeather0926", typeof(RectTransform));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(board, scene);
            }
            board.layer = 5;
            var cv = Ensure<Canvas>(board);
            cv.renderMode = RenderMode.WorldSpace;
            var brt = RT(board);
            brt.sizeDelta = new Vector2(1000, 620);
            brt.localScale = Vector3.one * 0.25f;
            var cg = Ensure<CanvasGroup>(board);
            cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

            var temp = FindOrCreate(board.transform, "Temp0926");
            var tt = Txt(temp, "19°", 300, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
            tt.horizontalOverflow = HorizontalWrapMode.Overflow; tt.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(RT(temp), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(1000, 320));
            AddShadow(temp);
            var cond = FindOrCreate(board.transform, "Cond0926");
            var ct = Txt(cond, "맑음", 72, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(cond), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -350), new Vector2(1000, 100));
            AddShadow(cond);
            var det = FindOrCreate(board.transform, "Detail0926");
            var dt = Txt(det, "미세먼지 좋음 · 일몰 18:22 · 바람 1.2m/s", 46, new Color(1, 1, 1, 0.88f), TextAnchor.UpperCenter, FontStyle.Normal);
            dt.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(RT(det), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -460), new Vector2(1000, 140));
            AddShadow(det);
            board.SetActive(false);

            // ── 오브젝트 우선일 때 위쪽 작은 칩 (UI 캔버스) ──
            var chip = FindOrCreate(root, "SkyChip0926");
            chip.transform.SetSiblingIndex(Find(root, "Dock0926") != null ? Find(root, "Dock0926").transform.GetSiblingIndex() : chip.transform.GetSiblingIndex());
            Img(chip, Spr("r0926_pill"), Glass, Image.Type.Sliced, 64f / 40f).raycastTarget = false;
            SetRect(RT(chip), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(360, 84));
            Ensure<R0926SafeInset>(chip);
            var ccg = Ensure<CanvasGroup>(chip); ccg.alpha = 0f; ccg.blocksRaycasts = false;
            var dot = FindOrCreate(chip.transform, "Dot0926");
            Img(dot, Spr("r0926_circle"), new Color(1f, 0.82f, 0.48f, 1f), Image.Type.Simple).raycastTarget = false;
            SetRect(RT(dot), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46, 0), new Vector2(26, 26));
            var chipT = FindOrCreate(chip.transform, "Label0926");
            var cht = Txt(chipT, "맑음 19°", 34, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var crt = RT(chipT); crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = new Vector2(76, 0); crt.offsetMax = new Vector2(-24, 0);

            var marker = Find(root, "Redesign0926Marker");
            var sky = Ensure<R0926SkyWeather>(marker != null ? marker : root.gameObject);
            var so = new SerializedObject(sky);
            so.FindProperty("skyBoard").objectReferenceValue = board.transform;
            so.FindProperty("skyGroup").objectReferenceValue = cg;
            so.FindProperty("tempText").objectReferenceValue = tt;
            so.FindProperty("condText").objectReferenceValue = ct;
            so.FindProperty("detailText").objectReferenceValue = dt;
            so.FindProperty("chipGroup").objectReferenceValue = ccg;
            so.FindProperty("chipText").objectReferenceValue = cht;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── AI에게 묻기: 목록 시트의 집계 줄 오른쪽 ──
            var sheet = Find(root, "Sheet0926");
            var rows = root.GetComponentInChildren<R0926PlaceRows>(true);
            if (sheet != null)
            {
                var btn = FindOrCreate(sheet.transform, "AskAI0926");
                Img(btn, Spr("r0926_pill"), new Color(Pink.r, Pink.g, Pink.b, 0.16f), Image.Type.Sliced, 64f / 30f).raycastTarget = true;
                SetRect(RT(btn), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-SheetPad, SummaryY + 6), new Vector2(250, 58));
                var bl = FindOrCreate(btn.transform, "Label0926");
                Txt(bl, "AI에게 묻기", 30, new Color(0.95f, 0.48f, 0.63f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
                var blr = RT(bl); blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one; blr.offsetMin = Vector2.zero; blr.offsetMax = Vector2.zero;
                Loc(bl, "AI에게 묻기", "Ask AI", "AIに聞く", "问 AI", "Preguntar a IA");
                // 집계 글자가 버튼과 겹치지 않게
                var summary = Find(sheet.transform, "Summary0926");
                if (summary != null) RT(summary).sizeDelta = new Vector2(-SheetPad * 2 - 270, 44);

                var ask = Ensure<R0926AskAI>(sheet);
                var aiSheet = BuildAskSheet(root, ask);
                var aso = new SerializedObject(ask);
                aso.FindProperty("rows").objectReferenceValue = rows;
                var locText = Find(root, "LocationText");
                aso.FindProperty("areaSource").objectReferenceValue = locText != null ? locText.GetComponent<Text>() : null;
                var dist = Find(root, "DistanceValueText");
                aso.FindProperty("radiusSource").objectReferenceValue = dist != null ? dist.GetComponent<Text>() : null;
                aso.FindProperty("sheet").objectReferenceValue = aiSheet;
                aso.ApplyModifiedPropertiesWithoutUndo();

                var b = Ensure<Button>(btn);
                b.targetGraphic = btn.GetComponent<Image>();
                ResetListeners(b);
                UnityEventTools.AddPersistentListener(b.onClick, ask.Open);
            }
            log.Add("sky weather + ask AI ok");
        }

        private static GameObject BuildAskSheet(Transform root, R0926AskAI ask)
        {
            var sheet = FindOrCreate(root, "AskAISheet0926");
            sheet.transform.SetAsLastSibling();
            Img(sheet, null, new Color(0, 0, 0, 0.5f), Image.Type.Simple).raycastTarget = true;
            SetRect(RT(sheet), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            OnTop(sheet);

            const float rowH = 140f, head = 140f, bottom = 40f, cancelH = 150f, gap = 16f;
            var items = new (string key, string icon, UnityAction act, string[] w)[]
            {
                ("ChatGPT", "r0926_i_chat", ask.AskChatGPT, new[] { "ChatGPT에서 묻기", "Ask ChatGPT", "ChatGPTで聞く", "在 ChatGPT 中提问", "Preguntar en ChatGPT" }),
                ("Claude", "r0926_i_chat", ask.AskClaude, new[] { "Claude에서 묻기", "Ask Claude", "Claudeで聞く", "在 Claude 中提问", "Preguntar en Claude" }),
                ("Gemini", "r0926_i_chat", ask.AskGemini, new[] { "Gemini에서 묻기 (복사 후 열기)", "Ask Gemini (copies first)", "Geminiで聞く（コピーして開く）", "在 Gemini 中提问（先复制）", "Preguntar en Gemini (copia primero)" }),
                ("Copy", "r0926_i_edit", ask.CopyOnly, new[] { "질문만 복사하기", "Copy the question", "質問をコピー", "仅复制问题", "Copiar la pregunta" }),
            };

            var cancel = FindOrCreate(sheet.transform, "Cancel0926");
            Img(cancel, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(cancel), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(-CardSide * 2, cancelH));
            Ensure<R0926SafeInset>(cancel).SetEdge(R0926SafeInset.Edge.Bottom);
            var cb = Ensure<Button>(cancel);
            ButtonLabel(cancel, CancelWords, Ink);
            ResetListeners(cb);
            UnityEventTools.AddPersistentListener(cb.onClick, ask.Close);
            Slide(cancel, 260f);

            var card = FindOrCreate(sheet.transform, "Card0926");
            Img(card, Spr("r0926_pill"), Card, Image.Type.Sliced, 64f / 56f).raycastTarget = true;
            SetRect(RT(card), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom + cancelH + gap), new Vector2(-CardSide * 2, head + rowH * items.Length));
            Ensure<R0926SafeInset>(card).SetEdge(R0926SafeInset.Edge.Bottom);
            Slide(card, 260f);

            var title = FindOrCreate(card.transform, "Title0926");
            Txt(title, "근처 장소 목록으로 내 AI에게 물어봐요", 36, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(RT(title), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-80, head));
            Loc(title, "근처 장소 목록으로 내 AI에게 물어봐요", "Ask your own AI about places nearby", "近くの場所一覧で自分のAIに聞く", "用附近地点列表询问你的 AI", "Pregunta a tu IA sobre lugares cercanos");

            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var row = FindOrCreate(card.transform, "Row_" + it.key);
                Img(row, null, new Color(1, 1, 1, 0), Image.Type.Simple).raycastTarget = true;
                SetRect(RT(row), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -head - rowH * i), new Vector2(0, rowH));
                var line = FindOrCreate(row.transform, "Line0926");
                Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
                SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-80, 2));
                var lb = FindOrCreate(row.transform, "Label0926");
                Txt(lb, it.w[0], 42, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                var lrt = RT(lb); lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(40, 0); lrt.offsetMax = new Vector2(-40, 0);
                Loc(lb, it.w[0], it.w[1], it.w[2], it.w[3], it.w[4]);
                var b = Ensure<Button>(row);
                b.targetGraphic = lb.GetComponent<Text>();
                ResetListeners(b);
                UnityEventTools.AddPersistentListener(b.onClick, it.act);
            }
            var tap = Ensure<R0926TapToClose>(sheet);
            var tso = new SerializedObject(tap);
            tso.FindProperty("closeButton").objectReferenceValue = cb;
            tso.ApplyModifiedPropertiesWithoutUndo();
            sheet.SetActive(false);
            return sheet;
        }

        private static void AddShadow(GameObject go)
        {
            var sh = Ensure<Shadow>(go);
            sh.effectColor = new Color(0, 0, 0, 0.35f);
            sh.effectDistance = new Vector2(3, -3);
        }
    }
}
