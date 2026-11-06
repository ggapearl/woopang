using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 이모지 (N-U) — 10-25 대표님 「채팅창 위에 이모지 발생하는 거 삭제 … 글만」 · 10-26 「'전송버튼' 좌측에 '이모지' 버튼 … 누르면 키패드가 사라지고
    /// 이모지만 잔뜩 … 키패드 전체를 채우는 식으로」 · 11-02 「너무 뻔한 거 말고 … 키패드 각 버튼이 입력창 글자보다 큰 것마냥 · 아래로 스크롤 · 최상단에서
    /// 아래로 스와이프하면 창이 내려가도록」. 그림은 Microsoft Fluent Emoji 3D(MIT) — Emoji/README.md.
    ///  · 이모지 창(EmojiPanel0926) — 캔버스에 하나. 키보드 자리에 뜨는 몸통(Sheet0926): 손잡이 · 목록 · 아래 분류 탭 8 + ⌫. 크기·자리는 R0926EmojiPanel.Layout 이 화면에 맞춘다
    ///  · 단추(EmojiButton0926) — 대화·댓글 입력칸 오른쪽 끝(보내기 버튼 바로 왼쪽), 칸 안 아래에 붙어 여러 줄이 돼도 그 자리
    ///  · 글 속 이모지 — 입력칸(R0926EmojiInput · R0926EmojiText) · 말풍선 · 대화 목록 미리보기 · 댓글(R0926EmojiText)
    /// 댓글 입력줄 위 빠른 반응 줄(Quick0926)은 CommentBar 에서 지운다 (10-25 「채팅창 위에 이모지 … 삭제」)
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static readonly Color EmojiBg = new Color(0.106f, 0.118f, 0.133f, 1f);     // 대화창 바탕보다 한 톤 밝게 — 키보드 자리라는 게 보이게
        private static readonly Color EmojiIcon = new Color(0.70f, 0.73f, 0.76f, 1f);
        private const float EmojiTabsH = 136f, EmojiTopPad = 44f;                            // R0926EmojiPanel 과 같은 값 (실제 자리는 Layout 이)

        private static void ApplyEmoji(Transform root, List<string> log)
        {
            EnsureEmojiFont(log);
            var panel = BuildEmojiPanel(root);

            var room = root.Find("ChatRoomPanel");
            var area = root.Find("ChatRoomPanel/Clip0926/Background/InputArea") ?? root.Find("ChatRoomPanel/Background/InputArea");
            var chatInput = area != null ? area.Find("ChatInput")?.GetComponent<InputField>() : null;
            if (room != null && chatInput != null) EmojiKey(chatInput, room.GetComponent<MobileKeyboardHandler>(), panel, 100f, 64f, 16f);
            else log.Add("이모지: 대화 입력칸 없음");

            var cm = FindInScene<CommentManager>(root.gameObject);
            var field = cm != null ? cm.commentInputField : null;
            if (field != null && cm.commentPanel != null) EmojiKey(field, cm.commentPanel.GetComponent<MobileKeyboardHandler>(), panel, 90f, 58f, 10f);
            else log.Add("이모지: 댓글 입력칸 없음");

            // 장소 추가·수정 — 키보드 위 입력줄 (11-05 대표님 「'장소추가' 하는 탭의 텍스트입력필드에서도 '이모지'」). 이 칸들을 쓸 때만 단추가 보인다
            EmojiKeyOnMirror(root.Find("UploadPage"), new[] { "Clip0926/UploadSheet0926/CubeUploadPage/Panel/NameInput", "Clip0926/UploadSheet0926/ModelUploadPage/Panel/NameInput" }, panel, log);
            EmojiKeyOnMirror(root.Find("Fixpage"), new[] { "FixUploadPage/Panel/NameInput", "FixUploadPage/Panel/DescriptionInput" }, panel, log);

            const string Dm = "Assets/Prefabs/DM/";
            EmojiOnPrefab(Dm + "MyMessageBubble.prefab", "ContentText", log);
            EmojiOnPrefab(Dm + "OtherMessageBubble.prefab", "ContentText", log);
            EmojiOnPrefab(Dm + "AdminMessageBubble.prefab", "ContentText", log);
            EmojiOnPrefab(Dm + "ConversationItem.prefab", "PreviewText", log);
            EmojiOnPrefab(Dm + "AdminNoticeItem.prefab", "PreviewText", log);
            EmojiOnPrefab("Assets/Prefabs/CommentItem_Template.prefab", "Text_content", log);
            log.Add("emoji ok");
        }

        // ── 이모지 창 ─────────────────────────────────────────
        private static R0926EmojiPanel BuildEmojiPanel(Transform root)
        {
            var go = FindOrCreate(root, "EmojiPanel0926");
            go.SetActive(true);
            Stretch(RT(go));
            var cv = Ensure<Canvas>(go);
            var cvso = new SerializedObject(cv);   // 꺼진 오브젝트엔 overrideSorting 대입이 안 먹는다 — 직렬화 값으로
            cvso.FindProperty("m_OverrideSorting").boolValue = true;
            cvso.FindProperty("m_SortingOrder").intValue = 8000;   // 대화·댓글 창 위, 사진 크게 보기(9200)·토스트(9500) 아래
            cvso.ApplyModifiedPropertiesWithoutUndo();
            Ensure<GraphicRaycaster>(go);
            var panel = Ensure<R0926EmojiPanel>(go);
            var center = new Vector2(0.5f, 0.5f);
            var left = new Vector2(0, 0.5f);
            var right = new Vector2(1, 0.5f);

            // 몸통 — 화면 아래에 붙어 키보드 높이만큼. 바탕이 탭 줄 · ⌫ · 손잡이를 누른 손가락을 받아 위의 R0926EmojiPanel 로
            var sheet = FindOrCreate(go.transform, "Sheet0926");
            sheet.SetActive(true);
            SetRect(RT(sheet), Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 1280));
            var bg = FindOrCreate(sheet.transform, "Bg0926");
            Stretch(RT(bg));
            Img(bg, null, EmojiBg, Image.Type.Simple).raycastTarget = true;
            var line = FindOrCreate(sheet.transform, "Line0926");
            SetRect(RT(line), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));
            Img(line, null, Line, Image.Type.Simple).raycastTarget = false;
            var handle = FindOrCreate(sheet.transform, "Handle0926");
            SetRect(RT(handle), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(96, 12));
            Img(handle, Spr("r0926_pill"), new Color(1, 1, 1, 0.22f), Image.Type.Sliced, 63f / 6f).raycastTarget = false;

            // 목록 — 세로로만. 칸·이모지 그림은 실행 때 R0926EmojiPanel 이 Content 아래에 만든다
            var sv = FindOrCreate(sheet.transform, "Scroll0926");
            var svr = RT(sv);
            svr.anchorMin = Vector2.zero; svr.anchorMax = Vector2.one; svr.pivot = center;
            svr.offsetMin = new Vector2(0, EmojiTabsH); svr.offsetMax = new Vector2(0, -EmojiTopPad);
            EditorUtility.SetDirty(svr);
            var vp = FindOrCreate(sv.transform, "Viewport");
            Stretch(RT(vp));
            Ensure<RectMask2D>(vp);
            Img(vp, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            var content = FindOrCreate(vp.transform, "Content");
            SetRect(RT(content), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            var press = FindOrCreate(content.transform, "PressMark");   // 누른 칸 — 그림보다 뒤
            press.transform.SetAsFirstSibling();
            SetRect(RT(press), new Vector2(0, 1), new Vector2(0, 1), center, Vector2.zero, new Vector2(186, 170));
            var pressImg = Img(press, Spr("r0926_pill"), new Color(1, 1, 1, 0.12f), Image.Type.Sliced, 63f / 36f);
            pressImg.raycastTarget = false;
            press.SetActive(false);
            var sr = Ensure<R0926EmojiScroll>(sv);
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic; sr.elasticity = 0.1f;
            sr.inertia = true; sr.decelerationRate = 0.135f; sr.scrollSensitivity = 40f;
            sr.viewport = RT(vp); sr.content = RT(content);
            EditorUtility.SetDirty(sr);
            var sso = new SerializedObject(sr);
            sso.FindProperty("panel").objectReferenceValue = panel;
            sso.ApplyModifiedPropertiesWithoutUndo();

            // 아래 탭 줄 — 자주 쓰는 것 + 7 분류 · 맨 오른쪽 ⌫
            var tabs = FindOrCreate(sheet.transform, "Tabs0926");
            SetRect(RT(tabs), Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, EmojiTabsH));
            var tline = FindOrCreate(tabs.transform, "Line0926");
            SetRect(RT(tline), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));
            Img(tline, null, Line, Image.Type.Simple).raycastTarget = false;
            var mark = FindOrCreate(tabs.transform, "Mark0926");
            mark.transform.SetSiblingIndex(1);
            SetRect(RT(mark), left, left, center, Vector2.zero, new Vector2(120, 104));
            var markImg = Img(mark, Spr("r0926_pill"), new Color(1, 1, 1, 0.1f), Image.Type.Sliced, 63f / 30f);
            markImg.raycastTarget = false;
            var cells = new RectTransform[R0926EmojiPanel.Sections];
            var icons = new RawImage[R0926EmojiPanel.Sections];
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = FindOrCreate(tabs.transform, "Tab" + i);
                SetRect(RT(cell), left, left, center, Vector2.zero, new Vector2(150, EmojiTabsH));
                cells[i] = RT(cell);
                var ic = FindOrCreate(cell.transform, "Icon");
                SetRect(RT(ic), center, center, center, Vector2.zero, new Vector2(72, 72));
                var ri = Ensure<RawImage>(ic);
                ri.raycastTarget = false;
                ri.color = new Color(1, 1, 1, 0.45f);
                EditorUtility.SetDirty(ri);
                icons[i] = ri;
            }
            var del = FindOrCreate(tabs.transform, "Delete0926");
            SetRect(RT(del), right, right, right, Vector2.zero, new Vector2(150, EmojiTabsH));
            var dic = FindOrCreate(del.transform, "Icon");
            SetRect(RT(dic), center, center, center, Vector2.zero, new Vector2(76, 76));
            var delImg = Img(dic, Spr("r0926_i_backspace"), Color.white, Image.Type.Simple);
            delImg.raycastTarget = false;
            delImg.preserveAspect = true;

            // 뒤로가기 = 창만 내린다 (떠 있을 때만 등록된다 — 몸통이 꺼지면 빠진다)
            var back = FindOrCreate(sheet.transform, "Back0926");
            SetRect(RT(back), center, center, center, Vector2.zero, Vector2.zero);
            var bb = Ensure<Button>(back);
            bb.transition = Selectable.Transition.None;
            bb.navigation = new Navigation { mode = Navigation.Mode.None };
            Ensure<ClickButtonOnBack>(back);
            ResetListeners(bb);
            UnityEventTools.AddPersistentListener(bb.onClick, panel.CloseByBack);
            EditorUtility.SetDirty(bb);

            var so = new SerializedObject(panel);
            so.FindProperty("sheet").objectReferenceValue = RT(sheet);
            so.FindProperty("scroll").objectReferenceValue = sr;
            so.FindProperty("content").objectReferenceValue = RT(content);
            so.FindProperty("tabs").objectReferenceValue = RT(tabs);
            SetArray(so.FindProperty("tabCells"), cells);
            SetArray(so.FindProperty("tabIcons"), icons);
            so.FindProperty("tabMark").objectReferenceValue = markImg;
            so.FindProperty("deleteKey").objectReferenceValue = RT(del);
            so.FindProperty("deleteIcon").objectReferenceValue = delImg;
            so.FindProperty("pressMark").objectReferenceValue = pressImg;
            so.FindProperty("font").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            sheet.SetActive(false);
            return panel;
        }

        // ── 입력칸 오른쪽 끝 단추 · 칸 안 글 ─────────────────────
        private static GameObject EmojiKey(InputField field, MobileKeyboardHandler kb, R0926EmojiPanel panel, float size, float icon, float pad, bool inset = true)
        {
            var key = FindOrCreate(field.transform, "EmojiButton0926");
            var corner = new Vector2(1, 0);   // 칸 안 오른쪽 아래 — 여러 줄로 늘어나도 맨 아래 줄 옆
            SetRect(RT(key), corner, corner, corner, new Vector2(-pad, pad), new Vector2(size, size));
            var hit = Img(key, null, new Color(0, 0, 0, 0), Image.Type.Simple);
            hit.raycastTarget = true;
            var b = Ensure<Button>(key);
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.targetGraphic = hit;
            ResetListeners(b);   // 누름은 R0926EmojiButton 이 직접
            var ic = FindOrCreate(key.transform, "Icon0926");
            var center = new Vector2(0.5f, 0.5f);
            SetRect(RT(ic), center, center, center, Vector2.zero, new Vector2(icon, icon));
            var iconImg = Img(ic, Spr("r0926_i_smile"), EmojiIcon, Image.Type.Simple);
            iconImg.raycastTarget = false;
            iconImg.preserveAspect = true;

            var eb = Ensure<R0926EmojiButton>(key);
            var so = new SerializedObject(eb);
            so.FindProperty("input").objectReferenceValue = field;
            so.FindProperty("keyboard").objectReferenceValue = kb;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("icon").objectReferenceValue = iconImg;
            so.FindProperty("smile").objectReferenceValue = Spr("r0926_i_smile");
            so.FindProperty("keys").objectReferenceValue = Spr("r0926_i_keyboard");
            so.ApplyModifiedPropertiesWithoutUndo();

            // 글이 단추 밑으로 들어가지 않게 (키보드 위 입력줄은 단추를 켤 때 UploadInputMirror 가 줄인다)
            float textRight = pad + size + 4f;
            foreach (var t in inset ? new[] { field.textComponent, field.placeholder as Text } : new Text[0])
            {
                if (t == null) continue;
                var trt = t.rectTransform;
                trt.offsetMax = new Vector2(-textRight, trt.offsetMax.y);
                EditorUtility.SetDirty(trt);
            }
            Ensure<R0926EmojiInput>(field.gameObject);
            if (field.textComponent != null)
            {
                var et = Ensure<R0926EmojiText>(field.textComponent.gameObject);
                et.convert = false;   // 칸 글은 R0926EmojiInput 이 바꾼다
                EditorUtility.SetDirty(et);
            }
            return key;
        }

        // 키보드 위 입력줄 — sources 칸을 쓸 때만 단추를 켠다. 원래 칸 글에도 그림 (입력줄이 우팡 이모지 글자로 옮겨 준다)
        private static void EmojiKeyOnMirror(Transform page, string[] sources, R0926EmojiPanel panel, List<string> log)
        {
            var m = page != null ? page.GetComponent<UploadInputMirror>() : null;
            if (m == null) { log.Add("이모지: 키보드 위 입력줄 없음 " + (page != null ? page.name : "?")); return; }
            var so = new SerializedObject(m);
            var bar = so.FindProperty("mirrorInput").objectReferenceValue as InputField;
            if (bar == null) { log.Add("이모지: 입력줄 칸 없음 " + page.name); return; }
            var list = new List<InputField>();
            foreach (var path in sources)
            {
                var t = page.Find(path);
                var f = t != null ? t.GetComponent<InputField>() : null;
                if (f == null) { log.Add("이모지: 칸 없음 " + page.name + "/" + path); continue; }
                list.Add(f);
                if (f.textComponent == null) continue;
                var et = Ensure<R0926EmojiText>(f.textComponent.gameObject);
                et.convert = false;
                EditorUtility.SetDirty(et);
            }
            const float Size = 86f, Icon = 56f, Pad = 12f;   // 입력줄 칸 높이 110 (Apply10 InputBar) — 가운데에
            var key = EmojiKey(bar, null, panel, Size, Icon, Pad, false);
            var bso = new SerializedObject(key.GetComponent<R0926EmojiButton>());
            bso.FindProperty("mirror").objectReferenceValue = m;
            bso.ApplyModifiedPropertiesWithoutUndo();
            float textRight = bar.textComponent != null ? -bar.textComponent.rectTransform.offsetMax.x : 0f;
            var arr = so.FindProperty("emojiSources");
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.FindProperty("emojiKey").objectReferenceValue = key;
            so.FindProperty("emojiInset").floatValue = Mathf.Max(0f, Pad + Size + 4f - textRight);
            so.ApplyModifiedPropertiesWithoutUndo();
            key.SetActive(false);   // 이모지 칸을 쓸 때만 입력줄이 켠다
            log.Add("이모지 입력줄: " + page.name + " (칸 " + list.Count + ")");
        }

        // 말풍선 · 대화 목록 미리보기 · 댓글 — 글 속 이모지 자리에 그림 (이미 있으면 프리팹을 다시 쓰지 않는다)
        private static void EmojiOnPrefab(string path, string child, List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add("이모지: 프리팹 없음 " + path); return; }
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var t = Find(go.transform, child);
                var text = t != null ? t.GetComponent<Text>() : null;
                if (text == null) { log.Add("이모지: 글 없음 " + path + " › " + child); return; }
                if (text.GetComponent<R0926EmojiText>() != null) return;
                text.gameObject.AddComponent<R0926EmojiText>();
                PrefabUtility.SaveAsPrefabAsset(go, path);
                log.Add("이모지 글: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        private const string EmojiFontPath = "Assets/Resources/Fonts/WoopangEmoji.ttf";
        private static readonly string[] BodyFontPaths =
        {
            "Assets/Resources/Fonts/AppleSDGothicNeoM.ttf",
            "Assets/TextMesh Pro/Fonts/AppleSDGothicNeoM.ttf",
        };

        // 본문 글꼴 두 벌의 대체 글꼴 두 번째에 WoopangEmoji — 이모지 자리 글자(U+E000~)가 1.18em 폭을 갖는다.
        // 속성(ti.fontNames)으로 넣으면 저장 때 되돌아가 직렬화 필드에 쓴다 (EnsureFontFallback 과 같음 — 그쪽 목록에도 두 번째로 있다)
        internal static void EnsureEmojiFont(List<string> log)
        {
            var emojiFont = AssetDatabase.LoadAssetAtPath<Font>(EmojiFontPath);
            if (emojiFont == null) { log.Add("이모지 글꼴 없음: " + EmojiFontPath); return; }
            foreach (var path in BodyFontPaths)
            {
                var ti = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
                if (ti == null) { log.Add("글꼴 없음: " + path); continue; }
                var so = new SerializedObject(ti);
                var names = so.FindProperty("m_FontNames");
                var refs = so.FindProperty("m_FallbackFontReferences");
                if (names == null || refs == null) { log.Add("글꼴: 대체 글꼴 칸 없음 " + path); continue; }
                bool hasName = false, hasRef = false;
                for (int i = 0; i < names.arraySize; i++) hasName |= names.GetArrayElementAtIndex(i).stringValue == emojiFont.name;
                for (int i = 0; i < refs.arraySize; i++) hasRef |= refs.GetArrayElementAtIndex(i).objectReferenceValue == emojiFont;
                if (hasName && hasRef) continue;
                if (!hasName)
                {
                    int at = Mathf.Min(1, names.arraySize);
                    names.InsertArrayElementAtIndex(at);
                    names.GetArrayElementAtIndex(at).stringValue = emojiFont.name;
                }
                if (!hasRef)
                {
                    refs.arraySize++;
                    refs.GetArrayElementAtIndex(refs.arraySize - 1).objectReferenceValue = emojiFont;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                ti.SaveAndReimport();
                log.Add("이모지 대체 글꼴: " + path);
            }
        }
    }
}
