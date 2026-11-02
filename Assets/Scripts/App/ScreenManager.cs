using System.Collections;
using UnityEngine;

public class ScreenManager : MonoBehaviour
{
   [SerializeField] private float keepAwakeTime = 60f; // 화면 유지 시간 (초)
   
   void Start()
   {
       // 화면 꺼짐 방지
       Screen.sleepTimeout = SleepTimeout.NeverSleep;
       
       // 일정 시간 후 원래 설정으로 복원
       StartCoroutine(RestoreScreenTimeout());
   }
   
   IEnumerator RestoreScreenTimeout()
   {
       yield return new WaitForSeconds(keepAwakeTime);
       
       // 시스템 기본값으로 복원
       Screen.sleepTimeout = SleepTimeout.SystemSetting;
   }
   
   void OnApplicationPause(bool pauseStatus)
   {
       if (!pauseStatus) // 앱이 다시 활성화될 때
       {
           Screen.sleepTimeout = SleepTimeout.NeverSleep;
           StopAllCoroutines();
           StartCoroutine(RestoreScreenTimeout());
       }
   }
   
   void OnApplicationFocus(bool hasFocus)
   {
       if (hasFocus) // 앱이 포커스를 받을 때
       {
           Screen.sleepTimeout = SleepTimeout.NeverSleep;
           StopAllCoroutines();
           StartCoroutine(RestoreScreenTimeout());
       }
   }
}