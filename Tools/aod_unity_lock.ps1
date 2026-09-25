# Candado de Unity (atómico). Es la única fuente de verdad sobre quién usa Unity.
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/aod_unity_lock.ps1 status
#   ... take    -Sector "Programación" -Motivo "PvP en el ring"   (exit 0 = tomado, exit 1 = ocupado)
#   ... release -Sector "Programación"                            (solo lo libera quien lo tiene)
#   ... release -Sector "Cerebro" -Force                          (solo Cerebro, para un candado abandonado)
# Regla: si "take" devuelve exit 1, NO se toca Assets/ ni se abre Play. Se espera o se avisa a Cerebro.
param(
    [Parameter(Position = 0)][ValidateSet("status", "take", "release")][string]$Accion = "status",
    [string]$Sector = "",
    [string]$Motivo = "",
    [switch]$Force
)

$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root ".aod"
$lock = Join-Path $dir "unity.lock"
New-Item -ItemType Directory -Force $dir | Out-Null

# Clave de comparación: solo a-z y 0-9. Así "Animación", "Animacion" y "AnimaciÃ³n"
# (una tilde mal decodificada desde Bash) dan la misma clave.
function Clave([string]$s) { return ([regex]::Replace($s.ToLowerInvariant(), '[^a-z0-9]', '')) }

function Leer {
    if (-not (Test-Path $lock)) { return $null }
    try { return Get-Content $lock -Raw | ConvertFrom-Json } catch { return $null }
}

switch ($Accion) {
    "status" {
        $l = Leer
        if ($l) { "OCUPADO por $($l.sector) desde $($l.desde) - $($l.motivo)"; exit 1 }
        "LIBRE"; exit 0
    }
    "take" {
        if (-not $Sector) { "Falta -Sector"; exit 2 }
        $json = (@{ sector = $Sector; desde = (Get-Date).ToString("HH:mm:ss"); motivo = $Motivo } | ConvertTo-Json -Compress)
        try {
            # CreateNew falla si el archivo ya existe: dos chats no pueden tomarlo a la vez.
            $fs = [IO.File]::Open($lock, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            $bytes = [Text.Encoding]::UTF8.GetBytes($json)
            $fs.Write($bytes, 0, $bytes.Length); $fs.Close()
            "TOMADO por $Sector"; exit 0
        } catch {
            $l = Leer
            "OCUPADO por $($l.sector) desde $($l.desde) - $($l.motivo). No toques Assets/ ni Play."; exit 1
        }
    }
    "release" {
        $l = Leer
        if (-not $l) { "Ya estaba LIBRE"; exit 0 }
        if ((Clave $l.sector) -ne (Clave $Sector) -and -not ($Force -and (Clave $Sector) -eq "cerebro")) {
            "No podés liberarlo: lo tiene $($l.sector)."; exit 1
        }
        Remove-Item $lock -Force
        "LIBERADO por $Sector"; exit 0
    }
}
