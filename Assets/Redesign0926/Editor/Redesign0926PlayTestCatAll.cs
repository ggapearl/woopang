using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-22 대표님 「원래대로 '전체'보다 많을 수 있도록 … '공공데이터' 를 키지 않아도 해당 분류를 눌렀을 때 관련한 내용이 모두 발생하도록」 —
    /// 분류를 고르면 「공공기관」 · 「공공데이터」 칩과 상관없이 그 분류 자료가 모두 나온다 (칩을 꺼 둔 채면 분류 쪽이 '전체'보다 많을 수 있다 · 11-21 S7 에 좁혔다가 되돌림).
    ///  · catall : 앱 안에 바로 넣는다 (서버에 묻지 않는다 — 가짜 번호는 시험 동안 '상세를 묻는 중' · '사진 받기 쉼'으로 막는다)
    ///    우팡 DB 의 기관 자료(랜드마크 3 · 화장실 1) · 사람이 올린 곳(랜드마크 1 · 음식점 1) · 관광공사(숙박 1 · 관광지 1) · 근처 가게(음식점 2 · 샵 1 · 숙소 1)
    ///    ① 두 칩을 끄고 '전체' · 분류 8개 — '전체'는 사람이 올린 둘만, 분류마다 그 분류 자료가 모두 (목록 · AR 배분 자격 · 매니저 보이기) · '랜드마크'는 '전체'보다 기관 자료 셋이 많다
    ///    ② 칩을 켜면 — '전체'에 12곳 모두 · 분류마다 그 분류 자료 그대로 · 분류는 '전체'의 부분
    ///    ③ 분류를 고른 동안 두 칩은 회색 체크로 잠겨 누를 수 없다 (교통 칩도) · '전체'로 돌아오면 고르기 전 값 · 저장값 그대로
    ///    ④ 칩 하나씩 (11-26 대표님 9번) — 「공공데이터」만: 기관 랜드마크 · 화장실 · 가게 (관광공사 · 시청 없음) · 「공공기관」만: 관광공사 · 시청 (기관 랜드마크 · 화장실 · 가게 없음)
    ///    끝나면 넣은 것 · 칩 · 분류 · 저장값을 되돌린다 (로그엔 개수만 — 이름 · 자리는 적지 않는다)
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanCatAll()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            var footerF = typeof(PlaceListManager).GetField("cachedFooter", NP);
            var combinedF = typeof(PlaceListManager).GetField("combinedPlaces", NP);   // 지금 목록 줄 (빈 목록이면 다시 묻는 동안에도 비어 있다 — 보이는 줄은 지난 것이 남음)
            var passM = typeof(FilterManager).GetMethod("IsPassingFilter", NP);
            var dmShowM = typeof(DataManager).GetMethod("ShouldShowObject", NP);
            var tourShowM = typeof(TourAPIManager).GetMethod("ShouldShowByFilter", NP);
            var storeShowM = typeof(StoreManager).GetMethod("ShouldShow", NP, null, new[] { typeof(string) }, null);
            var pubF = typeof(FilterManager).GetField("filterPublicData", NP);
            var storesF = typeof(FilterManager).GetField("filterStores", NP);
            var inFlight = typeof(DataManager).GetField("detailInFlight", NP);
            var rowsFailed = typeof(R0926PlaceRows).GetField("detailFailed", NP);
            var fullF = typeof(FilterManager).GetField("currentFullAllocations", NP);        // AR 에 3D 로 둔 곳
            var indF = typeof(FilterManager).GetField("currentIndicatorAllocations", NP);    // AR 에 화살표 · 박스로 둔 곳
            var provF = typeof(FilterManager).GetField("cacheProviders", NP);
            const string PrefPub = "Filter_PublicData_V2", PrefStores = "Filter_Stores_V1";

            int[] dIds = { 990411, 990412, 990413, 990414, 990415, 990416 };
            string[] dCats = { "landmark", "landmark", "landmark", "toilet", "landmark", "food" };
            bool[] dPub = { true, true, true, true, false, false };   // 기관이 올린 곳 · 사람이 올린 곳
            string[] tIds = { "kt_cs_1", "kt_cs_2" };
            string[] tTypes = { "32", "12" };   // 숙박 · 관광지
            string[] sIds = { "st_cs1", "st_cs2", "st_cs3", "st_cs4" };
            string[] sCats = { "food", "food", "shop", "stay" };
            var mine = new HashSet<string>(dIds.Select(i => i.ToString()).Concat(tIds).Concat(sIds));
            const string People = "990415,990416";   // 사람이 올린 둘 — 두 칩을 끈 '전체'엔 이 둘만
            var cats = new[]
            {
                FilterManager.CategoryFilterState.Shop, FilterManager.CategoryFilterState.Food, FilterManager.CategoryFilterState.Cafe,
                FilterManager.CategoryFilterState.Park, FilterManager.CategoryFilterState.Toilet, FilterManager.CategoryFilterState.Sport,
                FilterManager.CategoryFilterState.Landmark, FilterManager.CategoryFilterState.Stay,
            };
            // 분류마다 나와야 할 곳 (넣은 것 중) — 칩을 켜든 끄든 같다
            var want = new Dictionary<FilterManager.CategoryFilterState, string>
            {
                { FilterManager.CategoryFilterState.Shop, "st_cs3" }, { FilterManager.CategoryFilterState.Food, "990416,st_cs1,st_cs2" },
                { FilterManager.CategoryFilterState.Cafe, "" }, { FilterManager.CategoryFilterState.Park, "" },
                { FilterManager.CategoryFilterState.Toilet, "990414" }, { FilterManager.CategoryFilterState.Sport, "" },
                { FilterManager.CategoryFilterState.Landmark, "990411,990412,990413,990415" }, { FilterManager.CategoryFilterState.Stay, "kt_cs_1,st_cs4" },
            };

            var catWas = FilterManager.CategoryFilterState.All;
            bool petWas = false, pubWas = true, storesWas = true, pressed = true;
            DataManager dm = null;
            TourAPIManager tm = null;
            StoreManager stm = null;
            var snaps = new Dictionary<string, Snap>();
            var bad = new List<string>();

            FilterManager Fm() => UnityEngine.Object.FindAnyObjectByType<FilterManager>();
            bool PetOn() => Fm() != null && Fm().PetState == FilterManager.PetFriendlyFilterState.OnlyPetFriendly;
            bool PubOn() => Fm() != null && pubF?.GetValue(Fm()) is bool b && b;
            bool StoresOn() => Fm() != null && storesF?.GetValue(Fm()) is bool b && b;
            Toggle ChipT(string name)
            {
                foreach (var t in UnityEngine.Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include))
                    if (t.name == name && t.transform.parent != null && t.transform.parent.name == "FilterButtonPanel") return t;
                return null;
            }
            // 사람이 누르듯 — 잠긴 칩은 누르지 못한다
            bool Press(string name)
            {
                var t = ChipT(name);
                if (t == null || !t.interactable) { log.Add("  " + name + " 칩을 누를 수 없음 (" + (t == null ? "없음" : "잠김") + ")"); return false; }
                t.isOn = !t.isOn;
                return true;
            }
            string Ids(IEnumerable<string> s) => string.Join(",", s.OrderBy(x => x, StringComparer.Ordinal));
            string CatName(FilterManager.CategoryFilterState c) => c == FilterManager.CategoryFilterState.All ? "전체" : c.ToString();
            void Refresh() { Fm()?.TriggerReallocation(); Plm()?.UpdateUI(); }
            string Chip(Toggle t) => t == null ? "없음" : (t.isOn ? "체크" : "빈칸") + (t.interactable ? " · 누를 수 있음" : " · 잠김");

            IEnumerable<CachedPlaceData> Cached()
            {
                if (dm != null) foreach (var c in dm.GetLightCache()) if (mine.Contains(c.rawId)) yield return c;
                if (tm != null) foreach (var c in tm.GetCachedPlaces()) if (mine.Contains(c.rawId)) yield return c;
                if (stm != null) foreach (var c in stm.GetCachedPlaces()) if (mine.Contains(c.rawId)) yield return c;
            }
            Snap Take()
            {
                var s = new Snap();
                var p = Plm();
                if (p != null)
                {
                    if (combinedF?.GetValue(p) is System.Collections.IEnumerable rows)
                        foreach (var o in rows)
                        {
                            if (!(o is System.Runtime.CompilerServices.ITuple t) || t.Length < 3 || !(t[2] is string id)) continue;
                            s.rows++;
                            if (mine.Contains(id)) s.list.Add(id);
                        }
                    string f = footerF != null ? footerF.GetValue(p) as string ?? "" : "";
                    foreach (var line in f.Split('\n'))
                    {
                        int k = line.LastIndexOf(':');
                        if (k > 0 && int.TryParse(line.Substring(k + 1).Trim(), out int n)) s.foot[line.Substring(0, k).Trim()] = n;
                    }
                }
                var fm = Fm();
                if (fm != null && passM != null)
                {
                    var filters = fm.GetActiveFilters();
                    foreach (var c in Cached())
                    {
                        s.cached++;
                        if ((bool)passM.Invoke(fm, new object[] { c, filters })) s.pass.Add(c.rawId);
                    }
                    // 받은 실제 자료까지 — 지금 칩 · 분류로 AR 에 뜰 자격이 있는 곳 · 기관 자료인 곳
                    if (provF?.GetValue(fm) is List<IPlaceCacheProvider> provs)
                        foreach (var pv in provs)
                        {
                            if (pv == null || !pv.IsCacheReady) continue;
                            foreach (var c in pv.GetCachedPlaces())
                            {
                                if (c.filterKey == "publicData") s.pub.Add(c.uniqueId);
                                if (!HiddenPlaces.IsHidden(c.uniqueId) && (bool)passM.Invoke(fm, new object[] { c, filters })) s.elig.Add(c.uniqueId);
                            }
                        }
                    if (fullF?.GetValue(fm) is HashSet<string> full) s.ar.UnionWith(full);
                    if (indF?.GetValue(fm) is HashSet<string> ind) s.ar.UnionWith(ind);
                }
                for (int i = 0; i < dIds.Length; i++)
                    if (dm != null && dmShowM != null && (bool)dmShowM.Invoke(dm, new object[] { new PlaceData { id = dIds[i], category = dCats[i], is_public = dPub[i], model_type = "cube" } }))
                        s.show.Add(dIds[i].ToString());
                for (int i = 0; i < tIds.Length; i++)
                    if (tm != null && tourShowM != null && (bool)tourShowM.Invoke(tm, new object[] { TourAPIManager.CategoryOf(new TourPlaceData { contenttypeid = tTypes[i] }) }))
                        s.show.Add(tIds[i]);
                for (int i = 0; i < sIds.Length; i++)
                    if (stm != null && storeShowM != null && (bool)storeShowM.Invoke(stm, new object[] { sCats[i] }))
                        s.show.Add(sIds[i]);
                return s;
            }
            string Foot(Snap s) => string.Join(" · ", s.foot.Select(kv => kv.Key + " " + kv.Value));
            void Record(string tag, FilterManager.CategoryFilterState c)
            {
                var s = Take();
                snaps[tag + CatName(c)] = s;
                log.Add($"  [{tag}] {CatName(c)} — 목록 줄 {s.rows} · AR {s.ar.Count}곳(기관 자료 {s.ar.Count(s.pub.Contains)}) · 넣은 곳 {s.cached} 중 목록 {s.list.Count} · 배분 자격 {s.pass.Count} · 매니저 보이기 {s.show.Count} · 아래 줄 {Foot(s)}");
            }
            // 분류마다 그 분류 자료가 모두 (목록 · 배분 자격 · 매니저 보이기 — 넣은 곳 기준)
            void Exact(string tag)
            {
                foreach (var c in cats)
                {
                    var s = snaps[tag + CatName(c)];
                    if (Ids(s.list) != want[c] || Ids(s.pass) != want[c] || Ids(s.show) != want[c])
                        bad.Add($"{tag} {CatName(c)}: 목록 {Ids(s.list)} · 배분 {Ids(s.pass)} · 매니저 {Ids(s.show)} (바라는 것 {want[c]})");
                }
            }
            // 분류 쪽에만 있는 곳 ('전체'에 없는 곳) — 목록 · 배분 · 매니저(넣은 곳)와 AR · 아래 줄 출처별 수(받은 실제 자료 포함)
            List<string> Beyond(string tag, FilterManager.CategoryFilterState c)
            {
                var all = snaps[tag + "전체"];
                var s = snaps[tag + CatName(c)];
                var extra = new List<string>();
                int l = s.list.Count(x => !all.list.Contains(x)), pa = s.pass.Count(x => !all.pass.Contains(x)), sh = s.show.Count(x => !all.show.Contains(x));
                if (l > 0) extra.Add("목록 " + l);
                if (pa > 0) extra.Add("배분 " + pa);
                if (sh > 0) extra.Add("매니저 " + sh);
                int ar = s.ar.Count(x => !all.elig.Contains(x));
                if (ar > 0) extra.Add($"AR {ar}곳(기관 자료 {s.ar.Count(x => !all.elig.Contains(x) && s.pub.Contains(x))})");
                foreach (var kv in s.foot)
                    if (!all.foot.TryGetValue(kv.Key, out int n0) || kv.Value > n0) extra.Add($"아래 줄 {kv.Key} {kv.Value} > 전체 {(all.foot.TryGetValue(kv.Key, out int z) ? z : 0)}");
                return extra;
            }

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Wait(() => DataManager.Instance != null && DataManager.Instance.GetSpawnedObjects().Count > 0, 40f, "주변 장소가 AR 에 뜸");
            // 관광공사 자료를 다 받은 뒤에 넣는다 — 받는 중에 넣으면 받은 목록이 넣은 것을 지운다 (숙소 시험과 같음)
            Wait(() => { tm = UnityEngine.Object.FindAnyObjectByType<TourAPIManager>(); return tm != null && tm.IsCacheReady; }, 30f, "관광공사 자료 받음");
            Wait(() => tm == null || (tm.IsDataLoaded() && !TourFetching(tm)), 45f, "관광공사 반경별 받기 모두 끝남");
            Wait(() => { stm = StoreManager.Instance; return stm != null && stm.IsCacheReady && !stm.EditorFetching; }, 30f, "가게 매니저 준비됨 (운영 서버엔 가게 길이 없어 빈 목록으로 쉼)");
            Sleep(1.0f);
            Do(() =>
            {
                dm = DataManager.Instance;
                var fm = Fm();
                catWas = fm != null ? fm.CategoryState : FilterManager.CategoryFilterState.All;
                petWas = PetOn();
                pubWas = PubOn();
                storesWas = StoresOn();
                StashStoresPrefs();   // 칩 · 분류 · 애견동반 저장값 — 시험이 끊겨도 러너(StoresFinish)가 되돌린다
                if (fm != null && catWas != FilterManager.CategoryFilterState.All) fm.SetCategory(FilterManager.CategoryFilterState.All);
                if (petWas) ClickToggle("PetFriendlyToggle");

                double la = VirtualLocation.Instance.Latitude, lo = VirtualLocation.Instance.Longitude;
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
                        uniqueId = "dm_" + raw, rawId = raw, displayName = "시험 분류 " + (dPub[i] ? "기관 " : "사람 ") + dCats[i] + " " + i,
                        latitude = la - 0.0009 - i * 0.0002, longitude = lo + 0.0004, category = dCats[i], sourceManager = "DataManager", modelType = "cube",
                        petFriendly = false, filterKey = FilterManager.IsPublicSource(dPub[i], dCats[i]) ? "publicData" : "",
                    });
                }
                if (tm != null)
                    for (int i = 0; i < tIds.Length; i++)
                    {
                        var t = new TourPlaceData { contentid = tIds[i], contenttypeid = tTypes[i], title = "시험 분류 관광공사 " + i, mapy = la + 0.0009 + i * 0.0002, mapx = lo + 0.0004 };
                        t.color = TourAPIManager.ColorHexOf(TourAPIManager.CategoryOf(t));
                        tm.GetPlaceDataMap()[t.contentid] = t;
                        tm.GetCachedPlaces().RemoveAll(c => c.rawId == t.contentid);
                        tm.GetCachedPlaces().Add(new CachedPlaceData
                        {
                            uniqueId = "tour_" + t.contentid, rawId = t.contentid, displayName = t.title, latitude = t.mapy, longitude = t.mapx,
                            category = TourAPIManager.CategoryOf(t), sourceManager = "TourAPI", modelType = "cube", petFriendly = true, filterKey = "publicData",
                        });
                    }
                if (stm != null)
                    for (int i = 0; i < sIds.Length; i++)
                    {
                        var s = new StoreData { id = sIds[i], name = "시험 분류 가게 " + i, category = sCats[i], lat = la + 0.0004, lon = lo - 0.0009 - i * 0.0002, source = "test" };
                        stm.GetPlaceDataMap()[s.id] = s;
                        stm.GetCachedPlaces().RemoveAll(c => c.rawId == s.id);
                        stm.GetCachedPlaces().Add(new CachedPlaceData
                        {
                            uniqueId = StoreManager.Prefix + s.id, rawId = s.id, displayName = s.name, latitude = s.lat, longitude = s.lon,
                            category = s.category, sourceManager = "Store", modelType = "cube", petFriendly = false, filterKey = stm.FilterKey,
                        });
                    }
                log.Add($"  넣음: 우팡 DB {dIds.Length}(기관 {dPub.Count(x => x)}) · 관광공사 {tIds.Length} · 가게 {sIds.Length}" + (fly == null ? " (묻는 중 막기 실패)" : "") + (failed == null ? " (사진 쉼 실패)" : ""));
                log.Add($"  처음 칩 — 공공기관 {(pubWas ? "켜짐" : "꺼짐")} · 공공데이터 {(storesWas ? "켜짐" : "꺼짐")} · 분류 {CatName(catWas)}");
            });
            Check(() => passM != null && dmShowM != null && tourShowM != null && storeShowM != null && footerF != null && combinedF != null && pubF != null && storesF != null
                        && fullF != null && indF != null && provF != null && dm != null && tm != null && stm != null,
                  "시험에 쓸 안쪽 값 · 함수 · 매니저를 모두 찾음");
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);

            // ① 대표님 화면처럼 — 공공기관 · 공공데이터 칩을 끄고 '전체' · 분류 8개
            Do(() => { if (PubOn()) Press("PublicDataToggle"); if (StoresOn()) Press("StoresToggle"); });
            Sleep(0.5f);
            Do(Refresh);
            Sleep(0.4f);
            Check(() => !PubOn() && !StoresOn() && Fm().CategoryState == FilterManager.CategoryFilterState.All, "공공기관 · 공공데이터 칩 끔 · 분류 '전체'");
            Do(() => Record("칩 끔", FilterManager.CategoryFilterState.All));
            Shot("catall_off_all");
            foreach (var c in cats)
            {
                var cc = c;
                Do(() => Fm()?.SetCategory(cc));
                Sleep(0.3f);
                Do(Refresh);
                Sleep(0.3f);
                Do(() => Record("칩 끔", cc));
                if (cc == FilterManager.CategoryFilterState.Landmark) Shot("catall_off_landmark");
            }
            Check(() =>
            {
                var a = snaps["칩 끔전체"];
                return Ids(a.list) == People && Ids(a.pass) == People && Ids(a.show) == People && !a.ar.Any(a.pub.Contains);
            }, "칩 끔 '전체' — 사람이 올린 둘만 (목록 · AR 배분 자격 · 매니저) · AR 에 기관 자료 없음");
            Check(() =>
            {
                bad.Clear();
                Exact("칩 끔");
                foreach (var b in bad) log.Add("  " + b);
                return bad.Count == 0;
            }, "칩을 꺼 둔 채로도 분류마다 그 분류 자료가 모두 — 목록 · AR 배분 자격 · 매니저 (11-22 대표님 「'공공데이터' 를 키지 않아도 해당 분류를 눌렀을 때 관련한 내용이 모두」)");
            Check(() =>
            {
                foreach (var c in cats)
                {
                    var extra = Beyond("칩 끔", c);
                    if (extra.Count > 0) log.Add($"  칩 끔 {CatName(c)} — '전체'보다 많은 곳: " + string.Join(" · ", extra));
                }
                var a = snaps["칩 끔전체"];
                var lm = snaps["칩 끔" + CatName(FilterManager.CategoryFilterState.Landmark)];
                return Ids(lm.list.Where(x => !a.list.Contains(x))) == "990411,990412,990413" && Ids(lm.pass.Where(x => !a.pass.Contains(x))) == "990411,990412,990413";
            }, "칩 끔 — '랜드마크'는 '전체'보다 기관 랜드마크 셋이 많다 (분류가 '전체'보다 많을 수 있다 — 11-22 대표님)");

            // ② 칩을 켜면 — '전체'에 모두 · 분류마다 그 분류 자료 그대로 · 분류는 '전체'의 부분
            Do(() => Fm()?.SetCategory(FilterManager.CategoryFilterState.All));
            Sleep(0.3f);
            Check(() => !PubOn() && !StoresOn() && ChipT("PublicDataToggle") is Toggle p0 && !p0.isOn && p0.interactable,
                  "분류를 풀면 두 칩은 고르기 전 값(끔) · 다시 누를 수 있음");
            Do(() => { if (!PubOn()) Press("PublicDataToggle"); if (!StoresOn()) Press("StoresToggle"); });
            Sleep(0.5f);
            Do(Refresh);
            Sleep(0.4f);
            Do(() => Record("칩 켬", FilterManager.CategoryFilterState.All));
            Check(() => PubOn() && StoresOn() && snaps["칩 켬전체"].list.Count == mine.Count, "칩 켬 — '전체'에 넣은 12곳이 모두");
            foreach (var c in cats)
            {
                var cc = c;
                Do(() => Fm()?.SetCategory(cc));
                Sleep(0.3f);
                Do(Refresh);
                Sleep(0.3f);
                Do(() => Record("칩 켬", cc));
            }
            Check(() =>
            {
                bad.Clear();
                Exact("칩 켬");
                foreach (var c in cats)
                {
                    var extra = Beyond("칩 켬", c);
                    if (extra.Count > 0) bad.Add($"칩 켬 {CatName(c)}: '전체'에 없는 곳 — " + string.Join(" · ", extra));
                }
                foreach (var b in bad) log.Add("  " + b);
                return bad.Count == 0;
            }, "칩 켬 — 분류마다 그 분류의 넣은 곳이 목록 · AR 배분 · 매니저 모두에 그대로 · '전체'의 부분 (아래 줄 출처별 수 · AR 포함)");

            // ③ 분류를 고른 동안 — 두 칩은 회색 체크로 잠김 · 누를 수 없음 · '전체'로 돌아오면 고르기 전 값 (켬 → 켬 · 끔 → 끔)
            Do(() => Fm()?.SetCategory(FilterManager.CategoryFilterState.All));
            Sleep(0.4f);
            Check(() => PubOn() && StoresOn() && ChipT("PublicDataToggle") is Toggle p1 && p1.isOn && p1.interactable && ChipT("StoresToggle") is Toggle s1 && s1.isOn && s1.interactable,
                  "분류를 풀면 두 칩은 고르기 전 값(켬) · 다시 누를 수 있음");
            Do(() => { Press("PublicDataToggle"); Press("StoresToggle"); });
            Sleep(0.5f);
            Do(Refresh);
            Sleep(0.3f);
            Check(() => !PubOn() && !StoresOn() && PlayerPrefs.GetInt(PrefPub, 1) == 0 && PlayerPrefs.GetInt(PrefStores, 1) == 0, "'전체'에서 두 칩 끔 · 저장값 0");
            Do(() => Fm()?.SetCategory(FilterManager.CategoryFilterState.Landmark));
            Sleep(0.4f);
            Do(Refresh);
            Sleep(0.3f);
            Do(() => Record("잠금", FilterManager.CategoryFilterState.Landmark));
            Check(() =>
            {
                Toggle p = ChipT("PublicDataToggle"), s = ChipT("StoresToggle"), m = ChipT("MetroToggle"), t = ChipT("TerminalToggle");
                log.Add($"  랜드마크 — 공공기관 {Chip(p)} · 공공데이터 {Chip(s)} · 지하철 {Chip(m)} · 터미널 / 기차 {Chip(t)} · 맡겨 둔 값 공공기관 {(PubOn() ? "켬" : "끔")} · 공공데이터 {(StoresOn() ? "켬" : "끔")}");
                return p != null && p.isOn && !p.interactable && s != null && s.isOn && !s.interactable && m != null && !m.interactable && t != null && !t.interactable && !PubOn() && !StoresOn();
            }, "분류를 고른 동안 — 공공기관 · 공공데이터 칩은 회색 체크로 잠김 (맡겨 둔 값은 끔 그대로) · 지하철 · 터미널 / 기차 칩도 잠김");
            Check(() =>
            {
                var s = snaps["잠금" + CatName(FilterManager.CategoryFilterState.Landmark)];
                string w = want[FilterManager.CategoryFilterState.Landmark];
                return Ids(s.list) == w && Ids(s.pass) == w && Ids(s.show) == w;
            }, "칩을 꺼 둔 채 '랜드마크' — 기관 랜드마크 셋 · 사람이 올린 하나 모두 (목록 · AR 배분 · 매니저)");
            Do(() => pressed = Press("PublicDataToggle"));
            Sleep(0.3f);
            Check(() => !pressed && !PubOn() && PlayerPrefs.GetInt(PrefPub, 1) == 0, "잠긴 공공기관 칩은 눌리지 않음 · 값 · 저장값 그대로");
            Do(() => Fm()?.SetCategory(FilterManager.CategoryFilterState.All));
            Sleep(0.4f);
            Do(Refresh);
            Sleep(0.3f);
            Do(() => Record("잠금 풂", FilterManager.CategoryFilterState.All));
            Check(() =>
            {
                Toggle p = ChipT("PublicDataToggle"), s = ChipT("StoresToggle");
                log.Add($"  '전체'로 — 공공기관 {Chip(p)} · 공공데이터 {Chip(s)}");
                bool listOff = Plm() != null && !Plm().ShownEntries.Any(e => mine.Contains(e.id) && e.id != "990415" && e.id != "990416");
                return !PubOn() && !StoresOn() && p != null && !p.isOn && p.interactable && s != null && !s.isOn && s.interactable
                       && Ids(snaps["잠금 풂전체"].list) == People && listOff && PlayerPrefs.GetInt(PrefPub, 1) == 0 && PlayerPrefs.GetInt(PrefStores, 1) == 0;
            }, "'전체'로 돌아오면 두 칩은 고르기 전 값(끔) · 다시 누를 수 있음 · 목록엔 사람이 올린 둘만 (기관 자료 · 관광공사 · 가게 없음) · 저장값 0 그대로");

            // ④ 칩 하나씩 — 기관 랜드마크 · 화장실은 「공공데이터」 칩, 관광공사 · 시청은 「공공기관」 칩
            //    (11-26 대표님 9번 「'공공데이터' 를 켜도 발생하지 않는 경우가 … 공공화장실 등도 '공공기관' 이 아닌 '공공데이터'로 포함되서 이 버튼을 키면 해당 관련한 리스트가 발생해야」)
            var govRow = new CachedPlaceData { uniqueId = "dm_990419", rawId = "990419", category = "gov", sourceManager = "DataManager", modelType = "cube", filterKey = "publicData" };
            bool GovPass() => Fm() != null && (bool)passM.Invoke(Fm(), new object[] { govRow, Fm().GetActiveFilters() });
            bool GovShow() => dm != null && (bool)dmShowM.Invoke(dm, new object[] { new PlaceData { id = 990419, category = "gov", is_public = true, model_type = "cube" } });
            int FootN(Snap s, string k) => s.foot.TryGetValue(k, out int n) ? n : 0;
            string[] orgLand = { "dm_990411", "dm_990412", "dm_990413", "dm_990414" };   // 기관 랜드마크 셋 · 화장실 (AR 자격은 uniqueId)
            string dataOnly = Ids(new[] { "990411", "990412", "990413", "990414", "990415", "990416", "st_cs1", "st_cs2", "st_cs3", "st_cs4" });
            string orgOnly = Ids(new[] { "990415", "990416", "kt_cs_1", "kt_cs_2" });
            bool govA = true, govShowA = true, govB = false, govShowB = false;
            Do(() => Press("StoresToggle"));
            Sleep(0.5f);
            Do(Refresh);
            Sleep(0.4f);
            Do(() => { Record("공공데이터만", FilterManager.CategoryFilterState.All); govA = GovPass(); govShowA = GovShow(); });
            Shot("catall_data_only");
            Check(() =>
            {
                var s = snaps["공공데이터만전체"];
                bool elig = orgLand.All(s.elig.Contains) && !s.elig.Contains("tour_kt_cs_1") && !s.elig.Contains("tour_kt_cs_2");
                bool foot = AppLanguage.Code != "ko" || (FootN(s, "공공데이터") >= 8 && FootN(s, "공공기관") == 0);
                log.Add($"  공공데이터만 — 목록 {Ids(s.list)} · AR 자격 기관 랜드마크 · 화장실 {orgLand.Count(s.elig.Contains)}/4 · 시청(기관) 배분 {govA} · 매니저 {govShowA} · 아래 줄 공공데이터 {FootN(s, "공공데이터")} · 공공기관 {FootN(s, "공공기관")}");
                return !PubOn() && StoresOn() && Ids(s.list) == dataOnly && Ids(s.pass) == dataOnly && Ids(s.show) == dataOnly && elig && foot && !govA && !govShowA;
            }, "「공공데이터」만 켠 '전체' — 기관 랜드마크 셋 · 화장실 · 사람이 올린 둘 · 가게 넷 (목록 · AR 자격 · 매니저) · 관광공사 · 시청은 없음 · 아래 줄 공공데이터 수에 기관 랜드마크 · 화장실 (11-26 대표님 9번)");
            Do(() => { Press("StoresToggle"); Press("PublicDataToggle"); });
            Sleep(0.5f);
            Do(Refresh);
            Sleep(0.4f);
            Do(() => { Record("공공기관만", FilterManager.CategoryFilterState.All); govB = GovPass(); govShowB = GovShow(); });
            Check(() =>
            {
                var s = snaps["공공기관만전체"];
                bool elig = !orgLand.Any(s.elig.Contains) && s.elig.Contains("tour_kt_cs_1") && s.elig.Contains("tour_kt_cs_2");
                bool foot = AppLanguage.Code != "ko" || (FootN(s, "공공기관") >= 2 && FootN(s, "공공데이터") == 0);
                log.Add($"  공공기관만 — 목록 {Ids(s.list)} · AR 자격 기관 랜드마크 · 화장실 {orgLand.Count(s.elig.Contains)}/4 · 시청(기관) 배분 {govB} · 매니저 {govShowB} · 아래 줄 공공기관 {FootN(s, "공공기관")} · 공공데이터 {FootN(s, "공공데이터")}");
                return PubOn() && !StoresOn() && Ids(s.list) == orgOnly && Ids(s.pass) == orgOnly && Ids(s.show) == orgOnly && elig && foot && govB && govShowB;
            }, "「공공기관」만 켠 '전체' — 관광공사 둘 · 사람이 올린 둘 · 시청(기관) · 기관 랜드마크 · 화장실 · 가게는 없음 (11-26 대표님 9번)");
            Check(() => FilterManager.ChipKey("publicData", "landmark") == "stores" && FilterManager.ChipKey("publicData", "park") == "stores"
                        && FilterManager.ChipKey("publicData", "sport") == "stores" && FilterManager.ChipKey("publicData", "toilet") == "stores"
                        && FilterManager.ChipKey("publicData", "gov") == "publicData" && FilterManager.ChipKey("publicData", "culture") == "publicData"
                        && FilterManager.ChipKey("publicData", "tour") == "publicData" && FilterManager.ChipKey("publicData", "stay") == "publicData"
                        && FilterManager.ChipKey("", "park") == "" && FilterManager.ChipKey("stores", "food") == "stores",
                  "칩 나누기 — 기관 랜드마크 · 공원 · 스포츠 · 화장실 = 공공데이터 · 시청 · 박물관 · 관광공사(관광지 · 숙박) = 공공기관 · 사람이 올린 공원 = 우팡 데이터 · 가게 = 공공데이터");

            // 되돌리기 — 넣은 것 · 막아 둔 번호 · 칩 · 분류 · 저장값
            Do(() =>
            {
                var fm = Fm();
                if (fm != null && fm.CategoryState != FilterManager.CategoryFilterState.All) fm.SetCategory(FilterManager.CategoryFilterState.All);
                if (PubOn() != pubWas) Press("PublicDataToggle");
                if (StoresOn() != storesWas) Press("StoresToggle");
                if (fm != null && catWas != FilterManager.CategoryFilterState.All) fm.SetCategory(catWas);
                if (PetOn() != petWas) ClickToggle("PetFriendlyToggle");
                var pr = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>(FindObjectsInactive.Include);
                var fly = dm != null && inFlight != null ? inFlight.GetValue(dm) as HashSet<int> : null;
                var failed = pr != null && rowsFailed != null ? rowsFailed.GetValue(pr) as Dictionary<int, float> : null;
                foreach (int id in dIds)
                {
                    string raw = id.ToString();
                    if (dm != null) { dm.DespawnFullObject(raw); dm.DespawnIndicatorOnly(raw); dm.GetLightCache().RemoveAll(c => c.rawId == raw); }
                    fly?.Remove(id);
                    failed?.Remove(id);
                }
                if (tm != null)
                    foreach (var id in tIds) { tm.DespawnFullObject(id); tm.DespawnIndicatorOnly(id); tm.GetPlaceDataMap().Remove(id); tm.GetCachedPlaces().RemoveAll(c => c.rawId == id); }
                if (stm != null)
                    foreach (var id in sIds) { stm.DespawnFullObject(id); stm.DespawnIndicatorOnly(id); stm.GetPlaceDataMap().Remove(id); stm.GetCachedPlaces().RemoveAll(c => c.rawId == id); }
            });
            Sleep(0.6f);
            Do(() =>
            {
                StoresFinish();   // 저장값을 처음 그대로 (없던 값은 지움)
                Refresh();
                log.Add("  되돌림: 분류 " + CatName(Fm() != null ? Fm().CategoryState : FilterManager.CategoryFilterState.All) + " · 애견동반 " + (PetOn() ? "켜짐" : "꺼짐")
                        + " · 공공기관 칩 " + (PubOn() ? "켜짐" : "꺼짐") + " · 공공데이터 칩 " + (StoresOn() ? "켜짐" : "꺼짐"));
            });
            Sleep(0.4f);
            Check(() => Fm() != null && Fm().CategoryState == catWas && PetOn() == petWas && PubOn() == pubWas && StoresOn() == storesWas
                        && SessionState.GetString(StoresBackupKey, "") == "" && !Cached().Any() && (Plm() == null || !Plm().ShownEntries.Any(e => mine.Contains(e.id))),
                  "되돌림 — 넣은 곳 없앰 · 분류 · 애견동반 · 두 칩 · 저장값 처음대로");
        }

        private class Snap
        {
            public int rows, cached;
            public readonly HashSet<string> list = new HashSet<string>(), pass = new HashSet<string>(), show = new HashSet<string>();
            public readonly HashSet<string> ar = new HashSet<string>(), elig = new HashSet<string>(), pub = new HashSet<string>();   // AR 에 뜬 곳 · 뜰 자격 · 기관 자료 (uniqueId)
            public readonly Dictionary<string, int> foot = new Dictionary<string, int>();
        }
    }
}
