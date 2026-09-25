using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class AOInventoryV10 : MonoBehaviour
{
    public const int BASIC_SLOTS = 24;
    public const int MAX_SLOTS = 42;

    [Serializable]
    public class Slot
    {
        public int itemIndex;
        public int amount;
    }

    [SerializeField]
    Slot[] slots =
        new Slot[BASIC_SLOTS];

    [Header("Equipo")]
    [SerializeField] int weapon;
    [SerializeField] int armor;
    [SerializeField] int shield;
    [SerializeField] int helmet;
    [SerializeField] int amulet;
    [SerializeField] int magicAccessory;
    [SerializeField] int munition;          // arrows/bullets (objType 32): separate slot, like the original
    AOCharacterRenderer character;

    bool showInventory;
    int selectedSlot = -1;
    float nextPotionUseAt;
    string message = "";
    float messageUntil;

    public int EquippedWeapon => weapon;
    public int EquippedArmor => armor;
    public int EquippedShield => shield;
    public int EquippedHelmet => helmet;
    public int EquippedAmulet => amulet;
    public int EquippedMagicAccessory => magicAccessory;
    public int EquippedMunition => munition;
    public int SlotCount
    {
        get
        {
            EnsureSlots();
            return slots.Length;
        }
    }

    public int SelectedSlot => selectedSlot;

    public int GetSlotItemIndex(int index)
    {
        EnsureSlots();

        if (index < 0 ||
            index >= slots.Length)
            return 0;

        return slots[index].itemIndex;
    }

    public int GetSlotAmount(int index)
    {
        EnsureSlots();

        if (index < 0 ||
            index >= slots.Length)
            return 0;

        return slots[index].amount;
    }

    public void SelectSlotPublic(int index)
    {
        EnsureSlots();

        selectedSlot =
            index >= 0 &&
            index < slots.Length
            ? index
            : -1;
    }

    public bool MoveSlotPublic(
        int from,
        int to)
    {
        EnsureSlots();

        if (from < 0 ||
            to < 0 ||
            from >= slots.Length ||
            to >= slots.Length ||
            from == to)
            return false;

        Slot source =
            slots[from];

        Slot target =
            slots[to];

        if (source == null ||
            source.itemIndex <= 0 ||
            source.amount <= 0)
            return false;

        if (target == null)
        {
            target = new Slot();
            slots[to] = target;
        }

        if (target.itemIndex == 0)
        {
            target.itemIndex =
                source.itemIndex;

            target.amount =
                source.amount;

            source.itemIndex = 0;
            source.amount = 0;
        }
        else if (
            target.itemIndex ==
            source.itemIndex)
        {
            target.amount +=
                source.amount;

            source.itemIndex = 0;
            source.amount = 0;
        }
        else
        {
            int oldItem =
                target.itemIndex;

            int oldAmount =
                target.amount;

            target.itemIndex =
                source.itemIndex;

            target.amount =
                source.amount;

            source.itemIndex =
                oldItem;

            source.amount =
                oldAmount;
        }

        selectedSlot = to;
        return true;
    }

    public AOItemDatabaseV10.ItemDef
        GetSelectedItem()
    {
        int id =
            GetSlotItemIndex(
                selectedSlot);

        return id > 0
            ? AOItemDatabaseV10.Get(id)
            : null;
    }

    public bool IsEquippedPublic(
        int itemIndex)
    {
        return IsEquipped(itemIndex);
    }

    public bool UseConsumableById(int itemId)
    {
        if (AOInterfaceV0101.InputCaptured || AOOnlineClientV240.InputBlocked) return false;
        var item = AOItemDatabaseV10.Get(itemId);
        if (item == null || !item.Consumable) return false;
        for (int i = 0; i < slots.Length; i++) if (slots[i] != null && slots[i].itemIndex == itemId && slots[i].amount > 0)
        {
            int previous = selectedSlot; selectedSlot = i;
            // AO original (use key + UseInvItem): a successful use ends meditation.
            try { bool used = TryUseSelectedConsumable(); if (used) GetComponent<AOPlayerMagicV120>()?.InterruptMeditation(); return used; }
            finally { selectedSlot = previous; AOOnlineClientV240.Checkpoint(false); }
        }
        AOInterfaceV0101.PushMessage("No te queda " + item.name + "."); return false;
    }

    public void UseOrToggleSelected()
    {
        AOPlayerCombatV09 combatState =
            GetComponent<AOPlayerCombatV09>();

        if (combatState != null &&
            combatState.IsDead)
        {
            Flash(
                "Estás muerto. No podés usar ni equipar objetos.");
            return;
        }

        // AO original (UserItemClick): mouse use is ignored while meditating.
        AOPlayerMagicV120 magicState =
            GetComponent<AOPlayerMagicV120>();

        if (magicState != null &&
            magicState.IsMeditating)
            return;

        AOItemDatabaseV10.ItemDef item =
            GetSelectedItem();

        if (item == null)
            return;

        if (item.IsParchment)
        {
            TryLearnSelectedSpell();
            return;
        }

        // AO original: arrows/bullets go to their own slot (EquipItem); "Usar" on the equipped
        // projectile weapon asks for a target (WorkRequestTarget Proyectiles).
        if (item.objType == ObjTypeMunition)
        {
            ToggleSelectedMunition();
            return;
        }

        if (item.projectile > 0 &&
            item.index == weapon)
        {
            GetComponent<AOPlayerCombatV09>()?.BeginRangedTargeting();
            return;
        }

        if (item.Equipable)
        {
            ToggleSelectedEquipment();
            return;
        }

        TryUseSelectedConsumable();
    }

    public const int ObjTypeMunition = 32;

    public AOItemDatabaseV10.ItemDef GetMunition() => AOItemDatabaseV10.Get(munition);

    void ToggleSelectedMunition()
    {
        AOItemDatabaseV10.ItemDef item = GetSelectedItem();
        if (item == null || item.objType != ObjTypeMunition)
            return;

        if (munition == item.index)
        {
            munition = 0;
            Flash("Municiones desequipadas: " + item.name);
            return;
        }

        munition = item.index;
        Flash("Municiones equipadas: " + item.name);
    }

    public void UnequipMunition() => munition = 0;

    // One shot = one unit, taken from the equipped stack (RemoveItemByIndexPublic skips equipped items).
    public bool ConsumeMunition()
    {
        EnsureSlots();
        if (munition <= 0)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.itemIndex != munition || slot.amount <= 0)
                continue;

            slot.amount--;
            if (slot.amount <= 0)
            {
                slot.itemIndex = 0;
                slot.amount = 0;
                if (selectedSlot == i)
                    selectedSlot = -1;
            }

            if (CountItem(munition) <= 0)
                munition = 0;
            return true;
        }

        munition = 0;
        return false;
    }

    public void ToggleSelectedEquipment()
    {
        AOItemDatabaseV10.ItemDef item =
            GetSelectedItem();

        if (item == null ||
            !item.Equipable)
            return;

        if (IsEquipped(item.index))
            UnequipSelectedType();
        else
            EquipSelected();
    }

    void Awake()
    {
        EnsureSlots();

        character =
            GetComponentInChildren
                <AOCharacterRenderer>(true);
    }

    void Start()
    {
        RefreshVisualEquipment();
    }

    void Update()
    {
        if (AOInterfaceV0101.InputCaptured)
            return;

        if (PressedInventory())
            showInventory =
                !showInventory;

    }

    void EnsureSlots()
    {
        if (slots == null ||
            slots.Length != BASIC_SLOTS)
        {
            Slot[] old = slots;

            slots =
                new Slot[BASIC_SLOTS];

            if (old != null)
            {
                int count =
                    Mathf.Min(
                        old.Length,
                        slots.Length);

                for (int i = 0;
                     i < count; i++)
                {
                    slots[i] = old[i];
                }
            }
        }

        for (int i = 0;
             i < slots.Length; i++)
        {
            if (slots[i] == null)
                slots[i] = new Slot();
        }
    }

    public bool AddItem(
        int itemIndex,
        int amount)
    {
        EnsureSlots();

        if (itemIndex <= 0 ||
            amount <= 0)
            return false;

        for (int i = 0;
             i < slots.Length; i++)
        {
            if (slots[i].itemIndex ==
                itemIndex)
            {
                slots[i].amount +=
                    amount;

                return true;
            }
        }

        for (int i = 0;
             i < slots.Length; i++)
        {
            if (slots[i].itemIndex == 0)
            {
                slots[i].itemIndex =
                    itemIndex;

                slots[i].amount =
                    amount;

                return true;
            }
        }

        Flash("Inventario lleno.");
        return false;
    }

    public bool RemoveItemAtPublic(
        int slotIndex,
        int amount)
    {
        EnsureSlots();

        if (slotIndex < 0 ||
            slotIndex >= slots.Length ||
            amount <= 0)
            return false;

        Slot slot =
            slots[slotIndex];

        if (slot == null ||
            slot.itemIndex <= 0 ||
            slot.amount < amount)
            return false;

        if (IsEquipped(
                slot.itemIndex))
        {
            Flash(
                "Quitá el objeto equipado antes de moverlo o venderlo.");
            return false;
        }

        slot.amount -= amount;

        if (slot.amount <= 0)
        {
            slot.itemIndex = 0;
            slot.amount = 0;

            if (selectedSlot ==
                slotIndex)
                selectedSlot = -1;
        }

        return true;
    }

    public bool IsSlotEquippedPublic(
        int slotIndex)
    {
        int itemIndex =
            GetSlotItemIndex(
                slotIndex);

        return IsEquipped(
            itemIndex);
    }

    public void RestoreSaveState(
        int[] itemIndices,
        int[] amounts,
        int savedWeapon,
        int savedArmor,
        int savedShield,
        int savedHelmet,
        int savedAmulet,
        int savedMagicAccessory,
        int savedMunition = 0)
    {
        EnsureSlots();

        for (int i = 0;
             i < slots.Length;
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
                ? Mathf.Max(
                    0,
                    amounts[i])
                : 0;

            if (amount <= 0)
                itemIndex = 0;

            slots[i].itemIndex =
                itemIndex;

            slots[i].amount =
                itemIndex > 0
                ? amount
                : 0;
        }

        weapon =
            CountItem(savedWeapon) > 0
            ? savedWeapon
            : 0;

        armor =
            CountItem(savedArmor) > 0
            ? savedArmor
            : 0;

        shield =
            CountItem(savedShield) > 0
            ? savedShield
            : 0;

        helmet =
            CountItem(savedHelmet) > 0
            ? savedHelmet
            : 0;

        amulet =
            CountItem(savedAmulet) > 0
            ? savedAmulet
            : 0;

        magicAccessory =
            CountItem(
                savedMagicAccessory) > 0
            ? savedMagicAccessory
            : 0;

        munition =
            CountItem(savedMunition) > 0
            ? savedMunition
            : 0;

        selectedSlot = -1;

        ValidateEquipmentForRPG();
        RefreshVisualEquipment();
    }

    public void ClearForNewGame()
    {
        EnsureSlots();

        for (int i = 0;
             i < slots.Length;
             i++)
        {
            slots[i].itemIndex = 0;
            slots[i].amount = 0;
        }

        weapon = 0;
        armor = 0;
        shield = 0;
        helmet = 0;
        amulet = 0;
        magicAccessory = 0;
        munition = 0;

        selectedSlot = -1;


        RefreshVisualEquipment();
    }

    public int FreeSlotCountPublic()
    {
        EnsureSlots();

        int count = 0;

        foreach (Slot slot in slots)
        {
            if (slot == null ||
                slot.itemIndex <= 0 ||
                slot.amount <= 0)
            {
                count++;
            }
        }

        return count;
    }

    public bool RemoveItemByIndexPublic(
        int itemIndex,
        int amount)
    {
        EnsureSlots();

        if (itemIndex <= 0 ||
            amount <= 0 ||
            CountItem(itemIndex) < amount)
            return false;

        int remaining =
            amount;

        for (int i = 0;
             i < slots.Length &&
             remaining > 0;
             i++)
        {
            Slot slot =
                slots[i];

            if (slot == null ||
                slot.itemIndex !=
                    itemIndex ||
                slot.amount <= 0)
                continue;

            if (IsEquipped(
                    slot.itemIndex))
            {
                continue;
            }

            int take =
                Mathf.Min(
                    remaining,
                    slot.amount);

            slot.amount -=
                take;

            remaining -=
                take;

            if (slot.amount <= 0)
            {
                slot.itemIndex = 0;
                slot.amount = 0;

                if (selectedSlot == i)
                    selectedSlot = -1;
            }
        }

        return remaining <= 0;
    }

    public void UnequipAllForDeath()
    {
        weapon = 0;
        armor = 0;
        shield = 0;
        helmet = 0;
        amulet = 0;
        magicAccessory = 0;
        munition = 0;

        selectedSlot = -1;
    }

    public int CountItem(
        int itemIndex)
    {
        int total = 0;
        EnsureSlots();

        foreach (Slot slot in slots)
        {
            if (slot.itemIndex ==
                itemIndex)
            {
                total += slot.amount;
            }
        }

        return total;
    }

    public AOItemDatabaseV10.ItemDef
        GetWeapon()
    {
        return AOItemDatabaseV10.Get(
            weapon);
    }

    public AOItemDatabaseV10.ItemDef
        GetArmor()
    {
        return AOItemDatabaseV10.Get(
            armor);
    }

    public AOItemDatabaseV10.ItemDef
        GetShield()
    {
        return AOItemDatabaseV10.Get(
            shield);
    }

    public AOItemDatabaseV10.ItemDef
        GetHelmet()
    {
        return AOItemDatabaseV10.Get(
            helmet);
    }

    public AOItemDatabaseV10.ItemDef
        GetAmulet()
    {
        return AOItemDatabaseV10.Get(
            amulet);
    }

    public AOItemDatabaseV10.ItemDef
        GetMagicAccessory()
    {
        return AOItemDatabaseV10.Get(
            magicAccessory);
    }

    public bool HasItem(
        int itemIndex)
    {
        return CountItem(
            itemIndex) > 0;
    }

    public int RollDamageAgainstNpc(
        int baseMinHit,
        int baseMaxHit,
        int strength,
        float classModifier,
        bool ranged = false)
    {
        int userDamage =
            UnityEngine.Random.Range(
                Mathf.Max(
                    0,
                    baseMinHit),
                Mathf.Max(
                    baseMinHit,
                    baseMaxHit) + 1);

        AOItemDatabaseV10.ItemDef w =
            GetWeapon();

        int weaponDamage = 0;
        int maxWeaponDamage = 0;

        if (w != null)
        {
            int min =
                w.minHitToNpc > 0
                ? w.minHitToNpc
                : w.minHit;

            int max =
                w.maxHitToNpc > 0
                ? w.maxHitToNpc
                : w.maxHit;

            max =
                Mathf.Max(
                    min,
                    max);

            weaponDamage =
                UnityEngine.Random.Range(
                    Mathf.Max(0, min),
                    Mathf.Max(0, max) + 1);

            maxWeaponDamage =
                Mathf.Max(0, max);

            // Projectile weapon: the equipped ammunition of its subtype adds its damage (UserDamageToNpc).
            AOItemDatabaseV10.ItemDef ammo = ranged && w.munition > 0 ? GetMunition() : null;
            if (ammo != null && ammo.subType == w.munition)
            {
                weaponDamage += UnityEngine.Random.Range(Mathf.Max(0, ammo.minHit), Mathf.Max(ammo.minHit, ammo.maxHit) + 1);
                maxWeaponDamage += Mathf.Max(0, ammo.maxHit);
            }
        }

        float modifier =
            Mathf.Max(
                0.05f,
                classModifier);

        float raw =
            (3f * weaponDamage +
             maxWeaponDamage *
             0.2f *
             Mathf.Max(
                 0,
                 strength - 15) +
             userDamage) *
            modifier;

        return Mathf.Max(
            0,
            Mathf.RoundToInt(raw));
    }

    public int RollDefenseAbsorption()
    {
        bool headHit =
            UnityEngine.Random.Range(
                0,
                4) == 0;

        int absorbed = 0;

        if (headHit)
        {
            absorbed +=
                RollDefense(
                    GetHelmet());
        }
        else
        {
            absorbed +=
                RollDefense(
                    GetArmor());

            absorbed +=
                RollDefense(
                    GetShield());
        }

        return absorbed;
    }

    static int RollDefense(
        AOItemDatabaseV10.ItemDef item)
    {
        if (item == null)
            return 0;

        int min =
            Mathf.Max(
                0,
                item.minDef);

        int max =
            Mathf.Max(
                min,
                item.maxDef);

        return UnityEngine.Random.Range(
            min,
            max + 1);
    }

    public int AverageDefense()
    {
        int result = 0;

        AOItemDatabaseV10.ItemDef a =
            GetArmor();

        AOItemDatabaseV10.ItemDef s =
            GetShield();

        AOItemDatabaseV10.ItemDef h =
            GetHelmet();

        if (a != null)
            result +=
                (a.minDef +
                 a.maxDef) / 2;

        if (s != null)
            result +=
                (s.minDef +
                 s.maxDef) / 2;

        if (h != null)
            result +=
                (h.minDef +
                 h.maxDef) / 8;

        return result;
    }

    bool TryLearnSelectedSpell()
    {
        if (selectedSlot < 0 ||
            selectedSlot >= slots.Length)
            return false;

        Slot slot =
            slots[selectedSlot];

        if (slot == null ||
            slot.itemIndex <= 0 ||
            slot.amount <= 0)
            return false;

        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                slot.itemIndex);

        if (item == null ||
            !item.IsParchment)
            return false;

        AOPlayerCombatV09 combat =
            GetComponent<AOPlayerCombatV09>();

        if (combat != null &&
            combat.IsDead)
        {
            Flash(
                "No podés leer un pergamino muerto.");
            return false;
        }

        AOPlayerRPGV11 rpg =
            GetComponent<AOPlayerRPGV11>();

        if (rpg == null)
        {
            Flash(
                "No encuentro el perfil RPG.");
            return false;
        }

        if (rpg.Hunger <= 0 ||
            rpg.Thirst <= 0)
        {
            Flash(
                "Estás demasiado hambriento o sediento para comprender el manuscrito.");
            return false;
        }

        if (!rpg.CanEquip(
                item,
                out string restriction))
        {
            Flash(
                "No podés comprender el pergamino: " +
                restriction);
            return false;
        }

        AOPlayerMagicV120 magic =
            GetComponent<AOPlayerMagicV120>();

        if (magic == null)
        {
            Flash(
                "No encuentro el sistema de magia v0.12.");
            return false;
        }

        AOPlayerMagicV120.LearnResult result =
            magic.TryLearnSpell(
                item.spellIndex);

        if (result ==
            AOPlayerMagicV120.LearnResult.Learned)
        {
            ConsumeSelectedOne();

            Flash(
                "Aprendiste " +
                AOSpellDatabaseV120.Get(
                    item.spellIndex).name +
                ".");

            return true;
        }

        if (result ==
            AOPlayerMagicV120.LearnResult.AlreadyKnown)
        {
            Flash(
                "Ya conocés ese hechizo.");
        }
        else if (
            result ==
            AOPlayerMagicV120.LearnResult.Full)
        {
            Flash(
                "No tenés espacio para más hechizos (40/40).");
        }
        else
        {
            Flash(
                "El pergamino no contiene un hechizo válido.");
        }

        return false;
    }

    bool TryUseSelectedConsumable()
    {
        // Duel: potions are used on the server (duel HP and the potion limit are its business); it sends "remove".
        if (AODuelUI.InDuel &&
            selectedSlot >= 0 &&
            selectedSlot < slots.Length &&
            slots[selectedSlot] != null &&
            slots[selectedSlot].amount > 0)
            return AOOnlineClientV240.UseInDuel(slots[selectedSlot].itemIndex);

        if (selectedSlot < 0 ||
            selectedSlot >= slots.Length)
            return false;

        Slot slot =
            slots[selectedSlot];

        if (slot == null ||
            slot.itemIndex <= 0 ||
            slot.amount <= 0)
            return false;

        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                slot.itemIndex);

        if (item == null)
        {
            Flash("Objeto desconocido.");
            return false;
        }

        AOPlayerCombatV09 combat =
            GetComponent<AOPlayerCombatV09>();

        AOPlayerRPGV11 rpg =
            GetComponent<AOPlayerRPGV11>();

        if (combat != null &&
            combat.IsDead)
        {
            Flash(
                "No podés usar consumibles muerto.");
            return false;
        }

        if (rpg == null)
        {
            Flash(
                "No encuentro AOPlayerRPGV11.");
            return false;
        }

        if (item.minLevel > 0 &&
            rpg.Level < item.minLevel)
        {
            Flash(
                "Necesitás nivel " +
                item.minLevel +
                " para usar " +
                item.name +
                ".");
            return false;
        }

        if (item.maxLevel > 0 &&
            rpg.Level > item.maxLevel)
        {
            Flash(
                item.name +
                " sólo puede usarse hasta nivel " +
                item.maxLevel +
                ".");
            return false;
        }

        bool used = false;
        string result = "";

        switch (item.objType)
        {
            case 1:
            {
                if (item.minHunger <= 0)
                {
                    Flash(
                        item.name +
                        " todavía no tiene uso local.");
                    return false;
                }

                int gained =
                    rpg.AddHunger(
                        item.minHunger);

                used = true;

                result =
                    "Comiste " +
                    item.name +
                    " (+" +
                    gained +
                    " hambre).";

                AOConsumableFeedbackV114
                    .Used(
                        character,
                        true,
                        item.sound1);

                break;
            }

            case 13:
            {
                if (item.minThirst <= 0)
                    return false;

                int gained =
                    rpg.AddThirst(
                        item.minThirst);

                used = true;

                result =
                    "Bebiste " +
                    item.name +
                    " (+" +
                    gained +
                    " sed).";

                AOConsumableFeedbackV114
                    .Used(
                        character,
                        false,
                        item.sound1);

                break;
            }

            case 34:
            {
                if (item.minThirst <= 0)
                    return false;

                int gained =
                    rpg.AddThirst(
                        item.minThirst);

                used = true;

                result =
                    "Bebiste " +
                    item.name +
                    " (+" +
                    gained +
                    " sed).";

                AOConsumableFeedbackV114
                    .Used(
                        character,
                        false,
                        item.sound1);

                break;
            }

            case 11:
            {
                if (Time.time <
                    nextPotionUseAt)
                {
                    Flash(
                        "Debés esperar antes de tomar otra poción.");
                    return false;
                }

                switch (item.potionType)
                {
                    case 1:
                    {
                        int increase =
                            rpg.ApplyAttributePotion(
                                1,
                                item.minModifier,
                                item.maxModifier,
                                item.durationEffect);

                        used = true;

                        result =
                            "Agilidad +" +
                            increase +
                            " por " +
                            Mathf.Max(
                                1,
                                item.durationEffect) +
                            " s.";

                        break;
                    }

                    case 2:
                    {
                        int increase =
                            rpg.ApplyAttributePotion(
                                2,
                                item.minModifier,
                                item.maxModifier,
                                item.durationEffect);

                        used = true;

                        result =
                            "Fuerza +" +
                            increase +
                            " por " +
                            Mathf.Max(
                                1,
                                item.durationEffect) +
                            " s.";

                        break;
                    }

                    case 3:
                    {
                        if (combat == null)
                        {
                            Flash(
                                "No encuentro el sistema de HP.");
                            return false;
                        }

                        int lo =
                            Mathf.Min(
                                item.minModifier,
                                item.maxModifier);

                        int hi =
                            Mathf.Max(
                                item.minModifier,
                                item.maxModifier);

                        int amount =
                            UnityEngine.Random.Range(
                                Mathf.Max(
                                    0,
                                    lo),
                                Mathf.Max(
                                    0,
                                    hi) + 1);

                        int restored =
                            combat.RestoreHealth(
                                amount);

                        used = true;

                        result =
                            "Poción de vida: +" +
                            restored +
                            " HP.";

                        break;
                    }

                    case 4:
                    {
                        int restored =
                            rpg.RestoreManaPercent(
                                item.percent);

                        used = true;

                        result =
                            "Poción de maná: +" +
                            restored +
                            " MP.";

                        break;
                    }

                    case 7:
                    {
                        int restored =
                            rpg.RestoreStaminaRange(
                                item.minModifier,
                                item.maxModifier);

                        used = true;

                        result =
                            "Poción de energía: +" +
                            restored +
                            " STA.";

                        break;
                    }

                    case 5:
                    case 6:
                        Flash(
                            "Esta poción depende del sistema de estados " +
                            "(veneno/parálisis), que llega con hechizos/estados.");
                        return false;

                    default:
                        Flash(
                            item.name +
                            " todavía no está soportado en v0.11.4.");
                        return false;
                }

                if (used)
                {
                    nextPotionUseAt =
                        Time.time + 0.8f;

                    AOConsumableFeedbackV114
                        .Used(
                            character,
                            false,
                            item.sound1);
                }

                break;
            }

            default:
                Flash(
                    item.name +
                    " todavía no tiene acción local.");
                return false;
        }

        if (!used)
            return false;

        int replacement = 0;

        if (item.objType == 34)
            replacement =
                item.closedBottleIndex;

        ConsumeSelectedOne();

        if (replacement > 0)
        {
            AddItem(
                replacement,
                1);
        }

        Flash(result);
        return true;
    }

    void ConsumeSelectedOne()
    {
        if (selectedSlot < 0 ||
            selectedSlot >= slots.Length)
            return;

        Slot slot =
            slots[selectedSlot];

        if (slot == null ||
            slot.amount <= 0)
            return;

        slot.amount--;

        if (slot.amount <= 0)
        {
            slot.itemIndex = 0;
            slot.amount = 0;
        }
    }

    void EquipSelected()
    {
        if (selectedSlot < 0 ||
            selectedSlot >= slots.Length)
            return;

        Slot slot =
            slots[selectedSlot];

        if (slot.itemIndex <= 0)
            return;

        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                slot.itemIndex);

        if (item == null)
        {
            Flash("Objeto desconocido.");
            return;
        }

        AOPlayerRPGV11 rpg =
            GetComponent<AOPlayerRPGV11>();

        if (rpg != null &&
            !rpg.CanEquip(
                item,
                out string restriction))
        {
            Flash(restriction);
            return;
        }

        switch (item.objType)
        {
            case 2:
                weapon = item.index;

                if (item.twoHands)
                    shield = 0;

                Flash(
                    "Arma equipada: " +
                    item.name);
                break;

            case 3:
                armor = item.index;

                Flash(
                    "Armadura equipada: " +
                    item.name);
                break;

            case 16:
                AOItemDatabaseV10.ItemDef
                    currentWeapon =
                        GetWeapon();

                if (currentWeapon != null &&
                    currentWeapon.twoHands)
                {
                    Flash(
                        "No podés usar escudo " +
                        "con un arma a dos manos.");
                    return;
                }

                shield = item.index;

                Flash(
                    "Escudo equipado: " +
                    item.name);
                break;

            case 17:
                helmet = item.index;

                Flash(
                    "Casco equipado: " +
                    item.name);
                break;

            case 21:
                amulet = item.index;

                Flash(
                    "Amuleto equipado: " +
                    item.name);
                break;

            case 30:
            case 35:
                magicAccessory =
                    item.index;

                Flash(
                    "Accesorio mágico equipado: " +
                    item.name);
                break;

            default:
                Flash(
                    item.name +
                    " todavía no se equipa " +
                    "en v0.10.");
                return;
        }

        RefreshVisualEquipment();

        AOCombatFeedbackV113.PlayEquip(
            character,
            item.objType);
    }

    void UnequipSelectedType()
    {
        if (selectedSlot < 0 ||
            selectedSlot >= slots.Length)
            return;

        int id =
            slots[selectedSlot].itemIndex;

        if (id == weapon)
            weapon = 0;

        if (id == armor)
            armor = 0;

        if (id == shield)
            shield = 0;

        if (id == helmet)
            helmet = 0;

        if (id == amulet)
            amulet = 0;

        if (id == magicAccessory)
            magicAccessory = 0;

        if (id == munition)
            munition = 0;

        RefreshVisualEquipment();
    }

    public void ValidateEquipmentForRPG()
    {
        AOPlayerRPGV11 rpg =
            GetComponent<AOPlayerRPGV11>();

        if (rpg == null)
            return;

        if (!CanRemainEquipped(
                rpg,
                weapon))
            weapon = 0;

        if (!CanRemainEquipped(
                rpg,
                armor))
            armor = 0;

        if (!CanRemainEquipped(
                rpg,
                shield))
            shield = 0;

        if (!CanRemainEquipped(
                rpg,
                helmet))
            helmet = 0;

        if (!CanRemainEquipped(
                rpg,
                amulet))
            amulet = 0;

        if (!CanRemainEquipped(
                rpg,
                magicAccessory))
            magicAccessory = 0;

        RefreshVisualEquipment();
    }

    bool CanRemainEquipped(
        AOPlayerRPGV11 rpg,
        int itemIndex)
    {
        if (itemIndex <= 0)
            return true;

        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(
                itemIndex);

        return item != null &&
            rpg.CanEquip(
                item,
                out _);
    }

    public void RefreshVisualEquipment()
    {
        AOCharacterProfileVisualV111 profile =
            GetComponent
                <AOCharacterProfileVisualV111>();

        if (profile == null)
        {
            profile =
                GetComponentInChildren
                    <AOCharacterProfileVisualV111>(true);
        }

        if (profile != null &&
            profile.DeadVisual)
        {
            return;
        }

        if (character == null)
        {
            character =
                GetComponentInChildren
                    <AOCharacterRenderer>(true);
        }

        if (character == null)
            return;

        AOItemDatabaseV10.ItemDef
            armorItem =
                GetArmor();

        AOPlayerRPGV11 rpg =
            GetComponent<AOPlayerRPGV11>();

        var visuals =
            AOItemDatabaseV10
                .BuildEquipmentVisuals(
                    null,
                    GetHelmet(),
                    GetWeapon(),
                    GetShield());

        bool bodyOverride = false;
        float headX = 0f;
        float headY = 0f;
        float bodyX = 0f;

        if (armorItem != null)
        {
            int bodyId = 0;

            if (rpg != null)
            {
                bodyId =
                    armorItem
                        .BodyForProfile(
                            rpg.RaceId,
                            rpg.GenderId);
            }

            if (bodyId > 0 &&
                AOCharacterVisualDatabaseV111
                    .TryBuildArmorBody(
                        bodyId,
                        out AOCharacterRenderer.DirectionVisual[]
                            armorVisuals,
                        out headX,
                        out headY,
                        out bodyX))
            {
                for (int i = 0;
                     i < visuals.Length &&
                     i < armorVisuals.Length;
                     i++)
                {
                    visuals[i].body =
                        armorVisuals[i].body;
                }

                bodyOverride = true;
            }
            else
            {
                // Fallback v0.10 para proyectos sin DB v0.11.1.
                var legacy =
                    AOItemDatabaseV10
                        .BuildEquipmentVisuals(
                            armorItem,
                            null,
                            null,
                            null);

                for (int i = 0;
                     i < visuals.Length &&
                     i < legacy.Length;
                     i++)
                {
                    visuals[i].body =
                        legacy[i].body;
                }

                bodyOverride =
                    armorItem.hasBodyOverride;

                if (bodyOverride)
                {
                    headX =
                        armorItem.headOffsetX /
                        32f;

                    headY =
                        -armorItem.headOffsetY /
                        32f;

                    bodyX =
                        armorItem.bodyShiftX /
                        32f;
                }
            }
        }

        character.ConfigureEquipment(
            visuals,
            bodyOverride,
            headX,
            headY,
            bodyX);

        character.ForceRefreshVisuals();

        AOItemDatabaseV10.ItemDef
            currentWeapon =
                GetWeapon();

        AOItemDatabaseV10.ItemDef
            currentShield =
                GetShield();

        Debug.Log(
            "[AO v0.11.2] Visual equipo actualizado: " +
            "arma=" +
            (currentWeapon == null
                ? "-"
                : currentWeapon.name) +
            " escudo=" +
            (currentShield == null
                ? "-"
                : currentShield.name) +
            " armadura=" +
            (armorItem == null
                ? "-"
                : armorItem.name) +
            " | bodyFrames=" +
            character.CurrentBodyFrameCount +
            " weaponFrames=" +
            character.CurrentWeaponFrameCount +
            " shieldFrames=" +
            character.CurrentShieldFrameCount);
    }

    bool IsEquipped(
        int itemIndex)
    {
        return itemIndex > 0 &&
            (itemIndex == weapon ||
             itemIndex == armor ||
             itemIndex == shield ||
             itemIndex == helmet ||
             itemIndex == amulet ||
             itemIndex == magicAccessory ||
             itemIndex == munition);
    }

    void Flash(
        string text,
        float seconds = 3f)
    {
        message = text;

        messageUntil =
            Time.time +
            seconds;

        Debug.Log(
            "[AO v0.10] " +
            text);
    }

    static string ItemName(
        int index)
    {
        var item =
            AOItemDatabaseV10.Get(
                index);

        return item == null
            ? "-"
            : item.name;
    }

    bool PressedInventory()
    {
        return AOPlayerSettingsV230.Pressed(AOGameAction.Inventory);
    }

    void OnGUI()
    {
        if (AOOnlineClientV240.InputBlocked) return;
        if (AOInterfaceV0101.Active)
            return;
        if (!showInventory)
        {
            GUI.Box(
                new Rect(
                    12,
                    Screen.height - 140,
                    330,
                    24),
                "I: Inventario");

            return;
        }

        EnsureSlots();

        Rect window =
            new Rect(
                20,
                145,
                760,
                520);

        GUI.Box(
            window,
            "Inventario AO v0.10 — 24 slots básicos");

        GUI.Label(
            new Rect(
                window.x + 18,
                window.y + 28,
                500,
                24),
            "Arma [" +
            ItemName(weapon) +
            "]  Armadura [" +
            ItemName(armor) +
            "]");

        GUI.Label(
            new Rect(
                window.x + 18,
                window.y + 50,
                500,
                24),
            "Escudo [" +
            ItemName(shield) +
            "]  Casco [" +
            ItemName(helmet) +
            "]");

        float startX =
            window.x + 18;

        float startY =
            window.y + 86;

        float slotW = 78f;
        float slotH = 72f;

        for (int i = 0;
             i < BASIC_SLOTS; i++)
        {
            int col = i % 6;
            int row = i / 6;

            Rect r =
                new Rect(
                    startX +
                    col * (slotW + 4),
                    startY +
                    row * (slotH + 4),
                    slotW,
                    slotH);

            Slot slot = slots[i];

            if (AOAudioV190.Clicked(GUI.Button(
                    r,
                    i == selectedSlot
                        ? "●"
                        : "")))
            {
                selectedSlot = i;
            }

            if (slot.itemIndex <= 0)
                continue;

            AOItemDatabaseV10.ItemDef item =
                AOItemDatabaseV10.Get(
                    slot.itemIndex);

            Sprite icon =
                AOItemDatabaseV10.Icon(
                    slot.itemIndex);

            if (icon != null)
            {
                Rect ir =
                    new Rect(
                        r.x + 20,
                        r.y + 8,
                        38,
                        38);

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

            GUI.Label(
                new Rect(
                    r.x + 4,
                    r.y + 48,
                    r.width - 8,
                    20),
                (IsEquipped(
                    slot.itemIndex)
                    ? "[E] "
                    : "") +
                (item == null
                    ? "OBJ " +
                      slot.itemIndex
                    : item.name) +
                " x" + slot.amount);
        }

        Rect details =
            new Rect(
                window.x + 520,
                window.y + 86,
                220,
                300);

        GUI.Box(
            details,
            "Detalle");

        if (selectedSlot >= 0 &&
            selectedSlot <
                slots.Length &&
            slots[selectedSlot]
                .itemIndex > 0)
        {
            Slot slot =
                slots[selectedSlot];

            AOItemDatabaseV10.ItemDef item =
                AOItemDatabaseV10.Get(
                    slot.itemIndex);

            if (item != null)
            {
                string text =
                    item.name + "\n" +
                    "ID: " + item.index +
                    " | Tipo: " +
                    item.objType + "\n" +
                    "Cantidad: " +
                    slot.amount + "\n" +
                    "Daño: " +
                    item.minHit + "-" +
                    item.maxHit + "\n" +
                    "DEF: " +
                    item.minDef + "-" +
                    item.maxDef + "\n" +
                    "Valor: " +
                    item.value +
                    (item.twoHands
                        ? "\nDos manos"
                        : "");

                GUI.Label(
                    new Rect(
                        details.x + 12,
                        details.y + 28,
                        details.width - 24,
                        150),
                    text);

                if (item.Equipable)
                {
                    if (AOAudioV190.Clicked(GUI.Button(
                            new Rect(
                                details.x + 12,
                                details.y + 190,
                                details.width - 24,
                                32),
                            IsEquipped(
                                item.index)
                                ? "Quitar"
                                : "Equipar")))
                    {
                        if (IsEquipped(
                                item.index))
                            UnequipSelectedType();
                        else
                            EquipSelected();
                    }
                }
            }
        }

        GUI.Label(
            new Rect(
                window.x + 520,
                window.y + 398,
                220,
                50),
            "DEF media local: " +
            AverageDefense() +
            "\nI: cerrar");

        if (!string.IsNullOrEmpty(
                message) &&
            Time.time <=
                messageUntil)
        {
            GUI.Box(
                new Rect(
                    window.x + 18,
                    window.y + 404,
                    480,
                    56),
                message);
        }
    }
}
