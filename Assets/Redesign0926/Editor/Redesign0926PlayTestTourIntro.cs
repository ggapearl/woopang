using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-27 36번 대표님 「각 기관청 API 에 있어서 정보를 다 가져오면 될 것 같거든?」 — 관광공사 소개(detailIntro: 운영 시간 · 쉬는 날 · 요금 · 주차 · 체크인 …)
        //  · 글 만들기(IntroText · CleanIntro): 타입마다 다른 칸 이름 · <br> 줄바꿈 · 링크 태그 · &nbsp; · 「0」 빼기 · 전화번호와 같은 「문의」 빼기 · 행사 기간
        //  · 정보 창을 열 때 받는다 — 숙박 길을 시험 서버(127.0.0.1:5800 · tour_stub.py)로: 소개 · 공통(개요 · 홈페이지)이 창에 들어옴 · 반려견 정보는 묻지 않음(숙박 길)
        //  · 다시 열면 묻지 않고 바로 · 목록을 다시 받아 자료가 새것이 돼도 이어 씀(빈 값으로 전화를 덮지 않음) · 못 받으면 다음에 다시
        //  · 늦게 온 답은 그사이 다른 장소(가게)로 바뀐 창을 덮지 않는다 · 가게 전화(인허가)와 출처 줄
        //  서버 · 관광공사에는 묻지 않는다 (숙박 길만 바꾼다 · 가짜 번호). 끝나면 넣은 장소 · 캐시 · 받은 표시 · 숙박 주소를 되돌린다
        private static void PlanTourIntro()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            const string Stub = "http://127.0.0.1:5800", Dead = "http://127.0.0.1:9";
            string[] ids = { "kt_ti_1", "kt_ti_2", "kt_ti_3" };
            var carry = typeof(TourAPIManager).GetMethod("CarryDetails", NP);
            var doneF = typeof(TourAPIManager).GetField("detailDone", NP);
            var busyF = typeof(TourAPIManager).GetField("detailBusy", NP);
            var detailsF = typeof(TourAPIManager).GetField("cachedPlaceDetails", NP);
            FieldInfo fInfoText = typeof(DoubleTap3D).GetField("placeInfoText", NP);
            TourAPIManager tm = null;
            DoubleTap3D host = null;
            TourPlaceData p1 = null, p3 = null;
            bool opened = false;
            JObject stats = null;

            HashSet<string> DoneSet() => tm != null ? doneF?.GetValue(tm) as HashSet<string> : null;
            HashSet<string> Busy() => tm != null ? busyF?.GetValue(tm) as HashSet<string> : null;
            Dictionary<string, TourPlaceData> Details() => tm != null ? detailsF?.GetValue(tm) as Dictionary<string, TourPlaceData> : null;
            bool Done(string kind, string id) => DoneSet() != null && DoneSet().Contains(kind + ":" + id);
            string Txt() { var t = host != null ? fInfoText?.GetValue(host) as Text : null; return t != null ? t.text ?? "" : ""; }
            bool Open() => host != null && host.fullscreenCanvasGroup != null && host.fullscreenCanvasGroup.gameObject.activeSelf && DoubleTap3D.Shown == host;
            bool Closed() => host == null || host.fullscreenCanvasGroup == null || !host.fullscreenCanvasGroup.gameObject.activeSelf;
            int Count(string key) => stats != null && stats[key] != null ? (int)stats[key] : 0;
            void Stub_(string path)
            {
                try { using (var wc = new System.Net.WebClient()) { wc.Encoding = System.Text.Encoding.UTF8; string s = wc.DownloadString(Stub + "/" + path); if (path == "stats") stats = JObject.Parse(s); } }
                catch (System.Exception e) { log.Add("  시험 서버 " + path + " 실패: " + e.GetType().Name); if (path == "stats") stats = null; }
            }
            TourPlaceData Place(string id, string type, string addr) => new TourPlaceData
            {
                contentid = id, contenttypeid = type, title = "시험 TI " + id.Substring(6), stayOnly = true, petFriendly = false, addr1 = addr, tel = "",
                mapy = VirtualLocation.Instance.Latitude + 0.0004, mapx = VirtualLocation.Instance.Longitude,
            };
            int At(string s, string part) => s.IndexOf(part, System.StringComparison.Ordinal);

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                host = R0926PlaceInfo.Host();
            });
            Wait(() => host != null && host.IsReady, 10f, "대리 정보 창 준비");
            // 관광공사 자료를 다 받은 뒤에 — 받는 중에 숙박 주소를 바꾸면 진짜 숙소 상세가 시험 서버로 간다
            Wait(() => { tm = Object.FindAnyObjectByType<TourAPIManager>(); return tm != null && tm.IsCacheReady; }, 30f, "관광공사 자료 받음");
            Wait(() => tm == null || (tm.IsDataLoaded() && !TourFetching(tm)), 45f, "관광공사 반경별 받기 모두 끝남");
            Check(() => tm != null && tm.isActiveAndEnabled && carry != null && doneF != null && busyF != null && detailsF != null && fInfoText != null,
                  "시험에 쓸 안쪽 함수 · 값을 모두 찾음");

            // 1) 글 만들기 — 함수만 (지어낸 응답)
            string J(string itemJson) => "{\"response\":{\"header\":{\"resultCode\":\"0000\",\"resultMsg\":\"OK\"},\"body\":{\"items\":{\"item\":[" + itemJson + "]},\"numOfRows\":1,\"pageNo\":1,\"totalCount\":1}}}";
            Check(() =>
            {
                string s = TourAPIManager.IntroText(J("{\"contenttypeid\":\"12\",\"usetime\":\"09:00~18:00<br>(입장 마감 17:00)\",\"restdate\":\"매주 월요일\",\"parking\":\"가능\",\"infocenter\":\"02-1234-5678\",\"heritage1\":\"0\",\"chkpet\":\"\"}"), "02-1234-5678");
                log.Add("  관광지 소개 글: " + s.Replace("\n", " ⏎ "));
                return s == "운영 시간: 09:00~18:00\n(입장 마감 17:00)\n\n쉬는 날: 매주 월요일\n\n주차: 가능";
            }, "관광지 — 운영 시간(<br> 은 줄바꿈) · 쉬는 날 · 주차 순 · 위 전화번호와 같은 「문의」는 뺀다 · 빈 칸 · 「0」 은 없음");
            Check(() =>
            {
                string s = TourAPIManager.IntroText(J("{\"contenttypeid\":\"32\",\"checkintime\":\"15:00\",\"checkouttime\":\"11:00\",\"chkcooking\":\"불가\",\"reservationurl\":\"<a href=\\\"https://example.com/r\\\" target=\\\"_blank\\\">https://example.com/r</a>\",\"infocenterlodging\":\"010-9999-0000\",\"subfacility\":\"&nbsp;\"}"), "");
                log.Add("  숙박 소개 글: " + s.Replace("\n", " ⏎ "));
                return s == "체크인: 15:00\n\n체크아웃: 11:00\n\n객실 취사: 불가\n\n예약 사이트: https://example.com/r\n\n문의: 010-9999-0000";
            }, "숙박 — 체크인 · 체크아웃 · 객실 취사 · 예약 사이트(링크 태그는 글만) · 문의(전화가 없으면 보인다) · &nbsp; 뿐인 칸은 없음");
            Check(() =>
            {
                string a = TourAPIManager.IntroText(J("{\"contenttypeid\":\"15\",\"eventstartdate\":\"20261101\",\"eventenddate\":\"20261130\",\"usetimefestival\":\"무료\",\"eventplace\":\"시험 광장\"}"));
                string b = TourAPIManager.IntroText(J("{\"contenttypeid\":\"15\",\"eventstartdate\":\"20261201\",\"eventenddate\":\"20261201\"}"));
                string c = TourAPIManager.IntroText(J("{\"contenttypeid\":\"39\",\"opentimefood\":\"11:00~21:00\",\"restdatefood\":\"연중무휴\",\"firstmenu\":\"시험 국수\",\"packing\":\"가능\"}"));
                log.Add("  행사: " + a.Replace("\n", " ⏎ ") + " | 하루 행사: " + b + " | 음식점: " + c.Replace("\n", " ⏎ "));
                return a == "기간: 2026.11.01 ~ 2026.11.30\n\n이용 요금: 무료\n\n행사 장소: 시험 광장" && b == "기간: 2026.12.01"
                       && c == "운영 시간: 11:00~21:00\n\n쉬는 날: 연중무휴\n\n대표 메뉴: 시험 국수\n\n포장: 가능";
            }, "행사 기간(YYYY.MM.DD · 하루면 한 날짜) · 이용 요금 · 음식점 영업시간 · 대표 메뉴 · 포장 — 타입마다 다른 칸 이름");
            Check(() =>
            {
                string empty = "{\"response\":{\"header\":{\"resultCode\":\"0000\",\"resultMsg\":\"OK\"},\"body\":{\"items\":\"\",\"numOfRows\":10,\"pageNo\":1,\"totalCount\":0}}}";
                string err = "{\"responseTime\":\"x\",\"resultCode\":\"10\",\"resultMsg\":\"INVALID_REQUEST_PARAMETER_ERROR\"}";
                return TourAPIManager.IntroText(empty) == "" && TourAPIManager.IntroText(err) == "" && TourAPIManager.IntroText("{broken") == "" && TourAPIManager.IntroText("") == "";
            }, "비었거나(items 빈 글) · 관광공사 오류 · 깨진 글이면 빈 소개");
            Check(() => TourAPIManager.CleanIntro("0") == null && TourAPIManager.CleanIntro(" - ") == null && TourAPIManager.CleanIntro("  ") == null
                        && TourAPIManager.CleanIntro(" 가능 ") == "가능" && TourAPIManager.CleanIntro("a&nbsp;b &amp; c") == "a b & c"
                        && TourAPIManager.CleanIntro("한 줄<BR/>둘<br />셋") == "한 줄\n둘\n셋" && TourAPIManager.CleanIntro("<b>굵게</b>") == "굵게"
                        && TourAPIManager.CleanIntro("가<br><br><br><br>나") == "가\n\n나",
                  "값 다듬기 — 「0」 · 「-」 · 빈 칸은 없음 · 엔티티는 글자로 · <br> 여러 꼴은 줄바꿈(세 줄 이상은 두 줄로) · 다른 태그는 글만");

            // 2) 정보 창을 열면 받는다 — 숙박 길을 시험 서버로
            Do(() =>
            {
                Stub_("reset");
                TourAPIManager.EditorStayBase = Stub;
                p1 = Place(ids[0], "32", "시험 주소 1");
                tm.GetPlaceDataMap()[p1.contentid] = p1;
                opened = R0926PlaceInfo.Fill(host, p1) && host.OpenInfo();
            });
            Wait(() => Open(), 5f, "관광공사 숙소 정보 창 열림");
            Wait(() => Txt().Contains("체크인: 15:00") && Txt().Contains("홈페이지:") && Txt().Contains("둘째 줄"), 12f, "소개 · 공통이 창에 들어옴");
            Sleep(0.6f);
            Shot("tourintro_stay");
            Check(() =>
            {
                string s = Txt();
                log.Add("  정보 글: " + s.Replace("\n", " ⏎ "));
                bool has = new[] { "시험 주소 1", "체크인: 15:00", "체크아웃: 11:00", "객실 취사: 가능", "주차: 가능\n(10대)", "예약: 전화 예약",
                                   "예약 사이트: https://example.com/stay", "문의: 010-0000-0000", "홈페이지: https://example.com", "시험 개요\n둘째 줄" }.All(s.Contains);
                bool clean = !s.Contains("<") && !s.Contains("&nbsp;") && !s.Contains("픽업") && !s.Contains("부대시설") && !s.Contains("객실:");
                bool order = At(s, "시험 주소 1") < At(s, "체크인:") && At(s, "체크인:") < At(s, "홈페이지:") && At(s, "홈페이지:") < At(s, "시험 개요");
                return opened && has && clean && order;
            }, "창을 열면 소개(체크인 · 체크아웃 · 취사 · 주차 · 예약 · 문의) · 홈페이지 · 개요가 들어온다 — 주소 → 소개 → 개요 순 · 태그 · 빈 칸 없음");
            Do(() => Stub_("stats"));
            Check(() =>
            {
                log.Add("  시험 서버가 받은 요청: " + (stats != null ? stats.ToString(Newtonsoft.Json.Formatting.None) : "모름"));
                bool noPet = stats != null && !stats.Properties().Any(p => p.Name.StartsWith("detailPetTour"));
                return Count("detailIntro:kt_ti_1") == 1 && Count("detailCommon:kt_ti_1") == 1 && noPet
                       && Done("intro", ids[0]) && Done("common", ids[0]) && Busy() != null && !Busy().Contains(ids[0]);
            }, "소개 · 공통 한 번씩 · 숙박 길이라 반려견 정보는 묻지 않음 · 받은 표시 남고 받는 중 표시는 풀림");

            // 3) 다시 열면 묻지 않고 바로
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => Closed(), 5f, "정보 창 닫힘");
            Do(() => opened = R0926PlaceInfo.Fill(host, p1) && host.OpenInfo());
            Wait(() => Open(), 5f, "다시 열림");
            Check(() => Txt().Contains("체크인: 15:00") && Txt().Contains("시험 개요"), "다시 열면 받은 소개 · 개요가 바로 보인다");
            Sleep(1.0f);
            Do(() => Stub_("stats"));
            Check(() => Count("detailIntro:kt_ti_1") == 1 && Count("detailCommon:kt_ti_1") == 1, "다시 열어도 관광공사에 또 묻지 않는다 (하루 호출 한도)");

            // 4) 목록을 다시 받아 같은 장소 자료가 새것이 될 때 — 받은 상세를 옮겨 담는다 · 빈 값으로 전화를 덮지 않는다
            Check(() =>
            {
                var fresh = Place(ids[0], "32", "시험 주소 1");
                fresh.tel = "02-5555-5555";   // 목록에서 온 전화 — 공통 상세는 빈 전화로 왔다
                tm.GetPlaceDataMap()[fresh.contentid] = fresh;
                carry.Invoke(tm, new object[] { fresh });
                log.Add($"  새 자료: 소개 {(string.IsNullOrEmpty(fresh.intro) ? "없음" : "있음")} · 홈페이지 「{fresh.homepage}」 · 전화 「{fresh.tel}」");
                bool ok = fresh.intro != null && fresh.intro.Contains("체크인: 15:00") && fresh.homepage == "https://example.com"
                          && fresh.overview != null && fresh.overview.Contains("둘째 줄") && fresh.tel == "02-5555-5555"
                          && Details() != null && Details().TryGetValue(fresh.contentid, out var holder) && holder == fresh;
                p1 = fresh;
                return ok;
            }, "목록을 다시 받아도 받은 소개 · 홈페이지 · 개요를 이어 쓴다 · 목록의 전화는 빈 값으로 덮지 않는다");

            // 5) 못 받으면(닫힌 자리) 표시하지 않는다 — 다음에 창을 열 때 다시
            Do(() =>
            {
                DoubleTap3D.CloseShown();
                TourAPIManager.EditorStayBase = Dead;
                var p2 = Place(ids[1], "39", "시험 주소 2");
                tm.GetPlaceDataMap()[p2.contentid] = p2;
                opened = R0926PlaceInfo.Fill(host, p2) && host.OpenInfo();
            });
            Wait(() => Open(), 5f, "닫힌 자리 장소 정보 창 열림");
            Wait(() => Busy() != null && !Busy().Contains(ids[1]), 40f, "닫힌 자리 — 받기를 포기하고 끝남");
            Check(() =>
            {
                string s = Txt();
                log.Add($"  닫힌 자리: 소개 표시 {Done("intro", ids[1])} · 공통 표시 {Done("common", ids[1])} · 글 「{s.Replace("\n", " ⏎ ")}」");
                return opened && !Done("intro", ids[1]) && !Done("common", ids[1]) && s.Contains("시험 주소 2") && !s.Contains("운영 시간");
            }, "못 받으면 받은 표시를 남기지 않는다(다음에 열 때 다시) · 창은 주소만으로 그대로");

            // 6) 늦게 온 답은 그사이 다른 장소(가게)로 바뀐 창을 덮지 않는다 · 가게 전화(인허가) · 출처 줄
            Do(() =>
            {
                DoubleTap3D.CloseShown();
                TourAPIManager.EditorStayBase = Stub;
                p3 = Place(ids[2], "32", "시험 주소 3");
                tm.GetPlaceDataMap()[p3.contentid] = p3;
                opened = R0926PlaceInfo.Fill(host, p3) && host.OpenInfo();
            });
            Sleep(0.4f);
            Do(() =>
            {
                var s = new StoreData { id = "st_ti_1", name = "시험 TI 가게", category = "food", upjong = "한식", floor = "1", addr = "시험 가게 주소", tel = "02-1234-5678",
                                        lat = VirtualLocation.Instance.Latitude, lon = VirtualLocation.Instance.Longitude };
                opened = R0926PlaceInfo.Fill(host, s) && host.OpenInfo();
            });
            Wait(() => Busy() != null && !Busy().Contains(ids[2]), 15f, "늦은 소개 · 공통 받기 끝");
            Sleep(0.6f);
            Shot("tourintro_store");
            Check(() =>
            {
                string s = Txt();
                log.Add("  가게 정보 글: " + s.Replace("\n", " ⏎ "));
                return opened && Open() && s.Contains("시험 가게 주소") && s.Contains("02-1234-5678") && s.Contains("전화: 행정안전부 지방행정 인허가 정보")
                       && !s.Contains("체크인") && !s.Contains("시험 주소 3");
            }, "늦게 온 관광공사 답이 가게 창을 덮지 않는다 · 가게 전화(인허가 영업 중) · 출처 줄");
            Check(() => p3.intro != null && p3.intro.Contains("체크인: 14:00") && Done("intro", ids[2]) && Done("common", ids[2]),
                  "늦게 온 답도 그 장소 자료에는 담긴다 (다음에 열면 바로)");
            Do(() =>
            {
                var s = new StoreData { id = "st_ti_2", name = "시험 TI 가게 둘", category = "shop", upjong = "잡화", floor = "1", addr = "시험 가게 주소 2", tel = "",
                                        lat = VirtualLocation.Instance.Latitude, lon = VirtualLocation.Instance.Longitude };
                opened = R0926PlaceInfo.Fill(host, s) && host.OpenInfo();
            });
            Wait(() => Open() && Txt().Contains("시험 가게 주소 2"), 5f, "전화 없는 가게 창");
            Check(() => !Txt().Contains("인허가") && !Txt().Contains("02-"), "인허가와 맞지 않은 가게는 전화 줄 · 인허가 출처 줄이 없다");

            // 되돌리기
            Do(() =>
            {
                DoubleTap3D.CloseShown();
                TourAPIManager.EditorStayBase = null;
                if (tm != null)
                {
                    var det = Details();
                    var done = DoneSet();
                    var busy = Busy();
                    foreach (var id in ids)
                    {
                        tm.GetPlaceDataMap().Remove(id);
                        det?.Remove(id);
                        busy?.Remove(id);
                        if (done != null) foreach (var k in new[] { "img", "common", "pet", "intro" }) done.Remove(k + ":" + id);
                    }
                }
                if (host != null)
                {
                    host.ResetData();
                    DataManager.Instance?.ConfigureInfo(host, null);
                }
            });
            Wait(() => Closed(), 5f, "정보 창 닫힘");
            Check(() => TourAPIManager.EditorStayBase == null && tm != null && !ids.Any(id => tm.GetPlaceDataMap().ContainsKey(id))
                        && (Details() == null || !ids.Any(Details().ContainsKey)) && (DoneSet() == null || !DoneSet().Any(k => k.Contains(":kt_ti_")))
                        && host != null && host.InfoSource == null,
                  "넣은 장소 · 캐시 · 받은 표시 · 숙박 주소 · 창 자료 모두 되돌림");
        }
    }
}
