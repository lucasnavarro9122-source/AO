# Contexto para continuar Argentum Remake en otra IA

Actualizado: 24/09/2026. Este documento resume lo recuperable de esta charla y del proyecto local. No es una transcripción completa. La otra IA debe comprobar el código actual antes de cambiarlo.

## Objetivo y preferencias del usuario

Migrar Argentum Online a Unity y desarrollar después una versión propia. Primera alpha privada para jugar y progresar con amigos; el anfitrión ejecuta el servidor en su PC y se conectan mediante Tailscale. Priorizar una versión pequeña y funcional, sin sobredimensionarla.

- Responder en español, breve y claro. Ahorrar tokens; estilo caveman full para la conversación.
- Mantener las estadísticas y el balance de clases del juego original hasta probar con amigos.
- No agregar voces de NPC. Sí música de ciudades, efectos e intro original.
- Interfaz fiel al original, botones conectados, sin textos superpuestos ni funciones como «dar kit de prueba».
- Chat sin mensajes de autoguardado; texto hablado sobre el personaje, sin fondo negro.
- Conservar todos los personajes, partidas y preferencias. Ya hubo un incidente de una prueba que sobrescribió un guardado: las pruebas deben usar datos aislados y respaldo previo.

## Ubicaciones y herramientas

- Proyecto Unity / repositorio Git: `C:\Users\lucas\OneDrive\Desktop\AoDuels\My project (1)`.
- Material original: subcarpeta `Archivos Originales` del proyecto.
- Distribución y servidor: `C:\Users\lucas\OneDrive\Desktop\AoDuels\AO_Online`.
- Código del servidor propio: `OnlineServer` dentro del proyecto.
- Scripts del juego: `Assets/AOMigrator/Runtime`.
- Herramientas del editor: `Assets/AOMigrator/Editor`.
- Escena: `Assets/Scenes/AOMigrator/Generated/AO_Ciudad_de_Ullathorpe_Playable.unity`.
- Unity: **6000.3.17f1**, confirmado en `ProjectSettings/ProjectVersion.txt`.
- Unity.exe: `C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe`.
- Herramientas auxiliares: Python y .NET; shell PowerShell.
- Recursos originales de referencia: https://github.com/orgs/ao-libre/repositories . Hay varias versiones de Argentum: no mezclar datos/protocolos suponiendo compatibilidad.

## Estado de la migración

Hay mapas, renderizado, personajes, NPC, inventario, comercio, banco, magia, misiones, muerte/resurrección, guardado, audio y configuración. No afirmar que la migración está al 100%.

Los informes históricos registran 842 mapas cargados, 4.275 objetos, 141 hechizos definidos y 317 misiones con contenido. Son cifras de esos informes, no una prueba nueva de todas las mecánicas. `MigrationReports/PROJECT_AUDIT_20260923.md` y `MigrationReports/README.md` contienen información histórica: sus afirmaciones de que no existe red quedaron obsoletas después de implementar el servidor cooperativo.

La alpha cooperativa usa un servidor propio C# y protocolo 2. Incluye enemigos, vida, botín, puertas y comercio compartidos; EXP y crédito de misiones para compañeros cercanos; nombres/equipo, curación/resurrección entre jugadores y persistencia. Sigue siendo una alpha para amigos de confianza, sin paridad completa con el servidor original. No asumir que existe PvP, clanes u oficios completos. Ver `OnlineServer/README.md` y `AO_Online/LEEME.md`.

`AO_Online/VERSION.txt` declara cliente 0.25 y servidor 0.25.1, protocolo 2. El último commit corrige tableros de misiones que caminaban como NPC. Revisar los binarios y fechas antes de afirmar qué código incluye el ZIP instalado.

## Trabajo de esta charla: controles AO / MOBA

Se implementaron en código:

- Selector **Ajustes > Controles > AO / MOBA**, con teclas independientes por perfil.
- AO conserva sus controles; macros de hechizos apagadas de inicio, habilitables desde configuración. Teclas iniciales F5/F6/F7/F8.
- MOBA: clic derecho para caminar, perseguir/atacar o acercarse a interactuar; Q/W/E/R para hechizos; 1/2/3/4 para consumibles; M mapa, I inventario, J misiones, C personaje, F interactuar, G recoger, A atacar, S detener y B meditar.
- Teclas y botones de mouse editables, validación de conflictos y opción de quitar tecla. Enter/Escape reservados.
- Hechizos aprendidos y consumibles asignables por personaje y perfil. Una acción por pulsación; conservar maná, requisitos y cooldown.
- Rutas por casillas respetando obstáculos; movimiento de fantasma hacia el sacerdote.
- Música de inicio MIDI 2, identificada como **Argentum Opening**. Ullathorpe usa MIDI 4. Caché MIDI de rutas cortas en `%LOCALAPPDATA%/AoDuels/Music` para evitar fallos de Windows MCI.

Archivos principales: `AOControlProfilesV260.cs`, `AOActionBarV260.cs`, `AOShortcutHUDV260.cs`, `AOControlsSettingsUIV260.cs`; modificaciones en ajustes, interfaz, jugador, magia, combate, inventario, audio y menú.

### Verificación efectivamente realizada

`MigrationReports/controls_v260.json` dio `passed: true`, etapa 7, el **24/09/2026 a las 00:23**. Probó perfiles, conflictos, macros AO, rutas, clic derecho sintético, consumibles, maná/cooldown, bloqueo de acciones bajo ajustes y cambio de intro a música de ciudad. Hay capturas `controls_ao_v260.png` y `controls_moba_v260.png`. Las pruebas previas confirmaron 7 archivos JSON de guardado sin cambios.

**Importante:** existen modificaciones posteriores del 24/09 por la tarde. Esa prueba no valida la versión actual completa. El build/ZIP de estos cambios no quedó confirmado en esta charla. No presentar la documentación de 0.26 como prueba de que el ejecutable distribuido ya la contiene.

## Cambios posteriores encontrados, todavía sin verificar aquí

Hay código nuevo de arrastrar y soltar en la barra (`AOActionBarDragDropV261`, `AOInterfaceActionBarBridgeV261`), proyectiles dirigidos (`AOSkillShotConfigV267`, `AOSkillShotProjectileV267`), animación de lanzamiento (`AOCastAnimationDatabaseV268`, `AOCastAnimationRuntimeV268`), efectos persistentes/reemplazos visuales y meditación (`AOMeditationVisualV269`). También cambiaron renderizador, magia, efectos y grilla. Leer sus fuentes y averiguar el alcance vigente antes de modificarlas; no descartarlas ni tratarlas como propias de la prueba de 00:23.

## Pendiente recomendado al retomar

1. Leer este documento, `git status`, fuentes actuales y guías del servidor. Separar informes históricos de pruebas de la revisión actual.
2. Preservar los cambios sin commit y todos los archivos nuevos. Un checkout del último commit por sí solo NO contiene el trabajo actual.
3. Respaldar los guardados antes de cualquier prueba. No usar personajes del usuario como fixtures.
4. Compilar y probar en Unity los controles y los módulos posteriores, incluyendo una sesión cooperativa real. No alterar balance de clases sin una nueva indicación.
5. Si todo pasa, generar cliente Windows, verificar el ZIP y registrar qué commit/revisión incluye. Guardar en Git únicamente el trabajo revisado, respetando cambios ajenos.

## Comandos y pruebas

Desde la raíz del proyecto:

```powershell
git status --short
dotnet build AOCoopCompile.csproj -v:q -clp:ErrorsOnly
python Tools/test_controls_unity.py
```

La compilación auxiliar cubre scripts runtime y no sustituye el build ni Play. La prueba requiere Unity abierto fuera de Play; crea preferencias temporales y protege guardados. Revisar su código si las nuevas mecánicas cambiaron supuestos de la prueba. Usa un mouse sintético porque el editor puede consumir los eventos cuando Game View pierde foco.

Build: **AO Migrator > Build private room client**. Salida: `AO_Online/Release/Client/ArgentumOnline.exe`.

```powershell
python Tools/package_online_client.py
```

Genera `AO_Online/Cliente-para-amigos.zip` y verifica su CRC. Conservar una copia del ZIP anterior.

`AOOnlineBuildV240.cs` también reconoce solicitudes explícitas en `Temp`: `refresh_online_client`, `build_online_client` y `restart_online_editor`. La última intenta guardar escenas y assets antes de salir. No dejar marcadores olvidados ni solicitarlos durante una partida del usuario.

## Guardados, servidor y precauciones concretas

- Guardados locales: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/My project (1)`; entre ellos `AO_Demo/save_slot_1.json`.
- Servidor: `AO_Online/Release/Server/Saves` y `room-key.txt`. No reemplazarlos al actualizar ejecutables.
- Preferencias e identidad online también usan PlayerPrefs de Windows; no borrarlas.
- El servidor usa TCP 7777 y Tailscale. El anfitrión puede usar `127.0.0.1`; amigos necesitan la IP Tailscale del anfitrión y su propia conexión autorizada. Las claves se comparten en privado.
- No publicar claves, tokens, credenciales, registros que los contengan ni partidas personales.
- Hay respaldos de cliente/servidor anteriores en `AO_Online`. El proyecto está en OneDrive; hubo fallos de acceso a `Temp/BurstOutput`. Antes de mover esa caché, verificar rutas y conservar copia; nunca borrar carpetas calculadas sin comprobarlas.
- La cámara del juego no necesariamente tiene etiqueta `MainCamera`: usar `AOCameraFollow`/`AOActionBarV260.GameCamera`.

## Recuperación de Unity

Durante esta sesión Unity quedó funcionando sin ventana accesible. A pedido del usuario se respaldaron escenas, ajustes y código en `MigrationReports/editor-recovery-20260924-001627`, se reinició el editor y se recuperó su ventana. No se logró confirmar un guardado de la escena en memoria antes del cierre; se conservó la escena de recuperación disponible en disco. Las partidas JSON quedaron intactas. Existe además `Assets/_Recovery`; no eliminarlo sin revisar.

## Qué entregar a otra IA

- **IA con acceso local/IDE:** abrir la carpeta del proyecto y darle este archivo.
- **IA de chat sin acceso al disco:** adjuntar este archivo y los archivos necesarios; una ruta de Windows por sí sola no le da acceso. Para una copia del proyecto incluir `Assets` con sus `.meta`, `Packages`, `ProjectSettings`, `OnlineServer` y `Tools` (sin binarios temporales ni secretos). Agregar recursos originales concretos cuando la tarea los necesite.
- Excluir `Library`, `Temp`, `Logs`, `obj`, `bin`, credenciales y guardados personales. Conservar respaldos privados aparte.
- Para la charla literal, copiar los mensajes que interese conservar a un documento y adjuntar las capturas originales por separado. Este resumen no transporta automáticamente la memoria ni las imágenes de la conversación.

### Prompt inicial sugerido

«Continuá mi proyecto Argentum Remake. Leé CONTEXTO_PARA_OTRA_IA.md y comprobá el estado real del repositorio antes de editar. Conservá partidas y cambios existentes; no cambies el balance de clases ni agregues voces de NPC. Distinguí pruebas antiguas de la revisión actual. Respondé en español y breve. Primero decime qué encontraste y cuál es el siguiente paso concreto.»

## Snapshot de Git al crear este archivo

Último commit confirmado: d69daef Fix stationary quest boards in cooperative server.

```text
 M Assets/AOMigrator/Editor/AOOnlineBuildV240.cs
 M Assets/AOMigrator/Runtime/AOAudioMusicV210.cs
 M Assets/AOMigrator/Runtime/AOCharacterRenderer.cs
 M Assets/AOMigrator/Runtime/AOGridMap.cs
 M Assets/AOMigrator/Runtime/AOInterfaceClassicControlsV200.cs
 M Assets/AOMigrator/Runtime/AOInventoryV10.cs
 M Assets/AOMigrator/Runtime/AOMagicEffectRuntimeV129.cs
 M Assets/AOMigrator/Runtime/AOMainMenuV140.cs
 M Assets/AOMigrator/Runtime/AOPlayerCombatV09.cs
 M Assets/AOMigrator/Runtime/AOPlayerMagicV120.cs
 M Assets/AOMigrator/Runtime/AOPlayerSettingsV230.cs
 M Assets/AOMigrator/Runtime/AOSpellFXV120.cs
 M Assets/AOMigrator/Runtime/AOTestPlayer.cs
?? Assets/AOMigrator/Editor/AOControlsQA260.cs
?? Assets/AOMigrator/Editor/AOControlsQA260.cs.meta
?? Assets/AOMigrator/Runtime/AOActionBarDragDropV261.cs
?? Assets/AOMigrator/Runtime/AOActionBarDragDropV261.cs.meta
?? Assets/AOMigrator/Runtime/AOActionBarV260.cs
?? Assets/AOMigrator/Runtime/AOActionBarV260.cs.meta
?? Assets/AOMigrator/Runtime/AOCastAnimationDatabaseV268.cs
?? Assets/AOMigrator/Runtime/AOCastAnimationDatabaseV268.cs.meta
?? Assets/AOMigrator/Runtime/AOCastAnimationRuntimeV268.cs
?? Assets/AOMigrator/Runtime/AOCastAnimationRuntimeV268.cs.meta
?? Assets/AOMigrator/Runtime/AOControlProfilesV260.cs
?? Assets/AOMigrator/Runtime/AOControlProfilesV260.cs.meta
?? Assets/AOMigrator/Runtime/AOControlsSettingsUIV260.cs
?? Assets/AOMigrator/Runtime/AOControlsSettingsUIV260.cs.meta
?? Assets/AOMigrator/Runtime/AOInterfaceActionBarBridgeV261.cs
?? Assets/AOMigrator/Runtime/AOInterfaceActionBarBridgeV261.cs.meta
?? Assets/AOMigrator/Runtime/AOMeditationVisualV269.cs
?? Assets/AOMigrator/Runtime/AOMeditationVisualV269.cs.meta
?? Assets/AOMigrator/Runtime/AOShortcutHUDV260.cs
?? Assets/AOMigrator/Runtime/AOShortcutHUDV260.cs.meta
?? Assets/AOMigrator/Runtime/AOSkillShotConfigV267.cs
?? Assets/AOMigrator/Runtime/AOSkillShotConfigV267.cs.meta
?? Assets/AOMigrator/Runtime/AOSkillShotProjectileV267.cs
?? Assets/AOMigrator/Runtime/AOSkillShotProjectileV267.cs.meta
?? Assets/AOMigrator/Runtime/AOSpellPersistentVisualV130.cs
?? Assets/AOMigrator/Runtime/AOSpellPersistentVisualV130.cs.meta
?? Assets/AOMigrator/Runtime/AOSpellVisualOverridesV130.cs
?? Assets/AOMigrator/Runtime/AOSpellVisualOverridesV130.cs.meta
?? Assets/Resources/AOMigrator/MeditationV269.meta
?? Assets/Resources/AOMigrator/MeditationV269/
?? Assets/StreamingAssets/AOMigrator/CastV268.meta
?? Assets/StreamingAssets/AOMigrator/CastV268/
?? Assets/StreamingAssets/AOMigrator/MeditationV269.meta
?? Assets/StreamingAssets/AOMigrator/MeditationV269/
?? Assets/StreamingAssets/AOMigrator/SpellOverrides.meta
?? Assets/StreamingAssets/AOMigrator/SpellOverrides/
?? Assets/_Recovery.meta
?? Assets/_Recovery/
?? MigrationReports/CONTROLES_AO_MOBA_V260.md
?? MigrationReports/controls_ao_v260.png
?? MigrationReports/controls_moba_v260.png
?? MigrationReports/controls_v260.json
?? MigrationReports/editor-recovery-20260924-001627/
?? MigrationReports/unity_classic_journal_qa.png
?? ProjectSettings.zip
?? Tools/test_controls_unity.py
```
