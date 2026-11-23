using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-17 K1 — 축제 칩을 켜면 목록 맨 위에는 가깝고 곧 하는 세 곳 중 무작위 한 곳만 (축제 색 바탕 · 흰 글자 — 애견동반의 보호소 줄처럼),
    /// 나머지 축제는 장소와 섞여 거리순으로 아래 (거리 슬라이더 최대 10km 밖도 맨 끝 쪽에).
    ///  · festband : 광고 줄 하나 · 축제 여섯(1 · 3 · 2 · 6 · 25 · 120km — 서버 순서 그대로)을 앱 안에 바로 넣고(서버는 닫힌 포트 — 운영 서버에 묻지 않음) 축제 칩을 켠다 →
    ///    맨 위 광고 → 그다음 고른 축제 하나(앞의 세 곳 중 · 축제 색 바탕 · 흰 글자) → 나머지 다섯은 바탕 없이 거리순 아래 (25 · 120km 도 장소들 뒤에) /
    ///    다시 그려도 같은 곳 · 목록을 다시 열거나 칩을 다시 켜면 다른 곳 / 줄 칸이 차도 먼 축제 줄은 그린다 / 누르면 묻는 창 없이 축제 이름 검색 /
    ///    칩을 끄면 축제 줄이 빠지고 그 줄을 다시 쓰는 줄은 원래 색
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanFestBand()
        {
            const string DockList = "Dock0926/List_Button";
            const string ListX = "ListPanel/DockMirror0926/XButton_List";
            string[] titles = { "시험 축제 하나", "시험 축제 둘", "시험 축제 셋", "시험 축제 넷", "시험 축제 다섯", "시험 축제 여섯" };
            double[] km = { 1.0, 3.0, 2.0, 6.0, 25.0, 120.0 };
            string[] icons = { "fireworks", "maple", "crab", "party", "fireworks", "maple" };
            var pool = new HashSet<string> { titles[0], titles[1], titles[2] };
            var picks = new List<string>();
            bool festWas = false;
            R0926PlaceRows pr = null;
            var maxRowsField = typeof(R0926PlaceRows).GetField("maxRows", BindingFlags.NonPublic | BindingFlags.Instance);
            var chipColorField = typeof(R0926FilterChip).GetField("color", BindingFlags.NonPublic | BindingFlags.Instance);
            // 줄은 목록 글이 바뀔 때만 다시 그린다 — 에디터는 자리가 그대로라 글이 같으니, 줄 칸을 바꾼 뒤엔 지난 글을 지워 다시 그리게
            var lastField = typeof(R0926PlaceRows).GetField("last", BindingFlags.NonPublic | BindingFlags.Instance);
            int maxRowsWas = -1;

            Func<int> topIdx = () =>
            {
                var p = Plm();
                if (p == null) return -1;
                for (int i = 0; i < p.ShownEntries.Count; i++) if (p.ShownEntries[i].top) return i;
                return -1;
            };
            Func<string> topName = () => { int i = topIdx(); return i >= 0 ? Plm().ShownEntries[i].name : null; };
            Func<Transform, Image> bandOf = row => row != null ? row.Find("Band")?.GetComponent<Image>() : null;
            Func<Transform, bool> bandOn = row => { var b = bandOf(row); return b != null && b.gameObject.activeSelf; };
            Func<Transform> topRow = () => { int i = topIdx(); var l = ListRows(); return i >= 0 && i < l.Count ? l[i] : null; };
            Func<int> fests = () => { var p = Plm(); return p == null ? 0 : p.ShownEntries.Count(e => e.kind == PlaceListManager.EntryKind.Festival && !e.promo); };
            Func<bool> ready = () => topIdx() >= 0 && bandOn(topRow()) && fests() == titles.Length;
            Func<PlaceListManager.ListEntry, bool> nearHunt = e => e.kind == PlaceListManager.EntryKind.Hunt && R0926HuntList.Nearby.Exists(x => x.id == e.huntId && !x.joined);
            Func<Color, Color, float, bool> same = (a, b, tol) => Mathf.Abs(a.r - b.r) < tol && Mathf.Abs(a.g - b.g) < tol && Mathf.Abs(a.b - b.b) < tol && Mathf.Abs(a.a - b.a) < tol;
            Func<Color, string> hex = c => "#" + ColorUtility.ToHtmlStringRGBA(c);
            Func<Color, Color, float> contrast = (fg, bg) =>
            {
                Func<float, float> lin = v => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
                Func<Color, float> lum = c => 0.2126f * lin(c.r) + 0.7152f * lin(c.g) + 0.0722f * lin(c.b);
                float a = lum(fg), b = lum(bg);
                return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
            };
            // 줄 색만 (거리는 적지 않는다)
            Func<string> rowsLog = () =>
            {
                var p = Plm();
                var l = ListRows();
                var s = new List<string>();
                for (int i = 0; i < l.Count && i < 4; i++)
                {
                    var name = l[i].Find("Name")?.GetComponent<Text>();
                    var dist = l[i].Find("Dist")?.GetComponent<Text>();
                    string kind = p != null && i < p.ShownEntries.Count ? p.ShownEntries[i].kind + (p.ShownEntries[i].promo ? "(광고)" : p.ShownEntries[i].top ? "(맨 위)" : "") : "?";
                    s.Add($"{i + 1}:{kind} 바탕 {(bandOn(l[i]) ? hex(bandOf(l[i]).color) : "-")} 이름 {(name != null ? hex(name.color) : "-")} 거리 {(dist != null ? hex(dist.color) : "-")}");
                }
                return string.Join(" | ", s);
            };
            // 목록을 닫았다 다시 연다
            Action reopen = () =>
            {
                Do(() => { if (Active("ListPanel")) Click(ListX + "/ClosePx0926"); });
                Sleep(0.8f);
                Do(() => Click(DockList));
                Sleep(1.2f);
                Do(() => Plm()?.UpdateUI());
                Wait(ready, 10f, "목록 다시 엶 → 맨 위 축제 하나");
                Sleep(0.4f);
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
            });
            Do(() => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()));
            Wait(() => DataManager.Instance != null && DataManager.Instance.GetSpawnedObjects().Count > 0, 40f, "주변 장소가 AR 에 뜸");
            Sleep(1.5f);
            Do(() =>
            {
                pr = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>(FindObjectsInactive.Include);
                festWas = ChipOn("FestivalToggle");
                // 축제는 운영 서버에 묻지 않는다 — 닫힌 포트로 돌리고(받기 실패 → 넣은 목록 그대로) 서버가 주는 순서(광고 → 가깝고 곧 하는 순)대로 앱 안에 바로 넣는다
                R0926Nearby.EditorBase = "http://127.0.0.1:9";
                R0926Nearby.EditorOpened = new List<string>();
                double la = VirtualLocation.Instance.Latitude, lo = VirtualLocation.Instance.Longitude;
                string today = DateTime.Now.ToString("yyyyMMdd"), later = DateTime.Now.AddDays(5).ToString("yyyyMMdd");
                R0926Nearby.Festivals.Clear();
                R0926Nearby.Festivals.Add(new R0926Nearby.Festival
                {
                    id = "ad:kt", title = "시험 광고 줄", lat = la + 0.06, lon = lo, start = today, end = later, icon = "party", promo = true, ad_id = "kt-ad", link = "",
                });
                for (int i = 0; i < titles.Length; i++)
                    R0926Nearby.Festivals.Add(new R0926Nearby.Festival
                    {
                        id = "kt_" + i, title = titles[i], lat = la + km[i] / 111.32, lon = lo, start = today, end = later, icon = icons[i], dist = (int)(km[i] * 1000),
                    });
                if (!festWas) ClickToggle("FestivalToggle");
            });
            Sleep(0.4f);
            Do(() => Click(DockList));
            Sleep(1.2f);
            Do(() => Plm()?.UpdateUI());
            Wait(ready, 10f, "축제 칩 → 맨 위 축제 하나 · 축제 여섯 모두");
            Sleep(0.6f);
            Do(() => { picks.Add(topName()); log.Add("  " + rowsLog() + " · 고른 축제: " + topName()); });
            Shot("festband_top");

            Check(() =>
            {
                var p = Plm();
                return p != null && p.ShownEntries.Count > 1 && p.ShownEntries[0].promo && !bandOn(ListRows()[0]) && topIdx() == 1 && p.ShownEntries.Count(e => e.top) == 1;
            }, "맨 위 광고 줄은 그대로 · 바로 아래 축제 한 곳만 바탕 (11-17 K1 「랜덤으로 하나만 상단에」)");
            Check(() => pool.Contains(topName() ?? ""), "맨 위 축제는 가깝고 곧 하는 세 곳(서버 순서 앞 셋) 중 하나 — 넷째(6km) · 먼 축제는 고르지 않음");
            Check(() =>
            {
                var b = bandOf(topRow());
                var chip = Chip("FestivalToggle")?.GetComponent<R0926FilterChip>();
                Color cc = chip != null && chipColorField != null ? (Color)chipColorField.GetValue(chip) : Color.clear;
                ColorUtility.TryParseHtmlString("#" + R0926Nearby.FestivalHex, out var fh);
                log.Add($"  줄 바탕 {(b != null ? hex(b.color) : "-")} · 축제 칩 색 {hex(cc)} · 축제 색 {hex(fh)}");
                return b != null && same(new Color(b.color.r, b.color.g, b.color.b, 1f), new Color(cc.r, cc.g, cc.b, 1f), 0.01f)
                       && same(new Color(fh.r, fh.g, fh.b, 1f), new Color(cc.r, cc.g, cc.b, 1f), 0.01f) && Mathf.Abs(b.color.a - 0.94f) < 0.01f;
            }, "맨 위 축제 줄 바탕 = 축제 칩 색 (FF6B57) · 보호소 줄처럼 꽉 차게 (0.94)");
            Check(() =>
            {
                var name = topRow()?.Find("Name")?.GetComponent<Text>();
                var dist = topRow()?.Find("Dist")?.GetComponent<Text>();
                var b = bandOf(topRow());
                if (name == null || dist == null || b == null) return false;
                var opaque = new Color(b.color.r, b.color.g, b.color.b, 1f);
                log.Add($"  이름 {hex(name.color)} (대비 {contrast(name.color, opaque):F1}) · 날짜·거리 {hex(dist.color)} · 글: {dist.text}");
                return same(name.color, Color.white, 0.01f) && dist.color.r > 0.99f && dist.color.g > 0.99f && dist.color.b > 0.99f && dist.color.a >= 0.8f && dist.color.a < 0.97f;
            }, "맨 위 축제 이름은 흰 글자 · 날짜·거리는 그보다 살짝 옅은 흰색");
            Check(() =>
            {
                var p = Plm();
                var l = ListRows();
                int t = topIdx();
                for (int i = 0; i < l.Count && i < p.ShownEntries.Count; i++)
                    if (i != t && bandOn(l[i])) { log.Add("  바탕이 또 있음 " + (i + 1)); return false; }
                return t >= 0;
            }, "다른 줄(광고 · 나머지 축제 · 장소)은 바탕 없음");
            Check(() =>
            {
                var p = Plm();
                var l = ListRows();
                // 바탕 없는 축제 줄의 거리 글자색 = 장소 줄의 거리 글자색 (템플릿)
                Color? placeDist = null, festDist = null;
                for (int i = 0; i < l.Count && i < p.ShownEntries.Count; i++)
                {
                    var e = p.ShownEntries[i];
                    var d = l[i].Find("Dist")?.GetComponent<Text>();
                    if (d == null) continue;
                    if (e.kind == PlaceListManager.EntryKind.Place && placeDist == null) placeDist = d.color;
                    if (e.kind == PlaceListManager.EntryKind.Festival && !e.top && !e.promo && festDist == null) festDist = d.color;
                }
                return placeDist.HasValue && festDist.HasValue && same(placeDist.Value, festDist.Value, 0.01f);
            }, "나머지 축제 줄은 바탕 없이 원래 글자색 (장소 줄과 같게)");

            // 나머지 다섯은 맨 위 묶음(광고 · 맨 위 축제 · 근처 보물찾기) 아래 장소와 섞여 거리순 — 25 · 120km 축제는 장소들 뒤 (슬라이더 최대 10km 밖도)
            Check(() =>
            {
                var p = Plm();
                var s = p.ShownEntries;
                int i = topIdx() + 1;
                while (i < s.Count && nearHunt(s[i])) i++;
                int start = i;
                float last = -1f;
                int lastPlace = -1;
                var at = new Dictionary<string, int>();
                for (; i < s.Count; i++)
                {
                    var e = s[i];
                    if (e.promo || e.top || nearHunt(e)) { log.Add("  맨 위 줄이 아래에 " + (i + 1)); return false; }
                    if (e.distance + 0.5f < last) { log.Add("  거리순이 아님 " + (i + 1)); return false; }
                    last = e.distance;
                    if (e.kind == PlaceListManager.EntryKind.Place) lastPlace = i;
                    if (e.kind == PlaceListManager.EntryKind.Festival) at[e.name] = i;
                }
                var rest = titles.Where(x => x != topName()).ToList();
                bool all = rest.All(at.ContainsKey);
                int far1 = all ? at[titles[4]] : -1, far2 = all ? at[titles[5]] : -1;
                log.Add($"  아래 묶음 {s.Count - start}줄 · 축제 {at.Count} (순서 {string.Join(",", at.OrderBy(x => x.Value).Select(x => Array.IndexOf(titles, x.Key) + 1))}) · 마지막 장소 줄 {lastPlace + 1} · 25km {far1 + 1} · 120km {far2 + 1}");
                return all && at.Count == titles.Length - 1 && lastPlace >= 0 && far1 > lastPlace && far2 > far1;
            }, "나머지 축제 다섯은 바탕 없이 장소와 섞여 거리순 — 10km 넘는 축제(25 · 120km)도 장소들 뒤 맨 끝 쪽 (11-17 「10km 외에 있어도 하단에」)");
            Check(() =>
            {
                var names = ListRows().Select(r => r.Find("Name")?.GetComponent<Text>()?.text ?? "").ToList();
                return names.Any(x => x.StartsWith(titles[4])) && names.Any(x => x.StartsWith(titles[5]));
            }, "먼 축제 줄도 목록에 그려짐");
            Check(() => SummaryText().EndsWith(" " + titles.Length), "집계 줄의 축제 수는 광고 줄을 빼고 여섯 (맨 위 하나 + 아래 다섯)");

            // 몇 초마다 다시 그려도 같은 곳 (매번 고르면 맨 윗줄이 깜빡인다)
            for (int k = 0; k < 3; k++) { Do(() => Plm()?.UpdateUI()); Sleep(0.3f); }
            Check(() => topName() == picks[0], "다시 그려도 맨 위 축제는 같은 곳");

            // 목록을 다시 열 때마다 앞과 다른 곳 (세 곳 중)
            reopen();
            Do(() => { picks.Add(topName()); log.Add("  다시 연 뒤: " + topName()); });
            Check(() => pool.Contains(picks[1] ?? "") && picks[1] != picks[0], "목록을 다시 열면 앞과 다른 축제 (세 곳 중)");
            reopen();
            Do(() => { picks.Add(topName()); log.Add("  또 연 뒤: " + topName()); });
            Check(() => pool.Contains(picks[2] ?? "") && picks[2] != picks[1], "또 열면 또 다른 축제");

            // 축제 칩을 껐다 켜도 새로 고른다
            Do(() => ClickToggle("FestivalToggle"));
            Sleep(0.4f);
            Do(() => ClickToggle("FestivalToggle"));
            Sleep(0.4f);
            Do(() => Plm()?.UpdateUI());
            Wait(ready, 10f, "축제 칩 다시 켬 → 맨 위 축제 하나");
            Do(() => { picks.Add(topName()); log.Add("  칩 다시 켠 뒤: " + topName()); });
            Check(() => pool.Contains(picks[3] ?? "") && picks[3] != picks[2], "축제 칩을 다시 켜면 앞과 다른 축제");

            // 줄 칸(maxRows)이 차도 축제 줄은 더 그린다 — 칸을 셋으로 줄여 본다
            Do(() =>
            {
                if (pr == null || maxRowsField == null) return;
                maxRowsWas = (int)maxRowsField.GetValue(pr);
                maxRowsField.SetValue(pr, 3);
                lastField?.SetValue(pr, null);
                Plm()?.UpdateUI();
            });
            Sleep(0.8f);
            Check(() =>
            {
                var p = Plm();
                var s = p.ShownEntries;
                var want = new List<string>();
                for (int i = 0; i < s.Count; i++) if (i < 3 || s[i].kind == PlaceListManager.EntryKind.Festival) want.Add(s[i].name);
                var got = ListRows().Select(r => r.Find("Name")?.GetComponent<Text>()?.text ?? "").ToList();
                log.Add($"  줄 칸 3 → 그린 줄 {got.Count} (바란 {want.Count}) · 칸 밖 축제 {want.Count - Mathf.Min(3, s.Count)}");
                if (maxRowsField == null || got.Count != want.Count || want.Count <= 3) return false;
                for (int j = 3; j < want.Count; j++) if (!got[j].StartsWith(want[j])) { log.Add("  칸 밖 줄 다름 " + (j + 1)); return false; }
                return want.Contains(titles[5]);
            }, "줄 칸이 차도 그 뒤 축제 줄은 그린다 (장소 줄은 칸까지만) — 장소가 많은 곳에서 먼 축제가 잘리지 않게");
            Check(() =>
            {
                // 칸 밖 축제 줄도 제 줄 정보 (날짜 · 거리 글)
                var p = Plm();
                var l = ListRows();
                var row = l.Find(r => (r.Find("Name")?.GetComponent<Text>()?.text ?? "").StartsWith(titles[5]));
                var f = p.ShownEntries.FirstOrDefault(x => x.name == titles[5]).data as R0926Nearby.Festival;
                var d = row?.Find("Dist")?.GetComponent<Text>()?.text ?? "";
                return row != null && f != null && d.Contains(R0926Nearby.When(f.start, f.end)) && d.Contains(" · ") && d.EndsWith("km");
            }, "칸 밖 축제 줄도 '진행 중 · 거리' 그대로");
            Do(() =>
            {
                if (pr != null && maxRowsField != null && maxRowsWas > 0) { maxRowsField.SetValue(pr, maxRowsWas); maxRowsWas = -1; }
                if (pr != null) lastField?.SetValue(pr, null);
                Plm()?.UpdateUI();
            });
            Sleep(0.6f);

            // 누르면 묻는 창 없이 축제 이름 검색 (축제는 길안내 줄이 아니다 — D4 묻는 창과 겹치지 않음)
            Do(() => { R0926Nearby.EditorOpened.Clear(); topRow()?.GetComponent<Button>().onClick.Invoke(); });
            Sleep(0.5f);
            Check(() => !Active("GuideAsk0926") && R0926Nearby.EditorOpened.Count == 1 && R0926Nearby.EditorOpened[0] == R0926Nearby.FestivalLink(picks[3]),
                "맨 위 축제 줄을 누르면 묻는 창 없이 축제 이름으로 찾은 쪽");

            // 축제 칩을 끄면 축제 줄이 빠지고, 그 줄을 다시 쓰는 줄은 원래 색
            Do(() => { if (!Active("ListPanel")) Click(DockList); });
            Sleep(0.8f);
            Do(() => ClickToggle("FestivalToggle"));
            Sleep(0.4f);
            Do(() => Plm()?.UpdateUI());
            Wait(() => { var p = Plm(); return p != null && !p.ShownEntries.Any(e => e.kind == PlaceListManager.EntryKind.Festival) && ListRows().Count > 0; }, 6f, "축제 칩 끔 → 축제 줄 없음");
            Sleep(0.6f);
            Do(() => log.Add("  끈 뒤: " + rowsLog()));
            Check(() =>
            {
                var l = ListRows();
                Color tpl = new Color(0, 0, 0, 0);
                foreach (var r in l)
                {
                    if (bandOn(r)) return false;
                    var d = r.Find("Dist")?.GetComponent<Text>();
                    if (d == null) return false;
                    if (tpl.a == 0f) tpl = d.color;
                    else if (!same(d.color, tpl, 0.01f)) return false;
                }
                return l.Count > 0;
            }, "축제 칩을 끄면 맨 윗줄도 바탕 없이 원래 거리 색 (줄을 다시 써도 남지 않음)");

            // 되돌리기 — 축제 칩 · 줄 칸 · 넣은 축제 · 서버 주소 · 목록
            Do(() =>
            {
                if (pr != null && maxRowsField != null && maxRowsWas > 0) { maxRowsField.SetValue(pr, maxRowsWas); lastField?.SetValue(pr, null); }
                if (ChipOn("FestivalToggle") != festWas) ClickToggle("FestivalToggle");
                R0926Nearby.EditorForget();
                R0926Nearby.EditorBase = null;
                R0926Nearby.EditorOpened = null;
                if (Active("ListPanel")) Click(ListX + "/ClosePx0926");
            });
            Sleep(0.8f);
            Check(() => ChipOn("FestivalToggle") == festWas && (pr == null || maxRowsField == null || (int)maxRowsField.GetValue(pr) == 150), "되돌림: 축제 칩 처음 상태 · 줄 칸 150");
        }
    }
}
