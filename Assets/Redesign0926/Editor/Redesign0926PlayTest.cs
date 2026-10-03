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
    ///  · 프로필(또는 로그인 안내): 뒤로가기로 닫힘
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
            if (mode == "store2")
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

            // 프로필 (로그인 안 돼 있으면 로그인 안내)
            Do(() => Click("Dock0926/MiniProfile"));
            Sleep(1.0f);
            Shot("profile_open");
            Do(() => log.Add("  열린 창: " + OpenPanels()));
            Back();
            Sleep(0.6f);
            Check(() => OpenPanels() == "-", "프로필/로그인 안내: 뒤로가기로 닫힘");

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

        private static void PlanStore2()
        {
            Do(() => Backdrop("bg_city.png"));
            Sleep(1.3f);
            Shot("s0_splash_a");
            Sleep(1.4f);
            Shot("s0_splash_b");
            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Sleep(6f);
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
                running = false;
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + 3.0;
                if (File.Exists(PlanPath) && !File.Exists(ResultPath + ".lock") && !EditorApplication.isCompiling
                    && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;
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
            InputSystem.settings.editorInputBehaviorInPlayMode = savedBehavior;
            InputSystem.settings.backgroundBehavior = savedBackground;
            Application.runInBackground = savedRunInBackground;
            try { File.Delete(PlanPath); } catch (Exception e) { log.Add("plan 삭제 실패 " + e.Message); }
            File.WriteAllLines(ResultPath, log);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;   // 마지막 캡처가 저장될 틈을 둔다
        }
    }
}
