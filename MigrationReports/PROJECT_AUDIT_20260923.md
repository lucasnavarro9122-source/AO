# Auditoría del proyecto Argentum en Unity — 23/09/2026

## Alcance y criterio

Revisé el proyecto Unity, sus informes de migración, la escena, los scripts de ejecución, los recursos originales disponibles y la instalación local del servidor. Separé datos comprobados de pruebas aún pendientes. No ejecuté una compilación nueva ni una sesión de juego nueva durante esta auditoría; las pruebas de Unity citadas provienen de los informes y del registro del editor generados hoy. El registro del editor termina a las 15:43.

## Estado general

**Estado: prototipo jugable local, con migración visual de mapas avanzada. Todavía no es cliente AO conectado ni build distribuible validada.** El proyecto Unity usa 6000.3.17f1. Tiene una escena jugable generada de unos 29 MB y 67 scripts de ejecución en `Assets/AOMigrator/Runtime`. `AOTestPlayer`, combate, inventario, magia, banco, misiones, NPC, menú y guardado funcionan como sistemas locales o tienen implementación local. No encontré transporte TCP, WebSocket ni cliente de paquetes dentro de los scripts de Unity revisados. El estado del jugador y los resultados de combate se calculan localmente.

Al iniciar la auditoría, el proyecto no era un repositorio Git. Después se creó un repositorio local y se guardaron los archivos de Unity, configuración, herramientas e informes en un commit inicial. `Archivos Originales`, la instalación `C:\AO20`, cachés y copias temporales quedaron fuera de ese commit; deben conservarse por separado para poder regenerar importaciones. El proyecto reside dentro de OneDrive. `Library` ocupa aproximadamente 5,93 GB y contiene 41.921 archivos.

## Lo que ya está migrado y comprobado

| Área | Evidencia | Estado |
| --- | --- | --- |
| Mapas | `unity_full_audit.json`: 842/842 mapas JSON, 8.684.642 celdas, 456.662 cuadros, 673 texturas utilizadas, sin coordenadas de destino inválidas. | Migración de datos y carga amplia completada para los CSM utilizables. |
| Recorrido en Unity | `unity_map_tour.json`: 842 visitados, 0 fallidos. Mediana 90,4 ms y percentil 95 147,5 ms de carga en ese editor. | Prueba de carga completa; no equivale a probar mecánicas de cada mapa. |
| Apariencia del mundo | 682 PNG importados; capas, bloqueos, disparadores, NPC, objetos, luces y partículas preservados. `npc_visual_report.json` registra 5.427 colocaciones de NPC sin cuerpos faltantes. | Amplia cobertura visual; comparación píxel a píxel con cliente original pendiente. |
| Ambiente | 6.209 luces; 325 definiciones de partículas, 100 usadas; clima y ciclo día/noche probados en Play. | Implementado localmente; aún sin eventos de servidor. |
| Contenido RPG | Base de 4.275 objetos, 141 hechizos definidos y 317 misiones con contenido. Sistemas locales de combate, inventario, magia, banco, muerte y creación de personaje. | Implementación parcial del comportamiento AO; paridad integral y flujo conectado pendientes. |

**Misiones:** `Quests.DAT` declara 353 índices, pero contiene 343 secciones `[QUESTn]`. Diez índices no existen en el archivo. Las otras 26 secciones que Unity no importó están vacías. Las 317 secciones con contenido coinciden con las 317 importadas. Por tanto, la diferencia 317/353 no demuestra pérdida de misiones jugables.

**Avisos antiguos:** tres diagnósticos de instalación indican `Magic=False`. La escena serializada no lleva magia, pero `AOMagicAutoBootstrapV120` la agrega después de cargar la escena. Hay que comprobar el flujo funcional en Play; no corresponde tratar esos diagnósticos antiguos como prueba de que la magia está ausente ahora. El `UnityException` de `MaterialPropertyBlock` del registro aparece durante una compilación anterior. El código actual inicializa ese objeto durante `ApplyMapLight`, y el mismo registro continúa con pruebas de mapas y luz completadas. No hay evidencia de ese error después del cambio.

## Bloqueos y brechas

### P0 — Una versión de servidor estable y compatible

El árbol 2026 del servidor está en `C:\AO20\argentum-online-server` (commit `3362c29`), pero no tiene `Server.exe` compilado. El VB6 instalado no permite `/make` y su IDE falla al abrir. El `server.exe` oficial 2025 v5.02.0082 está en `C:\AO20\release-v5.1.82` y llegó a escuchar `127.0.0.1:7667` con recursos históricos preparados. Durante esta auditoría no hay proceso ni puerto en escucha. Windows registró dos cierres de ese ejecutable con `0xc0000005` a las 16:08 y 16:09; la causa precisa no está determinada. El binario 2025 y los datos/protocolo 2026 no deben asumirse compatibles. La instalación y límites están en `C:\AO20\LOCAL_SETUP.md`.

**Criterio de salida:** seleccionar una versión concreta de servidor, binario y recursos; lograr arranque repetible y sesión local estable; documentar formato de paquetes, cifrado, autenticación y compatibilidad. No incorporar la clave local en Git.

### P0 — Cliente de red y autoridad de juego

En `Assets/AOMigrator/Runtime` no aparece implementación `TcpClient`, `Socket`, `System.Net`, `NetworkStream`, WebSocket ni `UnityWebRequest`. `AOWorldManagerV07` expone métodos para hora y clima, pero no recibe paquetes. Movimiento, combate, inventario, misiones y guardado son locales. `AOSaveGameV140` guarda JSON bajo `Application.persistentDataPath`; hechizos y banco también tienen persistencia separada. Estos datos sirven para prototipo sin servidor, pero al conectar el juego deben quedar bajo reglas del servidor cuando corresponda.

**Criterio de salida:** una conexión local que complete autenticación y selección/creación de personaje, cargue mapa y posición desde servidor, intercambie movimiento y muestre otro jugador. Después migrar inventario, combate, NPC, magia, misiones, banco y clima por etapas. Usar el cliente VB6 original como referencia de paquetes de la versión de servidor elegida.

### P1 — Build correcto y reproducible

`ProjectSettings/EditorBuildSettings.asset` habilita solo `Assets/Scenes/SampleScene.unity`; la escena `AO_Ciudad_de_Ullathorpe_Playable.unity` no figura allí. Un build realizado ahora empezaría por la escena de muestra. No encontré una prueba de build de escritorio final. Tampoco hay ensayos automáticos de Unity Test Framework; sí existen herramientas de QA personalizadas que auditaron y recorrieron mapas.

**Criterio de salida:** elegir escena inicial correcta, generar build Windows, abrirlo fuera del editor y verificar menú, mapa, entrada, guardado/carga, transición 1 a 2 y cierre sin errores. Después repetir con red local.

### P1 — Paridad de comportamiento y pruebas

La auditoría 842/842 demuestra que los mapas cargan, no que cada NPC, hechizo, misión o interacción conserve reglas AO. El diagnóstico de magia informa 11 hechizos cubiertos por su prueba, de 141 definidos; no es una medición de cuántos están implementados. Hace falta una matriz de casos representativos tomada del cliente y servidor originales. Validar creación, muerte, comercio, banco, botín, combate, hechizos, misiones, efectos, teletransporte y persistencia, con resultados esperados verificables.

La optimización redujo las seis cargas de prueba de 1.469 ms a 842 ms en el editor. Faltan mediciones en build y hardware objetivo, memoria, pausas por recolección y comparación visual con VB6. La escena grande y recursos importados justifican esa prueba antes de ampliar contenido.

### P2 — Recursos originales faltantes

`mapa844.csm` tiene cero bytes también en el repositorio consultado; el mapa 843 no está disponible. El mapa 266 contiene 10 salidas a mapas 3000–3004 no presentes. Faltan definiciones visuales originales para objetos 566, 567, 568, 570, 727, 757, 759 y 1645. La consulta de 25 forks recientes no encontró esos archivos compatibles. No mezclar objetos de `ao-libre/ao-server`: su numeración difiere. Posible explicación de 3000–3004 como instancias dinámicas requiere verificar código del servidor; no está demostrada.

**Criterio de salida:** encontrar fuentes compatibles o documentar sustituciones explícitas, sin inventar datos originales ni cambiar silenciosamente IDs.

## Calidad y seguridad del código

Semgrep `p/default` revisó 215 archivos con 322 reglas y produjo un aviso en `Tools/find_source_gaps.py:20`: URL dinámica en `urllib`. La llamada obtiene metadatos de forks de GitHub y construye una URL con host HTTPS fijo `raw.githubusercontent.com`; no encontré un camino directo desde esos metadatos hacia el esquema `file://`. El aviso requiere validación si se amplía la herramienta para aceptar URL externas. El análisis estático no demuestra ausencia de otros defectos.

`AOSaveGameV140` usa archivo temporal y copia de respaldo, lo que ayuda a recuperar datos. La escritura final usa `File.Copy` sobre el destino y los datos de magia/banco tienen archivos separados; verificar coherencia entre ellos y recuperación tras cierre abrupto. El guardado local no debe considerarse fuente autorizada de personaje en modo conectado.

## Orden recomendado

1. Conservar el commit Git local ya creado y elegir un destino remoto si se necesita respaldo fuera de este equipo. Mantener cachés, claves y descargas originales fuera del repositorio.
2. Corregir escena inicial y obtener primer build Windows reproducible. Ejecutar prueba corta fuera del editor.
3. Fijar versión de servidor, recursos y cliente de referencia. Resolver falta del binario 2026 o estabilizar y aceptar explícitamente el protocolo 2025 para una prueba temporal.
4. Documentar protocolo y completar una primera conexión vertical: login, personaje, entrada al mapa y movimiento con otro cliente. Mantener lógica local desacoplada durante transición.
5. Migrar autoridades de juego por dominio y agregar pruebas de paridad para cada dominio. Validar guardado servidor y recuperación.
6. Cerrar recursos faltantes y diferencias visuales según impacto real. Perfilar build en hardware objetivo.

**Próximo trabajo recomendado:** el paso 2, seguido de prueba de estabilidad y protocolo del servidor elegido. Añadir más contenido visual antes de eso reduce poco riesgo del proyecto.
