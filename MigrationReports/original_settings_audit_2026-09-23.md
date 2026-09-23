# Configuración del cliente original y estado en Unity

Fuentes: `Archivos Originales/argentum-online-client-master/argentum-online-client-master/CODIGO/frmOpciones.frm`, `Archivos Originales/Recursos-master/Recursos-master/OUTPUT/Configuracion.ini` y `Teclas.ini`.

## Conectado al juego local

- Teclas reasignables para las 14 acciones de teclado que hoy ejecuta Unity: movimiento, interacción, ataques, recoger, inventario, mapa, misiones, personaje y guardado/carga rápidos. Se impiden duplicados y se pueden restablecer los valores iniciales de esta versión.
- Movimiento alternativo con flechas, independiente de WASD.
- Volumen general de efectos, pasos y ambiente de lluvia; música de mapa activada o silenciada.
- Pantalla completa en el ejecutable, VSync, indicador de FPS, minimapa centrado, número de mapa y texto sobre el personaje.
- Preferencias persistentes mediante `PlayerPrefs`, separadas de los archivos de personajes.

## Detectado en los originales, pendiente de su sistema correspondiente

- `Teclas.ini` define 33 acciones para el cliente en línea. Las acciones restantes no tienen aún equivalente funcional en el modo local (macros, acciones sociales, hechizos o comandos de red), por lo que no aparecen como botones inertes en Ajustes.
- `frmOpciones.frm` incluye volumen de música MIDI. El reproductor MIDI actual de Unity usa MCI/WinMM y solo permite encender o apagar la pista sin afectar otros programas. El control de volumen de música requerirá reemplazar ese reproductor por uno que mezcle audio dentro de Unity.
- Las opciones de render avanzado, tutoriales, clanes, chat global y preferencias de hechizos dependen de funciones del cliente en línea o de sistemas que esta versión aún no implementa.
- Sensibilidad e inversión de ratón del cliente original no se aplican al movimiento local actual por teclado. Se agregarán cuando exista una acción de juego controlada por movimiento del ratón.

## Verificación

Compilación de `Assembly-CSharp` sin errores; en Unity Play se revisaron las pestañas, cambio y restablecimiento de teclas, audio, FPS y minimapa centrado. La preferencia de minimapa se dejó en su valor anterior.
