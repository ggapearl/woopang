using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-17 B6 — 남의 장소 수정 요청에도 「여기로 옮기기」 (대표님 「내가 직접 올린 게 아니어도 '위치' 가 발생해서 … '여기로 옮기기' 버튼이 있으면 좋을 것 같아」).
    ///  · fixmove : 시험 서버(127.0.0.1:5792 · 임시 DB)의 장소 #3 을 남의 장소 수정 요청 창으로 열어
    ///              스위치(기본 꺼짐) · 거리 안내 → 켜면 「옮겨 달라고 요청」 → 보내면 요청에 지금 위치 → 다시 열면 꺼짐 →
    ///              끈 채로는 바꾼 것 없음 → 이름만 고쳐 보내면 위치 없음 → 내 장소 모드에선 숨고 위치 줄이 보임 · 칸끼리 안 겹침 ·
    ///              최소 글자 38 에서도 안내가 한 줄 (5개 언어 · 꺼짐/가까움/멀리/좌표 없음 · 수정 요청/내 장소).
    /// 운영 서버에 쓰지 않는다 — 고치기 창의 서버 주소(EditorBase)를 시험 서버로 돌리고 끝나면 되돌린다.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanFixMove()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var T = typeof(CubeDataFixManager);
            CubeDataFixManager fix = null;
            Func<string, object> fv = n => T.GetField(n, F).GetValue(fix);
            Action<string, object> fs = (n, v) => T.GetField(n, F).SetValue(fix, v);
            Func<string, object[], object> call = (n, a) => T.GetMethod(n, F).Invoke(fix, a);
            Func<GameObject> panel = () => (GameObject)fv("fixUIPanel");
            Func<GameObject> req = () => (GameObject)fv("requestMove");
            Func<Toggle> tg = () => (Toggle)fv("requestMoveToggle");
            Func<Text> note = () => (Text)fv("requestMoveNote");
            Func<bool> loaded = () => fix != null && !(bool)fv("isProcessing") && fv("cleanPhotos") != null;
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Exception || (type == LogType.Error && msg.Contains("CubeDataFixManager"))) errors.Add(msg);
            };
            JObject last = null;
            int lastId = 0;
            string name0 = null;
            double hereLat = 0, hereLon = 0;

            Func<JObject> lastFix = () =>
            {
                string r = FixHttp("/__fix/last");
                try { return string.IsNullOrEmpty(r) ? null : JObject.Parse(r); } catch (Exception) { return null; }
            };
            Func<JObject, string, double?> num = (j, k) => j == null || j[k] == null || j[k].Type == JTokenType.Null ? (double?)null : (double)j[k];
            Func<bool> fresh = () => last != null && last["id"] != null && (int)last["id"] > lastId;

            // 장소 #3 을 수정 요청 창에 — BeginEdit 와 같은 순서 (fixphoto 와 같게)
            Action open = () =>
            {
                panel().SetActive(true);
                fs("doubleTap", null);
                int gen = (int)fv("loadGen") + 1;
                fs("loadGen", gen);
                call("ResetToInitialState", null);
                fs("editId", 3);
                call("SetOwnerMode", new object[] { false });
                var pd = new PlaceData { id = 3, name = "시험 장소", main_photo = "uploads/seed_3/main.jpg", sub_photos = new List<List<string>>() };
                if (!fix.isActiveAndEnabled) { log.Add("  고치기 창이 꺼져 있어 불러오지 못함"); return; }
                fix.StartCoroutine((IEnumerator)call("LoadPlace", new object[] { pd, gen }));
            };
            Action<string> openAndLoad = what =>
            {
                Do(open);
                Wait(loaded, 40f, what);
                Do(() =>
                {
                    if ((bool)fv("ownerMode")) { log.Add("  서버가 내 장소라고 함 — 수정 요청으로 돌림"); call("SetOwnerMode", new object[] { false }); }
                });
                Sleep(1.2f);   // 거리 안내는 1초마다
            };
            Action send = () =>
            {
                var j = lastFix();
                lastId = j != null && j["id"] != null ? (int)j["id"] : 0;
                fix.StartCoroutine((IEnumerator)call("ValidateAndSubmit", null));
            };
            Action<string> submit = what =>
            {
                Do(send);
                Wait(() => !panel().activeSelf, 25f, what + " — 보냄 · 창 닫힘");
                Sleep(2.3f);   // ValidateAndSubmit 의 2초 뒤 정리까지
                Do(() =>
                {
                    last = lastFix();
                    log.Add($"  받은 요청 #{(last != null ? last["id"] : null)} · 위치 {(num(last, "latitude") != null ? "있음" : "없음")} · 고도 {(num(last, "altitude") != null ? "있음" : "없음")}");
                });
            };
            Func<RectTransform, Rect> world = rt =>
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                Application.logMessageReceived += onLog;
                CubeDataFixManager.EditorBase = FixDev;
                fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                FixHttp("/__fix/loccols", "{}");   // 운영에 더할 위치 칸 — 시험 DB 에만
            });
            Check(() => fix != null && panel() != null && req() != null && tg() != null && note() != null && lastFix() != null,
                "준비: 고치기 창 · 「여기로 옮기기」 칸 연결 · 시험 서버 (임시 DB)");

            // 1) 남의 장소 — 스위치가 보이고 기본은 꺼짐 · 이름표 줄에 거리
            openAndLoad("장소 #3 수정 요청 창");
            Check(() => !(bool)fv("ownerMode") && req().activeInHierarchy && note().gameObject.activeInHierarchy && !tg().isOn
                        && !((GameObject)fv("locationRow")).activeInHierarchy,
                "남의 장소: 「여기로 옮기기」 스위치가 보이고 기본은 꺼짐 (내 장소용 위치 줄은 숨김)");
            Check(() =>
            {
                string t = note().text;
                log.Add($"  꺼짐 안내: 「{System.Text.RegularExpressions.Regex.Replace(t, "[0-9.]+(k?m)", "N$1")}」");
                return AppLanguage.Code != "ko" ? !string.IsNullOrEmpty(t) : t.StartsWith("지금 내 위치에서 ") && (t.EndsWith("m") || t.EndsWith("km"));
            }, "꺼져 있으면 이름표 줄에 「지금 내 위치에서 ○m」");
            Check(() =>
            {
                var desc = (InputField)fv("descriptionInput");
                Rect d = world((RectTransform)desc.transform), r = world((RectTransform)req().transform);
                Rect txt = world(desc.textComponent.rectTransform);
                var loc = fv("localizer") as FixPlaceLocalizer;
                var lab = loc != null ? typeof(FixPlaceLocalizer).GetField("descriptionLabel", F).GetValue(loc) as Text : null;
                Rect n = world(note().rectTransform), l = lab != null ? world(lab.rectTransform) : Rect.zero;
                float s = note().rectTransform.lossyScale.x;
                float labRight = l.xMin + (lab != null ? lab.preferredWidth * lab.rectTransform.lossyScale.x : 0f);
                float noteLeft = n.xMax - Mathf.Min(n.width, note().preferredWidth * s);
                log.Add($"  칸: 스위치 {r.xMin:0}~{r.xMax:0} / 입력 글 끝 {txt.xMax:0} · 이름표 글 끝 {labRight:0} / 안내 글 시작 {noteLeft:0}");
                return r.yMin >= d.yMin - 1 && r.yMax <= d.yMax + 1 && r.xMax <= d.xMax + 1 && r.xMin >= txt.xMax - 1 && labRight < noteLeft;
            }, "칸이 겹치지 않는다 — 스위치는 「수정할 내용」 칸 오른쪽 끝(입력 글 영역 밖) · 안내는 이름표 글 오른쪽");
            Shot("fixmove_off");

            // 2) 켜면 「여기로 옮겨 달라고 요청해요」 · 보내면 요청에 지금 위치
            Do(() => tg().isOn = true);
            Check(() =>
            {
                string t = note().text;
                log.Add($"  켜짐 안내: 「{System.Text.RegularExpressions.Regex.Replace(t, "[0-9.]+(k?m)", "N$1")}」 · 빨강 {(note().color.r > 0.9f && note().color.g < 0.5f)}");
                return AppLanguage.Code != "ko" ? !string.IsNullOrEmpty(t) : t.Contains("옮겨 달라고 요청");
            }, "켜면 안내가 「여기로 옮겨 달라고 요청 · 거리」 (1km 넘게 떨어졌으면 빨갛게)");
            Shot("fixmove_on");
            Do(() =>
            {
                if (GeoHere.TryGet(out hereLat, out hereLon, out _)) return;
                log.Add("  편집기 위치를 못 읽음");
            });
            Do(() => name0 = ((InputField)fv("nameInput")).text);
            submit("스위치만 켜고 보냄");
            Check(() => fresh() && num(last, "latitude") != null && num(last, "longitude") != null
                        && Math.Abs(num(last, "latitude").Value - Math.Round(hereLat, 6)) < 2e-6 && Math.Abs(num(last, "longitude").Value - Math.Round(hereLon, 6)) < 2e-6
                        && last["target_id"] != null && (int)last["target_id"] == 3 && (string)last["name"] == name0,
                "스위치만 켜도 보낼 수 있고, 요청에 지금 위치(소수 6자리)가 실린다 — 장소 #3 · 이름 그대로");

            // 3) 다시 열면 꺼져 있다 · 끈 채로 아무것도 안 바꾸면 보내지 않는다
            openAndLoad("다시 엶");
            Check(() => req().activeInHierarchy && !tg().isOn, "다시 열면 스위치는 꺼져 있다 (매번 새로 고른다)");
            Do(send);
            Sleep(1.5f);
            Check(() => panel().activeSelf && (lastFix()?["id"] == null || (int)lastFix()["id"] == lastId), "끈 채로 바꾼 게 없으면 보내지 않는다 (창 그대로)");

            // 4) 이름만 고쳐 보내면 위치는 없다
            Do(() => ((InputField)fv("nameInput")).text = name0 + " 고침");
            submit("이름만 고쳐 보냄");
            Check(() => fresh() && (string)last["name"] == name0 + " 고침" && num(last, "latitude") == null && num(last, "longitude") == null,
                "스위치를 안 켜면 위치를 보내지 않는다");

            // 5) 내 장소 모드 — 스위치·안내는 숨고 원래 위치 줄
            openAndLoad("다시 엶 (내 장소 모드 확인)");
            Do(() => call("SetOwnerMode", new object[] { true }));
            Sleep(0.3f);
            Check(() => !req().activeInHierarchy && !note().gameObject.activeInHierarchy && ((GameObject)fv("locationRow")).activeInHierarchy,
                "내 장소면 이 스위치·안내는 숨고 원래 위치 줄(여기로 옮기기)이 보인다");
            Do(() => ((Toggle)fv("moveHereToggle")).isOn = true);
            Sleep(0.3f);
            Shot("fixmove_owner");
            Do(() => ((Toggle)fv("moveHereToggle")).isOn = false);
            Do(() => call("SetOwnerMode", new object[] { false }));
            Check(() => req().activeInHierarchy && !tg().isOn, "다시 남의 장소 모드 — 스위치 보이고 꺼짐");

            // 6) 최소 글자 38(10-16 대표님 — 모든 글자 38 이상)에서도 안내가 한 줄에 다 들어간다 — 두 줄이 되면 아래가 잘린다(11-17 「…옮겨 달라고」)
            //    거리: 위도 12도(약 1334km · 빨강) · 0.0089도(약 990m) — 안내는 숫자를 가린다
            Check(() =>
            {
                var bad = new List<string>();
                string lang0 = AppLanguage.EditorOverride;
                bool has0 = (bool)fv("hasPlaceCoords");
                double lat0 = (double)fv("placeLat"), lon0 = (double)fv("placeLon");
                var ownTg = (Toggle)fv("moveHereToggle");
                var ownNote = (Text)fv("locationNote");
                try
                {
                    if (!GeoHere.TryGet(out double la, out double lo, out _)) { log.Add("  편집기 위치를 못 읽음"); return false; }
                    foreach (bool owner in new[] { false, true })
                    {
                        call("SetOwnerMode", new object[] { owner });
                        Text nt = owner ? ownNote : note();
                        Toggle t = owner ? ownTg : tg();
                        foreach (string lang in new[] { "ko", "en", "ja", "zh", "es" })
                        {
                            AppLanguage.EditorOverride = lang;
                            foreach (var (on, has, dLat) in new[] { (true, true, 12.0), (true, true, 0.0089), (false, true, 12.0), (true, false, 0.0) })
                            {
                                fs("hasPlaceCoords", has); fs("placeLat", la + dLat); fs("placeLon", lo);
                                t.SetIsOnWithoutNotify(on);
                                call("RefreshLocation", new object[] { true });
                                float w = nt.preferredWidth, room = nt.rectTransform.rect.width;
                                string what = !on ? "꺼짐" : !has ? "좌표없음" : dLat > 1 ? "멀리" : "가까움";
                                if (w > room + 1f)
                                    bad.Add($"{(owner ? "내 장소" : "요청")}/{lang}/{what} {w:0}>{room:0} 「{System.Text.RegularExpressions.Regex.Replace(nt.text, "[0-9.]+(k?m)", "N$1")}」");
                            }
                        }
                    }
                }
                finally
                {
                    AppLanguage.EditorOverride = lang0;
                    fs("hasPlaceCoords", has0); fs("placeLat", lat0); fs("placeLon", lon0);
                    ownTg.SetIsOnWithoutNotify(false); tg().SetIsOnWithoutNotify(false);
                    call("SetOwnerMode", new object[] { false });
                    call("RefreshLocation", new object[] { true });
                }
                log.Add($"  글자: 요청 {note().fontSize}(최소 {note().resizeTextMinSize}) 칸 {note().rectTransform.rect.width:0} · 내 장소 {ownNote.fontSize}(최소 {ownNote.resizeTextMinSize}) 칸 {ownNote.rectTransform.rect.width:0}");
                foreach (var b in bad) log.Add("  넘침: " + b);
                return bad.Count == 0;
            }, "글자 38 에서도 위치 안내가 한 줄 — 5개 언어 · 꺼짐/가까움/멀리/좌표 없음 · 수정 요청/내 장소");
            Check(() => req().activeInHierarchy && !tg().isOn && !(bool)fv("ownerMode"), "확인 뒤 원래대로 — 남의 장소 모드 · 스위치 꺼짐");

            // 되돌리기 — 창 · 서버 주소
            Do(() =>
            {
                if (panel() != null && panel().activeSelf) { panel().SetActive(false); call("ResetToInitialState", null); }
                CubeDataFixManager.EditorBase = null;
                Application.logMessageReceived -= onLog;
                foreach (var e in errors) log.Add("  오류: " + e);
            });
            Check(() => errors.Count == 0, "오류 없음 (고치기 창 · 보내기)");
        }
    }
}
