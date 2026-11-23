using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-16 A7 — 수정 요청이 사진을 고쳤을 때만 보내고, 있던 사진은 경로로 보내는지 (우팡M 「요청한 사진으로 바꾸기」 확인).
    ///  · fixphoto : 시험 서버(127.0.0.1:5792 · 임시 DB)의 장소 #3 을 수정 요청 창에 불러와
    ///               이름만 → 사진 안 보냄 · 순서만 → 있던 경로 그대로 · 새 추가 사진 → 그 사진만 파일 · 새 대표 → 대표만 파일 ·
    ///               못 불러온 사진 → 회색 칸으로 남고 경로 그대로 (그대로 두면 빠지지 않는다).
    /// 운영 서버에 쓰지 않는다 — 고치기 창의 서버 주소(EditorBase)를 시험 서버로 돌리고, 끝나면 장소 사진과 주소를 되돌린다.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private const string FixDev = "http://127.0.0.1:5792";

        private static string FixHttp(string path, string json = null)
        {
            using (var wc = new System.Net.WebClient())
            {
                wc.Encoding = System.Text.Encoding.UTF8;
                try
                {
                    if (json == null) return wc.DownloadString(FixDev + path);
                    wc.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
                    return wc.UploadString(FixDev + path, json);
                }
                catch (System.Net.WebException e) { log.Add("  시험 서버 " + path + " 실패: " + e.Message); return ""; }
            }
        }

        private static void PlanFixPhoto()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const string Main = "uploads/seed_3/main.jpg", S1 = "uploads/seed_3/sub_1.jpg", S2 = "uploads/seed_3/sub_2.jpg", Nope = "uploads/seed_3/nope.jpg";
            const string SeedPhotos = "{\"main\":\"seed_3\\\\main.jpg\",\"subs\":[\"uploads/seed_3/sub_1.jpg\",\"seed_3/sub_2.jpg\"]}";   // 옛 값 모양 그대로 (역슬래시 · uploads/ 없음)
            var T = typeof(CubeDataFixManager);
            CubeDataFixManager fix = null;
            Func<string, object> fv = n => T.GetField(n, F).GetValue(fix);
            Action<string, object> fs = (n, v) => T.GetField(n, F).SetValue(fix, v);
            Func<string, object[], object> call = (n, a) => T.GetMethod(n, F).Invoke(fix, a);
            Func<List<Texture2D>> subs = () => (List<Texture2D>)fv("subPhotos");
            Func<List<string>> keeps = () => (List<string>)fv("subKeepPaths");
            Func<GameObject> panel = () => (GameObject)fv("fixUIPanel");
            Func<bool> loaded = () => fix != null && !(bool)fv("isProcessing") && fv("cleanPhotos") != null;
            Func<Color32, Texture2D> newTex = c =>
            {
                var t = new Texture2D(96, 64, TextureFormat.RGB24, false);
                var px = new Color32[96 * 64];
                for (int i = 0; i < px.Length; i++) px[i] = (i / 96) % 16 < 8 ? c : new Color32(250, 250, 250, 255);   // 줄무늬 — 단색은 JPEG 가 너무 작다
                t.SetPixels32(px);
                t.Apply();
                return t;
            };
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Exception || (type == LogType.Error && msg.Contains("CubeDataFixManager"))) errors.Add(msg);
            };
            JObject last = null;
            int lastId = 0;
            string name0 = null;

            Func<JObject> lastFix = () =>
            {
                string r = FixHttp("/__fix/last");
                try { return string.IsNullOrEmpty(r) ? null : JObject.Parse(r); } catch (Exception) { return null; }
            };
            Func<JObject, string> mainOf = j => j == null || j["main_photo"] == null || j["main_photo"].Type == JTokenType.Null ? null : (string)j["main_photo"];
            Func<JObject, List<string>> subsOf = j =>
            {
                if (j == null || j["sub_photos"] == null || j["sub_photos"].Type != JTokenType.Array) return null;
                var l = new List<string>();
                foreach (var s in j["sub_photos"]) l.Add((string)s);
                return l;
            };
            Func<JObject, string> said = j => j == null ? "(없음)" : $"#{j["id"]} 대표 {mainOf(j) ?? "null"} · 추가 {(subsOf(j) == null ? "null" : "[" + string.Join(", ", subsOf(j)) + "]")}";
            Func<bool> fresh = () => last != null && last["id"] != null && (int)last["id"] > lastId;

            // 장소 #3 을 수정 요청 창에 — BeginEdit 와 같은 순서 (두 번 누른 장소 대신 번호로)
            Action open = () =>
            {
                panel().SetActive(true);
                fs("doubleTap", null);
                int gen = (int)fv("loadGen") + 1;
                fs("loadGen", gen);
                call("ResetToInitialState", null);
                fs("editId", 3);
                call("SetOwnerMode", new object[] { false });
                var pd = new PlaceData { id = 3, name = "시험 장소", main_photo = Main, sub_photos = new List<List<string>>() };
                if (!fix.isActiveAndEnabled) { log.Add("  고치기 창이 꺼져 있어 불러오지 못함"); return; }
                fix.StartCoroutine((IEnumerator)call("LoadPlace", new object[] { pd, gen }));
            };
            Action asRequest = () =>
            {
                // 편집기 로그인 계정이 운영자면 서버가 '내 장소'라 할 수 있다 — 이 시험은 수정 요청 길만 본다
                if ((bool)fv("ownerMode")) { log.Add("  서버가 내 장소라고 함 — 수정 요청으로 돌림"); call("SetOwnerMode", new object[] { false }); }
                log.Add($"  불러옴: 대표 {fv("mainKeepPath")} · 추가 [{string.Join(", ", keeps())}] · 창 {panel().activeInHierarchy}");
            };
            Action<string> openAndLoad = what =>
            {
                Do(open);
                Wait(loaded, 40f, what);
                Do(asRequest);
            };
            Action<string> submit = what =>
            {
                Do(() =>
                {
                    var j = lastFix();
                    lastId = j != null && j["id"] != null ? (int)j["id"] : 0;
                    fix.StartCoroutine((IEnumerator)call("ValidateAndSubmit", null));
                });
                Wait(() => !panel().activeSelf, 25f, what + " — 보냄 · 창 닫힘");
                Sleep(2.3f);   // ValidateAndSubmit 의 2초 뒤 정리까지
                Do(() => { last = lastFix(); log.Add("  받은 요청: " + said(last)); });
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
                FixHttp("/__place/3/photos", SeedPhotos);
            });
            Check(() => fix != null && panel() != null && lastFix() != null, "준비: 고치기 창 · 시험 서버 (임시 DB)");

            // 1) 이름만 고침 — 사진은 보내지 않는다
            openAndLoad("장소 #3 불러옴 (대표 + 추가 2장)");
            Check(() => (string)fv("mainKeepPath") != null && keeps().Count == 2 && keeps()[0] != null && keeps()[1] != null && !(bool)fv("ownerMode"),
                "서버 사진을 경로째 불러온다 (대표 · 추가 2장) · 수정 요청 창");
            Do(() =>
            {
                var ni = (InputField)fv("nameInput");
                name0 = ni.text;
                ni.text = name0 + " 고침";
            });
            submit("이름만 고침");
            Check(() => fresh() && (string)last["name"] == name0 + " 고침" && mainOf(last) == null && subsOf(last) == null,
                "이름만 고치면 사진은 보내지 않는다 (요청의 사진 칸 비어 있음 → 우팡M 에 사진 줄 없음)");

            // 2) 추가 사진 순서만 — 있던 경로 그대로 (다시 압축한 사본을 보내지 않는다)
            openAndLoad("다시 불러옴");
            Do(() => call("MoveSub", new object[] { 0, 1 }));
            submit("추가 사진 순서만 바꿈");
            Check(() => fresh() && mainOf(last) == Main && subsOf(last) != null && string.Join("|", subsOf(last)) == S2 + "|" + S1,
                "순서만 바꾸면 있던 사진의 경로 그대로 — 대표 " + Main + " · 추가 [sub_2, sub_1]");

            // 3) 새 추가 사진 — 그 사진만 파일로, 있던 사진은 경로로
            openAndLoad("다시 불러옴");
            Do(() => { call("AddSub", new object[] { newTex(new Color32(40, 160, 90, 255)), null }); call("UpdateSubPhotoGrid", null); });
            submit("추가 사진 한 장 넣음");
            Check(() =>
            {
                var s = subsOf(last);
                return fresh() && mainOf(last) == Main && s != null && s.Count == 3 && s[0] == S1 && s[1] == S2
                       && s[2].StartsWith("uploads/locations_fix/") && s[2].EndsWith("/sub_1.jpg");
            }, "새 추가 사진만 올린다 — 대표·있던 추가 사진은 경로 그대로 · 새 사진은 요청 폴더의 sub_1.jpg");

            // 4) 새 대표 사진 — 대표만 파일로
            openAndLoad("다시 불러옴");
            Do(() =>
            {
                var was = (Texture2D)fv("mainPhoto");
                fs("mainPhoto", newTex(new Color32(200, 60, 60, 255)));
                fs("mainKeepPath", null);
                if (was != null) UnityEngine.Object.Destroy(was);
            });
            submit("대표 사진 바꿈");
            Check(() =>
            {
                var s = subsOf(last);
                string m = mainOf(last);
                return fresh() && m != null && m.StartsWith("uploads/locations_fix/") && m.EndsWith("/main.jpg")
                       && s != null && string.Join("|", s) == S1 + "|" + S2;
            }, "대표만 바꾸면 대표만 올린다 — 추가 사진은 경로 그대로 [sub_1, sub_2]");

            // 5) 못 불러온 사진 — 회색 칸으로 남고 경로 그대로 (손대지 않으면 요청에서 빠지지 않는다)
            Do(() => FixHttp("/__place/3/photos", "{\"main\":\"seed_3\\\\main.jpg\",\"subs\":[\"" + S1 + "\",\"" + Nope + "\"]}"));
            openAndLoad("못 불러오는 사진이 든 장소 불러옴");
            Check(() => keeps().Count == 2 && keeps()[1] == Nope && subs().Count == 2 && subs()[1] != null && subs()[1].width == 8,
                "못 불러온 추가 사진도 회색 칸으로 자리를 지킨다 (경로 그대로)");
            Do(() => ((InputField)fv("nameInput")).text = name0 + " 다시");
            submit("못 불러온 사진 둔 채 이름만 고침");
            Check(() => fresh() && mainOf(last) == null && subsOf(last) == null, "못 불러온 사진이 있어도 이름만 고치면 사진은 보내지 않는다");
            openAndLoad("다시 불러옴");
            Do(() => call("MoveSub", new object[] { 0, 1 }));
            submit("못 불러온 사진과 순서 바꿈");
            Check(() => fresh() && subsOf(last) != null && string.Join("|", subsOf(last)) == Nope + "|" + S1,
                "못 불러온 사진은 경로로 남는다 — 순서를 바꿔도 빠지지 않음 [nope, sub_1]");

            // 되돌리기 — 장소 사진 · 창 · 서버 주소
            Do(() =>
            {
                FixHttp("/__place/3/photos", SeedPhotos);
                if (panel() != null && panel().activeSelf) { panel().SetActive(false); call("ResetToInitialState", null); }
                CubeDataFixManager.EditorBase = null;
                Application.logMessageReceived -= onLog;
                foreach (var e in errors) log.Add("  오류: " + e);
            });
            Check(() => errors.Count == 0, "오류 없음 (고치기 창 · 보내기)");
        }
    }
}
