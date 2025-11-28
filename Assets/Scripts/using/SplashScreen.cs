using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 앱 시작 시 전체화면 스플래시를 표시하는 컴포넌트
/// SplashScene에서 3.5초 표시 후 페이드아웃하며 다음 씬으로 전환
/// </summary>
public class SplashScreen : MonoBehaviour
{
    [SerializeField]
    private Image splashImage;

    [SerializeField]
    private float waitTime = 3.5f; // 스플래시 표시 시간 (초)

    [SerializeField]
    private float fadeDuration = 0.5f; // 페이드아웃 시간 (초)

    void Start()
    {
        if (splashImage == null)
        {
            Debug.LogError("SplashImage가 할당되지 않았습니다. Inspector에서 확인해주세요.");
            // 스플래시 없이 다음 씬으로 이동
            LoadNextScene();
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

        // 페이드 완료 후 다음 씬으로 이동
        LoadNextScene();
    }

    void LoadNextScene()
    {
        // Build Settings의 다음 씬으로 이동
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.LogError("다음 씬이 Build Settings에 없습니다!");
        }
    }
}
