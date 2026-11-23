using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Redesign0926
{
    /// <summary>
    /// 11-17 B9 — 더블터치하면 서브사진이 창과 함께 바로 뜨는지 (대표님: 「더블터치해서 사진발생하는 건 진짜 중요한 거라서 바로바로」).
    ///  · dtspeed : 가까운 큐브(서브사진 있는 것) 5개를 차례로 처음 여는 것처럼 열고, 창이 보이기 시작한 때와 사진이 보인 때를 프레임마다 잰다.
    ///  · dtcache : 디스크 캐시(SubPhotoCache) — 네트워크마다 0.8초 늦춰(LTE 흉내) 가까운 큐브 첫 장을 미리 받아 두는지, 메모리에는 안 푸는지,
    ///              열면 창과 같이 뜨는지 · 캐시에 없을 때 · 한 번 눌렀을 때 · 오래된 것 지우기(1시간 · 3일 · 용량)
    /// 운영 서버는 읽기만 한다(앱이 사진을 받는 것과 같은 요청).
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private const System.Reflection.BindingFlags DsF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        private const System.Reflection.BindingFlags CsF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        private static readonly List<DoubleTap3D> dsTargets = new List<DoubleTap3D>();
        private static readonly List<float> dsPhotoTimes = new List<float>();
        private static DoubleTap3D dsCur;
        private static float dsT0, dsCanvasAt, dsPhotoAt;
        private static int dsTogether, dsOpened;

        private static List<string> DsUrls(DoubleTap3D d) => d != null ? (List<string>)typeof(DoubleTap3D).GetField("imageUrls", DsF).GetValue(d) : null;
        private static object DsField(DoubleTap3D d, string n) => typeof(DoubleTap3D).GetField(n, DsF)?.GetValue(d);
        private static int DsOwned(DoubleTap3D d) => d != null ? ((List<Sprite>)DsField(d, "ownedPhotos")).Count : -1;

        // 캐시 안을 들여다본다 — 주소 → 파일 (pre: 미리 받고 안 연 것 · seen: 열어 본 것)
        private static string CsRoot => (string)typeof(SubPhotoCache).GetField("root", CsF).GetValue(null);
        private static string CsKey(string src)
        {
            var norm = (string)typeof(SubPhotoCache).GetMethod("Norm", CsF).Invoke(null, new object[] { src });
            return (string)typeof(SubPhotoCache).GetMethod("Key", CsF).Invoke(null, new object[] { norm });
        }
        private static string CsWhere(string src)
        {
            string r = CsRoot, k = CsKey(src);
            if (r == null) return null;
            if (File.Exists(r + "/seen/" + k)) return "seen";
            return File.Exists(r + "/pre/" + k) ? "pre" : null;
        }
        private static void CsForget(string src)
        {
            string r = CsRoot, k = CsKey(src);
            foreach (var p in new[] { r + "/seen/" + k, r + "/pre/" + k }) if (File.Exists(p)) File.Delete(p);
        }

        private static void DsSample()
        {
            if (dsCur == null || dsCur.fullscreenCanvasGroup == null) return;
            float t = Time.realtimeSinceStartup - dsT0;
            var cg = dsCur.fullscreenCanvasGroup;
            var img = dsCur.fullscreenImage;
            float ca = cg.gameObject.activeSelf ? cg.alpha : 0f;
            bool photo = img != null && img.enabled && img.sprite != null && img.color.a > 0.5f;
            if (dsCanvasAt < 0f && ca > 0.05f) dsCanvasAt = t;
            if (dsPhotoAt < 0f && photo && ca > 0.05f) dsPhotoAt = t;
        }

        // 원본 · 768 썸네일 크기 (머리만 — 내려받지 않는다)
        private static string DsSizes(string path)
        {
            if (string.IsNullOrEmpty(path)) return "-";
            string Head(string url)
            {
                try
                {
                    var rq = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                    rq.Method = "HEAD"; rq.Timeout = 8000;
                    using (var rs = (System.Net.HttpWebResponse)rq.GetResponse()) return rs.ContentLength >= 0 ? (rs.ContentLength / 1024) + "KB" : "?";
                }
                catch (Exception e) { return "실패(" + e.GetType().Name + ")"; }
            }
            if (path.StartsWith("http")) return "바깥 주소 " + Head(ApiConfig.Secure(path));
            string clean = path.Replace("\\", "/").TrimStart('/');
            return "원본 " + Head(ApiConfig.MAIN_SERVER + "/" + clean) + " · 768 썸네일 " + Head(ApiConfig.MAIN_SERVER + "/thumb/768/" + clean);
        }

        private static void DsBoot()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
        }

        // 서브사진 있는 큐브 — 가까운 순
        private static void DsPick(int n)
        {
            Wait(() =>
            {
                dsTargets.Clear();
                if (DataManager.Instance == null || Camera.main == null) return false;
                var map = DataManager.Instance.GetPlaceDataMap();
                var cam = Camera.main.transform.position;
                foreach (var kv in DataManager.Instance.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy || !map.TryGetValue(kv.Key, out var p) || p.model_type != "cube") continue;
                    if (DataManager.Instance.IsAnimCategory(kv.Key)) continue;
                    var d = kv.Value.GetComponentInChildren<DoubleTap3D>(true);
                    var u = DsUrls(d);
                    if (d != null && d.IsReady && u != null && u.Count > 0) dsTargets.Add(d);
                }
                dsTargets.Sort((a, b) => (a.transform.position - cam).sqrMagnitude.CompareTo((b.transform.position - cam).sqrMagnitude));
                return dsTargets.Count >= n;
            }, 40f, "서브사진 있는 큐브");
            Do(() => { if (dsTargets.Count > n) dsTargets.RemoveRange(n, dsTargets.Count - n); });
        }

        // 처음 여는 것처럼 열고 창 · 사진이 보인 때를 잰다
        private static void DsOpen(Func<DoubleTap3D> pick, string label, string shot = null)
        {
            Do(() =>
            {
                dsCur = pick();
                if (dsCur == null) return;
                typeof(DoubleTap3D).GetField("isCooldown", DsF).SetValue(dsCur, false);
                dsCanvasAt = dsPhotoAt = -1f;
                dsT0 = Time.realtimeSinceStartup;
                EditorApplication.update -= DsSample;
                EditorApplication.update += DsSample;
                typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", DsF).Invoke(dsCur, null);
            });
            Wait(() => dsCur == null || dsPhotoAt >= 0f, 15f, label + " 사진");
            Do(() =>
            {
                if (dsCur == null) return;
                dsOpened++;
                var img = dsCur.fullscreenImage;
                string dims = img != null && img.sprite != null && img.sprite.texture != null ? img.sprite.texture.width + "x" + img.sprite.texture.height : "-";
                bool together = dsPhotoAt >= 0f && dsPhotoAt <= dsCanvasAt + 0.05f;
                if (together) dsTogether++;
                if (dsPhotoAt >= 0f) dsPhotoTimes.Add(dsPhotoAt);
                log.Add($"  {label}) id {DsField(dsCur, "id")} · 창 보임 {dsCanvasAt:0.000}초 · 사진 보임 {(dsPhotoAt >= 0f ? dsPhotoAt.ToString("0.000") + "초" : "15초 안에 안 뜸")} · {(together ? "창과 같이" : "창 먼저 → 사진 나중")} · 사진 {dims}");
            });
            if (shot != null) Shot(shot);
            Do(() => { if (dsCur != null) dsCur.closeButton.onClick.Invoke(); });
            Wait(() => dsCur == null || !(bool)DsField(dsCur, "isFullscreen"), 3f, "닫힘");
            Do(() => EditorApplication.update -= DsSample);
            Sleep(0.3f);
        }

        private static void DsReset() => Do(() => { dsPhotoTimes.Clear(); dsTogether = dsOpened = 0; });

        private static float DsMax() => dsPhotoTimes.Count > 0 ? dsPhotoTimes.Max() : -1f;

        private static void PlanDtSpeed()
        {
            const int N = 5;
            DsBoot();
            DsPick(N);
            Sleep(1.5f);   // 미리 받기가 돌 시간 (실제 사람도 둘러본 뒤 누른다)
            DsReset();
            Do(() =>
            {
                var first = dsTargets.Count > 0 ? dsTargets[0] : null;
                log.Add($"  큐브 {dsTargets.Count}개 · 첫 화면이 장소 글(placeInfoTextPanel) {(first != null && first.placeInfoTextPanel != null)}");
                foreach (var d in dsTargets)
                {
                    var u = DsUrls(d);
                    log.Add($"   · id {DsField(d, "id")} '{DsField(d, "placeName")}' · 사진 {u.Count}장 · 첫 장 {DsSizes(u[0])}");
                }
            });
            for (int i = 0; i < N; i++)
            {
                int k = i;
                DsOpen(() => k < dsTargets.Count ? dsTargets[k] : null, (k + 1).ToString(), k == 0 ? "dtspeed_first" : null);
            }
            Do(() =>
            {
                dsCur = null;
                var s = dsPhotoTimes.OrderBy(x => x).ToList();
                float med = s.Count > 0 ? s[s.Count / 2] : -1f;
                log.Add($"  요약: 연 것 {dsOpened} · 사진이 창과 같이 {dsTogether} · 사진 보인 때 가운데 {med:0.000}초 · 가장 늦은 {DsMax():0.000}초");
            });
        }

        private static void PlanDtCache()
        {
            const int N = 5;
            const float Delay = 0.8f;
            int hits0 = 0, miss0 = 0;
            DoubleTap3D tapT = null;

            Do(() =>
            {
                string r = Path.Combine(Application.temporaryCachePath, "subphotos");
                try { if (Directory.Exists(r)) Directory.Delete(r, true); } catch (Exception e) { log.Add("  캐시 폴더 비우기 실패: " + e.Message); }
                SubPhotoCache.SimulatedNetDelay = Delay;
                log.Add($"  캐시 폴더 비움 · 네트워크로 받을 때마다 {Delay}초 늦춤 (휴대폰 LTE 흉내)");
            });
            DsBoot();
            DsPick(N);

            // 1) 열기 전에 — 앱이 고르는 순서(앞쪽 먼저 · 가까운 8곳) 그대로 첫 장이 디스크에 (메모리에는 없음)
            var top = new List<DoubleTap3D>();
            Wait(() =>
            {
                top.Clear();
                var cam = Camera.main;
                if (cam == null) return false;
                var live = (IEnumerable<DoubleTap3D>)typeof(DoubleTap3D).GetField("live", CsF).GetValue(null);
                var srcOf = typeof(DoubleTap3D).GetProperty("PrefetchSrc", DsF);
                var ranked = new List<KeyValuePair<float, DoubleTap3D>>();
                foreach (var d in live)
                {
                    if (d == null || srcOf.GetValue(d) == null) continue;
                    Vector3 v = d.transform.position - cam.transform.position;
                    float s = v.sqrMagnitude;
                    if (Vector3.Dot(cam.transform.forward, v) < 0f) s *= 4f;
                    ranked.Add(new KeyValuePair<float, DoubleTap3D>(s, d));
                }
                ranked.Sort((a, b) => a.Key.CompareTo(b.Key));
                top.AddRange(ranked.Take(8).Select(x => x.Value));
                return top.Count >= N && top.All(d => CsWhere(DsUrls(d)[0]) != null);
            }, 40f, "가까운 8곳 첫 장이 디스크에");
            Do(() =>
            {
                var map = DataManager.Instance.GetPlaceDataMap();
                int id(DoubleTap3D d) => (int)DsField(d, "id");
                dsTargets.Clear();
                dsTargets.AddRange(top.Where(d => map.TryGetValue(id(d), out var p) && p.model_type == "cube" && d.IsReady && d.placeInfoTextPanel == null).Take(N));
                log.Add($"  미리 받기 순위 8곳 중 우팡 큐브 {dsTargets.Count}곳을 연다 · 8곳 첫 장 디스크에 {top.Count(d => CsWhere(DsUrls(d)[0]) != null)}/{top.Count}");
                log.Add($"  미리 받은 사진 {SubPhotoCache.Prefetched}장");
                foreach (var d in dsTargets)
                    log.Add($"   · id {DsField(d, "id")} · 사진 {DsUrls(d).Count}장 · 첫 장 {CsWhere(DsUrls(d)[0]) ?? "없음"} · 메모리 사진 {DsOwned(d)}");
            });
            Check(() => top.Count >= N && top.All(d => CsWhere(DsUrls(d)[0]) == "pre") && dsTargets.Count == N, "가까운 8곳(앞쪽 먼저) 첫 장을 열기 전에 디스크에 받아 둔다");
            Check(() => dsTargets.All(d => d.imageSprites.Count == 0 && DsOwned(d) == 0), "미리 받은 건 디스크에만 — 메모리에 푼 사진은 없다 (10-23 아이폰 꺼짐 재발 방지)");

            // 2) 열면 창과 같이
            DsReset();
            Do(() => { hits0 = SubPhotoCache.OpenHits; miss0 = SubPhotoCache.OpenMisses; });
            for (int i = 0; i < N; i++)
            {
                int k = i;
                DsOpen(() => k < dsTargets.Count ? dsTargets[k] : null, (k + 1).ToString(), k == 0 ? "dtcache_first" : null);
                if (k > 0) continue;
                // 첫 곳 사진 전부 — 크기와 메모리 (옛 원본이면 장당 100MB 넘게)
                Wait(() => dsTargets.Count == 0 || (bool)DsField(dsTargets[0], "photosLoaded"), 30f, "첫 곳 사진 다 받음");
                Do(() =>
                {
                    if (dsTargets.Count == 0) return;
                    var sp = dsTargets[0].imageSprites;
                    long mem = sp.Where(s => s != null && s.texture != null).Sum(s => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(s.texture));
                    log.Add($"  id {DsField(dsTargets[0], "id")} 사진 {sp.Count}장 · {string.Join(" ", sp.Select(s => s != null && s.texture != null ? s.texture.width + "x" + s.texture.height : "-"))} · 메모리 {mem >> 20}MB");
                });
            }
            Do(() => log.Add($"  요약: 연 것 {dsOpened} · 사진이 창과 같이 {dsTogether} · 가장 늦은 {DsMax():0.000}초 · 디스크에서 {SubPhotoCache.OpenHits - hits0}장 · 받아서 {SubPhotoCache.OpenMisses - miss0}장"));
            Check(() => dsOpened == N && dsTogether == N, "미리 받아 둔 곳은 사진이 창과 같이 뜬다 (5곳 모두)");
            Check(() => DsMax() >= 0f && DsMax() < Delay, $"사진이 네트워크 늦춤({Delay}초)보다 먼저 뜬다 — 디스크에서 바로");
            Check(() => dsTargets.All(d => CsWhere(DsUrls(d)[0]) == "seen"), "연 사진은 '열어 본' 쪽으로 옮긴다 (마지막으로 본 뒤 3일 보관)");
            Check(() =>
            {
                var holder = typeof(DoubleTap3D).GetField("photoHolder", CsF).GetValue(null) as DoubleTap3D;
                int others = dsTargets.Where(d => d != holder).Sum(d => DsOwned(d));
                log.Add($"  사진을 든 곳 id {(holder != null ? DsField(holder, "id") : "없음")} · 다른 곳 메모리 사진 {others}장");
                return dsTargets.Count > 0 && holder == dsTargets[dsTargets.Count - 1] && others == 0;
            }, "메모리에는 지금 연 한 곳 사진만 (예전 그대로)");

            // 3) 캐시에 없으면 — 받아서 뜬다 (창 먼저, 늦춤만큼 뒤에)
            Do(() => { CsForget(DsUrls(dsTargets[0])[0]); dsPhotoTimes.Clear(); dsTogether = 0; });
            DsOpen(() => dsTargets[0], "캐시에 없는 1");
            Check(() => dsPhotoTimes.Count == 1 && dsPhotoTimes[0] >= Delay && CsWhere(DsUrls(dsTargets[0])[0]) == "seen", $"캐시에 없으면 받아서(그리고 남겨서) 뜬다 — 늦춤({Delay}초) 뒤");

            // 4) 한 번 누르면 앞 사진 세 장을 먼저 받기 시작
            Do(() =>
            {
                tapT = dsTargets.Skip(1).FirstOrDefault(d => DsUrls(d).Count >= 3 && DsOwned(d) == 0);
                if (tapT == null) { log.Add("  사진 3장 이상인 다른 큐브 없음 — 건너뜀"); return; }
                int n = Math.Min(4, DsUrls(tapT).Count);
                for (int i = 0; i < n; i++) CsForget(DsUrls(tapT)[i]);
                log.Add($"  id {DsField(tapT, "id")} 앞 사진 {n}장을 캐시에서 지우고 한 번 누름");
                typeof(DoubleTap3D).GetMethod("PrefetchOnTap", DsF).Invoke(tapT, null);
            });
            Wait(() => tapT == null || Enumerable.Range(0, 3).All(i => CsWhere(DsUrls(tapT)[i]) == "pre"), 3 * Delay + 5f, "한 번 누른 곳 앞 사진 세 장 디스크에");
            Check(() => tapT == null || Enumerable.Range(0, 3).All(i => CsWhere(DsUrls(tapT)[i]) == "pre") && DsOwned(tapT) == 0
                        && (DsUrls(tapT).Count < 4 || CsWhere(DsUrls(tapT)[3]) == null), "한 번 누르면 앞 사진 세 장만 디스크로 먼저 받는다 (메모리엔 안 푼다)");

            // 5) 오래된 것 지우기 — 시험용 빈 폴더에서
            Check(() =>
            {
                string r = Path.Combine(Application.temporaryCachePath, "subphotos_sweeptest").Replace('\\', '/');
                if (Directory.Exists(r)) Directory.Delete(r, true);
                Directory.CreateDirectory(r + "/pre"); Directory.CreateDirectory(r + "/seen");
                var now = DateTime.UtcNow;
                void Make(string rel, TimeSpan age, int kb = 1)
                {
                    File.WriteAllBytes(r + "/" + rel, new byte[kb * 1024]);
                    File.SetLastWriteTimeUtc(r + "/" + rel, now - age);
                }
                Make("pre/old", TimeSpan.FromHours(2)); Make("pre/new", TimeSpan.FromMinutes(5));
                Make("seen/old", TimeSpan.FromDays(4)); Make("seen/new", TimeSpan.FromDays(1));
                Make("pre/x.1.tmp", TimeSpan.FromMinutes(20)); Make("pre/y.2.tmp", TimeSpan.Zero);
                var sweep = typeof(SubPhotoCache).GetMethod("Sweep", CsF);
                sweep.Invoke(null, new object[] { r, now, 1L << 40 });
                bool ttl = !File.Exists(r + "/pre/old") && File.Exists(r + "/pre/new") && !File.Exists(r + "/seen/old") && File.Exists(r + "/seen/new")
                           && !File.Exists(r + "/pre/x.1.tmp") && File.Exists(r + "/pre/y.2.tmp");
                log.Add($"  시간: 안 연 2시간 {(File.Exists(r + "/pre/old") ? "남음" : "지움")} · 안 연 5분 {(File.Exists(r + "/pre/new") ? "남음" : "지움")} · 본 지 4일 {(File.Exists(r + "/seen/old") ? "남음" : "지움")} · 본 지 1일 {(File.Exists(r + "/seen/new") ? "남음" : "지움")} · 받다 만 20분 {(File.Exists(r + "/pre/x.1.tmp") ? "남음" : "지움")} · 받는 중 {(File.Exists(r + "/pre/y.2.tmp") ? "남음" : "지움")}");

                Directory.Delete(r, true);
                Directory.CreateDirectory(r + "/seen");
                Make("seen/l1", TimeSpan.FromHours(3), 100); Make("seen/l2", TimeSpan.FromHours(2), 100); Make("seen/l3", TimeSpan.FromHours(1), 100);
                sweep.Invoke(null, new object[] { r, now, 250L * 1024 });
                bool cap = !File.Exists(r + "/seen/l1") && !File.Exists(r + "/seen/l2") && File.Exists(r + "/seen/l3");
                log.Add($"  용량(시험 250KB · 300KB 있음): 오래된 l1 {(File.Exists(r + "/seen/l1") ? "남음" : "지움")} · l2 {(File.Exists(r + "/seen/l2") ? "남음" : "지움")} · 최근 l3 {(File.Exists(r + "/seen/l3") ? "남음" : "지움")}");
                Directory.Delete(r, true);
                return ttl && cap;
            }, "안 연 것 1시간 · 본 것 3일 · 받다 만 것 10분 뒤 지우고, 용량을 넘으면 오래된 것부터 (3/4 까지)");

            Do(() => { SubPhotoCache.SimulatedNetDelay = 0f; dsCur = null; });
        }

    }
}
