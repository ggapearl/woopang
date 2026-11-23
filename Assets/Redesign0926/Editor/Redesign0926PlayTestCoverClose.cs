using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-17 D3 — 목록 오른쪽 끝 사진으로 연 정보 창 위에도 왼쪽 아래 목록 칸 X '닫기'.
    ///  · coverclose : 목록 → 장소 사진 → 정보 창. 목록은 숨지만 목록 칸 X 는 도크 목록 칸 자리에 그대로(다 보임 · 누름 받음 · 도크 아이콘과 겹치지 않음) →
    ///    그 자리를 누르면 정보 창만 닫히고 목록으로(목록은 열린 채) · X 틀은 원래대로 → X 를 다시 누르면 목록이 닫힘 /
    ///    뒤로가기도 정보 창만 닫음 / 상세 「길안내」로 목록째 내리면 X 도 남지 않고 도크 목록 칸이 돌아옴
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanCoverClose()
        {
            const string Mirror = "ListPanel/DockMirror0926";
            const string X = Mirror + "/XButton_List";
            const string Back = X + "/CoverBack0926";
            const string DockList = "Dock0926/List_Button";
            Func<bool> infoOpen = () => { var d = DoubleTap3D.Shown; return d != null && Active("FullScreenPanel") && d.fullscreenCanvasGroup != null && d.fullscreenCanvasGroup.alpha >= 0.99f; };
            Func<CanvasGroup> listGroup = () => Find("ListPanel")?.GetComponent<CanvasGroup>();
            Func<CanvasGroup> dockGroup = () => Find(Mirror)?.GetComponent<CanvasGroup>();
            Func<bool> listHidden = () => { var g = listGroup(); return g != null && g.alpha <= 0.01f && !g.blocksRaycasts && !g.interactable; };
            Func<bool> listBack = () => { var g = listGroup(); return Active("ListPanel") && (g == null || (g.alpha >= 0.99f && g.blocksRaycasts && g.interactable)); };
            Func<bool> xShown = () =>
            {
                var g = dockGroup();
                return g != null && g.ignoreParentGroups && g.alpha >= 0.99f && g.blocksRaycasts && g.interactable
                    && Active(X + "/Icon0926") && Active(X + "/Label0926");
            };
            Func<bool> dockNormal = () => { var g = dockGroup(); return g != null && !g.ignoreParentGroups && g.alpha >= 0.99f && g.blocksRaycasts && g.interactable; };
            Func<float> dockListAlpha = () => Find(DockList)?.GetComponent<CanvasGroup>()?.alpha ?? -1f;
            Func<string> state = () =>
            {
                var g = dockGroup();
                var lg = listGroup();
                return $"정보 창 {(infoOpen() ? "열림" : "-")} · 목록 {(Active("ListPanel") ? "열림" : "닫힘")} 투명 {(lg != null ? lg.alpha : 1f):F2} · X 틀 "
                     + (g == null ? "없음" : $"{g.alpha:F2}{(g.ignoreParentGroups ? " 따로" : "")}{(g.blocksRaycasts ? " 누름" : "")}")
                     + $" · 덮는 칸 {(Active(Back) ? "켜짐" : "꺼짐")} · 도크 목록 칸 {dockListAlpha():F2}";
            };
            // 손으로 누른 것과 같게 — 그 칸 가운데를 맞혀 맨 위에 맞는 것에 누름을 보낸다 (덮는 칸이 받는지까지)
            Func<string, string> tapTop = path =>
            {
                var rt = Find(path) as RectTransform;
                var es = EventSystem.current;
                if (rt == null || es == null) return "-";
                var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = ScreenOf(rt) };
                var hits = new List<RaycastResult>();
                es.RaycastAll(ped, hits);
                if (hits.Count == 0) return "없음";
                ped.pointerCurrentRaycast = hits[0];
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, ped, ExecuteEvents.pointerClickHandler);
                return hits[0].gameObject.name;
            };
            int PlaceRow()
            {
                var p = Plm();
                var rows = ListRows();
                if (p == null) return -1;
                for (int i = 0; i < rows.Count && i < p.ShownEntries.Count; i++)
                {
                    var e = p.ShownEntries[i];
                    if (e.kind == PlaceListManager.EntryKind.Place && e.data != null && rows[i].Find("Photo")?.GetComponent<Button>() != null) return i;
                }
                return -1;
            }
            void OpenPhoto(string what)
            {
                Wait(() => PlaceRow() >= 0, 20f, what + ": 목록에 장소 줄");
                Do(() =>
                {
                    int i = PlaceRow();
                    if (i < 0) { log.Add("  장소 줄 없음"); return; }
                    log.Add("  " + what + ": " + (i + 1) + "번째 줄 사진");
                    ListRows()[i].Find("Photo").GetComponent<Button>().onClick.Invoke();
                });
                Wait(() => infoOpen() && listHidden(), 12f, what + ": 사진 → 정보 창 · 목록 숨김");
                Sleep(0.4f);   // 목록이 옅어지는 0.18초
            }
            string firstHit = null, secondHit = null;

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
            Check(() => Find(Back) != null && !Active(Back) && Find(Mirror)?.GetComponent<CanvasGroup>() != null, "준비: 목록 X 위 덮는 칸(CoverBack0926 · 평소 꺼짐) · X 틀 CanvasGroup");

            // ── 1) 사진으로 연 정보 창 위 X ──
            Do(() => Click(DockList));
            Sleep(1.5f);
            Check(() => Active("ListPanel") && Active(X) && dockNormal() && !Active(Back), "목록을 열면 X 틀은 평소대로 (부모 따름 · 덮는 칸 꺼짐)");
            OpenPhoto("첫째");
            Do(() => log.Add("  " + state()));
            Check(xShown, "정보 창이 떠 있어도 목록 칸 X '닫기'가 보임 (11-16 D3 — 예전엔 목록과 함께 사라졌다)");
            Check(listHidden, "목록 나머지는 그대로 숨김 (정보 창을 가리지 않음)");
            Check(() => dockListAlpha() <= 0.01f, "도크의 목록 칸 아이콘은 숨은 채 (X 와 겹치지 않음)");
            Check(() =>
            {
                var a = Find(X) as RectTransform;
                var b = Find(DockList) as RectTransform;
                if (a == null || b == null) return false;
                float d = Vector2.Distance(ScreenOf(a), ScreenOf(b));
                log.Add($"  X 가운데 ↔ 도크 목록 칸 가운데 {d:F1}px");
                return d <= 2f;
            }, "X 는 도크 목록 칸 자리 그대로");
            Check(() => Active(Back), "X 위 덮는 칸 켜짐 (정보 창만 닫게)");
            HitCheck(() => Find(Back) as RectTransform, false, "X 자리를 누르면 맨 위에 덮는 칸 (정보 창 · 도크가 가로채지 않음)");
            Shot("coverclose_info");
            // 정보 창에서 이어 연 창(고치기 · 프로필 · 추가 · 사진 자르기 — 목록보다 아래 층)이 뜨면 X 는 그 위로 비치지 않게 숨는다.
            // 실제 창은 열 때 서버 · 로그인을 타므로, 덮는 창 목록에 빈 창 하나를 넣어 켜고 끈다
            GameObject fake = null;
            Func<List<GameObject>> watch = () => typeof(R0926PlaceRows).GetField("coverWatch", NPI)?.GetValue(UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>()) as List<GameObject>;
            Check(() =>
            {
                var w = watch();
                var names = w == null ? "-" : string.Join(",", w.ConvertAll(g => g != null ? g.name : "null"));
                log.Add("  덮는 창: " + names);
                return names == "FullScreenPanel,FullProfilePanel,UploadPage,Fixpage,PhotoCrop0926";
            }, "덮는 창 = 정보 창 · 프로필 · 추가 · 고치기 · 사진 자르기");
            Do(() =>
            {
                fake = new GameObject("FakeOver0926");
                fake.SetActive(false);
                watch()?.Add(fake);
            });
            Do(() => fake.SetActive(true));
            Sleep(0.4f);
            Do(() => log.Add("  다른 창 위: " + state()));
            Check(() => { var g = dockGroup(); return g != null && g.alpha <= 0.01f && !g.blocksRaycasts && !Active(Back) && infoOpen(); }, "정보 창 위에 다른 창이 뜨면 X 는 숨고 누름도 받지 않음");
            Do(() => fake.SetActive(false));
            Sleep(0.4f);
            Check(() => xShown() && Active(Back) && infoOpen(), "그 창이 닫히면 X 가 다시 보이고 눌림");
            Do(() => { watch()?.Remove(fake); UnityEngine.Object.Destroy(fake); });
            Do(() => firstHit = tapTop(X));
            Wait(() => !Active("FullScreenPanel") && listBack(), 4f, "X → 정보 창 닫힘 · 목록");
            Do(() => log.Add("  누른 것: " + firstHit + " · " + state()));
            Check(() => firstHit == "CoverBack0926", "X 를 누르면 덮는 칸이 받음");
            Check(() => !Active("FullScreenPanel") && DoubleTap3D.Shown == null, "정보 창이 닫힘");
            Check(listBack, "목록은 열린 채 다시 보이고 눌림 (목록까지 닫지 않음)");
            Sleep(0.4f);
            Check(() => dockNormal() && !Active(Back) && Active(X), "목록이 돌아오면 X 틀은 원래대로 · 덮는 칸 꺼짐");
            Shot("coverclose_back");
            Do(() => secondHit = tapTop(X));
            Wait(() => !Active("ListPanel"), 3f, "X 다시 → 목록 닫힘");
            Sleep(0.4f);
            Do(() => log.Add("  누른 것: " + secondHit + " · " + state()));
            Check(() => secondHit == "ClosePx0926" && !Active("ListPanel"), "목록으로 돌아온 뒤 X 는 원래대로 목록을 닫음 (원래 덮개 ClosePx0926)");
            Check(() => dockListAlpha() >= 0.99f, "목록이 닫히면 도크 목록 칸이 돌아옴");

            // ── 2) 뒤로가기 — 정보 창만 닫는다 (덮는 칸은 뒤로가기에 끼지 않는다) ──
            Do(() => Click(DockList));
            Sleep(1.5f);
            OpenPhoto("둘째");
            Check(xShown, "다시 열어도 X 보임");
            Do(BackNow);
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Wait(() => !Active("FullScreenPanel") && listBack(), 4f, "뒤로가기 → 목록");
            Sleep(0.4f);
            Do(() => log.Add("  " + state()));
            Check(() => !Active("FullScreenPanel") && listBack() && dockNormal() && !Active(Back), "뒤로가기는 정보 창만 닫고 목록으로 · X 틀 원래대로");

            // ── 3) 상세 「길안내」 — 목록째 내린다. X 가 남지 않는다 ──
            R0926DetailGuide dg = null;
            OpenPhoto("셋째");
            Do(() =>
            {
                dg = UnityEngine.Object.FindAnyObjectByType<R0926DetailGuide>();
                log.Add("  길안내 버튼: " + (dg == null ? "없음" : (dg.GetComponent<CanvasGroup>()?.alpha >= 0.99f ? "보임" : "숨김 (자리 모름 · 바로 앞)")));
            });
            Do(() =>
            {
                var b = dg != null ? typeof(R0926DetailGuide).GetField("button", NPI)?.GetValue(dg) as Button : null;
                if (b != null && dg.GetComponent<CanvasGroup>()?.alpha >= 0.99f) b.onClick.Invoke();
                else { DoubleTap3D.CloseShown(); UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>()?.CloseForGuide(); }   // 같은 길 (안내만 빼고)
            });
            Sleep(0.1f);
            Do(() => log.Add("  길안내 직후: " + state()));
            Wait(() => !Active("ListPanel") && !Active("FullScreenPanel"), 4f, "길안내 → 정보 창 · 목록 닫힘");
            Sleep(0.4f);
            Do(() => log.Add("  " + state()));
            Check(() => !Active("ListPanel") && !Active("FullScreenPanel"), "길안내: 정보 창과 목록이 함께 닫힘");
            Check(() => dockListAlpha() >= 0.99f && !Active(Back) && dockNormal(), "길안내: X 가 남지 않고 도크 목록 칸이 돌아옴 · X 틀 원래대로");
            Do(() => { R0926GuideArrow.Stop(); R0926Notch.StopGuide(); });
            Sleep(0.5f);
        }
    }
}
