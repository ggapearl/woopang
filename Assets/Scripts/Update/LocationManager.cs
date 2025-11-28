using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Globalization;
using UnityEngine.Networking;
using SimpleJSON;
using System.Text;

public class LocationManager : MonoBehaviour
{
    [SerializeField] private Image statusImage;
    [SerializeField] private Sprite successSprite;
    [SerializeField] private Sprite failSprite;
    [SerializeField] private Text infoText;
    [SerializeField] private float refreshInterval = 30f;

    private string currentLanguage;
    private bool isRefreshing = false;
    private WaitForSeconds waitOneSecond = new WaitForSeconds(1f);
    private WaitForSeconds waitRefreshInterval;
    private StringBuilder textBuilder = new StringBuilder(200);

    void Awake()
    {
        waitRefreshInterval = new WaitForSeconds(refreshInterval);
    }

    void Start()
    {
        currentLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToLower();
        DisplayInitializingMessage();
        StartCoroutine(CheckLocationService());
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Debug.Log("앱이 포그라운드로 전환됨 - 위치 요청 재시작");
            DisplayInitializingMessage();
            StartCoroutine(CheckLocationService());
        }
    }

    void DisplayInitializingMessage()
    {
        statusImage.gameObject.SetActive(false);

        string message = currentLanguage switch
        {
            "ko" => "위치서비스 초기화 중",
            "ja" => "位置サービス初期化中",
            "zh" => "位置服务初始化中",
            "es" => "Inicializando el servicio de ubicación",
            _ => "Initializing location service"
        };
        infoText.text = message;
    }

    IEnumerator CheckLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("위치 서비스가 사용자에 의해 비활성화됨");
            DisplayLocationDisabledMessage();
            yield break;
        }

        Debug.Log("위치 서비스 시작 시도");
        Input.location.Start();

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            Debug.Log($"초기화 대기 중... 남은 시간: {maxWait}초");
            yield return waitOneSecond;
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.Log("위치 서비스 초기화 실패");
            DisplayLocationDisabledMessage();
            yield break;
        }
        else if (maxWait <= 0)
        {
            Debug.Log("위치 서비스 초기화 타임아웃");
            DisplayLocationDisabledMessage();
            yield break;
        }
        else if (Input.location.status == LocationServiceStatus.Running)
        {
            float latitude = Input.location.lastData.latitude;
            float longitude = Input.location.lastData.longitude;
            Debug.Log($"위치 서비스 성공 - Lat: {latitude}, Lon: {longitude}");
            StartCoroutine(GetAddressFromCoordinates(latitude, longitude));

            if (!isRefreshing)
            {
                isRefreshing = true;
                StartCoroutine(RefreshLocationPeriodically());
            }
        }
    }

    void DisplayLocationDisabledMessage()
    {
        statusImage.sprite = failSprite;
        statusImage.gameObject.SetActive(true);

        string message = currentLanguage switch
        {
            "ko" => "위치서비스가 활성화되지 않았습니다.",
            "ja" => "位置サービスが有効になっていません。",
            "zh" => "位置服务未启用。",
            "es" => "El servicio de ubicación no está activado.",
            _ => "Location service is not enabled."
        };
        infoText.text = message;
    }

    IEnumerator GetAddressFromCoordinates(float latitude, float longitude)
    {
        statusImage.sprite = successSprite;
        statusImage.gameObject.SetActive(true);

        StringBuilder urlBuilder = new StringBuilder(100);
        urlBuilder.Append("https://nominatim.openstreetmap.org/reverse?lat=");
        urlBuilder.Append(latitude.ToString("F4"));
        urlBuilder.Append("&lon=");
        urlBuilder.Append(longitude.ToString("F4"));
        urlBuilder.Append("&format=json&accept-language=");
        urlBuilder.Append(currentLanguage);

        using (UnityWebRequest request = UnityWebRequest.Get(urlBuilder.ToString()))
        {
            request.SetRequestHeader("User-Agent", "WoopangARApp/1.0");
            request.timeout = 10; // 10초 타임아웃
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"[LocationManager] API 응답: {jsonResponse}");
                    JSONNode data = JSON.Parse(jsonResponse);

                    textBuilder.Clear();
                    textBuilder.Append("Lat: ").Append(latitude.ToString("F4"));
                    textBuilder.Append(", Lon: ").Append(longitude.ToString("F4"));
                    textBuilder.Append("\n");

                    string displayName = data["display_name"].Value;
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        string[] addressParts = displayName.Split(',');
                        if (addressParts.Length >= 3)
                        {
                            textBuilder.Append(addressParts[0].Trim()).Append(", ");
                            textBuilder.Append(addressParts[1].Trim()).Append(", ");
                            textBuilder.Append(addressParts[2].Trim());
                        }
                        else
                        {
                            textBuilder.Append(displayName);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[LocationManager] display_name 데이터가 없음");
                        textBuilder.Append(currentLanguage == "ko" ? "주소 정보 없음" : "No address");
                    }

                    infoText.text = textBuilder.ToString();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LocationManager] JSON 파싱 에러: {e.Message}");
                    textBuilder.Clear();
                    textBuilder.Append("Lat: ").Append(latitude.ToString("F4"));
                    textBuilder.Append(", Lon: ").Append(longitude.ToString("F4"));
                    infoText.text = textBuilder.ToString();
                }
            }
            else
            {
                Debug.LogError($"[LocationManager] API 요청 실패: {request.error}, Status Code: {request.responseCode}");

                // 좌표만이라도 표시
                textBuilder.Clear();
                textBuilder.Append("Lat: ").Append(latitude.ToString("F4"));
                textBuilder.Append(", Lon: ").Append(longitude.ToString("F4"));
                infoText.text = textBuilder.ToString();
            }
        }
    }

    IEnumerator RefreshLocationPeriodically()
    {
        while (isRefreshing)
        {
            yield return waitRefreshInterval;
            if (Input.location.status == LocationServiceStatus.Running)
            {
                float latitude = Input.location.lastData.latitude;
                float longitude = Input.location.lastData.longitude;
                Debug.Log($"주기적 갱신 - Lat: {latitude}, Lon: {longitude}");
                StartCoroutine(GetAddressFromCoordinates(latitude, longitude));
            }
            else
            {
                Debug.Log("위치 서비스가 실행 중이 아님 - 갱신 중지");
                isRefreshing = false;
            }
        }
    }

    public void RequestLocationUpdate()
    {
        Debug.Log("수동 위치 갱신 요청");
        DisplayInitializingMessage();
        StartCoroutine(CheckLocationService());
    }

    void OnDisable()
    {
        isRefreshing = false;
        Input.location.Stop();
        Debug.Log("LocationManager 비활성화 - 위치 서비스 중지");
    }
}