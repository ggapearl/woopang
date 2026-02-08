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
#if UNITY_EDITOR
        // VirtualLocation이 있으면 그 좌표를, 없으면 기본 청주 좌표 사용
        float lat = VirtualLocation.Instance != null ? VirtualLocation.Instance.Latitude : 36.6361f;
        float lon = VirtualLocation.Instance != null ? VirtualLocation.Instance.Longitude : 126.8280f;
        StartCoroutine(GetAddressFromCoordinates(lat, lon));
        if (!isRefreshing)
        {
            isRefreshing = true;
            StartCoroutine(RefreshLocationPeriodically());
        }
        yield break;
#else
        if (!Input.location.isEnabledByUser)
        {
            DisplayLocationDisabledMessage();
            yield break;
        }

        Input.location.Start();

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return waitOneSecond;
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            DisplayLocationDisabledMessage();
            yield break;
        }
        else if (maxWait <= 0)
        {
            DisplayLocationDisabledMessage();
            yield break;
        }
        else if (Input.location.status == LocationServiceStatus.Running)
        {
            float latitude = Input.location.lastData.latitude;
            float longitude = Input.location.lastData.longitude;
            StartCoroutine(GetAddressFromCoordinates(latitude, longitude));

            if (!isRefreshing)
            {
                isRefreshing = true;
                StartCoroutine(RefreshLocationPeriodically());
            }
        }
#endif
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
                // API 요청 실패 시 조용히 처리 (에디터에서 타임아웃 발생 정상)

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
#if UNITY_EDITOR
            // VirtualLocation이 있으면 그 좌표를, 없으면 기본 청주 좌표 사용
            float latitude = VirtualLocation.Instance != null ? VirtualLocation.Instance.Latitude : 36.6361f;
            float longitude = VirtualLocation.Instance != null ? VirtualLocation.Instance.Longitude : 126.8280f;
            // 에디터에서 주기적 갱신 (로그 제거)
            StartCoroutine(GetAddressFromCoordinates(latitude, longitude));
#else
            if (Input.location.status == LocationServiceStatus.Running)
            {
                float latitude = Input.location.lastData.latitude;
                float longitude = Input.location.lastData.longitude;
                // 주기적 갱신 (로그 제거)
                StartCoroutine(GetAddressFromCoordinates(latitude, longitude));
            }
            else
            {
                isRefreshing = false;
            }
#endif
        }
    }

    public void RequestLocationUpdate()
    {
        DisplayInitializingMessage();
        StartCoroutine(CheckLocationService());
    }

    void OnDisable()
    {
        isRefreshing = false;
        Input.location.Stop();
    }
}