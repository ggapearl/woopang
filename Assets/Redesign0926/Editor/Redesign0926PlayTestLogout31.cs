using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 31번 대표님 「그리고 '로그아웃' 할 경우에 발생하는 모달도 우리 레이아웃이랑 안어울리는데 확인하고 수정해줘」
        //  · 로그아웃 확인이 프로필 틀 안이 아니라 캔버스 바로 밑(로그인 안내 위)에서 화면 전체를 어둡게 덮는다 — 도크 X 도 눌리지 않는다
        //  · 다른 확인창과 같은 어두운 카드(1040x560) · 취소(왼쪽) / 로그아웃(오른쪽 · 빨간 글자) 알약
        //  · 취소 · 뒤로가기 = 확인창만 닫히고 프로필은 그대로
        //  시험 아이디로 로그인한 척(SfLogin)만 하고, 프로필·팔로우 요청은 닿지 않는 주소로 돌린다. 「로그아웃」(확인)은 누르지 않는다
        private static bool l31Run;

        private static void L31Finish()
        {
            if (!l31Run) return;
            l31Run = false;
            var pm = ProfileManager.Instance;
            if (pm != null && pm.logoutConfirmDialog != null && pm.logoutConfirmDialog.activeSelf) pm.logoutCancelButton?.onClick.Invoke();
            SfLogin(false);
            ProfileManager.EditorBase = null;
            FollowManager.EditorBase = null;
            R0926FriendsMap.EditorBase = null;
            R0926Nearby.EditorBase = null;
            R0926HuntApi.EditorBase = null;
        }

        private static void PlanLogout31()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            ProfileManager pm = null;
            GameObject dlg = null;

            string Lbl(Button b) => b != null ? b.transform.Find("Label0926")?.GetComponent<Text>()?.text ?? "(글자 없음)" : "(버튼 없음)";
            bool DlgOpen() => dlg != null && dlg.activeInHierarchy;
            // 화면의 한 점을 눌렀을 때 맨 위에 맞는 것
            GameObject TopHit(Vector2 screen)
            {
                var es = EventSystem.current;
                if (es == null) return null;
                var hits = new System.Collections.Generic.List<RaycastResult>();
                es.RaycastAll(new PointerEventData(es) { position = screen }, hits);
                return hits.Count > 0 ? hits[0].gameObject : null;
            }
            Vector2 ScreenOf(Transform t)
            {
                var rt = t as RectTransform;
                var c = rt.GetComponentInParent<Canvas>().rootCanvas;
                var cam = c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
                return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            }

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                l31Run = true;
                ProfileManager.EditorBase = FollowManager.EditorBase = R0926FriendsMap.EditorBase = "http://127.0.0.1:9";
                R0926Nearby.EditorBase = R0926HuntApi.EditorBase = "http://127.0.0.1:9";
                pm = ProfileManager.Instance;
                dlg = pm != null ? pm.logoutConfirmDialog : null;
                SfLogin(true);
            });
            Sleep(0.4f);
            Check(() =>
            {
                var login = Find("LoginPromptPanel");
                var full = Find("FullProfilePanel");
                log.Add($"  확인창 자리: 부모 {(dlg != null ? dlg.transform.parent.name : "-")} · 순서 {(dlg != null ? dlg.transform.GetSiblingIndex() : -1)} (로그인 안내 {(login != null ? login.GetSiblingIndex() : -1)} · 프로필 {(full != null ? full.GetSiblingIndex() : -1)})");
                return pm != null && dlg != null && !dlg.activeSelf && login != null && full != null
                    && dlg.transform.parent == login.parent && dlg.transform.GetSiblingIndex() == login.GetSiblingIndex() + 1 && dlg.transform.GetSiblingIndex() > full.GetSiblingIndex();
            }, "로그아웃 확인창이 캔버스 바로 밑 · 로그인 안내 바로 위(프로필보다 위)에 있다");

            // 내 프로필 → 로그아웃
            Do(() => typeof(ProfileManager).GetMethod("ShowProfilePanel", F).Invoke(pm, new object[] { new ProfileData { id = LoginManager.Instance.CurrentUser.id, username = "hunt_test_sfme", bio = "", followers_count = 3, following_count = 5 } }));
            Wait(() => Active("FullProfilePanel") && pm.logoutButton != null && pm.logoutButton.gameObject.activeInHierarchy, 3f, "내 프로필 · 로그아웃 버튼");
            Sleep(0.8f);
            Do(() => pm.logoutButton.onClick.Invoke());
            Wait(DlgOpen, 2f, "로그아웃 확인창 열림");
            Sleep(0.6f);
            Check(() =>
            {
                var drt = (RectTransform)dlg.transform;
                var prt = (RectTransform)dlg.transform.parent;
                var dim = dlg.GetComponent<Image>();
                var box = dlg.transform.Find("Panel") as RectTransform;
                var msg = pm.logoutConfirmText;
                var yes = pm.logoutConfirmButton;
                var no = pm.logoutCancelButton;
                string yesSpr = yes != null && yes.image != null && yes.image.sprite != null ? yes.image.sprite.name : "-";
                string noSpr = no != null && no.image != null && no.image.sprite != null ? no.image.sprite.name : "-";
                log.Add($"  덮개 {drt.rect.width:0}x{drt.rect.height:0} (캔버스 {prt.rect.width:0}x{prt.rect.height:0}) · 어둡기 {(dim != null ? dim.color.a : -1f):0.00} · 카드 {(box != null ? $"{box.rect.width:0}x{box.rect.height:0}" : "없음")}");
                log.Add($"  글 「{(msg != null ? msg.text : "-")}」 {(msg != null ? msg.fontSize : 0)}px · 버튼 왼쪽 「{Lbl(no)}」({noSpr}) · 오른쪽 「{Lbl(yes)}」({yesSpr})");
                bool leftRight = yes != null && no != null && ((RectTransform)no.transform).position.x < ((RectTransform)yes.transform).position.x;
                var yt = yes != null ? yes.transform.Find("Label0926")?.GetComponent<Text>() : null;
                return Mathf.Abs(drt.rect.width - prt.rect.width) < 1f && Mathf.Abs(drt.rect.height - prt.rect.height) < 1f
                    && dim != null && Mathf.Abs(dim.color.a - 0.5f) < 0.01f && dim.raycastTarget
                    && box != null && Mathf.Abs(box.rect.width - 1040f) < 1f && Mathf.Abs(box.rect.height - 560f) < 1f
                    && msg != null && msg.text.Length > 0 && msg.fontSize == 44
                    && Lbl(no) == "취소" && Lbl(yes) == "로그아웃" && yesSpr == "r0926_pill" && noSpr == "r0926_pill" && leftRight
                    && yt != null && yt.color.r > 0.6f && yt.color.g < 0.4f;
            }, "화면 전체 어둡게(0.5) · 어두운 카드 1040x560 · 취소(왼쪽) / 로그아웃(오른쪽 · 빨간 글자) 알약");
            Check(() =>
            {
                var x = Find("FullProfilePanel/DockMirror0926/ProfileToggle0926");
                var hit = x != null ? TopHit(ScreenOf(x)) : null;
                bool inDlg = hit != null && hit.transform.IsChildOf(dlg.transform);
                log.Add($"  도크 X 자리를 누르면 맞는 것: {(hit != null ? hit.name : "없음")} ({(inDlg ? "확인창 덮개" : "확인창 밖")})");
                return x != null && inDlg;
            }, "확인창이 떠 있으면 도크 X(프로필 닫기)는 덮개에 가려 눌리지 않는다");
            Check(() => BackButtonHandler.Instance != null && BackButtonHandler.Instance.Top == pm.logoutCancelButton, "뒤로가기가 먼저 누를 버튼 = 확인창 취소");
            Shot("l31_logout");

            // 취소 — 확인창만 닫히고 프로필은 그대로 · 로그인도 그대로
            Do(() => pm.logoutCancelButton.onClick.Invoke());
            Wait(() => !DlgOpen(), 1.5f, "취소로 닫힘");
            Check(() => Active("FullProfilePanel") && LoginManager.Instance.CurrentUser != null && LoginManager.Instance.CurrentUser.id == SfMe, "취소: 확인창만 닫히고 프로필 · 로그인은 그대로");

            // 뒤로가기 — 같은 결과
            Do(() => pm.logoutButton.onClick.Invoke());
            Wait(DlgOpen, 2f, "로그아웃 확인창 다시 열림");
            Sleep(0.5f);
            Do(() => BackButtonHandler.Instance.HandleBackPress());
            Wait(() => !DlgOpen(), 1.5f, "뒤로가기로 닫힘");
            Sleep(0.4f);
            Check(() => { log.Add($"  뒤로가기 뒤 열린 창 {OpenPanels()}"); return Active("FullProfilePanel") && LoginManager.Instance.CurrentUser != null && LoginManager.Instance.CurrentUser.id == SfMe; },
                "뒤로가기: 확인창만 닫히고 프로필은 그대로");
            Shot("l31_after");

            Do(() => Click("FullProfilePanel/DockMirror0926/ProfileToggle0926"));
            Wait(() => !Active("FullProfilePanel"), 2f, "프로필 닫힘");
            Do(L31Finish);
        }
    }
}
