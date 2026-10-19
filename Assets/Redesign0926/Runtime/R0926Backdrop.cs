using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// 카메라 화면(AR 배경)의 밝기를 화면 자리별로 잰다 — 인디케이터 둘레 그림자를 밝은 곳에선 어둡게, 어두운 곳에선 밝게 고르려고 (R0926IndicatorHalo).
/// AR 배경 재질로 48×48 작은 그림을 그려 GPU 에서 비동기로 읽는다 (0.3초마다 · 9KB) — 메인 스레드를 기다리게 하지 않는다.
/// 처음 Luma 를 부를 때 저절로 생긴다. 모르면(에디터·AR 꺼짐·기기 미지원) -1.
/// </summary>
public class R0926Backdrop : MonoBehaviour
{
    private const int N = 48;
    private const float Interval = 0.3f;
    private const float Stale = 2f;   // 이보다 오래된 측정은 버린다 (AR 이 멈춘 동안)

    private static R0926Backdrop inst;
    private static readonly byte[] grid = new byte[N * N];
    private static float gridTime = -100f;

    private ARCameraBackground bg;
    private RenderTexture rt;
    private Material passFor;
    private int pass = -1;
    private float nextShot, nextFind;
    private bool pending;

    /// <summary>화면 자리(픽셀, 왼쪽 아래 기준) 둘레의 배경 밝기 0~1 — 모르면 -1</summary>
    public static float Luma(Vector2 screenPos)
    {
        if (inst == null) inst = new GameObject("Backdrop0926").AddComponent<R0926Backdrop>();
        if (Time.unscaledTime - gridTime > Stale) return -1f;
        int cx = Mathf.Clamp((int)(screenPos.x / Mathf.Max(1, Screen.width) * N), 0, N - 1);
        int cy = Mathf.Clamp((int)(screenPos.y / Mathf.Max(1, Screen.height) * N), 0, N - 1);
        int sum = 0, cnt = 0;
        for (int y = Mathf.Max(0, cy - 1); y <= Mathf.Min(N - 1, cy + 1); y++)
            for (int x = Mathf.Max(0, cx - 1); x <= Mathf.Min(N - 1, cx + 1); x++)
            {
                sum += grid[y * N + x];
                cnt++;
            }
        return sum / (255f * cnt);
    }

    private void Update()
    {
        if (pending || Time.unscaledTime < nextShot) return;
        nextShot = Time.unscaledTime + Interval;
        if (!SystemInfo.supportsAsyncGPUReadback) { enabled = false; return; }
        if (bg == null)
        {
            if (Time.unscaledTime < nextFind) return;
            nextFind = Time.unscaledTime + 3f;
            bg = FindFirstObjectByType<ARCameraBackground>();
            if (bg == null) return;
        }
        if (!bg.isActiveAndEnabled || !bg.backgroundRenderingEnabled) return;
        Material mat = bg.material;
        if (mat == null) return;
        if (rt == null)
        {
            // sRGB — 밝기를 눈에 보이는 대로 (선형 색공간 프로젝트)
            rt = new RenderTexture(N, N, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Backdrop0926" };
            rt.Create();
        }
        if (mat != passFor)
        {
            passFor = mat;
            pass = FindPass(mat);
        }
        Graphics.Blit(null, rt, mat, pass);
        pending = true;
        AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, OnRead);
    }

    // 카메라 그림을 그리는 패스 — 가림(occlusion) 처리 패스는 건너뛴다. 이름 없는 한 패스짜리면 -1(전부)
    private static int FindPass(Material m)
    {
        for (int i = 0; i < m.passCount; i++)
            if (m.GetPassName(i).Contains("Camera Background")) return i;
        return -1;
    }

    private void OnRead(AsyncGPUReadbackRequest req)
    {
        pending = false;
        if (this == null || req.hasError) return;
        var data = req.GetData<byte>();
        if (data.Length < N * N * 4) return;
        for (int i = 0; i < N * N; i++)
        {
            int o = i * 4;
            grid[i] = (byte)((data[o] * 54 + data[o + 1] * 183 + data[o + 2] * 19) >> 8);   // 줄 0 = 화면 아래
        }
        gridTime = Time.unscaledTime;
    }

    private void OnDestroy()
    {
        if (inst == this) inst = null;
        gridTime = -100f;
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }
}
