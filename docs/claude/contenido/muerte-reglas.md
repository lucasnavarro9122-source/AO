# Pérdida al morir: reglas originales (AO20)

Contenido · 24/09/2026. Para Programación (tarea "Pérdida de objetos/oro al morir").
Fuente: `argentum-online-server-master/Codigo`:
- `Modulo_UsUaRiOs.bas` (`UserDie`, ~l. 1779);
- `InvUsuario.bas` (`TirarTodosLosItems`, `ItemSeCae`, `DropAmmount`, `ItemNewbieProtegidoAlMorir`);
- `GameLogic.bas` (`EsNewbie`, `CargarMapasEspeciales`), `FileIO.bas` (`DropItems`);
- `Example.Configuracion.ini`.

Hoy el juego no suelta nada al morir (`AODeathRespawnV160.cs:146-178`: desequipa, fantasma y `/HOGAR`).

## 1. Cuándo se tiran los ítems
Al morir se llama a `TirarTodosLosItems` **solo si se cumplen las tres**:
1. La casilla donde muere **no** tiene trigger 6 (`ZONAPELEA`).
2. El mapa tiene `DropItems = True`. Vale para todos los mapas salvo los de la lista `[MapasNoDrop]` de `MapasEspeciales.dat`, y **esa sección no existe en los datos públicos**: ningún mapa es "no drop".
3. Es un usuario común (no GM).

Si lleva equipado el **Pendiente del Sacrificio**, en vez de tirar todo se consume el pendiente y no se pierde nada más.

## 2. Qué se tira
**Oro:** se tira al piso `oro − OroPorNivelBilletera × nivel`, con `OroPorNivelBilletera = 1000` (`Configuracion.ini`).
- Es decir, **1.000 de oro por nivel quedan protegidos**. Ejemplo: nivel 10 con 25.000 → se tiran 15.000.

**Ítems:** se tira **todo el inventario, incluido lo equipado**, salvo:
- los que no caen según `ItemSeCae`: `NoSeCae=1`, llaves (`otKeys`), barcos, monturas, `Intirable=1`, `Destruye=1`, `Instransferible=1`;
- los objetos **newbie** (`Newbie=1`) si el personaje es **newbie**, o sea, nivel ≤ 12 (`LimiteNewbie = 12`, constante del servidor).

**Cantidad:** el stack completo (`DropAmmount`). Excepción: con un amuleto de protección (`EfectoMagico=12`), una parte de los minerales, maderas y peces queda protegida según los porcentajes del amuleto.

- Pirata nivel ≥ 37 en galeón: la regla tiene un 33 % aleatorio, pero en el código actual siempre devuelve que cae (`PirataCaeItem = True`).
- Mapas 58–61 durante un evento faccionario: no se tira nada.

**Dónde cae:** en la casilla libre más cercana (`Tilelibre` + `ClosestLegalPos`). Si no hay lugar, el ítem se pierde.

## 3. Qué no se pierde
- EXP y nivel: no hay pérdida de experiencia al morir.
- Hechizos aprendidos, skills y atributos.
- El oro del banco.

## 4. En la demo AO BATTLESERVER
- **Retos:** no se tira nada (`CaenItems=false`, decisión de Lucas; ring con trigger 6).
- **Dungeon (1011–1017) y hub:** con las reglas originales, se tira todo menos 1.000 de oro por nivel y, hasta el nivel 12, los objetos newbie. **Decide Lucas**:
  - (a) original: se cae todo, y el grupo o uno mismo vuelve a buscarlo;
  - (b) poner los mapas de la demo en `MapasNoDrop`. Es el mecanismo original, pero la lista pública está vacía.

  Recomiendo **(a)** en el dungeon, por fidelidad y para que "no sea fácil". Los personajes de la demo están separados de las partidas reales (decisión 3), así que no hay riesgo para los guardados de verdad.
