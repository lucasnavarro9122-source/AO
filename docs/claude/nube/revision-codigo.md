# Revisión de código en la nube (25/09)

Solo lectura, sin Unity. Revisa el código **commiteado** en `384fe18` (V260–V269 contra `d69daef`). El trabajo de la noche del 24/09 hecho en la PC no está incluido. Antes de arreglar, confirmar que la línea sigue igual en la PC.

Formato: gravedad · CONFIRMADO/POSIBLE · dueño.

## Gameplay y magia (Programación)

**Resultado general:** sin fallas graves nuevas.
- La refactorización a `CommitCastResources` cobra maná, energía y enfriamiento una sola vez, tanto en skill shots como en casteo normal.
- Meditación y efectos persistentes limpian bien sus objetos.
- `CanStep` quedó conectado en los dos caminos de movimiento.

Aparte, el bug conocido de los EOT: el parche está en `parches/eot-dano-magico-doble.md`. **Pasa también en NPC**, no solo en el jugador.

1. **Media · POSIBLE · Programación.** `AOSkillShotProjectileV267.cs:141-150` (`FindNpcHit`/`NpcsThisFrame`) busca NPC en toda la escena, sin filtrar por mapa.
   - Hoy no pasa porque hay un solo mapa cargado. Con varios mapas o interiores cargados a la vez (los de la demo), un proyectil podría pegarle a un NPC de otro mapa con las mismas coordenadas.
   - Arreglo: descartar los NPC cuyo grid no sea el del proyectil.
2. **Baja · CONFIRMADO · Programación (revisar si es intencional).** `AOPlayerMagicV120.cs:283` (`TryTargetMouse`) marca el clic como usado antes de validar mapa, maná, rango y agua.
   - Si el hechizo falla, ese clic tampoco mueve ni ataca (`AOActionBarV260.cs:62`).
   - En el AO original, el clic con el cursor de hechizo tampoco mueve, así que probablemente está bien. Confirmar con el perfil MOBA.
3. **Baja · CONFIRMADO · Arte (rendimiento).** `AOSpellFXV120.cs:9` llama a `AOCastAnimationRuntimeV268.PlayNearestNpc` en cada hechizo, también los del jugador, y eso recorre todos los NPC de la escena.
   - Con muchos NPC puede dar microtirones.
   - Arreglo: llamarlo solo cuando el que lanza es un NPC, pasando el caster en vez de buscarlo por posición.

## Interfaz y controles (Interfaz / QA)

**Resultado general:** sin riesgo para guardados ni PlayerPrefs del usuario.
- No hay `DeleteAll`.
- `RestoreKeys()` solo borra teclas del perfil de controles.
- Las pruebas QA usan prefijo temporal y `ProtectLocalSave`.
- El HUD de depuración de `AOTestPlayer` quedó en `#if UNITY_EDITOR`.

4. **Baja · CONFIRMADO · Interfaz.** `AOActionBarDragDropV261.cs:255-266`: al soltar un hechizo en la barra se ignora el error de `SetSpellMacros`.
   - Si la tecla choca con otra acción, el jugador ve "Hechizo asignado a …" pero las macros siguen apagadas, sin ningún aviso.
   - Arreglo: mostrar el error con `AOInterfaceV0101.PushMessage`.
5. **Baja · POSIBLE · Interfaz.** `AOActionBarDragDropV261.cs` (`BeginPotentialDrag`/`Update`) no revisa `InputCaptured` ni los modales antes de arrastrar (solo el clic derecho lo hace).
   - Si un modal deja la barra visible, se puede arrastrar un slot a través del modal.
   - Arreglo: el mismo chequeo de `AOActionBarV260.Update`.
6. **Baja · POSIBLE · Interfaz.** `AOInterfaceClassicControlsV200.cs:93-108`: al reasignar una tecla, cualquier clic de mouse en cualquier parte de la pantalla se toma como la tecla nueva.
   - Arreglo: aceptar clics solo dentro del diálogo, o agregar un botón Cancelar.
7. **Baja · POSIBLE · QA.** `AOPlayerSettingsV230.TestPrefixOverride` no se limpia al terminar `AOControlsQA260`/`AOModulesQA270`.
   - Con la configuración actual (`EnterPlayModeOptions = 0`, recarga el dominio) se limpia al entrar a Play, así que el impacto es mínimo.
   - Si alguien desactiva la recarga de dominio, las teclas "se resetean" hasta reiniciar Unity.
   - Arreglo: poner `null` en `Finish()` y `AbortAfterReload()`.
8. **Baja · CONFIRMADO · Interfaz.** El menú dice "Sala privada: hasta 10 jugadores." (`AOMainMenuV140.cs:390`), pero entran 11 (anfitrión + 10). Unificar el texto.
9. **Info · QA.** `AOOnlineBuildV240.cs:9-27`: el marcador `Temp/restart_online_editor` guarda escenas y assets sin preguntar y cierra Unity.
   - Está documentado en `.claude/rules/online.md`. Riesgo: un marcador olvidado guarda cambios de escena no deseados.

## Seguridad del servidor (Servidor)

(pendiente)
