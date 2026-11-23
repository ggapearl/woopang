using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 L6 — 대표님 「3D 모델 — 다운로드 단계 없이 기본으로 바로 보이게 ('시바견' 등). 앞으로 큰 용량은 안 쓸 예정」.
    ///  · animglb : 운영 시바견(dm_34837 · 서버엔 분류 anim · model_type cube)으로 — 처음부터 3D(GLB)로 뜬다 · 받기 창(DanceAnimController)이 한 번도 안 뜬다 ·
    ///    두 번 누르면 정보 창 · 받다 실패하면 큐브(사진 상자)로 내려가고 다시 3D 로 올려 받기를 되풀이하지 않는다 ·
    ///    같은 자료가 다시 와도 큐브 그대로 · 모델 주소가 바뀌어 오면 새 모델 3D 로 · 풀이 섞이지 않는다 (큐브 풀엔 큐브 · 3D 풀엔 3D).
    ///    서버에 쓰는 것 없음 — 시바견 상세 · 모델 · 사진은 공개 GET (진짜 번호). 받기 실패는 닫힌 자리 127.0.0.1:9.
    ///    시바견 자리는 운영 상세에서 받아 그 옆으로 옮긴다 (좌표 · 거리는 기록에 남기지 않는다). 끝나면 3D 칩을 처음대로
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanAnimGlb()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance, NS = BindingFlags.NonPublic | BindingFlags.Static;
            const int Dog = 34837;
            const string Broken = "http://127.0.0.1:9/l6_broken.glb";   // 받기 실패 흉내 — 닫힌 자리라 바로 실패
            DataManager dm = null;
            FilterManager fm = null;
            PlaceData seed = null;
            string goodUrl = null;
            bool was3D = true, panelEver = false, hadCtrl = false;
            UnityWebRequest req = null;
            DoubleTap3D dt = null;

            T Field<T>(string name) where T : class => dm != null ? typeof(DataManager).GetField(name, NP)?.GetValue(dm) as T : null;
            GameObject Obj() => Field<Dictionary<int, GameObject>>("spawnedObjects") is Dictionary<int, GameObject> m && m.TryGetValue(Dog, out var g) ? g : null;
            PlaceData Data() => Field<Dictionary<int, PlaceData>>("placeDataMap") is Dictionary<int, PlaceData> m && m.TryGetValue(Dog, out var p) ? p : null;
            bool Loading() => Field<HashSet<int>>("currentlyLoadingGLB") is HashSet<int> l && l.Contains(Dog);
            string Kind()
            {
                var g = Obj();
                return g == null ? "없음" : g.name.EndsWith("_custom") ? "3D" : g.name.EndsWith("_cube") ? "큐브" : g.name;
            }
            bool Loaded()
            {
                var l = Obj() != null ? Obj().GetComponent<GLBModelLoader>() : null;
                return l != null && l.IsModelLoaded;
            }
            bool PanelOpen()
            {
                var c = UnityEngine.Object.FindAnyObjectByType<DanceAnimController>(FindObjectsInactive.Include);
                bool open = c != null && c.confirmPanel != null && c.confirmPanel.activeInHierarchy;
                if (open) panelEver = true;
                return open;
            }
            bool InfoOpen() => dt != null && dt.fullscreenCanvasGroup != null && dt.fullscreenCanvasGroup.gameObject.activeSelf;
            bool IsGlbObj(GameObject o) => o.GetComponentsInChildren<Transform>(true).Any(t => t.name == "GLBContainer");
            string PoolsMixed()
            {
                var cubes = Field<Queue<GameObject>>("cubeObjectPool");
                var glbs = Field<Queue<GameObject>>("glbObjectPool");
                if (cubes == null || glbs == null) return "풀을 못 읽음";
                int badCube = cubes.Count(o => o != null && IsGlbObj(o)), badGlb = glbs.Count(o => o != null && !IsGlbObj(o));
                log.Add($"  풀: 큐브 {cubes.Count}개 (3D 섞임 {badCube}) · 3D {glbs.Count}개 (큐브 섞임 {badGlb})");
                return badCube == 0 && badGlb == 0 ? "" : "섞임";
            }
            void Set3D(bool on)
            {
                var t = fm != null ? typeof(FilterManager).GetField("object3DToggle", NP)?.GetValue(fm) as Toggle : null;
                if (t != null) t.isOn = on;
            }
            PlaceData ServerCopy(string url)
            {
                // 서버가 보내는 모양 그대로 (춤 모델은 model_type cube) — 다시 묻기 · 고친 장소 응답 흉내
                var c = JsonConvert.DeserializeObject<PlaceData>(JsonConvert.SerializeObject(Data()));
                c.model_type = "cube";
                c.original_model_type = null;
                if (url != null) c.model_url = url;
                return c;
            }

            // 0) 규칙 — 받은 자료를 3D 로 바꾸는 것은 춤 모델(anim)이고 모델 주소가 있을 때만
            Check(() =>
            {
                var adopt = typeof(DataManager).GetMethod("Adopt", NS);
                if (adopt == null) return false;
                string A(string cat, string type, string url)
                {
                    var p = new PlaceData { category = cat, model_type = type, model_url = url };
                    adopt.Invoke(null, new object[] { p });
                    return p.model_type;
                }
                return A("anim", "cube", "uploads/x/shiba.glb") == "custom" && A("anim", "cube", "") == "cube" && A("anim", "cube", null) == "cube"
                       && A("cafe", "cube", "uploads/x/a.glb") == "cube" && A("anim", "custom", "u.glb") == "custom" && A("", "custom", "u.glb") == "custom";
            }, "규칙 — 춤 모델(anim) + 모델 주소 → 3D · 주소 없으면 큐브 그대로 · 다른 분류 · 이미 3D 는 손대지 않음");

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                dm = UnityEngine.Object.FindAnyObjectByType<DataManager>(FindObjectsInactive.Include);
                fm = UnityEngine.Object.FindAnyObjectByType<FilterManager>(FindObjectsInactive.Include);
                var c = UnityEngine.Object.FindAnyObjectByType<DanceAnimController>(FindObjectsInactive.Include);
                hadCtrl = c != null && c.confirmPanel != null;
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
                log.Add("  받기 창(DanceAnimController) 장면에 있음: " + hadCtrl + " · 시바견 상세: "
                        + (seed == null ? "못 받음" : $"분류 {seed.category} · 서버 model_type {seed.model_type} · 모델 주소 {(string.IsNullOrEmpty(seed.model_url) ? "없음" : "있음")}"));
                if (seed == null) return;
                goodUrl = seed.model_url;
                VirtualLocation.Instance.SetCoordinates((float)(seed.latitude + 0.0003), (float)seed.longitude);   // 시바견 바로 옆
                fm?.TriggerReallocation();
            });
            Check(() => seed != null && seed.category == "anim" && seed.model_type == "cube" && !string.IsNullOrEmpty(seed.model_url),
                "시험 전제 — 시바견은 서버에 분류 anim · model_type cube · 모델 주소 있음 (예전엔 이래서 큐브로 떴다)");

            // 1) 처음부터 3D — 큐브로 떴다가 바뀌는 게 아니라 처음 뜬 오브젝트가 3D
            Wait(() => { PanelOpen(); return Obj() != null; }, 60f, "시바견이 AR 에 뜸");
            Check(() =>
            {
                log.Add($"  처음 뜬 모양: {Kind()} · model_type {Data()?.model_type} · 원래 값 {Data()?.original_model_type}");
                return Kind() == "3D" && Data() != null && Data().model_type == "custom";
            }, "처음부터 3D(GLB)로 뜸 — 큐브(사진 상자)가 아니라");
            Wait(() => { PanelOpen(); return Loaded(); }, 40f, "모델 받기 끝");
            Check(() => Loaded() && !panelEver, "받기 창 없이 모델이 다 떴다 (운영과 같은 파일)");
            Sleep(1.5f);
            Shot("animglb_ar");

            // 2) 두 번 누르기 — 받기 창이 아니라 다른 장소처럼 정보 창
            Do(() =>
            {
                dt = Obj() != null ? Obj().GetComponentInChildren<DoubleTap3D>(true) : null;
                if (dt == null) return;
                typeof(DoubleTap3D).GetField("isCooldown", NP)?.SetValue(dt, false);
                dt.DoubleTapFromModal();
            });
            Sleep(1.0f);
            Check(() =>
            {
                bool info = InfoOpen(), panel = PanelOpen();
                log.Add($"  두 번 누름: 정보 창 {info} · 받기 창 {panel}");
                return info && !panel;
            }, "두 번 누르면 정보 창 (받기 창 아님)");
            Shot("animglb_info");
            Do(() => { if (InfoOpen()) typeof(DoubleTap3D).GetMethod("CloseFullscreen", NP)?.Invoke(dt, null); });
            Sleep(0.8f);
            Check(() => !InfoOpen(), "정보 창 닫힘");

            // 3) 받다 실패하면 — 큐브로 내려가고, 다시 3D 로 올려 받기를 되풀이하지 않는다 (Adopt 는 받은 자료에만 · 만들 때는 안 부른다)
            Do(() =>
            {
                dm.DespawnFullObject(Dog.ToString());   // 떠 있는 3D 를 3D 풀로 — 주소를 바꾸기 전에
                Data().model_url = Broken;
                dm.SpawnFullObject(Dog.ToString());     // 같은 프레임 — 배분이 끼어들 틈 없이
            });
            Wait(() => { PanelOpen(); return Kind() == "큐브"; }, 30f, "받기 실패 → 큐브");
            Check(() => Kind() == "큐브" && Data().model_type == "cube", "받기 실패 → 사진 상자(큐브)로");
            Shot("animglb_cube");
            Sleep(6f);
            Check(() =>
            {
                log.Add($"  6초 뒤: {Kind()} · 받는 중 {Loading()}");
                return Kind() == "큐브" && !Loading();
            }, "큐브로 내린 뒤 다시 3D 로 올려 받기를 되풀이하지 않음");

            // 4) 같은 자료가 다시 와도(다시 묻기 · 고친 장소 — 서버는 cube 로 보낸다) 내린 큐브는 그대로
            Do(() => dm.ApplyEditedPlace(ServerCopy(null), false));
            Sleep(1.5f);
            Check(() =>
            {
                log.Add($"  같은 자료 다시 옴: {Kind()} · model_type {Data().model_type} · 받는 중 {Loading()}");
                return Kind() == "큐브" && Data().model_type == "cube" && !Loading();
            }, "같은 자료가 다시 와도 큐브 그대로 (3D 로 올려 다시 받지 않음)");

            // 5) 모델 주소가 바뀌어 오면(관리자가 모델을 바꿈) 새 모델 3D 로 — 원래 주소로 돌려놓기도 겸한다
            Do(() => dm.ApplyEditedPlace(ServerCopy(goodUrl), false));
            Wait(() => { PanelOpen(); return Kind() == "3D" && Loaded(); }, 40f, "모델 주소가 바뀌어 옴 → 3D");
            Check(() => Kind() == "3D" && Loaded() && Data().model_url == goodUrl && Data().model_type == "custom",
                "모델 주소가 바뀌어 오면 큐브를 내리고 새 모델 3D 로 (원래 주소로 돌려놓음)");
            Check(() => PoolsMixed() == "", "풀이 섞이지 않음 — 큐브 풀엔 큐브만 · 3D 풀엔 3D 만");
            Check(() => !panelEver, "시험 내내 받기 창이 한 번도 안 뜸");

            Do(() => { if ((PlayerPrefs.GetInt("Filter_Object3D_V2", 1) == 1) != was3D) Set3D(was3D); });
        }
    }
}
