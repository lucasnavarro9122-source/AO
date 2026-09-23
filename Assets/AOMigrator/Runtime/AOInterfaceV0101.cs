using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public partial class AOInterfaceV0101 : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static bool InputCaptured { get; private set; }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Active = false;
        InputCaptured = false;
    }

    const float REF_W = 1024f;
    const float REF_H = 768f;

    enum UpperTab
    {
        Inventory,
        Spells
    }

    enum LowerTab
    {
        Stats,
        Info
    }

    [Header("Referencias")]
    [SerializeField] AOTestPlayer player;
    [SerializeField] AOPlayerCombatV09 combat;
    [SerializeField] AOInventoryV10 inventory;
    [SerializeField] AOWorldManagerV07 world;
    [SerializeField] Camera gameCamera;
    [SerializeField] AOPlayerRPGV11 rpgV11;
    [SerializeField] AOPlayerMagicV120 magicV120;
    [SerializeField] AOPlayerMagicStatusV120 magicStatusV120;
    [SerializeField] AODeathRespawnV160 deathV160;

    [Header("Fallback si v0.11 no está activo")]
    [SerializeField] int mana = 0;
    [SerializeField] int maxMana = 0;
    [SerializeField] int stamina = 100;
    [SerializeField] int maxStamina = 100;
    [SerializeField] int hunger = 100;
    [SerializeField] int maxHunger = 100;
    [SerializeField] int thirst = 100;
    [SerializeField] int maxThirst = 100;
    [SerializeField] int localExpPerBar = 1000;

    Texture2D hudFrame;
    Texture2D panelInventory;
    Texture2D panelSpells;
    Texture2D panelStats;
    Texture2D panelInfo;

    Texture2D barHp;
    Texture2D barMana;
    Texture2D barStamina;
    Texture2D barHunger;
    Texture2D barThirst;
    Texture2D barExp;

    Texture2D btnInvDefault;
    Texture2D btnInvOver;
    Texture2D btnInvOff;
    Texture2D btnSpellDefault;
    Texture2D btnSpellOver;
    Texture2D btnSpellOff;

    UpperTab upperTab =
        UpperTab.Inventory;

    LowerTab lowerTab =
        LowerTab.Stats;

    readonly List<string> chat =
        new List<string>();

    readonly Dictionary<int, Texture2D>
        minimapCache =
            new Dictionary<int, Texture2D>();

    GUIStyle chatStyle;
    GUIStyle chatInputStyle;
    GUIStyle speechStyle;
    GUIStyle speechOutlineStyle;
    GUIStyle tinyWhite;
    GUIStyle centeredWhite;
    GUIStyle slotCountStyle;
    GUIStyle invisibleButton;
    GUIStyle tooltipStyle;
    GUIStyle mapTitleStyle;

    Rect frameRect;
    float scale = 1f;
    Vector2 spellScroll;

    bool chatEditing;
    string chatInput = "";
    string speechText = "";
    float speechUntil;
    AOCharacterRenderer speechVisual;

    bool largeMap;
    bool showRPGPanel;

    int dragSource = -1;
    bool dragging;
    Vector2 dragStart;

    public void Configure(
        AOTestPlayer newPlayer,
        AOPlayerCombatV09 newCombat,
        AOInventoryV10 newInventory,
        AOWorldManagerV07 newWorld,
        Camera newCamera)
    {
        player = newPlayer;
        combat = newCombat;
        inventory = newInventory;
        world = newWorld;
        gameCamera = newCamera;
    }

    void Awake()
    {
        Active = true;
        InputCaptured = false;
        FindReferences();
        LoadTextures();
    }

    void OnEnable()
    {
        Active = true;
        InputCaptured = false;

    }

    void OnDisable()
    {
        Active = false;
        InputCaptured = false;

        if (gameCamera != null)
        {
            gameCamera.rect =
                new Rect(
                    0f,
                    0f,
                    1f,
                    1f);
        }
    }

    void OnDestroy()
    {
        if (Active)
            Active = false;

        InputCaptured = false;
    }

    void Update()
    {
        FindReferences();
        UpdateTopActions();

        if (AOMainMenuV140.ModalOpen || AOCharacterCreationV170.ModalOpen)
            topDialog = TopDialog.None;

        if (!chatEditing &&
            !AOCityUIV130.ModalOpen &&
            !AOQuestUIV150.ModalOpen &&
            !AOCharacterCreationV170.ModalOpen &&
            !AOMainMenuV140.ModalOpen &&
            topDialog == TopDialog.None)
        {
            if (PressedInventory())
                upperTab =
                    UpperTab.Inventory;

            if (PressedMap())
                largeMap = !largeMap;

            if (PressedRPG())
                showRPGPanel =
                    !showRPGPanel;
        }

        InputCaptured =
            chatEditing ||
            AOCityUIV130.ModalOpen ||
            AOQuestUIV150.ModalOpen ||
            AOCharacterCreationV170.ModalOpen ||
            AOMainMenuV140.ModalOpen ||
            topDialog != TopDialog.None;

        UpdateGeometry();
        UpdateCameraViewport();
    }

    void FindReferences()
    {
        if (player == null)
        {
            player =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOTestPlayer>();
        }

        if (combat == null &&
            player != null)
        {
            combat =
                player.GetComponent
                    <AOPlayerCombatV09>();
        }

        if (inventory == null &&
            player != null)
        {
            inventory =
                player.GetComponent
                    <AOInventoryV10>();
        }

        if (rpgV11 == null &&
            player != null)
        {
            rpgV11 =
                player.GetComponent
                    <AOPlayerRPGV11>();
        }

        if (magicV120 == null &&
            player != null)
        {
            magicV120 =
                player.GetComponent
                    <AOPlayerMagicV120>();
        }

        if (magicStatusV120 == null &&
            player != null)
        {
            magicStatusV120 =
                player.GetComponent
                    <AOPlayerMagicStatusV120>();
        }

        if (deathV160 == null &&
            player != null)
        {
            deathV160 =
                player.GetComponent
                    <AODeathRespawnV160>();
        }

        if (world == null)
        {
            world =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOWorldManagerV07>();
        }

        if (gameCamera == null)
        {
            AOCameraFollow follow =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOCameraFollow>();

            if (follow != null)
            {
                gameCamera =
                    follow.GetComponent<Camera>();
            }

            if (gameCamera == null)
                gameCamera = Camera.main;
        }
    }

    void LoadTextures()
    {
        hudFrame = LoadUI("hud_frame");

        panelInventory =
            LoadUI("panel_inventory");

        panelSpells =
            LoadUI("panel_spells");

        panelStats =
            LoadUI("panel_stats");

        panelInfo =
            LoadUI("panel_info");

        barHp = LoadUI("bar_hp");
        barMana = LoadUI("bar_mana");

        barStamina =
            LoadUI("bar_stamina");

        barHunger =
            LoadUI("bar_hunger");

        barThirst =
            LoadUI("bar_thirst");

        barExp = LoadUI("bar_exp");

        btnInvDefault =
            LoadUI(
                "btn_inventory_default");

        btnInvOver =
            LoadUI(
                "btn_inventory_over");

        btnInvOff =
            LoadUI(
                "btn_inventory_off");

        btnSpellDefault =
            LoadUI(
                "btn_spells_default");

        btnSpellOver =
            LoadUI(
                "btn_spells_over");

        btnSpellOff =
            LoadUI(
                "btn_spells_off");
    }

    static Texture2D LoadUI(
        string name)
    {
        Texture2D t =
            Resources.Load<Texture2D>(
                "AOMigrator/InterfaceV0101/" +
                name);

        if (t == null)
        {
            Debug.LogError(
                "[AO UI v0.10.3] Falta " +
                name);
        }

        return t;
    }

    Texture2D CurrentMinimap()
    {
        if (world == null)
            return null;

        int map =
            world.CurrentMapNumber;

        if (map <= 0)
            return null;

        if (minimapCache.TryGetValue(
                map,
                out Texture2D cached))
        {
            return cached;
        }

        Texture2D texture =
            Resources.Load<Texture2D>(
                "AOMigrator/MinimapsV0103/map_" +
                map);

        if (texture != null)
            minimapCache[map] = texture;

        return texture;
    }

    void BuildStyles()
    {
        invisibleButton =
            new GUIStyle(
                GUIStyle.none);

        chatStyle =
            new GUIStyle(
                GUI.skin.label);

        chatStyle.fontSize = 12;
        chatStyle.normal.textColor =
            new Color(
                0.92f,
                0.92f,
                0.92f);

        chatStyle.wordWrap = false;

        chatStyle.alignment =
            TextAnchor.LowerLeft;

        speechStyle = new GUIStyle(chatStyle);
        speechStyle.fontSize = 13;
        speechStyle.fontStyle = FontStyle.Bold;
        speechStyle.wordWrap = true;
        speechStyle.alignment = TextAnchor.MiddleCenter;
        speechStyle.normal.textColor = new Color(1f, 0.97f, 0.77f);

        speechOutlineStyle = new GUIStyle(speechStyle);
        speechOutlineStyle.normal.textColor =
            new Color(0.04f, 0.03f, 0.02f, 0.95f);

        chatInputStyle =
            new GUIStyle(
                GUI.skin.textField);

        chatInputStyle.fontSize = 12;
        chatInputStyle.normal.textColor =
            Color.white;

        chatInputStyle.focused.textColor =
            Color.white;

        tinyWhite =
            new GUIStyle(
                GUI.skin.label);

        tinyWhite.fontSize = 10;

        tinyWhite.normal.textColor =
            Color.white;

        tinyWhite.wordWrap = false;

        centeredWhite =
            new GUIStyle(
                tinyWhite);

        centeredWhite.alignment =
            TextAnchor.MiddleCenter;

        slotCountStyle =
            new GUIStyle(
                tinyWhite);

        slotCountStyle.fontStyle =
            FontStyle.Bold;

        slotCountStyle.alignment =
            TextAnchor.LowerRight;

        tooltipStyle =
            new GUIStyle(
                GUI.skin.box);

        tooltipStyle.fontSize = 11;
        tooltipStyle.wordWrap = true;
        tooltipStyle.alignment =
            TextAnchor.UpperLeft;

        tooltipStyle.normal.textColor =
            Color.white;

        mapTitleStyle =
            new GUIStyle(
                GUI.skin.box);

        mapTitleStyle.fontSize = 13;
        mapTitleStyle.fontStyle =
            FontStyle.Bold;

        mapTitleStyle.alignment =
            TextAnchor.MiddleCenter;

        mapTitleStyle.normal.textColor =
            Color.white;
    }

    void UpdateGeometry()
    {
        scale =
            Mathf.Min(
                Screen.width / REF_W,
                Screen.height / REF_H);

        float width =
            REF_W * scale;

        float height =
            REF_H * scale;

        frameRect =
            new Rect(
                (Screen.width - width) *
                    0.5f,
                (Screen.height - height) *
                    0.5f,
                width,
                height);
    }

    Rect R(
        float x,
        float y,
        float w,
        float h)
    {
        return new Rect(
            frameRect.x +
                x * scale,
            frameRect.y +
                y * scale,
            w * scale,
            h * scale);
    }

    void UpdateCameraViewport()
    {
        if (gameCamera == null ||
            scale <= 0f)
            return;

        if (AOMainMenuV140.EntranceOpen)
        {
            gameCamera.pixelRect = new Rect(
                0f, 0f, Screen.width, Screen.height);
            return;
        }

        float px =
            frameRect.x +
            8f * scale;

        float top =
            frameRect.y +
            152f * scale;

        float pw =
            736f * scale;

        float ph =
            608f * scale;

        float py =
            Screen.height -
            (top + ph);

        gameCamera.pixelRect =
            new Rect(
                px,
                py,
                pw,
                ph);
    }

    void OnGUI()
    {
        if (AOMainMenuV140.ModalOpen || AOCharacterCreationV170.ModalOpen)
            return;

        if (hudFrame == null)
            return;

        if (chatStyle == null)
            BuildStyles();

        UpdateGeometry();
        HandleChatKeyboard();

        DrawLetterbox();

        GUI.DrawTexture(
            frameRect,
            hudFrame,
            ScaleMode.StretchToFill,
            true);

        DrawMinimap();

        DrawUpperPanel();
        DrawLowerPanel();
        DrawExperience();
        DrawChat();
        DrawSpeech();

        if (largeMap)
            DrawLargeMapOverlay();

        if (showRPGPanel)
            DrawRPGPanel();

        DrawDragGhost();
        DrawMagicStatusOverlay();
        DrawTopButtons();
        DrawTopDialog();
    }

    void DrawMagicStatusOverlay()
    {
        if (magicStatusV120 == null)
            return;

        if (magicStatusV120.IsBlind)
        {
            Color old =
                GUI.color;

            GUI.color =
                new Color(
                    0f,
                    0f,
                    0f,
                    0.82f);

            GUI.DrawTexture(
                R(8f,152f,736f,608f),
                Texture2D.whiteTexture);

            GUI.color = old;

            GUI.Label(
                R(270f,420f,220f,28f),
                "CEGUERA",
                centeredWhite);
        }

        string states =
            magicStatusV120.StatusSummary;

        if (!string.IsNullOrEmpty(states))
        {
            GUI.Box(
                R(250f,154f,250f,24f),
                states);
        }
    }

    void HandleChatKeyboard()
    {
        if (AOCityUIV130.ModalOpen || AOQuestUIV150.ModalOpen ||
            topDialog != TopDialog.None)
            return;

        Event e =
            Event.current;

        if (e == null ||
            e.type !=
                EventType.KeyDown)
            return;

        if (e.keyCode ==
                KeyCode.Return ||
            e.keyCode ==
                KeyCode.KeypadEnter)
        {
            if (!chatEditing)
            {
                chatEditing = true;
                InputCaptured = true;

                GUI.FocusControl(
                    "AO_CHAT_INPUT");

                e.Use();
                return;
            }

            SubmitChat();
            e.Use();
            return;
        }

        if (chatEditing &&
            e.keyCode ==
                KeyCode.Escape)
        {
            chatEditing = false;
            InputCaptured = false;
            chatInput = "";
            GUI.FocusControl(null);
            e.Use();
        }
    }

    void SubmitChat()
    {
        string clean =
            (chatInput ?? "")
                .Trim();

        if (!string.IsNullOrEmpty(
                clean))
        {
            if (string.Equals(
                    clean,
                    "/hogar",
                    System.StringComparison
                        .OrdinalIgnoreCase))
            {
                if (deathV160 == null)
                {
                    PushMessage(
                        "Sistema /HOGAR no disponible.");
                }
                else
                {
                    deathV160.TryGoHome(
                        out string homeResult);

                    PushMessage(
                        homeResult);
                }
            }
            else
            {
                PushMessage(
                    "Tú: " +
                    clean);
                speechText = clean.Replace('\r', ' ').Replace('\n', ' ');
                speechUntil = Time.unscaledTime + 5f +
                    0.06f * speechText.Length;
            }
        }

        chatInput = "";
        chatEditing = false;
        InputCaptured = false;
        GUI.FocusControl(null);
    }

    void DrawLetterbox()
    {
        Color old =
            GUI.color;

        GUI.color =
            Color.black;

        if (frameRect.x > 0f)
        {
            GUI.DrawTexture(
                new Rect(
                    0f,
                    0f,
                    frameRect.x,
                    Screen.height),
                Texture2D.whiteTexture);

            GUI.DrawTexture(
                new Rect(
                    frameRect.xMax,
                    0f,
                    Screen.width -
                        frameRect.xMax,
                    Screen.height),
                Texture2D.whiteTexture);
        }

        if (frameRect.y > 0f)
        {
            GUI.DrawTexture(
                new Rect(
                    frameRect.x,
                    0f,
                    frameRect.width,
                    frameRect.y),
                Texture2D.whiteTexture);

            GUI.DrawTexture(
                new Rect(
                    frameRect.x,
                    frameRect.yMax,
                    frameRect.width,
                    Screen.height -
                        frameRect.yMax),
                Texture2D.whiteTexture);
        }

        GUI.color = old;
    }

    void DrawMinimap()
    {
        Rect rect =
            R(
                638.4f,
                40f,
                100f,
                100f);

        Texture2D mini =
            CurrentMinimap();

        if (mini != null)
        {
            GUI.DrawTexture(
                rect,
                mini,
                ScaleMode.StretchToFill,
                false);
        }
        else
        {
            GUI.Box(
                rect,
                "Sin\nminimapa");
        }

        DrawMapMarker(rect);

        if (AOAudioV190.Clicked(GUI.Button(
                rect,
                GUIContent.none,
                invisibleButton)))
        {
            largeMap =
                !largeMap;
        }

        if (world != null)
        {
            GUI.Label(
                R(
                    638.4f,
                    141.5f,
                    100f,
                    14f),
                "Mapa " +
                world.CurrentMapNumber,
                centeredWhite);
        }
    }

    void DrawMapMarker(
        Rect mapRect)
    {
        if (player == null)
            return;

        float nx =
            Mathf.Clamp01(
                (player.TileX - 1f) /
                99f);

        float ny =
            Mathf.Clamp01(
                (player.TileY - 1f) /
                99f);

        float size =
            Mathf.Max(
                3f,
                4f * scale);

        Rect marker =
            new Rect(
                mapRect.x +
                nx * mapRect.width -
                size * 0.5f,
                mapRect.y +
                ny * mapRect.height -
                size * 0.5f,
                size,
                size);

        Color old =
            GUI.color;

        GUI.color =
            new Color(
                1f,
                0.18f,
                0.12f,
                1f);

        GUI.DrawTexture(
            marker,
            Texture2D.whiteTexture);

        GUI.color = old;
    }

    void DrawLargeMapOverlay()
    {
        Texture2D mini =
            CurrentMinimap();

        Rect outer =
            R(
                160f,
                190f,
                430f,
                470f);

        GUI.Box(
            outer,
            GUIContent.none);

        string title =
            world == null
            ? "Mapa"
            : "Mapa " +
              world.CurrentMapNumber +
              " — " +
              world.CurrentMapName;

        GUI.Box(
            R(
                175f,
                202f,
                400f,
                30f),
            title,
            mapTitleStyle);

        Rect mapRect =
            R(
                185f,
                245f,
                380f,
                380f);

        if (mini != null)
        {
            GUI.DrawTexture(
                mapRect,
                mini,
                ScaleMode.StretchToFill,
                false);

            DrawMapMarker(
                mapRect);
        }

        if (AOAudioV190.Clicked(GUI.Button(
                R(
                    470f,
                    635f,
                    95f,
                    24f),
                "Cerrar (M)")))
        {
            largeMap = false;
        }
    }

    void DrawUpperPanel()
    {
        Texture2D panel =
            upperTab ==
                UpperTab.Inventory
            ? panelInventory
            : panelSpells;

        if (panel != null)
        {
            GUI.DrawTexture(
                R(
                    768f,
                    160f,
                    247f,
                    325f),
                panel,
                ScaleMode.StretchToFill,
                true);
        }

        Rect invRect =
            R(
                769f,
                161f,
                122f,
                28f);

        Rect spellRect =
            R(
                893f,
                161f,
                122f,
                28f);

        bool invHover =
            invRect.Contains(
                Event.current
                    .mousePosition);

        bool spellHover =
            spellRect.Contains(
                Event.current
                    .mousePosition);

        Texture2D invButton =
            upperTab ==
                UpperTab.Inventory
            ? btnInvOff
            : (invHover
                ? btnInvOver
                : btnInvDefault);

        Texture2D spellButton =
            upperTab ==
                UpperTab.Spells
            ? btnSpellOff
            : (spellHover
                ? btnSpellOver
                : btnSpellDefault);

        if (invButton != null)
        {
            GUI.DrawTexture(
                invRect,
                invButton,
                ScaleMode.StretchToFill,
                true);
        }

        if (spellButton != null)
        {
            GUI.DrawTexture(
                spellRect,
                spellButton,
                ScaleMode.StretchToFill,
                true);
        }

        if (AOAudioV190.Clicked(GUI.Button(
                invRect,
                GUIContent.none,
                invisibleButton)))
        {
            upperTab =
                UpperTab.Inventory;
        }

        if (AOAudioV190.Clicked(GUI.Button(
                spellRect,
                GUIContent.none,
                invisibleButton)))
        {
            upperTab =
                UpperTab.Spells;
        }

        if (upperTab ==
            UpperTab.Inventory)
        {
            DrawInventorySlots();
        }
        else
        {
            DrawSpellPlaceholder();
        }
    }

    Rect InventorySlotRect(
        int index)
    {
        const float startX =
            787f;

        const float startY =
            210f;

        const float pitch =
            35f;

        const float slotSize =
            32f;

        int col =
            index % 6;

        int row =
            index / 6;

        return R(
            startX +
                col * pitch,
            startY +
                row * pitch,
            slotSize,
            slotSize);
    }

    int SlotAt(
        Vector2 mouse)
    {
        if (inventory == null)
            return -1;

        int count =
            Mathf.Min(
                AOInventoryV10
                    .BASIC_SLOTS,
                inventory.SlotCount);

        for (int i = 0;
             i < count;
             i++)
        {
            if (InventorySlotRect(i)
                .Contains(mouse))
            {
                return i;
            }
        }

        return -1;
    }

    void DrawInventorySlots()
    {
        if (inventory == null)
        {
            GUI.Label(
                R(
                    786f,
                    225f,
                    210f,
                    40f),
                "Inventario v0.10 no encontrado.",
                centeredWhite);

            return;
        }

        int count =
            Mathf.Min(
                AOInventoryV10
                    .BASIC_SLOTS,
                inventory.SlotCount);

        int hovered = -1;

        for (int i = 0;
             i < count;
             i++)
        {
            Rect slot =
                InventorySlotRect(i);

            if (slot.Contains(
                    Event.current
                        .mousePosition))
            {
                hovered = i;
            }

            DrawInventorySlot(
                i,
                slot);

            HandleInventorySlotEvent(
                i,
                slot);
        }

        AOItemDatabaseV10.ItemDef
            selected =
                inventory
                    .GetSelectedItem();

        if (selected != null)
        {
            string help =
                selected.IsParchment
                ? "Doble click: aprender hechizo"
                : selected.Equipable
                    ? "Doble click: equipar | Arrastrar: mover"
                    : (selected.Consumable
                        ? "Doble click: usar | Arrastrar: mover"
                        : "Arrastrar: mover objeto");

            GUI.Label(
                R(
                    787f,
                    449f,
                    210f,
                    18f),
                help,
                centeredWhite);
        }
        else
        {
            GUI.Label(
                R(
                    787f,
                    449f,
                    210f,
                    18f),
                "Seleccioná un objeto",
                centeredWhite);
        }

        if (hovered >= 0)
            DrawItemTooltip(
                hovered);
    }

    void DrawInventorySlot(
        int index,
        Rect slot)
    {
        Color old =
            GUI.color;

        GUI.color =
            index ==
                inventory.SelectedSlot
            ? new Color(
                0.82f,
                0.70f,
                0.23f,
                0.88f)
            : new Color(
                0.12f,
                0.13f,
                0.13f,
                0.82f);

        GUI.Box(
            slot,
            GUIContent.none);

        GUI.color = old;

        int itemIndex =
            inventory
                .GetSlotItemIndex(
                    index);

        int amount =
            inventory
                .GetSlotAmount(
                    index);

        if (itemIndex <= 0)
            return;

        Sprite icon =
            AOItemDatabaseV10.Icon(
                itemIndex);

        if (icon != null)
        {
            Rect ir =
                new Rect(
                    slot.x +
                        2f * scale,
                    slot.y +
                        2f * scale,
                    slot.width -
                        4f * scale,
                    slot.height -
                        4f * scale);

            DrawSprite(
                icon,
                ir);
        }

        if (amount > 1)
        {
            GUI.Label(
                new Rect(
                    slot.x,
                    slot.y +
                        slot.height -
                        15f * scale,
                    slot.width -
                        2f * scale,
                    14f * scale),
                amount.ToString(),
                slotCountStyle);
        }

        if (inventory
            .IsEquippedPublic(
                itemIndex))
        {
            GUI.Label(
                new Rect(
                    slot.x +
                        2f * scale,
                    slot.y +
                        1f * scale,
                    20f * scale,
                    15f * scale),
                "E",
                tinyWhite);
        }
    }

    void HandleInventorySlotEvent(
        int index,
        Rect slot)
    {
        Event e =
            Event.current;

        if (e == null)
            return;

        if (e.type ==
                EventType.MouseDown &&
            e.button == 0 &&
            slot.Contains(
                e.mousePosition))
        {
            inventory
                .SelectSlotPublic(
                    index);

            if (e.clickCount >= 2)
            {
                inventory
                    .UseOrToggleSelected();

                dragSource = -1;
                dragging = false;
            }
            else
            {
                dragSource =
                    index;

                dragStart =
                    e.mousePosition;

                dragging =
                    false;
            }

            e.Use();
            return;
        }

        if (e.type ==
                EventType.MouseDrag &&
            e.button == 0 &&
            dragSource >= 0)
        {
            if (Vector2.Distance(
                    dragStart,
                    e.mousePosition) >
                5f * scale)
            {
                dragging = true;
            }

            e.Use();
            return;
        }

        if (e.type ==
                EventType.MouseUp &&
            e.button == 0 &&
            dragSource >= 0)
        {
            if (dragging)
            {
                int target =
                    SlotAt(
                        e.mousePosition);

                if (target >= 0 &&
                    target !=
                        dragSource)
                {
                    if (inventory
                        .MoveSlotPublic(
                            dragSource,
                            target))
                    {
                        PushMessage(
                            "Objeto movido.");
                    }
                }
            }

            dragSource = -1;
            dragging = false;
            e.Use();
        }
    }

    void DrawDragGhost()
    {
        if (!dragging ||
            dragSource < 0 ||
            inventory == null)
            return;

        int id =
            inventory
                .GetSlotItemIndex(
                    dragSource);

        if (id <= 0)
            return;

        Sprite icon =
            AOItemDatabaseV10.Icon(
                id);

        if (icon == null)
            return;

        Vector2 m =
            Event.current
                .mousePosition;

        Rect r =
            new Rect(
                m.x -
                    16f * scale,
                m.y -
                    16f * scale,
                32f * scale,
                32f * scale);

        Color old =
            GUI.color;

        GUI.color =
            new Color(
                1f,
                1f,
                1f,
                0.72f);

        DrawSprite(
            icon,
            r);

        GUI.color = old;
    }

    static void DrawSprite(
        Sprite icon,
        Rect rect)
    {
        if (icon == null ||
            icon.texture == null)
            return;

        Rect tr =
            icon.textureRect;

        Rect uv =
            new Rect(
                tr.x /
                    icon.texture.width,
                tr.y /
                    icon.texture.height,
                tr.width /
                    icon.texture.width,
                tr.height /
                    icon.texture.height);

        GUI.DrawTextureWithTexCoords(
            rect,
            icon.texture,
            uv,
            true);
    }

    void DrawItemTooltip(
        int slot)
    {
        int id =
            inventory
                .GetSlotItemIndex(
                    slot);

        if (id <= 0)
            return;

        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(id);

        if (item == null)
            return;

        int amount =
            inventory
                .GetSlotAmount(
                    slot);

        string type =
            ItemTypeName(
                item.objType);

        string text =
            item.name +
            "\n" +
            type +
            " | ID " +
            item.index +
            "\nCantidad: " +
            amount;

        int hitMin =
            item.minHitToNpc > 0
            ? item.minHitToNpc
            : item.minHit;

        int hitMax =
            item.maxHitToNpc > 0
            ? item.maxHitToNpc
            : item.maxHit;

        if (hitMin > 0 ||
            hitMax > 0)
        {
            text +=
                "\nDaño: " +
                hitMin +
                " - " +
                hitMax;
        }

        if (item.minDef > 0 ||
            item.maxDef > 0)
        {
            text +=
                "\nDefensa: " +
                item.minDef +
                " - " +
                item.maxDef;
        }

        if (item.value > 0)
        {
            text +=
                "\nValor: " +
                item.value;
        }

        if (item.twoHands)
        {
            text +=
                "\nArma a dos manos";
        }

        if (item.objType == 1 &&
            item.minHunger > 0)
        {
            text +=
                "\nComida: +" +
                item.minHunger +
                " hambre";
        }
        else if (
            (item.objType == 13 ||
             item.objType == 34) &&
            item.minThirst > 0)
        {
            text +=
                "\nBebida: +" +
                item.minThirst +
                " sed";
        }
        else if (
            item.objType == 11 &&
            item.Consumable)
        {
            text +=
                "\nPoción tipo " +
                item.potionType;
        }

        if (item.IsParchment)
        {
            AOSpellDatabaseV120.SpellDef
                spell =
                    AOSpellDatabaseV120.Get(
                        item.spellIndex);

            text +=
                "\nHechizo: " +
                (spell == null
                    ? item.spellIndex.ToString()
                    : spell.name);
        }

        if (!string.IsNullOrWhiteSpace(
                item.description))
        {
            text +=
                "\n\n" +
                Short(
                    item.description,
                    150);
        }

        Vector2 m =
            Event.current
                .mousePosition;

        float w =
            230f * scale;

        float h =
            150f * scale;

        float x =
            Mathf.Min(
                m.x +
                    16f * scale,
                Screen.width -
                    w -
                    6f);

        float y =
            Mathf.Min(
                m.y +
                    12f * scale,
                Screen.height -
                    h -
                    6f);

        GUI.Box(
            new Rect(
                x,
                y,
                w,
                h),
            text,
            tooltipStyle);
    }

    static string ItemTypeName(
        int type)
    {
        if (type == 2)
            return "Arma";

        if (type == 3)
            return "Armadura";

        if (type == 5)
            return "Oro";

        if (type == 11)
            return "Poción";

        if (type == 16)
            return "Escudo";

        if (type == 17)
            return "Casco";

        if (type == 21)
            return "Amuleto";

        if (type == 24)
            return "Pergamino";

        if (type == 30)
            return "Instrumento mágico";

        if (type == 32)
            return "Flecha";

        if (type == 35)
            return "Anillo mágico";

        return "Objeto";
    }

    void DrawSpellPlaceholder()
    {
        if (magicV120 == null)
        {
            GUI.Label(
                R(785f,218f,215f,90f),
                "Magia no disponible.",
                centeredWhite);
            return;
        }

        if (magicV120.KnownSpellCount == 0)
        {
            GUI.Label(
                R(790f,235f,200f,72f),
                "No conocés hechizos.\nAprendé con un pergamino.",
                centeredWhite);

            if (AOAudioV190.Clicked(GUI.Button(
                    R(815f,365f,160f,28f),
                    magicV120.MeditationLabel)))
            {
                magicV120.ToggleMeditation();
            }

            return;
        }

        Rect viewport =
            R(783f,205f,220f,127f);

        float rowHeight =
            23f * scale;

        float contentHeight =
            Mathf.Max(
                viewport.height,
                magicV120.KnownSpellCount *
                rowHeight);

        spellScroll =
            GUI.BeginScrollView(
                viewport,
                spellScroll,
                new Rect(
                    0f,
                    0f,
                    viewport.width -
                    18f * scale,
                    contentHeight));

        for (int i = 0;
             i < magicV120.KnownSpellCount;
             i++)
        {
            AOSpellDatabaseV120.SpellDef spell =
                magicV120.GetKnownSpellAt(i);

            if (spell == null)
                continue;

            bool selected =
                spell.id ==
                magicV120.SelectedSpellId;

            string label =
                (selected ? "▶ " : "") +
                spell.name;

            if (AOAudioV190.Clicked(GUI.Button(
                    new Rect(
                        0f,
                        i * rowHeight,
                        viewport.width -
                        22f * scale,
                        rowHeight -
                        2f * scale),
                    label)))
            {
                magicV120.SelectSpell(
                    spell.id);
            }
        }

        GUI.EndScrollView();

        AOSpellDatabaseV120.SpellDef current =
            magicV120.SelectedSpell;

        if (current == null)
            return;

        Sprite icon =
            AOSpellDatabaseV120.Icon(
                current.id);

        if (icon != null)
        {
            Rect ir =
                R(786f,340f,38f,38f);

            Rect tr =
                icon.textureRect;

            Rect uv =
                new Rect(
                    tr.x /
                    icon.texture.width,
                    tr.y /
                    icon.texture.height,
                    tr.width /
                    icon.texture.width,
                    tr.height /
                    icon.texture.height);

            GUI.DrawTextureWithTexCoords(
                ir,
                icon.texture,
                uv,
                true);
        }

        float cooldown =
            magicV120.CooldownRemaining(
                current.id);

        string detail =
            current.name +
            "\nMana " +
            current.manaRequired +
            " | STA " +
            current.staminaRequired +
            " | Magia " +
            current.minSkill +
            "\n" +
            current.TargetLabel +
            (cooldown > 0f
                ? " | CD " +
                  cooldown.ToString("0.0")
                : "");

        GUI.Label(
            R(830f,338f,172f,45f),
            detail,
            tinyWhite);

        GUI.Label(
            R(786f,385f,210f,18f),
            Short(
                current.description,
                42),
            tinyWhite);

        if (!current.supportedLocal)
        {
            GUI.Label(
                R(885f,405f,110f,23f),
                "No disponible",
                centeredWhite);
        }
        else
        {
            if (AOAudioV190.Clicked(GUI.Button(
                    R(772f,446f,152f,33f),
                    GUIContent.none,
                    invisibleButton)))
            {
                if (current.target == 1)
                    magicV120.CastSelectedOnSelf();
                else
                    magicV120.BeginCastSelected();
            }

            if (current.target == 3 &&
                AOAudioV190.Clicked(GUI.Button(
                    R(895f,405f,98f,23f),
                    "En mí")))
            {
                magicV120.CastSelectedOnSelf();
            }
        }

        if (AOAudioV190.Clicked(GUI.Button(
                R(789f,405f,95f,23f),
                magicV120.MeditationLabel)))
        {
            magicV120.ToggleMeditation();
        }

        if (magicV120.IsTargeting)
        {
            GUI.Box(
                R(180f,128f,520f,28f),
                magicV120.TargetPrompt);
        }
    }

    void DrawLowerPanel()
    {
        Texture2D panel =
            lowerTab ==
                LowerTab.Stats
            ? panelStats
            : panelInfo;

        if (panel != null)
        {
            GUI.DrawTexture(
                R(
                    756f,
                    521f,
                    266f,
                    245f),
                panel,
                ScaleMode.StretchToFill,
                true);
        }

        Rect statsTab =
            R(
                756f,
                521f,
                133f,
                28f);

        Rect infoTab =
            R(
                889f,
                521f,
                133f,
                28f);

        if (AOAudioV190.Clicked(GUI.Button(
                statsTab,
                GUIContent.none,
                invisibleButton)))
        {
            lowerTab =
                LowerTab.Stats;
        }

        if (AOAudioV190.Clicked(GUI.Button(
                infoTab,
                GUIContent.none,
                invisibleButton)))
        {
            lowerTab =
                LowerTab.Info;
        }

        if (lowerTab ==
            LowerTab.Stats)
        {
            DrawStatsPanel();
        }
        else
        {
            DrawOriginalInfoButtons();
        }
    }

    void DrawStatsPanel()
    {
        int hp =
            combat == null
            ? 0
            : combat.HP;

        int maxHp =
            combat == null
            ? 1
            : Mathf.Max(
                1,
                combat.MaxHP);

        DrawClipped(
            barHp,
            R(
                791.2f,
                601.8f,
                172.8f,
                12.8f),
            SafeRatio(
                hp,
                maxHp));

        int activeMana =
            rpgV11 != null
            ? rpgV11.Mana
            : mana;

        int activeMaxMana =
            rpgV11 != null
            ? rpgV11.MaxMana
            : maxMana;

        int activeStamina =
            rpgV11 != null
            ? rpgV11.Stamina
            : stamina;

        int activeMaxStamina =
            rpgV11 != null
            ? rpgV11.MaxStamina
            : maxStamina;

        int activeHunger =
            rpgV11 != null
            ? rpgV11.Hunger
            : hunger;

        int activeMaxHunger =
            rpgV11 != null
            ? rpgV11.MaxHunger
            : maxHunger;

        int activeThirst =
            rpgV11 != null
            ? rpgV11.Thirst
            : thirst;

        int activeMaxThirst =
            rpgV11 != null
            ? rpgV11.MaxThirst
            : maxThirst;

        DrawClipped(
            barMana,
            R(
                791.2f,
                629.8f,
                172.8f,
                12.8f),
            SafeRatio(
                activeMana,
                activeMaxMana));

        DrawClipped(
            barStamina,
            R(
                790.4f,
                660.2f,
                71.2f,
                7.2f),
            SafeRatio(
                activeStamina,
                activeMaxStamina));

        DrawClipped(
            barThirst,
            R(
                912f,
                660.2f,
                25.6f,
                7.2f),
            SafeRatio(
                activeThirst,
                activeMaxThirst));

        DrawClipped(
            barHunger,
            R(
                975.2f,
                661f,
                25.6f,
                6.4f),
            SafeRatio(
                activeHunger,
                activeMaxHunger));

        GUI.Label(
            R(
                865f,
                600f,
                64f,
                18f),
            hp +
            "/" +
            maxHp,
            centeredWhite);

        GUI.Label(
            R(
                865f,
                628f,
                64f,
                18f),
            activeMaxMana > 0
                ? activeMana +
                  "/" +
                  activeMaxMana
                : "0/0",
            centeredWhite);

        GUI.Label(
            R(
                806f,
                652f,
                60f,
                18f),
            activeStamina.ToString(),
            centeredWhite);

        GUI.Label(
            R(
                912f,
                652f,
                30f,
                18f),
            activeThirst.ToString(),
            centeredWhite);

        GUI.Label(
            R(
                971f,
                652f,
                34f,
                18f),
            activeHunger.ToString(),
            centeredWhite);

        long gold =
            combat == null
            ? 0
            : combat.Gold;

        GUI.Label(
            R(
                788f,
                558f,
                110f,
                20f),
            gold.ToString(),
            tinyWhite);

    }

    void DrawOriginalInfoButtons()
    {
        // Posiciones originales de frmMain dentro de panelInf.
        Rect home =
            R(
                891f,
                634f,
                36f,
                33f);

        Rect stats =
            R(
                891f,
                672f,
                36f,
                33f);

        Rect quest =
            R(
                892f,
                596f,
                36f,
                33f);

        if (AOAudioV190.Clicked(GUI.Button(
                home,
                GUIContent.none,
                invisibleButton)))
        {
            if (deathV160 == null)
            {
                PushMessage(
                    "Sistema de Hogar no disponible.");
            }
            else
            {
                deathV160.TryGoHome(
                    out string result);

                PushMessage(
                    result);
            }
        }

        if (AOAudioV190.Clicked(GUI.Button(
                stats,
                GUIContent.none,
                invisibleButton)))
        {
            lowerTab =
                LowerTab.Stats;
        }

        if (AOAudioV190.Clicked(GUI.Button(
                quest,
                GUIContent.none,
                invisibleButton)))
        {
            AOQuestUIV150 journal = player == null
                ? UnityEngine.Object.FindFirstObjectByType<AOQuestUIV150>()
                : player.GetComponent<AOQuestUIV150>();
            if (journal != null)
                journal.OpenJournal();
            else
                PushMessage("Diario de misiones no disponible.");
        }

    }

    void DrawExperience()
    {
        if (combat == null ||
            barExp == null)
            return;

        long exp =
            rpgV11 != null
            ? rpgV11.Experience
            : combat.Exp;

        long stepLong =
            rpgV11 != null &&
            rpgV11.ExpToNextLevel > 0
            ? rpgV11.ExpToNextLevel
            : Mathf.Max(
                1,
                localExpPerBar);

        float ratio =
            stepLong <= 0
            ? 0f
            : Mathf.Clamp01(
                exp /
                (float)stepLong);

        DrawClipped(
            barExp,
            R(
                772f,
                103.2f,
                188.8f,
                12.8f),
            ratio);

        GUI.Label(
            R(
                785f,
                83f,
                180f,
                18f),
            (rpgV11 != null
                ? "NV " +
                  rpgV11.Level +
                  "  EXP "
                : "EXP ") +
            exp +
            (rpgV11 != null &&
             rpgV11.ExpToNextLevel > 0
                ? "/" +
                  rpgV11.ExpToNextLevel
                : ""),
            centeredWhite);
    }

    void DrawChat()
    {
        int count = Mathf.Min(4, chat.Count);
        for (int i = 0; i < count; i++)
        {
            string original = chat[chat.Count - count + i]
                .Replace('\r', ' ').Replace('\n', ' ');
            string line = original;
            while (line.Length > 1 &&
                   chatStyle.CalcSize(new GUIContent(line)).x > 610f * scale)
                line = original.Substring(0, line.Length - 2) + "…";

            GUI.Label(R(16f, 39f + (4 - count + i) * 18f, 612f, 18f),
                line, chatStyle);
        }

        if (chatEditing)
        {
            GUI.SetNextControlName(
                "AO_CHAT_INPUT");

            chatInput =
                GUI.TextField(
                    R(
                        40f,
                        120f,
                        545.6f,
                        24f),
                    chatInput,
                    160,
                    chatInputStyle);

            GUI.FocusControl(
                "AO_CHAT_INPUT");
        }
        else
        {
            GUI.Label(
                R(
                    40f,
                    120f,
                    545f,
                    20f),
                "Enter: escribir mensaje local",
                tinyWhite);
        }
    }

    void DrawSpeech()
    {
        if (string.IsNullOrEmpty(speechText) ||
            Time.unscaledTime >= speechUntil ||
            player == null || gameCamera == null)
            return;

        if (speechVisual == null)
            speechVisual = player.GetComponentInChildren<AOCharacterRenderer>(true);

        Vector3 anchor = speechVisual == null
            ? player.transform.position + Vector3.up * 1.5f
            : speechVisual.SpeechAnchor;
        Vector3 screen = gameCamera.WorldToScreenPoint(anchor);
        if (screen.z <= 0f)
            return;

        Rect view = gameCamera.pixelRect;
        if (!view.Contains(new Vector2(screen.x, screen.y)))
            return;

        string visible = WrapSpeech(Short(speechText, 72));
        float width = Mathf.Min(220f * scale, view.width - 8f);
        float height = Mathf.Min(84f,
            speechStyle.CalcHeight(new GUIContent(visible), width - 12f) + 6f);
        float x = Mathf.Clamp(screen.x - width * 0.5f,
            view.xMin + 4f, view.xMax - width - 4f);
        float top = Screen.height - view.yMax;
        float bottom = Screen.height - view.yMin;
        float y = Mathf.Clamp(Screen.height - screen.y - height - 5f,
            top + 4f, bottom - height - 4f);

        float outline = Mathf.Max(1f, scale);
        Rect textRect = new Rect(x, y, width, height);
        GUI.Label(new Rect(x - outline, y, width, height), visible,
            speechOutlineStyle);
        GUI.Label(new Rect(x + outline, y, width, height), visible,
            speechOutlineStyle);
        GUI.Label(new Rect(x, y - outline, width, height), visible,
            speechOutlineStyle);
        GUI.Label(new Rect(x, y + outline, width, height), visible,
            speechOutlineStyle);
        GUI.Label(textRect, visible, speechStyle);
    }

    static string WrapSpeech(string text)
    {
        string[] words = text.Split(new[] { ' ' },
            StringSplitOptions.RemoveEmptyEntries);
        string result = "";
        int lineLength = 0;
        foreach (string word in words)
        {
            if (lineLength > 0 && lineLength + word.Length + 1 > 18)
            {
                result += "\n";
                lineLength = 0;
            }
            else if (lineLength > 0)
            {
                result += " ";
                lineLength++;
            }

            result += word;
            lineLength += word.Length;
        }

        return result;
    }

    void DrawRPGPanel()
    {
        if (rpgV11 == null)
        {
            GUI.Box(
                R(
                    150f,
                    175f,
                    520f,
                    380f),
                "AO RPG v0.11\n\nNo se encontró AOPlayerRPGV11.");

            return;
        }

        Rect outer =
            R(
                120f,
                165f,
                590f,
                490f);

        GUI.Box(
            outer,
            "PERSONAJE");

        if (AOAudioV190.Clicked(GUI.Button(
                R(603f, 177f, 45f, 30f), "X")))
            showRPGPanel = false;

        GUI.Label(
            R(
                140f,
                195f,
                540f,
                48f),
            rpgV11.RaceName +
            " / " +
            rpgV11.GenderName +
            " / " +
            rpgV11.ClassName +
            "  |  Nivel " +
            rpgV11.Level +
            "\nEXP " +
            rpgV11.Experience +
            "/" +
            (rpgV11.ExpToNextLevel > 0
                ? rpgV11.ExpToNextLevel.ToString()
                : "MAX") +
            "  |  Puntos libres: " +
            rpgV11.SkillPoints,
            tinyWhite);

        GUI.Label(
            R(
                140f,
                245f,
                540f,
                46f),
            "FUE " +
            rpgV11.Strength +
            "   AGI " +
            rpgV11.Agility +
            "   INT " +
            rpgV11.Intelligence +
            "   CON " +
            rpgV11.Constitution +
            "   CAR " +
            rpgV11.Charisma +
            "\nHP " +
            (combat == null
                ? 0
                : combat.HP) +
            "/" +
            rpgV11.MaxHP +
            "   MP " +
            rpgV11.Mana +
            "/" +
            rpgV11.MaxMana +
            "   STA " +
            rpgV11.Stamina +
            "/" +
            rpgV11.MaxStamina,
            tinyWhite);

        for (int i = 1;
             i <= 24;
             i++)
        {
            int column =
                (i - 1) / 12;

            int row =
                (i - 1) % 12;

            float x =
                column == 0
                ? 140f
                : 430f;

            float y =
                300f +
                row * 27f;

            AORPGDatabaseV11.SkillDef skill =
                AORPGDatabaseV11.GetSkill(i);

            string name =
                skill == null
                ? "Skill " + i
                : skill.name;

            GUI.Label(
                R(
                    x,
                    y,
                    205f,
                    22f),
                Short(name, 24) +
                ": " +
                rpgV11.GetSkill(i),
                tinyWhite);

            GUI.enabled =
                rpgV11.SkillPoints > 0 &&
                rpgV11.GetSkill(i) <
                AORPGDatabaseV11.MaxSkill;

            if (AOAudioV190.Clicked(GUI.Button(
                    R(
                        x + 205f,
                        y,
                        28f,
                        22f),
                    "+")))
            {
                if (rpgV11
                    .TryIncreaseSkill(i))
                {
                    PushMessage(
                        name +
                        " sube a " +
                        rpgV11.GetSkill(i) +
                        ".");
                }
            }

            GUI.enabled = true;
        }
    }

    static float SafeRatio(
        int value,
        int max)
    {
        if (max <= 0)
            return 0f;

        return Mathf.Clamp01(
            value /
            (float)max);
    }

    static void DrawClipped(
        Texture2D texture,
        Rect rect,
        float ratio)
    {
        if (texture == null ||
            ratio <= 0f)
            return;

        ratio =
            Mathf.Clamp01(
                ratio);

        Rect clipped =
            new Rect(
                rect.x,
                rect.y,
                rect.width *
                    ratio,
                rect.height);

        GUI.DrawTextureWithTexCoords(
            clipped,
            texture,
            new Rect(
                0f,
                0f,
                ratio,
                1f),
            true);
    }

    static string Short(
        string text,
        int max)
    {
        if (string.IsNullOrEmpty(
                text) ||
            text.Length <= max)
        {
            return text ?? "";
        }

        return text.Substring(
            0,
            Mathf.Max(
                1,
                max - 1)) +
            "…";
    }

    public static void PushMessage(
        string message)
    {
        AOInterfaceV0101 ui =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOInterfaceV0101>();

        if (ui == null ||
            string.IsNullOrWhiteSpace(
                message))
            return;

        ui.chat.Add(message);

        while (ui.chat.Count > 60)
            ui.chat.RemoveAt(0);
    }

    bool PressedInventory()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.iKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.I);
#endif
    }

    bool PressedMap()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.mKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.M);
#endif
    }

    bool PressedRPG()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.f9Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F9);
#endif
    }
}
