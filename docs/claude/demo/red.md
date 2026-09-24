# Demo AO BATTLESERVER: red y servidor

Sector: Servidor y Multiplayer · 24/09 (rev. 2: incorpora `pruebas.md` §2 y §5 de QA y `ui.md` §7 de Interfaz) · **Fase 1: diseño. No hay nada implementado.**
Fuentes: `ModRetos.bas` (+ usos de `EnReto` en `SistemaCombate.bas`, `Modulo_UsUaRiOs.bas`, `modHechizos.bas`, `Protocol.bas`, `TCP.bas`, `Trabajo.bas`), `docs/claude/demo-arenas.md` y el servidor actual (`OnlineServer/CoopRoom.cs`, `Program.cs`).

## Resumen
- El servidor decide todo lo del reto: quién pega, cuánto daño hace, quién muere, las rondas, el tiempo y el oro. El cliente solo pide y muestra.
- **El oro es del servidor** en protocolo 3: billetera y banco por personaje, más un **libro contable** de solo agregar. El oro que manda el cliente en el snapshot se ignora. Así se cierra el agujero de duplicación que encontró QA (A-07), no solo durante los retos.
- Cada operación con oro tiene una clave única (`op`) guardada en el libro, así que se aplica **una sola vez** aunque el mensaje llegue repetido, haya reconexiones o se reinicie el servidor.
- La semilla del generador de arena la elige el servidor. El generador es C# puro y compartido, con PRNG propio y tabla dorada de hashes.
- Espectadores: el estado del mapa (cada 150 ms) más los eventos `duelRingState` y `fx`.
- **Protocolo 3** al publicar la demo. Los clientes de protocolo 2 no pueden entrar (sección 10).

## 1. Original frente a demo

| Aspecto | Original (`ModRetos.bas`) | Demo (propuesta) |
|---|---|---|
| Equipos | `/RETO` con lista `a;b;c…` intercalada (rival, compañero, rival…), de 1 vs 1 a 5 vs 5 | Igual |
| Validaciones | Estar en ciudad segura; no estar en otro reto, torneo, evento, consulta ni cárcel; apuesta entre el mínimo y 100 M; máximo de pociones rojas (obj 38) | Igual, pero "ciudad" pasa a ser el hub o la zona de arenas de la demo |
| Cobro de la apuesta | Se descuenta al **empezar** en la sala (`IniciarReto`), con el oro revisado otra vez | Se **retiene al aceptar** (custodia), como pide la especificación |
| Sala | Una sala libre al azar; si no hay, lista de espera FIFO | Igual |
| Rondas | Al mejor de 3 (`Ronda ≥ 3` o `|Puntaje| ≥ 2`); los lados se alternan cada ronda; al empezar cada ronda se revive y se limpia a todos y hay conteo | Igual |
| Tiempo | 600 s; al agotarse gana quien va arriba y, si van iguales, es empate | Igual |
| Pago | `pozo × (1 − 0,1)` repartido entre los ganadores **conectados** | Igual, pero con el libro: el ganador desconectado cobra igual |
| Empate | `pozo × 0,9` repartido entre todos | Igual (con impuesto también en empate; ver decisiones) |
| Abandono o desconexión | `/ABANDONAR` o desconexión: queda descalificado al instante; si era el último de su equipo, ese equipo pierde | Igual con `/ABANDONAR`. Para desconexiones, **30 s de gracia** (sección 5) |
| Fuego amigo | Solo lo impide el grupo (party) | El servidor lo bloquea siempre |
| `CaenItems` | Opcional, con fase para juntar ítems | Siempre falso: el servidor ignora `caenItems=true` |
| ELO | Solo si todos son nivel 33 o más | Fuera de la demo |
| Bloqueos durante el reto | No se puede invocar, ocultarse, montar ni usar `/regresar` | Igual, y además comerciar y soltar o levantar objetos |

## 2. Estados

**Solicitud**: `Libre → Enviada → (aceptan todos) → EnCola | EnSala`. Pasa a `Cancelada` si el retador cancela, si alguien rechaza, si alguien pierde la condición o si pasan **60 s** sin que acepten todos (el original no tenía vencimiento).

**Sala**: `Libre → Conteo (15 s, quietos) → Pelea → [ronda ganada] → Conteo → … → Fin → Libre`. Al liberarse, arranca el primero de la cola.

## 3. Oro autoritativo: billetera, banco y libro contable

**Cuentas** (todas en el libro):
- `player:<char>` (billetera) y `bank:<char>`;
- `escrow:<retoId>` (custodia);
- `tax` (caja del impuesto);
- `world` (fuente y sumidero: botín de NPC, tiendas, misiones).

**Libro** `Saves/ledger.jsonl`, solo agregar, una línea por movimiento: `{txId, op, retoId, from, to, amount, reason, time}`.
- `txId` es correlativo; `op` es la clave idempotente única.
- Cada línea se escribe con flush a disco **antes** de responder al cliente.
- Al arrancar, el servidor relee el libro y reconstruye los saldos. El libro manda sobre `world.json`.
- Invariante: todas las cuentas suman 0. Para QA: `Σ billeteras + Σ bancos + Σ custodias + caja = −world` (el total que entró al juego).

**Qué cambia en protocolo 3**
- Se ignoran `combat.gold` y el oro del banco que vienen en el snapshot del cliente. Al guardar, el servidor escribe su propio valor en el snapshot (así un servidor 2 podría leerlo si hay que volver atrás).
- `welcome`, `result` y `state` traen `wallet` y `bank` (valores absolutos). El cliente **muestra** esos valores.
- Toda operación con oro pasa por el servidor:
  - Ya pasan hoy: levantar el oro del botín, comprar y vender en las tiendas compartidas.
  - Nuevos: `bank {amount ±}` (depositar o retirar), `questReward {quest}` (el monto sale del catálogo; `op = quest:<char>:<quest>:<vez>`) y la pérdida de oro al morir, cuando Contenido defina esa regla.
  - Programación tiene que pasar al servidor los 2 lugares donde hoy el cliente cambia oro solo: `AOQuestSystemV150` (recompensa) y `AOCityBankV130` (banco). La compra y la venta a comerciantes de ciudad (`AOCityNPCSystemV130`) **ya pasan por el servidor** en línea (`Trade`); las otras líneas son del modo sin conexión. Fuera de línea sigue todo igual que hoy.
- **Apertura**: el primer arranque del servidor 3 crea una línea `world → player` y otra `world → bank` para cada personaje guardado, con los valores de su snapshot. Es la única vez que se confía en el oro del cliente. Antes se hace un respaldo.

**Custodia de un reto**
1. **Retener** (al crear o al aceptar): `player:<char> → escrow:<retoId>` por el monto de la apuesta, con `op = escrow:<retoId>:<char>`. Solo si la billetera alcanza. Un personaje puede tener **una sola custodia abierta** (A-05). En los retos de equipo, si alguien no tiene oro, esa aceptación se rechaza y no se mueve nada (A-04).
2. **Devolver** (cancelación antes de empezar, o reinicio del servidor con un reto abierto): `escrow → player` por el total, sin impuesto (`op = refund:<retoId>:<char>`).
3. **Pagar**, todo con enteros:
   - `pozo = apuesta × n`;
   - `impuesto = pozo / 10`;
   - `premio = pozo − impuesto`;
   - `porGanador = premio / ganadores`.

   El resto de la división va a la caja. Se escribe `escrow → player` para cada ganador (o para todos si hay empate) y `escrow → tax`. La custodia termina en 0. Ejemplo A-01: 1 vs 1 con apuesta de 10.000 → el ganador cobra 18.000 (+8.000 neto) y la caja se lleva 2.000. Los ganadores son los que siguen en el equipo; quien abandonó no cobra, igual que en el original.
4. El resultado del reto y sus líneas del libro se escriben en un solo paso, bajo el mismo lock. No existe un estado "resultado confirmado pero sin pagar": si el servidor se cae antes, el reto sigue abierto y al arrancar se devuelve.

**IDs idempotentes**
- Cada pedido del cliente lleva `request` (GUID), como hoy. Si llega repetido, se devuelve la misma respuesta (caché en memoria).
- Lo que mueve oro, además, usa la `op` semántica guardada en el libro: `escrow:`, `refund:`, `payout:`, `tax:<retoId>`, `pickup:<lootId>`, `buy:<request>`, `quest:`… Un mismo `op` nunca se escribe dos veces, aunque haya reconexiones o reinicios (A-06).
- El `retoId` lo genera el servidor. Si llega repetido un `duelChallenge` con el mismo `request`, devuelve el mismo reto.

## 4. PvP autoritativo

- **Cuándo hay PvP**: solo entre participantes del mismo reto, de equipos contrarios, dentro de su ring y en la fase `Pelea`. En cualquier otro caso sigue la regla de hoy: "no se permite daño entre amigos".
- **Vida y maná del reto en el servidor**:
  - Al empezar cada ronda, el servidor revive y llena a todos (`RevivirYLimpiar`).
  - Durante la pelea, el cliente **muestra** la vida que le manda el servidor (`hurt`, `players[].hp`) y no se aplica daño local.
  - La muerte la decide el servidor (`hp ≤ 0`).
- **Acciones**:
  - Cuerpo a cuerpo: `attack {id: jugador}`, a distancia de 1 casilla más 1 de tolerancia por lag.
  - Hechizo: `cast {id: jugador}`.
  - Skill shot: `cast {id, amount: ms de vuelo}`; el atacante detecta el impacto contra el avatar remoto.
- **El servidor valida**: fase, equipos, ambos vivos, alcance, enfriamientos (`NextAttack` y `NextCast`), maná (en el reto descuenta el costo del catálogo) y estados.
- **Fórmulas**: las de PvP del original (`SistemaCombate.bas`, `modHechizos.bas`) en **un archivo C# compartido sin UnityEngine**, compilado por Unity y por el servidor (como `AOCoopProtocolV250`).
  - Stats: los que el cliente ya reporta más `magicDefense` (nuevo). El servidor los acota.
- **Pociones**: `use {item}`. El servidor revisa el inventario del snapshot, el máximo de pociones y el intervalo; cura la vida del reto y manda `remove`.
- **Estados** (parálisis, inmovilizar, veneno, ceguera…): el servidor los guarda por jugador, como ya hace con los NPC, y los manda en el estado.
- **Movimiento**: en el conteo, la posición queda fija. Una posición fuera del ring se rechaza y el evento `warp` la corrige.

## 5. Desconexiones (matriz A-20 de QA)

Gracia: 30 s (configurable; 0 = original). Mientras corre, el personaje sigue en el ring, quieto, y le pueden pegar. Un espectador que se cae no afecta nada.

| Estado \ evento | Se cae A (o B) | Se caen los dos | Reinicio del servidor | A vuelve dentro de la gracia | A vuelve después |
|---|---|---|---|---|---|
| Invitado (sin custodia) | Si A es el retador, se cancela la solicitud; si es invitado, la invitación sigue abierta hasta vencer | Se cancela | Las solicitudes se pierden (no hay oro en juego) | Puede aceptar si sigue abierta | Igual |
| Aceptado (en custodia, en espera o en cola) | Corre la gracia; la sala no arranca mientras falte alguien | Corre la gracia | Se cancela y se devuelve todo (sin impuesto) | Sigue normal | Se cancela y se devuelve todo |
| Conteo | Corre la gracia; el conteo sigue | Corre la gracia para los dos | Se cancela y se devuelve todo | Vuelve al ring con la vida del servidor | Abandono: si era el último de su equipo, ese equipo pierde y se paga |
| Ronda en curso | Igual que en conteo | Si ninguno de los dos equipos vuelve, fin por marcador (empate si van 0 a 0) | Se cancela y se devuelve todo | Igual que en conteo | Igual que en conteo |
| Entre rondas | Igual que en conteo | Igual que en ronda en curso | Se cancela y se devuelve todo | Igual que en conteo | Igual que en conteo |
| Pagado | No afecta | No afecta | Queda pagado (está en el libro) | Recibe `duelEnd` y su `wallet` | Igual |

Al reconectar, el servidor le **reenvía el estado completo**: sus invitaciones abiertas (`duelInvite`), el reto en curso (`duelStart` y `duelRoundStart` con la hora del servidor) y los resultados que no confirmó (por el diario de eventos).

## 6. Semilla del generador de arena

- El servidor elige la semilla (int32 de `RandomNumberGenerator`) **una vez por reto** y la manda en `duelStart` y en `duelRingState`.
- **Requisitos para el generador** (lo diseña Programación en `arquitectura.md`; QA G-02):
  - C# puro y compartido;
  - sin `UnityEngine`, sin `System.Random` y **sin float**;
  - PRNG propio (SplitMix64 o PCG32) y solo enteros;
  - `GenVersion`.

  El servidor también lo ejecuta, porque necesita los bloqueos para validar posiciones, spawns y línea de vista.
- **Tabla dorada**: un archivo versionado con `(semilla, tema) → hash del layout` para N semillas. Tiene que dar igual en el proyecto de prueba .NET (el runtime del servidor) y en Unity (Mono). Si se cambia el generador, sube `GenVersion` y se regenera la tabla.
- Si un cliente tiene otra `GenVersion`, no puede entrar al reto y, como espectador, ve un aviso.

## 7. Espectadores

- La zona de arenas es un mapa de demo (ID ≥ 1000). El estado del mapa ya les muestra a los que pelean moviéndose.
- `duelRingState` se manda a todos los del mapa **cuando algo cambia** y al entrar al mapa: el cartel de la sala.
- `fx` (efímero, con `seq`): `{kind: hit | miss | spell | shot | death, from, to, spell, damage, x, y, dx, dy}`, para ver golpes, hechizos y proyectiles.
- **No interfieren**:
  - Las casillas del ring cuentan como bloqueadas para quien no participa.
  - El servidor rechaza cualquier acción de un no participante hacia adentro del ring.
  - El borde del ring corta los proyectiles.
- `duelAnnounce` va a todos en la demo.
- La sala admite 11 conexiones. Un 5 vs 5 deja 1 lugar para espectadores (ver decisiones).

## 8. Mensajes (protocolo 3, nombres iguales a `ui.md` §7)

Todos van en `AOCoopMessage` con `type` en camelCase. Los campos nuevos son opcionales.

| Orden de la UI | `type` C→S | Campos |
|---|---|---|
| `Challenge` | `duelChallenge` | `text` = `"nombre;nombre;…"`, `amount` = apuesta, `item` = máximo de pociones (−1 = sin límite); `caenItems` se ignora |
| `Accept` / `Reject` | `duelAccept` / `duelReject` | `name` = retador |
| `Cancel` / `Abandon` | `duelCancel` / `duelAbandon` | — |
| `ListDuels` | `duelList` | → responde con un `duelRingState` por cada sala |

| Evento de la UI | `type` S→C | Campos | Entrega |
|---|---|---|---|
| `DuelInvite` | `duelInvite` | de, nivel, equipoA[], equipoB[], apuesta, maxPociones, venceEnSeg | Directa; se reenvía al reconectar |
| `DuelInviteClosed` | `duelInviteClosed` | de, motivo: cancelado · rechazado · vencido · iniciado | Directa |
| `DuelWaiting` | `duelWaiting` | faltan[] (o "sin sala: en cola") | Directa |
| `DuelStart` | `duelStart` | sala, equipoA[], equipoB[], apuesta, semilla, genVersion, tema, duracionMax | Directa; se reenvía al reconectar |
| `DuelRoundStart` | `duelRoundStart` | ronda, conteoSeg, horaServidor, spawn x/y, hp, mana | Directa; se reenvía al reconectar |
| `DuelRoundEnd` | `duelRoundEnd` | ronda, equipoGanador, marcador | Directa |
| `DuelEnd` | `duelEnd` | resultado: victoria · derrota · empate · tiempo · abandono, premio, impuesto | **Diario con ack** (llega aunque estés desconectado) |
| `DuelAnnounce` | `duelAnnounce` | texto | A todos en la demo |
| `DuelRingState` | `duelRingState` | sala, fase, nombres, apuesta, ronda, marcador, restante, semilla, genVersion | A todos en el mapa, al cambiar |
| `DuelError` | `result` con `ok=false` | texto tal cual | El cliente lo muestra como `DuelError` |

Además: `hurt` (ya existe) ahora también llega desde jugadores; `warp` corrige la posición; `wallet` y `bank` viajan en `welcome`, `result` y `state`. `AOCoopPlayer` suma `arena`, `team`, `magicDefense`, `paralyzed` e `immobile`. `horaServidor` es la hora del servidor en ms; el cliente calcula el desfase en el `welcome` y lo corrige con cada `state`.

## 9. Modo prueba (contrato con QA)

- `--test`: habilita `testLedger` (saldos por cuenta, total e invariante) y rechaza cualquier cliente que no venga de 127.0.0.1. **Nunca** se usa en la sala real.
- `--test-time-scale <f>` (solo junto con `--test`): multiplica los tiempos del reto (60 s de invitación, 15 s de conteo, 600 s de máximo y 30 s de gracia) sin cambiar las reglas. Ejemplo: `0.05` convierte 15 s en 0,75 s.
- Los datos van a `--data` (como hoy en `test_coop_server.py`: puerto, clave y guardados temporales).
- **Cliente en modo prueba por línea de comandos**: `ArgentumOnline.exe --ao-test-server 127.0.0.1:<puerto> --ao-test-key-file <archivo> --ao-test-profile <carpeta>`.
  - Identidad y preferencias en la carpeta de prueba, sin tocar PlayerPrefs.
  - Guardado local protegido.
  - Solo acepta loopback.

  Es del cliente online (Servidor); la ruta del guardado la pone Programación.

## 10. Compatibilidad 2 ↔ 3

| Caso | Qué pasa |
|---|---|
| Cliente 2 → servidor 3 | Se rechaza en el `hello` con "Cliente incompatible. Instalá el ZIP cooperativo nuevo." (el control ya existe). No hay modo mixto: el cliente 2 cambia oro solo y chocaría con la billetera del servidor |
| Cliente 3 → servidor 2 | El servidor viejo lo rechaza con el mismo mensaje. El cliente 3 agrega: "El anfitrión tiene que actualizar el servidor" |
| Guardados | `world.json` suma `retos` como campo opcional; `ledger.jsonl` es nuevo. Antes del primer arranque del servidor 3: respaldo (`aod-respaldo`) y apertura del libro |
| Volver al servidor 2 | Al cerrarse, el servidor 3 devuelve las custodias abiertas y deja el oro de cada snapshot igual al del libro. El servidor 2 carga sin retos |
| 0.26 | Sigue en protocolo 2 (no incluye la demo) |

Un cliente viejo ignora los eventos que no conoce pero igual les da ack. Por eso los eventos de oro y de reto no pueden llegarle nunca, y por eso hay que subir al protocolo 3.

## 11. Pruebas
Las define QA en `pruebas.md` (G-xx, A-01…A-10, A-20, A-30, A-40, R-xx). De este lado se agregan: el invariante del libro después de cada paso, la tabla de la sección 5 como resultado esperado de A-20, y los casos de protocolo 2 ↔ 3 de la sección 10.

## 12. Coordinación

- **Programación:**
  - generador compartido (C# puro, versionado, tabla dorada);
  - fórmulas PvP compartidas;
  - vida del servidor en el cliente durante el reto;
  - skill shots contra avatares remotos;
  - bloqueos del reto;
  - pasar al servidor el oro de misiones y banco (sección 3);
  - ruta del guardado en modo prueba.
- **Contenido**: fórmulas PvP originales; `rewardGold` de las misiones en el catálogo (`export_online_catalog.py`); regla de pérdida de oro al morir; confirmar `Retos.dat`. Además: con apuesta mínima 10.000 y la economía de la alpha, ¿no queda muy alta? (lo decide Lucas).
- **Interfaz**: `ui.md` §7 ya coincide con la sección 8. Los errores llegan como `result` y se muestran como `DuelError`.
- **Arte**: `fx` para espectadores y el proyectil remoto.
- **QA**: las pruebas de `pruebas.md`, con `--test` y `--test-time-scale`.

## 12 bis. Acuerdos con Programación (`arquitectura.md`, marcas Δ)

- **Generador**: `AOArenaGen.Generate(seed, theme)` en `Runtime/Shared/`, SplitMix64, `GenVersion = 1`, 23×19. Las celdas son `Floor`, `Solid`, `Water` y `Deco`.
  - El agua **no se camina pero deja pasar proyectiles**. El servidor valida la línea de vista y los skill shots con el `ArenaLayout.SegmentClear` compartido.
- **Lados**: `SpawnA` y `SpawnB` traen 5 posiciones cada uno (de 1 vs 1 a 5 vs 5). En las rondas pares, el equipo A usa `SpawnB`. No se regenera el layout.
- **Tema**: fijo por ring (el piso está horneado en el mapa 1001). Igual se manda en `duelStart` y `duelRingState`. La semilla sigue siendo una por reto.
- **Mapas de demo**: IDs 1000–1999, como máximo 100×100 (el servidor indexa `y*101+x`).
- **Compilación**: `AOOnlineServer.csproj` enlaza `../Assets/AOMigrator/Runtime/Shared/*.cs`. Si alguien mete `UnityEngine` ahí, el servidor no compila, y eso sirve de control.
- **Servidor** (pedidos de Programación que acepto):
  - `npcLayoutVersion` por mapa en el catálogo (`export_online_catalog.py`). Si no coincide con el de `world.json`, se reinician los NPC de ese mapa, para no heredar estados con IDs desplazados.
  - Durante el reto se ignoran `hp` y `dead` del snapshot del cliente.
  - `attack` y `cast` con `{id: jugador}`; las posiciones y los ids de los avatares enemigos ya viajan en `state.players[]`.
- **Riesgo al terminar el reto**: el cliente vuelve a su estado anterior en `duelEnd`, pero **solo** en vida, maná, estados y posición (como `RevivirYLimpiar` + `DevolverPosAnterior`). **Nunca** en inventario, oro ni experiencia: si restaurara el snapshot completo, las pociones usadas en el reto volverían y se duplicarían objetos. El servidor guarda la posición y la vida previas al reto y las manda en `duelEnd`.

## 13. Decisiones para Lucas

| # | Pregunta | Recomendación |
|---|---|---|
| 1 | ¿Retener la apuesta al aceptar o al empezar, como el original? | Al aceptar |
| 2 | Desconexión: ¿30 s de gracia o descalificación inmediata, como el original? | 30 s |
| 3 | ¿Semilla por reto o por ronda? | Por reto |
| 4 | ¿Impuesto del 10 % también en empate, como el original? | Sí |
| 5 | ¿Subir el límite de 11 conexiones para tener más espectadores? | Según cuántos amigos jueguen |
| 6 | ¿Protocolo 3 con oro del servidor en todo el juego? Es más trabajo (misiones, banco, venta), pero sin eso se puede duplicar oro y apostarlo | Sí |

## Anexo: auditoría V267–V269 (hecha el 24/09)

- **V267 (skill shots)**: el daño ya funcionaba en línea con `cast`.
  - Estaba roto el enfriamiento: el cliente lo contaba desde el lanzamiento y el servidor desde el impacto. **Arreglado**: `amount` lleva los ms de vuelo, con tope de 1500.
  - Además: `CanCastNpcSpell` evita que se cobre maná si el servidor no admite el efecto.
- **V268 (casteo)** y **V269 (meditación)**: son visuales; el maná se regenera en el cliente.
  - **Hecho**: `AOCoopPlayer.meditationFx/castSpell/castSeq`, con avatares remotos que muestran el aura (el sonido baja con la distancia, a cargo de Arte) y la animación de casteo.
- Todo sigue en protocolo 2. `test_coop_server.py` y `test_static_npcs.py` pasan. Falta probarlo en Unity y en una partida real.
- Pendiente: que los compañeros vean el proyectil del skill shot. Se retoma con los `fx` de la demo.
