using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다른 버튼을 대신 눌러 준다 — 코드에서 AddListener 로 붙은 동작까지 그대로 실행된다.
/// (예: '계정 삭제' → 기존 '프로필 편집' 버튼. 삭제는 편집 웹페이지 안에 있다)
/// </summary>
public class R0926PressButton : MonoBehaviour
{
    [SerializeField] private Button target;

    public void Press()
    {
        if (target != null && target.interactable) target.onClick.Invoke();
    }
}
