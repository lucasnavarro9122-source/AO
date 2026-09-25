"""Hechizos de NPC del original -> Assets/Resources/AOMigrator/MagicV129/npc_spells.json.

Lee npcs.dat como el servidor original (map_migration.npc_dat): LanzaSpells, Sp1..SpN, IntervaloLanzarHechizo
(8000 ms si falta), MagicBonus, DontHitVisiblePlayers y Movement. Solo datos: no cambia stats.
Lo usan el juego sin conexión (AONPCSpellCasterV902) y el servidor (catálogo "npcSpells", export_online_catalog.py).
Reglas y fuentes: docs/claude/contenido/npc_hechizos_original.md.

  python Tools/export_npc_spells.py            escribe el JSON
  python Tools/export_npc_spells.py --check    compara con lo que hay en disco (no escribe)
"""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools"))
import map_migration as mm  # noqa: E402

NPCS_DAT = ROOT / "Archivos Originales/Recursos-master/Recursos-master/Dat/npcs.dat"
SPELLS = ROOT / "Assets/Resources/AOMigrator/MagicV129/spells.json"
OUT = ROOT / "Assets/Resources/AOMigrator/MagicV129/npc_spells.json"


def number(raw, key, default=0):
    try:
        return int(float(raw.get(key, default) or default))
    except ValueError:
        return default


def build():
    npcs = mm.npc_dat(NPCS_DAT)
    known = {s["id"] for s in json.loads(SPELLS.read_text("utf-8-sig"))["spells"]}
    rows, missing = [], set()
    for index in sorted(npcs):
        raw = npcs[index]
        slots = number(raw, "lanzaspells")
        if slots <= 0:
            continue
        # Sp1..SpN in slot order: repeated ids weigh more in the draw; 0 (empty slot) is kept, as in the original.
        spells = [number(raw, f"sp{i}") for i in range(1, slots + 1)]
        missing |= {s for s in spells if s and s not in known}
        try:
            bonus = float(raw.get("magicbonus", 0) or 0)
        except ValueError:
            bonus = 0.0
        rows.append({"npcIndex": index, "spells": spells, "castIntervalMs": number(raw, "intervalolanzarhechizo"),
                     "magicBonus": bonus, "dontHitVisiblePlayers": number(raw, "donthitvisibleplayers") != 0,
                     "movement": number(raw, "movement")})
    if missing:
        raise SystemExit(f"hechizos de NPC que no están en spells.json: {sorted(missing)}")
    return {"version": "npc-spells-1", "fuente": "npcs.dat (ao-org Recursos); reglas: docs/claude/contenido/npc_hechizos_original.md",
            "npcs": rows}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    text = json.dumps(build(), ensure_ascii=False, separators=(",", ":")) + "\n"
    if args.check:
        same = OUT.exists() and OUT.read_text("utf-8") == text
        print("CHECK:", "igual" if same else "DIFIERE")
        return 0 if same else 1
    OUT.write_text(text, "utf-8")
    print(f"{OUT.relative_to(ROOT)}: {text.count('npcIndex')} NPC que lanzan hechizos")
    return 0


if __name__ == "__main__":
    sys.exit(main())
