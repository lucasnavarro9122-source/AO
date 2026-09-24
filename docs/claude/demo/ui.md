# Demo AO BATTLESERVER · Interfaz (fase 1, solo diseño)

Autor: **AoDuels: Interfaz y Controles** · 24/09/2026 · No toca `Assets/`.

Base original (ao-org AO20): `CODIGO/frmRetos.frm`, `MenuUser.frm`, `ProtocolCmdParse.bas` (`/RETAR`, `/ACEPTAR`, `/CANCELAR`, `/ABANDONAR`), servidor `ModRetos.bas` y `General.bas` (conteo), `Dat/Retos.dat`. Los gráficos están en `Archivos Originales/Recursos-master/Recursos-master/interface/es_*.bmp`.

Marcas: **[O]** = como el original. **[N]** = nuevo, no original: necesita el OK de Lucas.

---

## 0. Qué hacía el original (resumen)
| Paso | Original AO20 |
|---|---|
| Abrir el reto | Botón **Retos** del panel inferior derecho, clic en un jugador → menú de usuario → **Retar**, o `/RETAR nick@nick…` (rellena los nombres). |
| Formulario | Ventana `ventanaretos.bmp`: jugadores por equipo con −/+ (1–5), nombres por equipo, **Apuesta**, casilla **Pociones rojas máx.** (+ campo corto), casilla **Caen ítems**, botón **Retar**, **X** para cerrar y una línea de error roja. |
| Invitación | Solo en la consola, 3 líneas: «Pepe(32) te invita a jugar el siguiente reto:» / «A y B vs C y D. Apuesta: 10.000 monedas de oro.» / «Escribe /ACEPTAR PEPE para participar en el reto.» |
| Aceptar / cancelar | `/ACEPTAR <retador>` · el retador anula con `/CANCELAR`. **No existe "rechazar".** |
| Inicio | «Otorgas X monedas de oro al pozo del reto.» · «¡Ha comenzado el reto!» · «Para admitir la derrota escribe /ABANDONAR.» |
| Conteo | 15 s por ronda (`TiempoConteo=15`). Una línea por segundo en la consola: `>>>  15  <<<` … `>>> YA! <<<`. El jugador queda **congelado** hasta el YA. |
| Rondas | Al mejor de 3. «Comienza la ronda N°1» · «Esta ronda es para Pepe.» |
| Final | Al ganador: «Has ganado X monedas de oro». **A todo el servidor:** «Retos » A venció a B y se quedó con el botín de: X monedas de oro.» Por tiempo (600 s): «Se ha agotado el tiempo del reto.» + «Retos » A vs B. Ninguno pudo vencer a su rival.» |
| Espectadores | No hay ninguna interfaz propia: solo el anuncio global del final. |

**Problema en nuestro cliente:** el chat muestra **4 líneas**, sin colores ni scroll (`AOInterfaceV0101.DrawChat`). Con el conteo original (15 líneas por ronda), la invitación y todo lo demás desaparecerían en segundos. Por eso propongo mover el conteo y el marcador a la pantalla ([N]) y dejar en el chat solo los mensajes clave del original.

---

## 1. Entrar a la demo desde el menú principal
Pantalla **INGRESAR** (`AOMainMenuV140`, panel `Rect(277,326,470,350)`):

```
        INGRESAR
     Elegí cómo jugar
 [   JUGAR SIN CONEXIÓN   ]
 [    JUGAR CON AMIGOS    ]
 [ DEMO AO BATTLESERVER   ]   ← nuevo, mismo estilo entrancePrimary
        [ SALIR ]
```
- Se agranda el panel unos 55 px y los botones bajan (misma grilla de 59 px). El texto de ayuda queda debajo de SALIR.
- **DEMO AO BATTLESERVER** abre un subpanel con la misma estructura que "SALA PRIVADA":
  - Título "DEMO AO BATTLESERVER" y una línea: «Arenas con apuestas y dungeon por niveles».
  - **[ ARENAS Y DUNGEON CON AMIGOS ]** → reutiliza los campos IP de Tailscale y Clave de sala de SALA PRIVADA. El PvP necesita el servidor.
  - **[ SOLO DUNGEON (SIN CONEXIÓN) ]** → modo local. Las arenas quedan cerradas, con el cartel «Las arenas requieren jugar con amigos».
  - **[ VOLVER ]**
- Después va a la **selección de personaje** de siempre y se aparece en el **hub de la demo**. En la consola: «Bienvenido a la Demo AO BATTLESERVER. Arenas: salida norte · Dungeon: salida sur.» (el texto final lo define Contenido).
- **Decisión pendiente (Lucas/Cerebro):** personajes de la demo **separados** (slots o prefijo propio: recomendado, así no se tocan los guardados reales) o los mismos personajes de siempre. Lo implementan Programación y Servidor; la interfaz solo filtra la lista.

---

## 2. Desafiar a un jugador (formulario de retos) [O]
**Ventanas a reutilizar:** el sistema `TopDialog` de `AOInterfaceClassicControlsV200`. Se agrega `TopDialog.Retos`, que ya trae el skin clásico `AOClassicSkinV200`, la escala `R()`, el cierre con Escape y `GUI.depth = -85`. Fondo: `es_ventanaretos.bmp`, importado a `Resources/AOMigrator/ClassicUI/`.

**Formas de abrirlo:**
1. **Botón Retos** del HUD en su lugar original: panel inferior derecho, `R(765,672,36,33)`, a la izquierda de Estadísticas. Es un botón invisible sobre el marco, igual que Hogar y Quest. Si el marco no trae el ícono, se dibuja `es_boton-retos-default/over/off.bmp`.
2. **Menú de usuario** [O, adaptado]: clic izquierdo sobre otro jugador (sin hechizo en modo apuntar) abre el menú de `MenuUser.frm`: *Comerciar · Grupo · Privado · Retar · Denunciar*. En la demo solo quedan activos **Privado** y **Retar**; el resto sale en gris. **Retar** abre el formulario con el nombre ya cargado en el primer rival.
3. **Chat:** `/RETAR`, `/RETO` (abre vacío) y `/RETAR pepe@juan@ana` (rellena en orden). Se agrega en `SubmitChat`, igual que `/hogar`.

**Diseño** (proporciones de `frmRetos`):
```
┌─────────────────── RETOS ─────────────────── [X]┐
│  Jugadores por equipo:  [−]  1  [+]              │
│                                                  │
│   TU EQUIPO                 RIVALES              │
│  [ Lucas (vos)      ]      [ Pepe          ]     │
│  [                  ]      [               ]     │  ← filas visibles según el número
│                                                  │
│  Apuesta: [ 10000        ] monedas de oro        │
│  [✓] Máximo de pociones rojas: [ 5  ]            │
│  [ ] Caen ítems   (desactivado en la demo)       │
│                                                  │
│  <línea de error en rojo>                        │
│                 [   RETAR   ]                    │
└──────────────────────────────────────────────────┘
```
- El primer campo es el propio personaje y no se edita [O]. Orden de envío igual al original: tu equipo y rivales alternados, sin incluir al retador.
- **Casillas:** `es_check-amarillo.bmp`. El campo corto de pociones solo aparece si la casilla está marcada [O].
- **Caen ítems:** en gris y bloqueado con el tooltip «Desactivado en la demo» (supuesto de Cerebro: `CaenItems=false`).
- **Validación local** [O], con los mismos textos que el original:
  - «Faltan jugadores.»
  - «Nombre inválido 'x'.»
  - «Hay jugadores repetidos.»
  - «Cantidad de pociones inválida.»
  - «La apuesta mínima es de 10.000 monedas de oro.»
  - «No tenés oro suficiente.»
- **Ayuda para escribir nombres** [N, opcional]: al enfocar un campo, una lista corta de jugadores conectados (sale de la lista del grupo online) para elegir con un clic.
- **Input:** con un campo enfocado, `InputCaptured = true` (como el chat), así Q/W/E, 1–4 y el movimiento no se disparan. **Escape** cierra; **Enter** no envía, para evitar retos por error.
- **Al enviar** (consola) [O]:
  - «Has enviado una solicitud para el siguiente reto:»
  - «Lucas vs Pepe. Apuesta: 10.000 monedas de oro.»
  - «Escribe /CANCELAR para anular la solicitud.»
  - [N] Además, arriba a la derecha del viewport aparece el aviso **«Reto enviado · esperando 1 jugador · [Cancelar]»**, con el mismo estilo que el punto 3.

**Decisión pendiente:** apuesta mínima en la demo. El original pide 10.000. Si los personajes de la demo arrancan con poco oro, Contenido propone un valor (por ejemplo 0 o 1.000).

---

## 3. Recibir la invitación: aceptar o rechazar
**En la consola [O]:** las 3 líneas originales (invitación, equipos + apuesta + máximo de pociones, «Escribe /ACEPTAR PEPE…»).

**Aviso en pantalla [N]:** reutiliza `es_ventanaconfirmar.bmp` + `es_boton-aceptar-*` / `es_boton-rechazar-*`, en tamaño chico:
```
┌──────────── RETO ────────────┐
│ Pepe (32) te desafía:         │
│ Pepe vs Lucas                 │
│ Apuesta: 10.000 oro           │
│ Máx. 5 pociones rojas         │
│  [ ACEPTAR ]   [ RECHAZAR ]   │
│  Vence en 45 s · (1 de 2)     │
└───────────────────────────────┘
```
- **Ubicación:** arriba a la derecha del viewport del juego: `v = AOActionBarV260.GameCamera.pixelRect`, `x = v.xMax − 310`, `y = Screen.height − v.yMax + 8`. No tapa el centro, ni el chat, ni la hotbar.
- **No es modal:** no bloquea el movimiento ni el combate. Solo responde al mouse (sin teclas rápidas, para no aceptar por error peleando).
- **ACEPTAR** = `/ACEPTAR PEPE` [O]. Si falla, el aviso muestra el error del servidor: «Necesitás al menos 10.000 monedas de oro…», «Tenés demasiadas pociones rojas (máximo 5)».
- **RECHAZAR** [N]: el original no lo tiene. Cierra el aviso y, si Servidor lo implementa, le avisa al retador: «Lucas rechazó el reto.». Si no, solo cierra y la invitación vence sola.
- **Varias invitaciones:** en cola, se muestra una por vez con «(1 de 2)».
- **Después de aceptar:** «Has aceptado el reto de Pepe.» [O], y el aviso pasa a «Esperando a 2 jugadores…» hasta que empiece.
- Si el retador cancela: «El reto ha sido cancelado.» [O] y el aviso se cierra.

---

## 4. Conteo, marcador de rondas y resultado (peleadores)
**Mapa de pantalla** (para que nada se superponga):

| Zona del viewport | Qué va | Cuándo |
|---|---|---|
| Arriba al centro (`y = top + 8`) | **Marcador** | Todo el reto |
| Primer tercio, al centro | **Número del conteo**, luego «¡YA!», «Ronda para Pepe» o «¡VICTORIA!» (uno por vez) | Conteo, fin de ronda, fin del reto |
| Arriba a la derecha | Aviso de invitación o de reto enviado | Solo fuera del reto |
| Chat (4 líneas) | Solo mensajes clave [O] | Siempre |

**Marcador [N]:** panel chico con el skin clásico, semitransparente, de 360×44:
```
  Ronda 2 de 3  ·  Lucas  1 — 0  Pepe  ·  8:32
```
- En equipos: «Equipo A (Lucas, Juan) 1 — 0 Equipo B (Pepe, Ana)», con nombres cortados con «…».
- Nombres en dorado. El reloj cuenta los 600 s máximos.
- Choque detectado: el cartel **ESPÍRITU** (`AODeathRespawnV160`) usa ese mismo lugar. **Pedido a Programación:** no mostrarlo durante un reto (en el ring no hay /HOGAR). En su lugar, el marcador suma una línea: «Caíste · esperá la próxima ronda».

**Conteo:**
- En pantalla [N]: número grande (≈48 px) en dorado con contorno negro y sin fondo, de 15 a 1, y después «¡YA!» durante 1 s.
- En el chat: solo «Comienza la ronda N°2» [O] y «¡YA!» [O], no una línea por segundo.
- Mientras dura el conteo el jugador está congelado [O]. La hotbar muestra sus slots atenuados.

**Fin de ronda:**
- Chat: «Esta ronda es para Pepe.» [O].
- Pantalla: «Ronda para Pepe» durante 2 s y el marcador se actualiza. Después vuelve el conteo.

**Fin del reto:**
- Chat [O]: «Has ganado 18.000 monedas de oro» (al ganador), y el anuncio global del punto 5.
- Pantalla [N], 4 s:
  - **«¡VICTORIA!»** + «+18.000 monedas de oro (impuesto 10 %)»
  - **«DERROTA»** + «Perdiste la apuesta»
  - **«EMPATE»** + «Nadie pudo vencer · apuestas devueltas» (según decida Servidor)
- Abandono: `/ABANDONAR` → «Has abandonado el reto.» [O]. Sin botón de rendirse, para evitar clics por error. El comando figura en el Manual (`TopDialog.Manual`) y en el marcador.
- Al terminar, el jugador vuelve a la grada o al hub (lo define Programación) y se borran el marcador y los avisos.

**Estilo del texto grande:** fuente Cardo (`AOClassicSkinV200`), dorado `#FFD773`, contorno negro de 1 px. Nunca con un rectángulo negro de fondo (misma regla que el texto hablado).

---

## 5. Espectadores
1. **Anuncio en el chat de todos los jugadores de la demo:**
   - Al empezar [N]: «Retos » Sala 3: Lucas vs Pepe por 10.000 monedas de oro.»
   - Al terminar [O]: «Retos » Lucas venció a Pepe y se quedó con el botín de: 10.000 monedas de oro.» / «Retos » Lucas vs Pepe. Ninguno pudo vencer a su rival.»
2. **Cartel sobre cada ring activo [N]:** texto anclado al mundo (se proyecta con `GameCamera.WorldToScreenPoint`), justo arriba del borde superior del ring, solo si el ring está en pantalla:
   ```
   Sala 3 · Lucas vs Pepe · 10.000 oro · Ronda 2 · 1–0 · 8:32
   ```
   - Durante el conteo: «Sala 3 · Lucas vs Pepe · empieza en 12».
   - Sin fondo, texto con contorno, fuente chica. Queda fuera del ring, así no tapa a los peleadores ni sus nombres. Un ring vacío no muestra nada, o «Sala 3 · libre» si Lucas lo prefiere.
3. **Intervenir:** si un espectador intenta dañar a alguien en el ring, en el chat sale «No podés intervenir en un reto.» (la regla es de Programación y Servidor; el texto, de Interfaz).
4. **`/RETOS`** [N, opcional]: lista en el chat los retos activos («Sala 3: Lucas vs Pepe · 10.000 · 1–0»).
5. **Decisión pendiente:** ¿los espectadores también apuestan? No existe en el original. Si Lucas lo quiere, se diseña aparte una ventana «Apostar» en la grada (elegir equipo y monto), con la misma custodia del servidor.

---

## 6. Qué se reutiliza y qué es nuevo
| Pieza | Se reutiliza | Nuevo |
|---|---|---|
| Formulario de retos | `TopDialog` + `AOClassicSkinV200` + `R()` (`AOInterfaceClassicControlsV200`) | `TopDialog.Retos`, gráficos `es_ventanaretos`, `es_boton-retar`, `es_check-amarillo`, `es_campo-corto`, `es_boton-sm-mas/menos` |
| Botón Retos del HUD | Patrón de `DrawOriginalInfoButtons` (Hogar, Quest, Estadísticas) | Botón en `R(765,672,36,33)` |
| Comandos | `SubmitChat` (como `/hogar`) | `/RETAR`, `/RETO`, `/ACEPTAR`, `/CANCELAR`, `/ABANDONAR`, `/RETOS` |
| Mensajes | `AOInterfaceV0101.PushMessage` | Textos originales de `ModRetos.bas` |
| Invitación | `es_ventanaconfirmar` + `es_boton-aceptar/rechazar` | Aviso no modal con cola |
| Menú de usuario | — (no existe en el proyecto) | Menú de `MenuUser.frm` sobre otro jugador |
| Marcador, conteo, resultado | Ancla al viewport (igual que ESPÍRITU y el botón Grupo) | Paneles nuevos |
| Cartel sobre el ring | Idea del texto hablado (`DrawSpeech`) | Texto anclado al mundo |
| Menú principal | Botones `entrancePrimary` y subpanel tipo SALA PRIVADA (`AOMainMenuV140`) | Botón y subpanel DEMO |

Implementación sugerida (fase 2): un solo módulo nuevo de Interfaz, `AODuelUIV2xx`, enganchado como los `partial` de `AOInterfaceV0101`. Toma los gráficos de `Resources/AOMigrator/ClassicUI/` y recibe los eventos del cliente online.

---

## 7. Eventos que la interfaz necesita del cliente o servidor
Acordado con Servidor: `red.md` §8 usa estos nombres como `type` en camelCase. La interfaz **no decide nada**: solo muestra estos eventos y manda estas órdenes.
- **Órdenes:**
  - `duelChallenge{jugadores[], apuesta, maxPocionesRojas | -1}` (`caenItems` no se manda: en la demo siempre es falso)
  - `duelAccept{retador}`
  - `duelCancel`
  - `duelAbandon`
  - [N] `duelReject{retador}`
  - [N] `duelList`
- **Eventos:**
  - `duelInvite{de, nivel, equipoA[], equipoB[], apuesta, maxPociones, venceEnSeg}`
  - `duelInviteClosed{de, motivo: cancelado | rechazado | vencido | iniciado}`
  - `duelWaiting{faltan[]}`
  - `duelStart{sala, equipoA[], equipoB[], apuesta, semilla, duracionMax}`
  - `duelRoundStart{ronda, conteoSeg, horaServidor, aparicion, hp, mana}` (el cliente cuenta solo: no hace falta un paquete por segundo)
  - `duelRoundEnd{ronda, equipoGanador, marcador}`
  - `duelEnd{resultado: victoria | derrota | empate | tiempo | abandono, premio, impuesto}`
  - `duelAnnounce{texto}` (a todos en la demo)
  - `duelRingState{sala, nombres, apuesta, ronda, marcador, restante}` (para los carteles de espectadores; alcanza con mandarlo al cambiar)
- **Errores:** no hay un evento propio. Llegan como `result` con `ok=false` y el texto del servidor tal cual. La interfaz los muestra en la línea de error del formulario, o en el aviso de invitación si vienen de `duelAccept`, y en el chat.
- **Oro:** `wallet` y `bank` llegan con valores absolutos del servidor. La interfaz muestra esos números y nunca suma ni resta la apuesta por su cuenta.
- **Reconexión:** el servidor reenvía `duelInvite`, `duelStart` y `duelRoundStart`. La interfaz rearma el aviso, el marcador y el conteo (lo que falta del conteo se calcula con `horaServidor`) sin repetir los mensajes del chat.
- **`duelEnd` llega por el diario con ack:** también le llega a quien estaba desconectado. Al reconectar se muestra una sola vez el cartel del resultado y la línea del chat («Resultado de tu último reto: …»).

---

## 8. Pendientes y dependencias
- **Lucas decide:**
  - personajes de la demo separados o compartidos;
  - apuesta mínima en la demo;
  - espectadores que apuestan (sí o no);
  - los [N] de este documento: marcador, conteo en pantalla, rechazar, cartel del ring, `/RETOS`, ayuda de nombres.
- **Arte:**
  - minimapas y mapa grande de los mapas ≥ 1000;
  - borde visible del ring y lugar para el cartel;
  - ¿el marco del HUD trae el ícono de Retos?
- **Programación:**
  - ocultar ESPÍRITU durante el reto;
  - congelar al jugador en el conteo;
  - bloquear hechizos y proyectiles de espectadores;
  - atenuar la hotbar durante el conteo (la parte visual la hace Interfaz).
- **Servidor:** los eventos del punto 7; `Reject` y el anuncio de inicio son nuevos.
- **QA:**
  - que el aviso y el formulario no roben teclas (InputCaptured);
  - que nada se superponga a 16:9 y a 4:3 (con la tabla del punto 4);
  - reto completo con 2 clientes.
