"""Package the current Windows player for friends without Unity debug data."""

from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


project = Path(__file__).resolve().parents[1]
release = project.parent / "AO_Online"
client = release / "Release" / "Client"
output = release / "Cliente-para-amigos.zip"
temporary = release / "Cliente-para-amigos.zip.tmp"

if not (client / "ArgentumOnline.exe").is_file():
    raise SystemExit("Falta Release/Client/ArgentumOnline.exe; compilá Unity primero.")

with ZipFile(temporary, "w", compression=ZIP_DEFLATED,
             compresslevel=1, allowZip64=True) as archive:
    for path in client.rglob("*"):
        if path.is_file() and "DoNotShip" not in str(path):
            archive.write(path, path.relative_to(client))

with ZipFile(temporary) as archive:
    if archive.testzip() is not None:
        raise SystemExit("El ZIP no pasó la verificación CRC.")

temporary.replace(output)
print(f"Listo: {output} ({output.stat().st_size / 1_000_000:.1f} MB)")
