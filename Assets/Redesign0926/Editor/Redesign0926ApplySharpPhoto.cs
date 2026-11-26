using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 11-26 대표님 15번 「'근처장소' 탭에서 발생하는 '사용자' 프로필의 경우 사진이 명확하게 발생하지 않고 테두리 질감이 지저분하게 발생하거든? …
    /// 더블터치해서 프로필 봤을 때도 사진이 흐리게 발생하는데」
    ///  · 목록 사람 줄 사진 — 밉맵으로 받고(R0926PlaceRows) R0926SharpPhotoUI(밉맵 반 단계 앞당김). 256 사본을 기기에서 약 80px 로 줄여 그려 자글거렸다
    ///  · 목록 사람 줄 고리 — R0926RingUI(계산 고리). 그림 고리(256px · 밉맵 없음)를 반의반으로 줄여 가장자리가 자글거리고 바깥 번짐이 얼룩졌다
    ///  · 프로필 창 사진(FullProfilePanel/…/AvatarMask/Avatar) — R0926SharpAvatarUI(한 단계 앞당김). 512 사진을 약 300px 로 그릴 때 256 밉맵을 늘려 흐렸다
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string SharpPhotoMatPath = "Assets/Redesign0926/Materials/R0926SharpPhotoUI.mat";
        private const string SharpAvatarMatPath = "Assets/Redesign0926/Materials/R0926SharpAvatarUI.mat";
        private const string RingUiMatPath = "Assets/Redesign0926/Materials/R0926RingUI.mat";

        // 찾고 없으면 만든다 → 값을 다시 넣는다 (셰이더가 없으면 null — 쓰는 쪽은 예전 그대로)
        private static Material UiMat(string path, string shader, List<string> log, params (string name, float value)[] props)
        {
            var sh = Shader.Find(shader);
            if (sh == null) { log.Add("재질: 셰이더 " + shader + " 없음 — 예전 그대로"); return null; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                mat = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
                log.Add("재질 만듦: " + mat.name);
            }
            if (mat.shader != sh) mat.shader = sh;
            foreach (var (n, v) in props)
                if (mat.HasProperty(n) && !Mathf.Approximately(mat.GetFloat(n), v)) mat.SetFloat(n, v);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SharpPhotoMat(List<string> log) => UiMat(SharpPhotoMatPath, "Hidden/R0926/SharpPhotoUI", log, ("_MipBias", -0.5f));
        private static Material SharpAvatarMat(List<string> log) => UiMat(SharpAvatarMatPath, "Hidden/R0926/SharpPhotoUI", log, ("_MipBias", -1f));
        // 안쪽 0.79 · 바깥 0.965 (칸 반지름 비율) — 사진 지름 96 이면 칸 113.9, 띠 45~55 (안쪽이 사진 가장자리를 3 덮는다 · R0926PlaceRows)
        private static Material RingUiMat(List<string> log) => UiMat(RingUiMatPath, "Hidden/R0926/RingUI", log, ("_Inner", 0.79f), ("_Outer", 0.965f));

        // 프로필 창 사진 — 자르는 원(AvatarMask) 아래 사진(Avatar)에 또렷한 재질
        private static void ProfilePhotoSharp(Transform mask, List<string> log)
        {
            var av = mask != null ? mask.Find("Avatar") : null;
            var img = av != null ? av.GetComponent<Image>() : null;
            if (img == null) { log.Add("프로필 창 사진(AvatarMask/Avatar) 없음"); return; }
            var mat = SharpAvatarMat(log);
            if (mat != null && img.material != mat) { img.material = mat; EditorUtility.SetDirty(img); }
        }
    }
}
