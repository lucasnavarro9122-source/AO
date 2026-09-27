"""Video sin Unity de las gotas V295 (AOMapWeatherDropsV295), con los números del C#: trazos con brillos de oscilación,
lluvia que brilla a contraluz de los faroles (y casi desaparece en la oscuridad), cortinas de lluvia, relámpago que
enciende todas las gotas, coronas en el piso y ondas en los charcos. Ullathorpe: de noche junto a los faroles y de día.

  python Tools/hd_remake/video_gotas_v295.py SALIDA.mp4 [--x 22 --y 12]"""
import argparse, json, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, str(Path(__file__).resolve().parent))
import video_luz_v292 as VL
import dungeon_vista_p1 as v
import preview_luces as pl
import luz_vista_v291 as L

FPS = 20
ROOT = Path(__file__).resolve().parents[2]


def streaks(T):
    """Los 4 trazos del C# (RainStreak): perfil fino, punta brillante, 2-3 brillos de oscilación y puntitos."""
    rnd = np.random.default_rng(295)
    out = []
    for _ in range(4):
        bands = [rnd.uniform(.1, .4), rnd.uniform(.4, .7), rnd.uniform(.7, .95)]
        along = np.linspace(1, 0, 64)[:, None]                  # fila 0 = cola (arriba)
        body = (1 - along) ** .8 * np.clip(along * 10 + .35, 0, 1)
        hl = .45 + sum(.55 * np.exp(-((along - b) / .05) ** 2) * (1 if i == 0 else .7) for i, b in enumerate(bands))
        hl = np.minimum(1.3, hl + (rnd.random((64, 1)) < .06) * .4)
        across = 1 - np.abs((np.arange(8) + .5) / 8 * 2 - 1)[None, :]
        out.append(np.clip(across ** 2.2 * body * hl, 0, 1))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('salida')
    ap.add_argument('--x', type=int, default=22)
    ap.add_argument('--y', type=int, default=12)
    args = ap.parse_args()
    pl.S, pl.HD, pl.T = VL.K, False, v.T
    m = pl.load(1)
    x0, y0, T, W, H = args.x, args.y, v.T, VL.W, VL.H
    rng = np.random.default_rng(8)
    to_px = lambda p: ((p[0] - (x0 - 1)) * T, (-(y0 - 1) - p[1]) * T)
    to_world = lambda px, py: np.array([px / T + (x0 - 1), -(py / T) - (y0 - 1)])
    gn, un = VL.layers(m, x0, y0, (120, 120, 120))
    gd, ud = VL.layers(m, x0, y0, (190, 190, 200))
    Hh, Ww = gn.shape[:2]
    lamps = []
    for l in m['lights']:
        r = (l['range'] - 99 if l['range'] >= 100 else l['range']) + .5
        p = np.array([l['x'] - .5, -l['y'] + .5])
        if x0 - 5 <= l['x'] < x0 + W + 5 and y0 - 5 <= l['y'] < y0 + H + 5:
            lamps.append((p, r, np.array([1, .82, .55]) * .55 + np.array([1, 1, 1]) * .45, l['x'] * 131 + l['y'] * 17))
    spots = next(mm['spots'] for mm in json.loads((ROOT / 'Assets/Resources/AOMigrator/WorldV07/puddle_spots.json').read_text('utf-8'))['maps'] if mm['map'] == 1)
    puddles = [(np.array([spots[i] - .5, -spots[i + 1] + .5]), spots[i + 2] / 100 * 1.3, spots[i + 2] / 100 * .7)
               for i in range(0, len(spots), 4) if x0 <= spots[i] < x0 + W and y0 <= spots[i + 1] < y0 + H]
    ST = streaks(T)
    specs = [(.5, 1.6 * .13, .3, 7, 9), (.75, 1.6 * .22, .55, 11, 14), (1.0, 1.6 * .32, .42, 16, 20)]
    drops = []
    for i in range(240):
        k = 0 if i % 20 < 9 else 1 if i % 20 < 17 else 2
        drops.append(dict(k=k, x=rng.uniform(0, Ww), y=rng.uniform(-Hh, Hh), land=rng.uniform(0, Hh),
                          sp=rng.uniform(*specs[k][3:5]), a=specs[k][2] * rng.uniform(.75, 1.1), v=int(rng.integers(0, 4))))
    splashes = []
    cache = {}
    frames = []
    dt = 1 / FPS
    glow_cache = {}

    def streak_img(var, spec):
        key = (var, spec)
        if key not in cache:
            wpx = max(2, int(round(8 / 64 * T * specs[spec][0])))
            hpx = max(4, int(round(specs[spec][1] * T)))
            cache[key] = np.asarray(Image.fromarray((ST[var] * 255).astype(np.uint8)).resize((wpx, hpx), Image.BILINEAR), np.float32) / 255
        return cache[key]

    crown = None

    for f in range(FPS * 12):
        t = f * dt
        night = t < 6.5
        daylight = 0.0 if night else 1.0
        base = (gn * (1 - un[..., 3:]) + un[..., :3]) if night else (gd * (1 - ud[..., 3:]) + ud[..., :3])
        img = base.copy()
        # Charcos (simple, para las ondas).
        for c, pw, ph in puddles:
            w_, h_ = int(pw * T), int(ph * T)
            yy, xx = np.mgrid[0:h_, 0:w_]
            a = np.clip((1 - np.hypot((xx + .5) / w_ * 2 - 1, (yy + .5) / h_ * 2 - 1) / .8) * 9, 0, 1)
            cx, cy = to_px(c)
            l_, tp = int(cx - w_ / 2), int(cy - h_ / 2)
            x1, y1, x2, y2 = max(0, l_), max(0, tp), min(Ww, l_ + w_), min(Hh, tp + h_)
            if x1 < x2 and y1 < y2:
                img[y1:y2, x1:x2] *= 1 + (np.array([.68, .72, .8]) - 1) * a[y1 - tp:y2 - tp, x1 - l_:x2 - l_, None] * .48
        darkness = .53 if night else 0
        if night:                                               # halos de faroles
            for p, r, col, seed in lamps:
                size = int(r * 2 * T) // 4 * 4
                glow_cache.setdefault(size, VL.radial(size) ** 3)
                q = to_px(p)
                VL.add_sprite(img, glow_cache[size], q[0], q[1], col, .24 * darkness * VL.flicker(seed, t))
            ys = np.arange(Hh)[:, None]
            img *= 1 + (np.array([.6, .68, .95])[None, None, :] - 1) * (.42 * (.55 + .45 * (1 - ys / Hh)))[..., None]
        img *= 1 + (np.array([.42, .48, .6]) - 1) * .2
        # Relámpago a los 4 s (de noche).
        tt = t - 4.0
        flash = 0.0
        if 0 <= tt < .9:
            flash = min(1, (math.exp(-tt / .06) + (.7 * math.exp(-(tt - .16) / .1) if tt > .16 else 0)) * .9)
        ambient = .42 + .58 * daylight
        for dr in drops:
            sp = specs[dr['k']]
            dr['y'] += dr['sp'] * T * dt
            if dr['k'] == 1 and dr['y'] >= dr['land']:
                wp = to_world(dr['x'], dr['land'])
                in_p = any((((wp - c) / (np.array([pw, ph]) * .5)) ** 2).sum() < .8 for c, pw, ph in puddles)
                splashes.append([dr['x'], dr['land'], 0.0, 1.5 if in_p else 1.0])
                dr.update(y=rng.uniform(-Hh * .3, 0), x=rng.uniform(0, Ww), land=rng.uniform(0, Hh), v=int(rng.integers(0, 4)))
            elif dr['y'] > Hh + T:
                dr.update(y=rng.uniform(-Hh * .3, 0), x=rng.uniform(0, Ww), v=int(rng.integers(0, 4)))
            wp = to_world(dr['x'], dr['y'])
            color = np.array([.86, .9, 1]) * ambient
            alpha = dr['a']
            best, lampc = 0, None
            for p, r, col, seed in lamps:
                d = np.linalg.norm(wp - p)
                if d < r + 1:
                    fl = 1 - d / (r + 1)
                    if fl > best:
                        best, lampc = fl, col * VL.flicker(seed, t)
            lit = best * (.35 + darkness * 1.6)
            if lit > .01:
                color = color + (lampc - color) * min(1, lit)
                alpha *= 1 + 2.5 * lit
            if flash > .01:
                color = color + (np.array([.85, .9, 1]) - color) * flash
                alpha *= 1 + 2 * flash
            curtain = L.perlin(np.array([wp[0] * .07 + t * .3]), np.array([wp[1] * .05 - t * .1]))[0]
            alpha *= .7 + .6 * curtain
            s = streak_img(dr['v'], dr['k'])
            h_, w_ = s.shape
            left, top = int(dr['x'] - w_ / 2), int(dr['y'] - h_ * .9)
            x1, y1, x2, y2 = max(0, left), max(0, top), min(Ww, left + w_), min(Hh, top + h_)
            if x1 < x2 and y1 < y2:
                img[y1:y2, x1:x2] += s[y1 - top:y2 - top, x1 - left:x2 - left, None] * color * min(alpha, 1) * .9
        for sp_ in splashes:
            sp_[2] += dt
            k = sp_[2] / .28
            if k >= 1:
                continue
            ripple = sp_[3] >= 1.4
            sc = (.12 + k * .28) * (.7 + .3 * sp_[3]) * (1.25 if ripple else 1)
            r = max(3, int(sc * T / 2))
            yy, xx = np.mgrid[-r // 2:r // 2 + 1, -r:r + 1]
            rr = np.hypot(xx / r, yy / max(r // 2, 1))
            if ripple:
                shape = np.clip(1 - np.abs(rr - .85) * 9, 0, 1) + .6 * np.clip(1 - np.abs(rr - .5) * 10, 0, 1)
            else:
                ang = np.arctan2(-yy, xx)
                spikes = (yy < 0) * np.clip(np.cos(ang * 6) * 1.4 - .4, 0, 1) * np.clip(1 - np.abs(rr - .82) * 6, 0, 1)
                shape = np.clip(np.clip(1 - np.abs(rr - .62) * 7, 0, 1) + spikes * .8 + np.clip(.3 - rr, 0, 1) * 2, 0, 1)
            impact = 1.5 if (not ripple and k < .15) else 1
            col = np.array([.86, .9, 1]) * ambient
            wp = to_world(sp_[0], sp_[1])
            for p, rad, c_, seed in lamps:
                d = np.linalg.norm(wp - p)
                if d < rad + 1:
                    col = col + (c_ - col) * min(1, (1 - d / (rad + 1)) * (.35 + darkness * 1.6))
            VL.add_sprite(img, np.clip(shape, 0, 1), sp_[0], sp_[1], col, .38 * min(sp_[3], 1.2) * (1 - k) * impact)
        splashes[:] = [s_ for s_ in splashes if s_[2] < .28]
        if flash > .01:
            img = img + np.array([.72, .8, 1]) * flash * .42
        out = v.to_img(img)
        d = ImageDraw.Draw(out)
        d.rectangle((0, 0, out.width, 26), fill=(0, 0, 0))
        label = ('Noche: la lluvia casi no se ve en lo oscuro y brilla a contraluz de los faroles' if t < 4 else
                 'Relámpago: todas las gotas se encienden un instante' if t < 6.5 else
                 'Día: trazos con brillos de oscilación, cortinas de lluvia, coronas y ondas en los charcos')
        d.text((10, 7), label, fill=(255, 255, 255))
        frames.append(out)
    import imageio.v2 as imageio
    wr = imageio.get_writer(args.salida, fps=FPS, codec='libx264', quality=8, macro_block_size=8)
    for fr in frames:
        wr.append_data(np.asarray(fr))
    wr.close()
    frames[FPS * 2].save(Path(args.salida).with_suffix('.jpg'), quality=92)
    print(args.salida, frames[0].size, len(frames), 'cuadros', len(lamps), 'faroles', len(puddles), 'charcos')


if __name__ == '__main__':
    main()
