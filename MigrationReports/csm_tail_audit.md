# Auditoría del final de archivos CSM

Fecha: 2026-09-23. `python Tools/csm_tail_audit.py` leyó los 843 archivos descargados. Hay 842 CSM válidos y `mapa844.csm` vacío. En 790 CSM quedan 4.439.136 bytes después del último registro declarado; 52 terminan exactamente en ese registro. `csm_tail_audit.json` guarda por mapa tamaño, desplazamiento final, longitud y SHA-256 de esa cola.

`GrabarMapaCSM` de `argentum-online-worldeditor/Codigo/ModCargaIAO.bas` escribe cabecera, límites, metadatos y las once listas de longitud explícita. Termina con las salidas (`TEs`) y cierra el archivo: no escribe un campo posterior. El lector de CSM del cliente original en `argentum-online-client/CODIGO/Recursos.bas` también deja de leer al terminar esas listas. Por eso la cola no tiene efecto al cargar ni dibujar el mapa original y no corresponde inventar una sección Unity para ella.

La cola suele contener bytes con aspecto de registros. Una explicación posible es contenido de una versión anterior del archivo que quedó detrás de una escritura más corta en modo binario. No está probado cuál escritura lo produjo. Se conservan los CSM originales sin tocar; el importador registra `trailing_bytes` y utiliza solo los registros declarados.
