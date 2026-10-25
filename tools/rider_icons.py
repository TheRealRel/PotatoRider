# PotatoRider page icons (Valheim): small 3D models built in code and rendered in the same pixel style as Potato
# Tweaks' page icons (40 px art shown at 80 px, dark outline), using Tweaks' renderer (tools/render_icons.py, copied).
#   python3 -I tools/rider_icons.py docs/images [name ...]
import sys, os, math
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from render_icons import (render, box, cyl, sphere, torus, prism, rot, flat, wood, metal, rgb, fbm,
                          WOOD, WOOD_D, WOOD_L, IRON, IRON_D, BRONZE, LEATHER, GOLD)

LEATHER_D = rgb(98, 58, 32)
RED = rgb(150, 42, 34)

def saddle():
    lea, dark = flat(LEATHER, 0.22, 7, 41), flat(LEATHER_D, 0.22, 7, 43)
    # Side profile: low seat dipping between a tall cantle (back, left) and a pommel with a horn (front, right).
    prof = [(-0.75, 0.0), (0.75, 0.0), (0.78, 0.38), (0.66, 0.62), (0.56, 0.5), (0.4, 0.3), (0.0, 0.22),
            (-0.4, 0.28), (-0.6, 0.5), (-0.68, 0.72), (-0.8, 0.6)]
    parts = [(prism(prof, 0.55), lea),
             (prism([(-0.62, 0.3), (-0.42, 0.24), (-0.6, 0.46)], 0.57), dark),                                  # cantle edge
             (prism([(-0.85, -0.12), (0.85, -0.12), (0.8, 0.02), (-0.8, 0.02)], 0.8), flat(RED, 0.25, 6, 45)),   # blanket
             (box((0.09, 0.5, 0.04), (0.05, -0.32, 0.3)), dark),                                                  # stirrup leather
             (torus(0.1, 0.022, (0.05, -0.6, 0.31), axis="z"), metal(IRON)),                                     # stirrup
             (cyl(0.06, 0.14, (0.66, 0.68, 0), sections=10), metal(BRONZE))]                                     # horn
    return render(parts, yaw=-18, pitch=14, fov=30)

def bow():
    limb = wood(WOOD_L, axis=1)
    parts = []
    pts = []
    for i in range(17):
        t = i / 16 * 2 - 1                      # -1 .. 1 along the bow
        y = t * 0.95; x = -0.32 * (1 - t * t) + 0.08 * t ** 4
        pts.append((x, y))
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        L = math.hypot(x1 - x0, y1 - y0); a = math.degrees(math.atan2(y1 - y0, x1 - x0))
        thick = 0.055 * (1 - 0.45 * abs((y0 + y1) / 2) / 0.95)
        seg = cyl(thick, L * 1.15, ((x0 + x1) / 2, (y0 + y1) / 2, 0), axis="x", sections=8)
        rot(seg, [0, 0, 1], a, ((x0 + x1) / 2, (y0 + y1) / 2, 0)); parts.append((seg, limb))
    parts.append((box((0.12, 0.26, 0.12), (-0.32, 0, 0)), flat(LEATHER_D, 0.2, 6, 47)))     # grip
    (xa, ya), (xb, yb) = pts[0], pts[-1]
    parts.append((cyl(0.012, math.hypot(xb - xa, yb - ya), ((xa + xb) / 2, 0, 0), sections=6), flat(rgb(225, 215, 190), 0.05)))  # string
    shaft = cyl(0.022, 1.3, (0.05, 0, 0), axis="x", sections=6)
    parts += [(shaft, wood(WOOD, axis=0)),
              (prism([(0.7, -0.07), (0.86, 0), (0.7, 0.07)], 0.04, (0, 0, 0)), metal(IRON)),            # arrowhead
              (prism([(-0.6, 0), (-0.48, 0.09), (-0.4, 0.09), (-0.5, 0)], 0.02), flat(rgb(225, 225, 220), 0.1)),   # fletching
              (prism([(-0.6, 0), (-0.48, -0.09), (-0.4, -0.09), (-0.5, 0)], 0.02), flat(rgb(225, 225, 220), 0.1))]
    for m, _ in parts: rot(m, [0, 0, 1], 35)
    return render(parts, yaw=15, pitch=10, fov=30)

def horseshoe():
    iron = metal(IRON)
    parts = []
    n = 18
    for i in range(n):
        a0, a1 = math.radians(-35 + 250 * i / n), math.radians(-35 + 250 * (i + 1) / n)
        am = (a0 + a1) / 2; R = 0.5
        seg = box((0.22, 0.07, R * (a1 - a0) * 1.15), (math.cos(am) * R, 0, math.sin(am) * R))
        rot(seg, [0, 1, 0], -math.degrees(am), (math.cos(am) * R, 0, math.sin(am) * R)); parts.append((seg, iron))
    for i in (2, 5, 12, 15):                                          # nail holes
        am = math.radians(-35 + 250 * (i + 0.5) / n)
        parts.append((cyl(0.035, 0.08, (math.cos(am) * 0.5, 0.01, math.sin(am) * 0.5), sections=6), flat(rgb(35, 35, 40), 0.05)))
    for m, _ in parts: rot(m, [0, 1, 0], 145)
    return render(parts, yaw=0, pitch=58, fov=30)

def horse():
    coat = flat(rgb(175, 172, 168), 0.18, 6, 51)                       # grey, like the icon's pony
    mane = flat(rgb(70, 66, 64), 0.25, 9, 53)
    side = [(-0.35, -0.6), (0.25, -0.6), (0.3, -0.1), (0.42, 0.25), (0.75, 0.38), (0.78, 0.58), (0.5, 0.72),
            (0.15, 0.82), (0.0, 0.95), (-0.12, 0.78), (-0.25, 0.4), (-0.4, 0.0)]
    parts = [(prism(side, 0.42), coat),
             (prism([(-0.3, -0.6), (-0.44, 0.0), (-0.3, 0.42), (-0.14, 0.8), (0.02, 0.97), (-0.2, 0.92), (-0.42, 0.62), (-0.58, 0.1), (-0.52, -0.6)], 0.3, (0.0, 0, 0)), mane),
             (prism([(-0.02, 0.88), (0.08, 1.12), (0.14, 0.86)], 0.1, (0, 0, 0.13)), coat),   # ears
             (prism([(-0.02, 0.88), (0.08, 1.12), (0.14, 0.86)], 0.1, (0, 0, -0.13)), coat),
             (sphere(0.055, (0.3, 0.6, 0.21)), flat(rgb(25, 22, 22), 0.05)),                     # eye
             (sphere(0.045, (0.7, 0.42, 0.17)), flat(rgb(45, 40, 40), 0.05)),                    # nostril
             (box((0.06, 0.06, 0.46), (0.45, 0.42, 0)), flat(LEATHER_D, 0.2, 6, 55)),             # bridle noseband
             (box((0.05, 0.42, 0.46), (0.18, 0.55, 0)), flat(LEATHER_D, 0.2, 6, 57))]             # cheek strap
    return render(parts, yaw=-14, pitch=6, fov=30)

ICONS = {"saddle": saddle, "bow": bow, "horseshoe": horseshoe, "horse": horse}

if __name__ == "__main__":
    out = sys.argv[1]; names = sys.argv[2:] or list(ICONS)
    os.makedirs(out, exist_ok=True)
    done = []
    for n in names:
        im = ICONS[n](); im.save(os.path.join(out, f"icon-{n}.png")); done.append(im)
    sheet = Image.new("RGBA", (len(done) * 88, 88), (58, 50, 44, 255))
    for i, im in enumerate(done): sheet.paste(im, (i * 88 + 4, 4), im)
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(os.path.join(out, "_sheet.png"))
