# Clima, VFX y rendimiento de Ullathorpe: auditoría (nube, 26/09)

Pedido de Lucas: más atmósfera al menor costo. Prioridad: estabilidad > rendimiento > gameplay legible > coherencia artística > espectacularidad.

Método: lectura del código en `Assets/AOMigrator/Runtime`, de los datos (mapas, `particle_defs`, `map_environment`) y de la configuración (URP, calidad, `.meta`).
- No hay Unity en la nube: nada de esto está medido con el Profiler. Los costos son estimados.
- Vista de lluvia hoy / propuesta: `lluvia_hoy_vs_propuesta.jpg` (`Tools/hd_remake/clima_vista_lluvia.py`) (mismo presupuesto de 200 sprites).

## 1. Arquitectura actual del clima
```
AOWorldManagerV07
 ├─ BuildMapWeather() ── crea AOMapWeather (1 por mapa) con:
 │     map_environment.json (por mapa: rain / snow / fog permitidos, baseLight)
 │     particle_defs.json: lluvia = def 58 (200 gotas, sprite 11x15 de tex_15063)
 │                         nieve  = def 57 (90 copos, tex_126 16x16)
 │                         niebla = fogSprites (tex_131 / tex_132, 512x512)
 ├─ ApplyRainToggle / ApplySnowToggle / ApplyFogToggle   (entradas de los paquetes RainToggle / NieveToggle /
 │                                                        NieblaToggle del servidor original)
 ├─ SyncWorldTime → SetWorldHour → AOMapLighting.DayColor (tabla del AO por hora) → re-ilumina todo el mapa
 └─ AOWorldManagerRoofV210: techos por disparador (1, 4, 16, 20–199) con fundido
AOMapWeather.Update: gotas y niebla hijas de un objeto que sigue a la cámara
AOAudioV190.SetWeather: loop de lluvia (sonido 194) y cierre (195), 2D, volumen 0,3 x Ambiente
AOLighting2DV283 (luz Mejorada): luz global por hora o por baseLight; no mira el clima
```

## 2. Sistemas encontrados
| Sistema | Dónde | Estado |
|---|---|---|
| Lluvia | `AOMapWeather` | Funciona, pero **nunca se prende en el juego** (solo desde QA del Editor). |
| Nieve | `AOMapWeather` | Igual: dormida. |
| Niebla | `AOMapWeather` (2 capas de mosaicos) | Dormida; solo 1 mapa la permite (126). |
| Hora del mundo / día-noche | `AOWorldManagerV07.SyncWorldTime` | **Dormida**: nadie la llama en el juego, la hora queda fija en las 13:00. |
| Permisos por mapa | `map_environment.json` | 524 mapas con lluvia (Ullathorpe sí), 37 con nieve, 1 con niebla. |
| Sonido | `AOAudioV190` | 1 loop de lluvia; sin trueno, sin viento, sin nieve. |
| Techos (zona cubierta) | `AOWorldManagerRoofV210` | Funciona; sirve tal cual como "zona protegida" del clima. |
| Partículas de mapa | `AOMapParticleGroup` + `particle_defs.json` | Funcionan (chimeneas, fogones, faroles, mariposas en Ulla). |
| Servidor online | `OnlineServer` | No manda clima ni hora. |

## 3. Problemas
1. **El clima no existe para el jugador:** sin disparador en el juego ni en el servidor, nunca llueve ni cambia la hora.
2. **"Partículas sobre la pantalla":** las gotas y la niebla son hijas de un objeto que sigue a la cámara.
   - Al caminar, la lluvia viaja pegada a la pantalla.
   - La niebla se desliza con la cámara.
3. **Todo igual:**
   - un solo sprite de gota (una "lágrima" blanca, no un trazo), mismo tamaño, sin capas;
   - deriva fija por gota, sin viento;
   - velocidad al azar pero constante.
   - La nieve es la misma lógica, con otro sprite y otros rangos.
4. **Niebla uniforme:** 2 capas del mismo alfa en toda la pantalla, sin bancos ni claros ni niebla de suelo.
5. **Sin integración con la luz:** llover no cambia el color ni la oscuridad; no hay tormenta ni relámpagos.
6. **Bajo techo sigue lloviendo encima:** las gotas se dibujan en el orden 32000, sobre todo.
7. **Un cuadro de retraso:** el clima sigue a la cámara en `Update` y la cámara se mueve en `LateUpdate`.
8. **Con el clima apagado igual recorre y apaga todas las gotas y mosaicos cada cuadro** (costo chico, pero inútil).

## 4. Funciones incompletas o no migradas
- **Relámpago:** hay partículas 26 "Lightning" y 126 "Lluvia de relámpagos", sin uso; no hay flash ni trueno.
- **Tormenta de arena (59), niebla (172, 173 y 220), fogata (104), humo (10), humo azul (19) e insectos (9):** están en `particles.ind` pero no se migraron. Solo se migra lo que usa algún mapa (105 de 325).
- **Sonidos:** el AO trae 299 y se migraron 42; hay que identificar trueno, viento y ventisca.
- **Paquetes de clima:** el protocolo propio del servidor (protocolo 3) no tiene clima ni hora.

## 5. Reutilización (no crear managers duplicados)
- **`AOMapWeather` pasa a ser el motor de clima** (`WeatherManager`). Ya está creado y configurado por mapa; no hace falta otro.
- **Techos (`roofTriggers` del jugador):** son la `WeatherOcclusionZone`, con datos de mapa que ya existen.
- **`AOPlayerSettingsV230`:** nivel de calidad de efectos (ya guarda HD, luz, vsync y volúmenes).
- **Patrón `SetEnhanced` / `ModeChanged`:** avisar un cambio de calidad.
- **`AOMapParticleGroup`:** humo, chispas y polvo ambiental; el viento solo le suma deriva.
- **`AOSpellFXV120`:** ya es un singleton de efectos (hechizos); ahí va el pooling de efectos por evento.
- **Herramientas de QA del Editor:** `AOMapVisualQA`, `AOMapLoadProfile` y `AOMapLightCycleQA` ya fuerzan clima y hora; la ventana de depuración se apoya en ellas.

## 6. Cuellos de botella (todo el juego, no solo el clima)
**ALTO**
- **Cada casilla lleva su propio bloque de propiedades** (luz Original, `AOWorldManagerV07.cs:1099-1110`) y el shader no es compatible con el SRP Batcher: ~1 draw call por casilla visible (500–1000 en ciudad).
- **Se instancia el mapa entero:** 11k SpriteRenderers en Ulla, 18k en el mapa 59, todos activos y sin activación por distancia.
- **Atlas HD sin comprimir:** 440 MB en Ullathorpe con HD encendido (por defecto); BC7 los baja a unos 110 MB.
- **Las texturas no se liberan nunca al cambiar de mapa** (`textureCache` sin `UnloadUnusedAssets`).
- **FPS sin tope:** vsync apagado por defecto y sin `targetFrameRate`. En notebooks: calor, bajón de frecuencia y tirones.
- **`AOTerrainHDStyleManager`:** recorre todos los SpriteRenderers cada segundo (~160 KB de basura) y carga un registro de 2,4 MB al arrancar, sin efecto real.

**MEDIO**
- La carga de mapa pasa entera en un solo cuadro (JSON de hasta 1,46 MB), y la luz de los techos y árboles se reescribe cada cuadro aunque no cambie.
- Partículas:
  - un GameObject por partícula, creados de golpe;
  - un bloque de propiedades cada una;
  - se habilitan todas cada cuadro.
- HDR prendido.
- **Día/noche:** al activarlo, re-ilumina las 11–18k casillas cada 0,25 s. Hay que resolverlo antes de prenderlo.

**BAJO**
- El clima en sí: 200 gotas con un material y sin bloque de propiedades se agrupan bien. La niebla son hasta 2x96 mosaicos.

**CPU y basura por cuadro (109 scripts: 37 `Update`, 2 `LateUpdate`, 14 `OnGUI`; sin física)**

ALTO:
- **Teclas y atajos:** cada consulta de tecla lee `PlayerPrefs` y arma textos (`AOControlProfilesV260.cs:8-60`). Son unas 25 por cuadro.
- **Barra de atajos (`AOShortcutHUDV260.cs:56-132`):** crea 2 `GUIStyle` por evento.
- **Chat (`AOInterfaceV0101.cs:2298-2313`):** recorta las líneas carácter por carácter en cada evento, y cada mensaje hace un `FindFirstObjectByType`.
- **Terreno HD viejo (`AOTerrainHDStyleManager`):** escaneo cada segundo y `LateUpdate` por mosaico.
- **Mosaicos animados:** un `Update` por mosaico, leyendo `shader.name`.

MEDIO:
- techos y árboles;
- cliente online (LINQ por cuadro y guardado completo cada 1 s);
- panel de misiones;
- textos de la interfaz armados en cada evento.

Sin pooling en ningún lado:
- hechizos, flechas, disparos de habilidad, efectos por turno y botín se crean y se destruyen cada vez;
- el `WaitForSeconds` se crea en cada cuadro de animación.

Tope de FPS: no hay (vsync apagado y sin `targetFrameRate`), así que todo lo anterior se multiplica.

## 7. Calidad visual actual del clima
Funcional y fiel al AO, pero plano:
- lluvia de "lágrimas" iguales pegadas a la pantalla;
- nieve que cae como lluvia lenta;
- niebla como velo uniforme;
- sin viento, tormenta ni cambio de luz;
- no respeta los techos.

## 8. Mejoras posibles (sin más partículas)
Composición y profundidad antes que cantidad:
- **Tres capas, con las mismas 200 gotas repartidas:**
  - fondo: chicas, lentas, tenues, con parallax menor;
  - medio: normales;
  - frente: grandes, rápidas y con desenfoque.
- **Gotas en el mundo, no en la pantalla**, con parallax por capa.
- **Trazos en vez de lágrimas:** sprite de trazo generado por código, con largo según la velocidad.
- **Viento global suave:** ruido coherente, ráfagas con curva, ángulo por capa.
- **Salpicaduras:** 15–30 anillos reutilizados, donde "aterriza" cada gota (vista cenital: aterriza en su punto de vida, no al pie de la pantalla).
- **Niebla por bancos:**
  - textura de ruido suave generada una vez (256², 256 KB) anclada al mundo;
  - densidad que varía por zona, con claros;
  - una capa de suelo aparte.
- **Luz del clima:** un solo quad de pantalla multiplicativo (tinte frío, menos saturación, más oscuro en tormenta). En luz Original no se re-iluminan 11k sprites; en Mejorada se ajusta la luz global.
- **Relámpago:** quad aditivo de pantalla con doble destello irregular, relámpagos lejanos (flash débil sin trueno) y trueno con retraso.
- **Bajo techo:** la precipitación y la niebla bajan en 0,5 s y la lluvia suena más baja.

## 9. Skills
No hay skills externas de Unity, VFX ni clima disponibles (búsqueda vacía). Se crean dos propias del proyecto, con lo encontrado acá:
- `aod-clima-vfx`: motor de clima, partículas, niebla, viento, luces del clima, perfiles, presupuestos y errores frecuentes.
- `aod-rendimiento`: cómo diagnosticar en este proyecto (Profiler, Frame Debugger, memoria), puntos calientes conocidos, presupuestos para hardware modesto y checklist.

No se crean las 14 por separado: repetirían lo mismo y gastan contexto.

## 10. Arquitectura final propuesta
```
AOMapWeather  (= WeatherManager; sigue siendo 1 por mapa)
 ├─ WeatherState actual ──► interpola hacia ─► WeatherProfile objetivo (transición en N s)
 │     intensidad, tipo (lluvia/nieve/ceniza), niebla, viento, tormenta, tinte/oscuridad, sonido
 ├─ Capas de precipitación: fondo / medio / frente  (1 pool, cuentas x calidad; parallax por capa)
 ├─ Salpicaduras (pool)            ├─ Niebla por bancos + niebla de suelo (anclada al mundo)
 ├─ Relámpagos (flash doble + trueno con retraso)    ├─ Quad de luz del clima (tinte/oscuridad)
 └─ Oclusión: techo del jugador (AOWorldManagerRoofV210) → fundido suave
AOWind (estático): dirección, fuerza, ráfagas por ruido coherente → lo leen clima, humo (AOMapParticleGroup) y niebla
weather_profiles.json (Resources): Clear, Cloudy, Drizzle, Rain, HeavyRain, Storm, LightSnow, Snow, Blizzard, Fog, Ash
AOPlayerSettingsV230.EffectsQuality: Low / Medium / High / Ultra → multiplicadores (nunca toca gameplay)
Editor: AOWeatherDebugWindow (forzar perfiles, sliders, partículas activas, viento, FPS, ms, luces)
Disparo en el juego (decisión de Lucas + Servidor): el servidor manda hora y clima → mismo clima para todos
```
Los perfiles van en JSON y no en ScriptableObject porque el proyecto ya es data-driven por Resources JSON y así se editan desde las herramientas (y desde la nube).

## Orden propuesto (mayor mejora visual, menor riesgo)
| # | Cambio | Riesgo | Notas |
|---|---|---|---|
| 1 | Lluvia y nieve en el mundo, 3 capas, trazo y viento, mismas 200 gotas | BAJO | Solo `AOMapWeather`; hoy está dormido, así que no cambia el juego actual. |
| 2 | Niebla por bancos + niebla de suelo | BAJO | Ídem. |
| 3 | Bajo techo: fundido suave | BAJO | Lee los techos que ya existen. |
| 4 | Seguir la cámara en `LateUpdate`; no recorrer lo apagado | BAJO | Arregla el cuadro de retraso. |
| 5 | Ventana "Weather Debug" (solo Editor) | BAJO | No entra en el build. |
| 6 | Calidad de efectos en `AOPlayerSettingsV230` (sin UI aún) | BAJO | Interfaz agrega la opción después. |
| 7 | Perfiles + transiciones + relámpago + quad de luz del clima | MEDIO | Se prueba con la ventana de depuración. |
| 8 | Prender el clima y la hora en el juego (servidor → cliente) | MEDIO | Cambia el protocolo; va junto con el arreglo del re-iluminado. |
| 9 | Arreglos de rendimiento generales (ver 6) | BAJO a ALTO | Tope de FPS y sacar el escaneo HD viejo son BAJO; comprimir HD, MEDIO; mallas por trozos, ALTO. |
