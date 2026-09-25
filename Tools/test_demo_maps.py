"""D-20 of docs/claude/demo/pruebas.md: static audit of the demo maps (no Unity).

For every demo map (Resources/AOMigrator/WorldV07/Maps/map_1000.json, 1001 and 1010-1017):
- route: from each arrival (arrivals.fromAbove / fromBelow, or spawn) every exit tile and the other arrival
  must be reachable, walking like the game (AOGridMap.CanEnter / CanStep: directional flags, water,
  triggers 13/201, 8 directions without cutting corners);
- holes: no reachable tile may be void (layer 1 = GRH 1 with nothing else, the "black" of the dungeons) nor
  touch void on its 4 sides;
- false entrances: every stair hole (GRH 57950) must have an exit on it or next to it; exits of the original
  map ("base": "copy N" in Tools/demo_maps/<id>.art.json) whose graphic is still there must be real exits;
- exits: the destination map exists, the destination tile is inside, has floor, can be entered, is not an
  exit itself (no bouncing), and the destination map has an exit back to this map reachable from it.
Writes MigrationReports/demo_maps_check.json and exits with 1 if anything fails.
Usage: python Tools/test_demo_maps.py [map ...]"""
import json
import re
import sys
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAPS = ROOT / 'Assets/Resources/AOMigrator/WorldV07/Maps'
SPECS = ROOT / 'Tools/demo_maps'
DEMO_MAPS = [1000, 1001] + list(range(1010, 1018))
HUB = 1000
VOID_GRH, STAIR_GRH = 1, 57950
NORTH, EAST, SOUTH, WEST = 1, 2, 3, 4
ALL_SIDES, WATER, COAST = 0xF, 0x20, 0x80
BLOCKING_TRIGGERS = {13, 201}
BRIDGE_VALID = 17
DIRS8 = [(dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
_cache = {}


class Map:
    def __init__(self, number):
        path = MAPS / f'map_{number}.json'
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        self.number = number
        self.x0, self.x1, self.y0, self.y1 = data['xmin'], data['xmax'], data['ymin'], data['ymax']
        self.flags = {(b['x'], b['y']): b['flags'] for b in data.get('blocks', [])}
        self.triggers = {(t['x'], t['y']): t['trigger'] for t in data.get('triggers', [])}
        self.layers = {}
        for c in data.get('cells', []):
            if c.get('grh', 0) > 0:
                self.layers.setdefault((c['x'], c['y']), {})[c['layer']] = c['grh']
        self.exits = {(e['x'], e['y']): e for e in data.get('exits', [])}
        arrivals = data.get('arrivals') or {}
        self.arrivals = {k: tuple(v) for k, v in arrivals.items() if v}
        if not self.arrivals and data.get('spawn'):
            self.arrivals = {'spawn': tuple(data['spawn'])}

    def inside(self, x, y):
        return self.x0 <= x <= self.x1 and self.y0 <= y <= self.y1

    def void(self, x, y):
        layers = self.layers.get((x, y), {})
        return not layers or (layers == {1: VOID_GRH})

    def has_grh(self, x, y, grh):
        return grh in self.layers.get((x, y), {}).values()

    def can_enter(self, x, y, heading):
        if not self.inside(x, y):
            return False
        flags = self.flags.get((x, y), 0)
        if flags & (1 << (heading - 1)):
            return False
        trigger = self.triggers.get((x, y), 0)
        if trigger in BLOCKING_TRIGGERS:
            return False
        if flags & WATER and not flags & COAST and trigger != BRIDGE_VALID:
            return False
        return True

    def enterable(self, x, y):
        return any(self.can_enter(x, y, h) for h in (NORTH, EAST, SOUTH, WEST))

    def can_step(self, fx, fy, tx, ty):
        dx, dy = tx - fx, ty - fy
        horizontal = EAST if dx > 0 else WEST if dx < 0 else 0
        vertical = SOUTH if dy > 0 else NORTH if dy < 0 else 0
        if not horizontal: return self.can_enter(tx, ty, vertical)
        if not vertical: return self.can_enter(tx, ty, horizontal)
        return (self.can_enter(tx, ty, horizontal) and self.can_enter(tx, ty, vertical)
                and self.can_enter(fx + dx, fy, horizontal) and self.can_enter(fx, fy + dy, vertical))

    def reachable(self, start):
        """Tiles reachable on foot. Standing on an exit warps you, so exits are reached but not crossed."""
        seen = {start}; queue = deque([start])
        while queue:
            x, y = queue.popleft()
            if (x, y) in self.exits and (x, y) != start:
                continue
            for dx, dy in DIRS8:
                n = (x + dx, y + dy)
                if n not in seen and self.can_step(x, y, *n):
                    seen.add(n); queue.append(n)
        return seen


def load(number):
    if number not in _cache:
        _cache[number] = Map(number) if (MAPS / f'map_{number}.json').exists() else None
    return _cache[number]


def base_map(number):
    spec = SPECS / f'{number}.art.json'
    if not spec.exists():
        return None
    match = re.match(r'copy (\d+)', json.loads(spec.read_text(encoding='utf-8-sig')).get('base', ''))
    return int(match.group(1)) if match else None


def audit(number):
    m = load(number); errors = []; notes = []; details = {}
    if m is None:
        return [f'falta map_{number}.json'], notes, details
    if not m.arrivals:
        errors.append('sin arrivals ni spawn')
    reach_all = set()
    for name, arrival in m.arrivals.items():
        if not m.inside(*arrival) or m.void(*arrival) or not m.enterable(*arrival):
            errors.append(f'llegada {name} {arrival} no es una casilla caminable con piso')
            continue
        if arrival in m.exits:
            errors.append(f'llegada {name} {arrival} está sobre una salida (rebota)')
        reach = m.reachable(arrival); reach_all |= reach
        for tile in m.exits:
            if tile not in reach:
                errors.append(f'desde la llegada {name} {arrival} no se llega a la salida {tile}')
        for other, target in m.arrivals.items():
            if other != name and target not in reach:
                errors.append(f'desde la llegada {name} no se llega a la llegada {other} {target}')
    # Holes: walking on void or next to it. Black that was already black in the original map is its design
    # (cave borders): only void created by the demo (crop / paint) counts as a hole.
    source = base_map(number); src = load(source) if source else None
    def new_void(x, y):
        return m.void(x, y) and not (src and src.inside(x, y) and src.void(x, y))
    sides = ((0, 1), (0, -1), (1, 0), (-1, 0))
    on_void = sorted(t for t in reach_all if m.void(*t))
    near_new = sorted(t for t in reach_all if not m.void(*t) and t not in m.exits and
                      any(m.inside(t[0] + dx, t[1] + dy) and new_void(t[0] + dx, t[1] + dy) for dx, dy in sides))
    near_old = sum(1 for t in reach_all if not m.void(*t) and t not in m.exits and t not in near_new and
                   any(m.inside(t[0] + dx, t[1] + dy) and m.void(t[0] + dx, t[1] + dy) for dx, dy in sides))
    if on_void: errors.append(f'{len(on_void)} casillas alcanzables sobre el vacío, p. ej. {on_void[:6]}')
    if near_new: errors.append(f'{len(near_new)} casillas alcanzables pegadas a vacío nuevo (del recorte), p. ej. {near_new[:6]}')
    if near_old: notes.append(f'{near_old} casillas junto a negro que ya estaba en el mapa original')
    details['sobre_vacio'] = on_void; details['pegadas_a_vacio_nuevo'] = near_new
    # False entrances: stair holes without an exit on or next to them.
    stairs = [t for t in m.layers if m.has_grh(*t, STAIR_GRH)]
    lonely = [t for t in stairs if not any((t[0] + dx, t[1] + dy) in m.exits for dx in (-1, 0, 1) for dy in (-1, 0, 1))]
    if lonely: errors.append(f'{len(lonely)} escaleras (GRH {STAIR_GRH}) sin salida al lado: {sorted(lonely)[:6]}')
    details['escaleras_sin_salida'] = sorted(lonely)
    if src:
        kept = []
        for tile in src.exits:
            if tile in m.exits or m.void(*tile):
                continue
            if m.layers.get(tile) == src.layers.get(tile) and tile in reach_all:
                kept.append(tile)
        if kept: errors.append(f'{len(kept)} entradas del mapa original {source} siguen dibujadas y alcanzables sin salida: {sorted(kept)[:6]}')
        details['entradas_originales_sin_salida'] = sorted(kept)
        notes.append(f'mapa base {source}')
    # Exits: destination valid, not bouncing, and a way back.
    for tile, e in m.exits.items():
        dest = load(e['destMap']); target = (e['destX'], e['destY'])
        where = f"salida {tile} → mapa {e['destMap']} {target}"
        if dest is None:
            errors.append(f'{where}: el mapa destino no existe'); continue
        if not dest.inside(*target) or dest.void(*target) or not dest.enterable(*target):
            errors.append(f'{where}: la casilla de llegada no es caminable o no tiene piso'); continue
        if target in dest.exits:
            errors.append(f'{where}: se llega encima de otra salida (rebota)')
        back = [t for t, x in dest.exits.items() if x['destMap'] == number]
        if e['destMap'] == HUB and not back:
            notes.append(f'{where}: vuelta al hub de una sola mano (a propósito)')
        elif not back:
            errors.append(f'{where}: el mapa {e["destMap"]} no tiene salida de vuelta al {number}')
        elif not any(t in dest.reachable(target) for t in back):
            errors.append(f'{where}: desde la llegada no se alcanza la salida de vuelta')
    notes.append(f'{len(m.exits)} salidas, {len(reach_all)} casillas alcanzables, {len(stairs)} escaleras')
    return errors, notes, details


def main():
    numbers = [int(a) for a in sys.argv[1:]] or DEMO_MAPS
    report = {}; failed = 0
    for number in numbers:
        errors, notes, details = audit(number)
        report[str(number)] = dict(passed=not errors, errors=errors, notes=notes,
                                   details={k: [list(t) for t in v] for k, v in details.items() if v})
        failed += bool(errors)
        print(f"map_{number}: {'PASA' if not errors else 'FALLA'} · {'; '.join(notes)}")
        for error in errors:
            print(f'   - {error}')
    out = ROOT / 'MigrationReports/demo_maps_check.json'
    out.write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding='utf-8')
    print(f'\n{len(numbers) - failed}/{len(numbers)} mapas pasan. Informe: {out}')
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()
