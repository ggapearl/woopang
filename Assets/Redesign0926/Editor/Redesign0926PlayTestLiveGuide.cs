using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 10번 ② 대표님 「위에 노치관련 수정안 확인했어. 1,2번 둘다 진행해.」 — 홈으로 나가도 이어지는 길안내
        //  (아이폰: 아일랜드 · 잠금 화면 실시간 현황 / 안드로이드: 진행 중 알림 — 준비만). 에디터는 네이티브를 부르지 않고 R0926LiveGuide 가 부른 순서만 남긴다.
        //  · 안내 시작 → begin (이름 · 도착 10m · 20분) — 배터리: 안내하는 동안만
        //  · × · 노치로 끝내기 → end 한 번 · 이미 끝난 뒤 또 끝내도 다시 부르지 않는다
        //  · 안내 중 다른 곳으로 다시 안내 → begin 한 번 더 (앞의 것은 네이티브가 정리)
        //  · 도착(10m 안 1초) → arrived 한 번 → 「도착했어요」 2.5초 뒤 안내가 끝나도 end 를 또 부르지 않는다 (잠금 화면은 「도착했어요」 4초 뒤 스스로 닫힌다)
        //  · 20분 → end
        private static bool lgRun;

        private static void LgFinish()
        {
            if (!lgRun) return;
            lgRun = false;
            R0926GuideArrow.Stop();
            R0926LiveGuide.EditorCalls = null;
        }

        private static void PlanLiveGuide()
        {
            var calls = new List<string>();
            R0926GuideArrow ga = null;
            string Calls() => calls.Count == 0 ? "없음" : string.Join(" / ", calls);
            // 가상 위치에서 북쪽 · 동쪽으로 몇 m
            void Go(double north, double east, string name)
            {
                var v = VirtualLocation.Instance;
                double lat = v.Latitude + north / 111320.0;
                double lon = v.Longitude + east / (111320.0 * Math.Cos(v.Latitude * Math.PI / 180.0));
                log.Add($"  안내 시작 '{name}' {R0926GuideArrow.Guide((float)lat, (float)lon, null, name)}");
            }

            NmBoot(false);   // 가까이 카드는 조용히 — 이어 받기로 안내가 끝나지 않게
            Do(() =>
            {
                lgRun = true;
                ga = R0926GuideArrow.Instance;
                if (ga == null || !ga.isActiveAndEnabled) throw new Exception("R0926GuideArrow 없음");
                R0926GuideArrow.Stop();
                R0926LiveGuide.EditorCalls = calls;
                calls.Clear();
            });

            // 1) 시작 → begin · 끝내기 → end
            Do(() => Go(200, 0, "시험 장소"));
            Check(() => { log.Add("  호출: " + Calls()); return ga.EditorGuiding && calls.Count == 1 && calls[0] == "begin 시험 장소 10 1200"; },
                "길안내를 시작하면 실시간 현황도 시작 — 이름 · 도착 10m · 20분");
            Sleep(0.6f);
            Check(() => calls.Count == 1, "안내하는 동안 다시 부르지 않는다 (거리 · 방향은 네이티브가 직접 잰다)");
            Do(() => R0926GuideArrow.Stop());
            Check(() => { log.Add("  호출: " + Calls()); return !ga.EditorGuiding && calls.Count == 2 && calls[1] == "end"; }, "끝내면(× · 노치) 실시간 현황도 바로 끝");
            Do(() => R0926GuideArrow.Stop());
            Check(() => calls.Count == 2, "이미 끝난 뒤 또 끝내도 다시 부르지 않는다");

            // 2) 안내 중 다른 곳으로 → begin 한 번 더
            Do(() => { calls.Clear(); Go(200, 0, "첫 장소"); Go(0, 300, "둘째 장소"); });
            Check(() => { log.Add("  호출: " + Calls()); return calls.Count == 2 && calls[0] == "begin 첫 장소 10 1200" && calls[1] == "begin 둘째 장소 10 1200"; },
                "안내 중 다른 곳으로 다시 안내하면 새로 시작 (앞의 것은 네이티브가 정리)");
            Do(() => R0926GuideArrow.Stop());
            Check(() => calls.Count == 3 && calls[2] == "end", "끝 — end 한 번");

            // 3) 도착 → arrived 한 번 · 「도착했어요」 뒤 끝나도 end 를 또 부르지 않는다
            Do(() => { calls.Clear(); Go(4, 0, "도착 장소"); });
            Wait(() => ga.EditorArrived, 3f, "10m 안에 1초 — 도착");
            Check(() => { log.Add("  도착: " + Calls()); return calls.Count == 2 && calls[0] == "begin 도착 장소 10 1200" && calls[1] == "arrived"; },
                "도착하면 잠금 화면 · 아일랜드도 「도착했어요」 (arrived)");
            Wait(() => !ga.EditorGuiding, 4f, "도착 2.5초 뒤 안내 끝");
            Check(() => { log.Add("  안내 끝: " + Calls()); return !ga.EditorGuiding && calls.Count == 2; }, "「도착했어요」 뒤 안내가 끝나도 다시 끄지 않는다 (네이티브가 4초 뒤 닫는다)");

            // 4) 20분 → end
            Do(() =>
            {
                calls.Clear();
                Go(300, 0, "오래 걸리는 곳");
                var f = typeof(R0926GuideArrow).GetField("gStart", BindingFlags.NonPublic | BindingFlags.Instance);
                f.SetValue(ga, Time.unscaledTime - 20f * 60f - 1f);   // 시작이 20분 전이었던 것처럼
            });
            Wait(() => !ga.EditorGuiding, 2f, "20분 지나 끝");
            Check(() => { log.Add("  20분: " + Calls()); return !ga.EditorGuiding && calls.Count == 2 && calls[1] == "end"; }, "20분이 지나면 실시간 현황도 끝 (배터리)");

            // 되돌리기
            Do(LgFinish);
            Check(() => R0926LiveGuide.EditorCalls == null && !ga.EditorGuiding, "되돌림 — 호출 기록 끔 · 안내 없음");
        }
    }
}
