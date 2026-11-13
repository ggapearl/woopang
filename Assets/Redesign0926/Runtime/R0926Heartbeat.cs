using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장소 추가 ▸ 샘플 오브젝트(미리보기 큐브)의 심장 박동 — 11-09 대표님 「2초에 한번씩 짧게 2번 (심장처럼) 오브젝트도 터치했을 때처럼
/// 살짝 두번 커졌다작아졌다하면서 반짝이도록」.
///  · 2초마다 쿵-쿵: 누를 때(Object3DTouchHaptic — 실제로는 1.09배쯤)처럼 살짝 커졌다 돌아오고, 그때마다 테두리가 번쩍.
///  · 큐브 재질의 느린 숨쉬기(_MinGlow 0.5 · 약 3초 주기)는 이 큐브의 재질 복사본에서만 끄고 _EdgeIntensity 를 여기서 움직인다 — 다른 큐브는 그대로.
///  · 짜잔(ARPreviewController) 순간의 번쩍임도 여기서(Flash). 박동은 다 튀어나온 뒤 Beat 로 켠다 — 그 전엔 크기를 짜잔이 움직인다.
/// 크기는 뿌리(0000_Cube)에 준다 — 누를 때 커지는 건 자식 Cube 라 둘이 겹쳐도 서로 덮어쓰지 않는다.
/// </summary>
public class R0926Heartbeat : MonoBehaviour
{
    private const float Interval = 2f;
    private const float LubGrow = 0.09f, DubGrow = 0.06f, DubAt = 0.3f;
    private const float RestGlow = 0.8f, BeatGlow = 1.4f, FlashGlow = 2.7f;   // 재질 원래 세기에 곱한다 — 쉴 때 0.8 은 예전 숨쉬기의 가운데쯤

    private static readonly int EdgeIntensity = Shader.PropertyToID("_EdgeIntensity");
    private static readonly int MinGlow = Shader.PropertyToID("_MinGlow");
    private static readonly int GlowPulseSpeed = Shader.PropertyToID("_GlowPulseSpeed");

    private Material[] mats;
    private float[] baseEdge;
    private Vector3 rest;
    private bool beating;
    private float beatFrom, flashAt = -99f, flashFor = 1f;

    private void Awake()
    {
        var list = new List<Material>();
        var bases = new List<float>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var m = r.material;   // 이 큐브의 복사본 — 사진을 입힐 때 이미 복사돼 있다
            if (m == null || !m.HasProperty(EdgeIntensity)) continue;
            if (m.HasProperty(MinGlow)) m.SetFloat(MinGlow, 1f);
            if (m.HasProperty(GlowPulseSpeed)) m.SetFloat(GlowPulseSpeed, 0f);
            list.Add(m);
            bases.Add(m.GetFloat(EdgeIntensity));
        }
        mats = list.ToArray();
        baseEdge = bases.ToArray();
    }

    /// <summary>테두리가 확 밝았다가 seconds 동안 가라앉는다</summary>
    public void Flash(float seconds)
    {
        flashAt = Time.time;
        flashFor = Mathf.Max(0.01f, seconds);
    }

    /// <summary>박동 시작 — 첫 쿵은 firstIn 초 뒤. 지금 크기를 쉬는 크기로 삼는다</summary>
    public void Beat(float firstIn)
    {
        rest = transform.localScale;
        beatFrom = Time.time + firstIn;
        beating = true;
    }

    private void LateUpdate()
    {
        float env = 0f;
        if (beating)
        {
            float x = Time.time - beatFrom, grow = 0f;
            if (x >= 0f)
            {
                x %= Interval;
                float lub = Pulse(x, 0.08f, 0.2f), dub = Pulse(x - DubAt, 0.07f, 0.18f);
                grow = LubGrow * lub + DubGrow * dub;
                env = Mathf.Max(lub, 0.75f * dub);
            }
            transform.localScale = rest * (1f + grow);
        }
        float f = 1f - (Time.time - flashAt) / flashFor;
        float k = RestGlow + BeatGlow * env + (f > 0f ? FlashGlow * f * f : 0f);
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null) mats[i].SetFloat(EdgeIntensity, baseEdge[i] * k);
    }

    private void OnDisable()
    {
        if (beating) transform.localScale = rest;
    }

    // 0 → 1 → 0 — rise 초 동안 솟고 fall 초 동안 가라앉는다
    private static float Pulse(float x, float rise, float fall)
    {
        if (x <= 0f || x >= rise + fall) return 0f;
        return x < rise ? Mathf.Sin(x / rise * Mathf.PI * 0.5f) : 0.5f + 0.5f * Mathf.Cos((x - rise) / fall * Mathf.PI);
    }
}
