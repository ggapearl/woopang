using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-17 D5 — 애견동반을 켜면 목록 맨 위에 뜨는 보호소 줄: 애견동반 칩(켜짐)처럼 진한 노랑 바탕 · 검정 글자, 오른쪽 방향 · 거리는 보일 만큼 회색.
    ///  · shelterband : 보호소 세 곳을 앱 안에 바로 넣고(서버는 닫힌 포트 — 운영 서버에 묻지 않음) 애견동반을 켠다 →
    ///    맨 위 보호소 줄 바탕 = 켜진 칩 바탕색 · 이름 = 칩 글자색 · 방향/거리 회색(노랑과 대비 4.5 이상) · 다른 줄은 그대로 /
    ///    누르면 묻는 창 없이 카카오맵 쪽 / 애견동반을 끄면 그 줄을 다시 쓰는 줄이 원래 색으로
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanShelterBand()
        {
            const string DockList = "Dock0926/List_Button";
            const string ListX = "ListPanel/DockMirror0926/XButton_List";
            var petWas = FilterManager.PetFriendlyFilterState.All;
            FilterManager fm = null;
            Func<RectTransform> topRow = () => { var l = ListRows(); return l.Count > 0 ? (RectTransform)l[0] : null; };
            Func<Transform, Image> bandOf = row => row != null ? row.Find("Band")?.GetComponent<Image>() : null;
            Func<Transform, bool> bandOn = row => { var b = bandOf(row); return b != null && b.gameObject.activeSelf; };
            Func<bool> shelterTop = () =>
            {
                var p = Plm();
                return p != null && p.ShownEntries.Count > 0 && p.ShownEntries[0].kind == PlaceListManager.EntryKind.Shelter && ListRows().Count > 0;
            };
            Func<Color, Color, float, bool> same = (a, b, tol) => Mathf.Abs(a.r - b.r) < tol && Mathf.Abs(a.g - b.g) < tol && Mathf.Abs(a.b - b.b) < tol && Mathf.Abs(a.a - b.a) < tol;
            Func<Color, string> hex = c => "#" + ColorUtility.ToHtmlStringRGBA(c);
            // 두 색의 명암 대비 (WCAG) — 바탕(불투명도 a)은 어두운 목록 칸 위에 얹힌 색으로
            Func<Color, Color, float> contrast = (fg, bg) =>
            {
                Func<float, float> lin = v => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
                Func<Color, float> lum = c => 0.2126f * lin(c.r) + 0.7152f * lin(c.g) + 0.0722f * lin(c.b);
                float a = lum(fg), b = lum(bg);
                return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
            };
            Func<string> rowsLog = () =>
            {
                var p = Plm();
                var l = ListRows();
                var s = new List<string>();
                for (int i = 0; i < l.Count && i < 4; i++)
                {
                    var name = l[i].Find("Name")?.GetComponent<Text>();
                    var dist = l[i].Find("Dist")?.GetComponent<Text>();
                    string kind = p != null && i < p.ShownEntries.Count ? p.ShownEntries[i].kind.ToString() : "?";
                    s.Add($"{i + 1}:{kind} 바탕 {(bandOn(l[i]) ? hex(bandOf(l[i]).color) : "-")} 이름 {(name != null ? hex(name.color) : "-")} 거리 {(dist != null ? hex(dist.color) : "-")}");
                }
                return string.Join(" | ", s);
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
                fm = UnityEngine.Object.FindAnyObjectByType<FilterManager>();
                petWas = fm != null ? fm.PetState : FilterManager.PetFriendlyFilterState.All;
                // 보호소는 운영 서버에 묻지 않는다 — 닫힌 포트로 돌리고(받기 실패 → 넣은 목록 그대로) 가까운 세 곳을 앱 안에 바로 넣는다
                R0926Nearby.EditorBase = "http://127.0.0.1:9";
                R0926Nearby.EditorOpened = new List<string>();
                double la = VirtualLocation.Instance.Latitude, lo = VirtualLocation.Instance.Longitude;
                R0926Nearby.Shelters.Clear();
                for (int i = 0; i < 3; i++)
                    R0926Nearby.Shelters.Add(new R0926Nearby.Shelter
                    {
                        id = "sb_" + i, name = "시험 유기동물 보호소 " + (i + 1), addr = "", tel = "",
                        url = "https://place.map.kakao.com/sb_" + i, lat = la + 0.004 * (i + 1), lon = lo + 0.003 * (i + 1), dist = 500 * (i + 1),
                    });
                if (fm != null && fm.PetState != FilterManager.PetFriendlyFilterState.OnlyPetFriendly) ClickToggle("PetFriendlyToggle");
            });
            Sleep(0.4f);
            Do(() => Click(DockList));
            Sleep(1.2f);
            Do(() => Plm()?.UpdateUI());
            Wait(() => shelterTop() && bandOn(topRow()), 10f, "애견동반 → 맨 위 보호소 줄");
            Sleep(0.6f);
            Do(() => log.Add("  " + rowsLog()));
            Shot("shelterband_top");

            Check(() => shelterTop() && bandOn(topRow()), "애견동반을 켜면 맨 위 보호소 줄에 바탕 (11-16 D5)");
            Check(() =>
            {
                var chip = Chip("PetFriendlyToggle");
                var cb = chip != null ? chip.GetComponent<Image>() : null;
                var b = bandOf(topRow());
                log.Add($"  칩 바탕 {(cb != null ? hex(cb.color) : "-")} · 줄 바탕 {(b != null ? hex(b.color) : "-")}");
                return cb != null && b != null && same(cb.color, b.color, 0.01f);
            }, "보호소 줄 바탕 = 켜진 애견동반 칩 바탕 (진한 노랑 · 같은 불투명도)");
            Check(() =>
            {
                var chipLabel = Chip("PetFriendlyToggle")?.Find("Label")?.GetComponent<Text>();
                var name = topRow()?.Find("Name")?.GetComponent<Text>();
                log.Add($"  칩 글자 {(chipLabel != null ? hex(chipLabel.color) : "-")} · 줄 이름 {(name != null ? hex(name.color) : "-")}");
                return chipLabel != null && name != null && same(chipLabel.color, name.color, 0.01f) && name.color.r < 0.2f;
            }, "보호소 이름 = 칩 글자색 (검정)");
            Check(() =>
            {
                var dist = topRow()?.Find("Dist")?.GetComponent<Text>();
                var b = bandOf(topRow());
                if (dist == null || b == null) return false;
                float k = contrast(dist.color, b.color), kName = contrast(R0926FilterChip.InkOnWhite, b.color);
                bool gray = Mathf.Abs(dist.color.r - dist.color.g) < 0.02f && Mathf.Abs(dist.color.g - dist.color.b) < 0.02f;
                log.Add($"  방향·거리 {hex(dist.color)} · 노랑과 대비 {k:F1} (이름 {kName:F1}) · 글: {dist.text}");
                return gray && k >= 4.5f && k < kName && dist.color.r > 0.2f;
            }, "방향 · 거리는 회색 — 노랑 위에서 보이되(대비 4.5 이상) 이름보다 옅게");
            Check(() =>
            {
                var l = ListRows();
                var tplDist = l.Count > 1 ? l[1].Find("Dist")?.GetComponent<Text>() : null;
                for (int i = 1; i < l.Count; i++)
                {
                    if (bandOn(l[i])) return false;
                    var d = l[i].Find("Dist")?.GetComponent<Text>();
                    if (d == null || tplDist == null || !same(d.color, tplDist.color, 0.01f)) return false;
                }
                return l.Count > 1 && !same(tplDist.color, new Color32(0x55, 0x55, 0x55, 0xFF), 0.02f);
            }, "다른 줄은 바탕 없이 원래 색 그대로");
            Check(() => { var b = bandOf(topRow()); return b != null && b.transform.GetSiblingIndex() == 0 && b.sprite != null && !b.raycastTarget; }, "바탕은 줄 맨 뒤 (분홍 누름 바탕 · 글자 · 사진 아래) · 누름은 줄이 받음");

            // 누르면 묻는 창 없이 카카오맵 쪽 (보호소는 길안내 줄이 아니다 — D4 묻는 창과 겹치지 않음)
            Do(() => { R0926Nearby.EditorOpened.Clear(); ListRows()[0].GetComponent<Button>().onClick.Invoke(); });
            Sleep(0.5f);
            Check(() => !Active("GuideAsk0926") && R0926Nearby.EditorOpened.Count == 1 && R0926Nearby.EditorOpened[0].StartsWith("https://place.map.kakao.com/sb_"),
                "보호소 줄을 누르면 묻는 창 없이 카카오맵 장소 쪽");

            // 애견동반을 끄면 보호소 줄이 빠지고, 그 줄을 다시 쓰는 장소 줄은 원래 색
            Do(() => ClickToggle("PetFriendlyToggle"));
            Sleep(0.4f);
            Do(() => Plm()?.UpdateUI());
            Wait(() => { var p = Plm(); return p != null && !p.ShownEntries.Any(e => e.kind == PlaceListManager.EntryKind.Shelter) && ListRows().Count > 0; }, 6f, "애견동반 끔 → 보호소 줄 없음");
            Sleep(0.6f);
            Do(() => log.Add("  끈 뒤: " + rowsLog()));
            Check(() =>
            {
                var l = ListRows();
                var tpl = new Color(0, 0, 0, 0);
                foreach (var r in l)
                {
                    if (bandOn(r)) return false;
                    var d = r.Find("Dist")?.GetComponent<Text>();
                    if (d == null) return false;
                    if (tpl.a == 0f) tpl = d.color;
                    else if (!same(d.color, tpl, 0.01f)) return false;
                    var n = r.Find("Name")?.GetComponent<Text>();
                    if (n == null || same(n.color, R0926FilterChip.InkOnWhite, 0.02f)) return false;
                }
                return l.Count > 0;
            }, "애견동반을 끄면 맨 윗줄도 바탕 없이 원래 이름 · 거리 색 (줄을 다시 써도 남지 않음)");

            // 되돌리기 — 애견동반 · 넣은 보호소 · 서버 주소 · 목록
            Do(() =>
            {
                if (fm != null && fm.PetState != petWas) ClickToggle("PetFriendlyToggle");
                R0926Nearby.EditorForget();
                R0926Nearby.EditorBase = null;
                R0926Nearby.EditorOpened = null;
                if (Active("ListPanel")) Click(ListX + "/ClosePx0926");
            });
            Sleep(0.8f);
            Check(() => fm == null || fm.PetState == petWas, "되돌림: 애견동반 처음 상태");
        }
    }
}
