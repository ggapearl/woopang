using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class StyleCommentItem : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Woopang/Style Selected Comment Item")]
    static void StyleItem()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || selected.GetComponent<CommentItem>() == null)
        {
            Debug.LogError("CommentItem 컴포넌트가 있는 오브젝트를 선택해주세요!");
            return;
        }

        // 1. Root Layout (Horizontal)
        HorizontalLayoutGroup rootLayout = selected.GetComponent<HorizontalLayoutGroup>();
        if (rootLayout == null) rootLayout = selected.AddComponent<HorizontalLayoutGroup>();
        rootLayout.childControlWidth = false;
        rootLayout.childForceExpandWidth = false;
        rootLayout.childControlHeight = false;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.spacing = 10;
        rootLayout.padding = new RectOffset(10, 10, 10, 10);

        // 2. Content Area (Find by name or type)
        Transform contentArea = selected.transform.Find("ContentArea");
        if (contentArea != null)
        {
            VerticalLayoutGroup vLayout = contentArea.GetComponent<VerticalLayoutGroup>();
            vLayout.spacing = 2; // 아이디-내용 간격 최소화
            vLayout.childAlignment = TextAnchor.UpperLeft;

            // Info Row (Name + Date)
            Transform infoRow = contentArea.Find("InfoRow");
            if (infoRow != null)
            {
                HorizontalLayoutGroup hLayout = infoRow.GetComponent<HorizontalLayoutGroup>();
                hLayout.spacing = 15;
                hLayout.childAlignment = TextAnchor.LowerLeft;
                
                // Name Text
                Text nameTxt = infoRow.Find("Text")?.GetComponent<Text>(); // 이름이 Text일 수 있음
                if (nameTxt != null)
                {
                    nameTxt.fontSize = 44;
                    nameTxt.fontStyle = FontStyle.Bold;
                    nameTxt.color = Color.white;
                }
                
                // Date Text (2번째 자식)
                if (infoRow.childCount > 1)
                {
                    Text dateTxt = infoRow.GetChild(1).GetComponent<Text>();
                    if (dateTxt != null)
                    {
                        dateTxt.fontSize = 36; // 날짜는 조금 더 작게
                        dateTxt.color = new Color(0.7f, 0.7f, 0.7f);
                    }
                }
            }

            // Message Text (2번째 자식)
            if (contentArea.childCount > 1)
            {
                Text msgTxt = contentArea.GetChild(1).GetComponent<Text>();
                if (msgTxt != null)
                {
                    msgTxt.fontSize = 50; // 내용은 크게
                    msgTxt.color = Color.white;
                    msgTxt.lineSpacing = 1.1f;
                }
            }
        }

        // 3. Like Button
        Transform likeBtn = selected.transform.Find("LikeButton");
        if (likeBtn != null)
        {
            RectTransform rect = likeBtn.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60, 80);
            
            // Heart Icon
            Transform heart = likeBtn.Find("Text"); // 하트가 텍스트인 경우
            if (heart != null)
            {
                Text heartTxt = heart.GetComponent<Text>();
                heartTxt.fontSize = 40;
                heartTxt.color = Color.white;
                heart.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 15);
            }

            // Count
            if (likeBtn.childCount > 1)
            {
                Text countTxt = likeBtn.GetChild(1).GetComponent<Text>(); // 2번째 자식
                if (countTxt != null)
                {
                    countTxt.fontSize = 28;
                    countTxt.color = Color.gray;
                    countTxt.rectTransform.anchoredPosition = new Vector2(0, -20);
                }
            }
        }

        Debug.Log("Comment Item 스타일 적용 완료!");
    }
#endif
}
