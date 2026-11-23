using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-17 D1 — 설정 › 사람 › 사용자 오브젝트 유효시간 (1시간 / 6시간 / 24시간 / 일주일 · 기본 일주일) 에 따라 목록 · AR 의 사람이 같게.
    ///  · peoplewin : 근처 사람(지금 앱을 켠 사람) 1명 + 지도 친구(앱을 꺼 둔 친구 — 마지막 접속 15분 · 3시간 · 10시간 · 3일 · 9일 · 거리 슬라이더 밖)를 넣고
    ///    네 칸마다 목록 줄 = AR 오브젝트 = 기대값 · 사람 칩 '숨기기'면 둘 다 없음(숨긴 채 풀로 간 오브젝트도 다시 보임) · 가까이 카드는 지금 여기 있는 사람만 ·
    ///    설정 줄 4칸 · 5개 언어 한 줄 맞춤. 친구 자리는 운영 서버에 묻지 않는다 (R0926FriendsMap.EditorBase 를 닫힌 포트로 — 넣은 자료로만).
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanPeopleWin()
        {
            const BindingFlags SF = BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags IF = BindingFlags.NonPublic | BindingFlags.Instance;
            const string Row = "ListPanel/Clip0926/Sheet0926/SettingsPanel0926/Viewport/Content/GroupPeople/RowPeopleWindow";
            const string WinKey = "People_Window";
            string[] winName = { "1시간", "6시간", "24시간", "일주일" };
            var fm = typeof(R0926FriendsMap);
            bool hadWin = false;
            int winWas = R0926PeopleSettings.Default;
            var filterWas = UserFilterMode.All;
            string me = null, langWas = null, idWas = null, nameWas = null;
            float range = 5000f;
            P2PManager pm = null;

            // 카메라 앞(수평) dist 미터 · 오른쪽 deg 도 — 에디터는 사람을 카메라 + (동, 북) 으로 놓는다 (P2PManager.UpdateAvatarPositionInEditor)
            Func<float, float, (double lat, double lon)> at = (dist, deg) =>
            {
                Vector3 f = Camera.main.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
                Vector3 d = Quaternion.Euler(0f, deg, 0f) * f.normalized * dist;
                double la = VirtualLocation.Instance.Latitude, lo = VirtualLocation.Instance.Longitude;
                return (la + d.z / 111320.0, lo + d.x / (111320.0 * Math.Cos(la * Math.PI / 180.0)));
            };
            Func<string, float, float, string> liveJson = (id, dist, deg) =>
            {
                var p = at(dist, deg);
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"user_id\":\"{0}\",\"username\":\"{0}\",\"latitude\":{1:0.0000000},\"longitude\":{2:0.0000000},\"altitude\":0,\"avatar_url\":\"\",\"avatar_glb_url\":\"\",\"distance\":{3:0.0}}}", id, p.lat, p.lon, dist);
            };
            Action<string[]> live = list => pm.InjectTestUsersData("{\"users\":[" + string.Join(",", list) + "]}");
            // 지도 친구 자리를 받은 것처럼 (R0926FriendsMap.Publish 와 같은 결과 — 서버에 묻지 않는다)
            Action<(string id, float dist, float deg, double ago)[]> friends = list =>
            {
                var shared = (System.Collections.IList)fm.GetField("shared", SF).GetValue(null);
                shared.Clear();
                double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                foreach (var (id, dist, deg, ago) in list)
                {
                    var p = at(dist, deg);
                    shared.Add(new R0926FriendsMap.SharedFriend { id = id, name = id, avatar = "", lat = p.lat, lon = p.lon, seenAt = now - ago });
                }
                fm.GetField("sharedAt", SF).SetValue(null, Time.realtimeSinceStartup);
                fm.GetField("sharedUser", SF).SetValue(null, me);
                (fm.GetField("SharedChanged", SF).GetValue(null) as Action)?.Invoke();
            };
            Func<HashSet<string>> listIds = () =>
            {
                var s = new HashSet<string>();
                var plm = Plm();
                if (plm != null)
                    foreach (var e in plm.ShownEntries)
                        if (e.kind == PlaceListManager.EntryKind.Person && e.id != null && e.id.StartsWith("pw_")) s.Add(e.id);
                return s;
            };
            Func<HashSet<string>> arIds = () =>
            {
                var s = new HashSet<string>();
                foreach (var kv in pm.GetActiveAvatars())
                    if (kv.Key.StartsWith("pw_") && kv.Value != null && kv.Value.activeInHierarchy) s.Add(kv.Key);
                return s;
            };
            Func<IEnumerable<string>, string> show = s => s.Any() ? string.Join(",", s.OrderBy(x => x)) : "없음";
            Func<GameObject, bool> seen = go =>
            {
                if (go == null) return false;
                foreach (var r in go.GetComponentsInChildren<Renderer>(false))
                    if (r.enabled && !r.forceRenderingOff && (r is MeshRenderer || r is SkinnedMeshRenderer)) return true;
                return false;
            };
            Func<HashSet<string>> nearIds = () =>
            {
                var s = new HashSet<string>();
                var items = NmField<System.Collections.IList>("items");
                if (items == null) return s;
                foreach (var it in items)
                {
                    var p = it.GetType().GetField("person").GetValue(it) as P2PUserInfo;
                    if (p != null && p.GetUserId() != null) s.Add(p.GetUserId());
                }
                return s;
            };

            // 근처 사람 1 · 지도 친구 — 15분 · 3시간 · 10시간 · 3일 · 9일(언제나 빠짐) · 30분이지만 거리 슬라이더 밖(언제나 빠짐)
            var roster = new List<(string id, float dist, float deg, double ago)>
            {
                ("pw_m15", 18f, 40f, 15 * 60.0),
                ("pw_h3", 24f, 80f, 3 * 3600.0),
                ("pw_h10", 30f, 120f, 10 * 3600.0),
                ("pw_d3", 36f, 160f, 3 * 86400.0),
                ("pw_d9", 42f, 200f, 9 * 86400.0),
            };
            var expect = new[]
            {
                new HashSet<string> { "pw_live", "pw_m15" },
                new HashSet<string> { "pw_live", "pw_m15", "pw_h3" },
                new HashSet<string> { "pw_live", "pw_m15", "pw_h3", "pw_h10" },
                new HashSet<string> { "pw_live", "pw_m15", "pw_h3", "pw_h10", "pw_d3" },
            };
            string liveRow = null;

            Do(() => R0926FriendsMap.EditorBase = "http://127.0.0.1:9");   // 친구 자리 받기는 닫힌 포트로 — 운영 서버에 묻지 않고 넣은 자료로만
            NmBoot(false);
            Do(() =>
            {
                pm = P2PManager.Instance;
                if (pm == null || VirtualLocation.Instance == null || Camera.main == null) throw new Exception("P2PManager · 가상 위치 · 카메라 없음");
                idWas = typeof(P2PManager).GetField("currentUserId", IF).GetValue(pm) as string;
                nameWas = typeof(P2PManager).GetField("currentUsername", IF).GetValue(pm) as string;
                // 지도 친구는 로그인한 사람만 — 시험 아이디만 넣는다 (SfLogin 과 같이 로그인 이벤트 · 토큰 없이: 다른 매니저가 운영 서버에 시험 아이디로 묻지 않게)
                me = "pw_me";
                SfLogin(true, me);
                pm.SetTestUserId(me, me);
                hadWin = PlayerPrefs.HasKey(WinKey);
                winWas = R0926PeopleSettings.Window;
                filterWas = pm.GetUserFilterMode();
                langWas = AppLanguage.EditorOverride;
                range = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
                roster.Add(("pw_far", range + 2000f, 240f, 30 * 60.0));
                log.Add($"  거리 슬라이더 {range:0}m · 유효시간 저장값 {(hadWin ? winName[winWas] : "없음(기본 일주일)")} · 사람 칩 {filterWas}");
                if (filterWas != UserFilterMode.All) pm.SetUserFilterMode(UserFilterMode.All);   // 플레이 동안만 (저장값은 그대로)
                liveRow = liveJson("pw_live", 12f, 0f);
                live(new[] { liveRow });
                friends(roster.ToArray());
            });
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => Modes()?.Show(0));
            Sleep(0.6f);

            // 1) 네 칸 — 목록 줄 = AR 오브젝트 = 기대값
            for (int k = 0; k < 4; k++)
            {
                int w = k;
                bool ok = false;
                string got = "";
                Do(() => R0926PeopleSettings.Window = w);
                Wait(() =>
                {
                    if (!pm.GetActiveAvatars().ContainsKey("pw_live")) live(new[] { liveRow });   // 운영 근처 사람 받기(5초마다)가 시험 사람을 지웠으면 다시
                    var l = listIds();
                    var a = arIds();
                    got = $"목록 {show(l)} / AR {show(a)}";
                    ok = l.SetEquals(expect[w]) && a.SetEquals(expect[w]);
                    return ok;
                }, 6f, $"유효시간 {winName[w]}");
                Check(() => { log.Add("  " + got); return ok; }, $"유효시간 {winName[w]} — 목록 · AR 이 같게: {show(expect[w])} (9일 전 · 슬라이더 밖 친구는 언제나 빠짐)");
            }
            Check(() =>
            {
                var stale = new[] { "pw_m15", "pw_h3", "pw_h10", "pw_d3" }.All(pm.IsStale);
                var data = (System.Collections.IDictionary)typeof(P2PManager).GetField("nearbyUsersData", IF).GetValue(pm);
                bool clean = !data.Contains("pw_m15") && !data.Contains("pw_d3");
                log.Add($"  지도 친구 표시 {stale} · 근처 사람 자료에 지도 친구 없음 {clean} · 지금 켠 사람 pw_live 지도 친구 아님 {!pm.IsStale("pw_live")}");
                return stale && clean && !pm.IsStale("pw_live");
            }, "AR 의 지도 친구는 '앱을 꺼 둔 친구'로 따로 — 근처 사람 자료(목록 · 친구 지도가 '지금 켠 사람'으로 쓰는 것)엔 넣지 않는다");
            Check(() =>
            {
                bool all = expect[3].All(id => pm.GetActiveAvatars().TryGetValue(id, out var go) && seen(go));
                log.Add("  AR 에 보임: " + show(expect[3].Where(id => pm.GetActiveAvatars().TryGetValue(id, out var go) && seen(go))));
                return all;
            }, "일주일 — 다섯 사람 모두 AR 에 보인다 (렌더러 켜짐)");

            // 2) 지도 친구가 앱을 켰다 — 같은 사람이 근처 사람으로 오면 지도 친구 표시가 풀리고 한 오브젝트로
            Do(() => live(new[] { liveRow, liveJson("pw_h3", 24f, 80f) }));
            Sleep(0.4f);
            Check(() =>
            {
                bool one = arIds().Count(id => id == "pw_h3") == 1;
                log.Add($"  pw_h3 지도 친구 {pm.IsStale("pw_h3")} · 목록 {show(listIds())}");
                return !pm.IsStale("pw_h3") && one && pm.GetActiveAvatars().ContainsKey("pw_h3");
            }, "지도 친구가 앱을 켜면 — 근처 사람(지금 자리)으로 바뀐다 (지도 친구 표시 풀림 · 한 오브젝트)");
            Do(() => live(new[] { liveRow }));
            Sleep(0.4f);
            Check(() =>
            {
                log.Add($"  pw_h3 지도 친구 {pm.IsStale("pw_h3")} · AR {show(arIds())}");
                return pm.IsStale("pw_h3") && pm.GetActiveAvatars().ContainsKey("pw_h3");
            }, "다시 앱을 끄면(근처 사람에서 빠지면) — 마지막 자리로 AR 에 남는다 (일주일 안)");

            // 3) 사람 칩 '숨기기' — 목록 · AR 모두 없음 → '전체' — 숨긴 채 풀로 간 오브젝트를 다시 써도 보인다
            Do(() => pm.SetUserFilterMode(UserFilterMode.None));
            Sleep(0.3f);
            Do(() => live(new[] { liveRow }));   // '숨기기' 동안 온 근처 사람 — 숨긴 채 풀로 돌아간다
            Sleep(1.2f);
            Check(() =>
            {
                var l = listIds();
                var visible = arIds().Where(id => seen(pm.GetActiveAvatars()[id])).ToList();
                bool staleGone = !new[] { "pw_m15", "pw_h3", "pw_h10", "pw_d3" }.Any(id => pm.GetActiveAvatars().ContainsKey(id));
                log.Add($"  숨기기: 목록 {show(l)} · AR 에 보이는 사람 {show(visible)} · 지도 친구 내림 {staleGone}");
                return l.Count == 0 && visible.Count == 0 && staleGone;
            }, "사람 칩 '숨기기' — 목록 · AR 모두 사람 없음 (지도 친구도 바로 내림)");
            Do(() => { pm.SetUserFilterMode(UserFilterMode.All); live(new[] { liveRow }); });
            {
                bool ok = false;
                string got = "";
                Wait(() =>
                {
                    if (!pm.GetActiveAvatars().ContainsKey("pw_live")) live(new[] { liveRow });
                    var l = listIds();
                    var a = arIds();
                    bool liveSeen = pm.GetActiveAvatars().TryGetValue("pw_live", out var go) && seen(go);
                    got = $"목록 {show(l)} / AR {show(a)} · pw_live 보임 {liveSeen}";
                    ok = l.SetEquals(expect[3]) && a.SetEquals(expect[3]) && liveSeen;
                    return ok;
                }, 6f, "다시 '전체'");
                Check(() => { log.Add("  " + got); return ok; }, "다시 '전체' — 목록 · AR 이 다시 같게 · '숨기기' 동안 풀로 간 오브젝트를 다시 써도 보인다");
            }

            // 4) 가까이 카드 — 지금 여기 있는 사람만 (앱을 꺼 둔 지도 친구는 AR 에는 보여도 카드로 띄우지 않는다)
            Back();
            Sleep(0.8f);
            // 시험은 근처 카드를 꺼 두고 시작한다(EditorQuiet) — 이 단계부터 켠다 · 「발견한 장소」는 AR 화면이 뜬 지 10초 뒤부터라 그만큼 지난 것으로 · 앱 시작 「근처 장소」는 이미 뜬 것으로
            Do(() =>
            {
                R0926NearModal.EditorQuiet = false;
                if (nmModal != null) { nmModal.EditorStartShown = true; nmModal.EditorLive(30f); nmModal.EditorForget(); }
            });
            Do(() =>
            {
                roster.Add(("pw_near", 1.4f, -25f, 5 * 60.0));
                friends(roster.ToArray());
                live(new[] { liveRow, liveJson("pw_nl", 1.5f, 25f) });
            });
            {
                bool ok = false;
                string got = "";
                Wait(() =>
                {
                    if (!pm.GetActiveAvatars().ContainsKey("pw_nl")) live(new[] { liveRow, liveJson("pw_nl", 1.5f, 25f) });
                    var n = nearIds();
                    got = $"카드 {show(n.Where(x => x.StartsWith("pw_")))} · AR 에 pw_near {pm.GetActiveAvatars().ContainsKey("pw_near")}(지도 친구 {pm.IsStale("pw_near")})";
                    ok = n.Contains("pw_nl") && !n.Contains("pw_near") && pm.GetActiveAvatars().ContainsKey("pw_near");
                    return ok;
                }, 5f, "가까이 카드");
                Check(() => { log.Add("  " + got); return ok; }, "가까이 카드 — 1.5m 의 지금 켠 사람은 뜨고 · 1.4m 의 지도 친구(5분 전)는 AR 에만 (카드 없음)");
            }
            Do(() =>
            {
                roster.Add(("pw_nl", 1.5f, 25f, 60.0));   // 방금(1분 전)까지 켜 있던 친구 — 지도 친구로 남는다
                friends(roster.ToArray());
                live(new[] { liveRow });
            });
            {
                bool ok = false;
                string got = "";
                Wait(() =>
                {
                    var n = nearIds();
                    got = $"카드 {show(n.Where(x => x.StartsWith("pw_")))} · pw_nl 지도 친구 {pm.IsStale("pw_nl")} · AR 에 있음 {pm.GetActiveAvatars().ContainsKey("pw_nl")}";
                    ok = !n.Contains("pw_nl") && pm.IsStale("pw_nl") && pm.GetActiveAvatars().ContainsKey("pw_nl");
                    return ok;
                }, 4f, "카드에 떠 있던 사람이 앱을 껐다");
                Check(() => { log.Add("  " + got); return ok; }, "카드에 떠 있던 사람이 앱을 끄면 — 카드에서 내리고 AR 엔 마지막 자리로 남는다");
            }

            // 5) 기기 자리 맞춤 셈 (P2PAvatarPlacer) — 앵커 축 x 동 · z 북 · 50m 안은 원만 옮김 · 높이는 3m 넘게 달라질 때만
            Check(() =>
            {
                Vector3 n = P2PAvatarPlacer.Enu(37.0, 127.0, 37.001, 127.0);
                Vector3 e = P2PAvatarPlacer.Enu(37.0, 127.0, 37.0, 127.001);
                double ex = 111.32 * Math.Cos(37.0 * Math.PI / 180.0);
                bool enu = Mathf.Abs(n.z - 111.32f) < 0.01f && Mathf.Abs(n.x) < 1e-4f && Math.Abs(e.x - ex) < 0.01 && Mathf.Abs(e.z) < 1e-4f;
                bool glide = P2PAvatarPlacer.Glide(new Vector3(30f, 0f, 0f)) && P2PAvatarPlacer.Glide(new Vector3(30f, 0f, 39f)) && !P2PAvatarPlacer.Glide(new Vector3(40f, 0f, 40f));
                bool level = P2PAvatarPlacer.Level(0f, 2f) == 0f && P2PAvatarPlacer.Level(0f, 4f) == 4f && P2PAvatarPlacer.Level(5f, 3f) == 5f && P2PAvatarPlacer.Level(5f, 1.5f) == 1.5f;
                log.Add($"  북 0.001° = {n.z:0.00}m · 동 0.001° = {e.x:0.00}m (기대 {ex:0.00}) · 50m 안 원만 {glide} · 높이 {level}");
                return enu && glide && level;
            }, "기기 자리 맞춤 셈 — 동 · 북 거리 · 50m 넘으면 앵커를 새로 · 높이는 3m 넘게 달라질 때만");
            Check(() =>
            {
                var tpl = pm.GetType().GetField("userAvatarPrefab", IF)?.GetValue(pm) as GameObject;
                if (tpl == null) foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>()) if (go.name == "P2P_Avatar" && go.scene.IsValid() && go.transform.parent == null) { tpl = go; break; }
                bool ok = tpl != null && tpl.GetComponent<CustomARGeospatialCreatorAnchor>() != null && tpl.GetComponent<P2PAvatarPlacer>() != null;
                var one = pm.GetActiveAvatars().TryGetValue("pw_live", out var a) ? a : null;
                bool spawned = one != null && one.GetComponent<P2PAvatarPlacer>() != null && one.GetComponent<CustomARGeospatialCreatorAnchor>() != null;
                var anc = one != null ? one.GetComponent<CustomARGeospatialCreatorAnchor>() : null;
                log.Add($"  원본 P2P_Avatar 앵커 · 자리 맞춤 {ok} · 띄운 사람에도 {spawned} · 에디터는 앵커 좌표 없음(나침반 모드가 건너뜀) {(anc != null && !anc.HasCoordinates)}");
                return ok && spawned && anc != null && !anc.HasCoordinates;
            }, "사람 오브젝트에 지오스페이셜 앵커 + 자리 맞춤 — 기기에서 AR 시작 자리(원점)에 놓이지 않게 (에디터는 예전대로 카메라 기준)");

            // 6) 설정 › 사람 › 사용자 오브젝트 유효시간 — 4칸 · 누르면 바뀜 · 5개 언어 한 줄
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => Modes()?.Show(2));
            Sleep(0.8f);
            Check(() =>
            {
                var seg = Find(Row + "/Seg");
                int n = seg != null ? seg.transform.childCount : 0;
                string words = seg == null ? "" : string.Join(" / ", Enumerable.Range(0, n).Select(i => seg.transform.GetChild(i).GetComponentInChildren<Text>(true)?.text));
                var title = Find(Row + "/Title")?.GetComponent<Text>()?.text;
                log.Add($"  「{title}」 · {n}칸: {words}");
                return n == 4 && title == "사용자 오브젝트 유효시간" && words == "1시간 / 6시간 / 24시간 / 일주일";
            }, "설정 줄 「사용자 오브젝트 유효시간」 — 1시간 / 6시간 / 24시간 / 일주일");
            Do(() => Find(Row + "/Seg/Step0").GetComponent<Button>().onClick.Invoke());
            Sleep(0.3f);
            Check(() =>
            {
                float a0 = Find(Row + "/Seg/Step0").GetComponent<Image>().color.a, a3 = Find(Row + "/Seg/Step3").GetComponent<Image>().color.a;
                log.Add($"  1시간 누름 → 저장값 {R0926PeopleSettings.Window} · 칸 밝기 {a0:0.00}/{a3:0.00}");
                return R0926PeopleSettings.Window == 0 && PlayerPrefs.GetInt(WinKey, -1) == 0 && a0 > 0.1f && a3 < 0.01f;
            }, "1시간 칸을 누르면 — 저장되고 그 칸이 밝게");
            {
                bool ok = false;
                string got = "";
                Wait(() =>
                {
                    if (!pm.GetActiveAvatars().ContainsKey("pw_live")) live(new[] { liveRow });
                    var a = arIds();
                    got = "AR " + show(a);
                    ok = a.Contains("pw_live") && a.Contains("pw_m15") && !a.Contains("pw_h3") && !a.Contains("pw_d3");
                    return ok;
                }, 4f, "설정에서 1시간");
                Check(() => { log.Add("  " + got); return ok; }, "설정 칸을 누르면 AR 도 바로 — 1시간 (15분 전 친구만 · 3시간 · 3일 전 친구는 내림)");
            }
            Do(() => Find(Row + "/Seg/Step3").GetComponent<Button>().onClick.Invoke());
            Sleep(0.3f);
            Check(() => R0926PeopleSettings.Window == 3 && Find(Row + "/Seg/Step3").GetComponent<Image>().color.a > 0.1f, "일주일 칸을 누르면 — 다시 일주일");
            Check(() =>
            {
                bool ok = true;
                var row = Find(Row);
                if (row == null) return false;
                var texts = row.GetComponentsInChildren<R0926LocalizedText>(true);
                foreach (var lang in new[] { "ko", "en", "ja", "zh", "es" })
                {
                    AppLanguage.EditorOverride = lang;
                    foreach (var lt in texts) lt.Set(lt.ko, lt.en, lt.ja, lt.zh, lt.es);
                    Canvas.ForceUpdateCanvases();
                    foreach (var lt in texts)
                    {
                        var t = lt.GetComponent<Text>();
                        float need = t.preferredWidth, box = t.rectTransform.rect.width;
                        if (need > box + 1f) { ok = false; log.Add($"  넘침 {lang} {lt.name} '{t.text}' 칸 {box:0} < 글 {need:0}"); }
                    }
                }
                AppLanguage.EditorOverride = langWas;
                foreach (var lt in texts) lt.Set(lt.ko, lt.en, lt.ja, lt.zh, lt.es);
                Canvas.ForceUpdateCanvases();
                log.Add($"  글 {texts.Length}칸 × 5개 언어 — 제목 칸 {Find(Row + "/Title").GetComponent<RectTransform>().rect.width:0} · 칸 하나 {Find(Row + "/Seg/Step0").GetComponent<RectTransform>().rect.width:0}");
                return ok && texts.Length == 6;
            }, "설정 줄 — 5개 언어(한 · 영 · 일 · 중 · 스페인어) 모두 한 줄로 칸 안에");
            Sleep(0.3f);
            Shot("peoplewin_settings");

            // 되돌리기 — 유효시간 저장값 · 사람 칩 · 시험 사람 · 넣은 친구 자리 · 서버 주소(NmFinish)
            Do(() =>
            {
                R0926PeopleSettings.Window = winWas;
                if (!hadWin) { PlayerPrefs.DeleteKey(WinKey); PlayerPrefs.Save(); typeof(R0926PeopleSettings).GetField("window", SF).SetValue(null, -1); }
                if (pm.GetUserFilterMode() != filterWas) pm.SetUserFilterMode(filterWas);
                ((System.Collections.IList)fm.GetField("shared", SF).GetValue(null)).Clear();
                fm.GetField("sharedAt", SF).SetValue(null, float.NegativeInfinity);
                (fm.GetField("SharedChanged", SF).GetValue(null) as Action)?.Invoke();
                foreach (var id in new[] { "pw_live", "pw_m15", "pw_h3", "pw_h10", "pw_d3", "pw_d9", "pw_far", "pw_near", "pw_nl" }) pm.RemoveTestUserAvatar(id);
                pm.SetTestUserId(idWas, nameWas);
                SfLogin(false);
                log.Add($"  되돌림: 유효시간 {(PlayerPrefs.HasKey(WinKey) ? winName[R0926PeopleSettings.Window] : "저장값 없음(기본 일주일)")} · 사람 칩 {pm.GetUserFilterMode()} · 시험 사람 {arIds().Count}명 남음");
            });
            Back();
            Sleep(0.5f);
            Do(NmFinish);
        }
    }
}
