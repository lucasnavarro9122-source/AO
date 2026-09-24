# Retos de la demo: rondas, empate e impuesto

Contenido · 24/09/2026. Fuente: `argentum-online-server-master/Codigo/ModRetos.bas` y `Recursos-master/Dat/Retos.dat` (ao-org AO20).
Marcas: **[original]** = está en el código original; **[no original]** = propuesta nuestra, a aprobar por Lucas.

## 1. Rondas y ganador [original]
- Al mejor de 3. `Puntaje` arranca en 0: +1 si gana el equipo derecho, −1 si gana el izquierdo (`ProcesarRondaGanada`).
- **Se gana una ronda** cuando mueren **todos** los del equipo rival (`MuereEnReto` sale si queda alguno vivo).
- **El reto termina** cuando `Ronda ≥ 3` o `|Puntaje| ≥ 2`: 2–0 termina en la ronda 2 y 1–1 se define en la ronda 3.
- Antes de cada ronda se revive y se cura a todos, y cambian de lado (`iniciarRonda`: `(Ronda + i) Mod 2`). Conteo de 15 s (`TiempoConteo`).
- **Abandono o desconexión** (`AbandonarReto`):
  - Si al equipo le quedan otros jugadores, sigue con uno menos.
  - Si era el último, su equipo **pierde el reto entero** (fuerza `Puntaje = ±123`) y el rival cobra.

## 2. Empate
| Caso | Qué hace el original | Regla para la demo |
|---|---|---|
| Termina con `Puntaje = 0` | **[original]** `FinalizarReto`: cada jugador recibe `pozo neto ÷ jugadores`, o sea, **recupera el 90 % de su apuesta**. Se anuncia "ninguno pudo vencer a su rival". Sin ELO. | Igual. |
| **Tiempo agotado** (600 s, `DuracionMaxima`) | **Está a medias.** `DuracionMaxima` se carga en `TiempoRestante`, pero **nada lo descuenta** y **nadie llama** a `FinalizarReto(Sala, TiempoAgotado:=True)`: en el código público el límite no se aplica. La lógica de `FinalizarReto` sí contempla el caso: decide por `Puntaje` y avisa "se agotó el tiempo". | **[no original, pero con la regla original de `FinalizarReto`]** A los 600 s de empezado el reto (sin contar los conteos), se termina con el puntaje actual. La ronda en curso **no cuenta**. Si va `Puntaje ≠ 0`, gana el que va arriba y cobra todo. Si va 0 (0–0 o 1–1), es empate y cada uno recupera el 90 %. |
| **Doble muerte** (los últimos de los dos equipos mueren en el mismo tic) | **[original, por orden]** No hay caso especial: se procesa una muerte por vez. La primera que se procesa le da la ronda al rival, `iniciarRonda` revive a todos y la segunda muerte ya no cuenta. En la práctica pierde la ronda **el que el servidor procesa primero**. | **[no original, propuesta]** Si en el mismo tic del servidor mueren los últimos vivos de los dos equipos, la **ronda es nula**: no suma puntaje ni cuenta como ronda, y se repite con conteo. Evita que el orden de proceso decida. Si Lucas prefiere la fidelidad total, se usa la regla original (pierde el primero procesado) y QA prueba esa. |

## 3. Impuesto del 10 % [original]
**Va sobre el pozo total, no sobre la ganancia.**
```
pozo      = Apuesta × cantidadDeJugadores            (todos ponen lo mismo)
pozoNeto  = pozo × (1 − ImpuestoApuesta)             (ImpuestoApuesta = 0.1)
ganador   = pozoNeto \ tamañoActualDelEquipoGanador  (división entera: el resto no se reparte)
empate    = pozoNeto \ cantidadDeJugadores
```
- La apuesta se descuenta a todos **al empezar el reto** (`IniciarReto`, `GLD − Apuesta`). No hay devolución si se abandona.
- `tamañoActualDelEquipoGanador` baja si un ganador abandonó antes: los que quedan se reparten el pozo.
- Ejemplos con una apuesta de 10.000:

| Reto | Pozo | Impuesto | Cobra cada ganador | Neto ganador / perdedor | Empate (cada uno) |
|---|---|---|---|---|---|
| 1 vs 1 | 20.000 | 2.000 | 18.000 | +8.000 / −10.000 | 9.000 (−1.000) |
| 2 vs 2 | 40.000 | 4.000 | 18.000 | +8.000 / −10.000 | 9.000 |
| 5 vs 5 | 100.000 | 10.000 | 18.000 | +8.000 / −10.000 | 9.000 |

- Sobre la ganancia neta, el impuesto equivale a un 20 % (2.000 de 10.000). También se cobra en el empate.

## 4. Otros datos de `Retos.dat` / `ModRetos.bas` [original]
- `MaximoEquipo=5`, `ApuestaMinima=10000`, `APUESTA_MAXIMA=100.000.000`, `TiempoConteo=15`, `TiempoGuardarItems=60`.
- `PocionesMaximas`: tope opcional de pociones rojas (obj 38) que se pueden llevar; se controla al crear el reto.
- `CaenItems`: si es verdadero, los perdedores tiran sus ítems en el centro del ring y los ganadores tienen 60 s para juntarlos. En la demo va en **falso** (`demo-arenas.md`).
- ELO: solo cambia si **todos** son nivel ≥ 33 (±10 % del ELO total rival por jugador). En la demo (nivel ≤ 30) **nunca cambia**.

## 5. Pendiente de Lucas (lo pidió Interfaz)
- **Apuesta mínima en la demo.** El original pide 10.000.
- Con la economía estimada en `progresion.md` §8, un personaje de nivel 10–15 junta unos 10.000–20.000 por hora y gasta casi todo en pociones.
- **Propuesta [no original]: mínimo 1.000** en la demo, o 0 para retos amistosos sin apuesta.
