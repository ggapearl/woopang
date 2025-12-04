using UnityEngine;

/// <summary>
/// 오브젝트 생성 시 자동으로 SparkleEffect 재생
/// Sample_Prefab, GLB_Prefab 등에 추가
/// </summary>
public class SparkleOnSpawn : MonoBehaviour
{
    [Header("Sparkle Effect Reference")]
    [Tooltip("SparkleEffect 컴포넌트 (없으면 자동 추가)")]
    public SparkleEffect sparkleEffect;

    [Header("Auto Play Settings")]
    [Tooltip("생성 시 자동 재생")]
    public bool playOnEnable = true;

    void OnEnable()
    {
        if (playOnEnable)
        {
            PlaySparkle();
        }
    }

    public void PlaySparkle()
    {
        // SparkleEffect가 없으면 추가
        if (sparkleEffect == null)
        {
            sparkleEffect = GetComponent<SparkleEffect>();
            if (sparkleEffect == null)
            {
                sparkleEffect = gameObject.AddComponent<SparkleEffect>();

                // circle.png 로드 시도
                Sprite circleSprite = Resources.Load<Sprite>("UI/circle");
                if (circleSprite == null)
                {
                    // 경로가 다를 수 있으니 다시 시도
                    circleSprite = Resources.Load<Sprite>("sou/UI/circle");
                }

                if (circleSprite != null)
                {
                    sparkleEffect.sparkleSprite = circleSprite;
                }
                else
                {
                    Debug.LogWarning("[SparkleOnSpawn] circle.png를 찾을 수 없습니다. Resources 폴더에 있는지 확인하세요.");
                }
            }
        }

        // 반짝임 재생
        sparkleEffect.PlaySparkle3D();
    }
}
