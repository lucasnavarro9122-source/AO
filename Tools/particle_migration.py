"""Decode VB6 particles.ind and import map particle definitions for Unity."""

from __future__ import annotations

import argparse
import json
import re
import shutil
import struct
from pathlib import Path

from PIL import Image

from map_migration import graphics_index, resolve_grh


ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Archivos Originales/Recursos-master/Recursos-master"
MAPS = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"
TEXTURES = ROOT / "Assets/Resources/AOMigrator/WorldV07/Textures"
OUTPUT = ROOT / "Assets/Resources/AOMigrator/WorldV07/particle_defs.json"
REPORT = ROOT / "MigrationReports/particle_import_report.json"
MAP_FILE = re.compile(r"map_[0-9]+\.json\Z")


def decode(path: Path) -> list[dict]:
    content = path.read_bytes()
    offset = 0

    def read(fmt: str):
        nonlocal offset
        size = struct.calcsize(fmt)
        if offset + size > len(content):
            raise ValueError(f"particles.ind truncado en byte {offset}")
        value = struct.unpack_from(fmt, content, offset)
        offset += size
        return value

    (count,) = read("<h")
    result = []
    for index in range(1, count + 1):
        (name_length,) = read("<H")
        if name_length > 1000 or offset + name_length > len(content):
            raise ValueError(f"Nombre de partícula inválido en byte {offset}")
        name = content[offset:offset + name_length].decode("cp1252")
        offset += name_length
        (particle_count, grh_count, particle_id, x1, y1, x2, y2,
         angle, vecx1, vecx2, vecy1, vecy2, life1, life2,
         friction) = read("<15i")
        (spin, spin_low, spin_high, alpha_blend, gravity,
         gravity_strength, bounce_strength, x_move, y_move,
         move_x1, move_x2, move_y1, move_y2) = read("<BffBBiiBB4i")
        (rank,) = read("<H")
        if rank != 1:
            raise ValueError(f"Rango de lista GRH inválido en partícula {index}")
        length, lower_bound = read("<ii")
        if length < 0 or length > 1000 or lower_bound != 1:
            raise ValueError(f"Lista GRH inválida en partícula {index}")
        grh_ids = list(read(f"<{length}i"))
        colors = list(read("<12i"))
        speed, life_counter, resize, resize_x, resize_y = read("<fihhh")
        result.append({
            "id": index, "name": name, "count": particle_count,
            "sourceId": particle_id, "grhIds": grh_ids,
            "origin": [x1, y1, x2, y2], "angle": angle,
            "velocity": [vecx1, vecx2, vecy1, vecy2],
            "life": [life1, life2], "friction": friction,
            "spin": bool(spin), "spinSpeed": [spin_low, spin_high],
            "alphaBlend": bool(alpha_blend), "gravity": bool(gravity),
            "gravityStrength": gravity_strength,
            "bounceStrength": bounce_strength,
            "moveX": bool(x_move), "moveY": bool(y_move),
            "moveBounds": [move_x1, move_x2, move_y1, move_y2],
            "colors": [colors[i:i + 3] for i in range(0, 12, 3)],
            "speed": speed, "lifeCounter": life_counter,
            "resize": bool(resize), "resizeX": resize_x,
            "resizeY": resize_y, "declaredGrhs": grh_count,
        })
    if offset != len(content):
        raise ValueError(f"particles.ind tiene {len(content) - offset} bytes finales")
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    definitions = decode(SOURCE / "init/particles.ind")
    grh = graphics_index(SOURCE / "init/graficos.ini")
    used: set[int] = set()
    for path in MAPS.glob("map_*.json"):
        if MAP_FILE.fullmatch(path.name):
            data = json.loads(path.read_text("utf-8"))
            used.update(row["particle"] for row in data.get("particles", []))
    map_used = set(used)
    # IDs from the original client's Particula_Nieve and Particula_Lluvia.
    used.update((57, 58))

    textures: set[int] = set()
    unresolved: set[int] = set()
    invalid_crops: set[int] = set()
    resolved = []
    for entry in definitions:
        if entry["id"] not in used:
            continue
        sprites = []
        for grh_id in entry["grhIds"]:
            frames, fps = resolve_grh(grh_id, grh)
            if not frames:
                unresolved.add(grh_id)
            else:
                sprites.append({"grh": grh_id, "frames": frames, "fps": fps})
                textures.update(frame["fileNum"] for frame in frames)
        entry["sprites"] = sprites
        # JsonUtility cannot deserialize arrays nested inside arrays.
        entry["cornerColors"] = [channel for bgr in entry["colors"]
                                 for channel in bgr]
        # The VB6 client passes B,G,R into SetRGBA; average its four corners.
        entry["tint"] = [sum(rgb[channel] for rgb in entry["colors"]) // 4
                         for channel in (2, 1, 0)]
        resolved.append(entry)

    fog_sprites = []
    for grh_id in (32014, 32015):
        frames, fps = resolve_grh(grh_id, grh)
        if not frames:
            unresolved.add(grh_id)
        else:
            fog_sprites.append({"grh": grh_id, "frames": frames, "fps": fps})
            textures.update(frame["fileNum"] for frame in frames)

    graphics = SOURCE / "Graficos"
    missing_textures = sorted(number for number in textures
                              if not (graphics / f"{number}.png").is_file())
    for entry in resolved:
        for sprite in entry["sprites"]:
            for frame in sprite["frames"]:
                source = graphics / f'{frame["fileNum"]}.png'
                if not source.is_file():
                    continue
                with Image.open(source) as image:
                    if frame["sx"] < 0 or frame["sy"] < 0 or \
                            frame["sx"] + frame["width"] > image.width or \
                            frame["sy"] + frame["height"] > image.height:
                        invalid_crops.add(frame["fileNum"])
    for sprite in fog_sprites:
        for frame in sprite["frames"]:
            source = graphics / f'{frame["fileNum"]}.png'
            if not source.is_file():
                continue
            with Image.open(source) as image:
                if frame["sx"] < 0 or frame["sy"] < 0 or \
                        frame["sx"] + frame["width"] > image.width or \
                        frame["sy"] + frame["height"] > image.height:
                    invalid_crops.add(frame["fileNum"])

    report = {"source_definitions": len(definitions),
              "map_used_types": len(map_used), "weather_types": [57, 58],
              "used_types": len(used), "resolved_types": len(resolved),
              "fog_grh": [sprite["grh"] for sprite in fog_sprites],
              "unresolved_grh": sorted(unresolved),
              "missing_textures": missing_textures,
              "invalid_crops": sorted(invalid_crops),
              "texture_count": len(textures),
              "max_particles_per_group": max((d["count"] for d in resolved), default=0),
              "applied": args.apply}

    if args.apply:
        if unresolved or missing_textures or invalid_crops:
            raise ValueError("Faltan GRH o texturas, o hay recortes inválidos")
        TEXTURES.mkdir(parents=True, exist_ok=True)
        for number in sorted(textures):
            target = TEXTURES / f"tex_{number}.png"
            if not target.exists():
                shutil.copy2(graphics / f"{number}.png", target)
        OUTPUT.write_text(json.dumps({"version": "1.1", "definitions": resolved,
                                      "fogSprites": fog_sprites},
                                     ensure_ascii=False, separators=(",", ":")), "utf-8")
        REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2), "utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
