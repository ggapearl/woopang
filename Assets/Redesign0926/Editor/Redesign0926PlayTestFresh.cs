using System;
using System.Collections.Generic;
using UnityEngine;

namespace Redesign0926
{
    /// <summary>
    /// 11-16 우팡M 사이즈 반영 시험 — 대표님 「너가 테스트로 하나 극단적으로 크게해서 잘 반영되는 지 확인해보고 수정해줘」.
    ///  · freshscale : 앱으로 돌아올 때 떠 있는 장소를 다시 받아(DataManager.FreshenSpawned) 바뀐 것만 고친다 —
    ///                 사이즈 8 을 넣으면 사진 상자 8배 · 눌러도 8배로 돌아옴 · 같은 값은 건드리지 않음 · 원래 값으로 되돌림 ·
    ///                 진짜 서버로 다시 받으면(고친 게 없으니) 하나도 다시 그리지 않음 · 3D 장소는 그 사이즈로 다시 뜸.
    /// 운영 서버에 쓰지 않는다 — 고친 값은 기기(편집기) 안에서만이고 끝나면 원래 값으로 되돌린다. 서버는 상세를 읽기만 한다.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanFreshScale()
        {
            const System.Reflection.BindingFlags PF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var privateNames = new HashSet<string> { "집", "우팡이네집", "엄빠집", "우팡이똥샷", "우팡이똥꼬샷" };
            Func<PlaceData, PlaceData> copy = pd => (PlaceData)typeof(object).GetMethod("MemberwiseClone", PF).Invoke(pd, null);
            Func<GameObject, Transform> boxOf = g => g != null ? g.GetComponentInChildren<DoubleTap3D>(true)?.transform : null;
            Func<Transform, Vector3> restOf = b =>
            {
                var h = b != null ? b.GetComponent<Object3DTouchHaptic>() : null;
                var f = h != null ? typeof(Object3DTouchHaptic).GetField("restScale", PF) : null;
                return f != null ? (Vector3)f.GetValue(h) : (b != null ? b.localScale : Vector3.zero);
            };
            var dmType = typeof(DataManager);
            Func<float> lastFreshen = () => (float)dmType.GetField("lastFreshenAt", PF).GetValue(DataManager.Instance);

            int id = -1, applied = -1;
            GameObject go = null;
            Transform box = null;
            PlaceData orig = null;
            Vector3 boxWas = Vector3.one;
            float lossyWas = 0f;
            var refs = new Dictionary<int, PlaceData>();
            var warns = new List<string>();
            float freshenWas = 0f;
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type != LogType.Log && msg.Contains("다시 받은 장소")) warns.Add(msg);
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                Application.logMessageReceived += onLog;
            });
            Wait(() =>
            {
                var dm = DataManager.Instance;
                if (dm?.GetSpawnedObjects() == null) return false;
                foreach (var kv in dm.GetSpawnedObjects())
                    if (kv.Value != null && kv.Value.activeInHierarchy && boxOf(kv.Value) != null && boxOf(kv.Value) != kv.Value.transform
                        && dm.GetPlaceDataMap().TryGetValue(kv.Key, out var pd) && pd.model_type != "custom" && !privateNames.Contains(pd.name?.Trim() ?? ""))
                        return true;
                return false;
            }, 40f, "우팡 장소 사진 상자");
            Sleep(2f);

            // 1) 사이즈 8 — 우팡M 에서 고친 값이 다시 받기로 들어온 것과 같다
            Do(() =>
            {
                var dm = DataManager.Instance;
                foreach (var kv in dm.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy || boxOf(kv.Value) == null || boxOf(kv.Value) == kv.Value.transform) continue;
                    if (!dm.GetPlaceDataMap().TryGetValue(kv.Key, out var pd) || pd.model_type == "custom" || privateNames.Contains(pd.name?.Trim() ?? "")) continue;
                    id = kv.Key; go = kv.Value; orig = pd; break;
                }
                box = boxOf(go);
                boxWas = box.localScale;
                lossyWas = box.lossyScale.x;
                var big = copy(orig);
                big.model_scale = 8f;
                applied = dm.ApplyFreshPlaces(new List<PlaceData> { big });
                log.Add($"  장소 {id} · 원래 사이즈 {orig.model_scale:0.##} · 상자 {boxWas.x:0.###} (화면 {lossyWas:0.###}) → 고친 개수 {applied}");
            });
            Sleep(0.3f);
            Do(() => log.Add($"  사이즈 8 뒤: 상자 {box.localScale.x:0.###} (화면 {box.lossyScale.x:0.###} · {box.lossyScale.x / Mathf.Max(lossyWas, 1e-6f):0.00}배) · 쉬는 크기 {restOf(box).x:0.###}"));
            Check(() => applied == 1 && Mathf.Abs(box.localScale.x - 8f) < 0.01f && Mathf.Abs(box.lossyScale.x / lossyWas - 8f / Mathf.Max(boxWas.x, 1e-6f)) < 0.05f,
                "사이즈 8 → 사진 상자가 8배 (화면 크기도 같은 배율)");
            Check(() => Mathf.Abs(restOf(box).x - 8f) < 0.01f, "눌렀다 떼도 8배로 돌아온다 (쉬는 크기도 8)");
            Check(() => DataManager.Instance.GetSpawnedObjects().TryGetValue(id, out var now) && now == go && go.activeInHierarchy
                && Mathf.Approximately(DataManager.Instance.GetPlaceDataMap()[id].model_scale, 8f),
                "같은 오브젝트에서 바뀐다 (다시 만들지 않음) · 장소 값도 8");
            Do(HidePrivatePlaces);
            Shot("fresh_big");

            // 2) 같은 값 — 아무것도 건드리지 않는다
            Do(() =>
            {
                var same = copy(DataManager.Instance.GetPlaceDataMap()[id]);
                applied = DataManager.Instance.ApplyFreshPlaces(new List<PlaceData> { same });
            });
            Check(() => applied == 0, "같은 값으로 다시 받으면 고치지 않는다 (사진 · 앵커 그대로)");

            // 3) 원래 값으로 되돌림
            Do(() => applied = DataManager.Instance.ApplyFreshPlaces(new List<PlaceData> { copy(orig) }));
            Sleep(0.3f);
            Check(() => applied == 1 && Vector3.Distance(box.localScale, boxWas) < 0.01f && Mathf.Abs(box.lossyScale.x - lossyWas) < 0.01f * Mathf.Max(lossyWas, 1f),
                "원래 사이즈로 다시 받으면 원래 크기");

            // 4) 진짜 앱 복귀 길 — RestartFetchingAfterResume 이 떠 있는 장소를 서버에서 다시 받는다 (읽기만). 고친 게 없으니 하나도 바뀌지 않아야
            Do(() =>
            {
                var dm = DataManager.Instance;
                refs.Clear();
                foreach (var kv in dm.GetSpawnedObjects())
                    if (kv.Value != null && dm.GetPlaceDataMap().TryGetValue(kv.Key, out var pd)) refs[kv.Key] = pd;
                dmType.GetField("lastFreshenAt", PF).SetValue(dm, -100f);
                dmType.GetField("_lastResumeFetchTime", PF).SetValue(dm, -100f);
                freshenWas = -100f;
                dm.RestartFetchingAfterResume();
                log.Add($"  앱 복귀 — 떠 있는 우팡 장소 {refs.Count}개를 다시 받음");
            });
            Check(() => lastFreshen() > freshenWas, "앱으로 돌아오면 떠 있는 장소를 다시 묻는다");
            Sleep(8f);
            Do(() =>
            {
                var dm = DataManager.Instance;
                int same = 0, changed = 0;
                foreach (var kv in refs)
                {
                    if (!dm.GetPlaceDataMap().TryGetValue(kv.Key, out var now)) continue;
                    if (ReferenceEquals(now, kv.Value)) same++;
                    else { changed++; log.Add($"  바뀜: {kv.Key}"); }
                }
                log.Add($"  다시 받은 뒤: 그대로 {same} · 바뀜 {changed} · 경고 {warns.Count}");
                if (changed > 0 || warns.Count > 0 || same == 0) log.Add("FAIL 고친 게 없으면 진짜 서버로 다시 받아도 하나도 다시 그리지 않는다");
                else log.Add("PASS 고친 게 없으면 진짜 서버로 다시 받아도 하나도 다시 그리지 않는다");
            });

            // 5) 3D 장소 — 다시 받지 않고 그 자리에서 그 사이즈 (떠 있지 않아도 · 둘레에 있을 때만)
            //    예전 길(모델을 다시 받기)은 꺼진 오브젝트에서 실패해 세 번 뒤 큐브로 바뀌었다 (이 시험의 첫 판에서 발견)
            int glbId = -1;
            GameObject glbGo = null;
            GLBModelLoader glb = null;
            GameObject glbModel = null;
            PlaceData glbOrig = null;
            float glbWas = 0f, want = 0f;
            Vector3 colWas = Vector3.zero;
            Func<Vector3> colSize = () => { var c = glb != null ? glb.GetComponentInChildren<DoubleTap3D>(true)?.GetComponent<BoxCollider>() : null; return c != null ? c.size : Vector3.zero; };
            Func<bool> glbSame = () => glb != null && glb.HasLoadedModel() && glb.GetLoadedModel() == glbModel
                && DataManager.Instance.GetSpawnedObjects().TryGetValue(glbId, out var now) && now == glbGo
                && DataManager.Instance.GetPlaceDataMap()[glbId].model_type == "custom";
            Do(() =>
            {
                var dm = DataManager.Instance;
                foreach (var kv in dm.GetSpawnedObjects())
                {
                    if (kv.Value == null || !dm.GetPlaceDataMap().TryGetValue(kv.Key, out var pd) || pd.model_type != "custom") continue;
                    var l = kv.Value.GetComponent<GLBModelLoader>();
                    if (l == null || !l.HasLoadedModel() || l.GetLoadedModel() == null) continue;
                    glbId = kv.Key; glbGo = kv.Value; glb = l; glbModel = l.GetLoadedModel(); glbOrig = pd;
                    glbWas = glbModel.transform.localScale.x;
                    break;
                }
                if (glb == null) { log.Add("  3D 장소가 둘레에 없음 — 건너뜀"); return; }
                colWas = colSize();
                var big = copy(glbOrig);
                big.model_scale = want = Mathf.Max(glbOrig.model_scale, 0.1f) * 3f;
                applied = dm.ApplyFreshPlaces(new List<PlaceData> { big });
                log.Add($"  3D 장소 {glbId} (떠 있음 {glbGo.activeInHierarchy}) · 모델 {glbWas:0.###} · 누르는 칸 {colWas.x:0.##} → 사이즈 {want:0.##} (고친 개수 {applied})");
            });
            Check(() => glb == null || applied == 1 && glbSame() && Mathf.Abs(glbModel.transform.localScale.x - want) < 0.01f
                && (colWas == Vector3.zero || Mathf.Abs(colSize().x - colWas.x * want / glbWas) < 0.01f * Mathf.Max(1f, colWas.x)),
                "3D 장소 사이즈 3배 → 같은 모델이 바로 3배 (다시 받지 않음) · 누르는 칸도 3배");
            Sleep(8f);
            Do(() => { if (glb != null) log.Add($"  8초 뒤: 모델 {(glb.GetLoadedModel() != null ? glb.GetLoadedModel().transform.localScale.x : -1f):0.###} · 같은 모델 {glbSame()}"); });
            Check(() => glb == null || glbSame() && Mathf.Abs(glbModel.transform.localScale.x - want) < 0.01f, "8초 뒤에도 그대로 (큐브로 바뀌지 않음)");
            Do(() => { if (glb != null) applied = DataManager.Instance.ApplyFreshPlaces(new List<PlaceData> { copy(glbOrig) }); });
            Check(() => glb == null || applied == 1 && glbSame() && Mathf.Abs(glbModel.transform.localScale.x - glbWas) < 0.01f
                && (colWas == Vector3.zero || Mathf.Abs(colSize().x - colWas.x) < 0.01f * Mathf.Max(1f, colWas.x)),
                "3D 장소 원래 사이즈로 되돌림 (누르는 칸도)");
            Do(() => Application.logMessageReceived -= onLog);
        }
    }
}
