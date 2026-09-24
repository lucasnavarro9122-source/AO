# AO Online: alpha cooperativa 0.25

Servidor privado .NET 10, protocolo 2, para el anfitrión y diez amigos. Se ejecuta dentro de Tailscale, en TCP 7777.

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
dotnet publish OnlineServer/AOOnlineServer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../AO_Online/Release/Server-next
```

`Data/catalog.json.gz` contiene datos de juego de los 842 mapas importados. No incluye imágenes ni audio. `test_coop_server.py` usa un puerto, identidades y guardados temporales; prueba dos jugadores, recompensas, botín, comercio, puertas, reinicio, recuperación y el límite de once conexiones.

La prueba opcional `python Tools/test_coop_unity.py` usa Unity abierto y el menú de prueba del Editor mediante un marcador temporal. No debe ejecutarse contra una sala real. Comprueba también que los guardados locales respaldados permanezcan intactos.

Para actualizar: cerrar clientes y servidor, respaldar `Saves`, `room-key.txt` y los perfiles locales; sustituir ejecutables y catálogo sin borrar esos datos. Todos deben usar el cliente de protocolo 2.
