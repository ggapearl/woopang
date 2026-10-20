using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가운데 '처리 중' 카드 — 도는 분홍 원호 + 한 줄 안내 (+ 올리기는 진행 막대). 뒤를 어둡게 덮어 그동안 다른 곳이 안 눌린다.
/// 10-20 대표님 「메인사진 올릴 때 '업로드 중..' '크롭 중..' 같은 안내나 로딩스피너」 — 사진 자르기 · 장소 올리기 · 장소 수정 보내기.
/// 자기 캔버스로 자르기 화면(30000)보다 위 (Redesign0926ApplyCrop 이 만들어 꺼 둔다)
/// </summary>
public class R0926Busy : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Text label;
    [SerializeField] private RectTransform spin;
    [SerializeField] private GameObject bar;           // 진행 막대 (올리기만)
    [SerializeField] private RectTransform barFill;

    private const float FadeOut = 0.15f;
    private const float Stale = 20f;   // 부른 쪽이 멈춰(예외·코루틴 정지) 더 알리지 않으면 저절로 걷힌다 — 화면 전체를 막는 카드라 남으면 앱을 못 쓴다
    private float hideAt = -1f, shownAt;

    public bool Showing => gameObject.activeSelf && hideAt < 0f;

    /// <param name="progress">0~1 이면 막대에 채운다. 음수면 막대 없이</param>
    public void Show(string text, float progress = -1f)
    {
        hideAt = -1f;
        shownAt = Time.unscaledTime;   // 올리는 동안은 1초마다 다시 부른다 (CubeUploadManager.SendRequestWithTimeout)
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (group != null) { group.alpha = 1f; group.blocksRaycasts = true; }
        if (label != null) label.text = text;
        bool on = progress >= 0f;
        if (bar != null && bar.activeSelf != on) bar.SetActive(on);
        if (on && barFill != null) barFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf || hideAt >= 0f) return;
        hideAt = Time.unscaledTime;
        if (group != null) group.blocksRaycasts = false;   // 사라지는 동안에도 아래가 바로 눌리게
    }

    private void LateUpdate()
    {
        if (spin != null) spin.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 400f);   // 사진 칸 원호와 같은 빠르기
        if (hideAt < 0f && Time.unscaledTime - shownAt > Stale) Hide();
        if (hideAt < 0f) return;
        float k = (Time.unscaledTime - hideAt) / FadeOut;
        if (group != null) group.alpha = 1f - Mathf.Clamp01(k);
        if (k >= 1f) { hideAt = -1f; gameObject.SetActive(false); }
    }
}
