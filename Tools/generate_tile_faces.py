#!/usr/bin/env python3
"""
Generates Assets/_Project/Resources/TileFaces.png — traditional Hong Kong mahjong tile faces
for all 42 tile kinds, in one atlas.

Layout: 8 columns x 6 rows of 256x340 cells (tile face aspect 3:4) in a 2048x2048 image.
Kind index k (see Tile.cs) lives at column k % 8, row k // 8, counted from the TOP-LEFT.
TileAtlas.cs must use the same numbers.

Requires Pillow and a CJK font (default: WenQuanYi Zen Hei). Usage:
    python3 Tools/generate_tile_faces.py [--font PATH] [--out PATH]
"""
import argparse
import math
from PIL import Image, ImageDraw, ImageFont

COLS, ROWS = 8, 6
CELL_W, CELL_H = 256, 340
ATLAS = 2048
IVORY = (246, 241, 226)
RED = (200, 30, 35)
GREEN = (20, 125, 70)
BLUE = (25, 60, 150)
DARK = (25, 25, 45)
SS = 3  # supersampling factor for smooth edges


def font(path, size):
    return ImageFont.truetype(path, size * SS)


class Face:
    """Draws one tile face at SS x resolution, then downsamples."""

    def __init__(self):
        self.img = Image.new("RGB", (CELL_W * SS, CELL_H * SS), IVORY)
        self.d = ImageDraw.Draw(self.img)

    def s(self, v):
        return v * SS

    def circle(self, cx, cy, r, fill, outline=None, width=0):
        cx, cy, r = self.s(cx), self.s(cy), self.s(r)
        self.d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill, outline=outline, width=self.s(width))

    def text(self, cx, cy, txt, fnt, fill):
        box = self.d.textbbox((0, 0), txt, font=fnt)
        w, h = box[2] - box[0], box[3] - box[1]
        self.d.text((self.s(cx) - w / 2 - box[0], self.s(cy) - h / 2 - box[1]), txt, font=fnt, fill=fill)

    def rect(self, x0, y0, x1, y1, fill=None, outline=None, width=0, radius=0):
        self.d.rounded_rectangle([self.s(x0), self.s(y0), self.s(x1), self.s(y1)], radius=self.s(radius),
                                 fill=fill, outline=outline, width=self.s(width))

    def poly(self, pts, fill):
        self.d.polygon([(self.s(x), self.s(y)) for x, y in pts], fill=fill)

    def done(self):
        return self.img.resize((CELL_W, CELL_H), Image.LANCZOS)


# ------------------------------------------------------------------ dots (筒)

def dot(f, cx, cy, r, color):
    """A traditional 'coin' dot: coloured ring, white ring, coloured disc, centre pip."""
    f.circle(cx, cy, r, fill=color)
    f.circle(cx, cy, r * 0.78, fill=IVORY)
    f.circle(cx, cy, r * 0.62, fill=color)
    f.circle(cx, cy, r * 0.30, fill=IVORY)
    f.circle(cx, cy, r * 0.16, fill=color)


def big_dot(f):
    cx, cy = CELL_W / 2, CELL_H / 2
    f.circle(cx, cy, 100, fill=GREEN)
    # petals
    for i in range(16):
        a = i * math.pi / 8
        f.circle(cx + math.cos(a) * 82, cy + math.sin(a) * 82, 13, fill=IVORY)
    f.circle(cx, cy, 64, fill=BLUE)
    f.circle(cx, cy, 50, fill=IVORY)
    f.circle(cx, cy, 38, fill=RED)
    f.circle(cx, cy, 16, fill=IVORY)
    f.circle(cx, cy, 8, fill=RED)


def dots(rank):
    f = Face()
    W, H = CELL_W, CELL_H
    if rank == 1:
        big_dot(f)
        return f.done()
    layouts = {
        2: [(W / 2, H * 0.29, GREEN), (W / 2, H * 0.71, BLUE)],
        3: [(W * 0.27, H * 0.22, BLUE), (W / 2, H / 2, RED), (W * 0.73, H * 0.78, GREEN)],
        4: [(W * 0.3, H * 0.3, BLUE), (W * 0.7, H * 0.3, GREEN), (W * 0.3, H * 0.7, GREEN), (W * 0.7, H * 0.7, BLUE)],
        5: [(W * 0.27, H * 0.24, BLUE), (W * 0.73, H * 0.24, GREEN), (W / 2, H / 2, RED),
            (W * 0.27, H * 0.76, GREEN), (W * 0.73, H * 0.76, BLUE)],
        6: [(W * 0.3, H * 0.19, GREEN), (W * 0.7, H * 0.19, GREEN),
            (W * 0.3, H * 0.52, RED), (W * 0.7, H * 0.52, RED), (W * 0.3, H * 0.81, RED), (W * 0.7, H * 0.81, RED)],
        7: [(W * 0.22, H * 0.13, GREEN), (W * 0.5, H * 0.23, GREEN), (W * 0.78, H * 0.33, GREEN),
            (W * 0.3, H * 0.6, RED), (W * 0.7, H * 0.6, RED), (W * 0.3, H * 0.85, RED), (W * 0.7, H * 0.85, RED)],
        8: [(W * x, H * y, BLUE) for y in (0.14, 0.38, 0.62, 0.86) for x in (0.3, 0.7)],
        9: [(W * x, H * y, c) for y, c in ((0.2, BLUE), (0.5, RED), (0.8, GREEN)) for x in (0.22, 0.5, 0.78)],
    }
    r = {2: 58, 3: 46, 4: 52, 5: 44, 6: 40, 7: 33, 8: 34, 9: 34}[rank]
    for x, y, c in layouts[rank]:
        dot(f, x, y, r, c)
    return f.done()


# ------------------------------------------------------------------ bamboo (索)

def stick(f, cx, cy, length, color, angle=0.0, width=30):
    """A bamboo stick: a long capsule with three nodes, optionally tilted (radians)."""
    dx, dy = math.sin(angle), -math.cos(angle)
    half = length / 2
    steps = 24
    for i in range(steps + 1):
        t = -half + length * i / steps
        f.circle(cx + dx * t, cy + dy * t, width / 2, fill=color)
    for t in (-half * 0.62, 0, half * 0.62):
        nx, ny = cx + dx * t, cy + dy * t
        px, py = -dy, dx
        f.d.line([(f.s(nx - px * width * 0.55), f.s(ny - py * width * 0.55)),
                  (f.s(nx + px * width * 0.55), f.s(ny + py * width * 0.55))], fill=IVORY, width=f.s(5))
    # bright stripe down the middle
    f.d.line([(f.s(cx - dx * half * 0.9), f.s(cy - dy * half * 0.9)),
              (f.s(cx + dx * half * 0.9), f.s(cy + dy * half * 0.9))], fill=IVORY, width=f.s(3))


def bird(f):
    """1 Bamboo: a stylised sparrow perched on a bamboo stem."""
    W, H = CELL_W, CELL_H
    stick(f, W * 0.5, H * 0.8, 90, GREEN, angle=0.0, width=20)
    f.poly([(70, 210), (40, 270), (75, 255), (60, 300), (100, 245)], GREEN)       # tail feathers
    f.poly([(90, 190), (55, 175), (70, 215)], BLUE)
    f.d.ellipse([f.s(78), f.s(120), f.s(190), f.s(240)], fill=GREEN)               # body
    f.d.ellipse([f.s(100), f.s(150), f.s(175), f.s(230)], fill=(60, 170, 100))      # belly
    f.poly([(110, 160), (165, 150), (130, 205)], BLUE)                               # wing
    f.circle(176, 112, 34, fill=RED)                                                 # head
    f.circle(186, 104, 7, fill=IVORY)
    f.circle(188, 104, 3.5, fill=DARK)
    f.poly([(205, 110), (232, 118), (205, 124)], (220, 150, 30))                     # beak
    for x in (125, 145):
        f.d.line([(f.s(x), f.s(236)), (f.s(x - 6), f.s(262))], fill=RED, width=f.s(4))


def bamboo(rank):
    f = Face()
    W, H = CELL_W, CELL_H
    if rank == 1:
        bird(f)
        return f.done()
    L = 92
    xs3 = (W * 0.22, W * 0.5, W * 0.78)
    layouts = {
        2: [(W / 2, H * 0.27, BLUE), (W / 2, H * 0.73, GREEN)],
        3: [(W / 2, H * 0.27, BLUE), (W * 0.32, H * 0.73, GREEN), (W * 0.68, H * 0.73, GREEN)],
        4: [(W * x, H * y, c) for y in (0.27, 0.73) for x, c in ((0.3, GREEN), (0.7, BLUE))],
        5: [(W * 0.22, H * 0.27, GREEN), (W * 0.78, H * 0.27, BLUE), (W * 0.5, H * 0.5, RED),
            (W * 0.22, H * 0.73, BLUE), (W * 0.78, H * 0.73, GREEN)],
        6: [(x, H * y, GREEN) for y in (0.27, 0.73) for x in xs3],
        7: [(W / 2, H * 0.17, RED)] + [(x, H * y, GREEN) for y in (0.5, 0.82) for x in xs3],
        9: [(x, H * y, RED if x == W * 0.5 else GREEN) for y in (0.18, 0.5, 0.82) for x in xs3],
    }
    if rank == 8:
        # Two rows of four in the traditional "M / W" arrangement.
        for row, y in ((0, H * 0.29), (1, H * 0.71)):
            for i, x in enumerate((W * 0.18, W * 0.39, W * 0.61, W * 0.82)):
                tilt = (0.28 if i in (0, 2) else -0.28) * (1 if row == 0 else -1)
                stick(f, x, y, 100, GREEN, angle=tilt, width=22)
        return f.done()
    # Two rows: 156 px apart, so length + width must stay under ~145. Three rows: ~110 apart.
    length, width = {2: (112, 32), 3: (110, 30), 4: (110, 30), 5: (100, 28), 6: (108, 28),
                     7: (70, 26), 9: (70, 26)}[rank]
    for x, y, c in layouts[rank]:
        stick(f, x, y, length, c, width=width)
    return f.done()


# ------------------------------------------------------------------ characters & honours

NUMERALS = "一二三四五六七八九"


def characters(rank, fpath):
    f = Face()
    f.text(CELL_W / 2, CELL_H * 0.29, NUMERALS[rank - 1], font(fpath, 112), DARK)
    f.text(CELL_W / 2, CELL_H * 0.71, "萬", font(fpath, 128), RED)
    return f.done()


def big_char(ch, color, fpath, size=190):
    f = Face()
    f.text(CELL_W / 2, CELL_H / 2, ch, font(fpath, size), color)
    return f.done()


def white_dragon():
    f = Face()
    f.rect(38, 48, CELL_W - 38, CELL_H - 48, outline=BLUE, width=12, radius=6)
    f.rect(62, 72, CELL_W - 62, CELL_H - 72, outline=BLUE, width=5, radius=4)
    return f.done()


def bonus(ch, number, color, fpath):
    f = Face()
    f.text(CELL_W / 2, CELL_H * 0.56, ch, font(fpath, 150), color)
    f.text(CELL_W * 0.2, CELL_H * 0.15, str(number), font(fpath, 60), RED)
    return f.done()


def all_faces(fpath):
    faces = []
    faces += [dots(r) for r in range(1, 10)]
    faces += [bamboo(r) for r in range(1, 10)]
    faces += [characters(r, fpath) for r in range(1, 10)]
    faces += [big_char(c, DARK, fpath) for c in "東南西北"]
    faces += [big_char("中", RED, fpath), big_char("發", GREEN, fpath), white_dragon()]
    flowers = [("梅", (200, 40, 90)), ("蘭", (110, 50, 160)), ("菊", (215, 130, 20)), ("竹", GREEN)]
    seasons = [("春", GREEN), ("夏", RED), ("秋", (200, 110, 20)), ("冬", BLUE)]
    faces += [bonus(c, i + 1, col, fpath) for i, (c, col) in enumerate(flowers)]
    faces += [bonus(c, i + 1, col, fpath) for i, (c, col) in enumerate(seasons)]
    assert len(faces) == 42
    return faces


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--font", default="/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc")
    ap.add_argument("--out", default="Assets/_Project/Resources/TileFaces.png")
    args = ap.parse_args()

    atlas = Image.new("RGB", (ATLAS, ATLAS), IVORY)
    for k, face in enumerate(all_faces(args.font)):
        atlas.paste(face, ((k % COLS) * CELL_W, (k // COLS) * CELL_H))
    atlas.save(args.out, optimize=True)
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()
