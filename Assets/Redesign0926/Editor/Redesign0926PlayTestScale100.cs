using System;
using System.Collections.Generic;
using UnityEngine;

namespace Redesign0926
{
    /// <summary>
    /// 11-20 W1 우팡M 사이즈 상한 10 → 100 — 대표님 「100까지 설정할 수 있도록 하고, 실제로 해당 사이즈가 적용될 수 있도록 해줘」.
    ///  · scale100 : 우팡M 에서 고친 값이 앱에 들어오는 길(ApplyFreshPlaces)로 사이즈 100 · 50 · 2.5 를 넣어
    ///               사진 상자가 그 배율이 되는지 · 밑면 높이는 그대로(위로 커짐 — 땅 밑으로 안 내려감)인지 ·
    ///               원래 값으로 되돌리면 자리 · 크기가 처음 그대로인지 · 꼭대기가 카메라 그리는 거리 안인지 ·
    ///               3D 장소는 모델이 100배가 되는지(발밑 높이는 기록만) 본다.
    /// 운영 서버에 쓰지 않는다 — 고친 값은 편집기 안에서만이고 끝나면 원래 값으로 되돌린다.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanScale100()
        {
            const System.Reflection.BindingFlags PF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const System.Reflection.BindingFlags PS = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var privateNames = new HashSet<string> { "집", "우팡이네집", "엄빠집", "우팡이똥샷", "우팡이똥꼬샷" };
            Func<PlaceData, PlaceData> copy = pd => (PlaceData)typeof(object).GetMethod("MemberwiseClone", PF).Invoke(pd, null);
            Func<GameObject, Transform> boxOf = g => g != null ? g.GetComponentInChildren<DoubleTap3D>(true)?.transform : null;

            int id = -1, applied = -1;
            GameObject go = null;
            Transform box = null;
            PlaceData orig = null;
            Vector3 posWas = Vector3.zero, scaleWas = Vector3.one;
            float lossyWas = 0f, bottomWas = 0f, depth = 0.5f;
            // 상자 밑면 · 꼭대기 높이 — 앵커(맨 위) 기준 세로 거리 (월드 m · 렌더러가 숨어 있어도 모양으로 잰다)
            Func<float> bottom = () => box.TransformPoint(new Vector3(0f, -depth, 0f)).y - go.transform.position.y;
            Func<float> top = () => box.TransformPoint(new Vector3(0f, depth, 0f)).y - go.transform.position.y;
            Func<float, bool> sized = s => Mathf.Abs(box.localScale.x - s) < 0.01f
                && Mathf.Abs(box.lossyScale.x / Mathf.Max(lossyWas, 1e-6f) - s / Mathf.Max(scaleWas.x, 1e-6f)) < 0.01f * s;
            Func<bool> sameBottom = () => Mathf.Abs(bottom() - bottomWas) < 0.02f;
            Action<float> fresh = s =>
            {
                var p = copy(orig);
                p.model_scale = s;
                applied = DataManager.Instance.ApplyFreshPlaces(new List<PlaceData> { p });
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Wait(() =>
            {
                var dm = DataManager.Instance;
                if (dm?.GetSpawnedObjects() == null) return false;
                foreach (var kv in dm.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy || boxOf(kv.Value) == null || boxOf(kv.Value) == kv.Value.transform) continue;
                    if (!dm.GetPlaceDataMap().TryGetValue(kv.Key, out var pd) || pd.model_type == "custom" || privateNames.Contains(pd.name?.Trim() ?? "")) continue;
                    id = kv.Key; go = kv.Value; orig = pd; box = boxOf(go);
                    return true;
                }
                return false;
            }, 40f, "우팡 장소 사진 상자");
            Sleep(1f);
            Do(() =>
            {
                depth = (float)typeof(DataManager).GetMethod("BoxBottomDepth", PS).Invoke(null, new object[] { box });
                posWas = box.localPosition;
                scaleWas = box.localScale;
                lossyWas = box.lossyScale.x;
                bottomWas = bottom();
                log.Add($"  장소 {id} · 원래 사이즈 {orig.model_scale:0.##} · 상자 칸 {scaleWas.x:0.##} · 한 변 {lossyWas * depth * 2f:0.##}m · 밑면 {bottomWas:0.##}m (앵커 기준)");
            });

            // 1) 사이즈 100 — 우팡M 의 새 상한
            Do(() => fresh(100f));
            Sleep(0.3f);
            Do(() => log.Add($"  100 뒤: 상자 칸 {box.localScale.x:0.##} · 한 변 {box.lossyScale.x * depth * 2f:0.#}m · 밑면 {bottom():0.##}m · 꼭대기 {top():0.#}m"));
            Check(() => applied == 1 && sized(100f), "사이즈 100 → 사진 상자 100배 (화면 크기도 같은 배율)");
            Check(() => sameBottom(), "사이즈 100 → 밑면 높이 그대로 · 위로 커짐 (땅 밑으로 안 내려감)");
            Check(() => DataManager.Instance.GetSpawnedObjects().TryGetValue(id, out var now) && now == go && go.activeInHierarchy
                && Mathf.Approximately(DataManager.Instance.GetPlaceDataMap()[id].model_scale, 100f),
                "같은 오브젝트에서 바뀐다 (다시 만들지 않음) · 장소 값도 100");
            Check(() =>
            {
                var cam = Camera.main;
                if (cam == null) return false;
                float far = Vector3.Distance(cam.transform.position, box.TransformPoint(new Vector3(depth, depth, depth)));
                log.Add($"  꼭대기 모서리까지 {far:0}m · 카메라 그리는 거리 {cam.farClipPlane:0}m");
                return far < cam.farClipPlane;
            }, "사이즈 100 상자 꼭대기가 카메라 그리는 거리 안 (잘리지 않음)");
            Do(HidePrivatePlaces);
            Shot("scale100_box");

            // 2) 50 · 2.5 — 예전 상한(10)을 넘는 값 · 작은 값도 밑면 그대로
            Do(() => fresh(50f));
            Sleep(0.3f);
            Check(() => applied == 1 && sized(50f) && sameBottom(), "사이즈 50 → 50배 · 밑면 그대로");
            Do(() => fresh(2.5f));
            Sleep(0.3f);
            Check(() => applied == 1 && sized(2.5f) && sameBottom(), "사이즈 2.5 → 2.5배 · 밑면 그대로");

            // 3) 사이즈가 빈(0) 장소에 다시 쓰인 상자 — 1배 · 밑면 그대로 (풀에서 다시 꺼낸 것과 같은 길)
            Do(() => typeof(DataManager).GetMethod("ApplyBoxScale", PS).Invoke(null, new object[] { go, box, 0f }));
            Check(() => Mathf.Abs(box.localScale.x - 1f) < 0.001f && sameBottom(), "사이즈 비면(0) → 1배 · 밑면 그대로");

            // 4) 원래 값으로 되돌림 — 자리 · 크기 처음 그대로
            Do(() => fresh(orig.model_scale));
            Sleep(0.3f);
            Do(() => log.Add($"  되돌린 뒤: 상자 칸 {box.localScale.x:0.##} · 자리 차이 {Vector3.Distance(box.localPosition, posWas):0.0000} · 밑면 {bottom():0.##}m"));
            Check(() => Vector3.Distance(box.localPosition, posWas) < 0.0005f && Vector3.Distance(box.localScale, scaleWas) < 0.001f
                && Mathf.Abs(box.lossyScale.x - lossyWas) < 0.001f * Mathf.Max(lossyWas, 1f),
                "원래 사이즈로 되돌리면 자리 · 크기 처음 그대로");

            // 5) 3D 장소 — 모델이 100배 (발밑 높이는 모델마다 축이 달라 기록만)
            int glbId = -1;
            GLBModelLoader glb = null;
            GameObject glbGo = null, glbModel = null;
            PlaceData glbOrig = null;
            float glbWas = 0f, footWas = 0f;
            Func<float> foot = () =>
            {
                var rs = glbModel != null ? glbModel.GetComponentsInChildren<Renderer>(true) : null;
                if (rs == null || rs.Length == 0) return float.NaN;
                float y = float.MaxValue;
                foreach (var r in rs)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    var smr = r as SkinnedMeshRenderer;
                    Bounds lb = smr != null ? smr.localBounds : mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds();
                    for (int i = 0; i < 8; i++)
                    {
                        var c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        y = Mathf.Min(y, r.transform.TransformPoint(c).y);
                    }
                }
                return y - glbGo.transform.position.y;
            };
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
                footWas = foot();
                var big = copy(glbOrig);
                big.model_scale = 100f;
                applied = dm.ApplyFreshPlaces(new List<PlaceData> { big });
            });
            Sleep(0.3f);
            Do(() => { if (glb != null) log.Add($"  3D 장소 {glbId} · 모델 {glbWas:0.##} → {glbModel.transform.localScale.x:0.##} · 발밑 {footWas:0.##}m → {foot():0.##}m (앵커 기준)"); });
            Check(() => glb == null || applied == 1 && glb.GetLoadedModel() == glbModel && Mathf.Abs(glbModel.transform.localScale.x - 100f) < 0.01f,
                "3D 장소 사이즈 100 → 같은 모델이 바로 100배");
            Do(() => { if (glb != null) applied = DataManager.Instance.ApplyFreshPlaces(new List<PlaceData> { copy(glbOrig) }); });
            Sleep(0.3f);
            Check(() => glb == null || applied == 1 && Mathf.Abs(glbModel.transform.localScale.x - glbWas) < 0.01f, "3D 장소 원래 사이즈로 되돌림");
        }
    }
}
