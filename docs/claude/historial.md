# Historial del proyecto (chats anteriores con ChatGPT)

**Fuentes crudas completas** en `docs/claude/fuentes/`: los dos chats convertidos a .md, CONTEXTO_PARA_OTRA_IA.md, y el flujo y manual de SpriteForge. No leerlas enteras: buscar con grep lo puntual (ej. `grep -n "Meditación" docs/claude/fuentes/*.md`) cuando este resumen no alcance.

Resumen de los chats exportados "Recrear Argentum en Unity" (19/09) y "Ver directorio local" (24/09), más el flujo SpriteForge. Datos históricos: verificar contra el código antes de afirmar algo.

## Origen y decisiones de base
- Fuente: organización **ao-org** (AO20): `argentum-online-client` (VB6), `argentum-online-server` (VB6), `Recursos`, `argentum-online-worldeditor`, `ao-ui` (React, solo referencia). Copias locales en `Archivos Originales/`.
- Licencias: cliente/servidor AGPL-3.0; `Recursos` con licencia propia. Revisar antes de publicar o vender.
- Estrategia: reimplementar en C# usando el AO original como especificación. No traducir VB6 línea por línea. Primero funcionalidad fiel y después la renovación gráfica con IA (Higgsfield), por familias de assets y sin tocar la lógica.
- La colisión está separada del visual: matriz lógica 100×100 como en AO (bloqueos, agua, costa, árboles, triggers). Reemplazar un gráfico nunca cambia dónde se camina.
- Mapas `.csm`: tamaño de cabecera en 0 (asumir 100×100); 4 capas + objetos, luces, partículas, NPC, salidas (`tDatosTE`). Los GRH se leen de `Graficos.ini` (recorte o animación). `Grh31492` está malformado y hay GRH duplicados; algunos sprites se pasan del PNG (se completa con padding transparente).

## Versiones del migrador/juego (orden cronológico)
- v0.1–0.2: AO Migrador (Python + Tk) → analiza, genera el puente y construye la escena estática de mapa1 (Ullathorpe: 11.025 celdas, 921 sprites, 46 PNG).
- v0.3: escena jugable, movimiento por casilla, colisiones como `LegalPos`, overlay F2. Hotfix de strings con saltos de línea (CS1010).
- v0.4–0.5: personaje compuesto (Body/Head/Helmet/Weapon/Shield, 4 direcciones) + Character Builder (moldes, presets, prefabs).
- v0.6.x: 27 NPC + 131 objetos en Ullathorpe. Reparaciones de AOMigrator duplicado, referencia perdida a AOGridMap y visual del mapa.
- v0.7: mundo conectado (World Manager, 21 mapas, salidas). v0.8: IA de NPC (Movement 2/20, Caminata, 380 ms).
- v0.9: combate local (npcs.dat: HP, MinHIT/MaxHIT, DEF, PoderAtaque/Evasión), loot, respawn.
- v0.10.x: inventario/equipo (obj.dat: 4.275 objetos, 885 equipables), interfaz original (frmMain 1024×768, viewport 736×608), minimapa, chat y tooltips.
- v0.11.x: RPG real (Balance.dat, 24 skills, tabla de EXP hasta nivel 47), raza/género visual, fix de animación, combate visual, consumibles y supervivencia.
- v0.12.0–0.12.9: magia completa local (141 hechizos, 60 EOT, 19 invocaciones, meditación, materialización, teleport). Hotfix de ambigüedad `Object` (usar `UnityEngine.Object.`, no reemplazar a ciegas).
- v0.13: NPC de ciudad (821 NPC; 163 comerciantes, banco de 42 slots, sacerdote). v0.14: guardado/carga + menú. v0.15: quests (Quests.DAT, máximo 5 activas). v0.16: muerte/fantasma BODY829, /HOGAR 105 s → mapa 1 X57 Y44. v0.17: creación de personaje + equipo y hechizos iniciales por clase.
- v0.18: drops + respawn + marcador `!` de quest (existen `AOLootRespawnQuestMarkersV180Installer`, `AOQuestMarker*V180`).
- v0.19–0.23 (sin chat exportado): audio V190, interfaz clásica V200, entorno/puertas/techos/árboles V210, carga clásica V220, ajustes V230.
- Hay informes con **842 mapas** cargados (la v0.7 empezó con 21).
- v0.24–0.25: servidor cooperativo propio (.NET, protocolo 2, Tailscale, 1 anfitrión + 10 amigos).
- Pipeline de terreno HD con Higgsfield en marcha: `AOTerrainHD*`, `AOTerrainHiggsPrepV029`.

## Chat "Ver directorio local" (24/09): v261–v269 + herramientas
- v261–263: hotbar de 8 slots abajo a la izquierda (QWER hechizos, 1234 consumibles), drag & drop, swap, cooldown visual. Fix: usar **Input System** (`Mouse.current`), nunca `UnityEngine.Input`.
- v264: overrides visuales por hechizo desde `StreamingAssets/AOMigrator/SpellOverrides/02_Por_Hechizo` (122 manifests): `particleviaje` = proyectil, `spell_fx` = impacto, `particle` = respaldo, `icon` = último recurso.
- v265: EOT (`eot_client_effect`, `eot_tickfx`, `eot_onhitfx`, `eot_aura`) + `spell_visual_tuning.json` (escalas, offsets, velocidad, fps y duración por hechizo).
- v266: movimiento en 8 direcciones en AO y MOBA, sin cortar esquinas, diagonal normalizada. Los sprites siguen en 4 direcciones; hacer 8 visuales es un upgrade futuro.
- Audio de hechizos: 141/141 IDs WAV correctos. Faltan 8 audios originales (163, 229, 234, 239, 242, 253, 255, 256), que afectan 21 hechizos. No inventar sonidos.
- v267: **skill shots** para los 14 hechizos con `particleviaje`: Dardo Mágico, Flecha Mágica, Misil Mágico, Petrificar, Flecha Eléctrica, Lamento de la Banshee, Maldición del Bufón, Gloria del Bufón, Ira de ReyarB, Flecha (NPCS), Flecha Scramer (NPCS), Colmillo Filoso (NPCS), Ataque de Veneno (NPCS) y Onda Mágica.
  - Van en línea recta y chocan con árboles, paredes y puertas; tienen rango máximo y se pueden esquivar.
  - Se ajustan en `skillshot_tuning.json` (speed, range, hitRadius, visualScale).
- v268: animación de casteo del jugador (Idle/Walk/Attack/Casting) + `CastAnimation` original de NPC (13 bodies, 232 frames). La IA mágica original de NPC todavía no existe.
- v269: meditación con B: aura (inicio → loop → cierre) + sonido 158. El tier de FX va por nivel: 1–12 → 115, 13–24 → 116, 25–34 → 117, 35–44 → 118, 45–46 → 119, 47+ → 120.
  - El cliente original tiene 15 variantes (FX 122–141). La tabla nivel→FX real la decide el servidor.
- **AO Spell Tester** (HTML externo, v1 → v1.2): hechizo, audio, EOT, meditación. En el proyecto: `Tools/SpellTester/`. Sirve de laboratorio de QA visual antes de meter cosas al juego.
- Presupuesto Higgsfield (remaster a 4×):
  - Volumen: ~1.164 texturas únicas en el proyecto (sin minimapas); ~3.000 generaciones para un remaster completo.
  - Con Seedream 4.5 (~1 crédito): 5.000–6.000 créditos, ≈ US$150–300.
  - Nunca mandar sprite por sprite (75.906 recortes): trabajar por atlas u hoja. Hacer un piloto de 20–30 texturas primero.

## SpriteForge (ComfyUI)
- Flujo `AoDuels_SpriteForge.json` (en Downloads, con manual PDF): Qwen-Image-Edit 2511 (bf16) + qwen_2.5_vl_7b + qwen_image_vae + BiRefNet.
- 1 imagen de personaje + la primera línea del prompt (nombre de la animación, también en inglés) → hoja 4×2 (2048×1024) en verde #00FF00 → 8 frames de 512×512 con alpha + spritesheet para Unity + preview webp. Tarda ~2–4 min; con `steps` 12 sale más barato.
- En Unity: Sprite Multiple, Point, sin compresión, Grid By Cell Size 512×512.
- ComfyUI **no está instalado** (solo el instalador Comfy-Desktop en Downloads). Existe `C:\pinokio`.
- Higgsfield: Lucas se quedó sin créditos (24/09); vuelve más adelante.

## Pendientes que surgieron en los chats
- IA mágica original de NPC (qué hechizos conoce cada uno, rango, intervalo, CastAnimation).
- Sprites de 8 direcciones (NE/NO/SE/SO).
- Los 8 audios de hechizo que faltan (buscar en otras versiones de Recursos).
- Pérdida de objetos/oro al morir (faltan las reglas del servidor: MapasNoDrop, newbie).
- Spell Tester v1.3: comparar original vs HD, zoom, pivote, frame a frame, exportar el tuning.
- Profesiones, clanes, PvP y facciones: no hechos.
