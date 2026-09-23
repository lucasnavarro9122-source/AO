# Interfaz clásica: avance del 23 de septiembre de 2026

## Implementado

- Pantalla de creación adaptable al tamaño de Game View, con recursos gráficos y fuente del `ao-ui` descargado. Selección de género, seis razas, doce clases, seis ciudades iniciales, cabeza y nombre. Volver y Crear personaje usan las transiciones existentes.
- Menú principal, ventanas de ciudad y diario de misiones usan el aspecto clásico. Las transiciones que alteran ventanas se ejecutan en `Update` para evitar el error de `GUILayout` durante `OnGUI`.
- El botón de misiones del HUD abre el diario. Los botones superiores abren Ajustes, Manual y Mercado AO; minimizar funciona en la compilación para Windows; salir ofrece volver al menú o cerrar el juego tras guardar.
- La creación de personaje comprueba que exista el mapa inicial y conserva el guardado anterior hasta que se pueda escribir el nuevo. `Capture()` guarda nombre y ciudad inicial, que antes se omitían.

## Comprobación

- `dotnet build Assembly-CSharp-Editor.csproj -nologo -v:q -p:NoWarn=0649`: 0 errores, 0 advertencias.
- Unity 6000.3.17f1 en Play: prueba de interfaz completada (`unity_classic_ui_qa.json`), 6 mapas de ciudades disponibles, 12 pares raza/género con cabezas, creador visible y diario invocado. Capturas de menú y creador en este directorio. Esta prueba no confirma cada clic manual ni la disponibilidad actual de los sitios externos.

## Pendiente

- Resto de pantallas y funciones del cliente original (grupo, clan, comercio, mercado dentro del juego, etc.) requieren migrar sus reglas y, en algunos casos, conexión con servidor. Los botones implementados aquí cubren el menú, creador y controles visibles del HUD actual; no equivalen a paridad total con el cliente original.
- Probar de forma manual en Unity los flujos completos de crear personaje, continuar, diario, Ajustes y salida. Hacerlo con un guardado de prueba separado.

## Incidente de guardado local

Una prueba anterior activó la sesión sin cargar el personaje existente y el guardado automático al salir sobrescribió `save_slot_1.json` y su `.bak.json` en `AppData/LocalLow/DefaultCompany/My project (1)/AO_Demo`. El menú mostraba antes un personaje en mapa 56; no hay otra copia para restaurarlo. Los archivos actuales se conservaron. La prueba final de interfaz no volvió a modificar ese guardado. No se conoce la posición ni el inventario anteriores.
