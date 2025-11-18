using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

public class ARSessionRecovery : MonoBehaviour
{
    [SerializeField] private ARSession arSession;
    private List<GameObject> spawnedObjects = new List<GameObject>(); // 생성된 오브젝트 목록
    private Vector2 lastKnownLocation; // 이전 위치 저장

    void OnEnable()
    {
        if (arSession == null)
        {
            arSession = FindObjectOfType<ARSession>();
            if (arSession == null)
            {
                Debug.LogError("ARSession not found!");
                enabled = false;
                return;
            }
        }
        ARSession.stateChanged += OnARSessionStateChanged;
    }

    void OnDisable()
    {
        ARSession.stateChanged -= OnARSessionStateChanged;
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            StartCoroutine(UpdateLocationAndObjects());
        }
    }

    System.Collections.IEnumerator UpdateLocationAndObjects()
    {
        // 위치 서비스 초기화
        if (Input.location.status != LocationServiceStatus.Running)
        {
            Input.location.Start();
            float timeout = Time.unscaledTime + 5f;
            yield return new WaitUntil(() => Input.location.status == LocationServiceStatus.Running || Time.unscaledTime >= timeout);
        }

        if (Input.location.status == LocationServiceStatus.Running)
        {
            lastKnownLocation = new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
            Debug.Log($"Location updated: {lastKnownLocation}");
            UpdateARObjects();
        }
        else
        {
            Debug.LogWarning("Failed to update location.");
            ShowErrorMessage("위치 정보를 갱신할 수 없습니다. 다시 시도하세요.");
        }
    }

    void UpdateARObjects()
    {
        Vector2 currentLocation = GetCurrentLocation();
        // 기존 오브젝트 제거
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (!IsObjectValidForLocation(spawnedObjects[i], currentLocation))
            {
                Destroy(spawnedObjects[i]);
                spawnedObjects.RemoveAt(i);
            }
        }
        // 새로운 오브젝트 생성
        if (ARSession.state == ARSessionState.SessionTracking)
        {
            SpawnObjectsForLocation(currentLocation);
        }
        Debug.Log($"AR objects updated. Location: {currentLocation}, Objects: {spawnedObjects.Count}, Session state: {ARSession.state}");
    }

    Vector2 GetCurrentLocation()
    {
        if (Input.location.status == LocationServiceStatus.Running)
        {
            return new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        }
        return lastKnownLocation; // 위치 서비스 실패 시 마지막 위치 반환
    }

    bool IsObjectValidForLocation(GameObject obj, Vector2 location)
    {
        // 오브젝트의 위치 태그와 현재 위치 비교 (앱별 구현 필요)
        return true; // 임시 구현
    }

    void SpawnObjectsForLocation(Vector2 location)
    {
        // 위치 기반 오브젝트 생성 (앱별 구현 필요)
        // GameObject newObj = Instantiate(prefab, position, rotation);
        // spawnedObjects.Add(newObj);
    }

    void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        Debug.Log($"ARSession state: {args.state}, NotTrackingReason: {ARSession.notTrackingReason}");
        if (args.state == ARSessionState.SessionTracking)
        {
            UpdateARObjects();
        }
    }

    void ShowErrorMessage(string message)
    {
        Debug.LogError(message);
        // UI로 에러 메시지 표시 (구현 필요)
    }
}