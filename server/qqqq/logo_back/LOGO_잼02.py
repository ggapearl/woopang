import os
import shutil
import random
from PIL import Image, ImageDraw, ImageFont

# ========================================================
# [설정] 가독성 최적화 버전 (선명함 + 깔끔함)
# ========================================================
TEXT = "잼민이"
HEIGHT_TARGET = 20   # 가독성을 위해 높이를 적절히 확보 (너무 작으면 뭉개짐)

# 색상 설정 (RGB)
COLOR_START = (220, 0, 90)   # 짙은 핑크
COLOR_END = (25, 25, 180)    # 진한 파란색

# 사용할 문자 설정 (가독성을 해치는 복잡한 문자 제거)
BLOCK_MAIN = "█"   # 글자 본문 (꽉 찬 네모)
BLOCK_EDGE = "▓"   # 글자 테두리 (약간 흐린 네모)
# 포인트 문자: 공간을 많이 차지하는 ●, ◆ 제거하고 심플한 것만 남김
SPARKLE_CHARS = ["✨", "★"] 
# ========================================================

def get_gradient_color(ratio, start_rgb, end_rgb, fade_factor=1.0):
    r = int(start_rgb[0] * (1 - ratio) + end_rgb[0] * ratio)
    g = int(start_rgb[1] * (1 - ratio) + end_rgb[1] * ratio)
    b = int(start_rgb[2] * (1 - ratio) + end_rgb[2] * ratio)
    return (int(r * fade_factor), int(g * fade_factor), int(b * fade_factor))

def print_clean_logo():
    os.system('cls' if os.name == 'nt' else 'clear')

    # 1. 폰트 로드
    font_path = "C:/Windows/Fonts/malgunbd.ttf"
    if not os.path.exists(font_path): font_path = "C:/Windows/Fonts/malgun.ttf"
    try: font = ImageFont.truetype(font_path, size=120)
    except: return

    # 2. 캔버스 생성 (블러 효과 제거 -> 선명도 상승)
    dummy = ImageDraw.Draw(Image.new('RGB', (1, 1)))
    bbox = dummy.textbbox((0, 0), TEXT, font=font)
    w, h = bbox[2]-bbox[0], bbox[3]-bbox[1]
    
    pad_x, pad_y = int(w*0.1), int(h*0.2)
    
    # 고해상도 이미지 생성
    image_hi = Image.new('RGB', (w + pad_x*2, h + pad_y*2), (0, 0, 0))
    draw = ImageDraw.Draw(image_hi)
    draw.text((pad_x, pad_y), TEXT, font=font, fill=(255, 255, 255))
    
    # [수정] ImageFilter.GaussianBlur 삭제함 (글자 뭉개짐 방지)

    # 3. 리사이징
    aspect = image_hi.width / image_hi.height
    new_height = HEIGHT_TARGET
    new_width = int(aspect * new_height * 2.0)

    try:
        term_w = shutil.get_terminal_size().columns
        # 줄바꿈 방지 안전 여백
        if new_width > term_w - 2:
            ratio = (term_w - 2) / new_width
            new_width = term_w - 2
            new_height = int(new_height * ratio)
    except: pass

    # 깔끔한 축소
    image_lo = image_hi.resize((new_width, new_height), Image.Resampling.BILINEAR)
    pixels = image_lo.convert('L').load()

    # 중앙 정렬을 위한 공백
    try:
        term_w = shutil.get_terminal_size().columns
        padding_left = " " * max(0, (term_w - new_width) // 2)
    except:
        padding_left = ""

    # 4. 출력
    final_output = []
    
    for y in range(new_height):
        line_buffer = padding_left
        for x in range(new_width):
            brightness = pixels[x, y]
            grad_ratio = x / new_width
            
            color = (0,0,0)
            char_to_print = " "

            # [수정] 밝기 기준을 단순화하고 문자를 통일하여 가독성 확보
            if brightness > 128:
                # 글자 본문
                char_to_print = BLOCK_MAIN
                
                # 반짝이 효과: 확률을 2%로 낮추고, 가독성을 해치지 않는 선에서만 적용
                if random.random() > 0.98:
                    char_to_print = random.choice(SPARKLE_CHARS)
                
                color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 1.0)

            elif brightness > 50:
                # 글자 테두리 (외곽선을 잡아줌)
                char_to_print = BLOCK_EDGE
                color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 0.6)

            else:
                # 배경: 깨끗하게 비움 (별 제거)
                char_to_print = " "

            if char_to_print == " ":
                 line_buffer += " "
            else:
                 line_buffer += f"\033[38;2;{color[0]};{color[1]};{color[2]}m{char_to_print}"

        final_output.append(line_buffer + "\033[0m")

    print("\n".join(final_output))
    print("\033[0m")

if __name__ == "__main__":
    print_clean_logo()
    os.system("pause")