# Recursos que no aparecen en las fuentes consultadas

Consulta: 2026-09-23.

Se revisaron además 25 forks recientes de [`ao-org/Recursos`](https://github.com/ao-org/Recursos/forks) mediante `Tools/find_source_gaps.py`: ninguno tiene un `mapa844.csm` no vacío, archivos `mapa3000.csm`–`mapa3004.csm`, ni las ocho secciones de objeto buscadas. Resultado verificable en `source_gap_forks.json`. El árbol del repositorio [`ao-org/argentum-online-server`](https://github.com/ao-org/argentum-online-server) tampoco publica esos archivos de mapas.

Se revisó una copia superficial de `ao-org/argentum-online-server` en `C:/Users/lucas/AppData/Local/Temp/ao_source_gap_server_20260923`: no contiene CSM ni directorio `Mapas`; la búsqueda de los números 3000–3004 en código VB6 encontró constantes de cuerpos desnudos de personajes, pero no referencias a esos números como mapas. Esto no demuestra el origen de las diez salidas del mapa 266.

## Mapa 844

`Mapas/mapa844.csm` local mide 0 bytes. También mide 0 bytes en [`ao-org/Recursos`, rama master](https://github.com/ao-org/Recursos/blob/master/Mapas/mapa844.csm) y en [NuevaInterfazBoveda](https://github.com/ao-org/Recursos/blob/NuevaInterfazBoveda/Mapas/mapa844.csm). La API de GitHub muestra un solo commit para esta ruta. No se encontró una copia válida en ese repositorio.

## Ocho objetos

Los índices 566, 567, 568, 570, 727, 757, 759 y 1645 no tienen sección `[OBJn]` en el [`Dat/obj.dat` de master](https://github.com/ao-org/Recursos/blob/master/Dat/obj.dat) ni en [NuevaInterfazBoveda](https://github.com/ao-org/Recursos/blob/NuevaInterfazBoveda/Dat/obj.dat). El archivo local `init/localindex.dat` da `GRHINDEX=0` para todos.

El [`ao-libre/ao-server` `Dat/obj.dat`](https://github.com/ao-libre/ao-server/blob/master/Dat/obj.dat) contiene siete de esos números, pero corresponde a otra numeración de objetos (1.238 secciones frente a 4.280 de `ao-org/Recursos`). Por ejemplo, 566–570 son “Flores” en ese fork, mientras los números vecinos del recurso local son ropa. Reutilizar esas definiciones alteraría el significado de los mapas; no se mezclaron recursos.

Para completar esos elementos hace falta otra copia compatible del recurso de Argentum 20 o una decisión explícita de reemplazo visual.

## Salidas del mapa 266

La auditoría completa encontró 10 salidas del mapa 266 hacia los mapas 3000–3004 (dos por destino). Esos cinco mapas no están entre los 842 CSM utilizables descargados. Las rutas `Mapas/mapa3000.csm` a `Mapas/mapa3004.csm` de `ao-org/Recursos`, rama `master`, devolvieron HTTP 404 el 2026-09-23. Las salidas se conservaron; Unity rechaza el traslado a un mapa ausente. Falta determinar si eran instancias generadas por servidor o mapas de otra fuente compatible.
