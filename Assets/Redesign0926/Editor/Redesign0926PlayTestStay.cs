using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 L1 — 대표님 「목록 카테고리에 '펜션 / 호텔' 추가 — '애견동반' 체크했을 때도 문제없이」.
    ///  · stay : 관광공사 자료 둘(숙박 32 '시험 숙소 펜션' · 관광지 12 '시험 관광지')과 사람이 올린 곳 셋('숙소' 애견동반 안 됨 '시험 사용자 호텔' ·
    ///    '숙소' 애견동반 '시험 애견 펜션' · '공원' 애견동반 '시험 애견 공원')을 앱 안에 바로 넣는다 — 서버에 묻지 않는다
    ///    (가짜 번호는 시험 동안 '상세를 묻는 중' · '사진 받기 쉼'으로 막아 둔다) →
    ///    분류 고르기 9줄 · 다섯째 '숙소' 침대 그림 / 고르면 칩 이름 · 침대 그림 · 하늘빛 청록 / 목록은 숙박 셋만(관광공사 숙박 포함 —
    ///    예전엔 분류를 고르면 관광공사 자료가 통째로 빠졌다) · 줄 앞 침대 그림 / 애견동반을 켜면 애견동반 되는 둘만 / AR 의 관광공사 오브젝트도 숙박만 /
    ///    '전체'로 돌아오면 관광지가 다시 (공공데이터 칩대로). 끝나면 넣은 것 · 칩 · 저장값을 모두 되돌린다
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanStay()
        {
            const string PrefCat = "Filter_Category_V1", PrefPet = "Filter_PetFriendly_V3";
            string[] tIds = { "kt_stay_1", "kt_stay_2" };
            string[] tNames = { "시험 숙소 펜션", "시험 관광지" };
            string[] tTypes = { "32", "12" };
            int[] dIds = { 990301, 990302, 990303 };
            string[] dNames = { "시험 사용자 호텔", "시험 애견 펜션", "시험 애견 공원" };
            string[] dCats = { "stay", "stay", "park" };
            bool[] dPet = { false, true, true };
            var catWas = FilterManager.CategoryFilterState.All;
            bool petWas = false;
            int prefCatWas = 0, prefPetWas = 0;
            bool hadPrefCat = false, hadPrefPet = false;
            TourAPIManager tm = null;
            var inFlight = typeof(DataManager).GetField("detailInFlight", BindingFlags.NonPublic | BindingFlags.Instance);
            var rowsFailed = typeof(R0926PlaceRows).GetField("detailFailed", BindingFlags.NonPublic | BindingFlags.Instance);
            var showBy = typeof(TourAPIManager).GetMethod("ShouldShowByFilter", BindingFlags.NonPublic | BindingFlags.Instance);

            FilterManager Fm() => UnityEngine.Object.FindAnyObjectByType<FilterManager>();
            bool PetOn() => Fm() != null && Fm().PetState == FilterManager.PetFriendlyFilterState.OnlyPetFriendly;
            bool MenuOpen() { var m = UnityEngine.Object.FindAnyObjectByType<R0926CategoryMenu>(); return m != null && m.IsOpen; }
            List<PlaceListManager.ListEntry> Places()
            {
                var p = Plm();
                return p == null ? new List<PlaceListManager.ListEntry>() : p.ShownEntries.Where(e => e.kind == PlaceListManager.EntryKind.Place).ToList();
            }
            bool Has(string name) => Places().Any(e => e.name == name);
            string TourCat(string id)
            {
                if (tm == null) return "tour";
                if (tm.GetPlaceDataMap().TryGetValue(id, out var p)) return TourAPIManager.CategoryOf(p);
                var c = tm.GetCachedPlaces().Find(x => x.rawId == id);
                return c != null ? c.category : "tour";
            }
            bool TourVisible(string id)
            {
                if (tm == null) return false;
                bool full = tm.GetSpawnedObjects().TryGetValue(id, out var go) && go != null && go.activeSelf;
                bool ind = tm.GetIndicatorOnlyObjects().TryGetValue(id, out var io) && io != null && io.activeSelf;
                return full || ind;
            }
            bool ShowsCat(string cat) => tm != null && showBy != null && (bool)showBy.Invoke(tm, new object[] { cat });
            string Names() => string.Join(" · ", Places().Where(e => e.name.StartsWith("시험 ")).Select(e => e.name + "(" + e.icon + ")"));
            Action refresh = () => { Plm()?.UpdateUI(); };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Wait(() => DataManager.Instance != null && DataManager.Instance.GetSpawnedObjects().Count > 0, 40f, "주변 장소가 AR 에 뜸");
            // 관광공사 자료를 다 받은 뒤에 넣는다 — 받는 중에 넣으면 받은 목록이 넣은 것을 지운다.
            // 반경을 넓혀 가며 여러 번 받는다(loadRadii) — 첫 반경만 받고 캐시 준비가 켜지므로 남은 반경까지 끝나길 기다린다 (첫 실행 땐 둘째 반경 응답이 넣은 것을 지웠다)
            Wait(() => { tm = UnityEngine.Object.FindAnyObjectByType<TourAPIManager>(); return tm != null && tm.IsCacheReady; }, 30f, "관광공사 자료 받음");
            Wait(() => tm == null || (tm.IsDataLoaded() && !TourFetching(tm)), 45f, "관광공사 반경별 받기 모두 끝남");
            Sleep(1.0f);
            Do(() =>
            {
                tm = UnityEngine.Object.FindAnyObjectByType<TourAPIManager>();
                var fm = Fm();
                catWas = fm != null ? fm.CategoryState : FilterManager.CategoryFilterState.All;
                petWas = PetOn();
                hadPrefCat = PlayerPrefs.HasKey(PrefCat); prefCatWas = PlayerPrefs.GetInt(PrefCat, 0);
                hadPrefPet = PlayerPrefs.HasKey(PrefPet); prefPetWas = PlayerPrefs.GetInt(PrefPet, 0);
                if (fm != null && catWas != FilterManager.CategoryFilterState.All) fm.SetCategory(FilterManager.CategoryFilterState.All);
                if (petWas) ClickToggle("PetFriendlyToggle");

                // 실제로 받은 관광공사 자료에 관광 타입이 들어오는지 (값만 세고 이름 · 자리는 적지 않는다)
                if (tm != null)
                {
                    var real = tm.GetPlaceDataMap().Values.ToList();
                    log.Add($"  받은 관광공사 자료 {real.Count}곳 — 관광 타입 값 있음 {real.Count(p => !string.IsNullOrEmpty(p.contenttypeid))} · 숙박(32) {real.Count(p => TourAPIManager.CategoryOf(p) == "stay")} · 빛 캐시 숙박 {tm.GetCachedPlaces().Count(c => c.category == "stay")}");
                }

                double la = VirtualLocation.Instance.Latitude, lo = VirtualLocation.Instance.Longitude;
                // 관광공사 자료 — 받아 온 것과 같은 자리(장소 지도 · 빛 캐시)에. 색 · 분류는 TourAPIManager 가 받을 때 쓰는 함수 그대로
                if (tm != null)
                    for (int i = 0; i < tIds.Length; i++)
                    {
                        var t = new TourPlaceData { contentid = tIds[i], contenttypeid = tTypes[i], title = tNames[i], mapy = la + 0.0011 + i * 0.0003, mapx = lo + 0.0006 };
                        t.color = TourAPIManager.ColorHexOf(TourAPIManager.CategoryOf(t));
                        tm.GetPlaceDataMap()[t.contentid] = t;
                        tm.GetCachedPlaces().RemoveAll(c => c.rawId == t.contentid);
                        tm.GetCachedPlaces().Add(new CachedPlaceData
                        {
                            uniqueId = "tour_" + t.contentid, rawId = t.contentid, displayName = t.title, latitude = t.mapy, longitude = t.mapx,
                            category = TourAPIManager.CategoryOf(t), sourceManager = "TourAPI", modelType = "cube", petFriendly = true, filterKey = "publicData",
                        });
                    }
                // 사람이 올린 곳 — 빛 캐시에만 (상세 없음). 가짜 번호는 상세를 묻지 않게 '묻는 중' · 목록 사진도 '받기 쉼'으로
                var dm = DataManager.Instance;
                var pr = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>(FindObjectsInactive.Include);
                var fly = dm != null && inFlight != null ? inFlight.GetValue(dm) as HashSet<int> : null;
                var failed = pr != null && rowsFailed != null ? rowsFailed.GetValue(pr) as Dictionary<int, float> : null;
                for (int i = 0; i < dIds.Length; i++)
                {
                    fly?.Add(dIds[i]);
                    if (failed != null) failed[dIds[i]] = Time.unscaledTime + 100000f;
                    if (dm == null) continue;
                    string raw = dIds[i].ToString();
                    dm.GetLightCache().RemoveAll(c => c.rawId == raw);
                    dm.GetLightCache().Add(new CachedPlaceData
                    {
                        uniqueId = "dm_" + raw, rawId = raw, displayName = dNames[i], latitude = la - 0.0012 - i * 0.0003, longitude = lo - 0.0005,
                        category = dCats[i], sourceManager = "DataManager", modelType = "cube", petFriendly = dPet[i], filterKey = "",
                    });
                }
                log.Add("  넣음: 관광공사 " + tIds.Length + " · 사람이 올린 곳 " + dIds.Length + (fly == null ? " (묻는 중 막기 실패)" : "") + (failed == null ? " (사진 쉼 실패)" : ""));
            });
            Check(() => TourAPIManager.CategoryOf(new TourPlaceData { contenttypeid = "32" }) == "stay" && TourAPIManager.CategoryOf(new TourPlaceData { contenttypeid = "12" }) == "tour"
                        && TourAPIManager.CategoryOf(new TourPlaceData()) == "tour",
                  "관광공사 숙박(32)은 '숙소' · 관광지(12) · 타입 없음은 관광");
            Check(() => TourAPIManager.ColorHexOf("stay") == ColorUtility.ToHtmlStringRGB(DataManager.GetCategoryColor("stay")) && TourAPIManager.ColorHexOf("tour") == "FBC15D"
                        && ColorUtility.ToHtmlStringRGB(DataManager.GetCategoryColor("stay")) == "1FC7D4" && DataManager.IsChipCategory("stay"),
                  "숙박 색 = '숙소' 칩 색 #1FC7D4 (관광은 예전 노랑) · 칩 분류라 예전 색보다 칩 색이 먼저");
            Check(() => DataManager.ResolvePlaceColorHex("FBC15D", "stay", "x") == "1FC7D4", "서버에 다른 색이 남아 있어도 '숙소'는 칩 색");

            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(refresh);
            Sleep(0.4f);
            Do(() => log.Add("  전체 · 시험 줄: " + Names()));
            Check(() => Has(tNames[0]) == ChipOn("StoresToggle") && Has(tNames[1]) == ChipOn("StoresToggle") && Has(dNames[0]) && Has(dNames[1]) && Has(dNames[2]),
                  "'전체'에선 넣은 다섯 곳이 다 보임 (관광공사 자료는 공공데이터 칩대로)");

            // 분류 고르기 — 9줄 · 다섯째 '숙소' 침대 그림
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.4f);
            Shot("stay_menu");
            Check(() => MenuOpen() && MenuRows() == 9, "분류 고르기 목록 9줄");
            Do(() => log.Add("  분류 순서: " + MenuOrder()));
            Check(() =>
            {
                var names = MenuOrder().Split('·');
                return names.Length == 9 && (names[4] == "숙소" || names[4] == "Stays") && (names[3] == "샵" || names[3] == "Shop") && (names[5] == "공원" || names[5] == "Park");
            }, "다섯째 줄 '숙소' — 샵 다음 · 공원 앞 (올리기 분류와 같은 자리)");
            Check(() => MenuIconsOk(), "줄 앞 그림 — '숙소'는 침대 그림 (r0926_c_stay)");
            Do(() => PickCategory(4));
            Sleep(0.5f);
            Do(refresh);
            Sleep(0.4f);
            Shot("stay_list");
            Check(() => Fm().CategoryState == FilterManager.CategoryFilterState.Stay && !MenuOpen(), "다섯째 줄을 누르면 '숙소' · 목록 닫힘");
            Check(() => FilterManager.ActiveCategoryNow == "stay" && Fm().GetActiveCategoryFilter() == "stay", "고른 분류 값 = stay");
            Check(() => (ChipLabel("CategoryToggle") == "숙소" || ChipLabel("CategoryToggle") == "Stays") && ChipIcon("CategoryToggle") == "r0926_c_stay",
                  "칩 이름 '숙소' · 침대 그림");
            Check(() => ChipTint("CategoryToggle", DataManager.GetCategoryColor("stay")), "칩 바탕은 '숙소' 색");
            Check(() =>
            {
                var c = Chip("CategoryToggle");
                var t = c != null ? c.Find("Label")?.GetComponent<Text>() : null;
                if (t == null) return false;
                log.Add($"  칩 글자 크기 {t.fontSize} · 글자 폭 {Mathf.RoundToInt(t.preferredWidth)} · 칩 폭 {Mathf.RoundToInt(c.rect.width)}");
                return t.fontSize >= 38 && ChipTextsFit();
            }, "칩 글자가 칸 안 — 앱 최소 글자(38) 이상 · 다섯 나라 말 모두");
            Do(() => log.Add("  숙소 · 시험 줄: " + Names() + " · 장소 줄 " + Places().Count));
            Check(() => Has(tNames[0]) && Has(dNames[0]) && Has(dNames[1]), "목록에 숙박 셋 — 관광공사 숙박 · 사람이 올린 호텔 · 애견 펜션 (예전엔 분류를 고르면 관광공사 자료가 통째로 빠졌다)");
            Check(() => !Has(tNames[1]) && !Has(dNames[2]), "관광지 · 공원은 빠짐");
            Check(() => Places().Count > 0 && Places().All(e => e.icon == "stay"), "남은 장소는 모두 '숙소' (줄 그림 이름 stay)");
            Check(() => PlaceRowIconsOk(), "장소 줄 앞은 침대 그림 (분류 칩과 같은 그림)");
            Check(() => ShowsCat("stay") && !ShowsCat("tour"), "AR 관광공사 오브젝트 — 숙박만 보이고 관광지는 숨김");
            Sleep(2.5f);   // 분류를 바꾸면 바로 다시 배분한다 — 만들기는 한 프레임에 하나씩이라 잠깐 기다린다
            Do(() => log.Add($"  AR 관광공사 — 숙소 펜션 {(TourVisible(tIds[0]) ? "보임" : "안 보임")} · 관광지 {(TourVisible(tIds[1]) ? "보임" : "안 보임")}"));
            Do(() => { if (!TourVisible(tIds[0]) || TourVisible(tIds[1])) log.Add("  " + StayDiag(tm, tIds[0])); });
            Check(() => TourVisible(tIds[0]) && !TourVisible(tIds[1]), "AR 에서도 관광공사 숙소는 보이고(3D 또는 인디케이터) 관광지는 안 보임");
            Check(() =>
            {
                if (tm == null) return false;
                foreach (var kv in tm.GetSpawnedObjects()) if (kv.Value != null && kv.Value.activeSelf && TourCat(kv.Key) != "stay") { log.Add("  숙박 아닌 관광 3D 가 보임"); return false; }
                foreach (var kv in tm.GetIndicatorOnlyObjects()) if (kv.Value != null && kv.Value.activeSelf && TourCat(kv.Key) != "stay") { log.Add("  숙박 아닌 관광 인디케이터가 보임"); return false; }
                return true;
            }, "보이는 관광공사 오브젝트는 모두 숙박");

            // 애견동반 켜기 — 애견동반 되는 숙소만
            Do(() => { if (!PetOn()) ClickToggle("PetFriendlyToggle"); });
            Sleep(0.5f);
            Do(refresh);
            Sleep(0.4f);
            Shot("stay_pet");
            Do(() => log.Add("  숙소 + 애견동반 · 시험 줄: " + Names()));
            Check(() => PetOn() && Fm().CategoryState == FilterManager.CategoryFilterState.Stay, "애견동반을 켜도 분류는 '숙소' 그대로");
            Check(() => Has(tNames[0]) && Has(dNames[1]) && !Has(dNames[0]), "애견동반 숙소만 — 관광공사 숙박(모두 애견동반) · 애견 펜션은 남고 애견동반 안 되는 호텔은 빠짐");
            Check(() => !Has(tNames[1]) && !Has(dNames[2]) && Places().All(e => e.icon == "stay" && e.pet), "관광지 · 공원은 여전히 빠짐 · 남은 곳은 모두 애견동반 숙소");
            Check(() => ShowsCat("stay") && !ShowsCat("tour"), "AR 도 애견동반을 켠 채 숙박만");
            Sleep(2.5f);
            Do(() => { if (!TourVisible(tIds[0]) || TourVisible(tIds[1])) log.Add("  " + StayDiag(tm, tIds[0])); });
            Check(() => TourVisible(tIds[0]) && !TourVisible(tIds[1]), "애견동반을 켠 채 AR 에서도 관광공사 숙소만");

            // '전체'로 — 관광지가 다시 (공공데이터 칩대로)
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.3f);
            Do(() => PickCategory(0));
            Sleep(0.5f);
            Do(refresh);
            Sleep(0.4f);
            Do(() => log.Add("  전체 + 애견동반 · 시험 줄: " + Names()));
            Check(() => Fm().CategoryState == FilterManager.CategoryFilterState.All && ChipIcon("CategoryToggle") == "r0926_c_all", "'전체'로 돌아옴 · 네 칸 그림");
            Check(() => Has(tNames[1]) == ChipOn("StoresToggle") && Has(dNames[2]) && !Has(dNames[0]), "'전체'에선 관광지가 다시 (「공공데이터」 칩대로 · 11-27) · 애견동반은 그대로");
            Check(() => ShowsCat("tour") == ChipOn("StoresToggle") && ShowsCat("stay") == ChipOn("StoresToggle"), "AR 관광공사 오브젝트도 다시 「공공데이터」 칩대로 (11-27)");

            // 되돌리기 — 넣은 것 · 막아 둔 번호 · 칩 · 저장값
            Do(() =>
            {
                if (PetOn() != petWas) ClickToggle("PetFriendlyToggle");
                Fm()?.SetCategory(catWas);
                if (tm != null)
                    foreach (var id in tIds)
                    {
                        tm.DespawnFullObject(id);
                        tm.DespawnIndicatorOnly(id);
                        tm.GetPlaceDataMap().Remove(id);
                        tm.GetCachedPlaces().RemoveAll(c => c.rawId == id);
                    }
                var dm = DataManager.Instance;
                var pr = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>(FindObjectsInactive.Include);
                var fly = dm != null && inFlight != null ? inFlight.GetValue(dm) as HashSet<int> : null;
                var failed = pr != null && rowsFailed != null ? rowsFailed.GetValue(pr) as Dictionary<int, float> : null;
                foreach (int id in dIds)
                {
                    string raw = id.ToString();
                    if (dm != null)
                    {
                        dm.DespawnFullObject(raw);
                        dm.DespawnIndicatorOnly(raw);
                        dm.GetLightCache().RemoveAll(c => c.rawId == raw);
                    }
                    fly?.Remove(id);
                    failed?.Remove(id);
                }
                if (hadPrefCat) PlayerPrefs.SetInt(PrefCat, prefCatWas); else PlayerPrefs.DeleteKey(PrefCat);
                if (hadPrefPet) PlayerPrefs.SetInt(PrefPet, prefPetWas); else PlayerPrefs.DeleteKey(PrefPet);
                PlayerPrefs.Save();
                Plm()?.UpdateUI();
                log.Add("  되돌림: 분류 " + Fm()?.CategoryState + " · 애견동반 " + (PetOn() ? "켜짐" : "꺼짐"));
            });
            Check(() => Fm() != null && Fm().CategoryState == catWas && PetOn() == petWas && PlayerPrefs.GetInt(PrefCat, 0) == prefCatWas && PlayerPrefs.GetInt(PrefPet, 0) == prefPetWas,
                  "분류 · 애견동반 · 저장값 처음대로");
            Check(() => !Places().Any(e => e.name.StartsWith("시험 ")) && (tm == null || !tm.GetPlaceDataMap().ContainsKey(tIds[0])), "넣은 장소는 모두 지움");
        }

        /// <summary>관광공사 자료를 받는 중인지 — TourAPIManager.RefreshCache 의 '같은 자리를 받는 중' 판단과 같다 (시작 시각 · 180초)</summary>
        private static bool TourFetching(TourAPIManager tm)
        {
            var f = typeof(TourAPIManager).GetField("progressiveFetchStartedAt", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            float startedAt = (float)f.GetValue(tm);
            return startedAt > 0f && Time.realtimeSinceStartup - startedAt < 180f;
        }

        /// <summary>AR 에 안 뜰 때 어디서 막혔는지 — 배분 대상 · 배분 · 만들어짐 · 켜짐 (참·거짓과 개수만, 자리 · 거리는 적지 않는다)</summary>
        private static string StayDiag(TourAPIManager tm, string rawId)
        {
            var fm = UnityEngine.Object.FindAnyObjectByType<FilterManager>();
            if (fm == null || tm == null) return "진단 — 필터 · 관광공사 매니저 없음";
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            var provs = typeof(FilterManager).GetField("cacheProviders", F)?.GetValue(fm) as System.Collections.IList;
            var ind = typeof(FilterManager).GetField("currentIndicatorAllocations", F)?.GetValue(fm) as HashSet<string>;
            var full = typeof(FilterManager).GetField("currentFullAllocations", F)?.GetValue(fm) as HashSet<string>;
            string u = "tour_" + rawId;
            bool hasGo = tm.GetSpawnedObjects().TryGetValue(rawId, out var go) && go != null;
            bool hasInd = tm.GetIndicatorOnlyObjects().TryGetValue(rawId, out var io) && io != null;
            int tourFull = full?.Count(x => x.StartsWith("tour_")) ?? -1, tourInd = ind?.Count(x => x.StartsWith("tour_")) ?? -1;
            return $"진단 — 배분 대상에 관광공사 {provs != null && provs.Contains(tm)} · 캐시 준비 {tm.IsCacheReady} · 캐시 {tm.GetCachedPlaces().Count}곳"
                 + $" · 3D 배분 {full?.Contains(u)} · 화살표 배분 {ind?.Contains(u)} · 배분 전체 3D {full?.Count} 화살표 {ind?.Count} (관광공사 {tourFull}·{tourInd})"
                 + $" · 3D 있음 {hasGo} 켜짐 {(hasGo && go.activeSelf)} · 화살표 있음 {hasInd} 켜짐 {(hasInd && io.activeSelf)}"
                 + $" · 견본 {tm.samplePrefab != null} · 화살표 견본 {typeof(TourAPIManager).GetField("indicatorOnlyPrefab", F | BindingFlags.Public)?.GetValue(tm) != null}"
                 + $" · 만드는 중 {fm.Dripping} · 시작화면 {BootOverlay.Showing}";
        }
    }
}
