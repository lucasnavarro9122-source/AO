"""Corrige en los mapas ya migrados 3 datos de NPC que map_migration.py leía con claves que npcs.dat no tiene.

- defense:        npcs.dat usa DEF            (MODULO_NPCs.bas: .StatsDef = ...GetValue(SectionName, "DEF"))
- preferredRange: npcs.dat usa PreferedRange  (con una sola r)
- visión:         npcs.dat usa Distancia; si es 0, el servidor usa 15 x 13 por eje
                  (AI_NPC.bas EnRangoVision, Consts.bas DEFAULT_NPC_VISION_RANGE_X/Y)

Solo toca esos campos (y agrega visionRangeX/visionRangeY). Por defecto simula; con --aplicar
guarda un respaldo en MigrationReports/backup_npc_stats_<fecha>/ y reescribe con el mismo formato.
Después: python Tools/export_online_catalog.py
Aprobado por Lucas el 25/09 (DEF y rango preferido originales, visión 15 x 13).
"""
import argparse
import json
import re
import shutil
import sys
import time
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAPS = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"
DAT = ROOT / "Archivos Originales/Recursos-master/Recursos-master/Dat/npcs.dat"
VISION_X, VISION_Y = 15, 13


def npc_dat(path: Path) -> dict[int, dict[str, str]]:
    # Igual que map_migration.npc_dat (sin importar ese módulo, que necesita Pillow).
    result, current = {}, None
    for line in path.read_text("cp1252").splitlines():
        match = re.match(r"\s*\[NPC(\d+)\]", line, re.IGNORECASE)
        if match:
            current = result.setdefault(int(match.group(1)), {})
        elif current is not None and "=" in line and not line.lstrip().startswith("'"):
            key, value = line.split("=", 1)
            current[key.strip().lower()] = value.split("'", 1)[0].strip()
    return result


def number(raw: dict, key: str, default: int = 0) -> int:
    try:
        return int(raw.get(key, default))
    except ValueError:
        return default


def original_values(raw: dict) -> dict:
    distancia = number(raw, "distancia")
    vx, vy = (distancia, distancia) if distancia > 0 else (VISION_X, VISION_Y)
    # visionRange queda como respaldo para código que todavía mira un solo radio.
    return {"defense": number(raw, "def"), "preferredRange": number(raw, "preferedrange"),
            "visionRange": max(vx, vy), "visionRangeX": vx, "visionRangeY": vy}


def with_values(npc: dict, values: dict) -> dict:
    # Mantener el orden de claves: visionRangeX/Y van justo después de visionRange.
    out = {}
    for key, value in npc.items():
        if key in ("visionRangeX", "visionRangeY"):
            continue
        out[key] = values.get(key, value)
        if key == "visionRange":
            out["visionRangeX"], out["visionRangeY"] = values["visionRangeX"], values["visionRangeY"]
    return out


def run() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--dat", type=Path, default=DAT, help="npcs.dat original (cp1252)")
    parser.add_argument("--aplicar", action="store_true", help="escribir los cambios (sin esto, solo simula)")
    args = parser.parse_args()
    if not args.dat.is_file():
        print(f"No existe {args.dat}. Pasá la ruta con --dat.")
        return 2
    dat = npc_dat(args.dat)
    stats, changed_npcs, skipped = Counter(), {}, {}
    pending = []
    for path in sorted(MAPS.glob("map_*.json"), key=lambda p: int(p.stem.split("_")[1])):
        raw_bytes = path.read_bytes()
        data = json.loads(raw_bytes.decode("utf-8-sig"))
        if json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8") != raw_bytes:
            print(f"AVISO: {path.name} no tiene el formato compacto de map_migration; se saltea.")
            stats["mapas_salteados"] += 1
            continue
        touched = False
        for i, npc in enumerate(data.get("npcs", [])):
            stats["npc_en_mapas"] += 1
            raw = dat.get(npc.get("npcIndex"))
            if raw is None or raw.get("name", "").strip() != str(npc.get("name", "")).strip():
                # Otra versión de npcs.dat o un NPC propio del proyecto: no adivinar.
                skipped[npc.get("npcIndex")] = npc.get("name")
                continue
            values = original_values(raw)
            new = with_values(npc, values)
            if new != npc:
                for key in values:
                    if npc.get(key) != new.get(key):
                        stats[key] += 1
                before = {k: npc.get(k) for k in values}
                changed_npcs.setdefault(npc["npcIndex"], (npc.get("name"), before, values))
                data["npcs"][i] = new
                touched = True
        if touched:
            stats["mapas_con_cambios"] += 1
            pending.append((path, data))

    print(f"NPC en mapas: {stats['npc_en_mapas']} | mapas con cambios: {stats['mapas_con_cambios']}")
    print("Entradas que cambian por campo:", {k: stats[k] for k in ("defense", "preferredRange", "visionRange", "visionRangeX", "visionRangeY")})
    if skipped:
        print(f"Salteados (no están en npcs.dat o el nombre no coincide): {len(skipped)} ->",
              ", ".join(f"{k} {v}" for k, v in sorted(skipped.items())[:10]))
    notable = [(i, n, b, v) for i, (n, b, v) in changed_npcs.items()
               if b["defense"] != v["defense"] or b["preferredRange"] != v["preferredRange"]]
    print(f"NPC con DEF o rango preferido distinto: {len(notable)}")
    for i, name, before, values in sorted(notable, key=lambda r: -r[3]["defense"]):
        print(f"  {i:5} {name:<30} DEF {before['defense']} -> {values['defense']}"
              f"   rango preferido {before['preferredRange']} -> {values['preferredRange']}")
    if not args.aplicar:
        print("\nSimulación: no se escribió nada. Para aplicar: --aplicar (hace respaldo antes).")
        return 0
    backup = ROOT / "MigrationReports" / time.strftime("backup_npc_stats_%Y%m%d-%H%M%S")
    backup.mkdir(parents=True, exist_ok=False)
    for path, data in pending:
        shutil.copy2(path, backup / path.name)
        temp = path.with_name(path.name + ".writing")
        temp.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), "utf-8")
        temp.replace(path)
    print(f"\nAplicado en {len(pending)} mapas. Respaldo: {backup.relative_to(ROOT)}")
    print("Siguiente: python Tools/export_online_catalog.py")
    return 0


if __name__ == "__main__":
    sys.exit(run())
