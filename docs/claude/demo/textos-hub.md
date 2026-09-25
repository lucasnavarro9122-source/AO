# Demo AO BATTLESERVER: textos del hub y del dungeon

Contenido · fase 2 · 24/09/2026.
- Estilo del AO original: tuteo ("Escribe", "Bajas"), frases cortas. Sin mensajes de autoguardado.
- Los carteles del original son objetos `ObjType=8` con su `Texto` en `obj.dat`. Donde existe uno que sirve tal cual, se reutiliza (**[original]**); si no, el texto es nuevo (**[nuevo]**).
- Los mensajes de retos (invitación, conteo, resultado) no están acá: son los originales de `ModRetos.bas` que ya lista `ui.md`.

> **Salidas del hub (confirmado por Cerebro, 24/09):** Arenas al **norte**, Dungeon al **sur** (como `arquitectura.md` y `ui.md`).

## 1. Consola al aparecer en el hub (mapa 1000) [nuevo]
Dos líneas (el chat muestra 4), cada vez que se entra a la demo:
1. `Bienvenido a la Demo AO BATTLESERVER.`
2. `Arenas: salida norte · Dungeon: salida sur.`

Modo sin conexión (`ui.md` §1: solo dungeon): en lugar de la línea 2, `Dungeon: salida sur. Las arenas requieren jugar con amigos.`

## 2. Carteles del hub y de la zona de arenas
| Dónde | Objeto (GRH) | Texto |
|---|---|---|
| Salida a las Arenas | gráfico 50902 («Arena I») | [nuevo] `Arenas de retos. De 1 contra 1 hasta 5 contra 5, con apuesta mínima de 1.000 monedas de oro.` |
| Entrada de cada ring (grada) | OBJ 2441, 2442, 2443, 2447, 2448, 2451, 2452 (Arena I…VII) | [original] `Bienvenido a la arena.` |
| Salida al Dungeon y entrada 1010 | gráfico 50925 («Bienvenido al Newbie Dungeon», el de los mapas 37/264) | [nuevo] `Dungeon: siete pisos, de nivel 1 a 30. Cuanto más bajes, más fuertes serán las criaturas.` |
| Cartelera central 19543 | — | La usa Interfaz si muestra el ranking de retos. Sin texto fijo. |

## 3. Carteles de cada piso (en el límite de la zona segura de la entrada)
Gráfico (propuesta de Arte, 24/09): los carteles numerados «Nº1…Nº7» (GRH 50849–50855), en `entrada + (2,1)`. **Solo el gráfico:** los objetos originales que lo usan (OBJ 2406–2412) son carteles de casas y su `Texto` dice «Casa Número N. Propiedad Privada…». El texto al hacer clic tiene que ser el de esta tabla, así que va como objeto o texto propio de la demo, no como el OBJ original. Los tres originales que existen se agregan como decoración junto a la entrada.

| Piso (mapa) | Texto del cartel [nuevo] | Cartel original extra |
|---|---|---|
| P1 Dungeon Newbie (1011) | `Dungeon Newbie · Nivel recomendado: 1 a 4. Serpientes, escorpiones y lobos. Al fondo acecha Wolfang.` | — |
| P2 Catacumbas (1012) | `Catacumbas · Nivel recomendado: 4 a 10. Aquí los muertos no descansan.` | — |
| P3 Mausoleo (1013) | `Mausoleo · Nivel recomendado: 10 a 15. Nadie sale vivo de la cripta del Guardián.` | — |
| P4 Tumba del desierto (1014) | `Tumba del desierto · Nivel recomendado: 15 a 20. Escorpiones y escarabajos custodian a la Momia.` | — |
| P5 Nido de arañas (1015) | `Nido de arañas · Nivel recomendado: 20 a 24. Lleva antídotos: todas envenenan.` | — |
| P6 Cueva de las Gorgonas (1016) | `Cueva de las Gorgonas · Nivel recomendado: 24 a 27. Liches y magos oscuros; la Medusa espera en la orilla del lago. Mejor en grupo.` | — (el de Veriil ya no va) |
| P7 Guarida del Dragón (1017) | `Guarida del Dragón · Nivel recomendado: 27 a 30. Solo un grupo fuerte vencerá a Vytaiz.` | OBJ 1136 [original]: `Bienvenido a Dungeon Dragon.` |

- En P1, P4 y P5 hay NPC que envenenan (`Veneno` en npcs.dat): Serpiente Collet, Escorpión, Serpiente Bicéfala, Escorpión Califa y las cinco arañas más el Mutante.
- El cartel original del Newbie Dungeon (OBJ 2461) **no** se usa: habla de una "zona de entrenamiento con blancos de combate" que en la demo no existe.

## 4. Consola al usar escaleras y portales [nuevo]
| Evento | Texto |
|---|---|
| Entrar al dungeon desde la capilla del cementerio (1010 → 1011) | `Bajas al Dungeon Newbie (niveles 1 a 4).` |
| Bajar al piso siguiente | `Bajas a <nombre del piso> (niveles X a Y).` Por ejemplo: `Bajas al Mausoleo (niveles 10 a 15).` |
| Subir al piso anterior | `Subes a <nombre del piso>.` Desde P1: `Subes a la entrada del dungeon.` |
| Portal de P7 después del jefe | `El portal te devuelve a la plaza de la demo.` |
| Aviso de nivel bajo (opcional) | `Estas criaturas te superan. Nivel recomendado: X a Y.` Solo si el nivel del jugador es menor que el mínimo del piso − 2, una vez por entrada. |

Artículo delante de cada nombre: "al Dungeon Newbie", "a las Catacumbas", "al Mausoleo", "a la Tumba del desierto", "al Nido de arañas", "a la Cueva de las Gorgonas", "a la Guarida del Dragón".

## 5. Muerte dentro de la demo
Se usan los mensajes originales de muerte. El `/HOGAR` original ("Escribe /HOGAR si deseas regresar rápido a tu hogar.") lleva al hub: el hogar de la demo es el mapa 1000 (`arquitectura.md` §2.6). No hace falta texto nuevo.

## 6. Música y luz de los mapas nuevos (para `map_music.json` / `map_environment.json`)
La música es la original de cada piso. **Luz (25/09, regla 8 de `mapa/reglas-y-recorrido.md`):** baja de más clara a más oscura según la profundidad; el P3 conserva su luz propia.

| Mapa | Fuente | Música (`musicId`) | `baseLight` | Nota |
|---|---|---|---|---|
| 1000 Hub | 1 Ullathorpe | 4 | 0 | sin lluvia (la plaza es de la demo) |
| 1001 Arenas | 324 Zona de Ring | 9 | −1 | |
| 1010 Entrada: Cementerio de Nix | 4 | 3 | 11845330 | superficie de noche con luna, sin lluvia; la capilla baja al P1 |
| 1011 P1 Dungeon Newbie | 264 | 7 | 10526880 | |
| 1012 P2 Catacumbas | 40 Catacumbas Ullathorpe | 9 | 9737364 | |
| 1013 P3 Mausoleo | 392 | 9 | −8355670 | la luz propia del Mausoleo |
| 1014 P4 Tumba del desierto | 564 Pirámide | 19 | 9208436 | |
| 1015 P5 Nido de arañas | 291 | 9 | 7765124 | |
| 1016 P6 Cueva de las Gorgonas | 311 | 18 | 6581890 | |
| 1017 P7 Guarida del Dragón | 366 Magma Dungeon | 17 | 8278080 | lava: roja y oscura, lo más profundo |

Las migraciones ya conservan estas entradas ≥ 1000 al volver a correrlas (`music_migration.py`, `map_environment_migration.py`).
