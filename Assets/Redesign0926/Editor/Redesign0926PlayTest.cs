using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 플레이 모드 동작 시험 (에디터 전용). .redesign0926_playtest 파일(1줄: 캡처 폴더)이 생기면 플레이를 켜고
    /// 가상 마우스·뒤로가기(Esc)로 창을 열고 닫아 본 뒤 결과를 .redesign0926_playtest_result 에 적고 플레이를 끈다.
    ///  · 목록: 크게 끌어내리면 닫힘 / 조금 끌면 제자리 / 뒤로가기로 닫힘
    ///  · 추가: 크게 끌어내리면 닫힘 (v19)
    ///  · 프로필(또는 로그인 안내): 뒤로가기로 닫힘 · 열린 동안 도크 칸은 X · 크게 끌어내리면 닫힘 (v19)
    ///  · 메시지(또는 로그인 안내): 크게 끌어내리면 닫힘
    /// </summary>
    [InitializeOnLoad]
    public static class Redesign0926PlayTest
    {
        private static string Root => Directory.GetCurrentDirectory();
        private static string PlanPath => Path.Combine(Root, ".redesign0926_playtest");
        private static string ResultPath => Path.Combine(Root, ".redesign0926_playtest_result");

        private static readonly List<string> log = new List<string>();
        private static readonly Queue<Func<bool>> steps = new Queue<Func<bool>>();   // true 를 돌려주면 다음 단계로
        private static double nextPoll, waitUntil = -1;
        private static bool running;
        private static string dir;
        private static InputSettings.EditorInputBehaviorInPlayMode savedBehavior;
        private static InputSettings.BackgroundBehavior savedBackground;
        private static bool savedRunInBackground;

        static Redesign0926PlayTest()
        {
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(PlanPath)) Start(); };
            EditorApplication.update += Tick;
        }

        private static void Start()
        {
            dir = File.ReadAllLines(PlanPath)[0].Trim();
            Directory.CreateDirectory(dir);
            log.Clear();
            log.Add("playtest " + DateTime.Now.ToString("HH:mm:ss"));
            savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            savedRunInBackground = Application.runInBackground;
            Application.runInBackground = true;   // 대표님이 다른 창을 쓰고 있어도 시험이 멈추지 않게 (창을 빼앗지 않는다)
            steps.Clear();
            var lines = File.ReadAllLines(PlanPath);
            string mode = lines.Length > 1 ? lines[1].Trim() : "";
            // 스토어 캡처는 날씨를 광화문 좌표로 넣는다 — 판의 지역 이름도 그 자리로 (에디터 위치의 지역이 찍혔다)
            R0926SkyWeather.EditorRegionOverride = mode == "store" || mode == "store2" || mode == "store3" || mode == "sky" || mode == "ar" ? StoreRegion : null;
            if (mode == "splashprof") PlanSplashProf();
            else if (mode == "chatnav") PlanChatNav();
            else if (mode == "profile")
            {
                Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
                Do(() => { if (Active("LoginPromptPanel")) BackNow(); ProfileManager.Instance?.ShowProfile("3"); });
                Sleep(3.5f);
                Shot("profile");
            }
            else if (mode == "skyfast") PlanSkyFast();
            else if (mode == "inputsync") PlanInputSync();
            else if (mode == "store3") { storeSrc = lines.Length > 2 ? lines[2].Trim() : ""; PlanStore3(); }
            else if (mode == "sky") { storeSrc = lines.Length > 2 ? lines[2].Trim() : ""; PlanSky(); }
            else if (mode == "ar") { storeSrc = lines.Length > 2 ? lines[2].Trim() : ""; PlanAr(); }
            else if (mode == "update") PlanUpdate();
            else if (mode == "audit") PlanAudit();
            else if (mode == "hunt") PlanHunt();
            else if (mode == "huntprefs") PlanHuntPrefs();
            else if (mode == "huntpush") PlanHuntPush();
            else if (mode == "huntprofile") PlanHuntProfile();
            else if (mode == "hunttreasure") PlanHuntTreasure();
            else if (mode == "login") PlanLogin();
            else if (mode == "chatlook") PlanChatLook();
            else if (mode == "v24") PlanV24();
            else if (mode == "maptiles") PlanMapTiles();
            else if (mode == "store2")
            {
                // 스토어 캡처 2 — 원래 자리(3D 오브젝트가 있는 곳)에서 시작화면 · 8방향 · 하늘 날씨
                storeSrc = lines.Length > 2 ? lines[2].Trim() : "";
                PlanStore2();
            }
            else if (mode == "store")
            {
                // 스토어 캡처 — 광화문 근처로 옮기고, 검은 카메라 화면 대신 흐린 배경을 3D 오브젝트 뒤에 깐다
                storeSrc = lines.Length > 2 ? lines[2].Trim() : "";
                VirtualLocation.Instance.SetCoordinates(37.5759f, 126.9768f);
                PlanStore();
            }
            else if (mode == "upload") PlanUpload(); else if (mode == "look") PlanLook(); else if (mode == "look2") PlanLook2(); else if (mode == "look3") PlanLook3(); else Plan();
            running = true;
        }

        // ── 시험 순서 ────────────────────────────────────────────
        private static void Plan()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.5f);

            // 목록 — 크게 끌어내리기
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Shot("list_open");
            Drag("ListPanel/Sheet0926", 0.45f, "list_drag");
            Sleep(1.0f);
            Check(() => !Active("ListPanel"), "목록: 크게 끌어내리면 닫힘");
            Shot("list_closed");

            // 목록 — 조금만 끌면 제자리
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => Remember("ListPanel/Sheet0926"));
            Drag("ListPanel/Sheet0926", 0.06f, null);
            Sleep(1.0f);
            Check(() => Active("ListPanel") && SamePlace("ListPanel/Sheet0926"), "목록: 조금 끌면 제자리로 돌아옴");

            // 목록 — 뒤로가기
            Back();
            Sleep(0.6f);
            Check(() => !Active("ListPanel"), "목록: 뒤로가기로 닫힘");

            // 목록 — 도크 X: 바로 꺼지지 않고 내려간 뒤 닫힘
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(0.05f);
            Shot("list_x_closing");
            Check(() => Active("ListPanel"), "목록: X 누른 직후엔 아직 보임(내려가는 중)");
            Sleep(0.8f);
            Check(() => !Active("ListPanel"), "목록: X 누르면 내려간 뒤 닫힘");

            // 추가 — 크게 끌어내리기 (v19: 도크 X 말고도 밀어서 닫힌다)
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Drag("UploadPage/UploadSheet0926", 0.45f, "upload_drag");
            Sleep(1.0f);
            Check(() => !Active("UploadPage") && Active("Dock0926/PlusButton"), "추가: 크게 끌어내리면 닫힘 (도크 + 다시 보임)");

            // 프로필 (로그인 안 돼 있으면 로그인 안내)
            Do(() => Click("Dock0926/MiniProfile"));
            Sleep(1.0f);
            Shot("profile_open");
            Do(() => log.Add("  열린 창: " + OpenPanels()));
            Back();
            Sleep(0.6f);
            Check(() => OpenPanels() == "-", "프로필/로그인 안내: 뒤로가기로 닫힘");

            // 프로필 — 열린 동안 도크 칸은 X '닫기' · 카드를 크게 끌어내리면 닫힘 (v19, 로그인돼 있을 때만)
            Do(() => Click("Dock0926/MiniProfile"));
            Sleep(1.0f);
            Check(() =>
            {
                if (!Active("FullProfilePanel")) return true;   // 로그인 안내였다
                var t = Find("Dock0926/MiniProfile");
                var cg = t != null ? t.GetComponent<CanvasGroup>() : null;
                return cg != null && cg.alpha < 0.01f && Active("FullProfilePanel/DockMirror0926/ProfileToggle0926/Icon0926");
            }, "프로필: 열린 동안 도크 사진 칸 대신 X '닫기'");
            Drag("FullProfilePanel/Content", 0.45f, "profile_drag", () => Active("FullProfilePanel"));
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Sleep(1.0f);
            Check(() => OpenPanels() == "-", "프로필(또는 로그인 안내): 크게 끌어내리면 닫힘");

            // 메시지 (로그인 안 돼 있으면 로그인 안내)
            Do(() => Click("Dock0926/Message_Button"));
            Sleep(1.2f);
            Do(() => log.Add("  열린 창: " + OpenPanels()));
            Drag("MessagePanel/Background", 0.45f, "msg_drag", () => Active("MessagePanel"));
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Sleep(1.2f);
            Check(() => OpenPanels() == "-", "메시지(또는 로그인 안내): 끌어내리기/뒤로가기로 닫힘");

            // 아무 창도 없을 때 뒤로가기 — 에디터에선 앱을 뒤로 보내지 못하니 창이 새로 열리지 않는지만
            Back();
            Sleep(0.6f);
            Check(() => OpenPanels() == "-", "메인 뒤로가기: 창이 열리지 않음 (기기에선 moveTaskToBack)");
            Shot("main_after_back");
        }

        // 추가 화면 카드 배치 확인 — 카드 크기·위치와 SwipePanelController 값
        private static void PlanUpload()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Shot("upload_open");
            Do(() =>
            {
                foreach (var n in new[] { "UploadPage/UploadSheet0926", "UploadPage/UploadSheet0926/CubeUploadPage", "UploadPage/UploadSheet0926/ModelUploadPage" })
                {
                    var rt = Find(n) as RectTransform;
                    if (rt == null) { log.Add("  없음 " + n); continue; }
                    log.Add($"  {n}: w={rt.rect.width:0} pos={rt.anchoredPosition} size={rt.sizeDelta} aMin={rt.anchorMin} aMax={rt.anchorMax} active={rt.gameObject.activeInHierarchy}");
                }
                var sp = UnityEngine.Object.FindAnyObjectByType<SwipePanelController>();
                if (sp != null)
                    foreach (var f in new[] { "panelWidth", "panelDistance", "currentAnchoredX", "currentPanel" })
                    {
                        var fi = typeof(SwipePanelController).GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        log.Add($"  swipe.{f} = {fi?.GetValue(sp)}");
                    }
                log.Add($"  screen={Screen.width}x{Screen.height} canvasScale={CanvasRoot()?.GetComponent<Canvas>()?.scaleFactor}");
            });
        }

        // 0930 시안 둘러보기 — 창마다 열어 찍는다 (칩 상태·탭 넘김·페이드·안내·상태 알약)
        private static void PlanLook()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(0.3f);
            Shot("main_status");
            Sleep(2.0f);
            Shot("main");

            Do(() => Click("Dock0926/List_Button"));
            Sleep(0.12f);
            Shot("list_rising");
            Sleep(1.0f);
            Shot("list");
            Do(() => ClickToggle("PetFriendlyToggle"));
            Sleep(0.5f);
            Shot("list_pet_required");
            Do(() => ClickToggle("PetFriendlyToggle"));
            Sleep(0.4f);
            Shot("list_pet_excluded");
            Do(() => ClickToggle("PetFriendlyToggle"));
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.4f);
            Shot("list_category_shop");
            for (int i = 0; i < 7; i++) Do(() => ClickToggle("CategoryToggle"));   // 한 바퀴 돌아 '전체'로
            Do(() => Modes()?.Show(1));
            Sleep(0.15f);
            Shot("tab_sliding");
            Sleep(0.8f);
            Shot("map");
            Do(() => Modes()?.Show(2));
            Sleep(0.9f);
            Shot("settings");
            Do(() => Modes()?.Show(0));
            Sleep(0.8f);
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(0.12f);
            Shot("list_closing");
            Sleep(1.0f);

            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Shot("add");
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.0f);

            Do(() => Click("Dock0926/Message_Button"));
            Sleep(1.2f);
            Shot("msg");
            Back();
            Sleep(1.0f);

            Do(() => Click("Dock0926/MiniProfile"));
            Sleep(0.1f);
            Shot("profile_fading");
            Sleep(0.8f);
            Shot("profile");
            Do(() => log.Add("  열린 창: " + OpenPanels()));
            Do(() => Click("FullProfilePanel/DockMirror0926/ProfileToggle0926"));
            Sleep(0.1f);
            Shot("profile_fading_out");
            Sleep(0.6f);
            Check(() => !Active("FullProfilePanel"), "프로필: 도크 프로필 칸을 다시 누르면 닫힘");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Sleep(0.6f);

            Do(() =>
            {
                var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include);
                if (cm != null && cm.commentPanel != null) cm.commentPanel.SetActive(true);
            });
            Sleep(1.2f);
            Shot("comment");
            Do(() =>
            {
                var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include);
                if (cm != null && cm.commentInputField != null) cm.commentInputField.text = "여기 연못 예뻐요";
            });
            Sleep(0.5f);
            Shot("comment_typed");
            Do(() =>
            {
                var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include);
                if (cm != null) { if (cm.commentInputField != null) cm.commentInputField.text = ""; cm.commentPanel.SetActive(false); }
            });
            Sleep(0.5f);

            Do(() =>
            {
                var lm = UnityEngine.Object.FindAnyObjectByType<LoadingManager>(FindObjectsInactive.Include);
                if (lm != null && lm.loadingPanel != null) { lm.loadingPanel.SetActive(true); if (lm.loadingText != null) lm.loadingText.text = "GPS 로딩중..."; }
            });
            Sleep(0.6f);
            Shot("loading_gps");
            Do(() =>
            {
                var lm = UnityEngine.Object.FindAnyObjectByType<LoadingManager>(FindObjectsInactive.Include);
                if (lm != null && lm.loadingPanel != null) lm.loadingPanel.SetActive(false);
            });

            Do(() => UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include)?.ForceShowGuide());
            Sleep(1.8f);
            Shot("guide1");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Next());
            Sleep(1.2f);
            Shot("guide2");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Next());
            Sleep(1.2f);
            Shot("guide3");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip());
            Sleep(1.0f);
            Check(() => !Active("FirstTimeGuidePanel"), "안내: 건너뛰기로 닫힘");
            Do(() => { PlayerPrefs.SetInt("IsFirstTime", 1); PlayerPrefs.Save(); });
        }

        // 분류 칩 순환 · 댓글 입력줄 · 인디케이터 X 두 번 누르기
        private static void PlanLook2()
        {
            Wait(() => !BootOverlay.Showing, 40f, "시작화면 끝");
            Sleep(1.0f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.5f);
            Do(() => log.Add("  분류 = " + UnityEngine.Object.FindAnyObjectByType<FilterManager>()?.CategoryState));
            Shot("cat_1");
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.5f);
            Do(() => log.Add("  분류 = " + UnityEngine.Object.FindAnyObjectByType<FilterManager>()?.CategoryState));
            Shot("cat_2");
            for (int i = 0; i < 6; i++) Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.5f);
            Do(() => log.Add("  분류(한 바퀴 뒤) = " + UnityEngine.Object.FindAnyObjectByType<FilterManager>()?.CategoryState));
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(1.0f);

            Do(() => UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include)?.OpenCommentPanel(1, "시험"));
            Sleep(1.5f);
            Shot("comment");
            Do(() =>
            {
                var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include);
                var bar = UnityEngine.Object.FindAnyObjectByType<R0926CommentBar>(FindObjectsInactive.Include);
                if (bar != null) bar.React("❤️");
                log.Add("  입력칸 = " + (cm != null && cm.commentInputField != null ? cm.commentInputField.text : "-"));
            });
            Sleep(0.6f);
            Shot("comment_react");
            Do(() => UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include)?.ClosePanel());
            Sleep(1.0f);

            // 인디케이터 X — 첫 번째는 '삭제'로 바뀌기만, 두 번째에 숨김
            Do(() =>
            {
                closeBtn = null;
                foreach (var b in UnityEngine.Object.FindObjectsByType<Button>())
                    if (b.name == "Close0926" && b.gameObject.activeInHierarchy) { closeBtn = b; break; }
                log.Add("  인디케이터 X: " + (closeBtn != null ? "있음" : "없음"));
                if (closeBtn != null) closeBtn.onClick.Invoke();
            });
            Sleep(0.4f);
            Shot("x_armed");
            Check(() => closeBtn == null || closeBtn.gameObject.activeInHierarchy, "X 첫 번째: 숨기지 않고 '삭제'로");
            Do(() => { if (closeBtn != null) closeBtn.onClick.Invoke(); });
            Sleep(0.6f);
            Check(() => closeBtn == null || !closeBtn.gameObject.activeInHierarchy, "X 두 번째: 숨김");
            Shot("x_hidden");

            // 설정 '오브젝트 삭제 기능' 끄기 → X 없음 · 꺾쇠 넷 / 다시 켜기 → X
            Do(() => R0926PlaceSettings.RemoveButton = false);
            Sleep(0.6f);
            Check(() => CountActive("Close0926") == 0, "삭제 기능 끄면 X 가 사라짐");
            Shot("x_off");
            Do(() => R0926PlaceSettings.RemoveButton = true);
            Sleep(0.6f);
            Check(() => CountActive("Close0926") > 0, "삭제 기능 다시 켜면 X");
            Shot("x_on");
        }
        private static Button closeBtn;

        // 10-04 수정 확인 — 채팅방이 화면 안에 열리는지 · 도크 메시지 칸 · 키보드 위 입력줄 · 프로필 · 장소 추가 · 첫 안내
        private static void PlanLook3()
        {
            Wait(() => !BootOverlay.Showing, 40f, "시작화면 끝");
            Sleep(1.0f);

            // 장소 추가 + 키보드 위 입력줄 (이름 칸을 누른 것처럼)
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.4f);
            Shot("add");
            Do(() =>
            {
                var mirror = WiredMirror();
                InputField nameInput = null;
                foreach (var f in UnityEngine.Object.FindObjectsByType<InputField>(FindObjectsInactive.Exclude))
                    if (f.name == "NameInput" && f.gameObject.activeInHierarchy && f.transform.parent.parent.name == "CubeUploadPage") { nameInput = f; break; }
                var m = typeof(UploadInputMirror).GetMethod("ActivateMirrorFor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                log.Add("  입력줄: " + (mirror != null ? "있음" : "없음") + " · 이름칸 " + (nameInput != null ? "있음" : "없음"));
                if (mirror != null && nameInput != null && m != null) m.Invoke(mirror, new object[] { nameInput });
            });
            Sleep(0.05f);
            Do(() => log.Add("  입력줄 처음 투명도 = " + MirrorAlpha()));
            Sleep(0.8f);
            Do(() => log.Add("  0.8초 뒤 투명도 = " + MirrorAlpha()));
            Do(() =>
            {
                var mirror = WiredMirror();
                var mi = mirror != null ? typeof(UploadInputMirror).GetField("mirrorInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(mirror) as InputField : null;
                var src = Find("UploadPage/Clip0926/UploadSheet0926/CubeUploadPage/Panel/NameInput")?.GetComponent<InputField>();
                log.Add("  입력줄 안내 글 = '" + ((mi?.placeholder as Text)?.text ?? "-") + "' · 장소 이름칸 안내 글 = '" + ((src?.placeholder as Text)?.text ?? "-") + "'");
            });
            Shot("mirror");
            Do(() =>
            {
                var mirror = WiredMirror();
                var m = typeof(UploadInputMirror).GetMethod("OnCloseClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mirror != null && m != null) m.Invoke(mirror, null);
            });
            Sleep(0.4f);
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.0f);

            // 장소 수정 화면 — 키보드 위 입력줄 (10-04 연결. 예전엔 연결이 통째로 비어 있었다)
            Do(() => { var fp = Find("Fixpage"); if (fp != null) fp.gameObject.SetActive(true); });
            Sleep(0.8f);
            Shot("fix");
            Do(() =>
            {
                var fp = Find("Fixpage");
                var m = fp != null ? fp.GetComponent<UploadInputMirror>() : null;
                var src = Find("Fixpage/FixUploadPage/Panel/NameInput")?.GetComponent<InputField>();
                var act = typeof(UploadInputMirror).GetMethod("ActivateMirrorFor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                log.Add("  장소 수정 입력줄: " + (m != null ? "있음" : "없음") + " · 이름칸 " + (src != null ? "있음" : "없음"));
                if (m != null && src != null && act != null) act.Invoke(m, new object[] { src });
            });
            Sleep(0.8f);
            Shot("fix_mirror");
            Do(() =>
            {
                var fp = Find("Fixpage");
                var m = fp != null ? fp.GetComponent<UploadInputMirror>() : null;
                var mi = m != null ? typeof(UploadInputMirror).GetField("mirrorInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(m) as InputField : null;
                if (mi != null) mi.text = "테스트 이름";
                var close = typeof(UploadInputMirror).GetMethod("OnCloseClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (m != null && close != null) close.Invoke(m, null);
                var src = Find("Fixpage/FixUploadPage/Panel/NameInput")?.GetComponent<InputField>();
                log.Add("  닫은 뒤 장소 수정 이름칸 = '" + (src != null ? src.text : "-") + "'");
                if (src != null) src.text = "";
            });
            Sleep(0.3f);
            Do(() => { var fp = Find("Fixpage"); if (fp != null) fp.gameObject.SetActive(false); });
            Sleep(0.5f);

            // 메시지 창 → 채팅방 (관리자 WOOPANG 방은 로그인 없이도 열 수 있다)
            Do(() =>
            {
                var mpm = UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include);
                if (mpm != null && mpm.messagePanel != null) mpm.messagePanel.SetActive(true);
            });
            Sleep(1.0f);
            Shot("msg");
            Do(() =>
            {
                var mpm = UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include);
                if (mpm != null) mpm.OpenChatRoom("3", "WOOPANG", null, true);
            });
            Sleep(0.15f);
            Shot("chat_rising");
            Sleep(1.2f);
            Shot("chat");
            Check(() => Active("ChatRoomPanel"), "채팅방: 열려 있음");
            Check(() => OnScreen("ChatRoomPanel/Clip0926/Background"), "채팅방: 대화창이 화면 안에 보임");
            Do(() => log.Add("  메시지 칸 투명도 = " + (Find("Dock0926/Message_Button")?.GetComponent<CanvasGroup>()?.alpha.ToString() ?? "-")));
            Do(() => Click("ChatRoomPanel/Clip0926/Background/Header/BackButton/ClosePx0926"));
            Sleep(1.0f);
            Do(() => log.Add("  닫은 뒤 열린 창: " + OpenPanels()));
            Do(() =>
            {
                var mpm = UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include);
                if (mpm != null) { mpm.messagePanel.SetActive(false); mpm.chatRoomPanel.SetActive(false); }
            });
            Sleep(0.6f);

            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Sleep(0.4f);
            // 프로필 (WOOPANG 계정 — 남의 프로필 모양)
            Do(() => ProfileManager.Instance?.ShowProfile("3"));
            Sleep(2.5f);
            Shot("profile");
            Do(() => { var p = Find("FullProfilePanel"); if (p != null) p.gameObject.SetActive(false); });
            Sleep(0.5f);

            // 첫 안내
            Do(() => UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include)?.ForceShowGuide());
            Sleep(1.8f);
            Shot("guide1");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip());
            Sleep(0.8f);
            Do(() => { PlayerPrefs.SetInt("IsFirstTime", 1); PlayerPrefs.Save(); });
        }

        private static string storeSrc;
        private const string StoreRegion = "종로구 세종로";   // 스토어 캡처 날씨 좌표(광화문 37.5759, 126.9768)의 지역
        private static RawImage backdrop;

        private static void PlanStore()
        {
            Do(() => Backdrop("bg_city.png"));
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(7f);   // 주변 장소를 받아 올 틈
            Shot("s1_main");

            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.6f);
            Shot("s3_list");
            Do(() => Modes()?.Show(1));
            Sleep(3.0f);
            Shot("s4_map");
            Do(() => Modes()?.Show(0));
            Sleep(0.6f);
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(1.0f);

            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.4f);
            Shot("s5_add");
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.0f);

            Do(() => ProfileManager.Instance?.ShowProfile("3"));
            Sleep(2.5f);
            Shot("s6_profile");
            Do(() => { var p = Find("FullProfilePanel"); if (p != null) p.gameObject.SetActive(false); });
            Sleep(0.6f);

            // 하늘 — 카메라를 위로 들면 날씨판이 뜬다
            Do(() =>
            {
                Backdrop("bg_sky.png");
                var cam = Camera.main;
                if (cam == null) return;
                foreach (var bh in cam.GetComponents<Behaviour>()) if (bh.GetType().Name.Contains("PoseDriver")) bh.enabled = false;
                cam.transform.rotation = Quaternion.Euler(-62f, cam.transform.eulerAngles.y, 0f);
            });
            Sleep(4.5f);
            Shot("s2_sky");
        }

        // 스토어 캡처 3 — 아이폰과 같은 시야각으로 (에디터 카메라는 훨씬 넓어 날씨판·오브젝트가 작게 찍혔다)
        private static void PlanStore3()
        {
            Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(6f);
            Do(() => { HidePrivatePlaces(); HideAddressLine(); LogTargets(); DeviceFov(); });   // 주소줄엔 에디터 위치가 찍힌다
            for (int yaw = 0; yaw < 360; yaw += 30)
            {
                int y = yaw;
                Do(() => AimCamera(-3f, y));
                Sleep(1.4f);
                Shot("ar_yaw" + y.ToString("000"));
            }
            Do(() =>
            {
                InjectWeather();
                Backdrop("bg_sky.png");
                AimCamera(-45f, 0f);
            });
            Sleep(4.5f);
            Shot("sky45");
            Do(() => AimCamera(-58f, 0f));
            Sleep(2.0f);
            Shot("sky58");
        }

        // 메시지 목록 → 대화방 → ← : 옆으로 넘어가는지, 돌아온 목록이 제자리인지 (아래에서 다시 올라오지 않는지)
        // 대화창 모양 보기 — 로그인 없이 예시 대화를 채워 찍는다 (서버는 부르지 않는다: 불러오기가 끝난 뒤 내용만 갈아 끼움)
        private static void PlanChatLook()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include)?.OpenChatRoom("chatlook_peer", "연남동이웃", null, false));
            Sleep(1.6f);
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Sleep(0.5f);
            Do(() => FillSampleChat(false));
            Sleep(0.8f);
            Shot("chat_look");
            Check(() => OnScreen("ChatRoomPanel/Clip0926/Background"), "대화방이 화면 안");
            Do(() =>
            {
                var mpm = UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include);
                if (mpm == null || mpm.chatMessageContent == null) return;
                foreach (Transform c in mpm.chatMessageContent)
                {
                    if (!c.gameObject.activeSelf) continue;
                    var rt = (RectTransform)c;
                    var b = rt.Find("BubbleContainer") ?? rt.Find("Bubble");
                    var t = c.GetComponentInChildren<Text>();
                    log.Add("  " + c.name + " h=" + rt.rect.height.ToString("0") + (b != null ? " bubble=" + ((RectTransform)b).rect.size.ToString("0") : "") + (t != null ? " font=" + t.fontSize : ""));
                }
            });
            Do(() => UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include)?.OpenChatRoom("3", "WOOPANG", null, true));
            Sleep(1.6f);
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Sleep(0.4f);
            Do(() => FillSampleChat(true));
            Sleep(0.8f);
            Shot("chat_admin");
        }

        // v24 확인 — 목록 글자 · 설정(30/60fps · 맨 아래 버전) · 'AR 세션 복구 중' 알약 폭 · 불러오는 중에도 도크 누름 · 저작권 자리
        private static readonly List<float> pillWidths = new List<float>();
        // 친구 지도 깊은 확대 — 시험 타일 서버(127.0.0.1:5098 → 브이월드)로 단계별 화면 · 최소 단계·출처 확인 (2026-10-11)
        private static void PlanMapTiles()
        {
            const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            R0926FriendsMap map = null;
            R0926MapTiles tiles = null;
            Func<string, int> count = name => (typeof(R0926MapTiles).GetField(name, F)?.GetValue(tiles) as System.Collections.ICollection)?.Count ?? -1;
            Do(() =>
            {
                R0926MapTiles.EditorBase = "http://127.0.0.1:5098";
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
            });
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => Modes()?.Show(1));
            Sleep(1.0f);
            Do(() =>
            {
                map = UnityEngine.Object.FindAnyObjectByType<R0926FriendsMap>(FindObjectsInactive.Include);
                tiles = map != null ? typeof(R0926FriendsMap).GetField("tiles", F)?.GetValue(map) as R0926MapTiles : null;
            });
            Wait(() => tiles != null && tiles.Available, 15f, "서버 타일 켜짐 (시험 서버)");
            Do(() =>
            {
                var maxF = typeof(R0926FriendsMap).GetProperty("MaxZoom", F);
                log.Add("  최대 확대 " + (maxF != null ? ((float)maxF.GetValue(map)).ToString("0") : "?") + "배 · 최대 단계 " + tiles.MaxLevel);
                map.EditorPreview(true, 1f, 37.5f, 127f);
            });
            Sleep(2.0f);
            Check(() => count("wanted") == 0 && count("inflight") == 0, "세계 보기 — 6 단계보다 얕으면 타일을 묻지 않음");
            Shot("tiles_world");
            var stops = new (float lv, float lat, float lon, string name)[]
            {
                (8f, 36.4f, 127.6f, "korea_z8"), (11f, 37.5665f, 126.978f, "seoul_z11"), (13f, 37.5665f, 126.978f, "seoul_z13"),
                (16.25f, 37.5665f, 126.978f, "seoul_max"), (16.25f, 36.6359f, 126.828f, "yesan_max"),
            };
            foreach (var st in stops)
            {
                Do(() =>
                {
                    float max = (float)typeof(R0926FriendsMap).GetProperty("MaxZoom", F).GetValue(map);
                    map.EditorPreview(true, max / Mathf.Pow(2f, 16.25f - st.lv), st.lat, st.lon);
                });
                Sleep(0.5f);
                Wait(() => count("inflight") == 0, 20f, st.name + " 타일 받기 끝");
                Sleep(0.8f);
                Do(() =>
                {
                    var at = typeof(R0926MapTiles).GetField("attribution", F)?.GetValue(tiles) as Text;
                    log.Add("  " + st.name + ": 받은 타일 " + count("cache") + " · 출처 " + (at != null && at.enabled ? "'" + at.text + "'" : "(안 보임)"));
                });
                Shot("tiles_" + st.name);
            }
            Check(() =>
            {
                var at = typeof(R0926MapTiles).GetField("attribution", F)?.GetValue(tiles) as Text;
                return at != null && at.enabled && at.text == "© 국토교통부 V-World";
            }, "지도 출처 '© 국토교통부 V-World' 보임");
            Do(() =>
            {
                map?.EditorPreviewClear();
                R0926MapTiles.EditorBase = null;
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
            });
        }

        private static void PlanV24()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            // 앞 시험(audit)이 첫 안내를 다시 띄워 두면(ForceShowGuide 가 IsFirstTime 을 지운다) 안내 막이 도크를 가린다
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.3f);
            Shot("v24_list");
            Do(() =>
            {
                Text name = null;
                foreach (var t in CanvasRoot().GetComponentsInChildren<Text>())
                    if (t.name == "Name" && t.transform.parent != null && t.transform.parent.parent != null && t.transform.parent.parent.name == "Content") { name = t; break; }
                log.Add(name != null ? $"  목록 이름 글자 {name.fontSize} '{name.text}'" : "  목록 줄 없음");
                Transform copy = null;
                foreach (var t in CanvasRoot().GetComponentsInChildren<Transform>(true)) if (t.name == "copyright") { copy = t; break; }
                var crt = copy as RectTransform;
                log.Add(crt != null ? $"  저작권 자리 pos={crt.anchoredPosition} size={crt.sizeDelta} aMin={crt.anchorMin} aMax={crt.anchorMax}" : "  저작권 없음");
                log.Add((crt != null && Vector2.Distance(crt.anchoredPosition, new Vector2(0, 20)) < 0.5f && Vector2.Distance(crt.sizeDelta, new Vector2(600, 100)) < 0.5f ? "PASS " : "FAIL ") + "저작권: 대표님이 잡은 자리 그대로 (0,20 · 600×100)");
            });
            Do(() => Modes()?.Show(2));
            Sleep(0.9f);
            Do(() =>
            {
                var sp = UnityEngine.Object.FindAnyObjectByType<R0926SettingsPanel>(FindObjectsInactive.Include);
                var sr = sp != null ? sp.GetComponentInChildren<ScrollRect>(true) : null;
                if (sr != null) sr.verticalNormalizedPosition = 0f;
                else log.Add("  설정 스크롤 없음");
            });
            Sleep(0.5f);
            Shot("v24_settings_bottom");
            Do(() =>
            {
                Transform vf = null;
                foreach (var t in CanvasRoot().GetComponentsInChildren<Transform>(true)) if (t.name == "VersionFooter") { vf = t; break; }
                var vt = vf != null ? vf.GetComponentInChildren<Text>(true) : null;
                log.Add((vt != null && vt.text.StartsWith("Version ") ? "PASS " : "FAIL ") + "설정 맨 아래 버전: " + (vt != null ? "'" + vt.text + "' fs" + vt.fontSize : "없음"));
                var sp = UnityEngine.Object.FindAnyObjectByType<R0926SettingsPanel>(FindObjectsInactive.Include);
                log.Add("  처음 fps " + Application.targetFrameRate + " (저장 " + R0926FrameRate.Fps + ")");
                sp?.SetFps(1);
            });
            Sleep(0.3f);
            Shot("v24_settings_60");
            Check(() => Application.targetFrameRate == 60 && R0926FrameRate.Fps == 60, "설정: 60fps 를 고르면 60");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926SettingsPanel>(FindObjectsInactive.Include)?.SetFps(0));
            Sleep(0.2f);
            Check(() => Application.targetFrameRate == 30 && R0926FrameRate.Fps == 30, "설정: 30fps 로 되돌림 (기본)");
            Do(() => Modes()?.Show(0));
            Sleep(0.6f);
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(1.2f);

            // 'AR 세션 복구 중' — 점이 늘어나도 알약 폭은 그대로
            Do(() =>
            {
                pillWidths.Clear();
                var lm = UnityEngine.Object.FindAnyObjectByType<LoadingManager>(FindObjectsInactive.Include);
                if (lm == null || lm.loadingPanel == null) { log.Add("FAIL 불러오는 중 패널 없음"); return; }
                lm.loadingPanel.SetActive(true);
                typeof(LoadingManager).GetMethod("StartDotAnimation", F)?.Invoke(lm, new object[] { "AR 세션 복구 중" });
            });
            for (int i = 0; i < 10; i++)
            {
                Sleep(0.25f);
                Do(() =>
                {
                    var lm = UnityEngine.Object.FindAnyObjectByType<LoadingManager>(FindObjectsInactive.Include);
                    var pillComp = lm != null ? lm.loadingPanel.GetComponentInChildren<R0926StatusPill>(true) ?? lm.loadingPanel.GetComponent<R0926StatusPill>() : null;
                    var pr = pillComp != null ? typeof(R0926StatusPill).GetField("pill", F)?.GetValue(pillComp) as RectTransform : null;
                    if (pr != null) pillWidths.Add(pr.sizeDelta.x);
                    if (pillWidths.Count == 3) log.Add("  알약 글: " + (lm.loadingText != null ? lm.loadingText.text : "-"));
                });
                if (i == 4) Shot("v24_pill");
            }
            Do(() =>
            {
                if (pillWidths.Count == 0) { log.Add("FAIL 알약 폭을 못 읽음"); return; }
                float lo = pillWidths[0], hi = pillWidths[0];
                foreach (var w in pillWidths) { lo = Mathf.Min(lo, w); hi = Mathf.Max(hi, w); }
                log.Add((hi - lo < 0.5f ? "PASS " : "FAIL ") + $"'AR 세션 복구 중' 알약 폭 그대로 ({lo:0.0}~{hi:0.0}, {pillWidths.Count}번)");
                // 불러오는 중에도 도크를 누를 수 있어야 한다 — 도크 버튼 가운데에서 맨 위에 걸리는 것이 도크
                var es = UnityEngine.EventSystems.EventSystem.current;
                var dock = Find("Dock0926");
                foreach (var n in new[] { "List_Button", "PlusButton", "Message_Button", "MiniProfile" })
                {
                    var rt = dock != null ? dock.Find(n) as RectTransform : null;
                    if (rt == null || es == null) { log.Add("  도크 칸 없음 " + n); continue; }
                    var c = new Vector3[4];
                    rt.GetWorldCorners(c);
                    var at = (Vector2)((c[0] + c[2]) / 2f);
                    var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = at }, hits);
                    var top = hits.Count > 0 ? hits[0].gameObject.transform : null;
                    log.Add((top != null && top.IsChildOf(dock) ? "PASS " : "FAIL ") + "불러오는 중 도크 '" + n + "' 누름 → " + (top != null ? top.name : "없음"));
                }
            });
            Do(() =>
            {
                var lm = UnityEngine.Object.FindAnyObjectByType<LoadingManager>(FindObjectsInactive.Include);
                typeof(LoadingManager).GetMethod("StopDotAnimation", F)?.Invoke(lm, null);
                if (lm != null && lm.loadingPanel != null) lm.loadingPanel.SetActive(false);
            });
        }

        private static void FillSampleChat(bool admin)
        {
            var mpm = UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include);
            if (mpm == null || mpm.chatMessageContent == null) { log.Add("FAIL 대화창 없음"); return; }
            mpm.StopAllCoroutines();
            for (int i = mpm.chatMessageContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(mpm.chatMessageContent.GetChild(i).gameObject);
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(MessagePanelManager);
            var sep = t.GetMethod("CreateDateSeparator", F);
            var bubble = t.GetMethod("CreateMessageBubble", F);
            var sys = t.GetMethod("CreateSystemMessageBubble", F);
            var now = DateTime.Now;
            if (admin)
            {
                sep?.Invoke(mpm, new object[] { now.AddDays(-1) });
                sys?.Invoke(mpm, new object[] { "WOOPANG", "우팡에 오신 걸 환영해요! 근처 장소를 하늘에 띄워 보세요.", now.AddDays(-1).ToString("HH:mm") });
                sep?.Invoke(mpm, new object[] { now });
                sys?.Invoke(mpm, new object[] { "WOOPANG", "보물찾기가 새로 생겼어요. '추가' 창의 세 번째 카드에서 직접 열 수 있어요.", now.ToString("HH:mm") });
                return;
            }
            DMMessage M(int id, bool mine, string text, DateTime at, bool read = true, bool liked = false) => new DMMessage
            {
                id = id, sender_id = mine ? "me" : "chatlook_peer", content = text, is_read = read, is_liked = liked, is_mine = mine,
                created_at = at.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss") + "Z", sender_username = mine ? "나" : "연남동이웃",
            };
            sep?.Invoke(mpm, new object[] { now.AddDays(-1) });
            bubble?.Invoke(mpm, new object[] { M(1, false, "안녕하세요! 연남동 공원 앞에 올린 장소 봤어요", now.AddDays(-1)), false });
            bubble?.Invoke(mpm, new object[] { M(2, true, "앗 감사해요 ㅎㅎ", now.AddDays(-1)), true });
            bubble?.Invoke(mpm, new object[] { M(3, false, "주말에 거기서 보물찾기 열어 볼까 하는데, 같이 하실래요? 상품은 커피 쿠폰 두 장이에요", now.AddDays(-1), true, true), false });
            sep?.Invoke(mpm, new object[] { now });
            bubble?.Invoke(mpm, new object[] { M(4, true, "좋아요! 몇 시쯤이요?", now), true });
            bubble?.Invoke(mpm, new object[] { M(5, true, "저는 오후 두 시 이후면 다 괜찮아요. 근처 카페에서 기다릴게요", now, false), true });
            bubble?.Invoke(mpm, new object[] { M(6, false, "그럼 3시에 봬요", now), false });
        }

        private static void PlanChatNav()
        {
            Vector2 msgPos = Vector2.zero;
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                var mpm = UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include);
                if (mpm != null && mpm.messagePanel != null) mpm.messagePanel.SetActive(true);
            });
            Sleep(1.0f);
            Do(() => { var rt = Find("MessagePanel/Clip0926/Background") as RectTransform; if (rt != null) msgPos = rt.anchoredPosition; });
            Do(() => UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include)?.OpenChatRoom("3", "WOOPANG", null, true));
            Sleep(0.12f);
            Shot("nav_push_mid");
            Sleep(0.6f);
            Shot("nav_chat");
            Check(() => Active("ChatRoomPanel") && !Active("MessagePanel"), "넘긴 뒤: 대화방만 켜짐");
            Check(() => OnScreen("ChatRoomPanel/Clip0926/Background"), "대화방이 화면 안");
            Do(() => Click("ChatRoomPanel/Clip0926/Background/Header/BackButton/ClosePx0926"));
            Sleep(0.12f);
            Shot("nav_pop_mid");
            Sleep(0.8f);
            Shot("nav_back");
            Check(() => Active("MessagePanel") && !Active("ChatRoomPanel"), "돌아온 뒤: 목록만 켜짐");
            Do(() =>
            {
                var rt = Find("MessagePanel/Clip0926/Background") as RectTransform;
                log.Add("  목록 자리: 처음 " + msgPos + " · 돌아온 뒤 " + (rt != null ? rt.anchoredPosition.ToString() : "-"));
            });
            Check(() => { var rt = Find("MessagePanel/Clip0926/Background") as RectTransform; return rt != null && Vector2.Distance(rt.anchoredPosition, msgPos) < 1f; }, "목록이 제자리 (가로·세로)");
            Do(() => log.Add("  열린 창: " + OpenPanels()));
        }

        // 보물찾기 카드 — 시험 서버(127.0.0.1:5099 · X-Test-User, scratchpad hunt_dev_server.py)로:
        // 추가 창 세 번째 카드 → 처음 주최는 수칙 동의 → 숨기기 → 다른 사람으로 참가 → 근접 → 보물 열기
        private const string HuntCardPath = "UploadPage/Clip0926/UploadSheet0926/HuntUploadPage/Body0926/";
        private static Vector2 huntSpot;   // 보물을 숨긴 자리 (다가가며 진동을 볼 때 돌아올 곳)
        private static Transform FirstActiveChild(string path)
        {
            var t = Find(path);
            if (t == null) return null;
            foreach (Transform c in t) if (c.gameObject.activeSelf) return c;
            return null;
        }
        private static int ActiveChildren(string path)
        {
            var t = Find(path);
            int n = 0;
            if (t != null) foreach (Transform c in t) if (c.gameObject.activeSelf) n++;
            return n;
        }
        private static void PlanHunt()
        {
            const string H = HuntCardPath;
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = "hunt_test_host";
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() => log.Add("  열린 창(시작): " + OpenPanels()));
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() => log.Add("  열린 창(추가 누른 뒤): " + OpenPanels() + " · 추가 " + Active("UploadPage")));
            Do(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude)?.SwitchToPanel(2));
            Sleep(2.0f);
            Do(() => log.Add("  열린 창(보물찾기 카드): " + OpenPanels()));
            Shot("hunt_hub");
            Check(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude)?.GetCurrentPanel() == 2, "추가 창 세 번째 카드(보물찾기)로 넘어감");
            Check(() => Active(H + "Hub") && !Active(H + "Hub/Viewport/Content/LoginBox"), "처음 화면 · 로그인 안내 없음(시험 사용자)");
            Do(() => Click(H + "Hub/Viewport/Content/Hero"));
            Sleep(2.0f);
            Shot("hunt_rules");
            Check(() => Active(H + "Rules"), "처음 주최 → 주최 수칙");
            Check(() => ActiveChildren(H + "Rules/Viewport/Content/List") == 8, "수칙 8가지");
            Do(() => Click(H + "Rules/Viewport/Content/Agree"));
            Sleep(0.3f);
            Do(() => Click(H + "Rules/Viewport/Content/AgreeButton"));
            Sleep(1.5f);
            Shot("hunt_form");
            Check(() => Active(H + "Form"), "동의 → 숨기기 양식");
            Check(() => ActiveChildren(H + "Form/Viewport/Content/PlayerChips") == 5, "참가 인원 칩 5개(10·20·30·50·100)");
            Do(() => { var f = Find(H + "Form/Viewport/Content/TitleInput")?.GetComponent<InputField>(); if (f != null) f.text = "시험 보물 (플레이 시험)"; });
            Do(() => { var f = Find(H + "Form/Viewport/Content/HintInput")?.GetComponent<InputField>(); if (f != null) f.text = "충남도청 앞 분수대 근처"; });
            Do(() => Click(H + "Form/Viewport/Content/Submit"));
            Sleep(2.5f);
            Shot("hunt_detail_host");
            Check(() => Active(H + "Detail"), "숨기기 → 상세(주최자)");
            Check(() => !Active(H + "Detail/Viewport/Content/Open"), "주최자 상세엔 '보물 열기' 없음");
            Check(() => (Find(H + "Detail/Viewport/Content/Info")?.GetComponent<Text>()?.text ?? "").Contains("참가  0/30명"), "상세: 참가 0/30명 (인원 기본 30)");
            Do(() => { R0926HuntApi.EditorTestUser = "hunt_test_player"; UnityEngine.Object.FindAnyObjectByType<R0926HuntCard>(FindObjectsInactive.Include)?.ShowHub(); });
            Sleep(2.5f);
            Shot("hunt_hub_player");
            Check(() => ActiveChildren(H + "Hub/Viewport/Content/NearList") >= 1, "다른 사람 근처 목록에 그 게임");
            Do(() => FirstActiveChild(H + "Hub/Viewport/Content/NearList")?.GetComponent<Button>()?.onClick.Invoke());
            Sleep(2.0f);
            // 보물에서 250m 떨어진 데서 참가 → 첫 근접 확인은 기준만 → 보물 자리로 걸어가면 한 번 진동 ('가까워지면 진동' 기본 켜짐)
            Do(() =>
            {
                huntSpot = new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
                VirtualLocation.Instance.SetCoordinates(huntSpot.x + 0.00225f, huntSpot.y);
                R0926HuntCard.EditorBuzzes = 0;
            });
            Do(() => Click(H + "Detail/Viewport/Content/Join"));
            Sleep(11.0f);   // 참가 → 상세 다시 → 근접 확인(8.5초마다)
            Do(() => log.Add("  250m 밖: " + (Find(H + "Detail/Viewport/Content/Near")?.GetComponent<Text>()?.text ?? "-")));
            Check(() => !Active(H + "Detail/Viewport/Content/Open") && R0926HuntCard.EditorBuzzes == 0, "250m 밖에서 참가 → '보물 열기' 아직 없음 · 진동 없음(첫 확인은 기준만)");
            Do(() => VirtualLocation.Instance.SetCoordinates(huntSpot.x, huntSpot.y));
            Sleep(9.5f);
            Shot("hunt_near");
            Check(() => (Find(H + "Detail/Viewport/Content/Info")?.GetComponent<Text>()?.text ?? "").Contains("참가  1/30명"), "참가 → 상세 1/30명");
            Check(() => Active(H + "Detail/Viewport/Content/Open"), "보이는 거리 안 → '보물 열기'");
            Check(() => R0926HuntCard.EditorBuzzes == 1, "보물 자리로 다가감 → 진동 한 번");
            Do(() => Click(H + "Detail/Viewport/Content/Open"));
            Sleep(3.0f);
            Shot("hunt_found");
            Do(() => log.Add("  내 상태: " + (Find(H + "Detail/Viewport/Content/Mine")?.GetComponent<Text>()?.text ?? "-")));
            Check(() => (Find(H + "Detail/Viewport/Content/Mine")?.GetComponent<Text>()?.text ?? "").Contains("찾았어요"), "보물 열기 → 찾음");
            Do(() => { R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; });
        }

        // 설정 '보물찾기' 묶음 — 서버 설정(/api/hunts/prefs)이 스위치에 · 누르면 서버에 저장 · 참여를 끄면 알림 두 줄이 흐려지고 보물찾기 카드에 안내
        private static void PlanHuntPrefs()
        {
            const string S = "ListPanel/Sheet0926/SettingsPanel0926/Viewport/Content/";
            const string H = HuntCardPath;
            const string U = "hunt_test_prefs";
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = U;
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926SheetModes>(FindObjectsInactive.Include)?.Show(2));
            Sleep(2.0f);
            Do(() => ScrollToChild(S + "SecHunt"));
            Sleep(0.8f);
            Shot("prefs_on");
            Check(() => Active(S + "GroupHunt/RowHunt") && Active(S + "GroupHuntAlerts/RowHuntNew") && Active(S + "GroupHuntAlerts/RowHuntNear"), "설정 탭에 보물찾기 세 줄(참여 · 새 게임 알림 · 가까워지면 진동)");
            Check(() => SwitchOn(S + "GroupHunt/RowHunt") && SwitchOn(S + "GroupHuntAlerts/RowHuntNew") && SwitchOn(S + "GroupHuntAlerts/RowHuntNear"), "처음엔 셋 다 켜짐 (서버 기본값)");
            Check(() => !Active(S + "HuntNote"), "로그인돼 있으면 안내 줄 없음");
            Do(() => Click(S + "GroupHuntAlerts/RowHuntNear"));
            Sleep(1.5f);
            Check(() => !SwitchOn(S + "GroupHuntAlerts/RowHuntNear") && !ServerPref(U, "notify_near") && !R0926HuntPrefs.NotifyNear, "가까워지면 진동 끔 → 스위치·서버·앱 값 모두 꺼짐");
            Do(() => Click(S + "GroupHuntAlerts/RowHuntNear"));
            Sleep(1.5f);
            Check(() => SwitchOn(S + "GroupHuntAlerts/RowHuntNear") && ServerPref(U, "notify_near"), "다시 켬 → 서버도 켜짐");
            Do(() => Click(S + "GroupHunt/RowHunt"));
            Sleep(1.5f);
            Shot("prefs_off");
            Check(() => !SwitchOn(S + "GroupHunt/RowHunt") && !ServerPref(U, "enabled"), "참여 끔 → 서버에 저장");
            Check(() => Find(S + "GroupHuntAlerts")?.GetComponent<CanvasGroup>()?.interactable == false, "참여를 끄면 알림 두 줄이 흐려짐(못 누름)");
            Back();
            Sleep(1.0f);
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude)?.SwitchToPanel(2));
            Sleep(2.0f);
            Shot("prefs_hub_off");
            Check(() => Active(H + "Hub/Viewport/Content/OffBox") && !Active(H + "Hub/Viewport/Content/NearEmpty"), "보물찾기 카드 → '설정에서 보물찾기 참여를 꺼 두었어요' 한 줄만 (같은 말 두 줄 X)");
            Back();
            Sleep(1.0f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926SheetModes>(FindObjectsInactive.Include)?.Show(2));
            Sleep(2.0f);
            Check(() => !SwitchOn(S + "GroupHunt/RowHunt"), "다시 열어도 서버 값(꺼짐) 그대로");
            Do(() => Click(S + "GroupHunt/RowHunt"));
            Sleep(1.5f);
            Check(() => SwitchOn(S + "GroupHunt/RowHunt") && ServerPref(U, "enabled") && Find(S + "GroupHuntAlerts")?.GetComponent<CanvasGroup>()?.interactable == true,
                "참여 다시 켬 → 서버 켜짐 · 알림 줄 다시 누를 수 있음");
            Do(() => { R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; });
        }

        // 보물찾기 알림을 누름 — 앱이 켜지는 중이면 시작화면·첫 안내가 걷힌 뒤 · 위에 덮인 창(목록)은 닫고 추가 창 → 보물찾기 카드 → 그 게임 상세.
        // 그 게임 상세를 보는 중 그 게임 소식이 오면 알림 대신 다시 받기만 · 끝 알림(별점 자격)은 주최자 프로필
        private static int pushHuntA, pushHuntB, profileAt0;
        private static void PlanHuntPush()
        {
            const string H = HuntCardPath;
            Do(() =>
            {
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = "hunt_test_player";
                pushHuntA = DevCreateHunt("hunt_test_pusha", "알림 시험 보물 A", "36.659300");
                pushHuntB = DevCreateHunt("hunt_test_pushb", "알림 시험 보물 B", "36.659700");
                profileAt0 = Find("FullProfilePanel")?.GetSiblingIndex() ?? -1;
                log.Add("  시험 게임 " + pushHuntA + " · " + pushHuntB + " · 열린 창(시작): " + OpenPanels());
                R0926HuntPush.Open(pushHuntA.ToString(), "hunt_test_pusha", false);   // 앱이 켜지는 중 알림을 누른 것과 같다
            });
            Check(() => pushHuntA > 0 && pushHuntB > 0, "개발 서버에 시험 게임 둘");
            Check(() => UnityEngine.Object.FindAnyObjectByType<R0926HuntPush>() != null, "캔버스에 알림 길잡이(R0926HuntPush)");
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Wait(() => Active(H + "Detail") && DetailTitle().Contains("알림 시험 보물 A"), 15f, "켜지는 중 누른 알림 → 상세");
            Sleep(1.0f);
            Shot("push_cold");
            Check(() => Active("UploadPage") && UploadSwipe()?.GetCurrentPanel() == 2 && DetailTitle().Contains("알림 시험 보물 A"),
                "켜지는 중 누른 알림 → 시작화면이 걷힌 뒤 추가 창 보물찾기 카드의 그 게임(A) 상세");
            Back();
            Sleep(1.2f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.5f);
            Check(() => Active("ListPanel") && !Active("UploadPage"), "목록 창이 덮고 있음 (알림을 누르기 전)");
            Do(() => R0926HuntPush.Open(pushHuntB.ToString(), "hunt_test_pushb", false));
            Wait(() => Active(H + "Detail") && DetailTitle().Contains("알림 시험 보물 B"), 12f, "목록 위에서 누른 알림 → 상세");
            Sleep(1.0f);
            Shot("push_warm");
            Check(() => !Active("ListPanel") && Active("UploadPage") && DetailTitle().Contains("알림 시험 보물 B"), "목록 창을 닫고 그 게임(B) 상세");
            Do(() => DevPost("hunt_test_pushb", "/api/hunts/" + pushHuntB + "/message", "{\"text\":\"알림 시험 소식\"}"));
            Check(() => R0926HuntPush.RefreshShowing(HuntNews(pushHuntB)) && !R0926HuntPush.RefreshShowing(HuntNews(pushHuntA)),
                "보고 있는 게임(B) 소식은 다시 받기만(알림 X) · 다른 게임(A) 소식은 알림");
            Sleep(2.0f);
            Shot("push_refresh");
            Check(() => FeedHas("알림 시험 소식"), "다시 받은 상세에 새 소식");
            Do(() => R0926HuntPush.Open(pushHuntB.ToString(), "3", true));
            Wait(() => Active("FullProfilePanel"), 12f, "끝 알림(별점 자격) → 주최자 프로필");
            Sleep(1.0f);
            Shot("push_review");
            Check(() => Active("FullProfilePanel") && Active("UploadPage") && Above("FullProfilePanel", "UploadPage"),
                "끝 알림(별점 자격) → 주최자 프로필이 보던 추가 창 위에 (추가 창은 그 아래 그대로)");
            Back();
            Sleep(1.0f);
            Check(() => !Active("FullProfilePanel") && Active("UploadPage") && profileAt0 >= 0 && Find("FullProfilePanel")?.GetSiblingIndex() == profileAt0,
                "프로필을 닫으면 추가 창으로 · 프로필 창은 원래 순서로 (다른 데서 여는 프로필은 예전 그대로)");
            Do(() => { R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; });
        }

        private static bool Above(string a, string b)
        {
            var x = Find(a); var y = Find(b);
            return x != null && y != null && x.parent == y.parent && x.GetSiblingIndex() > y.GetSiblingIndex();
        }
        private static SwipePanelController UploadSwipe() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude);
        private static string DetailTitle() => Find(HuntCardPath + "Detail/Viewport/Content/Title")?.GetComponent<Text>()?.text ?? "";
        private static bool FeedHas(string s)
        {
            var feed = Find(HuntCardPath + "Detail/Viewport/Content/Feed");
            if (feed != null) foreach (var t in feed.GetComponentsInChildren<Text>()) if (t.text.Contains(s)) return true;
            return false;
        }
        private static Dictionary<string, string> HuntNews(int id) => new Dictionary<string, string> { { "msg_type", "hunt_message" }, { "hunt_id", id.ToString() } };

        [Serializable] private class DevHuntReply { public DevHunt hunt; }
        [Serializable] private class DevHunt { public int id; }
        // 개발 서버에 시험 게임 하나 (수칙 동의 → 숨기기) — 시험 아이디라 알림은 기록만 된다
        private static int DevCreateHunt(string host, string title, string lat)
        {
            DevPost(host, "/api/hunts/rules/accept", "{\"version\":1}");
            string r = DevPost(host, "/api/hunts", "{\"title\":\"" + title + "\",\"mode\":\"touch\",\"winners\":1,\"radius_km\":5,\"visible_m\":50,\"lat\":" + lat
                                                  + ",\"lon\":126.673000,\"acc\":5,\"duration_min\":60}");
            return string.IsNullOrEmpty(r) ? 0 : JsonUtility.FromJson<DevHuntReply>(r)?.hunt?.id ?? 0;
        }
        private static string DevPost(string user, string path, string json)
        {
            using (var wc = new System.Net.WebClient())
            {
                wc.Encoding = System.Text.Encoding.UTF8;
                wc.Headers.Add("X-Test-User", user);
                wc.Headers.Add("Content-Type", "application/json");
                try { return wc.UploadString("http://127.0.0.1:5099" + path, json); }
                catch (System.Net.WebException e) { log.Add("  개발 서버 " + path + " 실패: " + e.Message); return ""; }
            }
        }

        // AR 보물 — 참가한 게임의 보물에 '보이는 거리'(50m)까지 다가가면 그 자리에 금빛 상자 · 누르면 찾기.
        // 개발 서버의 두 게임(누르기 T1 · 코드 T2)에 시험 아이디로 참가해 두고, 에디터 위치(VirtualLocation)를 옮기며 걷는다
        private const string TreasurePref = "r0926_hunt_treasure";
        private const string TrPlayer = "hunt_test_tplayer";
        private const System.Reflection.BindingFlags NPI = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        private static int trHunt1, trHunt2;
        private static Vector2 trBase;
        private static bool treasureRun;
        private static void PlanHuntTreasure()
        {
            const string H = HuntCardPath;
            const string T1Title = "AR 보물 시험 1", T2Title = "AR 보물 시험 2 (코드)", Question = "문 앞 번호 네 자리는?";
            Do(() =>
            {
                treasureRun = true;
                PlayerPrefs.DeleteKey(TreasurePref);
                typeof(R0926HuntTreasure).GetField("loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, false);
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = TrPlayer;
                trBase = new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
                // T1 은 출발점 33m 북쪽(누르기) · T2 는 그 동쪽 98m(코드)
                trHunt1 = DevCreateHuntAt("hunt_test_thost", T1Title, trBase.x + 0.0003f, trBase.y, null, null);
                trHunt2 = DevCreateHuntAt("hunt_test_thost2", T2Title, trBase.x + 0.0003f, trBase.y + 0.0011f, "4321", Question);
                foreach (int id in new[] { trHunt1, trHunt2 })
                    DevPost(TrPlayer, "/api/hunts/" + id + "/join", "{\"lat\":" + Deg(trBase.x) + ",\"lon\":" + Deg(trBase.y) + "}");
                log.Add("  시험 게임 T1 " + trHunt1 + " · T2 " + trHunt2 + " · 출발점 " + Deg(trBase.x) + "," + Deg(trBase.y));
                VirtualLocation.Instance.SetCoordinates(trBase.x - 0.00225f, trBase.y);   // T1 에서 284m 남쪽
            });
            Check(() => trHunt1 > 0 && trHunt2 > 0 && Has(DevGet(TrPlayer, "/api/hunts/" + trHunt2), "\"joined\":true"), "개발 서버에 시험 게임 둘 (누르기 T1 · 코드 T2) · 참가");
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                if (Active("LoginPromptPanel")) BackNow();
            });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Sleep(0.6f);
            Check(() => UnityEngine.Object.FindAnyObjectByType<R0926HuntTreasure>() != null && !R0926HuntTreasure.Tracking(trHunt1) && R0926HuntTreasure.SpawnedObjects.Count == 0,
                "캔버스에 AR 보물(R0926HuntTreasure) · 처음엔 지켜보는 게임 없음");

            // 지켜보기 — 상세를 열면 · 처음 화면의 참가 목록 · 기기에 적어 둔다
            Do(() => R0926HuntPush.Open(trHunt1.ToString(), null, false));
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(T1Title), 15f, "T1 상세");
            Sleep(1.0f);
            Check(() => R0926HuntTreasure.Tracking(trHunt1) && PrefLine(trHunt1) == trHunt1 + "\t0\t" + T1Title, "참가한 게임 상세를 열면 지켜봄 · 기기에 적어 둠 (id · 시작 시각 · 제목)");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926HuntCard>(FindObjectsInactive.Exclude)?.ShowHub());
            Wait(() => R0926HuntTreasure.Tracking(trHunt2), 10f, "처음 화면 참가 목록 → T2");
            Sleep(0.5f);
            Check(() => R0926HuntTreasure.Tracking(trHunt2) && PrefLine(trHunt2) == trHunt2 + "\t0\t" + T2Title, "처음 화면의 참가 목록(T2)도 지켜봄");
            Check(() => R0926HuntTreasure.SpawnedObjects.Count == 0, "284m 밖 → 보물 안 뜸 (서버가 자리를 안 준다)");
            CloseUpload();
            Check(() => !Active("UploadPage"), "추가 창을 닫음 — 닫혀 있어도 지켜본다");

            // 45m — 보이는 거리 안: 상자 · 알림 · 가까워져 진동 (카드 없이도)
            Do(() => { R0926HuntCard.EditorBuzzes = 0; VirtualLocation.Instance.SetCoordinates(trBase.x + 0.0003f - 0.000405f, trBase.y); });
            Wait(() => R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + trHunt1), 25f, "45m → T1 보물");
            Sleep(0.8f);
            Do(() => FaceTreasure(trHunt1));
            Sleep(0.3f);
            Shot("treasure_45m");
            Check(() =>
            {
                var go = Treasure(trHunt1);
                var t = go != null ? go.GetComponentInChildren<Target>(true) : null;
                var r = go != null ? go.GetComponentInChildren<MeshRenderer>(true) : null;
                return t != null && r != null && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("GoldenGlowCube")
                       && t.PlaceName == T1Title && t.placeId == "hunt_" + trHunt1 && Mathf.Abs(t.TargetColor.b - 0.31f) < 0.01f && t.OnIndicatorTapped != null;
            }, "금빛 상자 · 이름 · 금색 박스·화살표 · 박스를 눌러도 찾기");
            Check(() => Active("HuntToast0926") && TextOf("HuntToast0926/Pill/Title") == "보물이 근처에 있어요!" && TextOf("HuntToast0926/Pill/Sub") == "금빛 상자를 눌러 열어 보세요",
                "처음 보이면 상태 알약 아래 알림 '보물이 근처에 있어요!'");
            Check(() =>
            {
                var pill = Find("HuntToast0926/Pill") as RectTransform;
                var title = Find("HuntToast0926/Pill/Title")?.GetComponent<Text>();
                var area = Find("HuntToast0926")?.parent as RectTransform;
                if (pill == null || title == null || area == null) return false;
                log.Add("  알림 폭 " + pill.rect.width.ToString("0") + " / 화면 " + area.rect.width.ToString("0") + " · 글 " + title.preferredWidth.ToString("0"));
                return pill.rect.width <= area.rect.width - 95f && title.preferredWidth <= title.rectTransform.rect.width + 1f;
            }, "알림 알약은 글에 맞춘 폭 · 화면 안");
            Check(() => R0926HuntCard.EditorBuzzes >= 1, "카드를 닫아 둬도 가까워지면 진동");

            // 다섯 매니저와 같이 — 줌 · 백그라운드 숨김/복구 · fallback 렌더러 · 하늘 날씨 가림
            Check(() =>
            {
                var z = UnityEngine.Object.FindAnyObjectByType<ARObjectZoomController>(FindObjectsInactive.Include);
                var go = Treasure(trHunt1);
                if (z == null || go == null) { log.Add("  줌 " + (z != null) + " · 보물 " + (go != null)); return false; }
                typeof(ARObjectZoomController).GetField("currentZoom", NPI).SetValue(z, 1.5f);
                typeof(ARObjectZoomController).GetMethod("ApplyZoomToARObjects", NPI).Invoke(z, null);
                bool zoomed = Mathf.Abs(go.transform.localScale.x - 1.5f) < 0.01f;
                z.ResetZoom();
                return zoomed && Mathf.Abs(go.transform.localScale.x - 1f) < 0.01f;
            }, "줌 1.5배 → 보물도 1.5배 · 되돌리면 1배");
            Check(() =>
            {
                var lm = UnityEngine.Object.FindAnyObjectByType<LoadingManager>(FindObjectsInactive.Include);
                var go = Treasure(trHunt1);
                if (lm == null || go == null) return false;
                typeof(LoadingManager).GetMethod("HideAllManagerObjects", NPI).Invoke(lm, null);
                bool off = !go.activeSelf;
                typeof(LoadingManager).GetMethod("RestoreAllManagerObjects", NPI).Invoke(lm, null);
                return off && go.activeSelf;
            }, "백그라운드 복귀: LoadingManager 가 숨겼다가 다시 켬");
            Check(() =>
            {
                var lm = UnityEngine.Object.FindAnyObjectByType<LoadingManager>(FindObjectsInactive.Include);
                var go = Treasure(trHunt1);
                var a = go != null ? go.GetComponent<CustomARGeospatialCreatorAnchor>() : null;
                if (lm == null || a == null) return false;
                var force = typeof(CustomARGeospatialCreatorAnchor).GetField("_forceHideRenderers", NPI);
                var vis = typeof(LoadingManager).GetMethod("SetAllManagerRenderersVisible", NPI);
                vis.Invoke(lm, new object[] { false });
                bool off = (bool)force.GetValue(a) && Renderers(go, false);
                vis.Invoke(lm, new object[] { true });
                return off && !(bool)force.GetValue(a) && Renderers(go, true);
            }, "fallback: 렌더러 숨김 → 다시 보임 (Target 박스는 그대로)");
            Check(() =>
            {
                var sw = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                if (sw == null || typeof(R0926SkyWeather).GetField("cam", NPI).GetValue(sw) == null) { log.Add("  하늘 날씨/카메라 없음"); return false; }
                var check = typeof(R0926SkyWeather).GetMethod("Check", NPI, null, new[] { typeof(IEnumerable<GameObject>), typeof(Vector3) }, null);
                return (bool)check.Invoke(sw, new object[] { R0926HuntTreasure.SpawnedObjects.Values, Camera.main.transform.position });
            }, "하늘 날씨: 60m 안 화면의 보물을 가까운 오브젝트로 침");

            // 45m 에서 상자를 누름 → '조금만 더 가까이' (판정 거리 30m)
            TapTreasure(() => trHunt1);
            Wait(() => TextOf("HuntToast0926/Pill/Title") == "조금만 더 가까이", 6f, "45m 누름 → 조금만 더");
            Sleep(0.4f);
            Shot("treasure_toofar");
            Do(() => log.Add("  알림: " + TextOf("HuntToast0926/Pill/Title") + " / " + TextOf("HuntToast0926/Pill/Sub")));
            Check(() => TextOf("HuntToast0926/Pill/Title") == "조금만 더 가까이" && TextOf("HuntToast0926/Pill/Sub").EndsWith("m 더 가 보세요")
                        && R0926HuntTreasure.Tracking(trHunt1) && Treasure(trHunt1) != null,
                "45m 에서 상자를 누르면 '조금만 더 가까이 · Nm 더 가 보세요' · 보물은 그대로");

            // 12m — 누르면 찾음 → 상자가 사라지고 그 게임 상세
            Do(() => VirtualLocation.Instance.SetCoordinates(trBase.x + 0.0003f - 0.000108f, trBase.y));
            Sleep(0.6f);
            Do(() => FaceTreasure(trHunt1));
            Sleep(0.3f);
            Shot("treasure_12m");
            TapTreasure(() => trHunt1);
            Wait(() => TextOf("HuntToast0926/Pill/Title") == "보물을 찾았어요!", 6f, "12m 누름 → 찾음");
            Sleep(0.15f);
            Shot("treasure_found");
            Check(() => TextOf("HuntToast0926/Pill/Sub") == "1번째로 찾았어요" && !R0926HuntTreasure.Tracking(trHunt1) && !R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + trHunt1),
                "찾음 → '보물을 찾았어요! · 1번째로 찾았어요' · 그만 지켜봄");
            Sleep(0.6f);
            Check(() => GameObject.Find("HuntTreasure_" + trHunt1) == null && PrefLine(trHunt1) == null, "상자는 살짝 커지며 사라짐 · 기기 기록에서도 지움");
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(T1Title), 12f, "찾은 뒤 그 게임 상세");
            Sleep(1.2f);
            Shot("treasure_found_detail");
            Check(() => TextOf(H + "Detail/Viewport/Content/Mine").Contains("찾았어요") && Has(DevGet(TrPlayer, "/api/hunts/" + trHunt1), "\"rank\":1"),
                "찾은 뒤 그 게임 상세 — '찾았어요' · 서버에 1등");
            Check(() => !Active("HuntToast0926"), "상세가 열리면 알림은 걷힘 — 창의 탭을 가리지 않게");

            // 코드 게임 — 창(T1 상세)이 열린 채 다가가면 '근처에 있어요'는 창을 닫을 때까지 미룬다
            Do(() => VirtualLocation.Instance.SetCoordinates(trBase.x + 0.0003f - 0.00018f, trBase.y + 0.0011f));   // T2 에서 20m 남쪽
            Wait(() => R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + trHunt2), 25f, "20m → T2 보물 (상세가 열린 채)");
            Sleep(0.5f);
            Check(() => Active("UploadPage") && !Active("HuntToast0926"), "창이 열려 있으면 '보물이 근처에 있어요' 알림을 미룸");
            CloseUpload();
            Wait(() => Active("HuntToast0926") && TextOf("HuntToast0926/Pill/Title") == "보물이 근처에 있어요!", 6f, "창을 닫으면 미뤄 둔 알림");
            Check(() => Active("HuntToast0926") && TextOf("HuntToast0926/Pill/Title") == "보물이 근처에 있어요!", "창을 닫으면 미뤄 둔 '보물이 근처에 있어요!'");
            // 박스(화살표)를 누르는 길로: 거리 통과 → '코드를 넣어 주세요' → 상세의 코드 칸
            Sleep(0.3f);
            Do(() => FaceTreasure(trHunt2));
            Sleep(0.3f);
            Shot("treasure_code_20m");
            Do(() => Treasure(trHunt2)?.GetComponentInChildren<Target>(true)?.OnIndicatorTapped?.Invoke());
            Wait(() => TextOf("HuntToast0926/Pill/Title") == "코드를 넣어 주세요", 6f, "박스 누름 → 코드");
            Check(() => TextOf("HuntToast0926/Pill/Sub") == Question, "코드 게임: '코드를 넣어 주세요' · 아래 줄에 문제");
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(T2Title) && Active(H + "Detail/Viewport/Content/ClaimCodeBox"), 20f, "T2 상세 코드 칸");
            Sleep(1.0f);
            Shot("treasure_code_detail");
            Check(() => Active(H + "Detail/Viewport/Content/ClaimCodeBox") && R0926HuntTreasure.Tracking(trHunt2) && Treasure(trHunt2) != null && !Active("HuntToast0926"),
                "그 게임 상세의 코드 칸 · 보물은 그대로 (코드를 넣을 때까지) · 알림은 걷힘");

            // 설정에서 보물찾기 참여를 끄면 치운다 (지켜보기는 그대로) · 다시 켜면 다시 뜬다
            Do(() => R0926HuntPrefs.Enabled = false);
            Sleep(0.4f);
            Check(() => !R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + trHunt2) && R0926HuntTreasure.Tracking(trHunt2), "참여를 끄면 보물을 치움 (지켜보기는 그대로)");
            Do(() => R0926HuntPrefs.Enabled = true);
            Wait(() => R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + trHunt2), 20f, "다시 켜면 보물");
            Check(() => R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + trHunt2), "다시 켜면 다음 근접 확인에서 다시 뜸");

            // 나가기 → 치우고 그만 본다
            Do(() => log.Add("  나가기 버튼 보임 " + Active(H + "Detail/Viewport/Content/Leave")));
            Do(() => Click(H + "Detail/Viewport/Content/Leave"));
            Wait(() => !R0926HuntTreasure.Tracking(trHunt2), 8f, "나가기 → 그만 봄");
            Sleep(0.5f);
            Check(() => !R0926HuntTreasure.Tracking(trHunt2) && !R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + trHunt2)
                        && GameObject.Find("HuntTreasure_" + trHunt2) == null && !PlayerPrefs.HasKey(TreasurePref),
                "나가기 → 보물 치움 · 지켜보기 끝 · 기기 기록 지움");

            // 지켜보는 게임은 최근 다섯 · 예약 게임은 시작 시각부터 · 주최자·찾은 게임은 안 봄 (서버 없이)
            Check(() =>
            {
                for (int i = 1; i <= 6; i++) TrackFake(990000 + i, "live", false, "null");
                bool ok = !R0926HuntTreasure.Tracking(990001);
                for (int i = 2; i <= 6; i++) ok &= R0926HuntTreasure.Tracking(990000 + i);
                for (int i = 1; i <= 6; i++) R0926HuntTreasure.Untrack(990000 + i);
                return ok && !PlayerPrefs.HasKey(TreasurePref);
            }, "지켜보는 게임은 최근 다섯 — 여섯째가 오면 가장 오래 안 본 것부터 놓음");
            Check(() =>
            {
                TrackFake(990011, "scheduled", false, "null", "2026-10-12T13:00:00Z");
                bool at = PrefLine(990011) == "990011\t" + new DateTimeOffset(2026, 10, 12, 13, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() + "\tAR";
                TrackFake(990012, "live", true, "null");
                TrackFake(990013, "live", false, "1");
                bool skip = !R0926HuntTreasure.Tracking(990012) && !R0926HuntTreasure.Tracking(990013);
                R0926HuntTreasure.Untrack(990011);
                return at && skip && !PlayerPrefs.HasKey(TreasurePref);
            }, "예약 게임은 시작 시각(UTC)을 적어 그때부터 · 주최자·이미 찾은 게임은 안 봄");
            Do(() => { R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; PlayerPrefs.DeleteKey(TreasurePref); treasureRun = false; });
        }

        private static int DevCreateHuntAt(string host, string title, float lat, float lon, string code, string question)
        {
            DevPost(host, "/api/hunts/rules/accept", "{\"version\":1}");
            string mode = code == null ? "\"mode\":\"touch\"" : "\"mode\":\"code\",\"code\":\"" + code + "\",\"question\":\"" + question + "\"";
            return HuntId(DevPost(host, "/api/hunts", "{\"title\":\"" + title + "\"," + mode + ",\"winners\":1,\"radius_km\":5,\"visible_m\":50,\"lat\":" + Deg(lat)
                                                    + ",\"lon\":" + Deg(lon) + ",\"acc\":5,\"duration_min\":60}"));
        }
        private static string Deg(float v) => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
        // 서버 없이 카드가 그린 것처럼 — SimpleJSON 은 패키지(TextMateSharp)에도 같은 이름이 있어 에디터 쪽에선 타입 이름을 쓰지 않는다
        private static void TrackFake(int id, string status, bool host, string rank, string startsAt = null)
        {
            string json = "{\"id\":" + id + ",\"status\":\"" + status + "\",\"title\":\"AR\",\"is_host\":" + (host ? "true" : "false")
                          + (startsAt != null ? ",\"starts_at\":\"" + startsAt + "\"" : "") + ",\"me\":{\"joined\":true,\"rank\":" + rank + "}}";
            var node = typeof(R0926HuntTreasure).Assembly.GetType("SimpleJSON.JSON").GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { json });
            typeof(R0926HuntTreasure).GetMethod("Track").Invoke(null, new[] { node });
        }
        // 기기에 적어 둔 그 게임 줄 (없으면 null)
        private static string PrefLine(int id)
        {
            foreach (var line in PlayerPrefs.GetString(TreasurePref, "").Split('\n'))
                if (line.StartsWith(id + "\t")) return line;
            return null;
        }
        private static GameObject Treasure(int id) => R0926HuntTreasure.SpawnedObjects.TryGetValue("hunt_" + id, out var go) ? go : null;
        private static Collider TreasureCollider(int id) => Treasure(id)?.GetComponentInChildren<Collider>();
        private static bool Renderers(GameObject go, bool on)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.enabled != on) return false;
            return true;
        }
        // 상자가 화면 가운데쯤 오게 — 에디터 카메라가 다른 쪽을 보고 있으면 돌린다 (기기에선 사람이 돌아본다)
        private static void FaceTreasure(int id)
        {
            var c = TreasureCollider(id); var cam = Camera.main;
            if (c == null || cam == null) { log.Add("  보물/카메라 없음"); return; }
            Vector3 v = cam.WorldToViewportPoint(c.bounds.center);
            log.Add("  보물 화면 위치 " + v.ToString("F2") + " · 거리 " + Vector3.Distance(cam.transform.position, c.bounds.center).ToString("0.0") + "m");
            if (v.z > 0f && v.x > 0.2f && v.x < 0.8f && v.y > 0.2f && v.y < 0.8f) return;
            Vector3 d = c.bounds.center - cam.transform.position; d.y = 0f;
            cam.transform.rotation = Quaternion.LookRotation(d);
            log.Add("  카메라를 보물 쪽으로 돌림 → " + cam.WorldToViewportPoint(c.bounds.center).ToString("F2"));
        }
        // 상자를 마우스로 누른다 — 먼저 그 자리로 옮겨 UI 판정이 그 자리를 보게 한 뒤 누르고 뗀다
        private static void TapTreasure(Func<int> id)   // 게임 id 는 시험이 돌 때 읽는다 (순서를 짤 땐 아직 없다)
        {
            Vector2 p = default;
            R0926HuntTreasure tr = null;
            Do(() =>
            {
                tr = UnityEngine.Object.FindAnyObjectByType<R0926HuntTreasure>();
                var c = TreasureCollider(id()); var cam = Camera.main;
                if (tr == null || c == null || cam == null) { tr = null; log.Add("  누를 보물 없음"); return; }
                Vector3 s = cam.WorldToScreenPoint(c.bounds.center);
                p = new Vector2(s.x, s.y);
                tr.EditorFeed(true, p);   // 에디터에선 넣은 마우스 이벤트가 기기에 닿지 않는다 — 스와이프 시험처럼 직접 넣는다
            });
            Sleep(0.15f);
            Do(() => { if (tr != null) tr.EditorFeed(false, p); });
            Sleep(0.2f);
        }
        // 추가 창이 열려 있으면 뒤로가기로 닫는다 (상세 → 처음 화면 → 닫힘)
        private static void CloseUpload()
        {
            for (int i = 0; i < 3; i++)
            {
                Do(() => { if (Active("UploadPage")) BackNow(); });
                Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
                Sleep(0.8f);
            }
        }

        // 프로필의 보물찾기 칸 — 프로필 창은 실제 회원(3)으로 열고 보물찾기 기록만 시험 아이디로 (R0926HuntProfile.EditorHostAlias).
        // 시험 주최자: 끝난 게임 A(넷이 찾음 · 셋이 별 5·4·3) + 진행 중 B · 평가가 한 명뿐인 주최자 · 주최한 적 없는 사람
        private const string ProfHost = "hunt_test_phost", ProfHost2 = "hunt_test_phost2", ProfMe = "hunt_test_prater";
        private const string ProfReview = "힌트가 친절해서 금방 찾았어요";
        private static readonly string[] ProfPlayers = { "hunt_test_pr1", "hunt_test_pr2", "hunt_test_pr3", ProfMe };
        private static int profHuntA, profHuntB, profHuntC;
        private static void PlanHuntProfile()
        {
            const string P = "FullProfilePanel/Content/BottomPanel/";
            const string Col = P + "FollowStats/HuntStat0926";
            const string C = P + "HuntSection0926/Scroll/Viewport/Content/";
            Do(() =>
            {
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = ProfMe;
                R0926HuntProfile.EditorHostAlias = ProfHost;
                SeedHostProfile();
                log.Add("  시험 게임 A " + profHuntA + " · B " + profHuntB + " · C " + profHuntC);
            });
            Check(() => profHuntA > 0 && profHuntB > 0 && profHuntC > 0, "개발 서버에 시험 주최자 게임 (끝난 A · 진행 중 B · 평가 한 명뿐인 C)");
            Check(() => Has(DevGet(ProfMe, "/api/hunts/profile/" + ProfHost), "\"hosted\":1", "\"raters\":3", "\"rating\":3.8"),
                "서버: 주최 1 · 3명 평가 → 3.8 (5·4·3 에 3명분 3.5 를 섞음)");
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); ProfileManager.Instance?.ShowProfile("3"); });
            Wait(() => Active(Col), 12f, "프로필 → '주최' 칸");
            Sleep(1.0f);
            Shot("hprof_column");
            Check(() => TextOf(Col + "/Main").Contains("<b>1</b>") && TextOf(Col + "/Main").Contains("★") && TextOf(Col + "/Main").Contains("3.8") && TextOf(Col + "/Label") == "주최",
                "숫자 줄 '주최' 칸: 1 · ★ 3.8");
            Check(() => !Active(P + "HuntSection0926"), "처음엔 접혀 있음");

            // 펼침
            Do(() => Click(Col));
            Sleep(1.5f);
            Shot("hprof_open");
            Check(() => Active(P + "HuntSection0926") && TextOf(C + "Head/Right") == "주최 기록 · 리뷰", "'주최' 칸을 누르면 같은 카드 안에 펼침 · 머리 '주최 기록 · 리뷰'");
            Check(() => TextOf(C + "RecordBox/Nums/N0").Contains("<b>1</b>") && TextOf(C + "RecordBox/Nums/N2").Contains("<b>4</b>") && TextOf(C + "RecordBox/Foot/Left").StartsWith("첫 주최"),
                "주최 기록: 주최 1 · 평균 참가 4 · 첫 주최 날짜");
            Check(() => ActiveChildren(C + "OpenList") == 1 && (FirstActiveChild(C + "OpenList")?.Find("Title")?.GetComponent<Text>()?.text ?? "").Contains("프로필 시험 보물 B")
                        && (FirstActiveChild(C + "OpenList")?.Find("Sub")?.GetComponent<Text>()?.text ?? "").StartsWith("진행 중"),
                "지금 열린 게임 한 줄 (B · 진행 중)");
            Check(() => TextOf(C + "RateBox/Row/Num") == "3.8" && TextOf(C + "RateBox/Row/Count") == "3명 평가", "참여자 평가 3.8 · 3명 평가");
            Check(() => Active(C + "Form") && TextOf(C + "Form/Title").Contains("프로필 시험 보물 A") && TextOf(C + "Form/InputRow/Send/Label") == "남기기",
                "별점 남기기: 찾은 게임(A) · '남기기'");
            Check(() => ReviewRows(C + "ReviewsBox/List", false) == 3 && !Active(C + "ReviewsBox/More"), "참여자 리뷰 셋 · 남의 프로필이라 리뷰마다 '신고' 없음");
            Check(() => Active(C + "Actions/Row/Report") && Active(C + "Actions/Row/Block") && TextOf(C + "Actions/Row/Report/Label") == "주최자 신고",
                "주최자 신고 · 차단 (참가한 적 있음)");
            Check(() => !Active(C + "SuspendBox"), "남의 프로필엔 정지 안내 없음");
            Check(() => CardFits(P, C), "펼친 카드가 화면 안 (위 안전 영역 아래 ~ 도크 위) · 넘치는 만큼 펼침 칸 안 스크롤");
            Do(() => { var sr = Find(P + "HuntSection0926/Scroll")?.GetComponent<ScrollRect>(); if (sr != null) { sr.StopMovement(); sr.verticalNormalizedPosition = 0f; } });
            Sleep(0.6f);
            Shot("hprof_bottom");

            // 리뷰 한 줄 — 키보드 위 입력줄
            Do(() => Mirror(C + "Form/InputRow/ReviewInput", true));
            Sleep(0.5f);
            Shot("hprof_typing");
            Check(() => Active("FullProfilePanel/UploadInputMirror"), "리뷰 칸을 누르면 키보드 위 입력줄");
            Do(() => Mirror(C + "Form/InputRow/ReviewInput", false));
            Sleep(0.6f);
            Check(() => Find(C + "Form/InputRow/ReviewInput")?.GetComponent<InputField>()?.text == ProfReview && TextOf(C + "Form/InputRow/ReviewInput/Count") == ProfReview.Length + "/80",
                "입력줄을 닫으면 리뷰 칸에 · 글자 수 " + ProfReview.Length + "/80");

            // 별 넷 → 남기기
            Do(() => Click(C + "Form/Stars/S4"));
            Sleep(0.3f);
            Check(() => StarsLit(C + "Form/Stars/S") == 4, "별 넷을 누르면 넷까지 금색");
            Do(() => Click(C + "Form/InputRow/Send"));
            Sleep(2.5f);
            Shot("hprof_rated");
            Check(() => Has(DevGet(ProfMe, "/api/hunts/profile/" + ProfHost), "\"my_rating\":4", "\"raters\":4", "\"rating\":3.8") && MyReview(ProfMe, ProfHost) == ProfReview,
                "남기기 → 서버에 별 4 · 리뷰 저장 · 4명 평가 (3.8)");
            Check(() => TextOf(C + "Form/InputRow/Send/Label") == "고치기" && TextOf(C + "RateBox/Row/Count") == "4명 평가" && StarsLit(C + "Form/Stars/S") == 4
                        && Find(C + "Form/InputRow/ReviewInput")?.GetComponent<InputField>()?.text == ProfReview,
                "다시 받은 뒤: '고치기' · 4명 평가 · 별·리뷰 그대로");
            Check(() => ReviewRows(C + "ReviewsBox/List", false) == 3 && Active(C + "ReviewsBox/More") && TextOf(C + "ReviewsBox/More/Label") == "리뷰 1개 더 보기",
                "리뷰 넷 → 셋만 · '리뷰 1개 더 보기'");

            // 주최자 신고
            Do(() => Click(C + "Actions/Row/Report"));
            Sleep(0.8f);
            Shot("hprof_reasons");
            Check(() => Active(C + "ReasonBox") && ActiveChildren(C + "ReasonBox/List") == 5 && (FirstActiveChild(C + "ReasonBox/List")?.Find("Label")?.GetComponent<Text>()?.text ?? "") == "위험한 곳에 숨김",
                "신고 → 사유 다섯 (위험한 곳에 숨김부터 정한 순서)");
            Check(() => InView(C + "ReasonBox", P + "HuntSection0926/Scroll/Viewport"), "사유 칸까지 스크롤");
            Do(() => FirstActiveChild(C + "ReasonBox/List")?.GetComponent<Button>()?.onClick.Invoke());
            Sleep(2.0f);
            Check(() => !Active(C + "ReasonBox") && TextOf(C + "Actions/Row/Report/Label") == "신고함 — 관리자가 확인 중"
                        && Find(C + "Actions/Row/Report")?.GetComponent<Button>()?.interactable == false
                        && Has(DevGet(ProfMe, "/api/hunts/profile/" + ProfHost), "\"reported\":true"),
                "사유를 고르면 서버에 신고 · 버튼은 '신고함 — 관리자가 확인 중'(못 누름)");

            // 차단 — 두 번 눌러야 · 풀기는 한 번
            Do(() => Click(C + "Actions/Row/Block"));
            Sleep(0.4f);
            Check(() => TextOf(C + "Actions/Row/Block/Label") == "한 번 더 누르면 차단" && Has(DevGet(ProfMe, "/api/hunts/profile/" + ProfHost), "\"blocked\":false"),
                "차단 한 번 → '한 번 더 누르면 차단' (아직 안 막음)");
            Do(() => Click(C + "Actions/Row/Block"));
            Sleep(2.0f);
            Check(() => Has(DevGet(ProfMe, "/api/hunts/profile/" + ProfHost), "\"blocked\":true") && TextOf(C + "Actions/Row/Block/Label") == "차단 풀기", "두 번 → 차단 · '차단 풀기'");
            Do(() => Click(C + "Actions/Row/Block"));
            Sleep(2.0f);
            Check(() => Has(DevGet(ProfMe, "/api/hunts/profile/" + ProfHost), "\"blocked\":false") && TextOf(C + "Actions/Row/Block/Label") == "차단", "'차단 풀기' 한 번 → 풀림");

            // 내 프로필 — 주최자 본인
            Back();
            Sleep(1.0f);
            Do(() => { R0926HuntApi.EditorTestUser = ProfHost; ProfileManager.Instance?.ShowProfile("3"); });
            Wait(() => Active(Col), 12f, "주최자 본인 → '주최' 칸");
            Sleep(0.6f);
            Do(() => Click(Col));
            Sleep(1.5f);
            Check(() => TextOf(C + "Head/Right") == "내 주최 기록" && !Active(C + "Form") && !Active(C + "Actions") && !Active(C + "SuspendBox"),
                "내 프로필: '내 주최 기록' · 별점 남기기·신고/차단 없음");
            Check(() => ReviewRows(C + "ReviewsBox/List", true) == 3 && Active(C + "ReviewsBox/More"), "내 프로필 리뷰엔 '신고'");
            Do(() => Click(C + "ReviewsBox/More"));
            Sleep(0.6f);
            Check(() => ReviewRows(C + "ReviewsBox/List", true) == 4 && !Active(C + "ReviewsBox/More"), "'더 보기' → 리뷰 넷 다");
            Shot("hprof_mine");
            Do(() => FirstActiveChild(C + "ReviewsBox/List")?.Find("Report")?.GetComponent<Button>()?.onClick.Invoke());
            Sleep(0.8f);
            Check(() => Active(C + "ReasonBox") && ActiveChildren(C + "ReasonBox/List") == 3 && (FirstActiveChild(C + "ReasonBox/List")?.Find("Label")?.GetComponent<Text>()?.text ?? "") == "욕설·비방",
                "리뷰 신고 → 사유 셋 (욕설·비방부터)");
            Do(() => FirstActiveChild(C + "ReasonBox/List")?.GetComponent<Button>()?.onClick.Invoke());
            Sleep(2.0f);
            Check(() => !Active(C + "ReasonBox") && Has(DevPost(ProfHost, "/api/hunts/report", "{\"target\":\"review\",\"rid\":" + FirstReviewRid(ProfHost, ProfHost) + ",\"reason\":\"abuse\"}"), "\"duplicate\":true"),
                "리뷰 신고가 서버에 (같은 신고를 또 하면 '이미 신고함')");

            // 신고가 쌓여 정지 — 30일 안에 서로 다른 참가자 셋
            Do(() =>
            {
                foreach (var u in new[] { "hunt_test_pr1", "hunt_test_pr2" })
                    DevPost(u, "/api/hunts/report", "{\"target\":\"host\",\"host_id\":\"" + ProfHost + "\",\"reason\":\"no_prize\"}");
            });
            Check(() => Has(DevGet(ProfHost, "/api/hunts/profile/" + ProfHost), "\"suspended\":true"), "참가자 셋이 신고 → 서버: 주최 정지");
            Back();
            Sleep(1.0f);
            Do(() => ProfileManager.Instance?.ShowProfile("3"));
            Wait(() => Active(Col), 12f, "다시 연 내 프로필");
            Sleep(0.6f);
            Do(() => Click(Col));
            Sleep(1.5f);
            Shot("hprof_suspended");
            Check(() => Active(C + "SuspendBox") && TextOf(C + "SuspendBox/Text").Contains("최근 30일 동안 참가자 3명이 신고해") && TextOf(C + "SuspendBox/Text").Contains("새 보물찾기를 열 수 없어요"),
                "정지 안내 (본인에게만): '최근 30일 동안 참가자 3명이 신고해 새 보물찾기를 열 수 없어요'");

            // 평가가 3명이 안 되는 주최자
            Back();
            Sleep(1.0f);
            Do(() => { R0926HuntProfile.EditorHostAlias = ProfHost2; R0926HuntApi.EditorTestUser = ProfMe; ProfileManager.Instance?.ShowProfile("3"); });
            Wait(() => Active(Col), 12f, "평가 한 명뿐인 주최자 → '주최' 칸");
            Sleep(0.6f);
            Check(() => TextOf(Col + "/Main").Contains("<b>1</b>") && TextOf(Col + "/Main").Contains("평가 모으는 중") && !TextOf(Col + "/Main").Contains("★"),
                "평가가 3명 미만 → 별 대신 '평가 모으는 중'");
            Do(() => Click(Col));
            Sleep(1.5f);
            Shot("hprof_collecting");
            Check(() => TextOf(C + "RateBox/Row/Num") == "–" && TextOf(C + "RateBox/Row/Count") == "1명 평가" && !Active(C + "Form") && !Active(C + "Actions/Row/Report") && Active(C + "Actions/Row/Block"),
                "펼침: 평가 '–' · 1명 평가 · 참가 안 한 사람은 남기기·신고 없음 (차단만)");

            // 주최한 적 없는 사람
            Back();
            Sleep(1.0f);
            Do(() => { R0926HuntProfile.EditorHostAlias = "hunt_test_pnobody"; ProfileManager.Instance?.ShowProfile("3"); });
            Sleep(3.5f);
            Shot("hprof_none");
            Check(() =>
            {
                var row = Find(P + "FollowStats")?.GetComponent<HorizontalLayoutGroup>();
                log.Add("  숫자 줄 간격 " + (row != null ? row.spacing.ToString("0") : "-"));
                return Active("FullProfilePanel") && !Active(Col) && !Active(P + "HuntSection0926") && row != null && row.spacing > 30.5f;
            }, "주최한 적 없는 사람은 '주최' 칸·펼침 없음 · 숫자 줄 간격 원래대로");

            // 별점 알림 → 주최자 프로필을 펼친 채 별점 남기기까지
            Back();
            Sleep(1.0f);
            Do(() => { R0926HuntProfile.EditorHostAlias = ProfHost; R0926HuntPush.Open(profHuntA.ToString(), "3", true); });
            Wait(() => Active(P + "HuntSection0926") && Active(C + "Form"), 15f, "별점 알림 → 주최자 프로필 펼침");
            Sleep(1.0f);
            Shot("hprof_push");
            Check(() => Active(C + "Form") && InView(C + "Form", P + "HuntSection0926/Scroll/Viewport") && TextOf(C + "Form/Title").Contains("프로필 시험 보물 A"),
                "별점 알림 → 펼친 채 별점 남기기(그 게임)가 보이게 내려감");
            Do(() => { R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; R0926HuntProfile.EditorHostAlias = null; });
        }

        // 시험 주최자 기록 만들기 — 모두 시험 아이디라 알림은 개발 서버에 기록만 된다
        private static void SeedHostProfile()
        {
            const string spot = "\"lat\":36.660100,\"lon\":126.673000";
            DevPost(ProfHost, "/api/hunts/rules/accept", "{\"version\":1}");
            profHuntA = HuntId(DevPost(ProfHost, "/api/hunts", "{\"title\":\"프로필 시험 보물 A\",\"mode\":\"touch\",\"winners_all\":true,\"radius_km\":5,\"visible_m\":50,"
                                                              + spot + ",\"acc\":5,\"duration_min\":60}"));
            if (profHuntA <= 0) return;
            foreach (var u in ProfPlayers) SeedPlay(u, profHuntA, spot);
            DevPost(ProfHost, "/api/hunts/" + profHuntA + "/end", "{}");
            string[] reviews = { "힌트가 딱 좋았어요", "재밌었는데 조금 멀었어요", "상품 전달이 늦었어요" };
            for (int i = 0; i < 3; i++)
                DevPost(ProfPlayers[i], "/api/hunts/" + profHuntA + "/rate", "{\"stars\":" + (5 - i) + ",\"review\":\"" + reviews[i] + "\",\"device_id\":\"hprof-" + ProfPlayers[i] + "\"}");
            profHuntB = DevCreateHunt(ProfHost, "프로필 시험 보물 B", "36.660400");
            profHuntC = DevCreateHunt(ProfHost2, "프로필 시험 보물 C", "36.660100");
            if (profHuntC <= 0) return;
            SeedPlay(ProfPlayers[0], profHuntC, spot);
            DevPost(ProfHost2, "/api/hunts/" + profHuntC + "/end", "{}");
            DevPost(ProfPlayers[0], "/api/hunts/" + profHuntC + "/rate", "{\"stars\":5,\"review\":\"좋았어요\",\"device_id\":\"hprof-" + ProfPlayers[0] + "\"}");
        }
        private static void SeedPlay(string user, int hunt, string spot)
        {
            DevPost(user, "/api/hunts/" + hunt + "/join", "{" + spot + "}");
            string r = DevPost(user, "/api/hunts/" + hunt + "/claim", "{" + spot + ",\"acc\":5,\"device_id\":\"hprof-" + user + "\"}");
            if (!Has(r, "\"result\":\"found\"")) log.Add("  " + user + " 찾기 실패: " + r);
        }
        private static int HuntId(string reply) => string.IsNullOrEmpty(reply) ? 0 : JsonUtility.FromJson<DevHuntReply>(reply)?.hunt?.id ?? 0;

        [Serializable] private class ProfReply { public ProfMeJ me; public ProfReviewJ[] reviews; }
        [Serializable] private class ProfMeJ { public ProfCanJ[] can_review; }
        [Serializable] private class ProfCanJ { public int hunt_id; public string my_review; }
        [Serializable] private class ProfReviewJ { public int rid; }
        private static ProfReply Prof(string user, string host)
        {
            string r = DevGet(user, "/api/hunts/profile/" + host);
            return string.IsNullOrEmpty(r) ? null : JsonUtility.FromJson<ProfReply>(r);
        }
        private static string MyReview(string user, string host)
        {
            var p = Prof(user, host);
            if (p?.me?.can_review != null) foreach (var c in p.me.can_review) if (c.hunt_id == profHuntA) return c.my_review;
            return null;
        }
        private static int FirstReviewRid(string user, string host)
        {
            var p = Prof(user, host);
            return p?.reviews != null && p.reviews.Length > 0 ? p.reviews[0].rid : 0;
        }
        private static string DevGet(string user, string path)
        {
            using (var wc = new System.Net.WebClient())
            {
                wc.Encoding = System.Text.Encoding.UTF8;
                wc.Headers.Add("X-Test-User", user);
                try { return wc.DownloadString("http://127.0.0.1:5099" + path); }
                catch (System.Net.WebException e) { log.Add("  개발 서버 " + path + " 실패: " + e.Message); return ""; }
            }
        }
        // 서버 JSON 에 이 조각들이 다 있나 (공백 무시 · 한글은 \u 로 오니 영문 키·값만)
        private static bool Has(string json, params string[] parts)
        {
            if (string.IsNullOrEmpty(json)) return false;
            var s = new System.Text.StringBuilder();
            foreach (char ch in json) if (!char.IsWhiteSpace(ch)) s.Append(ch);
            string flat = s.ToString();
            foreach (var p in parts) if (!flat.Contains(p)) { log.Add("  응답에 없음 " + p); return false; }
            return true;
        }
        private static string TextOf(string path) => Find(path)?.GetComponent<Text>()?.text ?? "";
        // 보이는 리뷰 줄 수 — 줄마다 '신고'가 report 와 같아야 (아니면 -1)
        private static int ReviewRows(string listPath, bool report)
        {
            var l = Find(listPath);
            if (l == null) return -1;
            int n = 0;
            foreach (Transform r in l)
            {
                if (!r.gameObject.activeSelf) continue;
                var rep = r.Find("Report");
                if (rep == null || rep.gameObject.activeSelf != report) return -1;
                n++;
            }
            return n;
        }
        private static int StarsLit(string prefix)
        {
            int n = 0;
            for (int i = 1; i <= 5; i++)
            {
                var t = Find(prefix + i + "/Glyph")?.GetComponent<Text>();
                if (t != null && t.color.a > 0.5f) n++;
            }
            return n;
        }
        // 키보드 위 입력줄 — 열고 쓰기(open) · 닫기 (에디터엔 키보드가 없어 닫기를 누른 것으로)
        private static void Mirror(string srcPath, bool open)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(UploadInputMirror);
            var m = Find("FullProfilePanel")?.GetComponent<UploadInputMirror>();
            var src = Find(srcPath)?.GetComponent<InputField>();
            var mi = m != null ? t.GetField("mirrorInput", F).GetValue(m) as InputField : null;
            if (m == null || src == null || mi == null) { log.Add("  리뷰 입력줄 없음 (" + (m == null ? "거울" : src == null ? "칸" : "입력줄") + ")"); return; }
            if (open) { t.GetMethod("ActivateMirrorFor", F).Invoke(m, new object[] { src }); mi.text = ProfReview; }
            else t.GetMethod("OnCloseClicked", F).Invoke(m, null);
        }
        // 이 칸의 위 끝이 보이는 영역 안에
        private static bool InView(string path, string viewPath)
        {
            var t = Find(path) as RectTransform;
            var v = Find(viewPath) as RectTransform;
            if (t == null || v == null) return false;
            var a = new Vector3[4];
            var b = new Vector3[4];
            t.GetWorldCorners(a);
            v.GetWorldCorners(b);
            return a[1].y <= b[1].y + 1f && a[1].y >= b[0].y;
        }
        // 카드가 위 안전 영역 아래 ~ 도크 위 (화면 좌표)
        private static bool CardFits(string panelPath, string contentPath)
        {
            var lay = Find("FullProfilePanel/Content")?.GetComponent<R0926ProfileLayout>();
            var card = lay != null ? typeof(R0926ProfileLayout).GetField("card", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(lay) as RectTransform : null;
            if (card == null) { log.Add("  카드 없음"); return false; }
            var canvas = card.GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var cs = new Vector3[4];
            card.GetWorldCorners(cs);
            float top = RectTransformUtility.WorldToScreenPoint(cam, cs[1]).y, bottom = RectTransformUtility.WorldToScreenPoint(cam, cs[0]).y;
            float floor = Screen.safeArea.yMin;
            var dock = Find("FullProfilePanel/DockMirror0926") as RectTransform;
            if (dock != null && dock.gameObject.activeInHierarchy)
            {
                dock.GetWorldCorners(cs);
                floor = Mathf.Max(floor, RectTransformUtility.WorldToScreenPoint(cam, cs[1]).y);
            }
            var sec = Find(panelPath + "HuntSection0926") as RectTransform;
            var ct = Find(contentPath.TrimEnd('/')) as RectTransform;
            log.Add("  카드 위 " + top.ToString("0") + " / 안전 영역 위 " + Screen.safeArea.yMax.ToString("0") + " · 카드 아래 " + bottom.ToString("0") + " / 도크 위 " + floor.ToString("0")
                    + " · 펼침 칸 " + (sec != null ? sec.rect.height.ToString("0") : "-") + " / 내용 " + (ct != null ? LayoutUtility.GetPreferredHeight(ct).ToString("0") : "-")
                    + " (화면 " + Screen.width + "x" + Screen.height + ")");
            return top <= Screen.safeArea.yMax + 1f && bottom >= floor - 1f;
        }

        // 스크롤 목록에서 그 칸이 맨 위에 오게
        private static void ScrollToChild(string path)
        {
            var t = Find(path) as RectTransform;
            var sr = t != null ? t.GetComponentInParent<ScrollRect>() : null;
            if (t == null || sr == null) { log.Add("  스크롤 못 함 " + path); return; }
            Canvas.ForceUpdateCanvases();
            float top = -t.anchoredPosition.y - t.rect.height * (1f - t.pivot.y);
            sr.StopMovement();
            sr.content.anchoredPosition = new Vector2(sr.content.anchoredPosition.x, Mathf.Max(0f, top - 20f));
        }

        private static bool SwitchOn(string rowPath) => Find(rowPath + "/Switch")?.GetComponent<Image>()?.sprite?.name == "r0926_sw_on";

        // 시험 서버(127.0.0.1:5099)에 실제로 저장된 값 (에디터에선 SimpleJSON 이 다른 dll 과 이름이 겹쳐 JsonUtility 로)
        [Serializable] private class PrefsReply { public PrefsVals prefs; }
        [Serializable] private class PrefsVals { public bool enabled, notify_new, notify_near; }

        private static bool ServerPref(string user, string key)
        {
            using (var wc = new System.Net.WebClient())
            {
                wc.Encoding = System.Text.Encoding.UTF8;
                wc.Headers.Add("X-Test-User", user);
                var p = JsonUtility.FromJson<PrefsReply>(wc.DownloadString("http://127.0.0.1:5099/api/hunts/prefs")).prefs;
                return key == "enabled" ? p.enabled : key == "notify_new" ? p.notify_new : p.notify_near;
            }
        }

        // 로그인 — 앱의 로그인 버튼 → (브라우저 대신) '앱으로 돌아가기' 링크 복사 → 에디터가 받아 로그인 → 보물찾기를 그 로그인으로.
        // 로그인 확인과 보물찾기는 시험 서버(127.0.0.1:5099)가 받는다 — 토큰 hunt-dev:<이름> → 사용자 hunt_test_<이름>.
        // 시작 전 로그인 저장값은 덜어 두었다가(로그아웃 상태로 시작) 끝나면 되돌린다. 클립보드도 되돌린다.
        private static readonly string[] LoginKeys = { LoginManager.AUTH_TOKEN_KEY, "SavedUserId", "SavedUsername", "LoginProvider" };
        private const string LoginBackupKey = "Redesign0926PlayTest.LoginBackup";
        private const string LoginReadyKey = "Redesign0926PlayTest.LoginReady";
        private static string savedClipboard;
        private static void StashLogin()
        {
            var parts = new List<string>();
            foreach (var k in LoginKeys) parts.Add(PlayerPrefs.HasKey(k) ? k + "\u0001" + PlayerPrefs.GetString(k, "") : k);
            SessionState.SetString(LoginBackupKey, string.Join("\u0002", parts));
            foreach (var k in LoginKeys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }
        private static void RestoreLogin()
        {
            SessionState.EraseBool(LoginReadyKey);
            string s = SessionState.GetString(LoginBackupKey, "");
            SessionState.EraseString(LoginBackupKey);
            foreach (var k in LoginKeys) PlayerPrefs.DeleteKey(k);
            foreach (var part in s.Split('\u0002'))
            {
                int i = part.IndexOf('\u0001');
                if (i > 0) PlayerPrefs.SetString(part.Substring(0, i), part.Substring(i + 1));
            }
            PlayerPrefs.Save();
        }
        // 시험 서버만 받는 가짜 로그인 — 비밀 값이 아니다. 운영 서버는 이 형식을 받지 않는다
        private const string LocalTestLogin = "hunt-dev:";
        private static void CopyLink(string who) => EditorGUIUtility.systemCopyBuffer = "woopang://auth?token=" + LocalTestLogin + who;
        private static void PlanLogin()
        {
            const string H = HuntCardPath;
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                SessionState.SetBool(LoginReadyKey, true);
                savedClipboard = EditorGUIUtility.systemCopyBuffer;
                LoginManager.EditorAuthBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = null;
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Check(() => LoginManager.Instance != null && !LoginManager.Instance.IsLoggedIn, "시작: 로그아웃 상태");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude)?.SwitchToPanel(2));
            Sleep(2.0f);
            Shot("login_hub_out");
            Check(() => Active(H + "Hub/Viewport/Content/LoginBox"), "로그인 전 → 보물찾기 처음 화면에 로그인 안내");
            Do(() => Click(H + "Hub/Viewport/Content/LoginBox/LoginButton"));
            Sleep(0.8f);
            Shot("login_prompt");
            Check(() => Active("LoginPromptPanel"), "로그인 버튼 → 로그인 안내 창");
            // 안내 창의 '로그인'(웹 로그인)이 하는 일 중 브라우저 열기만 뺐다 — 시험이 PC 에 브라우저를 띄우지 않게
            Do(() => { LoginManager.EditorBeforeWebLogin?.Invoke(); LoginManager.Instance.CloseLoginPrompt(); });
            Check(() => EditorLoginWatcher.Waiting && !Active("LoginPromptPanel"), "웹 로그인 열기 → 안내 창 닫힘 · 에디터가 복사한 링크를 기다림");
            Sleep(1.5f);
            Check(() => !LoginManager.Instance.IsLoggedIn, "링크를 복사하기 전엔 로그인 안 됨");
            Do(() => CopyLink("host"));   // 브라우저의 '앱으로 돌아가기' → 링크 주소 복사와 같다
            Wait(() => LoginManager.Instance.IsLoggedIn, 10f, "복사한 링크 → 로그인");
            Sleep(2.0f);
            Shot("login_hub_in");
            Do(() => log.Add("  로그인: " + LoginManager.Instance.CurrentUsername + " · 저장 " + PlayerPrefs.GetString("LoginProvider", "-")));
            Check(() => LoginManager.Instance.CurrentUsername == "시험_host", "시험 서버가 확인한 사용자로 로그인");
            Check(() => string.IsNullOrEmpty(EditorGUIUtility.systemCopyBuffer), "로그인 뒤 클립보드에서 토큰 지움");
            Check(() => !Active(H + "Hub/Viewport/Content/LoginBox"), "로그인되면 보물찾기 처음 화면이 저절로 새로 — 로그인 안내 사라짐");
            Do(() => Click(H + "Hub/Viewport/Content/Hero"));
            Sleep(2.0f);
            Check(() => Active(H + "Rules"), "그 로그인으로 처음 주최 → 주최 수칙");
            Do(() => Click(H + "Rules/Viewport/Content/Agree"));
            Sleep(0.3f);
            Do(() => Click(H + "Rules/Viewport/Content/AgreeButton"));
            Sleep(1.5f);
            Check(() => Active(H + "Form"), "동의 → 숨기기 양식");
            Do(() => { var f = Find(H + "Form/Viewport/Content/TitleInput")?.GetComponent<InputField>(); if (f != null) f.text = "로그인 시험 보물"; });
            Do(() => { var f = Find(H + "Form/Viewport/Content/HintInput")?.GetComponent<InputField>(); if (f != null) f.text = "충남도청 앞 분수대 근처"; });
            Do(() => Click(H + "Form/Viewport/Content/Submit"));
            Sleep(2.5f);
            Shot("login_detail_host");
            Check(() => Active(H + "Detail") && !Active(H + "Detail/Viewport/Content/Open"), "숨기기 → 상세(주최자 · '보물 열기' 없음)");
            // 다른 사람으로 다시 로그인 — 같은 길(웹 로그인 → 링크 복사)
            Do(() => LoginManager.EditorBeforeWebLogin?.Invoke());
            Do(() => CopyLink("player"));
            Wait(() => LoginManager.Instance.CurrentUsername == "시험_player", 10f, "두 번째 사람 로그인");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926HuntCard>(FindObjectsInactive.Include)?.ShowHub());
            Sleep(2.5f);
            Shot("login_hub_player");
            Check(() => ActiveChildren(H + "Hub/Viewport/Content/NearList") >= 1, "두 번째 사람 근처 목록에 그 게임");
            Do(() => FirstActiveChild(H + "Hub/Viewport/Content/NearList")?.GetComponent<Button>()?.onClick.Invoke());
            Sleep(2.0f);
            Do(() => Click(H + "Detail/Viewport/Content/Join"));
            Sleep(11.0f);   // 참가 → 상세 다시 → 근접 확인(8.5초마다)
            Check(() => Active(H + "Detail/Viewport/Content/Open"), "보이는 거리 안 → '보물 열기'");
            Do(() => Click(H + "Detail/Viewport/Content/Open"));
            Sleep(3.0f);
            Shot("login_found");
            Do(() => log.Add("  내 상태: " + (Find(H + "Detail/Viewport/Content/Mine")?.GetComponent<Text>()?.text ?? "-")));
            Check(() => (Find(H + "Detail/Viewport/Content/Mine")?.GetComponent<Text>()?.text ?? "").Contains("찾았어요"), "보물 열기 → 찾음");
            Do(() =>
            {
                R0926HuntApi.EditorBase = null;
                LoginManager.EditorAuthBase = null;
                EditorGUIUtility.systemCopyBuffer = savedClipboard ?? "";
                savedClipboard = null;
            });
        }

        // 요청 대조 — 날씨판 지역 이름(에디터 위치 그대로) · 목록/지도/설정 · 지도 최대 확대 · 추가창과 크기 · 첫 안내 버튼 소리
        private static void PlanAudit()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() => AimCamera(-60f, 0f));
            Wait(() =>
            {
                var sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                var f = typeof(R0926SkyWeather).GetField("boardShowing", F);
                return sky != null && f != null && (bool)f.GetValue(sky);
            }, 40f, "날씨판 뜸");
            Sleep(4.0f);   // 주소(지역) 받기
            Do(() =>
            {
                var sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                var rt = typeof(R0926SkyWeather).GetField("regionText", F)?.GetValue(sky) as Text;
                log.Add("  지역: LocationManager.ShortRegion='" + (LocationManager.ShortRegion ?? "(없음)") + "' · 판 글='" + (rt != null ? rt.text : "(칸 없음)") + "' 보임=" + (rt != null && rt.enabled && rt.gameObject.activeInHierarchy));
            });
            Shot("audit_sky");
            Do(() => AimCamera(0f, 0f));
            Sleep(1.0f);

            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Shot("audit_list");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926SheetModes>(FindObjectsInactive.Include)?.Show(1));
            Sleep(1.2f);
            Shot("audit_map");
            Do(() =>
            {
                var map = UnityEngine.Object.FindAnyObjectByType<R0926FriendsMap>(FindObjectsInactive.Include);
                var maxF = typeof(R0926FriendsMap).GetProperty("MaxZoom", F);
                var tiles = typeof(R0926FriendsMap).GetField("tiles", F)?.GetValue(map) as R0926MapTiles;
                log.Add("  지도: 최대 확대 " + (maxF != null ? maxF.GetValue(map) : "?") + "배 · 서버 타일 " + (tiles != null && tiles.Available ? "켜짐" : "꺼짐"));
                map?.EditorPreview(true, maxF != null ? (float)maxF.GetValue(map) : 200f, 37.5665f, 126.978f);
            });
            Sleep(1.5f);
            Shot("audit_map_max");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926SheetModes>(FindObjectsInactive.Include)?.Show(2));
            Sleep(1.2f);
            Shot("audit_settings");
            Do(() =>
            {
                var sp = UnityEngine.Object.FindAnyObjectByType<R0926SettingsPanel>(FindObjectsInactive.Include);
                var sr = sp != null ? sp.GetComponent<ScrollRect>() : null;
                if (sr != null) sr.verticalNormalizedPosition = 0f;
            });
            Sleep(0.8f);
            Shot("audit_settings_bottom");
            Do(() =>
            {
                var ls = Find("ListPanel/Sheet0926") as RectTransform;
                log.Add("  목록 시트 높이 " + (ls != null ? ls.rect.height.ToString("0") : "?"));
            });
            Back();
            Sleep(1.0f);
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Shot("audit_upload");
            Do(() =>
            {
                var us = Find("UploadPage/UploadSheet0926") as RectTransform;
                log.Add("  추가 시트 높이 " + (us != null ? us.rect.height.ToString("0") : "?"));
            });
            Back();
            Sleep(1.0f);

            Do(() =>
            {
                var ov = UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include);
                var clip = typeof(R0926GuideOverlay).GetField("tapSound", F)?.GetValue(ov) as AudioClip;
                var dock = Find("Dock0926/List_Button")?.GetComponent<UITouchForwarder>();
                var dclip = dock != null ? new UnityEditor.SerializedObject(dock).FindProperty("customSound").objectReferenceValue as AudioClip : null;
                log.Add("  첫 안내 버튼 소리 '" + (clip != null ? clip.name : "(없음)") + "' · 도크 버튼 소리 '" + (dclip != null ? dclip.name : "(없음)") + "'");
                UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include)?.ForceShowGuide();
            });
            Sleep(2.0f);
            Shot("audit_guide");
        }

        // 앱을 켜고 시작화면이 걷히자마자 하늘을 비추면 날씨판이 몇 초 만에 뜨는지 (저장된 날씨 없이 — 처음 설치한 것처럼)
        private static void PlanSkyFast()
        {
            double t0 = 0, tSplash = 0;
            Do(() =>
            {
                t0 = EditorApplication.timeSinceStartup;
                foreach (var k in new[] { "SkyWeather_json", "SkyWeather_at", "SkyWeather_lat", "SkyWeather_lon" }) PlayerPrefs.DeleteKey(k);
            });
            Wait(() => !BootOverlay.Showing, 30f, "시작화면 끝");
            Do(() => { tSplash = EditorApplication.timeSinceStartup; AimCamera(-45f, 0f); });
            // 하늘 칩은 노치 아래 상태 알약과 같은 자리 — 알약이 보이는 동안(칩이 사라질 0.6초를 빼고) 칩이 떠 있으면 겹친 것
            const System.Reflection.BindingFlags NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            bool overlap = false, sawPill = false;
            double pillT = -1;
            Func<CanvasGroup> chip = () =>
            {
                var sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                return sky != null ? typeof(R0926SkyWeather).GetField("chipGroup", NP)?.GetValue(sky) as CanvasGroup : null;
            };
            Action watch = () =>
            {
                var g = chip();
                if (g == null) return;
                double now = EditorApplication.timeSinceStartup;
                bool pill = R0926StatusPill.AnyShowing;
                if (!pill) pillT = -1;
                else { sawPill = true; if (pillT < 0) pillT = now; }
                if (pill && now - pillT > 0.6 && now - pillT < 30 && g.alpha > 0.5f) overlap = true;
            };
            Wait(() =>
            {
                watch();
                var sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                var f = typeof(R0926SkyWeather).GetField("boardShowing", NP);
                return sky != null && f != null && (bool)f.GetValue(sky);
            }, 40f, "날씨판 뜸");
            Do(() => log.Add("  시작화면 끝에서 " + (EditorApplication.timeSinceStartup - tSplash).ToString("0.0") + "초 · 플레이 시작에서 " + (EditorApplication.timeSinceStartup - t0).ToString("0.0") + "초"));
            Wait(() => { watch(); var g = chip(); return g != null && g.alpha > 0.9f && !R0926StatusPill.AnyShowing; }, 40f, "알약이 걷힌 뒤 하늘 칩");
            Do(() => log.Add("  상태 알약 " + (sawPill ? "보였다가 걷힘" : "안 보임")));
            Check(() => !overlap, "하늘 칩이 상태 알약과 겹치지 않음");
            Sleep(1.2f);
            Shot("skyfast");
        }

        // 키보드 위 입력줄 → 원래 칸 — 쓰는 동안 · 닫은 뒤 원래 칸에 '보이는' 글자까지 확인 (모든 입력칸)
        private static void PlanInputSync()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(0.5f);
            Do(() => { var up = Find("UploadPage"); if (up != null) up.gameObject.SetActive(true); var fp = Find("Fixpage"); if (fp != null) fp.gameObject.SetActive(true); });
            Sleep(0.6f);
            Do(() =>
            {
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var t = typeof(UploadInputMirror);
                foreach (var m in UnityEngine.Object.FindObjectsByType<UploadInputMirror>(FindObjectsInactive.Include))
                {
                    var mi = t.GetField("mirrorInput", F).GetValue(m) as InputField;
                    var srcs = t.GetField("sourceInputs", F).GetValue(m) as InputField[];
                    if (mi == null || srcs == null) { log.Add("  " + m.name + ": 연결 없음"); continue; }
                    foreach (var src in srcs)
                    {
                        if (src == null) continue;
                        // 꺼진 칸·꺼진 화면(보물찾기 양식·상세 등) — 실제로 쓸 땐 보이는 화면이니 위까지 켜 두고 본다 (인스타그램 칸은 스위치를 켜야 보인다)
                        // 바깥(화면)부터 켠다 — 화면이 처음 켜질 때 Awake 가 안쪽 칸을 끄기도 한다 (프로필의 보물찾기 펼침 칸)
                        var turnedOn = new List<GameObject>();
                        for (var a = src.transform; a != null; a = a.parent)
                            if (!a.gameObject.activeSelf) turnedOn.Insert(0, a.gameObject);
                        foreach (var g in turnedOn) g.SetActive(true);
                        // 막 켠 화면은 레이아웃이 아직 0 크기라 칸 글자가 비어 그려진다 — 실제로는 화면이 먼저 보인 뒤에 칸을 누른다
                        if (turnedOn.Count > 0) Canvas.ForceUpdateCanvases();
                        src.text = "";
                        t.GetMethod("ActivateMirrorFor", F).Invoke(m, new object[] { src });
                        mi.text = "우팡 테스트";
                        string live = src.textComponent != null ? src.textComponent.text : "-";
                        t.GetMethod("OnCloseClicked", F).Invoke(m, null);
                        string after = src.textComponent != null ? src.textComponent.text : "-";
                        bool ok = src.text == "우팡 테스트" && after == "우팡 테스트";
                        log.Add((ok && live == "우팡 테스트" ? "PASS " : "FAIL ") + m.name + "/" + src.transform.parent.parent.name + "/" + src.name
                                + " · 쓰는 중 보임='" + live + "' · 닫은 뒤 값='" + src.text + "' 보임='" + after + "'");
                        src.text = "";
                        for (int k = turnedOn.Count - 1; k >= 0; k--) turnedOn[k].SetActive(false);
                    }
                }
            });
            Do(() => { var fp = Find("Fixpage"); if (fp != null) fp.gameObject.SetActive(false); });
            // 3D모델 탭에서 이름을 쓰고 닫아도 3D모델 탭에 머무는지 (예전엔 '장소'로 넘어갔다)
            Do(() =>
            {
                var sw = UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude);
                if (sw == null) { log.Add("  넘기기 없음"); return; }
                sw.SwitchToPanel(1);
            });
            Sleep(0.6f);
            Do(() =>
            {
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var t = typeof(UploadInputMirror);
                var up = Find("UploadPage");
                var m = up != null ? up.GetComponent<UploadInputMirror>() : null;
                var src = Find("UploadPage/Clip0926/UploadSheet0926/ModelUploadPage/Panel/NameInput")?.GetComponent<InputField>();
                var mi = m != null ? t.GetField("mirrorInput", F).GetValue(m) as InputField : null;
                if (m == null || src == null || mi == null) { log.Add("  3D 이름칸 없음"); return; }
                t.GetMethod("ActivateMirrorFor", F).Invoke(m, new object[] { src });
                mi.text = "강아지";
                t.GetMethod("OnCloseClicked", F).Invoke(m, null);
            });
            Sleep(0.8f);
            Check(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude)?.GetCurrentPanel() == 1, "3D모델에 이름을 쓰고 닫아도 3D모델 탭에 머문다");
            Do(() => { var src = Find("UploadPage/Clip0926/UploadSheet0926/ModelUploadPage/Panel/NameInput")?.GetComponent<InputField>(); if (src != null) src.text = ""; });
        }

        // 시작화면이 도는 동안 무거운 프레임과 그 원인 (프로파일러는 플레이 직전에 켜 둠)
        private static void PlanSplashProf()
        {
            Wait(() => { Redesign0926SplashProf.Analyze(); return !BootOverlay.Showing; }, 30f, "시작화면 끝");
            Sleep(0.5f);
            Do(() => { Redesign0926SplashProf.Analyze(); Redesign0926SplashProf.Stop(); Redesign0926SplashProf.Report(log); });
        }

        // 스토어용 하늘 날씨 — 날씨판만 또렷하게 (인디케이터·주소줄은 숨김: 주소줄엔 에디터 위치의 정확한 좌표가 찍힌다)
        private static void PlanSky()
        {
            Do(() => { Backdrop("bg_sky.png"); DeviceFov(); });
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(4f);
            Do(() =>
            {
                foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude)) t.gameObject.SetActive(false);
                HideAddressLine();
                InjectWeather();
                AimCamera(-45f, 0f);
            });
            Sleep(4.5f);
            Shot("sky45c");
            Do(() => AimCamera(-52f, 0f));
            Sleep(2.0f);
            Shot("sky52c");
        }

        // 스토어용 카메라 화면 — 장소 박스가 모인 쪽을 겨눈다 (개인 장소·주소줄은 숨김)
        private static void PlanAr()
        {
            Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(6f);
            Do(() => { HidePrivatePlaces(); HideAddressLine(); });
            Do(() => AimAt(new[] { "구수한농장", "뒷산고봉", "뒷산고봉무덤" }, 0f));
            Sleep(1.5f);
            Shot("ar_west");
            Do(() => AimAt(new[] { "구수한농장", "우팡이연못", "뒷산고봉" }, -4f));
            Sleep(1.5f);
            Shot("ar_west2");
            Do(() => AimAt(new[] { "충청남도교육청기록원", "시바견 (Quaternius)" }, 0f));
            Sleep(1.5f);
            Shot("ar_north");
        }

        private static void AimAt(string[] names, float pitchOffset)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude))
                if (Array.IndexOf(names, t.PlaceName) >= 0) { sum += (t.transform.position - cam.transform.position).normalized; n++; }
            if (n == 0) { log.Add("  대상 없음 " + string.Join(",", names)); return; }
            foreach (var bh in cam.GetComponents<Behaviour>()) if (bh.GetType().Name.Contains("PoseDriver")) bh.enabled = false;
            var rot = Quaternion.LookRotation((sum / n).normalized, Vector3.up);
            cam.transform.rotation = rot * Quaternion.Euler(pitchOffset, 0f, 0f);
            log.Add("  겨눔 " + n + "곳 (" + string.Join(", ", names) + ")");
        }

        private static void HideAddressLine()
        {
            var p = Find("LocationManagerPanel");
            if (p == null) { log.Add("  주소줄 없음"); return; }
            p.gameObject.SetActive(false);   // 투명도는 '창이 열리면 숨기는' 장치가 매 프레임 되돌린다 — 촬영 때만 끈다
        }

        private static void DeviceFov()
        {
            var cam = Camera.main;
            if (cam == null) return;
            log.Add("  카메라 시야각 " + cam.fieldOfView.ToString("0.0") + " → 66 (아이폰 세로 AR)");
            cam.fieldOfView = 66f;
        }

        // 스토어 화면에 개인 장소 이름이 찍히지 않게 — 촬영 때만 끈다
        private static void HidePrivatePlaces()
        {
            var hide = new HashSet<string> { "집", "엄빠집", "우팡이똥샷", "우팡이똥꼬샷" };
            int n = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude))
                if (hide.Contains(t.PlaceName?.Trim() ?? "")) { t.gameObject.SetActive(false); n++; }
            log.Add("  개인 장소 숨김 " + n);
        }

        private static void LogTargets()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var list = new List<(float d, string s)>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude))
            {
                Vector3 v = t.transform.position - cam.transform.position;
                float bearing = Mathf.Repeat(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, 360f);
                list.Add((v.magnitude, t.PlaceName + " " + v.magnitude.ToString("0") + "m @" + bearing.ToString("0") + "°"));
            }
            list.Sort((x, y) => x.d.CompareTo(y.d));
            for (int i = 0; i < Mathf.Min(14, list.Count); i++) log.Add("  · " + list[i].s);
        }

        private static void InjectWeather()
        {
            var sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
            if (sky == null) { log.Add("  날씨판 없음"); return; }
            string json;
            using (var wc = new System.Net.WebClient()) { wc.Encoding = System.Text.Encoding.UTF8; json = wc.DownloadString(ApiConfig.MAIN_SERVER + "/api/weather?lat=37.5759&lon=126.9768"); }
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var wt = typeof(R0926SkyWeather).GetNestedType("Weather", System.Reflection.BindingFlags.NonPublic);
            var w = JsonUtility.FromJson(json, wt);
            typeof(R0926SkyWeather).GetMethod("Show", F).Invoke(sky, new[] { w });
            typeof(R0926SkyWeather).GetField("hasData", F).SetValue(sky, true);
            typeof(R0926SkyWeather).GetField("nextFetch", F).SetValue(sky, float.MaxValue);
            sky.EditorRefreshRegion();
            log.Add("  날씨 넣음: " + json.Substring(0, Math.Min(160, json.Length)));
        }

        // 업데이트 안내 — 일반 · 강제 · 버전 모를 때(안드로이드) (강제는 스토어로 넘어가지 않게 카드만 그린다)
        private static void PlanUpdate()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1f);
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            Do(() =>
            {
                var c = UnityEngine.Object.FindAnyObjectByType<AutoUpdateChecker>(FindObjectsInactive.Include);
                if (c == null) { log.Add("  검사기 없음"); return; }
                c.StopAllCoroutines();   // 15초 뒤 실제 확인이 끼어들지 않게
                typeof(AutoUpdateChecker).GetField("latestVersion", F).SetValue(c, "1.2.54");
                typeof(AutoUpdateChecker).GetMethod("ShowNormalUpdatePanel", F).Invoke(c, null);
            });
            Sleep(0.8f);
            Shot("upd_normal");
            Do(() =>
            {
                var c = UnityEngine.Object.FindAnyObjectByType<AutoUpdateChecker>(FindObjectsInactive.Include);
                var card = UnityEngine.Object.FindAnyObjectByType<R0926UpdateCard>(FindObjectsInactive.Include);
                if (c == null || card == null) { log.Add("  카드 없음"); return; }
                foreach (var n in new[] { "updateButton", "cancelButton" })
                    (typeof(AutoUpdateChecker).GetField(n, F).GetValue(c) as Button)?.gameObject.SetActive(false);
                card.ShowForce(Application.version, "1.2.54", 0.6f);
            });
            Sleep(0.6f);
            Shot("upd_force");
            // 안드로이드 — Play 는 새 버전 이름을 주지 않는다: 버전 알약 없이 글만
            Do(() =>
            {
                var c = UnityEngine.Object.FindAnyObjectByType<AutoUpdateChecker>(FindObjectsInactive.Include);
                if (c == null) return;
                typeof(AutoUpdateChecker).GetField("latestVersion", F).SetValue(c, null);
                typeof(AutoUpdateChecker).GetMethod("ShowNormalUpdatePanel", F).Invoke(c, null);
            });
            Sleep(0.6f);
            Shot("upd_noversion");
            Check(() =>
            {
                var c = UnityEngine.Object.FindAnyObjectByType<AutoUpdateChecker>(FindObjectsInactive.Include);
                var card = UnityEngine.Object.FindAnyObjectByType<R0926UpdateCard>(FindObjectsInactive.Include);
                if (c == null || card == null) return false;
                var pill = typeof(R0926UpdateCard).GetField("versionPill", F).GetValue(card) as RectTransform;
                var b1 = typeof(AutoUpdateChecker).GetField("updateButton", F).GetValue(c) as Button;
                var b2 = typeof(AutoUpdateChecker).GetField("cancelButton", F).GetValue(c) as Button;
                return pill != null && !pill.gameObject.activeSelf
                       && b1 != null && b1.gameObject.activeSelf && b2 != null && b2.gameObject.activeSelf;
            }, "버전 모를 때(안드로이드): 버전 알약 없이 '업데이트 · 나중에'");
        }

        private static void PlanStore2()
        {
            Do(() => Backdrop("bg_city.png"));
            Sleep(1.3f);
            Shot("s0_splash_a");
            Sleep(1.4f);
            Shot("s0_splash_b");
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(6f);
            Do(() => { HidePrivatePlaces(); HideAddressLine(); });   // 주소줄엔 에디터 위치가 찍힌다
            for (int yaw = 0; yaw < 360; yaw += 45)
            {
                int y = yaw;
                Do(() => AimCamera(-4f, y));
                Sleep(1.6f);
                Shot("s1_yaw" + y.ToString("000"));
            }
            Do(() =>
            {
                var sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                if (sky == null) { log.Add("  날씨판 없음"); return; }
                string json;
                using (var wc = new System.Net.WebClient()) { wc.Encoding = System.Text.Encoding.UTF8; json = wc.DownloadString(ApiConfig.MAIN_SERVER + "/api/weather?lat=37.5759&lon=126.9768"); }
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var wt = typeof(R0926SkyWeather).GetNestedType("Weather", System.Reflection.BindingFlags.NonPublic);
                var w = JsonUtility.FromJson(json, wt);
                typeof(R0926SkyWeather).GetMethod("Show", F).Invoke(sky, new[] { w });
                typeof(R0926SkyWeather).GetField("hasData", F).SetValue(sky, true);
                typeof(R0926SkyWeather).GetField("nextFetch", F).SetValue(sky, float.MaxValue);
                sky.EditorRefreshRegion();
                log.Add("  날씨 넣음: " + json.Substring(0, Math.Min(120, json.Length)));
                Backdrop("bg_sky.png");
                AimCamera(-62f, 0f);
            });
            Sleep(4.5f);
            Shot("s2_sky");
        }

        private static void AimCamera(float pitch, float yaw)
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var bh in cam.GetComponents<Behaviour>()) if (bh.GetType().Name.Contains("PoseDriver")) bh.enabled = false;
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private static void Backdrop(string file)
        {
            var cam = Camera.main;
            if (cam == null || string.IsNullOrEmpty(storeSrc)) { log.Add("  배경 못 깖"); return; }
            if (backdrop == null)
            {
                var go = new GameObject("StoreBackdrop0926", typeof(RectTransform), typeof(Canvas));
                var c = go.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.farClipPlane * 0.9f;
                c.sortingOrder = -1000;
                var img = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
                img.transform.SetParent(go.transform, false);
                var rt = (RectTransform)img.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                backdrop = img.GetComponent<RawImage>();
                backdrop.raycastTarget = false;
            }
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(Path.Combine(storeSrc, file)));
            backdrop.texture = tex;
        }

        // 장소 수정 화면(Fixpage)에도 하나 붙어 있는데 연결이 비어 있다 — 입력줄이 연결된 것만
        private static UploadInputMirror WiredMirror()
        {
            var f = typeof(UploadInputMirror).GetField("mirrorPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var m in UnityEngine.Object.FindObjectsByType<UploadInputMirror>(FindObjectsInactive.Exclude))
                if (f != null && f.GetValue(m) as GameObject != null) return m;
            return null;
        }

        private static string MirrorAlpha()
        {
            var m = WiredMirror();
            if (m == null) return "-";
            var f = typeof(UploadInputMirror).GetField("mirrorPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var go = f?.GetValue(m) as GameObject;
            if (go == null) return "패널 없음";
            var cg = go.GetComponent<CanvasGroup>();
            return (go.activeSelf ? "켜짐 " : "꺼짐 ") + (cg != null ? cg.alpha.ToString("0.00") : "cg 없음");
        }

        private static bool OnScreen(string path)
        {
            var rt = Find(path) as RectTransform;
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float visibleTop = Mathf.Min(c[1].y, Screen.height), visibleBottom = Mathf.Max(c[0].y, 0f);
            log.Add($"  대화창 화면 위치: 아래 {c[0].y:0} · 위 {c[1].y:0} (화면 높이 {Screen.height})");
            return visibleTop - visibleBottom > Screen.height * 0.3f;
        }
        private static int CountActive(string name)
        {
            int n = 0;
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>())
                if (b.name == name && b.gameObject.activeInHierarchy) n++;
            return n;
        }

        private static R0926SheetModes Modes() => UnityEngine.Object.FindAnyObjectByType<R0926SheetModes>(FindObjectsInactive.Include);
        private static void ClickToggle(string name)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include))
                if (t.name == name && t.transform.parent != null && t.transform.parent.name == "FilterButtonPanel") { t.isOn = !t.isOn; return; }   // 장소 추가 카드에도 같은 이름이 있다
            log.Add("  토글 없음 " + name);
        }

        // ── 단계 도우미 ──────────────────────────────────────────
        private static void Do(Action a) => steps.Enqueue(() => { a(); return true; });
        // 실제 시간과 함께 게임 프레임 수도 채울 때까지 — 에디터가 뒤에 있으면 프레임이 드물게 돌아 캡처가 앞 장면에 머물렀다
        private static int waitFrame = -1;
        private static void Sleep(float s) => steps.Enqueue(() =>
        {
            if (waitUntil < 0) { waitUntil = EditorApplication.timeSinceStartup + s; waitFrame = Time.frameCount + Mathf.CeilToInt(s * 30f); }
            if (EditorApplication.timeSinceStartup < waitUntil || Time.frameCount < waitFrame) return false;
            waitUntil = -1;
            return true;
        });
        private static void Wait(Func<bool> cond, float max, string what)
        {
            double until = -1;
            steps.Enqueue(() =>
            {
                if (until < 0) until = EditorApplication.timeSinceStartup + max;
                if (cond()) { log.Add("  " + what); return true; }
                if (EditorApplication.timeSinceStartup > until) { log.Add("  시간 초과: " + what); return true; }
                return false;
            });
        }
        private static void Check(Func<bool> ok, string what) => Do(() => log.Add((ok() ? "PASS " : "FAIL ") + what));
        // 찍고 나서 두 프레임 더 기다린다 — 캡처는 그 프레임 끝에 저장되므로 다음 단계가 화면을 먼저 바꾸지 않게
        private static void Shot(string name)
        {
            int f = -1;
            steps.Enqueue(() =>
            {
                if (f < 0) { ScreenCapture.CaptureScreenshot(Path.Combine(dir, "test_" + name + ".png")); f = Time.frameCount; }
                if (Time.frameCount < f + 2) return false;
                f = -1;
                return true;
            });
        }

        private static void Back()
        {
            Do(BackNow);
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
        }
        private static void BackNow() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape));

        // 시트 윗부분(제목 줄)을 잡고 시트 높이 × fraction 만큼 0.3초에 걸쳐 끌어내린다
        private static void Drag(string sheetPath, float fraction, string midShot, Func<bool> when = null)
        {
            Vector2 from = default, to = default;
            int i = 0;
            bool skip = false;
            R0926SwipeDismiss sd = null;
            int frame = -1;
            const int n = 12;
            steps.Enqueue(() =>
            {
                skip = when != null && !when();
                if (skip) return true;
                var rt = Find(sheetPath) as RectTransform;
                if (rt == null) { log.Add("  시트 없음 " + sheetPath); skip = true; return true; }
                var c = new Vector3[4];
                rt.GetWorldCorners(c);   // 오버레이 캔버스 — 월드 좌표가 곧 화면 좌표
                float h = c[1].y - c[0].y;
                from = new Vector2((c[1].x + c[2].x) * 0.5f, c[1].y - Mathf.Min(70f, h * 0.05f));
                to = from - new Vector2(0f, h * fraction);
                sd = rt.GetComponent<R0926SwipeDismiss>();
                if (sd == null) { log.Add("  끌어 닫기 없음 " + sheetPath); skip = true; return true; }
                sd.EditorFeed(true, from);
                i = 0;
                frame = Time.frameCount;
                return true;
            });
            steps.Enqueue(() =>
            {
                if (skip) return true;
                if (Time.frameCount == frame) return false;   // 한 프레임에 한 걸음 — 에디터 갱신이 게임 프레임보다 잦다
                frame = Time.frameCount;
                i++;
                sd.EditorFeed(true, Vector2.Lerp(from, to, (float)i / n));
                if (i == n / 2 && midShot != null) ScreenCapture.CaptureScreenshot(Path.Combine(dir, "test_" + midShot + ".png"));
                if (i == 3) Diagnose(sheetPath, from);
                return i >= n;
            });
            steps.Enqueue(() => skip || Time.frameCount > frame);   // 마지막 걸음이 읽힌 뒤에 놓는다
            Do(() => { if (!skip) sd.EditorFeed(false, to); });
            Sleep(0.1f);
            Do(() => { if (!skip && sd != null) sd.EditorFeedEnd(); });
        }
        private static void Diagnose(string sheetPath, Vector2 at)
        {
            var p = Pointer.current;
            log.Add($"  진단: pointer={(p == null ? "null" : p.GetType().Name)} pressed={(p != null && p.press.isPressed)} pos={(p != null ? p.position.ReadValue().ToString() : "-")} from={at} screen={Screen.width}x{Screen.height}");
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null)
            {
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = at }, hits);
                log.Add("  진단: 맨 위 " + (hits.Count > 0 ? hits[0].gameObject.name + " (" + hits[0].gameObject.transform.parent?.name + ")" : "없음") + " · 총 " + hits.Count);
                if (hits.Count > 0)
                {
                    var h = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IDragHandler>(hits[0].gameObject);
                    string comps = h == null ? "-" : string.Join(",", System.Array.ConvertAll(h.GetComponents<Component>(), c => c.GetType().Name));
                    log.Add("  진단: 끌기 받는 곳 " + (h == null ? "없음" : h.name + " [" + comps + "]"));
                }
            }
            var sd = Find(sheetPath)?.GetComponent<R0926SwipeDismiss>();
            var ph = typeof(R0926SwipeDismiss).GetField("phase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            log.Add("  진단: swipe=" + (sd == null ? "없음" : (sd.isActiveAndEnabled + " phase=" + ph?.GetValue(sd))));
        }


        // ── 씬 조회 ──────────────────────────────────────────────
        private static Transform CanvasRoot()
        {
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == "Canvas") return r.transform;
            return null;
        }
        // 0930: 시트가 도크 윗선 틀(Clip0926) 안에 들어갔다 — 예전 경로로도 찾게
        private static Transform Find(string path)
        {
            var c = CanvasRoot();
            if (c == null) return null;
            var t = c.Find(path);
            int k = path.IndexOf('/');
            return t != null || k <= 0 ? t : c.Find(path.Substring(0, k) + "/Clip0926" + path.Substring(k));
        }
        private static bool Active(string path) { var t = Find(path); return t != null && t.gameObject.activeInHierarchy; }
        private static void Click(string path)
        {
            var b = Find(path)?.GetComponent<Button>();
            if (b == null) { log.Add("  버튼 없음 " + path); return; }
            b.onClick.Invoke();
        }
        private static string OpenPanels()
        {
            var open = new List<string>();
            foreach (var p in new[] { "ListPanel", "MessagePanel", "ChatRoomPanel", "FullProfilePanel", "LoginPromptPanel", "FollowPanel", "AskAISheet0926" })
                if (Active(p)) open.Add(p);
            return open.Count == 0 ? "-" : string.Join(",", open);
        }
        private static Vector2 remembered;
        private static void Remember(string path) { var rt = Find(path) as RectTransform; if (rt != null) remembered = rt.anchoredPosition; }
        private static bool SamePlace(string path) { var rt = Find(path) as RectTransform; return rt != null && Vector2.Distance(rt.anchoredPosition, remembered) < 1f; }

        // ── 진행 ────────────────────────────────────────────────
        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                if (running) R0926SkyWeather.EditorRegionOverride = null;   // 시험 도중 플레이를 끈 경우 — 다음 플레이에 남지 않게 (도메인 다시 읽기 꺼져 있다)
                if (running) { R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; R0926HuntProfile.EditorHostAlias = null; LoginManager.EditorAuthBase = null; }   // 보물찾기·로그인 시험 서버도 같은 이유로
                if (treasureRun) { PlayerPrefs.DeleteKey(TreasurePref); treasureRun = false; }   // 시험 게임을 지켜보던 기록 (시험이 중간에 끊겨도)
                if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(LoginReadyKey, false)) RestoreLogin();
                running = false;
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + 3.0;
                if (File.Exists(PlanPath) && !File.Exists(ResultPath + ".lock") && !EditorApplication.isCompiling
                    && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // 시작화면 측정은 첫 프레임부터 — 플레이를 켜기 전에 프로파일러를 켠다
                    var pl = File.ReadAllLines(PlanPath);
                    if (pl.Length > 1 && pl[1].Trim() == "splashprof") Redesign0926SplashProf.StartRecording();
                    if (pl.Length > 1 && pl[1].Trim() == "login") StashLogin();
                    EditorApplication.isPlaying = true;
                }
                return;
            }
            if (!running) return;
            try
            {
                while (steps.Count > 0 && steps.Peek()()) steps.Dequeue();
            }
            catch (Exception e)
            {
                log.Add("ERROR " + e.Message + (e.InnerException != null ? " ← " + e.InnerException.GetType().Name + ": " + e.InnerException.Message + " @ " + e.InnerException.StackTrace?.Split((char)10)[0] : ""));
                steps.Clear();
            }
            if (steps.Count > 0) return;
            running = false;
            if (treasureRun) { PlayerPrefs.DeleteKey(TreasurePref); treasureRun = false; R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; }
            R0926SkyWeather.EditorRegionOverride = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedBehavior;
            InputSystem.settings.backgroundBehavior = savedBackground;
            Application.runInBackground = savedRunInBackground;
            try { File.Delete(PlanPath); } catch (Exception e) { log.Add("plan 삭제 실패 " + e.Message); }
            File.WriteAllLines(ResultPath, log);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;   // 마지막 캡처가 저장될 틈을 둔다
        }
    }
}
