#!/usr/bin/env python3
"""
FullProfilePanel.prefab 의 Content 자식들을 2배로 확대하는 스크립트
- m_SizeDelta (x, y) 값 2배
- m_AnchoredPosition (x, y) 값 2배
- m_FontSize 값 2배
- m_Spacing 값 2배
"""

import re

PREFAB_PATH = "Assets/Prefabs/FullProfilePanel.prefab"

def scale_value(match, scale=2.0):
    """숫자 값을 scale 배로 변환"""
    val = float(match.group(1))
    new_val = val * scale
    # 정수면 정수로, 소수면 소수로
    if new_val == int(new_val):
        return str(int(new_val))
    return str(new_val)

def main():
    with open(PREFAB_PATH, 'r', encoding='utf-8') as f:
        content = f.read()

    # 원본 백업
    with open(PREFAB_PATH + '.backup_before_2x', 'w', encoding='utf-8') as f:
        f.write(content)

    # 변경 카운터
    changes = {
        'sizeDelta': 0,
        'anchoredPosition': 0,
        'fontSize': 0,
        'spacing': 0
    }

    lines = content.split('\n')
    new_lines = []

    for line in lines:
        new_line = line

        # m_SizeDelta: {x: 200, y: 200} -> {x: 400, y: 400}
        if 'm_SizeDelta:' in line and '{x:' in line:
            # x 값
            def replace_x(m):
                changes['sizeDelta'] += 1
                return f"x: {scale_value(m)}"
            new_line = re.sub(r'x:\s*(-?[\d.]+)', replace_x, new_line)
            # y 값
            def replace_y(m):
                return f"y: {scale_value(m)}"
            new_line = re.sub(r'y:\s*(-?[\d.]+)', replace_y, new_line)

        # m_AnchoredPosition: {x: 0, y: 280} -> {x: 0, y: 560}
        elif 'm_AnchoredPosition:' in line and '{x:' in line:
            def replace_x(m):
                changes['anchoredPosition'] += 1
                return f"x: {scale_value(m)}"
            new_line = re.sub(r'x:\s*(-?[\d.]+)', replace_x, new_line)
            def replace_y(m):
                return f"y: {scale_value(m)}"
            new_line = re.sub(r'y:\s*(-?[\d.]+)', replace_y, new_line)

        # m_FontSize: 24 -> 48
        elif 'm_FontSize:' in line:
            def replace_fontsize(m):
                changes['fontSize'] += 1
                return f"m_FontSize: {scale_value(m)}"
            new_line = re.sub(r'm_FontSize:\s*(\d+)', replace_fontsize, new_line)

        # m_Spacing: 40 -> 80
        elif 'm_Spacing:' in line and 'm_LineSpacing' not in line:
            def replace_spacing(m):
                changes['spacing'] += 1
                return f"m_Spacing: {scale_value(m)}"
            new_line = re.sub(r'm_Spacing:\s*(-?[\d.]+)', replace_spacing, new_line)

        new_lines.append(new_line)

    # 저장
    with open(PREFAB_PATH, 'w', encoding='utf-8') as f:
        f.write('\n'.join(new_lines))

    print("=" * 50)
    print("FullProfilePanel.prefab 2x 스케일 완료!")
    print("=" * 50)
    print(f"SizeDelta 변경: {changes['sizeDelta']}개")
    print(f"AnchoredPosition 변경: {changes['anchoredPosition']}개")
    print(f"FontSize 변경: {changes['fontSize']}개")
    print(f"Spacing 변경: {changes['spacing']}개")
    print()
    print("백업 파일: " + PREFAB_PATH + '.backup_before_2x')
    print()
    print("Unity에서 프리팹을 다시 열어서 확인하세요!")

if __name__ == '__main__':
    main()
