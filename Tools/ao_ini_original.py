"""Lee los .dat de Argentum Online (npcs.dat, obj.dat, Hechizos.dat) exactamente como el servidor original.

El servidor usa clsIniManager (argentum-online-server-master/Codigo/clsIniManager.cls):
  - guarda cada línea "clave=valor" de la sección, en orden, con la clave en mayúsculas (sin recortar espacios);
  - ordena las claves con un quicksort de pivote al medio (SortChildNodes);
  - GetValue busca con búsqueda binaria (FindKey).
Con claves repetidas en una sección, el valor que lee NO es siempre el primero ni el último: depende del orden.
Los importadores que tomaban "el último" (map_migration.npc_dat) o "el primero" leían otro valor en algunos casos.

Uso (desde la raíz del proyecto):
  python Tools/ao_ini_original.py diferencias            claves repetidas donde el original no coincide con primero/último
  python Tools/ao_ini_original.py impacto                campos de nuestros JSON que hoy no tienen el valor original
  python Tools/ao_ini_original.py aplicar --confirmar    corrige solo esos campos (requiere el candado de Unity de Programación)

Como librería: `read_numbered(path, "NPC")` -> {índice: {clave_minúscula: valor_sin_comentario}}, igual formato que map_migration.npc_dat.
"""
from __future__ import annotations

import argparse
import glob
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DAT = ROOT / "Archivos Originales/Recursos-master/Recursos-master/Dat"
RES = ROOT / "Assets/Resources/AOMigrator"
ITEMS = RES / "ItemsV10/items.json"
NPC_LOOT = RES / "LootV180/npc_loot.json"
SPELLS = [RES / "MagicV129/spells.json", RES / "MagicV120/spells.json"]
MAPS = RES / "WorldV07/Maps"


# ---------------------------------------------------------------- clsIniManager

def read_sections(path: Path) -> dict[str, list[list[str]]]:
    """Secciones en orden de archivo: {NOMBRE: [[CLAVE, valor], ...]} (Initialize de clsIniManager)."""
    sections: dict[str, list[list[str]]] = {}
    current = None
    for raw in path.read_text("cp1252").split("\n"):
        text = raw.rstrip("\r")
        if not text:
            continue
        if text[0] == "[":
            pos = text.find("]", 1)
            if pos > 0:
                current = text[1:pos].strip().upper()
                sections.setdefault(current, [])
            continue
        pos = text.find("=", 1)          # InStr(2, Text, "=")
        if pos > 0 and current is not None:
            sections[current].append([text[:pos].upper(), text[pos + 1:]])
    return sections


def _sort(values: list[list[str]], first: int, last: int) -> None:
    """SortChildNodes: quicksort de pivote al medio, comparación binaria de strings."""
    lo, hi = first, last
    pivot = values[(lo + hi) // 2][0]
    while lo <= hi:
        while values[lo][0] < pivot and lo < last:
            lo += 1
        while values[hi][0] > pivot and hi > first:
            hi -= 1
        if lo <= hi:
            values[lo], values[hi] = values[hi], values[lo]
            lo += 1
            hi -= 1
    if first < hi:
        _sort(values, first, hi)
    if lo < last:
        _sort(values, lo, last)


def _find(values: list[list[str]], key: str) -> str | None:
    """FindKey: búsqueda binaria sobre las claves ordenadas."""
    lo, hi = 0, len(values) - 1
    while lo <= hi:
        mid = (lo + hi) // 2
        if values[mid][0] < key:
            lo = mid + 1
        elif values[mid][0] > key:
            hi = mid - 1
        else:
            return values[mid][1]
    return None


def resolve(entries: list[list[str]]) -> dict[str, str]:
    """Lo que devuelve GetValue para cada clave de la sección."""
    ordered = [list(e) for e in entries]
    if ordered:
        _sort(ordered, 0, len(ordered) - 1)
    return {key: _find(ordered, key) for key in {k for k, _ in entries}}


def clean(value: str | None) -> str:
    """Como npc_dat: sin el comentario ('...) ni espacios. Val() de VB6 toma el número inicial."""
    return "" if value is None else value.split("'", 1)[0].strip()


def read_numbered(path: Path, prefix: str) -> dict[int, dict[str, str]]:
    """{índice: {clave_minúscula: valor}} para secciones [PREFIXnnn], leídas como el servidor original."""
    result = {}
    for name, entries in read_sections(path).items():
        if name.startswith(prefix) and name[len(prefix):].isdigit():
            result[int(name[len(prefix):])] = {k.lower(): clean(v) for k, v in resolve(entries).items()}
    return result


def duplicates(path: Path):
    """(sección, clave, original, primero, último) de cada clave repetida."""
    for name, entries in read_sections(path).items():
        keys = [k for k, _ in entries]
        repeated = {k for k in keys if keys.count(k) > 1}
        if not repeated:
            continue
        original = resolve(entries)
        for key in sorted(repeated):
            values = [clean(v) for k, v in entries if k == key]
            yield name, key, clean(original[key]), values[0], values[-1]


# ---------------------------------------------------------------- impacto en nuestros datos

def as_int(value: str) -> int:
    digits = ""
    for ch in value.strip():
        if ch.isdigit() or (ch == "-" and not digits):
            digits += ch
        else:
            break
    try:
        return int(digits)
    except ValueError:
        return 0


# (archivo .dat, prefijo, CLAVE) -> [(json, lista, clave_de_índice, campo, conversión)]
FIELD_MAP = {
    ("npcs.dat", "NPC", "GIVEGLD"): [("loot", "giveGold", as_int), ("maps", "giveGold", as_int)],
    ("npcs.dat", "NPC", "GIVEEXP"): [("loot", "giveExp", as_int), ("maps", "giveExp", as_int)],
    ("npcs.dat", "NPC", "ATTACKABLE"): [("loot", "attackable", lambda v: as_int(v) != 0), ("maps", "attackable", lambda v: as_int(v) != 0)],
    ("npcs.dat", "NPC", "INTERVALORESPAWN"): [("loot", "respawnMaxSeconds", as_int)],
    ("npcs.dat", "NPC", "INTERVALORESPAWNMIN"): [("loot", "respawnMinSeconds", as_int)],
    ("npcs.dat", "NPC", "NPCLVL"): [("loot", "level", as_int)],
    ("npcs.dat", "NPC", "PODEREVASION"): [("maps", "evasionPower", as_int)],
    ("npcs.dat", "NPC", "PODERATAQUE"): [("maps", "attackPower", as_int)],
    ("npcs.dat", "NPC", "MAXHP"): [("maps", "maxHp", as_int)],
    ("npcs.dat", "NPC", "DEF"): [("maps", "defense", as_int)],
    ("obj.dat", "OBJ", "VALOR"): [("items", "value", as_int)],
    ("obj.dat", "OBJ", "MINELV"): [("items", "minLevel", as_int)],
    ("obj.dat", "OBJ", "NAME"): [("items", "name", str)],
    ("obj.dat", "OBJ", "GRHINDEX"): [("items", "grhIndex", as_int)],
    ("obj.dat", "OBJ", "MINDEF"): [("items", "minDef", as_int)],
    ("obj.dat", "OBJ", "MAXDEF"): [("items", "maxDef", as_int)],
    ("obj.dat", "OBJ", "ANIM"): [("items", "anim", as_int)],
    ("obj.dat", "OBJ", "CRUCIAL"): [("items", "crucial", lambda v: as_int(v) != 0)],
    ("obj.dat", "OBJ", "TEXTO"): [("items", "description", str)],
    ("hechizos.dat", "HECHIZO", "EOTID"): [("spells", "eotId", as_int)],
}

# Claves repetidas que no usa ninguno de nuestros JSON (se informan, no se tocan).
NOT_USED_NOTE = "sin campo en nuestros JSON"


def load_targets():
    targets = {"items": [], "loot": [], "spells": [], "maps": []}
    if ITEMS.exists():
        targets["items"].append((ITEMS, json.loads(ITEMS.read_text("utf-8")), "items", "index"))
    if NPC_LOOT.exists():
        targets["loot"].append((NPC_LOOT, json.loads(NPC_LOOT.read_text("utf-8")), "npcs", "npcIndex"))
    for path in SPELLS:
        if path.exists():
            targets["spells"].append((path, json.loads(path.read_text("utf-8")), "spells", "id"))
    return targets


def map_files():
    return sorted(Path(p) for p in glob.glob(str(MAPS / "map_*.json")))


def impact():
    """[(archivo_json, índice, campo, actual, original, dat, sección, clave)] donde el JSON no tiene el valor original."""
    changes = []
    targets = load_targets()
    wanted_maps = []
    for dat_name, prefix in (("npcs.dat", "NPC"), ("obj.dat", "OBJ"), ("Hechizos.dat", "HECHIZO")):
        path = DAT / dat_name
        if not path.exists():
            continue
        for section, key, original, first, last in duplicates(path):
            if not section.startswith(prefix) or not section[len(prefix):].isdigit():
                continue
            index = int(section[len(prefix):])
            for target, field, convert in FIELD_MAP.get((dat_name.lower(), prefix, key), []):
                expected = convert(original)
                if target == "maps":
                    wanted_maps.append((index, field, expected, dat_name, section, key))
                    continue
                for jpath, data, list_key, id_key in targets[target]:
                    for entry in data.get(list_key, []):
                        if entry.get(id_key) == index and field in entry and entry[field] != expected:
                            changes.append((jpath, index, field, entry[field], expected, dat_name, section, key))
    if wanted_maps:
        for mpath in map_files():
            data = json.loads(mpath.read_text("utf-8"))
            for npc in data.get("npcs", []):
                for index, field, expected, dat_name, section, key in wanted_maps:
                    if npc.get("npcIndex") == index and field in npc and npc[field] != expected:
                        changes.append((mpath, index, field, npc[field], expected, dat_name, section, key))
    return changes


def icon_for(grh: int, current: dict) -> dict:
    """First frame of the GRH (graficos.ini), like the item importer builds `icon`."""
    import map_migration as mm
    frames, _ = mm.resolve_grh(grh, mm.graphics_index(DAT.parent / "init/graficos.ini"))
    return frames[0] if frames else current


def dump_compact(data) -> bytes:
    return json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def lock_is_mine() -> bool:
    try:
        out = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                              str(ROOT / "Tools/aod_unity_lock.ps1"), "status"], capture_output=True, text=True, timeout=60)
    except (OSError, subprocess.TimeoutExpired):
        return False
    return "Programación" in (out.stdout or "") or "Programacion" in (out.stdout or "")


def apply(changes) -> int:
    by_file: dict[Path, list] = {}
    for change in changes:
        by_file.setdefault(change[0], []).append(change)
    for path, file_changes in by_file.items():
        raw = path.read_bytes()
        data = json.loads(raw.decode("utf-8"))
        if dump_compact(data) != raw:
            print(f"NO SE ESCRIBE {path.relative_to(ROOT)}: volver a guardarlo cambiaría el formato del archivo")
            return 1
        list_key, id_key = ("npcs", "npcIndex") if path.parent == MAPS or path == NPC_LOOT else \
                           ("items", "index") if path == ITEMS else ("spells", "id")
        for _, index, field, _, expected, *_ in file_changes:
            for entry in data.get(list_key, []):
                if entry.get(id_key) == index and field in entry:
                    entry[field] = expected
                    if field == "grhIndex" and "icon" in entry:
                        entry["icon"] = icon_for(expected, entry["icon"])
        temp = path.with_name(path.name + ".writing")
        temp.write_bytes(dump_compact(data))
        temp.replace(path)
        print(f"escrito {path.relative_to(ROOT)} ({len(file_changes)} campo(s))")
    return 0


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("modo", choices=["diferencias", "impacto", "aplicar"])
    parser.add_argument("--confirmar", action="store_true")
    args = parser.parse_args()

    if args.modo == "diferencias":
        for dat_name in ("npcs.dat", "obj.dat", "Hechizos.dat"):
            path = DAT / dat_name
            rows = [r for r in duplicates(path) if r[2] != r[4] or r[2] != r[3]] if path.exists() else []
            print(f"== {dat_name}: {len(rows)} claves repetidas donde el original no es el primero o no es el último")
            for section, key, original, first, last in rows:
                used = "usado" if any(k[0] == dat_name.lower() and k[2] == key for k in FIELD_MAP) else NOT_USED_NOTE
                print(f"  {section:<12} {key:<18} original={original[:40]!r} primero={first[:25]!r} último={last[:25]!r} ({used})")
        return 0

    changes = impact()
    print(f"{len(changes)} campo(s) de nuestros JSON no tienen el valor que lee el servidor original:")
    for path, index, field, current, expected, dat_name, section, key in changes:
        print(f"  {path.relative_to(ROOT)}  #{index} {field}: {current!r} -> {expected!r}   ({dat_name} [{section}] {key})")
    if args.modo == "impacto":
        return 0
    if not args.confirmar:
        print("Simulación: agregá --confirmar para escribir.")
        return 0
    if not lock_is_mine():
        print("No se escribe: el candado de Unity no es de Programación (Tools/aod_unity_lock.ps1 status).")
        return 1
    return apply(changes)


if __name__ == "__main__":
    sys.exit(main())
