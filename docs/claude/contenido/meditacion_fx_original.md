# Meditación original (AO20): FX y tabla por nivel

Contenido · 24/09/2026. Datos completos por FX (GRH, frames, recortes, PNG): `meditacion_fx_original.json` (misma carpeta).

## FX 153–172: no existen en ninguna fuente pública
- `init/fxs.ind` (lo que carga el cliente) y `init/FXs.ini` tienen `NumFxs=150`, igual en nuestra copia, en GitHub `ao-org/Recursos` master y en la rama `NuevaInterfazBoveda`. Su último cambio es del 05/04/2024.
- `Dat/Meditaciones.dat` (153–172) se subió el 21/05/2026 y el servidor lo usa desde el commit `98f4fd0` (18/12/2025). Ese arte está en los recursos de producción, que no son públicos (el repo no tiene el `OUTPUT` comprimido).
- Con nuestros recursos, el cliente original tampoco podría dibujar el 153–172 (`FxData(1 To 150)`).

## Tablas originales por nivel (servidor ao-org)
| Nivel | Hasta dic-2025 (2020–2025, `e_Meditaciones`) | Desde `e555793` 17/12/2025 (defaults del server) | Hoy (`Meditaciones.dat`) ciudadano / criminal |
|---|---|---|---|
| 1–12 | 115 | 115 | 153 / 154 |
| 13–14 | 115 | 116 | 155 / 156 |
| 15–17 | 116 | 116 | 155 / 156 |
| 18–24 | 116 | 116 | 157 / 158 |
| 25–28 | 117 | 117 | 159 / 160 |
| 29–32 | 117 | 117 | 161 / 162 |
| 33–35 | 117 | 118 | 163 / 164 |
| 36 | 118 | 118 | 163 / 164 |
| 37–44 | 118 | 118 | 165–169 / 166–170 |
| 45–46 | 119 | 118 | 171 / 172 |
| 47+ | 120 | 120 | 120 (los dos bandos) |

- Viejo: `Case 1 To 14 / 15 To 24 / 25 To 35 / 35 To 44 / 45 To 46 / Else`. En VB6 gana el primer `Case`, así que el nivel 35 va al 117.
- Criminal = facción Caos, Criminal o Concilio (`Protocol.HandleMeditate`). Antes de dic-2025 no había diferencia por bando.
- **Recomendado para AoDuels:** la tabla "hasta dic-2025". Es original, estuvo en producción cinco años y usa los seis FX que tenemos (115–120). Sin arte criminal: los dos bandos usan el mismo FX.

## FX de meditación por nivel (115–120)
Todos tienen 10 frames, `Offset 0,0`, loop infinito (`LoadComposedFx`: un clip con `LoopCount=-1`). El frame se calcula como `(NumFrames-1)*progreso+1`, sobre `speed` = duración total del ciclo.

| FX | GRH anim | PNG (`Graficos/`) | Frame (px) | Ciclo (ms) |
|---|---|---|---|---|
| 115 | 3600 | 3448 | 30×40 | 555 |
| 116 | 2640 | 3447 | 40×85 | 555 |
| 117 | 4927 | 3446 | 80×170 | 555 |
| 118 | 6999 | 3445 | 160×170 | 555 |
| 119 | 5865 | 3444 | 140×265 | 555 |
| 120 | 29547 | 3206 | 140×265 | **1111** |

## FX 122–141: "Libros espíritu" (objetos donador, no van por nivel)
- `obj.dat` OBJ 3687–3702 (`ObjType=50`, `Subtipo=4`, `HechizoIndex`=FX). Si el libro está equipado, reemplaza el FX por nivel.
- Compuesta (`AddComposedMetitation`):
  1. clip 1: el FX de inicio, una vez;
  2. clip 2: el FX de loop, infinito;
  3. clip 3: el inicio al revés, a mitad de tiempo (al dejar de meditar).
- Pares inicio→loop:
  - 122, 127, 128, 129 → 126
  - 123, 131, 132, 133 → 130
  - 124, 135, 136, 137 → 134
  - 139, 140, 141 → 138
  - 125 no tiene par: es un FX simple.
- Frames de 96×128, 5 por FX.
  - 122–125: PNG 7106, ciclo de 555 ms.
  - 126–141: PNG 7107, ciclo de 999 ms.
  - Los recortes (x, y) de cada uno están en el JSON.
