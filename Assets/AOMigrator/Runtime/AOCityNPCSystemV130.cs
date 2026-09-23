using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class AOCityNPCSystemV130 : MonoBehaviour
{
    AOInventoryV10 inventory;
    AOPlayerCombatV09 combat;
    AOPlayerRPGV11 rpg;
    AOCityUIV130 ui;

    AudioSource audioSource;

    bool testGoldGranted;

    readonly Dictionary<int, Dictionary<int, int>>
        finiteStock =
            new Dictionary<int, Dictionary<int, int>>();

    void Awake()
    {
        FindReferences();

        audioSource =
            GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource =
                gameObject.AddComponent
                    <AudioSource>();

            audioSource.playOnAwake =
                false;

            audioSource.spatialBlend =
                0f;
        }
    }

    void Update()
    {
        if (!testGoldGranted &&
            PressedTestGold())
        {
            FindReferences();

            if (combat != null)
            {
                combat.AddGold(
                    20000,
                    true);

                testGoldGranted = true;

                AOCityUIV130.Message(
                    "F4 TEST: +20.000 de oro.");
            }
        }
    }

    void FindReferences()
    {
        if (inventory == null)
            inventory =
                GetComponent<AOInventoryV10>();

        if (combat == null)
            combat =
                GetComponent<AOPlayerCombatV09>();

        if (rpg == null)
            rpg =
                GetComponent<AOPlayerRPGV11>();

        if (ui == null)
            ui =
                GetComponent<AOCityUIV130>();
    }

    public bool TryInteract(
        AONPCMetadata npc)
    {
        if (npc == null)
            return false;

        FindReferences();

        AOCityNPCDatabaseV130.NPCDef def =
            AOCityNPCDatabaseV130.Get(
                npc.NpcIndex);

        if (def == null ||
            ui == null)
            return false;

        bool isPriest =
            def.npcType == 1 ||
            def.npcType == 9;

        if (combat != null &&
            combat.IsDead &&
            !isPriest)
        {
            AOInterfaceV0101.PushMessage(
                "¡¡Estás muerto!! Sólo un sacerdote puede ayudarte.");
            return true;
        }

        AOQuestSystemV150 quests =
            GetComponent
                <AOQuestSystemV150>();

        if (quests != null)
        {
            quests.NotifyNpcInteracted(
                npc.NpcIndex);

            if (quests.TryOpenForNpc(
                    npc.NpcIndex,
                    def))
            {
                return true;
            }
        }

        if (def.trades)
        {
            ui.OpenMerchant(
                def);
            return true;
        }

        if (def.npcType == 4)
        {
            ui.OpenBank(
                def);
            return true;
        }

        if (def.npcType == 1 ||
            def.npcType == 9)
        {
            ui.OpenPriest(
                def);

            return true;
        }

        ui.OpenDialogue(
            def);

        return true;
    }

    public long Gold =>
        combat == null
        ? 0
        : combat.Gold;

    public int TradingSkill =>
        rpg == null
        ? 0
        : rpg.GetSkill(9);

    public int BuyPrice(
        int itemIndex)
    {
        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                itemIndex);

        if (item == null)
            return 0;

        float discount =
            1f +
            TradingSkill /
            100f;

        return Mathf.Max(
            1,
            Mathf.CeilToInt(
                item.value /
                Mathf.Max(
                    1f,
                    discount)));
    }

    public int SellPrice(
        int itemIndex)
    {
        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                itemIndex);

        if (item == null ||
            !item.SellableToNPC)
            return 0;

        return Mathf.Max(
            0,
            Mathf.FloorToInt(
                item.value /
                3f));
    }

    public bool MerchantAccepts(
        AOCityNPCDatabaseV130.NPCDef merchant,
        int itemIndex)
    {
        if (merchant == null)
            return false;

        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                itemIndex);

        if (item == null ||
            !item.SellableToNPC)
            return false;

        if (merchant.itemType == 100)
            return true;

        if (merchant.itemType > 0 &&
            merchant.itemType ==
                item.objType)
            return true;

        if (merchant.stock != null)
        {
            foreach (
                AOCityNPCDatabaseV130.ShopEntry entry
                in merchant.stock)
            {
                if (entry != null &&
                    entry.itemIndex ==
                        itemIndex)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int CurrentStock(
        AOCityNPCDatabaseV130.NPCDef merchant,
        AOCityNPCDatabaseV130.ShopEntry entry)
    {
        if (merchant == null ||
            entry == null)
            return 0;

        if (entry.infinite)
            return -1;

        Dictionary<int, int> stock =
            GetFiniteStock(
                merchant);

        return stock.TryGetValue(
            entry.itemIndex,
            out int amount)
            ? amount
            : 0;
    }

    public bool Buy(
        AOCityNPCDatabaseV130.NPCDef merchant,
        AOCityNPCDatabaseV130.ShopEntry entry,
        int amount,
        out string message)
    {
        FindReferences();

        message = "";

        if (merchant == null ||
            entry == null ||
            inventory == null ||
            combat == null)
            return false;

        amount =
            Mathf.Max(
                1,
                amount);

        int stock =
            CurrentStock(
                merchant,
                entry);

        if (stock == 0)
        {
            message =
                "El comerciante no tiene más stock.";
            return false;
        }

        if (stock > 0)
        {
            amount =
                Mathf.Min(
                    amount,
                    stock);
        }

        int unit =
            BuyPrice(
                entry.itemIndex);

        long total =
            (long)unit *
            amount;

        if (combat.Gold <
            total)
        {
            message =
                "No tenés suficiente oro. Necesitás " +
                total +
                ".";
            return false;
        }

        if (!inventory.AddItem(
                entry.itemIndex,
                amount))
        {
            message =
                "No hay espacio en tu inventario.";
            return false;
        }

        if (!combat.SpendGold(
                total))
        {
            message =
                "No pude descontar el oro.";
            return false;
        }

        if (!entry.infinite)
        {
            Dictionary<int, int> values =
                GetFiniteStock(
                    merchant);

            values[entry.itemIndex] =
                Mathf.Max(
                    0,
                    stock -
                    amount);
        }

        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                entry.itemIndex);

        message =
            "Compraste " +
            (item == null
                ? "OBJ " +
                  entry.itemIndex
                : item.name) +
            " x" +
            amount +
            " por " +
            total +
            " oro.";

        return true;
    }

    public bool Sell(
        AOCityNPCDatabaseV130.NPCDef merchant,
        int inventorySlot,
        int amount,
        out string message)
    {
        FindReferences();

        message = "";

        if (merchant == null ||
            inventory == null ||
            combat == null)
            return false;

        int itemIndex =
            inventory.GetSlotItemIndex(
                inventorySlot);

        int available =
            inventory.GetSlotAmount(
                inventorySlot);

        if (itemIndex <= 0 ||
            available <= 0)
        {
            message =
                "No hay objeto seleccionado.";
            return false;
        }

        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                itemIndex);

        if (item == null)
        {
            message =
                "Objeto desconocido.";
            return false;
        }

        if (inventory
            .IsSlotEquippedPublic(
                inventorySlot))
        {
            message =
                "Quitá el objeto antes de venderlo.";
            return false;
        }

        if (!MerchantAccepts(
                merchant,
                itemIndex))
        {
            message =
                merchant.name +
                " no compra este tipo de objeto.";
            return false;
        }

        int unit =
            SellPrice(
                itemIndex);

        if (unit <= 0)
        {
            message =
                "Ese objeto no se puede vender.";
            return false;
        }

        amount =
            Mathf.Clamp(
                amount,
                1,
                available);

        if (!inventory
            .RemoveItemAtPublic(
                inventorySlot,
                amount))
        {
            message =
                "No pude retirar el objeto del inventario.";
            return false;
        }

        long total =
            (long)unit *
            amount;

        combat.AddGold(
            total);

        message =
            "Vendiste " +
            item.name +
            " x" +
            amount +
            " por " +
            total +
            " oro.";

        return true;
    }

    public void HealOrResurrect(
        out string message)
    {
        FindReferences();

        message = "";

        if (combat == null)
            return;

        bool wasDead =
            combat.IsDead;

        if (wasDead)
            combat.ResurrectFromPriest();
        else
            combat.HealFullyFromPriest();

        PlaySound(
            117);

        message =
            wasDead
            ? "El sacerdote te ha devuelto a la vida."
            : "El sacerdote curó todas tus heridas.";
    }

    public void PlayClose(
        AOCityNPCDatabaseV130.NPCDef def)
    {
        // Los sonidos de apertura y cierre del NPC incluyen voces.
        // Se conserva el sonido de curación, que es un efecto independiente.
    }

    public void CaptureFiniteStock(
        out int[] npcIds,
        out int[] itemIds,
        out int[] amounts)
    {
        List<int> npcList =
            new List<int>();

        List<int> itemList =
            new List<int>();

        List<int> amountList =
            new List<int>();

        foreach (
            KeyValuePair<int, Dictionary<int, int>>
            npcEntry in finiteStock)
        {
            if (npcEntry.Value == null)
                continue;

            foreach (
                KeyValuePair<int, int>
                itemEntry in npcEntry.Value)
            {
                npcList.Add(
                    npcEntry.Key);

                itemList.Add(
                    itemEntry.Key);

                amountList.Add(
                    Mathf.Max(
                        0,
                        itemEntry.Value));
            }
        }

        npcIds =
            npcList.ToArray();

        itemIds =
            itemList.ToArray();

        amounts =
            amountList.ToArray();
    }

    public void RestoreFiniteStock(
        int[] npcIds,
        int[] itemIds,
        int[] amounts)
    {
        finiteStock.Clear();

        if (npcIds == null ||
            itemIds == null ||
            amounts == null)
            return;

        int count =
            Mathf.Min(
                npcIds.Length,
                Mathf.Min(
                    itemIds.Length,
                    amounts.Length));

        for (int i = 0;
             i < count;
             i++)
        {
            if (npcIds[i] <= 0 ||
                itemIds[i] <= 0)
                continue;

            if (!finiteStock.TryGetValue(
                    npcIds[i],
                    out Dictionary<int, int> values))
            {
                values =
                    new Dictionary<int, int>();

                finiteStock[
                    npcIds[i]] =
                    values;
            }

            values[
                itemIds[i]] =
                Mathf.Max(
                    0,
                    amounts[i]);
        }
    }

    public void ResetFiniteStock()
    {
        finiteStock.Clear();
    }

    Dictionary<int, int> GetFiniteStock(
        AOCityNPCDatabaseV130.NPCDef merchant)
    {
        if (!finiteStock.TryGetValue(
                merchant.npcIndex,
                out Dictionary<int, int> values))
        {
            values =
                new Dictionary<int, int>();

            if (merchant.stock != null)
            {
                foreach (
                    AOCityNPCDatabaseV130.ShopEntry entry
                    in merchant.stock)
                {
                    if (entry != null &&
                        !entry.infinite)
                    {
                        values[entry.itemIndex] =
                            Mathf.Max(
                                0,
                                entry.amount);
                    }
                }
            }

            finiteStock[
                merchant.npcIndex] =
                values;
        }

        return values;
    }

    void PlaySound(
        int soundId)
    {
        if (soundId <= 0 ||
            audioSource == null)
            return;

        AudioClip clip =
            Resources.Load<AudioClip>(
                "AOMigrator/CityV130/Audio/wav_" +
                soundId);

        if (clip != null)
        {
            audioSource.PlayOneShot(
                clip,
                0.8f);
        }
    }

    bool PressedTestGold()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.f4Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(
            KeyCode.F4);
#endif
    }
}
