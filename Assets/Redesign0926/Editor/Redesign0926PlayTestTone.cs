using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-18 H4 — 대표님 「AR화면 내에서 인디케이터 … 현재톤을 유지한 채로 기존의 밝은 색(흰색, 초록색 etc.) 은 유지하고
    /// 카페 같이 짙은 느낌은 좀 더 밝은 톤으로 … 셰도우가 어두우니까 … 글자, 텍스트가 안보이는」.
    ///  · h4tone : ① 밝은 색(흰색 · 초록 · 금색 · 노랑 · 주황)은 그대로, 짙은 색은 같은 색조로 주황만큼 밝게 (투명도 그대로)
    ///             ② 실제 인디케이터 — 박스 · 이름 · 거리 · 화살표가 밝힌 색으로 그려지고, Target 색(목록 · 칩 · 3D 와 같은 값)은 그대로
    /// 시험용 상자만 만들고 끝나면 지운다 — 서버 · 장소 데이터는 건드리지 않는다.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanIndicatorTone()
        {
            Camera cam = null;
            Behaviour pd = null;
            bool pdWas = false;
            Vector3 camPos0 = Vector3.zero;
            Quaternion camRot0 = Quaternion.identity;
            Target tpl = null;
            var made = new List<GameObject>();
            var live = new List<KeyValuePair<string, Target>>();
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Exception) errors.Add(msg);
            };
            Func<Color, string> hex = c => ColorUtility.ToHtmlStringRGB(c);
            Func<Color, Color, float, bool> same = (a, b, e) =>
                Mathf.Abs(a.r - b.r) <= e && Mathf.Abs(a.g - b.g) <= e && Mathf.Abs(a.b - b.b) <= e && Mathf.Abs(a.a - b.a) <= e;
            Func<Color, float> lum = c =>
                0.2126f * Mathf.GammaToLinearSpace(c.r) + 0.7152f * Mathf.GammaToLinearSpace(c.g) + 0.0722f * Mathf.GammaToLinearSpace(c.b);
            Func<Color, float> vsBlack = c => (lum(c) + 0.05f) / 0.05f;   // 검은 그림자와의 대비 (WCAG)
            Func<float, float, float> hueGap = (a, b) => Mathf.Abs(Mathf.DeltaAngle(a, b));
            Func<string, Color> hx = s => ColorUtility.TryParseHtmlString("#" + s, out var c) ? c : Color.magenta;

            // 밝은 색 — 그대로 (흰색 · 공원 초록 · 랜드마크 금색 · 음식 주황 · 관광 노랑 · 보물 금색 · 역/터미널 초록)
            var bright = new List<KeyValuePair<string, Color>>
            {
                new KeyValuePair<string, Color>("흰색(기타·분류 없음)", DataManager.GetCategoryColor("")),
                new KeyValuePair<string, Color>("공원 초록", DataManager.GetCategoryColor("park")),
                new KeyValuePair<string, Color>("랜드마크 금색", DataManager.GetCategoryColor("landmark")),
                new KeyValuePair<string, Color>("음식 주황", DataManager.GetCategoryColor("food")),
                new KeyValuePair<string, Color>("관광 노랑", hx("FBC15D")),
                new KeyValuePair<string, Color>("보물 금색", new Color(0.910f, 0.749f, 0.310f, 1f)),
                new KeyValuePair<string, Color>("역·터미널 초록", Color.green),
            };
            // 짙은 색 — 같은 색조로 밝게 (카페 모카 · 샵 파랑 · 화장실 보라 · 스포츠 빨강 · 공공 회청 · 교육 하늘 · 의료 청록 · 복지 · 지하철 · 사람 분홍)
            var dark = new List<KeyValuePair<string, Color>>
            {
                new KeyValuePair<string, Color>("카페 모카", DataManager.GetCategoryColor("cafe")),
                new KeyValuePair<string, Color>("샵 파랑", DataManager.GetCategoryColor("shop")),
                new KeyValuePair<string, Color>("화장실 보라", DataManager.GetCategoryColor("toilet")),
                new KeyValuePair<string, Color>("스포츠 빨강", DataManager.GetCategoryColor("sport")),
                new KeyValuePair<string, Color>("공공 회청", DataManager.GetCategoryColor("gov")),
                new KeyValuePair<string, Color>("교육 하늘", DataManager.GetCategoryColor("edu")),
                new KeyValuePair<string, Color>("의료 청록", DataManager.GetCategoryColor("medical")),
                new KeyValuePair<string, Color>("복지 청록그린", DataManager.GetCategoryColor("welfare")),
                new KeyValuePair<string, Color>("지하철 청록", hx("3DA29C")),
                new KeyValuePair<string, Color>("사람 분홍", new Color(0.914f, 0.325f, 0.514f, 1f)),
                new KeyValuePair<string, Color>("서버 짙은 회색", hx("333333")),
            };

            // ① 색 바꾸기 규칙 (인디케이터 없이 함수만)
            Do(() =>
            {
                foreach (var kv in bright)
                    log.Add($"  {kv.Key} #{hex(kv.Value)} 밝기 {R0926IndicatorTone.Lightness(kv.Value):0.000} → #{hex(R0926IndicatorTone.Lift(kv.Value))}");
                foreach (var kv in dark)
                {
                    Color o = R0926IndicatorTone.Lift(kv.Value);
                    log.Add($"  {kv.Key} #{hex(kv.Value)} → #{hex(o)} · 밝기 {R0926IndicatorTone.Lightness(kv.Value):0.000} → {R0926IndicatorTone.Lightness(o):0.000}" +
                            $" · 색조 {R0926IndicatorTone.Hue(kv.Value):0.0}° → {R0926IndicatorTone.Hue(o):0.0}° · 검은 그림자 대비 {vsBlack(kv.Value):0.0} → {vsBlack(o):0.0}");
                }
            });
            Check(() =>
            {
                foreach (var kv in bright) if (R0926IndicatorTone.Lift(kv.Value) != kv.Value) { log.Add("  바뀜: " + kv.Key); return false; }
                return true;
            }, "밝은 색(흰색 · 초록 · 금색 · 노랑 · 주황)은 그대로");
            Check(() =>
            {
                foreach (var kv in dark)
                {
                    Color o = R0926IndicatorTone.Lift(kv.Value);
                    float l0 = R0926IndicatorTone.Lightness(kv.Value), l1 = R0926IndicatorTone.Lightness(o);
                    if (l1 < R0926IndicatorTone.MinLightness - 0.004f || l1 <= l0 + 0.03f) { log.Add($"  덜 밝아짐: {kv.Key} {l0:0.000} → {l1:0.000}"); return false; }
                }
                return true;
            }, "짙은 색은 모두 주황(음식)만큼 밝아진다");
            Check(() =>
            {
                foreach (var kv in dark)
                {
                    if (kv.Key == "서버 짙은 회색") continue;   // 무채색은 색조가 없다
                    Color o = R0926IndicatorTone.Lift(kv.Value);
                    if (hueGap(R0926IndicatorTone.Hue(kv.Value), R0926IndicatorTone.Hue(o)) > 1.5f) { log.Add("  색조 바뀜: " + kv.Key); return false; }
                }
                return true;
            }, "색조는 그대로 — 카페는 밝은 카페색, 파랑은 밝은 파랑 (1.5° 안)");
            Check(() =>
            {
                Color o = R0926IndicatorTone.Lift(hx("333333"));
                return Mathf.Abs(o.r - o.g) < 0.01f && Mathf.Abs(o.g - o.b) < 0.01f;
            }, "무채색(회색)은 밝은 회색으로 — 색이 끼지 않는다");
            Check(() =>
            {
                foreach (var kv in dark) if (vsBlack(R0926IndicatorTone.Lift(kv.Value)) < 8f) { log.Add("  대비 모자람: " + kv.Key); return false; }
                return true;
            }, "검은 그림자와의 대비가 모두 8 이상 (카페 모카는 5.2 였다)");
            Check(() =>
            {
                Color half = DataManager.GetCategoryColor("cafe"); half.a = 0.5f;
                return Mathf.Abs(R0926IndicatorTone.Lift(half).a - 0.5f) < 1e-4f && R0926IndicatorTone.Lift(Color.clear) == Color.clear;
            }, "투명도는 그대로 (멀어지며 옅어지는 화살표 · 글 없는 칸)");

            // ② 실제 인디케이터 — 장소 큐브 틀과 같은 모양으로 상자를 화면에 두 줄 다섯 칸 늘어놓는다
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
                cam = Camera.main;
                DeviceFov();
                foreach (var b in cam.GetComponents<Behaviour>()) if (b.GetType().Name.Contains("PoseDriver")) pd = b;
                pdWas = pd != null && pd.enabled;
                if (pd != null) pd.enabled = false;   // 시험 동안 카메라는 이 시험이 돌린다
                camPos0 = cam.transform.position;
                camRot0 = cam.transform.rotation;
                cam.transform.rotation = Quaternion.identity;
                tpl = DataManager.Instance != null && DataManager.Instance.cubePrefab != null ? DataManager.Instance.cubePrefab.GetComponentInChildren<Target>(true) : null;
                errors.Clear();
                Application.logMessageReceived -= onLog;
                Application.logMessageReceived += onLog;

                var show = new List<KeyValuePair<string, Color>>
                {
                    new KeyValuePair<string, Color>("카페", DataManager.GetCategoryColor("cafe")),
                    new KeyValuePair<string, Color>("샵", DataManager.GetCategoryColor("shop")),
                    new KeyValuePair<string, Color>("화장실", DataManager.GetCategoryColor("toilet")),
                    new KeyValuePair<string, Color>("스포츠", DataManager.GetCategoryColor("sport")),
                    new KeyValuePair<string, Color>("공공기관", DataManager.GetCategoryColor("gov")),
                    new KeyValuePair<string, Color>("공원", DataManager.GetCategoryColor("park")),
                    new KeyValuePair<string, Color>("랜드마크", DataManager.GetCategoryColor("landmark")),
                    new KeyValuePair<string, Color>("음식점", DataManager.GetCategoryColor("food")),
                    new KeyValuePair<string, Color>("의료", DataManager.GetCategoryColor("medical")),
                    new KeyValuePair<string, Color>("기타", DataManager.GetCategoryColor("")),
                };
                Vector3 c0 = cam.transform.position;
                for (int i = 0; i < show.Count; i++)
                {
                    float yaw = (i % 2 == 0 ? -7f : 7f), pitch = 22f - (i / 2) * 11f;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "H4Test_" + show[i].Key;
                    UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                    go.transform.position = c0 + Quaternion.Euler(-pitch, yaw, 0f) * new Vector3(0f, 0f, 40f + i);
                    go.transform.localScale = Vector3.one * 1.5f;
                    var t = go.AddComponent<Target>();
                    if (tpl != null) t.CopyLook(tpl);
                    t.TargetColor = show[i].Value;
                    t.PlaceName = show[i].Key;
                    live.Add(new KeyValuePair<string, Target>(show[i].Key, t));
                    made.Add(go);
                }
            });
            Sleep(1.2f);
            Func<Target, Image> img = t => t != null && t.indicator != null ? t.indicator.GetComponent<Image>() : null;
            Func<Target, string, Text> txt = (t, f) => t != null && t.indicator != null ? typeof(Indicator).GetField(f, NPI)?.GetValue(t.indicator) as Text : null;
            Do(() =>
            {
                foreach (var kv in live)
                {
                    var t = kv.Value;
                    var im = img(t);
                    var nt = txt(t, "nameText");
                    log.Add($"  {kv.Key}: 인디케이터 {(t.indicator != null && t.indicator.Active ? t.indicator.Type.ToString() : "없음")} · Target #{hex(t.TargetColor)} · 박스 #{(im != null ? hex(im.color) : "-")} · 이름 #{(nt != null ? hex(nt.color) : "-")}");
                }
            });
            Check(() => tpl != null, "준비: 장소 큐브 틀의 Target (모양 · 크기 같게)");
            Check(() =>
            {
                foreach (var kv in live)
                {
                    var t = kv.Value;
                    if (t.indicator == null || !t.indicator.Active || t.indicator.Type != IndicatorType.BOX) { log.Add("  박스 없음: " + kv.Key); return false; }
                    Color want = R0926IndicatorTone.Lift(t.TargetColor);
                    var im = img(t);
                    var nt = txt(t, "nameText");
                    var dt = txt(t, "distanceText");
                    if (im == null || !same(im.color, want, 0.004f)) { log.Add("  박스 색 다름: " + kv.Key); return false; }
                    if (nt != null && !same(nt.color, want, 0.004f)) { log.Add("  이름 색 다름: " + kv.Key); return false; }
                    if (dt != null && !same(dt.color, want, 0.004f)) { log.Add("  거리 색 다름: " + kv.Key); return false; }
                }
                return live.Count == 10;
            }, "화면 안 박스 10개 — 박스 · 이름 · 거리가 밝힌 색으로 그려진다");
            Check(() =>
            {
                foreach (var kv in live)
                {
                    string cat = kv.Key == "카페" ? "cafe" : kv.Key == "샵" ? "shop" : kv.Key == "화장실" ? "toilet" : kv.Key == "스포츠" ? "sport" : kv.Key == "공공기관" ? "gov"
                               : kv.Key == "공원" ? "park" : kv.Key == "랜드마크" ? "landmark" : kv.Key == "음식점" ? "food" : kv.Key == "의료" ? "medical" : "";
                    if (kv.Value.TargetColor != DataManager.GetCategoryColor(cat)) { log.Add("  Target 색 바뀜: " + kv.Key); return false; }
                }
                return true;
            }, "Target 색(목록 · 칩 · 3D 와 같은 값)은 그대로 — 그릴 때만 밝힌다");
            Check(() =>
            {
                var cafe = live.Find(kv => kv.Key == "카페").Value;
                var park = live.Find(kv => kv.Key == "공원").Value;
                var white = live.Find(kv => kv.Key == "기타").Value;
                return img(cafe) != null && R0926IndicatorTone.Lightness(img(cafe).color) > R0926IndicatorTone.Lightness(cafe.TargetColor) + 0.1f
                       && img(park) != null && img(park).color == park.TargetColor && img(white) != null && img(white).color == Color.white;
            }, "카페 박스는 눈에 띄게 밝아지고(밝기 +0.1 넘게), 공원 초록 · 흰색 박스는 그대로");
            Shot("h4tone_boxes");

            // 등 돌리면 화살표 — 화살표 · 글자도 같은 밝힌 색
            Do(() => cam.transform.rotation = Quaternion.Euler(0f, 180f, 0f));
            Sleep(1.0f);
            Check(() =>
            {
                var cafe = live.Find(kv => kv.Key == "카페").Value;
                var im = img(cafe);
                var dt = txt(cafe, "distanceText");
                bool ok = cafe.indicator != null && cafe.indicator.Active && cafe.indicator.Type == IndicatorType.ARROW && im != null
                          && same(im.color, R0926IndicatorTone.Lift(cafe.TargetColor), 0.004f) && (dt == null || same(dt.color, R0926IndicatorTone.Lift(cafe.TargetColor), 0.004f));
                log.Add($"  등 돌림 — 카페 {(cafe.indicator != null && cafe.indicator.Active ? cafe.indicator.Type.ToString() : "없음")} · 화살표 #{(im != null ? hex(im.color) : "-")} · 글 #{(dt != null ? hex(dt.color) : "-")}");
                return ok;
            }, "화면 밖 화살표 · 이름 · 거리도 같은 밝힌 색");
            Shot("h4tone_arrows");

            Do(() =>
            {
                foreach (var g in made) if (g != null) UnityEngine.Object.Destroy(g);
                made.Clear();
                live.Clear();
                if (cam != null) cam.transform.SetPositionAndRotation(camPos0, camRot0);
                if (pd != null) pd.enabled = pdWas;
                Application.logMessageReceived -= onLog;
                if (errors.Count > 0) log.Add("  예외: " + errors[0]);
            });
            Sleep(0.3f);
            Check(() => errors.Count == 0, "예외 없음");
        }
    }
}
