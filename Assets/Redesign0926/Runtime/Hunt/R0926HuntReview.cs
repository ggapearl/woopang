using UnityEngine;

/// <summary>
/// 보물 숨기기 확인 카드 (캔버스 맨 위 층 · R0926HuntCard 가 채운다) — 보물찾기 카드가 꺼지면(추가 창을 닫거나 다른 칸으로)
/// 같이 닫힌다. 카드가 꺼지는 중엔 다른 오브젝트를 끄지 않으므로 여기서 다음 프레임에 스스로 끈다.
/// </summary>
public class R0926HuntReview : MonoBehaviour
{
    private void LateUpdate()
    {
        if (!R0926HuntCard.Reviewing) gameObject.SetActive(false);
    }
}
