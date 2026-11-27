using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-27 대표님 버그 리포트 「오브젝트 더블터치 후 첫장사진 발생하지 않은 거」 — 정보 창(DoubleTap3D)에서 중간에 멈춘 코루틴이 남긴 표시
        //  ① 넘기는 중에 오브젝트가 꺼졌다 켜짐 → 넘기기 표시가 풀리고 보던 사진이 뜬다 · 다음 장으로도 넘어간다
        //  ② 사진을 받는 중에 꺼졌다 켜짐 → 다시 받아 첫 사진이 뜬다 · 닫기 버튼이 먹는다 (시험 사진 서버 5798 · 받기 1.2초 늦춤)
        //  ③ 두 번 누른 뒤 2초 안에 꺼졌다 켜짐 → 닫고 다시 두 번 누르면 열린다
        //  ④ 닫히는 중에 꺼짐 → 반쯤 투명한 창이 남지 않는다 · 다시 열면 제대로 뜬다
        //  ⑤ 끌던 손가락을 시스템이 가져감(Canceled) → 끌기 표시가 풀린다
        //  대리 정보 창(R0926PlaceInfo.Host)으로 한다 — AR 장소는 건드리지 않는다. 끝나면 사진 · 채운 값 · 사진 캐시 설정 · 손가락 장치를 되돌린다
        private static void PlanPhotoStuck()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            DoubleTap3D host = null;
            bool opened = false, slidingAtCut = false, loadingAtCut = false, cooldownAtCut = false, fadingAtCut = false, draggingBefore = false;
            string baseWas = null;
            float delayWas = 0f;
            bool cacheSet = false;
            var made = new List<Sprite>();
            FieldInfo F(string n) => typeof(DoubleTap3D).GetField(n, NP);
            FieldInfo fSliding = F("isSliding"), fDragging = F("isDragging"), fCooldown = F("isCooldown"), fFading = F("isFadingOut"), fLoading = F("photosLoading"), fIndex = F("imageIndex");
            bool B(FieldInfo f) => host != null && f?.GetValue(host) is bool b && b;
            int Index() => host != null && fIndex?.GetValue(host) is int i ? i : -99;
            bool Open() => host != null && host.fullscreenCanvasGroup != null && host.fullscreenCanvasGroup.gameObject.activeSelf && DoubleTap3D.Shown == host;
            bool Closed() => host == null || host.fullscreenCanvasGroup == null || !host.fullscreenCanvasGroup.gameObject.activeSelf;
            string Big() { var img = host != null ? host.fullscreenImage : null; return img == null || !img.enabled ? "꺼짐" : img.sprite != null ? (img.sprite.name.Length > 0 ? img.sprite.name : "받은 사진") : "빈 칸"; }
            Sprite Make(Color c, string label)
            {
                var tex = new Texture2D(96, 128, TextureFormat.RGBA32, false);
                var px = new Color[96 * 128];
                for (int i = 0; i < px.Length; i++) px[i] = c;
                tex.SetPixels(px);
                tex.Apply();
                var s = Sprite.Create(tex, new Rect(0, 0, 96, 128), new Vector2(0.5f, 0.5f));
                s.name = label;
                made.Add(s);
                return s;
            }
            void Place(int id, string name) => R0926PlaceInfo.Fill(host, new PlaceData { id = id, name = name, username = "tester", category = "shop", model_type = "cube" });
            void Toggle() { host.gameObject.SetActive(false); host.gameObject.SetActive(true); }

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                DoubleTap3D.CloseShown();
                host = R0926PlaceInfo.Host();
            });
            Wait(() => host != null && host.IsReady && DoubleTap3D.Shown == null, 10f, "대리 정보 창 준비");

            // ① 넘기는 중에 꺼졌다 켜짐
            Do(() =>
            {
                Place(990501, "멈춤 시험 1");
                host.imageSprites = new List<Sprite> { Make(new Color(0.95f, 0.55f, 0.2f), "s1a"), Make(new Color(0.2f, 0.62f, 0.7f), "s1b"), Make(new Color(0.55f, 0.75f, 0.3f), "s1c") };
                opened = host.OpenInfo();
            });
            Wait(() => Open(), 5f, "정보 창 열림");
            Sleep(0.7f);
            Do(() =>
            {
                host.ShowNextImage();            // 첫 장 → 둘째로 넘기기 시작
                slidingAtCut = B(fSliding);
                Toggle();                        // 넘기는 중에 꺼짐 — 넘기던 코루틴이 멈춘다
            });
            Sleep(0.4f);
            Check(() =>
            {
                log.Add($"  넘기는 중 {(slidingAtCut ? "예" : "아니오")} → 꺼졌다 켜진 뒤: 넘기는 중 표시 {(B(fSliding) ? "남음" : "풀림")} · 창 {(Open() ? "열림" : "닫힘")} · 번호 {Index()} · 큰 사진 {Big()}");
                return opened && slidingAtCut && !B(fSliding) && Open() && Index() == 1 && Big() == "s1b";
            }, "넘기는 중에 오브젝트가 꺼졌다 켜져도 넘기기 표시가 풀리고 보던 사진(둘째)이 뜬다");
            Do(() => host.ShowNextImage());
            Sleep(0.9f);
            Check(() =>
            {
                log.Add($"  다음으로: 번호 {Index()} · 큰 사진 {Big()}");
                return Index() == 2 && Big() == "s1c";
            }, "그다음 장으로도 넘어간다 (예전엔 넘기기 표시가 남아 막혔다)");
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => Closed(), 5f, "정보 창 닫힘");

            // ② 사진을 받는 중에 꺼졌다 켜짐 — 시험 사진 서버(5798)에서 1.2초 늦게 받는다. 번호는 매번 새로(디스크에 받아 둔 것이 없게)
            Do(() =>
            {
                baseWas = SubPhotoCache.EditorBase;
                delayWas = SubPhotoCache.SimulatedNetDelay;
                cacheSet = true;
                SubPhotoCache.EditorBase = "http://127.0.0.1:5798";
                SubPhotoCache.SimulatedNetDelay = 1.2f;
                long n = System.DateTime.Now.Ticks % 100000000L;
                host.imageSprites = new List<Sprite>();
                Place(990502, "멈춤 시험 2");
                host.SetImageUrls(new List<string> { $"uploads/sub/t21b_{n}1.jpg", $"uploads/sub/t21b_{n}2.jpg" });
                opened = host.OpenInfo();
            });
            Wait(() => Open(), 5f, "정보 창 열림 (사진 받는 중)");
            Sleep(0.3f);
            Do(() =>
            {
                loadingAtCut = B(fLoading) && Big() == "꺼짐";
                Toggle();                        // 받는 중에 꺼짐 — 받던 사진을 놓는다
            });
            Wait(() => Big() != "꺼짐", 15f, "첫 사진 뜸");
            Sleep(0.5f);
            Shot("photostuck_resume");
            Check(() =>
            {
                log.Add($"  받는 중 {(loadingAtCut ? "예(빈 칸)" : "아니오")} → 꺼졌다 켜진 뒤: 창 {(Open() ? "열림" : "닫힘")} · 번호 {Index()} · 큰 사진 {Big()}");
                return opened && loadingAtCut && Open() && Index() == 0 && Big() == "받은 사진";
            }, "사진을 받는 중에 꺼졌다 켜져도 다시 받아 첫 사진이 뜬다 (예전엔 빈 칸으로 남았다)");
            Do(() => { if (host.closeButton != null) host.closeButton.onClick.Invoke(); });
            Wait(() => Closed(), 5f, "닫기 버튼으로 닫힘");
            Check(() =>
            {
                log.Add($"  닫기 버튼: 창 {(Closed() ? "닫힘" : "그대로")}");
                return Closed();
            }, "꺼졌다 켜진 뒤에도 닫기 버튼이 먹는다 (예전엔 꺼질 때 뗀 연결을 다시 안 이었다)");

            // ③ 두 번 누른 뒤 2초 안에 꺼졌다 켜짐
            Do(() =>
            {
                SubPhotoCache.EditorBase = baseWas;
                SubPhotoCache.SimulatedNetDelay = delayWas;
                cacheSet = false;
                Place(990503, "멈춤 시험 3");
                host.imageSprites = new List<Sprite> { Make(new Color(0.8f, 0.3f, 0.5f), "s3a"), Make(new Color(0.3f, 0.4f, 0.85f), "s3b") };
                host.DoubleTapFromModal();       // 두 번 누른 것과 같은 길 — 2초 쿨다운 시작
            });
            Wait(() => Open(), 5f, "두 번 누르기로 열림");
            Do(() =>
            {
                cooldownAtCut = B(fCooldown);
                Toggle();                        // 쿨다운 안에 꺼짐 — 쿨다운을 풀던 코루틴이 멈춘다
            });
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => Closed(), 5f, "정보 창 닫힘");
            Sleep(2.3f);                         // 쿨다운(2초)이 원래대로 지났을 때
            Do(() => host.DoubleTapFromModal());
            Wait(() => Open(), 3f, "다시 두 번 누르기로 열림");
            Check(() =>
            {
                log.Add($"  쿨다운 중 {(cooldownAtCut ? "예" : "아니오")} → 꺼졌다 켜진 뒤 2.3초: 쿨다운 {(B(fCooldown) ? "남음" : "풀림")} · 다시 두 번 누르기 {(Open() ? "열림" : "안 열림")}");
                return cooldownAtCut && Open();
            }, "두 번 누른 뒤 2초 안에 꺼졌다 켜져도 다시 두 번 누르면 열린다 (예전엔 쿨다운이 안 풀려 그 장소가 다시 안 열렸다)");
            Sleep(0.6f);

            // ④ 닫히는 중에 꺼짐
            Do(() =>
            {
                DoubleTap3D.CloseShown();        // 닫기 시작 (페이드아웃)
                fadingAtCut = B(fFading);
                host.gameObject.SetActive(false);
            });
            Sleep(0.4f);
            Check(() =>
            {
                float a = host.fullscreenCanvasGroup != null ? host.fullscreenCanvasGroup.alpha : -1f;
                log.Add($"  닫히는 중 {(fadingAtCut ? "예" : "아니오")} → 꺼진 뒤: 창 {(Closed() ? "닫힘" : "남음 (투명도 " + a.ToString("0.00") + ")")} · 보이는 창 {(DoubleTap3D.Shown == null ? "없음" : "있음")}");
                return fadingAtCut && Closed() && DoubleTap3D.Shown == null && !B(fFading);
            }, "닫히는 중에 오브젝트가 꺼져도 반쯤 투명한 창이 남지 않는다");
            Do(() => { host.gameObject.SetActive(true); opened = host.OpenInfo(); });
            Wait(() => Open(), 5f, "다시 열림");
            Sleep(0.8f);
            Check(() =>
            {
                float a = host.fullscreenCanvasGroup != null ? host.fullscreenCanvasGroup.alpha : -1f;
                log.Add($"  다시 열기: 창 {(Open() ? "열림" : "닫힘")} · 투명도 {a:0.00} · 큰 사진 {Big()}");
                return opened && Open() && a > 0.99f && Big() == "s3a";
            }, "그 뒤 다시 열면 제대로 뜬다");

            // ⑤ 끌던 손가락을 시스템이 가져감 (전화 · 제어 센터 · 홈 막대)
            Do(() =>
            {
                ptFocusWas = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Sleep(0.2f);
            Vector2 fp = default;
            int fr = -1, mv = 0;
            steps.Enqueue(() =>
            {
                if (ptTouch == null) return true;
                fp = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
                InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Began, position = fp });
                fr = Time.frameCount;
                mv = 0;
                return true;
            });
            steps.Enqueue(() =>
            {
                if (ptTouch == null || mv >= 8) return true;
                if (Time.frameCount == fr) return false;   // 한 프레임에 한 걸음
                fr = Time.frameCount;
                fp += new Vector2(-28f, 0f);
                InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = fp });
                mv++;
                return false;
            });
            steps.Enqueue(() => ptTouch == null || Time.frameCount > fr + 1);
            Do(() =>
            {
                draggingBefore = B(fDragging);
                if (ptTouch != null) InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Canceled, position = fp });
            });
            Sleep(0.6f);
            Check(() =>
            {
                log.Add($"  옆으로 끌기 {(draggingBefore ? "중" : "안 됨")} → 손가락 취소 뒤: 끌기 표시 {(B(fDragging) ? "남음" : "풀림")} · 큰 사진 {Big()}");
                return draggingBefore && !B(fDragging) && Big() == "s3a";
            }, "끌던 손가락을 시스템이 가져가도(취소) 끌기 표시가 풀리고 사진은 제자리");

            // 되돌리기
            Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
                if (cacheSet) { SubPhotoCache.EditorBase = baseWas; SubPhotoCache.SimulatedNetDelay = delayWas; cacheSet = false; }
                DoubleTap3D.CloseShown();
            });
            Wait(() => Closed(), 5f, "정보 창 닫힘");
            Do(() =>
            {
                if (host != null)
                {
                    if (!host.gameObject.activeSelf) host.gameObject.SetActive(true);
                    host.imageSprites = new List<Sprite>();
                    host.ResetData();
                    DataManager.Instance?.ConfigureInfo(host, null);
                }
                foreach (var s in made)
                {
                    if (s == null) continue;
                    if (s.texture != null) Object.Destroy(s.texture);
                    Object.Destroy(s);
                }
                made.Clear();
            });
            Check(() => (host == null || host.imageSprites.Count == 0) && ptTouch == null && SubPhotoCache.EditorBase == baseWas, "되돌림 — 넣은 사진 · 손가락 장치 · 사진 캐시 설정 처음대로");
        }
    }
}
