using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-18 I1 — 정보 수정 · 장소수정 요청의 로고 칸 「AI 작업」 (대표님 「정보수정 요청, 장소정보 수정(다른 사람 요청 및 본인 요청, 우팡M 포함)
    /// 다들 각각 투명하게 만드는 거랑 AI 이용한 것들에 대한 옵션이 해당 요청에 있어서도 할 수 있도록」).
    ///  · fixai : 시험 서버(127.0.0.1:5792 · 임시 DB)의 장소 #3 을 남의 장소 수정 요청 창으로 열어
    ///            로고 칸 아래 「AI 작업」 → 그 자리에서 AI 화면 → 투명하게 → 확인 → 보내면 요청에 투명 PNG →
    ///            취소는 그대로 · ‹ 는 넣기 · 다시 열면 고른 그대로 · 같은 칸을 다시 누르면 원래 사진 → 보낼 것 없음 →
    ///            칩·미리보기를 짚고 끌어도 수정 카드가 안 닫힘 → 3D 캐릭터(요청사항으로 다시 만들기) → 요청에 model.glb →
    ///            내 장소(#200 · 시험 회원 13)는 /model 먼저 → 장소가 3D 로 · 대표 사진 투명 PNG → 떠 있는 장소는 새 모델로 다시 뜬다 →
    ///            5개 언어 글자가 칸 안에.
    /// 운영 서버에 쓰지 않는다 — 고치기 창의 서버 주소(EditorBase)를 시험 서버로 돌리고 끝나면 되돌린다.
    /// 편집기 로그인 토큰은 있는지만 본다 (만들거나 바꾸거나 지우지 않는다) — 내 장소 부분은 시험 서버가 그동안만 토큰 없는 요청도 시험 회원 13 으로 본다.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanFixAi()
        {
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            const string Main3 = "uploads/seed_3/main.jpg", Main200 = "uploads/seed_200/main.jpg";
            const string Remodel = FixDev + "/uploads/__fixai_remodel.glb";   // 캐시에만 넣는다 — 받으러 가지 않는다
            const string Ask = "빨간 모자를 쓴 고양이";
            var langs = new[] { "ko", "en", "ja", "zh", "es" };
            var T = typeof(CubeDataFixManager);
            var P = typeof(R0926FixAiPanel);
            CubeDataFixManager fix = null;
            R0926FixAiPanel ai = null;
            Func<string, object> fv = n => T.GetField(n, F).GetValue(fix);
            Action<string, object> fs = (n, v) => T.GetField(n, F).SetValue(fix, v);
            Func<string, object[], object> call = (n, a) => T.GetMethod(n, F).Invoke(fix, a);
            Func<string, object> av = n => P.GetField(n, F).GetValue(ai);
            Func<GameObject> panel = () => (GameObject)fv("fixUIPanel");
            Func<bool> loaded = () => fix != null && !(bool)fv("isProcessing") && fv("cleanPhotos") != null;
            Func<string, Button> btn = n => (Button)av(n);
            Func<string, RectTransform> part = n => (RectTransform)av(n);
            Func<Text> status = () => (Text)av("status");
            Func<RawImage> photo = () => (RawImage)av("photo");
            Func<RawImage> checker = () => (RawImage)av("checker");
            Func<R0926ProfileModelView> model = () => (R0926ProfileModelView)av("model");
            Func<InputField> request = () => (InputField)av("request");
            Func<int, Button> chip = i => ((Button[])av("chips"))[i];
            Func<int, bool> chipOn = i => { var b = ((Image[])av("chipBgs"))[i]; return b != null && b.color.a > 0.9f && b.color.r > 0.8f; };   // 켜짐 = 분홍 · 꺼짐 = 옅은 흰
            Func<bool> aiLit = () => { var b = (Image)av("aiButtonBg"); return b != null && b.color.a > 0.9f && b.color.r > 0.8f; };       // 넣어 두면 분홍 · 아니면 어두운 유리
            Func<bool> made = () => ai != null && !ai.Working && ai.Made != null;
            Func<bool> madeChar = () => made() && ai.Made.Glb != null;
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Exception
                    || (type == LogType.Error && (msg.Contains("[CubeDataFixManager]") || msg.Contains("[R0926FixAiPanel]")))
                    || (type == LogType.Warning && msg.Contains("[CubeDataFixManager]") && msg.Contains("실패 id=")))
                    errors.Add(type + " " + msg);
            };
            JObject last = null;
            int lastId = 0;
            Texture2D orig = null, madeTex = null;
            R0926AiWork.Result first = null;
            byte[] glb2 = null, ownerGlb = null;
            bool ownerOk = false;
            string lang0 = null;
            int remodelId = 0;
            PlaceData remodelOrig = null;
            GameObject remodelOld = null;
            const string DropKey = "r0926_menu_style";
            bool hadDrop = false;
            int sDrop = 1;

            Func<string, JObject> getJ = path =>
            {
                string r = FixHttp(path);
                try { return string.IsNullOrEmpty(r) ? null : JObject.Parse(r); } catch (Exception) { return null; }
            };
            Func<JObject> lastFix = () => getJ("/__fix/last");
            Func<bool> fresh = () => last != null && last["id"] != null && (int)last["id"] > lastId;
            Func<JToken, bool> isNull = t => t == null || t.Type == JTokenType.Null;
            Func<RectTransform, Rect> world = rt =>
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            };
            // 글이 칸 안에 드는지 — 맞춤 글자면 가장 작은 크기로 잰다 · 넘치면 까닭
            Func<Text, bool, string> spill = (t, bold) =>
            {
                var r = t.rectTransform.rect;
                float ppu = Mathf.Max(0.0001f, t.pixelsPerUnit);
                var g = new TextGenerator();
                var gw = t.GetGenerationSettings(Vector2.zero);
                var gh = t.GetGenerationSettings(new Vector2(r.width, 0f));
                if (t.resizeTextForBestFit)
                {
                    gw.resizeTextForBestFit = gh.resizeTextForBestFit = false;
                    gw.fontSize = gh.fontSize = t.resizeTextMinSize;
                }
                if (bold) gw.fontStyle = gh.fontStyle = FontStyle.Bold;
                float w = g.GetPreferredWidth(t.text, gw) / ppu;
                float h = g.GetPreferredHeight(t.text, gh) / ppu;
                if (t.horizontalOverflow == HorizontalWrapMode.Overflow && w > r.width + 1f) return $"폭 {w:0}>{r.width:0}";
                if (h > r.height + 1f) return $"높이 {h:0}>{r.height:0}";
                return null;
            };

            // 장소를 수정 창에 — BeginEdit 와 같은 순서 (fixmove 와 같게)
            Action<int, string, string> open = (id, name, main) =>
            {
                panel().SetActive(true);
                fs("doubleTap", null);
                int gen = (int)fv("loadGen") + 1;
                fs("loadGen", gen);
                call("ResetToInitialState", null);
                fs("editId", id);
                call("SetOwnerMode", new object[] { false });
                var pd = new PlaceData { id = id, name = name, main_photo = main, sub_photos = new List<List<string>>() };
                if (!fix.isActiveAndEnabled) { log.Add("  고치기 창이 꺼져 있어 불러오지 못함"); return; }
                fix.StartCoroutine((IEnumerator)call("LoadPlace", new object[] { pd, gen }));
            };
            Action<string> openRequest = what =>
            {
                Do(() => open(3, "시험 장소", Main3));
                Wait(loaded, 40f, what);
                Do(() =>
                {
                    if ((bool)fv("ownerMode")) { log.Add("  서버가 내 장소라고 함 — 수정 요청으로 돌림"); call("SetOwnerMode", new object[] { false }); }
                    orig = fix.MainPhoto;
                });
                Sleep(0.3f);
            };
            Action send = () =>
            {
                var j = lastFix();
                lastId = j != null && j["id"] != null ? (int)j["id"] : 0;
                fix.StartCoroutine((IEnumerator)call("ValidateAndSubmit", null));
            };
            Action<string> submit = what =>
            {
                Do(send);
                Wait(() => !panel().activeSelf, 25f, what + " — 보냄 · 창 닫힘");
                Sleep(2.3f);   // ValidateAndSubmit 의 2초 뒤 정리까지
                Do(() =>
                {
                    last = lastFix();
                    var mf = last?["main_file"] as JObject;
                    var md = last?["model"] as JObject;
                    log.Add($"  받은 요청 #{last?["id"]} · 대표 사진 {System.IO.Path.GetFileName((string)last?["main_photo"] ?? "")}"
                            + $" (PNG {mf?["png"]} · 투명 {mf?["clear"]}) · 3D {(md != null ? md["size"] + "B" : "없음")}");
                });
            };
            // 내 장소 확인 — 시험 회원을 못 꾸렸으면 SKIP 으로 남긴다
            Action<Func<bool>, string> owner = (ok, what) => Do(() => log.Add(!ownerOk ? "SKIP " + what : (ok() ? "PASS " : "FAIL ") + what));

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
                CubeDataFixManager.EditorBase = FixDev;
                fix = UnityEngine.Object.FindAnyObjectByType<CubeDataFixManager>(FindObjectsInactive.Include);
                ai = UnityEngine.Object.FindAnyObjectByType<R0926FixAiPanel>(FindObjectsInactive.Include);
                lang0 = AppLanguage.EditorOverride;
                hadDrop = PlayerPrefs.HasKey(DropKey); sDrop = PlayerPrefs.GetInt(DropKey, 1);
                R0926MenuStyle.Droplet = true;   // 물방울 모드에서 칸 · 사진 · AI 화면이 비치지 않는지 본다 (11-26 대표님 6 · 7번) — 끝나면 되돌린다
                log.Add($"  외부 AI 없음(폰 안에서) {R0926AiWork.Provider == null}");
            });
            Check(() => fix != null && ai != null && panel() != null && btn("aiButton") != null && part("view") != null && lastFix() != null,
                "준비: 고치기 창 · AI 작업 칸 연결 · 시험 서버 (임시 DB)");

            // 1) 남의 장소 수정 요청 — 로고 칸 아래 「AI 작업」 (불러오는 동안은 숨김)
            bool hidWhileBusy = false;
            Do(() => { hidWhileBusy = false; open(3, "시험 장소", Main3); });
            Wait(() =>
            {
                if ((bool)fv("isProcessing") && !btn("aiButton").gameObject.activeSelf) hidWhileBusy = true;
                return loaded();
            }, 40f, "장소 #3 수정 요청 창");
            Do(() =>
            {
                if ((bool)fv("ownerMode")) { log.Add("  서버가 내 장소라고 함 — 수정 요청으로 돌림"); call("SetOwnerMode", new object[] { false }); }
                orig = fix.MainPhoto;
            });
            Sleep(0.3f);
            // 13번 (11-26 대표님 「'우팡이네집' 의 경우 정보수정을 하려고 햇는데 … 좌우로 찌그러진 채로 사진을 불러오거든? (장소로고추가)」)
            // 시험 서버 대표 사진은 320×240 JPG (가로가 긴 사진) — 로고 칸 · AI · 저장이 늘려 넣지 않게 정사각형으로
            Check(() =>
            {
                var t = fix.MainPhoto;
                var im = (Image)fv("mainPhotoDisplay");
                var r = im != null && im.sprite != null ? im.sprite.rect : default(Rect);
                log.Add($"  대표 사진 {t?.width}×{t?.height} (서버 320×240) · 칸 그림 {r.width}×{r.height} · 투명 {R0926PhotoAlpha.Has(t)} · 경로 그대로 {(string)fv("mainKeepPath") == Main3}");
                return t != null && t.width == t.height && (t.width == 240 || t.width == 320) && im != null && im.sprite != null
                       && Mathf.Abs(r.width - r.height) < 0.5f && (string)fv("mainKeepPath") == Main3;
            }, "정사각형이 아닌 서버 대표 사진(320×240)은 찌그러뜨리지 않고 정사각형으로 · 서버 경로는 그대로 (11-26 대표님 13번)");
            Check(() =>
            {
                // 우팡이네집처럼 — 가로 4:3 투명 캔버스 가운데 동그라미 로고
                var src = new Texture2D(400, 300, TextureFormat.RGBA32, false);
                var px = new Color32[400 * 300];
                for (int y = 0; y < 300; y++)
                    for (int x = 0; x < 400; x++)
                        px[y * 400 + x] = (x - 200) * (x - 200) + (y - 150) * (y - 150) <= 120 * 120 ? new Color32(220, 120, 60, 255) : new Color32(0, 0, 0, 0);
                src.SetPixels32(px);
                src.Apply(false, false);
                var sq = R0926PhotoAlpha.Square(src, true);
                var o = sq.GetPixels32();
                int x0 = 9999, x1 = -1, y0 = 9999, y1 = -1;
                for (int y = 0; y < sq.height; y++)
                    for (int x = 0; x < sq.width; x++)
                        if (o[y * sq.width + x].a > 128) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
                log.Add($"  투명 로고 400×300 → {sq.width}×{sq.height} · 동그라미 가로 {x1 - x0 + 1} 세로 {y1 - y0 + 1} · 가운데 ({(x0 + x1) * 0.5f:0.0}, {(y0 + y1) * 0.5f:0.0}) · 모서리 알파 {o[0].a}/{o[o.Length - 1].a}");
                bool ok = sq.width == 400 && sq.height == 400 && R0926PhotoAlpha.Has(sq) && o[0].a == 0 && o[o.Length - 1].a == 0
                          && Mathf.Abs((x1 - x0) - (y1 - y0)) <= 2 && Mathf.Abs((x0 + x1) * 0.5f - 200f) <= 1.5f && Mathf.Abs((y0 + y1) * 0.5f - 200f) <= 1.5f;
                UnityEngine.Object.Destroy(src);
                UnityEngine.Object.Destroy(sq);
                return ok;
            }, "투명 로고(가로가 긴 캔버스)는 다 보이게 투명 여백 — 동그라미가 그대로 둥글다 (13번)");
            // AR 큐브 면 — 받은 사진을 그대로 늘려 붙이지 않는다 (투명 로고는 여백 · 보통 사진은 가운데 잘라) · 받은 것은 지운다 (지우기는 프레임 끝)
            GameObject cube13 = null;
            MeshRenderer mr13 = null;
            Material mat13 = null;
            Texture2D logo13 = null, pic13 = null;
            string face1 = "", face2 = "";
            Do(() =>
            {
                cube13 = new GameObject("FixAi13Cube");
                cube13.SetActive(false);
                mr13 = cube13.AddComponent<MeshRenderer>();
                mat13 = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mr13.sharedMaterial = mat13;
                var idc = cube13.AddComponent<ImageDisplayController>();
                idc.cubeRenderer = mr13;
                var apply = typeof(ImageDisplayController).GetMethod("ApplyBaseMap", F);
                logo13 = new Texture2D(400, 300, TextureFormat.RGBA32, false);
                logo13.SetPixels32(new Color32[400 * 300]);
                logo13.Apply(false, true);
                apply.Invoke(idc, new object[] { logo13, "fixai13/logo.png" });
                var t1 = mr13.material.GetTexture("_BaseMap");
                face1 = t1 != null ? $"{t1.width}x{t1.height}" : "-";
                pic13 = new Texture2D(300, 450, TextureFormat.RGB24, false);
                pic13.Apply(false, true);
                apply.Invoke(idc, new object[] { pic13, "fixai13/pic.jpg" });
                var t2 = mr13.material.GetTexture("_BaseMap");
                face2 = t2 != null ? $"{t2.width}x{t2.height}" : "-";
            });
            Sleep(0.1f);
            Check(() =>
            {
                log.Add($"  AR 큐브 면: 투명 400×300 → {face1} · 보통 300×450 → {face2} · 받은 것 지움 {logo13 == null}/{pic13 == null}");
                return face1 == "400x400" && face2 == "300x300" && logo13 == null && pic13 == null;
            }, "AR 큐브 면: 정사각형이 아닌 사진은 정사각형으로 바꿔 붙인다 (투명 로고 = 여백 · 보통 사진 = 가운데) · 받은 것은 지운다 (13번)");
            Do(() =>
            {
                var t = mr13 != null ? mr13.material.GetTexture("_BaseMap") : null;
                if (t != null) UnityEngine.Object.Destroy(t);
                if (mr13 != null) UnityEngine.Object.Destroy(mr13.material);
                if (mat13 != null) UnityEngine.Object.Destroy(mat13);
                if (cube13 != null) UnityEngine.Object.Destroy(cube13);
            });
            Check(() =>
            {
                var b = btn("aiButton");
                log.Add($"  불러오는 동안 숨김 {hidWhileBusy} · 지금 보임 {b.gameObject.activeInHierarchy} · 분홍 {aiLit()} · AI 화면 {part("view").gameObject.activeSelf}");
                return !(bool)fv("ownerMode") && b.gameObject.activeInHierarchy && b.interactable && !aiLit() && !part("view").gameObject.activeSelf
                       && !fix.AiOn && fix.MainPhoto != null;
            }, "수정 요청 창: 로고 칸에 「AI 작업」(어두운 유리) · AI 화면은 닫힘");
            Check(() =>
            {
                Rect t = world((RectTransform)((Button)fv("mainPhotoButton")).transform), a = world((RectTransform)btn("aiButton").transform);
                log.Add($"  로고 칸 {t.xMin:0}~{t.xMax:0} × {t.yMin:0}~{t.yMax:0} · AI 작업 {a.xMin:0}~{a.xMax:0} × {a.yMin:0}~{a.yMax:0}");
                return a.xMin >= t.xMin - 1 && a.xMax <= t.xMax + 1 && a.yMin >= t.yMin - 1 && a.yMax <= t.center.y;
            }, "「AI 작업」은 로고 칸 안 아래쪽");
            Shot("fixai_button");
            // 6번 (11-26 대표님 「'정보수정' 들어갔을 때 발생하는 각 사진들도 투명하게 나오면 안되거든? 배경만 투명하게 하고 각 버튼 및 추가로 발생하는 것들은 투명하게 발생하지 않도록」)
            Check(() =>
            {
                var pg = Find("Fixpage/FixUploadPage");
                var sk = pg != null ? pg.GetComponent<R0926WindowSkin>() : null;
                var mp = (Image)fv("mainPhotoDisplay");
                var mf = mp != null ? mp.GetComponent<R0926WindowFill>() : null;
                var names = new List<string>();
                int nb = 0;
                int fd = pg != null ? WsFaded(pg, names, out nb) : -1;
                float pa = mp != null ? WsDrawnAlpha(mp) : -1f;
                bool keep = pg != null && pg.GetComponent<R0926KeepSolid>() != null;
                log.Add($"  정보수정 창 — 물방울 {R0926MenuStyle.Droplet} · 창 켜짐 {sk?.Showing} · 비침 {sk?.SeeThrough} · 칸 불투명 {keep}"
                        + $" · 상자 {nb}개 중 옅어진 것 {fd}{(names.Count > 0 ? " (" + string.Join(", ", names) + ")" : "")}"
                        + $" · 로고 사진 알파 {pa:0.00} (원래 {(mp != null ? mp.color.a : 0f):0.00} · 맞춤 {(mf != null ? mf.FillMode.ToString() : "없음")} · 그림 {(mp != null && mp.sprite != null)})");
                return R0926MenuStyle.Droplet && sk != null && sk.Showing && keep && nb > 0 && fd == 0 && mp != null && mp.sprite != null && pa >= 0.99f * mp.color.a;
            }, "물방울 모드 정보수정 — 창 바탕만 비치고 칸 · 버튼 · 로고 사진은 투명하지 않게 (11-26 대표님 6번)");

            // 2) 누르면 수정 카드가 그 자리에서 AI 화면 — ‹ · 안내 · 큰 미리보기(지금 로고) · 네 칸 · 취소|확인
            Do(() => btn("aiButton").onClick.Invoke());
            Sleep(0.2f);
            Check(() =>
            {
                string s = status().text;
                log.Add($"  안내 「{s}」 · 미리보기 = 로고 {photo().texture == orig} · 요청사항 칸 {part("ask").gameObject.activeSelf}");
                return ai.Open && part("view").gameObject.activeInHierarchy && photo().enabled && photo().texture == orig && checker().enabled
                       && !part("ask").gameObject.activeSelf && (AppLanguage.Code != "ko" || s.StartsWith("원하는 효과를 고르세요"))
                       && !chipOn(0) && !chipOn(1) && !chipOn(2) && !chipOn(3);
            }, "AI 화면: 안내 한 줄 · 미리보기에 지금 로고 · 네 칸 다 꺼짐 · 요청사항 칸 숨김");
            Check(() =>
            {
                Rect v = world(part("view")), card = world((RectTransform)part("view").parent);
                Rect f = world(part("foot")), tl = world(part("tiles")), sg = world(part("stage")), st = world(status().rectTransform);
                log.Add($"  아래부터 취소|확인 {f.yMin:0}~{f.yMax:0} · 네 칸 {tl.yMin:0}~{tl.yMax:0} · 미리보기 {sg.yMin:0}~{sg.yMax:0} · 안내 {st.yMin:0}~{st.yMax:0}");
                return f.yMax <= tl.yMin + 1 && tl.yMax <= sg.yMin + 1 && sg.yMax <= st.yMin + 1 && sg.height > tl.height
                       && Mathf.Abs(v.xMin - card.xMin) < 2 && Mathf.Abs(v.xMax - card.xMax) < 2 && Mathf.Abs(v.yMin - card.yMin) < 2 && Mathf.Abs(v.yMax - card.yMax) < 2;
            }, "칸 차례: 취소|확인 < 네 칸 < 미리보기 < 안내 — 겹침 없음 · AI 화면이 수정 카드를 꽉 채움");
            Shot("fixai_open");
            // 7번 (11-26 대표님 「오브젝트 메인사진의 'AI작업' 버튼을 눌렀을 때도 발생하는 레이아웃이 투명해서 겹쳐서 발생하면 안되거든?」)
            Check(() =>
            {
                var v = part("view");
                var sk = v != null ? v.GetComponent<R0926WindowSkin>() : null;
                var names = new List<string>();
                int nb = 0;
                int fd = v != null ? WsFaded(v, names, out nb) : -1;
                log.Add($"  AI 화면 — 켜짐 {sk?.Showing} · 수정 카드와 겹침 {sk?.Stacked} · 불투명 가림 {sk?.Covered} · 비침 {sk?.SeeThrough}"
                        + $" · 상자 {nb}개 중 옅어진 것 {fd}{(names.Count > 0 ? " (" + string.Join(", ", names) + ")" : "")}");
                return R0926MenuStyle.Droplet && sk != null && sk.Showing && sk.Covered && fd == 0;
            }, "물방울 모드 AI 작업 화면 — 바탕이 수정 카드를 불투명하게 가린다(밑 칸이 비치지 않음) · 네 칸 · 버튼도 투명하지 않게 (11-26 대표님 7번)");

            // 3) 01 투명하게 — 고르는 즉시 작업 → 미리보기에 투명한 결과(바둑판 위) · 로고 칸은 아직 그대로
            bool sawWorking = false;
            string workText = null;
            Do(() => { sawWorking = false; chip(0).onClick.Invoke(); });
            Wait(() =>
            {
                if (ai.Working) { sawWorking = true; workText = status().text; }
                return made();
            }, 30f, "투명하게 만들기");
            Check(() =>
            {
                var m = ai.Made;
                log.Add($"  작업 중 보임 {sawWorking}{(workText != null ? " 「" + System.Text.RegularExpressions.Regex.Replace(workText, "<[^>]+>", "") + "」" : "")} · 투명 {m.Alpha} · {m.Clear} · 안내 「{status().text}」");
                return m.Alpha && m.Clear != R0926AiImage.ClearMode.None && m.Style == R0926AiWork.Style.None && photo().texture == m.Tex && photo().enabled
                       && checker().enabled && chipOn(0) && !chipOn(3) && fix.MainPhoto == orig && !fix.AiOn && !aiLit();
            }, "투명하게: 바로 만들어 미리보기에 (바둑판 위) · 01 분홍 · 로고 칸은 아직 그대로");
            Shot("fixai_clear");

            // 4) 확인 — 로고 칸에 넣는다 (AI 작업 분홍) · 보내면 수정 요청에 투명 PNG
            Do(() => { madeTex = ai.Made.Tex; btn("viewConfirm").onClick.Invoke(); });
            Sleep(0.2f);
            Check(() =>
            {
                var disp = (Image)fv("mainPhotoDisplay");
                return !ai.Open && !part("view").gameObject.activeSelf && fix.AiOn && fix.MainPhoto == madeTex && fix.AiGlb == null && aiLit()
                       && disp != null && disp.sprite != null && disp.sprite.texture == madeTex && fv("mainKeepPath") == null;
            }, "확인: 로고 칸에 투명한 로고 · 「AI 작업」 분홍 · 새 사진으로 올릴 준비");
            Shot("fixai_kept");
            submit("투명 로고 요청");
            Check(() =>
            {
                var mf = last?["main_file"] as JObject;
                return fresh() && last["target_id"] != null && (int)last["target_id"] == 3 && ((string)last["main_photo"] ?? "").EndsWith("/main.png")
                       && mf != null && (bool)mf["png"] && (bool)mf["clear"] && isNull(last["model"]);
            }, "수정 요청에 투명한 로고가 PNG 그대로 (JPG 로 검게 메우지 않음) · 3D 없음");
            Check(() => !fix.AiOn && fix.AiGlb == null, "보낸 뒤 창을 비우면 AI 작업도 비운다");

            // 5) 취소는 그대로 · ‹ 는 넣기 · 다시 열면 고른 그대로 · 같은 칸을 다시 누르면 원래 사진 → 확인하면 원래대로 (보낼 것 없음)
            openRequest("다시 엶 (취소 · 뒤로 · 되돌리기)");
            Check(() => !fix.AiOn && !aiLit() && !ai.Open && fix.MainPhoto == orig,
                "보낸 뒤 다시 열면 「AI 작업」은 어두운 유리 · 로고 칸은 서버 사진 (넣은 것 없음)");
            Do(() => { btn("aiButton").onClick.Invoke(); chip(1).onClick.Invoke(); });
            Wait(made, 30f, "만화톤");
            Do(() => btn("viewCancel").onClick.Invoke());
            Sleep(0.2f);
            Check(() => !ai.Open && !fix.AiOn && fix.MainPhoto == orig && (string)fv("mainKeepPath") == Main3 && !aiLit(),
                "취소: 만든 것은 버리고 로고 칸 그대로 (서버 사진 그대로)");
            Do(() => btn("aiButton").onClick.Invoke());
            Sleep(0.1f);
            Check(() => ai.Open && ai.Style == R0926AiWork.Style.None && !ai.Clear && !chipOn(0) && !chipOn(1) && photo().texture == orig,
                "취소한 뒤 다시 열면 아무것도 안 골라져 있다");
            Do(() => chip(1).onClick.Invoke());
            Wait(made, 30f, "만화톤 다시");
            Do(() => { madeTex = ai.Made.Tex; btn("back").onClick.Invoke(); });
            Sleep(0.2f);
            Check(() => !ai.Open && fix.AiOn && fix.MainPhoto == madeTex && aiLit(), "‹ (뒤로): 만든 것을 로고 칸에 넣고 수정 카드로");
            Do(() => btn("aiButton").onClick.Invoke());
            Sleep(0.1f);
            Check(() => ai.Open && ai.Style == R0926AiWork.Style.Toon && chipOn(1) && !chipOn(0) && photo().texture == fix.MainPhoto,
                "다시 열면 넣어 둔 그대로 — 02 만화톤 골라짐 · 미리보기에 넣은 로고");
            Do(() => chip(1).onClick.Invoke());   // 같은 칸을 다시 — 끈다
            Wait(() => !ai.Working, 5f, "다 끔");
            Check(() =>
            {
                log.Add($"  안내 「{status().text}」");
                return !chipOn(1) && photo().texture == orig && (AppLanguage.Code != "ko" || status().text.StartsWith("원래 사진"));
            }, "같은 칸을 다시 누르면 꺼지고 미리보기는 원래 사진");
            Do(() => btn("viewConfirm").onClick.Invoke());
            Sleep(0.2f);
            Check(() => !fix.AiOn && fix.MainPhoto == orig && (string)fv("mainKeepPath") == Main3 && !aiLit(),
                "확인하면 원래 사진 그대로 (같은 사진 · 서버 경로로 되돌림)");
            Do(send);
            Sleep(1.5f);
            Check(() =>
            {
                var j = lastFix();
                return panel().activeSelf && (j?["id"] == null || (int)j["id"] == lastId);
            }, "다 끄고 확인하면 바꾼 게 없다 — 보내지 않는다 (창 그대로)");

            // 6) 칩 · 미리보기를 짚고 끌어도 수정 카드째 닫히지 않는다 — 그 끌기는 AI 화면 몫 (R0926SwipeDismiss 가 고르는 방법 그대로)
            Do(() => btn("aiButton").onClick.Invoke());
            Sleep(0.1f);
            Check(() =>
            {
                var bad = new List<string>();
                foreach (var go in new[] { chip(0).gameObject, chip(3).gameObject, part("stage").gameObject, photo().gameObject, status().gameObject, part("view").gameObject })
                {
                    var h = ExecuteEvents.GetEventHandler<IDragHandler>(go);
                    if (h == null || !h.transform.IsChildOf(part("view")) || h.GetComponent<ScrollRect>() != null)
                        bad.Add(go.name + "→" + (h != null ? h.name : "없음"));
                }
                foreach (var b in bad) log.Add("  끌기: " + b);
                return bad.Count == 0;
            }, "AI 화면 어디를 짚고 끌어도 그 끌기는 AI 화면 몫 (수정 카드 아래로 닫기 아님)");
            Do(() => btn("viewCancel").onClick.Invoke());

            // 11번 (11-26 대표님 「3D오브젝트의 경우 AI수정에 있어서 '투명 / 만화 / 색연필' 이런게 없이 그냥 '3D캐릭터' 수정만」)
            string type11 = null;
            Do(() =>
            {
                type11 = (string)fv("editModelType");
                fs("editModelType", "custom");
                btn("aiButton").onClick.Invoke();
            });
            Sleep(0.3f);
            Shot("fixai_model3d");
            Check(() =>
            {
                Rect tl = world(part("tiles")), c3 = world((RectTransform)chip(3).transform);
                string s = status().text;
                log.Add($"  3D 모델 장소: 01~03 보임 {chip(0).gameObject.activeSelf}/{chip(1).gameObject.activeSelf}/{chip(2).gameObject.activeSelf}"
                        + $" · 3D 캐릭터 폭 {c3.width / tl.width:0.00} (줄 대비) · 안내 「{s}」 (처음 종류 {type11})");
                return fix.IsModelPlace && !chip(0).gameObject.activeSelf && !chip(1).gameObject.activeSelf && !chip(2).gameObject.activeSelf
                       && chip(3).gameObject.activeInHierarchy && c3.width > tl.width * 0.97f && !ai.Clear && ai.Style == R0926AiWork.Style.None
                       && s.StartsWith("3D 캐릭터");
            }, "3D 모델 장소: AI 작업은 「3D 캐릭터」 한 칸만 — 투명하게 · 만화톤 · 색연필 없음 · 한 줄을 다 채움 (11-26 대표님 11번)");
            Do(() => chip(0).onClick.Invoke());
            Check(() => !ai.Clear && !ai.Working && ai.Made == null, "3D 모델 장소: 숨긴 01 을 (코드로) 눌러도 아무 일 없음");
            Do(() =>
            {
                btn("viewCancel").onClick.Invoke();
                fs("editModelType", type11 ?? "");
                btn("aiButton").onClick.Invoke();
            });
            Sleep(0.3f);
            Check(() =>
            {
                Rect tl = world(part("tiles")), c3 = world((RectTransform)chip(3).transform);
                return !fix.IsModelPlace && chip(0).gameObject.activeSelf && chip(1).gameObject.activeSelf && chip(2).gameObject.activeSelf && c3.width < tl.width * 0.3f
                       && (AppLanguage.Code != "ko" || status().text.StartsWith("원하는 효과를 고르세요"));
            }, "사진 장소로 돌아오면 다시 네 칸 · 안내도 원래대로");
            Do(() => btn("viewCancel").onClick.Invoke());

            // 7) 04 3D 캐릭터 — 요청사항 칸 · 미리보기에서 도는 캐릭터 · 요청사항으로 다시 만들기 → 확인 → 요청에 model.glb + 정지 사진 PNG
            Do(() => { btn("aiButton").onClick.Invoke(); chip(3).onClick.Invoke(); });
            Wait(madeChar, 40f, "3D 캐릭터");
            Sleep(0.8f);   // 미리보기 모델이 뜨게
            Check(() =>
            {
                var m = ai.Made;
                first = m;
                var mv = model();
                log.Add($"  캐릭터 「{m.Label}」 · GLB {m.Glb.Length / 1024}KB · 정지 사진 {m.Tex?.width}×{m.Tex?.height} 투명 {m.Alpha}"
                        + $" · 모델 보임 {mv.Showing} 불러오는 중 {mv.Loading} · 안내 「{status().text}」");
                return m.Style == R0926AiWork.Style.Character && m.Tex != null && m.Alpha && part("ask").gameObject.activeInHierarchy && chipOn(3) && !chipOn(0)
                       && !photo().enabled && !checker().enabled && (mv.Showing || mv.Loading) && btn("make").interactable;
            }, "3D 캐릭터: 요청사항 칸이 열리고 · 미리보기는 사진 대신 캐릭터 · 만들기 눌림");
            Check(() =>
            {
                Rect a = world(part("ask")), t = world(part("tiles")), f = world(part("foot")), s = world(part("stage"));
                return f.yMax <= a.yMin + 1 && a.yMax <= t.yMin + 1 && t.yMax <= s.yMin + 1;
            }, "요청사항 칸은 취소|확인 위 · 네 칸 아래 (미리보기가 그만큼 줄어든다)");
            Shot("fixai_char");
            Do(() => { request().text = Ask; btn("make").onClick.Invoke(); });
            Wait(() => madeChar() && ai.Made != first, 40f, "요청사항으로 다시 만들기");
            Check(() =>
            {
                glb2 = ai.Made.Glb;
                log.Add($"  다시 만든 캐릭터 「{ai.Made.Label}」 · GLB {glb2.Length / 1024}KB · 처음과 다름 {!ReferenceEquals(first.Glb, glb2)}");
                return glb2 != null && !ReferenceEquals(first.Glb, glb2) && ai.Style == R0926AiWork.Style.Character;
            }, "만들기: 요청사항으로 새 캐릭터");
            Do(() => { madeTex = ai.Made.Tex; btn("viewConfirm").onClick.Invoke(); });
            Sleep(0.2f);
            Check(() => !ai.Open && fix.AiOn && ReferenceEquals(fix.AiGlb, glb2) && fix.MainPhoto == madeTex && aiLit(),
                "확인: 로고 칸에 캐릭터 정지 사진 · 3D 모델도 같이 넣어 둠");
            Do(() => btn("aiButton").onClick.Invoke());
            Sleep(0.8f);
            Check(() => ai.Open && ai.Style == R0926AiWork.Style.Character && chipOn(3) && !photo().enabled && (model().Showing || model().Loading)
                        && request().text == Ask,
                "다시 열면 캐릭터가 미리보기에 · 요청사항도 그대로");
            Do(() => btn("back").onClick.Invoke());
            submit("캐릭터 요청");
            Check(() =>
            {
                var mf = last?["main_file"] as JObject;
                var md = last?["model"] as JObject;
                return fresh() && last["target_id"] != null && (int)last["target_id"] == 3 && mf != null && (bool)mf["png"]
                       && md != null && (bool)md["glb"] && (long)md["size"] == glb2.Length;
            }, "수정 요청에 캐릭터 정지 사진(PNG) + 3D 모델(model.glb · 만든 그대로)");

            // 8) 내 장소 (시험 회원 13 의 #200) — 3D 캐릭터를 먼저 /model 로 → 장소가 3D 로 · 대표 사진은 /edit 로 투명 PNG · 앱 안 장소 값도 바로
            Do(() =>
            {
                // 시험 서버가 이 동안만 토큰 없는 요청도 시험 회원 13 으로 본다 (any) — 편집기 로그인 토큰은 읽기만 · 바꾸지 않는다
                bool tok = !string.IsNullOrEmpty(PlayerPrefs.GetString(LoginManager.AUTH_TOKEN_KEY, ""));
                string r = FixHttp("/__owner/setup", "{\"any\":true}");
                ownerOk = r.Contains("\"ok\"");
                log.Add($"  편집기 로그인 토큰 있음 {tok} · 시험 회원 준비 {ownerOk}" + (ownerOk ? "" : " — 내 장소 시험 건너뜀"));
            });
            Do(() => { if (ownerOk) open(OwnerPlace, "내 시험 장소", Main200); });
            Wait(() => !ownerOk || loaded(), 40f, "내 장소 #200 고치기 창");
            Sleep(0.3f);
            owner(() => (bool)fv("ownerMode") && btn("aiButton").gameObject.activeInHierarchy, "내 장소: 서버가 「내 것」이라 해서 고치기 모드 · 「AI 작업」 있음");
            Do(() => { if (ownerOk) { btn("aiButton").onClick.Invoke(); chip(3).onClick.Invoke(); } });
            Wait(() => !ownerOk || madeChar(), 40f, "내 장소 3D 캐릭터");
            Do(() => { if (ownerOk) { ownerGlb = ai.Made.Glb; btn("viewConfirm").onClick.Invoke(); } });
            Sleep(0.2f);
            owner(() => fix.AiOn && ReferenceEquals(fix.AiGlb, ownerGlb) && aiLit(), "내 장소: 캐릭터를 로고 칸에");
            Shot("fixai_owner");
            Do(() => { if (ownerOk) fix.StartCoroutine((IEnumerator)call("ValidateAndSubmit", null)); });
            Wait(() => !ownerOk || !panel().activeSelf, 30f, "내 장소 저장 · 창 닫힘");
            Sleep(0.5f);
            owner(() =>
            {
                var j = getJ("/__owner/place/" + OwnerPlace);
                var mf = j?["model_file"] as JObject;
                var pf = j?["main_file"] as JObject;
                string url = (string)j?["model_url"] ?? "";
                log.Add($"  #{OwnerPlace} · {j?["model_type"]} · 모델 {System.IO.Path.GetFileName(url)} {mf?["size"]}B · {j?["model_format"]} 움직임 {j?["has_animation"]}"
                        + $" · 대표 사진 {System.IO.Path.GetFileName((string)j?["main_photo"] ?? "")} 투명 {pf?["clear"]} · 폴더 파일 {(j?["files"] as JArray)?.Count}");
                return j != null && (string)j["model_type"] == "custom" && System.Text.RegularExpressions.Regex.IsMatch(url, @"^uploads/seed_200/model_\d{17}\.glb$")
                       && mf != null && (bool)mf["glb"] && (long)mf["size"] == ownerGlb.Length && (string)j["model_format"] == "glb"
                       && pf != null && (bool)pf["png"] && (bool)pf["clear"];
            }, "내 장소: 서버 장소가 3D(custom · model_<시각>.glb · 만든 그대로)로 · 대표 사진은 투명 PNG");
            owner(() =>
            {
                var map = DataManager.Instance != null ? DataManager.Instance.GetPlaceDataMap() : null;
                return map != null && map.TryGetValue(OwnerPlace, out var p) && p.model_type == "custom" && (p.model_url ?? "").Contains("/model_");
            }, "앱 안 장소 값도 바로 3D (ApplyEditedPlace)");
            Do(() =>
            {
                DataManager.Instance?.GetPlaceDataMap().Remove(OwnerPlace);   // 시험 장소 — 앱 안에서도 걷는다
                if (ownerOk) FixHttp("/__owner/teardown", "{}");
            });

            // 9) 떠 있는 장소의 모델이 바뀌면 (내 장소 저장 · 우팡M 반영) 그 자리에서 새 3D 로 다시 뜬다 — 시험 모델은 캐시에만 · 장소 값은 앱 안에서만 잠깐
            var dmT = typeof(DataManager);
            Func<HashSet<int>> loadingGlb = () => (HashSet<int>)dmT.GetField("currentlyLoadingGLB", F).GetValue(DataManager.Instance);
            Do(() =>
            {
                var dm = DataManager.Instance;
                remodelId = 0;
                if (dm == null || glb2 == null) return;
                foreach (var kv in dm.GetSpawnedObjects())
                {
                    if (kv.Value == null || !kv.Value.activeInHierarchy) continue;
                    if (!dm.GetPlaceDataMap().TryGetValue(kv.Key, out var p) || p == null || p.model_type != "cube") continue;
                    remodelId = kv.Key; remodelOrig = p; remodelOld = kv.Value;
                    break;
                }
                if (remodelId == 0) return;
                var copy = JsonConvert.DeserializeObject<PlaceData>(JsonConvert.SerializeObject(remodelOrig));
                copy.model_type = "custom";
                copy.model_url = Remodel;
                GLBModelLoader.PreloadCache(Remodel, glb2);
                dm.ApplyEditedPlace(copy, false);
            });
            Wait(() =>
            {
                if (remodelId == 0) return true;
                var dm = DataManager.Instance;
                return dm.GetSpawnedObjects().TryGetValue(remodelId, out var o) && o != null && o != remodelOld && !loadingGlb().Contains(remodelId);
            }, 20f, "새 모델로 다시 뜸");
            Do(() =>
            {
                if (remodelId == 0) { log.Add("SKIP 떠 있던 장소가 새 3D 모델로 다시 뜬다 (떠 있는 큐브 장소 없음)"); return; }
                var dm = DataManager.Instance;
                dm.GetSpawnedObjects().TryGetValue(remodelId, out var o);
                var loader = o != null ? o.GetComponent<GLBModelLoader>() : null;
                bool ok = o != null && o != remodelOld && o.name == $"Place_{remodelId}_custom" && loader != null && loader.IsModelLoaded
                          && dm.GetPlaceDataMap()[remodelId].model_url == Remodel && (remodelOld == null || !remodelOld.activeSelf || remodelOld == o);
                log.Add((ok ? "PASS " : "FAIL ") + "떠 있던 큐브 장소가 새 3D 모델로 그 자리에서 다시 뜬다 (옛 큐브는 내림)");
                log.Add($"  {o?.name} · 모델 다 뜸 {loader?.IsModelLoaded}");
                // 되돌리기 — 모델 풀로 내리고 원래 값으로 다시 띄운다
                dm.DespawnFullObject(remodelId.ToString());
                dm.GetPlaceDataMap()[remodelId] = remodelOrig;
                dm.SpawnFullObject(remodelId.ToString());
                GLBModelLoader.ClearCacheForUrl(Remodel);
            });
            Sleep(0.5f);
            Do(() =>
            {
                if (remodelId == 0) return;
                var dm = DataManager.Instance;
                bool ok = dm.GetSpawnedObjects().TryGetValue(remodelId, out var o) && o != null && o.name == $"Place_{remodelId}_{remodelOrig.model_type}"
                          && dm.GetPlaceDataMap()[remodelId] == remodelOrig;
                log.Add((ok ? "PASS " : "FAIL ") + "되돌림: 원래 장소 값 · 원래 모습으로 다시 뜸");
            });

            // 10) 글자가 칸 안에 — 5개 언어 · 「AI 작업」 · 네 칸 이름(굵게) · 요청사항 안내 · 만들기 · 취소|확인 · 다 만든 안내 · 처음 안내(사진 있음/없음) · 사진 없이 01~03
            openRequest("다시 엶 (글자)");
            Do(() => { btn("aiButton").onClick.Invoke(); chip(3).onClick.Invoke(); });   // 요청사항 칸까지 펴 둔다
            Wait(madeChar, 40f, "글자 시험용 캐릭터");
            Check(() =>
            {
                var bad = new List<string>();
                var locs = new List<R0926LocalizedText>(part("view").GetComponentsInChildren<R0926LocalizedText>(true));
                locs.AddRange(btn("aiButton").GetComponentsInChildren<R0926LocalizedText>(true));
                var chipTexts = new HashSet<Text>((Text[])av("chipLabels"));
                var doneM = P.GetMethod("Done", BindingFlags.NonPublic | BindingFlags.Static);
                string st0 = status().text;
                var m = ai.Made;
                var toon = new R0926AiWork.Result { Style = R0926AiWork.Style.Toon, Clear = R0926AiImage.ClearMode.Photo };
                var pencil = new R0926AiWork.Result { Style = R0926AiWork.Style.Pencil, Clear = R0926AiImage.ClearMode.Logo };
                var alpha = new R0926AiWork.Result { Style = R0926AiWork.Style.None, Clear = R0926AiImage.ClearMode.Alpha };
                try
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (string lang in langs)
                    {
                        AppLanguage.EditorOverride = lang;
                        foreach (var l in locs)
                        {
                            var t = l.GetComponent<Text>();
                            l.Set(l.ko, l.en, l.ja, l.zh, l.es);
                            string why = spill(t, chipTexts.Contains(t));
                            if (why != null) bad.Add($"{lang}/{l.transform.parent.name}/{l.name} {why} 「{t.text}」");
                        }
                        foreach (var r in new[] { m, toon, pencil, alpha })
                        {
                            status().text = (string)doneM.Invoke(null, new object[] { r });
                            string why = spill(status(), false);
                            if (why != null) bad.Add($"{lang}/다 만든 안내 {why} 「{status().text}」");
                        }
                    }
                }
                finally
                {
                    AppLanguage.EditorOverride = lang0;
                    foreach (var l in locs) l.Set(l.ko, l.en, l.ja, l.zh, l.es);
                    status().text = st0;
                }
                log.Add($"  글자 {locs.Count}칸 × 5개 언어 · 다 만든 안내 4가지");
                foreach (var b in bad) log.Add("  넘침: " + b);
                return bad.Count == 0;
            }, "글자가 칸 안에 — 5개 언어 · AI 작업 · 네 칸(굵게) · 요청사항 · 만들기 · 취소|확인 · 다 만든 안내");
            Check(() =>
            {
                var bad = new List<string>();
                Texture2D mp = fix.MainPhoto;
                var icons = (Image[])av("chipIcons");
                bool dimOk = true;
                try
                {
                    foreach (string lang in langs)
                    {
                        AppLanguage.EditorOverride = lang;
                        foreach (bool noPhoto in new[] { false, true })
                        {
                            btn("viewCancel").onClick.Invoke();
                            if (noPhoto) fs("mainPhoto", null);
                            btn("aiButton").onClick.Invoke();   // 다시 열면 그 언어 처음 안내
                            string why = spill(status(), false);
                            if (why != null) bad.Add($"{lang}/처음 안내{(noPhoto ? "(사진 없음)" : "")} {why} 「{status().text}」");
                            if (noPhoto)
                            {
                                dimOk &= icons[0].color.a < 0.5f && icons[1].color.a < 0.5f && icons[2].color.a < 0.5f && icons[3].color.a > 0.9f;
                                chip(1).onClick.Invoke();   // 사진 없이 만화톤 → 「먼저 로고 사진을…」 (만들지 않는다)
                                why = spill(status(), false);
                                if (why != null) bad.Add($"{lang}/사진 없이 고름 {why} 「{status().text}」");
                                dimOk &= !ai.Working && ai.Style == R0926AiWork.Style.None && !chipOn(1);
                            }
                            fs("mainPhoto", mp);
                        }
                    }
                }
                finally
                {
                    AppLanguage.EditorOverride = lang0;
                    fs("mainPhoto", mp);
                    btn("viewCancel").onClick.Invoke();
                }
                log.Add($"  사진 없을 때 01~03 흐림 · 골라도 안 만듦 {dimOk}");
                foreach (var b in bad) log.Add("  넘침: " + b);
                return bad.Count == 0 && dimOk;
            }, "안내 한 줄이 칸 안에 — 5개 언어 · 처음 안내 · 로고 사진이 없을 때 · 사진 없이 01~03 을 고를 때 (흐리게 · 안 만듦)");

            // 되돌리기 — AI 화면 · 창 · 서버 주소 · 언어
            Do(() =>
            {
                if (ai != null && ai.Open) btn("viewCancel").onClick.Invoke();
                if (panel() != null && panel().activeSelf) { panel().SetActive(false); call("ResetToInitialState", null); }
                CubeDataFixManager.EditorBase = null;
                AppLanguage.EditorOverride = lang0;
                if (hadDrop) PlayerPrefs.SetInt(DropKey, sDrop); else PlayerPrefs.DeleteKey(DropKey);
                PlayerPrefs.Save();
                R0926MenuStyle.Reload();
                DataManager.Instance?.GetPlaceDataMap().Remove(OwnerPlace);
                if (ownerOk) FixHttp("/__owner/teardown", "{}");
                Application.logMessageReceived -= onLog;
                foreach (var e in errors) log.Add("  오류: " + e);
            });
            Check(() => errors.Count == 0, "오류 없음 (고치기 창 · AI 작업 · 보내기)");
        }

        private const int OwnerPlace = 200;   // 시험 서버 /__owner/setup 의 회원 13 장소
    }
}
