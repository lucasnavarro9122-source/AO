"""Audit AO CSM maps against Unity WorldV07 JSON; batch import follows."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import struct
from collections import Counter
from pathlib import Path

from PIL import Image


MAP_NAME = re.compile(r"mapa([0-9]+)\.csm", re.IGNORECASE)
# Mapas >= 1000 son de la demo (demo_map_builder.py): no salen de un CSM y
# las migraciones que reescriben catalogos tienen que conservarlos.
DEMO_MIN_MAP = 1000


def keep_demo_entries(catalog: Path) -> list[dict]:
    """Entradas >= DEMO_MIN_MAP que ya estan en un catalogo {"maps": [...]}."""
    if not catalog.exists():
        return []
    rows = json.loads(catalog.read_text(encoding="utf-8")).get("maps", [])
    return [row for row in rows
            if int(row.get("mapNumber", 0)) >= DEMO_MIN_MAP]


class CSMError(ValueError):
    pass


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.pos = 0

    def take(self, fmt: str):
        size = struct.calcsize(fmt)
        if self.pos + size > len(self.data):
            raise CSMError(f"CSM truncado en byte {self.pos}")
        value = struct.unpack_from(fmt, self.data, self.pos)
        self.pos += size
        return value

    def string(self) -> str:
        (length,) = self.take("<H")
        if length > 4096 or self.pos + length > len(self.data):
            raise CSMError(f"String VB6 inválido en byte {self.pos}")
        value = self.data[self.pos:self.pos + length].decode("cp1252")
        self.pos += length
        return value


def parse_csm(path: Path) -> dict:
    reader = Reader(path.read_bytes())
    counts = reader.take("<11i")
    if any(n < 0 or n > 100000 for n in counts):
        raise CSMError("Conteos CSM inválidos")
    xmax, xmin, ymax, ymin = reader.take("<4h")
    name = reader.string()
    (backup,) = reader.take("<B")
    restrict = reader.string()
    music_high, music_low = reader.take("<ii")
    (safe,) = reader.take("<B")
    zone, terrain, ambient = reader.string(), reader.string(), reader.string()
    base_light, letter_grh, extra1, extra2 = reader.take("<4i")
    extra3 = reader.string()
    rain, snow, fog = reader.take("<3B")

    def records(count: int, fmt: str) -> list[tuple]:
        return [reader.take(fmt) for _ in range(count)]

    blocks = records(counts[0], "<hhB")
    layers = [records(counts[i], "<hhi") for i in range(1, 5)]
    triggers = records(counts[5], "<hhh")
    particles = records(counts[7], "<hhi")
    lights = records(counts[6], "<hhiB")
    objects = records(counts[9], "<hhhh")
    npcs = records(counts[8], "<hhh")
    exits = records(counts[10], "<hhhhh")
    number = int(MAP_NAME.fullmatch(path.name).group(1))
    if (xmin, xmax, ymin, ymax) == (0, 0, 0, 0):
        xmin, xmax, ymin, ymax = 1, 100, 1, 100
    return {
        "number": number, "name": name, "zone": zone, "terrain": terrain,
        "ambient": ambient, "safe": bool(safe),
        "bounds": (xmin, xmax, ymin, ymax),
        "blocks": blocks, "layers": layers, "triggers": triggers,
        "particles": particles, "lights": lights, "objects": objects,
        "npcs": npcs, "exits": exits,
        "trailing_bytes": len(reader.data) - reader.pos,
        "meta": {"backup": backup, "restrict": restrict,
                 "music_high": music_high, "music_low": music_low,
                 "base_light": base_light, "letter_grh": letter_grh,
                 "extra1": extra1, "extra2": extra2, "extra3": extra3,
                 "rain": rain, "snow": snow, "fog": fog},
    }


def graphics_index(path: Path) -> dict[int, tuple]:
    """Read GRH definitions. Last definition wins, matching VB6 loader."""
    definitions = {}
    for raw in path.read_text("cp1252").splitlines():
        if not raw.lower().startswith("grh") or "=" not in raw:
            continue
        key, value = raw.split("=", 1)
        try:
            grh = int(key[3:])
            fields = [part.strip() for part in value.split("-")]
            count = int(fields[0])
            if count == 1 and len(fields) == 6:
                definitions[grh] = ("static", *map(int, fields[1:6]))
            elif count > 1 and len(fields) >= count + 2:
                definitions[grh] = ("animated", tuple(map(int, fields[1:count + 1])),
                                    float(fields[count + 1]))
            elif count == 1 and len(fields) > 6:
                inferred = len(fields) - 2
                definitions[grh] = ("animated", tuple(map(int, fields[1:inferred + 1])),
                                    float(fields[inferred + 1]))
        except (ValueError, IndexError):
            continue
    return definitions


def resolve_grh(grh: int, definitions: dict[int, tuple],
                seen: frozenset[int] = frozenset()) -> tuple[list[dict], float]:
    if grh in seen or grh not in definitions:
        return [], 0.0
    definition = definitions[grh]
    if definition[0] == "static":
        file_num, sx, sy, width, height = definition[1:]
        if file_num <= 0 or width <= 0 or height <= 0:
            return [], 0.0
        return [{"fileNum": file_num, "sx": sx, "sy": sy,
                 "width": width, "height": height,
                 "key": f"f{file_num}_{sx}_{sy}_{width}_{height}"}], 0.0
    frames = []
    for frame_id in definition[1]:
        subframes, _ = resolve_grh(frame_id, definitions, seen | {grh})
        if not subframes:
            return [], 0.0
        frames.append(subframes[0])
    duration_ms = definition[2]
    fps = 1000.0 * len(frames) / duration_ms if duration_ms > 0 else 8.0
    return frames, fps


def npc_dat(path: Path) -> dict[int, dict[str, str]]:
    result = {}
    current = None
    for line in path.read_text("cp1252").splitlines():
        match = re.match(r"\s*\[NPC(\d+)\]", line, re.IGNORECASE)
        if match:
            current = result.setdefault(int(match.group(1)), {})
        elif current is not None and "=" in line and not line.lstrip().startswith("'"):
            key, value = line.split("=", 1)
            current[key.strip().lower()] = value.split("'", 1)[0].strip()
    return result


def npc_entry(index: int, x: int, y: int, raw: dict[str, str],
              bodies: dict[int, dict], heads: dict[int, dict],
              template: dict | None) -> dict:
    if template is not None:
        return {**template, "x": x, "y": y}

    def number(key: str, default: int = 0) -> int:
        try:
            return int(raw.get(key, default))
        except ValueError:
            return default

    body_id, head_id = number("body"), number("head")
    body, head = bodies.get(body_id), heads.get(head_id)
    directions = []
    for heading in range(1, 5):
        def frames(record: dict | None) -> list[dict]:
            if not record:
                return []
            return next((d["frames"] for d in record["directions"]
                         if d["heading"] == heading), [])
        directions.append({"heading": heading, "body": frames(body),
                           "head": frames(head), "helmet": [],
                           "weapon": [], "shield": []})
    movement = number("movement", 1)
    return {
        "npcIndex": index, "x": x, "y": y,
        "name": raw.get("name", f"NPC {index}"),
        "description": raw.get("desc", ""),
        "npcType": number("npctype"), "movement": movement,
        "sourceMovement": movement, "hostile": bool(number("hostile")),
        "attackRange": number("attackrange"),
        "preferredRange": number("preferredrange"),
        "visionRange": number("visionrange", 8),
        "moveIntervalMs": number("intervalomovimiento", 300),
        "waterValid": bool(number("aguavalida")),
        "landInvalid": bool(number("tierrainvalida")),
        "lavaValid": bool(number("lavavalida")),
        "walkRoute": [],
        "maxHp": number("maxhp"), "minHit": number("minhit"),
        "maxHit": number("maxhit"), "defense": number("def"),
        "attackPower": number("poderataque"),
        "evasionPower": number("poderevasion"),
        "attackable": bool(number("attackable")),
        "attackIntervalMs": number("intervaloataque", 2000),
        "respawnMinSeconds": 0, "respawnMaxSeconds": 0,
        "giveExp": number("giveexp"), "giveGold": number("givegld"),
        "drops": [], "showName": bool(number("showname")),
        "heading": number("heading", 3), "body": body_id,
        "head": head_id, "helmet": 0, "weapon": 0, "shield": 0,
        "walkFps": 18.0,
        "headOffsetX": body.get("headOffsetX", 0) if body else 0,
        "headOffsetY": body.get("headOffsetY", 0) if body else 0,
        "bodyShiftX": body.get("bodyShiftX", 0) if body else 0,
        "directions": directions,
    }


def world_json(source: dict, definitions: dict[int, tuple],
               npc_data: dict[int, dict[str, str]], bodies: dict[int, dict],
               heads: dict[int, dict], templates: dict[int, dict],
               items: dict[int, dict]) -> tuple[dict, dict]:
    cells = [{"x": x, "y": y, "layer": layer, "grh": grh, "sprite": grh}
             for layer, rows in enumerate(source["layers"], 1)
             for x, y, grh in rows]
    unique_grh = sorted({c["grh"] for c in cells})
    sprites = []
    unresolved = []
    for grh in unique_grh:
        frames, fps = resolve_grh(grh, definitions)
        if not frames:
            unresolved.append(grh)
            continue
        sprite = {"id": grh, **frames[0]}
        if len(frames) > 1:
            sprite["frames"] = frames
            sprite["fps"] = round(fps, 3)
        sprites.append(sprite)

    npcs = []
    missing_npc_definitions = set()
    missing_npc_visuals = set()
    for x, y, index in source["npcs"]:
        raw = npc_data.get(index)
        if raw is None and index not in templates:
            missing_npc_definitions.add(index)
        entry = npc_entry(index, x, y, raw or {}, bodies, heads,
                          templates.get(index))
        if not any(d["body"] for d in entry["directions"]):
            missing_npc_visuals.add(index)
        npcs.append(entry)

    objects = []
    missing_objects = set()
    for x, y, index, amount in source["objects"]:
        item = items.get(index)
        if item is None:
            missing_objects.add(index)
            item = {}
        grh = item.get("grhIndex", 0)
        frames, fps = resolve_grh(grh, definitions)
        objects.append({"objIndex": index, "x": x, "y": y,
                        "amount": amount, "name": item.get("name", f"OBJ {index}"),
                        "description": item.get("description", ""),
                        "objType": item.get("objType", 0), "grhIndex": grh,
                        "fps": round(fps, 3), "frames": frames})

    xmin, xmax, ymin, ymax = source["bounds"]
    data = {
        "version": "0.7.1-batch", "mapNumber": source["number"],
        "mapName": source["name"], "zone": source["zone"],
        "terrain": source["terrain"], "ambient": source["ambient"],
        "safe": source["safe"], "xmin": xmin, "xmax": xmax,
        "ymin": ymin, "ymax": ymax,
        "cells": cells, "sprites": sprites,
        "blocks": [{"x": x, "y": y, "flags": flags}
                   for x, y, flags in source["blocks"]],
        "triggers": [{"x": x, "y": y, "trigger": trigger}
                     for x, y, trigger in source["triggers"]],
        "exits": [{"x": x, "y": y, "destMap": dest_map,
                   "destX": dest_x, "destY": dest_y}
                  for x, y, dest_map, dest_x, dest_y in source["exits"]],
        "npcs": npcs, "objects": objects,
        "lights": [{"x": x, "y": y, "color": color, "range": radius}
                   for x, y, color, radius in source["lights"]],
        "particles": [{"x": x, "y": y, "particle": particle}
                      for x, y, particle in source["particles"]],
    }
    issues = {"unresolved_grh": unresolved,
              "missing_npc_definitions": sorted(missing_npc_definitions),
              "missing_npc_visuals": sorted(missing_npc_visuals),
              "missing_objects": sorted(missing_objects),
              "trailing_bytes": source["trailing_bytes"]}
    return data, issues


def all_frames(data: dict):
    for sprite in data.get("sprites", []):
        yield from sprite.get("frames") or [sprite]
    for npc in data.get("npcs", []):
        for direction in npc.get("directions", []):
            for part in ("body", "head", "helmet", "weapon", "shield"):
                yield from direction.get(part, [])
    for obj in data.get("objects", []):
        yield from obj.get("frames", [])


def add_existing_visuals(data: dict, source: dict,
                         definitions: dict[int, tuple]) -> bool:
    changed = False
    by_id = {sprite["id"]: sprite for sprite in data.get("sprites", [])}
    for grh in {g for layer in source["layers"] for _, _, g in layer}:
        frames, fps = resolve_grh(grh, definitions)
        if not frames:
            continue
        if grh not in by_id:
            sprite = {"id": grh, **frames[0]}
            data["sprites"].append(sprite)
            by_id[grh] = sprite
            changed = True
        sprite = by_id[grh]
        if len(frames) > 1 and sprite.get("frames") != frames:
            sprite["frames"] = frames
            sprite["fps"] = round(fps, 3)
            changed = True
    for key, rows, maker in (
        ("lights", source["lights"],
         lambda r: {"x": r[0], "y": r[1], "color": r[2], "range": r[3]}),
        ("particles", source["particles"],
         lambda r: {"x": r[0], "y": r[1], "particle": r[2]}),
    ):
        if key not in data:
            data[key] = [maker(row) for row in rows]
            changed = True
    return changed


def atomic_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + ".writing")
    temp.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), "utf-8")
    temp.replace(path)


def batch_import(root: Path, source_dir: Path, output_dir: Path,
                 definitions: dict[int, tuple], limit: int | None,
                 dry_run: bool) -> dict:
    visual_data = json.loads((root / "Assets/Resources/AOMigrator/CharacterV111/character_visuals.json").read_text("utf-8"))
    bodies = {entry["id"]: entry for entry in visual_data["bodies"]}
    heads = {entry["id"]: entry for entry in visual_data["heads"]}
    items = {entry["index"]: entry for entry in json.loads(
        (root / "Assets/Resources/AOMigrator/ItemsV10/items.json").read_text("utf-8"))["items"]}
    npc_data = npc_dat(root / "Archivos Originales/Recursos-master/Recursos-master/Dat/npcs.dat")
    templates = {}
    for path in output_dir.glob("map_*.json"):
        for entry in json.loads(path.read_text("utf-8")).get("npcs", []):
            templates.setdefault(entry["npcIndex"], entry)

    paths = sorted((p for p in source_dir.iterdir() if MAP_NAME.fullmatch(p.name)),
                   key=lambda p: int(MAP_NAME.fullmatch(p.name).group(1)))
    if limit is not None:
        paths = paths[:limit]
    results = []
    required_extents = {}
    summary = Counter()
    backup_dir = root / "MigrationReports/backup_existing_maps"
    for path in paths:
        number = int(MAP_NAME.fullmatch(path.name).group(1))
        try:
            source = parse_csm(path)
            target_path = output_dir / f"map_{number}.json"
            if target_path.exists():
                data = json.loads(target_path.read_text("utf-8"))
                changed = add_existing_visuals(data, source, definitions)
                issues = {"trailing_bytes": source["trailing_bytes"]}
                status = "updated" if changed else "preserved"
                if changed and not dry_run:
                    backup_dir.mkdir(parents=True, exist_ok=True)
                    backup = backup_dir / target_path.name
                    if not backup.exists():
                        shutil.copy2(target_path, backup)
                    atomic_json(target_path, data)
            else:
                data, issues = world_json(source, definitions, npc_data,
                                          bodies, heads, templates, items)
                status = "created"
                if not dry_run:
                    atomic_json(target_path, data)
            validation = compare(source, data, definitions)["checks"]
            if not all(validation.values()):
                status = "validation_failed"
            for frame in all_frames(data):
                if not isinstance(frame, dict):
                    continue
                number_id = frame.get("fileNum", 0)
                if number_id <= 0:
                    continue
                x = frame.get("sx", 0) + frame.get("width", 0)
                y = frame.get("sy", 0) + frame.get("height", 0)
                old = required_extents.get(number_id, (0, 0))
                required_extents[number_id] = (max(old[0], x), max(old[1], y))
            summary[status] += 1
            results.append({"map": number, "status": status,
                            "source_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                            "checks_failed": [key for key, ok in validation.items() if not ok],
                            "issues": issues})
        except (CSMError, struct.error, UnicodeDecodeError, OSError,
                ValueError, KeyError, TypeError) as exc:
            summary["failed"] += 1
            results.append({"map": number, "status": "failed", "error": str(exc)})

    copied = 0
    padded = []
    missing_textures = []
    source_graphics = root / "Archivos Originales/Recursos-master/Recursos-master/Graficos"
    source_by_id = {int(p.stem): p for p in source_graphics.iterdir()
                    if p.stem.isdigit() and p.suffix.lower() == ".png"}
    texture_dir = root / "Assets/Resources/AOMigrator/WorldV07/Textures"
    for number, (width, height) in sorted(required_extents.items()):
        original = source_by_id.get(number)
        if original is None:
            missing_textures.append(number)
            continue
        target = texture_dir / f"tex_{number}.png"
        with Image.open(original) as image:
            native_width, native_height = image.size
            goal_width, goal_height = max(native_width, width), max(native_height, height)
        if target.exists():
            with Image.open(target) as image:
                if image.width >= goal_width and image.height >= goal_height:
                    continue
        if dry_run:
            copied += 1
            if (goal_width, goal_height) != (native_width, native_height):
                padded.append(number)
            continue
        texture_dir.mkdir(parents=True, exist_ok=True)
        temp = target.with_name(f"tex_{number}.writing.png")
        if (goal_width, goal_height) == (native_width, native_height):
            shutil.copy2(original, temp)
        else:
            with Image.open(original) as image:
                padded_image = Image.new("RGBA", (goal_width, goal_height), (0, 0, 0, 0))
                padded_image.paste(image.convert("RGBA"), (0, 0))
                padded_image.save(temp)
            padded.append(number)
        temp.replace(target)
        copied += 1

    report = {"source_files": len(paths), "map_status": dict(summary),
              "textures_required": len(required_extents),
              "textures_copied_or_replaced": copied,
              "textures_padded": padded,
              "missing_textures": missing_textures,
              "dry_run": dry_run, "maps": results}
    if not dry_run:
        atomic_json(root / "MigrationReports/map_batch_report.json", report)
    return report


def compare(source: dict, target: dict, definitions: dict[int, tuple] | None = None) -> dict:
    checks = {}
    checks["map_number"] = source["number"] == target.get("mapNumber")
    checks["name"] = source["name"] == target.get("mapName")
    checks["zone"] = source["zone"] == target.get("zone")
    checks["terrain"] = source["terrain"] == target.get("terrain")
    checks["bounds"] = list(source["bounds"]) == [target.get(k) for k in ("xmin", "xmax", "ymin", "ymax")]

    expected_cells = Counter((x, y, layer, grh)
                             for layer, rows in enumerate(source["layers"], 1)
                             for x, y, grh in rows)
    actual_cells = Counter((c["x"], c["y"], c["layer"], c["grh"])
                           for c in target.get("cells", []))
    checks["cells"] = expected_cells == actual_cells
    checks["blocks"] = Counter(source["blocks"]) == Counter(
        (b["x"], b["y"], b["flags"]) for b in target.get("blocks", []))
    checks["triggers"] = Counter(source["triggers"]) == Counter(
        (t["x"], t["y"], t["trigger"]) for t in target.get("triggers", []))
    checks["exits"] = Counter(source["exits"]) == Counter(
        (e["x"], e["y"], e["destMap"], e["destX"], e["destY"])
        for e in target.get("exits", []))
    checks["npcs"] = Counter(source["npcs"]) == Counter(
        (n["x"], n["y"], n["npcIndex"]) for n in target.get("npcs", []))
    checks["objects"] = Counter(source["objects"]) == Counter(
        (o["x"], o["y"], o["objIndex"], o["amount"])
        for o in target.get("objects", []))
    if definitions is not None:
        wrong_sprites = []
        unresolved = []
        for sprite in target.get("sprites", []):
            frames, _ = resolve_grh(sprite["id"], definitions)
            if not frames:
                unresolved.append(sprite["id"])
            elif any(sprite.get(k) != frames[0][k]
                     for k in ("fileNum", "sx", "sy", "width", "height")):
                wrong_sprites.append(sprite["id"])
        checks["sprite_definitions"] = not wrong_sprites and not unresolved
        missing_sprite = {g for layer in source["layers"] for _, _, g in layer} - {
            s["id"] for s in target.get("sprites", [])}
        checks["sprite_coverage"] = not missing_sprite
        checks["animation_frames"] = all(
            len(sprite.get("frames", [])) > 1
            for sprite in target.get("sprites", [])
            if len(resolve_grh(sprite["id"], definitions)[0]) > 1)
    result = {
        "source": source["number"], "checks": checks,
        "source_counts": {
            "layers": [len(rows) for rows in source["layers"]],
            "blocks": len(source["blocks"]), "triggers": len(source["triggers"]),
            "exits": len(source["exits"]), "npcs": len(source["npcs"]),
            "objects": len(source["objects"]), "lights": len(source["lights"]),
            "particles": len(source["particles"]),
            "trailing_bytes": source["trailing_bytes"],
        },
    }
    if definitions is not None:
        result["visual_issues"] = {
            "wrong_sprites": wrong_sprites[:20],
            "unresolved_sprites": unresolved[:20],
            "missing_sprite_ids": sorted(missing_sprite)[:20],
            "animated_grh_without_frames": sum(
                len(resolve_grh(s["id"], definitions)[0]) > 1
                and len(s.get("frames", [])) < 2 for s in target.get("sprites", [])),
        }
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--audit", nargs="*", type=int, default=[1, 2, 40])
    parser.add_argument("--scan", action="store_true")
    parser.add_argument("--batch", action="store_true")
    parser.add_argument("--limit", type=int)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    root = args.project.resolve()
    source_dir = root / "Archivos Originales/Recursos-master/Recursos-master/Mapas"
    output_dir = root / "Assets/Resources/AOMigrator/WorldV07/Maps"
    definitions = graphics_index(root / "Archivos Originales/Recursos-master/Recursos-master/init/graficos.ini")
    if args.batch:
        report = batch_import(root, source_dir, output_dir, definitions,
                              args.limit, args.dry_run)
        print(json.dumps({key: value for key, value in report.items() if key != "maps"},
                         ensure_ascii=False, indent=2))
        print("failed_maps:", [entry for entry in report["maps"]
                               if entry["status"] in ("failed", "validation_failed")][:20])
        return
    if args.scan:
        totals = Counter()
        failures = []
        used_grh = set()
        used_npc = set()
        used_objects = set()
        animated = set()
        trailing = []
        files = sorted((p for p in source_dir.iterdir() if MAP_NAME.fullmatch(p.name)),
                       key=lambda p: int(MAP_NAME.fullmatch(p.name).group(1)))
        for path in files:
            try:
                m = parse_csm(path)
                for layer in m["layers"]:
                    totals["cells"] += len(layer)
                    used_grh.update(record[2] for record in layer)
                for key in ("blocks", "triggers", "particles", "lights",
                            "objects", "npcs", "exits"):
                    totals[key] += len(m[key])
                used_npc.update(n[2] for n in m["npcs"])
                used_objects.update(o[2] for o in m["objects"])
                if m["trailing_bytes"]:
                    trailing.append((m["number"], m["trailing_bytes"]))
            except (CSMError, struct.error, UnicodeDecodeError, OSError) as exc:
                failures.append({"file": path.name, "error": str(exc)})
        unresolved = sorted(g for g in used_grh if not resolve_grh(g, definitions)[0])
        animated = {g for g in used_grh if len(resolve_grh(g, definitions)[0]) > 1}
        texture_numbers = {f["fileNum"] for g in used_grh
                           for f in resolve_grh(g, definitions)[0]}
        graphics_dir = root / "Archivos Originales/Recursos-master/Recursos-master/Graficos"
        existing_textures = {int(p.stem) for p in graphics_dir.iterdir()
                             if p.suffix.lower() == ".png" and p.stem.isdigit()}
        print(json.dumps({"source_files": len(files), "parsed": len(files)-len(failures),
                          "failures": failures[:30], "totals": totals,
                          "unique_grh": len(used_grh), "unique_npc": len(used_npc),
                          "unique_objects": len(used_objects),
                          "animated_grh": len(animated),
                          "unresolved_grh": len(unresolved),
                          "unresolved_examples": unresolved[:30],
                          "needed_textures": len(texture_numbers),
                          "missing_textures": sorted(texture_numbers-existing_textures)[:30],
                          "trailing_count": len(trailing),
                          "trailing_max": max((n for _, n in trailing), default=0)},
                         ensure_ascii=False, indent=2))
        return
    result = []
    for number in args.audit:
        original = parse_csm(source_dir / f"mapa{number}.csm")
        target = json.loads((output_dir / f"map_{number}.json").read_text("utf-8"))
        result.append(compare(original, target, definitions))
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
