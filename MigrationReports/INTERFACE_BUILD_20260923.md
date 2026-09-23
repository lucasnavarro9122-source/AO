# Interfaz y build de Windows — 23/09/2026

## Entrega

- Escena inicial de build: `AO_Ciudad_de_Ullathorpe_Playable.unity`.
- Build Windows de Unity 6000.3.17f1: `Builds/Windows/Argentum-Unity.exe`, junto con su carpeta `_Data` y demás archivos. El registro de Unity indica `Build Finished, Result: Success.`
- Menú `AO > Build Windows` para repetir el build desde el editor.
- Compilación C# de ejecución y editor: cero errores y cero advertencias.

## Interfaz revisada

- Menú: Continuar, Nueva partida y Salir.
- Creador: género, raza, clase, ciudad, cabeza, nombre, volver y crear personaje.
- HUD: inventario, hechizos, estadísticas, información, minimapa, mapa ampliado, chat local, Hogar, diario y ficha de personaje.
- Ventanas: comercio con NPC, banco, sacerdote, diálogo, diario, ajustes, manual, mercado web y salida.
- Los atajos del HUD ya no se ejecutan detrás de ventanas modales. Ajustes y las demás ventanas superiores cierran con Escape.
- Se eliminaron atajos y textos que regalaban oro, equipo, consumibles y hechizos, anulaban costes de magia, alteraban recursos, recargaban mapas o detenían NPC.

## Límite actual

La interfaz disponible cubre los sistemas locales del prototipo. Grupo, clan, comercio entre jugadores, mercado dentro del juego, cuenta y chat entre jugadores requieren servidor y cliente de red. Dibujar esas ventanas sin conexión no completaría su función. El botón Mercado AO abre el servicio web externo del cliente original; su disponibilidad depende del sitio.

No se ejecutó el build con el perfil del usuario: el prototipo usa autoguardado y comparte `Application.persistentDataPath` con el editor. Se copió el guardado actual a `MigrationReports/backup_player_save_before_windows_build_20260923/` y se verificó que el SHA-256 de `save_slot_1.json` coincide con el original.
