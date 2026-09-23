# Entrada al juego y preparación para servidor — 23/09/2026

## Cambio visual

El diálogo local sobre el personaje usa letras claras con contorno fino. Se eliminó el rectángulo negro detrás del texto. El tiempo y el ajuste de líneas siguen la referencia `clsDialogs.cls` del cliente original.

## Facciones y ciudad inicial

- El servidor original define seis estados: Criminal, Ciudadano, Caos, Armada, concilio y consejo (`Codigo/Declares.bas`). No son seis opciones equivalentes de creación; Armada/Caos y sus consejos tienen reglas de incorporación (`Codigo/ModFacciones.bas`).
- Al crear un usuario, el servidor fija `Faccion.Status = 1` (Ciudadano) y toma mapa/posición de la ciudad de hogar (`Codigo/TCP.bas`). El cliente original disponible fuerza `CHARACTER_CREATION_HOME_FORGAT` y deshabilita la lista de ciudad (`CODIGO/ModLogin.bas`, `CODIGO/ModUtils.bas`).
- `frmEligeAlineacion.frm` existe, pero su selección llama `WriteGuildFundate`: sirve para fundar un clan, no para elegir facción al crear personaje.
- Unity hoy deja elegir entre seis ciudades (`AOHomeCityV200`) y guarda el hogar en el JSON local. No guarda facción ni aplica reglas de facción a la creación.
- Los identificadores de ciudad tampoco coinciden del todo: en el servidor `cForgat = 7` porque `cArkhein = 6`, mientras Unity guarda Forgat como ID 6. El enum del servidor también incluye Eldoria, Penthar y Morgrim. Hay que definir una tabla de conversión y migración de guardados antes de conectar creación/respawn.
- Una selección de facción que determine ciudad sería una regla nueva. Habría que definir facciones iniciales permitidas, ciudades, restricciones de mapas, persistencia y validación en servidor antes de mostrarla como elección funcional.

## Menú y launcher disponibles

- El cliente original incluye `frmConnect.frm/.frx` y `frmCrearPersonaje.frm/.frx`. La biblioteca `Recursos-master/interface` incluye fondos y botones de entrada en español.
- `argentum20-lanzador-master` contiene un launcher WPF (`Launcher.csproj`, .NET Core 3.1) que descarga/actualiza y abre `Argentum20/Cliente/Argentum.exe`. No hay ejecutable compilado en esa carpeta y este equipo sólo tiene runtime .NET 10. No abre `Argentum-Unity.exe` sin portarlo.
- `ao20-autoupdate-master` es otro actualizador VB6, configurado para el antiguo `Argentum.exe`; tampoco está integrado en Unity.
- El menú actual de Unity sólo ofrece Continuar, Nueva partida y Salir. Carece de cuenta, selección de personaje, estado de servidor y flujo de conexión. Su apariencia puede aprovechar los fondos y botones originales, pero conviene diseñarlo alrededor del flujo de red definitivo.

## Bloqueos para lanzamiento conectado

1. Elegir una versión única de servidor, recursos y protocolo. El código local de servidor 2026 no tiene `Server.exe`. Existe un `server.exe` oficial 2025 en `C:\AO20\release-v5.1.82`, probado en escucha local, pero no se ha demostrado compatible con código/datos 2026. Ver `C:\AO20\LOCAL_SETUP.md`.
2. Conseguir un arranque local estable del servidor elegido. El ejecutable 2025 registró cierres `0xc0000005` en una prueba posterior.
3. Implementar en Unity el cliente de red: conexión, autenticación, lista/creación de personajes, entrada al mapa y movimiento de otro jugador. Corregir el mapeo de IDs de ciudades al protocolo. Hoy movimiento, combate, inventario y JSON de guardado son locales.
4. Pasar gradualmente combate, inventario, NPC, magia, misiones, banco y persistencia a autoridad del servidor; verificar paridad con cliente original.
5. Terminar entrada visual, launcher/actualizador y pruebas de build fuera del editor: instalación limpia, reconexión, dos clientes, guardado, rendimiento y fallos.

El build actual permite una demostración local; todavía no es un cliente multijugador listo para lanzar.

## Verificación de esta revisión

- C# compiló sin errores ni advertencias.
- Unity 6000.3.17f1 terminó el build de Windows con `Build Finished, Result: Success.`. Los archivos actualizados están en `Builds/Windows` y el ensamblado coincide con el generado en la copia aislada.
- No se abrió una partida durante esta verificación; la apariencia final del contorno debe confirmarse en Play sin sustituir el guardado actual.
