using UnityEngine;

/// <summary>
/// Update() 메서드 성능 최적화를 위한 Throttle 헬퍼 클래스
/// 매 프레임 대신 지정된 간격마다만 코드를 실행하여 CPU 사용량 감소
/// </summary>
public class UpdateThrottle
{
    private float interval;
    private float lastUpdateTime;

    /// <summary>
    /// UpdateThrottle 인스턴스 생성
    /// </summary>
    /// <param name="intervalInSeconds">업데이트 간격 (초)</param>
    public UpdateThrottle(float intervalInSeconds)
    {
        interval = intervalInSeconds;
        lastUpdateTime = -intervalInSeconds; // 첫 실행 즉시 허용
    }

    /// <summary>
    /// 업데이트 가능 여부 확인 (간격이 지났으면 true 반환)
    /// </summary>
    public bool ShouldUpdate()
    {
        float currentTime = Time.time;
        if (currentTime - lastUpdateTime >= interval)
        {
            lastUpdateTime = currentTime;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 업데이트 간격 변경
    /// </summary>
    public void SetInterval(float intervalInSeconds)
    {
        interval = intervalInSeconds;
    }

    /// <summary>
    /// 마지막 업데이트 시간 리셋 (즉시 실행 허용)
    /// </summary>
    public void Reset()
    {
        lastUpdateTime = -interval;
    }
}
