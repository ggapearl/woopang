using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 앱 전체 읽기 쉬움 (2026-10-16 대표님: "다 너무 작은 부분에 있어서는 해당 폰트로는 발생치 않도록 최소한의 사이즈를 정해두어야" ·
    /// "인풋필드가 포커싱 되어서 잘 기입되도록 — inputfield 내에서도 작성되고 있구나 라는 느낌이 들도록").
    ///  · 토스트 — 예전 ToastManager 는 어디에도 붙어 있지 않아 앱의 오류·안내가 하나도 뜨지 않았다 → 화면 위쪽 알약으로 살린다
    ///  · 최소 글자 38(≈ 아이폰 11pt · 안드로이드 10.7dp — 애플·구글 권장 최소). 씬과 앱이 띄우는 줄 프리팹 모두.
    ///    키운 글이 칸보다 높아 통째로 사라지는 것(Truncate)은 넘쳐 보이게 바꾸고 로그에 남긴다 — 캡처로 확인
    ///  · 입력칸 — 커서를 굵고 분홍으로 · 메시지·팔로우 검색칸은 키보드 위 작은 기본 입력줄 없이 칸 안에서 바로 쓰이게
    ///    · 지금 쓰는 칸에 분홍 테두리, 키보드 위 입력줄로 쓰는 칸(장소·보물찾기)은 입력줄 위로 보이게 스크롤 (R0926InputFocus)
    /// 다른 단계가 글자 크기를 다 정한 뒤 맨 끝에 돈다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        public const int MinFont = 38;
        private static readonly string[] MinFontSkip = { "DebugLogCapturePanel" };   // 개발용 창
        // 앱이 띄우는 줄·말풍선 프리팹 (씬에 없는 것) — HeartAnimation 은 하트 그림 글자라 뺀다
        private static readonly string[] MinFontPrefabs =
        {
            "Assets/Prefabs/DM/ConversationItem.prefab", "Assets/Prefabs/DM/AdminNoticeItem.prefab", "Assets/Prefabs/DM/DateSeparator.prefab",
            "Assets/Prefabs/DM/MyMessageBubble.prefab", "Assets/Prefabs/DM/OtherMessageBubble.prefab", "Assets/Prefabs/DM/AdminMessageBubble.prefab",
            "Assets/Prefabs/CommentItem_Template.prefab", "Assets/Prefabs/FollowListItem.prefab",
            "Assets/Resources/Prefabs/Profile/FollowerItem.prefab", "Assets/Resources/Prefabs/Profile/FollowingItem.prefab",
        };
        private static readonly string[] HiddenNativeInput = { "MessagePanel/Clip0926/Background/SearchArea/SearchInput", "FollowPanel/Clip0926/Background/SearchBar/SearchInput" };

        private static void ApplyReadable(Transform root, List<string> log)
        {
            ApplyToast(root, log);

            var notes = new List<string>();
            int raised = RaiseFonts(root, "", notes);
            foreach (var path in MinFontPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add(path + " 없음"); continue; }
                var go = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = RaiseFonts(go.transform, System.IO.Path.GetFileNameWithoutExtension(path) + ":", notes);
                    if (n > 0) PrefabUtility.SaveAsPrefabAsset(go, path);
                    raised += n;
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            log.Add("min font " + MinFont + ": " + raised + "곳 키움");
            foreach (var n in notes) log.Add("  · " + n);

            InputPolish(root, log);
        }

        // ── 최소 글자 ───────────────────────────────────────────
        private static int RaiseFonts(Transform top, string where, List<string> notes)
        {
            int n = 0;
            foreach (var t in top.GetComponentsInChildren<Text>(true))
            {
                if (UnderAny(t.transform, top, MinFontSkip)) continue;
                bool changed = false;
                if (t.fontSize < MinFont) { t.fontSize = MinFont; changed = true; }
                if (t.resizeTextForBestFit && t.resizeTextMinSize < MinFont)
                {
                    t.resizeTextMinSize = MinFont;
                    if (t.resizeTextMaxSize < MinFont) t.resizeTextMaxSize = MinFont;
                    changed = true;
                    notes.Add(where + PathUnder(t.transform, top) + " (맞춤 최소 " + MinFont + ")");
                }
                if (!changed) continue;
                n++;
                // 키운 글이 칸보다 높으면 Truncate 는 줄을 통째로 지운다 — 넘쳐도 보이는 게 낫다
                var rect = t.rectTransform.rect;
                if (!t.resizeTextForBestFit && t.verticalOverflow == VerticalWrapMode.Truncate && rect.height > 0f && t.preferredHeight > rect.height + 1f)
                {
                    t.verticalOverflow = VerticalWrapMode.Overflow;
                    notes.Add(where + PathUnder(t.transform, top) + " (칸 높이 " + Mathf.RoundToInt(rect.height) + " < 글 " + Mathf.RoundToInt(t.preferredHeight) + " → 넘침 허용)");
                }
                else if (!t.resizeTextForBestFit && t.horizontalOverflow == HorizontalWrapMode.Overflow && !string.IsNullOrEmpty(t.text) && rect.width > 0f && t.preferredWidth > rect.width + 1f)
                    notes.Add(where + PathUnder(t.transform, top) + " (칸 폭 " + Mathf.RoundToInt(rect.width) + " < 글 " + Mathf.RoundToInt(t.preferredWidth) + " — 옆으로 넘침, 확인)");
                EditorUtility.SetDirty(t);
            }
            return n;
        }

        private static bool UnderAny(Transform t, Transform top, string[] names)
        {
            for (var p = t; p != null && p != top; p = p.parent)
                foreach (var name in names) if (p.name == name) return true;
            return false;
        }

        private static string PathUnder(Transform t, Transform top)
        {
            var s = t.name;
            for (var p = t.parent; p != null && p != top; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        // ── 입력칸 ─────────────────────────────────────────────
        private static void InputPolish(Transform root, List<string> log)
        {
            int n = 0;
            foreach (var f in root.GetComponentsInChildren<InputField>(true))
            {
                f.caretWidth = 4;
                f.customCaretColor = true;
                f.caretColor = Pink;
                f.selectionColor = new Color(Pink.r, Pink.g, Pink.b, 0.35f);
                EditorUtility.SetDirty(f);
                n++;
            }
            int hidden = 0;
            foreach (var path in HiddenNativeInput)
            {
                var t = root.Find(path);
                var f = t != null ? t.GetComponent<InputField>() : null;
                if (f == null) { log.Add("입력칸 없음: " + path); continue; }
                f.shouldHideMobileInput = true;   // 검색칸은 화면 위쪽 — 키보드에 가리지 않으니 칸 안에서 바로
                EditorUtility.SetDirty(f);
                hidden++;
            }
            log.Add("input polish: 커서 " + n + "칸 · 기본 입력줄 숨김 " + hidden + "칸");

            // 지금 쓰는 칸 테두리 · 키보드 위 입력줄로 쓸 땐 원래 칸이 입력줄 위로 보이게 스크롤
            var fx = Ensure<R0926InputFocus>(root.gameObject);
            var so = new SerializedObject(fx);
            so.FindProperty("ringSprite").objectReferenceValue = Spr("r0926_pill_ring");
            so.FindProperty("pillSprite").objectReferenceValue = Spr("r0926_pill");
            var ms = root.GetComponentsInChildren<UploadInputMirror>(true);
            var arr = so.FindProperty("mirrors");
            arr.arraySize = ms.Length;
            for (int i = 0; i < ms.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = ms[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("input focus ok (입력줄 " + ms.Length + "개)");
        }

        // ── 토스트 — 화면 위쪽, 상태 알약 아래 (보물 알림과 같은 자리) ──
        private static void ApplyToast(Transform root, List<string> log)
        {
            var host = FindOrCreate(root, "Toast0926");
            host.transform.SetAsLastSibling();
            Stretch(RT(host));
            var cv = Ensure<Canvas>(host);
            cv.overrideSorting = true;
            cv.sortingOrder = 9500;   // 창·도크·축하 화면(9000) 위
            var hcg = Ensure<CanvasGroup>(host);
            hcg.blocksRaycasts = false;
            hcg.interactable = false;
            EditorUtility.SetDirty(hcg);

            var stack = FindOrCreate(host.transform, "Stack");
            SetRect(RT(stack), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14 - PillH - 20), new Vector2(1240, 900));
            Ensure<R0926SafeInset>(stack).SetEdge(R0926SafeInset.Edge.Top);
            var vlg = Ensure<VerticalLayoutGroup>(stack);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 16;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            EditorUtility.SetDirty(vlg);

            var tpl = FindOrCreate(stack.transform, "Template");
            Img(tpl, Spr("r0926_pill"), new Color(PillBg.r, PillBg.g, PillBg.b, 0.92f), Image.Type.Sliced, 63f / 56f).raycastTarget = false;
            var hlg = Ensure<HorizontalLayoutGroup>(tpl);
            hlg.padding = new RectOffset(44, 52, 30, 30);
            hlg.spacing = 22;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            EditorUtility.SetDirty(hlg);
            var le = Ensure<LayoutElement>(tpl);
            le.minHeight = 112;
            EditorUtility.SetDirty(le);
            Ensure<CanvasGroup>(tpl);

            var dot = FindOrCreate(tpl.transform, "Dot");
            Img(dot, Spr("r0926_circle"), Soft, Image.Type.Simple).raycastTarget = false;
            var dle = Ensure<LayoutElement>(dot);
            dle.minWidth = 24; dle.preferredWidth = 24; dle.minHeight = 24; dle.preferredHeight = 24;
            EditorUtility.SetDirty(dle);

            var text = FindOrCreate(tpl.transform, "Text");
            var t = Txt(text, "", 42, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            tpl.SetActive(false);

            var tm = Ensure<ToastManager>(host);
            var so = new SerializedObject(tm);
            so.FindProperty("container").objectReferenceValue = RT(stack);
            so.FindProperty("template").objectReferenceValue = tpl;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("toast ok");
        }
    }
}
