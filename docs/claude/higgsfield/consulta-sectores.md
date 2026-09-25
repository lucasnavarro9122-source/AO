# Consulta Higgsfield → sectores (25/09)

Chat nuevo **AO BATTLESERVER: Higgsfield**: todo lo de Higgsfield (generar, mejorar, gastar créditos) pasa por acá. Arte sigue siendo dueño de integrar en Unity y de los criterios visuales.

- Saldo al 25/09: **995,61 créditos, plan Plus** (la skill `aod-arte` dice "sin créditos": ya no es así).
- Qué ofrece Higgsfield: imagen (Seedream 4.5 ≈ 1 crédito, Nano Banana, GPT Image), upscale, quitar fondo, outpaint, video (se puede sacar frames para FX), audio/SFX, 3D.
- Límites del proyecto: sin voces de NPC, música e intro originales, no cambiar stats, regla 4× (misma silueta, pivote, frames y timing).

## Cómo mandarla
Estos chats corren en la PC de Lucas y este chat es en la nube: no los alcanza por SendMessage. Dos opciones:
- **A (recomendada):** pegar el prompt de abajo en **CEREBRO**, que se lo manda a los 6 sectores.
- **B:** pegar a mano cada mensaje en su chat.

Las respuestas van a `docs/claude/higgsfield/respuestas.md` (una sección por sector, 5 líneas como máximo). Después, pegarlas en este chat o commitearlas.

### Prompt para CEREBRO
```
Hay un chat nuevo "AO BATTLESERVER: Higgsfield" (en la nube) que se encarga de todo lo de Higgsfield. Mandá por SendMessage a cada sector su pregunta de abajo. Cada uno contesta en docs/claude/higgsfield/respuestas.md, en su sección, con 5 líneas como máximo y sin gastar créditos. Anotá en sectores.md que Higgsfield pasa al chat nuevo (Arte integra en Unity y define los criterios). Avisame cuando estén todas.
[pegar acá las 6 preguntas]
```

## Preguntas por sector

**Programación**
```
Consulta de Higgsfield: ¿qué assets gráficos te faltan o son placeholder en tus sistemas (skill shots, mascotas invocadas, muerte, NPC, puertas, loot)? ¿Qué restricciones técnicas tiene que cumplir lo que se genere para no romper tu código (tamaño, cantidad de frames, pivote, nombres, rutas)? Máximo 5 líneas en docs/claude/higgsfield/respuestas.md → Programación.
```

**Arte y Animación**
```
Consulta de Higgsfield: hay 995 créditos (plan Plus). ¿Arrancamos con el piloto de arte.md §5 (8 texturas, ~16 créditos) o preferís otro? ¿Qué familia gana más con HD: terreno, personajes, FX de hechizos, meditación o sprites de 8 direcciones? ¿Cómo querés recibir los resultados (carpeta, nombres, qué validás con AOTerrainHDDiagnosticV029 o con el Spell Tester)? ¿Quitar fondo con Higgsfield o con birefnet? Máximo 5 líneas en docs/claude/higgsfield/respuestas.md → Arte.
```

**Servidor y Multiplayer**
```
Consulta de Higgsfield: si las texturas pasan a 4×, ¿cambia algo para el servidor, el catálogo o el tamaño del cliente que bajan los amigos por Tailscale? ¿Hay un tope de tamaño del ZIP? ¿Te sirve algo gráfico (por ejemplo, un ícono o una pantalla de sala o conexión)? Máximo 5 líneas en docs/claude/higgsfield/respuestas.md → Servidor.
```

**Contenido y Fidelidad AO**
```
Consulta de Higgsfield: ¿qué gráficos tienen que quedar 100 % originales (sin HD) por fidelidad? ¿Podés armar, por familia, la lista de GRH o archivos originales de referencia para los lotes (ring, hub, pisos del dungeon, NPC del dungeon)? Los 21 hechizos mudos (audios 163, 229, 234, 239, 242, 253, 255 y 256): ¿aceptarías SFX creados con Higgsfield a partir de la descripción del hechizo o de un sonido original parecido? (Lo decide Lucas; hoy quedan mudos.) Máximo 5 líneas en docs/claude/higgsfield/respuestas.md → Contenido.
```

**Interfaz y Controles**
```
Consulta de Higgsfield: ¿qué piezas del HUD clásico se ven borrosas o chicas en 16:9 (marcos, botones, íconos de hechizos y objetos de 32×32, barras, fuentes)? ¿Se pueden reemplazar por versiones 4× idénticas sin romper los rects ni el 9-slice? ¿Hace falta algo nuevo, como el ícono de Retos o la pantalla de carga? Máximo 5 líneas en docs/claude/higgsfield/respuestas.md → Interfaz.
```

**QA y Releases**
```
Consulta de Higgsfield: ¿qué chequeos automáticos querés para aceptar un asset HD (tamaño 4× exacto, alfa, pivote, cantidad de frames, peso del archivo)? ¿Hace falta un interruptor original/HD para volver atrás? ¿Cuál es el tope de tamaño del ZIP del cliente? Máximo 5 líneas en docs/claude/higgsfield/respuestas.md → QA.
```

## Ideas iniciales de este chat (antes de las respuestas)
1. **Piloto de terreno** (arte.md §5): 8 texturas, ~16 créditos. Confirma si Seedream saca 4096 px y si las texturas repiten sin costuras.
2. **Íconos de hechizos y objetos** en 4× para la hotbar y el inventario: se notan mucho y son pocos por lote (una hoja por grupo).
3. **FX de meditación 115–120 y de los hechizos más usados:** upscale de los frames con el mismo timing y QA en el Spell Tester.
4. **Personajes en 8 direcciones:** generar NE/NO/SE/SO desde las 4 originales (hoja por cuerpo). Es lo más caro: hacerlo después del piloto.
5. **SFX de los 21 hechizos mudos:** solo si Lucas cambia la decisión del 24/09.

Nada se genera sin el OK de Lucas con el costo a la vista.
