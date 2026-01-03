import os
import shutil
import random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

# ========================================================
# [설정] 디자인 및 색상 커스터마이징
# ========================================================
TEXT = "잼민이"
HEIGHT_TARGET = 22   # 높이

# 색상 설정 (RGB)
# 좌측: 무게감 있는 짙은 핑크
COLOR_START = (220, 0, 90)
# 우측: 훨씬 더 진하고 깊은 파란색
COLOR_END = (25, 25, 180)
# 배경 별빛 색상 (아주 어두운 보라색)
BG_STAR_COLOR = (80, 40, 130)

# 텍스처 문자 세트
SHADE_CHARS = ["░", "▒", "▓", "█"]
# 포인트 특수문자
SPARKLE_CHARS = ["✨", "★", "✦", "◆", "●"]
# 배경 패턴 문자
BG_CHARS = ["·", "˚", " ", " ", " "] 
# ========================================================

def get_gradient_color(ratio, start_rgb, end_rgb, fade_factor=1.0):
    """그라데이션 색상을 계산합니다."""
    r = int(start_rgb[0] * (1 - ratio) + end_rgb[0] * ratio)
    g = int(start_rgb[1] * (1 - ratio) + end_rgb[1] * ratio)
    b = int(start_rgb[2] * (1 - ratio) + end_rgb[2] * ratio)
    return (int(r * fade_factor), int(g * fade_factor), int(b * fade_factor))

def print_art_logo():
    os.system('cls' if os.name == 'nt' else 'clear')

    # 1. 폰트 로드
    font_path = "C:/Windows/Fonts/malgunbd.ttf"
    if not os.path.exists(font_path): font_path = "C:/Windows/Fonts/malgun.ttf"
    try: font = ImageFont.truetype(font_path, size=150)
    except: return

    # 2. 캔버스 생성 및 텍스트 그리기
    dummy = ImageDraw.Draw(Image.new('RGB', (1, 1)))
    bbox = dummy.textbbox((0, 0), TEXT, font=font)
    w, h = bbox[2]-bbox[0], bbox[3]-bbox[1]
    
    pad_x, pad_y = int(w*0.15), int(h*0.3)
    
    image_hi = Image.new('RGB', (w + pad_x*2, h + pad_y*2), (0, 0, 0))
    draw = ImageDraw.Draw(image_hi)
    draw.text((pad_x, pad_y), TEXT, font=font, fill=(255, 255, 255))
    
    image_hi = image_hi.filter(ImageFilter.GaussianBlur(radius=2))

    # 3. 리사이징 (변수명 오류 수정됨)
    aspect = image_hi.width / image_hi.height
    new_height = HEIGHT_TARGET
    new_width = int(aspect * new_height * 2.0) 

    try:
        term_w = shutil.get_terminal_size().columns
        if new_width > term_w:
            new_height = int(new_height * (term_w / new_width))
            new_width = term_w - 1
    except: pass

    # 최종 크기 조절
    image_lo = image_hi.resize((new_width, new_height), Image.Resampling.BILINEAR)
    pixels = image_lo.convert('L').load() 

    # 4. 아트웍 출력
    final_output = []
    for y in range(new_height):
        line_buffer = ""
        for x in range(new_width):
            brightness = pixels[x, y] 
            grad_ratio = x / new_width 
            
            color = (0,0,0)
            char_to_print = " "

            if brightness < 30:
                # 배경
                if random.random() > 0.85: 
                    char_to_print = random.choice(BG_CHARS)
                    color = BG_STAR_COLOR
                else:
                    char_to_print = " "

            elif brightness < 100:
                # 그림자/테두리
                shade_idx = int((brightness - 30) / 70 * 2)
                char_to_print = SHADE_CHARS[shade_idx]
                color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 0.7)

            elif brightness < 220:
                 # 본문
                shade_idx = 2 + int((brightness - 100) / 120 * 1)
                char_to_print = SHADE_CHARS[shade_idx]
                color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 1.0)

            else:
                # 하이라이트 (반짝이)
                if random.random() > 0.92: 
                    char_to_print = random.choice(SPARKLE_CHARS)
                    c = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 1.0)
                    color = (min(255, c[0]+50), min(255, c[1]+50), min(255, c[2]+50))
                else:
                    char_to_print = SHADE_CHARS[3] 
                    color = get_gradient_color(grad_ratio, COLOR_START, COLOR_END, 1.0)

            if char_to_print == " ":
                 line_buffer += " "
            else:
                 line_buffer += f"\033[38;2;{color[0]};{color[1]};{color[2]}m{char_to_print}"

        final_output.append(line_buffer + "\033[0m")

    print("\n".join(final_output))
    print("\033[0m")

if __name__ == "__main__":
    print_art_logo()
    os.system("pause")