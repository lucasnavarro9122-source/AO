# Demo AO BATTLESERVER: progresión del dungeon

Contenido y Fidelidad AO · fase 1 (solo análisis) · 24/09/2026.
- Datos completos por NPC (stats, hechizos, drops, mapas de origen) y tiempos por nivel: `dungeon-npcs.json`.
- Modelo reproducible: `modelo_progresion.py`. Reglas de retos (empate, tiempo agotado, impuesto): `retos-reglas.md`.
- Fuentes: ao-org AO20.
  - `Recursos-master/Dat`: `npcs.dat`, `obj.dat`, `Hechizos.dat`, `Balance.dat`.
  - Servidor VB6: `SistemaCombate.bas`, `modHechizos.bas`, `Trabajo.bas`, `Modulo_UsUaRiOs.bas`, `intervalos.ini`, `Example.Configuracion.ini`.
  - Drops: `Resources/AOMigrator/LootV180/npc_loot.json`.

## Resumen y decisiones para Lucas
1. **7 pisos, niveles 1 → 30**, cada uno con NPC que en el AO original ya conviven en la misma zona. Hay un jefe por piso y el más fuerte queda al final (Vytaiz, 20.000 PV).
2. **La EXP original (×1) no sirve para la demo.** Llegar a 30 lleva entre 168 h (guerrero) y 285 h (clérigo); del 20 al 30 cada nivel tarda entre 3 y 70 h.
   - **Propuesta:** multiplicador de EXP del servidor **por tramo de nivel** (×1 · ×2 · ×7 · ×17 · ×30 · ×44). Así el total queda en **7–13 h** y cada nivel tarda entre 2 y 60 min.
   - No toca stats de NPC ni de clases, pero hay que programarlo: el original solo tiene un `ExpMult` global.
   - Un `ExpMult` global ×10–×20 también funciona (8–33 h), pero los pisos 1–3 se pasan en minutos.
   - **Decide Lucas.**
3. **El oro no alcanza para las pociones** desde el piso 3, y el cazador gasta ~2.300 flechas por hora.
   - En el AO20 los NPC casi solo tiran oro: el equipo se compra.
   - **Opciones:** `OroMult` ×2 (config original del servidor), descansar entre grupos o jugar en grupo con clérigo.
   - **Decide Lucas.**
4. **Nuestro juego difiere del original** en cosas que cambian estos tiempos (sección 9): el arco no dispara a distancia, no se regenera vida, y el intervalo de ataque es de 0,75 s en vez de 1,165 s. Son pedidos para Programación.

## 1. Tabla de EXP (la del juego)
Es la de `Balance.dat [EXP]`, igual a `rpg_balance.json` (`experience`). Al subir de nivel se descuenta: `Exp = Exp − ELU`.

| Nv | EXP | Nv | EXP | Nv | EXP |
|---|---|---|---|---|---|
| 1 | 500 | 11 | 28.832 | 21 | 1.053.130 |
| 2 | 750 | 12 | 43.248 | 22 | 1.474.382 |
| 3 | 1.125 | 13 | 71.360 | 23 | 2.064.135 |
| 4 | 1.687 | 14 | 99.904 | 24 | 2.889.789 |
| 5 | 2.531 | 15 | 139.866 | 25 | 4.450.275 |
| 6 | 3.796 | 16 | 195.813 | 26 | 5.562.844 |
| 7 | 5.695 | 17 | 274.138 | 27 | 6.953.555 |
| 8 | 8.542 | 18 | 383.793 | 28 | 8.691.944 |
| 9 | 12.814 | 19 | 537.311 | 29 | 10.864.930 |
| 10 | 19.221 | 20 | 752.235 | 30 | 13.581.163 |

## 2. Fórmulas originales usadas (servidor VB6)
- **EXP:** por golpe, proporcional al daño: `GiveEXP × daño / MaxHP` (el total por NPC es `GiveEXP`), × `ExpMult` (=1 en el ejemplo).
  - Penalización: si tu nivel supera al `NPCLVL` en más de 4, pierde 5 % por nivel extra (`PenaltyExpUserPerLevel=0.05`).
- **Golpe físico:** acierto = `50 + (Ataque − Evasión NPC) × 0,4`, limitado a 5–95 %.
  - `Ataque = (skill + 3 % × skill × AGI) × modClase + 2,5 × (nivel − 12)`.
  - Daño = `(3 × arma + máxArma × 0,2 × (FUE − 15) + golpe propio) × modDaño − DEF NPC`.
  - Golpe propio = `(nivel − 1) × GolpeClase + 1..2`.
  - Intervalos: 1165 ms cuerpo a cuerpo; 1200 ms flechas (una flecha por disparo).
- **Hechizo:** `azar(MinHP, MaxHP) × (1 + 3 % × nivel)` + % del báculo.
  - Resta la defensa mágica: `MagicDef % + 2 % × (RM − skill Magia)`, y después `DEFm` fijo.
  - Intervalo: 1230 ms.
- **Maná:** `INT × ManaInicial + MultMana × INT × (nivel − 1)`.
  - Meditar: `MaxMAN × (3,5 % + 0,035 % × skill Meditar)` cada 400 ms, después de 0,8 s.
- **Vida:** `CON + (Vida clase − (21 − CON) × 0,5) × (nivel − 1)`.
  - Solo se regenera fuera de combate (10 s sin pelear): 5–10 % cada 8 s, o cada 2 s descansando.
  - Poción roja: 27 PV fijos, 18 de oro, intervalo 380 ms.
- **NPC → jugador:** acierto `50 + (PoderAtaque − Evasión) × 0,4`, limitado a 10–90 %.
  - Daño = `azar(MinHIT, MaxHIT)` − armadura (o casco, 1 de cada 6 golpes) − escudo.
  - Ataca cada 2000 ms (salvo `IntervaloAtaque`) y lanza hechizos cada 8000 ms (salvo `IntervaloLanzarHechizo`).
- **Skills por uso:** tope `≈ 2,5 × nivel` (`SubirSkill`), más 10 + 5 puntos por nivel para asignar.

## 3. Supuestos del modelo (estimación, no medición)
- Razas: Humano para guerrero, clérigo y cazador (FUE 19, AGI 19, INT 18, CON 20); Gnomo para el mago (INT 22, AGI 21, CON 18).
- Skill de ataque: uso + 40 % de los puntos (el mago pone 60 % en Magia). Tácticas: uso + 15 %. Meditar: uso + 20 %.
- Equipo comprado a comerciantes originales:
  - **Guerrero:** Espada Larga → Hacha de Piedra (nv 5) → Hacha de Bárbaro (10).
  - **Clérigo:** igual que el guerrero, con Maza de Guerra en los niveles 5–7.
  - **Cazador:** Arco Simple → Reforzado (5) → Compuesto (10), con Flecha / Flecha +1.
  - **Mago:** Vara de Fresno → Bastón Nudoso (10).
  - Tope = lo que venden los comerciantes originales del hub. El Hacha de Guerra Dos Filos, el Arco de Roble y el Báculo Engarzado solo los venden mercaderes que no están en ningún mapa (actualizado en fase 2).
  - **Hechizos** (mago y clérigo): Dardo, Flecha Mágica, Flecha Eléctrica, Misil y Tormenta de Fuego según la skill Magia; Descarga desde el nivel 20 (cuesta 600.000 de oro).
  - **Defensa** total supuesta por tramo: 2 / 7 / 12 / 17 / 24 / 30 (el mago, 70 %).
- 4 s entre muertes (caminar al grupo siguiente).
- Parte de los golpes del NPC que se reciben, según cómo se ataca (se elige el modo con más daño por segundo): cuerpo a cuerpo 100 %, arco 60 %, hechizo 35 %. El clérigo que castea recibe el 35 %.
- Hechizos: ciclo "lanzar hasta vaciar el maná y meditar hasta llenar". El tiempo por lanzamiento es `1,23 s + costo ÷ regeneración + 0,8 s × costo ÷ maná máximo` (arranque de la meditación prorrateado).
- EXP/min de un piso = promedio de los NPC que la clase puede farmear solo. Tiempo por NPC = `vida ÷ daño por segundo + 4 s + pociones × 0,38 s`.
- **Modelo reproducible:** `docs/claude/demo/modelo_progresion.py` tiene todas estas fórmulas y supuestos en un solo lugar. Con `--check` compara contra `tiempoPorNivel` del JSON (hoy da OK). Para D-01, QA debería caer dentro de ±20 % con los mismos supuestos.
- Un NPC es "farmeable solo" si:
  - te hace ≤ 30 PV/s (las pociones cubren 71 PV/s);
  - su golpe máximo, después de la armadura, es ≤ 50 % de tu vida (100 % para el mago).
- El jugador farmea el piso de su tramo; si no puede con ningún NPC, baja al piso anterior.

## 4. Pisos
| Piso | Nivel | Tema y origen | Comunes (vivos) | Jefe (respawn) | Respawn |
|---|---|---|---|---|---|
| P1 Madriguera | 1–4 | Dungeon Newbie (mapas 37, 167, 168, 264) | 16 | Wolfang 250 PV (60 s) | 20 s |
| P2 Cementerio | 4–10 | Cementerio de Nix (mapa 4) | 14 | Humano No-Muerto 1000 PV (3 min) | 30 s |
| P3 Mausoleo | 10–15 | Mausoleo (mapa 392) | 10 | Guardián del Mausoleo 5000 PV (2 min, original 100–120 s) | 45 s |
| P4 Pirámide | 15–20 | Pirámide (mapas 564, 567) | 12 | Momia 5000 PV (5 min) | 60 s |
| P5 Nido de arañas | 20–24 | Catacumbas Lvl2 (mapas 291–293) | 12 | Mutante Arácnido 7000 PV (5 min, original) | 60 s |
| P6 Torre de Veriil | 24–27 | Dungeon Veriil / Nueva Esperanza | 10 | Medusa 7000 PV (8 min) | 90 s |
| P7 Guarida del Dragón | 27–30 | Dungeon Dragon (391); jefe del Limbo de los Justos (314) | 8 | **Vytaiz 20.000 PV** (6 min, original 300–360 s) | 120 s |

NPC originales, stats sin cambios (Nv = `NPCLVL`; RM = resistencia mágica):

| Piso | NPC (#) | Rol × cant. | Nv | Vida | Golpe | DEF | Ataque/Evasión | RM | EXP | Oro | Hechizos | Drop original |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| P1 | Serpiente Collet (935) | común ×4 | 2 | 30 | 5–9 | 0 | 30/25 | 0 | 36 | 6 | — | solo oro |
| P1 | Escorpión (509) | común ×3 | 2 | 30 | 6–10 | 0 | 30/10 | 0 | 30 | 9 | — | solo oro |
| P1 | Murciélago (506) | común ×3 | 2 | 15 | 1–3 | 0 | 30/10 | 0 | 18 | 3 | — | solo oro |
| P1 | Rata (511) | común ×2 | 2 | 15 | 2–5 | 0 | 10/10 | 0 | 18 | 5 | — | solo oro |
| P1 | Serpiente Bicéfala (113) | fondo ×2 | 5 | 125 | 8–15 | 0 | 20/20 | 5 | 100 | 25 | — | solo oro |
| P1 | Lobo (500) | fondo ×2 | 6 | 60 | 10–15 | 0 | 80/50 | 0 | 72 | 0 | — | Piel de Lobo Salvaje |
| P1 | Wolfang (965) | jefe | 6 | 250 | 20–25 | 0 | 100/40 | 0 | 250 | 150 | — | 3 Pieles de Lobo |
| P2 | Esqueleto (514) | común ×4 | 7 | 350 | 12–15 | 0 | 30/30 | 17 | 385 | 35 | — | solo oro |
| P2 | Esqueleto Vigía (946) | común ×4 | 9 | 400 | 13–18 | 0 | 30/40 | 23 | 480 | 36 | — | solo oro |
| P2 | Esqueleto Guerrero (945) | común ×3 | 7 | 600 | 10–25 | 0 | 30/30 | 17 | 690 | 57 | — | solo oro |
| P2 | Zombie (507) | fondo ×3 | 8 | 500 | 20–30 | 0 | 150/35 | 20 | 625 | 40 | — | solo oro |
| P2 | Humano No-Muerto (563) | jefe | 17 | 1.000 | 45–50 | 0 | 105/120 | 43 | 1.200 | 110 | — | solo oro |
| P3 | Esqueleto Mágico (1002) | común ×4 | 11 | 2.000 | 20–35 | 0 | 125/50 | 20 | 2.600 | 200 | Flecha Mágica | solo oro |
| P3 | Liche Menor (1003) | común ×3 | 13 | 2.200 | 30–40 | 0 | 125/75 | 33 | 2.750 | 242 | — | solo oro |
| P3 | Esqueleto Dragón (1001) | fondo ×3 | 14 | 2.500 | 45–60 | 0 | 111/76 | 35 | 3.000 | 300 | — | solo oro |
| P3 | Guardián del Mausoleo (524) | jefe | 17 | 5.000 | 70–145 | 0 | 300/120 | 35 | 6.500 | 500 | — | Anillo de Oro |
| P4 | Escorpión (1223) | común ×4 | 7 | 500 | 40–60 | 0 | 60/30 | 0 | 550 | 75 | — | solo oro |
| P4 | Escorpión Califa (1224) | común ×3 | 13 | 1.500 | 50–65 | 0 | 100/75 | 0 | 1.800 | 180 | — | solo oro |
| P4 | Escarabajo (647) | común ×3 | 17 | 2.000 | 60–75 | 0 | 120/120 | 0 | 2.500 | 220 | — | solo oro |
| P4 | Escarabajo Gigante (646) | fondo ×2 | 20 | 3.000 | 80–120 | 0 | 160/140 | 0 | 3.900 | 315 | — | solo oro |
| P4 | Momia (645) | jefe | 28 | 5.000 | 110–150 | 0 | 160/180 | 0 | 7.000 | 500 | — | solo oro |
| P5 | Araña Nociva (970) | común ×3 | 16 | 2.200 | 95–110 | 0 | 150/80 | 40 | 2.530 | 242 | — | solo oro |
| P5 | Araña Venenosa (975) | común ×3 | 16 | 2.350 | 95–115 | 0 | 150/85 | 40 | 2.585 | 282 | — | solo oro |
| P5 | Araña Poison (977) | común ×2 | 17 | 2.500 | 110–120 | 0 | 150/100 | 43 | 2.750 | 300 | — | solo oro |
| P5 | Viuda Negra (973) | fondo ×2 | 18 | 2.800 | 115–125 | 0 | 150/105 | 45 | 3.220 | 308 | — | solo oro |
| P5 | Latrodectus (978) | fondo ×2 | 22 | 3.000 | 120–135 | 0 | 150/115 | 55 | 3.600 | 300 | — | solo oro |
| P5 | Mutante Arácnido (979) | jefe | 13 | 7.000 | 150–220 | 0 | 150/135 | 33 | 9.100 | 350 | — | solo oro |
| P6 | Liche (557) | común ×4 | 22 | 2.000 | 120–170 | 0 | 220/150 | 45 | 2.800 | 380 | — | solo oro |
| P6 | Mago Malvado (533) | común ×4 | 22 | 3.000 | 100–115 | 0 | 200/150 | 55 | 4.400 | 600 | Tormenta de Fuego, Paralizar | 1/20 Sombrero de Aprendiz |
| P6 | Orco Brujo (555) | élite ×2 | 24 | 2.500 | 90–210 | 0 | 200/165 | 60 | 7.000 | 300 | Flecha Eléctrica | 1/20 Vara de Fresno |
| P6 | Medusa (540) | jefe | 23 | 7.000 | 115–160 | 0 | 220/155 | 57 | 11.250 | 350 | Descarga Eléctrica, Inmovilizar | 1/100 Pendiente del Experto |
| P7 | Pequeño Dragón Azul (595) | común ×4 | 25 | 6.500 | 105–115 | 10 | 200/120 | 20 | 8.450 | 650 | Tormenta de Fuego | 1/125 pergamino Tormenta de Fuego |
| P7 | Pequeño Dragón Rojo (587) | común ×4 | 25 | 5.000 | 110–120 | 10 | 200/120 | 20 | 6.750 | 450 | Tormenta de Fuego | 1/100 pergamino Tormenta de Fuego |
| P7 | Vytaiz (544) | jefe final | 27 | 20.000 | 160–180 | 20 | 500/175 | 25 | 35.000 | 2.000 | Paralizar, Incinerar | 1/300 Insignia Roja o Azul |

- **Descartados:**
  - **Cloacas/Drenaje** (Rata Gigante, Murciélago Gigante, Arañas, Bandolero): rinden 20–40 % menos EXP/min que el Mausoleo en el mismo tramo.
  - **Lagarto:** solo agua.
  - **Liche (557), Orco Brujo y Aksha/Kadru/Lynn:** su golpe máximo (≥ 170) no se aguanta solo antes del nivel 27.
- **P6 y P7 son de grupo para guerrero y clérigo.** Solo, el clérigo baja al P5 desde el nivel 25. El Liche solo lo farmean mago y cazador.

## 5. Distribución, distancias y respawn
- Mapas 1011–1017 (P1–P7, rango de `arquitectura.md`). En `dungeon-npcs.json`, cada piso trae `zonas` G1…Gn en orden de recorrido, qué NPC y cuántos van en cada una, y la sala del JEFE. Las coordenadas exactas las pone el builder sobre el recorte de Arte.
- Mapa útil de unos 70×70. En la entrada, zona segura de 8 tiles sin spawns, con la escalera de subida.
- **Grupos de 3–4 NPC** con **≥ 16 tiles entre centros**: la visión de los NPC es 14×12 (`NPCVisionRange`), así que atacar un grupo no arrastra al siguiente. Son unos 3,4 s caminando (210 ms/tile).
- **Recorrido en circuito**, sin volver sobre los pasos: 4–5 grupos en unos 150 tiles, alrededor de 35 s para dar la vuelta. Comunes cerca de la entrada, "fondo" al final del circuito.
- **Sala del jefe** de 12×12 al final, con un pasillo de ≥ 16 tiles desde el último grupo.
  - La escalera al piso siguiente se alcanza sin matar al jefe (amigos de distinto nivel).
- **Cantidad y respawn**, calculados para 4 jugadores a la vez en el piso:
  - `vivos ≥ 4 × respawn / tiempo por muerte + 4` de margen.
  - El original reaparece al instante, en cualquier lugar del mapa. Propuesta: respawn en su zona de grupo y nunca a menos de 5 tiles de un jugador (pedido a Programación).
- Cartel en cada escalera: "Nivel recomendado X–Y".

## 6. Tiempo estimado por nivel (minutos, con el multiplicador propuesto)
| Nv | EXP para subir | Mult. | Piso G/M/Cl/Cz | Guerrero | Mago | Clérigo | Cazador |
|---|---|---|---|---|---|---|---|
| 1 | 500 | ×1 | 1/1/1/1 | 2 | 2 | 2 | 3 |
| 2 | 750 | ×1 | 1/1/1/1 | 3 | 2 | 3 | 3 |
| 3 | 1.125 | ×1 | 1/1/1/1 | 4 | 4 | 4 | 4 |
| 4 | 1.687 | ×1 | 2/2/2/2 | 2 | 2 | 3 | 3 |
| 5 | 2.531 | ×1 | 2/2/2/2 | 2 | 2 | 3 | 2 |
| 6 | 3.796 | ×1 | 2/2/2/2 | 3 | 3 | 4 | 3 |
| 7 | 5.695 | ×1 | 2/2/2/2 | 4 | 4 | 6 | 4 |
| 8 | 8.542 | ×1 | 2/2/2/2 | 5 | 6 | 7 | 6 |
| 9 | 12.814 | ×1 | 2/2/2/2 | 7 | 8 | 10 | 8 |
| 10 | 19.221 | ×2 | 3/3/3/3 | 4 | 5 | 7 | 5 |
| 11 | 28.832 | ×2 | 3/3/3/3 | 6 | 7 | 9 | 7 |
| 12 | 43.248 | ×2 | 3/3/3/3 | 8 | 10 | 12 | 9 |
| 13 | 71.360 | ×2 | 3/3/3/3 | 12 | 16 | 20 | 14 |
| 14 | 99.904 | ×2 | 3/3/3/3 | 15 | 22 | 25 | 18 |
| 15 | 139.866 | ×7 | 4/4/4/4 | 8 | 10 | 11 | 9 |
| 16 | 195.813 | ×7 | 4/4/4/4 | 10 | 13 | 15 | 11 |
| 17 | 274.138 | ×7 | 4/4/4/4 | 13 | 18 | 20 | 15 |
| 18 | 383.793 | ×7 | 4/4/4/4 | 18 | 24 | 27 | 20 |
| 19 | 537.311 | ×7 | 4/4/4/4 | 24 | 34 | 37 | 27 |
| 20 | 752.235 | ×17 | 5/5/5/5 | 12 | 20 | 20 | 13 |
| 21 | 1.053.130 | ×17 | 5/5/5/5 | 16 | 28 | 28 | 18 |
| 22 | 1.474.382 | ×17 | 5/5/5/5 | 22 | 40 | 40 | 24 |
| 23 | 2.064.135 | ×17 | 5/5/5/5 | 31 | 56 | 56 | 34 |
| 24 | 2.889.789 | ×30 | 5/6/6/6 | 24 | 32 | 34 | 26 |
| 25 | 4.450.275 | ×30 | 6/6/6/6 | 33 | 48 | 51 | 37 |
| 26 | 5.562.844 | ×30 | 6/6/6/6 | 39 | 58 | 62 | 44 |
| 27 | 6.953.555 | ×44 | 7/7/6/7 | 32 | 51 | 54 | 36 |
| 28 | 8.691.944 | ×44 | 7/7/6/7 | 39 | 63 | 69 | 44 |
| 29 | 10.864.930 | ×44 | 7/7/6/7 | 46 | 77 | 89 | 52 |
| **Total** | | | | **7,4 h** | **11,1 h** | **12,1 h** | **8,3 h** |

**Multiplicadores comparados** (horas totales para llegar a 30):

| Opción | Guerrero | Mago | Clérigo | Cazador | Nota |
|---|---|---|---|---|---|
| ×1 original | 168 | 264 | 285 | 188 | inviable |
| ×10 global | 17 | 26 | 29 | 19 | niveles 1–14 en unos 15 min: P1–P3 sobran |
| ×20 global | 8 | 13 | 14 | 9 | igual, más marcado |
| Por tramo (aprobada) | 7,4 | 11,1 | 12,1 | 8,3 | cada piso se usa |

- Hay saltos en los cambios de tramo (por ejemplo, el nivel 14 tarda 15 min y el 15, 8 min). Un multiplicador por nivel (tabla de 30 valores) lo suaviza; queda para cuando Lucas elija.
- El mago y el clérigo son más lentos solos: es el balance original (el golpe del guerrero crece 3 por nivel). En grupo se compensa.

## 7. Jefes (tiempo para matarlo)
| Jefe | Nivel de referencia | Guerrero solo | Grupo de 4 (G+M+Cl+Cz) | Golpe máx. tras armadura / vida del guerrero |
|---|---|---|---|---|
| Wolfang | 4 | 20 s | 5 s | 21 / 50 |
| Humano No-Muerto | 10 | 43 s | 11 s | 35 / 110 |
| Guardián del Mausoleo | 15 | 117 s (39 PV/s: no se aguanta) | 36 s | 125 / 160 |
| Momia | 20 | 107 s (33 PV/s) | 30 s | 120 / 210 |
| Mutante Arácnido | 24 | 86 s (38 PV/s) | 29 s | 190 / 250 |
| Medusa | 27 | 80 s (40 PV/s + Descarga) | 28 s | 124 / 280 |
| **Vytaiz (final)** | 30 | 265 s (60 PV/s) | **86 s** | 144 / 310 |

Desde el P3, los jefes son para grupo. Vytaiz pide 4–5 jugadores de nivel 28–30.

## 8. Drops y economía
- **En el AO20, los NPC tiran casi solo oro.** `GlobalDropTable.dat` tiene `DROPCOUNT=0` y el equipo se compra.
- Drops originales notables:
  - P1: Piel de Lobo (Lobo), 3 pieles (Wolfang).
  - P3: Anillo de Oro (Guardián).
  - P6: 1/20 Sombrero de Aprendiz (Mago Malvado), 1/20 Vara de Fresno (Orco Brujo), 1/100 Pendiente del Experto (Medusa).
  - P7: 1/100–1/125 pergamino de Tormenta de Fuego (dragones), 1/300 Insignia (Vytaiz).
- **Drop útil para cualquier clase = oro para el equipo del tramo siguiente.** Precios originales:
  - Hacha de Bárbaro 7.500 · Hacha Dos Filos 13.200.
  - Arco Compuesto 6.000 · Arco de Roble 13.000.
  - Bastón Nudoso 7.500 · Báculo Engarzado 12.000.
  - Tormenta de Fuego 50.000 · Descarga 600.000.
- **Oro por hora contra gasto** (nivel medio del piso, pociones cuando se pelea sin pausa; el cazador también paga flechas):

| Piso | Guerrero | Mago | Clérigo | Cazador |
|---|---|---|---|---|
| P1 (nv 2) | +3.152 / −1.934 | +3.166 / −986 | +2.735 / −1.465 | +2.538 / −11.092 |
| P2 (nv 7) | +7.363 / −5.585 | +6.378 / −3.333 | +4.642 / −2.262 | +6.482 / −32.460 |
| P3 (nv 12) | +14.253 / −22.658 | +11.292 / −16.635 | +9.294 / −15.314 | +12.187 / −47.655 |
| P4 (nv 17) | +21.410 / −16.379 | +14.131 / −15.940 | +14.344 / −13.775 | +18.950 / −38.443 |
| P5 (nv 22) | +24.783 / −37.685 | +13.856 / −28.760 | +14.971 / −41.384 | +23.678 / −53.889 |
| P6 (nv 25) | +39.464 / −46.619 | +25.764 / −34.763 | baja a P5 | +34.282 / −62.919 |
| P7 (nv 28) | +23.560 / −45.899 | +13.786 / −37.317 | baja a P5 | +20.469 / −63.475 |

- **Es una tendencia, no una cifra exacta.** El daño recibido es la parte más incierta del modelo, porque supone que el NPC pega toda la pelea. Igual, desde el P3 ninguna clase se sostiene solo con pociones, y el cazador no se sostiene nunca.
- **Opciones** (todas config o diseño, sin tocar stats; decide Lucas):
  1. `OroMult` ×2 (config original del servidor).
  2. Descanso entre grupos: la vida vuelve 7,5 % cada 2 s tras 10 s sin combate, a cambio de un 20–30 % más de tiempo.
  3. Comerciante en el hub con precios originales (pociones, flechas, equipo por tramo).
  4. Cofre del jefe con pociones y flechas, marcado como **no original**.

## 9. Diferencias del juego actual con el original (pedidos)
| Sector | Qué | Efecto en la demo |
|---|---|---|
| Programación | **El ataque solo alcanza la casilla de enfrente** (`AOPlayerCombatV09`): el arco aplica el modificador de proyectiles pero no dispara a distancia. El acierto (50 + (Ataque − Evasión) × 0,4) sí está, igual que el original. | El cazador recibe todos los golpes (se estimó 60 %): más pociones o tramos más lentos. |
| Programación | **No se regenera vida** (solo energía). | Sin descanso posible: todo es con pociones. |
| Programación | Intervalo de ataque de **0,75 s** por defecto (`attackIntervalSeconds`; el original es 1,165 s). | +55 % de daño por segundo: todo cuerpo a cuerpo ~35 % más rápido que esta tabla. |
| Programación | Respawn con 0,35 s de respaldo si `respawnMax` = 0. | Definir el respawn por piso (tabla 4). |
| Programación | La EXP se da toda al morir (el original la da por golpe). | Igual solo; cambia en grupo, porque el original reparte por daño. |
| Servidor | Multiplicador de EXP por tramo y `OroMult` (si Lucas los aprueba). | Sección 6. |

**Riesgos:**
- Los tiempos son de un modelo con supuestos (skills, equipo, 4 s entre muertes). Cuando existan los pisos, validarlos con la simulación de QA y una partida real.
- El piso P6 depende de NPC con hechizos (Paralizar, Descarga): hace falta la IA mágica de NPC (tarea de Programación).
