using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-23 Tripo(3D 생성 회사) 답장에 붙일 「3D 오브젝트가 실제로 반영된 우팡 앱 화면」 — 대표님 「너가 앱에서 스크린샷 작업해서 효딩쓰에 전달해」.
    ///  · tripo : 스토어 사진과 같은 방식 — 카메라 영상 자리에 도시 배경 사진, 화면 · 3D 는 앱이 그린 그대로 (로그아웃 상태 · 개인 장소 · 주소줄은 촬영 때만 끔)
    ///    ① 운영 3D 장소(시바견 · 공개 GET) 앞 AR — 3D 가 아닌 오브젝트는 촬영 때만 끈다 · 한 번 눌러 움직이는 모습
    ///       편집기에선 3D 가 DB 높이에 떠 있어 올려다보면 하늘 날씨판이 열린다 → 카메라를 시바견 눈높이로 옮겨 수평으로 본다 (폰에서 땅 위에 선 모습)
    ///       계획 파일 넷째 줄 = 「거리/방향[/숙임]」 목록(쉼표) — 있으면 AR 만 찍는다 (방향 = 시바견에서 본 내 쪽, 북 0 · 동 90) · 「list」 가 있으면 근처 목록도
    ///       근처 목록은 3D 가 아닌 장소(집 이름 같은 개인 글 포함)를 촬영 때만 숨긴다 (HiddenPlaces — 메모리에만 · 끝에 되살린다)
    ///    ② 보물찾기 3D 보물상자 (스토어 6번과 같은 길 — 프리팹을 카메라 앞에)
    ///    ③ 장소 추가 ▸ AI 작업 ▸ 3D 캐릭터 (요청사항 한 줄 → 폰 안에서 만든 저폴리 캐릭터)
    ///    서버에 쓰는 것 없음 · 로그엔 개수 · 크기만 (이름 · 자리 · 거리는 남기지 않는다) · 끝나면 3D 칩 · 목록 거리 · 숨긴 장소 · 입력 설정을 처음대로
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanTripo(string spec)
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            const int Dog = 34837;
            const string Bc = "ARPreviewPanel/MessageContainer/ButtonContainer/", Op = "ARPreviewPanel/MessageContainer/AiOptions0926/";
            DataManager dm = null;
            FilterManager fm = null;
            PlaceData seed = null;
            UnityWebRequest req = null;
            bool was3D = true, hadDist = false;
            float distWas = 0f;
            var hid = new List<string>();
            ARPreviewController prev = null;
            R0926AiWorkPanel panel = null;
            CubeUploadManager cum = null;

            GameObject Obj() => dm != null && typeof(DataManager).GetField("spawnedObjects", NP)?.GetValue(dm) is Dictionary<int, GameObject> m && m.TryGetValue(Dog, out var g) ? g : null;
            bool Loaded()
            {
                var o = Obj();
                var l = o != null ? o.GetComponent<GLBModelLoader>() : null;
                return l != null && l.IsModelLoaded && o.name.EndsWith("_custom");
            }
            Collider DogCol() => Obj() != null ? Obj().GetComponentInChildren<DoubleTap3D>(true)?.GetComponent<Collider>() : null;
            void Set3D(bool on)
            {
                var t = fm != null ? typeof(FilterManager).GetField("object3DToggle", NP)?.GetValue(fm) as Toggle : null;
                if (t != null) t.isOn = on;
            }
            // 시바견 옆 dNorth · dEast 미터에 선다 (좌표는 기록에 남기지 않는다)
            void Stand(double dNorth, double dEast)
            {
                double lat = seed.latitude + dNorth / 111320.0;
                double lon = seed.longitude + dEast / (111320.0 * Math.Cos(seed.latitude * Math.PI / 180.0));
                VirtualLocation.Instance.SetCoordinates((float)lat, (float)lon);
                fm?.TriggerReallocation();
            }
            // 3D 가 아닌 오브젝트 · 사람은 촬영 때만 끈다 (앱 데이터는 그대로)
            int HideNon3D()
            {
                int n = 0;
                foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude))
                    if (t.GetComponent<GLBModelLoader>() == null && t.GetComponentInParent<GLBModelLoader>() == null) { t.gameObject.SetActive(false); n++; }
                return n;
            }
            bool ModelBounds(out Bounds b)
            {
                b = default;
                var m = Obj()?.GetComponent<GLBModelLoader>()?.GetLoadedModel();
                if (m == null) return false;
                bool any = false;
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                return any;
            }
            // 시바견에서 bearing 쪽으로 dist 미터 떨어진 눈높이(모델 바닥 + 1.4m)에서 수평으로 본다 — pitch 음수 = 살짝 올려다봄
            void View(float dist, float bearing, float pitch)
            {
                var cam = Camera.main;
                if (cam == null || !ModelBounds(out var b)) { log.Add("  볼 3D 없음"); return; }
                float r = bearing * Mathf.Deg2Rad;
                Vector3 off = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)) * dist;
                foreach (var bh in cam.GetComponents<Behaviour>()) if (bh.GetType().Name.Contains("PoseDriver")) bh.enabled = false;
                cam.transform.position = new Vector3(b.center.x, b.min.y + 1.4f, b.center.z) + off;
                cam.transform.rotation = Quaternion.LookRotation(-off.normalized, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);
                Stand(off.z, off.x);   // 화면의 거리 표시도 같은 자리로
            }
            bool SkyOpen()
            {
                var sw = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Exclude);
                return sw != null && typeof(R0926SkyWeather).GetField("skyShow", NP)?.GetValue(sw) is bool v && v;
            }
            void TapDog(Func<bool> when)
            {
                int f = -1;
                Vector2 p = default;
                steps.Enqueue(() =>
                {
                    if (f < 0 && !when()) return true;
                    if (f < 0)
                    {
                        var cam = Camera.main;
                        var col = DogCol();
                        if (ptTouch == null || cam == null || col == null) { log.Add("  누를 곳 없음"); return true; }
                        Vector3 sp = cam.WorldToScreenPoint(col.bounds.center);
                        if (sp.z <= 0f) { log.Add("  3D 가 화면 뒤"); return true; }
                        p = new Vector2(sp.x, sp.y);
                        InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 9, phase = UnityEngine.InputSystem.TouchPhase.Began, position = p });
                        f = Time.frameCount;
                    }
                    if (Time.frameCount < f + 2) return false;
                    InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 9, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = p });
                    return true;
                });
            }
            void LogDog(string what)
            {
                var cam = Camera.main;
                var o = Obj();
                var m = o != null ? o.GetComponent<GLBModelLoader>()?.GetLoadedModel() : null;
                if (cam == null || m == null) { log.Add("  " + what + ": 3D 없음"); return; }
                int tris = 0;
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                {
                    Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh != null) tris += mesh.triangles.Length / 3;
                }
                ModelBounds(out var b);
                float top = float.MinValue, bot = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    float y = cam.WorldToScreenPoint(c).y;
                    top = Mathf.Max(top, y); bot = Mathf.Min(bot, y);
                }
                log.Add($"  {what}: 모델 크기 {b.size.x:0.00}×{b.size.y:0.00}×{b.size.z:0.00} · 삼각형 {tris} · 화면 높이의 {(top - bot) / Screen.height * 100f:0}% (아래 {bot / Screen.height * 100f:0}%) · 움직이는 중 {o.GetComponent<GLBModelLoader>().IsMoving} · 하늘판 {(SkyOpen() ? "열림" : "닫힘")}");
            }

            Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            Wait(() => !BootOverlay.Showing, 40f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                dm = UnityEngine.Object.FindAnyObjectByType<DataManager>(FindObjectsInactive.Include);
                fm = UnityEngine.Object.FindAnyObjectByType<FilterManager>(FindObjectsInactive.Include);
                was3D = PlayerPrefs.GetInt("Filter_Object3D_V2", 1) == 1;
                if (!was3D) Set3D(true);
                hadDist = PlayerPrefs.HasKey("MaxDisplayDistance");
                distWas = PlayerPrefs.GetFloat("MaxDisplayDistance", 0f);
                var vt = UnityEngine.Object.FindAnyObjectByType<P2PVirtualUserTester>();
                if (vt != null) { vt.StopSimulation(); vt.StopAllCoroutines(); }
                P2PManager.Instance?.RemoveTestUserAvatar("3");
                ptFocusWas = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
                req = UnityWebRequest.Get(ApiConfig.LOCATIONS_DETAIL + "?ids=" + Dog);   // 공개 GET
                req.timeout = 15;
                req.SendWebRequest();
            });
            Wait(() => req != null && req.isDone, 20f, "3D 장소 상세 (공개 GET)");
            Do(() =>
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { seed = JsonConvert.DeserializeObject<List<PlaceData>>(req.downloadHandler.text)?.Find(p => p != null && p.id == Dog); }
                    catch (Exception) { seed = null; }
                }
                req.Dispose();
                if (seed != null) Stand(-14.0, 0.0);
            });
            Check(() => seed != null && !string.IsNullOrEmpty(seed.model_url), "운영 3D 장소 — 모델 주소 있음");
            Wait(Loaded, 60f, "3D 모델이 떴다");
            Sleep(2.0f);
            Do(() => { HidePrivatePlaces(); HideAddressLine(); });
            var views = new List<(float d, float b, float p)>();
            bool wantList = false, wantAi = false;
            foreach (var it in (string.IsNullOrEmpty(spec) ? "8/45,8/315,9/270,list" : spec).Split(','))
            {
                if (it.Trim() == "list") { wantList = true; continue; }
                if (it.Trim() == "ai") { wantAi = true; continue; }   // 3D 캐릭터 단계만 (요청 적기 → 만든 캐릭터 → 확인 뒤 AR 미리보기)
                var a = it.Split('/');
                if (a.Length >= 2 && float.TryParse(a[0], out var d) && float.TryParse(a[1], out var be))
                    views.Add((d, be, a.Length > 2 && float.TryParse(a[2], out var pi) ? pi : -3f));
            }
            foreach (var v in views)
            {
                string tag = $"{v.d:0}m_{v.b:0}";
                Do(() => View(v.d, v.b, v.p));
                Sleep(1.2f);
                Do(() => { log.Add("  3D 아닌 오브젝트 끔 " + HideNon3D()); View(v.d, v.b, v.p); });
                Sleep(0.3f);
                bool tapped = false;
                Do(() => { var l = Obj()?.GetComponent<GLBModelLoader>(); tapped = l != null && !l.IsMoving; });
                TapDog(() => tapped);
                Sleep(1.0f);
                Do(() => LogDog(tag));
                Shot("tripo_ar_" + tag);
            }
            if (wantList)
            {
                Do(() =>
                {
                    foreach (var c in dm.GetLightCache())
                        if (!FilterManager.Is3D(c.category ?? "", c.modelType ?? "cube") && HiddenPlaces.Hide("dm_" + c.rawId)) hid.Add("dm_" + c.rawId);
                    foreach (var pd in dm.GetPlaceDataMap().Values)
                        if (pd != null && !FilterManager.Is3D(pd.category ?? "", pd.model_type ?? "cube") && HiddenPlaces.Hide("dm_" + pd.id)) hid.Add("dm_" + pd.id);
                    var ic = UnityEngine.Object.FindAnyObjectByType<R0926IndicatorClose>(FindObjectsInactive.Include);
                    var ub = ic != null ? typeof(R0926IndicatorClose).GetField("undoBar", NP)?.GetValue(ic) as CanvasGroup : null;
                    if (ub != null) ub.gameObject.SetActive(false);   // 「이 장소를 숨겼어요」 띠 — 촬영용으로 숨긴 것이라 끈다
                    log.Add("  목록에서 3D 아닌 장소 숨김 " + hid.Count);
                    Click("Dock0926/List_Button");
                });
                Sleep(1.2f);
                foreach (var km in new[] { 100f, 300f })
                {
                    Do(() =>
                    {
                        var plm = UnityEngine.Object.FindAnyObjectByType<PlaceListManager>(FindObjectsInactive.Include);
                        var sl = plm != null ? typeof(PlaceListManager).GetField("distanceSlider", NP)?.GetValue(plm) as Slider : null;
                        if (sl != null) sl.value = km;
                    });
                    Sleep(2.0f);
                    Do(() =>
                    {
                        var plm = UnityEngine.Object.FindAnyObjectByType<PlaceListManager>(FindObjectsInactive.Include);
                        var rows = plm != null ? typeof(PlaceListManager).GetField("combinedPlaces", NP)?.GetValue(plm) as System.Collections.ICollection : null;
                        log.Add($"  목록 {km:0}m 줄 " + (rows != null ? rows.Count : -1));
                    });
                    Shot($"tripo_list3d_{km:0}");
                }
                Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
                Sleep(1.0f);
            }
            if (!string.IsNullOrEmpty(spec) && !wantAi) { Cleanup(); return; }

            // ② 보물찾기 3D 보물상자 — 스토어 6번과 같은 길
            GameObject chestGo = null;
            Do(() =>
            {
                foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude)) t.gameObject.SetActive(false);
                AimCamera(-3f, 0f);
                if (wantAi) return;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Redesign0926/Prefabs/HuntTreasure0926.prefab");
                chestGo = prefab != null ? UnityEngine.Object.Instantiate(prefab) : null;
                var cam = Camera.main;
                if (chestGo != null && cam != null)
                {
                    Vector3 fw = cam.transform.forward; fw.y = 0f; fw.Normalize();
                    chestGo.transform.position = cam.transform.position + fw * 4.2f + Vector3.down * 0.55f;
                }
                log.Add("  보물상자 " + (chestGo != null ? "놓음" : "못 놓음"));
            });
            Sleep(1.6f);
            if (!wantAi) Shot("tripo_chest");
            Do(() => { if (chestGo != null) UnityEngine.Object.Destroy(chestGo); });
            Sleep(0.3f);

            // ③ 장소 추가 ▸ AI 작업 ▸ 3D 캐릭터 — 사진을 고른 직후와 같게 미리보기를 열고 (대표 사진 = 스토어 브랜드 그림)
            Do(() =>
            {
                AimCamera(-14f, 0f);
                R0926AiWork.Provider = null;
                cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                prev = UnityEngine.Object.FindAnyObjectByType<ARPreviewController>(FindObjectsInactive.Include);
                panel = UnityEngine.Object.FindAnyObjectByType<R0926AiWorkPanel>(FindObjectsInactive.Include);
                var src = new Texture2D(2, 2) { name = "TripoDemoPhoto" };
                src.LoadImage(File.ReadAllBytes(Path.Combine(storeSrc, "brand.png")));
                typeof(CubeUploadManager).GetField("mainPhoto", NP).SetValue(cum, src);
                typeof(CubeUploadManager).GetMethod("ClearAi", NP).Invoke(cum, null);
                typeof(CubeUploadManager).GetMethod("StartARPreview", NP).Invoke(cum, new object[] { src });
            });
            Wait(() => prev != null && prev.CubeReady, 15f, "미리보기 큐브 다 생김");
            Sleep(0.4f);
            Do(() =>
            {
                var b = Find(Bc + "AiButton0926")?.GetComponent<Button>();
                if (b != null && b.gameObject.activeInHierarchy && b.interactable) b.onClick.Invoke(); else log.Add("  AI 작업 못 누름");
            });
            Sleep(0.4f);
            Do(() => Click(Op + "Tiles/Chip4"));
            Wait(() => panel != null && !panel.Working && panel.Model != null, 40f, "3D 캐릭터 (무작위)");
            Do(() =>
            {
                var f = Find(Op + "Ask/Request")?.GetComponent<InputField>();
                if (f != null) f.text = "노란 고양이, 빨간 모자, 손 흔들기";
            });
            Sleep(0.6f);
            Shot("tripo_ai1_typed");   // 요청사항 한 줄을 적은 모습 — 만들기 누르기 전 (위 캐릭터는 3D 캐릭터 칸을 누를 때 나온 무작위)
            Do(() => Click(Op + "Ask/Make"));
            Sleep(0.3f);
            Wait(() => panel != null && !panel.Working && panel.Model != null, 40f, "3D 캐릭터 (요청사항)");
            Sleep(1.2f);
            Do(() =>
            {
                int tris = 0;
                if (panel.Model != null)
                    foreach (var r in panel.Model.GetComponentsInChildren<Renderer>())
                    {
                        Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                        if (mesh != null) tris += mesh.triangles.Length / 3;
                    }
                log.Add($"  AI 캐릭터 삼각형 {tris} · 안내 '{Find(Op + "Status")?.GetComponent<Text>()?.text}'");
            });
            Shot("tripo_aichar");
            Sleep(0.9f);
            Shot("tripo_aichar_b");
            Do(() => Click(Op + "Foot/Confirm"));   // 확인 = 만든 캐릭터를 두고 세 버튼으로 — AR 미리보기
            Sleep(1.5f);
            Shot("tripo_ai3_ar");

            Cleanup();

            void Cleanup() => Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
                if ((PlayerPrefs.GetInt("Filter_Object3D_V2", 1) == 1) != was3D) Set3D(was3D);
                if (hadDist) PlayerPrefs.SetFloat("MaxDisplayDistance", distWas); else PlayerPrefs.DeleteKey("MaxDisplayDistance");
                foreach (var id in hid) HiddenPlaces.Unhide(id);
                if (hid.Count > 0) log.Add("  숨긴 장소 되살림 " + hid.Count + " · 남은 숨김 " + HiddenPlaces.Count);
                hid.Clear();
                PlayerPrefs.Save();
            });
        }
    }
}
