using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 L7 — 대표님 「3D 모델 — 기본은 움직임 없이(멈춤) · 누르면 그 모델이 가진 움직임을 10초 정도 하고 멈춤」.
    ///  · animtap : 운영 시바견(dm_34837 · 공개 GET)으로 — 떠 있을 땐 뼈대가 그대로(멈춤) · 가상 손가락으로 한 번 누르면(진짜 누르기 길 — 레이캐스트)
    ///    그 모델의 클립이 돈다 · 10초에 가장 가까운 바퀴 수만큼 돌고 처음 멈춤 자세(첫 장면)로 멈춘다 · 움직이는 중에 또 누르면 그 자리부터 다시 센다 ·
    ///    두 번 누르면 정보 창 (바로) · 반납하면 움직임을 멈추고 클립도 뗀다(풀에서 다시 쓸 때 앞 모델 움직임이 섞이지 않게) · 다시 떠도 멈춤 ·
    ///    누르면 움직임을 켜지 않은 곳(아바타 · AI 미리보기 · 보물)은 예전처럼 받자마자 움직인다.
    ///    서버에 쓰는 것 없음. 시바견 자리는 운영 상세에서 받아 그 옆으로 옮긴다 (좌표 · 거리는 기록에 남기지 않는다). 끝나면 3D 칩 · 입력 설정을 처음대로
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanAnimTap()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            const int Dog = 34837;
            DataManager dm = null;
            FilterManager fm = null;
            PlaceData seed = null;
            bool was3D = true, movingAfterReturn = true;
            UnityWebRequest req = null;
            GLBModelLoader gl = null;
            DoubleTap3D dt = null;
            Animation anim = null;
            List<Transform> bones = null;
            List<(Vector3 p, Quaternion r)> rest0 = null, snap = null;
            float tapAt = -1f, tap2At = -1f, stopAt = -1f, cycle = 0f, total = 0f;
            int clipsAfterReturn = -1;
            GameObject probe = null;
            GLBModelLoader probeGl = null;
            bool? probeOk = null;
            List<Transform> probeBones = null;
            List<(Vector3 p, Quaternion r)> probeSnap = null;

            T Field<T>(string name) where T : class => dm != null ? typeof(DataManager).GetField(name, NP)?.GetValue(dm) as T : null;
            GameObject Obj() => Field<Dictionary<int, GameObject>>("spawnedObjects") is Dictionary<int, GameObject> m && m.TryGetValue(Dog, out var g) ? g : null;
            bool Loaded()
            {
                var o = Obj();
                var l = o != null ? o.GetComponent<GLBModelLoader>() : null;
                return l != null && l.IsModelLoaded && o.name.EndsWith("_custom");
            }
            bool InfoOpen() => dt != null && dt.fullscreenCanvasGroup != null && dt.fullscreenCanvasGroup.gameObject.activeSelf;
            List<string> Clips() => gl != null ? typeof(GLBModelLoader).GetField("motionClips", NP)?.GetValue(gl) as List<string> : null;
            List<(Vector3 p, Quaternion r)> Pose(List<Transform> ts) => ts?.Select(t => t != null ? (t.localPosition, t.localRotation) : (Vector3.zero, Quaternion.identity)).ToList();
            // 뼈대 차이 — 가장 많이 움직인 뼈의 각도(도)와 위치(모델 단위 × 1000) 중 큰 쪽 (등장 연출이 바꾸는 크기는 보지 않는다)
            float Diff(List<(Vector3 p, Quaternion r)> a, List<(Vector3 p, Quaternion r)> b)
            {
                if (a == null || b == null || a.Count != b.Count) return 999f;
                float d = 0f;
                for (int i = 0; i < a.Count; i++) d = Mathf.Max(d, Mathf.Max(Quaternion.Angle(a[i].r, b[i].r), (a[i].p - b[i].p).magnitude * 1000f));
                return d;
            }
            void Grab()
            {
                var o = Obj();
                gl = o != null ? o.GetComponent<GLBModelLoader>() : null;
                dt = o != null ? o.GetComponentInChildren<DoubleTap3D>(true) : null;
                anim = dt != null ? dt.GetComponent<Animation>() : null;
                bones = gl != null && gl.GetLoadedModel() != null ? gl.GetLoadedModel().GetComponentsInChildren<Transform>(true).ToList() : null;
            }
            // 카메라를 시바견 누르는 칸 가운데로 — 에디터 카메라가 다른 쪽을 보고 있으면 (기기에선 사람이 돌아본다)
            Vector2? Aim()
            {
                var cam = Camera.main;
                var col = dt != null ? dt.GetComponent<Collider>() : null;
                if (cam == null || col == null) return null;
                cam.transform.rotation = Quaternion.LookRotation(col.bounds.center - cam.transform.position);
                Vector3 sp = cam.WorldToScreenPoint(col.bounds.center);
                if (sp.z <= 0f) return null;
                if (!(Physics.Raycast(cam.ScreenPointToRay(sp), out var hit) && hit.collider == col))
                    log.Add("  누를 자리 앞을 가린 것: " + (hit.collider != null ? hit.collider.name : "없음"));
                return new Vector2(sp.x, sp.y);
            }
            // 가상 손가락으로 한 번 — 누르고 두 프레임 뒤에 뗀다 (PlanNudge 와 같은 길 — DoubleTap3D 가 레이캐스트로 받는다)
            void Tap(int id, Action onDown)
            {
                int f = -1;
                Vector2 p = default;
                steps.Enqueue(() =>
                {
                    if (f < 0)
                    {
                        var a = ptTouch != null ? Aim() : null;
                        if (a == null) { log.Add("  누를 곳 없음"); return true; }
                        p = a.Value;
                        InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Began, position = p });
                        f = Time.frameCount;
                        onDown?.Invoke();
                    }
                    if (Time.frameCount < f + 2) return false;
                    InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = p });
                    return true;
                });
            }
            void Set3D(bool on)
            {
                var t = fm != null ? typeof(FilterManager).GetField("object3DToggle", NP)?.GetValue(fm) as Toggle : null;
                if (t != null) t.isOn = on;
            }

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                dm = UnityEngine.Object.FindAnyObjectByType<DataManager>(FindObjectsInactive.Include);
                fm = UnityEngine.Object.FindAnyObjectByType<FilterManager>(FindObjectsInactive.Include);
                was3D = PlayerPrefs.GetInt("Filter_Object3D_V2", 1) == 1;
                if (!was3D) Set3D(true);
                ptFocusWas = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;   // 에디터 창이 뒤에 있어도
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
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
                if (seed == null) { log.Add("  시바견 상세: 못 받음"); return; }
                VirtualLocation.Instance.SetCoordinates((float)(seed.latitude + 0.0003), (float)seed.longitude);   // 시바견 바로 옆
                fm?.TriggerReallocation();
            });
            Check(() => seed != null && seed.category == "anim" && !string.IsNullOrEmpty(seed.model_url), "시험 전제 — 시바견은 춤 모델(anim) · 모델 주소 있음");

            // 1) 떠 있을 땐 멈춤 — 그 모델이 가진 움직임은 붙어 있되 돌지 않는다
            Wait(() => Loaded(), 60f, "시바견 3D 가 다 떴다");
            Do(() =>
            {
                Grab();
                var names = Clips();
                cycle = 0f;
                if (anim != null && names != null) foreach (var n in names) if (anim[n] != null) cycle = Mathf.Max(cycle, anim[n].length);
                total = cycle > 0f ? cycle * Mathf.Max(1f, Mathf.Round(GLBModelLoader.MotionSeconds / cycle)) : 0f;
                log.Add($"  모델이 가진 클립 {(anim != null ? anim.GetClipCount() : 0)}개 · 누르면 도는 것: {(names == null ? "?" : string.Join(", ", names))} · 한 바퀴 {cycle:0.00}초 → 누르면 {total:0.00}초");
                Aim();
            });
            Check(() => gl != null && gl.motionOnTap && gl.MotionClipCount > 0 && anim != null && bones != null && bones.Count > 1,
                "장소 3D 는 「누르면 움직임」이 켜져 있고 그 모델의 움직임 클립이 붙어 있다");
            Sleep(0.5f);
            Do(() => rest0 = Pose(bones));
            Sleep(1.5f);
            Check(() =>
            {
                float d = Diff(rest0, Pose(bones));
                log.Add($"  1.5초 사이 뼈대 변화 {d:0.000} · 재생 중 {(anim != null && anim.isPlaying)}");
                return d < 0.05f && anim != null && !anim.isPlaying && !gl.IsMoving;
            }, "기본은 멈춤 — 1.5초 동안 뼈대가 그대로 · 도는 클립 없음");
            Shot("animtap_still");

            // 2) 한 번 누르면 그 모델의 움직임 — 10초에 가장 가까운 바퀴 수만큼 돌고 처음 자세(첫 장면)로 멈춘다
            Tap(7, null);
            Wait(() => { if (gl != null && gl.IsMoving) { tapAt = Time.time; return true; } return false; }, 3f, "한 번 누름 → 움직이기 시작");
            Sleep(0.7f);
            Check(() =>
            {
                float d = Diff(rest0, Pose(bones));
                var names = Clips();
                bool playing = anim != null && names != null && names.Count > 0 && anim.IsPlaying(names[0]);
                log.Add($"  누른 뒤 0.7초: 움직이는 중 {gl?.IsMoving} · 그 클립 재생 {playing} · 뼈대 변화 {d:0.0}");
                return gl != null && gl.IsMoving && playing && d > 1f;
            }, "한 번 누르면 그 모델이 가진 움직임이 돈다");
            Check(() => !InfoOpen(), "한 번 누르기로는 정보 창이 뜨지 않음");
            Shot("animtap_moving");
            Wait(() => { if (gl != null && !gl.IsMoving) { stopAt = Time.time; return true; } return false; }, 25f, "움직임이 저절로 멈춤");
            Check(() =>
            {
                float dur = stopAt - tapAt, d = Diff(rest0, Pose(bones));
                log.Add($"  움직인 시간 {dur:0.00}초 (바퀴에 맞춘 값 {total:0.00}초) · 멈춘 자세와 처음 자세 차이 {d:0.000}");
                return tapAt > 0f && stopAt > tapAt && Mathf.Abs(dur - total) < 0.6f && dur > 7.5f && dur < 13.5f && d < 0.05f;
            }, "10초 정도 움직이고 한 바퀴가 끝나는 자리에서 처음 자세(첫 장면)로 멈춘다");
            Sleep(1.0f);
            Check(() => Diff(rest0, Pose(bones)) < 0.05f && anim != null && !anim.isPlaying, "멈춘 뒤 그대로 (다시 돌지 않음)");

            // 3) 움직이는 중에 또 누르면 그 자리부터 다시 센다
            Tap(7, null);
            Wait(() => { if (gl != null && gl.IsMoving) { tapAt = Time.time; return true; } return false; }, 3f, "다시 누름 → 움직임");
            Sleep(5.0f);
            Check(() => gl != null && gl.IsMoving, "5초 뒤에도 움직이는 중");
            Tap(7, () => tap2At = Time.time);
            Wait(() => { if (gl != null && !gl.IsMoving) { stopAt = Time.time; return true; } return false; }, 25f, "또 누른 움직임이 저절로 멈춤");
            Check(() =>
            {
                float all = stopAt - tapAt, after = stopAt - tap2At;
                log.Add($"  처음 누른 뒤 {all:0.00}초 · 또 누른 뒤 {after:0.00}초에 멈춤");
                return tap2At > tapAt && all > total + 3f && after > total - cycle - 0.6f && after < total + 0.6f && Diff(rest0, Pose(bones)) < 0.05f;
            }, "움직이는 중에 또 누르면 그 자리부터 다시 센다 (처음 누른 10초에 멈추지 않음) · 멈출 땐 처음 자세");

            // 4) 두 번 누르면 정보 창 — 한 번 누르기(움직임)를 기다리지 않고 바로
            Sleep(0.5f);
            Tap(7, null);
            steps.Enqueue(() => dt == null || Time.time >= (float)typeof(DoubleTap3D).GetField("lastTapTime", NP).GetValue(dt) + 0.15f);
            Tap(8, null);
            Sleep(0.8f);
            Check(() => InfoOpen(), "두 번 누르면 정보 창 (움직임과 같이 와도 바로)");
            Shot("animtap_info");
            Do(() => { if (InfoOpen()) typeof(DoubleTap3D).GetMethod("CloseFullscreen", NP)?.Invoke(dt, null); });
            Sleep(0.8f);
            Check(() => !InfoOpen(), "정보 창 닫힘");

            // 5) 반납하면 움직임을 멈추고 클립도 뗀다 — 풀에서 다른 장소로 다시 쓸 때 앞 모델 움직임이 섞이지 않게 · 다시 떠도 멈춤
            Do(() =>
            {
                var a = anim;
                var l = gl;
                dm.DespawnFullObject(Dog.ToString());
                clipsAfterReturn = a != null ? a.GetClipCount() : -1;
                movingAfterReturn = l != null && l.IsMoving;
                dm.SpawnFullObject(Dog.ToString());   // 같은 프레임 — 배분이 끼어들 틈 없이
            });
            Check(() =>
            {
                log.Add($"  반납 뒤: 남은 클립 {clipsAfterReturn}개 · 움직이는 중 {movingAfterReturn}");
                return clipsAfterReturn == 0 && !movingAfterReturn;
            }, "반납하면 (움직이던 중이어도) 멈추고 움직임 클립을 뗀다");
            Wait(() => Loaded(), 40f, "시바견 3D 가 다시 떴다");
            Do(() => { Grab(); snap = Pose(bones); });
            Sleep(1.2f);
            Check(() =>
            {
                float d = Diff(snap, Pose(bones));
                log.Add($"  다시 뜬 뒤: 클립 {(anim != null ? anim.GetClipCount() : 0)}개 · 누르면 도는 것 {gl?.MotionClipCount}개 · 1.2초 사이 뼈대 변화 {d:0.000}");
                return gl != null && gl.motionOnTap && gl.MotionClipCount > 0 && anim != null && !anim.isPlaying && d < 0.05f;
            }, "다시 떠도 기본은 멈춤 (움직임 클립은 새로 붙음)");

            // 6) 누르면 움직임을 켜지 않은 곳(아바타 · AI 미리보기 · 보물)은 예전처럼 받자마자 움직인다 — 같은 모델을 그 방식으로 한 번 받아 본다
            Do(() =>
            {
                probe = new GameObject("L7_AutoplayProbe");
                probe.transform.position = new Vector3(0f, -500f, 0f);   // 화면 밖
                probeGl = probe.AddComponent<GLBModelLoader>();
                probeGl.displayOnly = true;
            });
            Sleep(0.3f);   // Start 가 GLBContainer 를 만든 뒤
            Do(() =>
            {
                if (seed == null || probeGl == null) { probeOk = false; return; }
                string url = seed.model_url.StartsWith("http") ? seed.model_url : ApiConfig.MAIN_SERVER + "/" + seed.model_url.Replace("\\", "/");
                probeGl.StartCoroutine(probeGl.LoadGLBModelCoroutine(url, 1f, ok => probeOk = ok));
            });
            Wait(() => probeOk != null, 40f, "다른 곳 방식으로 같은 모델 받기");
            Sleep(0.3f);
            Do(() =>
            {
                probeBones = probeGl != null && probeGl.GetLoadedModel() != null ? probeGl.GetLoadedModel().GetComponentsInChildren<Transform>(true).ToList() : null;
                probeSnap = Pose(probeBones);
            });
            Sleep(1.0f);
            Check(() =>
            {
                var m = probeGl != null ? probeGl.GetLoadedModel() : null;
                var a = m != null && m.transform.parent != null ? m.transform.parent.GetComponent<Animation>() : null;
                float d = Diff(probeSnap, Pose(probeBones));
                log.Add($"  다른 곳: 받기 {probeOk} · 누르면 움직임 {probeGl?.motionOnTap} · 재생 중 {(a != null && a.isPlaying)} · 1초 사이 뼈대 변화 {d:0.0}");
                return probeOk == true && probeGl != null && !probeGl.motionOnTap && a != null && a.isPlaying && d > 1f;
            }, "누르면 움직임을 켜지 않은 곳(아바타 · AI 미리보기 · 보물)은 예전처럼 받자마자 움직인다");
            Do(() =>
            {
                var a = probeGl != null && probeGl.GetLoadedModel() != null ? probeGl.GetLoadedModel().transform.parent.GetComponent<Animation>() : null;
                probeGl?.ClearModel();
                log.Add($"  다른 곳 비우기: 남은 클립 {(a != null ? a.GetClipCount() : -1)}개");
                if (probe != null) UnityEngine.Object.Destroy(probe);
                probe = null;
            });

            Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
                if ((PlayerPrefs.GetInt("Filter_Object3D_V2", 1) == 1) != was3D) Set3D(was3D);
            });
        }
    }
}
