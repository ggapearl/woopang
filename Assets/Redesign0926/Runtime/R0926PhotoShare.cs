using System.Collections;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 사진 공유 시트 — 크게 보기의 '공유'가 연다. 10-30 대표님 「공유하는 창은 '팔로잉' 하고 있는 친구에게 공유할 수 있도록」.
/// 내가 팔로잉하는 사람 목록에서 '보내기'를 누르면 같은 사진 주소를 그 사람에게 메시지로 보낸다(서버 사진 파일은 하나). 여러 명에게 차례로 보낼 수 있다.
/// 모양은 신고 시트와 같다 — 카드 + 아래 닫기 알약 · 끌어내리기 · 뒤로가기 · 바깥 누르기. 화면은 Redesign0926ApplyChatPhoto 가 만든다
/// </summary>
public class R0926PhotoShare : MonoBehaviour
{
    [SerializeField] private MessagePanelManager chat;
    [SerializeField] private RectTransform card;
    [SerializeField] private RectTransform content;
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private Text status;

    private const float Head = 174f, RowH = 168f;   // 제목 150 + 목록 아래 여백 24 — Redesign0926ApplyChatPhoto 와 같은 값
    private const int MinRows = 2, MaxRows = 5;     // 친구가 적으면 카드가 줄어든다 · 많으면 안에서 스크롤 (가로 화면에도 들어가는 높이)

    private string url;
    private int session;

    public bool IsOpen => gameObject.activeSelf;

    private void OnDisable() => session++;

    public void Open(string photoUrl)
    {
        url = photoUrl;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        Clear();
        Rows(0);
        Say(R0926LocalizedText.Say("불러오는 중…", "Loading…", "読み込み中…", "正在加载…", "Cargando…"));
        StartCoroutine(Load(session));
    }

    public void Close() => gameObject.SetActive(false);

    private void Clear()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var c = content.GetChild(i).gameObject;
            if (c != rowTemplate) Destroy(c);
        }
    }

    private void Rows(int n)
    {
        float shown = n > MaxRows ? MaxRows + 0.5f : Mathf.Max(n, MinRows);   // 더 있으면 다음 줄이 반쯤 보인다 — 내려 볼 수 있다는 표시
        card.sizeDelta = new Vector2(card.sizeDelta.x, Head + shown * RowH);
    }

    private void Say(string text)
    {
        status.text = text;
        status.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private IEnumerator Load(int s)
    {
        string me = LoginManager.Instance != null && LoginManager.Instance.CurrentUser != null ? LoginManager.Instance.CurrentUser.id : null;
        if (string.IsNullOrEmpty(me)) { Say(LoadFail); yield break; }

        string id = UnityWebRequest.EscapeURL(me);
        JSONNode j = null;
        using (var req = UnityWebRequest.Get($"{MessagePanelManager.SearchBase}/api/following?user_id={id}&requester_id={id}"))
        {
            req.timeout = 15;
            LoginManager.ApplyAuth(req);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) j = JSON.Parse(req.downloadHandler.text);
        }
        if (s != session) yield break;
        if (j == null) { Say(LoadFail); yield break; }

        var list = j["following"].AsArray;
        Rows(list.Count);
        if (list.Count == 0)
        {
            Say(R0926LocalizedText.Say("아직 팔로잉한 친구가 없어요", "You're not following anyone yet", "まだフォローしている友だちがいません", "你还没有关注任何好友", "Aún no sigues a nadie"));
            yield break;
        }
        Say("");
        foreach (JSONNode user in list) AddRow(user["user_id"].Value, user["username"].Value, user["avatar_url"].Value);
    }

    private static string LoadFail => R0926LocalizedText.Say("목록을 불러오지 못했어요", "Couldn't load the list", "リストを読み込めません", "无法加载列表", "No se pudo cargar la lista");

    private void AddRow(string id, string username, string avatarUrl)
    {
        var row = Instantiate(rowTemplate, content);
        row.name = "Row_" + id;
        row.SetActive(true);
        row.transform.Find("Name0926").GetComponent<Text>().text = username;
        ProfileManager.LoadAvatarWithMaskAsync(id, avatarUrl, row.transform.Find("Avatar0926"), username);

        var btn = row.transform.Find("Send0926").GetComponent<Button>();
        var label = btn.GetComponentInChildren<Text>();
        label.text = R0926LocalizedText.Say("보내기", "Send", "送る", "发送", "Enviar");
        btn.onClick.AddListener(() => Send(id, btn, label));
    }

    private void Send(string id, Button btn, Text label)
    {
        btn.interactable = false;
        string sending = url;
        chat.SendMessageToUser(id, sending, ok =>
        {
            if (btn == null) return;
            if (ok)
            {
                label.text = R0926LocalizedText.Say("보냄", "Sent", "送信済み", "已发送", "Enviada");
                btn.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            }
            else
            {
                btn.interactable = true;
                if (ToastManager.Instance != null)
                    ToastManager.Instance.ShowWarning(R0926LocalizedText.Say("보내지 못했어요. 다시 보내 주세요", "Not sent. Please try again", "送れませんでした。もう一度送ってください", "未发送，请重试", "No se envió. Inténtalo de nuevo"));
            }
        });
    }
}
