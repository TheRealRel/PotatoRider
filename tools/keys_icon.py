# PotatoRider page icon: W, A, S and D keycaps, drawn as 40 px pixel art (shown at 80 px like the others).
# Keycap anatomy after real keys seen from a little above: a flat top face, a taller front skirt below it in shadow,
# thin side walls, the legend on the top face. Carved light wood to match the saddle and bow, letters cut dark.
#   python3 -I tools/keys_icon.py docs/images/icon-keys.png
import sys
import numpy as np
from PIL import Image

OUT = (43, 26, 16)
HI, BASE, MID, DARK = (222, 186, 128), (190, 142, 84), (146, 100, 54), (102, 66, 36)
CUT, LIP = (64, 38, 22), (212, 168, 108)
GRAIN = (172, 126, 72)

GLYPH = {  # 5 x 5
    "W": ["X...X", "X...X", "X.X.X", "XX.XX", "X...X"],
    "A": [".XXX.", "X...X", "XXXXX", "X...X", "X...X"],
    "S": [".XXXX", "X....", ".XXX.", "....X", "XXXX."],
    "D": ["XXXX.", "X...X", "X...X", "X...X", "XXXX."],
}

def key(img, x, y, letter, w=12, h=12):
    # outline box
    img[y:y + h, x:x + w] = OUT
    # body inside the outline
    img[y + 1:y + h - 1, x + 1:x + w - 1] = MID
    skirt = 4                                     # front skirt rows at the bottom
    top_y0, top_y1 = y + 1, y + h - 1 - skirt     # top face rows
    img[top_y0:top_y1, x + 2:x + w - 2] = BASE    # top face, inset from the side walls
    img[top_y0, x + 2:x + w - 2] = HI              # lit back edge
    img[top_y0:top_y1, x + 2] = HI                 # lit left edge
    img[top_y1 - 1, x + 3:x + w - 2] = MID         # top face's front edge turning down
    img[top_y1:y + h - 1, x + 1:x + w - 1] = MID   # skirt
    img[y + h - 2, x + 1:x + w - 1] = DARK         # skirt's lower edge in shadow
    img[top_y1:y + h - 1, x + w - 2] = DARK        # right side wall in shadow
    img[y + 1:top_y1, x + w - 2] = MID
    img[y + 1:top_y1, x + 1] = MID
    # corners rounded: outline pixels off
    for cx, cy in ((x, y), (x + w - 1, y), (x, y + h - 1), (x + w - 1, y + h - 1)):
        img[cy, cx] = (0, 0, 0)
    g = GLYPH[letter]
    gx, gy = x + (w - 5) // 2, top_y0 + 1
    for r, row in enumerate(g):
        for c, ch in enumerate(row):
            if ch == "X":
                img[gy + r, gx + c] = CUT
    # carved: a light lip below each cut where the face continues
    for r, row in enumerate(g):
        for c, ch in enumerate(row):
            if ch == "X" and (r + 1 >= 5 or g[r + 1][c] != "X"):
                yy = gy + r + 1
                if yy < top_y1 - 1 and tuple(img[yy, gx + c]) == BASE: img[yy, gx + c] = LIP
    return img

def grain(img, x, y, w, seed):
    # a few short streaks of grain on the top face, never over the letter or its lip
    rng = np.random.default_rng(seed)
    for _ in range(4):
        yy = y + 2 + rng.integers(0, 6); xx = x + 3 + rng.integers(0, w - 7); n = rng.integers(2, 4)
        for k in range(n):
            if tuple(img[yy, xx + k]) == BASE: img[yy, xx + k] = GRAIN

def build():
    img = np.zeros((40, 40, 3), np.uint8)
    # keys are drawn on black; black = transparent
    w, h = 13, 14
    x0 = (40 - (3 * w + 0)) // 2
    yt = (40 - (2 * h + 1)) // 2
    keys = [(x0 + w, yt, "W")] + [(x0 + i * w, yt + h + 1, ch) for i, ch in enumerate("ASD")]
    for n, (x, y, ch) in enumerate(keys):
        key(img, x, y, ch, w, h); grain(img, x, y, w, n + 7)
    alpha = (img.sum(axis=2) > 0).astype(np.uint8) * 255
    rgba = np.dstack([img, alpha])
    return Image.fromarray(rgba, "RGBA").resize((80, 80), Image.NEAREST)

if __name__ == "__main__":
    build().save(sys.argv[1])
