using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 L3 — 대표님 「숙박업(펜션·호텔·모텔·민박) 정부 공개 API 있는지 확인 → 있으면 받아와서 '펜션·호텔' 분류로」 (같은 날 이름은 「'숙소' 라고」).
    ///  · stayapi : 국문 관광정보(KorService2) 숙박 목록을 반려동물 여행 자료에 합치는 길 — 가짜 응답으로, 서버 · 관광공사에 묻지 않는다
    ///    (숙박 주소는 닫힌 자리 127.0.0.1:9 · 가짜 번호 9개는 상세 캐시와 받은 표시(detailDone · 11-27 종류별)를 미리 채워 상세를 묻지 않는다 · 404 는 시험 서버 5792) →
    ///    같은 번호는 반려동물 자료 쪽(애견동반 · 이름) · 숙박 목록에 섞여 온 다른 타입은 버림 · 숙박 목록에서만 온 곳은 애견동반 아님 · 가까운 순 · 상세는 숙박 길 /
    ///    '전체'에선 AR 숙소 4곳까지(목록은 전부) · '숙소'를 고르면 전부 · 애견동반을 켜면 애견 숙소만 (목록 · AR · 정보 창 애견 표시) /
    ///    3D 로 띄운 숙소만 상세 사진 · 숙박 목록이 없거나 깨져도 반려동물 자료만 · 서버에 숙박 길이 없으면(404) 다시 묻지 않음.
    ///    끝나면 빛 캐시 · 넣은 장소 · 상세 캐시 · 칩 · 저장값 · 숙박 주소를 모두 되돌린다
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanStayApi()
        {
            const string PrefCat = "Filter_Category_V1", PrefPet = "Filter_PetFriendly_V3", PrefPub = "Filter_Stores_V1";   // 관광공사 자료 = 「공공데이터」 칩 (11-27 · 예전 「공공기관」)
            const string DeadBase = "http://127.0.0.1:9", DeadPhoto = "http://127.0.0.1:9/l3_stay.jpg", NoPath = "http://127.0.0.1:5792/l3_no_such_path";
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            // 1 관광지(반려동물 자료) · 2 숙박(반려동물 자료에도 숙박 목록에도) · 3~8 숙박 목록에만 · 9 숙박 목록에 섞여 온 관광지(버려야 한다)
            string[] ids = { "kt_l3_1", "kt_l3_2", "kt_l3_3", "kt_l3_4", "kt_l3_5", "kt_l3_6", "kt_l3_7", "kt_l3_8", "kt_l3_9" };
            double[] north = { 0.0007, 0.0003, 0.0005, 0.0009, 0.0011, 0.0013, 0.0015, 0.0017, 0.0001 };
            string[] names = { "시험 L3 관광지", "시험 L3 애견 숙소", "시험 L3 숙소 3", "시험 L3 숙소 4", "시험 L3 숙소 5", "시험 L3 숙소 6", "시험 L3 숙소 7", "시험 L3 숙소 8", "시험 L3 섞여 온 관광지" };
            const string DupName = "시험 L3 숙박 목록 쪽 이름";
            string[] stayIds = ids.Skip(1).Take(7).ToArray();       // 숙소 7곳 (2~8)
            string[] stayOnlyIds = ids.Skip(2).Take(6).ToArray();   // 숙박 목록에서만 온 6곳 (3~8)
            string[] stayOnlyNames = names.Skip(2).Take(6).ToArray();
            const string SortedIds = "kt_l3_2,kt_l3_3,kt_l3_1,kt_l3_4,kt_l3_5,kt_l3_6,kt_l3_7,kt_l3_8";
            const string EmptyJson = "{\"response\":{\"header\":{\"resultCode\":\"0000\",\"resultMsg\":\"OK\"},\"body\":{\"items\":\"\",\"numOfRows\":100,\"pageNo\":1,\"totalCount\":0}}}";
            const string ErrJson = "{\"responseTime\":\"x\",\"resultCode\":\"10\",\"resultMsg\":\"INVALID_REQUEST_PARAMETER_ERROR\"}";

            var process = typeof(TourAPIManager).GetMethod("ProcessTourData", NP);
            var merge = typeof(TourAPIManager).GetMethod("MergeStays", NP);
            var fetchText = typeof(TourAPIManager).GetMethod("FetchText", NP);
            var baseFor = typeof(TourAPIManager).GetMethod("BaseFor", NP);
            var detailsF = typeof(TourAPIManager).GetField("cachedPlaceDetails", NP);
            var triedF = typeof(TourAPIManager).GetField("stayDetailTried", NP);
            var doneF = typeof(TourAPIManager).GetField("detailDone", NP);   // 상세 종류별 받은 표시 — 사진 · 공통 · 반려견 · 소개 (11-27)
            string[] detailKinds = { "img", "common", "pet", "intro" };
            var fullF = typeof(FilterManager).GetField("currentFullAllocations", NP);
            var indF = typeof(FilterManager).GetField("currentIndicatorAllocations", NP);
            // 공공데이터는 실제 값을 본다 — 칩은 분류를 고른 동안 회색 '체크'로 묶이고, 칩 격자가 닫혀 있으면 찾지 못해 늘 꺼짐으로 읽혔다
            var pubF = typeof(FilterManager).GetField("filterStores", NP);
            var dtPet = typeof(DoubleTap3D).GetField("petFriendly", NP);
            var dtUrls = typeof(DoubleTap3D).GetField("imageUrls", NP);

            TourAPIManager tm = null;
            List<CachedPlaceData> cacheWas = null;
            var catWas = FilterManager.CategoryFilterState.All;
            bool petWas = false, pubWas = true;
            int prefCatWas = 0, prefPetWas = 0, prefPubWas = 1;
            bool hadPrefCat = false, hadPrefPet = false, hadPrefPub = false;
            bool procDone = false, ftDone = false;
            string ftGot = "x", petJson = null, stayJson = null, photoId = null;
            float ftSec = -1f;

            FilterManager Fm() => UnityEngine.Object.FindAnyObjectByType<FilterManager>();
            bool PetOn() => Fm() != null && Fm().PetState == FilterManager.PetFriendlyFilterState.OnlyPetFriendly;
            bool PubOn() => Fm() != null && pubF?.GetValue(Fm()) is bool b && b;
            Dictionary<string, TourPlaceData> Details() => tm != null ? detailsF?.GetValue(tm) as Dictionary<string, TourPlaceData> : null;
            HashSet<string> Tried() => tm != null ? triedF?.GetValue(tm) as HashSet<string> : null;
            HashSet<string> DoneSet() => tm != null ? doneF?.GetValue(tm) as HashSet<string> : null;
            HashSet<string> Allocated()
            {
                var s = new HashSet<string>();
                var fm = Fm();
                if (fm == null) return s;
                foreach (var f in new[] { fullF, indF })
                    if (f?.GetValue(fm) is HashSet<string> h)
                        foreach (var u in h) if (u.StartsWith("tour_kt_l3_")) s.Add(u.Substring(5));
                return s;
            }
            bool Visible(string id)
            {
                if (tm == null) return false;
                bool full = tm.GetSpawnedObjects().TryGetValue(id, out var go) && go != null && go.activeSelf;
                bool ind = tm.GetIndicatorOnlyObjects().TryGetValue(id, out var io) && io != null && io.activeSelf;
                return full || ind;
            }
            DoubleTap3D Info(string id) => tm != null && tm.GetSpawnedObjects().TryGetValue(id, out var go) && go != null ? go.GetComponentInChildren<DoubleTap3D>(true) : null;
            List<PlaceListManager.ListEntry> Rows()
            {
                var p = Plm();
                return p == null ? new List<PlaceListManager.ListEntry>()
                                 : p.ShownEntries.Where(e => e.kind == PlaceListManager.EntryKind.Place && e.name != null && e.name.StartsWith("시험 L3")).ToList();
            }
            bool Row(string name) => Rows().Any(e => e.name == name);
            string RowNames() => string.Join(" · ", Rows().Select(e => e.name.Replace("시험 L3 ", "") + (e.pet ? "(애견)" : "")));
            string Ids(IEnumerable<string> s) => string.Join(",", s.OrderBy(x => x, StringComparer.Ordinal).Select(x => x.Replace("kt_l3_", "")));
            List<TourPlaceData> PetList() => JsonConvert.DeserializeObject<TourAPIResponse>(petJson)?.response?.body?.items?.item;
            List<TourPlaceData> Merge(List<TourPlaceData> l, string s) => merge?.Invoke(tm, new object[] { l, s }) as List<TourPlaceData>;
            void Realloc() => Fm()?.TriggerReallocation();

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Wait(() => DataManager.Instance != null && DataManager.Instance.GetSpawnedObjects().Count > 0, 40f, "주변 장소가 AR 에 뜸");
            // 관광공사 자료를 다 받은 뒤에 — 받는 중이면 받은 목록이 넣은 것을 지운다 (반경별로 여러 번 받는다)
            Wait(() => { tm = UnityEngine.Object.FindAnyObjectByType<TourAPIManager>(); return tm != null && tm.IsCacheReady; }, 30f, "관광공사 자료 받음");
            Wait(() => tm == null || (tm.IsDataLoaded() && !TourFetching(tm)), 45f, "관광공사 반경별 받기 모두 끝남");
            Sleep(1.0f);
            Check(() => tm != null && process != null && merge != null && fetchText != null && baseFor != null && detailsF != null && triedF != null && doneF != null
                        && fullF != null && indF != null && pubF != null && dtPet != null && dtUrls != null, "시험에 쓸 안쪽 함수 · 값을 모두 찾음");

            Do(() =>
            {
                tm = UnityEngine.Object.FindAnyObjectByType<TourAPIManager>();
                var fm = Fm();
                if (tm == null) return;
                // 실제로 받은 것 — 개수만 (서버에 숙박 길이 아직 없으면 숙박 목록에서 온 곳 0 · 404 라 다시 묻지 않는다)
                var real = tm.GetPlaceDataMap().Values.ToList();
                log.Add($"  실제로 받은 관광공사 자료 {real.Count}곳 — 숙박 목록에서 온 곳 {real.Count(p => p.stayOnly)} · 숙소 분류 {real.Count(p => TourAPIManager.CategoryOf(p) == "stay")} · 빛 캐시 {tm.GetCachedPlaces().Count}");

                catWas = fm != null ? fm.CategoryState : FilterManager.CategoryFilterState.All;
                petWas = PetOn();
                pubWas = PubOn();
                hadPrefCat = PlayerPrefs.HasKey(PrefCat); prefCatWas = PlayerPrefs.GetInt(PrefCat, 0);
                hadPrefPet = PlayerPrefs.HasKey(PrefPet); prefPetWas = PlayerPrefs.GetInt(PrefPet, 0);
                hadPrefPub = PlayerPrefs.HasKey(PrefPub); prefPubWas = PlayerPrefs.GetInt(PrefPub, 1);
                if (fm != null && catWas != FilterManager.CategoryFilterState.All) fm.SetCategory(FilterManager.CategoryFilterState.All);
                if (petWas) ClickToggle("PetFriendlyToggle");
                if (!pubWas) ClickToggle("StoresToggle");

                cacheWas = new List<CachedPlaceData>(tm.GetCachedPlaces());
                TourAPIManager.EditorStayBase = DeadBase;
                var det = Details();
                if (det != null) foreach (var id in ids) det[id] = new TourPlaceData { contentid = id, imageUrls = new List<string>() };   // 가짜 번호는 상세를 묻지 않는다
                var doneSet = DoneSet();
                if (doneSet != null) foreach (var id in ids) foreach (var k in detailKinds) doneSet.Add(k + ":" + id);   // 종류별로 받은 것으로 (사진 · 공통 · 반려견 · 소개)

                double la = VirtualLocation.Instance.Latitude, lo = VirtualLocation.Instance.Longitude;
                object Item(int i, string type, string title) => new
                {
                    contentid = ids[i], contenttypeid = type, title,
                    mapx = (lo + 0.0002 * (i % 3)).ToString("F7", CultureInfo.InvariantCulture),
                    mapy = (la + north[i]).ToString("F7", CultureInfo.InvariantCulture),
                    firstimage = "", firstimage2 = "",
                };
                string Json(params object[] items) => JsonConvert.SerializeObject(new
                {
                    response = new
                    {
                        header = new { resultCode = "0000", resultMsg = "OK" },
                        body = new { items = new { item = items }, numOfRows = 100, pageNo = 1, totalCount = items.Length },
                    },
                });
                petJson = Json(Item(0, "12", names[0]), Item(1, "32", names[1]));
                // 숙박 목록은 일부러 뒤섞어 — 가까운 순은 앱이 맞춘다 · 2번은 이름이 다르게 한 번 더 · 9번은 숙박이 아닌 타입
                stayJson = Json(Item(7, "32", names[7]), Item(1, "32", DupName), Item(8, "12", names[8]), Item(3, "32", names[3]), Item(2, "32", names[2]),
                                Item(6, "32", names[6]), Item(4, "32", names[4]), Item(5, "32", names[5]));
                log.Add("  넣을 것: 반려동물 자료 2곳 · 숙박 목록 8곳(겹침 1 · 다른 타입 1) — 상세 캐시 " + (det != null ? "미리 채움" : "못 채움"));
            });
            Sleep(0.6f);   // 칩은 누른 다음 프레임에 바뀐다
            Do(() => log.Add("  시험 시작 칩 — 공공데이터 " + (PubOn() ? "켜짐" : "꺼짐") + " · 애견동반 " + (PetOn() ? "켜짐" : "꺼짐") + " · 분류 " + Fm()?.CategoryState));

            // 합치기 — 함수 하나만 (빛 캐시 · AR 은 아직 그대로)
            Check(() =>
            {
                var a = Merge(PetList(), null);
                var b = Merge(PetList(), "{broken");
                var c = Merge(PetList(), EmptyJson);
                var d = Merge(PetList(), ErrJson);
                return a?.Count == 2 && b?.Count == 2 && c?.Count == 2 && d?.Count == 2 && a.All(p => p.petFriendly && !p.stayOnly);
            }, "숙박 목록이 없거나 · 깨졌거나 · 0곳(items 빈 글) · 관광공사 오류여도 반려동물 자료 그대로 (예전과 같다)");
            Check(() =>
            {
                var m = Merge(PetList(), stayJson);
                if (m == null) return false;
                var two = m.Find(p => p.contentid == ids[1]);
                log.Add($"  합친 것 {m.Count}곳 — 숙박 목록에서만 {m.Count(p => p.stayOnly)} · 애견동반 {m.Count(p => p.petFriendly)}");
                return m.Count == 8 && two != null && two.title == names[1] && two.petFriendly && !two.stayOnly && m.Count(p => p.contentid == ids[1]) == 1
                       && stayOnlyIds.All(id => m.Any(p => p.contentid == id && p.stayOnly && !p.petFriendly && p.imageUrls != null))
                       && !m.Any(p => p.contentid == ids[8]);
            }, "합치기 — 같은 번호는 반려동물 자료 쪽(애견동반 · 이름) · 숙박 목록에서만 온 6곳은 애견동반 아님 · 섞여 온 관광지는 버림");
            Check(() => { var m = Merge(null, stayJson); return m != null && m.Count == 7 && m.All(p => p.stayOnly && !p.petFriendly); },
                  "반려동물 자료가 비어도 숙박 목록 7곳은 들어온다");
            Check(() =>
            {
                string s = baseFor.Invoke(tm, new object[] { new TourPlaceData { stayOnly = true } }) as string;
                string p = baseFor.Invoke(tm, new object[] { new TourPlaceData() }) as string;
                TourAPIManager.EditorStayBase = null;
                string live = baseFor.Invoke(tm, new object[] { new TourPlaceData { stayOnly = true } }) as string;
                TourAPIManager.EditorStayBase = DeadBase;
                return s == DeadBase && p == ApiConfig.TOUR_API_PROXY && live == ApiConfig.TOUR_API_PROXY + "/stay";
            }, "상세 길 — 숙박 목록에서만 온 곳은 숙박 길(/proxy/stay) · 나머지는 예전 반려동물 길");

            // 서버에 숙박 길이 없을 때(404) — 다시 묻지 않고 바로 끝 (5초 기다림 없음)
            Do(() =>
            {
                ftDone = false; ftGot = "x";
                float t0 = Time.realtimeSinceStartup;
                var it = fetchText?.Invoke(tm, new object[] { NoPath, 2, (Action<string>)(s => ftGot = s), true }) as IEnumerator;
                tm.StartCoroutine(L3Run(tm, it, () => { ftSec = Time.realtimeSinceStartup - t0; ftDone = true; }));
            });
            Wait(() => ftDone, 15f, "404 받기 끝");
            Do(() => log.Add($"  404 — {ftSec:0.0}초 만에 끝"));
            Check(() => ftDone && ftGot == null && ftSec >= 0f && ftSec < 3f, "서버에 숙박 길이 없으면(404) 다시 묻지 않는다 — 바로 반려동물 자료만 (예전 서버에서도 늦어지지 않는다)");

            // 진짜 처리 함수에 가짜 응답 둘 — 합치기 · 가까운 순 · 빛 캐시 · 장소 지도
            Do(() =>
            {
                procDone = false;
                var it = process?.Invoke(tm, new object[] { petJson, stayJson, (float)VirtualLocation.Instance.Latitude, (float)VirtualLocation.Instance.Longitude }) as IEnumerator;
                tm.StartCoroutine(L3Run(tm, it, () => procDone = true));
            });
            Wait(() => procDone, 30f, "가짜 응답 처리 끝");
            Check(() => tm != null && string.Join(",", tm.GetCachedPlaces().Select(c => c.rawId)) == SortedIds,
                  "빛 캐시는 가까운 순 — 반려동물 자료와 숙박이 섞여 와도 거리대로 (상한을 넘으면 먼 곳부터 빠진다)");
            Check(() =>
            {
                var c = tm.GetCachedPlaces();
                CachedPlaceData Of(string id) => c.Find(x => x.rawId == id);
                return c.Count == 8 && c.All(x => x.sourceManager == "TourAPI" && x.filterKey == "publicData")
                       && Of(ids[0])?.category == "tour" && Of(ids[0]).petFriendly
                       && stayIds.All(id => Of(id)?.category == "stay") && Of(ids[1]).petFriendly && stayOnlyIds.All(id => !Of(id).petFriendly);
            }, "빛 캐시 — 숙소 7곳은 '숙소' 분류 · 관광지는 관광 · 애견동반은 반려동물 자료 두 곳만 · 모두 공공데이터 칩");
            Check(() =>
            {
                var map = tm.GetPlaceDataMap();
                return stayOnlyIds.All(id => map.TryGetValue(id, out var p) && p.stayOnly && !p.petFriendly && p.imageUrls != null)
                       && map.TryGetValue(ids[1], out var two) && !two.stayOnly && two.title == names[1] && !map.ContainsKey(ids[8]);
            }, "장소 지도 — 숙박 목록에서만 온 곳 표시 · 겹친 번호는 반려동물 자료 이름 · 섞여 온 관광지 없음");

            // '전체' — AR 숙소는 가까운 4곳까지 · 목록은 전부
            Do(Realloc);
            Sleep(2.5f);
            Wait(() => Fm() == null || !Fm().Dripping, 10f, "배분대로 다 만듦");
            Do(() => log.Add($"  '전체' 배분된 시험 장소 {Ids(Allocated())} · 보이는 숙소 {stayIds.Count(Visible)}곳"));
            Check(() => { int n = Allocated().Count(stayIds.Contains); return n >= 1 && n <= 4; }, "'전체'에선 숙소는 가까운 4곳까지만 배분 (3D · 화살표 합쳐 — 사람이 올린 곳이 밀려나지 않게)");
            Check(() => stayIds.Count(Visible) <= 4, "'전체' AR 에 보이는 숙소도 4곳까지");
            Check(() => stayOnlyIds.Where(Visible).All(Allocated().Contains), "숙박 목록에서만 온 숙소는 배분이 고른 곳만 AR 에 (처리 중에 스스로 띄우지 않는다)");
            // 숙소는 상세 사진 주소를 묻고 그 사진까지 받은 뒤에 뜬다(11-19 P1) — 물은 곳이 다 뜰 때까지
            Wait(() =>
            {
                var tr = Tried();
                return tr != null && stayOnlyIds.Where(tr.Contains).All(id => tm.GetSpawnedObjects().TryGetValue(id, out var go) && go != null);
            }, 20f, "상세를 물은 숙소가 3D 로 다 뜸");
            Check(() =>
            {
                var tr = Tried();
                if (tr == null) return false;
                var asked = stayOnlyIds.Where(tr.Contains).ToList();
                var shown3d = stayOnlyIds.Where(id => tm.GetSpawnedObjects().TryGetValue(id, out var go) && go != null).ToList();
                log.Add($"  상세 사진을 물은 숙소 {Ids(asked)} · 3D 로 뜬 숙소 {Ids(shown3d)}");
                return asked.Count == shown3d.Count && asked.All(shown3d.Contains);
            }, "숙소 상세 사진은 3D 로 띄운 곳만 묻는다 (도심엔 숙소가 수십 곳 · 하루 호출 한도)");
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => Plm()?.UpdateUI());
            Sleep(0.4f);
            Do(() => log.Add("  '전체' 목록 시험 줄: " + RowNames()));
            Check(() => names.Take(8).All(Row) && !Row(DupName) && !Row(names[8]),
                  "'전체' 목록 — 관광지 · 숙소 7곳 모두 (목록은 줄이지 않는다 · 화장실과 같다) · 겹친 이름 · 섞여 온 관광지 없음");
            Check(() => Rows().Where(e => e.name == names[1]).All(e => e.pet) && Rows().Any(e => e.name == names[1])
                        && Rows().Where(e => stayOnlyNames.Contains(e.name)).All(e => !e.pet),
                  "목록 애견 표시 — 애견 숙소만 · 숙박 목록에서만 온 숙소는 아님");

            // 사진 — 아직 3D 로 뜨지 않은 숙소 하나의 상세 캐시를 새 목록으로 (처음 만들 때 넘기는 빈 목록과 다른 것) → '숙소'에서 처음 뜰 때 정보 창에 들어가야 한다
            Do(() =>
            {
                var tr = Tried();
                photoId = stayOnlyIds.LastOrDefault(id => !tm.GetSpawnedObjects().ContainsKey(id) && (tr == null || !tr.Contains(id)));
                var det = Details();
                if (photoId != null && det != null && det.TryGetValue(photoId, out var d)) d.imageUrls = new List<string> { DeadPhoto };
                log.Add("  사진 시험 숙소: " + (photoId != null ? photoId.Replace("kt_l3_", "") : "없음"));
            });

            // '숙소' — 한도 없음
            Do(() => { Fm()?.SetCategory(FilterManager.CategoryFilterState.Stay); Realloc(); });
            Sleep(3f);
            Wait(() => Fm() == null || !Fm().Dripping, 10f, "배분대로 다 만듦");
            Do(() => Plm()?.UpdateUI());
            Sleep(0.4f);
            Shot("stayapi_stay");
            Do(() => log.Add($"  '숙소' 배분된 시험 장소 {Ids(Allocated())} · 보이는 숙소 {stayIds.Count(Visible)}곳 · 목록: " + RowNames()));
            Check(() => stayIds.All(Allocated().Contains) && !Allocated().Contains(ids[0]), "'숙소'를 고르면 숙소 7곳 모두 배분 · 관광지는 빠짐 (한도 없음)");
            Check(() => stayIds.All(Visible) && !Visible(ids[0]), "AR 에도 숙소 7곳 · 관광지 안 보임");
            Check(() => names.Skip(1).Take(7).All(Row) && !Row(names[0]) && Rows().All(e => e.icon == "stay"), "목록도 숙소 7곳만 · 줄 그림 stay");
            Check(() =>
            {
                var two = Info(ids[1]);
                var only = stayOnlyIds.Select(Info).Where(d => d != null).ToList();
                log.Add($"  정보 창 — 애견 숙소 {(two != null ? "3D" : "화살표")} · 숙박 목록 숙소 3D {only.Count}곳");
                return (two == null || (bool)dtPet.GetValue(two)) && only.Count > 0 && only.All(d => !(bool)dtPet.GetValue(d));
            }, "정보 창 애견 표시 — 애견 숙소는 켜짐 · 숙박 목록에서만 온 숙소는 꺼짐 (예전엔 관광공사 자료면 모두 켜짐)");
            Check(() =>
            {
                if (photoId == null) { log.Add("  사진 시험 못 함 — 숙소가 모두 '전체'에서 이미 3D 로 떴다"); return false; }
                var d = Info(photoId);
                var urls = d != null ? dtUrls.GetValue(d) as List<string> : null;
                return Tried() != null && Tried().Contains(photoId) && urls != null && urls.Count == 1 && urls[0] == DeadPhoto;
            }, "숙소를 처음 3D 로 띄우면 그때 상세 사진을 받아 정보 창에 (숙박 길 · 한 번만)");

            // '숙소' + 애견동반 — 애견 숙소만
            Do(() => { if (!PetOn()) ClickToggle("PetFriendlyToggle"); Realloc(); });
            Sleep(3f);
            Wait(() => Fm() == null || !Fm().Dripping, 10f, "배분대로 다 만듦");
            Do(() => Plm()?.UpdateUI());
            Sleep(0.4f);
            Do(() => log.Add($"  '숙소' + 애견동반 배분 {Ids(Allocated())} · 목록: " + RowNames()));
            Check(() => PetOn() && Allocated().SetEquals(new[] { ids[1] }), "애견동반을 켜면 배분은 애견 숙소 하나만 (숙박 목록에서만 온 6곳은 애견동반 자료가 아니다)");
            Check(() => Visible(ids[1]) && !stayOnlyIds.Any(Visible), "AR 도 애견 숙소만");
            Check(() => Rows().Count == 1 && Row(names[1]) && Rows()[0].pet, "목록도 애견 숙소 한 줄 (애견 표시)");

            // 되돌리기 — 칩 · 저장값 · 넣은 장소 · 상세 캐시 · 빛 캐시 · 숙박 주소
            Do(() =>
            {
                if (PetOn() != petWas) ClickToggle("PetFriendlyToggle");
                // 분류부터 '전체'로 — 분류를 풀 때 맡겨 둔 공공데이터 값이 되돌아오므로 그 뒤에 눌러야 남는다
                Fm()?.SetCategory(FilterManager.CategoryFilterState.All);
                if (PubOn() != pubWas) ClickToggle("StoresToggle");
                if (catWas != FilterManager.CategoryFilterState.All) Fm()?.SetCategory(catWas);
            });
            Sleep(0.6f);   // 칩이 바뀌며 저장값을 쓴 다음에 저장값을 되돌린다
            Do(() =>
            {
                if (tm != null)
                {
                    var det = Details();
                    var tr = Tried();
                    var doneSet = DoneSet();
                    foreach (var id in ids)
                    {
                        tm.DespawnFullObject(id);
                        tm.DespawnIndicatorOnly(id);
                        tm.GetPlaceDataMap().Remove(id);
                        det?.Remove(id);
                        tr?.Remove(id);
                        if (doneSet != null) foreach (var k in detailKinds) doneSet.Remove(k + ":" + id);
                    }
                    if (cacheWas != null)
                    {
                        tm.GetCachedPlaces().Clear();
                        tm.GetCachedPlaces().AddRange(cacheWas);
                    }
                }
                TourAPIManager.EditorStayBase = null;
                if (hadPrefCat) PlayerPrefs.SetInt(PrefCat, prefCatWas); else PlayerPrefs.DeleteKey(PrefCat);
                if (hadPrefPet) PlayerPrefs.SetInt(PrefPet, prefPetWas); else PlayerPrefs.DeleteKey(PrefPet);
                if (hadPrefPub) PlayerPrefs.SetInt(PrefPub, prefPubWas); else PlayerPrefs.DeleteKey(PrefPub);
                PlayerPrefs.Save();
                Realloc();
                Plm()?.UpdateUI();
                log.Add("  되돌림: 분류 " + Fm()?.CategoryState + " · 애견동반 " + (PetOn() ? "켜짐" : "꺼짐") + " · 공공데이터 " + (PubOn() ? "켜짐" : "꺼짐")
                        + " · 빛 캐시 " + (tm != null ? tm.GetCachedPlaces().Count : -1));
            });
            Sleep(0.4f);
            Check(() =>
            {
                bool cat = Fm() != null && Fm().CategoryState == catWas, pet = PetOn() == petWas, pub = PubOn() == pubWas;
                bool prefs = PlayerPrefs.GetInt(PrefCat, 0) == prefCatWas && PlayerPrefs.GetInt(PrefPet, 0) == prefPetWas && PlayerPrefs.GetInt(PrefPub, 1) == prefPubWas;
                if (!(cat && pet && pub && prefs)) log.Add($"  되돌림 확인 — 분류 {cat} · 애견동반 {pet} · 공공데이터 {pub} · 저장값 {prefs}");
                return cat && pet && pub && prefs;
            }, "분류 · 애견동반 · 공공데이터 · 저장값 처음대로");
            Check(() => TourAPIManager.EditorStayBase == null && tm != null && cacheWas != null && tm.GetCachedPlaces().Count == cacheWas.Count
                        && !ids.Any(id => tm.GetPlaceDataMap().ContainsKey(id) || tm.GetSpawnedObjects().ContainsKey(id) || tm.GetCachedPlaces().Any(c => c.rawId == id))
                        && (Details() == null || !ids.Any(Details().ContainsKey)) && !Rows().Any()
                        && (DoneSet() == null || !DoneSet().Any(k => k.Contains(":kt_l3_"))),
                  "넣은 장소 · 상세 캐시 · 받은 표시 모두 지움 · 빛 캐시 처음대로 · 숙박 주소 원래대로");
        }

        /// <summary>코루틴이 끝나면 알린다 — 안쪽 코루틴을 반사로 꺼내 돌릴 때</summary>
        private static IEnumerator L3Run(MonoBehaviour host, IEnumerator inner, Action done)
        {
            if (inner != null) yield return host.StartCoroutine(inner);
            done();
        }
    }
}
