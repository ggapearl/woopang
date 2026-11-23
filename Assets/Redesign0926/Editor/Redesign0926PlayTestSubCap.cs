using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-17 B7 — 추가 사진은 10장까지 (11-16 대표님 「수정요청할 때 서브사진 선택하는 바에 있어서 갤러리에서 10장이상 사진이 선택되거든?
    /// … 10장이 초과되는 사진은 안내와 함께 선택이 되지 않도록」 · 「업로드할 때도 마찬가지일 지」).
    ///  · subcap : 장소 수정(시험 서버 5792 · 장소 #3) · 장소 올리기 · 3D 모델 올리기 세 곳에서 같은 차례로
    ///             12장 고름 → 10장만 · 안내 / 남은 칸만큼 고름(아이폰 창이 거기서 막는 경우) → 다 참 · 안내 / 덜 고름 → 안내 없음 /
    ///             다 찬 뒤 + → 촬영·갤러리 창 없이 안내 · 갤러리를 바로 불러도 안내만 / 한 칸 남으면 + 는 예전처럼.
    /// 아이폰 고르기 창의 장수 제한(selectionLimit)은 에디터에서 열 수 없다 — 기기에서 확인.
    /// 서버에 아무것도 보내지 않는다 (수정 창은 시험 서버에서 불러오기만 · 올리기 창은 사진만 넣고 지운다).
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanSubCap()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            const System.Reflection.BindingFlags FS = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            string dir = Path.Combine(Application.temporaryCachePath, "pt_subcap");
            var files = new List<string>();
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) => { if (type == LogType.Exception) errors.Add(msg); };

            MonoBehaviour cur = null;   // 지금 보는 화면의 매니저 (CubeDataFixManager · CubeUploadManager · ModelUploadManager)
            Func<string, object> fv = n => cur.GetType().GetField(n, F).GetValue(cur);
            Func<string, object[], object> call = (n, a) => cur.GetType().GetMethod(n, F).Invoke(cur, a);
            Func<List<Texture2D>> subs = () => (List<Texture2D>)fv("subPhotos");
            Func<string> warnText = () =>
            {
                var w = (GameObject)fv("warningObj");
                var t = w != null ? w.GetComponentInChildren<Text>(true) : null;
                return w != null && w.activeInHierarchy && t != null ? t.text : "";
            };
            Func<string> maxText = () => (string)typeof(CubeUploadManager).GetProperty("MaxSubText", FS).GetValue(null);   // 세 화면이 같은 말
            Func<GameObject> dialogPanel = () =>
            {
                var fi = cur.GetType().GetField("photoSourceDialog", F);
                var d = fi != null ? fi.GetValue(cur) as PhotoSourceDialog : null;
                return d != null ? typeof(PhotoSourceDialog).GetField("dialogPanel", F).GetValue(d) as GameObject : null;
            };
            Func<bool> dialogOpen = () => dialogPanel() != null && dialogPanel().activeInHierarchy;
            Action<int> keep = n =>   // 앞의 n 장만 남긴다 (수정 창은 사진마다 원래 주소 칸이 짝으로 있다)
            {
                var s = subs();
                while (s.Count > n) { UnityEngine.Object.Destroy(s[s.Count - 1]); s.RemoveAt(s.Count - 1); }
                if (cur is CubeDataFixManager) { var k = (IList)fv("subKeepPaths"); while (k.Count > n) k.RemoveAt(k.Count - 1); }
                call("UpdateSubPhotoGrid", null);
            };
            Action hideWarn = () => { cur.CancelInvoke("HideWarning"); call("HideWarning", null); };
            bool done = false;
            Action<int> load = n =>   // 고르기 창이 n 장을 돌려준 것처럼 — 실제 읽기 코루틴
            {
                done = false;
                string m = cur is CubeUploadManager ? "LoadMultipleImagesWithFallback" : "LoadSubPhotosWithFallback";
                cur.StartCoroutine((IEnumerator)call(m, new object[] { files.GetRange(0, n).ToArray(), (Action)(() => done = true) }));
            };
            Func<string> state = () => $"{subs().Count}장 · 안내 「{warnText()}」";

            // 같은 차례를 세 화면에서 — 3D 모델은 + 가 바로 갤러리(촬영/갤러리 창 없음)
            Action<string, string, bool> sequence = (what, tag, hasDialog) =>
            {
                Do(() => { keep(0); hideWarn(); });
                Do(() => load(12));
                Wait(() => done, 30f, what + " — 12장 읽기");
                Check(() => { log.Add("  12장 고름: " + state()); return subs().Count == 10 && warnText() == maxText(); },
                    what + ": 12장을 골라도 10장만 들어가고 「최대 10장」 안내 (안드로이드 고르기 창은 장수를 못 막는다)");

                Do(() => { hideWarn(); keep(7); });
                Do(() => load(3));
                Wait(() => done, 30f, what + " — 남은 3칸 읽기");
                Check(() => { log.Add("  남은 칸만큼: " + state()); return subs().Count == 10 && warnText() == maxText(); },
                    what + ": 남은 칸만큼 고르면(아이폰 고르기 창이 거기서 막는다) 다 차고 같은 안내");

                Do(() => { hideWarn(); keep(5); });
                Do(() => load(2));
                Wait(() => done, 30f, what + " — 2장 읽기");
                Check(() => { log.Add("  덜 고름: " + state()); return subs().Count == 7 && warnText() == ""; },
                    what + ": 칸이 남게 고르면 안내 없음");

                Do(() => load(3));
                Wait(() => done, 30f, what + " — 다시 채우기");
                Do(hideWarn);
                Do(() => ((Button)fv("subPhotosButton")).onClick.Invoke());
                Sleep(0.6f);
                Check(() => { log.Add("  다 찬 뒤 +: " + state() + " · 고르기 창 " + (dialogOpen() ? "뜸" : "안 뜸")); return subs().Count == 10 && !dialogOpen() && warnText() == maxText() && !(bool)fv("isProcessing"); },
                    what + ": 10장이 다 찬 뒤 + 를 누르면 고르기 창 없이 바로 안내");
                Shot("subcap_full_" + tag);

                if (hasDialog)
                {
                    Do(hideWarn);
                    Do(() => cur.StartCoroutine((IEnumerator)call("SelectSubPhotosFromGallery", null)));
                    Sleep(0.2f);
                    Check(() => warnText() == maxText() && !(bool)fv("isProcessing") && subs().Count == 10,
                        what + ": 다 찬 채 갤러리를 바로 불러도 고르기 창 없이 안내만");
                }

                Do(() => { hideWarn(); keep(9); });
                Do(() => ((Button)fv("subPhotosButton")).onClick.Invoke());
                Sleep(0.8f);
                if (hasDialog)
                {
                    Check(() => dialogOpen() && warnText() == "", what + ": 한 칸 남으면 + 는 예전처럼 촬영·갤러리 창");
                    Shot("subcap_dialog_" + tag);
                    Do(() => typeof(PhotoSourceDialog).GetMethod("OnCancelButtonClicked", F).Invoke(cur.GetType().GetField("photoSourceDialog", F).GetValue(cur), null));
                    Sleep(0.7f);
                    Check(() => !dialogOpen(), what + ": 취소로 닫힘");
                }
                else   // 에디터의 갤러리는 아무것도 돌려주지 않는다 — 안내 없이 그대로 · 칸이 풀려 있다
                    Check(() => warnText() == "" && subs().Count == 9 && !(bool)fv("isProcessing"), what + ": 한 칸 남으면 + 는 예전처럼 갤러리 (막지 않음)");
                Do(() => { keep(0); hideWarn(); });
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
                Application.logMessageReceived += onLog;
                Directory.CreateDirectory(dir);
                files.Clear();
                for (int i = 0; i < 12; i++)   // 색이 다른 작은 사진 12장
                {
                    var t = new Texture2D(64, 64, TextureFormat.RGB24, false);
                    var px = new Color[64 * 64];
                    var c = Color.HSVToRGB(i / 12f, 0.6f, 0.9f);
                    for (int k = 0; k < px.Length; k++) px[k] = c;
                    t.SetPixels(px);
                    t.Apply();
                    string p = Path.Combine(dir, "sub" + i + ".jpg");
                    File.WriteAllBytes(p, t.EncodeToJPG(85));
                    UnityEngine.Object.Destroy(t);
                    files.Add(p);
                }
            });

            // 1) 장소 수정 — 시험 서버의 장소 #3 을 수정 요청 창으로 (fixmove 와 같은 차례)
            CubeDataFixManager fix = null;
            Do(() =>
            {
                CubeDataFixManager.EditorBase = FixDev;
                fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                cur = fix;
                ((GameObject)fv("fixUIPanel")).SetActive(true);
                var T = typeof(CubeDataFixManager);
                T.GetField("doubleTap", F).SetValue(fix, null);
                int gen = (int)fv("loadGen") + 1;
                T.GetField("loadGen", F).SetValue(fix, gen);
                call("ResetToInitialState", null);
                T.GetField("editId", F).SetValue(fix, 3);
                call("SetOwnerMode", new object[] { false });
                var pd = new PlaceData { id = 3, name = "시험 장소", main_photo = "uploads/seed_3/main.jpg", sub_photos = new List<List<string>>() };
                fix.StartCoroutine((IEnumerator)call("LoadPlace", new object[] { pd, gen }));
            });
            Wait(() => fix != null && !(bool)fv("isProcessing") && fv("cleanPhotos") != null, 40f, "장소 #3 수정 창");
            Check(() => fix != null && fix.isActiveAndEnabled && fv("photoSourceDialog") != null && fv("subPhotosButton") != null && fv("warningObj") != null,
                "준비: 장소 수정 창 · 추가 사진 + · 촬영/갤러리 창 · 안내 연결");
            sequence("장소 수정", "fix", true);
            Do(() =>
            {
                var p = (GameObject)fv("fixUIPanel");
                if (p != null && p.activeSelf) { p.SetActive(false); call("ResetToInitialState", null); }
                CubeDataFixManager.EditorBase = null;
            });
            Sleep(0.5f);

            // 2) 장소 올리기 — 도크 + 로 연다
            CubeUploadManager up = null;
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            Do(() =>
            {
                up = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                cur = up;
            });
            Check(() => up != null && up.isActiveAndEnabled && !(bool)fv("isProcessing") && fv("photoSourceDialog") != null && fv("subPhotosButton") != null && fv("warningObj") != null,
                "준비: 장소 올리기 창 · 추가 사진 + · 촬영/갤러리 창 · 안내 연결");
            sequence("장소 올리기", "upload", true);

            // 3) 3D 모델 올리기 — 같은 올리기 창의 옆 장
            ModelUploadManager mum = null;
            Do(() =>
            {
                mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                cur = mum;
            });
            Check(() => mum != null && mum.isActiveAndEnabled && !(bool)fv("isProcessing") && fv("subPhotosButton") != null && fv("warningObj") != null,
                "준비: 3D 모델 올리기 · 추가 사진 + · 안내 연결");
            sequence("3D 모델 올리기", "model", false);
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(0.8f);

            Do(() =>
            {
                Application.logMessageReceived -= onLog;
                foreach (var e in errors) log.Add("  오류: " + e);
                try { Directory.Delete(dir, true); } catch (Exception) { }
            });
            Check(() => errors.Count == 0, "오류 없음 (사진 읽기 · 안내 · 촬영/갤러리 창)");
        }
    }
}
