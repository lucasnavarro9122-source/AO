---
name: aod-servidor
description: Trabajo en el servidor cooperativo de AoDuels (OnlineServer .NET, protocolo 2, Tailscale, sala privada de 11 jugadores). Usar para cambiar el servidor o el cliente online, sincronizar mecánicas nuevas, correr las pruebas de red, publicar el servidor o diagnosticar problemas de conexión de los amigos.
---

# Servidor y multiplayer

## Piezas
- Servidor: `OnlineServer/` (`Program.cs`, `CoopRoom.cs`, `Data/catalog.json.gz`). README: `OnlineServer/README.md`.
- Cliente: `AOOnlineClientV240.cs`, protocolo `AOCoopProtocolV250.cs`. QA: `AOCoopQA250` (Editor).
- En producción: `../AO_Online/Release/Server` (con `Saves/world.json` + `.bak` y `room-key.txt`). TCP **7777** por Tailscale. El anfitrión usa `127.0.0.1`; los amigos, la IP Tailscale del anfitrión.
- Diseño: el servidor es dueño del mundo compartido (NPC por mapa, botín único, puertas, stock de comerciantes, diario de recompensas sin duplicados). Parte del progreso personal todavía se calcula en Unity. Es una alpha para amigos de confianza: sin anti-cheat completo, sin PvP, clanes ni oficios.

## Comandos
```powershell
python Tools/export_online_catalog.py            # si cambiaron datos de mapas/NPC/objetos
dotnet build OnlineServer/AOOnlineServer.csproj -v:q -clp:ErrorsOnly
python Tools/test_coop_server.py                 # 2 clientes, puerto e identidades aislados
python Tools/test_static_npcs.py
python Tools/test_coop_unity.py                  # opcional; Unity abierto, fuera de Play; nunca contra la sala real
dotnet publish OnlineServer/AOOnlineServer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../AO_Online/Release/Server-next
```

## Cambiar el protocolo
1. Toda mecánica nueva que afecte a otros jugadores (skill shots V267, casteo V268, meditación V269, auras) tiene que decidir si es **solo visual local** o si se **sincroniza**. Documentarlo en el reporte.
2. Mensaje nuevo o campo nuevo: agregarlo en `AOCoopProtocolV250` y en el servidor al mismo tiempo, compatible hacia atrás si se puede. Si se rompe la compatibilidad: protocolo 3, y avisar a QA para subir las versiones y el ZIP.
3. Las recompensas, el botín y el comercio los confirma siempre el servidor. Nunca confiar en el cliente para crear objetos u oro.
4. Probar con `test_coop_server.py` y agregar un caso nuevo para la mecánica.

## Publicar (con `aod-respaldo` antes)
- Publicar en `Server-next`, cerrar el servidor en vivo, respaldar `Saves` + `room-key.txt`, reemplazar **solo los ejecutables y el catálogo**. Nunca pisar `Saves/` ni `room-key.txt`.
- Subir la versión en `../AO_Online/VERSION.txt`.
- No publicar ni leer claves; no pegar logs que contengan tokens.

## Diagnóstico de conexión de un amigo
Revisar: ¿Tailscale conectado y autorizado en los dos? ¿IP Tailscale correcta? ¿Servidor corriendo en el 7777? ¿Firewall de Windows? ¿Misma versión de protocolo (VERSION.txt)? ¿Clave de sala correcta (compartida en privado)?
