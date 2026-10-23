using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 보물찾기 AR 보물상자 (10-22 대표님: 금빛 큐브가 '보물' 같지 않다 — 보물상자로, 색 톤은 그대로 · 둘레가 살짝 반짝이게 ·
/// 누르면 열리는 애니메이션과 함께 상품·축하 메세지).
///  · 제자리에서 천천히 돌고(위아래로 떠다니지 않음) 둘레에 흰 별빛이 피었다 사그라든다
///  · 누를 때마다 살짝 커졌다 돌아온다 (Nudge — 일반 오브젝트와 같은 1.15배) · 서버에 '찾았다'를 묻는 동안엔 달그락(Rattle)
///  · 찾으면(Open) 이쪽을 돌아보고 뚜껑이 열리며 빛기둥과 별빛이 솟은 뒤 작아지며 사라진다(뿌리째) — 그 사이 축하 화면이 뜬다
/// 몸통·뚜껑 메시와 재질은 Apply(Redesign0926ApplyHuntChest)가 만든다. 앵커(뿌리) 아래 'Chest' 에 둔다.
/// 보물 모양(10-24 대표님 — 주최자가 고른다): 보물상자 · 선물상자 · 보물알(뚜껑이 열린다) · 보석 · 별 · 하트(빛만 솟는다) ·
///  사진(액자에 끼운다) · 3D 모델(GLB — 못 받으면 보물상자). 서버 hunts.object_kind / object_url → Dress.
/// </summary>
public class R0926HuntChest : MonoBehaviour
{
    [SerializeField] private Transform lid;               // 원점 = 경첩(몸통 뒤 위 모서리) — X 로 돌면 열린다
    [SerializeField] private float spinSpeed = 24f;       // 도/초
    [SerializeField] private Skin[] skins;                // 모양마다 몸통·뚜껑 (보물상자 포함)

    /// <summary>보물 모양 하나 — 뚜껑이 없으면(보석·별·하트·사진·모델) 열 때 빛만 솟는다</summary>
    [Serializable]
    public class Skin
    {
        public string kind;
        public Mesh body, lid;
        public Material[] bodyMats, lidMats;
        public Vector3 hinge;                             // 뚜껑 원점
        public float seam;                                // 빛기둥·별빛이 솟는 높이
        public Vector3 boxCenter, boxSize;                // 누르기 충돌 상자
    }

    /// <summary>사진 칸 — 액자(사진 모양) 앞면의 정사각형 (상자 좌표)</summary>
    private const float PhotoSide = 0.68f, PhotoY = 0.12f, PhotoZ = 0.032f;
    private const float ModelSize = 1.0f;                 // 3D 모델은 가장 긴 변을 보물상자 폭에 맞춘다

    private const float NudgeScale = 1.15f, NudgeHold = 0.16f, NudgeSpeed = 5f;   // 일반 오브젝트(Object3DTouchHaptic)와 같은 느낌
    private const float LidOpen = 112f;
    private static readonly int PhaseId = Shader.PropertyToID("_PulsePhaseOffset");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Mesh beamMesh;
    private static Material beamMat;

    private Renderer body;
    private Vector3 rest;
    private float yaw, wobble, nudge = 1f, nudgeUntil = -1f;
    private bool rattle, opening, gone;
    private float seam = 0.10f;                           // 몸통 윗면 높이 (상자 좌표) — 빛기둥이 여기서 솟는다
    private readonly List<GameObject> extras = new List<GameObject>();   // 사진 판·3D 모델 — 다른 모양으로 갈아입으면 걷는다
    private readonly List<Renderer> extraRenderers = new List<Renderer>();
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();   // 받은 사진·재질 — 지울 때 같이
    private Transform modelHolder;
    private int dressGen;

    /// <summary>지금 입은 모양 (기본 보물상자)</summary>
    public string Kind { get; private set; } = "chest";
    /// <summary>사진·3D 모델을 받는 중</summary>
    public bool Loading { get; private set; }
    /// <summary>입힌 3D 모델 (없으면 null)</summary>
    public GameObject Model { get; private set; }
    /// <summary>액자에 끼운 사진 (없으면 null)</summary>
    public Texture Photo { get; private set; }

    /// <summary>여는 중 (찾음) — 누름·달그락은 더 받지 않는다</summary>
    public bool Opening => opening;
    /// <summary>뚜껑이 열린 각도 (도)</summary>
    public float LidAngle => lid != null ? Quaternion.Angle(Quaternion.identity, lid.localRotation) : 0f;

    private void Awake()
    {
        body = GetComponent<Renderer>();
        rest = transform.localScale;
        yaw = transform.eulerAngles.y;
        // 상자마다 빛이 숨 쉬는 박자를 다르게 (예전 금빛 큐브처럼)
        var mpb = new MaterialPropertyBlock();
        mpb.SetFloat(PhaseId, UnityEngine.Random.Range(0f, Mathf.PI * 2f));
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.SetPropertyBlock(mpb);
        Twinkle();   // 앵커가 렌더러를 숨기기 전에 만들어 두어야 같이 숨고 같이 보인다
    }

    private void OnDisable()
    {
        // 여는 도중 꺼지면(백그라운드 등) 이어 갈 수 없다 — 남기지 않는다
        if (opening && !gone && transform.parent != null) { gone = true; Destroy(transform.parent.gameObject); }
    }

    private void OnDestroy()
    {
        foreach (var o in owned) if (o != null) Destroy(o);
    }

    private void Update()
    {
        // 사진 판·모델(받는 동안의 로딩 고리 포함)도 앵커가 몸통을 숨기고 보일 때 같이 (나중에 붙어 앵커가 모른다)
        if (Loading && modelHolder != null) modelHolder.GetComponentsInChildren(true, extraRenderers);
        if (body != null)
            foreach (var r in extraRenderers) if (r != null && r.enabled != body.enabled) r.enabled = body.enabled;

        if (!opening) yaw += spinSpeed * Time.deltaTime;
        float tilt = wobble + (rattle && !opening ? Mathf.Sin(Time.unscaledTime * 40f) * 3.5f : 0f);
        transform.rotation = Quaternion.Euler(0f, yaw, tilt);
        float goal = Time.unscaledTime < nudgeUntil ? NudgeScale : 1f;
        nudge = Mathf.Lerp(nudge, goal, Time.unscaledDeltaTime * NudgeSpeed);
        if (goal == 1f && nudge < 1.001f) nudge = 1f;
        transform.localScale = rest * nudge;
    }

    /// <summary>누름 — 살짝 커졌다 돌아온다</summary>
    public void Nudge()
    {
        if (!opening) nudgeUntil = Time.unscaledTime + NudgeHold;
    }

    /// <summary>서버에 '찾았다'를 묻는 동안 달그락 — 답이 오면 Open 이나 Rattle(false)</summary>
    public void Rattle(bool on)
    {
        rattle = on && !opening;
    }

    // ── 모양 ──────────────────────────────────────────────
    /// <summary>서버의 보물 모양 — 코드로 만든 모양은 바로, 사진·3D 모델은 받아서 붙인다 (못 받으면 보물상자 그대로)</summary>
    public void Dress(string kind, string url)
    {
        if (string.IsNullOrEmpty(kind) || kind == Kind) return;
        if ((kind == "photo" || kind == "model") && string.IsNullOrEmpty(url)) return;
        if (!Wear(kind)) return;   // 모르는 모양(새 앱이 만든 모양을 옛 앱이 받음) — 보물상자 그대로
        if (kind == "photo") StartCoroutine(PhotoCo(url, dressGen));
        else if (kind == "model") StartCoroutine(ModelCo(url, dressGen));
    }

    /// <summary>모양 갈아입기 (사진·모델은 빈 액자·빈 자리까지만 — 붙이는 건 ShowPhoto · ShowModel). 모르는 모양이면 false</summary>
    public bool Wear(string kind)
    {
        Skin s = null;
        if (skins != null) foreach (var k in skins) if (k != null && k.kind == kind) { s = k; break; }
        if (s == null) return false;
        ClearExtras();
        Kind = kind;
        var mf = GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = s.body;
        if (body != null) body.sharedMaterials = s.bodyMats ?? new Material[0];
        if (lid != null)
        {
            lid.localPosition = s.hinge;
            lid.localRotation = Quaternion.identity;
            var lf = lid.GetComponent<MeshFilter>();
            if (lf != null) lf.sharedMesh = s.lid;
            var lr = lid.GetComponent<MeshRenderer>();
            if (lr != null) lr.sharedMaterials = s.lidMats ?? new Material[0];
        }
        var bc = GetComponent<BoxCollider>();
        if (bc != null && s.boxSize != Vector3.zero) { bc.center = s.boxCenter; bc.size = s.boxSize; }
        seam = s.seam;
        return true;
    }

    /// <summary>액자에 사진을 끼운다 (가로·세로 비율 그대로 정사각형 칸 안에) — 앞뒤 두 장, 뒤에서 봐도 바로 읽힌다</summary>
    public void ShowPhoto(Texture tex)
    {
        if (tex == null || Kind != "photo") return;
        ClearExtras();
        var sh = Shader.Find("Sprites/Default");   // 늘 빌드에 들어간다 — 사진은 그늘 없이 환하게
        if (sh == null) return;
        var mat = new Material(sh) { name = "HuntPhoto", mainTexture = tex };
        owned.Add(mat);
        float w = PhotoSide, h = PhotoSide;
        if (tex.width >= tex.height) h *= (float)tex.height / Mathf.Max(1, tex.width);
        else w *= (float)tex.width / Mathf.Max(1, tex.height);
        var quad = PhotoQuad(w, h);
        owned.Add(quad);
        for (int side = 0; side < 2; side++)
        {
            var go = new GameObject(side == 0 ? "PhotoFront" : "PhotoBack");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, PhotoY, side == 0 ? -PhotoZ : PhotoZ);
            go.transform.localRotation = side == 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = body == null || body.enabled;
            extras.Add(go);
            extraRenderers.Add(mr);
        }
        Photo = tex;
    }

    /// <summary>3D 모델(GLB)을 붙인다 — 받은 바이트를 GLBModelLoader.PreloadCache 에 미리 넣어 두면 내려받지 않는다. 못 받으면 보물상자로</summary>
    public void ShowModel(string url)
    {
        if (string.IsNullOrEmpty(url) || Kind != "model") return;
        ClearExtras();
        StartCoroutine(ModelCo(url, dressGen));
    }

    private IEnumerator PhotoCo(string url, int gen)
    {
        Loading = true;
        using (var req = UnityWebRequestTexture.GetTexture(R0926HuntApi.Url(url), true))
        {
            req.timeout = 20;
            yield return req.SendWebRequest();
            if (gen != dressGen) yield break;
            Loading = false;
            var tex = req.result == UnityWebRequest.Result.Success ? DownloadHandlerTexture.GetContent(req) : null;
            if (tex == null) { Wear("chest"); yield break; }
            ShowPhoto(tex);
            owned.Add(tex);   // 붙인 뒤에 — 붙이며 앞서 붙인 것들을 지운다
        }
    }

    private IEnumerator ModelCo(string url, int gen)
    {
        Loading = true;
        var holder = new GameObject("Model");
        holder.transform.SetParent(transform, false);
        extras.Add(holder);
        modelHolder = holder.transform;
        var loader = holder.AddComponent<GLBModelLoader>();
        yield return null;   // 불러오기 칸(GLBContainer)은 GLBModelLoader.Start 가 만든다
        bool ok = false;
        if (gen == dressGen && loader != null) yield return loader.LoadGLBModelCoroutine(R0926HuntApi.Url(url), 1f, r => ok = r);
        if (gen != dressGen || holder == null) yield break;
        Loading = false;
        if (!ok || !loader.HasLoadedModel()) { Wear("chest"); yield break; }
        Model = loader.GetLoadedModel();
        Fit(holder.transform, Model.transform);
        holder.GetComponentsInChildren(true, extraRenderers);
        foreach (var r in extraRenderers) r.enabled = body == null || body.enabled;
    }

    // 가장 긴 변을 보물상자 폭에 맞추고 가운데를 상자 가운데로. 모델 뿌리 좌표로 잰다 — 불러오기 연출이 모델을 0.85배에서 키우고
    // 로딩 고리도 잠깐 남아 있어 월드 경계로 재면 작게 붙는다(10-24 시험: 0.68). 뿌리의 끝 크기는 불러올 때 준 배율 1
    private static void Fit(Transform holder, Transform model)
    {
        var toHolder = holder.worldToLocalMatrix * model.parent.localToWorldMatrix * Matrix4x4.TRS(model.localPosition, model.localRotation, Vector3.one) * model.worldToLocalMatrix;
        bool any = false;
        var b = new Bounds();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var lb = r.localBounds;
            if (lb.extents == Vector3.zero) continue;
            var smr = r as SkinnedMeshRenderer;
            var m = toHolder * (smr != null && smr.rootBone != null ? smr.rootBone : r.transform).localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
            }
        }
        float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (!any || size < 1e-4f) return;
        float k = ModelSize / size;
        holder.localScale = Vector3.one * k;
        holder.localPosition = -b.center * k;
    }

    private void ClearExtras()
    {
        dressGen++;
        Loading = false;
        foreach (var go in extras) if (go != null) Destroy(go);
        extras.Clear();
        extraRenderers.Clear();
        foreach (var o in owned) if (o != null) Destroy(o);   // 미리보기는 같은 보물에 사진을 여러 번 갈아 끼운다
        owned.Clear();
        modelHolder = null;
        Model = null;
        Photo = null;
    }

    private static Mesh PhotoQuad(float w, float h)
    {
        var m = new Mesh { name = "HuntPhotoQuad" };
        m.SetVertices(new[] { new Vector3(-w / 2f, -h / 2f, 0f), new Vector3(w / 2f, -h / 2f, 0f), new Vector3(-w / 2f, h / 2f, 0f), new Vector3(w / 2f, h / 2f, 0f) });
        m.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
        m.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
        m.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);   // 앞(-Z 쪽)에서 보이는 면
        m.RecalculateBounds();
        return m;
    }

    /// <summary>찾았다 — 이쪽을 돌아보고 뚜껑이 열리며 빛기둥·별빛이 솟은 뒤 작아지며 사라진다 (뿌리째, 약 1.2초 — 축하 화면의 상자가 뜨기 전에 비킨다)</summary>
    public void Open()
    {
        if (opening || !isActiveAndEnabled) return;
        opening = true;
        rattle = false;
        StartCoroutine(OpenCo());
    }

    private IEnumerator OpenCo()
    {
        var target = GetComponent<Target>();
        if (target != null) target.enabled = false;   // 박스·화살표를 내린다
        Transform root = transform.parent != null ? transform.parent : transform;
        bool seen = body != null && body.enabled;     // 복구 중(렌더러 숨김)이면 빛 연출 없이 사라진다
        var cam = Camera.main;

        // 1) 이쪽을 돌아본다 — 살짝 들썩이며
        float y0 = yaw, y1 = yaw;
        if (cam != null)
        {
            Vector3 d = transform.position - cam.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) y1 = y0 + Mathf.DeltaAngle(y0, Quaternion.LookRotation(d).eulerAngles.y);
        }
        yield return Tween(0.22f, k =>
        {
            yaw = Mathf.Lerp(y0, y1, k * k * (3f - 2f * k));
            wobble = Mathf.Sin(k * Mathf.PI * 3f) * 5f * (1f - k);
        });
        wobble = 0f;

        // 2) 뚜껑이 열리고 빛기둥·별빛이 솟는다
        Transform beam = seen ? Beam(root) : null;
        if (seen) Fountain(root);
        yield return Tween(0.4f, k =>
        {
            if (lid != null) lid.localRotation = Quaternion.Euler(LidOpen * BackOut(k), 0f, 0f);
            BeamAt(beam, cam, Mathf.Clamp01(k * 1.6f), Mathf.Clamp01(k * 2f));
        });
        // 3) 잠깐 빛나다
        yield return Tween(0.2f, k => BeamAt(beam, cam, 1f + 0.05f * Mathf.Sin(k * Mathf.PI), 1f));
        // 4) 작아지며 사라진다
        Vector3 s0 = root.localScale;
        yield return Tween(0.35f, k =>
        {
            root.localScale = s0 * (1f - k * k);
            BeamAt(beam, cam, 1f, 1f - k);
        });
        gone = true;
        Destroy(root.gameObject);
    }

    // ── 둘레 별빛 ────────────────────────────────────────────
    private void Twinkle()
    {
        var mat = StarSparkle.Material;
        if (mat == null) return;
        var go = new GameObject("Twinkle");
        go.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
        go.transform.localPosition = transform.localPosition;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 2f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;   // 꺼졌다 켜져도(백그라운드 복귀) 다시 핀다
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.98f, 0.9f), new Color(1f, 0.9f, 0.62f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // 줌을 따른다
        main.maxParticles = 30;
        var em = ps.emission;
        em.rateOverTime = 7f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.85f;
        sh.radiusThickness = 0.3f;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        vel.y = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);   // 천천히 피어오른다
        vel.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, StarSparkle.TwinkleCurve(2));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.minParticleSize = 0.003f;   // 멀리서도 점으로는 보이게
        r.maxParticleSize = 0.08f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }

    // ── 열 때: 위로 솟는 별빛 (월드에 따로 두어 상자가 사라진 뒤에도 마저 흩날린다) ──
    private void Fountain(Transform root)
    {
        var mat = StarSparkle.Material;
        if (mat == null) return;
        float s = root.lossyScale.x;
        var go = new GameObject("HuntChestFountain");
        go.transform.SetPositionAndRotation(transform.TransformPoint(0f, seam, 0f), Quaternion.Euler(-90f, 0f, 0f));
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f * s, 3f * s);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f * s, 0.14f * s);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.88f, 0.55f));
        main.gravityModifier = 0.35f * s;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26), new ParticleSystem.Burst(0.12f, 16) });
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 24f;
        sh.radius = 0.28f * s;
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = 6f * s;
        lim.dampen = 0.08f;   // 솟았다가 공중에서 느려지며 흩날린다
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f * s;
        noise.frequency = 0.6f;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, StarSparkle.TwinkleCurve(3));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.minParticleSize = 0.003f;
        r.maxParticleSize = 0.1f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        Destroy(go, 2.2f);
    }

    // ── 열 때: 상자 속에서 솟는 빛기둥 (늘 이쪽을 보는 세로 판) ──
    private Transform Beam(Transform root)
    {
        if (!BeamAssets()) return null;
        var go = new GameObject("Beam");
        go.transform.SetParent(root, false);
        go.transform.position = transform.TransformPoint(0f, seam, 0f);
        go.transform.localScale = Vector3.zero;
        go.AddComponent<MeshFilter>().sharedMesh = beamMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = beamMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go.transform;
    }

    private static void BeamAt(Transform beam, Camera cam, float grow, float alpha)
    {
        if (beam == null) return;
        beam.localScale = new Vector3(0.75f * (0.55f + 0.45f * grow), 1.9f * grow, 1f);
        if (cam != null)
        {
            Vector3 d = beam.position - cam.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) beam.rotation = Quaternion.LookRotation(d);
        }
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor(ColorId, new Color(1f, 0.9f, 0.62f, 0.8f * Mathf.Clamp01(alpha)));
        beam.GetComponent<Renderer>().SetPropertyBlock(mpb);
    }

    // 아래가 밝고 위·양옆으로 사그라드는 흰 빛 (앞뒤 두 면) — Sprites/Default 는 늘 빌드에 들어간다 (StarSparkle 과 같이)
    private static bool BeamAssets()
    {
        if (beamMat != null) return true;
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) return false;
        const int w = 128, h = 256;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f), v = (y + 0.5f) / h;
                float side = Mathf.Pow(Mathf.Clamp01(1f - u), 2.2f);
                float up = Mathf.Pow(1f - v, 1.6f) * Mathf.SmoothStep(0f, 1f, v / 0.06f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(side * up) * 255f));
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "HuntChestBeam", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        beamMat = new Material(sh) { name = "HuntChestBeam", mainTexture = tex };
        beamMesh = new Mesh { name = "HuntChestBeam" };
        beamMesh.SetVertices(new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f) });
        beamMesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
        beamMesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
        beamMesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 }, 0);
        beamMesh.RecalculateBounds();
        return true;
    }

    // ── 작은 도구 ───────────────────────────────────────────
    private static IEnumerator Tween(float secs, Action<float> step)
    {
        for (float t = 0f; t < secs; t += Time.unscaledDeltaTime)
        {
            step(t / secs);
            yield return null;
        }
        step(1f);
    }

    private static float BackOut(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
