using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

public class ARSessionRecovery : MonoBehaviour
{
    [SerializeField] private ARSession arSession;
    private List<GameObject> spawnedObjects = new List<GameObject>(); // ������ ������Ʈ ���
    private Vector2 lastKnownLocation; // ���� ��ġ ����

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
        // ��ġ ���� �ʱ�ȭ
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
            ShowErrorMessage("��ġ ������ ������ �� �����ϴ�. �ٽ� �õ��ϼ���.");
        }
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
        Debug.Log($"AR objects updated. Location: {currentLocation}, Objects: {spawnedObjects.Count}, Session state: {ARSession.state}");
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
        Debug.Log($"ARSession state: {args.state}, NotTrackingReason: {ARSession.notTrackingReason}");
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