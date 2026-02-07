using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 에디터용 가상 키보드
/// InputField에 포커스되면 화면 하단에 가상 키보드 표시
/// 모바일 디바이스에서는 자동으로 비활성화됨
/// </summary>
public class EditorVirtualKeyboard : MonoBehaviour
{
    public static EditorVirtualKeyboard Instance { get; private set; }

    [Header("키보드 설정")]
    [SerializeField] private float keyboardHeight = 300f;
    [SerializeField] private Color backgroundColor = new Color(0.15f, 0.15f, 0.18f, 0.98f);
    [SerializeField] private Color keyColor = new Color(0.25f, 0.25f, 0.28f, 1f);
    [SerializeField] private Color keyTextColor = Color.white;
    [SerializeField] private Color specialKeyColor = new Color(0.35f, 0.35f, 0.38f, 1f);

    [Header("폰트 설정")]
    [SerializeField] private Font keyFont;
    [SerializeField] private int keyFontSize = 24;

    private GameObject keyboardPanel;
    private InputField currentInputField;
    private bool isShiftActive = false;
    private bool isKoreanMode = true;
    private Canvas parentCanvas;

    // 한글 자모
    private static readonly string[] koreanConsonants = { "ㅂ", "ㅈ", "ㄷ", "ㄱ", "ㅅ", "ㅛ", "ㅕ", "ㅑ", "ㅐ", "ㅔ",
                                                           "ㅁ", "ㄴ", "ㅇ", "ㄹ", "ㅎ", "ㅗ", "ㅓ", "ㅏ", "ㅣ",
                                                           "ㅋ", "ㅌ", "ㅊ", "ㅍ", "ㅠ", "ㅜ", "ㅡ" };
    private static readonly string[] koreanShiftConsonants = { "ㅃ", "ㅉ", "ㄸ", "ㄲ", "ㅆ", "ㅛ", "ㅕ", "ㅑ", "ㅒ", "ㅖ",
                                                                "ㅁ", "ㄴ", "ㅇ", "ㄹ", "ㅎ", "ㅗ", "ㅓ", "ㅏ", "ㅣ",
                                                                "ㅋ", "ㅌ", "ㅊ", "ㅍ", "ㅠ", "ㅜ", "ㅡ" };

    // QWERTY 레이아웃
    private static readonly string[] qwertyRow1 = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
    private static readonly string[] qwertyRow2 = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
    private static readonly string[] qwertyRow3 = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
    private static readonly string[] qwertyRow4 = { "Z", "X", "C", "V", "B", "N", "M" };

    // 한글 레이아웃 (두벌식)
    private static readonly string[] hangulRow1 = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
    private static readonly string[] hangulRow2 = { "ㅂ", "ㅈ", "ㄷ", "ㄱ", "ㅅ", "ㅛ", "ㅕ", "ㅑ", "ㅐ", "ㅔ" };
    private static readonly string[] hangulRow3 = { "ㅁ", "ㄴ", "ㅇ", "ㄹ", "ㅎ", "ㅗ", "ㅓ", "ㅏ", "ㅣ" };
    private static readonly string[] hangulRow4 = { "ㅋ", "ㅌ", "ㅊ", "ㅍ", "ㅠ", "ㅜ", "ㅡ" };

    // 한글 쌍자음
    private static readonly string[] hangulShiftRow2 = { "ㅃ", "ㅉ", "ㄸ", "ㄲ", "ㅆ", "ㅛ", "ㅕ", "ㅑ", "ㅒ", "ㅖ" };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (!Application.isEditor)
        {
            enabled = false;
            return;
        }

        // 폰트 로드
        if (keyFont == null)
        {
            keyFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
            if (keyFont == null)
                keyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // 캔버스 찾기
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            parentCanvas = FindFirstObjectByType<Canvas>();
    }

    void Update()
    {
#if UNITY_EDITOR
        // 현재 선택된 오브젝트가 InputField인지 확인
        GameObject selected = EventSystem.current?.currentSelectedGameObject;
        if (selected != null)
        {
            InputField inputField = selected.GetComponent<InputField>();
            if (inputField != null && inputField != currentInputField)
            {
                ShowKeyboard(inputField);
            }
        }
        else if (currentInputField != null && keyboardPanel != null)
        {
            // 포커스 해제되면 키보드 숨김 (키보드 자체 클릭 제외)
            if (selected == null || !IsKeyboardElement(selected))
            {
                // 딜레이를 두고 숨김 (키 클릭 후 포커스 복원 시간 확보)
            }
        }
#endif
    }

    private bool IsKeyboardElement(GameObject obj)
    {
        if (keyboardPanel == null) return false;
        return obj.transform.IsChildOf(keyboardPanel.transform) || obj == keyboardPanel;
    }

    /// <summary>
    /// 가상 키보드 표시
    /// </summary>
    public void ShowKeyboard(InputField inputField)
    {
        currentInputField = inputField;

        if (keyboardPanel == null)
        {
            CreateKeyboardPanel();
        }

        keyboardPanel.SetActive(true);
        UpdateKeyLabels();
    }

    /// <summary>
    /// 가상 키보드 숨김
    /// </summary>
    public void HideKeyboard()
    {
        if (keyboardPanel != null)
            keyboardPanel.SetActive(false);

        currentInputField = null;
    }

    private void CreateKeyboardPanel()
    {
        if (parentCanvas == null) return;

        // 키보드 패널 생성
        keyboardPanel = new GameObject("EditorVirtualKeyboard");
        keyboardPanel.transform.SetParent(parentCanvas.transform, false);

        RectTransform panelRect = keyboardPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 0);
        panelRect.pivot = new Vector2(0.5f, 0);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(0, keyboardHeight);

        // 배경
        Image bg = keyboardPanel.AddComponent<Image>();
        bg.color = backgroundColor;

        // 레이아웃
        VerticalLayoutGroup vlg = keyboardPanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;

        // 숫자 행
        CreateKeyRow(keyboardPanel.transform, qwertyRow1);

        // 문자 행들
        if (isKoreanMode)
        {
            CreateKeyRow(keyboardPanel.transform, hangulRow2, "row2");
            CreateKeyRow(keyboardPanel.transform, hangulRow3, "row3");
            CreateKeyRowWithSpecials(keyboardPanel.transform, hangulRow4, "row4");
        }
        else
        {
            CreateKeyRow(keyboardPanel.transform, qwertyRow2, "row2");
            CreateKeyRow(keyboardPanel.transform, qwertyRow3, "row3");
            CreateKeyRowWithSpecials(keyboardPanel.transform, qwertyRow4, "row4");
        }

        // 스페이스바 행
        CreateSpaceBarRow(keyboardPanel.transform);

        // 맨 앞으로
        keyboardPanel.transform.SetAsLastSibling();
    }

    private void CreateKeyRow(Transform parent, string[] keys, string rowName = "")
    {
        GameObject row = new GameObject(string.IsNullOrEmpty(rowName) ? "KeyRow" : rowName);
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 50);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        foreach (string key in keys)
        {
            CreateKey(row.transform, key);
        }
    }

    private void CreateKeyRowWithSpecials(Transform parent, string[] keys, string rowName)
    {
        GameObject row = new GameObject(rowName);
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 50);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // Shift 키
        CreateSpecialKey(row.transform, "⇧", OnShiftPressed, 1.5f);

        foreach (string key in keys)
        {
            CreateKey(row.transform, key);
        }

        // Backspace 키
        CreateSpecialKey(row.transform, "⌫", OnBackspacePressed, 1.5f);
    }

    private void CreateSpaceBarRow(Transform parent)
    {
        GameObject row = new GameObject("SpaceBarRow");
        row.transform.SetParent(parent, false);

        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 50);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // 한/영 전환
        CreateSpecialKey(row.transform, isKoreanMode ? "한" : "EN", OnLanguageToggle, 1.2f);

        // 스페이스바
        CreateSpecialKey(row.transform, "Space", OnSpacePressed, 5f);

        // 완료 버튼
        CreateSpecialKey(row.transform, "완료", OnDonePressed, 1.2f);
    }

    private void CreateKey(Transform parent, string keyLabel)
    {
        GameObject keyObj = new GameObject($"Key_{keyLabel}");
        keyObj.transform.SetParent(parent, false);

        RectTransform keyRect = keyObj.AddComponent<RectTransform>();

        Image keyBg = keyObj.AddComponent<Image>();
        keyBg.color = keyColor;

        Button btn = keyObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.45f);
        colors.pressedColor = new Color(0.5f, 0.5f, 0.55f);
        btn.colors = colors;

        string capturedKey = keyLabel;
        btn.onClick.AddListener(() => OnKeyPressed(capturedKey));

        // 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(keyObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = keyLabel;
        text.font = keyFont;
        text.fontSize = keyFontSize;
        text.color = keyTextColor;
        text.alignment = TextAnchor.MiddleCenter;
    }

    private void CreateSpecialKey(Transform parent, string label, Action onClick, float widthMultiplier = 1f)
    {
        GameObject keyObj = new GameObject($"Key_{label}");
        keyObj.transform.SetParent(parent, false);

        RectTransform keyRect = keyObj.AddComponent<RectTransform>();

        LayoutElement le = keyObj.AddComponent<LayoutElement>();
        le.flexibleWidth = widthMultiplier;

        Image keyBg = keyObj.AddComponent<Image>();
        keyBg.color = specialKeyColor;

        Button btn = keyObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.45f, 0.45f, 0.5f);
        colors.pressedColor = new Color(0.55f, 0.55f, 0.6f);
        btn.colors = colors;

        btn.onClick.AddListener(() => onClick?.Invoke());

        // 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(keyObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.font = keyFont;
        text.fontSize = keyFontSize - 2;
        text.color = keyTextColor;
        text.alignment = TextAnchor.MiddleCenter;
    }

    private void OnKeyPressed(string key)
    {
        if (currentInputField == null) return;

        // Shift 적용
        string actualKey = key;
        if (isShiftActive && isKoreanMode)
        {
            int idx = System.Array.IndexOf(hangulRow2, key);
            if (idx >= 0 && idx < hangulShiftRow2.Length)
            {
                actualKey = hangulShiftRow2[idx];
            }
        }
        else if (isShiftActive && !isKoreanMode)
        {
            actualKey = key.ToUpper();
        }
        else if (!isShiftActive && !isKoreanMode)
        {
            actualKey = key.ToLower();
        }

        currentInputField.text += actualKey;
        currentInputField.caretPosition = currentInputField.text.Length;

        // Shift 해제
        if (isShiftActive)
        {
            isShiftActive = false;
            UpdateKeyLabels();
        }

        // 포커스 유지
        currentInputField.ActivateInputField();
    }

    private void OnShiftPressed()
    {
        isShiftActive = !isShiftActive;
        UpdateKeyLabels();
    }

    private void OnBackspacePressed()
    {
        if (currentInputField == null || string.IsNullOrEmpty(currentInputField.text)) return;

        currentInputField.text = currentInputField.text.Substring(0, currentInputField.text.Length - 1);
        currentInputField.caretPosition = currentInputField.text.Length;
        currentInputField.ActivateInputField();
    }

    private void OnSpacePressed()
    {
        if (currentInputField == null) return;

        currentInputField.text += " ";
        currentInputField.caretPosition = currentInputField.text.Length;
        currentInputField.ActivateInputField();
    }

    private void OnLanguageToggle()
    {
        isKoreanMode = !isKoreanMode;

        // 키보드 재생성
        if (keyboardPanel != null)
        {
            Destroy(keyboardPanel);
            keyboardPanel = null;
        }
        CreateKeyboardPanel();
    }

    private void OnDonePressed()
    {
        HideKeyboard();
    }

    private void UpdateKeyLabels()
    {
        if (keyboardPanel == null) return;

        // row2 업데이트
        Transform row2 = keyboardPanel.transform.Find("row2");
        if (row2 != null)
        {
            string[] labels = isKoreanMode ? (isShiftActive ? hangulShiftRow2 : hangulRow2) : qwertyRow2;
            UpdateRowLabels(row2, labels);
        }

        // row3 업데이트
        Transform row3 = keyboardPanel.transform.Find("row3");
        if (row3 != null)
        {
            string[] labels = isKoreanMode ? hangulRow3 : qwertyRow3;
            UpdateRowLabels(row3, labels);
        }

        // row4 업데이트 (첫 번째와 마지막은 특수키)
        Transform row4 = keyboardPanel.transform.Find("row4");
        if (row4 != null)
        {
            string[] labels = isKoreanMode ? hangulRow4 : qwertyRow4;
            int idx = 0;
            for (int i = 1; i < row4.childCount - 1 && idx < labels.Length; i++)
            {
                Text text = row4.GetChild(i).GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = isShiftActive && !isKoreanMode ? labels[idx].ToUpper() : labels[idx];
                }
                idx++;
            }

            // Shift 키 색상 업데이트
            Transform shiftKey = row4.GetChild(0);
            if (shiftKey != null)
            {
                Image shiftBg = shiftKey.GetComponent<Image>();
                if (shiftBg != null)
                {
                    shiftBg.color = isShiftActive ? new Color(0.5f, 0.5f, 0.9f) : specialKeyColor;
                }
            }
        }

        // 한/영 버튼 업데이트
        Transform spaceRow = keyboardPanel.transform.Find("SpaceBarRow");
        if (spaceRow != null && spaceRow.childCount > 0)
        {
            Text langText = spaceRow.GetChild(0).GetComponentInChildren<Text>();
            if (langText != null)
            {
                langText.text = isKoreanMode ? "한" : "EN";
            }
        }
    }

    private void UpdateRowLabels(Transform row, string[] labels)
    {
        int idx = 0;
        foreach (Transform child in row)
        {
            if (idx >= labels.Length) break;

            Text text = child.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = isShiftActive && !isKoreanMode ? labels[idx].ToUpper() : labels[idx];
            }
            idx++;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
