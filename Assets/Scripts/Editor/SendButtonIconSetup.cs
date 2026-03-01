using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

/// <summary>
/// SendButton에 WhiteArrow 스프라이트를 자동 설정
/// MessagePanelManager와 CommentManager의 sendButton에 적용
/// </summary>
[InitializeOnLoad]
public class SendButtonIconSetup
{
    private static readonly string ARROW_SPRITE_PATH = "Assets/Pixel Play/Sprites/WhiteArrow.png";

    static SendButtonIconSetup()
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

        // MessagePanelManager의 sendButton 설정
        var msgMgr = Object.FindFirstObjectByType<MessagePanelManager>();
        if (msgMgr != null && msgMgr.sendButton != null)
        {
            if (SetupSendButtonIcon(msgMgr.sendButton, msgMgr))
                changed = true;
        }

        // CommentManager의 sendButton 설정
        var commentMgr = Object.FindFirstObjectByType<CommentManager>();
        if (commentMgr != null && commentMgr.sendButton != null)
        {
            if (SetupSendButtonIcon(commentMgr.sendButton, null))
                changed = true;
        }

        if (changed)
        {
            if (msgMgr != null)
                EditorSceneManager.MarkSceneDirty(msgMgr.gameObject.scene);
        }
    }

    /// <summary>
    /// SendButton에 WhiteArrow 아이콘 설정
    /// </summary>
    private static bool SetupSendButtonIcon(Button sendBtn, MessagePanelManager msgMgr)
    {
        if (sendBtn == null) return false;

        // 이미 sendButtonIconSprite가 설정되어 있으면 그것을 사용
        Sprite arrowSprite = null;
        if (msgMgr != null && msgMgr.sendButtonIconSprite != null)
        {
            arrowSprite = msgMgr.sendButtonIconSprite;
        }
        else
        {
            arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ARROW_SPRITE_PATH);
        }

        if (arrowSprite == null) return false;

        // SendButton의 Image에 스프라이트 설정
        Image btnImage = sendBtn.GetComponent<Image>();
        if (btnImage != null && btnImage.sprite != arrowSprite)
        {
            btnImage.sprite = arrowSprite;
            btnImage.preserveAspect = true;

            // 색상 적용
            if (msgMgr != null)
                btnImage.color = msgMgr.sendButtonIconColor;
            else
                btnImage.color = Color.white;

            EditorUtility.SetDirty(sendBtn.gameObject);

            // MessagePanelManager에 스프라이트 참조도 저장
            if (msgMgr != null && msgMgr.sendButtonIconSprite == null)
            {
                msgMgr.sendButtonIconSprite = arrowSprite;
                EditorUtility.SetDirty(msgMgr);
            }

            return true;
        }

        return false;
    }
}
