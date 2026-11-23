using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 L2 — 대표님 「오브젝트 X 로 숨겼을 때 '이 장소를 숨겼어요' — 지금 하단에 뜸 → 기본 알림 팝업과 같은 로직 · 같은 위치(화면 가운데)」.
    ///  · hidecenter : 서버에 없는 번호 하나를 이 기기에서 숨겨(HiddenPlaces — 앱을 껐다 켜면 사라지는 기억) 알림만 띄운다 →
    ///    자리 · 높이 · 최소 폭 · 알약 색 · 모서리 · 글자 크기 · 밝은 테두리가 기본 알림 팝업(WarningText — '업로드 성공!')과 같은지 /
    ///    실제 화면에서도 가운데(위치 칩 · 도크와 떨어져) / '되돌리기'를 누르면 다시 보임 · 알림이 걷힘 / 그냥 두면 4초 뒤 걷힘 / 목록을 열면 바로 걷힘.
    ///    끝나면 숨긴 번호를 되살린다
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanHideCenter()
        {
            const string Fake = "dm_990399";   // 서버에 없는 번호 — 실제 장소는 숨기지 않는다
            string Msg() => R0926LocalizedText.Say("이 장소를 숨겼어요", "Place hidden", "この場所を非表示にしました", "已隐藏此地点", "Lugar oculto");
            string UndoWord() => R0926LocalizedText.Say("되돌리기", "Undo", "元に戻す", "撤销", "Deshacer");
            RectTransform Bar() => Find("Undo0926") as RectTransform;
            RectTransform Warn() => Find("WarningText") as RectTransform;
            CanvasGroup Cg() => Bar() != null ? Bar().GetComponent<CanvasGroup>() : null;
            bool Shown() { var cg = Cg(); return Bar() != null && Bar().gameObject.activeInHierarchy && cg != null && cg.alpha > 0.95f; }
            bool Gone() { var b = Bar(); return b == null || !b.gameObject.activeInHierarchy; }
            var wc = new Vector3[4];
            Rect World(RectTransform rt) { rt.GetWorldCorners(wc); return Rect.MinMaxRect(wc[0].x, wc[0].y, wc[2].x, wc[2].y); }
            bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
            var centre = new Vector2(0.5f, 0.5f);

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                HiddenPlaces.Unhide(Fake);
                Backdrop("bg_city.png");
            });
            Sleep(1.2f);

            Check(() =>
            {
                var b = Bar(); var w = Warn();
                if (b == null || w == null) { log.Add("  숨김 알림 " + (b != null) + " · 기본 팝업 " + (w != null)); return false; }
                var le = b.GetComponent<LayoutElement>();
                log.Add($"  숨김 알림 자리 {b.anchoredPosition} · 높이 {b.sizeDelta.y:0} · 최소 폭 {(le != null ? le.minWidth : -1f):0} — 기본 팝업 {w.anchoredPosition} · {w.sizeDelta.x:0}×{w.sizeDelta.y:0}");
                return b.anchorMin == centre && b.anchorMax == centre && b.pivot == centre && w.anchorMin == centre && w.anchorMax == centre && w.pivot == centre
                    && Vector2.Distance(b.anchoredPosition, w.anchoredPosition) < 0.5f && Mathf.Abs(b.sizeDelta.y - w.sizeDelta.y) < 0.5f
                    && le != null && Mathf.Abs(le.minWidth - w.sizeDelta.x) < 0.5f && b.GetComponent<R0926SafeInset>() == null;
            }, "숨김 알림 자리 = 기본 알림 팝업(WarningText) — 화면 가운데 고정 · 같은 자리 · 같은 높이 · 같은 최소 폭 (예전엔 아래 위치 칩 위 · 홈 막대 여백)");
            Check(() =>
            {
                var b = Bar(); var w = Warn();
                var bi = b != null ? b.GetComponent<Image>() : null; var wi = w != null ? w.GetComponent<Image>() : null;
                var bt = b != null ? b.Find("Text0926")?.GetComponent<Text>() : null; var wt = w != null ? w.GetComponentInChildren<Text>(true) : null;
                var rim = b != null ? b.Find("Rim0926") : null; var ri = rim != null ? rim.GetComponent<Image>() : null;
                var wr = w != null ? w.Find("Rim0926")?.GetComponent<Image>() : null;
                var gl = rim != null ? rim.Find("Glint") : null;
                if (bi == null || wi == null || bt == null || wt == null) return false;
                log.Add($"  알약 {bi.sprite?.name} · 색 {ColorUtility.ToHtmlStringRGBA(bi.color)} · 모서리 {bi.pixelsPerUnitMultiplier:0.000} · 글자 {bt.fontSize}"
                      + $" — 기본 팝업 {wi.sprite?.name} · {ColorUtility.ToHtmlStringRGBA(wi.color)} · {wi.pixelsPerUnitMultiplier:0.000} · {wt.fontSize} · 테두리 {(ri != null)}/{(wr != null)}");
                return bi.sprite == wi.sprite && bi.type == wi.type && Near(bi.color, wi.color) && Mathf.Abs(bi.pixelsPerUnitMultiplier - wi.pixelsPerUnitMultiplier) < 0.001f
                    && bt.fontSize == wt.fontSize && ri != null && wr != null && ri.sprite == wr.sprite && Mathf.Abs(ri.color.a - wr.color.a) < 0.01f
                    && Mathf.Abs(ri.pixelsPerUnitMultiplier - bi.pixelsPerUnitMultiplier) < 0.001f && rim.GetSiblingIndex() == b.childCount - 1
                    && gl != null && gl.GetComponent<R0926Glint>() != null;
            }, "모양도 같다 — 알약 그림 · 색 · 모서리 · 글자 크기 · 밝은 테두리와 훑는 빛");

            // 숨기면 — 가운데에 알림
            Do(() => HiddenPlaces.Hide(Fake));
            Sleep(0.4f);
            Check(() =>
            {
                var b = Bar();
                var t = b != null ? b.Find("Text0926")?.GetComponent<Text>() : null;
                var l = b != null ? b.Find("UndoButton0926/Label")?.GetComponent<Text>() : null;
                return Shown() && t != null && t.text == Msg() && l != null && l.text == UndoWord();
            }, "숨기면 '이 장소를 숨겼어요 · 되돌리기'가 뜬다");
            Check(() =>
            {
                var c = CanvasRoot() as RectTransform;
                var chip = Find("LocationManagerPanel") as RectTransform;
                var dock = Find("Dock0926") as RectTransform;
                if (c == null || Bar() == null || Warn() == null) return false;
                Rect cr = World(c), r = World(Bar()), w = World(Warn());
                float nx = (r.center.x - cr.xMin) / cr.width, ny = (r.center.y - cr.yMin) / cr.height;
                float wx = (w.center.x - cr.xMin) / cr.width, wy = (w.center.y - cr.yMin) / cr.height;
                bool clearOfChip = chip == null || !chip.gameObject.activeInHierarchy || World(chip).yMax < r.yMin;
                bool clearOfDock = dock == null || World(dock).yMax < r.yMin;
                log.Add($"  화면에서 가운데 {nx:0.000}, {ny:0.000} (0 아래 · 1 위) · 기본 팝업 {wx:0.000}, {wy:0.000} · 폭 {r.width / cr.width:0.000} (기본 팝업 {w.width / cr.width:0.000}) · 위치 칩 · 도크와 떨어짐 {clearOfChip}/{clearOfDock}");
                return Mathf.Abs(nx - 0.5f) < 0.01f && ny > 0.45f && ny < 0.65f && Mathf.Abs(nx - wx) < 0.005f && Mathf.Abs(ny - wy) < 0.005f
                    && r.width >= w.width - 1f && Mathf.Abs(r.height - w.height) < 1f && clearOfChip && clearOfDock;
            }, "실제 화면에서도 기본 알림 팝업과 같은 가운데 자리 · 같은 크기 (위치 칩 · 도크 위가 아님)");
            Shot("hidecenter");

            // 되돌리기
            Do(() => Click("Undo0926/UndoButton0926"));
            Sleep(0.45f);
            Check(() => !HiddenPlaces.IsHidden(Fake) && Gone(), "'되돌리기'를 누르면 다시 보이고 알림은 걷힌다");

            // 그냥 두면 4초 뒤 걷힌다 (숨김은 그대로)
            Do(() => HiddenPlaces.Hide(Fake));
            Sleep(0.4f);
            Check(() => Shown(), "다시 숨기면 다시 뜬다");
            Sleep(4.2f);
            Check(() => Gone() && HiddenPlaces.IsHidden(Fake), "그냥 두면 4초 뒤 걷힌다 (숨긴 것은 그대로)");
            Do(() => HiddenPlaces.Unhide(Fake));

            // 창이 열리면 바로 걷는다 — 창 위에 남아 누름을 막지 않게
            Do(() => HiddenPlaces.Hide(Fake));
            Sleep(0.4f);
            Do(() => Click("Dock0926/List_Button"));
            Sleep(0.6f);
            Check(() => Active("ListPanel") && (Gone() || (Cg() != null && Cg().alpha < 0.01f && !Cg().blocksRaycasts)), "목록을 열면 알림은 바로 걷힌다");
            Do(() => BackNow());
            Sleep(0.8f);
            Do(() => HiddenPlaces.Unhide(Fake));

            // 견줘 보기 — 기본 알림 팝업 '업로드 성공!' 이 같은 자리에
            Do(() =>
            {
                var cu = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                if (cu != null) cu.ShowWarning(LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText("upload_success") : "업로드 성공!");
            });
            Sleep(0.4f);
            Shot("hidecenter_warn");
            Do(() =>
            {
                var w = Warn();
                if (w != null) w.gameObject.SetActive(false);
                HiddenPlaces.Unhide(Fake);
            });
            Sleep(0.3f);
            Check(() => !HiddenPlaces.IsHidden(Fake) && Gone() && !Active("ListPanel") && (Warn() == null || !Warn().gameObject.activeSelf), "끝 — 숨긴 번호 되살림 · 알림 · 목록 · 기본 팝업 모두 닫힘");
        }
    }
}
