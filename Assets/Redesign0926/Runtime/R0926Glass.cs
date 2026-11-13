using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 물방울메뉴 — 이 Image(알약)를 물방울 유리로 그린다. 기본메뉴면 아무것도 바꾸지 않는다(예전 그림 그대로).
/// 알약 크기 · 모서리 반지름을 정점에 실어 보내고(uv1 · uv2) 셰이더(R0926LiquidGlass)가 가장자리 굴절 · 반사광을 계산한다.
/// 비치는 뒤 화면은 R0926GlassBlur 가 흐려 둔 AR 카메라 화면 — 창(목록 · 메시지 …)이 열려 뒤가 AR 화면이 아닐 땐 비침 없이 테두리 빛만 남긴다.
/// </summary>
[RequireComponent(typeof(Image))]
public class R0926Glass : BaseMeshEffect
{
    [SerializeField] private Material glass;      // R0926LiquidGlass.mat
    [SerializeField] private Shader blur;          // R0926GlassBlur.shader — 빌드에 들어가도록 여기서 잡아 둔다
    [SerializeField] private float tint = 0.6f;    // 원래 그림의 알파 — 유리에 얹는 색의 진하기 (사라지는 중인지는 이 값에 대한 비율로 읽는다)
    [SerializeField] private GameObject[] covers;  // 이 창 중 하나가 켜져 있으면 뒤가 AR 화면이 아니다 (늘 켜진 틀이면 그 안의 켜졌다 꺼지는 것)

    /// <summary>지금 뒤 화면을 비추는 것들 — 하나도 없으면 흐림도 쉰다</summary>
    public static readonly HashSet<R0926Glass> Live = new HashSet<R0926Glass>();

    private Image img;
    private Material before;
    private bool on, seeThrough;

    public bool Showing => on;
    public bool SeeThrough => on && seeThrough;
    /// <summary>지금 뒤를 가린 창 이름 (시험용) — 없으면 빈 글</summary>
    public string CoveredBy { get { foreach (var o in covers ?? new GameObject[0]) if (o != null && o.activeInHierarchy) return o.name; return ""; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Live.Clear();

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!Application.isPlaying) return;   // 에디터에서 씬에 유리 재질이 저장되지 않게
        img = GetComponent<Image>();
        R0926MenuStyle.Changed += Restyle;
        Restyle();
    }

    protected override void OnDisable()
    {
        if (Application.isPlaying)
        {
            R0926MenuStyle.Changed -= Restyle;
            SetOn(false);
        }
        base.OnDisable();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        if (Application.isPlaying && isActiveAndEnabled) Restyle();
    }

    private void LateUpdate()
    {
        if (on) SetSeeThrough(!Covered());
    }

    private void Restyle()
    {
        if (img == null) return;
        var c = img.canvas;
        bool overlay = c != null && c.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
        SetOn(R0926MenuStyle.Droplet && glass != null && overlay);
    }

    private bool Covered()
    {
        if (covers == null) return false;
        foreach (var o in covers)
            if (o != null && o.activeInHierarchy) return true;
        return false;
    }

    private void SetOn(bool v)
    {
        if (img == null || on == v) return;
        on = v;
        if (v)
        {
            before = img.material == img.defaultMaterial ? null : img.material;
            // uv1 · uv2 를 셰이더로 보내려면 캔버스가 그 채널을 실어야 한다 — 안쪽 캔버스(따로 정렬하는 토스트)부터 맨 위까지
            for (var cv = img.canvas; cv != null; cv = cv.transform.parent != null ? cv.transform.parent.GetComponentInParent<Canvas>() : null)
                cv.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            img.material = glass;
            SetSeeThrough(!Covered());
        }
        else
        {
            SetSeeThrough(false);
            img.material = before;
        }
        img.SetVerticesDirty();
    }

    private void SetSeeThrough(bool v)
    {
        if (seeThrough == v) return;
        seeThrough = v;
        if (v)
        {
            Live.Add(this);
            R0926GlassBlur.Need(blur);
        }
        else
        {
            Live.Remove(this);
            if (Live.Count == 0) R0926GlassBlur.Rest();
        }
        img.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!on || img == null) return;
        Rect r = ((RectTransform)transform).rect;
        Vector2 mid = r.center;
        float hw = r.width * 0.5f, hh = r.height * 0.5f;
        float rad = Mathf.Min(hw, hh);
        // 늘린 알약(Sliced)은 그림 모서리 크기가 곧 둥근 반지름 — Image 가 그리는 것과 같은 셈
        if (img.type == Image.Type.Sliced && img.sprite != null)
        {
            float ppu = img.pixelsPerUnit * img.pixelsPerUnitMultiplier;
            if (ppu > 0f && img.sprite.border.x > 0f) rad = Mathf.Min(rad, img.sprite.border.x / ppu);
        }
        var size = new Vector4(hw, hh, rad, tint > 0f ? tint : Mathf.Max(img.color.a, 0.01f));
        float see = seeThrough ? 1f : 0f;
        var v = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            v.uv1 = new Vector4(v.position.x - mid.x, v.position.y - mid.y, see, 0f);
            v.uv2 = size;
            vh.SetUIVertex(v, i);
        }
    }
}
