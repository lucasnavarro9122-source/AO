# AO Online: alpha cooperativa (servidor en protocolo 3)

Servidor privado .NET 10 para el anfitrión y diez amigos. Se ejecuta dentro de Tailscale, en TCP 7777.
El código actual usa el **protocolo 3**: el oro lo lleva el servidor. La 0.26 publicada (`Server-next-v260`) sigue en protocolo 2.

## Oro del servidor (protocolo 3)

- Billetera y banco de cada personaje en `Saves/ledger.jsonl`: un libro de solo agregar, con una línea por movimiento, que se escribe a disco antes de responder. El oro que manda el cliente en su guardado se ignora.
- La primera conexión con el servidor 3 abre las cuentas con el oro que tenía el personaje; es la única vez que se confía en el cliente.
- Cada operación tiene una clave única (`op`), así que un pedido repetido, una reconexión o un reinicio nunca pagan dos veces. Si el servidor se corta a mitad de una escritura, la línea incompleta se descarta al arrancar y queda guardada aparte.
- **No borrar ni editar `ledger.jsonl`**: se respalda junto con `world.json`.

## Instancia de la demo AO BATTLESERVER

Es el mismo ejecutable con otras reglas y otros datos, así que nunca toca las partidas reales:

```powershell
.\AOOnlineServer.exe --demo --data SavesDemo --port 7778
```

- `--demo` activa los retos (arenas del mapa 1001), la EXP por tramo y el oro ×2 (`AODemoRates`).
- `SavesDemo` tiene su `world.json` con `.bak` y su propio `ledger.jsonl`.
- El cliente se conecta al 7778 cuando se elige "Demo AO BATTLESERVER" en el menú.
- Firewall del anfitrión (una sola vez, PowerShell como administrador). Solo acepta direcciones de Tailscale:
  `New-NetFirewallRule -DisplayName "AO BATTLESERVER demo 7778" -Direction Inbound -Protocol TCP -LocalPort 7778 -RemoteAddress 100.64.0.0/10 -Action Allow`
- Tailscale no necesita nada más: los amigos usan la misma IP de Tailscale del anfitrión, con el puerto 7778.

## Seguridad y límites

- `--bind auto` (por defecto): escucha solo en 127.0.0.1 y en las IP de Tailscale de la PC (100.64.0.0/10). Si no encuentra Tailscale, escucha en todas las interfaces y lo avisa en la consola. `--bind any` escucha en todas; `--bind ip1,ip2`, en esas.
- El saludo (`hello`) tiene 5 s de plazo. Como mucho 4 conexiones sin saludar por dirección y 32 en total.
- Como mucho 30 personajes guardados por sala; cada alta queda anotada en la consola.
- El botín del piso desaparece a los 10 minutos, con un tope de 200 objetos por mapa (se va el más viejo).
- El stock limitado de los comerciantes vuelve 30 minutos después de la primera compra.
- `world.json` se guarda al instante solo en compra, venta, recoger, soltar, banco, misiones y pociones; el resto, cada segundo. Si OneDrive o el antivirus lo bloquean, se registra y se reintenta, sin cortar a nadie. El oro nunca depende de `world.json`: va en el libro.
- Un error dentro del mundo se registra y la sala sigue.

## Modo prueba

- `--test`: escucha solo en 127.0.0.1 y habilita `testLedger` (saldos e invariante del libro). Nunca se usa en la sala real.
- `--test-time-scale 0.05` (solo junto con `--test`): acelera los tiempos de los retos (invitación, conteo, máximo y gracia).

## Progreso compartido

- Una simulación de NPC por mapa: posición, vida, combate, muerte y reaparición.
- Grupo automático: jugadores vivos del mismo mapa a un máximo de 12 casillas reciben EXP repartida. Cada uno recibe crédito de misión por la muerte; los requisitos de cada misión siguen siendo individuales.
- El botín y el oro de enemigos existen una sola vez. El primero que recoge conserva el objeto. El panel Grupo permite soltar objetos transferibles para otro jugador.
- Puertas y stock limitado de comerciantes compartidos. Compras y ventas se confirman en el servidor.
- Nombre, nivel, vida, equipo, espíritu, mascotas y chat visibles entre compañeros. Se permiten curación y resurrección de aliados; no hay PvP.
- Guardados por personaje en `Saves/world.json`, con respaldo `.bak`. Incluyen nivel, EXP, habilidades, inventario, oro, banco, hechizos, misiones y ubicación.

El servidor conserva un diario ordenado de recompensas y transacciones hasta que el cliente confirma haberlas guardado. Recoger, entregar o reconectar no debe duplicar objetos. El cliente envía su estado cada segundo y al realizar acciones importantes. Un cierre brusco puede perder el último segundo de cambios personales aún no enviados.

## Alcance

Esta es una alpha para amigos de confianza. El servidor controla el mundo compartido; parte de las fórmulas y del progreso personal todavía se calcula en Unity. No es un servidor público con protección completa contra clientes modificados. Tampoco reproduce todavía todo AO20: clanes, PvP, oficios completos y algunos efectos mágicos avanzados sobre NPC siguen pendientes. Los efectos que no admite la sala se rechazan explícitamente.

La primera conexión importa el personaje local. Las siguientes recuperan el personaje del anfitrión. El guardado local permanece separado. La identidad se conserva en PlayerPrefs, vinculada a la sala y al nombre: actualizar el ejecutable no la cambia. No cambies la clave de sala ni borres PlayerPrefs si querés conservar ese acceso.

## Compilar y probar

Desde la raíz del proyecto:

```powershell
python Tools/export_online_catalog.py
dotnet build OnlineServer/AOOnlineServer.csproj
python Tools/test_coop_server.py
python Tools/test_duel_server.py
dotnet publish OnlineServer/AOOnlineServer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../AO_Online/Release/Server-next
```

`Data/catalog.json.gz` contiene datos de juego de los 842 mapas importados. No incluye imágenes ni audio. `test_coop_server.py` usa un puerto, identidades y guardados temporales; prueba dos jugadores, recompensas, botín, comercio, puertas, reinicio, recuperación y el límite de once conexiones.

La prueba opcional `python Tools/test_coop_unity.py` usa Unity abierto y el menú de prueba del Editor mediante un marcador temporal. No debe ejecutarse contra una sala real. Comprueba también que los guardados locales respaldados permanezcan intactos.

Para actualizar: cerrar clientes y servidor, respaldar `Saves`, `room-key.txt` y los perfiles locales; sustituir ejecutables y catálogo sin borrar esos datos. Todos deben usar el cliente de protocolo 2.
