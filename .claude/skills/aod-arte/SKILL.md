---
name: aod-arte
description: Pipeline de arte y animación de AoDuels: sprites, spritesheets, FX de hechizos, auras, casteo, meditación, terreno HD, remaster con IA (Higgsfield, SpriteForge/ComfyUI) y Spell Tester. Usar para crear, mejorar, importar o ajustar cualquier gráfico o animación, o para ajustar el tamaño o la posición de un efecto.
---

# Arte y animación

## Reglas del remaster (no negociables)
- **4×** la resolución original: 32→128, 64→256, 256→1024, 512→2048. Los atlas de 2048 se dividen (un 4× serían 8192).
- Mismo canvas relativo, **misma silueta y pose**, mismo **pivot**, misma **cantidad y orden de frames**, mismo timing.
- No tocar hitboxes, rangos ni colisiones: el visual nunca cambia el gameplay (la colisión vive en `AOGridMap`).
- Mantener la lectura top-down de AO: se tiene que distinguir qué se camina y qué bloquea.
- Nunca procesar sprite por sprite (75.906 recortes): trabajar por atlas u hoja por personaje/animación. Primero un piloto de 20–30 texturas (suelo, árbol, personaje, animal, objeto, hechizo, UI).

## Herramientas
- **Higgsfield: solo a través del chat local "AO BATTLESERVER: Higgsfield"** (sector 7). Ningún otro chat genera por su cuenta; el chat en la nube con el nombre viejo no es el sector.
  - Pedidos autosuficientes por SendMessage: asset, tamaño original, 4×, frames, pivot, reglas.
  - Higgsfield entrega en `docs/claude/higgsfield/` (o donde indique Arte) y Arte integra en `Assets/`.
  - ~960 créditos (plan Plus): generar solo con el OK de Lucas y mostrando el costo antes. Seedream 4.5 ≈ 1 crédito/imagen. Remaster completo ≈ 5–6k créditos (US$150–300). Las generaciones por MCP descuentan créditos; el "Unlimited" es solo en la web. Mostrar el costo antes de generar y pedir el OK de Lucas en cada lote.
- **SpriteForge** (ComfyUI, `Downloads/AoDuels_SpriteForge.json` + manual PDF): 1 imagen → 8 frames de 512×512 con alpha + hoja 2048×1024 + preview.
  - Se edita solo la 1.ª línea del prompt: animación en español + inglés entre paréntesis.
  - Modelos: `qwen_image_edit_2511_bf16`, `qwen_2.5_vl_7b_fp8_scaled`, `qwen_image_vae`, `birefnet`.
  - ComfyUI **no está instalado** (hay un instalador en Downloads y existe `C:\pinokio`). Instalar o descargar modelos requiere el OK de Lucas.
- **Spell Tester** (`Tools/SpellTester/index.html`, abrir en el navegador): hechizo + proyectil + impacto + EOT + audio + meditación, sin Unity. Usarlo como QA visual antes de meter cosas al juego.
- **Pipeline de terreno HD** en el Editor: `AOTerrainHDPipelineWindow`, `AOTerrainHiggsPrepV029`, `AOTerrainSemanticClassifierV029`, diagnóstico `AOTerrainHDDiagnosticV029`.
- Skills de Unity: `unity:sprite-editor` (slicing), `unity:manage-sprite-atlas`, `unity:2d-pixel-perfect`.

## Importar a Unity
- Sprite (2D and UI), Sprite Mode Multiple, **Filter Point**, **Compression None**, sin mipmaps; slice Grid By Cell Size (512×512 para SpriteForge).
- Los FX de hechizos van por datos, no por escena: `StreamingAssets/AOMigrator/SpellOverrides/02_Por_Hechizo/<hechizo>/manifest.json` + PNG.
  - Prioridad: `particleviaje` (proyectil) → `spell_fx` (impacto) → `particle` → `icon`.
  - EOT: `eot_client_effect`, `eot_tickfx`, `eot_onhitfx`, `eot_aura`.
- Ajuste fino sin tocar código:
  - `spell_visual_tuning.json`: `projectileScale`, `impactScale`, `persistentScale`, `*YOffset`, `projectileSpeed`, `impactDuration`, `persistentFps`, `transientFps`.
  - `skillshot_tuning.json`: `visualScale` es de Arte; `speed`, `range` y `hitRadius` son gameplay y necesitan el OK de Programación.
- Casteo: `StreamingAssets/AOMigrator/CastV268` (13 bodies originales). Meditación: `MeditationV269` + `Resources/AOMigrator/MeditationV269` (FX 115–120, sonido 158).

## Pendientes de arte
Personajes en 8 direcciones (hoy son 4), Spell Tester v1.3 (original vs HD, zoom, pivote, frame a frame, exportar tuning), piloto HD. Ver `docs/claude/tablero.md`.
