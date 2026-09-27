# Actualización para todos los sectores: lluvia nueva, sombras y luz viva (nube, 27/09)

CEREBRO: pasale a cada sector su parte (SendMessage). Contenido está archivado: su parte la asignás vos.
- **Rama:** `origin/claude/nifty-thompson-r3ulpf`. Paquete 4: commits `2118ec6` … el del mensaje. Sigue al paquete 3 (`75a7f78`, clima V290, que ya integraste).
- **Todo es visual y local:** no hay protocolo, guardados, datos de mapas ni gameplay. Compila fuera de Unity (runtime y Editor); **falta compilar en Unity y probar en Play**.

## CEREBRO
Integrar como siempre:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/aod_unity_lock.ps1 take -Sector "CEREBRO" -Motivo "merge de la nube (luz V291-V292)"
git fetch origin; git merge origin/claude/nifty-thompson-r3ulpf
```
- **Archivos compartidos:** el merge toca 3 líneas de `docs/claude/tablero.md` ("Próxima versión libre" → V293 y dos líneas en "Hecho"). Si tenés cambios sin commit ahí, commitealos antes.
- **Después:**
  1. Compilar en Unity.
  2. `aod-verificar`.
  3. `aod-respaldo` antes de Play.
  4. Probar con **AO Migrator > Clima (depuración)**, en Ullathorpe.
  5. Repartir.
- **Riesgo: MEDIO.** Aparece un objeto nuevo solo al arrancar ("AO Sky v291": cielo, sombras y luz viva) que dibuja encima del mapa al aire libre. Si molesta, se desactiva borrando ese objeto en Play.
- **Leé:** `docs/claude/nube/clima-vfx/luz-v291.md` (investigación, qué hace, costo, cómo probar y la ronda 2) y la sección 27/09 de `paquete1.md` (lluvia nueva).
- **Tu trabajo de luces:** tu trabajo de luces y día/noche (pedido `pc/luces-tormenta`, todavía sin subir) puede superponerse con esto. Subilo para compararlo y elegir con Lucas.

## Arte (dueño visual)
- **Lluvia** (`AOMapWeather`, `AOMapWeatherCharacterV290`):
  - gotas proporcionales al personaje (como mucho 1/3 de su altura, 1–2 px de ancho);
  - caída vertical en lluvia y tormenta;
  - gotitas que rebotan en la cabeza y los hombros del jugador.
- **Sombras de personajes** (`AOCharacterShadowsV291`):
  - silueta al lado opuesto de la luz dominante: sol o luna por hora, o farol cercano;
  - largo de 0,5 a 1,3 veces la altura; giro en pasos de 5°; mancha de contacto;
  - ordenada debajo del personaje en su fila.
- **Cielo** (`AOSkyV291`):
  - sol y luna por hora;
  - nubes que se mueven con el viento y tapan sol y luna;
  - noche Purkinje: fría, más azul arriba;
  - luz de luna solo en los claros;
  - rayos entre nubes.
- **Luz viva** (`AOLivingLightV292`):
  - halos que parpadean en los faroles y antorchas del AO;
  - personajes teñidos del color del farol cercano;
  - relámpagos con destello y sombra dura;
  - luciérnagas de noche.
- **Qué revisar en Play:** hora 7, 13, 17:30 y 23 (botones de la ventana), nubes, relámpagos, calidad Baja y Alta, Luz Original y Mejorada. Los valores están comentados en cada archivo.
- **Vistas previas:** `docs/claude/nube/clima-vfx/luz_v291.jpg`, `lluvia_v290.jpg` y `lluvia_rebote_v290.jpg`.

## Interfaz y Controles
- La opción "Calidad de efectos" del menú (pedido anterior) ahora también controla:
  - nubes y luna (Media+) y rayos (Alta+);
  - sombras: Baja solo el jugador, Media 16, Alta 32, Ultra 48;
  - halos de faroles y luz sobre personajes (Media+);
  - luciérnagas (Alta+);
  - rebotes de lluvia (Media+).
- Nada nuevo en pantalla para el jugador: sin textos ni teclas.

## QA y Releases
Con candado y respaldo, en Ullathorpe y un dungeon:
1. Ventana de depuración:
   - horas 7, 13, 17:30 y 23: las sombras caen al lado opuesto del sol o la luna;
   - de noche junto a un farol: la sombra se aleja de la llama y el personaje toma su color;
   - nubes en 0,4 y en 0,8;
   - Tormenta y "Relámpago ahora";
   - Nieve;
   - calidad Baja y Alta.
2. El clic en NPC tiene que seguir igual: las sombras no son hijas del personaje.
3. En dungeon no hay cielo, solo sombras y halos de antorchas.
4. FPS y ms con Tormenta en Alta y en Baja.
5. Regresión: `test_modules_unity.py`, el perfil de carga y el Editor.log sin errores.

## Programación
- **Módulos nuevos:** `AOSkyV291`, `AOCharacterShadowsV291`, `AOLivingLightV292` y `AOMapWeatherCharacterV290`.
- **Parciales de solo lectura:** `AOWorldManagerSkyV291` (`CurrentBaseLight`, `CurrentMapOutdoor`, `IsRoofAt`) y `AOWorldManagerWeatherV290` (`PlayerTransform`).
- No se editó `AOCharacterRenderer`, `AOWorldManagerV07`, `AOMapLighting` ni `AOLighting2DV283`.
- **Ojo:** las sombras buscan personajes cada 1,5 s con `FindObjectsByType`. Si hay un registro de personajes, conviene usarlo.
- **Pendiente de Lucas:** activar día/noche en el juego (hoy la hora queda fija en 13). Antes hay que resolver el pico al re-iluminar el mapa (auditoría §6).

## Servidor y Multiplayer
Nada por ahora. Si se activa el día/noche, la hora va por `SyncWorldTime` y el cielo la sigue solo.

## Contenido y Fidelidad AO (lo asigna CEREBRO)
- **Sonido de trueno:** buscar en el AO original su id para los relámpagos. Hoy no hay ninguno entre los migrados.
- **Nieve:** confirmar si suena en el AO (pedido anterior).
- **Faroles:** la luz blanca del AO se tiñe a un cálido de farol solo en el halo y en la luz sobre personajes; la luz del mapa no cambia.
