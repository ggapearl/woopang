using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 4번 대표님 「…해당 창을 닫거나 앱이 꺼지더라도 해당 요청한 3D모델링 작업이 잃어버리지 않고 … glb 로 저장되도록 하거나
        //  프로필에서 확인이 되어서 서버에서 가지고 있어야할 것 같거든? 해당 로직 지금 API연결하는 건 아니지만 미리 잘 정리해줘」 — R0926AiJobs
        //  가짜 서버 127.0.0.1:5799 (aijobs_stub.py) 로만 — 운영 서버 · 외부 AI 에 묻지 않는다. 폰 저장소는 임시 폴더 · 계정은 시험 아이디 흉내.
        //  · 출시 상태(서버 주소 없음)에서는 폰에 아무것도 만들지 않는다
        //  · 맡기기 → 기록(사진 포함)을 먼저 적고 보냄 → 서버 번호 · 「순서 기다리는 중」
        //  · 앱 껐다 켜기(메모리 비우고 기록 다시 읽기) → 이어서 확인 → 완성 → GLB · 정지 사진 받아 둠 · 프로필 점 · 본 것은 다시 안 뜸
        //  · 첫 응답을 잃어버려도 같은 번호로 다시 → 서버는 한 번만 만든다
        //  · 외부 AI 실패 → 「실패」 · 하루 횟수 거절 → 다시 보내지 않고 「실패」 · 그만두기 → 서버에도
        //  · 다시 깔기(폰 기록 통째로 지움) → 서버 「내 작업」으로 채움 · GLB 다시 받음 · 점 없음
        //  · 지우기 → 서버에도 · 다음 복구에 되살아나지 않음 · 다른 계정에는 안 보임 · 로그인 전이면 맡기지 않음
        private const string AjBase = "http://127.0.0.1:5799";
        private static bool ajRun, ajLogged;
        private static string ajBaseBackup, ajDir;
        private static int ajChanges;

        [Serializable]
        private sealed class AjDebug { public int created, keyPosts, photos, jobs; public string[] deleted, canceled; }

        private static void AjOnChanged(R0926AiJobs.Entry e) => ajChanges++;

        private static void AjFinish()
        {
            if (!ajRun) return;
            ajRun = false;
            R0926AiJobs.Changed -= AjOnChanged;
            R0926AiJobs.Base = ajBaseBackup;
            R0926AiJobs.EditorReload();   // 일꾼 내리고 메모리 비움 — 서버 주소가 비어 다시 읽지 않는다
            R0926AiJobs.EditorDir = null;
            R0926AiJobs.EditorPollEvery = -1f;
            if (ajLogged) { ajLogged = false; SfLogin(false); }
            try { if (!string.IsNullOrEmpty(ajDir) && Directory.Exists(ajDir)) Directory.Delete(ajDir, true); } catch { }
        }

        // 가짜 서버 손잡이 — 주소는 그 단계에서 정한다 (폰 번호 등)
        private static void AjStub(string method, Func<string> path, string json, Action<string> got)
        {
            UnityWebRequest req = null;
            string p = null;
            Do(() =>
            {
                p = path();
                req = new UnityWebRequest(AjBase + p, method) { downloadHandler = new DownloadHandlerBuffer() };
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.SendWebRequest();
            });
            Wait(() => req != null && req.isDone, 5f, "가짜 서버 " + method);
            Do(() =>
            {
                string text = req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null;
                if (text == null) log.Add($"  가짜 서버 응답 없음: {method} {p} ({req.responseCode})");
                req.Dispose();
                got?.Invoke(text);
            });
        }

        private static void PlanAiJobs()
        {
            R0926AiJobs.Entry e1 = null, e2 = null, e3 = null, e4 = null, e5 = null;
            string id1 = null, id2 = null, id3 = null, id4 = null, sid1 = null, sid5 = null, resetOk = null;
            bool shipClean = false, guestNull = false, offNull = false;
            byte[] photo = null;
            AjDebug dbg = null;
            R0926AiJobs.Entry F(string id) => id == null ? null : R0926AiJobs.Find(id);
            string St(R0926AiJobs.Entry e) => e == null ? "없음"
                : e.state + (e.Working ? $" {e.progress}% · 앞 {e.position}" : "") + (string.IsNullOrEmpty(e.error) ? "" : " · " + e.error);
            void Dbg(Func<string> key) => AjStub("GET", () => "/debug?key=" + (key() ?? ""), null, t => dbg = t != null ? JsonUtility.FromJson<AjDebug>(t) : null);
            bool Has(string[] a, string v) => a != null && v != null && Array.IndexOf(a, v) >= 0;

            NmBoot(false);   // 가까이 카드는 조용히
            AjStub("POST", () => "/reset", null, t => resetOk = t);
            AjStub("POST", () => "/mode", "{\"hold\":true}", null);   // 첫 작업은 「앱을 껐다 켤」 때까지 붙잡아 둔다
            Do(() =>
            {
                ajRun = true;
                ajChanges = 0;
                shipClean = !R0926AiJobs.On && !Directory.Exists(Path.Combine(Application.persistentDataPath, "ai_jobs"));
                ajBaseBackup = R0926AiJobs.Base;
                ajDir = Path.Combine(Application.temporaryCachePath, "aijobs_test");
                if (Directory.Exists(ajDir)) Directory.Delete(ajDir, true);
                SfLogin(true);
                ajLogged = true;
                R0926AiJobs.EditorDir = ajDir;
                R0926AiJobs.EditorPollEvery = 0.2f;
                R0926AiJobs.Base = AjBase;
                R0926AiJobs.EditorReload();
                R0926AiJobs.Changed += AjOnChanged;
                var tex = new Texture2D(8, 8);
                photo = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
            });
            Check(() => shipClean, "출시 상태(서버 주소 없음)에서는 켜지지 않고 폰에 보관함도 만들지 않는다");
            Check(() => resetOk != null && R0926AiJobs.On && R0926AiJobs.All.Count == 0 && photo != null, "준비: 가짜 서버 5799 · 빈 임시 보관함 · 시험 계정");

            // 1) 맡기기 — 보내기 전에 폰에 먼저 적는다
            Do(() => { e1 = R0926AiJobs.Submit(photo, "character", "Toon", "노란 강아지 · 모자", "시험 장소"); id1 = e1?.id; });
            Check(() =>
            {
                string store = Path.Combine(ajDir, "jobs.json");
                bool rec = e1 != null && File.Exists(store) && File.ReadAllText(store).Contains(e1.id);
                string src = R0926AiJobs.SrcPath(e1);
                bool pic = src != null && File.Exists(src);
                log.Add($"  맡김: {St(e1)} · 기록 {rec} · 사진 {pic}");
                return rec && pic && e1.owner == SfMe;
            }, "맡기면 보내기 전에 폰 기록에 먼저 적는다 (사진 포함 · 맡긴 계정)");
            Wait(() => e1 != null && !string.IsNullOrEmpty(e1.serverId), 5f, "서버가 받아 번호를 줌");
            Check(() => { log.Add($"  서버가 받음: {St(e1)} · 단계 '{e1.stage}'"); return e1.state == R0926AiJobs.State.Queued && !string.IsNullOrEmpty(e1.stage); },
                "서버가 받으면 「순서 기다리는 중」 (앞 순서 · 단계 말)");

            // 2) 앱을 껐다 켬 — 메모리를 비우고 폰 기록에서 다시 읽어 이어 간다
            Do(() => { sid1 = e1.serverId; R0926AiJobs.EditorReload(); });
            Check(() => { var r = F(id1); log.Add($"  다시 켬: {St(r)}"); return r != null && r != e1 && r.serverId == sid1 && r.Working && !r.hasGlb; },
                "앱을 껐다 켜도 맡긴 작업이 기록에 남아 있다 (같은 서버 번호 · 만드는 중)");
            AjStub("POST", () => "/mode", "{\"hold\":false}", null);
            Wait(() => F(id1) != null && F(id1).hasGlb, 8f, "이어서 확인 → 완성 → GLB 받아 둠");
            Check(() =>
            {
                var r = F(id1);
                string glb = R0926AiJobs.GlbPath(r), png = R0926AiJobs.ThumbPath(r);
                byte[] b = glb != null && File.Exists(glb) ? File.ReadAllBytes(glb) : null;
                bool magic = b != null && b.Length >= 12 && Encoding.ASCII.GetString(b, 0, 4) == "glTF";
                bool same = b != null && Encoding.ASCII.GetString(b).Contains("WOOPANG-TEST " + sid1);
                bool pic = png != null && File.Exists(png);
                log.Add($"  완성: {St(r)} · GLB {(b != null ? b.Length : 0)}바이트 · 정지 사진 {pic} · 이름 '{r?.label}'");
                return r != null && r.state == R0926AiJobs.State.Done && magic && same && pic && !string.IsNullOrEmpty(r.label);
            }, "다 되면 GLB · 정지 사진을 폰에 받아 둔다 (서버가 정리해도 남게)");
            Check(() => { log.Add($"  프로필 점 {R0926AiJobs.Unseen} · 바뀜 알림 {ajChanges}번"); return R0926AiJobs.Unseen == 1 && ajChanges >= 4; },
                "새로 완성 — 프로필 줄 점 1 · 단계가 바뀔 때마다 화면에 알림 (Changed)");
            Do(() => { R0926AiJobs.MarkSeen(id1); R0926AiJobs.EditorReload(); });
            Check(() => R0926AiJobs.Unseen == 0 && F(id1) != null && F(id1).seen && F(id1).hasGlb, "본 것은 껐다 켜도 점이 다시 뜨지 않는다 · 받아 둔 GLB 그대로");

            // 3) 첫 응답을 잃어버려도 — 같은 번호로 다시 보내고 서버는 한 번만 만든다
            AjStub("POST", () => "/mode", "{\"mode\":\"drop_first\"}", null);
            Do(() => { e2 = R0926AiJobs.Submit(photo, "character", "Pencil", "모자 쓴 고양이", "시험 장소"); id2 = e2?.id; });
            Wait(() => e2 != null && !string.IsNullOrEmpty(e2.serverId), 8f, "첫 응답은 잃어버림(500) → 같은 번호로 다시 → 번호 받음");
            Dbg(() => id2);
            Check(() => { log.Add($"  같은 번호로 보낸 횟수 {dbg?.keyPosts} · 서버가 만든 작업 {dbg?.created}"); return dbg != null && dbg.keyPosts == 2 && dbg.created == 2; },
                "끊겨서 다시 보내도 서버는 한 번만 만든다 (돈이 두 번 나가지 않게)");
            AjStub("POST", () => "/mode", "{}", null);

            // 4) 외부 AI 실패 · 하루 횟수 거절
            Do(() =>
            {
                e3 = R0926AiJobs.Submit(photo, "character", "Toon", "실패하는 요청", ""); id3 = e3?.id;
                e4 = R0926AiJobs.Submit(photo, "character", "Toon", "거절되는 요청", ""); id4 = e4?.id;
            });
            Wait(() => e3 != null && e3.state == R0926AiJobs.State.Failed && e4 != null && e4.state == R0926AiJobs.State.Failed, 6f, "실패 · 거절");
            Dbg(() => id4);
            Check(() =>
            {
                log.Add($"  실패: {St(e3)} / 거절: {St(e4)} · 거절된 것 보낸 횟수 {dbg?.keyPosts}");
                return !string.IsNullOrEmpty(e3.error) && !e3.hasGlb && e4.error == "오늘은 3번까지 만들 수 있어요" && string.IsNullOrEmpty(e4.serverId) && dbg != null && dbg.keyPosts == 1;
            }, "외부 AI 가 못 만들면 「실패」(이유) · 하루 횟수로 거절되면 다시 보내지 않고 바로 「실패」");

            // 5) 그만두기 — 서버에도 알린다
            Do(() => e5 = R0926AiJobs.Submit(photo, "character", "Toon", "그만둘 요청", ""));
            Wait(() => e5 != null && !string.IsNullOrEmpty(e5.serverId), 5f, "그만둘 작업 맡김");
            Do(() => { sid5 = e5.serverId; R0926AiJobs.Cancel(e5.id); });
            Check(() => e5.state == R0926AiJobs.State.Canceled && F(e5.id) != null, "그만두면 바로 「그만둠」");
            Wait(() => !e5.cancelPending, 3f, "서버에 그만두기 알림");
            Dbg(() => "");
            Check(() => { log.Add($"  그만둠: {St(e5)} · 서버가 접은 작업 {(dbg?.canceled != null ? dbg.canceled.Length : 0)}개"); return dbg != null && Has(dbg.canceled, sid5) && e5.state == R0926AiJobs.State.Canceled; },
                "그만두기는 서버에도 알린다 (시작 전이면 비용 없음)");

            // 6) 앱을 지웠다 깔음 · 폰을 바꿈 — 폰 기록을 통째로 지우고 켬 → 로그인한 계정의 서버 「내 작업」으로 채운다
            Wait(() => F(id2) != null && F(id2).hasGlb, 8f, "둘째 작업도 완성 · 받아 둠");
            Do(() =>
            {
                R0926AiJobs.Base = null;
                R0926AiJobs.EditorReload();          // 일꾼 내림
                Directory.Delete(ajDir, true);       // 앱을 지움 — 폰 기록 · 받아 둔 GLB 모두
                R0926AiJobs.Base = AjBase;
                R0926AiJobs.EditorReload();          // 다시 깔고 켬 (로그인한 상태)
            });
            Wait(() => F(id1) != null && F(id1).hasGlb && F(id2) != null && F(id2).hasGlb && F(id3) != null, 8f, "서버 목록으로 채우고 GLB 다시 받음");
            Check(() =>
            {
                var all = R0926AiJobs.All;
                var r1 = F(id1);
                var r3 = F(id3);
                log.Add($"  복구 {all.Count}개: " + string.Join(" / ", all.ConvertAll(x => St(x))));
                return r1 != null && r1.serverId == sid1 && r1.seen && R0926AiJobs.Unseen == 0 && r3 != null && r3.state == R0926AiJobs.State.Failed
                    && F(id4) == null && File.Exists(R0926AiJobs.GlbPath(r1)) && all.Count == 4;
            }, "다시 깔아도 서버 「내 작업」으로 채운다 (같은 번호 · GLB 다시 받음 · 예전 것은 점 없음 · 거절된 것은 서버에 없어 빠짐)");

            // 7) 지우기 — 폰 파일 · 서버 모두 · 다음 복구에 되살아나지 않는다
            Do(() => R0926AiJobs.Remove(id1));
            Check(() => F(id1) == null && !File.Exists(Path.Combine(ajDir, id1 + ".glb")) && R0926AiJobs.All.Count == 3, "지우면 목록 · 폰 파일에서 바로 빠진다");
            Sleep(1f);
            Dbg(() => "");
            Check(() => { log.Add($"  서버에서 지운 작업 {(dbg?.deleted != null ? dbg.deleted.Length : 0)}개"); return dbg != null && Has(dbg.deleted, sid1); }, "서버에도 지우기를 알린다");
            Do(() => R0926AiJobs.EditorReload());
            Sleep(1f);
            Check(() => F(id1) == null && R0926AiJobs.All.Count == 3, "다시 켜서 서버와 맞춰도 지운 것은 되살아나지 않는다");

            // 8) 다른 계정 · 로그인 전 · 꺼짐
            Do(() => { SfLogin(false); SfLogin(true, "aijobs_test_other"); });
            Check(() => R0926AiJobs.All.Count == 0 && F(id2) == null, "다른 계정으로 로그인하면 앞 계정의 작업은 보이지 않는다");
            Do(() => { SfLogin(false); SfLogin(true); });
            Check(() => R0926AiJobs.All.Count == 3 && F(id2) != null, "원래 계정으로 돌아오면 다시 보인다");
            Do(() =>
            {
                var lm = LoginManager.Instance;
                var user = typeof(LoginManager).GetProperty("CurrentUser");
                var saved = lm.CurrentUser;
                user.SetValue(lm, null);
                guestNull = R0926AiJobs.Submit(photo, "character", "Toon", "로그인 전", "") == null;
                user.SetValue(lm, saved);
                R0926AiJobs.Base = null;
                offNull = R0926AiJobs.Submit(photo, "character", "Toon", "꺼짐", "") == null;
                R0926AiJobs.Base = AjBase;
            });
            Check(() => guestNull && offNull && R0926AiJobs.All.Count == 3, "로그인 전 · 서버 주소가 빈 출시 상태에서는 맡기지 않는다 (기록도 안 남음)");

            // 되돌리기
            Do(AjFinish);
            Check(() => !ajRun && !R0926AiJobs.On && R0926AiJobs.EditorDir == null && !Directory.Exists(ajDir), "되돌림 — 서버 주소 비움 · 임시 보관함 지움 · 로그인 흉내 되돌림");
        }
    }
}
