---
name: aod-rendimiento
description: Diagnóstico y optimización de rendimiento de AoDuels/AO BATTLESERVER en Unity 6 URP 2D (FPS, CPU, GPU, draw calls, batching, overdraw, memoria de texturas, GC, carga de mapas, pooling, culling). Usar antes de agregar sistemas visuales grandes, cuando algo tira FPS o tarda en cargar, y al revisar cambios que corren cada cuadro.
---

# Rendimiento (Unity 6, URP 2D)

Objetivo: **60 FPS estables en hardware modesto** (gráfica integrada, 4–8 GB de RAM). Estabilidad primero.

## Puntos calientes conocidos (auditoría del 26/09: `docs/claude/nube/clima-vfx/auditoria.md`)
| Riesgo | Qué | Dónde |
|---|---|---|
| ALTO | Un MaterialPropertyBlock por casilla (luz Original) y el shader fuera de `UnityPerMaterial`: sin batching, ~1 draw call por casilla visible | `AOWorldManagerV07.ApplyMapLight`, `AOMapVertexLit.shader` |
| ALTO | Mapa entero instanciado (11k SpriteRenderers en Ulla, 18k en el mapa 59) | `AOWorldManagerV07.BuildVisuals` |
| ALTO | Atlas sin comprimir: HD ~440 MB en Ulla; nunca se liberan al cambiar de mapa | `.meta` / `AOHDTextureImportV279`, `textureCache` |
| ALTO | FPS sin tope (vsync apagado, sin `targetFrameRate`) | `AOPlayerSettingsV230` |
| ALTO | `PlayerPrefs` y cadenas en cada consulta de tecla (~25 por cuadro) | `AOControlProfilesV260` |
| ALTO | Escaneo de terreno HD viejo cada 1 s | `AOTerrainHDStyleManager` |
| MEDIO | Carga de mapa en un solo cuadro; `OnGUI` que crea estilos y textos por evento; partículas con 1 GameObject y 1 PropertyBlock cada una; techos y árboles que reescriben el color cada cuadro | varios |

## Cómo diagnosticar (en la PC, con el candado de Unity y el respaldo)
1. **Profiler (CPU):** Play en Ullathorpe, 30 s quieto + 30 s caminando.
   - Ordenar por *Self ms*.
   - Buscar `OnGUI`, `Update` de scripts AO* y `GC.Alloc` > 0 en cada cuadro.
   - Mirar la columna *GC Alloc* del hilo principal.
2. **Frame Debugger:** contar draw calls y SetPass.
   - Si cada casilla es su propio draw ("SRP Batcher: node not compatible" o "different MaterialPropertyBlock"), es el punto 1 de la tabla.
3. **Rendering stats (Game view):** Batches, SetPass, Tris. Comparar Luz Original contra Mejorada: Mejorada no usa PropertyBlock.
4. **Memory Profiler:** Textures, ordenado por tamaño. Cambiar 3 veces de mapa y ver si crece (no se libera).
5. **Carga:** `MapLoadMetrics` (log al cargar) y `Editor/AOMapLoadProfile.cs`.
6. **Medir antes y después** con la misma escena, cámara y duración. Anotar ms, draws y MB en el reporte.

## Reglas para código nuevo
- **Por cuadro:**
  - nada de `new`, LINQ, `string` +, `$""`, `ToString()`, `Find*`, `GetComponent`, `Camera.main` ni `PlayerPrefs`;
  - cachear en `Awake`, `Start` o al cambiar el dato.
- **`OnGUI`:** `GUIStyle` y `GUIContent` creados una vez; textos cacheados hasta que cambie el valor; `useGUILayout = false` si no se usa `GUILayout`.
- **Pools:**
  - crear al activar y reusar;
  - activar o desactivar solo cuando cambia el estado, no cada cuadro;
  - nunca `Destroy` en régimen.
- **Batching:**
  - mismo material y textura;
  - sin MaterialPropertyBlock en sprites que se repiten; los datos por objeto van en vertex color o en un global;
  - ordenar para no intercalar atlas.
- **Culling:** lo que está fuera de la cámara más un margen se apaga. Usar la caja real de la cámara, no un número fijo.
- **Corrutinas:** `WaitForSeconds` cacheado; no crear uno por cuadro de animación.
- **Texturas:**
  - Point, sin mipmaps;
  - **comprimir** (BC7 en Standalone) salvo que se vea peor;
  - nunca `isReadable` salvo necesidad;
  - liberar al cambiar de mapa (`Resources.UnloadUnusedAssets` detrás de la pantalla de carga).
- **Cargas grandes:** `Resources.LoadAsync` y construcción repartida en varios cuadros detrás de `AOClassicLoadingV220`.
- **Calidad:** los efectos decorativos escalan con `AOPlayerSettingsV230` y se apagan en `Low`. El gameplay no depende de ellos.

## Riesgo del cambio (siempre decirlo)
- **BAJO:** local, reversible, sin datos ni protocolo; por ejemplo, tope de FPS, cachear estilos, no reescribir lo que no cambió.
- **MEDIO:** toca carga, memoria, import de texturas o un sistema compartido.
- **ALTO:** cambia cómo se dibuja o construye el mapa (mallas por trozos, activación por distancia), el protocolo o los guardados.

## Checklist de revisión
- [ ] ¿Corre cada cuadro? ¿Asigna memoria? ¿Busca objetos?
- [ ] ¿Rompe el batching (material, textura o PropertyBlock nuevos)?
- [ ] ¿Cuánto overdraw suma (pantallas completas con alfa)?
- [ ] ¿Cuánta memoria (texturas nuevas: ancho × alto × 4 bytes sin comprimir)?
- [ ] ¿Se apaga fuera de cámara y en calidad baja?
- [ ] Medido en la PC o, si no se puede, estimado y marcado "sin medir".
