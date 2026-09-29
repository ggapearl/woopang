using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Redesign0926
{
    /// <summary>
    /// 플레이 모드 캡처 (에디터 전용). 시작화면처럼 실행 중에만 드러나는 겹침·그리기 순서를 확인할 때 쓴다.
    ///
    /// .redesign0926_playshot 파일: 1줄 저장 폴더, 2줄 찍을 시각(초, 쉼표로). 파일이 생기면 플레이를 켜고
    /// 시각마다 게임 화면을 찍은 뒤 플레이를 끈다. 결과는 .redesign0926_playshot_result.
    /// </summary>
    [InitializeOnLoad]
    public static class Redesign0926PlayShot
    {
        private static string Root => Directory.GetCurrentDirectory();
        private static string PlanPath => Path.Combine(Root, ".redesign0926_playshot");
        private static string ResultPath => Path.Combine(Root, ".redesign0926_playshot_result");

        private static double startedAt = -1;
        private static double nextPoll;
        private static readonly List<float> pending = new List<float>();
        private static readonly List<string> log = new List<string>();
        private static string dir;

        static Redesign0926PlayShot()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
        }

        private static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !File.Exists(PlanPath)) return;
            var lines = File.ReadAllLines(PlanPath);
            dir = lines[0].Trim();
            Directory.CreateDirectory(dir);
            pending.Clear();
            foreach (var p in lines[1].Split(','))
                if (float.TryParse(p.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float t))
                    pending.Add(t);
            pending.Sort();
            log.Clear();
            log.Add("play " + DateTime.Now.ToString("HH:mm:ss"));
            startedAt = EditorApplication.timeSinceStartup;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + 3.0;
                if (File.Exists(PlanPath) && !EditorApplication.isCompiling && !EditorApplication.isUpdating
                    && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;
                return;
            }
            if (startedAt < 0) return;

            double elapsed = EditorApplication.timeSinceStartup - startedAt;
            while (pending.Count > 0 && elapsed >= pending[0])
            {
                string path = Path.Combine(dir, $"play_{pending[0]:0.0}s.png");
                ScreenCapture.CaptureScreenshot(path);
                log.Add($"{pending[0]:0.0}s → {path} (frame {Time.frameCount}, 실제 {elapsed:0.00}s)");
                pending.RemoveAt(0);
                if (pending.Count == 0) startedAt = EditorApplication.timeSinceStartup - 1000;   // 마지막 컷 저장될 시간을 둔다
            }
            if (pending.Count == 0 && elapsed > 1001.0)
            {
                startedAt = -1;
                try { File.Delete(PlanPath); } catch (Exception e) { log.Add("plan 삭제 실패 " + e.Message); }
                File.WriteAllLines(ResultPath, log);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
