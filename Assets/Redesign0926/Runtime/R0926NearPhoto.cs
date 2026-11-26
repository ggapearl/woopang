using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「근처 장소」 카드(R0926NearModal)의 사진 칸 — AR 에 없는 곳(아직 안 떴거나 · 꺼졌거나)은 3D 대신 대표 사진 (11-26 부터 먼 곳도 AR 에 떠 있으면 3D).
/// 장소는 둥근 네모 · 사람은 원 + 그 사람의 공개 범위 색 테두리(목록 · 도크 프로필과 같은 색). 사진이 없거나 받는 중이면 옅은 바탕에 그림(핀 · 사람).
/// 칸(View)의 아이라 칸이 옆으로 밀리고 작아지면 같이 따라간다.
/// </summary>
public sealed class R0926NearPhoto : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image mask;          // 모양 (Mask — 그림 자체는 안 보인다)
    [SerializeField] private RawImage pic;
    [SerializeField] private Image icon;          // 사진이 없을 때
    [SerializeField] private Image rim;           // 테두리
    [SerializeField] private Sprite roundShape, circleShape, roundRim, circleRim, placeIcon, personIcon;
    [SerializeField] private Sprite huntIcon;     // 보물찾기 — 목록 줄과 같은 상자 그림 (11-20 H6)
    [Tooltip("둥근 네모 모서리 — 늘린 알약 그림(모서리 63px)의 배율 (작을수록 둥글다)")]
    [SerializeField] private float roundPpu = 0.9f;
    [Tooltip("장소 사진 테두리 색")]
    [SerializeField] private Color placeRim = new Color(1f, 1f, 1f, 0.35f);
    [Tooltip("사람(원) 사진을 칸 안쪽으로 줄이는 폭 — 원 고리(r0926_ring) 띠가 칸 반지름의 0.939~0.986 이라, 칸 끝까지 자른 사진 가장자리(계단)가 고리 밖으로 비어져 나왔다 (11-26 대표님 15번)")]
    [SerializeField] private float circleInset = 10f;

    private bool round = true, shaped;
    private Texture shown;

    /// <summary>alpha 0 이면 숨는다. tex 가 없으면 그림(핀 · 사람 · 보물상자)</summary>
    public void Show(float alpha, bool person, Texture tex, Color ring, bool hunt = false)
    {
        bool on = alpha > 0.001f;
        if (gameObject.activeSelf != on) gameObject.SetActive(on);
        if (!on) return;
        if (group != null) group.alpha = alpha;
        Shape(!person);
        if (rim != null)
        {
            var c = person ? ring : placeRim;
            if (rim.color != c) rim.color = c;
        }
        if (tex != shown)
        {
            shown = tex;
            if (pic != null)
            {
                pic.texture = tex;
                pic.enabled = tex != null;
                if (tex != null)
                {
                    float w = Mathf.Max(1, tex.width), h = Mathf.Max(1, tex.height);
                    pic.uvRect = w > h ? new Rect((1f - h / w) * 0.5f, 0f, h / w, 1f) : new Rect(0f, (1f - w / h) * 0.5f, 1f, w / h);   // 가운데를 정사각형으로
                }
            }
        }
        if (icon != null)
        {
            bool ic = tex == null;
            if (icon.enabled != ic) icon.enabled = ic;
            var s = person ? personIcon : hunt && huntIcon != null ? huntIcon : placeIcon;
            if (icon.sprite != s) icon.sprite = s;
        }
    }

    /// <summary>숨기고 사진을 놓는다 — 사진은 카드가 지운다</summary>
    public void Hide()
    {
        shown = null;
        if (pic != null) { pic.texture = null; pic.enabled = false; }
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void Shape(bool r)
    {
        if (shaped && r == round) return;
        shaped = true;
        round = r;
        if (mask != null)
        {
            mask.sprite = r ? roundShape : circleShape;
            mask.type = r ? Image.Type.Sliced : Image.Type.Simple;
            mask.pixelsPerUnitMultiplier = r ? roundPpu : 1f;
            // 원은 고리 띠 가운데까지만 — 자른 자리의 계단을 고리가 덮는다 (둥근 네모는 칸 그대로)
            float inset = r ? 0f : circleInset;
            var mrt = mask.rectTransform;
            mrt.offsetMin = new Vector2(inset, inset);
            mrt.offsetMax = new Vector2(-inset, -inset);
        }
        if (rim != null)
        {
            rim.sprite = r ? roundRim : circleRim;
            rim.type = r ? Image.Type.Sliced : Image.Type.Simple;
            rim.pixelsPerUnitMultiplier = r ? roundPpu : 1f;
        }
    }
}
