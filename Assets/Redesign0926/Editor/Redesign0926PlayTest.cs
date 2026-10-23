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
            // 기기 화면과 UI 바탕(캔버스)의 크기가 다르면 자리 시험이 전부 어긋난다 — 먼저 확인하고, 어긋났으면 시뮬레이터를 주 화면으로 다시 맞춘 뒤 다시 잰다
            // (10-19: UI 가 숨은 Game 탭 크기 877x646 으로 깔려 왼쪽 아래 구석에 작게 그려졌다)
            string firstScreen = null;
            Do(() =>
            {
                if (CanvasMatchesScreen(out firstScreen)) return;
                MainViewToSimulator();
                screenFixUntil = EditorApplication.timeSinceStartup + 3.0;
            });
            steps.Enqueue(() => EditorApplication.timeSinceStartup >= screenFixUntil || CanvasMatchesScreen(out _));
            Do(() =>
            {
                bool ok = CanvasMatchesScreen(out var now);
                screenFixUntil = 0;
                log.Add((ok ? "  " : "FAIL ") + now + $" · 보기 {PlayModeWindow.GetViewType()}"
                    + (now != firstScreen ? " (처음 " + firstScreen + ")" : "") + (mainViewNote.Length > 0 ? " · 주 화면 " + mainViewNote : ""));
            });
            if (mode == "splashprof") PlanSplashProf();
            else if (mode == "chatnav") PlanChatNav();
            else if (mode == "woopang1") PlanWoopangOne();
            else if (mode == "land") PlanLand();
            else if (mode == "profile")
            {
                Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
                Do(() => { if (Active("LoginPromptPanel")) BackNow(); ProfileManager.Instance?.ShowProfile("3"); });
                Sleep(3.5f);
                Shot("profile");
            }
            else if (mode == "skyfast") PlanSkyFast();
            else if (mode == "inputsync") PlanInputSync();
            else if (mode == "caret") PlanCaret();
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
            else if (mode == "huntchest") PlanHuntChest();
            else if (mode == "login") PlanLogin();
            else if (mode == "loginswitch") PlanLoginSwitch();
            else if (mode == "chatlook") PlanChatLook();
            else if (mode == "v24") PlanV24();
            else if (mode == "farfade") PlanFarFade();
            else if (mode == "nudge") PlanNudge();
            else if (mode == "photomem") PlanPhotoMem();
            else if (mode == "appinfo") PlanAppInfo();
            else if (mode == "p2pzoom") PlanP2PZoom();
            else if (mode == "maptiles") PlanMapTiles();
            else if (mode == "mapcoord") PlanMapCoord();
            else if (mode == "vwshot") PlanVwShot();
            else if (mode == "arprev") { storeSrc = lines.Length > 2 ? lines[2].Trim() : ""; PlanArPrev(); }
            else if (mode == "pickers") PlanPickers();
            else if (mode == "photoload") { storeSrc = lines.Length > 2 ? lines[2].Trim() : ""; PlanPhotoLoad(); }
            else if (mode == "crop") PlanCrop();
            else if (mode == "addcenter") PlanAddCenter();
            else if (mode == "overlap") PlanOverlap();
            else if (mode == "avatar") PlanAvatar();
            else if (mode == "follow") PlanFollow();
            else if (mode == "follownotif") PlanFollowNotif();
            else if (mode == "msgfix") PlanMsgFix();
            else if (mode == "searchfollow") PlanSearchFollow();
            else if (mode == "mapprofile") PlanMapProfile();
            else if (mode == "scrolllock") PlanScrollLock();
            else if (mode == "filters") PlanFilters();
            else if (mode == "guidetouch") PlanGuideTouch();
            else if (mode == "guide") { storeSrc = lines.Length > 2 ? lines[2].Trim() : ""; PlanGuide(); }
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
            Do(() => ClickToggle("PetFriendlyToggle"));   // 반려견은 두 가지 — 다시 누르면 전부
            Do(() => ClickToggle("CategoryToggle"));      // 분류 고르기 목록이 펼쳐진다
            Sleep(0.4f);
            Shot("list_category_menu");
            Do(() => PickCategory(1));
            Sleep(0.4f);
            Shot("list_category_shop");
            Do(() => ClickToggle("CategoryToggle"));
            Do(() => PickCategory(0));   // '전체'로
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

        // 분류 고르기 · 댓글 입력줄 · 인디케이터 X 두 번 누르기
        private static void PlanLook2()
        {
            Wait(() => !BootOverlay.Showing, 40f, "시작화면 끝");
            Sleep(1.0f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.5f);
            Shot("cat_menu");
            Do(() => PickCategory(1));
            Sleep(0.5f);
            Do(() => log.Add("  분류 = " + UnityEngine.Object.FindAnyObjectByType<FilterManager>()?.CategoryState));
            Shot("cat_1");
            Do(() => ClickToggle("CategoryToggle"));
            Do(() => PickCategory(2));
            Sleep(0.5f);
            Do(() => log.Add("  분류 = " + UnityEngine.Object.FindAnyObjectByType<FilterManager>()?.CategoryState));
            Shot("cat_2");
            Do(() => ClickToggle("CategoryToggle"));
            Do(() => PickCategory(0));
            Sleep(0.5f);
            Do(() => log.Add("  분류(전체로) = " + UnityEngine.Object.FindAnyObjectByType<FilterManager>()?.CategoryState));
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
        // B7 (10-18) — 팔로잉/팔로워 창. 서버를 부르지 않게 목록을 예시로 채워 그대로 그린다
        private static FollowManager Fm() => UnityEngine.Object.FindAnyObjectByType<FollowManager>(FindObjectsInactive.Include);
        private static void FillFollow(bool followers)
        {
            var fm = Fm();
            if (fm == null) { log.Add("FollowManager 없음"); return; }
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(FollowManager);
            var samples = new[]
            {
                ("ft1", "yeonnam_cafe", true, 320), ("ft2", "sigorpd", true, 210), ("ft3", "꼬요", false, 88),
                ("ft4", "소제이_공방", false, 41), ("ft5", "hongdae.walk.long_name_check", false, 12), ("ft6", "연트럴파크", true, 7),
            };
            var fl = new List<FollowManager.FollowUserData>();
            var gl = new List<FollowManager.FollowUserData>();
            foreach (var (id, name, f, c) in samples)
            {
                fl.Add(new FollowManager.FollowUserData { userId = id, username = name, avatarUrl = "", isFollowing = f, followerCount = c });
                if (f) gl.Add(new FollowManager.FollowUserData { userId = id, username = name, avatarUrl = "", isFollowing = true, followerCount = c });
            }
            t.GetField("followersList", F).SetValue(fm, fl);
            t.GetField("followingList", F).SetValue(fm, gl);
            t.GetField("currentUserId", F).SetValue(fm, "follow_test_me");
            t.GetField("showingFollowers", F).SetValue(fm, followers);
            if (fm.panel != null) fm.panel.SetActive(true);
            if (fm.titleText != null) fm.titleText.text = "gogle";
            t.GetMethod("ApplyCustomFonts", F).Invoke(fm, null);
            t.GetMethod("UpdateTabUI", F).Invoke(fm, null);
            t.GetMethod("DisplayCurrentList", F).Invoke(fm, null);
        }

        private static void FollowRows(string tag)
        {
            var fm = Fm();
            if (fm == null || fm.contentParent == null) return;
            foreach (Transform c in fm.contentParent)
            {
                if (!c.gameObject.activeSelf || !c.name.StartsWith("Item_")) continue;
                var rt = (RectTransform)c;
                var u = c.Find("Username")?.GetComponent<Text>();
                var a = c.Find("ActionButton") as RectTransform;
                var m = c.Find("MessageButton") as RectTransform;
                var av = c.Find("Avatar") as RectTransform;
                log.Add("  " + tag + " " + c.name + " h=" + rt.rect.height.ToString("0")
                    + (av != null ? " avatar=" + av.rect.width.ToString("0") : "")
                    + (u != null ? " id=" + u.fontSize + (u.preferredWidth > ((RectTransform)u.transform).rect.width + 1 ? "(넘침)" : "") : "")
                    + (a != null && a.gameObject.activeSelf ? " act=" + a.rect.size.ToString("0") + "'" + a.GetComponentInChildren<Text>()?.text + "' fs" + a.GetComponentInChildren<Text>()?.fontSize : "")
                    + (m != null && m.gameObject.activeSelf ? " msg=" + m.rect.size.ToString("0") : ""));
            }
        }

        private static bool FollowButtonsApart()
        {
            var fm = Fm();
            if (fm == null || fm.contentParent == null) return false;
            foreach (Transform c in fm.contentParent)
            {
                if (!c.gameObject.activeSelf || !c.name.StartsWith("Item_")) continue;
                var a = c.Find("ActionButton") as RectTransform;
                var m = c.Find("MessageButton") as RectTransform;
                var u = c.Find("Username") as RectTransform;
                if (a == null || u == null) return false;
                var ac = new Vector3[4]; a.GetWorldCorners(ac);
                var uc = new Vector3[4]; u.GetWorldCorners(uc);
                if (uc[2].x > ac[0].x + 1f) return false;                         // 아이디 칸이 버튼 밑으로 들어감
                if (m != null && m.gameObject.activeSelf)
                {
                    var mc = new Vector3[4]; m.GetWorldCorners(mc);
                    if (ac[2].x > mc[0].x + 1f && mc[2].x > ac[0].x + 1f) return false;   // 두 버튼이 겹침
                }
            }
            return true;
        }

        private static bool FollowDockClose()
        {
            var t = Find("Dock0926/MiniProfile");
            var cg = t != null ? t.GetComponent<CanvasGroup>() : null;
            return cg != null && cg.alpha < 0.01f && Active("FollowPanel/DockMirror0926/DockClose0926/Icon0926");
        }

        private static int FollowRowCount()
        {
            var fm = Fm();
            int n = 0;
            if (fm != null && fm.contentParent != null)
                foreach (Transform c in fm.contentParent) if (c.gameObject.activeSelf && c.name.StartsWith("Item_")) n++;
            return n;
        }

        private static void PlanFollow()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Sleep(0.5f);
            Do(() => FillFollow(true));
            Sleep(1.0f);
            Shot("follow_followers");
            Do(() => FollowRows("팔로워"));
            Check(() => OnScreen("FollowPanel/Background"), "팔로우 창이 화면 안");
            Check(() =>
            {
                var bg = Find("FollowPanel/Background") as RectTransform;
                var dock = Find("Dock0926") as RectTransform;
                if (bg == null || dock == null) return false;
                Rect b = ScreenRect(bg), d = ScreenRect(dock);
                log.Add($"  FollowPanel: 창 y {b.yMin:0}~{b.yMax:0} · 도크 위 {d.yMax:0} · 화면 {Screen.width}x{Screen.height}");
                return b.yMin >= d.yMax - 1f;
            }, "세로: 팔로우 창이 도크 위에 뜸");
            Check(FollowButtonsApart, "팔로워: 아이디·팔로우·메시지 버튼이 서로 안 겹침");
            Check(FollowDockClose, "도크 프로필 칸 자리에 X '닫기'");
            Check(() => { string h = TopHit("FollowPanel/DockMirror0926/DockClose0926"); log.Add("  도크 닫기 자리: " + h); return h.StartsWith("ClosePx0926") && h.EndsWith("(그 칸)"); },
                "도크 '닫기' 자리를 누르면 닫기 덮개가 받음");
            Check(() => { string h = TopHit("FollowPanel/Background/Header/BackButton"); log.Add("  ‹ 자리: " + h); return h.StartsWith("ClosePx0926") && h.EndsWith("(그 칸)"); },
                "‹ 자리를 누르면 닫기 덮개가 받음");

            // 아이디 검색 — 글을 쓰면 지우기 X 가 나타나고 목록이 걸러진다
            Check(() => !Active("FollowPanel/Background/SearchBar/SearchClearButton"), "검색: 빈 칸엔 지우기 X 없음");
            Do(() => { var fm = Fm(); if (fm != null && fm.searchInput != null) fm.searchInput.text = "yeon"; });
            Sleep(0.4f);
            Shot("follow_search");
            Check(() => Active("FollowPanel/Background/SearchBar/SearchClearButton") && FollowRowCount() == 1, "검색: 'yeon' → 한 줄 · 지우기 X 보임");
            Do(() => Tap("FollowPanel/Background/SearchBar/SearchClearButton"));
            Sleep(0.4f);
            Check(() => !Active("FollowPanel/Background/SearchBar/SearchClearButton") && FollowRowCount() == 6, "검색: X 누르면 비우고 여섯 줄 다시");

            Do(() => FillFollow(false));
            Sleep(0.8f);
            Shot("follow_following");
            Do(() => FollowRows("팔로잉"));
            Check(FollowButtonsApart, "팔로잉: 아이디·메시지 버튼이 서로 안 겹침");

            Do(() => Rotate(true));
            Wait(() => Turned(true), 8f, "가로로 돌림");
            Sleep(1.0f);
            Do(() => FillFollow(true));
            Sleep(0.8f);
            Shot("follow_land");
            Check(() => OnScreen("FollowPanel/Background"), "가로: 팔로우 창이 화면 안");
            Check(() => LeftOfRail("FollowPanel"), "가로: 팔로우 창이 오른쪽 도크 막대를 가리지 않음");
            Check(() => { string h = TopHit("FollowPanel/DockMirror0926/DockClose0926"); log.Add("  가로 도크 닫기 자리: " + h); return h.EndsWith("(그 칸)"); },
                "가로: 도크 막대의 '닫기'를 누를 수 있음");
            Check(FollowButtonsApart, "가로: 버튼·아이디 안 겹침");
            Do(() => Rotate(false));
            Wait(() => Turned(false), 8f, "세로로 돌림");
            Sleep(0.8f);

            // 도크 '닫기' — 카드가 내려간 뒤 메인으로 (‹ 와 달리 프로필로 돌아가지 않는다)
            Do(() => Tap("FollowPanel/DockMirror0926/DockClose0926/ClosePx0926"));
            Wait(() => !Active("FollowPanel"), 4f, "도크 '닫기'로 팔로우 창 닫힘");
            Sleep(0.5f);
            Check(() => !Active("FollowPanel") && !Active("FullProfilePanel"), "도크 '닫기' 뒤 메인 (프로필 안 뜸)");
            Check(() =>
            {
                var t = Find("Dock0926/MiniProfile");
                var cg = t != null ? t.GetComponent<CanvasGroup>() : null;
                return cg != null && cg.alpha > 0.99f;
            }, "닫은 뒤 도크 프로필 칸 다시 보임");
            Do(() => { var fm = Fm(); if (fm != null && fm.panel != null) fm.panel.SetActive(false); });
        }

        // B9 (10-18) — 메세지 주고받는 소리 80%: 받는 소리 0.2 → 0.16 · 보내기 버튼만 누름 소리 0.8배 (다른 버튼은 그대로)
        // B10 (10-18) — 메세지 밀어 삭제: 목록 자르기는 반듯한 사각(옛 둥근 카드 마스크가 맨 위·아래 '삭제' 모서리를 깎았다) · '삭제'는 한 번에 하나만
        private const string SwNote = "swipe_test_b10_";
        private static List<SwipeToDeleteHandler> SwipeRows()
        {
            var list = new List<SwipeToDeleteHandler>();
            var mpm = Mpm();
            if (mpm == null || mpm.conversationListContent == null) return list;
            foreach (Transform c in mpm.conversationListContent)
            {
                var h = c.GetComponent<SwipeToDeleteHandler>();
                if (h != null && c.gameObject.activeSelf) list.Add(h);
            }
            return list;
        }
        private static SwipeToDeleteHandler SwRow(string id) => SwipeRows().Find(h => h.userId == id);
        private static float DeleteWidth(SwipeToDeleteHandler h)
        {
            var d = h != null ? h.transform.Find("DeleteButton") : null;
            return d != null && d.gameObject.activeSelf ? ((RectTransform)d).sizeDelta.x : 0f;
        }
        private static int OpenDeleteCount() => SwipeRows().FindAll(h => DeleteWidth(h) > 1f).Count;
        // 손가락으로 왼쪽으로 미는 것과 같은 순서로 이벤트를 준다 (누름 → 끌기 시작 → 끌기 → 끝)
        private static void SwipeLeft(SwipeToDeleteHandler h, float dist)
        {
            if (h == null) { log.Add("  밀 줄 없음"); return; }
            var c = new Vector3[4];
            ((RectTransform)h.transform).GetWorldCorners(c);
            var start = new Vector2(Mathf.Lerp(c[0].x, c[2].x, 0.7f), (c[0].y + c[2].y) * 0.5f);
            var ev = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = start, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            h.OnPointerDown(ev);
            h.OnBeginDrag(ev);
            ev.position = start + new Vector2(-24f, 0f); h.OnDrag(ev);
            ev.position = start + new Vector2(-dist, 0f); h.OnDrag(ev);
            h.OnEndDrag(ev);
        }
        private static float FwScale(UITouchForwarder fw) => fw != null ? new UnityEditor.SerializedObject(fw).FindProperty("volumeScale").floatValue : -1f;
        private static bool SwNoteSaved(string id)
        {
            var list = typeof(MessagePanelManager).GetField("conversations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(Mpm()) as List<ConversationSummary>;
            return (list != null && list.Exists(c => c.notificationId == id)) || PlayerPrefs.GetString("SystemNotifications", "").Contains(id);
        }

        // 10-19 대표님 — 첫 안내 중: 하단 버튼은 눌리고(누르면 그 창이 열리고 안내는 닫힘) AR 오브젝트는 안 눌림, 안내가 끝난 뒤부터 오브젝트
        // · '(친구찾기)' 는 도크 칸이 아니라 메시지 창 제목 아래에만
        // G1 (10-20 대표님 「첫 안내 — 위쪽으로 · 크게, 버튼 하이라이트 유지」) — 세로 3쪽 · 가로 3쪽.
        // 글은 화면 가운데 위 · 크게, 비춘 칸 위(가로는 왼쪽)에 화살표, 그 사이 점선 길. 구멍은 칸 그대로
        private static void PlanGuide()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            R0926GuideOverlay ov = null;
            Func<string, object> get = n => typeof(R0926GuideOverlay).GetField(n, F)?.GetValue(ov);
            Action<string, bool> look = (tag, land) =>
            {
                Check(() =>
                {
                    var text = get("text") as Text;
                    var next = get("next") as RectTransform;
                    var arrow = get("arrow") as RectTransform;
                    var skip = get("skip") as RectTransform;
                    var hole = get("hole") as RectTransform;
                    var path = get("path") as R0926GuidePath;
                    var targets = get("targets") as RectTransform[];
                    var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                    var target = targets != null && g != null && g.CurrentPage < targets.Length ? targets[g.CurrentPage] : null;
                    if (text == null || next == null || arrow == null || skip == null || hole == null || path == null || target == null) { log.Add("  안내 조각 없음"); return false; }
                    Rect t = ScreenRect(text.rectTransform), n = ScreenRect(next), a = ScreenRect(arrow), k = ScreenRect(skip), h = ScreenRect(hole), c = ScreenRect(target);
                    log.Add($"  {tag}: 글 {t.yMin:0}~{t.yMax:0} (가운데 {t.center.y / Screen.height:0.00}H, 글자 {text.fontSize}) · 다음 {n.width:0}x{n.height:0} · 화살표 ({a.center.x:0},{a.center.y:0}) · 칸 ({c.center.x:0},{c.center.y:0}) · 건너뛰기 아래 {k.yMin:0} · 길 {(path.show ? "보임" : "숨음")}");
                    bool ok = text.fontSize >= 70 && n.height >= 120 && path.show && t.yMax < k.yMin
                        && Vector2.Distance(h.center, c.center) < 8f
                        && t.xMin >= 0f && t.xMax <= Screen.width && n.yMin >= 0f;
                    if (land) ok &= t.xMax < a.xMin && a.xMax < c.xMin && Mathf.Abs(a.center.y - c.center.y) < 8f && n.xMax < a.xMin;
                    else ok &= t.center.y > Screen.height * 0.45f && n.yMin > a.yMax && a.yMin > c.yMax && Mathf.Abs(a.center.x - c.center.x) < 8f;
                    return ok;
                }, $"첫 안내 {tag}: 글 크게 · {(land ? "왼쪽 가운데" : "가운데 위")} · 화살표가 비춘 칸을 가리킴 · 점선 길 · 건너뛰기와 안 겹침");
            };
            Action<bool, string> pages = (land, pre) =>
            {
                Do(() => UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include)?.ForceShowGuide());
                Sleep(1.8f);
                Do(() => ov = UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include));
                for (int i = 1; i <= 3; i++)
                {
                    int page = i;
                    look(pre + page, land);
                    Shot("guide_" + pre + page);
                    if (page < 3) { Do(() => ov?.Next()); Sleep(1.2f); }
                }
                Do(() => ov?.Skip());
                Sleep(1.0f);
                Check(() => !Active("FirstTimeGuidePanel"), "안내: 건너뛰기로 닫힘");
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                HidePrivatePlaces();
                Backdrop("bg_city.png");
            });
            Sleep(1.0f);
            pages(false, "p");
            Do(() => Rotate(true));
            Wait(() => Turned(true), 8f, "가로로 돌림");
            Sleep(1.0f);
            pages(true, "l");
            Do(() => Rotate(false));
            Wait(() => Turned(false), 8f, "세로로 되돌림");
        }

        private static void PlanGuideTouch()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                PlayerPrefs.SetInt("IsFirstTime", 1);   // 본 사람으로 — 아니면 6초 뒤 처음 안내가 늦게 떠 '안내가 없을 때' 를 잴 수 없다 (안내는 아래에서 직접 띄운다)
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(1.0f);

            // I1 — 메시지 창 제목 아래 작게 '(친구찾기)' · 도크 칸엔 없음
            Check(() => Find("Dock0926/Message_Button") != null && Find("Dock0926/Message_Button/SubLabel0926") == null, "도크 '메세지' 칸에 '(친구찾기)' 없음");
            Do(() => { var m = Mpm(); if (m != null && m.messagePanel != null) m.messagePanel.SetActive(true); });
            Sleep(1.0f);
            Check(() =>
            {
                const string H = "MessagePanel/Clip0926/Background/";
                var bg = Find(H.TrimEnd('/'));
                var title = Find(H + "Header/Title")?.GetComponent<Text>();
                var sub = Find(H + "Header/Sub0926")?.GetComponent<Text>();
                var area = Find(H + "SearchArea") as RectTransform;
                if (bg == null || title == null || sub == null || area == null || !sub.gameObject.activeInHierarchy) { log.Add("  제목·(친구찾기)·검색칸 없음"); return false; }
                var c = new Vector3[4];
                area.GetWorldCorners(c);
                float searchTop = bg.InverseTransformPoint(c[1]).y;
                Vector2 ty = GlyphY(title, bg), sy = GlyphY(sub, bg);
                log.Add($"  제목 글자 {ty.y:0}~{ty.x:0} · '{sub.text}' {sy.y:0}~{sy.x:0} (크기 {sub.fontSize}) · 검색칸 윗선 {searchTop:0}");
                return sub.text == "(친구찾기)" && sub.fontSize <= 40 && ty.x - sy.y >= 8f && sy.x - searchTop >= 8f;
            }, "메시지 창 제목 아래 작게 '(친구찾기)' — 제목·검색칸과 겹치지 않음");
            Shot("msg_title_sub");
            Do(() => { var m = Mpm(); if (m != null && m.messagePanel != null) m.messagePanel.SetActive(false); });
            Sleep(0.6f);
            Check(() => Find("MessagePanel/Clip0926/Background/Header/Sub0926") != null && !Active("MessagePanel/Clip0926/Background/Header/Sub0926"),
                "메시지 창을 닫으면 '(친구찾기)' 도 안 보임");

            // I2 — 첫 안내 중
            Check(() => !R0926GuideOverlay.BlocksWorld, "안내가 없을 땐 오브젝트를 누를 수 있음");
            Do(() => UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include)?.ForceShowGuide());
            Sleep(1.8f);
            Check(() => Active("FirstTimeGuidePanel") && R0926GuideOverlay.BlocksWorld, "안내 중에는 AR 오브젝트 터치를 막음");
            Do(() =>
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var dock = Find("Dock0926");
                var guide = Find("FirstTimeGuidePanel");
                var c = new Vector3[4];
                foreach (var n in new[] { "List_Button", "PlusButton", "Message_Button", "MiniProfile" })
                {
                    var rt = dock != null ? dock.Find(n) as RectTransform : null;
                    if (rt == null || es == null) { log.Add("FAIL 도크 칸 없음 " + n); continue; }
                    rt.GetWorldCorners(c);
                    var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = (Vector2)((c[0] + c[2]) / 2f) }, hits);
                    var top = hits.Count > 0 ? hits[0].gameObject.transform : null;
                    log.Add((top != null && top.IsChildOf(dock) ? "PASS " : "FAIL ") + "안내 중에도 도크 '" + n + "' 누름 → " + (top != null ? top.name : "없음"));
                }
                // 오브젝트가 떠 있을 화면 가운데·위쪽은 안내 막이 받는다 (뒤 AR 로 새지 않게)
                foreach (var at in new[] { new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), new Vector2(Screen.width * 0.25f, Screen.height * 0.7f) })
                {
                    var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = at }, hits);
                    var top = hits.Count > 0 ? hits[0].gameObject.transform : null;
                    log.Add((top != null && guide != null && top.IsChildOf(guide) ? "PASS " : "FAIL ") + $"안내 중 화면 ({at.x:0},{at.y:0}) 은 안내 막이 받음 → " + (top != null ? top.name : "없음"));
                }
            });
            Shot("guide_dock_open");
            // 비춘 '목록' 을 누르면 목록이 열리고 안내는 닫힌다 → 그 뒤부터 오브젝트
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.4f);
            Check(() => !Active("FirstTimeGuidePanel") && Active("ListPanel"), "안내 중 '목록' 누름 → 목록이 열리고 안내는 닫힘");
            Check(() => !R0926GuideOverlay.BlocksWorld, "안내가 끝나면 다시 오브젝트를 누를 수 있음");
            Shot("guide_dock_list");
            Do(() => BackNow());
            Sleep(0.8f);
            // 안내가 없을 땐 도크 버튼이 안내를 건드리지 않는다 (붙였던 연결을 뗐는지) — 로그인 안 돼 있으면 메시지 대신 로그인 안내
            Do(() => Click("Dock0926/Message_Button"));
            Sleep(0.8f);
            Do(() => log.Add("  열린 창: " + OpenPanels()));
            Check(() => !Active("FirstTimeGuidePanel") && (Active("MessagePanel") || Active("LoginPromptPanel")), "안내가 끝난 뒤 도크는 평소대로");
            Do(() => BackNow());
            Sleep(0.6f);
            Do(() => { PlayerPrefs.SetInt(FirstTimeGuide.FirstTimeKey, 1); PlayerPrefs.Save(); });
        }

        // 글자가 실제로 그려진 위아래 (space 기준) — x 는 아래, y 는 위
        private static Vector2 GlyphY(Text t, Transform space)
        {
            var vs = t.cachedTextGenerator.verts;
            float inv = 1f / Mathf.Max(0.0001f, t.pixelsPerUnit), lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < vs.Count; i++)
            {
                float y = space.InverseTransformPoint(t.rectTransform.TransformPoint(vs[i].position * inv)).y;
                lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
            }
            return vs.Count == 0 ? Vector2.zero : new Vector2(lo, hi);
        }

        private static void PlanMsgFix()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);

            // B9 — 소리 크기
            Check(() => { var m = Mpm(); return m != null && Mathf.Abs(m.chatReceiveSfxVolume - 0.16f) < 0.001f; }, "받는 소리 0.16 (예전 0.2 의 80%)");
            Check(() => { var m = Mpm(); var fw = m != null && m.sendButton != null ? m.sendButton.GetComponent<UITouchForwarder>() : null; return Mathf.Abs(FwScale(fw) - 0.8f) < 0.001f; },
                "보내기 버튼 누름 소리 0.8배");
            Check(() =>
            {
                var m = Mpm();
                int others = 0, changed = 0;
                foreach (var fw in UnityEngine.Object.FindObjectsByType<UITouchForwarder>(FindObjectsInactive.Include))
                {
                    if (m != null && m.sendButton != null && fw.gameObject == m.sendButton.gameObject) continue;
                    others++;
                    if (Mathf.Abs(FwScale(fw) - 1f) > 0.001f) { changed++; log.Add("  다른 버튼 소리가 바뀜: " + fw.name); }
                }
                log.Add("  다른 버튼 " + others + "개");
                return others > 0 && changed == 0;
            }, "다른 버튼 소리는 그대로");

            // B10 — 목록 자르기 · 한 번에 하나만 · 삭제
            Do(() => { var m = Mpm(); if (m != null && m.messagePanel != null) m.messagePanel.SetActive(true); });
            Sleep(0.8f);
            Check(() =>
            {
                var vp = Find("MessagePanel/Clip0926/Background/ConversationList/Viewport");
                return vp != null && vp.GetComponent<Mask>() == null && vp.GetComponent<RectMask2D>() != null;
            }, "목록 자르기는 반듯한 사각 (둥근 마스크 없음)");
            // (10-21) 이 기기 알림은 이제 WOOPANG 한 줄로 합쳐진다 — 밀기는 목록에만 넣은 시험 대화 줄 셋으로 보고,
            // '삭제'는 서버에 묻지 않는 WOOPANG 줄로 누른다(저장값은 덜어 두었다가 되돌림). 대화 줄 '삭제'는 운영 서버에서 대화를 지우므로 누르지 않는다
            Do(() =>
            {
                StashWoopangPrefs();
                var list = Convs();
                for (int i = 1; i <= 3; i++)
                    list?.Add(new ConversationSummary { userId = SwNote + i, username = "밀어 삭제 시험 " + i, avatarUrl = "", lastMessage = "swipe test " + i,
                        lastMessageTime = DateTime.Now.AddMinutes(i).ToString("yyyy-MM-dd HH:mm:ss") });
                Mpm()?.AddLocationNotificationFromPush("밀어 삭제 시험", "swipe test woopang", SwNote + "w");   // 목록도 다시 그린다
            });
            Sleep(0.6f);
            Check(() => SwRow(SwNote + "1") != null && SwRow(SwNote + "2") != null && SwRow(SwNote + "3") != null && SwRow("woopang_admin") != null, "준비: 시험 대화 줄 셋 + WOOPANG 줄");
            Do(() => SwipeLeft(SwRow(SwNote + "3"), 420f));
            Sleep(0.3f);
            Shot("msgfix_swipe_top");
            Do(() => log.Add("  맨 위 줄: " + (SwipeRows().Count > 0 ? SwipeRows()[0].userId : "-") + " · 열린 삭제 " + OpenDeleteCount()));
            Check(() => DeleteWidth(SwRow(SwNote + "3")) > 200f && OpenDeleteCount() == 1, "왼쪽으로 밀면 '삭제'가 열림");
            Do(() => SwipeLeft(SwRow(SwNote + "2"), 420f));
            Sleep(0.3f);
            Shot("msgfix_swipe_second");
            Check(() => DeleteWidth(SwRow(SwNote + "3")) < 1f && DeleteWidth(SwRow(SwNote + "2")) > 200f && OpenDeleteCount() == 1,
                "다른 줄을 밀면 먼저 열린 '삭제'는 닫힘 (한 번에 하나)");
            Do(() => SwipeLeft(SwRow(SwNote + "2"), 40f));
            Sleep(0.2f);
            Check(() => OpenDeleteCount() == 0, "조금만 밀면 도로 닫힘");
            Do(() => SwipeLeft(SwRow("woopang_admin"), 420f));
            Sleep(0.2f);
            Do(() =>
            {
                var d = SwRow("woopang_admin")?.transform.Find("DeleteButton");
                var b = d != null ? d.GetComponent<Button>() : null;
                if (b != null) b.onClick.Invoke(); else log.Add("  삭제 버튼 없음");
            });
            Sleep(0.5f);
            Check(() => SwRow("woopang_admin") == null && WoopangRowCount() == 0 && !SwNoteSaved(SwNote + "w") && OpenDeleteCount() == 0,
                "'삭제'를 누르면 그 줄만 지워짐 (WOOPANG — 이 기기 알림까지)");
            Check(() => SwRow(SwNote + "1") != null && SwRow(SwNote + "2") != null && SwRow(SwNote + "3") != null, "다른 줄은 그대로");

            // 정리 — 저장값을 되돌리고(이 기기 알림·공지 숨김·읽음 시각) 시험 대화 줄은 목록에서만 뺀다
            string before = null;
            Do(() =>
            {
                RestoreWoopangPrefs();
                before = PlayerPrefs.GetString("SystemNotifications", "");
                Convs()?.RemoveAll(c => c.userId != null && c.userId.StartsWith(SwNote));
                MpmCall("LoadSystemNotifications");
                MpmCall("RefreshConversationUI");
            });
            Sleep(0.4f);
            Check(() => SwRow(SwNote + "1") == null && !SwNoteSaved(SwNote + "w") && PlayerPrefs.GetString("SystemNotifications", "") == before
                     && SessionState.GetString(WoopangBackupKey, "") == "", "시험 줄 지움 · 저장값 되돌림");
        }

        // WOOPANG 줄 '삭제'를 실제로 눌러 보는 시험 — 이 편집기의 저장값(이 기기 알림·관리자 공지 숨김·읽음 시각)을 덜어 두었다가 되돌린다.
        // 시험이 중간에 끊겨도(오류 · 플레이 끔 · 스크립트 다시 읽기) 되돌리도록 SessionState 에 둔다
        private static readonly string[] WoopangStrKeys = { "SystemNotifications", "LocalAdminMessages", "AdminBroadcastLastReadTime" };
        private const string WoopangIntKey = "HiddenAdminBroadcastMaxId";
        private const string WoopangBackupKey = "Redesign0926PlayTest.WoopangPrefs";
        private static void StashWoopangPrefs()
        {
            var parts = new List<string>();
            foreach (var k in WoopangStrKeys) parts.Add(PlayerPrefs.HasKey(k) ? k + "\u0001" + PlayerPrefs.GetString(k, "") : k);
            parts.Add(PlayerPrefs.HasKey(WoopangIntKey) ? WoopangIntKey + "\u0001" + PlayerPrefs.GetInt(WoopangIntKey) : WoopangIntKey);
            SessionState.SetString(WoopangBackupKey, string.Join("\u0002", parts));
        }
        private static void RestoreWoopangPrefs()
        {
            string s = SessionState.GetString(WoopangBackupKey, "");
            if (s.Length == 0) return;
            SessionState.EraseString(WoopangBackupKey);
            foreach (var part in s.Split('\u0002'))
            {
                int i = part.IndexOf('\u0001');
                string k = i > 0 ? part.Substring(0, i) : part;
                if (i < 0) PlayerPrefs.DeleteKey(k);
                else if (k == WoopangIntKey) PlayerPrefs.SetInt(k, int.Parse(part.Substring(i + 1)));
                else PlayerPrefs.SetString(k, part.Substring(i + 1));
            }
            PlayerPrefs.Save();
        }

        // B11 (10-18) — 메세지 검색으로 아는 사람 찾아 팔로우: 대화가 없는 사람도 검색 줄 오른쪽 끝 알약으로 바로 팔로우 ·
        // 왼쪽 아바타 → 그 사람 프로필(팔로우 버튼) · 내가 차단한 사람은 알약 없음 · 나를 차단한 사람은 아예 안 나옴 · 검색 중 새 알림이 와도 결과 유지.
        // 검색·팔로우·프로필은 시험 서버(127.0.0.1:5099)의 고정 목록. 로그인은 이벤트 없이 시험 아이디만 넣고 토큰은 두지 않는다
        // (로그인 이벤트를 내면 다른 매니저들이 운영 서버에 시험 아이디로 묻는다 — 시험 서버는 아이디를 요청에서 읽는다)
        private const string SfMe = "hunt_test_sfme", SfNote = "search_test_b11";
        private static UserData sfUserBackup;
        private static bool sfGuestBackup;
        private static void SfLogin(bool on, string id = SfMe)
        {
            var lm = LoginManager.Instance;
            if (lm == null) return;
            var user = typeof(LoginManager).GetProperty("CurrentUser");
            var guest = typeof(LoginManager).GetProperty("IsGuest");
            if (on)
            {
                sfUserBackup = lm.CurrentUser;
                sfGuestBackup = lm.IsGuest;
                user.SetValue(lm, new UserData { id = id, username = id });
                guest.SetValue(lm, false);
            }
            else
            {
                user.SetValue(lm, sfUserBackup);
                guest.SetValue(lm, sfGuestBackup);
            }
        }
        private static Transform SfRow(string name)
        {
            var m = Mpm();
            if (m == null || m.conversationListContent == null) return null;
            foreach (Transform c in m.conversationListContent)
            {
                if (!c.gameObject.activeSelf) continue;
                var t = c.Find("Content/UsernameText")?.GetComponent<Text>();
                if (t != null && t.text == name) return c;
            }
            return null;
        }
        private static Transform SfPill(string name) => SfRow(name)?.Find("Content/" + R0926FollowPill.Name);
        private static string SfPillText(string name)
        {
            var p = SfPill(name);
            if (p == null || !p.gameObject.activeSelf) return null;
            var t = p.GetComponentInChildren<Text>(true);
            return t != null ? t.text : "";
        }
        private static void SfTap(Transform t)
        {
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b != null) b.onClick.Invoke(); else log.Add("  누를 것 없음");
        }
        // 프로필의 팔로우 버튼 상태 — follow(팔로우) · following(팔로잉) · ?(아직)
        private static string SfFollowState()
        {
            var pm = ProfileManager.Instance;
            if (pm == null) return "-";
            bool a = pm.followButton != null && pm.followButton.gameObject.activeInHierarchy;
            bool b = pm.followedButton != null && pm.followedButton.gameObject.activeInHierarchy;
            return a && !b ? "follow" : b && !a ? "following" : "?";
        }
        // 미리보기 글 오른쪽 끝이 알약 왼쪽 끝보다 왼쪽
        private static bool SfPreviewClear(string name)
        {
            var row = SfRow(name);
            var pill = SfPill(name) as RectTransform;
            var pv = row != null ? row.Find("Content/PreviewText") as RectTransform : null;
            if (pv == null || pill == null) return false;
            var a = new Vector3[4];
            var b = new Vector3[4];
            pv.GetWorldCorners(a);
            pill.GetWorldCorners(b);
            return a[2].x <= b[0].x + 0.5f;
        }
        private static bool SfDevFollowing(string other) =>
            Has(DevGet(SfMe, "/api/is_following?follower_id=" + SfMe + "&following_id=" + other), "\"is_following\":true");

        private static void PlanSearchFollow()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                SessionState.SetBool(LoginReadyKey, true);   // 플레이를 끄면 덜어 둔 로그인 저장값을 되돌린다
                const string dev = "http://127.0.0.1:5099";
                MessagePanelManager.EditorBase = dev;
                ProfileManager.EditorBase = dev;
                FollowManager.EditorBase = dev;
                R0926HuntApi.EditorBase = dev;
                R0926HuntApi.EditorTestUser = SfMe;
                DevPost(SfMe, "/api/dev/search_reset", "{}");
                SfLogin(true);
            });
            Check(() => LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn && LoginManager.Instance.CurrentUser.id == SfMe, "준비: 시험 아이디 (로그인 이벤트 없이)");

            // 1) 메세지 창에서 아이디 검색 — 대화가 없는 사람도 나온다
            Do(() =>
            {
                var m = Mpm();
                m.messagePanel.SetActive(true);
                if (m.searchInput != null) { m.searchInput.gameObject.SetActive(true); m.searchInput.text = "goosoon"; }
            });
            Sleep(0.3f);
            Do(() => Mpm()?.searchButton?.onClick.Invoke());   // 돋보기 → 서버 검색
            Wait(() => SfRow("goosoon56") != null, 8f, "검색 결과 받음");
            Sleep(0.5f);
            Shot("searchfollow_results");
            Do(() => log.Add("  알약: goosoon56=" + SfPillText("goosoon56") + " · friend=" + SfPillText("goosoon_friend") + " · fan=" + SfPillText("goosoon_fan")
                             + " · blocked=" + (SfPillText("goosoon_blocked") ?? "없음")));
            Check(() => SfRow("goosoon_blocker") == null, "나를 차단한 사람은 검색에 안 나옴");
            Check(() => SfRow("goosoon_blocked") != null && SfPillText("goosoon_blocked") == null, "내가 차단한 사람은 알약 없음");
            Check(() => SfPillText("goosoon56") == "팔로우", "대화 없는 사람 → 오른쪽 끝 '팔로우'");
            Check(() => SfPillText("goosoon_fan") == "맞팔로우", "나를 팔로우하는 사람 → '맞팔로우'");
            Check(() => SfPillText("goosoon_friend") == "팔로잉", "이미 팔로우 → '팔로잉'");
            Check(() => SfPreviewClear("goosoon56"), "미리보기 글이 알약 밑으로 안 들어감");

            // 2) 검색 중 새 알림이 와도 결과 그대로
            Do(() => Mpm()?.AddLocationNotificationFromPush("검색 중 알림 시험", "search test", SfNote));
            Sleep(0.5f);
            Check(() => SfRow("goosoon56") != null && SfRow("goosoon_fan") != null, "검색 중 새 알림이 와도 결과 그대로 (예전엔 대화 목록으로 바뀜)");

            // 3) '팔로우' 알약 → 바로 팔로우
            Do(() => SfTap(SfPill("goosoon56")));
            Wait(() => SfPillText("goosoon56") == "팔로잉", 6f, "알약 → 팔로우");
            Check(() => SfPillText("goosoon56") == "팔로잉" && SfDevFollowing("hunt_test_s1"), "'팔로우' 누르면 바로 팔로우 → '팔로잉'");

            // 4) 아바타 → 그 사람 프로필 · 프로필에서 맞팔로우 → 닫으면 검색 결과로 (알약도 맞춤)
            Do(() => SfTap(SfRow("goosoon_fan")?.Find("Content/Avatar")));
            Wait(() => Active("FullProfilePanel") && SfFollowState() != "?", 8f, "아바타 → 프로필");
            Sleep(0.6f);
            Shot("searchfollow_profile");
            Do(() => log.Add("  프로필: " + ProfileManager.Instance?.usernameText?.text + " · 버튼 " + SfFollowState() + " · 열린 창: " + OpenPanels()));
            Check(() => Active("FullProfilePanel") && !Active("MessagePanel") && ProfileManager.Instance.usernameText.text == "goosoon_fan",
                "아바타 → 그 사람 프로필 (메세지 창은 뒤로)");
            Check(() => SfFollowState() == "follow", "팔로우 안 한 사람 → 프로필에 '팔로우' 버튼");
            Do(() => ProfileManager.Instance.followButton.onClick.Invoke());
            Wait(() => SfFollowState() == "following", 6f, "프로필에서 팔로우");
            Do(() => ProfileManager.Instance.CloseFullProfile());
            Wait(() => Active("MessagePanel") && SfPillText("goosoon_fan") == "팔로잉", 6f, "프로필 닫음");
            Check(() => !Active("FullProfilePanel") && Active("MessagePanel") && SfRow("goosoon56") != null && Mpm().searchInput.text == "goosoon",
                "프로필 닫으면 검색 결과 그대로");
            Check(() => SfPillText("goosoon_fan") == "팔로잉" && SfDevFollowing("hunt_test_s3"), "프로필에서 팔로우한 것이 그 줄 알약에도 ('맞팔로우' → '팔로잉')");

            // 5) '팔로잉' 알약은 끊지 않고 프로필로 (취소는 프로필에서) → 취소하고 닫으면 알약 '맞팔로우'
            Do(() => SfTap(SfPill("goosoon_friend")));
            Wait(() => Active("FullProfilePanel") && SfFollowState() != "?", 8f, "'팔로잉' 알약 → 프로필");
            Check(() => SfFollowState() == "following" && SfDevFollowing("hunt_test_s2"), "'팔로잉' 알약은 끊지 않고 프로필로");
            Do(() => ProfileManager.Instance.followedButton.onClick.Invoke());
            Wait(() => SfFollowState() == "follow", 6f, "프로필에서 취소");
            Do(() => ProfileManager.Instance.CloseFullProfile());
            Wait(() => Active("MessagePanel") && SfPillText("goosoon_friend") == "맞팔로우", 6f, "프로필 닫음");
            Sleep(0.3f);
            Shot("searchfollow_back");
            Check(() => SfPillText("goosoon_friend") == "맞팔로우" && !SfDevFollowing("hunt_test_s2"), "프로필에서 취소 → 그 줄 알약 '맞팔로우' (상대는 나를 팔로우)");

            // 6) 검색 끝(X) → 대화 목록
            Do(() => Mpm()?.searchButton?.onClick.Invoke());
            Sleep(0.5f);
            Check(() => SfRow("goosoon56") == null && SfRow("goosoon_fan") == null, "검색을 끝내면 대화 목록으로");

            // 정리 — 시험 알림 줄 · 시험 아이디 · 시험 서버 주소
            Do(() =>
            {
                var m = Mpm();
                if (m != null)
                    typeof(MessagePanelManager).GetMethod("DeleteSystemNotification", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(m, new object[] { SfNote, null });
                SfLogin(false);
                MessagePanelManager.EditorBase = null;
                ProfileManager.EditorBase = null;
                FollowManager.EditorBase = null;
                R0926HuntApi.EditorBase = null;
                R0926HuntApi.EditorTestUser = null;
                DevPost(SfMe, "/api/dev/search_reset", "{}");
                if (m != null && m.messagePanel != null) m.messagePanel.SetActive(false);
            });
            Sleep(0.3f);
            Check(() => !PlayerPrefs.GetString("SystemNotifications", "").Contains(SfNote), "시험 알림 줄 지움");
        }

        // N2·N3·N15 (10-21) — 지도의 친구 핀·친구 목록 줄을 누르면 그 사람 프로필. 목록 시트가 프로필보다 위 층이라 열린 채 띄우면 카드가 시트 뒤에 겹쳤다 —
        // 시트는 닫았다가 프로필을 닫으면 보던 자리 그대로 다시 연다. 팔로우 중이면 '팔로우 중 | 메세지 보내기' 한 줄 · 프로필 사진을 눌러도 대화창 (예전엔 프로필만 닫혔다).
        // 친구 위치·프로필·팔로우·대화는 시험 서버(127.0.0.1:5099)의 고정 답. 시험 토큰은 덜어 둔 로그인 자리에만 두고 플레이를 끄면 되돌린다
        private const string MpMe = "hunt_test_mpme", MpFriend = "hunt_test_s2", MpFan = "hunt_test_s3";
        private static readonly List<string> mpErrors = new List<string>();
        private static void MpLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception) mpErrors.Add(msg);
        }
        private static void PlanMapProfile()
        {
            const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var T = typeof(R0926FriendsMap);
            R0926FriendsMap map = null;
            string viewBackup = null;
            float halfW = 0f;
            Func<string, Transform> pin = id => (T.GetField("pinLayer", F)?.GetValue(map) as RectTransform)?.Find("Pin_" + id);
            Func<string, Transform> row = id => (T.GetField("listContent", F)?.GetValue(map) as RectTransform)?.Find("Friend_" + id);
            Func<Transform, bool> on = t => t != null && t.gameObject.activeInHierarchy;
            Action<Transform> tap = t =>
            {
                if (t == null) { log.Add("  누를 것 없음"); return; }
                UnityEngine.EventSystems.ExecuteEvents.Execute(t.gameObject, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left }, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            };
            Func<ProfileManager> pm = () => ProfileManager.Instance;
            Func<string> who = () => pm()?.usernameText?.text;
            Func<bool> msgOn = () => on(pm()?.messageButton?.transform);
            Func<float> width = () => { var b = pm()?.followButton?.transform as RectTransform; return b != null ? b.rect.width : -1f; };
            // '팔로우 중 | 메세지 보내기' — 같은 줄에 폭 반씩 · 사이를 두고
            Func<bool> pairOk = () =>
            {
                var a = pm()?.followedButton?.transform as RectTransform;
                var b = pm()?.messageButton?.transform as RectTransform;
                if (!on(a) || !on(b)) return false;
                var ca = new Vector3[4];
                var cb = new Vector3[4];
                a.GetWorldCorners(ca);
                b.GetWorldCorners(cb);
                float s = a.lossyScale.x > 0f ? a.lossyScale.x : 1f;
                float wa = (ca[2].x - ca[0].x) / s, wb = (cb[2].x - cb[0].x) / s, gap = (cb[0].x - ca[2].x) / s, dy = Mathf.Abs(ca[0].y - cb[0].y) / s;
                halfW = wa;
                log.Add($"  한 줄: 팔로우 중 {wa:F0} · 메세지 보내기 {wb:F0} · 사이 {gap:F0} · 높이 차 {dy:F1}");
                return Mathf.Abs(wa - wb) < 1f && gap > 8f && dy < 1f;
            };
            Func<double[]> center = () =>
            {
                var vp = (RectTransform)map.transform;
                object n = T.GetMethod("ToNorm", F).Invoke(map, new object[] { vp.rect.size / 2f });
                var a = new object[] { n, null, null };
                T.GetMethod("LatLon", SF).Invoke(null, a);
                return new[] { (double)a[1], (double)a[2] };
            };
            Func<string, int> dmCount = kind =>
            {
                string r = DevGet(MpMe, "/api/dev/dm_log"), key = "\"kind\":\"" + kind + "\"";
                int n = 0;
                for (int i = r.IndexOf(key, StringComparison.Ordinal); i >= 0; i = r.IndexOf(key, i + 1, StringComparison.Ordinal)) n++;
                return n;
            };
            Func<string> chatTitle = () => Mpm()?.chatRoomTitle?.text;

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                SessionState.SetBool(LoginReadyKey, true);   // 플레이를 끄면 덜어 둔 로그인 저장값을 되돌린다
                const string dev = "http://127.0.0.1:5099";
                MessagePanelManager.EditorBase = dev;
                ProfileManager.EditorBase = dev;
                FollowManager.EditorBase = dev;
                P2PManager.EditorBase = dev;
                FirebaseNotification.EditorBase = dev;
                R0926FriendsMap.EditorBase = dev;
                R0926HuntApi.EditorBase = dev;
                R0926HuntApi.EditorTestUser = MpMe;
                R0926MapTiles.EditorBase = "http://127.0.0.1:9";   // 닫힌 자리 — 지도 타일 없이 (진짜 서버의 타일을 부르지 않는다)
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
                viewBackup = PlayerPrefs.HasKey("R0926Map.View") ? PlayerPrefs.GetString("R0926Map.View") : null;
                PlayerPrefs.DeleteKey("R0926Map.View");   // 처음 열면 친구가 다 보이게
                PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, LocalTestLogin + MpMe.Substring("hunt_test_".Length));
                DevPost(MpMe, "/api/dev/search_reset", "{}");   // 나 ↔ s2 맞팔 · s3 → 나만
                DevGet(MpMe, "/api/dev/dm_log?reset=1");
                SfLogin(true, MpMe);
                mpErrors.Clear();
                Application.logMessageReceived -= MpLog;
                Application.logMessageReceived += MpLog;
            });
            Check(() => LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn && LoginManager.Instance.CurrentUser.id == MpMe, "준비: 시험 아이디 (로그인 이벤트 없이)");

            // 1) 목록 → 지도 · 친구 핀 둘
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => Modes()?.Show(1));
            Sleep(0.8f);
            Do(() => map = UnityEngine.Object.FindAnyObjectByType<R0926FriendsMap>(FindObjectsInactive.Include));
            Wait(() => on(pin(MpFriend)) && on(pin(MpFan)) && on(row(MpFriend)) && on(row(MpFan)), 10f, "친구 핀·줄 받음 (시험 서버)");
            Sleep(0.6f);
            Shot("mapprofile_map");
            Do(() => log.Add($"  맞힘 검사 화면 {Screen.width}x{Screen.height} · 핀 맨 위: " + TopHitAt(pin(MpFriend) as RectTransform) + " · 줄 맨 위: " + TopHitAt(row(MpFan) as RectTransform)));
            HitCheck(() => pin(MpFriend) as RectTransform, false, "친구 핀이 가려지지 않음");

            // 2) 핀 → 그 사람 프로필 · 시트는 닫히고 프로필만 · 팔로우 중이면 '팔로우 중 | 메세지 보내기'
            Do(() => tap(pin(MpFriend)));
            Wait(() => Active("FullProfilePanel") && who() == "goosoon_friend" && SfFollowState() == "following", 8f, "핀 → 프로필");
            Sleep(0.8f);
            Shot("mapprofile_profile_following");
            Do(() => log.Add("  열린 창: " + OpenPanels() + " · 도크 목록 " + (Active("Dock0926/List_Button") ? "보임" : "숨음") + " · 도크 맨 위: " + TopHit("Dock0926/List_Button")));
            Check(() => OpenPanels() == "FullProfilePanel" && !Active("ListPanel"), "핀 → 그 사람 프로필만 (목록 시트는 닫혀 겹치지 않음)");
            Check(() => Active("Dock0926/List_Button"), "도크 '목록'은 제자리");
            HitCheck(() => Find("Dock0926/List_Button") as RectTransform, true, "도크 '목록'은 프로필 덮개에 가려 눌리지 않음");
            Check(() => SfFollowState() == "following" && msgOn() && pairOk(), "팔로우 중 → '팔로우 중 | 메세지 보내기' 한 줄 (폭 반씩)");
            HitCheck(() => pm().messageButton.transform as RectTransform, false, "'메세지 보내기'가 가려지지 않음");
            Check(() => (pm().messageButton.GetComponentInChildren<Text>(true)?.text ?? "") == "메세지 보내기", "글: '메세지 보내기'");

            // 3) 프로필 닫기 → 시트가 다시 · 지도는 누른 친구 자리 그대로
            Do(() => pm().closeButton.onClick.Invoke());
            Wait(() => Active("ListPanel") && !Active("FullProfilePanel"), 5f, "프로필 닫음");
            Sleep(0.8f);
            Shot("mapprofile_back_map");
            Do(() => { var c = center(); log.Add($"  지도 가운데 {c[0]:F4}, {c[1]:F4} · 탭 {Modes()?.Current}"); });
            Check(() => Active("ListPanel") && Modes()?.Current == 1 && Active("ListPanel/DockMirror0926/XButton_List") && !Active("Dock0926/List_Button"),
                "프로필을 닫으면 목록 시트가 지도 탭 그대로 다시 열림 (도크 X 도)");
            Check(() => { var c = center(); return Math.Abs(c[0] - 37.5665) < 0.05 && Math.Abs(c[1] - 126.978) < 0.05; }, "지도는 누른 친구 자리 그대로 (가다 만 곳이 아니라)");
            Check(() => on(row(MpFriend)?.Find("Sel")), "누른 친구 줄이 골라진 채");

            // 4) 친구 목록 줄 → 프로필 · 팔로우 안 한 사람은 '팔로우' 한 칸 (메세지 버튼 없음) · 뒤로가기로 닫으면 시트가 다시
            Do(() => tap(row(MpFan)));
            Wait(() => Active("FullProfilePanel") && who() == "goosoon_fan" && SfFollowState() != "?", 8f, "줄 → 프로필");
            Sleep(0.8f);
            Shot("mapprofile_profile_follow");
            Check(() => OpenPanels() == "FullProfilePanel", "친구 줄 → 그 사람 프로필만");
            Do(() => log.Add("  팔로우 폭 " + width().ToString("F0")));
            Check(() => SfFollowState() == "follow" && !msgOn() && Mathf.Abs(width() - (2f * halfW + 24f)) < 2f,
                "팔로우 안 한 사람 → '팔로우' 한 칸 (메세지 버튼 없음 · 폭 그대로)");
            Back();
            Wait(() => Active("ListPanel") && !Active("FullProfilePanel"), 5f, "뒤로가기 → 프로필 닫음");
            Sleep(0.6f);
            Check(() => OpenPanels() == "ListPanel" && Modes()?.Current == 1, "뒤로가기로 닫아도 목록 시트가 지도 그대로 (하나만)");
            Back();
            Wait(() => !Active("ListPanel"), 5f, "뒤로가기 → 시트 닫음");
            Sleep(0.6f);
            Check(() => OpenCount() == 0 && Active("Dock0926/List_Button"), "한 번 더 뒤로가기 → 시트도 닫힘 (남는 창 없음)");

            // 5) '메세지 보내기' → 그 사람 대화창 (프로필·시트는 닫고 되살아나지 않음)
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Wait(() => on(pin(MpFriend)), 6f, "다시 지도");
            Do(() => tap(pin(MpFriend)));
            Wait(() => Active("FullProfilePanel") && who() == "goosoon_friend" && msgOn(), 8f, "핀 → 프로필 (팔로우 중)");
            Sleep(0.5f);
            Do(() => tap(pm().messageButton.transform));
            Wait(() => Active("ChatRoomPanel"), 6f, "'메세지 보내기' → 대화창");
            Sleep(1.0f);
            Shot("mapprofile_chat");
            Do(() => log.Add("  열린 창: " + OpenPanels() + " · 대화 제목 " + chatTitle() + " · 서버 대화 " + dmCount("conversation") + " · 읽음 " + dmCount("read_all")));
            Check(() => OpenPanels() == "ChatRoomPanel" && chatTitle() == "goosoon_friend", "'메세지 보내기' → 그 사람 대화창만 (프로필·목록 시트 닫힘)");
            Check(() => dmCount("conversation") >= 1, "대화는 시험 서버에서 받음 (운영 서버에 시험 아이디로 묻지 않음)");
            Back();
            Wait(() => !Active("ChatRoomPanel"), 5f, "뒤로가기 → 대화창 닫음");
            Sleep(0.8f);
            Do(() => log.Add("  대화창 닫은 뒤: " + OpenPanels()));
            Check(() => OpenPanels() == "MessagePanel", "대화창을 닫으면 메세지 목록 하나만 (프로필·목록 시트가 다시 겹쳐 뜨지 않음)");
            CloseAll();

            // 6) 프로필 사진 → 대화창 (예전엔 사진을 눌러도 프로필만 닫혔다)
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Wait(() => on(pin(MpFan)), 6f, "다시 지도");
            Do(() => tap(pin(MpFan)));
            Wait(() => Active("FullProfilePanel") && who() == "goosoon_fan" && SfFollowState() != "?", 8f, "핀 → 프로필 (팔로우 안 함)");
            Sleep(0.5f);
            Do(() => log.Add("  사진 맨 위: " + TopHit("FullProfilePanel/Content/TopPanel/AvatarMask")));
            HitCheck(() => Find("FullProfilePanel/Content/TopPanel/AvatarMask") as RectTransform, false, "프로필 사진이 가려지지 않음");
            Do(() => tap(pm().avatarButton.transform));
            Wait(() => Active("ChatRoomPanel"), 6f, "사진 → 대화창");
            Sleep(0.8f);
            Shot("mapprofile_avatar_chat");
            Check(() => OpenPanels() == "ChatRoomPanel" && chatTitle() == "goosoon_fan", "팔로우 안 한 사람도 사진을 누르면 그 사람 대화창");

            // 7) 대화창에서 연 그 사람 프로필 → '메세지 보내기'는 같은 대화창으로 돌아감 (새로 열지 않음).
            //    지금 대화창 머리에는 사진 칸이 없어(chatRoomAvatar 비어 있음) 사진 대신 그 길을 바로 부른다
            Do(() => DevPost(MpMe, "/api/follow", "{\"following_id\":\"" + MpFan + "\"}"));
            int convBefore = 0;
            Do(() => convBefore = dmCount("conversation"));
            Do(() => Mpm().OpenProfileFromChatRoom(MpFan, "goosoon_fan"));
            Wait(() => Active("FullProfilePanel") && who() == "goosoon_fan" && msgOn(), 8f, "대화창에서 프로필 (방금 팔로우)");
            Sleep(0.5f);
            Check(() => OpenPanels() == "FullProfilePanel", "대화창에서 연 프로필 — 대화창은 뒤로 숨음");
            Do(() => tap(pm().messageButton.transform));
            Wait(() => Active("ChatRoomPanel") && !Active("FullProfilePanel"), 6f, "'메세지 보내기' → 대화창");
            Sleep(0.6f);
            Check(() => OpenPanels() == "ChatRoomPanel" && chatTitle() == "goosoon_fan" && dmCount("conversation") == convBefore,
                "그 사람 대화창에서 연 프로필의 '메세지 보내기' → 보던 대화창 그대로 (다시 받지 않음)");
            Back();
            Wait(() => !Active("ChatRoomPanel"), 5f, "대화창 닫음");
            Sleep(0.6f);
            Check(() => OpenPanels() == "MessagePanel", "대화창을 닫으면 메세지 목록 하나만");

            // 8) 근처 사람(P2P) 목록에 정확히 온 친구 → 서버의 약 1km 칸 가운데가 아니라 그 자리 (10-21 한자리의 두 휴대폰이 서로 약 500m 떨어져 보였다)
            Func<string, object> friendOf = id =>
            {
                foreach (var o in (System.Collections.IList)T.GetField("friends", F).GetValue(map))
                    if ((string)o.GetType().GetField("user_id").GetValue(o) == id) return o;
                return null;
            };
            Func<string, string, object> fv = (id, n) => { var o = friendOf(id); return o?.GetType().GetField(n).GetValue(o); };
            Func<double, double, Vector2> viewAt = (la, lo) =>
                (Vector2)T.GetMethod("ToView", F).Invoke(map, new[] { T.GetMethod("NormD", SF).Invoke(null, new object[] { la, lo }) });
            Action<bool> nearby = add =>
            {
                if (!add) { P2PManager.Instance?.RemoveTestUserAvatar(MpFriend); return; }
                // 에디터의 가상 사용자(1초마다 근처 사람을 넣는다)가 시험이 넣은 근처 사람 목록을 덮어쓰지 않게
                var vt = UnityEngine.Object.FindAnyObjectByType<P2PVirtualUserTester>();
                if (vt != null) { vt.StopSimulation(); vt.StopAllCoroutines(); }
                P2PManager.Instance?.InjectTestUsersData("{\"users\":[{\"user_id\":\"" + MpFriend + "\",\"username\":\"goosoon_friend\",\"latitude\":37.5707,\"longitude\":126.9801,\"altitude\":0,\"distance\":470}]}");
            };
            CloseAll();
            Do(() => PlayerPrefs.DeleteKey("R0926Map.View"));   // 처음 열 때처럼 친구가 다 보이게
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => Modes()?.Show(1));
            Wait(() => on(pin(MpFriend)) && on(pin(MpFan)), 8f, "다시 지도 · 친구 핀 둘");
            Check(() => fv(MpFriend, "exact") is bool b0 && !b0, "근처 목록에 오기 전엔 서버 자리 (약 1km 칸)");
            Do(() => nearby(true));
            Wait(() => fv(MpFriend, "exact") is bool b1 && b1, 8f, "근처 친구 → 정확한 자리 (5초마다 확인)");
            Sleep(0.3f);
            Shot("mapprofile_nearby_exact");
            Do(() =>
            {
                var q = viewAt(37.5707, 126.9801);
                var pr = pin(MpFriend) as RectTransform;
                log.Add($"  근처 친구 {fv(MpFriend, "lat")}, {fv(MpFriend, "lon")} · 핀 {(pr != null ? pr.anchoredPosition.ToString() : "없음")} · 그 자리 ({q.x:F1}, {-q.y:F1}) · 줄 " +
                        row(MpFriend)?.Find("Sub")?.GetComponent<Text>()?.text);
            });
            Check(() => Math.Abs((float)fv(MpFriend, "lat") - 37.5707) < 1e-4 && Math.Abs((float)fv(MpFriend, "lon") - 126.9801) < 1e-4,
                "근처 친구 = 근처 사람 목록의 자리 (서버가 보낸 37.5665, 126.978 이 아니라)");
            Check(() => { var pr = pin(MpFriend) as RectTransform; var q = viewAt(37.5707, 126.9801); return pr != null && Vector2.Distance(pr.anchoredPosition, new Vector2(q.x, -q.y)) < 1f; },
                "핀도 그 자리에");
            Check(() => fv(MpFan, "exact") is bool b2 && !b2 && Math.Abs((float)fv(MpFan, "lat") - 36.3504) < 1e-4, "근처 목록에 없는 친구는 서버 자리 그대로");
            Do(() => nearby(false));

            Check(() => mpErrors.Count == 0, "오류 없음" + (mpErrors.Count > 0 ? " — " + mpErrors[0] : ""));
            Do(() => { foreach (var e in mpErrors) log.Add("  오류: " + e); });

            // 정리 — 시험 서버 주소 · 시험 아이디 · 보던 지도 자리
            CloseAll();
            Do(() =>
            {
                Application.logMessageReceived -= MpLog;
                SfLogin(false);
                MessagePanelManager.EditorBase = null;
                ProfileManager.EditorBase = null;
                FollowManager.EditorBase = null;
                P2PManager.EditorBase = null;
                FirebaseNotification.EditorBase = null;
                R0926FriendsMap.EditorBase = null;
                R0926HuntApi.EditorBase = null;
                R0926HuntApi.EditorTestUser = null;
                R0926MapTiles.EditorBase = null;
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                PlayerPrefs.DeleteKey(LoginManager.AUTH_TOKEN_KEY);
                if (viewBackup != null) PlayerPrefs.SetString("R0926Map.View", viewBackup); else PlayerPrefs.DeleteKey("R0926Map.View");
                PlayerPrefs.Save();
                DevPost(MpMe, "/api/dev/search_reset", "{}");
            });
        }

        // B8 (10-18) — 새 팔로워 알림을 누르면 덮인 창(추가 창까지)을 닫고 내 팔로워 탭 · 그 사람을 맨 위에 잠깐 밝게.
        // 목록은 시험 서버(127.0.0.1:5099)의 고정 목록, 로그인 대신 시험 아이디 — 운영 서버에 시험 아이디로 묻지 않는다
        private const string FnMe = "hunt_test_fn_me", FnNew = "소제이_공방", FnNote = "follow_test_b8";

        private static Transform FollowFirst()
        {
            var fm = Fm();
            if (fm == null || fm.contentParent == null) return null;
            foreach (Transform c in fm.contentParent) if (c.gameObject.activeSelf && c.name.StartsWith("Item_")) return c;
            return null;
        }
        private static string FollowFirstName() { var c = FollowFirst(); return c != null ? c.name.Substring(5) : null; }
        private static float FollowFirstGlow() { var c = FollowFirst(); var img = c != null ? c.GetComponent<Image>() : null; return img != null ? img.color.a : -1f; }
        private static bool HuntPushPending() =>
            (bool)typeof(R0926HuntPush).GetField("pending", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
        private static bool FollowNoteSaved()
        {
            var list = typeof(MessagePanelManager).GetField("conversations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(MessagePanelManager.Instance) as List<ConversationSummary>;
            return list != null && list.Exists(c => c.notificationId == FnNote);
        }
        // WOOPANG 대화의 새 팔로워 말풍선 — 말풍선 칸(BubbleContainer)에 단 버튼
        private static Button FollowBubble()
        {
            var m = Mpm();
            if (m == null || m.chatMessageContent == null || !m.chatMessageContent.gameObject.activeInHierarchy) return null;
            foreach (var t in m.chatMessageContent.GetComponentsInChildren<Text>(false))
                if (t.text.Contains(FnNew + "님이 회원님을 팔로우")) return t.GetComponentInParent<Button>();
            return null;
        }

        private static void PlanFollowNotif()
        {
            var followData = new Dictionary<string, string> { { "notification_type", "follow" }, { "from_username", FnNew } };
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                FollowManager.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = FnMe;
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Check(() => !R0926HuntPush.OpenFollow(new Dictionary<string, string> { { "msg_type", "hunt_new" }, { "hunt_id", "1" } })
                     && !R0926HuntPush.OpenFollow(new Dictionary<string, string> { { "notification_type", "like" }, { "from_username", FnNew } }),
                "보물찾기·좋아요 알림은 팔로워 길로 안 감");

            // 1) 추가 창을 연 채 알림을 누름 → 추가 창까지 닫고 내 팔로워 탭
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.4f);
            Check(() => Active("UploadPage"), "준비: 추가 창 열림");
            Check(() => R0926HuntPush.OpenFollow(followData), "새 팔로워 알림 → 팔로워 길");
            Wait(() => Active("FollowPanel") && FollowFirst() != null, 10f, "팔로워 목록 받음");
            Sleep(0.25f);
            Shot("follownotif_open");
            Do(() => log.Add("  첫 줄: " + FollowFirstName() + " · 밝기 " + FollowFirstGlow().ToString("0.000") + " · 열린 창: " + OpenPanels()));
            Check(() => Active("FollowPanel") && !Active("UploadPage"), "추가 창은 닫히고 팔로우 창만");
            Check(() => { var fm = Fm(); return fm != null && fm.titleText != null && fm.titleText.text == FnMe; }, "내 목록 (제목 = 내 아이디)");
            Check(() => FollowFirstName() == FnNew, "새 팔로워가 맨 위 (원래 순서로는 다섯째)");
            Check(() => FollowFirstGlow() > 0.03f, "그 줄을 밝힘");
            Check(() => { R0926HuntPush.OpenFollow(followData); return !HuntPushPending(); }, "같은 알림이 두 길로 와도(FCM 콜백 · 인텐트) 한 번만");
            Sleep(3.4f);
            Shot("follownotif_after");
            Check(() => FollowFirstGlow() >= 0f && FollowFirstGlow() < 0.005f, "밝힘은 잠깐 — 3초쯤 뒤 꺼짐");

            // 2) 탭을 오가도 맨 위 그대로 · 다시 밝히지는 않음
            Do(() => Fm()?.followingTab?.onClick.Invoke());
            Sleep(1.2f);
            Do(() => Fm()?.followersTab?.onClick.Invoke());
            Sleep(1.2f);
            Check(() => FollowFirstName() == FnNew && FollowFirstGlow() < 0.005f, "팔로잉 갔다 와도 맨 위 그대로 (밝힘은 한 번)");

            // 3) 도크 '닫기' → 메인
            Do(() => Tap("FollowPanel/DockMirror0926/DockClose0926/ClosePx0926"));
            Wait(() => !Active("FollowPanel"), 4f, "도크 '닫기'로 닫힘");
            Sleep(0.4f);
            Check(() => !Active("FollowPanel") && !Active("FullProfilePanel") && !Active("UploadPage"), "닫기 → 메인 (프로필·추가 창 안 뜸)");

            // 4) 보통으로 열면(프로필 → 팔로워) 맨 위 고정·밝힘 없음
            Do(() => Fm()?.ShowFollowers(FnMe, FnMe));
            Wait(() => Active("FollowPanel") && FollowFirst() != null, 8f, "보통으로 열기");
            Sleep(0.3f);
            Check(() => FollowFirstName() != FnNew && FollowFirstGlow() < 0.005f, "보통으로 열면 원래 순서 · 밝힘 없음");
            Do(() => Fm()?.CloseAway());
            Sleep(0.4f);

            // 5) 메세지 탭의 '새로운 팔로워' 알림 → 내 팔로워 탭. (10-21) 알림은 따로 줄이 아니라 WOOPANG 대화 안의 말풍선 — 그 말풍선을 누른다
            Do(() => { var mpm = MessagePanelManager.Instance; if (mpm != null && mpm.messagePanel != null) mpm.messagePanel.SetActive(true); });
            Sleep(0.8f);
            Do(() => MessagePanelManager.Instance?.AddLocationNotificationFromPush("새로운 팔로워", FnNew + "님이 회원님을 팔로우했습니다", FnNote, 0f, 0f, 0f, "", FnNew));
            Sleep(0.6f);
            Shot("follownotif_msgrow");
            Do(TapWoopangRow);
            Wait(() => Active("ChatRoomPanel") && !Active("MessagePanel") && FollowBubble() != null, 10f, "WOOPANG 대화에 새 팔로워 말풍선");
            Sleep(0.3f);
            Shot("follownotif_bubble");
            Do(() => { var b = FollowBubble(); if (b != null) b.onClick.Invoke(); else log.Add("  새 팔로워 말풍선 못 찾음"); });
            Wait(() => Active("FollowPanel") && FollowFirst() != null, 10f, "말풍선 → 팔로워 탭");
            Sleep(0.25f);
            Shot("follownotif_fromrow");
            Check(() => Active("FollowPanel") && !Active("MessagePanel") && !Active("ChatRoomPanel"), "새 팔로워 말풍선 → 팔로워 탭 (메세지·대화 창 닫힘)");
            Check(() => FollowFirstName() == FnNew && FollowFirstGlow() > 0.03f, "말풍선으로 열어도 새 팔로워 맨 위 · 밝힘");

            // 정리 — 시험 알림 줄은 저장값에서도 지운다
            Do(() =>
            {
                var mpm = MessagePanelManager.Instance;
                if (mpm != null)
                    typeof(MessagePanelManager).GetMethod("DeleteSystemNotification", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(mpm, new object[] { FnNote, null });
                Fm()?.CloseAway();
                FollowManager.EditorBase = null;
                R0926HuntApi.EditorTestUser = null;
            });
            Sleep(0.3f);
            Check(() => !FollowNoteSaved() && !PlayerPrefs.GetString("SystemNotifications", "").Contains(FnNote), "시험 알림 줄 지움");
        }

        // B6 (10-18) — 주소 없이 불린 기본 그림이 그 사람 사진 칸을 차지하지 않는지 · 같은 사진은 한 번만 받는지
        // 사진은 시험용 로컬 서버(127.0.0.1:5097)에서 — 운영 서버는 건드리지 않는다
        private static readonly List<RectTransform> avatarBoxes = new List<RectTransform>();
        private static Sprite avatarFirstDefault;
        private static void PlanAvatar()
        {
            const string url = "http://127.0.0.1:5097/b6_avatar.png";
            Func<int, int> texW = i =>
            {
                var img = avatarBoxes.Count > i && avatarBoxes[i] != null ? avatarBoxes[i].Find("AvatarImage")?.GetComponent<Image>() : null;
                return img != null && img.sprite != null ? img.sprite.texture.width : -1;
            };
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                avatarBoxes.Clear();
                var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Exclude);
                for (int i = 0; i < 12; i++)
                {
                    var go = new GameObject("B6Avatar" + i, typeof(RectTransform));
                    go.transform.SetParent(canvas.rootCanvas.transform, false);
                    var rt = (RectTransform)go.transform;
                    rt.sizeDelta = new Vector2(60f, 60f);
                    rt.anchoredPosition = new Vector2(-330f + i * 60f, 0f);
                    avatarBoxes.Add(rt);
                }
                ProfileManager.LoadAvatarWithMaskAsync("b6_peer", null, avatarBoxes[0], "이웃");
            });
            Sleep(0.3f);
            Check(() => texW(0) == 128, "주소 없이 → 이름 기본 그림");
            Do(() => avatarFirstDefault = avatarBoxes[0].Find("AvatarImage").GetComponent<Image>().sprite);
            Do(() => { for (int i = 1; i <= 10; i++) ProfileManager.LoadAvatarWithMaskAsync("b6_peer", url, avatarBoxes[i], "이웃"); });
            Sleep(2.5f);
            Check(() => { for (int i = 1; i <= 10; i++) if (texW(i) != 37) return false; return true; }, "주소로 받은 진짜 사진 10칸 (기본 그림이 막지 않음)");
            Do(() => ProfileManager.LoadAvatarWithMaskAsync("b6_peer", null, avatarBoxes[11], "이웃"));
            Sleep(0.3f);
            Check(() => texW(11) == 37, "그 뒤 주소 없이 불러도 진짜 사진");
            Do(() => ProfileManager.LoadAvatarWithMaskAsync("b6_nophoto", null, avatarBoxes[0], "이웃"));
            Sleep(0.3f);
            Check(() => avatarFirstDefault != null && avatarBoxes[0].Find("AvatarImage").GetComponent<Image>().sprite == avatarFirstDefault, "같은 이름 기본 그림은 한 장을 다시 씀");
            Shot("avatar_b6");
            Do(() => { foreach (var b in avatarBoxes) if (b != null) UnityEngine.Object.Destroy(b.gameObject); avatarBoxes.Clear(); });
        }

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

        // ── D1 (10-15·16 대표님): 옆으로 밀다 아래로 내려도 세로 목록이 따라 내려오지 않는다 ──
        // 가상 터치 화면으로 손가락을 흉내 낸다 — 이벤트 시스템·목록·카드 넘기기·밀어 닫기가 진짜 손가락처럼 받는다
        private static Touchscreen ptTouch;
        private static InputSettings.EditorInputBehaviorInPlayMode ptFocusWas;
        private static float lockY0, lockDev;
        private static ScrollRect lockList;

        private static void PlanScrollLock()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                ptFocusWas = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;   // 에디터 창이 뒤에 있어도
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Check(() => UnityEngine.Object.FindAnyObjectByType<R0926ScrollLock>() != null, "목록 잠금이 캔버스에 붙어 있음");

            // 1) 추가 창 — 장소 · 보물찾기 카드 (10-15 사진: 보물찾기 카드를 옆으로 밀다 내리니 내용이 크게 내려왔다)
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            LockCase("추가 창 장소 카드 · 옆으로 밀다 아래로", () => CardList(0), new Vector2(-70f, 0f), new Vector2(0f, -420f), "lock_add_place", true);
            Do(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>()?.SwitchToPanel(2));
            Sleep(1.2f);
            LockCase("추가 창 보물찾기 카드 · 옆으로 밀다 아래로", () => CardList(2), new Vector2(70f, 0f), new Vector2(0f, -420f), "lock_add_hunt", true);

            // 2) 목록 가운데서도 — 옆으로 시작한 손가락은 목록을 굴리지 않는다
            Do(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>()?.SwitchToPanel(0));
            Sleep(1.2f);
            Do(() => { var sr = CardList(0); if (sr != null) sr.verticalNormalizedPosition = 0.5f; });
            Sleep(0.3f);
            LockCase("추가 창 장소 카드 가운데 · 옆으로 밀다 위아래로", () => CardList(0), new Vector2(-70f, 0f), new Vector2(0f, 300f), null, true);

            // 3) 위로 시작한 손가락은 예전처럼 굴린다
            Do(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>()?.SwitchToPanel(0));
            Sleep(1.2f);
            Do(() => { var sr = CardList(0); if (sr != null) sr.verticalNormalizedPosition = 1f; });
            Sleep(0.3f);
            LockCase("추가 창 장소 카드 · 위로 굴리기", () => CardList(0), new Vector2(0f, 300f), Vector2.zero, null, false);
            Do(() => { var sr = CardList(0); if (sr != null) sr.verticalNormalizedPosition = 1f; });
            Back();
            Sleep(1.0f);

            // 4) 팔로잉/팔로워 (10-16 사진) — 옆으로 밀다 아래로 · 맨 위에서 곧장 아래로는 창이 내려간다(밀어 닫기)
            Do(() => FillFollow(false));
            Sleep(1.0f);
            LockCase("팔로잉 목록 · 옆으로 밀다 아래로", () => PanelList("FollowPanel"), new Vector2(-70f, 0f), new Vector2(0f, -420f), "lock_follow", true);
            Sleep(0.4f);
            float sheetY0 = 0f, sheetDrop = 0f;
            Do(() => { var bg = Find("FollowPanel/Background") as RectTransform; sheetY0 = bg != null ? bg.anchoredPosition.y : 0f; sheetDrop = 0f; });
            LockCase("팔로잉 목록 맨 위 · 곧장 아래로", () => PanelList("FollowPanel"), new Vector2(0f, -150f), Vector2.zero, "lock_follow_pull", true, 20,
                () => { var bg = Find("FollowPanel/Background") as RectTransform; if (bg != null) sheetDrop = Mathf.Max(sheetDrop, sheetY0 - bg.anchoredPosition.y); });
            Do(() => log.Add($"  창이 내려간 만큼 {sheetDrop:0}"));
            Check(() => sheetDrop > 40f, "맨 위에서 곧장 아래로 — 목록 대신 창이 내려간다 (밀어 닫기 그대로)");
            Sleep(0.8f);
            Do(() => { if (Active("FollowPanel")) BackNow(); });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Sleep(0.8f);

            // 5) 메시지 목록 (10-15)
            Do(() => { var m = Mpm(); if (m != null && m.messagePanel != null) m.messagePanel.SetActive(true); });
            Sleep(1.0f);
            LockCase("메시지 목록 · 옆으로 밀다 아래로", () => PanelList("MessagePanel"), new Vector2(-70f, 0f), new Vector2(0f, -420f), "lock_message", true);

            Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
            });
        }

        // 손가락 한 번: 목록 위에서 눌러 first → second 로 움직이고 뗀다. still 이면 목록이 그대로여야, 아니면 움직여야 PASS
        private static void LockCase(string what, Func<ScrollRect> list, Vector2 first, Vector2 second, string shot, bool still, int perLeg = 8, Action extra = null)
        {
            Do(() =>
            {
                lockList = list();
                lockY0 = lockList != null ? lockList.content.anchoredPosition.y : 0f;
                lockDev = 0f;
                if (lockList == null) log.Add("  목록 없음: " + what);
            });
            Finger(() => lockList != null ? ListPoint(lockList) : (Vector2?)null, second == Vector2.zero ? new[] { first } : new[] { first, second }, perLeg,
                () => { if (lockList != null) lockDev = Mathf.Max(lockDev, Mathf.Abs(lockList.content.anchoredPosition.y - lockY0)); extra?.Invoke(); }, shot);
            Sleep(0.5f);
            Do(() =>
            {
                if (lockList == null) { log.Add("SKIP " + what + " (목록 없음)"); return; }
                float end = Mathf.Abs(lockList.content.anchoredPosition.y - lockY0);
                bool ok = still ? lockDev < 2f && end < 2f : lockDev > 30f;
                log.Add((ok ? "PASS " : "FAIL ") + what + $" — 끄는 동안 최대 {lockDev:0.#} · 뗀 뒤 {end:0.#} (목록 {lockList.name}, 높이 {lockList.content.rect.height:0}/{((RectTransform)lockList.transform).rect.height:0})");
            });
        }

        // 가상 손가락 — at 에서 누르고 legs 를 차례로, 한 프레임에 한 걸음씩 움직인 뒤 뗀다. 프레임마다 watch
        private static void Finger(Func<Vector2?> at, Vector2[] legs, int stepsPerLeg, Action watch, string shot)
        {
            Vector2 p = default;
            int leg = 0, k = 0, frame = -1;
            bool skip = false;
            steps.Enqueue(() =>
            {
                var a = at();
                skip = a == null || ptTouch == null;
                if (skip) return true;
                p = a.Value;
                leg = 0; k = 0;
                InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Began, position = p });
                frame = Time.frameCount;
                return true;
            });
            steps.Enqueue(() =>
            {
                if (skip) return true;
                if (Time.frameCount == frame) return false;   // 한 프레임에 한 걸음 — 에디터 갱신이 게임 프레임보다 잦다
                frame = Time.frameCount;
                watch?.Invoke();
                if (leg >= legs.Length) return true;
                p += legs[leg] / stepsPerLeg;
                InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = p });
                if (++k < stepsPerLeg) return false;
                k = 0;
                leg++;
                if (leg == legs.Length && shot != null) ScreenCapture.CaptureScreenshot(Path.Combine(dir, "test_" + shot + ".png"));
                return false;
            });
            steps.Enqueue(() => { if (skip || Time.frameCount > frame + 1) return true; watch?.Invoke(); return false; });
            Do(() => { if (!skip) InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = p }); });
        }

        // 목록 보이는 칸의 위쪽 ⅓ 가운데 (오버레이 캔버스 — 월드 좌표가 곧 화면 좌표)
        private static Vector2? ListPoint(ScrollRect sr)
        {
            var view = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
            var c = new Vector3[4];
            view.GetWorldCorners(c);
            float x = (c[0].x + c[2].x) * 0.5f, y = Mathf.Lerp(c[1].y, c[0].y, 0.33f);
            if (x < 0 || x > Screen.width || y < 0 || y > Screen.height) return null;
            return new Vector2(x, y);
        }

        private static ScrollRect CardList(int card)
        {
            var s = UnityEngine.Object.FindAnyObjectByType<SwipePanelController>();
            var p = s == null ? null : card == 0 ? s.panel1 : card == 1 ? s.panel2 : s.panel3;
            if (p == null) return null;
            foreach (var sr in p.GetComponentsInChildren<ScrollRect>())
                if (sr.vertical && !sr.horizontal && sr.isActiveAndEnabled) return sr;
            return null;
        }

        private static ScrollRect PanelList(string panel)
        {
            var t = Find(panel);
            if (t == null) return null;
            foreach (var sr in t.GetComponentsInChildren<ScrollRect>())
                if (sr.vertical && !sr.horizontal && sr.isActiveAndEnabled) return sr;
            return null;
        }

        // 친구 지도 깊은 확대 — 시험 타일 서버(127.0.0.1:5098 → 브이월드)로 단계별 화면 · 최소 단계·출처 확인 (2026-10-11)
        // 추가 창 카드 자리 (10-13 대표님) — 장소는 왼쪽에 붙이고 3D모델을 오른쪽에 살짝,
        // 3D모델·보물찾기는 가운데에 두고 옆 카드를 살짝 (3D모델은 양옆 · 보물찾기는 왼쪽)
        private static readonly float[] addMeasure = new float[12];   // 카드마다 왼쪽 여백 · 오른쪽 여백 · 왼쪽 카드 보이는 폭 · 오른쪽 카드 보이는 폭
        private static void PlanAddCenter()
        {
            SwipePanelController swipe = null;
            Func<RectTransform, Vector2> edges = rt =>
            {
                var wrap = (RectTransform)rt.parent;
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return new Vector2(wrap.InverseTransformPoint(c[0]).x - wrap.rect.xMin, wrap.InverseTransformPoint(c[2]).x - wrap.rect.xMin);
            };
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() => swipe = UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude));
            Check(() => swipe != null && swipe.panel3 != null, "추가 창 · 카드 세 장");
            for (int k = 0; k < 3; k++)
            {
                int i = k;
                Do(() => swipe?.SwitchToPanel(i));
                Sleep(1.2f);
                Do(() =>
                {
                    if (swipe == null || swipe.panel3 == null) return;
                    var p = new[] { swipe.panel1, swipe.panel2, swipe.panel3 };
                    float w = ((RectTransform)p[i].parent).rect.width;
                    var e = edges(p[i]);
                    float peekL = i > 0 ? Mathf.Max(0f, edges(p[i - 1]).y) : 0f;
                    float peekR = i < 2 ? Mathf.Max(0f, w - edges(p[i + 1]).x) : 0f;
                    addMeasure[i * 4] = e.x; addMeasure[i * 4 + 1] = w - e.y; addMeasure[i * 4 + 2] = peekL; addMeasure[i * 4 + 3] = peekR;
                    log.Add($"  카드 {i + 1}: 왼쪽 여백 {e.x:F0} · 오른쪽 여백 {w - e.y:F0} · 왼쪽 카드 {peekL:F0} · 오른쪽 카드 {peekR:F0} (틀 폭 {w:F0})");
                });
                Shot("add_" + (i + 1));
            }
            Check(() => Mathf.Abs(addMeasure[0] - 44f) < 3f && Mathf.Abs(addMeasure[3] - 110f) < 3f, "장소: 그대로 — 왼쪽에 붙고 3D모델이 오른쪽에 살짝");
            Check(() => Mathf.Abs(addMeasure[4] - addMeasure[5]) < 2f && addMeasure[6] > 20f && Mathf.Abs(addMeasure[6] - addMeasure[7]) < 2f, "3D모델: 가운데 · 장소·보물찾기가 양옆에 같은 폭으로 살짝");
            Check(() => Mathf.Abs(addMeasure[8] - addMeasure[9]) < 2f && addMeasure[10] > 20f, "보물찾기: 가운데 · 3D모델이 왼쪽에 살짝");
            Do(() => swipe?.SwitchToPanel(0));
            Sleep(1.0f);
            Check(() => swipe != null && Mathf.Abs(edges(swipe.panel1).x - 44f) < 3f, "다시 장소로 넘기면 제자리");
        }

        private static void PlanMapTiles()
        {
            const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            R0926FriendsMap map = null;
            R0926MapTiles tiles = null;
            Func<string, int> count = name => (typeof(R0926MapTiles).GetField(name, F)?.GetValue(tiles) as System.Collections.ICollection)?.Count ?? -1;
            Func<int> holes = () => tiles != null ? (int)typeof(R0926MapTiles).GetField("holes", F).GetValue(tiles) : -1;
            Func<double> maxZoom = () => Convert.ToDouble(typeof(R0926FriendsMap).GetProperty("MaxZoom", F).GetValue(map));
            Func<double> top = () => Convert.ToDouble(typeof(R0926FriendsMap).GetProperty("TopLevel", F).GetValue(map));
            Func<double, float> zoomAt = lv => (float)(maxZoom() / Math.Pow(2, top() - (lv < 0 ? top() : lv)));   // lv: 1:1 기준 단계 (< 0 = 가장 깊이)
            // 지금 화면에 쓰는 타일 — 다 받은 뒤엔 받는 단계의 칸들 그대로
            Func<HashSet<long>> used = () => typeof(R0926MapTiles).GetField("inUse", F)?.GetValue(tiles) as HashSet<long>;
            Func<int> fetchLevel = () => { int z = -1; var u = used(); if (u != null) foreach (var k in u) z = Math.Max(z, (int)(k >> 48)); return z; };
            Func<bool> oneLevel = () => { var u = used(); int z = fetchLevel(); if (u == null || u.Count == 0) return false; foreach (var k in u) if ((int)(k >> 48) != z) return false; return true; };
            string diskRoot = System.IO.Path.Combine(Application.temporaryCachePath, "maptiles");
            Action wipeDisk = () => { try { if (System.IO.Directory.Exists(diskRoot)) System.IO.Directory.Delete(diskRoot, true); } catch (Exception) { } };
            Do(() =>
            {
                R0926MapTiles.EditorBase = "http://127.0.0.1:5098";
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
                wipeDisk();   // 기기 캐시 없이 — 서버에서 받는 길부터
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
                log.Add("  최대 확대 " + maxZoom().ToString("0") + "배 · 타일 최대 단계 " + tiles.MaxLevel + " · 화면 dpi " + Screen.dpi.ToString("0")
                    + " → 타일을 " + tiles.DensityLevels.ToString("0.00") + "단계 덜 자세한 것으로 크게 · 가장 깊이 1:1 기준 " + top().ToString("0.00") + "단계");
                map.EditorPreview(true, 1f, 37.5f, 127f);
            });
            Sleep(2.0f);
            Check(() => count("wanted") == 0 && count("inflight") == 0, "세계 보기 — 6 단계보다 얕으면 타일을 묻지 않음");
            Check(() => tiles.MaxLevel == 18, "서버 최대 단계 18 (10-21 16→18)");
            Shot("tiles_world");
            var stops = new (float lv, float lat, float lon, string name)[]
            {
                (8f, 36.4f, 127.6f, "korea_z8"), (11f, 37.5665f, 126.978f, "seoul_z11"), (13f, 37.5665f, 126.978f, "seoul_z13"),
                (16f, 37.5665f, 126.978f, "seoul_z16"), (-1f, 37.5665f, 126.978f, "seoul_max"), (-1f, 36.6359f, 126.828f, "yesan_max"),
                (15f, 37.5283f, 126.9326f, "hunt_approx_z15"),
            };
            foreach (var st in stops)
            {
                Do(() => map.EditorPreview(true, zoomAt(st.lv), st.lat, st.lon));
                Sleep(0.5f);
                Wait(() => count("inflight") == 0, 20f, st.name + " 타일 받기 끝");
                Sleep(0.8f);
                Do(() =>
                {
                    var at = typeof(R0926MapTiles).GetField("attribution", F)?.GetValue(tiles) as Text;
                    log.Add("  " + st.name + ": 받는 단계 " + fetchLevel() + " · 화면 칸 " + (used()?.Count ?? -1) + " · 빈칸 " + holes()
                        + " · 메모리 타일 " + count("cache") + " · 출처 " + (at != null && at.enabled ? "'" + at.text + "'" : "(안 보임)"));
                });
                Shot("tiles_" + st.name);
                if (st.name == "seoul_z13")
                {
                    Check(() => holes() == 0 && used() != null && used().Count <= 40, "서울 13 — 빈칸 없음 · 한 화면 타일 40장 이하 (예전 60~70장)");
                    Check(() =>
                    {
                        var layer = typeof(R0926FriendsMap).GetField("pinLayer", F)?.GetValue(map) as RectTransform;
                        var a = layer != null ? layer.Find("Hunt_-1") : null;
                        var b = layer != null ? layer.Find("Hunt_-2") : null;
                        var at = a != null ? a.Find("Name/Text")?.GetComponent<Text>() : null;
                        return a != null && a.gameObject.activeSelf && b != null && b.gameObject.activeSelf && at != null && at.text.Length > 0;
                    }, "지도에 보물찾기 둘 (정확히 · 대략) — 금빛 상자 핀 + 게임 이름");
                    int panHoles = -1;
                    Do(() =>
                    {
                        map.EditorPreview(true, zoomAt(13), st.lat, st.lon + 360f / (1 << fetchLevel()));   // 받는 단계 한 칸만큼 옆으로
                        panHoles = holes();
                    });
                    Check(() => panHoles == 0, "옆으로 한 칸 옮기자마자 빈칸 없음 (위 단계 두 배 넓게 미리 받아 흐리게라도)");
                    Sleep(0.3f);
                    Wait(() => count("inflight") == 0, 20f, "seoul_z13 옆 칸 받기 끝");
                }
                if (st.name == "seoul_z16")
                {
                    int outHoles = -1, outLevel = -1, wasLevel = -1;
                    bool outExact = false;
                    Do(() =>
                    {
                        wasLevel = fetchLevel();
                        map.EditorPreview(true, zoomAt(15), st.lat, st.lon);
                        outHoles = holes(); outLevel = fetchLevel(); outExact = oneLevel();
                    });
                    Check(() => outHoles == 0 && outExact && outLevel == wasLevel - 1, "한 단계 축소하자마자 빈칸 없이 제 단계로 (위 단계 두 배 넓게 미리 받기)");
                    Sleep(0.3f);
                    Wait(() => count("inflight") == 0, 20f, "seoul_z15 타일 받기 끝");
                }
                if (st.name == "seoul_max")
                    Check(() => fetchLevel() == tiles.MaxLevel && holes() == 0, "가장 깊이 확대하면 가장 자세한 단계(서버 최대)를 받아 씀");
                if (st.name == "hunt_approx_z15")
                    Check(() => (typeof(R0926FriendsMap).GetField("huntAreaPool", F)?.GetValue(map) as List<GameObject>)?.Exists(g => g != null && g.activeSelf) == true,
                        "대략 공개 게임 — '이 안 어딘가' 금빛 원");
            }
            Check(() =>
            {
                var at = typeof(R0926MapTiles).GetField("attribution", F)?.GetValue(tiles) as Text;
                return at != null && at.enabled && at.text == "© 국토교통부 V-World";
            }, "지도 출처 '© 국토교통부 V-World' 보임");
            // 기기 캐시 — 받은 타일을 서버가 적어 준 기간만 둔다. 지도를 닫았다 열면 서버 없이 거기서
            Sleep(1.0f);
            int files = 0;
            bool expiryOk = false;
            Do(() =>
            {
                var all = System.IO.Directory.Exists(diskRoot) ? System.IO.Directory.GetFiles(diskRoot, "*.tile", System.IO.SearchOption.AllDirectories) : new string[0];
                files = all.Length;
                DateTime now = DateTime.UtcNow;
                expiryOk = files > 0 && Array.TrueForAll(all, f => { var t = System.IO.File.GetLastWriteTimeUtc(f); return t > now && t <= now.AddSeconds(259200 + 60); });
                var dirs = System.IO.Directory.Exists(diskRoot) ? Array.ConvertAll(System.IO.Directory.GetDirectories(diskRoot), System.IO.Path.GetFileName) : new string[0];
                log.Add("  기기 캐시 " + files + "장 · 폴더 " + string.Join(",", dirs));
            });
            Check(() => files > 0 && expiryOk, "받은 타일이 기기 캐시에 — 기한(파일 시각)은 서버가 적어 준 3일 안");
            Do(() =>
            {
                tiles.enabled = false;   // 지도를 닫으면 메모리 타일은 버린다
                R0926MapTiles.EditorBase = "http://127.0.0.1:9";   // 서버가 없어도
                tiles.enabled = true;
                map.EditorPreview(true, zoomAt(13), 37.5665f, 126.978f);
            });
            Check(() => count("cache") == 0, "닫았다 열면 메모리는 빈 채로 시작");
            Wait(() => count("inflight") == 0 && count("cache") > 0 && holes() == 0, 6f, "다시 열기 — 기기 캐시로 채움");
            Check(() => holes() == 0 && oneLevel(), "서버 없이도 다시 열면 기기 캐시로 다 보임");
            Shot("tiles_reopen_disk");
            Do(() =>
            {
                map?.EditorPreviewClear();
                R0926MapTiles.EditorBase = null;
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                wipeDisk();
            });
        }

        // 지도 좌표 (10-15 대표님: "다른창을 다녀오면 기존에 보던 곳이 리셋 … 유지 · 꾸욱 누르고 있으면 핀 발생하면서 위도랑 경도 … 복사 ·
        // '검색' 기능이 있어서 '좌표'를 입력하면 해당 위치에 핀이 박히면서 적절한 사이즈로 지도가 확대") — 지도 타일 없이 (진짜 서버의 타일을 부르지 않는다)
        private static void PlanMapCoord()
        {
            const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var T = typeof(R0926FriendsMap);
            R0926FriendsMap map = null;
            Func<string, object> get = name => T.GetField(name, F)?.GetValue(map);
            Func<string, Button> btn = path => map != null ? map.transform.Find(path)?.GetComponent<Button>() : null;
            Func<GameObject> pinGo = () => (get("dropPin") as Transform)?.gameObject;
            Func<double, double, bool> dropAt = (la, lo) => (bool)get("hasDrop") && Math.Abs((double)get("dropLat") - la) < 0.01 && Math.Abs((double)get("dropLon") - lo) < 0.01;
            // 지금 화면 가운데의 위도 · 경도 · 배율
            Func<double[]> center = () =>
            {
                var vp = (RectTransform)map.transform;
                object n = T.GetMethod("ToNorm", F).Invoke(map, new object[] { vp.rect.size / 2f });
                var a = new object[] { n, null, null };
                T.GetMethod("LatLon", SF).Invoke(null, a);
                return new[] { (double)a[1], (double)a[2], (double)get("zoom") };
            };
            UnityEngine.EventSystems.PointerEventData press = null;
            double zoomBefore = 0;
            // 떠 있는 토스트 중에 이 글이 든 것
            Func<string, bool> toastHas = part =>
            {
                var tm = ToastManager.Instance;
                var l = tm != null ? typeof(ToastManager).GetField("live", F)?.GetValue(tm) as System.Collections.IList : null;
                if (l == null) return false;
                foreach (var o in l) if ((o.GetType().GetField("message")?.GetValue(o) as string ?? "").Contains(part)) return true;
                return false;
            };
            Func<bool> resultsOn = () => (get("searchResults") as GameObject)?.activeSelf == true;
            Func<int> rowsOn = () => { int n = 0; foreach (var r in (Button[])get("resultRows")) if (r != null && r.gameObject.activeSelf) n++; return n; };
            Func<int, string, string> rowText = (i, part) => ((Button[])get("resultRows"))[i].transform.Find(part)?.GetComponent<Text>()?.text ?? "";
            Func<string> pinName = () => { var t = get("dropName") as Text; return t != null && t.gameObject.activeSelf ? t.text : ""; };
            Func<bool> barOn = () => (get("searchBar") as GameObject).activeSelf;
            Action<string> search = q => { var f = get("searchInput") as InputField; f.text = q; f.onSubmit.Invoke(f.text); };
            // 지도 가운데를 손가락으로 누름 — onMap 이 아니면 지도 위의 다른 것(동의 카드 덮개)을 누른 셈
            Action<bool> pressDown = onMap =>
            {
                var vp = (RectTransform)map.transform;
                var c = new Vector3[4];
                vp.GetWorldCorners(c);   // 오버레이 캔버스 — 월드 좌표가 곧 화면 좌표
                press = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                {
                    button = UnityEngine.EventSystems.PointerEventData.InputButton.Left, pointerId = -1, position = (c[0] + c[2]) / 2f,
                    pointerCurrentRaycast = new UnityEngine.EventSystems.RaycastResult { gameObject = onMap ? map.gameObject : map.transform.Find("Dim0926").gameObject },
                };
                zoomBefore = (double)get("zoom");
                UnityEngine.EventSystems.ExecuteEvents.Execute(map.gameObject, press, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            };
            Action<int> release = clicks =>
            {
                UnityEngine.EventSystems.ExecuteEvents.Execute(map.gameObject, press, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
                press.clickCount = clicks;
                UnityEngine.EventSystems.ExecuteEvents.Execute(map.gameObject, press, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            };

            Do(() =>
            {
                R0926MapTiles.EditorBase = "http://127.0.0.1:9";   // 닫힌 자리 — 타일 정보를 못 받아 타일 없이 돈다
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
                R0926Geo.EditorBase = "http://127.0.0.1:5099";   // 주소 찾기는 시험 서버의 정해 둔 답 (브이월드를 부르지 않는다)
            });
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);

            // 좌표 글 해석
            var cases = new (string text, double lat, double lon)[]
            {
                ("37.5665, 126.9780", 37.5665, 126.978), ("37.5665 126.9780", 37.5665, 126.978), ("126.9780, 37.5665", 37.5665, 126.978),
                ("https://www.google.com/maps/@37.5665,126.9780,15z", 37.5665, 126.978),
                ("37°33'59.4\"N 126°58'40.8\"E", 37.5665, 126.978), ("북위 37도 33분 59.4초, 동경 126도 58분 40.8초", 37.5665, 126.978),
                ("N 37° 33.990' E 126° 58.680'", 37.5665, 126.978), ("37.5665N 126.9780E", 37.5665, 126.978),
                ("33.8688° S, 151.2093° E", -33.8688, 151.2093), ("E 151.2093 S 33.8688", -33.8688, 151.2093), ("−33.8688, 151.2093", -33.8688, 151.2093),
                ("40.7128, -74.0060", 40.7128, -74.006), ("여기서 만나요 37.5665, 126.9780 ㅎㅎ", 37.5665, 126.978),
                ("37 127", 37, 127),
            };
            foreach (var cs in cases)
                Check(() => R0926FriendsMap.TryParseCoord(cs.text, out double la, out double lo) && Math.Abs(la - cs.lat) < 1e-4 && Math.Abs(lo - cs.lon) < 1e-4, "좌표 해석: " + cs.text);
            foreach (var bad in new[] { "서울역", "95.1, 200.2", "37.5665", "", "강남대로 123길 45", "종로3가 45-4", "세종대로 110", "인계동 1111-2" })
                Check(() => !R0926FriendsMap.TryParseCoord(bad, out _, out _), "좌표 아님: '" + bad + "'");

            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => Modes()?.Show(1));
            Sleep(1.0f);
            Do(() =>
            {
                map = UnityEngine.Object.FindAnyObjectByType<R0926FriendsMap>(FindObjectsInactive.Include);
                map.EditorPreview(true, 100f, 37.5665f, 126.978f);
                T.GetField("nextRefresh", F)?.SetValue(map, float.MaxValue);   // 시험 동안 서버에 다시 묻지 않게 (로그인 상태에 따라 동의 카드가 덮는다)
            });
            Sleep(0.5f);
            Check(() => map != null && pinGo() != null && get("searchBar") != null && get("searchInput") != null && btn("Search0926") != null, "지도 좌표 연결 (핀 · 돋보기 · 검색 줄)");

            // 꾹 누르기 → 핀 · 손을 떼도 확대 안 함 · 두 번 탭 확대는 그대로
            Do(() => pressDown(true));
            Sleep(0.8f);
            Do(() => release(1));
            Sleep(0.5f);
            Check(() => pinGo() != null && pinGo().activeSelf && dropAt(37.5665, 126.978) && Math.Abs((double)get("zoom") - zoomBefore) < 1e-6,
                "꾹 누르면 그 자리에 핀 + 위도·경도 · 손을 떼도 확대하지 않음");
            Check(() => System.Text.RegularExpressions.Regex.IsMatch((get("dropText") as Text)?.text ?? "", @"^-?\d{1,2}\.\d{6}, -?\d{1,3}\.\d{6}$"), "말풍선 좌표 — 소수 여섯째 자리 (약 10cm)");
            Wait(() => pinName() != "", 4f, "꾹 누른 자리 주소");
            Check(() => pinName() == "중구 세종대로 110", "꾹 누른 자리 — 말풍선 위 줄에 그 자리 주소 (우팡 서버 → 브이월드)");
            Check(() =>
            {
                var b = get("dropBubble") as RectTransform;
                var bc = new Vector3[4]; b.GetWorldCorners(bc);
                var vc = new Vector3[4]; ((RectTransform)map.transform).GetWorldCorners(vc);
                var nc = new Vector3[4]; (get("dropName") as Text).rectTransform.GetWorldCorners(nc);
                return bc[0].x >= vc[0].x - 0.5f && bc[2].x <= vc[2].x + 0.5f && nc[2].x <= bc[2].x + 0.5f && nc[0].y >= bc[0].y - 0.5f;
            }, "주소가 들어와 넓어진 말풍선도 지도 안 · 주소 줄이 말풍선 안");
            Shot("coord_pin");
            Do(() => { zoomBefore = (double)get("zoom"); press.clickCount = 2; UnityEngine.EventSystems.ExecuteEvents.Execute(map.gameObject, press, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler); });
            Sleep(0.7f);
            Check(() => Math.Abs((double)get("zoom") / zoomBefore - 2) < 0.01, "두 번 탭 확대는 그대로");
            Check(() =>
            {
                string saved = GUIUtility.systemCopyBuffer;
                if (string.IsNullOrEmpty(saved)) { log.Add("  (클립보드가 비었거나 글이 아님 — 복사 시험 건너뜀)"); return true; }
                (get("dropBubble") as Transform).Find("Copy").GetComponent<Button>().onClick.Invoke();
                string got = GUIUtility.systemCopyBuffer;
                GUIUtility.systemCopyBuffer = saved;   // 대표님 클립보드 그대로
                return got == (get("dropText") as Text).text;
            }, "복사 — 말풍선의 좌표가 그대로 클립보드에");
            Do(() => (get("dropBubble") as Transform).Find("Clear").GetComponent<Button>().onClick.Invoke());
            Sleep(0.2f);
            Check(() => !(bool)get("hasDrop") && !pinGo().activeSelf, "× — 핀 지움");

            // 지도 위의 다른 것을 꾹 · 누른 채 끌기 — 핀 없음
            Do(() => pressDown(false));
            Sleep(0.8f);
            Do(() => release(1));
            Check(() => !(bool)get("hasDrop"), "지도 위의 다른 것(동의 카드 덮개)을 꾹 눌러도 핀 없음");
            Do(() => { pressDown(true); UnityEngine.EventSystems.ExecuteEvents.Execute(map.gameObject, press, UnityEngine.EventSystems.ExecuteEvents.beginDragHandler); });
            Sleep(0.8f);
            Do(() => UnityEngine.EventSystems.ExecuteEvents.Execute(map.gameObject, press, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler));
            Check(() => !(bool)get("hasDrop"), "누른 채 끌면 핀 없음 (지도만 움직임)");

            // 지도 끝의 핀 — 말풍선이 지도 안으로
            Do(() => T.GetMethod("DropPinAt", F).Invoke(map, new object[] { new Vector2(40f, ((RectTransform)map.transform).rect.height * 0.6f) }));
            Sleep(0.2f);
            Check(() =>
            {
                var b = get("dropBubble") as RectTransform;
                var bc = new Vector3[4]; b.GetWorldCorners(bc);
                var vc = new Vector3[4]; ((RectTransform)map.transform).GetWorldCorners(vc);
                return bc[0].x >= vc[0].x && bc[2].x <= vc[2].x;
            }, "지도 왼쪽 끝 핀 — 말풍선이 지도 안으로 밀려 들어옴");
            Shot("coord_pin_edge");
            Do(() => T.GetMethod("DropPinAt", F).Invoke(map, new object[] { new Vector2(((RectTransform)map.transform).rect.width * 0.5f, 70f) }));
            Sleep(0.2f);
            Check(() => (get("dropBubble") as RectTransform).pivot.y > 0.5f, "지도 위쪽 끝 핀 — 말풍선은 핀 아래로");
            Shot("coord_pin_top");

            // 좌표 검색 — 구글 지도 주소 · 못 알아보는 글 · 닫기 · 붙여넣기
            Do(() => btn("Search0926").onClick.Invoke());
            Sleep(0.3f);
            Check(() => (get("searchBar") as GameObject).activeSelf && !(get("searchButton") as GameObject).activeSelf, "돋보기 — 위쪽 검색 줄 열림 (돋보기는 숨김)");
            Do(() => (get("searchInput") as InputField).text = "https://www.google.com/maps/@37.5444,127.0374,15z");
            Sleep(0.2f);
            Shot("coord_search_bar");
            Do(() => { var f = get("searchInput") as InputField; f.onSubmit.Invoke(f.text); });
            Sleep(1.2f);
            Check(() => !(get("searchBar") as GameObject).activeSelf && pinGo().activeSelf && dropAt(37.5444, 127.0374), "좌표 검색(구글 지도 주소) — 검색 줄 닫히고 그 자리에 핀");
            Check(() => { var c = center(); return Math.Abs(c[0] - 37.5444) < 0.01 && Math.Abs(c[1] - 127.0374) < 0.01 && Math.Abs(c[2] - 64) < 0.5; },
                "좌표 검색 — 그 자리가 화면 가운데 · 동네 배율 (타일 없을 땐 64배)");
            Shot("coord_search_done");
            // 주소·장소 이름 — 여럿이면 검색 줄 아래 목록 · 하나면 바로 · 없으면 안내 · 서버가 안 되면 오류 안내
            Do(() => btn("Search0926").onClick.Invoke());
            Sleep(0.2f);
            Do(() => search("서울역"));
            Check(() => barOn() && resultsOn() && rowsOn() == 1 && rowText(0, "Title") == "찾는 중…" && !((Button[])get("resultRows"))[0].interactable,
                "장소 이름 — 묻는 동안 '찾는 중…' 한 줄 (누를 수 없음)");
            Wait(() => rowsOn() == 3, 4f, "서울역 결과 세 줄");
            Check(() => resultsOn() && rowsOn() == 3 && rowText(0, "Title") == "서울역" && rowText(0, "Address") == "서울특별시 용산구 한강대로 405"
                        && rowText(1, "Title") == "서울역버스환승센터" && barOn() && dropAt(37.5444, 127.0374),
                "장소 이름(서울역) — 검색 줄 아래 목록 세 줄 (이름 · 주소) · 핀은 아직 그대로");
            Check(() =>
            {
                var bar = (RectTransform)(get("searchBar") as GameObject).transform;
                var res = (RectTransform)(get("searchResults") as GameObject).transform;
                var bc = new Vector3[4]; bar.GetWorldCorners(bc);
                var rc = new Vector3[4]; res.GetWorldCorners(rc);
                var vc = new Vector3[4]; ((RectTransform)map.transform).GetWorldCorners(vc);
                return rc[1].y <= bc[0].y + 0.5f && rc[0].y >= vc[0].y && rc[0].x >= vc[0].x && rc[2].x <= vc[2].x && res.rect.height > 3 * 140;
            }, "결과 목록 — 검색 줄 바로 아래 · 지도 안");
            Shot("coord_search_results");
            Do(() => ((Button[])get("resultRows"))[0].onClick.Invoke());
            Sleep(1.2f);
            Check(() => !barOn() && !resultsOn() && dropAt(37.55465, 126.97059) && pinName() == "서울역",
                "목록에서 고름 — 검색 줄·목록 닫히고 그 자리에 핀 · 핀 위에 장소 이름");
            Check(() => { var c = center(); return Math.Abs(c[0] - 37.55465) < 0.01 && Math.Abs(c[1] - 126.97059) < 0.01; }, "고른 장소가 화면 가운데");
            Shot("coord_place_pin");

            // 시험 서버가 1.5초 늦게 '세종대로 110' 한 곳을 답한다 — 그 전에 닫으면 핀이 옮겨 가지 않아야
            Do(() => btn("Search0926").onClick.Invoke());
            Sleep(0.2f);
            Do(() => search("느린주소"));
            Sleep(0.3f);
            Do(() => btn("SearchBar0926/Close").onClick.Invoke());
            Sleep(2.0f);
            Check(() => !barOn() && !resultsOn() && dropAt(37.55465, 126.97059) && pinName() == "서울역", "묻는 중에 검색 줄을 닫으면 늦게 온 답은 버림 (핀이 옮겨 가지 않음)");

            Do(() => btn("Search0926").onClick.Invoke());
            Sleep(0.2f);
            Do(() => search("세종대로 110"));
            Wait(() => !barOn(), 4f, "주소 한 곳 → 바로 이동");
            Sleep(0.8f);
            Check(() => !resultsOn() && dropAt(37.56637, 126.97792) && pinName() == "중구 세종대로 110 (태평로1가)",
                "주소(세종대로 110) — 한 곳이면 목록 없이 바로 그 자리 · 핀 위 이름은 시·도를 뺀 주소");

            Do(() => btn("Search0926").onClick.Invoke());
            Sleep(0.2f);
            Do(() => search("서울역"));
            Wait(() => rowsOn() == 3, 4f, "다시 서울역 목록");
            Do(() => (get("searchInput") as InputField).text = "서울역 1");
            Sleep(0.1f);
            Check(() => !resultsOn() && barOn(), "칸의 글을 고치면 지난 목록은 거둠");
            Do(() => search("없는곳zzqx"));
            Wait(() => toastHas("찾지 못했어요"), 4f, "못 찾음 안내");
            Check(() => toastHas("찾지 못했어요") && !resultsOn() && barOn() && dropAt(37.56637, 126.97792), "찾는 게 없음 — '찾지 못했어요' 안내 · 검색 줄·핀 그대로");
            Shot("coord_search_none");
            Do(() => search("서버오류"));
            Wait(() => toastHas("지금은 주소를 찾을 수 없어요"), 4f, "서버 오류 안내");
            Check(() => toastHas("지금은 주소를 찾을 수 없어요") && !resultsOn() && barOn(), "서버가 안 됨 — 오류 안내 · 검색 줄 그대로 (좌표는 계속 됨)");
            Do(() => search("서"));
            Check(() => toastHas("두 글자 이상") && !resultsOn(), "한 글자 — 묻지 않고 '두 글자 이상' 안내");
            Do(() => btn("SearchBar0926/Close").onClick.Invoke());
            Sleep(0.2f);
            Check(() => !(get("searchBar") as GameObject).activeSelf && (get("searchButton") as GameObject).activeSelf, "검색 줄 닫기 — 돋보기 다시 보임");
            bool pasted = false;
            Check(() =>
            {
                string saved = GUIUtility.systemCopyBuffer;
                if (string.IsNullOrEmpty(saved)) { log.Add("  (클립보드가 비었거나 글이 아님 — 붙여넣기 시험 건너뜀)"); return true; }
                btn("Search0926").onClick.Invoke();
                GUIUtility.systemCopyBuffer = "부산시청 35.1798, 129.0750";
                btn("SearchBar0926/Paste").onClick.Invoke();
                GUIUtility.systemCopyBuffer = saved;   // 대표님 클립보드 그대로
                pasted = true;
                return (get("searchInput") as InputField).text.Contains("35.1798") && !(get("searchBar") as GameObject).activeSelf;
            }, "붙여넣기 — 복사해 둔 좌표를 칸에 넣고 바로 찾음");
            Sleep(1.4f);
            Check(() => !pasted || (dropAt(35.1798, 129.075) && Math.Abs(center()[0] - 35.1798) < 0.01), "붙여넣은 좌표로 이동 (부산시청)");

            // 보던 자리 — 다른 탭에 다녀와도 · 기억이 없으면 처음처럼
            double[] before = null;
            Do(() => { before = center(); Modes()?.Show(0); });
            Sleep(0.6f);
            Do(() => Modes()?.Show(1));
            Sleep(1.0f);
            Do(() => T.GetField("nextRefresh", F)?.SetValue(map, float.MaxValue));
            Check(() => { var c = center(); return Math.Abs(c[0] - before[0]) < 1e-5 && Math.Abs(c[1] - before[1]) < 1e-5 && Math.Abs(c[2] / before[2] - 1) < 1e-6; },
                "목록 탭에 다녀와도 보던 자리 그대로 (가운데·배율)");
            Check(() => PlayerPrefs.HasKey("R0926Map.View") && (bool)get("hasDrop"), "보던 자리를 기기에 적어 둠 (앱을 다시 켜도) · 꽂은 핀도 그대로");
            Shot("coord_view_kept");
            Do(() => Modes()?.Show(0));
            // 창이 옆으로 다 밀려나 꺼진 뒤에야(≈0.44초) 다시 켤 때 처음처럼 맞춘다 — 그 전에 돌아오면 보던 자리 그대로가 맞다
            Wait(() => map != null && !map.gameObject.activeInHierarchy, 3f, "지도 탭이 꺼짐");
            Do(() => { PlayerPrefs.DeleteKey("R0926Map.View"); Modes()?.Show(1); });
            Sleep(1.0f);
            Check(() => !(bool)get("touched") && (bool)get("fitted"), "기억한 자리가 없으면 처음처럼 모두가 보이게 맞춤");
            Do(() =>
            {
                map?.EditorPreviewClear();
                R0926MapTiles.EditorBase = null;
                R0926Geo.EditorBase = null;
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
            });
        }

        // 브이월드 운영키 신청 '기능화면' (10-19) — 실제 배경지도(시험 타일 서버 5098 → 브이월드) 위에서 검색 목록 · 고른 장소 핀 · 꾹 눌러 꽂은 핀의 주소,
        // 그리고 도크 위 주소 한 줄. 공개될 수 있는 그림이라 개인 장소·에디터 위치는 찍지 않는다 — 도크 위 주소는 서울시청 자리로 다시 묻는다(실제 서버 → 브이월드).
        // 지도 검색·핀 주소는 시험 서버(5099)의 정해 둔 답 — 서울역·서울시청의 실제 주소 그대로
        private static void PlanVwShot()
        {
            const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var T = typeof(R0926FriendsMap);
            R0926FriendsMap map = null;
            R0926MapTiles tiles = null;
            LocationManager lm = null;
            Func<string, object> get = name => T.GetField(name, F)?.GetValue(map);
            Func<string, int> count = name => (typeof(R0926MapTiles).GetField(name, F)?.GetValue(tiles) as System.Collections.ICollection)?.Count ?? -1;
            Func<string> dockText = () => (typeof(LocationManager).GetField("infoText", F)?.GetValue(lm) as Text)?.text ?? "";
            Func<string> pinName = () => { var t = get("dropName") as Text; return t != null && t.gameObject.activeSelf ? t.text : ""; };
            Func<int> rowsOn = () => { int n = 0; foreach (var r in (Button[])get("resultRows")) if (r != null && r.gameObject.activeSelf && r.interactable) n++; return n; };
            Action<double, double, float> look = (lat, lon, lv) =>
            {
                double max = Convert.ToDouble(T.GetProperty("MaxZoom", F).GetValue(map));
                float top = tiles.MaxLevel + R0926FriendsMap.OverZoomLevels;
                map.EditorPreview(true, (float)(max / Math.Pow(2, top - lv)), (float)lat, (float)lon);
                T.GetField("nextRefresh", F)?.SetValue(map, float.MaxValue);
            };
            Action<string> settle = name => { Sleep(0.6f); Wait(() => count("inflight") == 0, 20f, name + " 타일 받기 끝"); Sleep(0.8f); };

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

            Do(() =>
            {
                HidePrivatePlaces();
                lm = UnityEngine.Object.FindAnyObjectByType<LocationManager>(FindObjectsInactive.Include);
                var m = typeof(LocationManager).GetMethod("GetAddressFromCoordinates", F);
                if (lm != null && m != null) lm.StartCoroutine((System.Collections.IEnumerator)m.Invoke(lm, new object[] { 37.5665f, 126.978f }));
            });
            Wait(() => dockText().Contains("37.5665"), 10f, "도크 위 주소 (서울시청)");
            Sleep(0.3f);
            Do(() => log.Add("  도크 위: " + dockText()));
            Shot("vw_dock");

            Do(() => R0926Geo.EditorBase = "http://127.0.0.1:5099");
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => Modes()?.Show(1));
            Sleep(1.0f);
            Do(() =>
            {
                map = UnityEngine.Object.FindAnyObjectByType<R0926FriendsMap>(FindObjectsInactive.Include);
                tiles = map != null ? T.GetField("tiles", F)?.GetValue(map) as R0926MapTiles : null;
            });
            Wait(() => tiles != null && tiles.Available, 15f, "서버 타일 켜짐 (시험 서버)");
            Do(() => look(37.5590, 126.9745, 15f));
            settle("서울역 둘레");
            Do(() => map.OpenSearch());
            Sleep(0.3f);
            Do(() => { var f = get("searchInput") as InputField; f.text = "서울역"; f.onSubmit.Invoke(f.text); });
            Wait(() => rowsOn() == 3, 5f, "서울역 목록 세 줄");
            Sleep(0.5f);
            Shot("vw_search");
            Do(() => map.PickResult(0));
            Sleep(1.6f);
            settle("서울역");
            Do(() => log.Add("  고른 장소 핀: " + pinName()));
            Shot("vw_place");

            Do(() => look(37.5665, 126.9780, 16f));
            settle("서울시청");
            Do(() => T.GetMethod("DropPinAt", F).Invoke(map, new object[] { ((RectTransform)map.transform).rect.size / 2f }));
            Wait(() => pinName() != "", 5f, "꾹 누른 자리 주소");
            Sleep(0.6f);
            Do(() => log.Add("  꾹 누른 핀: " + pinName() + " · " + ((get("dropText") as Text)?.text ?? "")));
            Shot("vw_pin");
            Do(() =>
            {
                map?.EditorPreviewClear();
                R0926MapTiles.EditorBase = null;
                R0926Geo.EditorBase = null;
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
            });
        }

        // E3 (10-19) 장소 추가 미리보기 — 대표 사진을 고르면 '오브젝트 생성 중' 안내와 함께 앞쪽에 스피너 → 사진 큐브.
        // 휴대폰을 앞으로 들었을 때(0°)와 아래로 숙였을 때(50°·75°) — 생길 자리가 화면 안에 있는지 · 안내가 어디에 뜨는지 찍는다.
        // 10-20: 생길 자리가 화면 밖이면 큐브 화살표의 2배인 화살표가 반짝이며 가리킨다 · 생성 중에 취소하면 큐브가 남지 않는다
        // E4 (10-20) 사진을 불러오는 동안 그 칸에 빈 칸+원호 — 대표 칸 · 추가 사진(실제 읽기 코루틴으로 한 장씩 바뀌는지) ·
        // 열 장이어도 칸이 줄어 자리 안에(예전엔 네 장을 넘으면 '이름' 칸을 덮었다) · 예전 알약(LoadingPanel)이 시트에 가리는지
        // 추가 창의 사진·모델 파일 칸 — 눌렀을 때 그 칸이 실제로 맨 위에서 눌리는가 · 처리 중 표시가 남아 막고 있지 않은가
        private static void PlanPickers()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            CubeUploadManager cum = null;
            ModelUploadManager mum = null;
            PhotoSourceDialog dlg = null;
            SwipePanelController swipe = null;
            Func<object, string, Button> btn = (o, f) => o == null ? null : o.GetType().GetField(f, F)?.GetValue(o) as Button;
            Func<object, bool> busy = o => o != null && (bool)o.GetType().GetField("isProcessing", F).GetValue(o);
            Func<bool> dlgAnim = () => dlg != null && (bool)typeof(PhotoSourceDialog).GetField("isAnimating", F).GetValue(dlg);
            // 그 칸 가운데를 눌렀을 때 맨 위에 걸리는 것
            Func<Button, string> top = b =>
            {
                if (b == null) return "버튼 없음";
                if (!b.gameObject.activeInHierarchy) return b.name + " 꺼짐";
                var r = ScreenRect((RectTransform)b.transform);
                var es = UnityEngine.EventSystems.EventSystem.current;
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = r.center }, hits);
                if (hits.Count == 0) return b.name + " → 아무것도 안 걸림";
                var h = hits[0].gameObject;
                bool mine = h.transform == b.transform || h.transform.IsChildOf(b.transform);
                var handler = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(h);
                return b.name + (mine ? " OK" : " ✗ 막힘: " + h.name + " (" + h.transform.parent?.name + ")") + " · 누르면 " + (handler != null ? handler.name : "없음")
                    + " · 그 아래 " + string.Join(", ", hits.GetRange(1, Mathf.Min(3, hits.Count - 1)).ConvertAll(x => x.gameObject.name));
            };
            Func<Button, bool> topOk = b =>
            {
                if (b == null || !b.gameObject.activeInHierarchy) return false;
                var r = ScreenRect((RectTransform)b.transform);
                var es = UnityEngine.EventSystems.EventSystem.current;
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = r.center }, hits);
                if (hits.Count == 0) return false;
                var handler = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
                return handler == b.gameObject;
            };
            Action<string> state = tag =>
            {
                log.Add("  [" + tag + "] 장소 처리중 " + busy(cum) + " · 3D 처리중 " + busy(mum) + " · 사진 시트 애니 " + dlgAnim()
                    + " · 카드 " + (swipe != null ? swipe.GetCurrentPanel().ToString() : "?"));
                log.Add("    장소: " + top(btn(cum, "mainPhotoButton")) + " | " + top(btn(cum, "subPhotosButton")) + " | " + top(btn(cum, "resetPhotosButton")));
                log.Add("    3D: " + top(btn(mum, "selectFileButton")) + " | " + top(btn(mum, "selectedFileButton")) + " | " + top(btn(mum, "subPhotosButton")) + " | " + top(btn(mum, "resetPhotosButton")));
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Check(() => SoundOnAll("UploadPage", "Seg0926", "Chips0926"), "위 탭(장소·3D·보물)·분류 칩에 누름 소리 — 다른 버튼과 같은 기본 소리 (10-21)");
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() =>
            {
                cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                dlg = UnityEngine.Object.FindAnyObjectByType<PhotoSourceDialog>(FindObjectsInactive.Include);
                swipe = UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Include);
                log.Add("  관리자 수: 장소 " + UnityEngine.Object.FindObjectsByType<CubeUploadManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length
                    + " · 3D " + UnityEngine.Object.FindObjectsByType<ModelUploadManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length
                    + " · 사진 시트 " + UnityEngine.Object.FindObjectsByType<PhotoSourceDialog>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                state("장소 탭 처음");
            });
            Shot("pickers_place");
            Check(() => topOk(btn(cum, "mainPhotoButton")) && topOk(btn(cum, "subPhotosButton")), "장소: 대표·추가 사진 칸이 맨 위에서 눌린다");
            Do(() => btn(cum, "mainPhotoButton")?.onClick.Invoke());
            Sleep(0.7f);
            Check(() => Active("PhotoSourceDialog/DialogPanel"), "장소: 대표 사진 칸 → 사진 고르기 시트");
            Do(() => Click("PhotoSourceDialog/DialogPanel/BottomContainer/ContentCard/ButtonRow/CancelButton/ClosePx0926"));
            Sleep(0.8f);

            Do(() => swipe.SwitchToPanel(1));
            Sleep(1.2f);
            Do(() => state("3D 탭"));
            Shot("pickers_model");
            Check(() => btn(mum, "selectFileButton") != null && btn(mum, "selectFileButton").gameObject.activeInHierarchy
                        && !btn(mum, "selectedFileButton").gameObject.activeInHierarchy, "3D: 처음엔 파일 칸(체크 아님)");
            Check(() => topOk(btn(mum, "selectFileButton")) && topOk(btn(mum, "subPhotosButton")), "3D: 파일·추가 사진 칸이 맨 위에서 눌린다");
            Check(() => { var c = Find("UploadPage/Clip0926/UploadSheet0926/ModelUploadPage/Panel/Chips0926"); return c != null && c.childCount == 8; }, "3D: 분류 칩 8개(장소와 같게)");

            // 파일을 고른 뒤 — 체크 칸 없이 칸 아래 글자가 파일 이름, 초기화하면 원래대로 (10-21)
            string glb = null, capDefault = null;
            Func<string> capText = () => (typeof(ModelUploadManager).GetField("fileCaption", F).GetValue(mum) as Text)?.text;
            Do(() =>
            {
                capDefault = capText();
                glb = System.IO.Path.Combine(Application.temporaryCachePath, "pk_test.glb");
                System.IO.File.WriteAllBytes(glb, new byte[64]);
                typeof(ModelUploadManager).GetMethod("ProcessSelectedFile", F).Invoke(mum, new object[] { glb });
            });
            Sleep(0.3f);
            Do(() => log.Add("  [파일 고른 뒤] 칸 아래 글자 '" + capText() + "' · 체크 칸 켜짐 " + btn(mum, "selectedFileButton").gameObject.activeSelf));
            Shot("pickers_model_file");
            Check(() => btn(mum, "selectFileButton").gameObject.activeInHierarchy && !btn(mum, "selectedFileButton").gameObject.activeSelf
                        && capText() == "pk_test.glb", "3D: 파일을 골라도 체크 칸 없이 칸 아래에 파일 이름");
            Check(() => topOk(btn(mum, "selectFileButton")), "3D: 파일을 고른 뒤에도 파일 칸이 눌린다");
            Do(() => btn(mum, "resetPhotosButton").onClick.Invoke());
            Sleep(0.3f);
            Check(() => typeof(ModelUploadManager).GetField("selectedFilePath", F).GetValue(mum) == null && !string.IsNullOrEmpty(capDefault) && capText() == capDefault,
                "3D: 초기화하면 고른 파일도 비고 글자가 원래대로");
            Do(() => { try { System.IO.File.Delete(glb); } catch { } });

            // 파일 고르기 창의 답을 잃었을 때 — 다시 누르면 풀린다 (에디터에선 파일 칸이 모달 창을 띄우므로 칸 대신 PickerFree 를 직접)
            Do(() =>
            {
                ((R0926PickerWait)typeof(ModelUploadManager).GetField("pick", F).GetValue(mum)).Begin(R0926PickerWait.Source.File);
                typeof(ModelUploadManager).GetField("isProcessing", F).SetValue(mum, true);
            });
            Sleep(1.7f);
            Check(() => (bool)typeof(ModelUploadManager).GetMethod("PickerFree", F).Invoke(mum, null) && !busy(mum), "3D: 답을 잃은 파일 고르기 → 다시 누르면 풀린다");

            Do(() => swipe.SwitchToPanel(2));
            Sleep(1.2f);
            Do(() => swipe.SwitchToPanel(0));
            Sleep(1.2f);
            Do(() => state("보물찾기 다녀와서 장소 탭"));
            Check(() => topOk(btn(cum, "mainPhotoButton")) && topOk(btn(cum, "subPhotosButton")), "장소: 다른 탭 다녀와도 사진 칸이 눌린다");
            Do(() => btn(cum, "mainPhotoButton")?.onClick.Invoke());
            Sleep(0.7f);
            Check(() => Active("PhotoSourceDialog/DialogPanel"), "장소: 다시 눌러도 사진 고르기 시트");
            Do(() => Click("PhotoSourceDialog/DialogPanel/BottomContainer/ContentCard/ButtonRow/CancelButton/ClosePx0926"));
            Sleep(0.8f);

            // 초기화 = 대표 사진까지 (10-21)
            Do(() =>
            {
                var tex = new Texture2D(8, 8);
                typeof(CubeUploadManager).GetField("mainPhoto", F).SetValue(cum, tex);
                typeof(CubeUploadManager).GetMethod("SetMainSprite", F).Invoke(cum, new object[] { tex });
                typeof(CubeUploadManager).GetMethod("SetMainPhotoUIState", F).Invoke(cum, new object[] { true });
            });
            Sleep(0.3f);
            Func<bool> mainShown = () => ((Component)typeof(CubeUploadManager).GetField("mainPhotoDisplay", F).GetValue(cum)).gameObject.activeSelf;
            Check(() => typeof(CubeUploadManager).GetField("mainPhoto", F).GetValue(cum) != null && mainShown(), "장소: (준비) 대표 사진을 넣었다");
            Do(() => btn(cum, "resetPhotosButton").onClick.Invoke());
            Sleep(0.3f);
            Check(() => typeof(CubeUploadManager).GetField("mainPhoto", F).GetValue(cum) == null && !mainShown(), "장소: 초기화하면 대표 사진도 빈다");

            // 고르기 창의 답을 잃었을 때 — 불러오는 칸이 떠 있어도 칸이 눌리고, 다시 누르면 풀려 사진 시트가 뜬다 (10-21)
            Do(() =>
            {
                ((R0926PickerWait)typeof(CubeUploadManager).GetField("pick", F).GetValue(cum)).Begin(R0926PickerWait.Source.Gallery);
                typeof(CubeUploadManager).GetField("isProcessing", F).SetValue(cum, true);
                typeof(CubeUploadManager).GetMethod("MainLoading", F).Invoke(cum, new object[] { true });
            });
            Sleep(0.4f);
            Do(() => state("답을 잃은 고르기"));
            Shot("pickers_lost");
            Check(() => topOk(btn(cum, "mainPhotoButton")), "장소: 불러오는 칸이 떠 있어도 대표 사진 칸이 눌린다");
            Do(() => btn(cum, "mainPhotoButton")?.onClick.Invoke());   // 연 직후 — 아직은 답을 기다린다
            Sleep(0.4f);
            Check(() => busy(cum) && !Active("PhotoSourceDialog/DialogPanel"), "장소: 연 직후(1.5초 안)엔 기다린다");
            Sleep(1.4f);
            Do(() => btn(cum, "mainPhotoButton")?.onClick.Invoke());
            Sleep(0.7f);
            Do(() => state("다시 누른 뒤"));
            Check(() => !busy(cum) && Active("PhotoSourceDialog/DialogPanel"), "장소: 답을 잃은 고르기 → 다시 누르면 풀리고 사진 시트");
            Do(() => Click("PhotoSourceDialog/DialogPanel/BottomContainer/ContentCard/ButtonRow/CancelButton/ClosePx0926"));
            Sleep(0.8f);
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.0f);
            // 닫았다 다시 열어도
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() => state("다시 연 장소 탭"));
            Check(() => topOk(btn(cum, "mainPhotoButton")), "다시 연 추가 창: 대표 사진 칸이 눌린다");
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(0.8f);
        }

        private static void PlanPhotoLoad()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            CubeUploadManager cum = null;
            R0926PhotoLoading pl = null;
            Func<List<Texture2D>> photos = () => typeof(CubeUploadManager).GetField("subPhotos", F).GetValue(cum) as List<Texture2D>;
            Func<int, string[]> paths = n =>
            {
                var names = new[] { "bg_city.png", "bg_sky.png", "brand.png" };
                var r = new string[n];
                for (int i = 0; i < n; i++) r[i] = Path.Combine(storeSrc, names[i % names.Length]);
                return r;
            };
            Func<Transform, Rect> rectOf = t => t != null ? ScreenRect((RectTransform)t) : default;
            Func<string, float> spinAngle = n =>
            {
                var t = pl != null ? pl.transform.Find(n + "/Spin0926") : null;
                return t != null ? t.localEulerAngles.z : float.NaN;
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() =>
            {
                cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                pl = typeof(CubeUploadManager).GetField("photoLoading", F)?.GetValue(cum) as R0926PhotoLoading;
                var mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                var fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                var mpl = typeof(ModelUploadManager).GetField("photoLoading", F)?.GetValue(mum) as R0926PhotoLoading;
                var fpl = typeof(CubeDataFixManager).GetField("photoLoading", F)?.GetValue(fix) as R0926PhotoLoading;
                // 비어 있는 직렬화 칸은 '가짜 null' 이라 그냥 != null 로 보면 있다고 나온다 — Unity 비교로
                Func<R0926PhotoLoading, string, bool> has = (x, f) => (typeof(R0926PhotoLoading).GetField(f, F).GetValue(x) as UnityEngine.Object) != null;
                Func<R0926PhotoLoading, string> desc = x => x == null ? "없음" : x.transform.parent.name + "/" + x.name
                    + " 대표 " + (has(x, "mainSkeleton") ? "O" : "-")
                    + " 추가 " + (has(x, "subTemplate") ? "O" : "-");
                log.Add($"  장소 {desc(pl)} · 3D모델 {desc(mpl)} · 장소 수정 {desc(fpl)}");
            });
            Check(() =>
            {
                var mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                var fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                var mpl = typeof(ModelUploadManager).GetField("photoLoading", F)?.GetValue(mum) as R0926PhotoLoading;
                var fpl = typeof(CubeDataFixManager).GetField("photoLoading", F)?.GetValue(fix) as R0926PhotoLoading;
                Func<R0926PhotoLoading, string, bool> has = (x, f) => (typeof(R0926PhotoLoading).GetField(f, F).GetValue(x) as UnityEngine.Object) != null;
                return pl != null && mpl != null && fpl != null
                    && has(pl, "mainSkeleton") && has(pl, "subTemplate")
                    && !has(mpl, "mainSkeleton") && has(mpl, "subTemplate")      // 3D 모델 대표 칸은 모델 파일
                    && has(fpl, "mainSkeleton") && has(fpl, "subTemplate");
            }, "연결: 장소(대표·추가) · 3D모델(추가) · 장소 수정(대표·추가)");

            // 0) 사진 고르기 시트 — 대표 사진 칸을 누르면 '촬영 / 갤러리 / 취소' 가 한 줄에 큰 칸으로 (E5)
            const string dlgRow = "PhotoSourceDialog/DialogPanel/BottomContainer/ContentCard/ButtonRow/";
            string[] tiles = { "CameraButton", "GalleryButton", "CancelButton" };
            Do(() => (typeof(CubeUploadManager).GetField("mainPhotoButton", F).GetValue(cum) as Button)?.onClick.Invoke());
            Sleep(0.7f);
            Shot("photoload_dialog");
            Check(() =>
            {
                bool ok = Active("PhotoSourceDialog/DialogPanel");
                string desc = "";
                Rect prev = default;
                for (int i = 0; i < tiles.Length; i++)
                {
                    var t = Find(dlgRow + tiles[i]);
                    var r = rectOf(t);
                    Text label = null;
                    foreach (var x in t != null ? t.GetComponentsInChildren<Text>(true) : new Text[0]) if (x.gameObject.activeInHierarchy) { label = x; break; }
                    desc += $" {tiles[i]}({r.x:0},{r.y:0} {r.width:0}x{r.height:0} 「{(label != null ? label.text : "-")}」 {(label != null ? label.fontSize : 0)})";
                    if (r.width < Screen.width * 0.25f || r.height < 250f) ok = false;                 // 큼직하게
                    if (label == null || label.fontSize < 48 || !r.Contains(ScreenRect(label.rectTransform).center)) ok = false;
                    if (i > 0 && (Mathf.Abs(r.y - prev.y) > 2f || r.xMin < prev.xMax)) ok = false;   // 한 줄 · 왼쪽부터 차례로
                    prev = r;
                }
                log.Add("  사진 고르기:" + desc);
                return ok;
            }, "사진 고르기: 촬영 · 갤러리 · 취소 — 한 줄에 큰 칸");
            Do(() => Click(dlgRow + "CancelButton/ClosePx0926"));
            Sleep(1.0f);
            Check(() => !Active("PhotoSourceDialog/DialogPanel") && !pl.MainShowing, "사진 고르기: 취소로 닫힘 (빈 칸도 안 뜸)");

            // 1) 예전 알약 — 업로드 진행(%)도 이 알약에 뜬다. 장소 추가 시트 위에서 보이는가
            Do(() => typeof(CubeUploadManager).GetMethod("ShowSpinner", F).Invoke(cum, new object[] { "업로드 중 42%" }));
            Sleep(0.5f);
            Do(() =>
            {
                var lp = Find("LoadingPanel");
                var up = Find("UploadPage");
                var r = rectOf(lp);
                var es = UnityEngine.EventSystems.EventSystem.current;
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = r.center }, hits);
                log.Add($"  알약: 순서 LoadingPanel {lp?.GetSiblingIndex()} · UploadPage {up?.GetSiblingIndex()} · 자리 ({r.center.x:0},{r.center.y:0}) {r.width:0}x{r.height:0}"
                    + " · 그 자리 맨 위 " + (hits.Count > 0 ? hits[0].gameObject.name + " (" + hits[0].gameObject.transform.parent?.name + ")" : "없음"));
            });
            Shot("photoload_pill");
            Do(() => typeof(CubeUploadManager).GetMethod("HideSpinner", F).Invoke(cum, null));

            // 2) 대표 칸 — 사진 칸을 덮는 빈 칸, 원호가 돈다
            float a0 = 0f;
            Do(() => pl.SetMain(true));
            Sleep(0.5f);
            Do(() => a0 = spinAngle("MainLoading0926"));
            Sleep(0.25f);
            Check(() =>
            {
                var m = pl.transform.Find("MainLoading0926");
                var d = pl.transform.Find("MainPhotoDisplay");
                Rect rm = rectOf(m), rd = rectOf(d);
                float a1 = spinAngle("MainLoading0926");
                log.Add($"  대표 칸: 빈 칸 ({rm.x:0},{rm.y:0}) {rm.width:0}x{rm.height:0} · 사진 칸 ({rd.x:0},{rd.y:0}) {rd.width:0}x{rd.height:0} · 원호 {a0:0}° → {a1:0}° · 사진보다 위 {m.GetSiblingIndex() > d.GetSiblingIndex()}");
                return pl.MainShowing && Mathf.Abs(rm.x - rd.x) < 2 && Mathf.Abs(rm.y - rd.y) < 2 && Mathf.Abs(rm.width - rd.width) < 2
                    && Mathf.Abs(Mathf.DeltaAngle(a0, a1)) > 5f && m.GetSiblingIndex() > d.GetSiblingIndex();
            }, "대표 사진 불러오는 동안: 사진 칸을 덮는 빈 칸 · 원호가 돈다");
            Shot("photoload_main");
            Do(() => pl.SetMain(false));

            // 3) 추가 사진 — 빈 칸 셋 / 한 장 들어온 뒤(사진 하나 + 빈 칸 둘)
            Do(() => pl.SetSub(3));
            Sleep(0.6f);
            Shot("photoload_sub3");
            Do(() =>
            {
                var tex = NativeGallery.LoadImageAtPath(paths(1)[0], 1024, false, false);
                photos().Add(tex);
                typeof(CubeUploadManager).GetMethod("UpdateSubPhotoGrid", F).Invoke(cum, null);
                pl.SetSub(2);
            });
            Sleep(0.5f);
            Shot("photoload_sub_mid");
            Check(() =>
            {
                var grid = pl.transform.Find("SubPhotoContainer");
                int on = 0; string order = "";
                foreach (Transform c in grid) if (c.gameObject.activeSelf) { on++; order += c.name.StartsWith("SubLoading") ? "□" : "■"; }
                log.Add($"  추가 사진 칸: {order} (■ 사진 · □ 불러오는 중)");
                return order == "■□□";
            }, "추가 사진: 들어온 사진 뒤에 불러오는 빈 칸");
            Do(() => { typeof(CubeUploadManager).GetMethod("ResetSubPhotos", F).Invoke(cum, null); pl.SetSub(0); });
            Sleep(0.3f);

            // 4) 실제 읽기 코루틴 — 세 장: 빈 칸 셋이 먼저 깔리고, 한 장씩 사진으로 바뀌어 끝에 빈 칸 0
            var seen = new List<string>();
            bool done = false;
            double until = 0;
            Do(() =>
            {
                seen.Clear(); done = false; until = EditorApplication.timeSinceStartup + 20;
                var it = (System.Collections.IEnumerator)typeof(CubeUploadManager).GetMethod("LoadMultipleImagesWithFallback", F)
                    .Invoke(cum, new object[] { paths(3), (Action)(() => done = true) });
                cum.StartCoroutine(it);
            });
            steps.Enqueue(() =>
            {
                string st = photos().Count + "+" + pl.SubShowing;
                if (seen.Count == 0 || seen[seen.Count - 1] != st) seen.Add(st);
                return done || EditorApplication.timeSinceStartup > until;
            });
            Check(() =>
            {
                log.Add("  세 장 읽기: " + string.Join(" → ", seen) + " (사진+빈 칸)");
                bool sum3 = true; int prev = 99;
                foreach (var st in seen)
                {
                    var a = st.Split('+');
                    int ph = int.Parse(a[0]), sk = int.Parse(a[1]);
                    if (ph + sk != 3 && !(ph == 3 && sk == 0)) sum3 = false;
                    if (sk > prev) sum3 = false;
                    prev = sk;
                }
                return done && seen.Count >= 3 && seen[0] == "0+3" && seen[seen.Count - 1] == "3+0" && sum3;
            }, "추가 사진 세 장: 빈 칸 셋이 먼저 → 한 장씩 사진으로 → 끝에 빈 칸 없음");
            Sleep(0.3f);
            Shot("photoload_three");
            Check(() =>
            {
                var grid = pl.transform.Find("SubPhotoContainer");
                string sz = ""; bool square = true; int n = 0;
                foreach (Transform c in grid)
                {
                    if (!c.gameObject.activeSelf || !c.name.StartsWith("SubPhoto_")) continue;
                    var sp = c.GetComponent<Image>().sprite;
                    n++;
                    if (sp == null) { square = false; continue; }
                    sz += $" {sp.texture.width}x{sp.texture.height}→{sp.rect.width:0}x{sp.rect.height:0}";
                    if (Mathf.Abs(sp.rect.width - sp.rect.height) > 1f) square = false;
                }
                log.Add("  썸네일 (원본→칸):" + sz);
                return n == 3 && square;
            }, "추가 사진 썸네일: 가운데 정사각형으로 잘라 칸을 채움");

            // 5) 열 장 — 칸이 줄어 대표 사진 높이 안에 모두 (이름 칸을 덮지 않는다)
            Do(() =>
            {
                done = false; until = EditorApplication.timeSinceStartup + 30;
                var it = (System.Collections.IEnumerator)typeof(CubeUploadManager).GetMethod("LoadMultipleImagesWithFallback", F)
                    .Invoke(cum, new object[] { paths(7), (Action)(() => done = true) });
                cum.StartCoroutine(it);
            });
            steps.Enqueue(() => done || EditorApplication.timeSinceStartup > until);
            Sleep(0.4f);
            Check(() =>
            {
                var grid = pl.transform.Find("SubPhotoContainer");
                var name = pl.transform.Find("NameInput");
                var label = pl.transform.Find("Name");
                Rect rg = rectOf(grid), rn = rectOf(name), rl = rectOf(label);
                float low = float.MaxValue; int on = 0;
                foreach (Transform c in grid) if (c.gameObject.activeSelf) { on++; low = Mathf.Min(low, rectOf(c).yMin); }
                var cell = grid.GetComponent<GridLayoutGroup>().cellSize;
                log.Add($"  열 장: 사진 {photos().Count} · 칸 {cell.x:0} · 맨 아래 칸 밑 {low:0} · 그리드 밑 {rg.yMin:0} · 이름표 위 {rl.yMax:0} · 이름 칸 위 {rn.yMax:0}");
                return photos().Count == 10 && on == 10 && low >= rg.yMin - 2 && low > rl.yMax && cell.x < 203f;
            }, "추가 사진 열 장: 칸이 줄어 자리 안에 · 이름 칸을 덮지 않음");
            Shot("photoload_ten");

            // 6) 3D 모델 카드 — 추가 사진 빈 칸 모양만
            Do(() =>
            {
                var mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                var mpl = typeof(ModelUploadManager).GetField("photoLoading", F)?.GetValue(mum) as R0926PhotoLoading;
                UnityEngine.Object.FindAnyObjectByType<SwipePanelController>()?.SwitchToPanel(1);
                if (mpl != null) mpl.SetSub(2);
            });
            Sleep(1.0f);
            Shot("photoload_model");
            Do(() =>
            {
                var mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                var mpl = typeof(ModelUploadManager).GetField("photoLoading", F)?.GetValue(mum) as R0926PhotoLoading;
                if (mpl != null) mpl.SetSub(0);
                UnityEngine.Object.FindAnyObjectByType<SwipePanelController>()?.SwitchToPanel(0);
                typeof(CubeUploadManager).GetMethod("ResetSubPhotos", F).Invoke(cum, null);
            });
            Sleep(0.6f);
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.0f);
        }

        private static void PlanArPrev()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            ARPreviewController prev = null;
            Func<GameObject> spinner = () => typeof(ARPreviewController).GetField("spawnedSpinner", F)?.GetValue(prev) as GameObject;
            Func<GameObject> cube = () => typeof(ARPreviewController).GetField("spawnedCube", F)?.GetValue(prev) as GameObject;
            Func<Target> guide = () => (typeof(ARPreviewController).GetField("spawnGuide", F)?.GetValue(prev) as GameObject)?.GetComponent<Target>();
            Func<Indicator, bool> shown = ind => ind != null && ind.gameObject.activeInHierarchy;
            Func<Indicator, string> label = ind =>
            {
                var nt = typeof(Indicator).GetField("nameText", F)?.GetValue(ind) as Text;
                var dt = typeof(Indicator).GetField("distanceText", F)?.GetValue(ind) as Text;
                return ((ind.Type == IndicatorType.BOX && nt != null ? nt.text + " " : "") + (dt != null ? dt.text : "")).Replace("\n", " ");
            };
            // 10-22 '생성 중...' — 큐브 인디케이터와 같은 모양·크기(밖 화살표 · 안 박스)에 이름만 '생성 중...', 다 생기면 큐브 것('신규 오브젝트')
            Action<string, bool, bool> guideCheck = (tag, off, ready) => Check(() =>
            {
                var g = guide();
                var c = cube() != null ? cube().GetComponentInChildren<Target>() : null;
                var cp = (typeof(ARPreviewController).GetField("cubePrefab", F)?.GetValue(prev) as GameObject)?.GetComponentInChildren<Target>(true);
                if (cp == null) { log.Add($"  {tag}: 큐브 본 없음"); return false; }
                var want = off ? IndicatorType.ARROW : IndicatorType.BOX;
                float wantSize = off ? cp.MaxArrowSize : cp.MaxBoxSize;   // 5m 남짓 — 가까울 때 크기
                string name = LocalizationManager.Instance.GetText(ready ? "new_object" : "spawn_guide_creating");
                var who = ready ? c : g;
                var ind = who != null ? who.indicator : null;
                bool ok = shown(ind) && ind.Type == want;
                string text = ok ? label(ind) : "";
                float sc = ok ? ind.transform.localScale.x : 0f;
                Rect r = ok ? ScreenRect((RectTransform)ind.transform) : default;
                bool sparkle = ok && (typeof(Indicator).GetField("sparkleOn", F)?.GetValue(ind) as bool? ?? false);
                bool other = ready ? g != null : c != null && shown(c.indicator);   // 같은 자리에 둘이 겹침
                log.Add($"  {tag}: {(ready ? "큐브" : "안내")} {(ok ? ind.Type.ToString() : "없음")} '{text}' 크기 {sc:0.0} (본 {wantSize:0.0}) 반짝 {(sparkle ? "O" : "X")} 자리 ({r.center.x:0},{r.center.y:0}) · {(ready ? "안내 남음" : "큐브 인디케이터")} {(other ? "있음" : "없음")}");
                return ok && text.Contains(name) && Mathf.Abs(sc / wantSize - 1f) <= 0.02f && sparkle != ready && !other && (!off || r.center.y > Screen.height * 0.6f);
            }, $"생길 자리 {tag}: " + (ready ? "다 생기면 큐브 인디케이터 '신규 오브젝트' · 안내는 사라짐" : "'생성 중...' 안내 · 큐브 인디케이터와 같은 모양·크기 · 반짝 · 큐브 것과 안 겹침") + (off ? " (화면 밖 → 위쪽 화살표)" : " (화면 안 → 박스)"));
            // 10-22 별빛 — 흰 별 그림 · 늘 빌드에 들어가는 셰이더 · 흩날림
            Action<string> starCheck = tag => Check(() =>
            {
                var go = GameObject.Find("ARPreview_StarBurst");
                var ps = go != null ? go.GetComponent<ParticleSystem>() : null;
                var m = go != null ? go.GetComponent<ParticleSystemRenderer>().sharedMaterial : null;
                int n = ps != null ? ps.particleCount : 0;
                if (ps != null && n > 0)
                {
                    var arr = new ParticleSystem.Particle[n];
                    ps.GetParticles(arr);
                    var cam = Camera.main;
                    var px = new List<string>();
                    for (int i = 0; i < Mathf.Min(n, 6); i++)
                    {
                        float sz = arr[i].GetCurrentSize(ps);
                        Vector3 a = cam.WorldToScreenPoint(arr[i].position), b = cam.WorldToScreenPoint(arr[i].position + cam.transform.up * sz);
                        px.Add($"({a.x:0},{a.y:0}) {sz:0.000}m={Vector2.Distance(a, b):0}px");
                    }
                    log.Add($"  {tag}: 별 몇 개 " + string.Join(" · ", px) + $" · 그림 {(go.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture as Texture2D)?.width}px · 카메라 시야 {Camera.main.fieldOfView:0}°");
                }
                log.Add($"  {tag}: 별빛 {n}개 · 셰이더 {(m != null ? m.shader.name : "-")} · 그림 {(m != null && m.mainTexture != null ? m.mainTexture.name : "-")} · 색 {(ps != null ? ps.main.startColor.color.ToString() : "-")} · 크기 {(ps != null ? ps.main.startSize.constantMax : 0f):0.00}m");
                return n > 0 && m != null && m.shader.name == "Sprites/Default" && m.mainTexture == StarSparkle.Texture && ps.main.startColor.color == Color.white && ps.noise.enabled;
            }, $"별빛 {tag}: 흰 별 그림 · 기기에도 있는 셰이더 · 흩날림");
            Action<string> where = name =>
            {
                var go = cube() != null ? cube() : spinner();
                var cam = Camera.main;
                string msg = Find("ARPreviewPanel/MessageContainer/MessageText")?.GetComponent<Text>()?.text ?? "";
                if (go == null || cam == null) { log.Add($"  {name}: 오브젝트 없음 · 안내 '{msg}'"); return; }
                var sp = cam.WorldToScreenPoint(go.transform.position);
                bool on = sp.z > 0 && sp.x >= 0 && sp.x <= Screen.width && sp.y >= 0 && sp.y <= Screen.height;
                log.Add($"  {name}: {go.name} 화면 ({sp.x:0}, {sp.y:0}) 거리 {sp.z:0.0} {(on ? "화면 안" : "화면 밖")} · 안내 '{msg}'");
            };
            Action<float, string, bool?> run = (pitch, tag, off) =>
            {
                Do(() => AimCamera(pitch, 0f));
                Do(() => Click("Dock0926/PlusButton"));
                Sleep(1.2f);
                Do(() =>
                {
                    var cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                    prev = UnityEngine.Object.FindAnyObjectByType<ARPreviewController>(FindObjectsInactive.Include);
                    var tex = new Texture2D(2, 2);
                    tex.LoadImage(File.ReadAllBytes(Path.Combine(storeSrc, "bg_city.png")));
                    typeof(CubeUploadManager).GetMethod("StartARPreview", F).Invoke(cum, new object[] { tex });
                });
                foreach (var (t, n) in new[] { (0.15f, "a"), (0.6f, "b"), (1.4f, "c"), (1.0f, "s"), (1.4f, "d"), (1.6f, "e"), (1.5f, "f") })
                {
                    Sleep(t);
                    Do(() => where(tag + "_" + n));
                    if (off.HasValue && (n == "c" || n == "d" || n == "f")) guideCheck(tag + "_" + n, off.Value, n == "f");   // c 스피너 · d 큐브 생기는 중 · f 다 생김
                    if (n == "s" || n == "d") starCheck(tag + "_" + n);   // s 별빛 피어남 · d 흩날림
                    Shot("arprev_" + tag + "_" + n);
                }
                Do(() => Click("ARPreviewPanel/MessageContainer/ButtonContainer/CancelButton"));
                Sleep(1.0f);
                Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
                Sleep(1.0f);
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                HidePrivatePlaces();
                Backdrop("bg_city.png");
                DeviceFov();   // 에디터 카메라는 144° 라 별빛·큐브가 기기보다 훨씬 작게 찍힌다
            });
            Check(() =>
            {
                var arr = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]).FindProperty("m_AlwaysIncludedShaders");
                var names = new List<string>();
                for (int i = 0; i < arr.arraySize; i++) names.Add((arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader)?.name ?? "-");
                log.Add("  늘 빌드에 넣는 셰이더: " + string.Join(", ", names));
                return names.Contains("Sprites/Default");
            }, "별빛 셰이더(Sprites/Default)는 기기 빌드에 늘 들어감");
            Sleep(0.6f);
            run(0f, "level", false);
            run(50f, "down", null);
            run(75f, "flat", true);

            // 생성 중(스피너)에 취소 — 예전엔 생성 코루틴이 계속 돌아 몇 초 뒤 빈 자리에 큐브와 '신규 오브젝트' 인디케이터가 남았다
            Do(() => AimCamera(75f, 0f));
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() =>
            {
                var cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(Path.Combine(storeSrc, "bg_city.png")));
                typeof(CubeUploadManager).GetMethod("StartARPreview", F).Invoke(cum, new object[] { tex });
            });
            Sleep(0.9f);
            Do(() => Click("ARPreviewPanel/MessageContainer/ButtonContainer/CancelButton"));
            Sleep(4.5f);
            Check(() =>
            {
                int left = 0;
                foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsSortMode.None))
                    if (t.transform.root.name.StartsWith("0000_Cube") || t.name == "ARPreview_SpawnGuide") left++;
                log.Add($"  취소 4.5초 뒤: 남은 큐브·안내 {left} · 스피너 {(spinner() != null ? "있음" : "없음")}");
                return left == 0 && cube() == null && spinner() == null && guide() == null;
            }, "생성 중 취소: 몇 초 뒤에도 큐브·안내·스피너가 남지 않음");
            Shot("arprev_cancel_early");
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.0f);
        }

        // E11 — 대표 사진 자르기 화면(R0926PhotoCrop) · 처리 중 카드(R0926Busy) (10-20)
        // 네 귀 색이 다른 사진(아래 왼쪽 빨강 · 아래 오른쪽 초록 · 위 왼쪽 파랑 · 위 오른쪽 노랑)을 잘라 결과 화소를 본다 — 끌고 돌린 그대로 잘리는지
        private static void PlanCrop()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            CubeUploadManager cum = null;
            R0926PhotoCrop crop = null;
            R0926Busy busy = null;
            Texture2D got = null;
            bool completed = false, gotDone = false, busySeen = false;
            float seenMax = 0f, seenMin = 0f, gridMax = 0f;
            string busyText = "";
            Vector2 tapM = default, tapImg = default;

            Func<string, object> cf = n => typeof(R0926PhotoCrop).GetField(n, F).GetValue(crop);
            Func<RectTransform> cFrame = () => (RectTransform)cf("frame");
            Func<RectTransform[]> cTiles = () => (RectTransform[])cf("tiles");
            Func<float> cScale = () => (float)cf("scale");
            Func<Vector2> cPos = () => (Vector2)cf("pos");
            Func<float> cCover = () => (float)typeof(R0926PhotoCrop).GetProperty("Cover", F).GetValue(crop);
            Func<Vector2> cLimit = () => (Vector2)typeof(R0926PhotoCrop).GetMethod("Limit", F).Invoke(crop, new object[] { cScale() });
            Func<float> cGrid = () => ((CanvasGroup)cf("grid")).alpha;
            Func<float> cAngle = () => ((RawImage)cf("photo")).rectTransform.localEulerAngles.z;
            Func<Vector2> cCenter = () => cFrame().TransformPoint(cFrame().rect.center);   // 오버레이 캔버스 — 월드 좌표가 곧 화면 좌표
            Func<Text> busyLabel = () => typeof(R0926Busy).GetField("label", F).GetValue(busy) as Text;
            Func<Texture2D> mainPhoto = () => typeof(CubeUploadManager).GetField("mainPhoto", F).GetValue(cum) as Texture2D;
            Func<bool> warn = () => (typeof(CubeUploadManager).GetField("warningObj", F).GetValue(cum) as GameObject)?.activeInHierarchy ?? false;
            Func<string> cropping = () => R0926LocalizedText.Say("자르는 중…", "Cropping…", "切り抜き中…", "裁剪中…", "Recortando…");

            Func<Texture2D> quad = () =>
            {
                const int w = 800, h = 600;
                var src = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color32[w * h];
                Color32 red = new Color32(255, 0, 0, 255), green = new Color32(0, 255, 0, 255), blue = new Color32(0, 0, 255, 255), yellow = new Color32(255, 255, 0, 255);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[y * w + x] = y < h / 2 ? (x < w / 2 ? red : green) : (x < w / 2 ? blue : yellow);
                src.SetPixels32(px);
                src.Apply();
                return src;
            };
            // 자른 결과는 읽기 전용 — GPU 로 옮겨 읽는다. 줄은 위에서 아래로 "/" 로 잇는다
            Func<Texture2D, float[], float[], string> colors = (img, xs, ys) =>
            {
                if (img == null) return "없음";
                var rt = RenderTexture.GetTemporary(img.width, img.height, 0);
                var was = RenderTexture.active;
                Graphics.Blit(img, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(img.width, img.height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0, 0, img.width, img.height), 0, 0, false);
                copy.Apply();
                RenderTexture.active = was;
                RenderTexture.ReleaseTemporary(rt);
                var rows = new List<string>();
                foreach (var fy in ys)
                {
                    string row = "";
                    foreach (var fx in xs)
                    {
                        Color32 c = copy.GetPixel(Mathf.Clamp((int)(fx * copy.width), 0, copy.width - 1), Mathf.Clamp((int)(fy * copy.height), 0, copy.height - 1));
                        bool hr = c.r > 180, hg = c.g > 180, hb = c.b > 180, lr = c.r < 75, lg = c.g < 75, lb = c.b < 75;
                        row += hr && lg && lb ? "빨" : lr && hg && lb ? "초" : lr && lg && hb ? "파" : hr && hg && lb ? "노" : $"?({c.r},{c.g},{c.b})";
                    }
                    rows.Add(row);
                }
                UnityEngine.Object.Destroy(copy);
                return string.Join("/", rows);
            };
            // 돌린 것도 — 네 귀의 최소·최대 (화면 좌표)
            Func<RectTransform, Rect> bounds = rt =>
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return Rect.MinMaxRect(Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x), Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y),
                    Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x), Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y));
            };
            Func<bool> covers = () =>
            {
                Rect fr = bounds(cFrame()), pr = bounds(((RawImage)cf("photo")).rectTransform);
                return pr.xMin <= fr.xMin + 1f && pr.xMax >= fr.xMax - 1f && pr.yMin <= fr.yMin + 1f && pr.yMax >= fr.yMax - 1f;
            };
            Func<Vector2, GameObject> topAt = p =>
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = p }, hits);
                return hits.Count > 0 ? hits[0].gameObject : null;
            };
            // 세로: 위 제목 · 가운데 정사각형 틀 · 아래 한 줄 세 칸. 가로: 오른쪽에 세 칸을 위에서 아래로
            Func<bool, string, bool> layout = (land, tag) =>
            {
                Rect fr = bounds(cFrame()), br = bounds((RectTransform)cf("bar")), hr = bounds((RectTransform)cf("header"));
                var ts = cTiles();
                bool ok = ts.Length == 3 && Mathf.Abs(fr.width - fr.height) < 2f
                    && fr.xMin >= -1f && fr.yMin >= -1f && fr.xMax <= Screen.width + 1f && fr.yMax <= Screen.height + 1f
                    && br.xMin >= -1f && br.yMin >= -1f && br.xMax <= Screen.width + 1f && br.yMax <= Screen.height + 1f
                    && hr.yMin >= fr.yMax - 1f
                    && (land ? br.xMin >= fr.xMax - 1f : br.yMax <= fr.yMin + 1f);
                string desc = $"화면 {Screen.width}x{Screen.height} · 틀 ({fr.x:0},{fr.y:0}) {fr.width:0}x{fr.height:0} · 제목 아래끝 {hr.yMin:0} · 카드 ({br.x:0},{br.y:0}) {br.width:0}x{br.height:0}";
                Rect prev = default;
                for (int i = 0; i < ts.Length; i++)
                {
                    Rect tr = bounds(ts[i]);
                    var label = ts[i].GetComponentInChildren<Text>(true);
                    desc += $" | {ts[i].name} ({tr.x:0},{tr.y:0}) {tr.width:0}x{tr.height:0} 「{(label != null ? label.text : "-")}」 {(label != null ? label.fontSize : 0)}";
                    ok &= label != null && label.gameObject.activeInHierarchy && label.text.Length > 0 && label.fontSize >= 48 && tr.Contains(bounds(label.rectTransform).center);
                    ok &= tr.xMin >= br.xMin - 1f && tr.xMax <= br.xMax + 1f && tr.yMin >= br.yMin - 1f && tr.yMax <= br.yMax + 1f;
                    if (i > 0)
                        ok &= land ? Mathf.Abs(tr.x - prev.x) < 2f && Mathf.Abs(tr.height - prev.height) < 2f && tr.yMax <= prev.yMin + 1f
                                   : Mathf.Abs(tr.y - prev.y) < 2f && Mathf.Abs(tr.width - prev.width) < 2f && tr.xMin >= prev.xMax - 1f;
                    prev = tr;
                }
                Rect t0 = ts.Length > 0 ? bounds(ts[0]) : default;
                ok &= land ? t0.height > Screen.height * 0.18f : t0.width > Screen.width * 0.25f;   // 사진 고르기 시트처럼 큼직하게
                log.Add($"  [{tag}] " + desc);
                return ok;
            };
            Action<Texture2D> open = src =>
            {
                got = null; gotDone = false;
                crop.Open(src, res => { got = res; gotDone = true; UnityEngine.Object.Destroy(src); }, () => UnityEngine.Object.Destroy(src));
            };
            Action<Vector2> doubleTap = p =>
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                crop.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(es)
                {
                    position = p, clickCount = 2,
                    pointerPressRaycast = new UnityEngine.EventSystems.RaycastResult { gameObject = crop.transform.Find("Bg0926").gameObject }
                });
            };
            // 두 손가락 — 틀 가운데를 사이에 두고 gap0 → gap1 (화면 px) 로 벌리거나 오므린 뒤 뗀다. 한 프레임에 한 걸음
            Action<float, float, Action> twoFingers = (gap0, gap1, watch) =>
            {
                Vector2 mid = default;
                int k = 0, fc = -1;
                const int n = 10;
                Action<UnityEngine.InputSystem.TouchPhase, float> put = (ph, g) =>
                {
                    InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = ph, position = mid - new Vector2(g * 0.5f, 0f) });
                    InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 8, phase = ph, position = mid + new Vector2(g * 0.5f, 0f) });
                };
                steps.Enqueue(() => { mid = cCenter(); k = 0; put(UnityEngine.InputSystem.TouchPhase.Began, gap0); fc = Time.frameCount; return true; });
                steps.Enqueue(() =>
                {
                    if (Time.frameCount == fc) return false;
                    fc = Time.frameCount;
                    watch?.Invoke();
                    if (k >= n) return true;
                    k++;
                    put(UnityEngine.InputSystem.TouchPhase.Moved, Mathf.Lerp(gap0, gap1, k / (float)n));
                    return false;
                });
                steps.Enqueue(() => { if (Time.frameCount > fc + 1) return true; watch?.Invoke(); return false; });
                Do(() => put(UnityEngine.InputSystem.TouchPhase.Ended, gap1));
            };
            // 돌린 횟수 · 끄는 방향 → 완료 → 결과 색 (xs 왼쪽→오른쪽, ys 위→아래 비율)
            Action<int, Vector2, string, float[], float[], string> cropCase = (turns, drag, expect, xs, ys, tag) =>
            {
                Do(() => open(quad()));
                Sleep(0.4f);
                for (int i = 0; i < turns; i++) { Do(() => crop.Rotate()); Sleep(0.35f); }
                if (drag != Vector2.zero) { Finger(() => cCenter(), new[] { drag }, 8, null, null); Sleep(0.6f); }
                if (turns == 1 && drag == Vector2.zero) Shot("crop_rotated");
                Check(() =>
                {
                    Vector2 l = cLimit(), p = cPos();
                    Vector2 want = new Vector2(drag.x > 0f ? l.x : drag.x < 0f ? -l.x : 0f, drag.y > 0f ? l.y : drag.y < 0f ? -l.y : 0f);
                    log.Add($"  [{tag}] 각도 {cAngle():0.#}° · 배율 {cScale() / cCover():0.###} · 자리 ({p.x:0.#},{p.y:0.#}) 기대 ({want.x:0.#},{want.y:0.#})");
                    return Mathf.Abs(Mathf.DeltaAngle(cAngle(), 90f * turns)) < 0.5f && Mathf.Abs(cScale() / cCover() - 1f) < 0.01f && (p - want).magnitude < 1f && covers();
                }, $"{tag}: 돌린 모양에 틀을 빈틈없이 덮음" + (drag != Vector2.zero ? " · 끈 쪽 끝까지" : ""));
                Do(() => cTiles()[2].GetComponent<Button>().onClick.Invoke());
                Wait(() => gotDone, 5f, tag + " 자르기 끝");
                Check(() =>
                {
                    string c = colors(got, xs, ys);
                    log.Add($"  [{tag}] 결과 {(got != null ? got.width + "x" + got.height : "없음")} · 색 {c} (기대 {expect})");
                    bool ok = got != null && got.width == 600 && got.height == 600 && c == expect && !crop.IsOpen;
                    if (got != null) UnityEngine.Object.Destroy(got);
                    got = null;
                    return ok;
                }, $"{tag}: 틀 안에 보이던 그대로 잘림");
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                crop = UnityEngine.Object.FindAnyObjectByType<R0926PhotoCrop>(FindObjectsInactive.Include);
                busy = UnityEngine.Object.FindAnyObjectByType<R0926Busy>(FindObjectsInactive.Include);
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });

            // 0) 연결
            Check(() =>
            {
                var fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                var mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                Func<Type, object, string, UnityEngine.Object> get = (t, o, n) => o == null ? null : t.GetField(n, F)?.GetValue(o) as UnityEngine.Object;
                UnityEngine.Object a1 = get(typeof(CubeUploadManager), cum, "photoCrop"), a2 = get(typeof(CubeUploadManager), cum, "busy");
                UnityEngine.Object b1 = get(typeof(CubeDataFixManager), fix, "photoCrop"), b2 = get(typeof(CubeDataFixManager), fix, "busy");
                UnityEngine.Object c2 = get(typeof(ModelUploadManager), mum, "busy");
                log.Add($"  연결: 자르기 {(crop != null ? crop.name : "없음")} · 처리 중 {(busy != null ? busy.name : "없음")} · 장소 추가 {a1 == crop}/{a2 == busy} · 장소 수정 {b1 == crop}/{b2 == busy} · 3D 모델 {c2 == busy}");
                return crop != null && busy != null && a1 == crop && a2 == busy && b1 == crop && b2 == busy && c2 == busy;
            }, "연결: 장소 추가 · 장소 수정 (자르기 + 처리 중) · 3D 모델 (처리 중)");
            Check(() =>
            {
                var ts = cTiles();
                var dockFw = Find("Dock0926/List_Button")?.GetComponent<UITouchForwarder>();
                var clip = dockFw != null ? typeof(UITouchForwarder).GetField("customSound", F).GetValue(dockFw) as AudioClip : null;
                string[] want = { "Cancel", "Rotate", "Done" };
                bool ok = ts != null && ts.Length == 3 && clip != null;
                string desc = "";
                for (int i = 0; ts != null && i < ts.Length && i < 3; i++)
                {
                    var b = ts[i].GetComponent<Button>();
                    var fw = ts[i].GetComponent<UITouchForwarder>();
                    var snd = fw != null ? typeof(UITouchForwarder).GetField("customSound", F).GetValue(fw) as AudioClip : null;
                    bool wired = b != null && b.onClick.GetPersistentEventCount() == 1 && b.onClick.GetPersistentTarget(0) == crop && b.onClick.GetPersistentMethodName(0) == want[i];
                    desc += $" {ts[i].name}({(wired ? want[i] : "연결 X")} · 소리 {(snd != null ? snd.name : "-")})";
                    ok &= wired && snd == clip;
                }
                bool back = ts != null && ts.Length > 0 && ts[0].GetComponent<ClickButtonOnBack>() != null;
                log.Add($"  세 칸:{desc} · 하단 메뉴 소리 {(clip != null ? clip.name : "-")} · 취소에 뒤로가기 {(back ? "O" : "X")}");
                return ok && back;
            }, "세 칸: 취소 · 회전 · 완료 연결 · 하단 메뉴와 같은 물방울 소리 · 뒤로가기 = 취소");
            Check(() =>
            {
                var dim = busy != null ? busy.transform.Find("Dim0926") : null;
                Canvas bc = busy != null ? busy.GetComponent<Canvas>() : null, cc = crop != null ? crop.GetComponent<Canvas>() : null;
                bool swallow = dim != null && dim.GetComponent<Button>() != null && dim.GetComponent<ClickButtonOnBack>() != null;
                log.Add($"  처리 중 카드: 순서 {(bc != null ? bc.sortingOrder : -1)} · 자르기 {(cc != null ? cc.sortingOrder : -1)} · 뒤로가기 삼킴 {(swallow ? "O" : "X")}");
                return swallow && bc != null && cc != null && bc.overrideSorting && cc.overrideSorting && bc.sortingOrder > cc.sortingOrder
                    && !busy.gameObject.activeSelf && !crop.IsOpen;
            }, "처리 중 카드: 자르기 화면보다 위 · 뒤로가기를 삼킴 · 처음엔 둘 다 꺼짐");

            // 1) 장소 추가에서 대표 사진을 고르면 자르기 화면 (뒤 장소 추가 창은 숨김)
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() => { completed = false; typeof(CubeUploadManager).GetMethod("ProcessCropAndDisplay", F).Invoke(cum, new object[] { quad(), (Action)(() => completed = true) }); });
            Sleep(0.6f);
            Shot("crop_open");
            Check(() => crop.IsOpen && !Active("UploadPage") && !busy.gameObject.activeSelf, "대표 사진을 고르면 자르기 화면 (뒤 장소 추가 창은 숨김)");
            Check(() => layout(false, "세로"), "세로: 정사각형 틀이 화면 안 · 제목 아래 · 아래 한 줄 큰 세 칸 (취소 · 회전 · 완료)");
            Check(() => covers() && Mathf.Abs(cScale() / cCover() - 1f) < 0.01f && cPos().magnitude < 1f, "처음: 사진이 틀을 빈틈없이 덮는 가장 작은 크기 · 가운데");
            Check(() =>
            {
                string desc = "";
                bool ok = true;
                foreach (var t in cTiles())
                {
                    var hit = topAt(ScreenRect(t).center);
                    desc += $" {t.name}→{(hit != null ? hit.name : "없음")}";
                    ok &= hit != null && hit.transform.IsChildOf(t);
                }
                log.Add("  세 칸 누름:" + desc);
                return ok;
            }, "세 칸 모두 그 자리를 누르면 그 칸이 받음");

            // 2) 뒤로가기 = 취소 — '자르기 실패' 없이 장소 추가 창으로
            Back();
            Sleep(0.6f);
            Check(() =>
            {
                log.Add($"  취소 뒤: 자르기 {(crop.IsOpen ? "열림" : "닫힘")} · 장소 추가 창 {(Active("UploadPage") ? "보임" : "숨음")} · 끝 알림 {completed} · 안내 {(warn() ? "뜸" : "없음")} · 대표 사진 {(mainPhoto() != null ? "있음" : "없음")}");
                return !crop.IsOpen && Active("UploadPage") && completed && !warn() && mainPhoto() == null;
            }, "뒤로가기 → 취소: 장소 추가 창으로 · '자르기 실패' 안내 없음 · 대표 사진 그대로(없음)");

            // 3) 손가락 — 한 손가락 끌기 (진짜 이벤트 시스템으로)
            Do(() => { completed = false; typeof(CubeUploadManager).GetMethod("ProcessCropAndDisplay", F).Invoke(cum, new object[] { quad(), (Action)(() => completed = true) }); });
            Sleep(0.6f);
            Do(() => { seenMax = float.MinValue; gridMax = 0f; });
            Finger(() => cCenter(), new[] { new Vector2(600f, 0f) }, 8, () => { seenMax = Mathf.Max(seenMax, cPos().x); gridMax = Mathf.Max(gridMax, cGrid()); }, "crop_drag");
            Sleep(0.6f);
            Check(() =>
            {
                Vector2 l = cLimit(), p = cPos();
                log.Add($"  끌기: 끄는 동안 최대 {seenMax:0.#} · 놓은 뒤 ({p.x:0.#},{p.y:0.#}) · 틀 끝 {l.x:0.#} · 3x3 줄 최대 {gridMax:0.##} → {cGrid():0.##}");
                return l.x > 50f && seenMax > l.x + 10f && Mathf.Abs(p.x - l.x) < 1f && Mathf.Abs(p.y) < 1f && gridMax > 0.3f && cGrid() < 0.05f;
            }, "한 손가락 끌기: 손가락을 따라오고, 놓으면 틀 끝까지만 (빈틈 없이) · 끄는 동안만 3x3 줄");

            // 4) 두 번 톡 — 누른 자리를 크게 (누른 곳이 손가락 밑에 그대로) · 다시 두 번 톡 = 처음 크기
            Do(() =>
            {
                var fr = cFrame();
                tapM = new Vector2(fr.rect.width / 4f, fr.rect.width / 8f);
                tapImg = (tapM - cPos()) / cScale();
                doubleTap(fr.TransformPoint(tapM));
            });
            Sleep(0.5f);
            Check(() =>
            {
                Vector2 now = (tapM - cPos()) / cScale();
                log.Add($"  두 번 톡: 배율 {cScale() / cCover():0.###} · 누른 자리의 사진 점 ({tapImg.x:0.#},{tapImg.y:0.#}) → ({now.x:0.#},{now.y:0.#})");
                return Mathf.Abs(cScale() / cCover() - 2.2f) < 0.01f && (now - tapImg).magnitude < 1f && covers();
            }, "두 번 톡: 누른 자리를 2.2배로 (누른 곳이 손가락 밑에 그대로)");
            Do(() => doubleTap(cCenter()));
            Sleep(0.5f);
            Check(() => Mathf.Abs(cScale() / cCover() - 1f) < 0.01f && cPos().magnitude < 1f, "다시 두 번 톡: 처음 크기 · 가운데");

            // 5) 휠 (에디터·PC) — 한 칸 15%
            Do(() =>
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                float per = es.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule im ? im.scrollDeltaPerTick : 1f;
                crop.OnScroll(new UnityEngine.EventSystems.PointerEventData(es) { position = cCenter(), scrollDelta = new Vector2(0f, 3f * per) });
                log.Add($"  휠: 한 칸 {per} → 배율 {cScale() / cCover():0.###}");
            });
            Check(() => Mathf.Abs(cScale() / cCover() - Mathf.Pow(1.15f, 3f)) < 0.01f, "휠 세 칸: 1.15³ ≈ 1.52배");
            Do(() => doubleTap(cCenter()));
            Sleep(0.5f);

            // 6) 두 손가락 — 벌린 만큼 · 틀보다 작게 줄였다 놓으면 다시 꽉 차게
            twoFingers(120f, 360f, null);
            Sleep(0.6f);
            Check(() =>
            {
                float k = cScale() / cCover();
                log.Add($"  두 손가락 벌림: 배율 {k:0.###} · 터치 화면 {(Touchscreen.current == ptTouch ? "시험 손가락" : Touchscreen.current != null ? Touchscreen.current.name : "없음")}");
                return Mathf.Abs(k - 3f) < 0.1f && covers();
            }, "두 손가락 벌리기: 벌린 만큼 커짐 (3배)");
            Do(() => seenMin = float.MaxValue);
            twoFingers(360f, 72f, () => seenMin = Mathf.Min(seenMin, cScale() / cCover()));
            Sleep(0.6f);
            Check(() =>
            {
                float k = cScale() / cCover();
                log.Add($"  두 손가락 오므림: 가장 작을 때 {seenMin:0.###} → 놓은 뒤 {k:0.###}");
                return seenMin < 0.99f && Mathf.Abs(k - 1f) < 0.01f && cPos().magnitude < 1f && covers();
            }, "두 손가락 오므리기: 틀보다 작게 줄였다 놓으면 다시 꽉 차게");

            // 7) 오른쪽으로 끌어 둔 채 완료 — 바로 '자르는 중…' · 끌어 둔 그대로 대표 사진에
            Finger(() => cCenter(), new[] { new Vector2(600f, 0f) }, 8, null, null);
            Sleep(0.6f);
            Do(() =>
            {
                cTiles()[2].GetComponent<Button>().onClick.Invoke();
                busySeen = busy.Showing;
                busyText = busyLabel() != null ? busyLabel().text : "";
            });
            Check(() => { log.Add($"  완료 누른 순간: 카드 {(busySeen ? "보임" : "없음")} 「{busyText}」"); return busySeen && busyText == cropping(); }, "완료를 누르면 바로 '자르는 중…' 카드");
            Wait(() => completed, 5f, "자르기 끝");
            Sleep(0.4f);
            Shot("crop_done");
            Check(() =>
            {
                var mp = mainPhoto();
                string c = colors(mp, new[] { 0.25f, 0.6f, 0.73f }, new[] { 0.75f, 0.25f });
                bool preview = Active("ARPreviewPanel");
                log.Add($"  완료: 대표 사진 {(mp != null ? mp.width + "x" + mp.height : "없음")} · 색 {c} (기대 파파노/빨빨초) · {(preview ? "AR 미리보기" : Active("UploadPage") ? "장소 추가 창" : "?")} · 안내 {(warn() ? "뜸" : "없음")} · 카드 {(busy.gameObject.activeSelf ? "남음" : "걷힘")}");
                return mp != null && mp.width == 600 && mp.height == 600 && c == "파파노/빨빨초" && !crop.IsOpen && !warn() && !busy.gameObject.activeSelf && (preview || Active("UploadPage"));
            }, "완료: 끌어 둔 그대로 잘려 대표 사진에 (600x600) · 카드 걷힘 · 안내 없음 → AR 미리보기(또는 장소 추가 창)");
            Do(() => { if (Active("ARPreviewPanel")) Click("ARPreviewPanel/MessageContainer/ButtonContainer/CancelButton"); });
            Sleep(1.2f);
            Check(() => Active("UploadPage") && !crop.IsOpen, "미리보기를 닫으면 장소 추가 창으로");

            // 8) 돌리기 — 왼쪽으로 90°씩, 돌린 그대로 잘림
            cropCase(0, Vector2.zero, "파노/빨초", new[] { 0.25f, 0.75f }, new[] { 0.75f, 0.25f }, "그대로");
            cropCase(1, Vector2.zero, "노초/파빨", new[] { 0.25f, 0.75f }, new[] { 0.75f, 0.25f }, "왼쪽으로 한 번");
            cropCase(1, new Vector2(0f, 600f), "노초/파빨/파빨", new[] { 0.25f, 0.75f }, new[] { 0.72f, 0.6f, 0.25f }, "왼쪽으로 한 번 + 위로 끌기");
            cropCase(2, Vector2.zero, "초빨/노파", new[] { 0.25f, 0.75f }, new[] { 0.75f, 0.25f }, "두 번");
            cropCase(3, Vector2.zero, "빨파/초노", new[] { 0.25f, 0.75f }, new[] { 0.75f, 0.25f }, "세 번");

            // 9) 자르는 중 카드는 자르기 화면 위 — 그동안 세 칸이 안 눌림
            Do(() => open(quad()));
            Sleep(0.4f);
            Do(() => busy.Show(cropping()));
            Sleep(0.4f);
            Shot("crop_busy");
            Check(() =>
            {
                GameObject mid = topAt(cCenter()), done = topAt(ScreenRect(cTiles()[2]).center);
                log.Add($"  자르는 중: 틀 가운데 맨 위 {(mid != null ? mid.name : "없음")} · 완료 칸 자리 맨 위 {(done != null ? done.name : "없음")}");
                return busy.Showing && crop.IsOpen && mid != null && mid.transform.IsChildOf(busy.transform) && done != null && done.transform.IsChildOf(busy.transform);
            }, "자르는 중 카드: 자르기 화면 위 · 그동안 세 칸이 안 눌림");
            Do(() => busy.Hide());
            Sleep(0.4f);
            Do(() => crop.Cancel());
            Sleep(0.3f);

            // 10) 올리는 중 — 장소 추가 창 위에 카드 + 막대 · 뒤로가기도 삼킴
            Do(() => typeof(CubeUploadManager).GetMethod("ShowUploadProgress", F).Invoke(cum, new object[] { 42 }));
            Sleep(0.4f);
            Shot("crop_upload_busy");
            Check(() =>
            {
                var lb = busyLabel();
                var barGo = typeof(R0926Busy).GetField("bar", F).GetValue(busy) as GameObject;
                var fill = typeof(R0926Busy).GetField("barFill", F).GetValue(busy) as RectTransform;
                var mid = topAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                var xb = Find("UploadPage/DockMirror0926/XButton_Upload") as RectTransform;
                var onX = xb != null ? topAt(ScreenRect(xb).center) : null;
                string expect = R0926LocalizedText.Say("업로드 중…", "Uploading…", "アップロード中…", "上传中…", "Subiendo…") + " 42%";
                log.Add($"  올리는 중: 「{(lb != null ? lb.text : "-")}」 막대 {(barGo != null && barGo.activeInHierarchy ? "보임" : "없음")} 채움 {(fill != null ? fill.anchorMax.x : -1f):0.00} · 가운데 맨 위 {(mid != null ? mid.name : "없음")} · 닫기 자리 맨 위 {(onX != null ? onX.name : "없음")}");
                return busy.Showing && lb != null && lb.text == expect && barGo != null && barGo.activeInHierarchy && fill != null && Mathf.Abs(fill.anchorMax.x - 0.42f) < 0.001f
                    && mid != null && mid.transform.IsChildOf(busy.transform) && onX != null && onX.transform.IsChildOf(busy.transform) && Active("UploadPage");
            }, "올리는 중: 장소 추가 창 위에 '업로드 중… 42%' 카드 + 막대 · 그동안 아래(닫기 포함)는 안 눌림");
            Back();
            Sleep(0.5f);
            Check(() => busy.Showing && Active("UploadPage"), "올리는 중 뒤로가기: 카드가 삼켜 장소 추가 창이 닫히지 않음");
            Do(() => typeof(CubeUploadManager).GetMethod("HideBusy", F).Invoke(cum, null));
            Sleep(0.4f);
            Check(() => !busy.gameObject.activeSelf && Active("UploadPage"), "다 올리면 카드가 걷힘");
            Check(() =>
            {
                var fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                var mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                typeof(CubeDataFixManager).GetMethod("ShowUploadProgress", F).Invoke(fix, new object[] { 10 });
                string a = busyLabel().text;
                typeof(ModelUploadManager).GetMethod("ShowUploadProgress", F).Invoke(mum, new object[] { 77 });
                string b = busyLabel().text;
                typeof(ModelUploadManager).GetMethod("HideBusy", F).Invoke(mum, null);
                string ea = R0926LocalizedText.Say("보내는 중…", "Sending…", "送信中…", "发送中…", "Enviando…") + " 10%";
                string eb = R0926LocalizedText.Say("업로드 중…", "Uploading…", "アップロード中…", "上传中…", "Subiendo…") + " 77%";
                log.Add($"  장소 수정 「{a}」 · 3D 모델 「{b}」");
                return a == ea && b == eb;
            }, "장소 수정 '보내는 중… N%' · 3D 모델 '업로드 중… N%' — 같은 카드");
            Sleep(0.4f);
            Do(() => { busy.Show("시험"); typeof(R0926Busy).GetField("shownAt", F).SetValue(busy, Time.unscaledTime - 21f); });
            Sleep(0.5f);
            Check(() => !busy.gameObject.activeSelf, "부른 쪽이 20초 넘게 소식이 없으면 카드가 저절로 걷힘 (앱이 막히지 않게)");

            // 11) 가로 — 오른쪽에 세 칸을 세로로, 왼쪽에 틀
            Do(() => open(quad()));
            Sleep(0.4f);
            Do(() => Rotate(true));
            Wait(() => Turned(true), 8f, "가로로 돌림");
            Sleep(1.0f);
            Shot("crop_land");
            Check(() => layout(true, "가로") && covers(), "가로: 오른쪽에 세 칸 세로로 (취소 · 회전 · 완료) · 왼쪽에 틀 · 사진이 틀을 덮음");
            Do(() => Rotate(false));
            Wait(() => Turned(false), 8f, "세로로 되돌림");
            Sleep(1.0f);
            Check(() => layout(false, "다시 세로") && covers(), "다시 세로: 제자리 · 사진이 틀을 덮음");
            Do(() => crop.Cancel());
            Sleep(0.3f);
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.0f);
            Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
            });
        }

        // E7 — 설정 ▸ 장소 ▸ '멀리 있는 오브젝트 흐리게' 네 칸: 누르면 저장값 · 칸 표시 · 안개 · 실제 큐브 투명도가 바로 바뀐다 (10-20)
        private static GameObject fadeCube, nearCube;
        private static readonly float[] fadeRatio = new float[4];
        private static readonly float[] nearEdge = new float[4];
        private static readonly float[] fadeFogEnd = new float[4];
        private static void PlanFarFade()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const string C = "ListPanel/Sheet0926/SettingsPanel0926/Viewport/Content/";
            const string R = C + "GroupHidden/RowFarFade/";
            int before = 2;
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                before = R0926PlaceSettings.FarFade;
                log.Add("  처음 단계 " + before);
            });
            Wait(() => (fadeCube = FarCube(false)) != null, 25f, "큐브 생김");
            Do(() =>
            {
                nearCube = FarCube(true);
                var cam = Camera.main;
                foreach (var c in new[] { fadeCube, nearCube })
                    log.Add(c != null && cam != null ? $"  잴 큐브 '{c.name}' 거리 {Vector3.Distance(cam.transform.position, c.transform.position):0}m" : "  잴 큐브 없음");
            });
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => Modes()?.Show(2));
            Sleep(1.2f);
            Do(() => ScrollToChild(C + "SecPlaces"));
            Sleep(0.6f);
            Shot("farfade_settings");
            Check(() => Active(R + "Seg/Step0") && Active(R + "Seg/Step3") && Find(R + "Seg/Step4") == null, "설정 ▸ 장소에 '멀리 있는 오브젝트 흐리게' 네 칸");
            Check(() =>
            {
                var tr = Find(R + "Title")?.GetComponent<Text>();
                return tr != null && tr.text.Length > 0 && Find(C + "GroupHidden/RowFarFade").GetSiblingIndex() == 1;
            }, "제목이 있고 '오브젝트 삭제 기능' 바로 아래");
            for (int i = 0; i < 4; i++)
            {
                int lv = i;
                Do(() => Click(R + "Seg/Step" + lv));
                Sleep(0.7f);
                Shot("farfade_tab" + lv);
                Do(() =>
                {
                    var df = UnityEngine.Object.FindAnyObjectByType<R0926DistanceFade>();
                    int applied = df != null ? (int)typeof(R0926DistanceFade).GetField("level", F).GetValue(df) : -9;
                    float farA = df != null ? (float)typeof(R0926DistanceFade).GetField("farA", F).GetValue(df) : -1f;
                    fadeRatio[lv] = CubeAlphaRatio(fadeCube);
                    nearEdge[lv] = CubeAlphaRatio(nearCube, "_EdgeIntensity");
                    fadeFogEnd[lv] = RenderSettings.fogEndDistance;
                    bool tabs = true;
                    for (int k = 0; k < 4; k++)
                    {
                        var img = Find(R + "Seg/Step" + k)?.GetComponent<Image>();
                        if (img == null || (k == lv) != (img.color.a > 0.1f)) tabs = false;
                    }
                    bool ok = R0926PlaceSettings.FarFade == lv && PlayerPrefs.GetInt("Place_FarFade", -1) == lv && applied == lv && tabs && RenderSettings.fog == (lv > 0);
                    log.Add((ok ? "PASS " : "FAIL ") + $"단계 {lv}: 저장 {R0926PlaceSettings.FarFade} · 적용 {applied} · 칸 표시 {(tabs ? "맞음" : "틀림")} · 안개 {(RenderSettings.fog ? "켜짐" : "꺼짐")} 끝 {RenderSettings.fogEndDistance:0}m · 가장 멀 때 투명도 {farA:0.00} · 먼 큐브 {fadeRatio[lv]:0.00} · 가까운 큐브 테두리 {nearEdge[lv]:0.00}");
                });
            }
            Check(() => fadeRatio[0] > 0.97f, "끄기: 먼 큐브도 투명도 그대로 (1)");
            Check(() => fadeRatio[0] > fadeRatio[1] + 0.01f && fadeRatio[1] > fadeRatio[2] + 0.01f && fadeRatio[2] > fadeRatio[3] + 0.01f, "세질수록 먼 큐브가 더 옅다 (끄기 > 약하게 > 보통 > 강하게)");
            Check(() => fadeFogEnd[1] > fadeFogEnd[2] && fadeFogEnd[2] > fadeFogEnd[3], "세질수록 안개가 가까이에서 짙어진다");
            Check(() => nearEdge[0] > 0f && Mathf.Abs(nearEdge[0] - nearEdge[3]) < 0.02f && Mathf.Abs(nearEdge[1] - nearEdge[2]) < 0.02f, "가까운 큐브 테두리 빛은 어느 단계든 같다 (끄기를 골라도 가까운 건 그대로)");
            Do(() => Modes()?.Show(0));
            Sleep(0.6f);
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(1.2f);
            Do(() => R0926PlaceSettings.FarFade = 0);
            Sleep(0.7f);
            Shot("farfade_ar0");
            Do(() => R0926PlaceSettings.FarFade = 3);
            Sleep(0.7f);
            Shot("farfade_ar3");
            Do(() => R0926PlaceSettings.FarFade = before);
            Sleep(0.4f);
            Check(() => R0926PlaceSettings.FarFade == before, "시험 뒤 원래 단계로 되돌림");
        }

        // 지금 보이는 큐브(투명도 셰이더) 가운데 카메라에서 가장 먼 것(nearest 면 가장 가까운 것) — 흐림 세기를 실제 값으로 재려고
        private static GameObject FarCube(bool nearest)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var all = new List<GameObject>();
            if (DataManager.Instance?.GetSpawnedObjects() != null) all.AddRange(DataManager.Instance.GetSpawnedObjects().Values);
            if (TourAPIManager.Instance?.GetSpawnedObjects() != null) all.AddRange(TourAPIManager.Instance.GetSpawnedObjects().Values);
            if (SubwayManager.Instance?.GetSpawnedObjects() != null) all.AddRange(SubwayManager.Instance.GetSpawnedObjects().Values);
            if (TrainStationManager.Instance?.GetSpawnedObjects() != null) all.AddRange(TrainStationManager.Instance.GetSpawnedObjects().Values);
            if (TerminalManager.Instance?.GetSpawnedObjects() != null) all.AddRange(TerminalManager.Instance.GetSpawnedObjects().Values);
            GameObject best = null;
            float pick = nearest ? float.MaxValue : 60f;   // 먼 쪽은 60m 넘는 것만 — 40m 안쪽은 단계와 상관없이 그대로라 잴 수 없다
            foreach (var go in all)
            {
                if (go == null || !go.activeInHierarchy || CubeAlphaRatio(go) < 0f) continue;
                float d = Vector3.Distance(cam.transform.position, go.transform.position);
                if (nearest ? d < pick : d > pick) { pick = d; best = go; }
            }
            return best;
        }

        // 큐브에 실제로 들어간 투명도(또는 테두리 빛) ÷ 재질 원래 값 (투명도 셰이더가 없거나 꺼져 있으면 -1)
        private static float CubeAlphaRatio(GameObject go, string prop = "_GlobalAlpha")
        {
            if (go == null) return -1f;
            var block = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty("_GlobalAlpha") || !mats[i].HasProperty(prop)) continue;
                    float baseA = mats[i].GetFloat(prop);
                    r.GetPropertyBlock(block, i);
                    if (baseA <= 0f || !block.HasFloat(prop)) return -1f;
                    return block.GetFloat(prop) / baseA;
                }
            }
            return -1f;
        }

        // ── 10-22 N12: 일반 오브젝트도 보물상자처럼 — 누를 때마다 살짝 커졌다 돌아오고, 두 번 톡톡이면 커지며 사진이 뜬다 ──
        private static void PlanNudge()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            Object3DTouchHaptic hap = null;
            DoubleTap3D dt = null;
            float rest = 1f, peak = 0f, atTap2 = 0f;
            Func<float> ratio = () => hap != null ? hap.transform.localScale.x / rest : 0f;
            Func<bool> open = () => dt != null && dt.fullscreenCanvasGroup != null && dt.fullscreenCanvasGroup.gameObject.activeSelf;
            Func<float> lastTap = () => dt != null ? (float)typeof(DoubleTap3D).GetField("lastTapTime", F).GetValue(dt) : 0f;
            // 가상 손가락 한 번 — 오브젝트 가운데를 누르고 두 프레임 뒤에 뗀다
            Action<int> tap = id =>
            {
                int f = -1;
                Vector2 p = default;
                steps.Enqueue(() =>
                {
                    if (hap == null || ptTouch == null) return true;
                    if (f < 0)
                    {
                        Vector3 sp = Camera.main.WorldToScreenPoint(hap.GetComponent<Collider>().bounds.center);
                        p = new Vector2(sp.x, sp.y);
                        InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Began, position = p });
                        f = Time.frameCount;
                    }
                    peak = Mathf.Max(peak, ratio());
                    if (Time.frameCount < f + 2) return false;
                    InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = p });
                    return true;
                });
            };
            // sec 초 동안 크기를 지켜본다 (가장 컸던 배율) — 커져 있을 때 한 장 찍는다 (따로 찍으면 찍는 두 프레임 동안 정점을 놓친다)
            Action<float, string> watch = (sec, shot) =>
            {
                double until = -1;
                bool shotDone = shot == null;
                steps.Enqueue(() =>
                {
                    if (until < 0) until = EditorApplication.timeSinceStartup + sec;
                    peak = Mathf.Max(peak, ratio());
                    if (!shotDone && ratio() >= 1.06f) { ScreenCapture.CaptureScreenshot(Path.Combine(dir, "test_" + shot + ".png")); shotDone = true; }
                    return EditorApplication.timeSinceStartup >= until;
                });
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                ptFocusWas = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;   // 에디터 창이 뒤에 있어도
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Wait(() => (hap = NudgePick()) != null, 25f, "누를 오브젝트");
            Do(() =>
            {
                dt = hap != null ? hap.GetComponent<DoubleTap3D>() : null;
                rest = hap != null ? hap.transform.localScale.x : 1f;
                log.Add(hap != null ? $"  누를 오브젝트 '{hap.transform.parent.name}' 거리 {Vector3.Distance(Camera.main.transform.position, hap.transform.position):0.0}m · 크기 {rest:0.00}" : "  누를 오브젝트 없음");
            });
            Sleep(0.4f);
            Shot("nudge_rest");

            // 1) 한 번 — 살짝 커졌다 돌아온다 · 사진은 안 뜬다
            Do(() => peak = 0f);
            tap(7);
            watch(1.4f, "nudge_tap1");
            Do(() => log.Add($"  한 번: 가장 클 때 {peak:0.000}배 · 지금 {ratio():0.000}배 · 사진 {(open() ? "뜸" : "안 뜸")}"));
            Check(() => peak > 1.05f && peak < 1.16f && Mathf.Abs(ratio() - 1f) < 0.003f && !open(), "한 번 누름: 살짝 커졌다(1.05~1.15배) 제 크기로 · 사진은 안 뜸");

            // 2) 두 번 톡톡 — 두 번째는 첫 누름이 닿고 0.15초 뒤 (DoubleTap3D 는 게임 시간 0.1~0.3초를 두 번으로 본다)
            Sleep(0.4f);
            Do(() => peak = 0f);
            tap(7);
            steps.Enqueue(() => dt == null || Time.time >= lastTap() + 0.15f);
            Do(() => { atTap2 = ratio(); peak = 0f; });
            tap(8);
            watch(0.5f, "nudge_tap2");
            Do(() => log.Add($"  두 번: 두 번째 누를 때 {atTap2:0.000}배 → 가장 클 때 {peak:0.000}배 · 사진 {(open() ? "뜸" : "안 뜸")}"));
            Check(() => open() && peak > atTap2 + 0.01f && peak < 1.16f, "두 번 톡톡: 한 번 더 살짝 커지며 사진 화면이 뜬다");
            watch(1.2f, null);
            Shot("nudge_open");
            Check(() => Mathf.Abs(ratio() - 1f) < 0.003f, "사진이 뜬 뒤 오브젝트는 제 크기로 돌아와 있음");
            Do(() => { if (open()) typeof(DoubleTap3D).GetMethod("CloseFullscreen", F).Invoke(dt, null); });
            Sleep(0.8f);
            Check(() => !open() && Mathf.Abs(ratio() - 1f) < 0.003f, "사진 화면을 닫음 · 크기 그대로");

            Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
            });
        }

        // ── 10-23 A2: 아이폰이 'AR 세션' 화면에서 꺼지던 것 — 상세 사진은 열 때 받고 한 번에 한 장소 것만 · 풀로 돌아가면 사진을 놓는다 ──
        private static void PlanPhotoMem()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const System.Reflection.BindingFlags S = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            int idA = -1, idB = -1;
            DoubleTap3D a = null, b = null;
            GameObject pooled = null;
            Texture2D faceWas = null, fake = null;
            bool poolOk = false;
            P2PUserInfo pu = null;
            Func<DoubleTap3D, int> urls = d => d != null ? ((List<string>)typeof(DoubleTap3D).GetField("imageUrls", F).GetValue(d)).Count : -1;
            Func<DoubleTap3D, int> owned = d => d != null ? ((List<Sprite>)typeof(DoubleTap3D).GetField("ownedPhotos", F).GetValue(d)).Count : -1;
            Func<DoubleTap3D, bool> loaded = d => d != null && (bool)typeof(DoubleTap3D).GetField("photosLoaded", F).GetValue(d);
            Func<DoubleTap3D> holder = () => typeof(DoubleTap3D).GetField("photoHolder", S).GetValue(null) as DoubleTap3D;
            Func<DoubleTap3D, bool> open = d => d != null && d.fullscreenCanvasGroup != null && d.fullscreenCanvasGroup.gameObject.activeSelf;
            Func<int, GameObject> spawned = id => DataManager.Instance != null && DataManager.Instance.GetSpawnedObjects().TryGetValue(id, out var go) ? go : null;
            Func<GameObject, ImageDisplayController> disp = go => go != null ? go.GetComponentInChildren<ImageDisplayController>(true) : null;
            Func<ImageDisplayController, Texture2D> face = c => c != null ? typeof(ImageDisplayController).GetField("baseMapTexture", F).GetValue(c) as Texture2D : null;
            Func<int, DoubleTap3D> dtOf = id => spawned(id) != null ? spawned(id).GetComponentInChildren<DoubleTap3D>(true) : null;

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            // 상세 사진이 있는 우팡 큐브 둘 (면 사진까지 받은 것)
            Wait(() =>
            {
                if (DataManager.Instance == null) return false;
                var map = DataManager.Instance.GetPlaceDataMap();
                idA = idB = -1;
                foreach (var kv in DataManager.Instance.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy || !map.TryGetValue(kv.Key, out var p) || p.model_type != "cube") continue;
                    if (p.sub_photos == null || urls(kv.Value.GetComponentInChildren<DoubleTap3D>(true)) <= 0 || face(disp(kv.Value)) == null) continue;
                    if (idA < 0) idA = kv.Key; else { idB = kv.Key; break; }
                }
                return idB > 0;
            }, 40f, "상세 사진이 있는 큐브 둘");
            Do(() =>
            {
                a = dtOf(idA);
                b = dtOf(idB);
                log.Add($"  큐브 A {idA} 상세 사진 주소 {urls(a)}장 · B {idB} {urls(b)}장 · 면 사진 {face(disp(spawned(idA))).width}px");
            });
            Check(() => a.imageSprites.Count == 0 && owned(a) == 0 && b.imageSprites.Count == 0 && owned(b) == 0, "뜰 때는 상세 사진을 받지 않는다 (주소만)");
            Check(() => !face(disp(spawned(idA))).isReadable && !face(disp(spawned(idB))).isReadable, "면 사진은 읽기 전용 (CPU 쪽 사본 없음)");

            // 1) A 를 열면 그때 받는다
            Do(() => typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", F).Invoke(a, null));
            Wait(() => open(a) && loaded(a), 30f, "A 상세 사진 받음");
            Sleep(0.6f);
            Shot("photomem_open_a");
            Do(() => log.Add($"  A 를 열고: 받은 사진 {a.imageSprites.Count}장 · 사진을 든 오브젝트 {(holder() == a ? "A" : holder() == null ? "없음" : holder().name)}"));
            Check(() => a.imageSprites.Count == urls(a) && owned(a) == urls(a) && holder() == a, "A 를 열면 그때 받는다 — 주소 수만큼 · 사진을 든 건 A 하나");
            Check(() => a.imageSprites.TrueForAll(x => x != null && x.texture != null && !x.texture.isReadable), "상세 사진도 읽기 전용");
            Do(() => typeof(DoubleTap3D).GetMethod("CloseFullscreen", F).Invoke(a, null));
            Sleep(1.2f);
            Check(() => !open(a) && owned(a) == urls(a), "닫아도 A 사진은 그대로 (다시 열면 바로 보인다)");

            // 2) B 를 열면 A 사진은 놓는다
            Do(() => typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", F).Invoke(b, null));
            Wait(() => open(b) && loaded(b), 30f, "B 상세 사진 받음");
            Sleep(0.6f);
            Shot("photomem_open_b");
            Do(() => log.Add($"  B 를 열고: A 사진 {owned(a)}장 · B 사진 {owned(b)}장"));
            Check(() => holder() == b && owned(a) == 0 && a.imageSprites.Count == 0 && owned(b) == urls(b), "B 를 열면 A 사진은 지운다 — 한 번에 한 장소 것만");
            Do(() => typeof(DoubleTap3D).GetMethod("CloseFullscreen", F).Invoke(b, null));
            Sleep(1.2f);

            // 3) B 를 풀로 돌려보낸다 — 면 사진·상세 사진을 놓고 프리팹 원래 면으로
            Do(() =>
            {
                pooled = spawned(idB);
                faceWas = face(disp(pooled));
                DataManager.Instance.DespawnFullObject(idB.ToString());
                var c = disp(pooled);
                var def = typeof(ImageDisplayController).GetField("defaultFaceTexture", F).GetValue(c) as Texture;
                var now = c.cubeRenderer.material.HasProperty("_BaseMap") ? c.cubeRenderer.material.GetTexture("_BaseMap") : c.cubeRenderer.material.mainTexture;
                poolOk = !pooled.activeSelf && face(c) == null && owned(b) == 0 && b.imageSprites.Count == 0 && holder() == null && now == def;
                log.Add($"  풀로 간 B: 켜짐 {pooled.activeSelf} · 면 사진 {(face(c) == null ? "놓음" : "들고 있음")} · 상세 사진 {owned(b)}장 · 면 {(now == def ? "원래 면" : now != null ? now.name : "없음")}");
            });
            Sleep(0.3f);
            Check(() => poolOk && faceWas == null, "풀로 돌아간 큐브는 면 사진을 지우고(메모리에서) 상세 사진도 놓고 원래 면으로");
            Do(() => DataManager.Instance.SpawnFullObject(idB.ToString()));
            Wait(() => face(disp(spawned(idB))) != null, 30f, "B 다시 뜨고 면 사진 다시 받음");
            Check(() => !face(disp(spawned(idB))).isReadable && urls(dtOf(idB)) > 0 && dtOf(idB).imageSprites.Count == 0, "다시 뜬 B: 면 사진은 다시 받고 상세 사진은 주소만");

            // 3b) 열자마자 넘기면 — 아직 안 온 칸으로 넘어가 있다가 사진이 도착하면 뜬다
            int slot = -9;
            Do(() =>
            {
                typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", F).Invoke(a, null);
                a.ShowNextImage();
                slot = (int)typeof(DoubleTap3D).GetField("imageIndex", F).GetValue(a);
                log.Add($"  A 를 다시 열고 바로 넘김: 받은 사진 {a.imageSprites.Count}장 · 지금 칸 {slot}");
            });
            Wait(() => open(a) && loaded(a), 30f, "A 상세 사진 다시 받음");
            Sleep(0.8f);
            Shot("photomem_slot");
            Check(() => slot >= 0 && slot < a.imageSprites.Count && a.fullscreenImage.enabled && a.fullscreenImage.sprite == a.imageSprites[slot]
                        && (int)typeof(DoubleTap3D).GetField("imageIndex", F).GetValue(a) == slot, "열자마자 넘겨도 그 칸 사진이 도착하면 뜬다");
            Do(() => typeof(DoubleTap3D).GetMethod("CloseFullscreen", F).Invoke(a, null));
            Sleep(1.2f);

            // 4) 관광공사 — 미리 받지 않고 우팡 서버(/locations/번호)로 찾지 않는다
            Check(() =>
            {
                var objs = TourAPIManager.Instance != null ? TourAPIManager.Instance.GetSpawnedObjects() : null;
                if (objs == null || objs.Count == 0) { log.Add("  관광공사 오브젝트 없음 — 건너뜀"); return true; }
                int n = 0, eager = 0, fallback = 0, withUrls = 0;
                foreach (var go in objs.Values)
                {
                    var d = go != null ? go.GetComponentInChildren<DoubleTap3D>(true) : null;
                    if (d == null) continue;
                    n++;
                    if (owned(d) > 0) eager++;
                    if (urls(d) > 0) withUrls++;
                    if ((bool)typeof(DoubleTap3D).GetField("serverPhotoFallback", F).GetValue(d)) fallback++;
                }
                log.Add($"  관광공사 오브젝트 {n} · 사진 주소 받은 것 {withUrls} · 미리 받은 것 {eager} · 우팡 서버로 찾는 것 {fallback}");
                return eager == 0 && fallback == 0;
            }, "관광공사: 상세 사진을 미리 받지 않고 · 우팡 서버로 찾지 않는다");

            // 5) 근처 사람 — 풀에서 다른 사람으로 다시 쓰이면 앞사람 사진을 지우고 원래 면으로
            Do(() =>
            {
                var vt = UnityEngine.Object.FindAnyObjectByType<P2PVirtualUserTester>(FindObjectsInactive.Include);
                if (vt != null) { vt.StopSimulation(); vt.StopAllCoroutines(); }
                foreach (var u in UnityEngine.Object.FindObjectsByType<P2PUserInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!u.gameObject.activeInHierarchy) { pu = u; break; }
                if (pu == null) return;
                pu.Initialize("pt_a2_user1", "pt1", "", "", 10f);
                fake = new Texture2D(8, 8);
                typeof(P2PUserInfo).GetMethod("ApplyProfileTextureToAvatar", F).Invoke(pu, new object[] { fake });
                typeof(P2PUserInfo).GetField("avatarTextureUrl", F).SetValue(pu, "pt://avatar1");
                pu.Initialize("pt_a2_user2", "pt2", "", "", 10f);
            });
            Sleep(0.3f);
            Check(() =>
            {
                if (pu == null) { log.Add("  쉬고 있는 사람 오브젝트 없음 — 건너뜀"); return true; }
                var r = (MeshRenderer)typeof(P2PUserInfo).GetField("avatarRenderer", F).GetValue(pu);
                var def = typeof(P2PUserInfo).GetField("defaultAvatarTexture", F).GetValue(pu) as Texture;
                var now = r.material.HasProperty("_BaseMap") ? r.material.GetTexture("_BaseMap") : r.material.mainTexture;
                bool held = typeof(P2PUserInfo).GetField("avatarTexture", F).GetValue(pu) as Texture2D != null;
                log.Add($"  앞사람 사진 {(fake == null ? "지워짐" : "남아 있음")} · 지금 면 {(now == def ? "원래 면" : now != null ? now.name : "없음")}");
                return fake == null && !held && now == def;
            }, "근처 사람: 다른 사람으로 다시 쓰이면 앞사람 사진을 지우고 원래 면으로");

            // 6) 알림 기록 — 최근 500개만
            Check(() =>
            {
                var fn = UnityEngine.Object.FindAnyObjectByType<FirebaseNotification>(FindObjectsInactive.Include);
                if (fn == null) { log.Add("  FirebaseNotification 없음 — 건너뜀"); return true; }
                var set = (HashSet<string>)typeof(FirebaseNotification).GetField("processedMessageIds", F).GetValue(fn);
                var order = (Queue<string>)typeof(FirebaseNotification).GetField("processedOrder", F).GetValue(fn);
                var remember = typeof(FirebaseNotification).GetMethod("RememberProcessed", F);
                var keep = order.ToArray();
                for (int i = 0; i < 600; i++) remember.Invoke(fn, new object[] { "pt_a2_" + i });
                bool ok = set.Count == 500 && order.Count == 500 && !set.Contains("pt_a2_0") && !set.Contains("pt_a2_99") && set.Contains("pt_a2_100") && set.Contains("pt_a2_599");
                log.Add($"  처음 {keep.Length}개 · 600개 더 넣은 뒤 {set.Count}개 (가장 오래된 것부터 빠짐 {(ok ? "맞음" : "틀림")})");
                set.Clear();
                order.Clear();
                foreach (var k in keep) remember.Invoke(fn, new object[] { k });   // 시험 전으로 (저장은 안 했다)
                return ok && set.Count == keep.Length;
            }, "알림 기록: 최근 500개만 기억 — 오래된 것부터 빠진다");
        }

        // ── 10-22 N18: 사람(P2P) 오브젝트도 핀치 줌을 따른다 — 본 크기(2배) × 배율 · 줌 중에 새로 뜬 사람·풀에서 다시 꺼낸 사람도 ──
        private static void PlanP2PZoom()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            ARObjectZoomController z = null;
            float baseS = 0f;
            var modeWas = UserFilterMode.All;
            // 지금 가상 위치에서 북쪽으로 m 미터 — 에디터는 이 차이로 카메라 앞에 세운다
            Func<string, int, string> user = (id, m) =>
            {
                double lat = VirtualLocation.Instance.Latitude + m / 111320.0, lon = VirtualLocation.Instance.Longitude;
                return $"{{\"user_id\":\"{id}\",\"username\":\"{id}\",\"latitude\":{lat:0.0000000},\"longitude\":{lon:0.0000000},\"altitude\":0,\"distance\":{m}}}";
            };
            Func<string[], string> near = ids =>
            {
                var parts = new List<string>();
                for (int i = 0; i < ids.Length; i++) parts.Add(user(ids[i], 12 + i * 6));
                return "{\"users\":[" + string.Join(",", parts) + "]}";
            };
            Func<Dictionary<string, GameObject>> avatars = () => typeof(P2PManager).GetField("activeUserAvatars", F).GetValue(P2PManager.Instance) as Dictionary<string, GameObject>;
            Func<string, float> sc = id => avatars().TryGetValue(id, out var go) && go != null ? go.transform.localScale.x : -1f;
            Func<float, string[], bool> all = (k, ids) =>
            {
                string desc = "";
                bool ok = true;
                foreach (var id in ids) { float v = sc(id); desc += $" {id} {v:0.00}"; if (Mathf.Abs(v - baseS * k) > 0.001f) ok = false; }
                log.Add($"  배율 {k:0.0} (본 {baseS:0.0} → {baseS * k:0.00}):" + desc);
                return ok;
            };
            Action<float> zoomTo = k =>
            {
                typeof(ARObjectZoomController).GetField("currentZoom", F).SetValue(z, k);
                typeof(ARObjectZoomController).GetMethod("ApplyZoomToARObjects", F).Invoke(z, null);
            };
            Action<string> mode = m =>
            {
                var um = (UserFilterMode)Enum.Parse(typeof(UserFilterMode), m);
                var fp = UnityEngine.Object.FindAnyObjectByType<P2PUserFilterPanel>();
                if (fp != null) fp.SetFilterModeExternal(um); else P2PManager.Instance?.SetUserFilterMode(um);
            };
            Action face = () =>
            {
                var cam = Camera.main;
                if (cam == null || !avatars().TryGetValue("zt1", out var go) || go == null) return;
                Vector3 d = go.transform.position - cam.transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) cam.transform.rotation = Quaternion.LookRotation(d);
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                // 에디터의 가상 사용자(1초마다 근처 사람 'woopang'을 넣는다)가 시험이 넣은 근처 사람 목록을 덮어쓰지 않게
                var vt = UnityEngine.Object.FindAnyObjectByType<P2PVirtualUserTester>();
                if (vt != null) { vt.StopSimulation(); vt.StopAllCoroutines(); }
                z = UnityEngine.Object.FindAnyObjectByType<ARObjectZoomController>(FindObjectsInactive.Include);
                var tpl = typeof(P2PManager).GetField("userAvatarPrefab", F).GetValue(P2PManager.Instance) as GameObject;
                baseS = tpl != null ? tpl.transform.localScale.x : 0f;
                var fp = UnityEngine.Object.FindAnyObjectByType<P2PUserFilterPanel>();
                modeWas = fp != null ? fp.GetCurrentFilterMode() : P2PManager.Instance.GetUserFilterMode();
                mode("All");
                log.Add($"  줌 {(z != null ? "있음" : "없음")} · 사람 오브젝트 본 크기 {baseS:0.0} · 필터 {modeWas} → All");
            });
            Sleep(0.4f);

            // 1) 줌 1 — 본 크기 그대로
            Do(() => P2PManager.Instance.InjectTestUsersData(near(new[] { "zt1", "zt2" })));
            Sleep(0.5f);
            Do(face);
            Sleep(0.4f);
            Shot("p2pzoom_1x");
            Check(() => baseS > 0f && all(1f, new[] { "zt1", "zt2" }), "줌 1: 사람 오브젝트는 본 크기 그대로 (반으로 줄지 않음)");

            // 2) 1.5배 — 떠 있는 사람도 같이 커지고, 줌 중에 새로 뜬 사람도 1.5배로 나온다
            Do(() => zoomTo(1.5f));
            Sleep(0.4f);
            Shot("p2pzoom_15x");
            Check(() => all(1.5f, new[] { "zt1", "zt2" }), "핀치 1.5배: 떠 있는 사람 오브젝트도 1.5배");
            Do(() =>
            {
                var dm = DataManager.Instance?.GetSpawnedObjects();
                foreach (var kv in dm ?? new Dictionary<int, GameObject>()) { if (kv.Value != null && kv.Value.activeSelf) { log.Add($"  (참고) 장소 오브젝트 '{kv.Value.name}' 크기 {kv.Value.transform.localScale.x:0.00}"); break; } }
            });
            Do(() => P2PManager.Instance.InjectTestUsersData(near(new[] { "zt1", "zt2", "zt3" })));
            Sleep(0.4f);
            Check(() => all(1.5f, new[] { "zt1", "zt2", "zt3" }), "줌 중에 새로 뜬 사람도 지금 배율(1.5배)로");

            // 3) 0.6배로 줄이고 · 되돌리면 본 크기
            Do(() => zoomTo(0.6f));
            Sleep(0.3f);
            Check(() => all(0.6f, new[] { "zt1", "zt2", "zt3" }), "핀치 0.6배: 작아짐");
            Do(() => z.ResetZoom());
            Sleep(0.3f);
            Check(() => all(1f, new[] { "zt1", "zt2", "zt3" }), "줌 되돌리기: 본 크기");

            // 4) 사라졌다 풀에서 다시 꺼낸 사람도 그때 배율로
            Do(() => { foreach (var id in new[] { "zt1", "zt2", "zt3" }) P2PManager.Instance.RemoveTestUserAvatar(id); });
            Do(() => zoomTo(2f));
            Do(() => P2PManager.Instance.InjectTestUsersData(near(new[] { "zt1" })));
            Sleep(0.3f);
            Check(() => all(2f, new[] { "zt1" }), "풀에서 다시 꺼낸 사람도 지금 배율(2배)로");

            Do(() =>
            {
                z.ResetZoom();
                P2PManager.Instance.RemoveTestUserAvatar("zt1");
                mode(modeWas.ToString());
            });
            Sleep(0.3f);
            Check(() => avatars().Count == 0 || !avatars().ContainsKey("zt1"), "시험 사람 정리 · 줌·필터 되돌림");
        }

        // 누를 오브젝트 — 가까운 것부터 카메라를 돌려 화면 가운데에 두고, 그 자리를 누르면 그 오브젝트가 맞는 것 (앞을 다른 것이 안 가림)
        private static Object3DTouchHaptic NudgePick()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var all = new List<GameObject>();
            if (DataManager.Instance?.GetSpawnedObjects() != null) all.AddRange(DataManager.Instance.GetSpawnedObjects().Values);
            if (TourAPIManager.Instance?.GetSpawnedObjects() != null) all.AddRange(TourAPIManager.Instance.GetSpawnedObjects().Values);
            if (SubwayManager.Instance?.GetSpawnedObjects() != null) all.AddRange(SubwayManager.Instance.GetSpawnedObjects().Values);
            if (TrainStationManager.Instance?.GetSpawnedObjects() != null) all.AddRange(TrainStationManager.Instance.GetSpawnedObjects().Values);
            if (TerminalManager.Instance?.GetSpawnedObjects() != null) all.AddRange(TerminalManager.Instance.GetSpawnedObjects().Values);
            all.RemoveAll(go => go == null || !go.activeInHierarchy);
            Vector3 eye = cam.transform.position;
            all.Sort((a, b) => Vector3.Distance(eye, a.transform.position).CompareTo(Vector3.Distance(eye, b.transform.position)));
            foreach (var go in all)
            {
                var h = go.GetComponentInChildren<Object3DTouchHaptic>();
                var c = h != null ? h.GetComponent<Collider>() : null;
                if (c == null || !c.enabled || h.GetComponent<DoubleTap3D>() == null) continue;
                cam.transform.rotation = Quaternion.LookRotation(c.bounds.center - eye);
                Vector3 sp = cam.WorldToScreenPoint(c.bounds.center);
                if (sp.z > 0f && Physics.Raycast(cam.ScreenPointToRay(sp), out var hit) && hit.collider == c) return h;
            }
            return null;
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
            });
            Check(() => ListToBottom("세로"), "목록: 저작권 줄 없이 시트 아래 끝까지 (저작권은 설정 › 앱 정보로 — 10-23)");
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
            });
            Check(() => FooterOrder("세로"), "설정 맨 아래: 버전이 위 · 저작권 그림이 아래");
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926SettingsPanel>(FindObjectsInactive.Include)?.SetFps(1));
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

        // 설정 › 앱 정보 (10-23 대표님) — 문의 master@woopang.com · 버전 아래 저작권 그림 · 개인정보처리방침이 앱 안에서 바로 열림 ·
        // 목록은 저작권 줄 없이 시트 아래 끝까지. 세로와 가로 둘 다
        private const string SetPath = "ListPanel/Sheet0926/SettingsPanel0926/";
        private const string PrivacyPath = SetPath + "PrivacyPage0926";
        private static void PlanAppInfo()
        {
            const string C = SetPath + "Viewport/Content/";
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                if (Active("LoginPromptPanel")) BackNow();
            });
            Sleep(0.6f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.3f);
            Do(() => Modes()?.Show(0));
            Sleep(0.8f);
            Shot("appinfo_list");
            Check(() => ListToBottom("세로"), "세로: 목록이 시트 아래 끝까지 · 저작권 줄 없음");
            Do(() => Modes()?.Show(2));
            Sleep(0.9f);
            Do(ScrollSettingsBottom);
            Sleep(0.6f);
            Shot("appinfo_settings");
            Check(() => (Find(C + "GroupInfo/RowContact/Value")?.GetComponent<Text>()?.text ?? "") == "master@woopang.com", "문의: master@woopang.com");
            Check(() => FooterOrder("세로"), "세로: 버전이 위 · 저작권 그림이 아래 (보이는 칸 안)");
            Do(() => Click(C + "GroupInfo/RowPrivacy"));
            Sleep(0.8f);
            Shot("appinfo_privacy");
            Check(() => PrivacyShown("세로"), "개인정보처리방침: 누르면 앱 안에서 바로 (제목 11개 · 설정 쪽을 덮음)");
            Do(() => ScrollPrivacy(0f));
            Sleep(0.6f);
            Shot("appinfo_privacy_end");
            Check(PrivacyEnd, "끝까지 내리면 마지막 줄(시행일)이 보이는 칸 안");
            Do(BackNow);
            Sleep(0.8f);
            Check(() => !Active(PrivacyPath) && Active(C + "GroupInfo/RowPrivacy") && Active("ListPanel"), "안드로이드 뒤로가기 → 설정으로 (목록 시트는 그대로)");
            Do(() => Click(C + "GroupInfo/RowPrivacy"));
            Sleep(0.8f);
            Do(() => Click("ListPanel/Sheet0926/ModeSeg0926/TabSettings"));
            Sleep(0.8f);
            Check(() => !Active(PrivacyPath), "설정 탭을 누르면 닫힘");
            // 가로 — 연 채로 돌린다
            Do(() => Click(C + "GroupInfo/RowPrivacy"));
            Sleep(0.6f);
            Do(() => Rotate(true));
            Wait(() => Turned(true), 8f, "가로로 돌림");
            Sleep(1.2f);
            Shot("appinfo_land_privacy");
            Check(() => PrivacyShown("가로"), "가로: 개인정보처리방침이 설정 쪽을 덮고 화면 안");
            Check(() => TopHit(PrivacyPath + "/Top0926").EndsWith("(그 칸)"), "가로: ‹ 줄을 누를 수 있음");
            Do(() => ScrollPrivacy(0f));
            Sleep(0.6f);
            Shot("appinfo_land_privacy_end");
            Check(PrivacyEnd, "가로: 끝까지 내리면 마지막 줄이 보이는 칸 안");
            Do(() => Click(PrivacyPath + "/Top0926"));
            Sleep(0.8f);
            Check(() => !Active(PrivacyPath), "가로: ‹ 로 닫힘");
            Do(ScrollSettingsBottom);
            Sleep(0.6f);
            Shot("appinfo_land_settings");
            Check(() => FooterOrder("가로"), "가로: 버전이 위 · 저작권 그림이 아래 (보이는 칸 안)");
            Do(() => Modes()?.Show(0));
            Sleep(0.9f);
            Shot("appinfo_land_list");
            Check(() => ListToBottom("가로"), "가로: 목록이 시트 아래 끝까지");
            Do(() => Rotate(false));
            Wait(() => Turned(false), 8f, "세로로 되돌림");
            Sleep(0.8f);
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(1.0f);
        }

        // 목록이 시트 아래 끝까지 내려오는지 — 저작권 줄을 설정으로 옮긴 뒤 (10-23)
        private static bool ListToBottom(string when)
        {
            var sheet = Find("ListPanel/Sheet0926") as RectTransform;
            RectTransform list = null;
            if (sheet != null) foreach (var t in sheet.GetComponentsInChildren<RectTransform>(true)) if (t.name == "Scroll View") { list = t; break; }
            bool copy = false;
            foreach (var t in CanvasRoot().GetComponentsInChildren<Transform>(true)) if (t.name == "copyright") { copy = true; break; }
            if (sheet == null || list == null || !list.gameObject.activeInHierarchy) { log.Add($"  {when}: 목록 없음"); return false; }
            float scale = sheet.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            Rect s = ScreenRect(sheet), l = ScreenRect(list);
            float gap = (l.yMin - s.yMin) / scale;
            log.Add($"  {when}: 목록 아래 {l.yMin:0} · 시트 아래 {s.yMin:0} → 여백 {gap:0} · 저작권 그림 {(copy ? "있음" : "없음")}");
            return !copy && gap >= 0f && gap <= 40f && l.yMin >= -1f;
        }

        // 설정 맨 아래 — 버전이 위, 저작권 그림이 아래, 둘 다 보이는 칸 안 (10-23)
        private static bool FooterOrder(string when)
        {
            var ver = Find(SetPath + "Viewport/Content/VersionFooter") as RectTransform;
            var cp = Find(SetPath + "Viewport/Content/CopyrightFooter") as RectTransform;
            var vp = Find(SetPath + "Viewport") as RectTransform;
            if (ver == null || cp == null || vp == null) { log.Add($"  {when}: 버전 {ver != null} · 저작권 {cp != null}"); return false; }
            var img = cp.GetComponent<Image>();
            var vt = ver.GetComponentInChildren<Text>();
            Rect v = ScreenRect(ver), c = ScreenRect(cp), w = ScreenRect(vp);
            log.Add($"  {when}: 버전 '{vt?.text}' {v.yMin:0}~{v.yMax:0} · 저작권 {c.yMin:0}~{c.yMax:0} '{(img != null && img.sprite != null ? img.sprite.name : "-")}' · 보이는 칸 {w.yMin:0}~{w.yMax:0}");
            return img != null && img.sprite != null && img.sprite.name == "r0926_copyright" && vt != null && vt.text.StartsWith("Version ")
                && v.yMin >= c.yMax - 1f && c.yMin >= w.yMin - 1f && v.yMax <= w.yMax + 1f && c.yMin >= -1f;
        }

        private static void ScrollSettingsBottom()
        {
            var sr = UnityEngine.Object.FindAnyObjectByType<R0926SettingsPanel>(FindObjectsInactive.Include)?.GetComponent<ScrollRect>();
            if (sr == null) { log.Add("  설정 스크롤 없음"); return; }
            Canvas.ForceUpdateCanvases();
            sr.StopMovement();
            sr.verticalNormalizedPosition = 0f;
        }

        private static void ScrollPrivacy(float v)
        {
            var sr = Find(PrivacyPath + "/Scroll0926")?.GetComponent<ScrollRect>();
            if (sr == null) { log.Add("  개인정보처리방침 스크롤 없음"); return; }
            Canvas.ForceUpdateCanvases();
            sr.StopMovement();
            sr.verticalNormalizedPosition = v;
        }

        // 개인정보처리방침 — 제목 11개 · 줄이 다 만들어지고, 설정 쪽을 덮고, 화면 안 (10-23)
        private static bool PrivacyShown(string when)
        {
            var page = Find(PrivacyPath) as RectTransform;
            var sp = Find(SetPath.TrimEnd('/')) as RectTransform;
            if (page == null || sp == null || !page.gameObject.activeInHierarchy) { log.Add($"  {when}: 개인정보처리방침이 안 열림"); return false; }
            var content = Find(PrivacyPath + "/Scroll0926/Viewport/Content");
            int heads = 0, lines = 0;
            Text note = null;
            if (content != null)
                foreach (Transform t in content)
                {
                    if (!t.gameObject.activeSelf) continue;
                    if (t.name == "Head") heads++;
                    if (t.name != "Gap") lines++;
                    if (note == null && t.name == "Note") note = t.GetComponent<Text>();
                }
            Rect p = ScreenRect(page), s = ScreenRect(sp);
            bool covers = Mathf.Abs(p.xMin - s.xMin) < 2f && Mathf.Abs(p.xMax - s.xMax) < 2f && Mathf.Abs(p.yMax - s.yMax) < 2f && Mathf.Abs(p.yMin - s.yMin) < 2f;
            bool onScreen = p.xMin >= -1f && p.xMax <= Screen.width + 1f && p.yMin >= -1f && p.yMax <= Screen.height + 1f;
            log.Add($"  {when}: 제목 {heads} · 줄 {lines} · 첫 줄 '{note?.text}' · 쪽 {p.xMin:0}~{p.xMax:0} × {p.yMin:0}~{p.yMax:0} · 설정 쪽 {s.xMin:0}~{s.xMax:0} × {s.yMin:0}~{s.yMax:0}");
            return heads == 11 && lines > 50 && note != null && note.text.Contains("2026") && covers && onScreen;
        }

        // 끝까지 내리면 맨 끝 줄('… 시행합니다')이 보이는 칸 안에
        private static bool PrivacyEnd()
        {
            var vp = Find(PrivacyPath + "/Scroll0926/Viewport") as RectTransform;
            var content = vp != null ? vp.Find("Content") : null;
            if (content == null) return false;
            Text last = null;
            foreach (var t in content.GetComponentsInChildren<Text>()) last = t;
            if (last == null) return false;
            Rect l = ScreenRect(last.rectTransform), w = ScreenRect(vp);
            log.Add($"  맨 끝 줄 '{last.text}' {l.yMin:0}~{l.yMax:0} · 보이는 칸 {w.yMin:0}~{w.yMax:0}");
            return last.text.Contains("2026") && l.yMin >= w.yMin - 1f && l.yMax <= w.yMax + 1f;
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

        // 10-21 대표님 — 관리자 메세지(WOOPANG)가 같은 이름으로 두 줄 · WOOPANG 대화만 아래에서 올라옴 · 대화창 스켈레톤은 늦을 때만.
        // 이 기기 알림(업로드 완료·위치·새 팔로워)은 WOOPANG 한 줄 · 한 대화로 합치고, 목록에서 열면 다른 대화처럼 옆으로 넘어간다
        private const string WoNote = "woopang_one_test_";
        private static List<ConversationSummary> Convs() =>
            typeof(MessagePanelManager).GetField("conversations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(Mpm()) as List<ConversationSummary>;
        private static object MpmField(string name)
        {
            var m = Mpm();
            var f = typeof(MessagePanelManager).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return m != null && f != null ? f.GetValue(m) : null;
        }
        private static void MpmCall(string method, params object[] args)
        {
            var mi = typeof(MessagePanelManager).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mi != null) mi.Invoke(Mpm(), args); else log.Add("  메서드 없음 " + method);
        }
        private static bool ChatSkeletonUp() { var g = MpmField("chatSkeleton") as GameObject; return g != null; }
        private static bool WoNoteSaved(string id) { var list = Convs(); return (list != null && list.Exists(c => c.notificationId == id)) || PlayerPrefs.GetString("SystemNotifications", "").Contains(id); }
        // 목록에서 'WOOPANG' 이라고 쓰인 줄 수
        private static int WoopangRowCount()
        {
            var m = Mpm();
            if (m == null || m.conversationListContent == null) return -1;
            int n = 0;
            foreach (Transform row in m.conversationListContent)
            {
                if (!row.gameObject.activeSelf) continue;
                foreach (var t in row.GetComponentsInChildren<Text>(true))
                    if (t.text == "WOOPANG") { n++; break; }
            }
            return n;
        }
        private static Transform WoopangRowPart(string name)
        {
            var h = SwRow("woopang_admin");
            if (h == null) return null;
            var t = h.transform.Find("Content/" + name);
            return t != null ? t : h.transform.Find(name);
        }
        private static string WoopangPreview() { var t = WoopangRowPart("PreviewText"); var tx = t != null ? t.GetComponent<Text>() : null; return tx != null ? tx.text : ""; }
        private static string WoopangBadge()
        {
            var b = WoopangRowPart("UnreadBadge");
            if (b == null || !b.gameObject.activeSelf) return "";
            var t = b.GetComponentInChildren<Text>(true);
            return t != null ? t.text : "?";
        }
        private static void TapWoopangRow()
        {
            var h = SwRow("woopang_admin");
            Button b = null;
            var content = h != null ? h.transform.Find("Content") : null;
            if (content != null) b = content.GetComponent<Button>();
            if (b == null && h != null) b = h.GetComponent<Button>();
            if (b != null) b.onClick.Invoke(); else log.Add("  WOOPANG 줄 버튼 없음");
        }
        // 목록의 WOOPANG 줄이 보여야 할 안 읽음 = 서버 공지 요약 줄 + 이 기기의 안 읽은 알림
        private static int WoopangUnreadExpected()
        {
            var list = Convs();
            if (list == null) return -1;
            int n = 0;
            bool admin = false;
            foreach (var c in list)
            {
                if (c.isSystemMessage && !c.isAdminBroadcast && c.userId != "3") { if (!c.isRead) n++; }
                else if (!admin && (c.isSystemMessage || c.isAdminBroadcast || c.userId == "3")) { admin = true; n += c.unreadCount; }
            }
            return n;
        }
        // 대화창 말풍선 글 (위에서 아래로) — 날짜 구분선·빈 안내는 뺀다
        private static List<string> ChatBubbleTexts()
        {
            var list = new List<string>();
            var m = Mpm();
            if (m == null || m.chatMessageContent == null) return list;
            foreach (Transform b in m.chatMessageContent)
            {
                var box = b.gameObject.activeSelf ? b.Find("BubbleContainer") : null;
                if (box == null) continue;
                var sb = new System.Text.StringBuilder();
                foreach (var t in box.GetComponentsInChildren<Text>(false)) sb.Append(t.text);
                list.Add(sb.ToString());
            }
            return list;
        }
        private static float NavOffset()
        {
            var nav = R0926ChatNav.Instance;
            var f = typeof(R0926ChatNav).GetField("chatApplied", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return nav != null && f != null ? (float)f.GetValue(nav) : 0f;
        }
        private static float ChatRise()
        {
            var nav = R0926ChatNav.Instance;
            var sf = typeof(R0926ChatNav).GetField("chatSlide", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var s = nav != null && sf != null ? sf.GetValue(nav) as R0926SlideIn : null;
            var af = typeof(R0926SlideIn).GetField("applied", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return s != null && af != null ? (float)af.GetValue(s) : 0f;
        }

        private static void PlanWoopangOne()
        {
            float side = 0f, rise = 0f;
            bool skelSeen = false, skelEarly = false;
            double openedAt = 0, loadSec = -1;
            const string Body1 = "우팡 한 줄 시험 — 근처 새 오브젝트", Body2 = "우팡한줄친구님이 회원님을 팔로우했습니다", Body3 = "우팡 한 줄 시험 — 대화 보는 중에 온 알림";
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() => { var m = Mpm(); if (m != null && m.messagePanel != null) m.messagePanel.SetActive(true); });
            Sleep(1.0f);
            Do(() => log.Add("  처음 WOOPANG 줄 " + WoopangRowCount() + "개"));

            // 1) 알림 둘(보통 · 새 팔로워)이 와도 WOOPANG 은 한 줄 — 미리보기는 마지막 알림, 안 읽음은 합
            Do(() => Mpm()?.AddLocationNotificationFromPush("📍 우팡 한 줄 시험", Body1, WoNote + "1"));
            Sleep(1.1f);   // 시각은 초 단위 — 둘째 알림이 확실히 뒤에 오게
            Do(() => Mpm()?.AddLocationNotificationFromPush("새로운 팔로워", Body2, WoNote + "2", 0f, 0f, 0f, "", "woopang_one_friend"));
            Sleep(0.6f);
            Shot("woopang1_list");
            Do(() => log.Add("  WOOPANG 줄 " + WoopangRowCount() + "개 · 미리보기 '" + WoopangPreview() + "' · 배지 '" + WoopangBadge() + "' (기대 " + WoopangUnreadExpected() + ")"));
            Check(() => WoopangRowCount() == 1 && SwipeRows().FindAll(h => h.userId == "woopang_admin").Count == 1, "WOOPANG 은 한 줄 (알림이 와도 줄이 늘지 않음)");
            Check(() => SwipeRows().TrueForAll(h => h.userId == null || !h.userId.StartsWith(WoNote)), "알림마다 따로 생기던 줄 없음");
            Check(() => WoopangPreview().StartsWith("새로운 팔로워"), "미리보기는 마지막 알림");
            Check(() => { int e = WoopangUnreadExpected(); return e >= 2 && WoopangBadge() == e.ToString(); }, "안 읽음 배지 = 공지 + 이 기기 알림");

            // 2) WOOPANG 줄을 누르면 다른 대화처럼 옆으로 넘어간다 (아래에서 올라오지 않음) · 빨리 오면 스켈레톤 없음
            Do(() => { side = 0f; rise = 0f; skelSeen = false; openedAt = EditorApplication.timeSinceStartup; loadSec = -1; TapWoopangRow(); skelEarly = ChatSkeletonUp(); });
            Wait(() =>
            {
                side = Mathf.Max(side, NavOffset());
                rise = Mathf.Max(rise, Mathf.Abs(ChatRise()));
                skelSeen |= ChatSkeletonUp();
                if (loadSec < 0 && !(bool)MpmField("chatLoading")) loadSec = EditorApplication.timeSinceStartup - openedAt;
                return !Active("MessagePanel") && Active("ChatRoomPanel") && loadSec >= 0;
            }, 12f, "WOOPANG 대화 열림");
            Sleep(0.4f);
            Shot("woopang1_chat");
            Do(() => log.Add("  옆으로 " + side.ToString("0") + " · 아래에서 " + rise.ToString("0") + " · 불러오기 " + loadSec.ToString("0.00") + "초 · 스켈레톤 " + (skelSeen ? "보임" : "안 보임")));
            Check(() => side > 100f && rise < 1f, "WOOPANG 줄 → 대화방이 옆에서 들어옴 (아래에서 올라오지 않음)");
            Check(() => Active("ChatRoomPanel") && !Active("MessagePanel"), "넘긴 뒤 대화방만");
            Check(() => !skelEarly && (loadSec >= 0.33 || !skelSeen) && !ChatSkeletonUp(), "스켈레톤은 늦을 때만 (바로 띄우지 않음 · 끝나면 치움)");
            Do(() => { var b = ChatBubbleTexts(); log.Add("  말풍선 " + b.Count + "개 · 끝 둘: " + (b.Count >= 2 ? b[b.Count - 2].Replace("\n", " ") + " / " + b[b.Count - 1].Replace("\n", " ") : "-")); });
            Check(() =>
            {
                var b = ChatBubbleTexts();
                int i1 = b.FindIndex(s => s.Contains(Body1)), i2 = b.FindIndex(s => s.Contains(Body2));
                return i1 >= 0 && i2 == b.Count - 1 && i1 < i2;
            }, "이 기기 알림도 WOOPANG 대화 안에 (시간 순 · 최신이 맨 아래)");
            Check(() => { var b = ChatBubbleTexts(); var f = b.Find(s => s.Contains(Body2)); return f != null && f.Contains("›"); }, "새 팔로워 알림은 '›' 표시");
            Check(() =>
            {
                var m = Mpm();
                if (m == null) return false;
                foreach (Transform b in m.chatMessageContent)
                {
                    var box = b.Find("BubbleContainer");
                    if (box == null) continue;
                    bool follow = false;
                    foreach (var t in box.GetComponentsInChildren<Text>(false)) if (t.text.Contains(Body2)) follow = true;
                    if (!follow) continue;
                    var btn = box.GetComponent<Button>();
                    var img = box.GetComponent<Image>();
                    return btn != null && img != null && img.raycastTarget;
                }
                return false;
            }, "새 팔로워 말풍선은 누를 수 있음 (→ 내 팔로워)");
            Check(() => { var m = Mpm(); var l = Convs(); return m != null && m.GetUnreadLocationNotificationCount() == 0 && l != null && l.FindAll(c => c.notificationId != null && c.notificationId.StartsWith(WoNote)).TrueForAll(c => c.isRead); },
                "열면 이 기기 알림도 읽음");

            // 3) 대화를 보는 중에 온 알림은 맨 아래에 바로 붙고 읽은 것으로
            Do(() => Mpm()?.AddLocationNotificationFromPush("📍 우팡 한 줄 시험 3", Body3, WoNote + "3"));
            Sleep(0.5f);
            Shot("woopang1_live");
            Check(() => { var b = ChatBubbleTexts(); return b.Count > 0 && b[b.Count - 1].Contains(Body3) && b.FindAll(s => s.Contains(Body3)).Count == 1; }, "보는 중에 온 알림 → 맨 아래 한 번");
            Check(() => { var l = Convs(); var c = l != null ? l.Find(x => x.notificationId == WoNote + "3") : null; return c != null && c.isRead && Mpm().GetUnreadLocationNotificationCount() == 0; }, "보는 중에 온 알림은 읽음");

            // 늦을 때의 스켈레톤 — 불러오기를 일부러 늦출 수 없어 '늦으면 띄우기'만 직접 돌린다: 0.35초 뒤 서서히 뜨고, 다 오면 치운다
            Do(() =>
            {
                var m = Mpm();
                var e = typeof(MessagePanelManager).GetMethod("SkeletonIfSlow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(m, new object[] { true }) as System.Collections.IEnumerator;
                typeof(MessagePanelManager).GetField("chatSkeletonWait", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(m, m.StartCoroutine(e));
            });
            Wait(() => { var g = MpmField("chatSkeleton") as GameObject; var cg = g != null ? g.GetComponent<CanvasGroup>() : null; return cg != null && cg.alpha > 0.99f; }, 3f, "늦을 때 스켈레톤 뜸");
            Shot("woopang1_skeleton");
            Check(() => { var g = MpmField("chatSkeleton") as GameObject; return g != null && g.activeInHierarchy && !g.GetComponent<CanvasGroup>().blocksRaycasts; }, "늦으면 스켈레톤이 서서히 뜸 (누름은 막지 않음)");
            Do(() => MpmCall("EndChatSkeleton"));
            Sleep(0.2f);
            Check(() => !ChatSkeletonUp() && MpmField("chatSkeletonWait") == null, "다 오면 스켈레톤 치움");

            // 4) ← 로 목록에 돌아오면 그대로 한 줄 · 안 읽음 없음
            Do(() => Click("ChatRoomPanel/Clip0926/Background/Header/BackButton/ClosePx0926"));
            Wait(() => Active("MessagePanel") && !Active("ChatRoomPanel"), 4f, "목록으로");
            Sleep(0.6f);
            Shot("woopang1_back");
            Check(() => WoopangRowCount() == 1 && WoopangBadge() == "", "돌아오면 WOOPANG 한 줄 · 안 읽음 없음");
            // 로그인돼 있으면 ← 뒤 도크 메시지 버튼이 목록을 다시 받아 그린다 — 시험은 로그아웃이라 그 다시 그리기만 직접
            Do(() => MpmCall("RefreshConversationUI"));
            Sleep(0.3f);
            Do(() => log.Add("  다시 그린 뒤 WOOPANG 줄 " + WoopangRowCount() + "개 · 미리보기 '" + WoopangPreview() + "' · 배지 '" + WoopangBadge() + "'"));
            Check(() => WoopangRowCount() == 1 && WoopangBadge() == "" && WoopangPreview().Contains("시험 3"), "다시 그려도 한 줄 · 미리보기는 보는 중에 온 알림");

            // 정리 — 시험 알림은 저장값에서도 지운다
            Do(() =>
            {
                for (int i = 1; i <= 3; i++)
                    if (WoNoteSaved(WoNote + i)) MpmCall("DeleteSystemNotification", WoNote + i, null);
                MpmCall("RefreshConversationUI");
            });
            Sleep(0.4f);
            Check(() => !WoNoteSaved(WoNote + "1") && !WoNoteSaved(WoNote + "2") && !WoNoteSaved(WoNote + "3"), "시험 알림 지움");
        }

        // 가로 화면 메시지 (10-14 대표님 — 메시지 탭이 안 뜨기도 하고, 대화창 말풍선이 제목·입력칸을 가리며 위아래로 들썩)
        // 게임 화면을 가로로 돌려 메시지 목록 · 대화방 · 키보드(가로 높이)에서 제목줄 · 말풍선 칸 · 입력칸이 겹치지 않는지 잰다
        private static void PlanLand()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() => Rotate(true));
            Wait(() => Turned(true), 8f, "가로로 돌림");
            Sleep(1.0f);
            Shot("land_home");
            Check(() => TopHit("Dock0926/Message_Button").EndsWith("(그 칸)"), "가로: 도크 막대의 메시지 칸을 누를 수 있음");
            Do(() => { var m = Mpm(); if (m != null && m.messagePanel != null) m.messagePanel.SetActive(true); });   // 도크 메시지 — 로그인 확인 뒤에 여는 것과 같다
            Sleep(1.0f);
            Shot("land_msg");
            Check(() => OnScreen("MessagePanel/Clip0926/Background"), "가로: 메시지 목록이 화면 안");
            Check(() => LeftOfRail("MessagePanel"), "가로: 메시지 목록이 오른쪽 도크 막대를 가리지 않음");
            Check(() => TopHit("MessagePanel/DockMirror0926/CloseButton").EndsWith("(그 칸)"), "가로: 도크 막대의 닫기를 누를 수 있음");
            Do(() => Mpm()?.OpenChatRoom("chatlook_peer", "연남동이웃", null, false));
            Sleep(1.6f);
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Sleep(0.4f);
            Do(() => FillSampleChat(false));
            Sleep(0.8f);
            Shot("land_chat");
            Check(() => ChatFits("가로"), "가로 대화방: 말풍선 칸이 제목줄 아래 · 입력칸 위");
            Do(() => { var k = Mkh(); var c = CanvasRoot() as RectTransform; if (k != null && c != null) k.EditorPreviewKeyboard(c.rect.height * 0.55f); });   // 가로 키보드는 화면 높이의 절반 남짓
            Sleep(1.4f);
            Shot("land_chat_kb");
            Check(() => ChatFits("가로 + 키보드"), "가로 대화방 + 키보드: 입력칸이 키보드 위 · 말풍선 칸이 제목줄과 입력칸 사이");
            ChatSteady("가로 + 키보드");
            Do(KeyboardDown);
            Sleep(2.2f);   // 키보드가 내려간 걸 1초 기다린 뒤 돌아간다
            Shot("land_chat_after");
            Check(() => ChatFits("키보드 내린 뒤"), "키보드를 내리면 제자리");
            // 대화방을 연 채 세로 ↔ 가로
            Do(() => Rotate(false));
            Wait(() => Turned(false), 8f, "세로로 돌림");
            Sleep(0.8f);
            Shot("land_chat_portrait");
            Check(() => ChatFits("세로"), "대화방을 연 채 세로로: 겹치지 않음");
            Do(() => { var k = Mkh(); var c = CanvasRoot() as RectTransform; if (k != null && c != null) k.EditorPreviewKeyboard(c.rect.height * 0.38f); });
            Sleep(1.4f);
            Shot("land_chat_portrait_kb");
            Check(() => ChatFits("세로 + 키보드"), "세로 + 키보드: 그대로 (예전 자리)");
            // 키보드를 띄운 채 가로로 — 새 방향 배치 위에서 키보드 위로 (키보드가 내려가면 옛 방향 자리로 돌아가 가로 배치를 덮던 것)
            Do(() => Rotate(true));
            Wait(() => Turned(true), 8f, "키보드 띄운 채 가로로");
            Do(() => { var k = Mkh(); var c = CanvasRoot() as RectTransform; if (k != null && c != null) k.EditorPreviewKeyboard(c.rect.height * 0.55f); });   // 기기 키보드도 돌면 새로 놓인다
            Sleep(1.4f);
            Shot("land_chat_turn_kb");
            Check(() => ChatFits("돌린 뒤 가로 + 키보드"), "키보드 띄운 채 가로로: 왼쪽 열 · 입력칸이 키보드 위");
            Do(KeyboardDown);
            Sleep(2.2f);
            Shot("land_chat_again");
            Check(() => ChatFits("돌린 뒤 키보드 내림"), "키보드 띄운 채 돌린 뒤 내리면 가로 제자리");
            CloseAll();
            Do(() => Rotate(false));
            Wait(() => Turned(false), 8f, "세로로 되돌림");
        }

        // 화면이 다 돌았는지 — 크기를 바꾸는 동안 게임 보기가 잠깐 창 크기(879x693 같은)로 그려져 그 순간을 재면 틀린다
        private static bool Turned(bool land) => R0926Orientation.IsLandscape == land
            && (land ? Screen.width > Screen.height * 1.5f : Screen.height > Screen.width * 1.5f);

        private static MobileKeyboardHandler Mkh() => Find("ChatRoomPanel")?.GetComponent<MobileKeyboardHandler>();

        private static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);   // 오버레이 캔버스 — 월드 좌표가 곧 화면 좌표
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        // 가로에서 창이 오른쪽 도크 막대 왼쪽에 머무는지
        private static bool LeftOfRail(string panel)
        {
            var bg = Find(panel + "/Clip0926/Background") as RectTransform;
            var rail = Find(panel + "/DockMirror0926") as RectTransform;
            if (bg == null || rail == null) return false;
            Rect b = ScreenRect(bg), r = ScreenRect(rail);
            log.Add($"  {panel}: 창 {b.xMin:0}~{b.xMax:0} · 도크 막대 {r.xMin:0}~");
            return b.xMax <= r.xMin + 1f;
        }

        // 대화방 — 제목줄 아래 · 입력칸 위에 말풍선 칸이 들어가고, 키보드가 떠 있으면 입력칸이 키보드 위에 (화면 좌표)
        private static bool ChatFits(string when)
        {
            var k = Mkh();
            var header = Find("ChatRoomPanel/Clip0926/Background/Header") as RectTransform;
            if (k == null || header == null || k.viewportRect == null || k.inputAreaRect == null || k.backgroundRect == null) { log.Add("  대화방 칸 없음"); return false; }
            Rect h = ScreenRect(header), v = ScreenRect(k.viewportRect), i = ScreenRect(k.inputAreaRect), b = ScreenRect(k.backgroundRect);
            float kb = k.editorKeyboardPreviewObj != null ? ScreenRect((RectTransform)k.editorKeyboardPreviewObj.transform).yMax : 0f;
            bool land = Screen.width > Screen.height;
            var rail = Find("ChatRoomPanel/DockMirror0926") as RectTransform;
            float railX = land && rail != null ? ScreenRect(rail).xMin : Screen.width;
            log.Add($"  [{when}] 화면 {Screen.width}x{Screen.height} · 창 x {b.xMin:0}~{b.xMax:0} y {b.yMin:0}~{b.yMax:0} · 제목줄 {h.yMin:0}~{h.yMax:0} · 말풍선 칸 {v.yMin:0}~{v.yMax:0} · 입력칸 {i.yMin:0}~{i.yMax:0}"
                + (kb > 0f ? $" · 키보드 위 {kb:0}" : "") + (land ? $" · 도크 막대 {railX:0}~" : ""));
            foreach (var rt in new[] { k.backgroundRect, k.scrollViewRect, k.viewportRect, k.inputAreaRect })
                if (rt != null) log.Add($"    {rt.name}: a {rt.anchorMin}~{rt.anchorMax} off {rt.offsetMin}~{rt.offsetMax} 크기 {rt.rect.size} ← {rt.parent?.name}");
            return v.yMax <= h.yMin + 1f && v.yMin >= i.yMax - 1f && v.height >= Screen.height * 0.08f
                && h.yMax <= Screen.height + 1f && i.yMin >= kb - 1f && (!land || b.xMin >= -1f && b.xMax <= railX + 1f);   // 세로는 키보드가 뜨면 일부러 양옆 끝까지 넓힌다
        }

        // 기기에서 키보드를 내리면 입력칸 포커스도 풀린다 — 에디터 가상 키보드는 포커스를 남겨 두므로 같이 푼다
        private static void KeyboardDown()
        {
            var k = Mkh();
            if (k == null) return;
            k.EditorResetPreview();
            if (k.targetInputField != null) k.targetInputField.DeactivateInputField();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }

        // 자리가 잡힌 뒤 0.6초 동안 창이 위아래로 움직이지 않는지
        private static void ChatSteady(string when)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            double until = -1;
            steps.Enqueue(() =>
            {
                if (until < 0) until = EditorApplication.timeSinceStartup + 0.6;
                var k = Mkh();
                if (k != null && k.backgroundRect != null) { float y = ScreenRect(k.backgroundRect).yMin; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
                return EditorApplication.timeSinceStartup >= until;
            });
            Check(() => { log.Add($"  [{when}] 창 아래 끝 흔들림 {hi - lo:0.0}px"); return hi - lo < 2f; }, when + ": 위아래로 들썩이지 않음");
        }

        // 게임 화면 가로 ↔ 세로 — 기기 시뮬레이터는 크기를 바꿀 수 없어 그동안만 Game 보기로. 원래 보기·크기는 시험이 끝날 때(중간에 끊겨도) 되돌린다
        private static int savedGameViewSize = -1;
        private static PlayModeWindow.PlayModeViewTypes? savedViewType;
        private static void Rotate(bool land)
        {
            PlayModeWindow.GetRenderingResolution(out uint w, out uint h);
            if (savedViewType == null)
            {
                savedViewType = PlayModeWindow.GetViewType();
                if (savedViewType != PlayModeWindow.PlayModeViewTypes.GameView) PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
                savedGameViewSize = GameViewSize(-1);
            }
            uint lo = Math.Min(w, h), hi = Math.Max(w, h);
            PlayModeWindow.SetCustomRenderingResolution(land ? hi : lo, land ? lo : hi, land ? "playtest landscape" : "playtest portrait");
            log.Add($"  보기 {PlayModeWindow.GetViewType()} · {w}x{h} → {(land ? hi : lo)}x{(land ? lo : hi)}");
        }
        private static void RestoreGameViewSize()
        {
            if (savedViewType == null) return;
            if (savedGameViewSize >= 0) GameViewSize(savedGameViewSize);
            if (savedViewType != PlayModeWindow.PlayModeViewTypes.GameView) PlayModeWindow.SetViewType(savedViewType.Value);
            savedGameViewSize = -1;
            savedViewType = null;
        }
        // 기기 시뮬레이터를 '주 플레이 화면'으로 — 주 화면은 마지막으로 손이 간(SetFocus) 창이고, 그때 그 창 크기가 UI 바탕 크기(SetMainPlayModeViewSize)가 된다.
        // 가로 시험이 보기를 Game 으로 바꿨다 되돌리면 Unity 가 뒤에 있어 시뮬레이터에 손이 가지 않아, 숨은 Game 탭 크기(877x646)가 남았다 (10-19).
        // 주 화면이 이미 시뮬레이터로 적혀 있어도 크기는 옛것일 수 있다 — 늘 SetFocus(true) 를 부른다 (창을 앞으로 가져오지는 않는다)
        private static string mainViewNote = "";
        private static double screenFixUntil;
        private static void MainViewToSimulator()
        {
            mainViewNote = "";
            if (PlayModeWindow.GetViewType() != PlayModeWindow.PlayModeViewTypes.SimulatorView) return;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var pmv = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
            if (pmv == null) { mainViewNote = "PlayModeView 없음"; return; }
            UnityEngine.Object sim = null;
            foreach (var v in Resources.FindObjectsOfTypeAll(pmv)) if (v.GetType().Name == "SimulatorWindow") { sim = v; break; }
            if (sim == null) { mainViewNote = "시뮬레이터 창 없음"; return; }
            var setFocus = pmv.GetMethod("SetFocus", F, null, new[] { typeof(bool) }, null);
            if (setFocus == null) { mainViewNote = "SetFocus 없음"; return; }
            try { setFocus.Invoke(sim, new object[] { true }); }
            catch (Exception e) { mainViewNote = "맞추기 실패 " + (e.InnerException ?? e).Message; return; }
            var size = pmv.GetProperty("targetSize", F)?.GetValue(sim);
            mainViewNote = "시뮬레이터로 맞춤" + (size is Vector2 v2 ? $" {v2.x:0}x{v2.y:0}" : "");
        }

        private static bool CanvasMatchesScreen(out string note)
        {
            note = "";
            var root = Find("Dock0926")?.GetComponentInParent<Canvas>()?.rootCanvas;
            if (root == null) return true;
            var px = root.pixelRect;
            note = $"화면 {Screen.width}x{Screen.height} · UI 바탕 {px.width:0}x{px.height:0}";
            return Mathf.Abs(px.width - Screen.width) < 2f && Mathf.Abs(px.height - Screen.height) < 2f;
        }

        // 게임 창에서 고른 크기 칸 — set < 0 이면 읽기만
        private static int GameViewSize(int set)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var t = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var views = t != null ? Resources.FindObjectsOfTypeAll(t) : new UnityEngine.Object[0];
            if (views.Length == 0) return -1;
            if (set >= 0) { t.GetMethod("SizeSelectionCallback", F)?.Invoke(views[0], new object[] { set, null }); return set; }
            return t.GetProperty("selectedSizeIndex", F)?.GetValue(views[0]) is int i ? i : -1;
        }

        // 보물찾기 카드 — 시험 서버(127.0.0.1:5099 · X-Test-User, scratchpad hunt_dev_server.py)로:
        // 추가 창 세 번째 카드 → 처음 주최는 수칙 동의 → 숨기기 → 1시간·1일 연장 → 다른 사람으로 참가 → 근접 → AR 상자 두 번 톡톡 → 축하 화면
        private const string HuntCardPath = "UploadPage/Clip0926/UploadSheet0926/HuntUploadPage/Body0926/";
        private const string HuntCardToast = "UploadPage/Clip0926/UploadSheet0926/HuntUploadPage/Toast0926/Text";   // 카드 아래 알약 (Body0926 밖)
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
            Check(() => Find(H + "Detail/Viewport/Content/Open") == null && Active(H + "Detail/Viewport/Content/ExtendRow/Extend") && Active(H + "Detail/Viewport/Content/ExtendRow/ExtendDay"),
                "주최자 상세: '보물 열기' 버튼 없음 · '1시간 연장하기 / 1일 연장하기' 한 줄");
            Check(() => (Find(H + "Detail/Viewport/Content/Info")?.GetComponent<Text>()?.text ?? "").Contains("참가  0/30명"), "상세: 참가 0/30명 (인원 기본 30)");
            Check(() =>
            {
                var a = Find(H + "Detail/Viewport/Content/ExtendRow/Extend") as RectTransform;
                var b = Find(H + "Detail/Viewport/Content/ExtendRow/ExtendDay") as RectTransform;
                return a != null && b != null && Mathf.Abs(a.position.y - b.position.y) < 1f && Mathf.Abs(a.rect.width - b.rect.width) < 1f;
            }, "연장 두 버튼은 같은 줄 · 같은 폭");
            // 누르는 즉시 남은 시간이 바뀐다 (서버 답을 기다리지 않음) — 60분 → 2시간 → 26시간
            Do(() => log.Add("  연장 전: " + TextOf(H + "Detail/Viewport/Content/Status")));
            Do(() => Click(H + "Detail/Viewport/Content/ExtendRow/Extend"));
            Check(() => TextOf(H + "Detail/Viewport/Content/Status") == "2시간 남음", "'1시간 연장하기' → 누르자마자 '2시간 남음'");
            Sleep(1.5f);
            Shot("hunt_extend");
            Check(() => TextOf(H + "Detail/Viewport/Content/Status") == "2시간 남음" && TextOf(HuntCardToast) == "1시간 늘렸어요" && FeedHas("1시간 늘었어요"),
                "서버 저장 뒤에도 2시간 · '1시간 늘렸어요' · 소식 줄");
            Do(() => Click(H + "Detail/Viewport/Content/ExtendRow/ExtendDay"));
            Check(() => TextOf(H + "Detail/Viewport/Content/Status") == "26시간 남음", "'1일 연장하기' → 누르자마자 '26시간 남음'");
            Sleep(1.5f);
            Check(() => TextOf(H + "Detail/Viewport/Content/Status") == "26시간 남음" && TextOf(HuntCardToast) == "1일 늘렸어요", "서버 저장 뒤에도 26시간 · '1일 늘렸어요'");
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
            Check(() => !TextOf(H + "Detail/Viewport/Content/Near").Contains("두 번 톡톡") && R0926HuntCard.EditorBuzzes == 0, "250m 밖에서 참가 → 아직 안 보임 · 진동 없음(첫 확인은 기준만)");
            Do(() => VirtualLocation.Instance.SetCoordinates(huntSpot.x - 0.00009f, huntSpot.y));   // 10m 남쪽 — 상자를 화면에서 누를 수 있게
            Sleep(9.5f);
            Shot("hunt_near");
            Check(() => (Find(H + "Detail/Viewport/Content/Info")?.GetComponent<Text>()?.text ?? "").Contains("참가  1/30명"), "참가 → 상세 1/30명");
            Check(() => TextOf(H + "Detail/Viewport/Content/Near").Contains("두 번 톡톡"), "보이는 거리 안 → '보물상자를 두 번 톡톡 눌러 열어요'");
            Check(() => R0926HuntCard.EditorBuzzes == 1, "보물 자리로 다가감 → 진동 한 번");
            OpenTreasureAR("hunt_celebrate");
            Shot("hunt_found");
            Do(() => log.Add("  내 상태: " + (Find(H + "Detail/Viewport/Content/Mine")?.GetComponent<Text>()?.text ?? "-")));
            Check(() => (Find(H + "Detail/Viewport/Content/Mine")?.GetComponent<Text>()?.text ?? "").Contains("찾았어요"), "두 번 톡톡 → 축하 → 확인 → 상세 '찾았어요'");
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
            Check(() => Find(S + "GroupChannels/RowLive") == null && Find(S + "GroupChannels/RowAds") == null && Active(S + "GroupChannels/RowEvents"),
                "설정에 '라이브 방송' · '광고 · 제휴 소식' 줄 없음 (H1) — 날씨·이벤트 줄은 그대로");
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

        // 겹침 (10-13 대표님 C11·C16·C17) — 창이 열린 채 다른 칸·보물찾기로 가도 앞 창이 남아 겹치지 않게.
        // ① 도크 칸 바꾸기: 목록·추가·메세지·프로필 중 하나가 열린 채 다른 칸을 누르면 앞 창은 닫힌다
        // ② 메세지창 → 대화창의 보물찾기 말풍선(첫 줄 '[보물찾기 · 제목]')을 한 번 누르면 → 대화창·메세지창을 닫고 그 게임 상세 · 두 번 누르면 좋아요만
        // ③ 내 프로필 → 팔로잉 목록 → 사람 → 그 프로필의 보물찾기 줄 → 상세 (프로필이 닫히며 팔로우 목록이 되살아나 상세 위에 겹쳤다)
        private const string OvlHost = "hunt_test_ohost", OvlMe = "hunt_test_ome", OvlView = "hunt_test_oview", OvlTitle = "겹침 시험 보물";
        private static int ovlHunt;
        private static string ovlSlot;
        private static MessagePanelManager Mpm() => UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include);
        private static Text ovlHuntText, ovlMineText, ovlPlainText;
        private static DoubleTouchLikeHandler ovlTap;
        private static bool ovlLiked;
        private static void PlanOverlap()
        {
            const string H = HuntCardPath;
            Do(() =>
            {
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = OvlMe;
                R0926HuntProfile.EditorHostAlias = OvlHost;
                ovlHunt = DevCreateHunt(OvlHost, OvlTitle, "36.659300");
                DevPost(OvlMe, "/api/hunts/" + ovlHunt + "/join", "{\"lat\":36.659300,\"lon\":126.673000}");
                log.Add("  시험 게임 " + ovlHunt);
            });
            Check(() => ovlHunt > 0 && Has(DevGet(OvlMe, "/api/hunts/lookup?title=" + Uri.EscapeDataString(OvlTitle) + "&with=" + OvlHost), "\"hunt_id\":" + ovlHunt),
                "서버: 대화 상대(주최자)와 함께한 그 제목의 게임 번호 (lookup)");
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);

            // ① 창이 열려 있으면 도크의 다른 칸은 창에 가려 누를 수 없다 — 칸을 바꿔 두 창이 겹쳐 열릴 길이 없는지.
            //    그리고 뒤로가기만으로 다 닫히는지 (메시지 목록이 X 없이 다시 켜져 닫히지 않고 상세 위에 남았다)
            var opens = new (string name, Action open)[]
            {
                ("ListPanel", () => Tap("Dock0926/List_Button")),
                ("UploadPage", () => Tap("Dock0926/PlusButton")),
                ("MessagePanel", () => { var m = Mpm(); if (m != null && m.messagePanel != null) m.messagePanel.SetActive(true); }),
                ("ChatRoomPanel", () => Mpm()?.OpenChatRoom(OvlHost, "보물 주최자", null, false)),
                ("FullProfilePanel", () => ProfileManager.Instance?.ShowProfile("3")),
                ("FollowPanel", () => FollowManager.Instance?.ShowFollowing("3", "WOOPANG")),
            };
            string[] slots = { "List_Button", "PlusButton", "Message_Button", "MiniProfile" };
            foreach (var (name, open) in opens)
            {
                string pn = name;
                var op = open;
                Do(op);
                Sleep(1.4f);
                Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
                Sleep(0.3f);
                Do(() =>
                {
                    var s = new List<string>();
                    ovlSlot = null;
                    foreach (var sl in slots)
                    {
                        string hit = TopHit("Dock0926/" + sl);
                        s.Add(sl + "=" + hit);
                        if (Active("Dock0926/" + sl) && hit.EndsWith("(그 칸)")) ovlSlot = sl;
                    }
                    log.Add("  " + pn + " 열림 (" + OpenPanels() + (Active("UploadPage") ? ",UploadPage" : "") + ") · 도크: " + string.Join(" · ", s));
                });
                Shot("ovl_dock_" + pn);
                Check(() => ovlSlot == null, pn + " 이 열리면 도크의 다른 칸은 창에 가려 눌리지 않음 (두 창이 겹쳐 열리지 않음)");
                CloseAll();
                Check(() => OpenCount() == 0, pn + ": 뒤로가기만으로 모두 닫힘");
            }

            // ② 메세지창 → 대화창 → 보물찾기 말풍선
            Do(() =>
            {
                var mpm = UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include);
                if (mpm != null && mpm.messagePanel != null) mpm.messagePanel.SetActive(true);
            });
            Sleep(0.8f);
            Do(() => UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include)?.OpenChatRoom(OvlHost, "보물 주최자", null, false));
            Sleep(1.6f);
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Sleep(0.5f);
            Do(FillHuntChat);
            Sleep(0.8f);
            Shot("ovl_chat");
            Check(() => ovlHuntText != null && ovlHuntText.text.StartsWith("<size=40><b><color=#FFCF5C>보물찾기 · " + OvlTitle + "  ›</color></b></size>\n") && ovlHuntText.text.EndsWith("근처 정자 쪽에 숨겼어요"),
                "보물찾기 말풍선(상대): 첫 줄이 금색 '보물찾기 · 제목 ›' · 아래 본문");
            Check(() => ovlMineText != null && ovlMineText.text.StartsWith("<size=40><b><color=#FFE7A3>보물찾기 · ") && ovlMineText.text.EndsWith("참가했어요!"), "보물찾기 말풍선(내 말): 분홍 위에 옅은 금색 머리");
            Check(() => ovlPlainText != null && ovlPlainText.text == "안녕하세요", "보통 말풍선은 그대로");
            Check(() => BubbleFits(ovlHuntText), "보물찾기 말풍선 높이가 머리+본문을 다 담음");
            Do(() =>
            {
                // 두 번 누름 — 좋아요만 (실제 서버 대신 여기서 센다) · 상세로 가지 않는다
                typeof(DoubleTouchLikeHandler).GetField("lastTapTime", NPI)?.SetValue(ovlTap, Time.time - 0.15f);
                ovlTap.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            });
            Sleep(1.0f);
            Check(() => ovlLiked && Active("ChatRoomPanel") && !Active("UploadPage"), "보물찾기 말풍선 두 번 누름: 좋아요만 · 대화창 그대로");
            Do(() => ovlTap.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)));
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(OvlTitle), 12f, "말풍선 한 번 누름 → 상세");
            Sleep(1.0f);
            Shot("ovl_chat_to_hunt");
            Check(() => !Active("ChatRoomPanel") && !Active("MessagePanel") && Active("UploadPage") && UploadSwipe()?.GetCurrentPanel() == 2 && DetailTitle().Contains(OvlTitle),
                "보물찾기 말풍선 한 번 누름: 대화창·메세지창을 닫고 추가 창 보물찾기 카드의 그 게임 상세");
            CloseAll();

            // ③ 팔로잉 목록 → 사람 → 그 프로필의 보물찾기 줄
            const string P = "FullProfilePanel/Content/BottomPanel/";
            const string C = P + "HuntSection0926/Scroll/Viewport/Content/";
            Do(() => FollowManager.Instance?.ShowFollowing("3", "WOOPANG"));
            Sleep(1.2f);
            Check(() => Active("FollowPanel"), "팔로잉 목록 열림");
            Do(() => typeof(FollowManager).GetMethod("OnItemClicked", NPI)?.Invoke(FollowManager.Instance, new object[] { "3", "WOOPANG" }));
            Wait(() => Active(P + "FollowStats/HuntStat0926"), 12f, "목록의 사람 → 프로필");
            Check(() => !Active("FollowPanel") && ProfileManager.openedFromFollowPanel, "프로필을 열면 목록은 숨고 '목록에서 왔음' 표시");
            Do(() => Click(P + "FollowStats/HuntStat0926"));
            Sleep(1.5f);
            Do(() => FirstActiveChild(C + "OpenList")?.GetComponent<Button>()?.onClick.Invoke());
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(OvlTitle), 12f, "프로필의 게임 줄 → 상세");
            Sleep(1.5f);
            Shot("ovl_follow_to_hunt");
            Check(() => !Active("FollowPanel") && !Active("FullProfilePanel") && Active("UploadPage") && DetailTitle().Contains(OvlTitle),
                "팔로잉 목록 → 프로필 → 게임 줄: 상세 위에 팔로우 목록·프로필이 남지 않음");
            CloseAll();

            // ④ 참여만 한 사람의 프로필 — 숫자 줄 '참여' 칸 · 펼치면 '참여' 줄 · 누르면 그 게임 상세 (10-13 대표님: 내 프로필에도, 남이 봐도)
            const string Col = P + "FollowStats/HuntStat0926";
            Check(() => Has(DevGet(OvlView, "/api/hunts/profile/" + OvlMe), "\"joined\":[{", "\"id\":" + ovlHunt + ","), "서버: 참여 중인 게임이 남이 본 프로필에도 (joined)");
            foreach (var (viewer, head, who) in new[] { (OvlMe, "내가 참여 중", "내 프로필"), (OvlView, "참여 중", "남이 본 프로필") })
            {
                string v = viewer, hd = head, w = who;
                Do(() => { R0926HuntApi.EditorTestUser = v; R0926HuntProfile.EditorHostAlias = OvlMe; ProfileManager.Instance?.ShowProfile("3"); });
                Wait(() => Active(Col) && TextOf(Col + "/Label") == "참여", 12f, w + " → '참여' 칸");
                Sleep(0.8f);
                Check(() => TextOf(Col + "/Main").Contains("<b>1</b>") && TextOf(Col + "/Main").Contains("진행 중"), w + ": 숫자 줄 '참여' 칸 1 · 진행 중");
                Do(() => Click(Col));
                Sleep(1.2f);
                Shot("ovl_joined_" + (v == OvlMe ? "mine" : "other"));
                Check(() => TextOf(C + "Head/Right") == hd && ActiveChildren(C + "OpenList") == 1
                            && (FirstActiveChild(C + "OpenList")?.Find("Title")?.GetComponent<Text>()?.text ?? "").StartsWith("<color=#3FC1B0>참여</color>  " + OvlTitle)
                            && (FirstActiveChild(C + "OpenList")?.Find("Sub")?.GetComponent<Text>()?.text ?? "").StartsWith("진행 중")
                            && !Active(C + "RecordBox") && !Active(C + "RateBox"),
                    w + ": 펼치면 머리 '" + hd + "' · '참여' 줄 하나(진행 중) · 주최 기록·평가 칸은 없음");
                Do(() => FirstActiveChild(C + "OpenList")?.GetComponent<Button>()?.onClick.Invoke());
                Wait(() => Active(H + "Detail") && DetailTitle().Contains(OvlTitle), 12f, w + "의 '참여' 줄 → 상세");
                Sleep(1.0f);
                Check(() => !Active("FullProfilePanel") && Active("UploadPage") && DetailTitle().Contains(OvlTitle), w + ": '참여' 줄을 누르면 프로필을 닫고 그 게임 상세");
                CloseAll();
            }
            // 서로 차단한 사이엔 참여 중인 게임으로 있는 동네를 짐작하지 못하게
            Do(() => DevPost(OvlMe, "/api/block", "{\"blocked_id\":\"" + OvlView + "\"}"));
            Check(() => Has(DevGet(OvlView, "/api/hunts/profile/" + OvlMe), "\"joined\":[]") && Has(DevGet(OvlMe, "/api/hunts/profile/" + OvlMe), "\"id\":" + ovlHunt + ","),
                "서버: 나를 차단한 사람의 참여 중인 게임은 안 보임 (본인에게는 그대로)");
            Do(() => DevPost(OvlMe, "/api/unblock", "{\"blocked_id\":\"" + OvlView + "\"}"));
            Do(() => { R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; R0926HuntProfile.EditorHostAlias = null; });
        }

        // 손으로 누른 것과 같게 — onClick 만이 아니라 그 오브젝트의 누름 처리기 모두
        private static void Tap(string path)
        {
            var t = Find(path);
            if (t == null) { log.Add("  누를 것 없음 " + path); return; }
            var es = UnityEngine.EventSystems.EventSystem.current;
            UnityEngine.EventSystems.ExecuteEvents.Execute(t.gameObject, new UnityEngine.EventSystems.PointerEventData(es) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left },
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }

        // 그 칸 자리를 누르면 맨 위에 맞는 것 — 열린 창이 도크의 다른 칸을 가리는지
        private static string TopHit(string path) => TopHitAt(Find(path) as RectTransform);
        private static string TopHitAt(RectTransform rt)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (rt == null || es == null) return "-";
            var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center)) }, hits);
            return hits.Count == 0 ? "없음" : hits[0].gameObject.name + (hits[0].gameObject.transform.IsChildOf(rt) ? " (그 칸)" : " (가림)");
        }

        // 맨 위에 맞는 것이 그 칸인지(covered 면 다른 것인지) — 가끔 아무것도 안 맞는 프레임이 있어 맞을 때까지 3초 안에서 기다렸다 본다
        private static void HitCheck(Func<RectTransform> rt, bool covered, string what)
        {
            double until = -1;
            int misses = 0;
            string missAt = "";
            steps.Enqueue(() =>
            {
                if (until < 0) until = EditorApplication.timeSinceStartup + 3;
                string hit = TopHitAt(rt());
                if (hit == "없음" && EditorApplication.timeSinceStartup < until) { misses++; missAt = $"{Screen.width}x{Screen.height}"; return false; }
                bool ok = hit != "없음" && hit != "-" && hit.EndsWith("(그 칸)") != covered;
                log.Add((ok ? "PASS " : "FAIL ") + what + (ok ? "" : " — 맨 위: " + hit) + (misses > 0 ? $" (빈 맞힘 {misses}번 · 그때 화면 {missAt})" : ""));
                return true;
            });
        }

        private static int OpenCount()
        {
            int n = Active("UploadPage") ? 1 : 0;
            foreach (var p in new[] { "ListPanel", "MessagePanel", "ChatRoomPanel", "FullProfilePanel", "LoginPromptPanel", "FollowPanel" })
                if (Active(p)) n++;
            return n;
        }

        // 뒤로가기를 열린 창이 없어질 때까지 (많아야 여섯 번)
        private static void CloseAll()
        {
            for (int i = 0; i < 6; i++)
            {
                Do(() => { if (OpenCount() > 0) BackNow(); });
                Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
                Sleep(0.7f);
            }
            Do(() => { if (OpenCount() > 0) log.Add("  남은 창: " + OpenPanels() + (Active("UploadPage") ? ",UploadPage" : "")); });
        }

        private static void FillHuntChat()
        {
            var mpm = UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include);
            if (mpm == null || mpm.chatMessageContent == null) { log.Add("FAIL 대화창 없음"); return; }
            mpm.StopAllCoroutines();
            for (int i = mpm.chatMessageContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(mpm.chatMessageContent.GetChild(i).gameObject);
            var bubble = typeof(MessagePanelManager).GetMethod("CreateMessageBubble", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            DMMessage M(int id, bool mine, string text) => new DMMessage
            {
                id = id, sender_id = mine ? OvlMe : OvlHost, content = text, is_read = true, is_mine = mine,
                created_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss") + "Z", sender_username = mine ? "나" : "보물 주최자",
            };
            bubble?.Invoke(mpm, new object[] { M(9101, false, "안녕하세요"), false });
            bubble?.Invoke(mpm, new object[] { M(9102, true, "[보물찾기 · " + OvlTitle + "]\n참가했어요!"), true });
            bubble?.Invoke(mpm, new object[] { M(9103, false, "[보물찾기 · " + OvlTitle + "]\n근처 정자 쪽에 숨겼어요"), false });
            Text Body(int k) => mpm.chatMessageContent.childCount > k ? mpm.chatMessageContent.GetChild(k).Find("BubbleContainer/ContentText")?.GetComponent<Text>() : null;
            ovlPlainText = Body(0);
            ovlMineText = Body(1);
            ovlHuntText = Body(2);
            ovlTap = mpm.chatMessageContent.childCount > 2 ? mpm.chatMessageContent.GetChild(2).GetComponent<DoubleTouchLikeHandler>() : null;
            ovlLiked = false;
            ovlTap?.Initialize(9103, (RectTransform)mpm.chatMessageContent.GetChild(2), (id, rt) => ovlLiked = true);
        }

        // 말풍선 높이 ≥ 글 높이 + 위아래 여백 (BubbleAutoSizer 가 머리 줄까지 셈)
        private static bool BubbleFits(Text t)
        {
            if (t == null) return false;
            var box = t.transform.parent as RectTransform;
            var vlg = box != null ? box.GetComponent<VerticalLayoutGroup>() : null;
            float need = t.preferredHeight + (vlg != null ? vlg.padding.vertical : 0);
            log.Add("  보물찾기 말풍선 높이 " + (box != null ? box.rect.height.ToString("0") : "-") + " · 글 " + t.preferredHeight.ToString("0"));
            return box != null && box.rect.height + 1f >= need;
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

        // AR 보물 — 참가한 게임의 보물에 '보이는 거리'(50m)까지 다가가면 그 자리에 보물상자 · 두 번 톡톡이면 열고 찾기.
        // 개발 서버의 두 게임(누르기 T1 · 코드 T2)에 시험 아이디로 참가해 두고, 에디터 위치(VirtualLocation)를 옮기며 걷는다
        private const string TreasurePref = "r0926_hunt_treasure";
        private const string TrPlayer = "hunt_test_tplayer";
        private const System.Reflection.BindingFlags NPI = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        private static int trHunt1, trHunt2;
        private static Vector2 trBase;
        private static Vector3 trChestAt;
        private static float trChestYaw;
        private static bool treasureRun;
        private static void PlanHuntTreasure()
        {
            const string H = HuntCardPath;
            const string T1Title = "AR 보물 시험 1", T2Title = "AR 보물 시험 2 (코드)", Question = "문 앞 번호 네 자리는?", Prize = "커피 쿠폰 한 장";
            const string C = "HuntCelebrate0926/Panel/Content/Info/";
            Do(() =>
            {
                treasureRun = true;
                PlayerPrefs.DeleteKey(TreasurePref);
                typeof(R0926HuntTreasure).GetField("loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, false);
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = TrPlayer;
                trBase = new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
                // T1 은 출발점 33m 북쪽(누르기) · T2 는 그 동쪽 98m(코드)
                trHunt1 = DevCreateHuntAt("hunt_test_thost", T1Title, trBase.x + 0.0003f, trBase.y, null, null, Prize);
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
                return t != null && r != null && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("HuntChestGold")
                       && t.PlaceName == T1Title && t.placeId == "hunt_" + trHunt1 && Mathf.Abs(t.TargetColor.b - 0.31f) < 0.01f && t.OnIndicatorTapped != null && t.Sparkle;
            }, "보물상자(금빛 재질) · 이름 · 금색 박스·화살표(별빛) · 박스를 눌러도 찾기");
            Check(() =>
            {
                var go = Treasure(trHunt1);
                var chest = go != null ? go.GetComponentInChildren<R0926HuntChest>() : null;
                var tw = go != null ? go.transform.Find("Twinkle")?.GetComponent<ParticleSystem>() : null;
                if (chest == null || tw == null) { log.Add("  상자 " + (chest != null) + " · 별빛 " + (tw != null)); return false; }
                trChestAt = chest.transform.position;
                trChestYaw = chest.transform.eulerAngles.y;
                return go.GetComponentInChildren<GoldenGlowCube>(true) == null && chest.transform.Find("Lid") != null && chest.LidAngle < 1f && tw.isPlaying;
            }, "보물상자 — 뚜껑 닫힘 · 둘레에 별빛 · 예전 금빛 큐브(GoldenGlowCube)는 없음");
            Sleep(0.6f);
            Check(() =>
            {
                var chest = Treasure(trHunt1)?.GetComponentInChildren<R0926HuntChest>();
                if (chest == null) return false;
                float turned = Mathf.Abs(Mathf.DeltaAngle(trChestYaw, chest.transform.eulerAngles.y));
                log.Add("  0.6초에 " + turned.ToString("0.0") + "° 돎 · 높이 차 " + Mathf.Abs(chest.transform.position.y - trChestAt.y).ToString("0.000") + "m");
                return turned > 5f && (chest.transform.position - trChestAt).sqrMagnitude < 1e-6f;
            }, "보물은 제자리에서 천천히 돎 — 위아래로 떠다니지 않음");
            Check(() => Active("HuntToast0926") && TextOf("HuntToast0926/Pill/Title") == "보물이 근처에 있어요!" && TextOf("HuntToast0926/Pill/Sub") == "보물상자를 두 번 톡톡 눌러 열어 보세요",
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

            // 한 번만 누르면 여는 법 안내 · 45m 에서 두 번 톡톡 → '조금만 더 가까이' (판정 거리 30m)
            TapTreasure(() => trHunt1);
            Wait(() => TextOf("HuntToast0926/Pill/Title") == "두 번 톡톡 누르면 보물이 열려요", 4f, "한 번 누름 → 여는 법");
            Check(() => TextOf("HuntToast0926/Pill/Title") == "두 번 톡톡 누르면 보물이 열려요" && R0926HuntTreasure.Tracking(trHunt1) && Treasure(trHunt1) != null,
                "한 번만 누르면 '두 번 톡톡 누르면 보물이 열려요' · 열리지 않음");
            Sleep(0.6f);
            DoubleTapTreasure(() => trHunt1);
            Wait(() => TextOf("HuntToast0926/Pill/Title") == "조금만 더 가까이", 6f, "45m 두 번 톡톡 → 조금만 더");
            Sleep(0.4f);
            Shot("treasure_toofar");
            Do(() => log.Add("  알림: " + TextOf("HuntToast0926/Pill/Title") + " / " + TextOf("HuntToast0926/Pill/Sub")));
            Check(() => TextOf("HuntToast0926/Pill/Title") == "조금만 더 가까이" && TextOf("HuntToast0926/Pill/Sub").EndsWith("m 더 가 보세요")
                        && R0926HuntTreasure.Tracking(trHunt1) && Treasure(trHunt1) != null,
                "45m 에서 상자를 누르면 '조금만 더 가까이 · Nm 더 가 보세요' · 보물은 그대로");

            // 12m — 두 번 톡톡 → 찾음 → 상자가 이쪽을 보고 열린 뒤 사라지고 · 축하 화면 → 그 게임 상세
            Do(() => VirtualLocation.Instance.SetCoordinates(trBase.x + 0.0003f - 0.000108f, trBase.y));
            Sleep(0.6f);
            Do(() => FaceTreasure(trHunt1));
            Sleep(0.3f);
            Shot("treasure_12m");
            DoubleTapTreasure(() => trHunt1);
            Wait(() => R0926HuntCelebrate.Showing, 8f, "12m 두 번 톡톡 → 축하 화면");
            Sleep(0.35f);
            Shot("treasure_open_ar");   // AR 상자가 이쪽을 보고 뚜껑이 열리는 중
            Check(() =>
            {
                var chest = UnityEngine.Object.FindAnyObjectByType<R0926HuntChest>();
                var t = chest != null ? chest.GetComponent<Target>() : null;
                if (chest == null) { log.Add("  여는 상자 없음"); return false; }
                log.Add("  뚜껑 " + chest.LidAngle.ToString("0") + "°");
                return chest.Opening && chest.LidAngle > 20f && t != null && !t.enabled;
            }, "찾음 → 상자가 이쪽을 보고 뚜껑이 열림 · 박스·화살표는 걷힘");
            Sleep(0.6f);
            Shot("treasure_open");   // 화면이 스르륵 — 열린 보물상자가 튀어나오는 중
            Sleep(1.4f);
            Shot("treasure_found");
            Do(() => log.Add("  축하: " + TextOf(C + "Title") + " / " + TextOf(C + "Rank") + " / " + TextOf(C + "PrizeCard/Prize") + " / " + TextOf(C + "Note")));
            Check(() => TextOf(C + "Title") == "보물을 찾았습니다!" && TextOf(C + "Rank") == "가장 먼저 찾았어요  ·  " + T1Title
                        && TextOf(C + "PrizeCard/Prize") == Prize && TextOf(C + "Note").Contains("님에게 찾았다는 메세지를 보냈어요"),
                "축하 화면: '보물을 찾았습니다!' · 가장 먼저 · 상품 안내 · 주최자에게 메세지 보냄");
            Check(() => !R0926HuntTreasure.Tracking(trHunt1) && !R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + trHunt1), "찾음 → 그만 지켜봄");
            Check(() => GameObject.Find("HuntTreasure_" + trHunt1) == null && PrefLine(trHunt1) == null, "상자는 열린 뒤 작아지며 사라짐 · 기기 기록에서도 지움");
            Do(() => Click(C + "OK"));
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(T1Title), 12f, "확인 → 그 게임 상세");
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
            Do(() => { var tap = Treasure(trHunt2)?.GetComponentInChildren<Target>(true)?.OnIndicatorTapped; tap?.Invoke(); tap?.Invoke(); });   // 두 번 톡톡
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
                bool host = R0926HuntTreasure.Tracking(990012) && PrefLine(990012) == "990012\t0\tAR\th";
                bool skip = !R0926HuntTreasure.Tracking(990013);
                R0926HuntTreasure.Untrack(990011);
                R0926HuntTreasure.Untrack(990012);
                return at && host && skip && !PlayerPrefs.HasKey(TreasurePref);
            }, "예약 게임은 시작 시각(UTC)을 적어 그때부터 · 주최자도 내 보물을 봄(참가는 못 함) · 이미 찾은 게임은 안 봄");
            Do(() => { R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; PlayerPrefs.DeleteKey(TreasurePref); treasureRun = false; });
        }

        // 보물상자 근접 촬영 — 서버 없이 프리팹을 눈앞에 두고: 가까이·중간·멀리 · 누름(살짝 커짐) · 달그락 · 열기(이쪽을 봄 → 뚜껑 · 빛기둥 · 별빛) · 축하 화면
        private static GameObject hcGo;
        private static R0926HuntChest hcChest;
        private static float hcRest, hcPeak, hcTilt, hcYaw;
        private static void PlanHuntChest()
        {
            const string C = "HuntCelebrate0926/Panel/Content/";
            Action<float> place = d => steps.Enqueue(() =>
            {
                var cam = Camera.main;
                if (hcGo == null || cam == null) return true;
                Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
                hcGo.transform.position = cam.transform.position + f * d + Vector3.down * 0.35f;
                return true;
            });
            // sec 초 동안 매 프레임 지켜본다 (찍는 두 프레임 사이에 정점을 놓치지 않게)
            Action<float, Action> watch = (sec, each) =>
            {
                double until = -1;
                steps.Enqueue(() =>
                {
                    if (until < 0) until = EditorApplication.timeSinceStartup + sec;
                    each();
                    return EditorApplication.timeSinceStartup >= until;
                });
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() =>
            {
                DeviceFov();
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";   // 축하 화면이 무엇을 부르더라도 개발 서버로만
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Redesign0926/Prefabs/HuntTreasure0926.prefab");
                hcGo = prefab != null ? UnityEngine.Object.Instantiate(prefab) : null;
                if (hcGo != null) hcGo.name = "HuntChestShot";
                hcChest = hcGo != null ? hcGo.GetComponentInChildren<R0926HuntChest>() : null;
                hcRest = hcChest != null ? hcChest.transform.localScale.x : 1f;
            });
            place(3.6f);
            Sleep(1.2f);
            Check(() =>
            {
                if (hcChest == null) { log.Add("  보물상자 프리팹 없음"); return false; }
                var mf = hcChest.GetComponent<MeshFilter>(); var mr = hcChest.GetComponent<MeshRenderer>();
                var lid = hcChest.transform.Find("Lid");
                var lmf = lid != null ? lid.GetComponent<MeshFilter>() : null;
                var mats = mr != null ? mr.sharedMaterials : new Material[0];
                log.Add("  몸통 " + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + " " + mf.sharedMesh.vertexCount : "없음") + " · 재질 " + string.Join(", ", System.Array.ConvertAll(mats, m => m != null ? m.name : "null")));
                return mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "HuntChestBody" && mats.Length == 3 && mats[0] != null && mats[0].name == "HuntChestGold_Mat"
                       && lmf != null && lmf.sharedMesh != null && lmf.sharedMesh.name == "HuntChestLid" && hcChest.LidAngle < 1f
                       && hcGo.GetComponentInChildren<GoldenGlowCube>(true) == null && hcChest.GetComponent<Target>() != null && hcChest.GetComponent<BoxCollider>() != null;
            }, "보물상자 프리팹 — 몸통·뚜껑(경첩) 모양 · 금빛·청동·금화 재질 · 뚜껑 닫힘 · 박스·화살표(Target) · 누르는 상자");
            Check(() =>
            {
                var tw = hcGo != null ? hcGo.transform.Find("Twinkle")?.GetComponent<ParticleSystem>() : null;
                if (tw == null) return false;
                log.Add("  별빛 " + tw.particleCount + "개");
                return tw.isPlaying && tw.particleCount > 0;
            }, "둘레에 흰 별빛이 피었다 사그라듦 (켜자마자 — 미리 돌려 둠)");
            Shot("chest_close");
            Do(() => hcYaw = hcChest != null ? hcChest.transform.eulerAngles.y : 0f);
            place(9f);
            Sleep(0.6f);
            Shot("chest_mid");
            place(20f);
            Sleep(0.6f);
            Shot("chest_far");
            Check(() => hcChest != null && Mathf.Abs(Mathf.DeltaAngle(hcYaw, hcChest.transform.eulerAngles.y)) > 10f, "제자리에서 천천히 돎");

            // 한 번 누름 — 살짝 커졌다 돌아온다 (예전 금빛 상자와 같은 1.15배 · 0.16초)
            place(3.6f);
            Sleep(0.5f);
            Do(() => { hcPeak = 0f; hcChest?.Nudge(); });
            watch(0.12f, () => { if (hcChest != null) hcPeak = Mathf.Max(hcPeak, hcChest.transform.localScale.x / hcRest); });
            Shot("chest_nudge");
            watch(0.5f, () => { if (hcChest != null) hcPeak = Mathf.Max(hcPeak, hcChest.transform.localScale.x / hcRest); });
            Check(() =>
            {
                float now = hcChest != null ? hcChest.transform.localScale.x / hcRest : 0f;
                log.Add("  누름: 가장 클 때 " + hcPeak.ToString("0.000") + "배 → 지금 " + now.ToString("0.000"));
                return hcPeak > 1.05f && hcPeak < 1.16f && Mathf.Abs(now - 1f) < 0.01f;
            }, "누르면 살짝 커졌다 돌아옴");

            // 서버에 묻는 동안 달그락
            Do(() => { hcTilt = 0f; hcChest?.Rattle(true); });
            watch(0.3f, () => { if (hcChest != null) hcTilt = Mathf.Max(hcTilt, Mathf.Abs(Mathf.DeltaAngle(0f, hcChest.transform.eulerAngles.z))); });
            Shot("chest_rattle");
            Do(() => hcChest?.Rattle(false));
            Sleep(0.2f);
            Check(() =>
            {
                float z = hcChest != null ? Mathf.Abs(Mathf.DeltaAngle(0f, hcChest.transform.eulerAngles.z)) : 99f;
                log.Add("  달그락: 가장 기울 때 " + hcTilt.ToString("0.0") + "° → 멈춘 뒤 " + z.ToString("0.0") + "°");
                return hcTilt > 1.5f && hcTilt < 4f && z < 0.01f;
            }, "묻는 동안 달그락 (±3.5°) · 멈추면 반듯");

            // 찾음 — 이쪽을 보고 뚜껑이 열리며 빛기둥·별빛 → 작아지며 사라짐 · 그 사이 축하 화면
            Do(() =>
            {
                hcChest?.Open();
                R0926HuntCelebrate.Show(0, "카페 앞 보물찾기", 1, "커피 쿠폰 한 장", "우팡");
            });
            Sleep(0.12f);
            Shot("chest_open_turn");
            Sleep(0.22f);
            Shot("chest_open_lid");
            Check(() =>
            {
                if (hcChest == null) return false;
                var t = hcChest.GetComponent<Target>();
                var cam = Camera.main.transform;
                Vector3 to = cam.position - hcChest.transform.position; to.y = 0f;
                float face = Vector3.Angle(-hcChest.transform.forward, to);   // 자물쇠(앞)가 -Z
                log.Add("  열기: 뚜껑 " + hcChest.LidAngle.ToString("0") + "° · 앞이 카메라에서 " + face.ToString("0") + "°");
                return hcChest.Opening && hcChest.LidAngle > 20f && face < 8f && t != null && !t.enabled;
            }, "열 때 앞(자물쇠)이 이쪽을 보고 뚜껑이 열림 · 박스·화살표는 걷힘");
            Sleep(0.3f);
            Shot("chest_open_beam");
            Check(() =>
            {
                bool beam = hcGo != null && hcGo.transform.Find("Beam") != null;
                bool stars = GameObject.Find("HuntChestFountain") != null;
                log.Add("  뚜껑 " + (hcChest != null ? hcChest.LidAngle.ToString("0") : "-") + "° · 빛기둥 " + beam + " · 솟는 별빛 " + stars);
                return hcChest != null && hcChest.LidAngle > 100f && beam && stars;
            }, "뚜껑이 활짝 (100° 넘게) · 빛기둥과 흰 별빛이 솟음");
            Sleep(0.45f);
            Shot("chest_modal_pop");
            Sleep(0.4f);
            Shot("chest_modal_lid");
            Check(() =>
            {
                var stage = Find(C + "Stage");
                int coins = 0, paper = 0;
                if (stage != null)
                    foreach (Transform c in stage)
                        if (c.name == "Paper" && c.gameObject.activeSelf) { paper++; if (c.GetComponent<Image>().sprite != null && c.GetComponent<Image>().sprite.name == "r0926_coin") coins++; }
                log.Add("  축하 화면 꽃가루 " + paper + "개 중 금화 " + coins);
                return Active(C + "Stage/ChestBody") && Active(C + "Stage/ChestLid") && !Active(C + "Stage/GiftBox") && paper > 20 && coins > 3;
            }, "축하 화면: 열린 보물상자 · 금화가 섞인 꽃가루");
            Sleep(0.9f);
            Shot("chest_modal_done");
            Check(() =>
            {
                var lid = Find(C + "Stage/ChestLid");
                var info = Find(C + "Info")?.GetComponent<CanvasGroup>();
                return hcGo == null && lid != null && Mathf.Abs(lid.localScale.y - 1f) < 0.01f && info != null && info.alpha > 0.99f
                       && TextOf(C + "Info/Title") == "보물을 찾았습니다!" && TextOf(C + "Info/PrizeCard/Prize") == "커피 쿠폰 한 장" && Active(C + "Info/OK");
            }, "AR 상자는 사라지고 · 축하 화면 다 열림 (뚜껑 · '보물을 찾았습니다!' · 상품 · 확인)");
            Do(() =>
            {
                var cel = UnityEngine.Object.FindAnyObjectByType<R0926HuntCelebrate>(FindObjectsInactive.Include);
                (typeof(R0926HuntCelebrate).GetField("panel", NPI)?.GetValue(cel) as GameObject)?.SetActive(false);   // '확인'은 그 게임 상세를 서버에서 연다 — 서버 없는 시험이라 닫기만
                R0926HuntApi.EditorBase = null;
            });
        }

        private static int DevCreateHuntAt(string host, string title, float lat, float lon, string code, string question, string prize = null)
        {
            DevPost(host, "/api/hunts/rules/accept", "{\"version\":1}");
            string mode = code == null ? "\"mode\":\"touch\"" : "\"mode\":\"code\",\"code\":\"" + code + "\",\"question\":\"" + question + "\"";
            if (prize != null) mode += ",\"prize\":\"" + prize + "\"";
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
        // 두 번 톡톡 — 0.45초 안에 같은 상자를 두 번 (한 단계 안에서 누르고 떼고를 두 번 — 단계 사이 시간이 들쭉날쭉해도 창을 넘지 않게)
        private static void DoubleTapTreasure(Func<int> id)
        {
            Do(() =>
            {
                var tr = UnityEngine.Object.FindAnyObjectByType<R0926HuntTreasure>();
                var c = TreasureCollider(id()); var cam = Camera.main;
                if (tr == null || c == null || cam == null) { log.Add("  누를 보물 없음"); return; }
                Vector3 s = cam.WorldToScreenPoint(c.bounds.center);
                var p = new Vector2(s.x, s.y);
                for (int i = 0; i < 2; i++) { tr.EditorFeed(true, p); tr.EditorFeed(false, p); }
            });
            Sleep(0.2f);
        }
        // 상세가 근접 확인 중인 게임을 AR 로 연다 — 창을 닫고 · 상자를 두 번 톡톡 → 축하 화면 → '확인' → 그 게임 상세
        private static int arHunt;
        private static void OpenTreasureAR(string shot)
        {
            const string C = "HuntCelebrate0926/Panel/Content/Info/";
            Do(() => { arHunt = R0926HuntCard.NearHuntId; log.Add("  AR 로 열 게임 " + arHunt); });
            CloseUpload();
            Wait(() => arHunt != 0 && R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + arHunt), 25f, "AR 보물 상자");
            Sleep(0.6f);
            Do(() => FaceTreasure(arHunt));
            Sleep(0.3f);
            DoubleTapTreasure(() => arHunt);
            Wait(() => R0926HuntCelebrate.Showing, 8f, "두 번 톡톡 → 축하 화면");
            Sleep(2.4f);
            Shot(shot);
            Do(() => log.Add("  축하: " + TextOf(C + "Title") + " / " + TextOf(C + "Rank") + " / " + TextOf(C + "PrizeCard/Prize") + " / " + TextOf(C + "Note")));
            Check(() => TextOf(C + "Title") == "보물을 찾았습니다!" && Active(C + "OK"), "두 번 톡톡 → '보물을 찾았습니다!' 축하 화면");
            Do(() => Click(C + "OK"));
            Wait(() => !R0926HuntCelebrate.Showing && Active(HuntCardPath + "Detail"), 12f, "확인 → 그 게임 상세");
            Sleep(1.2f);
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
            Check(() => Has(DevGet(ProfMe, "/api/hunts/profile/" + ProfHost), "\"hosted\":2", "\"avg_participants\":4.0", "\"raters\":3", "\"rating\":3.8"),
                "서버: 주최 2 (끝난 A + 진행 중 B — 10-17) · 평균 참가 4 (끝난 게임 인원) · 3명 평가 → 3.8 (5·4·3 에 3명분 3.5 를 섞음)");
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
            Check(() => TextOf(Col + "/Main").Contains("<b>2</b>") && TextOf(Col + "/Main").Contains("★") && TextOf(Col + "/Main").Contains("3.8") && TextOf(Col + "/Label") == "주최",
                "숫자 줄 '주최' 칸: 2 · ★ 3.8");
            Check(() => !Active(P + "HuntSection0926"), "처음엔 접혀 있음");

            // 펼침
            Do(() => Click(Col));
            Sleep(1.5f);
            Shot("hprof_open");
            Check(() => Active(P + "HuntSection0926") && TextOf(C + "Head/Right") == "주최 기록 · 리뷰", "'주최' 칸을 누르면 같은 카드 안에 펼침 · 머리 '주최 기록 · 리뷰'");
            Check(() => TextOf(C + "RecordBox/Nums/N0").Contains("<b>2</b>") && TextOf(C + "RecordBox/Nums/N2").Contains("<b>4</b>") && TextOf(C + "RecordBox/Foot/Left").StartsWith("첫 주최"),
                "주최 기록: 주최 2 · 평균 참가 4 · 첫 주최 날짜");
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
            Check(() => Active(H + "Detail") && Find(H + "Detail/Viewport/Content/Open") == null, "숨기기 → 상세(주최자 · '보물 열기' 버튼 없음)");
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
            Do(() => { var v = VirtualLocation.Instance; v.SetCoordinates(v.Latitude - 0.00009f, v.Longitude); });   // 10m 물러나 — 상자를 화면에서 누를 수 있게
            Do(() => Click(H + "Detail/Viewport/Content/Join"));
            Sleep(11.0f);   // 참가 → 상세 다시 → 근접 확인(8.5초마다)
            Check(() => TextOf(H + "Detail/Viewport/Content/Near").Contains("두 번 톡톡"), "보이는 거리 안 → '두 번 톡톡 눌러 열어요'");
            OpenTreasureAR("login_celebrate");
            Shot("login_found");
            Do(() => log.Add("  내 상태: " + (Find(H + "Detail/Viewport/Content/Mine")?.GetComponent<Text>()?.text ?? "-")));
            Check(() => (Find(H + "Detail/Viewport/Content/Mine")?.GetComponent<Text>()?.text ?? "").Contains("찾았어요"), "두 번 톡톡 → 찾음");
            Do(() =>
            {
                R0926HuntApi.EditorBase = null;
                LoginManager.EditorAuthBase = null;
                EditorGUIUtility.systemCopyBuffer = savedClipboard ?? "";
                savedClipboard = null;
            });
        }

        // 로그인 바꾸기 — 로그아웃하고 다른 아이디로 들어오면 근처 사람(P2P)·팔로잉 목록·작은 프로필이 새 계정으로 (10-21 대표님
        // 「다른 아이디로 로그인했는데 우측 하단 사진이 그대로 · 서로 멀리 보이고 보물찾기 메시지도 안 온다」 · 「'팔로잉'이면 근처 팔로잉한 사람이 안 나온다」).
        // 시험 서버(127.0.0.1:5099): hunt-dev:p2pa → 숫자 아이디 2147400001(사진 있음) · p2pb → 2147400002(사진 없음) — 운영처럼 숫자(앱이 숫자로 읽는다).
        // 근처 사람 등록·해제와 푸시 토큰 등록·해제는 기록만 하고, 기록에는 숫자 아이디를 hunt_test_p2pa 처럼 이름으로 적는다
        [Serializable] private class P2PEvent { public string kind, token_user, user_id; }
        [Serializable] private class P2PEvents { public P2PEvent[] events; }
        private static List<string> P2PLog()
        {
            var r = JsonUtility.FromJson<P2PEvents>(DevGet("", "/api/dev/p2p_log"));
            var list = new List<string>();
            if (r?.events != null) foreach (var e in r.events) list.Add(e.kind + ":" + e.token_user + ":" + e.user_id);
            return list;
        }
        private static void PlanLoginSwitch()
        {
            const string Dev = "http://127.0.0.1:5099";
            const string IdA = "2147400001", IdB = "2147400002";
            // 근처에 둘 — 팔로잉한 ft1(시험 서버의 팔로잉 목록에 있다)과 모르는 사람
            const string NearJson = "{\"users\":[{\"user_id\":\"ft1\",\"username\":\"yeonnam_cafe\",\"latitude\":36.6593,\"longitude\":126.673,\"altitude\":0,\"distance\":30},"
                                  + "{\"user_id\":\"hunt_test_stranger\",\"username\":\"stranger\",\"latitude\":36.6594,\"longitude\":126.6731,\"altitude\":0,\"distance\":40}]}";
            string P2PId() => P2PManager.Instance != null ? typeof(P2PManager).GetField("currentUserId", NPI).GetValue(P2PManager.Instance) as string : "(없음)";
            bool Following(string id) => P2PManager.Instance != null && ((HashSet<string>)typeof(P2PManager).GetField("followingUserIds", NPI).GetValue(P2PManager.Instance)).Contains(id);
            string Near()
            {
                if (P2PManager.Instance == null) return "-";
                var ids = P2PManager.Instance.GetFilteredNearbyUsers().ConvertAll(u => u.user_id);
                ids.Sort(StringComparer.Ordinal);
                return string.Join(",", ids);
            }
            Image Mini() => ProfileManager.Instance != null ? ProfileManager.Instance.miniAvatarImage : null;
            int MiniW() { var s = Mini() != null ? Mini().sprite : null; return s != null && s.texture != null ? s.texture.width : -1; }
            string MiniName() => ProfileManager.Instance != null && ProfileManager.Instance.miniUsernameText != null ? ProfileManager.Instance.miniUsernameText.text : "";
            string DockLabel() { var l = UnityEngine.Object.FindAnyObjectByType<R0926ProfileLabel>(); return l != null ? l.GetComponent<Text>().text : ""; }
            bool Logged(string ev) => P2PLog().Contains(ev);
            void Diag(string at) => log.Add("  [" + at + "] 근처 '" + Near() + "' · 모드 " + P2PManager.Instance?.GetUserFilterMode()
                                            + " · 자료 " + (P2PManager.Instance?.GetNearbyUsers().Count ?? -1) + " · 팔로잉 ft1 " + Following("ft1"));
            void Mode(UserFilterMode m) { var p = UnityEngine.Object.FindAnyObjectByType<P2PUserFilterPanel>(); if (p != null) p.SetFilterModeExternal(m); else P2PManager.Instance?.SetUserFilterMode(m); }
            var modeWas = UserFilterMode.All;
            int logoutAt = 0;
            // 로그아웃한 뒤엔 앞 사람(p2pa) 토큰으로 간 게 해제 말고는 없어야
            bool NothingOldAfterLogout()
            {
                var l = P2PLog();
                for (int i = logoutAt; i < l.Count; i++)
                    if (l[i].Contains(":hunt_test_p2pa:") && !l[i].StartsWith("unregister:")) { log.Add("  로그아웃 뒤 " + l[i]); return false; }
                return true;
            }

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                SessionState.SetBool(LoginReadyKey, true);
                savedClipboard = EditorGUIUtility.systemCopyBuffer;
                LoginManager.EditorAuthBase = Dev;
                ProfileManager.EditorBase = Dev;
                FollowManager.EditorBase = Dev;
                P2PManager.EditorBase = Dev;
                FirebaseNotification.EditorBase = Dev;
                R0926HuntApi.EditorBase = Dev;
                R0926HuntApi.EditorTestUser = null;
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                DevGet("", "/api/dev/p2p_log?reset=1");
                // 에디터의 가상 사용자(1초마다 근처 사람 'woopang'을 넣는다)가 시험이 넣은 근처 사람 목록을 덮어쓰지 않게
                var vt = UnityEngine.Object.FindAnyObjectByType<P2PVirtualUserTester>();
                if (vt != null) { vt.StopSimulation(); vt.StopAllCoroutines(); }
                var p = UnityEngine.Object.FindAnyObjectByType<P2PUserFilterPanel>();
                if (p != null) modeWas = p.GetCurrentFilterMode();
                Mode(UserFilterMode.FollowingOnly);
            });
            Sleep(0.6f);
            Check(() => LoginManager.Instance != null && !LoginManager.Instance.IsLoggedIn && P2PId() == null, "시작: 로그아웃 · 근처 사람(P2P) 아이디 없음");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });

            // 첫 사람 — 사진 있는 p2pa
            Do(() => { LoginManager.EditorBeforeWebLogin?.Invoke(); CopyLink("p2pa"); });
            Wait(() => LoginManager.Instance.CurrentUser?.id == IdA, 10f, "첫 사람(p2pa) 로그인");
            Check(() => P2PId() == IdA, "근처 사람(P2P)도 p2pa 로");
            Wait(() => MiniW() == 97, 10f, "작은 프로필에 p2pa 사진");
            Check(() => MiniW() == 97 && MiniName() == "시험_p2pa", "작은 프로필 = p2pa 사진·이름");
            Wait(() => Following("ft1"), 8f, "팔로잉 목록 받음");
            Check(() => Following("ft1") && !Following("hunt_test_stranger"), "팔로잉 목록 — 서버가 주는 user_id 로 읽음 (예전엔 id 를 읽어 늘 비었다)");
            Wait(() => Logged("register:hunt_test_p2pa:hunt_test_p2pa"), 45f, "위치 준비 뒤 p2pa 로 등록 (에디터는 GPS 가 없어 30초 기다린다)");
            Check(() => Logged("register:hunt_test_p2pa:hunt_test_p2pa"), "근처 사람 서버에 p2pa 로 등록 (그 사람 토큰으로)");

            // 근처 사람 목록 — '팔로잉만'이면 팔로잉한 사람만 · '모든 사람'이면 다 · 필터를 오가도 바로
            Do(() => Mode(UserFilterMode.All));
            Do(() => P2PManager.Instance.InjectTestUsersData(NearJson));
            Check(() => Near() == "ft1,hunt_test_stranger", "모든 사람 → 근처 둘 다");
            Do(() => Mode(UserFilterMode.FollowingOnly));
            Sleep(0.3f);
            Check(() => Near() == "ft1", "팔로잉만 → 근처의 팔로잉한 사람만 (예전엔 아무도 안 나왔다)");
            Do(() => Diag("팔로잉만"));
            Do(() => Mode(UserFilterMode.All));
            Sleep(0.3f);
            Do(() => Diag("다시 모든 사람"));
            Check(() => Near() == "ft1,hunt_test_stranger", "다시 모든 사람 → 가렸던 사람도 바로 (예전엔 다음 위치 갱신까지 빠졌다)");
            Do(() => Mode(UserFilterMode.FollowingOnly));

            // 로그아웃 — 그 사람 토큰이 남아 있을 때 근처 사람 서버에서 내리고, 작은 프로필을 비운다
            Do(() => { logoutAt = P2PLog().Count; LoginManager.Instance.Logout(); });
            Sleep(1.0f);
            Shot("loginswitch_out");
            Check(() => P2PId() == null && Near() == "", "로그아웃 → 근처 사람 아이디·목록 비움");
            Check(() => Logged("unregister:hunt_test_p2pa:hunt_test_p2pa"), "로그아웃 → 근처 사람 서버에서 p2pa 를 내림 (그 사람 토큰으로)");
            Check(() => Logged("fcm_unregister::hunt_test_p2pa") || FirebaseNotification.GetInstance() == null, "로그아웃 → 푸시 토큰에서 p2pa 떼기");
            Check(() => Mini() != null && Mini().sprite == ProfileManager.Instance.defaultAvatarSprite, "로그아웃 → 작은 프로필 사진이 기본 그림으로");
            Check(() => DockLabel() == "로그인" || DockLabel() == "Sign in", "로그아웃 → 도크 이름 '로그인'");

            // 다른 아이디 — 사진 없는 p2pb
            Do(() => { LoginManager.EditorBeforeWebLogin?.Invoke(); CopyLink("p2pb"); });
            Wait(() => LoginManager.Instance.CurrentUser?.id == IdB, 10f, "다른 아이디(p2pb)로 로그인");
            Check(() => P2PId() == IdB, "근처 사람(P2P)도 p2pb 로 — 예전엔 처음 아이디를 끝까지 썼다");
            Wait(() => Logged("register:hunt_test_p2pb:hunt_test_p2pb"), 5f, "p2pb 로 다시 등록");
            Check(() => Logged("register:hunt_test_p2pb:hunt_test_p2pb"), "근처 사람 서버에 p2pb 로 바로 다시 등록 (위치 준비는 이미 끝났다)");
            Wait(() => MiniName() == "시험_p2pb", 8f, "작은 프로필 이름 p2pb");
            Sleep(1.0f);
            Shot("loginswitch_b");
            Check(() => MiniName() == "시험_p2pb" && MiniW() != 97, "작은 프로필 = p2pb (앞 사람 p2pa 사진이 남지 않음)");
            Check(() => DockLabel() == "시험_p2pb", "도크 이름 = 시험_p2pb");
            Wait(() => Following("ft1"), 8f, "p2pb 의 팔로잉 목록");
            Check(() => Following("ft1"), "p2pb 로 팔로잉 목록 다시 받음");
            Check(() => NothingOldAfterLogout(), "로그아웃 뒤 p2pa 로 보낸 것은 해제뿐");
            Do(() =>
            {
                LoginManager.Instance.Logout();   // 시험 서버에 둔 채로 — 끝난 뒤 운영 서버로 시험 토큰이 가지 않게
                Mode(modeWas);
                P2PManager.EditorBase = null;
                FirebaseNotification.EditorBase = null;
                ProfileManager.EditorBase = null;
                FollowManager.EditorBase = null;
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
                map?.EditorPreview(true, maxF != null ? Convert.ToSingle(maxF.GetValue(map)) : 200f, 37.5665f, 126.978f);
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

        // 키보드 위 입력줄을 쓰는 칸 안의 깜빡이는 커서 (10-20) — 글 끝에 서고 · 입력줄 커서를 따라가고 · 깜빡이고 · 치면 켜지고 · 닫으면 사라지는지,
        // 그리고 입력줄을 쓰는 모든 칸에서 칸 글 영역 안에 입력줄 커서와 같은 모양으로 그려지는지 (여러 줄 칸 포함)
        private static void PlanCaret()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var mt = typeof(UploadInputMirror);
            UploadInputMirror mirror = null;
            InputField src = null, bar = null;
            float endDx = 0f;

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(0.5f);
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.4f);
            Do(() =>
            {
                mirror = WiredMirror();
                src = Find("UploadPage/Clip0926/UploadSheet0926/CubeUploadPage/Panel/NameInput")?.GetComponent<InputField>();
                bar = mirror != null ? mt.GetField("mirrorInput", F).GetValue(mirror) as InputField : null;
                log.Add("  업로드 화면 " + (Active("UploadPage") ? "열림" : "안 열림") + " · 입력줄 " + (mirror != null ? "있음" : "없음") + " · 이름칸 " + (src != null ? "있음" : "없음"));
                if (mirror != null && src != null) mt.GetMethod("ActivateMirrorFor", F).Invoke(mirror, new object[] { src });
            });
            Sleep(0.3f);
            Check(() => FieldCaret(src) != null && FieldCaret(src).gameObject.activeInHierarchy, "입력줄을 켜면 이름칸 안에 커서가 생김");
            Check(() => bar != null && bar.caretWidth >= 3 && bar.customCaretColor, "입력줄 커서에 굵기·분홍이 입혀짐");
            Check(() => { var img = FieldCaret(src); return img != null && bar != null && img.color == bar.caretColor && Mathf.Approximately(img.rectTransform.sizeDelta.x, bar.caretWidth); }, "칸 안 커서가 입력줄 커서와 같은 굵기·색");
            Do(() => { float dx = CaretDx(src); log.Add((Mathf.Abs(dx) < 3f ? "PASS " : "FAIL ") + "빈 칸이면 커서가 글 왼쪽 끝에 섬 (" + dx.ToString("0.0") + ")"); });
            Shot("caret_empty");

            // 치면 글 끝으로
            Do(() => { bar.text = "우팡 테스트"; bar.MoveTextEnd(false); });
            Sleep(0.2f);
            Do(() =>
            {
                float dx = CaretDx(src), w = src.textComponent.preferredWidth;
                endDx = dx;
                log.Add((Mathf.Abs(dx - w) < 4f ? "PASS " : "FAIL ") + $"글 끝에 섬: 커서 {dx:0.0} · 글 폭 {w:0.0}");
            });
            Shot("caret_text");

            // 깜빡임 — 1.6초 동안 켜짐·꺼짐이 다 보여야 한다 (한 번 깜빡이는 데 1.2초)
            int on = 0, off = 0;
            double until = -1;
            steps.Enqueue(() =>
            {
                var img = FieldCaret(src);
                if (until < 0) until = EditorApplication.timeSinceStartup + 1.6;
                if (img != null) { if (img.enabled) on++; else off++; }
                if (EditorApplication.timeSinceStartup < until) return false;
                log.Add((on > 0 && off > 0 ? "PASS " : "FAIL ") + $"깜빡임: 1.6초 동안 켜짐 {on}번 · 꺼짐 {off}번 확인");
                until = -1;
                return true;
            });

            // 꺼진 순간에 글을 치면 바로 켜진다 (치는 동안은 늘 켜져 있게)
            double offUntil = -1;
            steps.Enqueue(() =>
            {
                var img = FieldCaret(src);
                if (offUntil < 0) offUntil = EditorApplication.timeSinceStartup + 2.0;
                if (img != null && !img.enabled) { offUntil = -1; return true; }
                if (EditorApplication.timeSinceStartup < offUntil) return false;
                log.Add("FAIL 꺼진 순간이 오지 않음");
                offUntil = -1;
                return true;
            });
            Do(() => { bar.text += "!"; bar.MoveTextEnd(false); });
            Sleep(0.1f);
            Check(() => { var img = FieldCaret(src); return img != null && img.enabled; }, "꺼져 있다가도 글을 치면 바로 켜짐");

            // 입력줄 커서를 둘째 글자 뒤로 옮기면 칸 안의 커서도 따라간다
            Do(() => { bar.caretPosition = 2; });
            Sleep(0.2f);
            Do(() =>
            {
                float dx = CaretDx(src);
                var t = src.textComponent;
                float want = new TextGenerator().GetPreferredWidth("우팡", t.GetGenerationSettings(Vector2.zero)) / t.pixelsPerUnit;
                log.Add((dx > 0f && dx < endDx && Mathf.Abs(dx - want) < 4f ? "PASS " : "FAIL ") + $"입력줄 커서를 둘째 글자 뒤로 옮기면 따라감: 커서 {dx:0.0} · 두 글자 폭 {want:0.0} · 글 끝 {endDx:0.0}");
            });

            // 닫으면 사라진다 (보내기 칸이 글을 보내지 않도록 누름 없이 닫기)
            Do(() => mt.GetMethod("BeginClose", F).Invoke(mirror, new object[] { false }));
            Sleep(0.4f);
            Check(() => { var img = FieldCaret(src); return img == null || !img.gameObject.activeSelf; }, "입력줄을 닫으면 칸 안의 커서가 사라짐");
            Do(() => { if (src != null) src.text = ""; });

            // 입력줄을 쓰는 모든 칸 — 칸 글 영역 안에 그려지는지
            var all = new List<KeyValuePair<UploadInputMirror, InputField>>();
            Do(() =>
            {
                foreach (var m in UnityEngine.Object.FindObjectsByType<UploadInputMirror>(FindObjectsInactive.Include))
                {
                    var srcs = mt.GetField("sourceInputs", F).GetValue(m) as InputField[];
                    if (srcs == null || mt.GetField("mirrorInput", F).GetValue(m) == null) continue;
                    foreach (var s0 in srcs) if (s0 != null) all.Add(new KeyValuePair<UploadInputMirror, InputField>(m, s0));
                }
                log.Add("  입력줄을 쓰는 칸 " + all.Count + "개");
            });
            int idx = -1, readyFrame = 0, phase = 0;
            var turnedOn = new List<GameObject>();
            steps.Enqueue(() =>
            {
                if (Time.frameCount < readyFrame) return false;
                var m = idx >= 0 && idx < all.Count ? all[idx].Key : null;
                var s = m != null ? all[idx].Value : null;
                var b = m != null ? mt.GetField("mirrorInput", F).GetValue(m) as InputField : null;
                if (phase == 0)
                {
                    if (++idx >= all.Count) return true;
                    m = all[idx].Key;
                    s = all[idx].Value;
                    // 꺼진 칸·꺼진 화면은 위까지 켜 두고 본다 (실제로 쓸 땐 보이는 화면이다) — 바깥부터
                    turnedOn.Clear();
                    for (var a = s.transform; a != null; a = a.parent)
                        if (!a.gameObject.activeSelf) turnedOn.Insert(0, a.gameObject);
                    foreach (var g in turnedOn) g.SetActive(true);
                    if (turnedOn.Count > 0) Canvas.ForceUpdateCanvases();
                    s.text = "";
                    mt.GetMethod("ActivateMirrorFor", F).Invoke(m, new object[] { s });
                    phase = 1;
                    readyFrame = Time.frameCount + 3;   // 입력줄이 켜진 뒤에 — 에디터는 이때 글 전체를 선택해 둔다
                    return false;
                }
                if (phase == 1)
                {
                    b.text = "우팡 테스트";
                    b.MoveTextEnd(false);
                    phase = 2;
                    readyFrame = Time.frameCount + 4;
                    return false;
                }
                log.Add(CaretInside(s, b));
                mt.GetMethod("BeginClose", F).Invoke(m, new object[] { false });
                s.text = "";
                for (int k = turnedOn.Count - 1; k >= 0; k--) turnedOn[k].SetActive(false);
                phase = 0;
                readyFrame = Time.frameCount + 2;
                return false;
            });
        }

        private static Image FieldCaret(InputField f)
        {
            if (f == null || f.textComponent == null) return null;
            var t = f.textComponent.transform.Find("FieldCaret0926");
            return t != null ? t.GetComponent<Image>() : null;
        }

        // 칸 글 왼쪽 끝에서 칸 안의 커서까지 (칸 글 단위)
        private static float CaretDx(InputField f)
        {
            var img = FieldCaret(f);
            if (img == null) return float.NaN;
            var c = new Vector3[4];
            var t = new Vector3[4];
            img.rectTransform.GetWorldCorners(c);
            f.textComponent.rectTransform.GetWorldCorners(t);
            return (c[0].x - t[0].x) / f.textComponent.rectTransform.lossyScale.x;
        }

        // 칸 안의 커서가 칸 글 영역 안에 있고 입력줄 커서와 같은 모양인지 — 한 줄 결과
        private static string CaretInside(InputField f, InputField bar)
        {
            string name = f.transform.parent.parent.name + "/" + f.name;
            var img = FieldCaret(f);
            if (img == null || !img.gameObject.activeInHierarchy) return "FAIL " + name + " · 칸 안에 커서가 없음";
            var c = new Vector3[4];
            var t = new Vector3[4];
            img.rectTransform.GetWorldCorners(c);
            f.textComponent.rectTransform.GetWorldCorners(t);
            float s = f.textComponent.rectTransform.lossyScale.x;
            float left = (c[0].x - t[0].x) / s, right = (t[2].x - c[2].x) / s;   // 글 영역 왼쪽·오른쪽에서 안으로 들어온 만큼
            float mid = (c[0].y + c[2].y) * 0.5f;
            bool ok = left >= -1f && right >= -1f && mid >= t[0].y && mid <= t[2].y;
            bool same = img.color == bar.caretColor && bar.caretWidth >= 3 && bar.customCaretColor;
            string inside = ok ? "예" : "아니오", look = same ? "같음" : "다름";
            return (ok && same ? "PASS " : "FAIL ") + name + $" · 커서 왼쪽 {left:0.0} · 오른쪽 여유 {right:0.0} · 높이 {(c[2].y - c[0].y) / s:0.0} · 칸 글 영역 안 {inside} · 입력줄과 모양 {look}";
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
            int fpsDuring = -1;
            Wait(() => { Redesign0926SplashProf.Analyze(); if (BootOverlay.Showing) fpsDuring = Application.targetFrameRate; return !BootOverlay.Showing; }, 30f, "시작화면 끝");
            Sleep(0.5f);
            Do(() =>
            {
                Redesign0926SplashProf.Analyze(); Redesign0926SplashProf.Stop(); Redesign0926SplashProf.Report(log);
                var s = UnityEngine.Object.FindAnyObjectByType<R0926Splash>(FindObjectsInactive.Include);
                var f = typeof(R0926Splash).GetField("heldFor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                log.Add("  렌즈 출발까지 " + (s != null && f != null ? ((float)f.GetValue(s)).ToString("0.00") + "초" : "?")
                    + " · 시작화면 동안 fps " + fpsDuring + " · 끝난 뒤 fps " + Application.targetFrameRate + " (설정 " + R0926FrameRate.Fps + ")");
            });
            Check(() => fpsDuring == 60 && Application.targetFrameRate == R0926FrameRate.Fps, "시작화면 동안만 60fps, 끝나면 설정값으로");
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

        // 목록 필터 (10-17·10-18 대표님) — '전체' 고르기 목록 · 칩 두 줄(4·3) · 터미널 / 기차 한 칩 · 반려견 두 가지 · 켜진 흰 칩 · '목록|지도|설정' 칸 · AI에게 묻기 숨김
        private static void PlanFilters()
        {
            FilterManager Fm() => UnityEngine.Object.FindAnyObjectByType<FilterManager>();
            bool MenuOpen() { var m = UnityEngine.Object.FindAnyObjectByType<R0926CategoryMenu>(); return m != null && m.IsOpen; }
            Wait(() => !BootOverlay.Showing, 40f, "시작화면 끝");
            Sleep(1.0f);
            Do(() => Fm()?.SetCategory(FilterManager.CategoryFilterState.All));
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Shot("filters_list");
            Do(() => log.Add("  칩: " + ChipLayout()));
            Check(() => ChipGridOk(), "칩 두 줄 — 위 4칸(전체·반려견·공공데이터·3D) · 아래 3칸(지하철·터미널 / 기차·사람), 칸 경계가 위아래로 맞음");
            Do(() => log.Add("  이름: " + ChipLabel("PublicDataToggle") + " · " + ChipLabel("TerminalToggle")));
            Check(() => ChipLabel("TerminalToggle").Contains("/"), "터미널 / 기차 한 칩");
            Check(() => ChipLabel("PublicDataToggle") == "공공데이터" || ChipLabel("PublicDataToggle") == "Public data", "'공공' → '공공데이터'");
            Check(() => NoOldLongPress(), "0926 칩에 옛 길게 누르기(LongPressHandler)가 없음 — 0.8초 넘게 누른 손을 떼도 칩이 다시 꺼지지 않음");
            Check(() => !AnyActive("AskAI0926"), "'AI에게 묻기' 숨김");
            Do(() => log.Add("  '목록|지도|설정' 칸 높이 " + TabH() + " · 칩 높이 " + ChipHeight("CategoryToggle")));
            Check(() => Mathf.Abs(TabH() - ChipHeight("CategoryToggle")) < 1f, "'목록|지도|설정' 칸 높이 = 필터 칩 높이");
            Check(() => WhiteChip("CategoryToggle") && WhiteChip("3DObjectToggle"), "켜진 '전체'·'3D' 칩은 꽉 찬 흰색");

            // '전체' → 고르기 목록
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.4f);
            Shot("filters_menu");
            Check(() => MenuOpen(), "'전체'를 누르면 분류 고르기 목록이 펼쳐짐");
            Check(() => MenuRows() == 8, "분류 8줄 (전체·샵·음식점·카페·공원·화장실·스포츠·랜드마크)");
            Check(() => Fm().CategoryState == FilterManager.CategoryFilterState.All, "펼치기만 해서는 분류가 바뀌지 않음");
            Do(() => PickCategory(4));
            Sleep(0.4f);
            Shot("filters_park");
            Check(() => Fm().CategoryState == FilterManager.CategoryFilterState.Park && !MenuOpen(), "'공원'을 고르면 공원으로 · 목록 닫힘");
            Check(() => ChipLabel("CategoryToggle") == "공원" || ChipLabel("CategoryToggle") == "Park", "칩 이름 = 공원");
            Check(() => Fm().GetActiveFilters()["alcohol"], "분류를 골라도 술 파는 곳을 숨기지 않음");
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.3f);
            Shot("filters_menu_park");
            Do(() => ClickShade());
            Sleep(0.2f);
            Check(() => !MenuOpen() && Fm().CategoryState == FilterManager.CategoryFilterState.Park, "바깥을 누르면 닫힘 · 분류는 그대로");
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.3f);
            Do(() => Modes()?.Show(1));
            Sleep(0.6f);
            Check(() => !MenuOpen(), "지도로 넘어가면 닫힘");
            Do(() => Modes()?.Show(0));
            Sleep(0.6f);
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.3f);
            Do(() => PickCategory(0));
            Sleep(0.3f);
            Check(() => Fm().CategoryState == FilterManager.CategoryFilterState.All, "'전체'로 돌아옴");

            // 터미널 / 기차 — 한 번에 둘 다
            Do(() => ClickToggle("TerminalToggle"));
            Sleep(0.3f);
            Check(() => !Fm().GetActiveFilters()["terminal"] && !Fm().GetActiveFilters()["train"], "터미널 / 기차를 끄면 둘 다 꺼짐");
            Shot("filters_terminal_off");
            Do(() => ClickToggle("TerminalToggle"));
            Sleep(0.3f);
            Check(() => Fm().GetActiveFilters()["terminal"] && Fm().GetActiveFilters()["train"], "다시 켜면 둘 다 켜짐");

            // 반려견 — 두 가지
            Do(() => ClickToggle("PetFriendlyToggle"));
            Sleep(0.4f);
            Shot("filters_pet_only");
            Check(() => Fm().PetState == FilterManager.PetFriendlyFilterState.OnlyPetFriendly, "반려견 한 번 → 반려견 되는 곳만");
            Check(() => ChipLabel("PetFriendlyToggle") == "반려견만" || ChipLabel("PetFriendlyToggle") == "Pets only", "반려견 되는 곳만 → 칩 이름 '반려견만'");
            Do(() => ClickToggle("PetFriendlyToggle"));
            Sleep(0.3f);
            Check(() => Fm().PetState == FilterManager.PetFriendlyFilterState.All, "반려견 두 번 → 전부 ('애견동반 안 됨' 없음)");
            Check(() => ChipLabel("PetFriendlyToggle") == "반려견" || ChipLabel("PetFriendlyToggle") == "Pets", "전부 → 칩 이름 '반려견'");

            // 사람 — '팔로잉만'(꽉 찬 옅은 분홍) · '모든 사람' (10-21 대표님)
            P2PUserFilterPanel Pp() => UnityEngine.Object.FindAnyObjectByType<P2PUserFilterPanel>();
            var p2pWas = UserFilterMode.All;
            Do(() => { var p = Pp(); if (p != null) { p2pWas = p.GetCurrentFilterMode(); p.SetFilterModeExternal(UserFilterMode.FollowingOnly); } });
            Sleep(0.2f);
            Shot("filters_p2p_following");
            Check(() => ChipLabel("P2PUserToggle") == "팔로잉만" || ChipLabel("P2PUserToggle") == "Following only", "팔로잉 → 칩 이름 '팔로잉만'");
            Check(() => PinkChip("P2PUserToggle"), "'팔로잉만'은 꽉 찬 옅은 분홍 (글자는 어둡게)");
            Do(() => Pp()?.SetFilterModeExternal(UserFilterMode.All));
            Sleep(0.2f);
            Shot("filters_p2p_all");
            Check(() => ChipLabel("P2PUserToggle") == "모든 사람" || ChipLabel("P2PUserToggle") == "Everyone", "전체 → 칩 이름 '모든 사람'");
            Check(() => !PinkChip("P2PUserToggle"), "'모든 사람'은 은은한 분홍 (꽉 찬 칸 아님)");
            Do(() => Pp()?.SetFilterModeExternal(p2pWas));
            Check(() => SoundOnAll("ListPanel", "ModeSeg0926"), "'목록|지도|설정' 탭에 누름 소리 (10-21)");

            // 3D — 끄면 어둡게 · 켜면 흰색
            Do(() => ClickToggle("3DObjectToggle"));
            Sleep(0.3f);
            Check(() => !WhiteChip("3DObjectToggle"), "3D 끄면 흰색이 아님");
            Shot("filters_3d_off");
            Do(() => ClickToggle("3DObjectToggle"));
            Sleep(0.3f);
            Check(() => WhiteChip("3DObjectToggle"), "3D 켜면 다시 흰색");
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(1.0f);
        }

        private static RectTransform Chip(string name)
        {
            var grid = UnityEngine.Object.FindAnyObjectByType<R0926ChipGrid>();
            return grid != null ? grid.transform.Find(name) as RectTransform : null;
        }
        private static string ChipLabel(string name) { var t = Chip(name)?.Find("Label")?.GetComponent<Text>(); return t != null ? t.text : ""; }
        private static float ChipHeight(string name) { var c = Chip(name); return c != null ? c.rect.height : -1f; }
        private static float TabH() { var m = Modes(); var t = m != null ? m.transform.Find("TabList") as RectTransform : null; return t != null ? t.rect.height : -1f; }
        private static bool WhiteChip(string name) { var b = Chip(name)?.GetComponent<Image>(); return b != null && b.color.a > 0.9f && b.color.r > 0.85f && b.color.g > 0.85f && b.color.b > 0.85f; }
        private static bool PinkChip(string name)
        {
            var b = Chip(name)?.GetComponent<Image>();
            var t = Chip(name)?.Find("Label")?.GetComponent<Text>();
            return b != null && t != null && b.color.a > 0.9f && b.color.r > 0.95f && b.color.g < 0.9f && b.color.b > 0.8f && t.color.r < 0.3f;
        }
        // 그 아래 이 이름들의 묶음 속 버튼이 모두 누름 소리(UITouchForwarder)를 내나
        private static bool SoundOnAll(string rootPath, params string[] names)
        {
            var root = Find(rootPath);
            if (root == null) { log.Add("  없음 " + rootPath); return false; }
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (Array.IndexOf(names, t.name) < 0) continue;
                foreach (var b in t.GetComponentsInChildren<Button>(true))
                {
                    if (b.GetComponent<UITouchForwarder>() == null) { log.Add("  소리 없음 " + t.name + "/" + b.name); return false; }
                    n++;
                }
            }
            log.Add("  누름 소리 " + n + "개 (" + string.Join("·", names) + ")");
            return n > 0;
        }
        private static bool NoOldLongPress()
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<R0926FilterChip>())
                if (c.GetComponent<LongPressHandler>() != null) return false;
            return true;
        }
        private static bool AnyActive(string name)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<RectTransform>())
                if (t.name == name && t.gameObject.activeInHierarchy) return true;
            return false;
        }
        // 분류 고르기 목록의 몇 번째 줄인지 (아니면 -1)
        private static int MenuRow(Button b)
        {
            var p = b.transform.parent;
            if (p == null || p.name != "Content0926" || !b.name.StartsWith("Row")) return -1;
            for (var a = p; a != null; a = a.parent)
                if (a.name == "CategoryMenu0926") return int.TryParse(b.name.Substring(3), out var i) ? i : -1;
            return -1;
        }
        private static int MenuRows()
        {
            int n = 0;
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>()) if (MenuRow(b) >= 0) n++;
            return n;
        }
        // 분류 고르기 목록의 그 줄을 누른다 (칩을 먼저 눌러 펼친 뒤)
        private static void PickCategory(int index)
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>())
                if (MenuRow(b) == index) { b.onClick.Invoke(); return; }
            log.Add("  분류 줄 없음 " + index);
        }
        private static void ClickShade()
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>())
                if (b.name == "Shade0926" && b.transform.parent != null && b.transform.parent.name == "CategoryMenu0926") { b.onClick.Invoke(); return; }
            log.Add("  분류 목록 바깥 없음");
        }
        private static string ChipLayout()
        {
            var grid = UnityEngine.Object.FindAnyObjectByType<R0926ChipGrid>();
            if (grid == null) return "칩 칸 없음";
            var sb = new System.Text.StringBuilder();
            foreach (Transform t in grid.transform)
            {
                if (!t.gameObject.activeSelf) continue;
                var r = (RectTransform)t;
                sb.Append(t.name).Append(" (").Append(Mathf.RoundToInt(r.anchoredPosition.x)).Append(',').Append(Mathf.RoundToInt(r.anchoredPosition.y))
                  .Append(" w").Append(Mathf.RoundToInt(r.sizeDelta.x)).Append(") ");
            }
            return sb.ToString();
        }
        private static bool ChipGridOk()
        {
            string[] top = { "CategoryToggle", "PetFriendlyToggle", "PublicDataToggle", "3DObjectToggle" };
            string[] bottom = { "MetroToggle", "TerminalToggle", "P2PUserToggle" };
            var grid = UnityEngine.Object.FindAnyObjectByType<R0926ChipGrid>();
            if (grid == null || grid.transform.Find("TrainStationToggle") != null) return false;
            int active = 0;
            foreach (Transform t in grid.transform) if (t.gameObject.activeSelf) active++;
            if (active != 7) return false;
            var T = new RectTransform[4];
            var B = new RectTransform[3];
            for (int i = 0; i < 4; i++) { T[i] = Chip(top[i]); if (T[i] == null) return false; }
            for (int i = 0; i < 3; i++) { B[i] = Chip(bottom[i]); if (B[i] == null) return false; }
            bool Near(float a, float b) => Mathf.Abs(a - b) < 1.5f;
            float L(RectTransform r) => r.anchoredPosition.x;
            float R(RectTransform r) => r.anchoredPosition.x + r.sizeDelta.x;
            foreach (var r in T) if (!Near(r.anchoredPosition.y, T[0].anchoredPosition.y)) return false;
            foreach (var r in B) if (!Near(r.anchoredPosition.y, B[0].anchoredPosition.y)) return false;
            if (B[0].anchoredPosition.y >= T[0].anchoredPosition.y) return false;
            return Near(L(B[0]), L(T[0])) && Near(R(B[0]), R(T[0]))
                && Near(L(B[1]), L(T[1])) && Near(R(B[1]), R(T[2]))
                && Near(L(B[2]), L(T[3])) && Near(R(B[2]), R(T[3]));
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
                if (running) { FollowManager.EditorBase = null; MessagePanelManager.EditorBase = null; ProfileManager.EditorBase = null; P2PManager.EditorBase = null; FirebaseNotification.EditorBase = null; R0926FriendsMap.EditorBase = null; R0926MapTiles.EditorBase = null; }   // 팔로우·검색·프로필·친구 지도·타일 시험 서버도
                if (treasureRun) { PlayerPrefs.DeleteKey(TreasurePref); treasureRun = false; }   // 시험 게임을 지켜보던 기록 (시험이 중간에 끊겨도)
                if (running) RestoreWoopangPrefs();   // WOOPANG 줄 삭제 시험이 덜어 둔 저장값 (시험 도중 플레이를 끈 경우)
                RestoreGameViewSize();   // 가로 시험이 중간에 끊겨도 게임 창 크기는 원래대로
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
                    if (pl.Length > 1 && (pl[1].Trim() == "login" || pl[1].Trim() == "loginswitch" || pl[1].Trim() == "searchfollow" || pl[1].Trim() == "mapprofile")) StashLogin();
                    RestoreWoopangPrefs();   // 지난 시험이 스크립트 다시 읽기로 끊겨 못 되돌린 것
                    MainViewToSimulator();
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
            if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);   // 가상 손가락 — 시험이 중간에 끊겨도 남지 않게
            ptTouch = null;
            PlayerPrefs.SetInt("IsFirstTime", 1);   // ForceShowGuide 가 지운 '본 적 있음' — 그대로 두면 다음 시험 도중 6초 뒤 처음 안내가 뜬다
            PlayerPrefs.Save();
            if (treasureRun) { PlayerPrefs.DeleteKey(TreasurePref); treasureRun = false; R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; }
            RestoreWoopangPrefs();
            RestoreGameViewSize();
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
