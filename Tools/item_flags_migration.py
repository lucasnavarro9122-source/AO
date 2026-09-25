"""Completa en items.json datos de obj.dat que la migracion original no traia.

- noSeCae / intirable: flags de ItemSeCae (regla de muerte).
- munition: clave `Municiones` de obj.dat (el tipo de flecha que usa el arma;
  0 = dispara sin municion). Estaba en 0 en todos los arcos.

El servidor original lee obj.dat sin distinguir mayusculas (clsIniManager
usa UCase), asi que NoSeCae, NoseCae y Nosecae son la misma clave.
`Destruye` ya esta como `destroyOnSell` e `Instransferible` como
`untransferable` (verificado: mismos objetos).

Sin argumentos simula y reporta; con --apply escribe items.json.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OBJ = ROOT / "Archivos Originales/Recursos-master/Recursos-master/Dat/obj.dat"
ITEMS = ROOT / "Assets/Resources/AOMigrator/ItemsV10/items.json"
FLAGS = {"nosecae": "noSeCae", "intirable": "intirable"}
NUMBERS = {"municiones": "munition"}


def serialize(data: dict) -> bytes:
    return json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def obj_flags() -> tuple[dict[int, dict[str, bool]], list[str]]:
    result: dict[int, dict[str, bool]] = {}
    conflicts = []
    current = None
    for line in OBJ.read_text("cp1252").splitlines():
        match = re.match(r"\s*\[OBJ(\d+)\]", line, re.IGNORECASE)
        if match:
            current = result.setdefault(int(match.group(1)), {})
            continue
        if current is None or "=" not in line or line.lstrip().startswith("'"):
            continue
        key, value = line.split("=", 1)
        key = key.strip().lower()
        if key in NUMBERS:
            try:
                current.setdefault(NUMBERS[key], int(float(value.split("'", 1)[0].strip() or 0)))
            except ValueError:
                pass
            continue
        if key not in FLAGS:
            continue
        try:
            flag = int(float(value.split("'", 1)[0].strip() or 0)) != 0
        except ValueError:
            flag = False
        name = FLAGS[key]
        if name in current and current[name] != flag:
            conflicts.append(f"OBJ con {name} repetido y distinto")
            continue  # como FindKey: vale la primera aparicion
        current.setdefault(name, flag)
    return result, conflicts


def with_flags(item: dict, flags: dict[str, bool]) -> dict:
    """Inserta los flags despues de destroyOnSell (o al final)."""
    out = {}
    for key, value in item.items():
        if key in FLAGS.values():
            continue
        out[key] = flags.get(key, 0) if key in NUMBERS.values() else value
        if key == "destroyOnSell":
            for name in FLAGS.values():
                out[name] = flags.get(name, False)
    for name in FLAGS.values():
        out.setdefault(name, flags.get(name, False))
    return out


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    raw = ITEMS.read_bytes()
    data = json.loads(raw)
    if serialize(data) != raw:
        raise SystemExit("items.json no tiene el formato esperado: no se toca")
    flags, conflicts = obj_flags()
    data["items"] = [with_flags(item, flags.get(item["index"], {}))
                     for item in data["items"]]
    counts = {name: sum(1 for item in data["items"] if item[name])
              for name in (*FLAGS.values(), *NUMBERS.values())}
    if args.apply:
        temp = ITEMS.with_name(ITEMS.name + ".writing")
        temp.write_bytes(serialize(data))
        temp.replace(ITEMS)
    print(json.dumps({"applied": args.apply, "items": len(data["items"]),
                      "true": counts, "conflicts": conflicts},
                     ensure_ascii=False))


if __name__ == "__main__":
    main()
