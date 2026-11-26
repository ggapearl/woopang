using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-27 35번 대표님 「'근처 장소' 에서 오브젝트를 더블터치해서 … 발생하는 첫 사진(sub사진) 이 공백으로 발생하는 경우가 있거든? … 만든사람 부분에 중복으로 뭔가 글자가 발생하거든?」
        //  · AR 에 없는 장소의 정보 창(대리 오브젝트 PlaceInfo0926)은 장면의 아무 큐브에서 창 연결을 받는다 — 관광공사 큐브에서 받으면 위 글 칸(TourAPI_Description)이 따라왔다
        //  · 우팡 장소: 위 글 칸 꺼짐(예전엔 인스타 아이디가 「Created by …」 자리에 겹쳐 찍힘) · 「Created by 이름」 한 줄 · 첫 장은 첫 상세 사진(예전엔 글 없는 정보 글 쪽 = 빈 회색)
        //  · 정보 글이 있는 곳(역 · 터미널 · 가게 · 관광공사): 예전대로 정보 글 쪽이 첫 장 · 주소 · 개요가 보인다
        //  관광공사 큐브에서 받은 경우를 일부러 만든다(위 글 칸을 대리 창에 넣음). 상세 사진은 시험용 두 장을 사진 목록에 바로 넣는다(서버에 묻지 않음).
        //  끝나면 연결 · 사진 · 채운 값을 되돌린다
        private static void PlanInfoHost()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            const string G = "FullScreenPanel/FullScreenGuide/";
            DoubleTap3D host = null;
            Text descWas = null;
            bool opened = false;
            var made = new List<Sprite>();
            FieldInfo fIsInfo = typeof(DoubleTap3D).GetField("isPlaceInfoPage", NP);
            FieldInfo fIndex = typeof(DoubleTap3D).GetField("imageIndex", NP);
            FieldInfo fInfoText = typeof(DoubleTap3D).GetField("placeInfoText", NP);
            bool IsInfo() => host != null && fIsInfo?.GetValue(host) is bool b && b;
            int Index() => host != null && fIndex?.GetValue(host) is int i ? i : -99;
            Text InfoText() => host != null ? fInfoText?.GetValue(host) as Text : null;
            bool Open() => host != null && host.fullscreenCanvasGroup != null && host.fullscreenCanvasGroup.gameObject.activeSelf && DoubleTap3D.Shown == host;
            bool Closed() => host == null || host.fullscreenCanvasGroup == null || !host.fullscreenCanvasGroup.gameObject.activeSelf;
            string Big() { var img = host != null ? host.fullscreenImage : null; return img == null || !img.enabled ? "꺼짐" : img.sprite != null ? img.sprite.name : "빈 칸"; }
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

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                host = R0926PlaceInfo.Host();
            });
            Wait(() => host != null && host.IsReady, 10f, "대리 정보 창 준비");
            Check(() =>
            {
                var pnl = host != null ? host.placeInfoTextPanel : null;
                Text old = pnl != null ? pnl.GetComponentInChildren<Text>() : null;   // 예전 방식 (꺼진 패널 안은 못 찾는지 참고로)
                log.Add($"  대리 창 정보 글 패널 {(pnl != null ? (pnl.activeInHierarchy ? "있음 · 켜짐" : "있음 · 꺼짐") : "없음")} · 글 칸 {(InfoText() != null ? "찾음" : "못 찾음")} · (참고) 예전 방식으로는 {(old != null ? "찾음" : "못 찾음")}");
                return pnl != null && InfoText() != null;
            }, "대리 창은 고른 원본과 상관없이 정보 글 패널을 가진다 · 꺼진 패널 안의 글 칸도 찾는다");

            // 관광공사 큐브에서 연결을 받은 경우로 — 위 글 칸(TourAPI_Description)을 대리 창에 넣는다
            Do(() =>
            {
                descWas = host.descriptionTextUI;
                Transform t = Find(G + "TourAPI_Description");
                Text tour = t != null ? t.GetComponent<Text>() : null;
                if (tour == null && host.fullscreenCanvasGroup != null)
                    foreach (var x in host.fullscreenCanvasGroup.GetComponentsInChildren<Text>(true))
                        if (x.name == "TourAPI_Description") { tour = x; break; }
                if (tour != null) host.descriptionTextUI = tour;
                log.Add($"  관광공사 큐브 연결로: 위 글 칸 {(tour != null ? "넣음" : "없음")} (원래 {(descWas == null ? "없음" : descWas == tour ? "같은 칸" : "다른 칸")})");
            });

            // 1) 우팡 장소 — 인스타 아이디가 있는 곳 (타이어24 처럼)
            Do(() =>
            {
                var p = new PlaceData { id = 990435, name = "타이어24 시험", username = "tester35", instagram_id = "tire24_test", category = "shop", model_type = "cube" };
                bool filled = R0926PlaceInfo.Fill(host, p);
                host.imageSprites = new List<Sprite> { Make(new Color(0.95f, 0.55f, 0.2f), "t35a"), Make(new Color(0.2f, 0.62f, 0.7f), "t35b") };
                opened = filled && host.OpenInfo();
            });
            Wait(() => Open(), 5f, "우팡 장소 정보 창 열림");
            Sleep(0.7f);
            Shot("infohost_first");
            Check(() =>
            {
                var cb = host.createdByText;
                bool descOff = host.descriptionTextUI == null || !host.descriptionTextUI.gameObject.activeInHierarchy;
                log.Add($"  위 글 칸 {(descOff ? "꺼짐" : "켜짐 「" + host.descriptionTextUI.text + "」")} · 만든 사람 줄 「{(cb != null && cb.gameObject.activeInHierarchy ? cb.text : "안 보임")}」");
                return opened && descOff && cb != null && cb.gameObject.activeInHierarchy && cb.text == "Created by tester35";
            }, "우팡 장소 — 위 글 칸 꺼짐(인스타 아이디가 「Created by」 자리에 겹치지 않음) · 「Created by 이름」 한 줄");
            Check(() =>
            {
                bool panelOff = host.placeInfoTextPanel == null || !host.placeInfoTextPanel.activeInHierarchy;
                log.Add($"  첫 장: 정보 글 쪽 {(IsInfo() ? "예" : "아니오")} · 사진 번호 {Index()} · 정보 글 패널 {(panelOff ? "꺼짐" : "켜짐")} · 큰 사진 {Big()}");
                return !IsInfo() && Index() == 0 && panelOff && Big() == "t35a";
            }, "우팡 장소 — 첫 장은 첫 상세 사진 (글 없는 정보 글 쪽 = 빈 회색이 먼저 뜨지 않음)");
            Do(() => host.ShowPreviousImage());
            Sleep(0.7f);
            Check(() =>
            {
                log.Add($"  이전으로: 정보 글 쪽 {(IsInfo() ? "예" : "아니오")} · 사진 번호 {Index()} · 큰 사진 {Big()}");
                return !IsInfo() && Index() == 0 && Big() == "t35a";
            }, "첫 사진에서 이전으로 넘겨도 빈 정보 글 쪽으로 가지 않는다");
            Do(() => host.ShowNextImage());
            Sleep(0.9f);
            Shot("infohost_second");
            Check(() =>
            {
                log.Add($"  다음으로: 사진 번호 {Index()} · 큰 사진 {Big()}");
                return Index() == 1 && Big() == "t35b";
            }, "다음으로 넘기면 둘째 상세 사진");
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => Closed(), 5f, "정보 창 닫힘");

            // 2) 정보 글이 있는 곳 (역) — 예전대로 정보 글 쪽이 첫 장
            Do(() =>
            {
                host.imageSprites = new List<Sprite>();   // 넣은 사진은 비운다 (역은 상세 사진 없음)
                var f = new FacilityData { type = "subway", name = "시험역", address = "시험 주소 1", extra_info = "시험 개요" };
                opened = R0926PlaceInfo.Fill(host, f) && host.OpenInfo();
            });
            Wait(() => Open(), 5f, "역 정보 창 열림");
            Sleep(0.7f);
            Shot("infohost_station");
            Check(() =>
            {
                var t = InfoText();
                bool panelOn = host.placeInfoTextPanel != null && host.placeInfoTextPanel.activeInHierarchy;
                string txt = t != null ? t.text : null;
                log.Add($"  역: 정보 글 쪽 {(IsInfo() ? "예" : "아니오")} · 번호 {Index()} · 패널 {(panelOn ? "켜짐" : "꺼짐")} · 글 {(txt == null ? "칸 없음" : txt.Contains("시험 주소 1") && txt.Contains("시험 개요") ? "주소 · 개요 보임" : "주소 · 개요 없음")}");
                return opened && IsInfo() && Index() == -1 && panelOn && txt != null && txt.Contains("시험 주소 1") && txt.Contains("시험 개요");
            }, "정보 글이 있는 곳(역 · 터미널 · 가게 · 관광공사)은 예전대로 정보 글 쪽이 첫 장 · 주소 · 개요가 보인다");
            Check(() =>
            {
                var cb = host.createdByText;
                bool descOff = host.descriptionTextUI == null || !host.descriptionTextUI.gameObject.activeInHierarchy;
                log.Add($"  역: 위 글 칸 {(descOff ? "꺼짐" : "켜짐")} · 만든 사람 줄 「{(cb != null && cb.gameObject.activeInHierarchy ? cb.text : "안 보임")}」");
                return descOff && cb != null && cb.gameObject.activeInHierarchy && cb.text == "Created by WOOPANG";
            }, "역 — 위 글 칸 꺼짐 · 「Created by WOOPANG」 한 줄");
            Sleep(0.6f);
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => Closed(), 5f, "정보 창 닫힘");

            // 되돌리기 — 위 글 칸 연결 · 넣은 사진 · 채운 값
            Do(() =>
            {
                if (host != null)
                {
                    host.descriptionTextUI = descWas;
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
            Check(() => host == null || (host.descriptionTextUI == descWas && host.imageSprites.Count == 0), "되돌림 — 위 글 칸 연결 · 넣은 사진 처음대로");
        }
    }
}
