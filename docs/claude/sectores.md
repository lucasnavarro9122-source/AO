# Sectores de AoDuels

Todos trabajan en la misma carpeta y la misma rama. Cada sector **solo edita lo suyo**. **AO BATTLESERVER: CEREBRO** coordina, integra, revisa y commitea (solo con pedido de Lucas).

Cómo se complementan: Contenido trae los datos originales → Programación los vuelve sistemas → Interfaz los muestra y los controla → Arte los hace ver bien → Servidor los comparte online → QA verifica y entrega a los amigos.

| # | Sector (nombre del chat) | Dueño de |
|---|---|---|
| 1 | **AO BATTLESERVER: Programación** | Lógica de juego en C#: jugador, combate, magia (lógica), NPC/IA, mundo/grilla, inventario, RPG, quests, guardado, muerte, skill shots |
| 2 | **AO BATTLESERVER: Arte y Animación** | Render, sprites, animaciones, FX de hechizos, meditación, casteo, iluminación/clima, terreno HD, Higgsfield, SpriteForge, Spell Tester |
| 3 | **AO BATTLESERVER: Servidor y Multiplayer** | `OnlineServer/`, protocolo, cliente online, sincronización, Tailscale, publicación del servidor |
| 4 | **AO BATTLESERVER: Contenido y Fidelidad AO** | Datos originales → bases de datos del juego: mapas, NPC, objetos, hechizos (datos), quests, drops, audio/música, balance fiel al AO |
| 5 | **AO BATTLESERVER: Interfaz y Controles** | HUD clásico, ventanas, menú, creación de personaje, ajustes, controles AO/MOBA, hotbar, marcadores y feedback en pantalla |
| 6 | **AO BATTLESERVER: QA y Releases** | Pruebas, verificadores, respaldos, build del cliente, ZIP para amigos, versión, informes |
| 7 | **AO BATTLESERVER: Higgsfield** (chat local) | **Todo lo de Higgsfield**: generaciones, créditos, presupuestos, pilotos, remaster HD, FX/video, audio, 3D y material para mostrar. Carpeta propia: `docs/claude/higgsfield/` |

**Higgsfield (sector 7):**
- Ningún otro chat usa Higgsfield directamente: todo pedido va a "AO BATTLESERVER: Higgsfield" con SendMessage. Ojo: hay otro chat **en la nube** con el nombre viejo ("Higgsfield mejoras AO Battleserver"); ese no es el sector.
- Arte prepara los pedidos (atlas, hojas, referencias, reglas 4×) e integra los resultados a Unity. Higgsfield entrega los archivos en su carpeta o donde Arte indique, sin tocar `Assets/`.
- Créditos: nada se genera sin el OK de Lucas y sin mostrar el costo antes. Cada lote se anota en `docs/claude/higgsfield/`.

## Mapa de archivos (`Assets/AOMigrator/Runtime` salvo que se indique)

**1 Programación:** `AOPlayerCombatV09`, `AOPlayerMagicV120`, `AOPlayerMagicStatusV120`, `AOPlayerRPGV11`, `AOTestPlayer`, `AOCameraFollow`, `AOGridMap`, `AOWorldManager*`, `AONPCMovementV08`, `AONPCCombatV09`, `AONPCMagicStatusV120`, `AONPCMetadata`, `AOInventoryV10`, `AOLootPickupV09`, `AOSaveGameV140`, `AOSaveBootstrapV140`, `AOQuestSystemV150`, `AOQuestBootstrapV150`, `AODeathRespawnV160`, `AODeathRespawnBootstrapV160`, `AOSkillShotProjectileV267`, `AOMagicAutoBootstrapV120`, `AOMagicEffectRuntimeV129`, `AOSummonedPetV129`, `AOCityNPCSystemV130`, `AOCityBankV130`, `AOCityBootstrapV130`, `AOInteractable`, `AODoorV210`, `AOHomeCityV200`, `AOInitialLoadoutV170`, `AOCharacterIdentityV170`, `AORPGAutoBootstrapV11`, `AOWorldObjectMetadata`.

**2 Arte y Animación:** `AOCharacterRenderer`, `AOAnimatedSprite`, `AOCharacterVisualDatabaseV111`, `AOCharacterProfileVisualV111`, `AOSpellFXV120`, `AOSpellVisualOverridesV130`, `AOSpellPersistentVisualV130`, `AOCastAnimation*V268`, `AOMeditationVisualV269`, `AOCombatFeedbackV113`, `AODeathVisualV160`, `AOMimicVisualV129`, `AOMapLighting`, `AOMapParticleGroup`, `AOMapWeather`, `AORenderOrderV210`, `AOTerrainHD*`. Editor: `AOTerrain*`, `AOMapVisual*`, `AOMapTextureImport`, `AOOriginalGrhResolverV029`. Datos: `StreamingAssets/AOMigrator/{CastV268,MeditationV269,SpellOverrides}` (incluye `spell_visual_tuning.json`, `skillshot_tuning.json`: el visual es de Arte; `range/speed/hitRadius` necesitan el OK de Programación), texturas de `Resources/AOMigrator`. Herramientas: `Tools/SpellTester/`, SpriteForge.

**3 Servidor:** `OnlineServer/**`, `AOOnlineClientV240`, `AOCoopProtocolV250`. Editor: `AOCoopQA250`. Tools: `test_coop_server.py`, `test_coop_unity.py`, `test_static_npcs.py`, `export_online_catalog.py`. Publicación del servidor en `../AO_Online/Release/Server*`.

**4 Contenido:** `AOCityNPCDatabaseV130`, `AONPCLootDatabaseV180`, `AONPCMagicDatabaseV129`, `AOMagicEffectDatabaseV129`, `AOSpellDatabaseV120`, `AOQuestDatabaseV150`, `AORPGDatabaseV11`, `AOItemDatabaseV10`, `AOSummonDatabaseV129`, `AODoorCatalogV210`, `AOAudioV190`, `AOAudioMusicV210`, `AOSkillShotConfigV267`, JSON de datos en `Resources/AOMigrator` y `StreamingAssets/AOMigrator/Music`, `Tools/*migration*.py`, `find_source_gaps.py`, `csm_tail_audit.py`. Solo lectura: `Archivos Originales/`.

**5 Interfaz y Controles:** `AOInterface*`, `AOClassicSkinV200`, `AOCityUIV130`, `AOQuestUIV150`, `AOQuestMarker*V180`, `AOLootFeedbackV180`, `AOConsumableFeedbackV114`, `AOMainMenuV140`, `AOCharacterCreation*`, `AOClassicLoadingV220`, `AOPlayerSettingsV230`, `AOControlProfilesV260`, `AOControlsSettingsUIV260`, `AOActionBar*`, `AOShortcutHUDV260`, `AOInterfaceActionBarBridgeV261`. Editor: `AOInterface*`, `AOClassicUIQA`.

**6 QA y Releases:** Editor: `*QA*` (salvo `AOCoopQA250`), `*Verifier*`, `*Installer*`, `AOOnlineBuildV240`, `AOMapFullAudit`, `AOMapTourQA`, `AOMapMigrationSmoke`, `AOMapLoadProfile`. Tools: `test_controls_unity.py`, `package_online_client.py`. `MigrationReports/`, `../AO_Online/Release/Client`, `../AO_Online/*.zip`, `../Respaldos/`.

**Compartido (solo con aviso a Cerebro):** la escena `.unity`, `ProjectSettings/`, `Packages/`, `CLAUDE.md`, `.claude/`, `docs/claude/sectores.md`. Un archivo que no figura en el mapa es de Programación hasta que Cerebro diga otra cosa.

## Reglas de convivencia
1. **Al arrancar:** leer `CLAUDE.md`, este archivo y tu sección de `docs/claude/tablero.md`.
2. **Archivo ajeno:** no se edita. Anotá el pedido en `tablero.md` → "Pedidos entre sectores" y avisá al sector dueño o a Cerebro con SendMessage. Excepción: arreglar un error de compilación que bloquea a todos, con aviso inmediato.
3. **Unity es uno solo. El candado REAL es `Tools/aod_unity_lock.ps1`** (atómico; desde el 25/09 04:45 reemplaza a la línea del tablero, que queda solo como información):
   - Antes de escribir en `Assets/`, de entrar en Play, de correr pruebas en Unity o de un build: `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/aod_unity_lock.ps1 take -Sector "<tu sector>" -Motivo "<qué>"`.
   - Si devuelve **exit 1 (OCUPADO): cortá todo.** No se escribe en `Assets/` ni se abre Play. Los scripts automáticos tienen que abortar con ese exit code.
   - Al terminar: `... release -Sector "<tu sector>"` y avisale al siguiente de la cola. `status` muestra quién lo tiene.
   - El orden lo sigue marcando la "Cola de Unity" del tablero: tomalo solo cuando sea tu turno.
   - Nunca entres en Play si Lucas está jugando.
   - **Mientras otro tiene el candado, no dispares recompilaciones.** No guardes cambios en `Assets/**` (preparalos y guardalos cuando el candado vuelva a `libre`), no crees marcadores en `Temp/` y no fuerces refresh ni pruebas. Una recompilación en medio de Play reinicia los estáticos y rompe la prueba (pasó el 24/09 19:04).
   - Sí podés editar fuera de `Assets/` (OnlineServer, Tools, docs).
   - **Cola de Unity:** si en el tablero hay una "Cola de Unity", el candado se toma **solo por turno**. Anotate al final de la cola y, al liberar, avisale al siguiente. Nadie se adelanta, aunque Unity esté libre en ese momento.
4. **Módulos nuevos:** pedí el número en `tablero.md` ("Próxima versión libre"), usalo y subilo en 1. Formato `AO<Nombre>V<nnn>.cs` con su `.meta`.
5. **Guardados:** respaldo previo con la skill `aod-respaldo` antes de cualquier prueba en Unity o en el servidor.
6. **Sin commits:** los sectores no commitean. Al terminar una tarea: actualizar `tablero.md` y mandar el resumen a "AO BATTLESERVER: CEREBRO" (qué archivos, qué se verificó, qué falta).
7. **Balance y fidelidad:** no cambiar stats ni balance de clases sin pedido de Lucas. Las dudas de fórmulas van a Contenido, que consulta el servidor VB6 original.
8. **Skills del proyecto:** `aod-sector` (este protocolo), `aod-respaldo`, `aod-verificar`, `aod-modulo-nuevo`, `aod-datos-ao`, `aod-arte`, `aod-servidor`, `aod-release`.
