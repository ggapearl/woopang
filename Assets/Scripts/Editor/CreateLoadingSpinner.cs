using UnityEngine;
using UnityEditor;

public class CreateLoadingSpinner : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create Loading Spinner Prefab")]
    public static void CreateSpinner()
    {
        // 1. 구체 생성
        GameObject spinner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spinner.name = "LoadingSpinner";
        
        // 2. 크기 조정
        spinner.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        
        // 3. 콜라이더 제거
        DestroyImmediate(spinner.GetComponent<Collider>());
        
        // 4. 머티리얼 적용 (경로가 다르면 수정 필요)
        string matPath = "Assets/Materials/LoadingSpinnerMaterial.mat"; 
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat != null)
        {
            spinner.GetComponent<Renderer>().material = mat;
        }
        
        // 5. 회전 스크립트 추가
        spinner.AddComponent<SpinnerRotator>();
        
        // 6. 프리팹 저장
        string prefabPath = "Assets/Prefabs/LoadingSpinner.prefab";
        if (!System.IO.Directory.Exists("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        
        PrefabUtility.SaveAsPrefabAsset(spinner, prefabPath);
        DestroyImmediate(spinner);
        
        Debug.Log($"로딩 스피너 프리팹 생성 완료: {prefabPath}");
    }
#endif
}