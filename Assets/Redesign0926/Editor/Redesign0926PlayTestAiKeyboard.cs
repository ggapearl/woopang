using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-24 N2 대표님 「이게 타이핑 하는데 해당 위치에 키패드가 올라와서 타이핑이 어떻게 입력되는 지 안보이거든? … 적당히 아래로 스크롤 해서,
        //  해당 입력창은 보이도록 … 아니면 그 AI추가 작업창 … 창 자체를 좀 올려서 혹은 크게?」
        //  · 장소 추가 ▸ AI 작업 ▸ 3D 캐릭터 ▸ 요청사항 — 키보드 위 입력줄이 뜨면 카드째 입력줄 위로 (요청사항 칸이 입력줄 위에 보인다) · 닫으면 제자리
        //  · 정보 수정 ▸ AI 작업 ▸ 3D 캐릭터 ▸ 요청사항 — 요청사항 · 네 칸을 입력줄 위로 (미리보기가 그만큼 줄어든다) · 닫으면 제자리
        //  · 정보 수정 AI 화면이 물방울 · 네온에서도 수정 카드를 가린다 (예전엔 바탕이 안 그려져 뒤 수정 칸들이 비쳤다) · 바깥 빛은 한 번만
        //  시험 서버(127.0.0.1:5792 · 임시 DB)의 장소 #3 만 연다 — 운영 서버에 묻지 않는다. 디스플레이 설정은 되돌린다.
        private static bool akRun;
        private static int akStyle = -1, akNeon = -1, akDensity = -1;

        // 끝에서 한 번, 오류로 끊겨도 실행기 끝에서 한 번
        private static void AkFinish()
        {
            if (!akRun) return;
            akRun = false;
            SnWrite(SnStyleKey, akStyle);
            SnWrite(SnNeonKey, akNeon);
            SnWrite(SnDensityKey, akDensity);
            PlayerPrefs.Save();
            R0926MenuStyle.Reload();
            CubeDataFixManager.EditorBase = null;
            R0926AiWork.Provider = null;
        }

        private static void PlanAiKeyboard()
        {
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            const string Bc = "ARPreviewPanel/MessageContainer/ButtonContainer/", Op = "ARPreviewPanel/MessageContainer/AiOptions0926/";
            const string Card = "ARPreviewPanel/MessageContainer";
            const string AddText = "노란 고양이, 빨간 모자", FixText = "빨간 모자를 쓴 고양이";
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Exception
                    || type == LogType.Error && (msg.Contains("[R0926AiWorkPanel]") || msg.Contains("[R0926FixAiPanel]") || msg.Contains("[CubeDataFixManager]") || msg.Contains("UploadInputMirror")))
                    errors.Add(type + " " + msg);
            };
            var T = typeof(CubeDataFixManager);
            var P = typeof(R0926FixAiPanel);
            var activate = typeof(UploadInputMirror).GetMethod("ActivateMirrorFor", F);
            R0926AiWorkPanel add = null;
            ARPreviewController prev = null;
            CubeUploadManager cum = null;
            CubeDataFixManager fix = null;
            R0926FixAiPanel fai = null;
            Texture2D photo = null;
            Rect cardWas = default, askWas = default, tilesWas = default, stageWas = default;

            Func<string, object> fv = n => T.GetField(n, F).GetValue(fix);
            Action<string, object> fs = (n, v) => T.GetField(n, F).SetValue(fix, v);
            Func<string, object[], object> call = (n, a) => T.GetMethod(n, F).Invoke(fix, a);
            Func<string, object> av = n => P.GetField(n, F).GetValue(fai);
            Func<string, RectTransform> part = n => (RectTransform)av(n);
            Func<GameObject> fixPanel = () => (GameObject)fv("fixUIPanel");
            Func<bool> loaded = () => fix != null && !(bool)fv("isProcessing") && fv("cleanPhotos") != null;
            Func<UploadInputMirror> addMirror = () => add != null ? typeof(R0926AiWorkPanel).GetField("mirror", F).GetValue(add) as UploadInputMirror : null;
            Func<UploadInputMirror> fixMirror = () => fai != null ? av("mirror") as UploadInputMirror : null;
            Func<InputField> addReq = () => Find(Op + "Ask/Request")?.GetComponent<InputField>();
            Func<InputField> fixReq = () => fai != null ? av("request") as InputField : null;
            Func<RectTransform> card = () => Find(Card)?.transform as RectTransform;
            // 캔버스 단위 · 화면 아래 끝이 0
            Func<RectTransform, Rect> cv = rt =>
            {
                var root = rt != null ? rt.GetComponentInParent<Canvas>()?.rootCanvas : null;
                if (root == null) return default;
                var cr = (RectTransform)root.transform;
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                Vector3 a = cr.InverseTransformPoint(c[0]), b = cr.InverseTransformPoint(c[2]);
                return Rect.MinMaxRect(a.x - cr.rect.xMin, a.y - cr.rect.yMin, b.x - cr.rect.xMin, b.y - cr.rect.yMin);
            };
            // 화면 위 안전선 (캔버스 단위 · 아래 끝부터)
            Func<RectTransform, float> safeTop = rt =>
            {
                var root = rt != null ? rt.GetComponentInParent<Canvas>()?.rootCanvas : null;
                if (root == null) return 0f;
                return ((RectTransform)root.transform).rect.height - (Screen.height - Screen.safeArea.yMax) / Mathf.Max(0.01f, root.scaleFactor);
            };
            Func<Rect, Rect, bool> same = (a, b) => Mathf.Abs(a.xMin - b.xMin) < 1.5f && Mathf.Abs(a.xMax - b.xMax) < 1.5f && Mathf.Abs(a.yMin - b.yMin) < 1.5f && Mathf.Abs(a.yMax - b.yMax) < 1.5f;
            Func<string> skinState = () =>
            {
                var img = fai != null ? part("view").GetComponent<Image>() : null;
                var sk = img != null ? img.GetComponent<R0926WindowSkin>() : null;
                var fl = img != null ? img.GetComponent<R0926WindowFill>() : null;
                var under = fai != null ? fai.GetComponent<R0926WindowSkin>() : null;
                return $"AI 화면 창 모양 {(sk == null ? "없음" : sk.Showing ? "그림" : "안 그림")}" + (sk != null && sk.Showing ? $" · 밑 창과 같은 자리 {sk.Stacked} · 비침 {sk.SeeThrough} · 가림 {sk.Covered}" : "")
                       + $" · 칸 맞춤 {(fl != null ? fl.FillMode.ToString() : "없음")} · 수정 카드 비침 {under?.SeeThrough} · 가림 {under?.Covered}";
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
                akStyle = SnRead(SnStyleKey); akNeon = SnRead(SnNeonKey); akDensity = SnRead(SnDensityKey);
                akRun = true;
                R0926MenuStyle.Droplet = true; R0926MenuStyle.Neon = true; R0926MenuStyle.Density = 70;   // 처음 켠 앱 그대로 (물방울 · 네온)
                Application.logMessageReceived += onLog;
                R0926AiWork.Provider = null;
                cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                prev = UnityEngine.Object.FindAnyObjectByType<ARPreviewController>(FindObjectsInactive.Include);
                add = UnityEngine.Object.FindAnyObjectByType<R0926AiWorkPanel>(FindObjectsInactive.Include);
                AimCamera(-14f, 0f);
                const int S = 256;
                photo = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "AiKbPhoto" };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                        px[y * S + x] = new Color32((byte)(40 + x * 0.7f), (byte)(110 + y * 0.5f), (byte)(210 - x * 0.4f - y * 0.2f), 255);
                photo.SetPixels32(px);
                photo.Apply();
                typeof(CubeUploadManager).GetField("mainPhoto", F).SetValue(cum, photo);
                typeof(CubeUploadManager).GetMethod("ClearAi", F).Invoke(cum, null);
                typeof(CubeUploadManager).GetMethod("StartARPreview", F).Invoke(cum, new object[] { photo });
            });
            Wait(() => prev != null && prev.CubeReady, 15f, "미리보기 큐브");
            Sleep(0.3f);

            // 1) 장소 추가 ▸ AI 작업 ▸ 3D 캐릭터 — 요청사항 칸
            Do(() => Click(Bc + "AiButton0926"));
            Sleep(0.3f);
            Do(() => Click(Op + "Tiles/Chip4"));
            Wait(() => add != null && add.Open && !add.Working && add.Model != null, 40f, "3D 캐릭터");
            Sleep(0.5f);
            Do(() => cardWas = cv(card()));
            Check(() =>
            {
                var f = addReq();
                Rect fr = f != null ? cv((RectTransform)f.transform) : default;
                log.Add($"  카드 {cardWas.yMin:0}~{cardWas.yMax:0} · 요청사항 칸 {fr.yMin:0}~{fr.yMax:0} · 키보드 {EditorVirtualKeyboard.CurrentVisibleHeight:0}");
                return f != null && f.gameObject.activeInHierarchy && addMirror() != null && addMirror().ActiveSource == null;
            }, "장소 추가 AI 작업: 3D 캐릭터 → 요청사항 칸 (키보드는 아직)");

            // 2) 요청사항 칸 → 키보드 위 입력줄 — 카드째 입력줄 위로
            Do(() => activate.Invoke(addMirror(), new object[] { addReq() }));
            Wait(() => addMirror().PlacedBar != null && KbRetryUp(), 5f, "장소 추가 — 입력줄이 키보드 위에");
            Sleep(1.2f);   // 카드가 다 올라갈 때까지
            Check(() =>
            {
                var m = addMirror();
                Rect bar = cv(m.PlacedBar), f = cv((RectTransform)addReq().transform), c = cv(card());
                float top = safeTop(card());
                log.Add($"  키보드 {EditorVirtualKeyboard.CurrentVisibleHeight:0} · 입력줄 {bar.yMin:0}~{bar.yMax:0} · 요청사항 칸 {f.yMin:0}~{f.yMax:0} (칸~입력줄 {f.yMin - bar.yMax:0})"
                        + $" · 카드 {c.yMin:0}~{c.yMax:0} (올리기 전 {cardWas.yMin:0}~{cardWas.yMax:0} · 올린 만큼 {c.yMax - cardWas.yMax:0}) · 화면 위 안전선 {top:0}");
                bool whole = c.yMin >= bar.yMax + 24f;                 // 카드가 통째로 입력줄 위
                bool capped = c.yMax >= top - 28f - 2f && c.yMax <= top;   // 다 못 들어가 위 안전선 아래까지만
                return m.ActiveSource == addReq() && f.yMin >= bar.yMax + 24f && f.yMax <= top && (whole || capped || f.yMin - bar.yMax <= 32f) && c.yMax > cardWas.yMax + 1f;
            }, "장소 추가: 입력줄이 뜨면 카드째 올라가 요청사항 칸이 입력줄 위에 보인다 (화면 위 안전선 아래 · 칸은 꼭 입력줄 위)");
            Check(() => BarOnKeyboard(addMirror(), "장소 추가 요청사항"), "입력줄은 그대로 키보드 바로 위");
            Do(() => addMirror().Bar.text = AddText);
            Sleep(0.4f);
            Check(() => addReq().text == AddText, "친 글이 요청사항 칸에 — 입력줄 위에서 보인다");
            Shot("aikb_add_typing");

            // 3) 키보드를 내리면 카드가 제자리로 (보내지 않는다)
            Do(() => addMirror().CloseBySwipe());
            Sleep(1.2f);
            Check(() =>
            {
                Rect c = cv(card());
                log.Add($"  닫은 뒤 카드 {c.yMin:0}~{c.yMax:0} (처음 {cardWas.yMin:0}~{cardWas.yMax:0}) · 입력줄 {(addMirror().ActiveSource != null ? "열림" : "닫힘")} · 키보드 {EditorVirtualKeyboard.CurrentVisibleHeight:0} · 만드는 중 {add.Working}");
                return addMirror().ActiveSource == null && same(c, cardWas) && addReq().text == AddText && !add.Working;
            }, "장소 추가: 키보드를 내리면 카드가 제자리 · 친 글은 칸에 남고 · 보내지 않음");
            Shot("aikb_add_closed");
            Do(() =>
            {
                (typeof(R0926AiWorkPanel).GetField("viewCancel", F).GetValue(add) as Button)?.onClick.Invoke();
                var cancel = Find(Bc + "CancelButton")?.GetComponent<Button>();
                if (cancel != null && cancel.gameObject.activeInHierarchy) cancel.onClick.Invoke();
            });
            Sleep(0.6f);
            // 취소하면 장소 추가 화면으로 돌아온다 — 그 화면은 닫고 정보 수정으로 (안 닫으면 오른쪽 끝에 비쳐 보인다)
            Do(() => typeof(CubeUploadManager).GetMethod("ResetToInitialState", F).Invoke(cum, null));
            Sleep(0.3f);
            Check(() => !((GameObject)typeof(CubeUploadManager).GetField("uploadPage", F).GetValue(cum)).activeInHierarchy, "장소 추가 화면을 닫았다");

            // 4) 정보 수정 ▸ AI 작업 ▸ 3D 캐릭터 — 시험 서버의 장소 #3 을 남의 장소 수정 요청 창으로
            Do(() =>
            {
                CubeDataFixManager.EditorBase = FixDev;
                fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                fai = UnityEngine.Object.FindAnyObjectByType<R0926FixAiPanel>(FindObjectsInactive.Include);
                fixPanel().SetActive(true);
                fs("doubleTap", null);
                int gen = (int)fv("loadGen") + 1;
                fs("loadGen", gen);
                call("ResetToInitialState", null);
                fs("editId", 3);
                call("SetOwnerMode", new object[] { false });
                var pd = new PlaceData { id = 3, name = "시험 장소", main_photo = "uploads/seed_3/main.jpg", sub_photos = new List<List<string>>() };
                fix.StartCoroutine((IEnumerator)call("LoadPlace", new object[] { pd, gen }));
            });
            Wait(loaded, 40f, "장소 #3 수정 요청 창");
            Do(() => { if ((bool)fv("ownerMode")) call("SetOwnerMode", new object[] { false }); });
            Sleep(0.3f);
            Do(() => ((Button)av("aiButton")).onClick.Invoke());
            Sleep(0.6f);
            Check(() =>
            {
                var img = part("view").GetComponent<Image>();
                var sk = img != null ? img.GetComponent<R0926WindowSkin>() : null;
                log.Add("  " + skinState());
                return fai.Open && sk != null && sk.Showing && sk.Stacked && sk.Covered && img.GetComponent<R0926WindowFill>() == null;
            }, "물방울 · 네온: 정보 수정 AI 화면이 제 창 모양으로 수정 카드를 가린다 (밑 창 모양을 이어 그림 · 뒤 칸이 비치지 않음 · 바깥 빛은 한 번)");
            Shot("aikb_fix_open");
            Do(() => ((Button[])av("chips"))[3].onClick.Invoke());
            Wait(() => fai.Open && !fai.Working && fai.Made != null && fai.Made.Glb != null, 40f, "정보 수정 — 3D 캐릭터");
            Sleep(0.6f);
            Do(() => { askWas = cv(part("ask")); tilesWas = cv(part("tiles")); stageWas = cv(part("stage")); });
            Check(() =>
            {
                log.Add($"  요청사항 {askWas.yMin:0}~{askWas.yMax:0} · 네 칸 {tilesWas.yMin:0}~{tilesWas.yMax:0} · 미리보기 {stageWas.yMin:0}~{stageWas.yMax:0}");
                return part("ask").gameObject.activeInHierarchy && fixMirror() != null && fixMirror().ActiveSource == null;
            }, "정보 수정 AI 작업: 3D 캐릭터 → 요청사항 칸 (키보드는 아직)");
            Check(() =>
            {
                var ph = (RawImage)av("photo");
                var mk = ph != null ? ph.GetComponentInParent<Mask>() : null;
                var wf = mk != null ? mk.GetComponent<R0926WindowFill>() : null;
                var mv = (Component)av("model");
                log.Add($"  미리보기 틀(마스크) {(mk != null ? mk.name : "없음")} · 창 채우기 {(wf != null ? wf.FillMode.ToString() : "없음")} · 캐릭터 {(mv != null && mv.gameObject.activeInHierarchy ? "보임" : "숨김")}");
                return mk != null && mk.enabled && (wf == null || wf.FillMode != R0926WindowFill.Mode.Page) && mv != null && mv.gameObject.activeInHierarchy;
            }, "물방울 · 네온: 미리보기 틀이 지워지지 않아 캐릭터가 보인다");
            Shot("aikb_fix_char");

            // 5) 요청사항 칸 → 키보드 위 입력줄 — 요청사항 · 네 칸이 입력줄 위로 · 미리보기는 그만큼 줄어든다
            Do(() => activate.Invoke(fixMirror(), new object[] { fixReq() }));
            Wait(() => fixMirror().PlacedBar != null && KbRetryUp(), 5f, "정보 수정 — 입력줄이 키보드 위에");
            Sleep(1.2f);
            Check(() =>
            {
                var m = fixMirror();
                Rect bar = cv(m.PlacedBar), f = cv((RectTransform)fixReq().transform), ask = cv(part("ask")), tl = cv(part("tiles")), sg = cv(part("stage"));
                Rect st = cv(((Text)av("status")).rectTransform), view = cv(part("view"));
                log.Add($"  키보드 {EditorVirtualKeyboard.CurrentVisibleHeight:0} · 입력줄 {bar.yMin:0}~{bar.yMax:0} · 요청사항 칸 {f.yMin:0}~{f.yMax:0} (칸~입력줄 {f.yMin - bar.yMax:0})"
                        + $" · 네 칸 {tl.yMin:0}~{tl.yMax:0} · 미리보기 {sg.yMin:0}~{sg.yMax:0} (높이 {sg.height:0} · 처음 {stageWas.height:0}) · 안내 {st.yMin:0}~{st.yMax:0} · 화면 {view.yMin:0}~{view.yMax:0}");
                return m.ActiveSource == fixReq() && f.yMin >= bar.yMax + 24f && ask.yMax <= tl.yMin + 1f && tl.yMax <= sg.yMin + 1f && sg.yMax <= st.yMin + 1f
                       && sg.height >= 150f && sg.height < stageWas.height && tl.yMax <= view.yMax;
            }, "정보 수정: 입력줄이 뜨면 요청사항 · 네 칸이 입력줄 위로 — 미리보기가 그만큼 줄고 겹침 없음");
            Check(() => BarOnKeyboard(fixMirror(), "정보 수정 요청사항"), "입력줄은 그대로 키보드 바로 위");
            Do(() => fixMirror().Bar.text = FixText);
            Sleep(0.4f);
            Check(() => fixReq().text == FixText, "친 글이 요청사항 칸에 — 입력줄 위에서 보인다");
            Shot("aikb_fix_typing");

            // 6) 키보드를 내리면 제자리 (보내지 않는다)
            Do(() => fixMirror().CloseBySwipe());
            Sleep(1.2f);
            Check(() =>
            {
                Rect ask = cv(part("ask")), tl = cv(part("tiles")), sg = cv(part("stage"));
                log.Add($"  닫은 뒤 요청사항 {ask.yMin:0}~{ask.yMax:0} · 네 칸 {tl.yMin:0}~{tl.yMax:0} · 미리보기 {sg.yMin:0}~{sg.yMax:0} · 만드는 중 {fai.Working}");
                return fixMirror().ActiveSource == null && same(ask, askWas) && same(tl, tilesWas) && same(sg, stageWas) && fixReq().text == FixText && !fai.Working;
            }, "정보 수정: 키보드를 내리면 요청사항 · 네 칸 · 미리보기가 제자리 · 친 글은 칸에 남고 · 보내지 않음");

            // 7) 물방울만 · 네온만 · 둘 다 끔 — AI 화면은 늘 수정 카드를 가린다
            Do(() => { R0926MenuStyle.Droplet = true; R0926MenuStyle.Neon = false; });
            Sleep(0.5f);
            Check(() =>
            {
                var sk = part("view").GetComponent<R0926WindowSkin>();
                log.Add("  물방울만 — " + skinState());
                return sk != null && sk.Showing && sk.Stacked && sk.Covered;
            }, "물방울만: AI 화면이 수정 카드를 가린다 (밑 창 모양 그대로)");
            Do(() => { R0926MenuStyle.Droplet = false; R0926MenuStyle.Neon = true; });
            Sleep(0.5f);
            Check(() =>
            {
                var sk = part("view").GetComponent<R0926WindowSkin>();
                log.Add("  네온만 — " + skinState());
                return sk != null && sk.Showing && sk.Stacked && sk.Covered && !sk.SeeThrough;
            }, "네온만: AI 화면은 불투명 남색 (수정 칸들이 비치지 않음)");
            Shot("aikb_fix_neon");
            Do(() => { R0926MenuStyle.Droplet = false; R0926MenuStyle.Neon = false; });
            Sleep(0.5f);
            Check(() =>
            {
                var img = part("view").GetComponent<Image>();
                var sk = img.GetComponent<R0926WindowSkin>();
                log.Add($"  둘 다 끔 — {skinState()} · 바탕 알파 {img.color.a:0.00}");
                return sk != null && !sk.Showing && img.color.a >= 0.95f && img.enabled;
            }, "둘 다 끔: 예전처럼 불투명한 회색 바탕");

            // 되돌리기 — AI 화면 · 수정 창 · 서버 주소 · 디스플레이 설정
            Do(() =>
            {
                if (fai != null && fai.Open) ((Button)av("viewCancel")).onClick.Invoke();
                if (fixPanel() != null && fixPanel().activeSelf) { fixPanel().SetActive(false); call("ResetToInitialState", null); }
                AkFinish();
                Application.logMessageReceived -= onLog;
                foreach (var e in errors) log.Add("  오류: " + e);
            });
            Check(() => errors.Count == 0, "오류 없음 (AI 작업 · 입력줄 · 고치기 창)");
        }
    }
}
