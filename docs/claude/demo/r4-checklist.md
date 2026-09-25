# R-4: reto real con 2 clientes compilados (0.27) + espectador

Prueba de humo antes de pasar el ZIP. Todo en la PC de Lucas, contra un servidor **aislado** (nunca la sala real).

## Antes
1. `aod-respaldo` (guarda LocalLow, incluida la partida de demo `AO_BattleDemo/`, y PlayerPrefs).
2. **La partida de demo de Lucas (`AO_BattleDemo/`) no se toca ni se mueve.** Cada cliente abre con `--ao-test-profile <nombre>` (Programación) y guarda en `AO_Test/<nombre>/`. Sin ese argumento no se corre R-4. Build: uno intermedio desde el commit que trae el argumento, sin empaquetar.
3. `python Tools/r4_guard.py before`: hash de todos los guardados (normales + `AO_BattleDemo/`) y de las PlayerPrefs del juego.

## Servidor
`powershell -NoProfile -ExecutionPolicy Bypass -File Tools/r4_demo_server.ps1` → imprime la carpeta temporal y la ruta de la clave **temporal**.

## Clientes (abrir `../AO_Online/Release/Client/ArgentumOnline.exe` dos veces)
- Cliente A: `ArgentumOnline.exe --ao-test-profile r4a -logFile <tmp>\r4a.log` → INGRESAR → DEMO AO BATTLESERVER → IP `127.0.0.1` + clave temporal → JUGAR CON AMIGOS → crear "RetadorUno".
- Cliente B: `--ao-test-profile r4b -logFile <tmp>\r4b.log`, igual, crear "RetadorDos".
- Espectador: `--ao-test-profile r4spec -logFile <tmp>\r4spec.log`, crear "Mirón" y pararse en la grada.
- En cada log tiene que salir `[AO Test] perfil de prueba '<nombre>'`; si no sale, cerrar y no seguir.
- (Opcional) Un bot espectador: `python -c` con `Tools/test_duel_server.py` (DuelPeer) parado al lado de la arena.

## Reto
1. A: `/RETAR RetadorDos`, apuesta 1.000. B: ACEPTAR.
2. Verificar: conteo en los dos; marcador arriba; arena igual en los dos; nadie se mueve durante el conteo.
3. Pelear al mejor de 3. Al final: "¡VICTORIA!" y "DERROTA", premio 1.800, oro del ganador +800 y del perdedor −1.000.
4. Capturas: conteo, pelea y resultado, en los dos clientes.

## Después
- Cerrar los clientes y el servidor. `python Tools/r4_guard.py after`: guardados normales y `AO_BattleDemo/` idénticos, nada nuevo fuera de `AO_Test/`, y en el registro solo cambian las claves de ventana de Unity (`Screenmanager*`).
- `AO_Test/r4*` queda como evidencia; no toca datos de Lucas.
