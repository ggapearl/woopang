using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

public class ARSessionRecovery : MonoBehaviour
{
    [SerializeField] private ARSession arSession;
    private List<GameObject> spawnedObjects = new List<GameObject>(); // 스폰된 오브젝트 목록
    private Vector2 lastKnownLocation; // 마지막 위치 저장

    [Header("GPS Recovery")]
    [Tooltip("GPS 신호 못 잡을 때 재시도 횟수")]
    [SerializeField] private int maxGpsRetries = 3;
    [Tooltip("재시도 사이 대기 시간 (초)")]
    [SerializeField] private float retryInterval = 2f;
    [Tooltip("각 재시도의 GPS Start 타임아웃 (초)")]
    [SerializeField] private float gpsStartTimeout = 5f;

    void OnEnable()
    {
        if (arSession == null)
        {
            arSession = FindFirstObjectByType<ARSession>();
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
        // GPS 신호 못 잡으면 maxGpsRetries회 재시도. 각 시도 사이 안내 Toast 표시.
        for (int attempt = 1; attempt <= maxGpsRetries; attempt++)
        {
            if (Input.location.status != LocationServiceStatus.Running)
            {
                Input.location.Start();
                float timeout = Time.unscaledTime + gpsStartTimeout;
                yield return new WaitUntil(() => Input.location.status == LocationServiceStatus.Running || Time.unscaledTime >= timeout);
            }

            if (Input.location.status == LocationServiceStatus.Running)
            {
                lastKnownLocation = new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
                UpdateARObjects();
                yield break; // 성공
            }

            // 실패 — 마지막 시도가 아니면 안내 후 재시도
            if (attempt < maxGpsRetries)
            {
                if (ToastManager.Instance != null)
                {
                    bool isKo = Application.systemLanguage == SystemLanguage.Korean;
                    string msg = isKo
                        ? $"AR 세션 복구 중... ({attempt}/{maxGpsRetries})"
                        : $"Recovering AR session... ({attempt}/{maxGpsRetries})";
                    ToastManager.Instance.ShowWarning(msg);
                }
                yield return new WaitForSeconds(retryInterval);
            }
        }

        // maxGpsRetries회 모두 실패 — 최종 안내 (실외 이동 권장)
        if (ToastManager.Instance != null)
        {
            bool isKo = Application.systemLanguage == SystemLanguage.Korean;
            string msg = isKo
                ? "GPS 신호를 찾지 못하였습니다.\n실외 또는 신호가 잡히는 곳으로 이동해주세요."
                : "GPS signal not found.\nPlease move outdoors or to an area with signal.";
            ToastManager.Instance.ShowError(msg);
        }
        ShowErrorMessage("GPS signal not found after retries.");
    }

    void UpdateARObjects()
    {
        Vector2 currentLocation = GetCurrentLocation();
        // ���� ������Ʈ ����
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (!IsObjectValidForLocation(spawnedObjects[i], currentLocation))
            {
                Destroy(spawnedObjects[i]);
                spawnedObjects.RemoveAt(i);
            }
        }
        // ���ο� ������Ʈ ����
        if (ARSession.state == ARSessionState.SessionTracking)
        {
            SpawnObjectsForLocation(currentLocation);
        }
    }

    Vector2 GetCurrentLocation()
    {
        if (Input.location.status == LocationServiceStatus.Running)
        {
            return new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        }
        return lastKnownLocation; // ��ġ ���� ���� �� ������ ��ġ ��ȯ
    }

    bool IsObjectValidForLocation(GameObject obj, Vector2 location)
    {
        // ������Ʈ�� ��ġ �±׿� ���� ��ġ �� (�ۺ� ���� �ʿ�)
        return true; // �ӽ� ����
    }

    void SpawnObjectsForLocation(Vector2 location)
    {
        // ��ġ ��� ������Ʈ ���� (�ۺ� ���� �ʿ�)
        // GameObject newObj = Instantiate(prefab, position, rotation);
        // spawnedObjects.Add(newObj);
    }

    void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        if (args.state == ARSessionState.SessionTracking)
        {
            UpdateARObjects();
        }
    }

    void ShowErrorMessage(string message)
    {
        Debug.LogError(message);
        // UI�� ���� �޽��� ǥ�� (���� �ʿ�)
    }
}