using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 장소 추가 ▸ 「이곳에 오브젝트를 추가하시겠습니까?」 ▸ AI 작업 — 대표 사진을 고른 옵션대로 손본다.
/// 11-12 대표님 「01 투명하게 · 02 만화톤 · 03 색연필 · 04 3D 캐릭터('요청사항')」 — 02~04 는 하나만. 01 은 02·03 과만 겹친다 (11-13 — 캐릭터는 투명하게가 필요 없다).
/// 지금은 폰 안에서 무료로: 사진은 영상처리(R0926AiImage), 캐릭터는 도형을 이어 짓는다(R0926AiCharacter · 받침 사진을 든 모습).
/// 외부 3D 제작 AI 를 붙일 땐 Provider 를 바꿔 끼운다 (같은 Job 을 받아 같은 Out 을 돌려주면 된다).
/// 사진 읽기 · 텍스처 만들기만 메인 스레드, 나머지는 배경 스레드.
/// </summary>
public static class R0926AiWork
{
    public enum Style { None, Toon, Pencil, Character }

    public sealed class Job
    {
        public Color32[] Px;      // Side x Side, 0 줄 = 아래 · null = 사진 없이 캐릭터만 (보물찾기 '3D 캐릭터 생성')
        public int Side;
        public bool Clear;
        public Style Style;
        public string Request;
        public int Seed;
    }

    public sealed class Out
    {
        public Color32[] Px;      // 손본 사진 (Side x Side)
        public bool Alpha;        // 바깥이 투명하다 — PNG 로 올린다
        public byte[] Glb;        // 3D 캐릭터
        public string Label;      // 캐릭터 요약 ("노란 강아지 · 모자")
        public R0926AiImage.ClearMode Clear;
    }

    public sealed class Result
    {
        public Texture2D Tex;
        public bool Alpha;
        public byte[] Glb;
        public string Label;
        public Style Style;
        public R0926AiImage.ClearMode Clear;
    }

    /// <summary>외부 AI 자리 — null 이면 폰 안에서 (배경 스레드에서 불린다 · Unity API 금지)</summary>
    public static Func<Job, Out> Provider;

    /// <summary>photo 를 손봐 done 에 넘긴다 (실패면 null). variant 를 바꾸면 같은 요청도 다른 캐릭터</summary>
    public static IEnumerator Run(Texture2D photo, bool clear, Style style, string request, int variant, Action<Result> done)
    {
        if (photo == null) { done?.Invoke(null); yield break; }
        var job = new Job
        {
            Px = Capture(photo, R0926AiImage.Side),
            Side = R0926AiImage.Side,
            Clear = clear,
            Style = style,
            Request = (request ?? "").Trim(),
        };
        job.Seed = Seed(job.Px, job.Request, variant);
        var work = Provider ?? Local;
        var task = Task.Run(() => work(job));
        while (!task.IsCompleted) yield return null;
        if (task.IsFaulted || task.Result == null || task.Result.Px == null)
        {
            Debug.LogError("[R0926AiWork] 작업 실패: " + (task.Exception != null ? task.Exception.GetBaseException().Message : "결과 없음"));
            done?.Invoke(null);
            yield break;
        }
        var o = task.Result;
        var tex = new Texture2D(job.Side, job.Side, TextureFormat.RGBA32, false) { name = "AiWork", wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(o.Px);
        tex.Apply(false, false);
        done?.Invoke(new Result { Tex = tex, Alpha = o.Alpha, Glb = o.Glb, Label = o.Label, Style = style, Clear = o.Clear });
    }

    /// <summary>사진 없이 요청사항만으로 3D 캐릭터 (보물찾기 '3D 캐릭터 생성') — done 에 Glb · Label (실패면 null). variant 를 바꾸면 같은 요청도 다른 모습</summary>
    public static IEnumerator Character(string request, int variant, Action<Result> done)
    {
        var job = new Job { Style = Style.Character, Request = (request ?? "").Trim() };
        job.Seed = Seed(null, job.Request, variant);
        var work = Provider ?? Local;
        var task = Task.Run(() => work(job));
        while (!task.IsCompleted) yield return null;
        var o = task.IsFaulted ? null : task.Result;
        if (o == null || o.Glb == null)
        {
            Debug.LogError("[R0926AiWork] 캐릭터 실패: " + (task.Exception != null ? task.Exception.GetBaseException().Message : "결과 없음"));
            done?.Invoke(null);
            yield break;
        }
        done?.Invoke(new Result { Glb = o.Glb, Label = o.Label, Style = Style.Character });
    }

    private static Out Local(Job j)
    {
        if (j.Px == null)   // 사진 없이 캐릭터만
        {
            var only = R0926AiCharacter.Parse(j.Request, j.Seed);
            return new Out { Glb = R0926AiCharacter.Build(only), Label = only.Label };
        }
        var orig = R0926AiImage.FromColors(j.Px, j.Side, j.Side);
        var img = j.Style == Style.Toon ? R0926AiImage.Toon(orig)
                : j.Style == Style.Pencil ? R0926AiImage.Pencil(orig)
                : orig;
        var mode = R0926AiImage.ClearMode.None;
        if (j.Clear && j.Style != Style.Character) img = R0926AiImage.Clear(orig, img, out mode);   // 캐릭터는 사진 그대로 팻말에 든다
        var o = new Out { Px = R0926AiImage.ToColors(img), Alpha = R0926AiImage.HasAlpha(img), Clear = mode };
        if (j.Style == Style.Character)
        {
            var spec = R0926AiCharacter.Parse(j.Request, j.Seed);
            o.Glb = R0926AiCharacter.Build(spec, o.Px, j.Side, o.Alpha);   // 손본 사진을 받침에 들고 선다
            o.Label = spec.Label;
        }
        return o;
    }

    // 자른 대표 사진을 Side x Side 로 — 읽기 꺼진 텍스처도 되게 GPU 로 옮겨 읽는다 (사진 칸과 같은 sRGB)
    private static Color32[] Capture(Texture2D src, int side)
    {
        var rt = RenderTexture.GetTemporary(side, side, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var tmp = new Texture2D(side, side, TextureFormat.RGBA32, false);
        tmp.ReadPixels(new Rect(0, 0, side, side), 0, 0, false);
        tmp.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = tmp.GetPixels32();
        UnityEngine.Object.Destroy(tmp);
        return px;
    }

    // 같은 사진 · 같은 요청 · 같은 차례면 같은 캐릭터 (FNV — string.GetHashCode 는 실행마다 다를 수 있다)
    private static int Seed(Color32[] px, string request, int variant)
    {
        unchecked
        {
            uint h = 2166136261;
            for (int i = 0; px != null && i < px.Length; i += 97)
            {
                h = (h ^ px[i].r) * 16777619;
                h = (h ^ px[i].g) * 16777619;
                h = (h ^ px[i].b) * 16777619;
            }
            foreach (char c in request) h = (h ^ c) * 16777619;
            h = (h ^ (uint)variant) * 16777619;
            return (int)(h & 0x7fffffff);
        }
    }
}
