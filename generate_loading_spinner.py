"""
저해상도 로딩 스피너 텍스처 생성 스크립트
64x64 PNG 파일로 최소 용량, 최대 성능
"""
from PIL import Image, ImageDraw
import math

def create_loading_spinner(size=64, segments=8, thickness=0.2):
    """
    원형 로딩 스피너 텍스처 생성

    Args:
        size: 이미지 크기 (픽셀)
        segments: 세그먼트 개수 (8개 = 깔끔한 원형)
        thickness: 두께 비율 (0.2 = 20%)
    """
    # 투명 배경 이미지 생성
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    center = size // 2
    outer_radius = center - 2
    inner_radius = int(outer_radius * (1 - thickness))

    # 세그먼트별로 그리기 (점점 투명해지는 효과)
    for i in range(segments):
        angle_start = (360 / segments) * i - 90  # -90도부터 시작 (12시 방향)
        angle_end = angle_start + (360 / segments) - 5  # 약간의 간격

        # 투명도 계산 (첫 번째가 가장 진하고, 마지막이 가장 투명)
        alpha = int(255 * (1 - i / segments))
        color = (255, 255, 255, alpha)  # 흰색, 점점 투명

        # 호(arc) 그리기
        draw.pieslice(
            [center - outer_radius, center - outer_radius,
             center + outer_radius, center + outer_radius],
            start=angle_start,
            end=angle_end,
            fill=color
        )

        # 내부 원 지우기 (도넛 모양)
        draw.ellipse(
            [center - inner_radius, center - inner_radius,
             center + inner_radius, center + inner_radius],
            fill=(0, 0, 0, 0)
        )

    return img

def create_simple_dots_spinner(size=64, dots=8, dot_radius=4):
    """
    점 스타일 로딩 스피너 (더 가벼움)
    """
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    center = size // 2
    circle_radius = center - dot_radius - 4

    for i in range(dots):
        angle = (360 / dots) * i - 90
        rad = math.radians(angle)

        # 점 위치 계산
        x = center + int(circle_radius * math.cos(rad))
        y = center + int(circle_radius * math.sin(rad))

        # 투명도 계산
        alpha = int(255 * (1 - i / dots))
        color = (255, 255, 255, alpha)

        # 점 그리기
        draw.ellipse(
            [x - dot_radius, y - dot_radius,
             x + dot_radius, y + dot_radius],
            fill=color
        )

    return img

if __name__ == '__main__':
    # 64x64 원형 스피너 (더 가벼움)
    spinner_64 = create_loading_spinner(size=64, segments=8, thickness=0.25)
    spinner_64.save('Assets/Resources/Sprites/loading_spinner_64.png', optimize=True)
    print('[OK] loading_spinner_64.png created (64x64)')

    # 128x128 원형 스피너 (조금 더 선명)
    spinner_128 = create_loading_spinner(size=128, segments=12, thickness=0.2)
    spinner_128.save('Assets/Resources/Sprites/loading_spinner_128.png', optimize=True)
    print('[OK] loading_spinner_128.png created (128x128)')

    # 64x64 점 스타일 (가장 가벼움)
    dots_64 = create_simple_dots_spinner(size=64, dots=8, dot_radius=3)
    dots_64.save('Assets/Resources/Sprites/loading_dots_64.png', optimize=True)
    print('[OK] loading_dots_64.png created (64x64 dots style)')

    print('')
    print('Saved to: Assets/Resources/Sprites/')
    print('Recommended: loading_spinner_64.png (lightest and fastest)')
