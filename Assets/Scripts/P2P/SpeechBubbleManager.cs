/**
 * SpeechBubbleManager.cs
 * 말풍선 시스템 관리
 * - 3D 공간 오브젝트 / P2P 아바타에 말풍선 추가
 * - 7일 자동 삭제
 * - 생성자만 수정 가능
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-01
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;

[Serializable]
public class SpeechBubbleData
{
    public string id;
    public string owner_id;
    public string target_type;  // "place" or "user"
    public string target_id;
    public string content;
    public double latitude;
    public double longitude;
    public double altitude;
    public string background_color;
    public string text_color;
    public string created_at;
    public string expires_at;
}

public class SpeechBubbleManager : MonoBehaviour
{
    public static SpeechBubbleManager Instance { get; private set; }

    [Header("Prefab")]
    public GameObject speechBubblePrefab;

    [Header("Settings")]
    public Vector2 bubbleSize = new Vector2(200, 120); // 가로 x 세로
    public float billboardOffsetY = 2.5f; // 오브젝트 위 높이
    public int maxContentLength = 200; // 최대 200자

    [Header("Server")]
    private string serverUrl => ApiConfig.SPEECH_BUBBLE_SERVER;

    [Header("Create Bubble UI")]
    public GameObject createBubblePanel;
    public TMP_InputField contentInputField;
    public Button submitButton;
    public Button cancelButton;
    public TextMeshProUGUI characterCountText;

    private Dictionary<string, GameObject> activeBubbles = new Dictionary<string, GameObject>();
    private string pendingTargetType;
    private string pendingTargetId;

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
        SetupCreateBubbleUI();

        // 주기적으로 만료된 말풍선 체크 (5분마다)
        InvokeRepeating(nameof(CheckExpiredBubbles), 300f, 300f);
    }

    private void SetupCreateBubbleUI()
    {
        if (createBubblePanel != null)
            createBubblePanel.SetActive(false);

        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitBubble);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelBubble);

        if (contentInputField != null)
        {
            contentInputField.characterLimit = maxContentLength;
            contentInputField.onValueChanged.AddListener(OnContentChanged);
        }
    }

    /// <summary>
    /// 말풍선 생성 UI 표시
    /// </summary>
    public void ShowCreateBubbleUI(string targetType, string targetId)
    {
        pendingTargetType = targetType;
        pendingTargetId = targetId;

        if (createBubblePanel != null)
            createBubblePanel.SetActive(true);

        if (contentInputField != null)
        {
            contentInputField.text = "";
            contentInputField.ActivateInputField();
        }

        UpdateCharacterCount();
    }

    private void OnContentChanged(string text)
    {
        UpdateCharacterCount();
    }

    private void UpdateCharacterCount()
    {
        if (characterCountText != null && contentInputField != null)
        {
            int currentLength = contentInputField.text.Length;
            characterCountText.text = $"{currentLength} / {maxContentLength}";
        }
    }

    private void OnSubmitBubble()
    {
        if (contentInputField == null || string.IsNullOrEmpty(contentInputField.text.Trim()))
        {
            return;
        }

        string content = contentInputField.text.Trim();

        // 서버에 말풍선 생성 요청
        StartCoroutine(CreateSpeechBubble(pendingTargetType, pendingTargetId, content));

        // UI 닫기
        OnCancelBubble();
    }

    private void OnCancelBubble()
    {
        if (createBubblePanel != null)
            createBubblePanel.SetActive(false);

        pendingTargetType = null;
        pendingTargetId = null;
    }

    /// <summary>
    /// 서버에 말풍선 생성 요청
    /// </summary>
    public IEnumerator CreateSpeechBubble(string targetType, string targetId, string content)
    {
        if (LoginManager.Instance == null || LoginManager.Instance.CurrentUser == null)
        {
            Debug.LogError("[SpeechBubbleManager] User not logged in");
            yield break;
        }

        // 현재 위치 가져오기 (AREarthManager 사용)
        Google.XR.ARCoreExtensions.AREarthManager earthManager = FindFirstObjectByType<Google.XR.ARCoreExtensions.AREarthManager>();
        if (earthManager == null || earthManager.EarthTrackingState != UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
        {
            Debug.LogError("[SpeechBubbleManager] GPS not available");
            yield break;
        }

        var pose = earthManager.CameraGeospatialPose;

        // 요청 데이터 생성
        JObject requestData = new JObject
        {
            ["owner_id"] = LoginManager.Instance.CurrentUser.id,
            ["target_type"] = targetType,
            ["target_id"] = targetId,
            ["content"] = content,
            ["latitude"] = pose.Latitude,
            ["longitude"] = pose.Longitude,
            ["altitude"] = pose.Altitude
        };

        string json = requestData.ToString();
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest($"{serverUrl}/api/p2p/speech_bubble", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            // 응답에서 bubble_id 받아서 로컬에 생성
            JObject response = JObject.Parse(request.downloadHandler.text);
            string bubbleId = response["bubble_id"]?.ToString();

            if (!string.IsNullOrEmpty(bubbleId))
            {
                // 말풍선 로드 및 표시
                StartCoroutine(LoadSpeechBubble(bubbleId));
            }
        }
        else
        {
            Debug.LogError($"[SpeechBubbleManager] Failed to create speech bubble: {request.error}");
        }

        request.Dispose();
    }

    /// <summary>
    /// 서버에서 말풍선 로드
    /// </summary>
    private IEnumerator LoadSpeechBubble(string bubbleId)
    {
        UnityWebRequest request = UnityWebRequest.Get($"{serverUrl}/api/p2p/speech_bubble/{bubbleId}");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            SpeechBubbleData bubbleData = JsonConvert.DeserializeObject<SpeechBubbleData>(request.downloadHandler.text);
            InstantiateBubble(bubbleData);
        }

        request.Dispose();
    }

    /// <summary>
    /// 말풍선 3D 오브젝트 생성
    /// </summary>
    public GameObject InstantiateBubble(SpeechBubbleData data)
    {
        if (speechBubblePrefab == null)
        {
            Debug.LogError("[SpeechBubbleManager] Speech bubble prefab not assigned");
            return null;
        }

        // 위치 계산 (GPS 좌표)
        Vector3 position = new Vector3((float)data.latitude, (float)data.altitude + billboardOffsetY, (float)data.longitude);

        GameObject bubble = Instantiate(speechBubblePrefab, position, Quaternion.identity);
        bubble.name = $"SpeechBubble_{data.id}";

        // 크기 설정
        Canvas canvas = bubble.GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            RectTransform rect = canvas.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = bubbleSize;
            }
        }

        // 텍스트 설정
        TextMeshProUGUI textComponent = bubble.GetComponentInChildren<TextMeshProUGUI>();
        if (textComponent != null)
        {
            textComponent.text = data.content;

            // 색상 설정
            if (!string.IsNullOrEmpty(data.text_color))
            {
                if (ColorUtility.TryParseHtmlString(data.text_color, out Color textColor))
                {
                    textComponent.color = textColor;
                }
            }
        }

        // 배경 색상 설정
        Image backgroundImage = bubble.GetComponentInChildren<Image>();
        if (backgroundImage != null && !string.IsNullOrEmpty(data.background_color))
        {
            if (ColorUtility.TryParseHtmlString(data.background_color, out Color bgColor))
            {
                backgroundImage.color = bgColor;
            }
        }

        // Billboard 컴포넌트 확인
        Billboard billboardScript = bubble.GetComponent<Billboard>();
        if (billboardScript == null)
        {
            billboardScript = bubble.AddComponent<Billboard>();
        }

        // 활성 말풍선 딕셔너리에 추가
        activeBubbles[data.id] = bubble;

        // 만료 시간 체크
        DateTime expiresAt = DateTime.Parse(data.expires_at);
        float timeUntilExpiry = (float)(expiresAt - DateTime.Now).TotalSeconds;
        if (timeUntilExpiry > 0)
        {
            StartCoroutine(RemoveBubbleAfterDelay(data.id, timeUntilExpiry));
        }
        else
        {
            // 이미 만료됨
            Destroy(bubble);
            activeBubbles.Remove(data.id);
        }

        return bubble;
    }

    private IEnumerator RemoveBubbleAfterDelay(string bubbleId, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (activeBubbles.ContainsKey(bubbleId))
        {
            GameObject bubble = activeBubbles[bubbleId];
            if (bubble != null)
            {
                Destroy(bubble);
            }
            activeBubbles.Remove(bubbleId);
        }
    }

    /// <summary>
    /// 만료된 말풍선 체크 및 제거
    /// </summary>
    public void CheckExpiredBubbles()
    {
        // 서버에서 만료된 말풍선 목록 가져오기
        StartCoroutine(FetchAndRemoveExpiredBubbles());
    }

    private IEnumerator FetchAndRemoveExpiredBubbles()
    {
        // TODO: 서버 API 호출하여 만료된 말풍선 ID 목록 받기
        yield return null;
    }

    /// <summary>
    /// 특정 타겟의 말풍선 로드
    /// </summary>
    public IEnumerator LoadBubblesForTarget(string targetType, string targetId)
    {
        UnityWebRequest request = UnityWebRequest.Get($"{serverUrl}/api/p2p/speech_bubbles?target_type={targetType}&target_id={targetId}");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            JObject response = JObject.Parse(request.downloadHandler.text);
            JArray bubbles = (JArray)response["bubbles"];

            if (bubbles != null)
            {
                foreach (JObject bubbleObj in bubbles)
                {
                    SpeechBubbleData data = bubbleObj.ToObject<SpeechBubbleData>();
                    InstantiateBubble(data);
                }
            }
        }

        request.Dispose();
    }
}
