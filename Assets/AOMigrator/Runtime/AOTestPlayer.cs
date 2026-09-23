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

            moveTime +=
                Time.deltaTime /
                Mathf.Max(
                    0.02f,
                    moveDuration /
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
                int chainedHeading =
                    AOInterfaceV0101.InputCaptured
                    ? 0
                    : ReadHeading();

                if (chainedHeading != 0 &&
                    TryStep(
                        chainedHeading,
                        true))
                {
                    return;
                }

                if (character != null)
                    character.SetWalking(false);
            }

            return;
        }

        int requested = ReadHeading();

        if (requested != 0)
            TryStep(
                requested,
                false);
    }

    bool TryStep(
        int requestedHeading,
        bool preserveWalkPhase)
    {
        heading = requestedHeading;
        if (character != null)
            character.SetHeading(heading);

        int nx = tileX;
        int ny = tileY;

        if (heading == AOGridMap.NORTH) ny--;
        else if (heading == AOGridMap.EAST) nx++;
        else if (heading == AOGridMap.SOUTH) ny++;
        else if (heading == AOGridMap.WEST) nx--;

        if (!map.CanEnter(nx, ny, heading) ||
            AOInteractionRegistry.IsBlocked(nx, ny))
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

    int ReadHeading()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null) return 0;
        if (keyboard.wKey.isPressed ||
            keyboard.upArrowKey.isPressed)
            return AOGridMap.NORTH;
        if (keyboard.dKey.isPressed ||
            keyboard.rightArrowKey.isPressed)
            return AOGridMap.EAST;
        if (keyboard.sKey.isPressed ||
            keyboard.downArrowKey.isPressed)
            return AOGridMap.SOUTH;
        if (keyboard.aKey.isPressed ||
            keyboard.leftArrowKey.isPressed)
            return AOGridMap.WEST;
        return 0;
#else
        if (Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.UpArrow))
            return AOGridMap.NORTH;
        if (Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.RightArrow))
            return AOGridMap.EAST;
        if (Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.DownArrow))
            return AOGridMap.SOUTH;
        if (Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.LeftArrow))
            return AOGridMap.WEST;
        return 0;
#endif
    }

    bool PressedInteract()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    void OnGUI()
    {
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
}
