using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 0926 씬의 루트 Canvas 만 골라 상태별로 PNG 캡처한다. 켠 패널·캔버스 모드는 전부 원상복구.
    /// &lt;dir&gt;/_bg.png 가 있으면 카메라 화면 대신 깔아서 실제 대비를 본다.
    /// </summary>
    public static class Redesign0926Capture
    {
        private const int Width = 590;    // iPhone 15 (1179x2556) 의 절반
        private const int Height = 1278;

        private static readonly Dictionary<string, string[]> States = new Dictionary<string, string[]>
        {
            { "main", new string[0] },
            { "list", new[] { "ListPanel", "XButton_List", "!List_Button" } },
            { "fmap0", new[] { "ListPanel", "XButton_List", "!List_Button", "MapPanel0926" } },
            { "fmap", new[] { "ListPanel", "XButton_List", "!List_Button", "MapPanel0926" } },
            { "fmapz", new[] { "ListPanel", "XButton_List", "!List_Button", "MapPanel0926" } },
            { "fset", new[] { "ListPanel", "XButton_List", "!List_Button", "SettingsPanel0926" } },
            { "skychip", new[] { "^SkyChip0926" } },
            { "splash", new[] { "Splash0926" } },
            { "splash_mid", new[] { "Splash0926" } },
            { "splash_ja", new[] { "Splash0926" } },
            { "splash_zh", new[] { "Splash0926" } },
            { "splash_es", new[] { "Splash0926" } },
            { "splash_en", new[] { "Splash0926" } },
            { "splash_mid_ja", new[] { "Splash0926" } },
            { "splash_mid_zh", new[] { "Splash0926" } },
            { "splash_mid_es", new[] { "Splash0926" } },
            { "splash_mid_en", new[] { "Splash0926" } },
            { "profile", new[] { "FullProfilePanel" } },
            { "follow", new[] { "FollowPanel" } },
            { "dm", new[] { "MessagePanel", "MessagePanel>CloseButton", "!Message_Button" } },
            { "chat", new[] { "ChatRoomPanel", "ChatRoomPanel>CloseButton", "Popover0926", "!Message_Button" } },
            { "full", new[] { "FullScreenPanel" } },
            { "comment", new[] { "CommentPanel_HQ" } },
            { "remove", new[] { "RemoveRequestPanel" } },
            { "fix", new[] { "Fixpage" } },
            { "dance", new[] { "DanceAnimPanel" } },
            { "upload", new[] { "UploadPage", "XButton_Upload", "!ModelUploadPage", "!PlusButton" } },
            { "login", new[] { "LoginPromptPanel" } },
            { "perm", new[] { "LocationPermissionPanel", "Box" } },
            { "guide", new[] { "FirstTimeGuidePanel" } },
            { "more", new[] { "FixButtonPanel", "FixButton_Opposite" } },
            { "report", new[] { "ReportSheet0926" } },
            { "loading", new[] { "LoadingPanel" } },
            { "net", new[] { "^NetBanner0926" } },
            { "cont", new[] { "ContinueCaptureDialog>DialogPanel" } },
            { "skelc", new[] { "CommentPanel_HQ", "SkeletonComment_Template" } },
            { "banner", new[] { "NotificationBanner" } },
            { "warn", new[] { "WarningText" } },
            { "update", new[] { "UpdateChecker" } },
            { "photodlg", new[] { "PhotoSourceDialog>DialogPanel" } },
            { "arprev", new[] { "ARPreviewPanel" } },
            { "cube", new[] { "UploadPage", "CubeUploadPage", "!ModelUploadPage", "!PlusButton", "XButton_Upload" } },
            { "model", new[] { "UploadPage", "ModelUploadPage", "!CubeUploadPage", "!PlusButton", "XButton_Upload" } },
        };

        // PlaceListManager 가 실제로 쓰는 형식 그대로 (캡처 미리보기용 예시)
        private const string SampleList =
            "<color=#E854A1>연남고집 - 42m</color>\n" +
            "<color=#E95383>👤 sigorpd - 65m</color>\n" +
            "<color=#FBC15D>꼬요 - 118m</color>\n" +
            "<color=#4DD980>경의선숲길 공원 - 180m</color>\n" +
            "<color=#4080F2>소제이 - 260m</color>\n" +
            "<color=#AE54C4>연남동 공중화장실 - 290m</color>\n" +
            "<color=#3DA29C>홍대입구역 - 640m</color>\n" +
            "<color=#E854A1>시실리 - 1240m</color>\n" +
            "<color=#D9A621>연트럴파크 - 1480m</color>\n" +
            "<color=#FBC15D>토쿠이 - 2210m</color>\n" +
            "\n우팡 데이터: 12\n공공데이터: 9\n대중교통 데이터: 2\n근처 사용자: 1";

        public static void CaptureStates(Scene scene, string dir, string[] states, List<string> log)
        {
            Directory.CreateDirectory(dir);
            Canvas canvas = FindMainCanvas(scene);
            if (canvas == null) { log.Add("capture: Canvas 없음"); return; }

            foreach (var raw in states)
            {
                string state = raw.Trim();
                if (state.Length == 0) continue;
                bool land = state.StartsWith("L_");
                string key = land ? state.Substring(2) : state;
                string[] names = States.TryGetValue(key, out var n) ? n : key.Split('+');
                var orient = land ? canvas.GetComponentInChildren<R0926Orientation>(true) : null;
                var toggled = new List<GameObject>();
                var hidden = new List<GameObject>();
                var alphaRestore = new List<(CanvasGroup, float)>();
                try
                {
                    foreach (var raw2 in names)
                    {
                        if (raw2.StartsWith("^"))
                        {
                            var shown = FindInCanvas(canvas.transform, raw2.Substring(1));
                            var cgp = shown != null ? shown.GetComponent<CanvasGroup>() : null;
                            if (cgp != null) { alphaRestore.Add((cgp, cgp.alpha)); cgp.alpha = 1f; }
                            continue;
                        }
                        bool off = raw2.StartsWith("!");
                        string name = off ? raw2.Substring(1) : raw2;
                        var go = FindInCanvas(canvas.transform, name);
                        if (off)
                        {
                            if (go != null && go.activeSelf) { go.SetActive(false); hidden.Add(go); }
                            continue;
                        }
                        if (go == null) { log.Add($"capture {state}: '{name}' 없음"); continue; }
                        // 부모까지 켜야 보인다
                        for (Transform t = go.transform; t != null && t != canvas.transform; t = t.parent)
                        {
                            if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); toggled.Add(t.gameObject); }
                        }
                    }
                    var rowsView = key == "list" ? canvas.GetComponentInChildren<R0926PlaceRows>(true) : null;
                    if (rowsView != null) rowsView.EditorPreview(SampleList);
                    if (orient != null) orient.ApplyAll(true, 1458f);
                    var splash = key.StartsWith("splash") ? canvas.GetComponentInChildren<R0926Splash>(true) : null;
                    if (splash != null)
                    {
                        bool mid = key.StartsWith("splash_mid");   // 렌즈가 훑는 중 (0.95초)
                        int cut = mid ? 11 : 7;
                        string lang = key.Length > cut ? key.Substring(cut) : "ko";
                        Canvas.ForceUpdateCanvases();
                        splash.EditorPreview(lang, mid ? 0.95f : 3.3f);
                        Canvas.ForceUpdateCanvases();
                    }
                    var modes = key.StartsWith("fmap") || key == "fset" ? canvas.GetComponentInChildren<R0926SheetModes>(true) : null;
                    if (modes != null) modes.Show(key == "fset" ? 2 : 1);
                    var fmap = key.StartsWith("fmap") ? canvas.GetComponentInChildren<R0926FriendsMap>(true) : null;
                    if (fmap != null)
                    {
                        Canvas.ForceUpdateCanvases();
                        fmap.EditorPreview(key != "fmap0", key == "fmapz" ? 22f : 0f, 36.4f, 130.8f);
                        Canvas.ForceUpdateCanvases();
                    }
                    string path = Path.Combine(dir, state + ".png");
                    log.Add(Render(canvas, path, dir, land) ? "saved " + path : "render 실패 " + state);
                    if (orient != null) orient.ApplyAll(false);
                    if (rowsView != null) rowsView.EditorPreviewClear();
                    if (fmap != null) fmap.EditorPreviewClear();
                    if (modes != null) modes.Show(0);
                    if (splash != null) splash.EditorPreviewClear();
                }
                finally
                {
                    for (int i = toggled.Count - 1; i >= 0; i--)
                        if (toggled[i] != null) toggled[i].SetActive(false);
                    foreach (var h in hidden) if (h != null) h.SetActive(true);
                    foreach (var (g, al) in alphaRestore) if (g != null) g.alpha = al;
                }
            }
        }

        public static Canvas FindMainCanvas(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponent<Canvas>();
                if (c != null && root.name == "Canvas") return c;
            }
            return null;
        }

        public static GameObject FindInCanvas(Transform root, string name)
        {
            int k = name.IndexOf('>');
            if (k > 0)
            {
                var parent = FindInCanvas(root, name.Substring(0, k));
                return parent == null ? null : FindInCanvas(parent.transform, name.Substring(k + 1));
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static bool Render(Canvas canvas, string path, string dir, bool landscape = false)
        {
            int w = landscape ? Height : Width, h = landscape ? Width : Height;
            var camGO = new GameObject("~R0926Cam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.5f, 0.56f, 1f);
            cam.orthographic = true;
            cam.cullingMask = 1 << 5;
            cam.transform.position = new Vector3(0, 0, -5000);

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            var prevMode = canvas.renderMode;
            var prevCam = canvas.worldCamera;
            var prevDist = canvas.planeDistance;
            GameObject bg = null;
            Texture2D bgTex = null;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;

                string bgPath = Path.Combine(dir, landscape && File.Exists(Path.Combine(dir, "_bg_land.png")) ? "_bg_land.png" : "_bg.png");
                if (File.Exists(bgPath))
                {
                    bgTex = new Texture2D(2, 2);
                    bgTex.LoadImage(File.ReadAllBytes(bgPath));
                    bg = new GameObject("~R0926Bg", typeof(RectTransform), typeof(RawImage));
                    bg.layer = 5;
                    bg.transform.SetParent(canvas.transform, false);
                    bg.transform.SetAsFirstSibling();
                    var brt = (RectTransform)bg.transform;
                    brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                    brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
                    var ri = bg.GetComponent<RawImage>();
                    ri.texture = bgTex;
                    ri.raycastTarget = false;
                }

                Canvas.ForceUpdateCanvases();
                cam.Render();
                // 레이아웃 그룹은 한 프레임 늦게 잡히는 경우가 있어 두 번 그린다
                Canvas.ForceUpdateCanvases();
                cam.Render();

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Redesign0926] 캡처 실패: " + e.Message);
                return false;
            }
            finally
            {
                if (bg != null) Object.DestroyImmediate(bg);
                if (bgTex != null) Object.DestroyImmediate(bgTex);
                canvas.renderMode = prevMode;
                canvas.worldCamera = prevCam;
                canvas.planeDistance = prevDist;
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGO);
            }
        }
    }
}
