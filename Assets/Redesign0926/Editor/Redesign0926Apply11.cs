using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Redesign0926
{
    /// <summary>
    /// v19 (2026-10-05):
    ///  · 창 닫기 통일 — 추가도 아래로 밀어 닫기(Apply8 Sheets · 넘기기와 같은 방향 잠금) · 추가 카드 손잡이 다시(Apply10 Grabs) ·
    ///    프로필 카드 아래로 밀어 닫기 · 프로필이 열리면 도크 칸도 X '닫기'(Apply9 ProfileFade · Apply10 DockSlots)
    ///  · '되돌리기' 알림 — 창이 열리면 바로 거둠(Apply6) · 가로 자리(Apply4)
    ///  · 업로드 제한시간 — 아래 (장면에 저장된 값이라 스크립트 기본값만 바꿔선 안 바뀐다)
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float UploadTimeout = 60f;   // 장소 추가·3D 모델 추가·정보 수정 모두 — 보낸 순간부터 (사진 줄이기 시간 제외)

        private static void Apply1005(Transform root, List<string> log)
        {
            UploadTimeouts(root, log);
            log.Add("1005 ok");
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
