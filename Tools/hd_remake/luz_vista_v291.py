"""Vista sin Unity de la luz V291: sombras de personajes del lado opuesto a la luz, nubes que tapan el sol y la
luna, noche con luna tenue (Purkinje) y manchas de luna solo donde se abren las nubes.
Misma lógica y números que AOSkyV291.cs y AOCharacterShadowsV291.cs; la luz del mapa es la del AO (Original).

  python Tools/hd_remake/luz_vista_v291.py SALIDA.jpg [--mapa 1] [--x 22 --y 12] [--hd]

Es una aproximación para Lucas: el juego real lo dibuja Unity (probar con AO Migrator > Clima (depuración))."""
import argparse, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, str(Path(__file__).resolve().parent))
import dungeon_vista_p1 as v
import preview_luces as pl

OUT_T = 48                                   # px por casilla en la salida
DAY = [(120, 120, 120)] * 4 + [(138, 138, 138), (156, 156, 145), (170, 170, 155), (185, 185, 185), (200, 200, 200),
       (220, 220, 220), (235, 235, 235), (245, 245, 245), (255, 255, 255), (255, 255, 255), (255, 255, 255),
       (245, 245, 245), (230, 230, 230), (220, 220, 220), (200, 200, 180), (180, 160, 160), (160, 160, 160),
       (140, 140, 140), (120, 120, 140), (120, 120, 120)]      # AOMapLighting.Day (ModMetereologia.bas)


def day_color(hour):
    hour %= 24
    cur, prev = int(hour), (int(hour) + 23) % 24
    f = hour - cur
    return tuple(int(DAY[prev][i] + (DAY[cur][i] - DAY[prev][i]) * f) for i in range(3))


def smooth(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


def inv_lerp(a, b, x):
    return (x - a) / (b - a)


def sky(hour, coverage):
    """AOSkyV291.UpdateSkyLight."""
    hour %= 24
    sun = smooth(inv_lerp(5.5, 7.5, hour)) * (1 - smooth(inv_lerp(16.5, 18.5, hour)))
    moon = 1 - sun
    day = 6 <= hour < 18
    t = (hour - 6) / 12 if day else ((hour - 18) % 24) / 12
    phi = t * math.pi
    to_light = np.array([math.cos(phi), -0.6 * math.sin(phi)])
    to_light /= np.linalg.norm(to_light)
    elevation = 6 + ((64 if day else 48) - 6) * math.sin(phi)
    length = min(max(1 / math.tan(math.radians(elevation)), 0.5), 1.3)
    direct = 1 - smooth(inv_lerp(0.6, 0.92, coverage))
    alpha = max(sun * 0.45, moon * 0.25) * direct
    return dict(sun=sun, moon=moon, dir=-to_light, len=length, alpha=alpha)


# Ruido de gradiente (como Mathf.PerlinNoise: 0..1, suave); no es idéntico, alcanza para la vista.
_rng = np.random.default_rng(11)
_grad = _rng.normal(size=(256, 2))
_grad /= np.linalg.norm(_grad, axis=1)[:, None]
_perm = _rng.permutation(256)


def perlin(x, y):
    xi, yi = np.floor(x).astype(int), np.floor(y).astype(int)
    xf, yf = x - xi, y - yi
    def g(ix, iy, dx, dy):
        h = _perm[(_perm[ix & 255] + iy) & 255]
        return _grad[h, 0] * dx + _grad[h, 1] * dy
    u, w = xf * xf * (3 - 2 * xf), yf * yf * (3 - 2 * yf)
    n00, n10 = g(xi, yi, xf, yf), g(xi + 1, yi, xf - 1, yf)
    n01, n11 = g(xi, yi + 1, xf, yf - 1), g(xi + 1, yi + 1, xf - 1, yf - 1)
    n = (n00 + (n10 - n00) * u) + ((n01 + (n11 - n01) * u) - (n00 + (n10 - n00) * u)) * w
    return np.clip(n * 0.9 + 0.5, 0, 1)


def field(qx, qy, coverage):
    """AOSkyV291.Field: nubosidad 0..1 en espacio nube (1 = una mancha de 7 unidades)."""
    n = 0.65 * perlin(qx * 0.23 + 11.3, qy * 0.23 + 5.7) + 0.35 * perlin(qx * 0.61 + 2.1, qy * 0.61 + 9.4)
    x = np.clip((n - (1 - coverage) + 0.16) / 0.32, 0, 1)
    return x * x * (3 - 2 * x)


def silhouette(npc):
    im = pl.npc_img(npc)
    return None if im is None else np.asarray(im, np.float32)[..., 3] / 255


def stamp_shadow(img, sil, feet_px, angle_deg, length, alpha, width=0.9):
    """Silueta negra apoyada en los pies, estirada según el largo y girada hacia donde cae (como el C#)."""
    h, w = sil.shape
    sh = max(1, int(round(h * length)))
    w = max(1, int(round(w * width)))
    s = Image.fromarray((sil * 255).astype(np.uint8), 'L').resize((w, sh), Image.BILINEAR)
    size = 2 * max(w, sh) + 4
    canvas = Image.new('L', (size, size), 0)
    canvas.paste(s, (size // 2 - w // 2, size // 2 - sh))          # pie del sprite en el centro
    canvas = canvas.rotate(round(angle_deg / 5) * 5, Image.BILINEAR)     # pasos de 5° como el C#                 # antihorario, como el eje z de Unity
    a = np.asarray(canvas, np.float32) / 255 * alpha
    left, top = int(feet_px[0] - size // 2), int(feet_px[1] - size // 2)
    H, W = img.shape[:2]
    x1, y1, x2, y2 = max(0, left), max(0, top), min(W, left + size), min(H, top + size)
    if x1 < x2 and y1 < y2:
        img[y1:y2, x1:x2] *= (1 - a[y1 - top:y2 - top, x1 - left:x2 - left])[..., None]


def contact(img, feet_px, T, alpha=0.26):
    w, h = int(0.72 * T), int(0.36 * T)
    yy, xx = np.mgrid[0:h, 0:w]
    f = np.clip(1 - np.hypot((xx + .5) / w * 2 - 1, (yy + .5) / h * 2 - 1), 0, 1) ** 2 * alpha
    left, top = int(feet_px[0] - w / 2), int(feet_px[1] - h / 2)
    H, W = img.shape[:2]
    x1, y1, x2, y2 = max(0, left), max(0, top), min(W, left + w), min(H, top + h)
    if x1 < x2 and y1 < y2:
        img[y1:y2, x1:x2] *= (1 - f[y1 - top:y2 - top, x1 - left:x2 - left])[..., None]


def render(m, x0, y0, W, H, hour, coverage, cloud_offset):
    T = v.T
    amb = day_color(hour)
    m = dict(m, env=dict(m['env'], baseLight=(amb[0] << 16) | (amb[1] << 8) | amb[2]))
    cor = v.corners_original(m)
    ground = dict(m, cells=[c for c in m['cells'] if c['layer'] in (1, 2)], npcs=[])
    upper = dict(m, cells=[c for c in m['cells'] if c['layer'] in (3, 4)])
    img = np.zeros((H * T, W * T, 3), np.float32)

    pending = {}                                  # sombras que se dibujan justo antes de su personaje

    def paint(mm):
        for s, x, y in v.draw_list(mm, x0, y0, W, H):
            if (x, y) in pending:
                pending.pop((x, y))()
            left, top = v.place(s, x, y, x0, y0)
            a = np.asarray(s, np.float32) / 255
            cy, cx = min(max(y - m['ymin'], 0), cor.shape[0] - 1), min(max(x - m['xmin'], 0), cor.shape[1] - 1)
            v._paste(img, a[..., :3] * v.tint(s.size, cor[cy, cx]), a[..., 3:], left, top)

    paint(ground)
    st = sky(hour, coverage)
    to_px = lambda wx, wy: ((wx - (x0 - 1)) * T, (-(y0 - 1) - wy) * T)
    ambient = 0.47 + 0.53 * st['sun']
    lights = []
    for L in m['lights']:
        rng = L['range'] - 99 if L['range'] >= 100 else L['range']
        if rng <= 0:
            continue
        c = L['color'] & 0xFFFFFF
        lum = (0.3 * ((c >> 16) & 255) + 0.59 * ((c >> 8) & 255) + 0.11 * (c & 255)) / 255
        lights.append((np.array([L['x'] - 0.5, -L['y'] + 0.5]), rng + 0.5, min(1, lum * 1.2)))
    # Sombras: justo debajo de cada personaje en su fila (como el C#: orden del personaje - 1).
    for n in m['npcs']:
        if not (x0 - 1 <= n['x'] < x0 + W + 1 and y0 - 1 <= n['y'] < y0 + H + 2):
            continue
        sil = silhouette(n)
        if sil is None:
            continue
        feet = np.array([n['x'] - 0.5, -n['y']])
        qx, qy = (feet - cloud_offset) / 7
        cloud = float(field(np.array([qx]), np.array([qy]), coverage)[0])
        d, ln, al = st['dir'], st['len'], st['alpha'] * (1 - 0.85 * cloud)
        best, bp, bd = 0, None, 0
        for pos, radius, power in lights:
            dist = np.linalg.norm(feet - pos)
            if dist >= radius:
                continue
            wgt = power * (1 - dist / radius) ** 2 * min(max(1.15 - ambient, 0), 1)
            if wgt > best:
                best, bp, bd = wgt, pos, dist
        pa = min(best * 1.3, 1) * 0.5
        if pa > al and bd > 0.2:
            d, ln, al = (feet - bp) / bd, min(max(0.45 + bd * 0.2, 0.45), 1.1), pa
        angle = math.degrees(math.atan2(-d[0], d[1]))
        fp = to_px(*feet)
        pending[(n['x'], n['y'])] = (lambda sil=sil, fp=fp, angle=angle, ln=ln, al=al:
                                     (stamp_shadow(img, sil, fp, angle, ln, al), contact(img, fp, T)))
    paint(upper)

    # Cielo: tinte de noche (multiplica, más frío arriba), sombras de nubes y luz de luna en los claros.
    Hh, Ww = img.shape[:2]
    ys, xs = np.mgrid[0:Hh:8, 0:Ww:8]
    wx = xs / T + (x0 - 1)
    wy = -(ys / T) - (y0 - 1)
    cloud = field((wx - cloud_offset[0]) / 7, (wy - cloud_offset[1]) / 7, coverage)
    partly = 1 - smooth(inv_lerp(0.62, 0.92, coverage))
    shade = (st['sun'] * 0.5 + st['moon'] * 0.3) * partly * cloud
    shade_col = np.array([0.7, 0.72, 0.86]) + (np.array([0.6, 0.64, 0.76]) - np.array([0.7, 0.72, 0.86])) * st['sun']
    mult = 1 + (shade_col[None, None, :] - 1) * shade[..., None]
    grade_a = st['moon'] * 0.42 * (0.55 + 0.45 * (1 - ys / Hh))[..., None]      # arriba = lejos = más azul
    mult *= 1 + (np.array([0.6, 0.68, 0.95])[None, None, :] - 1) * grade_a
    moon_light = st['moon'] * 0.1 * (1 - 0.8 * smooth(inv_lerp(0.5, 0.92, coverage)))
    add = ((1 - cloud) ** 2 * moon_light)[..., None] * np.array([0.55, 0.66, 0.95])[None, None, :]
    up = lambda a: np.asarray(Image.fromarray(a.astype(np.float32)).resize((Ww, Hh), Image.BILINEAR)) if a.ndim == 2 else None
    mult_full = np.stack([up(mult[..., i]) for i in range(3)], -1)
    add_full = np.stack([up(add[..., i]) for i in range(3)], -1)
    img = img * mult_full + add_full
    return v.to_img(img).resize((W * OUT_T, H * OUT_T), Image.LANCZOS)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('salida')
    ap.add_argument('--mapa', type=int, default=1)
    ap.add_argument('--x', type=int, default=22)
    ap.add_argument('--y', type=int, default=12)
    ap.add_argument('--hd', action='store_true', help='texturas HD de Resources/AOMigratorHD')
    args = ap.parse_args()
    ap_hd = '--hd' in sys.argv
    pl.S, pl.HD, pl.T = v.K, ap_hd, v.T          # por defecto texturas originales (Ullathorpe volvió al original)
    m = pl.load(args.mapa)
    W, H = 13, 9
    offset = np.array([args.x + 3.0, -args.y - 1.0])
    panels = [('Mañana 9 h · nubes sueltas', 9, 0.4), ('Atardecer 17:30', 17.5, 0.35),
              ('Noche 23 h · luna y faroles', 23, 0.35), ('Noche nublada · luna solo en los claros', 23, 0.62)]
    tiles = []
    for title, hour, cov in panels:
        im = render(m, args.x, args.y, W, H, hour, cov, offset)
        d = ImageDraw.Draw(im)
        d.rectangle((0, 0, im.width, 22), fill=(0, 0, 0))
        d.text((8, 5), title, fill=(255, 255, 255))
        tiles.append(im)
    w, h = tiles[0].size
    sheet = Image.new('RGB', (w * 2 + 6, h * 2 + 6), (0, 0, 0))
    for i, im in enumerate(tiles):
        sheet.paste(im, ((i % 2) * (w + 6), (i // 2) * (h + 6)))
    sheet.save(args.salida, quality=90)
    print(args.salida, sheet.size)


if __name__ == '__main__':
    main()
