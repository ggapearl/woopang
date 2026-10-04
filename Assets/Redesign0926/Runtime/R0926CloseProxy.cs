using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 닫기 버튼 위에 투명하게 덮는 누름 영역. 누르면(뒤로가기·바깥 누르기 포함) 시트가 아래로 미끄러져 내려간 뒤
/// 원래 버튼을 눌러 준다 — 닫는 로직은 원래 버튼(에디터 연결 + 코드 AddListener) 것을 그대로 쓴다.
/// 원래 버튼을 바로 누르면 창이 뚝 끊기듯 사라졌다.
/// </summary>
public class R0926CloseProxy : MonoBehaviour
{
    [SerializeField] private R0926Closer sheet;   // 아래로 미끄러지는 시트 또는 옅어지는 창
    [SerializeField] private Button real;

    public void Run()
    {
        if (real == null) return;
        if (sheet != null && sheet.isActiveAndEnabled) sheet.AnimateClose(real);
        else real.onClick.Invoke();
    }
}
