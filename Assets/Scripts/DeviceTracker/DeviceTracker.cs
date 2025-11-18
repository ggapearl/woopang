using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class DeviceTracker : MonoBehaviour
{
    [SerializeField] private float updateInterval = 10f; // 위치 전송 주기 (초)
    [SerializeField] private string serverUrl = "https://woopang.com/device-location";
    private string deviceId;
    private string deviceName = "우팡이"; // UI 표시용 고정 이름

    void Start()
    {
        deviceId = PlayerPrefs.GetString("DeviceId", SystemInfo.deviceUniqueIdentifier);
        PlayerPrefs.SetString("DeviceId", deviceId);
        Debug.Log($"[DeviceTracker] Device ID: {deviceId}, Name: {deviceName}");

        StartCoroutine(StartLocationService());
    }

    private IEnumerator StartLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogError("[DeviceTracker] 위치 서비스 비활성화");
            yield break;
        }

        Input.location.Start();
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("[DeviceTracker] 위치 서비스 시작 실패");
            yield break;
        }

        Debug.Log("[DeviceTracker] 위치 서비스 시작됨");
        StartCoroutine(SendLocationPeriodically());
    }

    private IEnumerator SendLocationPeriodically()
    {
        while (true)
        {
            LocationInfo location = Input.location.lastData;
            yield return StartCoroutine(SendLocationToServer(location));
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private IEnumerator SendLocationToServer(LocationInfo location)
    {
        string json = JsonUtility.ToJson(new DeviceLocationData
        {
            deviceId = deviceId,
            deviceName = deviceName,
            latitude = location.latitude,
            longitude = location.longitude,
            altitude = location.altitude
        });

        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"[DeviceTracker] 위치 전송: {json}");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[DeviceTracker] 위치 전송 성공: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"[DeviceTracker] 위치 전송 실패: {request.error}");
            }
        }
    }

    [System.Serializable]
    private class DeviceLocationData
    {
        public string deviceId;
        public string deviceName;
        public float latitude;
        public float longitude;
        public float altitude;
    }
}