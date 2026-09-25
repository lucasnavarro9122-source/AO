"""Genera tuning_data.js para el Spell Tester v1.3.

El tester se abre con file:// y el navegador no deja hacer fetch() de JSON,
por eso los valores de spell_visual_tuning.json se embeben en un .js.

Uso (desde la raíz del repo o desde esta carpeta):
    python Tools/SpellTester/generar_tuning_data.py

Solo lee archivos; escribe únicamente Tools/SpellTester/tuning_data.js.
De skillshot_tuning.json se copia SOLO visualScale (Arte). speed, range y
hitRadius son gameplay y no se incluyen.
"""
import datetime
import hashlib
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OVR = os.path.join(REPO, "Assets", "StreamingAssets", "AOMigrator", "SpellOverrides")
VISUAL = os.path.join(OVR, "spell_visual_tuning.json")
SKILLSHOT = os.path.join(OVR, "skillshot_tuning.json")
OUT = os.path.join(HERE, "tuning_data.js")


def rel(path):
    return os.path.relpath(path, REPO).replace(os.sep, "/")


def main():
    raw = open(VISUAL, "rb").read()
    text = raw.decode("utf-8-sig")
    data = json.loads(text)
    entries = data.get("entries") or []

    float_fields, int_fields = [], []
    for entry in entries:
        for key, value in entry.items():
            if isinstance(value, bool):
                continue
            if isinstance(value, float) and key not in float_fields:
                float_fields.append(key)
            elif isinstance(value, int) and key not in int_fields:
                int_fields.append(key)
    int_fields = [k for k in int_fields if k not in float_fields]

    indent = 2
    for line in text.splitlines()[1:]:
        stripped = line.lstrip(" ")
        if stripped:
            indent = len(line) - len(stripped) or 2
            break

    meta = {
        "source": rel(VISUAL),
        "generated": datetime.date.today().isoformat(),
        "sha1": hashlib.sha1(raw).hexdigest(),
        "entries": len(entries),
        "floatFields": float_fields,
        "intFields": int_fields,
        "indent": indent,
        "eol": "\r\n" if b"\r\n" in raw else "\n",
        "trailingNewline": raw.endswith(b"\n"),
    }

    skill = {"source": rel(SKILLSHOT), "note": "solo visualScale (Arte); speed/range/hitRadius son gameplay y no se incluyen", "entries": {}}
    if os.path.exists(SKILLSHOT):
        sdata = json.loads(open(SKILLSHOT, "rb").read().decode("utf-8-sig"))
        skill["version"] = sdata.get("version", "")
        for e in sdata.get("entries") or []:
            if "spellId" in e:
                skill["entries"][str(e["spellId"])] = e.get("visualScale", 1.0)

    js = [
        "// GENERADO por generar_tuning_data.py - no editar a mano.",
        "// Fuente: " + meta["source"] + " (" + meta["generated"] + ")",
        "window.AO_VISUAL_TUNING = " + json.dumps(data, ensure_ascii=False, indent=2) + ";",
        "window.AO_VISUAL_TUNING_META = " + json.dumps(meta, ensure_ascii=False) + ";",
        "window.AO_SKILLSHOT_VISUAL = " + json.dumps(skill, ensure_ascii=False) + ";",
        "",
    ]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(js))
    print("tuning_data.js:", len(entries), "entradas, version", data.get("version"), "->", rel(OUT))


if __name__ == "__main__":
    main()
