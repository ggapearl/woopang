using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GLB 등장 연출 — 로딩 스피너 → 디졸브 전환.
///
/// 기존에는 GLB 프리팹에 렌더러가 없어 로딩 중엔 아무것도 없다가 완성되면 툭 튀어나왔다.
/// 느린 회선에서는 그 전에 큐브 플레이스홀더가 보여 "기본 프리팹 → 바뀜"으로 읽혔다.
///
/// 흐름은 업로드 미리보기(ARPreviewController)와 동일하게 맞췄다:
///   스피너 등장·회전 → (GLB 로드) → 스피너 페이드아웃 + GLB 페이드인 교차
///
/// GLBModelLoader가 호출하며, 스폰·앵커·필터 로직은 건드리지 않는다.
/// </summary>
public class GLBSpawnEffect : MonoBehaviour
{
    [Header("연출 사용")]
    [Tooltip("끄면 기존처럼 로드 완료 시 즉시 표시된다")]
    public bool enableEffect = true;

    [Header("타이밍 (초)")]
    [Tooltip("GLB가 서서히 나타나는 시간")]
    public float fadeInDuration = 1.0f;
    [Tooltip("스피너가 사라지는 시간")]
    public float spinnerFadeDuration = 0.6f;

    [Header("스피너")]
    [Tooltip("비워두면 내장 연출(구체 4개 궤도 회전)을 쓴다. " +
             "Assets/Prefabs/LoadingSpinner 를 여기에 끌어다 놓으면 그걸 사용")]
    public GameObject spinnerPrefab;
    public float spinnerScale = 0.6f;
    [Tooltip("스피너 회전 속도 (도/초)")]
    public float spinnerRotateSpeed = 120f;
    [Tooltip("모델 중심 기준 스피너 높이 오프셋 (m)")]
    public float spinnerHeightOffset = 0.5f;

    [Header("등장 강조")]
    [Tooltip("이 배율에서 1.0으로 커지며 등장. 1이면 크기 변화 없음")]
    public float popFromScale = 0.85f;

    private GameObject activeSpinner;
    private Coroutine spinRoutine;

    // ── 스피너 ────────────────────────────────────────────────

    /// <summary>로드 시작 시점에 호출 — 스피너를 띄운다.</summary>
    public void BeginLoading(Transform anchor)
    {
        if (!enableEffect || anchor == null) return;
        StopSpinner();

        // Inspector에 프리팹이 지정돼 있으면 그것, 아니면 내장 연출을 만든다.
        // 프리팹을 Resources로 복제하면 GUID가 중복되므로 참조 방식만 지원한다.
        activeSpinner = spinnerPrefab != null
            ? Instantiate(spinnerPrefab)
            : BuildFallbackSpinner();

        activeSpinner.name = "GLB_LoadingSpinner";
        activeSpinner.transform.SetParent(anchor, false);
        activeSpinner.transform.localPosition = Vector3.up * spinnerHeightOffset;
        activeSpinner.transform.localScale = Vector3.one * spinnerScale;

        spinRoutine = StartCoroutine(SpinLoop());
    }

    private IEnumerator SpinLoop()
    {
        while (activeSpinner != null)
        {
            activeSpinner.transform.Rotate(Vector3.up, spinnerRotateSpeed * Time.deltaTime, Space.Self);
            yield return null;
        }
    }

    /// <summary>내장 연출 — 궤도를 도는 작은 구 4개. 외부 에셋 의존이 없다.</summary>
    private GameObject BuildFallbackSpinner()
    {
        var root = new GameObject("GLB_LoadingSpinner_Fallback");
        Shader sh = null;
        var tmpl = Resources.Load<Material>("GLBFallbackUnlit");
        if (tmpl != null) sh = tmpl.shader;
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");

        for (int i = 0; i < 4; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = s.GetComponent<Collider>();
            if (col != null) Destroy(col);
            s.transform.SetParent(root.transform, false);
            float a = i * Mathf.PI * 0.5f;
            s.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.35f, 0f, Mathf.Sin(a) * 0.35f);
            s.transform.localScale = Vector3.one * 0.12f;

            if (sh != null)
            {
                var m = new Material(sh);
                m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));
                s.GetComponent<Renderer>().material = m;
            }
        }
        return root;
    }

    private void StopSpinner()
    {
        if (spinRoutine != null) { StopCoroutine(spinRoutine); spinRoutine = null; }
        if (activeSpinner != null) { Destroy(activeSpinner); activeSpinner = null; }
    }

    // ── 전환 ──────────────────────────────────────────────────

    /// <summary>
    /// 로드 완료 시점에 호출 — 스피너를 지우며 모델을 서서히 드러낸다.
    /// 연출이 꺼져 있거나 모델이 없으면 즉시 표시하고 끝낸다.
    /// </summary>
    public void RevealModel(GameObject model)
    {
        if (model == null) return;

        if (!enableEffect)
        {
            StopSpinner();
            return;
        }
        StartCoroutine(RevealRoutine(model));
    }

    private IEnumerator RevealRoutine(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            StopSpinner();
            yield break;
        }

        // 페이드 대상 머터리얼과 원래 색을 모아둔다
        var mats = new List<Material>();
        var baseColors = new List<Color>();
        foreach (var r in renderers)
        {
            foreach (var m in r.materials)
            {
                if (m == null) continue;
                mats.Add(m);
                baseColors.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
            }
        }

        for (int i = 0; i < mats.Count; i++) SetTransparent(mats[i], true);
        ApplyAlpha(mats, baseColors, 0f);

        Vector3 targetScale = model.transform.localScale;
        Vector3 fromScale = targetScale * Mathf.Clamp(popFromScale, 0.01f, 1f);

        // 스피너는 모델이 드러나는 동안 함께 사라진다
        if (activeSpinner != null)
            StartCoroutine(FadeOutSpinner(activeSpinner, spinnerFadeDuration));

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeInDuration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);   // ease-out cubic
            ApplyAlpha(mats, baseColors, eased);
            model.transform.localScale = Vector3.Lerp(fromScale, targetScale, eased);
            yield return null;
        }

        ApplyAlpha(mats, baseColors, 1f);
        model.transform.localScale = targetScale;
        for (int i = 0; i < mats.Count; i++) SetTransparent(mats[i], false);

        StopSpinner();
    }

    private static void ApplyAlpha(List<Material> mats, List<Color> baseColors, float alpha)
    {
        for (int i = 0; i < mats.Count; i++)
        {
            if (mats[i] == null) continue;
            var c = baseColors[i];
            c.a = alpha;
            if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", c);
            if (mats[i].HasProperty("_Color")) mats[i].SetColor("_Color", c);
        }
    }

    /// <summary>URP 머터리얼의 Surface 모드를 전환. 페이드가 끝나면 반드시 Opaque로 되돌린다.</summary>
    private static void SetTransparent(Material m, bool transparent)
    {
        if (m == null) return;
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            m.SetFloat("_Surface", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            m.SetInt("_ZWrite", 1);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
    }

    private IEnumerator FadeOutSpinner(GameObject spinner, float duration)
    {
        var rs = spinner.GetComponentsInChildren<Renderer>(true);
        var mats = new List<Material>();
        var cols = new List<Color>();
        foreach (var r in rs)
            foreach (var m in r.materials)
            {
                if (m == null) continue;
                mats.Add(m);
                cols.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
                SetTransparent(m, true);
            }

        float t = 0f;
        while (t < duration && spinner != null)
        {
            t += Time.deltaTime;
            ApplyAlpha(mats, cols, 1f - Mathf.Clamp01(t / duration));
            yield return null;
        }
    }

    void OnDisable() => StopSpinner();
    void OnDestroy() => StopSpinner();
}
