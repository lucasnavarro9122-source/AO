---
name: aod-datos-ao
description: Encuentra y lee los datos y el código originales de Argentum Online (AO20, ao-org) para que AoDuels sea fiel al original. Usar cuando haga falta un stat, fórmula, hechizo, NPC, objeto, quest, FX, sonido, mapa o gráfico original, o para comprobar si algo "es así en el AO".
---

# Datos originales de AO

Base: `Archivos Originales/` (solo lectura, 1,2 GB). Buscar siempre con grep puntual; nunca listar ni leer carpetas enteras.

## Dónde está cada cosa
Raíz de recursos: `Archivos Originales/Recursos-master/Recursos-master/`
| Qué | Archivo |
|---|---|
| NPC (stats, drops, hechizos, CastAnimation, movimiento) | `Dat/npcs.dat` (también `Variables de NPCs.txt`) |
| Objetos | `Dat/obj.dat` (+ `ObjAlquimista/Carpintero/Sastre`, `ArmasHerrero`, `ArmadurasHerrero`) |
| Hechizos (maná, WAV, FX, target, área) | `Dat/Hechizos.dat` |
| Efectos en el tiempo | `Dat/EffectsOverTime.dat` |
| **Meditación nivel → FX** | `Dat/Meditaciones.dat` (reemplaza los tiers inventados de V269 si coincide) |
| Clases, razas, fórmulas | `Dat/Balance.dat` (hay `Balance2/3`: confirmar cuál usa el server) |
| Quests | `Dat/Quests.DAT`, `GlobalQuests.dat` |
| Drops globales, tesoros | `Dat/GlobalDropTable.dat`, `Tesoros.dat` |
| Ciudades, mapas especiales, zonas | `Dat/Ciudades.Dat`, `MapasEspeciales.dat`, `zonas.dat`, `Map.dat` |
| Invocaciones | `Dat/Invokar.dat` |
| Gráficos (GRH), cuerpos, cabezas, cascos, armas, escudos | `init/graficos.ini`, `cuerpos.dat`, `cabezas.ini`, `cascos.ini`, `armas.dat`, `escudos.dat`, `moldes.ini` |
| FX y partículas | `init/FXs.ini`, `Effects.ini`, `particles.ind`, `ProjectileDef.dat` |
| Mapas | `Mapas/mapa<N>.csm` · minimapas en `Minimapas/` |
| Audio | `wav/<id>.wav`, `SoundsOgg/`, `midi/`, `Mp3/` |
| Interfaz | `interface/` (+ referencia en `ao-ui-master/`) |

Código original (VB6, la verdad de las fórmulas):
- Servidor: `Archivos Originales/argentum-online-server-master/Codigo/`. Claves: `AI_NPC.bas` (IA de NPC), `GameLogic.bas`, `Comercio.bas`, `modBanco.bas`, `InvUsuario.bas`, `EffectsOverTime.bas`, `Hogar.bas`, `CharacterPersistence.bas`, `ModAreas.bas`; hechizos en `modHechizos`/`*Hechizo*`. Intervalos en `intervalos.ini`.
- Cliente: `Archivos Originales/argentum-online-client-master/argentum-online-client-master/CODIGO/` (render, `CargarMapa`, `LoadGrhIni`, `HandleDoAnimation`, frmMain).
- Editor de mapas: `argentum-online-worldeditor-master/`.

## Datos ya procesados en el proyecto
`Assets/Resources/AOMigrator/*.json` (NPC, objetos, hechizos, quests, RPG, visuales) y `OnlineServer/Data/catalog.json.gz` (842 mapas, lo genera `Tools/export_online_catalog.py`). Antes de reprocesar, fijate si el dato ya existe ahí.

## Reglas
- Los `.dat`/`.ini` están en Windows-1252: leer con `encoding="cp1252"`.
- No mezclar versiones de AO (hay varias en ao-libre/ao-org); los datos de este proyecto vienen de **ao-org AO20**.
- Si el cliente no tiene el dato (lo decide el servidor), buscar en el código del servidor antes de inventar. Si no aparece, decirlo y proponer una regla marcada como "no original".
- Balance: reproducir el original. Cualquier cambio de stats o clases necesita el OK de Lucas.
- Delegar búsquedas grandes al subagente `buscador-codigo`.
