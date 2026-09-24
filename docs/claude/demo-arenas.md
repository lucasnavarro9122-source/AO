# Demo AO BATTLESERVER: especificación

Estado: **fase 1 (análisis y diseño)**. La implementación empieza cuando Lucas aprueba el diseño consolidado por Cerebro.

## Pedido de Lucas (24/09)
Una zona de demo **aparte del mapa actual** con dos caminos:

### A) Zona PvP: arenas
- Varias arenas **separadas con espacio entre sí** para que otros jugadores **miren las peleas** desde afuera.
- **Apuestas** habilitadas.
- **Conteo** antes de empezar.
- **Generador de mapa** de la arena: el interior del ring **no es siempre igual**.

### B) Camino al dungeon: mazmorras
- Un dungeon que lleva a mazmorras con NPC **de menor a mayor**: los más débiles al principio y el más fuerte al final.
- **Distancias pensadas para un farmeo cómodo** según la subida de nivel.
- Bichos **acordes al nivel** y **drops acordes** para que **cualquier clase** pueda subir.
- **No demasiado fácil.**

## Supuestos de Cerebro (Lucas puede corregirlos)
- **Mapas nuevos** con IDs propios (≥ 1000), separados de los 842 originales.
  - Un **mapa hub de demo**, donde se aparece al entrar a la demo, con dos salidas: Arenas y Dungeon.
  - Acceso al hub desde el menú: "Demo AO BATTLESERVER".
- **PvP solo dentro del ring** y solo durante un reto aceptado. Fuera del ring nadie puede dañar a nadie; los espectadores no pueden interferir (hechizos y proyectiles bloqueados por el borde del ring).
- **Retos** basados en `Dat/Retos.dat` y `ModRetos.bas`:
  - 1 vs 1 a 5 vs 5, apuesta en oro con **custodia del servidor** (el oro se retiene al aceptar y se paga al ganador) y impuesto del 10 %.
  - Conteo de 15 s, máximo 600 s, rondas. Al morir en el ring **no se caen ítems** (`CaenItems=false`).
- **Espectadores**: las arenas quedan dentro de una zona común, con bordes visibles y pasillos o gradas alrededor. Cualquiera ve la pelea de cerca y en el chat se anuncia quién pelea y cuánto se apuesta.
- **Generador de arena**: layout del interior del ring de 23×19 generado con una **semilla** que elige el servidor y manda a jugadores y espectadores (todos ven lo mismo).
  - Obstáculos (pilares, árboles, rocas, agua) **simétricos** para que sea justo.
  - Siempre hay camino entre los dos lados y ningún spawn queda encerrado.
  - Temas visuales por región (bosque, desierto, nieve, dungeon, pantano, ciudad).
- **Dungeon**: niveles 1 → ~30 para la demo (a confirmar con el análisis), en varios pisos/mazmorras con NPC **originales** de AO elegidos por fuerza, con sus drops originales, ajustados solo en ubicación, cantidad y respawn (no en stats).
- Balance de clases intacto.

## Fase 1: tareas por sector (solo diseño y análisis; no tocar `Assets/`)
| Sector | Entregable (en `docs/claude/demo/`) |
|---|---|
| Contenido | `progresion.md` + `dungeon-npcs.json`: tabla de EXP, NPC candidatos por tramo de nivel (HP, daño, defensa, EXP, oro, drops, hechizos), bichos por piso, densidad y respawn, **tiempo estimado para subir cada nivel** por clase (guerrero, mago, clérigo, cazador como mínimo) y distancias entre grupos. Debe cuidar que no sea ni muy fácil ni muy lento. |
| Programación | `arquitectura.md`: cómo crear mapas nuevos de demo (hub, zona de arenas, pisos del dungeon) con el sistema actual (`WorldV07`, `AOGridMap`, World Manager); diseño del generador de arena con semilla (algoritmo, simetría, validación de caminos); reglas del PvP local dentro del ring. |
| Servidor | `red.md`: PvP autoritativo en el servidor, flujo de reto (invitar/aceptar/sala/conteo/rondas/fin), custodia de apuestas sin duplicados, envío de la semilla, espectadores, cambios de protocolo (protocolo 3) y compatibilidad. |
| Interfaz | `ui.md`: pantallas de desafío/aceptar/apuesta, conteo, marcador de rondas, anuncio a espectadores y acceso a la demo desde el menú, fieles al estilo clásico. |
| Arte | `arte.md`: set de tiles y objetos por tema para el generador y los pisos del dungeon (sacados de mapas originales), bordes y gradas de la zona de arenas, y plan de remaster con Higgsfield por tema. |
| QA | `pruebas.md`: cómo se va a probar: validez y justicia del generador (N semillas), apuestas sin duplicar oro, reto completo con 2 clientes, simulación de progresión del dungeon. |

Cerebro consolida todo en un plan de implementación, lo presenta a Lucas y, con su OK, abre la fase 2.

## Datos originales ya verificados
- `Dat/Retos.dat`: 16 salas de 23×19 en los mapas 324/372/389/390 ("Zona de Ring"), 5 vs 5, apuesta mínima 10.000, impuesto de 0,1, 600 s, conteo de 15 s, 60 s para juntar ítems.
- `Dat/Scenarios/DeathMatch.ini` (mapa 49, ring que se achica con los postes `OBJ3763–3768`), `NavalBattle.ini`, `snakes.ini`. Arena de Clanes: mapas 272/273.
- Código: `ModRetos.bas`, `ModTorneos.bas`, `CustomScenarios.bas`, `ScenarioDeathMatch.cls`.
- Los 7 mapas ya están importados en `Resources/AOMigrator/WorldV07/Maps/`.
- Único faltante de los originales: 8 audios de hechizo.
