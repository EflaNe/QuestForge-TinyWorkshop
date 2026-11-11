"""Tiny Workshop main-game redesign: cuts the ReDesign reference sheets into clean, aligned sprites.

Sources (Assets/_TinyWorkshop/Art/ReDesign):
  main_game_target_hd.png  - target screenshot (backdrop is inpainted from it)
  *-2.png robot idle/walk sheet, *-3.png machine + terminal idle sheet, *-4.png HUD kit
Output: Assets/_TinyWorkshop/Art/Main (world sprites, 100 PPU) and Art/Main/UI (HUD pieces)
        plus Art/Main/layout.json (anchor offsets the scene builder reads).
Run:  python Tools/redesign_assets.py   (needs numpy, scipy, Pillow)
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1] / 'Assets/_TinyWorkshop/Art'
SRC = ROOT / 'ReDesign'
OUT = ROOT / 'Main'
UI = OUT / 'UI'
OUT.mkdir(exist_ok=True)
UI.mkdir(exist_ok=True)
PPU = 100                      # world sprites: 100 px per unit
REF_PPU = 1792 / 20            # target screenshot: 1792 px == 20 world units
layout = {}


def src(pattern):
    return np.array(Image.open(next(SRC.glob(pattern))).convert('RGBA'))


def save(arr, path):
    Image.fromarray(arr.astype(np.uint8), 'RGBA').save(path)


def resize(arr, scale=None, size=None):
    im = Image.fromarray(arr.astype(np.uint8), 'RGBA').convert('RGBa')
    if size is None:
        size = (max(1, round(arr.shape[1] * scale)), max(1, round(arr.shape[0] * scale)))
    return np.array(im.resize(size, Image.LANCZOS).convert('RGBA'))


def label(mask):
    lab, n = ndimage.label(mask)
    return lab, n


def keep(arr, mask):
    out = arr.copy()
    out[..., 3] = np.where(mask, out[..., 3], 0)
    return out


def bbox(mask):
    ys, xs = np.nonzero(mask)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def erase(arr, box, color=None):
    """Fill a rectangle on a flat panel with the median colour of its border ring."""
    x0, y0, x1, y1 = box
    if color is None:
        ring = np.concatenate([arr[y0, x0:x1], arr[y1 - 1, x0:x1], arr[y0:y1, x0], arr[y0:y1, x1 - 1]])
        ring = ring[ring[:, 3] > 200]
        color = np.median(ring, axis=0)
    arr[y0:y1, x0:x1, :3] = np.array(color)[:3]
    arr[y0:y1, x0:x1, 3] = 255


def flood_extract(arr, is_bg):
    """Remove background reachable from the crop border (stops at the dark outline)."""
    bg = is_bg(arr)
    lab, _ = label(bg)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    outside = np.isin(lab, list(border))
    fg = ~outside & (arr[..., 3] > 0)
    lab2, n = label(fg)
    if n:
        sizes = ndimage.sum(fg, lab2, range(1, n + 1))
        fg = lab2 == (np.argmax(sizes) + 1)
    fg = ndimage.binary_fill_holes(fg)
    out = keep(arr, fg)
    x0, y0, x1, y1 = bbox(fg)
    return out[y0:y1, x0:x1]


def lum(a):
    return a[..., 0] * .3 + a[..., 1] * .59 + a[..., 2] * .11


def trim(arr, pad=2):
    x0, y0, x1, y1 = bbox(arr[..., 3] > 8)
    arr = arr[y0:y1, x0:x1]
    return np.pad(arr, ((pad, pad), (pad, pad), (0, 0)))


# ---------------------------------------------------------------- robot (idle row + walk row)
def robot():
    sheet = src('*00_53_33-2.png')
    cols = [0, 209, 418, 627, 836, 1045, 1254]
    rows = [(313, 624), (626, 938)]
    frames = []
    for r, (y0, y1) in enumerate(rows):
        for c in range(6):
            cell = sheet[y0 + 6:y1 - 6, cols[c] + 6:cols[c + 1] - 6]
            mask = cell[..., 3] > 128
            lab, n = label(mask)
            sizes = ndimage.sum(mask, lab, range(1, n + 1))
            body = ndimage.binary_dilation(lab == (np.argmax(sizes) + 1), iterations=1)
            x0, y0b, x1, y1b = bbox(body)
            h = y1b - y0b
            head = body[y0b + int(h * .22):y0b + int(h * .48)]
            hx = np.nonzero(head.any(axis=0))[0]
            frames.append((keep(cell, body), (hx.min() + hx.max()) / 2, y1b, h))
    cw, ch = 190, 232
    scale = 130 / np.median([f[3] for f in frames])     # robot ~1.3 world units tall
    names = [f'robot_idle_{i}' for i in range(6)] + [f'robot_walk_{i}' for i in range(6)]
    for name, (img, cx, bottom, _) in zip(names, frames):
        canvas = np.zeros((ch, cw, 4), np.uint8)
        ox, oy = int(round(cw / 2 - cx)), ch - bottom
        h, w = img.shape[:2]
        sx0, sy0 = max(0, -ox), max(0, -oy)
        dx0, dy0 = max(0, ox), max(0, oy)
        ww, hh = min(w - sx0, cw - dx0), min(h - sy0, ch - dy0)
        region = img[sy0:sy0 + hh, sx0:sx0 + ww]
        dst = canvas[dy0:dy0 + hh, dx0:dx0 + ww]
        np.copyto(dst, region, where=(region[..., 3:4] > 0))
        save(resize(canvas, scale), OUT / f'{name}.png')
    layout['robotHeight'] = 130 / PPU


# ---------------------------------------------------------------- machine + terminal idle sheet
def machine_and_terminal():
    sheet = src('*00_53_33-3.png')
    cols = [0, 365, 725, 1085, 1448]
    # Production machine (row 1)
    raw = []
    for c in range(4):
        cell = sheet[90:500, cols[c]:cols[c + 1]].copy()
        mask = cell[..., 3] > 128
        lab, n = label(mask)
        sizes = ndimage.sum(mask, lab, range(1, n + 1))
        bi = np.argmax(sizes) + 1
        body = lab == bi
        bx0, by0, bx1, by1 = bbox(body)
        keepm = body.copy()
        sparks = np.zeros_like(body)
        for i in range(1, n + 1):
            if i == bi or sizes[i - 1] < 25:
                continue
            comp = lab == i
            cx0, cy0, cx1, cy1 = bbox(comp)
            ccx, ccy = (cx0 + cx1) / 2, (cy0 + cy1) / 2
            # "!!" marks right of the button are replaced by a code-driven "ready" spark
            if ccx > bx0 + (bx1 - bx0) * .62 and ccy > by0 + (by1 - by0) * .12:
                sparks |= comp
            else:
                keepm |= comp
        keepm = ndimage.binary_dilation(keepm, iterations=1) & (cell[..., 3] > 0)
        base = body[by1 - 30:by1]
        left = np.nonzero(base.any(axis=0))[0].min()
        raw.append((cell, keepm, sparks, left, by1, bx1 - bx0))
    cw, ch = 352, 400
    width = np.median([r[5] for r in raw])
    scale = 460 / width                                 # body 4.6 world units wide
    frames = []
    offsets = []
    for cell, keepm, sparks, left, bottom, _ in raw:
        offsets.append((2 - left, ch - bottom))
        canvas = np.zeros((ch, cw, 4), np.uint8)
        spark = np.zeros((ch, cw, 4), np.uint8)
        ox, oy = 2 - left, ch - bottom
        img = keep(cell, keepm)
        simg = keep(cell, sparks)
        h, w = img.shape[:2]
        sy0 = max(0, -oy)
        hh = min(h - sy0, ch - (oy + sy0))
        ww = min(w - max(0, -ox), cw - max(0, ox))
        canvas[oy + sy0:oy + sy0 + hh, max(0, ox):max(0, ox) + ww] = img[sy0:sy0 + hh, max(0, -ox):max(0, -ox) + ww]
        spark[oy + sy0:oy + sy0 + hh, max(0, ox):max(0, ox) + ww] = simg[sy0:sy0 + hh, max(0, -ox):max(0, -ox) + ww]
        frames.append((canvas, spark))
    # Button cap (red cylinder) measured on frame 0 of the sheet: top ellipse + side + bottom ellipse.
    f0 = frames[0][0].astype(int)
    ox, oy = offsets[0]
    yy, xx = np.mgrid[0:ch, 0:cw]
    sx, sy = xx - ox, yy - oy + 90          # canvas -> sheet coordinates
    top = ((sx - 210.5) / 38.5) ** 2 + ((sy - 293) / 19) ** 2 <= 1
    side = (sx >= 172) & (sx <= 249) & (sy >= 293) & (sy <= 322)
    bottom_e = ((sx - 210.5) / 38.5) ** 2 + ((sy - 322) / 15) ** 2 <= 1
    cap = (top | side | bottom_e) & (f0[..., 3] > 128)
    # only the red cap and its dark outline (no gold ring, no cream panel)
    cap &= (f0[..., 0] - f0[..., 1] > 55) | (lum(f0) < 85)
    lab, n = label(cap)
    sizes = ndimage.sum(cap, lab, range(1, n + 1))
    cap = ndimage.binary_fill_holes(lab == (np.argmax(sizes) + 1))
    cx0, cy0, cx1, cy1 = bbox(cap)
    cap_img = keep(frames[0][0], cap)[cy0:cy1, cx0:cx1]
    socket = np.array([46, 34, 30, 255])
    hole = ndimage.binary_dilation(cap, iterations=1)
    for canvas, _ in frames:
        under = hole & (canvas[..., 3] > 0)
        canvas[under] = socket
        # soft inner shading so the empty socket reads as depth
        inner = ndimage.binary_erosion(hole, iterations=4) & under
        canvas[inner] = [70, 50, 40, 255]
    s = scale
    for i, (canvas, spark) in enumerate(frames):
        save(resize(canvas, s), OUT / f'machine_{i}.png')
    save(resize(trim(frames[0][1], 3), s), OUT / 'machine_spark.png')
    save(resize(np.pad(cap_img, ((2, 2), (2, 2), (0, 0))), s), OUT / 'machine_cap.png')

    def world(px, py):          # canvas px -> offset from bottom-centre pivot, world units
        return [round((px - cw / 2) * s / PPU, 3), round((ch - py) * s / PPU, 3)]
    sx0, sy0, sx1, sy1 = bbox(frames[0][1][..., 3] > 8)
    layout['machineCap'] = world((cx0 + cx1) / 2, cy1 + 2)     # pivot: bottom-centre of cap
    layout['machineSpark'] = world((sx0 + sx1) / 2, (sy0 + sy1) / 2)
    lamps = []
    for cond in [lambda a: (a[..., 1] > 150) & (a[..., 0] < 140) & (a[..., 2] < 120),
                 lambda a: (a[..., 0] > 200) & (a[..., 1] > 170) & (a[..., 2] < 110),
                 lambda a: (a[..., 0] > 170) & (a[..., 1] < 110) & (a[..., 2] < 100)]:
        m = cond(f0) & (f0[..., 3] > 128)
        m[ch // 2:] = False
        m[:, int(cw * .55):] = False
        lab, n = label(m)
        sizes = ndimage.sum(m, lab, range(1, n + 1))
        lx0, ly0, lx1, ly1 = bbox(lab == (np.argmax(sizes) + 1))
        lamps.append(world((lx0 + lx1) / 2, (ly0 + ly1) / 2))
    layout['machineLamps'] = lamps
    body0 = frames[0][0][..., 3] > 128
    low = body0[int(ch * .78):]
    right = np.nonzero(low.any(axis=0))[0].max()
    col = np.nonzero(body0[:, right - 12])[0]
    layout['machineOutlet'] = world(right - 10, col.min() - 10)
    bx0, by0, bx1, by1 = bbox(body0)
    layout['machineSize'] = [round((bx1 - bx0) * s / PPU, 3), round((by1 - by0) * s / PPU, 3)]
    layout['machineBodyTop'] = world(0, by0)[1]

    # Delivery terminal (row 2): keep everything (beacon pulse marks are the animation)
    raw = []
    for c in range(4):
        cell = sheet[600:930, cols[c]:cols[c + 1]].copy()
        mask = cell[..., 3] > 128
        lab, n = label(mask)
        sizes = ndimage.sum(mask, lab, range(1, n + 1))
        keepm = np.isin(lab, [i + 1 for i in range(n) if sizes[i] >= 25])
        keepm = ndimage.binary_dilation(keepm, iterations=1) & (cell[..., 3] > 0)
        body = lab == (np.argmax(sizes) + 1)
        x0, y0, x1, y1 = bbox(body)
        raw.append((keep(cell, keepm), x0, y1, x1 - x0))
    cw, ch = 340, 300
    s = 330 / np.median([r[3] for r in raw])
    for i, (img, left, bottom, _) in enumerate(raw):
        canvas = np.zeros((ch, cw, 4), np.uint8)
        ox, oy = 4 - left, ch - bottom
        h, w = img.shape[:2]
        sy0 = max(0, -oy)
        hh = min(h - sy0, ch - (oy + sy0))
        ww = min(w - max(0, -ox), cw - max(0, ox))
        canvas[oy + sy0:oy + sy0 + hh, max(0, ox):max(0, ox) + ww] = img[sy0:sy0 + hh, max(0, -ox):max(0, -ox) + ww]
        save(resize(canvas, s), OUT / f'terminal_{i}.png')
    layout['terminalSize'] = [round(cw * s / PPU, 3), round(ch * s / PPU, 3)]


# ---------------------------------------------------------------- backdrop
def harmonic_fill(img, mask, iters=(1500, 400, 150)):
    """Inpaint masked pixels with a smooth membrane (Laplace) solved coarse-to-fine; no streaks."""
    out = img[..., :3].astype(np.float32)
    # the dark floor seam must not bleed upward: hide it from the solver under masked columns
    cols = mask.any(axis=0)
    out[790:, cols] = out[786, 1000]
    for level, n in zip((4, 2, 1), iters):
        h, w = out.shape[0] // level, out.shape[1] // level
        small = np.array(Image.fromarray(out.clip(0, 255).astype(np.uint8)).resize((w, h), Image.BILINEAR), np.float32)
        m = np.array(Image.fromarray((mask * 255).astype(np.uint8)).resize((w, h), Image.NEAREST)) > 0
        m = ndimage.binary_dilation(m, iterations=1)
        if level == 4:
            small[m] = small[~m].mean(axis=0)
        else:
            prev = np.array(Image.fromarray(out.clip(0, 255).astype(np.uint8)).resize((w, h), Image.BILINEAR), np.float32)
            small[m] = prev[m]
        for _ in range(n):
            avg = (np.roll(small, 1, 0) + np.roll(small, -1, 0) + np.roll(small, 1, 1) + np.roll(small, -1, 1)) / 4
            small[m] = avg[m]
        up = np.array(Image.fromarray(small.clip(0, 255).astype(np.uint8)).resize((out.shape[1], out.shape[0]), Image.BILINEAR), np.float32)
        full_m = np.array(Image.fromarray(m.astype(np.uint8) * 255).resize((out.shape[1], out.shape[0]), Image.NEAREST)) > 0
        sel = full_m & mask
        out[sel] = up[sel]
    rng = np.random.default_rng(7)
    out[mask] += rng.normal(0, 2.0, out[mask].shape)
    img[mask, :3] = out[mask].clip(0, 255).astype(np.uint8)


def backdrop():
    img = np.array(Image.open(SRC / 'main_game_target_hd.png').convert('RGBA')).astype(np.uint8)
    H, W = img.shape[:2]
    # HUD baked into the screenshot: replicate a clean ceiling column under the top HUD
    clean = img[:132, 468:472].copy()
    for x0, x1, y1 in [(0, 410, 132), (520, 1185, 100), (1290, 1792, 92)]:
        for x in range(x0, x1, 4):
            w = min(4, x1 - x)
            img[:y1, x:x + w] = clean[:y1, :w]
    # gameplay objects that become real sprites
    boxes = [(268, 278, 612, 500),            # machine top, lamps, steam, button sparks
             (200, 500, 612, 790),            # machine body
             (600, 575, 790, 790),            # pipe + conveyor + gears
             (66, 574, 205, 790),             # robot + heart bubble
             (622, 612, 896, 706),            # platform 1
             (796, 520, 1106, 612),           # platform 2
             (1046, 612, 1350, 706),          # platform 3
             (1314, 700, 1428, 790),          # chest
             (1498, 446, 1680, 487),          # beacon + truck bubble
             (1424, 486, 1680, 790)]          # terminal
    mask = np.zeros((H, W), bool)
    for x0, y0, x1, y1 in boxes:
        mask[y0:y1, x0:x1] = True
        if y1 >= 790:   # rebuild the baseboard strip under objects from a clean column
            img[786:793, x0:x1] = img[786:793, 1000:1001]
    harmonic_fill(img, mask)
    # structural pillars are vertically uniform: rebuild them through the patches
    img[278:790, 546:600] = img[262:263, 546:600]
    img[612:706, 1219:1274] = img[600:601, 1219:1274]
    # bottom bar of the screenshot: extend the steel base down
    img[902:] = img[896:897].repeat(H - 902, axis=0)
    out = resize(img, size=(2000, round(H * 2000 / W)))
    out[..., 3] = 255
    save(out, OUT / 'backdrop.png')
    layout['floorTopY'] = round((H / 2 - 794) / REF_PPU, 3)
    layout['backdropSize'] = [20, round(H / REF_PPU, 3)]


def ref_world(px, py, H=1007):
    return [round((px - 896) / REF_PPU, 3), round((H / 2 - py) / REF_PPU, 3)]


def props():
    img = np.array(Image.open(SRC / 'main_game_target_hd.png').convert('RGBA'))
    s = PPU / REF_PPU

    def teal_wall(a):
        a = a.astype(int)
        return ((a[..., 1] - a[..., 0] > 18) & (lum(a) > 95)) | (a[..., 3] < 10)
    plat = flood_extract(img[522:612, 798:1104].copy(), teal_wall)
    plat = resize(plat, s)
    save(plat, OUT / 'platform.png')
    wood = plat[..., 0].astype(int) - plat[..., 2] > 40
    rows = np.nonzero(wood.any(axis=1))[0]
    layout['platformTopFromBottom'] = round((plat.shape[0] - rows.min()) / PPU, 3)
    layout['platformSize'] = [round(plat.shape[1] / PPU, 3), round(plat.shape[0] / PPU, 3)]

    heart = flood_extract(img[588:680, 96:186].copy(), teal_wall)
    save(resize(heart, s * 1.0), OUT / 'bubble_heart.png')
    layout['lamps'] = [ref_world(313, 236), ref_world(903, 232), ref_world(1497, 236)]
    layout['refPlatforms'] = [ref_world(760, 640), ref_world(951, 548), ref_world(1197, 640)]


# ---------------------------------------------------------------- HUD kit
def ui():
    kit = src('*00_53_34-4.png')

    work = kit.copy()
    for e in [(178, 62, 538, 190),                       # objective card text + bar
              (612, 106, 806, 166),                      # "Stage 2/5"
              (203, 270, 360, 334), (583, 270, 724, 334),  # counter numbers
              (42, 922, 1406, 1006),                     # bottom bar content
              (543, 584, 748, 648), (760, 594, 946, 650),  # missions panel tabs
              (978, 594, 1168, 650), (1190, 584, 1408, 648),  # upgrades panel tabs
              (538, 668, 938, 832), (984, 668, 1404, 832),  # panel rows
              (1260, 692, 1320, 720)]:                  # Lv chip text
        erase(work, e)

    def piece(name, box, use_work=True, comp_only=True):
        a = (work if use_work else kit)[box[1]:box[3], box[0]:box[2]].copy()
        if comp_only:
            m = a[..., 3] > 128
            lab, n = label(m)
            sizes = ndimage.sum(m, lab, range(1, n + 1))
            m = ndimage.binary_fill_holes(lab == (np.argmax(sizes) + 1))
            m = ndimage.binary_dilation(m, iterations=1) & (a[..., 3] > 0)
            x0, y0, x1, y1 = bbox(m)
            a = keep(a, m)[y0:y1, x0:x1]
        save(a, UI / f'{name}.png')
        return a

    piece('card', (30, 36, 562, 218))
    piece('counter_coin', (76, 246, 452, 358))
    piece('counter_gear', (478, 244, 820, 358))
    piece('bottom_bar', (14, 900, 1432, 1030))
    piece('truck_bubble', (964, 232, 1132, 374), use_work=False)
    for i, n in enumerate(['home', 'build', 'box', 'settings']):
        piece(f'btn_{n}', (590 + i * 181, 390, 744 + i * 181, 536), use_work=False)
    # bottom-bar glyphs sit on the opaque bar: cut them out by flooding the bar colour
    keys = kit.copy()
    erase(keys, (1016, 947, 1042, 982))
    erase(keys, (826, 947, 904, 982))
    barcol = np.median(kit[924:932, 470:600, :3].reshape(-1, 3), axis=0)

    def on_bar(a):
        return (np.abs(a[..., :3].astype(int) - barcol).sum(axis=-1) < 70) | (a[..., 3] < 10)
    for name, box in {'logo_gear': (40, 926, 112, 1004), 'icon_mouse': (410, 934, 462, 996),
                      'keycap': (1002, 934, 1056, 994), 'keycap_wide': (812, 934, 918, 994)}.items():
        save(flood_extract(keys[box[1]:box[3], box[0]:box[2]].copy(), on_bar), UI / f'{name}.png')
    piece('btn_up', (1326, 676, 1394, 742), use_work=False)
    # stage track: label pill, plain bar strip, nodes, gift
    a = work[90:184, 583:833].copy()
    a[..., 3] = np.where(a[..., 3] > 0, a[..., 3], 0)
    save(trim(a, 0), UI / 'stage_label.png')
    strip = kit[100:182, 948:952].copy()
    save(np.repeat(strip[:, :1], 24, axis=1), UI / 'stage_track.png')
    for name, (cx, cy, r) in {'node_done': (894, 133, 37), 'node_current': (1005, 134, 43), 'node_empty': (1115, 133, 35)}.items():
        a = kit[cy - r:cy + r, cx - r:cx + r].copy()
        yy, xx = np.mgrid[-r:r, -r:r] + .5
        d = np.sqrt(xx ** 2 + yy ** 2)
        a[..., 3] = (a[..., 3] * np.clip(r - d, 0, 1)).astype(np.uint8)
        save(a, UI / f'{name}.png')
    gift = flood_extract(kit[70:192, 1262:1412].copy(), lambda a: (lum(a.astype(int)) < 205) | (a[..., 3] < 10))
    save(gift, UI / 'gift.png')
    # mission / upgrade windows (tabs drawn, text replaced by live labels)
    piece('window_missions', (516, 568, 958, 858))
    piece('window_upgrades', (964, 568, 1422, 858))
    row = kit.copy()
    erase(row, (553, 677, 925, 740))
    save(row[668:749, 540:937], UI / 'row.png')
    piece('lv_chip', (1254, 684, 1326, 726), comp_only=False)

    def icon(name, box):
        a = flood_extract(kit[box[1]:box[3], box[0]:box[2]].copy(),
                          lambda a: ((a[..., 0].astype(int) > 225) & (a[..., 1] > 200) & (a[..., 2] > 160)) | (a[..., 3] < 10))
        save(a, UI / f'{name}.png')
        return a
    icon('icon_crate', (548, 680, 610, 740))
    icon('icon_truck', (548, 760, 612, 812))
    g = icon('icon_gear', (994, 672, 1068, 744))
    icon('icon_box', (994, 756, 1066, 826))
    # gameplay gear sprite: same silver gear, world size ~0.62 units
    save(resize(g, 62 / max(g.shape[:2])), OUT / 'gear_part.png')
    # progress bars + plain window, drawn in the kit's style
    bar(UI / 'bar_track.png', '#2c3436', '#9aa29f', '#7f8885')
    bar(UI / 'bar_fill.png', '#2c3436', '#3fbe43', '#7fe06a')
    plain_window(UI / 'window_plain.png')
    glow(OUT / 'soft_glow.png')


def bar(path, outline, fill, hi):
    im = Image.new('RGBA', (64, 28), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, 63, 27), 13, fill=outline)
    d.rounded_rectangle((3, 3, 60, 24), 10, fill=fill)
    d.rounded_rectangle((6, 5, 57, 12), 4, fill=hi)
    im.save(path)


def plain_window(path):
    im = Image.new('RGBA', (160, 160), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 4, 159, 159), 26, fill='#1e2a2c')
    d.rounded_rectangle((4, 4, 155, 151), 23, fill='#e9c48f')
    d.rounded_rectangle((4, 4, 155, 145), 23, fill='#fbe6c3')
    d.rounded_rectangle((0, 0, 159, 155), 26, outline='#1e2a2c', width=4)
    im.save(path)


def glow(path):
    n = 128
    yy, xx = np.mgrid[0:n, 0:n] - (n - 1) / 2
    d = np.sqrt(xx ** 2 + yy ** 2) / (n / 2)
    a = np.clip(1 - d, 0, 1) ** 2
    arr = np.zeros((n, n, 4), np.uint8)
    arr[..., :3] = 255
    arr[..., 3] = (a * 255).astype(np.uint8)
    save(arr, path)


if __name__ == '__main__':
    robot()
    machine_and_terminal()
    backdrop()
    props()
    ui()
    def unity(v):   # JsonUtility-friendly: [x, y] -> {"x", "y"}, lists of those stay lists
        if isinstance(v, list) and len(v) == 2 and all(isinstance(n, (int, float)) for n in v):
            return {'x': float(v[0]), 'y': float(v[1])}
        if isinstance(v, list):
            return [unity(n) for n in v]
        return float(v)
    (OUT / 'layout.json').write_text(json.dumps({k: unity(v) for k, v in layout.items()}, indent=2))
    print(json.dumps(layout, indent=1))
