using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Redesign0926
{
    /// <summary>
    /// 사람 오브젝트 (10-25 대표님 「기본적으로는 '빌보드' 효과로 그냥 정면이 항상 보이는 동그라미 (이것도 확대축소 가능) …
    /// 테두리에 핑크색 원이 감싸고 있도록 (우측하단 프로필 버튼처럼)」).
    /// 씬의 P2P_Avatar/Cube 를 사진 원으로 바꾼다 — 예전엔 프로필 사진을 붙인 육면체였다.
    /// 터치 · 화살표(Target) · 거리 표시 컴포넌트는 Cube 에 그대로 둔다. 원은 P2PBillboard 가 늘 카메라로 돌린다.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const string PersonDiscMatPath = "Assets/Redesign0926/Materials/PersonDisc_Mat.mat";
        private const float PersonDiscScale = 1.5f;      // 원 지름(판의 72%)이 예전 육면체 한 변과 비슷하게
        private const float PersonTouchSize = 0.76f;     // 누르는 칸 — 원 + 숨쉬는 테두리

        private static void ApplyPerson(Scene scene, List<string> log)
        {
            GameObject avatar = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "P2P_Avatar") { avatar = go; break; }
            var cube = avatar != null ? avatar.transform.Find("Cube") : null;
            if (cube == null) { log.Add("사람 오브젝트: P2P_Avatar/Cube 없음"); return; }

            var sh = Shader.Find("Universal Render Pipeline/R0926PersonDisc");
            if (sh == null) { log.Add("사람 오브젝트: 셰이더 R0926PersonDisc 없음"); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PersonDiscMatPath);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, PersonDiscMatPath);
            }
            mat.shader = sh;
            mat.SetColor("_RingColor", Pink);
            EditorUtility.SetDirty(mat);

            // 메시를 ProBuilder 가 쥐고 있으면 편집기에서 육면체로 되돌린다 — 판으로 갈기 전에 뗀다 (옛 육면체 메시는 씬에 들어 있던 것이라 같이 사라진다)
            var pb = cube.GetComponent<ProBuilderMesh>();
            if (pb != null) Object.DestroyImmediate(pb);

            var filter = cube.GetComponent<MeshFilter>();
            filter.hideFlags = HideFlags.None;
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

            var mr = cube.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            cube.localScale = Vector3.one * PersonDiscScale;
            var box = cube.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size = new Vector3(PersonTouchSize, PersonTouchSize, 0.1f);
                box.center = Vector3.zero;
            }

            // 예전 육면체 둘레 빛 — 물결은 셰이더가 그린다 (자리는 남겨 둔다)
            var pulse = cube.Find("Pulse");
            if (pulse != null) pulse.gameObject.SetActive(false);

            // 드래그로 돌리기는 3D 모델일 때만 — 원은 늘 정면 (P2PUserInfo 가 켠다)
            var rotator = cube.GetComponent<CubeTouchRotator>();
            if (rotator != null) rotator.enabled = false;

            var bb = Ensure<P2PBillboard>(cube.gameObject);
            var so = new SerializedObject(bb);
            so.FindProperty("reverse").boolValue = true;   // Quad 앞면은 -Z — 사진이 뒤집혀 보이지 않게
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(cube.gameObject);
            EditorUtility.SetDirty(mr);
            EditorUtility.SetDirty(filter);
            AssetDatabase.SaveAssets();
            log.Add("사람 오브젝트: 사진 원 + 분홍 테두리");
        }
    }
}
