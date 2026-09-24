using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class AOPlayerCombatV09 : MonoBehaviour
{
    [Header("Stats locales de prueba (todavia no son las stats de una clase AO)")]
    [SerializeField] int maxHp = 250;
    [SerializeField] int minHit = 20;
    [SerializeField] int maxHit = 35;
    [SerializeField] int attackPower = 50;
    [SerializeField] int evasionPower = 50;
    [SerializeField] int defense = 5;
    [SerializeField] int strength = 18;
    [SerializeField] float meleeClassModifier = 1f;
    [SerializeField] float attackIntervalSeconds = 0.75f;

    AOTestPlayer player;
    AOInventoryV10 inventoryV10;
    AOPlayerRPGV11 rpgV11;
    AOCharacterRenderer characterVisual;
    int hp;
    long exp;
    long gold;
    bool dead;
    float nextAttackAt;
    string combatText = "";
    float combatTextUntil;
    readonly Dictionary<int, int> inventory =
        new Dictionary<int, int>();

    public int HP => hp;

    public int MaxHP =>
        rpgV11 != null
        ? rpgV11.MaxHP
        : maxHp;

    public int AttackPower
    {
        get
        {
            int value =
                rpgV11 != null
                ? rpgV11.GetAttackPower(
                    inventoryV10)
                : attackPower;

            AOMagicEffectRuntimeV129 effects =
                GetComponent<AOMagicEffectRuntimeV129>();

            return value +
                (effects == null
                    ? 0
                    : effects.HitBonus);
        }
    }

    public int EvasionPower
    {
        get
        {
            int value =
                rpgV11 != null
                ? rpgV11.GetEvasionPower()
                : evasionPower;

            AOMagicEffectRuntimeV129 effects =
                GetComponent<AOMagicEffectRuntimeV129>();

            return value +
                (effects == null
                    ? 0
                    : effects.EvasionBonus);
        }
    }

    public int Defense => defense;
    public bool IsDead => dead;

    public long Exp =>
        rpgV11 != null
        ? rpgV11.Experience
        : exp;

    public long Gold => gold;

    public bool SpendGold(
        long amount)
    {
        if (amount <= 0)
            return true;

        if (gold < amount)
            return false;

        gold -= amount;
        return true;
    }

    public void AddGold(
        long amount,
        bool notify = false)
    {
        if (amount <= 0)
            return;

        gold += amount;

        if (notify)
        {
            Flash(
                "Recibiste " +
                amount +
                " de oro.");
        }
    }

    public void RestoreSaveState(
        int savedHp,
        long savedGold,
        bool savedDead)
    {
        StopAllCoroutines();

        if (characterVisual == null)
        {
            characterVisual =
                GetComponentInChildren
                    <AOCharacterRenderer>(true);
        }

        gold =
            System.Math.Max(
                0L,
                savedGold);

        dead =
            savedDead;

        AODeathRespawnV160 deathSystem =
            GetComponent
                <AODeathRespawnV160>();

        if (dead)
        {
            hp = 0;

            AOCombatFeedbackV113.ResetVisual(
                characterVisual);

            if (deathSystem != null)
                deathSystem.ApplyLoadedDeadState();
            else
                SetGhostVisual(
                    true);
        }
        else
        {
            hp =
                Mathf.Clamp(
                    savedHp,
                    1,
                    MaxHP);

            AOCombatFeedbackV113.ResetVisual(
                characterVisual);

            if (deathSystem != null)
                deathSystem.OnResurrected();
            else
                SetGhostVisual(
                    false);
        }

        AOTestPlayer movement =
            GetComponent
                <AOTestPlayer>();

        if (movement != null)
            movement.enabled = true;
    }

    public void ResetForNewGame()
    {
        RestoreSaveState(
            MaxHP,
            0,
            false);
    }

    public void HealFullyFromPriest()
    {
        if (dead)
        {
            ResurrectFromPriest();
            return;
        }

        hp =
            Mathf.Max(
                1,
                MaxHP);

        AOPlayerMagicStatusV120 status =
            GetComponent
                <AOPlayerMagicStatusV120>();

        if (status != null)
        {
            status.CurePoison();
            status.RemoveParalysis();
        }

        Flash(
            "El sacerdote te ha curado por completo.");
    }

    public void ResurrectFromPriest()
    {
        hp =
            Mathf.Max(
                1,
                MaxHP);

        dead = false;

        if (characterVisual == null)
        {
            characterVisual =
                GetComponentInChildren
                    <AOCharacterRenderer>(true);
        }

        AOCombatFeedbackV113.ResetVisual(
            characterVisual);

        AODeathRespawnV160 deathSystem =
            GetComponent
                <AODeathRespawnV160>();

        if (deathSystem != null)
            deathSystem.OnResurrected();
        else
            SetGhostVisual(
                false);

        AOPlayerMagicStatusV120 status =
            GetComponent
                <AOPlayerMagicStatusV120>();

        if (status != null)
        {
            status.CurePoison();
            status.RemoveParalysis();
            status.RemoveInvisibility();
        }

        AOTestPlayer movement =
            GetComponent<AOTestPlayer>();

        if (movement != null)
            movement.enabled = true;

        Flash(
            "El sacerdote te ha resucitado.");
    }

    public int RestoreHealth(
        int amount)
    {
        if (dead ||
            amount <= 0)
            return 0;

        AOMagicEffectRuntimeV129 effects =
            GetComponent<AOMagicEffectRuntimeV129>();

        if (effects != null)
            amount =
                effects.ApplyIncomingHealing(
                    amount);

        int before = hp;

        hp =
            Mathf.Clamp(
                hp + amount,
                0,
                MaxHP);

        int restored =
            hp - before;

        if (restored > 0)
        {
            Flash(
                "Recuperaste " +
                restored +
                " HP.");
        }

        return restored;
    }

    public bool PayHealthCost(
        int amount)
    {
        amount =
            Mathf.Max(
                0,
                amount);

        if (amount <= 0)
            return true;

        if (dead ||
            hp < amount)
            return false;

        hp =
            Mathf.Max(
                0,
                hp - amount);

        if (hp <= 0)
            StartCoroutine(
                DeathRoutine());

        return true;
    }

#if UNITY_EDITOR // debug only: hidden in player builds
    public void SetHealthForTesting()
    {
        if (dead)
            return;

        hp =
            Mathf.Max(
                1,
                MaxHP / 3);

        Flash(
            "TEST: HP " +
            hp +
            "/" +
            MaxHP +
            ".");
    }
#endif

    public void SyncFromRPG(
        bool refill)
    {
        rpgV11 =
            GetComponent<AOPlayerRPGV11>();

        if (rpgV11 == null)
            return;

        if (refill)
            hp = rpgV11.MaxHP;
        else
            hp =
                Mathf.Clamp(
                    hp,
                    0,
                    rpgV11.MaxHP);
    }

    void Awake()
    {
        player = GetComponent<AOTestPlayer>();
        inventoryV10 =
            GetComponent<AOInventoryV10>();

        rpgV11 =
            GetComponent<AOPlayerRPGV11>();

        characterVisual =
            GetComponentInChildren
                <AOCharacterRenderer>(true);

        hp =
            Mathf.Max(
                1,
                MaxHP);
    }

    void OnEnable()
    {
        if (!dead &&
            hp <= 0)
        {
            hp =
                Mathf.Max(
                    1,
                    MaxHP);
        }
    }

    void Update()
    {
        if (AOOnlineClientV240.InputBlocked) return;
        if (player == null)
            player = GetComponent<AOTestPlayer>();

        if (dead || player == null)
            return;

        if (AOInterfaceV0101.InputCaptured)
            return;

        if (PressedAttack())
            TryAttack();

        if (PressedPickup())
            TryPickup();
    }

    public void AttackFromControls() { if (!dead && !AOInterfaceV0101.InputCaptured && !AOOnlineClientV240.InputBlocked) TryAttack(); }
    public void PickupFromControls() { if (!dead && !AOInterfaceV0101.InputCaptured && !AOOnlineClientV240.InputBlocked) TryPickup(); }

    void TryAttack()
    {
        AOPlayerMagicStatusV120 magicStatus =
            GetComponent<AOPlayerMagicStatusV120>();

        if (magicStatus != null &&
            magicStatus.IsParalyzed)
        {
            Flash("Estás paralizado.");
            return;
        }

        // AO original (HandleAttack): attacking ends meditation, then the attack goes on.
        AOPlayerMagicV120 magic = GetComponent<AOPlayerMagicV120>();
        if (magic != null)
            magic.InterruptMeditation();

        if (Time.time < nextAttackAt)
            return;

        if (rpgV11 == null)
            rpgV11 =
                GetComponent<AOPlayerRPGV11>();

        if (rpgV11 != null &&
            !rpgV11.TrySpendStamina(
                UnityEngine.Random.Range(
                    1,
                    11)))
        {
            Flash("Estás muy cansado.");
            return;
        }

        nextAttackAt =
            Time.time +
            Mathf.Max(0.15f, attackIntervalSeconds);

        if (characterVisual == null)
        {
            characterVisual =
                GetComponentInChildren
                    <AOCharacterRenderer>(true);
        }

        AOItemDatabaseV10.ItemDef
            equippedWeapon =
                inventoryV10 == null
                ? null
                : inventoryV10.GetWeapon();

        AOCombatFeedbackV113.PlayAttack(
            characterVisual,
            player.Heading,
            equippedWeapon == null
                ? 0
                : equippedWeapon.weaponType);

        int x = player.TileX;
        int y = player.TileY;

        if (player.Heading == AOGridMap.NORTH) y--;
        else if (player.Heading == AOGridMap.EAST) x++;
        else if (player.Heading == AOGridMap.SOUTH) y++;
        else if (player.Heading == AOGridMap.WEST) x--;

        AOInteractable interactable =
            AOInteractionRegistry.FindFirst(x, y);

        if (interactable == null)
        {
            AOCombatFeedbackV113.PlayMiss(
                transform.position +
                HeadingWorldOffset(
                    player.Heading));

            Flash("Golpeas al aire.");
            return;
        }

        AONPCCombatV09 target =
            interactable.GetComponent<AONPCCombatV09>();

        if (target == null || !target.IsAlive)
        {
            Flash("No hay una criatura atacable enfrente.");
            return;
        }

        if (!target.Attackable)
        {
            Flash(target.DisplayName + " no es atacable en esta prueba.");
            return;
        }

        if (AOOnlineClientV240.Requested) { AOOnlineClientV240.Attack(target); return; }

        float chance =
            Mathf.Clamp(
                50f +
                (AttackPower -
                 target.EvasionPower) * 0.4f,
                5f,
                95f);

        if (UnityEngine.Random.Range(1, 101) > chance)
        {
            AOCombatFeedbackV113.PlayMiss(
                target.transform.position);

            Flash("Fallaste contra " + target.DisplayName + ".");
            target.NotifyProvoked();
            return;
        }

        if (inventoryV10 == null)
            inventoryV10 =
                GetComponent<AOInventoryV10>();

        int activeMinHit =
            rpgV11 != null
            ? rpgV11.MinHit
            : minHit;

        int activeMaxHit =
            rpgV11 != null
            ? rpgV11.MaxHit
            : maxHit;

        int activeStrength =
            rpgV11 != null
            ? rpgV11.Strength
            : strength;

        float damageModifier =
            rpgV11 != null
            ? rpgV11.GetDamageModifier(
                inventoryV10)
            : meleeClassModifier;

        int raw =
            inventoryV10 != null
            ? inventoryV10
                .RollDamageAgainstNpc(
                    activeMinHit,
                    activeMaxHit,
                    activeStrength,
                    damageModifier)
            : UnityEngine.Random.Range(
                Mathf.Max(
                    0,
                    activeMinHit),
                Mathf.Max(
                    activeMinHit,
                    activeMaxHit) + 1);

        AOMagicEffectRuntimeV129 effects =
            GetComponent<AOMagicEffectRuntimeV129>();

        if (effects != null)
            raw =
                effects.ModifyOutgoingPhysical(
                    raw);

        int damage =
            Mathf.Max(
                0,
                raw -
                target.Defense);

        target.TakeDamage(damage, this);

        if (effects != null)
            effects.NotifyPhysicalHit(
                target,
                this);
        Flash(
            "Golpeas a " + target.DisplayName +
            " por " + damage + ".");
    }

    static Vector3 HeadingWorldOffset(
        int heading)
    {
        switch (heading)
        {
            case AOGridMap.NORTH:
                return Vector3.up;

            case AOGridMap.EAST:
                return Vector3.right;

            case AOGridMap.SOUTH:
                return Vector3.down;

            case AOGridMap.WEST:
                return Vector3.left;
        }

        return Vector3.down;
    }

    void TryPickup()
    {
        AOLootPickupV09 loot =
            AOLootPickupV09.FindAt(
                player.TileX,
                player.TileY);

        if (loot == null)
        {
            int x = player.TileX;
            int y = player.TileY;

            if (player.Heading == AOGridMap.NORTH) y--;
            else if (player.Heading == AOGridMap.EAST) x++;
            else if (player.Heading == AOGridMap.SOUTH) y++;
            else if (player.Heading == AOGridMap.WEST) x--;

            loot =
                AOLootPickupV09.FindAt(x, y);
        }

        if (loot == null)
        {
            Flash("No hay loot para recoger.");
            return;
        }

        if (AOOnlineClientV240.Requested) { AOOnlineClientV240.Pickup(loot); return; }

        AddItem(
            loot.ItemIndex,
            loot.Amount,
            loot.DisplayName);

        loot.Consume();
    }

    public void ReceiveOnlineDamage(int damage)
    {
        if (dead || damage <= 0) return;
        hp = Mathf.Max(0, hp - damage);
        AOCombatFeedbackV113.PlayHit(characterVisual, damage);
        if (hp <= 0) StartCoroutine(DeathRoutine());
    }

    public void ReceiveMagicDamage(
        int damage,
        string sourceName)
    {
        if (dead ||
            damage <= 0)
            return;

        AOMagicEffectRuntimeV129 effects =
            GetComponent<AOMagicEffectRuntimeV129>();

        if (effects != null)
            damage =
                effects.ModifyIncomingMagic(
                    damage);

        hp = Mathf.Max(
            0,
            hp - damage);

        if (characterVisual == null)
            characterVisual = GetComponentInChildren<AOCharacterRenderer>(true);

        AOCombatFeedbackV113.PlayHit(
            characterVisual,
            damage);

        Flash((string.IsNullOrEmpty(sourceName) ? "Magia" : sourceName) +
            " te causa " + damage + " de daño.");

        if (hp <= 0)
            StartCoroutine(DeathRoutine());
    }

    public void ReceiveNpcDamage(
        AONPCCombatV09 attacker,
        int rawDamage,
        int npcAttackPower)
    {
        if (dead)
            return;

        float chance =
            Mathf.Clamp(
                50f +
                (npcAttackPower - EvasionPower) * 0.4f,
                10f,
                90f);

        if (UnityEngine.Random.Range(1, 101) > chance)
        {
            AOCombatFeedbackV113.PlayMiss(
                transform.position);

            Flash(attacker.DisplayName + " falla.");
            return;
        }

        if (inventoryV10 == null)
            inventoryV10 =
                GetComponent<AOInventoryV10>();

        int equipmentAbsorb =
            inventoryV10 != null
            ? inventoryV10
                .RollDefenseAbsorption()
            : 0;

        int damage =
            Mathf.Max(
                0,
                rawDamage -
                defense -
                equipmentAbsorb);

        AOMagicEffectRuntimeV129 effects =
            GetComponent<AOMagicEffectRuntimeV129>();

        if (effects != null)
            damage =
                effects.ModifyIncomingPhysical(
                    damage);

        hp =
            Mathf.Max(0, hp - damage);

        if (characterVisual == null)
        {
            characterVisual =
                GetComponentInChildren
                    <AOCharacterRenderer>(true);
        }

        AOCombatFeedbackV113.PlayHit(
            characterVisual,
            damage);

        Flash(
            attacker.DisplayName +
            " te golpea por " +
            damage + ".");

        if (hp <= 0)
            StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        if (dead)
            yield break;

        dead = true;
        hp = 0;

        if (characterVisual == null)
        {
            characterVisual =
                GetComponentInChildren
                    <AOCharacterRenderer>(true);
        }

        AOCombatFeedbackV113.PlayDeath(
            characterVisual,
            rpgV11 != null &&
            rpgV11.GenderId == 2);

        AOTestPlayer movement =
            GetComponent<AOTestPlayer>();

        if (movement != null)
            movement.enabled = false;

        AODeathRespawnV160 deathSystem =
            GetComponent
                <AODeathRespawnV160>();

        if (deathSystem != null)
            deathSystem.OnDeathStarted();

        Flash(
            "Has muerto. Permanecés en el lugar como espíritu. " +
            "Buscá un sacerdote o usá /HOGAR.",
            5f);

        yield return new WaitForSeconds(
            2f);

        AOCombatFeedbackV113.ResetVisual(
            characterVisual);

        if (deathSystem != null)
            deathSystem.EnterGhostState();
        else
            SetGhostVisual(
                true);

        // No hay RespawnAtSpawn(): el fantasma queda donde murió.
        if (movement != null)
            movement.enabled = true;

        Flash(
            "Sos un espíritu. No podés combatir, castear, comerciar ni usar objetos.");
    }

    void SetGhostVisual(
        bool ghost)
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

        if (profile != null)
        {
            profile.SetDeadVisual(
                ghost);
            return;
        }

        if (characterVisual == null)
        {
            characterVisual =
                GetComponentInChildren
                    <AOCharacterRenderer>(true);
        }

        if (characterVisual == null)
            return;

        if (ghost)
        {
            AODeathVisualV160.TryApplyToRenderer(
                characterVisual);
            return;
        }

        foreach (
            SpriteRenderer renderer in
            characterVisual
                .GetComponentsInChildren
                    <SpriteRenderer>(true))
        {
            if (renderer != null)
                renderer.color =
                    Color.white;
        }
    }

    public void AddRewards(int expAmount, int goldAmount)
    {
        if (rpgV11 == null)
            rpgV11 =
                GetComponent<AOPlayerRPGV11>();

        if (rpgV11 != null)
            rpgV11.AddExperience(
                Mathf.Max(
                    0,
                    expAmount));
        else
            exp +=
                Mathf.Max(
                    0,
                    expAmount);

        gold += Mathf.Max(0, goldAmount);

        if (expAmount > 0 || goldAmount > 0)
        {
            Flash(
                "Recompensa: +" +
                Mathf.Max(0, expAmount) +
                " EXP, +" +
                Mathf.Max(0, goldAmount) +
                " oro.");
        }
    }

    public bool TryAddLootItem(
        int itemIndex,
        int amount,
        string itemName)
    {
        if (amount <= 0)
            return false;

        if (itemIndex ==
            AONPCLootDatabaseV180
                .GoldItemIndex)
        {
            AddGold(
                amount);

            Flash(
                "Recogiste " +
                amount +
                " monedas de oro.");

            return true;
        }

        if (inventoryV10 == null)
        {
            inventoryV10 =
                GetComponent
                    <AOInventoryV10>();
        }

        if (inventoryV10 == null)
            return false;

        if (!inventoryV10.AddItem(
                itemIndex,
                amount))
        {
            Flash(
                "No tenés espacio en el inventario.");

            return false;
        }

        Flash(
            "Recogiste " +
            itemName +
            " x" +
            amount +
            ".");

        return true;
    }

    public void AddItem(
        int itemIndex,
        int amount,
        string itemName)
    {
        if (TryAddLootItem(
                itemIndex,
                amount,
                itemName))
        {
            return;
        }

        // Compatibilidad únicamente si el componente de inventario
        // no existe. Si está lleno, el loot físico no debe desaparecer.
        if (inventoryV10 != null)
            return;

        if (!inventory.ContainsKey(
                itemIndex))
        {
            inventory[itemIndex] =
                0;
        }

        inventory[itemIndex] +=
            amount;
    }

    public int GetItemAmount(int itemIndex)
    {
        return inventory.TryGetValue(
            itemIndex, out int amount)
            ? amount
            : 0;
    }

    void Flash(
        string text,
        float seconds = 3f)
    {
        combatText = text;
        combatTextUntil =
            Time.time +
            Mathf.Max(0.5f, seconds);

        Debug.Log("[AO v0.9] " + text);
    }

    bool PressedAttack()
    {
        return AOPlayerSettingsV230.Pressed(AOGameAction.Attack) ||
               AOPlayerSettingsV230.Pressed(AOGameAction.AttackAlternate);
    }

    bool PressedPickup()
    {
        return AOPlayerSettingsV230.Pressed(AOGameAction.PickUp);
    }

    void OnGUI()
    {
        if (AOOnlineClientV240.InputBlocked) return;
        if (AOInterfaceV0101.Active)
            return;
        float ratio =
            maxHp <= 0
            ? 0f
            : Mathf.Clamp01(
                (float)hp / maxHp);

        GUI.Box(
            new Rect(12, Screen.height - 112, 320, 100),
            "AO v0.9 - Combate local");

        GUI.Label(
            new Rect(24, Screen.height - 88, 290, 22),
            "HP: " + hp + " / " + maxHp +
            " | EXP: " + exp +
            " | Oro: " + gold);

        Rect bg =
            new Rect(
                24,
                Screen.height - 62,
                280,
                16);

        GUI.Box(bg, GUIContent.none);

        GUI.Box(
            new Rect(
                bg.x,
                bg.y,
                bg.width * ratio,
                bg.height),
            GUIContent.none);

        GUI.Label(
            new Rect(
                24,
                Screen.height - 42,
                290,
                22),
            "Ctrl/Espacio: atacar | G: loot | I: inventario");

        if (!string.IsNullOrEmpty(combatText) &&
            Time.time <= combatTextUntil)
        {
            GUI.Box(
                new Rect(
                    344,
                    Screen.height - 90,
                    460,
                    58),
                combatText);
        }
    }
}
