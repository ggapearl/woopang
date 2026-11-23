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
    /// 11-20 N8a — 대표님 「새 가게 DB(소상공인 상가정보) → 이름 「공공데이터」」 · 「앱은 가까운 몇 개만 받기」: 근처 가게 매니저(StoreManager).
    ///  · stores : 내 PC 가짜 가게 서버(127.0.0.1:5795 · stores_stub.py)에서 받는다 — 운영 서버 · 상가정보 API 에 묻지 않는다.
    ///    받기(반경 50m · 20곳 · 좌표 없는 줄 · 같은 번호 두 번째는 버림 · 모르는 분류는 샵) → AR 에 뜸(분류 스티커 · 사진을 못 받아도 스티커) ·
    ///    정보 창 업종 · 층 · 출처 줄(오브젝트 · 목록 사진으로 열 때 같다) · 목록 줄 그림과 아래 「공공데이터: N」 /
    ///    칩 끄고 켜기 · 분류를 고르면 그 분류 가게만 · 칩은 회색 · 애견동반을 켜면 가게 없음 / X 숨기기(store_ 번호) · 되살리기 /
    ///    서버가 404 · not_ready → 빈 목록으로 쉬고(AR · 목록에서 빠지고 남은 정보도 지움) 다시 묻지 않음 · 429 · 500 → 목록은 그대로 두고 잠시 묻지 않음.
    ///    끝나면 가짜 서버 주소 · 분류 · 애견동반 · 칩 · 저장값 · 숨긴 가게를 되돌린다 (로그엔 번호 · 개수만)
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private const string StoresStub = "http://127.0.0.1:5795";
        private const string StoresBackupKey = "Redesign0926PlayTest.StoresPrefs";
        private static readonly string[] StoresPrefKeys = { "Filter_Stores_V1", "Filter_Category_V1", "Filter_PetFriendly_V3", "Filter_PublicData_V2" };

        // 칩 · 분류 저장값 — 시험이 중간에 끊겨도(오류 · 플레이 끔 · 스크립트 다시 읽기) 되돌리도록 SessionState 에 둔다
        private static void StashStoresPrefs()
        {
            var parts = new List<string>();
            foreach (var k in StoresPrefKeys) parts.Add(PlayerPrefs.HasKey(k) ? k + "\u0001" + PlayerPrefs.GetInt(k) : k);
            SessionState.SetString(StoresBackupKey, string.Join("\u0002", parts));
        }

        /// <summary>가게 시험 뒷정리 — 끝날 때 · 오류로 끊겼을 때(러너) · 다음 시험을 켜기 전</summary>
        private static void StoresFinish()
        {
            StoreManager.EditorBase = null;
            string s = SessionState.GetString(StoresBackupKey, "");
            if (s.Length == 0) return;
            SessionState.EraseString(StoresBackupKey);
            foreach (var part in s.Split('\u0002'))
            {
                int i = part.IndexOf('\u0001');
                if (i < 0) PlayerPrefs.DeleteKey(part);
                else PlayerPrefs.SetInt(part.Substring(0, i), int.Parse(part.Substring(i + 1)));
            }
            PlayerPrefs.Save();
        }

        private static string StoresDev(string q)
        {
            using (var wc = new System.Net.WebClient())
            {
                wc.Encoding = System.Text.Encoding.UTF8;
                try { return wc.DownloadString(StoresStub + q).Trim(); }
                catch (System.Net.WebException e) { log.Add("  가짜 가게 서버 실패: " + e.Message); return ""; }
            }
        }

        private static void PlanStores()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            string[] ids = { "st_t1", "st_t2", "st_t3", "st_t4", "st_t5", "st_t6" };
            var footerF = typeof(PlaceListManager).GetField("cachedFooter", NP);
            var overviewF = typeof(DoubleTap3D).GetField("overview", NP);
            var nameF = typeof(DoubleTap3D).GetField("placeName", NP);
            var storesF = typeof(FilterManager).GetField("filterStores", NP);
            var fullF = typeof(FilterManager).GetField("currentFullAllocations", NP);
            var indF = typeof(FilterManager).GetField("currentIndicatorAllocations", NP);
            var photoKey = typeof(R0926PlaceRows).GetMethod("PhotoKey", NP);

            StoreManager stm = null;
            var catWas = FilterManager.CategoryFilterState.All;
            bool petWas = false, storesWas = true, stubOk = false;
            int hits = 0;
            string infoNote = "", hideId = null;
            bool ko = AppLanguage.Code == "ko";
            string footLabel = ko ? "공공데이터:" : "Public data:";

            FilterManager Fm() => UnityEngine.Object.FindAnyObjectByType<FilterManager>();
            bool PetOn() => Fm() != null && Fm().PetState == FilterManager.PetFriendlyFilterState.OnlyPetFriendly;
            bool StoresOn() => Fm() != null && storesF?.GetValue(Fm()) is bool b && b;
            bool StoresFilter() => Fm() != null && Fm().GetActiveFilters().TryGetValue("stores", out bool v) && v;
            Toggle StoresChip()
            {
                foreach (var t in UnityEngine.Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include))
                    if (t.name == "StoresToggle" && t.transform.parent != null && t.transform.parent.name == "FilterButtonPanel") return t;
                return null;
            }
            int Hits() => int.TryParse(StoresDev("/dev/hits"), out int h) ? h : -1;
            void Mode(string m) => StoresDev("/dev/mode?m=" + m);
            HashSet<string> Alloc(bool fullOnly)
            {
                var s = new HashSet<string>();
                var fm = Fm();
                if (fm == null) return s;
                foreach (var f in fullOnly ? new[] { fullF } : new[] { fullF, indF })
                    if (f?.GetValue(fm) is HashSet<string> set)
                        foreach (var u in set) if (u.StartsWith(StoreManager.Prefix)) s.Add(u.Substring(StoreManager.Prefix.Length));
                return s;
            }
            HashSet<string> Spawned()
            {
                var s = new HashSet<string>();
                if (stm == null) return s;
                foreach (var kv in stm.GetSpawnedObjects()) if (kv.Value != null && kv.Value.activeSelf) s.Add(kv.Key);
                foreach (var kv in stm.GetIndicatorOnlyObjects()) if (kv.Value != null && kv.Value.activeSelf) s.Add(kv.Key);
                return s;
            }
            List<PlaceListManager.ListEntry> Rows()
            {
                var p = Plm();
                return p == null ? new List<PlaceListManager.ListEntry>() : p.ShownEntries.Where(e => e.data is StoreData).ToList();
            }
            string RowIds() => Ids(Rows().Select(r => r.id));
            string Footer() { var p = Plm(); return p != null ? footerF?.GetValue(p) as string ?? "" : ""; }
            string Ids(IEnumerable<string> s) => string.Join(",", s.OrderBy(x => x, StringComparer.Ordinal).Select(x => x.Replace("st_t", "")));
            void Realloc() { Fm()?.TriggerReallocation(); Plm()?.UpdateUI(); }
            void Cat(FilterManager.CategoryFilterState c) => Fm()?.SetCategory(c);
            void AskAgain() { var h = Fm() != null ? Fm().Here : Vector2.zero; stm.RefreshCache(h.x, h.y); }
            bool StickerFace(GameObject go, string category)
            {
                var d = go != null ? go.GetComponentInChildren<ImageDisplayController>(true) : null;
                if (d == null || d.cubeRenderer == null || d.cubeRenderer.sharedMaterial == null) return false;
                var m = d.cubeRenderer.sharedMaterial;
                var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                var want = Resources.Load<Texture2D>(StoreManager.StickerPath(category));
                return want != null && tex == want;
            }
            // 칩 · 분류를 바꾸고 다시 배분 — 칩은 누른 다음 프레임에 바뀐다
            void After(Action a, Func<bool> until, string what)
            {
                Do(a);
                Sleep(0.6f);
                Do(Realloc);
                Wait(until, 10f, what);
                Do(() => Plm()?.UpdateUI());
                Sleep(0.3f);
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
            Wait(() => { stm = StoreManager.Instance; return stm != null && stm.IsCacheReady && !stm.EditorFetching; }, 30f, "가게 매니저 준비됨");
            Check(() => stm != null && footerF != null && overviewF != null && nameF != null && storesF != null && fullF != null && indF != null && photoKey != null,
                  "시험에 쓸 안쪽 값 · 함수를 모두 찾음");
            Do(() =>
            {
                // 운영 서버엔 아직 가게 길이 없다 (서버 패치 · 표 만들기 승인 전) — 빈 목록으로 쉬고 있어야 한다
                log.Add($"  시작 — 쉬는 중 {stm.EditorDormant} · 가게 빛 캐시 {stm.GetCachedPlaces().Count} · 떠 있는 가게 {Spawned().Count}");
                var fm = Fm();
                catWas = fm != null ? fm.CategoryState : FilterManager.CategoryFilterState.All;
                petWas = PetOn();
                storesWas = StoresOn();
                StashStoresPrefs();
                if (fm != null && catWas != FilterManager.CategoryFilterState.All) fm.SetCategory(FilterManager.CategoryFilterState.All);
                if (petWas) ClickToggle("PetFriendlyToggle");
                if (!storesWas) ClickToggle("StoresToggle");
                stubOk = StoresDev("/dev/reset") == "ok";
                StoreManager.EditorBase = StoresStub;
                stm.EditorRefetch();
            });
            Check(() => stubOk, "가짜 가게 서버(5795)가 떠 있음");
            Wait(() => stm != null && !stm.EditorFetching && stm.GetCachedPlaces().Count > 0, 15f, "가짜 서버에서 가게 목록 받음");
            Check(() =>
            {
                var c = stm.GetCachedPlaces();
                var map = stm.GetPlaceDataMap();
                string last = StoresDev("/dev/last");
                int n = Hits();
                log.Add($"  받은 것 — 가게 빛 캐시 {c.Count} ({Ids(c.Select(x => x.rawId))}) · 요청 {last} · 서버에 물은 횟수 {n}");
                return c.Count == 6 && Ids(c.Select(x => x.rawId)) == "1,2,3,4,5,6"
                       && c.All(x => x.sourceManager == "Store" && x.filterKey == "stores" && x.uniqueId == StoreManager.Prefix + x.rawId && !x.petFriendly)
                       && map.TryGetValue("st_t5", out var t5) && t5.category == "shop"
                       && map.TryGetValue("st_t1", out var t1) && t1.name == "시험 가게 1" && t1.category == "food"
                       && !map.ContainsKey("st_t7") && last == "radius=50&limit=20" && n == 1;
            }, "받기 — 가까운 가게 6곳(반경 50m · 20곳) · 좌표 없는 줄 · 같은 번호 두 번째는 버림 · 모르는 분류는 샵 · 한 번만 물음");

            // AR — 공공기관 칩이 켜져 있으면 가게는 가까운 6곳까지 (6곳이라 전부)
            Wait(() => Alloc(false).Count == 6, 15f, "가게 6곳이 배분됨");
            Wait(() => { var f = Alloc(true); return f.Count > 0 && f.All(id => stm.GetSpawnedObjects().TryGetValue(id, out var g) && g != null); }, 20f,
                 "3D 로 고른 가게가 모두 3D 로 뜸 (사진 가게는 사진을 못 받은 뒤 스티커로)");
            Shot("stores_ar");
            Check(() =>
            {
                var full = Alloc(true);
                var bad = full.Where(id => !(stm.GetPlaceDataMap().TryGetValue(id, out var s) && stm.GetSpawnedObjects().TryGetValue(id, out var go) && StickerFace(go, s.category))).ToList();
                log.Add($"  3D {full.Count}곳 ({Ids(full)}) · 화살표만 {Alloc(false).Count - full.Count}곳 · 스티커가 아닌 면 [{Ids(bad)}]");
                foreach (var id in bad.Take(2))
                {
                    var go = stm.GetSpawnedObjects().TryGetValue(id, out var g) ? g : null;
                    var d = go != null ? go.GetComponentInChildren<ImageDisplayController>(true) : null;
                    var r = d != null ? d.cubeRenderer : null;
                    var m = r != null ? r.sharedMaterial : null;
                    var tex = m == null ? null : m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    log.Add($"    {id.Replace("st_t", "")}: 그림 붙이는 곳 {(d == null ? "없음" : d.enabled ? "켜짐" : "꺼짐")} · 면 {(r != null ? r.name + (r.enabled ? "" : "(꺼짐)") : "없음")} · 재질 {(m != null ? m.name : "없음")} · 붙은 그림 {(tex != null ? tex.name : "없음")}"
                            + $" · 렌더러 {(go != null ? go.GetComponentsInChildren<Renderer>(true).Length : 0)}개");
                }
                return full.Count > 0 && bad.Count == 0 && full.Contains("st_t2");
            }, "3D 가게 앞면은 분류 스티커 — 사진 주소가 있어도 못 받으면 스티커 (기본 면이 보이지 않음)");

            // 정보 창 — 오브젝트를 두 번 눌러 열 때와 목록 사진으로 열 때(R0926PlaceInfo) 같은 글
            Check(() =>
            {
                if (!stm.GetSpawnedObjects().TryGetValue("st_t1", out var go) || go == null) return false;
                var dt = go.GetComponentInChildren<DoubleTap3D>(true);
                var s1 = stm.GetPlaceDataMap()["st_t1"];
                string ov = overviewF.GetValue(dt) as string ?? "";
                string nm = nameF.GetValue(dt) as string ?? "";
                string want = StoreManager.Overview(s1);
                bool src = ko ? ov.StartsWith("한식 · 1층") && ov.Contains("출처: 소상공인시장진흥공단 상가(상권)정보") : ov.Contains("SEMAS");
                var host = R0926PlaceInfo.Host();
                bool filled = R0926PlaceInfo.Fill(host, s1);
                string hostOv = host != null ? overviewF.GetValue(host) as string ?? "" : "";
                infoNote = "이름 " + (nm == s1.name ? "같음" : "다름") + " · 글 " + ov.Replace("\n", " / ") + " · 목록 사진으로 열 때 " + (hostOv == want ? "같음" : "다름");
                return nm == s1.name && ov == want && src && filled && hostOv == want;
            }, "정보 창 — 업종 · 층 · 「출처: 소상공인시장진흥공단 상가(상권)정보」 (오브젝트 · 목록 사진으로 열 때 같다)");
            Do(() => log.Add("  " + infoNote));
            Check(() => !ko || (StoreManager.FloorText("1") == "1층" && StoreManager.FloorText("B1") == "지하 1층" && StoreManager.FloorText("지1") == "지하 1층"
                        && StoreManager.FloorText("지하2") == "지하 2층" && StoreManager.FloorText("3층") == "3층" && StoreManager.FloorText("") == ""
                        && StoreManager.FloorText("M") == "M" && StoreManager.FloorText("0") == "0"),
                  "층 글 — 1 → 1층 · B1 · 지1 · 지하2 → 지하 N층 · 3층 그대로 · 빈 칸은 안 씀 · 모르는 값은 그대로");

            // 목록
            Do(() => Plm()?.UpdateUI());
            Sleep(0.3f);
            Check(() =>
            {
                var rows = Rows();
                string f = Footer();
                var rk = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>(FindObjectsInactive.Include);
                string K(string id)
                {
                    var e = rows.FirstOrDefault(r => r.id == id);
                    return rk != null && e.data != null ? photoKey.Invoke(rk, new object[] { e }) as string : null;
                }
                string k1 = K("st_t1"), k5 = K("st_t5"), k2 = K("st_t2");
                bool foot = ko ? f.Contains("\n공공데이터: 6") && f.Contains("\n공공기관: ") : f.Contains(": 6");
                log.Add($"  목록 가게 줄 {rows.Count} ({RowIds()}) · 줄 그림 1={k1} · 5={k5} · 2={(k2 != null && k2.StartsWith("https") ? "사진 주소" : k2)} · 아래 줄 {(foot ? "맞음" : "다름")}");
                return rows.Count == 6 && rows.All(r => r.kind == PlaceListManager.EntryKind.Place && !r.pet) && foot
                       && k1 == "res:PublicIcons/food" && k5 == "res:PublicIcons/shop" && k2 != null && k2.StartsWith("https");
            }, "목록 — 가게 6줄 · 줄 그림은 분류 스티커(사진 가게는 사진) · 아래 「공공데이터: 6」 · 관광공사 줄은 「공공기관」");

            // 칩 끄고 켜기
            After(() => ClickToggle("StoresToggle"), () => Alloc(false).Count == 0 && Spawned().Count == 0, "공공데이터 칩을 끄면 가게가 AR 에서 빠짐");
            Check(() => !StoresOn() && !StoresFilter() && PlayerPrefs.GetInt("Filter_Stores_V1", 1) == 0 && Alloc(false).Count == 0 && Spawned().Count == 0
                        && Rows().Count == 0 && !Footer().Contains(footLabel) && stm.GetCachedPlaces().Count == 6,
                  "칩 끔 — AR · 목록 · 아래 줄에서 가게가 빠짐 · 저장값 0 · 받은 목록은 그대로");
            After(() => ClickToggle("StoresToggle"), () => Alloc(false).Count == 6, "칩을 켜면 가게 6곳이 다시 배분됨");
            Check(() => StoresOn() && StoresFilter() && PlayerPrefs.GetInt("Filter_Stores_V1", 0) == 1 && Rows().Count == 6 && Hits() == 1,
                  "칩 켬 — 가게 6곳 · 목록 6줄 · 저장값 1 · 서버엔 다시 묻지 않음");

            // 분류 — 그 분류 가게만 (모르는 분류는 샵) · 공공데이터 칩은 회색
            After(() => Cat(FilterManager.CategoryFilterState.Food), () => Ids(Alloc(false)) == "1,6", "음식점 분류 — 음식점 가게 2곳만 배분");
            Check(() =>
            {
                var chip = StoresChip();
                log.Add($"  음식점 — 배분 {Ids(Alloc(false))} · 보이는 {Ids(Spawned())} · 목록 {RowIds()} · 칩 누르기 {(chip != null && chip.interactable ? "됨" : "안 됨")}");
                return Ids(Alloc(false)) == "1,6" && Ids(Spawned()) == "1,6" && RowIds() == "1,6" && chip != null && !chip.interactable && !StoresFilter();
            }, "분류 음식점 — AR · 목록 모두 음식점 가게만 · 공공데이터 칩은 회색(누를 수 없음)");
            After(() => Cat(FilterManager.CategoryFilterState.Shop), () => Ids(Alloc(false)) == "3,5", "샵 분류 — 샵 가게 2곳만");
            Check(() => Ids(Alloc(false)) == "3,5" && Ids(Spawned()) == "3,5" && RowIds() == "3,5", "분류 샵 — 편의점 · 모르는 분류(→ 샵) 가게만");
            After(() => Cat(FilterManager.CategoryFilterState.Stay), () => Ids(Alloc(false)) == "4", "숙소 분류 — 숙소 가게 1곳만");
            Check(() => Ids(Alloc(false)) == "4" && RowIds() == "4", "분류 숙소 — 숙소 가게만");
            After(() => Cat(FilterManager.CategoryFilterState.All), () => Alloc(false).Count == 6, "전체로 돌아가면 가게 6곳");
            Check(() => { var chip = StoresChip(); return Alloc(false).Count == 6 && RowIds() == "1,2,3,4,5,6" && chip != null && chip.interactable && chip.isOn && StoresFilter(); },
                  "전체 — 가게 6곳 · 공공데이터 칩 다시 누를 수 있고 켜진 그대로");

            // 애견동반 — 가게 자료엔 애견동반이 없다
            After(() => ClickToggle("PetFriendlyToggle"), () => PetOn() && Alloc(false).Count == 0 && Spawned().Count == 0, "애견동반을 켜면 가게가 빠짐");
            Check(() => PetOn() && Alloc(false).Count == 0 && Spawned().Count == 0 && Rows().Count == 0, "애견동반 — AR · 목록에서 가게가 빠짐");
            After(() => ClickToggle("PetFriendlyToggle"), () => !PetOn() && Alloc(false).Count == 6, "애견동반을 끄면 가게 6곳");

            // X 숨기기 — store_ 번호 · 되살리기
            Do(() =>
            {
                GameObject go = null;
                if (!stm.GetSpawnedObjects().TryGetValue("st_t1", out go) || go == null) stm.GetIndicatorOnlyObjects().TryGetValue("st_t1", out go);
                var t = go != null ? go.GetComponentInChildren<Target>(true) : null;
                hideId = t != null ? HiddenPlaces.ResolveUniqueId(t) : null;
                if (hideId == StoreManager.Prefix + "st_t1") HiddenPlaces.HideTarget(t);
            });
            Sleep(0.4f);
            Do(Realloc);
            Wait(() => HiddenPlaces.IsHidden("store_st_t1") && !Alloc(false).Contains("st_t1") && !Spawned().Contains("st_t1"), 10f, "X 로 숨긴 가게가 AR 에서 빠짐");
            Do(() => Plm()?.UpdateUI());
            Sleep(0.3f);
            Check(() => hideId == "store_st_t1" && HiddenPlaces.IsHidden(hideId) && Ids(Alloc(false)) == "2,3,4,5,6" && RowIds() == "2,3,4,5,6",
                  "숨기기 — 번호 store_st_t1 · AR · 목록에서 빠지고 나머지 5곳은 그대로");
            After(() => HiddenPlaces.Unhide("store_st_t1"), () => Alloc(false).Count == 6, "되살리면 가게 6곳");
            Check(() => !HiddenPlaces.IsHidden("store_st_t1") && Alloc(false).Contains("st_t1") && RowIds() == "1,2,3,4,5,6", "되살리기 — 숨긴 가게가 다시 AR · 목록에");

            // 429 · 500 — 받은 목록은 그대로 두고 잠시 묻지 않는다 (쉬기는 아님)
            Do(() => { hits = Hits(); Mode("429"); stm.EditorRefetch(); });
            Wait(() => !stm.EditorFetching, 10f, "429 응답 받음");
            Do(AskAgain);
            Sleep(1.0f);
            Check(() => Hits() == hits + 1 && !stm.EditorDormant && stm.GetCachedPlaces().Count == 6 && Alloc(false).Count == 6,
                  "429(너무 자주) — 받은 목록 · AR 그대로 · 1분 동안 다시 묻지 않음");
            Do(() => { hits = Hits(); Mode("500"); stm.EditorRefetch(); });
            Wait(() => !stm.EditorFetching, 10f, "500 응답 받음");
            Do(AskAgain);
            Sleep(1.0f);
            Check(() => Hits() == hits + 1 && !stm.EditorDormant && stm.GetCachedPlaces().Count == 6 && Alloc(false).Count == 6,
                  "500(서버 오류) — 받은 목록 · AR 그대로 · 잠시(8초부터 늘려 가며) 다시 묻지 않음");

            // 404 — 서버에 가게 길이 없다 (지금 운영 서버) → 빈 목록으로 30분 쉰다
            Do(() => { hits = Hits(); Mode("404"); stm.EditorRefetch(); });
            Wait(() => !stm.EditorFetching, 10f, "404 응답 받음");
            Wait(() => Alloc(false).Count == 0 && Spawned().Count == 0, 10f, "404 — 가게가 AR 에서 빠짐");
            Do(AskAgain);
            Sleep(7.0f);   // 걸으며 다시 묻기(3초마다)가 두 번 돈다 — 쉬는 동안은 묻지 않아야 한다
            Do(() => Plm()?.UpdateUI());
            Sleep(0.3f);
            Check(() => Hits() == hits + 1 && stm.EditorDormant && stm.IsCacheReady && stm.GetCachedPlaces().Count == 0 && stm.GetPlaceDataMap().Count == 0
                        && Rows().Count == 0 && !Footer().Contains(footLabel),
                  "404(서버에 가게 길 없음) — 빈 목록으로 쉼 · AR · 목록 · 아래 줄에서 빠지고 남은 가게 정보도 지움 · 쉬는 동안 다시 묻지 않음");

            // not_ready — 서버가 아직 자료를 받기 전 → 404 와 같이 쉰다
            Do(() => { Mode("ok"); stm.EditorRefetch(); });
            Wait(() => !stm.EditorFetching && stm.GetCachedPlaces().Count == 6, 10f, "다시 ok — 가게 6곳 받음");
            Wait(() => Alloc(false).Count == 6, 10f, "가게 6곳 다시 배분");
            Do(() => { hits = Hits(); Mode("notready"); stm.EditorRefetch(); });
            Wait(() => !stm.EditorFetching, 10f, "not_ready 응답 받음");
            Wait(() => Alloc(false).Count == 0 && Spawned().Count == 0, 10f, "not_ready — 가게가 AR 에서 빠짐");
            Do(() => Plm()?.UpdateUI());
            Sleep(0.3f);
            Check(() => Hits() == hits + 1 && stm.EditorDormant && stm.GetCachedPlaces().Count == 0 && stm.GetPlaceDataMap().Count == 0 && Rows().Count == 0,
                  "not_ready(서버가 아직 자료를 받기 전) — 404 와 같이 빈 목록으로 쉼");

            // 되돌리기 — 가게는 운영과 같이 빈 목록으로 쉬는 채로 둔다
            Do(() =>
            {
                foreach (var id in ids) { HiddenPlaces.Unhide(StoreManager.Prefix + id); PlaceSpawnGate.Drop(StoreManager.Prefix + id); }
                var fm = Fm();
                if (fm != null && fm.CategoryState != catWas) fm.SetCategory(catWas);
                if (PetOn() != petWas) ClickToggle("PetFriendlyToggle");
                if (StoresOn() != storesWas) ClickToggle("StoresToggle");
            });
            Sleep(0.6f);
            Do(() =>
            {
                StoresFinish();   // 가짜 서버 주소 없앰 · 저장값을 처음 그대로 (없던 값은 지움)
                StoresDev("/dev/reset");
                Realloc();
                log.Add("  되돌림: 분류 " + Fm()?.CategoryState + " · 애견동반 " + (PetOn() ? "켜짐" : "꺼짐") + " · 공공데이터 칩 " + (StoresOn() ? "켜짐" : "꺼짐")
                        + " · 가게 빛 캐시 " + (stm != null ? stm.GetCachedPlaces().Count : -1));
            });
            Sleep(0.4f);
            Check(() => StoreManager.EditorBase == null && Fm() != null && Fm().CategoryState == catWas && PetOn() == petWas && StoresOn() == storesWas
                        && SessionState.GetString(StoresBackupKey, "") == "" && !ids.Any(id => HiddenPlaces.IsHidden(StoreManager.Prefix + id))
                        && stm.GetCachedPlaces().Count == 0 && stm.GetSpawnedObjects().Count == 0,
                  "되돌림 — 가짜 서버 주소 없앰 · 분류 · 애견동반 · 칩 · 저장값 · 숨긴 가게 처음대로 · 가게는 운영과 같이 빈 목록으로 쉬는 중");
        }
    }
}
