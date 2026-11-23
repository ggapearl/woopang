using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-17 D2 — 목록 사람 줄 오른쪽 사진: 원 + 그 사람의 공개 범위 색 테두리 (전체공개 분홍 · 팔로잉 공개 금색 — 도크 프로필 사진과 같은 고리).
    ///  · personring : 근처 사람 셋(공개 범위 public · followingonly · 값 없음 = 지금 서버)과 지도 친구 하나(서버 응답 JSON 그대로 읽어 Publish — followingonly)를 넣고
    ///    사람 줄 = 원 96 · 가운데 그대로 · 고리 색 · 고리가 사진 누르기를 막지 않음 / 장소 줄 = 둥근 네모 120 · 고리 없음 /
    ///    사람 칩 '숨기기'로 줄을 다시 쓰면 장소 모양 · '전체'로 돌아오면 다시 원. 친구 자리는 운영 서버에 묻지 않는다 (넣은 자료로만).
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanPersonRing()
        {
            const BindingFlags SF = BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags IF = BindingFlags.NonPublic | BindingFlags.Instance;
            const float Disc = 96f;
            float ringSize = (Disc * 0.5f - 3f) / 0.68f * 2f;
            var fm = typeof(R0926FriendsMap);
            var filterWas = UserFilterMode.All;
            string idWas = null, nameWas = null;
            P2PManager pm = null;
            RectTransform tplPhoto = null;
            Texture2D face = null;
            Color pink = ProfileManager.OutlineColor("public"), gold = ProfileManager.OutlineColor("followingonly");
            var want = new Dictionary<string, Color> { { "pr_pub", pink }, { "pr_fol", gold }, { "pr_old", pink }, { "pr_mapf", gold } };

            Func<float, float, (double lat, double lon)> at = (dist, deg) =>
            {
                Vector3 f = Camera.main.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
                Vector3 d = Quaternion.Euler(0f, deg, 0f) * f.normalized * dist;
                double la = VirtualLocation.Instance.Latitude, lo = VirtualLocation.Instance.Longitude;
                return (la + d.z / 111320.0, lo + d.x / (111320.0 * Math.Cos(la * Math.PI / 180.0)));
            };
            Func<string, float, float, string, string> liveJson = (id, dist, deg, vis) =>
            {
                var p = at(dist, deg);
                string mode = vis == null ? "" : ",\"visibility_mode\":\"" + vis + "\"";   // null — 지금 서버처럼 값을 안 보낸다
                return string.Format(CultureInfo.InvariantCulture,
                    "{{\"user_id\":\"{0}\",\"username\":\"{0}\",\"latitude\":{1:0.0000000},\"longitude\":{2:0.0000000},\"altitude\":0,\"avatar_url\":\"\",\"avatar_glb_url\":\"\",\"distance\":{3:0.0}{4}}}", id, p.lat, p.lon, dist, mode);
            };
            string liveRows = null;
            Action live = () => pm.InjectTestUsersData(liveRows);
            // 지도 친구 — 서버 응답(friends_map)의 한 줄을 그대로 Friend 로 읽어 Publish (공개 범위 키 이름까지 본다)
            Action mapFriend = () =>
            {
                var ft = fm.GetNestedType("Friend", BindingFlags.NonPublic);
                var p = at(20f, 60f);
                string json = string.Format(CultureInfo.InvariantCulture,
                    "{{\"user_id\":\"pr_mapf\",\"username\":\"pr_mapf\",\"avatar_url\":\"\",\"visibility_mode\":\"followingonly\",\"lat\":{0:0.0000000},\"lon\":{1:0.0000000},\"ago\":1800,\"precise\":true}}", p.lat, p.lon);
                var arr = Array.CreateInstance(ft, 1);
                arr.SetValue(JsonUtility.FromJson(json, ft), 0);
                fm.GetMethod("Publish", SF).Invoke(null, new object[] { arr });
            };
            // 그 사람의 줄 — ShownEntries 순번 = 줄 순번
            Func<string, Transform> rowOf = id =>
            {
                var plm = Plm();
                var list = ListRows();
                if (plm == null) return null;
                var shown = plm.ShownEntries;
                for (int i = 0; i < shown.Count && i < list.Count; i++)
                    if (shown[i].kind == PlaceListManager.EntryKind.Person && shown[i].id == id) return list[i];
                return null;
            };
            Func<HashSet<string>> people = () =>
            {
                var s = new HashSet<string>();
                var plm = Plm();
                if (plm != null)
                    foreach (var e in plm.ShownEntries)
                        if (e.kind == PlaceListManager.EntryKind.Person && e.id != null && e.id.StartsWith("pr_")) s.Add(e.id);
                return s;
            };
            // 줄 안에서 사진 칸 가운데 — 템플릿 자리(오른쪽 기준)에서 셈한 값과 견준다
            Func<RectTransform, Vector2> centerOf = rt => (Vector2)rt.localPosition + rt.rect.center;
            Func<Transform, Vector2> wantCenter = row =>
            {
                var rr = ((RectTransform)row).rect;
                Vector2 anchor = rr.min + Vector2.Scale(rr.size, tplPhoto.anchorMin);
                return anchor + tplPhoto.anchoredPosition + Vector2.Scale(new Vector2(0.5f, 0.5f) - tplPhoto.pivot, tplPhoto.sizeDelta);
            };
            Func<Color, Color, bool> same = (a, b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
            Func<Color, string> hex = c => "#" + ColorUtility.ToHtmlStringRGB(c);
            // 사람 줄 모양 — 원 96 · 가운데 그대로 · 고리(크기 · 가운데 · 색)
            Func<string, string> personShape = id =>
            {
                var row = rowOf(id);
                if (row == null) return "줄 없음";
                var ph = (RectTransform)row.Find("Photo");
                var bg = ph.GetComponent<Image>();
                var ring = row.Find("PersonRing") as RectTransform;
                var ri = ring != null ? ring.GetComponent<Image>() : null;
                var c = wantCenter(row);
                var bad = new List<string>();
                if (bg.sprite == null || bg.sprite.name != "r0926_circle" || bg.type != Image.Type.Simple) bad.Add("마스크 " + (bg.sprite != null ? bg.sprite.name : "없음") + "/" + bg.type);
                if (Vector2.Distance(ph.sizeDelta, new Vector2(Disc, Disc)) > 0.5f) bad.Add("크기 " + ph.sizeDelta);
                if (Vector2.Distance(centerOf(ph), c) > 0.5f) bad.Add($"가운데 {centerOf(ph)} ≠ {c}");
                if (ri == null || !ring.gameObject.activeSelf) bad.Add("고리 없음");
                else
                {
                    if (ri.sprite == null || ri.sprite.name != "r0926_ring_mini") bad.Add("고리 그림 " + (ri.sprite != null ? ri.sprite.name : "없음"));
                    if (Mathf.Abs(ring.sizeDelta.x - ringSize) > 0.5f) bad.Add($"고리 크기 {ring.sizeDelta.x:0.0}");
                    if (Vector2.Distance(centerOf(ring), c) > 0.5f) bad.Add($"고리 가운데 {centerOf(ring)}");
                    if (!same(ri.color, want[id])) bad.Add($"고리 색 {hex(ri.color)} ≠ {hex(want[id])}");
                    if (ri.raycastTarget) bad.Add("고리가 누르기를 받음");
                    if (ring.GetSiblingIndex() != ph.GetSiblingIndex() + 1) bad.Add("고리 순서");
                }
                return bad.Count == 0 ? null : string.Join(" · ", bad);
            };
            // 장소 줄 모양 — 템플릿 그대로(둥근 네모 120) · 고리 꺼짐
            Func<int, string> placeShapes = minRows =>
            {
                var plm = Plm();
                var list = ListRows();
                var shown = plm.ShownEntries;
                var tbg = tplPhoto.GetComponent<Image>();
                int n = 0;
                var bad = new List<string>();
                for (int i = 0; i < shown.Count && i < list.Count; i++)
                {
                    if (shown[i].kind == PlaceListManager.EntryKind.Person) continue;
                    n++;
                    var ph = (RectTransform)list[i].Find("Photo");
                    var bg = ph.GetComponent<Image>();
                    var ring = list[i].Find("PersonRing");
                    if (bg.sprite != tbg.sprite || bg.type != tbg.type || Mathf.Abs(bg.pixelsPerUnitMultiplier - tbg.pixelsPerUnitMultiplier) > 0.001f) bad.Add($"{i}번 마스크 {(bg.sprite != null ? bg.sprite.name : "없음")}/{bg.type}");
                    if (Vector2.Distance(ph.sizeDelta, tplPhoto.sizeDelta) > 0.5f || Vector2.Distance(ph.anchoredPosition, tplPhoto.anchoredPosition) > 0.5f) bad.Add($"{i}번 칸 {ph.sizeDelta} @ {ph.anchoredPosition}");
                    if (ring != null && ring.gameObject.activeSelf) bad.Add($"{i}번 고리 켜짐");
                }
                log.Add($"  장소 줄 {n}개 — 템플릿 마스크 {(tbg.sprite != null ? tbg.sprite.name : "없음")} · 칸 {tplPhoto.sizeDelta.x:0}");
                if (n < minRows) bad.Add($"장소 줄 {n}개 < {minRows}");
                return bad.Count == 0 ? null : string.Join(" · ", bad);
            };

            Do(() => R0926FriendsMap.EditorBase = "http://127.0.0.1:9");   // 친구 자리 받기는 닫힌 포트로 — 운영 서버에 묻지 않고 넣은 자료로만
            NmBoot(false);
            Do(() =>
            {
                pm = P2PManager.Instance;
                if (pm == null || VirtualLocation.Instance == null || Camera.main == null) throw new Exception("P2PManager · 가상 위치 · 카메라 없음");
                var pr = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>(FindObjectsInactive.Include);
                var tpl = pr != null ? new SerializedObject(pr).FindProperty("rowTemplate").objectReferenceValue as GameObject : null;
                tplPhoto = tpl != null ? tpl.transform.Find("Photo") as RectTransform : null;
                if (tplPhoto == null) throw new Exception("목록 줄 템플릿 Photo 없음");
                idWas = typeof(P2PManager).GetField("currentUserId", IF).GetValue(pm) as string;
                nameWas = typeof(P2PManager).GetField("currentUsername", IF).GetValue(pm) as string;
                SfLogin(true, "pr_me");   // 지도 친구는 로그인한 사람만 — 시험 아이디만 (로그인 이벤트 · 토큰 없이)
                pm.SetTestUserId("pr_me", "pr_me");
                filterWas = pm.GetUserFilterMode();
                if (filterWas != UserFilterMode.All) pm.SetUserFilterMode(UserFilterMode.All);   // 플레이 동안만 (저장값은 그대로)
                liveRows = "{\"users\":[" + string.Join(",", liveJson("pr_pub", 10f, 0f, "public"), liveJson("pr_fol", 14f, 30f, "followingonly"), liveJson("pr_old", 16f, -30f, null)) + "]}";
                live();
                mapFriend();
            });
            Check(() =>
            {
                var so = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>(FindObjectsInactive.Include));
                var c = so.FindProperty("circleSprite").objectReferenceValue as Sprite;
                var r = so.FindProperty("personRingSprite").objectReferenceValue as Sprite;
                log.Add($"  원 {(c != null ? c.name : "없음")} · 고리 {(r != null ? r.name : "없음")} · 사람 사진 {so.FindProperty("personPhotoSize").floatValue:0}");
                return c != null && c.name == "r0926_circle" && r != null && r.name == "r0926_ring_mini";
            }, "적용 — 목록 줄에 원 그림(r0926_circle) · 고리 그림(r0926_ring_mini) 연결");
            Check(() =>
            {
                var data = (System.Collections.IDictionary)typeof(P2PManager).GetField("nearbyUsersData", IF).GetValue(pm);
                string V(string id) => data.Contains(id) ? ((NearbyUserData)data[id]).visibility_mode : "(없음)";
                var mf = R0926FriendsMap.Shared.FirstOrDefault(f => f.id == "pr_mapf");
                log.Add($"  근처 사람 pr_pub '{V("pr_pub")}' · pr_fol '{V("pr_fol")}' · pr_old '{V("pr_old")}' · 지도 친구 pr_mapf '{mf?.vis}'");
                return V("pr_pub") == "public" && V("pr_fol") == "followingonly" && V("pr_old") == "" && mf != null && mf.vis == "followingonly";
            }, "서버 응답의 visibility_mode 를 읽는다 — 근처 사람 · 지도 친구 (값이 없으면 빈 값)");
            Check(() =>
            {
                log.Add($"  색: 전체공개 {hex(pink)} · 팔로잉 공개 {hex(gold)} · 비공개 {hex(ProfileManager.OutlineColor("private"))} · 값 없음 {hex(ProfileManager.OutlineColor(null))}");
                return hex(pink) == "#E95383" && hex(gold) == "#FFD700" && hex(ProfileManager.OutlineColor("private")) == "#808080" && same(ProfileManager.OutlineColor(null), pink) && same(ProfileManager.OutlineColor(""), pink);
            }, "공개 범위 색 = 도크 프로필 사진 색 (분홍 · 금색 · 회색 — 값이 없으면 전체공개 분홍)");

            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => Modes()?.Show(0));
            Sleep(0.6f);
            Wait(() =>
            {
                if (!pm.GetActiveAvatars().ContainsKey("pr_pub")) live();   // 운영 근처 사람 받기(5초마다)가 시험 사람을 지웠으면 다시
                return people().SetEquals(want.Keys);
            }, 6f, "목록에 시험 사람 넷");

            // 1) 사람 줄 — 원 + 공개 범위 색 고리
            foreach (var id in want.Keys)
            {
                string pid = id;
                Check(() =>
                {
                    string bad = personShape(pid);
                    if (bad != null) log.Add($"  {pid}: {bad}");
                    return bad == null;
                }, $"사람 줄 {pid} — 원 {Disc:0} · 가운데 그대로 · 고리 {(same(want[pid], gold) ? "금색(팔로잉 공개)" : "분홍(전체공개)")}");
            }
            // 2) 장소 줄 — 템플릿 모양 그대로
            Check(() =>
            {
                string bad = placeShapes(1);
                if (bad != null) log.Add("  " + bad);
                return bad == null;
            }, "장소 줄 — 둥근 네모 120 그대로 · 고리 없음");
            // 3) 고리가 사진 누르기를 막지 않는다 — 사진 가운데를 누르면 사진 칸(정보 창 단추)이 먼저 받는다
            //    고리만 걸리는 자리 — 사진 가장자리 바로 바깥(반지름 49.5)은 고리 위지만 사진 칸 밖이라 줄(길안내)이 받는다
            Func<Vector2, string> hitAt = local =>
            {
                var row = rowOf("pr_fol");
                var ph = row != null ? (RectTransform)row.Find("Photo") : null;
                var es = EventSystem.current;
                if (ph == null || es == null) return "줄 · 이벤트 시스템 없음";
                var canvas = ph.GetComponentInParent<Canvas>().rootCanvas;
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 scr = RectTransformUtility.WorldToScreenPoint(cam, ph.TransformPoint(ph.rect.center + local));
                var hits = new List<RaycastResult>();
                es.RaycastAll(new PointerEventData(es) { position = scr }, hits);
                return hits.Count > 0 ? hits[0].gameObject.name : "없음";
            };
            Wait(() => hitAt(Vector2.zero) != "없음", 3f, "목록이 누르기를 받을 때까지 (열리는 전환이 끝나면)");
            Check(() =>
            {
                string mid = hitAt(Vector2.zero), edge = hitAt(new Vector2(0f, Disc * 0.5f + 1.5f));
                log.Add($"  사진 가운데 → '{mid}' · 사진 바로 바깥(고리 위) → '{edge}'");
                return mid == "Photo" && edge != "PersonRing";
            }, "고리는 누르기를 받지 않는다 — 사진은 정보 창 단추 그대로 · 고리 위는 줄로");

            // 4) 줄을 다시 쓴다 — 사람 칩 '숨기기'면 사람 줄 자리에 장소가 와도 둥근 네모 · '전체'로 돌아오면 다시 원
            Do(() => pm.SetUserFilterMode(UserFilterMode.None));
            Wait(() => people().Count == 0, 3f, "숨기기 — 목록에서 사람 빠짐");
            Sleep(0.3f);   // 목록 줄은 LateUpdate 에서 글이 바뀐 것을 보고 다시 그린다 — 항목이 바뀐 그 프레임엔 아직 옛 줄
            Check(() =>
            {
                string bad = placeShapes(1);
                int on = ListRows().Count(r => r.Find("PersonRing") != null && r.Find("PersonRing").gameObject.activeSelf);
                if (bad != null)
                {
                    log.Add("  " + bad);
                    var shown = Plm().ShownEntries;
                    var list = ListRows();
                    for (int i = 0; i < 4 && i < shown.Count && i < list.Count; i++)
                        log.Add($"  {i}번 줄 글 '{list[i].Find("Name").GetComponent<Text>().text}' · 항목 '{shown[i].name}' ({shown[i].kind})");
                }
                log.Add($"  숨기기: 사람 {people().Count}명 · 켜진 고리 {on}");
                return bad == null && on == 0 && people().Count == 0;
            }, "사람 칩 '숨기기' — 사람이 쓰던 줄을 장소가 받아도 둥근 네모로 (고리 꺼짐)");
            Do(() => { pm.SetUserFilterMode(UserFilterMode.All); live(); });
            Wait(() =>
            {
                if (!pm.GetActiveAvatars().ContainsKey("pr_pub")) live();
                return people().SetEquals(want.Keys);
            }, 6f, "전체 — 시험 사람 넷 다시");
            Sleep(0.3f);
            Check(() =>
            {
                var bad = want.Keys.Select(id => (id, why: personShape(id))).Where(x => x.why != null).ToList();
                foreach (var (id, why) in bad) log.Add($"  {id}: {why}");
                string pb = placeShapes(1);
                if (pb != null) log.Add("  " + pb);
                return bad.Count == 0 && pb == null;
            }, "사람 칩 '전체' — 다시 원 + 고리 색 그대로 · 장소 줄은 둥근 네모");

            // 눈으로 보기 — 사진이 있는 것처럼 (색 띠 그림) 원으로 잘리는지 · 도크 프로필 사진과 비교 (캡처는 로컬에만)
            Do(() =>
            {
                face = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                var px = new Color32[64 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        px[y * 64 + x] = Color32.Lerp(new Color32(40, 120, 200, 255), new Color32(130, 220, 190, 255), y / 63f);
                face.SetPixels32(px);
                face.Apply();
                foreach (var id in new[] { "pr_pub", "pr_fol" })
                {
                    var pic = rowOf(id)?.Find("Photo/Pic")?.GetComponent<RawImage>();
                    if (pic == null) continue;
                    pic.texture = face;
                    pic.uvRect = new Rect(0f, 0f, 1f, 1f);
                    pic.enabled = true;
                    pic.canvasRenderer.SetAlpha(1f);
                }
            });
            Sleep(0.3f);
            Shot("personring_list");

            // 되돌리기 — 사람 칩 · 시험 사람 · 넣은 친구 자리 · 시험 아이디 · 서버 주소(NmFinish)
            Do(() =>
            {
                foreach (var id in new[] { "pr_pub", "pr_fol" })
                {
                    var pic = rowOf(id)?.Find("Photo/Pic")?.GetComponent<RawImage>();
                    if (pic != null && pic.texture == face) { pic.texture = null; pic.enabled = false; }
                }
                if (face != null) UnityEngine.Object.Destroy(face);
                if (pm.GetUserFilterMode() != filterWas) pm.SetUserFilterMode(filterWas);
                ((System.Collections.IList)fm.GetField("shared", SF).GetValue(null)).Clear();
                fm.GetField("sharedAt", SF).SetValue(null, float.NegativeInfinity);
                fm.GetField("sharedUser", SF).SetValue(null, null);
                (fm.GetField("SharedChanged", SF).GetValue(null) as Action)?.Invoke();
                foreach (var id in want.Keys) pm.RemoveTestUserAvatar(id);
                pm.SetTestUserId(idWas, nameWas);
                SfLogin(false);
                log.Add($"  되돌림: 사람 칩 {pm.GetUserFilterMode()} · 시험 사람 {pm.GetActiveAvatars().Keys.Count(k => k.StartsWith("pr_"))}명 남음");
            });
            Back();
            Sleep(0.5f);
            Do(NmFinish);
        }
    }
}
