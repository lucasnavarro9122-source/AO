# Demo AO BATTLESERVER: decisiones de Lucas (consolidado por Cerebro)

Estado: **APROBADAS por Lucas el 24/09/2026: las 17 recomendaciones, tal cual.** Fase 2 abierta.

| # | Tema | Recomendación | Fuente |
|---|---|---|---|
| 1 | Velocidad de subida: con la EXP original (×1), llegar a nivel 30 lleva 160–330 h | Multiplicador de EXP **por tramo** (×1 · ×2 · ×7 · ×17 · ×30 · ×44) → 7–13 h en total, 2–60 min por nivel | progresion.md |
| 2 | El oro no alcanza para pociones desde el piso 3 | `OroMult` ×2 (config original del servidor) | progresion.md |
| 3 | Personajes de la demo | Separados (`AO_BattleDemo/`): la demo no toca las partidas reales | arquitectura.md, ui.md |
| 4 | Apuesta mínima | 1.000 (el original pide 10.000), y 0 para retos amistosos | retos-reglas.md |
| 5 | ¿Los espectadores apuestan? | No, en la primera versión | ui.md |
| 6 | Momento de retener la apuesta | Al aceptar | red.md |
| 7 | Desconexión en un reto | 30 s de gracia; después, descalificación | red.md |
| 8 | Semilla del generador | Una por reto | red.md |
| 9 | Impuesto del 10 % en empate | Sí (como el original; cada uno recupera el 90 %) | red.md, retos-reglas.md |
| 10 | Tiempo agotado / doble muerte | Por puntaje (regla de `FinalizarReto`) / ronda nula (no original) | retos-reglas.md |
| 11 | Oro del servidor en todo el juego (protocolo 3) | Sí: hoy el cliente puede duplicar oro | red.md |
| 12 | Límite de conexiones (hoy 11) | Mantener 11 salvo que haya más amigos | red.md |
| 13 | Tema del ring | Fijo por ring | arquitectura.md |
| 14 | Borde del ring | Cuerdas y postes (no tapa la vista) | arte.md |
| 15 | Cantidad de arenas | 4 rings alrededor de una plaza | arte.md, arquitectura.md |
| 16 | Salas del dungeon | Copiadas de los mapas originales | arte.md |
| 17 | Diferencias del juego con el AO original que afectan el farmeo (el arco no dispara a distancia, no se regenera vida, el ataque va a 0,75 s en vez de 1,165 s, el respawn por defecto de los NPC es de 0,35 s) | Corregirlas para que sea fiel (Programación) | progresion.md §9 |
