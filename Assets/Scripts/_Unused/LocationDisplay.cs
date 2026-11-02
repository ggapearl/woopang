using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GPSDisplay : MonoBehaviour
{
    public Text gpsText; // UI Text 연결
    private bool isGPSEnabled = false;

    void Start()
    {
        StartCoroutine(StartGPS());
    }

    IEnumerator StartGPS()
    {
        if (!Input.location.isEnabledByUser)
        {
            gpsText.text = "GPS 꺼짐";
            yield break;
        }

        Input.location.Start();
        int maxWait = 20; // 최대 20초 대기
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (maxWait <= 0 || Input.location.status == LocationServiceStatus.Failed)
        {
            gpsText.text = "GPS 오류";
            yield break;
        }

        isGPSEnabled = true;
    }

    void Update()
    {
        if (isGPSEnabled)
        {
            double latitude = System.Math.Round(Input.location.lastData.latitude, 4);
            double longitude = System.Math.Round(Input.location.lastData.longitude, 4);
            double altitude = System.Math.Round(Input.location.lastData.altitude, 2);

            // 한 칸 띄우고 UI에 표시 (고도는 소수점 2자리까지)
            gpsText.text = $"{latitude}  {longitude}  {altitude:F2}";
        }
    }
}
