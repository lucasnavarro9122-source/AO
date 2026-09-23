# Migración de mapas AO a Unity

Fecha: 2026-09-23. Fuente local: `Archivos Originales/Recursos-master/Recursos-master` (`ao-org/Recursos`).

## Estado comprobado

- 843 CSM encontrados; 842 legibles y migrados. `mapa844.csm` mide 0 bytes incluso en el ZIP original.
- Los 842 JSON conservan capas, bloqueos, triggers, salidas, NPC, objetos, luces y partículas. La auditoría de mapas 1, 2 y 40 compara esos datos con los CSM sin diferencias detectadas en sus controles.
- 682 PNG en `Assets/Resources/AOMigrator/WorldV07/Textures`. La auditoría de recortes y archivos no halló faltantes para los gráficos importados.
- Los 281 índices de NPC antes sin cuerpo visual ya se resolvieron: 1.542 colocaciones de cuerpo y 74 de cabeza completadas. No quedan colocaciones de NPC sin cuerpo entre las 5.427 registradas. Informe: `npc_visual_report.json`.
- Se importaron 325 definiciones originales de partículas; los 100 tipos usados en mapas y los tipos 57/58 de nieve y lluvia tienen gráficos válidos. El renderizador Unity muestra los grupos visibles cerca de la cámara con material aditivo. Informe: `particle_import_report.json`.
- Los 6.209 registros de luces de mapas se interpretan en Unity con los cuatro colores por vértice del cliente VB6 (luces redondas y cuadradas). `SetWorldHour` aplica la paleta original de 24 horas a mapas sin luz base fija. También se importaron luz ambiental y marcas de lluvia, nieve y niebla de los 842 CSM.
- En Unity 6000.3.17f1, Play mostró los mapas 1 (ciudad), 2 (bosque), 4 (NPC recuperados), 40 (catacumbas con luces y partículas), 119 (nieve) y 126 (niebla). La lluvia también se probó en mapa 1, y quitar la niebla en mapa 126 restauró la vista despejada. La salida del mapa 1 al 2 pasó `AO_MAP_EXIT_QA_OK`. No hubo errores de compilación C# en esas pruebas.
- En el editor, a 1024 × 520, las estadísticas mostraron alrededor de 149 FPS estables en mapa 1 y 177 FPS en mapa 2 tras cargar. La pausa de carga se midió por etapas; la creación de visuales fue su componente principal.
- La auditoría completa ejecutada dentro de Unity revisó 842/842 JSON, 8.684.642 celdas, 456.662 cuadros y 673 texturas usadas. No encontró recortes, cuadros ni coordenadas de destino inválidos. Encontró 10 salidas del mapa 266 hacia mapas ausentes 3000–3004. Informe: `unity_full_audit.json`; verificación independiente de archivos: `local_full_audit.json`.
- Se midieron las mismas seis cargas en Play antes y después de reutilizar renderizadores. El tiempo acumulado bajó de 1.469 a 842 ms; la construcción visual, de 1.063 a 492 ms. Son mediciones puntuales del editor, no garantía para todos los equipos. Informes: `unity_load_profile_before_pool.json` y `unity_load_profile.json`.
- Los comandos `SnowToggle` en mapa 119, `NieblaToggle` en mapa 126 y `RainToggle` en mapa 1 pasaron el control automatizado en Play. La escena ya no emite el aviso de script ausente tras corregir la referencia GUID obsoleta de `AOCharacterRenderer`.
- La recorrida posterior cargó los 842 mapas en Play sin fallos, con mediana de 90,4 ms y percentil 95 de 147,5 ms por carga en este editor. Guardó 12 capturas de mapas diversos. Informe: `unity_map_tour.json`; imágenes: `VisualTour/`. La primera pasada descubrió que la cámara no estaba etiquetada `MainCamera`; se corrigió el capturador y la segunda pasada terminó sin errores.
- Una prueba adicional del mapa 1 midió brillo medio de 35,10 de día y 21,00 de noche al llamar `SetWorldHour`. `SyncWorldTime(0, 60000)` avanzó la hora tras la espera de Play. En mapa 40 contó 28 grupos de partículas y 30 sprites visibles tras 60 cuadros. `JsonUtility` cargó los doce componentes de color por vértice en los 102 tipos importados. También guardó capturas de los mapas tardíos 750, 780, 800 y 840. Informe: `unity_light_cycle_qa.json`.
- La auditoría de cola CSM confirmó que los 790 archivos con bytes posteriores a los registros declarados suman 4.439.136 bytes. El escritor y lector VB6 terminan en las salidas del mapa; los bytes posteriores no forman parte de una sección leída por el cliente. Informe: `csm_tail_audit.md` y `csm_tail_audit.json`.

## Pendiente y límites

- Faltan definiciones visuales originales para los objetos 566, 567, 568, 570, 727, 757, 759 y 1645. `Dat/obj.dat` no los define y `init/localindex.dat` indica `GRHINDEX=0`. Sus posiciones se conservan. La búsqueda en GitHub queda documentada en `source_gap_report.md`.
- El CSM 844 vacío requiere un ejemplar válido si ese mapa debe existir.
- Lluvia, nieve y niebla se representan en Unity usando gráficos originales. `SetWeather` permite activarlas para pruebas; el cliente Unity todavía no recibe el estado meteorológico desde el servidor. Las marcas del mapa indican capacidad, no precipitación permanente.
- El mundo Unity expone `ApplyRainToggle`, `ApplySnowToggle` y `ApplyFogToggle`, equivalentes a los tres paquetes de clima VB6. La niebla cambia su opacidad a razón de 1 nivel cada 100 ms, como `TimerNiebla`. Aún falta un transporte de red que entregue esos paquetes: el proyecto Unity no contiene un cliente de red. La hora mundial puede fijarse con `SetWorldHour` o sincronizarse con `SyncWorldTime` usando los dos valores del paquete VB6; todavía no llegan desde el servidor. Las partículas ahora usan la escala temporal original (`milisegundos × 0,018`), cuatro colores, giro, vida del grupo y materiales aditivos; pueden quedar diferencias en detalles de aleatoriedad y animación respecto de VB6.
- El mapa 266 tiene 10 salidas hacia 3000–3004, ausentes en la fuente disponible. Ver `source_gap_report.md`.
- Las colas de 790 CSM se preservan en los archivos originales; el cliente VB6 no las lee. Su origen histórico concreto no está demostrado.
- El informe `map_batch_report.json` documenta el lote inicial y conserva advertencias de NPC de *antes* de la migración visual. Para el estado actual de NPC usar `npc_visual_report.json`.
- La recorrida verificó carga y renderizado en los 842 mapas y guardó una muestra visual. Una comparación píxel a píxel con capturas del ejecutable VB6 y mediciones en equipos objetivo siguen pendientes.

## Regenerar

1. `python Tools/map_migration.py --scan` — inventario sin cambios.
2. `python Tools/map_migration.py --batch` — regenerar JSON base desde CSM.
3. `python Tools/npc_visual_migration.py --apply` — reponer cuerpos y cabezas de NPC después del lote base.
4. `python Tools/particle_migration.py --apply` — regenerar definiciones de partículas.
5. `python Tools/map_environment_migration.py --apply` — regenerar ambiente y clima.
6. `python Tools/map_migration.py --audit 1 2 40` — comparación puntual.
7. `python Tools/csm_tail_audit.py` — auditar bytes no referenciados por el formato CSM.
8. `python Tools/find_source_gaps.py` — consultar forks públicos por los recursos ausentes.

El lote base creó `MigrationReports/backup_existing_maps` y `backup_code`; la migración de NPC creó `backup_before_npc_visuals.zip`. En Play, `AO Migrador > QA visual` abre los mapas de control, prueba clima y salida 1→2 sin modificar la partida guardada.
