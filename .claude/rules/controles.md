---
paths:
  - "Assets/AOMigrator/Runtime/AOControl*.cs"
  - "Assets/AOMigrator/Runtime/AOActionBar*.cs"
  - "Assets/AOMigrator/Runtime/AOShortcutHUD*.cs"
  - "Assets/AOMigrator/Runtime/AOInterfaceActionBarBridge*.cs"
  - "Assets/AOMigrator/Runtime/AOSkillShot*.cs"
  - "Assets/AOMigrator/Runtime/AOCastAnimation*.cs"
  - "Assets/AOMigrator/Runtime/AOMeditationVisual*.cs"
  - "Assets/AOMigrator/Runtime/AOPlayerSettings*.cs"
  - "Assets/AOMigrator/Editor/AOControlsQA*.cs"
  - "Tools/test_controls_unity.py"
---

# Controles AO / MOBA (V260) y módulos posteriores

## Diseño implementado
- Selector **Ajustes > Controles > AO / MOBA**, teclas independientes por perfil.
- AO: conserva controles originales; macros de hechizos apagadas de inicio (habilitables), teclas iniciales F5/F6/F7/F8.
- MOBA: clic derecho camina / persigue-ataca / se acerca a interactuar. Q/W/E/R hechizos; 1/2/3/4 consumibles; M mapa, I inventario, J misiones, C personaje, F interactuar, G recoger, A atacar, S detener, B meditar.
- Teclas y botones de mouse editables, validación de conflictos, opción de quitar tecla. Enter/Escape reservados.
- Hechizos aprendidos y consumibles asignables por personaje y perfil. Una acción por pulsación; respetar maná, requisitos y cooldown.
- Rutas por casillas respetando obstáculos; el fantasma camina hacia el sacerdote.
- Música: intro MIDI 2 (**Argentum Opening**), Ullathorpe MIDI 4. Caché MIDI de rutas cortas en `%LOCALAPPDATA%/AoDuels/Music` (evita fallos de Windows MCI).
- Archivos: `AOControlProfilesV260.cs`, `AOActionBarV260.cs`, `AOShortcutHUDV260.cs`, `AOControlsSettingsUIV260.cs` + cambios en ajustes, interfaz, jugador, magia, combate, inventario, audio y menú.

## Verificación
- `MigrationReports/controls_v260.json`: `passed: true`, etapa 7, 24/09/2026 00:23. Cubrió perfiles, conflictos, macros AO, rutas, clic derecho sintético, consumibles, maná/cooldown, bloqueo bajo ajustes, cambio intro→música ciudad. Capturas `controls_ao_v260.png`, `controls_moba_v260.png`. 7 JSON de guardado sin cambios.
- La prueba usa mouse sintético (el editor consume eventos si Game View pierde foco).

## Módulos posteriores (24/09 tarde, SIN verificar)
- V261: drag & drop en barra (`AOActionBarDragDropV261`, `AOInterfaceActionBarBridgeV261`).
- V267: proyectiles dirigidos (`AOSkillShotConfigV267`, `AOSkillShotProjectileV267`).
- V268: animación de lanzamiento (`AOCastAnimationDatabaseV268`, `AOCastAnimationRuntimeV268`, datos en `StreamingAssets/AOMigrator/CastV268`).
- V269: meditación (`AOMeditationVisualV269`, `Resources/` y `StreamingAssets/AOMigrator/MeditationV269`).
- V130: efectos persistentes y reemplazos visuales (`AOSpellPersistentVisualV130`, `AOSpellVisualOverridesV130`, `StreamingAssets/AOMigrator/SpellOverrides`).
- Leer fuentes antes de modificar; no descartarlos ni tratarlos como parte de la prueba de 00:23. Revisar si rompen supuestos de `test_controls_unity.py`.
