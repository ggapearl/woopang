using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 광고 자리 한 칸 — 서버(/ads/api/slot)가 이 자리·이 동네에 맞는 광고를 주면 그걸, 없으면 '이 자리에 광고하세요' 카드를 보인다.
/// 누르면 광고 링크(또는 woopang.com/ads 문의 페이지)로. 노출·클릭은 /ads/api/event 로 기록(광고주 보고용).
/// 광고 목록은 서버의 server/ads/ads.json — 앱 업데이트 없이 바뀐다.
/// </summary>
public class R0926AdSlot : MonoBehaviour
{
    [SerializeField] private string placement = "friends_map";
    [SerializeField] private RawImage photo;
    [SerializeField] private GameObject icon;
    [SerializeField] private Text title;
    [SerializeField] private Text body;
    [SerializeField] private Text cta;
    [SerializeField] private float refreshMinutes = 10f;

    [Serializable] private class Ad { public string id; public string advertiser; public string title; public string body; public string cta; public string image_url; public string link; }
    [Serializable] private class Resp { public Ad ad; }

    private Ad current;
    private string viewedId;
    private float nextFetch;
    private string loadedImage;

    private bool HasAd => current != null && !string.IsNullOrEmpty(current.id);

    private void OnEnable()
    {
        Show();
        if (Time.unscaledTime >= nextFetch) StartCoroutine(Fetch());
        else SendView();
    }

    private IEnumerator Fetch()
    {
        nextFetch = Time.unscaledTime + refreshMinutes * 60f;
        string url = ApiConfig.MAIN_SERVER + "/ads/api/slot?placement=" + placement + "&lang=" + R0926LocalizedText.Lang();
        if (Input.location.status == LocationServiceStatus.Running)
            url += "&lat=" + Input.location.lastData.latitude.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                 + "&lon=" + Input.location.lastData.longitude.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);   // 동네 수준(약 100m)만
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            Resp r = null;
            try { r = JsonUtility.FromJson<Resp>(req.downloadHandler.text); } catch (Exception) { }
            current = r?.ad;
        }
        Show();
        SendView();
        if (HasAd && !string.IsNullOrEmpty(current.image_url) && current.image_url != loadedImage) StartCoroutine(LoadImage(current.image_url));
    }

    private void Show()
    {
        if (HasAd)
        {
            if (title != null) title.text = string.IsNullOrEmpty(current.advertiser) ? current.title : current.advertiser;
            if (body != null) body.text = string.IsNullOrEmpty(current.advertiser) ? current.body : current.title + (string.IsNullOrEmpty(current.body) ? "" : " · " + current.body);
            if (cta != null) cta.text = string.IsNullOrEmpty(current.cta) ? L("보기", "View", "見る", "查看", "Ver") : current.cta;
        }
        else
        {
            if (title != null) title.text = L("이 자리에 가게를 알려 보세요", "Put your shop here", "ここにお店を載せませんか", "在这里宣传你的店", "Anuncia tu tienda aquí");
            if (body != null) body.text = L("동네 반경 안의 우팡 사용자에게만 보여요", "Shown only to WOOPANG users nearby", "近くのWOOPANGユーザーだけに表示", "仅向附近的 WOOPANG 用户展示", "Solo para usuarios cercanos de WOOPANG");
            if (cta != null) cta.text = L("광고 문의", "Advertise", "広告のご相談", "广告咨询", "Anunciar");
        }
        bool hasPhoto = HasAd && photo != null && photo.texture != null && loadedImage == current.image_url;
        if (photo != null) photo.enabled = hasPhoto;
        if (icon != null) icon.SetActive(!hasPhoto);
    }

    private IEnumerator LoadImage(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success || photo == null) yield break;
            photo.texture = DownloadHandlerTexture.GetContent(req);
            loadedImage = url;
        }
        Show();
    }

    public void Click()
    {
        if (HasAd)
        {
            StartCoroutine(Event("click"));
            if (!string.IsNullOrEmpty(current.link)) Application.OpenURL(current.link);
        }
        else Application.OpenURL("https://woopang.com/ads/?from=" + placement);
    }

    private void SendView()
    {
        if (!HasAd || viewedId == current.id || !isActiveAndEnabled) return;
        viewedId = current.id;   // 같은 광고는 불러올 때마다 한 번만
        StartCoroutine(Event("view"));
    }

    private IEnumerator Event(string type)
    {
        string json = "{\"ad_id\":\"" + current.id.Replace("\"", "") + "\",\"placement\":\"" + placement + "\",\"type\":\"" + type + "\"}";
        using (var req = new UnityWebRequest(ApiConfig.MAIN_SERVER + "/ads/api/event", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 8;
            yield return req.SendWebRequest();
        }
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
