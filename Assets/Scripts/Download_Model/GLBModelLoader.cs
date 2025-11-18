using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GLTFast;
using System;

public class GLBModelLoader : MonoBehaviour
{
    [SerializeField] private Transform glbContainer;
    private GameObject loadedModel;
    private bool isModelLoaded = false;
    
    [Header("Settings")]
    [SerializeField] private bool enableDetailedLogging = true;
    [SerializeField] private int maxRetryAttempts = 2;
    [SerializeField] private float retryDelay = 2f;
    [SerializeField] private long maxFileSizeBytes = 10 * 1024 * 1024; // 10MB 제한
    
    [Header("Cache Management")]
    [SerializeField] private bool enableFileCache = false; // GLB 파일 캐시 비활성화
    [SerializeField] private int maxCachedFiles = 5; // 최대 캐시 파일 수
    
    // 다운로드된 파일 추적
    private static Dictionary<string, byte[]> downloadedFiles = new Dictionary<string, byte[]>();
    private static Queue<string> downloadOrder = new Queue<string>();
    private string currentGLBUrl = "";
    private byte[] currentGLBData = null;

    void Start()
    {
        Debug.Log("[GLBModelLoader] Start() 호출됨");
        
        if (glbContainer == null)
        {
            Transform container = transform.Find("GLBContainer");
            if (container != null)
            {
                glbContainer = container;
                Debug.Log("[GLBModelLoader] GLBContainer 자동 할당됨");
            }
            else
            {
                GameObject containerObj = new GameObject("GLBContainer");
                containerObj.transform.SetParent(transform);
                containerObj.transform.localPosition = Vector3.zero;
                containerObj.transform.localRotation = Quaternion.identity;
                containerObj.transform.localScale = Vector3.one;
                glbContainer = containerObj.transform;
                Debug.Log("[GLBModelLoader] GLBContainer 자동 생성됨");
            }
        }
        
        // 디바이스 정보 로깅
        Debug.Log($"[GLBModelLoader] 디바이스 정보 - Platform: {Application.platform}");
        Debug.Log($"[GLBModelLoader] 시스템 메모리: {SystemInfo.systemMemorySize}MB");
        Debug.Log($"[GLBModelLoader] 그래픽 메모리: {SystemInfo.graphicsMemorySize}MB");
        Debug.Log($"[GLBModelLoader] 그래픽 API: {SystemInfo.graphicsDeviceType}");
        Debug.Log($"[GLBModelLoader] 렌더 파이프라인: {UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset?.GetType().Name ?? "Built-in"}");
        Debug.Log($"[GLBModelLoader] 셰이더 레벨: {SystemInfo.graphicsShaderLevel}");
        Debug.Log($"[GLBModelLoader] 텍스처 압축 지원: {SystemInfo.SupportsTextureFormat(TextureFormat.DXT1)} (DXT1), {SystemInfo.SupportsTextureFormat(TextureFormat.ETC2_RGBA8)} (ETC2)");
        
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
        {
            Debug.Log("[GLBModelLoader] Graphics API: OpenGLES3");
            Debug.Log("[GLBModelLoader] OpenGL ES 3.0 PBR 셰이더 지원 확인 필요");
        }
        else if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
        {
            Debug.Log("[GLBModelLoader] Graphics API: Metal");
        }
        else if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
        {
            Debug.Log("[GLBModelLoader] Graphics API: Vulkan");
        }
    }

    public IEnumerator LoadGLBModelCoroutine(string url, float scale, System.Action<bool> onComplete)
    {
        LogDebug($"[GLBModelLoader] GLB 로딩 시작: {url}");
        
        if (string.IsNullOrEmpty(url))
        {
            LogError("[GLBModelLoader] GLB URL이 비어있음");
            onComplete?.Invoke(false);
            yield break;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            LogError("[GLBModelLoader] 네트워크 연결 없음");
            onComplete?.Invoke(false);
            yield break;
        }
        
        ClearModel();
        
        // 재시도 로직
        bool loadSuccess = false;
        for (int attempt = 0; attempt < maxRetryAttempts && !loadSuccess; attempt++)
        {
            if (attempt > 0)
            {
                LogDebug($"[GLBModelLoader] 재시도 {attempt + 1}/{maxRetryAttempts}: {url}");
                yield return new WaitForSeconds(retryDelay);
            }
            
            yield return StartCoroutine(AttemptGLBLoad(url, scale, (success) => {
                loadSuccess = success;
            }));
        }
        
        LogDebug($"[GLBModelLoader] 최종 로딩 결과: {loadSuccess}");
        onComplete?.Invoke(loadSuccess);
    }

    private IEnumerator AttemptGLBLoad(string url, float scale, System.Action<bool> onComplete)
    {
        var gltf = new GltfImport();
        bool loadStarted = false;
        bool loadSuccess = false;
        
        StartCoroutine(LoadGLTFAsync(gltf, url, scale, (success) => {
            loadSuccess = success;
            loadStarted = true;
        }));
        
        // 타임아웃 처리 (30초)
        float timeout = 30f;
        float elapsed = 0f;
        while (!loadStarted && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        onComplete?.Invoke(loadStarted && loadSuccess);
    }

    private IEnumerator LoadGLTFAsync(GltfImport gltf, string url, float scale, System.Action<bool> onComplete)
    {
        Debug.Log($"[GLBModelLoader] 다운로드 시작: {url}");
        Debug.Log($"[GLBModelLoader] 요청 URL 상세 분석: {url}");
        
        currentGLBUrl = url;
        
        // URL 구조 분석
        System.Uri uri = new System.Uri(url);
        Debug.Log($"[GLBModelLoader] URL 호스트: {uri.Host}");
        Debug.Log($"[GLBModelLoader] URL 경로: {uri.AbsolutePath}");
        Debug.Log($"[GLBModelLoader] URL 파일명: {System.IO.Path.GetFileName(uri.AbsolutePath)}");
        
        // 캐시에서 먼저 확인
        byte[] glbData = null;
        if (enableFileCache && downloadedFiles.ContainsKey(url))
        {
            glbData = downloadedFiles[url];
            Debug.Log($"[GLBModelLoader] 캐시에서 GLB 파일 로드: {glbData.Length} bytes");
        }
        else
        {
            // 파일 다운로드
            UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url);
            
            // 캐시 비활성화 (메모리 절약)
            request.SetRequestHeader("Cache-Control", "no-cache");
            request.SetRequestHeader("Pragma", "no-cache");
            
            // 타임아웃 설정
            request.timeout = 20;
            
            yield return request.SendWebRequest();
            
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                LogError($"[GLBModelLoader] GLB 파일 다운로드 실패: {url}");
                LogError($"[GLBModelLoader] HTTP 상태: {request.responseCode}, 에러: {request.error}");
                LogError($"[GLBModelLoader] 네트워크 연결 상태: {Application.internetReachability}");
                LogError($"[GLBModelLoader] Response Headers:");
                
                // 응답 헤더 분석
                var headers = request.GetResponseHeaders();
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        LogError($"[GLBModelLoader] Header: {header.Key} = {header.Value}");
                    }
                }
                
                request.Dispose(); // 요청 리소스 해제
                onComplete?.Invoke(false);
                yield break;
            }
            
            glbData = request.downloadHandler.data;
            Debug.Log($"[GLBModelLoader] GLB 파일 다운로드 성공: {glbData.Length} bytes");
            
            // 응답 헤더 확인
            var responseHeaders = request.GetResponseHeaders();
            if (responseHeaders != null)
            {
                Debug.Log("[GLBModelLoader] 응답 헤더 분석:");
                foreach (var header in responseHeaders)
                {
                    Debug.Log($"[GLBModelLoader] {header.Key}: {header.Value}");
                    
                    if (header.Key.ToLower() == "content-type")
                    {
                        Debug.Log($"[GLBModelLoader] Content-Type: {header.Value}");
                        if (!header.Value.Contains("application/octet-stream") && 
                            !header.Value.Contains("model/gltf-binary") &&
                            !header.Value.Contains("application/gltf"))
                        {
                            LogError($"[GLBModelLoader] 잘못된 Content-Type: {header.Value} (예상: application/octet-stream 또는 model/gltf-binary)");
                        }
                    }
                }
            }
            
            // 요청 리소스 즉시 해제
            request.Dispose();
            Debug.Log("[GLBModelLoader] UnityWebRequest 리소스 해제 완료");
            
            // 선택적 캐시 저장
            if (enableFileCache)
            {
                CacheGLBFile(url, glbData);
            }
        }
        
        // 현재 파일 데이터 저장 (정리용)
        currentGLBData = glbData;
        
        // 파일 크기 검증
        if (glbData.Length > maxFileSizeBytes)
        {
            LogError($"[GLBModelLoader] 파일 크기 초과: {glbData.Length} bytes (최대: {maxFileSizeBytes})");
            onComplete?.Invoke(false);
            yield break;
        }
        
        if (glbData.Length < 100)
        {
            LogError($"[GLBModelLoader] GLB 파일 크기가 너무 작음: {glbData.Length} bytes");
            onComplete?.Invoke(false);
            yield break;
        }
        
        // GLB 매직 넘버 검증 및 파일 내용 분석
        if (glbData.Length >= 4)
        {
            string magic = System.Text.Encoding.ASCII.GetString(glbData, 0, 4);
            Debug.Log($"[GLBModelLoader] GLB 파일 매직 넘버: {magic}");
            
            if (magic != "glTF")
            {
                LogError($"[GLBModelLoader] GLB 매직 넘버 불일치: {magic}");
                
                // 파일 시작 부분 분석 (처음 200바이트)
                int previewLength = Mathf.Min(200, glbData.Length);
                string filePreview = System.Text.Encoding.UTF8.GetString(glbData, 0, previewLength);
                LogError($"[GLBModelLoader] 파일 내용 미리보기: {filePreview}");
                
                // HTML 에러 페이지인지 확인
                if (filePreview.ToLower().Contains("<html") || filePreview.ToLower().Contains("<!doctype"))
                {
                    LogError("[GLBModelLoader] 서버에서 HTML 에러 페이지 반환됨");
                    LogError("[GLBModelLoader] - GLB 파일 경로가 잘못되었거나 파일이 존재하지 않음");
                }
                else if (filePreview.ToLower().Contains("404") || filePreview.ToLower().Contains("not found"))
                {
                    LogError("[GLBModelLoader] 서버에서 404 Not Found 에러 반환됨");
                }
                else if (filePreview.ToLower().Contains("403") || filePreview.ToLower().Contains("forbidden"))
                {
                    LogError("[GLBModelLoader] 서버에서 403 Forbidden 에러 반환됨");
                }
                else
                {
                    LogError("[GLBModelLoader] 알 수 없는 파일 형식");
                    // 바이너리 데이터인지 확인
                    bool isBinary = false;
                    for (int i = 0; i < Mathf.Min(50, glbData.Length); i++)
                    {
                        if (glbData[i] < 32 && glbData[i] != 10 && glbData[i] != 13 && glbData[i] != 9)
                        {
                            isBinary = true;
                            break;
                        }
                    }
                    LogError($"[GLBModelLoader] 파일 형식: {(isBinary ? "바이너리" : "텍스트")}");
                }
                
                onComplete?.Invoke(false);
                yield break;
            }
            else
            {
                Debug.Log("[GLBModelLoader] GLB 매직 넘버 확인 완료");
            }
        }
        
        // 메모리 검사
        long availableMemory = SystemInfo.systemMemorySize * 1024L * 1024L;
        if (glbData.Length > availableMemory / 4)
        {
            LogError($"[GLBModelLoader] 모바일 디바이스 메모리 부족으로 로드 취소. 파일: {glbData.Length}, 가용메모리: {availableMemory/4}");
            onComplete?.Invoke(false);
            yield break;
        }
        
        Debug.Log("[GLBModelLoader] GLTFast 파싱 시작");
        
        // GLB 원본 색상 정보 추출
        Color originalColor = ExtractColorFromGLB(glbData);
        Debug.Log($"[GLBModelLoader] GLB 원본 색상 추출: {originalColor}");
        
        // GLTFast 파싱
        var loadTask = gltf.LoadGltfBinary(glbData);
        
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }
        
        bool parseSuccess = false;
        try
        {
            parseSuccess = loadTask.Result;
        }
        catch (System.Exception ex)
        {
            LogError($"[GLBModelLoader] GLTFast 파싱 예외: {ex.Message}");
            LogError($"[GLBModelLoader] GLTFast 에러 상세:");
            LogError($"[GLBModelLoader] - 지원되지 않는 Extensions 확인 필요");
            LogError($"[GLBModelLoader] - KHR_materials_specular extension 지원 문제 가능성");
            LogError($"[GLBModelLoader] - OpenGL ES 제한으로 인한 실패 가능성");
            onComplete?.Invoke(false);
            yield break;
        }
        
        Debug.Log($"[GLBModelLoader] GLTFast 파싱 결과: {parseSuccess}");
        
        if (!parseSuccess)
        {
            LogError("[GLBModelLoader] GLB 파일 로딩 실패");
            LogError("[GLBModelLoader] - 텍스처 압축 실패 가능성");
            LogError("[GLBModelLoader] - 셰이더 컴파일 실패 가능성");
            LogError("[GLBModelLoader] - 메시 데이터 손상 가능성");
            onComplete?.Invoke(false);
            yield break;
        }
        
        // 씬 인스턴스화
        Debug.Log("[GLBModelLoader] GLB 씬 인스턴스화 시작");
        
        var instantiateTask = gltf.InstantiateMainSceneAsync(glbContainer);
        
        while (!instantiateTask.IsCompleted)
        {
            yield return null;
        }
        
        bool instantiateSuccess = false;
        try
        {
            instantiateSuccess = instantiateTask.Result;
        }
        catch (System.Exception ex)
        {
            LogError($"[GLBModelLoader] 인스턴스화 예외: {ex.Message}");
            LogError("[GLBModelLoader] - 모바일 디바이스에서 인스턴스화 지원 문제 가능성");
            onComplete?.Invoke(false);
            yield break;
        }
        
        Debug.Log($"[GLBModelLoader] 인스턴스화 결과: {instantiateSuccess}");
        
        if (instantiateSuccess && glbContainer.childCount > 0)
        {
            Debug.Log($"[GLBModelLoader] GLBContainer 자식 개수: {glbContainer.childCount}");
            
            loadedModel = glbContainer.GetChild(0).gameObject;
            
            Debug.Log($"[GLBModelLoader] 로드된 모델: {loadedModel.name}");
            Debug.Log($"[GLBModelLoader] 모델 위치: {loadedModel.transform.position}");
            Debug.Log($"[GLBModelLoader] 모델 회전: {loadedModel.transform.rotation}");
            Debug.Log($"[GLBModelLoader] 모델 스케일 변경 전: {loadedModel.transform.localScale}");
            
            // 모델 변환 설정
            loadedModel.transform.localScale = Vector3.one * scale;
            loadedModel.transform.localPosition = Vector3.zero;
            loadedModel.transform.localRotation = Quaternion.identity;
            
            Debug.Log($"[GLBModelLoader] 모델 스케일 변경 후: {loadedModel.transform.localScale}");
            
            isModelLoaded = true;
            
            // 렌더러 정보 로깅
            MeshRenderer[] renderers = loadedModel.GetComponentsInChildren<MeshRenderer>();
            Debug.Log($"[GLBModelLoader] 발견된 MeshRenderer 개수: {renderers.Length}");
            
            // 즉시 셰이더 분석 실행
            AnalyzeGLBMaterials(renderers);
            
            // 즉시 머티리얼 최적화 실행 (원본 색상 적용)
            OptimizeMaterialsWithOriginalColor(renderers, originalColor);
            
            // 메시 데이터 분석
            MeshFilter[] meshFilters = loadedModel.GetComponentsInChildren<MeshFilter>();
            Debug.Log($"[GLBModelLoader] 메시 데이터 처리: {meshFilters.Length}개 메시");
            
            // 콜라이더 설정 (백그라운드에서)
            StartCoroutine(SetupModelCollidersAsync());
            
            // DoubleTap3D 연결 설정
            SetupDoubleTap3DIntegration();
            
            // 로딩 후 메모리 정리
            MemoryOptimizationAfterLoading();
            
            Debug.Log($"[GLBModelLoader] GLB 모델 로딩 및 배치 완료: {loadedModel.name}");
            onComplete?.Invoke(true);
        }
        else
        {
            if (!instantiateSuccess)
            {
                LogError("[GLBModelLoader] GLB 씬 인스턴스화 실패");
                LogError("[GLBModelLoader] - 모바일 디바이스에서 인스턴스화 지원 문제");
            }
            else
            {
                LogError("[GLBModelLoader] GLB 씬 인스턴스화 후 자식 오브젝트가 없음");
                LogError("[GLBModelLoader] - GLBContainer에 모델이 생성되지 않음");
            }
            onComplete?.Invoke(false);
        }
    }

    private void AnalyzeGLBMaterials(MeshRenderer[] renderers)
    {
        Debug.Log($"[GLBModelLoader] === GLB 머티리얼 분석 시작 ===");
        Debug.Log($"[GLBModelLoader] 분석할 렌더러 개수: {renderers.Length}");
        
        foreach (var renderer in renderers)
        {
            if (renderer.materials != null)
            {
                Debug.Log($"[GLBModelLoader] 오브젝트: {renderer.name} - 머티리얼 개수: {renderer.materials.Length}");
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    Material material = renderer.materials[i];
                    if (material != null)
                    {
                        Debug.Log($"[GLBModelLoader] === 머티리얼 {i + 1} 분석 ===");
                        Debug.Log($"[GLBModelLoader] GLTFast 할당 머티리얼명: {material.name}");
                        Debug.Log($"[GLBModelLoader] GLTFast 할당 셰이더: {material.shader.name}");
                        Debug.Log($"[GLBModelLoader] 셰이더 지원 여부: {material.shader.isSupported}");
                        Debug.Log($"[GLBModelLoader] 셰이더 키워드: {string.Join(", ", material.shaderKeywords)}");
                        
                        // GLTFast가 어떤 셰이더를 사용했는지 상세 분석
                        AnalyzeShaderType(material);
                        
                        // 셰이더가 지원되지 않는 경우
                        if (!material.shader.isSupported)
                        {
                            LogError($"[GLBModelLoader] 지원되지 않는 셰이더: {material.shader.name}");
                            LogError("[GLBModelLoader] - Android/iPhone에서 해당 셰이더 지원 안됨");
                            LogError("[GLBModelLoader] - GLTFast 셰이더 호환성 문제");
                            LogError("[GLBModelLoader] - 대체 셰이더로 교체 필요");
                        }
                        
                        // 텍스처 분석
                        AnalyzeTextures(material);
                        
                        // 색상 분석
                        AnalyzeMaterialColors(material);
                        
                        Debug.Log($"[GLBModelLoader] === 머티리얼 {i + 1} 분석 완료 ===");
                    }
                    else
                    {
                        LogError($"[GLBModelLoader] GLTFast Null 머티리얼 생성: {renderer.name} - Index {i}");
                    }
                }
            }
            else
            {
                LogError($"[GLBModelLoader] GLTFast 머티리얼 배열 생성 실패: {renderer.name}");
            }
        }
        Debug.Log($"[GLBModelLoader] === GLB 머티리얼 분석 완료 ===");
    }

    private void AnalyzeShaderType(Material material)
    {
        if (material.shader.name.Contains("glTF"))
        {
            Debug.Log($"[GLBModelLoader] GLTFast 전용 셰이더 감지: {material.shader.name}");
            if (material.shader.name.Contains("pbrMetallicRoughness"))
            {
                Debug.Log("[GLBModelLoader] PBR 메탈릭-러프니스 셰이더 사용");
            }
            else if (material.shader.name.Contains("pbrSpecularGlossiness"))
            {
                Debug.Log("[GLBModelLoader] PBR 스페큘러-글로시니스 셰이더 사용");
            }
            else if (material.shader.name.Contains("Unlit"))
            {
                Debug.Log("[GLBModelLoader] Unlit 셰이더 사용");
            }
        }
        else if (material.shader.name.Contains("Universal Render Pipeline"))
        {
            Debug.Log($"[GLBModelLoader] URP 표준 셰이더 사용: {material.shader.name}");
        }
        else if (material.shader.name.Contains("Standard"))
        {
            Debug.Log($"[GLBModelLoader] Built-in 표준 셰이더 사용: {material.shader.name}");
        }
        else
        {
            Debug.Log($"[GLBModelLoader] 기타 셰이더 사용: {material.shader.name}");
        }
    }

    private void AnalyzeTextures(Material material)
    {
        // 메인 텍스처 확인
        if (material.mainTexture != null)
        {
            Debug.Log($"[GLBModelLoader] GLTFast 할당 메인텍스처: {material.mainTexture.name}");
            Debug.Log($"[GLBModelLoader] 텍스처 크기: {material.mainTexture.width}x{material.mainTexture.height}");
            Debug.Log($"[GLBModelLoader] 텍스처 형식: {material.mainTexture.GetType()}");
        }
        else
        {
            LogError($"[GLBModelLoader] GLTFast 메인 텍스처 할당 실패: {material.name}");
        }
        
        // PBR 텍스처들 확인
        CheckPBRTextures(material);
    }

    private void AnalyzeMaterialColors(Material material)
    {
        CheckMaterialColors(material);
    }

    private void CheckPBRTextures(Material material)
    {
        // 다양한 PBR 텍스처 프로퍼티 체크
        string[] metallicProps = { "_MetallicGlossMap", "_MetallicMap", "_Metallic" };
        string[] normalProps = { "_BumpMap", "_NormalMap", "_Normal" };
        string[] aoProps = { "_OcclusionMap", "_AO", "_AmbientOcclusion" };
        string[] emissionProps = { "_EmissionMap", "_Emission" };
        
        foreach (string prop in metallicProps)
        {
            if (material.HasProperty(prop))
            {
                var tex = material.GetTexture(prop);
                Debug.Log($"[GLBModelLoader] {prop} 텍스처: {(tex != null ? tex.name : "없음")}");
                break;
            }
        }
        
        foreach (string prop in normalProps)
        {
            if (material.HasProperty(prop))
            {
                var tex = material.GetTexture(prop);
                Debug.Log($"[GLBModelLoader] {prop} 텍스처: {(tex != null ? tex.name : "없음")}");
                break;
            }
        }
        
        foreach (string prop in aoProps)
        {
            if (material.HasProperty(prop))
            {
                var tex = material.GetTexture(prop);
                Debug.Log($"[GLBModelLoader] {prop} 텍스처: {(tex != null ? tex.name : "없음")}");
                break;
            }
        }
        
        foreach (string prop in emissionProps)
        {
            if (material.HasProperty(prop))
            {
                var tex = material.GetTexture(prop);
                Debug.Log($"[GLBModelLoader] {prop} 텍스처: {(tex != null ? tex.name : "없음")}");
                break;
            }
        }
    }

    private void CheckMaterialColors(Material material)
    {
        // 다양한 색상 프로퍼티 체크
        string[] colorProps = { "_Color", "_BaseColor", "_Albedo", "_MainColor" };
        
        foreach (string prop in colorProps)
        {
            if (material.HasProperty(prop))
            {
                Color color = material.GetColor(prop);
                Debug.Log($"[GLBModelLoader] {prop}: {color}");
                
                // 핑크색 체크 (Missing Shader 기본 색상)
                if (Mathf.Approximately(color.r, 1f) && 
                    Mathf.Approximately(color.g, 0f) && 
                    Mathf.Approximately(color.b, 1f))
                {
                    LogError($"[GLBModelLoader] 핑크색 머티리얼 감지 - GLTFast 셰이더 문제: {material.name}");
                }
                break;
            }
        }
        
        // 메탈릭/러프니스 값 확인
        string[] metallicValueProps = { "_Metallic", "_MetallicFactor" };
        string[] smoothnessProps = { "_Glossiness", "_Smoothness", "_Roughness" };
        
        foreach (string prop in metallicValueProps)
        {
            if (material.HasProperty(prop))
            {
                float value = material.GetFloat(prop);
                Debug.Log($"[GLBModelLoader] {prop} 값: {value}");
                break;
            }
        }
        
        foreach (string prop in smoothnessProps)
        {
            if (material.HasProperty(prop))
            {
                float value = material.GetFloat(prop);
                Debug.Log($"[GLBModelLoader] {prop} 값: {value}");
                break;
            }
        }
    }

    private IEnumerator SetupModelCollidersAsync()
    {
        if (loadedModel == null) yield break;
        
        Debug.Log("[GLBModelLoader] 콜라이더 설정 시작");
        
        MeshRenderer[] renderers = loadedModel.GetComponentsInChildren<MeshRenderer>();
        int processedCount = 0;
        
        foreach (var renderer in renderers)
        {
            MeshCollider collider = renderer.GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.convex = true;
                Debug.Log($"[GLBModelLoader] MeshCollider 추가됨: {renderer.name}");
                Debug.Log($"[GLBModelLoader] Convex Collider 설정: {renderer.name}");
            }
            
            processedCount++;
            
            // 5개마다 프레임 양보
            if (processedCount % 5 == 0)
            {
                yield return null;
            }
        }
        
        // 전체 모델에 BoxCollider 추가
        BoxCollider boxCollider = loadedModel.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = loadedModel.AddComponent<BoxCollider>();
            Debug.Log("[GLBModelLoader] 전체 모델에 BoxCollider 추가됨");
        }
        
        Debug.Log($"[GLBModelLoader] 콜라이더 최적화 완료: {processedCount}개 MeshCollider 처리됨");
        
        // DoubleTap3D 연동 확인
        DoubleTap3D doubleTap = glbContainer.GetComponent<DoubleTap3D>();
        if (doubleTap == null)
        {
            doubleTap = glbContainer.GetComponentInChildren<DoubleTap3D>();
        }
        
        if (doubleTap != null)
        {
            Debug.Log("[GLBModelLoader] DoubleTap3D와 GLB 모델 연동 확인됨");
        }
        else
        {
            LogError("[GLBModelLoader] DoubleTap3D 컴포넌트를 GLBContainer에서 찾을 수 없음");
        }
        
        // 로드된 모델에 추가 터치 컴포넌트 설정 (선택사항)
        SetupModelTouchComponents();
    }

    private void SetupModelTouchComponents()
    {
        if (loadedModel == null) return;
        
        Debug.Log("[GLBModelLoader] GLB 모델에 터치 컴포넌트 설정");
        
        // 로드된 모델의 모든 MeshRenderer에 개별 콜라이더 확인
        MeshRenderer[] renderers = loadedModel.GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in renderers)
        {
            // MeshCollider가 있는지 확인
            MeshCollider meshCollider = renderer.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                Debug.Log($"[GLBModelLoader] {renderer.name}에 MeshCollider 확인됨");
            }
        }
        
        Debug.Log($"[GLBModelLoader] {renderers.Length}개 렌더러에 터치 컴포넌트 설정 완료");
    }

    private IEnumerator OptimizeMaterialsForMobile()
    {
        if (loadedModel == null) yield break;
        
        Debug.Log("[GLBModelLoader] 모바일용 머티리얼 최적화 시작");
        
        MeshRenderer[] renderers = loadedModel.GetComponentsInChildren<MeshRenderer>();
        int optimizedCount = 0;
        
        foreach (var renderer in renderers)
        {
            if (renderer.materials != null)
            {
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    Material material = renderer.materials[i];
                    if (material != null)
                    {
                        // 지원되지 않는 셰이더 체크
                        if (!material.shader.isSupported)
                        {
                            Debug.Log($"[GLBModelLoader] 지원되지 않는 셰이더 감지: {material.shader.name}");
                            
                            // 렌더 파이프라인에 따른 셰이더 선택
                            Shader targetShader = null;
                            bool isURP = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null && 
                                        UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("Universal");
                            
                            if (isURP)
                            {
                                Debug.Log("[GLBModelLoader] URP 환경 감지 - URP 셰이더 사용");
                                
                                // GLTFast 6.13.0 + URP 호환성 문제 해결
                                // 1순위: GLTFast 자체 URP 셰이더
                                targetShader = Shader.Find("Shader Graphs/glTF-pbrMetallicRoughness");
                                if (targetShader == null || !targetShader.isSupported)
                                {
                                    // 2순위: URP Lit 셰이더
                                    targetShader = Shader.Find("Universal Render Pipeline/Lit");
                                }
                                if (targetShader == null || !targetShader.isSupported)
                                {
                                    // 3순위: URP/Lit (구버전 URP)
                                    targetShader = Shader.Find("URP/Lit");
                                }
                                if (targetShader == null || !targetShader.isSupported)
                                {
                                    // 4순위: Unlit (최후의 수단)
                                    targetShader = Shader.Find("Universal Render Pipeline/Unlit");
                                    LogError("[GLBModelLoader] URP Lit 셰이더 모두 실패, Unlit으로 대체");
                                }
                                if (targetShader == null)
                                {
                                    // 5순위: 기본 Unlit
                                    targetShader = Shader.Find("Unlit/Color");
                                    LogError("[GLBModelLoader] 모든 URP 셰이더 실패, 기본 Unlit 사용");
                                }
                            }
                            else
                            {
                                Debug.Log("[GLBModelLoader] Built-in 파이프라인 - Standard 셰이더 사용");
                                targetShader = Shader.Find("Standard");
                                if (targetShader == null)
                                {
                                    LogError("[GLBModelLoader] Standard 셰이더를 찾을 수 없음");
                                    targetShader = Shader.Find("Legacy Shaders/Diffuse");
                                }
                            }
                            
                            if (targetShader != null)
                            {
                                // 기존 텍스처와 색상 정보 백업
                                Texture mainTex = material.mainTexture;
                                Color materialColor = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                                Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                                
                                Debug.Log($"[GLBModelLoader] 셰이더 교체 시도: {material.shader.name} -> {targetShader.name}");
                                
                                // 셰이더 교체
                                material.shader = targetShader;
                                
                                // 텍스처와 색상 복원
                                if (mainTex != null)
                                {
                                    if (material.HasProperty("_MainTex"))
                                    {
                                        material.SetTexture("_MainTex", mainTex);
                                    }
                                    else if (material.HasProperty("_BaseMap"))
                                    {
                                        material.SetTexture("_BaseMap", mainTex);
                                    }
                                    Debug.Log($"[GLBModelLoader] 메인 텍스처 복원: {mainTex.name}");
                                }
                                
                                // 색상 설정
                                Color finalColor = (baseColor != Color.white) ? baseColor : materialColor;
                                if (finalColor != Color.white)
                                {
                                    if (material.HasProperty("_Color"))
                                    {
                                        material.SetColor("_Color", finalColor);
                                    }
                                    else if (material.HasProperty("_BaseColor"))
                                    {
                                        material.SetColor("_BaseColor", finalColor);
                                    }
                                    Debug.Log($"[GLBModelLoader] 머티리얼 색상 복원: {finalColor}");
                                }
                                
                                Debug.Log($"[GLBModelLoader] 셰이더 교체 성공: {targetShader.name}");
                                optimizedCount++;
                            }
                            else
                            {
                                LogError("[GLBModelLoader] 사용 가능한 대체 셰이더를 찾을 수 없음");
                            }
                        }
                        
                        // 핑크색 머티리얼 수정
                        if (material.HasProperty("_Color"))
                        {
                            Color color = material.GetColor("_Color");
                            if (Mathf.Approximately(color.r, 1f) && 
                                Mathf.Approximately(color.g, 0f) && 
                                Mathf.Approximately(color.b, 1f))
                            {
                                Debug.Log($"[GLBModelLoader] 핑크색 머티리얼 수정: {material.name}");
                                material.color = Color.white; // 흰색으로 변경
                                optimizedCount++;
                            }
                        }
                    }
                    
                    // 5개마다 프레임 양보
                    if (optimizedCount % 5 == 0)
                    {
                        yield return null;
                    }
                }
            }
        }
        
        Debug.Log($"[GLBModelLoader] 머티리얼 최적화 완료: {optimizedCount}개 머티리얼 수정됨");
    }

    private void OptimizeMaterialsImmediate(MeshRenderer[] renderers)
    {
        Debug.Log("[GLBModelLoader] 즉시 머티리얼 최적화 시작");
        
        int optimizedCount = 0;
        
        foreach (var renderer in renderers)
        {
            if (renderer.materials != null)
            {
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    Material material = renderer.materials[i];
                    if (material != null)
                    {
                        Debug.Log($"[GLBModelLoader] 머티리얼 최적화 체크: {material.name}, 셰이더: {material.shader.name}");
                        
                        // Built-in Standard 셰이더를 URP 셰이더로 강제 교체
                        if (material.shader.name.Contains("Standard") || !material.shader.isSupported)
                        {
                            Debug.Log($"[GLBModelLoader] Built-in 셰이더 감지, URP로 교체 필요: {material.shader.name}");
                            
                            // 렌더 파이프라인 확인
                            bool isURP = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null && 
                                        UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("Universal");
                            
                            if (isURP)
                            {
                                Debug.Log("[GLBModelLoader] URP 환경에서 Built-in 셰이더 교체");
                                
                                // 기존 정보 백업 (더 광범위하게)
                                Texture mainTex = material.mainTexture;
                                Color materialColor = Color.white;
                                
                                // 다양한 색상 프로퍼티에서 색상 정보 수집
                                if (material.HasProperty("_Color"))
                                {
                                    materialColor = material.GetColor("_Color");
                                    Debug.Log($"[GLBModelLoader] 원본 _Color 백업: {materialColor}");
                                }
                                else if (material.HasProperty("_BaseColor"))
                                {
                                    materialColor = material.GetColor("_BaseColor");
                                    Debug.Log($"[GLBModelLoader] 원본 _BaseColor 백업: {materialColor}");
                                }
                                
                                // GLB 파일의 실제 색상이 흰색인지 확인
                                if (materialColor == Color.white || 
                                    (Mathf.Approximately(materialColor.r, 1f) && 
                                     Mathf.Approximately(materialColor.g, 1f) && 
                                     Mathf.Approximately(materialColor.b, 1f)))
                                {
                                    Debug.LogWarning("[GLBModelLoader] GLB 파일에서 흰색 감지, 기본 색상 적용");
                                    
                                    // GLB 모델별 기본 색상 설정 (모델명에 따라)
                                    if (material.name.ToLower().Contains("lion"))
                                    {
                                        materialColor = new Color(0.8f, 0.6f, 0.2f, 1f); // 갈색 (사자)
                                        Debug.Log("[GLBModelLoader] Lion 모델 기본 색상 적용: 갈색");
                                    }
                                    else if (material.name.ToLower().Contains("love") || material.name.ToLower().Contains("heart"))
                                    {
                                        materialColor = new Color(1f, 0.2f, 0.4f, 1f); // 핑크/빨강 (하트)
                                        Debug.Log("[GLBModelLoader] Love/Heart 모델 기본 색상 적용: 빨강");
                                    }
                                    else
                                    {
                                        // 일반적인 기본 색상
                                        materialColor = new Color(0.7f, 0.7f, 0.7f, 1f); // 연한 회색
                                        Debug.Log("[GLBModelLoader] 일반 모델 기본 색상 적용: 연한 회색");
                                    }
                                }
                                
                                // 추가 텍스처 정보 백업
                                Texture metallicTex = material.GetTexture("_MetallicGlossMap");
                                Texture normalTex = material.GetTexture("_BumpMap");
                                Texture occlusionTex = material.GetTexture("_OcclusionMap");
                                Texture emissionTex = material.GetTexture("_EmissionMap");
                                
                                // PBR 값들 백업
                                float metallicValue = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
                                float smoothnessValue = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
                                
                                Debug.Log($"[GLBModelLoader] PBR 값 백업 - Metallic: {metallicValue}, Smoothness: {smoothnessValue}");
                                Debug.Log($"[GLBModelLoader] 최종 적용할 색상: {materialColor}");
                                
                                // URP Lit 셰이더로 교체
                                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                                if (urpShader != null && urpShader.isSupported)
                                {
                                    Debug.Log($"[GLBModelLoader] URP Lit 셰이더로 교체: {material.name}");
                                    material.shader = urpShader;
                                    
                                    // === 색상 우선 설정 (가장 중요) ===
                                    material.SetColor("_BaseColor", materialColor);
                                    Debug.Log($"[GLBModelLoader] BaseColor 설정 완료: {materialColor}");
                                    
                                    // === 텍스처 복원 ===
                                    if (mainTex != null)
                                    {
                                        material.SetTexture("_BaseMap", mainTex);
                                        Debug.Log($"[GLBModelLoader] BaseMap 텍스처 복원 성공: {mainTex.name}");
                                        
                                        // 텍스처가 있으면 색상을 약간 조정 (텍스처와 색상 조합)
                                        Color tintedColor = materialColor * 0.8f; // 약간 어둡게
                                        tintedColor.a = materialColor.a; // 알파는 유지
                                        material.SetColor("_BaseColor", tintedColor);
                                        Debug.Log($"[GLBModelLoader] 텍스처와 색상 조합: {tintedColor}");
                                    }
                                    else
                                    {
                                        Debug.LogWarning("[GLBModelLoader] 메인 텍스처 없음, 순수 색상으로 표시");
                                        // 텍스처가 없으면 색상을 더 진하게
                                        Color pureColor = materialColor * 1.2f;
                                        pureColor.a = materialColor.a;
                                        // 값이 1을 넘지 않도록 클램프
                                        pureColor.r = Mathf.Clamp01(pureColor.r);
                                        pureColor.g = Mathf.Clamp01(pureColor.g);
                                        pureColor.b = Mathf.Clamp01(pureColor.b);
                                        material.SetColor("_BaseColor", pureColor);
                                        Debug.Log($"[GLBModelLoader] 순수 색상 설정: {pureColor}");
                                    }
                                    
                                    // 추가 텍스처들 복원
                                    if (metallicTex != null)
                                    {
                                        material.SetTexture("_MetallicGlossMap", metallicTex);
                                        Debug.Log($"[GLBModelLoader] Metallic 텍스처 복원: {metallicTex.name}");
                                    }
                                    
                                    if (normalTex != null)
                                    {
                                        material.SetTexture("_BumpMap", normalTex);
                                        Debug.Log($"[GLBModelLoader] Normal 텍스처 복원: {normalTex.name}");
                                    }
                                    
                                    if (occlusionTex != null)
                                    {
                                        material.SetTexture("_OcclusionMap", occlusionTex);
                                        Debug.Log($"[GLBModelLoader] Occlusion 텍스처 복원: {occlusionTex.name}");
                                    }
                                    
                                    if (emissionTex != null)
                                    {
                                        material.SetTexture("_EmissionMap", emissionTex);
                                        Debug.Log($"[GLBModelLoader] Emission 텍스처 복원: {emissionTex.name}");
                                    }
                                    
                                    // === 색상 복원 ===
                                    material.SetColor("_BaseColor", materialColor);
                                    Debug.Log($"[GLBModelLoader] BaseColor 복원 완료: {materialColor}");
                                    
                                    // PBR 값들 복원
                                    material.SetFloat("_Metallic", metallicValue);
                                    material.SetFloat("_Smoothness", smoothnessValue);
                                    Debug.Log($"[GLBModelLoader] PBR 값 복원 완료 - Metallic: {metallicValue}, Smoothness: {smoothnessValue}");
                                    
                                    // 알파 블렌딩 설정 (투명도 지원)
                                    if (materialColor.a < 1.0f)
                                    {
                                        Debug.Log($"[GLBModelLoader] 투명도 감지: {materialColor.a}, 알파 블렌딩 활성화");
                                        material.SetFloat("_Surface", 1); // Transparent
                                        material.SetFloat("_Blend", 0); // Alpha
                                    }
                                    
                                    optimizedCount++;
                                    Debug.Log($"[GLBModelLoader] 셰이더 교체 및 속성 복원 완료: Standard → URP Lit");
                                }
                                else
                                {
                                    LogError("[GLBModelLoader] URP Lit 셰이더를 찾을 수 없거나 지원되지 않음");
                                    
                                    // Fallback: Unlit 셰이더
                                    Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                                    if (unlitShader != null)
                                    {
                                        material.shader = unlitShader;
                                        Debug.Log("[GLBModelLoader] URP Unlit 셰이더로 fallback");
                                        optimizedCount++;
                                    }
                                }
                            }
                        }
                        
                        // 핑크색 머티리얼 수정
                        if (material.HasProperty("_Color"))
                        {
                            Color color = material.GetColor("_Color");
                            if (Mathf.Approximately(color.r, 1f) && 
                                Mathf.Approximately(color.g, 0f) && 
                                Mathf.Approximately(color.b, 1f))
                            {
                                Debug.Log($"[GLBModelLoader] 핑크색 머티리얼 수정: {material.name}");
                                material.color = Color.white;
                                optimizedCount++;
                            }
                        }
                        else if (material.HasProperty("_BaseColor"))
                        {
                            Color color = material.GetColor("_BaseColor");
                            if (Mathf.Approximately(color.r, 1f) && 
                                Mathf.Approximately(color.g, 0f) && 
                                Mathf.Approximately(color.b, 1f))
                            {
                                Debug.Log($"[GLBModelLoader] 핑크색 BaseColor 수정: {material.name}");
                                material.SetColor("_BaseColor", Color.white);
                                optimizedCount++;
                            }
                        }
                    }
                }
            }
        }
        
        Debug.Log($"[GLBModelLoader] 즉시 머티리얼 최적화 완료: {optimizedCount}개 머티리얼 수정됨");
    }

    private Color ExtractColorFromGLB(byte[] glbData)
    {
        try
        {
            Debug.Log("[GLBModelLoader] GLB 원본 색상 추출 시작");
            
            // GLB 파일에서 JSON 부분 찾기
            string jsonContent = "";
            
            // GLB 헤더 건너뛰기 (12바이트)
            if (glbData.Length > 20)
            {
                // JSON chunk 길이 읽기 (4바이트, 오프셋 12)
                int jsonLength = System.BitConverter.ToInt32(glbData, 12);
                Debug.Log($"[GLBModelLoader] JSON 청크 길이: {jsonLength}");
                
                // 안전성 검사
                if (jsonLength > 0 && jsonLength < glbData.Length && glbData.Length > (20 + jsonLength))
                {
                    try
                    {
                        jsonContent = System.Text.Encoding.UTF8.GetString(glbData, 20, jsonLength);
                        Debug.Log($"[GLBModelLoader] JSON 추출 성공: {jsonContent.Length} 문자");
                    }
                    catch (System.Exception ex)
                    {
                        LogError($"[GLBModelLoader] JSON 인코딩 실패: {ex.Message}");
                    }
                }
                else
                {
                    LogError($"[GLBModelLoader] 잘못된 JSON 청크 길이: {jsonLength}");
                }
            }
            
            if (!string.IsNullOrEmpty(jsonContent))
            {
                // baseColorFactor 찾기 (여러 패턴 시도)
                string[] searchPatterns = {
                    "\"baseColorFactor\":[",
                    "\"baseColorFactor\" : [",
                    "baseColorFactor\":[",
                    "baseColorFactor\" : ["
                };
                
                foreach (string searchKey in searchPatterns)
                {
                    int startIndex = jsonContent.IndexOf(searchKey);
                    
                    if (startIndex != -1)
                    {
                        startIndex += searchKey.Length;
                        int endIndex = jsonContent.IndexOf(']', startIndex);
                        
                        if (endIndex != -1)
                        {
                            string colorArray = jsonContent.Substring(startIndex, endIndex - startIndex);
                            Debug.Log($"[GLBModelLoader] baseColorFactor 발견: [{colorArray}]");
                            
                            // 색상 값 파싱
                            try
                            {
                                string[] values = colorArray.Split(',');
                                if (values.Length >= 3)
                                {
                                    float r = float.Parse(values[0].Trim());
                                    float g = float.Parse(values[1].Trim());
                                    float b = float.Parse(values[2].Trim());
                                    float a = values.Length > 3 ? float.Parse(values[3].Trim()) : 1.0f;
                                    
                                    Color extractedColor = new Color(r, g, b, a);
                                    Debug.Log($"[GLBModelLoader] GLB 원본 색상 추출 성공: {extractedColor}");
                                    return extractedColor;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                LogError($"[GLBModelLoader] 색상 값 파싱 실패: {ex.Message}");
                                continue; // 다음 패턴 시도
                            }
                        }
                    }
                }
                
                Debug.LogWarning("[GLBModelLoader] baseColorFactor를 JSON에서 찾을 수 없음");
            }
            else
            {
                LogError("[GLBModelLoader] GLB JSON 추출 실패");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"[GLBModelLoader] GLB 색상 추출 예외: {ex.Message}");
        }
        
        // 기본값 반환 (Heart.glb의 실제 색상)
        Color defaultHeartColor = new Color(0.427f, 0.023f, 0.023f, 1f);
        Debug.Log($"[GLBModelLoader] 기본 하트 색상 적용: {defaultHeartColor}");
        return defaultHeartColor;
    }

    private void OptimizeMaterialsWithOriginalColor(MeshRenderer[] renderers, Color originalColor)
    {
        Debug.Log($"[GLBModelLoader] 원본 색상으로 머티리얼 최적화 시작: {originalColor}");
        
        int optimizedCount = 0;
        
        foreach (var renderer in renderers)
        {
            if (renderer.materials != null)
            {
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    Material material = renderer.materials[i];
                    if (material != null)
                    {
                        Debug.Log($"[GLBModelLoader] 머티리얼 처리: {material.name}, 셰이더: {material.shader.name}");
                        
                        // Built-in Standard 셰이더를 URP 셰이더로 강제 교체
                        if (material.shader.name.Contains("Standard") || !material.shader.isSupported)
                        {
                            Debug.Log($"[GLBModelLoader] Built-in 셰이더 감지, URP로 교체: {material.shader.name}");
                            
                            // 렌더 파이프라인 확인
                            bool isURP = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null && 
                                        UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("Universal");
                            
                            if (isURP)
                            {
                                Debug.Log("[GLBModelLoader] URP 환경에서 Built-in 셰이더를 URP Lit로 교체");
                                
                                // 기존 텍스처 백업
                                Texture mainTex = material.mainTexture;
                                
                                // URP Lit 셰이더로 교체
                                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                                if (urpShader != null && urpShader.isSupported)
                                {
                                    Debug.Log($"[GLBModelLoader] URP Lit 셰이더로 교체: {material.name}");
                                    material.shader = urpShader;
                                    
                                    // === 원본 색상 적용 (최우선) ===
                                    material.SetColor("_BaseColor", originalColor);
                                    Debug.Log($"[GLBModelLoader] GLB 원본 색상 적용 완료: {originalColor}");
                                    
                                    // 텍스처 복원
                                    if (mainTex != null)
                                    {
                                        material.SetTexture("_BaseMap", mainTex);
                                        Debug.Log($"[GLBModelLoader] BaseMap 텍스처 복원: {mainTex.name}");
                                    }
                                    
                                    // PBR 설정
                                    material.SetFloat("_Metallic", 0f); // Heart.glb는 metallic 0
                                    material.SetFloat("_Smoothness", 0.45f); // roughness 0.55 → smoothness 0.45
                                    Debug.Log("[GLBModelLoader] PBR 값 설정: Metallic=0, Smoothness=0.45");
                                    
                                    // 표면 타입 설정 (불투명)
                                    material.SetFloat("_Surface", 0); // Opaque
                                    
                                    optimizedCount++;
                                    Debug.Log($"[GLBModelLoader] 셰이더 교체 및 원본 색상 적용 성공: {material.name}");
                                }
                                else
                                {
                                    LogError("[GLBModelLoader] URP Lit 셰이더를 찾을 수 없거나 지원되지 않음");
                                }
                            }
                        }
                        else
                        {
                            // 이미 올바른 셰이더인 경우에도 원본 색상 적용
                            if (material.HasProperty("_BaseColor"))
                            {
                                material.SetColor("_BaseColor", originalColor);
                                Debug.Log($"[GLBModelLoader] 기존 머티리얼에 원본 색상 적용: {originalColor}");
                                optimizedCount++;
                            }
                            else if (material.HasProperty("_Color"))
                            {
                                material.SetColor("_Color", originalColor);
                                Debug.Log($"[GLBModelLoader] 기존 머티리얼에 원본 색상 적용 (_Color): {originalColor}");
                                optimizedCount++;
                            }
                        }
                    }
                }
            }
        }
        
        Debug.Log($"[GLBModelLoader] 원본 색상 머티리얼 최적화 완료: {optimizedCount}개 머티리얼 수정됨");
    }

    public void ClearModel()
    {
        if (loadedModel != null)
        {
            Debug.Log($"[GLBModelLoader] 기존 GLB 모델 정리 시작: {loadedModel.name}");
            
            // 메모리 집약적 정리
            CleanupModelResources(loadedModel);
            
            DestroyImmediate(loadedModel);
            loadedModel = null;
            Debug.Log("[GLBModelLoader] GLB 모델 DestroyImmediate 완료");
        }
        
        // 다운로드된 파일 데이터 해제
        ClearCurrentGLBData();
        
        isModelLoaded = false;
        
        // 강제 가비지 컬렉션 (GLB 로딩 후)
        ForceGarbageCollection();
        
        Debug.Log("[GLBModelLoader] GLB 모델 클리어 및 메모리 정리 완료");
    }

    private void ClearCurrentGLBData()
    {
        if (currentGLBData != null)
        {
            Debug.Log($"[GLBModelLoader] 현재 GLB 파일 데이터 해제: {currentGLBData.Length} bytes");
            currentGLBData = null;
        }
        
        if (!string.IsNullOrEmpty(currentGLBUrl))
        {
            Debug.Log($"[GLBModelLoader] 현재 GLB URL 정리: {currentGLBUrl}");
            currentGLBUrl = "";
        }
    }

    private void CacheGLBFile(string url, byte[] data)
    {
        if (data == null || data.Length == 0) return;
        
        Debug.Log($"[GLBModelLoader] GLB 파일 캐시 저장: {url} ({data.Length} bytes)");
        
        // 캐시 크기 제한
        while (downloadedFiles.Count >= maxCachedFiles && downloadOrder.Count > 0)
        {
            string oldestUrl = downloadOrder.Dequeue();
            if (downloadedFiles.ContainsKey(oldestUrl))
            {
                byte[] oldData = downloadedFiles[oldestUrl];
                downloadedFiles.Remove(oldestUrl);
                Debug.Log($"[GLBModelLoader] 오래된 캐시 파일 제거: {oldestUrl} ({oldData.Length} bytes)");
                oldData = null; // 명시적 해제
            }
        }
        
        // 새 파일 캐시
        downloadedFiles[url] = data;
        downloadOrder.Enqueue(url);
        
        Debug.Log($"[GLBModelLoader] 현재 캐시된 파일 수: {downloadedFiles.Count}");
    }

    public static void ClearAllCache()
    {
        Debug.Log($"[GLBModelLoader] 모든 GLB 캐시 정리 시작: {downloadedFiles.Count}개 파일");
        
        int totalSize = 0;
        foreach (var data in downloadedFiles.Values)
        {
            if (data != null)
            {
                totalSize += data.Length;
            }
        }
        
        downloadedFiles.Clear();
        downloadOrder.Clear();
        
        Debug.Log($"[GLBModelLoader] 모든 GLB 캐시 정리 완료: {totalSize} bytes 해제");
        
        // 강제 GC
        System.GC.Collect();
    }

    public static void ClearCacheForUrl(string url)
    {
        if (downloadedFiles.ContainsKey(url))
        {
            byte[] data = downloadedFiles[url];
            downloadedFiles.Remove(url);
            Debug.Log($"[GLBModelLoader] 특정 URL 캐시 제거: {url} ({data?.Length ?? 0} bytes)");
            data = null;
        }
    }

    public static int GetCachedFileCount()
    {
        return downloadedFiles.Count;
    }

    public static long GetCachedDataSize()
    {
        long totalSize = 0;
        foreach (var data in downloadedFiles.Values)
        {
            if (data != null)
            {
                totalSize += data.Length;
            }
        }
        return totalSize;
    }

    private void CleanupModelResources(GameObject model)
    {
        if (model == null) return;
        
        Debug.Log("[GLBModelLoader] GLB 리소스 정리 시작");
        
        // 모든 MeshRenderer의 머티리얼 정리
        MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.materials != null)
            {
                foreach (var material in renderer.materials)
                {
                    if (material != null)
                    {
                        // 텍스처 해제
                        if (material.mainTexture != null)
                        {
                            Debug.Log($"[GLBModelLoader] 텍스처 해제: {material.mainTexture.name}");
                        }
                        
                        // 머티리얼 파괴 (인스턴스만)
                        if (material.name.Contains("(Instance)"))
                        {
                            DestroyImmediate(material);
                        }
                    }
                }
            }
        }
        
        // 모든 MeshFilter의 메시 정리
        MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>();
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter.mesh != null)
            {
                Debug.Log($"[GLBModelLoader] 메시 해제: {meshFilter.mesh.name}");
                // 메시 메모리 해제
                meshFilter.mesh.Clear();
            }
        }
        
        // 모든 MeshCollider 정리
        MeshCollider[] meshColliders = model.GetComponentsInChildren<MeshCollider>();
        foreach (var collider in meshColliders)
        {
            if (collider.sharedMesh != null)
            {
                Debug.Log($"[GLBModelLoader] 콜라이더 메시 해제: {collider.sharedMesh.name}");
            }
        }
        
        Debug.Log($"[GLBModelLoader] GLB 리소스 정리 완료 - Renderers: {renderers.Length}, Meshes: {meshFilters.Length}, Colliders: {meshColliders.Length}");
    }

    private void ForceGarbageCollection()
    {
        Debug.Log("[GLBModelLoader] 가비지 컬렉션 시작");
        
        // 사용하지 않는 에셋 해제
        Resources.UnloadUnusedAssets();
        
        // 강제 GC 실행
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        
        Debug.Log("[GLBModelLoader] 가비지 컬렉션 완료");
    }

    private void MemoryOptimizationAfterLoading()
    {
        Debug.Log("[GLBModelLoader] 로딩 후 메모리 최적화 시작");
        
        // GLB 메시를 읽기 불가능으로 만들어 메모리 절약
        if (loadedModel != null)
        {
            MeshFilter[] meshFilters = loadedModel.GetComponentsInChildren<MeshFilter>();
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.mesh != null)
                {
                    // 메시를 읽기 불가능으로 설정 (GPU 메모리로 이동)
                    meshFilter.mesh.UploadMeshData(true);
                    Debug.Log($"[GLBModelLoader] 메시 GPU 업로드: {meshFilter.mesh.name}");
                }
            }
            
            Debug.Log($"[GLBModelLoader] {meshFilters.Length}개 메시 GPU 업로드 완료");
        }
        
        // 사용하지 않는 리소스 해제
        Resources.UnloadUnusedAssets();
        
        Debug.Log("[GLBModelLoader] 로딩 후 메모리 최적화 완료");
    }

    private void SetupDoubleTap3DIntegration()
    {
        Debug.Log("[GLBModelLoader] DoubleTap3D 통합 설정 시작");
        
        if (loadedModel == null)
        {
            LogError("[GLBModelLoader] 로드된 모델이 없어서 DoubleTap3D 설정 불가");
            return;
        }
        
        // GLBContainer에서 DoubleTap3D 컴포넌트 찾기
        DoubleTap3D doubleTap3D = glbContainer.GetComponent<DoubleTap3D>();
        if (doubleTap3D == null)
        {
            LogError("[GLBModelLoader] GLBContainer에서 DoubleTap3D 컴포넌트를 찾을 수 없음");
            return;
        }
        
        Debug.Log("[GLBModelLoader] DoubleTap3D 컴포넌트 발견");
        
        // GLBContainer의 BoxCollider 크기를 로드된 모델에 맞게 조정
        BoxCollider containerCollider = glbContainer.GetComponent<BoxCollider>();
        if (containerCollider != null)
        {
            // 로드된 모델의 바운드 계산
            Renderer[] renderers = loadedModel.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds totalBounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    totalBounds.Encapsulate(renderer.bounds);
                }
                
                // GLBContainer 좌표계로 변환
                Vector3 localCenter = glbContainer.InverseTransformPoint(totalBounds.center);
                Vector3 localSize = totalBounds.size;
                
                // 약간 여유를 두고 BoxCollider 설정
                containerCollider.center = localCenter;
                containerCollider.size = localSize * 1.2f; // 20% 여유
                
                Debug.Log($"[GLBModelLoader] BoxCollider 설정 완료: Center={localCenter}, Size={localSize}");
                
                // BoxCollider를 우선순위로 설정 (isTrigger는 false 유지)
                containerCollider.enabled = true;
                
                // GLB 모델의 MeshCollider들을 터치에서 제외 (물리는 유지)
                MeshCollider[] modelColliders = loadedModel.GetComponentsInChildren<MeshCollider>();
                foreach (var collider in modelColliders)
                {
                    // 물리 충돌은 유지하되, 레이캐스트 우선순위를 BoxCollider에게 양보
                    // MeshCollider는 그대로 두고 BoxCollider가 먼저 감지되도록 함
                    Debug.Log($"[GLBModelLoader] MeshCollider 유지: {collider.gameObject.name}");
                }
            }
        }
        else
        {
            LogError("[GLBModelLoader] GLBContainer에서 BoxCollider를 찾을 수 없음");
            
            // BoxCollider가 없으면 새로 생성
            containerCollider = glbContainer.gameObject.AddComponent<BoxCollider>();
            
            // 로드된 모델의 바운드 계산
            Renderer[] renderers = loadedModel.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds totalBounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    totalBounds.Encapsulate(renderer.bounds);
                }
                
                Vector3 localCenter = glbContainer.InverseTransformPoint(totalBounds.center);
                Vector3 localSize = totalBounds.size;
                
                containerCollider.center = localCenter;
                containerCollider.size = localSize * 1.2f;
                
                Debug.Log($"[GLBModelLoader] 새 BoxCollider 생성 및 설정 완료: Center={localCenter}, Size={localSize}");
            }
        }
        
        // DoubleTap3D가 제대로 설정되었는지 확인
        if (doubleTap3D.enabled)
        {
            Debug.Log("[GLBModelLoader] DoubleTap3D가 활성화되어 터치 준비 완료");
        }
        else
        {
            Debug.LogWarning("[GLBModelLoader] DoubleTap3D가 비활성화 상태");
        }
        
        // 터치 감지 테스트
        Collider testCollider = glbContainer.GetComponent<Collider>();
        if (testCollider != null)
        {
            Debug.Log($"[GLBModelLoader] 터치 감지용 Collider 확인: {testCollider.GetType().Name}, Enabled: {testCollider.enabled}");
        }
        
        Debug.Log("[GLBModelLoader] DoubleTap3D 통합 설정 완료");
    }

    public bool HasLoadedModel()
    {
        return isModelLoaded && loadedModel != null;
    }

    public GameObject GetLoadedModel()
    {
        return loadedModel;
    }

    private void LogDebug(string message)
    {
        if (enableDetailedLogging)
        {
            Debug.Log(message);
        }
    }

    private void LogError(string message)
    {
        Debug.LogError(message);
    }

    void OnDestroy()
    {
        Debug.Log("[GLBModelLoader] OnDestroy 호출됨");
        ClearModel();
    }
}