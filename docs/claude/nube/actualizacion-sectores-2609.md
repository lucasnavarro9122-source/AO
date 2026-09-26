# Actualización para todos los sectores: clima, VFX y rendimiento (nube, 26/09)

CEREBRO: pasale a cada sector su parte (SendMessage a "AO BATTLESERVER: <Sector>"). Contenido está archivado: su parte la asignás vos.

- **Rama:** `origin/claude/nifty-thompson-r3ulpf`. Paquete 3: commits `c923f82` … el del mensaje. Sigue a `1f5b4d7`, el paquete 2 del P1 (`actualizacion-sectores-2509.md`).
- **Contenido del paquete:**
  1. La auditoría de clima, VFX y rendimiento de Ullathorpe.
  2. Dos skills nuevas.
  3. El clima V290, aprobado por Lucas: paquetes 1 y 1b.
  4. La respuesta sobre el gasto de Higgsfield.

## CEREBRO
Integrar:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/aod_unity_lock.ps1 take -Sector "CEREBRO" -Motivo "merge de la nube (clima V290)"
git fetch origin; git merge origin/claude/nifty-thompson-r3ulpf
```
- **Este merge toca archivos compartidos:**
  - `docs/claude/tablero.md`: 3 líneas. "Próxima versión libre" pasa a V291, dos pedidos nuevos y una línea en "Hecho".
  - `CLAUDE.md`: una línea en "Skills del proyecto".

  Si tenés cambios sin commit en esos archivos, commitealos antes. Si hay conflicto, quedate con lo tuyo y sumá esas líneas.
- **Después:**
  1. Compilar en Unity: consola sin `error CS`.
  2. `aod-verificar`.
  3. `aod-respaldo` antes de entrar en Play.
  4. Liberar el candado.
  5. Repartir.
- **Riesgo para el 0.27: bajo.** El clima sigue dormido en el juego (solo lo activa QA), el contrato público de `AOMapWeather` no cambió y no se tocaron mapas, protocolo ni guardados.
- **Leé:** `docs/claude/nube/clima-vfx/paquete1.md` (qué cambió, cómo probarlo, costo y riesgo) y `docs/claude/nube/clima-vfx/auditoria.md` (diagnóstico completo, con riesgo BAJO/MEDIO/ALTO por punto).

**Decisiones de Lucas que siguen abiertas** (no repartir como tareas todavía):
1. Tormenta completa: nubes, oscurecer, viento, lluvia, relámpagos y trueno.
2. Activar el clima y la hora en el juego (servidor).
3. Arreglos de rendimiento de la sección de Programación.

## Arte (dueño visual del clima y los efectos)
- **Clima V290:**
  - lluvia y nieve en el mundo, en 3 capas con parallax y viento;
  - salpicaduras;
  - agua que escurre por los techos y gotea del alero;
  - las 2 capas de niebla del AO ancladas al mundo, más niebla de suelo;
  - tinte de día de lluvia que multiplica (el negro sigue negro).

  Misma cantidad que el AO (200 gotas, 90 copos).
- **Qué revisar en Play:** menú **AO Migrator > Clima (depuración)**, en Ullathorpe:
  - si los trazos de lluvia, el anillo de la salpicadura y la niebla de suelo quedan bien con el HD y con Luz Original y Mejorada;
  - los valores de cada capa están en las tablas `RainLayers` y `SnowLayers` de `Runtime/AOMapWeather.cs`.

  Vista previa sin Unity: `docs/claude/nube/clima-vfx/lluvia_v290.jpg` (`Tools/hd_remake/clima_vista_v290.py` arma el GIF).
- **Skill nueva `aod-clima-vfx`:** qué existe, reglas, presupuestos, checklist y errores conocidos. Usala antes de sumar humo, fuego, polvo o partículas.
- **Techos de otros mapas para el agua:** `python Tools/roof_flow.py 1 34 59` (o `--todos`) regenera `Resources/AOMigrator/WorldV07/roof_flow.json`. Hoy solo está Ullathorpe: 20 techos, 8 KB.
- **Sin cambios de arte del original:** la gota del AO (`tex_15063`) se reemplazó por un trazo generado por código. Lo aprobó Lucas.

## Interfaz y Controles
- **Pedido:** opción "Calidad de efectos" en el menú de opciones, con Baja, Media, Alta y Ultra.
  - Escribe `AOPlayerSettingsV230.EffectsQuality` (enum `AOEffectsQuality`, en PlayerPrefs con el prefijo de siempre; por defecto Alta = cantidad del AO).
  - Solo cambia clima y efectos decorativos, nunca el gameplay. El cambio se aplica en caliente (evento `EffectsQualityChanged`).
- **Pendiente de Lucas:** la auditoría también lista puntos de rendimiento en la interfaz (`auditoria.md` §6: estilos de `AOShortcutHUDV260` por evento y recorte del chat en `AOInterfaceV0101`). Los propone, no los asigna.

## QA y Releases
Con candado, respaldo y Ullathorpe; los pasos están en `paquete1.md` → "Cómo probarlo".
- **Ventana de depuración:** probar Lluvia, Lluvia fuerte, Tormenta, Niebla, Nieve y Ventisca. En Ullathorpe se habilitan solas.
- **Comprobar:**
  - la lluvia queda en el mundo al caminar;
  - salpicaduras en el piso;
  - agua en los techos que gotea del alero;
  - entrar a una casa: adentro no llueve y el sonido baja.
- **FPS/ms:** Lluvia fuerte en Alta y en Baja. La ventana muestra FPS, ms y contadores (gotas, salpicaduras, agua en techos, mosaicos de niebla).
- **Regresión:**
  - `test_modules_unity.py`;
  - `AOMapVisualQA` / `AOMapLoadProfile`: `PrecipitationVisible` sigue siendo verdadero a los 2 cuadros de `SetState`;
  - Editor.log sin errores.
- **Profiler, cuando haya lugar:** medir los puntos ALTO de `auditoria.md` §6 (draw calls con Luz Original, memoria de texturas HD al cambiar de mapa, basura por cuadro). La receta está en la skill nueva `aod-rendimiento`.

## Programación
- **Archivos:**
  - `Runtime/AOMapWeather.cs` reescrito (mismo contrato público);
  - parciales nuevas, que no editan los archivos de otros: `AOWorldManagerWeatherV290` (lee `PlayerRoofTrigger`, `PlayerUnderRoof`, `TriggerAtWorld`) y `AOAudioWeatherV290` (`AOAudioV190.SetWeatherLevel`);
  - `AOWindV290` (viento global);
  - `AOEffectsQualityV290` (calidad Low–Ultra);
  - `AOMapWeatherRoofsV290` (agua en techos);
  - Editor `AOWeatherDebugV290`.

  Versión tomada: V290 (la próxima libre es V291). Compila fuera de Unity; **falta compilar en Unity**.
- **Skill nueva `aod-rendimiento`:** puntos calientes conocidos, cómo diagnosticar y reglas para código que corre cada cuadro.
- **Pendiente de Lucas** (la auditoría los propone, no los asigna):
  - **Bajo riesgo:**
    - tope de FPS (hoy no hay `targetFrameRate` y el vsync está apagado);
    - sacar el escaneo de `AOTerrainHDStyleManager` (cada 1 s);
    - cachear `AOControlProfilesV260` (lee PlayerPrefs en cada tecla, unas 25 veces por cuadro).
  - **Medio o alto:**
    - una luz Original sin PropertyBlock por casilla;
    - mapa por trozos;
    - liberar texturas al cambiar de mapa;
    - comprimir los atlas HD;
    - pooling de hechizos y proyectiles.

## Servidor y Multiplayer
- **Nada que hacer ahora:** no cambian el protocolo ni el catálogo.
- **Más adelante, si Lucas activa el clima:**
  - mandar a los clientes los paquetes que ya existen del AO: `RainToggle`, `NieveToggle` y `NieblaToggle` (del lado cliente, `AOWorldManagerV07.Apply*Toggle`);
  - sincronizar la hora (`SyncWorldTime`).

  Antes hay que resolver el pico al re-iluminar el mapa al cambiar la hora (auditoría §6).

## Contenido y Fidelidad AO (lo asigna CEREBRO)
Para validar la fidelidad:
- La nieve ya no hace sonar el loop de lluvia 194; la versión anterior en Unity lo hacía sonar también con nieve. Confirmar en el AO original si la nieve suena o es silenciosa.
- Las 2 capas de niebla del AO conservan dibujo, alfa y sentido de movimiento, pero ahora quedan fijas en el mundo en vez de moverse con la pantalla.
