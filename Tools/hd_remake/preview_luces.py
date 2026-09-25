"""Vista previa sin Unity: recorte de un mapa con luz actual (AOMapLighting) contra la luz propuesta
(luces suaves, brillo, sombras de contacto, partículas, personajes que resaltan).

  python Tools/hd_remake/preview_luces.py SALIDA [--mapa 1] [--escala 4] [--hd]

--hd usa las texturas de Resources/AOMigratorHD (4x) donde existan; el resto, el original ampliado.
Es una vista de diseño para Lucas y Arte: el juego real lo dibuja Unity."""
import json, math, random, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
WORLD = ROOT / 'Assets/Resources/AOMigrator/WorldV07'
S = 1          # escala (4 = remaster 4x)
HD = False
T = 32
_tex = {}


def tex(file_num):
    if file_num not in _tex:
        hd = ROOT / 'Assets/Resources/AOMigratorHD/WorldV07/Textures' / f'tex_{file_num}.png'
        p = WORLD / 'Textures' / f'tex_{file_num}.png'
        if HD and hd.exists() and S == 4:
            _tex[file_num] = Image.open(hd).convert('RGBA')
        elif p.exists():
            t = Image.open(p).convert('RGBA')
            _tex[file_num] = t.resize((t.width * S, t.height * S), Image.NEAREST) if S > 1 else t
        else:
            _tex[file_num] = None
    return _tex[file_num]


def crop(frame):
    t = tex(frame['fileNum'])
    if t is None: return None
    return t.crop((frame['sx'] * S, frame['sy'] * S, (frame['sx'] + frame['width']) * S, (frame['sy'] + frame['height']) * S))


def decode(packed):
    packed &= 0xFFFFFF
    return np.array([(packed >> 16) & 255, (packed >> 8) & 255, packed & 255], np.float32) / 255


def load(num):
    m = json.loads((WORLD / 'Maps' / f'map_{num}.json').read_text('utf-8-sig'))
    env = json.loads((WORLD / 'map_environment.json').read_text('utf-8-sig'))
    m['env'] = next((e for e in env['maps'] if e['mapNumber'] == num), {})
    return m


def albedo(m, x0, y0, w, h, shadows=False, chars=True, margin=8):
    """Dibuja capas 1-4 como el juego: pivote abajo al centro del tile; 3 y personajes ordenados por fila."""
    ox, oy = (x0 - 1 - margin) * T, (y0 - 1 - margin) * T
    W, H = (w + 2 * margin) * T, (h + 2 * margin) * T
    img = Image.new('RGBA', (W, H), (0, 0, 0, 255))
    sprites = {s['id']: s for s in m['sprites']}
    inside = lambda c: x0 - margin <= c['x'] < x0 + w + margin and y0 - margin <= c['y'] < y0 + h + margin
    def put(sprite_img, x, y, shadow=False):
        sw, sh = sprite_img.size
        left, top = (x - 1) * T + T // 2 - sw // 2 - ox, y * T - sh - oy
        if shadow:
            d = ImageDraw.Draw(sh_layer)
            ew = max(18, int(sw * .55)); d.ellipse((left + sw // 2 - ew // 2, y * T - oy - 9, left + sw // 2 + ew // 2, y * T - oy + 3), fill=(0, 0, 0, 120))
        img.alpha_composite(sprite_img, (left, top))
    cells = [c for c in m['cells'] if inside(c)]
    roofs = {(c['x'], c['y']) for c in cells if c['layer'] == 4}
    for layer in (1, 2):
        for c in cells:
            if c['layer'] == layer and c['sprite'] in sprites:
                s = crop(sprites[c['sprite']])
                if s: put(s, c['x'], c['y'])
    sh_layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    row = []
    for c in cells:
        if c['layer'] == 3 and c['sprite'] in sprites: row.append((c['y'], c['x'], 'cell', c))
    if chars:
        for n in m['npcs']:
            if inside(n): row.append((n['y'], n['x'], 'npc', n))
    row.sort(key=lambda r: (r[0], r[1]))
    char_boxes = []
    if shadows:  # sombras de contacto primero, debajo de todo lo que está parado
        for _, _, kind, c in row:
            s = crop(sprites[c['sprite']]) if kind == 'cell' else npc_img(c)
            if s and (kind == 'npc' or s.size[1] >= 48): put_shadow(sh_layer, s, c['x'], c['y'], ox, oy)
        img.alpha_composite(sh_layer.filter(ImageFilter.GaussianBlur(3 * S)))
    for _, _, kind, c in row:
        s = crop(sprites[c['sprite']]) if kind == 'cell' else npc_img(c)
        if not s: continue
        sw, sh = s.size
        left, top = (c['x'] - 1) * T + T // 2 - sw // 2 - ox, c['y'] * T - sh - oy
        img.alpha_composite(s, (left, top))
        if kind == 'npc': char_boxes.append((s, left, top, (c['x'] - 1) * T + T // 2 - ox, c['y'] * T - 8 * S - oy))
    roof_rects = []
    for c in cells:
        if c['layer'] == 4 and c['sprite'] in sprites:
            s = crop(sprites[c['sprite']])
            if s:
                put(s, c['x'], c['y'])
                l4, t4 = (c['x'] - 1) * T + T // 2 - s.width // 2 - ox, c['y'] * T - s.height - oy
                roof_rects.append((l4, t4, s))
    box = (margin * T, margin * T, (margin + w) * T, (margin + h) * T)
    def under_roof(px, py):
        for l4, t4, rs in roof_rects:
            if l4 <= px < l4 + rs.width and t4 <= py < t4 + rs.height and rs.getpixel((px - l4, py - t4))[3] > 0: return True
        return False
    chars_out = [(s, l - margin * T, t - margin * T) for s, l, t, px, py in char_boxes if not under_roof(px, py)]
    return img.crop(box), chars_out


def put_shadow(layer, s, x, y, ox, oy):
    sw, sh = s.size
    d = ImageDraw.Draw(layer)
    cx, base = (x - 1) * T + T // 2 - ox, y * T - oy
    ew = max(20 * S, int(sw * .6))
    d.ellipse((cx - ew // 2, base - 8 * S, cx + ew // 2, base + 4 * S), fill=(0, 0, 0, 140))


def npc_img(n):
    """Cuerpo y cabeza apoyados en la misma base (como AOCharacterRenderer: cabeza subida -headOffsetY px)."""
    d = next((d for d in n.get('directions', []) if d['heading'] == 3), None)
    if not d or not d['body']: return None
    body = crop(d['body'][0])
    if body is None: return None
    head = crop(d['head'][0]) if d.get('head') else None
    if head is None: return body
    up, hx = -n.get('headOffsetY', 0), n.get('headOffsetX', 0)
    W = max(body.width, head.width) + 2 * abs(hx); H = max(body.height, head.height + max(0, up)) + max(0, -up)
    canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    base = H - max(0, -up)
    canvas.alpha_composite(body, ((W - body.width) // 2, base - body.height))
    canvas.alpha_composite(head, ((W - head.width) // 2 + hx, base - head.height - up))
    bbox = canvas.getbbox()
    # Recortar solo arriba y a los costados: la base (pies) queda abajo del lienzo.
    return canvas.crop((0, bbox[1], W, H))


def light_actual(m, x0, y0, w, h, hour=0):
    """AOMapLighting.Build: color por esquina de tile, interpolado dentro del tile."""
    base = m['env'].get('baseLight', 0)
    amb = decode(base) if base else np.array([120, 120, 120], np.float32) / 255
    corners = np.tile(amb, (h, w, 4, 1))
    for L in m['lights']:
        rng = L['range'] - 99 if L['range'] >= 100 else L['range']
        if rng <= 0 or rng > 128: continue
        col = decode(L['color'])
        for y in range(L['y'] - rng, L['y'] + rng + 1):
            for x in range(L['x'] - rng, L['x'] + rng + 1):
                ty, tx = y - y0, x - x0
                if not (0 <= ty < h and 0 <= tx < w): continue
                if L['range'] >= 100:
                    radius = rng * 32 + 16
                    for c in range(4):
                        vx, vy = x * 32 + (32 if c >= 2 else 0), y * 32 + (32 if (c & 1) == 0 else 0)
                        d2 = (L['x'] * 32 + 16 - vx) ** 2 + (L['y'] * 32 + 16 - vy) ** 2
                        if d2 <= radius * radius:
                            t = d2 / (radius * radius); corners[ty, tx, c] = col + (corners[ty, tx, c] - col) * t
                elif abs(x - L['x']) < rng and abs(y - L['y']) < rng:
                    corners[ty, tx, :] = col
    out = np.zeros((h * T, w * T, 3), np.float32)
    u = (np.arange(T) + .5) / T
    for ty in range(h):
        for tx in range(w):
            ll, ul, lr, ur = corners[ty, tx]
            top = ul[None, :] * (1 - u)[:, None] + ur[None, :] * u[:, None]
            bot = ll[None, :] * (1 - u)[:, None] + lr[None, :] * u[:, None]
            out[ty * T:(ty + 1) * T, tx * T:(tx + 1) * T] = top[None, :, :] * (1 - u)[:, None, None] + bot[None, :, :] * u[:, None, None]
    return out


def warm(col):
    # Las luces blancas del original se leen como faroles/antorchas cálidos.
    return np.array([1.0, .74, .46], np.float32) if col.min() > .85 else col


def light_propuesta(m, x0, y0, w, h, ambient, strength=1.05):
    H, W = h * T, w * T
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    L = np.tile(np.array(ambient, np.float32), (H, W, 1))
    glow = np.zeros((H, W, 3), np.float32)
    sources = []
    spr = {sp['id']: sp for sp in m['sprites']}
    tall = {(c['x'], c['y']): spr[c['sprite']]['height'] for c in m['cells'] if c['layer'] == 3 and c['sprite'] in spr and spr[c['sprite']]['height'] >= 64}
    for l in m['lights']:
        rng = l['range'] - 99 if l['range'] >= 100 else l['range']
        cx, cy = (l['x'] - x0) * T + T // 2, (l['y'] - y0) * T + T // 2
        r = (max(2.5, rng * 1.25) if l['range'] >= 100 else max(3.5, rng * 1.6)) * T
        if cx < -r or cy < -r or cx > W + r or cy > H + r: continue
        raw = decode(l['color'])
        moon = raw.min() <= .85 and raw[2] - raw[0] >= .1 and raw.max() - raw.min() < .25   # luna: suave (AOLighting2DV283)
        col = warm(raw)
        d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / r
        fall = np.clip(1 - d, 0, 1) ** 2
        L += col * fall[..., None] * (strength * .55 / 1.05 if moon else strength)
        L += col * (np.clip(1 - d / 1.8, 0, 1) ** 3)[..., None] * .22  # luz de relleno lejana
        lamp_y = cy + T // 2 - tall[(l['x'], l['y'])] * S + 14 * S if (l['x'], l['y']) in tall else cy
        core = np.exp(-(((xx - cx) ** 2 + (yy - lamp_y) ** 2) / (18 * S) ** 2))
        glow += col * core[..., None] * .45
        sources.append((cx, cy, col))
    return L, glow, sources


def to_np(img): return np.asarray(img.convert('RGB'), np.float32) / 255


def finish(rgb, glow, particles, x0, y0, seed=3):
    rgb = rgb + glow
    bright = np.clip(rgb - .78, 0, None)
    bloom = np.asarray(Image.fromarray((np.clip(bright, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(10 * S)), np.float32) / 255
    rgb = rgb + bloom * .9
    H, W, _ = rgb.shape
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    vig = 1 - .42 * (((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2) ** 1.2
    rgb *= np.clip(vig, .35, 1)[..., None]
    lum = rgb.mean(axis=2, keepdims=True)
    rgb = lum + (rgb - lum) * 1.15
    rgb = (rgb - .5) * 1.06 + .5
    img = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8)).convert('RGBA')
    fx = Image.new('RGBA', img.size, (0, 0, 0, 0)); d = ImageDraw.Draw(fx)
    rnd = random.Random(seed)
    for p in particles:
        for _ in range(2):
            px, py = (p['x'] - x0) * T + rnd.randint(0, T - 1), (p['y'] - y0) * T + rnd.randint(-10 * S, T - 1)
            if 0 <= px < W and 0 <= py < H:
                c = rnd.choice([(255, 214, 120), (190, 255, 150), (160, 200, 255)])
                d.ellipse((px - 2 * S, py - 2 * S, px + 2 * S, py + 2 * S), fill=c + (230,))
    fx = Image.alpha_composite(fx.filter(ImageFilter.GaussianBlur(2.2 * S)), fx)
    img.alpha_composite(fx)
    return img


def highlight_characters(img, chars, L, rim=(1.0, .86, .6)):
    """Personajes siempre legibles: se iluminan con un mínimo propio, contorno oscuro y borde de luz."""
    for s, left, top in chars:
        a = np.asarray(s.split()[3], np.float32) / 255
        rgb = np.asarray(s.convert('RGB'), np.float32) / 255
        h, w = a.shape
        x1, y1 = max(0, left), max(0, top)
        x2, y2 = min(img.width, left + w), min(img.height, top + h)
        if x2 <= x1 or y2 <= y1: continue
        sub = L[y1:y2, x1:x2]
        lit = rgb[y1 - top:y2 - top, x1 - left:x2 - left] * np.maximum(sub, .78)
        mask = Image.fromarray((a * 255).astype(np.uint8))
        outline = np.asarray(mask.filter(ImageFilter.MaxFilter(2 * S + 1)), np.float32) / 255 - a
        top_edge = np.clip(a - np.roll(a, S, axis=0), 0, 1)
        lit = lit + np.array(rim) * top_edge[y1 - top:y2 - top, x1 - left:x2 - left, None] * .55
        base = np.asarray(img.convert('RGB'), np.float32)[y1:y2, x1:x2] / 255
        aa = a[y1 - top:y2 - top, x1 - left:x2 - left, None]
        oo = np.clip(outline[y1 - top:y2 - top, x1 - left:x2 - left, None], 0, 1) * .75
        base = base * (1 - oo)
        out = base * (1 - aa) + np.clip(lit, 0, 1) * aa
        img.paste(Image.fromarray((out * 255).astype(np.uint8)), (x1, y1))
    return img


def best_window(m, w, h):
    best, pick = -1, (1, 1)
    for y0 in range(m['ymin'], m['ymax'] - h + 2, 2):
        for x0 in range(m['xmin'], m['xmax'] - w + 2, 2):
            inside = lambda o: x0 <= o['x'] < x0 + w and y0 <= o['y'] < y0 + h
            score = sum(inside(l) for l in m['lights']) * 2 + sum(inside(n) for n in m['npcs']) * 3 + sum(inside(p) for p in m['particles'])
            score += sum(1 for c in m['cells'] if c['layer'] == 3 and inside(c)) * .05
            if score > best: best, pick = score, (x0, y0)
    return pick


def panel(num, ambient, out, window=None, w=25, h=19):
    global HD
    m = load(num)
    x0, y0 = window or best_window(m, w, h)
    hd, HD = HD, False; _tex.clear()          # izquierda: siempre las texturas originales
    base, _ = albedo(m, x0, y0, w, h)
    HD = hd; _tex.clear()
    actual = np.clip(to_np(base) * light_actual(m, x0, y0, w, h), 0, 1)
    lit_base, chars = albedo(m, x0, y0, w, h, shadows=True)
    L, glow, _ = light_propuesta(m, x0, y0, w, h, ambient)
    prop = finish(to_np(lit_base) * L, glow, m['particles'], x0, y0)
    prop = highlight_characters(prop, chars, L)
    a = Image.fromarray((actual * 255).astype(np.uint8))
    sheet = Image.new('RGB', (a.width * 2 + 12, a.height), (20, 20, 20))
    sheet.paste(a, (0, 0)); sheet.paste(prop.convert('RGB'), (a.width + 12, 0))
    sheet.save(out)
    print(f'mapa {num} ({m.get("mapName")}): ventana x{x0} y{y0} {w}x{h} -> {out}')


if __name__ == '__main__':
    import argparse
    ap = argparse.ArgumentParser(description='Vista previa de luz (y texturas HD) sin Unity')
    ap.add_argument('salida', type=Path)
    ap.add_argument('--mapa', type=int, default=1)
    ap.add_argument('--escala', type=int, default=1, choices=(1, 2, 4))
    ap.add_argument('--hd', action='store_true')
    ap.add_argument('--ventana', type=int, nargs=2, metavar=('X', 'Y'))
    args = ap.parse_args()
    S, HD = args.escala, args.hd
    T = 32 * S
    ambient = {1: (.24, .30, .50), 392: (.30, .31, .42)}.get(args.mapa, (.26, .30, .46))
    args.salida.mkdir(parents=True, exist_ok=True)
    name = f"luz_{args.mapa}_x{S}{'_hd' if HD else ''}.png"
    panel(args.mapa, ambient, args.salida / name, tuple(args.ventana) if args.ventana else None)
