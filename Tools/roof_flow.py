"""Pendiente de los techos para el agua de lluvia (nube, 26/09): genera
Assets/Resources/AOMigrator/WorldV07/roof_flow.json, que lee AOMapWeatherRoofsV290.

  python Tools/roof_flow.py                 # mapa 1 (Ullathorpe)
  python Tools/roof_flow.py 1 34 59         # mapas elegidos
  python Tools/roof_flow.py --todos         # todos los mapas con lluvia y techos

Cómo lo calcula (sin Unity, desde los mismos datos del juego):
- Máscara de techos: alfa de los sprites de la capa 4 a 1x, ubicados como el juego (pie del sprite al centro de
  la casilla), en una grilla de 8 px (1/4 de casilla).
- Techo = zona conexa con el mismo disparador de techo (el mismo que usa AOWorldManagerRoofV210 para
  desvanecerlo); así el clima sabe qué techo está transparente cuando el jugador entra.
- Cumbrera: la columna donde el borde de arriba del techo está más alto. Si el borde de arriba es casi plano,
  el techo cae hacia el frente (modo 1: el agua baja derecho); si no, es a dos aguas (modo 0: se aleja de la
  cumbrera y baja).
Formato: por mapa, `runs` = pares (etiqueta, cantidad) de la grilla fila por fila (0 = sin techo; n = roofs[n-1])."""
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
WORLD = ROOT / 'Assets/Resources/AOMigrator/WorldV07'
OUT = WORLD / 'roof_flow.json'
CELL = 8            # px de la grilla (1x)
T = 32              # px por casilla
MIN_CELLS = 6       # techos más chicos (adornos sueltos) no se cuentan
FLAT = 10           # px: si el borde de arriba varía menos, el techo cae al frente
_tex = {}


def tex(file_num):
    if file_num not in _tex:
        p = WORLD / 'Textures' / f'tex_{file_num}.png'
        _tex[file_num] = np.asarray(Image.open(p).convert('RGBA'))[..., 3] if p.exists() else None
    return _tex[file_num]


def is_roof_trigger(t):
    return t in (1, 4, 16) or 20 <= t < 200


def load(num):
    return json.loads((WORLD / 'Maps' / f'map_{num}.json').read_text('utf-8-sig'))


def roof_trigger_near(trig, roof_triggers, x, y):
    """Igual que AOWorldManagerV07.RoofTriggerNear."""
    t = trig.get((x, y), 0)
    if t in roof_triggers:
        return t
    for d in (1, 2):
        for dy in range(-d, d + 1):
            for dx in range(-d, d + 1):
                t = trig.get((x + dx, y + dy), 0)
                if t in roof_triggers:
                    return t
    return 0


def build(num):
    m = load(num)
    cells4 = [c for c in m['cells'] if c['layer'] == 4]
    if not cells4:
        return None
    sprites = {s['id']: s for s in m['sprites']}
    trig = {(t['x'], t['y']): t['trigger'] for t in m.get('triggers', [])}
    roof_triggers = {trig.get((c['x'], c['y']), 0) for c in cells4}
    roof_triggers = {t for t in roof_triggers if is_roof_trigger(t)}
    W, H = m['xmax'] * T, m['ymax'] * T
    alpha = np.zeros((H, W), np.bool_)
    owner = np.full((H, W), -1, np.int32)          # disparador de techo del píxel (0 = techo sin interior)
    for c in cells4:
        s = sprites.get(c['sprite'])
        if s is None:
            continue
        t = tex(s['fileNum'])
        if t is None:
            continue
        a = t[s['sy']:s['sy'] + s['height'], s['sx']:s['sx'] + s['width']] > 127
        sh, sw = a.shape
        left, top = (c['x'] - 1) * T + T // 2 - sw // 2, c['y'] * T - sh
        x1, y1, x2, y2 = max(0, left), max(0, top), min(W, left + sw), min(H, top + sh)
        if x1 >= x2 or y1 >= y2:
            continue
        sub = a[y1 - top:y2 - top, x1 - left:x2 - left]
        alpha[y1:y2, x1:x2] |= sub
        o = owner[y1:y2, x1:x2]
        o[sub] = roof_trigger_near(trig, roof_triggers, c['x'], c['y'])
    gw, gh = W // CELL, H // CELL
    blocks = alpha[:gh * CELL, :gw * CELL].reshape(gh, CELL, gw, CELL)
    roof = blocks.mean((1, 3)) >= 0.5
    own = owner[:gh * CELL, :gw * CELL].reshape(gh, CELL, gw, CELL).max((1, 3))

    labels = np.zeros((gh, gw), np.int32)
    roofs = []
    for gy in range(gh):
        for gx in range(gw):
            if not roof[gy, gx] or labels[gy, gx]:
                continue
            key = own[gy, gx]
            stack, comp = [(gy, gx)], []
            labels[gy, gx] = -1
            while stack:
                cy, cx = stack.pop()
                comp.append((cy, cx))
                for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                    if 0 <= ny < gh and 0 <= nx < gw and roof[ny, nx] and not labels[ny, nx] and own[ny, nx] == key:
                        labels[ny, nx] = -1
                        stack.append((ny, nx))
            if len(comp) < MIN_CELLS:
                for cy, cx in comp:
                    labels[cy, cx] = 0
                roof[tuple(np.array(comp).T)] = False
                continue
            roofs.append(comp_info(comp, max(0, int(key))))
            for cy, cx in comp:
                labels[cy, cx] = len(roofs)
    runs = []
    flat = labels.ravel()
    i = 0
    while i < len(flat):
        j = i
        while j < len(flat) and flat[j] == flat[i]:
            j += 1
        runs += [int(flat[i]), j - i]
        i = j
    return {'map': num, 'width': gw, 'height': gh, 'runs': runs, 'roofs': roofs}


def comp_info(comp, trigger):
    ys = np.array([c[0] for c in comp])
    xs = np.array([c[1] for c in comp])
    tops = {}
    for y, x in zip(ys, xs):
        tops[x] = min(tops.get(x, 10 ** 9), y)
    cols = sorted(tops)
    top_px = np.array([tops[c] for c in cols]) * CELL
    center = (xs.min() + xs.max() + 1) / 2
    ridge_col = min(cols, key=lambda c: (tops[c], abs(c + 0.5 - center)))
    mode = 1 if top_px.max() - top_px.min() < FLAT else 0
    # En mundo: x = px / 32. La cumbrera se guarda en unidades de mundo.
    return {'trigger': trigger, 'mode': mode, 'ridge': round((ridge_col + 0.5) * CELL / T, 3)}


def main(args):
    if '--todos' in args:
        env = json.loads((WORLD / 'map_environment.json').read_text('utf-8-sig'))['maps']
        nums = sorted(e['mapNumber'] for e in env if e.get('rain') and (WORLD / 'Maps' / f"map_{e['mapNumber']}.json").exists())
    else:
        nums = [int(a) for a in args if a.isdigit()] or [1]
    maps = []
    for n in nums:
        entry = build(n)
        if entry and entry['roofs']:
            maps.append(entry)
            modes = sum(r['mode'] for r in entry['roofs'])
            print(f"mapa {n}: {len(entry['roofs'])} techos ({len(entry['roofs']) - modes} a dos aguas, {modes} al frente), "
                  f"{len(entry['runs']) // 2} tramos")
    data = {'version': 1, 'cell': CELL, 'maps': maps}
    OUT.write_text(json.dumps(data, separators=(',', ':')), 'utf-8')
    print(OUT.relative_to(ROOT), f'{OUT.stat().st_size / 1024:.0f} KB')


if __name__ == '__main__':
    main(sys.argv[1:])
