import os
import shutil
import random
from PIL import Image, ImageDraw, ImageFont

# ========================================================
# [설정] 선명한 글자 + 은은한 우주 배경
# ========================================================
TEXT = "잼민이"
HEIGHT_TARGET = 20   # 가독성 좋은 높이

# 색상 설정 (RGB)
COLOR_START = (220, 0, 90)   # 짙은 핑크
COLOR_END = (25, 25, 180)    # 진한 파란색

# 배경 별 색상 (눈에 띄지 않게 어둡고 은은하게)
STAR_COLORS = [
    (60, 60, 100),   # 어두운 남색
    (80, 40, 120),   # 어두운 보라
    (50, 50, 80)     # 흐린 회색빛
]

# 사용할 문자
BLOCK_MAIN = "█"   # 글자 본문
BLOCK_EDGE = "▓"   # 글자 테두리
# 포인트 문자 (글자 안 반짝이)
SPARKLE_CHARS = ["✨", "★"] 
# 배경 별 문자 (공백을 많이 섞어서 밀도 조절)
BG_CHARS = ["·", "˚", "✦", " ", " ", " ", " ", " ", " ", " ", " ", " "]
# ========================================================

def get_gradient_color(ratio, start_rgb, end_rgb, fade_factor=1.0):
    r = int(start_rgb[0] * (1 - ratio) + end_rgb[0] * ratio)
    g = int(start_rgb[1] * (1 - ratio) + end_rgb[1] * ratio)
    b = int(start_rgb[2] * (1 - ratio) + end_rgb[2] * ratio)
    return (int(r * fade_factor), int(g * fade_factor), int(b * fade_factor))

def print_space_logo():
    os.system('cls' if os.name == 'nt' else 'clear')

    # 1. 폰트 로드
    font_path = "C:/Windows/Fonts/malgunbd.ttf"
    if not os.path.exists(font_path): font_path = "C:/Windows/Fonts/malgun.ttf"
    try: font = ImageFont.truetype(font_path, size=120)
    except: return

    # 2. 캔버스 생성 (블러 없이 선명하게)
    dummy = ImageDraw.Draw(Image.new('RGB', (1, 1)))
    bbox = dummy.textbbox((0, 0), TEXT, font=font)
    w, h = bbox[2]-bbox[0], bbox[3]-bbox[1]
    
    pad_x, pad_y = int(w*0.1), int(h*0.2)
    image_hi = Image.new('RGB', (w + pad_x*2, h + pad_y*2), (0, 0, 0))
    draw = ImageDraw.Draw(image_hi)
    draw.text((pad_x, pad_y), TEXT, font=font, fill=(255, 255, 255))
    
    # 3. 리사이징
    aspect = image_hi.width / image_hi.height
    new_height = HEIGHT_TARGET
    new_width = int(aspect * new_height * 2.0)

    try:
        term_w = shutil.get_terminal_size().columns
        if new_width > term_w - 2:
            ratio = (term_w - 2) / new_width
            new_width = term_w - 2
            new_height = int(new_height * ratio)
    except: pass

    image_lo = image_hi.resize((new_width, new_height), Image.Resampling.BILINEAR)
    pixels = image_lo.convert('L').load()

    # 중앙 정렬을 위한 공백 계산
    try:
        term_w = shutil.get_terminal_size().columns
        # 왼쪽 여백 공간도 별이 찍힐 수 있게 공간 확보 대신 문자열로 처리
        padding_len = max(0, (term_w - new_width) // 2)
    except:
        padding_len = 0

    # 4. 출력
    final_output = []
    
    for y in range(new_height):
        line_buffer = ""
        
        # [왼쪽 여백] 그냥 공백 대신 별을 뿌려줌
        for _ in range(padding_len):
            if random.random() > 0.95: # 5% 확률로 별
                bg_char = random.choice(["·", "˚"])
                c = random.choice(STAR_COLORS)
                line_buffer += f"\033[38;2;{c[0]};{c[1]};{c[2]}m{bg_char}"
            else:
                line_buffer += " "

        # [본문 영역]
        for x in range(new_width):
            brightness = pixels[x, y]
            grad_ratio = x / new_width
            
            color = (0,0,0)
            char_to_print = " "

            # 글자 본문 (밝음)
            if brightness > 128:
                char_to_print = BLOCK_MAIN
                # 글자 안 포인트
                if random.random() > 0.98:
                    char_to_print = random.choice(SPARKLE_CHARS)
                color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 1.0)

            # 글자 테두리 (중간 밝기)
            elif brightness > 50:
                char_to_print = BLOCK_EDGE
                color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 0.6)

            # 배경 (어두움) -> 우주 효과
            else:
                if random.random() > 0.92: # 8% 확률로 배경 별
                    char_to_print = random.choice(BG_CHARS)
                    # 배경 별은 미리 정의한 어두운 색 중 하나 선택
                    c = random.choice(STAR_COLORS)
                    color = c
                else:
                    char_to_print = " "

            if char_to_print == " ":
                 line_buffer += " "
            else:
                 line_buffer += f"\033[38;2;{color[0]};{color[1]};{color[2]}m{char_to_print}"

        final_output.append(line_buffer + "\033[0m")

    print("\n".join(final_output))
    print("\033[0m")

if __name__ == "__main__":
    print_space_logo()
    print("김영훈 만세")
    os.system("pause")