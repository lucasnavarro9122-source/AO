"""Run opt-in QA for the duel UI V272 and hotbar V261 in an open Unity editor, preserving local saves."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import time

root = Path(__file__).resolve().parents[1]
source = Path(os.environ["USERPROFILE"]) / "AppData/LocalLow/DefaultCompany/My project (1)"
backup = root / "Temp" / f"interface-qa-saves-{time.time_ns()}"
backup.mkdir(parents=True)
hashes = {}
for path in source.rglob("*.json"):
    relative = path.relative_to(source)
    target = backup / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, target)
    hashes[str(relative)] = hashlib.sha256(path.read_bytes()).hexdigest()
(backup / "manifest.json").write_text(json.dumps(hashes, indent=2), encoding="utf-8")

# Never ask an old assembly to execute a just-edited test.
sources = list((root / "Assets/AOMigrator").rglob("*.cs"))
newest_runtime = max(path.stat().st_mtime for path in sources if "Editor" not in path.parts)
newest_editor = max(path.stat().st_mtime for path in sources if "Editor" in path.parts)
assemblies = [root / "Library/ScriptAssemblies" / name for name in
              ("Assembly-CSharp.dll", "Assembly-CSharp-Editor.dll")]
(root / "Temp/refresh_online_client").write_text("", encoding="utf-8")
deadline = time.monotonic() + 180
print("Waiting for Unity import; leave the editor open outside Play.", flush=True)
while not all(path.exists() and path.stat().st_mtime >= newest for path, newest in zip(assemblies, (newest_runtime, newest_editor))):
    if time.monotonic() > deadline:
        raise TimeoutError("Unity has not imported the latest scripts")
    time.sleep(1)
time.sleep(4)

report = root / "MigrationReports/interface_v274.json"
if report.exists():
    report.replace(backup / "previous-result.json")
flag = root / "Temp/run_interface_qa"
(root / "Temp/stop_interface_qa").unlink(missing_ok=True)
flag.write_text("", encoding="utf-8")
print("Protected Play test requested.", flush=True)
deadline = time.monotonic() + 360
try:
    while not report.exists():
        if time.monotonic() > deadline:
            raise TimeoutError("Unity interface QA did not finish")
        time.sleep(1)
    time.sleep(1)
    result = json.loads(report.read_text(encoding="utf-8"))
    print(json.dumps(result, ensure_ascii=True, indent=1), flush=True)
    if not result["passed"]:
        raise AssertionError(f"Unity interface QA failed: {len(result['failures'])} failures, {len(result['errors'])} log errors")
finally:
    if flag.exists():
        flag.unlink()
    changed = [name for name, digest in hashes.items()
               if not (source / name).exists()
               or hashlib.sha256((source / name).read_bytes()).hexdigest() != digest]
    if changed:
        raise AssertionError(f"Local saves changed: {changed}; preserved backup: {backup}")
    print(f"PASS: {len(hashes)} original save files unchanged.", flush=True)
