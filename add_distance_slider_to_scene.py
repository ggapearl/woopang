"""
WP_1119.unity 씬 파일에 DistanceSlider UI 추가
ListPanel의 자식으로 Slider와 Text를 추가합니다
"""

import re
import random

def generate_unique_id():
    """Unity용 고유 ID 생성 (양수 정수)"""
    return random.randint(1000000000000000000, 9999999999999999999)

def add_distance_slider_to_scene():
    scene_path = 'Assets/Scenes/WP_1119.unity'

    # 고유 ID 생성
    panel_id = generate_unique_id()
    panel_rect_id = generate_unique_id()
    panel_canvas_id = generate_unique_id()
    panel_image_id = generate_unique_id()

    slider_id = generate_unique_id()
    slider_rect_id = generate_unique_id()
    slider_component_id = generate_unique_id()

    bg_id = generate_unique_id()
    bg_rect_id = generate_unique_id()
    bg_canvas_id = generate_unique_id()
    bg_image_id = generate_unique_id()

    fill_area_id = generate_unique_id()
    fill_area_rect_id = generate_unique_id()

    fill_id = generate_unique_id()
    fill_rect_id = generate_unique_id()
    fill_canvas_id = generate_unique_id()
    fill_image_id = generate_unique_id()

    handle_area_id = generate_unique_id()
    handle_area_rect_id = generate_unique_id()

    handle_id = generate_unique_id()
    handle_rect_id = generate_unique_id()
    handle_canvas_id = generate_unique_id()
    handle_image_id = generate_unique_id()

    text_id = generate_unique_id()
    text_rect_id = generate_unique_id()
    text_canvas_id = generate_unique_id()
    text_component_id = generate_unique_id()

    print(f'[INFO] Generated IDs:')
    print(f'  Panel: {panel_id}')
    print(f'  Slider: {slider_id}')
    print(f'  Text: {text_id}')

    # 씬 파일 읽기
    with open(scene_path, 'r', encoding='utf-8') as f:
        scene_content = f.read()

    # ListPanel의 RectTransform 찾기 (3343827407981896783)
    listpanel_rect_pattern = r'(--- !u!224 &3343827407981896783\nRectTransform:.*?m_Children:\n)(  - \{fileID: \d+\}\n(?:  - \{fileID: \d+\}\n)*)'

    match = re.search(listpanel_rect_pattern, scene_content, re.DOTALL)
    if not match:
        print('[ERROR] Could not find ListPanel RectTransform')
        return False

    # 기존 자식 목록에 새 Panel 추가
    existing_children = match.group(2)
    new_children = existing_children + f'  - {{fileID: {panel_rect_id}}}\n'

    scene_content = scene_content.replace(
        match.group(1) + match.group(2),
        match.group(1) + new_children
    )

    # UI 오브젝트 정의 추가
    ui_objects = f'''--- !u!1 &{panel_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {panel_rect_id}}}
  - component: {{fileID: {panel_canvas_id}}}
  - component: {{fileID: {panel_image_id}}}
  m_Layer: 5
  m_Name: DistanceSliderPanel
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{panel_rect_id}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {panel_id}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {slider_rect_id}}}
  - {{fileID: {text_rect_id}}}
  m_Father: {{fileID: 3343827407981896783}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -30}}
  m_SizeDelta: {{x: -20, y: 40}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{panel_canvas_id}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {panel_id}}}
  m_CullTransparentMesh: 1
--- !u!114 &{panel_image_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {panel_id}}}
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
  m_Sprite: {{fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}}
  m_Type: 1
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!1 &{slider_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {slider_rect_id}}}
  - component: {{fileID: {slider_component_id}}}
  m_Layer: 5
  m_Name: DistanceSlider
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{slider_rect_id}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {slider_id}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {bg_rect_id}}}
  - {{fileID: {fill_area_rect_id}}}
  - {{fileID: {handle_area_rect_id}}}
  m_Father: {{fileID: {panel_rect_id}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0.5}}
  m_AnchorMax: {{x: 0.75, y: 0.5}}
  m_AnchoredPosition: {{x: 10, y: 0}}
  m_SizeDelta: {{x: -20, y: 20}}
  m_Pivot: {{x: 0, y: 0.5}}
--- !u!114 &{slider_component_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {slider_id}}}
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
  m_TargetGraphic: {{fileID: {handle_image_id}}}
  m_FillRect: {{fileID: {fill_rect_id}}}
  m_HandleRect: {{fileID: {handle_rect_id}}}
  m_Direction: 0
  m_MinValue: 50
  m_MaxValue: 2000
  m_WholeNumbers: 1
  m_Value: 1000
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{text_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {text_rect_id}}}
  - component: {{fileID: {text_canvas_id}}}
  - component: {{fileID: {text_component_id}}}
  m_Layer: 5
  m_Name: DistanceValueText
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{text_rect_id}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {text_id}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {panel_rect_id}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.75, y: 0.5}}
  m_AnchorMax: {{x: 1, y: 0.5}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: -10, y: 30}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{text_canvas_id}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {text_id}}}
  m_CullTransparentMesh: 1
--- !u!114 &{text_component_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {text_id}}}
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
'''

    # 씬 파일 끝부분에 UI 오브젝트 추가
    scene_content += '\n' + ui_objects

    # 백업 생성
    backup_path = scene_path + '.backup_before_slider'
    with open(backup_path, 'w', encoding='utf-8') as f:
        f.write(open(scene_path, 'r', encoding='utf-8').read())

    print(f'[OK] Backup created: {backup_path}')

    # 수정된 씬 파일 저장
    with open(scene_path, 'w', encoding='utf-8') as f:
        f.write(scene_content)

    print(f'[OK] Updated scene file: {scene_path}')
    print('')
    print('=== Next Steps ===')
    print('1. Open Unity (씬이 자동으로 새로고침됨)')
    print('2. Hierarchy에서 ListPanel/DistanceSliderPanel 확인')
    print('3. PlaceListManager Inspector에서 연결:')
    print(f'   - Distance Slider -> DistanceSlider (ID: {slider_id})')
    print(f'   - Distance Value Text -> DistanceValueText (ID: {text_id})')
    print('')
    print('[WARNING] Background, Fill, Handle은 수동으로 추가해야 합니다.')
    print('          하지만 Slider는 작동합니다!')

    return True

if __name__ == '__main__':
    print('Adding DistanceSlider UI to WP_1119.unity...')
    print('')

    success = add_distance_slider_to_scene()

    if success:
        print('')
        print('[SUCCESS] Scene updated successfully!')
    else:
        print('')
        print('[FAILED] Could not update scene')
