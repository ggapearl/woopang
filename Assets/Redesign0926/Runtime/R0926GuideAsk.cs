using System;
using UnityEngine;

/// <summary>
/// 목록에서 장소 줄을 누르면 바로 안내하지 않고 묻는다 — 「○○으로 안내할까요?」 취소 / 확인.
/// 11-16 대표님 D4 「목록탭에서 '해당 장소를 터치해서 위치안내를 시작하기 전에 팝업이 발생해서 'ㅇㅇㅇ으로 안내할까요?' 라는 안내가 발생한 다음에
/// 확인을 누르면 안내하도록 해야할 것 같아. 스와이프하다가 잘못누르거나 창이 작아서 잘못 누른 경우에도 바로 안내하니까 좀 불편해」.
/// 모양은 '삭제 요청' 묻는 창과 같다 (가운데 카드 · 왼쪽 취소 · 오른쪽 확인). 취소 · 뒤로가기 · 바깥 누르기는 아무 일 없이 닫힌다 (목록은 그대로)
/// </summary>
public class R0926GuideAsk : MonoBehaviour
{
    [SerializeField] private R0926LocalizedText ask;
    private Action confirmed;

    public bool Showing => gameObject.activeSelf;

    public void Show(string name, Action onYes)
    {
        string n = string.IsNullOrEmpty(name) ? "" : name.Trim();
        string b = "<b>" + n + "</b>";
        if (ask != null)
            ask.Set(b + Ro(n) + " 안내할까요?", "Guide to " + b + "?", b + "へ案内しますか？", "要导航到" + b + "吗？", "¿Guiar hasta " + b + "?");
        gameObject.SetActive(true);
        confirmed = onYes;   // 켜진 뒤에 — 켜면서 OnDisable 이 비우지 않게
    }

    /// <summary>확인 — 창을 닫고 안내를 시작한다</summary>
    public void Yes()
    {
        var a = confirmed;
        confirmed = null;
        gameObject.SetActive(false);
        a?.Invoke();
    }

    /// <summary>취소 · 뒤로가기 · 바깥 누르기 — 아무 일 없이 닫는다</summary>
    public void No()
    {
        confirmed = null;
        gameObject.SetActive(false);
    }

    private void OnDisable() => confirmed = null;

    /// <summary>이름 뒤 '으로' / '로' — 끝 글자 받침으로 (ㄹ 받침은 '로'). 끝의 괄호 · 문장부호는 건너뛰고, 숫자 · 영문은 읽는 소리로, 그 밖의 글자는 '(으)로'</summary>
    public static string Ro(string s)
    {
        s ??= "";
        for (int i = s.Length - 1; i >= 0; i--)
        {
            char c = s[i];
            if (c >= 0xAC00 && c <= 0xD7A3)
            {
                int jong = (c - 0xAC00) % 28;
                return jong == 0 || jong == 8 ? "로" : "으로";
            }
            if (c >= '0' && c <= '9') return "036".IndexOf(c) >= 0 ? "으로" : "로";   // 영 · 삼 · 육 — 받침 (10 · 100 … 도 십 · 백)
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
            {
                char l = char.ToLowerInvariant(c);
                bool batchim = l == 'm' || l == 'n' || (l == 'g' && i > 0 && char.ToLowerInvariant(s[i - 1]) == 'n');   // 엠 · 엔 · -ng(빌딩) — 받침. CU로 · GS25로 · 이태원(Itaewon)으로
                return batchim ? "으로" : "로";
            }
            if (char.IsLetter(c)) return "(으)로";
        }
        return "(으)로";
    }
}
