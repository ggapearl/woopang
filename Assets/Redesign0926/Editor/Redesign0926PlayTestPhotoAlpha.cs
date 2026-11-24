using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-24 대표님 「투명PNG 에 있어서는 검은색으로 채우지말고 알파값 빼서, 투명하도록」 — 근처 장소 카드의 동그란 로고 둘레가 검게 나왔다.
        //  자르기(RGB24) · 줄이기(RGB24) · 올리기(JPG)가 투명한 곳을 검게 굳혔다 → 투명한 화소가 있을 때만 RGBA32 로 남기고 PNG 로 올린다 (R0926PhotoAlpha).
        //  여느 사진(JPG · 투명한 곳 없는 PNG)은 예전 그대로 RGB24 → JPG. 서버에는 보내지 않는다 (폼만 만들어 본다).
        private static void PlanPhotoAlpha()
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            CubeUploadManager cum = null;
            CubeDataFixManager fix = null;
            ModelUploadManager mum = null;
            R0926PhotoCrop crop = null;
            ARPreviewController prev = null;
            bool completed = false, downDone = false;
            Texture2D down = null;
            string downErr = null;
            var made = new List<UnityEngine.Object>();

            // 동그란 로고 — 가운데 주황 원 · 바깥은 투명(clear) 또는 흰 바탕
            Func<int, int, bool, TextureFormat, Texture2D> logo = (w, h, clear, fmt) =>
            {
                var t = new Texture2D(w, h, fmt, false);
                var px = new Color32[w * h];
                float r = Mathf.Min(w, h) * 0.4f, cx = w * 0.5f, cy = h * 0.5f;
                Color32 fill = new Color32(255, 140, 0, 255), outside = clear ? new Color32(0, 0, 0, 0) : new Color32(255, 255, 255, 255);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        px[y * w + x] = dx * dx + dy * dy <= r * r ? fill : outside;
                    }
                t.SetPixels32(px);
                t.Apply();
                made.Add(t);
                return t;
            };
            // 읽기 전용이어도 — GPU 로 옮겨 RGBA32 로 읽는다
            Func<Texture, Color32[]> read = t =>
            {
                var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32);
                var was = RenderTexture.active;
                Graphics.Blit(t, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0, false);
                copy.Apply();
                RenderTexture.active = was;
                RenderTexture.ReleaseTemporary(rt);
                var px = copy.GetPixels32();
                UnityEngine.Object.Destroy(copy);
                return px;
            };
            // 네 귀 중 가장 진한 화소 · 가운데 화소
            Func<Texture, (byte a, Color32 edge, Color32 mid)> probe = t =>
            {
                var px = read(t);
                int w = t.width, h = t.height;
                byte a = 0;
                Color32 edge = default;
                foreach (var (fx, fy) in new[] { (0.03f, 0.03f), (0.97f, 0.03f), (0.03f, 0.97f), (0.97f, 0.97f) })
                {
                    var c = px[Mathf.Clamp((int)(fy * h), 0, h - 1) * w + Mathf.Clamp((int)(fx * w), 0, w - 1)];
                    if (c.a >= a) { a = c.a; edge = c; }
                }
                return (a, edge, px[(h / 2) * w + w / 2]);
            };
            Func<Color32, bool> orange = c => c.a > 245 && c.r > 235 && c.g > 115 && c.g < 165 && c.b < 25;
            Func<Texture, string> info = t =>
            {
                if (t == null) return "없음";
                var p = probe(t);
                string f = t is Texture2D t2 ? t2.format.ToString() : t.graphicsFormat.ToString();
                return $"{t.width}x{t.height} {f} · 귀 ({p.edge.r},{p.edge.g},{p.edge.b},{p.edge.a}) · 가운데 ({p.mid.r},{p.mid.g},{p.mid.b},{p.mid.a})";
            };
            Func<Texture, bool> clearLogo = t => { if (t == null) return false; var p = probe(t); return p.a < 8 && orange(p.mid); };
            Func<Texture, bool> whiteLogo = t =>
            {
                if (t == null) return false;
                var p = probe(t);
                return p.edge.a == 255 && p.edge.r > 245 && p.edge.g > 245 && p.edge.b > 245 && orange(p.mid);
            };
            Func<byte[], bool> isPng = b => b != null && b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
            Func<byte[], bool> isJpg = b => b != null && b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
            Func<byte[], string> kind = b => isPng(b) ? "PNG" : isJpg(b) ? "JPG" : "?";
            Func<byte[], Texture2D> decode = b =>
            {
                if (b == null) return null;
                var t = new Texture2D(2, 2);
                made.Add(t);
                return t.LoadImage(b) ? t : null;
            };
            // 추가 사진 줄이기 (800 안으로) — 세 곳의 같은 이름 함수
            Func<MonoBehaviour, Texture2D, Texture2D> shrink = (m, src) =>
            {
                var r = (Texture2D)m.GetType().GetMethod("ResizeTextureKeepAspectWithRenderTexture", F).Invoke(m, new object[] { src, 800, 800 });
                if (r != null) made.Add(r);
                return r;
            };
            Func<byte[], string> latin = d =>
            {
                var ch = new char[d.Length];
                for (int i = 0; i < d.Length; i++) ch[i] = (char)d[i];
                return new string(ch);
            };
            // 폼(multipart)에서 그 파일 칸의 Content-Type · 내용
            Func<byte[], string, (string mime, byte[] body)> part = (d, name) =>
            {
                string s = latin(d);
                int i = s.IndexOf("; filename=\"" + name + "\"", StringComparison.Ordinal);
                if (i < 0 || !s.StartsWith("\r\n--", StringComparison.Ordinal)) return (null, null);
                string bound = "\r\n--" + s.Substring(4, s.IndexOf("\r\n", 4, StringComparison.Ordinal) - 4);
                int ct = s.LastIndexOf("Content-Type: ", i, StringComparison.Ordinal);
                string mime = ct < 0 ? null : s.Substring(ct + 14, s.IndexOf("\r\n", ct, StringComparison.Ordinal) - ct - 14);
                int start = s.IndexOf("\r\n\r\n", i, StringComparison.Ordinal) + 4;
                int end = start < 4 ? -1 : s.IndexOf(bound, start, StringComparison.Ordinal);
                if (end < start) return (mime, null);
                var body = new byte[end - start];
                Array.Copy(d, start, body, 0, body.Length);
                return (mime, body);
            };
            Func<Texture2D> mainPhoto = () => typeof(CubeUploadManager).GetField("mainPhoto", F).GetValue(cum) as Texture2D;
            Func<bool> mainAlpha = () => (bool)typeof(CubeUploadManager).GetField("mainPhotoAlpha", F).GetValue(cum);
            Func<RectTransform[]> tiles = () => (RectTransform[])typeof(R0926PhotoCrop).GetField("tiles", F).GetValue(crop);
            Func<bool> cubeReady = () => prev != null && (bool)typeof(ARPreviewController).GetField("isCubeReady", F).GetValue(prev);

            // 장소 추가에서 대표 사진을 고른다 — 갤러리 PNG 는 RGBA32 로 읽힌다 (NativeGallery: .jpg · .jpeg 만 RGB24)
            Action<bool, string> pick = (clear, tag) =>
            {
                Do(() =>
                {
                    completed = false;
                    typeof(CubeUploadManager).GetMethod("ProcessCropAndDisplay", F).Invoke(cum, new object[] { logo(600, 400, clear, TextureFormat.RGBA32), (Action)(() => completed = true) });
                });
                Sleep(0.6f);
                if (clear) Shot("pa_crop_open");
                Check(() => crop.IsOpen, $"[{tag}] 대표 사진을 고르면 자르기 화면");
                Do(() => tiles()[2].GetComponent<Button>().onClick.Invoke());   // 완료
                Wait(() => completed, 5f, "자르기 끝");
                Sleep(0.4f);
                Check(() =>
                {
                    var mp = mainPhoto();
                    log.Add($"  [{tag}] 자른 대표 사진 {info(mp)} · PNG 로 올림 {mainAlpha()}");
                    return clear ? mp != null && mp.format == TextureFormat.RGBA32 && mainAlpha() && clearLogo(mp)
                                 : mp != null && mp.format == TextureFormat.RGB24 && !mainAlpha() && whiteLogo(mp);
                }, clear ? "[투명 PNG] 자른 대표 사진도 둘레가 투명 (RGBA32) · PNG 로 올린다 표시" : "[흰 바탕 PNG] 예전처럼 RGB24 · JPG 로 올린다");
                Check(() =>
                {
                    // 올리기(UploadCube)와 같은 줄이기 · 같은 갈래
                    var r = (Texture2D)typeof(CubeUploadManager).GetMethod("ResizeTextureWithRenderTexture", F).Invoke(cum, new object[] { mainPhoto(), 444, 444, mainAlpha() });
                    made.Add(r);
                    byte[] b = mainAlpha() ? r.EncodeToPNG() : r.EncodeToJPG(50);
                    var back = decode(b);
                    log.Add($"  [{tag}] 올릴 대표 사진 → {kind(b)} {b.Length / 1024f:0.0}KB · 다시 읽음 {info(back)}");
                    return clear ? isPng(b) && clearLogo(back) : isJpg(b) && whiteLogo(back);
                }, clear ? "[투명 PNG] 올릴 대표 사진(444) — PNG · 둘레 투명 그대로" : "[흰 바탕 PNG] 올릴 대표 사진(444) — JPG 그대로");
                if (clear)
                {
                    Wait(() => cubeReady(), 8f, "AR 미리보기 큐브 다 생김");
                    Sleep(0.6f);
                    Shot("pa_preview");
                    Check(() =>
                    {
                        var cube = prev != null ? typeof(ARPreviewController).GetField("spawnedCube", F).GetValue(prev) as GameObject : null;
                        var mr = cube != null ? cube.GetComponentInChildren<MeshRenderer>() : null;
                        var m = mr != null ? mr.sharedMaterial : null;
                        var t = m != null && m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        log.Add($"  미리보기 큐브: {(m != null ? m.shader.name + " · 그리는 순서 " + m.renderQueue : "재질 없음")} · 사진 {info(t)}");
                        return t != null && t == mainPhoto() && m.renderQueue >= 2500 && clearLogo(t);
                    }, "[투명 PNG] AR 미리보기 큐브 — 투명한 대표 사진 그대로 (투명 셰이더라 둘레가 비친다)");
                }
                Do(() => { if (Active("ARPreviewPanel")) Click("ARPreviewPanel/MessageContainer/ButtonContainer/CancelButton"); });
                Sleep(1.2f);
                Check(() => Active("UploadPage") && !crop.IsOpen, $"[{tag}] 미리보기를 닫으면 장소 추가 창으로");
            };

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() =>
            {
                if (Active("LoginPromptPanel")) BackNow();
                var g = UnityEngine.Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) UnityEngine.Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                cum = UnityEngine.Object.FindAnyObjectByType<CubeUploadManager>(FindObjectsInactive.Include);
                fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                mum = UnityEngine.Object.FindAnyObjectByType<ModelUploadManager>(FindObjectsInactive.Include);
                crop = UnityEngine.Object.FindAnyObjectByType<R0926PhotoCrop>(FindObjectsInactive.Include);
                prev = UnityEngine.Object.FindAnyObjectByType<ARPreviewController>(FindObjectsInactive.Include);
            });
            Sleep(0.6f);
            Check(() => cum != null && fix != null && mum != null && crop != null && prev != null, "준비: 장소 추가 · 정보 수정 · 3D 모델 올리기 · 자르기 화면 · AR 미리보기");

            // 1) 줄이기 — 추가 사진 (장소 추가 · 정보 수정 · 3D 모델)
            Check(() =>
            {
                var clear = logo(600, 400, true, TextureFormat.RGBA32);
                var white = logo(600, 400, false, TextureFormat.RGBA32);
                var jpg = logo(600, 400, false, TextureFormat.RGB24);
                bool ok = R0926PhotoAlpha.CanHave(clear) && R0926PhotoAlpha.CanHave(white) && !R0926PhotoAlpha.CanHave(jpg);
                foreach (var m in new MonoBehaviour[] { cum, fix, mum })
                {
                    Texture2D a = shrink(m, clear), b = shrink(m, white), c = shrink(m, jpg);
                    log.Add($"  {m.GetType().Name}: 투명 PNG → {info(a)} | 흰 바탕 PNG → {info(b)} | JPG → {info(c)}");
                    ok &= a != null && a.format == TextureFormat.RGBA32 && a.width == 600 && a.height == 400 && clearLogo(a)
                        && b != null && b.format == TextureFormat.RGB24 && whiteLogo(b)
                        && c != null && c.format == TextureFormat.RGB24 && whiteLogo(c);
                }
                return ok;
            }, "줄이기: 투명한 PNG 는 RGBA32 로 투명한 그대로 · 흰 바탕 PNG 와 JPG 는 예전처럼 RGB24 (장소 추가 · 정보 수정 · 3D 모델)");
            Check(() =>
            {
                var big = logo(1600, 1000, true, TextureFormat.RGBA32);
                big.Apply(false, true);   // 정보 수정은 갤러리 사진을 읽기 전용으로 불러온다
                var r = shrink(fix, big);
                log.Add($"  1600x1000 읽기 전용 투명 PNG → {info(r)}");
                return r != null && r.format == TextureFormat.RGBA32 && r.width == 800 && r.height == 500 && clearLogo(r);
            }, "큰 사진을 줄여도 · 읽기 전용이어도 투명한 그대로 (1600x1000 → 800x500)");

            // 2) 올릴 바이트 — 투명하면 PNG(다시 읽어도 투명) · 아니면 JPG
            Check(() =>
            {
                var a = shrink(cum, logo(600, 400, true, TextureFormat.RGBA32));
                var b = shrink(cum, logo(600, 400, false, TextureFormat.RGBA32));
                byte[] pa = R0926PhotoAlpha.Encode(a, 50), pb = R0926PhotoAlpha.Encode(b, 50);
                var back = decode(pa);
                log.Add($"  투명 → {kind(pa)} {pa.Length / 1024f:0.0}KB · 다시 읽음 {info(back)} | 흰 바탕 → {kind(pb)} {pb.Length / 1024f:0.0}KB");
                return isPng(pa) && isJpg(pb) && clearLogo(back);
            }, "올리기: 투명한 사진은 PNG (다시 읽어도 둘레가 투명) · 여느 사진은 JPG 그대로");

            // 3) 받기 — 서버는 이름이 main.jpg 여도 받은 내용(PNG)을 그대로 준다 · 앱(근처 장소 카드 · 목록 · AR 큐브)은 내용을 보고 읽는다
            Do(() =>
            {
                var a = shrink(cum, logo(600, 400, true, TextureFormat.RGBA32));
                string path = Path.Combine(Application.temporaryCachePath, "pa_main.jpg");
                File.WriteAllBytes(path, R0926PhotoAlpha.Encode(a, 50));
                var req = UnityWebRequestTexture.GetTexture("file:///" + path.Replace('\\', '/').TrimStart('/'), true);   // 카드 · 목록과 같게 읽기 전용
                req.SendWebRequest().completed += _ =>
                {
                    if (req.result == UnityWebRequest.Result.Success) { down = DownloadHandlerTexture.GetContent(req); made.Add(down); }
                    else downErr = req.error;
                    req.Dispose();
                    try { File.Delete(path); } catch (Exception) { }
                    downDone = true;
                };
            });
            Wait(() => downDone, 5f, "받기 끝");
            Check(() =>
            {
                log.Add($"  main.jpg(PNG 내용) 받음 → {(down != null ? info(down) : "실패 " + downErr)}");
                return down != null && R0926PhotoAlpha.CanHave(down) && clearLogo(down);
            }, "받기: 이름이 .jpg 여도 내용이 PNG 면 투명한 그대로 (근처 장소 카드 · 목록 · AR 큐브가 쓰는 UnityWebRequestTexture)");

            // 4) 장소 추가 — 투명한 PNG 로고 · 흰 바탕 PNG 를 대표 사진으로
            Do(() => Click("Dock0926/PlusButton"));
            Sleep(1.2f);
            pick(true, "투명 PNG");
            pick(false, "흰 바탕 PNG");
            Do(() => Click("UploadPage/DockMirror0926/XButton_Upload"));
            Sleep(1.0f);

            // 5) 정보 수정 — 고친 사진을 폼에 담는 곳 (보내지는 않는다 · 끝나면 원래 값으로)
            Check(() =>
            {
                var T = typeof(CubeDataFixManager);
                System.Reflection.FieldInfo fMain = T.GetField("mainPhoto", F), fKeep = T.GetField("mainKeepPath", F), fAiOn = T.GetField("aiOn", F), fAiAlpha = T.GetField("aiAlpha", F);
                var subs = (List<Texture2D>)T.GetField("subPhotos", F).GetValue(fix);
                var keeps = (List<string>)T.GetField("subKeepPaths", F).GetValue(fix);
                var append = T.GetMethod("AppendPhotos", F);
                object wasMain = fMain.GetValue(fix), wasKeep = fKeep.GetValue(fix), wasAiOn = fAiOn.GetValue(fix), wasAiAlpha = fAiAlpha.GetValue(fix);
                var wasSubs = new List<Texture2D>(subs);
                var wasKeeps = new List<string>(keeps);
                Func<Texture2D, bool, bool, List<Texture2D>, WWWForm> fill = (main, aiOn, aiAlpha, add) =>
                {
                    fMain.SetValue(fix, main);
                    fKeep.SetValue(fix, null);
                    fAiOn.SetValue(fix, aiOn);
                    fAiAlpha.SetValue(fix, aiAlpha);
                    subs.Clear();
                    keeps.Clear();
                    foreach (var s in add) { subs.Add(s); keeps.Add(null); }
                    var form = new WWWForm();
                    return (bool)append.Invoke(fix, new object[] { form }) ? form : null;
                };
                try
                {
                    // 자른 대표 사진(읽기 전용 RGBA32) · 갤러리 추가 사진(읽기 전용 — 투명 PNG · JPG)
                    var cropped = logo(400, 400, true, TextureFormat.RGBA32); cropped.Apply(false, true);
                    var subClear = logo(600, 400, true, TextureFormat.RGBA32); subClear.Apply(false, true);
                    var subJpg = logo(600, 400, false, TextureFormat.RGB24); subJpg.Apply(false, true);
                    var plain = logo(400, 400, false, TextureFormat.RGB24); plain.Apply(false, true);

                    var f1 = fill(cropped, false, false, new List<Texture2D> { subClear, subJpg });
                    var main1 = f1 != null ? part(f1.data, "main.png") : (null, null);
                    var sub1 = f1 != null ? part(f1.data, "sub_1.png") : (null, null);
                    var sub2 = f1 != null ? part(f1.data, "sub_2.jpg") : (null, null);
                    Texture2D m1 = decode(main1.body), s1 = decode(sub1.body);
                    bool plan = f1 != null && latin(f1.data).Contains("[\"new:0\",\"new:1\"]");
                    log.Add($"  투명 대표 + 추가(투명 · JPG): main.png {main1.mime ?? "없음"} {kind(main1.body)} → {info(m1)} | sub_1.png {sub1.mime ?? "없음"} {kind(sub1.body)} → {info(s1)} | sub_2.jpg {sub2.mime ?? "없음"} {kind(sub2.body)} · 순서 {(plan ? "new:0 · new:1" : "?")}");
                    bool ok1 = main1.mime == "image/png" && isPng(main1.body) && clearLogo(m1) && m1.width == 444
                        && sub1.mime == "image/png" && isPng(sub1.body) && clearLogo(s1)
                        && sub2.mime == "image/jpeg" && isJpg(sub2.body) && plan;

                    var f2 = fill(plain, false, false, new List<Texture2D>());
                    var main2 = f2 != null ? part(f2.data, "main.jpg") : (null, null);
                    log.Add($"  흰 바탕(RGB24) 대표: main.jpg {main2.mime ?? "없음"} {kind(main2.body)}");
                    bool ok2 = main2.mime == "image/jpeg" && isJpg(main2.body) && f2 != null && part(f2.data, "main.png").mime == null;

                    var f3 = fill(cropped, true, false, new List<Texture2D>());
                    var f4 = fill(cropped, true, true, new List<Texture2D>());
                    var main3 = f3 != null ? part(f3.data, "main.jpg") : (null, null);
                    var main4 = f4 != null ? part(f4.data, "main.png") : (null, null);
                    log.Add($"  AI 작업 대표: 투명 아님 → main.jpg {kind(main3.body)} · 투명하게 → main.png {kind(main4.body)}");
                    bool ok3 = isJpg(main3.body) && isPng(main4.body);
                    return ok1 && ok2 && ok3;
                }
                finally
                {
                    fMain.SetValue(fix, wasMain);
                    fKeep.SetValue(fix, wasKeep);
                    fAiOn.SetValue(fix, wasAiOn);
                    fAiAlpha.SetValue(fix, wasAiAlpha);
                    subs.Clear(); subs.AddRange(wasSubs);
                    keeps.Clear(); keeps.AddRange(wasKeeps);
                }
            }, "정보 수정: 투명한 대표 · 추가 사진은 PNG(둘레 투명) · 여느 사진은 JPG · AI 작업은 예전 표시대로");

            Do(() =>
            {
                foreach (var o in made) if (o != null) UnityEngine.Object.Destroy(o);
                made.Clear();
            });
        }
    }
}
