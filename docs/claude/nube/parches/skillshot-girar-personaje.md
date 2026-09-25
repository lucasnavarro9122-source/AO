# Parche: el personaje gira hacia donde tira el skill shot

Dueño: **Programación**. Preparado en la nube el 25/09 contra `384fe18`. Decisión de Lucas (25/09): 4 direcciones como el original, y al tirar un skill shot el personaje **gira de verdad**.

## Qué cambia
- `AOTestPlayer.FaceHeading(int)` (nuevo): cambia el heading de juego y el visual **sin cortar la animación de caminar**. No usa `RestoreHeading`, que hace `SetWalking(false)`.
- `AOPlayerMagicV120.AimHeading(Vector2, int)` (nuevo): de la dirección apuntada saca una de las 4 direcciones (la del eje dominante).
  - Si el apuntado está a ≤ 55° de la dirección actual, la conserva: son 10° de margen sobre los 45°, para que no parpadee al tirar en diagonal.
- `TryLaunchSkillShotMouse`: gira después de cobrar maná y enfriamiento, y antes de la animación de casteo. Así el casteo ya se ve hacia el nuevo lado.

## Efectos
- **Gameplay:** el próximo golpe cuerpo a cuerpo e interactuar van hacia el nuevo lado, y se guarda en la partida.
- **Online:** los compañeros lo ven solo, porque el heading ya viaja en cada `position`. El servidor lo recorta a 1–4. No cambia el protocolo.
- **Los hechizos con objetivo (clic en casilla) no giran,** como en el original.

## Verificación
- En la nube: el parche aplica sobre `384fe18` y los 2 archivos quedan sin errores de sintaxis (Roslyn, C# 9).
- Sin Unity no pude compilar contra UnityEngine. Usa solo APIs que ya usa el proyecto (`Mathf.DeltaAngle`, `Mathf.Atan2`, constantes de `AOGridMap`).

## Aplicar y probar en la PC
```powershell
git apply --check docs/claude/nube/parches/skillshot-girar-personaje.patch
git apply docs/claude/nube/parches/skillshot-girar-personaje.patch
```
Si la PC cambió esas funciones a la noche, aplicarlo a mano: son 2 métodos nuevos y 1 línea en `TryLaunchSkillShotMouse`.

Prueba en Unity (antes, `aod-respaldo`):
1. Mirando al S, tirar Dardo Mágico hacia la derecha: gira al E y el proyectil sale al E.
2. Tirar a 40° del E (casi diagonal): sigue mirando al E. A 60°: gira al N.
3. Después de girar al E, atacar cuerpo a cuerpo: pega al tile del E.
4. Caminando, tirar hacia un costado: gira y sigue caminando, sin trabarse la animación.
5. Online con 2 clientes: el compañero ve el giro.

Sugerencia para QA: sumar los casos 1 a 3 a `AOModulesQA270` (V267).
