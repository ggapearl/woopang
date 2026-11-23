using System;
using System.Collections.Generic;
using UnityEngine;

namespace Redesign0926
{
    /// <summary>
    /// 11-18 H3 — 대표님 「'우팡산' 오브젝트가 인디케이터 없이 발생했거든? … 왜 인디케이터는 발생하지 않는데 목록에는 있고 3D오브젝트만 떠있는 지」.
    ///  · h3ind : 3D 는 보이는데 화살표 · 박스가 없던 길을 하나씩 막았는지 —
    ///            ① 높이를 올려 직선거리가 Target 끄는 거리(400 m)를 넘은 큐브 (우팡산과 같은 상황) — Target 이 켜진 채 박스가 붙는다
    ///            ② Target 끄는 거리와 3D 로 띄우는 반경(500 m) 사이 — 3D 와 박스가 함께
    ///            ③ 기기 규칙으로: 앵커 자리가 없으면 3D 도 인디케이터도 없다 (원점에 뜨지 않게) · 나침반 자리에 놓이면 둘 다 · 거리 숨김은 그대로
    ///            ④ 화살표가 개수 제한을 넘어도 보고 있는 큐브의 박스는 남고, 잘리는 건 먼 화살표
    /// 시험용 상자만 만들고 끝나면 지운다 — 서버 · 장소 데이터는 건드리지 않는다.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanIndicatorGap()
        {
            Camera cam = null;
            Behaviour pd = null;
            bool pdWas = false;
            Vector3 camPos0 = Vector3.zero;
            Quaternion camRot0 = Quaternion.identity;
            OffScreenIndicator osi = null;
            float tplThreshold = -1f, tplHide = -1f, tplMin = -1f, full = 0f;
            int maxCount = 0;
            var made = new List<GameObject>();
            Target ta = null, tb = null, tc = null, td = null;
            CustomARGeospatialCreatorAnchor ac = null;
            DistanceBasedVisibility dc = null;
            Vector3 posA = Vector3.zero, posB = Vector3.zero, posC = Vector3.zero;
            var arrows = new List<Target>();
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Exception) errors.Add(msg);
            };

            Func<Target, bool> isBox = t => t != null && t.indicator != null && t.indicator.Active && t.indicator.Type == IndicatorType.BOX;
            Func<Target, bool> isArrow = t => t != null && t.indicator != null && t.indicator.Active && t.indicator.Type == IndicatorType.ARROW;
            Func<Target, string> ind = t => t == null ? "없음(지워짐)" : t.indicator == null || !t.indicator.Active ? "없음" : t.indicator.Type == IndicatorType.BOX ? "박스" : "화살표";
            Func<Target, bool> shown = t => t != null && t.GetComponent<Renderer>() != null && t.GetComponent<Renderer>().enabled;
            Func<int> activeAll = () =>
            {
                int n = 0;
                foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude))
                    if (t.indicator != null && t.indicator.Active) n++;
                return n;
            };
            Action<Vector3> look = p => cam.transform.rotation = Quaternion.LookRotation(p - cam.transform.position);
            Action clear = () =>
            {
                foreach (var g in made) if (g != null) UnityEngine.Object.Destroy(g);
                made.Clear();
            };
            // 장소 큐브와 같은 짜임 — 루트에 지오앵커, 같은 오브젝트에 Target · Target 끄기(OffscreenVisibility) · 거리 숨김(DistanceBasedVisibility).
            // 거리 값은 씬의 장소 큐브 틀(DataManager.cubePrefab) 그대로
            Func<string, Vector3, float, Color, Target> cube = (name, pos, size, col) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "H3Test_" + name;
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * size;
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.SetColor("_BaseColor", col);
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.AddComponent<CustomARGeospatialCreatorAnchor>();
                var t = go.AddComponent<Target>();
                t.PlaceName = name;
                var ov = go.AddComponent<OffscreenVisibility>();
                ov.distanceThreshold = tplThreshold;
                var dv = go.AddComponent<DistanceBasedVisibility>();
                dv.hideDistance = tplHide;
                dv.minDistance = tplMin;
                made.Add(go);
                return t;
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                cam = Camera.main;
                DeviceFov();
                foreach (var b in cam.GetComponents<Behaviour>()) if (b.GetType().Name.Contains("PoseDriver")) pd = b;
                pdWas = pd != null && pd.enabled;
                if (pd != null) pd.enabled = false;   // 시험 동안 카메라는 이 시험이 돌린다
                camPos0 = cam.transform.position;
                camRot0 = cam.transform.rotation;
                osi = UnityEngine.Object.FindAnyObjectByType<OffScreenIndicator>(FindObjectsInactive.Include);
                var tpl = DataManager.Instance != null ? DataManager.Instance.cubePrefab : null;
                var tov = tpl != null ? tpl.GetComponentInChildren<OffscreenVisibility>(true) : null;
                var tdv = tpl != null ? tpl.GetComponentInChildren<DistanceBasedVisibility>(true) : null;
                if (tov != null) tplThreshold = tov.distanceThreshold;
                if (tdv != null) { tplHide = tdv.hideDistance; tplMin = tdv.minDistance; }
                full = FilterManager.FullRadiusNow;
                var f = osi != null ? typeof(OffScreenIndicator).GetField("maxNormalIndicatorCount", NPI) : null;
                maxCount = f != null ? (int)f.GetValue(osi) : 0;
                errors.Clear();
                Application.logMessageReceived -= onLog;
                Application.logMessageReceived += onLog;
                log.Add($"  장소 큐브 틀 — Target 끄는 거리 {tplThreshold:0} m · 3D 거리 숨김 {tplMin:0}~{tplHide:0} m · 3D 로 띄우는 반경 {full:0} m · 인디케이터 개수 제한 {maxCount}");
            });
            Check(() => cam != null && osi != null && tplThreshold > 0f && tplHide > 0f && full > tplThreshold && maxCount > 0,
                "준비: 카메라 · 인디케이터 · 장소 큐브 틀의 거리 값 · 3D 반경(Target 끄는 거리보다 넓다) · 개수 제한");

            // ① 높이를 올려 직선거리가 Target 끄는 거리를 넘은 큐브 — 우팡산과 같은 상황 (땅 위 380 m · 165 m 위)
            Do(() =>
            {
                posA = cam.transform.position + new Vector3(0f, 165f, 380f);
                ta = cube("높이올린큐브", posA, 40f, new Color(0.95f, 0.75f, 0.3f));
                look(posA);
            });
            Sleep(1.3f);
            Do(() =>
            {
                Vector3 d = posA - cam.transform.position;
                float straight = d.magnitude;
                d.y = 0f;
                log.Add($"  땅 위 {d.magnitude:0} m · 직선 {straight:0} m (옛 규칙이면 Target {(straight > tplThreshold ? "꺼짐" : "켜짐")}) · " +
                        $"Target {(ta.enabled ? "켜짐" : "꺼짐")} · 3D {(shown(ta) ? "보임" : "숨김")} · 인디케이터 {ind(ta)}");
            });
            Check(() => (posA - cam.transform.position).magnitude > tplThreshold, "직선거리는 Target 끄는 거리를 넘는다 (우팡산과 같은 상황 — 고도를 올리기 전엔 안 넘었다)");
            Check(() => ta.enabled, "Target 이 켜져 있다 — 땅 위 거리로 잰다 (11-16 「이동거리는 평면거리로만」)");
            Check(() => shown(ta), "3D 가 보인다");
            Check(() => isBox(ta), "화면 안이라 박스 인디케이터가 붙는다 — 3D 만 떠 있지 않다");
            Shot("h3ind_raised");

            // ② Target 끄는 거리와 3D 반경 사이 — 예전엔 Target 이 꺼지고(인디케이터 없음) 3D 는 기기에서 앵커가 다시 켰다
            Do(() =>
            {
                float mid = (tplThreshold + full) * 0.5f;
                posB = cam.transform.position + Quaternion.Euler(0f, 40f, 0f) * new Vector3(0f, 0f, mid);
                tb = cube("사이큐브", posB, 40f, new Color(0.3f, 0.8f, 0.5f));
                look(posB);
            });
            Sleep(1.3f);
            Do(() => log.Add($"  {(tplThreshold + full) * 0.5f:0} m 큐브 — Target {(tb.enabled ? "켜짐" : "꺼짐")} · 3D {(shown(tb) ? "보임" : "숨김")} · 인디케이터 {ind(tb)}"));
            Check(() => tb.enabled && shown(tb) && isBox(tb), "Target 끄는 거리와 3D 반경 사이도 3D 와 박스가 함께");
            Do(clear);
            Sleep(0.3f);

            // ③ 기기 규칙 — 앵커 자리가 없으면 3D 도 인디케이터도 없다 · 나침반 자리에 놓이면 둘 다 · 거리 숨김은 그대로
            Do(() =>
            {
                CustomARGeospatialCreatorAnchor.EditorDeviceRule = true;
                posC = cam.transform.position + new Vector3(0f, 0f, 30f);
                tc = cube("앵커없는큐브", posC, 4f, new Color(0.9f, 0.3f, 0.35f));
                ac = tc.GetComponent<CustomARGeospatialCreatorAnchor>();
                dc = tc.GetComponent<DistanceBasedVisibility>();
                look(posC);
            });
            Sleep(0.8f);
            Do(() => log.Add($"  앵커 자리 없음 — 3D {(shown(tc) ? "보임" : "숨김")} · 인디케이터 {ind(tc)}"));
            Check(() => !shown(tc), "앵커 자리가 없으면 거리 안이어도 3D 를 켜지 않는다 — 원점에 인디케이터 없이 뜨지 않게");
            Check(() => !isBox(tc) && !isArrow(tc), "인디케이터도 없다 (둘이 엇갈리지 않는다)");
            Do(() => ac.CompassPlace(posC, Quaternion.identity));
            Sleep(0.6f);
            Do(() => log.Add($"  나침반 자리 — 3D {(shown(tc) ? "보임" : "숨김")} · 인디케이터 {ind(tc)}"));
            Check(() => ac.CompassPlaced && shown(tc) && isBox(tc), "어두워 나침반 자리에 놓이면 3D 와 박스가 함께 (예전엔 앵커가 없다고 3D 만)");
            Do(() => dc.minDistance = 50f);
            Sleep(0.8f);
            Check(() => !shown(tc), "자리가 있어도 거리 숨김은 그대로 — 너무 가까우면 숨긴다");
            Do(() => dc.minDistance = tplMin);
            Sleep(0.8f);
            Check(() => shown(tc), "다시 거리 안이면 보인다");
            Do(() => { CustomARGeospatialCreatorAnchor.EditorDeviceRule = false; clear(); });
            Sleep(0.3f);

            // ④ 화살표가 개수 제한을 넘어도 보고 있는 큐브의 박스는 남는다 — 잘리는 건 먼 화살표
            Do(() =>
            {
                cam.transform.rotation = Quaternion.identity;
                Vector3 c = cam.transform.position;
                int n = maxCount + 2;
                for (int i = 0; i < n; i++)
                {
                    var go = new GameObject("H3Test_Arrow_" + i);
                    go.transform.position = c + Quaternion.Euler(0f, 120f + i * (120f / (n - 1)), 0f) * new Vector3(0f, 0f, 20f + i * 2f);   // 등 뒤
                    var t = go.AddComponent<Target>();
                    t.PlaceName = "화살표" + i;
                    arrows.Add(t);
                    made.Add(go);
                }
                td = cube("보는큐브", c + new Vector3(0f, 0f, 300f), 30f, new Color(0.45f, 0.6f, 0.95f));
            });
            Sleep(0.8f);
            Do(() =>
            {
                int mine = 0;
                foreach (var t in arrows) if (isArrow(t)) mine++;
                log.Add($"  시험 화살표 {arrows.Count}개 중 {mine}개 표시 · 전체 인디케이터 {activeAll()}개 (제한 {maxCount}) · 300 m 큐브 {ind(td)}");
            });
            Check(() => isBox(td), "가장 멀어도 보고 있는 큐브의 박스는 남는다 (예전엔 더 가까운 화살표들에 밀려 잘렸다)");
            Check(() => activeAll() <= maxCount, "모두 합쳐 제한 개수를 넘지 않는다");
            Check(() => isArrow(arrows[0]) && !isArrow(arrows[arrows.Count - 1]), "잘리는 건 먼 화살표 — 가장 가까운 시험 화살표는 남고 가장 먼 것은 빠진다");
            Shot("h3ind_count");

            Do(() =>
            {
                CustomARGeospatialCreatorAnchor.EditorDeviceRule = false;
                clear();
                arrows.Clear();
                if (cam != null) cam.transform.SetPositionAndRotation(camPos0, camRot0);
                if (pd != null) pd.enabled = pdWas;
                Application.logMessageReceived -= onLog;
                if (errors.Count > 0) log.Add("  예외: " + errors[0]);
            });
            Sleep(0.3f);
            Check(() => errors.Count == 0, "예외 없음");
        }
    }
}
