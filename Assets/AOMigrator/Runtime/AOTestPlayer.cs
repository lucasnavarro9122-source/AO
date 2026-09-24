using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(SpriteRenderer))]
public class AOTestPlayer : MonoBehaviour
{
    [SerializeField] AOGridMap map;
    [SerializeField] int tileX;
    [SerializeField] int tileY;
    [SerializeField] int spawnX;
    [SerializeField] int spawnY;
    [SerializeField] float moveDuration = 0.12f;
    [SerializeField] int heading = AOGridMap.SOUTH;

    SpriteRenderer markerRenderer;
    AOCharacterRenderer character;
    AOWorldManagerV07 world;
    bool moving;
    float moveTime;
    Vector3 moveFrom;
    Vector3 moveTo;
    string interactionText = "";
    float interactionUntil;

    public int TileX => tileX;
    public int TileY => tileY;
    public int Heading => heading;
    public bool IsMoving => moving;
    public AOGridMap CurrentGrid => map;

    public void Initialize(AOGridMap grid, int x, int y)
    {
        map = grid;
        tileX = spawnX = x;
        tileY = spawnY = y;
        moving = false;
        moveTime = 0f;
        transform.position = map.TileToWorld(tileX, tileY);
        FindCharacter();
        if (character != null)
        {
            character.SetHeading(heading);
            character.SetWalking(false);
        }
        if (markerRenderer == null)
            markerRenderer = GetComponent<SpriteRenderer>();
        if (character != null && markerRenderer != null)
            markerRenderer.enabled = false;
        UpdateSorting();
    }

    public void RestoreHeading(
        int newHeading)
    {
        heading =
            Mathf.Clamp(
                newHeading,
                AOGridMap.NORTH,
                AOGridMap.WEST);

        FindCharacter();

        if (character != null)
        {
            character.SetHeading(
                heading);

            character.SetWalking(
                false);
        }

        UpdateSorting();
    }

    public void RespawnAtSpawn()
    {
        moving = false;
        moveTime = 0f;
        tileX = spawnX;
        tileY = spawnY;

        if (map != null)
            transform.position =
                map.TileToWorld(tileX, tileY);

        if (character != null)
            character.SetWalking(false);

        UpdateSorting();
    }

    void Awake()
    {
        if (GetComponent<AOActionBarV260>() == null) gameObject.AddComponent<AOActionBarV260>();
        markerRenderer = GetComponent<SpriteRenderer>();
        FindCharacter();
        world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
    }

    void Start()
    {
        if (map != null)
            transform.position = map.TileToWorld(tileX, tileY);

        FindCharacter();
        if (character != null)
        {
            character.SetHeading(heading);
            character.SetWalking(false);
            if (markerRenderer != null)
                markerRenderer.enabled = false;
        }
        UpdateSorting();
    }

    void FindCharacter()
    {
        character =
            GetComponentInChildren<AOCharacterRenderer>(true);
    }

    void Update()
    {
        if (AOOnlineClientV240.InputBlocked) return;
        if (map == null) return;

        if ((AOInterfaceV0101.InputCaptured ||
             AOCityUIV130.ModalOpen) &&
            !moving)
            return;

        if (PressedInteract())
            Interact();

        if (moving)
        {
            AOMagicEffectRuntimeV129 magicEffects =
                GetComponent<AOMagicEffectRuntimeV129>();

            float speed =
                magicEffects == null
                ? 1f
                : magicEffects.SpeedMultiplier;

            // Normaliza la velocidad: una diagonal recorre sqrt(2) tiles,
            // por lo que tarda proporcionalmente más que un paso cardinal.
            float stepDistance = Mathf.Max(1f, Vector3.Distance(moveFrom, moveTo));
            moveTime +=
                Time.deltaTime /
                Mathf.Max(
                    0.02f,
                    (moveDuration * stepDistance) /
                    Mathf.Max(
                        0.25f,
                        speed));

            float t = Mathf.Clamp01(moveTime);
            float eased = t * t * (3f - 2f * t);
            transform.position =
                Vector3.Lerp(moveFrom, moveTo, eased);
            UpdateSorting();

            if (t >= 1f)
            {
                transform.position = moveTo;
                moving = false;
                UpdateSorting();

                // Si la tecla sigue presionada, encadenamos el siguiente tile
                // sin detener la animación. Esto replica mejor el flujo de AO,
                // donde Moving continúa y Body/Weapon/Shield mantienen fase.
                Vector2Int chainedMove =
                    AOInterfaceV0101.InputCaptured
                    ? Vector2Int.zero
                    : ReadMoveVector();

                if (chainedMove != Vector2Int.zero &&
                    TryStepVector(
                        chainedMove.x,
                        chainedMove.y,
                        true))
                {
                    return;
                }

                if (character != null)
                    character.SetWalking(false);
            }

            return;
        }

        Vector2Int requested = ReadMoveVector();

        if (requested != Vector2Int.zero)
            TryStepVector(
                requested.x,
                requested.y,
                false);
    }

    public bool StepFromControls(int direction)
    {
        if (direction == AOGridMap.NORTH) return StepVectorFromControls(0, -1);
        if (direction == AOGridMap.EAST) return StepVectorFromControls(1, 0);
        if (direction == AOGridMap.SOUTH) return StepVectorFromControls(0, 1);
        if (direction == AOGridMap.WEST) return StepVectorFromControls(-1, 0);
        return false;
    }

    // Entrada común para mouse/pathfinding y controles de teclado.
    public bool StepVectorFromControls(int dx, int dy)
    {
        if (!enabled || moving || map == null || AOInterfaceV0101.InputCaptured || AOOnlineClientV240.InputBlocked) return false;
        return TryStepVector(dx, dy, false);
    }

    public void InteractFromControls() { if (enabled && !AOInterfaceV0101.InputCaptured) Interact(); }

    bool TryStepVector(
        int dx,
        int dy,
        bool preserveWalkPhase)
    {
        dx = Mathf.Clamp(dx, -1, 1);
        dy = Mathf.Clamp(dy, -1, 1);
        if (dx == 0 && dy == 0) return false;

        heading = FacingHeading(dx, dy);
        if (character != null)
            character.SetHeading(heading);

        int nx = tileX + dx;
        int ny = tileY + dy;

        bool blocked = !map.CanStep(tileX, tileY, nx, ny) ||
            AOInteractionRegistry.IsBlocked(nx, ny);

        // Una diagonal tampoco puede pasar entre dos objetos dinámicos.
        if (!blocked && dx != 0 && dy != 0)
        {
            blocked = AOInteractionRegistry.IsBlocked(tileX + dx, tileY) ||
                AOInteractionRegistry.IsBlocked(tileX, tileY + dy);
        }

        if (blocked)
        {
            if (character != null &&
                !preserveWalkPhase)
            {
                character.SetWalking(false);
            }

            return false;
        }

        moveFrom = transform.position;
        tileX = nx;
        tileY = ny;
        moveTo = map.TileToWorld(tileX, tileY);
        moveTime = 0f;
        moving = true;

        if (character != null)
            character.SetWalking(true);

        if (world == null)
            world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        AOAudioV190.PlayFootstep(
            world == null ? "" : world.CurrentTerrain,
            world == null ? "" : world.CurrentZone);

        return true;
    }

    int FacingHeading(int dx, int dy)
    {
        // Los gráficos actuales son de 4 direcciones. En diagonal mantenemos
        // la dirección previa si coincide con uno de los dos ejes; si no,
        // priorizamos norte/sur. El desplazamiento sigue siendo diagonal real.
        if (dx == 0) return dy > 0 ? AOGridMap.SOUTH : AOGridMap.NORTH;
        if (dy == 0) return dx > 0 ? AOGridMap.EAST : AOGridMap.WEST;

        if (heading == AOGridMap.EAST && dx > 0) return heading;
        if (heading == AOGridMap.WEST && dx < 0) return heading;
        if (heading == AOGridMap.SOUTH && dy > 0) return heading;
        if (heading == AOGridMap.NORTH && dy < 0) return heading;

        return dy > 0 ? AOGridMap.SOUTH : AOGridMap.NORTH;
    }

    void Interact()
    {
        int x = tileX;
        int y = tileY;

        if (heading == AOGridMap.NORTH) y--;
        else if (heading == AOGridMap.EAST) x++;
        else if (heading == AOGridMap.SOUTH) y++;
        else if (heading == AOGridMap.WEST) x--;

        AOInteractable target =
            AOInteractionRegistry.FindFirst(x, y);
        if (target == null)
        {
            AODoorV210 nearbyDoor = AODoorV210.FindForInteraction(x, y);
            if (nearbyDoor != null)
                target = nearbyDoor.GetComponent<AOInteractable>();
        }

        if (target == null)
        {
            interactionText =
                "No hay nada interactuable en la casilla " +
                x + ", " + y + ".";
        }
        else
        {
            AONPCMetadata npc =
                target as AONPCMetadata;

            if (npc != null)
            {
                AOCityNPCSystemV130 city =
                    GetComponent
                        <AOCityNPCSystemV130>();

                if (city != null &&
                    city.TryInteract(
                        npc))
                {
                    interactionText =
                        npc.DisplayName;

                    interactionUntil =
                        Time.time + 3f;

                    return;
                }
            }

            AOLootPickupV09 loot =
                target as AOLootPickupV09;

            if (loot != null)
            {
                if (AOOnlineClientV240.Requested) { AOOnlineClientV240.Pickup(loot); return; }
                AOPlayerCombatV09 combat =
                    GetComponent<AOPlayerCombatV09>();

                if (combat != null)
                {
                    if (combat.TryAddLootItem(
                            loot.ItemIndex,
                            loot.Amount,
                            loot.DisplayName))
                    {
                        interactionText =
                            "Recogiste " +
                            loot.DisplayName +
                            " x" +
                            loot.Amount +
                            ".";

                        loot.Consume();
                    }
                    else
                    {
                        interactionText =
                            "No tenés espacio para recoger " +
                            loot.DisplayName +
                            ".";
                    }

                    interactionUntil =
                        Time.time + 4f;
                    return;
                }
            }

            AODoorV210 door = target.GetComponent<AODoorV210>();
            if (door != null)
            {
                door.TryToggle(out interactionText);
                interactionUntil = Time.time + 3f;
                AOInterfaceV0101.PushMessage(interactionText);
                return;
            }

            interactionText = target.DisplayName;
            if (!string.IsNullOrWhiteSpace(target.Description))
                interactionText += "\n" + target.Description;

            Debug.Log(
                "[AO v0.7] Interaccion: " +
                target.DisplayName +
                " @ " + target.TileX + "," + target.TileY);
        }

        interactionUntil = Time.time + 4f;
    }

    void UpdateSorting()
    {
        int baseOrder =
            AORenderOrderV210.Character(-transform.position.y);

        if (markerRenderer == null)
            markerRenderer = GetComponent<SpriteRenderer>();

        if (markerRenderer != null)
            markerRenderer.sortingOrder = baseOrder;

        if (character == null)
            FindCharacter();

        if (character != null)
            character.UpdateSorting(baseOrder);
    }

    Vector2Int ReadMoveVector()
    {
        int x = 0;
        int y = 0;

        if (AOPlayerSettingsV230.Held(AOGameAction.MoveLeft)) x--;
        if (AOPlayerSettingsV230.Held(AOGameAction.MoveRight)) x++;
        if (AOPlayerSettingsV230.Held(AOGameAction.MoveUp)) y--;
        if (AOPlayerSettingsV230.Held(AOGameAction.MoveDown)) y++;

        return new Vector2Int(
            Mathf.Clamp(x, -1, 1),
            Mathf.Clamp(y, -1, 1));
    }

    bool PressedInteract()
    {
        return AOPlayerSettingsV230.Pressed(AOGameAction.Interact);
    }

#if UNITY_EDITOR // debug only: hidden in player builds
    void OnGUI()
    {
        if (AOOnlineClientV240.InputBlocked) return;
        if (AOInterfaceV0101.Active)
            return;
        string info =
            "AO v0.7 - mundo conectado\n" +
            "Mover: WASD / Flechas | E: interactuar\n" +
            "M: mapa | Q: misiones | I: inventario\n" +
            "Tile: " + tileX + ", " + tileY +
            " | Heading: " + heading;

        GUI.Box(new Rect(12, 12, 430, 104), info);

        if (!string.IsNullOrEmpty(interactionText) &&
            Time.time <= interactionUntil)
        {
            GUIStyle style =
                new GUIStyle(GUI.skin.box);
            style.wordWrap = true;
            style.alignment = TextAnchor.UpperLeft;

            GUI.Box(
                new Rect(12, 124, 470, 120),
                interactionText,
                style);
        }
    }
#endif
}
