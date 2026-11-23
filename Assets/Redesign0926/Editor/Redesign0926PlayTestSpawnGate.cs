using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 P1 — 대표님 「오브젝트 발생함에 있어서 … 메인사진, 서브사진까지 모두 불러오고나서 … 완료가 되면 오브젝트를 생성 …
    ///  이때 발생하는 오브젝트는 0.2초 페이드인으로」 (PlaceSpawnGate · R0926DistanceFade · GLBSpawnEffect).
    ///  · spawngate :
    ///    1) 우팡 큐브 3곳 — 느린 회선 흉내(곳마다 1.5초 · 서브사진마다 1초)로 다시 띄운다:
    ///       받는 동안은 임시 화살표만 · 뜨는 순간 이미 사진 면(기본 면 아님) · 서브사진은 다 받은 뒤 · 0.2초 페이드인
    ///    2) 기기 규칙 — 앵커가 잡히기 전엔 사진을 붙여도 면을 켜지 않는다 · 잡히면 사진 면이 0.2초 페이드인
    ///    3) 메인 사진 404 — 기다리지 않고 바로 뜬다 (예전처럼 기본 면 + 뒤에서 다시 받기)
    ///    4) 관광공사 — 같은 규칙 (사진 있는 곳이 떠 있으면)
    ///    5) 장소 3D (운영 시바견 · 공개 GET) — 스피너 없이 · 0.2초 페이드인 · 끝나면 그대로 보임.
    ///       기기 규칙으로 한 번 더 — 모델을 다 받아도 앵커가 잡히기 전엔 숨김 · 잡히면 0.2초
    ///    서버에 쓰는 것 없음 (상세 · 사진 · 모델은 공개 GET). 기록엔 번호 · 개수 · 시간만 (이름 · 좌표 · 거리는 남기지 않는다).
    ///    끝나면 시험 고리를 되돌린다 (늦춤 · 기기 규칙 · 바꾼 사진 주소 · 앵커 값 · 3D 칩 · 표본 수집)
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private sealed class SgTrack
        {
            public string label, main;
            public List<string> subs = new List<string>();
            public Func<GameObject> find;
            public Func<bool> indicator;
            public Action<GameObject> onAppear;
            public float appearAt = -1f, visibleAt = -1f;
            public bool faceOn, facePhoto, faceDefault, indSeen;
            public int subsDisk = -1, subsBusy = -1;
            public GameObject obj;
            public readonly List<Vector2> alpha = new List<Vector2>();
        }

        private static readonly List<SgTrack> sgTracks = new List<SgTrack>();
        private static readonly List<string> sgErrors = new List<string>();
        private static readonly List<Vector2> sgDogAlpha = new List<Vector2>();
        private static MaterialPropertyBlock sgMpb;
        private static float sgT0, sgDogVisibleAt = -1f;
        private static bool sgSpinnerEver, sgRevealEver;
        private static int sgOtherErrors;
        private static Func<GameObject> sgDogFind;

        private static void PlanSpawnGate()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance, NS = BindingFlags.NonPublic | BindingFlags.Static;
            const int Dog = 34837;
            DataManager dm = null;
            FilterManager fm = null;
            var picks = new List<int>();
            int completedWas = 0;

            T DmField<T>(string n) where T : class => dm != null ? typeof(DataManager).GetField(n, NP)?.GetValue(dm) as T : null;
            GameObject DmObj(int id) => dm != null && dm.GetSpawnedObjects().TryGetValue(id, out var g) ? g : null;
            bool DmInd(int id) => DmField<Dictionary<int, GameObject>>("indicatorOnlyObjects") is Dictionary<int, GameObject> m && m.ContainsKey(id);
            string DmMain(PlaceData p) => typeof(DataManager).GetMethod("GateMainPhoto", NS)?.Invoke(null, new object[] { p }) as string;
            void StartSampler(EditorApplication.CallbackFunction f)
            {
                sgT0 = Time.realtimeSinceStartup;
                EditorApplication.update -= f;
                EditorApplication.update += f;
            }
            SgTrack Track(int id, string tag)
            {
                var p = dm.GetPlaceDataMap()[id];
                return new SgTrack
                {
                    label = "dm_" + id + tag,
                    main = DmMain(p),
                    subs = new List<string>(DataManager.FlattenSubPhotos(p)),
                    find = () => DmObj(id),
                    indicator = () => DmInd(id),
                };
            }
            string Ramp(List<Vector2> a)
            {
                var r = SgRamp(a);
                return $"처음 {r.first:F2} · 다 보임(0.95) {r.reach:F2}초 · 중간 프레임 {r.mids} · 한 프레임 최대 +{r.jump:F2} · 표본 {r.n}";
            }
            bool FadeOk(List<Vector2> a)
            {
                var r = SgRamp(a);
                return r.n > 0 && r.first >= 0f && r.first <= 0.35f && r.reach >= 0.1f && r.reach <= 0.6f && r.mids >= 2;
            }
            CustomARGeospatialCreatorAnchor AnchorOf(GameObject go) => go == null ? null
                : go.GetComponentInChildren<CustomARGeospatialCreatorAnchor>(true) ?? go.GetComponentInParent<CustomARGeospatialCreatorAnchor>(true);
            // 기기는 앵커를 만들 때 렌더러를 모두 끈다 (에디터 EditorPlace 는 끄지 않는다) — 같게 맞춘다
            void HideLikeDevice(CustomARGeospatialCreatorAnchor a)
            {
                if (a != null) typeof(CustomARGeospatialCreatorAnchor).GetMethod("SetVisible", NP)?.Invoke(a, new object[] { false });
            }
            // 배분이 한 번 돌아 상세 · 사진 대기 표시(detailPendingIds)가 지워질 때까지 — 막 뜬 곳을 바로 내리면 그 표시가 남아
            // 15초 동안 다시 띄우지 않는다 (배분 밖에서 내린 시험 순서 탓 · 앱에선 배분이 내린다)
            void Settle(Func<string> uid)
            {
                Do(() => fm?.TriggerReallocation());
                Wait(() =>
                {
                    string u = uid();
                    return u == null || fm == null
                           || !(typeof(FilterManager).GetField("detailPendingIds", NP)?.GetValue(fm) is Dictionary<string, float> d && d.ContainsKey(u));
                }, 6f, "배분 대기 표시 정리");
            }

            // 0) 시작 — 사진 있는 우팡 큐브 3곳 (가까운 순 · 춤 분류 · 앱 내장 그림 분류 제외 · 서브사진 1~4장 · 배분이 3D 로 고른 곳)
            DsBoot();
            Do(() =>
            {
                dm = DataManager.Instance;
                fm = UnityEngine.Object.FindAnyObjectByType<FilterManager>(FindObjectsInactive.Include);
                sgErrors.Clear();
                sgOtherErrors = 0;
                Application.logMessageReceived -= SgLog;
                Application.logMessageReceived += SgLog;
            });
            Wait(() =>
            {
                picks.Clear();
                if (dm == null || Camera.main == null) return false;
                var map = dm.GetPlaceDataMap();
                var cam = Camera.main.transform.position;
                var cand = new List<KeyValuePair<float, int>>();
                foreach (var kv in dm.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy || !map.TryGetValue(kv.Key, out var p) || p == null || p.model_type != "cube") continue;
                    if (dm.IsAnimCategory(kv.Key) || DmMain(p) == null) continue;
                    int n = DataManager.FlattenSubPhotos(p).Count;
                    if (n < 1 || n > 4) continue;
                    if (fm != null && !fm.WantsFull("dm_" + kv.Key)) continue;
                    cand.Add(new KeyValuePair<float, int>((kv.Value.transform.position - cam).sqrMagnitude, kv.Key));
                }
                cand.Sort((a, b) => a.Key.CompareTo(b.Key));
                foreach (var c in cand) { picks.Add(c.Value); if (picks.Count == 3) break; }
                return picks.Count == 3;
            }, 60f, "사진 · 서브사진 있는 우팡 큐브 3곳");
            Sleep(2f);   // 처음 띄우기(한 프레임에 하나씩)가 가라앉게
            Do(() => log.Add("  고른 곳: " + string.Join(", ", picks.Select(i => "dm_" + i)) + $" · 관문 완료(지금까지) {PlaceSpawnGate.Completed}"));
            Check(() => picks.Count == 3, "시험 전제 — 사진 있는 우팡 큐브 3곳이 떠 있음");

            // 1) 느린 회선 — 사진을 다 받기 전엔 뜨지 않고(임시 화살표만), 뜨는 순간 이미 사진 면 · 서브사진 다 받음 · 0.2초 페이드인
            Do(() =>
            {
                if (picks.Count < 3) return;
                PlaceSpawnGate.SimulatedDelay = 1.5f;
                SubPhotoCache.SimulatedNetDelay = 1.0f;
                completedWas = PlaceSpawnGate.Completed;
                sgTracks.Clear();
                foreach (int id in picks)
                {
                    var k = Track(id, "");
                    foreach (var s in k.subs) CsForget(s);   // 디스크에서 지워 다시 받게
                    dm.DespawnFullObject(id.ToString());
                    sgTracks.Add(k);
                }
                StartSampler(SgSample);
                fm?.TriggerReallocation();
            });
            Wait(() => sgTracks.Count == 3 && sgTracks.All(k => k.appearAt >= 0f), 30f, "세 곳이 다시 뜸");
            Sleep(1.4f);
            Do(() =>
            {
                EditorApplication.update -= SgSample;
                PlaceSpawnGate.SimulatedDelay = 0f;
                SubPhotoCache.SimulatedNetDelay = 0f;
                foreach (var k in sgTracks)
                    log.Add($"  {k.label}: 뜬 때 {k.appearAt:F2}초 · 면 켜짐 {k.faceOn} · 사진 면 {k.facePhoto} · 기본 면 {k.faceDefault} · 서브사진 디스크 {k.subsDisk}/{k.subs.Count} · 받는 중 {k.subsBusy} · 임시 화살표 {k.indSeen} · 페이드 {Ramp(k.alpha)}");
                log.Add($"  관문 완료 +{PlaceSpawnGate.Completed - completedWas}");
            });
            Check(() => sgTracks.Count == 3 && sgTracks.All(k => k.appearAt >= 1.5f && k.appearAt < 20f), "느린 회선(곳마다 1.5초) — 사진을 받기 전엔 뜨지 않음 (1.5초 넘어서 뜸)");
            Check(() => sgTracks.Count == 3 && sgTracks.All(k => k.faceOn && k.facePhoto && !k.faceDefault), "뜨는 순간 이미 사진 면 — 기본 면으로 떴다가 바뀌지 않음");
            Check(() => sgTracks.Count == 3 && sgTracks.All(k => k.subsBusy == 0 || k.appearAt >= 9.5f), "서브사진을 다 받은 뒤에 뜸 (10초를 넘긴 곳만 예외)");
            Check(() => sgTracks.Count == 3 && sgTracks.All(k => k.indSeen), "받는 동안은 임시 화살표만 (오브젝트 없이)");
            Check(() => sgTracks.Count == 3 && sgTracks.All(k => FadeOk(k.alpha)), "나타날 때 0.2초 페이드인 (처음 0.35 이하 → 0.1~0.6초에 다 보임)");
            Check(() => PlaceSpawnGate.Completed - completedWas >= 3, "세 곳 모두 관문(사진 받기)을 거쳐 뜸");
            Shot("spawngate_cubes");

            // 2) 기기 규칙 — 앵커가 잡히기 전엔 사진을 붙여도 면을 켜지 않는다 · 보이게 되면 0.2초 페이드인
            //    보이게 되는 순간은 기기에도 있는 나침반 배치(CompassPlace)로 흉내 — 앵커 내부 값만 켜면 실제 AR 앵커가 없어 앵커가 다음 프레임에 다시 숨긴다
            CustomARGeospatialCreatorAnchor anc = null;
            bool hiddenAtBirth = false, photoAtBirth = false, hiddenLater = false;
            Settle(() => picks.Count == 3 ? "dm_" + picks[0] : null);
            Do(() =>
            {
                if (picks.Count < 3) return;
                PlaceSpawnGate.SimulatedDelay = 0.5f;
                CustomARGeospatialCreatorAnchor.EditorDeviceRule = true;
                sgTracks.Clear();
                var k = Track(picks[0], "(기기 규칙)");
                k.onAppear = go =>
                {
                    anc = AnchorOf(go);
                    HideLikeDevice(anc);
                };
                dm.DespawnFullObject(picks[0].ToString());
                sgTracks.Add(k);
                StartSampler(SgSample);
                fm?.TriggerReallocation();
            });
            Wait(() => sgTracks.Count == 1 && sgTracks[0].appearAt >= 0f, 15f, "기기 규칙 — 다시 뜸");
            Do(() =>
            {
                if (sgTracks.Count != 1) return;
                hiddenAtBirth = !sgTracks[0].faceOn;
                photoAtBirth = sgTracks[0].facePhoto;
            });
            Sleep(0.8f);
            Do(() =>
            {
                if (sgTracks.Count != 1) return;
                var idc = sgTracks[0].obj != null ? sgTracks[0].obj.GetComponentInChildren<ImageDisplayController>(true) : null;
                hiddenLater = idc != null && idc.cubeRenderer != null && !idc.cubeRenderer.enabled && sgTracks[0].alpha.Count == 0;
                if (anc != null) anc.CompassPlace(anc.transform.position, anc.transform.rotation);   // 보이게 되는 순간 (나침반 배치 — 제자리 그대로)
            });
            Sleep(1.4f);
            Do(() =>
            {
                EditorApplication.update -= SgSample;
                if (sgTracks.Count == 1)
                    log.Add($"  기기 규칙: 뜰 때 면 켜짐 {!hiddenAtBirth} · 사진 붙음 {photoAtBirth} · 0.8초 뒤까지 안 보임 {hiddenLater} · 앵커가 잡힌 뒤 페이드 {Ramp(sgTracks[0].alpha)}");
            });
            Check(() => hiddenAtBirth && photoAtBirth, "기기 규칙 — 뜰 때 사진은 이미 붙었지만 앵커가 잡히기 전이라 면을 켜지 않음");
            Check(() => hiddenLater, "앵커가 잡히기 전엔 계속 안 보임 (원점에 먼저 보이지 않게)");
            Check(() => sgTracks.Count == 1 && FadeOk(sgTracks[0].alpha), "앵커가 잡히면 사진 면이 0.2초 페이드인");
            Do(() =>
            {
                CustomARGeospatialCreatorAnchor.EditorDeviceRule = false;
                PlaceSpawnGate.SimulatedDelay = 0f;
                if (anc != null) anc.CompassRelease();   // 에디터 자리로 되돌린다
            });

            // 3) 메인 사진 404 — 관문은 기다리지 않는다 (예전처럼 기본 면 + 뒤에서 다시 받기)
            int badId = -1;
            string savedMain = null;
            Settle(() => picks.Count == 3 ? "dm_" + picks[1] : null);
            Do(() =>
            {
                if (picks.Count < 3) return;
                badId = picks[1];
                var p = dm.GetPlaceDataMap()[badId];
                savedMain = p.main_photo;
                p.main_photo = "uploads/p1_gate_no_such_photo.jpg";   // 없는 사진 (공개 GET → 404)
                sgTracks.Clear();
                sgTracks.Add(Track(badId, "(404)"));
                dm.DespawnFullObject(badId.ToString());
                StartSampler(SgSample);
                fm?.TriggerReallocation();
            });
            Wait(() => sgTracks.Count == 1 && sgTracks[0].appearAt >= 0f, 15f, "404 — 다시 뜸");
            Do(() =>
            {
                EditorApplication.update -= SgSample;
                if (sgTracks.Count == 1) log.Add($"  404: 뜬 때 {sgTracks[0].appearAt:F2}초 · 사진 면 {sgTracks[0].facePhoto}");
            });
            Check(() => sgTracks.Count == 1 && sgTracks[0].appearAt >= 0f && sgTracks[0].appearAt < 4f, "메인 사진이 없으면(404) 기다리지 않고 바로 뜸 (4초 안)");
            Settle(() => badId >= 0 ? "dm_" + badId : null);
            Do(() =>
            {
                if (badId < 0) return;
                dm.GetPlaceDataMap()[badId].main_photo = savedMain;   // 되돌린다
                dm.DespawnFullObject(badId.ToString());
                fm?.TriggerReallocation();
            });
            Wait(() => badId < 0 || (DmObj(badId) != null && DmObj(badId).activeInHierarchy), 15f, "사진 주소를 되돌려 다시 뜸");
            Check(() => badId < 0 || (dm.GetPlaceDataMap()[badId].main_photo == savedMain && DmObj(badId) != null), "404 시험 뒤 사진 주소를 되돌림 · 다시 뜸");

            // 4) 관광공사 — 같은 규칙 (반경 안에 사진 있는 곳이 떠 있으면)
            string tourId = null;
            Do(() =>
            {
                var tm = TourAPIManager.Instance;
                var tmap = tm != null ? typeof(TourAPIManager).GetField("placeDataMap", NP)?.GetValue(tm) as Dictionary<string, TourPlaceData> : null;
                if (tm == null || tmap == null || Camera.main == null) { log.Add("  관광공사: 매니저 없음 — 건너뜀"); return; }
                var cam = Camera.main.transform.position;
                float best = float.MaxValue;
                foreach (var kv in tm.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy || !tmap.TryGetValue(kv.Key, out var tp) || tp == null || string.IsNullOrEmpty(tp.firstimage)) continue;
                    if (fm != null && !fm.WantsFull("tour_" + kv.Key)) continue;
                    float d = (kv.Value.transform.position - cam).sqrMagnitude;
                    if (d < best) { best = d; tourId = kv.Key; }
                }
                if (tourId == null) { log.Add("  관광공사: 사진 있는 곳이 떠 있지 않음 — 건너뜀"); return; }
                var t = tmap[tourId];
                string tid = tourId;
                var k = new SgTrack
                {
                    label = "tour_" + tid,
                    main = t.firstimage,
                    subs = t.imageUrls != null ? new List<string>(t.imageUrls) : new List<string>(),
                    find = () => TourAPIManager.Instance != null && TourAPIManager.Instance.GetSpawnedObjects().TryGetValue(tid, out var g) ? g : null,
                    indicator = () => TourAPIManager.Instance != null
                        && typeof(TourAPIManager).GetField("indicatorOnlyObjects", NP)?.GetValue(TourAPIManager.Instance) is Dictionary<string, GameObject> m && m.ContainsKey(tid),
                };
                foreach (var s in k.subs) CsForget(s);
                PlaceSpawnGate.SimulatedDelay = 1.0f;
                sgTracks.Clear();
                sgTracks.Add(k);
                tm.DespawnFullObject(tid);
                StartSampler(SgSample);
                fm?.TriggerReallocation();
            });
            Wait(() => tourId == null || (sgTracks.Count == 1 && sgTracks[0].appearAt >= 0f), 20f, "관광공사 — 다시 뜸");
            Sleep(1.4f);
            Do(() =>
            {
                EditorApplication.update -= SgSample;
                PlaceSpawnGate.SimulatedDelay = 0f;
                if (tourId != null && sgTracks.Count == 1)
                {
                    var k = sgTracks[0];
                    log.Add($"  {k.label}: 뜬 때 {k.appearAt:F2}초 · 면 켜짐 {k.faceOn} · 사진 면 {k.facePhoto} · 기본 면 {k.faceDefault} · 상세 사진 {k.subs.Count}장(받는 중 {k.subsBusy}) · 임시 화살표 {k.indSeen} · 페이드 {Ramp(k.alpha)}");
                }
            });
            Check(() => tourId == null || (sgTracks.Count == 1 && sgTracks[0].appearAt >= 1.0f && sgTracks[0].faceOn && sgTracks[0].facePhoto && !sgTracks[0].faceDefault
                                           && (sgTracks[0].subsBusy == 0 || sgTracks[0].appearAt >= 9.5f) && FadeOk(sgTracks[0].alpha)),
                "관광공사도 사진을 받은 뒤 · 뜨는 순간 사진 면 · 0.2초 페이드인 (떠 있는 곳이 없으면 건너뜀)");

            // 5) 장소 3D (운영 시바견 · 공개 GET) — 스피너 없이 · 0.2초 페이드인 · 끝나면 그대로 보임
            bool was3D = true;
            PlaceData seed = null;
            UnityWebRequest req = null;
            void Set3D(bool on)
            {
                var tg = fm != null ? typeof(FilterManager).GetField("object3DToggle", NP)?.GetValue(fm) as Toggle : null;
                if (tg != null) tg.isOn = on;
            }
            Do(() =>
            {
                was3D = PlayerPrefs.GetInt("Filter_Object3D_V2", 1) == 1;
                if (!was3D) Set3D(true);
                req = UnityWebRequest.Get(ApiConfig.LOCATIONS_DETAIL + "?ids=" + Dog);   // 공개 GET · 진짜 번호
                req.timeout = 15;
                req.SendWebRequest();
            });
            Wait(() => req != null && req.isDone, 20f, "시바견 상세 (공개 GET)");
            Do(() =>
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { seed = JsonConvert.DeserializeObject<List<PlaceData>>(req.downloadHandler.text)?.Find(p => p != null && p.id == Dog); }
                    catch (Exception) { seed = null; }
                }
                req.Dispose();
                log.Add("  시바견 상세: " + (seed == null ? "못 받음" : $"분류 {seed.category} · 모델 주소 {(string.IsNullOrEmpty(seed.model_url) ? "없음" : "있음")}"));
            });
            Settle(() => seed != null && DmObj(Dog) != null ? "dm_" + Dog : null);
            Do(() =>
            {
                if (seed == null) return;
                sgSpinnerEver = sgRevealEver = false;
                sgDogVisibleAt = -1f;
                sgDogAlpha.Clear();
                sgDogFind = () => DmObj(Dog);
                StartSampler(SgGlbSample);
                if (DmObj(Dog) != null)
                {
                    log.Add("  시바견이 이미 떠 있어 내렸다가 다시 띄움 (나타나는 순간을 재려고)");
                    dm.DespawnFullObject(Dog.ToString());
                }
                else VirtualLocation.Instance.SetCoordinates((float)(seed.latitude + 0.0003), (float)seed.longitude);   // 시바견 바로 옆 (기록하지 않는다)
                fm?.TriggerReallocation();
            });
            Wait(() => seed == null || (sgDogVisibleAt >= 0f && Time.realtimeSinceStartup - sgT0 - sgDogVisibleAt > 3.6f), 90f, "시바견 3D 가 나타나 3초 지남");
            Do(() =>
            {
                EditorApplication.update -= SgGlbSample;
                var rs = SgModelRenderers(DmObj(Dog));
                SgGlbAlpha(rs, out int stale);
                log.Add($"  3D: 나타난 때 {sgDogVisibleAt:F2}초 · 스피너 {sgSpinnerEver} · 드러내기 {sgRevealEver} · 페이드 {Ramp(sgDogAlpha)} · 멈춘 투명도 {stale} · 끝 투명도 {(sgDogAlpha.Count > 0 ? sgDogAlpha[sgDogAlpha.Count - 1].y : -1f):F2}");
            });
            Check(() => seed != null && sgDogVisibleAt >= 0f, "시험 전제 — 시바견 3D 가 떴다");
            Check(() => seed != null && !sgSpinnerEver, "장소 3D — 받는 동안 스피너 없음");
            Check(() => seed != null && sgRevealEver && FadeOk(sgDogAlpha), "장소 3D — 0.2초 페이드인 (처음 0.35 이하 → 0.1~0.6초에 다 보임)");
            Check(() =>
            {
                if (seed == null || sgDogAlpha.Count == 0) return false;
                SgGlbAlpha(SgModelRenderers(DmObj(Dog)), out int stale);
                return sgDogAlpha[sgDogAlpha.Count - 1].y >= 0.95f && stale == 0;
            }, "장소 3D — 나타난 뒤 그대로 보임 (투명도가 중간에 멈추지 않음)");

            // 5-2) 기기 규칙 — 모델을 다 받아도 앵커가 잡히기 전엔 숨김 · 잡히면 0.2초 (다시 띄운다 — 풀로 돌아가면 모델을 지우고 다시 받는다)
            CustomARGeospatialCreatorAnchor dogAnc = null;
            bool dogHidden = false, dogLoadedHidden = false;
            Settle(() => seed != null && DmObj(Dog) != null ? "dm_" + Dog : null);
            Do(() =>
            {
                if (seed == null || DmObj(Dog) == null) return;
                CustomARGeospatialCreatorAnchor.EditorDeviceRule = true;
                sgSpinnerEver = sgRevealEver = false;
                sgDogVisibleAt = -1f;
                sgDogAlpha.Clear();
                StartSampler(SgGlbSample);   // 받는 동안 스피너 · 미리 보임이 없는지도 본다
                dm.DespawnFullObject(Dog.ToString());
                fm?.TriggerReallocation();
            });
            Wait(() => seed == null || (DmObj(Dog) != null && DmObj(Dog).activeInHierarchy), 20f, "시바견이 다시 뜸 (기기 규칙)");
            Do(() =>
            {
                dogAnc = AnchorOf(DmObj(Dog));
                HideLikeDevice(dogAnc);
            });
            Wait(() =>
            {
                if (seed == null) return true;
                var go = DmObj(Dog);
                var l = go != null ? go.GetComponent<GLBModelLoader>() : null;
                bool loading = DmField<HashSet<int>>("currentlyLoadingGLB") is HashSet<int> h && h.Contains(Dog);
                return l != null && l.IsModelLoaded && !loading;
            }, 40f, "모델 다시 받기 끝 (앵커는 아직)");
            Sleep(1.0f);   // 거리 페이드가 몇 번 지나가게 (숨긴 동안 넣은 색이 남는지 보려고)
            Do(() =>
            {
                if (seed == null) return;
                var go = DmObj(Dog);
                var rs = SgModelRenderers(go);
                dogLoadedHidden = go != null && go.GetComponent<GLBModelLoader>() != null && go.GetComponent<GLBModelLoader>().IsModelLoaded;
                dogHidden = rs.Count > 0 && rs.All(r => r == null || !r.enabled || !r.gameObject.activeInHierarchy) && sgDogVisibleAt < 0f;
                sgDogVisibleAt = -1f;
                sgDogAlpha.Clear();
                StartSampler(SgGlbSample);
                if (dogAnc != null) dogAnc.CompassPlace(dogAnc.transform.position, dogAnc.transform.rotation);   // 보이게 되는 순간 (나침반 배치 — 제자리 그대로)
            });
            Sleep(3.8f);
            Do(() =>
            {
                EditorApplication.update -= SgGlbSample;
                if (seed == null) return;
                SgGlbAlpha(SgModelRenderers(DmObj(Dog)), out int stale);
                log.Add($"  3D 기기 규칙: 받은 뒤 숨김 {dogHidden} · 스피너 {sgSpinnerEver} · 드러내기 {sgRevealEver} · 앵커가 잡힌 뒤 페이드 {Ramp(sgDogAlpha)} · 멈춘 투명도 {stale}");
            });
            Check(() => seed != null && dogLoadedHidden && dogHidden, "장소 3D 기기 규칙 — 모델을 다 받아도 앵커가 잡히기 전엔 숨김");
            Check(() => seed != null && !sgSpinnerEver && sgRevealEver && FadeOk(sgDogAlpha), "장소 3D 기기 규칙 — 앵커가 잡히면 0.2초 페이드인 (스피너 없음)");
            Check(() =>
            {
                if (seed == null || sgDogAlpha.Count == 0) return false;
                SgGlbAlpha(SgModelRenderers(DmObj(Dog)), out int stale);
                return sgDogAlpha[sgDogAlpha.Count - 1].y >= 0.95f && stale == 0;
            }, "장소 3D 기기 규칙 — 나타난 뒤 그대로 보임 (숨긴 동안 넣은 색이 남지 않음)");

            // 끝 — 시험 고리를 모두 되돌린다
            Do(() =>
            {
                EditorApplication.update -= SgSample;
                EditorApplication.update -= SgGlbSample;
                Application.logMessageReceived -= SgLog;
                PlaceSpawnGate.SimulatedDelay = 0f;
                SubPhotoCache.SimulatedNetDelay = 0f;
                CustomARGeospatialCreatorAnchor.EditorDeviceRule = false;
                if (dogAnc != null) dogAnc.CompassRelease();   // 에디터 자리로 되돌린다
                sgDogFind = null;
                if ((PlayerPrefs.GetInt("Filter_Object3D_V2", 1) == 1) != was3D) Set3D(was3D);
                log.Add($"  관문 · 만들기 오류 {sgErrors.Count}건 · 그 밖의 예외 {sgOtherErrors}건");
                foreach (var e in sgErrors) log.Add("    " + e);
            });
            Check(() => sgErrors.Count == 0, "관문 · 사진 받은 뒤 만들기에서 오류 · 예외 없음");
        }

        // 프레임마다 (EditorApplication.update) — 뜨기 전엔 임시 화살표를 보고, 뜬 순간의 면 · 서브사진을 적고, 면이 켜진 뒤 1.2초 동안 투명도를 잰다
        private static void SgSample()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= SgSample; return; }
            float t = Time.realtimeSinceStartup - sgT0;
            foreach (var k in sgTracks)
            {
                if (k.appearAt < 0f)
                {
                    if (k.indicator != null && k.indicator()) k.indSeen = true;
                    var go = k.find?.Invoke();
                    if (go == null || !go.activeInHierarchy) continue;
                    k.appearAt = t;
                    k.obj = go;
                    SgFace(go.GetComponentInChildren<ImageDisplayController>(true), k.main, out k.faceOn, out k.facePhoto, out k.faceDefault);
                    int disk = 0, busy = 0;
                    foreach (var s in k.subs)
                    {
                        if (CsWhere(s) != null) disk++;
                        if (SubPhotoCache.Busy(s)) busy++;
                    }
                    k.subsDisk = disk;
                    k.subsBusy = busy;
                    k.onAppear?.Invoke(go);
                }
                if (k.obj == null || (k.visibleAt >= 0f && t - k.visibleAt > 1.2f)) continue;
                var idc = k.obj.GetComponentInChildren<ImageDisplayController>(true);
                var face = idc != null ? idc.cubeRenderer : null;
                if (face == null || !face.enabled) continue;
                if (k.visibleAt < 0f) k.visibleAt = t;
                k.alpha.Add(new Vector2(t - k.visibleAt, SgAlpha(k.obj, face)));
            }
        }

        // 큐브 앞면 — 켜졌나 · 받은 사진(주소까지 같은)이 붙었나 · 프리팹 기본 면인가
        private static void SgFace(ImageDisplayController idc, string main, out bool on, out bool photo, out bool dflt)
        {
            on = photo = dflt = false;
            if (idc == null || idc.cubeRenderer == null) return;
            on = idc.cubeRenderer.enabled;
            var m = idc.cubeRenderer.sharedMaterial;
            Texture cur = m == null ? null : m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            var tex = typeof(ImageDisplayController).GetField("baseMapTexture", DsF)?.GetValue(idc) as Texture2D;
            var url = typeof(ImageDisplayController).GetField("baseMapUrl", DsF)?.GetValue(idc) as string;
            var def = typeof(ImageDisplayController).GetField("defaultFaceTexture", DsF)?.GetValue(idc) as Texture;
            photo = tex != null && cur == tex && url == main;
            dflt = cur == null || cur == def;
        }

        // 큐브 투명도 — 거리 페이드가 넣는 _GlobalAlpha (재질 값에 대한 비율). 면 렌더러부터, 없으면 같은 오브젝트의 다른 렌더러
        private static float SgAlpha(GameObject obj, Renderer face)
        {
            if (sgMpb == null) sgMpb = new MaterialPropertyBlock();
            int ga = Shader.PropertyToID("_GlobalAlpha");
            var list = new List<Renderer> { face };
            if (obj != null) list.AddRange(obj.GetComponentsInChildren<Renderer>(true));
            foreach (var r in list)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.HasProperty(ga)) continue;
                    r.GetPropertyBlock(sgMpb, i);
                    float own = m.GetFloat(ga);
                    float v = sgMpb.HasFloat(ga) ? sgMpb.GetFloat(ga) : own;
                    return own > 0.0001f ? v / own : v;
                }
            }
            return -1f;
        }

        // 표본을 마지막 값으로 나눠 — 처음 값 · 0.95 에 닿은 때 · 중간 프레임 수 · 한 프레임 최대 증가
        private static (float first, float reach, int mids, float jump, int n) SgRamp(List<Vector2> a)
        {
            if (a == null || a.Count == 0) return (-1f, -1f, 0, 1f, 0);
            float last = a[a.Count - 1].y;
            if (last <= 0.0001f) return (-1f, -1f, 0, 1f, a.Count);
            float first = a[0].y / last, reach = -1f, jump = 0f, prev = first;
            int mids = 0;
            foreach (var v in a)
            {
                float y = v.y / last;
                if (y > 0.05f && y < 0.95f) mids++;
                if (reach < 0f && y >= 0.95f) reach = v.x;
                jump = Mathf.Max(jump, y - prev);
                prev = y;
            }
            return (first, reach, mids, jump, a.Count);
        }

        // 장소 3D 모델 렌더러 (GLBContainer 아래 · 스피너 제외)
        private static List<Renderer> SgModelRenderers(GameObject root)
        {
            var list = new List<Renderer>();
            if (root == null) return list;
            Transform box = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GLBContainer");
            foreach (var r in (box != null ? box : root.transform).GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                bool spinner = false;
                for (var t = r.transform; t != null && t != root.transform; t = t.parent)
                    if (t.name.StartsWith("GLB_LoadingSpinner")) { spinner = true; break; }
                if (!spinner) list.Add(r);
            }
            return list;
        }

        // 보이는 투명도 — 프로퍼티 블록(거리 페이드가 색을 넣는다)이 재질보다 앞선다 · 불투명 재질은 알파가 보이지 않는다(1).
        // stale: 투명 재질인데 블록에 남은 알파가 재질보다 낮은 것 (숨긴 동안 넣은 색이 남음)
        private static float SgGlbAlpha(List<Renderer> rs, out int stale)
        {
            stale = 0;
            if (sgMpb == null) sgMpb = new MaterialPropertyBlock();
            int bc = Shader.PropertyToID("_BaseColor"), cc = Shader.PropertyToID("_Color");
            float min = 1f;
            bool any = false;
            foreach (var r in rs)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    int prop = m.HasProperty(bc) ? bc : m.HasProperty(cc) ? cc : 0;
                    if (prop == 0) continue;
                    any = true;
                    if (m.renderQueue <= (int)UnityEngine.Rendering.RenderQueue.GeometryLast) continue;
                    r.GetPropertyBlock(sgMpb, i);
                    float own = m.GetColor(prop).a;
                    float a = sgMpb.HasColor(prop) ? sgMpb.GetColor(prop).a : own;
                    if (a < own - 0.05f) stale++;
                    min = Mathf.Min(min, a);
                }
            }
            return any ? min : -1f;
        }

        // 장소 3D — 스피너 · 드러내기 · 모델이 켜진 뒤 3.5초 동안 보이는 투명도
        private static void SgGlbSample()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= SgGlbSample; return; }
            float t = Time.realtimeSinceStartup - sgT0;
            var go = sgDogFind?.Invoke();
            if (go == null || !go.activeInHierarchy) return;
            foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("GLB_LoadingSpinner") && tr.gameObject.activeInHierarchy) { sgSpinnerEver = true; break; }
            var fx = go.GetComponentInChildren<GLBSpawnEffect>(true);
            if (fx != null && fx.PlaceRevealing) sgRevealEver = true;
            var rs = SgModelRenderers(go);
            if (!rs.Any(r => r != null && r.enabled && r.gameObject.activeInHierarchy)) return;   // 만드는 동안 꺼 둔 칸 아래는 안 보인다
            if (sgDogVisibleAt < 0f) sgDogVisibleAt = t;
            if (t - sgDogVisibleAt <= 3.5f) sgDogAlpha.Add(new Vector2(t - sgDogVisibleAt, SgGlbAlpha(rs, out _)));
        }

        // 관문 · 만들기 오류만 모은다 (그 밖의 예외는 개수만) — 주소의 키 · 긴 소수(좌표)는 가린다
        private static void SgLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            msg = msg ?? "";
            stack = stack ?? "";
            bool ours = msg.Contains("[PlaceSpawnGate]") || msg.Contains("CreateObjectFromData 예외")
                        || (type == LogType.Exception && (stack.Contains("PlaceSpawnGate") || stack.Contains("SpawnPhotoReady") || stack.Contains("PhotosReady")
                                                          || stack.Contains("SetBaseMapTexture") || stack.Contains("RevealWhenShown") || stack.Contains("PlaceRevealRoutine")));
            if (!ours)
            {
                if (type == LogType.Exception) sgOtherErrors++;
                return;
            }
            string m = Regex.Replace(msg, @"(?i)(servicekey|key|token)=[^&\s]+", "$1=***");
            m = Regex.Replace(m, @"\d+\.\d{4,}", "#.#");
            if (sgErrors.Count < 20) sgErrors.Add(type + ": " + (m.Length > 160 ? m.Substring(0, 160) : m));
        }
    }
}
