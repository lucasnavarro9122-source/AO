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
- Servidor propio C#: `OnlineServer/` (protocolo 2). Distribución: `../AO_Online` (`VERSION.txt`, `LEEME.md`).
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

## Estado (24/09/2026)
- Último commit: `d69daef` (cliente 0.25, servidor 0.25.1, protocolo 2). El ZIP para amigos es 0.25 (23/09) y NO incluye nada posterior.
- Sin commit: controles AO/MOBA (V260) + módulos de la tarde del 24/09 (V261 drag&drop barra, V267 skill shots, V268 animación de lanzamiento, V269 meditación, V130 efectos persistentes/overrides) + cambios en renderizador, magia, efectos y grilla.
- Hecho el 24/09 (tarde), sobre el código actual sin commit:
  - Respaldo en `../Respaldos/guardados-20260924-1741`: LocalLow, `ServerSaves`, `PlayerPrefs.reg` y el informe viejo `controls_v260_0023.json`.
  - `dotnet build` auxiliar: 0 errores (107 warnings).
  - Consola de Unity (Editor.log): sin errores de compilación ni excepciones.
  - `python Tools/test_controls_unity.py`: PASA (etapa 7), 7 guardados intactos. `MigrationReports/controls_v260.json` ya es de esta corrida.
  - Creados `CLAUDE.md`, `.claude/rules/`, `.claude/agents/` (4 subagentes), `.claude/settings.json` y `docs/claude/` para ahorrar tokens.
- Falta verificar: V261, V267, V268, V269 y V130. `AOControlsQA260` no los prueba; solo se sabe que compilan y cargan sin excepciones.
- Informes en `MigrationReports/` son históricos (el audit dice que no hay red: obsoleto).
- Existe `Assets/_Recovery` y `MigrationReports/editor-recovery-20260924-001627`: no borrar sin revisar.

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
