using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v19 (2026-10-05):
    ///  · 창 닫기 통일 — 추가도 아래로 밀어 닫기(Apply8 Sheets · 넘기기와 같은 방향 잠금) · 추가 카드 손잡이 다시(Apply10 Grabs) ·
    ///    프로필 카드 아래로 밀어 닫기 · 프로필이 열리면 도크 칸도 X '닫기'(Apply9 ProfileFade · Apply10 DockSlots)
    ///  · '되돌리기' 알림 — 창이 열리면 바로 거둠(Apply6) · 가로 자리(Apply4)
    ///  · 업로드 제한시간 — 아래 (장면에 저장된 값이라 스크립트 기본값만 바꿔선 안 바뀐다)
    /// v21 (2026-10-05 추가 요청):
    ///  · 목록 시트 제목 '근처 장소'·'목록|지도|설정' 글자 키움 (Apply HeaderH · Apply6 SegTab)
    ///  · 지도·설정 탭 글자·줄·스위치 키움 (Apply6) · 지도 깊은 확대 — 서버 지도 타일(R0926MapTiles) · 타일 없을 때도 200배까지
    ///  · 장소 수정 카드도 아래로 밀어 닫기 + 손잡이 (아래)
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float UploadTimeout = 60f;   // 장소 추가·3D 모델 추가·정보 수정 모두 — 보낸 순간부터 (사진 줄이기 시간 제외)

        private static void Apply1005(Transform root, List<string> log)
        {
            UploadTimeouts(root, log);
            FixPageSwipe(root, log);
            log.Add("1005 ok");
        }

        // 장소 수정 카드 — 추가 카드처럼 아래로 밀어 닫기 (오른쪽 위 X·뒤로가기도 내려가며 닫힌다) + 손잡이.
        // 카드가 도크 윗선 틀 밖에 떠 있어 화면 아래 끝까지 내려 보낸다 · 끄는 동안 '수정 완료' 등이 눌리지 않게
        private static void FixPageSwipe(Transform root, List<string> log)
        {
            const string CardPath = "Fixpage/FixUploadPage", ClosePath = CardPath + "/XButton_FixUpload";
            var card = root.Find(CardPath);
            if (card == null || root.Find(ClosePath) == null) { log.Add("장소 수정 밀어 닫기: 카드·X 없음"); return; }
            Swipe(root, CardPath, ClosePath, null, log);
            var sd = card.GetComponent<R0926SwipeDismiss>();
            if (sd == null) return;
            var so = new SerializedObject(sd);
            so.FindProperty("offScreen").boolValue = true;
            so.FindProperty("cancelClicks").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            Proxy(root, sd, ClosePath, log);
            Grab(card.gameObject);

            // X 가 누르면 자기 자신도 끄고 있었다 — 장소 수정을 다시 열 때 켜 주는 곳이 없어 두 번째부터 X·뒤로가기(덮개가 X 안에 있다)가 사라졌다.
            // 창(Fixpage)을 끄면 X 도 함께 안 보이니 자기 끄기만 뺀다
            var xb = root.Find(ClosePath).GetComponent<Button>();
            if (xb != null)
                for (int i = xb.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    if (xb.onClick.GetPersistentTarget(i) == xb.gameObject && xb.onClick.GetPersistentMethodName(i) == "SetActive")
                    {
                        UnityEventTools.RemovePersistentListener(xb.onClick, i);
                        EditorUtility.SetDirty(xb);
                    }
        }

        // 3D 모델 30초 · 정보 수정 20초가 장면에 저장돼 있었다 → 장소 추가와 같은 60초
        private static void UploadTimeouts(Transform root, List<string> log)
        {
            var go = root.gameObject;
            foreach (var mb in new MonoBehaviour[]
            {
                FindInScene<CubeUploadManager>(go), FindInScene<ModelUploadManager>(go), FindInScene<CubeDataFixManager>(go),
            })
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                var p = so.FindProperty("uploadTimeoutSeconds");
                if (p == null) { log.Add("제한시간: 칸 없음 " + mb.GetType().Name); continue; }
                p.floatValue = UploadTimeout;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
