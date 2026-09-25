---
name: aod-servidor
description: Trabajo en el servidor cooperativo de AoDuels (OnlineServer .NET, protocolo 3 con oro del servidor, retos de la demo AO BATTLESERVER, Tailscale, sala privada de 11 jugadores). Usar para cambiar el servidor o el cliente online, sincronizar mecánicas nuevas, correr las pruebas de red, publicar el servidor o la instancia de la demo, o diagnosticar problemas de conexión de los amigos.
---

# Servidor y multiplayer

## Piezas
- Servidor: `OnlineServer/` (`Program.cs`, `CoopRoom.cs`, `CoopRoom.Gold.cs`, `CoopRoom.Duels.cs`, `Ledger.cs`, `Data/catalog.json.gz`). README: `OnlineServer/README.md`.
- Compila también `Assets/AOMigrator/Runtime/Shared/*.cs` (C# puro, sin UnityEngine: generador de arena, tasas de la demo, fórmulas) y el protocolo. Mientras otro sector tiene el candado de Unity, el protocolo se puede compilar desde un borrador: `-p:ProtocolPath=<borrador>`.
- Cliente: `AOOnlineClientV240.cs`, protocolo `AOCoopProtocolV250.cs`. QA: `AOCoopQA250` (Editor).
- En producción: `../AO_Online/Release/Server` (con `Saves/world.json` + `.bak`, `Saves/ledger.jsonl` y `room-key.txt`). TCP **7777** por Tailscale. El anfitrión usa `127.0.0.1`; los amigos, la IP de Tailscale del anfitrión.
- Demo AO BATTLESERVER: el mismo ejecutable con `--demo --data SavesDemo --port 7778` (retos, EXP por tramo, oro ×2). El cliente usa el 7778 cuando `AOMainMenuV140.BattleDemoOnline`.
- Diseño: el servidor es dueño del mundo compartido (NPC por mapa, botín único, puertas, stock de comerciantes, diario de recompensas sin duplicados) y, desde el protocolo 3, **del oro** (billetera, banco y custodia de apuestas en el libro). Parte del progreso personal todavía se calcula en Unity. Es una alpha para amigos de confianza: sin anti-cheat completo, sin clanes ni oficios. Solo hay PvP dentro de un reto de la demo.

## Comandos
```powershell
python Tools/export_online_catalog.py            # si cambiaron datos de mapas/NPC/objetos/misiones
dotnet build OnlineServer/AOOnlineServer.csproj -v:q -clp:ErrorsOnly
python Tools/test_coop_server.py                 # 2 clientes, oro del servidor, libro, demo; puerto e identidades aislados
python Tools/test_static_npcs.py
python Tools/test_duel_server.py                 # retos: custodia, rondas, pago, espectador, gracia, reinicio
python Tools/test_coop_unity.py                  # opcional; Unity abierto, fuera de Play; nunca contra la sala real
dotnet publish OnlineServer/AOOnlineServer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../AO_Online/Release/Server-next-v<versión>
```
- Modo prueba: `--test` (solo 127.0.0.1 + `testLedger`) y `--test-time-scale <f>` (solo con `--test`: acelera los tiempos de los retos). Nunca en la sala real.

## Cambiar el protocolo
1. Toda mecánica nueva que afecte a otros jugadores (skill shots V267, casteo V268, meditación V269, auras) tiene que decidir si es **solo visual local** o si se **sincroniza**. Documentarlo en el reporte.
2. Mensaje nuevo o campo nuevo: agregarlo en `AOCoopProtocolV250` y en el servidor al mismo tiempo, compatible hacia atrás si se puede. Si se rompe la compatibilidad, se sube el protocolo y se avisa a QA para subir las versiones y el ZIP. Un cliente viejo ignora los eventos que no conoce pero les da ack: los eventos de oro o de reto nunca pueden llegarle.
3. Las recompensas, el botín, el comercio y **todo movimiento de oro** los confirma el servidor, con una línea del libro y una `op` idempotente. Nunca confiar en el cliente para crear objetos u oro.
4. Probar con `test_coop_server.py` / `test_duel_server.py` y agregar un caso nuevo para la mecánica. Los bots tienen que leer y confirmar todo el tiempo: un socket sin leer se corta a los 1,5 s de demora de envío.

## Publicar (con `aod-respaldo` antes)
- Publicar en `Server-next-v<versión>`, cerrar el servidor en vivo, respaldar `Saves` (con `ledger.jsonl`) + `room-key.txt`, reemplazar **solo los ejecutables y el catálogo**. Nunca pisar `Saves/`, `SavesDemo/` ni `room-key.txt`. El reemplazo en vivo se hace con Lucas.
- Subir la versión en `../AO_Online/VERSION.txt`.
- No publicar ni leer claves; no pegar logs que contengan tokens.
- Instancia de la demo: firewall del anfitrión, una sola vez y como administrador, limitado a Tailscale:
  `New-NetFirewallRule -DisplayName "AO BATTLESERVER demo 7778" -Direction Inbound -Protocol TCP -LocalPort 7778 -RemoteAddress 100.64.0.0/10 -Action Allow`

## Diagnóstico de conexión de un amigo
Revisar:
- ¿Tailscale conectado y autorizado en los dos?
- ¿IP de Tailscale correcta?
- ¿Servidor corriendo en el 7777, o en el 7778 si es la demo?
- ¿Firewall de Windows (la regla del 7778 para la demo)?
- ¿Misma versión de protocolo (VERSION.txt)? Un cliente 3 contra un servidor 2 muestra "el anfitrión tiene que actualizar el servidor".
- ¿Clave de sala correcta (compartida en privado)?
