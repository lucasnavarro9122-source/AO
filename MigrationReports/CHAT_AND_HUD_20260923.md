# Chat y paneles del HUD — 23/09/2026

## Referencia original

`Archivos Originales/argentum-online-client-master/argentum-online-client-master/CODIGO/clsDialogs.cls` muestra el diálogo sobre el personaje, divide líneas de 18 caracteres y lo mantiene 5000 ms más 60 ms por carácter.

## Cambios

- El chat ya no recoge `Debug.Log`. Los registros del autoguardado, incluidas las rutas locales, quedan en la consola de Unity. Los mensajes de juego emitidos con `PushMessage` siguen visibles. El guardado manual sigue notificando «Partida guardada.».
- El texto local escrito con Enter se muestra en el chat y sobre la cabeza del personaje con el tiempo del cliente original. `/hogar` sigue siendo un comando y no se muestra como diálogo.
- El chat muestra cuatro líneas de una sola fila, dentro del área disponible.
- Se retiraron textos de hechizos, información y estadísticas que tapaban etiquetas, iconos y ranuras del marco original. Las acciones Meditar, Lanzar, Hogar, Estadísticas y Diario siguen conectadas.

## Verificación

- `dotnet build Assembly-CSharp.csproj -nologo -v:q -p:NoWarn=0649`: cero errores y cero advertencias.
- `git -c core.whitespace=cr-at-eol diff --check`: sin errores.
- Unity 6000.3.17f1 creó `Builds/Windows/Argentum-Unity.exe` desde una copia aislada: `Build Finished, Result: Success.`, cero errores y cero advertencias. Se copiaron los archivos resultantes al build entregable.
- La compilación no ejecuta una partida ni modifica el guardado del usuario.

La conversación continúa siendo local. El chat entre jugadores necesita servidor y cliente de red.
No se abrió una partida para comparar visualmente todas las resoluciones, porque este prototipo comparte el guardado automático con la sesión del usuario.
