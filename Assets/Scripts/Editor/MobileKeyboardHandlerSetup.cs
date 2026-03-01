using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// MobileKeyboardHandler를 ChatRoomPanel과 CommentPanel에 자동 추가/연결
/// + InputField → KeyboardPersistentInputField 자동 교체 (OnDeselect 차단용)
/// 씬 로드 시 자동 실행
/// </summary>
[InitializeOnLoad]
public class MobileKeyboardHandlerSetup
{
    static MobileKeyboardHandlerSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += CheckAndSetup;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += CheckAndSetupSilent;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += CheckAndSetupSilent;
    }

    private static void CheckAndSetup()
    {
        CheckAndSetupSilent();
    }

    private static void CheckAndSetupSilent()
    {
        bool changed = false;

        // ============================================================
        // ChatRoomPanel (MessagePanelManager)
        // ============================================================
        var msgManagers = Object.FindObjectsByType<MessagePanelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var msgMgr in msgManagers)
        {
            if (msgMgr != null && msgMgr.chatRoomPanel != null)
            {
                if (SetupChatRoomHandler(msgMgr))
                    changed = true;
            }
        }

        // ============================================================
        // CommentPanel (CommentManager)
        // ============================================================
        var commentManagers = Object.FindObjectsByType<CommentManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var commentMgr in commentManagers)
        {
            if (commentMgr != null && commentMgr.commentPanel != null)
            {
                if (SetupCommentHandler(commentMgr))
                    changed = true;
            }
        }

        if (changed)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    // ============================================================
    // InputField → KeyboardPersistentInputField 교체 (수동 프로퍼티 복사)
    // ============================================================

    /// <summary>
    /// 일반 InputField를 KeyboardPersistentInputField로 교체.
    /// 수동으로 모든 프로퍼티를 복사하여 m_Script 덮어쓰기 문제를 방지.
    /// 이미 KeyboardPersistentInputField이면 상태 검증 후 반환.
    /// </summary>
    private static InputField SwapToKeyboardPersistent(InputField existingInput)
    {
        if (existingInput == null) return null;

        // 이미 KPI인 경우 → 검증 + 복구만 수행
        if (existingInput is KeyboardPersistentInputField existingKPI)
        {
            RepairIfBroken(existingKPI);
            return existingKPI;
        }

        GameObject go = existingInput.gameObject;

        try
        {
            // ---- InputField 프로퍼티 저장 ----
            var textComponent = existingInput.textComponent;
            var placeholder = existingInput.placeholder;
            var text = existingInput.text;
            var characterLimit = existingInput.characterLimit;
            var contentType = existingInput.contentType;
            var lineType = existingInput.lineType;
            var inputType = existingInput.inputType;
            var keyboardType = existingInput.keyboardType;
            var characterValidation = existingInput.characterValidation;
            var caretBlinkRate = existingInput.caretBlinkRate;
            var caretWidth = existingInput.caretWidth;
            var caretColor = existingInput.caretColor;
            var customCaretColor = existingInput.customCaretColor;
            var selectionColor = existingInput.selectionColor;
            var readOnly = existingInput.readOnly;
            var shouldHideMobileInput = existingInput.shouldHideMobileInput;
            var shouldActivateOnSelect = existingInput.shouldActivateOnSelect;

            // ---- Selectable 프로퍼티 저장 ----
            var interactable = existingInput.interactable;
            var transition = existingInput.transition;
            var colors = existingInput.colors;
            var targetGraphic = existingInput.targetGraphic;
            var navigation = existingInput.navigation;
            var spriteState = existingInput.spriteState;
            var animationTriggers = existingInput.animationTriggers;

            // ---- 기존 InputField 제거 ----
            Object.DestroyImmediate(existingInput);

            // ---- KeyboardPersistentInputField 추가 ----
            var newInput = go.AddComponent<KeyboardPersistentInputField>();

            // ---- InputField 프로퍼티 복원 ----
            newInput.textComponent = textComponent;
            newInput.placeholder = placeholder;
            newInput.characterLimit = characterLimit;
            newInput.contentType = contentType;
            newInput.lineType = lineType;
            newInput.inputType = inputType;
            newInput.keyboardType = keyboardType;
            newInput.characterValidation = characterValidation;
            newInput.caretBlinkRate = caretBlinkRate;
            newInput.caretWidth = caretWidth;
            newInput.caretColor = caretColor;
            newInput.customCaretColor = customCaretColor;
            newInput.selectionColor = selectionColor;
            newInput.readOnly = readOnly;
            newInput.shouldHideMobileInput = shouldHideMobileInput;
            newInput.shouldActivateOnSelect = shouldActivateOnSelect;
            newInput.text = text; // text는 다른 프로퍼티 설정 후 마지막에

            // ---- Selectable 프로퍼티 복원 ----
            newInput.interactable = interactable;
            newInput.transition = transition;
            newInput.colors = colors;
            newInput.targetGraphic = targetGraphic;
            newInput.navigation = navigation;
            newInput.spriteState = spriteState;
            newInput.animationTriggers = animationTriggers;

            EditorUtility.SetDirty(go);
            Debug.Log($"[MKH Setup] InputField → KPI 교체 완료: {go.name} " +
                      $"textComp={textComponent != null} graphic={targetGraphic != null} interactable={interactable}");

            return newInput;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MKH Setup] InputField 교체 실패: {e.Message}");

            var fallback = go.GetComponent<InputField>();
            if (fallback == null)
            {
                fallback = go.AddComponent<KeyboardPersistentInputField>();
                Debug.LogWarning("[MKH Setup] Fallback KPI 추가됨");
            }
            RepairIfBroken(fallback as KeyboardPersistentInputField);
            return fallback;
        }
    }

    /// <summary>
    /// 이전 EditorJsonUtility 스왑으로 깨진 KPI를 자동 복구.
    /// textComponent, targetGraphic 등 필수 참조를 계층 구조에서 재탐색.
    /// </summary>
    private static void RepairIfBroken(KeyboardPersistentInputField kpi)
    {
        if (kpi == null) return;

        bool repaired = false;

        // textComponent 복구
        if (kpi.textComponent == null)
        {
            var texts = kpi.GetComponentsInChildren<Text>(true);
            foreach (var t in texts)
            {
                // placeholder가 아닌 Text 자식을 textComponent로 사용
                if (kpi.placeholder != null && t.gameObject == kpi.placeholder.gameObject) continue;
                if (t.gameObject == kpi.gameObject) continue;
                kpi.textComponent = t;
                repaired = true;
                Debug.Log($"[MKH Setup] textComponent 복구: {t.gameObject.name}");
                break;
            }
        }

        // placeholder 복구
        if (kpi.placeholder == null)
        {
            Transform placeholderTr = kpi.transform.Find("Placeholder");
            if (placeholderTr == null) placeholderTr = kpi.transform.Find("placeholder");
            if (placeholderTr != null)
            {
                var placeholderGraphic = placeholderTr.GetComponent<Graphic>();
                if (placeholderGraphic != null)
                {
                    kpi.placeholder = placeholderGraphic;
                    repaired = true;
                    Debug.Log($"[MKH Setup] placeholder 복구: {placeholderTr.name}");
                }
            }
        }

        // targetGraphic 복구 (레이캐스트 수신에 필수)
        if (kpi.targetGraphic == null)
        {
            var graphic = kpi.GetComponent<Graphic>();
            if (graphic != null)
            {
                kpi.targetGraphic = graphic;
                repaired = true;
                Debug.Log($"[MKH Setup] targetGraphic 복구: {graphic.GetType().Name}");
            }
        }

        // interactable 확인 (false면 터치 이벤트를 무시)
        if (!kpi.interactable)
        {
            kpi.interactable = true;
            repaired = true;
            Debug.Log("[MKH Setup] interactable 복구: true");
        }

        if (repaired)
        {
            EditorUtility.SetDirty(kpi);
            EditorUtility.SetDirty(kpi.gameObject);
            Debug.Log($"[MKH Setup] KPI 복구 완료: {kpi.gameObject.name}");
        }
    }

    /// <summary>
    /// ChatRoomPanel에 MobileKeyboardHandler 설정
    /// </summary>
    private static bool SetupChatRoomHandler(MessagePanelManager mgr)
    {
        GameObject chatRoomPanel = mgr.chatRoomPanel;

        MobileKeyboardHandler handler = chatRoomPanel.GetComponent<MobileKeyboardHandler>();
        bool isNew = (handler == null);
        if (isNew)
            handler = chatRoomPanel.AddComponent<MobileKeyboardHandler>();

        bool changed = isNew;

        // Background 연결
        Transform bgTransform = chatRoomPanel.transform.Find("Background");
        if (bgTransform != null && handler.backgroundRect == null)
        {
            handler.backgroundRect = bgTransform.GetComponent<RectTransform>();
            changed = true;
        }

        // InputArea 연결
        if (handler.inputAreaRect == null)
        {
            if (mgr.chatInputArea != null)
            {
                handler.inputAreaRect = mgr.chatInputArea.GetComponent<RectTransform>();
                changed = true;
            }
            else if (bgTransform != null)
            {
                Transform inputAreaTr = bgTransform.Find("InputArea");
                if (inputAreaTr != null)
                {
                    handler.inputAreaRect = inputAreaTr.GetComponent<RectTransform>();
                    changed = true;
                }
            }
        }

        // ScrollRect + ScrollView RectTransform 연결
        if (handler.chatScrollRect == null)
        {
            ScrollRect sr = null;

            // 1순위: chatMessageContent에서 부모 ScrollRect 찾기
            if (mgr.chatMessageContent != null)
                sr = mgr.chatMessageContent.GetComponentInParent<ScrollRect>();

            // 2순위: Background 하위에서 ScrollRect 찾기
            if (sr == null && bgTransform != null)
                sr = bgTransform.GetComponentInChildren<ScrollRect>(true);

            // 3순위: chatRoomPanel 하위 전체에서 찾기
            if (sr == null)
                sr = chatRoomPanel.GetComponentInChildren<ScrollRect>(true);

            if (sr != null)
            {
                handler.chatScrollRect = sr;
                handler.scrollViewRect = sr.GetComponent<RectTransform>();
                changed = true;

                // Viewport 자동 연결
                if (handler.viewportRect == null && sr.viewport != null)
                {
                    handler.viewportRect = sr.viewport;
                    changed = true;
                }
            }
        }

        // ★ InputField → KeyboardPersistentInputField 교체 + 기존 KPI 검증/복구
        if (mgr.chatInput != null)
        {
            var result = SwapToKeyboardPersistent(mgr.chatInput);
            if (result != null && result != mgr.chatInput)
            {
                mgr.chatInput = result;
                handler.targetInputField = result;
                EditorUtility.SetDirty(mgr);
                changed = true;
            }
        }

        // targetInputField 연결
        if (handler.targetInputField == null && mgr.chatInput != null)
        {
            handler.targetInputField = mgr.chatInput;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(handler);
            EditorUtility.SetDirty(chatRoomPanel);
        }

        return changed;
    }

    /// <summary>
    /// CommentPanel에 MobileKeyboardHandler 설정
    /// </summary>
    private static bool SetupCommentHandler(CommentManager mgr)
    {
        GameObject commentPanel = mgr.commentPanel;

        MobileKeyboardHandler handler = commentPanel.GetComponent<MobileKeyboardHandler>();
        bool isNew = (handler == null);
        if (isNew)
            handler = commentPanel.AddComponent<MobileKeyboardHandler>();

        bool changed = isNew;

        // Background 연결 (commentPanel 자체 또는 자식에서 찾기)
        if (handler.backgroundRect == null)
        {
            // CommentPanel의 panelRect을 background로 사용
            if (mgr.panelRect != null)
            {
                handler.backgroundRect = mgr.panelRect;
                changed = true;
            }
        }

        // InputArea 연결
        if (handler.inputAreaRect == null)
        {
            Transform inputAreaTr = commentPanel.transform.Find("InputArea");
            if (inputAreaTr == null && mgr.commentInputField != null)
                inputAreaTr = mgr.commentInputField.transform.parent;

            if (inputAreaTr != null)
            {
                handler.inputAreaRect = inputAreaTr.GetComponent<RectTransform>();
                changed = true;
            }
        }

        // ScrollRect + ScrollView RectTransform 연결
        if (handler.chatScrollRect == null)
        {
            ScrollRect sr = null;

            // 1순위: commentContent에서 부모 ScrollRect 찾기
            if (mgr.commentContent != null)
                sr = mgr.commentContent.GetComponentInParent<ScrollRect>();

            // 2순위: commentPanel 하위에서 ScrollRect 찾기
            if (sr == null)
                sr = commentPanel.GetComponentInChildren<ScrollRect>(true);

            if (sr != null)
            {
                handler.chatScrollRect = sr;
                handler.scrollViewRect = sr.GetComponent<RectTransform>();
                changed = true;

                // Viewport 자동 연결
                if (handler.viewportRect == null && sr.viewport != null)
                {
                    handler.viewportRect = sr.viewport;
                    changed = true;
                }
            }
        }

        // ★ InputField → KeyboardPersistentInputField 교체 + 기존 KPI 검증/복구
        if (mgr.commentInputField != null)
        {
            var result = SwapToKeyboardPersistent(mgr.commentInputField);
            if (result != null && result != mgr.commentInputField)
            {
                mgr.commentInputField = result;
                handler.targetInputField = result;
                EditorUtility.SetDirty(mgr);
                changed = true;
            }
        }

        // targetInputField 연결
        if (handler.targetInputField == null && mgr.commentInputField != null)
        {
            handler.targetInputField = mgr.commentInputField;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(handler);
            EditorUtility.SetDirty(commentPanel);
        }

        return changed;
    }
}
