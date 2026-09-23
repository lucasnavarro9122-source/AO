using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class AOCityUIV130 : MonoBehaviour
{
    enum Mode
    {
        None,
        Merchant,
        Bank,
        Priest,
        Dialogue
    }

    static AOCityUIV130 instance;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        instance = null;
    }

    public static bool ModalOpen =>
        instance != null &&
        instance.mode !=
            Mode.None;

    Mode mode;

    AOCityNPCDatabaseV130.NPCDef
        currentNPC;

    AOCityNPCSystemV130 city;
    AOCityBankV130 bank;
    AOInventoryV10 inventory;
    AOPlayerCombatV09 combat;

    Vector2 leftScroll;
    Vector2 rightScroll;

    int selectedShopItem = -1;
    int selectedInventorySlot = -1;
    int selectedBankSlot = -1;

    int quantity = 1;

    string message = "";

    Rect windowRect =
        new Rect(
            100,
            80,
            900,
            610);

    public void Configure(
        AOCityNPCSystemV130 newCity,
        AOCityBankV130 newBank)
    {
        city = newCity;
        bank = newBank;
        FindReferences();
    }

    void Awake()
    {
        instance = this;
        FindReferences();
    }

    void Update()
    {
        if (mode !=
                Mode.None &&
            PressedEscape())
        {
            Close();
        }
    }

    void FindReferences()
    {
        if (city == null)
            city =
                GetComponent
                    <AOCityNPCSystemV130>();

        if (bank == null)
            bank =
                GetComponent
                    <AOCityBankV130>();

        if (inventory == null)
            inventory =
                GetComponent
                    <AOInventoryV10>();

        if (combat == null)
            combat =
                GetComponent
                    <AOPlayerCombatV09>();
    }

    public void OpenMerchant(
        AOCityNPCDatabaseV130.NPCDef npc)
    {
        currentNPC = npc;
        mode = Mode.Merchant;
        ResetSelection();

        message =
            npc == null
            ? ""
            : npc.description;
    }

    public void OpenBank(
        AOCityNPCDatabaseV130.NPCDef npc)
    {
        currentNPC = npc;
        mode = Mode.Bank;
        ResetSelection();

        message =
            "Finanzas Goliath — bóveda local de 42 slots.";
    }

    public void OpenPriest(
        AOCityNPCDatabaseV130.NPCDef npc)
    {
        currentNPC = npc;
        mode = Mode.Priest;
        ResetSelection();

        message =
            npc == null
            ? ""
            : npc.description;
    }

    public void OpenDialogue(
        AOCityNPCDatabaseV130.NPCDef npc)
    {
        currentNPC = npc;
        mode = Mode.Dialogue;
        ResetSelection();

        message =
            npc == null
            ? ""
            : npc.description;
    }

    public static void Message(
        string text)
    {
        if (instance != null)
            instance.message =
                text ?? "";
    }

    public void Close()
    {
        if (city != null)
            city.PlayClose(
                currentNPC);

        mode = Mode.None;
        currentNPC = null;
        ResetSelection();
    }

    void ResetSelection()
    {
        selectedShopItem = -1;
        selectedInventorySlot = -1;
        selectedBankSlot = -1;
        quantity = 1;
        leftScroll = Vector2.zero;
        rightScroll = Vector2.zero;
    }

    void OnGUI()
    {
        if (mode ==
            Mode.None)
            return;

        FindReferences();

        windowRect.width =
            Mathf.Min(
                900f,
                Screen.width - 20f);

        windowRect.height =
            Mathf.Min(
                610f,
                Screen.height - 20f);

        windowRect.x =
            Mathf.Clamp(
                windowRect.x,
                10f,
                Mathf.Max(
                    10f,
                    Screen.width -
                    windowRect.width -
                    10f));

        windowRect.y =
            Mathf.Clamp(
                windowRect.y,
                10f,
                Mathf.Max(
                    10f,
                    Screen.height -
                    windowRect.height -
                    10f));

        windowRect =
            GUI.ModalWindow(
                130130,
                windowRect,
                DrawWindow,
                Title());
    }

    string Title()
    {
        string name =
            currentNPC == null
            ? "NPC"
            : currentNPC.name;

        if (mode ==
            Mode.Merchant)
            return name +
                " — Comercio";

        if (mode ==
            Mode.Bank)
            return name +
                " — Banco";

        if (mode ==
            Mode.Priest)
            return name +
                " — Sacerdote";

        return name;
    }

    void DrawWindow(
        int id)
    {
        GUILayout.BeginVertical();

        if (!string.IsNullOrWhiteSpace(
                message))
        {
            GUILayout.Box(
                message,
                GUILayout.Height(
                    48));
        }

        if (mode ==
            Mode.Merchant)
        {
            DrawMerchant();
        }
        else if (
            mode ==
            Mode.Bank)
        {
            DrawBank();
        }
        else if (
            mode ==
            Mode.Priest)
        {
            DrawPriest();
        }
        else
        {
            DrawDialogue();
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();

        GUILayout.Label(
            "Esc = cerrar");

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(
                "Cerrar",
                GUILayout.Width(
                    110)))
        {
            Close();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUI.DragWindow(
            new Rect(
                0,
                0,
                windowRect.width,
                25));
    }

    void DrawMerchant()
    {
        if (city == null ||
            inventory == null ||
            currentNPC == null)
            return;

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(
                420));

        GUILayout.Label(
            "Mercadería");

        leftScroll =
            GUILayout.BeginScrollView(
                leftScroll,
                GUILayout.Height(
                    330));

        AOCityNPCDatabaseV130.ShopEntry[]
            stock =
                currentNPC.stock;

        if (stock != null)
        {
            for (int i = 0;
                 i < stock.Length;
                 i++)
            {
                var entry =
                    stock[i];

                if (entry == null)
                    continue;

                AOItemDatabaseV10.ItemDef item =
                    AOItemDatabaseV10.Get(
                        entry.itemIndex);

                if (item == null)
                    continue;

                int currentStock =
                    city.CurrentStock(
                        currentNPC,
                        entry);

                string stockText =
                    currentStock < 0
                    ? "∞"
                    : currentStock.ToString();

                int price =
                    city.BuyPrice(
                        item.index);

                bool selected =
                    selectedShopItem ==
                    i;

                if (GUILayout.Button(
                        (selected
                            ? "▶ "
                            : "") +
                        item.name +
                        " | " +
                        price +
                        " oro | stock " +
                        stockText,
                        GUILayout.Height(
                            26)))
                {
                    selectedShopItem = i;
                    selectedInventorySlot =
                        -1;
                }
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(8);

        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(
                420));

        GUILayout.Label(
            "Tu inventario");

        rightScroll =
            GUILayout.BeginScrollView(
                rightScroll,
                GUILayout.Height(
                    330));

        for (int i = 0;
             i < inventory.SlotCount;
             i++)
        {
            int itemIndex =
                inventory
                    .GetSlotItemIndex(i);

            int amount =
                inventory
                    .GetSlotAmount(i);

            if (itemIndex <= 0 ||
                amount <= 0)
                continue;

            AOItemDatabaseV10.ItemDef item =
                AOItemDatabaseV10.Get(
                    itemIndex);

            string name =
                item == null
                ? "OBJ " +
                  itemIndex
                : item.name;

            bool selected =
                selectedInventorySlot ==
                    i;

            int sell =
                city.SellPrice(
                    itemIndex);

            if (GUILayout.Button(
                    (selected
                        ? "▶ "
                        : "") +
                    name +
                    " x" +
                    amount +
                    " | venta " +
                    sell,
                    GUILayout.Height(
                        26)))
            {
                selectedInventorySlot =
                    i;

                selectedShopItem = -1;
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        DrawQuantity();

        GUILayout.BeginHorizontal();

        GUILayout.Label(
            "Oro: " +
            city.Gold +
            " | Comerciar: " +
            city.TradingSkill);

        GUILayout.FlexibleSpace();

        GUI.enabled =
            selectedShopItem >= 0;

        if (GUILayout.Button(
                "Comprar",
                GUILayout.Width(
                    130)))
        {
            var entry =
                currentNPC.stock[
                    selectedShopItem];

            city.Buy(
                currentNPC,
                entry,
                quantity,
                out message);
        }

        GUI.enabled =
            selectedInventorySlot >= 0;

        if (GUILayout.Button(
                "Vender",
                GUILayout.Width(
                    130)))
        {
            city.Sell(
                currentNPC,
                selectedInventorySlot,
                quantity,
                out message);
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    void DrawQuantity()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(
            "Cantidad: " +
            quantity,
            GUILayout.Width(
                100));

        if (GUILayout.Button(
                "-",
                GUILayout.Width(
                    35)))
        {
            quantity =
                Mathf.Max(
                    1,
                    quantity - 1);
        }

        if (GUILayout.Button(
                "+",
                GUILayout.Width(
                    35)))
        {
            quantity =
                Mathf.Min(
                    10000,
                    quantity + 1);
        }

        if (GUILayout.Button(
                "1",
                GUILayout.Width(
                    45)))
            quantity = 1;

        if (GUILayout.Button(
                "5",
                GUILayout.Width(
                    45)))
            quantity = 5;

        if (GUILayout.Button(
                "10",
                GUILayout.Width(
                    45)))
            quantity = 10;

        GUILayout.EndHorizontal();
    }

    void DrawBank()
    {
        if (bank == null ||
            inventory == null ||
            combat == null)
            return;

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(
                420));

        GUILayout.Label(
            "Banco — 42 slots");

        leftScroll =
            GUILayout.BeginScrollView(
                leftScroll,
                GUILayout.Height(
                    330));

        for (int i = 0;
             i < AOCityBankV130.BANK_SLOTS;
             i++)
        {
            int itemIndex =
                bank.GetItemIndex(i);

            int amount =
                bank.GetAmount(i);

            if (itemIndex <= 0)
                continue;

            AOItemDatabaseV10.ItemDef item =
                AOItemDatabaseV10.Get(
                    itemIndex);

            bool selected =
                selectedBankSlot ==
                    i;

            if (GUILayout.Button(
                    (selected
                        ? "▶ "
                        : "") +
                    "[" +
                    (i + 1) +
                    "] " +
                    (item == null
                        ? "OBJ " +
                          itemIndex
                        : item.name) +
                    " x" +
                    amount,
                    GUILayout.Height(
                        26)))
            {
                selectedBankSlot = i;
                selectedInventorySlot =
                    -1;
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(8);

        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(
                420));

        GUILayout.Label(
            "Inventario");

        rightScroll =
            GUILayout.BeginScrollView(
                rightScroll,
                GUILayout.Height(
                    330));

        for (int i = 0;
             i < inventory.SlotCount;
             i++)
        {
            int itemIndex =
                inventory
                    .GetSlotItemIndex(i);

            int amount =
                inventory
                    .GetSlotAmount(i);

            if (itemIndex <= 0)
                continue;

            AOItemDatabaseV10.ItemDef item =
                AOItemDatabaseV10.Get(
                    itemIndex);

            bool selected =
                selectedInventorySlot ==
                    i;

            if (GUILayout.Button(
                    (selected
                        ? "▶ "
                        : "") +
                    "[" +
                    (i + 1) +
                    "] " +
                    (item == null
                        ? "OBJ " +
                          itemIndex
                        : item.name) +
                    " x" +
                    amount,
                    GUILayout.Height(
                        26)))
            {
                selectedInventorySlot = i;
                selectedBankSlot = -1;
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        DrawQuantity();

        GUILayout.BeginHorizontal();

        GUI.enabled =
            selectedInventorySlot >= 0;

        if (GUILayout.Button(
                "Depositar item",
                GUILayout.Width(
                    145)))
        {
            bank.DepositItem(
                selectedInventorySlot,
                quantity,
                out message);
        }

        GUI.enabled =
            selectedBankSlot >= 0;

        if (GUILayout.Button(
                "Retirar item",
                GUILayout.Width(
                    145)))
        {
            bank.WithdrawItem(
                selectedBankSlot,
                quantity,
                out message);
        }

        GUI.enabled = true;

        GUILayout.FlexibleSpace();

        GUILayout.Label(
            "Oro: " +
            combat.Gold +
            " | Banco: " +
            bank.BankGold);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Depositar 100",
                GUILayout.Width(
                    120)))
        {
            bank.DepositGold(
                100,
                out message);
        }

        if (GUILayout.Button(
                "Depositar 1000",
                GUILayout.Width(
                    120)))
        {
            bank.DepositGold(
                1000,
                out message);
        }

        if (GUILayout.Button(
                "Depositar todo",
                GUILayout.Width(
                    120)))
        {
            bank.DepositGold(
                combat.Gold,
                out message);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(
                "Retirar 100",
                GUILayout.Width(
                    110)))
        {
            bank.WithdrawGold(
                100,
                out message);
        }

        if (GUILayout.Button(
                "Retirar 1000",
                GUILayout.Width(
                    110)))
        {
            bank.WithdrawGold(
                1000,
                out message);
        }

        if (GUILayout.Button(
                "Retirar todo",
                GUILayout.Width(
                    110)))
        {
            bank.WithdrawGold(
                bank.BankGold,
                out message);
        }

        GUILayout.EndHorizontal();
    }

    void DrawPriest()
    {
        if (city == null ||
            combat == null)
            return;

        GUILayout.Space(
            35);

        GUILayout.Box(
            combat.IsDead
            ? "Estás muerto. El sacerdote puede devolverte a la vida."
            : "El sacerdote puede curar todas tus heridas y el veneno.",
            GUILayout.Height(
                80));

        GUILayout.Space(
            20);

        if (GUILayout.Button(
                combat.IsDead
                ? "Resucitar"
                : "Curar completamente",
                GUILayout.Height(
                    46)))
        {
            city.HealOrResurrect(
                out message);
        }
    }

    void DrawDialogue()
    {
        GUILayout.Space(
            20);

        GUILayout.Box(
            currentNPC == null
            ? ""
            : currentNPC.description,
            GUILayout.Height(
                220));

        if (currentNPC != null &&
            currentNPC.npcType == 17)
        {
            GUILayout.Label(
                "NPC de Quest: el diálogo ya está activo. " +
                "El seguimiento de misiones se conecta en la etapa de quests.");
        }
    }

    bool PressedEscape()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(
            KeyCode.Escape);
#endif
    }
}
