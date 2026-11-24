using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // L1 링크드인 (11-23 대표님 「프로필 항목에 'linkedin' … 프로필 열었을 때 아이디를 기입한 경우 이것도 역시 발생 … 프로필 수정, 기입할 때도 해당 링크 추가할 수 있도록 칸 하나 더」)
        //  가짜 프로필 서버(127.0.0.1:5796 · 스크래치패드 linkedin_stub.py — 받은 글을 그대로 적는다)로:
        //  SNS 줄 네 번째 칸 · 남의 프로필 네 칸 배치 · 편집 칸 · 주소를 붙여 넣으면 아이디만 저장 · 지우면 칸이 사라짐.
        //  로그인 저장값(토큰)은 건드리지 않는다 — 메모리 속 로그인 흉내(SfLogin)만. 링크드인 칸은 누르지 않는다(편집기에선 브라우저가 열린다) — 여는 일이 걸렸는지와 주소 셈만 본다.
        private const string LiBase = "http://127.0.0.1:5796", LiMe = "990001", LiOther = "990002";
        private static bool liRun;
        private static string liPmBase, liFmBase, liMpBase;

        // 시험 주소 · 로그인 흉내 되돌리기 — 끝에서 한 번, 오류로 끊겨도 실행기 끝에서 한 번 (플레이 모드에 들어갈 때 정적 값이 안 지워지는 설정이라)
        private static void LiFinish()
        {
            if (!liRun) return;
            liRun = false;
            SfLogin(false);
            ProfileManager.EditorBase = liPmBase;
            FollowManager.EditorBase = liFmBase;
            MessagePanelManager.EditorBase = liMpBase;
        }

        private static string LiCall(string path, string json = null)
        {
            using (var wc = new System.Net.WebClient())
            {
                wc.Encoding = System.Text.Encoding.UTF8;
                try
                {
                    if (json == null) return wc.DownloadString(LiBase + path);
                    wc.Headers.Add("Content-Type", "application/json");
                    return wc.UploadString(LiBase + path, json);
                }
                catch (System.Net.WebException e) { log.Add("  가짜 서버 " + path + " 실패: " + e.Message); return ""; }
            }
        }

        private static JArray LiPosts()
        {
            string r = LiCall("/__state");
            if (string.IsNullOrEmpty(r)) return null;
            try { return JObject.Parse(r)["posts"] as JArray; } catch (Exception) { return null; }
        }
        private static JObject LiPost(int i) { var p = LiPosts(); return p != null && p.Count > i ? p[i] as JObject : null; }

        private static int LiListeners(Button b)
        {
            if (b == null) return 0;
            var calls = typeof(UnityEngine.Events.UnityEventBase).GetField("m_Calls", NPI)?.GetValue(b.onClick);
            return calls != null ? (int)calls.GetType().GetProperty("Count").GetValue(calls) : 0;
        }

        // 켜진 SNS 칸들이 카드 안 · 겹치지 않음 · 가운데 · 기대한 차례(왼쪽부터)
        private static bool LiRow(ProfileManager pm, params Button[] order)
        {
            var layout = Find("FullProfilePanel/Content")?.GetComponent<R0926ProfileLayout>();
            var card = layout != null ? typeof(R0926ProfileLayout).GetField("card", NPI)?.GetValue(layout) as RectTransform : null;
            if (card == null || pm.snsIconsContainer == null) { log.Add("  카드 · SNS 줄 못 찾음"); return false; }
            var on = new List<RectTransform>();
            foreach (Transform t in pm.snsIconsContainer.transform) if (t.gameObject.activeInHierarchy) on.Add(t as RectTransform);
            on.Sort((a, b) => a.position.x.CompareTo(b.position.x));
            var cc = new Vector3[4];
            card.GetWorldCorners(cc);
            var parts = new List<string>();
            bool ok = on.Count == order.Length;
            float prevRight = float.MinValue, sum = 0, gapMin = float.MaxValue;
            for (int i = 0; i < on.Count; i++)
            {
                var c = new Vector3[4];
                on[i].GetWorldCorners(c);
                parts.Add($"{on[i].name.Replace("Button", "")} {c[0].x:0}~{c[2].x:0}×{c[2].y - c[0].y:0}");
                if (i < order.Length && order[i] != null && on[i] != order[i].transform) ok = false;
                if (c[0].x < cc[0].x || c[2].x > cc[2].x || c[0].y < cc[0].y || c[2].y > cc[2].y) ok = false;
                if (i > 0) { gapMin = Mathf.Min(gapMin, c[0].x - prevRight); if (c[0].x < prevRight - 0.5f) ok = false; }
                prevRight = c[2].x;
                sum += (c[0].x + c[2].x) / 2f;
            }
            float mid = on.Count > 0 ? sum / on.Count : 0, cardMid = (cc[0].x + cc[2].x) / 2f;
            if (on.Count > 0 && Mathf.Abs(mid - cardMid) > 4f) ok = false;
            log.Add($"  SNS 줄 {on.Count}칸: {string.Join(" · ", parts)} · 카드 {cc[0].x:0}~{cc[2].x:0} · 가운데 어긋남 {mid - cardMid:0.0}px"
                    + (on.Count > 1 ? $" · 가장 좁은 사이 {gapMin:0}px" : ""));
            return ok;
        }

        private static void PlanLinkedIn()
        {
            ProfileManager pm = null;
            R0926ProfileEdit pe = null;
            UploadInputMirror mirror = null;
            var T = typeof(UploadInputMirror);
            Func<string> state = () => pe != null ? typeof(R0926ProfileEdit).GetField("state", NPI).GetValue(pe).ToString() : "";
            Func<string, InputField> inp = k => Find(PfC + "F_" + k + "/Input")?.GetComponent<InputField>();
            Func<InputField> mi = () => mirror != null ? T.GetField("mirrorInput", NPI).GetValue(mirror) as InputField : null;
            Func<ProfileData> cur = () => pm != null ? typeof(ProfileManager).GetField("currentProfile", NPI).GetValue(pm) as ProfileData : null;
            Action<string, string> type = (k, text) =>
            {
                Do(() =>
                {
                    var src = inp(k);
                    if (mirror == null || src == null || mi() == null) { log.Add("  입력줄 없음 " + k); return; }
                    T.GetMethod("ActivateMirrorFor", NPI).Invoke(mirror, new object[] { src });
                    mi().text = text;
                });
                Sleep(0.3f);
                Do(() => { if (mirror != null) T.GetMethod("OnCloseClicked", NPI).Invoke(mirror, null); });
                Sleep(0.5f);
            };
            Action openMine = () =>
            {
                Do(() => pm.ShowProfile(LiMe));
                Wait(() => Active("FullProfilePanel") && pm.editProfileButton != null && pm.editProfileButton.gameObject.activeInHierarchy && cur()?.id == LiMe,
                    12f, "내 프로필 열림");
                Sleep(1.0f);
            };
            Action openEdit = () =>
            {
                Do(() => pm.editProfileButton.onClick.Invoke());
                Wait(() => Active(PfS) && state() == "Ready", 10f, "편집 시트 — 서버에서 내 정보 받음");
                Sleep(0.6f);
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                LiCall("/__reset", "{\"users\":{\"990001\":{\"username\":\"li_me\",\"bio\":\"링크드인 시험\",\"instagram_id\":\"insta_me\",\"linkedin_id\":\"hong-gildong-a1b2c3\"},"
                                 + "\"990002\":{\"username\":\"li_other\",\"instagram_id\":\"o_insta\",\"x_id\":\"o_x\",\"facebook_id\":\"o_fb\",\"linkedin_id\":\"o-linked\"}}}");
                liPmBase = ProfileManager.EditorBase; liFmBase = FollowManager.EditorBase; liMpBase = MessagePanelManager.EditorBase;
                liRun = true;
                ProfileManager.EditorBase = LiBase;
                FollowManager.EditorBase = LiBase;   // 남의 프로필의 팔로우 조회도 가짜 서버로 (운영 서버에 시험 아이디로 묻지 않게)
                MessagePanelManager.EditorBase = LiBase;
                SfLogin(true, LiMe);
                LoginManager.Instance.CurrentUser.username = "li_me";
                pm = ProfileManager.Instance;
                pe = UnityEngine.Object.FindAnyObjectByType<R0926ProfileEdit>(FindObjectsInactive.Include);
                mirror = Find("FullProfilePanel")?.GetComponent<UploadInputMirror>();
            });
            Check(() => pm != null && pe != null && mirror != null && LoginManager.Instance.CurrentUser.id == LiMe,
                "준비: 메모리 속 로그인 990001 li_me (저장된 토큰은 그대로) · 프로필은 가짜 서버로");

            Check(() =>
            {
                var b = pm.linkedinButton;
                var img = b != null ? b.GetComponent<Image>() : null;
                log.Add($"  링크드인 칸: {(b != null ? b.transform.parent.name + "/" + b.name : "없음")} · 그림 {(img != null && img.sprite != null ? img.sprite.name : "-")}"
                        + $" · 차례 {(b != null ? b.transform.GetSiblingIndex() + 1 + "/" + b.transform.parent.childCount : "-")}");
                return b != null && img != null && img.sprite != null && img.sprite.name == "linkedin_icon" && img.preserveAspect
                       && pm.snsIconsContainer != null && b.transform.parent == pm.snsIconsContainer.transform
                       && b.transform.GetSiblingIndex() == b.transform.parent.childCount - 1 && b.GetComponent<UITouchForwarder>() != null
                       && ((RectTransform)b.transform).sizeDelta == new Vector2(163, 163);
            }, "장면: SNS 줄 맨 끝에 LinkedInButton — 링크드인 그림 · 163 칸 · 누름 소리 부품 · ProfileManager 에 연결");
            Check(() =>
            {
                var f = typeof(R0926ProfileEdit).GetField("linkedinInput", NPI).GetValue(pe) as InputField;
                var src = T.GetField("sourceInputs", NPI).GetValue(mirror) as InputField[];
                return f != null && f == inp("linkedin") && src != null && Array.IndexOf(src, f) >= 0 && f.characterLimit == 200
                       && f.keyboardType == TouchScreenKeyboardType.URL && f.lineType == InputField.LineType.SingleLine;
            }, "장면: 편집 칸 F_linkedin — 시트에 연결 · 키보드 위 입력줄이 맡음 · 한 줄 · 200자(주소째 붙여 넣기) · 주소 키보드");
            Check(() =>
            {
                var g = Find(PfC + "F_linkedin");
                var x = Find(PfC + "F_x");
                var web = Find(PfC + "Web0926");
                log.Add($"  칸 이름 '{TextOf(PfC + "F_linkedin/Label0926")}' · 안내 '{TextOf(PfC + "F_linkedin/Input/Placeholder")}'");
                return g != null && x != null && web != null && TextOf(PfC + "F_linkedin/Label0926") == "링크드인"
                       && TextOf(PfC + "F_linkedin/Input/Placeholder") == "아이디 또는 프로필 주소"
                       && g.GetSiblingIndex() == x.GetSiblingIndex() + 1 && web.GetSiblingIndex() == web.parent.childCount - 1;
            }, "편집 칸 이름 「링크드인」 · 안내 「아이디 또는 프로필 주소」 · X 칸 바로 아래 · 맨 아래 「비밀번호 변경 · 계정 삭제」 그대로");
            Check(() =>
            {
                var cases = new (string raw, string handle)[]
                {
                    ("https://www.linkedin.com/in/hong-gildong-a1b2c3/?trk=public_profile", "hong-gildong-a1b2c3"),
                    ("kr.linkedin.com/in/%ED%99%8D%EA%B8%B8%EB%8F%99-1a2b", "홍길동-1a2b"),
                    ("http://linkedin.com/pub/old-style/12/345", "old-style"),
                    ("LinkedIn.com/IN/MixedCase#top", "MixedCase"),
                    ("@plain-id", "plain-id"),
                    ("  plain_id2  ", "plain_id2"),
                    ("https://www.linkedin.com/in/", ""),
                };
                int ok = 0;
                foreach (var c in cases)
                {
                    string h = ProfileManager.LinkedInHandle(c.raw);
                    if (h == c.handle) ok++; else log.Add($"  다름: {c.raw} → {h} (기대 {c.handle})");
                }
                string u1 = ProfileManager.LinkedInUrl("hong-gildong-a1b2c3"), u2 = ProfileManager.LinkedInUrl("홍길동-1a2b");
                log.Add($"  여는 주소: {u1} · {u2}");
                return ok == cases.Length && u1 == "https://www.linkedin.com/in/hong-gildong-a1b2c3" && u2 == "https://www.linkedin.com/in/%ED%99%8D%EA%B8%B8%EB%8F%99-1a2b";
            }, "주소째 붙여 넣어도 아이디만 (서버 시험과 같은 일곱 가지) · 여는 주소 linkedin.com/in/<아이디> (한글은 %XX)");

            // 내 프로필 — 인스타 · 링크드인
            openMine();
            Shot("li_mine_two");
            Check(() => pm.linkedinButton.gameObject.activeInHierarchy && pm.instagramButton.gameObject.activeInHierarchy
                        && !pm.xButton.gameObject.activeInHierarchy && !pm.facebookButton.gameObject.activeInHierarchy,
                "내 프로필 — 인스타 · 링크드인 아이디가 있으니 그 두 칸 (X · 페이스북은 없음)");
            Check(() => LiRow(pm, pm.instagramButton, pm.linkedinButton), "두 칸이 카드 안 · 겹치지 않음 · 가운데 · 인스타 → 링크드인");
            Check(() => LiListeners(pm.linkedinButton) >= 1, "링크드인 칸을 누르면 여는 일이 걸려 있다");

            // 남의 프로필 — 네 칸
            Do(() => pm.ShowProfile(LiOther));
            Wait(() => cur()?.id == LiOther && pm.xButton.gameObject.activeInHierarchy && pm.linkedinButton.gameObject.activeInHierarchy, 12f, "남의 프로필 — 네 칸");
            Sleep(1.0f);
            Shot("li_other_four");
            Check(() => LiRow(pm, pm.instagramButton, pm.xButton, pm.facebookButton, pm.linkedinButton),
                "남의 프로필 — 인스타 · X · 페이스북 · 링크드인 네 칸이 카드 안 · 겹치지 않음 · 가운데 (163 칸 · 간격 44)");

            // 편집 — 주소째 붙여 넣고 저장하면 아이디만
            openMine();
            openEdit();
            Check(() => inp("linkedin").text == "hong-gildong-a1b2c3" && inp("instagram").text == "insta_me" && inp("x").text == "",
                "편집 칸에 지금 링크드인 아이디 (다른 SNS 칸도 그대로)");
            Do(() =>
            {
                var sr = typeof(R0926ProfileEdit).GetField("scroll", NPI).GetValue(pe) as ScrollRect;
                if (sr != null) sr.verticalNormalizedPosition = 0f;
            });
            Sleep(0.6f);
            Shot("li_edit_bottom");
            Check(() =>
            {
                var sr = typeof(R0926ProfileEdit).GetField("scroll", NPI).GetValue(pe) as ScrollRect;
                var f = inp("linkedin").transform as RectTransform;
                if (sr == null || f == null) return false;
                var v = new Vector3[4]; sr.viewport.GetWorldCorners(v);
                var c = new Vector3[4]; f.GetWorldCorners(c);
                log.Add($"  보이는 곳 y {v[0].y:0}~{v[1].y:0} · 링크드인 칸 y {c[0].y:0}~{c[1].y:0}");
                return c[0].y >= v[0].y - 1 && c[1].y <= v[1].y + 1;
            }, "맨 아래로 내리면 링크드인 칸이 다 보인다");
            type("linkedin", "https://www.linkedin.com/in/new-person-77/?trk=abc");
            Check(() => inp("linkedin").text == "https://www.linkedin.com/in/new-person-77/?trk=abc", "키보드 위 입력줄에 쓴 주소가 칸에 들어감");
            Do(() => pe.OnSave());
            Wait(() => LiPosts()?.Count >= 1, 10f, "저장 → 가짜 서버가 받음");
            Check(() =>
            {
                var b = LiPost(0);
                log.Add("  보낸 것: " + (b != null ? b.ToString(Newtonsoft.Json.Formatting.None) : "없음"));
                return b != null && b.Count == 2 && (string)b["user_id"] == LiMe && (string)b["linkedin_id"] == "new-person-77";
            }, "저장하면 링크드인 아이디만 보낸다 (주소에서 아이디만 · 손대지 않은 칸은 안 보냄)");
            Wait(() => !Active(PfS), 6f, "저장 → 시트가 닫힘");
            Wait(() => cur()?.linkedin_id == "new-person-77", 8f, "프로필이 새 아이디로 다시 그려짐");
            Check(() => pm.linkedinButton.gameObject.activeInHierarchy && pm.instagramButton.gameObject.activeInHierarchy, "저장 뒤에도 인스타 · 링크드인 칸");

            // 지우면 칸이 사라진다
            openEdit();
            Check(() => inp("linkedin").text == "new-person-77", "다시 열면 아이디만 남아 있다");
            type("linkedin", "");
            Do(() => pe.OnSave());
            Wait(() => LiPosts()?.Count >= 2, 10f, "지우고 저장 → 가짜 서버가 받음");
            Check(() =>
            {
                var b = LiPost(1);
                log.Add("  보낸 것: " + (b != null ? b.ToString(Newtonsoft.Json.Formatting.None) : "없음"));
                return b != null && b.Count == 2 && b.Property("linkedin_id") != null && (string)b["linkedin_id"] == "";
            }, "지우면 빈 값을 보낸다");
            Wait(() => !Active(PfS), 6f, "시트가 닫힘");
            Wait(() => cur() != null && string.IsNullOrEmpty(cur().linkedin_id), 8f, "프로필이 다시 그려짐");
            Sleep(0.6f);
            Shot("li_cleared");
            Check(() => !pm.linkedinButton.gameObject.activeInHierarchy && pm.instagramButton.gameObject.activeInHierarchy
                        && LiRow(pm, pm.instagramButton),
                "링크드인을 지우면 그 칸이 사라지고 인스타 한 칸만 가운데");

            // 정리 — 로그인 흉내 · 시험 주소 되돌리기
            Do(() =>
            {
                if (Active(PfS)) pe.Close();
                if (Active("FullProfilePanel")) BackNow();
            });
            Sleep(0.6f);
            Do(LiFinish);
        }
    }
}
