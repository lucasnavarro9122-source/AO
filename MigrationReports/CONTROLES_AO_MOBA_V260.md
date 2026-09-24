# Controles AO/MOBA — alpha 0.26

## Uso

En **Ajustes > Controles**, elegí AO o MOBA. Cada perfil conserva sus propias teclas. Los cambios se guardan automáticamente como preferencias locales; no reemplazan el personaje.

- **AO:** conserva las teclas existentes. Las macros de hechizos empiezan desactivadas. Se habilitan en la pestaña **Hechizos**; por defecto usan F5, F6, F7 y F8.
- **MOBA:** clic derecho para caminar, perseguir/atacar un enemigo o acercarse a interactuar. Q/W/E/R para hechizos, 1/2/3/4 para consumibles, M para mapa, I para inventario, J para misiones, C para personaje, F para interactuar, G para recoger, A para atacar, S para detener la orden y B para meditar.
- **Hechizos:** asigná un hechizo aprendido a cada casilla. «Lanzar sobre el cursor» usa el objetivo señalado; desactivado, permite seleccionar el objetivo con clic izquierdo. Los hechizos personales se aplican al personaje.
- **Consumibles:** asigná objetos disponibles del inventario. El acceso sigue al tipo de objeto aunque cambie de casilla; cada pulsación usa una unidad.
- Pulsá una tecla configurada para cambiarla; también admite botones del mouse. La cruz quita una asignación de tecla. Enter y Escape permanecen reservados para chat y cierre/cancelación.
- Las asignaciones se conservan por personaje y perfil. «Restablecer perfil» restaura las teclas y los valores iniciales de macros, conservando los objetos y hechizos asignados.

Las macros lanzan una sola acción por pulsación. Se conservan los requisitos, el coste de maná, los tiempos de reutilización y las fórmulas originales. No se modificó el balance de clases.

## Música

El MIDI original **2**, identificado como «Argentum Opening», acompaña el menú de entrada. Al comenzar la sesión se reproduce la música del mapa; Ullathorpe usa el MIDI **4**. Windows utiliza una copia local corta en `%LOCALAPPDATA%/AoDuels/Music` para evitar fallos MCI con rutas largas. La opción de música afecta al inicio y a los mapas. Las voces de NPC siguen desactivadas.

## Distribución

El cambio corresponde al cliente. El servidor 0.25.1 y su protocolo 2 siguen siendo compatibles. Para disponer de estos controles hay que reemplazar la carpeta completa del cliente por la versión 0.26.

## Comprobaciones

La prueba optativa `AOControlsQA260` usa preferencias temporales y bloquea el guardado de personajes. Valida perfiles separados, conflictos de teclas, botones del mouse, rutas alrededor de paredes, clic derecho, consumibles, hechizos con coste/cooldown, bloqueo por ventanas y transición de música. El resultado se registra en `controls_v260.json`; las capturas muestran ambos perfiles.
