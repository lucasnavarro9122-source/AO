---
name: aod-modulo-nuevo
description: Convenciones y trampas conocidas para escribir o modificar scripts C# de AoDuels en Unity 6 (módulos AO*Vnnn, Input System, IMGUI, UnityEngine.Object, .meta, bootstraps, StreamingAssets). Usar antes de crear un script nuevo o de hacer cambios grandes en uno existente.
---

# Módulos C# de AoDuels

## Nombre y ubicación
- `Assets/AOMigrator/Runtime/AO<Nombre>V<nnn>.cs`: el número sale de "Próxima versión libre" en `docs/claude/tablero.md` (subilo al tomarlo). Editor: `Assets/AOMigrator/Editor/`.
- Unity genera el `.meta` al importar. No copiar `.meta` de otro archivo (GUID duplicado). No mover scripts fuera de `Assets/AOMigrator` (hubo un bug con copias duplicadas en `Assets/Scenes/AOMigrator` y `Assets/Runtime`).
- Preferir módulos nuevos que se enganchan a los existentes en lugar de reescribir archivos grandes ajenos (`AOPlayerMagicV120`, `AOCharacterRenderer`, `AOSpellFXV120`).

## Trampas que ya rompieron la compilación
- **Input:** el proyecto usa el **Input System package**. Usar `Keyboard.current` / `Mouse.current` (`wasPressedThisFrame`, `position.ReadValue()`). Nunca `UnityEngine.Input.*`.
- **Object ambiguo:** escribir `UnityEngine.Object.FindFirstObjectByType<T>()` / `FindObjectsByType<T>(FindObjectsSortMode.None)`. No reemplazar `Object.` a ciegas (una vez rompió `gameObject`).
- **Strings:** nada de saltos de línea literales dentro de `"..."` (CS1010). Usar `\n` o `@"..."`.
- **IMGUI:** no abrir ni cerrar ventanas modales ni cambiar de pantalla en medio de `OnGUI`; diferir al frame siguiente (flag + `Update`). "Two modal windows" y "Invalid GUILayout state" salen de ahí.
- **Métodos faltantes:** después de escribir, buscá con grep que cada método llamado exista (pasó con `HeadingWorldOffset`, `TryUseSelectedConsumable`).
- **Cámara:** no asumir el tag `MainCamera`; usar `AOCameraFollow` / `AOActionBarV260.GameCamera`.

## Arquitectura existente
- Bootstraps automáticos al entrar en Play (`AO*BootstrapV*`, `AO*AutoBootstrap*`): el módulo nuevo se agrega así, sin pasos manuales en la escena.
- La lógica de movimiento y colisión pasa por `AOGridMap` (matriz 100×100); nunca por colliders de sprites.
- Los datos originales ya procesados están en `Resources/AOMigrator/*.json`. Los datos que se editan en caliente (tuning, overrides) van en `StreamingAssets/AOMigrator/`.
- Guardado: `AOSaveGameV140`. Si agregás estado persistente, sumalo con compatibilidad hacia atrás (campos opcionales) y pedí la revisión de QA y Servidor (el servidor también guarda en `Saves/world.json`).
- Ajustes del jugador: `AOPlayerSettingsV230` (PlayerPrefs con prefijo).

## Reglas de producto
- Sin textos superpuestos ni botones o hotkeys de debug visibles para el jugador (F4/F8/F10/F12 eran kits de prueba y no deben aparecer).
- El chat no muestra autoguardados. Sin voces de NPC. No tocar balance ni stats de clases.

## Cierre
Correr `aod-verificar` y reportar según `aod-sector`.
