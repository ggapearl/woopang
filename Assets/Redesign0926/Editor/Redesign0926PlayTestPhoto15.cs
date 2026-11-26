using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Redesign0926
{
    public static partial class Redesign0926PlayTest
    {
        // 11-26 대표님 15번 「'근처장소' 탭에서 발생하는 '사용자' 프로필의 경우 사진이 명확하게 발생하지 않고 테두리 질감이 지저분하게 발생하거든? …
        // 더블터치해서 프로필 봤을 때도 사진이 흐리게 발생하는데」
        //  내 PC 시험 서버(127.0.0.1:5797 · 시험 사진 512 — 가는 선 · 글자)로만 — 운영 서버에 묻지 않는다
        //  1) 근처 카드 — 사람 원판을 보이는 원으로 재 카드에 크게 · 2) 두 번 톡톡 = 프로필 창 사진에 또렷한 재질
        //  3) 목록 「근처 장소」 사람 줄 — 사진 밉맵 · 3선형 · 또렷한 재질 · 계산 고리 · 4) 근처 카드 사진 칸(원) 마스크가 고리 띠 안쪽
        //  같은 자리에서 고치기 전 모습(판 대각선 재기 · 기본 재질 · 그림 고리 · 밉맵 없는 사진)도 찍어 견준다 (캡처는 로컬에만). 끝나면 전부 되돌린다
        private static void PlanPhoto15()
        {
            const string Stub = "http://127.0.0.1:5797";
            const string Uid = "p15";
            const float Disc = 96f;
            const BindingFlags IF = BindingFlags.NonPublic | BindingFlags.Instance;
            const BindingFlags SF = BindingFlags.NonPublic | BindingFlags.Static;
            string avatar = Stub + "/uploads/avatars/t15.jpg";
            P2PManager pm = null;
            var filterWas = UserFilterMode.All;
            GameObject av = null;
            Material photoMat = null, ringMat = null, avatarMat = null, discMat = null;
            float discBiasWas = -0.5f;
            Sprite oldRingSprite = null;
            Image avImg = null;
            Texture mipTex = null;
            Texture2D flat = null;            // 고치기 전 모습 — 밉맵 없이 받은 같은 256 사진
            UnityWebRequest flatReq = null;
            Vector2 ringSizeNew = default;
            var discF = typeof(R0926NearModal).GetField("discShader", SF);
            var discLookedF = typeof(R0926NearModal).GetField("discLooked", SF);
            var texUrlF = typeof(P2PUserInfo).GetField("avatarTextureUrl", IF);

            string LiveJson()
            {
                double lat = VirtualLocation.Instance.Latitude + 12 / 111320.0, lon = VirtualLocation.Instance.Longitude;
                return "{\"users\":[{\"user_id\":\"" + Uid + "\",\"username\":\"" + Uid + "\",\"latitude\":" + lat.ToString("0.0000000", CultureInfo.InvariantCulture)
                    + ",\"longitude\":" + lon.ToString("0.0000000", CultureInfo.InvariantCulture) + ",\"altitude\":0,\"avatar_url\":\"" + avatar
                    + "\",\"avatar_glb_url\":\"\",\"distance\":12,\"visibility_mode\":\"public\"}]}";
            }
            // 목록은 http 주소를 https 로 바꾼다 — 시험 서버로 돌리고, 운영처럼 256 사본(/thumb/256)을 받게
            string MapUrl(string u)
            {
                const string S = "https://127.0.0.1:5797/";
                return u.StartsWith(S, StringComparison.Ordinal) ? Stub + "/thumb/256/" + u.Substring(S.Length) : u;
            }
            Transform RowOf()
            {
                var plm = Plm();
                var list = ListRows();
                if (plm == null) return null;
                var shown = plm.ShownEntries;
                for (int i = 0; i < shown.Count && i < list.Count; i++)
                    if (shown[i].kind == PlaceListManager.EntryKind.Person && shown[i].id == Uid) return list[i];
                return null;
            }
            RawImage Pic() { var r = RowOf(); var t = r != null ? r.Find("Photo/Pic") : null; return t != null ? t.GetComponent<RawImage>() : null; }
            Image Ring() { var r = RowOf(); var t = r != null ? r.Find("PersonRing") : null; return t != null ? t.GetComponent<Image>() : null; }
            // 캡처에서 자를 자리 (가운데 · 반지름 — 화면 픽셀, 위가 0)
            void Where(string name, RectTransform rt, float size)
            {
                if (rt == null) return;
                var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 c = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
                Vector2 e = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center + new Vector2(size * 0.5f, 0f)));
                log.Add($"  자르기 {name} {c.x:0},{Screen.height - c.y:0} 반지름 {Mathf.Abs(e.x - c.x):0.0}px (화면 {Screen.width}x{Screen.height})");
            }
            string PathOf(Transform t) { string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }

            Do(() => R0926FriendsMap.EditorBase = NmDeadStub);   // 친구 자리 받기는 닫힌 포트로
            NmBoot(true, true);
            Do(NmClearAround);
            Do(() =>
            {
                pm = P2PManager.Instance;
                if (pm == null || VirtualLocation.Instance == null || Camera.main == null) throw new Exception("P2PManager · 가상 위치 · 카메라 없음");
                var rows = UnityEngine.Object.FindAnyObjectByType<R0926PlaceRows>(FindObjectsInactive.Include);
                var so = new SerializedObject(rows);
                photoMat = so.FindProperty("photoMaterial").objectReferenceValue as Material;
                ringMat = so.FindProperty("ringMaterial").objectReferenceValue as Material;
                oldRingSprite = so.FindProperty("personRingSprite").objectReferenceValue as Sprite;
                ProfileManager.EditorBase = Stub;   // 프로필 · 팔로우 — 시험 서버 (NmFinish 가 되돌린다)
                R0926PlaceRows.EditorPhotoUrl = MapUrl;
                filterWas = pm.GetUserFilterMode();
                if (filterWas != UserFilterMode.All) pm.SetUserFilterMode(UserFilterMode.All);   // 플레이 동안만 (저장값은 그대로)
                pm.InjectTestUsersData(LiveJson());
            });
            Check(() =>
            {
                log.Add($"  목록 사진 재질 {(photoMat != null ? photoMat.name + " (앞당김 " + photoMat.GetFloat("_MipBias") + ")" : "없음")} · 고리 재질 {(ringMat != null ? ringMat.name + " (안쪽 " + ringMat.GetFloat("_Inner") + " · 바깥 " + ringMat.GetFloat("_Outer") + ")" : "없음")} · 그림 고리 {(oldRingSprite != null ? oldRingSprite.name : "없음")}");
                return photoMat != null && photoMat.shader.name == "Hidden/R0926/SharpPhotoUI" && ringMat != null && ringMat.shader.name == "Hidden/R0926/RingUI";
            }, "적용 — 목록 줄에 또렷한 사진 재질 · 계산 고리 재질 연결");
            Sleep(0.5f);
            Do(() =>
            {
                av = pm.GetActiveAvatars().TryGetValue(Uid, out var go) ? go : null;
                if (av == null) throw new Exception("시험 사람 p15 가 안 떴다");
                NmPut(av, 1.6f);
            });
            Wait(() => nmModal.EditorShowing && nmModal.EditorCurrent == av, 4f, "사람 1.6m — 근처 카드 뜸");
            Wait(() =>
            {
                var ui = av != null ? av.GetComponentInChildren<P2PUserInfo>(true) : null;
                return ui != null && texUrlF != null && texUrlF.GetValue(ui) as string == avatar;
            }, 8f, "원판에 시험 사진");
            Sleep(1.6f);   // 카드 크기가 자리 잡게 (0.6초 넘게 달라야 따라가고 부드럽게 다가간다)
            Shot("p15_near");
            Check(() =>
            {
                var cov = NmCoverage(nmModal.EditorTexture(0));
                var view = nmModal.GetComponentsInChildren<RawImage>(true).FirstOrDefault(x => x.texture != null && x.texture == nmModal.EditorTexture(0));
                if (view != null) Where("near", view.rectTransform, view.rectTransform.rect.width);
                log.Add($"  카드 판에서 사람 원판 {cov.x:0.000}×{cov.y:0.000}");
                return Mathf.Max(cov.x, cov.y) > 0.6f;
            }, "근처 카드 — 사람 원판을 보이는 원(테두리 · 물결 끝)으로 재 카드에 크게 (예전엔 판 대각선으로 재 약 절반)");
            // 고치기 전 — 판 대각선으로 재기 · 원판 사진 밉맵 앞당김 없음
            Do(() =>
            {
                var mr = av.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(x => x.sharedMaterial != null && x.sharedMaterial.shader != null && x.sharedMaterial.shader.name.EndsWith("R0926PersonDisc"));
                discMat = mr != null ? mr.material : null;   // 사람마다 쓰는 재질 (P2PUserInfo 가 만든 것 — 에셋은 건드리지 않는다)
                if (discMat != null && discMat.HasProperty("_MipBias")) { discBiasWas = discMat.GetFloat("_MipBias"); discMat.SetFloat("_MipBias", 0f); }
                discLookedF?.SetValue(null, true);
                discF?.SetValue(null, null);
            });
            Sleep(2.4f);
            Shot("p15_near_old");
            Check(() =>
            {
                var cov = NmCoverage(nmModal.EditorTexture(0));
                log.Add($"  (고치기 전) 카드 판에서 사람 원판 {cov.x:0.000}×{cov.y:0.000} · 원판 재질 {(discMat != null ? "찾음" : "못 찾음")}");
                return discMat != null;
            }, "(견주기) 고치기 전 모습 — 판 대각선으로 잰 크기 · 밉맵 앞당김 없음");
            Do(() =>
            {
                if (discMat != null) discMat.SetFloat("_MipBias", discBiasWas);
                discLookedF?.SetValue(null, false);   // 다음 재기 때 원판 셰이더를 다시 찾는다
            });
            Sleep(2.4f);

            // 두 번 톡톡 = 프로필 창 (AR 에서 두 번 누른 것과 같은 길) — 시험 서버의 프로필 · 사진
            Do(() => { nmModal.EditorTap(); nmModal.EditorTap(); });
            Wait(() => Active("FullProfilePanel"), 4f, "두 번 톡톡 — 프로필 창");
            Wait(() =>
            {
                avImg = ProfileManager.Instance != null ? ProfileManager.Instance.avatarImage : null;
                var s = avImg != null ? avImg.sprite : null;
                return s != null && s.texture != null && s.texture.width == 512;
            }, 8f, "프로필 창에 시험 사진(512)");
            Sleep(0.6f);
            Shot("p15_profile");
            Check(() =>
            {
                if (avImg == null || avImg.sprite == null) return false;
                var t = avImg.sprite.texture;
                string path = PathOf(avImg.transform);
                Where("profile", avImg.rectTransform, avImg.rectTransform.rect.width);
                log.Add($"  프로필 사진 칸 {path} · 재질 {avImg.material.name} · 사진 {t.width}×{t.height} · 밉맵 {t.mipmapCount}");
                return avImg.material != null && avImg.material.name.StartsWith("R0926SharpAvatarUI") && t.mipmapCount > 1 && path.EndsWith("FullProfilePanel/Content/TopPanel/AvatarMask/Avatar");
            }, "프로필 창 사진 — 또렷한 재질(밉맵 한 단계 앞당김) · 512 사진");
            Do(() => { if (avImg != null) { avatarMat = avImg.material; avImg.material = null; } });
            Sleep(0.3f);
            Shot("p15_profile_old");
            Do(() => { if (avImg != null && avatarMat != null) avImg.material = avatarMat; });
            Back();
            Sleep(0.6f);
            Do(() => { if (Active("FullProfilePanel")) Click("FullProfilePanel/Content/CloseButton"); });
            Wait(() => !Active("FullProfilePanel"), 3f, "프로필 창 닫힘");

            // 사람을 멀리 — 근처 카드를 닫고 목록으로
            Do(() => NmPut(av, 9f));
            Wait(() => !nmModal.EditorShowing, 4f, "사람 9m — 근처 카드 닫힘");
            Do(() => Click("Dock0926/List_Button"));
            Sleep(1.0f);
            Do(() => Modes()?.Show(0));
            Sleep(0.6f);
            Wait(() =>
            {
                if (!pm.GetActiveAvatars().ContainsKey(Uid)) pm.InjectTestUsersData(LiveJson());   // 운영 근처 사람 받기가 시험 사람을 지웠으면 다시
                return RowOf() != null;
            }, 6f, "목록에 시험 사람 줄");
            Wait(() => { var p = Pic(); return p != null && p.enabled && p.texture != null; }, 10f, "사람 줄 사진 받음");
            Sleep(0.8f);   // 사진 나타나기 0.2초
            Shot("p15_list");
            Check(() =>
            {
                var p = Pic();
                var t = p != null ? p.texture as Texture2D : null;
                if (t == null) return false;
                log.Add($"  사진 {t.width}×{t.height} · 밉맵 {t.mipmapCount} · 거르기 {t.filterMode} · 재질 {p.material.name}");
                Where("list", (RectTransform)p.transform.parent, Disc);
                return t.width == 256 && t.mipmapCount > 1 && t.filterMode == FilterMode.Trilinear && p.material == photoMat;
            }, "목록 사람 줄 사진 — 256 사본을 밉맵으로 받고 3선형 · 또렷한 재질");
            Check(() =>
            {
                var r = Ring();
                if (r == null) return false;
                var rt = r.rectTransform;
                float want = (Disc * 0.5f - 3f) / ringMat.GetFloat("_Inner") * 2f;
                string col = ColorUtility.ToHtmlStringRGB(r.color);
                bool skin = r.GetComponent<R0926WindowFill>() != null;
                log.Add($"  고리 그림 {(r.sprite != null ? r.sprite.name : "없음")} · 재질 {r.material.name} · 크기 {rt.sizeDelta.x:0.0} (맞을 값 {want:0.0}) · 색 #{col} · 켜짐 {r.gameObject.activeSelf} · 창 스킨 붙음 {skin} (물방울 {R0926MenuStyle.Droplet})");
                return r.sprite == null && r.material == ringMat && Mathf.Abs(rt.sizeDelta.x - want) < 0.5f && r.gameObject.activeSelf && col == "E95383"
                       && !skin && r.GetComponent<R0926SkinIgnore>() != null;
            }, "목록 사람 줄 고리 — 계산 고리(그림 없음) · 안쪽 가장자리가 사진 가장자리를 3 덮는 크기 · 전체공개 분홍 · 창 스킨에서 빠짐(물방울 모드에서 안 옅어짐)");
            // 고치기 전 — 밉맵 없이 받은 사진 · 기본 재질 · 그림 고리(r0926_ring_mini)
            Do(() =>
            {
                flatReq = UnityWebRequestTexture.GetTexture(Stub + "/thumb/256/uploads/avatars/t15.jpg", true);
                flatReq.SendWebRequest();
            });
            Wait(() => flatReq == null || flatReq.isDone, 5f, "(견주기) 밉맵 없는 같은 사진");
            Do(() =>
            {
                if (flatReq != null)
                {
                    flat = flatReq.result == UnityWebRequest.Result.Success ? DownloadHandlerTexture.GetContent(flatReq) : null;
                    flatReq.Dispose();
                    flatReq = null;
                }
                var p = Pic();
                var r = Ring();
                if (p == null || r == null) return;
                mipTex = p.texture;
                if (flat != null) { flat.wrapMode = TextureWrapMode.Clamp; p.texture = flat; }
                p.material = null;
                ringSizeNew = r.rectTransform.sizeDelta;
                r.material = null;
                r.sprite = oldRingSprite;
                r.preserveAspect = true;
                float old = (Disc * 0.5f - 3f) / 0.68f * 2f;
                r.rectTransform.sizeDelta = new Vector2(old, old);
            });
            Sleep(0.3f);
            Shot("p15_list_old");
            Do(() =>
            {
                var p = Pic();
                var r = Ring();
                if (p != null && mipTex != null) { p.texture = mipTex; p.material = photoMat; }
                if (r != null && ringSizeNew != default) { r.sprite = null; r.material = ringMat; r.rectTransform.sizeDelta = ringSizeNew; }
                if (flat != null) UnityEngine.Object.Destroy(flat);
                flat = null;
            });
            Check(() =>
            {
                var p = Pic();
                var r = Ring();
                return p != null && p.material == photoMat && p.texture == mipTex && r != null && r.sprite == null && r.material == ringMat;
            }, "(견주기) 끝 — 사진 · 고리 고친 모습으로 되돌림");

            // 근처 카드 사진 칸 — 사람(원)은 마스크를 고리 띠 안쪽으로 · 장소(둥근 네모)는 칸 그대로
            Check(() =>
            {
                var slot = nmModal.GetComponentsInChildren<R0926NearPhoto>(true).FirstOrDefault();
                var mask = slot != null ? slot.transform.Find("Mask") as RectTransform : null;
                if (mask == null) { log.Add("  사진 칸 없음"); return false; }
                slot.Show(1f, true, null, ProfileManager.OutlineColor("public"));
                Vector2 pMin = mask.offsetMin, pMax = mask.offsetMax;
                slot.Show(1f, false, null, Color.white);
                Vector2 lMin = mask.offsetMin, lMax = mask.offsetMax;
                slot.Hide();
                log.Add($"  사람(원) 마스크 안쪽으로 {pMin.x:0.#} · {-pMax.x:0.#} · 장소(둥근 네모) {lMin.x:0.#} · {-lMax.x:0.#} · 칸 {((RectTransform)slot.transform).rect.width:0}");
                return Mathf.Abs(pMin.x - 10f) < 0.01f && Mathf.Abs(pMin.y - 10f) < 0.01f && Mathf.Abs(pMax.x + 10f) < 0.01f && Mathf.Abs(pMax.y + 10f) < 0.01f
                    && lMin == Vector2.zero && lMax == Vector2.zero;
            }, "근처 카드 사진 칸 — 사람(원)은 사진을 고리 띠 안쪽으로 10 · 장소는 칸 그대로");

            // 되돌리기 — 시험 사람 · 사람 칩 · 사진 주소 · 원판 재질 (서버 주소는 NmFinish)
            Do(() =>
            {
                if (flatReq != null) { flatReq.Dispose(); flatReq = null; }
                if (flat != null) { UnityEngine.Object.Destroy(flat); flat = null; }
                if (discMat != null) discMat.SetFloat("_MipBias", discBiasWas);
                discLookedF?.SetValue(null, false);
                R0926PlaceRows.EditorPhotoUrl = null;
                if (pm != null)
                {
                    pm.RemoveTestUserAvatar(Uid);
                    if (pm.GetUserFilterMode() != filterWas) pm.SetUserFilterMode(filterWas);
                    log.Add($"  되돌림: 사람 칩 {pm.GetUserFilterMode()} · 시험 사람 {(pm.GetActiveAvatars().ContainsKey(Uid) ? "남음" : "없음")}");
                }
            });
            Back();
            Sleep(0.5f);
            Do(NmFinish);
        }
    }
}
