# Pedido a Higgsfield: piloto HD de la demo AO BATTLESERVER

De: **AO BATTLESERVER: Arte y Animación** · Para: **AO BATTLESERVER: Higgsfield** (chat en la nube) · 25/09/2026

**No generar nada hasta que Lucas dé el OK y haya créditos.** Primero mostrarle el costo.

## Qué es
Remaster 4× de 8 hojas de gráficos originales de Argentum Online (vista cenital, tiles de 32×32 px). Es el piloto: si sale bien, después sigue el resto de la demo (~55 imágenes, ~110 créditos).

Los 8 PNG de esta carpeta (Lucas te los sube) son **recortes** de las hojas originales: solo la zona usada, desde la esquina superior izquierda.

## Archivos y tamaños
| Archivo | Qué es | Tamaño | Salida 4× |
|---|---|---|---|
| `6000_pasto.png` | 4 sets de pasto 4×4 (tiles de 32×32) | 512×128 | 2048×512 |
| `6640_nieve.png` | 4 sets de nieve 4×4 | 512×128 | 2048×512 |
| `6021_arena.png` | arena y sus transiciones | 768×768 | 3072×3072 |
| `5114_pantano.png` | barro de pantano y bordes | 1024×1024 | 4096×4096 |
| `5129_cripta.png` | piso de cripta con musgo, muros y pilares dorados | 800×640 | 3200×2560 |
| `5043_empedrado.png` | empedrado gris de plaza | 512×192 | 2048×768 |
| `5067_postes-cuerdas.png` | postes de madera y cuerdas del ring (sprites sueltos) | 512×384 | 2048×1536 |
| `6028_alfombra.png` | alfombra roja con fleco | 512×256 | 2048×1024 |

Si el modelo no llega a 4096 px, partir las salidas grandes (arena, pantano, cripta) en 4 cuadrantes iguales y generar cada uno por separado, con el mismo prompt y la misma semilla.

## Reglas (no negociables)
1. **Exactamente 4×** el tamaño del recorte. Cada tile de 32×32 pasa a 128×128 **en la misma posición**: el juego corta la hoja por coordenadas.
2. **Misma vista cenital** (top-down), misma silueta, misma paleta y misma lectura: lo que es camino sigue pareciendo camino y lo que bloquea sigue pareciendo pared.
3. **Los sets 4×4 tienen que seguir encajando sin costuras**, entre sí y consigo mismos (los bordes izquierdo/derecho y superior/inferior de cada set se repiten en el mapa).
4. **Fondo transparente = transparente.** Las zonas vacías de la hoja (transparentes en el PNG) quedan vacías.
   - Si el modelo no devuelve alfa, dejar ese fondo en **magenta plano #FF00FF**; Arte lo recorta.
   - No inventar objetos en el fondo.
5. **Sin texto, sin marcas de agua, sin bordes nuevos, sin sombras proyectadas nuevas.**
6. Postes y cuerdas: la cuerda es fina y tiene que seguir alineada con el poste de al lado. El anclaje del poste es abajo, en el centro del tile.
7. Estilo: pixel art de alta resolución, fiel al original. No hacer realismo fotográfico.

## Costo estimado (Seedream 4.5 ≈ 1 crédito por imagen)
- 8 imágenes si el modelo saca 4096 px; 17 si hay que partir arena, pantano y cripta.
- Con 1 reintento cada una: **~16 a 34 créditos**.

## Entrega
- Mismos nombres con el sufijo `_4x.png`, y un renglón por imagen con el modelo, el prompt y la semilla usados.
- Lucas los deja en Descargas; Arte los rearma en su hoja (con `manifiesto.json`) y los prueba en el juego.

**Criterio de aprobación (lo verifica Arte):** en el mapa no se ven costuras, las posiciones coinciden con el original, la transparencia está limpia y se lee igual que el original visto de lejos.
