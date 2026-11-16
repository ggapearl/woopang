using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-15 (B) 아래 길안내 화살표 시험 — R0926GuideArrow (시안 B1 「화살표」).
    ///  · guidearrow : 알약 「이름 거리 · 방향」 · 방향 말 · 화살표 그림이 가리키는 쪽 · 처음 3초 안내 줄 · 알약 누르기 · 20m · 도착 · 20분 ·
    ///                창 · 하늘 · 가까이 카드에 숨기 · 가까이 카드가 이어 받기 · 아래 고정 하늘 카드 위로 · 뒤를 가리켜도 주소줄 위 ·
    ///                물방울 / 기본 메뉴 · 목록 줄 누르기 (노치 없는 기기 · 노치 아이폰 — 노치와 같이 끝) ·
    ///                알약 오른쪽 × (11-16 — 한 번에 끝 · 노치도 같이 · 긴 이름도 × 와 겹치지 않게) ·
    ///                좌표로만 안내하면 10m 도착 · 정보 창 위 「길안내」(두 번 누른 창 · 목록 사진 창) · 지도 핀 「길안내」 ·
    ///                땅 위 거리(높이 무시) · 사람 줄은 안내하지 않음 (11-16)
    /// 운영 서버에 쓰지 않는다 — 하늘 옆 장 · 근처 사람 · 보물은 닿지 않는 주소로.
    /// </summary>
    public static partial class Redesign0926PlayTest
    {
        private static void PlanGuideArrow()
        {
            R0926GuideArrow ga = null;
            R0926Notch notch = null;
            R0926SkyWeather sky = null;
            RectTransform skyCard = null;
            GameObject cube = null;
            Target cubeT = null;
            bool noNotchWas = R0926Notch.EditorForceNoNotch;
            object pickedRow = null;
            string pickedName = "";
            var low = new float[1];
            var errors = new List<string>();
            const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            R0926DetailGuide dg = null;
            R0926FriendsMap map = null;
            Target dgTarget = null;
            string dgName = null, hostName = null, mapViewWas = null;
            float dgLat = 0f, dgLon = 0f;
            bool personTried = false;
            var listMax = new float[1];
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error && (msg + stack).Contains("R0926")) errors.Add(msg);
            };
            // 가상 위치에서 북쪽 · 동쪽으로 몇 m 떨어진 자리로 안내
            Action<double, double, string, Target> go = (north, east, name, t) =>
            {
                var v = VirtualLocation.Instance;
                double lat = v.Latitude + north / 111320.0;
                double lon = v.Longitude + east / (111320.0 * Math.Cos(v.Latitude * Math.PI / 180.0));
                R0926GuideArrow.Guide((float)lat, (float)lon, t, name);
            };
            Func<string> text = () => GaPlain(ga != null ? ga.EditorLabel : null);
            Func<string> hintText = () => ga != null && ga.EditorGroup != null ? ga.EditorGroup.Find("Hint")?.GetComponent<Text>()?.text ?? "" : "";
            Func<Vector2?> pillAt = () => ga != null ? SkyScreenRect(ga.EditorPill)?.center : null;
            Func<Vector2?> closeAt = () => ga != null && ga.EditorClose != null ? SkyScreenRect((RectTransform)ga.EditorClose.transform)?.center : null;
            Func<Vector2?> notchAt = () => notch != null ? SkyScreenRect(notch.EditorShape)?.center : null;
            Func<bool> notchUp = () => notch != null && notch.EditorShape.gameObject.activeSelf && notch.EditorOpen > 0.95f;
            Func<string> state = () => ga == null ? "화살표 없음"
                : $"안내 {(ga.EditorGuiding ? "중" : "끝")} · {(ga.EditorShowing ? "보임" : ga.EditorHidden ? "숨음" : "나타나는/사라지는 중")} · '{text()}' · 화살표 {ga.EditorYaw:0}°{(ga.EditorKnown ? "" : "(모름)")} · 안내 줄 {(ga.EditorHint ? "'" + hintText() + "'" : "없음")} · {ga.EditorMeters:0.0}m";
            // 바닥 — 주소줄 · 도크 중 높은 위쪽 (화면 px)
            Func<float> floorTop = () =>
            {
                float top = 0f;
                foreach (var n in new[] { "LocationManagerPanel", "Dock0926" })
                {
                    var r = SkyScreenRect(Find(n) as RectTransform);
                    if (r.HasValue) top = Mathf.Max(top, r.Value.yMax);
                }
                return top;
            };
            // 화살표 그림이 가리키는 쪽 — 판에서 오른쪽 + · 앞(화면 위) + 로 yaw 와 같은 쪽인지
            Func<float, bool> pointsAt = yaw =>
            {
                if (!GaDrawn(ga, out var d, out _, out _) || d.sqrMagnitude < 1f) return false;
                var want = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                return Vector2.Dot(d.normalized, want) > 0.7f;
            };
            // 목록의 장소 줄 — 방향이 있는 장소, 30m 넘게 떨어진 것부터 (가까우면 「거의 다 왔어요」 · 도착으로 이름이 안 보인다)
            Func<object> pickRow = () =>
            {
                var pr = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>();
                var list = pr != null ? typeof(R0926PlaceRows).GetField("rows", NmF)?.GetValue(pr) as System.Collections.IList : null;
                if (list == null) return null;
                object near = null;
                foreach (var r in list)
                {
                    var rt = r.GetType();
                    var g = rt.GetField("go").GetValue(r) as GameObject;
                    if (g == null || !g.activeInHierarchy || g.GetComponent<Button>() == null) continue;
                    if ((PlaceListManager.EntryKind)rt.GetField("kind").GetValue(r) != PlaceListManager.EntryKind.Place || (bool)rt.GetField("noDirection").GetValue(r)) continue;
                    if (string.IsNullOrEmpty(rt.GetField("key").GetValue(r) as string)) continue;
                    float m = GaMeters(rt.GetField("distLabel").GetValue(r) as string);
                    if (m > 30f && m < 50000f) return r;
                    if (near == null) near = r;
                }
                return near;
            };
            Action tapPicked = () =>
            {
                pickedRow = pickRow();
                if (pickedRow == null) { log.Add("  장소 줄 없음"); return; }
                var rt = pickedRow.GetType();
                pickedName = rt.GetField("key").GetValue(pickedRow) as string;
                log.Add("  누른 줄: " + pickedName + " [" + rt.GetField("distLabel").GetValue(pickedRow) + "]");
                (rt.GetField("go").GetValue(pickedRow) as GameObject).GetComponent<Button>().onClick.Invoke();
            };
            // 그 장소가 AR 에 떠 있지 않은 길 (멀어서 아직 안 뜬 장소) — 떠 있는 AR 오브젝트를 끈다
            Action hideTargets = () => { foreach (var t in UnityEngine.Object.FindObjectsByType<Target>(FindObjectsInactive.Exclude)) t.gameObject.SetActive(false); };
            // 알약 제목 — 이름 (20m 안이면 「거의 다 왔어요」, 도착하면 「도착했어요」)
            Func<bool> titled = () =>
            {
                string s = text(), n = GaPlain(pickedName).Trim();
                if (n.Length > 6) n = n.Substring(0, 6);
                return n.Length > 0 && s.StartsWith(n) || s.StartsWith("거의 다 왔어요") || s.StartsWith("도착했어요");
            };
            Func<string, bool> titledAs = nm =>
            {
                string s = text(), n = GaPlain(nm).Trim();
                if (n.Length > 6) n = n.Substring(0, 6);
                return n.Length > 0 && s.StartsWith(n) || s.StartsWith("거의 다 왔어요") || s.StartsWith("도착했어요");
            };
            // 안내 중인 장소 — 이름 · AR 오브젝트 · 자리 (R0926GuideArrow 안)
            Func<string> gName = () => typeof(R0926GuideArrow).GetField("gName", NmF)?.GetValue(ga) as string;
            Func<Target> gTarget = () => typeof(R0926GuideArrow).GetField("gTarget", NmF)?.GetValue(ga) as Target;
            Func<string, float> gF = n => (float)typeof(R0926GuideArrow).GetField(n, NmF).GetValue(ga);
            Func<string, string> clean = s => typeof(R0926Notch).GetMethod("Clean", SF)?.Invoke(null, new object[] { s }) as string;
            // 정보 창(두 번 누른 창)이 다 떠 있다
            Func<bool> infoOpen = () => { var d = DoubleTap3D.Shown; return d != null && Active("FullScreenPanel") && d.fullscreenCanvasGroup != null && d.fullscreenCanvasGroup.alpha >= 0.99f; };
            // 정보 창 위 「길안내」가 보이고 누를 수 있다
            Func<bool> dgOn = () => { var g = dg != null ? dg.GetComponent<CanvasGroup>() : null; return g != null && dg.gameObject.activeInHierarchy && g.alpha > 0.99f && g.interactable && g.blocksRaycasts; };
            Func<string, Rect?> topRect = n => dg != null ? SkyScreenRect(dg.transform.parent.Find(n) as RectTransform) : null;
            Func<CanvasGroup> listGroup = () => Find("ListPanel")?.GetComponent<CanvasGroup>();
            Func<System.Collections.IList> listRows = () =>
            {
                var pr = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>();
                return pr != null ? typeof(R0926PlaceRows).GetField("rows", NmF)?.GetValue(pr) as System.Collections.IList : null;
            };
            Func<RectTransform> bubble = () => map != null ? typeof(R0926FriendsMap).GetField("dropBubble", NmF)?.GetValue(map) as RectTransform : null;
            Func<RectTransform> mapGuide = () => bubble()?.Find("Guide") as RectTransform;

            if (!string.IsNullOrEmpty(storeSrc)) Do(() => { Backdrop("bg_city.png"); DeviceFov(); });
            NmBoot();
            NmWaitPlaces(1, true);
            Do(() =>
            {
                if (nmPicked.Count == 0) throw new Exception("AR 에 보이는 우팡 큐브가 없다");
                cube = nmPicked[0];
                cubeT = cube.GetComponentInChildren<Target>(true);
                NmClearAround();
                NmPut(cube, 9f, 30f);
                ga = R0926GuideArrow.Instance;
                notch = R0926Notch.Instance;
                R0926Notch.EditorForceNoNotch = true;   // 처음은 노치 없는 기기 (안드로이드 · 노치 없는 아이폰) — 화살표만
                notch?.EditorRemeasure();
                StashSkyPrefs();
                foreach (var k in new[] { "Sky_Enabled", "Sky_Weather", "Sky_Events" }) PlayerPrefs.SetInt(k, 1);
                PlayerPrefs.SetInt("Sky_Angle", 1);   // 28°
                foreach (var k in new[] { "Sky_StartCollapsed", "Sky_Swiped", "Sky_PinUsed" }) PlayerPrefs.SetInt(k, 0);
                R0926Nearby.EditorBase = NmDeadStub;
                R0926HuntApi.EditorBase = NmDeadStub;
                R0926FriendsMap.EditorBase = NmDeadStub;
                sky = UnityEngine.Object.FindAnyObjectByType<R0926SkyWeather>(FindObjectsInactive.Include);
                var pg = UnityEngine.Object.FindAnyObjectByType<R0926SkyPages>(FindObjectsInactive.Include);
                if (sky != null) typeof(R0926SkyWeather).GetField("collapsed", NmF)?.SetValue(sky, false);
                skyCard = pg != null && pg.EditorFrame != null ? pg.EditorFrame.Find("Glass0926") as RectTransform : null;
                InjectWeather();
                R0926MenuStyle.Droplet = true;
                errors.Clear();
                Application.logMessageReceived -= onLog;
                Application.logMessageReceived += onLog;
                ptTouch = InputSystem.AddDevice<Touchscreen>("PlayTestFinger");
                R0926Notch.EditorHeading = 0f;   // 북쪽을 보고 있다
                if (ga != null)
                {
                    var cv = typeof(R0926GuideArrow).GetField("covers", NmF)?.GetValue(ga) as GameObject[];
                    var up = new List<string>();
                    if (cv != null) foreach (var c in cv) if (c != null && c.activeInHierarchy) up.Add(c.name);
                    log.Add($"  덮는 창 {cv?.Length ?? 0}개 · 지금 떠 있는 것: {(up.Count == 0 ? "없음" : string.Join(", ", up))} · 고른 큐브 {NmName(cube)}");
                }
            });
            Check(() => ga != null && ga.isActiveAndEnabled && ga.EditorGroup != null && ga.EditorPill != null && ga.EditorMesh != null && notch != null && sky != null && skyCard != null,
                "준비: 길안내 화살표 (GuideArrow0926 · R0926GuideArrow) · 노치 · 하늘 카드");
            Check(() => ga.EditorHidden && !ga.EditorGuiding, "처음엔 화살표 없음 (안내 전)");

            // 1) 북쪽 200m — 북쪽을 보면 알약 「시험 북쪽 200m  200m · 앞」, 화살표는 앞, 처음 3초는 알약 위 「누르면 안내를 끝내요」
            Do(() => go(200, 0, "시험 북쪽 200m", null));
            Wait(() => ga.EditorShowing, 3f, "안내 시작 — 화살표 뜸");
            Sleep(0.5f);
            Do(() => log.Add("  " + state()));
            Check(() => ga.EditorGuiding && text() == "시험 북쪽 200m  200m · 앞", "알약 — 「시험 북쪽 200m  200m · 앞」 (이름 · 거리 · 방향 말)");
            Check(() => ga.EditorKnown && Mathf.Abs(Mathf.DeltaAngle(ga.EditorYaw, 0f)) < 3f && pointsAt(0f), "북쪽을 보면 화살표가 앞(화면 위)을 가리킨다");
            Check(() => ga.EditorHint && hintText() == "누르면 안내를 끝내요", "처음 3초 — 알약 위 「누르면 안내를 끝내요」");
            Shot("ga_start");

            // 2) 방향 말 — 시안 그대로 (15° 안 앞 · 70° 안 옆 앞 · 110° 안 옆 · 150° 넘으면 뒤)
            foreach (var (heading, word, yaw) in new[] { (315f, "오른쪽 앞", 45f), (270f, "오른쪽", 90f), (240f, "오른쪽 뒤", 120f), (180f, "뒤", 180f), (90f, "왼쪽", -90f), (30f, "왼쪽 앞", -30f) })
            {
                Do(() => R0926Notch.EditorHeading = heading);
                Sleep(1.2f);
                Do(() => log.Add($"  방위 {heading:0}°: " + state()));
                Check(() => text() == "시험 북쪽 200m  200m · " + word && Mathf.Abs(Mathf.DeltaAngle(ga.EditorYaw, yaw)) < 3f && pointsAt(yaw),
                    $"방위 {heading:0}° 를 보면 북쪽 장소는 「{word}」 — 화살표도 그쪽 ({yaw:0}°)");
                if (heading == 315f) Shot("ga_right_ahead");
                if (heading == 180f)
                {
                    Shot("ga_behind");
                    GaSampleLow(() => ga, low);   // 꺾쇠가 깜빡이는 한 바퀴 동안 가장 낮은 점
                    Do(() => log.Add($"  뒤를 가리킬 때 그림의 가장 낮은 점 y {low[0]:0} · 바닥(주소줄 · 도크 위) y {floorTop():0}"));
                    Check(() => low[0] < float.MaxValue && low[0] >= floorTop() - 1f, "뒤를 가리켜도 꺾쇠 · 화살표가 주소줄 · 도크를 덮지 않는다 (바닥 앞에서 스러짐)");
                }
            }
            Do(() => { R0926Notch.EditorHeading = 0f; go(2500, 0, "시험 북쪽 2.5km", null); });
            Sleep(0.8f);
            Check(() => text() == "시험 북쪽 2.5km  2.5km · 앞", "1km 넘으면 「2.5km」");

            // 3) 알약 누르기 — 3초 뒤 안내 줄이 사라지고, 누르면 그 말이 다시 3초, 그 말이 보일 때 누르면 끝
            Sleep(3.4f);
            Check(() => ga.EditorGuiding && !ga.EditorHint, "3초 뒤 안내 줄은 사라지고 안내는 이어짐");
            Press(pillAt, 0.08f, null);
            Sleep(0.5f);
            Check(() => ga.EditorGuiding && ga.EditorHint, "알약을 누르면 「누르면 안내를 끝내요」가 다시 (안내는 그대로)");
            Press(pillAt, 0.08f, null);
            Sleep(0.4f);
            Check(() => !ga.EditorGuiding, "그 말이 보일 때 알약을 누르면 안내 끝");
            Wait(() => ga.EditorHidden, 1.5f, "화살표 사라짐");
            Check(() => ga.EditorHidden, "안내가 끝나면 화살표 · 알약 사라짐");

            // 3b) × — 안내 줄이 사라진 뒤에도 한 번 누르면 바로 끝 (11-16 대표님 「길안내의 경우 하다가 취소할 수 있도록」)
            Do(() => go(200, 0, "시험 북쪽 200m", null));
            Wait(() => ga.EditorShowing, 3f, "다시 안내");
            Sleep(3.4f);
            Do(() => log.Add($"  × {SkyScreenRect(ga.EditorClose != null ? (RectTransform)ga.EditorClose.transform : null)} · 알약 {SkyScreenRect(ga.EditorPill)} · 글 왼쪽 여백 {GaTextLeftPad(ga):0} · 글과 × 사이 {GaCloseGap(ga):0} (알약 단위)"));
            Check(() => ga.EditorGuiding && !ga.EditorHint && ga.EditorClose != null && ga.EditorClose.gameObject.activeInHierarchy && GaCloseInside(ga)
                     && GaCloseGap(ga) >= 8f && GaTextLeftPad(ga) >= 8f && text() == "시험 북쪽 200m  200m · 앞",
                "알약 오른쪽 끝에 × — 알약 안 · 글과 겹치지 않는다 (안내 줄이 사라진 뒤에도 늘 보임)");
            Shot("ga_close");
            Press(closeAt, 0.08f, null);
            Sleep(0.3f);
            Check(() => !ga.EditorGuiding, "× 를 한 번 누르면 바로 안내 끝 (알약을 두 번 누를 필요 없이)");
            Wait(() => ga.EditorHidden, 1.5f, "화살표 사라짐");
            Check(() => ga.EditorHidden, "× 로 끝내면 화살표 · 알약 사라짐");
            // 긴 이름 — 화면 폭을 넘으면 글을 줄여 알약 안에 (× 와 겹치지 않게)
            Do(() => { R0926Notch.EditorHeading = 240f; go(200, 0, "아주아주긴장소이름입니다열여덟자가넘어요", null); });
            Wait(() => ga.EditorShowing, 3f, "긴 이름 안내");
            Sleep(1.2f);
            Do(() => log.Add($"  긴 이름: '{text()}' · 글 배율 {(ga.EditorLabelText != null ? ga.EditorLabelText.rectTransform.localScale.x : -1f):0.00} · 알약 {SkyScreenRect(ga.EditorPill)} · 글 왼쪽 여백 {GaTextLeftPad(ga):0} · 글과 × 사이 {GaCloseGap(ga):0}"));
            Check(() =>
            {
                var p = SkyScreenRect(ga.EditorPill);
                return p.HasValue && p.Value.xMin >= 0f && p.Value.xMax <= Screen.width && GaCloseInside(ga) && GaCloseGap(ga) >= 8f && GaTextLeftPad(ga) >= 8f;
            }, "긴 이름 · 긴 방향 말도 알약 안 — × 와 겹치지 않고 화면 밖으로 나가지 않는다");
            Do(() => { R0926GuideArrow.Stop(); R0926Notch.EditorHeading = 0f; });
            Wait(() => ga.EditorHidden, 1.5f, "안내 끝");

            // 4) 20m 안 — 「거의 다 왔어요」, 2m 안에 1초 — 「도착했어요」 · 화살표는 사라지고 알약만 → 2.5초 뒤 끝
            Do(() => go(12, 0, "시험 바로 앞", null));
            Wait(() => ga.EditorShowing, 3f, "12m 안내");
            Sleep(0.5f);
            Do(() => log.Add("  12m: " + state()));
            Check(() => text() == "거의 다 왔어요  12m · 앞", "20m 안 — 「거의 다 왔어요  12m · 앞」");
            Shot("ga_almost");
            Do(() => go(1, 0, "시험 바로 앞", null));
            Wait(() => ga.EditorArrived, 3f, "2m 안에 1초 — 도착");
            Sleep(0.5f);
            Do(() => log.Add("  도착: " + state()));
            Check(() => ga.EditorArrived && text() == "도착했어요  시험 바로 앞" && !ga.EditorHint, "도착 — 「도착했어요  시험 바로 앞」 (안내 줄 없음)");
            Check(() => ga.EditorMesh.color.a < 0.05f && ga.EditorShowing, "도착하면 화살표는 사라지고 알약만");
            Shot("ga_arrived");
            Wait(() => !ga.EditorGuiding, 4f, "도착 2.5초 뒤 끝");
            Check(() => !ga.EditorGuiding, "도착 2.5초 뒤 안내 끝");
            Do(() => go(1, 0, "시험 바로 앞", null));
            Wait(() => ga.EditorArrived && ga.EditorShowing, 3f, "다시 도착");
            Press(pillAt, 0.08f, null);
            Sleep(0.3f);
            Check(() => !ga.EditorGuiding, "도착한 뒤 알약을 누르면 바로 끝");

            // 5) 20분이 지나면 끝
            Do(() => go(200, 0, "시험 북쪽 200m", null));
            Wait(() => ga.EditorShowing, 3f, "다시 안내");
            Do(() => typeof(R0926GuideArrow).GetField("gStart", NmF).SetValue(ga, Time.unscaledTime - 20f * 60f - 1f));
            Sleep(0.3f);
            Check(() => !ga.EditorGuiding, "20분이 지나면 안내 끝");

            // 6) 창이 덮으면 숨고 안내는 이어진다 — 목록
            Do(() => go(200, 0, "시험 북쪽 200m", null));
            Wait(() => ga.EditorShowing, 3f, "다시 안내");
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => Active("ListPanel"), 3f, "목록 열림");
            Sleep(0.6f);
            Check(() => ga.EditorHidden && ga.EditorGuiding, "목록 창이 덮으면 화살표는 숨고 안내는 이어짐");
            Back();
            Wait(() => !Active("ListPanel"), 4f, "목록 닫힘");
            Wait(() => ga.EditorShowing, 3f, "창을 닫으면 화살표가 다시");
            Check(() => ga.EditorShowing && text() == "시험 북쪽 200m  200m · 앞", "창을 닫으면 화살표 · 알약이 다시");

            // 7) 하늘을 올려다보면 숨는다 → 아래에 고정한 하늘 카드가 있으면 그 위로
            Do(() => AimCamera(-45f, 0f));
            Wait(() => sky.SkyShowing, 10f, "하늘을 보면 판");
            Sleep(0.5f);
            Check(() => sky.SkyShowing && ga.EditorHidden && ga.EditorGuiding, "하늘 판을 올려다보는 동안 화살표는 숨음 (안내는 이어짐)");
            Do(() => sky.SetPinned(true));
            Sleep(0.8f);
            Do(() => AimCamera(0f, 0f));
            Wait(() => ga.EditorShowing, 3f, "아래 고정 — 화살표 다시");
            Sleep(1.2f);
            GaSampleLow(() => ga, low);
            Do(() => log.Add($"  고정한 하늘 카드 {SkyScreenRect(skyCard)} · 알약 {SkyScreenRect(ga.EditorPill)} · 그림 가장 낮은 점 y {low[0]:0}"));
            Check(() =>
            {
                var card = SkyScreenRect(skyCard);
                var pill = SkyScreenRect(ga.EditorPill);
                return sky.Pinned && !sky.PinnedTop && card.HasValue && pill.HasValue && ga.EditorShowing
                    && low[0] >= card.Value.yMax - 1f && pill.Value.yMin > card.Value.yMax && pill.Value.yMax <= Screen.height;
            }, "아래에 고정한 하늘 카드가 있으면 화살표 · 알약은 그 위로 비켜 선다");
            Shot("ga_skypin");
            Do(() => sky.SetPinned(false));
            Sleep(1.5f);
            GaSampleLow(() => ga, low);
            Check(() => !sky.Pinned && ga.EditorShowing && low[0] >= floorTop() - 1f && low[0] < floorTop() + 200f,
                "고정을 풀면 다시 주소줄 · 도크 바로 위로");

            // 8) 가까이 카드 — 다른 것이 2m 안에 들어와 카드가 뜨면 숨고, 사라지면 다시
            Do(() => NmPut(cube, 1.5f));
            Wait(() => nmModal.EditorShowing, 3f, "가까이 카드 뜸 (다른 오브젝트)");
            Sleep(0.5f);
            Check(() => nmModal.EditorShowing && ga.EditorHidden && ga.EditorGuiding, "가까이 카드가 떠 있는 동안 화살표는 숨음 (다른 장소 안내는 이어짐)");
            Do(() => NmPut(cube, 12f));
            Wait(() => !nmModal.EditorCardOn, 3f, "카드 사라짐");
            Wait(() => ga.EditorShowing, 3f, "카드가 사라지면 화살표가 다시");
            Check(() => !nmModal.EditorCardOn && ga.EditorShowing && ga.EditorGuiding, "카드가 사라지면 화살표가 다시");

            // 9) 이어 받기 — 안내하던 그 장소가 2m 안에 들어와 가까이 카드에 담기면 화살표는 끝
            Do(() => { NmPut(cube, 9f, 30f); go(300, 0, NmName(cube), cubeT); });
            Wait(() => ga.EditorShowing, 3f, "그 큐브로 안내");
            Sleep(0.4f);
            Do(() => log.Add("  큐브 안내: " + state()));
            Do(() => NmPut(cube, 1.5f));
            Wait(() => !ga.EditorGuiding, 3f, "2m 안 — 가까이 카드가 이어 받음");
            Check(() => !ga.EditorGuiding && nmModal.EditorCount >= 1 && nmModal.EditorCurrent == cube && cubeT != null && R0926NearHold.Held.Contains(cubeT),
                "안내하던 장소가 2m 안에 들어와 가까이 카드에 담기면 화살표 안내는 끝 (카드가 이어 받는다)");
            Shot("ga_handoff");
            Do(() => NmPut(cube, 12f));
            Wait(() => !nmModal.EditorCardOn, 3f, "카드 치움");

            // 10) AR 에 떠 있으면 카메라와 그 오브젝트 사이 (가까이 카드와 같은 잣대) — 에디터 AR 이 추적 중일 때만
            Do(() =>
            {
                bool tracking = UnityEngine.XR.ARFoundation.ARSession.state == UnityEngine.XR.ARFoundation.ARSessionState.SessionTracking;
                log.Add("  에디터 AR 세션: " + UnityEngine.XR.ARFoundation.ARSession.state);
                if (!tracking) { log.Add("SKIP AR 거리 — 에디터 AR 이 추적 중이 아니다 (기기에서만)"); return; }
                R0926Notch.EditorHeading = null;
                NmPut(cube, 6f, 40f);
                go(300, 0, NmName(cube), cubeT);
            });
            Sleep(1.2f);
            Do(() =>
            {
                if (R0926Notch.EditorHeading != null) return;
                log.Add("  AR 거리: " + state());
                bool ok = text().EndsWith("6m · 오른쪽 앞") && Mathf.Abs(Mathf.DeltaAngle(ga.EditorYaw, 40f)) < 4f;
                log.Add((ok ? "PASS " : "FAIL ") + "AR 에 떠 있으면 카메라와 그 오브젝트 사이 — 「6m · 오른쪽 앞」 (40°)");
                R0926GuideArrow.Stop();
                NmPut(cube, 12f);
                R0926Notch.EditorHeading = 0f;
            });

            // 11) 물방울메뉴 — 알약 바탕이 물방울 유리 · 기본메뉴 — 진한 반투명 · 화면 안 · 주소줄 위
            Do(() => go(290, 170, "우팡이네집", null));
            Wait(() => ga.EditorShowing, 3f, "다시 안내");
            Sleep(3.6f);   // 안내 줄이 사라진 모습
            Check(() =>
            {
                var g = ga.EditorPill.GetComponent<R0926Glass>();
                log.Add("  알약 바탕: " + (g == null ? "유리 없음" : g.Showing ? (g.SeeThrough ? "물방울 유리 · 비침" : "물방울 유리 · 덮임") : "유리 꺼짐") + " · '" + text() + "'");
                return g != null && g.Showing;
            }, "물방울메뉴 — 알약 바탕이 물방울 유리");
            Check(() =>
            {
                var p = SkyScreenRect(ga.EditorPill);
                float floor = floorTop();
                log.Add($"  알약 {p} · 바닥 y {floor:0} · 화면 {Screen.width}x{Screen.height}");
                return p.HasValue && p.Value.xMin >= 0f && p.Value.xMax <= Screen.width && p.Value.yMax <= Screen.height && p.Value.yMin > floor;
            }, "알약 — 화면 안 · 주소줄 · 도크 위");
            Shot("ga_droplet");
            Do(() => R0926MenuStyle.Droplet = false);
            Sleep(0.6f);
            Check(() => { var g = ga.EditorPill.GetComponent<R0926Glass>(); return g != null && !g.Showing && ga.EditorShowing; }, "기본메뉴 — 유리 없이 진한 반투명 알약");
            Shot("ga_basic");
            Do(() => R0926MenuStyle.Droplet = true);
            Do(() => R0926GuideArrow.Stop());
            Wait(() => ga.EditorHidden, 1.5f, "안내 끝");

            // 11a) AR 오브젝트 없이 좌표로만 안내할 때 (지도 핀 · AR 에 아직 없는 장소) — 위치(GPS)는 2m 를 가리지 못하니 10m 안에 1초면 도착
            Do(() => go(8, 0, "시험 8m 앞", null));
            Wait(() => ga.EditorArrived, 3f, "좌표 안내 — 10m 안에 1초면 도착");
            Do(() => log.Add("  좌표 안내 8m: " + state()));
            Check(() => ga.EditorArrived && text() == "도착했어요  시험 8m 앞", "좌표로만 안내하면 10m 안에서 도착 — 「도착했어요  시험 8m 앞」 (AR 오브젝트는 그대로 2m)");
            Do(() => R0926GuideArrow.Stop());
            Wait(() => ga.EditorHidden, 1.5f, "안내 끝");

            // 11b) 정보 창 위 「길안내」 — 두 번 누른 창의 버튼 줄(번역과 「···」 사이 분홍 동그라미)을 누르면 그 장소로 안내하고 창은 닫힌다
            //      (11-16 대표님 「더블탭하고 나서 발생하는 화면에 메뉴가 여러개 있잖아? 그때 거기에 화살표 버튼을 만들어서 안내하도록」)
            Do(() =>
            {
                NmPut(cube, 9f, 30f);
                dg = UnityEngine.Object.FindAnyObjectByType<R0926DetailGuide>(FindObjectsInactive.Include);
                var dt = cube.GetComponentInChildren<DoubleTap3D>(true);
                if (dt == null || !dt.OpenInfo()) log.Add("  큐브 정보 창을 열지 못함");
            });
            Wait(infoOpen, 4f, "큐브 정보 창");
            Sleep(0.3f);
            Do(() =>
            {
                bool known = R0926DetailGuide.Resolve(out dgLat, out dgLon, out dgTarget, out dgName);
                log.Add($"  정보 창 「길안내」 {(dg == null ? "없음" : dgOn() ? "보임" : "숨음")} · '{dgName}' · 오브젝트 {(dgTarget == null ? "없음" : dgTarget == cubeT ? "그 큐브" : dgTarget.name)} · 자리 {(known ? "앎" : "모름")}");
                log.Add($"  번역 {topRect("TranslationButton")} · 길안내 {topRect("GuideButton0926")} · 「···」 {topRect("FixButton")} · 화면 {Screen.width}x{Screen.height}");
            });
            Check(() => dgOn() && dgTarget == cubeT && !string.IsNullOrEmpty(dgName) && (dgLat != 0f || dgLon != 0f), "정보 창 위 「길안내」 — 자리를 아는 AR 오브젝트면 보인다 (그 큐브 · 그 자리)");
            Check(() =>
            {
                Rect? t = topRect("TranslationButton"), g = topRect("GuideButton0926"), f = topRect("FixButton");
                if (!t.HasValue || !g.HasValue || !f.HasValue) return false;
                return g.Value.xMin >= t.Value.xMax && g.Value.xMax <= f.Value.xMin && Mathf.Abs(g.Value.center.y - f.Value.center.y) < 2f && Mathf.Abs(g.Value.width - f.Value.width) < 2f;
            }, "「길안내」는 번역과 「···」 사이 — 「···」와 같은 줄 · 같은 크기 · 겹치지 않음");
            Check(() =>
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (Transform ch in dg.transform.parent)
                {
                    var r = SkyScreenRect(ch as RectTransform);
                    if (!r.HasValue) continue;
                    lo = Mathf.Min(lo, r.Value.xMin);
                    hi = Mathf.Max(hi, r.Value.xMax);
                }
                log.Add($"  버튼 줄 화면 x {lo:0} ~ {hi:0} (폭 {Screen.width})");
                return lo >= 0f && hi <= Screen.width;
            }, "한 칸 늘어난 버튼 줄도 모두 화면 안 (줄을 왼쪽으로 옮김)");
            Shot("ga_detail_btn");
            Press(() => topRect("GuideButton0926")?.center, 0.08f, null);
            Sleep(0.3f);
            Do(() => log.Add($"  누른 뒤: {state()} · 안내 '{gName()}' · 오브젝트 {(gTarget() == cubeT ? "그 큐브" : gTarget() == null ? "없음" : "다름")} · 정보 창 {(DoubleTap3D.Shown != null ? "열림" : "닫힘")}"));
            Check(() => ga.EditorGuiding && gName() == clean(dgName) && gTarget() == cubeT && gF("gLat") == dgLat && gF("gLon") == dgLon,
                "「길안내」를 누르면 그 장소로 안내 시작 (이름 · AR 오브젝트 · 자리 그대로)");
            Wait(() => DoubleTap3D.Shown == null, 1.5f, "정보 창 닫힘 (X 와 같은 0.3초 페이드)");
            Check(() => DoubleTap3D.Shown == null, "누르면 정보 창은 닫힌다 (X 와 같이)");
            Wait(() => !Active("FullScreenPanel") && ga.EditorShowing, 3f, "창이 닫히면 아래 화살표");
            Check(() => !Active("FullScreenPanel") && ga.EditorShowing && ga.EditorGuiding && titledAs(dgName), "창이 닫히면 AR 화면 아래 화살표 · 알약에 그 장소");
            Shot("ga_detail_guide");
            Do(() => R0926GuideArrow.Stop());
            Wait(() => ga.EditorHidden, 1.5f, "안내 끝");
            // 가까이 카드가 그 장소를 담고 있으면(바로 앞) 숨는다 — 안내를 시작해도 카드가 바로 이어 받아 끝난다
            Do(() => NmPut(cube, 1.5f));
            Wait(() => nmModal.EditorShowing && cubeT != null && R0926NearHold.Held.Contains(cubeT), 3f, "가까이 카드에 그 큐브");
            Do(() => cube.GetComponentInChildren<DoubleTap3D>(true)?.OpenInfo());
            Wait(infoOpen, 4f, "정보 창");
            Sleep(0.3f);
            Do(() => log.Add($"  가까이 카드에 담긴 채 연 정보 창: 담김 {R0926NearHold.Held.Contains(cubeT)} · 「길안내」 {(dgOn() ? "보임" : "숨음")}"));
            Check(() => R0926NearHold.Held.Contains(cubeT) ? !dgOn() && dg.GetComponent<CanvasGroup>().alpha < 0.01f : dgOn(), "「길안내」는 가까이 카드에 담긴 장소면 숨김 · 정보 창이 카드를 내려 담김이 풀리면 보임");
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => !Active("FullScreenPanel"), 3f, "정보 창 닫힘");
            Do(() => NmPut(cube, 12f));
            Wait(() => !nmModal.EditorCardOn, 3f, "카드 치움");

            // 11c) 지도 핀 — 꾹 눌러 꽂은 핀 말풍선의 「길안내」 → 그 좌표로 안내, 시트(목록 · 지도)는 내려간다
            //      (11-16 대표님 「지도에서 좌표찍은 핀을 길게 누르면 여기서도 길안내버튼이 발생해서 누르면 길안내 하도록」)
            //      운영 서버에 묻지 않는다 — 타일 · 주소는 닫힌 자리, 친구 · 보물은 미리보기. 핀 이름은 24자가 넘는 긴 이름 (말풍선이 가장 넓을 때)
            Do(() =>
            {
                mapViewWas = PlayerPrefs.HasKey("R0926Map.View") ? PlayerPrefs.GetString("R0926Map.View") : null;
                R0926MapTiles.EditorBase = "http://127.0.0.1:9";
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
                R0926Geo.EditorBase = NmDeadStub;
                Click("Dock0926/List_Button");
            });
            Wait(() => Active("ListPanel"), 3f, "목록 열림");
            Sleep(0.6f);
            Do(() => Modes()?.Show(1));
            Sleep(1.0f);
            Do(() =>
            {
                map = UnityEngine.Object.FindAnyObjectByType<R0926FriendsMap>(FindObjectsInactive.Include);
                if (map == null) { log.Add("  지도 없음"); return; }
                map.EditorPreview(true, 100f, 37.5665f, 126.978f);
                typeof(R0926FriendsMap).GetField("nextRefresh", NmF)?.SetValue(map, float.MaxValue);
                typeof(R0926FriendsMap).GetMethod("SetDrop", NmF)?.Invoke(map, new object[] { 37.5665, 126.978, "서울특별시 중구 세종대로 110 서울시청 본관 앞 광장" });
            });
            Sleep(0.4f);
            Do(() => log.Add($"  핀 말풍선 {SkyScreenRect(bubble())} · 길안내 {SkyScreenRect(mapGuide())} · 지도 {SkyScreenRect(map != null ? (RectTransform)map.transform : null)}"));
            Check(() =>
            {
                var g = mapGuide();
                var b = g != null ? g.GetComponent<Button>() : null;
                return b != null && g.gameObject.activeInHierarchy && b.onClick.GetPersistentEventCount() > 0 && b.onClick.GetPersistentMethodName(0) == "GuideDropPin" && g.GetSiblingIndex() == 1;
            }, "핀 말풍선에 「길안내」 — 좌표 다음 · 복사 앞 (GuideDropPin)");
            Check(() =>
            {
                Rect? b = SkyScreenRect(bubble()), v = SkyScreenRect(map != null ? (RectTransform)map.transform : null);
                if (!b.HasValue || !v.HasValue) return false;
                foreach (var n in new[] { "Guide", "Copy", "Clear" })
                {
                    var r = SkyScreenRect(bubble().Find(n) as RectTransform);
                    if (!r.HasValue || r.Value.xMin < b.Value.xMin - 0.5f || r.Value.xMax > b.Value.xMax + 0.5f) return false;
                }
                return b.Value.xMin >= v.Value.xMin - 0.5f && b.Value.xMax <= v.Value.xMax + 0.5f;
            }, "긴 이름이 든 말풍선도 지도 안 — 「길안내」 · 복사 · × 모두 말풍선 안");
            Shot("ga_map_pin");
            Press(() => SkyScreenRect(mapGuide())?.center, 0.08f, null);
            Sleep(0.3f);
            Do(() => log.Add($"  핀 길안내 누름: {state()} · 안내 '{gName()}' · 오브젝트 {(gTarget() == null ? "없음" : gTarget().name)}"));
            Check(() => ga.EditorGuiding && gName() == clean((typeof(R0926FriendsMap).GetField("dropName", NmF)?.GetValue(map) as Text)?.text) && gTarget() == null
                     && Mathf.Abs(gF("gLat") - 37.5665f) < 1e-4f && Mathf.Abs(gF("gLon") - 126.978f) < 1e-4f,
                "핀 「길안내」를 누르면 그 좌표로 안내 — 이름은 말풍선 위 줄 · AR 오브젝트 없음");
            Wait(() => !Active("ListPanel"), 3f, "시트가 내려감");
            Wait(() => ga.EditorShowing, 3f, "AR 화면 아래 화살표");
            Check(() => !Active("ListPanel") && ga.EditorShowing && ga.EditorGuiding, "시트가 내려가고 AR 화면 아래 화살표 · 알약 (안내는 AR 화면에서)");
            Shot("ga_map_guide");
            Do(() =>
            {
                R0926GuideArrow.Stop();
                map?.ClearDropPin();
                map?.EditorPreviewClear();
                R0926MapTiles.EditorBase = null;
                R0926Geo.EditorBase = null;
                typeof(R0926MapTiles).GetField("info", SF)?.SetValue(null, null);
                typeof(R0926MapTiles).GetField("infoRetryAt", SF)?.SetValue(null, 0f);
                if (mapViewWas != null) PlayerPrefs.SetString("R0926Map.View", mapViewWas); else PlayerPrefs.DeleteKey("R0926Map.View");
                Modes()?.Show(0);   // 다음 시험은 목록 탭에서
            });
            Wait(() => ga.EditorHidden, 1.5f, "안내 끝");

            // 12) 노치 없는 기기 — 장소 줄을 누르면 (AR 에 없어도) 목록이 내려가고 아래 화살표로 안내 (예전: 목록 그대로 · 「아직 AR화면 내에 없음」)
            Do(() => { NmPut(cube, 9f, 30f, 5f); Click("Dock0926/List_Button"); });   // 큐브는 9m 앞 5m 위 — 아래 땅 위 거리 시험
            Wait(() => Active("ListPanel") && pickRow() != null, 15f, "목록 · 장소 줄");
            Sleep(0.6f);
            // 이동 거리는 땅 위 거리만 (11-16 대표님 「이동거리의 경우 평면거리로만 계산해. 높이신경쓰지말고」) — 화면 밖 화살표 · 범위(Target) · 목록 거리 칸 모두
            Check(() =>
            {
                var cam = Camera.main;
                if (cam == null || cubeT == null) return false;
                Vector3 d = cubeT.transform.position - cam.transform.position;
                float flat = new Vector2(d.x, d.z).magnitude, got = cubeT.GetDistanceFromCamera(cam.transform.position);
                log.Add($"  5m 위 큐브: 화살표 거리 {got:0.00}m · 땅 위 {flat:0.00}m · 곧은 거리 {d.magnitude:0.00}m");
                return Mathf.Abs(got - flat) < 0.01f && d.magnitude - flat > 1f;
            }, "높이가 달라도 화면 밖 화살표 · 범위 거리는 땅 위 거리 (Target)");
            Check(() =>
            {
                var plm = UnityEngine.Object.FindAnyObjectByType<PlaceListManager>();
                var rows = listRows();
                var cam = Camera.main;
                if (plm == null || rows == null || cam == null || cubeT == null) return false;
                string key = null;
                foreach (var e in plm.ShownEntries) if (e.target == cubeT) { key = e.name; break; }
                if (key == null) { log.Add("  목록에 큐브 줄 없음"); return false; }
                foreach (var r in rows)
                {
                    if (r.GetType().GetField("key").GetValue(r) as string != key) continue;
                    string label = r.GetType().GetField("distLabel").GetValue(r) as string;
                    float m = GaMeters(label);
                    Vector3 d = cubeT.transform.position - cam.transform.position;
                    float flat = new Vector2(d.x, d.z).magnitude;
                    log.Add($"  큐브 줄 '{key}' 거리 칸 {label} · 땅 위 {flat:0.0}m · 곧은 거리 {d.magnitude:0.0}m");
                    return m >= 0f && Mathf.Abs(m - flat) <= 1f && Mathf.Abs(m - d.magnitude) > 0.5f;
                }
                log.Add("  큐브 줄이 목록 화면에 없음");
                return false;
            }, "목록 거리 칸도 땅 위 거리 (5m 위 큐브 — 곧은 거리가 아니라)");
            Do(() =>
            {
                // 목록 거리 칸 · 화살표 · 노치 모두 땅 위 거리 — 고른 줄의 목록 · AR · 위치(GPS) 거리를 같이 적어 둔다
                var r = pickRow();
                var plm = UnityEngine.Object.FindAnyObjectByType<PlaceListManager>();
                var cam = Camera.main;
                if (r == null || plm == null || cam == null) return;
                string key = r.GetType().GetField("key").GetValue(r) as string;
                foreach (var e in plm.ShownEntries)
                {
                    if (e.name != key) continue;
                    var v = VirtualLocation.Instance;
                    float gps = (float)GeoHere.Meters(v.Latitude, v.Longitude, e.lat, e.lon);
                    string ar = "AR 없음";
                    if (e.target != null)
                    {
                        Vector3 d = e.target.transform.position - cam.transform.position;
                        ar = $"AR {d.magnitude:0.0}m (수평 {new Vector2(d.x, d.z).magnitude:0.0}m · 높이 {d.y:+0.0;-0.0}m)";
                    }
                    log.Add($"  줄 '{key}' 거리 — 목록 {r.GetType().GetField("distLabel").GetValue(r)} · {ar} · 위치(GPS) {gps:0.0}m");
                    break;
                }
            });
            Do(hideTargets);
            // 사람 줄은 눌러도 안내하지 않는다 (11-16 대표님 「개인 프로필의 경우 따라가기 하지말자(스토킹 위험 방지)」) — 근처 사람이 없어 장소 줄을 잠깐 사람으로 바꿔 누른다
            Do(() =>
            {
                var r = pickRow();
                var plm = UnityEngine.Object.FindAnyObjectByType<PlaceListManager>();
                var list = plm != null ? typeof(PlaceListManager).GetField("shownEntries", NmF)?.GetValue(plm) as List<PlaceListManager.ListEntry> : null;
                if (r == null || list == null) { log.Add("  사람 줄 시험: 줄 없음"); return; }
                string key = r.GetType().GetField("key").GetValue(r) as string;
                int i = list.FindIndex(x => x.name == key);
                if (i < 0) { log.Add("  사람 줄 시험: 목록에 없음"); return; }
                var was = list[i];
                var person = was;
                person.kind = PlaceListManager.EntryKind.Person;
                list[i] = person;
                try { (r.GetType().GetField("go").GetValue(r) as GameObject).GetComponent<Button>().onClick.Invoke(); }
                finally { list[i] = was; }
                personTried = true;
                log.Add("  사람으로 바꿔 누른 줄: " + key);
            });
            Sleep(0.3f);
            Check(() => personTried && !ga.EditorGuiding && !notch.EditorGuiding && Active("ListPanel"), "사람 줄을 누르면 안내하지 않는다 (아래 화살표 · 노치 모두 · 목록 그대로)");
            Do(tapPicked);
            Sleep(0.3f);
            Check(() => ga.EditorGuiding && !notch.EditorGuiding, "노치 없는 기기 — 장소 줄을 누르면 아래 화살표 안내 시작 (노치 안내는 없음)");
            Wait(() => !Active("ListPanel"), 4f, "줄을 누르면 목록이 내려감");
            Wait(() => ga.EditorShowing, 3f, "목록이 내려가면 화살표");
            Sleep(0.3f);
            Do(() => log.Add("  줄 안내: " + state()));
            Check(() => !Active("ListPanel") && ga.EditorShowing && titled() && ga.EditorHint, "목록이 내려가면 화살표 · 알약에 그 장소 (처음 3초 안내 줄)");
            Shot("ga_row_nonotch");
            Do(() => R0926GuideArrow.Stop());
            Wait(() => ga.EditorHidden, 1.5f, "안내 끝");

            // 12b) 목록 사진으로 연 정보 창(AR 에 없는 장소)의 「길안내」 — 그 줄의 자리로 안내하고 창 · 목록이 같이 내려간다 (목록이 다시 비치지 않는다)
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => Active("ListPanel") && pickRow() != null, 8f, "목록 · 장소 줄");
            Sleep(0.6f);
            Do(() =>
            {
                hideTargets();   // AR 에 없는 장소 — 대리 창으로 연다
                hostName = null;
                var plm = UnityEngine.Object.FindAnyObjectByType<PlaceListManager>();
                var rows = listRows();
                if (plm == null || rows == null) return;
                object best = null;
                bool bestCached = true;
                float bestM = -1f;
                foreach (var r in rows)
                {
                    var rt = r.GetType();
                    var g = rt.GetField("go").GetValue(r) as GameObject;
                    var ph = rt.GetField("photoBox").GetValue(r) as RectTransform;
                    if (g == null || !g.activeInHierarchy || ph == null || ph.GetComponent<Button>() == null) continue;
                    if ((PlaceListManager.EntryKind)rt.GetField("kind").GetValue(r) != PlaceListManager.EntryKind.Place || (bool)rt.GetField("noDirection").GetValue(r)) continue;
                    string key = rt.GetField("key").GetValue(r) as string;
                    object data = null;
                    foreach (var e in plm.ShownEntries) if (e.name == key) { data = e.data; break; }
                    if (data == null) continue;
                    bool cached = data is CachedPlaceData;   // 우팡 장소는 상세를 받아서 연다 — 관광 · 교통 장소를 먼저
                    float m = GaMeters(rt.GetField("distLabel").GetValue(r) as string);
                    if (best == null || bestCached && !cached || bestCached == cached && m > bestM) { best = r; bestCached = cached; bestM = m; }
                }
                if (best == null) { log.Add("  사진 칸이 있는 장소 줄 없음"); return; }
                hostName = best.GetType().GetField("key").GetValue(best) as string;
                log.Add($"  사진을 누른 줄: {hostName} [{best.GetType().GetField("distLabel").GetValue(best)}] · {(bestCached ? "우팡 장소" : "관광 · 교통 장소")}");
                (best.GetType().GetField("photoBox").GetValue(best) as RectTransform).GetComponent<Button>().onClick.Invoke();
            });
            Wait(infoOpen, 10f, "목록 사진 — 정보 창");
            Sleep(0.4f);
            Do(() =>
            {
                bool known = R0926DetailGuide.Resolve(out dgLat, out dgLon, out dgTarget, out dgName);
                var g = listGroup();
                log.Add($"  목록 사진 정보 창: {(R0926PlaceInfo.IsHost(DoubleTap3D.Shown) ? "AR 밖 장소(대리 창)" : "AR 오브젝트 창")} · 「길안내」 {(dgOn() ? "보임" : "숨음")} · '{dgName}' · 자리 {(known ? "앎" : "모름")} · 목록 {(g != null ? g.alpha.ToString("0.00") : "-")}");
            });
            Check(() => hostName != null && dgOn() && (dgLat != 0f || dgLon != 0f) && (!R0926PlaceInfo.IsHost(DoubleTap3D.Shown) || GaPlain(dgName) == GaPlain(hostName)),
                "목록 사진으로 연 정보 창에도 「길안내」 — 그 줄의 장소 · 자리");
            Check(() => { var g = listGroup(); return g != null && g.alpha <= 0.01f; }, "정보 창이 떠 있는 동안 목록은 가려 둔다 (예전대로)");
            Shot("ga_host_btn");
            Press(() => topRect("GuideButton0926")?.center, 0.08f, null);
            GaSampleMax(() => { var g = listGroup(); return g != null && Active("ListPanel") ? g.alpha : 0f; }, listMax, 1.5);
            Do(() => log.Add($"  누른 뒤 1.5초: {state()} · 안내 '{gName()}' · 목록 {(Active("ListPanel") ? "열림" : "닫힘")} · 그동안 목록이 가장 진했을 때 {listMax[0]:0.00}"));
            Check(() => ga.EditorGuiding && gName() == clean(dgName) && gF("gLat") == dgLat && gF("gLon") == dgLon, "누르면 그 장소로 안내 (이름 · 자리 그대로)");
            Check(() => !Active("FullScreenPanel") && !Active("ListPanel") && listMax[0] <= 0.05f, "정보 창 · 목록이 같이 내려가고 목록은 다시 비치지 않는다");
            Wait(() => ga.EditorShowing, 3f, "아래 화살표");
            Check(() => ga.EditorShowing && titledAs(dgName), "AR 화면 아래 화살표 · 알약에 그 장소");
            Shot("ga_host_guide");
            Do(() => R0926GuideArrow.Stop());
            Wait(() => ga.EditorHidden, 1.5f, "안내 끝");
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => Active("ListPanel"), 3f, "목록 다시 열림");
            Sleep(0.8f);
            Check(() => { var g = listGroup(); return g != null && g.alpha >= 0.99f && g.blocksRaycasts && g.interactable; }, "다시 열면 목록이 그대로 보이고 눌린다 (가림이 남지 않음)");
            Back();
            Wait(() => !Active("ListPanel"), 4f, "목록 닫힘");

            // 13) 노치 아이폰 — 줄을 누르면 노치와 화살표가 같이. 알약으로 끝내도 · 노치로 끝내도 둘 다 끝
            Do(() => { R0926Notch.EditorForceNoNotch = false; notch.EditorRemeasure(); });
            Sleep(1.0f);
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => Active("ListPanel") && pickRow() != null, 8f, "목록 · 장소 줄");
            Sleep(0.6f);
            Do(tapPicked);
            Sleep(0.3f);
            Check(() => ga.EditorGuiding && notch.EditorGuiding, "노치 아이폰 — 줄을 누르면 노치 안내와 아래 화살표가 같이");
            Wait(() => !Active("ListPanel") && ga.EditorShowing && notchUp() && notch.EditorMode == "Guide", 5f, "목록이 내려가면 노치 · 화살표");
            Sleep(0.3f);
            Do(() => log.Add($"  노치 아이폰: {state()} · 노치 {notch.EditorMode} '{notch.EditorTitle}' {notch.EditorRight}"));
            Shot("ga_row_notch");
            Press(pillAt, 0.08f, null);   // 처음 3초 — 알약을 누르면 끝
            Sleep(0.4f);
            Check(() => !ga.EditorGuiding && !notch.EditorGuiding, "처음 3초 안에 알약을 누르면 화살표 · 노치 안내가 같이 끝");
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => Active("ListPanel") && pickRow() != null, 8f, "목록 · 장소 줄");
            Sleep(0.6f);
            Do(tapPicked);
            Wait(() => !Active("ListPanel") && ga.EditorShowing && notchUp() && notch.EditorMode == "Guide" && notch.EditorExpand > 0.9f, 5f, "노치 이름 · 화살표");
            Press(notchAt, 0.08f, null);   // 이름이 보일 때 노치를 누르면 끝
            Sleep(0.4f);
            Check(() => !notch.EditorGuiding && !ga.EditorGuiding, "노치(이름이 보일 때)를 눌러 끝내면 아래 화살표도 같이 끝");
            // × 로 끝내도 노치 안내까지 같이 (11-16) — 안내 줄 · 노치 이름이 들어간 뒤
            Do(() => Click("Dock0926/List_Button"));
            Wait(() => Active("ListPanel") && pickRow() != null, 8f, "목록 · 장소 줄");
            Sleep(0.6f);
            Do(tapPicked);
            Wait(() => !Active("ListPanel") && ga.EditorShowing && notchUp() && notch.EditorMode == "Guide", 5f, "노치 · 화살표");
            Sleep(3.4f);
            Check(() => ga.EditorGuiding && notch.EditorGuiding && !ga.EditorHint, "3초 뒤 — 알약 위 안내 줄은 들어가고 화살표 · 노치 안내는 이어짐");
            Press(closeAt, 0.08f, null);
            Sleep(0.4f);
            Check(() => !ga.EditorGuiding && !notch.EditorGuiding, "노치 아이폰 — × 를 한 번 누르면 화살표 · 노치 안내가 같이 끝");
            CloseAll();

            Check(() => errors.Count == 0, "오류 없음");
            Do(() =>
            {
                foreach (var e in errors) log.Add("  오류: " + e);
                Application.logMessageReceived -= onLog;
                R0926GuideArrow.Stop();
                R0926Notch.StopGuide();
                R0926Notch.EditorHeading = null;
                R0926Notch.EditorForceNoNotch = noNotchWas;
                notch?.EditorRemeasure();
                if (sky != null && sky.Pinned) sky.SetPinned(false);
                if (ptTouch != null && ptTouch.added) InputSystem.RemoveDevice(ptTouch);
                ptTouch = null;
            });
            Do(NmFinish);
        }

        private static string GaPlain(string s) => Regex.Replace(s ?? "", "<[^>]+>", "");

        // 알약 단위 — 글 오른쪽 끝에서 × 동그라미 왼쪽 끝까지 (음수면 겹침)
        private static float GaCloseGap(R0926GuideArrow ga)
        {
            var pill = ga != null ? ga.EditorPill : null;
            var lbl = ga != null ? ga.EditorLabelText : null;
            var disc = ga != null && ga.EditorClose != null ? ga.EditorClose.transform.Find("Disc") as RectTransform : null;
            if (pill == null || lbl == null || disc == null) return float.NegativeInfinity;
            var lr = lbl.rectTransform;
            float textRight = pill.InverseTransformPoint(lr.TransformPoint(new Vector3(lr.rect.center.x + lbl.preferredWidth * 0.5f, 0f, 0f))).x;
            float discLeft = pill.InverseTransformPoint(disc.TransformPoint(new Vector3(disc.rect.xMin, 0f, 0f))).x;
            return discLeft - textRight;
        }

        // 알약 단위 — 알약 왼쪽 끝에서 글 왼쪽 끝까지 (음수면 글이 알약 밖으로)
        private static float GaTextLeftPad(R0926GuideArrow ga)
        {
            var pill = ga != null ? ga.EditorPill : null;
            var lbl = ga != null ? ga.EditorLabelText : null;
            if (pill == null || lbl == null) return float.NegativeInfinity;
            var lr = lbl.rectTransform;
            float textLeft = pill.InverseTransformPoint(lr.TransformPoint(new Vector3(lr.rect.center.x - lbl.preferredWidth * 0.5f, 0f, 0f))).x;
            return textLeft - pill.rect.xMin;
        }

        // × 동그라미가 알약 안에 있다
        private static bool GaCloseInside(R0926GuideArrow ga)
        {
            var pill = ga != null ? ga.EditorPill : null;
            var disc = ga != null && ga.EditorClose != null ? ga.EditorClose.transform.Find("Disc") as RectTransform : null;
            if (pill == null || disc == null) return false;
            Vector3 a = pill.InverseTransformPoint(disc.TransformPoint(new Vector3(disc.rect.xMin, disc.rect.yMin, 0f)));
            Vector3 b = pill.InverseTransformPoint(disc.TransformPoint(new Vector3(disc.rect.xMax, disc.rect.yMax, 0f)));
            var r = pill.rect;
            return a.x >= r.xMin - 0.5f && b.x <= r.xMax + 0.5f && a.y >= r.yMin - 0.5f && b.y <= r.yMax + 0.5f;
        }

        // 줄 거리 칸의 "42m" · "1.2km" → 미터 (모르면 -1)
        private static float GaMeters(string s)
        {
            var m = Regex.Match(s ?? "", @"^\s*(\d+(?:\.\d+)?)\s*(km|m)\s*$");
            if (!m.Success) return -1f;
            float v = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return m.Groups[2].Value == "km" ? v * 1000f : v;
        }

        // 화살표 그림을 한 번 그려 본다 — 끝 방향(윗면 끝 − 꼬리 가운데, 판 단위) · 보이는 점 중 가장 낮은 것(화면 px) · 정점 수
        private static bool GaDrawn(R0926GuideArrow ga, out Vector2 tipDir, out float lowScreenY, out int verts)
        {
            tipDir = Vector2.zero; lowScreenY = float.MaxValue; verts = 0;
            var m = ga != null ? ga.EditorMesh : null;
            if (m == null || !m.isActiveAndEnabled) return false;
            var pop = typeof(R0926GuideArrowMesh).GetMethod("OnPopulateMesh", NmF, null, new[] { typeof(VertexHelper) }, null);
            if (pop == null) return false;
            using (var vh = new VertexHelper())
            {
                pop.Invoke(m, new object[] { vh });
                verts = vh.currentVertCount;
                if (verts < 7) return false;
                var v = new UIVertex();
                float low = float.MaxValue;
                for (int i = 0; i < verts; i++)
                {
                    vh.PopulateUIVertex(ref v, i);
                    if (v.color.a > 2 && v.position.y < low) low = v.position.y;
                }
                vh.PopulateUIVertex(ref v, verts - 7); Vector3 tip = v.position;   // 윗면 — 마지막 일곱 점, 첫 점이 끝
                vh.PopulateUIVertex(ref v, verts - 4); Vector3 t1 = v.position;
                vh.PopulateUIVertex(ref v, verts - 3); Vector3 t2 = v.position;
                tipDir = tip - (t1 + t2) * 0.5f;
                var canvas = m.canvas != null ? m.canvas.rootCanvas : null;
                Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                if (low < float.MaxValue) lowScreenY = RectTransformUtility.WorldToScreenPoint(cam, m.rectTransform.TransformPoint(new Vector3(0f, low, 0f))).y;
            }
            return true;
        }

        // 몇 초 동안 매 프레임 값을 보고 가장 큰 값을 모은다
        private static void GaSampleMax(Func<float> value, float[] max, double seconds)
        {
            double until = -1;
            steps.Enqueue(() =>
            {
                if (until < 0) { until = EditorApplication.timeSinceStartup + seconds; max[0] = float.MinValue; }
                max[0] = Mathf.Max(max[0], value());
                if (EditorApplication.timeSinceStartup < until) return false;
                until = -1;
                return true;
            });
        }

        // 꺾쇠는 1.5초마다 차례로 깜빡인다 — 한 바퀴(1.6초) 동안 매 프레임 그려 보고 가장 낮은 점(화면 px)을 모은다
        private static void GaSampleLow(Func<R0926GuideArrow> ga, float[] low)
        {
            double until = -1;
            steps.Enqueue(() =>
            {
                if (until < 0) { until = EditorApplication.timeSinceStartup + 1.6; low[0] = float.MaxValue; }
                if (GaDrawn(ga(), out _, out float y, out _)) low[0] = Mathf.Min(low[0], y);
                if (EditorApplication.timeSinceStartup < until) return false;
                until = -1;
                return true;
            });
        }
    }
}
