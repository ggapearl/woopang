using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;

public class CaptureAndTranslate : MonoBehaviour
{
    public Image flashImage;
    public RectTransform captureArea;

    void Start()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        RequestStoragePermission();
        #endif
    }

    #if UNITY_ANDROID && !UNITY_EDITOR
    private void RequestStoragePermission()
    {
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.WRITE_EXTERNAL_STORAGE"))
        {
            UnityEngine.Android.Permission.RequestUserPermission("android.permission.WRITE_EXTERNAL_STORAGE");
        }
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.READ_EXTERNAL_STORAGE"))
        {
            UnityEngine.Android.Permission.RequestUserPermission("android.permission.READ_EXTERNAL_STORAGE");
        }
    }
    #endif

    public void OnTranslateButtonClick()
    {
        StartCoroutine(CaptureWithZoomAndFlash());
    }

    private IEnumerator CaptureWithZoomAndFlash()
    {
        // 1. ȭ�� ��¦ Ȯ�� �ִϸ��̼�
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

        // 2. �÷��� ȿ��
        if (flashImage != null)
        {
            flashImage.gameObject.SetActive(true);
            flashImage.color = new Color(1, 1, 1, 0.8f);
            yield return new WaitForSeconds(0.2f);
            flashImage.color = new Color(1, 1, 1, 0);
        }

        // 3. ȭ�� ĸó
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

        // 4. ������ Pictures ������ ����
        string fileName = "CapturedImage_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        string galleryPath;

        #if UNITY_ANDROID && !UNITY_EDITOR
        galleryPath = Path.Combine("/storage/emulated/0/Pictures", fileName);
        #else
        galleryPath = Path.Combine(Application.persistentDataPath, fileName);
        #endif

        byte[] bytes = screenImage.EncodeToPNG();

        try
        {
            string directory = Path.GetDirectoryName(galleryPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(galleryPath, bytes);

            // 5. ������ ���ΰ�ħ
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaObject context = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (AndroidJavaClass mediaScanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
                    {
                        mediaScanner.CallStatic("scanFile", context, new string[] { galleryPath }, new string[] { "image/png" }, null);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("������ ���ΰ�ħ ����: " + e.Message);
            }
            #endif
        }
        catch (System.Exception e)
        {
            Debug.LogError("������ ���� ����: " + e.Message);
            string fallbackPath = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(fallbackPath, bytes);
            yield break;
        }

        // 6. ���� ���� �������� �̵�
        string url = "https://translate.google.com/?sl=auto&tl=ko&op=images";
        Application.OpenURL(url);
    }
}