import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

S = 4
W = 256 * S
BG = (10, 10, 13, 255)
WHITE = (245, 245, 247, 255)
LINE = (228, 228, 234, 255)
PURPLE = (139, 92, 246, 255)
DIM = (40, 40, 52, 255)

def chamfer(x0, y0, x1, y1, c):
    return [(x0 + c, y0), (x1, y0), (x1, y1 - c), (x1 - c, y1), (x0, y1), (x0, y0 + c)]

def seven(d, x, y, w, h, t, digit, fill, off):

    on = {0: "abcdef", 1: "bc", 2: "abged", 3: "abgcd", 4: "fgbc", 5: "afgcd", 6: "afgedc",
          7: "abc", 8: "abcdefg", 9: "abcdfg"}[digit]
    g = t * 0.18
    m = y + h / 2
    def hseg(cx0, cx1, cy):
        return [(cx0 + g, cy), (cx0 + g + t / 2, cy - t / 2), (cx1 - g - t / 2, cy - t / 2), (cx1 - g, cy),
                (cx1 - g - t / 2, cy + t / 2), (cx0 + g + t / 2, cy + t / 2)]
    def vseg(cx, cy0, cy1):
        return [(cx, cy0 + g), (cx + t / 2, cy0 + g + t / 2), (cx + t / 2, cy1 - g - t / 2), (cx, cy1 - g),
                (cx - t / 2, cy1 - g - t / 2), (cx - t / 2, cy0 + g + t / 2)]
    segs = {
        "a": hseg(x + t / 2, x + w - t / 2, y + t / 2),
        "g": hseg(x + t / 2, x + w - t / 2, m),
        "d": hseg(x + t / 2, x + w - t / 2, y + h - t / 2),
        "f": vseg(x + t / 2, y + t / 2, m), "b": vseg(x + w - t / 2, y + t / 2, m),
        "e": vseg(x + t / 2, m, y + h - t / 2), "c": vseg(x + w - t / 2, m, y + h - t / 2),
    }
    for k, poly in segs.items():
        d.polygon(poly, fill=fill if k in on else off)

def main(out):
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    pad = 10 * S
    d.polygon(chamfer(pad, pad, W - pad, W - pad, 34 * S), fill=BG)

    lines = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lines)
    for yy in range(pad, W - pad, 4 * S):
        ld.line([(pad, yy), (W - pad, yy)], fill=(18, 18, 24, 255), width=S)
    clip = Image.new("L", (W, W), 0)
    ImageDraw.Draw(clip).polygon(chamfer(pad, pad, W - pad, W - pad, 34 * S), fill=255)
    img.paste(lines, (0, 0), Image.composite(lines.split()[3], Image.new("L", (W, W), 0), clip))
    d = ImageDraw.Draw(img)
    d.polygon(chamfer(pad, pad, W - pad, W - pad, 34 * S), outline=PURPLE, width=5 * S)

    b, L, k = 20 * S, 26 * S, 4 * S
    for (cx, cy, sx, sy) in ((b, W - b, 1, -1), (W - b, b, -1, 1)):
        d.line([(cx, cy), (cx + sx * L, cy)], fill=WHITE, width=k)
        d.line([(cx, cy), (cx, cy + sy * L)], fill=WHITE, width=k)

    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed-Bold.ttf", 30 * S)
    except OSError:
        font = ImageFont.load_default()
    d.polygon(chamfer(44 * S, 30 * S, 212 * S, 70 * S, 10 * S), fill=WHITE)
    tw = d.textlength("NEON HUD", font=font)
    d.text(((W - tw) / 2, 33 * S), "NEON HUD", font=font, fill=BG)

    dw, dh, t, gap = 52 * S, 96 * S, 12 * S, 10 * S
    x0 = (W - (3 * dw + 2 * gap)) / 2
    y0 = 86 * S
    glow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    for i, digit in enumerate((1, 6, 4)):
        seven(gd, x0 + i * (dw + gap) - 4 * S, y0 + 2 * S, dw, dh, t, digit, PURPLE, (0, 0, 0, 0))
    img = Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(6 * S)))
    img = Image.alpha_composite(img, glow)
    d = ImageDraw.Draw(img)
    for i, digit in enumerate((1, 6, 4)):
        seven(d, x0 + i * (dw + gap), y0, dw, dh, t, digit, WHITE, DIM)

    n, sx0, sx1, sy0, sy1 = 12, 44 * S, 212 * S, 200 * S, 216 * S
    sw = (sx1 - sx0) / n
    for i in range(n):
        c = WHITE if i < 5 else (PURPLE if i < 9 else DIM)
        d.rectangle([sx0 + i * sw + 2 * S, sy0, sx0 + (i + 1) * sw - 2 * S, sy1], fill=c)
    img.resize((256, 256), Image.LANCZOS).save(out)

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "icon.png")
