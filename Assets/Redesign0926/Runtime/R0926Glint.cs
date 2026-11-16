using UnityEngine;

/// <summary>
/// 작은 안내 알약(토스트 · 업로드 안내 · 보물 알림)의 밝은 테두리를 따라 빛이 훑고 지나간다 — 어두운 화면에서도 알약이 묻히지 않게
/// (11-14 대표님 「해당 메세지 테두리에 살짝 외곽선을 밝은색으로 반짝이는 느낌으로」). 부모(Rim0926)의 Mask 안에서만 보여 테두리 선만 반짝인다.
/// 뜰 때 한 번, 떠 있는 동안 every 초마다 다시 — 그사이엔 테두리 밖에서 쉰다.
/// </summary>
public class R0926Glint : MonoBehaviour
{
    [SerializeField] private float delay = 0.08f;   // 떠오르기 시작하고 첫 반짝임까지
    [SerializeField] private float sweep = 0.75f;   // 왼쪽 끝 → 오른쪽 끝
    [SerializeField] private float every = 2.6f;    // 다시 훑기까지

    private RectTransform rt, area;
    private float from;

    private void OnEnable()
    {
        rt = (RectTransform)transform;
        area = rt.parent as RectTransform;
        from = Time.unscaledTime + delay;
        Place(-1f);
    }

    private void Update()
    {
        float e = Time.unscaledTime - from;
        Place(e < 0f ? -1f : e % every / sweep);
    }

    // k: 0 = 왼쪽 밖 → 1 = 오른쪽 밖. 그 밖의 값이면 오른쪽 밖에서 쉰다
    private void Place(float k)
    {
        if (area == null) return;
        float a = rt.localEulerAngles.z * Mathf.Deg2Rad;   // 비스듬한 빛 — 기울어진 만큼 가로로 더 차지한다
        float reach = Mathf.Abs(rt.rect.width * Mathf.Cos(a)) + Mathf.Abs(rt.rect.height * Mathf.Sin(a));
        float half = (area.rect.width + reach) * 0.5f;
        float x = k < 0f || k > 1f ? half : Mathf.Lerp(-half, half, k * k * (3f - 2f * k));
        if (!Mathf.Approximately(rt.anchoredPosition.x, x)) rt.anchoredPosition = new Vector2(x, 0f);
    }
}
