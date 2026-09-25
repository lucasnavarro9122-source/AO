"""Vista del P1 remasterizado contra la imagen de Lucas, sin instalar nada (nube, 25/09).

  python Tools/hd_remake/dungeon_vista_p1.py SALIDA RESULTADO [variantes] [decor] [nombre]

RESULTADO = carpeta de "dungeon_hd.py importar ... --salida" (usa RESULTADO/resultado/hd). Hace lo que tiene que
hacer el builder: variantes de piso (tex_90001) con bordes compatibles (dungeon_hd.bordes_piso), escombros y niebla
al pie de las paredes y haces de luz sobre piso libre (tex_90002). Escribe NOMBRE_vs_ref.jpg, _solo y _detalle."""
import sys, copy, hashlib
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, 'Tools/hd_remake'); import preview_luces as pl
SP = Path(sys.argv[1]); RES = Path(sys.argv[2]) / 'resultado/hd'
FLOOR = {'tex': 5095, 'sx': 512, 'sy': 288}; NEW = 90001; DECOR = len(sys.argv) > 4 and sys.argv[4] == 'decor'; NV = int(sys.argv[3]) if len(sys.argv) > 3 else 6
def variant_map(m):
    m = copy.deepcopy(m)
    spr = {s['id']: s for s in m['sprites']}
    nid = [900000]
    def add(sx, sy):
        nid[0] += 1
        m['sprites'].append({'id': nid[0], 'fileNum': NEW, 'sx': sx, 'sy': sy, 'width': 32, 'height': 32, 'key': f'n{nid[0]}'})
        return nid[0]
    cache = {}
    def sid(sx, sy):
        if (sx, sy) not in cache: cache[(sx, sy)] = add(sx, sy)
        return cache[(sx, sy)]
    h = lambda *a: int(hashlib.md5(repr(a).encode()).hexdigest(), 16)
    isfloor = lambda s: s and s['fileNum'] == FLOOR['tex'] and FLOOR['sx'] <= s['sx'] < FLOOR['sx'] + 128 and FLOOR['sy'] <= s['sy'] < FLOOR['sy'] + 128 and s['width'] == 32
    blocks = sorted({((c['x'] - (spr[c['sprite']]['sx'] - FLOOR['sx']) // 32) // 4, (c['y'] - (spr[c['sprite']]['sy'] - FLOOR['sy']) // 32) // 4)
                     for c in m['cells'] if c['layer'] == 1 and isfloor(spr.get(c['sprite']))}, key=lambda b: (b[1], b[0]))
    pick = {}
    for bx, by in blocks:   # de izquierda a derecha: el borde izquierdo tiene que ser el derecho del vecino
        need = ((pick[(bx - 1, by)] // 2) % 2) if (bx - 1, by) in pick else None
        cand = [k for k in range(NV) if need is None or k % 2 == need]
        r = h('p', bx, by) % 20
        base_ok = 0 in cand and r < 7
        pick[(bx, by)] = 0 if base_ok else cand[h('q', bx, by) % len(cand)]
    for c in m['cells']:
        if c['layer'] != 1: continue
        s = spr.get(c['sprite'])
        if isfloor(s):
            col, row = (s['sx'] - FLOOR['sx']) // 32, (s['sy'] - FLOOR['sy']) // 32
            k = pick[((c['x'] - col) // 4, (c['y'] - row) // 4)]
            c['sprite'] = sid((k % 4) * 128 + col * 32, (k // 4) * 128 + row * 32)
    if DECOR:   # escombros y niebla al pie de las paredes (lo que haría el builder)
        blocked = {(b['x'], b['y']) for b in m['blocks'] if b['flags']}
        floor = {(c['x'], c['y']) for c in m['cells'] if c['layer'] == 1 and c['grh'] != 1}
        used = {(c['x'], c['y']) for c in m['cells'] if c['layer'] in (2, 3)}
        def dsid(fx, sx, sy, w, hh):
            key = (fx, sx, sy)
            if key not in cache:
                nid[0] += 1
                m['sprites'].append({'id': nid[0], 'fileNum': 90002, 'sx': sx, 'sy': sy, 'width': w, 'height': hh, 'key': f'd{nid[0]}'})
                cache[key] = nid[0]
            return cache[key]
        for (x, y) in sorted(floor):
            if (x, y) in blocked or (x, y) in used: continue
            wall_n = (x, y - 1) in blocked or (x, y - 1) not in floor
            wall_w = (x - 1, y) in blocked or (x - 1, y) not in floor
            wall_e = (x + 1, y) in blocked or (x + 1, y) not in floor
            if not (wall_n or wall_w or wall_e): continue
            r = h('d', x, y) % 100
            if r < 24:
                m['cells'].append({'x': x, 'y': y, 'layer': 2, 'grh': 0, 'sprite': dsid(1, (r % 6) * 64, 0, 64, 64)})
            elif r < 38 and (x, y + 1) in floor:   # apoyada una casilla más abajo: queda sobre el piso, no sobre la pared
                m['cells'].append({'x': x, 'y': y + 1, 'layer': 2, 'grh': 0, 'sprite': dsid(2, (r % 3) * 128, 64, 128, 64)})
        free = lambda x, y: (x, y) in floor and (x, y) not in blocked
        for (x, y) in sorted(floor):   # haces de luz: uno cada tanto, con toda la huella (4x8) sobre piso libre
            if x % 5 != 2 or y % 7 != 4 or h('haz', x, y) % 3: continue
            if not all(free(x + dx, y - dy) for dx in range(-2, 2) for dy in range(0, 8)): continue
            m['cells'].insert(0, {'x': x, 'y': y, 'layer': 2, 'grh': 0, 'sprite': dsid(3, (h('k', x, y) % 2) * 128, 128, 128, 256)})
    return m
def render(m, x0, y0, w, h, hd):
    pl.S, pl.HD, pl.T = (4, True, 128) if hd else (1, False, 32)
    pl._tex.clear()
    if hd:
        for f in (5095, NEW, 90002):
            if (RES / f'tex_{f}.png').exists():
                pl._tex[f] = Image.open(RES / f'tex_{f}.png').convert('RGBA')
    base, _ = pl.albedo(m, x0, y0, w, h, chars=True, margin=3)
    light = pl.light_actual(m, x0, y0, w, h)
    if hd:
        light = np.asarray(Image.fromarray((np.clip(light, 0, 1) * 255).astype(np.uint8)).resize(base.size, Image.BILINEAR), np.float32) / 255
    rgb = pl.to_np(base) * light * 1.35
    return Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))
m = pl.load(1011)
mv = variant_map(m)
OUT = sys.argv[5] if len(sys.argv) > 5 else 'p1r5'
after = render(mv, 14, 22, 26, 22, True)
ref = Image.open('docs/claude/nube/hd/referencias/p1_objetivo_lucas.webp').convert('RGB')
a = after.resize(ref.size, Image.LANCZOS)
out = Image.new('RGB', (a.width * 2 + 12, a.height), (20, 20, 20)); out.paste(a, (0, 0)); out.paste(ref, (a.width + 12, 0))
out.thumbnail((2000, 2000), Image.LANCZOS); out.save(SP / f'{OUT}_vs_ref.jpg', quality=88)
a.save(SP / f'{OUT}_solo.jpg', quality=90)
render(mv, 20, 30, 8, 6, True).save(SP / f'{OUT}_detalle.jpg', quality=92)
print(out.size, a.size)
