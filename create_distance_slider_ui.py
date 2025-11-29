"""
Unity DistanceSlider UI 프리팹 생성 스크립트
ListPanel 안에 들어갈 거리 조정 슬라이더 UI 레이아웃 생성
"""

import os

def generate_guid():
    """간단한 Unity GUID 생성"""
    import random
    return ''.join([format(random.randint(0, 15), 'x') for _ in range(32)])

def create_distance_slider_prefab():
    """
    DistanceSliderPanel.prefab 생성
    - Canvas 하위의 Panel
    - Slider + Text (거리 값 표시)
    - 가로 레이아웃
    """

    # GUIDs
    panel_guid = generate_guid()
    slider_guid = generate_guid()
    bg_guid = generate_guid()
    fill_area_guid = generate_guid()
    fill_guid = generate_guid()
    handle_area_guid = generate_guid()
    handle_guid = generate_guid()
    text_guid = generate_guid()

    prefab_content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{panel_guid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {panel_guid}01}}
  - component: {{fileID: {panel_guid}02}}
  - component: {{fileID: {panel_guid}03}}
  m_Layer: 5
  m_Name: DistanceSliderPanel
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{panel_guid}01
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {panel_guid}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {slider_guid}01}}
  - {{fileID: {text_guid}01}}
  m_Father: {{fileID: 0}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -30}}
  m_SizeDelta: {{x: -20, y: 40}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{panel_guid}02
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {panel_guid}}}
  m_CullTransparentMesh: 1
--- !u!114 &{panel_guid}03
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {panel_guid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.1, g: 0.1, b: 0.1, a: 0.8}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{slider_guid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {slider_guid}01}}
  - component: {{fileID: {slider_guid}02}}
  m_Layer: 5
  m_Name: DistanceSlider
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{slider_guid}01
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {slider_guid}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {bg_guid}01}}
  - {{fileID: {fill_area_guid}01}}
  - {{fileID: {handle_area_guid}01}}
  m_Father: {{fileID: {panel_guid}01}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0.5}}
  m_AnchorMax: {{x: 0.75, y: 0.5}}
  m_AnchoredPosition: {{x: 10, y: 0}}
  m_SizeDelta: {{x: -20, y: 20}}
  m_Pivot: {{x: 0, y: 0.5}}
--- !u!114 &{slider_guid}02
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {slider_guid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 67db9e8f0e2ae9c40bc1e2b64352a6b4, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Navigation:
    m_Mode: 3
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_PressedColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}}
    m_SelectedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_DisabledColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {handle_guid}02}}
  m_FillRect: {{fileID: {fill_guid}01}}
  m_HandleRect: {{fileID: {handle_guid}01}}
  m_Direction: 0
  m_MinValue: 50
  m_MaxValue: 2000
  m_WholeNumbers: 1
  m_Value: 1000
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{bg_guid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {bg_guid}01}}
  - component: {{fileID: {bg_guid}02}}
  - component: {{fileID: {bg_guid}03}}
  m_Layer: 5
  m_Name: Background
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{bg_guid}01
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {bg_guid}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {slider_guid}01}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0.25}}
  m_AnchorMax: {{x: 1, y: 0.75}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{bg_guid}02
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {bg_guid}}}
  m_CullTransparentMesh: 1
--- !u!114 &{bg_guid}03
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {bg_guid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.3, g: 0.3, b: 0.3, a: 0.8}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{fill_area_guid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {fill_area_guid}01}}
  m_Layer: 5
  m_Name: Fill Area
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{fill_area_guid}01
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fill_area_guid}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {fill_guid}01}}
  m_Father: {{fileID: {slider_guid}01}}
  m_RootOrder: 1
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0.25}}
  m_AnchorMax: {{x: 1, y: 0.75}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: -20, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!1 &{fill_guid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {fill_guid}01}}
  - component: {{fileID: {fill_guid}02}}
  - component: {{fileID: {fill_guid}03}}
  m_Layer: 5
  m_Name: Fill
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{fill_guid}01
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fill_guid}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {fill_area_guid}01}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 10, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{fill_guid}02
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fill_guid}}}
  m_CullTransparentMesh: 1
--- !u!114 &{fill_guid}03
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fill_guid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.2, g: 0.6, b: 1, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{handle_area_guid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {handle_area_guid}01}}
  m_Layer: 5
  m_Name: Handle Slide Area
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{handle_area_guid}01
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {handle_area_guid}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {handle_guid}01}}
  m_Father: {{fileID: {slider_guid}01}}
  m_RootOrder: 2
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: -20, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!1 &{handle_guid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {handle_guid}01}}
  - component: {{fileID: {handle_guid}02}}
  - component: {{fileID: {handle_guid}03}}
  m_Layer: 5
  m_Name: Handle
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{handle_guid}01
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {handle_guid}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {handle_area_guid}01}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 20, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{handle_guid}02
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {handle_guid}}}
  m_CullTransparentMesh: 1
--- !u!114 &{handle_guid}03
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {handle_guid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{text_guid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {text_guid}01}}
  - component: {{fileID: {text_guid}02}}
  - component: {{fileID: {text_guid}03}}
  m_Layer: 5
  m_Name: DistanceValueText
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{text_guid}01
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {text_guid}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {panel_guid}01}}
  m_RootOrder: 1
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.75, y: 0.5}}
  m_AnchorMax: {{x: 1, y: 0.5}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: -10, y: 30}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{text_guid}02
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {text_guid}}}
  m_CullTransparentMesh: 1
--- !u!114 &{text_guid}03
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {text_guid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 5f7201a12d95ffc409449d95f23cf332, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}}
    m_FontSize: 18
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 4
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: 1.0km
"""

    prefab_path = 'Assets/Prefabs/DistanceSliderPanel.prefab'
    os.makedirs(os.path.dirname(prefab_path), exist_ok=True)

    with open(prefab_path, 'w', encoding='utf-8') as f:
        f.write(prefab_content)

    print(f'[OK] {prefab_path} created')
    return prefab_path

def create_meta_file():
    """DistanceSliderPanel.prefab.meta 생성"""
    meta_content = f"""fileFormatVersion: 2
guid: {generate_guid()}
PrefabImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

    meta_path = 'Assets/Prefabs/DistanceSliderPanel.prefab.meta'
    with open(meta_path, 'w', encoding='utf-8') as f:
        f.write(meta_content)

    print(f'[OK] {meta_path} created')

if __name__ == '__main__':
    print('Creating DistanceSliderPanel prefab...')
    print('')

    # Prefab 생성
    prefab_path = create_distance_slider_prefab()

    # Meta 파일 생성
    create_meta_file()

    print('')
    print('=== Setup Complete ===')
    print(f'Prefab: {prefab_path}')
    print('')
    print('Next steps in Unity:')
    print('1. Refresh Unity (Ctrl+R or Assets > Refresh)')
    print('2. Find ListPanel in Hierarchy')
    print('3. Drag "DistanceSliderPanel" prefab as child of ListPanel')
    print('4. Select PlaceListManager in Inspector')
    print('5. Connect fields:')
    print('   - Distance Slider -> DistanceSliderPanel/DistanceSlider')
    print('   - Distance Value Text -> DistanceSliderPanel/DistanceValueText')
    print('6. Done!')
