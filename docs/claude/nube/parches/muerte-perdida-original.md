# Pérdida al morir como el original

**Aprobado por Lucas el 25/09.** Reglas del original: `docs/claude/contenido/perdida_al_morir_original.md`.
Dueños: **Programación** (juego y reglas compartidas), **Servidor** (online), **Contenido** (datos de objetos).
Preparado en la nube contra `384fe18`. Se aplica en la PC.

## Qué pasa al morir
| Regla del original | Implementado |
|---|---|
| Se cae el oro por encima de la billetera: 1000 × nivel | Sí |
| Se cae cada pila entera que se puede tirar, lo equipado incluido (antes se desequipa) | Sí |
| Nunca caen: NoSeCae, Intirable, Destruye, Instransferible, llaves (9), barcos (31) y monturas (44) | Sí. Agrega `noDrop` y `cantThrow` a `items.json` |
| Newbie: hasta nivel 12 no se caen los objetos `Newbie=1` | Sí |
| Arena (trigger 6): no se cae nada | Sí |
| Dónde cae (Tilelibre): radio 0 a 15, fila por fila desde arriba a la izquierda, un objeto por tile, apila el mismo hasta 10.000, sin salidas, NPC ni jugadores | Sí, con la **misma función** en juego y servidor |
| Oro sin lugar: se queda con el jugador. Objeto sin lugar: se pierde | Sí |
| En el piso dura para siempre | Sí (igual que hoy) |
| No aplican o no existen en el proyecto: GM, Pendiente del Sacrificio (no hay ninguno en `obj.dat`), MapasNoDrop (lista vacía), eventos, pirata con Galeón y carros | No |

No se pierde experiencia ni nivel, igual que en el original.

## Cómo está hecho
- **Reglas compartidas** `Runtime/Shared/AODeathDropRulesV900.cs`: C# puro, sin UnityEngine. El servidor lo enlaza desde el csproj, como `AOCoopProtocolV250`; es el mecanismo que `arquitectura.md` pide para `Runtime/Shared`.
- **Offline** `AODeathDropV900.OnPlayerDeath`: lo llama `AODeathRespawnV160.OnDeathStarted` después de `UnequipAllForDeath`.
  - Primero saca cada objeto del inventario y recién después lo crea en el piso: si no se puede sacar, no aparece en el piso (sin duplicados).
- **Online:** el cliente no decide nada.
  - Al morir manda la acción `death` junto con su guardado.
  - El servidor (`CoopRoom.DeathDrop`) calcula qué cae con los datos del catálogo, lo pone en el piso compartido y devuelve el evento `death` (objetos + oro) para que el cliente lo saque de su inventario.
  - Rechaza a un jugador vivo y rechaza una segunda pérdida sin confirmar.
  - Lo tirado lo puede levantar cualquiera, como en el original.
- **Compatibilidad:** sigue siendo el protocolo 2.
  - Un cliente viejo nunca manda `death`, así que con él no se pierde nada ni se duplica nada.
  - Un servidor viejo responde "Acción desconocida" y no pasa nada.
- **Número de módulo provisorio: V900** (reservado para lo hecho en la nube). Si Cerebro prefiere la próxima versión libre, hay que renombrar `AODeathDropV900` y `AODeathDropRulesV900` en los 5 archivos que los usan (los de los 2 parches + el csproj).

## Archivos
| Archivo | Dueño | Qué es |
|---|---|---|
| `muerte-juego.patch` | Programación | Nuevos: `Runtime/Shared/AODeathDropRulesV900.cs` y `Runtime/AODeathDropV900.cs`. Cambian `AOItemDatabaseV10` (+`noDrop`, `cantThrow`) y `AODeathRespawnV160` (1 llamada) |
| `muerte-online.patch` | Servidor | `AOOnlineClientV240` (`ReportDeath`, evento `death`), `CoopRoom` (`DeathDrop`, `CanDropAt`) y csproj (enlace a Shared). **Requiere el de juego** |
| `Tools/add_item_drop_flags.py` (nuevo) | Contenido | Agrega `noDrop`/`cantThrow` a `items.json` desde `obj.dat`. Por defecto simula |
| `Tools/test_death_drop.py` (nuevo) | Servidor/QA | Prueba de la acción `death` |

## Verificación en la nube
- `add_item_drop_flags.py`: 4.275 objetos, 571 NoSeCae y 417 Intirable, igual que `obj.dat`. Fuera de esos 2 campos, `items.json` queda idéntico.
- Con los 2 parches, los datos y el catálogo regenerado: `test_death_drop` **PASA**. Comprueba:
  - oro sobre 1000 × nivel;
  - las 6 clases de objetos que nunca caen;
  - newbie a nivel 5 contra nivel 13;
  - orden de Tilelibre con tiles ocupados por NPC;
  - arena;
  - sin duplicados;
  - otro jugador levanta lo que cayó.
- `test_coop_server` y `test_static_npcs` también **PASAN**.
- Sin los parches, `test_death_drop` **FALLA** ("Acción desconocida").
- C# del juego: sin errores de sintaxis (Roslyn, C# 9). Las reglas compartidas además compilan de verdad dentro del servidor. **Falta compilar en Unity.**
- Después de probar se restauraron `items.json` y el catálogo: esta rama no los trae regenerados.

## Pasos en la PC (en orden)
1. `aod-respaldo`.
2. Aplicar los parches:
   ```powershell
   git apply docs/claude/nube/parches/muerte-juego.patch
   git apply docs/claude/nube/parches/muerte-online.patch
   ```
   - Si el csproj ya enlaza `Runtime/Shared/*.cs` (fase 2 de la demo), sacar la línea que agrega el parche al csproj; si no, se compila dos veces.
3. Actualizar los datos y regenerar el catálogo:
   ```powershell
   python Tools/add_item_drop_flags.py            # simula
   python Tools/add_item_drop_flags.py --aplicar
   python Tools/export_online_catalog.py
   ```
4. Servidor:
   ```powershell
   dotnet build OnlineServer/AOOnlineServer.csproj -v:q -clp:ErrorsOnly
   python Tools/test_death_drop.py; python Tools/test_coop_server.py; python Tools/test_static_npcs.py
   ```
5. Unity (tomar el candado; nunca con un personaje del usuario):
   - **Offline**, con un personaje de prueba de nivel 5, 10.000 de oro, una espada equipada, una poción y una llave. Al morir:
     - caen 5.000 de oro y, en los tiles de alrededor, la espada y la poción;
     - la llave queda en el inventario;
     - aparece el mensaje "Al morir se te cayeron…".
   - Morir parado sobre un tile de arena (trigger 6): no cae nada.
   - **Online** con 2 clientes: el que muere pierde lo mismo, el otro lo ve en el piso y lo puede levantar. Reconectar no duplica nada.
6. Después, sumar `python Tools/test_death_drop.py` a `.github/workflows/pruebas.yml`.
7. Publicar servidor y cliente **juntos**.
