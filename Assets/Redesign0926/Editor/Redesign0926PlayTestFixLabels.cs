using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-16 B4 · B5 — 남의 장소 '정보 수정' 창: 제목 「장소수정 요청」 · 칸 이름 「수정할 내용 (선택사항)」 (대표님이 정한 글자 그대로).
    ///  · fixlabels : 가까운 남의 장소 큐브의 정보 창 → '···' → '정보 수정' → 제목 · 칸 이름만 보고 닫는다 (보내지 않는다 — 운영 서버는 읽기만).
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanFixLabels()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const string More = "FullScreenPanel/FullScreenGuide/TopUIPanel/FixButton";
            const string Menu = "FullScreenPanel/FullScreenGuide/FixButtonPanel/MoreSheet0926/Tiles0926/";
            const string Page = "Fixpage/FixUploadPage/";
            CubeDataFixManager fix = null;
            DoubleTap3D target = null;
            string lang = null;

            DsBoot();
            Wait(() =>
            {
                if (DataManager.Instance == null || Camera.main == null) return false;
                var lm = LoginManager.Instance;
                string me = lm != null && lm.IsLoggedIn ? lm.CurrentUsername : null;
                var map = DataManager.Instance.GetPlaceDataMap();
                var cam = Camera.main.transform.position;
                float best = float.MaxValue;
                target = null;
                foreach (var kv in DataManager.Instance.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy || !map.TryGetValue(kv.Key, out var p) || p.model_type != "cube") continue;
                    if (string.IsNullOrEmpty(p.username) || p.username == me || DataManager.Instance.IsAnimCategory(kv.Key)) continue;
                    var d = kv.Value.GetComponentInChildren<DoubleTap3D>(true);
                    if (d == null || !d.IsReady) continue;
                    float s = (d.transform.position - cam).sqrMagnitude;
                    if (s < best) { best = s; target = d; }
                }
                return target != null;
            }, 40f, "남의 장소 큐브");
            Do(() =>
            {
                fix = Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                lang = AppLanguage.Code;
                log.Add($"  앱 언어 {lang} · 연 장소 id {(target != null ? DsField(target, "id") : "-")}");
                if (target == null) return;
                typeof(DoubleTap3D).GetField("isCooldown", DsF).SetValue(target, false);
                typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", DsF).Invoke(target, null);
            });
            Wait(() => DoubleTap3D.Shown != null, 5f, "정보 창 열림");
            Sleep(0.6f);
            Do(() => Click(More));
            Sleep(0.6f);
            Do(() => Click(Menu + "FixPlaceButton"));
            Wait(() => Active("Fixpage") && fix != null && !(bool)typeof(CubeDataFixManager).GetField("isProcessing", F).GetValue(fix), 20f, "정보 수정 창 — 지금 값 불러옴");
            Sleep(0.6f);
            Shot("fixlabels_other");
            Check(() =>
            {
                var title = Find(Page + "TITLE")?.GetComponent<Text>()?.text;
                var loc = typeof(CubeDataFixManager).GetField("localizer", F).GetValue(fix) as FixPlaceLocalizer;
                var label = loc != null ? typeof(FixPlaceLocalizer).GetField("descriptionLabel", F).GetValue(loc) as Text : null;
                var own = (bool)typeof(CubeDataFixManager).GetField("ownerMode", F).GetValue(fix);
                log.Add($"  제목 「{title}」 · 칸 이름 「{(label != null ? label.text : "-")}」 · 보임 {(label != null && label.gameObject.activeInHierarchy)} · 내 장소 {own}");
                if (own || label == null || !label.gameObject.activeInHierarchy) return false;
                if (lang == "ko") return title == "장소수정 요청" && label.text == "수정할 내용 (선택사항)";
                var t = (Dictionary<string, string[]>)typeof(FixPlaceLocalizer).GetField("modeTitles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
                return title == (t.ContainsKey(lang) ? t[lang] : t["en"])[1];
            }, "남의 장소 — 제목 「장소수정 요청」 · 칸 이름 「수정할 내용 (선택사항)」 (한국어)");
            Do(() => Click(Page + "XButton_FixUpload"));
            Sleep(0.4f);
            Check(() => !Active("Fixpage"), "닫기 — 보내지 않는다 (운영 서버로 가는 요청은 만들지 않음)");
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => DoubleTap3D.Shown == null, 3f, "정보 창 닫힘");
        }
    }
}
