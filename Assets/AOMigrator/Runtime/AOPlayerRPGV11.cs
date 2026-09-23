using System;
using UnityEngine;

[DisallowMultipleComponent]
public class AOPlayerRPGV11 : MonoBehaviour
{
    public enum Race
    {
        Humano = 1,
        Elfo = 2,
        ElfoOscuro = 3,
        Gnomo = 4,
        Enano = 5,
        Orco = 6
    }

    public enum Gender
    {
        Hombre = 1,
        Mujer = 2
    }

    public enum PlayerClass
    {
        Mago = 1,
        Clerigo = 2,
        Guerrero = 3,
        Asesino = 4,
        Bardo = 5,
        Druida = 6,
        Paladin = 7,
        Cazador = 8,
        Trabajador = 9,
        Pirata = 10,
        Ladron = 11,
        Bandido = 12
    }

    [Header("Perfil AO")]
    [SerializeField] bool initialized;
    [SerializeField] Race race = Race.Humano;
    [SerializeField] Gender gender = Gender.Hombre;
    [SerializeField] PlayerClass playerClass = PlayerClass.Guerrero;

    [Header("Atributos")]
    [SerializeField] int strength;
    [SerializeField] int agility;
    [SerializeField] int intelligence;
    [SerializeField] int constitution;
    [SerializeField] int charisma;

    [Header("Progresión")]
    [SerializeField] int level = 1;
    [SerializeField] long experience;
    [SerializeField] int skillPoints = 10;
    [SerializeField] int[] skillValues = new int[24];

    [Header("Stats")]
    [SerializeField] int maxHp;
    [SerializeField] int currentMana;
    [SerializeField] int maxMana;
    [SerializeField] int currentStamina;
    [SerializeField] int maxStamina;
    [SerializeField] int hunger = 100;
    [SerializeField] int maxHunger = 100;
    [SerializeField] int thirst = 100;
    [SerializeField] int maxThirst = 100;
    [SerializeField] int minHit = 1;
    [SerializeField] int maxHit = 2;

    // v0.11.4 timers.
    // intervalos.ini: Sed=4000/25=160 s; Hambre=4500/25=180 s.
    float nextThirstLossAt;
    float nextHungerLossAt;
    float nextStaminaRecoveryAt;

    // DuracionPociones del servidor corre una vez por segundo.
    float nextPotionSecondAt;
    int potionSecondsRemaining;

    public bool Initialized => initialized;
    public int RaceId => (int)race;
    public int GenderId => (int)gender;
    public int ClassId => (int)playerClass;

    public string RaceName =>
        AORPGDatabaseV11.GetRace(RaceId)?.name ??
        race.ToString();

    public string ClassName =>
        AORPGDatabaseV11.GetClass(ClassId)?.name ??
        playerClass.ToString();

    public string GenderName =>
        gender == Gender.Hombre
        ? "Hombre"
        : "Mujer";

    public int Strength => strength;
    public int Agility => agility;
    public int Intelligence => intelligence;
    public int Constitution => constitution;
    public int Charisma => charisma;

    public int Level => level;
    public long Experience => experience;
    public int SkillPoints => skillPoints;

    public int MaxHP => Mathf.Max(1, maxHp);
    public int Mana => currentMana;
    public int MaxMana => maxMana;
    public int Stamina => currentStamina;
    public int MaxStamina => maxStamina;
    public int Hunger => hunger;
    public int MaxHunger => maxHunger;
    public int Thirst => thirst;
    public int MaxThirst => maxThirst;
    public int MinHit => minHit;
    public int MaxHit => maxHit;

    public long ExpToNextLevel =>
        level >= AORPGDatabaseV11.MaxLevel
        ? 0
        : AORPGDatabaseV11.ExpForLevel(level);

    void Awake()
    {
        EnsureSkillArray();

        if (!initialized)
        {
            InitializeProfile(
                RaceId,
                GenderId,
                ClassId,
                true);
        }
    }

    void Update()
    {
        if (!initialized)
            return;

        float now = Time.time;

        if (nextThirstLossAt <= 0f)
            nextThirstLossAt = now + 160f;

        if (nextHungerLossAt <= 0f)
            nextHungerLossAt = now + 180f;

        if (now >= nextThirstLossAt)
        {
            nextThirstLossAt += 160f;

            if (thirst > 0)
            {
                thirst =
                    Mathf.Max(
                        0,
                        thirst - 10);

                Debug.Log(
                    "[AO v0.11.4] Sed: " +
                    thirst +
                    "/" +
                    maxThirst);
            }
        }

        if (now >= nextHungerLossAt)
        {
            nextHungerLossAt += 180f;

            if (hunger > 0)
            {
                hunger =
                    Mathf.Max(
                        0,
                        hunger - 10);

                Debug.Log(
                    "[AO v0.11.4] Hambre: " +
                    hunger +
                    "/" +
                    maxHunger);
            }
        }

        // EfectoStamina se evalúa cada 40ms.
        // StaminaIntervaloSinDescansar=10 => recuperación ~cada 0.44s.
        if (nextStaminaRecoveryAt <= 0f)
            nextStaminaRecoveryAt = now + 0.44f;

        if (now >= nextStaminaRecoveryAt)
        {
            nextStaminaRecoveryAt = now + 0.44f;

            AOPlayerCombatV09 combatState =
                GetComponent<AOPlayerCombatV09>();

            if ((combatState == null ||
                 !combatState.IsDead) &&
                hunger > 0 &&
                thirst > 0 &&
                currentStamina < maxStamina)
            {
                RecoverStaminaAO();
            }
        }

        if (potionSecondsRemaining > 0)
        {
            if (nextPotionSecondAt <= 0f)
                nextPotionSecondAt = now + 1f;

            if (now >= nextPotionSecondAt)
            {
                nextPotionSecondAt += 1f;
                potionSecondsRemaining--;

                if (potionSecondsRemaining <= 0)
                {
                    RestoreBaseAttributes();

                    Debug.Log(
                        "[AO v0.11.4] Terminó el efecto temporal de la poción.");
                }
            }
        }
    }

    public void InitializeProfile(
        int newRace,
        int newGender,
        int newClass,
        bool resetProgress)
    {
        race =
            (Race)Mathf.Clamp(
                newRace,
                1,
                6);

        gender =
            (Gender)Mathf.Clamp(
                newGender,
                1,
                2);

        playerClass =
            (PlayerClass)Mathf.Clamp(
                newClass,
                1,
                12);

        EnsureSkillArray();

        AORPGDatabaseV11.RaceDef raceDef =
            AORPGDatabaseV11.GetRace(
                RaceId);

        int baseAttribute =
            AORPGDatabaseV11.BaseAttribute;

        strength =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.strength);

        agility =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.agility);

        intelligence =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.intelligence);

        constitution =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.constitution);

        charisma =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.charisma);

        if (resetProgress)
        {
            level = 1;
            experience = 0;
            skillPoints =
                AORPGDatabaseV11.InitialSkillPoints;

            for (int i = 0;
                 i < skillValues.Length;
                 i++)
            {
                skillValues[i] = 0;
            }

            maxHp =
                Mathf.Max(
                    1,
                    constitution);

            RecalculateLevelStats();

            int rollMax =
                Mathf.Max(
                    1,
                    agility / 6);

            int roll =
                UnityEngine.Random.Range(
                    1,
                    rollMax + 1);

            if (roll == 1)
                roll = 2;

            maxStamina =
                Mathf.Max(
                    20,
                    20 * roll);

            currentMana = maxMana;
            currentStamina = maxStamina;
            hunger = maxHunger = 100;
            thirst = maxThirst = 100;

            potionSecondsRemaining = 0;
            nextPotionSecondAt = 0f;
            nextThirstLossAt = 0f;
            nextHungerLossAt = 0f;
            nextStaminaRecoveryAt = 0f;
        }
        else
        {
            RecalculateLevelStats();

            currentMana =
                Mathf.Clamp(
                    currentMana,
                    0,
                    maxMana);

            currentStamina =
                Mathf.Clamp(
                    currentStamina,
                    0,
                    maxStamina);
        }

        initialized = true;

        AOPlayerCombatV09 combat =
            GetComponent<AOPlayerCombatV09>();

        if (combat != null)
            combat.SyncFromRPG(true);

        AOInventoryV10 inventory =
            GetComponent<AOInventoryV10>();

        if (inventory != null)
            inventory.ValidateEquipmentForRPG();

        AOCharacterProfileVisualV111 profileVisual =
            GetComponent<AOCharacterProfileVisualV111>();

        if (profileVisual != null)
            profileVisual.SyncFromRPG();

        Debug.Log(
            "[AO v0.11] Perfil: " +
            RaceName + " / " +
            GenderName + " / " +
            ClassName +
            " | FUE " + strength +
            " AGI " + agility +
            " INT " + intelligence +
            " CON " + constitution +
            " CAR " + charisma);
    }

    public void RestoreSaveState(
        int savedRace,
        int savedGender,
        int savedClass,
        int savedLevel,
        long savedExperience,
        int savedSkillPoints,
        int[] savedSkills,
        int savedMaxHp,
        int savedMana,
        int savedMaxStamina,
        int savedStamina,
        int savedHunger,
        int savedMaxHunger,
        int savedThirst,
        int savedMaxThirst)
    {
        race =
            (Race)Mathf.Clamp(
                savedRace,
                1,
                6);

        gender =
            (Gender)Mathf.Clamp(
                savedGender,
                1,
                2);

        playerClass =
            (PlayerClass)Mathf.Clamp(
                savedClass,
                1,
                12);

        EnsureSkillArray();

        AORPGDatabaseV11.RaceDef raceDef =
            AORPGDatabaseV11.GetRace(
                RaceId);

        int baseAttribute =
            AORPGDatabaseV11.BaseAttribute;

        strength =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.strength);

        agility =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.agility);

        intelligence =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.intelligence);

        constitution =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.constitution);

        charisma =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.charisma);

        level =
            Mathf.Clamp(
                savedLevel,
                1,
                AORPGDatabaseV11.MaxLevel);

        experience =
            System.Math.Max(
                0L,
                savedExperience);

        skillPoints =
            Mathf.Max(
                0,
                savedSkillPoints);

        for (int i = 0;
             i < skillValues.Length;
             i++)
        {
            int value =
                savedSkills != null &&
                i < savedSkills.Length
                ? savedSkills[i]
                : 0;

            skillValues[i] =
                Mathf.Clamp(
                    value,
                    0,
                    AORPGDatabaseV11.MaxSkill);
        }

        maxHp =
            Mathf.Max(
                1,
                savedMaxHp);

        maxStamina =
            Mathf.Max(
                1,
                savedMaxStamina);

        RecalculateLevelStats();

        // Preservamos el MaxSTA exacto del save, importante para nivel 1
        // donde AO usa una tirada inicial.
        if (savedMaxStamina > 0)
        {
            maxStamina =
                savedMaxStamina;
        }

        currentMana =
            Mathf.Clamp(
                savedMana,
                0,
                maxMana);

        currentStamina =
            Mathf.Clamp(
                savedStamina,
                0,
                maxStamina);

        maxHunger =
            Mathf.Max(
                1,
                savedMaxHunger);

        hunger =
            Mathf.Clamp(
                savedHunger,
                0,
                maxHunger);

        maxThirst =
            Mathf.Max(
                1,
                savedMaxThirst);

        thirst =
            Mathf.Clamp(
                savedThirst,
                0,
                maxThirst);

        // Efectos temporales no se serializan.
        potionSecondsRemaining = 0;
        nextPotionSecondAt = 0f;
        nextThirstLossAt = 0f;
        nextHungerLossAt = 0f;
        nextStaminaRecoveryAt = 0f;

        initialized = true;

        AOCharacterProfileVisualV111 profileVisual =
            GetComponent
                <AOCharacterProfileVisualV111>();

        if (profileVisual != null)
            profileVisual.SyncFromRPG();

        AOInventoryV10 inv =
            GetComponent
                <AOInventoryV10>();

        if (inv != null)
            inv.ValidateEquipmentForRPG();

        Debug.Log(
            "[AO v0.14] RPG restaurado: " +
            RaceName +
            " / " +
            ClassName +
            " Nv " +
            level);
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0 ||
            level >= AORPGDatabaseV11.MaxLevel)
            return;

        experience += amount;

        bool leveled = false;

        while (level <
               AORPGDatabaseV11.MaxLevel)
        {
            long required =
                AORPGDatabaseV11.ExpForLevel(
                    level);

            if (required <= 0 ||
                experience < required)
                break;

            experience -= required;
            level++;

            AORPGDatabaseV11.ClassDef classDef =
                AORPGDatabaseV11.GetClass(
                    ClassId);

            if (classDef != null)
            {
                skillPoints +=
                    Mathf.Max(
                        0,
                        classDef.skillPointsPerLevel);
            }

            ApplyLifeLevelGain();
            RecalculateLevelStats();

            currentMana =
                Mathf.Min(
                    currentMana,
                    maxMana);

            currentStamina =
                Mathf.Min(
                    currentStamina,
                    maxStamina);

            leveled = true;

            Debug.Log(
                "[AO v0.11] ¡Subiste a nivel " +
                level +
                "! Puntos de skill libres: " +
                skillPoints);
        }

        if (leveled)
        {
            AOPlayerCombatV09 combat =
                GetComponent<AOPlayerCombatV09>();

            if (combat != null)
                combat.SyncFromRPG(true);
        }
    }

    void ApplyLifeLevelGain()
    {
        AORPGDatabaseV11.ClassDef classDef =
            AORPGDatabaseV11.GetClass(
                ClassId);

        if (classDef == null)
            return;

        float averageGain =
            classDef.life -
            (21f - constitution) *
            0.5f;

        int low =
            Mathf.FloorToInt(
                averageGain -
                AORPGDatabaseV11.LifeRange);

        int high =
            Mathf.CeilToInt(
                averageGain +
                AORPGDatabaseV11.LifeRange);

        int gain =
            UnityEngine.Random.Range(
                low,
                high + 1);

        int proposed =
            maxHp +
            gain;

        float expected =
            GetExpectedMaxHP();

        int upper =
            Mathf.RoundToInt(
                expected +
                AORPGDatabaseV11.LifeCapMax);

        int lower =
            Mathf.RoundToInt(
                expected +
                AORPGDatabaseV11.LifeCapMin);

        maxHp =
            Mathf.Max(
                1,
                Mathf.Clamp(
                    proposed,
                    lower,
                    upper));
    }

    float GetExpectedMaxHP()
    {
        AORPGDatabaseV11.ClassDef classDef =
            AORPGDatabaseV11.GetClass(
                ClassId);

        if (classDef == null)
            return constitution;

        return
            (classDef.life -
             (21f - constitution) *
             0.5f) *
            (level - 1) +
            constitution;
    }

    void RecalculateLevelStats()
    {
        AORPGDatabaseV11.ClassDef classDef =
            AORPGDatabaseV11.GetClass(
                ClassId);

        if (classDef == null)
            return;

        maxMana =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    intelligence *
                    classDef.initialMana +
                    classDef.multiMana *
                    intelligence *
                    (level - 1)));

        if (level > 1)
        {
            maxStamina =
                Mathf.Max(
                    1,
                    60 +
                    (level - 1) *
                    classDef.staminaPerLevel);
        }

        int hitModifier;

        if (level <= 36)
        {
            hitModifier =
                (level - 1) *
                classDef.hitPre36;
        }
        else
        {
            hitModifier =
                35 *
                classDef.hitPre36 +
                (level - 36) *
                classDef.hitPost36;
        }

        minHit =
            hitModifier + 1;

        maxHit =
            hitModifier + 2;
    }

    public int AddHunger(int amount)
    {
        int before = hunger;

        hunger =
            Mathf.Clamp(
                hunger +
                Mathf.Max(
                    0,
                    amount),
                0,
                maxHunger);

        return hunger - before;
    }

    public int AddThirst(int amount)
    {
        int before = thirst;

        thirst =
            Mathf.Clamp(
                thirst +
                Mathf.Max(
                    0,
                    amount),
                0,
                maxThirst);

        return thirst - before;
    }

    public bool CanPayMagicCost(
        int manaCost,
        int staminaCost)
    {
        return currentMana >= Mathf.Max(0, manaCost) &&
            currentStamina >= Mathf.Max(0, staminaCost);
    }

    public bool SpendMagicCost(
        int manaCost,
        int staminaCost)
    {
        manaCost = Mathf.Max(0, manaCost);
        staminaCost = Mathf.Max(0, staminaCost);
        if (!CanPayMagicCost(manaCost, staminaCost)) return false;
        currentMana -= manaCost;
        currentStamina -= staminaCost;
        return true;
    }

    public int ApplySpellAttributeModifier(
        int attribute,
        int direction,
        int minAmount,
        int maxAmount,
        int durationSeconds)
    {
        int lo=Mathf.Min(minAmount,maxAmount); int hi=Mathf.Max(minAmount,maxAmount); int amount=UnityEngine.Random.Range(lo,hi+1);
        AORPGDatabaseV11.RaceDef raceDef=AORPGDatabaseV11.GetRace(RaceId); int baseAttribute=AORPGDatabaseV11.BaseAttribute;
        if(attribute==1){ int b=baseAttribute+(raceDef==null?0:raceDef.strength); if(direction==1)strength=Mathf.Min(strength+amount,b*2); else if(direction==2)strength=Mathf.Max(1,strength-amount); else return 0; }
        else if(attribute==2){ int b=baseAttribute+(raceDef==null?0:raceDef.agility); if(direction==1)agility=Mathf.Min(agility+amount,b*2); else if(direction==2)agility=Mathf.Max(1,agility-amount); else return 0; }
        else if(attribute==5){ int b=baseAttribute+(raceDef==null?0:raceDef.charisma); if(direction==1)charisma=Mathf.Min(charisma+amount,b*2); else if(direction==2)charisma=Mathf.Max(1,charisma-amount); else return 0; }
        else return 0;
        potionSecondsRemaining=Mathf.Max(potionSecondsRemaining,Mathf.Max(1,durationSeconds)); nextPotionSecondAt=Time.time+1f; return amount;
    }

    public int RestoreManaAmount(int amount)
    {
        int before=currentMana;
        currentMana=Mathf.Clamp(currentMana+Mathf.Max(0,amount),0,maxMana);
        return currentMana-before;
    }

    public int ModifyMana(int amount)
    {
        int before=currentMana;
        currentMana=Mathf.Clamp(currentMana+amount,0,maxMana);
        return currentMana-before;
    }

    public int ModifyStamina(int amount)
    {
        int before=currentStamina;
        currentStamina=Mathf.Clamp(currentStamina+amount,0,maxStamina);
        return currentStamina-before;
    }

    public int ModifyHunger(int amount)
    {
        int before=hunger;
        hunger=Mathf.Clamp(hunger+amount,0,maxHunger);
        return hunger-before;
    }

    public int ModifyThirst(int amount)
    {
        int before=thirst;
        thirst=Mathf.Clamp(thirst+amount,0,maxThirst);
        return thirst-before;
    }

    public int RestoreManaPercent(
        int percentage)
    {
        if (maxMana <= 0)
            return 0;

        int before =
            currentMana;

        int amount =
            Mathf.RoundToInt(
                maxMana *
                Mathf.Max(
                    0,
                    percentage) /
                100f);

        currentMana =
            Mathf.Clamp(
                currentMana +
                amount,
                0,
                maxMana);

        return currentMana - before;
    }

    public int RestoreStaminaRange(
        int minAmount,
        int maxAmount)
    {
        int lo =
            Mathf.Min(
                minAmount,
                maxAmount);

        int hi =
            Mathf.Max(
                minAmount,
                maxAmount);

        lo =
            Mathf.Max(
                0,
                lo);

        hi =
            Mathf.Max(
                lo,
                hi);

        int amount =
            UnityEngine.Random.Range(
                lo,
                hi + 1);

        int before =
            currentStamina;

        currentStamina =
            Mathf.Clamp(
                currentStamina +
                amount,
                0,
                maxStamina);

        return currentStamina - before;
    }

    public int ApplyAttributePotion(
        int potionType,
        int minAmount,
        int maxAmount,
        int durationSeconds)
    {
        // Tipos estables de e_PotionType:
        // 1 Agilidad, 2 Fuerza.
        if (potionType != 1 &&
            potionType != 2)
            return 0;

        int lo =
            Mathf.Min(
                minAmount,
                maxAmount);

        int hi =
            Mathf.Max(
                minAmount,
                maxAmount);

        int increase =
            UnityEngine.Random.Range(
                lo,
                hi + 1);

        AORPGDatabaseV11.RaceDef raceDef =
            AORPGDatabaseV11.GetRace(
                RaceId);

        int baseAttribute =
            AORPGDatabaseV11.BaseAttribute;

        if (potionType == 1)
        {
            int baseAgility =
                baseAttribute +
                (raceDef == null
                    ? 0
                    : raceDef.agility);

            int before =
                agility;

            agility =
                Mathf.Min(
                    agility +
                    increase,
                    baseAgility * 2);

            increase =
                agility - before;
        }
        else
        {
            int baseStrength =
                baseAttribute +
                (raceDef == null
                    ? 0
                    : raceDef.strength);

            int before =
                strength;

            strength =
                Mathf.Min(
                    strength +
                    increase,
                    baseStrength * 2);

            increase =
                strength - before;
        }

        potionSecondsRemaining =
            Mathf.Max(
                1,
                durationSeconds);

        nextPotionSecondAt =
            Time.time + 1f;

        return increase;
    }

    public void OnPlayerDeath()
    {
        currentStamina = 0;

        RestoreBaseAttributes();

        nextPotionSecondAt = 0f;
        nextStaminaRecoveryAt =
            Time.time + 0.44f;
    }

    public void SetResourcesForTesting()
    {
        hunger =
            Mathf.Min(
                maxHunger,
                20);

        thirst =
            Mathf.Min(
                maxThirst,
                20);

        currentStamina =
            Mathf.Min(
                maxStamina,
                10);

        currentMana = 0;

        Debug.Log(
            "[AO v0.11.4] TEST: Hambre/Sed=20, Stamina=10, Mana=0.");
    }

    void RestoreBaseAttributes()
    {
        AORPGDatabaseV11.RaceDef raceDef =
            AORPGDatabaseV11.GetRace(
                RaceId);

        int baseAttribute =
            AORPGDatabaseV11.BaseAttribute;

        strength =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.strength);

        agility =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.agility);

        intelligence =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.intelligence);

        constitution =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.constitution);

        charisma =
            baseAttribute +
            (raceDef == null
                ? 0
                : raceDef.charisma);

        potionSecondsRemaining = 0;
        nextPotionSecondAt = 0f;
    }

    void RecoverStaminaAO()
    {
        int survival =
            GetSkill(8);

        int luck;

        if (survival <= 10)
            luck = 5;
        else if (survival <= 20)
            luck = 7;
        else if (survival <= 30)
            luck = 9;
        else if (survival <= 40)
            luck = 11;
        else if (survival <= 50)
            luck = 13;
        else if (survival <= 60)
            luck = 15;
        else if (survival <= 70)
            luck = 17;
        else if (survival <= 80)
            luck = 19;
        else if (survival <= 90)
            luck = 21;
        else if (survival <= 99)
            luck = 23;
        else
            luck = 25;

        int percentageAmount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    maxStamina *
                    luck /
                    100f));

        int maxRecovery =
            ClassId == 9
            ? percentageAmount
            : Mathf.Max(
                1,
                Mathf.RoundToInt(
                    percentageAmount /
                    1.6f));

        int recovery =
            UnityEngine.Random.Range(
                1,
                maxRecovery + 1);

        currentStamina =
            Mathf.Min(
                maxStamina,
                currentStamina +
                recovery);
    }

    public bool TrySpendStamina(int amount)
    {
        if (currentStamina < 10)
            return false;

        currentStamina =
            Mathf.Max(
                0,
                currentStamina -
                Mathf.Max(
                    0,
                    amount));

        return true;
    }

    public int GetSkill(int skillId)
    {
        EnsureSkillArray();

        if (skillId < 1 ||
            skillId > skillValues.Length)
            return 0;

        return skillValues[
            skillId - 1];
    }

    public bool TryIncreaseSkill(int skillId)
    {
        EnsureSkillArray();

        if (skillPoints <= 0 ||
            skillId < 1 ||
            skillId > skillValues.Length)
            return false;

        int index =
            skillId - 1;

        if (skillValues[index] >=
            AORPGDatabaseV11.MaxSkill)
            return false;

        skillValues[index]++;
        skillPoints--;

        return true;
    }

    public int GetWeaponSkillId(
        AOInventoryV10 inventory)
    {
        AOItemDatabaseV10.ItemDef weapon =
            inventory == null
            ? null
            : inventory.GetWeapon();

        if (weapon == null)
            return 13;

        // e_WeaponType:
        // 2 Dagger, 3 Bow, 8 Knuckle, 11 GunPowder.
        if (weapon.weaponType == 8)
            return 13;

        if (weapon.weaponType == 3 ||
            weapon.weaponType == 11)
            return 12;

        if (weapon.weaponType == 2)
            return 6;

        return 4;
    }

    public int GetAttackPower(
        AOInventoryV10 inventory)
    {
        AORPGDatabaseV11.ClassDef classDef =
            AORPGDatabaseV11.GetClass(
                ClassId);

        if (classDef == null)
            return 0;

        int skillId =
            GetWeaponSkillId(
                inventory);

        int skill =
            GetSkill(
                skillId);

        AOItemDatabaseV10.ItemDef weapon =
            inventory == null
            ? null
            : inventory.GetWeapon();

        float modifier =
            weapon != null &&
            (weapon.weaponType == 3 ||
             weapon.weaponType == 11)
            ? classDef.attackProjectiles
            : classDef.attackWeapons;

        float temp =
            (skill +
             ((3f * skill / 100f) *
              agility)) *
            modifier;

        return Mathf.RoundToInt(
            temp +
            2.5f *
            Mathf.Max(
                level - 12,
                0));
    }

    public int GetEvasionPower()
    {
        AORPGDatabaseV11.ClassDef classDef =
            AORPGDatabaseV11.GetClass(
                ClassId);

        if (classDef == null)
            return 0;

        int tactics =
            GetSkill(3);

        float result =
            (tactics +
             ((3f * tactics / 100f) *
              agility)) *
            classDef.evasion;

        result +=
            2.5f *
            Mathf.Max(
                level - 12,
                0);

        return Mathf.RoundToInt(
            result);
    }

    public float GetDamageModifier(
        AOInventoryV10 inventory)
    {
        AORPGDatabaseV11.ClassDef classDef =
            AORPGDatabaseV11.GetClass(
                ClassId);

        if (classDef == null)
            return 1f;

        AOItemDatabaseV10.ItemDef weapon =
            inventory == null
            ? null
            : inventory.GetWeapon();

        if (weapon == null)
            return classDef.damageWrestling;

        if (weapon.projectile > 0 ||
            weapon.weaponType == 3 ||
            weapon.weaponType == 11)
        {
            return classDef.damageProjectiles;
        }

        if (weapon.weaponType == 8)
            return classDef.damageWrestling;

        return classDef.damageWeapons;
    }

    public bool CanEquip(
        AOItemDatabaseV10.ItemDef item,
        out string reason)
    {
        reason = "";

        if (item == null)
        {
            reason = "Objeto desconocido.";
            return false;
        }

        if (item.minLevel > 0 &&
            level < item.minLevel)
        {
            reason =
                "Requiere nivel " +
                item.minLevel + ".";
            return false;
        }

        if (item.maxLevel > 0 &&
            level > item.maxLevel)
        {
            reason =
                "Sólo puede usarse hasta nivel " +
                item.maxLevel + ".";
            return false;
        }

        if (Contains(
                item.prohibitedClasses,
                ClassId))
        {
            reason =
                "Tu clase no puede usar " +
                item.name + ".";
            return false;
        }

        if (Contains(
                item.prohibitedRaces,
                RaceId))
        {
            reason =
                "Tu raza no puede usar " +
                item.name + ".";
            return false;
        }

        if (item.raceWhitelistMask != 0)
        {
            int bit =
                1 <<
                (RaceId - 1);

            if ((item.raceWhitelistMask &
                 bit) == 0)
            {
                reason =
                    "Este objeto no está habilitado para tu raza.";
                return false;
            }
        }

        if (item.maleOnly &&
            gender != Gender.Hombre)
        {
            reason =
                "Este objeto requiere personaje masculino.";
            return false;
        }

        if (item.femaleOnly &&
            gender != Gender.Mujer)
        {
            reason =
                "Este objeto requiere personaje femenino.";
            return false;
        }

        if (item.requiredSkillIndex > 0 &&
            GetSkill(
                item.requiredSkillIndex) <
            item.requiredSkillValue)
        {
            AORPGDatabaseV11.SkillDef skill =
                AORPGDatabaseV11.GetSkill(
                    item.requiredSkillIndex);

            reason =
                "Requiere " +
                (skill == null
                    ? "skill " +
                      item.requiredSkillIndex
                    : skill.name) +
                " " +
                item.requiredSkillValue +
                ".";
            return false;
        }

        return true;
    }

    static bool Contains(
        int[] values,
        int target)
    {
        if (values == null)
            return false;

        foreach (int value in values)
        {
            if (value == target)
                return true;
        }

        return false;
    }

    void EnsureSkillArray()
    {
        if (skillValues == null ||
            skillValues.Length != 24)
        {
            int[] old =
                skillValues;

            skillValues =
                new int[24];

            if (old != null)
            {
                Array.Copy(
                    old,
                    skillValues,
                    Mathf.Min(
                        old.Length,
                        skillValues.Length));
            }
        }
    }
}
