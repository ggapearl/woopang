using System;
using System.Collections.Generic;
using UnityEngine;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 K2 — 하늘 옆 장이 목록과 같게 (11-18 대표님 「하늘보기에서 '근처 축제' '근처 보물' 이 안보이는데 내가 숨긴 보물이라서 주최자는 안보이도록 해둔건가? …
    /// 메뉴에서 필터링 해서 있는 것들은 상단에서도 반영되도록」).
    ///  · skyboard : 서버는 닫힌 포트(운영 서버에 묻지 않음) — 보물찾기 응답 · 축제 목록을 앱 안에 바로 넣는다.
    ///    ① 내가 연 게임만 있을 때(서버의 근처 목록은 주최자 게임을 뺀다) → 「근처 보물찾기」 장이 생기고 첫 줄 「내가 숨긴 보물 · 진행 중」 · 노치 줄도 그 거리
    ///    ② 내 게임 · 근처 남의 게임 · 참가해서 찾은 게임 → 못 찾은 것 먼저(내 게임 → 근처 게임), 찾은 게임은 뒤 · 참가한 근처 게임은 한 줄만
    ///    ③ 축제 — 광고 · 가까운 축제 · 먼 축제(120km · 한 달 뒤) · 끝난 축제 → 축제 장엔 앞의 셋(목록과 같게), 핫플 장엔 가까운 축제만 섞인다
    ///    ④ 보물찾기 참여를 끄면 그 장이 빠진다
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private const int SbMine = 990201, SbFound = 990202, SbOther = 990203;

        private static void PlanSkyBoard()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const string Dead = "http://127.0.0.1:9";
            const R0926SkyPages.Kind Hunt = R0926SkyPages.Kind.Hunt, Fest = R0926SkyPages.Kind.Festival, Hot = R0926SkyPages.Kind.Hot;
            R0926SkyWeather sky = null;
            R0926SkyPages pg = null;
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error && (msg + stack).Contains("R0926")) errors.Add(msg);
            };
            Func<bool> showing = () => sky != null && (bool)typeof(R0926SkyWeather).GetField("skyShow", F).GetValue(sky);
            Func<R0926SkyPages.Kind, bool> page = k => { pg.EditorGo(k); return pg.EditorCurrent == k; };
            // 그 장에 떠 있는 줄 — '제목 | 둘째 줄' (오른쪽 거리 칸은 넣지 않는다)
            Func<R0926SkyPages.Kind, List<string>> rows = k =>
            {
                var s = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    var r = SkyRow(pg, k, i);
                    if (r?.root != null && r.root.gameObject.activeSelf && r.title != null) s.Add(r.title.text + (r.sub != null ? " | " + r.sub.text : ""));
                }
                return s;
            };
            Func<R0926SkyPages.Kind, List<string>> titles = k => rows(k).ConvertAll(t => t.Split('|')[0].Trim());
            Func<string> board = () => string.Join(", ", R0926HuntList.Board.ConvertAll(n => n.title + (n.host ? "(내 게임)" : n.found ? "(찾음)" : "")));
            Func<string, string, string, string, string> hunts = (prefs, hosted, joined, near) =>
                "{\"prefs\":{\"enabled\":" + prefs + "},\"hosted\":[" + hosted + "],\"joined\":[" + joined + "],\"hunts\":[" + near + "]}";

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
                nmRun = true;   // 끊겨도 NmFinish 가 하늘 설정 · 시험 주소 · 시험 사용자를 되돌린다
                StashSkyPrefs();
                foreach (var k in new[] { "Sky_Enabled", "Sky_Weather", "Sky_Events" }) PlayerPrefs.SetInt(k, 1);
                PlayerPrefs.SetInt("Sky_Angle", 1);   // 28°
                foreach (var k in new[] { "Sky_StartCollapsed", "Sky_Swiped", "Sky_PinUsed" }) PlayerPrefs.SetInt(k, 0);
                R0926Nearby.EditorBase = Dead;
                R0926FriendsMap.EditorBase = Dead;
                R0926HuntApi.EditorBase = Dead;
                R0926HuntApi.EditorTestUser = "sb_me";   // 로그인 없이도 보물찾기 목록을 지우지 않게 (다시 묻는 곳은 닫힌 포트 — 넣은 목록 그대로)
                sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                pg = UnityEngine.Object.FindAnyObjectByType<R0926SkyPages>(FindObjectsInactive.Include);
                if (sky == null || pg == null) return;
                typeof(R0926SkyWeather).GetField("collapsed", F)?.SetValue(sky, false);
                typeof(R0926SkyPages).GetField("nextFriends", F)?.SetValue(pg, float.MaxValue);
                InjectWeather();
                SbFestivals();
                SkyTakeHunts(hunts("true", SbHunt(SbMine, true, false, "시험 내 보물", "시험 선물", 150, 3, 0), "", ""));
                errors.Clear();
                Application.logMessageReceived -= onLog;
                Application.logMessageReceived += onLog;
            });
            Check(() => sky != null && pg != null, "준비: 하늘 판 · 옆 장");

            // ① 내가 연 게임만 — 서버의 근처 목록(hunts)엔 주최자 게임이 없다
            Do(() => AimCamera(-45f, 0f));
            Wait(() => showing() && page(Hunt), 15f, "하늘 판 · 「근처 보물찾기」 장");
            Sleep(0.9f);
            Do(() => log.Add($"  하늘 판 보물찾기: {board()} · 목록의 근처 보물 {R0926HuntList.Nearby.Count}개 · 줄: {string.Join(" / ", rows(Hunt))}"));
            Shot("sb_hunt_mine");
            Check(() => R0926HuntList.Nearby.Count == 0 && R0926HuntList.Board.Count == 1 && R0926HuntList.Board[0].host,
                "내가 연 게임 — 서버 근처 목록엔 없어도 하늘 판 목록엔 있다");
            Check(() => { var r = rows(Hunt); return pg.EditorCurrent == Hunt && r.Count == 1 && r[0].StartsWith("시험 내 보물 | 내가 숨긴 보물 · 진행 중"); },
                "「근처 보물찾기」 장 — 내가 숨긴 보물만 있어도 장이 생긴다 · 둘째 줄 「내가 숨긴 보물 · 진행 중」");
            Check(() => R0926SkyPages.NotchItems.Exists(i => i.kind == Hunt && i.text == "150m"), "노치로 접었을 때 보물찾기 장 — 첫 줄(내 게임)의 거리");

            // ② 내 게임 · 근처 남의 게임 · 참가해서 찾은 게임 (참가한 게임은 근처 목록에도 같이 온다)
            Do(() => SkyTakeHunts(hunts("true",
                SbHunt(SbMine, true, false, "시험 내 보물", "시험 선물", 150, 3, 0),
                SbHunt(SbFound, false, true, "시험 찾은 보물", "", 600, 9, 0),
                SbHunt(SbFound, false, true, "시험 찾은 보물", "", 600, 9, 0) + "," + SbHunt(SbOther, false, false, "시험 이웃 보물", "", 800, 5, 20))));
            Wait(() => R0926HuntList.Board.Count == 3, 5f, "하늘 판 보물찾기 셋");
            Sleep(1.2f);   // 판은 1초마다 다시 그린다
            Do(() => log.Add($"  하늘 판 보물찾기: {board()} · 목록의 근처 보물 {R0926HuntList.Nearby.Count}개 · 줄: {string.Join(" / ", rows(Hunt))}"));
            Shot("sb_hunt_three");
            Check(() =>
            {
                var b = R0926HuntList.Board;
                return b.Count == 3 && b[0].id == SbMine && b[1].id == SbOther && b[2].id == SbFound;
            }, "못 찾은 것 먼저(내 게임 → 근처 게임), 찾은 게임은 뒤 · 참가한 근처 게임은 한 줄만");
            Check(() =>
            {
                var r = rows(Hunt);
                return r.Count == 3 && r[0].StartsWith("시험 내 보물 | 내가 숨긴 보물") && r[1].StartsWith("시험 이웃 보물 | 진행 중") && r[2] == "시험 찾은 보물 | 찾은 보물입니다";
            }, "줄: 내가 숨긴 보물 · 근처 게임(진행 중) · 찾은 보물");
            Check(() => R0926HuntList.Nearby.Count == 2 && !R0926HuntList.Nearby.Exists(n => n.host || n.id == SbMine),
                "목록의 '근처 보물'(Nearby)은 그대로 — 내 게임은 안 들어간다");

            // ③ 축제 — 축제 장은 목록과 같은 축제(광고 → 서버 순서, 끝난 것만 뺌) · 핫플 장엔 가까운 축제만
            Do(() => page(Fest));
            Sleep(0.9f);
            Do(() => log.Add($"  축제 장: {string.Join(" / ", titles(Fest))}"));
            Shot("sb_festival");
            Check(() => string.Join("/", titles(Fest)) == "시험 광고 축제/시험 가까운 축제/시험 먼 축제",
                "축제 장 — 목록과 같은 축제 (광고 → 서버 순서) · 먼 축제(120km · 한 달 뒤)도 · 끝난 축제는 빠짐");
            Do(() => page(Hot));
            Sleep(0.9f);
            Do(() => log.Add($"  핫플 장: {string.Join(" / ", titles(Hot))}"));
            Check(() => { var t = titles(Hot); return pg.EditorCurrent == Hot && t.Contains("시험 가까운 축제") && !t.Contains("시험 먼 축제") && !t.Contains("시험 광고 축제") && !t.Contains("시험 끝난 축제"); },
                "핫플 장엔 가까운(50km · 2주 안) 축제만 섞인다 — 축제 광고 · 먼 축제는 축제 장에만");

            // ④ 보물찾기 참여를 끄면 그 장이 빠진다
            Do(() => SkyTakeHunts("{\"prefs\":{\"enabled\":false}}"));
            Wait(() => R0926HuntList.Board.Count == 0, 5f, "보물찾기 참여 끔 — 비움");
            Sleep(1.2f);
            Check(() => R0926HuntList.Board.Count == 0 && R0926HuntList.Nearby.Count == 0 && !page(Hunt), "보물찾기 참여를 끄면 「근처 보물찾기」 장이 빠진다");

            Check(() => errors.Count == 0, "오류 없음");
            Do(() =>
            {
                foreach (var e in errors) log.Add("  오류: " + e);
                Application.logMessageReceived -= onLog;
                AimCamera(0f, 0f);
                SkyTakeHunts(hunts("true", "", "", ""));
                R0926Nearby.EditorForget();
            });
            Do(NmFinish);   // 하늘 설정 · 시험 주소 · 시험 사용자
        }

        // 시험 보물찾기 한 판 (서버 _view 모양 — 진행 중 · 좌표 공개). 자리는 에디터 가상 위치에서 북쪽으로 meters 만큼 (기록에 남기지 않는다)
        private static string SbHunt(int id, bool host, bool found, string title, string prize, int meters, int players, int max)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var v = VirtualLocation.Instance;
            double la = v != null ? v.Latitude + meters / 111320.0 : 0.0, lo = v != null ? v.Longitude : 0.0;
            return "{\"id\":" + id + ",\"status\":\"live\",\"is_host\":" + (host ? "true" : "false")
                   + ",\"me\":{\"joined\":" + (host || found ? "true" : "false") + ",\"rank\":" + (found ? "1" : "null") + ",\"gave_up\":false},\"full\":false"
                   + ",\"title\":\"" + title + "\",\"prize\":\"" + prize + "\",\"official\":false"
                   + ",\"lat\":" + la.ToString("F6", inv) + ",\"lon\":" + lo.ToString("F6", inv)
                   + ",\"distance_m\":" + meters + ",\"participants\":" + players + ",\"max_players\":" + max + "}";
        }

        // 시험 축제 넷 — 광고(광고 아이디 없음: 노출 · 클릭을 어디에도 적지 않는다) · 가까운(2km · 오늘) · 먼(120km · 한 달 뒤) · 끝난(어제 끝)
        private static void SbFestivals()
        {
            var v = VirtualLocation.Instance;
            double la = v != null ? v.Latitude : 0.0, lo = v != null ? v.Longitude : 0.0;
            Func<int, string> day = d => DateTime.Now.AddDays(d).ToString("yyyyMMdd");
            R0926Nearby.Festivals.Clear();
            R0926Nearby.Festivals.Add(new R0926Nearby.Festival { id = "ad:sb", title = "시험 광고 축제", lat = la + 3.3 / 111.32, lon = lo, start = day(0), end = day(5), icon = "party", promo = true, ad_id = "", sub = "가을 한정", link = "", dist = 3300 });
            R0926Nearby.Festivals.Add(new R0926Nearby.Festival { id = "sb_near", title = "시험 가까운 축제", lat = la + 2.0 / 111.32, lon = lo, start = day(0), end = day(5), icon = "maple", dist = 2000 });
            R0926Nearby.Festivals.Add(new R0926Nearby.Festival { id = "sb_far", title = "시험 먼 축제", lat = la + 120.0 / 111.32, lon = lo, start = day(30), end = day(33), icon = "fireworks", dist = 120000 });
            R0926Nearby.Festivals.Add(new R0926Nearby.Festival { id = "sb_ended", title = "시험 끝난 축제", lat = la + 3.0 / 111.32, lon = lo, start = day(-10), end = day(-1), icon = "crab", dist = 3000 });
        }
    }
}
