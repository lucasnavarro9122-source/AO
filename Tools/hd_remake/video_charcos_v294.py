"""Video sin Unity de los charcos V294 (AOMapWeatherPuddlesV294), de cerca y con las mismas capas del C#:
tierra húmeda alrededor, agua casi transparente (se ve el piso), borde de cielo, brillos que titilan, ondas de lluvia,
y un personaje que cruza el charco (ondas desde los pies, el agua se corre y vuelve, el reflejo se desarma) y se queda
parado encima hasta que el reflejo vuelve a formarse. Charcos debajo de árboles y personajes, como en el juego.

  python Tools/hd_remake/video_charcos_v294.py SALIDA.mp4 [--mapa 1 --x 28 --y 16]"""
import argparse, json, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, str(Path(__file__).resolve().parent))
import video_luz_v292 as VL
import video_bosque_v293 as VB
import dungeon_vista_p1 as v
import preview_luces as pl
import luz_vista_v291 as L

K = 3                                     # 96 px por casilla: de cerca
FPS, SECONDS = 20, 11


def puddle_shape(w, h, seed):
    yy, xx = np.mgrid[0:h, 0:w]
    dx, dy = (xx + .5) / w * 2 - 1, (yy + .5) / h * 2 - 1
    ang = np.arctan2(dy, dx)
    edge = .7 + .16 * L.perlin(np.cos(ang) * 1.6 + 3 + seed * 11, np.sin(ang) * 1.6 + 7) + \
        .08 * L.perlin(np.cos(ang) * 4.2 + 9 + seed * 5, np.sin(ang) * 4.2 + 1)
    r = np.hypot(dx, dy) / edge
    return np.clip((1 - r) * 9, 0, 1), r, dy


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('salida')
    ap.add_argument('--mapa', type=int, default=1)
    ap.add_argument('--x', type=int, default=28)
    ap.add_argument('--y', type=int, default=16)
    args = ap.parse_args()
    v.K, v.T = K, 32 * K
    VL.K, VL.W, VL.H = K, 9, 6
    pl.S, pl.HD, pl.T = K, False, v.T
    m = VB.with_trees(pl.load(args.mapa))
    T, W, H = v.T, VL.W, VL.H
    rng = np.random.default_rng(3)
    # Un charco de Tools/puddle_spots.py (camino de tierra), el más grande cerca de --x --y.
    spots = next(mm['spots'] for mm in json.loads((Path(__file__).resolve().parents[2] /
                 'Assets/Resources/AOMigrator/WorldV07/puddle_spots.json').read_text('utf-8'))['maps'] if mm['map'] == args.mapa)
    cands = [(spots[i], spots[i + 1], spots[i + 2] / 100, spots[i + 3]) for i in range(0, len(spots), 4)]
    px_, py_, size, shape = max(cands, key=lambda c: c[2] - (abs(c[0] - args.x) + abs(c[1] - args.y)) * .1)
    x0, y0 = px_ - W // 2, py_ - H // 2
    g, u = VL.layers(m, x0, y0, (185, 185, 195))
    Hh, Ww = g.shape[:2]
    to_px = lambda p: ((p[0] - (x0 - 1)) * T, (-(y0 - 1) - p[1]) * T)
    center = np.array([px_ - .5, -py_ + .5])
    pw, ph = size * 1.3 * 1.25, size * .7 * 1.25        # algo más grande que el mínimo, para verlo de cerca
    walker = np.asarray(next(pl.npc_img(n) for n in pl.load(1)['npcs'] if n['name'].startswith('Trevor')), np.float32) / 255
    shape_w, shape_h = int(pw * T), int(ph * T)
    alpha_mask, radius, dyn = puddle_shape(shape_w, shape_h, shape)
    damp_mask, _, _ = puddle_shape(int(shape_w * 1.45), int(shape_h * 1.5), shape)
    rim = np.clip(1 - np.abs(radius - .82) * 6, 0, 1) * (.25 + .75 * (1 - (dyn + 1) / 2))
    offset, velocity, disturb, last_feet, step_at = np.zeros(2), np.zeros(2), 0.0, None, 0.0
    drops = [dict(k=0 if i % 20 < 9 else 1 if i % 20 < 17 else 2, x=rng.uniform(0, Ww), y=rng.uniform(-Hh, Hh),
                  land=rng.uniform(0, Hh)) for i in range(120)]
    specs = [(.5, 1.6 * .13, .3, 7, 9), (.75, 1.6 * .22, .55, 11, 14), (1.0, 1.6 * .32, .42, 16, 20)]
    for dr in drops:
        dr['sp'] = rng.uniform(*specs[dr['k']][3:5])
    rings = []
    frames = []
    dt = 1 / FPS

    def blend_mask(img, mask, cx, cy, fn):
        h, w = mask.shape
        left, top = int(cx - w / 2), int(cy - h / 2)
        x1, y1, x2, y2 = max(0, left), max(0, top), min(Ww, left + w), min(Hh, top + h)
        if x1 < x2 and y1 < y2:
            a = mask[y1 - top:y2 - top, x1 - left:x2 - left, None]
            img[y1:y2, x1:x2] = fn(img[y1:y2, x1:x2], a, (y1 - top, y2 - top, x1 - left, x2 - left))
        return (left, top)

    for f in range(FPS * SECONDS):
        t = f * dt
        wet = min(1, .6 + t / 8)
        # Personaje: entra por la izquierda, cruza el charco (2-5 s) y se queda parado en el borde de arriba.
        k = min(max((t - 1.5) / 3.5, 0), 1)
        feet = np.array([center[0] - 2.8 + k * 2.8 + (0 if t < 5 else 0), center[1] + ph * .28])
        if t >= 5:
            feet = np.array([center[0], center[1] + ph * .28])
        # Pisadas: agitación, empuje hacia donde va y ondas desde los pies.
        inside = (((feet - center) / (np.array([pw, ph]) * .5)) ** 2).sum() < 1
        if inside and last_feet is not None:
            step = feet - last_feet
            if (step ** 2).sum() > 1e-4:
                disturb = 1.0
                velocity += step / np.linalg.norm(step) * 3
                if t >= step_at:
                    step_at = t + .22
                    rings.append([feet.copy(), 0.0, 1.9])
        last_feet = feet.copy()
        velocity += (-30 * offset - 5 * velocity) * dt
        offset += velocity * dt
        disturb = max(0.0, disturb - dt / 1.6)
        stretch = 1 + min(.2, np.linalg.norm(offset) * .15)
        pc = center + offset * .14

        img = g.copy()
        cx, cy = to_px(center)
        # Tierra húmeda alrededor (más oscura, un poco más saturada).
        blend_mask(img, damp_mask, cx, cy, lambda px, a, _: px * (1 + (np.array([.78, .74, .7]) - 1) * a * .32 * wet))
        # Agua: se estira y se corre con el resorte.
        wm = np.asarray(Image.fromarray((alpha_mask * 255).astype(np.uint8)).resize(
            (max(4, int(shape_w * stretch)), max(4, int(shape_h / math.sqrt(stretch)))), Image.BILINEAR), np.float32) / 255
        rm = np.asarray(Image.fromarray((rim * 255).astype(np.uint8)).resize(wm.shape[::-1], Image.BILINEAR), np.float32) / 255
        wx, wy = to_px(pc)
        blend_mask(img, wm, wx, wy, lambda px, a, _: px * (1 + (np.array([.68, .72, .8]) - 1) * a * .48 * wet))
        blend_mask(img, rm, wx, wy, lambda px, a, _: px + np.array([.55, .62, .78]) * a * .16 * wet * (1 - .5 * disturb))
        # Reflejo del personaje: dado vuelta desde los pies, solo dentro del agua; se ondula si está agitada.
        fp = to_px(feet)
        hh, ww = walker.shape[:2]
        wob = math.sin(t * 16) * .06 * disturb * T
        sy = 1 - .2 * disturb * abs(math.sin(t * 7))
        refl = Image.fromarray((walker * 255).astype(np.uint8)).transpose(Image.FLIP_TOP_BOTTOM).resize((ww, max(1, int(hh * sy))))
        ra = np.asarray(refl, np.float32) / 255
        if disturb > .05:                                     # el agua agitada desarma el reflejo en franjas
            rows = np.arange(ra.shape[0])[:, None]
            shift = (np.sin(rows * .35 + t * 20) * 3 * disturb).astype(int)
            for r_ in range(ra.shape[0]):
                ra[r_] = np.roll(ra[r_], shift[r_, 0], axis=0)
        full = np.zeros((Hh, Ww, 4), np.float32)
        lx, ty = int(fp[0] - ww / 2 + wob), int(fp[1])
        x1, y1, x2, y2 = max(0, lx), max(0, ty), min(Ww, lx + ww), min(Hh, ty + ra.shape[0])
        if x1 < x2 and y1 < y2:
            full[y1:y2, x1:x2] = ra[y1 - ty:y2 - ty, x1 - lx:x2 - lx]
        water_full = np.zeros((Hh, Ww, 1), np.float32)
        blend_mask(water_full, wm, wx, wy, lambda px, a, _: np.maximum(px, (a > .35).astype(np.float32)))
        a_ref = full[..., 3:] * water_full * .55 * wet * (1 - .55 * disturb)
        img = img * (1 - a_ref) + full[..., :3] * np.array([.62, .68, .8]) * a_ref
        # Brillos que titilan en el agua.
        for gi in range(3):
            n = L.perlin(np.array([t * 2.3 + gi * 1.7]), np.array([gi * .37]))[0]
            a = max(0, (n - .55) * 3.5) * (.25 + .35 * max(disturb, 1)) * wet
            if a > .02:
                gx = (L.perlin(np.array([gi * .9]), np.array([t * .2]))[0] - .5) * pw * .7 * T
                gy = (L.perlin(np.array([t * .2]), np.array([gi * .9]))[0] - .5) * ph * .6 * T
                VL.add_sprite(img, VL.radial(int(.09 * T)) ** 2, wx + gx, wy + gy, (.9, .95, 1), a)
        # Personaje (con su sombra de contacto) y lo que está arriba (árboles, objetos).
        L.contact(img, fp, T)
        v._paste(img, walker[..., :3], walker[..., 3:], int(fp[0] - ww / 2), int(fp[1] - hh))
        img = img * (1 - u[..., 3:]) + u[..., :3]
        img *= 1 + (np.array([.42, .48, .6]) - 1) * .2
        # Lluvia (vertical, a escala del personaje) y ondas.
        for dr in drops:
            sp = specs[dr['k']]
            dr['y'] += dr['sp'] * T * dt
            if dr['k'] == 1 and dr['y'] >= dr['land']:
                wp = np.array([dr['x'] / T + (x0 - 1), -(dr['land'] / T) - (y0 - 1)])
                in_p = (((wp - pc) / (np.array([pw, ph]) * .5)) ** 2).sum() < .8
                rings.append([wp, 0.0, 1.5 if in_p else 1.0])
                dr['y'], dr['x'], dr['land'] = rng.uniform(-Hh * .3, 0), rng.uniform(0, Ww), rng.uniform(0, Hh)
            elif dr['y'] > Hh + T:
                dr['y'], dr['x'] = rng.uniform(-Hh * .3, 0), rng.uniform(0, Ww)
            ln, wd = int(sp[1] * T), max(1, int(round(sp[0] * 2 * K)))
            x, y1_ = int(dr['x']), int(dr['y'])
            for j in range(max(0, y1_ - ln), min(Hh, y1_)):
                img[j, max(0, x):min(Ww, x + wd)] += np.array([.86, .9, 1]) * sp[2] * ((j - (y1_ - ln)) / max(1, ln)) * .8
        for rg in rings:
            rg[1] += dt
            kk = rg[1] / .28
            if kk < 1:
                r = max(2, int((.12 + kk * .28) * (.7 + .3 * rg[2]) * T / 2))
                yy, xx = np.ogrid[-r // 2:r // 2 + 1, -r:r + 1]
                ring = np.clip(1 - np.abs(np.hypot(xx / r, yy / max(r // 2, 1)) - .75) * 5, 0, 1)
                p_ = to_px(rg[0])
                VL.add_sprite(img, ring, p_[0], p_[1], (.85, .9, 1), .38 * min(rg[2], 1.2) * (1 - kk))
        rings[:] = [rg for rg in rings if rg[1] < .28]
        out = v.to_img(img)
        d = ImageDraw.Draw(out)
        d.rectangle((0, 0, out.width, 26), fill=(0, 0, 0))
        label = ('Charco: tierra húmeda, agua casi transparente, borde de cielo, brillos y ondas de lluvia' if t < 1.5 else
                 'Lo pisa: ondas desde los pies, el agua se corre y el reflejo se desarma' if t < 5.5 else
                 'Se queda encima: el agua se calma y el reflejo vuelve a tomar forma')
        d.text((10, 7), label, fill=(255, 255, 255))
        frames.append(out)
    import imageio.v2 as imageio
    wr = imageio.get_writer(args.salida, fps=FPS, codec='libx264', quality=8, macro_block_size=8)
    for fr in frames:
        wr.append_data(np.asarray(fr))
    wr.close()
    frames[int(FPS * 9)].save(Path(args.salida).with_suffix('.jpg'), quality=92)
    print(args.salida, frames[0].size, len(frames), 'cuadros; charco en', px_, py_)


if __name__ == '__main__':
    main()
