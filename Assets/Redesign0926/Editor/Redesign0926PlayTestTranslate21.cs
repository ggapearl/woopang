using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 21번 대표님 「캡쳐해서 번역기로 넘어갈 때 해당 화면 자체를 다 캡쳐하지말고 서브사진만 다운받아서 넘어가도록 해줘. 그래야 번역이 더 깔끔하게
        //  나올 것 같아. 현재는 화면캡쳐를 하도록 했는데 캡쳐하는 것처럼 지금 로직은 유지하고, 서브사진만 저장되도록 하면 될 것 같아」
        //  · 번역 버튼(TranslationButton → CaptureAndTranslate) — 확대 · 번쩍임은 그대로, 저장하는 건 보고 있는 사진 한 장
        //  · 우팡 서버 사진: 원본(창엔 줄인 1080) → 원본이 없거나(404) 사진이 아니면(HTML) 디스크에 받아 둔 것 → 캐시가 모르는 그림은 띄운 그림을 JPG 로
        //  · 사진 없는 정보 글 쪽은 예전처럼 화면을 PNG 로
        //  가짜 사진 서버 127.0.0.1:5798 (photo21_stub.py) · 사진첩 저장 · 브라우저 열기는 시험 훅으로 받는다 (에디터 NativeGallery 는 파일을 쓰지 않는다 · 브라우저는 열지 않는다)
        private const string T21Base = "http://127.0.0.1:5798";
        private static bool t21Run;
        private static readonly List<UnityEngine.Object> t21Made = new List<UnityEngine.Object>();

        // 시험이 중간에 끊겨도 — 사진 서버 주소 · 시험 훅 · 만든 그림
        private static void T21Finish()
        {
            if (!t21Run) return;
            t21Run = false;
            SubPhotoCache.EditorBase = null;
            CaptureAndTranslate.EditorSaved = null;
            CaptureAndTranslate.EditorOpen = null;
            foreach (var o in t21Made) if (o != null) UnityEngine.Object.Destroy(o);
            t21Made.Clear();
        }

        private static void PlanTranslate21()
        {
            const string G = "FullScreenPanel/FullScreenGuide/";
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo fIndex = typeof(DoubleTap3D).GetField("imageIndex", NP);
            DoubleTap3D host = null;
            Button btn = null;
            CaptureAndTranslate cat = null;
            string run = DateTime.Now.ToString("HHmmssfff");   // 주소가 매번 달라 디스크 캐시에 남은 옛 시험 사진을 쓰지 않는다
            string A = "uploads/sub/t21a_" + run + ".jpg", B = "uploads/sub/t21b_" + run + ".jpg", C = "uploads/sub/t21c_" + run + ".jpg";
            var want = new Dictionary<string, byte[]>();
            var reqs = new Dictionary<string, UnityWebRequestAsyncOperation>();
            byte[] saved = null;
            string savedName = null, opened = null;
            int saves = 0, opens = 0;
            bool zoomSeen = false, flashSeen = false;
            Vector3 scaleWas = Vector3.one;
            float clickAt = 0f, savedAt = 0f;
            Sprite fake = null;

            int Index() => host != null && fIndex?.GetValue(host) is int i ? i : -99;
            bool Open() => host != null && host.fullscreenCanvasGroup != null && host.fullscreenCanvasGroup.gameObject.activeSelf && DoubleTap3D.Shown == host;
            bool Closed() => host == null || host.fullscreenCanvasGroup == null || !host.fullscreenCanvasGroup.gameObject.activeSelf;
            Sprite Big() { var img = host != null ? host.fullscreenImage : null; return img != null && img.enabled ? img.sprite : null; }
            Sprite Slot(int k) => host != null && k < host.imageSprites.Count ? host.imageSprites[k] : null;
            byte[] W(string k) => want.TryGetValue(k, out var b) ? b : null;
            string Len(string k) => want.TryGetValue(k, out var b) ? b.Length.ToString() : "없음";
            bool Same(byte[] a, byte[] b)
            {
                if (a == null || b == null || a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
                return true;
            }
            string Kind(byte[] b) => b == null || b.Length < 4 ? "없음" : b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF ? "JPG" : b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E ? "PNG" : "다른 것";
            string Dims(byte[] b)
            {
                if (b == null) return "-";
                var t = new Texture2D(2, 2);
                string s = t.LoadImage(b) ? t.width + "x" + t.height : "못 풂";
                UnityEngine.Object.Destroy(t);
                return s;
            }
            string Saved() => $"{savedName} · {Kind(saved)} {Dims(saved)} · {(saved != null ? saved.Length : 0)} 바이트 · 누른 뒤 {Mathf.Max(0f, savedAt - clickAt):0.00}초";
            void Watch()
            {
                if (cat != null && cat.captureArea != null && cat.captureArea.localScale.x > scaleWas.x * 1.02f) zoomSeen = true;
                if (cat != null && cat.flashImage != null && cat.flashImage.gameObject.activeInHierarchy && cat.flashImage.color.a > 0.5f) flashSeen = true;
            }
            void Press(int n, bool twice = false)
            {
                Do(() =>
                {
                    scaleWas = cat.captureArea.localScale;
                    zoomSeen = flashSeen = false;
                    savedAt = 0f;
                    clickAt = Time.unscaledTime;
                    btn.onClick.Invoke();
                    if (twice) btn.onClick.Invoke();   // 연달아 두 번
                });
                Wait(() =>
                {
                    Watch();
                    if (saves >= n && savedAt == 0f) savedAt = Time.unscaledTime;
                    return saves >= n && opens >= n;
                }, 15f, "번역 버튼 " + n + "번째 — 저장 · 번역 열기");
            }

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                t21Run = true;
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                host = R0926PlaceInfo.Host();
                SubPhotoCache.EditorBase = T21Base;
                CaptureAndTranslate.EditorSaved = (b, n) => { saved = b; savedName = n; saves++; };
                CaptureAndTranslate.EditorOpen = u => { opened = u; opens++; };
                var paths = new Dictionary<string, string> { { "a_orig", "/" + A }, { "a_thumb", "/thumb/1080/" + A }, { "b_thumb", "/thumb/1080/" + B }, { "c_thumb", "/thumb/1080/" + C } };
                foreach (var kv in paths) reqs[kv.Key] = UnityWebRequest.Get(T21Base + kv.Value).SendWebRequest();
                var t = Find(G + "TopUIPanel/TranslationButton");
                btn = t != null ? t.GetComponent<Button>() : null;
                cat = t != null ? t.GetComponent<CaptureAndTranslate>() : null;
            });
            Wait(() => host != null && host.IsReady, 10f, "대리 정보 창 준비");
            Wait(() => { foreach (var r in reqs.Values) if (!r.isDone) return false; return true; }, 6f, "시험 서버에서 맞춰 볼 파일 받기");
            Check(() =>
            {
                foreach (var kv in reqs)
                {
                    var rq = kv.Value.webRequest;
                    if (rq.result == UnityWebRequest.Result.Success) want[kv.Key] = rq.downloadHandler.data;
                    rq.Dispose();
                }
                reqs.Clear();
                log.Add($"  시험 사진: 원본 {Len("a_orig")} · 줄인 것 A {Len("a_thumb")} · B {Len("b_thumb")} · C {Len("c_thumb")} 바이트");
                return want.Count == 4 && !Same(W("a_orig"), W("a_thumb"));
            }, "준비: 가짜 사진 서버 5798 (원본 · 줄인 것이 서로 다른 파일)");
            Check(() =>
            {
                int n = btn != null ? btn.onClick.GetPersistentEventCount() : 0;
                bool wired = false;
                for (int i = 0; i < n; i++)
                    if (btn.onClick.GetPersistentTarget(i) == cat && btn.onClick.GetPersistentMethodName(i) == "OnTranslateButtonClick") wired = true;
                log.Add($"  번역 버튼 {(btn != null ? "있음" : "없음")} · 연결 {(wired ? "OnTranslateButtonClick" : "없음")} · 찍는 칸 {(cat != null && cat.captureArea != null ? cat.captureArea.name : "없음")} · 번쩍임 {(cat != null && cat.flashImage != null ? cat.flashImage.name : "없음")}");
                return wired && cat.captureArea != null && host.fullscreenImage != null && cat.captureArea == host.fullscreenImage.rectTransform && cat.flashImage != null;
            }, "번역 버튼 → CaptureAndTranslate.OnTranslateButtonClick · 찍는 칸 = 큰 사진(FullScreenImage) · 번쩍임 연결");

            // 1) 우팡 장소 — 상세 사진 3장 (A: 원본 있음 · B: 원본 404 · C: 원본 자리에 HTML)
            Do(() =>
            {
                var p = new PlaceData { id = 990421, name = "번역 시험", username = "tester21", category = "shop", model_type = "cube" };
                bool filled = R0926PlaceInfo.Fill(host, p);
                host.SetImageUrls(new List<string> { A, B, C }, false);   // 우팡 서버에서 찾지 않는다
                if (!(filled && host.OpenInfo())) log.Add("  정보 창을 열지 못함");
            });
            Wait(() => Open() && host.imageSprites.Count == 3 && Big() != null && Big() == Slot(0), 12f, "우팡 장소 정보 창 · 상세 사진 3장 받음");
            Sleep(0.6f);
            Shot("t21_photo_a");
            Check(() =>
            {
                var s = Big();
                log.Add($"  첫 장 {(s != null ? s.texture.width + "x" + s.texture.height : "없음")} · 사진 번호 {Index()}");
                return s != null && s.texture.width == 1080 && s.texture.height == 1440 && Index() == 0;
            }, "창에는 줄인 사진(1080x1440)이 뜬다 — 첫 상세 사진");
            Check(() =>
            {
                bool okA = SubPhotoCache.Source(Slot(0) != null ? Slot(0).texture : null, out var oa, out var ca);
                bool okOther = SubPhotoCache.Source(Texture2D.whiteTexture, out _, out _);
                bool file = ca != null && System.IO.File.Exists(ca);
                log.Add($"  사진 캐시: 원본 주소 {(oa == T21Base + "/" + A ? "서버 원본(/uploads/…)" : oa == null ? "없음" : "다른 주소")} · 받아 둔 파일 {(file ? "있음" : "없음")} · 모르는 그림 {(okOther ? "안다고 함" : "모름")}");
                return okA && oa == T21Base + "/" + A && file && !okOther;
            }, "사진 캐시가 띄운 사진의 원본 주소(줄인 /thumb/1080/ 아님)와 받아 둔 파일을 안다 · 모르는 그림은 모른다");

            Press(1);
            Check(() =>
            {
                log.Add("  저장: " + Saved() + $" · 확대 {(zoomSeen ? "보임" : "안 보임")} · 번쩍임 {(flashSeen ? "보임" : "안 보임")}");
                return Same(saved, W("a_orig")) && savedName != null && savedName.StartsWith("Woopang_") && savedName.EndsWith(".jpg");
            }, "첫 장 — 저장한 파일 = 서버 원본 그대로(1600x2134 · 창의 줄인 것 · 화면 캡처 아님) · Woopang_….jpg");
            Check(() => zoomSeen && flashSeen && cat.captureArea.localScale == scaleWas, "찍는 느낌은 그대로 — 사진이 살짝 커졌다 돌아오고(1.1배) 번쩍인다");
            Check(() =>
            {
                log.Add($"  번역 주소 {opened}");
                return saves == 1 && opens == 1 && opened != null && opened.StartsWith("https://translate.google.com/?sl=auto&tl=") && opened.EndsWith("&op=images");
            }, "저장 한 번 · 구글 번역(이미지) 한 번 연다");

            // 2) 둘째 장 — 원본이 없다(404) → 받아 둔 사진 · 연달아 두 번 눌러도 한 번
            Do(() => host.ShowNextImage());
            Wait(() => Index() == 1 && Big() != null && Big() == Slot(1), 3f, "둘째 사진으로 넘김");
            Sleep(0.5f);
            Press(2, true);
            Sleep(1.5f);
            Check(() =>
            {
                log.Add("  저장: " + Saved() + $" · 저장 {saves}번 · 열기 {opens}번");
                return Same(saved, W("b_thumb")) && savedName != null && savedName.EndsWith(".jpg") && saves == 2 && opens == 2;
            }, "둘째 장(원본 404) — 디스크에 받아 둔 사진 그대로(1080x1440) · 연달아 두 번 눌러도 한 번만 저장 · 한 번만 연다");

            // 3) 셋째 장 — 원본 자리에 오류 글(HTML) → 사진첩에 넣지 않고 받아 둔 사진
            Do(() => host.ShowNextImage());
            Wait(() => Index() == 2 && Big() != null && Big() == Slot(2), 3f, "셋째 사진으로 넘김");
            Sleep(0.5f);
            Press(3);
            Check(() =>
            {
                log.Add("  저장: " + Saved());
                return Same(saved, W("c_thumb")) && savedName != null && savedName.EndsWith(".jpg");
            }, "셋째 장(원본 자리에 오류 글 HTML) — HTML 을 저장하지 않고 받아 둔 사진 그대로");

            // 4) 캐시가 모르는 그림 (읽기 전용) — 띄운 그림 그대로 JPG
            Do(() =>
            {
                var tex = new Texture2D(640, 480, TextureFormat.RGBA32, false);
                var px = new Color32[640 * 480];
                for (int y = 0; y < 480; y++)
                    for (int x = 0; x < 640; x++)
                        px[y * 640 + x] = x < 320 ? new Color32(220, 40, 40, 255) : new Color32(30, 60, 210, 255);
                tex.SetPixels32(px);
                tex.Apply(false, true);   // 받은 사진처럼 읽기 전용
                fake = Sprite.Create(tex, new Rect(0, 0, 640, 480), new Vector2(0.5f, 0.5f));
                t21Made.Add(tex);
                t21Made.Add(fake);
                host.fullscreenImage.sprite = fake;
                host.fullscreenImage.enabled = true;
                host.fullscreenImage.color = Color.white;
            });
            Sleep(0.3f);
            Check(() => Big() == fake && !fake.texture.isReadable, "준비: 캐시가 모르는 읽기 전용 그림(왼쪽 빨강 · 오른쪽 파랑 640x480)을 띄움");
            Press(4);
            Check(() =>
            {
                var t = new Texture2D(2, 2);
                bool ok = saved != null && t.LoadImage(saved);
                Color l = ok ? t.GetPixel(160, 240) : Color.clear, r = ok ? t.GetPixel(480, 240) : Color.clear;
                bool dims = ok && t.width == 640 && t.height == 480;
                UnityEngine.Object.Destroy(t);
                bool near(Color c, float R, float Gc, float Bc) => Mathf.Abs(c.r * 255f - R) < 12f && Mathf.Abs(c.g * 255f - Gc) < 12f && Mathf.Abs(c.b * 255f - Bc) < 12f;
                log.Add("  저장: " + Saved() + $" · 왼쪽 {(Color32)l} · 오른쪽 {(Color32)r}");
                return Kind(saved) == "JPG" && savedName.EndsWith(".jpg") && dims && near(l, 220, 40, 40) && near(r, 30, 60, 210);
            }, "캐시가 모르는 그림 — 띄운 그림 그대로 JPG(640x480 · 색 그대로 · 화면 캡처 아님)");
            Do(() => { if (Slot(2) != null) host.fullscreenImage.sprite = Slot(2); });
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => Closed(), 5f, "정보 창 닫힘");

            // 5) 사진 없는 정보 글 쪽 (역) — 예전처럼 화면을 PNG 로
            Do(() =>
            {
                var f = new FacilityData { type = "subway", name = "시험역", address = "시험 주소 1", extra_info = "시험 개요" };
                if (!(R0926PlaceInfo.Fill(host, f) && host.OpenInfo())) log.Add("  역 정보 창을 열지 못함");
            });
            Wait(() => Open() && Index() == -1, 5f, "역 정보 창 열림 (정보 글 쪽)");
            Sleep(0.7f);
            Press(5);
            Check(() =>
            {
                log.Add("  저장: " + Saved() + $" · 큰 사진 {(host.fullscreenImage.enabled ? "켜짐" : "꺼짐")} · 번쩍임 {(flashSeen ? "보임" : "안 보임")}");
                return Kind(saved) == "PNG" && savedName != null && savedName.EndsWith(".png") && saves == 5 && opens == 5 && flashSeen;
            }, "사진 없는 정보 글 쪽 — 예전처럼 화면을 찍어 PNG 로 · 번역도 연다");
            Check(() =>
            {
                // 예전 셈 — 칸의 캔버스 크기를 픽셀로 썼다
                var a = cat.captureArea;
                int w = (int)a.sizeDelta.x, h = (int)a.sizeDelta.y;
                int x = Mathf.Clamp((int)(a.position.x - w / 2), 0, Screen.width - w), y = Mathf.Clamp((int)(a.position.y - h / 2), 0, Screen.height - h);
                w = Mathf.Min(w, Screen.width - x); h = Mathf.Min(h, Screen.height - y);
                bool oldOut = x < 0 || y < 0 || x + w > Screen.width || y + h > Screen.height;
                Rect now = cat.ScreenArea();
                bool inside = now.xMin >= 0 && now.yMin >= 0 && now.xMax <= Screen.width && now.yMax <= Screen.height;
                log.Add($"  찍는 자리: 지금 {now.x:0},{now.y:0} {now.width:0}x{now.height:0} · 예전 셈 {x},{y} {w}x{h}{(oldOut ? " (화면 밖)" : "")} · 화면 {Screen.width}x{Screen.height} · 저장 {Dims(saved)}");
                return inside && Dims(saved) == $"{now.width:0}x{now.height:0}";
            }, "화면 찍기는 칸이 화면에 놓인 자리만 — 화면 밖을 읽지 않는다 (저장 크기 = 그 자리)");
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => Closed(), 5f, "정보 창 닫힘");

            // 되돌리기 — 채운 값 · 사진 서버 주소 · 시험 훅 · 만든 그림
            Do(() =>
            {
                if (host != null)
                {
                    host.ResetData();
                    DataManager.Instance?.ConfigureInfo(host, null);
                }
                T21Finish();
            });
            Check(() => SubPhotoCache.EditorBase == null && CaptureAndTranslate.EditorSaved == null && CaptureAndTranslate.EditorOpen == null && t21Made.Count == 0,
                "되돌림 — 사진 서버 주소 · 시험 훅 · 만든 그림 처음대로");
        }
    }
}
