using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class UserProfilePage : MonoBehaviour {
    [Header("Display Fields")]
    public Text usernameText;
    public Text emailText;
    
    [Header("Input Fields")]
    public InputField phoneInput;
    public InputField instaInput;
    public InputField faceInput;
    public InputField xInput;

    [Header("Buttons")]
    public Button saveButton;
    public Button closeButton;

    void Start() {
        if (saveButton) saveButton.onClick.AddListener(OnSaveClicked);
        if (closeButton) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        RefreshUI();
    }

    public void RefreshUI() {
        var user = LoginManager.Instance.CurrentUser;
        if (user == null) return;

        if (usernameText) usernameText.text = user.username;
        if (emailText) emailText.text = user.email;
        if (phoneInput) phoneInput.text = user.phone;
        if (instaInput) instaInput.text = user.instagram_id;
        if (faceInput) faceInput.text = user.facebook_id;
        if (xInput) xInput.text = user.x_id;
    }

    private void OnSaveClicked() {
        StartCoroutine(UpdateProfileCoroutine());
    }

    private IEnumerator UpdateProfileCoroutine() {
        var user = LoginManager.Instance.CurrentUser;
        string json = JsonUtility.ToJson(new UserData {
            id = user.id,
            phone = phoneInput.text,
            instagram_id = instaInput.text,
            facebook_id = faceInput.text,
            x_id = xInput.text
        });

        using (UnityWebRequest www = new UnityWebRequest("https://woopang.com/api/profile/update", "POST")) {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success) {
                Debug.Log("Profile Updated!");
                StartCoroutine(LoginManager.Instance.FetchProfileCoroutine(user.id));
            }
        }
    }
}
