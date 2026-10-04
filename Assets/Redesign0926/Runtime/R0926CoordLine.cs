using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장소 추가 카드의 '좌표' — 지금 위치를 흐린 흰 글씨로 늘 보여 준다 (누를 것 없음).
/// 등록에 쓰는 값은 업로드 매니저의 입력칸이 그대로 갖고 있다 (보이지 않게만 해 둠).
/// </summary>
public class R0926CoordLine : MonoBehaviour
{
    [SerializeField] private Text text;
    [Tooltip("업로드 매니저가 채우는 원래 칸 — GPS 가 없을 때 그 안내 글을 대신 보여 준다")]
    [SerializeField] private InputField source;

    private float next;

    private void OnEnable() { next = 0f; }

    private void Update()
    {
        if (text == null || Time.unscaledTime < next) return;
        next = Time.unscaledTime + 1f;
        string s;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            var d = Input.location.lastData;
            s = d.latitude.ToString("F5") + ",  " + d.longitude.ToString("F5");
        }
#if UNITY_EDITOR
        else if (VirtualLocation.Instance != null)
            s = VirtualLocation.Instance.Latitude.ToString("F5") + ",  " + VirtualLocation.Instance.Longitude.ToString("F5");
#endif
        else if (source != null && !string.IsNullOrEmpty(source.text))
            s = source.text.StartsWith("Lat") ? FromSource(source.text) : source.text;   // 이미 받은 좌표 · '위치 확인 중' 같은 안내
        else
            s = L("위치 확인 중…", "Getting location…", "位置を確認中…", "正在获取位置…", "Obteniendo ubicación…");
        if (text.text != s) text.text = s;
    }

    // 업로드 매니저의 "Lat:36.6361,Lon:126.8280,Alt:0.00" → "36.6361,  126.8280"
    private static string FromSource(string t)
    {
        string lat = null, lon = null;
        foreach (var part in t.Split(','))
        {
            if (part.StartsWith("Lat:")) lat = part.Substring(4);
            else if (part.StartsWith("Lon:")) lon = part.Substring(4);
        }
        return lat != null && lon != null ? lat + ",  " + lon : t;
    }

    private static string L(string ko, string en, string ja, string zh, string es)
    {
        switch (R0926LocalizedText.Lang())
        {
            case "ko": return ko;
            case "ja": return ja;
            case "zh": return zh;
            case "es": return es;
            default: return en;
        }
    }
}
