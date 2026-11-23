using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-17 D4 — 목록에서 장소 줄을 누르면 바로 안내하지 않고 「○○으로 안내할까요?」.
    ///  · guideask : 받침(으로 · 로) 고르기 / 장소 줄 → 묻는 창(그 이름 · 안내는 아직 · 목록 그대로 · 목록 X 까지 덮음) →
    ///    취소 · 뒤로가기 · 바깥 누르기 = 아무 일 없이 닫힘(목록 그대로) → 확인 = 그 장소로 안내 시작 · 목록 내려감 /
    ///    묻는 동안 목록이 닫히면 창도 닫힘 / 사람 줄은 묻지 않음
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanGuideAsk()
        {
            const string Ask = "GuideAsk0926";
            const string No = Ask + "/Box0926/Buttons0926/Guide_NO";
            const string Yes = Ask + "/Box0926/Buttons0926/Guide_YES";
            const string Label = Ask + "/Box0926/Text";
            const string DockList = "Dock0926/List_Button";
            const string ListX = "ListPanel/DockMirror0926/XButton_List";
            const string Person = "ga_person";
            R0926GuideArrow arrow = null;
            R0926Notch notch = null;
            R0926PlaceRows pr = null;
            P2PManager pm = null;
            var filterWas = UserFilterMode.All;
            PlaceListManager.ListEntry asked = default;
            int askedRow = -1;
            string hit = null;

            Func<bool> guiding = () => (arrow != null && arrow.EditorGuiding) || (notch != null && notch.EditorGuiding);
            Func<bool> asking = () => Active(Ask);
            Func<string> label = () => Find(Label)?.GetComponent<Text>()?.text ?? "";
            Func<float> closeAt = () => pr != null ? (float)typeof(R0926PlaceRows).GetField("closeAt", NPI).GetValue(pr) : -9f;
            Func<bool> listUsable = () =>
            {
                var g = Find("ListPanel")?.GetComponent<CanvasGroup>();
                return Active("ListPanel") && (g == null || (g.alpha >= 0.99f && g.blocksRaycasts && g.interactable)) && closeAt() < 0f;
            };
            Func<string> state = () => $"묻는 창 {(asking() ? "열림" : "-")} · 안내 {(guiding() ? "중" : "-")} · 목록 {(Active("ListPanel") ? "열림" : "닫힘")} · 닫기 예약 {(closeAt() < 0f ? "없음" : "있음")}";
            // 손으로 누른 것과 같게 — 그 화면 자리를 맞혀 맨 위에 맞는 것에 누름을 보낸다 (부모 버튼까지 올라간다)
            Func<Vector2, string> tapScreen = pos =>
            {
                var es = EventSystem.current;
                if (es == null) return "-";
                var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = pos };
                var hits = new List<RaycastResult>();
                es.RaycastAll(ped, hits);
                if (hits.Count == 0) return "없음";
                ped.pointerCurrentRaycast = hits[0];
                ped.pointerPressRaycast = hits[0];
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, ped, ExecuteEvents.pointerClickHandler);
                return hits[0].gameObject.name;
            };
            Func<string, string> tapPath = path => { var rt = Find(path) as RectTransform; return rt == null ? "-" : tapScreen(ScreenOf(rt)); };
            // 줄의 이름 쪽(왼쪽 30%) — 오른쪽 사진 칸을 피해서
            Func<Transform, Vector2> namePoint = row =>
            {
                var rt = (RectTransform)row;
                var r = rt.rect;
                return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(new Vector3(r.xMin + r.width * 0.3f, r.center.y, 0f)));
            };
            Func<Transform, bool> onTop = row =>
            {
                var es = EventSystem.current;
                if (es == null) return false;
                var hits = new List<RaycastResult>();
                es.RaycastAll(new PointerEventData(es) { position = namePoint(row) }, hits);
                return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(row);
            };
            // 안내하는 장소 줄 — 사람 · 보물찾기 · 축제 · 보호소 · 방향 없는 줄은 빼고, 지금 화면에서 눌리는 것
            int GuideRow()
            {
                var p = Plm();
                var rows = ListRows();
                if (p == null) return -1;
                for (int i = 0; i < rows.Count && i < p.ShownEntries.Count; i++)
                {
                    var e = p.ShownEntries[i];
                    if (e.noDirection || e.kind == PlaceListManager.EntryKind.Person || e.kind == PlaceListManager.EntryKind.Hunt
                        || e.kind == PlaceListManager.EntryKind.Festival || e.kind == PlaceListManager.EntryKind.Shelter) continue;
                    if (onTop(rows[i])) return i;
                }
                return -1;
            }
            void AskOn(string what)
            {
                Wait(() => GuideRow() >= 0, 20f, what + ": 목록에 안내할 장소 줄");
                Do(() =>
                {
                    askedRow = GuideRow();
                    if (askedRow < 0) { log.Add("  장소 줄 없음"); return; }
                    asked = Plm().ShownEntries[askedRow];
                    hit = tapScreen(namePoint(ListRows()[askedRow]));
                    log.Add($"  {what}: {askedRow + 1}번째 줄 ({asked.kind}) 누름 → 맞은 것 {hit}");
                });
                Wait(asking, 3f, what + ": 줄 → 묻는 창");
                Sleep(0.35f);   // 카드가 올라오는 동안
            }
            Func<float, float, (double lat, double lon)> at = (dist, deg) =>
            {
                Vector3 f = Camera.main.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
                Vector3 d = Quaternion.Euler(0f, deg, 0f) * f.normalized * dist;
                double la = VirtualLocation.Instance.Latitude, lo = VirtualLocation.Instance.Longitude;
                return (la + d.z / 111320.0, lo + d.x / (111320.0 * Math.Cos(la * Math.PI / 180.0)));
            };

            // ── 0) 받침 — 이름 끝 글자로 '으로' / '로' ──
            Check(() =>
            {
                var cases = new (string name, string want)[]
                {
                    ("스타벅스 강남점", "으로"), ("카페", "로"), ("서울", "로"), ("카페(본점)", "으로"), ("우팡산!", "으로"),
                    ("GS25", "로"), ("주차장 3", "으로"), ("10", "으로"), ("CU", "로"), ("Itaewon", "으로"), ("Building", "으로"), ("Hotel", "로"),
                    ("", "(으)로"), ("東京", "(으)로"),
                };
                var bad = new List<string>();
                foreach (var c in cases)
                {
                    string got = R0926GuideAsk.Ro(c.name);
                    if (got != c.want) bad.Add($"{c.name}→{got}(바람 {c.want})");
                }
                log.Add("  받침: " + (bad.Count == 0 ? cases.Length + "개 다 맞음" : string.Join(" · ", bad)));
                return bad.Count == 0;
            }, "이름 뒤 '으로/로' — 받침 · ㄹ · 괄호 · 숫자 · 영문");

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Wait(() => DataManager.Instance != null && DataManager.Instance.GetSpawnedObjects().Count > 0, 40f, "주변 장소가 AR 에 뜸");
            Sleep(2f);
            Do(() =>
            {
                arrow = UnityEngine.Object.FindAnyObjectByType<R0926GuideArrow>(FindObjectsInactive.Include);
                notch = UnityEngine.Object.FindAnyObjectByType<R0926Notch>(FindObjectsInactive.Include);
                pr = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>(FindObjectsInactive.Include);
                R0926GuideArrow.Stop();
                R0926Notch.StopGuide();
            });
            Check(() =>
            {
                var ga = pr != null ? new SerializedObject(pr).FindProperty("guideAsk").objectReferenceValue as R0926GuideAsk : null;
                var a = Find(Ask);
                var l = Find("ListPanel");
                bool order = a != null && l != null && a.parent == l.parent && a.GetSiblingIndex() == l.GetSiblingIndex() + 1;
                log.Add($"  묻는 창 {(a != null ? "있음" : "없음")} · 목록 줄 연결 {(ga != null ? ga.name : "없음")} · 목록 바로 위 층 {order} · 화살표 {(arrow != null)} · 노치 {(notch != null)}");
                return ga != null && ga.name == Ask && !asking() && order && arrow != null;
            }, "준비: 묻는 창 GuideAsk0926 (평소 꺼짐 · 목록 바로 위 층) · 목록 줄에 연결");

            // ── 1) 줄을 누르면 묻기만 한다 → 취소 ──
            Do(() => Click(DockList));
            Sleep(1.5f);
            AskOn("첫째");
            Do(() => log.Add("  " + state() + " · 글: " + label()));
            Check(() => hit != null && hit != "없음" && hit != "-", "줄의 이름 쪽을 누르면 그 줄이 받음");
            Check(() => !guiding(), "누르기만 해서는 안내가 시작되지 않음 (11-16 D4 — 예전엔 바로 안내)");
            Check(() => Active("ListPanel") && closeAt() < 0f, "목록은 그대로 (내려갈 예약 없음)");
            Check(() =>
            {
                string want = "<b>" + asked.name.Trim() + "</b>" + R0926GuideAsk.Ro(asked.name.Trim()) + " 안내할까요?";
                return R0926LocalizedText.Lang() == "ko" ? label() == want : label().Contains(asked.name.Trim());
            }, "묻는 글 = 「(그 장소 이름)으로/로 안내할까요?」");
            Check(() =>
            {
                var t = Find(Label)?.GetComponent<Text>();
                var n = Find(No + "/Label0926")?.GetComponent<Text>();
                var y = Find(Yes + "/Label0926")?.GetComponent<Text>();
                log.Add($"  버튼: 왼쪽 「{n?.text}」 · 오른쪽 「{y?.text}」 · 글 크기 {t?.fontSize}");
                return t != null && t.supportRichText && n != null && y != null
                    && (R0926LocalizedText.Lang() != "ko" || (n.text == "취소" && y.text == "확인"));
            }, "왼쪽 취소 · 오른쪽 확인 ('삭제 요청' 묻는 창과 같은 카드)");
            HitCheck(() => Find(Yes) as RectTransform, false, "확인 버튼이 맨 위 (눌림)");
            HitCheck(() => Find(No) as RectTransform, false, "취소 버튼이 맨 위 (눌림)");
            HitCheck(() => Find(ListX) as RectTransform, true, "묻는 동안 목록 X 는 덮임 (어두운 바탕)");
            HitCheck(() => askedRow >= 0 && askedRow < ListRows().Count ? ListRows()[askedRow] as RectTransform : null, true, "묻는 동안 목록 줄도 덮임 (한 번 더 잘못 눌리지 않게)");
            Shot("guideask_dialog");
            Do(() => hit = tapPath(No));
            Wait(() => !asking(), 2f, "취소 → 닫힘");
            Sleep(0.3f);
            Do(() => log.Add("  취소 누름 (" + hit + ") → " + state()));
            Check(() => hit == "Guide_NO" || hit == "Label0926", "취소 자리를 누르면 취소 버튼이 받음");
            Check(() => !asking() && !guiding() && listUsable(), "취소: 안내 없이 닫히고 목록은 그대로 (눌림)");

            // ── 2) 뒤로가기 = 취소 (목록까지 닫지 않는다) ──
            AskOn("둘째");
            Do(BackNow);
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Wait(() => !asking(), 3f, "뒤로가기 → 닫힘");
            Sleep(0.6f);
            Do(() => log.Add("  뒤로가기 → " + state()));
            Check(() => !asking() && !guiding() && listUsable(), "뒤로가기: 묻는 창만 닫히고 목록은 그대로 · 안내 없음");

            // ── 3) 바깥(어두운 곳) 누르기 = 취소 ──
            AskOn("셋째");
            Do(() =>
            {
                var box = Find(Ask + "/Box0926") as RectTransform;
                var corners = new Vector3[4];
                box.GetWorldCorners(corners);
                float below = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y;
                var p = new Vector2(Screen.width * 0.5f, below * 0.45f);   // 카드 아래 어두운 곳
                hit = tapScreen(p);
                log.Add($"  바깥 누름 (카드 아래 {p.y:0}px / 화면 {Screen.height}px) → 맞은 것 {hit}");
            });
            Wait(() => !asking(), 2f, "바깥 → 닫힘");
            Sleep(0.3f);
            Check(() => hit == Ask, "카드 밖을 누르면 어두운 바탕(GuideAsk0926)이 받음");
            Check(() => !asking() && !guiding() && listUsable(), "바깥 누르기: 안내 없이 닫히고 목록은 그대로");

            // ── 4) 카드 안 글자를 눌러도 닫히지 않는다 ──
            AskOn("넷째");
            Do(() => hit = tapPath(Label));
            Sleep(0.3f);
            Check(() => asking() && !guiding(), "카드 안(글자)을 누르면 그대로 (닫히지도 · 안내하지도 않음)");

            // ── 5) 확인 = 그 장소로 안내 · 목록 내려감 ──
            Do(() => hit = tapPath(Yes));
            Wait(() => !asking() && guiding(), 2f, "확인 → 안내 시작");
            Do(() => log.Add("  확인 누름 (" + hit + ") → " + state()));
            Check(() =>
            {
                bool leads = R0926GuideArrow.Leads(asked.lat, asked.lon, out float m);
                log.Add($"  화살표가 그 장소로: {leads}");
                return !asking() && guiding() && leads;
            }, "확인: 창이 닫히고 그 장소로 안내 시작 (아래 화살표)");
            Wait(() => !Active("ListPanel"), 3f, "확인 → 목록 내려감");
            Sleep(0.4f);
            Check(() => !Active("ListPanel") && guiding(), "확인: 목록이 내려가고 안내는 이어짐 (예전 줄 누르기와 같게)");
            Shot("guideask_guiding");
            Do(() => { R0926GuideArrow.Stop(); R0926Notch.StopGuide(); });
            Sleep(0.6f);

            // ── 6) 묻는 동안 목록이 닫히면 창도 닫힌다 (안내 없음) ──
            Do(() => Click(DockList));
            Sleep(1.5f);
            AskOn("다섯째");
            Do(() => Click(ListX + "/ClosePx0926"));   // 다른 길로 목록이 닫히는 경우 (알림으로 다른 창이 열리는 등)
            Wait(() => !Active("ListPanel"), 3f, "목록 닫힘");
            Sleep(0.3f);
            Do(() => log.Add("  목록이 닫힌 뒤 → " + state()));
            Check(() => !asking() && !guiding(), "목록이 닫히면 묻는 창도 닫힘 · 안내 없음");

            // ── 7) 사람 줄은 묻지 않는다 (사람에게는 길안내를 하지 않는다 — 11-16) ──
            Do(() =>
            {
                pm = P2PManager.Instance;
                if (pm == null || VirtualLocation.Instance == null || Camera.main == null) { log.Add("  P2PManager · 가상 위치 · 카메라 없음"); return; }
                filterWas = pm.GetUserFilterMode();
                if (filterWas != UserFilterMode.All) pm.SetUserFilterMode(UserFilterMode.All);   // 플레이 동안만
                var p = at(9f, 0f);
                pm.InjectTestUsersData(string.Format(CultureInfo.InvariantCulture,
                    "{{\"users\":[{{\"user_id\":\"{0}\",\"username\":\"{0}\",\"latitude\":{1:0.0000000},\"longitude\":{2:0.0000000},\"altitude\":0,\"avatar_url\":\"\",\"avatar_glb_url\":\"\",\"distance\":9.0,\"visibility_mode\":\"public\"}}]}}",
                    Person, p.lat, p.lon));
            });
            Do(() => Click(DockList));
            Sleep(1.5f);
            int personRow = -1;
            Wait(() =>
            {
                var p = Plm();
                var rows = ListRows();
                personRow = -1;
                if (p == null) return false;
                for (int i = 0; i < rows.Count && i < p.ShownEntries.Count; i++)
                    if (p.ShownEntries[i].kind == PlaceListManager.EntryKind.Person && p.ShownEntries[i].id == Person && onTop(rows[i])) { personRow = i; return true; }
                return false;
            }, 15f, "목록에 시험 사람 줄");
            Do(() =>
            {
                if (personRow < 0) return;
                hit = tapScreen(namePoint(ListRows()[personRow]));
                log.Add($"  사람 줄 {personRow + 1}번째 누름 → 맞은 것 {hit}");
            });
            Sleep(0.5f);
            Check(() => personRow >= 0 && !asking() && !guiding(), "사람 줄은 묻는 창 없이 (안내도 없음 — 따라가지 않기)");

            // 되돌리기 — 시험 사람 · 사람 칩 · 안내 · 목록
            Do(() =>
            {
                if (pm != null)
                {
                    pm.RemoveTestUserAvatar(Person);
                    if (pm.GetUserFilterMode() != filterWas) pm.SetUserFilterMode(filterWas);
                }
                R0926GuideArrow.Stop();
                R0926Notch.StopGuide();
                if (Active("ListPanel")) Click(ListX + "/ClosePx0926");
            });
            Sleep(0.8f);
        }
    }
}
