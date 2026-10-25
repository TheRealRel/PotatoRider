# PotatoRider page icon: W, A, S and D keycaps as small 3D models, rendered like the other page icons (Tweaks'
# tools/render_icons.py: textured, lit from the top-left, 40 px art shown at 80 px, dark outline).
# Keycap after a real keyboard key: a square base, sides sloping in to a smaller top face set a little towards the
# back, the legend on the top face. Carved light wood to match the saddle and bow; letters cut in dark.
#   python3 -I tools/keys_icon.py docs/images/icon-keys.png
import sys, os
import numpy as np
import trimesh
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from render_icons import render, box, wood, flat, rgb, WOOD_L

GLYPH = {  # 5 x 5, row 0 at the back of the key
    "W": ["X...X", "X...X", "X.X.X", "XX.XX", "X...X"],
    "A": [".XXX.", "X...X", "XXXXX", "X...X", "X...X"],
    "S": [".XXXX", "X....", ".XXX.", "....X", "XXXX."],
    "D": ["XXXX.", "X...X", "X...X", "X...X", "XXXX."],
}

def keycap(cx, cz, letter, seed):
    b, t, h = 0.47, 0.37, 0.30                 # half-width at the base, at the top, height
    back = -0.04                                # the top sits a little towards the back
    pts = [(cx + sx * b, 0, cz + sz * b) for sx in (-1, 1) for sz in (-1, 1)] + \
          [(cx + sx * t, h, cz + back + sz * t) for sx in (-1, 1) for sz in (-1, 1)]
    cap = trimesh.convex.convex_hull(np.array(pts))
    parts = [(cap, wood(WOOD_L, axis=0, seed=seed))]
    cut = flat(rgb(62, 36, 20), 0.05)
    cell = 2 * t * 0.8 / 5
    x0, z0 = cx - 2.5 * cell + cell / 2, cz + back - 2.5 * cell + cell / 2
    for r, row in enumerate(GLYPH[letter]):
        for c, ch in enumerate(row):
            if ch == "X":
                parts.append((box((cell * 1.02, 0.02, cell * 1.02), (x0 + c * cell, h + 0.005, z0 + r * cell)), cut))
    return parts

def build():
    parts = []
    gap = 1.12
    parts += keycap(0, -gap, "W", 3)
    for i, ch in enumerate("ASD"): parts += keycap((i - 1) * gap, 0, ch, 4 + i)
    return render(parts, yaw=0, pitch=64, fov=26)   # render() already does the outline and colour steps; pixel_finish blurred the letters

if __name__ == "__main__":
    build().save(sys.argv[1])
