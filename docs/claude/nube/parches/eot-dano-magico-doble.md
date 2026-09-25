# Parche: daño mágico de los EOT aplicado dos veces

Dueño: **Programación**. Preparado en la nube el 25/09, sin Unity. Contra el commit `384fe18`.

## Bug
En `AOMagicEffectRuntimeV129.cs:107-108`, el tick de daño de un efecto persistente (EOT, `type==1` con valor negativo) llama a `ModifyIncomingMagic` antes de pasar el daño al receptor. Pero los receptores ya lo aplican adentro:
- Jugador: `AOPlayerCombatV09.ReceiveMagicDamage` (l.~690).
- NPC: `AONPCCombatV09.TakeMagicDamage` (l.~304).

**Efecto:**
- La reducción mágica se aplica dos veces. Con 50 % de reducción, un tick de 10 hace 2–3 en vez de 5.
- El escudo de absorción (`Absorb` / `protection`) se consume dos veces por tick.
- Pasa en el jugador (lo que decía el tablero) **y también en los NPC**.

Los demás llamadores (veneno, incineración, hechizos directos de `AOPlayerMagicV120`) ya pasan el daño crudo: son correctos.

## Arreglo
`eot-dano-magico-doble.patch`: pasa `-amount` crudo en las 2 líneas. El receptor aplica la reducción y el escudo una sola vez.

```powershell
git apply --check docs/claude/nube/parches/eot-dano-magico-doble.patch
git apply docs/claude/nube/parches/eot-dano-magico-doble.patch
```
Si la PC cambió esas líneas a la noche y `--check` falla, aplicarlo a mano: son 2 reemplazos de `ModifyIncomingMagic(-amount)` por `-amount`.

## Prueba (en Unity, con `aod-respaldo` antes)
1. Jugador con un efecto que dé `MagicDamageReduction` = 0,5 y sin escudo. Aplicarle un EOT de daño fijo (`tickPowerMin = tickPowerMax = -10`).
   - Esperado: cada tick baja **5** de vida. Antes del parche bajaba 2 o 3.
2. Lo mismo sobre un NPC: cada tick baja **5**.
3. Con escudo `protection = 8` y sin reducción, EOT de 10: el primer tick baja 2 de vida y el escudo queda en 0. Antes del parche, el escudo se gastaba dos veces.
4. Verificación rápida: `test_modules_unity.py` (V130 EOT) sigue pasando.

Sugerencia para QA: sumar el caso 1 a `AOModulesQA270` (V130) cuando se aplique.
