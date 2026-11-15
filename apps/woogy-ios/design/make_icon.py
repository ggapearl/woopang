# 우기 앱 아이콘(Woogy/Assets.xcassets/AppIcon.appiconset/icon-1024.png)을 원본 로고(광택)로 만든다.
# 2026-11-16 대표님 「향후 빌드할 때 로고를 지금로고 사용하지 말고 원본로고(광택) 을 사용해서 빌드하도록」 — 평면 아이콘으로 되돌리지 말 것.
# 원본 logo_gloss_original.png 는 투명 바탕 위 둥근 사각형(테두리 포함). 아이폰이 모서리를 다시 깎으므로 테두리 바깥은 버리고
# 가운데 정사각형(사방 3.5% 안쪽)만 1024 로 줄인다. 투명이 남으면 App Store Connect 가 거절하므로 RGB 로 저장한다.
# 쓰는 법: python make_icon.py   (Pillow)
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'logo_gloss_original.png')
OUT = os.path.join(HERE, '..', 'Woogy', 'Assets.xcassets', 'AppIcon.appiconset', 'icon-1024.png')
KEEP = 0.93   # 둥근 사각형 폭 중 남기는 비율 — 원본 테두리가 아이폰 모서리 안쪽에 겹쳐 보이지 않게

src = Image.open(SRC).convert('RGBA')
x0, y0, x1, y1 = src.split()[3].point(lambda v: 255 if v > 128 else 0).getbbox()
cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
side = min(x1 - x0, y1 - y0) * KEEP
icon = src.crop((round(cx - side / 2), round(cy - side / 2), round(cx + side / 2), round(cy + side / 2)))
icon = icon.resize((1024, 1024), Image.LANCZOS)

# 모서리에 남은 투명은 테두리 안쪽 색(네 변 가운데의 평균)으로 메운다 — 아이폰 모서리에 가려지는 자리
w, h = icon.size
pts = [(w // 2, int(h * 0.03)), (w // 2, int(h * 0.97)), (int(w * 0.03), h // 2), (int(w * 0.97), h // 2)]
fill = tuple(sum(icon.getpixel(p)[i] for p in pts) // len(pts) for i in range(3))
out = Image.new('RGBA', icon.size, fill + (255,))
out.alpha_composite(icon)
out.convert('RGB').save(OUT)
print('saved', os.path.normpath(OUT), out.size, 'RGB')
