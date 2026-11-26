using UnityEngine;

/// <summary>
/// 칸 불투명 — 물방울 모드에서 창 바탕만 비치고 이 아래 칸 · 버튼 · 사진은 제 알파 그대로 (R0926WindowFill 의 「상자 .4」를 안 한다).
/// 11-26 대표님 「'정보수정' 들어갔을 때 … 배경만 투명하게 하고 각 버튼 및 추가로 발생하는 것들은 투명하게 발생하지 않도록」 ·
/// 「분류 선택할 때 '전체' 버튼을 누르면 … 이 때 발생하는 배경은 투명하면 안될 것 같아」.
/// R0926SkinIgnore 와 달리 짙은 회색 → 남색 맞춤 · 창과 같은 바탕(쪽) 지우기는 그대로 한다. 에디터 적용 단계가 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class R0926KeepSolid : MonoBehaviour
{
}
