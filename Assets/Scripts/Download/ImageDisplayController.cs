using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ImageDisplayController : MonoBehaviour
{
    public Renderer cubeRenderer;
    public DoubleTap3D doubleTap3DScript;

    [Header("텍스처 패딩 설정")]
    [Tooltip("텍스처 패딩 값 (0 = 패딩 없음, 0.05 = 5% 패딩)")]
    [Range(0f, 0.2f)]
    public float texturePadding = 0.05f;

    [Tooltip("패딩 영역 색상")]
    public Color paddingColor = new Color(0.05f, 0.05f, 0.05f, 1f);

    private Texture2D baseMapTexture;
    private string baseMapUrl;   // 지금 면에 붙어 있는 사진 주소 — 같은 장소 정보를 다시 받아도 또 받지 않는다
    private Texture defaultFaceTexture;   // 프리팹에 원래 붙어 있던 면 — 사진을 지울 때 이걸로 되돌린다 (공유 자산이라 지우지 않는다)

    private Coroutine currentBaseMapCoroutine;
    private string wantedUrl;   // 이 면에 붙이려던 사진 — 받는 도중 오브젝트가 꺼지면 코루틴이 죽으므로 다시 켜질 때 이어 받는다 (10-29)

    // 세대 번호: 풀 재활용 시 증가 → 이전 코루틴이 yield 후 자신의 세대와 다르면 즉시 종료
    private int loadGeneration = 0;

    void Awake()
    {
        if (cubeRenderer == null || cubeRenderer.sharedMaterial == null) return;
        Material m = cubeRenderer.sharedMaterial;
        if (m.HasProperty("_BaseMap")) defaultFaceTexture = m.GetTexture("_BaseMap");
        else if (m.HasProperty("_MainTex")) defaultFaceTexture = m.GetTexture("_MainTex");
    }

    void Start()
    {
        // GLB 모델 등 동적으로 렌더러가 생성되는 경우를 위해 에러 처리 완화

        if (doubleTap3DScript == null)
        {
            Debug.LogError("[ImageDisplayController] doubleTap3DScript가 할당되지 않았습니다 - Sample_Prefab에 DoubleTap3D 추가 필요!");
        }
    }

    void OnEnable()
    {
        // 거리·필터 때문에 받는 도중 꺼졌다 켜지면 사진 없이 기본 면(프리팹 그대로)이 남던 것
        if (!string.IsNullOrEmpty(wantedUrl) && (baseMapTexture == null || baseMapUrl != wantedUrl))
        {
            if (currentBaseMapCoroutine != null) { StopCoroutine(currentBaseMapCoroutine); currentBaseMapCoroutine = null; }
            currentBaseMapCoroutine = StartCoroutine(LoadBaseMapTexture(wantedUrl, 0));
        }
    }

    // 메인 사진 설정
    public void SetBaseMap(string imageUrl)
    {
        if (!enabled) return;
        wantedUrl = imageUrl;
        if (imageUrl == baseMapUrl && baseMapTexture != null && currentBaseMapCoroutine == null) return;

        // 이전 BaseMap 코루틴 중단
        if (currentBaseMapCoroutine != null)
        {
            StopCoroutine(currentBaseMapCoroutine);
            currentBaseMapCoroutine = null;
        }

        // 큐브 숨기기 (로딩 중)
        if (cubeRenderer != null)
        {
            cubeRenderer.enabled = false;
        }

        if (!gameObject.activeInHierarchy) { currentBaseMapCoroutine = null; return; }   // 꺼진 채로는 받을 수 없다 — 켜질 때 OnEnable 이 받는다
        currentBaseMapCoroutine = StartCoroutine(LoadBaseMapTexture(imageUrl, 0));
    }

    /// <summary>
    /// 앱 내장 이미지(Resources)로 베이스맵 즉시 설정 — 서버 다운로드 없음.
    /// 공공데이터 심볼처럼 종류가 한정된 이미지에 사용. 경로에 텍스처가 없으면 false 반환
    /// (호출측이 서버 main_photo로 폴백할 수 있게).
    /// ⚠️ Resources 텍스처는 공유 자산이므로 절대 Destroy 하지 않음 → baseMapTexture에 담지 않음.
    /// </summary>
    public bool SetBaseMapFromResources(string resourcePath)
    {
        if (!enabled) return false;

        Texture2D tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null) return false;

        // 진행 중인 서버 로딩 중단 + 세대 증가(yield 후 돌아온 이전 코루틴 무효화)
        if (currentBaseMapCoroutine != null)
        {
            StopCoroutine(currentBaseMapCoroutine);
            currentBaseMapCoroutine = null;
        }
        loadGeneration++;
        wantedUrl = null;

        // 이전에 다운로드해 둔 동적 텍스처만 정리. 내장(공유) 텍스처는 소유하지 않으므로 참조만.
        if (baseMapTexture != null && baseMapTexture != tex) Destroy(baseMapTexture);
        baseMapTexture = null;
        baseMapUrl = null;

        if (cubeRenderer != null)
        {
            if (cubeRenderer.material.HasProperty("_BaseMap")) cubeRenderer.material.SetTexture("_BaseMap", tex);
            else if (cubeRenderer.material.HasProperty("_MainTex")) cubeRenderer.material.SetTexture("_MainTex", tex);
            ApplyPaddingSettings();
            cubeRenderer.enabled = true;
        }
        return true;
    }

    private IEnumerator LoadBaseMapTexture(string imageUrl, int attempt)
    {
        int myGeneration = loadGeneration;
        string fullUrl = imageUrl.StartsWith("http") ? imageUrl : ApiConfig.MAIN_SERVER + "/" + imageUrl.Replace("\\", "/");

        // 큐브는 화면에서 작게 보이므로 우팡 서버 사진은 512px 썸네일로 받는다 (원본 최대 2048px → 약 1/20 크기).
        // 썸네일이 실패하면 원본으로 한 번 더 받는다. 외부 사진(관광공사 등 http)은 원본 그대로.
        if (!imageUrl.StartsWith("http"))
        {
            string thumbUrl = ApiConfig.MAIN_SERVER + "/thumb/512/" + imageUrl.Replace("\\", "/").TrimStart('/');
            bool thumbOk = false;
            using (UnityWebRequest tr = UnityWebRequestTexture.GetTexture(thumbUrl, true))   // 읽기 전용 — CPU 쪽 사본을 남기지 않는다
            {
                tr.timeout = 20;
                yield return tr.SendWebRequest();
                if (myGeneration != loadGeneration) yield break;
                if (tr.result == UnityWebRequest.Result.Success)
                {
                    Texture2D t = ((DownloadHandlerTexture)tr.downloadHandler).texture;
                    if (t != null)
                    {
                        thumbOk = true;
                        ApplyBaseMap(t, imageUrl);
                    }
                }
            }
            if (thumbOk)
            {
                currentBaseMapCoroutine = null;
                yield break;
            }
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl, true))
        {
            request.timeout = 20;

            yield return request.SendWebRequest();

            // 세대 체크: 풀 재활용으로 이미 다른 장소가 되었으면 무시
            if (myGeneration != loadGeneration) yield break;

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D newTexture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                if (newTexture != null) ApplyBaseMap(newTexture, imageUrl);
            }
            else
            {
                Debug.LogError($"[ImageDisplayController] 로딩 실패: {request.error} ({fullUrl})");

                // 로딩 실패 시에도 큐브 표시 (눌러서 열 수 있게) — 사진은 뒤에서 다시 받아 바꿔 끼운다
                if (cubeRenderer != null)
                {
                    cubeRenderer.enabled = true;
                }
                if (attempt < 3)
                {
                    yield return new WaitForSeconds(4f * (attempt + 1));
                    if (myGeneration != loadGeneration) yield break;
                    yield return LoadBaseMapTexture(imageUrl, attempt + 1);
                    yield break;
                }
            }
        }
        currentBaseMapCoroutine = null;
    }

    private void ApplyBaseMap(Texture2D newTexture, string imageUrl)
    {
        if (baseMapTexture != null) Destroy(baseMapTexture);
        baseMapTexture = newTexture;
        baseMapUrl = imageUrl;
        if (cubeRenderer == null) return;

        if (cubeRenderer.material.HasProperty("_BaseMap")) cubeRenderer.material.SetTexture("_BaseMap", baseMapTexture);
        else if (cubeRenderer.material.HasProperty("_MainTex")) cubeRenderer.material.SetTexture("_MainTex", baseMapTexture);

        // 패딩 설정 적용
        ApplyPaddingSettings();

        // 큐브 표시
        cubeRenderer.enabled = true;
    }

    // 서브 사진 설정
    public void SetSubPhotos(List<string> subPhotoUrls)
    {
        if (!enabled) return;
        if (doubleTap3DScript != null) doubleTap3DScript.SetImageUrls(subPhotoUrls);
    }

    /// <summary>
    /// 진행 중인 로딩 코루틴 전부 중단 + 세대 번호 증가 (풀 재사용 전 호출)
    /// 세대 번호가 바뀌므로 yield 후 돌아온 이전 코루틴도 자동 무효화됨
    /// </summary>
    public void CancelPendingLoads()
    {
        wantedUrl = null;
        loadGeneration++; // 세대 증가 → yield 후 돌아온 이전 코루틴 무효화
        StopAllCoroutines();
        currentBaseMapCoroutine = null;
    }

    // 모든 텍스처 해제 — 풀로 돌아갈 때 · 파괴될 때
    public void ClearImages()
    {
        wantedUrl = null;
        loadGeneration++;
        StopAllCoroutines();
        currentBaseMapCoroutine = null;

        if (cubeRenderer != null)
        {
            if (cubeRenderer.material.HasProperty("_BaseMap"))
                cubeRenderer.material.SetTexture("_BaseMap", defaultFaceTexture);
            if (cubeRenderer.material.HasProperty("_MainTex"))
                cubeRenderer.material.SetTexture("_MainTex", defaultFaceTexture);
        }

        if (baseMapTexture != null && baseMapTexture != Texture2D.blackTexture)
        {
            Destroy(baseMapTexture);
            baseMapTexture = null;
        }
        baseMapUrl = null;

        if (doubleTap3DScript != null) doubleTap3DScript.ReleasePhotos();
    }

    void OnDestroy()
    {
        ClearImages(); // 컴포넌트 파괴 시 메모리 정리
    }

    // 패딩 설정을 머티리얼에 적용
    private void ApplyPaddingSettings()
    {
        if (cubeRenderer == null || cubeRenderer.material == null) return;

        Material mat = cubeRenderer.material;

        // 텍스처 패딩 값 적용
        if (mat.HasProperty("_TexturePadding"))
        {
            mat.SetFloat("_TexturePadding", texturePadding);
        }

        // 패딩 영역 색상 적용
        if (mat.HasProperty("_PaddingColor"))
        {
            mat.SetColor("_PaddingColor", paddingColor);
        }
    }

    // 런타임에서 패딩 값 변경
    public void SetTexturePadding(float padding)
    {
        texturePadding = Mathf.Clamp(padding, 0f, 0.2f);
        ApplyPaddingSettings();
    }

    // 런타임에서 패딩 색상 변경
    public void SetPaddingColor(Color color)
    {
        paddingColor = color;
        ApplyPaddingSettings();
    }
}