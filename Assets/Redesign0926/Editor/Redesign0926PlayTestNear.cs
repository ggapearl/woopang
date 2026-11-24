using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 근처 카드 시험 (R0926NearModal) — 11-18 대표님 J/M1 로 두 가지가 됐다.
    ///  「발견한 장소」 걷다가 4m 안에 들어온 것 하나 (AR 화면이 뜨고 10초 뒤부터 · 백그라운드에서 돌아온 뒤 10초도 쉰다). 닫으면 앱을 끌 때까지 그 장소는 다시 안 뜬다
    ///  「근처 장소」 앱 시작 2초 뒤(설정에서 끌 수 있다) · AR 화면을 아래→위로 밀면 — 목록(필터 · 분류 반영) 가까운 순 10곳. 25m 밖 · AR 에 없는 곳은 사진
    ///  · nearmodal : 발견한 장소 — 10초 · 4m · 4.6m · 5초 머묾 · 순간이동 · 높이 · 하나만 · × / 아래로 밀기 = 앱을 끌 때까지 · 곁의 것은 나갔다 오면 · 누르기 · 꺼짐 · 창이 덮으면 숨김 · 백그라운드
    ///  · nearmulti : 근처 장소 — 위로 밀기(도크 · 오브젝트 위 · 짧게 · 옆으로는 안 받음) · 목록 순서 그대로 · 3D ↔ 사진 · 사진 받기 · 사진 두 번 톡톡 · 넘기기 · 아래로 밀어 닫기 · 발견한 장소를 바꿔 끼기
    ///  · nearstart : 앱 시작 2초 뒤 · 설정을 끄면 안 뜸(위로 밀면 뜸) · 백그라운드에서 돌아오면 안 뜸 · 설정 줄
    ///  · nearglass : 물방울메뉴 유리 / 기본메뉴 · 고정한 하늘 카드(유리 · 비켜 서기) · 하늘 오브젝트가 떠 있으면 위로 밀기 안 받음
    ///  · nearkinds : 주변 사람 · 보물상자 · 공공데이터 · 공공교통
    /// 운영 서버에 쓰지 않는다 — 사람 프로필 · 하늘 옆 장은 닿지 않는 주소로 돌리고, 보물은 여는(받기) 데까지 가지 않는다. 사진은 목록처럼 읽기만.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private const System.Reflection.BindingFlags NmF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        private const System.Reflection.BindingFlags NmS = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        private const string NmStyleKey = "r0926_menu_style";
        private const string NmBackupKey = "Redesign0926PlayTest.NearPrefs";
        private const string NmStartKey = "Place_NearOnStart";
        private const string NmStartBackupKey = "Redesign0926PlayTest.NearStartPref";
        private const string NmDeadStub = "http://127.0.0.1:9";
        private const int NmMask = (1 << 28) | (1 << 29) | (1 << 30) | (1 << 31);
        private static readonly HashSet<string> NmPrivate = new HashSet<string> { "집", "엄빠집", "우팡이똥샷", "우팡이똥꼬샷" };

        private static bool nmRun, nmNear, nmKeepWanted, nmKeep;
        private static readonly HashSet<GameObject> nmPlaced = new HashSet<GameObject>();   // 시험이 NmPut 으로 둔 것 — NmKeep 이 치우지 않는다
        private static R0926NearModal nmModal;
        private static readonly Dictionary<GameObject, Vector3> nmHome = new Dictionary<GameObject, Vector3>();
        private static readonly List<GameObject> nmPicked = new List<GameObject>();

        // ── 발견한 장소 ──────────────────────────────────────────
        private static void PlanNearModal()
        {
            GameObject a = null, b = null;   // a: 고른 큐브 · b: 곁의 것
            DoubleTap3D dt = null;
            Object3DTouchHaptic haptic = null;
            Target target = null;
            string name = "";
            Vector2 cov1 = Vector2.zero;
            float scale1 = 0f, fit1 = 0f, t0 = 0f, tGone = 0f, tFound = -1f, pulled = 0f;
            bool closingNow = false;
            var layers0 = new Dictionary<Renderer, int>();
            var snap1 = new Dictionary<Renderer, float>();
            Func<bool> infoOpen = () => dt != null && dt.fullscreenCanvasGroup != null && dt.fullscreenCanvasGroup.gameObject.activeSelf;
            Func<bool> restored = () => NmSameLayers(layers0) && (target == null || !R0926NearHold.Held.Contains(target));
            Func<bool> gone = () => nmModal.EditorCount == 0 && !nmModal.EditorCardOn;
            Func<bool> found = () =>   // 처음 뜬 때를 적는다
            {
                if (nmModal.EditorCount == 0 && Time.unscaledTime < t0 + 14f) return false;
                tFound = nmModal.EditorCount > 0 ? Time.unscaledTime : -1f;
                return true;
            };

            if (!string.IsNullOrEmpty(storeSrc)) Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            NmBoot(true, true);
            NmWaitPlaces(1, true);
            Wait(() => (b = NmOther(nmPicked.Count > 0 ? nmPicked[0] : null)) != null, 20f, "곁에 둘 다른 장소");
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
                NmPut(b, 12f, 35f);
                nmModal.EditorLive(5f);   // AR 화면이 뜬 지 5초 — 10초가 될 때까지는 4m 안이어도 안 뜬다
                t0 = nmModal.EditorLiveAt;
                NmPut(a, 3f);
                log.Add($"  고른 큐브 '{name}' · 곁의 것 '{NmName(b)}' · 렌더러 {layers0.Count} · 레이어 {NmLayerList(a)} · 화면 표시 대상 {(target != null ? "있음" : "없음")}");
            });
            Sleep(2f);
            Check(gone, "AR 화면이 뜨고 10초 전엔 4m 안(3m)이어도 안 뜬다 (AR 이 자리를 잡는 동안)");
            steps.Enqueue(found);
            Check(() => { log.Add($"  AR 화면이 뜨고 {tFound - t0:0.00}초에 뜸"); return tFound >= 0f && tFound - t0 >= 9.95f && tFound - t0 <= 10.8f; }, "AR 화면이 뜨고 10초가 지나면 「발견한 장소」");

            // 1) 3m — 화면 가운데 카드 · 제목 · 꼬리표 · 안내 · AR 화면에선 빠짐
            Wait(() => nmModal.EditorShowing, 3f, "3m — 카드 뜸");
            Sleep(0.8f);
            Shot("near_single");
            Check(() => nmModal.EditorShowing && nmModal.EditorMode == "Found" && nmModal.EditorCount == 1 && nmModal.EditorCurrent == a, "4m 안(3m)에 들어오면 화면 가운데 「발견한 장소」 카드에 그 오브젝트 하나");
            Check(() =>
            {
                string t = nmModal.EditorTitle;
                log.Add("  제목: " + t.Replace("\n", " / "));
                return NmLine0(t) == NmShort(name) && t.EndsWith("\n바로 근처에 있습니다");
            }, "제목 「○○○ / 바로 근처에 있습니다」");
            Check(() => nmModal.EditorDistance == "발견한 장소 · 3.0m", "위 꼬리표 「발견한 장소 · 3.0m」");
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

            // 3) 뜬 지 5초가 지나면 4.6m 안에 있는 동안만 · 4m 밖에선 새로 뜨지 않는다 · 걸어서 멀어진 것은 닫은 게 아니다
            Wait(() => NmSince(0) >= 0f && Time.unscaledTime - NmSince(0) >= 5.3f, 8f, "뜬 지 5초");
            Do(() => NmPut(a, 4.4f));
            Sleep(0.7f);
            Check(() => nmModal.EditorCount == 1 && !nmModal.EditorClosing, "5초 뒤에도 4.6m 안(4.4m)에 있는 동안은 머문다");
            Do(() => NmPut(a, 5f));
            Wait(() => nmModal.EditorClosing || nmModal.EditorCount == 0, 1f, "5m — 빠짐");
            Wait(gone, 1.5f, "카드 사라짐");
            Check(gone, "4.6m 를 넘으면 카드가 사라진다");
            Check(() => !nmModal.EditorClosedFound(a) && !nmModal.EditorDismissed(a), "걸어서 멀어진 것은 닫은 것이 아니다 (다시 오면 뜬다)");
            Check(restored, "카드에서 빠지면 레이어가 원래대로 (AR 화면에 다시 보인다)");
            Check(() => { log.Add("  빠진 뒤 화면 표시: " + NmIndicator(target)); return target == null || target.indicator != null && target.indicator.Active; }, "카드에서 빠지면 AR 화면의 표시(박스)도 다시");
            Do(() => NmPut(a, 4.4f));
            Sleep(0.8f);
            Check(() => nmModal.EditorCount == 0, "4m 밖(4.4m)에선 새로 뜨지 않는다");

            // 4) 한 번 뜨면 5초는 4.6m 밖이어도 머문다
            Do(() => NmPut(a, 3.9f));
            Wait(() => nmModal.EditorCount == 1, 2f, "3.9m — 다시 뜸");
            Do(() => t0 = NmSince(0));
            Sleep(0.5f);
            Do(() => NmPut(a, 6f));
            steps.Enqueue(() => Time.unscaledTime >= t0 + 3f);
            Check(() => nmModal.EditorCount == 1 && !nmModal.EditorClosing, "뜬 지 3초 — 6m 로 멀어져도 그대로");
            steps.Enqueue(() =>
            {
                if (nmModal.EditorCount > 0 && !nmModal.EditorClosing && Time.unscaledTime <= t0 + 8f) return false;
                tGone = Time.unscaledTime;
                return true;
            });
            Check(() => { log.Add($"  뜬 뒤 {tGone - t0:0.00}초에 빠짐"); return tGone - t0 >= 4.9f && tGone - t0 <= 5.6f; }, "5초가 지나면 (4.6m 밖이라) 빠진다 — 4.9~5.6초");
            Wait(gone, 1.5f, "카드 사라짐");
            Check(restored, "레이어 원래대로");

            // 5) 순간이동(10m 넘게)은 바로 · 높이가 15m 넘게 다르면 안 띄움
            Do(() => NmPut(a, 3f));
            Wait(() => nmModal.EditorShowing, 3f, "3m — 다시 뜸");
            Do(() => { NmPut(a, 12f); t0 = Time.unscaledTime; });
            steps.Enqueue(() =>
            {
                if (nmModal.EditorCount > 0 && !nmModal.EditorClosing && Time.unscaledTime <= t0 + 3f) return false;
                tGone = Time.unscaledTime;
                return true;
            });
            Check(() => { log.Add($"  12m 로 옮긴 뒤 {tGone - t0:0.00}초에 빠짐"); return tGone - t0 <= 0.6f; }, "10m 넘게 순간이동하면 5초를 기다리지 않고 바로 빠진다");
            Wait(gone, 1.5f, "카드 사라짐");
            Do(() => NmPut(a, 3f, 0f, 16f));
            Sleep(0.8f);
            Check(() => nmModal.EditorCount == 0, "높이가 15m 넘게 다르면(위층 · 아래층) 4m 안이어도 안 띄운다");

            // 6) 하나만 · × — 그 장소는 앱을 끌 때까지 다시 안 뜬다 · 곁(4m 안)의 것은 나갔다 올 때까지
            Do(() => NmPut(a, 3f));
            Wait(() => nmModal.EditorShowing && nmModal.EditorCurrent == a, 3f, "3m — 다시 뜸");
            Do(() => NmPut(b, 3.6f, 35f));
            Sleep(0.8f);
            Check(() => b != null && nmModal.EditorCount == 1 && nmModal.EditorCurrent == a, "곁에 다른 것(3.6m)이 와도 「발견한 장소」는 하나만");
            Do(() =>
            {
                NmButton("close").onClick.Invoke();
                closingNow = nmModal.EditorClosing;
            });
            Check(() => closingNow && nmModal.EditorClosedFound(a) && nmModal.EditorDismissed(b), "× — 그 장소는 앱을 끌 때까지 '닫음' · 곁(4m 안)의 것도 잠시 안 띄움");
            Wait(gone, 1.5f, "× 뒤 카드 사라짐");
            Check(restored, "× 뒤 레이어 원래대로");
            Sleep(1.2f);
            Check(gone, "× 뒤엔 둘 다 4m 안에 그대로 있어도 안 뜬다");
            Do(() => { NmPut(a, 6f); NmPut(b, 6f, 35f); });
            Sleep(1.8f);
            Check(() => !nmModal.EditorDismissed(b) && nmModal.EditorClosedFound(a), "4m 밖에서 1초가 지나면 곁의 것은 다시 띄울 수 있다 (닫은 장소는 그대로)");
            Do(() => NmPut(a, 3f));
            Sleep(1.2f);
            Check(gone, "닫은 장소는 나갔다 다시 와도 안 뜬다 (앱을 끌 때까지)");
            Do(() => NmPut(b, 3.6f, 35f));
            Wait(() => nmModal.EditorShowing && nmModal.EditorCurrent == b, 3f, "곁의 것 3.6m — 뜸");
            Check(() => nmModal.EditorShowing && nmModal.EditorCount == 1 && nmModal.EditorCurrent == b, "곁에 있던 것은 나갔다 다시 오면 뜬다 (더 가까운 닫은 장소는 건너뜀)");
            Do(() => NmPut(b, 12f, 35f));
            Wait(gone, 2f, "곁의 것 치움");

            // 7) 아래로 밀기 — 조금은 제자리로 · 많이는 닫기(× 와 같다) · 앱을 다시 켜면(닫은 기억이 없어지면) 다시 뜬다
            Do(() => nmModal.EditorForget());
            Wait(() => nmModal.EditorShowing && nmModal.EditorCurrent == a, 3f, "잊기 — 다시 뜸");
            Check(() => nmModal.EditorShowing && nmModal.EditorCurrent == a, "앱을 다시 켜면(닫은 기억이 없어지면) 다시 뜬다");
            Do(() => pulled = NmPullDown(0.04f));
            Sleep(0.8f);
            Check(() => pulled < -20f && !nmModal.EditorClosing && nmModal.EditorShowing && Mathf.Abs(nmModal.EditorPullOffset) < 1f, "아래로 조금 끌었다 놓으면 제자리로");
            Do(() =>
            {
                pulled = NmPullDown(0.35f);
                closingNow = nmModal.EditorClosing;
            });
            Check(() => closingNow && nmModal.EditorClosedFound(a), "아래로 끌어내리면 닫힌다 — × 와 같이 앱을 끌 때까지 '닫음'");
            Wait(gone, 2.5f, "끌어내림 — 카드 사라짐");
            Check(restored, "끌어내린 뒤 레이어 원래대로");
            Do(() => nmModal.EditorForget());
            Wait(() => nmModal.EditorShowing && nmModal.EditorCurrent == a, 3f, "잊기 — 다시 뜸");

            // 8) 누르기 — 한 번 = 살짝 커짐(소리 · 진동) · 두 번 톡톡 = 정보 창 (AR 에서 두 번 누른 것과 같다)
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

            // 9) 오브젝트가 꺼지면(풀로 돌아감) 바로 빠지고 레이어도 원래대로
            Do(() => a.SetActive(false));
            Wait(() => nmModal.EditorCount == 0, 1f, "꺼짐 — 빠짐");
            Check(() => nmModal.EditorCount == 0 && restored(), "오브젝트가 꺼지면 카드에서 바로 빠지고 레이어도 원래대로");
            Do(() => a.SetActive(true));
            Wait(() => nmModal.EditorShowing && nmModal.EditorCurrent == a, 3f, "다시 켜짐 — 뜸");   // 꺼질 때 카드가 0.2초 사라지는 중엔 아직 아니다
            Check(() => nmModal.EditorShowing && nmModal.EditorCurrent == a, "다시 켜지면 다시 뜬다 (닫은 것이 아니다)");

            // 10) 목록 창이 열리면 숨고 · 닫으면 다시 (담긴 것은 그대로)
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => Active("ListPanel") && !nmModal.EditorCardOn, 3f, "목록 — 카드 숨음");
            Check(() => !nmModal.EditorCardOn && nmModal.EditorCount == 1 && restored(), "목록 창이 열리면 카드는 숨고(담긴 것은 그대로) 오브젝트는 AR 화면으로");
            Back();
            Wait(() => !Active("ListPanel") && nmModal.EditorShowing, 3f, "목록 닫음 — 카드 다시");
            Check(() => nmModal.EditorShowing && NmLayers(a, l => l == 29), "목록을 닫으면 카드가 다시 (다시 AR 화면에서 빠짐)");

            // 11) 백그라운드에서 돌아오면 — 10초 동안 「발견한 장소」를 쉰다 (AR 이 자리를 다시 잡는 동안) · 시작 「근처 장소」도 안 뜬다
            Do(() => NmPut(a, 12f));
            Wait(gone, 2f, "치움");
            Do(() =>
            {
                NmStartPref(true);
                nmModal.EditorStartShown = false;
                nmModal.EditorLive(30f);
                nmModal.EditorResume();
                t0 = Time.unscaledTime;
                tFound = -1f;
                NmPut(a, 3f);
            });
            Sleep(2f);
            Check(() => gone() && nmModal.EditorStartShown, "백그라운드에서 돌아오면 4m 안(3m)이어도 바로 안 뜬다 · 시작 「근처 장소」도 건너뜀");
            steps.Enqueue(found);
            Check(() => { log.Add($"  돌아온 뒤 {tFound - t0:0.00}초에 뜸"); return tFound >= 0f && tFound - t0 >= 9.95f && tFound - t0 <= 10.8f; }, "돌아오고 10초가 지나면 다시 「발견한 장소」");

            Do(() => { NmPut(a, 12f); NmResetZoom(); });
            Wait(gone, 2f, "정리");
            Do(NmFinish);
        }

        // ── 근처 장소 10곳 ─────────────────────────────────────
        private static void PlanNearMulti()
        {
            GameObject x = null, y = null;   // x: 카드에 든 AR 장소 (3m ↔ 40m) · y: 6m 앞 (오브젝트 위에서 민 것)
            int kx = -1, kp = -1, n = 0;
            bool ok = false, closingNow = false;
            float pulled = 0f;
            Vector2 freeP = new Vector2(-1f, -1f), yP = new Vector2(-1f, -1f);
            var ids = new List<string>();
            DoubleTap3D dtx = null;
            Func<int> dotsOn = () =>
            {
                int on = 0;
                foreach (var i in NmField<Image[]>("dotImages")) if (i != null && i.gameObject.activeSelf) on++;
                return on;
            };
            Func<bool> infoOpen = () => dtx != null && dtx.fullscreenCanvasGroup != null && dtx.fullscreenCanvasGroup.gameObject.activeSelf;
            Func<bool> closed = () => nmModal.EditorMode == "None" && nmModal.EditorCount == 0 && !nmModal.EditorCardOn;
            Func<int, string> rank = r => "\n가까운 순 " + r + "번째";

            if (!string.IsNullOrEmpty(storeSrc)) Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            NmBoot(true, true);
            NmWaitPlaces(1, false);
            NmWaitList(3);
            Do(() =>
            {
                foreach (var go in NmPlaces(12, false))
                {
                    var col = go.GetComponentInChildren<Collider>();
                    if (col != null && col.enabled) { y = go; break; }
                }
                if (y == null) throw new Exception("콜라이더가 있는 우팡 장소가 없다");
                nmPicked.Clear();
                nmPicked.Add(y);
                NmClearAround();
                NmPut(y, 6f, 0f, 0f);
            });
            Sleep(0.4f);

            // 1) 위로 밀기를 받는 자리 — 맨 AR 화면만 (도크 · 오브젝트 위 · 짧게 · 옆으로 · 아래로는 안 받음)
            Do(() =>
            {
                Physics.SyncTransforms();
                freeP = NmFree();
                var cam = Camera.main;
                var col = y.GetComponentInChildren<Collider>();
                if (cam != null && col != null) yP = cam.WorldToScreenPoint(col.bounds.center);
                log.Add($"  맨 AR 자리 {(freeP.x >= 0f ? $"({freeP.x / Screen.width:0.00}, {freeP.y / Screen.height:0.00})" : "없음")} · 6m 앞 '{NmName(y)}' 화면 ({yP.x / Screen.width:0.00}, {yP.y / Screen.height:0.00})");
            });
            Check(() => freeP.x >= 0f && nmModal.EditorCanSwipeUp(freeP), "맨 AR 화면(UI · 오브젝트 없는 곳)은 위로 밀기를 받는다");
            Check(() =>
            {
                var dock = Find("Dock0926") as RectTransform;
                var c = NmRect(dock).center;
                return dock != null && !nmModal.EditorCanSwipeUp(c) && !nmModal.EditorSwipeUp(c, c + NmUp(0.25f)) && nmModal.EditorMode == "None";
            }, "도크(UI) 위에서 민 것은 안 받는다");
            Check(() =>
            {
                var cam = Camera.main;
                bool hitY = false;
                if (cam != null && yP.x >= 0f && Physics.Raycast(cam.ScreenPointToRay(yP), out var hit, 2000f, Physics.DefaultRaycastLayers & ~NmMask, QueryTriggerInteraction.Collide))
                    hitY = hit.collider.transform.IsChildOf(y.transform);
                bool free = yP.x >= 0f && WorldTouch.Free(yP);
                log.Add($"  6m 앞 오브젝트 위 — 오브젝트에 닿음 {hitY} · UI 없음 {free}");
                return hitY && free && !nmModal.EditorCanSwipeUp(yP) && !nmModal.EditorSwipeUp(yP, yP + NmUp(0.25f)) && nmModal.EditorMode == "None";
            }, "오브젝트를 만지며 민 것은 안 받는다 (돌리기 · 옮기기와 겹치지 않게)");
            Check(() => freeP.x >= 0f
                        && !nmModal.EditorSwipeUp(freeP, freeP + NmUp(0.05f))
                        && !nmModal.EditorSwipeUp(freeP, freeP + new Vector2(Screen.height * 0.3f, Screen.height * 0.2f))
                        && !nmModal.EditorSwipeUp(freeP, freeP - NmUp(0.25f))
                        && nmModal.EditorMode == "None", "짧게 · 옆으로 · 아래로 민 것은 안 받는다");

            // 2) 위로 밀면 — 목록(필터 · 분류 반영) 맨 위부터 10곳, 가까운 순
            Do(() =>
            {
                ok = nmModal.EditorSwipeUp(freeP, freeP + NmUp(0.25f));
                var picks = NmListPicks();   // 카드가 막 다시 만든 목록 그대로
                n = picks.Count;
                ids.Clear();
                var names = new List<string>();
                foreach (var e in picks) { ids.Add(e.id); names.Add(NmShort(e.name) + (e.kind == PlaceListManager.EntryKind.Person ? "(사람)" : "")); }
                log.Add($"  위로 밀기 → {(ok ? "뜸" : "안 뜸")} · 목록 {n}곳: " + string.Join(", ", names));
            });
            Wait(() => nmModal.EditorShowing, 3f, "근처 장소 — 카드 뜸");
            Sleep(0.3f);
            Shot("near_multi");
            Check(() => ok && nmModal.EditorMode == "Nearby" && n >= 1 && n <= 10 && nmModal.EditorCount == n, "AR 화면을 아래→위로 밀면 「근처 장소」 카드 — 목록의 장소 · 사람 (최대 10곳)");
            Check(() =>
            {
                for (int k = 0; k < n; k++)
                    if (nmModal.EditorEntryId(k) != ids[k] || nmModal.EditorRank(k) != k + 1)
                    {
                        log.Add($"  {k + 1}번째가 다르다: 카드 {nmModal.EditorEntryId(k)} · 목록 {ids[k]} · 순번 {nmModal.EditorRank(k)}");
                        return false;
                    }
                return true;
            }, "카드 순서 = 목록 순서 (광고 줄 · 맨 위 축제 · 보물찾기 · 보호소는 빼고)");
            Check(() =>
            {
                string t = nmModal.EditorTitle;
                log.Add("  제목: " + t.Replace("\n", " / "));
                return t.EndsWith(rank(1)) && nmModal.EditorDistance.StartsWith("근처 장소 · ");
            }, "제목 「○○○ / 가까운 순 1번째」 · 위 꼬리표 「근처 장소 · 거리」");
            Check(() =>
            {
                if (n < 2) return true;
                var d = NmField<RectTransform>("dots");
                log.Add($"  점 {dotsOn()}개");
                return d != null && d.gameObject.activeInHierarchy && dotsOn() == n;
            }, "아래 점 — 장소 수만큼 (10개까지)");
            Check(() => n < 2 || !NmButton("prevButton").gameObject.activeSelf && NmButton("nextButton").gameObject.activeSelf, "처음엔 ‹ 없음 · › 있음");
            Check(() => n < 2 || nmModal.EditorHint == "옆으로 밀면 다음 · 가까운 순서", "처음 3초는 「옆으로 밀면 다음 · 가까운 순서」");
            Check(() => !nmModal.EditorCanSwipeUp(freeP), "「근처 장소」가 떠 있는 동안엔 위로 밀기를 다시 받지 않는다");

            // 3) AR 에 떠 있고 25m 안이면 3D · 멀면 사진 (0.25초 동안 바꿔 낀다)
            Do(() =>
            {
                for (int k = 0; k < n && x == null; k++)
                {
                    var go = nmModal.EditorItem(k);
                    if (go != null && go.GetComponentInChildren<DoubleTap3D>(true) != null) { x = go; kx = k; }
                }
                if (x == null) { log.Add("  카드에 AR 장소가 없다"); return; }
                dtx = x.GetComponentInChildren<DoubleTap3D>(true);
                nmModal.EditorGo(kx);
                NmPut(x, 3f);
                log.Add($"  {kx + 1}번째 '{NmName(x)}' 를 3m 로");
            });
            Sleep(0.9f);
            Check(() => x != null && nmModal.EditorTarget == kx && !nmModal.EditorPhoto(kx) && NmLayersOn(x, l => l == 29 + kx % 3), "AR 에 떠 있고 25m 안(3m)이면 3D 로 — 카드 칸 레이어 (AR 화면엔 없다)");
            Do(() => NmPut(x, 40f));
            Wait(() => x == null || nmModal.EditorPhoto(kx) && NmLayers(x, l => l < 28), 3f, "40m — 사진으로");
            Sleep(0.4f);
            Shot("near_photo");
            Check(() => x != null && nmModal.EditorPhoto(kx) && NmLayers(x, l => l < 28) && nmModal.EditorPhotoShown(kx % 3), "25m 밖(40m)이면 사진으로 — 오브젝트는 AR 화면으로 돌려보낸다");

            // 4) 사진 — 10곳 모두 미리 받는다 (목록 줄 사진과 같은 것 · 큰 사본 512)
            Wait(() => NmPhotoCount(n) > 0, 20f, "사진 받기");
            Do(() =>
            {
                int urls = 0, res = 0, none = 0, wait = 0;
                for (int k = 0; k < n; k++)
                {
                    string key = nmModal.EditorPhotoKey(k);
                    if (key == null) wait++;
                    else if (key.Length == 0) none++;
                    else if (key.StartsWith("res:")) res++;
                    else urls++;
                }
                log.Add($"  사진 주소 {urls} · 앱 그림 {res} · 사진 없음 {none} · 상세 기다림 {wait} · 받은 것 {NmPhotoCount(n)}");
                kp = x != null && nmModal.EditorPhoto(kx) && nmModal.EditorPhotoTexture(kx) != null ? kx : -1;
                for (int k = 0; k < n && kp < 0; k++) if (nmModal.EditorPhoto(k) && nmModal.EditorPhotoTexture(k) != null) kp = k;
                if (kp >= 0) nmModal.EditorGo(kp);
            });
            Sleep(0.7f);
            Check(() => NmPhotoCount(n) > 0, "사진을 받는다 (운영 서버 읽기만)");
            Check(() =>
            {
                if (kp < 0) { log.Add("  받은 사진이 든 사진 칸이 없다"); return false; }
                var frame = nmModal.EditorPhotoFrame(kp % 3);
                var pic = frame != null ? typeof(R0926NearPhoto).GetField("pic", NmF)?.GetValue(frame) as RawImage : null;
                return nmModal.EditorTarget == kp && frame != null && frame.gameObject.activeSelf && pic != null && pic.enabled && pic.texture == nmModal.EditorPhotoTexture(kp);
            }, "가운데 사진 칸에 그 장소 사진");

            // 5) 사진 칸을 두 번 톡톡 — 목록 사진을 누른 것과 같은 정보 창
            Do(() => { if (x != null) nmModal.EditorGo(kx); });
            Sleep(0.7f);
            Do(() => { if (x != null) { nmModal.EditorTap(); nmModal.EditorTap(); } });
            Wait(infoOpen, 5f, "사진 두 번 톡톡 — 정보 창");
            Check(infoOpen, "사진 칸을 두 번 톡톡 누르면 정보 창 (목록 사진을 누른 것과 같다)");
            Wait(() => !nmModal.EditorCardOn, 2f, "정보 창 — 카드 숨음");
            Check(() => !nmModal.EditorCardOn && nmModal.EditorMode == "Nearby" && nmModal.EditorCount == n, "정보 창이 열린 동안 카드는 숨고 10곳은 그대로");
            Sleep(1.5f);
            Do(() => { if (dtx != null && dtx.closeButton != null) dtx.closeButton.onClick.Invoke(); });
            Wait(() => !infoOpen(), 3f, "정보 창 닫힘");
            Do(() => { if (infoOpen()) dtx.closeButton.onClick.Invoke(); });
            Wait(() => !infoOpen() && nmModal.EditorShowing, 4f, "창을 닫으면 카드가 다시");
            Check(() => nmModal.EditorShowing && nmModal.EditorTarget == kx && nmModal.EditorCount == n, "정보 창을 닫으면 카드가 다시 (보던 것 그대로)");

            // 6) 10곳은 고정 — 목록이 새로 그려져도 그대로 (다시 밀어야 새로)
            Sleep(2.5f);
            Do(() => UnityEngine.Object.FindAnyObjectByType<PlaceListManager>(FindObjectsInactive.Include)?.UpdateUI());
            Sleep(0.5f);
            Check(() =>
            {
                if (nmModal.EditorCount != n) return false;
                for (int k = 0; k < n; k++) if (nmModal.EditorEntryId(k) != ids[k]) return false;
                return true;
            }, "목록이 새로 그려져도 카드의 10곳은 그대로");

            // 7) 넘기기 — › · 옆 칸 누르기 · 밀기 · 끝
            Do(() => nmModal.EditorGo(0));
            Sleep(0.6f);
            Do(() => { if (n >= 2) NmButton("nextButton").onClick.Invoke(); });
            Sleep(0.6f);
            Check(() => n < 2 || nmModal.EditorTarget == 1 && nmModal.EditorTitle.EndsWith(rank(2)), "› — 다음 곳 · 제목 「가까운 순 2번째」");
            Check(() => n < 3 || NmButton("prevButton").gameObject.activeSelf && NmButton("nextButton").gameObject.activeSelf, "가운데면 ‹ › 둘 다");
            Do(() => { if (n >= 3) nmModal.EditorTapSlot(2); });
            Sleep(0.6f);
            Check(() => n < 3 || nmModal.EditorTarget == 2, "옆 미리보기를 누르면 그쪽으로");
            Do(() => { if (n >= 4) NmSwipe(-300f); });
            Sleep(0.6f);
            Check(() => n < 4 || nmModal.EditorTarget == 3, "왼쪽으로 밀면 다음");
            Do(() => { if (n >= 4) NmSwipe(300f); });
            Sleep(0.6f);
            Check(() => n < 4 || nmModal.EditorTarget == 2, "오른쪽으로 밀면 앞");
            Do(() => nmModal.EditorGo(n - 1));
            Sleep(0.7f);
            Check(() => n < 2 || nmModal.EditorTarget == n - 1 && !NmButton("nextButton").gameObject.activeSelf && NmButton("prevButton").gameObject.activeSelf && nmModal.EditorTitle.EndsWith(rank(n)),
                "끝이면 › 없음 · 제목 「가까운 순 " + "n번째」");

            // 8) 아래로 밀기 — 조금은 제자리 · 많이는 닫기. 「근처 장소」는 닫아도 '닫은 장소'로 남지 않는다 · 4m 안의 것은 나갔다 올 때까지 「발견한 장소」로 안 뜬다
            Do(() => pulled = NmPullDown(0.04f));
            Sleep(0.8f);
            Check(() => pulled < -20f && !nmModal.EditorClosing && nmModal.EditorShowing && Mathf.Abs(nmModal.EditorPullOffset) < 1f, "아래로 조금 끌었다 놓으면 제자리로");
            Do(() => NmPut(x, 3f));
            Sleep(0.6f);
            Do(() =>
            {
                pulled = NmPullDown(0.35f);
                closingNow = nmModal.EditorClosing;
            });
            Check(() => closingNow, "아래로 끌어내리면 닫힌다");
            Wait(closed, 2.5f, "끌어내림 — 카드 사라짐");
            Check(() => closed() && (x == null || NmLayers(x, l => l < 28) && !nmModal.EditorClosedFound(x) && nmModal.EditorDismissed(x)),
                "닫으면 모두 AR 화면으로 · 「근처 장소」는 '닫은 장소'로 남지 않는다 · 4m 안의 것은 잠시 쉼");
            Sleep(1.5f);
            Check(closed, "닫자마자 4m 안(3m)의 것이 「발견한 장소」로 뜨지 않는다");

            // 9) 다시 위로 밀면 다시 (처음부터) · × 로 닫기 — 3m 의 것은 등 뒤로 (4m 안 그대로 · 큰 모델이 앞에 있으면 화면을 다 덮어 맨 AR 자리가 없다)
            Do(() => NmPut(x, 3f, 180f));
            Sleep(0.2f);
            Do(() => ok = NmSwipeUp());
            Wait(() => nmModal.EditorShowing, 3f, "다시 뜸");
            Check(() => ok && nmModal.EditorMode == "Nearby" && nmModal.EditorCount >= 1 && nmModal.EditorTarget == 0 && nmModal.EditorTitle.EndsWith(rank(1)), "닫은 뒤에도 위로 밀면 다시 「근처 장소」 (처음부터)");
            Do(() =>
            {
                NmButton("close").onClick.Invoke();
                closingNow = nmModal.EditorClosing;
            });
            Check(() => closingNow, "× — 닫힌다");
            Wait(closed, 2f, "× — 카드 사라짐");

            // 10) 「발견한 장소」가 떠 있어도 위로 밀면 「근처 장소」로 바꿔 낀다
            Do(() => NmPut(x, 12f));
            Sleep(1.8f);
            Do(() => NmPut(x, 3f));
            Wait(() => nmModal.EditorShowing && nmModal.EditorMode == "Found", 3f, "발견한 장소 뜸");
            Check(() => x != null && nmModal.EditorMode == "Found" && nmModal.EditorCurrent == x, "(준비) 3m 의 것이 「발견한 장소」로");
            Check(() =>
            {
                // AR 에서 맞지 않는 콜라이더는 막을 수 없다 — 렌더러와 같은 오브젝트(3D 모델)는 담을 때 같이 카드 레이어(28~31)로 가고,
                // 크게 키운 모델(배율 10 · 상자 10m)은 3m 앞이면 카메라가 상자 안이라 레이가 안 맞는다 (11-21 운영 장소 하나가 그렇게 커짐).
                // 큐브처럼 콜라이더만 남는 것은 바로 앞(3m)이면 카드 뒤에 통째로 가려질 수 있다 — 4m 안에서 조금씩 올리거나 옆으로 돌려 카드 밖으로 나온 자리에서 민다
                // (11-24 — 휴대폰 화각이 넓어 3m 앞 1~2m 상자도 화면 가운데 작은 네모라, 1.4m 올려도 카드 뒤 · 옆으로 40~70° 돌리면 카드 옆 맨 AR 에 걸린다)
                int open = 0, moved = 0, inside = 0;
                var eye = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                foreach (var c in x.GetComponentsInChildren<Collider>(false))
                {
                    if (!c.enabled) continue;
                    if (((1 << c.gameObject.layer) & NmMask) != 0) moved++;
                    else if (c.bounds.Contains(eye)) inside++;
                    else open++;
                }
                if (open == 0)
                {
                    log.Add($"  카드에 담긴 것의 콜라이더 — 카드 레이어로 간 것 {moved}개 · 카메라가 안에 든 큰 상자 {inside}개 (AR 에서 맞지 않음)");
                    return moved + inside > 0 && nmModal.EditorMode == "Found" && nmModal.EditorCurrent == x;
                }
                var p = new Vector2(-1f, -1f);
                float up = -0.6f, turn = 0f;
                foreach (float deg in new[] { 0f, 40f, -40f, 50f, -50f, 60f, -60f, 70f, -70f })
                {
                    foreach (float u in new[] { -0.6f, 0.4f, 0.9f, 1.4f })
                    {
                        up = u;
                        turn = deg;
                        NmPut(x, 3f, deg, u);
                        Physics.SyncTransforms();
                        p = NmOnHeld(x);
                        if (p.x >= 0f) break;
                    }
                    if (p.x >= 0f) break;
                }
                bool can = p.x >= 0f && nmModal.EditorCanSwipeUp(p) && nmModal.EditorMode == "Found" && nmModal.EditorCurrent == x;
                log.Add($"  카드에 담긴 것의 콜라이더 자리: {(p.x >= 0f ? $"찾음 (높이 {up:+0.0;-0.0}m · 옆 {turn:+0;-0;0}° · 화면 {p.x / Screen.width:0.00}, {p.y / Screen.height:0.00})" : "없음")} · AR 에 남은 콜라이더 {open}개 · 위로 밀기 받음 {can}");
                if (p.x < 0f) log.Add(NmHeldWhy(x));
                return can;
            }, "카드에 담겨 AR 화면에서 빠진 것(콜라이더만 남음) 위에서 밀어도 받는다 — 안 보이는 것에 막히지 않게");
            Do(() => ok = NmSwipeUp());
            Sleep(0.6f);
            Shot("near_found_swap");
            Check(() => ok && nmModal.EditorMode == "Nearby" && nmModal.EditorCount >= 1 && nmModal.EditorTitle.EndsWith(rank(1)) && nmModal.EditorDistance.StartsWith("근처 장소 · "),
                "「발견한 장소」가 떠 있어도 위로 밀면 「근처 장소」로 바꿔 낀다");
            Do(() => NmButton("close").onClick.Invoke());
            Wait(closed, 2f, "닫힘");
            Do(() => { NmPut(x, 12f); NmPut(y, 12f); });
            Do(NmFinish);
        }

        // ── 앱 시작 「근처 장소」 · 설정 · 백그라운드 ─────────────────
        private static void PlanNearStart()
        {
            float t0 = 0f, tOn = -1f;
            bool ok = false;
            string firstId = null;
            Func<bool> closed = () => nmModal.EditorMode == "None" && nmModal.EditorCount == 0 && !nmModal.EditorCardOn;

            if (!string.IsNullOrEmpty(storeSrc)) Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            NmBoot(true, true);
            NmWaitList(1);
            Do(NmClearAround);

            // 1) 설정 켬(기본) — AR 화면이 뜨고 2초 뒤 한 번
            Do(() =>
            {
                NmStartPref(true);
                nmModal.EditorStartShown = false;
                nmModal.EditorLive(0f);
                t0 = nmModal.EditorLiveAt;
            });
            Sleep(1.2f);
            Check(closed, "AR 화면이 뜨고 2초 전엔 안 뜬다 (1.2초)");
            steps.Enqueue(() =>
            {
                if (!nmModal.EditorCardOn && Time.unscaledTime < t0 + 6f) return false;
                tOn = nmModal.EditorCardOn ? Time.unscaledTime : -1f;
                var picks = NmListPicks();
                firstId = picks.Count > 0 ? picks[0].id : null;
                return true;
            });
            Check(() => { log.Add($"  AR 화면이 뜨고 {tOn - t0:0.00}초에 뜸"); return tOn >= 0f && tOn - t0 >= 1.95f && tOn - t0 <= 3.2f; }, "앱 시작 2초 뒤 「근처 장소」가 뜬다 (설정 켬 · 기본값)");
            Wait(() => nmModal.EditorShowing, 2f, "시작 카드 다 뜸");
            Shot("near_start");
            Check(() => nmModal.EditorMode == "Nearby" && nmModal.EditorStartShown && nmModal.EditorCount >= 1 && nmModal.EditorEntryId(0) == firstId && nmModal.EditorTitle.EndsWith("\n가까운 순 1번째"),
                "시작 카드는 「근처 장소」 — 목록 가까운 순 (최대 10곳)");
            Do(() => NmButton("close").onClick.Invoke());
            Wait(closed, 2f, "× — 닫힘");
            Sleep(3f);
            Check(closed, "× 로 닫으면 다시 안 뜬다 (앱을 켤 때 한 번)");

            // 2) 설정 끔 — 시작엔 안 뜨고, 위로 밀면 뜬다
            Do(() =>
            {
                NmStartPref(false);
                nmModal.EditorStartShown = false;
                nmModal.EditorLive(0f);
            });
            Sleep(4f);
            Check(() => closed() && nmModal.EditorStartShown, "설정 「앱 시작 시 근처 장소 보기」를 끄면 시작에 안 뜬다");
            Do(() => ok = NmSwipeUp());
            Wait(() => nmModal.EditorShowing, 3f, "위로 밀기 — 뜸");
            Check(() => ok && nmModal.EditorMode == "Nearby" && nmModal.EditorCount >= 1, "설정을 꺼도 AR 화면을 위로 밀면 「근처 장소」");
            Do(() => NmButton("close").onClick.Invoke());
            Wait(closed, 2f, "닫힘");

            // 3) 백그라운드에서 돌아오면 — 시작 카드는 안 뜨고(앱을 껐다 켤 때만), 위로 밀면 뜬다
            Do(() =>
            {
                NmStartPref(true);
                nmModal.EditorStartShown = false;
                nmModal.EditorLive(0f);
                nmModal.EditorResume();
            });
            Sleep(4f);
            Check(() => closed() && nmModal.EditorStartShown, "백그라운드에서 돌아오면 시작 「근처 장소」는 안 뜬다 (앱을 껐다 켤 때만)");
            Do(() => ok = NmSwipeUp());
            Wait(() => nmModal.EditorShowing, 3f, "위로 밀기 — 뜸");
            Check(() => ok && nmModal.EditorMode == "Nearby" && nmModal.EditorCount >= 1, "백그라운드에서 돌아와도 위로 밀면 「근처 장소」");
            Do(() => NmButton("close").onClick.Invoke());
            Wait(closed, 2f, "닫힘");

            // 4) 설정 줄 — 「앱 시작 시 근처 장소 보기」를 누르면 꺼지고 켜진다 (스위치 그림도)
            Check(() =>
            {
                var panel = UnityEngine.Object.FindAnyObjectByType<R0926SettingsPanel>(FindObjectsInactive.Include);
                Button row = null;
                foreach (var bt in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
                    if (bt.name == "RowNearStart") { row = bt; break; }
                if (panel == null || row == null) { log.Add("  설정 화면 · 줄 없음"); return false; }
                var sw = typeof(R0926SettingsPanel).GetField("nearStartSwitch", NmF)?.GetValue(panel) as Image;
                var on = typeof(R0926SettingsPanel).GetField("switchOn", NmF)?.GetValue(panel) as Sprite;
                var off = typeof(R0926SettingsPanel).GetField("switchOff", NmF)?.GetValue(panel) as Sprite;
                var title = row.transform.Find("Title")?.GetComponent<Text>();
                NmStartPref(true);
                row.onClick.Invoke();
                bool offOk = !R0926PlaceSettings.NearOnStart && sw != null && sw.sprite == off;
                row.onClick.Invoke();
                bool onOk = R0926PlaceSettings.NearOnStart && sw != null && sw.sprite == on;
                log.Add($"  설정 줄 '{(title != null ? title.text : "-")}' · 한 번 누름 → {(offOk ? "꺼짐" : "?")} · 또 누름 → {(onOk ? "켜짐" : "?")}");
                return offOk && onOk && sw.transform.IsChildOf(row.transform);
            }, "설정 줄을 누르면 「앱 시작 시 근처 장소 보기」가 꺼지고 켜진다 (스위치 그림도)");
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
            NmBoot(true, true);
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
            Do(() => { if (!nmModal.EditorShowing) log.Add("  카드가 안 뜬 까닭: " + NmWhy(a)); });
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
            Check(() =>
            {
                var p = NmOpenSpot();   // 지금 UI · 오브젝트가 없는 자리 — 막힌 까닭이 하늘뿐이게
                bool can = p.x >= 0f && nmModal.EditorCanSwipeUp(p);
                bool opened = p.x >= 0f && nmModal.EditorSwipeUp(p, p + NmUp(0.25f));
                log.Add($"  하늘을 보는 동안 위로 밀기: 빈 자리 {(p.x >= 0f ? "찾음" : "없음 — 가운데 " + NmSpotWhy(new Vector2(Screen.width * 0.5f, Screen.height * 0.3f)) + " · 왼쪽 아래 " + NmSpotWhy(new Vector2(Screen.width * 0.15f, Screen.height * 0.2f)))} · 받음 {can} · 뜸 {opened} · 카드 {nmModal.EditorMode}");
                return p.x >= 0f && sky.SkyShowing && !can && !opened && nmModal.EditorMode == "Found";
            }, "하늘 오브젝트가 떠 있는 동안엔 AR 화면을 위로 밀어도 「근처 장소」가 안 뜬다 (하늘보기 손짓과 겹치지 않게)");

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
            NmBoot(true, true);
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
            Do(() => { if (nmModal.EditorCurrent != av) log.Add("  카드가 안 뜬 까닭: " + NmWhy(av)); });
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

            // 3) 공공기관(관광공사) · 공공데이터(근처 가게 · 11-18 N8a) · 공공교통 — 떠 있으면
            NmKind("공공기관", "near_tour", () => UnityEngine.Object.FindAnyObjectByType<TourAPIManager>()?.GetSpawnedObjects()?.Values);
            NmKind("공공데이터(가게)", "near_store", () => UnityEngine.Object.FindAnyObjectByType<StoreManager>()?.GetSpawnedObjects()?.Values);
            NmKind("지하철", "near_subway", () => UnityEngine.Object.FindAnyObjectByType<SubwayManager>()?.GetSpawnedObjects()?.Values);
            NmKind("기차역", "near_train", () => UnityEngine.Object.FindAnyObjectByType<TrainStationManager>()?.GetSpawnedObjects()?.Values);
            NmKind("터미널", "near_terminal", () => UnityEngine.Object.FindAnyObjectByType<TerminalManager>()?.GetSpawnedObjects()?.Values);
            Do(NmFinish);
        }

        // ── 11-20 H6 「근처 장소」의 보물찾기 — 뜨기는 하지만 멀리서 두 번 눌러도 열지 않고 「직접 찾아가서」 안내 (상자 앞 4m 면 연다 · 「발견한 장소」는 nearkinds) ──
        // 운영 서버에 묻지 않는다 — 목록 줄은 앱 안 보물찾기 목록(R0926HuntList.Spots)에 시험 게임 하나, 상자는 프리팹을 직접 띄운다.
        // 누를 때마다 보물찾기의 「첫 번째 누름」을 지워 두어 여는 데(서버에 찾았다고 알림)까지 가지 않는다. 로그엔 번호 · 개수만
        private static void PlanNearHunt()
        {
            const int HuntId = 990002;
            const string GoFind = "보물상자는 직접 찾아가서 두 번 눌러야 열려요";
            string entryId = "hunt:" + HuntId;
            GameObject chest = null;
            object fake = null;
            int k = -1, tapped = -1;
            float nextList = 0f;
            var huntT = typeof(R0926HuntTreasure).GetNestedType("Hunt", System.Reflection.BindingFlags.NonPublic);
            Func<System.Collections.IList> hunts = () => (System.Collections.IList)typeof(R0926HuntTreasure).GetField("hunts", NmS).GetValue(null);
            var tapIdF = typeof(R0926HuntTreasure).GetField("tapId", NmS);
            Func<System.Collections.IList> toasts = () =>
            {
                var tm = ToastManager.Instance;
                return tm != null ? typeof(ToastManager).GetField("live", NmF)?.GetValue(tm) as System.Collections.IList : null;
            };
            Func<string, bool> toastHas = part =>
            {
                var l = toasts();
                if (l == null) return false;
                foreach (var o in l) if ((o.GetType().GetField("message")?.GetValue(o) as string ?? "").Contains(part)) return true;
                return false;
            };
            Func<int> card = () =>
            {
                for (int i = 0; i < nmModal.EditorCount; i++) if (nmModal.EditorEntryId(i) == entryId) return i;
                return -1;
            };
            Func<string> lastLine = () =>
            {
                var parts = nmModal.EditorTitle.Split('\n');
                return parts[parts.Length - 1];
            };
            Func<bool> chestIcon = () =>
            {
                for (int s = 0; s < 5; s++)
                {
                    if (!nmModal.EditorPhotoShown(s)) continue;
                    var ic = typeof(R0926NearPhoto).GetField("icon", NmF)?.GetValue(nmModal.EditorPhotoFrame(s)) as Image;
                    if (ic != null && ic.enabled && ic.sprite != null && ic.sprite.name == "r0926_i_treasure") return true;
                }
                return false;
            };
            // 로그인 안 한 에디터에선 보물찾기가 다음 프레임에 지켜보던 보물을 치운다 — 한 걸음 안에서 넣고 · 누르고 · 뺀다.
            // 보물찾기가 받은 누름(tapId)은 한 번 누를 때마다 읽고 지운다 — 두 번째 누름이 보물찾기에 닿아 여는 일이 없게
            Action<int> tap = n =>
            {
                if (fake != null) hunts().Add(fake);
                try
                {
                    var l = toasts();
                    if (l != null) foreach (var o in l) o.GetType().GetField("message")?.SetValue(o, "");
                    tapped = 0;
                    for (int i = 0; i < n; i++)
                    {
                        tapIdF.SetValue(null, 0);
                        nmModal.EditorTap();
                        int got = (int)tapIdF.GetValue(null);
                        if (got != 0) tapped = got;
                    }
                }
                finally
                {
                    if (fake != null) hunts().Remove(fake);
                    tapIdF.SetValue(null, 0);
                }
            };

            NmBoot(true, true);
            NmWaitList(1);
            Do(() =>
            {
                R0926HuntApi.EditorBase = NmDeadStub;   // 혹시라도 보물찾기가 서버에 물으면 죽은 주소로 (NmFinish 가 되돌린다)
                NmClearAround();
            });

            // 1) 목록에 시험 보물찾기 한 줄(참가한 게임 · 자리 공개 · 바로 앞) — 「근처 장소」에 든다 · 상자가 AR 에 없으면 상자 그림
            Do(() =>
            {
                var vl = VirtualLocation.Instance;
                if (vl == null) throw new Exception("가상 위치 없음");
                R0926HuntList.Spots.RemoveAll(s => s.id == HuntId);
                R0926HuntList.Spots.Add(new R0926HuntList.Spot { id = HuntId, title = "근처 보물 시험", coord = R0926HuntList.Coord.Exact, lat = vl.Latitude + 6 / 111320.0, lon = vl.Longitude, live = true, mine = true });
                nextList = 0f;
            });
            Wait(() =>
            {
                if (NmListPicks().Exists(e => e.id == entryId)) return true;
                if (Time.unscaledTime >= nextList)
                {
                    nextList = Time.unscaledTime + 1f;
                    var pl = UnityEngine.Object.FindAnyObjectByType<PlaceListManager>(FindObjectsInactive.Include);
                    if (pl != null && pl.isActiveAndEnabled) pl.UpdateUI();
                }
                return false;
            }, 6f, "목록에 시험 보물찾기 줄");
            Do(() => nmModal.EditorOpenNearby());
            Wait(() => nmModal.EditorShowing && nmModal.EditorMode == "Nearby", 3f, "「근처 장소」 뜸");
            Do(() => { k = card(); if (k > 0) nmModal.EditorGo(k); });
            Wait(() => k >= 0 && nmModal.EditorTarget == k && NmLine0(nmModal.EditorTitle) == "보물상자", 3f, "보물 카드로 넘김");
            Wait(chestIcon, 2f, "상자 그림");
            Wait(() => nmModal.EditorHint == "보물상자는 직접 찾아가서 열 수 있어요", 5f, "안내 글 (카드가 여럿이면 처음 3초는 「옆으로 밀면 다음」)");
            Sleep(0.4f);
            Shot("near_hunt_far");
            Check(() =>
            {
                int want = NmListPicks().FindIndex(e => e.id == entryId);
                log.Add($"  보물 카드 {k + 1}번째 (고를 줄 가운데 {want + 1}번째 · 카드 {nmModal.EditorCount}장) · 제목 {NmLine0(nmModal.EditorTitle)} / {lastLine()} · 안내 「{nmModal.EditorHint}」 · 상자 그림 {(chestIcon() ? "예" : "아니오")}");
                return k >= 0 && k == want && lastLine() == "가까운 순 " + (k + 1) + "번째"
                       && nmModal.EditorHint == "보물상자는 직접 찾아가서 열 수 있어요" && nmModal.EditorPhoto(k) && chestIcon()
                       && nmModal.EditorDistance.StartsWith("근처 장소 · ") && nmModal.EditorDistance.EndsWith("m");
            }, "「근처 장소」에 보물찾기도 든다 — 「보물상자 / 가까운 순 N번째」 · 상자 그림 · 안내 「보물상자는 직접 찾아가서 열 수 있어요」");
            Do(() => tap(2));
            Sleep(0.2f);
            Check(() => tapped == 0 && toastHas(GoFind) && nmModal.EditorShowing && nmModal.EditorMode == "Nearby",
                  "상자가 AR 에 없으면 두 번 눌러도 열지 않는다 — 「" + GoFind + "」 · 카드는 그대로");

            // 2) 상자가 AR 에 떠 있어도(12m · 카드도 3D) 멀면 열지 않는다 — 보물찾기 목록에 그 상자가 있어도
            Do(() =>
            {
                var ht = UnityEngine.Object.FindAnyObjectByType<R0926HuntTreasure>(FindObjectsInactive.Include);
                var prefab = ht != null ? typeof(R0926HuntTreasure).GetField("treasurePrefab", NmF).GetValue(ht) as GameObject : null;
                if (prefab == null) throw new Exception("보물상자 프리팹 없음");
                chest = UnityEngine.Object.Instantiate(prefab);
                chest.name = "HuntTreasure_" + HuntId;
                chest.transform.localScale = Vector3.one;
                ARObjectZoomController.Fit(chest);
                R0926HuntTreasure.SpawnedObjects["hunt_" + HuntId] = chest;
                fake = Activator.CreateInstance(huntT, true);
                huntT.GetField("id").SetValue(fake, HuntId);
                huntT.GetField("title").SetValue(fake, "근처 보물 시험");
                huntT.GetField("nextPoll").SetValue(fake, float.MaxValue);
                huntT.GetField("obj").SetValue(fake, chest);
                NmPut(chest, 12f);
            });
            Wait(() => k >= 0 && nmModal.EditorItem(k) == chest && !nmModal.EditorPhoto(k), 4f, "상자가 뜨면 카드도 3D 상자");
            Sleep(0.5f);
            Shot("near_hunt_chest");
            Do(() => tap(2));
            Sleep(0.2f);
            Check(() =>
            {
                log.Add($"  12m — 보물찾기가 받은 누름 {(tapped == 0 ? "없음" : "있음")} · 안내 {(toastHas(GoFind) ? "뜸" : "안 뜸")} · 카드 3D {(!nmModal.EditorPhoto(k) ? "예" : "아니오")}");
                return tapped == 0 && toastHas(GoFind) && nmModal.EditorItem(k) == chest;
            }, "상자가 보여도 멀면(12m) 두 번 눌러 열지 않는다 — 안내만");

            // 3) 상자 앞(2m) — 「발견한 장소」와 같이 누름이 보물찾기로 간다 (한 번 = 두 번째를 기다림)
            Do(() => NmPut(chest, 2f));
            Sleep(0.4f);
            Do(() => tap(1));
            Check(() =>
            {
                log.Add($"  2m — 보물찾기가 받은 누름 {(tapped == HuntId ? "시험 게임" : tapped == 0 ? "없음" : "다른 게임")} · 안내 {(toastHas(GoFind) ? "뜸" : "안 뜸")}");
                return tapped == HuntId && !toastHas(GoFind);
            }, "상자 앞(2m)이면 「근처 장소」에서도 누름이 보물찾기로 간다 (AR 에서 누른 것과 같은 길)");

            // 끝 — 시험 게임 · 상자 치우고 카드 닫기
            Do(() =>
            {
                R0926HuntList.Spots.RemoveAll(s => s.id == HuntId);
                R0926HuntTreasure.SpawnedObjects.Remove("hunt_" + HuntId);
                if (chest != null) UnityEngine.Object.Destroy(chest);
                var close = NmButton("close");
                if (close != null) close.onClick.Invoke();
            });
            Wait(() => nmModal.EditorMode == "None" && nmModal.EditorCount == 0 && !nmModal.EditorCardOn, 3f, "닫힘");
            Check(() => !R0926HuntList.Spots.Exists(s => s.id == HuntId) && !R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + HuntId) && nmModal.EditorMode == "None",
                  "되돌림 — 시험 보물찾기 · 상자 없음 · 카드 닫힘");
            Do(NmFinish);
        }

        // ── 공공기관(관광공사) · 공공교통 — 광화문 (교통 칩을 켠 채로 시작 · 끝나면 편집기 저장값 되돌림) ──
        private static void PlanNearPublic()
        {
            Func<Dictionary<string, GameObject>> tour = () => UnityEngine.Object.FindAnyObjectByType<TourAPIManager>()?.GetSpawnedObjects();
            Func<Dictionary<string, GameObject>> subway = () => UnityEngine.Object.FindAnyObjectByType<SubwayManager>()?.GetSpawnedObjects();
            Func<Dictionary<string, GameObject>> train = () => UnityEngine.Object.FindAnyObjectByType<TrainStationManager>()?.GetSpawnedObjects();
            Func<Dictionary<string, GameObject>> terminal = () => UnityEngine.Object.FindAnyObjectByType<TerminalManager>()?.GetSpawnedObjects();

            NmBoot(true, true);
            Wait(() => NmVisibleCount(tour()) > 0 && NmVisibleCount(subway()) > 0, 45f, "공공기관 · 지하철 오브젝트 뜸");
            Do(() => log.Add($"  떠 있는 것 — 공공기관 {NmVisibleCount(tour())} · 지하철 {NmVisibleCount(subway())} · 기차역 {NmVisibleCount(train())} · 터미널 {NmVisibleCount(terminal())}"));
            Do(NmClearAround);
            NmKind("공공기관", "near_tour", () => tour()?.Values);
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
                log.Add((ok ? "PASS " : "FAIL ") + label + " — 4m 안이면 이름으로 카드에");
            });
            Do(() => { if (obj != null) NmPut(obj, 12f); });
            Wait(() => obj == null || nmModal.EditorCount == 0 && !nmModal.EditorCardOn, 2f, label + " 치움");
        }

        // ── 도우미 ───────────────────────────────────────────
        // 시험마다 처음 — 시작화면 · 로그인 안내 · 첫 안내를 넘기고, 근처를 걷는 가상 사용자를 멈춘다.
        // near: 근처 카드를 쓰는 시험 — 처음부터 곁에 있던 것을 치운 뒤(NmClearAround) 켠다. 시작 「근처 장소」는 nearstart 만 · 「발견한 장소」는 바로(AR 화면이 뜬 지 30초로)
        // keep: 치운 뒤에 늦게 뜬 것(목록이 더 오거나 매니저가 제자리로 돌려놓은 것)도 시험 내내 6m 밖으로 (NmKeep)
        private static void NmBoot(bool near = true, bool keep = false)
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                nmRun = true;
                nmNear = near;
                nmKeepWanted = keep;
                NmStash();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                // 에디터의 가상 사용자(근처 사람 woopang — 15~20m 를 걸어 다닌다)가 4m 안에 들어와 카드에 끼지 않게
                var vt = UnityEngine.Object.FindAnyObjectByType<P2PVirtualUserTester>();
                if (vt != null) { vt.StopSimulation(); vt.StopAllCoroutines(); }
                if (P2PManager.Instance != null) P2PManager.Instance.RemoveTestUserAvatar("3");
                nmModal = UnityEngine.Object.FindAnyObjectByType<R0926NearModal>(FindObjectsInactive.Include);
                R0926NearModal.EditorQuiet = true;
                if (near && nmModal != null)
                {
                    nmModal.EditorStartShown = true;
                    nmModal.EditorLive(30f);
                    nmModal.EditorForget();
                }
                nmHome.Clear();
                nmPicked.Clear();
                nmPlaced.Clear();
                AimCamera(0f, 0f);
            });
            Sleep(0.6f);
            Check(() => nmModal != null && nmModal.isActiveAndEnabled, "준비: 근처 카드 (NearModal0926 · R0926NearModal)");
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

        // a 말고 AR 에 보이는 다른 우팡 장소 하나 (이름도 다른 것)
        private static GameObject NmOther(GameObject a)
        {
            string an = a != null ? NmName(a) : null;
            foreach (var go in NmPlaces(12, false)) if (go != a && NmName(go) != an) return go;
            return null;
        }

        // 「근처 장소」에 들 목록 줄 — 목록(필터 · 분류 반영) 가까운 순 장소 · 사람 · 보물찾기 10곳 (광고 줄 · 맨 위 줄 · 축제 · 보호소 · 찾은 보물 · 거리 슬라이더 밖 보물은 빼고 — R0926NearModal.OpenNearby 와 같게)
        private static List<PlaceListManager.ListEntry> NmListPicks()
        {
            var r = new List<PlaceListManager.ListEntry>();
            var pl = UnityEngine.Object.FindAnyObjectByType<PlaceListManager>(FindObjectsInactive.Include);
            var list = pl != null ? pl.ShownEntries : null;
            if (list == null) return r;
            float far = PlayerPrefs.GetFloat("MaxDisplayDistance", 5000f);
            foreach (var e in list)
            {
                if (e.promo || e.top) continue;
                if (e.kind == PlaceListManager.EntryKind.Hunt)
                {
                    if (e.huntId <= 0 || e.distance > far || e.distanceText == R0926HuntList.FoundText) continue;
                }
                else if (e.kind != PlaceListManager.EntryKind.Place && e.kind != PlaceListManager.EntryKind.Person) continue;
                r.Add(e);
            }
            if (r.Exists(x => x.kind == PlaceListManager.EntryKind.Hunt))
                r = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(r, x => x.distance));   // 같은 거리면 목록 순서 (OrderBy 는 안정 정렬)
            if (r.Count > 10) r.RemoveRange(10, r.Count - 10);
            return r;
        }

        // 목록에 n곳이 들 때까지 — 목록 창이 닫혀 있으면 줄이 묵어 있어 1초마다 다시 만든다
        private static void NmWaitList(int n)
        {
            float next = 0f;
            Wait(() =>
            {
                if (NmListPicks().Count >= n) return true;
                if (Time.unscaledTime >= next)
                {
                    next = Time.unscaledTime + 1f;
                    var pl = UnityEngine.Object.FindAnyObjectByType<PlaceListManager>(FindObjectsInactive.Include);
                    if (pl != null && pl.isActiveAndEnabled) pl.UpdateUI();
                }
                return false;
            }, 40f, "목록 장소 · 사람 " + n + "곳");
        }

        private static Vector2 NmUp(float frac) => new Vector2(0f, Screen.height * frac);

        // 맨 AR 화면 자리 — 위로 밀기를 받는 곳 (UI · 오브젝트 · 덮는 창 없는 곳). 없으면 (-1, -1)
        private static Vector2 NmFree()
        {
            float[] xs = { 0.5f, 0.3f, 0.7f, 0.15f, 0.85f };
            float[] ys = { 0.3f, 0.35f, 0.4f, 0.25f, 0.45f, 0.5f, 0.2f, 0.55f, 0.6f, 0.65f, 0.7f, 0.15f, 0.75f, 0.8f, 0.85f, 0.1f, 0.9f };
            foreach (float fy in ys)
                foreach (float fx in xs)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    if (nmModal.EditorCanSwipeUp(p)) return p;
                }
            return new Vector2(-1f, -1f);
        }

        // UI 가 없고 맨 앞에 맞는 물체가 go 인 자리 (카드에 담긴 것의 콜라이더 위). 없으면 (-1, -1)
        private static Vector2 NmOnHeld(GameObject go)
        {
            var cam = Camera.main;
            if (cam == null || go == null) return new Vector2(-1f, -1f);
            Physics.SyncTransforms();
            Func<Vector2, bool> on = p => WorldTouch.TopUI(p) == null
                && Physics.Raycast(cam.ScreenPointToRay(p), out var h, 2000f, Physics.DefaultRaycastLayers & ~NmMask, QueryTriggerInteraction.Collide)
                && h.collider.transform.IsChildOf(go.transform);
            // 먼저 그 물체의 콜라이더가 화면에 걸친 네모 안을 촘촘히 — 작은 물체는 화면 고정 격자 사이로 빠진다 (11-21)
            foreach (var c in go.GetComponentsInChildren<Collider>(false))
            {
                if (!c.enabled) continue;
                var bb = c.bounds;
                Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
                bool front = false;
                for (int i = 0; i < 8; i++)
                {
                    var s = cam.WorldToScreenPoint(bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                    if (s.z <= 0f) continue;
                    front = true;
                    lo = Vector2.Min(lo, s);
                    hi = Vector2.Max(hi, s);
                }
                if (!front) continue;
                var r = Rect.MinMaxRect(Mathf.Max(0f, lo.x), Mathf.Max(0f, lo.y), Mathf.Min(Screen.width - 1f, hi.x), Mathf.Min(Screen.height - 1f, hi.y));
                if (r.width <= 0f || r.height <= 0f) continue;
                for (int iy = 1; iy < 12; iy++)
                    for (int ix = 1; ix < 12; ix++)
                    {
                        var p = new Vector2(Mathf.Lerp(r.xMin, r.xMax, ix / 12f), Mathf.Lerp(r.yMin, r.yMax, iy / 12f));
                        if (on(p)) return p;
                    }
            }
            float[] xs = { 0.5f, 0.3f, 0.7f, 0.15f, 0.85f };
            float[] ys = { 0.5f, 0.55f, 0.45f, 0.6f, 0.4f, 0.65f, 0.35f, 0.7f, 0.3f, 0.75f, 0.25f, 0.8f, 0.2f };
            foreach (float fy in ys)
                foreach (float fx in xs)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    if (on(p)) return p;
                }
            return new Vector2(-1f, -1f);
        }

        // NmOnHeld 가 자리를 못 찾았을 때 — 콜라이더마다 크기 · 화면 범위 · 가운데에서 덮은 UI · 레이가 먼저 맞은 것 (장소 이름은 적지 않는다)
        private static string NmHeldWhy(GameObject go)
        {
            var cam = Camera.main;
            if (cam == null || go == null) return "    (카메라 없음)";
            var sb = new System.Text.StringBuilder();
            foreach (var c in go.GetComponentsInChildren<Collider>(false))
            {
                if (!c.enabled || ((1 << c.gameObject.layer) & NmMask) != 0) continue;
                var bb = c.bounds;
                Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < 8; i++)
                {
                    var sp = cam.WorldToScreenPoint(bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                    if (sp.z <= 0f) continue;
                    lo = Vector2.Min(lo, sp);
                    hi = Vector2.Max(hi, sp);
                }
                Vector2 mid = cam.WorldToScreenPoint(bb.center);
                var ui = WorldTouch.TopUI(mid);
                string hit = "없음";
                if (Physics.Raycast(cam.ScreenPointToRay(mid), out var h, 2000f, Physics.DefaultRaycastLayers & ~NmMask, QueryTriggerInteraction.Collide))
                    hit = h.collider.transform.IsChildOf(go.transform) ? (h.collider == c ? "이것" : "같은 장소의 다른 콜라이더")
                        : $"다른 것 (층 {h.collider.gameObject.layer} · {h.collider.GetType().Name}{(h.collider.isTrigger ? " 트리거" : "")} · {(h.distance < Vector3.Distance(cam.transform.position, bb.center) ? "이것보다 앞" : "이것보다 뒤")})";
                sb.Append($"    {c.GetType().Name}{(c.isTrigger ? " 트리거" : "")} 층 {c.gameObject.layer} · 크기 {bb.size.x:0.00}×{bb.size.y:0.00}×{bb.size.z:0.00}m"
                    + $" · 화면 x {lo.x / Screen.width:0.00}~{hi.x / Screen.width:0.00} y {lo.y / Screen.height:0.00}~{hi.y / Screen.height:0.00}"
                    + $" · 가운데 UI {(ui != null ? ui.name : "-")} · 레이 {hit}\n");
            }
            return sb.Length > 0 ? sb.ToString().TrimEnd('\n') : "    (AR 층 콜라이더 없음)";
        }

        // UI 도 오브젝트도 없는 자리 (카드가 받을지와 상관없이) — 하늘 · 덮는 창 때문에만 막히는지 볼 때. 없으면 (-1, -1)
        private static Vector2 NmOpenSpot()
        {
            var cam = Camera.main;
            if (cam == null) return new Vector2(-1f, -1f);
            Physics.SyncTransforms();
            float[] xs = { 0.5f, 0.3f, 0.7f, 0.15f, 0.85f };
            float[] ys = { 0.3f, 0.35f, 0.4f, 0.25f, 0.45f, 0.5f, 0.2f, 0.55f, 0.6f, 0.65f, 0.7f, 0.15f, 0.75f, 0.8f, 0.85f, 0.1f, 0.9f };
            foreach (float fy in ys)
                foreach (float fx in xs)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    if (WorldTouch.TopUI(p) == null && !Physics.Raycast(cam.ScreenPointToRay(p), 2000f, Physics.DefaultRaycastLayers & ~NmMask, QueryTriggerInteraction.Collide)) return p;
                }
            return new Vector2(-1f, -1f);
        }

        // 맨 AR 화면에서 화면 높이 25% 를 위로 민다 (1초 안에 민 것)
        private static bool NmSwipeUp()
        {
            Physics.SyncTransforms();
            var p = NmFree();
            if (p.x < 0f)
            {
                log.Add("  위로 밀 맨 AR 자리가 없다 — " + NmWhy() + " · 가운데 " + NmSpotWhy(new Vector2(Screen.width * 0.5f, Screen.height * 0.3f))
                        + " · 왼쪽 아래 " + NmSpotWhy(new Vector2(Screen.width * 0.15f, Screen.height * 0.2f)));
                return false;
            }
            bool ok = nmModal.EditorSwipeUp(p, p + NmUp(0.25f));
            log.Add($"  위로 밀기 ({p.x / Screen.width:0.00}, {p.y / Screen.height:0.00}) → {(ok ? "뜸" : "안 뜸")}");
            return ok;
        }

        // 카드를 가운데서 화면 높이의 frac 만큼 끌어내렸다 놓는다 — 한 프레임이라 튕김 속도는 0 (끈 거리로만 닫는다). 놓은 때 내려간 만큼을 돌려준다
        private static float NmPullDown(float frac)
        {
            var c = NmRect(nmModal.EditorCard).center;
            nmModal.EditorDrag(c, c - NmUp(frac));
            float p = nmModal.EditorPullOffset;
            log.Add($"  아래로 {frac:0.00} 끌기 → {p:0} · 닫힘 {(nmModal.EditorClosing ? "예" : "아니오")}");
            return p;
        }

        private static int NmPhotoCount(int n)
        {
            int c = 0;
            for (int k = 0; k < n; k++) if (nmModal.EditorPhotoTexture(k) != null) c++;
            return c;
        }

        private static void NmStartPref(bool on) => R0926PlaceSettings.NearOnStart = on;

        // 처음부터 6m 안에 있던 것은 멀리 (시험이 고른 것만 카드에 들게) — 그 뒤 근처 카드를 켠다 (NmBoot(true))
        private static void NmClearAround()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                int n = 0;
                foreach (var go in NmAll())
                {
                    if (nmPicked.Contains(go)) continue;
                    if (NmReach(go, cam.transform.position, out Vector3 d, out float span) > 6f) continue;
                    if (!nmHome.ContainsKey(go)) nmHome[go] = go.transform.position;
                    go.transform.position += (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back) * (40f + span);
                    n++;
                }
                if (n > 0) log.Add("  처음부터 6m 안에 있던 오브젝트 " + n + "개를 40m 밖으로");
            }
            if (nmKeepWanted && !nmKeep)
            {
                nmKeep = true;
                EditorApplication.update -= NmKeep;
                EditorApplication.update += NmKeep;
            }
            if (nmNear && nmModal != null) R0926NearModal.EditorQuiet = false;
        }

        // 시험 동안 늦게 뜬(또는 매니저가 제자리로 돌려놓은) 것도 6m 밖으로 — 시험이 둔 것(NmPut) · 고른 것만 카드에 들게. 편집기가 프레임마다 부른다
        private static void NmKeep()
        {
            if (!nmKeep || !EditorApplication.isPlaying) return;
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var go in NmAll())
            {
                if (nmPicked.Contains(go) || nmPlaced.Contains(go)) continue;
                if (NmReach(go, cam.transform.position, out Vector3 d, out float span) > 6f) continue;
                if (!nmHome.ContainsKey(go))
                {
                    nmHome[go] = go.transform.position;
                    log.Add("  늦게 6m 안에 든 '" + NmName(go) + "' 를 40m 밖으로");
                }
                go.transform.position += (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back) * (40f + span);
            }
        }

        // 카메라에서 오브젝트의 가장 가까운 겉면(콜라이더)까지 수평 거리 — 크게 키운 3D 모델(배율 10 · 상자 10m)은 가운데가 6m 밖이어도
        // 상자가 화면을 덮어 위로 밀기 · 누르기를 막는다 (11-21 운영 장소 하나가 그렇게 커져 nearmulti 가 맨 AR 자리를 못 찾음). span = 수평 반지름
        private static float NmReach(GameObject go, Vector3 me, out Vector3 d, out float span)
        {
            d = go.transform.position - me;
            d.y = 0f;
            float r = d.magnitude;
            span = 0f;
            foreach (var c in go.GetComponentsInChildren<Collider>(false))
            {
                if (!c.enabled) continue;
                var bb = c.bounds;
                Vector3 q = bb.ClosestPoint(me) - me;
                q.y = 0f;
                r = Mathf.Min(r, q.magnitude);
                span = Mathf.Max(span, Mathf.Max(bb.extents.x, bb.extents.z));
            }
            return r;
        }

        // 카드가 안 뜬 · 위로 밀기를 안 받은 까닭 (기록용 — 거리 숫자 · 자리는 남기지 않는다)
        private static string NmWhy(GameObject want = null)
        {
            if (nmModal == null) return "근처 카드 없음";
            var t = typeof(R0926NearModal);
            float now = Time.unscaledTime;
            object covered = t.GetMethod("Covered", NmF)?.Invoke(nmModal, null);
            object ready = t.GetMethod("FoundReady", NmF)?.Invoke(nmModal, new object[] { now });
            float resume = (float)t.GetField("resumeAt", NmF).GetValue(nmModal);
            var near = new List<string>();
            var cam = Camera.main;
            if (cam != null)
                foreach (var go in NmAll())
                {
                    Vector3 d = go.transform.position - cam.transform.position;
                    d.y = 0f;
                    if (d.magnitude <= 4f) near.Add(NmName(go) + (nmPlaced.Contains(go) || nmPicked.Contains(go) ? "(시험)" : ""));
                }
            string w = want == null ? "" : $" · 기다린 것: 닫음 {nmModal.EditorClosedFound(want)} · 잠시 쉼 {nmModal.EditorDismissed(want)} · 보임 {t.GetMethod("Visible", NmF)?.Invoke(nmModal, new object[] { want })} · 켜짐 {want.activeInHierarchy}";
            return $"카드 {nmModal.EditorMode}/{nmModal.EditorCount}개 · 켜짐 {nmModal.EditorCardOn} · 닫는 중 {nmModal.EditorClosing} · 가림 {covered} (안내 {R0926GuideOverlay.BlocksWorld} · 창 {NmCovering()} · 시작화면 {BootOverlay.Showing})"
                   + $" · 조용히 {R0926NearModal.EditorQuiet} · 발견 준비 {ready} (뜬 지 {now - nmModal.EditorLiveAt:0.0}초 · 돌아온 지 {(resume < 0f ? "-" : (now - resume).ToString("0.0"))}) · 4m 안 {near.Count}: {string.Join(", ", near)}{w}";
        }

        private static string NmSpotWhy(Vector2 p)
        {
            var top = WorldTouch.TopUI(p);
            string ui = "없음";
            if (top != null)
            {
                var tr = top.transform;
                ui = tr.name;
                for (int i = 0; i < 3 && tr.parent != null; i++) { tr = tr.parent; ui = tr.name + "/" + ui; }
            }
            string hit = "없음";
            var cam = Camera.main;
            if (cam != null && Physics.Raycast(cam.ScreenPointToRay(p), out var h, 2000f, Physics.DefaultRaycastLayers & ~NmMask, QueryTriggerInteraction.Collide))
            {
                var c = h.collider;
                var tr = c.transform;
                hit = tr.name;
                for (int i = 0; i < 3 && tr.parent != null; i++) { tr = tr.parent; hit = tr.name + "/" + hit; }
                Vector3 bs = c.bounds.size, ls = c.transform.lossyScale;
                string box = c is BoxCollider bc ? $" 상자 {bc.size.x:0.##}×{bc.size.y:0.##}×{bc.size.z:0.##}" : "";
                var owner = c.GetComponentInParent<DoubleTap3D>(true);
                Vector3 ms = Vector3.zero;
                if (owner != null)
                {
                    bool any = false;
                    var mb = new Bounds();
                    foreach (var r in owner.GetComponentsInChildren<Renderer>(false))
                    {
                        if (!any) { mb = r.bounds; any = true; } else mb.Encapsulate(r.bounds);
                    }
                    ms = any ? mb.size : Vector3.zero;
                }
                hit += $" ({c.GetType().Name}{(c.isTrigger ? " · 트리거" : "")} · 월드 크기 {bs.x:0.0}×{bs.y:0.0}×{bs.z:0.0}{box} · 배율 {ls.x:0.###} · 모델 그림 크기 {ms.x:0.0}×{ms.y:0.0}×{ms.z:0.0})";
            }
            return $"UI {ui} · 물체 {hit}";
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
            Add(UnityEngine.Object.FindAnyObjectByType<StoreManager>()?.GetSpawnedObjects());
            Add(R0926HuntTreasure.SpawnedObjects);
            if (P2PManager.Instance != null) Add(P2PManager.Instance.GetActiveAvatars());
            return all;
        }

        // 카메라 앞(수평) d 미터 · 오른쪽으로 deg 도 · 눈높이에서 up 미터
        private static void NmPut(GameObject go, float d, float deg = 0f, float up = -0.6f)
        {
            var cam = Camera.main;
            if (cam == null || go == null) return;
            nmPlaced.Add(go);
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

        // 켜진 렌더러만 (모델마다 꺼 둔 부품은 빼고)
        private static bool NmLayersOn(GameObject go, Func<int, bool> ok)
        {
            if (go == null) return false;
            int n = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                n++;
                if (!ok(r.gameObject.layer)) return false;
            }
            return n > 0;
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

        // 시험이 바꾸는 저장값(메뉴 모양 · 앱 시작 근처 장소) — 시험 전 값을 세션에 (오류로 끊겨도 NmFinish 가 되돌린다)
        private static void NmStash()
        {
            NmStashPref(NmBackupKey, NmStyleKey);
            NmStashPref(NmStartBackupKey, NmStartKey);
        }

        private static void NmStashPref(string backup, string key)
        {
            if (SessionState.GetString(backup, "").Length > 0) return;   // 끊긴 시험의 것이 남아 있으면 그게 진짜 원래 값
            SessionState.SetString(backup, PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key).ToString() : "-");
        }

        private static void NmRestorePref(string backup, string key)
        {
            string s = SessionState.GetString(backup, "");
            if (s.Length == 0) return;
            SessionState.EraseString(backup);
            if (s == "-") PlayerPrefs.DeleteKey(key);
            else if (int.TryParse(s, out int v)) PlayerPrefs.SetInt(key, v);
            PlayerPrefs.Save();
        }

        // 시험이 바꾼 것 되돌리기 — 시험 끝 · 오류로 끊긴 뒤 · 플레이를 끈 뒤 · 다음 시험을 켜기 전
        private static void NmFinish()
        {
            nmKeep = nmKeepWanted = false;
            EditorApplication.update -= NmKeep;
            if (nmRun)
            {
                R0926HuntApi.EditorTestUser = null;
                nmRun = false;
                nmNear = false;
                R0926NearModal.EditorQuiet = true;
                ProfileManager.EditorBase = null;
                R0926Nearby.EditorBase = null;
                R0926HuntApi.EditorBase = null;
                R0926FriendsMap.EditorBase = null;
                RestoreSkyPrefs();
            }
            NmRestorePref(NmBackupKey, NmStyleKey);
            NmRestorePref(NmStartBackupKey, NmStartKey);
            R0926MenuStyle.Reload();   // 값을 한 번 읽어 두므로 되돌린 값을 다시 읽게 (11-23 S8)
        }
    }
}
