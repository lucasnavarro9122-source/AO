# Guía de la sala privada (amigos y anfitrión)

Borrador de **Servidor y Multiplayer**, preparado en la nube el 25/09. Para integrar en `../AO_Online/LEEME.md` antes del próximo ZIP. Vale para protocolo 2 (cliente 0.25 o posterior).

---

## Para los amigos

### Primera vez
1. **Tailscale:** instalalo desde tailscale.com y entrá con tu cuenta. Aceptá la invitación que te manda el anfitrión. En la app de Tailscale tiene que aparecer la PC del anfitrión.
2. **Juego:** descomprimí `Cliente-para-amigos.zip` en una carpeta tuya (por ejemplo `Documentos\AO`) y abrí `ArgentumOnline.exe`.
   - Si Windows pregunta por la red, permití **redes privadas**.
3. **Conectar:** **JUGAR CON AMIGOS** → **SALA PRIVADA** → **INGRESAR**.
   - **IP de Tailscale del anfitrión:** la que te pasa el anfitrión (empieza con `100.`). También sirve el nombre de su PC en Tailscale.
   - **Clave de sala:** la que te pasa el anfitrión en privado. No la compartas ni la subas a ningún lado.
   - **CONECTAR**.
4. La primera vez se lleva tu personaje local a la sala. Después, la sala guarda tu personaje en la PC del anfitrión.

### Demo AO BATTLESERVER (arenas y dungeon)
- **Con amigos:** **INGRESAR** → **DEMO AO BATTLESERVER** → **JUGAR CON AMIGOS**. Usa la misma IP y la misma clave, pero entra a otra sala, en el puerto **7778**.
- **Sin conexión:** **SOLO DUNGEON**. Las arenas necesitan jugar con amigos.
- Los personajes de la demo son **aparte**: no tocan tu personaje de la sala normal ni el de un jugador.

### Actualizar a una versión nueva
1. Cerrá el juego.
2. Descomprimí el ZIP nuevo **encima** de la carpeta vieja (o en una carpeta nueva y borrá la vieja).
3. Abrí y conectá igual que antes. La IP queda recordada.

Tu acceso a la sala **no** está en la carpeta del juego: está guardado en Windows. Por eso actualizar no lo pierde. **No uses limpiadores de registro** (CCleaner y similares): borran ese acceso y la sala te toma como un jugador nuevo.

### Problemas comunes
| Mensaje o síntoma | Qué hacer |
|---|---|
| "Dirección inválida" | Revisá la IP: tiene que ser la de Tailscale del anfitrión (`100.x.x.x`), no la de su casa. |
| "Pegá la clave de sala del servidor" | El campo de la clave está vacío. |
| Se queda en "Conectando a …:7777" | Tailscale apagado de tu lado o del anfitrión, o su servidor está cerrado. Mirá que la PC del anfitrión figure como conectada en Tailscale. |
| Se queda en "Conectando a …:7778" (demo) | La sala normal puede estar abierta y la de la demo no: pedile al anfitrión que arranque `Iniciar-demo.bat`. |
| "Servidor desconectado. Reconectá para recuperar la partida." | Se cortó la conexión. Volvé a conectar: la partida sigue en el servidor. Como mucho se pierde el último segundo. |
| "Falta una operación del servidor. Reconectá." | Versión del cliente distinta de la del servidor. Pedile al anfitrión el ZIP actual. |
| La sala está llena | Entran el anfitrión y hasta 10 amigos. |

---

## Para el anfitrión (Lucas)

### Compartir la sala
- **Tailscale:** mejor **compartir solo tu PC** con cada amigo (Share en la consola de Tailscale) que invitarlos a toda tu red: así no ven tus otros equipos.
- **Puertos:** la sala normal usa TCP **7777** y la demo, TCP **7778**. **No los abras en el router.** Solo tienen que llegar por Tailscale.
  - Desde la 0.27 el servidor escucha solo en `127.0.0.1` y en las direcciones de Tailscale (`--bind auto`). El firewall de Windows es una segunda barrera. En PowerShell como administrador:
    ```powershell
    New-NetFirewallRule -DisplayName "AO sala (solo Tailscale)" -Direction Inbound -Protocol TCP -LocalPort 7777 -RemoteAddress 100.64.0.0/10 -Action Allow
    New-NetFirewallRule -DisplayName "AO demo (solo Tailscale)" -Direction Inbound -Protocol TCP -LocalPort 7778 -RemoteAddress 100.64.0.0/10 -Action Allow
    ```
    Y en *Firewall de Windows > Reglas de entrada*, desactivá cualquier regla vieja que permita `AOOnlineServer.exe` en redes privadas o públicas.
  - Al compartir tu PC por Tailscale, los amigos ven **todos** sus puertos (carpetas compartidas, escritorio remoto…). En la consola de Tailscale, en *Access controls*, permitiles solo `tcp:7777` y `tcp:7778` hacia tu PC. Por ejemplo, en el ACL: `"dst": ["tu-pc:7777,7778"]`.
- **Guardados y OneDrive:** si `AO_Online` está dentro de OneDrive, OneDrive puede bloquear `world.json` mientras lo sube. Mejor mover el servidor fuera de OneDrive o excluir esa carpeta.
- **Demo:** `Iniciar-demo.bat` arranca la segunda sala con `--demo --data SavesDemo --port 7778`. Puede andar al mismo tiempo que la sala normal. Tiene sus propios guardados, `SavesDemo`, y el servidor les hace `.bak` solo. El respaldo (`aod_backup.ps1`) ya incluye `SavesDemo`.
- **Clave:** mandala por mensaje privado. Si se filtra, cambiarla obliga a todos a reconectar: la identidad está atada a la sala.

### Actualizar el servidor (sin perder partidas)
1. Avisar y cerrar: primero los clientes, después el servidor.
2. Respaldo con fecha: `Tools/aod_backup.ps1` (skill `aod-respaldo`). Incluye `Saves` y los perfiles locales.
3. Publicar en `Server-next`, **no** encima del servidor que está andando:
   ```powershell
   python Tools/export_online_catalog.py
   dotnet build OnlineServer/AOOnlineServer.csproj
   python Tools/test_coop_server.py
   dotnet publish OnlineServer/AOOnlineServer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../AO_Online/Release/Server-next
   ```
4. Copiar el ejecutable y el catálogo de `Server-next` a `Server`. **Nunca** reemplazar ni borrar `Server/Saves`, `Server/SavesDemo` ni `room-key.txt`.
5. Arrancar el servidor, entrar vos con `127.0.0.1` y probar.
6. Recién ahí pasar el ZIP nuevo a los amigos. Todos tienen que usar la misma versión de protocolo.
7. Anotar en `../AO_Online/VERSION.txt` la versión y el commit de cliente y servidor.

### Si algo sale mal
- Volver al ejecutable anterior (hay copias en `../AO_Online`). Los `Saves` no se tocaron.
- Si se dañó un guardado: `Saves/world.json` tiene su `.bak`, y además está el respaldo del paso 2 (skill `aod-respaldo`, restaurar).

---

## Pendiente antes de publicarla
- [ ] Confirmar los pasos del menú con el cliente nuevo: la noche del 24/09 hubo cambios de menú y retos que no están en este commit.
- [ ] El menú dice "Sala privada: hasta 10 jugadores." (`AOMainMenuV140.cs:390`) y el servidor acepta 11 (anfitrión + 10). Unificar el texto (Interfaz).
- [ ] Verificar si el ZIP ya agrega la regla de firewall de Windows o si el amigo tiene que aceptarla a mano.
