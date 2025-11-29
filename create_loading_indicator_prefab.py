"""
Unity LoadingIndicator 프리팹 생성 스크립트
3D Sphere + Material + LoadingIndicator3D 스크립트가 설정된 프리팹 자동 생성
"""

import yaml
import os

# Unity GUID 생성 (간단한 버전)
def generate_guid():
    import random
    return ''.join([format(random.randint(0, 15), 'x') for _ in range(32)])

# LoadingIndicator GameObject 프리팹 생성
def create_loading_indicator_prefab():
    """
    LoadingIndicator.prefab 파일 생성
    - 3D Sphere
    - LoadingSpinnerMaterial 적용
    - LoadingIndicator3D 스크립트 추가
    - 비활성화 상태로 저장
    """

    prefab_content = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &6363636363636363636
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4444444444444444444}
  - component: {fileID: 3333333333333333333}
  - component: {fileID: 2323232323232323232}
  - component: {fileID: 1111111111111111111}
  - component: {fileID: 5555555555555555555}
  m_Layer: 0
  m_Name: LoadingIndicator
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 0
--- !u!4 &4444444444444444444
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 6363636363636363636}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 1.5, z: 2}
  m_LocalScale: {x: 0.12, y: 0.12, z: 0.12}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!33 &3333333333333333333
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 6363636363636363636}
  m_Mesh: {fileID: 10207, guid: 0000000000000000e000000000000000, type: 0}
--- !u!23 &2323232323232323232
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 6363636363636363636}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: """ + generate_guid() + """, type: 2}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {fileID: 0}
--- !u!135 &1111111111111111111
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 6363636363636363636}
  m_Material: {fileID: 0}
  m_IsTrigger: 0
  m_Enabled: 1
  serializedVersion: 2
  m_Radius: 0.5
  m_Center: {x: 0, y: 0, z: 0}
--- !u!114 &5555555555555555555
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 6363636363636363636}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: """ + generate_guid() + """, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  floatSpeed: 0.8
  floatHeight: 0.2
  rotationSpeed: 60
  rotationAxis: {x: 0.3, y: 1, z: 0.2}
  enablePulse: 1
  pulseSpeed: 2
  minScale: 0.95
  maxScale: 1.05
"""

    prefab_path = 'Assets/Prefabs/LoadingIndicator.prefab'
    os.makedirs(os.path.dirname(prefab_path), exist_ok=True)

    with open(prefab_path, 'w', encoding='utf-8') as f:
        f.write(prefab_content)

    print(f'[OK] {prefab_path} created')
    return prefab_path

# LoadingSpinnerMaterial 생성
def create_loading_material():
    """
    LoadingSpinnerMaterial.mat 파일 생성
    - Unlit/Transparent 셰이더
    - loading_spinner_64.png 텍스처 연결
    """

    texture_guid = generate_guid()

    material_content = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 6
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: LoadingSpinnerMaterial
  m_Shader: {fileID: 10752, guid: 0000000000000000f000000000000000, type: 0}
  m_ShaderKeywords:
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: 3000
  stringTagMap: {}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {fileID: 2800000, guid: """ + texture_guid + """, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Floats: []
    m_Colors:
    - _Color: {r: 1, g: 1, b: 1, a: 1}
"""

    material_path = 'Assets/Materials/LoadingSpinnerMaterial.mat'
    os.makedirs(os.path.dirname(material_path), exist_ok=True)

    with open(material_path, 'w', encoding='utf-8') as f:
        f.write(material_content)

    print(f'[OK] {material_path} created')
    return material_path, texture_guid

if __name__ == '__main__':
    print('Creating LoadingIndicator prefab and material...')

    # Material 생성
    material_path, texture_guid = create_loading_material()

    # Prefab 생성
    prefab_path = create_loading_indicator_prefab()

    print('')
    print('=== Setup Complete ===')
    print(f'Prefab: {prefab_path}')
    print(f'Material: {material_path}')
    print('')
    print('Next steps in Unity:')
    print('1. Refresh Unity (Ctrl+R or Assets > Refresh)')
    print('2. Select DataManager in Hierarchy')
    print('3. Drag "LoadingIndicator" prefab to "Loading Indicator" field')
    print('4. Done!')
