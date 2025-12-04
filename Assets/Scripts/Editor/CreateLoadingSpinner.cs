using UnityEngine;
using UnityEditor;

public class CreateLoadingSpinner : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create 3D Sphere Spinner (Dots)")]
    public static void CreateSpinnerSphere()
    {
        GameObject spinner = new GameObject("LoadingSpinner_Sphere");
        
        // 층별 점 개수 (1, 4, 8, 12, 8, 4, 1)
        int[] layerCounts = { 1, 4, 8, 12, 8, 4, 1 };
        float totalRadius = 0.5f;
        
        string matPath = "Assets/Materials/LoadingSpinnerMaterial.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        for (int layer = 0; layer < layerCounts.Length; layer++)
        {
            int count = layerCounts[layer];
            
            // 층의 높이 비율 (-1 ~ 1)
            // layer 0 -> 1 (top)
            // layer 3 -> 0 (center)
            // layer 6 -> -1 (bottom)
            // 정확한 구 형태를 위해 각도(latitude)로 계산
            float latAngle = Mathf.PI * ((float)layer / (layerCounts.Length - 1)) - (Mathf.PI / 2); // -90도 ~ 90도
            // 위 배열 순서대로라면 1이 맨 아래(-90), 12가 중간(0), 1이 맨 위(90)가 됨. (순서는 상관없음)
            
            // 하지만 사용자 요청 순서(1-4-8-12-8-4-1)는 대칭적이므로, 
            // 0번 인덱스를 맨 위(또는 아래)로 잡고 등간격 배치하겠습니다.
            float yRatio = (float)layer / (layerCounts.Length - 1); // 0 ~ 1
            float y = Mathf.Lerp(totalRadius, -totalRadius, yRatio); // 0.5 ~ -0.5
            
            // 해당 높이에서의 반지름 (구의 단면 원 반지름) r = sqrt(R^2 - y^2)
            float layerRadius = Mathf.Sqrt(totalRadius * totalRadius - y * y);
            
            // 점 크기 조절 (가운데는 크고 끝은 작게)
            float dotSize = Mathf.Lerp(0.05f, 0.12f, layerRadius / totalRadius); 

            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count);
                float rad = angle * Mathf.Deg2Rad;
                
                // XZ 평면에 원형 배치, 높이는 y
                Vector3 pos = new Vector3(Mathf.Cos(rad) * layerRadius, y, Mathf.Sin(rad) * layerRadius);

                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.transform.SetParent(spinner.transform, false);
                dot.transform.localPosition = pos;
                dot.transform.localScale = Vector3.one * dotSize;
                
                DestroyImmediate(dot.GetComponent<Collider>());
                if (mat != null) dot.GetComponent<Renderer>().material = mat;
            }
        }

        // 회전 스크립트
        SpinnerRotator rotator = spinner.AddComponent<SpinnerRotator>();
        rotator.axis = new Vector3(0, 1, 0); // Y축 회전 (지구가 자전하듯이)
        // 약간 기울여서 회전시키면 더 입체감이 남
        spinner.transform.rotation = Quaternion.Euler(15, 0, 15); 
        
        // 항상 카메라 보기 (Billboard) - 구체니까 굳이 필요 없거나, 
        // 전체 덩어리가 항상 정면을 보게 하려면 필요.
        spinner.AddComponent<Billboard>();
        
        string prefabPath = "Assets/Prefabs/LoadingSpinner.prefab";
        if (!System.IO.Directory.Exists("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        
        PrefabUtility.SaveAsPrefabAsset(spinner, prefabPath);
        DestroyImmediate(spinner);
        
        EditorUtility.DisplayDialog("완료", "3D 구체형 로딩 스피너가 생성되었습니다!", "확인");
    }
#endif
}