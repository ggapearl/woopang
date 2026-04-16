using UnityEngine;
using UnityEditor;
using TMPro;
using TMPro.EditorUtilities;
using System.Text;

/// <summary>
/// AppleSDGothicNeoM TMP Font Asset에 다국어 문자를 포함하여 재생성
/// 지원: 한글 주요음절(~2,800자) + CJK실사용한자(282자) + 히라가나/카타카나 + 라틴확장 + ASCII + 특수문자
/// 메뉴: WOOPANG > TMP Font Atlas 재생성 (다국어)
///
/// 디바이스에서 TMP 텍스트가 깨지는 문제 해결:
/// 에디터에서는 Dynamic으로 동적 생성되어 안 깨지지만,
/// 디바이스 빌드에서는 Static Atlas만 사용되므로 미리 글자를 구워넣어야 함.
/// </summary>
public class TMPFontAtlasBuilder
{
    private const string FONT_PATH = "Assets/TextMesh Pro/Fonts/AppleSDGothicNeoM.ttf";
    private const string FONT_ASSET_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/AppleSDGothicNeoM.asset";

    [MenuItem("WOOPANG/TMP Font Atlas 재생성 (다국어)")]
    public static void RebuildFontAtlas()
    {
        // 기존 Font Asset 로드
        TMP_FontAsset existingAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
        if (existingAsset == null)
        {
            Debug.LogError($"[TMPFontAtlasBuilder] Font Asset을 찾을 수 없습니다: {FONT_ASSET_PATH}");
            return;
        }

        // 소스 폰트 로드
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
        if (sourceFont == null)
        {
            Debug.LogError($"[TMPFontAtlasBuilder] 소스 폰트를 찾을 수 없습니다: {FONT_PATH}");
            return;
        }

        // 다국어 문자 세트 생성
        string characters = BuildCharacterSet();

        Debug.LogWarning($"[TMPFontAtlasBuilder] Atlas 재생성 시작... 총 {characters.Length}자 (한글+일본어+중국어+스페인어+영문)");
        Debug.LogWarning("[TMPFontAtlasBuilder] Font Asset Creator 창에서 수동으로 생성해주세요:");
        Debug.LogWarning($"  1. Source Font: AppleSDGothicNeoM");
        Debug.LogWarning($"  2. Atlas Resolution: 4096 x 4096");
        Debug.LogWarning($"  3. Character Set: Custom Characters");
        Debug.LogWarning($"  4. Custom Characters에 클립보드 내용 붙여넣기");
        Debug.LogWarning($"  5. Generate Font Atlas → Save (기존 파일 덮어쓰기)");

        // 클립보드에 문자 세트 복사
        EditorGUIUtility.systemCopyBuffer = characters;

        EditorUtility.DisplayDialog(
            "TMP Font Atlas 재생성",
            $"총 {characters.Length}자가 클립보드에 복사되었습니다.\n" +
            "(한글 ~2,800 + CJK 282 + 히라가나/카타카나 + 라틴 확장 + 특수문자)\n\n" +
            "다음 단계를 진행해주세요:\n\n" +
            "1. Source Font File: AppleSDGothicNeoM 선택\n" +
            "2. Sampling Point Size: Auto Size\n" +
            "3. Padding: 5 (px)\n" +
            "4. Packing Method: Fast\n" +
            "5. Atlas Resolution: 4096 x 4096\n" +
            "6. Character Set: Custom Characters\n" +
            "7. Custom Character List에 Ctrl+V (붙여넣기)\n" +
            "8. Generate Font Atlas 클릭\n" +
            "9. Save → 기존 파일 덮어쓰기\n\n" +
            $"경로: {FONT_ASSET_PATH}\n\n" +
            "※ Packing Method를 반드시 Fast로 설정 (Optimum은 매우 느림)\n" +
            "※ 폰트에 없는 문자는 자동 스킵됩니다 (Missing 표시)",
            "확인 — Font Asset Creator 열기"
        );

        // Font Asset Creator 창 자동 열기
        EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Font Asset Creator");
    }

    /// <summary>
    /// 한글 + 일본어 + 중국어 간체 + 스페인어 + ASCII + 특수문자 세트 생성
    /// 지원 언어: ko, en, zh, ja, es (LocalizationManager 기준)
    /// </summary>
    private static string BuildCharacterSet()
    {
        StringBuilder sb = new StringBuilder(32768);

        // ============================================================
        // ASCII 기본 (공백 ~ 틸다)
        // ============================================================
        for (char c = ' '; c <= '~'; c++)
            sb.Append(c);

        // ============================================================
        // Latin Extended — 스페인어/유럽어 악센트 문자
        // ============================================================
        // Latin-1 Supplement (À~ÿ): á, é, í, ó, ú, ñ, ü, ¡, ¿ 등
        for (char c = '\u00A0'; c <= '\u00FF'; c++)
            sb.Append(c);
        // Latin Extended-A (Ā~ſ): 추가 유럽어 문자
        for (char c = '\u0100'; c <= '\u017F'; c++)
            sb.Append(c);

        // ============================================================
        // 한글: 주요 초성+중성+종성 조합 (약 2,500자)
        // 전체 11,172자 대신 실사용 빈도 높은 조합만 포함
        // 나머지는 Font Asset을 Dynamic 모드로 전환하여 런타임 보충
        // ============================================================
        // 한글 자모 (ㄱ~ㅎ, ㅏ~ㅣ)
        for (char c = '\u3131'; c <= '\u3163'; c++)
            sb.Append(c);

        // 주요 한글 음절 생성 (초성 19 × 중성 21 × 주요종성 7 = 2,793자)
        // 종성: 없음(0), ㄴ(4), ㄹ(8), ㅁ(16), ㅂ(17), ㅇ(21), ㄱ(1) — 가장 빈도 높은 7개
        int[] mainJong = { 0, 1, 4, 8, 16, 17, 21 };
        for (int cho = 0; cho < 19; cho++)
        {
            for (int jung = 0; jung < 21; jung++)
            {
                foreach (int jong in mainJong)
                {
                    sb.Append((char)(0xAC00 + cho * 588 + jung * 28 + jong));
                }
            }
        }

        // ============================================================
        // 일본어 — 히라가나 + 카타카나
        // ============================================================
        // 히라가나 (ぁ~ゖ)
        for (char c = '\u3041'; c <= '\u3096'; c++)
            sb.Append(c);
        // 카타카나 (ァ~ヺ)
        for (char c = '\u30A1'; c <= '\u30FA'; c++)
            sb.Append(c);
        // 카타카나 장음 (ー)
        sb.Append('\u30FC');
        // 반각 카타카나 (ｦ~ﾝ)
        for (char c = '\uFF66'; c <= '\uFF9D'; c++)
            sb.Append(c);

        // ============================================================
        // CJK 한자 — LocalizationManager zh/ja 번역에서 실제 사용하는 264자만 포함
        // (전체 20,992자는 폰트 미지원 + Generate 시간 과다로 제외)
        // ============================================================
        string cjkChars =
            // LocalizationManager zh/ja 번역에서 추출 (264자)
            "一上不与个中主乎了交亮他优会传似位低体作使信修個備允光入全公共关其内册写准出击分列创初删到削前剪力功加务动効動包化厅厕友双发取可名向吗吧含启咖商啡器园图園在地坏場境处备多大天太失始姓子字完室容对将少就已常店建开张当录影征徴志息您成或所手打找択拍择指挡损据提損搜摄撮改效敗数文新方无时明暂暗更最有朋服期未机条枚标校案检検正此殊求没法注活测消添準灯点照片物特环現理環用登的相真破确確称移符第类索続纹线继绪续置美聊育能至衆行表被裁要覆見規視覚视觉話認説読认许论评话误说请象败超足跟踪载输込近还这进追退送选達適遮選部里重错開附除離面類食" +
            // 기타 Localizer 파일에서 추출 (18자)
            "件像坐型報好宠座必情標模洗独画立间需";
        foreach (char c in cjkChars)
            sb.Append(c);

        // ============================================================
        // 자주 쓰는 특수문자
        // ============================================================
        sb.Append("①②③④⑤⑥⑦⑧⑨⑩");
        sb.Append("★☆○●◎◇◆□■△▲▽▼→←↑↓↔");
        sb.Append("♠♣♥♦");
        sb.Append("…·•※");
        sb.Append("㈜㈝㈞");
        sb.Append("㎡㎢㎞㎝㎜㎖㎗ℓ㎘");
        sb.Append("℃℉");
        sb.Append("¥₩$€£");
        sb.Append("°±×÷≠≤≥∞∴∵");
        sb.Append("ⓐⓑⓒⓓⓔⓕⓖⓗⓘⓙⓚⓛⓜⓝⓞⓟⓠⓡⓢⓣⓤⓥⓦⓧⓨⓩ");

        // 일본어 구두점
        sb.Append("、。「」『』【】〈〉《》〔〕");
        sb.Append("・ー〜");

        // CJK 기호
        sb.Append("々〇〻");

        return sb.ToString();
    }
}
