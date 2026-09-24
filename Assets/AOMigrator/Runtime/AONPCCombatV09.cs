using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class AONPCCombatV09 : MonoBehaviour
{
    [Serializable]
    public class DropSpec
    {
        public int itemIndex;
        public string name;
        public int chanceDenominator;
        public int minAmount;
        public int maxAmount;
        public string source;
    }

    AONPCMetadata metadata;
    AONPCMovementV08 movement;
    AOCharacterRenderer visual;
    AOPlayerCombatV09 playerCombat;

    int maxHp;
    int hp;
    int minHit;
    int maxHit;
    int defense;
    int attackPower;
    int evasionPower;
    bool attackable;
    int attackIntervalMs;
    int respawnMinSeconds;
    int respawnMaxSeconds;
    int giveExp;
    int giveGold;
    DropSpec[] drops;

    int originX;
    int originY;
    bool alive = true;
    float nextAttackAt;
    Renderer[] renderers;

    public int NetworkId { get; set; }
    public void ApplyOnline(AOCoopNpc state)
    {
        bool wasAlive = alive;
        int oldHp = hp;
        hp = state.hp; maxHp = state.maxHp; alive = !state.dead;
        if (metadata != null) metadata.enabled = alive;
        if (wasAlive != alive) SetVisuals(alive);
        if (movement != null) movement.ApplyOnline(state.x, state.y, state.heading, alive);
        if (wasAlive && oldHp > hp) AOCombatFeedbackV113.PlayHit(visual, oldHp - hp);
    }
    public int HP => hp;
    public int MaxHP => maxHp;
    public int Defense
    {
        get
        {
            AOMagicEffectRuntimeV129 e =
                GetComponent<AOMagicEffectRuntimeV129>();

            return defense +
                (e == null
                    ? 0
                    : e.DefenseBonus);
        }
    }

    public int AttackPower
    {
        get
        {
            AOMagicEffectRuntimeV129 e =
                GetComponent<AOMagicEffectRuntimeV129>();

            return attackPower +
                (e == null
                    ? 0
                    : e.HitBonus);
        }
    }

    public int EvasionPower
    {
        get
        {
            AOMagicEffectRuntimeV129 e =
                GetComponent<AOMagicEffectRuntimeV129>();

            return evasionPower +
                (e == null
                    ? 0
                    : e.EvasionBonus);
        }
    }
    public bool Attackable => attackable;
    public bool IsAlive => alive;
    public string DisplayName =>
        metadata == null
        ? gameObject.name
        : metadata.DisplayName;

    public void Configure(
        AONPCMetadata newMetadata,
        AONPCMovementV08 newMovement,
        AOCharacterRenderer newVisual,
        AOPlayerCombatV09 newPlayerCombat,
        int startX,
        int startY,
        int newMaxHp,
        int newMinHit,
        int newMaxHit,
        int newDefense,
        int newAttackPower,
        int newEvasionPower,
        bool newAttackable,
        int newAttackIntervalMs,
        int newRespawnMinSeconds,
        int newRespawnMaxSeconds,
        int newGiveExp,
        int newGiveGold,
        DropSpec[] newDrops)
    {
        metadata = newMetadata;
        movement = newMovement;
        visual = newVisual;
        playerCombat = newPlayerCombat;

        originX = startX;
        originY = startY;

        maxHp = Mathf.Max(1, newMaxHp);
        hp = maxHp;
        minHit = Mathf.Max(0, newMinHit);
        maxHit = Mathf.Max(minHit, newMaxHit);
        defense = Mathf.Max(0, newDefense);
        attackPower = Mathf.Max(0, newAttackPower);
        evasionPower = Mathf.Max(0, newEvasionPower);
        attackable = newAttackable;
        attackIntervalMs =
            Mathf.Max(100, newAttackIntervalMs);
        respawnMinSeconds =
            Mathf.Max(0, newRespawnMinSeconds);
        respawnMaxSeconds =
            Mathf.Max(
                respawnMinSeconds,
                newRespawnMaxSeconds);
        giveExp = Mathf.Max(0, newGiveExp);
        giveGold = Mathf.Max(0, newGiveGold);
        drops = newDrops ?? new DropSpec[0];

        renderers =
            GetComponentsInChildren<Renderer>(true);

        alive = true;
    }

    public bool TryAttackPlayer()
    {
        if (AOOnlineClientV240.Requested) return false;
        if (!alive)
            return false;

        AONPCMagicStatusV120 status =
            GetComponent<AONPCMagicStatusV120>();

        if (status != null &&
            !status.CanAttack)
            return false;

        if (playerCombat == null)
            playerCombat =
                UnityEngine.Object
                    .FindFirstObjectByType<AOPlayerCombatV09>();

        if (playerCombat == null ||
            playerCombat.IsDead ||
            Time.time < nextAttackAt)
            return false;

        nextAttackAt =
            Time.time +
            attackIntervalMs / 1000f;

        if (visual != null)
        {
            int heading =
                metadata == null
                ? AOGridMap.SOUTH
                : metadata.Heading;

            AOCombatFeedbackV113.PlayAttack(
                visual,
                heading,
                0);
        }

        int raw =
            UnityEngine.Random.Range(
                minHit,
                maxHit + 1);

        AOMagicEffectRuntimeV129 effects =
            GetComponent<AOMagicEffectRuntimeV129>();

        if (effects != null)
            raw =
                effects.ModifyOutgoingPhysical(
                    raw);

        playerCombat.ReceiveNpcDamage(
            this,
            raw,
            attackPower);

        return true;
    }

    public int HealMagic(
        int amount)
    {
        if (!alive ||
            amount <= 0)
            return 0;

        AOMagicEffectRuntimeV129 magicEffects =
            GetComponent<AOMagicEffectRuntimeV129>();

        if (magicEffects != null)
            amount =
                magicEffects.ApplyIncomingHealing(
                    amount);

        int before = hp;
        hp = Mathf.Min(
            maxHp,
            hp + amount);

        return hp - before;
    }

    public void TakeDamage(
        int damage,
        AOPlayerCombatV09 attacker)
    {
        if (AOOnlineClientV240.Requested) return;
        if (!alive)
            return;

        NotifyProvoked();

        AOMagicEffectRuntimeV129 magicEffects =
            GetComponent<AOMagicEffectRuntimeV129>();

        int finalDamage =
            magicEffects == null
            ? Mathf.Max(
                0,
                damage)
            : magicEffects
                .ModifyIncomingPhysical(
                    Mathf.Max(
                        0,
                        damage));

        hp =
            Mathf.Max(
                0,
                hp - finalDamage);

        if (visual != null)
        {
            AOCombatFeedbackV113.PlayHit(
                visual,
                finalDamage);
        }

        if (hp <= 0)
            Die(attacker);
    }

    public void TakeMagicDamage(
        int damage,
        AOPlayerCombatV09 attacker)
    {
        if (AOOnlineClientV240.Requested) return;
        if (!alive)
            return;

        NotifyProvoked();

        AOMagicEffectRuntimeV129 magicEffects =
            GetComponent<AOMagicEffectRuntimeV129>();

        int finalDamage =
            magicEffects == null
            ? Mathf.Max(
                0,
                damage)
            : magicEffects
                .ModifyIncomingMagic(
                    Mathf.Max(
                        0,
                        damage));

        hp =
            Mathf.Max(
                0,
                hp - finalDamage);

        if (visual != null)
        {
            AOCombatFeedbackV113.PlayHit(
                visual,
                finalDamage);
        }

        if (hp <= 0)
            Die(attacker);
    }

    public void NotifyProvoked()
    {
        if (movement != null)
            movement.SetProvokedByPlayer(true);
    }

    void Die(AOPlayerCombatV09 killer)
    {
        if (!alive)
            return;

        alive = false;

        int deathX =
            metadata != null
            ? metadata.TileX
            : originX;

        int deathY =
            metadata != null
            ? metadata.TileY
            : originY;

        if (movement != null)
            movement.PauseForDeath();

        if (metadata != null)
            metadata.enabled = false;

        if (visual != null)
        {
            AOCombatFeedbackV113.PlayDeath(
                visual,
                false);

            StartCoroutine(
                HideVisualAfterDeath(
                    0.34f));
        }
        else
        {
            SetVisuals(false);
        }

        if (killer != null)
        {
            AOQuestSystemV150 quests =
                killer.GetComponent
                    <AOQuestSystemV150>();

            if (quests != null &&
                metadata != null)
            {
                quests.NotifyNpcKilled(
                    metadata.NpcIndex);
            }

            AONPCLootDatabaseV180.NPCDef source =
                metadata == null
                ? null
                : AONPCLootDatabaseV180.Get(
                    metadata.NpcIndex);

            int expReward =
                source == null
                ? giveExp
                : source.giveExp;

            // El oro en AO cae físicamente al piso.
            killer.AddRewards(
                expReward,
                0);

            if (source != null)
            {
                SpawnSourceLoot(
                    source,
                    killer,
                    deathX,
                    deathY);
            }
            else
            {
                // Fallback para NPCs no presentes en el DB.
                SpawnDrops(
                    deathX,
                    deathY);
            }
        }

        AONPCLootDatabaseV180.NPCDef respawnSource =
            metadata == null
            ? null
            : AONPCLootDatabaseV180.Get(
                metadata.NpcIndex);

        if (respawnSource != null &&
            respawnSource.respawnDisabled)
        {
            return;
        }

        float delay =
            GetSourceRespawnDelay(
                respawnSource);

        StartCoroutine(
            RespawnRoutine(
                delay,
                respawnSource));
    }

    void SpawnSourceLoot(
        AONPCLootDatabaseV180.NPCDef source,
        AOPlayerCombatV09 killer,
        int tileX,
        int tileY)
    {
        if (source == null)
            return;

        // Legacy resource behavior: NROITEMS/ObjN is the NPC inventory
        // and NPC_TIRAR_ITEMS drops every entry on death.
        if (source.inventoryDrops != null)
        {
            foreach (
                AONPCLootDatabaseV180.ItemDrop drop in
                source.inventoryDrops)
            {
                SpawnFloorItem(
                    drop,
                    tileX,
                    tileY);
            }
        }

        if (source.giveGold > 0)
        {
            AOLootPickupV09.Create(
                AONPCLootDatabaseV180
                    .GoldItemIndex,
                "Monedas de Oro",
                source.giveGold,
                tileX,
                tileY);
        }

        // Legacy Quiza system:
        // one chance roll, then one random candidate from QuizaDropea.
        if (source.randomDrops != null &&
            source.randomDrops.Length > 0)
        {
            int denominator =
                Mathf.Max(
                    1,
                    source.randomDropDenominator);

            if (UnityEngine.Random.Range(
                    1,
                    denominator + 1) ==
                1)
            {
                int index =
                    UnityEngine.Random.Range(
                        0,
                        source.randomDrops.Length);

                SpawnFloorItem(
                    source.randomDrops[index],
                    tileX,
                    tileY);

                AOLootFeedbackV180
                    .PlayDrop();
            }
        }

        SpawnQuestDrops(
            source,
            killer,
            tileX,
            tileY);
    }

    void SpawnFloorItem(
        AONPCLootDatabaseV180.ItemDrop drop,
        int tileX,
        int tileY)
    {
        if (drop == null ||
            drop.itemIndex <= 0 ||
            drop.amount <= 0)
            return;

        AOLootPickupV09.Create(
            drop.itemIndex,
            string.IsNullOrWhiteSpace(
                drop.name)
                ? "OBJ " +
                  drop.itemIndex
                : drop.name,
            drop.amount,
            tileX,
            tileY);
    }

    void SpawnQuestDrops(
        AONPCLootDatabaseV180.NPCDef source,
        AOPlayerCombatV09 killer,
        int tileX,
        int tileY)
    {
        if (source == null ||
            source.questDrops == null ||
            killer == null)
            return;

        AOQuestSystemV150 questSystem =
            killer.GetComponent
                <AOQuestSystemV150>();

        AOInventoryV10 inventory =
            killer.GetComponent
                <AOInventoryV10>();

        if (questSystem == null)
            return;

        foreach (
            AONPCLootDatabaseV180.QuestDrop drop in
            source.questDrops)
        {
            if (drop == null ||
                drop.questId <= 0 ||
                drop.itemIndex <= 0)
                continue;

            if (!questSystem.NeedsQuestItem(
                    drop.questId,
                    drop.itemIndex,
                    out _))
                continue;

            int denominator =
                Mathf.Max(
                    1,
                    drop.probabilityDenominator);

            if (UnityEngine.Random.Range(
                    1,
                    denominator + 1) !=
                1)
                continue;

            int amount =
                Mathf.Max(
                    1,
                    drop.amount);

            bool inserted =
                inventory != null &&
                inventory.AddItem(
                    drop.itemIndex,
                    amount);

            if (inserted)
            {
                AOInterfaceV0101.PushMessage(
                    "Objeto de quest: " +
                    (string.IsNullOrWhiteSpace(
                        drop.name)
                        ? "OBJ " +
                          drop.itemIndex
                        : drop.name) +
                    " x" +
                    amount +
                    ".");

                AOLootFeedbackV180
                    .PlayDrop();
            }
            else
            {
                // El server lo intenta dar al inventario. En la demo,
                // si está lleno lo dejamos en el piso para no destruirlo.
                AOLootPickupV09.Create(
                    drop.itemIndex,
                    string.IsNullOrWhiteSpace(
                        drop.name)
                        ? "OBJ " +
                          drop.itemIndex
                        : drop.name,
                    amount,
                    tileX,
                    tileY);
            }
        }
    }

    void SpawnDrops(
        int tileX,
        int tileY)
    {
        if (drops == null)
            return;

        foreach (DropSpec drop in drops)
        {
            if (drop == null ||
                drop.itemIndex <= 0)
                continue;

            int denominator =
                Mathf.Max(
                    1,
                    drop.chanceDenominator);

            if (UnityEngine.Random.Range(
                    1,
                    denominator + 1) != 1)
                continue;

            int amount =
                UnityEngine.Random.Range(
                    Mathf.Max(1, drop.minAmount),
                    Mathf.Max(
                        drop.minAmount,
                        drop.maxAmount) + 1);

            AOLootPickupV09.Create(
                drop.itemIndex,
                string.IsNullOrEmpty(drop.name)
                    ? "OBJ " + drop.itemIndex
                    : drop.name,
                amount,
                tileX,
                tileY);
        }
    }

    float GetSourceRespawnDelay(
        AONPCLootDatabaseV180.NPCDef source)
    {
        int min =
            source == null
            ? respawnMinSeconds
            : source.respawnMinSeconds;

        int max =
            source == null
            ? respawnMaxSeconds
            : source.respawnMaxSeconds;

        min =
            Mathf.Max(
                0,
                min);

        max =
            Mathf.Max(
                min,
                max);

        if (max <= 0)
        {
            // El servidor lo recrea inmediatamente. En Unity dejamos
            // 0.35 s para que se vea el feedback de muerte.
            return 0.35f;
        }

        return UnityEngine.Random.Range(
            min,
            max + 1);
    }

    IEnumerator RespawnRoutine(
        float seconds,
        AONPCLootDatabaseV180.NPCDef source)
    {
        yield return new WaitForSeconds(
            Mathf.Max(
                0.35f,
                seconds));

        hp = maxHp;
        alive = true;

        if (metadata != null)
            metadata.enabled = true;

        if (movement != null)
        {
            movement.RespawnFromSource(
                source != null &&
                source.respawnOrigPos);

            movement.SetProvokedByPlayer(
                false);
        }
        else if (metadata != null)
        {
            metadata.MoveToTile(
                originX,
                originY,
                metadata.Heading);
        }

        SetVisuals(
            true);

        if (visual != null)
        {
            AOCombatFeedbackV113.ResetVisual(
                visual);
        }
    }

    IEnumerator HideVisualAfterDeath(
        float seconds)
    {
        yield return new WaitForSeconds(
            Mathf.Max(
                0.05f,
                seconds));

        if (!alive)
            SetVisuals(false);
    }

    void SetVisuals(bool visible)
    {
        if (renderers == null ||
            renderers.Length == 0)
        {
            renderers =
                GetComponentsInChildren<Renderer>(true);
        }

        foreach (Renderer r in renderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }
}
