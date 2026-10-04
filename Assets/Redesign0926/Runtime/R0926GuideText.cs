using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 첫 실행 안내 1~3장 문구를 도크 배치(아래 '추가'·'목록'·'메세지')에 맞게 바꿔 보여 준다.
/// 문구는 쪽 오브젝트를 따라간다 (pages 와 Lines 가 짝) — 보이는 순서(목록 → 추가 → 메세지)는 FirstTimeGuide 가 정한다.
/// FirstTimeGuide 의 옛 문구('상단 + 버튼', '좌측하단', '우측하단')는 그대로 두고, 해당 페이지가 보일 때만 덮어쓴다.
/// 두 줄 안내 + 옅은 한 줄 — 자리는 R0926GuideOverlay 가 잡는다.
/// </summary>
public class R0926GuideText : MonoBehaviour
{
    [SerializeField] private Text guideText;
    [Tooltip("안내 쪽 오브젝트 01(추가) · 02(목록) · 03(메세지) — Lines 와 같은 순서 (보이는 순서와는 무관)")]
    [SerializeField] private GameObject[] pages;

    private const string Sub = "\n<size=42><color=#C9CED3>";
    private const string SubEnd = "</color></size>";

    private static readonly string[][] Lines =
    {
        // ko, en, ja, zh, es
        new[] {
            "‘추가’를 눌러\n이 자리에 장소나 3D 모델을 남겨요" + Sub + "사진·이름·분류만 넣으면 끝" + SubEnd,
            "Tap ‘Add’ to leave a place\nor a 3D model right here" + Sub + "Just a photo, a name and a category" + SubEnd,
            "「追加」で、この場所に\nスポットや3Dモデルを残せます" + Sub + "写真・名前・分類を入れるだけ" + SubEnd,
            "点击“添加”，\n在这里留下地点或3D模型" + Sub + "只需照片、名称和分类" + SubEnd,
            "Toca ‘Añadir’ para dejar aquí\nun lugar o un modelo 3D" + Sub + "Solo foto, nombre y categoría" + SubEnd },
        new[] {
            "아래 ‘목록’을 눌러\n근처 장소를 확인할 수 있어요" + Sub + "거리·분류로 골라 보고, 지도에서 친구도 볼 수 있어요" + SubEnd,
            "Tap ‘List’ below\nto see places nearby" + Sub + "Filter by distance or type, and see friends on the map" + SubEnd,
            "下の「リスト」で\n近くの場所を確認できます" + Sub + "距離や分類で絞り込み、地図で友だちも見られます" + SubEnd,
            "点击下方“列表”\n查看附近地点" + Sub + "按距离或分类筛选，还能在地图上看到朋友" + SubEnd,
            "Toca ‘Lista’ abajo\npara ver lugares cercanos" + Sub + "Filtra por distancia o tipo y ve a tus amigos en el mapa" + SubEnd },
        new[] {
            "‘메세지’에서\n친구와 이야기를 나눠요" + Sub + "새 메시지가 오면 숫자로 알려 드려요" + SubEnd,
            "Chat with friends\nin ‘Messages’" + Sub + "New messages show up as a number" + SubEnd,
            "「メッセージ」で\n友だちと話せます" + Sub + "新着メッセージは数字でお知らせします" + SubEnd,
            "在“消息”中\n和朋友聊天" + Sub + "有新消息时会以数字提醒" + SubEnd,
            "Habla con tus amigos\nen ‘Mensajes’" + Sub + "Los mensajes nuevos se muestran con un número" + SubEnd },
    };

    private void LateUpdate()
    {
        if (guideText == null || pages == null) return;
        for (int i = 0; i < pages.Length && i < Lines.Length; i++)
        {
            if (pages[i] == null || !pages[i].activeInHierarchy) continue;
            string want = Lines[i][LangIndex()];
            if (guideText.text != want) guideText.text = want;
            return;
        }
    }

    private static int LangIndex()
    {
        switch (R0926LocalizedText.Lang())
        {
            case "ko": return 0;
            case "ja": return 2;
            case "zh": return 3;
            case "es": return 4;
            default: return 1;
        }
    }
}
