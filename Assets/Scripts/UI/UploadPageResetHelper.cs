using UnityEngine;
using System.Collections;

/// <summary>
/// UploadPage 오브젝트에 부착하여 활성화 시 SwipePanelController를 첫 패널(CubeUploadPage)로 리셋
/// PlusButton이 GameObject.SetActive(true)를 직접 호출하므로 OnEnable에서 처리
/// </summary>
public class UploadPageResetHelper : MonoBehaviour
{
    private SwipePanelController swipePanelController;

    void OnEnable()
    {
        // 즉시 리셋 시도
        FindAndReset();

        // 1프레임 후 레이아웃 갱신 반영하여 다시 리셋
        StartCoroutine(DelayedReset());
    }

    private void FindAndReset()
    {
        if (swipePanelController == null)
            swipePanelController = FindFirstObjectByType<SwipePanelController>();

        if (swipePanelController != null)
            swipePanelController.ResetToFirstPanel();
    }

    private IEnumerator DelayedReset()
    {
        yield return null;
        FindAndReset();
    }
}
