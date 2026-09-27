"""Video sin Unity del enjambre de luciérnagas (AOFirefliesV292), con las mismas reglas del C#: de noche en el campo
se juntan en lo más oscuro, se abren y se escurren alrededor de un personaje que pasa y de los faroles (como el agua),
una sale a explorar y vuelve, y aparece alguna suelta que se pierde. Parpadeo que se sincroniza entre vecinas.

  python Tools/hd_remake/video_luciernagas_v292.py SALIDA.mp4 [--mapa 6] [--x 38 --y 72]"""
import argparse, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, str(Path(__file__).resolve().parent))
import video_luz_v292 as VL
import dungeon_vista_p1 as v
import preview_luces as pl
import luz_vista_v291 as L

FPS, SECONDS = 15, 14


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('salida')
    ap.add_argument('--mapa', type=int, default=6)
    ap.add_argument('--x', type=int, default=38)
    ap.add_argument('--y', type=int, default=72)
    args = ap.parse_args()
    pl.S, pl.HD, pl.T = VL.K, False, v.T
    m = pl.load(args.mapa)
    x0, y0, T, W, H = args.x, args.y, v.T, VL.W, VL.H
    rng = np.random.default_rng(9)
    ground, upper = VL.layers(dict(m, npcs=[]), x0, y0, (120, 120, 120))          # noche del AO
    Hh, Ww = ground.shape[:2]
    to_px = lambda p: ((p[0] - (x0 - 1)) * T, (-(y0 - 1) - p[1]) * T)
    lamps = [(np.array([l['x'] - .5, -l['y'] + .5]), (l['range'] - 99 if l['range'] >= 100 else l['range']) + .5)
             for l in m['lights'] if x0 - 6 <= l['x'] < x0 + W + 6 and y0 - 6 <= l['y'] < y0 + H + 6]
    ulla = pl.load(1)                                                    # un personaje humano (Ullathorpe)
    walker_img = next(pl.npc_img(n) for n in ulla['npcs'] if n['name'].startswith('Trevor'))
    walker = np.asarray(walker_img, np.float32) / 255

    # Tinte de noche (Purkinje) y luz de luna tenue: fijos para el video.
    ys = np.arange(Hh)[:, None]
    grade = .42 * (.55 + .45 * (1 - ys / Hh))
    mult = 1 + (np.array([.6, .68, .95])[None, None, :] - 1) * grade[..., None]

    def away_from_light(p):
        push = np.zeros(2)
        for lp, r in lamps:
            d = p - lp
            reach = r + 1.5
            mm = np.linalg.norm(d)
            if .01 < mm < reach:
                push += d / mm * (1 - mm / reach)
        return push

    view_c = np.array([x0 - 1 + W / 2, -(y0 - 1) - H / 2])

    def darkest():
        best, score = view_c, -1e9
        for _ in range(12):
            q = view_c + np.array([rng.uniform(-W / 2, W / 2) * .6, rng.uniform(-H / 2, H / 2) * .6])
            s = -np.linalg.norm(away_from_light(q)) * 3
            if s > score:
                best, score = q, s
        return best

    home = darkest()
    N, SW = 16, 14
    pos = np.array([home + rng.normal(0, .9, 2) for _ in range(N)])
    vel = np.zeros((N, 2))
    phase = rng.uniform(0, 6.28, N)
    seed = rng.uniform(0, 100, N)
    role = ['swarm'] * SW + ['off'] * (N - SW)
    timer = np.zeros(N)
    heading = np.zeros((N, 2))
    fade = np.zeros(N)
    glow = VL.radial(int(.36 * T)) ** 2
    floor = VL.radial(int(4.5 * T)) ** 2
    lamp_glow = {}
    frames = []
    dt = 1 / FPS
    for f in range(FPS * SECONDS):
        t = f * dt
        # Personaje que cruza el enjambre (de izquierda a derecha, entre los segundos 4 y 11).
        k = min(max((t - 4) / 7, 0), 1)
        feet = np.array([x0 - 1 + .5 + k * (W - 1), home[1] - .6])
        chars = [feet] if 3.5 < t < 11.5 else []
        if abs(t - 3) < dt / 2 or abs(t - 9) < dt / 2:                # una sale a explorar
            i = int(rng.integers(0, SW))
            role[i], timer[i], heading[i] = 'explorer', rng.uniform(4, 6), rng.normal(0, 1, 2)
            heading[i] /= np.linalg.norm(heading[i])
        if abs(t - 6) < dt / 2:                                        # aparece una suelta
            role[SW], timer[SW], pos[SW], fade[SW] = 'stray', 6, darkest() + np.array([3, 1.5]), 0
        members = [i for i in range(N) if role[i] == 'swarm']
        centroid = pos[members].mean(0) if members else home
        home = home + away_from_light(home) * .25 * dt
        img = ground * (1 - upper[..., 3:]) * 1 + upper[..., :3]
        # Farol: halo que parpadea.
        for lp, r in lamps:
            size = int(r * 2 * T) // 4 * 4
            lamp_glow.setdefault(size, VL.radial(size) ** 3)
            px = to_px(lp)
            VL.add_sprite(img, lamp_glow[size], px[0], px[1], (1, .87, .7), .24 * .53 * VL.flicker(7, t))
        # Personaje que camina (con su mancha de contacto).
        if chars:
            fp = to_px(feet)
            L.contact(img, fp, T)
            h, w = walker.shape[:2]
            left, top = int(fp[0] - w / 2), int(fp[1] - h)
            v._paste(img, walker[..., :3] * .72, walker[..., 3:], left, top)
        glow_sum = 0
        for i in range(N):
            if role[i] == 'off':
                continue
            p = pos[i]
            steer = np.array([L.perlin(np.array([t * .3]), np.array([seed[i]]))[0] - .5,
                              L.perlin(np.array([seed[i]]), np.array([t * .3]))[0] - .5]) * 1.6
            if role[i] == 'swarm':
                if len(members) > 1:
                    steer += (centroid - p) * .35
                steer += (home - p) * .12
            elif role[i] == 'explorer':
                steer += heading[i] * .9
                timer[i] -= dt
                if timer[i] <= 0:
                    role[i] = 'swarm'
            else:
                steer *= 1.5
                timer[i] -= dt
            for j in range(N):
                if j != i and role[j] != 'off':
                    d = p - pos[j]
                    m2 = d @ d
                    if 1e-4 < m2 < .2:
                        steer += d / m2 * .06
            steer += away_from_light(p) * 1.6
            for c in chars:
                d = p - (c + np.array([0, .8]))
                mm = np.linalg.norm(d)
                if .01 < mm < 1.8:
                    steer += d / mm * (1 - mm / 1.8) * 3
            vel[i] = vel[i] * (1 - min(1, 1.5 * dt)) + steer * dt
            mx = 1.65 if role[i] == 'explorer' else 1.1
            sp = np.linalg.norm(vel[i])
            if sp > mx:
                vel[i] *= mx / sp
            pos[i] = p + vel[i] * dt
            near = [j for j in range(N) if j != i and role[j] != 'off' and ((pos[j] - pos[i]) ** 2).sum() < 9]
            coupling = np.mean([math.sin(phase[j] - phase[i]) for j in near]) if near else 0
            phase[i] += dt * (3.4 + .9 * coupling)
            blink = max(0, math.sin(phase[i])) ** 3
            if role[i] == 'stray' and timer[i] <= 0:
                fade[i] = max(0, fade[i] - dt * .5)
                if fade[i] <= 0:
                    role[i] = 'off'
                    continue
            else:
                fade[i] = min(1, fade[i] + dt * .7)
            a = (.15 + .85 * blink) * fade[i]
            px = to_px(pos[i])
            VL.add_sprite(img, glow, px[0], px[1], (.8, 1, .45), a)
            if role[i] == 'swarm':
                glow_sum += a
        if glow_sum > .2:
            px = to_px(centroid)
            VL.add_sprite(img, floor, px[0], px[1], (.55, .85, .35), min(.12, glow_sum * .012))
        img = img * mult
        out = v.to_img(img)
        d = ImageDraw.Draw(out)
        d.rectangle((0, 0, out.width, 26), fill=(0, 0, 0))
        label = ('Enjambre en lo oscuro, lejos del farol' if t < 3.5 else
                 'Un personaje pasa: se abren como el agua y se vuelven a juntar' if t < 11.5 else
                 'Parpadeo que se sincroniza entre vecinas')
        if 3 <= t < 7 or 9 <= t < 13:
            label += '  ·  una sale a explorar'
        if 6 <= t < 12:
            label += '  ·  una suelta que se pierde'
        d.text((10, 7), label, fill=(255, 255, 255))
        frames.append(out)
    import imageio.v2 as imageio
    wr = imageio.get_writer(args.salida, fps=FPS, codec='libx264', quality=8, macro_block_size=8)
    for fr in frames:
        wr.append_data(np.asarray(fr))
    wr.close()
    frames[FPS * 7].save(Path(args.salida).with_suffix('.jpg'), quality=90)
    print(args.salida, frames[0].size, len(frames), 'cuadros')


if __name__ == '__main__':
    main()
