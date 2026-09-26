# Clima V290: paquete 1 y 1b (nube, 26/09)

Aprobado por Lucas ("Dale"): paquete 1 (lluvia, nieve y niebla con más profundidad y el mismo presupuesto) y 1b (agua que escurre por los techos). Parte de `auditoria.md` §8 y §10.

**Estado:**
- Compila fuera de Unity: runtime y ventana del Editor, contra las DLL de Unity 2021.3.
- **Falta** compilar en Unity 6 y probar en Play.
- El clima **sigue dormido en el juego**: nadie manda lluvia fuera de QA. Para verlo hay que usar la ventana de depuración.

Vista previa sin Unity, con los mismos números del código: `lluvia_v290.jpg` (`Tools/hd_remake/clima_vista_v290.py`, también arma un GIF).

## Qué cambia en pantalla
| Antes | Ahora |
|---|---|
| 200 gotas iguales pegadas a la pantalla: al caminar, la lluvia camina con el jugador | La misma cantidad (200 gotas; 90 copos) en el **mundo**, repartida en 3 capas: **fondo** (chica, lenta, tenue, parallax 0,8), **medio** (normal, aterriza) y **frente** (grande, rápida, pocas, parallax 1,25) |
| Sprite de gota del AO, vertical | Trazo fino con cola, inclinado por el viento. El viento es global (`AOWindV290`), con ráfagas y turbulencia suaves (ruido Perlin, sin saltos) |
| La gota desaparece al salir por abajo | Las gotas del medio **salpican** donde caen (anillo chato, 0,28 s) |
| Techos: nada | **Agua en los techos:** la gota que cae en un techo forma un hilo que escurre por la pendiente. A dos aguas se aleja de la cumbrera y baja; al frente, baja derecho. Al llegar al alero cae unas 2 casillas y salpica en el piso |
| Llueve adentro de la casa cuando el techo se vuelve transparente | Bajo el techo del jugador no hay gotas, salpicaduras ni agua; la lluvia se ve más tenue y el sonido baja al 45 % |
| Niebla: 2 capas pegadas a la pantalla | Las mismas 2 capas del AO (mismo dibujo, alfa y sentido), ancladas al mundo y empujadas por el viento, más **niebla de suelo**: manchas suaves con bancos y claros, sobre el piso y debajo de objetos, personajes y techos |
| — | **Tinte de día de lluvia:** multiplica (más frío y apagado, fuerza 0,2). El negro del vacío **sigue negro** |
| Cambios de golpe | Transiciones de 4 s al empezar, parar o cambiar de tipo. `SetState` (carga de mapa y QA) sigue siendo instantáneo |
| El sonido de lluvia sonaba también con nieve | El loop 194 suena solo con lluvia y su volumen sigue a la intensidad |

## Archivos
Todos nuevos, salvo `AOMapWeather.cs`. No se tocaron archivos de otros sectores: `AOWorldManagerV07` y `AOAudioV190` se extienden con clases parciales nuevas.

| Archivo | Qué hace |
|---|---|
| `Runtime/AOMapWeather.cs` (reescrito) | Mismo contrato público: `Configure`, `SetState`, `SetTarget`, `*Allowed`, `PrecipitationVisible`, `FogVisible`. Corre en `LateUpdate` después de la cámara (`DefaultExecutionOrder(1000)`) |
| `Runtime/AOMapWeatherRoofsV290.cs` | Agua de techos; lee `Resources/AOMigrator/WorldV07/roof_flow.json` |
| `Runtime/AOWindV290.cs` | Viento global: `Angle`, `Strength`, `Gust`, `FallDirection`, `Drift`. Sirve para lluvia, nieve, niebla y, más adelante, humo y polvo |
| `Runtime/AOEffectsQualityV290.cs` | `AOPlayerSettingsV230.EffectsQuality` (Low, Medium, High, Ultra; por defecto High = cantidad del AO) y los multiplicadores |
| `Runtime/AOWorldManagerWeatherV290.cs` | `PlayerRoofTrigger`, `PlayerUnderRoof`, `TriggerAtWorld` (solo lectura) |
| `Runtime/AOAudioWeatherV290.cs` | `AOAudioV190.SetWeatherLevel` (volumen de la lluvia) |
| `Editor/AOWeatherDebugV290.cs` | Ventana **AO Migrator > Clima (depuración)** |
| `Resources/AOMigrator/WorldV07/AOWeatherMultiply.shader` | Tinte que multiplica. Si falta o no está soportado, el tinte cae a negro con alfa: oscurece y el negro sigue negro |
| `Tools/roof_flow.py` → `roof_flow.json` | Pendiente de los techos desde los sprites de la capa 4. Ullathorpe: 20 techos (19 a dos aguas, 1 al frente), 8 KB. Otros mapas: `python Tools/roof_flow.py 1 34 59` o `--todos` |

## Calidad (Low, Medium, High, Ultra)
- **Cantidad de precipitación** sobre la del AO: 0,4 / 0,7 / 1 / 1,3.
- **Capas:** Low solo la del medio; Medium, fondo y medio; High y Ultra, las 3.
- **Salpicaduras** (pool): 0 / 14 / 26 / 36.
- **Agua en techos** (pool): 0 / 12 / 24 / 32.
- **Niebla:** Low, 1 capa del AO; Medium en adelante, 2 capas más la de suelo.
- La opción en el menú la agrega Interfaz (pedido en el tablero). Hasta entonces queda en High.

## Costo (estimado, sin medir)
- **High con lluvia:** hasta 200 sprites de gotas, 26 salpicaduras y 24 hilos de agua, todos con el mismo material aditivo y sin PropertyBlock, así que se agrupan. Suma un quad de tinte.
- **Niebla:** las 2 capas del AO (como antes). La de suelo suma unas 30 manchas; se dibujan solo las visibles y el sobredibujado es de ~1–2 pantallas.
- **Sin memoria en régimen:** sin `Instantiate`/`Destroy`, `new`, `Find` ni cadenas por cuadro. Todo se crea la primera vez y se reusa. Con el clima apagado no recorre nada.
- **Texturas propias:** 4 texturas chicas generadas por código una sola vez: trazo 8×64, anillo 32×16, mancha 64×64 y un píxel blanco.

## Riesgo: BAJO–MEDIO
- **Bajo:** el clima no corre en el juego hasta que alguien lo active. El contrato público no cambió. No toca datos de mapas, protocolo, guardados ni gameplay.
- **Medio:** reescribe un sistema compartido y agrega un shader. Hay que compilar en Unity y mirar el Editor.log.

## Cómo probarlo (PC, con candado de Unity y respaldo)
1. Compilar en Unity y revisar el Editor.log (`aod-verificar`).
2. Play en Ullathorpe. Menú **AO Migrator > Clima (depuración)**.
3. Probar Lluvia, Lluvia fuerte y Tormenta:
   - la lluvia queda en el mundo al caminar;
   - salpicaduras en el piso;
   - hilos de agua que bajan por los techos y gotean del alero;
   - al entrar a una casa, adentro no llueve.
4. Probar Niebla, Nieve y Ventisca. En Ullathorpe se habilitan solas: la ventana ignora los permisos del mapa para probar.
5. Cambiar la calidad en la ventana: no toca las preferencias del jugador. Mirar FPS y ms.
6. `test_modules_unity.py` y el perfil de carga (`AOMapLoadProfile`) deben seguir pasando: `PrecipitationVisible` se mantiene verdadero a los 2 cuadros de `SetState`.

## Lo que sigue (necesita decisión de Lucas)
- **Perfiles de clima y tormenta completa:** nubes, oscurecer, viento, lluvia, relámpagos y trueno con demora según distancia. Incluye relámpagos que iluminan con un flash de luz global corto.
- **Activar el clima en el juego:** RainToggle desde el servidor o clima local por hora. Antes hay que arreglar el pico al re-iluminar el mapa al cambiar la hora (`auditoria.md` §6).
- **Arreglos de rendimiento chicos y seguros:**
  - tope de FPS;
  - sacar el escaneo de `AOTerrainHDStyleManager`.
