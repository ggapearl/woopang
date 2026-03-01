using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// InputField 확장 — lockFocus가 true일 때 OnDeselect를 차단하여
/// 외부 터치(ScrollRect 스크롤 등) 시에도 포커스와 키보드가 유지되도록 함.
///
/// 문제 상황 (일반 InputField):
///   1. 사용자가 대화 내용 스크롤을 위해 ScrollRect 영역 터치
///   2. EventSystem → InputField.OnDeselect() → DeactivateInputField()
///   3. 네이티브 키보드가 닫힘 → 레이아웃 축소 → 깜빡임/스크롤 불가
///
/// 해결 (이 컴포넌트):
///   1. 사용자가 ScrollRect 영역 터치
///   2. EventSystem → OnDeselect() → lockFocus로 차단 → 아무 일 없음
///   3. 키보드 유지, 레이아웃 유지, 스크롤 정상 작동
///
/// ScrollRect 드래그는 포인터 이벤트(IBeginDragHandler/IDragHandler)로 동작하므로
/// selection 변경과 독립적 → OnDeselect를 차단해도 스크롤에 영향 없음.
///
/// MobileKeyboardHandler가 자동으로 lockFocus를 관리:
///   - 키보드 활성화 시: lockFocus = true
///   - native dismiss 시: lockFocus = false
///   - 패널 닫힘(OnDisable) 시: lockFocus = false
/// </summary>
public class KeyboardPersistentInputField : InputField
{
    /// <summary>
    /// true이면 OnDeselect를 무시하여 포커스/키보드 유지.
    /// MobileKeyboardHandler가 관리함.
    /// </summary>
    [HideInInspector]
    public bool lockFocus;

    public override void OnDeselect(BaseEventData eventData)
    {
        if (lockFocus) return;
        base.OnDeselect(eventData);
    }
}
