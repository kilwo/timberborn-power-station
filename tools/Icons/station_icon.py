# Draws the Power Transfer Station toolbar icon in the vanilla style: flat line art in the game's icon gold
# (186, 160, 107), ~2.5 px rounded strokes at 112 px, soft black drop shadow, transparent background.
# Style measured from the vanilla power icons (PowerShaftIcon, ClutchIcon, ZiplinePylonIcon) in resources.assets.
#
#   python station_icon.py <out.png> [preview.png]
import math
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

SIZE = 112
K = 8  # supersampling
GOLD = (186, 160, 107, 255)
STROKE = 2.5
OFFSET_X = 3  # keeps the left arrow's shadow inside the canvas


def p(x, y):
    return ((x + OFFSET_X) * K, y * K)


class Pen:
    def __init__(self):
        self.img = Image.new("L", (SIZE * K, SIZE * K), 0)
        self.d = ImageDraw.Draw(self.img)
        self.w = int(STROKE * K)

    def line(self, pts, width=None):
        w = int((width or STROKE) * K)
        pts = [p(*q) for q in pts]
        self.d.line(pts, fill=255, width=w, joint="curve")
        for q in (pts[0], pts[-1]):  # round caps
            self.d.ellipse([q[0] - w / 2, q[1] - w / 2, q[0] + w / 2, q[1] + w / 2], fill=255)

    def ellipse(self, cx, cy, rx, ry, width=None):
        w = int((width or STROKE) * K)
        self.d.ellipse([p(cx - rx, cy - ry), p(cx + rx, cy + ry)], outline=255, width=w)

    def rounded_rect(self, x0, y0, x1, y1, r, width=None):
        w = int((width or STROKE) * K)
        self.d.rounded_rectangle([p(x0, y0), p(x1, y1)], radius=r * K, outline=255, width=w)

    def polygon(self, pts, width=None):
        self.line(list(pts) + [pts[0]], width)

    def curve(self, a, b, sag, n=24, width=None):
        pts = []
        for i in range(n + 1):
            t = i / n
            pts.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t + sag * 4 * t * (1 - t)))
        self.line(pts, width)


def draw(pen):
    # Near station: wheel (horizontal pulley seen from above), tapered trestle, gearbox.
    cx, top = 33, 21
    pen.ellipse(cx, top, 14, 5)
    pen.line([(cx - 4, 27), (cx - 7.5, 67)])
    pen.line([(cx + 4, 27), (cx + 7.5, 67)])
    pen.line([(cx - 5.1, 40), (cx + 5.1, 40)], 2.0)
    pen.line([(cx - 6.4, 53), (cx + 6.4, 53)], 2.0)
    pen.rounded_rect(cx - 10, 67, cx + 10, 93, 3)
    # Shaft stubs: hatched bars with the vanilla shaft-icon end arrows (same proportions as PowerShaftIcon).
    for side in (-1, 1):
        x0 = cx + side * 10
        x1 = cx + side * 20
        lo, hi = min(x0, x1), max(x0, x1)
        pen.rounded_rect(lo, 74, hi, 86, 2.5)
        for hx in (0.3, 0.7):
            hx0 = lo + (hi - lo) * hx
            pen.line([(hx0 - 2, 83.5), (hx0 + 2, 76.5)], 1.8)
        tip = x1 + side * 9
        pen.polygon([(x1 + side * 2.5, 71.5), (tip, 80), (x1 + side * 2.5, 88.5)], 2.2)

    # Far station: smaller wheel on a short post.
    fx, ftop = 90, 44
    pen.ellipse(fx, ftop, 9, 3.2)
    pen.line([(fx - 1.6, 48), (fx - 1.6, 66)], 2.0)
    pen.line([(fx + 1.6, 48), (fx + 1.6, 66)], 2.0)
    pen.line([(fx - 7, 67), (fx + 7, 67)])

    # Cable loop: two sagging strands from the top and bottom of the near wheel to the far wheel.
    pen.curve((cx + 2, top - 5), (fx, ftop - 3.2), 4)
    pen.curve((cx + 6, top + 4.4), (fx, ftop + 3.2), 4)


def render(out_path):
    pen = Pen()
    draw(pen)
    mask = pen.img.resize((SIZE, SIZE), Image.LANCZOS)
    # Shadow: blurred mask, slightly down, ~45% black (vanilla shadow alpha peaks around 60 next to strokes).
    shadow_mask = mask.filter(ImageFilter.GaussianBlur(2.2))
    shadow_mask = ImageChops.offset(shadow_mask, 0, 1).point(lambda a: int(a * 0.55))
    icon = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    icon.paste((0, 0, 0, 255), (0, 0), shadow_mask)
    stroke = Image.new("RGBA", (SIZE, SIZE), GOLD)
    stroke.putalpha(mask)
    icon.alpha_composite(stroke)
    icon.save(out_path)
    return icon


if __name__ == "__main__":
    icon = render(sys.argv[1])
    print("wrote", sys.argv[1], "bbox", icon.getbbox())
