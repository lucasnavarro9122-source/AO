---
name: aod-sector
description: Protocolo de trabajo por sectores de AoDuels (Programación, Arte y Animación, Servidor, Contenido, Interfaz, QA y Cerebro). Usar al iniciar cualquier chat de un sector, antes de editar un archivo que quizás pertenezca a otro sector, al tomar o liberar Unity, al necesitar un número de versión para un módulo nuevo, y al terminar una tarea para reportar a Cerebro.
---

# Protocolo de sector

## Al arrancar
1. Identificá tu sector por el título del chat ("AO BATTLESERVER: <Sector>"). Si es "AO BATTLESERVER: CEREBRO", sos el coordinador.
2. Leé `docs/claude/sectores.md` (dueños de archivos y reglas) y tu sección de `docs/claude/tablero.md`.
3. Si hace falta el historial, está en `docs/claude/historial.md`.

## Antes de editar
- Buscá el archivo en el mapa de `sectores.md`. Si es de otro sector:
  - Anotá una línea en `tablero.md` → "Pedidos entre sectores": `dd/mm · Origen → Dueño · archivo · qué hace falta`.
  - Mandá el pedido con SendMessage al chat dueño ("AO BATTLESERVER: <Sector>") o a "AO BATTLESERVER: CEREBRO". Usá ListAgents para encontrar el nombre exacto.
  - No lo edites vos. Excepción: un error de compilación que frena a todos; lo arreglás mínimo y avisás al toque.
- Módulo nuevo: tomá "Próxima versión libre" del tablero, subila en 1 en el mismo momento y seguí `aod-modulo-nuevo`.

## Unity (un solo editor para todos)
- Antes de Play, de pruebas en Unity o de un build: si "Unity en uso por" dice `libre`, cambialo a `<Sector> · hh:mm · motivo`. Si no, esperá o avisá a Cerebro.
- Al terminar, volvé a dejar `libre`. Nunca entres en Play si Lucas está jugando: preguntá.
- Respaldo antes de probar: skill `aod-respaldo`.

## Al terminar una tarea
1. Verificá con `aod-verificar` (como mínimo la compilación auxiliar).
2. Actualizá tu sección del tablero: marcá `[x]` y agregá lo nuevo pendiente. Registrá en "Hecho" una línea con la fecha.
3. Mandá el reporte a "AO BATTLESERVER: CEREBRO" en 5 líneas como máximo:
   - archivos tocados;
   - qué se verificó y cómo;
   - qué falta o qué riesgos hay;
   - pedidos a otros sectores.
4. No commitees. Cerebro integra y commitea solo cuando Lucas lo pide.

## Cerebro (coordinador)
- Mantiene las prioridades del tablero, reparte tareas por SendMessage, resuelve los conflictos de archivos compartidos (escena, ProjectSettings, Packages, CLAUDE.md) y revisa los diffs con el subagente `revisor-diff` antes de commitear.
- Actualiza "Estado" en `CLAUDE.md` cuando cambia algo relevante.
- **Contexto de una sola vez:** cuando Lucas pasa conversaciones, documentos largos o instrucciones de organización, Cerebro los lee una vez y los resume en `docs/claude/`.
  - Los sectores nunca reciben ese material crudo ni se bifurcan del chat que lo cargó: arrancan desde los resúmenes.
  - Al evaluar consumo, el costo de ese arranque no cuenta como trabajo real.
