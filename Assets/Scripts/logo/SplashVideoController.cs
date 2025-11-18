using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

public class SplashVideoController : MonoBehaviour
{
    public VideoPlayer videoPlayer;  // VideoPlayer 컴포넌트
    public RawImage videoRawImage;  // 비디오를 표시할 RawImage
    public float fadeDuration = 1.5f;  // 페이드아웃 지속 시간
    public float videoDelay = 2.0f;    // 비디오 재생 후 대기 시간 (선택)

    void Start()
    {
        // 컴포넌트 초기화 및 설정
        if (videoPlayer == null || videoRawImage == null)
        {
            return;
        }

        // VideoPlayer 설정
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.APIOnly; // RawImage로 렌더링
        videoPlayer.isLooping = false; // 루프 비활성화
        videoPlayer.targetTexture = null; // RawImage에 직접 연결
        videoPlayer.Prepare();

        // 비디오 준비 완료 후 재생
        videoPlayer.prepareCompleted += (source) =>
        {
            videoPlayer.Play();
            videoRawImage.texture = videoPlayer.texture; // RawImage에 텍스처 연결
            videoRawImage.enabled = true; // RawImage 활성화
            videoRawImage.color = new Color(1, 1, 1, 1); // 초기 알파값 1
            StartCoroutine(CheckVideoEnd());
        };

        videoPlayer.errorReceived += (source, message) =>
        {
        };

        videoPlayer.loopPointReached += OnVideoFinished; // 비디오 끝 감지
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        StartCoroutine(FadeOutAndStop());
    }

    IEnumerator CheckVideoEnd()
    {
        float startTime = Time.time;
        float videoLength = (float)videoPlayer.length;

        while (videoPlayer.isPlaying)
        {
            if (Time.time - startTime > videoLength + 1.0f) // 비디오 길이 + 1초 이상 재생 시 강제 종료
            {
                videoPlayer.Stop();
                break;
            }
            yield return null;
        }

        yield return new WaitForSeconds(videoDelay); // 추가 대기 시간
        StartCoroutine(FadeOutAndStop());
    }

    IEnumerator FadeOutAndStop()
    {
        float t = 0;
        Color c = videoRawImage.color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(1 - t / fadeDuration); // 알파 값을 점진적으로 줄임
            videoRawImage.color = new Color(c.r, c.g, c.b, a);
            yield return null; // 프레임 단위로 진행
        }

        // 페이드아웃 완료 확인
        videoRawImage.color = new Color(c.r, c.g, c.b, 0); // 알파값 0으로 명시적 설정
        videoRawImage.enabled = false;
        videoPlayer.Stop(); // 비디오 정지

        // 단일 씬 내에서 대기 (추가 로직 필요 시 수정)
        while (true)
        {
            yield return null;
        }
    }
}