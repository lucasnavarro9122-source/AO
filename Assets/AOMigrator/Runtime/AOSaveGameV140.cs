using System;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class AOSaveGameV140 : MonoBehaviour
{
    public const int SCHEMA_VERSION = 1;

    [Serializable]
    public class CharacterSave
    {
        public string name;
        public int homeCityId = 1;
        public int homeMap = 1;
        public int homeX = 57;
        public int homeY = 44;
    }

    [Serializable]
    public class RPGSave
    {
        public int raceId;
        public int genderId;
        public int classId;

        public int level;
        public long experience;
        public int skillPoints;
        public int[] skills;

        public int maxHp;
        public int mana;
        public int maxStamina;
        public int stamina;

        public int hunger;
        public int maxHunger;
        public int thirst;
        public int maxThirst;

        public int headIndex;
    }

    [Serializable]
    public class CombatSave
    {
        public int hp;
        public long gold;
        public bool dead;
    }

    [Serializable]
    public class InventorySave
    {
        public int[] itemIndices;
        public int[] amounts;

        public int weapon;
        public int armor;
        public int shield;
        public int helmet;
        public int amulet;
        public int magicAccessory;
    }

    [Serializable]
    public class MagicSave
    {
        public int[] learnedSpells;
        public int selectedSpell;
    }

    [Serializable]
    public class WorldSave
    {
        public int map;
        public int x;
        public int y;
        public int heading;
    }

    [Serializable]
    public class BankSave
    {
        public int[] itemIndices;
        public int[] amounts;
        public long gold;
    }

    [Serializable]
    public class MerchantStockSave
    {
        public int[] npcIds;
        public int[] itemIds;
        public int[] amounts;
    }

    [Serializable]
    public class SaveData
    {
        public int schemaVersion =
            SCHEMA_VERSION;

        public string gameVersion =
            "0.17.0-alpha";

        public string savedAtUtc;

        public CharacterSave character =
            new CharacterSave();

        public RPGSave rpg =
            new RPGSave();

        public CombatSave combat =
            new CombatSave();

        public InventorySave inventory =
            new InventorySave();

        public MagicSave magic =
            new MagicSave();

        public WorldSave world =
            new WorldSave();

        public BankSave bank =
            new BankSave();

        public MerchantStockSave merchantStock =
            new MerchantStockSave();

        public AOQuestSystemV150.SaveState quests =
            new AOQuestSystemV150.SaveState();
    }

    AOTestPlayer player;
    AOPlayerRPGV11 rpg;
    AOPlayerCombatV09 combat;
    AOInventoryV10 inventory;
    AOPlayerMagicV120 magic;
    AOWorldManagerV07 world;
    AOCharacterProfileVisualV111 profileVisual;
    AOCityBankV130 bank;
    AOCityNPCSystemV130 city;
    AOQuestSystemV150 quests;
    AOCharacterIdentityV170 identity;

    float nextAutosaveAt;

    string SaveDirectory =>
        Path.Combine(
            Application.persistentDataPath,
            "AO_Demo");

    string SavePath =>
        Path.Combine(
            SaveDirectory,
            "save_slot_1.json");

    string BackupPath =>
        Path.Combine(
            SaveDirectory,
            "save_slot_1.bak.json");

    public bool HasSave =>
        File.Exists(
            SavePath) ||
        File.Exists(
            BackupPath);

    void Awake()
    {
        FindReferences();

        nextAutosaveAt =
            Time.unscaledTime +
            60f;
    }

    void Update()
    {
        if (!AOMainMenuV140.SessionActive)
            return;

        if (!AOInterfaceV0101.InputCaptured)
        {
            if (PressedQuickSave())
            {
                SaveGame(
                    true);
            }

            if (PressedQuickLoad())
            {
                LoadGame(
                    true);
            }
        }

        if (Time.unscaledTime >=
            nextAutosaveAt)
        {
            nextAutosaveAt =
                Time.unscaledTime +
                60f;

            SaveGame(
                false);
        }
    }

    void FindReferences()
    {
        if (player == null)
            player =
                GetComponent
                    <AOTestPlayer>();

        if (rpg == null)
            rpg =
                GetComponent
                    <AOPlayerRPGV11>();

        if (combat == null)
            combat =
                GetComponent
                    <AOPlayerCombatV09>();

        if (inventory == null)
            inventory =
                GetComponent
                    <AOInventoryV10>();

        if (magic == null)
            magic =
                GetComponent
                    <AOPlayerMagicV120>();

        if (profileVisual == null)
        {
            profileVisual =
                GetComponent
                    <AOCharacterProfileVisualV111>();

            if (profileVisual == null)
            {
                profileVisual =
                    GetComponentInChildren
                        <AOCharacterProfileVisualV111>(true);
            }
        }

        if (bank == null)
            bank =
                GetComponent
                    <AOCityBankV130>();

        if (city == null)
            city =
                GetComponent
                    <AOCityNPCSystemV130>();

        if (quests == null)
            quests =
                GetComponent
                    <AOQuestSystemV150>();

        if (identity == null)
            identity =
                GetComponent
                    <AOCharacterIdentityV170>();

        if (world == null)
        {
            world =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOWorldManagerV07>();
        }
    }

    public bool SaveGame(
        bool notify)
    {
        try
        {
            FindReferences();

            if (!ReadyForSave())
            {
                if (notify)
                {
                    AOInterfaceV0101.PushMessage(
                        "Todavía no puedo guardar: faltan sistemas del jugador.");
                }

                return false;
            }

            SaveData data =
                Capture();

            Directory.CreateDirectory(
                SaveDirectory);

            string json =
                JsonUtility.ToJson(
                    data,
                    true);

            string temp =
                SavePath +
                ".tmp";

            File.WriteAllText(
                temp,
                json);

            if (File.Exists(
                    SavePath))
            {
                File.Copy(
                    SavePath,
                    BackupPath,
                    true);
            }

            File.Copy(
                temp,
                SavePath,
                true);

            File.Delete(
                temp);

            if (bank != null)
                bank.Save();

            if (notify)
            {
                AOInterfaceV0101.PushMessage(
                    "Partida guardada.");
            }

            Debug.Log(
                "[AO v0.14] Save: " +
                SavePath);

            return true;
        }
        catch (
            Exception e)
        {
            Debug.LogError(
                "[AO v0.14] Error guardando: " +
                e);

            if (notify)
            {
                AOInterfaceV0101.PushMessage(
                    "Error al guardar: " +
                    e.Message);
            }

            return false;
        }
    }

    public bool LoadGame(
        bool notify)
    {
        try
        {
            FindReferences();

            if (!ReadyForSave())
            {
                if (notify)
                {
                    AOInterfaceV0101.PushMessage(
                        "Todavía no puedo cargar.");
                }

                return false;
            }

            SaveData data =
                ReadBestSave();

            if (data == null)
            {
                if (notify)
                {
                    AOInterfaceV0101.PushMessage(
                        "No hay una partida guardada válida.");
                }

                return false;
            }

            Apply(
                data);

            nextAutosaveAt =
                Time.unscaledTime +
                60f;

            if (notify)
            {
                AOInterfaceV0101.PushMessage(
                    "Partida cargada.");
            }

            Debug.Log(
                "[AO v0.14] Load OK.");

            return true;
        }
        catch (
            Exception e)
        {
            Debug.LogError(
                "[AO v0.14] Error cargando: " +
                e);

            if (notify)
            {
                AOInterfaceV0101.PushMessage(
                    "Error al cargar: " +
                    e.Message);
            }

            return false;
        }
    }

    public bool NewGame()
    {
        FindReferences();

        string fallbackName =
            identity == null
            ? "Aventurero"
            : identity.CharacterName;

        return NewGameWithCharacter(
            fallbackName,
            rpg == null
                ? 1
                : rpg.RaceId,
            rpg == null
                ? 1
                : rpg.GenderId,
            rpg == null
                ? 3
                : rpg.ClassId,
            profileVisual == null
                ? AOCharacterVisualDatabaseV111.DefaultHead(
                    rpg == null
                        ? 1
                        : rpg.RaceId,
                    rpg == null
                        ? 1
                        : rpg.GenderId)
                : profileVisual.HeadIndex);
    }

    public bool NewGameWithCharacter(
        string characterName,
        int raceId,
        int genderId,
        int classId,
        int headIndex,
        int homeCityId = 1)
    {
        try
        {
            FindReferences();

            if (!AOHomeCityV200.TryGet(homeCityId, out string homeName,
                                       out int homeMap, out int homeX,
                                       out int homeY))
                throw new Exception("Ciudad inicial inválida.");
            if (Resources.Load<TextAsset>(
                    "AOMigrator/WorldV07/Maps/map_" + homeMap) == null)
                throw new Exception("No está migrado el mapa de " + homeName + ".");

            if (identity == null)
            {
                identity =
                    gameObject
                        .AddComponent
                            <AOCharacterIdentityV170>();
            }

            identity.Configure(
                characterName,
                homeCityId,
                homeMap,
                homeX,
                homeY);

            if (rpg == null ||
                inventory == null ||
                magic == null ||
                combat == null ||
                world == null ||
                player == null)
            {
                throw new Exception(
                    "Faltan sistemas base para crear el personaje.");
            }

            rpg.InitializeProfile(
                raceId,
                genderId,
                classId,
                true);

            inventory.ClearForNewGame();

            magic.ClearSpellbookForNewGame();

            ClearTemporaryMagic();

            if (bank != null)
                bank.ClearAll();

            if (city != null)
                city.ResetFiniteStock();

            if (quests != null)
                quests.ClearAllForNewGame();

            combat.ResetForNewGame();

            if (profileVisual != null)
            {
                profileVisual.ApplyProfile(
                    rpg,
                    headIndex);

                profileVisual.SetHead(
                    headIndex);
            }

            if (!AOInitialLoadoutV170.Apply(
                    classId,
                    inventory,
                    magic,
                    out string loadoutResult))
            {
                throw new Exception(
                    loadoutResult);
            }

            if (profileVisual != null)
                profileVisual.SyncFromRPG();

            if (!world.MagicTeleport(
                    homeMap,
                    homeX,
                    homeY,
                    out string startResult))
            {
                throw new Exception(
                    "No pude iniciar en " + homeName + ": " +
                    startResult);
            }

            player.RestoreHeading(
                AOGridMap.SOUTH);

            nextAutosaveAt =
                Time.unscaledTime +
                60f;

            if (!SaveGame(false))
                throw new Exception("No pude guardar el personaje nuevo.");

            AOInterfaceV0101.PushMessage(
                "Bienvenido a " + homeName + ", " +
                identity.CharacterName +
                ".");

            Debug.Log(
                "[AO v0.17] Nuevo personaje: " +
                identity.CharacterName +
                " | raza=" +
                raceId +
                " género=" +
                genderId +
                " clase=" +
                classId +
                " head=" +
                headIndex);

            return true;
        }
        catch (
            Exception e)
        {
            Debug.LogError(
                "[AO v0.17] Error creando personaje: " +
                e);

            return false;
        }
    }

    public string GetSummary()
    {
        SaveData data =
            ReadBestSave();

        if (data == null)
            return "Sin partida guardada.";

        string className =
            data.rpg == null
            ? "-"
            : (
                AORPGDatabaseV11.GetClass(
                    data.rpg.classId)
                == null
                ? "Clase " +
                  data.rpg.classId
                : AORPGDatabaseV11.GetClass(
                    data.rpg.classId).name
            );

        string characterName =
            data.character == null ||
            string.IsNullOrWhiteSpace(
                data.character.name)
            ? "Aventurero"
            : data.character.name;

        return
            characterName +
            " — " +
            className +
            " Nv " +
            (data.rpg == null
                ? 1
                : data.rpg.level) +
            " | Mapa " +
            (data.world == null
                ? 1
                : data.world.map) +
            "\n" +
            (string.IsNullOrEmpty(
                data.savedAtUtc)
                ? ""
                : data.savedAtUtc);
    }

    public void DeleteSaveFiles()
    {
        TryDelete(
            SavePath);

        TryDelete(
            BackupPath);
    }

    SaveData Capture()
    {
        SaveData data =
            new SaveData();

        data.savedAtUtc =
            DateTime.UtcNow.ToString(
                "o");

        data.character.name = identity.CharacterName;
        data.character.homeCityId = identity.HomeCityId;
        data.character.homeMap = identity.HomeMap;
        data.character.homeX = identity.HomeX;
        data.character.homeY = identity.HomeY;

        data.rpg.raceId =
            rpg.RaceId;

        data.rpg.genderId =
            rpg.GenderId;

        data.rpg.classId =
            rpg.ClassId;

        data.rpg.level =
            rpg.Level;

        data.rpg.experience =
            rpg.Experience;

        data.rpg.skillPoints =
            rpg.SkillPoints;

        data.rpg.skills =
            new int[24];

        for (int i = 0;
             i < 24;
             i++)
        {
            data.rpg.skills[i] =
                rpg.GetSkill(
                    i + 1);
        }

        data.rpg.maxHp =
            rpg.MaxHP;

        data.rpg.mana =
            rpg.Mana;

        data.rpg.maxStamina =
            rpg.MaxStamina;

        data.rpg.stamina =
            rpg.Stamina;

        data.rpg.hunger =
            rpg.Hunger;

        data.rpg.maxHunger =
            rpg.MaxHunger;

        data.rpg.thirst =
            rpg.Thirst;

        data.rpg.maxThirst =
            rpg.MaxThirst;

        data.rpg.headIndex =
            profileVisual == null
            ? 0
            : profileVisual.HeadIndex;

        data.combat.hp =
            combat.HP;

        data.combat.gold =
            combat.Gold;

        data.combat.dead =
            combat.IsDead;

        data.inventory.itemIndices =
            new int[
                inventory.SlotCount];

        data.inventory.amounts =
            new int[
                inventory.SlotCount];

        for (int i = 0;
             i < inventory.SlotCount;
             i++)
        {
            data.inventory.itemIndices[i] =
                inventory.GetSlotItemIndex(
                    i);

            data.inventory.amounts[i] =
                inventory.GetSlotAmount(
                    i);
        }

        data.inventory.weapon =
            inventory.EquippedWeapon;

        data.inventory.armor =
            inventory.EquippedArmor;

        data.inventory.shield =
            inventory.EquippedShield;

        data.inventory.helmet =
            inventory.EquippedHelmet;

        data.inventory.amulet =
            inventory.EquippedAmulet;

        data.inventory.magicAccessory =
            inventory.EquippedMagicAccessory;

        if (magic != null)
        {
            data.magic.learnedSpells =
                magic.GetKnownSpellIdsCopy();

            data.magic.selectedSpell =
                magic.SelectedSpellId;
        }
        else
        {
            data.magic.learnedSpells =
                new int[0];
        }

        data.world.map =
            world.CurrentMapNumber;

        data.world.x =
            player.TileX;

        data.world.y =
            player.TileY;

        data.world.heading =
            player.Heading;

        if (bank != null)
        {
            data.bank.itemIndices =
                bank.CaptureItemIndices();

            data.bank.amounts =
                bank.CaptureAmounts();

            data.bank.gold =
                bank.BankGold;
        }

        if (city != null)
        {
            city.CaptureFiniteStock(
                out data.merchantStock.npcIds,
                out data.merchantStock.itemIds,
                out data.merchantStock.amounts);
        }

        if (quests != null)
        {
            data.quests =
                quests.CaptureSaveState();
        }

        return data;
    }

    void Apply(
        SaveData data)
    {
        ClearTemporaryMagic();

        if (identity == null)
        {
            identity =
                GetComponent
                    <AOCharacterIdentityV170>();
        }

        if (identity != null)
        {
            if (data.character != null)
            {
                identity.Configure(
                    data.character.name,
                    data.character.homeCityId <= 0
                        ? 1
                        : data.character.homeCityId,
                    data.character.homeMap <= 0
                        ? 1
                        : data.character.homeMap,
                    data.character.homeX <= 0
                        ? 57
                        : data.character.homeX,
                    data.character.homeY <= 0
                        ? 44
                        : data.character.homeY);
            }
            else
            {
                identity.Configure(
                    "Aventurero",
                    1,
                    1,
                    57,
                    44);
            }
        }

        if (data.rpg != null)
        {
            rpg.RestoreSaveState(
                data.rpg.raceId,
                data.rpg.genderId,
                data.rpg.classId,
                data.rpg.level,
                data.rpg.experience,
                data.rpg.skillPoints,
                data.rpg.skills,
                data.rpg.maxHp,
                data.rpg.mana,
                data.rpg.maxStamina,
                data.rpg.stamina,
                data.rpg.hunger,
                data.rpg.maxHunger,
                data.rpg.thirst,
                data.rpg.maxThirst);
        }

        if (data.inventory != null)
        {
            inventory.RestoreSaveState(
                data.inventory.itemIndices,
                data.inventory.amounts,
                data.inventory.weapon,
                data.inventory.armor,
                data.inventory.shield,
                data.inventory.helmet,
                data.inventory.amulet,
                data.inventory.magicAccessory);
        }

        if (profileVisual != null &&
            data.rpg != null &&
            data.rpg.headIndex > 0)
        {
            profileVisual.SetHead(
                data.rpg.headIndex);
        }

        if (magic != null &&
            data.magic != null)
        {
            magic.RestoreSpellbookForSave(
                data.magic.learnedSpells,
                data.magic.selectedSpell);
        }

        if (bank != null &&
            data.bank != null)
        {
            bank.RestoreSaveState(
                data.bank.itemIndices,
                data.bank.amounts,
                data.bank.gold);
        }

        if (city != null &&
            data.merchantStock != null)
        {
            city.RestoreFiniteStock(
                data.merchantStock.npcIds,
                data.merchantStock.itemIds,
                data.merchantStock.amounts);
        }

        if (quests != null)
        {
            quests.RestoreSaveState(
                data.quests);
        }

        if (combat != null &&
            data.combat != null)
        {
            combat.RestoreSaveState(
                data.combat.hp,
                data.combat.gold,
                data.combat.dead);
        }

        if (world != null &&
            data.world != null)
        {
            if (!world.MagicTeleport(
                    data.world.map,
                    data.world.x,
                    data.world.y,
                    out string teleportResult))
            {
                Debug.LogWarning(
                    "[AO v0.14] Posición save no restaurada: " +
                    teleportResult);
            }

            player.RestoreHeading(
                data.world.heading);
        }
    }

    void ClearTemporaryMagic()
    {
        AOMagicEffectRuntimeV129 effects =
            GetComponent
                <AOMagicEffectRuntimeV129>();

        if (effects != null)
            effects.ClearAll();

        AOPlayerMagicStatusV120 status =
            GetComponent
                <AOPlayerMagicStatusV120>();

        if (status != null)
        {
            status.RemoveDebuffs();
            status.RemoveInvisibility();
        }

        if (magic != null)
            magic.ResetRuntimeForLoad();
    }

    SaveData ReadBestSave()
    {
        SaveData data =
            TryRead(
                SavePath);

        if (data != null)
            return data;

        return TryRead(
            BackupPath);
    }

    SaveData TryRead(
        string path)
    {
        if (!File.Exists(
                path))
            return null;

        try
        {
            SaveData data =
                JsonUtility.FromJson
                    <SaveData>(
                        File.ReadAllText(
                            path));

            if (data == null ||
                data.schemaVersion <= 0 ||
                data.schemaVersion >
                    SCHEMA_VERSION)
            {
                return null;
            }

            return data;
        }
        catch
        {
            return null;
        }
    }

    bool ReadyForSave()
    {
        FindReferences();

        return
            player != null &&
            rpg != null &&
            combat != null &&
            inventory != null &&
            world != null &&
            quests != null &&
            identity != null;
    }

    static void TryDelete(
        string path)
    {
        try
        {
            if (File.Exists(
                    path))
            {
                File.Delete(
                    path);
            }
        }
        catch (
            Exception e)
        {
            Debug.LogWarning(
                "[AO v0.14] No pude borrar " +
                path +
                ": " +
                e.Message);
        }
    }

    bool PressedQuickSave()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.f1Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(
            KeyCode.F1);
#endif
    }

    bool PressedQuickLoad()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.f3Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(
            KeyCode.F3);
#endif
    }

    void OnApplicationPause(
        bool pause)
    {
        if (pause &&
            AOMainMenuV140.SessionActive)
        {
            SaveGame(
                false);
        }
    }

    void OnApplicationQuit()
    {
        if (AOMainMenuV140.SessionActive)
        {
            SaveGame(
                false);
        }
    }
}
