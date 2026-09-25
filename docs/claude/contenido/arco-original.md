# Arco y armas a distancia: cómo funciona en el AO20 original

Contenido · 24/09/2026. Para Programación (decisión 17: el arco dispara a distancia).
Fuentes:
- Cliente `CODIGO/ModGameplayUI.bas` (`UseItemKey`, `UserOrEquipItem`, `RequestProjectileTarget`, clic con `UsingSkill = Proyectiles`) y `Protocol.bas` (`HandleWorkRequestTarget`).
- Servidor `InvUsuario.bas` (`UseInvItem`, rama `otWeapon`) y `Protocol.bas` (`HandleWorkLeftClick`, `Case e_Skill.Proyectiles`).
- Constantes: `Consts.bas`, `Declares.bas`, `SistemaCombate.bas`.

## 1. Datos (items.json, corregido 24/09)
- `projectile = 1`: el arma dispara. 24 armas.
- `munition`: el **subtipo** de munición que usa (`Municiones` en obj.dat). 1 = flechas, 2 = balas, 0 = no gasta munición.
  - 11 armas usan flecha (arcos).
  - 7 usan bala: Trabuco, Pistola, Pistola del Principiante, Mosquete, Tarkul, Cañón Negro.
  - 6 no gastan munición: Hacha Vikinga (+1, +2, Principiante) y Cañón de bolsillo (y el de principiante).
- Munición = objetos tipo 32 (`otArrows`), con `subType` 1 (flechas: Flecha, +1, +2, +3, Élfica, Incendiaria…) o 2 (balas). El daño de la munición se suma al del arco (ver `pvp-formulas.md` §2).

## 2. Equipar
- Arco: se equipa como cualquier arma (doble clic o "Usar" sobre el ítem sin equipar → `EquipItem`).
- **Flechas: doble clic sobre ellas → `EquipItem`.** Van a una **ranura de munición aparte** (`EquippedMunitionSlot`/`EquippedMunitionObjIndex`); los tipos que se equipan así incluyen `otFlechas`.

## 3. Disparar (flujo original)
1. Con el arco **equipado**, "Usar" sobre el arco: tecla **U** (`UseItemKey`) o doble clic / menú Usar. Si el slot es un arma de proyectil equipada, el cliente manda `UseItem(slot, esProyectil=True)` (`RequestProjectileTarget`).
2. Servidor, `UseInvItem` → `otWeapon` con `Proyectil = 1`:
   - rechaza si está muerto, si no tiene energía (`MinSta ≤ 0`) o si el arco no está equipado (o está transformado);
   - si pasa, manda **`WorkRequestTarget(Proyectiles)`**.
3. Cliente: cursor de flecha (`E_ARROW`) y en la consola `Haz click sobre la victima...`.
4. **Clic izquierdo** sobre la casilla del objetivo → `WorkLeftClick(x, y, Proyectiles)`.
   - Intervalos del cliente: si no pasó el de flecha → `No puedes lanzar flechas tan rápido.`, según el modo de bloqueo de la configuración.
5. Servidor, `HandleWorkLeftClick` / `Proyectiles`, en este orden:
   1. Muerto, descansando, aturdido o casilla fuera del mapa → nada.
   2. **Casilla dentro de la pantalla:** `|dx| ≤ 11` y `|dy| ≤ 9` (`InRangoVision`: `MinXBorder = 1 + 23\2`, `MinYBorder = 1 + 18\2`). Si no, `PosUpdate` y sale.
   3. **Corta la meditación.**
   4. Intervalos del servidor: golpe↔magia 800 ms e `IntervaloFlechasCazadores` = **1200 ms**. Además, el cooldown del ítem.
   5. **Munición:** si `munition > 0`, tiene que haber munición equipada de tipo 32, con cantidad ≥ 1 y `subType == munition` del arma. Si falla, **desequipa la munición y cancela** (`WorkRequestTarget(0)`). Con `munition = 0` no pide nada.
   6. **Energía:** hace falta ≥ 10; se gasta **azar(1..10)** por disparo. Si hay menos: "no tenés energía" y cancela.
   7. Busca el objetivo en la casilla clickeada (`LookatTile`: usuario o NPC en esa casilla).
   8. **Contra usuario:**
      - `|dy| ≤ 13` (`RANGO_VISION_Y`), no a sí mismo y `PuedeAtacar`;
      - después `UsuarioAtacaUsuario(…, Ranged)`, que además exige distancia ≤ **18** (`MAXDISTANCIAARCO`);
      - FX y partículas de la flecha, y proyectil visual (`CreateProjectile`).
   9. **Contra NPC:** chequeo de rango con una rareza del original: sale solo si `|dy| > 13` **y** `|dx| > 15` (usa `And`). En la práctica alcanza con el paso 2.
      - Si es atacable → `UsuarioAtacaNpc(…, Ranged)` (acierto con Proyectiles, daño arco + flecha), más el proyectil visual.
   10. **Gasta 1 munición** si hubo ataque (acierte o falle), salvo en zona sin consumo. Si se termina, se desequipa.
- **No hay chequeo de línea de vista** (obstáculos) en el servidor: el proyectil es solo visual. Si Lucas quiere bloquear por paredes, sería **no original**.
- Un clic en una casilla vacía no dispara ni gasta munición.

## 4. Números
| Qué | Valor |
|---|---|
| Rango del clic | ±11 en X, ±9 en Y (la pantalla de 23×18) |
| Distancia máxima contra jugador | 18 |
| Intervalo entre flechas | 1200 ms (servidor) |
| Energía por disparo | 1–10 (hace falta ≥ 10) |
| Munición por disparo | 1 (solo si hubo ataque) |
