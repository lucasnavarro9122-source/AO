---
name: aod-clima-vfx
description: Motor de clima y efectos ambientales de AoDuels/AO BATTLESERVER (lluvia, nieve, niebla, viento, tormentas, relámpagos, humo, fuego, chispas, polvo, partículas de mapa, efectos por evento) y su integración con la luz. Usar antes de agregar o cambiar cualquier efecto visual ambiental, clima, partícula de mapa o calidad de efectos, y para estimar su costo.
---

# Clima y VFX ambientales

Prioridad: **estabilidad > rendimiento > gameplay legible > coherencia artística > espectacularidad**. Más atmósfera con menos costo, no más partículas.

## Qué existe (no duplicar)
| Pieza | Dónde | Qué hace |
|---|---|---|
| Motor de clima | `Runtime/AOMapWeather.cs` (1 por mapa, lo crea `AOWorldManagerV07.BuildMapWeather`) | lluvia, nieve, niebla; es el "WeatherManager": se extiende, no se reemplaza |
| Clima V290 | `AOMapWeatherRoofsV290` (agua en techos, `roof_flow.json` de `Tools/roof_flow.py`), `AOWindV290` (viento global), `AOEffectsQualityV290` (Low–Ultra), `AOWorldManagerWeatherV290` (techo del jugador), `AOAudioWeatherV290` (volumen de lluvia) | capas con parallax, salpicaduras, niebla de suelo, tinte que multiplica (`AOWeatherMultiply.shader`); detalle en `docs/claude/nube/clima-vfx/paquete1.md` |
| Depuración | Editor `AOWeatherDebugV290`: **AO Migrator > Clima (depuración)** | presets, intensidad, viento, niebla, calidad y contadores; nada queda guardado |
| Permisos por mapa | `Resources/AOMigrator/WorldV07/map_environment.json` | `rain`, `snow`, `fog`, `baseLight` |
| Disparo | `AOWorldManagerV07.ApplyRainToggle / ApplySnowToggle / ApplyFogToggle / SetWeather` | entradas del AO original; **hoy solo las llama QA del Editor** |
| Hora | `SyncWorldTime` → `SetWorldHour` → `AOMapLighting.DayColor` | **dormida**: la hora queda en 13:00 |
| Zonas cubiertas | `AOWorldManagerRoofV210` (disparadores 1, 4, 16, 20–199) | techo del jugador = zona protegida del clima |
| Partículas de mapa | `AOMapParticleGroup` + `particle_defs.json` (sale de `Tools/particle_migration.py`) | motor del AO: aditivas, se apagan fuera de pantalla (±16 × ±12) |
| Partículas propias | `particle_migration.DEMO_PARTICLES` (9001+) | se agregan a `particle_defs.json` sin tocar las demás |
| Luz | `AOMapLighting` (Original, por casilla) y `AOLighting2DV283` (Mejorada, Light2D) | ver "Luz" abajo |
| Efectos de hechizo | `AOSpellFXV120` (singleton) | ahí va el pooling de efectos por evento |
| Sonido | `AOAudioV190.SetWeather` (loop 194, cierre 195) | 42 de los 299 sonidos del AO migrados |
| Ajustes | `AOPlayerSettingsV230` | HD, luz, vsync; calidad de efectos: `EffectsQuality` (falta la opción en el menú) |

Diagnóstico completo: `docs/claude/nube/clima-vfx/auditoria.md`.

## Reglas
1. **Composición antes que cantidad.** Profundidad con 3 capas (fondo / medio / frente) repartiendo el mismo presupuesto:
   - **fondo:** chico, lento, tenue, parallax 0,6–0,8;
   - **medio:** normal;
   - **frente:** grande, rápido, desenfocado, pocas.
2. **Todo en el mundo, nada pegado a la pantalla.** Posición en coordenadas del mundo y envoltura alrededor de la cámara. Seguir a la cámara en `LateUpdate` (la cámara se mueve en `LateUpdate`).
3. **Variación coherente, no ruido cuadro a cuadro.** Usar `Mathf.PerlinNoise` o curvas en el tiempo para viento, ráfagas, densidad y frecuencia. Guardar la fase por partícula, no un `Random` cada cuadro.
4. **Viento global único** (`AOWind`, estático): lo leen lluvia, nieve, niebla, humo y polvo. Sin cambios bruscos.
5. **Cero Instantiate/Destroy en régimen:** pools creados al activar y reusados. El apagado desactiva el pool una vez, no cada cuadro.
6. **Luz del clima barata:**
   - **Original:** nunca re-iluminar el mapa (son 11–18k sprites). Usar un quad de pantalla multiplicativo (tinte, oscuridad) y otro aditivo para el relámpago.
   - **Mejorada:** ajustar el color y la intensidad de la luz global.
7. **Bajo techo:** fundido de 0,5 s de la precipitación, la niebla y el volumen de la lluvia. No cortar de golpe.
8. **Calidad:** un solo nivel (`Low / Medium / High / Ultra`) en `AOPlayerSettingsV230`, que da multiplicadores de cantidad, capas y extras. **Nunca cambia gameplay** (colisión, visión, daño).
9. **El vacío negro queda negro:** nada de partículas de área grande que caigan sobre el vacío (la 199 "Espacio Azul" cubre 22 casillas).
10. **Legibilidad:** personajes, nombres, proyectiles y avisos siempre por encima del clima y visibles. El frente de la lluvia va con alfa bajo.
11. **Datos, no código por mapa:** perfiles en JSON (`Resources`) y colocación por el builder o `demo_art_specs`. Nunca `if (mapa == 1)`.

## Presupuestos de referencia (hardware modesto, 60 FPS)
| Efecto | Sprites | Draw calls | Overdraw | CPU/cuadro |
|---|---|---|---|---|
| Lluvia 3 capas | ≤ 200 (x calidad) | 1–3 (mismo material, sin PropertyBlock) | < 0,5 pantalla | ~0,05 ms |
| Salpicaduras | 15–30 (pool) | 1 | mínimo | ~0,01 ms |
| Nieve 3 capas | ≤ 120 | 1–3 | < 0,5 | ~0,04 ms |
| Niebla por bancos | 2 capas de ~12 quads grandes | 2 | 1–2 pantallas a alfa 10–20 % | ~0,02 ms |
| Tinte del clima | 1 quad | 1 | 1 pantalla | 0 |
| Relámpago | 1 quad (solo durante el flash) | 1 | 1 pantalla | 0 |
| Grupo de partículas del AO | `count` (≤ 200) GameObjects | ~1 por partícula (hoy usan PropertyBlock) | según sprite | se apaga fuera de vista |

Si un efecto nuevo supera eso, justificar el costo o recortar.

## Checklist antes de dar un efecto por terminado
- [ ] ¿Existía algo equivalente (tabla de arriba, `particles.ind` con 325 definiciones)? Reusar.
- [ ] Costo estimado (sprites, draw calls, overdraw, CPU) anotado en el reporte.
- [ ] Sin `new`, LINQ, `Find*` ni cadenas en `Update`. Pool creado una vez.
- [ ] Funciona con **Luz Original y Mejorada**, y con **HD encendido y apagado**.
- [ ] Respeta techos, el vacío negro y la legibilidad.
- [ ] Escala con la calidad y se apaga del todo en `Low` si es decorativo.
- [ ] Vista previa sin Unity cuando se pueda (`Tools/hd_remake/dungeon_vista_p1.py`, `dungeon_vista_mapa.py`) y prueba en Unity con la ventana de depuración del clima.

## Errores frecuentes (ya pasaron)
- **Partícula aditiva fija que se quema en blanco:** la 52 "Explosión portal" puesta fija. Las explosiones son para eventos cortos.
- **Luz de color que oscurece:** en Original la luz multiplica; un azul puro tiñe y oscurece. Usar celestes o violetas claros y dar el brillo con un halo pintado.
- **Rectángulo de color en paredes:** en Original cada sprite toma la luz de su casilla de apoyo, estirada en todo su alto. Las luces de color van lejos de donde se apoyan las paredes altas.
- **Luz Mejorada quemada:** varias Light2D juntas suman. La luna (fría y casi blanca) va a 0,55.
- **Lluvia "sobre la pantalla":** gotas hijas de la cámara. Van en el mundo.
- **Emisor corrido:** el origen de la partícula es relativo al pie de la casilla, menos 16 px. Medir dónde está la llama o el foco del sprite.
