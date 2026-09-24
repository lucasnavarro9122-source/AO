using System;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class AOCityBankV130 : MonoBehaviour
{
    public const int BANK_SLOTS = 42;
    const int MAX_STACK = 10000;

    [Serializable]
    class BankSave
    {
        public int[] itemIndex =
            new int[BANK_SLOTS];

        public int[] amount =
            new int[BANK_SLOTS];

        public long gold;
    }

    BankSave data =
        new BankSave();

    AOInventoryV10 inventory;
    AOPlayerCombatV09 combat;

    string SavePath =>
        Path.Combine(
            Application.persistentDataPath,
            "ao_bank_v130.json");

    public long BankGold =>
        data == null
        ? 0
        : data.gold;

    void Awake()
    {
        FindReferences();
        Load();
    }

    void FindReferences()
    {
        if (inventory == null)
            inventory =
                GetComponent<AOInventoryV10>();

        if (combat == null)
            combat =
                GetComponent<AOPlayerCombatV09>();
    }

    void Ensure()
    {
        if (data == null)
            data =
                new BankSave();

        if (data.itemIndex == null ||
            data.itemIndex.Length !=
                BANK_SLOTS)
        {
            data.itemIndex =
                new int[BANK_SLOTS];
        }

        if (data.amount == null ||
            data.amount.Length !=
                BANK_SLOTS)
        {
            data.amount =
                new int[BANK_SLOTS];
        }
    }

    public int[] CaptureItemIndices()
    {
        Ensure();

        int[] result =
            new int[BANK_SLOTS];

        Array.Copy(
            data.itemIndex,
            result,
            BANK_SLOTS);

        return result;
    }

    public int[] CaptureAmounts()
    {
        Ensure();

        int[] result =
            new int[BANK_SLOTS];

        Array.Copy(
            data.amount,
            result,
            BANK_SLOTS);

        return result;
    }

    public void RestoreSaveState(
        int[] itemIndices,
        int[] amounts,
        long savedGold)
    {
        Ensure();

        for (int i = 0;
             i < BANK_SLOTS;
             i++)
        {
            int itemIndex =
                itemIndices != null &&
                i < itemIndices.Length
                ? Mathf.Max(
                    0,
                    itemIndices[i])
                : 0;

            int amount =
                amounts != null &&
                i < amounts.Length
                ? Mathf.Clamp(
                    amounts[i],
                    0,
                    MAX_STACK)
                : 0;

            if (amount <= 0)
                itemIndex = 0;

            data.itemIndex[i] =
                itemIndex;

            data.amount[i] =
                itemIndex > 0
                ? amount
                : 0;
        }

        data.gold =
            System.Math.Max(
                0L,
                savedGold);

        Save();
    }

    public void ClearAll()
    {
        data =
            new BankSave();

        Save();
    }

    public void AddGoldReward(
        long amount)
    {
        if (amount <= 0)
            return;

        Ensure();

        data.gold +=
            amount;

        Save();
    }

    public int GetItemIndex(
        int slot)
    {
        Ensure();

        if (slot < 0 ||
            slot >= BANK_SLOTS)
            return 0;

        return data.itemIndex[slot];
    }

    public int GetAmount(
        int slot)
    {
        Ensure();

        if (slot < 0 ||
            slot >= BANK_SLOTS)
            return 0;

        return data.amount[slot];
    }

    public bool DepositItem(
        int inventorySlot,
        int amount,
        out string message)
    {
        FindReferences();
        Ensure();

        message = "";

        if (inventory == null)
        {
            message =
                "Inventario no disponible.";
            return false;
        }

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
                "No hay objeto en ese slot.";
            return false;
        }

        amount =
            Mathf.Clamp(
                amount,
                1,
                available);

        if (inventory
            .IsSlotEquippedPublic(
                inventorySlot))
        {
            message =
                "Quitá el objeto antes de depositarlo.";
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

        if (item.newbie)
        {
            message =
                "Los objetos newbie no pueden depositarse.";
            return false;
        }

        int target =
            FindBankSlot(
                itemIndex,
                amount);

        if (target < 0)
        {
            message =
                "No hay espacio en el banco.";
            return false;
        }

        if (!inventory
            .RemoveItemAtPublic(
                inventorySlot,
                amount))
        {
            message =
                "No pude retirar el objeto del inventario.";
            return false;
        }

        if (data.itemIndex[target] == 0)
            data.itemIndex[target] =
                itemIndex;

        data.amount[target] +=
            amount;

        Save();

        message =
            "Depositaste " +
            item.name +
            " x" +
            amount +
            ".";

        return true;
    }

    public bool WithdrawItem(
        int bankSlot,
        int amount,
        out string message)
    {
        FindReferences();
        Ensure();

        message = "";

        if (inventory == null)
        {
            message =
                "Inventario no disponible.";
            return false;
        }

        if (bankSlot < 0 ||
            bankSlot >= BANK_SLOTS)
        {
            message =
                "Slot inválido.";
            return false;
        }

        int itemIndex =
            data.itemIndex[bankSlot];

        int available =
            data.amount[bankSlot];

        if (itemIndex <= 0 ||
            available <= 0)
        {
            message =
                "Ese slot del banco está vacío.";
            return false;
        }

        amount =
            Mathf.Clamp(
                amount,
                1,
                available);

        if (!inventory.AddItem(
                itemIndex,
                amount))
        {
            message =
                "No hay espacio en tu inventario.";
            return false;
        }

        data.amount[bankSlot] -=
            amount;

        if (data.amount[bankSlot] <= 0)
        {
            data.amount[bankSlot] = 0;
            data.itemIndex[bankSlot] = 0;
        }

        Save();

        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                itemIndex);

        message =
            "Retiraste " +
            (item == null
                ? "OBJ " +
                  itemIndex
                : item.name) +
            " x" +
            amount +
            ".";

        return true;
    }

    public bool DepositGold(
        long amount,
        out string message)
    {
        FindReferences();
        Ensure();

        message = "";

        if (combat == null)
        {
            message =
                "Oro no disponible.";
            return false;
        }

        if (amount <= 0)
        {
            message =
                "Cantidad inválida.";
            return false;
        }

        amount =
            Math.Min(
                amount,
                combat.Gold);

        if (amount <= 0 ||
            !combat.SpendGold(
                amount))
        {
            message =
                "No tenés suficiente oro.";
            return false;
        }

        data.gold += amount;

        Save();

        message =
            "Depositaste " +
            amount +
            " de oro.";

        return true;
    }

    public bool WithdrawGold(
        long amount,
        out string message)
    {
        FindReferences();
        Ensure();

        message = "";

        if (combat == null)
        {
            message =
                "Oro no disponible.";
            return false;
        }

        if (amount <= 0)
        {
            message =
                "Cantidad inválida.";
            return false;
        }

        amount =
            Math.Min(
                amount,
                data.gold);

        if (amount <= 0)
        {
            message =
                "No hay oro depositado.";
            return false;
        }

        data.gold -= amount;

        combat.AddGold(
            amount);

        Save();

        message =
            "Retiraste " +
            amount +
            " de oro.";

        return true;
    }

    int FindBankSlot(
        int itemIndex,
        int amount)
    {
        for (int i = 0;
             i < BANK_SLOTS;
             i++)
        {
            if (data.itemIndex[i] ==
                    itemIndex &&
                data.amount[i] +
                    amount <=
                    MAX_STACK)
            {
                return i;
            }
        }

        for (int i = 0;
             i < BANK_SLOTS;
             i++)
        {
            if (data.itemIndex[i] == 0)
                return i;
        }

        return -1;
    }

    public void Save()
    {
        if (AOOnlineClientV240.ProtectLocalSave) return;
        try
        {
            Ensure();

            File.WriteAllText(
                SavePath,
                JsonUtility.ToJson(
                    data,
                    true));
        }
        catch (
            Exception e)
        {
            Debug.LogWarning(
                "[AO v0.13] Bank save: " +
                e.Message);
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(
                    SavePath))
            {
                data =
                    new BankSave();
                return;
            }

            data =
                JsonUtility.FromJson
                    <BankSave>(
                        File.ReadAllText(
                            SavePath));

            Ensure();
        }
        catch (
            Exception e)
        {
            Debug.LogWarning(
                "[AO v0.13] Bank load: " +
                e.Message);

            data =
                new BankSave();
        }
    }

    void OnApplicationQuit()
    {
        Save();
    }
}
