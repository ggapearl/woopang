using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 'AI에게 묻기' — 지금 동네와 근처 장소 목록으로 질문을 만들어, 사용자가 쓰는 AI 앱(ChatGPT·Claude·Gemini)에 넘긴다.
/// 우팡 서버는 AI 를 부르지 않는다(비용 0). 좌표는 넘기지 않고 동네 이름만 쓴다.
/// </summary>
public class R0926AskAI : MonoBehaviour
{
    [SerializeField] private R0926PlaceRows rows;
    [SerializeField] private Text areaSource;     // 위치 칩 글자 (둘째 줄이 동네 이름)
    [SerializeField] private Text radiusSource;   // 거리 슬라이더 값 (예: 5.0km)
    [SerializeField] private GameObject sheet;
    [Tooltip("질문에 넣을 최대 장소 수")]
    [SerializeField] private int maxPlaces = 30;

    public void Open() { if (sheet != null) sheet.SetActive(true); }
    public void Close() { if (sheet != null) sheet.SetActive(false); }

    public void AskChatGPT() => Go("https://chatgpt.com/?q=" + Uri.EscapeDataString(Prompt()), false);
    public void AskClaude() => Go("https://claude.ai/new?q=" + Uri.EscapeDataString(Prompt()), false);
    public void AskGemini() => Go("https://gemini.google.com/app", true);   // 미리 채우기가 없어 복사 후 연다
    public void CopyOnly() { Copy(); Close(); }

    private void Go(string url, bool copyFirst)
    {
        if (copyFirst) Copy();
        Application.OpenURL(url);
        Close();
    }

    private void Copy()
    {
        GUIUtility.systemCopyBuffer = Prompt();
        if (ToastManager.Instance != null)
            ToastManager.Instance.ShowSuccess(L("질문을 복사했어요. AI 앱에 붙여 넣으세요", "Question copied. Paste it into your AI app",
                "質問をコピーしました。AIアプリに貼り付けてください", "已复制问题，请粘贴到 AI 应用", "Pregunta copiada. Pégala en tu app de IA"));
    }

    private string Prompt()
    {
        string area = Area();
        string radius = radiusSource != null ? radiusSource.text : "";
        var sb = new StringBuilder(1024);
        sb.Append(L(
            $"나는 지금 {area} 근처에 있어. 우팡(WOOPANG) 앱에서 반경 {radius} 안에 보이는 장소 목록이야.\n",
            $"I'm near {area} right now. These are places within {radius} in the WOOPANG app.\n",
            $"今 {area} の近くにいます。WOOPANGアプリで半径{radius}以内の場所の一覧です。\n",
            $"我现在在 {area} 附近。以下是 WOOPANG 应用中 {radius} 范围内的地点。\n",
            $"Estoy cerca de {area}. Estos son lugares a menos de {radius} en la app WOOPANG.\n"));
        int n = 0;
        if (rows != null)
        {
            foreach (var e in rows.Entries)
            {
                if (n >= maxPlaces) break;
                n++;
                sb.Append(n).Append(". ").Append(e.name).Append(" (").Append(e.distance).Append(")\n");
            }
        }
        sb.Append(L(
            "\n이 중에서 지금 가 보기 좋은 곳을 3곳 골라 이유와 함께 알려 줘.",
            "\nPick 3 places worth visiting now and tell me why.",
            "\nこの中から今行くのにおすすめの3か所を理由と一緒に教えて。",
            "\n请从中挑选 3 个现在值得去的地方，并说明理由。",
            "\nElige 3 lugares que valga la pena visitar ahora y dime por qué."));
        return sb.ToString();
    }

    private string Area()
    {
        if (areaSource == null || string.IsNullOrEmpty(areaSource.text)) return L("이 근처", "here", "この辺り", "这附近", "aquí");
        // 위치 칩: 1줄 좌표, 2줄 주소 → 주소만 (좌표는 넘기지 않는다)
        string[] lines = areaSource.text.Split('\n');
        return lines.Length > 1 ? lines[1].Trim() : L("이 근처", "here", "この辺り", "这附近", "aquí");
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
