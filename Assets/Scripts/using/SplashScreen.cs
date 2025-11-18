using UnityEngine;
using UnityEngine.UI; // Image 컴포넌트 사용을 위해 필요
using System.Collections;

public class SplashScreen : MonoBehaviour
{
    [SerializeField] // Inspector에서 보이도록 설정
    private Image splashImage; // Image 컴포넌트 참조

    [SerializeField] // Inspector에서 조절 가능하도록 설정
    private float waitTime = 5f; // 기본값 5초, Inspector에서 변경 가능

    [SerializeField] // Inspector에서 조절 가능하도록 설정
    private float fadeDuration = 1f; // 페이드아웃 시간, 기본값 1초

    void Start()
    {
        if (splashImage == null)
        {
            Debug.LogError("SplashImage가 할당되지 않았습니다. Inspector에서 확인해주세요.");
            return;
        }

        // 처음에 완전히 보이도록 설정
        Color color = splashImage.color;
        color.a = 1f;
        splashImage.color = color;

        StartCoroutine(ShowSplashScreen());
    }

    IEnumerator ShowSplashScreen()
    {
        // Inspector에서 설정한 시간만큼 이미지 유지
        yield return new WaitForSeconds(waitTime);

        // 페이드아웃 시작
        float timer = 0f;
        Color color = splashImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            splashImage.color = color;
            yield return null;
        }

        // 완전히 사라진 후 오브젝트 비활성화
        splashImage.gameObject.SetActive(false);
    }
}