using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메시지 목록 ↔ 대화방을 '메시지 안에서' 옆으로 넘긴다 (2026-10-04 대표님: 대화방에서 나올 때마다 목록이 아래에서
/// 다시 올라와 껐다 켜는 느낌이었다).
///  · 목록 → 대화방: 대화방이 오른쪽에서 밀려 들어오고 목록은 왼쪽으로 살짝 비켜났다가 꺼진다
///  · ← · 뒤로가기: 대화방이 오른쪽으로 빠지고 목록이 제자리로 돌아온 뒤 원래 ← 를 누른다(정리 로직은 그대로)
/// 패널 켜고 끄기·대화 정리는 MessagePanelManager 그대로 — 이 컴포넌트는 그 사이의 움직임만 맡는다.
/// 늘 켜져 있는 오브젝트(메시지 매니저 쪽)에 붙인다.
/// </summary>
public class R0926ChatNav : MonoBehaviour
{
    public static R0926ChatNav Instance { get; private set; }

    [SerializeField] private GameObject messagePanel;
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private RectTransform messageSheet;
    [SerializeField] private RectTransform chatSheet;
    [SerializeField] private R0926SlideIn messageSlide;
    [SerializeField] private R0926SlideIn chatSlide;
    [Tooltip("원래 ← 버튼 — 넘긴 뒤에 대신 누른다")]
    [SerializeField] private Button backButton;
    [SerializeField] private float duration = 0.3f;
    [Tooltip("목록이 비켜나는 정도 (시트 폭 대비)")]
    [SerializeField] private float parallax = 0.28f;

    private Coroutine running;
    private float msgApplied, chatApplied;   // 지금 더해 둔 가로 오프셋 (안전영역·다른 배치와 겹쳐도 정확히 되돌리게 증분으로만)

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>목록이 떠 있을 때 대화방을 열기 직전에 부른다 — 대화방은 아래에서 올라오지 않고 옆에서 들어온다</summary>
    public bool TryPushFromList()
    {
        if (messagePanel == null || !messagePanel.activeInHierarchy || chatSheet == null) return false;
        if (chatSlide != null) chatSlide.SkipNext();
        Run(Push());
        return true;
    }

    /// <summary>← · 뒤로가기 — 목록으로 옆으로 돌아간다</summary>
    public void Back()
    {
        if (running != null) return;
        if (chatPanel == null || !chatPanel.activeInHierarchy || messagePanel == null || chatSheet == null)
        {
            if (backButton != null) backButton.onClick.Invoke();
            return;
        }
        Run(Pop());
    }

    private void Run(IEnumerator r)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(r);
    }

    private IEnumerator Push()
    {
        yield return null;   // 대화방이 켜지고 배치가 잡힌 다음 프레임부터 (폭을 읽는다)
        float w = Width();
        SetChat(w); SetMsg(0f);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float e = Ease(t / duration);
            SetChat(w * (1f - e));
            SetMsg(-w * parallax * e);
            yield return null;
        }
        SetChat(0f);
        SetMsg(0f);
        if (messagePanel != null) messagePanel.SetActive(false);   // 예전처럼 대화방에 있는 동안 목록은 꺼 둔다
        running = null;
    }

    private IEnumerator Pop()
    {
        float w = Width();
        if (messageSlide != null) messageSlide.SkipNext();
        messagePanel.SetActive(true);
        SetMsg(-w * parallax);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float e = Ease(t / duration);
            SetChat(w * e);
            SetMsg(-w * parallax * (1f - e));
            yield return null;
        }
        SetChat(0f);
        SetMsg(0f);
        running = null;
        // 원래 ← — 대화 정리(폴링 멈춤·읽음 시간) 후 목록을 다시 연다. 그때 목록이 꺼졌다 켜지며 아래에서 올라오지 않게 한 번 건너뛴다
        if (messageSlide != null) messageSlide.SkipNext();
        if (backButton != null) backButton.onClick.Invoke();
        if (messageSlide != null) messageSlide.ClearSkip();
    }

    private float Width()
    {
        float w = chatSheet != null ? chatSheet.rect.width : 0f;
        if (w <= 1f && messageSheet != null) w = messageSheet.rect.width;
        return Mathf.Max(w, 400f) + 40f;
    }

    private void SetChat(float x)
    {
        if (chatSheet == null) return;
        chatSheet.anchoredPosition += new Vector2(x - chatApplied, 0f);
        chatApplied = x;
    }

    private void SetMsg(float x)
    {
        if (messageSheet == null) return;
        messageSheet.anchoredPosition += new Vector2(x - msgApplied, 0f);
        msgApplied = x;
    }

    private static float Ease(float p)
    {
        p = Mathf.Clamp01(p);
        return 1f - Mathf.Pow(1f - p, 3f);
    }
}
