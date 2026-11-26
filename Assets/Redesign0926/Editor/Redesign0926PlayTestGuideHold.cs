using System;
using UnityEngine;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 25번 대표님 「방향안내할 때 강조해서 인디케이터 보이는 건 방향안내가 끝날 때까지 유지하도록 해줘.」
        //  · 예전: 목록에서 고르면 그 장소 인디케이터 글자 뒤 분홍 스티커가 4초(focusDuration)만 — 안내는 이어지는데 강조가 사라졌다
        //  · 지금: 길안내(R0926GuideArrow)가 안내하는 곳을 붙들어(OffScreenIndicator.Held) 안내가 끝날 때까지 — 끝내면(× · 도착 · 20분 · 가까이 카드) 같이 끝
        //  · 목록에서 고른 것은 예전처럼 목록이 내려간 뒤에 붙고, 정보 창 「길안내」처럼 따로 고르지 않은 것은 바로
        //  · 좌표로만 안내하는 곳(지도 핀 · 고를 때 AR 에 없던 장소)은 그 자리에 장소가 뜨면 그때부터 (안내 셈은 그대로 좌표로)
        //  · 길안내 없이 고른 강조(하늘 판 핫플 등)는 예전처럼 4초
        private static bool ghRun, ghMoved;
        private static Target ghTb;
        private static double ghLat0, ghLon0;

        private static void GhFinish()
        {
            if (!ghRun) return;
            ghRun = false;
            R0926GuideArrow.Stop();
            if (ghMoved && ghTb != null) { ghTb.gpsLatitude = ghLat0; ghTb.gpsLongitude = ghLon0; }
            ghMoved = false;
            ghTb = null;
        }

        private static void PlanGuideHold()
        {
            GameObject a = null, b = null;
            Target ta = null, tb = null;
            OffScreenIndicator ind = null;
            R0926GuideArrow ga = null;
            float t0 = 0f;
            bool listFocus = false;   // 목록 강조(Focus)가 받아들여졌나 — 거절되면 앱도 늦춤 없이 바로 붙는다

            bool On(Target t) => t != null && t.indicator != null && t.indicator.ownerTarget == t && t.indicator.Active && t.indicator.FocusOn;
            bool Shown(Target t) => t != null && t.indicator != null && t.indicator.ownerTarget == t && t.indicator.Active;
            string S(Target t) => t == null ? "없음" : !Shown(t) ? "인디케이터 안 보임" : t.indicator.FocusOn ? "강조" : "강조 없음";
            string Held() => ind == null || ind.Held == null ? "없음" : ind.Held == ta ? "A" : ind.Held == tb ? "B" : "다른 것";
            string St() => $"A {S(ta)} · B {S(tb)} · 붙든 것 {Held()} · 안내 {(ga != null && ga.EditorGuiding ? "중" : "끝")} · {Time.unscaledTime - t0:0.0}초";
            // 목록 강조(Focus)가 false 일 때 왜인지 — OffScreenIndicator.Focus 의 조건을 그대로 (11-27 c53 에서 false 인데 늦춤 줄이 실패)
            string Why(Target t)
            {
                if (ind == null || t == null) return "인디케이터 · 대상 없음";
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var tp = typeof(OffScreenIndicator);
                var cam = ind.ViewCamera;
                bool vis = cam != null && PixelPlay.OffScreenIndicator.OffScreenIndicatorCore.IsTargetVisible(
                    PixelPlay.OffScreenIndicator.OffScreenIndicatorCore.GetScreenPosition(cam, t.transform.position));
                var list = tp.GetField("targets", F)?.GetValue(ind) as System.Collections.Generic.List<Target>;
                return $"분산 {tp.GetField("isFallbackMode", F)?.GetValue(ind)} · 억제 {tp.GetField("suppressNormalIndicators", F)?.GetValue(ind)} · 목록에 있음 {list != null && list.Contains(t)}"
                       + $" · 앵커 {t.IsAnchorReady} · 화면 안 {vis} · 박스 필요 {t.NeedBoxIndicator} · 화살표 필요 {t.NeedArrowIndicator}";
            }
            // 가상 위치에서 북쪽 · 동쪽으로 몇 m (안내 거리를 도착 거리 10m 밖으로 정해 두려고)
            void At(double north, double east, out float lat, out float lon)
            {
                var v = VirtualLocation.Instance;
                lat = (float)(v.Latitude + north / 111320.0);
                lon = (float)(v.Longitude + east / (111320.0 * Math.Cos(v.Latitude * Math.PI / 180.0)));
            }

            NmBoot(false);   // 가까이 카드는 조용히 — 이어 받기로 안내가 끝나지 않게
            NmWaitPlaces(2, true);
            Do(() =>
            {
                if (nmPicked.Count < 2) throw new Exception("AR 에 보이는 우팡 큐브가 2개 없다");
                ghRun = true;
                a = nmPicked[0];
                b = nmPicked[1];
                ta = a.GetComponentInChildren<Target>(true);
                tb = b.GetComponentInChildren<Target>(true);
                ghTb = tb;
                NmClearAround();
                NmPut(a, 15f, -10f);
                NmPut(b, 15f, 10f);
                ind = UnityEngine.Object.FindAnyObjectByType<OffScreenIndicator>();
                ga = R0926GuideArrow.Instance;
            });
            Sleep(1.2f);
            Check(() => { log.Add("  " + St()); return ind != null && ga != null && Shown(ta) && Shown(tb) && !On(ta) && !On(tb) && ind.Held == null; },
                "준비: 큐브 둘(A · B)이 15m 앞 — 인디케이터 둘 다 보임 · 강조 없음");

            // 1) 목록에서 고른 것처럼 — 길안내 + 목록 강조(목록 줄이 밝혀지고 내려가는 동안 늦게 붙음)
            Do(() =>
            {
                At(25, -6, out float lat, out float lon);
                t0 = Time.unscaledTime;
                bool started = R0926GuideArrow.Guide(lat, lon, ta, NmName(a));
                bool focused = listFocus = ind.Focus(ta, 1.5f);   // 시험은 늦춤을 1.5초로 늘려 잰다 (앱은 줄 밝힘 + 0.1초)
                log.Add($"  길안내 시작 {started} · 목록 강조 {focused}" + (focused ? "" : " — " + Why(ta)));
            });
            Check(() => { log.Add("  시작 직후: " + St()); return ind.Held == ta && ga.EditorGuiding; }, "길안내를 시작하면 그 장소를 붙든다");
            Sleep(0.5f);
            // 목록 강조가 그 순간 거절되면(앵커 · 화면 자리 — 이유는 위 줄) 앱도 붙든 강조가 바로 보인다 — 그땐 늦춤을 잴 수 없어 건너뛴다 (11-27 c46 · c53 에서 거절 · c42 · c54 는 받아들임)
            Check(() => { log.Add("  0.5초: " + St() + (listFocus ? "" : " (목록 강조가 거절돼 늦춤 없이 바로 — 늦춤 확인 건너뜀)")); return !listFocus || !On(ta); }, "목록에서 고른 것은 예전처럼 목록이 내려간 뒤에 붙는다 (늦춤 안에는 아직)");
            Sleep(1.7f);
            Check(() => { log.Add("  2.2초: " + St()); return On(ta) && !On(tb); }, "늦춤이 지나면 A 강조 (B 는 아님)");
            Shot("gh_list");

            // 2) 길안내 없이 고른 강조는 예전처럼 4초 — B 를 따로 강조해도 A 는 붙든 채
            Do(() => log.Add($"  B 따로 강조 {ind.Focus(tb)}"));
            Sleep(0.6f);
            Check(() => { log.Add("  B 따로 강조 0.6초: " + St()); return On(tb) && On(ta); }, "다른 장소를 따로 강조해도(하늘 판 핫플처럼) 안내 중인 A 는 그대로");
            Sleep(4.2f);
            Check(() => { log.Add("  7초: " + St()); return On(ta) && !On(tb) && ga.EditorGuiding; },
                "4초가 지나도 안내 중인 A 는 강조 유지 (예전엔 꺼짐) · 길안내 없이 고른 B 는 예전처럼 4초 뒤 꺼짐");
            Shot("gh_after4s");
            Do(() => R0926GuideArrow.Stop());
            Sleep(0.4f);
            Check(() => { log.Add("  안내 끝: " + St()); return !On(ta) && ind.Held == null && !ga.EditorGuiding && Shown(ta); }, "안내를 끝내면 강조도 같이 끝 (인디케이터는 그대로)");

            // 3) 정보 창 「길안내」처럼 — 장소를 알고 따로 고르지 않음 → 바로
            Do(() =>
            {
                At(25, -6, out float lat, out float lon);
                t0 = Time.unscaledTime;
                R0926GuideArrow.Guide(lat, lon, ta, NmName(a));
            });
            Sleep(0.4f);
            Check(() => { log.Add("  정보 창 길안내 0.4초: " + St()); return On(ta) && ind.Held == ta; }, "정보 창 「길안내」처럼 따로 고르지 않아도 안내하는 곳은 바로 강조");
            Do(() => R0926GuideArrow.Stop());
            Sleep(0.4f);
            Check(() => { log.Add("  안내 끝: " + St()); return !On(ta) && ind.Held == null; }, "끝내면 꺼짐");

            // 4) 좌표로만 안내(지도 핀 · 고를 때 AR 에 없던 곳) — 그 자리에 뜬 장소를 찾아 붙든다 (B 의 좌표를 잠시 옮겨 잰다 — 도착 거리 밖으로)
            Do(() =>
            {
                ghLat0 = tb.gpsLatitude; ghLon0 = tb.gpsLongitude; ghMoved = true;
                At(37, 23, out float lat, out float lon);
                tb.gpsLatitude = lat; tb.gpsLongitude = lon;
                t0 = Time.unscaledTime;
                R0926GuideArrow.Guide(lat, lon, null, tb.PlaceName);
            });
            Wait(() => On(tb), 2.5f, "좌표로만 안내 — 그 자리 장소 강조");
            Check(() => { log.Add("  좌표 안내: " + St()); return On(tb) && ind.Held == tb && !On(ta) && ga.EditorGuiding; }, "좌표로만 안내해도 그 자리에 뜬 장소(B)를 찾아 강조 · A 는 아님");
            Shot("gh_coords");
            Sleep(4.5f);
            Check(() => { log.Add("  좌표 안내 4.5초 뒤: " + St()); return On(tb) && ga.EditorGuiding; }, "좌표 안내도 끝날 때까지 강조 유지");
            Do(() => R0926GuideArrow.Stop());
            Sleep(0.4f);
            Check(() => { log.Add("  안내 끝: " + St()); return !On(tb) && ind.Held == null; }, "끝내면 꺼짐");

            // 되돌리기
            Do(GhFinish);
            Check(() => !ghMoved && tb != null && tb.gpsLatitude == ghLat0 && tb.gpsLongitude == ghLon0 && !ga.EditorGuiding && ind.Held == null, "되돌림 — B 좌표 처음대로 · 안내 없음");
        }
    }
}
