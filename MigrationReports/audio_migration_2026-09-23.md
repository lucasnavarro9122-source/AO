# Audio de Argentum en Unity — 2026-09-23

## Implementado

- La cámara jugable agrega un `AudioListener` si no existe otro. Antes, la escena jugable no tenía ninguno.
- Los 52 botones IMGUI reproducen el clic original `500`.
- Los pasos alternan sonidos según el terreno o zona del mapa: bosque `201/69`, nieve `199/200`, desierto `197/198` y ciudad/mazmorra `23/24`.
- La precipitación reproduce el ambiente `194` en bucle y el final `195`. El cambio de mapa detiene el ambiente anterior.
- Los fallos de ataque reproducen `2`. Los consumibles usan su `sound1` real, cuando lo tienen; el pulso visual ya no dispara por error el sonido de armadura.
- Se incorporaron 41 efectos generales originales y 18 sonidos adicionales de NPC, conservando las referencias de sonidos de combate, magia, muerte y botín que ya funcionaban en el código.

## Comprobación

`unity_audio_qa.json` registra la ejecución automática en Play: un `AudioListener`, los clips importados, reproducción de clic y ambiente de lluvia. La prueba verifica actividad de `AudioSource`; la salida física de los parlantes depende del equipo.

## Pendiente por falta de recursos o detalle del motor original

- Música por mapa: los archivos `ost_*.ogg` que espera el cliente original no están en `Archivos Originales`. Hay MIDI y un MP3 aislado, pero no equivalen automáticamente a esa banda sonora.
- Hechizos: faltan en los originales los sonidos `163`, `229`, `234`, `239`, `242`, `253`, `255` y `256`. Las demás referencias cargan desde Unity.
- NPC: faltan en los originales `214` y `215`. El resto de los sonidos abiertos/cerrados definidos por `city_npcs.json` carga desde Unity.
- Los pasos se eligen por metadatos del mapa, todavía no por la textura de cada casilla ni por embarcación.
- El ambiente interior/exterior y el sonido al pasar el cursor sobre botones (`501`) aún no están conectados.

Fuente local: `Archivos Originales/Recursos-master/Recursos-master`.
