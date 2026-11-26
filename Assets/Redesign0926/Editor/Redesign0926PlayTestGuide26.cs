using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 26번 대표님 「그리고 사용가이드 발생할 때 좌우로 스와이프할 때도 버튼 눌렀을 때처럼 햅틱효과가 있으면 좋을 것 같아(소리는 제외) 그리고 사용가이드를
        // 설정탭 '앱정보' 맨 위에 (문의 위쪽)  버튼을 하나 만들어서 누르면 사용가이드 나오도록 해주고, 처음 앱 설치한 사람도 사용가이드 끝나면 '근처 장소' 1초 후에 바로 보이도록 해줘」
        //  ① 카드를 손가락으로 넘기면 진동만(UIFeedbackManager.HapticOnly · 소리 없음) — 넘어갈 때만, 덜 밀거나 첫 장 너머는 조용히 · 버튼은 예전처럼 소리 + 진동
        //  ② 설정 › 앱 정보 맨 위(문의 위) 「사용가이드」 → 목록 시트를 내리고 첫 장부터 · 첫 실행 기록은 그대로 · 끝나도 근처 장소는 안 뜬다
        //  ③ 첫 설치 안내가 끝나면 1초 뒤 「근처 장소」 — 1초 전엔 안 뜸 · 안내 중 하단 버튼으로 창을 열었으면 건너뜀 (닫아도 뒤늦게 안 튀어나옴)
        private static bool g26Run;
        private static int g26Key = int.MinValue;   // 첫 실행 기록 (없으면 -1)

        private static void G26Finish()
        {
            if (!g26Run) return;
            g26Run = false;
            var cards = UnityEngine.Object.FindAnyObjectByType<R0926GuideCards>(FindObjectsInactive.Include);
            if (cards != null && cards.IsOpen) cards.Back();
            if (g26Key != int.MinValue)
            {
                if (g26Key < 0) PlayerPrefs.DeleteKey(FirstTimeGuide.FirstTimeKey);
                else PlayerPrefs.SetInt(FirstTimeGuide.FirstTimeKey, g26Key);
                PlayerPrefs.Save();
                g26Key = int.MinValue;
            }
        }

        private static void PlanGuide26()
        {
            const string C = SetPath + "Viewport/Content/";
            FirstTimeGuide guide = null;
            R0926GuideCards cards = null;
            int h0 = 0, f0 = 0;
            float at = 0f, shownAfter = -1f;

            int Key() => PlayerPrefs.HasKey(FirstTimeGuide.FirstTimeKey) ? PlayerPrefs.GetInt(FirstTimeGuide.FirstTimeKey) : -1;
            int Haptics() => UIFeedbackManager.EditorHaptics - h0;
            int Sounds() => UIFeedbackManager.EditorFeedbacks - f0;
            string Fb() => $"진동만 {Haptics()} · 소리+진동 {Sounds()} · {(cards != null ? cards.Index + 1 : 0)}장째";
            void Mark() { h0 = UIFeedbackManager.EditorHaptics; f0 = UIFeedbackManager.EditorFeedbacks; }
            // 손가락으로 카드를 민다 — frac: 화면 폭의 몇 배 (- 왼쪽 = 다음 장). 같은 프레임이라 빠르기 없이 민 거리만으로 정해진다
            void Swipe(float frac)
            {
                float y = Screen.height * 0.5f, x0 = Screen.width * (frac < 0f ? 0.8f : 0.2f);
                var e = new PointerEventData(EventSystem.current) { position = new Vector2(x0, y) };
                cards.OnBeginDrag(e);
                e.position = new Vector2(x0 + Screen.width * frac, y);
                cards.OnDrag(e);
                cards.OnEndDrag(e);
            }
            string Near() => nmModal == null ? "없음" : $"{nmModal.EditorMode} · 카드 {(nmModal.EditorShowing ? "보임" : nmModal.EditorCardOn ? "나타나는 중" : "안 보임")} · 기다림 {(nmModal.EditorAfterGuidePending ? "있음" : "없음")}";

            NmBoot(true);
            NmWaitPlaces(1, true);
            Do(() =>
            {
                g26Run = true;
                g26Key = Key();
                NmClearAround();   // 4m 안에 든 것은 치운다 — 「발견한 장소」가 끼지 않게 (근처 카드는 이때부터 깨어 있다)
                guide = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                cards = UnityEngine.Object.FindAnyObjectByType<R0926GuideCards>(FindObjectsInactive.Include);
                log.Add($"  첫 실행 기록 {g26Key} · 근처 {Near()}");
            });
            Sleep(0.5f);
            Check(() => guide != null && cards != null && !guide.Showing && nmModal.EditorMode != "Nearby", "준비: 안내(FirstTimeGuide · R0926GuideCards) 닫힘 · 근처 장소 안 뜸");

            // ① 손가락으로 넘기기 — 진동만, 소리 없이
            Do(() => guide.Replay(null));
            Wait(() => guide.Showing && cards.IsOpen, 3f, "안내 열림");
            Sleep(0.6f);
            Do(() => { Mark(); Swipe(-0.4f); });
            Sleep(0.5f);
            Check(() => { log.Add("  왼쪽으로 밂: " + Fb()); return cards.Index == 1 && Haptics() == 1 && Sounds() == 0; }, "왼쪽으로 밀어 다음 장 — 진동 한 번 · 소리 없음");
            Do(() => { Mark(); Swipe(-0.05f); });
            Sleep(0.5f);
            Check(() => { log.Add("  조금만 밂: " + Fb()); return cards.Index == 1 && Haptics() == 0 && Sounds() == 0; }, "덜 밀어 제자리로 돌아오면 진동 없음");
            Do(() => { Mark(); Swipe(0.4f); });
            Sleep(0.5f);
            Check(() => { log.Add("  오른쪽으로 밂: " + Fb()); return cards.Index == 0 && Haptics() == 1 && Sounds() == 0; }, "오른쪽으로 밀어 앞 장 — 진동 한 번 · 소리 없음");
            Do(() => { Mark(); Swipe(0.4f); });
            Sleep(0.5f);
            Check(() => { log.Add("  첫 장에서 오른쪽: " + Fb()); return cards.Index == 0 && Haptics() == 0; }, "첫 장 너머로 밀면 넘어가지 않아 진동 없음");
            Do(() => { Mark(); cards.Next(); });
            Sleep(0.5f);
            Check(() => { log.Add("  다음 버튼: " + Fb()); return cards.Index == 1 && Sounds() == 1 && Haptics() == 0; }, "다음 버튼은 예전처럼 소리 + 진동 한 번");
            Do(() => cards.Skip());
            Wait(() => !guide.Showing, 2f, "안내 닫힘");

            // ② 설정 › 앱 정보 맨 위 「사용가이드」
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.3f);
            Do(() => Modes()?.Show(2));
            Sleep(0.9f);
            Do(ScrollSettingsBottom);
            Sleep(0.6f);
            Check(() =>
            {
                var g = Find(C + "GroupInfo");
                var row = Find(C + "GroupInfo/RowGuide");
                string label = Find(C + "GroupInfo/RowGuide/Title")?.GetComponent<Text>()?.text ?? "";
                var names = new List<string>();
                if (g != null) foreach (Transform ch in g) names.Add(ch.name);
                log.Add($"  앱 정보 줄: {string.Join(" · ", names)} · 첫 줄 글 「{label}」");
                return row != null && row.gameObject.activeInHierarchy && row.GetSiblingIndex() == 0 && names.Count > 1 && names[1] == "RowContact"
                    && label == "사용가이드" && Find(C + "GroupInfo/RowGuide/Chevron") != null && row.GetComponent<Button>() != null && row.GetComponent<Button>().enabled;
            }, "설정 › 앱 정보 맨 위(문의 위)에 「사용가이드」 줄 (› · 누를 수 있음)");
            Shot("g26_settings");
            Do(() => { Click(C + "GroupInfo/RowGuide"); at = Time.unscaledTime; });
            Wait(() => !Active("ListPanel"), 2f, "목록 시트 내려감");
            Wait(() => guide.Showing && cards.IsOpen, 2.5f, "안내 열림");
            Check(() =>
            {
                log.Add($"  누르고 {Time.unscaledTime - at:0.00}초 · 열린 창 {OpenPanels()} · 첫 실행 기록 {Key()}");
                return guide.Showing && cards.IsOpen && cards.Index == 0 && !Active("ListPanel") && Key() == g26Key && !guide.EditorFirstRunPending;
            }, "누르면 목록 시트가 내려간 뒤 첫 장부터 안내 · 첫 실행 기록은 그대로");
            Sleep(0.6f);
            Shot("g26_replay");
            Do(() => cards.Skip());
            Wait(() => !guide.Showing, 2f, "안내 닫힘");
            Sleep(1.6f);
            Check(() => { log.Add("  닫고 1.6초: 근처 " + Near()); return nmModal.EditorMode != "Nearby" && !nmModal.EditorAfterGuidePending; }, "설정에서 다시 본 안내는 끝나도 근처 장소가 안 뜬다 (첫 설치만)");

            // ③ 첫 설치 — 안내가 끝나고 1초 뒤 「근처 장소」
            Do(() => guide.EditorFirstRun());
            Wait(() => guide.Showing && cards.IsOpen, 2.5f, "첫 설치 안내 열림");
            Sleep(0.8f);
            Do(() => cards.Skip());
            Wait(() => !guide.Showing, 2f, "첫 설치 안내 닫힘");
            Do(() => { at = Time.unscaledTime; shownAfter = -1f; log.Add("  닫힘: 근처 " + Near()); });
            Sleep(0.5f);
            Check(() => { log.Add("  닫고 0.5초: 근처 " + Near()); return !nmModal.EditorCardOn && nmModal.EditorAfterGuidePending; }, "1초 전에는 아직 안 뜬다 (기다리는 중)");
            Wait(() =>
            {
                if (!nmModal.EditorShowing || nmModal.EditorMode != "Nearby") return false;
                if (shownAfter < 0f) shownAfter = Time.unscaledTime - at;
                return true;
            }, 3f, "근처 장소 뜸");
            Check(() => { log.Add($"  근처 장소가 다 보인 때: 닫고 {shownAfter:0.00}초 · {Near()}"); return shownAfter >= 0.95f && shownAfter <= 2f && !nmModal.EditorAfterGuidePending; },
                "첫 설치 안내가 끝나고 1초 뒤 「근처 장소」 (나타나는 0.22초 포함)");
            Shot("g26_near");
            Do(() => nmModal.EditorDismiss());
            Sleep(0.8f);

            // ③-2 안내 중 하단 「목록」을 눌렀다 — 목록이 열리고 안내는 닫힌다(10-19). 다른 일을 하는 중이라 근처 장소는 건너뜀
            Do(() => guide.EditorFirstRun());
            Wait(() => guide.Showing && cards.IsOpen, 2.5f, "첫 설치 안내 다시 열림");
            Sleep(0.6f);
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => !guide.Showing, 2f, "하단 버튼으로 안내 닫힘");
            Sleep(1.8f);
            Check(() => { log.Add($"  목록 연 채 1.8초: 근처 {Near()} · 열린 창 {OpenPanels()}"); return Active("ListPanel") && nmModal.EditorMode != "Nearby" && !nmModal.EditorAfterGuidePending; },
                "안내 중 하단 버튼으로 창(목록)을 열었으면 근처 장소는 건너뜀");
            Do(BackNow);
            Sleep(1.2f);
            Check(() => { log.Add($"  목록 닫고 1.2초: 근처 {Near()} · 열린 창 {OpenPanels()}"); return !Active("ListPanel") && !nmModal.EditorCardOn; }, "목록을 닫아도 뒤늦게 튀어나오지 않음");

            Do(G26Finish);
            Check(() => Key() == g26Key || g26Key == int.MinValue, "되돌림 — 첫 실행 기록 그대로");
        }
    }
}
