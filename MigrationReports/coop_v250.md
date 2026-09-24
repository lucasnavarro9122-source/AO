# Alpha cooperativa 0.25 — protocolo 2

## Entregado

- Servidor privado para once conexiones: anfitrión y diez amigos.
- Mundo compartido en los 842 mapas importados: NPC, combate básico físico y mágico, muerte, reaparición, puertas y botín único.
- Grupo automático de jugadores vivos a 12 casillas. EXP repartida y crédito individual de misión por cada muerte compartida.
- Compras y ventas con stock compartido. Transferencia de objetos mediante el suelo; objetos equipados, de novato o intransferibles quedan protegidos.
- Nombres, nivel, vida, equipo y mascotas visibles. Curación y resurrección de aliados; sin PvP.
- Progreso de cada personaje guardado en el anfitrión: inventario, banco, oro, EXP, nivel, habilidades, hechizos y misiones.
- Diario de transacciones y recompensas, confirmaciones y reconexión para evitar duplicaciones. Recompensas sin espacio quedan pendientes.
- El cliente pausa acciones si se desconecta. El guardado local se conserva separado del personaje online.

## Evidencia

- `Tools/test_coop_server.py`: dos clientes, muerte compartida, reparto de EXP, botín único, solicitud repetida, transferencia, comercio, puertas, curación aliada, rechazo de PvP, persistencia tras reinicio, recuperación de recompensas sin confirmar y límite de once clientes.
- Compilación .NET: sin errores ni advertencias.
- Compilación de todos los scripts de ejecución Unity: sin errores; permanecen advertencias previas del proyecto.
- `Tools/test_coop_unity.py`: aprobado en Unity abierto. Un cliente Unity y un compañero simulado por TCP, NPC replicados, soltar/recoger sin duplicar, puerta compartida y panel Grupo. No sustituye una prueba con dos clientes gráficos a distancia.
- `MigrationReports/unity_coop_v250.json`: `passed=true`, etapa 7.
- Los siete archivos de guardado originales conservan su SHA256 tras la prueba.
- Build Windows completado con Unity 6000.3.17f1. El primer intento tuvo un bloqueo de `Temp/BurstOutput`; se apartó esa carpeta con respaldo y el segundo terminó correctamente.
- ZIP verificado por CRC. La DLL del paquete coincide por SHA256 con el build y contiene el protocolo cooperativo; no incluye clave de sala ni guardados del servidor.
- Servidor publicado comprobado desde esta PC en `127.0.0.1` y su dirección Tailscale. Rechaza correctamente clientes de protocolo 1. Esto no demuestra todavía acceso desde otra PC.

## Límites y siguiente prueba

Esta alpha es para amigos de confianza. Parte del estado personal todavía proviene de Unity; no ofrece protección completa contra clientes modificados. Clanes, PvP, oficios completos y algunos efectos mágicos avanzados sobre NPC no están implementados en la sala. Los efectos no soportados se rechazan explícitamente.

Cambios personales se envían cada segundo. Un cierre brusco puede perder el último segundo no enviado; transacciones y recompensas confirmadas tienen recuperación mediante diario.

Próxima validación: dos PC reales conectadas por Tailscale. Crear personajes distintos, entrar al mismo mapa, matar un enemigo juntos, repartir objetos, comerciar, reconectar y reiniciar el anfitrión. Verificar progreso y latencia antes de invitar a los diez amigos.

## Actualizaciones

Conservar `Release/Server/Saves`, `room-key.txt` y las identidades de PlayerPrefs. Sustituir cliente y servidor juntos; todos necesitan protocolo 2. El paquete de protocolo 1 queda como respaldo y no se conecta a esta sala.

## Corrección 0.25.1: tableros de misiones inmóviles

El servidor interpretaba el modo de movimiento 0 como movimiento aleatorio. Se respetan ahora los modos estacionarios 0, 1 y 3 tanto al caminar al azar como al perseguir objetivos. Los siete tableros de misiones del catálogo usan modo 0. Al cargar un mundo anterior, los NPC estacionarios recuperan posición y orientación originales.

`Tools/test_static_npcs.py` verifica que los tres modos permanezcan inmóviles, que los NPC con recorridos sí caminen y que se reparen guardados desplazados sin alterar personajes. Compilación sin errores ni advertencias. Servidor publicado y activo; tablero de Ullathorpe restaurado en 60,56. Los dos personajes guardados y la clave de sala se conservaron exactamente. Respaldo previo en `Release/Server-backup-before-v251-20260923-232422`.

El cliente 0.25 y su ZIP siguen siendo compatibles: protocolo 2. Esta corrección requiere solamente actualizar el servidor.
