using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CaptureAndTranslate : MonoBehaviour
{
    public Image flashImage;
    public RectTransform captureArea;

    public void OnTranslateButtonClick()
    {
        StartCoroutine(CaptureWithZoomAndFlash());
    }

    private IEnumerator CaptureWithZoomAndFlash()
    {
        // 1. 화면 살짝 확대 애니메이션
        if (captureArea != null)
        {
            Vector3 originalScale = captureArea.localScale;
            float zoomDuration = 0.1f;
            float zoomScale = 1.1f;

            for (float t = 0; t < zoomDuration; t += Time.deltaTime)
            {
                captureArea.localScale = Vector3.Lerp(originalScale, originalScale * zoomScale, t / zoomDuration);
                yield return null;
            }

            yield return new WaitForSeconds(0.1f);

            for (float t = 0; t < zoomDuration; t += Time.deltaTime)
            {
                captureArea.localScale = Vector3.Lerp(originalScale * zoomScale, originalScale, t / zoomDuration);
                yield return null;
            }
            captureArea.localScale = originalScale;
        }

        // 2. 플래시 효과
        if (flashImage != null)
        {
            flashImage.gameObject.SetActive(true);
            flashImage.color = new Color(1, 1, 1, 0.8f);
            yield return new WaitForSeconds(0.2f);
            flashImage.color = new Color(1, 1, 1, 0);
        }

        // 3. 화면 캡처
        yield return new WaitForEndOfFrame();
        Texture2D screenImage;
        Rect captureRect;

        if (captureArea != null)
        {
            Vector2 size = captureArea.sizeDelta;
            Vector2 position = captureArea.position;
            int width = (int)size.x;
            int height = (int)size.y;
            int x = (int)(position.x - width / 2);
            int y = (int)(position.y - height / 2);

            x = Mathf.Clamp(x, 0, Screen.width - width);
            y = Mathf.Clamp(y, 0, Screen.height - height);
            width = Mathf.Min(width, Screen.width - x);
            height = Mathf.Min(height, Screen.height - y);

            captureRect = new Rect(x, y, width, height);
            screenImage = new Texture2D(width, height, TextureFormat.RGB24, false);
        }
        else
        {
            captureRect = new Rect(0, 0, Screen.width, Screen.height);
            screenImage = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        }

        screenImage.ReadPixels(captureRect, 0, 0);
        screenImage.Apply();

        // 4. 사진첩에 저장 — NativeGallery 가 안드로이드 10+ 저장 방식·iOS 사진 권한을 처리한다
        //    (예전엔 안드로이드는 /storage/emulated/0/Pictures 에 직접 쓰고, iOS 는 앱 내부 폴더에 저장해
        //     번역 앱에서 사진을 고를 수 없었다)
        string fileName = "Woopang_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        byte[] bytes = screenImage.EncodeToPNG();
        Destroy(screenImage);
        bool done = false;
        NativeGallery.SaveImageToGallery(bytes, "WOOPANG", fileName, (success, path) => done = true);
        float wait = 0f;
        while (!done && wait < 3f) { wait += Time.unscaledDeltaTime; yield return null; }

        // 5. 구글 번역 이미지 모드 — 기기 언어로 번역 (저장에 실패해도 번역 화면의 카메라로 쓸 수 있게 연다)
        string url = "https://translate.google.com/?sl=auto&tl=" + TargetLanguage() + "&op=images";
        Application.OpenURL(url);
    }

    private static string TargetLanguage()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified: return "zh-CN";
            case SystemLanguage.ChineseTraditional: return "zh-TW";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.German: return "de";
            case SystemLanguage.Vietnamese: return "vi";
            case SystemLanguage.Thai: return "th";
            case SystemLanguage.Indonesian: return "id";
            default: return "en";
        }
    }
}