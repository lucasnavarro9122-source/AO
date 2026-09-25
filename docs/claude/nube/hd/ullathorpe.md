# Remaster HD: piso y agua de Ullathorpe y alrededores

**Decisión de Lucas (25/09):** no usar la demo. El lugar del remaster es **Ullathorpe (mapa 1)** y sus alrededores: **Bosque sur (2), Bosque norte (5), Bosque oeste (8) y Este (11)**, que son las salidas de la ciudad.
Alcance: **piso y agua**. Estilo: **"souls pixel art" en 4×, fiel a la resolución original**. La iluminación la hacen Unity y Claude, con Higgsfield para las texturas.
Dueño: **Arte**. Unity: **Arte + Programación**.

## Estilo (referencias de Lucas en `referencias/`)
- `ref1_ullathorpe_noche.webp`: Ullathorpe mismo (fuente, puesto, carteleras y techos) de noche.
- `ref2_cabana_portal.webp`: cabaña, cerezo, portal verde y muro de piedra.
- `ref3_bosque_nevado.webp`: bosque nevado con cristales azules y antorchas.

Qué tiene que tener:
- **Texturas:** pixel art con muchísimo micro-detalle (hojas de pasto, flores chicas blancas y amarillas, piedritas, musgo, grano de tierra), paleta limitada y píxeles nítidos. Vista cenital, sin perspectiva.
- **Tamaño:** igual al original en el mundo, un tile sigue siendo 32 px de juego. Por dentro, 4× (128 px por tile, "pixel de arte" de 2 px), así que un atlas de 1024 pasa a 4096 ("4k").
- **Iluminación (se hace en el motor, no en la textura):**
  - noche fría;
  - faroles y antorchas con charcos de luz cálida y brillo;
  - luciérnagas, niebla baja y haces de luz;
  - sombras bajo personajes y objetos.
- **Personajes siempre legibles:** contorno, borde de luz y luz mínima propia.
- **Regla técnica clave:** las texturas se generan con **luz plana y neutra**, sin sombras ni manchas de luz. Si la luz viene "pintada", se repite en cada tile y choca con las luces del juego.

## Inventario (medido por script)
- Capas 1 y 2 de los 5 mapas: **25 texturas y 111 sets de 4×4** (bloques de 128×128).
- Lo que más se ve:

| Textura | Uso | Sets usados |
|---|---|---|
| `tex_6000` (pasto) | ~29.900 casillas | 3 |
| `tex_5087` (pasto, pasto seco, pasto con piedras) | ~8.600 | 41 |
| `tex_6215` (caminos de tierra) | ~6.800 | 15 |
| `tex_5068` (madera, deck) | ~2.400 | 5 |
| `tex_6001`–`6004` (variantes de pasto) | ~1.900 | 16 |

- **Agua:** casi no hay agua de piso. Son 13 casillas en la ciudad (`tex_20`, estática). El agua que más se ve es **la fuente**: sprite animado de 6 cuadros (`tex_200`, 256×256, en x43 y49).
  - Propuesta para la fuente: rehacer el cuadro 1 y animar el agua con un shader y partículas de brillo, como en la referencia 1. Así se evita generar 6 cuadros coherentes.

## Resultado del piloto (25/09)
- Pasto y caminos generados con 2 modelos, con el mismo prompt y las 3 referencias. Costo: 5,25 créditos (quedan 995,61).
  - GPT Image 2.5: job `79f5b6ef-d4d1-4dee-b831-80a36b64277e`.
  - Seedream 5 Pro: job `5562a4c5-8624-4bd7-ad1a-b73d9c9802c4`.
- **Elegido por Lucas: Seedream 5 Pro** (el más oscuro): "respeta el dibujo, tiene el nivel de detalle que busco, se sabe qué es y tiene calidad".
- El prompt aprobado quedó en `Tools/hd_remake/ulla_piloto.py` (`MODEL = seedream_v5_pro`, 2k, 3:2). Agua y madera usan la misma estructura.
- Agua y madera con Seedream 5 Pro (2k, 1:1), usando como referencia el pasto aprobado para que la oscuridad y el estilo coincidan. Costo: 5 créditos.
  - Agua: job `db7cb9d6-399e-4145-98e8-f344cc6c441f`.
  - Madera: job `8bcb1642-a468-49c6-b806-5049d5fa2508`.
- **Pendiente:** bajar los resultados para importarlos (pixel art 4×, sin costuras) y armar la vista de Ullathorpe. En el entorno de la nube, la red bloquea `d8j0ntlcm91z4.cloudfront.net`, que es de donde Higgsfield sirve los resultados. Hay que habilitar ese dominio en el acceso a la red del entorno, o hacer ese paso en la PC.

## Piloto (3 generaciones, estimado 3–10 créditos con reintentos)
| Familia | Sets | Entrada |
|---|---|---|
| `pasto_caminos` | pasto base, pasto con piedras y flores, pasto seco, camino horizontal, camino vertical, curva | 1536×1024 (3×2 sets) |
| `agua` | agua de estanque (`tex_20`) | 512×512 |
| `madera` | tablones (`tex_5026`) | 512×512 |

Van **juntos en una imagen** los sets que se tocan (el pasto y los caminos): así el modelo mantiene el mismo pasto en los bordes de los caminos.

## Herramientas (`Tools/hd_remake/`, sin Unity)
- `python Tools/hd_remake/ulla_piloto.py preparar` → `../AO_HD/ullathorpe_piloto/`: `*_entrada.png` (4×, vecino más cercano), `*_prompt.txt` y `manifest.json`.
- `python Tools/hd_remake/ulla_piloto.py importar pasto_caminos generado.png`:
  - la lleva a 4× exacto;
  - la hace repetible sin costuras en los ejes que corresponden (el pasto en X e Y; los caminos solo a lo largo);
  - la pasa a pixel art (`--pixel 2`, `--colores 48`);
  - guarda cada set más una vista repetida 3×3.
  - Con `--aplicar`, instala los atlas en `Assets/Resources/AOMigratorHD/WorldV07/Textures/tex_<n>.png`. **Nunca pisa los originales;** lo no rehecho queda como el original ampliado.
- `python Tools/hd_remake/preview_luces.py SALIDA --mapa 1 --escala 4 --hd`: Ullathorpe con las texturas HD y la luz propuesta, al lado de la luz actual. Sirve para aprobar antes de abrir Unity.

Verificado en la nube con una imagen "generada" simulada: preparar → importar → vista 4×, todo sin costuras visibles. La prueba de luz con texturas originales está en `prueba_luz_ullathorpe.png`.

## Unity
- **Parche `parches/hd-texturas-mapa.patch`** (Arte + Programación):
  - `AOWorldManagerV07.LoadTexture` busca primero `AOMigratorHD/...`. Si lo encuentra, `GetSprite` multiplica el recorte y los pixelsPerUnit por 4: **mismo tamaño en el mundo**, 4× de detalle. Sin HD, todo sigue igual.
  - `AOWorldManagerV07.UseHDTextures` queda listo para la opción de Interfaz "Gráficos: Original / HD".
  - `Editor/AOHDTextureImportV901.cs` importa las texturas HD con Point, sin mipmaps, sin compresión y un máximo de 4096.
  - Sin errores de sintaxis (Roslyn, C# 9). **Falta compilar y probar en Unity.**
- **Iluminación (siguiente paso de Arte):**
  - URP 2D con una luz global por mapa (`baseLight`), y las luces de cada mapa (`lights`) como Light2D con falloff suave y parpadeo.
  - Volumen de post-proceso: bloom, viñeta y color.
  - Sombras de contacto: sprite blando bajo personajes y objetos altos.
  - Partículas: luciérnagas y polvo en las `particles` del mapa; niebla.
  - Personajes: contorno y borde de luz, más una luz mínima propia.
  - Opción "Luz: Original / Mejorada", porque la actual es la fiel al AO20. `preview_luces.py` muestra el objetivo.
- Número de módulo **provisorio V901**: renumerar al integrar si Cerebro prefiere la próxima versión libre.

## Cómo generar (en un chat con Higgsfield activo)
Las herramientas de Higgsfield se cargan al empezar un chat. En el chat de la nube donde se armó esto no estaban.

Pegar en un chat nuevo de este repo, con Higgsfield activado:

> Seguí `docs/claude/nube/hd/ullathorpe.md`. Corré `python Tools/hd_remake/ulla_piloto.py preparar --salida /tmp/hd`. Subí a Higgsfield `pasto_caminos_entrada.png` y las 3 referencias de `docs/claude/nube/hd/referencias/`. Elegí el modelo de edición de imagen con referencia de estilo más adecuado (models_explore). Mostrame el costo y esperá mi OK. Generá con el prompt de `pasto_caminos_prompt.txt`, importalo con `ulla_piloto.py importar` y mostrame la vista de `preview_luces.py --escala 4 --hd` antes de seguir con agua y madera.

**Criterios de aprobación:**
- el mismo dibujo del original (caminos en el mismo lugar);
- sin costuras al repetir;
- pasto igual en todos los sets;
- luz neutra;
- se lee como tus referencias con la luz del motor.
