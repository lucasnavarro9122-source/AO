# Lotes de Higgsfield (créditos y resultado)

Saldo de referencia: 960,61 antes de la ronda 1 (la nube ya había gastado 40,25).

| Fecha | Lote | Modelo | Imágenes | Créditos | Resultado |
|---|---|---|---|---|---|
| 25/09 | Zonas HD ronda 1 (Ullathorpe + mapas 2, 5, 8, 11) | `seedream_v5_pro` 2k 3:2 | 9 | **22,5** (saldo 938,11) | 21 piezas aceptadas, 31 pendientes. Con `--igualar-color`: 32. Sin aplicar. |
| 25/09 | Zonas HD ronda 2 (las 31 pendientes) | `seedream_v5_pro` 2k 3:2 `is_inpaint` | 6 (1 de prueba + 5) | **15** (saldo 923,11) | 31/31 pasan la puerta; 13 dudosas a ojo. Sin aplicar. |

## Zonas HD ronda 1 (25/09)
- OK directo de Lucas en el chat Higgsfield. Tope de Cerebro: 25.
- Entrada: `zonas-preparar --ronda 1` desde `docs/claude/nube/hd/zonas/zonas_pendientes.json` (manifiesto igual al de la nube). 52 piezas en 9 hojas 3×2.
- Referencias: la hoja de entrada + las 3 de Lucas (`docs/claude/nube/hd/referencias/`). Sin la hoja aprobada. Prompt: `ZONE_PROMPT`.
- Jobs: `docs/claude/nube/hd/zonas/jobs_r1.json`. Salida 2496×1664 por hoja.
- Todo en `../AO_HD/ullathorpe_zonas/` (fuera del repo, 73 MB):
  - `generadas_r1/r1_hoja_NN.png` (lo que devolvió Higgsfield);
  - `atlas_hd/tex_5087.png`, `tex_6215.png` (simulación con las 21 aceptadas);
  - `zonas_pendientes.json` (31 para la ronda 2);
  - `r1_comparacion.png` (entrada | generada, rechazos en rojo).
- `zonas-importar --ronda 1` **sin** `--aplicar`: 21 aceptadas (2.190 de 5.569 usos de la ronda), 31 rechazadas.
- Por qué se rechazan:
  1. **Negro inventado** (hojas 00–04, 07 y parte de 05): Seedream pinta bloques o escalones negros sobre pasto que en la entrada no tiene negro. Pasó lo mismo en la ronda 0 aunque ya no va la hoja aprobada.
  2. **Color** (hojas 05 y 06, variantes de pasto `tex_6002`–`6004`): se ven bien pero más azuladas y oscuras que el original (color 0,17–0,29, tope 0,19).
- Ideas para la ronda 2 (sin gastar hasta que Lucas diga):
  - Rellenar los huecos vacíos de la entrada con gris neutro en vez de negro y sacar "solid black empty slots stay solid black" del prompt cuando la hoja no tiene huecos.
  - Probar `is_inpaint: true` de Seedream para anclar la composición.
  - Probar primero 1 hoja (2,5 créditos) antes de las 6 (~15 créditos).
  - Color: que Arte decida si acepta las de las hojas 05 y 06 como están (no cuesta créditos).

## Zonas HD ronda 2 (25/09)
- OK directo de Lucas ("Dale"): 1 hoja de prueba y, si salía bien, las otras 5. Total 15 créditos.
- Cambios contra la ronda 1 (`docs/claude/higgsfield/zonas_r2_preparar.py`, no toca `ulla_piloto.py`):
  - huecos vacíos rellenados con copias de piezas (sin negro en la entrada);
  - prompt sin "solid black", con "nunca negro" y "mismo tono de cada material";
  - `is_inpaint: true`.
- Prueba (hoja 00, todo `tex_5087`, la que más negro tenía): 6/6, sin negro, color 0,04–0,08. Más detalle (pasto, flores, piedritas) y todo en su lugar.
- Jobs: `docs/claude/nube/hd/zonas/jobs_r2.json`. Salidas: `../AO_HD/ullathorpe_zonas/generadas_r2/`, comparación `r2_comparacion.png`.
- **Resultado:** el negro desapareció. La puerta acepta 31/31 (con `--igualar-color`). Pero a ojo hay 13 dudosas:
  - hoja 04: el modelo dibujó una **grilla de líneas oscuras** entre bloques (`6004` (3,0); `6215` (2,0), (2,1), (3,1), (1,2), (2,2)). La puerta no la ve: el negro son líneas finas.
  - 7 piezas cambiaron de material (pasto verde oscuro → pasto seco claro, 2,5× más brillo): `5087` (6,3), (5,5); `6000` (2,0); `6003` (1,0), (3,0); `6004` (1,0), (2,0). Igualar color arregla la media, no la textura.
- **Propuesta para Arte** (`atlas_hd_propuesta/`, sin gastar): ronda 1 con color igualado + ronda 2 encima, salvo 8 dudosas que tienen una versión buena de la ronda 1 (`propuesta_usa_r1.json`).
  - Siguen dudosas sin alternativa: `5087` (6,3) y (1,4); `6004` (3,0); `6215` (2,1), (3,1), (2,2). Si Arte las rechaza: ronda 3 = 1 hoja, 2,5 créditos (OK de Lucas).

## Cambio en `Tools/hd_remake/ulla_piloto.py` (25/09, pedido de Arte)
- `zonas-importar --igualar-color`: transferencia de color por pieza (media y desvío por canal = los del original, solo píxeles opacos).
- El negro y la forma se miden en la pieza **cruda**; el color, ya igualado. (Si se mide todo después, una pieza pintada de negro queda de un tono parejo y pasa: primera prueba dio 52/52, corregido.)

## Instalación (25/09 04:35, candado de Unity)
- OK de Arte a la propuesta salvo 3 piezas (cambian la forma): `5087` (6,3) y (1,4), `6215` (3,1). Quedan como estaban hasta la ronda 3.
- Atlas armados sobre los HD ya instalados (piloto + ronda 0), no sobre el original ampliado. Fuera de las 49 piezas: 0 píxeles cambiados.
- Copiados a `Assets/Resources/AOMigratorHD/WorldV07/Textures/`: `tex_5087`, `6000`, `6001`, `6215` (reemplazo) y `6002`, `6003`, `6004` (nuevos). MD5 verificado.
- Respaldo de los 4 reemplazados: `../AO_HD/ullathorpe_zonas/respaldo_hd_antes/`.
- Importado por Arte en Unity (04:36): `6002`–`6004` con .meta, Editor.log sin errores. Falta verlo en Play.
- Arte (25/09): ya en el juego (parche + V279). En las capturas de QA: mismo tamaño, sin costuras y combina con la luz Mejorada. El cruce rechazado `6215` (3,1), que quedó como el original ampliado, no desentona. Ronda 3: **opcional** (OK de Lucas).
