using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 첫 실행 안내 1~3장 문구를 도크 배치(아래 '추가'·'목록'·'메시지')에 맞게 바꿔 보여 준다.
/// FirstTimeGuide 의 옛 문구('상단 + 버튼', '좌측하단', '우측하단')는 그대로 두고, 해당 페이지가 보일 때만 덮어쓴다.
/// ⚠ 시안용: 확정되면 FirstTimeGuide.guideTemplates 자체를 고치고 이 컴포넌트는 지운다.
/// </summary>
public class R0926GuideText : MonoBehaviour
{
    [SerializeField] private Text guideText;
    [Tooltip("안내 1·2·3장 오브젝트 (01, 02, 03)")]
    [SerializeField] private GameObject[] pages;

    private static readonly string[][] Lines =
    {
        // ko, en, ja, zh, es
        new[] { "아래 '추가'를 눌러 지금 있는 장소를 등록할 수 있어요", "Tap 'Add' below to register the place you're at", "下の「追加」で今いる場所を登録できます", "点击下方“添加”即可登记当前所在地点", "Toca 'Añadir' abajo para registrar el lugar donde estás" },
        new[] { "아래 '목록'을 눌러 근처 장소를 확인할 수 있어요", "Tap 'List' below to see places nearby", "下の「リスト」で近くの場所を確認できます", "点击下方“列表”查看附近地点", "Toca 'Lista' abajo para ver lugares cercanos" },
        new[] { "아래 '메시지'를 눌러 메시지를 확인할 수 있어요", "Tap 'Messages' below to read your messages", "下の「メッセージ」でメッセージを確認できます", "点击下方“消息”查看消息", "Toca 'Mensajes' abajo para leer tus mensajes" },
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
