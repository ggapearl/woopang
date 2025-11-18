using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Linq; // 추가
using System; // 추가

public class TargetDeviceManager : MonoBehaviour
{
    [SerializeField] private float fetchInterval = 10f; // 추적 요청 주기 (초)
    [SerializeField] private float loadRadius = 1000f; // 조회 반경 (미터)
    [SerializeField] private GameObject devicePrefab; // 타겟 디바이스 프리팹
    [SerializeField] private int poolSize = 10; // 오브젝트 풀 크기
    private string serverUrl = "https://woopang.com/device-locations";
    private Dictionary<string, GameObject> spawnedDevices = new Dictionary<string, GameObject>();
    private Dictionary<string, DeviceData> deviceDataMap = new Dictionary<string, DeviceData>();
    private Queue<GameObject> devicePool = new Queue<GameObject>();
    private Vector2 lastPosition;

    void Start()
    {
        InitializeDevicePool();
        StartCoroutine(StartLocationServiceAndFetch());
    }

    private void InitializeDevicePool()
    {
        if (devicePrefab == null)
        {
            Debug.LogError("[TargetDeviceManager] devicePrefab 설정 안됨");
            return;
        }
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(devicePrefab, Vector3.zero, Quaternion.identity);
            obj.SetActive(false);
            devicePool.Enqueue(obj);
        }
    }

    private IEnumerator StartLocationServiceAndFetch()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogError("[TargetDeviceManager] 위치 서비스 비활성화");
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
            Debug.LogError("[TargetDeviceManager] 위치 서비스 시작 실패");
            yield break;
        }

        lastPosition = new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        StartCoroutine(FetchDeviceDataPeriodically());
    }

    private IEnumerator FetchDeviceDataPeriodically()
    {
        while (true)
        {
            LocationInfo currentLocation = Input.location.lastData;
            string url = $"{serverUrl}?lat={currentLocation.latitude}&lon={currentLocation.longitude}&radius={loadRadius}";
            yield return StartCoroutine(FetchDeviceDataFromServer(url));
            yield return new WaitForSeconds(fetchInterval);
        }
    }

    private IEnumerator FetchDeviceDataFromServer(string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                List<DeviceData> devices = JsonUtility.FromJson<DeviceDataWrapper>("{\"devices\":" + json + "}").devices;
                yield return StartCoroutine(ProcessDeviceData(devices));
            }
            else
            {
                Debug.LogError($"[TargetDeviceManager] 디바이스 데이터 가져오기 실패: {request.error}");
            }
        }
    }

    private IEnumerator ProcessDeviceData(List<DeviceData> devices)
    {
        foreach (DeviceData device in devices)
        {
            deviceDataMap[device.deviceId] = device;
            if (!spawnedDevices.ContainsKey(device.deviceId))
            {
                CreateDeviceObject(device);
            }
            else
            {
                UpdateDeviceObject(device, spawnedDevices[device.deviceId]);
            }
        }

        List<string> toRemove = spawnedDevices.Keys
            .Where(id => !devices.Exists(d => d.deviceId == id))
            .ToList();
        foreach (string id in toRemove)
        {
            ReturnToPool(spawnedDevices[id]);
            spawnedDevices.Remove(id);
            deviceDataMap.Remove(id);
        }

        yield return null;
    }

    private void CreateDeviceObject(DeviceData device)
    {
        GameObject obj = GetFromPool();
        if (obj == null) return;

        obj.SetActive(true);
        obj.name = $"Device_{device.deviceId}";
        if (SetupDeviceComponents(obj, device))
        {
            spawnedDevices[device.deviceId] = obj;
        }
        else
        {
            ReturnToPool(obj);
        }
    }

    private void UpdateDeviceObject(DeviceData device, GameObject existingObj)
    {
        SetupDeviceComponents(existingObj, device);
    }

    private bool SetupDeviceComponents(GameObject obj, DeviceData device)
    {
        CustomARGeospatialCreatorAnchor anchor = obj.GetComponentInChildren<CustomARGeospatialCreatorAnchor>();
        if (anchor == null)
        {
            Debug.LogError($"[TargetDeviceManager] Device {device.deviceId}에 CustomARGeospatialCreatorAnchor 없음");
            return false;
        }
        anchor.SetCoordinatesAndCreateAnchor(device.latitude, device.longitude, device.altitude);

        ImageDisplayController display = obj.GetComponentInChildren<ImageDisplayController>();
        if (display == null)
        {
            Debug.LogError($"[TargetDeviceManager] Device {device.deviceId}에 ImageDisplayController 없음");
            return false;
        }
        display.SetBaseMap("https://woopang.com/uploads/default_device.jpg");

        DoubleTap3D doubleTap = obj.GetComponentInChildren<DoubleTap3D>();
        if (doubleTap == null)
        {
            Debug.LogError($"[TargetDeviceManager] Device {device.deviceId}에 DoubleTap3D 없음");
            return false;
        }
        Sprite defaultSprite = Resources.Load<Sprite>("Sprites/default_icon");
        doubleTap.SetInfoImages(defaultSprite, defaultSprite, false, false, "", device.deviceName ?? "우팡이", 0, device.deviceId, "");

        Target target = obj.GetComponentInChildren<Target>();
        if (target == null)
        {
            Debug.LogError($"[TargetDeviceManager] Device {device.deviceId}에 Target 컴포넌트 없음");
            return false;
        }
        target.TargetColor = Color.red;
        target.PlaceName = device.deviceName ?? "우팡이";

        TextMeshProUGUI statusText = obj.GetComponentInChildren<TextMeshProUGUI>();
        if (statusText != null)
        {
            DateTime lastUpdated = DateTime.Parse(device.lastUpdated);
            TimeSpan elapsed = DateTime.UtcNow - lastUpdated;
            statusText.text = elapsed.TotalMinutes < 1 ? "방금 전" :
                              elapsed.TotalHours < 1 ? $"{(int)elapsed.TotalMinutes}분 전" :
                              $"{(int)elapsed.TotalHours}시간 전";
            statusText.color = elapsed.TotalMinutes < 5 ? Color.green : Color.gray;
        }

        return true;
    }

    private GameObject GetFromPool()
    {
        if (devicePool.Count > 0)
        {
            return devicePool.Dequeue();
        }
        if (spawnedDevices.Count < poolSize)
        {
            return Instantiate(devicePrefab, Vector3.zero, Quaternion.identity);
        }
        Debug.LogError("[TargetDeviceManager] 오브젝트 풀 한계 초과");
        return null;
    }

    private void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        devicePool.Enqueue(obj);
    }

    public Dictionary<string, DeviceData> GetDeviceDataMap()
    {
        return deviceDataMap;
    }

    [System.Serializable]
    public class DeviceData
    {
        public string deviceId;
        public string deviceName;
        public float latitude;
        public float longitude;
        public float altitude;
        public string lastUpdated;
    }

    [System.Serializable]
    public class DeviceDataWrapper
    {
        public List<DeviceData> devices;
    }
}