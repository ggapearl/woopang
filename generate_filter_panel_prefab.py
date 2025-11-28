"""
FilterButtonPanel.prefab 생성 스크립트
- 6개 토글: 애견동반, 공공데이터, 지하철, 버스, 주류판매, 우팡데이터
- 2개 버튼: 전체선택, 전체해제
- 각 토글 크기: 60x60 (2배 증가)
- 위치: 왼쪽 상단 (x:10, y:-10)
- 간격: 5px (VerticalLayoutGroup)
"""

# 토글 정의 (이름, 라벨 텍스트, fileID 시작번호)
toggles = [
    ("PetFriendlyToggle", "애견동반", 10),
    ("PublicDataToggle", "공공데이터", 20),
    ("SubwayToggle", "지하철", 40),
    ("BusToggle", "버스", 50),
    ("AlcoholToggle", "주류판매", 60),
    ("WoopangDataToggle", "우팡데이터", 70),
]

# 버튼 정의
buttons = [
    ("SelectAllButton", "전체 선택", 80),
    ("DeselectAllButton", "전체 해제", 90),
]

def generate_toggle(name, label, base_id):
    """토글 GameObject YAML 생성"""
    toggle_id = base_id
    bg_id = base_id + 3
    checkmark_id = base_id + 7
    label_id = base_id + 6

    # RectTransform, CanvasRenderer, Toggle component fileID
    rect_id = f"8100000{toggle_id:03d}"
    canvas_id = f"8100000{toggle_id+2:03d}"
    toggle_comp_id = f"8100000{toggle_id+1:03d}"
    bg_rect_id = f"8100000{bg_id:03d}"
    bg_canvas_id = f"8100000{bg_id+5:03d}"
    bg_image_id = f"8100000{bg_id+1:03d}"
    check_rect_id = f"8100000{checkmark_id:03d}"
    check_canvas_id = f"8100000{checkmark_id+2:03d}"
    check_image_id = f"8100000{checkmark_id-2:03d}"
    label_rect_id = f"8100000{label_id:03d}"
    label_canvas_id = f"8100000{label_id+25:03d}"
    label_text_id = f"8100000{label_id+26:03d}"

    return f'''--- !u!1 &8100000{toggle_id+6:03d}
GameObject:
  m_ObjectHideFlags: 0
  m_Name: {name}
  m_Layer: 5
  m_Component:
  - component: {{fileID: {rect_id}}}
  - component: {{fileID: {canvas_id}}}
  - component: {{fileID: {toggle_comp_id}}}
--- !u!224 &{rect_id}
RectTransform:
  m_GameObject: {{fileID: 8100000{toggle_id+6:03d}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children:
  - {{fileID: {bg_rect_id}}}
  - {{fileID: {label_rect_id}}}
  m_Father: {{fileID: 8100000002}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 60}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{canvas_id}
CanvasRenderer:
  m_GameObject: {{fileID: 8100000{toggle_id+6:03d}}}
--- !u!114 &{toggle_comp_id}
MonoBehaviour:
  m_GameObject: {{fileID: 8100000{toggle_id+6:03d}}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: 9085046f02f69544eb97fd06b6048fe2, type: 3}}
  m_Name:
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {bg_image_id}}}
  toggleTransition: 1
  graphic: {{fileID: {check_image_id}}}
  m_IsOn: 1
--- !u!1 &8100000{bg_id+37:03d}
GameObject:
  m_Name: Background
  m_Layer: 5
  m_Component:
  - component: {{fileID: {bg_rect_id}}}
  - component: {{fileID: {bg_canvas_id}}}
  - component: {{fileID: {bg_image_id}}}
--- !u!224 &{bg_rect_id}
RectTransform:
  m_GameObject: {{fileID: 8100000{bg_id+37:03d}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children:
  - {{fileID: {check_rect_id}}}
  m_Father: {{fileID: {rect_id}}}
  m_AnchorMin: {{x: 0, y: 0.5}}
  m_AnchorMax: {{x: 0, y: 0.5}}
  m_AnchoredPosition: {{x: 40, y: 0}}
  m_SizeDelta: {{x: 60, y: 60}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{bg_canvas_id}
CanvasRenderer:
  m_GameObject: {{fileID: 8100000{bg_id+37:03d}}}
--- !u!114 &{bg_image_id}
MonoBehaviour:
  m_GameObject: {{fileID: 8100000{bg_id+37:03d}}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name:
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_Sprite: {{fileID: 10905, guid: 0000000000000000f000000000000000, type: 0}}
--- !u!1 &8100000{checkmark_id+43:03d}
GameObject:
  m_Name: Checkmark
  m_Layer: 5
  m_Component:
  - component: {{fileID: {check_rect_id}}}
  - component: {{fileID: {check_canvas_id}}}
  - component: {{fileID: {check_image_id}}}
--- !u!224 &{check_rect_id}
RectTransform:
  m_GameObject: {{fileID: 8100000{checkmark_id+43:03d}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Father: {{fileID: {bg_rect_id}}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 50, y: 50}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{check_canvas_id}
CanvasRenderer:
  m_GameObject: {{fileID: 8100000{checkmark_id+43:03d}}}
--- !u!114 &{check_image_id}
MonoBehaviour:
  m_GameObject: {{fileID: 8100000{checkmark_id+43:03d}}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name:
  m_Color: {{r: 0.196, g: 0.196, b: 0.196, a: 1}}
  m_Sprite: {{fileID: 10901, guid: 0000000000000000f000000000000000, type: 0}}
--- !u!1 &8100000{label_id+54:03d}
GameObject:
  m_Name: Label
  m_Layer: 5
  m_Component:
  - component: {{fileID: {label_rect_id}}}
  - component: {{fileID: {label_canvas_id}}}
  - component: {{fileID: {label_text_id}}}
--- !u!224 &{label_rect_id}
RectTransform:
  m_GameObject: {{fileID: 8100000{label_id+54:03d}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Father: {{fileID: {rect_id}}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 35, y: 0}}
  m_SizeDelta: {{x: -110, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{label_canvas_id}
CanvasRenderer:
  m_GameObject: {{fileID: 8100000{label_id+54:03d}}}
--- !u!114 &{label_text_id}
MonoBehaviour:
  m_GameObject: {{fileID: 8100000{label_id+54:03d}}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: 5f7201a12d95ffc409449d95f23cf332, type: 3}}
  m_Name:
  m_FontData:
    m_Font: {{fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}}
    m_FontSize: 18
    m_Alignment: 3
  m_Text: "{label}"
'''

def generate_button(name, label, base_id):
    """버튼 GameObject YAML 생성"""
    button_id = base_id
    text_id = base_id + 6

    rect_id = f"8100000{button_id:03d}"
    canvas_id = f"8100000{button_id+2:03d}"
    image_id = f"8100000{button_id+4:03d}"
    button_comp_id = f"8100000{button_id+1:03d}"
    text_rect_id = f"8100000{text_id:03d}"
    text_canvas_id = f"8100000{text_id+25:03d}"
    text_comp_id = f"8100000{text_id+26:03d}"

    return f'''--- !u!1 &8100000{button_id+6:03d}
GameObject:
  m_Name: {name}
  m_Layer: 5
  m_Component:
  - component: {{fileID: {rect_id}}}
  - component: {{fileID: {canvas_id}}}
  - component: {{fileID: {image_id}}}
  - component: {{fileID: {button_comp_id}}}
--- !u!224 &{rect_id}
RectTransform:
  m_GameObject: {{fileID: 8100000{button_id+6:03d}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children:
  - {{fileID: {text_rect_id}}}
  m_Father: {{fileID: 8100000002}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 50}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{canvas_id}
CanvasRenderer:
  m_GameObject: {{fileID: 8100000{button_id+6:03d}}}
--- !u!114 &{image_id}
MonoBehaviour:
  m_GameObject: {{fileID: 8100000{button_id+6:03d}}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name:
  m_Color: {{r: 0.2, g: 0.6, b: 1, a: 0.8}}
  m_Sprite: {{fileID: 10905, guid: 0000000000000000f000000000000000, type: 0}}
--- !u!114 &{button_comp_id}
MonoBehaviour:
  m_GameObject: {{fileID: 8100000{button_id+6:03d}}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: 4e29b1a8efbd4b44bb3f3716e73f07ff, type: 3}}
  m_Name:
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {image_id}}}
--- !u!1 &8100000{text_id+54:03d}
GameObject:
  m_Name: Text
  m_Layer: 5
  m_Component:
  - component: {{fileID: {text_rect_id}}}
  - component: {{fileID: {text_canvas_id}}}
  - component: {{fileID: {text_comp_id}}}
--- !u!224 &{text_rect_id}
RectTransform:
  m_GameObject: {{fileID: 8100000{text_id+54:03d}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Father: {{fileID: {rect_id}}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{text_canvas_id}
CanvasRenderer:
  m_GameObject: {{fileID: 8100000{text_id+54:03d}}}
--- !u!114 &{text_comp_id}
MonoBehaviour:
  m_GameObject: {{fileID: 8100000{text_id+54:03d}}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: 5f7201a12d95ffc409449d95f23cf332, type: 3}}
  m_Name:
  m_FontData:
    m_Font: {{fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}}
    m_FontSize: 16
    m_Alignment: 4
  m_Text: "{label}"
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
'''

# Generate complete prefab
output = '''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &8100000001
GameObject:
  m_Name: FilterButtonPanel
  m_Layer: 5
  m_Component:
  - component: {fileID: 8100000002}
  - component: {fileID: 8100000003}
  - component: {fileID: 8100000004}
  - component: {fileID: 8100000005}
  - component: {fileID: 8100000099}
--- !u!224 &8100000002
RectTransform:
  m_GameObject: {fileID: 8100000001}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children:
'''

# Add children references
for name, label, base_id in toggles:
    output += f"  - {{fileID: 8100000{base_id:03d}}}\n"
for name, label, base_id in buttons:
    output += f"  - {{fileID: 8100000{base_id:03d}}}\n"

output += '''  m_Father: {fileID: 0}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 10, y: -10}
  m_SizeDelta: {x: 250, y: 600}
  m_Pivot: {x: 0, y: 1}
--- !u!222 &8100000003
CanvasRenderer:
  m_GameObject: {fileID: 8100000001}
--- !u!114 &8100000004
MonoBehaviour:
  m_GameObject: {fileID: 8100000001}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name:
  m_Color: {r: 0, g: 0, b: 0, a: 0.7}
--- !u!114 &8100000005
MonoBehaviour:
  m_GameObject: {fileID: 8100000001}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: 6a4ffff20160efb46b2d288164ef85c5, type: 3}
  m_Name:
  petFriendlyToggle: {fileID: 8100000011}
  publicDataToggle: {fileID: 8100000021}
  subwayToggle: {fileID: 8100000041}
  busToggle: {fileID: 8100000051}
  alcoholToggle: {fileID: 8100000061}
  woopangDataToggle: {fileID: 8100000071}
  selectAllButton: {fileID: 8100000081}
  deselectAllButton: {fileID: 8100000091}
  placeListManager: {fileID: 0}
  dataManager: {fileID: 0}
  tourAPIManager: {fileID: 0}
--- !u!114 &8100000099
MonoBehaviour:
  m_GameObject: {fileID: 8100000001}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: 59f8146938fff824cb5fd77236b75775, type: 3}
  m_Name:
  m_Padding:
    m_Left: 10
    m_Right: 10
    m_Top: 10
    m_Bottom: 10
  m_ChildAlignment: 0
  m_Spacing: 5
  m_ChildForceExpandWidth: 1
  m_ChildForceExpandHeight: 0
'''

# Add all toggles
for name, label, base_id in toggles:
    output += generate_toggle(name, label, base_id)

# Add all buttons
for name, label, base_id in buttons:
    output += generate_button(name, label, base_id)

print(f"Generated prefab with {len(toggles)} toggles and {len(buttons)} buttons")
print(f"Output length: {len(output)} characters")

# Save to file
with open(r"c:\woopang\Assets\Prefabs\FilterButtonPanel.prefab", "w", encoding="utf-8") as f:
    f.write(output)

print("✓ FilterButtonPanel.prefab has been generated successfully!")
