# Fórmulas PvP originales (para `AOPvpFormulasV###`)

Contenido · fase 2 · 24/09/2026.
- Transcriptas de `argentum-online-server-master/Codigo`: `SistemaCombate.bas` (`UsuarioImpacto`, `UserDamageToUser`, `GetUserDamageWithItem`, `PoderEvasion`, `AttackPower`) y `modHechizos.bas` (`HechizoPropUsuario`, rama `eDoDamage`). También `Modulo_UsUaRiOs.bas` (`GetUserMR`) y `Balance.dat`.
- `Porcentaje(a, b) = a × b / 100`. `RandomNumber(a, b)` es entero e **incluye los dos extremos**. Donde el original guarda en `Long`/`Integer`, redondea al asignar (VB6 redondea al par: `CLng(2.5) = 2`).
- **[sin dato público]** = la constante se lee de una sección de `Balance.dat` que no está en el repositorio público (tampoco en GitHub master, verificado), así que el servidor la carga como 0.

## 1. Acierto de un golpe (cuerpo a cuerpo o flecha) — `UsuarioImpacto`
```
poderAtaque   = AttackPower(atacante, skill del arma, modificador de ataque de clase)
                ; skill: Armas (espada, hacha…), Proyectiles (arco), Apuñalar (daga), Combate sin armas (sin arma o nudillos)
                ; modificador: MODATAQUEARMAS para Armas/Apuñalar/sin armas, MODATAQUEPROYECTILES para Proyectiles
AttackPower   = (skill + 3 % × skill × AGI) × modificador + 2,5 × max(nivel − 12, 0)   (+ bonus de efectos)
evasion       = PoderEvasion(victima)
              = (Tácticas + 3 % × Tácticas × AGI) × MODEVASION + 2,5 × max(nivel − 12, 0)   (+ bonus de efectos)
si la víctima tiene escudo con Porcentaje > 0:
    evasion += (Defensa con escudo × MODESCUDO / 2) × Porcentaje_escudo / 100
probExito     = clamp(50 + (poderAtaque − evasion) × 0,4, 5, 95)
si la víctima está meditando:
    probEvadir = (100 − probExito) × 0,75
    probExito  = min(90, 100 − probEvadir)
acierta       = RandomNumber(1, 100) ≤ probExito
```
- **Meditación:** `UsuarioAtacaUsuario` llama a `UsuarioAtacadoPorUsuario` **antes** de `UsuarioImpacto`, y eso ya corta la meditación de la víctima. Por eso el bloque "si la víctima está meditando" **nunca se aplica en un golpe**: en la práctica, cualquier ataque (acierte o no) corta la meditación y se tira el acierto normal. Los hechizos de daño también la cortan (`HechizoPropUsuario` → `UsuarioAtacadoPorUsuario`). Se implementa así, sin la penalización.
- "Golpe certero" (`flags.GolpeCertero`): el próximo golpe acierta siempre. Lo da un efecto.
- `Improved-Hit-Chance` (bonus de acierto por arma) está **apagado** en `Example.feature_toggle.ini` (=0): no se suma.
- Si falla y la víctima tiene escudo, hay "rechazo" (`probRechazo = clamp(Porcentaje_escudo × Defensa / (Defensa + Tácticas), 10, 90)`). Es solo visual: sonido y subida de skill.

## 2. Daño de un golpe — `UserDamageToUser`
```
golpePropio   = RandomNumber(MinHIT, MaxHIT) del atacante
                ; MinHIT = (nivel−1) × GOLPE_PRE_36 + 1; MaxHIT = (nivel−1) × GOLPE_PRE_36 + 2   (hasta nivel 36)
arma          = RandomNumber(MinHIT_arma, MaxHIT_arma)            ; contra jugadores usa MinHit/MaxHit (MinHitToNPC/MaxHitToNPC son solo contra NPC)
maxArma       = MaxHIT_arma
si el arma es de proyectil y hay munición:
    arma    += RandomNumber(MinHIT_flecha, MaxHIT_flecha)
    maxArma += MaxHIT_flecha
modClase      = MODDANOPROYECTILES (arco) | MODDANOWRESTLING (nudillos o sin arma) | MODDANOARMAS (resto)
base          = (3 × arma + maxArma × 0,2 × max(0, FUE − 15) + golpePropio) × modClase     (+ bonus lineal de efectos)
lugar         = RandomNumber(1, 8)
defensa       = lugar = 1 (cabeza) → RandomNumber(MinDef, MaxDef) del casco
                lugar 2..8 (cuerpo) → RandomNumber(MinDef, MaxDef) de la armadura + la del escudo
defensa      += bonus de defensa de efectos
defensa      −= penetración de armadura (ver §5)
daño          = max(0, base − defensa) × modificadorFísicoAtacante × reducciónFísicaVíctima   (ambos 1 sin efectos)
```
- En PvP la cabeza sale **1 de cada 8**; en NPC → jugador es 1 de cada 6.
- **Extras que no aplican a las clases de la demo** (guerrero, mago, clérigo, cazador):
  - **Crítico:** solo Bandido con nudillos.
  - **Apuñalar:** Asesino, o skill Apuñalar ≥ 10 con arma que apuñala (`Apuñala=1`). Suma `daño × MODAPUNALAR` (Guerrero 1,4, Cazador 1,3, Clérigo 1,25, Mago 0,1). La probabilidad es `skill × *StabbingChance` más `ExtraBackstabChance` si se ataca por la espalda. Esas constantes están en `[BACKSTAB]` **[sin dato público] → 0 %**.
  - **Desarmar:** Bandido y Ladrón con nudillos.
- Con crítico o apuñalada: si la víctima tenía la vida llena y el daño total la mata de un golpe, queda con la vida mínima (no muere de un golpe estando llena).

## 3. Daño de un hechizo — `HechizoPropUsuario` (efecto `eDoDamage`)
```
daño  = RandomNumber(MinHP, MaxHP) del hechizo
daño += Porcentaje(daño, 3 × nivel del lanzador)
por cada uno de: arma (báculo), amuleto, anillo  — en ese orden:
    daño += Porcentaje(daño, MagicDamageBonus)
    daño += MagicAbsoluteBonus
    penetración += MagicPenetration
si el hechizo NO es AntiRm:
    rm   = max(0, GetUserMR(víctima) − penetración)
    daño −= Porcentaje(daño, rm)
daño  = daño × modificadorMágicoLanzador × reducciónMágicaVíctima   (1 sin efectos)
daño  = max(0, daño)
```
- **Hechizos de área** (`AreaHechizo`, objetivo terreno), contra jugadores:
  - el daño se calcula igual, pero solo suma el `MagicDamageBonus` del arma y del anillo (no los absolutos ni el amuleto);
  - baja un **20 % por casilla** de distancia al centro (`tilDif`);
  - la resistencia se resta **ítem por ítem** (armadura, anillo, escudo y casco, cada uno con su `Porcentaje`) y después `× (1 − MODRESISTENCIAMAGICA)`.
- `GetUserMR` (lo que arquitectura llama `magicDefense`) = `ResistenciaMagica` de armadura + anillo + escudo + casco, + skill Resistencia Mágica × `MagicResistanceSkillProtectionModifier` **[sin dato público] → 0**, + 100 × `MODRESISTENCIAMAGICA` de la clase (0 para las cuatro clases).
- En obj.dat hay 64 ítems con `ResistenciaMagica`: por ejemplo, Sombrero de Mago 5, Sombrero de Aprendiz 3, Armadura de Dragón Negro 3, Escamas del Dragón Negro 2, Coraza Clerical 1.
- Penetración: Bastón Calavera 5, Báculo del Fuego Eterno 10, Arpa del Cosmos 6, Cruz Sagrada 5.
- Un hechizo de daño corta siempre la meditación de la víctima (vía `UsuarioAtacadoPorUsuario`). La regla del umbral de daño (`vida × INT × Meditar …`) es solo para golpes de NPC (`NpcDamage`).

## 4. Intervalos (`intervalos.ini`, ms)
| Acción | Intervalo |
|---|---|
| Golpe | 1165 (`IntervaloUserPuedeAtacar`) |
| Flecha | 1200 (`IntervaloFlechasCazadores`) |
| Hechizo | 1230 (`IntervaloLanzaHechizo`) |
| Golpe → hechizo / hechizo → golpe | 800 (`IntervaloGolpeMagia`, `IntervaloMagiaGolpe`) |
| Golpe → usar objeto | 800 (`IntervaloGolpeUsar`) |
| Poción con U / con clic | 380 / 276 |

## 5. Penetración de armadura — `GetArmorPenetration`
- `armor_penetration_feature` está **encendido** en el toggle de ejemplo.
- Probabilidad = `skill Armas × IgnoreArmorChance` **[sin dato público] → 0 %**. Así que con los datos públicos **no hay penetración**.
- Si Lucas quiere activarla, hacen falta los valores de `[BACKSTAB]` del servidor oficial.

## 6. Qué implementar en la demo (recomendación)
- Acierto, daño de golpe y daño de hechizo tal cual (§1–3), con apuñalar, crítico y penetración en 0 (datos públicos).
- Tope de pociones rojas por reto (`PocionesMaximas`, opcional en el formulario de `ModRetos`).
- Para las pruebas de Programación (tabla dorada), un caso de ejemplo. Guerrero nivel 20 (FUE 19, Armas 92) con Hacha de Bárbaro 8–15, contra un guerrero con armadura 17–20 y casco 10–15, sin escudo:
  - Golpe propio a nivel 20: `MinHIT = 19×3 + 1 = 58`, `MaxHIT = 59`.
  - Tiradas fijas `arma = 12`, `golpePropio = 59`, `lugar = 3`, `defensa armadura = 18`.
  - `base = (3×12 + 15×0,2×4 + 59) × 1,05 = (36 + 12 + 59) × 1,05 = 112,35` → `daño = 112,35 − 18 = 94,35` → **94** (`CLng`).
