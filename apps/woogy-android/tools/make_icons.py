# -*- coding: utf-8 -*-
"""우기 안드로이드 앱 아이콘 · 스플래시 · Play 스토어 그래픽 만들기.

2026-11-16 대표님 「향후 빌드할 때 로고를 지금로고 사용하지 말고 원본로고(광택)을 사용해서 빌드」 — 원본 광택 로고(apps/woogy-ios/design/logo_gloss_original.png)에서 만든다.
  python make_icons.py        → android/app/src/main/res/mipmap-*, drawable-nodpi/splash_logo.png, store/icon_512.png, store/feature_1024x500.png

안드로이드 적응형 아이콘은 108×108dp 캔버스, 가운데 지름 66dp 원 안이 어떤 모양으로 잘려도 보이는 안전 영역이다.
날개 하트(가로 약 72%)가 안전 영역에 들도록 원본을 80dp 로 놓는다(보이는 창 72dp 를 가득 채우고 모서리만 잘린다).
"""
import os
import pathlib

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent
RES = ROOT / "android" / "app" / "src" / "main" / "res"
STORE = ROOT / "store"
SRC = ROOT.parent / "woogy-ios" / "design" / "logo_gloss_original.png"
KEEP = 0.93          # 둥근 사각형 폭 중 남기는 비율(원본 테두리 제거) — iOS make_icon.py 와 같다


def base_icon():
    src = Image.open(SRC).convert("RGBA")
    x0, y0, x1, y1 = src.split()[3].point(lambda v: 255 if v > 128 else 0).getbbox()
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    side = min(x1 - x0, y1 - y0) * KEEP
    icon = src.crop((round(cx - side / 2), round(cy - side / 2), round(cx + side / 2), round(cy + side / 2)))
    icon = icon.resize((1024, 1024), Image.LANCZOS)
    w, h = icon.size
    pts = [(w // 2, int(h * 0.03)), (w // 2, int(h * 0.97)), (int(w * 0.03), h // 2), (int(w * 0.97), h // 2)]
    fill = tuple(sum(icon.getpixel(p)[i] for p in pts) // len(pts) for i in range(3))
    out = Image.new("RGBA", icon.size, fill + (255,))
    out.alpha_composite(icon)
    return out.convert("RGB"), fill


def rounded(img, radius_frac):
    s = img.size[0]
    m = Image.new("L", (s * 4, s * 4), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, s * 4 - 1, s * 4 - 1), radius=int(s * 4 * radius_frac), fill=255)
    m = m.resize((s, s), Image.LANCZOS)
    o = img.convert("RGBA")
    o.putalpha(m)
    return o


def circle(img):
    s = img.size[0]
    m = Image.new("L", (s * 4, s * 4), 0)
    ImageDraw.Draw(m).ellipse((0, 0, s * 4 - 1, s * 4 - 1), fill=255)
    o = img.convert("RGBA")
    o.putalpha(m.resize((s, s), Image.LANCZOS))
    return o


def mono_mask(icon):
    """초록 선(날개 하트)만 불투명으로 — 안드로이드 13 테마 아이콘용."""
    r, g, b = icon.split()
    m = ImageChops.subtract(g, r).point(lambda v: 255 if v > 60 else max(0, min(255, (v - 20) * 6)))
    return m.filter(ImageFilter.GaussianBlur(0.6))


def main():
    icon, fill = base_icon()
    print("icon bg color", "#%02X%02X%02X" % fill)
    DPI = {"mdpi": 1.0, "hdpi": 1.5, "xhdpi": 2.0, "xxhdpi": 3.0, "xxxhdpi": 4.0}
    mono = mono_mask(icon)
    for name, k in DPI.items():
        d = RES / f"mipmap-{name}"
        d.mkdir(parents=True, exist_ok=True)
        legacy = int(48 * k)
        rounded(icon.resize((legacy, legacy), Image.LANCZOS), 0.22).save(d / "ic_launcher.png")
        circle(icon.resize((legacy, legacy), Image.LANCZOS)).save(d / "ic_launcher_round.png")
        canvas = int(108 * k)
        inner = int(80 * k)
        fg = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
        fg.paste(icon.resize((inner, inner), Image.LANCZOS), ((canvas - inner) // 2, (canvas - inner) // 2))
        fg.save(d / "ic_launcher_foreground.png")
        mm = Image.new("L", (canvas, canvas), 0)
        mm.paste(mono.resize((inner, inner), Image.LANCZOS), ((canvas - inner) // 2, (canvas - inner) // 2))
        mono_img = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 255))
        mono_img.putalpha(mm)
        mono_img.save(d / "ic_launcher_monochrome.png")
    nd = RES / "drawable-nodpi"
    nd.mkdir(parents=True, exist_ok=True)
    rounded(icon.resize((360, 360), Image.LANCZOS), 0.22).save(nd / "splash_logo.png")

    STORE.mkdir(exist_ok=True)
    icon.resize((512, 512), Image.LANCZOS).convert("RGB").save(STORE / "icon_512.png", optimize=True)

    # Play 대표 그래픽 1024×500 — 노랑 바탕 + 광택 아이콘 + 문구
    W, H = 1024, 500
    bg = Image.new("RGB", (W, H), "#FFE98A")
    px = bg.load()
    for y in range(H):
        for x in range(W):
            t = (x / W * 0.6 + y / H * 0.4)
            px[x, y] = (255, int(243 - 22 * t), int(191 - 95 * t))
    f = bg.convert("RGBA")
    ic = rounded(icon.resize((300, 300), Image.LANCZOS), 0.22)
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sh.paste((60, 90, 40, 90), (86 + 6, 100 + 12, 86 + 306, 100 + 312), rounded(Image.new("RGB", (300, 300)), 0.22).split()[3])
    f.alpha_composite(sh.filter(ImageFilter.GaussianBlur(14)))
    f.alpha_composite(ic, (80, 100))
    d = ImageDraw.Draw(f)
    bold = r"C:\Windows\Fonts\malgunbd.ttf"
    reg = r"C:\Windows\Fonts\malgun.ttf"
    d.text((440, 138), "우리동네 기부천사", font=ImageFont.truetype(bold, 62), fill="#1B5E39")
    d.text((440, 232), "우기", font=ImageFont.truetype(bold, 120), fill="#2A6B42")
    d.text((444, 396), "이웃의 필요를 동네 가게에서 대신 결제해요", font=ImageFont.truetype(reg, 26), fill="#3F5247")
    f.convert("RGB").save(STORE / "feature_1024x500.png", optimize=True)
    print("done")


if __name__ == "__main__":
    main()
