"""Video sin Unity de la luz y el clima nuevos (V290-V292), con los mismos números del código:
1) del día a la noche: sombras que giran y se alargan con el sol, nubes que pasan y tapan el sol, noche fría con
   luz de luna en los claros, faroles que parpadean y tiñen a los personajes, luciérnagas;
2) tormenta: lluvia vertical a escala del personaje, salpicaduras, gotas que rebotan en la armadura, relámpagos
   con destello y sombra dura.

  python Tools/hd_remake/video_luz_v292.py SALIDA.mp4 [--mapa 1] [--x 22 --y 12]

Aproximación para Lucas (mismo mapa y luz Original del AO); el juego real lo dibuja Unity."""
import argparse, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, str(Path(__file__).resolve().parent))
import dungeon_vista_p1 as v
import preview_luces as pl
import luz_vista_v291 as L

K = 2                                     # 2x: 64 px por casilla
v.K, v.T = K, 32 * K
FPS = 15
W, H = 13, 9


def layers(m, x0, y0, amb_rgb):
    """Piso (RGB) y capa de arriba (RGBA: objetos, personajes, techos) con la luz Original para ese ambiente."""
    T = v.T
    mm = dict(m, env=dict(m['env'], baseLight=(amb_rgb[0] << 16) | (amb_rgb[1] << 8) | amb_rgb[2]))
    cor = v.corners_original(mm)
    ground = np.zeros((H * T, W * T, 3), np.float32)
    upper = np.zeros((H * T, W * T, 4), np.float32)
    for part, cells, npcs in ((ground, (1, 2), []), (upper, (3, 4), m['npcs'])):
        sub = dict(mm, cells=[c for c in m['cells'] if c['layer'] in cells], npcs=npcs)
        for s, x, y in v.draw_list(sub, x0, y0, W, H):
            left, top = v.place(s, x, y, x0, y0)
            a = np.asarray(s, np.float32) / 255
            cy, cx = min(max(y - m['ymin'], 0), cor.shape[0] - 1), min(max(x - m['xmin'], 0), cor.shape[1] - 1)
            rgb = a[..., :3] * v.tint(s.size, cor[cy, cx])
            if part is ground:
                v._paste(part, rgb, a[..., 3:], left, top)
            else:
                Hh, Ww = part.shape[:2]
                h, w = a.shape[:2]
                x1, y1, x2, y2 = max(0, left), max(0, top), min(Ww, left + w), min(Hh, top + h)
                if x1 >= x2 or y1 >= y2:
                    continue
                sa = a[y1 - top:y2 - top, x1 - left:x2 - left, 3:]
                sr = rgb[y1 - top:y2 - top, x1 - left:x2 - left]
                part[y1:y2, x1:x2, :3] = part[y1:y2, x1:x2, :3] * (1 - sa) + sr * sa
                part[y1:y2, x1:x2, 3:] = part[y1:y2, x1:x2, 3:] * (1 - sa) + sa
    return ground, upper


def radial(size):
    yy, xx = np.mgrid[0:size, 0:size]
    return np.clip(1 - np.hypot((xx + .5) / size * 2 - 1, (yy + .5) / size * 2 - 1), 0, 1)


def add_sprite(img, a, cx, cy, color, alpha):
    h, w = a.shape[:2]
    left, top = int(cx - w / 2), int(cy - h / 2)
    Hh, Ww = img.shape[:2]
    x1, y1, x2, y2 = max(0, left), max(0, top), min(Ww, left + w), min(Hh, top + h)
    if x1 < x2 and y1 < y2:
        img[y1:y2, x1:x2] += a[y1 - top:y2 - top, x1 - left:x2 - left, None] * np.array(color, np.float32) * alpha


def flicker(seed, t):
    return 1 + 0.3 * (L.perlin(np.array([t * 5.5]), np.array([seed * 0.173]))[0] - 0.5) + \
        0.08 * (L.perlin(np.array([t * 17]), np.array([seed * 0.311 + 3]))[0] - 0.5)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('salida')
    ap.add_argument('--mapa', type=int, default=1)
    ap.add_argument('--x', type=int, default=22)
    ap.add_argument('--y', type=int, default=12)
    args = ap.parse_args()
    pl.S, pl.HD, pl.T = K, False, v.T
    m = pl.load(args.mapa)
    x0, y0, T = args.x, args.y, v.T
    rng = np.random.default_rng(4)

    # Luz Original lineal en el ambiente: se dibuja con ambiente negro y blanco y se mezcla por hora.
    Hh, Ww = H * T, W * T
    to_px = lambda wx, wy: ((wx - (x0 - 1)) * T, (-(y0 - 1) - wy) * T)

    # Un personaje en el medio del camino (tierra clara) para que la sombra se lea bien: copia de un NPC visible.
    visible = [n for n in m['npcs'] if x0 - 1 <= n['x'] < x0 + W + 1 and y0 - 1 <= n['y'] < y0 + H + 2]
    if visible:
        m = dict(m, npcs=m['npcs'] + [dict(visible[0], x=x0 + 6, y=y0 + 4, name='Jugador')])
    g0, u0 = layers(m, x0, y0, (0, 0, 0))
    g1, u1 = layers(m, x0, y0, (255, 255, 255))
    npcs = []
    for n in m['npcs']:
        if x0 - 1 <= n['x'] < x0 + W + 1 and y0 - 1 <= n['y'] < y0 + H + 2:
            im = pl.npc_img(n)
            if im is None:
                continue
            a = np.asarray(im, np.float32) / 255
            npcs.append(dict(n=n, sil=a[..., 3], feet=np.array([n['x'] - .5, -n['y']]), h=im.height / T,
                             w=im.width))
    lamps = []
    for Lp in m['lights']:
        rng_ = Lp['range'] - 99 if Lp['range'] >= 100 else Lp['range']
        if rng_ <= 0:
            continue
        c = Lp['color'] & 0xFFFFFF
        col = np.array([(c >> 16) & 255, (c >> 8) & 255, c & 255], np.float32) / 255
        if col.max() - col.min() < 0.08:
            col = col + (np.array([1, .82, .55]) - col) * .55          # blanco del AO -> cálido de farol
        lum = (0.3 * ((c >> 16) & 255) + 0.59 * ((c >> 8) & 255) + 0.11 * (c & 255)) / 255
        lamps.append(dict(pos=np.array([Lp['x'] - .5, -Lp['y'] + .5]), r=rng_ + .5, col=col,
                          power=min(1, lum * 1.2), seed=Lp['x'] * 131 + Lp['y'] * 17))
    glow_cache = {}
    firefly = radial(int(0.3 * T)) ** 2
    streak = np.zeros((int(T), 3), np.float32)

    frames = []
    ys, xs = np.mgrid[0:Hh:8, 0:Ww:8]
    wxg, wyg = xs / T + (x0 - 1), -(ys / T) - (y0 - 1)
    up = lambda a: np.asarray(Image.fromarray(a.astype(np.float32)).resize((Ww, Hh), Image.BILINEAR))
    flies = [dict(p=np.array([x0 - 1 + rng.uniform(0, W), -(y0 - 1) - rng.uniform(0, H)]), ph=rng.uniform(0, 100))
             for _ in range(16)]

    def frame(hour, coverage, t, cloud_off, storm=0.0, flash=0.0, flash_dir=(0, 1), rain=None, caption=''):
        st = L.sky(hour, coverage)
        amb = np.array(L.day_color(hour), np.float32) / 255
        img = g0 + amb * (g1 - g0)
        darkness = 1 - (0.47 + 0.53 * st['sun'])
        # Sombras de personajes (sol/luna, farol cercano o relámpago).
        washes = []
        for c in npcs:
            feet = c['feet']
            q = (feet - cloud_off) / 7
            cloud = float(L.field(np.array([q[0]]), np.array([q[1]]), coverage)[0])
            d, ln, al = st['dir'], st['len'], st['alpha'] * (1 - .85 * cloud)
            best, lamp, bd = 0, None, 0
            for lp in lamps:
                dist = np.linalg.norm(feet - lp['pos'])
                if dist >= lp['r']:
                    continue
                wgt = lp['power'] * (1 - dist / lp['r']) ** 2 * min(max(1.15 - (1 - darkness), 0), 1)
                if wgt > best:
                    best, lamp, bd = wgt, lp, dist
            pa = min(best * 1.3, 1) * .5
            if pa > al and bd > .2:
                d, ln, al = (feet - lamp['pos']) / bd, min(max(.45 + bd * .2, .45), 1.1), pa
            if flash * .7 > al:
                d, ln, al = np.array(flash_dir), .8, flash * .7
            fp = to_px(*feet)
            L.stamp_shadow(img, c['sil'], fp, math.degrees(math.atan2(-d[0], d[1])), ln, al)
            L.contact(img, fp, T)
            wash = None
            if lamp is not None:
                wash = (lamp['col'], min(best * 1.6, 1) * .32 * flicker(lamp['seed'], t))
            if flash * .55 > (wash[1] if wash else 0):
                wash = (np.array([.78, .86, 1]), flash * .55)
            washes.append((c, fp, wash))
        ua = u0[..., 3:]
        upper = u0[..., :3] + amb * (u1[..., :3] - u0[..., :3])
        img = img * (1 - ua) + upper
        # Luz del farol o del relámpago sobre el personaje (copia aditiva de su silueta).
        for c, fp, wash in washes:
            if wash and wash[1] > .01:
                sil = c['sil']
                h, w = sil.shape
                left, top = int(fp[0] - w / 2), int(fp[1] - h)
                x1, y1, x2, y2 = max(0, left), max(0, top), min(Ww, left + w), min(Hh, top + h)
                if x1 < x2 and y1 < y2:
                    img[y1:y2, x1:x2] += sil[y1 - top:y2 - top, x1 - left:x2 - left, None] * wash[0] * wash[1]
        # Halos de faroles (según la oscuridad, parpadeando).
        for lp in lamps:
            px = to_px(*lp['pos'])
            if not (-lp['r'] * T < px[0] < Ww + lp['r'] * T and -lp['r'] * T < px[1] < Hh + lp['r'] * T):
                continue
            f = flicker(lp['seed'], t)
            size = int(lp['r'] * 2 * T * (.97 + .06 * f)) // 4 * 4
            if size not in glow_cache:
                glow_cache[size] = radial(size) ** 3
            add_sprite(img, glow_cache[size], px[0], px[1], lp['col'], min(.24 * darkness * f, 1))
        # Cielo: sombras de nubes, tinte de noche y luz de luna en los claros.
        cloud = L.field((wxg - cloud_off[0]) / 7, (wyg - cloud_off[1]) / 7, coverage)
        partly = 1 - L.smooth(L.inv_lerp(.62, .92, coverage))
        shade = (st['sun'] * .5 + st['moon'] * .3) * partly * cloud
        shade_col = np.array([.7, .72, .86]) + (np.array([.6, .64, .76]) - np.array([.7, .72, .86])) * st['sun']
        mult = 1 + (shade_col[None, None, :] - 1) * shade[..., None]
        grade = st['moon'] * .42 * (.55 + .45 * (1 - ys / Hh))[..., None]
        mult = mult * (1 + (np.array([.6, .68, .95])[None, None, :] - 1) * grade)
        moon_light = st['moon'] * .1 * (1 - .8 * L.smooth(L.inv_lerp(.5, .92, coverage)))
        add = ((1 - cloud) ** 2 * moon_light)[..., None] * np.array([.55, .66, .95])[None, None, :]
        if storm > 0:                                  # tinte de día de lluvia (multiplica)
            mult = mult * (1 + (np.array([.42, .48, .6]) - 1) * .2 * storm)
        img = img * np.stack([up(mult[..., i]) for i in range(3)], -1) + np.stack([up(add[..., i]) for i in range(3)], -1)
        # Luciérnagas de noche sin lluvia.
        fade = min(max((1 - st['sun'] - .5) * 2, 0), 1)
        if fade > 0 and storm == 0 and coverage < .8:
            for fl in flies:
                drift = np.array([L.perlin(np.array([t * .35]), np.array([fl['ph']]))[0] - .5,
                                  L.perlin(np.array([fl['ph']]), np.array([t * .35]))[0] - .5]) * 1.4
                fl['p'] = fl['p'] + drift / FPS
                blink = L.perlin(np.array([t * .9 + fl['ph']]), np.array([fl['ph'] * .5]))[0]
                a = min(max((blink - .45) * 3, 0), 1) * .9 * fade
                if a > .02:
                    px = to_px(*fl['p'])
                    add_sprite(img, firefly, px[0], px[1], (.8, 1, .45), a)
        if rain is not None:
            rain(img, t)
        if flash > .01:
            img = img + np.array([.72, .8, 1], np.float32) * flash * .42
        out = v.to_img(img)
        if caption:
            d = ImageDraw.Draw(out)
            d.rectangle((0, 0, out.width, 26), fill=(0, 0, 0))
            d.text((10, 7), caption, fill=(255, 255, 255))
        return out

    # 1) Del día a la noche (8 h -> 23:30) en 9 s, con nubes sueltas que pasan.
    n1 = FPS * 9
    for i in range(n1):
        hour = 8 + 15.5 * i / (n1 - 1)
        t = i / FPS
        cov = 0.42 if hour < 20 else 0.5
        off = np.array([x0 + 2.0 + t * 1.1, -y0 - 1.0 + t * 0.12])
        label = ('Día: la sombra va al lado opuesto del sol; las nubes lo tapan' if hour < 16.5 else
                 'Atardecer: sombras largas hacia el este' if hour < 18.5 else
                 'Noche: luna tenue en los claros, faroles que parpadean y tiñen, luciérnagas')
        frames.append(frame(hour, cov, t, off, caption=f'{int(hour):02d}:{int(hour % 1 * 60):02d}  ·  {label}'))

    # 2) Tormenta de noche: lluvia vertical a escala del personaje, salpicaduras, rebotes y relámpagos.
    hero = next((c for c in npcs if c['n'].get('name') == 'Jugador'), npcs[0])
    layers_r = [(.45, .5, 1.6 * .13, .3, 7, 9), (.40, .75, 1.6 * .22, .55, 11, 14), (.15, 1.0, 1.6 * .32, .42, 16, 20)]
    drops = []
    for i in range(300):
        k = 0 if i % 20 < 9 else 1 if i % 20 < 17 else 2
        drops.append(dict(k=k, x=rng.uniform(0, Ww), y=rng.uniform(-Hh, Hh), sp=rng.uniform(*layers_r[k][4:6]),
                          land=rng.uniform(0, Hh), a=layers_r[k][3] * rng.uniform(.75, 1.1)))
    splashes, bounces = [], []

    def rain(img, t):
        dt = 1 / FPS
        for dr in drops:
            spec = layers_r[dr['k']]
            dr['y'] += dr['sp'] * T * dt
            if dr['k'] == 1 and dr['y'] >= dr['land']:
                splashes.append([dr['x'], dr['land'], 0.0])
                dr['y'], dr['x'], dr['land'] = rng.uniform(-Hh * .3, 0), rng.uniform(0, Ww), rng.uniform(0, Hh)
            elif dr['y'] > Hh + T:
                dr['y'], dr['x'] = rng.uniform(-Hh * .3, 0), rng.uniform(0, Ww)
            ln, wd = int(spec[2] * T), max(1, int(round(spec[1] * 2 * K)))
            x, y1 = int(dr['x']), int(dr['y'])
            for j in range(max(0, y1 - ln), min(Hh, y1)):
                fade = (j - (y1 - ln)) / max(1, ln)
                img[j, max(0, x):min(Ww, x + wd)] += np.array([.86, .9, 1]) * dr['a'] * fade * .8
        for s in splashes:
            s[2] += dt
            k = s[2] / .28
            if k < 1:
                r = int((.12 + k * .28) * T / 2)
                yy, xx = np.ogrid[-r // 2:r // 2 + 1, -r:r + 1]
                ring = np.clip(1 - np.abs(np.hypot(xx / max(r, 1), yy / max(r // 2, 1)) - .75) * 5, 0, 1)
                add_sprite(img, ring, s[0], s[1], (.85, .9, 1), .38 * (1 - k))
        splashes[:] = [s for s in splashes if s[2] < .28]
        # Rebote en la armadura del personaje: 12 golpes/s x 1,5 (tormenta).
        feet = to_px(*hero['feet'])
        for _ in range(rng.poisson(18 / FPS)):
            side = -1 if rng.random() < .5 else 1
            if rng.random() < .45:
                hit = (feet[0] + rng.uniform(-.16, .16) * T, feet[1] - hero['h'] * T + rng.uniform(.02, .14) * T)
            else:
                hit = (feet[0] + side * rng.uniform(.12, .3) * T, feet[1] - hero['h'] * T * rng.uniform(.6, .7))
            bounces.append([hit[0], hit[1], 0, 0, 0.0, .09, True])
            for dd in range(4 if rng.random() < .4 else 3):
                o = (side if dd == 0 else (-side if rng.random() < .5 else side)) * rng.uniform(.5, 1.6)
                bounces.append([hit[0], hit[1], o * T, -rng.uniform(1.2, 2.3) * T, 0.0, rng.uniform(.3, .45), False])
        dot = radial(max(3, int(.09 * T)))
        for b in bounces:
            b[4] += dt
            k = b[4] / b[5]
            if k >= 1:
                continue
            if not b[6]:
                b[3] += 11 * T * dt
                b[0] += b[2] * dt
                b[1] += b[3] * dt
            add_sprite(img, dot, b[0], b[1], (.9, .95, 1), .85 * (1 - k) if b[6] else .9 * (1 - k * k))
        bounces[:] = [b for b in bounces if b[4] < b[5]]

    n2 = FPS * 7
    strikes = [1.2, 4.3]
    for i in range(n2):
        t = i / FPS
        flash, fdir = 0.0, (0.0, 1.0)
        for s0 in strikes:
            tt = t - s0
            if 0 <= tt < .9:
                env = math.exp(-tt / .06) + (.7 * math.exp(-(tt - .16) / .1) if tt > .16 else 0)
                flash = max(flash, min(env * .9, 1))
                ang = s0 * 2.1
                fdir = (math.cos(ang), math.sin(ang))
        off = np.array([x0 + 12.0 + t * 1.6, -y0 - 1.0])
        frames.append(frame(22.5, 0.92, 9 + t, off, storm=1.0, flash=flash, flash_dir=fdir, rain=rain,
                            caption='Tormenta: lluvia a escala del personaje, rebote en la armadura y relámpagos'))

    import imageio.v2 as imageio
    writer = imageio.get_writer(args.salida, fps=FPS, codec='libx264', quality=8, macro_block_size=8,
                                ffmpeg_params=['-pix_fmt', 'yuv420p'])
    for f in frames:
        writer.append_data(np.asarray(f))
    writer.close()
    frames[FPS * 4].save(Path(args.salida).with_suffix('.jpg'), quality=90)
    print(args.salida, frames[0].size, len(frames), 'cuadros')


if __name__ == '__main__':
    main()
