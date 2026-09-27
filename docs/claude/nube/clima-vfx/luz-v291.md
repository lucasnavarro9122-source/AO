# Luz V291: sombras de personajes, sol, luna y nubes (nube, 27/09)

Pedido de Lucas: investigar la luz en juegos 2D isométricos y mejorarla.
1. Que el personaje tenga sombra según de qué lado está la luz.
2. Una luna realista: luz tenue que no toca toda la superficie y tiñe de azul "el cielo".
3. Nubes que se interpongan entre el sol o la luna y el piso.

**Estado:**
- Compila fuera de Unity: runtime y Editor.
- **Falta** compilar en Unity 6 y probar en Play.
- La hora del juego sigue fija en las 13 (no hay día y noche activo). La noche se ve desde la ventana de depuración.

Vista previa sin Unity, con los mismos números del código: `luz_v291.jpg` (`Tools/hd_remake/luz_vista_v291.py`). Tiene 4 cuadros: mañana, atardecer, noche y noche nublada.

## Lo que se investigó
- **Sombras en 2D isométrico:** la técnica habitual ([Psychic Software, "Faking Shadows and Lights in a 2D isometric game"](https://www.psychicsoftware.com/2017/faking-shadows-and-lights-in-a-2d-game/); [foro de Unity](https://discussions.unity.com/t/how-do-i-create-a-proper-shadow-for-a-isometric-character/950951)) es así:
  - por cada luz cercana, una copia oscura de la silueta del personaje, apoyada en los pies y girada para alejarse de la luz;
  - la opacidad depende de la distancia a la luz;
  - un sombreador puede además "abrir" la punta lejana. No lo hicimos: sin Unity no se puede probar un shader nuevo.
- **Luna realista:** la luna no es azul. La vemos azul por el **efecto Purkinje** ([Wikipedia](https://en.wikipedia.org/wiki/Purkinje_effect)): con poca luz, el ojo usa bastones, los rojos se apagan, los azules y verdes quedan relativamente más claros y todo se desatura. El cine lo imita con la "noche americana": subexpuesto, frío y desaturado ([Filmmakers Academy](https://www.filmmakersacademy.com/blog-cinematic-moonlight-guide/)).
- **Nubes:**
  - La sombra de nube barata y convincente es una textura de ruido que se desplaza con el viento y enmascara la luz del sol ([Mirza Beig, "Fake Cloud Shadows"](https://mirzabeig.substack.com/p/unity-tutorial-fake-cloud-shadows)).
  - Con la misma máscara se tapan también los rayos de luz ([Cyanilux, god rays](https://www.cyanilux.com/tutorials/god-rays-shader-breakdown/)).

## Qué hace
**Sombras de personajes** (`Runtime/AOCharacterShadowsV291.cs`):
- **Quiénes:** jugador, NPC, compañeros y mascotas.
- **Forma:** la silueta de cuerpo, cabeza y casco en negro, apoyada en los pies, girada al lado opuesto a la luz y estirada según el largo. Suma una mancha suave de contacto bajo los pies.
- **Qué luz manda:**
  - **Al aire libre, de día:** el sol. Sale por el este, pasa por el sur y se pone por el oeste. La sombra es larga a la mañana y a la tarde, y corta al mediodía, hacia atrás del personaje. Opacidad 55 %.
  - **De noche:** la luna, con sombra tenue (28 %).
  - **Cerca de una luz del mapa:** si ilumina más que el cielo, la sombra se aleja de la llama y se alarga con la distancia. Son los mismos faroles y antorchas del AO y pesan más cuanto más oscuro está. En dungeons son la única fuente.
- **Nubes:** una nube encima del personaje apaga su sombra de sol, y con el cielo cubierto casi no hay sombras marcadas.
- **Personajes transparentes:** fantasma o invisible dan una sombra proporcional a su transparencia.
- **Orden de dibujo:** justo debajo del personaje en su fila. Cae sobre lo que está detrás (pasto, arbustos, pared) y queda debajo de lo que está adelante.
- **No son hijas del personaje:** así no cambian el área de clic de los NPC ni las tocan invisibilidad, mímesis o muerte.
- **Calidad:** Baja, solo el jugador; Media 16, Alta 32 y Ultra 48 personajes a la vista.

**Cielo** (`Runtime/AOSkyV291.cs`); solo al aire libre, es decir, mapas que siguen la hora y no son dungeon:
- **Nubes:** un campo de ruido anclado al mundo que se mueve con el viento (`AOWindV290`).
  - De día proyectan sombras que pasan por el piso, los techos y los personajes, multiplicando: el negro sigue negro.
  - Cobertura: con cielo despejado hay nubes sueltas; con lluvia, nieve o niebla el cielo se cubre de a poco y la luz se vuelve pareja.
- **Noche con luna:**
  - Tinte frío que baja los rojos (Purkinje) y es más azul arriba de la pantalla, lejos, como cielo, que abajo.
  - La luz de luna es tenue y solo toca el piso donde se abren las nubes: manchas frías que se mueven con ellas.
  - En Luz Mejorada, que ya trae su ambiente azul de noche, el tinte va a la mitad.
- **Rayos entre nubes** (Alta y Ultra): 3 haces suaves en los claros, cálidos de día y fríos de noche, más visibles con aire húmedo (lluvia o niebla).

**Ventana de depuración** (**AO Migrator > Clima (depuración)**), sección "Cielo y sombras":
- botones de hora (7 h, 13 h, 17:30, 23 h) y un control con "Aplicar";
- control de nubes;
- contadores de nubes, luna y sombras.

## Archivos
- **Nuevos:** `AOSkyV291`, `AOCharacterShadowsV291` y `AOWorldManagerSkyV291` (parcial de solo lectura: `CurrentBaseLight`, `CurrentMapOutdoor`).
- **Cambiados:** `AOEffectsQualityV290` (niveles `CloudShadows`, `SkyRays`, `CharacterShadows`) y la ventana `AOWeatherDebugV290`.
- **No se tocó:** `AOMapLighting`, `AOLighting2DV283`, `AOCharacterRenderer` ni `AOWorldManagerV07`.
- `AOSkyV291` se crea solo al arrancar (`AfterSceneLoad`), con las sombras en el mismo objeto.

## Costo (estimado, sin medir)
- **Sombras:** por personaje visible, 3 sprites de silueta y 1 de contacto, sin objetos nuevos por cuadro. Busca personajes cada 1,5 s (`FindObjectsByType`: pocos KB).
- **Cielo:**
  - 1 quad de tinte de noche;
  - unas 40 manchas de nube (solo las visibles) y otras 40 de luna (solo de noche);
  - 3 rayos.

  Sobredibujado de 1 a 3 pantallas en las zonas con nubes.

## Riesgo: MEDIO
- Suma dibujo encima de todo el mapa al aire libre y un objeto nuevo que arranca solo.
- No toca datos, protocolo, guardados ni gameplay. El clic en NPC queda igual.
- **Coordinar con CEREBRO y Arte:** tienen trabajo de luces y día/noche sin subir (`pc/luces-tormenta`). Esto lee la hora del mundo, así que si activan el día/noche se mueve solo. Si su luz ya hace algo parecido, hay que elegir una de las dos.

## Cómo probarlo (PC, con candado de Unity y respaldo)
1. Compilar en Unity y revisar el Editor.log.
2. Play en Ullathorpe y abrir **AO Migrator > Clima (depuración)**.
3. Hora 7, 13, 17:30 y 23: mirar hacia dónde caen las sombras del jugador y los NPC.
4. De noche, caminar cerca de un farol: la sombra se aleja de la llama.
5. Mover el control de nubes: 0,4 da nubes sueltas que pasan; 0,8, cielo cubierto.
6. Probar Luz Original y Mejorada, y calidad Baja y Alta.
7. Entrar a un dungeon: no hay cielo, solo sombras de antorchas.

## Lo que sigue (a decidir)
- Una sombra por cada luz cercana (hasta 2), como en la referencia, y la punta que se abre con un shader.
- Activar el día y la noche en el juego. Antes hay que arreglar el pico al re-iluminar el mapa (auditoría §6).
- Relámpagos que iluminen de golpe y proyecten sombras largas.

## Ronda 2 (27/09): sombras proporcionales y luz viva (V292), pedido de Lucas
**Sombras más proporcionales y al estilo del juego:**
- Largo entre 0,5 y 1,3 veces la altura del personaje (antes llegaban a 2,2). Con faroles, entre 0,45 y 1,1.
- Un 10 % más angostas que el cuerpo.
- Opacidad: sol 45 %, luna 25 %, faroles 50 %.
- Giro en pasos de 5° para que el pixel art no tiemble. La mancha de contacto mide el ancho real del cuerpo.
- Arreglos de la revisión: ya no busca el mundo en cada cuadro, y la sombra no queda flotando cuando se destruye una mascota o invocación.

**Luz viva** (`Runtime/AOLivingLightV292.cs`, creado junto al cielo):
- **Faroles y antorchas del AO** con halo en el piso que respira y parpadea. Se notan más cuanto más oscuro está (noche o dungeon); en Luz Mejorada, a la mitad.
- **Luz sobre los personajes** (`AOCharacterShadowsV291`): quien está cerca de un farol se tiñe de su color y parpadea al mismo ritmo (misma semilla). El blanco puro del AO se lleva a un cálido de farol. Es una copia aditiva encima, así que no toca los colores del personaje (invisibilidad y mímesis siguen iguales).
- **Relámpagos** en tormenta (lluvia con intensidad 1,2 o más; "Tormenta" y "Lluvia fuerte" en la ventana):
  - doble destello frío cada 6–16 s;
  - los personajes al aire libre se iluminan y proyectan una sombra dura desde el rayo;
  - botón "Relámpago ahora" en la ventana.
  - **Falta el trueno:** no hay un sonido de trueno migrado. Contenido tiene que buscar su id en el AO.
- **Luciérnagas de noche** al aire libre, sin lluvia: 16 que vuelan lento y se prenden y apagan (calidad Alta y Ultra).
- **Calidad:** Baja no tiene halos, luz sobre personajes ni luciérnagas; los relámpagos van en todas.
- **Costo:** hasta 24 halos y 16 luciérnagas, 1 quad de destello (solo durante el relámpago) y 3 sprites más por personaje visible para la luz.
- **Pendiente:** no hay vista previa nueva (se ahorró cupo de nube); se ve en Play con **AO Migrator > Clima (depuración)**.

