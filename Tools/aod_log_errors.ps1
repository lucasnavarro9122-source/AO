# Errores del Editor.log de Unity, deduplicados. Nunca vuelca el log entero.
# Uso: powershell -ExecutionPolicy Bypass -File Tools/aod_log_errors.ps1 [-DesdeUltimoPlay] [-Max 20]
param([switch]$DesdeUltimoPlay, [int]$Max = 20)

$log = Join-Path $env:LOCALAPPDATA "Unity\Editor\Editor.log"
if (-not (Test-Path $log)) { "No existe $log"; exit 1 }
$lines = Get-Content $log
$start = 0
if ($DesdeUltimoPlay) {
    $hit = $lines | Select-String "Reloading assemblies for play mode" | Select-Object -Last 1
    if ($hit) { $start = $hit.LineNumber }
}
"Editor.log: $((Get-Item $log).LastWriteTime) - desde linea $start de $($lines.Count)"
$errors = $lines[$start..($lines.Count - 1)] |
    Select-String -Pattern "error CS\d+|Exception|Compilation failed|NullReference|Assertion failed" |
    ForEach-Object { $_.Line.Trim() } |
    Group-Object | Sort-Object Count -Descending | Select-Object -First $Max
if (-not $errors) { "SIN ERRORES"; exit 0 }
$errors | ForEach-Object { "[x$($_.Count)] " + $_.Name.Substring(0, [Math]::Min(240, $_.Name.Length)) }
exit 2
