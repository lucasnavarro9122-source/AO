"""Video sin Unity en un bosque del AO (mapa 6), con las reglas del C#:
1) de noche, luciérnagas con mente colmena (AOFirefliesV292): el enjambre recorre lo oscuro, se abre al instante
   cuando pasa un personaje (alarma de todas), escapadas individuales y una exploradora;
2) lluvia (AOMapWeatherNatureV293): gotas que se deslizan entre las hojas y gotean desde la copa (debajo llueve
   menos), charcos que se forman con brillo de cielo y ondas, reflejo del personaje en el charco, gotas fluidas.

  python Tools/hd_remake/video_bosque_v293.py SALIDA.mp4 [--mapa 6] [--x 38 --y 72]"""
import argparse, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, str(Path(__file__).resolve().parent))
import video_luz_v292 as VL
import dungeon_vista_p1 as v
import preview_luces as pl
import luz_vista_v291 as L

FPS = 15


def with_trees(m):
    """Los árboles (objetos tipo 4) como sprites de la capa 3, para que la vista los dibuje."""
    sprites, cells = list(m['sprites']), list(m['cells'])
    for i, o in enumerate(o for o in m['objects'] if o['objType'] == 4 and o.get('frames')):
        f = o['frames'][0]
        sid = 90_000_000 + i
        sprites.append(dict(f, id=sid))
        cells.append(dict(x=o['x'], y=o['y'], layer=3, sprite=sid))
    return dict(m, sprites=sprites, cells=cells)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('salida')
    ap.add_argument('--mapa', type=int, default=6)
    ap.add_argument('--x', type=int, default=38)
    ap.add_argument('--y', type=int, default=72)
    args = ap.parse_args()
    pl.S, pl.HD, pl.T = VL.K, False, v.T
    m = with_trees(pl.load(args.mapa))
    x0, y0, T, W, H = args.x, args.y, v.T, VL.W, VL.H
    rng = np.random.default_rng(12)
    to_px = lambda p: ((p[0] - (x0 - 1)) * T, (-(y0 - 1) - p[1]) * T)
    view_c = np.array([x0 - 1 + W / 2, -(y0 - 1) - H / 2])
    ulla = pl.load(1)
    walker = np.asarray(next(pl.npc_img(n) for n in ulla['npcs'] if n['name'].startswith('Trevor')), np.float32) / 255
    lamps = [(np.array([l['x'] - .5, -l['y'] + .5]), (l['range'] - 99 if l['range'] >= 100 else l['range']) + .5)
             for l in m['lights'] if x0 - 6 <= l['x'] < x0 + W + 6 and y0 - 6 <= l['y'] < y0 + H + 6]
    trees = []
    for o in m['objects']:
        if o['objType'] == 4 and o.get('frames') and x0 - 4 <= o['x'] < x0 + W + 4 and y0 - 2 <= o['y'] < y0 + H + 8:
            f = o['frames'][0]
            w, h = f['width'] / 32, f['height'] / 32
            trees.append((np.array([o['x'] - .5, -o['y'] + h * .62]), w * .38))
    frames = []

    def caption(img, text):
        out = v.to_img(img)
        d = ImageDraw.Draw(out)
        d.rectangle((0, 0, out.width, 26), fill=(0, 0, 0))
        d.text((10, 7), text, fill=(255, 255, 255))
        return out

    def paste_walker(img, feet, shade):
        fp = to_px(feet)
        L.contact(img, fp, T)
        h, w = walker.shape[:2]
        v._paste(img, walker[..., :3] * shade, walker[..., 3:], int(fp[0] - w / 2), int(fp[1] - h))

    # ------------------------------------------------------------------ 1) luciérnagas (noche)
    g, u = VL.layers(m, x0, y0, (120, 120, 120))
    night = g * (1 - u[..., 3:]) + u[..., :3]
    ys = np.arange(night.shape[0])[:, None]
    night = night * (1 + (np.array([.6, .68, .95])[None, None, :] - 1) * (.42 * (.55 + .45 * (1 - ys / night.shape[0])))[..., None])

    def away_light(p):
        push = np.zeros(2)
        for lp, r in lamps:
            d = p - lp
            mm = np.linalg.norm(d)
            if .01 < mm < r + 1.5:
                push += d / mm * (1 - mm / (r + 1.5))
        return push

    def darkest(frm=None, dist=0):
        best, score = view_c, -1e9
        for _ in range(14):
            q = view_c + np.array([rng.uniform(-W / 2, W / 2) * .85, rng.uniform(-H / 2, H / 2) * .85])
            s = -np.linalg.norm(away_light(q)) * 3
            if frm is not None:
                s -= abs(np.linalg.norm(q - frm) - dist) * .35
            if s > score:
                best, score = q, s
        return best

    N, SW = 24, 22
    home = darkest()
    target = home.copy()
    pos = np.array([home + rng.uniform(-3, 3, 2) for _ in range(N)])
    vel = np.zeros((N, 2))
    phase, seed = rng.uniform(0, 6.28, N), rng.uniform(0, 100, N)
    role = ['swarm'] * SW + ['off', 'off']
    timer, heading = np.zeros(N), np.zeros((N, 2))
    alarm_until, alarm_from, next_wp = -1.0, np.zeros(2), 0.0
    glow = VL.radial(int(.36 * T)) ** 2
    dt = 1 / FPS
    for f in range(FPS * 10):
        t = f * dt
        if t >= next_wp:
            next_wp = t + rng.uniform(5, 9)
            target = darkest(home, 6.5)
        step = target - home
        n_ = np.linalg.norm(step)
        home = home + (step / n_ * min(n_, .9 * dt) if n_ > 1e-6 else 0) + away_light(home) * .5 * dt
        k = min(max((t - 3.5) / 5, 0), 1)
        feet = np.array([x0 - 1 + .5 + k * (W - 1), view_c[1] - .3])
        chars = [feet + np.array([0, .8])] if 3 < t < 9 else []
        if abs(t - 2) < dt / 2:
            i = int(rng.integers(0, SW))
            role[i], timer[i], heading[i] = 'explorer', 5, rng.normal(0, 1, 2)
            heading[i] /= np.linalg.norm(heading[i])
        members = [i for i in range(N) if role[i] == 'swarm']
        centroid = pos[members].mean(0) if members else home
        avg_v = vel[members].mean(0) if members else np.zeros(2)
        for i in members:
            for c in chars:
                if np.linalg.norm(pos[i] - c) < 2.3:
                    alarm_until, alarm_from = t + .6, c
        img = night.copy()
        if chars:
            paste_walker(img, feet, .72)
        resp = 1 - math.exp(-9 * dt)
        for i in range(N):
            if role[i] == 'off':
                continue
            p = pos[i]
            want = np.array([L.perlin(np.array([t * .45]), np.array([seed[i]]))[0] - .5,
                             L.perlin(np.array([seed[i]]), np.array([t * .45]))[0] - .5]) * 2.4
            mx = 1.5
            if role[i] == 'swarm':
                want += (centroid - p) * .18 + (home - p) * .22 + avg_v * .8
                if rng.random() < .05 * dt * 6:            # en el video, un poco más seguido para que se vea
                    role[i], timer[i], heading[i] = 'solo', rng.uniform(1, 2.5), rng.normal(0, 1, 2)
                    heading[i] /= np.linalg.norm(heading[i])
            elif role[i] in ('solo', 'explorer'):
                want += heading[i] * (2.2 if role[i] == 'solo' else 1.8)
                mx = 2.4 if role[i] == 'solo' else 2.1
                timer[i] -= dt
                if timer[i] <= 0:
                    role[i] = 'swarm'
            for j in range(N):
                if j != i and role[j] != 'off':
                    d = p - pos[j]
                    m2 = d @ d
                    if 1e-4 < m2 < .9:
                        want += d / m2 * .25
            near_c = min((np.linalg.norm(p - c) for c in chars), default=9)
            if near_c < 2.3:
                c = min(chars, key=lambda c: np.linalg.norm(p - c))
                want += (p - c) / max(near_c, .01) * (1 - near_c / 2.3) * 9
                mx = 3.4
            elif t < alarm_until and role[i] == 'swarm':
                d = p - alarm_from
                mm = np.linalg.norm(d)
                want += d / max(mm, .01) * 4 / (1 + mm * .25)
                mx = 2.7
            want += away_light(p) * 3
            sp = np.linalg.norm(want)
            if sp > mx:
                want *= mx / sp
            vel[i] = vel[i] + (want - vel[i]) * resp
            pos[i] = p + vel[i] * dt
            near = [j for j in range(N) if j != i and role[j] != 'off' and ((pos[j] - pos[i]) ** 2).sum() < 9]
            coup = np.mean([math.sin(phase[j] - phase[i]) for j in near]) if near else 0
            phase[i] += dt * ((5 if role[i] != 'swarm' else 3.4) + .9 * coup)
            blink = max(0, math.sin(phase[i])) ** 3
            px = to_px(pos[i])
            VL.add_sprite(img, glow, px[0], px[1], (.8, 1, .45), .15 + .85 * blink)
        lbl = 'Noche: el enjambre recorre lo oscuro'
        if 3 < t < 9:
            lbl = 'Pasa un personaje: alarma de la colmena, se abren al instante y se vuelven a juntar'
        if 2 <= t < 7:
            lbl += '  ·  una exploradora'
        frames.append(caption(img, lbl))

    # ------------------------------------------------------------------ 2) lluvia con árboles y charcos
    g, u = VL.layers(m, x0, y0, (190, 190, 200))
    ground, upper = g, u
    tint = np.array([.42, .48, .6])
    # Charcos: mismo hash que el C# (casillas caminables); acá, sin bloqueos.
    blocked = {(b['x'], b['y']) for b in m['blocks'] if b['flags'] & 15}
    puddles = []
    for y in range(y0, y0 + H + 1):
        for x in range(x0, x0 + W + 1):
            h = ((x * 73856093) ^ (y * 19349663) ^ (args.mapa * 83492791)) & 0xFFFFFFFF
            h ^= h >> 13
            h = (h * 0x5bd1e995) & 0xFFFFFFFF
            h ^= h >> 15
            if h % 100 >= 6 or (x, y) in blocked:
                continue
            jx, jy = ((h >> 8) & 255) / 255 - .5, ((h >> 16) & 255) / 255 - .5
            size = .7 + ((h >> 24) & 255) / 255 * .9
            puddles.append((np.array([x - .5 + jx * .5, -y + .5 + jy * .4]), size * 1.3, size * .7))
    hero_p = min(puddles, key=lambda p: np.linalg.norm(p[0] - view_c)) if puddles else None
    hero = hero_p[0] + np.array([0, hero_p[2] * .35]) if hero_p else view_c
    drops = [dict(k=0 if i % 20 < 9 else 1 if i % 20 < 17 else 2, x=rng.uniform(0, W * T), y=rng.uniform(-H * T, H * T),
                  land=rng.uniform(0, H * T)) for i in range(200)]
    specs = [(.5, 1.6 * .13, .3, 7, 9), (.75, 1.6 * .22, .55, 11, 14), (1.0, 1.6 * .32, .42, 16, 20)]
    for dr in drops:
        dr['sp'] = rng.uniform(*specs[dr['k']][3:5])
    splashes, leaves, drips = [], [], []
    for f in range(FPS * 10):
        t = f * dt
        wet = min(1, .25 + t / 6)                          # en el juego tarda ~45 s; acá acelerado
        img = ground * (1 - upper[..., 3:]) + upper[..., :3]
        # Charcos: piso más oscuro y frío, brillo de cielo arriba; reflejo del personaje adentro.
        Hh, Ww = img.shape[:2]
        for c, pw, ph in puddles:
            s = .4 + .6 * wet
            w_, h_ = int(pw * s * T), int(ph * s * T)
            if w_ < 4 or h_ < 4:
                continue
            yy, xx = np.mgrid[0:h_, 0:w_]
            r = np.hypot((xx + .5) / w_ * 2 - 1, (yy + .5) / h_ * 2 - 1)
            a = np.clip((1 - r) * 6, 0, 1)
            cx, cy = to_px(c)
            l, tp = int(cx - w_ / 2), int(cy - h_ / 2)
            x1, y1, x2, y2 = max(0, l), max(0, tp), min(Ww, l + w_), min(Hh, tp + h_)
            if x1 >= x2 or y1 >= y2:
                continue
            aa = a[y1 - tp:y2 - tp, x1 - l:x2 - l, None]
            sky = (1 - (yy[y1 - tp:y2 - tp, x1 - l:x2 - l, None] / h_)) * .5 + .5
            img[y1:y2, x1:x2] *= 1 + (np.array([.55, .6, .72]) - 1) * aa * .65 * wet
            img[y1:y2, x1:x2] += np.array([.36, .44, .6]) * aa * .14 * wet * sky
            if hero_p is not None and c is hero_p[0]:
                fp = to_px(hero)
                hh, ww = walker.shape[:2]
                flipped = walker[::-1]
                ref = np.zeros((Hh, Ww, 4), np.float32)
                lx, ty = int(fp[0] - ww / 2), int(fp[1])
                rx1, ry1, rx2, ry2 = max(0, lx), max(0, ty), min(Ww, lx + ww), min(Hh, ty + hh)
                ref[ry1:ry2, rx1:rx2] = flipped[ry1 - ty:ry2 - ty, rx1 - lx:rx2 - lx]
                mask = np.zeros((Hh, Ww, 1), np.float32)
                mask[y1:y2, x1:x2] = (aa > .35)
                ra = ref[..., 3:] * mask * .42 * wet
                img = img * (1 - ra) + ref[..., :3] * np.array([.62, .68, .8]) * .8 * ra
        paste_walker(img, hero, 1.0)
        img *= 1 + (tint - 1) * .2
        in_canopy = lambda p: next((tr for tr in trees if np.linalg.norm(p - tr[0]) < tr[1]), None)
        in_puddle = lambda p: any((((p - c) / (np.array([pw, ph]) * .5 * (.4 + .6 * wet))) ** 2).sum() < .8 for c, pw, ph in puddles)
        for dr in drops:
            sp = specs[dr['k']]
            dr['y'] += dr['sp'] * T * dt
            if dr['k'] == 1 and dr['y'] >= dr['land']:
                wp = np.array([dr['x'] / T + (x0 - 1), -(dr['land'] / T) - (y0 - 1)])
                tr = in_canopy(wp)
                if tr is not None:
                    if rng.random() < .55:
                        leaves.append([wp.copy(), 0.0, tr[0][1] - tr[1] * .8, rng.uniform(1.5, 3)])
                else:
                    splashes.append([dr['x'], dr['land'], 0.0, 1.5 if in_puddle(wp) else 1.0])
                dr['y'], dr['x'], dr['land'] = rng.uniform(-H * T * .3, 0), rng.uniform(0, W * T), rng.uniform(0, H * T)
            elif dr['y'] > H * T + T:
                dr['y'], dr['x'] = rng.uniform(-H * T * .3, 0), rng.uniform(0, W * T)
            ln, wd = int(sp[1] * T), max(1, int(round(sp[0] * 2 * VL.K)))
            x, y1 = int(dr['x']), int(dr['y'])
            age_fade = 1.0
            for j in range(max(0, y1 - ln), min(Hh, y1)):
                fade = (j - (y1 - ln)) / max(1, ln)
                img[j, max(0, x):min(Ww, x + wd)] += np.array([.86, .9, 1]) * sp[2] * fade * .8 * age_fade
        for lf in leaves:                                     # gota entre las hojas
            lf[1] += dt
            lf[0] = lf[0] + np.array([math.sin(lf[1] * 9) * .35, -.9]) * dt
            if lf[0][1] <= lf[2]:
                drips.append([lf[0].copy(), np.array([0, -.4]), lf[0][1] - rng.uniform(.4, 1.3)])
                lf[1] = 99
                continue
            px = to_px(lf[0])
            VL.add_sprite(img, VL.radial(5) ** 2, px[0], px[1], (.85, .95, 1), .55 * (.6 + .4 * abs(math.sin(lf[1] * 14))))
        leaves[:] = [lf for lf in leaves if lf[1] < lf[3]]
        for dp in drips:                                      # goteo desde la copa
            dp[1][1] -= 16 * dt
            dp[0] = dp[0] + dp[1] * dt
            if dp[0][1] <= dp[2]:
                px = to_px(np.array([dp[0][0], dp[2]]))
                splashes.append([px[0], px[1], 0.0, .9])
                dp[2] = 1e9
                continue
            px = to_px(dp[0])
            for j in range(int(px[1]) - 6, int(px[1])):
                if 0 <= j < Hh and 0 <= int(px[0]) < Ww:
                    img[j, int(px[0])] += np.array([.85, .92, 1]) * .5
        drips[:] = [dp for dp in drips if dp[2] < 1e8]
        for s_ in splashes:
            s_[2] += dt
            kk = s_[2] / .28
            if kk < 1:
                r = max(2, int((.12 + kk * .28) * (.7 + .3 * s_[3]) * T / 2))
                yy, xx = np.ogrid[-r // 2:r // 2 + 1, -r:r + 1]
                ring = np.clip(1 - np.abs(np.hypot(xx / r, yy / max(r // 2, 1)) - .75) * 5, 0, 1)
                VL.add_sprite(img, ring, s_[0], s_[1], (.85, .9, 1), .38 * min(s_[3], 1.2) * (1 - kk))
        splashes[:] = [s_ for s_ in splashes if s_[2] < .28]
        frames.append(caption(img, 'Lluvia: gotas que bajan por las hojas y gotean de la copa, charcos que crecen, '
                                   'ondas y reflejo del personaje'))

    import imageio.v2 as imageio
    wr = imageio.get_writer(args.salida, fps=FPS, codec='libx264', quality=8, macro_block_size=8)
    for fr in frames:
        wr.append_data(np.asarray(fr))
    wr.close()
    frames[FPS * 16].save(Path(args.salida).with_suffix('.jpg'), quality=90)
    print(args.salida, frames[0].size, len(frames), 'cuadros', 'árboles', len(trees), 'charcos', len(puddles))


if __name__ == '__main__':
    main()
