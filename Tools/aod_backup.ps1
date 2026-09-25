# Respaldo de guardados de AoDuels (solo copia, nunca borra).
# Uso: powershell -ExecutionPolicy Bypass -File Tools/aod_backup.ps1 [-Motivo "texto"]
param([string]$Motivo = "manual")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot                     # My project (1)
$aod = Split-Path -Parent $root                               # AoDuels
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
$dst = Join-Path $aod "Respaldos\guardados-$ts"
New-Item -ItemType Directory -Force $dst | Out-Null

$local = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\My project (1)"
$server = Join-Path $aod "AO_Online\Release\Server\Saves"
$serverDemo = Join-Path $aod "AO_Online\Release\Server\SavesDemo"

if (Test-Path $local) { Copy-Item $local (Join-Path $dst "LocalLow") -Recurse }
if (Test-Path $server) { Copy-Item $server (Join-Path $dst "ServerSaves") -Recurse }
if (Test-Path $serverDemo) { Copy-Item $serverDemo (Join-Path $dst "ServerSavesDemo") -Recurse }

# PlayerPrefs del juego compilado y del editor (Unity guarda las del editor en otra clave).
$prefs = @(
    @("PlayerPrefs.reg", "HKCU\Software\DefaultCompany\My project (1)"),
    @("PlayerPrefs_Editor.reg", "HKCU\Software\Unity\UnityEditor\DefaultCompany\My project (1)")
)
foreach ($p in $prefs) {
    $out = Join-Path $dst $p[0]
    & reg export $p[1] $out /y | Out-Null
    if (-not (Test-Path $out)) { Write-Warning "No se pudo exportar $($p[1])" }
}

$files = Get-ChildItem $dst -Recurse -File
$manifest = $files | ForEach-Object {
    [pscustomobject]@{ path = $_.FullName.Substring($dst.Length + 1); bytes = $_.Length; sha256 = (Get-FileHash $_.FullName).Hash }
}
@{ creado = (Get-Date).ToString("s"); motivo = $Motivo; archivos = $manifest } |
    ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dst "manifest.json") -Encoding utf8

"RESPALDO OK: $dst ($($files.Count) archivos)"
