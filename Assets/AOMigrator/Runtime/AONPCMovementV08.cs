using System;
using UnityEngine;

[DisallowMultipleComponent]
public class AONPCMovementV08 : MonoBehaviour
{
    [Serializable]
    public class WalkPoint
    {
        public int offsetX;
        public int offsetY;
        public int waitMs;
    }

    public static bool GlobalPaused { get; set; }
    public static int ActiveControllers { get; private set; }

    const int STATIC = 1;
    const int RANDOM = 2;
    const int FIXED_IN_POS = 3;
    const int SCRIPTED_WALK = 20;

    AOGridMap grid;
    public AOGridMap Grid => grid;
    AOTestPlayer player;
    AOWorldManagerV07 world;
    AONPCMetadata metadata;
    AOCharacterRenderer visual;
    AONPCCombatV09 combat;
    bool provokedByPlayer;

    int originX;
    int originY;
    int tileX;
    int tileY;
    int movement;
    bool hostile;
    int attackRange;
    int preferredRange;
    int visionRange;
    int visionX;
    int visionY;
    int moveIntervalMs;
    bool waterValid;
    bool landInvalid;
    bool lavaValid;
    WalkPoint[] walkRoute;

    int routeIndex;
    bool moving;
    Vector3 moveFrom;
    Vector3 moveTo;
    float moveT;
    float moveDuration;
    float nextThinkAt;
    int activeHeading = AOGridMap.SOUTH;

    public bool IsMoving => moving;
    public int TileX => tileX;
    public int TileY => tileY;
    public int MovementMode => movement;
    public bool Hostile => hostile;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        GlobalPaused = false;
        ActiveControllers = 0;
    }

    void OnEnable()
    {
        ActiveControllers++;
    }

    void OnDisable()
    {
        ActiveControllers =
            Mathf.Max(0, ActiveControllers - 1);
    }

    // Visión del original, por eje (15 x 13 por defecto). Sin estos datos
    // (mapas viejos) se sigue usando visionRange como radio cuadrado.
    public void SetVisionAxes(
        int x,
        int y)
    {
        visionX = Mathf.Max(0, x);
        visionY = Mathf.Max(0, y);
    }

    public void Configure(
        AOWorldManagerV07 newWorld,
        AOGridMap newGrid,
        AOTestPlayer newPlayer,
        AONPCMetadata newMetadata,
        AOCharacterRenderer newVisual,
        int startX,
        int startY,
        int movementMode,
        bool isHostile,
        int newAttackRange,
        int newPreferredRange,
        int newVisionRange,
        int newMoveIntervalMs,
        bool canWater,
        bool invalidLand,
        bool canLava,
        WalkPoint[] route)
    {
        world = newWorld;
        grid = newGrid;
        player = newPlayer;
        metadata = newMetadata;
        visual = newVisual;

        originX = startX;
        originY = startY;
        tileX = startX;
        tileY = startY;

        movement = movementMode;
        hostile = isHostile;
        attackRange = Mathf.Max(0, newAttackRange);
        preferredRange = Mathf.Max(0, newPreferredRange);
        visionRange = Mathf.Max(1, newVisionRange);
        moveIntervalMs = Mathf.Max(80, newMoveIntervalMs);
        waterValid = canWater;
        landInvalid = invalidLand;
        lavaValid = canLava;
        walkRoute = route ?? new WalkPoint[0];
        combat = GetComponent<AONPCCombatV09>();
        provokedByPlayer = false;

        routeIndex = 0;
        moving = false;
        activeHeading =
            metadata != null &&
            metadata.Heading >= AOGridMap.NORTH &&
            metadata.Heading <= AOGridMap.WEST
            ? metadata.Heading
            : AOGridMap.SOUTH;

        transform.position =
            grid.TileToWorld(tileX, tileY);

        if (visual != null)
        {
            visual.SetHeading(activeHeading);
            visual.SetWalking(false);
            UpdateSorting();
        }

        // Desfasamos el primer tick para que decenas de NPC no piensen
        // exactamente en el mismo frame.
        nextThinkAt =
            Time.time +
            UnityEngine.Random.Range(
                0.05f,
                Mathf.Max(0.08f, moveIntervalMs / 1000f));
    }

    public void ApplyOnline(int x, int y, int heading, bool alive)
    {
        tileX = x; tileY = y; activeHeading = heading;
        metadata.MoveToTile(x, y, heading);
        if (grid == null || visual == null) return;
        moveFrom = transform.position; moveTo = grid.TileToWorld(x, y);
        moving = alive && Vector3.Distance(moveFrom, moveTo) > .02f;
        moveT = 0; moveDuration = .15f;
        visual.SetHeading(heading); visual.SetWalking(moving);
        if (!moving) transform.position = moveTo;
        UpdateSorting();
    }

    void Update()
    {
        if (AOOnlineClientV240.Requested) { if (moving) UpdateMovement(); return; }
        if (grid == null ||
            metadata == null ||
            visual == null)
            return;

        if (moving)
        {
            UpdateMovement();
            return;
        }

        if (GlobalPaused)
            return;

        if (Time.time < nextThinkAt)
            return;

        Think();
    }

    void UpdateMovement()
    {
        moveT +=
            Time.deltaTime /
            Mathf.Max(0.04f, moveDuration);

        float t = Mathf.Clamp01(moveT);
        float eased =
            t * t * (3f - 2f * t);

        transform.position =
            Vector3.Lerp(
                moveFrom,
                moveTo,
                eased);

        UpdateSorting();

        if (t < 1f)
            return;

        transform.position = moveTo;
        moving = false;
        visual.SetWalking(false);
        UpdateSorting();

        if (movement == SCRIPTED_WALK &&
            walkRoute != null &&
            walkRoute.Length > 0)
        {
            WalkPoint point =
                walkRoute[
                    Mathf.Clamp(
                        routeIndex,
                        0,
                        walkRoute.Length - 1)];

            int tx =
                originX + point.offsetX;
            int ty =
                originY + point.offsetY;

            if (tileX == tx &&
                tileY == ty)
            {
                AdvanceRoute(point.waitMs);
                return;
            }
        }

        ScheduleNormalThink();
    }

    void Think()
    {
        if (movement == STATIC ||
            movement == FIXED_IN_POS ||
            movement == 0)
        {
            visual.SetWalking(false);
            ScheduleNormalThink();
            return;
        }

        if (movement == SCRIPTED_WALK)
        {
            ThinkScriptedWalk();
            return;
        }

        if (movement == RANDOM)
        {
            ThinkRandomMovement();
            return;
        }

        // Los modos defensivos, guardias, BG, invasiones, etc.
        // requieren sistemas de combate/targets que todavía no existen.
        visual.SetWalking(false);
        ScheduleNormalThink();
    }

    void ThinkScriptedWalk()
    {
        if (walkRoute == null ||
            walkRoute.Length == 0)
        {
            ScheduleNormalThink();
            return;
        }

        if (routeIndex < 0 ||
            routeIndex >= walkRoute.Length)
            routeIndex = 0;

        WalkPoint point =
            walkRoute[routeIndex];

        int targetX =
            originX + point.offsetX;
        int targetY =
            originY + point.offsetY;

        if (tileX == targetX &&
            tileY == targetY)
        {
            // Replica el caso de HacerCaminata en el que ya estaba
            // parado en el destino al entrar al tick.
            routeIndex =
                (routeIndex + 1) %
                walkRoute.Length;

            ScheduleNormalThink();
            return;
        }

        int heading =
            HeadingToward(
                tileX,
                tileY,
                targetX,
                targetY);

        if (!TryStartStep(heading))
        {
            // El servidor avanza la caminata si MoveNPCChar falla,
            // evitando que el NPC quede atascado para siempre.
            AdvanceRoute(point.waitMs);
        }
    }

    void ThinkRandomMovement()
    {
        AOPlayerCombatV09 playerCombat =
            player == null
            ? null
            : player.GetComponent
                <AOPlayerCombatV09>();

        if (playerCombat != null &&
            playerCombat.IsDead)
        {
            provokedByPlayer = false;
        }

        if ((hostile || provokedByPlayer) &&
            player != null &&
            (playerCombat == null ||
             !playerCombat.IsDead))
        {
            int dx =
                player.TileX - tileX;
            int dy =
                player.TileY - tileY;

            int distance =
                Mathf.Max(
                    Mathf.Abs(dx),
                    Mathf.Abs(dy));

            bool inVision =
                visionX > 0 && visionY > 0
                ? Mathf.Abs(dx) <= visionX &&
                  Mathf.Abs(dy) <= visionY
                : distance <= visionRange;

            if (inVision)
            {
                ThinkHostileAgainstPlayer(
                    dx,
                    dy,
                    distance);
                return;
            }
        }

        int originDistance =
            Mathf.Max(
                Mathf.Abs(tileX - originX),
                Mathf.Abs(tileY - originY));

        if (originDistance > 4)
        {
            if (!TryMoveToward(
                    originX,
                    originY))
            {
                ScheduleNormalThink();
            }
            return;
        }

        // AI_CaminarSinRumboCercaDeOrigen:
        // en el servidor intenta un movimiento aleatorio
        // aproximadamente 1 de cada 6 ticks.
        if (UnityEngine.Random.Range(1, 7) == 3)
        {
            int heading =
                UnityEngine.Random.Range(
                    AOGridMap.NORTH,
                    AOGridMap.WEST + 1);

            if (!TryStartStep(heading))
                ScheduleNormalThink();
        }
        else
        {
            FaceCurrentHeading(false);
            ScheduleNormalThink();
        }
    }

    void ThinkHostileAgainstPlayer(
        int dx,
        int dy,
        int distance)
    {
        if (combat == null)
            combat =
                GetComponent<AONPCCombatV09>();

        int effectiveAttackRange =
            Mathf.Max(1, attackRange);

        if (distance <= effectiveAttackRange &&
            combat != null)
        {
            combat.TryAttackPlayer();
        }

        int desiredDistance =
            attackRange <= 1
            ? 1
            : Mathf.Max(
                1,
                preferredRange > 0
                ? preferredRange
                : attackRange);

        if (distance > desiredDistance)
        {
            if (!TryMoveToward(
                    player.TileX,
                    player.TileY))
                ScheduleNormalThink();

            return;
        }

        if (attackRange > 1 &&
            distance < desiredDistance)
        {
            if (!TryMoveAwayFrom(
                    player.TileX,
                    player.TileY))
                ScheduleNormalThink();

            return;
        }

        int face =
            HeadingToward(
                tileX,
                tileY,
                player.TileX,
                player.TileY);

        SetHeading(face);
        visual.SetWalking(false);
        ScheduleNormalThink();
    }

    public void SetProvokedByPlayer(bool value)
    {
        provokedByPlayer = value;

        if (value)
            nextThinkAt = Time.time;
    }

    public void PauseForDeath()
    {
        moving = false;

        if (visual != null)
            visual.SetWalking(false);

        enabled = false;
    }

    public void RespawnFromSource(
        bool forceOriginalPosition)
    {
        if (forceOriginalPosition ||
            grid == null)
        {
            RespawnAtOrigin();
            return;
        }

        // El servidor CrearNPC busca una nueva posición legal del mapa
        // cuando OrigPos=0. Intentamos lo mismo con hasta 15 candidatos.
        for (int attempt = 0;
             attempt < 15;
             attempt++)
        {
            int minX =
                Mathf.Min(
                    grid.XMax,
                    grid.XMin + 2);

            int maxX =
                Mathf.Max(
                    minX,
                    grid.XMax - 2);

            int minY =
                Mathf.Min(
                    grid.YMax,
                    grid.YMin + 2);

            int maxY =
                Mathf.Max(
                    minY,
                    grid.YMax - 2);

            int x =
                UnityEngine.Random.Range(
                    minX,
                    maxX + 1);

            int y =
                UnityEngine.Random.Range(
                    minY,
                    maxY + 1);

            if (!ValidRespawnTile(
                    x,
                    y))
                continue;

            tileX = x;
            tileY = y;
            moving = false;
            moveT = 0f;

            transform.position =
                grid.TileToWorld(
                    tileX,
                    tileY);

            if (metadata != null)
            {
                metadata.MoveToTile(
                    tileX,
                    tileY,
                    activeHeading);
            }

            if (visual != null)
            {
                visual.SetHeading(
                    activeHeading);

                visual.SetWalking(
                    false);

                UpdateSorting();
            }

            nextThinkAt =
                Time.time +
                Mathf.Max(
                    0.08f,
                    moveIntervalMs /
                    1000f);

            enabled = true;
            return;
        }

        RespawnAtOrigin();
    }

    bool ValidRespawnTile(
        int x,
        int y)
    {
        if (grid == null ||
            !grid.InBounds(
                x,
                y))
            return false;

        int flags =
            grid.GetFlags(
                x,
                y);

        int trigger =
            grid.GetTrigger(
                x,
                y);

        if ((flags &
             AOGridMap.FLAG_ALL_SIDES) !=
            0)
            return false;

        if (trigger ==
                AOGridMap.TRIGGER_WORKER_ONLY ||
            trigger ==
                AOGridMap.TRIGGER_BLOCK_15)
            return false;

        bool deepWater =
            grid.IsDeepWater(
                x,
                y);

        if (landInvalid)
        {
            if (!deepWater)
                return false;
        }
        else if (!waterValid &&
                 deepWater)
        {
            return false;
        }

        bool lava =
            (flags &
             AOGridMap.FLAG_LAVA) !=
            0;

        if (lava &&
            !lavaValid)
            return false;

        return true;
    }

    public void RespawnAtOrigin()
    {
        tileX = originX;
        tileY = originY;
        moving = false;
        moveT = 0f;

        if (grid != null)
            transform.position =
                grid.TileToWorld(
                    tileX,
                    tileY);

        if (metadata != null)
        {
            metadata.MoveToTile(
                tileX,
                tileY,
                activeHeading);
        }

        if (visual != null)
        {
            visual.SetHeading(activeHeading);
            visual.SetWalking(false);
            UpdateSorting();
        }

        nextThinkAt =
            Time.time +
            Mathf.Max(
                0.08f,
                moveIntervalMs / 1000f);

        enabled = true;
    }

    bool TryMoveToward(
        int targetX,
        int targetY)
    {
        int dx =
            targetX - tileX;
        int dy =
            targetY - tileY;

        int h1;
        int h2;

        if (Mathf.Abs(dx) >=
            Mathf.Abs(dy))
        {
            h1 =
                dx >= 0
                ? AOGridMap.EAST
                : AOGridMap.WEST;

            h2 =
                dy >= 0
                ? AOGridMap.SOUTH
                : AOGridMap.NORTH;
        }
        else
        {
            h1 =
                dy >= 0
                ? AOGridMap.SOUTH
                : AOGridMap.NORTH;

            h2 =
                dx >= 0
                ? AOGridMap.EAST
                : AOGridMap.WEST;
        }

        if (TryStartStep(h1))
            return true;

        if (h2 != h1 &&
            TryStartStep(h2))
            return true;

        // Pequeño desvío lateral. No sustituye el A* del servidor,
        // pero evita muchos bloqueos simples mientras aún no hay combate.
        int[] alternatives =
            h1 == AOGridMap.NORTH ||
            h1 == AOGridMap.SOUTH
            ? new int[] {
                AOGridMap.EAST,
                AOGridMap.WEST
              }
            : new int[] {
                AOGridMap.NORTH,
                AOGridMap.SOUTH
              };

        if (UnityEngine.Random.value < 0.5f)
        {
            int t = alternatives[0];
            alternatives[0] =
                alternatives[1];
            alternatives[1] = t;
        }

        return
            TryStartStep(alternatives[0]) ||
            TryStartStep(alternatives[1]);
    }

    bool TryMoveAwayFrom(
        int targetX,
        int targetY)
    {
        int dx =
            tileX - targetX;
        int dy =
            tileY - targetY;

        int h1 =
            Mathf.Abs(dx) >=
            Mathf.Abs(dy)
            ? (dx >= 0
                ? AOGridMap.EAST
                : AOGridMap.WEST)
            : (dy >= 0
                ? AOGridMap.SOUTH
                : AOGridMap.NORTH);

        if (TryStartStep(h1))
            return true;

        int h2 =
            h1 == AOGridMap.NORTH ||
            h1 == AOGridMap.SOUTH
            ? (dx >= 0
                ? AOGridMap.EAST
                : AOGridMap.WEST)
            : (dy >= 0
                ? AOGridMap.SOUTH
                : AOGridMap.NORTH);

        return TryStartStep(h2);
    }

    bool TryStartStep(int heading)
    {
        if (heading < AOGridMap.NORTH ||
            heading > AOGridMap.WEST)
            return false;

        int nx = tileX;
        int ny = tileY;

        if (heading == AOGridMap.NORTH)
            ny--;
        else if (heading == AOGridMap.EAST)
            nx++;
        else if (heading == AOGridMap.SOUTH)
            ny++;
        else if (heading == AOGridMap.WEST)
            nx--;

        SetHeading(heading);

        if (!CanOccupy(
                nx,
                ny,
                heading))
        {
            visual.SetWalking(false);
            return false;
        }

        moveFrom =
            transform.position;

        tileX = nx;
        tileY = ny;

        // Reservamos el tile destino desde el comienzo del movimiento
        // para que dos NPC no lo seleccionen simultáneamente.
        metadata.MoveToTile(
            tileX,
            tileY,
            heading);

        moveTo =
            grid.TileToWorld(
                tileX,
                tileY);

        moveT = 0f;

        moveDuration =
            Mathf.Clamp(
                moveIntervalMs /
                1000f * 0.72f,
                0.10f,
                0.48f);

        moving = true;
        visual.SetWalking(true);
        return true;
    }

    bool CanOccupy(
        int x,
        int y,
        int heading)
    {
        if (!grid.CanEnterNPC(
                x,
                y,
                heading,
                waterValid,
                landInvalid,
                lavaValid))
            return false;

        if (world != null &&
            world.IsExitTile(x, y))
            return false;

        if (player != null &&
            player.TileX == x &&
            player.TileY == y)
            return false;

        AOInteractable occupied =
            AOInteractionRegistry.FindFirst(
                x,
                y);

        if (occupied != null &&
            occupied != metadata &&
            occupied.BlocksTile)
            return false;

        return true;
    }

    void AdvanceRoute(int waitMs)
    {
        routeIndex =
            (routeIndex + 1) %
            walkRoute.Length;

        visual.SetWalking(false);

        nextThinkAt =
            Time.time +
            Mathf.Max(
                moveIntervalMs / 1000f,
                waitMs / 1000f);
    }

    void ScheduleNormalThink()
    {
        nextThinkAt =
            Time.time +
            Mathf.Max(
                0.08f,
                moveIntervalMs / 1000f);
    }

    void SetHeading(int heading)
    {
        if (heading < AOGridMap.NORTH ||
            heading > AOGridMap.WEST)
            return;

        activeHeading = heading;

        metadata.SetHeading(heading);
        visual.SetHeading(heading);
    }

    void FaceCurrentHeading(
        bool walking)
    {
        visual.SetHeading(activeHeading);
        visual.SetWalking(walking);
    }

    void UpdateSorting()
    {
        int baseOrder =
            AORenderOrderV210.Character(-transform.position.y);

        visual.UpdateSorting(baseOrder);
    }

    static int HeadingToward(
        int x,
        int y,
        int targetX,
        int targetY)
    {
        int dx = targetX - x;
        int dy = targetY - y;

        if (Mathf.Abs(dx) >=
            Mathf.Abs(dy))
        {
            if (dx > 0)
                return AOGridMap.EAST;
            if (dx < 0)
                return AOGridMap.WEST;
        }

        if (dy > 0)
            return AOGridMap.SOUTH;
        if (dy < 0)
            return AOGridMap.NORTH;

        return AOGridMap.SOUTH;
    }
}
