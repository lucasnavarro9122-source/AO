"""Vista animada del clima V290 sin Unity (GIF): misma lógica y números que AOMapWeather.cs y
AOMapWeatherRoofsV290.cs (3 capas con parallax y viento, salpicaduras, agua que escurre por los techos con
roof_flow.json, goteo de los aleros y tinte que multiplica). La cámara se desplaza para que se note el parallax.

  python Tools/hd_remake/clima_vista_v290.py SALIDA.gif [--mapa 1] [--x 60 --y 19]

Es una aproximación para mostrar a Lucas; el juego real lo dibuja Unity (probar con AO Migrator > Clima)."""
import argparse, json, math, sys
from pathlib import Path
import numpy as np
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parent))
import dungeon_vista_p1 as v
import preview_luces as pl

ROOT = Path(__file__).resolve().parents[2]
S = 48                        # px por casilla en la salida
FPS, SECONDS = 12, 2.5
RAIN = [  # share, scale, length, alpha, speedMin, speedMax, parallax, landMin, landMax, lands  (AOMapWeather.RainLayers)
    (.45, .55, .6, .22, 7, 9, .8, 1.5, 5, False),
    (.40, .85, 1.0, .42, 11, 14, 1.0, 1.2, 6, True),
    (.15, 1.4, 1.9, .26, 17, 22, 1.25, 99, 99, False),
]
MARGIN = 1.5


def streak():
    w, h = 8, 64
    a = np.zeros((h, w, 4), np.float32)
    for y in range(h):
        along = 1 - y / (h - 1)                      # la fila 0 de la imagen es la cola (arriba)
        for x in range(w):
            across = 1 - abs((x + .5) / w * 2 - 1)
            a[y, x] = (220 / 255, 232 / 255, 1, across ** 1.6 * (1 - along) ** .7 * min(1, along * 8 + .3))
    return a


def ring():
    w, h = 32, 16
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.hypot((xx + .5) / w * 2 - 1, (yy + .5) / h * 2 - 1)
    a = np.clip(1 - np.abs(r - .75) * 5, 0, 1) + np.clip(.35 - r, 0, 1) * 1.2
    out = np.ones((h, w, 4), np.float32)
    out[..., 3] = np.clip(a, 0, 1)
    return out


def load_roofs(num):
    p = ROOT / 'Assets/Resources/AOMigrator/WorldV07/roof_flow.json'
    if not p.exists():
        return None
    data = json.loads(p.read_text('utf-8'))
    for e in data['maps']:
        if e['map'] == num:
            flat = np.zeros(e['width'] * e['height'], np.int32)
            i = 0
            r = e['runs']
            for k in range(0, len(r), 2):
                flat[i:i + r[k + 1]] = r[k]
                i += r[k + 1]
            return flat.reshape(e['height'], e['width']), e['roofs'], 32 / data['cell']
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('salida')
    ap.add_argument('--mapa', type=int, default=1)
    ap.add_argument('--x', type=int, default=60)
    ap.add_argument('--y', type=int, default=19)
    args = ap.parse_args()
    pl.S, pl.HD, pl.T = v.K, True, v.T
    m = pl.load(args.mapa)
    m['env'] = dict(m['env'], baseLight=0xF5F5F5)
    VW, VH, PAN = 13, 9, 2                           # vista en casillas y paneo total
    base = v.render_original(m, args.x, args.y, VW + PAN + 1, VH)
    img0 = Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8)).resize(((VW + PAN + 1) * S, VH * S), Image.LANCZOS)
    base = np.asarray(img0, np.float32) / 255
    tint = 1 + (np.array([.42, .48, .6]) - 1) * .2   # AOMapWeather.UpdateTint (multiplica)
    roofs = load_roofs(args.mapa)
    rng = np.random.default_rng(7)
    STREAK, RING = streak(), ring()
    cache = {}

    def sprite(key, spr, sx, sy, angle):
        k = (key, round(sx, 2), round(sy, 2), round(angle))
        if k not in cache:
            im = Image.fromarray((spr * 255).astype(np.uint8), 'RGBA')
            w, h = max(1, round(im.width / 64 * S * sx if key == 's' else im.width / 32 * S * sx)), \
                   max(1, round(im.height / 64 * S * sy if key == 's' else im.height / 32 * S * sy))
            im = im.resize((w, h), Image.BILINEAR)
            if angle:
                im = im.rotate(angle, Image.BILINEAR, expand=True)
            cache[k] = np.asarray(im, np.float32) / 255
        return cache[k]

    def stamp(img, a, cx, cy, alpha, tint_rgb=(1, 1, 1)):
        h, w = a.shape[:2]
        left, top = int(cx - w / 2), int(cy - h / 2)
        H, W = img.shape[:2]
        x1, y1, x2, y2 = max(0, left), max(0, top), min(W, left + w), min(H, top + h)
        if x1 < x2 and y1 < y2:
            sub = a[y1 - top:y2 - top, x1 - left:x2 - left]
            img[y1:y2, x1:x2] += sub[..., :3] * np.array(tint_rgb, np.float32) * sub[..., 3:] * alpha

    # Mundo: x = casillas desde la izquierda del mapa, y negativa hacia abajo (como Unity).
    half_w, half_h = VW / 2, VH / 2
    cam0 = np.array([args.x - 1 + half_w, -(args.y - 1) - half_h])
    wind = -14 * (.35 + 1.1 * .3)
    n = 200
    layers = []
    acc = [0, 0, 0]
    for i in range(n):
        pick = max(range(3), key=lambda l: RAIN[l][0] * (i + 1) - acc[l])
        acc[pick] += 1
        layers.append(pick)

    def respawn(i, cam, anywhere):
        L = RAIN[layers[i]]
        origin = cam * L[6]
        x = rng.uniform(-half_w - MARGIN, half_w + MARGIN)
        y = rng.uniform(-half_h, half_h + MARGIN) if anywhere or L[9] else half_h + rng.uniform(0, MARGIN)
        speed = rng.uniform(L[4], L[5])
        ang = wind * (.85 + .3 * L[6])
        rad = math.radians(ang)
        size = L[1] * rng.uniform(.85, 1.15)
        return dict(p=origin + (x, y), v=np.array([math.sin(rad), -math.cos(rad)]) * speed, age=0.0,
                    life=rng.uniform(L[7], L[8]) / speed, alpha=L[3] * rng.uniform(.75, 1.1),
                    sx=size, sy=size * L[2] * speed / 12, ang=ang)

    drops = [respawn(i, cam0, True) for i in range(n)]
    splashes, rivers = [], []

    def roof_at(p):
        if roofs is None:
            return 0
        lab, _, cpu = roofs
        gx, gy = int(math.floor(p[0] * cpu)), int(math.floor(-p[1] * cpu))
        return int(lab[gy, gx]) if 0 <= gy < lab.shape[0] and 0 <= gx < lab.shape[1] else 0

    def splash(p, strength):
        if sum(1 for s in splashes if s['age'] < .28) >= 26:
            return
        splashes.append(dict(p=np.array(p, float), age=0.0, st=strength))

    frames = []
    dt = 1 / FPS
    for f in range(int(FPS * SECONDS)):
        cam = cam0 + (PAN * f / (FPS * SECONDS), 0)
        ox = (cam[0] - half_w) - (args.x - 1)           # desplazamiento en casillas dentro del fondo
        img = base[:, int(ox * S):int(ox * S) + VW * S].copy() * tint
        to_px = lambda wp: ((wp[0] - (cam[0] - half_w)) * S, ((cam[1] + half_h) - wp[1]) * S)
        for i, d in enumerate(drops):
            L = RAIN[layers[i]]
            origin = cam * L[6]
            d['age'] += dt
            p = d['p'] + d['v'] * dt
            local = p - origin
            if d['age'] >= d['life'] or local[1] < -half_h - MARGIN:
                if L[9] and d['age'] >= d['life']:
                    wp = cam + local
                    k = roof_at(wp)
                    if k:
                        if rng.random() < .5 and sum(1 for r in rivers) < 24:
                            info = roofs[1][k - 1]
                            fl = np.array([0, -1.0]) if info['mode'] == 1 else np.array([(-1 if wp[0] < info['ridge'] else 1) * .86, -.5])
                            rivers.append(dict(p=wp.copy(), v=fl * rng.uniform(1.2, 1.9), roof=k, age=0.0,
                                               life=rng.uniform(.7, 1.6), st=1, ang=math.degrees(math.atan2(fl[0], -fl[1]))))
                        else:
                            splash(wp, .55)
                    else:
                        splash(wp, 1)
                drops[i] = respawn(i, cam, False)
                continue
            span = 2 * (half_w + MARGIN)
            if local[0] < -half_w - MARGIN: p[0] += span; local[0] += span
            elif local[0] > half_w + MARGIN: p[0] -= span; local[0] -= span
            d['p'] = p
            a = d['alpha'] * min(1, d['age'] * 20)
            spr = sprite('s', STREAK, d['sx'], d['sy'], d['ang'])
            x, y = to_px(cam + local)
            stamp(img, spr, x + math.sin(math.radians(d['ang'])) * -.4 * d['sy'] * S,
                  y - math.cos(math.radians(d['ang'])) * .4 * d['sy'] * S, a)
        for r in list(rivers):
            if r['st'] == 1:
                r['age'] += dt
                r['p'] = r['p'] + r['v'] * dt
                if roof_at(r['p']) != r['roof']:
                    r['st'], r['v'], r['floor'] = 2, np.array([r['v'][0] * .2, -.5]), r['p'][1] - rng.uniform(1.7, 2.3)
                    alpha, spr = .6, sprite('s', STREAK, .8, .2, 0)
                elif r['age'] >= r['life']:
                    r['dead'] = True; continue
                else:
                    alpha = .5 * min(1, r['age'] * 6) * min(1, (r['life'] - r['age']) * 3)
                    spr = sprite('s', STREAK, .75, .32, r['ang'])
            else:
                r['v'][1] -= 16 * dt
                r['p'] = r['p'] + r['v'] * dt
                if r['p'][1] <= r['floor']:
                    splash((r['p'][0], r['floor']), .9); r['dead'] = True; continue
                alpha, spr = .6, sprite('s', STREAK, .8, min(.55, max(.2, .2 - r['v'][1] * .035)), 0)
            x, y = to_px(r['p'])
            stamp(img, spr, x, y, alpha, (.85, .92, 1))
        rivers[:] = [r for r in rivers if not r.get('dead')]
        for s in list(splashes):
            s['age'] += dt
            k = s['age'] / .28
            if k >= 1:
                s['dead'] = True; continue
            sc = (.25 + k * .55) * (.7 + .3 * s['st'])
            x, y = to_px(s['p'])
            stamp(img, sprite('r', RING, sc, sc, 0), x, y, .38 * s['st'] * (1 - k), (.85, .9, 1))
        splashes[:] = [s for s in splashes if not s.get('dead')]
        frames.append(Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)))
    out = Path(args.salida)
    frames[len(frames) // 2].save(out.with_suffix('.jpg'), quality=90)
    pal = [fr.quantize(128, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE) for fr in frames]
    pal[0].save(out, save_all=True, append_images=pal[1:], duration=int(1000 / FPS), loop=0, optimize=True)
    print(out, frames[0].size, f'{out.stat().st_size / 1e6:.1f} MB')


if __name__ == '__main__':
    main()
