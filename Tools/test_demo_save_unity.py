"""Offline demo save isolation (Unity side: Assets/AOMigrator/Editor/AODemoSaveQA289.cs).

Unlike the other Unity QA runners, this test lets the game SAVE FOR REAL, because that is what it checks:
a demo character must be written only to LocalLow/.../AO_BattleDemo/, never to the normal saves.
Safety: it refuses to run if AO_BattleDemo/ already exists (never overwrite a real demo character); it copies
and hashes every save first; afterwards the normal saves must be byte-identical and new files may only appear
in AO_BattleDemo/. It NEVER restores anything by itself: on a failure it prints the backup path so the saves
can be restored with Lucas's OK. When it passes, the AO_BattleDemo/ folder it created is MOVED (not deleted)
into the backup folder, leaving LocalLow as it was."""
import hashlib
import json
import os
import shutil
import time
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = Path(os.environ["USERPROFILE"]) / "AppData/LocalLow/DefaultCompany/My project (1)"
demo = source / "AO_BattleDemo"
if demo.exists():
    raise SystemExit(f"{demo} ya existe: no corro la prueba para no pisar una partida de demo real.")

backup = root / "Temp" / f"demo-save-qa-{time.time_ns()}"
backup.mkdir(parents=True)
hashes = {}
for path in source.rglob("*"):
    if path.is_file() and "Unity" not in path.relative_to(source).parts:
        relative = path.relative_to(source)
        target = backup / "LocalLow" / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
        hashes[str(relative)] = hashlib.sha256(path.read_bytes()).hexdigest()
(backup / "manifest.json").write_text(json.dumps(hashes, indent=2), encoding="utf-8")
print(f"Copia previa: {backup} ({len(hashes)} archivos)", flush=True)

# Never ask an old assembly to execute a just-edited test.
sources = list((root / "Assets/AOMigrator").rglob("*.cs"))
newest = (max(p.stat().st_mtime for p in sources if "Editor" not in p.parts),
          max(p.stat().st_mtime for p in sources if "Editor" in p.parts))
assemblies = [root / "Library/ScriptAssemblies" / n for n in ("Assembly-CSharp.dll", "Assembly-CSharp-Editor.dll")]
(root / "Temp/refresh_online_client").write_text("", encoding="utf-8")
deadline = time.monotonic() + 300
print("Waiting for Unity import; leave the editor open outside Play.", flush=True)
while not all(a.exists() and a.stat().st_mtime >= n for a, n in zip(assemblies, newest)):
    if time.monotonic() > deadline:
        raise TimeoutError("Unity has not imported the latest scripts")
    time.sleep(1)
time.sleep(4)

report = root / "MigrationReports/demo_save_v289.json"
if report.exists():
    report.replace(backup / "previous-result.json")
flag = root / "Temp/run_demo_save_qa"
flag.write_text("", encoding="utf-8")
print("Play test requested (saves for real, demo only).", flush=True)
problems = []
try:
    deadline = time.monotonic() + 300
    while not report.exists():
        if time.monotonic() > deadline:
            raise TimeoutError("Unity demo save QA did not finish")
        time.sleep(1)
    time.sleep(2)  # let Play stop (the editor may still write on exit)
    result = json.loads(report.read_text(encoding="utf-8"))
    print(json.dumps(result, ensure_ascii=True, indent=1), flush=True)
    if not result["passed"]:
        problems.append("Unity: " + result["message"])
finally:
    if flag.exists():
        flag.unlink()
    now = {str(p.relative_to(source)): hashlib.sha256(p.read_bytes()).hexdigest()
           for p in source.rglob("*") if p.is_file() and "Unity" not in p.relative_to(source).parts}
    changed = [n for n, h in hashes.items() if now.get(n) != h]
    added = [n for n in now if n not in hashes]
    outside = [n for n in added if not n.startswith("AO_BattleDemo")]
    if changed:
        problems.append(f"GUARDADOS NORMALES CAMBIADOS: {changed}")
    if outside:
        problems.append(f"archivos nuevos fuera de AO_BattleDemo/: {outside}")
    slot = demo / "save_slot_1.json"
    if not slot.exists():
        problems.append("no apareció AO_BattleDemo/save_slot_1.json")
    elif json.loads(slot.read_text(encoding="utf-8-sig")).get("character", {}).get("name") != "DemoPrueba":
        problems.append("AO_BattleDemo/save_slot_1.json no tiene al personaje DemoPrueba")
    print(f"Nuevos en AO_BattleDemo/: {[n for n in added if n.startswith('AO_BattleDemo')]}", flush=True)
    if problems:
        print("FALLA. No se restauró nada automáticamente. Copia previa para restaurar con OK de Lucas: " + str(backup), flush=True)
        raise AssertionError("; ".join(problems))
    shutil.move(str(demo), str(backup / "AO_BattleDemo_creado_por_la_prueba"))
    print(f"PASS: {len(hashes)} archivos previos intactos; la demo guardó solo en AO_BattleDemo/ "
          f"(movido a {backup / 'AO_BattleDemo_creado_por_la_prueba'}).", flush=True)
