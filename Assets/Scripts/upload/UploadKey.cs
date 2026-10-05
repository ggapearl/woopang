using System;

/// <summary>
/// 장소 올리기 중복 방지 키(upload_id). 서버는 같은 키가 다시 오면 저장하지 않고 처음과 같은 성공으로 답한다.
/// 같은 내용을 다시 보내면(실패·시간 초과 뒤 다시 누름 — 앞 요청이 서버에 닿았을 수 있다) 같은 키,
/// 내용이 바뀌면(이름·사진·분류 등 — 다른 장소) 새 키. 성공·입력 초기화 때 Reset.
/// 예전엔 한 번 만든 키를 성공 전까지 계속 써서, 실패 뒤 다른 장소를 올려도 같은 키가 가 서버가 새 장소를 버릴 수 있었다.
/// </summary>
public sealed class UploadKey
{
    private string id;
    private ulong content;

    /// <summary>이 내용으로 보낼 키 — 지난번과 내용이 같으면 같은 키</summary>
    public string For(ulong contentHash)
    {
        if (id == null || contentHash != content)
        {
            id = Guid.NewGuid().ToString("N");
            content = contentHash;
        }
        return id;
    }

    public void Reset() => id = null;

    // 보낼 내용의 지문 (FNV-1a 64 — 암호용 아님)
    private const ulong Prime = 1099511628211UL;
    public const ulong Seed = 14695981039346656037UL;

    public static ulong Mix(ulong h, string s)
    {
        if (s != null)
            foreach (char c in s) { h ^= c; h *= Prime; }
        h ^= 0x1F; h *= Prime;   // 칸 구분
        return h;
    }

    public static ulong Mix(ulong h, byte[] bytes)
    {
        if (bytes != null)
            foreach (byte b in bytes) { h ^= b; h *= Prime; }
        h ^= 0x1E; h *= Prime;
        return h;
    }
}
