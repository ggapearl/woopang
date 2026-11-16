using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-15 (C) 가까이 모달 시험 — 2m 안에 들어온 오브젝트를 화면 가운데 카드에 (R0926NearModal).
    ///  · nearmodal : 하나 — 뜨기 · 제목 · 레이어 · 핀치 줌과 상관없는 크기 · 2.3m · 5초 머묾 · 순간이동 · 높이 · × · 누르기 · 꺼짐 · 창이 덮으면 숨김
    ///  · nearmulti : 여럿 — 가까운 순서 · 점 · ‹ › · 옆 칸 누르기 · 밀어 넘기기 · 순서가 바뀌어도 보던 것 · 보던 것이 사라지면 옆 것 · × 는 모두
    ///  · nearglass : 물방울메뉴 유리 / 기본메뉴 · 고정한 하늘 카드(유리 · 비켜 서기)
    ///  · nearkinds : 주변 사람 · 보물상자 · 공공데이터 · 공공교통
    /// 운영 서버에 쓰지 않는다 — 사람 프로필 · 하늘 옆 장은 닿지 않는 주소로 돌리고, 보물은 여는(받기) 데까지 가지 않는다.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private const System.Reflection.BindingFlags NmF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        private const System.Reflection.BindingFlags NmS = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        private const string NmStyleKey = "r0926_menu_style";
        private const string NmBackupKey = "Redesign0926PlayTest.NearPrefs";
        private const string NmDeadStub = "http://127.0.0.1:9";
        private const int NmMask = (1 << 28) | (1 << 29) | (1 << 30) | (1 << 31);
        private static readonly HashSet<string> NmPrivate = new HashSet<string> { "집", "엄빠집", "우팡이똥샷", "우팡이똥꼬샷" };

        private static bool nmRun;
        private static R0926NearModal nmModal;
        private static readonly Dictionary<GameObject, Vector3> nmHome = new Dictionary<GameObject, Vector3>();
        private static readonly List<GameObject> nmPicked = new List<GameObject>();

        // ── 하나 ─────────────────────────────────────────────
        private static void PlanNearModal()
        {
            GameObject a = null;
            DoubleTap3D dt = null;
            Object3DTouchHaptic haptic = null;
            Target target = null;
            string name = "";
            Vector2 cov1 = Vector2.zero;
            float scale1 = 0f, fit1 = 0f, t0 = 0f, tGone = 0f;
            var layers0 = new Dictionary<Renderer, int>();
            var snap1 = new Dictionary<Renderer, float>();
            Func<bool> infoOpen = () => dt != null && dt.fullscreenCanvasGroup != null && dt.fullscreenCanvasGroup.gameObject.activeSelf;
            Func<bool> restored = () => NmSameLayers(layers0) && (target == null || !R0926NearHold.Held.Contains(target));

            if (!string.IsNullOrEmpty(storeSrc)) Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            NmBoot();
            NmWaitPlaces(1, true);
            Do(() =>
            {
                if (nmPicked.Count == 0) throw new Exception("AR 에 보이는 우팡 큐브가 없다");
                a = nmPicked[0];
                dt = a.GetComponentInChildren<DoubleTap3D>(true);
                haptic = a.GetComponentInChildren<Object3DTouchHaptic>(true);
                target = a.GetComponentInChildren<Target>(true);
                name = dt.GetName();
                layers0.Clear();
                foreach (var r in a.GetComponentsInChildren<Renderer>(true)) layers0[r] = r.gameObject.layer;
                NmClearAround();
                log.Add($"  고른 큐브 '{name}' · 렌더러 {layers0.Count} · 레이어 {NmLayerList(a)} · 화면 표시 대상 {(target != null ? "있음" : "없음")}");
            });
            Sleep(0.6f);
            Check(() => nmModal.EditorCount == 0 && !nmModal.EditorCardOn, "처음엔 카드 없음 (2m 안에 아무것도 없다)");

            // 1) 1.5m — 화면 가운데 카드 · 제목 · 거리 · 안내 · AR 화면에선 빠짐
            Do(() => NmPut(a, 1.5f));
            Wait(() => nmModal.EditorShowing, 3f, "1.5m — 카드 뜸");
            Sleep(0.8f);
            Shot("near_single");
            Check(() => nmModal.EditorShowing && nmModal.EditorCount == 1 && nmModal.EditorCurrent == a, "2m 안(1.5m)에 들어오면 화면 가운데 카드에 그 오브젝트");
            Check(() =>
            {
                string t = nmModal.EditorTitle;
                log.Add("  제목: " + t.Replace("\n", " / "));
                return NmLine0(t) == NmShort(name) && t.EndsWith("\n바로 근처에 있습니다");
            }, "제목 「○○○ / 바로 근처에 있습니다」 (두 줄 · 「의」 없이 — 11-16)");
            Check(() => nmModal.EditorDistance == "1.5m", "거리 「1.5m」");
            Check(() => nmModal.EditorHint == "두 번 톡톡 누르면 자세히 볼 수 있어요", "안내 「두 번 톡톡 누르면 자세히 볼 수 있어요」");
            Check(() =>
            {
                var cam = Camera.main;
                var nc = nmModal.EditorCam(0);
                log.Add($"  레이어 {NmLayerList(a)} · AR 카메라에 28~31 {((cam.cullingMask & NmMask) != 0 ? "있음" : "없음")} · 카드 카메라 {(nc != null ? nc.cullingMask : 0)}");
                return NmLayers(a, l => l == 29) && (cam.cullingMask & NmMask) == 0 && nc != null && nc.cullingMask == 1 << 29
                       && (target == null || R0926NearHold.Held.Contains(target));
            }, "카드에 담긴 동안 AR 화면에선 빠진다 (렌더러 → 29 · AR 카메라는 28~31 을 안 찍음 · 카드 카메라는 29 만)");
            Check(() =>
            {
                var rt = nmModal.EditorTexture(0);
                cov1 = NmCoverage(rt);
                log.Add($"  카드 판 {NmRtInfo(rt)} · 물체가 차지한 크기 {cov1.x:0.000} × {cov1.y:0.000}");
                return Mathf.Max(cov1.x, cov1.y) > 0.3f && cov1.x < 0.999f && cov1.y < 0.999f;
            }, "카드 판에 물체가 투명 바탕으로 찍힌다 (판의 30% 넘게 · 가장자리에서 안 잘림)");
            Do(() =>
            {
                var r = NmRect(nmModal.EditorCard);
                log.Add($"  카드 자리 x {r.xMin:0}~{r.xMax:0} · y {r.yMin:0}~{r.yMax:0} · 배율 {nmModal.EditorCardScale:0.00} · 화면 {Screen.width}x{Screen.height}");
                log.Add("  화면 밖 표시(OffScreenIndicator): " + NmIndicator(target));
            });
            Check(() => target == null || target.indicator == null || !target.indicator.Active, "카드에 담긴 물체엔 AR 화면의 화살표 · 박스가 없다");
            Check(() =>
            {
                var r = NmRect(nmModal.EditorCard);
                return Mathf.Abs(r.center.x - Screen.width * 0.5f) < 3f && r.xMin >= 0f && r.xMax <= Screen.width && r.yMin >= 0f && r.yMax <= Screen.height;
            }, "카드는 화면 가로 가운데 · 화면 안");

            // 2) 핀치 줌 — AR 오브젝트는 커지고 작아져도 카드 안 크기는 그대로 (11-15 대표님 확인)
            Do(() => { scale1 = a.transform.localScale.x; fit1 = NmFitSize(a); snap1 = NmScaleSnap(a); NmZoom(1.5f); });
            Sleep(0.7f);
            Shot("near_zoom15");
            Check(() =>
            {
                var c = NmCoverage(nmModal.EditorTexture(0));
                float ks = a.transform.localScale.x / Mathf.Max(1e-5f, scale1);
                log.Add($"  핀치 1.5배: 오브젝트 ×{ks:0.00} · 카드 안 {cov1.x:0.000}×{cov1.y:0.000} → {c.x:0.000}×{c.y:0.000} · 맞춤 크기 {fit1:0.000} → {NmFitSize(a):0.000}{NmOddScale(a, snap1)}");
                return Mathf.Abs(ks - 1.5f) < 0.02f && NmSame(cov1, c, 0.05f);
            }, "핀치 줌 1.5배 — AR 오브젝트는 1.5배여도 카드 안 크기는 그대로 (±5%)");
            Sleep(1.2f);   // 크기가 0.6초 넘게 달라지면 카드가 다시 맞춘다 — 그 뒤(자리 잡은 뒤)에도 같은지
            Check(() =>
            {
                var c = NmCoverage(nmModal.EditorTexture(0));
                log.Add($"  1.9초 뒤: 카드 안 {c.x:0.000}×{c.y:0.000} · 맞춤 크기 {NmFitSize(a):0.000}{NmOddScale(a, snap1)}");
                return NmSame(cov1, c, 0.05f);
            }, "핀치 줌 1.5배 — 자리 잡은 뒤(1.9초)에도 카드 안 크기 그대로");
            Do(() => NmZoom(0.6f));
            Sleep(0.7f);
            Check(() =>
            {
                var c = NmCoverage(nmModal.EditorTexture(0));
                log.Add($"  핀치 0.6배: 카드 안 {c.x:0.000}×{c.y:0.000}");
                return NmSame(cov1, c, 0.05f);
            }, "핀치 줌 0.6배 — 카드 안 크기 그대로");
            Do(NmResetZoom);
            Sleep(0.4f);

            // 3) 뜬 지 5초가 지나면 2.3m 안에 있는 동안만 · 2m 밖에선 새로 뜨지 않는다
            Wait(() => NmSince(0) >= 0f && Time.unscaledTime - NmSince(0) >= 5.3f, 8f, "뜬 지 5초");
            Do(() => NmPut(a, 2.2f));
            Sleep(0.7f);
            Check(() => nmModal.EditorCount == 1, "5초 뒤에도 2.3m 안(2.2m)에 있는 동안은 머문다");
            Do(() => NmPut(a, 2.5f));
            Wait(() => nmModal.EditorCount == 0, 1f, "2.5m — 빠짐");
            Wait(() => !nmModal.EditorCardOn, 1.5f, "카드 사라짐");
            Check(() => nmModal.EditorCount == 0 && !nmModal.EditorCardOn, "2.3m 를 넘으면 카드가 사라진다");
            Check(restored, "카드에서 빠지면 레이어가 원래대로 (AR 화면에 다시 보인다)");
            Check(() => { log.Add("  빠진 뒤 화면 표시: " + NmIndicator(target)); return target == null || target.indicator != null && target.indicator.Active; }, "카드에서 빠지면 AR 화면의 표시(박스)도 다시");
            Do(() => NmPut(a, 2.2f));
            Sleep(0.8f);
            Check(() => nmModal.EditorCount == 0, "2m 밖(2.2m)에선 새로 뜨지 않는다");

            // 4) 한 번 뜨면 5초는 2m 밖이어도 머문다
            Do(() => NmPut(a, 1.9f));
            Wait(() => nmModal.EditorCount == 1, 2f, "1.9m — 다시 뜸");
            Do(() => t0 = NmSince(0));
            Sleep(0.5f);
            Do(() => NmPut(a, 3f));
            steps.Enqueue(() => Time.unscaledTime >= t0 + 3f);
            Check(() => nmModal.EditorCount == 1, "뜬 지 3초 — 3m 로 멀어져도 그대로");
            steps.Enqueue(() =>
            {
                if (nmModal.EditorCount > 0 && Time.unscaledTime <= t0 + 8f) return false;
                tGone = Time.unscaledTime;
                return true;
            });
            Check(() => { log.Add($"  뜬 뒤 {tGone - t0:0.00}초에 빠짐"); return tGone - t0 >= 4.9f && tGone - t0 <= 5.6f; }, "5초가 지나면 (2.3m 밖이라) 빠진다 — 4.9~5.6초");
            Wait(() => !nmModal.EditorCardOn, 1.5f, "카드 사라짐");
            Check(restored, "레이어 원래대로");

            // 5) 순간이동(10m 넘게)은 바로 · 높이가 15m 넘게 다르면 안 띄움
            Do(() => NmPut(a, 1.5f));
            Wait(() => nmModal.EditorShowing, 3f, "1.5m — 다시 뜸");
            Do(() => { NmPut(a, 12f); t0 = Time.unscaledTime; });
            steps.Enqueue(() =>
            {
                if (nmModal.EditorCount > 0 && Time.unscaledTime <= t0 + 3f) return false;
                tGone = Time.unscaledTime;
                return true;
            });
            Check(() => { log.Add($"  12m 로 옮긴 뒤 {tGone - t0:0.00}초에 빠짐"); return tGone - t0 <= 0.6f; }, "10m 넘게 순간이동하면 5초를 기다리지 않고 바로 빠진다");
            Wait(() => !nmModal.EditorCardOn, 1.5f, "카드 사라짐");
            Do(() => NmPut(a, 1.5f, 0f, 16f));
            Sleep(0.8f);
            Check(() => nmModal.EditorCount == 0, "높이가 15m 넘게 다르면(위층 · 아래층) 2m 안이어도 안 띄운다");

            // 6) × — 2m 밖으로 나갔다 다시 올 때까지 안 띄운다
            Do(() => NmPut(a, 1.5f));
            Wait(() => nmModal.EditorShowing, 3f, "1.5m — 다시 뜸");
            Do(() => NmButton("close").onClick.Invoke());
            Check(() => nmModal.EditorCount == 0 && nmModal.EditorDismissed(a), "× — 카드에서 빠지고 '닫음'으로 기억");
            Wait(() => !nmModal.EditorCardOn, 1.5f, "× 뒤 카드 사라짐");
            Check(restored, "× 뒤 레이어 원래대로");
            Sleep(1.2f);
            Check(() => nmModal.EditorCount == 0 && !nmModal.EditorCardOn, "× 뒤엔 2m 안에 그대로 있어도 다시 안 뜬다");
            Do(() => NmPut(a, 3f));
            Sleep(1.8f);
            Check(() => !nmModal.EditorDismissed(a), "2m 밖에서 1초 넘게 있으면 다시 띄울 수 있다");
            Do(() => NmPut(a, 1.5f));
            Wait(() => nmModal.EditorShowing, 3f, "다시 1.5m — 뜸");
            Check(() => nmModal.EditorShowing && nmModal.EditorCurrent == a, "× 했던 것도 나갔다 다시 오면 다시 뜬다");

            // 7) 누르기 — 한 번 = 살짝 커짐(소리 · 진동) · 두 번 톡톡 = 정보 창 (AR 에서 두 번 누른 것과 같다)
            Check(() => !infoOpen(), "정보 창 닫혀 있음");
            Do(() => nmModal.EditorTap());
            Check(() => haptic != null && typeof(Object3DTouchHaptic).GetField("nudging", NmF).GetValue(haptic) != null, "한 번 누르면 오브젝트가 살짝 커진다 (AR 에서 누른 것처럼)");
            Sleep(0.7f);
            Check(() => !infoOpen() && nmModal.EditorShowing, "한 번만 누르면 정보 창은 안 열린다");
            Do(() => { nmModal.EditorTap(); nmModal.EditorTap(); });
            Wait(infoOpen, 4f, "두 번 톡톡 — 정보 창");
            Check(infoOpen, "두 번 톡톡 누르면 정보 창 (AR 에서 두 번 누른 것과 같다)");
            Wait(() => !nmModal.EditorCardOn, 2f, "정보 창 — 카드 숨음");
            Check(() =>
            {
                log.Add("  정보 창 " + (dt.fullscreenCanvasGroup != null ? dt.fullscreenCanvasGroup.gameObject.name : "-") + " · 덮는 창 " + NmCovering());
                return !nmModal.EditorCardOn && restored();
            }, "정보 창이 열린 동안 카드는 숨고 오브젝트는 AR 화면으로 돌아간다");
            Sleep(1.5f);
            Do(() => dt.closeButton.onClick.Invoke());
            Wait(() => !infoOpen(), 3f, "정보 창 닫힘");
            Do(() => { if (infoOpen()) dt.closeButton.onClick.Invoke(); });
            Wait(() => !infoOpen() && nmModal.EditorShowing, 4f, "창을 닫으면 카드가 다시");
            Check(() => nmModal.EditorShowing && nmModal.EditorCurrent == a, "정보 창을 닫으면 카드가 다시 뜬다");

            // 8) 오브젝트가 꺼지면(풀로 돌아감) 바로 빠지고 레이어도 원래대로
            Do(() => a.SetActive(false));
            Wait(() => nmModal.EditorCount == 0, 1f, "꺼짐 — 빠짐");
            Check(() => nmModal.EditorCount == 0 && restored(), "오브젝트가 꺼지면 카드에서 바로 빠지고 레이어도 원래대로");
            Do(() => a.SetActive(true));
            Wait(() => nmModal.EditorShowing, 3f, "다시 켜짐 — 뜸");
            Check(() => nmModal.EditorShowing && nmModal.EditorCurrent == a, "다시 켜지면 다시 뜬다");

            // 9) 목록 창이 열리면 숨고 · 닫으면 다시 (담긴 것은 그대로)
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => Active("ListPanel") && !nmModal.EditorCardOn, 3f, "목록 — 카드 숨음");
            Check(() => !nmModal.EditorCardOn && nmModal.EditorCount == 1 && restored(), "목록 창이 열리면 카드는 숨고(담긴 것은 그대로) 오브젝트는 AR 화면으로");
            Back();
            Wait(() => !Active("ListPanel") && nmModal.EditorShowing, 3f, "목록 닫음 — 카드 다시");
            Check(() => nmModal.EditorShowing && NmLayers(a, l => l == 29), "목록을 닫으면 카드가 다시 (다시 AR 화면에서 빠짐)");

            Do(() => { NmPut(a, 12f); NmResetZoom(); });
            Wait(() => nmModal.EditorCount == 0 && !nmModal.EditorCardOn, 2f, "정리");
            Do(NmFinish);
        }

        // ── 여럿 ─────────────────────────────────────────────
        private static void PlanNearMulti()
        {
            GameObject a = null, b = null, c = null;   // 1.9m · 1.2m · 1.6m
            Func<int> dotsOn = () =>
            {
                int on = 0;
                foreach (var i in NmField<Image[]>("dotImages")) if (i != null && i.gameObject.activeSelf) on++;
                return on;
            };

            if (!string.IsNullOrEmpty(storeSrc)) Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            NmBoot();
            NmWaitPlaces(3, false);
            Do(() =>
            {
                if (nmPicked.Count < 3) throw new Exception("AR 에 보이는 우팡 장소가 3개 안 된다");
                a = nmPicked[0]; b = nmPicked[1]; c = nmPicked[2];
                NmClearAround();
                NmPut(a, 1.9f, -25f);
                NmPut(b, 1.2f, 0f);
                NmPut(c, 1.6f, 25f);
                log.Add($"  1.9m '{NmName(a)}' · 1.2m '{NmName(b)}' · 1.6m '{NmName(c)}'");
            });
            Wait(() => nmModal.EditorShowing && nmModal.EditorCount == 3, 3f, "셋 — 카드 뜸");
            Sleep(0.5f);
            Shot("near_multi");
            Check(() => nmModal.EditorItem(0) == b && nmModal.EditorItem(1) == c && nmModal.EditorItem(2) == a && nmModal.EditorCurrent == b,
                "여러 개면 가까운 순서 (1.2m · 1.6m · 1.9m) · 가운데는 가장 가까운 것");
            Check(() => nmModal.EditorTitle.Contains(NmShort(NmName(b))) && nmModal.EditorDistance == "1.2m", "제목 · 거리는 가운데 것");
            Check(() => { var d = NmField<RectTransform>("dots"); return d != null && d.gameObject.activeInHierarchy && dotsOn() == 3; }, "아래 점 3개");
            Check(() => !NmButton("prevButton").gameObject.activeSelf && NmButton("nextButton").gameObject.activeSelf, "처음엔 ‹ 없음 · › 있음");
            Check(() => nmModal.EditorHint == "옆으로 밀면 다음 · 가까운 순서", "처음 3초는 「옆으로 밀면 다음 · 가까운 순서」");
            Check(() =>
            {
                log.Add($"  레이어 — 가운데 {NmLayerList(b)} · 옆 {NmLayerList(c)} · 안 보이는 칸 {NmLayerList(a)} · 화면 밖 표시 {NmIndicator(b.GetComponentInChildren<Target>(true))}");
                return NmLayers(b, l => l == 29) && NmLayers(c, l => l == 30) && NmLayers(a, l => l == 28) && nmModal.EditorSlotObject(1) == c;
            }, "가운데 칸 29 · 옆 칸(미리보기) 30 · 안 보이는 칸 28 — 셋 다 AR 화면엔 없다");
            Check(() =>
            {
                foreach (var g in new[] { a, b, c })
                {
                    var t = g.GetComponentInChildren<Target>(true);
                    if (t != null && t.indicator != null && t.indicator.Active) return false;
                }
                return true;
            }, "카드에 담긴 셋 모두 AR 화면의 화살표 · 박스가 없다");
            Sleep(3.0f);
            Check(() => nmModal.EditorHint == "두 번 톡톡 누르면 자세히 볼 수 있어요", "3초 뒤엔 가운데 것을 누르는 법");

            // 넘기기 — › · 옆 칸 누르기 · ‹ · 밀기
            Do(() => NmButton("nextButton").onClick.Invoke());
            Sleep(0.6f);
            Check(() => nmModal.EditorTarget == 1 && nmModal.EditorCurrent == c, "› — 다음 것(1.6m)");
            Check(() => nmModal.EditorTitle.Contains(NmShort(NmName(c))) && nmModal.EditorDistance == "1.6m", "제목 · 거리도 바뀐다");
            Check(() => NmButton("prevButton").gameObject.activeSelf && NmButton("nextButton").gameObject.activeSelf, "가운데면 ‹ › 둘 다");
            Do(() => nmModal.EditorTapSlot(2));
            Sleep(0.6f);
            Check(() => nmModal.EditorTarget == 2 && nmModal.EditorCurrent == a && !NmButton("nextButton").gameObject.activeSelf, "옆 미리보기를 누르면 그쪽으로 · 끝이면 › 없음");
            Do(() => NmButton("prevButton").onClick.Invoke());
            Sleep(0.6f);
            Check(() => nmModal.EditorTarget == 1, "‹ — 앞 것");
            Do(() => nmModal.EditorGo(0));
            Sleep(0.6f);
            Do(() => NmSwipe(-300f));
            Sleep(0.6f);
            Check(() => nmModal.EditorTarget == 1 && nmModal.EditorCurrent == c, "왼쪽으로 밀면 다음");
            Do(() => NmSwipe(300f));
            Sleep(0.6f);
            Check(() => nmModal.EditorTarget == 0 && nmModal.EditorCurrent == b, "오른쪽으로 밀면 앞");
            Do(() => NmSwipe(-60f));
            Sleep(0.6f);
            Check(() => nmModal.EditorTarget == 0, "조금만 밀면 제자리");
            Do(() => NmSwipe(300f));
            Sleep(0.6f);
            Check(() => nmModal.EditorTarget == 0, "처음에서 더 밀어도 처음 (고무줄)");

            // 순서가 바뀌어도 보던 것은 그대로
            Do(() => nmModal.EditorGo(1));
            Sleep(0.6f);
            Do(() => NmPut(a, 0.8f, -25f));
            Sleep(2.6f);   // 2초마다 순서를 다시 본다
            Check(() => nmModal.EditorItem(0) == a && nmModal.EditorCurrent == c, "더 가까이 온 것이 생겨 순서가 바뀌어도 보던 것(가운데)은 그대로");

            // 보던 것이 사라지면 옆 것
            Do(() => NmPut(c, 12f, 25f));
            Wait(() => nmModal.EditorCount == 2, 1f, "보던 것 순간이동 — 빠짐");
            Sleep(0.5f);
            Check(() => nmModal.EditorCount == 2 && nmModal.EditorCurrent == b && dotsOn() == 2, "보던 것이 사라지면 옆 것이 가운데 · 점 2개");

            // × 는 카드에 든 것 모두
            Do(() => NmButton("close").onClick.Invoke());
            Check(() => nmModal.EditorCount == 0 && nmModal.EditorDismissed(a) && nmModal.EditorDismissed(b), "× 는 카드에 든 것 모두 닫는다");
            Wait(() => !nmModal.EditorCardOn, 1.5f, "카드 사라짐");
            Check(() => !nmModal.EditorCardOn && NmLayers(a, l => l < 28) && NmLayers(b, l => l < 28) && NmLayers(c, l => l < 28), "레이어 모두 원래대로");
            Do(NmFinish);
        }

        // ── 물방울 유리 · 고정한 하늘 카드 ────────────────────────
        private static void PlanNearGlass()
        {
            GameObject a = null;
            R0926SkyWeather sky = null;
            RectTransform frame = null, skyGlass = null;
            Transform pinLayer = null;
            string[] parts = { "Glass0926", "PinBtn0926", "Weather0926/Region0926", "Weather0926/Forecast0926" };
            Func<R0926Glass> cardGlass = () => nmModal != null && nmModal.EditorCard != null ? nmModal.EditorCard.GetComponent<R0926Glass>() : null;
            Func<string, R0926Glass> part = p => frame != null ? frame.Find(p)?.GetComponent<R0926Glass>() : null;
            Func<bool> skyShowing = () => sky != null && (bool)typeof(R0926SkyWeather).GetField("skyShow", NmF).GetValue(sky);
            Func<bool, bool> partsOn = on =>
            {
                foreach (var p in parts) { var g = part(p); if (g == null || !g.isActiveAndEnabled || g.Showing != on) return false; }
                return true;
            };
            Func<string> partsState = () =>
            {
                var s = new List<string>();
                foreach (var p in parts)
                {
                    var g = part(p);
                    s.Add(p.Substring(p.LastIndexOf('/') + 1) + " " + (g == null ? "없음" : !g.isActiveAndEnabled ? "꺼진 칸" : g.Showing ? (g.SeeThrough ? "유리·비침" : "유리·덮임") : "유리 아님"));
                }
                return string.Join(" · ", s);
            };

            Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            NmBoot();
            NmWaitPlaces(1, true);
            Do(() =>
            {
                if (nmPicked.Count == 0) throw new Exception("AR 에 보이는 우팡 큐브가 없다");
                a = nmPicked[0];
                NmClearAround();
                R0926MenuStyle.Droplet = true;
                NmPut(a, 1.5f);
            });
            Wait(() => nmModal.EditorShowing, 3f, "카드 뜸");
            Sleep(0.8f);
            Check(() =>
            {
                var g = cardGlass();
                log.Add("  카드 바탕: " + (g == null ? "유리 없음" : g.Showing ? (g.SeeThrough ? "물방울 유리 · 비침" : "물방울 유리 · 덮임 " + g.CoveredBy) : "유리 꺼짐"));
                return g != null && g.Showing && g.SeeThrough;
            }, "물방울메뉴 — 카드 바탕이 물방울 유리 (뒤 AR 화면이 비친다)");
            Shot("near_droplet");
            Do(() => R0926MenuStyle.Droplet = false);
            Sleep(0.6f);
            Check(() => { var g = cardGlass(); return g != null && !g.Showing && nmModal.EditorShowing; }, "기본메뉴 — 유리 없이 진한 반투명 바탕");
            Shot("near_basic");
            Do(() => R0926MenuStyle.Droplet = true);
            Sleep(0.4f);

            // 하늘 판을 띄운다 (옆 장은 닿지 않는 주소 — 운영 서버에 묻지 않게)
            Do(() =>
            {
                StashSkyPrefs();
                foreach (var k in new[] { "Sky_Enabled", "Sky_Weather", "Sky_Events" }) PlayerPrefs.SetInt(k, 1);
                PlayerPrefs.SetInt("Sky_StartCollapsed", 0);
                R0926Nearby.EditorBase = NmDeadStub;
                R0926HuntApi.EditorBase = NmDeadStub;
                R0926FriendsMap.EditorBase = NmDeadStub;
                sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                var pg = UnityEngine.Object.FindAnyObjectByType<R0926SkyPages>(FindObjectsInactive.Include);
                if (sky == null || pg == null) throw new Exception("하늘 날씨판 없음");
                typeof(R0926SkyWeather).GetField("collapsed", NmF)?.SetValue(sky, false);
                frame = pg.EditorFrame;
                skyGlass = frame != null ? frame.Find("Glass0926") as RectTransform : null;
                pinLayer = Find("SkyPin0926");
                InjectWeather();
                AimCamera(-45f, 0f);
            });
            Wait(skyShowing, 10f, "하늘을 보면 판");
            Sleep(1.0f);
            Shot("near_skyshow");
            Do(() => log.Add($"  하늘 판을 보는 동안: 카드 {(nmModal.EditorCardOn ? "켜짐" : "꺼짐")} · 하늘 판 캔버스 {NmCanvasMode(frame)} · {partsState()}"));
            Check(() => !nmModal.EditorCardOn && nmModal.EditorCount == 1 && sky.SkyShowing, "하늘을 올려다보는 동안 카드는 비켜 준다 (하늘 판을 가리지 않게 · 담긴 것은 그대로)");

            // 아래쪽 고정 — 카드는 그 위로 · 하늘 카드도 물방울 유리
            Do(() => sky.SetPinned(true));
            Sleep(0.8f);
            Do(() => AimCamera(0f, 0f));
            Sleep(1.2f);
            Shot("near_skypin");
            Check(() => sky.Pinned && !sky.PinnedTop && frame.parent == pinLayer, "하늘 카드 아래쪽 고정 (화면 UI 의 고정 자리로)");
            Check(() => { log.Add($"  고정한 하늘 카드: 캔버스 {NmCanvasMode(frame)} · 틀에 제 캔버스 {(frame.GetComponent<Canvas>() != null ? "있음" : "없음")} · {partsState()}"); return partsOn(true); },"물방울메뉴 — 고정한 하늘 카드(유리판 · 📌 · 지역 · 예보)도 물방울 유리");
            Check(() =>
            {
                var c = NmRect(nmModal.EditorCard);
                var s = NmRect(skyGlass);
                log.Add($"  카드 y {c.yMin:0}~{c.yMax:0} · 하늘 카드 y {s.yMin:0}~{s.yMax:0} · 카드 배율 {nmModal.EditorCardScale:0.00}");
                return nmModal.EditorShowing && c.yMin >= s.yMax - 1f && c.yMax <= Screen.height;
            }, "카드는 아래쪽에 고정한 하늘 카드 위로 비켜 선다 (겹치지 않음)");

            // 위쪽 고정 — 카드는 그 아래로
            Do(() => sky.SetPinned(true, true));
            Sleep(1.2f);
            Shot("near_skypintop");
            Check(() =>
            {
                var c = NmRect(nmModal.EditorCard);
                var s = NmRect(skyGlass);
                log.Add($"  위쪽 고정 — 카드 y {c.yMin:0}~{c.yMax:0} · 하늘 카드 y {s.yMin:0}~{s.yMax:0} · 카드 배율 {nmModal.EditorCardScale:0.00}");
                return sky.PinnedTop && nmModal.EditorShowing && c.yMax <= s.yMin + 1f && c.yMin >= 0f;
            }, "위쪽에 고정하면 카드는 그 아래로 (겹치지 않음 · 화면 안)");

            // 풀기 — 하늘 자리로 · 카드는 다시 가운데
            Do(() => sky.SetPinned(false));
            Sleep(1.2f);
            Check(() => { log.Add($"  고정 풀고: 틀 자리 {(frame.parent != null ? frame.parent.name : "-")} · {partsState()}"); return !sky.Pinned && frame.parent != pinLayer; }, "고정을 풀면 하늘 자리로");

            // 기본메뉴 — 고정한 하늘 카드는 예전 그대로
            Do(() => { R0926MenuStyle.Droplet = false; AimCamera(-45f, 0f); });
            Wait(skyShowing, 10f, "하늘 판 다시");
            Sleep(0.6f);
            Do(() => sky.SetPinned(true));
            Sleep(0.8f);
            Do(() => AimCamera(0f, 0f));
            Sleep(1.2f);
            Shot("near_basic_skypin");
            Check(() => { log.Add("  기본메뉴 · 고정: " + partsState()); return sky.Pinned && partsOn(false); }, "기본메뉴 — 고정한 하늘 카드는 유리 없이 예전 그대로");
            Do(() => sky.SetPinned(false));
            Sleep(0.6f);
            Do(NmFinish);
        }

        // ── 종류 ─────────────────────────────────────────────
        private static void PlanNearKinds()
        {
            GameObject av = null, chest = null;
            object fake = null;
            int huntTap = -1;
            bool fetched = false;
            Application.LogCallback onLog = (msg, st, type) => { if (msg.Contains("FetchProfile")) fetched = true; };
            var huntT = typeof(R0926HuntTreasure).GetNestedType("Hunt", System.Reflection.BindingFlags.NonPublic);
            Func<System.Collections.IList> hunts = () => (System.Collections.IList)typeof(R0926HuntTreasure).GetField("hunts", NmS).GetValue(null);
            var tapIdF = typeof(R0926HuntTreasure).GetField("tapId", NmS);

            if (!string.IsNullOrEmpty(storeSrc)) Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            NmBoot();
            Do(NmClearAround);

            // 1) 주변 사람 — 「○○님 / 바로 근처에 있습니다」 · 두 번 톡톡 = 프로필
            Do(() =>
            {
                var pm = P2PManager.Instance;
                if (pm == null || VirtualLocation.Instance == null) throw new Exception("P2PManager · 가상 위치 없음");
                if (pm.GetUserFilterMode() != UserFilterMode.All) pm.SetUserFilterMode(UserFilterMode.All);   // 플레이 동안만 (저장값은 그대로)
                double lat = VirtualLocation.Instance.Latitude + 12 / 111320.0, lon = VirtualLocation.Instance.Longitude;
                pm.InjectTestUsersData($"{{\"users\":[{{\"user_id\":\"zn1\",\"username\":\"zn1\",\"latitude\":{lat:0.0000000},\"longitude\":{lon:0.0000000},\"altitude\":0,\"distance\":12}}]}}");
            });
            Sleep(0.5f);
            Do(() =>
            {
                av = P2PManager.Instance.GetActiveAvatars().TryGetValue("zn1", out var go) ? go : null;
                if (av == null) throw new Exception("시험 사람 zn1 이 안 떴다");
                NmPut(av, 1.6f);
            });
            Wait(() => nmModal.EditorShowing && nmModal.EditorCurrent == av, 3f, "사람 1.6m — 카드 뜸");
            Sleep(0.6f);
            Shot("near_person");
            Check(() =>
            {
                log.Add("  제목: " + nmModal.EditorTitle.Replace("\n", " / ") + " · 렌더러 켜짐 " + NmVisible(av));
                return NmLine0(nmModal.EditorTitle) == "zn1님" && nmModal.EditorTitle.EndsWith("\n바로 근처에 있습니다");
            }, "주변 사람 — 「zn1님 / 바로 근처에 있습니다」");
            Check(() => nmModal.EditorHint == "두 번 톡톡 누르면 프로필을 볼 수 있어요", "안내 「두 번 톡톡 누르면 프로필을 볼 수 있어요」");
            Check(() => { var cv = NmCoverage(nmModal.EditorTexture(0)); log.Add($"  카드 안 {cv.x:0.000}×{cv.y:0.000}"); return Mathf.Max(cv.x, cv.y) > 0.3f; }, "사람 오브젝트도 카드 판에 찍힌다");
            Do(() =>
            {
                fetched = false;
                ProfileManager.EditorBase = NmDeadStub;   // 운영 서버에 시험 아이디로 묻지 않게 — 받기 실패 경고로 확인
                Application.logMessageReceived -= onLog;
                Application.logMessageReceived += onLog;
                log.Add("  프로필 매니저 " + (ProfileManager.Instance != null ? "있음" : "없음"));
                nmModal.EditorTap();
                nmModal.EditorTap();
            });
            Wait(() => fetched, 4f, "프로필 받기 시도");
            Do(() => { Application.logMessageReceived -= onLog; ProfileManager.EditorBase = null; });
            Check(() => fetched, "사람을 두 번 톡톡 = 프로필 열기 (AR 에서 두 번 누른 것과 같은 길)");
            Do(() => P2PManager.Instance.RemoveTestUserAvatar("zn1"));
            Wait(() => nmModal.EditorCount == 0, 1f, "사람 사라짐");
            Check(() => nmModal.EditorCount == 0 && NmLayers(av, l => l < 28), "사람이 사라지면(풀) 카드에서 빠지고 레이어 원래대로");
            Wait(() => !nmModal.EditorCardOn, 1.5f, "카드 사라짐");

            // 2) 보물상자 — 「보물상자 / 바로 근처에 있습니다」 · 누름은 보물찾기가 직접 (AR 에서 누른 것과 같은 길)
            Do(() =>
            {
                var ht = UnityEngine.Object.FindAnyObjectByType<R0926HuntTreasure>(FindObjectsInactive.Include);
                var prefab = ht != null ? typeof(R0926HuntTreasure).GetField("treasurePrefab", NmF).GetValue(ht) as GameObject : null;
                if (prefab == null) throw new Exception("보물상자 프리팹 없음");
                chest = UnityEngine.Object.Instantiate(prefab);
                chest.name = "HuntTreasure_990001";
                chest.transform.localScale = Vector3.one;
                ARObjectZoomController.Fit(chest);
                R0926HuntTreasure.SpawnedObjects["hunt_990001"] = chest;
                NmPut(chest, 1.4f);
            });
            Wait(() => nmModal.EditorShowing && nmModal.EditorCurrent == chest, 3f, "보물상자 1.4m — 카드 뜸");
            Sleep(0.6f);
            Shot("near_hunt");
            Check(() =>
            {
                log.Add("  제목: " + nmModal.EditorTitle.Replace("\n", " / "));
                string l0 = NmLine0(nmModal.EditorTitle);
                return l0.StartsWith("보물상자") && !l0.EndsWith("의") && nmModal.EditorTitle.EndsWith("\n바로 근처에 있습니다");
            }, "보물상자 — 「보물상자 / 바로 근처에 있습니다」");
            Check(() => nmModal.EditorHint == "두 번 톡톡 누르면 보물상자를 열어요", "안내 「두 번 톡톡 누르면 보물상자를 열어요」");
            Do(() =>
            {
                // 로그인 안 한 에디터에선 보물찾기가 다음 프레임에 지켜보던 보물을 치운다 — 한 걸음 안에서 넣고 · 누르고 · 뺀다 (여는 데까지 가지 않는다)
                fake = Activator.CreateInstance(huntT, true);
                huntT.GetField("id").SetValue(fake, 990001);
                huntT.GetField("title").SetValue(fake, "가까이 시험");
                huntT.GetField("nextPoll").SetValue(fake, float.MaxValue);
                huntT.GetField("obj").SetValue(fake, chest);
                hunts().Add(fake);
                try
                {
                    tapIdF.SetValue(null, 0);
                    nmModal.EditorTap();
                    huntTap = (int)tapIdF.GetValue(null);
                }
                finally
                {
                    hunts().Remove(fake);
                    tapIdF.SetValue(null, 0);
                }
            });
            Check(() => huntTap == 990001, "보물상자를 누르면 보물찾기가 받는다 (한 번 = 두 번째를 기다림)");
            Do(() => NmPut(chest, 12f));
            Wait(() => nmModal.EditorCount == 0 && !nmModal.EditorCardOn, 2f, "보물상자 멀리 — 카드 사라짐");
            Do(() =>
            {
                huntT.GetField("host").SetValue(fake, true);
                hunts().Add(fake);
                try { NmPut(chest, 1.4f); nmModal.EditorTick(); }
                finally { hunts().Remove(fake); }
            });
            Wait(() => nmModal.EditorShowing, 3f, "내 보물 — 카드 뜸");
            Check(() => nmModal.EditorHint == "누르면 내 보물찾기 현황을 볼 수 있어요", "내가 숨긴 보물 — 안내 「누르면 내 보물찾기 현황을 볼 수 있어요」");
            Do(() =>
            {
                R0926HuntTreasure.SpawnedObjects.Remove("hunt_990001");
                UnityEngine.Object.Destroy(chest);
            });
            Wait(() => nmModal.EditorCount == 0, 1f, "보물상자 지움 — 빠짐");
            Wait(() => !nmModal.EditorCardOn, 1.5f, "카드 사라짐");

            // 3) 공공데이터 · 공공교통 — 떠 있으면
            NmKind("공공데이터", "near_tour", () => UnityEngine.Object.FindAnyObjectByType<TourAPIManager>()?.GetSpawnedObjects()?.Values);
            NmKind("지하철", "near_subway", () => UnityEngine.Object.FindAnyObjectByType<SubwayManager>()?.GetSpawnedObjects()?.Values);
            NmKind("기차역", "near_train", () => UnityEngine.Object.FindAnyObjectByType<TrainStationManager>()?.GetSpawnedObjects()?.Values);
            NmKind("터미널", "near_terminal", () => UnityEngine.Object.FindAnyObjectByType<TerminalManager>()?.GetSpawnedObjects()?.Values);
            Do(NmFinish);
        }

        // ── 공공데이터 · 공공교통 — 광화문 (교통 칩을 켠 채로 시작 · 끝나면 편집기 저장값 되돌림) ──
        private static void PlanNearPublic()
        {
            Func<Dictionary<string, GameObject>> tour = () => UnityEngine.Object.FindAnyObjectByType<TourAPIManager>()?.GetSpawnedObjects();
            Func<Dictionary<string, GameObject>> subway = () => UnityEngine.Object.FindAnyObjectByType<SubwayManager>()?.GetSpawnedObjects();
            Func<Dictionary<string, GameObject>> train = () => UnityEngine.Object.FindAnyObjectByType<TrainStationManager>()?.GetSpawnedObjects();
            Func<Dictionary<string, GameObject>> terminal = () => UnityEngine.Object.FindAnyObjectByType<TerminalManager>()?.GetSpawnedObjects();

            NmBoot();
            Wait(() => NmVisibleCount(tour()) > 0 && NmVisibleCount(subway()) > 0, 45f, "공공데이터 · 지하철 오브젝트 뜸");
            Do(() => log.Add($"  떠 있는 것 — 공공데이터 {NmVisibleCount(tour())} · 지하철 {NmVisibleCount(subway())} · 기차역 {NmVisibleCount(train())} · 터미널 {NmVisibleCount(terminal())}"));
            Do(NmClearAround);
            NmKind("공공데이터", "near_tour", () => tour()?.Values);
            NmKind("지하철", "near_subway", () => subway()?.Values);
            NmKind("기차역", "near_train", () => train()?.Values);
            NmKind("터미널", "near_terminal", () => terminal()?.Values);
            Do(NmFinish);
        }

        private static int NmVisibleCount(Dictionary<string, GameObject> d)
        {
            int n = 0;
            if (d != null) foreach (var go in d.Values) if (go != null && go.activeInHierarchy && NmVisible(go)) n++;
            return n;
        }

        private static void NmKind(string label, string shot, Func<IEnumerable<GameObject>> source)
        {
            GameObject obj = null;
            Do(() =>
            {
                obj = null;
                var src = source();
                if (src != null)
                    foreach (var go in src)
                        if (go != null && go.activeInHierarchy && NmVisible(go)) { obj = go; break; }
                if (obj != null) NmPut(obj, 1.5f);
            });
            Wait(() => obj == null || nmModal.EditorShowing && nmModal.EditorCurrent == obj, 3f, label + " 1.5m");
            Sleep(0.5f);
            int f = -1;
            steps.Enqueue(() =>   // 떠 있는 것이 있을 때만 찍는다 (Shot 은 줄 끝에 넣으므로 여기선 직접)
            {
                if (obj == null) return true;
                if (f < 0) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "test_" + shot + ".png")); f = Time.frameCount; }
                return Time.frameCount >= f + 2;
            });
            Do(() =>
            {
                if (obj == null) { log.Add("SKIP " + label + " — 떠 있는 것이 없다"); return; }
                var cv = NmCoverage(nmModal.EditorTexture(0));
                log.Add($"  {label} — 제목: {nmModal.EditorTitle.Replace("\n", " / ")} · 안내: {nmModal.EditorHint} · 카드 안 {cv.x:0.000}×{cv.y:0.000}");
                bool ok = nmModal.EditorCurrent == obj && nmModal.EditorTitle.EndsWith("바로 근처에 있습니다") && !nmModal.EditorTitle.Contains("이 장소") && Mathf.Max(cv.x, cv.y) > 0.2f;
                log.Add((ok ? "PASS " : "FAIL ") + label + " — 2m 안이면 이름으로 카드에");
            });
            Do(() => { if (obj != null) NmPut(obj, 12f); });
            Wait(() => obj == null || nmModal.EditorCount == 0 && !nmModal.EditorCardOn, 2f, label + " 치움");
        }

        // ── 도우미 ───────────────────────────────────────────
        // 시험마다 처음 — 시작화면 · 로그인 안내 · 첫 안내를 넘기고, 근처를 걷는 가상 사용자를 멈춘다
        private static void NmBoot()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                nmRun = true;
                NmStash();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                // 에디터의 가상 사용자(근처 사람 woopang — 15~20m 를 걸어 다닌다)가 2m 안에 들어와 카드에 끼지 않게
                var vt = UnityEngine.Object.FindAnyObjectByType<P2PVirtualUserTester>();
                if (vt != null) { vt.StopSimulation(); vt.StopAllCoroutines(); }
                if (P2PManager.Instance != null) P2PManager.Instance.RemoveTestUserAvatar("3");
                nmModal = UnityEngine.Object.FindAnyObjectByType<R0926NearModal>(FindObjectsInactive.Include);
                nmHome.Clear();
                nmPicked.Clear();
                AimCamera(0f, 0f);
            });
            Sleep(0.6f);
            Check(() => nmModal != null && nmModal.isActiveAndEnabled, "준비: 가까이 모달 (NearModal0926 · R0926NearModal)");
        }

        private static void NmWaitPlaces(int n, bool cubeOnly)
        {
            Wait(() =>
            {
                nmPicked.Clear();
                nmPicked.AddRange(NmPlaces(n, cubeOnly));
                return nmPicked.Count >= n;
            }, 40f, "우팡 장소 " + n + "개" + (cubeOnly ? " (큐브 · 춤 모델 아님)" : ""));
        }

        // AR 에 보이는 우팡 장소 — 이름이 서로 다르고 개인 장소 · 이모지 이름 · 늘 도는 금빛 상자는 뺀다
        private static List<GameObject> NmPlaces(int n, bool cubeOnly)
        {
            var list = new List<GameObject>();
            var dm = UnityEngine.Object.FindAnyObjectByType<DataManager>();
            if (dm == null || dm.GetSpawnedObjects() == null) return list;
            var map = dm.GetPlaceDataMap();
            var names = new HashSet<string>();
            foreach (var kv in dm.GetSpawnedObjects())
            {
                var go = kv.Value;
                if (go == null || !go.activeInHierarchy || !NmVisible(go) || go.GetComponentInChildren<GoldenGlowCube>(true) != null) continue;
                if (cubeOnly && (map == null || !map.TryGetValue(kv.Key, out var p) || p.model_type != "cube" || dm.IsAnimCategory(kv.Key))) continue;
                var dt = go.GetComponentInChildren<DoubleTap3D>(true);
                string nm = dt != null ? dt.GetName() : null;
                if (string.IsNullOrEmpty(nm) || NmPrivate.Contains(nm.Trim()) || R0926Emoji.ToDisplay(nm) != nm || !names.Add(nm)) continue;
                if (go.GetComponentInChildren<Object3DTouchHaptic>(true) == null) continue;
                list.Add(go);
                if (list.Count >= n) break;
            }
            return list;
        }

        // 처음부터 3m 안에 있던 것은 멀리 (시험이 고른 것만 카드에 들게)
        private static void NmClearAround()
        {
            var cam = Camera.main;
            if (cam == null) return;
            int n = 0;
            foreach (var go in NmAll())
            {
                if (nmPicked.Contains(go)) continue;
                Vector3 d = go.transform.position - cam.transform.position;
                d.y = 0f;
                if (d.magnitude > 3f) continue;
                if (!nmHome.ContainsKey(go)) nmHome[go] = go.transform.position;
                go.transform.position += (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back) * 40f;
                n++;
            }
            if (n > 0) log.Add("  처음부터 3m 안에 있던 오브젝트 " + n + "개를 40m 밖으로");
        }

        private static List<GameObject> NmAll()
        {
            var all = new List<GameObject>();
            void Add<TKey>(Dictionary<TKey, GameObject> d)
            {
                if (d == null) return;
                foreach (var kv in d) if (kv.Value != null && kv.Value.activeInHierarchy) all.Add(kv.Value);
            }
            Add(UnityEngine.Object.FindAnyObjectByType<DataManager>()?.GetSpawnedObjects());
            Add(UnityEngine.Object.FindAnyObjectByType<TourAPIManager>()?.GetSpawnedObjects());
            Add(UnityEngine.Object.FindAnyObjectByType<SubwayManager>()?.GetSpawnedObjects());
            Add(UnityEngine.Object.FindAnyObjectByType<TrainStationManager>()?.GetSpawnedObjects());
            Add(UnityEngine.Object.FindAnyObjectByType<TerminalManager>()?.GetSpawnedObjects());
            Add(R0926HuntTreasure.SpawnedObjects);
            if (P2PManager.Instance != null) Add(P2PManager.Instance.GetActiveAvatars());
            return all;
        }

        // 카메라 앞(수평) d 미터 · 오른쪽으로 deg 도 · 눈높이에서 up 미터
        private static void NmPut(GameObject go, float d, float deg = 0f, float up = -0.6f)
        {
            var cam = Camera.main;
            if (cam == null || go == null) return;
            if (!nmHome.ContainsKey(go)) nmHome[go] = go.transform.position;
            Vector3 f = cam.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f = Quaternion.Euler(0f, deg, 0f) * f.normalized;
            go.transform.position = cam.transform.position + f * d + Vector3.up * up;
        }

        private static bool NmVisible(GameObject go)
        {
            if (go == null) return false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer || r is SpriteRenderer)) return true;
            return false;
        }

        private static string NmName(GameObject go)
        {
            var dt = go != null ? go.GetComponentInChildren<DoubleTap3D>(true) : null;
            return dt != null && !string.IsNullOrEmpty(dt.GetName()) ? dt.GetName() : go != null ? go.name : "";
        }

        // 카드 제목에 들어가는 모양 (R0926NearModal.TitleFor 와 같게)
        // 제목 첫 줄 (색 꼬리표 뺀 글)
        private static string NmLine0(string t) => System.Text.RegularExpressions.Regex.Replace((t ?? "").Split('\n')[0], "<[^>]+>", "");

        // 카드가 물체를 맞추는 크기 (감싸는 구 반지름 ÷ 맨 위 배율) — 핀치 줌에 안 변해야 카드 안 크기가 그대로
        private static float NmFitSize(GameObject go)
        {
            var m = typeof(R0926NearModal).GetMethod("Measure", NmF);
            var args = new object[] { go, null, null };
            if (m == null || !(bool)m.Invoke(nmModal, args)) return -1f;
            var ls = go.transform.lossyScale;
            return (float)args[2] / Mathf.Max(1e-4f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z))));
        }

        // 렌더러마다 (제 배율 ÷ 맨 위 배율) — 줌 뒤 달라진 것 = 맨 위 배율을 안 따라가는 부품
        private static Dictionary<Renderer, float> NmScaleSnap(GameObject go)
        {
            var d = new Dictionary<Renderer, float>();
            float root = Mathf.Max(1e-5f, Mathf.Abs(go.transform.lossyScale.x));
            foreach (var r in go.GetComponentsInChildren<Renderer>(false)) if (r.enabled) d[r] = Mathf.Abs(r.transform.lossyScale.x) / root;
            return d;
        }

        private static string NmOddScale(GameObject go, Dictionary<Renderer, float> before)
        {
            var now = NmScaleSnap(go);
            var odd = new List<string>();
            foreach (var kv in now)
                if (!before.TryGetValue(kv.Key, out float b)) odd.Add(kv.Key.name + "(새로 켜짐)");
                else if (Mathf.Abs(kv.Value / Mathf.Max(1e-5f, b) - 1f) > 0.02f) odd.Add($"{kv.Key.name} ×{kv.Value / Mathf.Max(1e-5f, b):0.00}");
            foreach (var kv in before) if (!now.ContainsKey(kv.Key)) odd.Add(kv.Key.name + "(꺼짐)");
            return odd.Count == 0 ? "" : " · 맨 위 배율을 안 따라간 부품: " + string.Join(", ", odd);
        }

        private static string NmShort(string n)
        {
            n = (n ?? "").Trim().Replace('<', '‹').Replace('>', '›').Replace('\n', ' ');
            return n.Length > 16 ? n.Substring(0, 16) + "…" : n;
        }

        private static T NmField<T>(string field) where T : class => typeof(R0926NearModal).GetField(field, NmF)?.GetValue(nmModal) as T;
        private static Button NmButton(string field) => NmField<Button>(field);

        private static float NmSince(int k)
        {
            var items = NmField<System.Collections.IList>("items");
            if (items == null || k < 0 || k >= items.Count) return -1f;
            var it = items[k];
            return (float)it.GetType().GetField("since").GetValue(it);
        }

        private static bool NmLayers(GameObject go, Func<int, bool> ok)
        {
            if (go == null) return false;
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return false;
            foreach (var r in rs) if (!ok(r.gameObject.layer)) return false;
            return true;
        }

        private static bool NmSameLayers(Dictionary<Renderer, int> was)
        {
            if (was.Count == 0) return false;
            foreach (var kv in was) if (kv.Key != null && kv.Key.gameObject.layer != kv.Value) return false;
            return true;
        }

        private static string NmLayerList(GameObject go)
        {
            if (go == null) return "-";
            var set = new SortedSet<int>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) set.Add(r.gameObject.layer);
            return string.Join(",", set);
        }

        private static string NmIndicator(Target t)
        {
            if (t == null) return "대상 없음";
            if (t.indicator == null) return "없음";
            return t.indicator.Active ? "켜짐 (" + t.indicator.Type + ")" : "꺼짐";
        }

        private static string NmCovering()
        {
            var cv = NmField<GameObject[]>("covers");
            var on = new List<string>();
            if (cv != null) foreach (var o in cv) if (o != null && o.activeInHierarchy) on.Add(o.name);
            return on.Count == 0 ? "없음" : string.Join(",", on);
        }

        private static string NmCanvasMode(Transform t)
        {
            var c = t != null ? t.GetComponentInParent<Canvas>() : null;
            return c != null ? c.rootCanvas.name + " " + c.rootCanvas.renderMode : "-";
        }

        private static Rect NmRect(RectTransform rt)
        {
            if (rt == null) return default;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);   // 오버레이 캔버스 — 월드 좌표가 곧 화면 좌표
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        private static string NmRtInfo(RenderTexture rt) => rt == null ? "없음" : $"{rt.width}x{rt.height} · MSAA {rt.antiAliasing}";

        private static bool NmSame(Vector2 a, Vector2 b, float tol) =>
            a.x > 0f && a.y > 0f && Mathf.Abs(b.x / a.x - 1f) <= tol && Mathf.Abs(b.y / a.y - 1f) <= tol;

        // 카드 판에서 물체가 차지한 크기 — 투명 바탕(0,0,0,0)이 아닌 픽셀의 상자 (판 크기 대비 0~1)
        private static Vector2 NmCoverage(RenderTexture rt)
        {
            if (rt == null) return Vector2.zero;
            int w = rt.width, h = rt.height;
            var tmp = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rt, tmp);
            var prev = RenderTexture.active;
            RenderTexture.active = tmp;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);
            var px = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = px[y * w + x];
                    if (Mathf.Max(p.a, Mathf.Max(p.r, Mathf.Max(p.g, p.b))) <= 25) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            return maxX < 0 ? Vector2.zero : new Vector2((maxX - minX + 1f) / w, (maxY - minY + 1f) / h);
        }

        private static void NmZoom(float k)
        {
            var z = UnityEngine.Object.FindAnyObjectByType<ARObjectZoomController>(FindObjectsInactive.Include);
            if (z == null) { log.Add("  줌 없음"); return; }
            typeof(ARObjectZoomController).GetField("currentZoom", NmF).SetValue(z, k);
            typeof(ARObjectZoomController).GetMethod("ApplyZoomToARObjects", NmF).Invoke(z, null);
        }

        private static void NmResetZoom()
        {
            var z = UnityEngine.Object.FindAnyObjectByType<ARObjectZoomController>(FindObjectsInactive.Include);
            if (z != null) z.ResetZoom();
        }

        // 카드를 옆으로 민다 — 손가락이 카드 좌표로 dx 만큼 (+ = 오른쪽). 한 프레임 안이라 튕김 속도는 0 — 민 거리로만 넘긴다
        private static void NmSwipe(float dx)
        {
            var card = nmModal.EditorCard;
            var r = NmRect(card);
            float k = r.width / Mathf.Max(1f, card.rect.width);
            Vector2 start = r.center;
            var e = new PointerEventData(EventSystem.current) { pointerId = 0, pressPosition = start, position = start + new Vector2(Mathf.Sign(dx) * 20f, 0f) };
            Func<string, object> f = n => typeof(R0926NearModal).GetField(n, NmF)?.GetValue(nmModal);
            string before = $"target {nmModal.EditorTarget} · pos {f("pos"):0.00} · 카드 {nmModal.EditorCardOn} · {nmModal.EditorCount}개";
            nmModal.OnPointerDown(e);
            nmModal.OnBeginDrag(e);
            object began = f("dragging");
            for (int i = 1; i <= 6; i++)
            {
                e.position = start + new Vector2(dx * k * i / 6f, 0f);
                nmModal.OnDrag(e);
            }
            object held = f("dragging");
            string mid = $"pos {f("pos"):0.00} · flingV {f("flingV"):0}";
            nmModal.OnEndDrag(e);
            int tc = 0;
            if (UnityEngine.InputSystem.Touchscreen.current != null)
                foreach (var t in UnityEngine.InputSystem.Touchscreen.current.touches) if (t.isInProgress) tc++;
            log.Add($"  밀기 {dx:+0;-0}: {before} → 끌기 시작 {began} · 끝까지 {held} · {mid} · 누른 손가락 {tc} · k {k:0.00} → target {nmModal.EditorTarget}");
        }

        // 메뉴 모양 저장값 — 시험 전 값을 세션에 (오류로 끊겨도 NmFinish 가 되돌린다)
        private static void NmStash()
        {
            if (SessionState.GetString(NmBackupKey, "").Length > 0) return;   // 끊긴 시험의 것이 남아 있으면 그게 진짜 원래 값
            SessionState.SetString(NmBackupKey, PlayerPrefs.HasKey(NmStyleKey) ? PlayerPrefs.GetInt(NmStyleKey).ToString() : "-");
        }

        // 시험이 바꾼 것 되돌리기 — 시험 끝 · 오류로 끊긴 뒤 · 플레이를 끈 뒤 · 다음 시험을 켜기 전
        private static void NmFinish()
        {
            if (nmRun)
            {
                nmRun = false;
                ProfileManager.EditorBase = null;
                R0926Nearby.EditorBase = null;
                R0926HuntApi.EditorBase = null;
                R0926FriendsMap.EditorBase = null;
                RestoreSkyPrefs();
            }
            string s = SessionState.GetString(NmBackupKey, "");
            if (s.Length == 0) return;
            SessionState.EraseString(NmBackupKey);
            if (s == "-") PlayerPrefs.DeleteKey(NmStyleKey);
            else if (int.TryParse(s, out int v)) PlayerPrefs.SetInt(NmStyleKey, v);
            PlayerPrefs.Save();
        }
    }
}
