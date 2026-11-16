using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 작은 안내 알약 — 밝은 테두리 + 테두리를 따라 훑는 빛 (11-14 대표님 「화면 자체가 어두운 환경에서는 잘 안보이거든? 해당 메세지 테두리에
    /// 살짝 외곽선을 밝은색으로 반짝이는 느낌으로 주어서 구분을 하면 좋겠어」). 토스트 · 업로드 안내 · 보물 알림 · 보물찾기 카드 알림.
    /// 테두리는 알약과 같은 9-slice(r0926_pill_ring · 같은 배율)라 모양이 꼭 맞고, 빛(R0926Glint)은 테두리 Mask 안에서만 보인다.
    /// 테두리에 캔버스를 따로 둬 빛이 움직여도 그 알약만 다시 그린다 (업로드 안내는 큰 캔버스 바로 아래라 전체를 다시 묶지 않게).
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float RimAlpha = 0.42f;   // 선형 색공간이라 눈에는 약 2/3 밝기 — 빛이 지나갈 때 거의 흰색까지 올라가 반짝여 보인다

        private static void ApplyToastRims(Transform root, List<string> log)
        {
            var hunt = Find(root, "HuntUploadPage");
            var pills = new (string what, Transform t)[]
            {
                ("토스트", root.Find("Toast0926/Stack/Template")),   // 캔버스 바로 아래 것 — 보물찾기 카드 안에도 같은 이름이 있다
                ("업로드 안내", root.Find("WarningText")),   // 장소 추가 · 정보 수정 · 삭제 요청 매니저 넷이 함께 쓴다
                ("보물 알림", root.Find("HuntToast0926/Pill")),
                ("보물찾기 카드 알림", hunt != null ? hunt.transform.Find("Toast0926") : null),
            };
            int n = 0;
            foreach (var (what, t) in pills)
            {
                var img = t != null ? t.GetComponent<Image>() : null;
                if (img == null) { log.Add("안내 알약 테두리: " + what + " 없음"); continue; }

                var rim = FindOrCreate(t, "Rim0926");
                rim.transform.SetAsLastSibling();
                Stretch(RT(rim));
                var le = Ensure<LayoutElement>(rim);
                le.ignoreLayout = true;   // 토스트는 가로 배치 안 — 글자 폭 계산에 끼지 않게
                EditorUtility.SetDirty(le);
                Img(rim, Spr("r0926_pill_ring"), new Color(1f, 1f, 1f, RimAlpha), Image.Type.Sliced, img.pixelsPerUnitMultiplier).raycastTarget = false;
                var mask = Ensure<Mask>(rim);
                mask.showMaskGraphic = true;
                EditorUtility.SetDirty(mask);
                Ensure<Canvas>(rim);

                var glint = FindOrCreate(rim.transform, "Glint");
                Img(glint, Spr("r0926_glow"), Color.white, Image.Type.Simple).raycastTarget = false;
                var grt = RT(glint);
                SetRect(grt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4000f, 0f), new Vector2(240f, 900f));   // 길쭉하게 — 위아래 테두리가 함께 밝아진다
                grt.localRotation = Quaternion.Euler(0f, 0f, -20f);   // 비스듬히 지나가는 빛
                EditorUtility.SetDirty(grt);
                Ensure<R0926Glint>(glint);
                n++;
            }
            log.Add("안내 알약 테두리 " + n + "곳");
        }
    }
}
