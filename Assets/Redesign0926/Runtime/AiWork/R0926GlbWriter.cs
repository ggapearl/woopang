using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// AI 작업 ▸ 3D 캐릭터를 GLB(glTF 2.0 바이너리)로 쓴다 — 앱의 GLB 로더(glTFast)가 그대로 읽는 최소한만.
/// 노드 TRS · 메시(위치 · 법선 · UV · uint16 색인) · 재질(기본색 그림, 언릿 확장) · 그림(JPEG/PNG를 BIN 안에) · 애니메이션(LINEAR).
/// 좌표는 glTF 그대로(오른손 · +Y 위 · +Z 앞) — 로더가 왼손으로 바꾼다. 배경 스레드에서 써도 된다(Unity API 없음).
/// </summary>
public sealed class R0926GlbWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly MemoryStream bin = new MemoryStream();
    private readonly List<string> views = new List<string>();
    private readonly List<string> accessors = new List<string>();
    private readonly List<string> images = new List<string>();
    private readonly List<string> textures = new List<string>();
    private readonly List<string> materials = new List<string>();
    private readonly List<string> meshes = new List<string>();
    private readonly List<Node> nodes = new List<Node>();
    private readonly List<string> channels = new List<string>();
    private readonly List<string> samplers = new List<string>();
    private readonly Dictionary<float[], int> timeAccessors = new Dictionary<float[], int>();

    private sealed class Node
    {
        public string Name;
        public Vector3 T;
        public Quaternion R;
        public Vector3 S;
        public int Mesh = -1;
        public readonly List<int> Children = new List<int>();
    }

    public int NodeCount => nodes.Count;

    // ── 그림 · 재질 ──
    public int AddTexture(byte[] data, string mime)
    {
        int view = AddView(data, -1);
        images.Add($"{{\"bufferView\":{view},\"mimeType\":\"{mime}\"}}");
        textures.Add($"{{\"sampler\":0,\"source\":{images.Count - 1}}}");
        return textures.Count - 1;
    }

    /// <param name="mask">알파 0.5 아래는 잘라 낸다 (투명한 사진 팻말)</param>
    public int AddMaterial(string name, int texture, bool mask, bool doubleSided = false)
    {
        var sb = new StringBuilder();
        sb.Append("{\"name\":").Append(Q(name));
        sb.Append(",\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":").Append(texture).Append("},\"metallicFactor\":0,\"roughnessFactor\":1}");
        if (mask) sb.Append(",\"alphaMode\":\"MASK\",\"alphaCutoff\":0.5");
        if (doubleSided) sb.Append(",\"doubleSided\":true");
        sb.Append(",\"extensions\":{\"KHR_materials_unlit\":{}}}");   // 그늘은 그림에 구워 두었다
        materials.Add(sb.ToString());
        return materials.Count - 1;
    }

    // ── 메시 ──
    public int AddMesh(string name, Vector3[] pos, Vector3[] nrm, Vector2[] uv, ushort[] idx, int material)
    {
        Vector3 mn = pos[0], mx = pos[0];
        var pb = new byte[pos.Length * 12];
        var nb = new byte[pos.Length * 12];
        var ub = new byte[pos.Length * 8];
        for (int i = 0; i < pos.Length; i++)
        {
            mn = Vector3.Min(mn, pos[i]); mx = Vector3.Max(mx, pos[i]);
            Put(pb, i * 12, pos[i].x); Put(pb, i * 12 + 4, pos[i].y); Put(pb, i * 12 + 8, pos[i].z);
            Vector3 n = nrm[i].sqrMagnitude > 1e-12f ? nrm[i].normalized : Vector3.up;
            Put(nb, i * 12, n.x); Put(nb, i * 12 + 4, n.y); Put(nb, i * 12 + 8, n.z);
            Put(ub, i * 8, uv[i].x); Put(ub, i * 8 + 4, uv[i].y);
        }
        var ib = new byte[idx.Length * 2];
        for (int i = 0; i < idx.Length; i++) { ib[i * 2] = (byte)(idx[i] & 0xff); ib[i * 2 + 1] = (byte)(idx[i] >> 8); }
        int ap = AddAccessor(AddView(pb, 34962), 5126, pos.Length, "VEC3", $"\"min\":[{F(mn.x)},{F(mn.y)},{F(mn.z)}],\"max\":[{F(mx.x)},{F(mx.y)},{F(mx.z)}]");
        int an = AddAccessor(AddView(nb, 34962), 5126, pos.Length, "VEC3", null);
        int au = AddAccessor(AddView(ub, 34962), 5126, pos.Length, "VEC2", null);
        int ai = AddAccessor(AddView(ib, 34963), 5123, idx.Length, "SCALAR", null);
        meshes.Add($"{{\"name\":{Q(name)},\"primitives\":[{{\"attributes\":{{\"POSITION\":{ap},\"NORMAL\":{an},\"TEXCOORD_0\":{au}}},\"indices\":{ai},\"material\":{material}}}]}}");
        return meshes.Count - 1;
    }

    // ── 노드 ──
    public int AddNode(string name, Vector3 t, Quaternion r, Vector3 s, int mesh = -1, int parent = -1)
    {
        nodes.Add(new Node { Name = name, T = t, R = r, S = s, Mesh = mesh });
        int id = nodes.Count - 1;
        if (parent >= 0) nodes[parent].Children.Add(id);
        return id;
    }

    /// <summary>다 만든 뒤에야 정해지는 자리 · 크기 (예: 발바닥을 0에 · 키 맞춤)</summary>
    public void SetNode(int node, Vector3 t, Quaternion r, Vector3 s)
    {
        var n = nodes[node];
        n.T = t; n.R = r; n.S = s;
    }

    // ── 애니메이션 ──
    public void AddTranslation(int node, float[] times, Vector3[] v) => AddChannel(node, "translation", times, Vec3(v), "VEC3");
    public void AddScale(int node, float[] times, Vector3[] v) => AddChannel(node, "scale", times, Vec3(v), "VEC3");
    public void AddRotation(int node, float[] times, Quaternion[] q)
    {
        var b = new byte[q.Length * 16];
        for (int i = 0; i < q.Length; i++)
        {
            var r = q[i];
            if (i > 0 && Quaternion.Dot(q[i - 1], r) < 0f) r = new Quaternion(-r.x, -r.y, -r.z, -r.w);   // 이웃 키와 같은 반구 — 거꾸로 한 바퀴 돌지 않게
            q[i] = r;
            Put(b, i * 16, r.x); Put(b, i * 16 + 4, r.y); Put(b, i * 16 + 8, r.z); Put(b, i * 16 + 12, r.w);
        }
        AddChannel(node, "rotation", times, b, "VEC4");
    }

    private void AddChannel(int node, string path, float[] times, byte[] values, string type)
    {
        if (!timeAccessors.TryGetValue(times, out int input))
        {
            var tb = new byte[times.Length * 4];
            for (int i = 0; i < times.Length; i++) Put(tb, i * 4, times[i]);
            input = AddAccessor(AddView(tb, -1), 5126, times.Length, "SCALAR", $"\"min\":[{F(times[0])}],\"max\":[{F(times[times.Length - 1])}]");
            timeAccessors[times] = input;
        }
        int output = AddAccessor(AddView(values, -1), 5126, times.Length, type, null);
        samplers.Add($"{{\"input\":{input},\"output\":{output},\"interpolation\":\"LINEAR\"}}");
        channels.Add($"{{\"sampler\":{samplers.Count - 1},\"target\":{{\"node\":{node},\"path\":\"{path}\"}}}}");
    }

    private static byte[] Vec3(Vector3[] v)
    {
        var b = new byte[v.Length * 12];
        for (int i = 0; i < v.Length; i++) { Put(b, i * 12, v[i].x); Put(b, i * 12 + 4, v[i].y); Put(b, i * 12 + 8, v[i].z); }
        return b;
    }

    // ── 마무리 ──
    public byte[] Build(int root, string animName)
    {
        var js = new StringBuilder(8192);
        js.Append("{\"asset\":{\"version\":\"2.0\",\"generator\":\"WOOPANG AI\"}");
        js.Append(",\"extensionsUsed\":[\"KHR_materials_unlit\"]");
        js.Append(",\"scene\":0,\"scenes\":[{\"nodes\":[").Append(root).Append("]}]");
        js.Append(",\"nodes\":[");
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (i > 0) js.Append(',');
            js.Append("{\"name\":").Append(Q(n.Name));
            if (n.T != Vector3.zero) js.Append($",\"translation\":[{F(n.T.x)},{F(n.T.y)},{F(n.T.z)}]");
            if (n.R != Quaternion.identity) js.Append($",\"rotation\":[{F(n.R.x)},{F(n.R.y)},{F(n.R.z)},{F(n.R.w)}]");
            if (n.S != Vector3.one) js.Append($",\"scale\":[{F(n.S.x)},{F(n.S.y)},{F(n.S.z)}]");
            if (n.Mesh >= 0) js.Append(",\"mesh\":").Append(n.Mesh);
            if (n.Children.Count > 0) js.Append(",\"children\":[").Append(string.Join(",", n.Children)).Append(']');
            js.Append('}');
        }
        js.Append(']');
        js.Append(",\"meshes\":[").Append(string.Join(",", meshes)).Append(']');
        if (materials.Count > 0) js.Append(",\"materials\":[").Append(string.Join(",", materials)).Append(']');
        if (textures.Count > 0)
        {
            js.Append(",\"textures\":[").Append(string.Join(",", textures)).Append(']');
            js.Append(",\"images\":[").Append(string.Join(",", images)).Append(']');
            js.Append(",\"samplers\":[{\"magFilter\":9729,\"minFilter\":9987,\"wrapS\":33071,\"wrapT\":33071}]");
        }
        if (channels.Count > 0)
            js.Append(",\"animations\":[{\"name\":").Append(Q(animName)).Append(",\"channels\":[").Append(string.Join(",", channels))
              .Append("],\"samplers\":[").Append(string.Join(",", samplers)).Append("]}]");
        js.Append(",\"accessors\":[").Append(string.Join(",", accessors)).Append(']');
        js.Append(",\"bufferViews\":[").Append(string.Join(",", views)).Append(']');
        js.Append(",\"buffers\":[{\"byteLength\":").Append(bin.Length).Append("}]}");

        byte[] json = Encoding.UTF8.GetBytes(js.ToString());
        int jsonLen = (json.Length + 3) & ~3;
        int binLen = (int)((bin.Length + 3) & ~3);
        int total = 12 + 8 + jsonLen + 8 + binLen;
        using (var ms = new MemoryStream(total))
        using (var w = new BinaryWriter(ms))
        {
            w.Write(0x46546C67u); w.Write(2u); w.Write((uint)total);
            w.Write((uint)jsonLen); w.Write(0x4E4F534Au);
            w.Write(json);
            for (int i = json.Length; i < jsonLen; i++) w.Write((byte)0x20);
            w.Write((uint)binLen); w.Write(0x004E4942u);
            w.Write(bin.GetBuffer(), 0, (int)bin.Length);
            for (long i = bin.Length; i < binLen; i++) w.Write((byte)0);
            w.Flush();
            return ms.ToArray();
        }
    }

    // ── 바탕 ──
    private int AddView(byte[] data, int target)
    {
        while (bin.Length % 4 != 0) bin.WriteByte(0);
        long off = bin.Length;
        bin.Write(data, 0, data.Length);
        views.Add($"{{\"buffer\":0,\"byteOffset\":{off},\"byteLength\":{data.Length}" + (target > 0 ? $",\"target\":{target}" : "") + "}");
        return views.Count - 1;
    }

    private int AddAccessor(int view, int componentType, int count, string type, string extra)
    {
        accessors.Add($"{{\"bufferView\":{view},\"componentType\":{componentType},\"count\":{count},\"type\":\"{type}\"" + (extra != null ? "," + extra : "") + "}");
        return accessors.Count - 1;
    }

    private static void Put(byte[] b, int off, float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
        var bytes = BitConverter.GetBytes(v);
        if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
        Buffer.BlockCopy(bytes, 0, b, off, 4);
    }

    private static string F(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
        return v.ToString("R", Inv);   // 정확히 되돌아오는 값 — 접근자 min/max 가 실제 값과 어긋나지 않게
    }

    private static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
