# Tablero de AoDuels

Lo actualiza cada sector al tomar o terminar trabajo. Cerebro lo ordena y define las prioridades.

**Unity en uso por:** libre
**Próxima versión libre:** V271 (V270 = QA `AOModulesQA270`)
**Último commit:** d69daef (hay mucho trabajo V260–V269 sin commitear; ver CLAUDE.md)

## Proyecto grande en curso: Demo AO BATTLESERVER
Especificación en `docs/claude/demo-arenas.md`.
- **Fase 1 (diseño):** cada sector entrega su documento en `docs/claude/demo/`. No tocar `Assets/` para esto.
- **Fase 1 terminada.** Lucas aprobó las 17 decisiones (`docs/claude/demo/decisiones.md`) el 24/09. **Fase 2 (implementación) abierta**; Cerebro reparte las tareas.

## Prioridades de Cerebro (en orden)
1. Verificar V261/V267/V268/V269/V130 (QA + Programación + Arte).
2. Sesión cooperativa real con el cliente nuevo (Servidor + QA).
3. Build del cliente 0.26 + ZIP para amigos (QA).
4. Commit revisado de todo lo verificado (Cerebro, con OK de Lucas).

## 1 · Programación
- [ ] IA mágica original de NPC: hechizos por NPC, rango, intervalo; conectar a `CastAnimation` (V268). Coordinar datos con Contenido.
- [ ] Pérdida de objetos/oro al morir: esperar las reglas que busca Contenido.
- [x] Revisar la integración de skill shots (V267) con el combate y la red. Arreglado: el proyectil lo absorbían NPC no atacables (banquero, comerciante) y buscaba NPC por proyectil cada frame.
- [x] Online: no lanzar skill shot si el servidor no soporta ese efecto (usa `CanCastNpcSpell` de Servidor, antes de cobrar maná).

- [x] Demo fase 1: `docs/claude/demo/arquitectura.md` (mapas ≥ 1000 con `demo_map_builder.py`, generador de arena puro y compartido, PvP en el ring, oro online en misiones, banco y venta).
- [ ] Bug: los EOT sobre el jugador aplican `ModifyIncomingMagic` dos veces (`AOMagicEffectRuntimeV129.cs:107` + `AOPlayerCombatV09.cs:690`). Corregir aparte, con prueba.

## 2 · Arte y Animación
- [x] Demo AO BATTLESERVER fase 1: `docs/claude/demo/arte.md` (sets GRH por tema con mapas fuente, set de ring f5067, módulo ring+grada, hub, temas de pisos del dungeon, plan Higgsfield ~110 créditos) + hojas de referencia en `docs/claude/demo/arte/`. Rev. 2: §4 con los 7 pisos de Contenido (set visual de cada mapa fuente).
- [x] **Meditación fiel (V269):** `meditation_fx.json` usa la tabla original compatible (OK de Cerebro 24/09): 1–14→115, 15–24→116, 25–35→117, 36–44→118, 45–46→119, 47+→120, igual para los dos bandos. La de `Meditaciones.dat` (153–172) queda documentada como "no disponible públicamente" (`levelTableNoDisponible`). API: `Begin(level, criminal)`, `BeginFx(fx)` (remotos, sonido atenuado por distancia), `FxForLevel`. Clips compuestos (`loopFx`) listos para los libros espíritu 122–141.
- [ ] Ver la meditación jugando (niveles 1, 15, 25, 36, 45, 47) y con un compañero online.
- [ ] Revisar V268 (casteo), V269 (meditación) y V130 (EOT/auras) en el juego y ajustar `spell_visual_tuning.json`.
- [ ] Spell Tester v1.3: original vs HD, zoom, pivote, frame a frame, exportar el tuning.
- [ ] Sprites de 8 direcciones (NE/NO/SE/SO): definir el pipeline (SpriteForge/Higgsfield).
- [ ] Piloto HD de 20–30 texturas cuando vuelvan los créditos de Higgsfield (bloqueado: sin créditos).
- [ ] Instalar ComfyUI + los modelos de SpriteForge (bloqueado: no instalado; pedir OK a Lucas por la descarga).

## 3 · Servidor y Multiplayer
- [x] Verificar si el protocolo 2 soporta skill shots, casteo y meditación (V267–V269) o si hace falta extenderlo. (Auditoría hecha; propuesta enviada a Cerebro.)
- [x] (OK de Lucas) Enfriamiento de los skill shots contado desde el lanzamiento (`amount` = ms de vuelo, tope 1500) + meditación y casteo visibles para los compañeros (`AOCoopPlayer.meditationFx/castSpell/castSeq`). Sigue siendo protocolo 2 y es compatible con versiones viejas. Las 2 llamadas de Programación ya están. Falta probarlo en Unity y en una partida real.
- [ ] Opcional: que los compañeros vean el proyectil del skill shot (hace falta `castX/castY` + un modo "solo visual" en `AOSkillShotProjectileV267`).
- [ ] Publicar el servidor 0.26 (`Server-next`, con `aod-respaldo` antes) cuando QA arme el cliente 0.26.
- [ ] Prueba cooperativa real con 2 clientes (la de `test_coop_server.py` más una partida real).
- [ ] Documentar el procedimiento de actualización para los amigos.
- [x] Demo fase 1: `docs/claude/demo/red.md` (retos basados en ModRetos, custodia de apuestas, PvP autoritativo, semilla, espectadores, protocolo 3). Esperando el diseño de Programación (generador y fórmulas compartidas) y las decisiones de Lucas (sección 13). Rev. 2: oro del servidor con libro contable, matriz de desconexiones, modo prueba y nombres de `ui.md`. Alineado con `arquitectura.md` (§12 bis).
- [ ] Fase 2 (cuando Lucas apruebe): enlazar `Runtime/Shared/*.cs` en el csproj del servidor; `npcLayoutVersion` en el catálogo y el servidor; libro contable; retos y custodia; `--test` y `--test-time-scale`.

## 4 · Contenido y Fidelidad AO
- [x] Buscar los 8 audios de hechizo faltantes (163, 229, 234, 239, 242, 253, 255, 256). **No son públicos:** no están en nuestra copia, en GitHub `ao-org/Recursos` (ni en su historial) ni en los releases. El cliente original busca `<id>.ogg`/`<id>.wav` y, si no existen, no suena. Quedan mudos (21 hechizos). Otras versiones de AO tienen otra numeración: no sirven sin escucharlos. **Decisión de Lucas (24/09): quedan sin sonido por ahora; no asignar sonidos provisorios.**
- [ ] ⏰ **RECORDATORIO: sonidos de los 21 hechizos mudos.** Faltan los audios 163, 229, 234, 239, 242, 253, 255 y 256; hay que conseguirlos o crearlos. Recordárselo a Lucas antes de cada release y en la preparación de la demo.
- [x] **Meditación fiel:** los FX 153–172 de `Meditaciones.dat` no son públicos (`fxs.ind` llega a 150, igual en GitHub). Tabla original compatible (server 2020 → dic-2025): 1–14→115, 15–24→116, 25–35→117, 36–44→118, 45–46→119, 47+→120, igual para los dos bandos. Detalle y datos por FX: `docs/claude/contenido/meditacion_fx_original.{md,json}`. Entregado a Arte; OK de Cerebro y aplicada por Arte (24/09).
- [x] **Demo fase 1:** `docs/claude/demo/progresion.md` + `dungeon-npcs.json`.
  - 7 pisos originales (mapas 1011–1017): Madriguera, Cementerio, Mausoleo, Pirámide, Nido de arañas, Torre de Veriil y Guarida del Dragón; jefe final Vytaiz.
  - Zonas, cantidad y respawn por piso; tiempo por nivel para 4 clases; economía.
  - **Decisiones de Lucas:** multiplicador de EXP por tramo (con ×1, llegar a 30 lleva 160–330 h) y `OroMult`/pociones.
- [ ] Demo: fórmulas PvP originales (`SistemaCombate`/`modHechizos`, `magicDefense`) para `AOPvpFormulas` (pedido de Programación).
- [x] Demo: reglas de retos en `docs/claude/demo/retos-reglas.md`.
  - El impuesto va **sobre el pozo total**; en el empate cada uno recupera el 90 %.
  - En el original, el tiempo máximo (600 s) **no se controla**. Propuesta: decidir por puntaje con la regla de `FinalizarReto`.
  - Doble muerte: en el original pierde el primero que se procesa; propuesta: ronda nula (no original).
  - Apuesta mínima propuesta: 1.000.
  - Modelo reproducible para QA: `modelo_progresion.py --check`.
- [ ] Demo: que `music_migration.py` y `map_environment_migration.py` conserven los IDs ≥ 1000; textos del hub.
- [ ] Reglas originales de pérdida al morir (MapasNoDrop, newbie, oro protegido).
- [ ] Datos de hechizos por NPC (npcs.dat: `Sp`, `LanzaSpells`, etc.) para Programación.

## 5 · Interfaz y Controles
- [x] Tooltip completo de la hotbar: hechizo (nombre, tecla, maná/energía, CD y restante, objetivo/área/skill shot, efecto, descripción, avisos) y consumible (nombre, tecla, cantidad, efecto). En `AOShortcutHUDV260`.
- [x] Hotbar v262-bis: clic derecho vacía el slot (`AOActionBarDragDropV261`); se bloquea con chat/modales o durante un arrastre.
- [x] Auditoría de textos superpuestos y debug: 2 superposiciones reales (cartel ESPÍRITU vs chat; botón Grupo vs hotbar en 4:3) y HUD viejos ocultos. Pedidos a Programación y Servidor abajo. En los archivos de Interfaz no hay botones ni hotkeys de debug.
- [x] Demo fase 1: `docs/claude/demo/ui.md` (menú → demo, formulario de retos fiel a `frmRetos`, invitación aceptar/rechazar, conteo, marcador, resultado, espectadores, ventanas reutilizables y eventos que necesita de Servidor).
- [ ] Ver jugando (con capturas) el tooltip, el cartel ESPÍRITU al morir y el botón Grupo junto a la hotbar (online), a 16:9 y a 4:3. Pedir a QA una prueba automática del clic derecho y del tooltip (V261 no tiene QA).

## 6 · QA y Releases
- [x] Pruebas automáticas aisladas para V261, V267, V268, V269 y V130: `Editor/AOModulesQA270.cs` + `python Tools/test_modules_unity.py` → `MigrationReports/modules_v270.json` (+ captura `modules_v270.png`). Si hay recarga de scripts en Play, protege el guardado, restaura el Input System y corta Play.
- [x] Volver a correr `test_modules_unity.py` después del fix de V267: `modules_v270.json` de las 19:43, `passed: true`, etapa 7, 0 fallas, 0 errores de log (incluye pared e impacto en NPC). Verificado por QA leyendo el informe.
- [x] Demo fase 1: `docs/claude/demo/pruebas.md` rev. 2, alineado con `arquitectura.md`, `red.md`, `retos-reglas.md` y `dungeon-npcs.json`. D-00 (`modelo_progresion.py --check`) = OK.
- [ ] Correr todas las pruebas y armar el informe de estado.
- [ ] Build del cliente + ZIP + copia del 0.25 + registrar el commit.

## Pedidos entre sectores
(formato: `fecha · de → para · archivo · qué hace falta`)
- 24/09 · Programación → Servidor · demo · leer `docs/claude/demo/arquitectura.md` §8 y los Δ: Water deja pasar proyectiles (en la línea de vista es transparente), SpawnA/SpawnB con lados alternados, tema fijo por ring pero se manda igual, `npcLayoutVersion` en el catálogo, ignorar `hp`/`dead` del snapshot durante el reto, enlazar `Runtime/Shared/*.cs` en el csproj, revisar la compra al comerciante (oro). → **Aceptado (Servidor):** `red.md` §12 bis. Compra y venta ya pasan por el servidor; en duelEnd se restauran solo vida, maná, estados y posición.
- 24/09 · Programación → Contenido/Arte/Interfaz/QA · demo · pedidos en `docs/claude/demo/arquitectura.md` §8.
- 24/09 · Programación → Servidor · `AOOnlineClientV240` · exponer `SupportsNpcSpell` como público sin mensaje (o un `CanCastNpcSpell`) para chequear antes de lanzar skill shot. Además: el proyectil es solo local, los otros jugadores no lo ven. → **Hecho (Servidor):** `AOOnlineClientV240.CanCastNpcSpell(spellId)`, público y sin mensaje. Que el proyectil lo vean otros es el punto 3 de la propuesta a Cerebro.
- 24/09 · Programación → Contenido · meditación · ¿en el AO original atacar cuerpo a cuerpo corta la meditación? Hoy `AOPlayerCombatV09` no la corta (solo moverse, castear, morir o maná lleno). → **Respuesta (Contenido):** sí. El server `Protocol.HandleAttack` pone `Meditando=False`, manda `MeditateToggle(char, 0)` y después ataca. El cliente no bloquea el ataque al meditar (solo al descansar), pero sí bloquea usar objetos con clic (`UserItemClick`: `If UserMeditar Then Exit Sub`). → **Hecho (Programación):** atacar corta la meditación (`AOPlayerCombatV09.TryAttack` → `AOPlayerMagicV120.InterruptMeditation`). Objetos: doble clic no hace nada meditando; tecla de consumible de la hotbar usa y corta la meditación (`AOInventoryV10`). → **Respuesta (Contenido):** es **usar** (doble clic en el inventario, menú Usar). Mientras meditás, el cliente lo ignora. La tecla U (`UseItemKey`) no revisa la meditación: el server `UseInvItem`, después de sus chequeos (aturdido, cooldown, intervalo), corta la meditación y usa el objeto.
- 24/09 · Arte → Contenido · FX meditación 153–172 · no están en `Recursos-master/init/FXs.ini` ni `fxs.ind` (llegan a 150). Buscar otra versión o definir el mapeo a las compuestas 122–141 del cliente (`Recursos.bas` LoadComposedFx). Entregar por FX: frames `fx_<id>/frame_NN.png`, `durationMs`, `offsetX/offsetY` y `loopFx` si es compuesta. → **Hecho (Contenido):** 153–172 no son públicos; usar la tabla original con 115–120 (Arte ya tiene los frames). Los 122–141 son "Libros espíritu" (objetos donador), no van por nivel. Datos en `docs/claude/contenido/meditacion_fx_original.json`.
- 24/09 · Arte → Programación · `AOPlayerMagicV120` · cuando existan facciones: `meditationVisual?.Begin(rpg.Level, esCriminal)` (criminal = Caos/Criminal/Concilio). En `ResetRuntimeForLoad` usar `End(true)` para cortar sin la animación de cierre.
- 24/09 · Arte → Servidor · sync de meditación · el FX lo decide `AOMeditationVisualV269.FxForLevel(level, criminal)` (como el server original); los jugadores remotos se muestran con `BeginFx(fx)` / `End()`.
- 24/09 · Interfaz → Programación · `AODeathRespawnV160` OnGUI (l.329) · el cartel "ESPÍRITU" usa píxeles fijos `Rect(320,12,360,56)` y tapa las primeras líneas del chat (`R(16,39…)`) y los FPS. Anclarlo al viewport del juego: `var v=AOActionBarV260.GameCamera.pixelRect; new Rect(v.center.x-180, Screen.height-v.yMax+8, 360, 56)`. → **Hecho (Programación):** anclado al viewport, y los HUD de prueba de `AOTestPlayer`/`AOWorldManagerV07` quedaron en `#if UNITY_EDITOR`. Los HUD de respaldo de combate e inventario siguen (OK de Interfaz). Falta verlo en pantalla. → **Hecho (Programación).** HUD viejos de `AOTestPlayer`/`AOWorldManagerV07` y `Set*ForTesting` → `#if UNITY_EDITOR`. Los de `AOPlayerCombatV09`/`AOInventoryV10` quedan: son el HUD de respaldo si falta la interfaz clásica.
- 24/09 · Interfaz → Servidor · `AOOnlineClientV240` OnGUI (l.421) · el botón "Grupo" en `Rect(10,H-33,130,26)` tapa Q/W/E de la hotbar en pantallas 4:3 (1024×768, 1280×960). Anclarlo a la derecha de la barra: `var b=player.GetComponent<AOActionBarV260>().GetBarRectGUI(); new Rect(b.xMax+8, b.yMax-26, 130, 26)` (y el panel encima de ese punto). → **Hecho (Servidor):** `GroupButtonRect()` lo pone a la derecha de la barra (si no entra, arriba de la barra; si la barra está oculta, en el lugar viejo) y el panel se abre encima. Falta verlo en pantalla a 4:3. → **Hecho (Servidor):** `GroupButtonRect()`. Si la barra está oculta, queda donde estaba; si no hay lugar a la derecha, va arriba de la barra. Falta verlo en Unity.
- 24/09 · Servidor → Programación · `AOSkillShotProjectileV267` + `AOPlayerMagicV120` · (1) en `FinishNpc`, justo antes de `owner.ResolveSkillShotHit`: `AOOnlineClientV240.SetSkillShotFlight(Time.time - launchedAt);` (guardar `launchedAt=Time.time` en `Configure`). (2) Al lado de las 2 llamadas a `AOCastAnimationRuntimeV268.PlayPlayer(gameObject,s)`: `AOOnlineClientV240.NotifyLocalCast(s.id);`. → **Hecho (Programación):** Servidor verificó con grep y compilación (0 errores). → **Hecho (Programación).** Build 0 errores; sin probar en Unity.
- 24/09 · Servidor → Arte · `AOMeditationVisualV269` · en los compañeros remotos el sonido de meditación suena a volumen completo (`spatialBlend=0`) en todo el mapa. ¿Bajarlo según la distancia o silenciarlo en los remotos (por ejemplo, un `BeginFx(fx, sound:false)`)? → **Hecho (Arte):** `BeginFx` baja el volumen de los remotos según la distancia, como el cliente original (−1,2 dB por tile, paneo, mudo fuera de pantalla). `Begin(level)` del jugador local sigue igual.
- 24/09 · Interfaz → Programación · baja prioridad · HUD viejos de prueba (`AOTestPlayer` "Tile/Heading", `AOWorldManagerV07` "Mapa/NPC IA", `AOPlayerCombatV09` y `AOInventoryV10` v0.9/v0.10) solo se ven si falta la interfaz clásica; `SetHealthForTesting`/`SetResourcesForTesting` no se llaman. Sugerido: borrarlos o envolverlos en `#if UNITY_EDITOR`.
- 24/09 · QA → Cerebro · Unity/candado · a las 19:04 una recompilación forzada entró en medio de mi Play. Eso reinicia las variables estáticas, incluida la protección de guardados. Pedido: mientras alguien tiene el candado, nadie refresca ni recompila Unity. Sugerencia para Lucas: Preferencias > General > *Script Changes While Playing* = **Stop Playing and Recompile**. La prueba nueva ya se defiende: si hay recarga en Play, protege y corta Play.
- 24/09 · QA → Programación · `AOSkillShotProjectileV267` · **bug:** el skill shot nunca daña a un NPC. `CheckMapCollision` corre antes que `FindNpcHit` y `ProjectileBlockedBetween` toma como pared la casilla del NPC (`AOInteractionRegistry.IsBlocked`), así que explota a 0,5 del centro (hitRadius 0,38). Caso: Dardo Mágico → Thelma (60,37), HP sin cambio. Lo reproduce `test_modules_unity.py`, etapa 7. → **Hecho (Programación, 19:44):** la colisión del proyectil ya no toma el cuerpo del NPC como pared (`AOGridMap.ProjectileBlockedBetween(..., npcsBlock:false)` + `AOInteractionRegistry.IsBlocked(x,y,npcsBlock)`). El impacto lo resuelve `FindNpcHit` por radio. `test_modules_unity.py`: **PASA** etapa 7, 7 guardados intactos, `aod_log_errors -DesdeUltimoPlay`: sin errores. Respaldo `guardados-20260924-194155`.
- 24/09 · QA → Servidor · `red.md` §3 (libro) · el resto de la división entera del pago ("no se reparte") necesita su propia línea en el libro (caja o descarte). Si no, el invariante de oro de A-01/A-02 no cierra.
- 24/09 · QA → Arte · `arte.md` · rango numérico de densidad de obstáculos por tema (mín–máx %) para G-09.
- 24/09 · QA → Contenido/Cerebro · `progresion.md` · D-02: hasta el nivel 30, Clérigo 13,3 h vs Guerrero 7,2 h (1,85×). Mi criterio propuesto es ≤ 1,5× por tramo: ¿se ajusta el criterio o la distribución de pisos o drops?

## Hecho
- 24/09 · Contenido · demo fase 1: `progresion.md` + `dungeon-npcs.json`. Modelo con las fórmulas del servidor VB6 y datos de npcs.dat/obj.dat/Hechizos.dat/Balance.dat. Diferencias del juego verificadas en el código. Sin tocar Assets/.
- 24/09 · Arte · meditación: tabla original 115–120 aplicada en `meditation_fx.json` (JSON válido; simulación nivel→FX OK). Sin Play.
- 24/09 · Arte · demo fase 1 `docs/claude/demo/arte.md` (análisis por script de los 842 mapas + graficos.ini/obj.dat/Retos.dat). Sin tocar Assets/.
- 24/09 · Programación · demo fase 1: `arquitectura.md` (solo diseño, sin tocar Assets/).
- 24/09 · Programación · fix V267: el skill shot ya daña NPC (antes chocaba con su casilla). `test_modules_unity.py` pasa completo (19:44).
- 24/09 · QA · `AOModulesQA270` (V270) + `test_modules_unity.py`. 3.ª corrida (19:32): **pasan** V261 (intercambiar y mover en la barra, rechazo de tipo equivocado, del inventario y de la lista de hechizos a la barra), V268 (15/15 NPC con 4 direcciones, renderer real, fin de la animación, casteo del jugador), V269 (6 tramos con frames, sonido, aura al meditar, recupera maná, lanzar corta la meditación), V130 (tuning, EOT persistente, buff, vencimiento, ClearAll) y V267 (tuning, lanzar desde la barra: maná, enfriamiento, alcance y pared). **Falla** V267 impacto en NPC (pedido a Programación). Guardados intactos en las 3 corridas. Respaldos: `guardados-20260924-190208`, `-192942` y `-193139`.
- 24/09 · Programación · pedido de Interfaz: cartel ESPÍRITU anclado al viewport (`AODeathRespawnV160`); HUD de prueba (`AOTestPlayer`, `AOWorldManagerV07`) y `Set*ForTesting` solo en el editor. Meditación fiel al usar objetos. Build auxiliar: 0 errores; sin probar en Unity.
- 24/09 · Programación · skill shot online no cobra maná si el servidor no soporta el efecto (`CanCastNpcSpell`). Atacar corta la meditación, como en el original. Build auxiliar: 0 errores.
- 24/09 · Interfaz · tooltip completo de la hotbar + clic derecho para vaciar + auditoría de superposiciones y debug. Build auxiliar: 0 errores. Unity compiló sin errores. `test_controls_unity.py`: PASA (etapa 7), 7 guardados intactos. Respaldo: `Respaldos/guardados-20260924-185643`. Falta verlo jugando.
- 24/09 · Contenido · meditación: 153–172 no son públicos; la tabla original compatible (115–120) y los datos por FX van a `docs/claude/contenido/`. Los 8 audios de hechizo no existen en ninguna fuente pública. Contesté a Programación: atacar corta la meditación. Solo lectura, sin tocar código.
- 24/09 · Arte · `AOMeditationVisualV269` + `meditation_fx.json`: tabla fiel nivel→FX, clips compuestos, offsets y fallback. Build auxiliar: 0 errores. Falta: Unity (recompilar/Play) y frames de 153–172.
- 24/09 · Arte · meditación remota (`BeginFx`): sonido atenuado por distancia y paneo como el original (−1,2 dB/tile, mudo fuera de pantalla). Build auxiliar: 0 errores.
- 24/09 · Programación · revisión V267/V268/V269 en `AOPlayerMagicV120` (revisor-diff + lectura). Fix en `AOSkillShotProjectileV267`. Build auxiliar: 0 errores. Falta probar en Unity.
- 24/09 · Cerebro · respaldo, prueba de controles (pasa), CLAUDE.md, subagentes, skills, sectores.
- 24/09 · Servidor · auditoría del protocolo 2 frente a V267, V268 y V269 (solo lectura, sin cambios en el código).
- 24/09 · Servidor · `CanCastNpcSpell`, botón Grupo junto a la hotbar, enfriamiento de los skill shots desde el lanzamiento, meditación y casteo visibles online (`AOCoopProtocolV250`, `AOOnlineClientV240`, `CoopRoom`, `test_coop_server.py`). Builds de runtime y servidor: 0 errores. `test_coop_server.py` y `test_static_npcs.py`: PASAN. Falta probar en Unity y en una partida real.
