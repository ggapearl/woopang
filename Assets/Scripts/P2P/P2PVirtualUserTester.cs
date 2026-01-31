/**
 * P2PVirtualUserTester.cs
 * P2P 시스템 테스트를 위한 가상 사용자 시뮬레이터
 * 에디터에서 실제 사용자 없이 P2P 기능을 테스트할 수 있습니다.
 *
 * 사용법:
 * 1. 씬에 빈 GameObject 생성
 * 2. P2PVirtualUserTester 컴포넌트 추가
 * 3. Play 모드에서 Inspector의 "Start Virtual Users" 버튼 클릭
 *
 * Author: Claude (Anthropic AI)
 * Date: 2026-01-15
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class VirtualUserConfig
{
    public string userId = "virtual_user_1";
    public string username = "가상유저1";
    public double startLatitude = 36.63586;
    public double startLongitude = 126.828;
    public double altitude = 10.0;
    public string avatarUrl = "";
    public string bio = "테스트용 가상 사용자";

    [Header("Movement Settings")]
    [Tooltip("이동 반경 (미터)")]
    public float movementRadius = 50f;
    [Tooltip("걷기 속도 (m/s) - 평균 걸음걸이 1.4m/s")]
    public float walkingSpeed = 1.4f;
    [Tooltip("방향 전환 간격 (초)")]
    public float directionChangeInterval = 5f;

    // Runtime state
    [HideInInspector] public double currentLat;
    [HideInInspector] public double currentLon;
    [HideInInspector] public float currentHeading; // 0-360 degrees
    [HideInInspector] public float timeUntilDirectionChange;
}

public class P2PVirtualUserTester : MonoBehaviour
{
    [Header("Test Location")]
    [Tooltip("테스트 기준 위치 (위도)")]
    public double baseLatitude = 36.63586;
    [Tooltip("테스트 기준 위치 (경도)")]
    public double baseLongitude = 126.828;
    [Tooltip("고정 위치로 있을 내 위치 (테스터는 움직이지 않음)")]
    public bool fixedPosition = true;

    [Header("Virtual Users")]
    public List<VirtualUserConfig> virtualUsers = new List<VirtualUserConfig>();

    [Header("Simulation Settings")]
    [Tooltip("위치 업데이트 주기 (초)")]
    public float updateInterval = 1f;
    [Tooltip("시뮬레이션 활성화")]
    public bool isSimulating = false;

    [Header("Debug")]
    public bool showDebugLogs = false;
    public bool drawGizmos = true;

    private Coroutine simulationCoroutine;

    // 지구 반경 (미터) - GPS 계산용
    private const double EARTH_RADIUS = 6371000.0;

    void Awake()
    {
#if UNITY_EDITOR
        // 기본 가상 사용자 2명 설정 (에디터 테스트용)
        if (virtualUsers.Count == 0)
        {
            SetupDefaultVirtualUsers();
        }
#endif
    }

    /// <summary>
    /// 기본 가상 사용자 2명 생성
    /// </summary>
    private void SetupDefaultVirtualUsers()
    {
        // 사용자 1: 동쪽으로 30m 떨어진 위치에서 시작
        var user1 = new VirtualUserConfig
        {
            userId = "virtual_user_001",
            username = "걷는사람A",
            startLatitude = baseLatitude,
            startLongitude = baseLongitude + 0.0003, // 약 30m 동쪽
            altitude = 10.0,
            bio = "산책 중인 가상 사용자 A",
            movementRadius = 40f,
            walkingSpeed = 1.2f,
            directionChangeInterval = 4f
        };

        // 사용자 2: 북서쪽으로 50m 떨어진 위치에서 시작
        var user2 = new VirtualUserConfig
        {
            userId = "virtual_user_002",
            username = "산책러B",
            startLatitude = baseLatitude + 0.0004, // 약 44m 북쪽
            startLongitude = baseLongitude - 0.0002, // 약 20m 서쪽
            altitude = 10.0,
            bio = "운동 중인 가상 사용자 B",
            movementRadius = 60f,
            walkingSpeed = 1.6f, // 조금 빠르게
            directionChangeInterval = 6f
        };

        virtualUsers.Add(user1);
        virtualUsers.Add(user2);

        Log("기본 가상 사용자 2명 생성됨");
    }

    /// <summary>
    /// 가상 사용자 시뮬레이션 시작
    /// </summary>
    [ContextMenu("Start Virtual Users")]
    public void StartSimulation()
    {
        if (isSimulating)
        {
            Log("이미 시뮬레이션 중입니다.");
            return;
        }

        // P2PManager 확인
        if (P2PManager.Instance == null)
        {
            LogError("P2PManager가 없습니다. P2P 시스템을 먼저 설정하세요.");
            return;
        }

        // 가상 사용자 초기화
        InitializeVirtualUsers();

        isSimulating = true;
        simulationCoroutine = StartCoroutine(SimulationLoop());

        Log($"가상 사용자 시뮬레이션 시작 - {virtualUsers.Count}명");
    }

    /// <summary>
    /// 가상 사용자 시뮬레이션 중지
    /// </summary>
    [ContextMenu("Stop Virtual Users")]
    public void StopSimulation()
    {
        if (!isSimulating)
        {
            Log("시뮬레이션이 실행 중이 아닙니다.");
            return;
        }

        isSimulating = false;

        if (simulationCoroutine != null)
        {
            StopCoroutine(simulationCoroutine);
            simulationCoroutine = null;
        }

        // 가상 사용자 아바타 제거
        RemoveVirtualUserAvatars();

        Log("가상 사용자 시뮬레이션 중지");
    }

    /// <summary>
    /// 가상 사용자 초기 위치 및 방향 설정
    /// </summary>
    private void InitializeVirtualUsers()
    {
        foreach (var user in virtualUsers)
        {
            user.currentLat = user.startLatitude;
            user.currentLon = user.startLongitude;
            user.currentHeading = Random.Range(0f, 360f);
            user.timeUntilDirectionChange = user.directionChangeInterval;
        }
    }

    /// <summary>
    /// 시뮬레이션 메인 루프
    /// </summary>
    private IEnumerator SimulationLoop()
    {
        while (isSimulating)
        {
            // 각 가상 사용자 이동
            foreach (var user in virtualUsers)
            {
                UpdateVirtualUserPosition(user);
            }

            // P2PManager에 가상 사용자 데이터 전달
            InjectVirtualUsersToP2P();

            yield return new WaitForSeconds(updateInterval);
        }
    }

    /// <summary>
    /// 가상 사용자 위치 업데이트 (걷기 시뮬레이션)
    /// </summary>
    private void UpdateVirtualUserPosition(VirtualUserConfig user)
    {
        // 방향 전환 타이머
        user.timeUntilDirectionChange -= updateInterval;
        if (user.timeUntilDirectionChange <= 0)
        {
            // 랜덤하게 방향 전환 (±90도 범위)
            user.currentHeading += Random.Range(-90f, 90f);
            user.currentHeading = (user.currentHeading + 360f) % 360f;
            user.timeUntilDirectionChange = user.directionChangeInterval + Random.Range(-1f, 1f);

            if (showDebugLogs)
            {
                Log($"{user.username}: 방향 전환 → {user.currentHeading:F0}°");
            }
        }

        // 이동 거리 계산 (m)
        float distanceToMove = user.walkingSpeed * updateInterval;

        // 새 위치 계산
        double newLat, newLon;
        CalculateNewPosition(
            user.currentLat, user.currentLon,
            user.currentHeading, distanceToMove,
            out newLat, out newLon
        );

        // 시작 위치에서 너무 멀어지면 방향 반전
        double distanceFromStart = CalculateDistance(
            user.startLatitude, user.startLongitude,
            newLat, newLon
        );

        if (distanceFromStart > user.movementRadius)
        {
            // 시작점 방향으로 회전
            user.currentHeading = CalculateBearing(newLat, newLon, user.startLatitude, user.startLongitude);
            user.timeUntilDirectionChange = user.directionChangeInterval;

            if (showDebugLogs)
            {
                Log($"{user.username}: 경계 도달, 복귀 방향으로 전환");
            }
        }
        else
        {
            user.currentLat = newLat;
            user.currentLon = newLon;
        }
    }

    /// <summary>
    /// GPS 좌표 기반 새 위치 계산
    /// </summary>
    private void CalculateNewPosition(
        double lat, double lon,
        float headingDegrees, float distanceMeters,
        out double newLat, out double newLon)
    {
        double headingRad = headingDegrees * Mathf.Deg2Rad;
        double latRad = lat * Mathf.Deg2Rad;
        double lonRad = lon * Mathf.Deg2Rad;

        double angularDistance = distanceMeters / EARTH_RADIUS;

        double newLatRad = System.Math.Asin(
            System.Math.Sin(latRad) * System.Math.Cos(angularDistance) +
            System.Math.Cos(latRad) * System.Math.Sin(angularDistance) * System.Math.Cos(headingRad)
        );

        double newLonRad = lonRad + System.Math.Atan2(
            System.Math.Sin(headingRad) * System.Math.Sin(angularDistance) * System.Math.Cos(latRad),
            System.Math.Cos(angularDistance) - System.Math.Sin(latRad) * System.Math.Sin(newLatRad)
        );

        newLat = newLatRad * Mathf.Rad2Deg;
        newLon = newLonRad * Mathf.Rad2Deg;
    }

    /// <summary>
    /// 두 GPS 좌표 간 거리 계산 (미터)
    /// </summary>
    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double lat1Rad = lat1 * Mathf.Deg2Rad;
        double lat2Rad = lat2 * Mathf.Deg2Rad;
        double deltaLat = (lat2 - lat1) * Mathf.Deg2Rad;
        double deltaLon = (lon2 - lon1) * Mathf.Deg2Rad;

        double a = System.Math.Sin(deltaLat / 2) * System.Math.Sin(deltaLat / 2) +
                   System.Math.Cos(lat1Rad) * System.Math.Cos(lat2Rad) *
                   System.Math.Sin(deltaLon / 2) * System.Math.Sin(deltaLon / 2);

        double c = 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));

        return EARTH_RADIUS * c;
    }

    /// <summary>
    /// 두 GPS 좌표 간 방위각 계산 (도)
    /// </summary>
    private float CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        double lat1Rad = lat1 * Mathf.Deg2Rad;
        double lat2Rad = lat2 * Mathf.Deg2Rad;
        double deltaLon = (lon2 - lon1) * Mathf.Deg2Rad;

        double x = System.Math.Sin(deltaLon) * System.Math.Cos(lat2Rad);
        double y = System.Math.Cos(lat1Rad) * System.Math.Sin(lat2Rad) -
                   System.Math.Sin(lat1Rad) * System.Math.Cos(lat2Rad) * System.Math.Cos(deltaLon);

        double bearing = System.Math.Atan2(x, y) * Mathf.Rad2Deg;
        return (float)((bearing + 360) % 360);
    }

    /// <summary>
    /// 가상 사용자 데이터를 P2PManager에 직접 주입
    /// </summary>
    private void InjectVirtualUsersToP2P()
    {
        if (P2PManager.Instance == null) return;

        // JSON 형태로 가상 사용자 데이터 생성
        string jsonResponse = BuildVirtualUsersJson();

        // P2PManager의 public 테스트 메서드 호출
        P2PManager.Instance.InjectTestUsersData(jsonResponse);
    }

    /// <summary>
    /// 가상 사용자 데이터 JSON 생성
    /// </summary>
    private string BuildVirtualUsersJson()
    {
        var usersList = new System.Text.StringBuilder();
        usersList.Append("{\"users\":[");

        for (int i = 0; i < virtualUsers.Count; i++)
        {
            var user = virtualUsers[i];

            // 내 위치에서 가상 사용자까지 거리 계산
            float distance = (float)CalculateDistance(
                baseLatitude, baseLongitude,
                user.currentLat, user.currentLon
            );

            if (i > 0) usersList.Append(",");

            usersList.Append("{");
            usersList.Append($"\"user_id\":\"{user.userId}\",");
            usersList.Append($"\"username\":\"{user.username}\",");
            usersList.Append($"\"latitude\":{user.currentLat},");
            usersList.Append($"\"longitude\":{user.currentLon},");
            usersList.Append($"\"altitude\":{user.altitude},");
            usersList.Append($"\"avatar_url\":\"{user.avatarUrl}\",");
            usersList.Append($"\"bio\":\"{user.bio}\",");
            usersList.Append($"\"distance\":{distance:F2}");
            usersList.Append("}");

            if (showDebugLogs)
            {
                Log($"{user.username}: ({user.currentLat:F6}, {user.currentLon:F6}) - {distance:F1}m");
            }
        }

        usersList.Append("]}");
        return usersList.ToString();
    }

    /// <summary>
    /// 가상 사용자 아바타 제거
    /// </summary>
    private void RemoveVirtualUserAvatars()
    {
        if (P2PManager.Instance == null) return;

        // P2PManager의 public 테스트 메서드 호출
        foreach (var user in virtualUsers)
        {
            P2PManager.Instance.RemoveTestUserAvatar(user.userId);
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"<color=#00FFFF>[P2PVirtualUserTester]</color> {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[P2PVirtualUserTester] {message}");
    }

    void OnDestroy()
    {
        StopSimulation();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!drawGizmos || virtualUsers == null) return;

        // 기준 위치 표시
        Gizmos.color = Color.green;
        Vector3 basePos = new Vector3(0, 0, 0);
        Gizmos.DrawWireSphere(basePos, 0.5f);

        // 가상 사용자 위치 표시 (상대적 위치)
        Gizmos.color = Color.cyan;
        foreach (var user in virtualUsers)
        {
            if (user.currentLat == 0 && user.currentLon == 0)
            {
                // 초기화 전 - startPosition 사용
                float dx = (float)((user.startLongitude - baseLongitude) * 111000 * System.Math.Cos(baseLatitude * Mathf.Deg2Rad));
                float dz = (float)((user.startLatitude - baseLatitude) * 111000);
                Vector3 pos = new Vector3(dx, 0, dz);
                Gizmos.DrawWireSphere(pos, 0.3f);
                Gizmos.DrawLine(basePos, pos);
            }
            else
            {
                // 시뮬레이션 중
                float dx = (float)((user.currentLon - baseLongitude) * 111000 * System.Math.Cos(baseLatitude * Mathf.Deg2Rad));
                float dz = (float)((user.currentLat - baseLatitude) * 111000);
                Vector3 pos = new Vector3(dx, 0, dz);
                Gizmos.DrawSphere(pos, 0.3f);
                Gizmos.DrawLine(basePos, pos);

                // 이동 방향 표시
                Vector3 direction = new Vector3(
                    Mathf.Sin(user.currentHeading * Mathf.Deg2Rad),
                    0,
                    Mathf.Cos(user.currentHeading * Mathf.Deg2Rad)
                );
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(pos, direction * 2f);
                Gizmos.color = Color.cyan;
            }
        }
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(P2PVirtualUserTester))]
public class P2PVirtualUserTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        P2PVirtualUserTester tester = (P2PVirtualUserTester)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

        GUI.enabled = Application.isPlaying;

        EditorGUILayout.BeginHorizontal();

        if (!tester.isSimulating)
        {
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Start Virtual Users", GUILayout.Height(30)))
            {
                tester.StartSimulation();
            }
        }
        else
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Stop Virtual Users", GUILayout.Height(30)))
            {
                tester.StopSimulation();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        // 상태 표시
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

        string status = tester.isSimulating ? "시뮬레이션 중" : "대기 중";
        EditorGUILayout.LabelField("상태:", status);
        EditorGUILayout.LabelField("가상 사용자:", $"{tester.virtualUsers.Count}명");

        if (tester.isSimulating)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Virtual User Positions", EditorStyles.boldLabel);

            foreach (var user in tester.virtualUsers)
            {
                float dist = (float)CalculateDistance(
                    tester.baseLatitude, tester.baseLongitude,
                    user.currentLat, user.currentLon
                );
                EditorGUILayout.LabelField($"{user.username}:", $"{dist:F1}m away, heading {user.currentHeading:F0}°");
            }

            // 지속적 업데이트
            Repaint();
        }
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double EARTH_RADIUS = 6371000.0;

        double lat1Rad = lat1 * Mathf.Deg2Rad;
        double lat2Rad = lat2 * Mathf.Deg2Rad;
        double deltaLat = (lat2 - lat1) * Mathf.Deg2Rad;
        double deltaLon = (lon2 - lon1) * Mathf.Deg2Rad;

        double a = System.Math.Sin(deltaLat / 2) * System.Math.Sin(deltaLat / 2) +
                   System.Math.Cos(lat1Rad) * System.Math.Cos(lat2Rad) *
                   System.Math.Sin(deltaLon / 2) * System.Math.Sin(deltaLon / 2);

        double c = 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));

        return EARTH_RADIUS * c;
    }
}
#endif
