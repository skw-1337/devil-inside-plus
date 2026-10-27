# Genere src/devil-inside-plus.ico : tele vintage dont l'ecran montre deux cornes rougeoyantes.
import os
from PIL import Image, ImageDraw, ImageFilter

def dessin(n):
    s = 4 * n
    im = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    m = s * 0.06
    d.rounded_rectangle([m, s * 0.14, s - m, s * 0.94], radius=s * 0.16, fill=(74, 44, 24), outline=(150, 100, 58), width=max(2, s // 40))
    ex0, ey0, ex1, ey1 = s * 0.16, s * 0.24, s * 0.72, s * 0.84
    glow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(glow).rounded_rectangle([ex0, ey0, ex1, ey1], radius=s * 0.12, fill=(232, 96, 28, 255))
    im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(s * 0.03)))
    d.rounded_rectangle([ex0, ey0, ex1, ey1], radius=s * 0.12, fill=(18, 12, 10))
    cx = (ex0 + ex1) / 2; w = ex1 - ex0; h = ey1 - ey0
    for side in (-1, 1):
        bx = cx + side * w * 0.16
        d.polygon([(bx - side * w * 0.12, ey0 + h * 0.62), (bx + side * w * 0.10, ey0 + h * 0.62), (bx + side * w * 0.22, ey0 + h * 0.18)], fill=(232, 70, 24))
    for k, y in enumerate((0.36, 0.56)):
        d.ellipse([s * 0.78, s * y, s * 0.88, s * (y + 0.10)], fill=(201, 162, 90))
    d.line([(s * 0.36, s * 0.14), (s * 0.24, s * 0.02)], fill=(150, 100, 58), width=max(2, s // 32))
    d.line([(s * 0.52, s * 0.14), (s * 0.64, s * 0.02)], fill=(150, 100, 58), width=max(2, s // 32))
    return im.resize((n, n), Image.LANCZOS)

ici = os.path.dirname(os.path.abspath(__file__))
big = dessin(256)
big.save(os.path.join(ici, "..", "src", "devil-inside-plus.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
big.save(os.path.join(ici, "..", "src", "devil-inside-plus.png"))
print("ok")
