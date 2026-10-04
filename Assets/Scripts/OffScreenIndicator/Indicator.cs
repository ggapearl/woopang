using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class Indicator : MonoBehaviour
{
    [SerializeField] private IndicatorType indicatorType;
    private Image indicatorImage;
    private Text distanceText;
    private Text nameText;

    /// <summary>
    /// 이 인디케이터가 표시하는 Target 참조 (터치 콜백용)
    /// </summary>
    [HideInInspector] public Target ownerTarget;

    [Header("Fade In Settings")]
    [Tooltip("화살표 페이드인 시간 (초)")]
    public float arrowFadeDuration = 0.3f;

    private CanvasGroup canvasGroup;

    // 거리에 따른 목표 투명도(1=불투명). OffScreenIndicator 가 화살표일 때 매 프레임 설정한다.
    // 페이드인과 곱해져, 등장은 0→distanceAlpha 로 자연스럽게 이어진다.
    private float distanceAlpha = 1f;
    private Coroutine fadeCoroutine;
    private bool isFirstActivation = true;

    /// <summary>
    /// 마지막으로 DrawIndicators에서 업데이트된 프레임 번호
    /// 2프레임 이상 업데이트 안 되면 orphan으로 판단하여 자동 해제
    /// </summary>
    [HideInInspector] public int lastUpdatedFrame;

    // ── 0926 박스 (R0926IndicatorClose 가 켠다) — 거리는 위·이름은 아래, 장소 박스는 오른쪽 위 꺾쇠 자리에 박스 색 X.
    //    X 를 누르면 그 자리에서 '삭제' 알약으로 펼쳐지고, 3초 안에 한 번 더 누르면 이 기기에서 숨긴다(HiddenPlaces: 3D·박스·화살표).
    //    오브젝트를 두 번 누르려다 X 를 잘못 눌러도 바로 사라지지 않게. 장소가 아닌 대상(P2P 사용자 등)에는 X 가 없다.
    public static bool LabelsSwapped;            // 거리 위 · 이름 아래
    public static bool CloseButtonEnabled;       // 설정 '오브젝트 삭제 기능' — 끄면 X 없이 꺾쇠 넷
    public static Sprite CloseIcon;              // 흰 X (박스 색으로 칠한다)
    public static Sprite CornerCutBox;           // 오른쪽 위 꺾쇠를 뺀 박스
    public static Sprite ClosePill;              // '삭제' 알약 (9-slice)
    public static string CloseConfirmLabel = "삭제";
    private const float CloseConfirmWindow = 3f;
    private const float XMin = 44f, XMax = 88f;  // X 크기 (캔버스 단위 ≈ 12~24pt) — 모서리 꺾쇠 길이의 75%
    private const float HitSize = 160f;          // 누르는 영역 (≈ 44pt)
    private const float PillH = 84f;             // '삭제' 알약 높이 (≈ 23pt)
    private RectTransform closeButton;
    private Target closeFor;
    private bool closePlace, closeOn;
    private Sprite boxSprite;                    // 프리팹의 원래 박스 (꺾쇠 넷)
    private Vector2 namePos, distPos;            // 원래 글자 자리
    private bool labelsSwapped;
    private RectTransform closePill;
    private Image closePillImg, closeXImg;
    private Text closeLabel;
    private float closeArmedAt = -1f, closeK;
    private bool closeRest;
    private CanvasGroup closeGroup;
    private Vector2 closeXCenter;                // 누르는 영역 안에서 X 가운데 (캔버스 단위)

    public bool Active
    {
        get
        {
            return transform.gameObject.activeInHierarchy;
        }
    }

    public IndicatorType Type
    {
        get
        {
            return indicatorType;
        }
    }

    void Awake()
    {
        indicatorImage = transform.GetComponent<Image>();

        // NameText / DistanceText 분리 검색 (BoxIndicator용)
        Transform nameT = transform.Find("NameText");
        Transform distT = transform.Find("DistanceText");
        if (nameT != null) nameText = nameT.GetComponent<Text>();
        if (distT != null) distanceText = distT.GetComponent<Text>();

        // fallback: 기존 "Text" 자식 (ArrowIndicator 등 호환)
        if (distanceText == null)
        {
            distanceText = transform.GetComponentInChildren<Text>();
        }
        boxSprite = indicatorImage.sprite;
        if (nameText != null) namePos = nameText.rectTransform.anchoredPosition;
        if (distanceText != null) distPos = distanceText.rectTransform.anchoredPosition;

        // 터치 가능하도록 Button 추가
        Button btn = GetComponent<Button>();
        if (btn == null)
        {
            btn = gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnIndicatorClicked);

        // CanvasGroup 추가 (페이드인용 + 터치 활성화)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        // 부모 패널의 CanvasGroup(BlocksRaycasts=0)을 무시하여 개별 인디케이터 터치 가능
        canvasGroup.ignoreParentGroups = true;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

    }

    public void SetImageColor(Color color)
    {
        indicatorImage.color = color;
    }

    public void SetDistanceText(float value, Color textColor, string placeName)
    {
        bool hasValidDistance = value >= 0;
        string distanceStr = hasValidDistance ? $"{Mathf.Floor(value)}m" : "";

        if (indicatorType == IndicatorType.BOX && nameText != null)
        {
            // 박스 인디케이터: 이름(위) + 거리(아래) 분리 표시
            nameText.text = string.IsNullOrEmpty(placeName) ? "" : $"<b>{placeName}</b>";
            nameText.color = textColor;

            if (distanceText != null)
            {
                distanceText.text = distanceStr;
                distanceText.color = textColor;
            }
        }
        else if (distanceText != null)
        {
            if (indicatorType == IndicatorType.ARROW)
            {
                // 화살표 인디케이터: 한글 2글자, 영어 1글자로 계산하여 16글자 제한
                int length = 0;
                int charIndex = 0;
                string truncatedName = placeName;

                foreach (char c in placeName)
                {
                    length += (c >= '\uAC00' && c <= '\uD7A3') ? 2 : 1;
                    charIndex++;
                    if (length > 16)
                    {
                        truncatedName = placeName.Substring(0, charIndex - 1) + "..";
                        break;
                    }
                }

                if (string.IsNullOrEmpty(placeName))
                    distanceText.text = distanceStr;
                else if (hasValidDistance)
                    distanceText.text = $"{truncatedName}\n{distanceStr}";
                else
                    distanceText.text = truncatedName;
            }
            else
            {
                // 박스 인디케이터 fallback (nameText가 없는 경우): 기존 방식
                if (string.IsNullOrEmpty(placeName))
                    distanceText.text = hasValidDistance ? $"<b>{distanceStr}</b>" : "";
                else if (hasValidDistance)
                    distanceText.text = $"<b>{placeName}\n{distanceStr}</b>";
                else
                    distanceText.text = $"<b>{placeName}</b>";
            }
            distanceText.color = textColor;
        }
    }

    public void SetTextRotation(Quaternion rotation)
    {
        if (distanceText != null)
        {
            distanceText.rectTransform.rotation = rotation;
        }
        if (nameText != null)
        {
            nameText.rectTransform.rotation = rotation;
        }
    }

    public void Activate(bool value)
    {
        // 이미 같은 상태면 아무 것도 하지 않음 (깜빡임 방지)
        bool wasActive = transform.gameObject.activeInHierarchy;

        transform.gameObject.SetActive(value);

        if (value && isFirstActivation)
        {
            isFirstActivation = false;
            StartFadeIn();
        }
        else if (value && !isFirstActivation && !wasActive)
        {
            // 재활성화 시에는 페이드인 없이 즉시 표시 (거리 기반 투명도 반영)
            if (canvasGroup != null)
            {
                canvasGroup.alpha = distanceAlpha;
            }
        }
        else if (!value)
        {
            // 비활성화될 때는 다음 활성화를 위해 플래그 리셋하지 않음 (깜빡임 방지)
            // isFirstActivation = true; // 제거

            // 페이드인 중이었다면 중단
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            // 알파값은 유지 (다시 활성화될 때 즉시 표시)
        }
    }

    private void StartFadeIn()
    {
        if (canvasGroup == null) return;

        // 기존 페이드인 중단
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // 페이드인 시작
        fadeCoroutine = StartCoroutine(FadeInCoroutine());
    }

    private IEnumerator FadeInCoroutine()
    {
        // 시작 알파값 0
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        float duration = arrowFadeDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 페이드인
            canvasGroup.alpha = Mathf.Lerp(0f, distanceAlpha, t);

            yield return null;
        }

        // 최종 알파값 = 거리 기반 목표
        canvasGroup.alpha = distanceAlpha;
        fadeCoroutine = null;
    }

    private void LateUpdate()
    {
        if (indicatorType != IndicatorType.BOX) return;
        SwapLabels(LabelsSwapped);

        if (ownerTarget != closeFor)
        {
            closeFor = ownerTarget;
            closePlace = ownerTarget != null && HiddenPlaces.ResolveUniqueId(ownerTarget) != null;
            ResetConfirm();   // 다른 장소로 재사용된 박스 — 곧바로 X 로
        }
        bool want = CloseButtonEnabled && closePlace;
        if (want != closeOn)
        {
            closeOn = want;
            if (want) EnsureCloseButton();
            if (closeButton != null) closeButton.gameObject.SetActive(want);
            // X 가 있으면 오른쪽 위 꺾쇠를 뺀 박스, 없으면 원래 박스(꺾쇠 넷)
            if (CornerCutBox != null && boxSprite != null) indicatorImage.sprite = want ? CornerCutBox : boxSprite;
            ResetConfirm();
        }
        if (!closeOn) return;

        float s = transform.localScale.x;
        if (s <= 0.0001f) return;
        // 오른쪽 위 꼭짓점 안쪽 c×c 자리 — X 의 바깥선이 박스 윗변·오른쪽 변과 맞는다. 박스가 커지고 작아져도 늘 그 자리
        float side = ((RectTransform)transform).rect.width * s;
        float c = Mathf.Clamp(side * 0.3f * 0.75f, XMin, XMax);
        // 누르는 영역은 박스 바깥(오른쪽 위)으로 넉넉히, 안쪽은 X 바로 옆까지만 —
        // 가운데에 두면 작은 박스에선 박스 한가운데(오브젝트)까지 덮어, 오브젝트를 누르려다 X 가 눌렸다
        float edge = c + 12f;
        Vector2 hitCenter = new Vector2(HitSize / 2f - edge, HitSize / 2f - edge);   // 꼭짓점 기준
        closeButton.localScale = Vector3.one / s;
        closeButton.anchoredPosition = hitCenter / s;
        closeXCenter = new Vector2(-c / 2f, -c / 2f) - hitCenter;
        closeXImg.rectTransform.anchoredPosition = closeXCenter;
        closeXImg.rectTransform.sizeDelta = new Vector2(c, c);
        var bc = indicatorImage.color;   // 박스 색 그대로 (분류 색)
        closeXImg.color = new Color(bc.r, bc.g, bc.b, bc.a * (1f - Smooth(closeK)));
        AnimateCloseConfirm(c);
    }

    private void SwapLabels(bool on)
    {
        if (on == labelsSwapped || nameText == null || distanceText == null || nameText == distanceText) return;
        labelsSwapped = on;
        nameText.rectTransform.anchoredPosition = on ? distPos : namePos;
        distanceText.rectTransform.anchoredPosition = on ? namePos : distPos;
    }

    private void ResetConfirm()
    {
        closeArmedAt = -1f;
        closeK = 0f;
        closeRest = false;
        if (distanceText != null) distanceText.canvasRenderer.SetAlpha(1f);
    }

    private static float Smooth(float x) => x * x * (3f - 2f * x);

    private void AnimateCloseConfirm(float c)
    {
        if (closePill == null) return;
        if (closeArmedAt >= 0f && Time.unscaledTime - closeArmedAt > CloseConfirmWindow) closeArmedAt = -1f;
        float target = closeArmedAt >= 0f ? 1f : 0f;
        if (closeK == 0f && target == 0f && closeRest) return;
        closeK = Mathf.MoveTowards(closeK, target, Time.unscaledDeltaTime / 0.22f);
        closeRest = closeK == 0f;
        float k = Smooth(closeK);
        // '삭제'일 때는 박스 선 위에, 거리 흐림과 상관없이 또렷하게
        if (closeK > 0f && closeButton.GetSiblingIndex() != closeButton.parent.childCount - 1) closeButton.SetAsLastSibling();
        if (closeGroup != null) closeGroup.ignoreParentGroups = closeK > 0.01f;

        // X 는 돌며 작아지고, 그 자리에서 빨간 알약이 왼쪽으로 펼쳐진다 (오른쪽 끝 = X 오른쪽 끝 조금 바깥)
        closeXImg.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f * k);
        closeXImg.rectTransform.localScale = Vector3.one * (1f - 0.6f * k);
        if (closeLabel != null && closeLabel.text != CloseConfirmLabel) closeLabel.text = CloseConfirmLabel;
        float w = (closeLabel != null ? closeLabel.preferredWidth : 0f) + 72f;
        closePill.sizeDelta = new Vector2(w, PillH);
        closePill.anchoredPosition = closeXCenter + new Vector2(c / 2f + 8f, 0f);
        closePill.localScale = new Vector3(Mathf.Lerp(0.2f, 1f, k), 1f, 1f);
        closePill.gameObject.SetActive(closeK > 0.001f);
        closePillImg.color = new Color(0.91f, 0.263f, 0.353f, 0.97f * Mathf.Clamp01(k * 1.5f));
        if (closeLabel != null) closeLabel.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 1.6f - 0.4f));
        // 그동안 위의 거리 글자는 옅게
        if (distanceText != null) distanceText.canvasRenderer.SetAlpha(1f - 0.78f * k);
    }

    private void EnsureCloseButton()
    {
        if (closeButton != null) return;
        var go = new GameObject("Close0926", typeof(RectTransform), typeof(Image), typeof(Button));
        closeButton = (RectTransform)go.transform;
        closeButton.SetParent(transform, false);
        closeButton.anchorMin = closeButton.anchorMax = new Vector2(1f, 1f);
        closeButton.pivot = new Vector2(0.5f, 0.5f);
        closeButton.sizeDelta = Vector2.one * HitSize;   // 누르는 영역은 보이는 X 보다 넉넉히
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        closeGroup = go.AddComponent<CanvasGroup>();

        var x = new GameObject("X", typeof(RectTransform), typeof(Image));
        ((RectTransform)x.transform).SetParent(closeButton, false);
        closeXImg = x.GetComponent<Image>();
        closeXImg.sprite = CloseIcon;
        closeXImg.preserveAspect = true;
        closeXImg.raycastTarget = false;

        var pill = new GameObject("Confirm", typeof(RectTransform), typeof(Image));
        closePill = (RectTransform)pill.transform;
        closePill.SetParent(closeButton, false);
        closePill.anchorMin = closePill.anchorMax = new Vector2(0.5f, 0.5f);
        closePill.pivot = new Vector2(1f, 0.5f);   // 오른쪽 끝에서 왼쪽으로 펼쳐진다
        closePillImg = pill.GetComponent<Image>();
        if (ClosePill != null)
        {
            closePillImg.sprite = ClosePill;
            closePillImg.type = Image.Type.Sliced;
            closePillImg.pixelsPerUnitMultiplier = ClosePill.border.y / (PillH / 2f);
        }
        closePillImg.raycastTarget = true;   // '삭제' 알약을 눌러도 된다
        pill.SetActive(false);

        Font font = nameText != null ? nameText.font : distanceText != null ? distanceText.font : null;
        if (font != null)
        {
            var lab = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labRt = (RectTransform)lab.transform;
            labRt.SetParent(closePill, false);
            labRt.anchorMin = Vector2.zero; labRt.anchorMax = Vector2.one;
            labRt.offsetMin = Vector2.zero; labRt.offsetMax = new Vector2(0f, 2f);
            closeLabel = lab.GetComponent<Text>();
            closeLabel.font = font;
            closeLabel.fontSize = 46;
            closeLabel.fontStyle = FontStyle.Bold;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            closeLabel.verticalOverflow = VerticalWrapMode.Overflow;
            closeLabel.raycastTarget = false;
            closeLabel.text = CloseConfirmLabel;
        }

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnCloseClicked);
    }

    private void OnCloseClicked()
    {
        if (ownerTarget == null) return;
        if (closeArmedAt < 0f || Time.unscaledTime - closeArmedAt > CloseConfirmWindow)
        {
            closeArmedAt = Time.unscaledTime;   // 첫 번째 — '삭제'로 바뀌기만
            closeRest = false;
            return;
        }
        ResetConfirm();
        HiddenPlaces.HideTarget(ownerTarget);
    }

    /// <summary>
    /// 인디케이터 터치/클릭 시 호출
    /// </summary>
    private void OnIndicatorClicked()
    {
        if (ownerTarget != null && ownerTarget.OnIndicatorTapped != null)
        {
            ownerTarget.OnIndicatorTapped.Invoke();
        }
    }

    public void SetScale(Vector3 scale)
    {
        transform.localScale = scale;
    }

    /// <summary>
    /// 인디케이터 알파값 직접 설정 (전환 애니메이션용)
    /// </summary>
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }

    /// <summary>
    /// 거리 기반 목표 투명도를 설정한다. 페이드인 중이 아니면 즉시 반영하고,
    /// 페이드인 중이면 값만 갱신해 코루틴이 이 값을 향해 진행한다(깜빡임 방지).
    /// </summary>
    public void SetDistanceAlpha(float alpha)
    {
        distanceAlpha = Mathf.Clamp01(alpha);
        if (canvasGroup != null && fadeCoroutine == null)
        {
            canvasGroup.alpha = distanceAlpha;
        }
    }

    /// <summary>
    /// Object Pool로 반환될 때 완전히 리셋
    /// </summary>
    public void ResetForPool()
    {
        isFirstActivation = true;
        ownerTarget = null;

        // 페이드인 중이었다면 중단
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        // 알파값 리셋
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
}

public enum IndicatorType
{
    BOX,
    ARROW
}