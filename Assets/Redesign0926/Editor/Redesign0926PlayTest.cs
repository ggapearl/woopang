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
            R0926SkyWeather.EditorRegionOverride = mode == "store" || mode == "store2" || mode == "store3" || mode == "sky" || mode == "skypages" || mode == "ar" ? StoreRegion : null;
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
            else if (mode == "splashzoom") PlanSplashZoom();
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
            else if (mode == "skypages")
            {
                // 하늘 옆 장 · 고정 — 날씨 좌표(광화문)로 옮겨 시험 서버의 축제 · 핫플 · 친구(서울)가 근처에 들게
                storeSrc = lines.Length > 2 ? lines[2].Trim() : "";
                VirtualLocation.Instance.SetCoordinates(37.5759f, 126.9768f);
                PlanSkyPages();
            }
            else if (mode == "ar") { storeSrc = lines.Length > 2 ? lines[2].Trim() : ""; PlanAr(); }
            else if (mode == "update") PlanUpdate();
            else if (mode == "audit") PlanAudit();
            else if (mode == "hunt") PlanHunt();
            else if (mode == "huntprefs") PlanHuntPrefs();
            else if (mode == "huntpush") PlanHuntPush();
            else if (mode == "huntprofile") PlanHuntProfile();
            else if (mode == "hunttreasure") PlanHuntTreasure();
            else if (mode == "huntchest")
            {
                // 3번째 줄에 배경 그림 폴더를 주면 — 메일·소개용 컷: 주변 장소 글자가 없는 자리(서해 바다)에서 흐린 도시 배경으로
                storeSrc = lines.Length > 2 ? lines[2].Trim() : "";
                if (storeSrc.Length > 0) VirtualLocation.Instance.SetCoordinates(35.5f, 124.3f);
                PlanHuntChest();
            }
            else if (mode == "huntshapes") PlanHuntShapes();
            else if (mode == "huntform") PlanHuntForm();
            else if (mode == "huntshare") PlanHuntShare();
            else if (mode == "huntfinders") PlanHuntFinders();
            else if (mode == "glbload") PlanGlbLoad();
            else if (mode == "login") PlanLogin();
            else if (mode == "loginswitch") PlanLoginSwitch();
            else if (mode == "authexpire") PlanAuthExpire();
            else if (mode == "chatlook") PlanChatLook();
            else if (mode == "v24") PlanV24();
            else if (mode == "farfade") PlanFarFade();
            else if (mode == "nudge") PlanNudge();
            else if (mode == "photomem") PlanPhotoMem();
            else if (mode == "detaillift") PlanDetailLift();
            else if (mode == "zoomind") PlanZoomInd();
            else if (mode == "zoomsnap") PlanZoomSnap();
            else if (mode == "placeedit") PlanPlaceEdit();
            else if (mode == "profileedit") PlanProfileEdit();
            else if (mode == "coords") PlanCoords();
            else if (mode == "comments") PlanComments();
            else if (mode == "nocat") PlanNoCategory();
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
            else if (mode == "mapnear") PlanMapNear();
            else if (mode == "scrolllock") PlanScrollLock();
            else if (mode == "kbswipe") PlanKbSwipe();
            else if (mode == "kbretry") PlanKbRetry();
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
            else if (mode == "cards" || mode == "cards_clean")
            {
                // 파트너 제안 카드 원본 화면 — 서울 시청 앞(가상 위치) · 운영 데이터 그대로 · AR 뒤는 흐린 도시 그림
                storeSrc = lines.Length > 2 ? lines[2].Trim() : "";
                VirtualLocation.Instance.SetCoordinates(CardSpot.x, CardSpot.y);
                PlanCards(mode == "cards_clean");
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
            Do(() => ClickToggle("PetFriendlyToggle"));   // 애견동반은 두 가지 — 다시 누르면 꺼짐(전부)
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

        // 파트너 제안 카드(보호소 · 행사)용 원본 화면 (10-26 효딩쓰 경유 대표님) — 서울 시청 앞 · 로그아웃 · 운영 데이터 그대로.
        // 대표님 계정의 광화문 시연 글 두 곳 · 좌표가 틀린 공공 화장실 한 곳(시청 앞에 찍힌 '현대자동차 개포점')은 찍는 동안만 숨긴다.
        // 강아지 3D 와 보물 상자는 그 자리에 실제로 있는 게 아니라 카메라 앞에 띄운 연출 — 운영 DB · 시험 서버에 아무것도 만들지 않는다
        private static readonly Vector2 CardSpot = new Vector2(37.5665f, 126.9780f);
        private static readonly string[] CardHide = { "dm_34820", "dm_34821", "dm_42176" };
        private const string CardDogUrl = "local://cards/shiba.glb";
        private const string CardDogFile = "C:/woopang/server/uploads/20260608_150000_Shiba_Quaternius/shiba.glb";   // 운영 '시바견 (Quaternius)' 장소와 같은 파일 (CC0)
        private static GameObject cardDog;
        private static bool cardSubway, cardTerminal, cardPublic, cardPet;
        private static FilterManager.CategoryFilterState cardCat;

        private static FilterManager CardFm() => UnityEngine.Object.FindAnyObjectByType<FilterManager>(FindObjectsInactive.Include);
        private static bool CardPetOn() => CardFm() != null && CardFm().PetState == FilterManager.PetFriendlyFilterState.OnlyPetFriendly;

        // clean: 주위 장소 글자 없는 컷만 — 필터는 저장되므로 끝에 전부 처음대로 되돌린다
        private static void PlanCards(bool clean)
        {
            Do(() =>
            {
                Backdrop("bg_city.png");
                foreach (var id in CardHide) HiddenPlaces.Hide(id);
                R0926Nearby.EditorBase = null;                   // 보호소는 운영 서버에서
                R0926Nearby.EditorForget();
                R0926Nearby.EditorOpened = new List<string>();   // 줄을 눌러도 진짜 쪽은 열지 않는다
            });
            Wait(() => !BootOverlay.Showing, 40f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                DeviceFov();
                var f = CardFm().GetActiveFilters();
                cardSubway = f["subway"]; cardTerminal = f["terminal"]; cardPublic = f["publicData"]; cardPet = CardPetOn(); cardCat = CardFm().CategoryState;
            });
            Check(() => LoginManager.Instance != null && !LoginManager.Instance.IsLoggedIn, "로그아웃 상태 — 계정 이름이 찍히지 않음");
            Sleep(12f);   // 주변 장소 · 관광공사 애견동반 여행지를 받아 3D 로 세울 틈
            Do(() => { HideAddressLine(); DeviceFov(); });   // 주소줄은 에디터가 켜질 때의 위치(대표님 PC 쪽)를 그대로 보여 준다

            if (clean) PlanCardsClean(); else PlanCardsFull();

            Do(() =>
            {
                if (cardDog != null) UnityEngine.Object.Destroy(cardDog);
                if (hcGo != null) UnityEngine.Object.Destroy(hcGo);
                var f = CardFm().GetActiveFilters();
                if (CardPetOn() != cardPet) ClickToggle("PetFriendlyToggle");
                if (f["subway"] != cardSubway) ClickToggle("MetroToggle");
                if (f["terminal"] != cardTerminal) ClickToggle("TerminalToggle");
                if (f["publicData"] != cardPublic) ClickToggle("PublicDataToggle");
                CardFm().SetCategory(cardCat);
                foreach (var id in CardHide) HiddenPlaces.Unhide(id);
                R0926Nearby.EditorOpened = null;
            });
            Check(() =>
            {
                var f = CardFm().GetActiveFilters();
                return CardPetOn() == cardPet && f["subway"] == cardSubway && f["terminal"] == cardTerminal && f["publicData"] == cardPublic && CardFm().CategoryState == cardCat;
            }, "필터는 처음대로 되돌림");
        }

        private static void PlanCardsFull()
        {
            // B ③ — 보물찾기 상자 (필터는 처음 그대로 — 주위에 시청 앞 실제 장소들)
            Do(() => HcSpawn("CardChest"));
            Sleep(1.5f);
            CardChestShots("B03_chest", new[] { 0, 60, 120, 200, 290 });

            // A 보호소 — 목록에서 애견동반을 켠다 (지하철 · 터미널 / 기차는 꺼서 애견동반 되는 곳만)
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.4f);
            Do(() =>
            {
                CardFm().SetCategory(FilterManager.CategoryFilterState.All);
                var f = CardFm().GetActiveFilters();
                if (f["subway"]) ClickToggle("MetroToggle");
                if (f["terminal"]) ClickToggle("TerminalToggle");
                if (!CardPetOn()) ClickToggle("PetFriendlyToggle");
            });
            Wait(() => Shown(PlaceListManager.EntryKind.Shelter) == 3, 15f, "가까운 보호소 세 곳 받음");
            Sleep(1.2f);
            Do(() => log.Add("  줄: " + RowsSummary(18) + " · 집계: " + SummaryText()));
            Check(() => ShelterOnTop(), "보호소 세 줄이 맨 위");
            Check(() => PetChip(true), "애견동반 켜짐 — 꽉 찬 진한 노랑");
            Shot("A02_shelters_top");
            Do(() => ListRowToTop(3));
            Sleep(0.8f);
            Shot("A01_pet_places");
            Do(() => ListRowToTop(0));
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(4f);   // 애견동반 아닌 3D 가 걷힐 틈
            Do(() => HideAddressLine());
            CardChestShots("B03_chest_quiet", new[] { 0, 120 });
            Do(() => { if (hcGo != null) UnityEngine.Object.Destroy(hcGo); });

            // A ④ — 강아지 3D(GLB)를 AR 화면에 (애견동반은 켠 채 — 주위엔 애견동반 되는 곳만) · 18m 앞
            CardDogLoad();
            foreach (int yaw in new[] { 110, 130 })
                foreach (int turn in new[] { 0, 90, 180, 270 })
                    CardDogShot("A04_dog_yaw" + yaw.ToString("000") + "_turn" + turn.ToString("000"), yaw, turn);
        }

        // 주위 글자 없는 컷 — 애견동반 켬 · 공공데이터 · 지하철 · 터미널 / 기차 끔 (사용자가 칩을 그렇게 둔 화면 그대로)
        private static void PlanCardsClean()
        {
            Do(() =>
            {
                var f = CardFm().GetActiveFilters();
                if (!CardPetOn()) ClickToggle("PetFriendlyToggle");
                if (f["publicData"]) ClickToggle("PublicDataToggle");
                if (f["subway"]) ClickToggle("MetroToggle");
                if (f["terminal"]) ClickToggle("TerminalToggle");
            });
            Sleep(4f);   // 꺼진 칩의 3D 가 걷힐 틈
            Do(() => HideAddressLine());
            CardDogLoad();
            foreach (int turn in new[] { 90, 270, 60, 300 })
                CardDogShot("A04c_dog_turn" + turn.ToString("000"), 120, turn);
            Do(() => { if (cardDog != null) UnityEngine.Object.Destroy(cardDog); });
            Do(() => HcSpawn("CardChest"));
            Sleep(1.5f);
            CardChestShots("B03c_chest", new[] { 0 });
            CardChestShots("B03c_chest_far", new[] { 0 }, 4.6f, 32f);
        }

        // 강아지 3D — 운영 '시바견' 장소와 같은 파일을 앱의 GLB 로더로 (풀에 꺼진 채 들어 있는 프리팹을 켜고 · DataManager 처럼 뿌리에 로더)
        private static void CardDogLoad()
        {
            Do(() =>
            {
                cardDog = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Download/0001_GLB.prefab"));
                cardDog.name = "CardDog";
                cardDog.SetActive(true);
                cardDog.AddComponent<GLBModelLoader>();
                CardPlace(cardDog, 18f, 1.2f, 0f);
            });
            Sleep(0.2f);
            Do(() =>
            {
                glbDone = glbOk = false;
                GLBModelLoader.PreloadCache(CardDogUrl, File.ReadAllBytes(CardDogFile));
                var ld = cardDog.GetComponent<GLBModelLoader>();
                ld.StartCoroutine(ld.LoadGLBModelCoroutine(CardDogUrl, 1f, r => { glbOk = r; glbDone = true; }));
            });
            Wait(() => glbDone, 20f, "강아지 3D 불러옴");
            Check(() => glbOk && cardDog.GetComponent<GLBModelLoader>().GetLoadedModel() != null, "강아지 3D 모델이 붙음");
            Sleep(1.5f);
        }

        // 18m 앞에 turn 도 비껴 선 강아지를 yaw 방향에서 한 장
        private static void CardDogShot(string name, int yaw, int turn)
        {
            Do(() => { AimCamera(-4f, yaw); CardPlace(cardDog, 18f, 1.2f, turn); });
            Sleep(1.4f);
            Do(() => { log.Add("  [" + name + "]"); LogTargets(); });
            Shot(name);
        }

        // 보물 상자를 카메라 앞 d 미터에 두고 정면에서 face 도 비껴 — 방향마다 한 장씩
        private static void CardChestShots(string name, int[] yaws, float d = 3.2f, float face = -28f)
        {
            foreach (int yaw in yaws)
            {
                int y = yaw;
                Do(() => AimCamera(-4f, y));
                HcPlace(d);
                Do(() => HcFace(face));
                Sleep(1.6f);
                Do(() => { log.Add("  [" + name + " " + y + "°]"); LogTargets(); });
                Shot(name + "_yaw" + y.ToString("000"));
            }
        }

        // 카메라 앞 d 미터 · 아래 down 미터에 놓고, 앞면이 카메라를 보다가 turn 도 비껴 서게
        private static void CardPlace(GameObject go, float d, float down, float turn)
        {
            var cam = Camera.main;
            if (go == null || cam == null) return;
            Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
            go.transform.position = cam.transform.position + f * d + Vector3.down * down;
            go.transform.rotation = Quaternion.LookRotation(-f) * Quaternion.Euler(0f, turn, 0f);
        }

        // 목록을 i 번째 줄이 보이는 칸 맨 위에 오게 굴린다
        private static void ListRowToTop(int i)
        {
            var rows = ListRows();
            if (rows.Count <= i) { log.Add("  줄 모자람 " + rows.Count); return; }
            var sr = rows[i].GetComponentInParent<ScrollRect>();
            if (sr == null || sr.viewport == null) { log.Add("  목록 스크롤 없음"); return; }
            Canvas.ForceUpdateCanvases();
            sr.StopMovement();
            var vc = new Vector3[4]; sr.viewport.GetWorldCorners(vc);
            var rc = new Vector3[4]; ((RectTransform)rows[i]).GetWorldCorners(rc);
            sr.content.anchoredPosition += new Vector2(0f, (vc[1].y - rc[1].y) / sr.content.lossyScale.y);
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

            // 4a) 친구 목록 줄 → 프로필이 아니라 지도가 그 친구 자리로 (10-29 대표님) — 프로필은 지도의 핀을 눌러서
            Do(() => tap(row(MpFan)));
            Sleep(1.4f);
            Check(() => Active("ListPanel") && !Active("FullProfilePanel"), "친구 줄 → 프로필은 안 뜨고 목록 시트 그대로");
            Check(() =>
            {
                var c = center();
                var list = (System.Collections.IList)T.GetField("friends", F).GetValue(map);
                foreach (var o in list)
                {
                    var ft = o.GetType();
                    if ((string)ft.GetField("user_id").GetValue(o) != MpFan) continue;
                    double la = (float)ft.GetField("lat").GetValue(o), lo = (float)ft.GetField("lon").GetValue(o);
                    return Math.Abs(c[0] - la) < 0.03 && Math.Abs(c[1] - lo) < 0.03;
                }
                return false;
            }, "지도 가운데가 그 친구 자리로");
            Check(() => on(row(MpFan)?.Find("Sel")) && !on(row(MpFriend)?.Find("Sel")), "누른 줄만 골라짐");

            // 4b) 지도의 핀 → 프로필 · 팔로우 안 한 사람은 '팔로우' 한 칸 (메세지 버튼 없음) · 뒤로가기로 닫으면 시트가 다시
            Do(() => tap(pin(MpFan)));
            Wait(() => Active("FullProfilePanel") && who() == "goosoon_fan" && SfFollowState() != "?", 8f, "핀 → 프로필");
            Sleep(0.8f);
            Shot("mapprofile_profile_follow");
            Check(() => OpenPanels() == "FullProfilePanel", "친구 핀 → 그 사람 프로필만");
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

        // 지도 목록 (10-29 대표님) — 친구 줄(최근 접속순) 아래에 근처 보물찾기(가까운 순) · 줄을 누르면 프로필·보물찾기 창이 아니라 지도가 그 자리로 ·
        // 핀·상자를 누르면 프로필 / 보물찾기 창 · 내 위치 점 둘레에 나침반 방향 표시.
        // 시험 서버(127.0.0.1:5099)의 시험 아이디와 시험 게임만 쓴다 — 운영 서버에 아무것도 올리지 않는다
        private const string MnHostA = "hunt_test_mnha", MnHostB = "hunt_test_mnhb";
        private static void PlanMapNear()
        {
            const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var T = typeof(R0926FriendsMap);
            R0926FriendsMap map = null;
            string viewBackup = null;
            int nearId = 0, farId = 0;
            Func<RectTransform> pins = () => T.GetField("pinLayer", F)?.GetValue(map) as RectTransform;
            Func<string, Transform> row = name => (T.GetField("listContent", F)?.GetValue(map) as RectTransform)?.Find(name);
            Func<Transform, bool> on = t => t != null && t.gameObject.activeInHierarchy;
            Action<Transform> tap = t =>
            {
                if (t == null) { log.Add("  누를 것 없음"); return; }
                UnityEngine.EventSystems.ExecuteEvents.Execute(t.gameObject, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left }, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            };
            Func<double[]> center = () =>
            {
                var vp = (RectTransform)map.transform;
                object n = T.GetMethod("ToNorm", F).Invoke(map, new object[] { vp.rect.size / 2f });
                var a = new object[] { n, null, null };
                T.GetMethod("LatLon", SF).Invoke(null, a);
                return new[] { (double)a[1], (double)a[2] };
            };
            Func<string, string> sub = id => row(id)?.Find("Sub")?.GetComponent<Text>()?.text ?? "";

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
                SessionState.SetBool(LoginReadyKey, true);
                const string dev = "http://127.0.0.1:5099";
                MessagePanelManager.EditorBase = dev;
                ProfileManager.EditorBase = dev;
                FollowManager.EditorBase = dev;
                P2PManager.EditorBase = dev;
                FirebaseNotification.EditorBase = dev;
                R0926FriendsMap.EditorBase = dev;
                R0926HuntApi.EditorBase = dev;
                R0926HuntApi.EditorTestUser = MpMe;
                R0926MapTiles.EditorBase = "http://127.0.0.1:9";
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
                viewBackup = PlayerPrefs.HasKey("R0926Map.View") ? PlayerPrefs.GetString("R0926Map.View") : null;
                PlayerPrefs.DeleteKey("R0926Map.View");
                PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, LocalTestLogin + MpMe.Substring("hunt_test_".Length));
                DevPost(MpMe, "/api/dev/search_reset", "{}");
                SfLogin(true, MpMe);
                VirtualLocation.Instance.SetCoordinates(37.5759f, 126.9768f);
                nearId = DevCreateHuntAt(MnHostA, "지도 시험 가까운 보물", 37.5790f, 126.9790f, null, null);   // 약 400m
                farId = DevCreateHuntAt(MnHostB, "지도 시험 먼 보물", 37.5950f, 126.9950f, null, null);       // 약 2.7km
                log.Add("  시험 게임 " + nearId + " · " + farId);
                mpErrors.Clear();
                Application.logMessageReceived -= MpLog;
                Application.logMessageReceived += MpLog;
            });
            Check(() => nearId > 0 && farId > 0, "개발 서버에 시험 게임 둘 (가까운 · 먼)");
            Check(() => LoginManager.Instance != null && LoginManager.Instance.IsLoggedIn && LoginManager.Instance.CurrentUser.id == MpMe, "준비: 시험 아이디");

            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => Modes()?.Show(1));
            Sleep(0.8f);
            Do(() => map = UnityEngine.Object.FindAnyObjectByType<R0926FriendsMap>(FindObjectsInactive.Include));
            Wait(() => on(row("Friend_" + MpFriend)) && on(row("HuntRow_" + nearId)) && on(row("HuntRow_" + farId)), 15f, "친구 줄 · 보물찾기 줄 받음 (시험 서버)");
            Sleep(0.8f);
            Shot("mapnear_list");

            // 1) 목록 순서 — 친구 줄 → '근처 보물찾기' 제목 → 가까운 순 보물찾기 → 광고 칸
            Check(() => row("HuntSection") != null && row("Friend_" + MpFriend).GetSiblingIndex() < row("HuntSection").GetSiblingIndex()
                && row("HuntSection").GetSiblingIndex() < row("HuntRow_" + nearId).GetSiblingIndex(), "친구 줄 아래에 '근처 보물찾기' 제목");
            Check(() => row("HuntRow_" + nearId).GetSiblingIndex() < row("HuntRow_" + farId).GetSiblingIndex(), "보물찾기는 가까운 순 (400m 가 2.7km 보다 위)");
            Check(() => row("HuntSection").GetComponentInChildren<Text>(true).text == "근처 보물찾기", "제목 글: 근처 보물찾기");
            Do(() => log.Add("  가까운 보물 둘째 줄: " + sub("HuntRow_" + nearId) + " · 먼 보물: " + sub("HuntRow_" + farId) + " · 이름: " + row("HuntRow_" + nearId).Find("Name")?.GetComponent<Text>()?.text));
            Check(() => sub("HuntRow_" + nearId).Contains("m") && sub("HuntRow_" + farId).Contains("km"), "보물찾기 줄에 거리 (400m · 2.7km)");
            Check(() => on(row("HuntRow_" + nearId).Find("HuntIcon")) && !on(row("HuntRow_" + nearId).Find("Avatar")), "보물찾기 줄은 아바타 대신 보물 그림");
            Check(() => on(row("Friend_" + MpFriend).Find("Avatar")) && !on(row("Friend_" + MpFriend).Find("HuntIcon")), "친구 줄은 아바타 그대로");
            Check(() => on(pins().Find("Hunt_" + nearId)), "지도에도 그 게임 상자");

            // 2) 보물찾기 줄 → 보물찾기 창이 아니라 지도가 그 자리로
            Do(() => tap(row("HuntRow_" + farId)));
            Sleep(1.4f);
            Shot("mapnear_focus_far");
            Check(() => Active("ListPanel") && !Active("UploadPage"), "보물찾기 줄 → 보물찾기 창은 안 뜸 (목록 시트 그대로)");
            Check(() => { var c = center(); return Math.Abs(c[0] - 37.5950) < 0.01 && Math.Abs(c[1] - 126.9950) < 0.01; }, "지도 가운데가 그 게임 자리로");
            Check(() => on(row("HuntRow_" + farId).Find("Sel")) && !on(row("HuntRow_" + nearId).Find("Sel")), "누른 줄만 골라짐");

            // 3) 지도의 상자 → 그 게임의 보물찾기 창
            Do(() => tap(pins().Find("Hunt_" + farId)));
            Wait(() => Active(HuntCardPath + "Detail"), 10f, "상자 → 보물찾기 창");
            Sleep(1.0f);
            Shot("mapnear_hunt_card");
            Check(() => Active("UploadPage") && DetailTitle().Contains("지도 시험 먼 보물"), "지도의 상자 → 그 게임의 보물찾기 창");
            CloseAll();
            Sleep(0.6f);

            // 4) 내 위치 방향 — 나침반 값이 있을 때 점 둘레의 쉐브론이 그 방향으로 (PC 에는 나침반이 없어 시험 값을 넣는다)
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() =>
            {
                Modes()?.Show(1);
                typeof(R0926FriendsMap).GetField("previewHeading", SF).SetValue(null, 90f);
                VirtualLocation.Instance.SetCoordinates(37.30f, 126.97f);   // 친구·상자 핀에 가리지 않게 잠깐 30km 남쪽에서 (타일 없는 시험 지도는 멀리서 보인다)
                T.GetField("zoom", F).SetValue(map, T.GetField("maxZoom", F).GetValue(map) is float mz ? (double)mz : 200.0);
                map.LocateMe();
            });
            Sleep(1.4f);
            Shot("mapnear_heading");
            Check(() =>
            {
                var h = T.GetField("meHeading", F)?.GetValue(map) as RectTransform;
                if (h == null || !h.gameObject.activeInHierarchy) return false;
                float z = h.localEulerAngles.z;
                log.Add("  방향 쉐브론 z = " + z.ToString("F1"));
                return Mathf.Abs(Mathf.DeltaAngle(z, -90f)) < 3f;
            }, "내 위치 점 둘레 방향 표시가 동쪽(90°)을 가리킴");
            Do(() => { typeof(R0926FriendsMap).GetField("previewHeading", SF).SetValue(null, -1f); VirtualLocation.Instance.SetCoordinates(37.5759f, 126.9768f); });
            Sleep(0.6f);
            Check(() => { var h = T.GetField("meHeading", F)?.GetValue(map) as RectTransform; return h != null && !h.gameObject.activeSelf; }, "나침반 값이 없으면 방향 표시는 숨김");

            Check(() => mpErrors.Count == 0, "오류 없음" + (mpErrors.Count > 0 ? " — " + mpErrors[0] : ""));
            Do(() => { foreach (var e in mpErrors) log.Add("  오류: " + e); });

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

        private static InputField ChatInputField() => UnityEngine.Object.FindAnyObjectByType<MessagePanelManager>(FindObjectsInactive.Include)?.chatInput;

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
            // 10-25 N-Y: 대화 입력줄만 키웠다 — 키보드 위 입력줄은 그대로
            Check(() =>
            {
                var f = ChatInputField();
                var ax = f != null ? f.GetComponent<AutoExpandInputField>() : null;
                var send = f != null ? f.transform.parent.Find("SendButton") as RectTransform : null;
                var ph = f != null ? f.placeholder as Text : null;
                if (f == null || ax == null || ax.scrollRect == null || send == null || ph == null) return false;
                var area = (RectTransform)f.transform.parent;
                var msg = (RectTransform)ax.scrollRect.transform;
                float h = ((RectTransform)f.transform).rect.height;
                log.Add("  입력 글자 " + f.textComponent.fontSize + " · 안내 " + ph.fontSize + " · 칸 " + h.ToString("0") + " · 줄 " + area.rect.height.ToString("0") + " · 보내기 " + send.rect.width.ToString("0") + " · 말풍선 칸 아래 " + msg.offsetMin.y.ToString("0") + " · 최대 " + ax.maxHeight.ToString("0"));
                return f.textComponent.fontSize == 54 && ph.fontSize == 52 && Mathf.Abs(h - 132) < 1 && Mathf.Abs(area.rect.height - 176) < 1
                    && Mathf.Abs(send.rect.width - 136) < 1 && Mathf.Abs(msg.offsetMin.y - 220) < 1 && Mathf.Abs(ax.maxHeight - 290) < 1;
            }, "대화 입력줄 — 글자 54 · 안내 52 · 칸 132 · 줄 176 · 보내기 136 · 말풍선 칸 아래 220");
            Check(() =>
            {
                int n = 0;
                foreach (var m in UnityEngine.Object.FindObjectsByType<UploadInputMirror>(FindObjectsInactive.Include))
                {
                    var f = m.transform.Find("UploadInputMirror/TextInput")?.GetComponent<InputField>();
                    if (f == null) continue;
                    if (f.textComponent.fontSize != 46 || Mathf.Abs(((RectTransform)f.transform).rect.height - 110) > 1) return false;
                    n++;
                }
                return n > 0;
            }, "키보드 위 입력줄은 그대로 (글자 46 · 칸 110)");
            Do(() => { var f = ChatInputField(); if (f != null) f.text = "입력칸이 커졌는지 보려고 길게 씁니다. 줄이 넘어가면 칸이 위로 자라고 네 줄을 넘으면 칸 안에서 굴러갑니다. 한 줄 더 씁니다. 또 한 줄 더 씁니다. 마지막 줄입니다."; });
            Sleep(0.4f);
            Shot("chat_input_long");
            Check(() =>
            {
                var f = ChatInputField();
                var ax = f != null ? f.GetComponent<AutoExpandInputField>() : null;
                if (ax == null || ax.scrollRect == null) return false;
                float h = ((RectTransform)f.transform).rect.height, bottom = ((RectTransform)ax.scrollRect.transform).offsetMin.y;
                log.Add("  긴 글 칸 " + h.ToString("0") + " · 말풍선 칸 아래 " + bottom.ToString("0"));
                return h > 133 && h <= 290.5f && Mathf.Abs(bottom - (220 + h - 132)) < 1;
            }, "긴 글이면 입력칸이 위로 자라고(최대 290) 말풍선 칸도 같이 올라감");
            Do(() => { var f = ChatInputField(); if (f != null) f.text = ""; });
            Sleep(0.3f);
            Check(() => { var f = ChatInputField(); return f != null && Mathf.Abs(((RectTransform)f.transform).rect.height - 132) < 1; }, "글을 지우면 입력칸이 원래 높이로");
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

        // ── N-B (10-25 대표님): 키보드가 떠 있을 때 입력칸을 아래로 쓸어내리면 키보드가 내려간다 ──
        // 대화·댓글 창 · 키보드 위 입력줄(장소 추가) · 메시지 검색칸. 키보드가 내려가며 입력줄도 내려와 손가락 밑으로 다시 오므로,
        // 그 자리(키보드 없을 때 칸 자리)에서 손을 떼어 입력칸이 다시 열리거나 단추가 눌리지 않는지 본다
        private static InputField kbField;
        private static string kbText;
        private static Button kbButton;
        private static int kbClicks, kbSent;
        private static float kbSheet;
        private static Vector2 kbRestField, kbRestSend;
        private static readonly Vector2[] kbLegs = new Vector2[3];   // 내리기 + 멈춤 두 번 (창이 제자리로 돌아올 때까지 누른 채)
        private static readonly System.Reflection.FieldInfo sheetApplied = typeof(R0926SwipeDismiss).GetField("applied", NPI);

        private static void PlanKbSwipe()
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
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Check(() => UnityEngine.Object.FindAnyObjectByType<R0926KeyboardSwipe>() != null, "키보드 쓸어내리기가 캔버스에 붙어 있음");

            // 1) 대화창 입력칸 — 키보드만 내려가고 창은 그대로 (아래로 밀어 닫는 시트가 같이 끌려 내려가지 않는다)
            Do(() => Mpm()?.OpenChatRoom("chatlook_peer", "연남동이웃", null, false));
            Sleep(1.6f);
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Sleep(0.4f);
            Do(() => FillSampleChat(false));
            Sleep(0.8f);
            Do(() =>
            {
                kbRestField = Center(Mkh()?.targetInputField) ?? default;
                kbRestSend = Center(Mpm()?.sendButton) ?? default;
                log.Add($"  키보드 없을 때 입력칸 가운데 {kbRestField} · 보내기 {kbRestSend} (화면 {Screen.width}x{Screen.height})");
            });
            KbUpFor(Mkh);
            Shot("kb_chat_up");
            Check(() => KbUp(Mkh()), "대화창: 키보드가 떠 있고 입력칸에 커서");
            Do(() => KbLegTo(Mkh()?.targetInputField, kbRestField));
            KbSwipe(() => Mkh()?.targetInputField, "kb_chat_swipe", "ChatRoomPanel");
            Sleep(1.0f);
            Shot("kb_chat_down");
            Check(() => KbDown(Mkh()), "대화창 입력칸을 아래로 쓸어내림 → 키보드가 내려가고 커서도 빠짐 · 제자리로 온 입력칸 위에서 손을 떼도 다시 열리지 않음");
            Check(() => Active("ChatRoomPanel") && kbSheet < 2f, "… 대화창은 닫히지도 끌려 내려가지도 않음");
            Check(() => ChatFits("쓸어내린 뒤"), "… 창이 키보드 없는 제자리로");

            // 2) 누르기만 · 옆으로 · 위로 · 대화 목록 굴리기 — 키보드 그대로
            KbUpFor(Mkh);
            Finger(() => Center(Mkh()?.targetInputField), new[] { new Vector2(0f, -4f) }, 2, null, null);
            Sleep(0.8f);
            Check(() => KbUp(Mkh()), "입력칸을 살짝 누르기만 하면 키보드 그대로");
            Finger(() => Center(Mkh()?.targetInputField), new[] { new Vector2(-120f, -10f) }, 8, null, null);
            Sleep(0.8f);
            Check(() => KbUp(Mkh()), "입력칸에서 옆으로 밀면(커서 옮기기) 키보드 그대로");
            Finger(() => Center(Mkh()?.targetInputField), new[] { new Vector2(0f, 200f) }, 8, null, null);
            Sleep(0.8f);
            Check(() => KbUp(Mkh()), "입력칸에서 위로 밀면 키보드 그대로");
            Finger(() => { var k = Mkh(); var sr = k != null && k.scrollViewRect != null ? k.scrollViewRect.GetComponent<ScrollRect>() : null; return sr != null ? ListPoint(sr) : null; },
                new[] { new Vector2(0f, 150f) }, 8, null, null);
            Sleep(1.4f);
            Check(() => KbUp(Mkh()), "대화 목록을 굴리면 키보드 그대로 (예전대로)");

            // 3) 보내기 단추에서 시작해 아래로 — 키보드는 내려가고, 제자리로 온 보내기 위에서 손을 떼도 눌리지 않는다
            Do(() =>
            {
                kbButton = Mpm()?.sendButton;
                kbClicks = 0;
                if (kbButton != null) kbButton.onClick.AddListener(KbClick);
                var k = Mkh();
                if (k != null && k.targetInputField != null) k.targetInputField.text = "";   // 혹시 눌려도 빈 글이라 보내지지 않는다
                log.Add("  보내기 단추: " + (kbButton != null ? kbButton.name + (kbButton.interactable ? "" : " (꺼짐)") : "없음"));
            });
            Do(() => KbLegTo(kbButton, kbRestSend));
            KbSwipe(() => kbButton, "kb_chat_send", "ChatRoomPanel");
            Sleep(1.0f);
            Check(() => KbDown(Mkh()), "보내기 단추에서 시작해 아래로 → 키보드가 내려감");
            Check(() => kbButton != null && kbClicks == 0 && kbButton.enabled, "… 보내기는 눌리지 않고, 손을 뗀 뒤 단추는 다시 켜짐");
            Check(() => Active("ChatRoomPanel") && kbSheet < 2f, "… 대화창은 그대로");
            Do(() => { if (kbButton != null) kbButton.onClick.RemoveListener(KbClick); });
            Do(KeyboardDown);
            CloseAll();
            Sleep(1.0f);

            // 4) 댓글 창 — 같은 방식
            Do(() => UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include)?.OpenCommentPanel(1, "시험"));
            Sleep(1.8f);
            Do(() => kbRestField = Center(CommentMkh()?.targetInputField) ?? default);
            KbUpFor(CommentMkh);
            Shot("kb_comment_up");
            Check(() => KbUp(CommentMkh()), "댓글 창: 키보드가 떠 있고 입력칸에 커서");
            Do(() => KbLegTo(CommentMkh()?.targetInputField, kbRestField));
            KbSwipe(() => CommentMkh()?.targetInputField, "kb_comment_swipe", null);
            Sleep(1.0f);
            Shot("kb_comment_down");
            Check(() => KbDown(CommentMkh()), "댓글 입력칸을 아래로 쓸어내림 → 키보드가 내려가고 손을 뗀 뒤 다시 열리지 않음");
            Check(() => { var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include); return cm != null && cm.commentPanel != null && cm.commentPanel.activeInHierarchy; }, "… 댓글 창은 그대로");

            // 4-2) 댓글이 많을 때 목록 가운데서 아래로 굴리기 (키보드 없이) — 시트 전체가 밀어 닫기 자리라 목록 대신 창이 내려가는지 본다
            Do(() =>
            {
                var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include);
                var make = typeof(CommentManager).GetMethod("CreateCommentItem", NPI);
                for (int i = 0; cm != null && make != null && i < 14; i++)
                    make.Invoke(cm, new object[] { new CommentData { id = 900 + i, user_id = "pt_" + i, username = "시험" + i, content = i % 2 == 0 ? "목록 굴리기 시험" : "목록 굴리기 시험 — 두 줄이 되게 조금 길게 씁니다 두 줄이 되게", created_at = System.DateTime.Now.AddHours(-i).ToString(), like_count = i } });
                var sr = CommentList();
                if (sr != null) { Canvas.ForceUpdateCanvases(); sr.verticalNormalizedPosition = 0.5f; }
                log.Add("  댓글 목록: " + (sr != null ? $"높이 {sr.content.rect.height:0}/{((RectTransform)sr.transform).rect.height:0}" : "없음"));
            });
            Sleep(0.6f);
            Do(() => { var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include); kbSheet = 0f; kbRestField = cm != null && cm.panelRect != null ? cm.panelRect.anchoredPosition : default; });
            LockCase("댓글 목록 가운데 · 아래로 굴리기", CommentList, new Vector2(0f, -300f), Vector2.zero, "kb_comment_list", false, 10,
                () => { var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include); if (cm != null && cm.panelRect != null) kbSheet = Mathf.Max(kbSheet, kbRestField.y - cm.panelRect.anchoredPosition.y); });
            Sleep(0.6f);
            Do(() => log.Add($"  댓글 창이 내려간 만큼 {kbSheet:0.#}"));
            Check(() => { var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include); return cm != null && cm.commentPanel != null && cm.commentPanel.activeInHierarchy && kbSheet < 2f; }, "댓글 목록 가운데서 아래로 굴리면 목록만 굴러가고 창은 그대로");
            Do(() => { var sr = CommentList(); if (sr != null) sr.verticalNormalizedPosition = 1f; });
            Sleep(0.5f);
            Finger(() => { var sr = CommentList(); return sr != null ? ListPoint(sr) : null; }, new[] { new Vector2(0f, -300f) }, 10, null, "kb_comment_list_top");
            Sleep(1.2f);
            Check(() => { var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include); return cm != null && cm.commentPanel != null && !cm.commentPanel.activeInHierarchy; }, "목록이 맨 위일 때 아래로 끌면 예전대로 댓글 창이 닫힘");
            Do(() => { var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include); if (cm != null && cm.commentPanel != null && cm.commentPanel.activeInHierarchy) cm.ClosePanel(); });
            Sleep(1.2f);

            // 5) 키보드 위 입력줄 (장소 추가 이름칸, 보내기 칸으로 둔다) — 입력줄 · 그 위 빈 곳 · ✓ 단추에서 시작해 아래로
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.4f);
            Do(() =>
            {
                kbSent = 0;
                KbNameInput();
                var m = WiredMirror();
                if (m != null && kbField != null) m.SetSubmitAction(kbField, KbSend);
            });
            KbMirrorOpen("쓸어내림 시험");
            Shot("kb_mirror_up");
            Check(() => WiredMirror()?.ActiveSource != null && WiredMirror().ActiveSource == kbField, "장소 이름칸: 키보드 위 입력줄이 떠 있음");
            Do(() => KbLeg(new Vector2(0f, -Screen.height * 0.3f)));
            KbSwipe(() => WiredMirror()?.Bar, "kb_mirror_swipe", null);
            Sleep(0.8f);
            Shot("kb_mirror_down");
            KbMirrorClosed("입력줄을 아래로 쓸어내림");

            KbMirrorOpen("쓸어내림 시험 둘");
            Finger(() => new Vector2(Screen.width * 0.5f, Screen.height * 0.72f), new[] { new Vector2(0f, -Screen.height * 0.25f) }, 10, null, null);
            Sleep(0.8f);
            KbMirrorClosed("입력줄 위 빈 곳(막)을 아래로 쓸어내림");

            KbMirrorOpen("쓸어내림 시험 셋");
            Do(() =>
            {
                var m = WiredMirror();
                kbButton = m != null ? typeof(UploadInputMirror).GetField("closeButton", NPI).GetValue(m) as Button : null;
                log.Add("  입력줄 단추: " + (kbButton != null ? kbButton.name : "없음"));
            });
            Do(() => KbLeg(new Vector2(0f, -Screen.height * 0.3f)));
            KbSwipe(() => kbButton, "kb_mirror_button", null);
            Sleep(0.8f);
            KbMirrorClosed("입력줄 ✓(보내기) 단추에서 시작해 아래로");
            Check(() => kbButton != null && kbButton.enabled, "… 손을 뗀 뒤 단추는 다시 켜짐");
            Do(() =>
            {
                var m = WiredMirror();
                if (m != null && kbField != null) m.SetSubmitAction(kbField, null);
                if (kbField != null) kbField.text = "";
            });
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.2f);

            // 6) 칸 안에서 바로 쓰는 칸 (메시지 검색) — 내렸다가 칸 위로 되돌아와 떼도 다시 열리지 않는다
            Do(() => { var m = Mpm(); if (m != null && m.messagePanel != null) m.messagePanel.SetActive(true); });
            Sleep(1.0f);
            Do(() => { var s = Mpm()?.searchInput; if (s != null) { s.Select(); s.ActivateInputField(); } });
            Sleep(0.8f);
            Shot("kb_search_up");
            Check(() => Mpm()?.searchInput != null && Mpm().searchInput.isFocused, "메시지 검색칸에 커서");
            Finger(() => Center(Mpm()?.searchInput), new[] { new Vector2(0f, -120f), new Vector2(0f, 120f) }, 10, null, "kb_search_swipe");
            Sleep(0.8f);
            Check(() => { var s = Mpm()?.searchInput; return s != null && !s.isFocused && s.enabled; }, "검색칸을 아래로 쓸어내림 → 커서가 빠지고, 칸 위로 돌아와 손을 떼도 다시 열리지 않음");
            Check(() => Active("MessagePanel"), "… 메시지 창은 그대로");
            CloseAll();
            Sleep(0.8f);

            Do(() =>
            {
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
            });
        }

        // 10-29 대표님 「장소추가 / 3D모델 추가 입력칸 — 키보드가 간헐적으로 안 뜨고 입력줄만 뜬다」
        // 안드로이드 유니티 키보드는 키보드 창이 포커스를 얻을 때 한 번만 키보드를 부른다 — 그 한 번이 묻힌 것을
        // 에디터 가상 키보드가 '못 들은 척'(EditorVirtualKeyboard.DropShows)으로 흉내 내고, 입력줄이 다시 부르는지 본다
        private static void PlanKbRetry()
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
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.4f);
            Do(KbNameInput);
            Check(() => kbField != null && WiredMirror() != null, "장소 추가 — 장소 이름칸과 키보드 위 입력줄");

            // 1) 평소 — 누르면 바로 뜨고, 다시 부르지 않는다
            KbRetryTap(0, () => kbField);
            Sleep(2.2f);
            Check(() => KbRetryUp() && WiredMirror().ActiveSource == kbField && WiredMirror().KeyboardRetries == 0, "평소: 장소 이름칸을 누르면 키보드가 바로 뜨고 다시 부르지 않음");
            KbRetryClose("평소");

            // 2) 키보드가 한 번 안 뜸 — 입력줄만 떠 있다가(대표님 사진) 잠깐 뒤 다시 불러 뜬다
            KbRetryTap(1, () => kbField);
            Sleep(0.9f);
            Check(() => !KbRetryUp() && WiredMirror()?.ActiveSource == kbField && KbRetryBarAlpha() > 0.9f && WiredMirror().KeyboardRetries == 0,
                "키보드가 '띄워'를 놓치면 — 입력줄만 뜨고 키보드는 없음 (안드로이드에서 생기던 모습)");
            Shot("kbretry_stuck");
            Sleep(1.4f);
            Check(() => KbRetryUp() && WiredMirror().KeyboardRetries == 1 && WiredMirror().Bar.isFocused && WiredMirror().PlacedBar != null,
                "… 1.5초 안에 안 뜨면 다시 불러 키보드가 뜨고, 입력줄이 키보드 위에 놓임");
            Shot("kbretry_recovered");
            Do(() => { var m = WiredMirror(); if (m != null) m.Bar.text = "다시 뜬 키보드"; });
            Sleep(0.2f);
            Check(() => kbField != null && kbField.text == "다시 뜬 키보드", "… 다시 뜬 키보드로 친 글이 장소 이름칸에 들어감");
            KbRetryClose("다시 뜬 뒤");
            Check(() => kbField != null && kbField.text == "다시 뜬 키보드", "… 닫은 뒤에도 장소 이름칸에 남음");
            Do(() => { if (kbField != null) kbField.text = ""; });

            // 3) 두 번 다시 불러도 안 뜸 — 입력줄은 닫지 않고 기다리다, 입력줄을 누르면 다시 불러 뜬다
            KbRetryTap(3, () => kbField);
            Sleep(4.2f);
            Check(() => !KbRetryUp() && WiredMirror()?.ActiveSource == kbField && WiredMirror().KeyboardRetries == 2 && EditorVirtualKeyboard.DropShows == 0,
                "두 번 다시 불러도 안 뜨면 — 더 부르지 않고 입력줄은 그대로 둠");
            Shot("kbretry_gaveup");
            Finger(() => Center(WiredMirror()?.Bar), new[] { Vector2.zero }, 2, null, null);
            Sleep(1.0f);
            Check(() => KbRetryUp() && WiredMirror().KeyboardRetries == 3 && WiredMirror().Bar.isFocused, "… 입력줄을 누르면 다시 불러 키보드가 뜸");
            Shot("kbretry_bartap");
            Finger(() => Center(WiredMirror()?.Bar), new[] { Vector2.zero }, 2, null, null);
            Sleep(0.8f);
            Check(() => KbRetryUp() && WiredMirror().KeyboardRetries == 3, "… 키보드가 떠 있을 땐 입력줄을 눌러도 다시 부르지 않음");
            KbRetryClose("입력줄을 눌러 뜬 뒤");

            // 4) 3D 모델 추가 이름칸 — 같은 입력줄
            Do(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude)?.SwitchToPanel(1));
            Sleep(0.8f);
            KbRetryTap(1, () => Find("UploadPage/Clip0926/UploadSheet0926/ModelUploadPage/Panel/NameInput")?.GetComponent<InputField>());
            Sleep(2.4f);
            Check(() => KbRetryUp() && WiredMirror()?.ActiveSource != null && WiredMirror().ActiveSource.transform.parent.parent.name == "ModelUploadPage" && WiredMirror().KeyboardRetries == 1,
                "3D 모델 이름칸: 키보드가 한 번 안 떠도 다시 불러 뜸");
            Shot("kbretry_model");
            KbRetryClose("3D 모델");

            Do(() =>
            {
                EditorVirtualKeyboard.DropShows = 0;
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = ptFocusWas;
            });
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.0f);
        }

        // 원래 칸을 손가락으로 누른다 — 그 전에 가상 키보드가 '띄워'를 몇 번 못 들은 척할지 정한다
        private static void KbRetryTap(int drops, Func<InputField> field)
        {
            Do(() => EditorVirtualKeyboard.DropShows = drops);
            Finger(() => Center(field()), new[] { Vector2.zero }, 2, null, null);
        }

        private static bool KbRetryUp() => EditorVirtualKeyboard.CurrentVisibleHeight > 0f;

        private static float KbRetryBarAlpha()
        {
            var m = WiredMirror();
            var g = m != null ? typeof(UploadInputMirror).GetField("mirrorGroup", NPI).GetValue(m) as CanvasGroup : null;
            return g != null ? g.alpha : -1f;
        }

        private static void KbRetryClose(string what)
        {
            Do(() => { var m = WiredMirror(); if (m != null && m.ActiveSource != null) typeof(UploadInputMirror).GetMethod("OnCloseClicked", NPI).Invoke(m, null); });
            Sleep(0.8f);
            Check(() => { var m = WiredMirror(); return m != null && m.ActiveSource == null && !KbRetryUp(); }, what + " → ✓ 로 닫으면 입력줄·키보드가 내려감");
        }

        private static void KbClick() => kbClicks++;
        private static void KbSend() => kbSent++;

        private static ScrollRect CommentList()
        {
            var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include);
            return cm != null && cm.commentContent != null ? cm.commentContent.GetComponentInParent<ScrollRect>() : null;
        }

        private static MobileKeyboardHandler CommentMkh()
        {
            var cm = UnityEngine.Object.FindAnyObjectByType<CommentManager>(FindObjectsInactive.Include);
            if (cm == null || cm.commentInputField == null) return null;
            foreach (var k in UnityEngine.Object.FindObjectsByType<MobileKeyboardHandler>(FindObjectsInactive.Include))
                if (k.targetInputField == cm.commentInputField) return k;
            return null;
        }

        // 기기에서 입력칸을 눌러 키보드가 올라온 것처럼 — 커서 + 키보드 높이
        private static void KbUpFor(Func<MobileKeyboardHandler> handler)
        {
            Do(() =>
            {
                var k = handler();
                var c = CanvasRoot() as RectTransform;
                if (k == null || c == null || k.targetInputField == null) { log.Add("  키보드를 띄울 창 없음"); return; }
                k.targetInputField.Select();
                k.targetInputField.ActivateInputField();
                k.EditorPreviewKeyboard(c.rect.height * 0.40f);   // 에디터 가상 키보드(칸을 고르면 뜬다)와 같은 높이 — 입력줄이 그 위에 놓인다
            });
            Sleep(1.4f);
        }

        private static bool KbUp(MobileKeyboardHandler k) => k != null && k.KeyboardUp && k.targetInputField != null && k.targetInputField.isFocused;
        private static bool KbDown(MobileKeyboardHandler k) => k != null && !k.KeyboardUp && k.targetInputField != null && !k.targetInputField.isFocused && k.targetInputField.enabled;

        private static void KbLeg(Vector2 move)
        {
            kbLegs[0] = move;
            kbLegs[1] = kbLegs[2] = Vector2.zero;
        }

        // 키보드 없을 때 그 칸 자리(rest)까지 내린다
        private static void KbLegTo(Component c, Vector2 rest)
        {
            var now = Center(c);
            KbLeg(now.HasValue && rest != default ? rest - now.Value : new Vector2(0f, -300f));
            log.Add($"  내릴 만큼 {kbLegs[0]}" + (now.HasValue && rest != default ? "" : " (칸 자리를 몰라 기본값)"));
        }

        // at 의 가운데를 눌러 내린 뒤 잠시 누른 채 있다가 뗀다 — 그동안 sheetPanel 의 밀어 닫는 시트가 내려간 만큼을 kbSheet 에
        private static void KbSwipe(Func<Component> at, string shot, string sheetPanel)
        {
            Do(() => kbSheet = 0f);
            Finger(() => Center(at()), kbLegs, 12, () =>
            {
                var sd = sheetPanel != null ? Find(sheetPanel)?.GetComponentInChildren<R0926SwipeDismiss>(true) : null;
                if (sd != null && sheetApplied != null) kbSheet = Mathf.Max(kbSheet, (float)sheetApplied.GetValue(sd));
            }, shot);
            Do(() => { if (sheetPanel != null) log.Add($"  시트가 내려간 만큼 {kbSheet:0.#}"); });
        }

        private static Vector2? Center(Component c)
        {
            var rt = c != null ? c.transform as RectTransform : null;
            if (rt == null || !rt.gameObject.activeInHierarchy) return null;
            return ScreenRect(rt).center;
        }

        private static void KbNameInput()
        {
            kbField = null;
            foreach (var f in UnityEngine.Object.FindObjectsByType<InputField>(FindObjectsInactive.Exclude))
                if (f.name == "NameInput" && f.gameObject.activeInHierarchy && f.transform.parent.parent.name == "CubeUploadPage") { kbField = f; break; }
        }

        private static void KbMirrorOpen(string text)
        {
            Do(() =>
            {
                var m = WiredMirror();
                kbText = text;
                if (m == null || kbField == null) { log.Add("  입력줄 · 장소 이름칸 없음"); return; }
                typeof(UploadInputMirror).GetMethod("ActivateMirrorFor", NPI).Invoke(m, new object[] { kbField });
                m.Bar.text = text;
            });
            Sleep(0.9f);
        }

        private static void KbMirrorClosed(string what)
        {
            Do(() => log.Add("  장소 이름칸 = '" + (kbField != null ? kbField.text : "-") + "' · 보낸 횟수 " + kbSent));
            Check(() =>
            {
                var m = WiredMirror();
                var panel = m != null ? typeof(UploadInputMirror).GetField("mirrorPanel", NPI).GetValue(m) as GameObject : null;
                return m != null && m.ActiveSource == null && panel != null && !panel.activeSelf;
            }, what + " → 입력줄이 닫힘");
            Check(() => kbField != null && kbField.text == kbText, what + " → 쓴 글은 원래 칸(장소 이름)에 남음");
            Check(() => kbSent == 0, what + " → 보내기 칸이어도 보내지 않음");
            Check(() => WiredMirror() != null && WiredMirror().Bar.enabled, what + " → 손을 뗀 뒤 입력줄 칸이 다시 켜짐");
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
            // 막 받은 타일이 아직 나타나는 중 (10-23 — 그동안은 아래 단계도 함께 깔린다)
            Func<bool> fadingNow = () => tiles != null && (bool)typeof(R0926MapTiles).GetField("fading", F).GetValue(tiles);
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
            Wait(() => count("inflight") == 0 && count("cache") > 0 && holes() == 0 && !fadingNow(), 6f, "다시 열기 — 기기 캐시로 채움");
            Check(() => holes() == 0 && oneLevel(), "서버 없이도 다시 열면 기기 캐시로 다 보임");
            Shot("tiles_reopen_disk");
            // 처음 보는 곳 (10-23) — 메모리·기기 캐시 없이 새 자리로: 화면을 덮는 두 단계 위 타일을 가장 먼저 받고, 막 받은 타일은 서서히 나타난다
            int coverCount = -1, coverLevel = -1, sharpLevel = -1;
            bool coverFirst = false;
            float minAlpha = 1f;
            Func<List<RawImage>> pool = () => typeof(R0926MapTiles).GetField("pool", F)?.GetValue(tiles) as List<RawImage>;
            Do(() =>
            {
                tiles.enabled = false;
                wipeDisk();
                R0926MapTiles.EditorBase = "http://127.0.0.1:5098";
                tiles.enabled = true;
                map.EditorPreview(true, zoomAt(15), 35.1587f, 129.1604f);   // 해운대 — 앞에서 안 본 곳
                var w = typeof(R0926MapTiles).GetField("wanted", F)?.GetValue(tiles) as List<(float dist, long key, bool ahead)>;
                coverCount = 0; coverFirst = w != null && w.Count > 0;
                if (w != null)
                {
                    foreach (var e in w) if (!e.ahead && e.dist >= 0f) sharpLevel = Math.Max(sharpLevel, (int)(e.key >> 48));
                    bool other = false;
                    foreach (var e in w)
                    {
                        if (e.dist >= 0f) { other = true; continue; }
                        coverCount++;
                        coverLevel = (int)(e.key >> 48);
                        if (other) coverFirst = false;
                    }
                }
                log.Add("  처음 보는 곳: 자세한 단계 " + sharpLevel + " · 먼저 받는 위 단계 " + coverLevel + " " + coverCount + "장 · 받을 것 " + (w?.Count ?? -1));
            });
            Check(() => coverFirst && coverCount >= 1 && coverCount <= 4 && coverLevel == sharpLevel - 2,
                "처음 보는 곳 — 화면을 덮는 두 단계 위 타일(1~4장)을 가장 먼저 받는다");
            Wait(() =>
            {
                var p = pool();
                if (p != null) foreach (var r in p) if (r != null && r.enabled && r.texture != null) minAlpha = Mathf.Min(minAlpha, r.color.a);
                return count("inflight") == 0 && holes() == 0 && !fadingNow();
            }, 20f, "처음 보는 곳 타일 받기 끝");
            Sleep(0.3f);
            Check(() => minAlpha < 0.99f, "막 받은 타일은 서서히 나타난다 (흐린 칸이 또렷한 칸으로 툭 바뀌지 않게)");
            Do(() => log.Add("  나타나는 중 가장 옅은 타일 " + minAlpha.ToString("0.00")));
            Check(() =>
            {
                var p = pool();
                if (p == null) return false;
                foreach (var r in p) if (r != null && r.enabled && r.color.a < 1f) return false;
                return holes() == 0 && oneLevel() && fetchLevel() == sharpLevel;
            }, "다 받은 뒤엔 자세한 단계 한 겹만 · 반투명으로 남은 칸 없음");
            Shot("tiles_fresh_area");
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
            // 에디터에선 UGUI 가 '지금 게임뷰가 보여 주는 화면 번호'와 캔버스 화면 번호가 다르면 누름을 버린다 — 그때 값을 같이 적는다
            Func<string> disp = () =>
            {
                var p = typeof(Display).GetProperty("activeEditorGameViewTarget", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                return "화면 " + Screen.width + "x" + Screen.height + " · 게임뷰 대상 " + (p != null ? p.GetValue(null) : "?") + " · 프레임 " + Time.frameCount;
            };
            // 그 칸 가운데를 눌렀을 때 맨 위에 걸리는 것
            Func<Button, string> top = b =>
            {
                if (b == null) return "버튼 없음";
                if (!b.gameObject.activeInHierarchy) return b.name + " 꺼짐";
                var r = ScreenRect((RectTransform)b.transform);
                var es = UnityEngine.EventSystems.EventSystem.current;
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = r.center }, hits);
                if (hits.Count == 0) return b.name + " → 아무것도 안 걸림 (누른 자리 " + r.center + " · " + disp() + ")";
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
                var handler = hits.Count == 0 ? null : UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
                if (handler != b.gameObject) log.Add("    (그 순간) " + top(b) + " · " + disp());
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

            // 꾹 눌러 끌기 · 칸마다 x (10-26) — 장소 정보 수정과 같은 손놀림. 든 동안 시트를 아래로 끌어도 시트는 그대로
            Func<List<Image>> ucells = () => typeof(CubeUploadManager).GetField("subPhotoDisplays", F).GetValue(cum) as List<Image>;
            Func<R0926PhotoReorder> ureo = () => typeof(CubeUploadManager).GetField("subReorder", F).GetValue(cum) as R0926PhotoReorder;
            Func<int, Vector2> ugrip = k => { var r = ScreenRect(ucells()[k].rectTransform); return new Vector2(r.xMin + r.width * 0.3f, r.yMin + r.height * 0.3f); };
            List<Texture2D> before = null;
            R0926SwipeDismiss usd = null;
            Vector2 u0 = default;
            Check(() => ureo() != null && typeof(CubeUploadManager).GetField("subPhotoRemoveTemplate", F).GetValue(cum) as Button != null,
                  "연결: 장소 추가 — 꾹 눌러 옮기기 · 칸마다 x");
            Check(() =>
            {
                bool ok = true;
                for (int k = 0; k < 3; k++)
                {
                    var x = ucells()[k].transform.Find("RemoveX0926");
                    ok &= x != null && x.gameObject.activeInHierarchy && rectOf(x).width <= rectOf(ucells()[k].transform).width * 0.55f;
                }
                return ok;
            }, "장소 추가에도 사진마다 오른쪽 위 x (칸의 절반 이하)");
            Do(() =>
            {
                before = new List<Texture2D>(photos());
                usd = Find("UploadPage/UploadSheet0926")?.GetComponent<R0926SwipeDismiss>();
                u0 = ugrip(2);
                ureo().EditorFeed(true, u0);
                if (usd != null) usd.EditorFeed(true, u0);
            });
            Sleep(0.55f);
            Check(() => R0926PhotoReorder.Holding && ureo().EditorGhostShown, "꾹 — 장소 추가에서도 사진이 떠오름");
            Do(() => Diagnose("UploadPage/UploadSheet0926", u0));   // 밀어 닫기가 이 손가락을 받을 수 있었는지 (끌기 받는 곳 없음 + phase Pending 이어야 아래 검사가 뜻이 있다)
            Do(() => { var p = u0 - new Vector2(0f, 120f); ureo().EditorFeed(true, p); if (usd != null) usd.EditorFeed(true, p); });
            Sleep(0.15f);
            Do(() => { var p = u0 - new Vector2(0f, 260f); ureo().EditorFeed(true, p); if (usd != null) usd.EditorFeed(true, p); });
            Sleep(0.25f);
            Shot("photoload_reorder_down");
            Check(() =>
            {
                float applied = usd != null ? (float)sheetApplied.GetValue(usd) : -1f;
                log.Add("  사진을 든 채 260px 아래로: 시트 내려감 " + applied.ToString("0") + " · 사진 " + photos().Count);
                return usd != null && applied < 1f && Active("UploadPage") && R0926PhotoReorder.Holding;
            }, "사진을 든 채 아래로 끌어도 시트는 그대로 (밀어 닫기가 손가락을 놓아준다)");
            Do(() => { var p = ugrip(0); ureo().EditorFeed(true, p); if (usd != null) usd.EditorFeed(true, p); });
            Sleep(0.25f);
            Do(() => { var p = ugrip(0); ureo().EditorFeed(false, p); if (usd != null) usd.EditorFeed(false, p); });
            Sleep(0.4f);
            Do(() => { ureo().EditorFeedEnd(); if (usd != null) usd.EditorFeedEnd(); });
            Shot("photoload_reorder");
            Check(() => photos().Count == 3 && photos()[0] == before[2] && photos()[1] == before[0] && photos()[2] == before[1] && !R0926PhotoReorder.Holding
                        && ucells()[0].sprite != null && ucells()[0].sprite.texture == before[2] && Active("UploadPage"),
                  "끌어 맨 앞으로 — 세 번째 사진이 첫 칸 (올리는 순서도 이대로)");
            Do(() => ucells()[1].transform.Find("RemoveX0926").GetComponent<Button>().onClick.Invoke());
            Sleep(0.3f);
            Check(() => photos().Count == 2 && photos()[0] == before[2] && photos()[1] == before[1] && !ucells()[2].gameObject.activeSelf,
                  "x — 그 사진만 빠지고 뒤가 당겨진다");

            // 5) 열 장 — 칸이 줄어 대표 사진 높이 안에 모두 (이름 칸을 덮지 않는다)
            Do(() =>
            {
                done = false; until = EditorApplication.timeSinceStartup + 30;
                var it = (System.Collections.IEnumerator)typeof(CubeUploadManager).GetMethod("LoadMultipleImagesWithFallback", F)
                    .Invoke(cum, new object[] { paths(8), (Action)(() => done = true) });
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
            Check(() =>
            {
                var grid = pl.transform.Find("SubPhotoContainer");
                float worst = 0f;
                foreach (Transform c in grid)
                {
                    if (!c.gameObject.activeSelf || !c.name.StartsWith("SubPhoto_")) continue;
                    var x = c.Find("RemoveX0926");
                    if (x == null) return false;
                    worst = Mathf.Max(worst, rectOf(x).width / rectOf(c).width);
                }
                log.Add($"  열 장 x 누름 자리 / 칸: 최대 {worst:0.00}");
                return worst > 0.3f && worst <= 0.55f;
            }, "칸이 작아져도 x 가 칸을 덮지 않음 — 칸의 절반 (예전엔 108 고정이라 작은 칸은 통째로 x)");
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

        // ── 10-26 N-C·N-M: 내가 올린 장소 고치기·지우기 ──
        // 대표님 「본인이 올린 것에 한해서는 직접 삭제 … 기존에 있던 것들을 그대로 불러와서 각 사진 우측 위에 (메인사진포함) x … '수정하기'」 ·
        // 「위치가 좀 바뀌었다면 … 수정한 사람이 수정하는 경우 이게 바로 적용, 위치가 바뀌도록」.
        // 시험 서버(5099)의 place_edit 는 버리는 Postgres(55442)를 쓴다 — 장면에 뜬 우팡 장소 두 곳의 번호로 시험 장소를 심고
        // (사진은 MAIN · 1~4 번호 그림), A 는 메모리의 올린 사람을 시험 아이디로 바꿔 '내 장소'로 연다.
        // 남의 장소(B)는 열어 보기만 — 수정 요청(/fix_upload)·삭제 요청은 운영 서버로 가므로 누르지 않는다
        [Serializable] private class DevPlaceRow { public bool exists, pet, main_on_disk, folder_on_disk; public string name, category, lat, lon, main, color; public string[] subs, files; public bool[] subs_on_disk; }
        private const string PeMe = "hunt_test_peme";
        // 시험 서버 /upload 가 마지막으로 받은 것 (api/dev/last_upload)
        [Serializable] private class DevLastUpload { public int id, main, subs; public string username, form_username, name, category, color, lat, lon; public bool has_category, has_color, has_upload_id; }
        private const string NcMe = "hunt_test_ncme";
        [Serializable] private class DevComment { public int id; public string user_id, content; }
        [Serializable] private class DevCommentReport { public string target_id, reporter_id, status, reason, detail; }
        [Serializable] private class DevCommentState { public DevComment[] comments; public DevCommentReport[] reports; }
        [Serializable] private class DevCommentSeed { public bool ok; public int[] ids; }
        // 장소 상세 위 여백 (10-24 대표님 「위쪽으로 여백이 큰데 … 버튼이나 서브사진들 및 각 레이아웃들을 전체적으로 좀 올려야」)
        // 우팡 큐브(사진 있는 것) · 관광공사(정보 글 있는 것)를 열어 첫 장 · 사진 장을 찍고 칸마다 화면 자리를 적는다 — 가로에서도 한 번
        private static void PlanDetailLift()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            DoubleTap3D a = null, t = null;
            Func<DoubleTap3D, bool> open = d => d != null && d.fullscreenCanvasGroup != null && d.fullscreenCanvasGroup.gameObject.activeSelf;
            Func<DoubleTap3D, bool> loaded = d => d != null && (bool)typeof(DoubleTap3D).GetField("photosLoaded", F).GetValue(d);
            Func<DoubleTap3D, int> urls = d => d != null ? ((List<string>)typeof(DoubleTap3D).GetField("imageUrls", F).GetValue(d)).Count : -1;
            Func<DoubleTap3D, string> addr = d => d != null ? typeof(DoubleTap3D).GetField("address", F).GetValue(d) as string : null;
            Action<DoubleTap3D> tap = d => typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", F).Invoke(d, null);
            Action<DoubleTap3D> close = d => typeof(DoubleTap3D).GetMethod("CloseFullscreen", F).Invoke(d, null);

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Wait(() =>
            {
                if (DataManager.Instance == null) return false;
                var map = DataManager.Instance.GetPlaceDataMap();
                foreach (var kv in DataManager.Instance.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy || !map.TryGetValue(kv.Key, out var p) || p.model_type != "cube") continue;
                    var d = kv.Value.GetComponentInChildren<DoubleTap3D>(true);
                    if (urls(d) > 0) { a = d; return true; }
                }
                return false;
            }, 40f, "상세 사진이 있는 우팡 큐브");
            Do(() => HideAddressLine());

            // 1) 우팡 큐브 — 첫 장 · 사진 장
            Do(() => tap(a));
            Wait(() => open(a) && loaded(a), 30f, "우팡 큐브 상세 사진 받음");
            Sleep(0.8f);
            Do(() => DetailRects("우팡 첫 장"));
            Shot("detail_a_first");
            Do(() => a.ShowNextImage());
            Sleep(1.0f);
            Do(() => DetailRects("우팡 다음 장"));
            Shot("detail_a_next");
            Do(() => close(a));
            Sleep(1.2f);

            // 2) 관광공사 — 주소 · 전화 같은 정보 글이 첫 장
            Do(() =>
            {
                var objs = TourAPIManager.Instance != null ? TourAPIManager.Instance.GetSpawnedObjects() : null;
                if (objs != null)
                    foreach (var go in objs.Values)
                    {
                        var d = go != null && go.activeInHierarchy ? go.GetComponentInChildren<DoubleTap3D>(true) : null;
                        if (d != null && !string.IsNullOrEmpty(addr(d))) { t = d; break; }
                    }
                log.Add(t != null ? "  관광공사 " + t.transform.root.name : "  정보 글 있는 관광공사 오브젝트 없음 — 건너뜀");
                if (t != null) tap(t);
            });
            Sleep(1.6f);
            Do(() => { if (t != null) DetailRects("관광공사 첫 장"); });
            Shot("detail_t_first");
            Do(() => { if (t != null) t.ShowNextImage(); });
            Sleep(1.2f);
            Shot("detail_t_next");
            Do(() => { if (t != null) close(t); });
            Sleep(1.2f);

            // 3) 가로 — 사진은 왼쪽 · 글 · 버튼은 오른쪽 열 (넘겨도 사진이 제자리로 들어오는지)
            Do(() => Rotate(true));
            Wait(() => Turned(true), 8f, "가로로 돌림");
            Sleep(1.0f);
            Do(() => tap(a));
            Wait(() => open(a) && loaded(a), 30f, "가로: 상세 열림");
            Sleep(0.8f);
            Do(() => DetailRects("가로 첫 장"));
            Shot("detail_land_first");
            Do(() => a.ShowNextImage());
            Sleep(1.0f);
            Do(() => DetailRects("가로 다음 장"));
            Shot("detail_land_next");
            Do(() => close(a));
            Sleep(1.2f);
            Do(() => Rotate(false));
            Wait(() => Turned(false), 8f, "세로로 되돌림");
            Sleep(0.8f);

            // 4) 세로로 돌아와 다시 — 가로를 다녀와도 세로 자리 그대로
            Do(() => tap(a));
            Wait(() => open(a) && loaded(a), 30f, "세로: 다시 열림");
            Sleep(0.8f);
            Do(() => a.ShowNextImage());
            Sleep(1.0f);
            Do(() => DetailRects("세로로 돌아와 다음 장"));
            Shot("detail_a_back");
            Do(() => close(a));
            Sleep(1.2f);
        }

        // 장소 상세 칸들의 화면 자리 — 화면 위에서 잰 px (위~아래). 글은 글자가 실제로 찍힌 줄 기준
        private static void DetailRects(string label)
        {
            var sa = Screen.safeArea;
            var sb = new System.Text.StringBuilder($"  [{label}] 화면 {Screen.width}x{Screen.height} · 안전영역 위 {Screen.height - sa.yMax:0} 아래 {sa.yMin:0}");
            const string G = "FullScreenPanel/FullScreenGuide/";
            foreach (var n in new[] { G + "TopUIPanel/Info_01", G + "TopUIPanel/Info_02", G + "TopUIPanel/InstgramButton", G + "TopUIPanel/TranslationButton", G + "TopUIPanel/FixButton",
                                      G + "PlaceName", G + "Roman0926", G + "CreatedByText", G + "TourAPI_Description", G + "PlaceInfoTextPanel",
                                      "FullScreenPanel/FullScreenImage", G + "Button_Previous", G + "Button_Close", "FullScreenPanel/CommentPreviewPanel", "Dock0926" })
            {
                var tr = Find(n) as RectTransform;
                if (tr == null || !tr.gameObject.activeInHierarchy) continue;
                var r = ScreenRect(tr);
                var tx = tr.GetComponent<Text>();
                string ink = "";
                if (tx != null && tx.enabled)
                {
                    if (string.IsNullOrEmpty(tx.text)) continue;
                    var gen = tx.cachedTextGenerator;
                    if (gen.vertexCount > 0)
                    {
                        float lo = float.MaxValue, hi = float.MinValue;
                        foreach (var v in gen.verts) { lo = Mathf.Min(lo, v.position.y); hi = Mathf.Max(hi, v.position.y); }
                        float k = tr.lossyScale.y / tx.pixelsPerUnit;
                        var mid = tr.TransformPoint(Vector3.zero).y;
                        ink = $" 글자 {Screen.height - (mid + hi * k):0}~{Screen.height - (mid + lo * k):0}";
                    }
                }
                var im = tr.GetComponent<Image>();
                if (im != null && !im.enabled && tx == null) continue;
                sb.Append($" | {n.Substring(n.LastIndexOf('/') + 1)} {Screen.height - r.yMax:0}~{Screen.height - r.yMin:0}{ink}");
            }
            log.Add(sb.ToString());
        }

        // 확대/축소 표시 (10-24 대표님 「화면확대하거나 축소할 때 발생하는 확대축소 레이아웃도 조금 올려줘. 아이폰의 경우 노치랑 겹치지만 않으면 돼」)
        // 표시를 띄운 채 세로 · 가로에서 찍고 노치 아래 칸들(상태 알약 · 하늘 칩)과 화면 자리를 적는다
        private static void PlanZoomInd()
        {
            ZoomIndicator z = null;
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                z = UnityEngine.Object.FindAnyObjectByType<ZoomIndicator>(FindObjectsInactive.Include);
                log.Add(z != null ? "  확대 표시 " + z.name : "  ZoomIndicator 없음");
            });
            Sleep(1.0f);
            Do(() => ZoomRects("확대 전"));
            Do(() => { if (z != null) z.UpdateZoom(1.5f); });
            Sleep(0.3f);
            Do(() => { if (z != null) z.UpdateZoom(2.0f); ZoomRects("세로 확대"); });
            Shot("zoom_in");
            Do(() => { if (z != null) z.UpdateZoom(1.2f); });
            Sleep(0.3f);
            Do(() => ZoomRects("세로 축소"));
            Shot("zoom_out");

            Do(() => Rotate(true));
            Wait(() => Turned(true), 8f, "가로로 돌림");
            Sleep(1.0f);
            Do(() => { if (z != null) z.UpdateZoom(2.0f); });
            Sleep(0.3f);
            Do(() => ZoomRects("가로 확대"));
            Shot("zoom_land");
            Do(() => Rotate(false));
            Wait(() => Turned(false), 8f, "세로로 되돌림");
            Sleep(0.8f);
            Do(() => { if (z != null) z.Hide(); });
            Sleep(0.6f);
            Do(() => ZoomRects("표시 끈 뒤"));
            Shot("zoom_after");
        }

        // 1.0x 스냅 (10-26 대표님 「1.0x 가 되었을 때 핸드폰에서 진동이 살짝 울리면서 snap … 1.0x 로 사용하도록 유도」)
        // 에디터는 핀치를 못 받으므로 손가락 사이 거리 변화(px)를 직접 넣는다 · 진동은 횟수로 센다
        private static void PlanZoomSnap()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var T = typeof(ARObjectZoomController);
            ARObjectZoomController z = null;
            float b = 0f, speed = 0f, min = 0f, max = 0f;   // b = 1.0x 에 붙는 칸 (px)
            int snaps0 = 0;
            Action<string> call = n => T.GetMethod(n, F).Invoke(z, null);
            Action<float> pinch = px => T.GetMethod("PinchBy", F).Invoke(z, new object[] { px });
            Func<float> zoom = () => z.GetZoomLevel();
            Func<int> snaps = () => ARObjectZoomController.EditorSnaps - snaps0;
            Func<string, bool> say = s => { log.Add($"  {s} → {zoom():0.000}x · 진동 {snaps()}"); return true; };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                z = UnityEngine.Object.FindAnyObjectByType<ARObjectZoomController>(FindObjectsInactive.Include);
                if (z == null) { log.Add("  ARObjectZoomController 없음"); return; }
                z.ResetZoom();
                speed = (float)T.GetField("zoomSpeed", F).GetValue(z);
                min = (float)T.GetField("minZoom", F).GetValue(z);
                max = (float)T.GetField("maxZoom", F).GetValue(z);
                b = (float)T.GetMethod("SnapBand", F).Invoke(z, null) / speed;
                snaps0 = ARObjectZoomController.EditorSnaps;
                log.Add($"  배율 {min}~{max} · 1px 에 {speed} · 1.0x 붙는 칸 ±{b:0}px (화면 {Screen.dpi:0}dpi)");
            });
            Check(() => z != null && b > 0f, "확대/축소 컨트롤러 · 붙는 칸");

            Do(() => { call("SyncRaw"); pinch(b * 0.5f); });
            Check(() => say("칸 절반 벌림") && zoom() == 1f && snaps() == 0, "1.0x 에서 조금 벌려도 1.0x 그대로 (붙어 있음 · 진동 없음)");
            Do(() => pinch(b * 0.6f));
            Check(() => say("칸을 넘김") && zoom() > 1f && zoom() < 1.05f, "칸을 넘으면 1.0x 에서 이어서 커짐 (튀지 않음)");
            Do(() => pinch(200f));
            Check(() => say("200px 더") && zoom() > 1.5f, "계속 벌리면 확대");
            Do(() => pinch(-(200f + b * 0.3f)));
            Check(() => say("다시 좁힘") && zoom() == 1f && snaps() == 1, "줄이다 1.0x 에 닿으면 붙고 진동 한 번");
            Do(() => ZoomRects("1.0x 붙음"));
            Shot("snap_1x");
            Do(() => { pinch(b * 0.25f); pinch(-b * 0.1f); pinch(b * 0.1f); pinch(-b * 0.1f); });
            Check(() => say("칸 경계에서 떨림") && zoom() == 1f && snaps() == 1, "칸 경계에서 손이 떨려도 진동은 더 안 울림");
            Do(() => pinch(-b * 2.5f));
            Check(() => say("더 좁힘") && zoom() < 0.95f, "더 좁히면 1.0x 아래로 축소");
            Do(() => pinch(b * 0.7f));
            Check(() => say("다시 벌림") && zoom() == 1f && snaps() == 2, "아래에서 올라와도 1.0x 에 붙고 진동");

            Do(() => { call("EndPinch"); call("SyncRaw"); pinch(b * 1.04f); });
            Check(() => say("칸을 살짝 넘긴 채") && zoom() > 1f && zoom() < 1.05f, "칸을 살짝 넘김 ('1.0x' 로 보임)");
            Do(() => call("EndPinch"));
            Check(() => say("손 뗌") && zoom() == 1f && snaps() == 2, "'1.0x' 로 보이는 채 손을 떼면 정확히 1.0x (진동 없이)");
            Do(() => { call("SyncRaw"); pinch(b + 0.3f / speed); pinch(-0.27f / speed); });
            Check(() => say("1.3x 에서 1.03x 까지") && Mathf.Abs(zoom() - 1.03f) < 0.005f, "1.3x 에서 1.0x 바로 앞까지 줄임");
            Do(() => call("EndPinch"));
            Check(() => say("손 뗌") && zoom() == 1f && snaps() == 3, "1.0x 바로 앞에서 손을 떼도 1.0x 로 붙고 진동");

            Do(() => { call("SyncRaw"); pinch(1e6f); });
            Check(() => say("끝까지 벌림") && Mathf.Abs(zoom() - max) < 0.001f, "끝까지 벌리면 최대 배율에서 멈춤");
            Do(() => pinch(-1e6f));
            Check(() => say("끝까지 좁힘") && Mathf.Abs(zoom() - min) < 0.001f, "끝까지 좁히면 최소 배율에서 멈춤");
            Do(() => { call("EndPinch"); z.ResetZoom(); });
            Check(() => say("되돌림") && zoom() == 1f, "되돌리면 1.0x");
        }

        private static void ZoomRects(string label)
        {
            var sa = Screen.safeArea;
            var sb = new System.Text.StringBuilder($"  [{label}] 화면 {Screen.width}x{Screen.height} · 안전영역 위 {Screen.height - sa.yMax:0} 아래 {sa.yMin:0}");
            var z = UnityEngine.Object.FindAnyObjectByType<ZoomIndicator>(FindObjectsInactive.Include);
            var oc = UnityEngine.Object.FindAnyObjectByType<ObjectCountUI>(FindObjectsInactive.Include);
            var parts = new List<Transform>();
            if (z != null) foreach (Transform c in z.transform) parts.Add(c);
            if (oc != null) foreach (var n in new[] { "Back", "Text", "Ic0926" }) { var b = oc.transform.Find(n); if (b != null) parts.Add(b); }
            var chip = Find("SkyChip0926"); if (chip != null) parts.Add(chip);
            foreach (var t in parts)
            {
                var rt = t as RectTransform;
                if (rt == null || !rt.gameObject.activeInHierarchy) { sb.Append($" | {t.name} 꺼짐"); continue; }
                var r = ScreenRect(rt);
                var tx = rt.GetComponent<Text>();
                float a = 1f;   // 실제로 보이는 정도 — 위로 올라가며 CanvasGroup 알파를 곱한다
                for (var p = rt.transform; p != null; p = p.parent)
                {
                    var g = p.GetComponent<CanvasGroup>();
                    if (g == null || !g.enabled) continue;
                    a *= g.alpha;
                    if (g.ignoreParentGroups) break;
                }
                var hw = rt.GetComponent<R0926HideWhile>();
                var own = hw != null ? hw.GetComponent<CanvasGroup>() : null;
                sb.Append($" | {t.name} {Screen.height - r.yMax:0}~{Screen.height - r.yMin:0} (x {r.xMin:0}~{r.xMax:0}){(tx != null ? " '" + tx.text + "'" : "")} 보임 {a:0.00}{(own != null ? $" (숨김장치 {own.alpha:0.00})" : "")}");
            }
            log.Add(sb.ToString());
        }

        // N-L (10-26 대표님) — 좌표는 소수 6자리로 저장·작동, 화면엔 4자리.
        // 장소·3D 올리기 · 좌표 줄 · 보물찾기가 같은 자리(GeoHere)를 쓰고, 내려받은 장소는 double 로 읽어 AR 자리까지 cm 로 맞는다
        private static void PlanCoords()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Vector3 was = Vector3.zero;
            CubeUploadManager cum = null;
            ModelUploadManager mum = null;
            Func<object, string, double> dv = (o, f) => (double)o.GetType().GetField(f, F).GetValue(o);
            Func<object, string> inputOf = o => (o.GetType().GetField("locationInput", F).GetValue(o) as InputField)?.text;

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                var v = VirtualLocation.Instance;
                was = new Vector3(v.Latitude, v.Longitude, v.Altitude);
                v.SetCoordinates(36.636123f, 126.828045f, 30.5f);   // float 그대로 F6 이면 경도가 126.828000 으로 잘리는 자리
                Click("Dock0926/PlusButton");
            });
            Sleep(1.6f);   // 좌표 줄은 1초마다
            Do(() =>
            {
                cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                typeof(CubeUploadManager).GetMethod("UpdateLocationDisplay", F).Invoke(cum, null);
                typeof(ModelUploadManager).GetMethod("UpdateLocationDisplay", F).Invoke(mum, null);
            });
            Shot("coords_upload");

            Check(() =>
            {
                var v = VirtualLocation.Instance;
                double lat = dv(cum, "gpsLat"), lon = dv(cum, "gpsLon");
                log.Add("  장소 올리기가 보낼 값: " + GeoHere.Save(lat) + ", " + GeoHere.Save(lon)
                        + "  (예전처럼 float 로 F6: " + ((float)lat).ToString("F6", inv) + ", " + ((float)lon).ToString("F6", inv) + ")");
                return lat == (double)v.Latitude && lon == (double)v.Longitude
                       && GeoHere.Save(lat) == ((double)v.Latitude).ToString("F6", inv) && GeoHere.Save(lon) == ((double)v.Longitude).ToString("F6", inv)
                       && GeoHere.Save(lon).Split('.')[1].Length == 6;
            }, "장소 올리기 — 위치를 double 그대로 들고 소수 6자리로 보낸다");
            Check(() =>
            {
                var v = VirtualLocation.Instance;
                return dv(mum, "gpsLat") == (double)v.Latitude && dv(mum, "gpsLon") == (double)v.Longitude && dv(mum, "gpsAlt") == (double)v.Altitude;
            }, "3D 올리기도 같은 값 (double)");
            Check(() =>
            {
                log.Add("  입력칸(장소): " + inputOf(cum) + " · (3D): " + inputOf(mum));
                return inputOf(cum) == "Lat:36.6361,Lon:126.8280,Alt:30.50" && inputOf(mum) == inputOf(cum);
            }, "올리기 창의 위치 값 — 화면은 소수 4자리");
            Check(() =>
            {
                var lines = UnityEngine.Object.FindObjectsByType<R0926CoordLine>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                var t = lines.Length > 0 ? typeof(R0926CoordLine).GetField("text", F).GetValue(lines[0]) as Text : null;
                log.Add("  좌표 줄: " + (t != null ? t.text : "-") + " (켜진 줄 " + lines.Length + ")");
                return t != null && t.text == "36.6361,  126.8280";
            }, "좌표 줄 — 저장되는 것과 같은 자리를 소수 4자리로 (예전엔 GPS 를 5자리)");
            Check(() =>
            {
                var m = typeof(R0926HuntCard).GetMethod("TryLocation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var args = new object[] { 0.0, 0.0, 0f };
                bool ok = m != null && (bool)m.Invoke(null, args);
                double lat = (double)args[0], lon = (double)args[1];
                log.Add("  보물찾기 위치: " + lat.ToString("R", inv) + ", " + lon.ToString("R", inv) + " · 정확도 " + args[2] + "m");
                return ok && lat == Math.Round((double)VirtualLocation.Instance.Latitude, 6) && lon == Math.Round((double)VirtualLocation.Instance.Longitude, 6);
            }, "보물찾기 — 같은 자리(GeoHere)를 소수 6자리로 (숨긴 자리 = AR 보물 자리)");
            Check(() =>
            {
                const string J = "{\"id\":1,\"latitude\":36.636109,\"longitude\":126.828045,\"altitude\":30.25}";
                var p = Newtonsoft.Json.JsonConvert.DeserializeObject<PlaceData>(J);
                var l = Newtonsoft.Json.JsonConvert.DeserializeObject<LightPlaceData>(J);
                var tp = Newtonsoft.Json.JsonConvert.DeserializeObject<TourPlaceData>("{\"mapx\":126.828045,\"mapy\":36.636109}");
                var go = new GameObject("CoordProbe0926");
                go.AddComponent<CustomARGeospatialCreatorAnchor>().SetCoordinatesAndCreateAnchor(p.latitude, p.longitude, p.altitude);
                Vector3 pos = go.transform.position;
                UnityEngine.Object.Destroy(go);
                // 에디터 앵커는 (36.6361, 126.8280) 에서 몇 m 인지로 놓인다 — 북 0.000009° = 1.002m · 동 0.000045° = 4.02m
                double wantZ = (36.636109 - 36.6361) * 111319.9;
                double wantX = (126.828045 - 126.8280) * 111319.9 * Math.Cos(36.6361 * (Math.PI / 180.0));
                log.Add($"  36.636109, 126.828045 → AR 자리 북 {pos.z:0.000}m (계산 {wantZ:0.000}) · 동 {pos.x:0.000}m (계산 {wantX:0.000})"
                        + $" — float 였다면 북 {((double)36.636109f - 36.6361) * 111319.9:0.000}m · 동 {((double)126.828045f - 126.8280) * 111319.9 * Math.Cos(36.6361 * (Math.PI / 180.0)):0.000}m");
                return p.latitude == 36.636109 && p.longitude == 126.828045 && p.altitude == 30.25
                       && l.latitude == 36.636109 && l.longitude == 126.828045 && tp.mapx == 126.828045 && tp.mapy == 36.636109
                       && Math.Abs(pos.z - wantZ) < 0.005 && Math.Abs(pos.x - wantX) < 0.005;
            }, "내려받은 장소 — 좌표를 double 로 읽어 AR 자리도 cm 까지 (예전 float 은 위도 0.4m·경도 0.7m 단위)");
            Do(() => VirtualLocation.Instance.SetCoordinates(was.x, was.y, was.z));
        }

        private static void PlanPlaceEdit()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const string More = "FullScreenPanel/FullScreenGuide/TopUIPanel/FixButton";
            const string Menu = "FullScreenPanel/FullScreenGuide/FixButtonPanel/MoreSheet0926/Tiles0926/";
            const string Page = "Fixpage/FixUploadPage/";
            const string NameA = "시험 가게", NameA2 = "시험 가게 고침", NameB = "남의 가게";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            CubeDataFixManager fix = null;
            RemoveRequest rr = null;
            DoubleTap3D tour = null;
            int idA = -1, idB = -1;
            double seedLat = 0, seedLon = 0;
            Vector2 here = Vector2.zero;
            DevPlaceRow row = null;
            Func<string, object> fv = n => typeof(CubeDataFixManager).GetField(n, F).GetValue(fix);
            Func<string, bool> shown = n =>
            {
                var o = fv(n);
                var go = o is Component c && c != null ? c.gameObject : o as GameObject;
                return go != null && go.activeInHierarchy;
            };
            Func<string, R0926LocalizedText> rrl = n => typeof(RemoveRequest).GetField(n, F).GetValue(rr) as R0926LocalizedText;
            Func<R0926LocalizedText, string> said = l => l != null ? l.GetComponent<Text>()?.text : null;
            Func<string, string> scene = n =>
            {
                var w = (string[])typeof(RemoveRequest).GetField(n, F).GetValue(rr);
                return w != null ? R0926LocalizedText.Say(w[0], w[1], w[2], w[3], w[4]) : null;
            };
            Func<List<string>> keeps = () => (List<string>)fv("subKeepPaths");
            Func<List<Texture2D>> subs = () => (List<Texture2D>)fv("subPhotos");
            Func<List<Image>> cells = () => (List<Image>)fv("subPhotoDisplays");
            Func<bool> loading = () => (bool)fv("isProcessing");
            Func<string> title = () => Find(Page + "TITLE")?.GetComponent<Text>()?.text;
            Func<string> submit = () => Find(Page + "Panel/SubmitButton/Label0926")?.GetComponent<Text>()?.text;
            Func<int, string> modeTitle = k =>
            {
                var loc = fv("localizer");
                var lang = (string)typeof(FixPlaceLocalizer).GetField("currentLanguage", F).GetValue(loc);
                var t = (Dictionary<string, string[]>)typeof(FixPlaceLocalizer).GetField("modeTitles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
                return (t.ContainsKey(lang) ? t[lang] : t["en"])[k];
            };
            Func<int, GameObject> spawned = id => DataManager.Instance != null && DataManager.Instance.GetSpawnedObjects().TryGetValue(id, out var go) ? go : null;
            Func<int, DoubleTap3D> dtOf = id => spawned(id) != null ? spawned(id).GetComponentInChildren<DoubleTap3D>(true) : null;
            Func<int, PlaceData> placeOf = id => DataManager.Instance != null && DataManager.Instance.GetPlaceDataMap().TryGetValue(id, out var p) ? p : null;
            Func<DoubleTap3D, bool> open = d => d != null && d.fullscreenCanvasGroup != null && d.fullscreenCanvasGroup.gameObject.activeSelf;
            Action<DoubleTap3D> tap = d => typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", F).Invoke(d, null);
            Func<int, string> photo = k => "uploads/pe_" + idA + "/" + (k == 0 ? "main.jpg" : "sub_" + k + ".jpg");
            Func<int[], string> order = ks => string.Join("|", Array.ConvertAll(ks, k => photo(k)));
            Func<int, DevPlaceRow> rowOf = id =>
            {
                string r = DevGet("", "/api/dev/place_row/" + id);
                return string.IsNullOrEmpty(r) ? null : JsonUtility.FromJson<DevPlaceRow>(r);
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
                SessionState.SetBool(LoginReadyKey, true);   // 플레이를 끄면 덜어 둔 로그인 저장값을 되돌린다
                CubeDataFixManager.EditorBase = "http://127.0.0.1:5099";
                PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, LocalTestLogin + PeMe.Substring("hunt_test_".Length));
                SfLogin(true, PeMe);
                fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                rr = UnityEngine.Object.FindAnyObjectByType<RemoveRequest>(FindObjectsInactive.Include);
            });
            Check(() => fix != null && rr != null && LoginManager.Instance.IsLoggedIn && LoginManager.Instance.CurrentUsername == PeMe, "준비: 시험 아이디 (로그인 이벤트 없이) · 장소 고치기는 시험 서버로");
            Check(() =>
            {
                var miss = new List<string>();
                foreach (var n in new[] { "openButton", "mainPhotoRemove", "subPhotoRemoveTemplate", "descriptionLabel", "locationLabel", "locationRow",
                                          "locationCoords", "locationNote", "moveHereToggle", "deletePlaceButton", "localizer", "removeRequest", "submitLabel" })
                    if (fv(n) == null || fv(n).Equals(null)) miss.Add(n);
                foreach (var n in new[] { "menuLabel", "dialogText", "yesLabel" })
                    if (rrl(n) == null) miss.Add("RemoveRequest." + n);
                log.Add("  연결 빠짐: " + (miss.Count == 0 ? "없음" : string.Join(", ", miss)));
                return miss.Count == 0;
            }, "연결: 정보 수정 칸 · 사진 x · 위치 줄 · 옮기기 · 삭제 · 메뉴·묻는 글·단추 글자");

            Wait(() =>
            {
                if (DataManager.Instance == null) return false;
                var map = DataManager.Instance.GetPlaceDataMap();
                idA = idB = -1;
                foreach (var kv in DataManager.Instance.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy || !map.ContainsKey(kv.Key) || dtOf(kv.Key) == null) continue;
                    if (idA < 0) idA = kv.Key; else { idB = kv.Key; break; }
                }
                return idB > 0;
            }, 40f, "장면에 뜬 우팡 장소 둘");
            Do(() =>
            {
                // A 는 지금 위치에서 북쪽으로 35m 쯤 — '여기로 옮기기' 를 켜고 저장하면 지금 위치(소수 6자리)로
                seedLat = Math.Round(VirtualLocation.Instance.Latitude + 0.000315, 6);
                seedLon = Math.Round((double)VirtualLocation.Instance.Longitude, 6);
                string at = ",\"lat\":" + seedLat.ToString("F6", inv) + ",\"lon\":" + seedLon.ToString("F6", inv) + ",\"alt\":30";
                string a = DevPost("", "/api/dev/place_seed", "{\"id\":" + idA + ",\"name\":\"" + NameA + "\",\"category\":\"shop\"" + at + ",\"subs\":4,\"owner\":\"peme\",\"pet\":true,\"insta\":\"pe_test\"}");
                string b = DevPost("", "/api/dev/place_seed", "{\"id\":" + idB + ",\"name\":\"" + NameB + "\",\"category\":\"food\"" + at + ",\"subs\":2,\"owner\":\"other\"}");
                placeOf(idA).username = PeMe;   // 메모리만 — 앱은 장소를 저장하지 않는다
                log.Add($"  A {idA} (내 장소로) · B {idB} (남의 장소) — 시험 장소 {(Has(a, "\"ok\":true") && Has(b, "\"ok\":true") ? "심음" : "못 심음")}");
            });

            // 1) 내 장소 A — '···' 는 '삭제' · 정보 수정은 지금 값 그대로 (사진은 올린 순서 · 칸마다 x)
            Do(() => tap(dtOf(idA)));
            Wait(() => open(dtOf(idA)), 10f, "A 상세 열림");
            Do(() => Click(More));
            Sleep(0.8f);
            Shot("placeedit_menu_mine");
            Check(() => said(rrl("menuLabel")) == R0926LocalizedText.Say("삭제", "Delete", "削除", "删除", "Eliminar"), "내 장소의 '···' — '삭제 요청' 대신 '삭제'");
            Do(() => Click(Menu + "FixPlaceButton"));
            Wait(() => Active("Fixpage") && !loading() && subs().Count == 4 && fv("mainPhoto") != null, 20f, "A 고치기 화면 — 서버 값 받음");
            Sleep(0.8f);
            Shot("placeedit_mine_prefill");
            Check(() =>
            {
                string name = ((InputField)fv("nameInput")).text, insta = ((InputField)fv("instagramIDInput")).text;
                int cat = (int)fv("currentCategoryIndex");
                bool pet = ((Toggle)fv("petFriendlyToggle")).isOn;
                log.Add($"  이름 '{name}' · 분류 {cat} · 반려견 {pet} · 인스타 '{insta}' · 대표 {fv("mainKeepPath")} · 추가 {string.Join(", ", keeps())}");
                return name == NameA && cat == 1 && pet && insta == "pe_test" && (string)fv("mainKeepPath") == photo(0) && string.Join("|", keeps()) == order(new[] { 1, 2, 3, 4 });
            }, "지금 값 그대로 — 이름·분류(샵)·스위치·인스타 · 사진은 올린 순서 (대표 + 1·2·3·4)");
            Check(() =>
            {
                var c = cells();
                bool ok = c.Count > 4;
                for (int i = 0; i < c.Count; i++)
                {
                    bool want = i < 4;
                    var x = c[i].transform.Find("RemoveX0926");
                    ok &= c[i].gameObject.activeSelf == want
                          && (!want || (c[i].sprite != null && c[i].sprite.texture == subs()[i] && x != null && x.gameObject.activeInHierarchy));
                }
                return ok && shown("mainPhotoRemove") && ((Image)fv("mainPhotoDisplay")).gameObject.activeInHierarchy;
            }, "사진 칸마다 오른쪽 위 x (대표 사진 포함) · 칸 순서 = 사진 순서");
            Check(() => title() == modeTitle(0) && submit() == R0926LocalizedText.Say("수정하기", "Save changes", "保存する", "保存修改", "Guardar cambios")
                        && shown("locationLabel") && shown("locationRow") && shown("deletePlaceButton") && !shown("descriptionInput") && !shown("descriptionLabel"),
                  "내 장소 — 제목 '" + "내 장소 수정" + "' · 위치 줄 · '삭제' · 수정할 내용 칸 없음 · 단추 '수정하기'");
            Check(() =>
            {
                string coords = ((Text)fv("locationCoords")).text, note = ((Text)fv("locationNote")).text;
                log.Add($"  위치 줄: {coords} · {note}");
                return coords == seedLat.ToString("F4", inv) + ", " + seedLon.ToString("F4", inv)
                       && note == R0926LocalizedText.Say("지금 내 위치에서 35m", "35m from you", "現在地から35m", "距你 35m", "A 35m de ti");
            }, "위치 줄 — 장소 좌표(보이는 건 소수 4자리) · 지금 내 위치에서 35m");

            // 고치기 — 2번 사진 x · 이름 · 분류(카페) · 반려견 끔 · 여기로 옮기기
            Do(() =>
            {
                cells()[1].transform.Find("RemoveX0926").GetComponent<Button>().onClick.Invoke();
                ((InputField)fv("nameInput")).text = NameA2;
                Click(Page + "Panel/Chips0926/Chip_cafe");
                ((Toggle)fv("petFriendlyToggle")).isOn = false;
                ((Toggle)fv("moveHereToggle")).isOn = true;
            });
            Sleep(0.6f);
            Shot("placeedit_mine_edit");
            Check(() => string.Join("|", keeps()) == order(new[] { 1, 3, 4 }) && subs().Count == 3 && cells()[2].gameObject.activeSelf && !cells()[3].gameObject.activeSelf
                        && cells()[1].sprite != null && cells()[1].sprite.texture == subs()[1],
                  "2번 사진 x — 그 사진만 빠지고 뒤가 당겨진다 (1·3·4)");

            // 꾹 눌러 끌어 순서 바꾸기 (10-26 대표님 「사진들을 옮겨서 … 보이는 순서를 조절」) — 맨 뒤(4)를 맨 앞으로 → 4·1·3
            Func<R0926PhotoReorder> reo = () => (R0926PhotoReorder)fv("subReorder");
            Func<int, Vector2> grip = k => { var r = ScreenRect(cells()[k].rectTransform); return new Vector2(r.xMin + r.width * 0.3f, r.yMin + r.height * 0.3f); };   // 사진 왼쪽 아래 (x 를 피해)
            Check(() => reo() != null, "연결: 추가 사진 꾹 눌러 옮기기");
            Do(() => reo().EditorFeed(true, grip(2)));
            Sleep(0.05f);
            Do(() => reo().EditorFeed(true, grip(2) + new Vector2(40f, 0f)));
            Sleep(0.5f);
            Check(() => !R0926PhotoReorder.Holding && !reo().EditorGhostShown && string.Join("|", keeps()) == order(new[] { 1, 3, 4 }),
                  "누르자마자 움직이면 들지 않음 — 시트 끌기·넘기기 몫");
            Do(() => reo().EditorFeed(false, grip(2)));
            Sleep(0.1f);
            Do(() => reo().EditorFeed(true, grip(2)));
            Sleep(0.15f);
            Check(() => !R0926PhotoReorder.Holding && !reo().EditorGhostShown, "0.15초 — 아직 들리지 않음");
            Sleep(0.4f);
            Shot("placeedit_reorder_lift");
            Check(() => R0926PhotoReorder.Holding && reo().EditorGhostShown && reo().EditorDimmed == 2 && cells()[2].color.a < 0.5f
                        && !cells()[2].transform.Find("RemoveX0926").gameObject.activeSelf && cells()[1].transform.Find("RemoveX0926").gameObject.activeSelf,
                  "꾹 누르면 사진이 떠오르고 그 자리는 흐리게 비움 (그 칸 x 만 감춤)");
            Do(() => reo().EditorFeed(true, grip(1)));
            Sleep(0.25f);
            Check(() => string.Join("|", keeps()) == order(new[] { 1, 4, 3 }) && reo().EditorDimmed == 1, "가운데 칸 위로 — 4 가 가운데 (1·4·3)");
            Do(() => reo().EditorFeed(true, grip(0)));
            Sleep(0.25f);
            Shot("placeedit_reorder_drag");
            Check(() => string.Join("|", keeps()) == order(new[] { 4, 1, 3 }) && reo().EditorDimmed == 0, "맨 앞 칸 위로 — 4·1·3");
            Do(() => reo().EditorFeed(false, grip(0)));
            Sleep(0.4f);
            Do(() => reo().EditorFeedEnd());
            Shot("placeedit_reorder_done");
            Check(() =>
            {
                bool ok = !R0926PhotoReorder.Holding && !reo().EditorGhostShown && reo().EditorDimmed == -1;
                for (int k = 0; k < 3; k++)
                    ok &= cells()[k].color.a > 0.99f && cells()[k].transform.Find("RemoveX0926").gameObject.activeSelf
                          && cells()[k].sprite != null && cells()[k].sprite.texture == subs()[k];
                return ok;
            }, "손을 떼면 그 칸에 내려앉음 — 흐림·x 되돌림 · 칸 그림 = 사진 순서");
            Check(() => (string)fv("selectedCategory") == "cafe", "분류 칩 → 카페");
            Check(() => ((Text)fv("locationNote")).text == R0926LocalizedText.Say("저장하면 지금 위치로 옮겨요 · 35m", "Saving moves it to where you are · 35m", "保存すると現在地に移動します · 35m", "保存后移到你现在的位置 · 35m", "Al guardar se moverá a tu ubicación · 35m"),
                  "'여기로 옮기기' 를 켜면 — '저장하면 지금 위치로 옮겨요 · 35m'");
            Check(() =>
            {
                var fl = typeof(CubeDataFixManager).GetField("placeLat", F);
                var refresh = typeof(CubeDataFixManager).GetMethod("RefreshLocation", F);
                double was = (double)fl.GetValue(fix);
                fl.SetValue(fix, was + 0.05);
                refresh.Invoke(fix, new object[] { true });
                var n = (Text)fv("locationNote");
                string far = n.text;
                bool red = n.color.r > 0.9f && n.color.g < 0.5f;
                fl.SetValue(fix, was);
                refresh.Invoke(fix, new object[] { true });
                log.Add("  5.6km 떨어진 장소였다면: " + far + (red ? " (빨강)" : " (색 그대로)"));
                return red && far.Contains("km");
            }, "1km 넘게 옮기게 되면 빨간 글로 한 번 더 알린다");

            Do(() =>
            {
                here = new Vector2(VirtualLocation.Instance.Latitude, VirtualLocation.Instance.Longitude);
                Click(Page + "Panel/SubmitButton");
            });
            Wait(() => !Active("Fixpage"), 20f, "'수정하기' → 저장되고 창 닫힘");
            Sleep(0.4f);
            Shot("placeedit_saved");
            Do(() => row = rowOf(idA));
            Check(() =>
            {
                if (row == null || !row.exists) return false;
                log.Add($"  서버: '{row.name}' · {row.category} · 반려견 {row.pet} · 대표 {row.main} · 추가 {string.Join(", ", row.subs)} · 폴더 {string.Join(", ", row.files)} · {row.lat},{row.lon}");
                return row.name == NameA2 && row.category == "cafe" && !row.pet && row.main == photo(0) && row.main_on_disk
                       && string.Join("|", row.subs) == order(new[] { 4, 1, 3 }) && Array.TrueForAll(row.subs_on_disk, x => x)
                       && Array.IndexOf(row.files, "sub_2.jpg") < 0;
            }, "서버에 바로 반영 — 이름·분류·스위치 · 사진은 옮긴 순서(4·1·3) 그대로 · 뺀 2번 파일은 지움");
            Check(() => row != null && row.lat == ((double)here.x).ToString("F6", inv) && row.lon == ((double)here.y).ToString("F6", inv),
                  "위치 — 지금 위치로 옮겨짐 (소수 6자리 그대로)");
            Check(() =>
            {
                var p = placeOf(idA);
                return p != null && p.name == NameA2 && p.category == "cafe" && Math.Abs(p.latitude - here.x) < 1e-5 && Math.Abs(p.longitude - here.y) < 1e-5;
            }, "앱에도 바로 — 이 장소의 이름·분류·좌표");
            Check(() =>
            {
                var p = placeOf(idA);
                return p != null && row != null && p.latitude.ToString("F6", inv) == row.lat && p.longitude.ToString("F6", inv) == row.lon;
            }, "앱 좌표도 서버와 같은 소수 6자리 (double — 예전 float 은 경도가 0.7m 단위로 끊겼다)");

            // 2) 다시 열면 고친 값 그대로 → 카드의 '삭제' 로 지우기
            Do(() => tap(dtOf(idA)));
            Wait(() => open(dtOf(idA)), 10f, "A 다시 상세");
            Do(() => Click(More));
            Sleep(0.6f);
            Do(() => Click(Menu + "FixPlaceButton"));
            Wait(() => Active("Fixpage") && !loading() && subs().Count == 3 && fv("mainPhoto") != null, 20f, "A 다시 고치기 — 고친 값");
            Sleep(0.5f);
            Shot("placeedit_reopen");
            Check(() => ((InputField)fv("nameInput")).text == NameA2 && (int)fv("currentCategoryIndex") == 3 && string.Join("|", keeps()) == order(new[] { 4, 1, 3 })
                        && ((Text)fv("locationCoords")).text == ((double)here.x).ToString("F4", inv) + ", " + ((double)here.y).ToString("F4", inv),
                  "다시 열면 고친 값 그대로 — 이름·카페·사진 4·1·3·옮긴 좌표");

            // 마지막 한 장은 빼지 않는다 — 사진이 있던 장소 (10-26 대표님 「최소한 한장의 서브사진이 있도록 유지」)
            Func<string> warn = () => { var w = (GameObject)fv("warningObj"); return w != null && w.activeInHierarchy ? w.GetComponentInChildren<Text>()?.text : null; };
            Do(() =>
            {
                cells()[2].transform.Find("RemoveX0926").GetComponent<Button>().onClick.Invoke();
                cells()[1].transform.Find("RemoveX0926").GetComponent<Button>().onClick.Invoke();
            });
            Sleep(0.3f);
            Check(() => subs().Count == 1 && string.Join("|", keeps()) == order(new[] { 4 }), "x 두 번 — 4 한 장만 남음");
            Do(() => cells()[0].transform.Find("RemoveX0926").GetComponent<Button>().onClick.Invoke());
            Sleep(0.3f);
            Shot("placeedit_keep_one");
            Check(() =>
            {
                log.Add("  마지막 한 장 x: " + subs().Count + "장 · 알림 '" + warn() + "'");
                return subs().Count == 1 && string.Join("|", keeps()) == order(new[] { 4 }) && cells()[0].gameObject.activeSelf
                       && warn() == R0926LocalizedText.Say("추가 사진은 한 장 이상 있어야 해요 — 새 사진을 먼저 넣고 빼 주세요", "Keep at least one extra photo — add a new one first, then remove this",
                           "追加写真は1枚以上必要です — 先に新しい写真を入れてから外してください", "至少要保留一张附加照片 — 请先添加新照片再删除", "Debe quedar al menos una foto adicional — añade otra antes de quitar esta");
            }, "마지막 한 장의 x — 빼지 않고 '추가 사진은 한 장 이상 있어야 해요 — 새 사진을 먼저 넣고 빼 주세요'");
            Do(() => typeof(CubeDataFixManager).GetMethod("ResetSubPhotos", F).Invoke(fix, null));
            Sleep(0.3f);
            Check(() => subs().Count == 0, "'초기화' — 추가 사진을 모두 비움 (새로 넣으려고)");
            Do(() => Click(Page + "Panel/SubmitButton"));
            Sleep(0.8f);
            Shot("placeedit_need_one");
            Do(() => row = rowOf(idA));
            Check(() =>
            {
                log.Add("  0장으로 '수정하기': 알림 '" + warn() + "' · 서버 추가 사진 " + (row != null ? string.Join(", ", row.subs) : "-"));
                return Active("Fixpage") && !loading()
                       && warn() == R0926LocalizedText.Say("추가 사진을 한 장 이상 넣어 주세요", "Add at least one extra photo", "追加写真を1枚以上入れてください", "请至少添加一张附加照片", "Añade al menos una foto adicional")
                       && row != null && string.Join("|", row.subs) == order(new[] { 4, 1, 3 });
            }, "추가 사진 0장으로 '수정하기' — 저장하지 않고 '추가 사진을 한 장 이상 넣어 주세요' (서버 그대로)");
            Do(() => ((Button)fv("deletePlaceButton")).onClick.Invoke());
            Wait(() => Active("RemoveRequestPanel"), 5f, "지울까 묻는 창");
            Sleep(0.5f);
            Shot("placeedit_delete_ask");
            Check(() => said(rrl("dialogText")) == R0926LocalizedText.Say("이 장소를 지울까요?\n사진과 댓글도 함께 지워져요", "Delete this place?\nIts photos and comments go too", "この場所を削除しますか？\n写真とコメントも消えます", "要删除这个地点吗？\n照片和评论也会一起删除", "¿Eliminar este lugar?\nTambién se borran sus fotos y comentarios")
                        && said(rrl("yesLabel")) == R0926LocalizedText.Say("지우기", "Delete", "削除する", "删除", "Eliminar"),
                  "카드의 '삭제' — '이 장소를 지울까요?' · 단추 '지우기' (삭제 요청이 아니다)");
            Do(() => Click("RemoveRequestPanel/Box0926/Buttons0926/Remove_YES"));
            Wait(() => placeOf(idA) == null && !Active("RemoveRequestPanel") && !Active("Fixpage"), 15f, "'지우기' → 장소 사라짐 · 창 닫힘");
            Sleep(0.4f);
            Shot("placeedit_deleted");
            Do(() => row = rowOf(idA));
            Check(() => row != null && !row.exists && !row.folder_on_disk && spawned(idA) == null, "서버에서 지움 (사진 폴더까지) · 장면에서도 바로 사라짐");
            Check(() => said(rrl("menuLabel")) == scene("menuReq") && said(rrl("dialogText")) == scene("dialogReq") && said(rrl("yesLabel")) == scene("yesReq"),
                  "지운 뒤 메뉴·묻는 글·단추는 원래대로 ('삭제 요청')");

            // 3) 남의 장소 B — '삭제 요청' 그대로 · '장소 수정 요청' (보내 보지는 않는다 — 운영 서버로 간다)
            Do(() => tap(dtOf(idB)));
            Wait(() => open(dtOf(idB)), 10f, "B 상세 열림");
            Do(() => Click(More));
            Sleep(0.6f);
            Check(() => said(rrl("menuLabel")) == scene("menuReq"), "남의 장소의 '···' — '삭제 요청' 그대로");
            Do(() => Click(Menu + "FixPlaceButton"));
            Wait(() => Active("Fixpage") && !loading() && subs().Count == 2 && fv("mainPhoto") != null, 20f, "B 고치기 화면 — 지금 값 (남의 장소)");
            Sleep(0.6f);
            Shot("placeedit_other");
            Check(() => title() == modeTitle(1) && ((InputField)fv("nameInput")).text == NameB && (int)fv("currentCategoryIndex") == 2
                        && submit() == R0926LocalizedText.Say("수정 요청", "Send request", "修正を依頼", "提交申请", "Enviar solicitud")
                        && shown("descriptionInput") && !shown("locationRow") && !shown("deletePlaceButton"),
                  "남의 장소 — '장소 수정 요청' · 지금 값으로 채움(사진 포함) · 수정할 내용 칸 · 위치 줄·삭제 없음 · '수정 요청'");
            Do(() => Click(Page + "XButton_FixUpload"));
            Sleep(0.4f);
            Check(() => !Active("Fixpage"), "닫기 — 남의 장소는 보내 보지 않는다 (수정 요청은 운영 서버로 간다)");

            // 4) 우팡 장소가 아닌 것(관광공사 등) — 이름만 채운다 (예전엔 빈 칸)
            Do(() =>
            {
                var objs = TourAPIManager.Instance != null ? TourAPIManager.Instance.GetSpawnedObjects() : null;
                if (objs != null)
                    foreach (var go in objs.Values)
                    {
                        var d = go != null && go.activeInHierarchy ? go.GetComponentInChildren<DoubleTap3D>(true) : null;
                        if (d != null && !string.IsNullOrEmpty(d.GetName())) { tour = d; break; }
                    }
                if (tour == null) { log.Add("  관광공사 오브젝트 없음 — 건너뜀"); return; }
                tap(tour);
            });
            Sleep(1.0f);
            Do(() => { if (tour != null) Click(Menu + "FixPlaceButton"); });
            Sleep(0.8f);
            Check(() =>
            {
                if (tour == null) return true;
                string n = ((InputField)fv("nameInput")).text;
                log.Add($"  관광공사 '{tour.GetName()}' → 이름 칸 '{n}' · 장소 번호 {fv("editId")} · 내 장소 {fv("ownerMode")}");
                return n == tour.GetName() && (int)fv("editId") == 0 && !(bool)fv("ownerMode") && subs().Count == 0 && title() == modeTitle(1);
            }, "우팡 장소가 아닌 것 — 이름만 채우고 수정 요청으로");
            Do(() => { if (tour != null) Click(Page + "XButton_FixUpload"); });
            Sleep(0.3f);

            Do(() =>
            {
                DevPost("", "/api/dev/place_reset", "{\"ids\":[" + idA + "," + idB + "]}");
                CubeDataFixManager.EditorBase = null;
                SfLogin(false);
                PlayerPrefs.DeleteKey(LoginManager.AUTH_TOKEN_KEY);
            });
        }

        // ── v53 앱 안 '프로필 편집' — 대표님 10-27 「안드로이드는 '사진설정'을 하려 해도 '사진첩'·'갤러리'가 아니라 '내 파일'로 안내」
        //    「아예 웹으로 이동하지 않고 앱 내에서 수정할 수 있으면 더 좋을 것 같긴 해」.
        //    시험 서버(127.0.0.1:5099)의 프로필 흉내(dev_profile_shim — 운영 app_improved 의 프로필·사진 코드 그대로, DB 는 버리는 55442):
        //    시험 회원 880001 pe_me(사진 있음) · 880002 pe_taken — 토큰 hunt-dev:pe1 이 880001.
        //    받는 동안 → 칸 → 아이디 규칙(한글·숫자와 마침표·남의 아이디) → 전화 → 저장(아이디·소개 두 줄·전화·X) → 30일 잠금 → 그냥 닫힘
        //    → 사진(자르기 → 올리기 · 서버 거절 · 그림 아님) → 닫기 셋(뒤로·X·끌기) → 불러오기 실패·다시 로그인
        [Serializable] private class DevProfileRow { public bool exists, changed; public string username, bio, phone, instagram_id, facebook_id, x_id, avatar_url, photo_format; public string[] files; public int photo_w, photo_h; }
        private const string PfId = "880001", PfToken = "hunt-dev:pe1";
        private const string PfS = "FullProfilePanel/ProfileEdit0926", PfCard = PfS + "/Card0926", PfC = PfCard + "/Form0926/Viewport/Content/";
        private const string PfPhoto = "Assets/Redesign0926/Splash0926_other/motif_kr.jpg";
        private static void PlanProfileEdit()
        {
            R0926ProfileEdit pe = null;
            R0926PhotoCrop crop = null;
            R0926Busy busy = null;
            UploadInputMirror mirror = null;
            string firstPhoto = null, newPhoto = null;
            var T = typeof(UploadInputMirror);
            Func<string> state = () => pe != null ? typeof(R0926ProfileEdit).GetField("state", NPI).GetValue(pe).ToString() : "";
            Func<string, InputField> inp = k => Find(PfC + "F_" + k + "/Input")?.GetComponent<InputField>();
            Func<string> msg = () => Active(PfCard + "/Message0926") ? TextOf(PfCard + "/Message0926") : "";
            Func<string> save = () => TextOf(PfCard + "/Save0926/Label0926");
            Func<CanvasGroup> form = () => Find(PfCard + "/Form0926")?.GetComponent<CanvasGroup>();
            Func<Image> photo = () => Find(PfC + "Avatar0926/Mask0926/Photo0926")?.GetComponent<Image>();
            Func<DevProfileRow> row = () =>
            {
                string r = DevGet("", "/api/dev/profile_state/1");
                return string.IsNullOrEmpty(r) ? new DevProfileRow() : JsonUtility.FromJson<DevProfileRow>(r);
            };
            Func<InputField> mi = () => mirror != null ? T.GetField("mirrorInput", NPI).GetValue(mirror) as InputField : null;
            Func<string> busyText = () => busy != null && busy.Showing ? (typeof(R0926Busy).GetField("label", NPI).GetValue(busy) as Text)?.text ?? "" : "";
            Func<string, bool> toastHas = part =>
            {
                var tm = ToastManager.Instance;
                var l = tm != null ? typeof(ToastManager).GetField("live", NPI)?.GetValue(tm) as System.Collections.IList : null;
                if (l == null) return false;
                foreach (var o in l) if ((o.GetType().GetField("message")?.GetValue(o) as string ?? "").Contains(part)) return true;
                return false;
            };
            // 칸을 누른 것처럼 키보드 위 입력줄을 열고 글을 쓴다 → 닫으면 칸에 들어간다
            Action<string, string> typeOpen = (k, text) =>
            {
                var src = inp(k);
                if (mirror == null || src == null || mi() == null) { log.Add("  입력줄 없음 " + k); return; }
                T.GetMethod("ActivateMirrorFor", NPI).Invoke(mirror, new object[] { src });
                mi().text = text;
            };
            Action typeClose = () => { if (mirror != null) T.GetMethod("OnCloseClicked", NPI).Invoke(mirror, null); };
            Action<string, string> type = (k, text) => { Do(() => typeOpen(k, text)); Sleep(0.3f); Do(typeClose); Sleep(0.5f); };
            Action edit = () => ProfileManager.Instance.editProfileButton.onClick.Invoke();

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
                ProfileManager.EditorBase = dev;
                FollowManager.EditorBase = dev;
                MessagePanelManager.EditorBase = dev;
                R0926HuntApi.EditorBase = dev;
                DevPost("", "/api/dev/profile_reset", "{\"users\":[{\"n\":1,\"username\":\"pe_me\",\"email\":\"pe@x.y\",\"bio\":\"처음 소개\",\"phone\":\"010-1234-5678\",\"instagram_id\":\"insta_me\",\"avatar\":true},"
                                                     + "{\"n\":2,\"username\":\"pe_taken\"}]}");
                PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, PfToken);
                SfLogin(true, PfId);
                LoginManager.Instance.CurrentUser.username = "pe_me";
                pe = UnityEngine.Object.FindAnyObjectByType<R0926ProfileEdit>(FindObjectsInactive.Include);
                if (pe != null)
                {
                    crop = typeof(R0926ProfileEdit).GetField("photoCrop", NPI).GetValue(pe) as R0926PhotoCrop;
                    busy = typeof(R0926ProfileEdit).GetField("busy", NPI).GetValue(pe) as R0926Busy;
                }
                mirror = Find("FullProfilePanel")?.GetComponent<UploadInputMirror>();
            });
            Check(() => pe != null && LoginManager.Instance.IsLoggedIn && LoginManager.Instance.CurrentUser.id == PfId && row().exists,
                "준비: 시험 회원 880001 pe_me (로그인 이벤트 없이) · 프로필은 시험 서버로");
            Check(() =>
            {
                var miss = new List<string>();
                foreach (var n in new[] { "form", "scroll", "avatar", "usernameInput", "usernameNote", "usernameLock", "bioInput", "phoneInput", "instagramInput",
                                          "facebookInput", "xInput", "message", "saveButton", "saveLabel", "photoCrop", "busy", "closer", "closeButton" })
                    if (!(typeof(R0926ProfileEdit).GetField(n, NPI)?.GetValue(pe) is UnityEngine.Object o) || o == null) miss.Add(n);
                log.Add("  연결 빠짐: " + (miss.Count == 0 ? "없음" : string.Join(", ", miss)));
                return miss.Count == 0;
            }, "연결: 칸 여섯 · 사진 · 안내 · 저장 · 자르기 · 진행 안내 · 닫기");

            // 내 프로필 → '편집' — 웹 대신 앱 안 시트 (받는 동안은 보이던 공개 정보 · 흐리게)
            Do(() => ProfileManager.Instance.ShowProfile(PfId));
            Wait(() => Active("FullProfilePanel") && ProfileManager.Instance.editProfileButton != null && ProfileManager.Instance.editProfileButton.gameObject.activeInHierarchy,
                12f, "내 프로필 → '편집' 버튼");
            Sleep(1.2f);
            Shot("pe_profile");
            Do(() => DevPost("", "/api/dev/profile_fail", "{\"get\":{\"delay\":1.5}}"));
            Do(edit);
            Sleep(0.5f);
            Shot("pe_loading");
            Check(() => Active(PfS) && state() == "Loading" && save() == "불러오는 중…" && inp("username").text == "pe_me" && inp("phone").text == ""
                        && form() != null && !form().blocksRaycasts && form().alpha < 0.5f,
                "'편집' → 앱 안 시트 (웹으로 나가지 않음) · 받는 동안 '불러오는 중…' · 공개 정보 먼저 · 칸은 흐리게(못 누름)");
            Wait(() => state() == "Ready", 10f, "서버에서 내 정보를 받음");
            Sleep(0.8f);
            Shot("pe_open");
            Check(() => inp("username").text == "pe_me" && inp("bio").text == "처음 소개" && inp("phone").text == "010-1234-5678" && inp("instagram").text == "insta_me"
                        && inp("facebook").text == "" && inp("x").text == "" && form().blocksRaycasts && form().alpha > 0.99f,
                "받은 내 정보가 칸에: 아이디 · 소개 · 전화번호(본인에게만 옴) · 인스타그램 · 칸을 누를 수 있음");
            Check(() => save() == "저장하기" && !Active(PfC + "F_username/Input/Lock0926") && TextOf(PfC + "F_username/Note0926").StartsWith("영문 소문자") && msg() == "",
                "'저장하기' · 아이디 잠금 없음 · 아이디 규칙 안내 한 줄");
            Check(() => photo()?.sprite != null && TextOf(PfC + "Avatar0926/Label0926") == "사진 바꾸기" && TextOf(PfC + "Web0926/Label0926") == "비밀번호 변경 · 계정 삭제  ›",
                "사진 칸에 지금 사진 · '사진 바꾸기' · 맨 아래 '비밀번호 변경 · 계정 삭제' (이것만 웹)");
            Check(() =>
            {
                var rt = Find(PfCard) as RectTransform;
                if (rt == null) return false;
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                log.Add($"  카드 {c[0].x:0},{c[0].y:0} ~ {c[2].x:0},{c[2].y:0} · 화면 {Screen.width}x{Screen.height}");
                return c[0].x >= -1 && c[0].y >= -1 && c[2].x <= Screen.width + 1 && c[2].y <= Screen.height + 1;
            }, "카드가 화면 안");
            Check(() =>
            {
                var src = mirror != null ? T.GetField("sourceInputs", NPI).GetValue(mirror) as InputField[] : null;
                if (src == null) return false;
                int n = 0;
                foreach (var k in new[] { "username", "bio", "phone", "instagram", "facebook", "x" })
                    if (inp(k) != null && Array.IndexOf(src, inp(k)) >= 0 && !inp(k).enabled) n++;
                log.Add("  입력줄이 맡은 칸 " + n + "/6");
                return n == 6;
            }, "여섯 칸 모두 키보드 위 입력줄로 (칸은 꺼 두고 누르면 입력줄 — 장소 추가·보물찾기 리뷰와 같은 줄)");
            Do(() => { var sr = Find(PfCard + "/Form0926")?.GetComponent<ScrollRect>(); if (sr != null) { sr.StopMovement(); sr.verticalNormalizedPosition = 0f; } });
            Sleep(0.4f);
            Shot("pe_bottom");
            Check(() => InView(PfC + "Web0926", PfCard + "/Form0926/Viewport"), "칸 목록을 내리면 맨 아래 줄까지 보임");
            Do(() => { var sr = Find(PfCard + "/Form0926")?.GetComponent<ScrollRect>(); if (sr != null) { sr.StopMovement(); sr.verticalNormalizedPosition = 1f; } });
            Sleep(0.3f);

            // 아이디 — 입력줄(영문 키보드·20자) · 한글 · 숫자와 마침표 · 남의 아이디
            Do(() => typeOpen("username", "한글아이디"));
            Sleep(0.4f);
            Shot("pe_typing");
            Check(() => Active("FullProfilePanel/UploadInputMirror") && mi() != null && mi().keyboardType == TouchScreenKeyboardType.EmailAddress && mi().characterLimit == 20
                        && mi().lineType == InputField.LineType.SingleLine,
                "아이디 칸 → 키보드 위 입력줄 (영문 키보드 · 20자 · 한 줄)");
            Do(typeClose);
            Sleep(0.5f);
            Check(() => inp("username").text == "한글아이디" && !Active("FullProfilePanel/UploadInputMirror"), "입력줄을 닫으면 아이디 칸에");
            Do(() => Click(PfCard + "/Save0926"));
            Sleep(0.3f);
            Check(() => msg().StartsWith("아이디는 영문 소문자") && state() == "Ready" && row().username == "pe_me", "한글 아이디 → 보내지 않고 저장 버튼 위에 안내");
            type("username", "1.2");
            Do(() => Click(PfCard + "/Save0926"));
            Sleep(0.3f);
            Check(() => msg() == "숫자와 마침표만으로 된 아이디는 쓸 수 없어요", "숫자·마침표만 → 안내 (서버가 기관 아이디 모양으로 막는 것)");
            type("username", "PE_Taken");
            Do(() => Click(PfCard + "/Save0926"));
            Wait(() => state() == "Ready" && msg() != "", 8f, "남의 아이디 → 서버 답");
            Shot("pe_taken");
            Check(() => msg() == "이미 쓰는 아이디예요" && save() == "저장하기" && row().username == "pe_me", "남의 아이디(대문자로 써도) → 서버 409 · '이미 쓰는 아이디예요' · 그대로");

            // 전화 — 숫자 키보드 · 글자가 섞이면 안내
            type("username", "pe_me");
            Do(() => typeOpen("phone", "010-abc"));
            Sleep(0.4f);
            Check(() => mi() != null && mi().keyboardType == TouchScreenKeyboardType.PhonePad && mi().characterLimit == 20, "전화번호 칸 → 숫자 키보드 · 20자");
            Do(typeClose);
            Sleep(0.5f);
            Do(() => Click(PfCard + "/Save0926"));
            Sleep(0.3f);
            Check(() => msg() == "전화번호는 숫자와 + - ( ) 만 쓸 수 있어요", "전화번호에 글자 → 안내");

            // 저장 — 아이디(대문자 → 소문자) · 소개 두 줄 · 전화 · X (인스타그램은 손대지 않아 보내지 않음)
            type("phone", "02-123-4567");
            Do(() => typeOpen("bio", "고친 소개\n둘째 줄"));
            Sleep(0.4f);
            Check(() => mi() != null && mi().lineType == InputField.LineType.MultiLineNewline && mi().characterLimit == 150, "소개 칸 → 여러 줄 · 150자");
            Do(typeClose);
            Sleep(0.5f);
            type("username", "PE_Renamed");
            type("x", "x_me");
            Do(() => DevPost("", "/api/dev/profile_fail", "{\"profile\":{\"delay\":1.2}}"));
            Do(() => Click(PfCard + "/Save0926"));
            Sleep(0.4f);
            Shot("pe_saving");
            Check(() => state() == "Saving" && save() == "저장하는 중…" && !form().blocksRaycasts, "저장 → '저장하는 중…' · 그동안 칸을 못 누름");
            Wait(() => !Active(PfS), 8f, "저장되면 시트가 내려가며 닫힘");
            Check(() => toastHas("프로필을 저장했어요"), "'프로필을 저장했어요'");
            Check(() =>
            {
                var r = row();
                log.Add($"  서버: '{r.username}' · 소개 '{(r.bio ?? "").Replace("\n", "⏎")}' · 전화 '{r.phone}' · 인스타 '{r.instagram_id}' · X '{r.x_id}' · 바꾼 날 {r.changed}");
                return r.username == "pe_renamed" && r.bio == "고친 소개\n둘째 줄" && r.phone == "02-123-4567" && r.instagram_id == "insta_me" && r.x_id == "x_me" && r.changed;
            }, "서버에 저장: 아이디 소문자로 · 소개 두 줄 · 전화 · X (인스타그램은 그대로)");
            Check(() => LoginManager.Instance.CurrentUser.username == "pe_renamed" && PlayerPrefs.GetString("SavedUsername", "") == "pe_renamed",
                "앱의 내 아이디도 새 아이디로 (다음에 켤 때도)");
            Wait(() => (typeof(ProfileManager).GetField("currentProfile", NPI).GetValue(ProfileManager.Instance) as ProfileData)?.username == "pe_renamed", 6f,
                "뒤의 프로필 창도 새 아이디로");
            Sleep(0.6f);
            Shot("pe_saved");

            // 다시 열면 아이디 30일 잠금 — 칸을 누르면 입력줄 대신 남은 날 · 고친 것 없이 '저장하기' 는 그냥 닫힘
            Do(edit);
            Wait(() => state() == "Ready", 10f, "다시 열기");
            Sleep(0.6f);
            Check(() => Active(PfC + "F_username/Input/Lock0926") && TextOf(PfC + "F_username/Note0926") == "아이디는 30일 뒤에 다시 바꿀 수 있어요"
                        && inp("username").text == "pe_renamed" && inp("bio").text == "고친 소개\n둘째 줄",
                "다시 열면: 아이디 잠금 (30일) · 새 아이디 · 소개 두 줄");
            Do(() => Click(PfC + "F_username/Input/Lock0926"));
            Sleep(0.3f);
            Shot("pe_locked");
            Check(() => msg() == "아이디는 30일 뒤에 다시 바꿀 수 있어요" && !Active("FullProfilePanel/UploadInputMirror"), "잠긴 아이디 칸을 누르면 입력줄 대신 남은 날");
            Do(() => Click(PfCard + "/Save0926"));
            Sleep(0.3f);
            Check(() => !Active(PfS) && row().username == "pe_renamed", "고친 것 없이 '저장하기' → 보내지 않고 닫힘");

            // 사진 — 사진첩 대신 EditorPick → 자르기 → 올리는 중(진행) → 새 사진
            Do(edit);
            Wait(() => state() == "Ready", 10f, "다시 열기");
            Do(() => { firstPhoto = row().avatar_url; DevPost("", "/api/dev/profile_fail", "{\"avatar\":{\"delay\":1.5}}"); });
            Do(() => pe.EditorPick(File.ReadAllBytes(PfPhoto)));
            Wait(() => crop != null && crop.IsOpen, 5f, "사진 → 자르기 화면");
            Func<string> cropTitle = () => crop.transform.Find("Head0926/Title0926")?.GetComponent<Text>()?.text ?? "";
            Check(() => cropTitle() == "프로필 사진", "자르기 화면 제목 '프로필 사진' (장소의 '메인 사진' 아님)");
            Sleep(0.6f);
            Shot("pe_crop");
            Do(() => crop.Done());
            Wait(() => busyText().StartsWith("사진 올리는 중"), 6f, "자르기 → '사진 올리는 중…'");
            Sleep(0.6f);
            Check(() => { log.Add($"  답을 기다리는 동안: 「{busyText()}」"); return busyText().StartsWith("사진 올리는 중") && !busyText().Contains("100%"); },
                  "답을 기다리는 동안 100% 로 보이지 않음 (99 까지)");
            Shot("pe_uploading");
            Wait(() => busy != null && !busy.Showing && toastHas("프로필 사진을 바꿨어요"), 12f, "올리기 끝 → '프로필 사진을 바꿨어요'");
            Wait(() => photo()?.sprite != null && photo().sprite.texture.width == 512, 6f, "시트 사진 칸에 새 사진 (서버의 512)");
            Sleep(0.5f);
            Shot("pe_photo");
            Check(() =>
            {
                var r = row();
                newPhoto = r.avatar_url;
                log.Add($"  서버 사진 {r.avatar_url} · {r.photo_w}x{r.photo_h} {r.photo_format} · 파일 {string.Join(",", r.files ?? new string[0])} (앞 사진 {firstPhoto})");
                return r.avatar_url != firstPhoto && r.avatar_url.StartsWith("/uploads/avatars/avatar_880001_") && r.photo_w == 512 && r.photo_h == 512 && r.photo_format == "JPEG"
                       && r.files != null && r.files.Length == 1 && !r.files[0].Contains("seed");
            }, "서버: 새 사진 512 정사각형 JPEG · 앞 사진 파일은 지움");
            Check(() => (LoginManager.Instance.CurrentUser.avatar_url ?? "").EndsWith(newPhoto ?? "-"), "앱의 내 사진 주소도 새 것");
            Do(() => pe.EditorPick(File.ReadAllBytes(PfPhoto)));
            Wait(() => crop.IsOpen, 5f, "사진 → 자르기");
            Do(() => crop.Cancel());
            Sleep(0.3f);
            Check(() => !crop.IsOpen && state() == "Ready" && row().avatar_url == newPhoto, "자르기에서 취소 → 올리지 않음");
            Texture2D placeTex = null;
            Do(() => { placeTex = new Texture2D(64, 64); crop.Open(placeTex, r => { if (r != null) UnityEngine.Object.Destroy(r); }, () => { }); });
            Check(() => crop.IsOpen && cropTitle() == "메인 사진", "제목 없이 열면(장소 추가·수정) 다시 '메인 사진'");
            Do(() => { crop.Cancel(); UnityEngine.Object.Destroy(placeTex); });
            Do(() => DevPost("", "/api/dev/profile_fail", "{\"avatar\":{\"status\":413,\"error\":\"too_large\"}}"));
            Do(() => pe.EditorPick(File.ReadAllBytes(PfPhoto)));
            Wait(() => crop.IsOpen, 5f, "다시 사진 → 자르기");
            Do(() => crop.Done());
            Wait(() => busy != null && !busy.Showing && toastHas("사진을 열지 못했어요"), 10f, "서버가 거절(너무 큼) → '사진을 열지 못했어요'");
            Check(() => row().avatar_url == newPhoto, "실패하면 사진은 그대로");
            Do(() => pe.EditorPick(System.Text.Encoding.ASCII.GetBytes("not an image")));
            Sleep(0.3f);
            Check(() => !crop.IsOpen && msg() == "사진을 열지 못했어요. 다른 사진을 골라 주세요", "그림이 아닌 파일 → 자르기 없이 안내");

            // 닫기 셋 — 뒤로가기 · X · 끌어내리기 (편집 시트만 닫히고 프로필은 그대로)
            Back();
            Sleep(0.9f);
            Check(() => !Active(PfS) && Active("FullProfilePanel"), "뒤로가기 → 편집 시트만 닫힘 (프로필은 그대로)");
            Do(edit);
            Wait(() => state() == "Ready", 10f, "다시 열기");
            Sleep(0.4f);
            Do(() => Click(PfCard + "/Close0926/ClosePx0926"));
            Sleep(0.9f);
            Check(() => !Active(PfS) && Active("FullProfilePanel"), "X → 내려가며 닫힘");
            Do(edit);
            Wait(() => state() == "Ready", 10f, "다시 열기");
            Sleep(0.6f);
            Drag(PfCard, 0.45f, "pe_drag");
            Sleep(0.9f);
            Check(() => !Active(PfS) && Active("FullProfilePanel"), "끌어내리면 닫힘 (프로필은 그대로)");

            // 불러오기 실패 → '다시 불러오기' · 토큰 없음 → '다시 로그인'
            Do(() => DevPost("", "/api/dev/profile_fail", "{\"get\":{\"status\":500,\"error\":\"x\"}}"));
            Do(edit);
            Wait(() => state() == "Failed", 8f, "불러오기 실패");
            Sleep(0.4f);
            Shot("pe_loadfail");
            Check(() => save() == "다시 불러오기" && msg() == "잠시 뒤 다시 해 주세요" && Find(PfCard + "/Save0926").GetComponent<Button>().interactable && !form().blocksRaycasts,
                "서버 오류 → '잠시 뒤 다시 해 주세요' · 버튼은 '다시 불러오기' · 칸은 못 누름");
            Do(() => Click(PfCard + "/Save0926"));
            Wait(() => state() == "Ready", 8f, "'다시 불러오기' → 받음");
            Check(() => msg() == "" && inp("username").text == "pe_renamed" && save() == "저장하기", "다시 받으면 안내가 사라지고 칸이 채워짐");
            Do(() => { pe.Close(); PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, ""); });
            Do(edit);
            Wait(() => state() == "Failed", 8f, "토큰 없이 열기");
            Check(() => msg() == "다시 로그인한 뒤 고칠 수 있어요", "토큰이 없으면(서버가 나로 알아보지 못함) '다시 로그인한 뒤 고칠 수 있어요'");

            Do(() =>
            {
                if (pe != null) pe.Close();
                ProfileManager.Instance?.CloseFullProfile();
                DevPost("", "/api/dev/profile_reset", "{}");
                ProfileManager.EditorBase = null;
                FollowManager.EditorBase = null;
                MessagePanelManager.EditorBase = null;
                R0926HuntApi.EditorBase = null;
                SfLogin(false);
                PlayerPrefs.DeleteKey(LoginManager.AUTH_TOKEN_KEY);
            });
            Sleep(0.5f);
        }

        // ── 10-25 N-X: 분류 없이 올리기 · 분류 색 — 대표님 「'분류' 에 있어서 체크하지 않고 올려도 되도록 … 그냥 흰색으로 발생하도록 해주고,
        //    각 필터링 분류의 색에 맞게 적절힌 인디케이터 색이 반영되어서 목록 및 AR화면에 잘 발생하도록 되어있는 지 확인해줘」.
        //    색은 DataManager.GetCategoryColor 하나 (목록 필터 칩 · 올리기 칩 · AR 인디케이터 · 목록 줄) · 분류 없음·기타 = 흰색.
        //    올리기·정보 수정은 시험 서버(5099 · 버리는 DB 55442)로만 — 운영 /upload·올림 알림(푸시)은 부르지 않는다.
        //    3D 올리기는 운영 서버에 하루 한도를 묻고 기록하므로 돌리지 않는다 (서버 쪽은 test_model_route.py 로 따로 시험)
        private static void PlanNoCategory()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const string Sheet = "UploadPage/Clip0926/UploadSheet0926/";
            const string Cube = Sheet + "CubeUploadPage/Panel/", Model = Sheet + "ModelUploadPage/Panel/", Fix = "Fixpage/FixUploadPage/Panel/";
            const string More = "FullScreenPanel/FullScreenGuide/TopUIPanel/FixButton";
            const string Menu = "FullScreenPanel/FullScreenGuide/FixButtonPanel/MoreSheet0926/Tiles0926/";
            const string FakeId = "990777", NameG = "분류 시험 장소", NameF = "분류 없이 올린 시험 장소";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string[] cats = { "shop", "food", "cafe", "park", "toilet", "sport", "landmark" };
            string[] hexes = { "4F8CFF", "FF8A3D", "A47551", "38D98A", "9C7AFF", "F04747", "F5C442" };
            CubeDataFixManager fix = null;
            CubeUploadManager cum = null;
            int idA = -1, upId = -1;
            PlaceData orig = null;
            DevPlaceRow row = null;
            DevLastUpload lu = null;
            Func<Color, string> hex = c => ColorUtility.ToHtmlStringRGB(c);
            Func<int, GameObject> spawned = id => DataManager.Instance != null && DataManager.Instance.GetSpawnedObjects().TryGetValue(id, out var go) ? go : null;
            Func<int, DoubleTap3D> dtOf = id => spawned(id) != null ? spawned(id).GetComponentInChildren<DoubleTap3D>(true) : null;
            Func<int, Target> targetOf = id => spawned(id) != null ? spawned(id).GetComponentInChildren<Target>(true) : null;
            Func<int, PlaceData> placeOf = id => DataManager.Instance != null && DataManager.Instance.GetPlaceDataMap().TryGetValue(id, out var p) ? p : null;
            Func<DoubleTap3D, bool> open = d => d != null && d.fullscreenCanvasGroup != null && d.fullscreenCanvasGroup.gameObject.activeSelf;
            Action<DoubleTap3D> tap = d => typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", F).Invoke(d, null);
            Action<DoubleTap3D> close = d => typeof(DoubleTap3D).GetMethod("CloseFullscreen", F).Invoke(d, null);
            Func<string, object> fv = n => typeof(CubeDataFixManager).GetField(n, F).GetValue(fix);
            Func<string, object> uv = n => typeof(CubeUploadManager).GetField(n, F).GetValue(cum);
            Func<PlaceData, PlaceData> copy = p => Newtonsoft.Json.JsonConvert.DeserializeObject<PlaceData>(Newtonsoft.Json.JsonConvert.SerializeObject(p));
            Func<string, float> chipAlpha = c => Find(Fix + "Chips0926/Chip_" + c)?.GetComponent<Image>()?.color.a ?? -1f;
            // 목록 줄의 색 (우팡 장소 번호 → RRGGBB) — 목록을 연 동안 combinedPlaces 에서
            Func<Dictionary<string, string>> listColors = () =>
            {
                var d = new Dictionary<string, string>();
                var cache = DataManager.Instance.GetLightCache();
                var cp = (List<(object place, float distance, string id, string displayText, string colorHex)>)typeof(PlaceListManager).GetField("combinedPlaces", F).GetValue(Plm());
                foreach (var e in cp)
                    if (e.place is PlaceData || (e.place is CachedPlaceData c && cache.Contains(c))) d[e.id] = e.colorHex;
                return d;
            };
            Func<int, DevPlaceRow> rowOf = id =>
            {
                string r = DevGet("", "/api/dev/place_row/" + id);
                return string.IsNullOrEmpty(r) ? null : JsonUtility.FromJson<DevPlaceRow>(r);
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
                SessionState.SetBool(LoginReadyKey, true);   // 플레이를 끄면 덜어 둔 로그인 저장값을 되돌린다
                CubeUploadManager.EditorBase = CubeDataFixManager.EditorBase = "http://127.0.0.1:5099";
                PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, LocalTestLogin + NcMe.Substring("hunt_test_".Length));
                SfLogin(true, NcMe);
                fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
            });
            Check(() => fix != null && cum != null && LoginManager.Instance.IsLoggedIn && LoginManager.Instance.CurrentUsername == NcMe,
                  "준비: 시험 아이디 (로그인 이벤트 없이) · 장소 올리기·고치기는 시험 서버로");

            // 1) 색표 하나 — 목록 필터 칩과 같은 색 · 기타·분류 없음은 흰색
            Check(() =>
            {
                var got = new List<string>();
                bool ok = true;
                for (int i = 0; i < cats.Length; i++)
                {
                    string h = hex(DataManager.GetCategoryColor(cats[i]));
                    got.Add(cats[i] + " " + h);
                    ok &= h == hexes[i] && hex(R0926FilterChip.CatColors[i + 1]) == h;
                }
                foreach (var c in new[] { "etc", "", null })
                {
                    string h = hex(DataManager.GetCategoryColor(c));
                    got.Add((c == null ? "null" : c == "" ? "(없음)" : c) + " " + h);
                    ok &= h == "FFFFFF";
                }
                log.Add("  " + string.Join(" · ", got));
                return ok;
            }, "색표 — 샵 4F8CFF · 음식점 FF8A3D · 카페 A47551 · 공원 38D98A · 화장실 9C7AFF · 스포츠 F04747 · 랜드마크 F5C442 (= 목록 필터 칩) · 기타·분류 없음 흰색");
            Check(() =>
            {
                var cases = new[]
                {
                    new[] { "33B2E6", "landmark", "F5C442" },   // 옛 앱이 올린 랜드마크 하늘색 → 칩 색
                    new[] { "AE53C5", "toilet", "9C7AFF" },     // 서버가 'purple' 을 바꿔 둔 색 → 칩 색
                    new[] { "FBC15D", "food", "FF8A3D" },
                    new[] { "4CD980", "park", "38D98A" },
                    new[] { null, "", "FFFFFF" },               // 분류 없이 올린 장소 (색 NULL)
                    new[] { "", "", "FFFFFF" },
                    new[] { "a", "", "FFFFFF" },                // 망가진 색 값
                    new[] { null, "etc", "FFFFFF" },
                    new[] { "44619B", "gov", "44619B" },        // 공공데이터는 서버 색 그대로
                    new[] { "FFFF00", "", "FFFF00" },           // 분류 없는 옛 장소에 남은 색 — 그대로 둔다
                };
                bool ok = true;
                foreach (var c in cases)
                {
                    string h = DataManager.ResolvePlaceColorHex(c[0], c[1]);
                    if (h != c[2]) { ok = false; log.Add($"  {c[0] ?? "null"} / '{c[1]}' → {h} (기대 {c[2]})"); }
                }
                return ok;
            }, "색 고르기 — 칩 분류는 옛 색이 남아 있어도 칩 색 · 분류 없음·기타·망가진 값은 흰색 · 공공데이터와 분류 없는 옛 장소의 색은 그대로");
            Check(() =>
            {
                var bad = new List<string>();
                int n = 0;
                foreach (var pg in new[] { ("장소", Cube), ("3D", Model), ("수정", Fix) })
                    foreach (var c in new[] { "shop", "food", "cafe", "park", "toilet", "sport", "landmark", "etc" })
                    {
                        var dot = Find(pg.Item2 + "Chips0926/Chip_" + c + "/Dot")?.GetComponent<Image>();
                        n++;
                        if (dot == null) bad.Add(pg.Item1 + " " + c + " 없음");
                        else if (hex(dot.color) != hex(DataManager.GetCategoryColor(c))) bad.Add(pg.Item1 + " " + c + " " + hex(dot.color));
                    }
                log.Add($"  칩 점 {n}개 — 다른 색 " + (bad.Count == 0 ? "없음" : string.Join(", ", bad)));
                return n == 24 && bad.Count == 0;
            }, "장소 추가 · 3D 추가 · 정보 수정의 분류 칩 점 = 같은 색표 (기타는 흰 점)");

            Wait(() =>
            {
                if (DataManager.Instance == null) return false;
                var map = DataManager.Instance.GetPlaceDataMap();
                idA = -1;
                foreach (var kv in DataManager.Instance.GetSpawnedObjects())
                    if (kv.Value != null && kv.Value.activeInHierarchy && map.TryGetValue(kv.Key, out var p) && p.model_type == "cube" && dtOf(kv.Key) != null && targetOf(kv.Key) != null)
                    { idA = kv.Key; break; }
                return idA > 0;
            }, 40f, "장면에 뜬 우팡 큐브 장소");

            // 2) 풀에서 꺼낸 인디케이터 — 앞서 쓴 장소의 색(여기선 하늘색)을 들고 있어도 이 장소의 색으로 (분류 없음은 흰색)
            Check(() =>
            {
                var dm = DataManager.Instance;
                var cache = dm.GetLightCache();
                var pool = (Queue<GameObject>)typeof(DataManager).GetField("indicatorOnlyPool", F).GetValue(dm);
                var fake = new CachedPlaceData
                {
                    uniqueId = "dm_" + FakeId, rawId = FakeId, displayName = "분류 없는 시험 장소", category = "", sourceManager = "DataManager", modelType = "cube",
                    latitude = VirtualLocation.Instance.Latitude + 0.5, longitude = VirtualLocation.Instance.Longitude, altitude = 30,
                };
                var got = new List<string>();
                cache.Add(fake);
                try
                {
                    dm.SpawnIndicatorOnly(FakeId);   // 풀을 한 번 채워 둔다 — 아래 매번 풀에서 꺼낸다
                    dm.DespawnIndicatorOnly(FakeId);
                    foreach (var c in new[] { "", "sport", "etc", "landmark" })
                    {
                        fake.category = c;
                        int stale = 0;
                        foreach (var go in pool)
                        {
                            var t = go != null ? go.GetComponentInChildren<Target>(true) : null;
                            if (t != null) { t.TargetColor = Color.cyan; stale++; }
                        }
                        bool on = dm.SpawnIndicatorOnly(FakeId);
                        var tg = on && dm.GetIndicatorOnlyObjects().TryGetValue(int.Parse(FakeId), out var o) && o != null ? o.GetComponentInChildren<Target>(true) : null;
                        got.Add((c == "" ? "(없음)" : c) + " " + (tg != null ? hex(tg.TargetColor) : "-") + (stale > 0 ? "" : " (풀 빔)"));
                        dm.DespawnIndicatorOnly(FakeId);
                    }
                }
                finally { cache.Remove(fake); }
                log.Add("  풀에서 꺼낸 인디케이터: " + string.Join(" · ", got));
                return string.Join("|", got) == "(없음) FFFFFF|sport F04747|etc FFFFFF|landmark F5C442";
            }, "풀에서 꺼낸 인디케이터 — 앞 장소 색이 남지 않음: 분류 없음 흰색 · 스포츠 빨강 · 기타 흰색 · 랜드마크 금색");

            // 3) 장면 전체 — AR 인디케이터 색 = 목록 줄 색
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.5f);
            Shot("nocat_list");
            Check(() =>
            {
                var dm = DataManager.Instance;
                var map = dm.GetPlaceDataMap();
                var list = listColors();
                int ar = 0, both = 0;
                var bad = new List<string>();
                Action<int, Target, string, string, string> one = (id, t, raw, cat, name) =>
                {
                    if (t == null) return;
                    ar++;
                    string want = DataManager.ResolvePlaceColorHex(raw, cat, name), got = hex(t.TargetColor);
                    if (got != want) bad.Add($"{id} AR {got}≠{want}");
                    if (DataManager.IsChipCategory(cat) && got != hex(DataManager.GetCategoryColor(cat))) bad.Add($"{id} {cat} {got}");
                    if (list.TryGetValue(id.ToString(), out var lh)) { both++; if (lh != got) bad.Add($"{id} 목록 {lh}≠AR {got}"); }
                };
                foreach (var kv in dm.GetSpawnedObjects())
                    if (kv.Value != null && map.TryGetValue(kv.Key, out var p)) one(kv.Key, kv.Value.GetComponentInChildren<Target>(true), p.color, p.category, p.name);
                foreach (var kv in dm.GetIndicatorOnlyObjects())
                {
                    var c = dm.GetLightCache().Find(x => x.rawId == kv.Key.ToString());
                    if (kv.Value != null && c != null) one(kv.Key, kv.Value.GetComponentInChildren<Target>(true), map.TryGetValue(kv.Key, out var p) ? p.color : null, c.category, c.displayName);
                }
                log.Add($"  AR {ar}곳 · 목록 {list.Count}줄 · 둘 다 {both}곳 — 어긋남 " + (bad.Count == 0 ? "없음" : string.Join(", ", bad.GetRange(0, Math.Min(8, bad.Count)))));
                return ar > 0 && both > 0 && bad.Count == 0;
            }, "장면의 우팡 장소 모두 — AR 인디케이터 색 = 목록 줄 색 · 칩 분류는 칩 색");
            Back();
            Sleep(0.8f);
            Check(() => !Active("ListPanel"), "목록 닫힘");

            // 4) 내 장소의 분류 비우기 — 스포츠(빨강)를 다시 눌러 해제 → '수정하기' → 서버 '' · 색 NULL → AR·목록 흰색
            Do(() =>
            {
                orig = copy(placeOf(idA));
                string seed = DevPost("", "/api/dev/place_seed", "{\"id\":" + idA + ",\"name\":\"" + NameG + "\",\"category\":\"sport\",\"lat\":" + orig.latitude.ToString("F6", inv)
                                      + ",\"lon\":" + orig.longitude.ToString("F6", inv) + ",\"alt\":" + orig.altitude.ToString("F2", inv) + ",\"subs\":2,\"owner\":\"" + NcMe.Substring("hunt_test_".Length) + "\"}");
                var p = copy(orig);
                p.name = NameG; p.category = "sport"; p.color = null; p.username = NcMe;
                DataManager.Instance.ApplyEditedPlace(p, false);   // 메모리만 — 이 장소를 내가 올린 스포츠 장소로 (앱은 장소를 저장하지 않는다)
                log.Add($"  A {idA} '{orig.name}' ({orig.category}/{orig.color ?? "null"}) → 시험 장소 {(Has(seed, "\"ok\":true") ? "심음" : "못 심음")}");
            });
            Sleep(0.3f);
            Check(() => targetOf(idA) != null && hex(targetOf(idA).TargetColor) == "F04747", "준비: 내 스포츠 장소 — AR 인디케이터 빨강");
            Do(() => tap(dtOf(idA)));
            Wait(() => open(dtOf(idA)), 10f, "A 상세 열림");
            Do(() => Click(More));
            Sleep(0.8f);
            Do(() => Click(Menu + "FixPlaceButton"));
            Wait(() => Active("Fixpage") && !(bool)fv("isProcessing") && (string)fv("selectedCategory") == "sport", 20f, "A 정보 수정 — 서버 값(스포츠) 받음");
            Sleep(0.6f);
            Shot("nocat_fix_sport");
            Check(() => (int)fv("currentCategoryIndex") == 6 && chipAlpha("sport") > 0.2f, "스포츠 칩이 골라져 있음");
            Do(() => Click(Fix + "Chips0926/Chip_sport"));
            Sleep(0.4f);
            Shot("nocat_fix_cleared");
            Check(() => (string)fv("selectedCategory") == "" && (int)fv("currentCategoryIndex") == 0 && chipAlpha("sport") >= 0f && chipAlpha("sport") < 0.1f,
                  "골라 둔 칩을 다시 누르면 해제 — 분류 없음");
            Do(() => Click(Fix + "SubmitButton"));
            Wait(() => !Active("Fixpage"), 20f, "'수정하기' — 분류 없이 저장되고 창 닫힘");
            Sleep(0.5f);
            Do(() => row = rowOf(idA));
            Check(() =>
            {
                log.Add($"  서버: '{row?.name}' · 분류 '{row?.category}' · 색 '{row?.color}'");
                return row != null && row.exists && row.category == "" && string.IsNullOrEmpty(row.color);
            }, "서버 — 분류 '' · 색 NULL");
            Check(() => placeOf(idA) != null && placeOf(idA).category == "" && targetOf(idA) != null && hex(targetOf(idA).TargetColor) == "FFFFFF",
                  "AR 인디케이터 바로 흰색");
            Do(() => { if (open(dtOf(idA))) close(dtOf(idA)); });
            Sleep(0.8f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.5f);
            Shot("nocat_list_white");
            Check(() =>
            {
                var l = listColors();
                log.Add("  목록 A 줄 색: " + (l.TryGetValue(idA.ToString(), out var h) ? h : "(목록에 없음)"));
                return h == "FFFFFF";
            }, "목록 줄도 흰색");
            Back();
            Sleep(0.8f);
            Do(() =>
            {
                if (orig != null) DataManager.Instance.ApplyEditedPlace(copy(orig), false);   // 장면은 원래 장소로
            });
            Sleep(0.3f);
            Check(() => orig != null && placeOf(idA)?.name == orig.name && targetOf(idA) != null
                        && hex(targetOf(idA).TargetColor) == DataManager.ResolvePlaceColorHex(orig.color, orig.category, orig.name),
                  "되돌림 — 장면의 A 는 원래 이름·색");

            // 5) 분류 없이 장소 올리기 — 칩을 하나도 안 고르고 '등록하기' → 막지 않고 올라감 · 분류 '' · 색은 보내지 않음
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() =>
            {
                ((InputField)uv("nameInput")).text = NameF;
                var main = new Texture2D(64, 64);
                typeof(CubeUploadManager).GetField("mainPhoto", F).SetValue(cum, main);
                typeof(CubeUploadManager).GetMethod("SetMainSprite", F).Invoke(cum, new object[] { main });
                typeof(CubeUploadManager).GetMethod("SetMainPhotoUIState", F).Invoke(cum, new object[] { true });
                var subs = (List<Texture2D>)uv("subPhotos");
                subs.Add(new Texture2D(64, 64));
                subs.Add(new Texture2D(64, 64));
                typeof(CubeUploadManager).GetMethod("UpdateSubPhotoGrid", F).Invoke(cum, null);
            });
            Sleep(0.6f);
            Shot("nocat_upload_ready");
            Check(() =>
            {
                log.Add($"  분류 '{uv("selectedCategory")}' ({uv("currentCategoryIndex")}) · 인스타 {uv("showInstagram")} · 추가 사진 {((List<Texture2D>)uv("subPhotos")).Count}장");
                return Active(Cube + "SubmitButton") && (string)uv("selectedCategory") == "" && (int)uv("currentCategoryIndex") == 0;
            }, "장소 추가 — 분류 칩을 하나도 고르지 않은 채");
            Do(() => Click(Cube + "SubmitButton"));
            Wait(() => !Active("UploadPage"), 30f, "'등록하기' — 막지 않고 올라가고 창 닫힘");
            Sleep(0.3f);
            Do(() =>
            {
                string j = DevGet("", "/api/dev/last_upload");
                lu = string.IsNullOrEmpty(j) ? null : JsonUtility.FromJson<DevLastUpload>(j);
                upId = lu != null ? lu.id : -1;
            });
            Check(() =>
            {
                if (lu == null || lu.id <= 0) return false;
                log.Add($"  시험 서버가 받은 것: #{lu.id} '{lu.name}' · 올린 사람 {lu.username} (폼 {lu.form_username}) · 분류 '{lu.category}' (칸 {lu.has_category}) · 색 칸 {lu.has_color} · {lu.lat},{lu.lon} · 사진 {lu.main}+{lu.subs} · 중복 막기 키 {lu.has_upload_id}");
                return lu.has_category && lu.category == "" && !lu.has_color && lu.username == NcMe && lu.form_username == NcMe && lu.name == NameF
                       && lu.main == 1 && lu.subs == 2 && lu.has_upload_id;
            }, "보낸 것 — 분류 '' · 색은 안 보냄 · 올린 사람 · 대표 + 추가 2장 · 중복 막기 키");
            Check(() =>
            {
                var six = new System.Text.RegularExpressions.Regex(@"^-?\d+\.\d{6}$");
                return lu != null && six.IsMatch(lu.lat ?? "") && six.IsMatch(lu.lon ?? "")
                       && Math.Abs(double.Parse(lu.lat, inv) - VirtualLocation.Instance.Latitude) < 1e-4
                       && Math.Abs(double.Parse(lu.lon, inv) - VirtualLocation.Instance.Longitude) < 1e-4;
            }, "좌표 — 소수 6자리 · 지금 위치");
            Do(() => row = upId > 0 ? rowOf(upId) : null);
            Check(() => row != null && row.exists && row.name == NameF && row.category == "" && string.IsNullOrEmpty(row.color),
                  "저장된 줄 — 분류 '' · 색 NULL (앱에서는 흰색)");

            Do(() =>
            {
                var ids = new List<string>();
                if (idA > 0) ids.Add(idA.ToString());
                if (upId > 0) ids.Add(upId.ToString());
                DevPost("", "/api/dev/place_reset", "{\"ids\":[" + string.Join(",", ids) + "]}");
                CubeUploadManager.EditorBase = CubeDataFixManager.EditorBase = null;
                SfLogin(false);
                PlayerPrefs.DeleteKey(LoginManager.AUTH_TOKEN_KEY);
            });
        }

        // ── 10-24 N-J · N-N: 댓글 — 대표님 「상품 댓글 … '신고' 할 수 있도록 하고 10개정도 신고 들어가면 그냥 삭제 … 본인이 쓴 댓글은 스와이프 통해서 삭제버튼」
        //    · 「'미르네집' … 댓글을 남겼는데 이게 바로 반영이 안되는데」
        //    쓰자마자 맨 위 · 사진 아래 미리보기도 바로 · 내 댓글을 밀면 빨간 '삭제' · 남의 댓글을 밀면 주황 '신고' → 사유 시트 → 내 목록에서 빠짐
        //    · 서로 다른 10명이 신고하면 서버가 지움. 댓글·신고는 시험 서버(5099 · 버리는 DB 55442)로만 — 운영 서버에는 쓰지 않는다
        private static void PlanComments()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const string Me = "cmme", Sheet = "CommentPanel_HQ/CommentReportSheet0926";
            const string Mine = "시험 댓글 — 쓰자마자 보여야 해요", OtherA = "남의 댓글 하나", OtherB = "신고할 남의 댓글";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            CommentManager cm = null;
            DoubleTap3D dt = null;
            int id = -1, idA = -1, idB = -1, mineId = -1;
            string meId = null;
            DevCommentState st = null;
            Func<int, GameObject> spawned = k => DataManager.Instance != null && DataManager.Instance.GetSpawnedObjects().TryGetValue(k, out var go) ? go : null;
            Func<int, DoubleTap3D> dtOf = k => spawned(k) != null ? spawned(k).GetComponentInChildren<DoubleTap3D>(true) : null;
            Func<DoubleTap3D, bool> open = d => d != null && d.fullscreenCanvasGroup != null && d.fullscreenCanvasGroup.gameObject.activeSelf;
            Action openComments = () => typeof(DoubleTap3D).GetMethod("OnCommentPreviewClicked", F).Invoke(dt, null);
            Func<List<CommentItem>> items = () =>
            {
                var l = new List<CommentItem>();
                if (cm != null && cm.commentContent != null)
                    foreach (Transform c in cm.commentContent)
                    {
                        var it = c.GetComponent<CommentItem>();
                        if (it != null && c.gameObject.activeSelf) l.Add(it);
                    }
                return l;
            };
            Func<int, CommentItem> item = k => items().Find(i => i.CommentId == k);
            Func<int, SwipeToDeleteHandler> sw = k => item(k) != null ? item(k).GetComponent<SwipeToDeleteHandler>() : null;
            Func<SwipeToDeleteHandler, string> swLabel = h => h != null ? h.transform.Find("DeleteButton/Text")?.GetComponent<Text>()?.text : null;
            Func<SwipeToDeleteHandler, Color> swColor = h => h != null && h.transform.Find("DeleteButton") != null ? h.transform.Find("DeleteButton").GetComponent<Image>().color : Color.clear;
            Action<SwipeToDeleteHandler> swTap = h => h.transform.Find("DeleteButton").GetComponent<Button>().onClick.Invoke();
            Func<string> preview = () => dt != null && dt.previewText != null ? dt.previewText.text : null;
            Func<string> none = () => R0926LocalizedText.Say("아직 댓글이 없습니다. 첫 댓글을 남겨보세요!", "No comments yet. Be the first to comment!",
                "コメントはまだありません。最初のコメントを残してください！", "暂无评论。快来抢沙发吧！", "Aún no hay comentarios. ¡Sé el primero en comentar!");
            Func<string, string> row = code => Find(Sheet + "/Card0926/Reason_" + code + "/Label0926")?.GetComponent<Text>()?.text;
            Func<SwipeToClose> behind = () => cm != null && cm.commentPanel != null ? cm.commentPanel.GetComponent<SwipeToClose>() : null;
            Func<DevCommentState> state = () =>
            {
                string r = DevGet("", "/api/dev/comment_state?location_id=" + id + "&ids=" + idA + "," + idB + "," + mineId);
                return string.IsNullOrEmpty(r) ? null : JsonUtility.FromJson<DevCommentState>(r);
            };
            Func<int, List<DevCommentReport>> reportsOf = k => st == null || st.reports == null ? new List<DevCommentReport>()
                : new List<DevCommentReport>(Array.FindAll(st.reports, x => x.target_id == k.ToString()));
            Func<int, bool> alive = k => st != null && st.comments != null && Array.Exists(st.comments, c => c.id == k);

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Wait(() =>
            {
                if (DataManager.Instance == null) return false;
                var map = DataManager.Instance.GetPlaceDataMap();
                foreach (var kv in DataManager.Instance.GetSpawnedObjects())
                    if (kv.Value != null && kv.Value.activeInHierarchy && map.ContainsKey(kv.Key) && dtOf(kv.Key) != null) { id = kv.Key; return true; }
                return false;
            }, 40f, "장면에 뜬 우팡 장소");
            Do(() =>
            {
                SessionState.SetBool(LoginReadyKey, true);   // 플레이를 끄면 덜어 둔 로그인 저장값을 되돌린다
                CommentManager.EditorBase = "http://127.0.0.1:5099";
                ReportService.EditorBase = "http://127.0.0.1:5099";
                PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, LocalTestLogin + Me);
                cm = CommentManager.Instance;
                dt = dtOf(id);
                // 시험 DB 에 같은 번호의 장소 — 댓글이 그 장소를 가리켜야 한다 (댓글은 아직 없이)
                DevPost("", "/api/dev/place_reset", "{\"ids\":[" + id + "]}");
                string at = ",\"lat\":" + Math.Round((double)VirtualLocation.Instance.Latitude, 6).ToString("F6", inv)
                          + ",\"lon\":" + Math.Round((double)VirtualLocation.Instance.Longitude, 6).ToString("F6", inv) + ",\"alt\":30";
                string p = DevPost("", "/api/dev/place_seed", "{\"id\":" + id + ",\"name\":\"댓글 시험\",\"category\":\"food\"" + at + ",\"subs\":1,\"owner\":\"cmo1\"}");
                string s = DevPost("", "/api/dev/comment_seed", "{\"location_id\":" + id + ",\"me\":\"" + Me + "\",\"comments\":[]}");
                meId = System.Text.RegularExpressions.Regex.Match(s ?? "", "\"me\"\\s*:\\s*\"(\\d+)\"").Groups[1].Value;
                SfLogin(true, meId);
                LoginManager.Instance.CurrentUser.username = "hunt_test_" + Me;
                log.Add($"  장소 {id} · 나 {meId} — 시험 장소 {(Has(p, "\"ok\":true") ? "심음" : "못 심음")}");
            });
            Check(() => cm != null && dt != null && !string.IsNullOrEmpty(meId) && LoginManager.Instance.IsLoggedIn, "준비: 시험 아이디 (로그인 이벤트 없이) · 댓글·신고는 시험 서버로");
            Check(() =>
            {
                var t = Find(Sheet);
                bool ok = t != null && t.GetComponent<R0926CommentReportSheet>() != null && t.GetComponent<R0926TapToClose>() != null
                          && Find(Sheet + "/Card0926")?.GetComponent<R0926SwipeDismiss>() != null
                          && Find(Sheet + "/Cancel0926/ClosePx0926")?.GetComponent<ClickButtonOnBack>() != null
                          && Find(Sheet + "/Cancel0926")?.GetComponent<ClickButtonOnBack>() == null
                          && t.parent == cm.commentPanel.transform && t.GetSiblingIndex() == t.parent.childCount - 1 && !t.gameObject.activeSelf;
                foreach (var code in new[] { "spam", "abuse", "sexual_hate", "other" }) ok &= Find(Sheet + "/Card0926/Reason_" + code) != null;
                return ok;
            }, "연결: 댓글 창 맨 위에 신고 시트 (사유 넷 · 끌어내리기 · 뒤로가기는 덮개 · 바깥 누르기) — 처음엔 꺼짐");

            // 1) 미리보기 '댓글 없음' → 댓글 창 → 보내자마자 맨 위 → 서버 번호 → 미리보기도 바로 (다시 열지 않고)
            Do(() => typeof(DoubleTap3D).GetMethod("OnDoubleTapCube", F).Invoke(dt, null));
            Wait(() => open(dt), 10f, "장소 상세 열림");
            Wait(() => preview() == none(), 10f, "사진 아래 미리보기 — 댓글 없음");
            Shot("comments_preview_empty");
            Check(() => preview() == none(), "미리보기: '아직 댓글이 없습니다' (앱 언어 — 예전엔 이 줄만 기기 언어)");
            Do(() => openComments());
            Wait(() => cm.IsPanelOpen && cm.commentPanel.activeInHierarchy, 5f, "댓글 창 열림");
            Sleep(1.2f);
            Check(() => items().Count == 0, "댓글 창 — 비어 있음");
            Do(() =>
            {
                cm.commentInputField.text = Mine;
                cm.PostComment();
                var l = items();
                bool ok = l.Count == 1 && l[0].CommentId == -1 && cm.commentContent.GetChild(0) == l[0].transform
                          && Array.Exists(l[0].GetComponentsInChildren<Text>(true), x => x.text.Contains(Mine));
                log.Add((ok ? "PASS " : "FAIL ") + "보내자마자 맨 위에 내 댓글 (서버 답 전 · 번호 -1)");
            });
            Wait(() => items().Count == 1 && items()[0].CommentId > 0, 10f, "서버 번호 받음");
            Do(() => mineId = items()[0].CommentId);
            Sleep(0.5f);
            Shot("comments_posted");
            Check(() => sw(mineId) != null && cm.commentInputField.text == "", "번호를 받으면 밀어 지우기가 붙음 · 입력칸 비움");
            Do(() => st = state());
            Check(() =>
            {
                var c = st != null && st.comments != null && st.comments.Length == 1 ? st.comments[0] : null;
                log.Add("  서버 댓글: " + (c != null ? c.id + " · " + c.user_id + " · " + c.content : "-"));
                return c != null && c.id == mineId && c.user_id == meId && c.content == Mine;
            }, "서버(시험 DB)에 내 댓글 — 토큰의 회원번호로");
            Wait(() => preview() != null && preview().Contains(Mine), 10f, "미리보기 바뀜");
            Check(() => preview() == "<b>hunt_test_" + Me + "</b>  " + Mine, "사진 아래 미리보기 — 다시 열지 않아도 내 댓글로 (10-24 미르네집)");

            // 2) 내 댓글을 밀면 빨간 '삭제' → 지움 → 미리보기도 '댓글 없음'
            Do(() => SwipeLeft(sw(mineId), 300f));
            Sleep(0.3f);
            Shot("comments_swipe_mine");
            Check(() =>
            {
                var h = sw(mineId); var c = swColor(h);
                log.Add($"  내 댓글 밀기: '{swLabel(h)}' · 색 {c} · 폭 {DeleteWidth(h)}");
                return swLabel(h) == LocalizationManager.Instance.GetText("swipe_delete") && c.r > 0.85f && c.g < 0.35f && DeleteWidth(h) > 200f;
            }, "내 댓글을 밀면 빨간 '삭제'");
            Do(() => swTap(sw(mineId)));
            Wait(() => item(mineId) == null, 10f, "'삭제' → 목록에서 사라짐");
            Do(() => st = state());
            Check(() => st != null && st.comments != null && st.comments.Length == 0, "서버에서도 지움");
            Wait(() => preview() == none(), 10f, "미리보기 — 댓글 없음");
            Check(() => preview() == none(), "지우면 미리보기도 바로 '댓글 없음'");

            // 3) 남의 댓글 둘 — 밀면 주황 '신고' → 사유 시트 (뒤로가기 · 바깥 누르기는 시트만 닫는다)
            Do(() =>
            {
                string s = DevPost("", "/api/dev/comment_seed", "{\"location_id\":" + id + ",\"me\":\"" + Me + "\",\"comments\":[{\"user\":\"cmo1\",\"content\":\"" + OtherA + "\"},{\"user\":\"cmo2\",\"content\":\"" + OtherB + "\"}]}");
                var seed = string.IsNullOrEmpty(s) ? null : JsonUtility.FromJson<DevCommentSeed>(s);
                if (seed != null && seed.ids != null && seed.ids.Length == 2) { idA = seed.ids[0]; idB = seed.ids[1]; }
                cm.ClosePanel();
            });
            Wait(() => !cm.IsPanelOpen && !cm.commentPanel.activeInHierarchy, 5f, "댓글 창 닫힘");
            Do(() => openComments());
            Wait(() => item(idA) != null && item(idB) != null && sw(idA) != null && sw(idB) != null, 10f, "남의 댓글 둘");
            Sleep(0.4f);
            Check(() => items().Count == 2 && cm.commentContent.GetChild(0) == item(idB).transform, "남의 댓글 둘 — 새 글(B)이 위");
            Do(() => SwipeLeft(sw(idB), 300f));
            Sleep(0.3f);
            Shot("comments_swipe_other");
            Check(() =>
            {
                var h = sw(idB); var c = swColor(h);
                log.Add($"  남의 댓글 밀기: '{swLabel(h)}' · 색 {c}");
                return swLabel(h) == R0926LocalizedText.Say("신고", "Report", "報告", "举报", "Denunciar") && c.r > 0.9f && c.g > 0.45f && c.g < 0.6f && c.b < 0.3f;
            }, "남의 댓글을 밀면 주황 '신고' (내 댓글의 빨간 '삭제'와 다르게)");
            Do(() => cm.commentInputField.ActivateInputField());
            Sleep(0.2f);
            Do(() => swTap(sw(idB)));
            Wait(() => Active(Sheet), 3f, "신고 사유 시트");
            Sleep(0.6f);
            Shot("comments_report_sheet");
            Check(() => cm.IsPanelOpen && DeleteWidth(sw(idB)) < 1f && item(idB) != null && !cm.commentInputField.isFocused && behind() != null && !behind().enabled,
                  "'신고' → 사유 시트 (댓글 창은 그대로 · 민 줄은 닫힘 · 입력칸 놓음 · 뒤 댓글 창 끌어 닫기는 쉼)");
            Check(() =>
            {
                string got = row("spam") + " / " + row("abuse") + " / " + row("sexual_hate") + " / " + row("other");
                log.Add("  사유: " + got);
                return row("spam") == R0926LocalizedText.Say("스팸·광고", "Spam or ads", "スパム・広告", "垃圾广告", "Spam")
                       && row("abuse") == R0926LocalizedText.Say("욕설·비방", "Harassment or insults", "誹謗中傷", "辱骂诽谤", "Acoso o insultos")
                       && row("sexual_hate") == R0926LocalizedText.Say("음란·혐오", "Sexual or hateful", "わいせつ・ヘイト", "色情或仇恨", "Sexual u odio")
                       && row("other") == R0926LocalizedText.Say("기타", "Other", "その他", "其他", "Otro");
            }, "사유 넷 — 스팸·광고 / 욕설·비방 / 음란·혐오 / 기타 (앱 언어)");
            Do(() => BackNow());
            Wait(() => !Active(Sheet), 3f, "뒤로가기 → 시트 닫힘");
            Sleep(0.3f);
            Check(() => cm.IsPanelOpen && cm.commentPanel.activeInHierarchy && behind() != null && behind().enabled, "뒤로가기 — 시트만 내려가고 댓글 창은 그대로 · 끌어 닫기 되살아남");
            Do(() => SwipeLeft(sw(idB), 300f));
            Sleep(0.2f);
            Do(() => swTap(sw(idB)));
            Wait(() => Active(Sheet), 3f, "다시 신고 시트");
            Sleep(0.6f);
            Do(() =>
            {
                // 화면 위쪽(카드 밖)을 누른다 — 맨 위에 맞는 것이 시트 바탕이어야 한다
                var es = UnityEngine.EventSystems.EventSystem.current;
                var pe = new UnityEngine.EventSystems.PointerEventData(es) { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.85f) };
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(pe, hits);
                if (hits.Count == 0) { log.Add("FAIL 바깥 누르기 — 맞는 것 없음"); return; }
                pe.pointerPressRaycast = hits[0]; pe.pointerCurrentRaycast = hits[0];
                log.Add((hits[0].gameObject == Find(Sheet).gameObject ? "PASS " : "FAIL ") + "카드 밖 맨 위는 시트 바탕 (" + PathOf(hits[0].gameObject.transform) + ")");
                UnityEngine.EventSystems.ExecuteEvents.Execute(UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject),
                    pe, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            });
            Wait(() => !Active(Sheet), 3f, "바깥 누르기 → 시트 닫힘");
            Check(() => !Active(Sheet) && cm.IsPanelOpen, "바깥(어두운 곳) 누르기 — 시트만 닫힘");

            // 4) 사유를 고르면 접수 — 그 댓글은 내 목록에서 바로 빠지고, 서버도 나에게는 다시 주지 않는다
            Do(() => SwipeLeft(sw(idB), 300f));
            Sleep(0.2f);
            Do(() => swTap(sw(idB)));
            Wait(() => Active(Sheet), 3f, "또 신고 시트");
            Sleep(0.6f);
            Do(() => Click(Sheet + "/Card0926/Reason_abuse"));
            Wait(() => !Active(Sheet) && item(idB) == null, 10f, "'욕설·비방' → 접수 · 시트 닫힘 · 목록에서 빠짐");
            Sleep(0.3f);
            Shot("comments_reported");
            Do(() => st = state());
            Check(() =>
            {
                var r = reportsOf(idB);
                log.Add("  B 신고: " + string.Join(", ", r.ConvertAll(x => x.reporter_id + "/" + x.reason + "/" + x.status)) + " · 남음 " + alive(idB));
                return r.Count == 1 && r[0].reporter_id == meId && r[0].reason == "abuse" && r[0].status == "open" && r[0].detail.Contains(OtherB) && alive(idB);
            }, "서버: 신고 한 건 (욕설·비방 · 내 회원번호 · 댓글 내용 함께) — 아직 1명이라 댓글은 남음");
            Check(() => item(idA) != null && items().Count == 1, "다른 댓글은 그대로");
            Wait(() => preview() == "<b>hunt_test_cmo1</b>  " + OtherA, 10f, "미리보기 — 신고한 댓글 말고");
            Check(() => preview() == "<b>hunt_test_cmo1</b>  " + OtherA, "미리보기도 신고한 댓글은 빼고 다음 댓글로");
            Do(() => cm.ClosePanel());
            Wait(() => !cm.IsPanelOpen && !cm.commentPanel.activeInHierarchy, 5f, "댓글 창 닫힘");
            // 다시 열면 예전 줄을 지우고(그 프레임 끝에 사라진다) 새로 받는다 — 지워지기 직전의 예전 A 를 새 목록으로 보지 않게
            CommentItem oldA = null;
            Do(() => { oldA = item(idA); openComments(); });
            Wait(() => item(idA) != null && item(idA) != oldA, 10f, "다시 열기 (새로 받은 목록)");
            Sleep(0.6f);
            Check(() =>
            {
                var l = items();
                log.Add("  다시 연 목록: " + string.Join(", ", l.ConvertAll(i => i.CommentId.ToString())) + " · 토큰 " + (PlayerPrefs.GetString(LoginManager.AUTH_TOKEN_KEY, "") != "" ? "있음" : "없음"));
                return l.Count == 1 && item(idB) == null;
            }, "다시 열어도 신고한 댓글은 안 옴 (서버가 신고한 사람에겐 빼고 준다)");

            // 5) 서로 다른 10명 — 9명이 신고해 둔 댓글을 내가 신고하면 서버가 지운다
            Do(() => DevPost("", "/api/dev/comment_reports", "{\"comment_id\":" + idA + ",\"n\":9}"));
            Do(() => SwipeLeft(sw(idA), 300f));
            Sleep(0.2f);
            Do(() => swTap(sw(idA)));
            Wait(() => Active(Sheet), 3f, "A 신고 시트");
            Sleep(0.6f);
            Do(() => Click(Sheet + "/Card0926/Reason_spam"));
            Wait(() => !Active(Sheet) && item(idA) == null, 10f, "'스팸·광고' → 접수 · 목록에서 빠짐");
            Sleep(0.3f);
            Shot("comments_auto_deleted");
            Do(() => st = state());
            Check(() =>
            {
                var r = reportsOf(idA);
                log.Add($"  A 신고 {r.Count}건 · 상태 {string.Join(",", new HashSet<string>(r.ConvertAll(x => x.status)))} · 남음 {alive(idA)}");
                return !alive(idA) && r.Count == 10 && r.TrueForAll(x => x.status == "auto_deleted") && r.Exists(x => x.reporter_id == meId && x.reason == "spam");
            }, "열 번째(서로 다른 10명) 신고 — 서버가 댓글을 지움 · 신고 10건 모두 'auto_deleted'");
            Check(() => alive(idB) && reportsOf(idB).Count == 1, "1명만 신고한 B 는 그대로 (나에게만 안 보임)");
            Wait(() => preview() == none(), 10f, "미리보기 — 남은 댓글 없음");
            Check(() => preview() == none() && items().Count == 0, "미리보기 · 목록 모두 '댓글 없음' (B 는 내가 신고해서 · A 는 지워져서)");

            Do(() =>
            {
                if (cm != null && cm.IsPanelOpen) cm.ClosePanel();
                DevPost("", "/api/dev/place_reset", "{\"ids\":[" + id + "]}");
                CommentManager.EditorBase = null;
                ReportService.EditorBase = null;
                SfLogin(false);
                PlayerPrefs.DeleteKey(LoginManager.AUTH_TOKEN_KEY);
            });
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
            Check(() => FooterLow("세로"), "세로: 버전·저작권을 조금씩 아래로 — 묶음에서 버전 글 62 · 저작권 124 · 맨 아래 여백 24 (예전 50·100·48)");
            Do(() => Click(C + "GroupInfo/RowPrivacy"));
            Sleep(0.8f);
            Shot("appinfo_privacy");
            Check(() => PrivacyShown("세로"), "개인정보처리방침: 누르면 앱 안에서 바로 (제목 11개 · 설정 쪽을 덮음)");
            Do(() => ScrollPrivacy(0f));
            Sleep(0.6f);
            Shot("appinfo_privacy_end");
            Check(PrivacyEnd, "끝까지 내리면 글의 마지막 줄이 보이는 칸 안");
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
            Check(() => FooterLow("가로"), "가로: 버전·저작권도 같은 간격 (묶음에서 62 · 124 · 맨 아래 24)");
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

        // 버전·저작권을 조금씩 아래로 (10-24 대표님) — 끝까지 내린 자리에서 캔버스 단위로 잰다
        private static bool FooterLow(string when)
        {
            var g = Find(SetPath + "Viewport/Content/GroupInfo") as RectTransform;
            var ver = Find(SetPath + "Viewport/Content/VersionFooter") as RectTransform;
            var cp = Find(SetPath + "Viewport/Content/CopyrightFooter") as RectTransform;
            var vp = Find(SetPath + "Viewport") as RectTransform;
            if (g == null || ver == null || cp == null || vp == null) { log.Add($"  {when}: 묶음·버전·저작권·보이는 칸 중 없음"); return false; }
            float scale = vp.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            Rect gr = ScreenRect(g), v = ScreenRect(ver), c = ScreenRect(cp), w = ScreenRect(vp);
            float toVer = (gr.yMin - v.center.y) / scale, toCopy = (gr.yMin - c.yMax) / scale, bottom = (c.yMin - w.yMin) / scale;
            log.Add($"  {when}: 묶음 아래 → 버전 글 가운데 {toVer:0.#} · 저작권 위 {toCopy:0.#} · 저작권 아래 여백 {bottom:0.#} (버전 칸 {v.height / scale:0.#})");
            return Mathf.Abs(toVer - 62f) < 1.5f && Mathf.Abs(toCopy - 124f) < 1.5f && Mathf.Abs(bottom - 24f) < 2f;
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

        // 끝까지 내리면 글 파일의 맨 끝 줄이 보이는 칸 안에 (10-24 부터 맨 끝은 시행일 다음의 DB-IP 표기)
        private static bool PrivacyEnd()
        {
            var vp = Find(PrivacyPath + "/Scroll0926/Viewport") as RectTransform;
            var content = vp != null ? vp.Find("Content") : null;
            if (content == null) return false;
            Text last = null;
            foreach (var t in content.GetComponentsInChildren<Text>()) last = t;
            if (last == null) return false;
            string src = System.IO.File.ReadAllText(R0926LocalizedText.Lang() == "ko" ? "Assets/Redesign0926/Text/privacy_ko.txt" : "Assets/Redesign0926/Text/privacy_en.txt");
            string want = System.Array.FindLast(src.Replace("\r", "").Split('\n'), s => s.Trim().Length > 0).Trim().TrimStart('-', '>', '#', ' ');
            Rect l = ScreenRect(last.rectTransform), w = ScreenRect(vp);
            log.Add($"  맨 끝 줄 '{last.text}' {l.yMin:0}~{l.yMax:0} · 보이는 칸 {w.yMin:0}~{w.yMax:0}");
            return last.text == want && l.yMin >= w.yMin - 1f && l.yMax <= w.yMax + 1f;
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
        // 제목이 이 글인 줄 — 시험 자리 근처에 진짜 게임이 있으면 첫 줄이 시험 게임이 아니다
        private static Transform ActiveChildTitled(string path, string title)
        {
            var t = Find(path);
            if (t == null) return null;
            foreach (Transform c in t)
                if (c.gameObject.activeSelf && (c.Find("Title")?.GetComponent<Text>()?.text ?? "").EndsWith(title)) return c;
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
            Check(() =>
            {
                var on = new List<Transform>();
                var list = Find(H + "Rules/Viewport/Content/List");
                if (list != null) foreach (Transform c in list) if (c.gameObject.activeSelf) on.Add(c);
                var t1 = on.Count == 8 ? on[0].Find("Title")?.GetComponent<Text>() : null;
                var t4 = on.Count == 8 ? on[3].Find("Title")?.GetComponent<Text>() : null;
                var b4 = on.Count == 8 ? on[3].Find("Body")?.GetComponent<Text>() : null;
                var b7 = on.Count == 8 ? on[6].Find("Body")?.GetComponent<Text>() : null;
                log.Add("  수칙 4: " + (t4 != null ? t4.text : "-") + " ⏎ " + (b4 != null ? b4.text.Replace("\n", " ⏎ ") : "-"));
                return t1 != null && t4 != null && b4 != null && b7 != null && t4.text == "이런 게임은 계정이 정지·삭제될 수 있어요"
                       && b4.text.Contains("낚시성") && b4.text.Contains("도박성") && b4.text.Contains("유해성") && b4.text.Contains("계정이 정지되거나 삭제돼요")
                       && t4.color.r > 0.9f && t4.color.b < 0.4f && t1.color != t4.color && !b7.text.Contains("대략");
            }, "수칙 4 — 낚시성·도박성·유해성 → 계정 정지·삭제 (제목 주황) · 7 에 없는 '대략·비공개' 안내 없음");
            Do(() => Click(H + "Rules/Viewport/Content/Agree"));
            Sleep(0.3f);
            Do(() => Click(H + "Rules/Viewport/Content/AgreeButton"));
            Sleep(1.5f);
            Shot("hunt_form");
            Check(() => Active(H + "Form"), "동의 → 숨기기 양식");
            const string F = HfForm, RV = HuntReview;
            Check(() => ActiveChildren(F + "PlayerChips") == 5, "참가 인원 칩 5개(10·20·30·50·100)");
            // 기본값 (10-26 대표님) — 고른 칩만 굵은 글자
            Check(() =>
            {
                var on = new List<string>();
                foreach (var box in new[] { "ShapeChips", "ModeChips", "WinnerChips", "PlayerChips", "VisibleChips", "RadiusChips", "AudienceChips", "StartChips", "DurationChips" })
                    on.Add(HuntChipOn(F + box));
                log.Add("  기본값: " + string.Join(" · ", on));
                return string.Join(",", on) == "chest,touch,1,100,10,10,all,0,10080";
            }, "기본값 — 보물상자 · 먼저 터치 · 당첨 1명 · 참가 100명 · 보이는 거리 10m · 참가 범위 10km · 모두 · 바로 · 일주일");
            Check(() => HuntChipValues(F + "DurationChips") == "60,180,1440,4320,10080" && TextOf(F + "DurationChips/Chip_10080/Label") == "일주일", "진행 시간 — 30분 없음 · 1시간부터 · 7일은 '일주일'");
            Check(() => HuntChipValues(F + "ShapeChips") == "chest,gift,heart,photo,model", "보물 모양 — 보물상자 · 선물상자 · 하트 · 사진 · 3D 모델만");
            Check(() => HuntChipValues(F + "AudienceChips") == "all,friends,follower,code" && TextOf(F + "AudienceChips/Chip_follower/Label") == "팔로워 모두", "누구에게 — 모두 · 맞팔 · 팔로워 모두 · 참가 코드");
            Check(() => Find(F + "CoordChips") == null && Find(F + "LCoord") == null, "'위치 공개' 칸 없음 (늘 정확히)");
            Check(() =>
            {
                int t = Find(F + "TitleInput").GetSiblingIndex();
                return Find(F + "LPrize").GetSiblingIndex() == t + 1 && Find(F + "PrizeInput").GetSiblingIndex() == t + 2 && Find(F + "LHint").GetSiblingIndex() == t + 3;
            }, "상품 칸은 제목과 힌트 사이");
            Check(() => TextOf(F + "AudienceNote").StartsWith("참가 범위 안의 우팡 사용자라면 누구나"), "'모두' — 안내 줄");
            Do(() => Click(F + "AudienceChips/Chip_code"));
            Check(() => TextOf(F + "AudienceNote").Contains("참가 코드가 만들어지고") && TextOf(F + "AudienceNote").Contains("어디서든"),
                "'참가 코드' — 목록·알림에 안 나오고 코드·공유 링크를 받은 사람만 어디서든 참가한다고 안내");
            Do(() => Click(F + "AudienceChips/Chip_follower"));
            Check(() => TextOf(F + "AudienceNote").StartsWith("나를 팔로우하는 사람만"), "'팔로워 모두' — 안내 줄");
            Do(() => Click(F + "AudienceChips/Chip_all"));
            // 시험은 1시간 · 보이는 거리 50m (연장 · 다가가기 시험이 이 값으로 잰다)
            Do(() => { Click(F + "DurationChips/Chip_60"); Click(F + "VisibleChips/Chip_50"); });
            Do(() =>
            {
                Find(F + "TitleInput").GetComponent<InputField>().text = "시험 보물 (플레이 시험)";
                Find(F + "PrizeInput").GetComponent<InputField>().text = "커피 쿠폰 (시험)";
                Find(F + "HintInput").GetComponent<InputField>().text = "충남도청 앞 분수대 근처";
            });

            // 숨기기 → 확인 카드 (10-26 대표님) — 고치기 · 바깥 · 뒤로가기는 양식으로 (적은 것 그대로) · 숨기기 → 같은 카드에 공유하기
            Do(() => Click(F + "Submit"));
            Sleep(0.6f);
            Shot("hunt_review");
            Check(() => Active(RV) && R0926HuntCard.Reviewing && TextOf(RV + "/Sheet0926/Head") == "이대로 숨길까요?" && ActiveChildren(RV + "/Sheet0926/Rows") == 11
                        && Active(RV + "/Sheet0926/Buttons/Edit") && TextOf(RV + "/Sheet0926/Buttons/Go/Label") == "숨기기"
                        && !Active(RV + "/Sheet0926/Buttons/Share") && !Active(RV + "/Sheet0926/Buttons/Done") && !Active(RV + "/Sheet0926/CodeBox"),
                "숨기기 → 확인 카드 '이대로 숨길까요?' · 11줄 · [고치기 | 숨기기]");
            Check(() => Active(RV + "/Sheet0926/Warn") && TextOf(RV + "/Sheet0926/Warn") == "낚시성·도박·유해한 게임은 내려가고, 계정이 정지되거나 삭제될 수 있어요"
                        && Find(RV + "/Sheet0926/Warn").GetSiblingIndex() == Find(RV + "/Sheet0926/Note").GetSiblingIndex() + 1,
                "확인 카드 — 안내 아래 경고 줄 (낚시성·도박·유해 → 계정 정지·삭제)");
            Check(() =>
            {
                foreach (var r in new[] { "제목", "상품", "힌트", "보물 모양", "찾는 방식", "당첨 · 참가", "보이는 거리", "참가 범위", "누구에게", "시작 · 진행", "숨길 자리" })
                    log.Add("  카드 " + r + ": " + ReviewRow(r).Replace("\n", " ⏎ "));
                return ReviewRow("제목") == "시험 보물 (플레이 시험)" && ReviewRow("상품") == "커피 쿠폰 (시험)" && ReviewRow("힌트") == "충남도청 앞 분수대 근처"
                       && ReviewRow("보물 모양") == "보물상자" && ReviewRow("찾는 방식") == "먼저 터치" && ReviewRow("당첨 · 참가") == "1명 · 최대 100명"
                       && ReviewRow("보이는 거리") == "50m" && ReviewRow("참가 범위") == "10km" && ReviewRow("누구에게") == "모두" && ReviewRow("시작 · 진행") == "바로 · 1시간"
                       && ReviewRow("숨길 자리").StartsWith("지금 서 있는 자리");
            }, "확인 카드 — 고른 그대로 한 줄씩");
            Check(() => Find(RV)?.GetComponent<Canvas>() is Canvas cv && cv.overrideSorting && cv.sortingOrder > 9500, "확인 카드는 캔버스 맨 위 층 (도크·창·토스트 위)");
            HitCheck(() => Find(RV + "/Sheet0926/Buttons/Go") as RectTransform, false, "확인 카드의 '숨기기'가 맨 위에서 눌림");
            Do(() => Click(RV + "/Sheet0926/Buttons/Edit"));
            Sleep(0.4f);
            Check(() => !Active(RV) && !R0926HuntCard.Reviewing && Active(H + "Form") && Find(F + "TitleInput").GetComponent<InputField>().text == "시험 보물 (플레이 시험)"
                        && Find(F + "PrizeInput").GetComponent<InputField>().text == "커피 쿠폰 (시험)", "고치기 → 카드만 닫힘 · 적은 것 그대로");
            Do(() => Click(F + "Submit"));
            Sleep(0.4f);
            Back();
            Sleep(0.6f);
            Check(() => !Active(RV) && Active("UploadPage") && Active(H + "Form"), "뒤로가기 → 카드만 닫힘 (추가 창 · 양식 그대로)");
            Do(() => Click(F + "Submit"));
            Sleep(0.4f);
            Do(() => Click(RV + "/Dim0926"));
            Sleep(0.4f);
            Check(() => !Active(RV) && Active(H + "Form"), "카드 바깥(어두운 곳) → 카드만 닫힘");
            Do(() => Click(F + "Submit"));
            Sleep(0.4f);
            Do(() => { R0926Share.EditorLast = null; Click(RV + "/Sheet0926/Buttons/Go"); });
            Wait(() => TextOf(RV + "/Sheet0926/Head") == "보물을 숨겼어요!", 10f, "숨김 → 카드 '보물을 숨겼어요!'");
            Sleep(0.5f);
            Shot("hunt_review_done");
            Do(() => { huntPosted = HuntPostedId(); log.Add("  숨긴 게임 " + huntPosted); });
            Check(() => huntPosted > 0 && TextOf(RV + "/Sheet0926/Sub") == "시험 보물 (플레이 시험)" && !Active(RV + "/Sheet0926/Rows") && !Active(RV + "/Sheet0926/CodeBox")
                        && Active(RV + "/Sheet0926/Buttons/Share") && Active(RV + "/Sheet0926/Buttons/Done") && !Active(RV + "/Sheet0926/Buttons/Edit") && !Active(RV + "/Sheet0926/Buttons/Go")
                        && TextOf(RV + "/Sheet0926/Note").StartsWith("참가 범위 안의 사람들이 목록에서"),
                "숨긴 뒤 같은 카드 — 제목 · [확인 | 공유하기] · '모두' 게임은 참가 코드 없음");
            Check(() => !Active(RV + "/Sheet0926/Warn"), "숨긴 뒤엔 경고 줄 없음 (숨기기 전에만)");
            Do(() => Click(RV + "/Sheet0926/Buttons/Share"));
            Do(() => log.Add("  공유 글: " + (R0926Share.EditorLast ?? "-").Replace("\n", " ⏎ ")));
            Check(() => (R0926Share.EditorLast ?? "").StartsWith("[우팡 보물찾기] 시험 보물 (플레이 시험)\n상품: 커피 쿠폰 (시험)\n") && R0926Share.EditorLast.EndsWith("/hunt/" + huntPosted)
                        && !R0926Share.EditorLast.Contains("참가 코드") && Active(RV),
                "공유하기 → '[우팡 보물찾기] 제목 ⏎ 상품 ⏎ …/hunt/번호' (코드 없음) · 카드는 그대로");
            Do(() => Click(RV + "/Sheet0926/Buttons/Done"));
            Wait(() => Active(H + "Detail"), 8f, "확인 → 상세");
            Sleep(1.0f);
            Shot("hunt_detail_host");
            Check(() => Active(H + "Detail") && !Active(RV) && !R0926HuntCard.Reviewing, "확인 → 카드 닫고 상세(주최자)");
            Check(() => Find(H + "Detail/Viewport/Content/Open") == null && Active(H + "Detail/Viewport/Content/ExtendRow/Extend") && Active(H + "Detail/Viewport/Content/ExtendRow/ExtendDay"),
                "주최자 상세: '보물 열기' 버튼 없음 · '1시간 연장하기 / 1일 연장하기' 한 줄");
            Check(() => TextOf(H + "Detail/Viewport/Content/Info").Contains("참가  0/100명") && !TextOf(H + "Detail/Viewport/Content/Info").Contains("위치 공개"),
                "상세: 참가 0/100명 (인원 기본 100) · '위치 공개' 줄 없음");
            Check(() => Active(H + "Detail/Viewport/Content/Share"), "주최자 상세에 '공유하기'");
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
            Do(() => ActiveChildTitled(H + "Hub/Viewport/Content/NearList", "시험 보물 (플레이 시험)")?.GetComponent<Button>()?.onClick.Invoke());
            Sleep(2.0f);
            Do(() => { R0926Share.EditorLast = null; Click(H + "Detail/Viewport/Content/Share"); });
            Check(() => Active(H + "Detail/Viewport/Content/Share") && (R0926Share.EditorLast ?? "").EndsWith("/hunt/" + huntPosted), "참가자 상세에도 '공유하기' ('모두' 게임) → 같은 링크");
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
            Check(() => !TextOf(H + "Detail/Viewport/Content/Near").StartsWith("보물이 보이는 거리예요") && R0926HuntCard.EditorBuzzes == 0, "250m 밖에서 참가 → 아직 안 보임 · 진동 없음(첫 확인은 기준만)");
            Do(() => VirtualLocation.Instance.SetCoordinates(huntSpot.x - 0.00009f, huntSpot.y));   // 10m 남쪽 — 상자를 화면에서 누를 수 있게
            Sleep(9.5f);
            Shot("hunt_near");
            Check(() => (Find(H + "Detail/Viewport/Content/Info")?.GetComponent<Text>()?.text ?? "").Contains("참가  1/100명"), "참가 → 상세 1/100명");
            Check(() => TextOf(H + "Detail/Viewport/Content/Near") == "보물이 보이는 거리예요", "보이는 거리 안 → '보물이 보이는 거리예요' (여는 법 안내 글 없이)");
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
            DevPost(host, "/api/hunts/rules/accept", "{\"version\":2}");
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
            const string T1Title = "AR 보물 시험 1", T2Title = "AR 보물 시험 2 (비밀번호)", Question = "문 앞 번호 네 자리는?", Prize = "커피 쿠폰 한 장";
            const string C = "HuntCelebrate0926/Panel/Content/", F = C + "Card/Front/";
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
            Do(() => log.Add("  처음 화면 줄 자물쇠: T1 " + HubRowLock(T1Title) + " · T2 " + HubRowLock(T2Title)));
            Check(() => HubRowLock(T2Title) == 1 && HubRowLock(T1Title) == 0, "처음 화면 참가 목록 — 비밀번호를 건 T2 줄만 제목 앞에 자물쇠");
            Shot("treasure_hub_lock");
            Check(() => R0926HuntTreasure.SpawnedObjects.Count == 0, "284m 밖 → 보물 안 뜸 (서버가 자리를 안 준다)");
            CloseUpload();
            Check(() => !Active("UploadPage"), "추가 창을 닫음 — 닫혀 있어도 지켜본다");

            // 비밀번호를 건 게임은 장소 목록 · 지도에도 자물쇠
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => Active("ListPanel") && HuntRowIcon(T1Title) != "" && HuntRowIcon(T2Title) != "", 15f, "장소 목록에 보물 줄 둘");
            Sleep(0.8f);
            Shot("treasure_list_lock");
            Do(() => log.Add("  목록 보물 줄 그림: T1 " + HuntRowIcon(T1Title) + " · T2 " + HuntRowIcon(T2Title)));
            Check(() => HuntRowIcon(T2Title) == "r0926_i_treasure_lock" && HuntRowIcon(T1Title) == "r0926_i_treasure", "장소 목록 — 비밀번호를 건 T2 줄만 자물쇠 달린 보물 그림");
            Do(() => Modes()?.Show(1));
            Wait(() => MapPinLock(trHunt1) >= 0 && MapPinLock(trHunt2) >= 0, 10f, "지도에 보물 핀 둘");
            Sleep(0.8f);
            Shot("treasure_map_lock");
            Do(() => log.Add("  지도 핀 자물쇠: T1 " + MapPinLock(trHunt1) + " · T2 " + MapPinLock(trHunt2)));
            Check(() => MapPinLock(trHunt2) == 1 && MapPinLock(trHunt1) == 0, "지도 — 비밀번호를 건 T2 핀만 테 오른쪽 아래 자물쇠");
            Do(() => Modes()?.Show(0));
            Sleep(0.6f);
            Back();
            Sleep(0.8f);
            Check(() => !Active("ListPanel"), "목록을 닫음");

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
            Check(() =>
            {
                var c = Treasure(trHunt1)?.GetComponentInChildren<R0926HuntChest>(true);
                return c != null && !c.Locked && !c.PadlockShown;
            }, "비밀번호 없는 보물(T1) — 자물쇠 없음");
            Check(() => Active("HuntToast0926") && TextOf("HuntToast0926/Pill/Title") == "보물이 근처에 있어요!" && !Active("HuntToast0926/Pill/Sub"),
                "처음 보이면 상태 알약 아래 알림 '보물이 근처에 있어요!' 한 줄 (여는 법 안내 글 없이)");
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

            // 한 번만 누르면 살짝 커졌다 돌아오기만 (여는 법 안내 글 없음) · 45m 에서 두 번 톡톡 → '조금만 더 가까이' (판정 거리 30m)
            TapTreasure(() => trHunt1);
            Sleep(0.9f);
            Do(() => log.Add("  한 번 누른 뒤 알림: " + (Active("HuntToast0926") ? TextOf("HuntToast0926/Pill/Title") : "없음")));
            Check(() => !TextOf("HuntToast0926/Pill/Title").Contains("톡톡") && !TextOf("HuntToast0926/Pill/Sub").Contains("톡톡")
                        && R0926HuntTreasure.Tracking(trHunt1) && Treasure(trHunt1) != null,
                "한 번만 누르면 — 여는 법 안내 글 없이 · 열리지 않음");
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
            Sleep(0.2f);
            Shot("treasure_reveal_fly");   // 찾은 그 보물이 AR 자리에서 가운데로 날아오는 중
            Check(() =>
            {
                var chest = RevealChest();
                log.Add("  무대의 보물 " + (chest != null ? chest.transform.root.name + " · 잡힘 " + chest.Held : "없음") + " · AR 자리에서 " + CelebrateFrom()
                        + " · 단계 " + R0926HuntCelebrate.EditorPhase);
                return chest != null && chest.transform.root.name == "HuntRevealStage" && chest.Held && chest.GetComponentInChildren<Target>(true) == null
                       && chest.GetComponentInChildren<Collider>(true) == null && GameObject.Find("HuntTreasure_" + trHunt1) == null && CelebrateFrom();
            }, "찾음 → AR 의 그 보물상자를 떼어 축하 화면으로 (보이던 자리에서 날아옴 · 박스·화살표·누르는 상자는 떼고 · AR 에는 안 남음)");
            Sleep(1.2f);
            Shot("treasure_reveal_suspense");   // 두근두근
            Do(() => log.Add("  두근두근 중 글자: " + (StageWords(C) == "" ? "없음" : StageWords(C))));
            Check(() => StageWords(C) == "" && Find(C + "Caption") == null, "두근두근 — 글자 없이 (진동·소리만)");
            Do(() => R0926HuntCelebrate.Show(trHunt1, T1Title, 1, 1, false, Prize, "dup", "chest", null));   // 거의 같은 때 누른 두 번째 '찾음'
            Check(() => R0926HuntCelebrate.Showing && R0926HuntCelebrate.EditorPhase == "Suspense" && RevealChest() != null,
                "축하 중에 같은 보물의 '찾음'이 또 와도 처음부터 다시 열지 않음");
            Wait(() => R0926HuntCelebrate.EditorPhase == "Open", 5f, "두근두근 → 짜잔");
            Sleep(0.15f);
            Shot("treasure_reveal_open");
            Wait(() => R0926HuntCelebrate.EditorPhase == "Done", 4f, "짜잔 → 카드");
            Sleep(0.8f);
            Shot("treasure_found");
            Do(() => log.Add("  카드: " + CardSummary(F) + " · 열리기까지 " + R0926HuntCelebrate.EditorOpenedAfter.ToString("0.00") + "초"));
            Check(() => TextOf(F + "Title") == "보물을 찾았습니다!" && TextOf(F + "Rank") == "가장 먼저 찾았어요" && TextOf(F + "Hunt") == T1Title
                        && TextOf(F + "PrizeBox/Prize") == Prize && TextOf(F + "Meta0/Value").EndsWith("님") && TextOf(F + "Meta2/Value") == "1 / 1명"
                        && TextOf(F + "Note").StartsWith("주최자에게 찾았다는 메세지를 보냈어요") && TextOf(F + "Seal/Num") == "1" && Active(F + "Crown"),
                "카드: '보물을 찾았습니다!' · 가장 먼저 · 게임 이름 · 상품 · 주최 · 순위 1 / 1명 · 주최자에게 메세지 · 1 메달 + 왕관");
            Check(() => R0926HuntCelebrate.EditorOpenedAfter >= 3f && R0926HuntCelebrate.EditorOpenedAfter <= 4.2f, "두근두근 3~4초 뒤에 짜잔");
            Check(() => R0926HuntCelebrate.EditorUnlockedAfter < 0f, "비밀번호 없는 보물 — 자물쇠 없이 바로 열림");
            Check(() => !R0926HuntTreasure.Tracking(trHunt1) && !R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + trHunt1), "찾음 → 그만 지켜봄");
            Check(() => RevealChest() == null && GameObject.Find("HuntRevealStage") == null && PrefLine(trHunt1) == null, "카드가 다 가리면 3D 보물·카메라를 치움 · 기기 기록에서도 지움");
            Do(() => Click(C + "OK"));
            Wait(() => !R0926HuntCelebrate.Showing, 2f, "확인 → 축하 화면 닫힘");
            Do(() => R0926HuntCelebrate.Show(trHunt1, T1Title, 1, 1, false, Prize, "dup", "chest", null));   // 답이 늦게 온 두 번째 '찾음'
            Sleep(0.3f);
            Check(() => !R0926HuntCelebrate.Showing, "막 닫은 보물의 늦게 온 '찾음' — 축하 화면을 다시 열지 않음");
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
            // 박스(화살표)를 누르는 길로: 거리 통과 → '비밀번호를 넣어 주세요' → 상세의 비밀번호 칸
            Sleep(0.3f);
            Do(() => FaceTreasure(trHunt2));
            Sleep(0.3f);
            Shot("treasure_code_20m");
            Check(() =>
            {
                var c = Treasure(trHunt2)?.GetComponentInChildren<R0926HuntChest>(true);
                var pad = c != null ? c.transform.Find("Padlock") : null;
                var r = pad != null ? pad.Find("Body")?.GetComponent<MeshRenderer>() : null;
                log.Add("  T2 자물쇠: " + (c == null ? "상자 없음" : "잠김 " + c.Locked + " · 보임 " + c.PadlockShown + " · 그림 " + (r != null && r.enabled)
                        + " · 재질 " + (r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "-") + " · 자리 " + (pad != null ? pad.localPosition.ToString("F3") : "-")));
                return c != null && c.Locked && c.PadlockShown && r != null && r.enabled && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("HuntLockSteel")
                       && pad.localPosition.z < -0.05f;
            }, "비밀번호를 건 보물(T2) — 앞에 은빛 자물쇠");
            Do(() => { var tap = Treasure(trHunt2)?.GetComponentInChildren<Target>(true)?.OnIndicatorTapped; tap?.Invoke(); tap?.Invoke(); });   // 두 번 톡톡
            Wait(() => TextOf("HuntToast0926/Pill/Title") == "비밀번호를 넣어 주세요", 6f, "박스 누름 → 비밀번호");
            Check(() => TextOf("HuntToast0926/Pill/Sub") == Question, "비밀번호 게임: '비밀번호를 넣어 주세요' · 아래 줄에 힌트");
            Check(() => Treasure(trHunt2)?.GetComponentInChildren<R0926HuntChest>(true)?.Locked == true, "비밀번호를 넣기 전엔 자물쇠 그대로");
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

            // 코드로 찾기 — 다시 참가 → 상세의 코드 칸 → 틀린 코드 → 맞는 코드 → 같은 연출 (그 게임 모양으로 새 상자)
            const string CodeRow = H + "Detail/Viewport/Content/ClaimCodeBox/CodeRow/", CardToast = "UploadPage/Clip0926/UploadSheet0926/HuntUploadPage/Toast0926/Text";
            Do(() => R0926HuntPush.Open(trHunt2.ToString(), null, false));
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(T2Title) && Active(H + "Detail/Viewport/Content/Join"), 15f, "T2 상세 (참가하기)");
            Do(() => Click(H + "Detail/Viewport/Content/Join"));
            Wait(() => R0926HuntTreasure.Tracking(trHunt2) && Active(H + "Detail/Viewport/Content/ClaimCodeBox"), 20f, "다시 참가 → 코드 칸");
            Do(() => { Find(CodeRow + "ClaimCodeInput").GetComponent<InputField>().text = "1111"; Click(CodeRow + "CodeOk"); });
            Wait(() => TextOf(CardToast).StartsWith("비밀번호가 달라요"), 8f, "틀린 비밀번호");
            Check(() => TextOf(CardToast).StartsWith("비밀번호가 달라요 — 남은 기회") && !R0926HuntCelebrate.Showing, "틀린 비밀번호 → '비밀번호가 달라요 — 남은 기회 N번' · 축하 화면 없음");
            Do(() => { Find(CodeRow + "ClaimCodeInput").GetComponent<InputField>().text = "4321"; Click(CodeRow + "CodeOk"); });
            Wait(() => R0926HuntCelebrate.Showing, 10f, "맞는 코드 → 축하 화면");
            Sleep(0.4f);
            Shot("treasure_code_reveal");
            Check(() =>
            {
                var chest = RevealChest();
                log.Add("  비밀번호로 찾음: 무대의 보물 " + (chest != null ? chest.name + " · 뚜껑 " + chest.HasLid + " · 자물쇠 " + chest.Locked + "/" + chest.PadlockShown : "없음")
                        + " · AR 자리에서 " + CelebrateFrom() + " · 단계 " + R0926HuntCelebrate.EditorPhase);
                return chest != null && chest.HasLid && chest.GetComponent<Target>() == null && !CelebrateFrom() && chest.Locked && chest.PadlockShown
                       && GameObject.Find("HuntTreasure_" + trHunt2) == null && !R0926HuntTreasure.Tracking(trHunt2);
            }, "비밀번호로 찾음 → 그 게임 모양으로 새 보물상자 (가운데서 떠오름 · 자물쇠 걸린 채) · AR 보물은 치우고 그만 봄");
            Wait(() => R0926HuntCelebrate.EditorPhase == "Done", 9f, "코드 → 두근두근 → 짜잔 → 카드");
            Sleep(0.8f);
            Shot("treasure_code_found");
            Do(() => log.Add("  카드: " + CardSummary(F) + " · 열리기까지 " + R0926HuntCelebrate.EditorOpenedAfter.ToString("0.00") + "초"));
            Check(() => TextOf(F + "Rank") == "가장 먼저 찾았어요" && TextOf(F + "Hunt") == T2Title && TextOf(F + "PrizeBox/Prize") == "따로 정한 상품은 없어요"
                        && TextOf(F + "Meta2/Value") == "1 / 1명" && TextOf(F + "Seal/Num") == "1" && Active(F + "Crown"),
                "코드로 찾은 카드: 가장 먼저 · 게임 이름 · 상품 없음 · 1 / 1명 · 1 메달 + 왕관");
            Check(() => R0926HuntCelebrate.EditorOpenedAfter >= 3f && R0926HuntCelebrate.EditorOpenedAfter <= 4.2f, "비밀번호로 찾아도 두근두근 3~4초 뒤에 짜잔");
            Check(() =>
            {
                float lead = R0926HuntCelebrate.EditorOpenedAfter - R0926HuntCelebrate.EditorUnlockedAfter;
                log.Add("  자물쇠 풀림 " + R0926HuntCelebrate.EditorUnlockedAfter.ToString("0.00") + "초 · 열림 " + R0926HuntCelebrate.EditorOpenedAfter.ToString("0.00") + "초 (" + lead.ToString("0.00") + "초 먼저)");
                return R0926HuntCelebrate.EditorUnlockedAfter > 0f && lead >= 0.15f && lead <= 0.45f;
            }, "비밀번호를 건 보물 — 열리기 직전(0.3초)에 자물쇠가 딸깍 풀려 떨어짐");
            Back();
            Wait(() => !R0926HuntCelebrate.Showing && Active(H + "Detail") && DetailTitle().Contains(T2Title) && TextOf(H + "Detail/Viewport/Content/Mine").Contains("찾았어요"), 12f, "뒤로가기 → T2 상세");
            Sleep(0.6f);
            Shot("treasure_code_detail_found");
            Check(() => !R0926HuntCelebrate.Showing && !Active(H + "Detail/Viewport/Content/ClaimCodeBox") && TextOf(H + "Detail/Viewport/Content/Mine").Contains("찾았어요")
                        && Has(DevGet(TrPlayer, "/api/hunts/" + trHunt2), "\"rank\":1") && !R0926HuntTreasure.Tracking(trHunt2) && !PlayerPrefs.HasKey(TreasurePref),
                "뒤로가기(= 확인) → 축하 화면 닫힘 · T2 상세 — '찾았어요' · 코드 칸 닫힘 · 서버에 1등 · 그만 지켜봄");

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
        private static float hcRest, hcPeak, hcTilt, hcYaw, hcLidMax, hcRollMax, hcScaleMax;
        private static void PlanHuntChest()
        {
            const string C = "HuntCelebrate0926/Panel/Content/", F = C + "Card/Front/";
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
                if (!string.IsNullOrEmpty(storeSrc)) Backdrop("bg_city.png");
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

            // 찾음 — AR 의 그 보물을 떼어 축하 화면으로 (R0926HuntTreasure.Take 처럼: 별빛은 상자 아래로 · 앵커 뿌리는 지움)
            //  날아옴 0.55초 → 두근두근 3.2초(쿵-쿵 진동 · 점점 세게 떪 · 뚜껑이 두 번 들썩이며 틈으로 빛 · 끝에 몰아침) → 짜잔(뚜껑 활짝 · 빛줄기 · 금화·꽃가루) → 카드가 솟아 뒤집힘
            Do(() =>
            {
                hcLidMax = 0f; hcRollMax = 0f; hcScaleMax = 0f;
                if (hcChest != null)
                {
                    hcGo.transform.Find("Twinkle")?.SetParent(hcChest.transform, true);
                    hcChest.transform.SetParent(null, true);
                    UnityEngine.Object.Destroy(hcGo);
                }
                R0926HuntCelebrate.Show(0, "카페 앞 보물찾기", 1, 3, false, "커피 쿠폰 한 장", "우팡", "chest", null, hcChest);
            });
            Action rec = () =>
            {
                var c = RevealChest();
                if (c == null || R0926HuntCelebrate.EditorPhase != "Suspense") return;
                var pv = c.transform.parent;
                hcLidMax = Mathf.Max(hcLidMax, c.LidAngle);
                hcRollMax = Mathf.Max(hcRollMax, Mathf.Abs(Mathf.DeltaAngle(0f, pv.localEulerAngles.z)));
                hcScaleMax = Mathf.Max(hcScaleMax, pv.localScale.x);
            };
            Sleep(0.15f);
            Shot("reveal_fly");
            Check(() =>
            {
                var c = RevealChest();
                log.Add("  무대의 보물 " + (c != null ? c.transform.root.name + " · 잡힘 " + c.Held + " · 뚜껑 " + c.HasLid : "없음") + " · AR 자리에서 " + CelebrateFrom()
                        + " · 단계 " + R0926HuntCelebrate.EditorPhase);
                return c != null && c == hcChest && c.transform.root.name == "HuntRevealStage" && c.Held && c.HasLid && c.GetComponent<Target>() == null
                       && hcGo == null && CelebrateFrom() && R0926HuntCelebrate.EditorPhase == "Arrive";
            }, "찾은 그 보물상자가 AR 에서 떨어져 축하 화면 무대로 (보이던 자리에서 날아옴 · 박스·화살표는 뗌)");
            watch(1.0f, rec);
            Shot("reveal_beat");
            Check(() => RevealPixels("reveal_rt"), "3D 보물은 투명 배경에 찍힘 (모서리 알파 0 · 상자는 불투명) — 뒤의 어둠·빛줄기가 비침");
            watch(0.65f, rec);
            Shot("reveal_peek");
            watch(1.45f, rec);
            Shot("reveal_crescendo");
            Do(() => log.Add("  두근두근 중 글자: " + (StageWords(C) == "" ? "없음" : StageWords(C))));
            Check(() => StageWords(C) == "" && Find(C + "Caption") == null, "두근두근 — 글자 없이 (진동·소리만)");
            watch(0.2f, rec);
            Wait(() => R0926HuntCelebrate.EditorPhase == "Open", 3f, "두근두근 → 짜잔");
            Do(() => log.Add("  두근두근: 뚜껑 가장 크게 " + hcLidMax.ToString("0.0") + "° · 가장 크게 떤 " + hcRollMax.ToString("0.0") + "° · 가장 크게 부푼 "
                             + hcScaleMax.ToString("0.000") + "배 · 진동 " + R0926HuntCelebrate.EditorBuzz + "번 · 열리기까지 " + R0926HuntCelebrate.EditorOpenedAfter.ToString("0.00") + "초"));
            Check(() => hcLidMax > 10f && hcLidMax < 16f, "두근두근 — 뚜껑이 들썩 (활짝 열리기 전엔 13° 까지만)");
            Check(() => hcRollMax > 3f && hcRollMax < 8f && hcScaleMax > 1.04f, "두근두근 — 쿵쿵 부풀고 끝으로 갈수록 세게 떪");
            Check(() => R0926HuntCelebrate.EditorOpenedAfter >= 3f && R0926HuntCelebrate.EditorOpenedAfter <= 4.2f && R0926HuntCelebrate.EditorBuzz >= 15,
                "뜬 뒤 3~4초에 짜잔 · 그 사이 심장 박동 진동");
            Sleep(0.35f);   // 뚜껑은 0.32초에 다 열린다 — 사진(한 장에 0.5초 넘게 걸리기도)보다 먼저 본다. 늦게 보면 카드가 이미 덮어 상자를 치운 뒤다
            Check(() => StageWords(C) == "", "짜잔 — 글자 없이");
            Check(() =>
            {
                var c = RevealChest();
                log.Add("  짜잔: 뚜껑 " + (c != null ? c.LidAngle.ToString("0") : "-") + "° · 빛줄기 " + Alpha(C + "Back/Rays").ToString("0.00") + " · 꽃가루 " + Confetti());
                return c != null && c.LidAngle > 100f && Alpha(C + "Back/Rays") > 0.5f && Confetti() > 30;
            }, "짜잔 — 뚜껑이 활짝 (100° 넘게) · 빛줄기 · 금화가 섞인 꽃가루");
            Shot("reveal_open");
            Wait(() => R0926HuntCelebrate.EditorPhase == "Card", 2f, "짜잔 → 카드");
            Sleep(0.3f);
            Shot("reveal_card_flip");
            Wait(() => R0926HuntCelebrate.EditorPhase == "Done", 2f, "카드 → 다 열림");
            Sleep(0.75f);
            Shot("reveal_done");
            Do(() => log.Add("  카드: " + CardSummary(F)));
            Check(() => TextOf(F + "Title") == "보물을 찾았습니다!" && TextOf(F + "Rank") == "가장 먼저 찾았어요" && TextOf(F + "Hunt") == "카페 앞 보물찾기"
                        && TextOf(F + "PrizeBox/Prize") == "커피 쿠폰 한 장" && TextOf(F + "Meta0/Value") == "우팡님" && TextOf(F + "Meta1/Value").Contains(":")
                        && TextOf(F + "Meta2/Value") == "1 / 3명" && TextOf(F + "Note").Contains("\n") && TextOf(F + "Seal/Num") == "1" && Active(F + "Crown")
                        && Active(C + "Card/Front") && !Active(C + "Card/BackFace"),
                "카드 앞면: '보물을 찾았습니다!' · 가장 먼저 · 게임 이름 · 상품 · 주최 우팡님 · 찾은 시각 · 1 / 3명 · 주최자 안내 두 줄 · 1 메달 + 왕관");
            Check(() =>
            {
                var ok = Find(C + "OK")?.GetComponent<CanvasGroup>();
                var card = Find(C + "Card") as RectTransform;
                log.Add("  확인 " + (ok != null ? ok.alpha.ToString("0.00") + " · 누름 " + ok.interactable : "없음") + " · 카드 " + (card != null ? card.localScale.ToString("0.000") + " " + card.anchoredPosition : "없음"));
                return ok != null && ok.alpha > 0.99f && ok.interactable && card != null && card.localScale == Vector3.one && card.anchoredPosition == Vector2.zero;
            }, "카드는 제자리 · '확인'이 떠서 누를 수 있음");
            Check(() => RevealChest() == null && GameObject.Find("HuntRevealStage") == null && !(Find(C + "ChestView")?.GetComponent<RawImage>()?.enabled ?? true),
                "카드가 다 가리면 3D 보물·카메라·판을 치움");
            Do(CloseCelebrate);

            // 2등 · AR 보물 없이(상세에서 코드로 찾음) — 같은 프리팹으로 새로 · 두근두근 중 화면(어둠)을 누르면 바로 짜잔 · 상품 없음 · 인원 제한 없음
            Do(() => R0926HuntCelebrate.Show(0, "카페 앞 보물찾기", 2, 0, false, "", "우팡", "chest", null));
            Sleep(1.3f);
            Shot("reveal_new");
            Check(() =>
            {
                var c = RevealChest();
                return c != null && c != hcChest && c.HasLid && c.GetComponent<Target>() == null && c.GetComponent<Collider>() == null && !CelebrateFrom()
                       && R0926HuntCelebrate.EditorPhase == "Suspense";
            }, "AR 보물이 없으면 프리팹으로 새 보물상자 (박스·화살표·누르는 상자 없이) · 가운데서 톡 튀어나옴");
            Do(() => Click("HuntCelebrate0926/Panel/Dim"));
            Wait(() => R0926HuntCelebrate.EditorPhase == "Open", 1.5f, "어둠을 누름 → 바로 짜잔");
            Check(() => R0926HuntCelebrate.EditorOpenedAfter > 1.2f && R0926HuntCelebrate.EditorOpenedAfter < 2.4f, "두근두근 중 화면을 누르면 끝의 몰아침(0.45초)만 남기고 바로 짜잔");
            Wait(() => R0926HuntCelebrate.EditorPhase == "Done", 3f, "카드");
            Sleep(0.75f);
            Shot("reveal_rank2");
            Do(() => log.Add("  카드: " + CardSummary(F)));
            Check(() => TextOf(F + "Rank") == "2번째로 찾았어요" && TextOf(F + "PrizeBox/Prize") == "따로 정한 상품은 없어요" && TextOf(F + "Meta2/Value") == "2번째"
                        && !TextOf(F + "Note").Contains("\n") && TextOf(F + "Seal/Num") == "2" && !Active(F + "Crown"),
                "2등: '2번째로 찾았어요' · 상품 없음 안내 · 순위 2번째(인원 제한 없음) · 안내 한 줄 · 2 메달 · 왕관 없음");
            Do(CloseCelebrate);

            // 뚜껑 없는 모양(보석) — 들썩 대신 폴짝 · 짜잔에 한 바퀴 / 마지막 보물 / 뒤로가기 = 확인 (연출 중에도 바로 닫힘)
            Do(() => { SetPushRunning(true); R0926HuntCelebrate.Show(0, "카페 앞 보물찾기", 3, 3, true, "커피 쿠폰 한 장", "우팡", "gem", null); });
            Sleep(1.9f);
            Shot("reveal_gem");
            Check(() =>
            {
                var c = RevealChest();
                return c != null && !c.HasLid && TextOf(F + "Rank") == "마지막 보물을 찾았어요" && TextOf(F + "Meta2/Value") == "3 / 3명";
            }, "보석 — 뚜껑 없는 모양도 같은 무대에 · 3등이 마지막이면 '마지막 보물을 찾았어요'");
            Back();
            Wait(() => !R0926HuntCelebrate.Showing, 2f, "뒤로가기 → 닫힘");
            Check(() => !R0926HuntCelebrate.Showing && RevealChest() == null && GameObject.Find("HuntRevealStage") == null && PushPending() == "0",
                "두근두근 중에도 뒤로가기 = 확인 — 바로 닫고 치움 · 그 게임 상세로");
            Do(() =>
            {
                SetPushRunning(false, true);
                R0926HuntApi.EditorBase = null;
            });
        }

        // 보물 모양 (C21 · 10-24) — 주최자가 고르는 8가지를 차례로 입혀 같은 각도에서 찍는다 · 사진은 액자에 · 3D 모델은 상자 폭에 맞춰 ·
        // 뚜껑 없는 모양은 열 때 빛만 솟음 · 사진을 못 받으면 보물상자로
        private const string ShapeGlb = "local://hunt-shapes/checker_box.glb", ShapeGlbFile = "Assets/Redesign0926/Editor/TestData/checker_box.glb";   // 그림 입힌 1.2×0.7×0.5 상자
        private static readonly Dictionary<string, string[]> ShapeMeshes = new Dictionary<string, string[]>
        {
            { "chest", new[] { "HuntChestBody", "HuntChestLid" } }, { "gift", new[] { "HuntGiftBody", "HuntGiftLid" } },
            { "egg", new[] { "HuntEggBody", "HuntEggLid" } }, { "gem", new[] { "HuntGem", null } }, { "star", new[] { "HuntStar", null } },
            { "heart", new[] { "HuntHeart", null } }, { "photo", new[] { "HuntPhotoFrame", null } }, { "model", new string[] { null, null } },
        };
        private static Texture2D hcShapeTex;

        private static void PlanHuntShapes()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);
            Do(() => { DeviceFov(); HcSpawn("HuntShapesShot"); });
            HcPlace(3.2f);
            Sleep(1.0f);
            foreach (var kind in ShapeMeshes.Keys)
            {
                string k = kind;
                if (k == "photo")
                    Do(() =>
                    {
                        hcShapeTex = new Texture2D(2, 2);
                        hcShapeTex.LoadImage(File.ReadAllBytes("Assets/Redesign0926/Splash0926_other/motif_kr.jpg"));
                        hcChest?.Wear("photo");
                        hcChest?.ShowPhoto(hcShapeTex);
                    });
                else if (k == "model")
                {
                    Do(() =>
                    {
                        GLBModelLoader.PreloadCache(ShapeGlb, File.ReadAllBytes(ShapeGlbFile));
                        hcChest?.Wear("model");
                        hcChest?.ShowModel(ShapeGlb);
                    });
                    Wait(() => hcChest != null && !hcChest.Loading && hcChest.Model != null, 20f, "3D 모델 붙음");
                    Check(() => hcChest != null && hcChest.Model != null && hcChest.Model.name != "GLB_LoadingSpinner", "불러온 모델이 로딩 고리가 아니라 GLB 모델 (예전 GetChild(0))");
                    Sleep(1.2f);   // 드러나는 연출(0.85배 → 1배, 1초)과 로딩 고리 걷힘이 끝난 뒤 잰다
                }
                else Do(() => hcChest?.Wear(k));
                Do(() => HcFace(-28f));
                Sleep(0.5f);
                Shot("shape_" + k);
                Check(() => ShapeOk(k), "모양 '" + k + "' — 몸통·뚜껑 메시 · 재질 수 = 부분 수" + (k == "photo" ? " · 사진 앞뒤" : k == "model" ? " · 상자 폭에 맞춤" : ""));
                Do(() => hcChest?.Lock(true));
                Sleep(0.3f);
                Shot("shape_lock_" + k);
                Check(() => LockOk(k), "모양 '" + k + "' + 비밀번호 — 은빛 자물쇠" + (k == "star" ? " (별 아래)" : " (앞면)"));
                Do(() => hcChest?.Lock(false));
                Check(() => hcChest != null && !hcChest.Locked && !hcChest.PadlockShown, "자물쇠를 떼면 바로 사라짐");
            }
            Check(() => hcChest != null && !hcChest.Wear("rocket") && hcChest.Kind == "model", "모르는 모양(새 앱이 만든 모양)은 그대로 둠");

            // 서버가 준 모양 — 사진·모델은 주소가 있어야 · 못 받으면 보물상자로
            Do(() =>
            {
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                hcChest?.Dress("gift", null);
                hcChest?.Dress("photo", "");
            });
            Check(() => hcChest != null && hcChest.Kind == "gift", "Dress: 선물상자는 바로 · 주소 없는 사진은 무시");
            Do(() => hcChest?.Dress("photo", "uploads/hunts/0/none.jpg"));
            Wait(() => hcChest != null && !hcChest.Loading && hcChest.Kind == "chest", 25f, "사진을 못 받으면 보물상자로");
            Do(() => R0926HuntApi.EditorBase = null);

            // 자물쇠 — 비밀번호가 달라 못 열면 덜컹 · 찾으면 고리가 딸깍 풀리고 떨어진 뒤 뚜껑이 열린다
            Do(() => { HcSpawn("HuntShapesLock"); hcChest?.Wear("chest"); hcChest?.Lock(true); });
            HcPlace(3.2f);
            Sleep(0.4f);
            Do(() => { HcFace(-28f); hcLockSwing = 0f; hcChest?.Jiggle(); });
            ShapeWatch(0.6f, () =>
            {
                var pad = hcChest != null ? hcChest.transform.Find("Padlock") : null;
                if (pad != null) hcLockSwing = Mathf.Max(hcLockSwing, Mathf.Abs(Mathf.DeltaAngle(0f, pad.localEulerAngles.z)));
            });
            Do(() => log.Add("  덜컹: 최대 " + hcLockSwing.ToString("0.0") + "°"));
            Check(() => hcChest != null && hcChest.Locked && hcChest.PadlockShown && hcLockSwing > 6f, "덜컹 — 자물쇠가 고리에 매달려 흔들림 · 그대로 잠김");
            Sleep(0.4f);
            Do(() => { hcLockSwing = 0f; hcChest?.Open(); });
            ShapeWatch(0.3f, () =>
            {
                var sh = hcChest != null ? hcChest.transform.Find("Padlock/Shackle") : null;
                if (sh != null) hcLockSwing = Mathf.Max(hcLockSwing, Mathf.Abs(Mathf.DeltaAngle(0f, sh.localEulerAngles.y)));
            });
            Shot("shape_unlock");
            Do(() => log.Add("  딸깍: 고리 " + hcLockSwing.ToString("0") + "° 돎"));
            Check(() => hcChest != null && !hcChest.Locked && hcLockSwing > 30f, "열면 먼저 자물쇠 고리가 딸깍 풀림");
            Wait(() => hcChest == null || !hcChest.PadlockShown, 1.5f, "풀린 자물쇠가 떨어져 사라짐");
            Check(() => hcChest != null && !hcChest.PadlockShown && hcChest.LidAngle > 60f, "풀린 자물쇠는 떨어져 사라지고 뚜껑이 열림 (상자가 작아져 사라지기 전)");

            // 열기 — 뚜껑 있는 모양은 뚜껑이, 없는 모양은 빛기둥·별빛만
            foreach (var kind in new[] { "gift", "egg", "gem", "heart" })
            {
                string k = kind;
                Do(() => { HcSpawn("HuntShapesOpen_" + k); hcChest?.Wear(k); });
                HcPlace(3.2f);
                Sleep(0.4f);
                Do(() => hcChest?.Open());
                Sleep(0.62f);
                Shot("shape_open_" + k);
                Check(() =>
                {
                    bool beam = hcGo != null && hcGo.transform.Find("Beam") != null;
                    log.Add("  열기 " + k + ": 뚜껑 " + (hcChest != null ? hcChest.LidAngle.ToString("0") : "-") + "° · 빛기둥 " + beam);
                    return hcChest != null && hcChest.Opening && beam && (ShapeMeshes[k][1] == null || hcChest.LidAngle > 100f);
                }, "열기 '" + k + "' — " + (ShapeMeshes[k][1] != null ? "뚜껑이 활짝 · 빛기둥" : "빛기둥·별빛"));
                Sleep(1.0f);
            }
            Do(() =>
            {
                if (hcGo != null) UnityEngine.Object.Destroy(hcGo);
                if (hcShapeTex != null) UnityEngine.Object.Destroy(hcShapeTex);
            });
        }

        // 보물 숨기기 확인 카드 (캔버스 맨 위 층 · R0926HuntCard 가 채운다)
        private const string HuntReview = "HuntReview0926";
        private static int huntPosted;
        // 확인 카드의 그 이름 줄 값 ('제목' · '상품' …) — 없으면 ""
        private static string ReviewRow(string label)
        {
            var rows = Find(HuntReview + "/Sheet0926/Rows");
            if (rows != null)
                foreach (Transform r in rows)
                    if (r.gameObject.activeSelf && r.Find("Label")?.GetComponent<Text>()?.text == label) return r.Find("Value")?.GetComponent<Text>()?.text ?? "";
            return "";
        }
        // 칩 줄에서 고른 칩(굵은 글자)의 값 — 하나가 아니면 ""
        private static string HuntChipOn(string box)
        {
            var t = Find(box);
            string on = null;
            if (t != null)
                foreach (Transform c in t)
                {
                    if (!c.gameObject.activeSelf || !c.name.StartsWith("Chip_") || c.Find("Label")?.GetComponent<Text>()?.fontStyle != FontStyle.Bold) continue;
                    if (on != null) return "";
                    on = c.name.Substring(5);
                }
            return on ?? "";
        }
        private static string HuntChipValues(string box)
        {
            var t = Find(box);
            var v = new List<string>();
            if (t != null) foreach (Transform c in t) if (c.gameObject.activeSelf && c.name.StartsWith("Chip_")) v.Add(c.name.Substring(5));
            return string.Join(",", v);
        }
        // 방금 숨긴 게임 번호 (확인 카드의 '확인'을 누르기 전까지)
        private static int HuntPostedId()
        {
            var card = UnityEngine.Object.FindAnyObjectByType<R0926HuntCard>(FindObjectsInactive.Include);
            return card != null && typeof(R0926HuntCard).GetField("postedId", NPI)?.GetValue(card) is int i ? i : 0;
        }
        // 개발 서버 응답 — 4xx 여도 본문을 (오류 이름 · host_id 를 보려고)
        private static string DevSend(string user, string path, string json = null)
        {
            using (var wc = new System.Net.WebClient())
            {
                wc.Encoding = System.Text.Encoding.UTF8;
                if (user != null) wc.Headers.Add("X-Test-User", user);
                wc.Headers.Add("Accept-Language", "ko-KR,ko;q=0.9");
                try
                {
                    if (json == null) return wc.DownloadString("http://127.0.0.1:5099" + path);
                    wc.Headers.Add("Content-Type", "application/json");
                    return wc.UploadString("http://127.0.0.1:5099" + path, json);
                }
                catch (System.Net.WebException e)
                {
                    if (e.Response == null) { log.Add("  개발 서버 " + path + " 실패: " + e.Message); return ""; }
                    using (var r = new StreamReader(e.Response.GetResponseStream(), System.Text.Encoding.UTF8)) return r.ReadToEnd();
                }
            }
        }
        // 근처 목록(/api/hunts/nearby)의 '근처 게임'에 그 번호가 있나 — 주최·참가 목록은 빼고 본다
        private static bool NearbyHas(string user, int id)
        {
            var v = VirtualLocation.Instance;
            string json = DevSend(user, "/api/hunts/nearby?lat=" + Deg(v.Latitude) + "&lon=" + Deg(v.Longitude));
            int a = json.IndexOf("\"hunts\":", StringComparison.Ordinal);
            if (a < 0) { log.Add("  근처 목록 못 받음 (" + user + ")"); return false; }
            int depth = 0, end = a;
            for (int i = json.IndexOf('[', a); i >= 0 && i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']' && --depth == 0) { end = i; break; }
            }
            return System.Text.RegularExpressions.Regex.IsMatch(json.Substring(a, end - a + 1), "\"id\":\\s*" + id + "[,}]");
        }

        private static void PushForget() => typeof(R0926HuntPush).GetField("lastKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, null);

        // 공유하기 · 공유 링크 · 누구에게 (10-26 대표님) — 시험 서버(127.0.0.1:5099)로:
        // ① 링크 읽기 ② 참가 코드 게임 → 숨긴 카드에 참가 코드 · 공유 글에 코드와 …/hunt/번호?c=코드 · 목록엔 안 나옴
        // ③ 다른 사람이 그 링크로 앱을 열면 바로 참가하고 상세 · 틀린 코드면 처음 화면 코드 칸에 넣어 두고 안내
        // ④ '팔로워 모두' — 팔로워·맞팔만 목록·참가 · 아닌 사람이 링크로 열면 주최자 프로필로 · 공유 페이지(/hunt/번호)
        // ⑤ 비밀번호 힌트 없이도 숨겨짐 (서버)
        private const string ShHost = "hunt_test_shost";
        private static int shCodeHunt, shFollowHunt;
        private static string shCode;
        private static void PlanHuntShare()
        {
            const string H = HuntCardPath, F = HfForm, RV = HuntReview;
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(1.0f);
            Do(() =>
            {
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = ShHost;
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(0.6f);

            // ① 링크 읽기
            Check(() =>
            {
                bool a = R0926HuntPush.TryParseLink("woopang://hunt?id=123&c=ab-c12x", out int i1, out string c1) && i1 == 123 && c1 == "ABC12X";
                bool b = R0926HuntPush.TryParseLink("https://woopang.com/hunt/77?c=QW12ER", out int i2, out string c2) && i2 == 77 && c2 == "QW12ER";
                bool c = R0926HuntPush.TryParseLink("https://www.woopang.com/hunt/8", out int i3, out string c3) && i3 == 8 && c3 == null;
                bool d = !R0926HuntPush.TryParseLink("https://evil.example/hunt/5?c=AAAAAA", out _, out _) && !R0926HuntPush.TryParseLink("woopang://auth?token=x", out _, out _)
                         && !R0926HuntPush.TryParseLink("woopang://hunt?id=-3", out _, out _) && !R0926HuntPush.TryParseLink("https://woopang.com/hunts/5", out _, out _);
                log.Add("  링크 읽기: " + a + " · " + b + " · " + c + " · " + d);
                return a && b && c && d;
            }, "링크 읽기 — woopang://hunt?id=…&c=… · https://(www.)woopang.com/hunt/번호?c=… (코드는 영문·숫자만 대문자로) · 다른 주소·로그인 링크는 무시");

            // ② 참가 코드 게임
            HfOpenForm();
            Do(() =>
            {
                Click(F + "AudienceChips/Chip_code");
                Click(F + "DurationChips/Chip_60");
                Find(F + "TitleInput").GetComponent<InputField>().text = "코드 시험 보물 (플레이 시험)";
                Find(F + "PrizeInput").GetComponent<InputField>().text = "";
                Find(F + "HintInput").GetComponent<InputField>().text = "";
            });
            Do(() => Click(F + "Submit"));
            Sleep(0.6f);
            Shot("share_code_review");
            Check(() => Active(RV) && ReviewRow("누구에게") == "참가 코드" && ReviewRow("참가 범위") == "코드를 받은 사람은 어디서든" && ReviewRow("상품") == "없음" && ReviewRow("힌트") == "없음"
                        && TextOf(RV + "/Sheet0926/Note").Contains("참가 코드가 만들어지고"),
                "참가 코드 게임 확인 카드 — 참가 범위 '코드를 받은 사람은 어디서든' · 상품·힌트 '없음' · 코드 안내");
            Do(() => { R0926Share.EditorLast = null; Click(RV + "/Sheet0926/Buttons/Go"); });
            Wait(() => TextOf(RV + "/Sheet0926/Head") == "보물을 숨겼어요!", 10f, "숨김 → 카드 '보물을 숨겼어요!'");
            Sleep(0.6f);
            Shot("share_code_done");
            Do(() => { shCodeHunt = HuntPostedId(); shCode = TextOf(RV + "/Sheet0926/CodeBox/Code"); log.Add("  코드 게임 " + shCodeHunt + " · 코드 " + shCode); });
            Check(() => shCodeHunt > 0 && Active(RV + "/Sheet0926/CodeBox") && System.Text.RegularExpressions.Regex.IsMatch(shCode ?? "", "^[A-Z0-9]{6}$")
                        && TextOf(RV + "/Sheet0926/CodeBox/L") == "참가 코드" && TextOf(RV + "/Sheet0926/Note").StartsWith("이 코드나 공유 링크를 받은 사람만"),
                "숨긴 카드에 참가 코드 6자 · '코드나 공유 링크를 받은 사람만 어디서든' 안내");
            Do(() => Click(RV + "/Sheet0926/Buttons/Share"));
            Do(() => log.Add("  공유 글: " + (R0926Share.EditorLast ?? "-").Replace("\n", " ⏎ ")));
            Check(() => R0926Share.EditorLast == "[우팡 보물찾기] 코드 시험 보물 (플레이 시험)\n참가 코드: " + shCode + "\nhttp://127.0.0.1:5099/hunt/" + shCodeHunt + "?c=" + shCode,
                "공유 글 — 제목 ⏎ 참가 코드 ⏎ …/hunt/번호?c=코드 (상품이 없으면 상품 줄 없음)");
            Check(() => !NearbyHas("hunt_test_s1", shCodeHunt), "참가 코드 게임은 다른 사람 근처 목록에 안 나옴");
            Do(() => Click(RV + "/Sheet0926/Buttons/Done"));
            Wait(() => Active(H + "Detail"), 8f, "확인 → 상세");
            Sleep(0.8f);
            Check(() => Active(H + "Detail/Viewport/Content/Share") && !Active(RV), "주최자 상세 — '공유하기' (참가 코드 게임도 주최자는 공유)");
            Do(() => { R0926Share.EditorLast = null; Click(H + "Detail/Viewport/Content/Share"); });
            Check(() => (R0926Share.EditorLast ?? "").EndsWith("/hunt/" + shCodeHunt + "?c=" + shCode), "주최자 상세의 공유하기도 코드가 든 링크");
            CloseUpload();

            // ③ 다른 사람이 공유 링크로 앱을 연다 — 코드가 들어가 바로 참가
            Do(() =>
            {
                R0926HuntApi.EditorTestUser = "hunt_test_s1";
                R0926HuntPush.OpenLink("woopang://hunt?id=" + shCodeHunt + "&c=" + shCode.ToLowerInvariant());
            });
            Wait(() => Active(H + "Detail") && DetailTitle().Contains("코드 시험 보물"), 15f, "링크 → 참가 → 상세");
            Sleep(0.6f);
            Shot("share_link_joined");
            Check(() => Active("UploadPage") && UploadSwipe()?.GetCurrentPanel() == 2 && TextOf(HuntCardToast) == "참가했어요!" && Has(DevSend("hunt_test_s1", "/api/hunts/" + shCodeHunt), "\"joined\":true"),
                "링크(woopang://hunt?id=…&c=…)로 열면 추가 창 보물찾기 카드 · '참가했어요!' · 서버에도 참가");
            Check(() => !Active(H + "Detail/Viewport/Content/Share"), "참가 코드 게임 — 참가자 상세엔 '공유하기' 없음 (코드는 주최자만 퍼뜨린다)");
            Do(() =>
            {
                R0926HuntApi.EditorTestUser = "hunt_test_s2";
                R0926HuntPush.OpenLink("https://woopang.com/hunt/" + shCodeHunt + "?c=ZZZZZZ");
            });
            Wait(() => Active(H + "Hub") && Find(H + "Hub/Viewport/Content/JoinCodeRow/JoinCodeInput")?.GetComponent<InputField>()?.text == "ZZZZZZ", 12f, "틀린 코드 링크 → 처음 화면");
            Sleep(0.4f);
            Shot("share_link_badcode");
            Check(() => TextOf(HuntCardToast) == R0926HuntApi.ErrorText("join_code", "ko") && !Has(DevSend("hunt_test_s2", "/api/hunts/" + shCodeHunt), "\"joined\":true"),
                "틀린 코드 링크 → 처음 화면 코드 칸에 그 코드 · 안내 · 참가 안 됨");
            CloseUpload();

            // ④ '팔로워 모두' — 시험 서버 팔로우: s2 맞팔 · s3 나를 팔로우 · s1 관계 없음
            Do(() => { R0926HuntApi.EditorTestUser = ShHost; DevSend(ShHost, "/api/dev/search_reset", "{}"); });
            HfOpenForm();
            Do(() =>
            {
                Click(F + "AudienceChips/Chip_follower");
                Click(F + "DurationChips/Chip_60");
                Find(F + "TitleInput").GetComponent<InputField>().text = "팔로워 시험 보물 (플레이 시험)";
                Find(F + "PrizeInput").GetComponent<InputField>().text = "아메리카노 (시험)";
            });
            Do(() => Click(F + "Submit"));
            Sleep(0.6f);
            Check(() => Active(RV) && ReviewRow("누구에게") == "팔로워 모두" && ReviewRow("참가 범위") == "10km" && TextOf(RV + "/Sheet0926/Note").StartsWith("나를 팔로우하는 사람만"),
                "팔로워 게임 확인 카드 — '팔로워 모두' · 참가 범위 10km");
            Do(() => { R0926Share.EditorLast = null; Click(RV + "/Sheet0926/Buttons/Go"); });
            Wait(() => TextOf(RV + "/Sheet0926/Head") == "보물을 숨겼어요!", 10f, "숨김");
            Sleep(0.6f);
            Shot("share_follow_done");
            Do(() => { shFollowHunt = HuntPostedId(); log.Add("  팔로워 게임 " + shFollowHunt); });
            Check(() => shFollowHunt > 0 && !Active(RV + "/Sheet0926/CodeBox") && TextOf(RV + "/Sheet0926/Note").StartsWith("나를 팔로우하는 사람에게만 보여요"),
                "숨긴 카드 — 코드 없음 · '팔로우하면 참가' 안내");
            Do(() => Click(RV + "/Sheet0926/Buttons/Share"));
            Check(() => (R0926Share.EditorLast ?? "").EndsWith("/hunt/" + shFollowHunt) && R0926Share.EditorLast.Contains("상품: 아메리카노 (시험)"), "팔로워 게임 공유 글 — 상품 · 링크(코드 없음)");
            Do(() => Click(RV + "/Sheet0926/Buttons/Done"));
            Wait(() => Active(H + "Detail"), 8f, "확인 → 상세");
            Check(() => NearbyHas("hunt_test_s3", shFollowHunt) && NearbyHas("hunt_test_s2", shFollowHunt) && !NearbyHas("hunt_test_s1", shFollowHunt),
                "근처 목록 — 나를 팔로우하는 사람(s3)·맞팔(s2)에겐 보이고 관계 없는 사람(s1)에겐 안 보임");
            Check(() => Has(DevSend("hunt_test_s1", "/api/hunts/" + shFollowHunt), "\"error\":\"followers_only\"", "\"host_id\":\"" + ShHost + "\"")
                        && !DevSend("hunt_test_s1", "/api/hunts/" + shFollowHunt).Contains("팔로워 시험 보물"),
                "팔로우 전 상세 요청 → followers_only + 주최자만 (제목은 안 줌)");
            Check(() => Has(DevSend("hunt_test_s1", "/api/hunts/" + shFollowHunt + "/join", "{\"lat\":" + Deg(VirtualLocation.Instance.Latitude) + ",\"lon\":" + Deg(VirtualLocation.Instance.Longitude) + "}"), "\"error\":\"followers_only\"")
                        && Has(DevSend("hunt_test_s3", "/api/hunts/" + shFollowHunt + "/join", "{\"lat\":" + Deg(VirtualLocation.Instance.Latitude) + ",\"lon\":" + Deg(VirtualLocation.Instance.Longitude) + "}"), "\"ok\":true"),
                "참가 — 관계 없는 사람은 followers_only · 나를 팔로우하는 사람은 참가됨");
            Check(() =>
            {
                string page = DevSend(null, "/hunt/" + shFollowHunt);
                bool ok = page.Contains("팔로워에게 열린 보물찾기") && page.Contains("팔로우하면 참가할 수 있어요") && !page.Contains("팔로워 시험 보물") && !page.Contains("아메리카노");
                string codePage = DevSend(null, "/hunt/" + shCodeHunt), codeFull = DevSend(null, "/hunt/" + shCodeHunt + "?c=" + shCode);
                ok &= codePage.Contains("초대받은 사람만 참가하는 보물찾기") && !codePage.Contains("코드 시험 보물") && !codePage.Contains(shCode)
                      && codeFull.Contains("코드 시험 보물") && codeFull.Contains(shCode) && codeFull.Contains("woopang://hunt?id=" + shCodeHunt + "&amp;c=" + shCode);
                return ok;
            }, "공유 페이지 — 팔로워 게임은 제목·상품 없이 '팔로우하면 참가' · 코드 게임은 맞는 ?c= 가 있어야 제목·코드 · 앱 열기 링크에 코드");
            CloseUpload();
            Do(() =>
            {
                R0926HuntApi.EditorTestUser = "hunt_test_s1";
                R0926HuntCard.EditorFollowHost = null;
                R0926HuntPush.OpenLink("https://woopang.com/hunt/" + shFollowHunt);
            });
            Wait(() => Active("UploadPage") && TextOf(HuntCardToast).StartsWith("주최자를 팔로우한 사람만 참가할 수 있어요"), 12f, "팔로우 전 링크 → 안내");
            Sleep(0.2f);
            Shot("share_follow_link");
            Check(() => TextOf(HuntCardToast) == "주최자를 팔로우한 사람만 참가할 수 있어요 — 프로필에서 팔로우해 주세요" && !Active(H + "Detail"),
                "팔로우 전 사람이 링크로 열면 — '프로필에서 팔로우해 주세요' (상세는 안 열림)");
            Wait(() => R0926HuntCard.EditorFollowHost == ShHost, 5f, "주최자 프로필 열기");
            Check(() => R0926HuntCard.EditorFollowHost == ShHost, "안내 뒤 주최자 프로필을 연다 (팔로우하면 참가)");
            Do(() =>
            {
                R0926HuntApi.EditorTestUser = "hunt_test_s3";
                PushForget();   // 같은 링크를 다른 사람으로 — 10초 안 같은 링크는 한 번으로 친다
                R0926HuntPush.OpenLink("woopang://hunt?id=" + shFollowHunt);
            });
            Wait(() => Active(H + "Detail") && DetailTitle().Contains("팔로워 시험 보물"), 12f, "팔로워는 링크 → 상세");
            Check(() => Active(H + "Detail") && !Active(H + "Detail/Viewport/Content/Share"), "나를 팔로우하는 사람은 링크로 상세 · 참가자 '공유하기'는 '모두' 게임만");
            CloseUpload();

            // ⑤ 비밀번호 힌트 없이 (서버도 선택)
            Check(() => Has(DevSend("hunt_test_spw", "/api/hunts/rules/accept", "{\"version\":2}"), "\"ok\":true")
                        && Has(DevSend("hunt_test_spw", "/api/hunts", "{\"title\":\"힌트 없는 비밀번호 (시험)\",\"mode\":\"code\",\"code\":\"1234\",\"question\":\"\",\"winners\":1,\"radius_km\":5,\"visible_m\":50,\"lat\":"
                                                              + Deg(VirtualLocation.Instance.Latitude) + ",\"lon\":" + Deg(VirtualLocation.Instance.Longitude) + ",\"acc\":5,\"duration_min\":60}"), "\"ok\":true"),
                "서버 — 비밀번호 힌트 없이도 숨겨짐 (힌트는 선택)");
            Do(() => { R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; });
        }

        // 숨기기 양식의 보물 모양 (10-24 대표님 — 코드로 만든 모양을 고르고, 사진·3D 모델은 올려서. 숨기기 전에 미리보기).
        // 10-26: 모양은 다섯(보물상자·선물상자·하트·사진·3D 모델) · 비밀번호 힌트는 선택 · 숨기기는 확인 카드를 거친다.
        // 시험 서버(127.0.0.1:5099)로: 모양 칩 5개 → 미리보기 칸이 그 모양을 찍는지 → 사진 고름(고르기 창 대신 EditorPick) → 숨기기(올린 주소가
        // 게임에 붙음) → 내 AR 보물이 그 사진 액자로 → 다시 양식: 못 여는 GLB 는 되돌림 → 3D 모델로 숨기기 → AR 보물에 그 모델
        private static R0926HuntCard hfCard;
        private static R0926HuntShapePreview hfPreview;
        private const string HfForm = HuntCardPath + "Form/Viewport/Content/";
        private static void PlanHuntForm()
        {
            const string H = HuntCardPath, F = HfForm;
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
            HfOpenForm();
            Do(() =>
            {
                hfCard = UnityEngine.Object.FindAnyObjectByType<R0926HuntCard>(FindObjectsInactive.Include);
                hfPreview = Find(F + "ShapePreview")?.GetComponent<R0926HuntShapePreview>();
            });
            Check(() => ActiveChildren(F + "ShapeChips") == 5 && TextOf(F + "LShape") == "보물 모양", "'보물 모양' 칩 5개 (보물상자·선물상자·하트·사진·3D 모델)");
            Check(() => !Active(F + "PickButton") && !Active(F + "PickCaption"), "보물상자 — 고르기 버튼 없음");
            Do(() => ScrollToChild(F + "LShape"));
            Wait(() => hfPreview != null && hfPreview.Chest != null && hfPreview.EditorTexture != null, 8f, "미리보기 칸이 찍기 시작");
            Sleep(0.8f);
            Shot("hf_chest");
            Check(() => hfPreview != null && hfPreview.Chest.Kind == "chest" && HfInk(), "미리보기 — 보물상자가 칸에 그려짐");
            Check(() => HfStageHidden(), "미리보기 무대는 칸의 카메라가 찍을 때만 켜짐 (AR 화면엔 안 보임)");

            // 비밀번호 — 칩을 고르면 칸이 열리고 미리보기 보물에 자물쇠 · 4자 · 힌트는 선택 (10-26)
            Do(() => ScrollToChild(F + "ModeChips"));
            Do(() => Click(F + "ModeChips/Chip_code"));
            Sleep(0.5f);
            Do(() => ScrollToChild(F + "LShape"));
            Sleep(0.4f);
            Shot("hf_lock_chest");
            Check(() => Active(F + "CodeBox") && hfPreview.Locked && hfPreview.Chest.Locked && hfPreview.Chest.PadlockShown && HfInk(),
                "'비밀번호' 칩 → 비밀번호·힌트 칸 · 미리보기 보물에 자물쇠");
            Check(() => TextOf(F + "CodeBox/LCode") == "비밀번호 (4자) — 보물 앞에서 넣을 답" && TextOf(F + "CodeBox/LQuestion") == "비밀번호 힌트 (선택)"
                        && Find(F + "CodeBox/CodeInput")?.GetComponent<InputField>()?.characterLimit == 4, "칸 이름 '비밀번호 (4자)' · '비밀번호 힌트 (선택)' · 4자까지만 들어감");
            Do(() =>
            {
                Find(F + "TitleInput").GetComponent<InputField>().text = "비밀번호 보물 (플레이 시험)";
                Find(F + "CodeBox/CodeInput").GetComponent<InputField>().text = "123";
                Find(F + "CodeBox/QuestionInput").GetComponent<InputField>().text = "";
                Click(F + "Submit");
            });
            Sleep(0.4f);
            Check(() => Active(F + "Submit") && TextOf(F + "Msg") == "비밀번호는 4자로 정해 주세요", "3자 → '비밀번호는 4자로 정해 주세요'");
            Do(() =>
            {
                Find(F + "CodeBox/CodeInput").GetComponent<InputField>().text = "1234";
                Click(F + "Submit");
            });
            Sleep(0.5f);
            Shot("hf_review_code");
            Check(() => Active(HuntReview) && ReviewRow("찾는 방식") == "비밀번호 1234" && TextOf(F + "Msg") == "",
                "비밀번호 힌트 없이도 확인 카드로 (힌트는 선택) · '비밀번호 1234' · 앞서 틀렸던 안내는 지워짐");
            Do(() => Click(HuntReview + "/Sheet0926/Buttons/Edit"));
            Sleep(0.3f);
            Do(() => { Find(F + "CodeBox/QuestionInput").GetComponent<InputField>().text = "가게 간판 아래 숫자"; Click(F + "Submit"); });
            Sleep(0.5f);
            Check(() => Active(HuntReview) && ReviewRow("찾는 방식") == "비밀번호 1234\n힌트 가게 간판 아래 숫자", "비밀번호 힌트를 쓰면 확인 카드에 함께");
            Do(() => Click(HuntReview + "/Sheet0926/Buttons/Edit"));
            Sleep(0.3f);
            Check(() => !Active(HuntReview) && Active(F + "CodeBox") && HfCurrent("id") == "", "고치기 → 양식으로 (숨기지 않음)");
            Do(() => Find(F + "CodeBox/QuestionInput").GetComponent<InputField>().text = "");
            Do(() => Click(F + "ModeChips/Chip_touch"));
            Sleep(0.4f);
            Check(() => !Active(F + "CodeBox") && !hfPreview.Locked && !hfPreview.Chest.PadlockShown, "'먼저 터치'로 돌리면 칸이 닫히고 자물쇠도 떨어짐");
            Do(() => Find(F + "TitleInput").GetComponent<InputField>().text = "");
            Do(() => ScrollToChild(F + "LShape"));
            Sleep(0.4f);
            foreach (var kind in new[] { "gift", "heart", "chest" })
            {
                string k = kind;
                Do(() => Click(F + "ShapeChips/Chip_" + k));
                Sleep(0.5f);
                Shot("hf_" + k);
                Check(() => hfPreview.Chest.Kind == k && HfInk() && !Active(F + "PickButton"), "모양 '" + k + "' 누름 → 미리보기도 '" + k + "'");
            }

            // 사진 — 고르기 전엔 빈 액자, 고르면 그 사진. 숨기면 올리고 그 주소가 게임에 붙는다
            Do(() => Click(F + "ShapeChips/Chip_photo"));
            Sleep(0.5f);
            Shot("hf_photo_empty");
            Check(() => hfPreview.Chest.Kind == "photo" && hfPreview.Chest.Photo == null && Active(F + "PickButton")
                        && TextOf(F + "PickButton/Label") == "앨범에서 사진 고르기" && TextOf(F + "PickCaption").Contains("액자"),
                "사진 칸 — 빈 액자 · '앨범에서 사진 고르기' · 안내");
            Do(() => Find(F + "TitleInput").GetComponent<InputField>().text = "사진 보물 (플레이 시험)");
            Do(() => Click(F + "Submit"));
            Sleep(0.4f);
            Check(() => Active(F + "Submit") && TextOf(F + "Msg") == "보물로 쓸 사진을 골라 주세요" && !Active(HuntReview), "사진 없이 숨기기 → '보물로 쓸 사진을 골라 주세요' (확인 카드 안 뜸)");
            Do(() => hfCard.EditorPick("photo", File.ReadAllBytes("Assets/Redesign0926/Splash0926_other/motif_kr.jpg")));
            Sleep(0.6f);
            Shot("hf_photo");
            Check(() => hfPreview.Chest.Kind == "photo" && hfPreview.Chest.Photo != null && HfInk(), "사진 고름 → 미리보기 액자에 그 사진");
            Check(() => TextOf(F + "PickButton/Label") == "다른 사진 고르기" && System.Text.RegularExpressions.Regex.IsMatch(TextOf(F + "PickCaption"), @"^사진 \d+×\d+ · \d+(KB|\.\dMB)$"),
                "'다른 사진 고르기' · '사진 W×H · 크기'");
            Do(() => log.Add("  고른 사진: " + TextOf(F + "PickCaption")));
            Do(() => Click(F + "Submit"));
            Sleep(0.4f);
            Check(() => Active(HuntReview) && ReviewRow("보물 모양") == "사진", "숨기기 → 확인 카드 (보물 모양 '사진')");
            Do(() => { Click(HuntReview + "/Sheet0926/Buttons/Go"); log.Add("  누른 직후: " + TextOf(HuntReview + "/Sheet0926/Buttons/Go/Label") + " · " + TextOf(F + "Submit/Label")); });
            Check(() => TextOf(HuntReview + "/Sheet0926/Buttons/Go/Label") == "올리는 중…" && TextOf(F + "Submit/Label") == "올리는 중…", "카드의 '숨기기' 누름 → 먼저 '올리는 중…'");
            Wait(() => TextOf(HuntReview + "/Sheet0926/Head") == "보물을 숨겼어요!", 20f, "올리고 숨김 → 카드 '보물을 숨겼어요!'");
            Do(() => Click(HuntReview + "/Sheet0926/Buttons/Done"));
            Wait(() => Active(H + "Detail"), 10f, "확인 → 상세");
            Sleep(1.0f);
            Shot("hf_photo_detail");
            Check(() => HfCurrent("object_kind") == "photo" && System.Text.RegularExpressions.Regex.IsMatch(HfCurrent("object_url"), @"^/?uploads/hunts/hunt_test_host/[0-9a-f]{16}\.jpg$"), "게임에 사진 모양 · 올린 주소 (uploads/hunts/<주최자>/…jpg)");
            Check(() => HfServedJpeg(HfCurrent("object_url")), "올린 사진을 서버가 내줌 (JPEG · 긴 변 1280 이하)");
            Do(() => log.Add("  게임: " + HfCurrent("object_kind") + " · " + HfCurrent("object_url")));
            HfArChest("photo");

            // 다시 양식 — 고른 사진은 그 게임 것이라 비우고 보물상자로. 3D 모델: 못 여는 GLB 는 되돌리고, 열리는 것으로 숨긴다
            HfOpenForm();
            Check(() => hfPreview.Chest.Kind == "chest" && !Active(F + "PickButton") && HfPicked("shape") == "chest", "숨긴 뒤 다시 양식 — 보물상자로 돌아옴 (고른 사진은 비움)");
            Do(() => ScrollToChild(F + "LShape"));
            Do(() => Click(F + "ShapeChips/Chip_model"));
            Sleep(0.5f);
            Check(() => hfPreview.Chest.Kind == "chest" && TextOf(F + "PickButton/Label") == "3D 모델(GLB) 고르기" && TextOf(F + "PickCaption").Contains("10MB"),
                "3D 모델 칸 — 고르기 전엔 보물상자 · 'GLB 고르기' · '10MB 까지'");
            Do(() => hfCard.EditorPick("model", System.Text.Encoding.ASCII.GetBytes("not a glb file at all")));
            Sleep(0.3f);
            Do(() => log.Add("  GLB 아님: " + TextOf(F + "PickCaption")));
            Check(() => TextOf(F + "PickCaption") == R0926HuntApi.ErrorText("file_type", "ko") && TextOf(F + "PickButton/Label") == "3D 모델(GLB) 고르기", "GLB 가 아닌 파일 → 고르기 칸 아래 안내 · 고른 것 없음");
            Do(() => hfCard.EditorPick("model", HfBrokenGlb()));
            Wait(() => TextOf(F + "PickCaption").Contains("열 수 없어요"), 20f, "속이 깨진 GLB → 안내");
            Sleep(0.3f);
            Shot("hf_model_broken");
            Check(() => TextOf(F + "PickCaption").Contains("열 수 없어요") && hfPreview.Chest.Kind == "chest" && TextOf(F + "PickButton/Label") == "3D 모델(GLB) 고르기",
                "앞머리만 맞고 속이 깨진 GLB → '열 수 없어요' · 고른 것 비움 · 보물상자");
            Do(() => hfCard.EditorPick("model", File.ReadAllBytes(ShapeGlbFile)));
            Wait(() => hfPreview.Chest.Kind == "model" && !hfPreview.Loading && hfPreview.Chest.Model != null, 20f, "미리보기에 3D 모델");
            Sleep(1.4f);   // 드러나는 연출이 끝난 뒤
            Shot("hf_model");
            Check(() => hfPreview.Chest.Kind == "model" && hfPreview.Chest.Model != null && hfPreview.Chest.Model.name != "GLB_LoadingSpinner" && HfInk(), "GLB 고름 → 미리보기에 그 모델");
            Check(() => TextOf(F + "PickButton/Label") == "다른 3D 모델 고르기" && TextOf(F + "PickCaption").StartsWith("3D 모델 · "), "'다른 3D 모델 고르기' · '3D 모델 · 크기'");
            Check(() => HfStageHidden(), "모델도 칸의 카메라가 찍을 때만 켜짐");
            Do(() => Find(F + "TitleInput").GetComponent<InputField>().text = "3D 모델 보물 (플레이 시험)");
            Do(() => Click(F + "Submit"));
            Sleep(0.4f);
            Check(() => Active(HuntReview) && ReviewRow("보물 모양") == "3D 모델", "숨기기 → 확인 카드 (보물 모양 '3D 모델')");
            Do(() => Click(HuntReview + "/Sheet0926/Buttons/Go"));
            Wait(() => TextOf(HuntReview + "/Sheet0926/Head") == "보물을 숨겼어요!", 20f, "올리고 숨김 → 카드 '보물을 숨겼어요!'");
            Do(() => Click(HuntReview + "/Sheet0926/Buttons/Done"));
            Wait(() => Active(H + "Detail"), 10f, "확인 → 상세");
            Sleep(1.0f);
            Check(() => HfCurrent("object_kind") == "model" && System.Text.RegularExpressions.Regex.IsMatch(HfCurrent("object_url"), @"^/?uploads/hunts/hunt_test_host/[0-9a-f]{16}\.glb$"), "게임에 3D 모델 모양 · 올린 주소 (…glb)");
            Do(() => log.Add("  게임: " + HfCurrent("object_kind") + " · " + HfCurrent("object_url")));
            HfArChest("model");
            Do(() => R0926HuntApi.EditorBase = null);
        }
        // 추가 창 → 보물찾기 카드 → 숨기기 (처음 주최면 수칙 동의)
        private static void HfOpenForm()
        {
            const string H = HuntCardPath;
            Do(() => { if (!Active("UploadPage")) Click("Dock0926/PlusButton"); });
            Sleep(1.2f);
            Do(() => UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Exclude)?.SwitchToPanel(2));
            Sleep(1.5f);
            Do(() => UnityEngine.Object.FindAnyObjectByType<R0926HuntCard>(FindObjectsInactive.Include)?.ShowHub());
            Sleep(1.0f);
            Do(() => Click(H + "Hub/Viewport/Content/Hero"));
            Sleep(2.0f);
            Do(() => { if (Active(H + "Rules")) Click(H + "Rules/Viewport/Content/Agree"); });
            Sleep(0.3f);
            Do(() => { if (Active(H + "Rules")) Click(H + "Rules/Viewport/Content/AgreeButton"); });
            Wait(() => Active(H + "Form"), 8f, "숨기기 양식");
            Sleep(0.6f);
        }
        // 숨긴 게임의 내 AR 보물 — 주최자는 자기 보물이 보인다. 추가 창을 닫고 그 보물이 고른 모양으로 붙는지
        private static void HfArChest(string kind)
        {
            Do(() =>
            {
                int.TryParse(HfCurrent("id"), out arHunt);   // 주최자는 근접 확인을 안 해 NearHuntId 가 0
                log.Add("  AR 로 볼 게임 " + arHunt);
                var v = VirtualLocation.Instance;
                v.SetCoordinates(v.Latitude - 0.00007f, v.Longitude);   // 8m 남쪽에서 본다 — 숨긴 자리에 서 있으면 보물이 화면을 덮는다
            });
            CloseUpload();
            Wait(() => HfArKind() == kind && HfArLoaded(), 40f, "AR 보물이 '" + kind + "' 모양으로");
            Sleep(1.4f);
            Do(() => FaceTreasure(arHunt));
            Sleep(0.6f);
            Shot("hf_ar_" + kind);
            Check(() => HfArKind() == kind && HfArLoaded(), "내 AR 보물 — '" + kind + "' (" + (kind == "photo" ? "올린 사진 액자" : "올린 3D 모델") + ")");
        }
        private static R0926HuntChest HfArChestOf() =>
            arHunt != 0 && R0926HuntTreasure.SpawnedObjects.TryGetValue("hunt_" + arHunt, out var go) && go != null ? go.GetComponentInChildren<R0926HuntChest>(true) : null;
        private static string HfPicked(string key) =>
            hfCard != null && typeof(R0926HuntCard).GetField("picked", NPI)?.GetValue(hfCard) is Dictionary<string, string> d && d.TryGetValue(key, out var v) ? v : "";
        private static string HfArKind() => HfArChestOf()?.Kind ?? "";
        private static bool HfArLoaded()
        {
            var c = HfArChestOf();
            return c != null && !c.Loading && (c.Kind == "photo" ? c.Photo != null : c.Kind != "model" || (c.Model != null && c.Model.name != "GLB_LoadingSpinner"));
        }
        // 상세에 띄운 게임의 값 (SimpleJSON 이름이 다른 dll 과 겹쳐 리플렉션으로)
        private static string HfCurrent(string key)
        {
            var node = hfCard != null ? typeof(R0926HuntCard).GetField("current", NPI)?.GetValue(hfCard) : null;
            var v = node?.GetType().GetProperty("Item", new[] { typeof(string) })?.GetValue(node, new object[] { key });
            return v == null ? "" : v.ToString().Trim('"').Replace("\\/", "/");
        }
        private static bool HfServedJpeg(string url)
        {
            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    var d = wc.DownloadData("http://127.0.0.1:5099/" + url.TrimStart('/'));
                    var t = new Texture2D(2, 2);
                    bool ok = d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && t.LoadImage(d) && Mathf.Max(t.width, t.height) <= 1280;
                    log.Add("  받은 사진 " + d.Length + "B · " + t.width + "x" + t.height);
                    UnityEngine.Object.Destroy(t);
                    return ok;
                }
            }
            catch (Exception e) { log.Add("  사진 받기 실패: " + e.Message); return false; }
        }
        // 앞머리(glTF 2 · 길이)는 맞고 속(JSON)이 깨진 GLB — 서버·앱의 앞머리 검사는 통과하고 불러오기에서 실패한다
        private static byte[] HfBrokenGlb()
        {
            var js = System.Text.Encoding.ASCII.GetBytes("{\"asset\":{\"version\":\"2.0\"},\"meshes\":[{]}");
            int pad = (4 - js.Length % 4) % 4, n = js.Length + pad;
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(0x46546C67u); w.Write(2u); w.Write((uint)(12 + 8 + n));
            w.Write((uint)n); w.Write(0x4E4F534Au); w.Write(js);
            for (int i = 0; i < pad; i++) w.Write((byte)0x20);
            w.Flush();
            return ms.ToArray();
        }
        // 칸이 찍은 그림에 무대색이 아닌 점이 충분한지 (모양이 실제로 그려졌는지)
        private static bool HfInk()
        {
            var rt = hfPreview != null ? hfPreview.EditorTexture : null;
            if (rt == null) return false;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            var bg = px[2 * rt.width + 2];
            int n = 0;
            foreach (var c in px) if (Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b) > 40) n++;
            UnityEngine.Object.Destroy(tex);
            float share = n / (float)px.Length;
            log.Add("  미리보기 " + rt.width + "x" + rt.height + " · 그려진 점 " + (share * 100f).ToString("0.0") + "%");
            return share > 0.02f && share < 0.7f;
        }
        private static bool HfStageHidden()
        {
            var st = GameObject.Find("HuntShapePreviewStage");
            if (st == null) return false;
            int on = 0, all = 0;
            foreach (var r in st.GetComponentsInChildren<Renderer>(true)) { all++; if (r.enabled) on++; }
            log.Add("  무대 렌더러 " + all + "개 중 켜진 것 " + on);
            return all > 0 && on == 0;
        }

        // 일반 장소 3D 모델 (0001_GLB · DataManager.SetupGLBObject 와 같은 길) — 09-07 로딩 고리가 생긴 뒤로 고리를 모델로 집어
        // 배율·색(셰이더)·콜라이더·애니메이션이 고리에 걸리고, 다시 쓸 때 앞 모델이 안 지워지던 것 (10-24 고침)
        private static GameObject glbObj;
        private static bool glbDone, glbOk;
        private static void PlanGlbLoad()
        {
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Sleep(0.6f);
            Do(() =>
            {
                DeviceFov();
                glbObj = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Download/0001_GLB.prefab"));
                glbObj.name = "GlbLoadShot";
                var cam = Camera.main;
                Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
                glbObj.transform.position = cam.transform.position + f * 4f + Vector3.down * 0.6f;
                glbObj.SetActive(true);   // 풀에 꺼진 채 들어 있는 프리팹
                glbObj.AddComponent<GLBModelLoader>();   // DataManager 처럼 뿌리에
            });
            Sleep(0.2f);   // Start 가 GLBContainer 를 잡는다
            Do(() =>
            {
                glbDone = glbOk = false;
                GLBModelLoader.PreloadCache(ShapeGlb, File.ReadAllBytes(ShapeGlbFile));
                var ld = glbObj.GetComponent<GLBModelLoader>();
                ld.StartCoroutine(ld.LoadGLBModelCoroutine(ShapeGlb, 1.5f, r => { glbOk = r; glbDone = true; }));
            });
            Wait(() => glbDone, 20f, "불러오기 끝");
            Check(() =>
            {
                var m = glbObj != null ? glbObj.GetComponent<GLBModelLoader>().GetLoadedModel() : null;
                log.Add("  불러온 모델 " + (m != null ? m.name : "없음"));
                return glbOk && m != null && m.name == "CheckerBox";
            }, "불러온 모델이 로딩 고리가 아니라 GLB 모델");
            Sleep(1.4f);   // 드러나는 연출 · 콜라이더(나눠서 붙임)가 끝나게
            Shot("glb_lion");
            Check(() =>
            {
                var ld = glbObj.GetComponent<GLBModelLoader>();
                var m = ld.GetLoadedModel();
                var mr = m != null ? m.GetComponentInChildren<MeshRenderer>() : null;
                var box = glbObj.transform.Find("GLBContainer")?.GetComponent<BoxCollider>();
                var mat = mr != null ? mr.sharedMaterial : null;
                bool tex = mat != null && mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null;
                log.Add("  배율 " + (m != null ? m.transform.localScale.x.ToString("0.00") : "-") + " · 셰이더 " + (mat != null ? mat.shader.name : "-") + " · 그림 " + tex
                        + " · 모델 콜라이더 " + (mr != null && mr.GetComponent<MeshCollider>() != null) + " · 누르는 상자가 모델을 감쌈 " + (box != null && mr != null && box.bounds.Contains(mr.bounds.center)));
                return ld.HasLoadedModel() && Mathf.Abs(m.transform.localScale.x - 1.5f) < 0.01f && mat != null && mat.shader.name.Contains("Universal Render Pipeline") && tex
                       && mr.GetComponent<MeshCollider>() != null && box != null && box.bounds.Contains(mr.bounds.center);
            }, "모델에 배율 1.5 · URP 셰이더로 바꿔도 바탕 그림 유지 · 모델 콜라이더 · 누르는 상자가 모델을 감쌈 · 연출 뒤에도 모델을 쥐고 있음");
            Do(() => glbObj.GetComponent<GLBModelLoader>().ClearModel());
            Sleep(0.1f);
            Check(() =>
            {
                var c = glbObj.transform.Find("GLBContainer");
                int left = 0;
                foreach (Transform t in c) if (t.name == "CheckerBox") left++;
                return left == 0;
            }, "다시 쓸 때(ClearModel) 앞 모델이 지워짐");
            Do(() => UnityEngine.Object.Destroy(glbObj));
        }

        private static float hcLockSwing;
        // 축하 화면 연출 중 보이는 글자 (카드 · 확인 버튼 말고) — 두근두근·짜잔 글자는 10-26 에 없앴다
        private static string StageWords(string content)
        {
            var root = Find(content.TrimEnd('/'));
            if (root == null) return "-";
            var card = root.Find("Card"); var ok = root.Find("OK");
            var words = new List<string>();
            foreach (var t in root.GetComponentsInChildren<Text>(false))
                if ((card == null || !t.transform.IsChildOf(card)) && (ok == null || !t.transform.IsChildOf(ok)) && t.text.Trim().Length > 0) words.Add(t.text);
            return string.Join(" · ", words);
        }
        // 보물찾기 처음 화면 목록 — 그 제목 줄의 자물쇠 (-1 줄 없음 · 0 없음 · 1 있음)
        private static int HubRowLock(string title)
        {
            var hub = Find(HuntCardPath + "Hub");
            if (hub == null) return -1;
            foreach (var t in hub.GetComponentsInChildren<Text>(false))
                if (t.name == "Title" && t.text.EndsWith(title) && t.transform.parent.Find("Lock") is Transform lk) return lk.gameObject.activeSelf ? 1 : 0;
            return -1;
        }
        // 장소 목록에서 그 제목 보물 줄 앞의 그림 ("" 줄 없음)
        private static string HuntRowIcon(string title)
        {
            foreach (var row in ListRows())
            {
                var n = row.Find("Name")?.GetComponent<Text>();
                if (n == null || !n.text.Contains(title)) continue;
                var d = row.Find("Dot")?.GetComponent<Image>();
                return d != null && d.sprite != null ? d.sprite.name : "-";
            }
            return "";
        }
        // 지도 보물 핀의 자물쇠 (-1 핀 없음 · 0 없음 · 1 있음)
        private static int MapPinLock(int id)
        {
            var list = Find("ListPanel");
            if (list == null) return -1;
            foreach (var t in list.GetComponentsInChildren<Transform>(false))
                if (t.name == "Hunt_" + id) { var lk = t.Find("Ring/Lock"); return lk != null && lk.gameObject.activeSelf ? 1 : 0; }
            return -1;
        }
        // sec 초 동안 매 프레임 지켜본다 (흔들림·고리 각도의 정점을 놓치지 않게)
        private static void ShapeWatch(float sec, Action each)
        {
            double until = -1;
            steps.Enqueue(() =>
            {
                if (until < 0) until = EditorApplication.timeSinceStartup + sec;
                each();
                return EditorApplication.timeSinceStartup >= until;
            });
        }
        // 자물쇠 — 걸렸고 · 보이고 · 몸통과 같이 그려지고 · 앞면(-Z)에 (별은 아래에 매달림)
        private static bool LockOk(string kind)
        {
            if (hcChest == null) return false;
            var pad = hcChest.transform.Find("Padlock");
            var r = pad != null ? pad.Find("Body")?.GetComponent<MeshRenderer>() : null;
            if (pad == null || r == null) { log.Add("  " + kind + ": 자물쇠 없음"); return false; }
            log.Add("  " + kind + " 자물쇠: 자리 " + pad.localPosition.ToString("F3") + " · 기울기 " + Mathf.DeltaAngle(0f, pad.localEulerAngles.x).ToString("0") + "° · 배율 "
                    + pad.localScale.x.ToString("0.00") + " · 그림 " + r.enabled);
            return hcChest.Locked && hcChest.PadlockShown && r.enabled && (kind == "star" ? pad.localPosition.y < -0.2f : pad.localPosition.z < -0.05f);
        }
        private static void HcSpawn(string name)
        {
            if (hcGo != null) UnityEngine.Object.Destroy(hcGo);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Redesign0926/Prefabs/HuntTreasure0926.prefab");
            hcGo = prefab != null ? UnityEngine.Object.Instantiate(prefab) : null;
            if (hcGo != null) hcGo.name = name;
            hcChest = hcGo != null ? hcGo.GetComponentInChildren<R0926HuntChest>() : null;
            hcRest = hcChest != null ? hcChest.transform.localScale.x : 1f;
        }
        private static void HcPlace(float d) => steps.Enqueue(() =>
        {
            var cam = Camera.main;
            if (hcGo == null || cam == null) return true;
            Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
            hcGo.transform.position = cam.transform.position + f * d + Vector3.down * 0.35f;
            return true;
        });
        // 도는 걸 멈추고 정면(-Z)에서 off 도 비껴 — 모양끼리 같은 각도로 견주어 보게
        private static void HcFace(float off)
        {
            var cam = Camera.main;
            if (hcChest == null || cam == null) return;
            Vector3 f = cam.transform.forward; f.y = 0f;
            typeof(R0926HuntChest).GetField("spinSpeed", NPI)?.SetValue(hcChest, 0f);
            typeof(R0926HuntChest).GetField("yaw", NPI)?.SetValue(hcChest, Quaternion.LookRotation(f).eulerAngles.y + off);
        }
        private static bool ShapeOk(string kind)
        {
            if (hcChest == null) return false;
            var mf = hcChest.GetComponent<MeshFilter>(); var mr = hcChest.GetComponent<MeshRenderer>();
            var lf = hcChest.transform.Find("Lid")?.GetComponent<MeshFilter>();
            string b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : null, l = lf != null && lf.sharedMesh != null ? lf.sharedMesh.name : null;
            int mats = mr != null ? mr.sharedMaterials.Length : -1, subs = mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : 0;
            bool nullMat = mr != null && System.Array.Exists(mr.sharedMaterials, m => m == null);
            log.Add("  " + kind + ": 몸통 " + (b ?? "-") + " · 뚜껑 " + (l ?? "-") + " · 재질 " + mats + "/" + subs + (hcChest.Photo != null ? " · 사진" : "") + (hcChest.Model != null ? " · 모델" : ""));
            var want = ShapeMeshes[kind];
            bool ok = hcChest.Kind == kind && b == want[0] && l == want[1] && mats == subs && !nullMat;
            if (kind == "photo") ok &= hcChest.Photo != null && hcChest.transform.Find("PhotoFront") != null && hcChest.transform.Find("PhotoBack") != null;
            if (kind == "model")
            {
                var holder = hcChest.transform.Find("Model");
                var loader = holder != null ? holder.GetComponent<GLBModelLoader>() : null;
                log.Add("  모델 칸 " + (holder != null ? "배율 " + holder.localScale.x.ToString("0.000") + " · 자리 " + holder.localPosition.ToString("F2") + " · 자식 " + holder.childCount : "없음")
                        + " · 불러온 모델 " + (loader != null && loader.HasLoadedModel() ? loader.GetLoadedModel().name : "없음") + " · Model " + (hcChest.Model != null ? hcChest.Model.name : "null"));
                var rs = hcChest.Model != null ? hcChest.Model.GetComponentsInChildren<Renderer>() : new Renderer[0];
                if (rs.Length == 0 || loader == null || !loader.HasLoadedModel() || loader.GetLoadedModel() != hcChest.Model) return false;
                if (holder.Find("GLBContainer/GLB_LoadingSpinner") != null) { log.Add("  로딩 고리가 남아 있음"); return false; }
                var rot = hcChest.transform.rotation;
                hcChest.transform.rotation = Quaternion.identity;
                var bb = rs[0].bounds;
                foreach (var r in rs) bb.Encapsulate(r.bounds);
                hcChest.transform.rotation = rot;
                float size = Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z)) / hcChest.transform.lossyScale.x;
                float off = (bb.center - hcChest.transform.position).magnitude / hcChest.transform.lossyScale.x;
                log.Add("  3D 모델: 가장 긴 변 " + size.ToString("0.00") + " · 가운데 어긋남 " + off.ToString("0.00") + " (상자 폭 1.0 기준)");
                ok &= Mathf.Abs(size - 1f) < 0.05f && off < 0.05f;
            }
            return ok;
        }

        private static int DevCreateHuntAt(string host, string title, float lat, float lon, string code, string question, string prize = null)
        {
            DevPost(host, "/api/hunts/rules/accept", "{\"version\":2}");
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
                CoverLog(p);
                tr.EditorFeed(true, p);   // 에디터에선 넣은 마우스 이벤트가 기기에 닿지 않는다 — 스와이프 시험처럼 직접 넣는다
            });
            Sleep(0.15f);
            Do(() => { if (tr != null) tr.EditorFeed(false, p); });
            Sleep(0.2f);
        }
        // 누를 자리를 UI 가 덮었으면 적는다 (덮였으면 보물이 아니라 그 UI 를 누른다)
        private static void CoverLog(Vector2 p)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = p }, hits);
            if (hits.Count > 0) log.Add("  누를 자리를 덮은 UI: " + PathOf(hits[0].gameObject.transform));
        }
        private static string PathOf(Transform t) => t == null ? "" : t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
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
                CoverLog(p);
                for (int i = 0; i < 2; i++) { tr.EditorFeed(true, p); tr.EditorFeed(false, p); }
            });
            Sleep(0.2f);
        }
        // 상세가 근접 확인 중인 게임을 AR 로 연다 — 창을 닫고 · 상자를 두 번 톡톡 → 축하 화면 → '확인' → 그 게임 상세
        private static int arHunt;
        private static void OpenTreasureAR(string shot)
        {
            const string C = "HuntCelebrate0926/Panel/Content/", F = C + "Card/Front/";
            Do(() => { arHunt = R0926HuntCard.NearHuntId; log.Add("  AR 로 열 게임 " + arHunt); });
            CloseUpload();
            Wait(() => arHunt != 0 && R0926HuntTreasure.SpawnedObjects.ContainsKey("hunt_" + arHunt), 25f, "AR 보물 상자");
            Sleep(0.6f);
            Do(() => FaceTreasure(arHunt));
            Sleep(0.3f);
            DoubleTapTreasure(() => arHunt);
            Wait(() => R0926HuntCelebrate.Showing, 8f, "두 번 톡톡 → 축하 화면");
            Wait(() => R0926HuntCelebrate.EditorPhase == "Done", 8f, "두근두근 → 짜잔 → 카드");
            Sleep(0.75f);
            Shot(shot);
            Do(() => log.Add("  카드: " + CardSummary(F)));
            Check(() => TextOf(F + "Title") == "보물을 찾았습니다!" && Active(C + "OK"), "두 번 톡톡 → 두근두근 → '보물을 찾았습니다!' 카드");
            Back();   // 안드로이드 뒤로가기 = 확인
            Wait(() => !R0926HuntCelebrate.Showing && Active(HuntCardPath + "Detail"), 12f, "뒤로가기(= 확인) → 그 게임 상세");
            Sleep(1.2f);
        }

        // ── 축하 화면 도우미 ──
        // 무대의 3D 보물 (없으면 null)
        private static R0926HuntChest RevealChest()
        {
            var rv = UnityEngine.Object.FindAnyObjectByType<R0926HuntReveal>(FindObjectsInactive.Include);
            return rv != null ? rv.Chest : null;
        }
        // AR 화면에 보이던 그 자리에서 날아왔나
        private static bool CelebrateFrom()
        {
            var cel = UnityEngine.Object.FindAnyObjectByType<R0926HuntCelebrate>(FindObjectsInactive.Include);
            return cel != null && (bool)typeof(R0926HuntCelebrate).GetField("hasFrom", NPI).GetValue(cel);
        }
        private static string CardSummary(string f) =>
            TextOf(f + "Title") + " / " + TextOf(f + "Rank") + " / " + TextOf(f + "Hunt") + " / " + TextOf(f + "PrizeBox/Label") + ": " + TextOf(f + "PrizeBox/Prize")
            + " / " + TextOf(f + "Meta0/Key") + " " + TextOf(f + "Meta0/Value") + " · " + TextOf(f + "Meta1/Key") + " " + TextOf(f + "Meta1/Value")
            + " · " + TextOf(f + "Meta2/Key") + " " + TextOf(f + "Meta2/Value") + " / " + TextOf(f + "Note").Replace("\n", " ⏎ ")
            + " / 메달 " + TextOf(f + "Seal/Num") + (Active(f + "Crown") ? " + 왕관" : "");
        private static float Alpha(string path) => Find(path)?.GetComponent<Graphic>()?.color.a ?? 0f;
        private static int Confetti()
        {
            var fx = Find("HuntCelebrate0926/Panel/Content/Fx");
            int n = 0;
            if (fx != null) foreach (Transform c in fx) if (c.name == "Paper" && c.gameObject.activeSelf) n++;
            return n;
        }
        // 서버 없는 시험 — '확인'(그 게임 상세를 서버에서 연다) 대신 닫기만
        private static void CloseCelebrate()
        {
            var cel = UnityEngine.Object.FindAnyObjectByType<R0926HuntCelebrate>(FindObjectsInactive.Include);
            if (cel != null) typeof(R0926HuntCelebrate).GetMethod("Finish", NPI)?.Invoke(cel, null);
        }
        // '확인'·뒤로가기가 연 게임 상세 길을 막아 둔다 (running 이면 R0926HuntPush 가 넘기지 않는다) · clear 면 쌓인 것도 버린다
        private static void SetPushRunning(bool on, bool clear = false)
        {
            var push = UnityEngine.Object.FindAnyObjectByType<R0926HuntPush>(FindObjectsInactive.Include);
            if (push != null) typeof(R0926HuntPush).GetField("running", NPI)?.SetValue(push, on);
            if (clear) typeof(R0926HuntPush).GetField("pending", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, false);
        }
        private static string PushPending()
        {
            const System.Reflection.BindingFlags S = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            bool on = (bool)(typeof(R0926HuntPush).GetField("pending", S)?.GetValue(null) ?? false);
            return on ? typeof(R0926HuntPush).GetField("pendingHunt", S)?.GetValue(null) as string : null;
        }
        // 3D 보물 판 — 모서리는 투명(알파 0) · 상자 자리는 불투명 · 상자가 판의 어디를 차지하나 (구도 맞추기) · 판을 그림으로도 남긴다
        private static bool RevealPixels(string name)
        {
            var rv = UnityEngine.Object.FindAnyObjectByType<R0926HuntReveal>(FindObjectsInactive.Include);
            var rt = rv != null ? rv.EditorTexture : null;
            if (rt == null) { log.Add("  찍은 판 없음"); return false; }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            int w = rt.width, h = rt.height, solid = 0, x0 = w, x1 = -1, y0 = h, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 128) { solid++; x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            int corner = Mathf.Max(px[0].a, px[w - 1].a, px[(h - 1) * w].a, px[h * w - 1].a);
            File.WriteAllBytes(Path.Combine(dir, "test_" + name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            log.Add("  판 " + w + "x" + h + " · 모서리 알파 " + corner + " · 불투명 " + (100f * solid / (w * h)).ToString("0.0") + "% · 상자 자리 x " + (x0 * 100 / w) + "~" + (x1 * 100 / w)
                    + "% · y " + (y0 * 100 / h) + "~" + (y1 * 100 / h) + "%");
            return corner == 0 && solid > w * h / 50;
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
            DevPost(ProfHost, "/api/hunts/rules/accept", "{\"version\":2}");
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

        // 보물찾기 '찾은 사람' (10-27 대표님) — 보물·상세의 현황 카드: 찾은 순서대로 사진·아이디, 다섯 명까지 · 'N명 더 보기' · 내 줄은 '나'.
        // 개발 서버: 시험 주최자의 '모두 당첨' 게임을 fd1~fd7 이 찾음 (이름 '시험_fd1' · fd1·fd3 만 사진(97x61) · fd4 는 열두 자 넘는 이름)
        private const string FdHost = "hunt_test_fdhost", FdHost0 = "hunt_test_fdhost0", FdHostS = "hunt_test_fdhosts", FdMe = "hunt_test_fd7", FdOut = "hunt_test_fd8";
        private const string FdTitle = "찾은 사람 시험", FdTitle0 = "찾은 사람 시험 (아직)", FdTitleS = "찾은 사람 시험 (예약)";
        private const string FdLong = "시험_보물찾기를정말좋…";
        private static int fdHunt, fdHunt0, fdHuntS;
        private static void PlanHuntFinders()
        {
            const string H = HuntCardPath, D = H + "Detail/Viewport/Content/", F = D + "Finders/";
            Do(() =>
            {
                treasureRun = true;   // 주최자 게임은 상세를 열면 지켜본다 — 끝나면 지운다
                PlayerPrefs.DeleteKey(TreasurePref);
                typeof(R0926HuntTreasure).GetField("loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, false);
                R0926HuntApi.EditorBase = "http://127.0.0.1:5099";
                R0926HuntApi.EditorTestUser = FdHost;
                ProfileManager.EditorBase = "http://127.0.0.1:5099";   // 찾은 사람 줄 → 그 사람 프로필도 개발 서버에서
                R0926HuntCard.EditorFinderProfile = null;
                string spot = "\"lat\":" + Deg(VirtualLocation.Instance.Latitude) + ",\"lon\":" + Deg(VirtualLocation.Instance.Longitude);
                string rest = ",\"mode\":\"touch\",\"radius_km\":5,\"visible_m\":50," + spot + ",\"acc\":5,\"duration_min\":60";
                foreach (var host in new[] { FdHost, FdHost0, FdHostS }) DevPost(host, "/api/hunts/rules/accept", "{\"version\":2}");
                fdHunt = HuntId(DevPost(FdHost, "/api/hunts", "{\"title\":\"" + FdTitle + "\",\"winners_all\":true" + rest + "}"));
                fdHunt0 = HuntId(DevPost(FdHost0, "/api/hunts", "{\"title\":\"" + FdTitle0 + "\",\"winners\":1" + rest + "}"));
                fdHuntS = HuntId(DevPost(FdHostS, "/api/hunts", "{\"title\":\"" + FdTitleS + "\",\"winners\":2" + rest + ",\"start_in_min\":30}"));
                for (int n = 1; n <= 7; n++) SeedPlay("hunt_test_fd" + n, fdHunt, spot);
                log.Add("  시험 게임 " + fdHunt + " · 아직 " + fdHunt0 + " · 예약 " + fdHuntS);
            });
            Check(() => fdHunt > 0 && fdHunt0 > 0 && fdHuntS > 0 && Has(DevGet(FdHost, "/api/hunts/" + fdHunt), "\"found\":7", "\"participants\":7"),
                "개발 서버: '모두 당첨' 게임을 fd1~fd7 이 찾음 · 아무도 못 찾은 게임 · 30분 뒤 시작하는 게임");
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

            // ① 주최자 — 게임 정보 바로 아래: 찾은 사람 7명 · 참가 7/100명 · 다섯 줄 + '2명 더 보기'
            Do(() => R0926HuntPush.Open(fdHunt.ToString(), null, false));
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(FdTitle) && FinderRows().Count > 0, 15f, "주최자 — 게임 상세");
            Wait(() => FinderPhoto(0) != null && FinderPhoto(0).enabled, 8f, "fd1 사진 받음");
            Sleep(0.5f);
            Shot("finders_host");
            Do(() => log.Add("  " + TextOf(F + "Head/Title") + " | " + TextOf(F + "Head/Stat") + " | " + FinderNames() + " | 더 보기 " + Active(F + "More") + " '" + FinderMoreText() + "'"));
            Check(() => Find(D + "Finders").GetSiblingIndex() == Find(D + "Info").GetSiblingIndex() + 1 && !Active(D + "Join"), "주최자: 현황 카드는 게임 정보 바로 아래");
            Check(() => TextOf(F + "Head/Title") == "찾은 사람 7명" && TextOf(F + "Head/Stat") == "" && !Active(F + "Empty"),
                "머리줄 '찾은 사람 7명' · 오른쪽은 비움 ('모두 당첨'이라 남은 자리가 없고, 참가 인원은 바로 위 게임 정보에)");
            Check(() => FinderNames() == "시험_fd1,시험_fd2,시험_fd3," + FdLong + ",시험_fd5" && FinderMe() == "" && Active(F + "More") && FinderMoreText() == "2명 더 보기",
                "찾은 순서대로 다섯 명 (열두 자 넘는 이름은 잘림) · '나' 없음 · '2명 더 보기'");
            Check(() =>
            {
                var r = FinderRows();
                for (int i = 0; i < r.Count; i++)
                {
                    string t = r[i].Find("Rank").GetComponent<Text>().text;
                    if (!t.Contains(">" + (i + 1) + "번째<") || !System.Text.RegularExpressions.Regex.IsMatch(t, @"   \d\d:\d\d$")) { log.Add("  순위 " + i + " '" + t + "'"); return false; }
                }
                return r.Count == 5;
            }, "줄마다 '1번째 … 5번째'(금색) · 찾은 시각 (오늘이면 시:분)");
            Check(() =>
            {
                var p1 = FinderPhoto(0); var p2 = FinderPhoto(1); var p3 = FinderPhoto(2);
                var t = p1 != null ? p1.texture : null;
                float w = 61f / 97f;
                string ini = FinderRows()[1].Find("Avatar/Face/Initial")?.GetComponent<Text>()?.text;
                log.Add("  사진: fd1 " + (t != null ? t.width + "x" + t.height : "-") + " uv " + (p1 != null ? p1.uvRect.ToString("F3") : "-")
                        + " · fd2 " + (p2 != null && p2.enabled) + " '" + ini + "' · fd3 " + (p3 != null && p3.enabled));
                return t != null && t.width == 97 && t.height == 61 && Mathf.Abs(p1.uvRect.width - w) < 0.01f && Mathf.Abs(p1.uvRect.x - (1f - w) / 2f) < 0.01f
                       && Mathf.Abs(p1.uvRect.height - 1f) < 0.01f && p2 != null && !p2.enabled && ini == "시" && p3 != null && p3.enabled;
            }, "사진 있는 사람(fd1·fd3)은 사진 — 가로로 긴 사진은 가운데 정사각형만 · 없는 사람(fd2)은 바탕색에 첫 글자");
            Check(FinderRowsFit, "이름·순위 글이 자기 칸 안에 (긴 이름도 옆 칸과 겹치지 않음)");

            Do(() => Click(F + "More"));
            Sleep(0.4f);
            Shot("finders_all");
            Check(() => FinderRows().Count == 7 && FinderNames().EndsWith(",시험_fd6,시험_fd7") && FinderMoreText() == "접기", "'2명 더 보기' → 일곱 명 모두 · '접기'");

            Do(() => { var r = FinderRows(); if (r.Count > 1) r[1].GetComponent<Button>().onClick.Invoke(); });
            Wait(() => Active("FullProfilePanel") && ProfileManager.Instance.usernameText.text == "시험_fd2", 8f, "fd2 줄 → 프로필");
            Sleep(0.6f);
            Shot("finders_profile");
            Check(() => R0926HuntCard.EditorFinderProfile == "hunt_test_fd2" && Active("FullProfilePanel") && ProfileManager.Instance.usernameText.text == "시험_fd2",
                "찾은 사람 줄을 누르면 그 사람 프로필 (추가 창 위로)");
            Back();
            Wait(() => !Active("FullProfilePanel"), 5f, "뒤로 → 프로필 닫음");
            Sleep(0.6f);
            Check(() => !Active("FullProfilePanel") && Active(H + "Detail") && DetailTitle().Contains(FdTitle), "프로필을 닫으면 보던 보물찾기 상세 그대로");
            Do(() => Click(F + "More"));
            Sleep(0.4f);
            Check(() => FinderRows().Count == 5 && FinderMoreText() == "2명 더 보기", "'접기' → 다시 다섯 명");

            // ② 주최자가 자기 AR 보물을 누르면 — 예전엔 '내가 숨긴 보물이에요' 알림만 → 이 현황 카드로
            Check(() => R0926HuntTreasure.Tracking(fdHunt), "주최자 게임은 지켜봄 (AR 보물)");
            CloseUpload();
            Check(() => !Active("UploadPage"), "추가 창을 닫음");
            Do(() => typeof(R0926HuntTreasure).GetMethod("Tap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { fdHunt }));
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(FdTitle) && FinderRows().Count > 0, 10f, "AR 보물을 누름 → 현황 카드");
            Sleep(0.5f);
            Shot("finders_ar_tap");
            Check(() => Active(H + "Detail") && DetailTitle().Contains(FdTitle) && TextOf(F + "Head/Title") == "찾은 사람 7명" && FinderRows().Count == 5,
                "주최자가 자기 AR 보물을 누르면 그 게임 현황 카드 (찾은 사람 7명)");

            // ③ 찾은 사람(fd7)이 보면 — 내 줄에 '나' · 내가 차단한 fd6 은 목록에서 빠짐 (숫자는 그대로 7명)
            Do(() => DevPost(FdMe, "/api/block", "{\"blocked_id\":\"hunt_test_fd6\"}"));
            CloseUpload();
            Do(() => { R0926HuntApi.EditorTestUser = FdMe; R0926HuntPush.OpenHunt(fdHunt); });
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(FdTitle) && FinderMe() != "", 15f, "fd7 — 게임 상세");
            Sleep(0.6f);
            Shot("finders_me");
            Do(() => log.Add("  " + TextOf(F + "Head/Title") + " | " + FinderNames() + " | 나: " + FinderMe() + " | 더 보기 " + Active(F + "More")));
            Check(() => TextOf(F + "Head/Title") == "찾은 사람 7명" && FinderNames() == "시험_fd1,시험_fd2,시험_fd3," + FdLong + ",시험_fd5,시험_fd7"
                        && FinderMe() == "시험_fd7" && !Active(F + "More") && Find(D + "Finders").GetSiblingIndex() == Find(D + "Info").GetSiblingIndex() + 1,
                "찾은 사람이 보면: 다섯 명 + 내 줄('나') · 차단한 fd6 은 빠짐 · 접어 둔 사람이 없으니 '더 보기' 없음");
            Check(() => { var r = FinderRows(); var b = r.Count == 6 ? r[5].GetComponent<Button>() : null; return b != null && !b.enabled && r[0].GetComponent<Button>().enabled; },
                "내 줄은 눌러도 아무것도 (다른 사람 줄만 프로필)");
            Check(FinderRowsFit, "'나' 표가 붙은 줄도 글이 칸 안에");
            Do(() => DevPost(FdMe, "/api/unblock", "{\"blocked_id\":\"hunt_test_fd6\"}"));

            // ④ 아직 참가 안 한 사람 — '참가하기·공유하기' 아래 (참가 버튼이 밀려 내려가지 않게)
            CloseUpload();
            Do(() => { R0926HuntApi.EditorTestUser = FdOut; R0926HuntPush.OpenHunt(fdHunt); });
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(FdTitle) && FinderRows().Count > 0, 15f, "fd8 — 게임 상세");
            Sleep(0.6f);
            Shot("finders_outsider");
            Check(() =>
            {
                var box = Find(D + "Finders"); var join = Find(D + "Join"); var share = Find(D + "Share");
                Transform above = share != null && share.gameObject.activeSelf ? share : join;
                log.Add("  참가 " + Active(D + "Join") + " · 공유 " + Active(D + "Share") + " · 칸 " + box.GetSiblingIndex() + " (위 " + above.name + " " + above.GetSiblingIndex() + ")");
                return Active(D + "Join") && box.GetSiblingIndex() == above.GetSiblingIndex() + 1 && FinderRows().Count == 5 && FinderMe() == "";
            }, "참가 전: 현황 카드는 '참가하기·공유하기' 바로 아래 · 다섯 명");

            // ⑤ 아직 아무도 못 찾음 → 끝냄 · 시작 전(예약) 게임엔 칸이 없다
            CloseUpload();
            Do(() => { R0926HuntApi.EditorTestUser = FdHost0; R0926HuntPush.OpenHunt(fdHunt0); });
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(FdTitle0), 15f, "아무도 못 찾은 게임");
            Sleep(0.6f);
            Shot("finders_none");
            Do(() => log.Add("  " + TextOf(F + "Head/Title") + " | " + TextOf(F + "Head/Stat") + " | " + TextOf(F + "Empty")));
            Check(() => Active(D + "Finders") && TextOf(F + "Head/Title") == "찾은 사람 0명" && TextOf(F + "Head/Stat") == "남은 당첨 1자리"
                        && Active(F + "Empty") && TextOf(F + "Empty") == "아직 찾은 사람이 없어요" && FinderRows().Count == 0 && !Active(F + "More"),
                "아무도 못 찾은 진행 중 게임: '찾은 사람 0명' · '남은 당첨 1자리' · '아직 찾은 사람이 없어요'");
            Do(() => DevPost(FdHost0, "/api/hunts/" + fdHunt0 + "/end", "{}"));
            CloseUpload();
            Do(() => R0926HuntPush.OpenHunt(fdHunt0));
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(FdTitle0) && TextOf(F + "Empty") == "찾은 사람 없이 끝났어요", 15f, "끝낸 뒤");
            Check(() => Active(F + "Empty") && TextOf(F + "Empty") == "찾은 사람 없이 끝났어요" && TextOf(F + "Head/Stat") == "",
                "끝난 게임: '찾은 사람 없이 끝났어요' · 남은 자리 줄 없음");
            CloseUpload();
            Do(() => { R0926HuntApi.EditorTestUser = FdHostS; R0926HuntPush.OpenHunt(fdHuntS); });
            Wait(() => Active(H + "Detail") && DetailTitle().Contains(FdTitleS), 15f, "예약 게임");
            Sleep(0.6f);
            Check(() => !Active(D + "Finders"), "시작 전 게임엔 '찾은 사람' 칸이 없음");
            CloseUpload();
            Do(() =>
            {
                R0926HuntApi.EditorBase = null; R0926HuntApi.EditorTestUser = null; ProfileManager.EditorBase = null; R0926HuntCard.EditorFinderProfile = null;
                PlayerPrefs.DeleteKey(TreasurePref); treasureRun = false;
            });
        }
        private static List<Transform> FinderRows()
        {
            var rows = new List<Transform>();
            var l = Find(HuntCardPath + "Detail/Viewport/Content/Finders/Rows");
            if (l != null) foreach (Transform c in l) if (c.gameObject.activeSelf) rows.Add(c);
            return rows;
        }
        private static string FinderNames() => string.Join(",", FinderRows().ConvertAll(r => r.Find("Name")?.GetComponent<Text>()?.text ?? "?"));
        private static string FinderMe() => string.Join(",", FinderRows().FindAll(r => r.Find("Me") != null && r.Find("Me").gameObject.activeSelf).ConvertAll(r => r.Find("Name").GetComponent<Text>().text));
        private static string FinderMoreText() => Find(HuntCardPath + "Detail/Viewport/Content/Finders/More")?.GetComponentInChildren<Text>(true)?.text ?? "";
        private static RawImage FinderPhoto(int i) { var r = FinderRows(); return i < r.Count ? r[i].Find("Avatar/Face/Photo")?.GetComponent<RawImage>() : null; }
        // 줄의 글이 자기 칸보다 길어 옆 칸에 겹치지 않나 (이름 · 순위)
        private static bool FinderRowsFit()
        {
            bool ok = true;
            foreach (var r in FinderRows())
                foreach (var n in new[] { "Name", "Rank" })
                {
                    var t = r.Find(n)?.GetComponent<Text>();
                    if (t == null) continue;
                    float w = t.rectTransform.rect.width, need = t.preferredWidth;
                    if (w + 1f < need) { log.Add("  넘침 " + n + " '" + t.text + "' 칸 " + w.ToString("0") + " < 글 " + need.ToString("0")); ok = false; }
                }
            return ok;
        }

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
            Sleep(0.6f);
            Do(() => Click(HuntReview + "/Sheet0926/Buttons/Go"));   // 숨기기 전 확인 카드 (10-26) → 같은 카드에 공유하기 · 확인
            Wait(() => Active(HuntReview + "/Sheet0926/Buttons/Done"), 10f, "확인 카드 → 숨김");
            Do(() => Click(HuntReview + "/Sheet0926/Buttons/Done"));
            Sleep(1.5f);
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
            Do(() => ActiveChildTitled(H + "Hub/Viewport/Content/NearList", "로그인 시험 보물")?.GetComponent<Button>()?.onClick.Invoke());
            Sleep(2.0f);
            Do(() => { var v = VirtualLocation.Instance; v.SetCoordinates(v.Latitude - 0.00007f, v.Longitude); });   // 8m 물러나 — 보이는 거리 기본 10m(v48) 안 · 상자를 화면에서 누를 수 있게
            Do(() => Click(H + "Detail/Viewport/Content/Join"));
            Sleep(11.0f);   // 참가 → 상세 다시 → 근접 확인(8.5초마다)
            Check(() => TextOf(H + "Detail/Viewport/Content/Near") == "보물이 보이는 거리예요", "보이는 거리 안 → '보물이 보이는 거리예요'");
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

        // 로그인 만료 (10-25 goosoon56 — 친구 지도가 '인터넷 연결을 확인' 이라는데 실은 7월에 받은 1시간 토큰이 그대로 남아 모든 요청이 401) —
        // ① 만료된 토큰을 둔 채 켜면 로그인된 척하지 않는다: 로그아웃 · 저장값 지움 · 푸시 연결 끊기 · 시작화면 뒤 「로그인이 만료됐어요」 (서버엔 묻지 않는다)
        // ② 로그인한 채 서버가 토큰을 거절하면(친구 지도 401) 지도에 '로그인 만료'(인터넷 탓 아님) · 서버에 한 번 확인해 로그아웃 + 안내
        // ③ 401 이어도 서버가 토큰이 살아 있다고 하면 로그인 그대로 · 서버가 준 새 토큰 저장 · 지도는 곧 다시 받아 정상
        // ④ 돌아올 때(포커스) 하루 넘은 토큰이면 서버에 물어 새 토큰 · 하루 안 된 토큰과 이미 바꾼 토큰은 묻지 않는다
        // ⑤ 돌아올 때 토큰이 만료돼 있으면 서버에 묻지 않고 로그아웃 + 안내
        // 시험 서버(127.0.0.1:5099)만 쓴다 — 시작 전 로그인 저장값은 덜어 두었다가(StashLogin) 끝나면 되돌린다
        private const string AuthDev = "http://127.0.0.1:5099";
        private const string ExpiredUser = "2147400099";
        [Serializable] private class DevAuth { public string[] verify; }
        // JWT 모양 시험 토큰 — 가운데에 발급·만료 시각 · 서명 자리의 hunt-dev-<이름> 은 시험 서버만 받는다 (운영 서버는 서명이 틀려 거절)
        private static string DevJwt(string who, long issuedAgo, long expiresIn)
        {
            Func<string, string> b64 = t => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(t)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return b64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}") + "." + b64("{\"user_id\":" + ExpiredUser + ",\"iat\":" + (now - issuedAgo) + ",\"exp\":" + (now + expiresIn) + "}")
                   + ".hunt-dev-" + who;
        }
        private static void ArmExpiredLogin()
        {
            SessionState.SetBool(LoginReadyKey, true);   // 플레이를 끄면 덜어 둔 로그인 저장값을 되돌린다
            PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, DevJwt("expired", 31 * 86400, -86400));   // 하루 전에 만료
            PlayerPrefs.SetString("SavedUserId", ExpiredUser);
            PlayerPrefs.SetString("SavedUsername", "시험_만료");
            PlayerPrefs.SetString("LoginProvider", "kakao");
            PlayerPrefs.Save();
            LoginManager.EditorAuthBase = AuthDev;      // 켜는 순간(Awake)부터 시험 서버로
            FirebaseNotification.EditorBase = AuthDev;  // 로그아웃 때 푸시 연결 끊기도
            DevGet("", "/api/dev/p2p_log?reset=1");
            DevGet("", "/api/dev/auth?reset=1");
        }
        private static void PlanAuthExpire()
        {
            const string Expired = "로그인이 만료됐어요.\n다시 로그인할까요?";
            R0926FriendsMap map = null;
            int verifyAt = 0;
            string Body() => map != null ? (typeof(R0926FriendsMap).GetField("consentBody", NPI)?.GetValue(map) as Text)?.text ?? "" : "";
            bool CardOn() => map != null && (typeof(R0926FriendsMap).GetField("consentCard", NPI)?.GetValue(map) as GameObject)?.activeSelf == true;
            string Prompt() => LoginManager.Instance?.promptText?.text ?? "";
            string Token() => PlayerPrefs.GetString(LoginManager.AUTH_TOKEN_KEY, "");
            int Verifies() => JsonUtility.FromJson<DevAuth>(DevGet("", "/api/dev/auth"))?.verify?.Length ?? -1;
            void Focus() => typeof(LoginManager).GetMethod("OnApplicationFocus", NPI)?.Invoke(LoginManager.Instance, new object[] { true });
            void Login(string who) { LoginManager.Instance.CloseLoginPrompt(); LoginManager.Instance.SimulateDeepLink("woopang://auth?token=" + LocalTestLogin + who); }

            // ① 만료된 토큰으로 켜기 (ArmExpiredLogin 이 플레이 전에 넣었다)
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                ProfileManager.EditorBase = AuthDev;
                FollowManager.EditorBase = AuthDev;
                P2PManager.EditorBase = AuthDev;
                MessagePanelManager.EditorBase = AuthDev;
                R0926HuntApi.EditorBase = AuthDev;
                R0926HuntApi.EditorTestUser = null;
                R0926FriendsMap.EditorBase = AuthDev;
                R0926MapTiles.EditorBase = "http://127.0.0.1:9";   // 닫힌 자리 — 지도 타일 없이 (진짜 서버의 타일을 부르지 않는다)
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Check(() => LoginManager.Instance != null && !LoginManager.Instance.IsLoggedIn, "① 만료된 토큰으로 켜면 로그인된 척하지 않는다 (로그아웃 상태)");
            Check(() => !PlayerPrefs.HasKey(LoginManager.AUTH_TOKEN_KEY) && !PlayerPrefs.HasKey("SavedUserId") && !PlayerPrefs.HasKey("SavedUsername"),
                "저장된 로그인을 지운다 — 다음에 켜도 그대로 로그아웃");
            Wait(() => Active("LoginPromptPanel") && Prompt() == Expired, 6f, "시작화면 뒤 「로그인이 만료됐어요 · 다시 로그인할까요?」");
            Sleep(0.6f);
            Shot("auth_expired_prompt");
            Wait(() => P2PLog().Contains("fcm_unregister::" + ExpiredUser), 8f, "그 아이디의 푸시 연결 끊기");
            Check(() => Verifies() == 0, "만료된 토큰은 서버에 묻지 않는다 (휴대폰 안에서 판단)");

            // ② 다시 로그인 → 서버가 그 토큰을 거절 (친구 지도 401)
            Do(() => Login("authx"));
            Wait(() => LoginManager.Instance.IsLoggedIn && LoginManager.Instance.CurrentUsername == "시험_authx", 10f, "② 다시 로그인 (시험 서버가 확인)");
            Do(() => DevGet("", "/api/dev/auth?revoke=authx"));
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => Modes()?.Show(1));
            Do(() => map = UnityEngine.Object.FindAnyObjectByType<R0926FriendsMap>(FindObjectsInactive.Include));
            Wait(() => Body().StartsWith("로그인이 만료됐어요"), 10f, "친구 지도 401 → '로그인이 만료됐어요' (인터넷 연결 탓이 아니다)");
            Sleep(0.4f);
            Shot("auth_map_expired");
            Wait(() => !LoginManager.Instance.IsLoggedIn, 10f, "서버에 한 번 확인 → 거절 → 로그아웃");
            Wait(() => Active("LoginPromptPanel") && Prompt() == Expired, 5f, "다시 로그인하라는 안내");
            Check(() => !PlayerPrefs.HasKey(LoginManager.AUTH_TOKEN_KEY), "거절된 토큰을 지운다");

            // ③ 401 이어도 서버가 토큰이 살아 있다고 하면 — 로그인 그대로 · 새 토큰 저장 · 지도는 곧 다시 받아 정상
            Do(() => Login("authy"));
            Wait(() => LoginManager.Instance.IsLoggedIn && LoginManager.Instance.CurrentUsername == "시험_authy", 10f, "③ 다른 아이디로 다시 로그인");
            Wait(() => !CardOn(), 12f, "지도 정상 (친구 핀)");
            Do(() =>
            {
                DevGet("", "/api/dev/auth?once401=authy&renew=authy:authy2");
                typeof(R0926FriendsMap).GetField("nextRefresh", NPI)?.SetValue(map, 0f);   // 지금 다시 받기
            });
            Wait(() => Token() == LocalTestLogin + "authy2", 10f, "친구 지도 401 → 서버 확인: 살아 있음 · 새 토큰 저장");
            Do(() => log.Add("  그 사이 지도: " + (CardOn() ? Body() : "(카드 없음)")));
            Check(() => LoginManager.Instance.IsLoggedIn && !Active("LoginPromptPanel"), "로그인 그대로 · 안내 없음 (401 한 번으로 내보내지 않는다)");
            Wait(() => !CardOn(), 12f, "지도는 곧 새 토큰으로 다시 받아 정상");
            Sleep(0.6f);
            Shot("auth_map_recovered");

            // ④ 돌아올 때(포커스) — 하루 넘은 토큰만 서버에 물어 새 토큰
            Do(() => { PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, DevJwt("authy", 3600, 29 * 86400)); verifyAt = Verifies(); Focus(); });
            Sleep(1.5f);
            Check(() => Verifies() == verifyAt && Token().EndsWith(".hunt-dev-authy") && LoginManager.Instance.IsLoggedIn, "④ 돌아올 때 — 하루 안 된 토큰은 서버에 묻지 않는다");
            Do(() => { PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, DevJwt("authy", 2 * 86400, 28 * 86400)); DevGet("", "/api/dev/auth?renew=authy:authy3"); Focus(); });
            Wait(() => Token() == LocalTestLogin + "authy3", 8f, "하루 넘은 토큰 → 서버에 물어 새 토큰으로");
            Check(() => Verifies() == verifyAt + 1 && LoginManager.Instance.IsLoggedIn, "물어본 건 한 번 · 로그인 그대로");
            Do(() => Focus());
            Sleep(1.5f);
            Check(() => Verifies() == verifyAt + 1, "새 토큰으로 돌아오면 다시 묻지 않는다");

            // ⑤ 돌아올 때 토큰이 만료돼 있으면 — 서버에 묻지 않고 로그아웃 + 안내
            Do(() => { PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, DevJwt("authy", 31 * 86400, -10)); verifyAt = Verifies(); Focus(); });
            Wait(() => !LoginManager.Instance.IsLoggedIn, 5f, "⑤ 돌아올 때 만료 → 로그아웃");
            Wait(() => Active("LoginPromptPanel") && Prompt() == Expired, 5f, "「로그인이 만료됐어요」 안내");
            Check(() => Verifies() == verifyAt, "만료는 서버에 묻지 않는다");
            Do(() =>
            {
                LoginManager.Instance.CloseLoginPrompt();
                if (LoginManager.Instance.IsLoggedIn) LoginManager.Instance.Logout();   // 시험 서버에 둔 채로 — 끝난 뒤 운영 서버로 시험 토큰이 가지 않게
                ProfileManager.EditorBase = null;
                FollowManager.EditorBase = null;
                P2PManager.EditorBase = null;
                MessagePanelManager.EditorBase = null;
                FirebaseNotification.EditorBase = null;
                R0926HuntApi.EditorBase = null;
                R0926FriendsMap.EditorBase = null;
                R0926MapTiles.EditorBase = null;
                LoginManager.EditorAuthBase = null;
                DevGet("", "/api/dev/auth?reset=1");
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
                        // 글자 수 제한 칸(보물찾기 비밀번호 4자)은 앞부분만 들어가는 게 맞다
                        string want = src.characterLimit > 0 && src.characterLimit < 6 ? "우팡 테스트".Substring(0, src.characterLimit) : "우팡 테스트";
                        bool ok = src.text == want && after == want;
                        log.Add((ok && live == want ? "PASS " : "FAIL ") + m.name + "/" + src.transform.parent.parent.name + "/" + src.name
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

        // 10-23 대표님 「오프닝 — 살짝 확대된 상태로(배경만, 별들 포함) 줌아웃, 뒷배경이 멀어지는 느낌 · 로고는 그대로」
        // 시작화면이 도는 동안 뒷배경(성운·별·문양)의 크기와 로고 자리를 재고, 0.1초마다 화면을 찍는다 (움직임 미리보기용)
        private static void PlanSplashZoom()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var rec = new List<(float t, float neb, float star, float motif)>();
            R0926Splash sp = null;
            RectTransform emb = null, word = null, motif = null;
            Transform neb = null, fx = null;
            R0926StarField stars = null;
            Vector2 embAt = default, wordAt = default;
            bool logoMoved = false, fxScaled = false;
            float nextSeq = 0f;
            int seq = 0;
            steps.Enqueue(() =>
            {
                if (sp == null)
                {
                    sp = UnityEngine.Object.FindAnyObjectByType<R0926Splash>(FindObjectsInactive.Include);
                    if (sp == null) { log.Add("  시작화면 없음"); return true; }
                    neb = sp.transform.Find("Nebula0926");
                    fx = sp.transform.Find("LensFx0926");
                    emb = sp.transform.Find("Emblem0926") as RectTransform;
                    word = sp.transform.Find("Wordmark0926") as RectTransform;
                    motif = sp.transform.Find("Lens0926/Motif") as RectTransform;
                    stars = sp.GetComponentInChildren<R0926StarField>(true);
                    embAt = emb.anchoredPosition;
                    wordAt = word.anchoredPosition;
                }
                if (!sp.isActiveAndEnabled) return true;
                float t = (float)typeof(R0926Splash).GetField("t", F).GetValue(sp);
                if (rec.Count == 0 || t - rec[rec.Count - 1].t >= 0.05f)
                    rec.Add((t, neb.localScale.x, stars != null ? stars.zoom : -1f, motif.sizeDelta.x));
                if (emb.anchoredPosition != embAt || word.anchoredPosition != wordAt) logoMoved = true;
                if (fx.localScale != Vector3.one) fxScaled = true;
                if (t >= nextSeq)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, "seq_" + (seq++).ToString("00") + "_" + t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
                    nextSeq = t + 0.1f;
                }
                return false;
            });
            (float t, float neb, float star, float motif) At(float time)
            {
                var best = rec[0];
                foreach (var r in rec) if (Mathf.Abs(r.t - time) < Mathf.Abs(best.t - time)) best = r;
                return best;
            }
            Do(() =>
            {
                if (rec.Count == 0) { log.Add("  기록 없음"); return; }
                var sb = new System.Text.StringBuilder("  뒷배경 크기 (초:성운/별/문양 폭) ");
                foreach (var time in new[] { 0f, 0.6f, 1.2f, 1.6f, 1.95f, 2.4f, 2.9f, 3.4f, 3.8f, 4.2f })
                {
                    var r = At(time);
                    sb.Append(r.t.ToString("0.00") + ":" + r.neb.ToString("0.000") + "/" + r.star.ToString("0.000") + "/" + r.motif.ToString("0") + "  ");
                }
                log.Add(sb.ToString());
                log.Add("  화면 " + seq + "장 · 마지막 기록 " + rec[rec.Count - 1].t.ToString("0.00") + "초");
            });
            Check(() => rec.Count > 0 && rec[0].t < 0.5f && rec[0].neb >= 1.14f, "처음엔 뒷배경이 확대돼 있다 (1.15배)");
            Check(() =>
            {
                for (int i = 1; i < rec.Count; i++) if (rec[i].neb > rec[i - 1].neb + 1e-4f) return false;
                return rec.Count > 1;
            }, "뒷배경은 작아지기만 한다 (멀어짐 — 다시 커지지 않음)");
            Check(() => rec.Count > 0 && Mathf.Abs(rec[rec.Count - 1].neb - 1f) < 0.003f, "끝엔 원래 크기 (1배)");
            Check(() => rec.Count > 0 && At(1.95f).neb > 1.05f && At(1.95f).neb < 1.1f, "배경이 다 드러나는 2초 무렵에도 아직 멀어지는 중 (1.05~1.1배)");
            Check(() => rec.Count > 0 && rec.TrueForAll(r => Mathf.Abs(r.star - r.neb) < 1e-4f), "별도 성운과 같은 줌으로");
            Check(() => rec.Count > 0 && rec[0].motif / rec[rec.Count - 1].motif > 1.18f, "문양도 확대(1.15 × 렌즈 1.06)에서 원래 크기로");
            Check(() => !logoMoved && emb.localScale == Vector3.one && word.localScale == Vector3.one, "로고(엠블럼·WOOPANG)는 자리·크기 그대로");
            Check(() => !fxScaled, "렌즈 테두리·반짝임은 줌과 상관없이 그대로");
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

        // ── P2 (10-23 · 10-25 대표님) — 하늘 옆 장 · 고정 ──
        // 날씨판을 옆으로 밀면 근처 보물찾기 · 축제 · 핫플 · 친구 · 언론사 라이브. 판을 아래로 끌거나 📌 를 누르면 화면 아래쪽에 고정,
        // 위로 밀거나 📌 를 다시 누르면 하늘로. 축제 · 핫플 · 친구는 시험 서버(127.0.0.1:5099), 보물찾기는 목록을 직접 넣는다(시험 서버의
        // 보물찾기는 운영 DB 를 쓴다). 줄 · 방송사 칸은 누르지 않는다 — 바깥 주소가 열리거나 보물찾기 서버에 묻는다
        private static readonly string[] SkyPrefKeys = { "Sky_Enabled", "Sky_Weather", "Sky_Events", "Sky_Angle", "Sky_StartCollapsed", "Sky_Swiped", "Sky_PinUsed" };
        private const string SkyPrefsBackupKey = "Redesign0926PlayTest.SkyPrefs";
        private static bool skyPagesRun;

        private static void PlanSkyPages()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            const string S = "ListPanel/Sheet0926/SettingsPanel0926/Viewport/Content/";
            R0926SkyWeather sky = null;
            R0926SkyPages pg = null;
            RectTransform frame = null, glass = null;
            Transform skyBoard = null, pinLayer = null;
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error && (msg + stack).Contains("R0926Sky")) errors.Add(msg);
            };
            Func<bool> showing = () => sky != null && (bool)typeof(R0926SkyWeather).GetField("skyShow", F).GetValue(sky);
            Func<float> skyAlpha = () => sky != null && typeof(R0926SkyWeather).GetField("skyGroup", F).GetValue(sky) is CanvasGroup g ? g.alpha : 0f;
            Func<float> glassAlpha = () => glass != null ? glass.GetComponent<Image>().color.a : -1f;
            Func<Vector2?> mid = () => SkyScreenRect(glass)?.center;
            Func<Vector2?> pinAt = () => pg != null ? SkyScreenRect(pg.EditorPinButton)?.center : null;
            var move = new Vector2[1];
            float maxPull = 0f;
            bool sawArmed = false, heldOk = true;
            double heldFor = 0;

            Do(() => { Backdrop("bg_sky.png"); DeviceFov(); });
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
                skyPagesRun = true;
                SessionState.SetBool(LoginReadyKey, true);   // 플레이를 끄면 덜어 둔 로그인 저장값을 되돌린다
                StashSkyPrefs();   // 하늘 설정 · '밀어 봤음' · '고정해 봤음' — 끝나면(끊겨도) 되돌린다
                foreach (var k in new[] { "Sky_Enabled", "Sky_Weather", "Sky_Events" }) PlayerPrefs.SetInt(k, 1);
                PlayerPrefs.SetInt("Sky_Angle", 1);   // 28°
                foreach (var k in new[] { "Sky_StartCollapsed", "Sky_Swiped", "Sky_PinUsed" }) PlayerPrefs.SetInt(k, 0);
                const string dev = "http://127.0.0.1:5099";
                MessagePanelManager.EditorBase = dev;
                ProfileManager.EditorBase = dev;
                FollowManager.EditorBase = dev;
                P2PManager.EditorBase = dev;
                FirebaseNotification.EditorBase = dev;
                R0926FriendsMap.EditorBase = dev;
                R0926Nearby.EditorBase = dev;
                R0926HuntApi.EditorBase = "http://127.0.0.1:9";   // 닫힌 자리 — 보물찾기 목록은 아래에서 넣는다 (다시 묻다 끊기면 넣은 목록 그대로)
                R0926HuntApi.EditorTestUser = MpMe;
                R0926MapTiles.EditorBase = "http://127.0.0.1:9";
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
                PlayerPrefs.SetString(LoginManager.AUTH_TOKEN_KEY, LocalTestLogin + MpMe.Substring("hunt_test_".Length));
                PlayerPrefs.Save();
                DevGet(MpMe, "/api/dev/nearby_mode?festivals=ok");
                DevPost(MpMe, "/api/dev/hot_set", "{}");   // 핫플 기본 세 곳 — 협력 업체 광고(맨 위) · 좋아요 많은 카페 · 새로 생긴 곳
                DevGet(MpMe, "/api/dev/ad_events?clear=1");
                R0926Nearby.EditorOpened = new List<string>();   // 줄을 눌러도 진짜 쪽은 열지 않는다
                SfLogin(true, MpMe);
                R0926Nearby.EditorForget();
                // SimpleJSON 은 에디터 쪽 다른 dll 과 이름이 겹쳐 리플렉션으로
                var node = typeof(R0926SkyPages).Assembly.GetType("SimpleJSON.JSON").GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { SkyHuntJson() });
                typeof(R0926HuntList).GetMethod("Take").Invoke(null, new[] { node });
                sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                pg = UnityEngine.Object.FindAnyObjectByType<R0926SkyPages>(FindObjectsInactive.Include);
                if (sky == null || pg == null) return;
                typeof(R0926SkyWeather).GetField("collapsed", F)?.SetValue(sky, false);
                typeof(R0926SkyPages).GetField("nextFriends", F)?.SetValue(pg, 0f);
                frame = pg.EditorFrame;
                glass = frame != null ? frame.Find("Glass0926") as RectTransform : null;
                skyBoard = typeof(R0926SkyWeather).GetField("skyBoard", F)?.GetValue(sky) as Transform;
                pinLayer = Find("SkyPin0926");
                foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude)) t.gameObject.SetActive(false);
                InjectWeather();
                errors.Clear();
                Application.logMessageReceived -= onLog;
                Application.logMessageReceived += onLog;
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
            });
            Check(() => sky != null && pg != null && frame != null && glass != null && skyBoard != null && pinLayer != null && pg.EditorPinButton != null,
                "준비: 판 · 틀(Frame0926) · 유리 · 📌 · 고정 자리(SkyPin0926)");
            Check(() => frame.parent == skyBoard && pinLayer.parent == Find("Dock0926")?.parent && pinLayer.GetSiblingIndex() + 1 == Find("Dock0926").GetSiblingIndex(),
                "틀은 하늘 판 안 · 고정 자리는 도크 바로 앞 (도크 · 창 아래)");

            // 1) 하늘을 보면 판 — 옆 장 여섯
            Do(() => AimCamera(-45f, 0f));
            Wait(() => showing() && pg.EditorPageCount >= 6, 15f, "하늘 판 · 옆 장 여섯");
            Do(() => log.Add($"  장 {pg.EditorPageCount} · 보물찾기 {R0926HuntList.Nearby.Count} · 축제 {R0926Nearby.Festivals.Count} · 핫플 {R0926Nearby.HotPlaces.Count}"));
            Sleep(3.0f);
            Shot("sp_weather");
            Check(() => pg.EditorPageCount == 6, "장 여섯 — 날씨 · 보물찾기 · 축제 · 핫플 · 친구 · 라이브");
            Check(() => glassAlpha() < 0.01f, "하늘에선 유리 바탕이 투명 (예전 모습 그대로)");
            foreach (var k in new[] { R0926SkyPages.Kind.Hunt, R0926SkyPages.Kind.Festival, R0926SkyPages.Kind.Hot, R0926SkyPages.Kind.Friends, R0926SkyPages.Kind.Live })
            {
                Do(() => pg.EditorGo(k));
                Sleep(0.9f);
                Shot("sp_" + k.ToString().ToLowerInvariant());
                Check(() => pg.EditorCurrent == k, k + " 장");
            }

            Do(() => pg.EditorGo(R0926SkyPages.Kind.Hunt));
            Sleep(0.9f);
            Do(() => log.Add("  근처 보물찾기 줄 그림: " + SkyIcon(pg, R0926SkyPages.Kind.Hunt, 0) + " · " + SkyIcon(pg, R0926SkyPages.Kind.Hunt, 1)));
            Check(() => SkyIcon(pg, R0926SkyPages.Kind.Hunt, 0) == "r0926_i_treasure" && SkyIcon(pg, R0926SkyPages.Kind.Hunt, 1) == "r0926_i_treasure_lock",
                "근처 보물찾기 장 — 비밀번호를 건 판(북촌)만 자물쇠 달린 보물 그림");

            // 1b) 광고 줄 (관리자가 /ads/admin 에서 띄운 협력 업체) — 축제 · 핫플 맨 위에 '광고 · …' · 고른 이모지 그림.
            //     지금 보는 장이면 노출 한 번(다시 봐도 한 번) · 누르면 클릭 한 번과 업체 링크(안 적었으면 카카오맵의 그 자리)
            Do(() => pg.EditorGo(R0926SkyPages.Kind.Festival));
            Wait(() => SkyIcon(pg, R0926SkyPages.Kind.Festival) == "emoji_1f950", 8f, "축제 광고 줄 이모지 그림");
            Sleep(1.3f);   // 라이브 장에서 세 장을 건너온다 — 다 멈춘 뒤 찍는다
            Shot("sp_festival_ad");
            Check(() => SkyRowText(pg, R0926SkyPages.Kind.Festival) == "시험 제휴 카페 | " + R0926Nearby.AdLabel + " · 가을 한정 1+1",
                "축제 장 맨 위는 광고 줄 — 가장 가까운 축제(5km)보다 멀어도(6.7km) · '광고 · 가을 한정 1+1' · 고른 이모지 🥐");
            Do(() => pg.EditorGo(R0926SkyPages.Kind.Hot));
            Wait(() => SkyIcon(pg, R0926SkyPages.Kind.Hot) == "emoji_1f3a8", 8f, "핫플 광고 줄 이모지 그림");
            Sleep(0.9f);
            Shot("sp_hot_ad");
            Check(() => SkyRowText(pg, R0926SkyPages.Kind.Hot) == "시험 제휴 공방 | " + R0926Nearby.AdLabel + " · 체험 10% 할인", "핫플 장 맨 위 광고 줄 — '광고 · 체험 10% 할인' · 고른 이모지 🎨");
            Do(() => R0926Nearby.EditorOpened?.Clear());
            Press(() => SkyScreenRect(SkyRow(pg, R0926SkyPages.Kind.Hot, 0)?.root)?.center, 0.12f, null);
            Sleep(0.6f);
            Do(() => pg.EditorGo(R0926SkyPages.Kind.Festival));
            Sleep(0.9f);
            Press(() => SkyScreenRect(SkyRow(pg, R0926SkyPages.Kind.Festival, 0)?.root)?.center, 0.12f, null);
            Sleep(1.0f);
            Do(() => log.Add("  연 쪽: " + string.Join(" , ", R0926Nearby.EditorOpened ?? new List<string>()) + " · 센 것: " + DevGet(MpMe, "/api/dev/ad_events")));
            Check(() => { var o = R0926Nearby.EditorOpened; return o != null && o.Count == 2 && o[0] == "https://woopang.com/" && o[1].StartsWith("https://map.kakao.com/link/map/"); },
                "광고 줄을 누르면 업체 링크 · 링크를 안 적은 광고는 카카오맵의 그 자리");
            Check(() => Has(DevGet(MpMe, "/api/dev/ad_events"), "\"festival|dev-fest|view\":1", "\"hot|dev-hot|view\":1", "\"festival|dev-fest|click\":1", "\"hot|dev-hot|click\":1"),
                "노출은 광고마다 한 번(장을 다시 봐도) · 누르면 클릭 한 번 — 축제 · 핫플 자리를 따로 센다");

            // 2) 하늘에서 옆으로 밀기
            Do(() => pg.EditorGo(R0926SkyPages.Kind.Weather));
            Sleep(0.8f);
            Do(() => move[0] = new Vector2(-(SkyScreenRect(glass)?.width ?? 0f) * 0.35f, 0f));
            Finger(mid, move, 10, null, null);
            Sleep(0.8f);
            Check(() => pg.EditorCurrent == R0926SkyPages.Kind.Hunt, "왼쪽으로 밀면 다음 장 (근처 보물찾기)");
            Check(() => PlayerPrefs.GetInt("Sky_Swiped", 0) == 1, "밀어 봤다고 저장 — 다음부턴 옆 장 미리 보이기 없음");
            Do(() => move[0] = new Vector2((SkyScreenRect(glass)?.width ?? 0f) * 0.35f, 0f));
            Finger(mid, move, 10, null, null);
            Sleep(0.8f);
            Check(() => pg.EditorCurrent == R0926SkyPages.Kind.Weather, "오른쪽으로 밀면 날씨로");

            // 3) 아래로 끌어 고정
            Do(() => { move[0] = new Vector2(0f, -(SkyScreenRect(glass)?.height ?? 0f) * 0.32f); maxPull = 0f; sawArmed = false; });
            Finger(mid, move, 12, () => { maxPull = Mathf.Max(maxPull, -pg.EditorPull); sawArmed |= pg.EditorArmed; }, "sp_pulling");
            Sleep(0.8f);
            Shot("sp_pinned");
            Do(() => log.Add($"  끄는 동안 판이 내려온 만큼 최대 {maxPull:0} · 놓으면 고정될 때 📌 미리 보임 {sawArmed}"));
            Check(() => sawArmed && maxPull > 60f, "아래로 끄는 동안 판이 손가락을 따라 내려오고, 충분히 끌면 📌 가 미리 금색");
            Check(() => sky.Pinned && frame.parent == pinLayer, "놓으면 고정 — 틀이 화면 UI 의 고정 자리로");
            Check(() => glassAlpha() > 0.5f, "고정하면 어두운 유리 바탕");
            Check(() => PlayerPrefs.GetInt("Sky_PinUsed", 0) == 1, "처음 고정 — 푸는 법 안내 한 번 (다음부턴 안 뜸)");
            Do(() =>
            {
                var g = SkyScreenRect(glass);
                var a = SkyScreenRect(Find("LocationManagerPanel") as RectTransform);
                var d = SkyScreenRect(Find("Dock0926") as RectTransform);
                float floor = Mathf.Max(a?.yMax ?? 0f, d?.yMax ?? 0f);
                bool ok = g.HasValue && g.Value.yMin > floor && g.Value.xMin >= 0f && g.Value.xMax <= Screen.width && g.Value.yMax < Screen.height;
                log.Add((ok ? "PASS " : "FAIL ") + "고정한 판 — 주소줄 · 도크 위, 화면 안" + (g.HasValue
                    ? $" (유리 x {g.Value.xMin:0}~{g.Value.xMax:0} · y {g.Value.yMin:0}~{g.Value.yMax:0} · 화면 폭의 {g.Value.width / Screen.width:P0} · 높이의 {g.Value.height / Screen.height:P0} · 주소줄 윗변 {a?.yMax:0} · 도크 윗변 {d?.yMax:0} · 화면 {Screen.width}x{Screen.height})"
                    : " (유리 없음)"));
            });

            // 4) 휴대폰을 내려도 고정한 판은 그대로 — 손가락도 받는다
            Do(() => AimCamera(0f, 0f));
            Sleep(2.5f);
            Shot("sp_pinned_down");
            Check(() => !showing() && sky.Pinned && frame.gameObject.activeInHierarchy && frame.GetComponent<CanvasGroup>().alpha > 0.99f,
                "휴대폰을 내려 하늘 판이 사라져도 고정한 판은 그대로");
            HitCheck(() => frame, false, "고정한 판이 다른 화면 UI 에 가려지지 않음");
            Do(() => move[0] = new Vector2(-(SkyScreenRect(glass)?.width ?? 0f) * 0.35f, 0f));
            Finger(mid, move, 10, null, null);
            Sleep(0.8f);
            Shot("sp_pinned_hunt");
            Check(() => pg.EditorCurrent == R0926SkyPages.Kind.Hunt && sky.Pinned, "고정한 판도 옆으로 밀면 다음 장 (고정은 그대로)");

            // 5) 위로 밀어 풀기 — 하늘을 안 보고 있으니 위로 날아가며 사라지고, 틀은 하늘 판으로 돌아간다
            Do(() => move[0] = new Vector2(0f, (SkyScreenRect(glass)?.height ?? 0f) * 0.32f));
            Finger(mid, move, 12, null, "sp_pushing");
            Sleep(0.9f);
            Check(() => !sky.Pinned && frame.parent == skyBoard && !frame.gameObject.activeInHierarchy, "위로 밀면 고정 풀림 — 하늘 판으로 돌아감 (하늘을 안 봐서 안 보임)");
            Do(() => AimCamera(-45f, 0f));
            Wait(() => showing() && frame.gameObject.activeInHierarchy, 6f, "다시 하늘을 보면 판이 하늘 자리에");
            Sleep(2.0f);
            Shot("sp_back_sky");
            Check(() => frame.parent == skyBoard && Mathf.Abs(frame.anchoredPosition.y) < 1f && Mathf.Abs(frame.localScale.x - 1f) < 0.01f && glassAlpha() < 0.01f,
                "하늘에선 틀이 판에 꽉 차게 · 유리 투명 (고정 전과 같음)");

            // 6) 경계에서 깜빡이지 않게 — 한 번 뜬 판은 설정 각도(28°)보다 8° 더 내려야 사라진다
            Do(() => AimCamera(-(R0926SkySettings.MinPitch - 4f), 0f));
            Sleep(1.5f);
            Check(() => showing(), "설정 각도보다 4° 내려도 그대로");
            Do(() => AimCamera(-(R0926SkySettings.MinPitch - 12f), 0f));
            Sleep(1.0f);
            Check(() => !showing(), "12° 내리면 사라짐");

            // 7) 판을 누르고 있는 동안은 휴대폰이 기울어도 그대로 (누르는 손에 휴대폰이 같이 기운다) — 날씨 장 가운데라 눌러도 열리는 것 없음
            Do(() => AimCamera(-45f, 0f));
            Wait(() => showing() && skyAlpha() > 0.9f, 8f, "다시 뜸");
            Do(() => pg.EditorGo(R0926SkyPages.Kind.Weather));
            Sleep(0.8f);
            Do(() => { log.Add("  누를 자리 맨 위: " + TopHitAt(glass)); heldOk = true; heldFor = 0; });
            Press(mid, 2.0f, t => { if (t > 0.2) AimCamera(-10f, 0f); if (t > 0.4) heldOk &= showing(); heldFor = t; });
            Do(() => log.Add($"  누른 채 {heldFor:0.0}초 · 그동안 휴대폰 10°"));
            Check(() => heldOk, "판을 누르고 있는 동안(2초 — 붙잡는 1.2초보다 길게)은 휴대폰을 내려도 판이 그대로");
            Sleep(2.5f);
            Check(() => !showing(), "손을 떼면 잠시 뒤 사라짐");

            // 8) 📌 로 고정 · 풀기 — 하늘을 보고 있으면 풀 때 하늘 자리로 돌아간다
            Do(() => AimCamera(-45f, 0f));
            Wait(() => showing() && skyAlpha() > 0.9f, 8f, "다시 뜸");
            Sleep(0.5f);
            Press(pinAt, 0.12f, null);
            Sleep(0.8f);
            Shot("sp_pin_button");
            Check(() => sky.Pinned && frame.parent == pinLayer, "📌 를 누르면 고정");
            Press(pinAt, 0.12f, null);
            Sleep(0.9f);
            Shot("sp_unpin_sky");
            Check(() => !sky.Pinned && frame.parent == skyBoard && frame.gameObject.activeInHierarchy && glassAlpha() < 0.01f, "📌 를 다시 누르면 하늘 자리로 (하늘을 보고 있어 그대로 보임)");

            // 9) 고정한 판은 창 아래 — 목록 시트를 열면 가려지고 닫으면 그대로. 설정 '동네 소식 · 라이브' 줄도 본다
            Press(pinAt, 0.12f, null);
            Sleep(0.8f);
            Do(() => AimCamera(0f, 0f));
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Do(() => Modes()?.Show(2));
            Sleep(1.5f);
            Do(() => ScrollToChild(S + "SecSky"));
            Sleep(0.8f);
            Shot("sp_settings");
            HitCheck(() => frame, true, "목록 창을 열면 고정한 판은 창 아래");
            Check(() => { var t = Find(S + "GroupChannels/RowEvents/Sub")?.GetComponent<Text>(); return t != null && t.preferredHeight <= ((RectTransform)t.transform).rect.height + 1f; },
                "설정 '동네 소식 · 라이브' 아랫줄이 한 줄에 들어감");
            CloseAll();
            Sleep(0.6f);
            Shot("sp_after_list");
            Check(() => sky.Pinned && frame.gameObject.activeInHierarchy, "창을 닫으면 고정한 판이 그대로");

            Check(() => errors.Count == 0, "오류 없음");
            Do(() => { foreach (var e in errors) log.Add("  오류: " + e); });
            Do(() => { Application.logMessageReceived -= onLog; SkyPagesCleanup(); });
        }

        // 하늘 옆 장의 줄 — k 장의 i 번째 줄
        private static R0926SkyPages.Row SkyRow(R0926SkyPages pg, R0926SkyPages.Kind k, int i)
        {
            var pages = typeof(R0926SkyPages).GetField("pages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(pg) as R0926SkyPages.Page[];
            if (pages != null) foreach (var p in pages) if (p != null && p.kind == k && p.rows != null && i < p.rows.Length) return p.rows[i];
            return null;
        }
        private static string SkyIcon(R0926SkyPages pg, R0926SkyPages.Kind k, int i = 0) { var r = SkyRow(pg, k, i); return r != null && r.icon != null && r.icon.sprite != null ? r.icon.sprite.name : ""; }
        private static string SkyRowText(R0926SkyPages pg, R0926SkyPages.Kind k) { var r = SkyRow(pg, k, 0); return r == null || r.title == null || r.sub == null ? "" : r.title.text + " | " + r.sub.text; }

        // 근처 보물찾기 두 판 — 진행 중(공식 · 420m · 7/100명 · 상품) · 3시간 뒤 시작(비공개 '5km 이내' · 비밀번호)
        private static string SkyHuntJson()
        {
            string soon = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
            const string me = "\"is_host\":false,\"me\":{\"joined\":false,\"rank\":0,\"gave_up\":false},\"full\":false";
            return "{\"prefs\":{\"enabled\":true},\"hosted\":[],\"joined\":[],\"hunts\":["
                   + "{\"id\":990101,\"status\":\"live\"," + me + ",\"title\":\"시험 광화문 보물찾기\",\"prize\":\"아메리카노 기프티콘\",\"official\":true,"
                   + "\"distance_m\":420,\"participants\":7,\"max_players\":100},"
                   + "{\"id\":990102,\"status\":\"scheduled\"," + me + ",\"title\":\"시험 북촌 골목 보물찾기\",\"prize\":\"\",\"official\":false,\"mode\":\"code\","
                   + "\"distance_text\":\"5km 이내\",\"participants\":2,\"max_players\":0,\"starts_at\":\"" + soon + "\"}]}";
        }

        // 화면에서 차지하는 자리 (픽셀) — 하늘 판은 월드 캔버스라 카메라로 비추고, 고정하면 화면 UI 라 그대로
        private static Rect? SkyScreenRect(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return null;
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            var w = new Vector3[4];
            rt.GetWorldCorners(w);
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in w)
            {
                if (cam != null && cam.WorldToScreenPoint(p).z <= 0f) return null;
                Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, p);
                lo = Vector2.Min(lo, s);
                hi = Vector2.Max(hi, s);
            }
            return Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
        }

        // 가상 손가락 — at 에서 누른 채 seconds 동안 가만히 있다가 뗀다 (watch 에 누른 뒤 흐른 초)
        private static void Press(Func<Vector2?> at, float seconds, Action<double> watch)
        {
            Vector2 p = default;
            double from = 0;
            bool skip = false;
            Do(() =>
            {
                var a = at();
                skip = a == null || ptTouch == null;
                if (skip) { log.Add("  누를 자리 없음"); return; }
                p = a.Value;
                from = EditorApplication.timeSinceStartup;
                InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Began, position = p });
            });
            steps.Enqueue(() =>
            {
                if (skip) return true;
                double t = EditorApplication.timeSinceStartup - from;
                watch?.Invoke(t);
                if (t < seconds) return false;
                InputSystem.QueueStateEvent(ptTouch, new TouchState { touchId = 7, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = p });
                return true;
            });
        }

        private static void StashSkyPrefs()
        {
            var parts = new List<string>();
            foreach (var k in SkyPrefKeys) parts.Add(PlayerPrefs.HasKey(k) ? k + "\u0001" + PlayerPrefs.GetInt(k) : k);
            SessionState.SetString(SkyPrefsBackupKey, string.Join("\u0002", parts));
        }
        private static void RestoreSkyPrefs()
        {
            string s = SessionState.GetString(SkyPrefsBackupKey, "");
            if (s.Length == 0) return;
            SessionState.EraseString(SkyPrefsBackupKey);
            foreach (var part in s.Split('\u0002'))
            {
                int i = part.IndexOf('\u0001');
                if (i < 0) PlayerPrefs.DeleteKey(part);
                else PlayerPrefs.SetInt(part.Substring(0, i), int.Parse(part.Substring(i + 1)));
            }
            PlayerPrefs.Save();
        }

        // 시험 서버 주소 · 시험 아이디 · 하늘 설정을 되돌린다 (시험이 오류로 끊겨도 Tick 이 부른다)
        private static void SkyPagesCleanup()
        {
            skyPagesRun = false;
            var sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
            if (sky != null && sky.Pinned) sky.SetPinned(false);
            SfLogin(false);
            MessagePanelManager.EditorBase = null;
            ProfileManager.EditorBase = null;
            FollowManager.EditorBase = null;
            P2PManager.EditorBase = null;
            FirebaseNotification.EditorBase = null;
            R0926FriendsMap.EditorBase = null;
            R0926Nearby.EditorBase = null;
            R0926Nearby.EditorOpened = null;
            R0926HuntApi.EditorBase = null;
            R0926HuntApi.EditorTestUser = null;
            R0926MapTiles.EditorBase = null;
            typeof(R0926MapTiles).GetField("info", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, null);
            PlayerPrefs.DeleteKey(LoginManager.AUTH_TOKEN_KEY);
            PlayerPrefs.Save();
            RestoreSkyPrefs();
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
            // 근처 축제·보호소는 시험 서버의 정해 둔 답 (관광공사·카카오를 부르지 않는다)
            Do(() =>
            {
                R0926Nearby.EditorBase = "http://127.0.0.1:5099";
                DevGet(FiMe, "/api/dev/nearby_mode?festivals=ok");
                DevGet(FiMe, "/api/dev/ad_events?clear=1");
                R0926Nearby.EditorForget();
                R0926Nearby.EditorOpened = new List<string>();   // 줄을 눌러도 진짜 쪽은 열지 않는다
            });
            Do(() => Fm()?.SetCategory(FilterManager.CategoryFilterState.All));
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.2f);
            Shot("filters_list");
            Do(() => log.Add("  칩: " + ChipLayout()));
            Check(() => ChipGridOk(), "칩 두 줄 4칸씩 — 위 전체·애견동반·공공데이터·축제 · 아래 지하철·터미널 / 기차·3D·사람, 칸 경계가 위아래로 맞음 (10-23)");
            Do(() => log.Add("  이름: " + ChipLabel("PublicDataToggle") + " · " + ChipLabel("FestivalToggle") + " · " + ChipLabel("TerminalToggle")));
            Check(() => ChipLabel("TerminalToggle").Contains("/"), "터미널 / 기차 한 칩");
            Check(() => ChipLabel("FestivalToggle") == "축제" || ChipLabel("FestivalToggle") == "Festivals", "'공공데이터' 오른쪽에 '축제' 칩");
            Check(() => ChipOn("FestivalToggle") == false, "축제 칩은 꺼진 채 시작");
            Check(() => ChipLabel("PublicDataToggle") == "공공데이터" || ChipLabel("PublicDataToggle") == "Public data", "'공공' → '공공데이터'");
            Check(() => ChipTextsFit(), "다섯 나라 말 모두 칩 칸 안에 (한 칸이 된 '터미널 / 기차' 포함)");
            Check(() => NoOldLongPress(), "0926 칩에 옛 길게 누르기(LongPressHandler)가 없음 — 0.8초 넘게 누른 손을 떼도 칩이 다시 꺼지지 않음");
            Check(() => !AnyActive("AskAI0926"), "'AI에게 묻기' 숨김");
            Do(() => log.Add("  '목록|지도|설정' 칸 높이 " + TabH() + " · 칩 높이 " + ChipHeight("CategoryToggle")));
            Check(() => Mathf.Abs(TabH() - ChipHeight("CategoryToggle")) < 1f, "'목록|지도|설정' 칸 높이 = 필터 칩 높이");
            Check(() => WhiteChip("CategoryToggle"), "켜진 '전체' 칩은 꽉 찬 흰색");
            Check(() => PurpleChip("3DObjectToggle"), "켜진 '3D' 칩은 꽉 찬 보라 + 흰 글자 (10-23)");

            // '전체' → 고르기 목록
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.4f);
            Shot("filters_menu");
            Check(() => MenuOpen(), "'전체'를 누르면 분류 고르기 목록이 펼쳐짐");
            Check(() => MenuRows() == 8, "분류 8줄");
            Do(() => log.Add("  분류 순서: " + MenuOrder()));
            Check(() => MenuOrder() == "전체·음식점·카페·샵·공원·스포츠·화장실·랜드마크" || MenuOrder() == "All·Food·Cafe·Shop·Park·Sports·Toilet·Landmark",
                  "분류 순서 — 전체·음식점·카페·샵·공원·스포츠·화장실·랜드마크 (10-23)");
            Check(() => MenuIconsOk(), "줄 앞은 분류 그림 (전체·그릇·모카 잔·가방·나무·공·화장실·건물) — 예전 색 점 없음");
            Check(() => Fm().CategoryState == FilterManager.CategoryFilterState.All, "펼치기만 해서는 분류가 바뀌지 않음");
            Do(() => PickCategory(2));
            Sleep(0.4f);
            Shot("filters_cafe");
            Check(() => Fm().CategoryState == FilterManager.CategoryFilterState.Cafe, "셋째 줄은 '카페'");
            Check(() => ChipIcon("CategoryToggle") == "r0926_c_cafe", "칩 점 자리에 모카 잔 그림");
            Check(() => ChipTint("CategoryToggle", new Color(0.643f, 0.459f, 0.318f)), "카페는 모카색 (10-23)");
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.3f);
            Do(() => PickCategory(5));
            Sleep(0.4f);
            Check(() => Fm().CategoryState == FilterManager.CategoryFilterState.Sport && ChipIcon("CategoryToggle") == "r0926_c_sport", "여섯째 줄은 '스포츠' · 공 그림");
            Check(() => ChipTint("CategoryToggle", new Color(0.94f, 0.28f, 0.28f)), "스포츠는 빨강 (10-23)");
            Do(() => ClickToggle("CategoryToggle"));
            Sleep(0.3f);
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
            Check(() => ChipIcon("CategoryToggle") == "r0926_circle", "'전체'는 그림 없이 점");

            // 터미널 / 기차 — 한 번에 둘 다
            Do(() => ClickToggle("TerminalToggle"));
            Sleep(0.3f);
            Check(() => !Fm().GetActiveFilters()["terminal"] && !Fm().GetActiveFilters()["train"], "터미널 / 기차를 끄면 둘 다 꺼짐");
            Shot("filters_terminal_off");
            Do(() => ClickToggle("TerminalToggle"));
            Sleep(0.3f);
            Check(() => Fm().GetActiveFilters()["terminal"] && Fm().GetActiveFilters()["train"], "다시 켜면 둘 다 켜짐");

            // 애견동반 — 꺼짐(회색) · 켜짐(꽉 찬 진한 노랑) 두 가지 (10-26 대표님 — 예전 '반려견' · '반려견만')
            Check(() => PetChip(false), "애견동반 꺼짐 — 다른 꺼진 칩처럼 회색 · 이름 '애견동반'");
            Do(() => ClickToggle("PetFriendlyToggle"));
            Sleep(0.4f);
            Shot("filters_pet_only");
            Check(() => Fm().PetState == FilterManager.PetFriendlyFilterState.OnlyPetFriendly, "애견동반 한 번 → 애견동반 되는 곳만");
            Check(() => PetChip(true), "켜짐 → 꽉 찬 진한 노랑 · 어두운 글자 · 이름 그대로 '애견동반'");
            // 애견동반 켜짐 — 맨 위에 가까운 유기견 보호소 세 곳 (10-23)
            Wait(() => Shown(PlaceListManager.EntryKind.Shelter) > 0, 10f, "보호소 받음");
            Sleep(0.6f);
            Shot("filters_shelter");
            Do(() => log.Add("  줄: " + RowsSummary(5)));
            Check(() => Shown(PlaceListManager.EntryKind.Shelter) == 3, "보호소는 세 곳만 (서버는 다섯 곳)");
            Check(() => ShelterOnTop(), "보호소 세 줄이 맨 위 — 가까운 가게보다 먼저");
            Check(() => RowIcons(3).TrueForAll(s => s == "r0926_f_shelter"), "보호소 줄 앞은 강아지 그림");
            Check(() => ShownLinks(PlaceListManager.EntryKind.Shelter).TrueForAll(l => l.StartsWith("https://place.map.kakao.com/")), "보호소를 누르면 카카오맵 장소 쪽 (전화·길찾기)");
            Do(() => ClickToggle("PetFriendlyToggle"));
            Sleep(0.3f);
            Check(() => Fm().PetState == FilterManager.PetFriendlyFilterState.All, "애견동반 다시 → 꺼짐(전부) ('애견동반 안 됨' 없음)");
            Check(() => PetChip(false), "꺼짐 → 회색 · 이름 그대로 '애견동반'");
            Shot("filters_pet_off");
            Sleep(0.4f);
            Check(() => Shown(PlaceListManager.EntryKind.Shelter) == 0, "애견동반을 끄면 보호소 줄 없음");

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
            Do(() => Pp()?.SetFilterModeExternal(UserFilterMode.None));
            Sleep(0.2f);
            Shot("filters_p2p_none");
            Check(() => ChipLabel("P2PUserToggle") == "주변 사람" || ChipLabel("P2PUserToggle") == "Nearby people", "끄면 칩 이름 '주변 사람' (10-23)");
            Do(() => Pp()?.SetFilterModeExternal(p2pWas));
            Check(() => SoundOnAll("ListPanel", "ModeSeg0926"), "'목록|지도|설정' 탭에 누름 소리 (10-21)");

            // 3D — 끄면 어둡게 · 켜면 꽉 찬 보라
            Do(() => ClickToggle("3DObjectToggle"));
            Sleep(0.3f);
            Check(() => !PurpleChip("3DObjectToggle") && !WhiteChip("3DObjectToggle"), "3D 끄면 어두운 칸");
            Shot("filters_3d_off");
            Do(() => ClickToggle("3DObjectToggle"));
            Sleep(0.3f);
            Check(() => PurpleChip("3DObjectToggle"), "3D 켜면 다시 꽉 찬 보라");

            // 축제 — 목록이 거리와 상관없이 근처 축제만 (10-23) · 맨 위는 관리자가 띄운 광고 줄 (10-25)
            Do(() => ClickToggle("FestivalToggle"));
            Wait(() => Shown(PlaceListManager.EntryKind.Festival) > 0, 10f, "축제 받음");
            Wait(() => RowIcons(1).Count > 0 && RowIcons(1)[0] == "emoji_1f950", 8f, "광고 줄 이모지 그림");
            Sleep(0.6f);
            Shot("filters_festival");
            Do(() => log.Add("  줄: " + RowsSummary(6) + " · 집계: " + SummaryText()));
            Check(() => Fm().GetActiveFilters()["festival"], "축제 칩을 켜면 festival 필터");
            Check(() => Shown(PlaceListManager.EntryKind.Festival) == 5 && ShownTotal() == 5, "목록이 광고 한 줄 + 축제 네 곳만 (가게·사람·보물찾기 없음)");
            Check(() => FestivalRowsOk(), "맨 위 광고 줄 '광고 · 6.7km'(가장 가까운 축제보다 멀어도) · 그 아래 가까운 순 · 거리 칸은 '며칠부터 · 거리' / '진행 중 · 거리' / '오늘까지 · 거리' · 200km 먼 곳도 나옴");
            Check(() => string.Join(",", RowIcons(5)) == "emoji_1f950,r0926_f_fireworks,r0926_f_maple,r0926_f_crab,r0926_f_fruit", "줄 앞은 축제마다 그림 (불꽃·단풍·꽃게·사과) · 광고 줄은 고른 이모지 🥐");
            Check(() =>
            {
                var l = ShownLinks(PlaceListManager.EntryKind.Festival);
                return l.Count == 5 && l[0].StartsWith("https://map.kakao.com/link/map/") && l.GetRange(1, 4).TrueForAll(x => x.StartsWith("https://m.search.naver.com/search.naver?query="));
            }, "축제를 누르면 축제 이름으로 검색 · 광고 줄은 업체 링크(안 적었으면 카카오맵의 그 자리)");
            Check(() => SummaryText().EndsWith(" 4"), "집계 줄의 축제 수는 광고 줄을 빼고 4");
            Do(() => { R0926Nearby.EditorOpened?.Clear(); ListRows()[0].GetComponent<Button>().onClick.Invoke(); });
            Sleep(0.8f);
            Check(() => R0926Nearby.EditorOpened != null && R0926Nearby.EditorOpened.Count == 1 && R0926Nearby.EditorOpened[0].StartsWith("https://map.kakao.com/link/map/"),
                "목록의 광고 줄을 누르면 그 자리 (카카오맵)");
            Check(() => Has(DevGet(FiMe, "/api/dev/ad_events"), "\"festival|dev-fest|view\":1", "\"festival|dev-fest|click\":1"), "목록의 광고 줄도 보이면 노출 한 번 · 누르면 클릭 한 번");
            Check(() => ChipsLocked(true, "CategoryToggle", "PetFriendlyToggle", "PublicDataToggle", "MetroToggle", "TerminalToggle", "3DObjectToggle", "P2PUserToggle"),
                  "축제를 보는 동안 다른 칩은 잠김 (어둡게)");
            Do(() => ClickToggle("FestivalToggle"));
            Sleep(0.6f);
            Check(() => Shown(PlaceListManager.EntryKind.Festival) == 0 && ShownTotal() > 0, "다시 누르면 원래 목록");
            Check(() => ChipsLocked(false, "CategoryToggle", "PetFriendlyToggle", "PublicDataToggle", "MetroToggle", "TerminalToggle", "3DObjectToggle", "P2PUserToggle"), "칩 잠김 풀림");

            // 공공데이터 신청 전(서버가 not_registered) — 빈 목록 대신 안내 한 줄
            Do(() => { DevGet(FiMe, "/api/dev/nearby_mode?festivals=not_registered"); R0926Nearby.EditorForget(); });
            Do(() => ClickToggle("FestivalToggle"));
            Wait(() => R0926Nearby.FestivalStatus == "not_registered", 10f, "축제 — 신청 전");
            Sleep(0.6f);
            Shot("filters_festival_pending");
            Check(() => Shown(PlaceListManager.EntryKind.Festival) == 1 && SummaryText().Contains(R0926Nearby.EmptyMessage().Split('\n')[0]),
                "신청 전이면 '축제 정보를 준비하고 있어요' 안내 — 광고 줄은 그대로 맨 위");
            Do(() => ClickToggle("FestivalToggle"));
            Sleep(0.4f);
            Do(() => { DevGet(FiMe, "/api/dev/nearby_mode?festivals=ok"); R0926Nearby.EditorBase = null; R0926Nearby.EditorOpened = null; R0926Nearby.EditorForget(); });
            Do(() => Click("ListPanel/DockMirror0926/XButton_List/ClosePx0926"));
            Sleep(1.0f);
        }

        private const string FiMe = "hunt_test_filters";
        private static bool ChipOn(string name) { var t = Chip(name)?.GetComponent<Toggle>(); return t != null && t.isOn; }
        private static bool PurpleChip(string name)
        {
            var b = Chip(name)?.GetComponent<Image>();
            var t = Chip(name)?.Find("Label")?.GetComponent<Text>();
            return b != null && t != null && b.color.a > 0.95f && Mathf.Abs(b.color.r - 0.545f) < 0.05f && Mathf.Abs(b.color.g - 0.361f) < 0.05f && Mathf.Abs(b.color.b - 0.965f) < 0.05f
                && t.color.r > 0.95f && t.color.g > 0.95f && t.color.b > 0.95f;
        }
        // 애견동반 칩 (10-26 대표님) — 켜짐: 꽉 찬 진한 노랑(#FFC107) + 어두운 글자 · 꺼짐: 다른 꺼진 칩처럼 거의 투명한 회색 + 흐린 글자. 이름은 늘 '애견동반'
        private static bool PetChip(bool on)
        {
            var c = Chip("PetFriendlyToggle");
            var b = c != null ? c.GetComponent<Image>() : null;
            var t = c != null ? c.Find("Label")?.GetComponent<Text>() : null;
            if (b == null || t == null) return false;
            bool named = t.text == "애견동반" || t.text == "Pet-friendly";
            if (!named || on != b.color.a > 0.9f)
                log.Add($"  애견동반 칩 '{t.text}' 바탕 {ColorUtility.ToHtmlStringRGBA(b.color)} 글자 {ColorUtility.ToHtmlStringRGBA(t.color)}");
            if (on) return named && b.color.a > 0.9f && Mathf.Abs(b.color.r - 1f) < 0.03f && Mathf.Abs(b.color.g - 0.757f) < 0.03f && b.color.b < 0.1f && t.color.r < 0.2f;
            return named && b.color.a < 0.1f && t.color.a < 0.5f;
        }
        private static string ChipIcon(string name) { var d = Chip(name)?.Find("Dot0926")?.GetComponent<Image>(); return d != null && d.sprite != null ? d.sprite.name : ""; }
        // 켜진 일반 칸 — 그 분류 색이 옅게 깔린다
        private static bool ChipTint(string name, Color want)
        {
            var b = Chip(name)?.GetComponent<Image>();
            return b != null && Mathf.Abs(b.color.r - want.r) < 0.02f && Mathf.Abs(b.color.g - want.g) < 0.02f && Mathf.Abs(b.color.b - want.b) < 0.02f;
        }
        private static bool ChipsLocked(bool locked, params string[] names)
        {
            foreach (var n in names)
            {
                var t = Chip(n)?.GetComponent<Toggle>();
                if (t == null || t.interactable == locked) { log.Add("  잠김 다름 " + n); return false; }
            }
            return true;
        }
        // 다섯 나라 말 중 가장 긴 이름이 앱 최소 글자(38)로 칩 칸에 들어가나 — 점·∨ 자리까지
        private static bool ChipTextsFit()
        {
            var sets = new (string chip, bool arrow, string[] texts)[]
            {
                ("CategoryToggle", true, new[] { "랜드마크", "Landmark", "ショップ", "Deporte", "Comida" }),
                ("PetFriendlyToggle", false, new[] { "애견동반", "Pet-friendly", "ペット同伴", "可带宠物", "Con mascotas" }),
                ("PublicDataToggle", false, new[] { "공공데이터", "Public data", "公共データ", "公共数据", "Público" }),
                ("FestivalToggle", false, new[] { "축제", "Festivals", "お祭り", "节庆", "Festivales" }),
                ("MetroToggle", false, new[] { "지하철", "Subway", "地下鉄", "地铁", "Metro" }),
                ("TerminalToggle", false, new[] { "터미널 / 기차", "Bus / Train", "ターミナル / 駅", "客运站 / 火车站", "Bus / Tren" }),
                ("P2PUserToggle", false, new[] { "주변 사람", "Nearby people", "Following only", "近くの人", "Gente cerca", "Solo seguidos" }),
            };
            bool ok = true;
            foreach (var s in sets)
            {
                var c = Chip(s.chip);
                var t = c != null ? c.Find("Label")?.GetComponent<Text>() : null;
                var d = c != null ? c.Find("Dot0926") as RectTransform : null;
                if (t == null || d == null) { log.Add("  칩 없음 " + s.chip); return false; }
                string was = t.text; int size = t.fontSize;
                float room = c.rect.width - 16f - (s.arrow ? 46f + 18f + 52f : d.sizeDelta.x + 18f);
                t.fontSize = 38;
                foreach (var x in s.texts)
                {
                    t.text = x;
                    if (t.preferredWidth > room) { log.Add("  칸 넘침 " + s.chip + " '" + x + "' " + Mathf.RoundToInt(t.preferredWidth) + " > " + Mathf.RoundToInt(room)); ok = false; }
                }
                t.text = was; t.fontSize = size;
            }
            return ok;
        }
        private static string MenuOrder()
        {
            var names = new SortedDictionary<int, string>();
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>())
            {
                int i = MenuRow(b);
                var t = i >= 0 ? b.transform.Find("Label")?.GetComponent<Text>() : null;
                if (t != null) names[i] = t.text;
            }
            return string.Join("·", names.Values);
        }
        private static bool MenuIconsOk()
        {
            string[] want = { "all", "food", "cafe", "shop", "park", "sport", "toilet", "landmark" };
            int n = 0;
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>())
            {
                int i = MenuRow(b);
                if (i < 0) continue;
                var ic = b.transform.Find("Icon")?.GetComponent<Image>();
                if (ic == null || ic.sprite == null || ic.sprite.name != "r0926_c_" + want[i] || b.transform.Find("Dot") != null)
                { log.Add("  분류 그림 다름 " + i + " " + (ic != null && ic.sprite != null ? ic.sprite.name : "없음")); return false; }
                n++;
            }
            return n == 8;
        }

        private static PlaceListManager Plm() => UnityEngine.Object.FindAnyObjectByType<PlaceListManager>();
        private static int ShownTotal() { var p = Plm(); return p != null ? p.ShownEntries.Count : 0; }
        private static int Shown(PlaceListManager.EntryKind kind)
        {
            var p = Plm();
            int n = 0;
            if (p != null) foreach (var e in p.ShownEntries) if (e.kind == kind) n++;
            return n;
        }
        private static List<string> ShownLinks(PlaceListManager.EntryKind kind)
        {
            var r = new List<string>();
            var p = Plm();
            if (p != null) foreach (var e in p.ShownEntries) if (e.kind == kind) r.Add(e.link ?? "");
            if (r.Count == 0) r.Add("");   // 줄이 없으면 실패로
            return r;
        }
        private static bool ShelterOnTop()
        {
            var p = Plm();
            if (p == null || p.ShownEntries.Count < 3) return false;
            for (int i = 0; i < 3; i++) if (p.ShownEntries[i].kind != PlaceListManager.EntryKind.Shelter) return false;
            if (p.ShownEntries.Count > 3)
                log.Add("  넷째 줄 " + p.ShownEntries[3].name + " " + Mathf.RoundToInt(p.ShownEntries[3].distance) + "m · 첫 보호소 " + Mathf.RoundToInt(p.ShownEntries[0].distance) + "m");
            return true;
        }
        private static bool FestivalRowsOk()
        {
            var p = Plm();
            if (p == null || p.ShownEntries.Count != 5) return false;
            var ad = p.ShownEntries[0];
            if (!ad.promo || ad.distanceText != R0926Nearby.AdLabel + " · " + R0926Nearby.Km(ad.distance) || ad.distance < p.ShownEntries[1].distance)
            {
                log.Add("  광고 줄 다름 " + ad.name + " '" + ad.distanceText + "' " + Mathf.RoundToInt(ad.distance) + "m");
                return false;
            }
            float last = -1f;
            for (int i = 1; i < p.ShownEntries.Count; i++)
            {
                var e = p.ShownEntries[i];
                string d = e.distanceText ?? "";
                bool when = d.Contains("부터") || d.Contains("진행 중") || d.Contains("오늘까지") || d.StartsWith("from") || d.StartsWith("on now") || d.StartsWith("ends today");
                if (!when || !d.Contains(" · ") || !(d.EndsWith("km") || d.EndsWith("m")) || e.distance < last) { log.Add("  축제 줄 다름 " + e.name + " '" + d + "'"); return false; }
                last = e.distance;
            }
            return last > 150000f;
        }
        // 목록 줄(화면) 앞 그림 이름 — 위에서부터 n 줄
        private static List<string> RowIcons(int n)
        {
            var r = new List<string>();
            foreach (var row in ListRows())
            {
                if (r.Count >= n) break;
                var d = row.Find("Dot")?.GetComponent<Image>();
                r.Add(d != null && d.sprite != null ? d.sprite.name : "");
            }
            return r;
        }
        private static string RowsSummary(int n)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var row in ListRows())
            {
                if (n-- <= 0) break;
                var d = row.Find("Dot")?.GetComponent<Image>();
                sb.Append(row.Find("Name")?.GetComponent<Text>()?.text).Append(" [").Append(row.Find("Dist")?.GetComponent<Text>()?.text).Append("] ")
                  .Append(d != null && d.sprite != null ? d.sprite.name : "-").Append(" | ");
            }
            return sb.ToString();
        }
        private static List<Transform> ListRows()
        {
            var r = new List<Transform>();
            var pr = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>();
            var box = pr != null ? new SerializedObject(pr).FindProperty("rowContainer").objectReferenceValue as Transform : null;
            if (box == null) return r;
            foreach (Transform t in box) if (t.gameObject.activeSelf && t.name == "Row") r.Add(t);
            return r;
        }
        private static string SummaryText()
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Text>())
                if (t.name == "Summary0926" && t.gameObject.activeInHierarchy) return t.text;
            return "";
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
            string[] top = { "CategoryToggle", "PetFriendlyToggle", "PublicDataToggle", "FestivalToggle" };
            string[] bottom = { "MetroToggle", "TerminalToggle", "3DObjectToggle", "P2PUserToggle" };
            var grid = UnityEngine.Object.FindAnyObjectByType<R0926ChipGrid>();
            if (grid == null || grid.transform.Find("TrainStationToggle") != null) return false;
            int active = 0;
            foreach (Transform t in grid.transform) if (t.gameObject.activeSelf) active++;
            if (active != 8) return false;
            var T = new RectTransform[4];
            var B = new RectTransform[4];
            for (int i = 0; i < 4; i++) { T[i] = Chip(top[i]); B[i] = Chip(bottom[i]); if (T[i] == null || B[i] == null) return false; }
            bool Near(float a, float b) => Mathf.Abs(a - b) < 1.5f;
            float L(RectTransform r) => r.anchoredPosition.x;
            float R(RectTransform r) => r.anchoredPosition.x + r.sizeDelta.x;
            foreach (var r in T) if (!Near(r.anchoredPosition.y, T[0].anchoredPosition.y)) return false;
            foreach (var r in B) if (!Near(r.anchoredPosition.y, B[0].anchoredPosition.y)) return false;
            if (B[0].anchoredPosition.y >= T[0].anchoredPosition.y) return false;
            for (int i = 0; i < 4; i++) if (!Near(L(B[i]), L(T[i])) || !Near(R(B[i]), R(T[i]))) return false;
            return true;
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
                if (running) { FollowManager.EditorBase = null; MessagePanelManager.EditorBase = null; ProfileManager.EditorBase = null; P2PManager.EditorBase = null; FirebaseNotification.EditorBase = null; R0926FriendsMap.EditorBase = null; R0926MapTiles.EditorBase = null; R0926Nearby.EditorBase = null; CubeDataFixManager.EditorBase = null; CommentManager.EditorBase = null; ReportService.EditorBase = null; }   // 팔로우·검색·프로필·친구 지도·타일·근처·장소 고치기·댓글 시험 서버도
                if (treasureRun) { PlayerPrefs.DeleteKey(TreasurePref); treasureRun = false; }   // 시험 게임을 지켜보던 기록 (시험이 중간에 끊겨도)
                if (running) RestoreWoopangPrefs();   // WOOPANG 줄 삭제 시험이 덜어 둔 저장값 (시험 도중 플레이를 끈 경우)
                if (running) { RestoreSkyPrefs(); skyPagesRun = false; }   // 하늘 옆 장 시험이 바꾼 하늘 설정도
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
                    if (pl.Length > 1 && (pl[1].Trim() == "login" || pl[1].Trim() == "loginswitch" || pl[1].Trim() == "searchfollow" || pl[1].Trim() == "mapprofile" || pl[1].Trim() == "mapnear" || pl[1].Trim() == "skypages" || pl[1].Trim() == "authexpire" || pl[1].Trim() == "placeedit" || pl[1].Trim() == "comments" || pl[1].Trim() == "nocat" || pl[1].Trim() == "profileedit")) StashLogin();
                    if (pl.Length > 1 && pl[1].Trim() == "authexpire") ArmExpiredLogin();   // 만료된 로그인을 둔 채 켠다
                    RestoreWoopangPrefs();   // 지난 시험이 스크립트 다시 읽기로 끊겨 못 되돌린 것
                    RestoreSkyPrefs();
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
            if (skyPagesRun) SkyPagesCleanup();   // 하늘 옆 장 시험이 오류로 끊긴 경우 — 시험 서버 주소 · 시험 아이디 · 하늘 설정
            RestoreGameViewSize();
            R0926SkyWeather.EditorRegionOverride = null;
            CubeDataFixManager.EditorBase = null;   // 장소 고치기 시험이 오류로 끊겨도 다음 플레이가 시험 서버로 가지 않게
            CommentManager.EditorBase = null; ReportService.EditorBase = null;   // 댓글 시험도
            InputSystem.settings.editorInputBehaviorInPlayMode = savedBehavior;
            InputSystem.settings.backgroundBehavior = savedBackground;
            Application.runInBackground = savedRunInBackground;
            try { File.Delete(PlanPath); } catch (Exception e) { log.Add("plan 삭제 실패 " + e.Message); }
            File.WriteAllLines(ResultPath, log);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;   // 마지막 캡처가 저장될 틈을 둔다
        }
    }
}
