using UnityEngine;

/// <summary>
/// 가로 화면에서 쓰기 어려운 화면(장소 추가·수정·상세)에 '세로로 돌려 주세요' 안내를 겹쳐 보여 준다.
/// </summary>
public class R0926PortraitHint : MonoBehaviour
{
    [SerializeField] private GameObject hint;

    private void LateUpdate()
    {
        if (hint == null) return;
        bool show = R0926Orientation.IsLandscape;
        if (hint.activeSelf != show) hint.SetActive(show);
    }

    private void OnDisable()
    {
        if (hint != null) hint.SetActive(false);
    }
}
