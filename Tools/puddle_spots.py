"""Dónde se juntan los charcos (nube, 27/09): en caminos de tierra y piedra, no en el pasto. Lee el color real del piso
(capa 1, texturas 1x) de cada casilla caminable sin techo y elige ~10 % de las casillas de camino rodeadas de camino.
Genera Assets/Resources/AOMigrator/WorldV07/puddle_spots.json, que lee AOMapWeatherPuddlesV294 (sin el archivo, usa
un reparto por hash).

  python Tools/puddle_spots.py                # mapas con lluvia y ciclo de día (baseLight 0), no dungeons
  python Tools/puddle_spots.py 1 6            # mapas elegidos"""
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
WORLD = ROOT / 'Assets/Resources/AOMigrator/WorldV07'
OUT = WORLD / 'puddle_spots.json'
_tex = {}


def tex(n):
    if n not in _tex:
        p = WORLD / 'Textures' / f'tex_{n}.png'
        _tex[n] = np.asarray(Image.open(p).convert('RGB'), np.float32) / 255 if p.exists() else None
    return _tex[n]


def ground_kind(rgb):
    """'camino' (tierra o piedra), 'pasto', 'agua' u 'otro', por el color medio del piso."""
    r, g, b = rgb
    mx, mn = max(rgb), min(rgb)
    sat = (mx - mn) / mx if mx > 0 else 0
    if b > r * 1.15 and b > g * 1.05:
        return 'agua'
    if g > r * 1.08 and g > b * 1.1:
        return 'pasto'
    # Tierra, barro y piedra: tonos cálidos o neutros (rojo >= verde), ni muy oscuros ni muy claros.
    if r >= g * 0.92 and 0.1 < (r + g + b) / 3 < 0.8 and sat < 0.7:
        return 'camino'
    return 'otro'


def build(num):
    m = json.loads((WORLD / 'Maps' / f'map_{num}.json').read_text('utf-8-sig'))
    sprites = {s['id']: s for s in m['sprites']}
    blocked = {(b['x'], b['y']) for b in m['blocks'] if b['flags'] & 15}
    trig = {(t['x'], t['y']): t['trigger'] for t in m.get('triggers', [])}
    roof_cells = {(c['x'], c['y']) for c in m['cells'] if c['layer'] == 4}
    kind = {}
    for c in m['cells']:
        if c['layer'] != 1:
            continue
        s = sprites.get(c['sprite'])
        t = tex(s['fileNum']) if s else None
        if t is None:
            continue
        crop = t[s['sy']:s['sy'] + s['height'], s['sx']:s['sx'] + s['width']]
        if crop.size:
            kind[(c['x'], c['y'])] = ground_kind(tuple(crop.reshape(-1, 3).mean(0)))
    out = []
    for (x, y), k in kind.items():
        if k != 'camino' or (x, y) in blocked or (x, y) in roof_cells:
            continue
        t = trig.get((x, y), 0)
        if t in (1, 4, 16) or 20 <= t < 200:
            continue
        around = sum(kind.get((x + dx, y + dy)) == 'camino' for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        if around < 3:
            continue
        h = ((x * 73856093) ^ (y * 19349663) ^ (num * 83492791)) & 0xFFFFFFFF
        h ^= h >> 13
        h = (h * 0x5bd1e995) & 0xFFFFFFFF
        h ^= h >> 15
        if h % 100 >= 10:
            continue
        size = round(0.75 + ((h >> 24) & 255) / 255 * 0.85, 2)
        out += [x, y, int(size * 100), int((h >> 5) % 3)]
    return out


def main(args):
    if args:
        nums = [int(a) for a in args if a.isdigit()]
    else:
        env = json.loads((WORLD / 'map_environment.json').read_text('utf-8-sig'))['maps']
        nums = []
        for e in env:
            if not e.get('rain') or e.get('baseLight', 0):
                continue
            p = WORLD / 'Maps' / f"map_{e['mapNumber']}.json"
            if p.exists() and '"DUNGEON"' not in p.read_text('utf-8-sig')[:400]:
                nums.append(e['mapNumber'])
    maps = []
    for n in sorted(nums):
        spots = build(n)
        if spots:
            maps.append({'map': n, 'spots': spots})
    data = {'version': 1, 'fields': 'x, y, tamaño x100, forma', 'maps': maps}
    OUT.write_text(json.dumps(data, separators=(',', ':')), 'utf-8')
    print(OUT.relative_to(ROOT), len(maps), 'mapas', sum(len(m['spots']) // 4 for m in maps), 'charcos',
          f'{OUT.stat().st_size / 1024:.0f} KB')


if __name__ == '__main__':
    main(sys.argv[1:])
