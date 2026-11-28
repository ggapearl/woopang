using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 대표님 23 · 24번 「지하철의 경우 API확인해서 출구번호도 나오도록」 「첫차, 막차 등의 정보가 있으면 오브젝트 더블터치했을 때 관련한 내용이 사진으로 불러올 수 있도록」
        //  · 서버(/api/nearby-facilities)가 지하철역에 lines · exits · photos(첫차·막차 · 출구 안내 카드 주소)를 붙인다 — 앱은 FacilityInfo 한 곳에서 정보 창에 넣는다
        //  · 풀의 오브젝트는 맨 위가 꺼져 있다 — 자식(Cube)을 다른 매니저처럼 (true) 로 찾는다 (11-27 시험에선 예전 () 도 찾았다 · 참고로 기록)
        //  · 이미 띄운 역도 다시 받은 목록의 노선 · 출구 · 카드로 바뀐다 (서버는 처음 보는 역을 뒤에서 맞춘다)
        //  카드 그림은 내 PC 시험 서버(127.0.0.1:5794 /transit/)에서 받는다 — 운영 서버에 묻지 않는다. 끝나면 만든 오브젝트 · 채운 값 · 목록 캐시를 되돌린다
        private static void PlanTransitInfo()
        {
            const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
            // 카드는 상대 주소로 넣고 사진 캐시가 시험 서버에서 받게 한다(SubPhotoCache.EditorBase). 앱은 http 를 막아 두어 http 절대 주소는 https 로 바꿔 받는다(ApiConfig.Secure)
            // — 그래서 http 시험 서버 주소를 그대로 넣으면 못 받는다. 운영 서버는 https://woopang.com/api/transit/... 절대 주소를 준다
            const string Base = "http://127.0.0.1:5794";
            const string Card1 = "transit/card1.png?v=2";
            const string Card2 = "transit/card2.png?v=2";
            string baseWas = null;
            bool baseSet = false;
            string json = "[{\"type\":\"subway\",\"name\":\"시험역\",\"latitude\":37.0001,\"longitude\":127.0001,\"address\":\"시험 주소 1\",\"extra_info\":\"\",\"altitude\":0.0,\"main_photo\":null,"
                + "\"lines\":[\"1호선\",\"2호선\"],\"exits\":\"1번 시험미술관 · 2번 시험광장\",\"photos\":[\"" + Card1 + "\",\"" + Card2 + "\"]},"
                + "{\"type\":\"train\",\"name\":\"시험기차역\",\"latitude\":37.0,\"longitude\":127.0,\"address\":null,\"extra_info\":\"KTX\",\"altitude\":0.0,\"main_photo\":null}]";

            SubwayManager sm = null;
            DoubleTap3D host = null, dt = null;
            GameObject probe = null;
            FacilityData v2 = null;
            string uid = null;
            bool opened = false;
            List<CachedPlaceData> cacheWas = null;
            FieldInfo fIsInfo = typeof(DoubleTap3D).GetField("isPlaceInfoPage", NP);
            FieldInfo fIndex = typeof(DoubleTap3D).GetField("imageIndex", NP);
            FieldInfo fInfoText = typeof(DoubleTap3D).GetField("placeInfoText", NP);
            FieldInfo fUrls = typeof(DoubleTap3D).GetField("imageUrls", NP);
            MethodInfo mSetup = typeof(SubwayManager).GetMethod("SetupObject", NP);
            MethodInfo mProcess = typeof(SubwayManager).GetMethod("ProcessFacilityData", NP);
            FieldInfo fSpawned = typeof(SubwayManager).GetField("spawnedObjects", NP);
            FieldInfo fPlaces = typeof(SubwayManager).GetField("placeDataMap", NP);
            FieldInfo fCache = typeof(SubwayManager).GetField("lightCache", NP);

            string Str(DoubleTap3D d, string n) => d == null ? null : typeof(DoubleTap3D).GetField(n, NP)?.GetValue(d) as string;
            List<string> Urls(DoubleTap3D d) => d == null ? null : fUrls?.GetValue(d) as List<string>;
            bool IsInfo() => host != null && fIsInfo?.GetValue(host) is bool b && b;
            int Index() => host != null && fIndex?.GetValue(host) is int i ? i : -99;
            string InfoText() { var t = host != null ? fInfoText?.GetValue(host) as Text : null; return t != null ? t.text : null; }
            bool Open() => host != null && host.fullscreenCanvasGroup != null && host.fullscreenCanvasGroup.gameObject.activeSelf && DoubleTap3D.Shown == host;
            bool Closed() => host == null || host.fullscreenCanvasGroup == null || !host.fullscreenCanvasGroup.gameObject.activeSelf;
            Dictionary<string, GameObject> Spawned() => sm == null ? null : fSpawned?.GetValue(sm) as Dictionary<string, GameObject>;
            Dictionary<string, FacilityData> Places() => sm == null ? null : fPlaces?.GetValue(sm) as Dictionary<string, FacilityData>;
            List<CachedPlaceData> Cache() => sm == null ? null : fCache?.GetValue(sm) as List<CachedPlaceData>;
            string Big()
            {
                var img = host != null ? host.fullscreenImage : null;
                if (img == null || !img.enabled || img.sprite == null) return "없음";
                var tex = img.sprite.texture;
                return tex == null ? "없음" : $"{tex.width}x{tex.height}";
            }
            bool CardShape()   // 카드는 1080x1350 (가로:세로 0.8)
            {
                var img = host != null ? host.fullscreenImage : null;
                var tex = img != null && img.enabled && img.sprite != null ? img.sprite.texture : null;
                return tex != null && Mathf.Abs((float)tex.width / tex.height - 0.8f) < 0.01f;
            }

            Wait(() => !BootOverlay.Showing, 25f, "시작화면 끝");
            Do(() => { if (Active("LoginPromptPanel")) BackNow(); });
            Do(() =>
            {
                var g = Object.FindAnyObjectByType<FirstTimeGuide>(FindObjectsInactive.Include);
                if (g != null && g.Showing) Object.FindAnyObjectByType<R0926GuideOverlay>(FindObjectsInactive.Include)?.Skip();
                sm = SubwayManager.Instance;
                host = R0926PlaceInfo.Host();
                baseWas = SubPhotoCache.EditorBase;   // 처음부터 — 미리 받기(Prefetch)도 운영 서버에 묻지 않게
                baseSet = true;
                SubPhotoCache.EditorBase = Base;
            });
            Wait(() => sm != null && host != null && host.IsReady, 10f, "지하철 매니저 · 대리 정보 창 준비");

            // 1) 서버 응답 읽기 · 정보 글
            Check(() =>
            {
                var list = JsonConvert.DeserializeObject<List<FacilityData>>(json);
                if (list == null || list.Count != 2) return false;
                var s = list[0];
                var t = list[1];
                v2 = s;
                log.Add($"  읽기: 지하철 노선 {s.lines?.Count ?? 0} · 출구 글 {(string.IsNullOrEmpty(s.exits) ? "없음" : "있음")} · 카드 {s.photos?.Count ?? 0} · 기차 새 칸 {(t.lines == null && t.exits == null && t.photos == null ? "없음(null)" : "있음")}");
                return s.lines != null && s.lines.Count == 2 && s.lines[0] == "1호선" && s.exits != null && s.exits.StartsWith("1번")
                    && s.photos != null && s.photos.Count == 2 && s.extra_info == ""
                    && t.lines == null && t.exits == null && t.photos == null && t.extra_info == "KTX";
            }, "서버 응답의 새 칸(lines · exits · photos)을 읽는다 · 없는 줄(기차 · 예전 서버)은 null");
            Check(() =>
            {
                string a = FacilityInfo.Intro(v2);
                string b = FacilityInfo.Intro(new FacilityData { type = "train", name = "x", extra_info = "KTX" });
                log.Add($"  소개 글: 「{a?.Replace("\n", " / ")}」 · 기차 {(b == null ? "없음" : "「" + b + "」")}");
                return a == "노선: 1호선 · 2호선\n출구: 1번 시험미술관 · 2번 시험광장\n출처: 국토교통부 TAGO 지하철정보" && b == null
                    && FacilityInfo.Photos(null).Count == 0 && FacilityInfo.Photos(new FacilityData()).Count == 0
                    && FacilityInfo.Photos(new FacilityData { photos = new List<string> { "", Card1 } }).Count == 1;
            }, "정보 글 — 「노선: …」 「출구: …」 「출처: 국토교통부 TAGO 지하철정보」 · 맞춘 역이 아니면 없음 · 빈 주소는 뺀다");

            // 시험 역은 에디터 가상 위치 그 자리 — 배분이 멀다고 풀로 거두지 않게 (자리는 기록에 찍지 않는다)
            Do(() =>
            {
                if (v2 != null && VirtualLocation.Instance != null)
                {
                    v2.latitude = VirtualLocation.Instance.Latitude;
                    v2.longitude = VirtualLocation.Instance.Longitude;
                }
            });

            // 2) 풀의 꺼진 오브젝트 — 자식(Cube)의 정보 창 · Target 을 찾는다 (예전 방식 결과는 참고로 기록)
            Do(() =>
            {
                probe = Object.Instantiate(sm.samplePrefab, new Vector3(0f, -500f, 0f), Quaternion.identity);
                probe.SetActive(false);   // 풀과 같은 상태 (InitializeObjectPool · ReturnToPool)
            });
            Check(() =>
            {
                if (probe == null) return false;
                var oldDt = probe.GetComponentInChildren<DoubleTap3D>();
                var oldT = probe.GetComponentInChildren<Target>();
                var newDt = probe.GetComponentInChildren<DoubleTap3D>(true);
                var newT = probe.GetComponentInChildren<Target>(true);
                log.Add($"  꺼진 풀 오브젝트: (참고) 예전 방식 DoubleTap3D {(oldDt != null ? "찾음" : "못 찾음")} · Target {(oldT != null ? "찾음" : "못 찾음")} → 지금 DoubleTap3D {(newDt != null ? "찾음" : "못 찾음")} · Target {(newT != null ? "찾음" : "못 찾음")}");
                return newDt != null && newT != null;
            }, "꺼진 풀 오브젝트에서도 정보 창(DoubleTap3D) · Target 을 찾는다");

            // 3) 매니저가 띄울 때 (SetupObject)
            Do(() =>
            {
                mSetup?.Invoke(sm, new object[] { probe, v2 });
                dt = probe != null ? probe.GetComponentInChildren<DoubleTap3D>(true) : null;
            });
            Check(() =>
            {
                var urls = Urls(dt);
                string intro = Str(dt, "intro");
                var tg = probe != null ? probe.GetComponentInChildren<Target>(true) : null;
                ColorUtility.TryParseHtmlString("#3da29c", out Color want);
                bool nameOk = Str(dt, "placeName") == "시험역", addrOk = Str(dt, "address") == "시험 주소 1";
                bool introOk = intro != null && intro.Contains("노선: 1호선 · 2호선") && intro.Contains("출구: 1번 시험미술관");
                bool urlOk = urls != null && urls.Count == 2 && urls[0] == Card1 && urls[1] == Card2;
                bool tOk = tg != null && tg.PlaceName == "시험역" && tg.TargetColor == want;
                log.Add($"  띄울 때: 이름 {(nameOk ? "들어감" : "없음")} · 주소 {(addrOk ? "들어감" : "없음")} · 노선 · 출구 {(introOk ? "들어감" : "없음")} · 카드 주소 {urls?.Count ?? -1} · Target 이름 · 색 {(tOk ? "들어감" : "없음")}");
                return nameOk && addrOk && introOk && urlOk && tOk;
            }, "지하철 매니저가 띄울 때 — 이름 · 주소 · 노선 · 출구 · 카드 주소 두 장 · Target 이름 · 색(#3da29c)");

            // 4) 이미 띄운 역 — 처음엔 서버가 맞추기 전(노선 · 카드 없음) → 다시 받은 목록에 붙어 오면 바뀐다
            Do(() =>
            {
                var v1 = new FacilityData { type = "subway", name = v2.name, latitude = v2.latitude, longitude = v2.longitude, address = v2.address, extra_info = "" };
                mSetup?.Invoke(sm, new object[] { probe, v1 });
                uid = v1.name + "_" + v1.latitude + "_" + v1.longitude;
                var c = Cache();
                cacheWas = c != null ? new List<CachedPlaceData>(c) : null;
                var map = Spawned();
                if (map != null) map[uid] = probe;
            });
            Check(() =>
            {
                var urls = Urls(dt);
                return urls != null && urls.Count == 0 && Str(dt, "intro") == null && Spawned() != null && Spawned().ContainsKey(uid);
            }, "처음엔 노선 · 출구 · 카드 없이 띄워 둠 (서버가 아직 맞추기 전)");
            Do(() =>
            {
                string again = "[" + JsonConvert.SerializeObject(v2) + "]";
                if (mProcess != null) sm.StartCoroutine((IEnumerator)mProcess.Invoke(sm, new object[] { again, (float)v2.latitude, (float)v2.longitude }));
            });
            Wait(() => Urls(dt) != null && Urls(dt).Count == 2, 6f, "다시 받은 목록 반영");
            Check(() =>
            {
                var urls = Urls(dt);
                string intro = Str(dt, "intro");
                log.Add($"  다시 받은 뒤: 카드 주소 {urls?.Count ?? -1} · 노선 · 출구 {(intro != null && intro.Contains("노선") ? "들어감" : "없음")} · 같은 오브젝트 {(Spawned() != null && Spawned().TryGetValue(uid, out var o) && o == probe ? "그대로" : "바뀜")}");
                return urls != null && urls.Count == 2 && intro != null && intro.Contains("출구: 1번") && Spawned() != null && Spawned().TryGetValue(uid, out var same) && same == probe;
            }, "이미 띄운 역도 다시 받은 목록의 노선 · 출구 · 카드로 바뀐다 (오브젝트는 그대로)");

            // 5) 정보 창 — 정보 글이 첫 장 · 다음으로 넘기면 첫차·막차 카드 · 출구 안내 카드
            Do(() => { opened = R0926PlaceInfo.Fill(host, v2) && host.OpenInfo(); });
            Wait(() => Open(), 5f, "역 정보 창 열림");
            Sleep(0.7f);
            Shot("transit_info");
            Check(() =>
            {
                string txt = InfoText();
                bool ok = txt != null && txt.Contains("주소: 시험 주소 1") && txt.Contains("노선: 1호선 · 2호선") && txt.Contains("출구: 1번 시험미술관 · 2번 시험광장") && txt.Contains("출처: 국토교통부 TAGO");
                log.Add($"  첫 장: 정보 글 쪽 {(IsInfo() ? "예" : "아니오")} · 번호 {Index()} · 글 {(ok ? "주소 · 노선 · 출구 · 출처 보임" : "빠진 것 있음")} · 「개요:」 {(txt != null && txt.Contains("개요") ? "있음" : "없음")}");
                return opened && IsInfo() && Index() == -1 && ok && !txt.Contains("개요");
            }, "정보 창 첫 장은 정보 글 — 주소 · 노선 · 출구 · 출처 (개요가 비면 「개요:」 줄 없음)");
            Wait(() => host != null && host.imageSprites != null && host.imageSprites.Count >= 2, 10f, "카드 두 장 받음 (시험 서버)");
            Do(() => host.ShowNextImage());
            Sleep(0.9f);
            Shot("transit_card1");
            Check(() =>
            {
                log.Add($"  다음으로: 정보 글 쪽 {(IsInfo() ? "예" : "아니오")} · 번호 {Index()} · 큰 사진 {Big()}");
                return !IsInfo() && Index() == 0 && CardShape();
            }, "다음으로 넘기면 첫 카드(첫차 · 막차)");
            Do(() => host.ShowNextImage());
            Sleep(0.9f);
            Shot("transit_card2");
            Check(() =>
            {
                log.Add($"  한 번 더: 번호 {Index()} · 큰 사진 {Big()}");
                return Index() == 1 && CardShape();
            }, "한 번 더 넘기면 둘째 카드(출구 안내)");
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => Closed(), 5f, "정보 창 닫힘");

            // 6) 기차역 · 터미널 — 노선 · 카드가 없으면 예전 정보 창 그대로 (주소 · 개요)
            Do(() =>
            {
                var tf = new FacilityData { type = "train", name = "시험기차역", address = "시험 주소 2", extra_info = "KTX" };
                opened = R0926PlaceInfo.Fill(host, tf) && host.OpenInfo();
            });
            Wait(() => Open(), 5f, "기차역 정보 창 열림");
            Sleep(0.6f);
            Check(() =>
            {
                string txt = InfoText();
                var urls = Urls(host);
                log.Add($"  기차역: 정보 글 쪽 {(IsInfo() ? "예" : "아니오")} · 카드 주소 {urls?.Count ?? -1} · 글 {(txt != null && txt.Contains("주소: 시험 주소 2") && txt.Contains("개요: KTX") ? "주소 · 개요 보임" : "빠진 것 있음")} · 노선 줄 {(txt != null && txt.Contains("노선") ? "있음" : "없음")}");
                return opened && IsInfo() && urls != null && urls.Count == 0 && txt != null && txt.Contains("주소: 시험 주소 2") && txt.Contains("개요: KTX") && !txt.Contains("노선");
            }, "기차역 — 노선 · 카드 없이 예전대로 주소 · 개요 (지난 역 카드가 남지 않음)");
            Do(() => DoubleTap3D.CloseShown());
            Wait(() => Closed(), 5f, "정보 창 닫힘");

            // 되돌리기 — 대리 창 값 · 시험 역 오브젝트 · 매니저 자료 · 목록 캐시
            Do(() =>
            {
                if (host != null)
                {
                    host.SetImageUrls(new List<string>(), false);
                    host.ResetData();
                    DataManager.Instance?.ConfigureInfo(host, null);
                }
                var map = Spawned();
                if (map != null && uid != null && map.TryGetValue(uid, out var o) && o == probe) map.Remove(uid);
                Places()?.Remove(uid ?? "");
                var c = Cache();
                if (c != null && cacheWas != null) { c.Clear(); c.AddRange(cacheWas); }
                // 그사이 배분이 풀로 거뒀으면 풀에서도 뺀다 — 지운 오브젝트가 풀에 남지 않게
                if (typeof(SubwayManager).GetField("objectPool", NP)?.GetValue(sm) is Queue<GameObject> pool && pool.Contains(probe))
                {
                    var keep = new List<GameObject>(pool);
                    keep.Remove(probe);
                    pool.Clear();
                    foreach (var g in keep) pool.Enqueue(g);
                }
                if (probe != null) Object.Destroy(probe);
                if (baseSet) { SubPhotoCache.EditorBase = baseWas; baseSet = false; }
            });
            Check(() => Spawned() != null && !Spawned().ContainsKey(uid ?? "") && Places() != null && !Places().ContainsKey(uid ?? "")
                        && (cacheWas == null || Cache().Count == cacheWas.Count) && Urls(host) != null && Urls(host).Count == 0
                        && !baseSet && SubPhotoCache.EditorBase == baseWas,
                "되돌림 — 시험 역 오브젝트 · 자료 · 목록 캐시 · 대리 창 사진 주소 · 사진 서버 주소 처음대로");
        }
    }
}
