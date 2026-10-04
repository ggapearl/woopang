using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace Redesign0926
{
    /// <summary>
    /// 시작화면이 도는 동안 프레임마다 걸린 시간을 재고, 무거운 프레임에서 시간을 가장 많이 쓴 작업을 적는다.
    /// (에디터 측정이라 기기와 절대값은 다르지만 '무엇이 튀는지'는 같다)
    /// </summary>
    public static class Redesign0926SplashProf
    {
        private struct Spike { public int frame; public float ms, game; public string detail; }

        private static int processed = -1;
        private static readonly List<float> frameMs = new List<float>();
        private static readonly List<float> gameMs = new List<float>();
        private static readonly List<Spike> spikes = new List<Spike>();

        public static void StartRecording()
        {
            processed = -1;
            frameMs.Clear(); gameMs.Clear(); spikes.Clear();
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.enabled = true;
        }

        /// <summary>새로 쌓인 프레임을 읽는다 (프로파일러는 최근 몇백 프레임만 들고 있어서 도중에 자주 부른다)</summary>
        public static void Analyze()
        {
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex - 1;   // 마지막은 아직 쓰는 중일 수 있다
            if (first < 0 || last < first) return;
            for (int i = System.Math.Max(first, processed + 1); i <= last; i++)
            {
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(i, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                           HierarchyFrameDataView.columnTotalTime, false))
                {
                    processed = i;
                    if (v == null || !v.valid) continue;
                    float ms = v.frameTimeMs;
                    float game = Child(v, v.GetRootItemID(), "PlayerLoop");
                    frameMs.Add(ms); gameMs.Add(game);
                    if (game > 30f)
                    {
                        var sb = new StringBuilder();
                        Walk(v, v.GetRootItemID(), 0, sb);
                        spikes.Add(new Spike { frame = i, ms = ms, game = game, detail = sb.ToString() });
                    }
                }
            }
        }

        public static void Stop() => ProfilerDriver.enabled = false;

        public static void Report(List<string> log)
        {
            if (gameMs.Count == 0) { log.Add("  프로파일: 프레임 없음"); return; }
            var sorted = gameMs.OrderBy(x => x).ToList();
            float P(float q) => sorted[System.Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];
            log.Add($"  프레임 {gameMs.Count} · 게임 처리 중앙 {P(0.5f):0.0}ms · 90% {P(0.9f):0.0}ms · 최대 {sorted.Last():0.0}ms · 30ms 넘음 {gameMs.Count(x => x > 30f)} · 50ms 넘음 {gameMs.Count(x => x > 50f)}");
            foreach (var s in spikes.OrderByDescending(x => x.game).Take(8))
            {
                log.Add($"  ▼ 프레임 {s.frame}: 게임 {s.game:0.0}ms (전체 {s.ms:0.0}ms)");
                foreach (var line in s.detail.Split('\n').Where(l => l.Length > 0).Take(14)) log.Add("     " + line);
            }
        }

        private static float Child(HierarchyFrameDataView v, int id, string name)
        {
            var ch = new List<int>();
            v.GetItemChildren(id, ch);
            foreach (var c in ch) if (v.GetItemName(c) == name) return v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
            return 0f;
        }

        // 시간이 큰 쪽으로 내려가며 3ms 넘는 것만 적는다
        private static void Walk(HierarchyFrameDataView v, int id, int depth, StringBuilder sb)
        {
            if (depth > 9) return;
            var ch = new List<int>();
            v.GetItemChildren(id, ch);
            foreach (var c in ch.OrderByDescending(c => v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime)))
            {
                float total = v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
                if (total < 3f) break;
                string name = v.GetItemName(c);
                if (depth == 0 && name != "PlayerLoop") continue;   // 에디터 자체 시간은 빼고 게임 쪽만
                float self = v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime);
                sb.Append(new string(' ', depth * 2)).Append(name).Append(' ').Append(total.ToString("0.0")).Append("ms");
                if (self > 2f) sb.Append(" (자체 ").Append(self.ToString("0.0")).Append(')');
                sb.Append('\n');
                Walk(v, c, depth + 1, sb);
            }
        }
    }
}
