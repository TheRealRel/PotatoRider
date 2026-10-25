# PotatoRider page icons (Valheim): small 3D models built in code and rendered in the same pixel style as Potato
# Tweaks' page icons (40 px art shown at 80 px, dark outline), using Tweaks' renderer (tools/render_icons.py, copied).
#   python3 -I tools/rider_icons.py docs/images [name ...]
import sys, os, math
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from render_icons import (R, finish_px, render, box, cyl, sphere, torus, prism, rot, flat, wood, metal, rgb, fbm,
                          WOOD, WOOD_D, WOOD_L, IRON, IRON_D, BRONZE, LEATHER, GOLD)

LEATHER_D = rgb(98, 58, 32)
RED = rgb(150, 42, 34)

def layered(layers, debug=None):
    # Shaded silhouettes stacked like cut-outs: each layer is a drawn outline with its own rounded height (distance
    # to its edge), later layers sit on top, and a lower layer gets a soft contact shadow where a higher one overlaps
    # its edge, so the parts read as separate pieces. Each layer: (draw_fn on a PIL ImageDraw, material, roundness).
    from PIL import ImageDraw
    from scipy import ndimage
    S = R
    top = np.full((S, S), -1); hmap = np.zeros((S, S))
    masks = []
    for i, (fn, mat, rnd) in enumerate(layers):
        im = Image.new("L", (S, S), 0); fn(ImageDraw.Draw(im)); m = np.asarray(im) > 0
        h = np.sqrt(np.clip(ndimage.distance_transform_edt(m) / (S * rnd), 0, 1))
        h = ndimage.gaussian_filter(h, 1.5)
        top[m] = i; hmap[m] = h[m] + i * 0.0
        masks.append(m)
    shape = top >= 0
    gy, gx = np.gradient(ndimage.gaussian_filter(hmap, 1.0) * S * 0.05)
    # Normals only from within a layer: zero the jumps where layers meet.
    for g in (gx, gy): g[np.abs(g) > 2.5] = 0
    n = np.dstack([-gx, gy, np.ones((S, S))]); n /= np.linalg.norm(n, axis=2, keepdims=True)
    yy, xx = np.mgrid[0:S, 0:S]
    pts = np.dstack([xx / S * 3, yy / S * 3, hmap]).reshape(-1, 3); nn = n.reshape(-1, 3)
    col = np.zeros((S, S, 3))
    for i, (fn, mat, rnd) in enumerate(layers):
        sel = top == i
        if sel.any(): col[sel] = mat(pts, nn).reshape(S, S, 3)[sel]
    L = np.array([-0.5, 0.8, 0.6]); L = L / np.linalg.norm(L)
    lam = np.clip(n[..., 0] * L[0] + n[..., 1] * L[1] + n[..., 2] * L[2], 0, 1)
    shade = 0.42 + 0.78 * lam
    # Contact shadow on a lower layer just below/around a higher layer's edge.
    for i in range(1, len(layers)):
        near = ndimage.binary_dilation(masks[i], iterations=int(S * 0.018)) & ~masks[i] & shape & (top < i)
        shade[near] *= 0.72
    img = col * shade[..., None]
    if debug: Image.fromarray(np.uint8(np.clip(img, 0, 1) * 255 * shape[..., None])).save(debug)
    return finish_px(img, shape.astype(float))

def saddle(debug=None):
    # A Norse war saddle in profile, front to the right, drawn after real saddles: a high cantle at the back and a
    # raised pommel at the front, both edged in metal, a seat dipping low between them, a big leather flap hanging
    # down the side, a woven pad showing all round under it, and the stirrup leather hanging from under the front of
    # the seat to a D-shaped iron with a flat tread.
    S = R
    def P(pts): return [(x * S * 0.9 + S * 0.05, (1 - y) * S * 0.9 + S * 0.05) for x, y in pts]
    tree = [(0.12, 0.56), (0.08, 0.7), (0.09, 0.83), (0.14, 0.92), (0.21, 0.93), (0.25, 0.86), (0.31, 0.76),
            (0.42, 0.7), (0.56, 0.7), (0.66, 0.74), (0.73, 0.84), (0.8, 0.91), (0.87, 0.9), (0.9, 0.8), (0.91, 0.62),
            (0.9, 0.56)]
    cantle_edge = [(0.08, 0.72), (0.09, 0.83), (0.14, 0.92), (0.21, 0.93), (0.25, 0.86)]
    pommel_edge = [(0.73, 0.84), (0.8, 0.91), (0.87, 0.9), (0.9, 0.8)]
    pad = [(0.06, 0.62), (0.94, 0.62), (0.96, 0.4), (0.9, 0.28), (0.1, 0.28), (0.04, 0.4)]
    flap = [(0.26, 0.6), (0.7, 0.6), (0.72, 0.3), (0.66, 0.14), (0.32, 0.14), (0.26, 0.24)]
    W = int(S * 0.05)
    wool = rgb(140, 40, 34)
    def pad_mat(p, n):
        c = flat(wool, 0.22, 8, 61)(p, n)
        band = np.abs(p[:, 1] / 3 - (1 - (0.33 * 0.9 + 0.05))) < 0.012           # a woven stripe near the lower edge
        c[band] = rgb(205, 165, 70); return c
    layers = [
        (lambda d: d.polygon(P(pad), fill=255), pad_mat, 0.1),
        (lambda d: d.polygon(P(tree), fill=255), flat(LEATHER, 0.2, 7, 41), 0.1),
        (lambda d: (d.line(P(cantle_edge), fill=255, width=W, joint="curve"),
                    d.line(P(pommel_edge), fill=255, width=W, joint="curve")), metal(BRONZE), 0.03),
        (lambda d: d.polygon(P(flap), fill=255), flat(rgb(118, 74, 40), 0.22, 7, 43), 0.12),
        (lambda d: d.line(P([(0.6, 0.66), (0.62, 0.21)]), fill=255, width=int(S * 0.045)), flat(LEATHER_D, 0.2, 6, 47), 0.03),
        (lambda d: d.line(P([(0.62 + 0.1 * math.cos(math.pi * k / 12), 0.04 + 0.17 * math.sin(math.pi * k / 12)) for k in range(13)]
                             + [(0.72, 0.04)]), fill=255, width=int(S * 0.04), joint="curve"),
         metal(IRON), 0.03),
    ]
    return layered(layers, debug)

def bow():
    # The limbs bow out toward the target (+x), the string runs straight between the tips on the archer's side, and
    # the arrow is nocked on the string and points out through the grip.
    limb = wood(WOOD_L, axis=1)
    parts, pts = [], []
    for i in range(17):
        t = i / 16 * 2 - 1                      # -1 .. 1 along the bow
        y = t * 0.95; x = 0.34 * (1 - t * t) - 0.06 * t ** 4
        pts.append((x, y))
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        L = math.hypot(x1 - x0, y1 - y0); a = math.degrees(math.atan2(y1 - y0, x1 - x0))
        thick = 0.055 * (1 - 0.45 * abs((y0 + y1) / 2) / 0.95)
        seg = cyl(thick, L * 1.15, ((x0 + x1) / 2, (y0 + y1) / 2, 0), axis="x", sections=8)
        rot(seg, [0, 0, 1], a, ((x0 + x1) / 2, (y0 + y1) / 2, 0)); parts.append((seg, limb))
    parts.append((box((0.12, 0.26, 0.12), (0.34, 0, 0)), flat(LEATHER_D, 0.2, 6, 47)))       # grip
    (xa, ya), (xb, yb) = pts[0], pts[-1]
    parts.append((cyl(0.012, math.hypot(xb - xa, yb - ya), ((xa + xb) / 2, 0, 0), sections=6), flat(rgb(225, 215, 190), 0.05)))  # string
    feather = flat(rgb(225, 225, 220), 0.1)
    parts += [(cyl(0.022, 1.12, (0.47, 0, 0), axis="x", sections=6), wood(WOOD, axis=0)),        # shaft, nock at the string
              (prism([(1.02, -0.07), (1.18, 0), (1.02, 0.07)], 0.04), metal(IRON)),               # head past the grip
              (prism([(-0.06, 0), (0.06, 0.09), (0.16, 0.09), (0.08, 0)], 0.02), feather),        # fletching by the string
              (prism([(-0.06, 0), (0.06, -0.09), (0.16, -0.09), (0.08, 0)], 0.02), feather)]
    for m, _ in parts: rot(m, [0, 0, 1], 35)
    return render(parts, yaw=15, pitch=10, fov=30)

def horseshoe():
    # Drawn after a real shoe: a U with the toe at the closed end and the two branches ending in heels; a groove (the
    # fuller) runs along each branch with three nail holes in it, none at the toe.
    iron, groove = metal(IRON), flat(rgb(34, 34, 40), 0.05)
    parts = []
    n, R0 = 22, 0.5
    span0, span1 = -40, 220                                            # degrees; the toe is at 90
    for i in range(n):
        a0 = math.radians(span0 + (span1 - span0) * i / n); a1 = math.radians(span0 + (span1 - span0) * (i + 1) / n)
        am = (a0 + a1) / 2
        seg = box((0.24, 0.08, R0 * (a1 - a0) * 1.15), (math.cos(am) * R0, 0, math.sin(am) * R0))
        rot(seg, [0, 1, 0], -math.degrees(am), (math.cos(am) * R0, 0, math.sin(am) * R0)); parts.append((seg, iron))
    for side in (-1, 1):                                               # fuller and nail holes along each branch
        for deg in (np.linspace(-25, 50, 3) if side < 0 else np.linspace(130, 205, 3)):
            am = math.radians(deg); r_ = R0 + 0.05
            parts.append((cyl(0.045, 0.1, (math.cos(am) * r_, 0.03, math.sin(am) * r_), sections=8), groove))
        lo, hi = (-32, 60) if side < 0 else (120, 212)
        for deg in np.linspace(lo, hi, 9):
            am = math.radians(deg); r_ = R0 + 0.05
            g = box((0.03, 0.09, 0.06), (math.cos(am) * r_, 0.008, math.sin(am) * r_))
            rot(g, [0, 1, 0], -deg, (math.cos(am) * r_, 0.008, math.sin(am) * r_)); parts.append((g, groove))
    for m, _ in parts: rot(m, [0, 1, 0], 180)                       # toe up, heels down, as a shoe is usually drawn
    return render(parts, yaw=0, pitch=72, fov=30)

def horse(debug=None):
    # A grey horse's head and neck in profile, facing right. A silhouette reads better than primitives here: the outline
    # is drawn, given a rounded height (distance to the edge) for lighting, then textured and finished like the others.
    # Tack like a real bridle: crownpiece over the poll behind the ear, cheek strap down behind the eye to a ring at
    # the corner of the mouth, noseband around the nose into the same ring, and the rein running back from the ring.
    # Straps are wide enough (about 2 px at icon size) to stay connected; one clear ear, not two tiny ones.
    from PIL import ImageDraw
    from scipy import ndimage
    S = R
    def P(pts): return [(x * S * 0.86 + S * 0.07, (1 - y) * S * 0.86 + S * 0.07) for x, y in pts]
    def mask(draw_fn):
        im = Image.new("L", (S, S), 0); draw_fn(ImageDraw.Draw(im)); return np.asarray(im) > 0
    body = [(0.08, 0.0), (0.07, 0.24), (0.13, 0.5), (0.25, 0.72), (0.37, 0.86), (0.47, 0.93), (0.57, 0.88),
            (0.67, 0.76), (0.77, 0.6), (0.87, 0.44), (0.94, 0.33), (0.97, 0.25), (0.95, 0.18), (0.88, 0.15),
            (0.8, 0.17), (0.72, 0.21), (0.6, 0.27), (0.5, 0.33), (0.45, 0.32), (0.46, 0.18), (0.52, 0.0)]
    ear = [(0.4, 0.86), (0.41, 0.95), (0.45, 1.03), (0.49, 1.06), (0.53, 1.0), (0.56, 0.92), (0.57, 0.86)]   # leaf-shaped, tip a little forward
    crest = [(0.08, 0.0), (0.07, 0.24), (0.13, 0.5), (0.25, 0.72), (0.37, 0.86), (0.45, 0.92)]
    W = int(S * 0.048)
    # Placed like a real snaffle bridle: the headpiece goes over the poll BEHIND the ear; the browband crosses the
    # forehead just under the ear's base, above the eye; the cheekpiece runs from the poll down behind the eye to the
    # bit at the corner of the mouth; the throatlatch loops from the poll down behind the jaw; the noseband sits a
    # couple of fingers below the cheekbone, clear of the nostril; the rein comes back off the bit.
    ring_at = (0.8, 0.2)                                                    # the bit, at the corner of the mouth
    poll = (0.43, 0.86)
    straps = [[poll, (0.47, 0.76), (0.56, 0.52), (0.68, 0.33), ring_at],   # cheekpiece, behind the eye
              [(0.45, 0.84), (0.6, 0.835)],                                 # browband, under the ear
              [poll, (0.4, 0.66), (0.46, 0.37)],                            # throatlatch, behind the jaw
              [(0.68, 0.225), (0.75, 0.38), (0.82, 0.52)],                  # noseband
              [ring_at, (0.63, 0.1), (0.47, 0.06)]]                         # rein
    rein_out = mask(lambda d: d.line(P(straps[4]), fill=255, width=W, joint="curve"))
    shape = mask(lambda d: (d.polygon(P(body), fill=255), d.polygon(P(ear), fill=255))) | rein_out
    mane = mask(lambda d: (d.line(P(crest), fill=255, width=int(S * 0.2), joint="curve"),
                           d.ellipse([*P([(0.47, 0.95)])[0], *P([(0.6, 0.83)])[0]], fill=255))) & ~mask(
        lambda d: d.polygon(P([(0.2, 0.0), (0.19, 0.25), (0.25, 0.48), (0.35, 0.66), (0.47, 0.78), (0.6, 0.8),
                               (0.6, 0.0)]), fill=255))
    mane &= ~mask(lambda d: d.polygon(P(ear), fill=255))
    strap = mask(lambda d: [d.line(P(st), fill=255, width=W, joint="curve") for st in straps]) & shape
    ring = mask(lambda d: d.ellipse([*P([(ring_at[0] - 0.03, ring_at[1] + 0.03)])[0], *P([(ring_at[0] + 0.03, ring_at[1] - 0.03)])[0]], fill=255))
    eye = mask(lambda d: d.ellipse([*P([(0.575, 0.725)])[0], *P([(0.625, 0.675)])[0]], fill=255))
    nostril = mask(lambda d: d.ellipse([*P([(0.885, 0.31)])[0], *P([(0.925, 0.27)])[0]], fill=255))
    inner_ear = mask(lambda d: d.polygon(P([(0.45, 0.9), (0.47, 0.99), (0.5, 1.0), (0.52, 0.92)]), fill=255))
    # Rounded height from the distance to the edge, then normals.
    h = np.sqrt(np.clip(ndimage.distance_transform_edt(shape) / (S * 0.12), 0, 1))
    h = ndimage.gaussian_filter(h, 2)
    gy, gx = np.gradient(h * S * 0.05)
    n = np.dstack([-gx, gy, np.ones_like(h)]); n /= np.linalg.norm(n, axis=2, keepdims=True)
    yy, xx = np.mgrid[0:S, 0:S]
    pts = np.dstack([xx / S * 3, yy / S * 3, h]).reshape(-1, 3); nn = n.reshape(-1, 3)
    col = flat(rgb(182, 179, 174), 0.22, 7, 51)(pts, nn).reshape(S, S, 3)
    for m_, c_ in ((mane, flat(rgb(64, 60, 58), 0.32, 11, 53)), (inner_ear, flat(rgb(120, 112, 108), 0.2, 6, 54)),
                   (strap, flat(LEATHER_D, 0.2, 6, 55)), (ring, metal(IRON))):
        col[m_] = c_(pts, nn).reshape(S, S, 3)[m_]
    L = np.array([-0.5, 0.8, 0.6]); L = L / np.linalg.norm(L)
    lam = np.clip(n[..., 0] * L[0] + n[..., 1] * L[1] + n[..., 2] * L[2], 0, 1)
    img = col * (0.42 + 0.78 * lam)[..., None]
    img[eye] = rgb(22, 20, 20); img[nostril] = rgb(52, 46, 46)
    if debug: Image.fromarray(np.uint8(np.clip(img, 0, 1) * 255 * shape[..., None])).save(debug)
    return finish_px(img, shape.astype(float))

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
