---
name: aod-release
description: Arma y entrega una versión nueva del cliente de AoDuels para los amigos (build de Windows, ZIP, VERSION.txt, registro del commit) sin romper partidas ni la sala. Usar cuando Lucas pide "armá el cliente", "pasale la versión a los amigos", "hacé el ZIP" o antes de una sesión cooperativa con cambios nuevos.
---

# Release del cliente

## Antes
1. Todo lo que entra está verificado (`aod-verificar`, niveles 1–3) y listado en el tablero. Decir explícitamente qué módulos entran **sin** prueba automática.
2. `aod-respaldo`.
3. Anotar el commit base (`git rev-parse --short HEAD`) y si hay cambios sin commitear. Lo ideal es que Cerebro commitee antes, con el OK de Lucas.
4. Tomar el candado de Unity.

## Build
- Unity: menú **AO Migrator > Build private room client** → `../AO_Online/Release/Client/ArgentumOnline.exe`.
- Alternativa sin tocar el menú: `AOOnlineBuildV240` reconoce los marcadores `Temp/build_online_client` (y `refresh_online_client`, `restart_online_editor`; este último no guarda nada y no reinicia si hay escenas con cambios). Crear uno solo si Lucas no está jugando, esperar el resultado y confirmar que el marcador desapareció.
- Revisar la fecha del `.exe` y los errores con `Tools/aod_log_errors.ps1`.

## Empaquetar
```powershell
Copy-Item ../AO_Online/Cliente-para-amigos.zip ("../AO_Online/Cliente-para-amigos_" + (Get-Date -Format yyyyMMdd-HHmm) + "_anterior.zip")
python Tools/package_online_client.py            # genera el ZIP y verifica el CRC
```
- Actualizar `../AO_Online/VERSION.txt`: versión del cliente, commit, protocolo y cambios en 3–5 líneas para los amigos.
- Si cambió el protocolo, el servidor tiene que salir al mismo tiempo (`aod-servidor`).

## Recordatorios que hay que mostrarle a Lucas en cada release
- ⏰ **21 hechizos sin sonido** (faltan los audios 163, 229, 234, 239, 242, 253, 255 y 256; no son públicos). Lucas decidió dejarlos mudos hasta conseguirlos o crearlos. Preguntarle si ya hay novedades.

## Después
- Probar el `.exe` empaquetado una vez (abrir, entrar a la sala de prueba o jugar local, salir) antes de avisar a los amigos.
- Registrar en `docs/claude/tablero.md` → Hecho: versión, commit y fecha.
- El ZIP no contiene Saves, `room-key.txt`, PlayerPrefs ni logs.
