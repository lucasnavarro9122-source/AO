# Reporte a los sectores: trabajo hecho en la nube (25/09, madrugada)

Chat de la nube "Revisión del repositorio", en la rama **`claude/nifty-thompson-r3ulpf`** (GitHub), basada en `384fe18`. **No incluye** el trabajo de la noche del 24/09 en la PC.

**Regla que se siguió:** solo archivos nuevos. El código existente de `Assets/` y `OnlineServer/` va como **parches** (`docs/claude/nube/parches/`), para que cada dueño los aplique sobre su versión de la PC.
Índice general: `docs/claude/nube/LEEME.md`.

## Cómo traerlo a la PC (CEREBRO)
```powershell
git switch -c pc/noche-2509; git add -A; git commit -m "Trabajo de los sectores, noche 24-25/09"
git fetch origin; git merge origin/claude/nifty-thompson-r3ulpf
```
- Son solo archivos nuevos, más `docs/claude/nube/*`, `.github/` y `Assets/Resources/AOMigratorHD/`: no se esperan conflictos.
- Después, cada sector aplica sus parches en el orden del `LEEME.md`: `git apply --check` primero; si falla, aplicarlo a mano siguiendo su `.md`.

## Decisiones de Lucas (25/09)
1. Personajes en **4 direcciones**. Al tirar un skill shot, el personaje **gira de verdad**.
2. **NPC:** DEF y rango preferido originales, visión **15×13** por eje.
3. **Pérdida al morir** como el original.
4. **Pruebas en GitHub Actions:** activas, en verde.
5. **Remake HD:** Ullathorpe más sus 4 alrededores (mapas 1, 2, 5, 8, 11).
   - Piso y agua, "souls pixel art" en 4×, fiel a la resolución original.
   - Modelo **Seedream 5 Pro**. La luz la pone el motor.
   - **No se usa la demo para el HD.**

## CEREBRO
- Integrar la rama (arriba) y pasar al `tablero.md` los pendientes de cada sector (abajo).
- **Créditos de Higgsfield:** la nube gastó **40,25** (saldo aproximado **960,6**).
  - Ya están hechos: piloto (pasto, caminos, agua y madera) y 20 piezas de zonas.
  - Coordinar con "AO BATTLESERVER: Higgsfield" y Arte para **no rehacer ni pagar dos veces**.
- Números de módulo **provisorios** de la nube: **V900** (pérdida al morir) y **V901** (importador HD). Renumerar si preferís la próxima versión libre.
- **Seguridad (hacer ya):** firewall solo Tailscale y ACL `tcp:7777` (ver `guia-amigos.md`).

## Programación
Parches (`docs/claude/nube/parches/`):
- `eot-dano-magico-doble`: los EOT aplicaban la reducción mágica 2 veces, en el jugador **y en los NPC**.
- `skillshot-girar-personaje`: `FaceHeading` + `AimHeading`, 4 direcciones con 10° de histéresis.
- `npc-vision-15x13-juego`: `AONPCMovementV08.SetVisionAxes` y los campos en `AOWorldManagerV07`.
- `muerte-juego` (**V900**): reglas compartidas `Runtime/Shared/AODeathDropRulesV900.cs`, `AODeathDropV900` y un enganche en `AODeathRespawnV160`.
- `hd-texturas-mapa` (junto con Arte): `AOWorldManagerV07` carga HD con el mismo tamaño en el mundo.

Pendientes de la revisión (`revision-codigo.md`):
- `FindNpcHit` debería filtrar por mapa.
- Revisar el clic de `TryTargetMouse` con el perfil MOBA.

Datos listos para la IA mágica de NPC: `docs/claude/contenido/npc_hechizos_original.{md,json}` (98 NPC). Hoy ningún NPC castea.

Todos verificados solo por sintaxis (Roslyn): **falta compilar y probar en Unity**.

## Servidor y Multiplayer
- **Aplicar primero `servidor-sesion-fantasma` (ALTA, reproducido):** un `hello` o un guardado ilegible apagaba la sala para todos, y quedaba guardado.
- `npc-vision-15x13-servidor`.
- `muerte-online`: acción y evento `death`. Requiere antes `muerte-juego`.
- Pruebas nuevas: `test_server_robustness`, `test_npc_vision` y `test_death_drop`. Cada una falla sin su parche y pasa con él. Sumarlas a `.github/workflows/pruebas.yml` al aplicar.
- Seguridad (`revision-codigo.md` #2–#12):
  - `--bind` y plazo del `hello`;
  - `Save` sin excepción y `Leave` con try/finally;
  - expiración y tope del botín;
  - guardar con `dirty` cada 1–2 s;
  - tope de personajes;
  - oro e inventario con libro contable;
  - `Harmful()` con EOT, velocidad, alcance y mascotas.
- `guia-amigos.md`: borrador para `../AO_Online/LEEME.md` (firewall, Tailscale y actualizar sin perder partidas).
- Catálogo: después de los datos de Contenido (NPC y objetos), regenerar con `export_online_catalog.py` y publicar servidor y cliente **juntos**.

## Contenido y Fidelidad AO
- `docs/claude/contenido/perdida_al_morir_original.md` y `npc_hechizos_original.{md,json}`. Fuentes: ao-org 08a5711 / 552d4ed.
- **Aprobado:** corregir las claves de `map_migration.py` (patch `npc-stats-map-migration`).
  - En la PC correr `Tools/fix_npc_stats_original.py` (simula; con `--aplicar` escribe) y después `export_online_catalog.py`.
  - Afecta 28 NPC con DEF, 1 con rango preferido, y la visión 15×13 de todos.
- **Para la pérdida al morir:** `Tools/add_item_drop_flags.py` agrega `noDrop` (NoSeCae, 571) y `cantThrow` (Intirable, 417) a `items.json`.

## Interfaz y Controles
Hallazgos (`revision-codigo.md`):
- el error de `SetSpellMacros` se ignora al soltar en la barra;
- arrastrar no revisa los modales;
- al reasignar una tecla, un clic en cualquier parte se toma como la tecla nueva;
- el texto "hasta 10 jugadores" debería decir 11;
- el chat puede tener `richText`.

Opciones nuevas para el menú de ajustes:
- **"Gráficos: Original / HD"**: `AOWorldManagerV07.UseHDTextures`, del parche `hd-texturas-mapa`;
- **"Luz: Original / Mejorada"**: cuando Arte haga las luces URP 2D.

## Arte y Animación (y AO BATTLESERVER: Higgsfield)
- `sprites-8-direcciones.md`: **decidido quedarse en 4 direcciones**.
- **Spell Tester v1.3:** `Tools/SpellTester/index_v13.html` (zoom, frame a frame, pivote, exportar tuning, original contra HD).
- **Remake HD** (`docs/claude/nube/hd/ullathorpe.md`, herramientas en `Tools/hd_remake/`):
  - Piloto aprobado: Seedream 5 Pro, prompt en `ulla_piloto.py`. Jobs registrados.
  - **Instalado** en `Assets/Resources/AOMigratorHD/WorldV07/Textures/`: 8 sets del piloto más **20 piezas** de zonas.
  - **Pendiente:** 52 piezas. Ronda 1 preparada: 9 hojas, ~22,5 créditos (`zonas-preparar --ronda 1`, después `zonas-importar --ronda 1 --aplicar`).
  - **Lección:** no pasar la hoja aprobada como referencia en hojas del mismo material, porque el modelo la copia. El importador valida cada pieza.
  - En la nube hay que permitir `d8j0ntlcm91z4.cloudfront.net` para bajar resultados.
  - `preview_luces.py --mapa 1 --escala 4 --hd`: vista de Ullathorpe, original contra HD con la luz nueva.
- **Luces (siguiente):** URP 2D ya está configurado (`Renderer2D.asset`), falta implementarlo.
  - Global Light 2D por mapa y Point Light 2D por cada luz del mapa, con parpadeo.
  - Volume con bloom y viñeta; partículas; contorno y borde de luz en personajes.
  - Opción Original / Mejorada.
- Rendimiento: `AOSpellFXV120.Play` llama a `PlayNearestNpc` en cada hechizo del jugador.

## QA y Releases
- **CI:** `.github/workflows/pruebas.yml`, en verde. Checkout parcial; valida JSON y Python, compila el servidor y corre `test_coop_server` y `test_static_npcs`.
  - Sumar `test_server_robustness`, `test_npc_vision` y `test_death_drop` cuando se apliquen sus parches.
- `Tools/ci_checks.py` sirve también en la PC.
- Probar en Unity cada parche siguiendo los pasos de su `.md` (con `aod-respaldo` antes).
- Hallazgos QA: `TestPrefixOverride` no se limpia; el marcador `restart_online_editor` guarda la escena sin preguntar.
- **Release 0.26:** servidor y cliente juntos, porque cambian el catálogo, la visión de NPC, la pérdida al morir y el HD.
