"""Agrega a items.json dos datos de obj.dat que faltaban para la pérdida al morir: noDrop (NoSeCae) y cantThrow (Intirable).

Los otros que usa la regla ya existen: untransferable (Instransferible), destroyOnSell (Destruye), newbie y objType.
Por defecto simula; con --aplicar reescribe items.json con el mismo formato compacto.
Después: python Tools/export_online_catalog.py (el servidor toma los mismos datos).
"""
import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ITEMS = ROOT / "Assets/Resources/AOMigrator/ItemsV10/items.json"
DAT = ROOT / "Archivos Originales/Recursos-master/Recursos-master/Dat/obj.dat"
FLAGS = (("noDrop", "noseCae"), ("cantThrow", "intirable"))


def obj_dat(path: Path) -> dict[int, dict[str, str]]:
    result, current = {}, None
    for line in path.read_text("cp1252").splitlines():
        match = re.match(r"\s*\[OBJ(\d+)\]", line, re.IGNORECASE)
        if match:
            current = result.setdefault(int(match.group(1)), {})
        elif current is not None and "=" in line and not line.lstrip().startswith("'"):
            key, value = line.split("=", 1)
            current[key.strip().lower()] = value.split("'", 1)[0].strip()
    return result


def run() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--dat", type=Path, default=DAT, help="obj.dat original (cp1252)")
    parser.add_argument("--aplicar", action="store_true", help="escribir items.json (sin esto, solo simula)")
    args = parser.parse_args()
    if not args.dat.is_file():
        print(f"No existe {args.dat}. Pasá la ruta con --dat.")
        return 2
    raw = ITEMS.read_bytes()
    data = json.loads(raw.decode("utf-8-sig"))
    if json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8") != raw:
        print("items.json no tiene el formato compacto esperado; no se toca.")
        return 2
    objs = obj_dat(args.dat)
    counts, mismatched = {name: 0 for name, _ in FLAGS}, []
    for i, item in enumerate(data["items"]):
        src = objs.get(item["index"])
        values = {name: False for name, _ in FLAGS}
        src_name = (src or {}).get("name", "").strip()
        if src_name and src_name != str(item.get("name", "")).strip():
            # Otra versión de obj.dat: no adivinar (las entradas vacías, sin nombre, no cuentan).
            mismatched.append((item["index"], item.get("name"), src_name))
        elif src is not None:
            for name, key in FLAGS:
                values[name] = src.get(key.lower(), "0").strip() == "1"
        for name in values:
            counts[name] += values[name]
        # Claves nuevas al final de cada objeto: no cambia el orden de las existentes.
        data["items"][i] = {**{k: v for k, v in item.items() if k not in values}, **values}
    print(f"Objetos: {len(data['items'])} | noDrop (NoSeCae): {counts['noDrop']} | cantThrow (Intirable): {counts['cantThrow']}")
    if mismatched:
        print(f"AVISO: {len(mismatched)} objetos con otro nombre en obj.dat (quedan en false):",
              ", ".join(f"{i} {a!r}/{b!r}" for i, a, b in mismatched[:8]))
    if not args.aplicar:
        print("Simulación: no se escribió nada. Para aplicar: --aplicar")
        return 0
    temp = ITEMS.with_name(ITEMS.name + ".writing")
    temp.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), "utf-8")
    temp.replace(ITEMS)
    print("items.json actualizado. Siguiente: python Tools/export_online_catalog.py")
    return 0


if __name__ == "__main__":
    sys.exit(run())
