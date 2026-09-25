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

Modelo de amenaza: sala privada por Tailscale con amigos de confianza. Las trampas con un cliente modificado van como gravedad media como mucho. La prioridad son las cosas que **tiran la sala o rompen guardados**.

**Bien hecho:**
- Clave de 144 bits comparada en tiempo constante, que nunca aparece en logs.
- Token por personaje guardado como SHA-256.
- Cada acción queda atada a la sesión, así que no se puede suplantar a otro jugador.
- Topes de tamaño, profundidad y ritmo de mensajes.
- `world.json` con `.tmp`, `File.Replace` y `.bak`. Sin rutas armadas con nombres, así que no hay path traversal.
- Diario de eventos que evita duplicados.

1. **ALTA · REPRODUCIDO · Servidor.** Un `hello` o un guardado con un campo ilegible apaga la sala para todos (sesión fantasma con mapa 0 y el ticker hace `stop.Cancel()`). También queda guardado y vuelve a pasar al reconectar.
   → **Parche y prueba listos:** `parches/servidor-sesion-fantasma.md`.
2. **ALTA si el puerto se ve fuera de Tailscale · POSIBLE · Servidor + Lucas.** `Program.cs:17` escucha en `IPAddress.Any`.
   - El `hello` no tiene plazo total: 60 s por byte, 32 permisos. Cualquiera que llegue al 7777 (LAN de casa si el firewall lo permitió en "Privada", o un reenvío de puertos) ocupa los 32 permisos mandando 1 byte cada 50 s, y nadie más puede entrar. Además la clave viaja en texto plano.
   - Arreglo: opción `--bind` (127.0.0.1 + la IP de Tailscale), plazo total de unos 5 s para el `hello` y tope de conexiones por IP antes de autenticar.
   - Mientras tanto, del lado de Lucas: regla de firewall solo para `100.64.0.0/10` y ACL de Tailscale que permita solo `tcp:7777` (ver `guia-amigos.md`).
3. **MEDIA · POSIBLE · Servidor.** Si falla un guardado (`world.json` bloqueado por OneDrive, el antivirus o `aod_backup.ps1`):
   - Dentro del tick, se cierra el servidor. Lo cubre el parche 1.
   - Dentro de `Leave`, no se liberan la conexión ni el permiso.
   - Arreglo: `Save` con reintento (registrar y dejar `dirty`), try/finally anidado en `Handle` y try/catch del accept dentro del `while`.
   - Recomendado: `Saves` fuera de OneDrive.
4. **MEDIA · CONFIRMADO · Servidor.** El botín del piso nunca expira ni tiene tope (`CoopRoom.cs:383-390`). Con uso normal crece para siempre dentro de `world.json` y del mensaje de estado. Con muchos `drop` el estado pasa de 1 MB y desconecta a todos los del mapa.
   - Arreglo: expiración de 5–10 min y tope de unos 200 por mapa.
5. **MEDIA · CONFIRMADO · Servidor.** Guarda el mundo entero en cada acción, bajo el candado (`CoopRoom.cs:129`). Con 11 jugadores son unos 15 guardados completos por segundo, más lag y OneDrive subiendo el archivo sin parar.
   - Arreglo: guardar con `dirty` cada 1–2 s, y al instante solo en compra, venta, recoger y soltar.
6. **MEDIA · CONFIRMADO · Servidor.** Altas de personaje sin límite (`CoopRoom.cs:72-80`). Con la clave se puede inflar `world.json` o reservar los nombres de los amigos.
   - Arreglo: tope de unos 30 personajes y registrar cada alta en consola.
7. **MEDIA · CONFIRMADO · Servidor.** El oro, el inventario, el nivel y los hechizos salen del snapshot del cliente. Un cliente modificado puede comprar con oro inventado, vender objetos que no tiene o vaciar el stock limitado, que además no se repone.
   - Esto va con el libro contable de `red.md` (fase 2). Sumar reposición del stock.
8. **MEDIA · CONFIRMADO · Servidor.** `Harmful()` (`CoopRoom.cs:517`) no mira `eotId` ni hambre, sed o carisma negativos. Un cliente modificado puede dañar a un amigo con un DoT: PvP por la puerta de atrás.
   - Arreglo: lista blanca de hechizos permitidos sobre aliados.
9. **MEDIA · CONFIRMADO · Servidor.** No hay control de velocidad de movimiento. El alcance de un hechizo se mide desde la posición reportada y no desde el NPC, y el maná no se verifica.
10. **MEDIA · CONFIRMADO · Servidor.** Las mascotas las declara el cliente, y el enfriamiento usa el `id` que manda el cliente, así que se puede saltear.
11. **BAJA · POSIBLE · Interfaz.** El chat puede tener texto enriquecido (`<size>`, `<color>`) si `GUI.skin.label` lo trae activado (`AOInterfaceV0101.cs:405`), y hay nombres con letras de otro alfabeto que parecen iguales.
    - Arreglo: `chatStyle.richText = false` y normalizar los nombres.
12. **BAJA · CONFIRMADO · Servidor.** `Event` lanza una excepción desde 256 eventos pendientes dentro del tick (lo cubre el parche 1). Un cliente que nunca confirma eventos queda invulnerable a los NPC.

**Orden sugerido:** parche 1 (ya), firewall y ACL (Lucas, 5 min), 3, 4 y 5 antes de una sesión larga, y 7 a 10 con la fase 2 (libro contable).
