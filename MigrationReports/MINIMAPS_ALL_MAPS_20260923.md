# Minimapas para todos los mapas — 23/09/2026

## Causa

`AOInterfaceV0101.CurrentMinimap` ya cambiaba de textura según `CurrentMapNumber`, pero `Assets/Resources/AOMigrator/MinimapsV0103` sólo tenía 22 PNG. Los demás mapas mostraban «Sin minimapa».

## Corrección

- `Archivos Originales/Recursos-master/Recursos-master/Minimapas` contiene 842 BMP de 100 × 100 píxeles, uno por cada mapa JSON de Unity. Los conjuntos de números coinciden exactamente.
- `Tools/import_all_minimaps.py` convierte los BMP a PNG sin alterar los píxeles. Verificó los 22 minimapas existentes y agregó los 820 faltantes. Una segunda ejecución no hizo cambios: `Maps=842 created=0 verified_existing=842`.
- `CurrentMinimap` sólo guarda en caché texturas encontradas, para poder cargar una que Unity importe más tarde durante una sesión del editor.
- El verificador de interfaz compara cada archivo de mapa con su minimapa, en vez de aceptar un mínimo de 21.

El marcador del jugador y el mapa ampliado usan la misma textura seleccionada por número de mapa. No se modificaron partidas guardadas.

## Verificación

- 842 PNG y 842 archivos `.meta`; los 842 GUID son únicos.
- C# de ejecución y editor: cero errores y cero advertencias.
- Unity 6000.3.17f1: `Build Finished, Result: Success.`. `Builds/Windows/Argentum-Unity_Data/resources.assets` coincide con la salida del build aislado.
- No se inició una sesión de juego durante esta comprobación, para no alterar el guardado automático actual.
