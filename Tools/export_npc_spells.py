"""Hechizos de NPC del original -> Assets/Resources/AOMigrator/MagicV129/npc_spells.json.

Lee npcs.dat como el servidor original (map_migration.npc_dat): LanzaSpells, Sp1..SpN, IntervaloLanzarHechizo
(8000 ms si falta), MagicBonus, DontHitVisiblePlayers, Movement y, para la IA de apoyo (Movement 11/13), RangoSpell,
Cd1..CdN, RestriccionDeAyuda y RestriccionDeAtaque; CantidadInvocaciones para los que invocan.
"summoned": la ficha completa (formato de los mapas, demo_map_builder.npc_entry) de cada criatura que se puede invocar.
Solo datos: no cambia stats.
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
import demo_map_builder as builder  # noqa: E402

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
                     "movement": number(raw, "movement"), "rangeSpell": number(raw, "rangospell"),
                     "cooldowns": [number(raw, f"cd{i}") for i in range(1, slots + 1)],
                     "help": number(raw, "restricciondeayuda"), "attack": number(raw, "restricciondeataque"),
                     "summonLimit": number(raw, "cantidadinvocaciones")})
    if missing:
        raise SystemExit(f"hechizos de NPC que no están en spells.json: {sorted(missing)}")
    # Creatures an NPC can summon (Invoca = 1: spells with summonNpc), as full map-format NPC entries.
    by_id = {s["id"]: s for s in json.loads(SPELLS.read_text("utf-8-sig"))["spells"]}
    wanted = sorted({by_id[s]["summonNpc"] for r in rows for s in r["spells"] if s and by_id[s].get("summonNpc", 0) > 0})
    sources, problems, summoned = builder.Sources(), [], []
    bodies, heads = sources.visuals
    for index in wanted:
        entry = builder.npc_entry(index, 0, 0, {}, sources, bodies, heads, problems)
        # Summons never respawn; vision 15 x 13 like every NPC of the original (AI_NPC.bas:34-35).
        entry.update(hostile=True, attackable=True, respawnMinSeconds=0, respawnMaxSeconds=0,
                     visionRange=15, visionRangeX=15, visionRangeY=13)
        summoned.append(entry)
    if problems:
        raise SystemExit("; ".join(problems))
    return {"version": "npc-spells-2", "fuente": "npcs.dat (ao-org Recursos); reglas: docs/claude/contenido/npc_hechizos_original.md",
            "npcs": rows, "summoned": summoned}


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
    data = json.loads(text)
    print(f"{OUT.relative_to(ROOT)}: {len(data['npcs'])} NPC que lanzan hechizos, {len(data['summoned'])} criaturas invocables")
    return 0


if __name__ == "__main__":
    sys.exit(main())
