"""Arma los mapas de la demo AO BATTLESERVER (IDs 1000-1999) desde especificaciones declarativas.

Uso (desde la raíz del proyecto):
  python Tools/demo_map_builder.py                  arma y valida todo en memoria (no escribe nada)
  python Tools/demo_map_builder.py 1011 1012        solo esos mapas (las salidas a otros mapas se validan igual)
  python Tools/demo_map_builder.py --write          escribe en Assets: mapas, música, ambiente y minimapas
                                                    (requiere el candado de Unity libre o de Programación)
  python Tools/demo_map_builder.py --out DIR        escribe solo los map_*.json en otra carpeta (revisión)
  python Tools/demo_map_builder.py --check          compara con lo que hay en disco (QA / antes de commitear)
  python Tools/demo_map_builder.py --show 264 [x0 y0 x1 y1]   dibuja en texto la caminabilidad de un mapa

Especificaciones: Tools/demo_maps/{id}.json (Programación: base y estructura) y
Tools/demo_maps/{id}.art.json (Arte: {"ops": [stamp | paint | erase | light]}). Orden: base -> Arte -> estructura.
Datos del dungeon: docs/claude/demo/dungeon-npcs.json (Contenido). Diseño: docs/claude/demo/arquitectura.md §2.
Nunca escribe mapas < 1000. No cambia stats de NPC: salen de npcs.dat.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "Tools"))
import map_migration as mm  # noqa: E402

MAPS_DIR = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"
TEXTURES_DIR = ROOT / "Assets/Resources/AOMigrator/WorldV07/Textures"
MUSIC_CATALOG = ROOT / "Assets/Resources/AOMigrator/AudioV190/map_music.json"
ENV_CATALOG = ROOT / "Assets/Resources/AOMigrator/WorldV07/map_environment.json"
VISUALS = ROOT / "Assets/Resources/AOMigrator/CharacterV111/character_visuals.json"
SPECS_DIR = ROOT / "Tools/demo_maps"
CONTENT = ROOT / "docs/claude/demo/dungeon-npcs.json"
ORIGINALS = ROOT / "Archivos Originales/Recursos-master/Recursos-master"
TABLERO = ROOT / "docs/claude/tablero.md"

MIN_ID, MAX_ID, SIZE = 1000, 1999, 100
FLAG_SIDES, FLAG_WATER, FLAG_COAST, FLAG_LAVA = 0xF, 0x20, 0x80, 0x100
TRIGGER_NPC_INVALID, TRIGGER_WORKER_ONLY, TRIGGER_BRIDGE, TRIGGER_FIGHT, TRIGGER_BLOCK = 3, 13, 17, 6, 201
RING_W, RING_H = 23, 19  # AOArenaGen.W / AOArenaGen.H
BUILDER_VERSION = 1

# Same ranges as AOWorldManagerV07.IsWaterGrh / IsLavaGrh.
WATER_RANGES = [(1505, 1520), (124, 139), (24223, 24238), (24303, 24318), (468, 483),
                (44668, 44683), (24143, 24158), (12628, 12643), (2948, 2963)]
LAVA_RANGES = [(57400, 57415), (16101, 16116), (26767, 26782)]

# Stats that always come from npcs.dat (the originals); visuals and the rest come from the map template.
NPC_STAT_KEYS = ("maxHp", "minHit", "maxHit", "defense", "attackPower", "evasionPower",
                 "attackIntervalMs", "giveExp", "giveGold")


class BuildError(Exception):
    pass


def in_ranges(value: int, ranges) -> bool:
    return any(lo <= value <= hi for lo, hi in ranges)


def rect_cells(rect):
    x, y, w, h = rect
    for yy in range(max(1, y), min(SIZE, y + h - 1) + 1):
        for xx in range(max(1, x), min(SIZE, x + w - 1) + 1):
            yield xx, yy


# ---------------------------------------------------------------- data sources

class Sources:
    """Lazy caches of original data shared by every map build."""

    def __init__(self):
        self._maps: dict[int, dict] = {}
        self._definitions = None
        self._npc_dat = None
        self._visuals = None
        self._templates: dict[int, dict] = {}
        self.content = json.loads(CONTENT.read_text("utf-8")) if CONTENT.exists() else {"pisos": []}

    def map(self, number: int) -> dict:
        if number not in self._maps:
            path = MAPS_DIR / f"map_{number}.json"
            if not path.exists():
                raise BuildError(f"no existe el mapa fuente {number}")
            self._maps[number] = json.loads(path.read_text("utf-8"))
        return self._maps[number]

    @property
    def definitions(self):
        if self._definitions is None:
            self._definitions = mm.graphics_index(ORIGINALS / "init/graficos.ini")
        return self._definitions

    @property
    def npc_dat(self):
        if self._npc_dat is None:
            self._npc_dat = mm.npc_dat(ORIGINALS / "Dat/npcs.dat")
        return self._npc_dat

    @property
    def visuals(self):
        if self._visuals is None:
            data = json.loads(VISUALS.read_text("utf-8"))
            self._visuals = ({e["id"]: e for e in data["bodies"]}, {e["id"]: e for e in data["heads"]})
        return self._visuals

    @property
    def items(self) -> dict[int, dict]:
        if not hasattr(self, "_items"):
            data = json.loads((ROOT / "Assets/Resources/AOMigrator/ItemsV10/items.json").read_text("utf-8"))
            self._items = {e["index"]: e for e in data["items"]}
        return self._items

    def floor(self, floor_id: str) -> dict:
        for floor in self.content.get("pisos", []):
            if floor.get("id") == floor_id:
                return floor
        raise BuildError(f"dungeon-npcs.json no tiene el piso {floor_id}")

    def npc_info(self, index: int) -> dict:
        for floor in self.content.get("pisos", []):
            for npc in floor.get("npcs", []) + [floor.get("jefe") or {}]:
                if npc.get("npcIndex") == index:
                    return npc
        return {}

    def npc_template(self, index: int, hint_maps) -> dict | None:
        """First NPC entry with this index in the original maps (hints first): keeps visuals and later fixes."""
        if index in self._templates:
            return self._templates[index]
        candidates = [m for m in hint_maps if (MAPS_DIR / f"map_{m}.json").exists()]
        candidates += sorted(int(p.stem[4:]) for p in MAPS_DIR.glob("map_*.json")
                             if p.stem[4:].isdigit() and int(p.stem[4:]) < MIN_ID)
        for number in candidates:
            for entry in self.map(number).get("npcs", []):
                self._templates.setdefault(entry["npcIndex"], entry)
            if index in self._templates:
                break
        return self._templates.get(index)

    def sprite(self, grh: int, pool: dict[int, dict]) -> dict | None:
        if grh in pool:
            return pool[grh]
        frames, fps = mm.resolve_grh(grh, self.definitions)
        if not frames:
            return None
        sprite = {"id": grh, **frames[0]}
        if len(frames) > 1:
            sprite["frames"] = frames
            sprite["fps"] = round(fps, 3)
        pool[grh] = sprite
        return sprite


# ---------------------------------------------------------------- map model

class MapModel:
    META = ("version", "mapNumber", "mapName", "zone", "terrain", "ambient", "safe", "xmin", "xmax", "ymin", "ymax")
    LISTS = ("cells", "sprites", "blocks", "triggers", "exits", "npcs", "objects", "lights", "particles")

    def __init__(self, data: dict):
        self.meta = {k: data.get(k) for k in self.META}
        self.cells = {(c["x"], c["y"], c["layer"]): (c["grh"], c.get("sprite", c["grh"])) for c in data.get("cells", [])}
        self.sprite_pool = {s["id"]: copy.deepcopy(s) for s in data.get("sprites", [])}
        self.blocks = {(b["x"], b["y"]): b["flags"] for b in data.get("blocks", [])}
        self.triggers = {(t["x"], t["y"]): t["trigger"] for t in data.get("triggers", [])}
        self.exits = {(e["x"], e["y"]): [e["destMap"], e["destX"], e["destY"]] for e in data.get("exits", [])}
        self.npcs = copy.deepcopy(data.get("npcs", []))
        self.objects = copy.deepcopy(data.get("objects", []))
        self.lights = copy.deepcopy(data.get("lights", []))
        self.particles = copy.deepcopy(data.get("particles", []))
        self.extra = {k: copy.deepcopy(v) for k, v in data.items() if k not in self.META and k not in self.LISTS}
        self.pending_exits: list[dict] = []   # exits whose destination is resolved after every map is built
        self.fixed_npcs: list[tuple] = []

    # -- walkability, mirroring AOGridMap.CanEnter (conservative: any side bit blocks)
    def flags(self, x: int, y: int) -> int:
        f = self.blocks.get((x, y), 0)
        layer1 = self.cells.get((x, y, 1))
        if layer1:
            if in_ranges(layer1[0], WATER_RANGES):
                f |= FLAG_WATER
            elif in_ranges(layer1[0], LAVA_RANGES):
                f |= FLAG_LAVA
        if (x, y, 2) in self.cells:
            f |= FLAG_COAST
        return f

    def walkable(self, x: int, y: int) -> bool:
        if not (1 <= x <= SIZE and 1 <= y <= SIZE):
            return False
        f = self.flags(x, y)
        if f & FLAG_SIDES:
            return False
        trigger = self.triggers.get((x, y), 0)
        if trigger in (TRIGGER_WORKER_ONLY, TRIGGER_BLOCK):
            return False
        if f & FLAG_WATER and not f & FLAG_COAST and trigger != TRIGGER_BRIDGE:
            return False
        return True

    def npc_spawnable(self, x: int, y: int) -> bool:
        return (self.walkable(x, y) and not self.flags(x, y) & (FLAG_WATER | FLAG_LAVA)
                and self.triggers.get((x, y), 0) != TRIGGER_NPC_INVALID and (x, y) not in self.exits)

    def reachable(self, start) -> set:
        seen = {tuple(start)} if self.walkable(*start) else set()
        stack = list(seen)
        while stack:
            x, y = stack.pop()
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if n not in seen and self.walkable(*n):
                    seen.add(n)
                    stack.append(n)
        return seen

    def to_json(self, sources: Sources) -> tuple[dict, list[str]]:
        problems = []
        sprites = {}
        cells = []
        for (x, y, layer), (grh, sprite_id) in sorted(self.cells.items(), key=lambda kv: (kv[0][2], kv[0][1], kv[0][0])):
            cells.append({"x": x, "y": y, "layer": layer, "grh": grh, "sprite": sprite_id})
            if sprite_id not in sprites:
                sprite = sources.sprite(sprite_id, self.sprite_pool)
                if sprite is None:
                    problems.append(f"GRH {sprite_id} sin definición en graficos.ini")
                else:
                    sprites[sprite_id] = sprite
        for sprite in sprites.values():
            for frame in sprite.get("frames") or [sprite]:
                if not (TEXTURES_DIR / f"tex_{frame['fileNum']}.png").exists():
                    problems.append(f"GRH {sprite['id']}: falta la textura tex_{frame['fileNum']}.png")
        data = dict(self.meta)
        data.update({
            "cells": cells,
            "sprites": [sprites[k] for k in sorted(sprites)],
            "blocks": [{"x": x, "y": y, "flags": f} for (x, y), f in sorted(self.blocks.items(), key=lambda kv: (kv[0][1], kv[0][0])) if f],
            "triggers": [{"x": x, "y": y, "trigger": t} for (x, y), t in sorted(self.triggers.items(), key=lambda kv: (kv[0][1], kv[0][0])) if t],
            "exits": [{"x": x, "y": y, "destMap": d[0], "destX": d[1], "destY": d[2]} for (x, y), d in sorted(self.exits.items(), key=lambda kv: (kv[0][1], kv[0][0]))],
            "npcs": self.npcs,
            "objects": sorted(self.objects, key=lambda o: (o["y"], o["x"], o.get("objIndex", 0))),
            "lights": sorted(self.lights, key=lambda o: (o["y"], o["x"])),
            "particles": sorted(self.particles, key=lambda o: (o["y"], o["x"])),
        })
        data.update(self.extra)
        return data, sorted(set(problems))


# ---------------------------------------------------------------- operations

def op_stamp(model: MapModel, op: dict, sources: Sources):
    src = MapModel(sources.map(op["src"]))
    x0, y0, w, h = op["x"], op["y"], op["w"], op["h"]
    dx, dy = op["dx"], op["dy"]                       # destination top-left
    layers = op.get("layers", [1, 2, 3, 4])
    shift = (dx - x0, dy - y0)
    dest = set(rect_cells((dx, dy, w, h)))
    inside = lambda x, y: x0 <= x < x0 + w and y0 <= y < y0 + h  # noqa: E731
    for layer in layers:
        for (x, y) in dest:
            model.cells.pop((x, y, layer), None)
    for (x, y, layer), value in src.cells.items():
        if layer in layers and inside(x, y) and (x + shift[0], y + shift[1]) in dest:
            model.cells[(x + shift[0], y + shift[1], layer)] = value
            model.sprite_pool.setdefault(value[1], src.sprite_pool.get(value[1]))
    model.sprite_pool = {k: v for k, v in model.sprite_pool.items() if v is not None}
    if op.get("blocks", True):
        for p in dest:
            model.blocks.pop(p, None)
        for (x, y), f in src.blocks.items():
            if inside(x, y) and (x + shift[0], y + shift[1]) in dest:
                model.blocks[(x + shift[0], y + shift[1])] = f
    if op.get("triggers", True):
        for p in dest:
            model.triggers.pop(p, None)
        for (x, y), t in src.triggers.items():
            if inside(x, y) and (x + shift[0], y + shift[1]) in dest:
                model.triggers[(x + shift[0], y + shift[1])] = t
    for key in ("objects", "lights", "particles"):
        if not op.get(key, True):
            continue
        kept = [o for o in getattr(model, key) if (o["x"], o["y"]) not in dest]
        for o in getattr(src, key):
            if inside(o["x"], o["y"]) and (o["x"] + shift[0], o["y"] + shift[1]) in dest:
                kept.append({**o, "x": o["x"] + shift[0], "y": o["y"] + shift[1]})
        setattr(model, key, kept)


def op_paint(model: MapModel, op: dict, sources: Sources):
    layer = op["layer"]
    cells = [tuple(c) for c in op["cells"]] if "cells" in op else list(rect_cells(op["rect"]))
    for x, y in cells:
        if "tile4x4" in op:
            grh = op["tile4x4"] + (y % 4) * 4 + (x % 4)
        elif "tile3x3" in op:
            grh = op["tile3x3"] + (y % 3) * 3 + (x % 3)
        else:
            grh = op["grh"]
        if sources.sprite(grh, model.sprite_pool) is None:
            raise BuildError(f"paint: GRH {grh} no existe en graficos.ini")
        model.cells[(x, y, layer)] = (grh, grh)


def op_erase(model: MapModel, op: dict, sources: Sources):
    for x, y in rect_cells(op["rect"]):
        model.cells.pop((x, y, op["layer"]), None)


def op_light(model: MapModel, op: dict, sources: Sources):
    """A map light (the engine's point light): replaces any light already on that tile."""
    model.lights = [l for l in model.lights if (l["x"], l["y"]) != (op["x"], op["y"])]
    model.lights.append({"x": op["x"], "y": op["y"], "color": op["color"], "range": op["range"]})


def op_cells(op: dict):
    return [tuple(c) for c in op["cells"]] if "cells" in op else list(rect_cells(op["rect"]))


def op_block(model: MapModel, op: dict, sources: Sources):
    flags = op.get("flags", FLAG_SIDES)
    for p in op_cells(op):
        model.blocks[p] = model.blocks.get(p, 0) | flags


def op_unblock(model: MapModel, op: dict, sources: Sources):
    for p in op_cells(op):
        f = model.blocks.get(p, 0) & ~FLAG_SIDES
        if f:
            model.blocks[p] = f
        else:
            model.blocks.pop(p, None)


def op_trigger(model: MapModel, op: dict, sources: Sources):
    for p in rect_cells(op["rect"]):
        if op["value"]:
            model.triggers[p] = op["value"]
        else:
            model.triggers.pop(p, None)


def op_npc(model: MapModel, op: dict, sources: Sources):
    """A fixed NPC (city services in the hub). Stats from npcs.dat, like the dungeon NPCs."""
    model.fixed_npcs.append((op["npcIndex"], op["x"], op["y"]))


def op_exit(model: MapModel, op: dict, sources: Sources):
    model.pending_exits.append({"x": op["x"], "y": op["y"], "destMap": op["destMap"], "dest": op["dest"]})


def op_clip(model: MapModel, op: dict, sources: Sources):
    """Block everything outside the rectangle and drop NPCs, objects and exits there."""
    x, y, w, h = op["rect"]
    inside = lambda px, py: x <= px < x + w and y <= py < y + h  # noqa: E731
    for py in range(1, SIZE + 1):
        for px in range(1, SIZE + 1):
            if not inside(px, py):
                model.blocks[(px, py)] = model.blocks.get((px, py), 0) | FLAG_SIDES
    model.npcs = [n for n in model.npcs if inside(n["x"], n["y"])]
    model.objects = [o for o in model.objects if inside(o["x"], o["y"])]
    model.exits = {p: d for p, d in model.exits.items() if inside(*p)}


OPS = {"stamp": op_stamp, "paint": op_paint, "erase": op_erase, "block": op_block, "unblock": op_unblock,
       "trigger": op_trigger, "exit": op_exit, "clip": op_clip, "npc": op_npc, "light": op_light}
ART_OPS = {"stamp", "paint", "erase", "light"}


# ---------------------------------------------------------------- dungeon floors (Contenido)

def expand_dungeon(spec: dict, sources: Sources) -> dict:
    """Turns {"dungeon": "P1"} into base/remove/clip/exits/arrivals/zones/music from dungeon-npcs.json."""
    floor = sources.floor(spec["dungeon"])
    m = floor["mapa"]
    rect = m["recorte"]
    this_id = spec["id"]

    def dest_of(stair):
        dest = stair["dest"]
        if isinstance(dest, dict):
            return [dest["x"], dest["y"]]
        return f"arrival:from{this_id}"   # the destination spec names where you land coming from this floor

    out = {
        "name": spec.get("name", floor["nombre"]),
        "base": {"copy": m["fuente"]},
        "remove": m.get("quitarDelOriginal", ["npcs", "exits"]),
        "structure": [{"op": "clip", "rect": [rect["x"], rect["y"], rect["ancho"], rect["alto"]]}],
        # The entrance tile is the up-stair itself (stepping on it teleports): spawn on the arrival tile.
        "spawn": [m["llegadaDesdeArriba"]["x"], m["llegadaDesdeArriba"]["y"]] if m.get("llegadaDesdeArriba")
                 else [m["entrada"]["x"], m["entrada"]["y"]],
        "safeCenter": [m["entrada"]["x"], m["entrada"]["y"]],
        "safeRadius": m.get("zonaSeguraRadio", 0),
        "arrivals": {},
        "zones": floor.get("zonas", []),
        "music": m.get("musicId"),
        "environment": {"baseLight": m.get("baseLight", 0), "rain": False, "snow": False, "fog": False},
        "dropOnDeath": True,   # decision 18: original death drop in the demo dungeon
        "npcHintMaps": [m["fuente"]] + sorted({o["mapa"] for n in floor.get("npcs", []) + [floor.get("jefe") or {}]
                                               for o in n.get("mapasOriginales", [])}),
    }
    for stair_key, arrival_key in (("escaleraSubida", "llegadaDesdeAbajo"), ("escaleraBajada", "llegadaDesdeArriba")):
        stair = m.get(stair_key)
        if stair:
            out["structure"].append({"op": "exit", "x": stair["x"], "y": stair["y"],
                                     "destMap": stair["destMap"], "dest": dest_of(stair)})
    if m.get("llegadaDesdeArriba"):
        out["arrivals"]["fromAbove"] = [m["llegadaDesdeArriba"]["x"], m["llegadaDesdeArriba"]["y"]]
    if m.get("llegadaDesdeAbajo"):
        out["arrivals"]["fromBelow"] = [m["llegadaDesdeAbajo"]["x"], m["llegadaDesdeAbajo"]["y"]]
    # Named arrivals used by neighbours that reference this floor by name.
    merged = {**out, **{k: v for k, v in spec.items() if k not in ("dungeon",)}}
    merged["structure"] = out["structure"] + spec.get("structure", [])
    merged["arrivals"] = {**out["arrivals"], **spec.get("arrivals", {})}
    return merged


def add_signs(model: MapModel, spec: dict, sources: Sources, problems: list[str]):
    """Clickable signs (ObjType 8): original ones keep their obj.dat text, new ones use spec["signTexts"]["x,y"]."""
    texts = spec.get("signTexts", {})
    for sign in spec.get("_signs", []):
        x, y, grh = sign["x"], sign["y"], sign["grh"]
        item = sources.items.get(sign.get("objIndex", 0), {})
        text = texts.get(f"{x},{y}") or item.get("description", "")
        if not text:
            problems.append(f"cartel ({x},{y}) sin texto: falta signTexts[\"{x},{y}\"] en la especificación")
        frames, fps = mm.resolve_grh(grh, sources.definitions)
        if not frames:
            problems.append(f"cartel ({x},{y}): GRH {grh} no existe")
        model.objects = [o for o in model.objects if (o["x"], o["y"]) != (x, y)]
        model.objects.append({"objIndex": sign.get("objIndex", 0), "x": x, "y": y, "amount": 1,
                              "name": item.get("name", "Cartel"), "description": text, "objType": 8,
                              "grhIndex": grh, "fps": round(fps, 3), "frames": frames})
        model.blocks[(x, y)] = model.blocks.get((x, y), 0) | FLAG_SIDES


def place_npcs(model: MapModel, spec: dict, sources: Sources, problems: list[str]):
    zones = spec.get("zones", [])
    if not zones:
        return
    spawn = tuple(spec["spawn"])
    reach = model.reachable(spawn)
    safe = spec.get("safeRadius", 0)
    sx, sy = spec.get("safeCenter", spawn)
    bodies, heads = sources.visuals
    taken: set = set()
    for zone in sorted(zones, key=lambda z: z.get("ordenRecorrido", 0)):
        cx, cy = zone["centro"]["x"], zone["centro"]["y"]
        radius = zone.get("radioSpawnTiles", 3)
        candidates = sorted(
            ((x, y) for y in range(cy - radius, cy + radius + 1) for x in range(cx - radius, cx + radius + 1)
             if (x - cx) ** 2 + (y - cy) ** 2 <= radius * radius and (x, y) in reach and model.npc_spawnable(x, y)
             and (x - sx) ** 2 + (y - sy) ** 2 > safe * safe),
            key=lambda p: ((p[0] - cx) ** 2 + (p[1] - cy) ** 2, p[1], p[0]))
        wanted = [(n["npcIndex"]) for n in zone["npcs"] for _ in range(n["cantidad"])]
        chosen = []
        for spread in (True, False):   # first pass keeps NPCs apart, second fills if the zone is tight
            for p in candidates:
                if len(chosen) == len(wanted):
                    break
                if p in taken or p in chosen:
                    continue
                if spread and any(abs(p[0] - q[0]) <= 1 and abs(p[1] - q[1]) <= 1 for q in chosen):
                    continue
                chosen.append(p)
        if len(chosen) < len(wanted):
            problems.append(f"zona {zone['zona']}: solo hay {len(chosen)} casillas válidas para {len(wanted)} NPC")
        for index, (x, y) in zip(wanted, chosen):
            taken.add((x, y))
            model.npcs.append(npc_entry(index, x, y, spec, sources, bodies, heads, problems))


def npc_entry(index, x, y, spec, sources, bodies, heads, problems):
    raw = sources.npc_dat.get(index)
    if raw is None:
        problems.append(f"NPC {index} no está en npcs.dat")
        raw = {}
    fresh = mm.npc_entry(index, x, y, raw, bodies, heads, None)
    template = sources.npc_template(index, spec.get("npcHintMaps", []))
    entry = copy.deepcopy(template) if template else fresh
    for key in NPC_STAT_KEYS:
        entry[key] = fresh[key]
    entry["x"], entry["y"] = x, y
    try:
        entry["level"] = int(raw.get("npclvl", 0))
    except ValueError:
        entry["level"] = 0
    info = sources.npc_info(index)
    if "respawnMinSeconds" in info:
        entry["respawnMinSeconds"] = info["respawnMinSeconds"]
        entry["respawnMaxSeconds"] = info.get("respawnMaxSeconds", info["respawnMinSeconds"])
    return entry


# ---------------------------------------------------------------- build

def load_specs(ids: list[int] | None) -> dict[int, dict]:
    specs = {}
    for path in sorted(SPECS_DIR.glob("*.json")):
        if path.name.endswith(".art.json"):
            continue
        spec = json.loads(path.read_text("utf-8"))
        spec_id = int(spec["id"])
        if not MIN_ID <= spec_id <= MAX_ID:
            raise BuildError(f"{path.name}: el ID {spec_id} no está en {MIN_ID}-{MAX_ID}")
        spec["_path"] = path.relative_to(ROOT).as_posix()
        art = SPECS_DIR / f"{spec_id}.art.json"
        art_data = json.loads(art.read_text("utf-8")) if art.exists() else {}
        spec["_art"] = art_data.get("ops", [])
        spec["_artBlock"] = art_data.get("decorBloqueante", [])   # Arte's blocking decoration (chairs, posts, pines)
        spec["_signs"] = art_data.get("carteles", [])              # Arte: position + graphic; text from obj.dat or signTexts
        specs[spec_id] = spec
    missing = [i for i in (ids or []) if i not in specs]
    if missing:
        raise BuildError(f"no hay especificación para {missing}")
    return specs


def build_one(spec: dict, sources: Sources) -> tuple[MapModel, list[str]]:
    problems: list[str] = []
    if "dungeon" in spec:
        spec = expand_dungeon(spec, sources)
        spec.setdefault("_path", "")
    base = spec["base"]
    if "copy" in base:
        model = MapModel(sources.map(base["copy"]))
    else:
        fill = base["fill"]
        model = MapModel({"xmin": 1, "xmax": SIZE, "ymin": 1, "ymax": SIZE})
        for y in range(1, SIZE + 1):
            for x in range(1, SIZE + 1):
                grh = fill["tile4x4"] + (y % 4) * 4 + (x % 4) if "tile4x4" in fill else fill["grh"]
                model.cells[(x, y, 1)] = (grh, grh)
                if fill.get("blocked", True):
                    model.blocks[(x, y)] = FLAG_SIDES
    for key in spec.get("remove", []):
        if key == "exits":
            model.exits = {}
        elif key in ("npcs", "objects", "lights", "particles"):
            setattr(model, key, [])
        elif key == "triggers":
            model.triggers = {}
    for op in spec.get("_art", []):
        if op.get("op") not in ART_OPS:
            problems.append(f"{spec['id']}.art.json: la op '{op.get('op')}' no es de arte (solo stamp, paint, erase, light)")
            continue
        OPS[op["op"]](model, op, sources)
    for op in spec.get("structure", []):
        OPS[op["op"]](model, op, sources)
    for x, y in spec.get("_artBlock", []):
        model.blocks[(x, y)] = model.blocks.get((x, y), 0) | FLAG_SIDES
    add_signs(model, spec, sources, problems)
    for ring in spec.get("rings", []):
        for p in rect_cells((ring["x"], ring["y"], RING_W, RING_H)):
            model.triggers[p] = TRIGGER_FIGHT
    model.meta.update({"version": f"demo-builder-{BUILDER_VERSION}", "mapNumber": spec["id"],
                       "xmin": 1, "xmax": SIZE, "ymin": 1, "ymax": SIZE})
    if spec.get("name"):
        model.meta["mapName"] = spec["name"]
    for key in ("zone", "terrain", "safe"):
        if key in spec:
            model.meta[key] = spec[key]
    model.extra["spawn"] = list(spec["spawn"])
    if spec.get("dropOnDeath"):
        model.extra["dropOnDeath"] = True
    model.extra["arrivals"] = spec.get("arrivals", {})
    if spec.get("rings"):
        model.extra["arenaRings"] = [{"id": r["id"], "x": r["x"], "y": r["y"], "w": RING_W, "h": RING_H,
                                      "theme": r["theme"]} for r in spec["rings"]]
    # Exits are placed now (positions matter for NPC placement); destinations are resolved later.
    for pending in model.pending_exits:
        model.exits[(pending["x"], pending["y"])] = [pending["destMap"], 0, 0]
    place_npcs(model, spec, sources, problems)
    bodies, heads = sources.visuals
    for index, x, y in model.fixed_npcs:
        if not model.npc_spawnable(x, y):
            problems.append(f"NPC fijo {index} en ({x},{y}): la casilla no sirve")
        model.npcs.append(npc_entry(index, x, y, spec, sources, bodies, heads, problems))
    layout = [[n["npcIndex"], n["x"], n["y"]] for n in model.npcs]
    model.extra["npcLayoutVersion"] = hashlib.sha1(json.dumps(layout).encode()).hexdigest()[:16]
    model.extra["demoBuilder"] = {"version": BUILDER_VERSION, "spec": spec.get("_path", "")}
    model.spec = spec
    return model, problems


def resolve_exits(models: dict[int, MapModel], problems: dict[int, list[str]]):
    for map_id, model in models.items():
        for pending in model.pending_exits:
            dest_map, dest = pending["destMap"], pending["dest"]
            target = models.get(dest_map)
            if isinstance(dest, list):
                xy = dest
            elif target is None:
                problems[map_id].append(f"salida ({pending['x']},{pending['y']}) → {dest_map}: '{dest}' necesita que {dest_map} se arme en esta corrida")
                continue
            elif dest == "spawn":
                xy = target.extra["spawn"]
            elif dest.startswith("arrival:"):
                name = dest.split(":", 1)[1]
                xy = target.extra["arrivals"].get(name)
                if xy is None:
                    problems[map_id].append(f"salida ({pending['x']},{pending['y']}): el mapa {dest_map} no define la llegada '{name}'")
                    continue
            else:
                problems[map_id].append(f"salida ({pending['x']},{pending['y']}): destino desconocido '{dest}'")
                continue
            model.exits[(pending["x"], pending["y"])] = [dest_map, xy[0], xy[1]]


def validate(map_id: int, model: MapModel, models: dict[int, MapModel], problems: list[str]):
    spawn = tuple(model.extra["spawn"])
    if not model.walkable(*spawn):
        problems.append(f"la aparición {spawn} no es caminable")
    reach = model.reachable(spawn)
    for name, xy in model.extra.get("arrivals", {}).items():
        if tuple(xy) not in reach:
            problems.append(f"la llegada '{name}' {tuple(xy)} no se alcanza desde la aparición")
    for (x, y), (dest_map, dx, dy) in model.exits.items():
        if not any(n in reach for n in ((x, y), (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))):
            problems.append(f"la salida ({x},{y}) no se alcanza desde la aparición")
        target = models.get(dest_map)
        if target is None:
            if dest_map >= MIN_ID or not (MAPS_DIR / f"map_{dest_map}.json").exists():
                problems.append(f"la salida ({x},{y}) va a {dest_map}, que no se armó ni existe")
            continue
        if not target.walkable(dx, dy):
            problems.append(f"la salida ({x},{y}) llega a {dest_map} ({dx},{dy}), que no es caminable")
        one_way = [x, y] in model.spec.get("oneWayExits", [])
        if not one_way and not any(d[0] == map_id for d in target.exits.values()):
            problems.append(f"la salida ({x},{y}) va a {dest_map}, pero {dest_map} no tiene salida de vuelta a {map_id}")
    for npc in model.npcs:
        if (npc["x"], npc["y"]) not in reach:
            problems.append(f"NPC {npc['npcIndex']} en ({npc['x']},{npc['y']}) fuera del recorrido")
    for ring in model.extra.get("arenaRings", []):
        interior = set(rect_cells((ring["x"], ring["y"], RING_W, RING_H)))
        blocked_inside = [p for p in interior if not model.walkable(*p)]
        if blocked_inside:
            problems.append(f"ring {ring['id']}: {len(blocked_inside)} casillas del interior no son caminables")
        border = {(x, y) for x in range(ring["x"] - 1, ring["x"] + RING_W + 1) for y in range(ring["y"] - 1, ring["y"] + RING_H + 1)} - interior
        open_border = sorted(p for p in border if model.walkable(*p))
        if open_border:
            problems.append(f"ring {ring['id']}: borde abierto en {open_border[:4]} (G-07)")
        if interior & reach:
            problems.append(f"ring {ring['id']}: el interior se alcanza caminando desde la aparición (los retos entran por warp)")


def build(ids: list[int] | None, sources: Sources):
    specs = load_specs(None)
    models: dict[int, MapModel] = {}
    problems: dict[int, list[str]] = {}
    for spec_id, spec in specs.items():   # always build every spec: exits are validated in both directions
        models[spec_id], problems[spec_id] = build_one(spec, sources)
    resolve_exits(models, problems)
    for map_id, model in models.items():
        validate(map_id, model, models, problems[map_id])
    selected = ids or sorted(models)
    outputs = {}
    for map_id in selected:
        data, render_problems = models[map_id].to_json(sources)
        problems[map_id].extend(render_problems)
        outputs[map_id] = data
    return outputs, {k: problems[k] for k in selected}, models


# ---------------------------------------------------------------- writing

def lock_allows_write() -> tuple[bool, str]:
    line = next((l for l in TABLERO.read_text("utf-8").splitlines() if l.startswith("**Unity en uso por:**")), "")
    owner = line.replace("**Unity en uso por:**", "").strip()
    return owner.startswith("libre") or owner.startswith("Programación"), owner


def merge_catalog(path: Path, rows: dict[int, dict]):
    data = json.loads(path.read_text("utf-8"))
    kept = [r for r in data["maps"] if int(r["mapNumber"]) not in rows]
    data["maps"] = kept + [rows[k] for k in sorted(rows)]
    temp = path.with_name(path.name + ".writing")
    temp.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), "utf-8")
    temp.replace(path)


def write_assets(outputs: dict[int, dict], models: dict[int, MapModel]):
    music, environment = {}, {}
    for map_id, data in outputs.items():
        assert map_id >= MIN_ID, map_id
        mm.atomic_json(MAPS_DIR / f"map_{map_id}.json", data)
        spec = models[map_id].spec
        if spec.get("music") is not None:
            music[map_id] = {"mapNumber": map_id, "musicId": spec["music"]}
        if spec.get("environment"):
            env = spec["environment"]
            environment[map_id] = {"mapNumber": map_id, "baseLight": env.get("baseLight", 0),
                                   "rain": bool(env.get("rain")), "snow": bool(env.get("snow")), "fog": bool(env.get("fog"))}
    if music:
        merge_catalog(MUSIC_CATALOG, music)
    if environment:
        merge_catalog(ENV_CATALOG, environment)
    try:
        import demo_minimap  # Arte
        for map_id, data in outputs.items():
            demo_minimap.write_minimap(map_id, demo_minimap.render_minimap(data))
    except Exception as exc:  # the map is still valid without a minimap
        print(f"AVISO: minimapas no generados ({exc})")


def show(map_id: int, box, sources: Sources):
    specs = load_specs(None)
    if map_id in specs:
        _, _, models = build([map_id], sources)
        model = models[map_id]
    else:
        model = MapModel(sources.map(map_id))
    x0, y0, x1, y1 = box or (1, 1, SIZE, SIZE)
    npcs = {(n["x"], n["y"]) for n in model.npcs}
    objects = {(o["x"], o["y"]) for o in model.objects}
    print(f"mapa {map_id} ({model.meta.get('mapName')}) · # bloqueado ~ agua ^ lava . caminable E salida N NPC o objeto 6 zona de pelea")
    print("     " + "".join(str((x // 10) % 10) if x % 10 == 0 else " " for x in range(x0, x1 + 1)))
    for y in range(y0, y1 + 1):
        row = []
        for x in range(x0, x1 + 1):
            f = model.flags(x, y)
            if (x, y) in model.exits:
                c = "E"
            elif (x, y) in npcs:
                c = "N"
            elif (x, y) in objects:
                c = "o"
            elif not model.walkable(x, y):
                c = "~" if f & FLAG_WATER and not f & FLAG_SIDES else "#"
            elif f & FLAG_LAVA:
                c = "^"
            elif model.triggers.get((x, y)) == TRIGGER_FIGHT:
                c = "6"
            else:
                c = "."
            row.append(c)
        print(f"{y:4d} " + "".join(row))


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("ids", nargs="*", type=int)
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--out", type=Path)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--show", type=int)
    args = parser.parse_args()
    sources = Sources()
    if args.show is not None:
        show(args.show, args.ids[:4] if len(args.ids) >= 4 else None, sources)
        return 0
    try:
        outputs, problems, models = build(args.ids or None, sources)
    except BuildError as exc:
        print(f"ERROR: {exc}")
        return 1
    failed = False
    for map_id in sorted(outputs):
        data = outputs[map_id]
        issues = problems[map_id]
        failed |= bool(issues)
        print(f"{'FALLA' if issues else 'OK   '} {map_id} {data.get('mapName')}: {len(data['npcs'])} NPC, "
              f"{len(data['exits'])} salidas, {len(data['sprites'])} sprites, {len(data.get('arenaRings', []))} rings")
        for issue in issues:
            print(f"        - {issue}")
    if args.check:
        differ = [m for m, data in outputs.items()
                  if not (MAPS_DIR / f"map_{m}.json").exists()
                  or json.loads((MAPS_DIR / f"map_{m}.json").read_text("utf-8")) != json.loads(json.dumps(data))]
        print("CHECK: en disco igual a lo armado" if not differ else f"CHECK: distinto o faltante en disco: {differ}")
        return 1 if differ or failed else 0
    if failed:
        print("No se escribe nada: hay fallas.")
        return 1
    if args.out:
        for map_id, data in outputs.items():
            mm.atomic_json(args.out / f"map_{map_id}.json", data)
        print(f"Escrito en {args.out}")
    if args.write:
        allowed, owner = lock_allows_write()
        if not allowed:
            print(f"No se escribe en Assets: Unity está en uso por {owner}")
            return 1
        write_assets(outputs, models)
        print("Escrito en Assets. Falta: python Tools/export_online_catalog.py (Servidor) para el catálogo del servidor.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
