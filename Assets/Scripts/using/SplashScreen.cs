using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SplashScreen : MonoBehaviour
{
    [SerializeField]
    private Image splashImage;

    [SerializeField]
    private float waitTime = 5f; // 스플래시 표시 시간 (초)

    [SerializeField]
    private float fadeDuration = 1f; // 페이드아웃 시간 (초)

    [SerializeField]
    private string nextSceneName = "MainScene"; // 다음 씬 이름

    void Start()
    {
        if (splashImage == null)
        {
            Debug.LogError("SplashImage가 할당되지 않았습니다. Inspector에서 확인해주세요.");
            return;
        }

        // 처음에 이미지를 불투명하게 설정
        Color color = splashImage.color;
        color.a = 1f;
        splashImage.color = color;

        StartCoroutine(ShowSplashScreen());
    }

    IEnumerator ShowSplashScreen()
    {
        // 지정된 시간만큼 이미지 표시
        yield return new WaitForSeconds(waitTime);

        // 페이드아웃 효과
        float timer = 0f;
        Color color = splashImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            splashImage.color = color;
            yield return null;
        }

        // 페이드 완료 후 오브젝트 비활성화
        splashImage.gameObject.SetActive(false);

        // 다음 씬으로 이동
        SceneManager.LoadScene(nextSceneName);
    }
}
