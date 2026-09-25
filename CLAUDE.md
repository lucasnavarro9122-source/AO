# AO · versión actual: AO BATTLESERVER (Unity)

Nombres: el proyecto se llama **AO** y la versión en desarrollo, **AO BATTLESERVER**. "AoDuels" es solo el nombre de la carpeta del escritorio y de los chats; `My project (1)` es el nombre técnico de la carpeta y del productName de Unity. No renombrar ninguno de los dos sin coordinar (productName define la ruta de los guardados).

Migración de Argentum Online a Unity + alpha cooperativa privada para amigos (servidor en la PC del anfitrión, conexión por Tailscale). Priorizar versión chica y funcional.

## Reglas del usuario
- Responder en español, breve y claro (estilo caveman). Ahorrar tokens.
- NO cambiar stats ni balance de clases del original sin pedido explícito.
- Sin voces de NPC. Sí música de ciudades, efectos e intro original.
- Interfaz fiel al original, botones conectados, sin textos superpuestos ni botones de debug ("dar kit de prueba").
- Chat sin mensajes de autoguardado; texto hablado sobre el personaje, sin fondo negro.
- No commitear sin que el usuario lo pida. Preservar cambios sin commit y archivos nuevos ajenos.
- **Contexto de una sola vez:** leer conversaciones viejas, cargar historial o recibir instrucciones de eficiencia es un costo de arranque, no trabajo real. Se paga una vez: se resume a archivos (`docs/claude/`) y a partir de ahí todo chat arranca desde esos resúmenes.
  - Nunca releer las conversaciones crudas ni bifurcar un chat que cargó ese contexto.
  - Al medir consumo, separar el arranque del trabajo real.
- Hay varias versiones de Argentum (https://github.com/orgs/ao-libre/repositories): no mezclar datos/protocolos suponiendo compatibilidad.

## Guardados: intocables
- Ya hubo una prueba que sobrescribió un guardado. Antes de cualquier prueba: respaldo con fecha. Nunca usar personajes del usuario como fixtures.
- Locales: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/My project (1)` (ej. `AO_Demo/save_slot_1.json`).
- Servidor: `../AO_Online/Release/Server/Saves` y `room-key.txt` — no reemplazar al actualizar ejecutables, no leer ni publicar la clave.
- PlayerPrefs de Windows (preferencias e identidad online): no borrar.

## Rutas clave
- Proyecto/repo: esta carpeta. Unity **6000.3.17f1** (`C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe`).
- Scripts juego: `Assets/AOMigrator/Runtime`. Editor: `Assets/AOMigrator/Editor`.
- Escena: `Assets/Scenes/AOMigrator/Generated/AO_Ciudad_de_Ullathorpe_Playable.unity` (27 MB: nunca leer entera, solo grep).
- Servidor propio C#: `OnlineServer/`. El repo está en **protocolo 3** (oro del servidor + retos de la demo); el ZIP 0.26 y `Server-next-v260` siguen en protocolo 2. La demo corre con `--demo --data SavesDemo --port 7778`. Distribución: `../AO_Online` (`VERSION.txt`, `LEEME.md`).
- Material original: `Archivos Originales/` (1,2 GB: solo grep puntual).
- Cámara: no asumir tag `MainCamera`; usar `AOCameraFollow` / `AOActionBarV260.GameCamera`.

## Comandos (filtrar salida)
```powershell
git status --short
dotnet build AOCoopCompile.csproj -v:q -clp:ErrorsOnly 2>&1 | Select-Object -Last 15   # ~107 warnings, ignorar
python Tools/test_controls_unity.py      # Unity abierto, fuera de Play; usa prefs temporales
python Tools/package_online_client.py    # genera ../AO_Online/Cliente-para-amigos.zip (guardar copia del anterior)
```
- `dotnet build` solo cubre scripts runtime; no reemplaza compilar en Unity ni Play.
- Log del editor: `Select-String -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Pattern "error CS|Exception" | Select-Object -Last 30`. Nunca volcar el log entero.
- Build cliente: menú **AO Migrator > Build private room client** → `../AO_Online/Release/Client/ArgentumOnline.exe`.

## No leer (sumideros de tokens)
`Builds/`, `Library/`, `Temp/`, `Logs/`, `obj/`, `bin/`, `AO_Migrator_Backup_*`, `MigrationReports/editor-recovery-*`, `ProjectSettings.zip`, `.meta` en masa, imágenes, `.unity`/`.prefab` completos. Varios están bloqueados en `.claude/settings.json`.

## Estado (24/09/2026, noche)
- Último commit: `384fe18`. Incluye V260–V270 verificados (controles, hotbar, skill shots, casteo, meditación, EOT, QA), la organización por sectores y el diseño de la demo.
- Sin commitear a propósito: `Assets/_Recovery`, `MigrationReports/editor-recovery-*` y `ProjectSettings.zip` (306 MB). No borrar sin revisar.
- El ZIP para amigos sigue siendo el 0.25. El 0.26 está en preparación (QA).
- Pruebas: `test_modules_unity.py` y `test_controls_unity.py` pasan; builds del cliente y del servidor sin errores.
- **Demo AO BATTLESERVER:** fase 1 (diseño) terminada; las 17 decisiones están aprobadas (`docs/claude/demo/decisiones.md`). Fase 2 (implementación) en curso.
- Informes viejos de `MigrationReports/` son históricos (el audit dice que no hay red: obsoleto).
## Sectores (chats), ver `docs/claude/sectores.md`
7 chats:
- **AO BATTLESERVER: CEREBRO** coordina, integra y commitea.
- Sectores: **AO BATTLESERVER: Programación**, **Arte y Animación**, **Servidor y Multiplayer**, **Contenido y Fidelidad AO**, **Interfaz y Controles**, **QA y Releases**.

Cada sector edita solo sus archivos, toma el candado de Unity y reporta a Cerebro. Tareas y prioridades en `docs/claude/tablero.md`. Al empezar cualquier chat: skill `aod-sector`.

## Skills del proyecto (`.claude/skills/`)
- `aod-sector`: protocolo entre sectores.
- `aod-respaldo`: respaldo de guardados (`Tools/aod_backup.ps1`).
- `aod-verificar`: build + `Tools/aod_log_errors.ps1` + pruebas.
- `aod-modulo-nuevo`: convenciones C# y trampas conocidas.
- `aod-datos-ao`: datos y código del AO original.
- `aod-arte`: remaster 4×, SpriteForge, Higgsfield, Spell Tester.
- `aod-servidor` y `aod-release`.
- `aod-cerebro`: puente nube ↔ CEREBRO (ver estado, mandar mensajes con OK de Lucas, buzón por GitHub).

## Siguiente paso
Ver "Prioridades de Cerebro" en `docs/claude/tablero.md`. Pendiente del Optimizador de tokens: cuando Lucas abra un chat nuevo en este proyecto, avisarle para medir el ahorro.

## Subagentes (`.claude/agents/`)
Delegar para no llenar el contexto: `buscador-codigo` (dónde se define/usa algo), `lector-logs` (Editor.log, build, servidor → solo errores), `verificador` (dotnet build + pruebas → pasa/falla), `revisor-diff` (revisar cambios antes de commit/build). Decisiones de diseño, red/protocolo y bugs entre módulos: en el chat principal.

## Detalle bajo demanda
- Controles AO/MOBA: `.claude/rules/controles.md` (se carga al tocar esos archivos).
- Servidor/online/build: `.claude/rules/online.md`.
- Recuperación de Unity, OneDrive, pasar el proyecto a otra IA: `docs/claude/recuperacion-y-traspaso.md`.
- Historial de chats anteriores (v0.1→v0.18, v261–v269, Spell Tester, Higgsfield, SpriteForge): `docs/claude/historial.md`.
- Documento original completo: `CONTEXTO_PARA_OTRA_IA.md`.
