# Demo AO BATTLESERVER: plan de pruebas (QA, fase 1)

Rev. 2 (24/09). Alineado con `arquitectura.md` (AOArenaGen), `red.md` (libro contable, `--test`, modo prueba), `retos-reglas.md` y `dungeon-npcs.json` + `modelo_progresion.py` (Contenido), y con `decisiones.md`. Es solo diseño; los nombres de pruebas nuevas son propuestas.

## 0. Reglas comunes a todas las pruebas
- **Guardados:** antes de cualquier prueba en Unity o con el servidor, `aod-respaldo`. Nunca personajes de Lucas: fixtures propios (`DemoQA_*`), `TestPrefixOverride` y `BeginIsolatedTest`. Se verifican con hash **`AO_Demo/` y `AO_BattleDemo/`**: la demo no toca nunca las partidas reales (decisión 3).
- **Servidor aislado:** `dotnet AOOnlineServer.dll --test [--test-time-scale f] --port <libre> --data <temp> --key-file <temp> --catalog <temp>`. `--test` solo acepta 127.0.0.1 y habilita `testLedger`. Nunca la sala real, `Release/Server/Saves` ni `room-key.txt`.
- **Cliente compilado en modo prueba:** `ArgentumOnline.exe --ao-test-server 127.0.0.1:<puerto> --ao-test-key-file <archivo> --ao-test-profile <carpeta>` (`red.md`). Cada instancia usa su propio perfil temporal.
- **Unity:** tomar el candado. Mientras dure la prueba, nadie guarda en `Assets/**`, crea marcadores en `Temp/` ni fuerza un refresh (`sectores.md`). Si igual hay una recarga en Play, la prueba protege el guardado, restaura el Input System y corta Play (patrón de `AOModulesQA270`).
- **Resultados:** `MigrationReports/demo_<parte>.json` (`passed`, `failures[]`, `notes[]`, métricas) y código de salida ≠ 0 si falla.
- **Lanzador único:** `Tools/test_demo_all.py` corre de lo más rápido a lo más lento y corta en la primera capa que falla: D-00 → generador → apuestas → reto con bots → reto en Unity → simulación.
- **Regresión:** además deben seguir pasando `test_controls_unity.py`, `test_modules_unity.py`, `test_coop_server.py`, `test_coop_unity.py` y `test_static_npcs.py`.
- IDs G-xx, A-xx, R-xx y D-xx para seguir cada caso en el tablero.

## 1. Generador de arena (`Runtime/Shared/AOArenaGenV###.cs`)
Base: C# puro, SplitMix64, sin float, `GenVersion`, simetría `0 = rot180 / 1 = espejo`, 8 intentos y después un layout de respaldo marcado `Attempt = 255`, más `Serialize()` ("AOAG…").

### Automáticas (proyecto de prueba .NET que enlaza el mismo `.cs`; segundos)
| ID | Qué se comprueba | Criterio |
|---|---|---|
| G-01 | Determinismo | Misma `(seed, theme)` → mismos bytes de `Serialize()`, en el mismo proceso y en procesos distintos. |
| G-02 | Igual en servidor y cliente | Tabla dorada: 50 semillas × tema → SHA-256 de `Serialize()`. La validan la prueba .NET **y** una prueba de Unity (Mono). Si se cambia el generador sin subir `GenVersion`, falla. |
| G-03 | Simetría | Según el `Symmetry` del layout, cada celda (tipo, variante y decoración) es igual a su imagen. Los spawns de B son la imagen de los de A. |
| G-04 | Caminos | BFS con el validador compartido (las reglas de `AOGridMap`): cada spawn llega a todos los rivales y no hay celdas caminables aisladas. |
| G-05 | Sin cuello de botella único | ≥ 2 caminos disjuntos entre los lados (min-cut ≥ 2). |
| G-06 | Spawns | Caminables, distintos, con ≥ 2 vecinos libres y simétricos, para 1v1 a 5v5 (orden `(2,9),(2,7),(2,11),(2,5),(2,13)`). |
| G-07 | Borde | El perímetro queda cerrado salvo las puertas. Ningún obstáculo tapa una puerta ni un spawn. Las cuerdas no tapan la vista pero bloquean el paso y los proyectiles (decisión 14). |
| G-08 | Justicia | Camino más corto spawn→rival y cobertura cerca de cada spawn iguales para los dos equipos. |
| G-09 | Densidad | % de obstáculos sobre las 437 celdas del interior, dentro del rango del tema: Bosque 10–18, Desierto 8–14, Nieve 8–15, Mazmorra 12–20, Pantano 10–18, Ciudad 8–14. Fuente de verdad: campo `densidad` de `StreamingAssets/AOMigrator/ArenaGen/arena_palette.json`; la prueba lee el JSON, no copia los números. |
| G-10 | Variedad | Con 10.000 semillas por tema, ≥ 99 % de layouts distintos. Ninguna celda interior no fija está bloqueada el 100 % de las veces. |
| G-11 | Tema válido | Paleta: `python Tools/demo_arena_palette_check.py` (de Arte; sale con 1 si falla) verifica los 6 temas, cada GRH con su textura, los sets de piso y agua y los límites de sprite. 24/09: **OK**. Generador: cada tile, objeto o variante que usa un layout está en la paleta de su tema. |
| G-12 | Semillas límite | 0, 1, negativas, `int.MaxValue`, `int.MinValue`: layout válido, sin excepción ni bucle. |
| G-13 | Respaldo y rendimiento | Uso del layout de respaldo (`Attempt = 255`) ≤ 0,1 % de las semillas (reportado por tema); ≤ 5 ms por arena con reintentos. |

Corrida completa: 10.000 semillas por tema. Rápida (antes de cada cambio en el generador): 500. Además, una hoja con 24 semillas por tema (`MigrationReports/demo_arenas_contact.png`) para que Arte y Lucas la miren.

### En Unity (`AODemoArenaQA`, Play aislado)
| ID | Qué se comprueba |
|---|---|
| G-20 | Con 3 semillas en un ring del mapa de arenas, las banderas de `AOGridMap` coinciden celda por celda con el layout. |
| G-21 | `FindPath` entre spawns existe y rodea los obstáculos. |
| G-22 | Un skill shot choca contra un pilar. El borde del ring frena un proyectil lanzado desde afuera (caso de pared de `AOModulesQA270`). |
| G-23 | Una captura por tema. |

## 2. Apuestas y oro (el oro nunca se duplica ni se pierde)
Base (`red.md` §3): en protocolo 3 el oro es del servidor; el libro `Saves/ledger.jsonl` es de solo agregar; cada movimiento tiene una `op` idempotente (`escrow:`, `refund:`, `payout:`, `tax:`, `pickup:`, `buy:`, `quest:`…); resultado y libro se escriben juntos. Reglas: `retos-reglas.md`.

### Invariante revisado después de CADA paso (con `testLedger`)
`Σ billeteras + Σ bancos + Σ custodias abiertas + caja (impuesto + resto de la división) = total inicial + oro creado por el juego (drops y misiones, cada uno con su op)`. Ningún saldo negativo. Custodias de retos terminados = 0. Al reiniciar el servidor, los saldos que reconstruye desde el libro son iguales a los de antes.
- **Pendiente con Servidor:** el resto de la división entera ("no se reparte") tiene que tener su propia línea en el libro (a la caja o a una cuenta de descarte). Si no, el invariante no cierra.

### Automáticas (`Tools/test_bets_server.py`, bots por protocolo como `Peer` de `test_coop_server.py`)
| ID | Caso | Esperado |
|---|---|---|
| A-01 | 1v1, apuesta 10.000, gana A | Pozo 20.000, impuesto 2.000. A cobra 18.000 (+8.000 neto), B −10.000, caja +2.000. |
| A-02 | 2v2 y 5v5; montos que no dividen exacto | Cada ganador cobra `pozoNeto \ ganadores` (18.000 con 10.000). El resto va a su línea del libro. |
| A-03 | Límites | Mínimo según la decisión 4 (1.000 propuesto; el original es 10.000). Apuesta 0 = amistoso, sin movimientos en el libro. Máximo 100.000.000. Se rechazan negativos, más que el saldo y cerca de `int.MaxValue`, sin mover nada. |
| A-04 | Equipo sin fondos | Si a alguno de un 5v5 no le alcanza, nadie queda en custodia (todo o nada). La custodia empieza al aceptar (decisión 6). |
| A-05 | Doble gasto | El mismo jugador acepta 2 retos a la vez (mensajes seguidos o 2 sockets con la misma identidad): solo uno toma custodia. |
| A-06 | Mensajes repetidos | Reenviar aceptar, fin de ronda o cobro, y reconectar a mitad de camino: cada `op` una sola vez. |
| A-07 | Cliente tramposo | Un `snapshot` con más oro o con el oro de antes de apostar se ignora, dentro y fuera del reto. Al reconectar el cliente recibe su `wallet` del servidor. |
| A-08 | Cancelar | Cancelar antes de arrancar (invitación o espera): devolución completa sin impuesto. |
| A-09 | Empate | Termina con `Puntaje = 0`: cada uno recupera el 90 % (9.000 de 10.000). Se cobra impuesto. |
| A-10 | Tiempo agotado | Con `--test-time-scale`, a los 600 s de reto (sin contar los conteos) termina con el puntaje actual y la ronda en curso no cuenta. `≠ 0`: gana el que va arriba y cobra todo. `0`: empate como A-09. |
| A-11 | Doble muerte en el mismo tic | Según la decisión 10: ronda nula y se repite con conteo (propuesta), o pierde el primero procesado (original). Se prueba la regla elegida. |
| A-12 | Abandono | `/ABANDONAR` o gracia vencida: si le quedan compañeros, el equipo sigue con uno menos; si era el último, su equipo pierde todo el reto y los ganadores que quedan se reparten el pozo. Sin devolución para el que abandona. |
| A-13 | Espectador | No puede apostar ni entrar a un reto ya armado (decisión 5). |
| A-14 | Extras del original | El ELO no cambia con niveles ≤ 30. `PocionesMaximas` (si se habilita) se controla al crear el reto. `CaenItems = false`: inventario igual antes y después. |
| A-15 | 4 rings a la vez | 4 retos simultáneos + cola (decisión 15), con apuestas cruzadas: el invariante global se mantiene. |
| A-16 | Oro fuera de los retos | Comprar, vender, banco, levantar oro del piso y cobrar misiones por el libro: idempotentes y sin duplicar al reconectar (el oro del servidor es para todo el juego, decisión 11). |

### Matriz de desconexiones (A-20)
Resultado esperado: la tabla de `red.md` §5. Estados: invitado · aceptado (en espera o en cola) · conteo · ronda · entre rondas · pagado. No existe "confirmado sin pagar": resultado y libro se escriben juntos. Eventos: se cae A · se caen los dos · se cae el espectador · reinicio del servidor (matar el proceso) · vuelve dentro de los 30 s de gracia · vuelve después. Por cada celda se revisan el invariante, el estado del reto y lo que recibe cada cliente al volver (`duelEnd`, `wallet`, vida del servidor).

### Prueba al azar contra un modelo (A-30)
Un modelo en Python aplica las reglas de `retos-reglas.md`. Se corren 10.000 secuencias al azar (retar, aceptar, cancelar, ganar ronda, doble muerte, abandonar, desconectar, reconectar, reiniciar, reenviar, comprar o vender). Después de cada paso, el estado del servidor tiene que coincidir con el modelo. Si falla, se guarda la semilla para reproducirlo.

### Red mala (A-40)
Un proxy TCP de prueba agrega 150 ms de latencia, pausas de 3 s y cortes a mitad de mensaje. Se repiten A-01, A-05, A-06 y A-12 a través del proxy.

## 3. Reto completo: 2 clientes aislados + 1 espectador
| Capa | Quién juega | Qué cubre | Cuándo |
|---|---|---|---|
| R-1 `test_duel_server.py` | 3 bots: A, B y espectador | Flujo completo del servidor | Cada cambio de red o reto |
| R-2 `test_duel_unity.py` | Editor = A; bots = B y espectador | Pantallas de `ui.md`, conteo, marcador, arena según la semilla, daño PvP | Cada cambio de cliente |
| R-3 | Editor = espectador; 2 bots pelean | Anuncio, vista desde afuera, no poder interferir | Cada cambio de cliente |
| R-4 humo manual | 2 clientes compilados en modo prueba + editor como espectador; servidor `--test` en la misma PC | Todo junto, con lista de pasos | Antes del ZIP |
| R-5 partida real | Lucas + amigos por Tailscale en una **sala de prueba** (otra `--data`, otra clave, sin `--test`) | Latencia y uso reales | Antes de anunciar la demo |

| ID | Paso | Esperado |
|---|---|---|
| R-01 | Entrar desde "Demo AO BATTLESERVER" | Aparece en el hub (mapa ≥ 1000) con su personaje de `AO_BattleDemo/`. `AO_Demo/` no cambia. |
| R-02 | A reta a B con apuesta | B recibe monto y equipo; al aceptar hay custodia (invariante). |
| R-03 | Semilla | Los 3 reciben la misma `seed + GenVersion + tema`, que se mantiene en todas las rondas (una por reto, decisión 8). El hash de `Serialize()` de cada cliente es igual al del servidor. El cliente no puede elegir la semilla. |
| R-04 | `GenVersion` distinta | Ese cliente no puede pelear; como espectador ve el aviso. |
| R-05 | Conteo de 15 s | Lo ven los 3. Moverse, atacar o lanzar durante el conteo se rechaza en el **servidor**. |
| R-06 | PvP | Solo dentro del ring y contra rivales del reto. Pegarle a un compañero o a alguien de afuera se rechaza, igual que el daño fuera del ring. Daño con `AOPvpFormulas` compartidas: el mismo resultado en servidor y cliente con el mismo RNG. |
| R-07 | Espectador | No entra al ring; sus hechizos y proyectiles chocan con el borde. Ve la pelea y el anuncio (quién pelea y cuánto se apuesta). |
| R-08 | Rondas | Al mejor de 3 (termina con 2–0 o en la ronda 3). Antes de cada ronda se revive, se cura y se cambia de lado. El marcador coincide en los 3. |
| R-09 | Muerte en el ring | No se caen ítems; inventario igual. |
| R-10 | Fin | Paga según A-01/A-02; los peleadores salen del ring; resultado anunciado. |
| R-11 | Tiempo agotado y empate | A-09/A-10 con clientes reales y `--test-time-scale`. |
| R-12 | Desconexión en plena pelea | Casos elegidos de A-20 con clientes reales (cerrar la ventana y matar el proceso). |
| R-13 | Versiones | Cliente 0.25 (protocolo 2) contra el servidor 3, y cliente nuevo contra el servidor 2: mensaje claro, sin excepción y sin tocar guardados (`red.md` §10). |
| R-14 | Volver al servidor 2 | Al cerrar el servidor 3 con custodias abiertas, se devuelven y el oro de cada snapshot queda igual al del libro; el servidor 2 carga sin retos. |

Capturas automáticas en R-2/R-3 (reto, conteo, ronda y resultado) a 16:9 y 4:3.

## 4. Progresión del dungeon (mapas 1011–1017)
Entradas de Contenido: `dungeon-npcs.json` (tabla de EXP, 7 pisos, NPC con stats completas, zonas, cantidad, respawn, `tiempoPorNivel`, `supuestosModelo`) y `modelo_progresion.py` (determinista, fórmulas del servidor VB6).

| ID | Qué | Criterio |
|---|---|---|
| D-00 | `python docs/claude/demo/modelo_progresion.py --check` | Reproduce `tiempoPorNivel` ("check: OK"). Última corrida, 24/09 22:45 (equipo tope = el que venden los comerciantes del hub): **OK**. Hasta el nivel 30: Guerrero 7,4 h, Mago 11,1 h, Clérigo 12,1 h y Cazador 8,3 h (antes 7,2 / 10,9 / 13,3 / 8,0). Si cambian los datos, se vuelve a correr. |
| D-01 | Monte Carlo (`Tools/sim_dungeon.py`, 1.000 corridas por clase y tramo, semilla fija), con las mismas fórmulas y supuestos | p50 de minutos por nivel dentro de ±30 % de `tiempoPorNivel`; se reportan p90 y la dispersión. |
| D-02 | Diferencia entre clases | Por tramo, la más lenta tarda **≤ 2×** la más rápida (Cerebro, 24/09: el balance de clases no se toca). Hoy el total da 12,1 h / 7,4 h = 1,64× (Clérigo vs Guerrero): se reporta como **característica del AO original**, no como falla. Ninguna clase queda trabada (0 kills posibles). |
| D-03 | "No demasiado fácil" | Al nivel del piso: vida perdida promedio por pelea ≥ 25 %; muertes por hora entre 0,2 y 2. |
| D-04 | "Farmeo cómodo" | Caminar ≤ 25 % del tiempo; esperar respawns ≤ 15 % con 2 jugadores. Se simula con 1, 2 y 5 jugadores por piso. |
| D-05 | Orden de dificultad | EXP por hora y daño recibido suben piso a piso; ningún NPC de un piso supera a los del siguiente. |
| D-06 | Economía | Con `OroMult` ×2 (decisión 2), el oro por hora alcanza para las pociones de cada tramo y cualquier clase puede equiparse. |
| D-07 | Configuración | Multiplicadores de EXP por tramo (decisión 1) y `OroMult`: los mismos valores en el servidor, el cliente y `dungeon-npcs.json`. |
| D-10 | Calibración contra Unity | Un bot en Play aislado pelea K NPC por piso con un fixture de clase y nivel fijos, y mide tiempo por kill y vida perdida. Tiene que dar ±20 % del modelo. |
| D-20 | Auditoría de los pisos | Todo piso se alcanza desde el hub y toda salida tiene su vuelta. El punto de aparición de cada piso es `mapa.llegadaDesdeArriba` (no `entrada`, que es la escalera de subida): tiene que ser caminable y no estar sobre una salida. Los spawns de NPC son caminables y están en su zona, y el respawn funciona (estilo `AOMapFullAudit`). |
| D-21 | `npcLayoutVersion` | Cambiar la lista de NPC de un piso sube la versión y el servidor reinicia esos NPC en vez de enlazarlos mal (`arquitectura.md` §2.5). |

**D-10 y las diferencias conocidas (decisión 17).** El juego hoy:
- ataca cada 0,75 s en vez de 1,165 s, y solo a la casilla de enfrente;
- dispara el arco sin distancia;
- no regenera vida;
- tiene un respawn de NPC por defecto de 0,35 s.

Contenido avisa que el cuerpo a cuerpo va a dar ~35 % más rápido que el modelo. **El simulador no se ajusta a esas diferencias.** Mientras Programación no las corrija (con el OK de Lucas), D-10 las reporta como "divergencia conocida" con su tamaño medido, y el resto tiene que quedar dentro de ±20 %. Después de la corrección, todo dentro de ±20 %: así D-10 sirve también para verificar el arreglo de fidelidad.

## 5. Contrato de pruebas: estado
| Sector | Necesario para probar | Estado |
|---|---|---|
| Programación | Generador puro y compartido + validador + `Serialize()`; enganche para cargar un layout en Play; `AOPvpFormulas` con RNG inyectable | Diseñado (`arquitectura.md`) |
| Servidor | Libro + `op`; `--test`/`testLedger`; `--test-time-scale`; oro autoritativo; tabla de desconexiones; protocolo 2↔3; cliente en modo prueba | Diseñado (`red.md`). **Falta:** la línea del libro para el resto de la división (§2). |
| Contenido | Datos del dungeon, modelo reproducible, reglas de retos | Entregado. D-00 = OK |
| Interfaz | Nombres o estados de las pantallas para verificarlas sin clics de píxel | Nombres en `ui.md` (tomados por `red.md`) |
| Arte | Set de tiles y objetos por tema con GRH (G-11) y límites de densidad (G-09) | Entregado: `arena_palette.json` + `demo_arena_palette_check.py` (OK) |

## 6. Orden en la fase 2
1. D-00 (ya pasa) y G-01…G-13 en cuanto exista `AOArenaGen` (no necesita Unity ni red).
2. A-01…A-16 y A-20 junto con el libro y la custodia, **antes** que la UI de apuestas. Después A-30 y A-40.
3. R-1 (bots), luego R-2/R-3 (Unity), luego R-4/R-5.
4. D-01…D-07 con los datos actuales; D-10 y D-20/D-21 cuando existan los pisos.
5. Build de la demo solo con todo en verde y los guardados intactos.

## Riesgos que ya vimos
- Una recarga de scripts en Play reinicia la protección de guardados y puede romper el Input System (pasó el 24/09): candado + autodefensa de las pruebas.
- Hoy el `snapshot` del cliente trae el oro: sin el oro autoritativo del protocolo 3, las apuestas permiten duplicar oro (A-07, A-16).
- Mono (Unity) y .NET tienen que generar lo mismo: G-02 lo vigila con la tabla dorada.
- Diferencias de combate con el original (decisión 17): el tiempo real de farmeo puede apartarse del modelo hasta que se corrijan (D-10).
