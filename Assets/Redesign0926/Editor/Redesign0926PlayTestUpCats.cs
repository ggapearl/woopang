using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-19 L4 — 대표님 「업로드 분류 '기타' 삭제 · '숙소' 추가 — 숙소 = 지금 공원 자리 · 공원 = 지금 화장실 자리 · 화장실 = 지금 기타 자리」.
    ///  · upcats : 장소 추가 · 3D 추가 · 정보 수정(L5)의 분류 칩 — 두 줄 '샵 음식점 카페 숙소 / 공원 스포츠 랜드마크 화장실' · '기타' 없음 · 칩 그림 ·
    ///    칩 값 = 관리자 분류 순서 · 칩을 누르면 그 분류(숙소 → 'stay' · 이름 · 숙소 색 1FC7D4) · 같은 칩을 다시 누르면 고르기 해제 ·
    ///    예전에 '기타'로 올라간 장소는 그대로 흰색.
    ///    올리기 단추는 누르지 않는다(서버에 보내는 것 없음). 끝나면 고른 분류를 처음대로 · 추가 창 닫기
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanUpCats()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance, NS = BindingFlags.NonPublic | BindingFlags.Static;
            const string Sheet = "UploadPage/Clip0926/UploadSheet0926/";
            string[] want = { "", "shop", "food", "cafe", "stay", "park", "sport", "landmark", "toilet" };
            string[][] words =
            {
                new[] { "샵", "Shop", "ショップ", "商店", "Tienda" },
                new[] { "음식점", "Food", "飲食店", "餐厅", "Comida" },
                new[] { "카페", "Cafe", "カフェ", "咖啡", "Café" },
                new[] { "숙소", "Stays", "宿泊", "住宿", "Alojamiento" },
                new[] { "공원", "Park", "公園", "公园", "Parque" },
                new[] { "스포츠", "Sports", "スポーツ", "运动", "Deporte" },
                new[] { "랜드마크", "Landmark", "名所", "地标", "Lugar" },
                new[] { "화장실", "Restroom", "トイレ", "卫生间", "Baño" },
            };
            var pages = new (string tag, string path, Type type, int tab)[]
            {
                ("장소 추가", Sheet + "CubeUploadPage/Panel/", typeof(CubeUploadManager), 0),
                ("3D 추가", Sheet + "ModelUploadPage/Panel/", typeof(ModelUploadManager), 1),
                ("정보 수정", "Fixpage/FixUploadPage/Panel/", typeof(CubeDataFixManager), -1),   // 11-19 L5 — 모양만 (여는 데 내 장소 · 로그인이 든다 · 누름은 placeedit 시험)
            };
            var mgr = new Dictionary<string, MonoBehaviour>();
            var idxWas = new Dictionary<string, int>();
            SwipePanelController swipe = null;

            int Idx(MonoBehaviour m) => m != null ? (int)m.GetType().GetField("currentCategoryIndex", NP).GetValue(m) : -1;
            string Sel(MonoBehaviour m) => m != null ? m.GetType().GetField("selectedCategory", NP).GetValue(m) as string : null;
            Text LabelOf(MonoBehaviour m) => m != null ? m.GetType().GetField("categoryToggleLabel", NP).GetValue(m) as Text : null;
            string[] Values(Type t) => t.GetField("categoryValues", NS)?.GetValue(null) as string[];
            Transform Chips(string path) => Find(path + "Chips0926");
            string[] ChipValues(string path)
            {
                var c = Chips(path)?.GetComponent<R0926CategoryChips>();
                return c != null ? typeof(R0926CategoryChips).GetField("values", NP).GetValue(c) as string[] : null;
            }
            bool Word(string got, string[] w) => got == w[0] || got == R0926LocalizedText.Say(w[0], w[1], w[2], w[3], w[4]);
            string Say(string key) => LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText(key) : null;
            string Join(IEnumerable<string> s) => s == null ? "(없음)" : string.Join(",", s.Select(x => x == "" ? "-" : x));

            // 칩 이름을 자리대로 — 한 줄에 네 개
            string Layout(string path)
            {
                var c = Chips(path);
                if (c == null) return "칩 없음";
                var rows = new List<string>();
                var cur = new List<string>();
                foreach (Transform ch in c)
                {
                    cur.Add(ch.Find("Label0926")?.GetComponent<Text>()?.text ?? ch.name);
                    if (cur.Count == 4) { rows.Add(string.Join(" ", cur)); cur.Clear(); }
                }
                if (cur.Count > 0) rows.Add(string.Join(" ", cur));
                return string.Join(" / ", rows);
            }

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Sleep(1.0f);

            // 1) 만든 모양 — 화면을 열기 전에도 (장면에 저장된 칩)
            Check(() =>
            {
                bool ok = true;
                foreach (var p in pages)
                {
                    var mv = Values(p.type);
                    var cv = ChipValues(p.path);
                    bool same = mv != null && cv != null && mv.SequenceEqual(want) && cv.SequenceEqual(want);
                    if (!same) log.Add($"  {p.tag}: 관리자 {Join(mv)} · 칩 {Join(cv)}");
                    ok &= same;
                }
                return ok;
            }, "분류 값 순서 — 관리자 = 칩 = (없음) 샵 음식점 카페 숙소 공원 스포츠 랜드마크 화장실 · '기타' 없음 (장소 추가 · 3D 추가 · 정보 수정)");
            Check(() =>
            {
                var bad = new List<string>();
                foreach (var p in pages)
                {
                    var c = Chips(p.path);
                    if (c == null) { bad.Add(p.tag + " 칩 없음"); continue; }
                    var names = c.Cast<Transform>().Select(t => t.name).ToArray();
                    var wantNames = want.Skip(1).Select(v => "Chip_" + v).ToArray();
                    if (!names.SequenceEqual(wantNames)) bad.Add(p.tag + " 순서 " + string.Join(",", names));
                    if (c.Find("Chip_etc") != null) bad.Add(p.tag + " 기타 남음");
                    var grid = c.GetComponent<GridLayoutGroup>();
                    if (grid == null || grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount || grid.constraintCount != 4) bad.Add(p.tag + " 네 칸 줄 아님");
                    for (int i = 0; i < wantNames.Length && i < c.childCount; i++)
                    {
                        var ch = c.GetChild(i);
                        var ic = ch.Find("Icon")?.GetComponent<Image>();
                        if (ic == null || ic.sprite == null || ic.sprite.name != "r0926_c_" + want[i + 1]) bad.Add(p.tag + " " + want[i + 1] + " 그림 " + (ic != null && ic.sprite != null ? ic.sprite.name : "-"));
                        var lb = ch.Find("Label0926")?.GetComponent<Text>();
                        if (lb == null || !Word(lb.text, words[i])) bad.Add(p.tag + " " + want[i + 1] + " 이름 " + (lb != null ? lb.text : "-"));
                    }
                    log.Add($"  {p.tag} 칩: {Layout(p.path)}");
                }
                if (bad.Count > 0) log.Add("  " + string.Join(" · ", bad));
                return bad.Count == 0;
            }, "칩 자리 — 윗줄 샵 음식점 카페 숙소 · 아랫줄 공원 스포츠 랜드마크 화장실 (숙소 = 예전 공원 자리 · 공원 = 화장실 자리 · 화장실 = 기타 자리) · 칩 그림 · 이름");
            Check(() => ColorUtility.ToHtmlStringRGB(DataManager.GetCategoryColor("stay")) == "1FC7D4" && DataManager.IsChipCategory("stay")
                        && DataManager.ResolvePlaceColorHex(null, "etc") == "FFFFFF" && DataManager.ResolvePlaceColorHex("FBC15D", "etc") == "FFFFFF"
                        && Say("category_stay") == R0926LocalizedText.Say("숙소", "Stays", "宿泊", "住宿", "Alojamiento"),
                  "숙소 = 칩 분류(청록 1FC7D4 · 이름 '숙소') · 예전에 '기타'로 올라간 장소는 그대로 흰색");

            // 2) 화면을 열고 — 칩을 누르면 그 분류
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() =>
            {
                swipe = UnityEngine.Object.FindAnyObjectByType<SwipePanelController>(FindObjectsInactive.Include);
                foreach (var p in pages.Where(p => p.tab >= 0))
                {
                    mgr[p.tag] = UnityEngine.Object.FindAnyObjectByType(p.type, FindObjectsInactive.Include) as MonoBehaviour;
                    idxWas[p.tag] = Idx(mgr[p.tag]);
                }
                log.Add("  처음 고른 분류 — " + string.Join(" · ", pages.Where(p => p.tab >= 0).Select(p => p.tag + " " + (idxWas[p.tag] >= 0 && idxWas[p.tag] < want.Length ? (want[idxWas[p.tag]] == "" ? "없음" : want[idxWas[p.tag]]) : "?"))));
            });
            Check(() => Active("UploadPage") && mgr.Values.All(m => m != null), "준비: 추가 창 열림 · 장소 · 3D 관리자");

            foreach (var p in pages.Where(p => p.tab >= 0))
            {
                var pg = p;
                Do(() => { if (swipe != null) swipe.SwitchToPanel(pg.tab); });
                Sleep(1.2f);
                Check(() =>
                {
                    Canvas.ForceUpdateCanvases();
                    var c = Chips(pg.path);
                    if (c == null || !c.gameObject.activeInHierarchy || c.childCount != 8) return false;
                    var pos = c.Cast<Transform>().Select(t => ((RectTransform)t).anchoredPosition).ToArray();
                    bool ok = true;
                    for (int k = 0; k < 8; k++)
                    {
                        int row = k / 4, col = k % 4;
                        if (Mathf.Abs(pos[k].y - pos[row * 4].y) > 1f) ok = false;            // 같은 줄은 같은 높이
                        if (col > 0 && pos[k].x <= pos[k - 1].x + 1f) ok = false;             // 왼쪽에서 오른쪽
                        if (row == 1 && Mathf.Abs(pos[k].x - pos[col].x) > 1f) ok = false;    // 위아래 칸이 같은 열
                    }
                    if (pos[4].y >= pos[0].y - 1f) ok = false;                                 // 둘째 줄이 아래
                    if (!ok) log.Add($"  {pg.tag} 칩 자리 " + string.Join(" ", pos.Select(v => $"({v.x:0},{v.y:0})")));
                    return ok;
                }, pg.tag + ": 화면에서도 네 칸 두 줄 (윗줄 → 아랫줄 · 왼쪽 → 오른쪽 · 위아래 같은 열)");
                Check(() =>
                {
                    var m = mgr[pg.tag];
                    var bad = new List<string>();
                    for (int i = 1; i < want.Length; i++)
                    {
                        Click(pg.path + "Chips0926/Chip_" + want[i]);
                        string lb = LabelOf(m)?.text;
                        if (Sel(m) != want[i] || Idx(m) != i || lb != Say("category_" + want[i]))
                            bad.Add($"{want[i]} → {Sel(m) ?? "null"}({Idx(m)}) '{lb}'");
                    }
                    if (bad.Count > 0) log.Add($"  {pg.tag}: " + string.Join(" · ", bad));
                    return bad.Count == 0;
                }, pg.tag + ": 칩마다 누르면 그 분류 · 분류 이름 (샵 → … → 숙소 → 공원 → 스포츠 → 랜드마크 → 화장실)");
                Check(() =>
                {
                    var m = mgr[pg.tag];
                    Click(pg.path + "Chips0926/Chip_stay");
                    var lb = LabelOf(m);
                    bool ok = Sel(m) == "stay" && Idx(m) == 4 && lb != null && ColorUtility.ToHtmlStringRGB(lb.color) == "1FC7D4";
                    if (!ok) log.Add($"  {pg.tag} 숙소: {Sel(m)}({Idx(m)}) 이름 색 {(lb != null ? ColorUtility.ToHtmlStringRGB(lb.color) : "-")}");
                    return ok;
                }, pg.tag + ": 숙소 칩 → 'stay' (올리면 분류 stay · 색 1FC7D4) · 이름도 숙소 색");
                Check(() =>
                {
                    var m = mgr[pg.tag];
                    Click(pg.path + "Chips0926/Chip_stay");   // 고른 칩을 다시 → 해제
                    return Sel(m) == "" && Idx(m) == 0 && LabelOf(m)?.text == Say("category_select");
                }, pg.tag + ": 고른 칩을 다시 누르면 분류 해제 (분류 없이 올리기 — 흰색)");
                Do(() =>
                {
                    var m = mgr[pg.tag];
                    int w = idxWas.TryGetValue(pg.tag, out var v) ? v : 0;
                    if (w > 0 && w < want.Length && Idx(m) != w) Click(pg.path + "Chips0926/Chip_" + want[w]);
                });
            }

            // 되돌리기 — 처음 탭 · 고른 분류 · 추가 창 닫기
            Do(() => { if (swipe != null) swipe.SwitchToPanel(0); });
            Sleep(0.6f);
            Check(() => pages.Where(p => p.tab >= 0).All(p => Idx(mgr[p.tag]) == (idxWas.TryGetValue(p.tag, out var v) ? v : 0)), "고른 분류 처음대로");
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(0.8f);
            Check(() => !Active("UploadPage"), "추가 창 닫힘");
        }
    }
}
