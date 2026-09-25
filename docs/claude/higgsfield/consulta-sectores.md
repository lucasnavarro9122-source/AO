# Consulta a los sectores: ¿cómo ayuda Higgsfield? (25/09/2026)

Enviada por el chat Higgsfield (local) a los 7 chats del grupo AO DUELS. Créditos al enviar: ~960 (plan Plus).

| Sector | Estado | Resumen |
|---|---|---|
| CEREBRO | contestó | Piloto HD primero. Tope de créditos: lo pregunta a Lucas; hasta entonces no generar nada. Carpeta y sección del tablero: OK. |
| Arte y Animación | contestó | 1) piloto de tiles, 2) meditación 115–120, 3) FX de hechizos PvP. 8 direcciones: más adelante. |
| Programación | esperando | |
| Servidor y Multiplayer | contestó | Nada por ahora. Sin cambios de protocolo ni de catálogo. |
| Contenido y Fidelidad AO | contestó | Solo upscale 4× del original; los huecos conocidos no se generan. Hace el control de fidelidad. Demo: 7 pisos + ring f5067 + 34 NPC. |
| Interfaz y Controles | esperando | |
| QA y Releases | contestó | Nada para la 0.26. Demo/0.27: ícono del .exe y banner/capturas para LEEME. Hace la puerta `Tools/hd_asset_gate.py`. |

## Reglas que salen de las respuestas
- Nada se genera sin el OK de Lucas y sin mostrar el costo (Cerebro está pidiendo el tope).
- **No escribir en `Assets/` ni en `Archivos Originales/`.** Salidas en `docs/claude/higgsfield/<lote>/salida/` (o Descargas). Integra Arte.
- No cambiar cantidad, orden ni timing de frames, pivot, canvas relativo, hitboxes ni rangos: el visual nunca cambia el gameplay.
- Gráficos nuevos se cuelgan de los 5 `fx` del servidor (`hit`, `miss`, `spell`, `heal`, `death`) + id de hechizo. Un tipo nuevo → avisar antes a Servidor.
- Cada lote se anota con créditos y resultado.

## Herramientas de Higgsfield a probar (25/09, sin gastar)
- `upscale_image` (bytedance, 2k/4k): **no reinventa el layout** → tiles en la misma posición. Salida a 4k y bajar a ×4 exacto en local. Candidato n.º 1 para tiles y hojas.
- `gpt_image_2_5` (`background: transparent`) y `nano_banana_pro` (4k): remaster con referencia si el upscale queda blando. Riesgo: mueven posiciones → pasar la puerta de QA.
- `remove_background`: alfa limpio en salidas con magenta.
- `autosprite` (tiras de sprites, direcciones iso NE/SE): para las 8 direcciones, más adelante.
- El costo exacto se ve con `get_cost` después de subir la imagen (subir no gasta créditos).

## Método acordado con Arte para el piloto (25/09)
- `upscale_image` 4k → bajada a ×4 exacto con **Lanczos** → `piloto-demo/salida/<nombre>_4x.png`. No recortar ni mover nada.
- Alfa: si el upscaler lo aplana, no lo arreglo yo; Arte vuelve a aplicar la máscara original ×4 (vecino más cercano).
- Costuras: Arte mide primero en el mapa. Si se ven, se reenvía cada set rodeado de copias de sí mismo (3×3) y se queda el centro. Arte avisa antes de gastar en eso.
- Los 7 pisos, el ring y los NPC se ordenan después del piloto.

## Puerta de calidad (QA, `Tools/hd_asset_gate.py`)
- Tamaño exacto 4×; misma grilla de celdas/frames; pivot y bbox de cada frame alineados (±1 px del original).
- Alfa sin halos ni semitransparencias donde el original era binario; máscara reducida ×4 vs. original con IoU ≥ 0,98.
- Tiles sin costuras (bordes que envuelven).
- Reducida ×4 se parece al original (paleta y forma, umbral de SSIM).
- Mismo nombre/GRH; importación Point sin compresión.
- Después: hoja lado a lado para Arte, OK de Lucas, y `test_modules_unity.py` + `test_interface_unity.py` sin errores nuevos en el Editor.log.

## Respuestas

### CEREBRO
- Renombró este chat a "AO BATTLESERVER: Higgsfield" y actualizó `sectores.md` y la skill `aod-arte`.
- Prioridad: piloto HD de la demo (Arte). Tope de créditos: pendiente de Lucas.
- Los pedidos de los sectores entran por Higgsfield; los resultados se entregan a Arte.

### Arte y Animación
1. **Demo:** piloto de tiles, tal cual `piloto-demo/` (8 PNG, tile 32→128, `pedido.md`).
2. **Demo:** meditación `Assets/StreamingAssets/AOMigrator/MeditationV269/fx_115…fx_120/frame_01..10.png` (RGBA, 10 frames, pivot abajo al centro; 555 ms, el 120 a 1111 ms). Tamaños: 115=30×40, 116=40×85, 117=80×170, 118=160×170, 119 y 120=140×265. Generar cada uno como **tira de 10** para que los frames sean coherentes (6 imágenes).
3. **Después:** FX de hechizos PvP en `StreamingAssets/AOMigrator/SpellOverrides/02_Por_Hechizo/<hechizo>/` (244; frames y tamaño en cada `manifest.json`). Arte manda la lista corta.
- 8 direcciones: más adelante (caro y difícil que salga coherente).
- Quitar fondo: sí, en las salidas con magenta.
- Validación: Spell Tester (`Tools/SpellTester/index.html`, original vs. HD) para meditación y hechizos; tiles con `Tools/demo_art_specs.py --preview` y después en Unity.

### Servidor y Multiplayer
- No necesita nada. Lo visual de los retos (anuncio, cuenta regresiva, ring, victoria) es de Interfaz y Arte.
- La imagen "cómo conectarse por Tailscale" es para QA (LEEME/ZIP), prioridad baja.

### Contenido y Fidelidad AO
- **Huecos conocidos: no generar** (no hay original; recrearlos sería inventar, ver `MigrationReports/source_gap_report.md`):
  - 8 audios de hechizo (163, 229, 234, 239, 242, 253, 255, 256): Lucas decidió sin sonido y **sin provisorios**.
  - Meditación 153–172: no son públicos; se usan 115–120.
  - 8 objetos sin sección en obj.dat (566, 567, 568, 570, 727, 757, 759, 1645; GRH 0).
- Reglas: solo upscale del gráfico original (frames, orden, duración, pivot/offset, ×4, silueta, paleta). Nunca diseño nuevo si existe el original. No tocar IDs de GRH, datos ni stats. Lo que no tenga original va aparte, marcado "no original", y solo con OK de Lucas.
- Control: revisa original vs. HD lado a lado (Spell Tester) antes de integrar.
- Prioridad demo: sets de los 7 pisos (mapas fuente 264, 4, 392, 564, 291, 142, 391) + ring f5067 de la arena 1001; después los cuerpos de los 34 NPC de `docs/claude/demo/dungeon-npcs.json`; después el resto del mundo. Todo dentro del plan de ~110 créditos (`demo/arte.md` §5).

### QA y Releases
- 0.26: nada (el ZIP queda en protocolo 2, no se recompila).
- Demo/0.27: ícono del .exe y banner o capturas para `LEEME.md`. Video opcional.
