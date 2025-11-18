using UnityEngine;
using System.Collections;

public class LocationTrigger : MonoBehaviour
{
    // 목표 위치의 위도와 경도
    public float targetLatitude = 37.5665f;  // 서울 광화문 좌표
    public float targetLongitude = 126.9780f;

    // 거리 임계값 (미터)
    public float triggerDistance = 50.0f;

    // 미리 배치한 3D 오브젝트 (활성화/비활성화할 오브젝트)
    public GameObject objectToActivate;

    // 위치 서비스 초기화 여부
    private bool isLocationEnabled = false;

    void Start()
    {
        // 위치 서비스 시작
        StartCoroutine(StartLocationService());
    }

    IEnumerator StartLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogError("Location service is disabled by user.");
            yield break;
        }

        Input.location.Start();
        int maxWait = 20;

        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (maxWait <= 0 || Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("Unable to determine device location.");
            yield break;
        }

        isLocationEnabled = true;
    }

    void Update()
    {
        if (isLocationEnabled)
        {
            float currentLatitude = Input.location.lastData.latitude;
            float currentLongitude = Input.location.lastData.longitude;

            float distance = CalculateDistance(currentLatitude, currentLongitude, targetLatitude, targetLongitude);

            if (distance <= triggerDistance)
            {
                TriggerEvent();
            }
        }
    }

    float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        float earthRadius = 6371000;  // 지구 반지름 (미터)
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);

        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(Mathf.Deg2Rad * lat1) * Mathf.Cos(Mathf.Deg2Rad * lat2) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return earthRadius * c;
    }

    void TriggerEvent()
    {
        Debug.Log("Target location reached!");

        // 오브젝트를 활성화하거나 배치
        if (objectToActivate != null)
        {
            objectToActivate.SetActive(true);  // 비활성화된 오브젝트를 활성화
        }
        else
        {
            // 만약 동적 생성하고 싶다면 프리팹을 사용해 Instantiate
            GameObject newObject = GameObject.CreatePrimitive(PrimitiveType.Cube);  // 예제: 큐브 생성
            newObject.transform.position = new Vector3(0, 0, 0);  // 원하는 위치에 배치
        }
    }
}
