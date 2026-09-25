using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class AOQuestSystemV150 : MonoBehaviour
{
    public const int MAX_ACTIVE_QUESTS = 5;

    [Serializable]
    public class ActiveQuestSave
    {
        public int questId;
        public int[] npcKills;
        public int[] npcTargets;
    }

    [Serializable]
    public class SaveState
    {
        public ActiveQuestSave[] active;
        public int[] completed;
        public int[] completedTimes;   // pares (questId, veces); opcional: los guardados viejos no lo traen
        public int trackedQuestId;
    }

    readonly List<ActiveQuestSave>
        active =
            new List<ActiveQuestSave>();

    readonly HashSet<int>
        completed =
            new HashSet<int>();

    // Cuántas veces se completó cada misión: el servidor paga cada vez una sola vez (RequestQuestReward).
    readonly Dictionary<int, int>
        completedTimes =
            new Dictionary<int, int>();

    int trackedQuestId;

    AOPlayerRPGV11 rpg;
    AOInventoryV10 inventory;
    AOPlayerCombatV09 combat;
    AOPlayerMagicV120 magic;
    AOCityBankV130 bank;
    AOQuestUIV150 ui;
    AOSaveGameV140 save;

    public int ActiveCount =>
        active.Count;

    public int CompletedCount =>
        completed.Count;

    public int TrackedQuestId =>
        trackedQuestId;

    void Awake()
    {
        FindReferences();
    }

    void FindReferences()
    {
        if (rpg == null)
            rpg =
                GetComponent
                    <AOPlayerRPGV11>();

        if (inventory == null)
            inventory =
                GetComponent
                    <AOInventoryV10>();

        if (combat == null)
            combat =
                GetComponent
                    <AOPlayerCombatV09>();

        if (magic == null)
            magic =
                GetComponent
                    <AOPlayerMagicV120>();

        if (bank == null)
            bank =
                GetComponent
                    <AOCityBankV130>();

        if (ui == null)
            ui =
                GetComponent
                    <AOQuestUIV150>();

        if (save == null)
            save =
                GetComponent
                    <AOSaveGameV140>();
    }

    public AOQuestDatabaseV150.QuestDef
        GetActiveQuestAt(
            int index)
    {
        if (index < 0 ||
            index >= active.Count)
            return null;

        return AOQuestDatabaseV150.Get(
            active[index].questId);
    }

    public bool IsActive(
        int questId)
    {
        return FindActive(
            questId) !=
            null;
    }

    public bool IsCompleted(
        int questId)
    {
        return completed.Contains(
            questId);
    }

    public void TrackQuest(
        int questId)
    {
        if (questId <= 0 ||
            !IsActive(questId))
        {
            trackedQuestId = 0;
            return;
        }

        trackedQuestId =
            questId;
    }

    public bool TryOpenForNpc(
        int npcIndex,
        AOCityNPCDatabaseV130.NPCDef npc)
    {
        FindReferences();

        if (ui == null ||
            npc == null)
            return false;

        List<int> offered =
            new List<int>();

        if (npc.questNumbers != null)
        {
            foreach (
                int questId in
                npc.questNumbers)
            {
                if (AOQuestDatabaseV150.Get(
                        questId) !=
                    null)
                {
                    offered.Add(
                        questId);
                }
            }
        }

        List<int> turnIns =
            new List<int>();

        foreach (
            ActiveQuestSave state in
            active)
        {
            AOQuestDatabaseV150.QuestDef quest =
                AOQuestDatabaseV150.Get(
                    state.questId);

            if (quest == null)
                continue;

            bool offeredHere =
                offered.Contains(
                    quest.id);

            bool talkToHere =
                quest.talkTo > 0 &&
                quest.talkTo ==
                    npcIndex;

            if (offeredHere ||
                talkToHere)
            {
                turnIns.Add(
                    quest.id);
            }
        }

        bool questNpc =
            npc.npcType == 17;

        if (!questNpc &&
            offered.Count == 0 &&
            turnIns.Count == 0)
        {
            return false;
        }

        ui.OpenNpcQuests(
            npc,
            offered.ToArray(),
            turnIns.ToArray());

        return true;
    }

    public void NotifyNpcKilled(
        int npcIndex)
    {
        if (npcIndex <= 0)
            return;

        foreach (
            ActiveQuestSave state in
            active)
        {
            AOQuestDatabaseV150.QuestDef quest =
                AOQuestDatabaseV150.Get(
                    state.questId);

            if (quest == null ||
                quest.requiredNpcs == null)
                continue;

            EnsureProgressArrays(
                state,
                quest);

            bool changed = false;

            for (int i = 0;
                 i < quest.requiredNpcs.Length;
                 i++)
            {
                AOQuestDatabaseV150.Requirement req =
                    quest.requiredNpcs[i];

                if (req == null ||
                    req.index !=
                        npcIndex)
                    continue;

                int before =
                    state.npcKills[i];

                state.npcKills[i] =
                    Mathf.Min(
                        req.amount,
                        state.npcKills[i] +
                        1);

                if (state.npcKills[i] >
                    before)
                {
                    changed = true;

                    AOCityNPCDatabaseV130.NPCDef npc =
                        AOCityNPCDatabaseV130.Get(
                            npcIndex);

                    AOInterfaceV0101.PushMessage(
                        quest.name +
                        ": " +
                        (npc == null
                            ? "NPC " +
                              npcIndex
                            : npc.name) +
                        " " +
                        state.npcKills[i] +
                        "/" +
                        req.amount);
                }
            }

            if (changed &&
                IsQuestComplete(
                    quest.id))
            {
                AOInterfaceV0101.PushMessage(
                    quest.name +
                    ": objetivos completos. Volvé a entregarla.");
            }
        }
    }

    public void NotifyNpcInteracted(
        int npcIndex)
    {
        if (npcIndex <= 0)
            return;

        foreach (
            ActiveQuestSave state in
            active)
        {
            AOQuestDatabaseV150.QuestDef quest =
                AOQuestDatabaseV150.Get(
                    state.questId);

            if (quest == null ||
                quest.requiredTargetNpcs == null)
                continue;

            EnsureProgressArrays(
                state,
                quest);

            for (int i = 0;
                 i < quest.requiredTargetNpcs.Length;
                 i++)
            {
                AOQuestDatabaseV150.Requirement req =
                    quest.requiredTargetNpcs[i];

                if (req == null ||
                    req.index !=
                        npcIndex)
                    continue;

                int before =
                    state.npcTargets[i];

                state.npcTargets[i] =
                    Mathf.Min(
                        req.amount,
                        state.npcTargets[i] +
                        1);

                if (state.npcTargets[i] >
                    before)
                {
                    AOCityNPCDatabaseV130.NPCDef npc =
                        AOCityNPCDatabaseV130.Get(
                            npcIndex);

                    AOInterfaceV0101.PushMessage(
                        quest.name +
                        ": visitaste a " +
                        (npc == null
                            ? "NPC " +
                              npcIndex
                            : npc.name) +
                        " " +
                        state.npcTargets[i] +
                        "/" +
                        req.amount);
                }
            }
        }
    }

    public bool CanAccept(
        int questId,
        out string reason)
    {
        FindReferences();

        reason = "";

        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                questId);

        if (quest == null)
        {
            reason =
                "Misión inexistente.";
            return false;
        }

        if (IsActive(
                questId))
        {
            reason =
                "Ya tenés esta misión en curso.";
            return false;
        }

        if (active.Count >=
            MAX_ACTIVE_QUESTS)
        {
            reason =
                "Sólo podés tener " +
                MAX_ACTIVE_QUESTS +
                " misiones activas.";
            return false;
        }

        if (quest.requiredQuest > 0 &&
            !IsCompleted(
                quest.requiredQuest))
        {
            AOQuestDatabaseV150.QuestDef previous =
                AOQuestDatabaseV150.Get(
                    quest.requiredQuest);

            reason =
                "Primero debés completar: " +
                (previous == null
                    ? "Quest " +
                      quest.requiredQuest
                    : previous.name) +
                ".";
            return false;
        }

        if (rpg == null)
        {
            reason =
                "No encuentro el personaje RPG.";
            return false;
        }

        if (rpg.Level <
            quest.requiredLevel)
        {
            reason =
                "Requiere nivel " +
                quest.requiredLevel +
                ".";
            return false;
        }

        if (quest.limitLevel > 0 &&
            rpg.Level >
                quest.limitLevel)
        {
            reason =
                "Tu nivel es demasiado alto para esta misión.";
            return false;
        }

        if (quest.workerOnly &&
            rpg.ClassId != 9)
        {
            reason =
                "Esta misión es sólo para Trabajador.";
            return false;
        }

        if (quest.requiredClasses != null &&
            quest.requiredClasses.Length > 0)
        {
            bool allowed = false;

            foreach (
                int classId in
                quest.requiredClasses)
            {
                if (classId ==
                    rpg.ClassId)
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
            {
                reason =
                    "Esta misión no está disponible para tu clase.";
                return false;
            }
        }

        if (quest.requiredSkillIndex > 0 &&
            rpg.GetSkill(
                quest.requiredSkillIndex) <
                quest.requiredSkillValue)
        {
            AORPGDatabaseV11.SkillDef skill =
                AORPGDatabaseV11.GetSkill(
                    quest.requiredSkillIndex);

            reason =
                "Requiere " +
                (skill == null
                    ? "skill " +
                      quest.requiredSkillIndex
                    : skill.name) +
                " " +
                quest.requiredSkillValue +
                ".";
            return false;
        }

        if (!quest.repeatable &&
            IsCompleted(
                questId))
        {
            reason =
                "Ya completaste esta misión.";
            return false;
        }

        // El prototipo no implementa facciones/global events todavía.
        // No se ignoran silenciosamente: las quests quedan bloqueadas.
        if (quest.permittedFactions > 0)
        {
            reason =
                "Esta misión depende del sistema de facciones, aún no disponible.";
            return false;
        }

        if (quest.globalQuestIndex > 0)
        {
            reason =
                "Esta misión depende de una Global Quest/Event, no disponible en single-player.";
            return false;
        }

        return true;
    }

    public bool AcceptQuest(
        int questId,
        out string message)
    {
        message = "";

        if (!CanAccept(
                questId,
                out message))
            return false;

        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                questId);

        ActiveQuestSave state =
            new ActiveQuestSave {
                questId = questId,
                npcKills =
                    new int[
                        quest.requiredNpcs == null
                        ? 0
                        : quest.requiredNpcs.Length],
                npcTargets =
                    new int[
                        quest.requiredTargetNpcs == null
                        ? 0
                        : quest.requiredTargetNpcs.Length]
            };

        active.Add(
            state);

        if (trackedQuestId <= 0)
            trackedQuestId =
                questId;

        message =
            "Misión aceptada: " +
            quest.name;

        AOInterfaceV0101.PushMessage(
            message);

        SaveAfterChange();

        return true;
    }

    public bool CanTurnIn(
        int questId,
        out string reason)
    {
        FindReferences();

        reason = "";

        ActiveQuestSave state =
            FindActive(
                questId);

        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                questId);

        if (state == null ||
            quest == null)
        {
            reason =
                "No tenés esta misión activa.";
            return false;
        }

        EnsureProgressArrays(
            state,
            quest);

        if (quest.requiredObjects != null)
        {
            foreach (
                AOQuestDatabaseV150.Requirement req
                in quest.requiredObjects)
            {
                if (req == null)
                    continue;

                if (inventory == null ||
                    inventory.CountItem(
                        req.index) <
                        req.amount)
                {
                    AOItemDatabaseV10.ItemDef item =
                        AOItemDatabaseV10.Get(
                            req.index);

                    reason =
                        "Falta " +
                        (item == null
                            ? "OBJ " +
                              req.index
                            : item.name) +
                        " " +
                        (inventory == null
                            ? 0
                            : inventory.CountItem(
                                req.index)) +
                        "/" +
                        req.amount +
                        ".";

                    return false;
                }
            }
        }

        if (quest.requiredNpcs != null)
        {
            for (int i = 0;
                 i < quest.requiredNpcs.Length;
                 i++)
            {
                AOQuestDatabaseV150.Requirement req =
                    quest.requiredNpcs[i];

                if (req == null)
                    continue;

                if (state.npcKills[i] <
                    req.amount)
                {
                    reason =
                        "Todavía faltan criaturas por derrotar.";
                    return false;
                }
            }
        }

        if (quest.requiredSpells != null)
        {
            foreach (
                int spellId in
                quest.requiredSpells)
            {
                if (magic == null ||
                    !magic.KnowsSpell(
                        spellId))
                {
                    AOSpellDatabaseV120.SpellDef spell =
                        AOSpellDatabaseV120.Get(
                            spellId);

                    reason =
                        "Necesitás aprender " +
                        (spell == null
                            ? "hechizo " +
                              spellId
                            : spell.name) +
                        ".";

                    return false;
                }
            }
        }

        if (quest.requiredSkillIndex > 0 &&
            rpg.GetSkill(
                quest.requiredSkillIndex) <
                quest.requiredSkillValue)
        {
            reason =
                "Todavía no cumplís el skill requerido.";
            return false;
        }

        if (quest.requiredTargetNpcs != null)
        {
            for (int i = 0;
                 i < quest.requiredTargetNpcs.Length;
                 i++)
            {
                AOQuestDatabaseV150.Requirement req =
                    quest.requiredTargetNpcs[i];

                if (req == null)
                    continue;

                if (state.npcTargets[i] <
                    req.amount)
                {
                    reason =
                        "Todavía te falta visitar al NPC indicado.";
                    return false;
                }
            }
        }

        int rewardObjectCount =
            quest.rewardObjects == null
            ? 0
            : quest.rewardObjects.Length;

        if (rewardObjectCount > 0 &&
            inventory.FreeSlotCountPublic() <
                rewardObjectCount)
        {
            reason =
                "No tenés suficientes slots libres para recibir la recompensa.";
            return false;
        }

        if (quest.rewardSpells != null &&
            quest.rewardSpells.Length > 0)
        {
            if (magic == null)
            {
                reason =
                    "No encuentro el spellbook.";
                return false;
            }

            int unknown = 0;

            foreach (
                int spellId in
                quest.rewardSpells)
            {
                if (!magic.KnowsSpell(
                        spellId))
                    unknown++;
            }

            if (unknown == 0)
            {
                reason =
                    "Ya conocés todos los hechizos que entrega esta misión.";
                return false;
            }

            if (magic.KnownSpellCount +
                unknown >
                AOSpellDatabaseV120.MaxUserSpells)
            {
                reason =
                    "No tenés espacio para los hechizos de recompensa.";
                return false;
            }
        }

        return true;
    }

    public bool TurnInQuest(
        int questId,
        out string message)
    {
        message = "";

        if (!CanTurnIn(
                questId,
                out message))
            return false;

        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                questId);

        if (quest.requiredObjects != null)
        {
            foreach (
                AOQuestDatabaseV150.Requirement req
                in quest.requiredObjects)
            {
                if (req == null)
                    continue;

                if (!inventory
                    .RemoveItemByIndexPublic(
                        req.index,
                        req.amount))
                {
                    message =
                        "No pude retirar los objetos requeridos. " +
                        "Si alguno está equipado, quitátelo.";
                    return false;
                }
            }
        }

        if (quest.requiredSpells != null &&
            magic != null)
        {
            foreach (
                int spellId in
                quest.requiredSpells)
            {
                magic.RemoveKnownSpell(
                    spellId);
            }
        }

        if (quest.rewardExp > 0 &&
            rpg != null)
        {
            rpg.AddExperience(
                quest.rewardExp);
        }

        bool onlineGoldReward =
            quest.rewardGold > 0 &&
            AOOnlineClientV240.Requested;

        if (quest.rewardGold > 0 &&
            combat != null &&
            !onlineGoldReward)
        {
            // Igual que el servidor: recompensas grandes van al banco.
            if (quest.rewardGold >=
                    100000 &&
                bank != null)
            {
                bank.AddGoldReward(
                    quest.rewardGold);
            }
            else
            {
                combat.AddGold(
                    quest.rewardGold);
            }
        }

        if (quest.rewardObjects != null)
        {
            foreach (
                AOQuestDatabaseV150.Requirement reward
                in quest.rewardObjects)
            {
                if (reward == null)
                    continue;

                inventory.AddItem(
                    reward.index,
                    reward.amount);
            }
        }

        if (quest.rewardSpells != null &&
            magic != null)
        {
            foreach (
                int spellId in
                quest.rewardSpells)
            {
                if (!magic.KnowsSpell(
                        spellId))
                {
                    magic.LearnSpell(
                        spellId);
                }
            }
        }

        ActiveQuestSave state =
            FindActive(
                questId);

        if (state != null)
            active.Remove(
                state);

        completed.Add(
            questId);

        int times =
            TimesCompleted(
                questId) + 1;

        completedTimes[questId] =
            times;

        if (trackedQuestId ==
            questId)
        {
            trackedQuestId =
                active.Count > 0
                ? active[0].questId
                : 0;
        }

        message =
            string.IsNullOrWhiteSpace(
                quest.finalDescription)
            ? "Misión completada: " +
              quest.name
            : quest.finalDescription;

        AOInterfaceV0101.PushMessage(
            "Misión completada: " +
            quest.name);

        SaveAfterChange();

        // Después de guardar: el servidor revisa la misión completada en el estado que acaba de recibir.
        if (onlineGoldReward)
            AOOnlineClientV240.RequestQuestReward(
                questId,
                times);

        return true;
    }

    public int TimesCompleted(
        int questId)
    {
        if (completedTimes.TryGetValue(
                questId,
                out int times))
            return times;

        return completed.Contains(
            questId)
            ? 1
            : 0;
    }

    public bool NeedsQuestItem(
        int questId,
        int itemIndex,
        out int remaining)
    {
        FindReferences();

        remaining = 0;

        if (itemIndex <= 0 ||
            !IsActive(
                questId))
            return false;

        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                questId);

        if (quest == null ||
            quest.requiredObjects == null)
            return false;

        int required = 0;

        foreach (
            AOQuestDatabaseV150.Requirement req in
            quest.requiredObjects)
        {
            if (req != null &&
                req.index ==
                    itemIndex)
            {
                required +=
                    Mathf.Max(
                        0,
                        req.amount);
            }
        }

        if (required <= 0)
            return false;

        int have =
            inventory == null
            ? 0
            : inventory.CountItem(
                itemIndex);

        remaining =
            Mathf.Max(
                0,
                required -
                have);

        return remaining > 0;
    }

    public int GetNpcQuestSymbolState(
        int npcIndex,
        int[] offeredQuestIds)
    {
        if (npcIndex <= 0)
            return 0;

        bool hasQuestContext =
            false;

        bool available =
            false;

        bool pending =
            false;

        bool ready =
            false;

        if (offeredQuestIds != null)
        {
            foreach (
                int questId in
                offeredQuestIds)
            {
                AOQuestDatabaseV150.QuestDef quest =
                    AOQuestDatabaseV150.Get(
                        questId);

                if (quest == null)
                    continue;

                hasQuestContext =
                    true;

                if (IsActive(
                        questId))
                {
                    if (CanTurnIn(
                            questId,
                            out _))
                    {
                        ready = true;
                    }
                    else
                    {
                        pending = true;
                    }
                }
                else if (CanAccept(
                             questId,
                             out _))
                {
                    available = true;
                }
            }
        }

        // TalkTo puede hacer que el símbolo aparezca en un NPC
        // distinto del que ofreció la misión.
        foreach (
            ActiveQuestSave state in
            active)
        {
            AOQuestDatabaseV150.QuestDef quest =
                AOQuestDatabaseV150.Get(
                    state.questId);

            if (quest == null ||
                quest.talkTo !=
                    npcIndex)
                continue;

            hasQuestContext =
                true;

            if (CanTurnIn(
                    quest.id,
                    out _))
            {
                ready = true;
            }
            else
            {
                pending = true;
            }
        }

        // Prioridad original del servidor:
        // disponible=1, pendiente=4, finalizada=3.
        if (ready)
            return 3;

        if (pending)
            return 4;

        if (available)
            return 1;

        // El servidor usa 2 para quest existente pero no disponible.
        if (hasQuestContext)
            return 2;

        return 0;
    }

    public bool IsQuestComplete(
        int questId)
    {
        return CanTurnIn(
            questId,
            out _);
    }

    public string GetProgressText(
        int questId)
    {
        FindReferences();

        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                questId);

        ActiveQuestSave state =
            FindActive(
                questId);

        if (quest == null)
            return "";

        if (state != null)
            EnsureProgressArrays(
                state,
                quest);

        StringBuilder sb =
            new StringBuilder();

        if (quest.requiredNpcs != null)
        {
            for (int i = 0;
                 i < quest.requiredNpcs.Length;
                 i++)
            {
                var req =
                    quest.requiredNpcs[i];

                if (req == null)
                    continue;

                AOCityNPCDatabaseV130.NPCDef npc =
                    AOCityNPCDatabaseV130.Get(
                        req.index);

                int have =
                    state == null ||
                    i >= state.npcKills.Length
                    ? 0
                    : state.npcKills[i];

                sb.AppendLine(
                    "Matar: " +
                    (npc == null
                        ? "NPC " +
                          req.index
                        : npc.name) +
                    " " +
                    have +
                    "/" +
                    req.amount);
            }
        }

        if (quest.requiredObjects != null)
        {
            foreach (
                var req in
                quest.requiredObjects)
            {
                if (req == null)
                    continue;

                AOItemDatabaseV10.ItemDef item =
                    AOItemDatabaseV10.Get(
                        req.index);

                int have =
                    inventory == null
                    ? 0
                    : inventory.CountItem(
                        req.index);

                sb.AppendLine(
                    "Objeto: " +
                    (item == null
                        ? "OBJ " +
                          req.index
                        : item.name) +
                    " " +
                    Mathf.Min(
                        have,
                        req.amount) +
                    "/" +
                    req.amount);
            }
        }

        if (quest.requiredTargetNpcs != null)
        {
            for (int i = 0;
                 i < quest.requiredTargetNpcs.Length;
                 i++)
            {
                var req =
                    quest.requiredTargetNpcs[i];

                if (req == null)
                    continue;

                AOCityNPCDatabaseV130.NPCDef npc =
                    AOCityNPCDatabaseV130.Get(
                        req.index);

                int have =
                    state == null ||
                    i >= state.npcTargets.Length
                    ? 0
                    : state.npcTargets[i];

                sb.AppendLine(
                    "Visitar: " +
                    (npc == null
                        ? "NPC " +
                          req.index
                        : npc.name) +
                    " " +
                    have +
                    "/" +
                    req.amount);
            }
        }

        if (quest.requiredSpells != null)
        {
            foreach (
                int spellId in
                quest.requiredSpells)
            {
                AOSpellDatabaseV120.SpellDef spell =
                    AOSpellDatabaseV120.Get(
                        spellId);

                bool have =
                    magic != null &&
                    magic.KnowsSpell(
                        spellId);

                sb.AppendLine(
                    "Hechizo: " +
                    (spell == null
                        ? "Spell " +
                          spellId
                        : spell.name) +
                    " [" +
                    (have
                        ? "OK"
                        : "FALTA") +
                    "]");
            }
        }

        if (quest.requiredSkillIndex > 0)
        {
            AORPGDatabaseV11.SkillDef skill =
                AORPGDatabaseV11.GetSkill(
                    quest.requiredSkillIndex);

            int have =
                rpg == null
                ? 0
                : rpg.GetSkill(
                    quest.requiredSkillIndex);

            sb.AppendLine(
                "Skill: " +
                (skill == null
                    ? quest.requiredSkillIndex.ToString()
                    : skill.name) +
                " " +
                have +
                "/" +
                quest.requiredSkillValue);
        }

        if (sb.Length == 0)
            sb.Append(
                "Sin objetivos contables.");

        return sb.ToString().TrimEnd();
    }

    public string GetRewardsText(
        int questId)
    {
        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                questId);

        if (quest == null)
            return "";

        StringBuilder sb =
            new StringBuilder();

        if (quest.rewardExp > 0)
            sb.AppendLine(
                "EXP: " +
                quest.rewardExp);

        if (quest.rewardGold > 0)
            sb.AppendLine(
                "Oro: " +
                quest.rewardGold);

        if (quest.rewardObjects != null)
        {
            foreach (
                var reward in
                quest.rewardObjects)
            {
                if (reward == null)
                    continue;

                AOItemDatabaseV10.ItemDef item =
                    AOItemDatabaseV10.Get(
                        reward.index);

                sb.AppendLine(
                    "Objeto: " +
                    (item == null
                        ? "OBJ " +
                          reward.index
                        : item.name) +
                    " x" +
                    reward.amount);
            }
        }

        if (quest.rewardSpells != null)
        {
            foreach (
                int spellId in
                quest.rewardSpells)
            {
                AOSpellDatabaseV120.SpellDef spell =
                    AOSpellDatabaseV120.Get(
                        spellId);

                sb.AppendLine(
                    "Hechizo: " +
                    (spell == null
                        ? "Spell " +
                          spellId
                        : spell.name));
            }
        }

        if (sb.Length == 0)
            return "Sin recompensa configurada.";

        return sb.ToString().TrimEnd();
    }

    public string GetAvailabilityText(
        int questId)
    {
        if (IsActive(
                questId))
        {
            if (CanTurnIn(
                    questId,
                    out _))
                return "LISTA PARA ENTREGAR";

            return "EN CURSO";
        }

        if (CanAccept(
                questId,
                out string reason))
            return "DISPONIBLE";

        return reason;
    }

    public SaveState CaptureSaveState()
    {
        SaveState data =
            new SaveState();

        data.active =
            new ActiveQuestSave[
                active.Count];

        for (int i = 0;
             i < active.Count;
             i++)
        {
            ActiveQuestSave src =
                active[i];

            data.active[i] =
                new ActiveQuestSave {
                    questId =
                        src.questId,
                    npcKills =
                        src.npcKills == null
                        ? new int[0]
                        : (int[])src.npcKills.Clone(),
                    npcTargets =
                        src.npcTargets == null
                        ? new int[0]
                        : (int[])src.npcTargets.Clone()
                };
        }

        int[] done =
            new int[
                completed.Count];

        completed.CopyTo(
            done);

        Array.Sort(
            done);

        data.completed =
            done;

        data.completedTimes =
            new int[
                done.Length * 2];

        for (int i = 0;
             i < done.Length;
             i++)
        {
            data.completedTimes[i * 2] =
                done[i];
            data.completedTimes[i * 2 + 1] =
                TimesCompleted(
                    done[i]);
        }

        data.trackedQuestId =
            trackedQuestId;

        return data;
    }

    public void RestoreSaveState(
        SaveState data)
    {
        active.Clear();
        completed.Clear();
        completedTimes.Clear();
        trackedQuestId = 0;

        if (data == null)
            return;

        if (data.completedTimes != null)
        {
            for (int i = 0;
                 i + 1 < data.completedTimes.Length;
                 i += 2)
            {
                if (data.completedTimes[i + 1] > 0)
                    completedTimes[data.completedTimes[i]] =
                        data.completedTimes[i + 1];
            }
        }

        if (data.completed != null)
        {
            foreach (
                int questId in
                data.completed)
            {
                if (AOQuestDatabaseV150.Get(
                        questId) !=
                    null)
                {
                    completed.Add(
                        questId);
                }
            }
        }

        if (data.active != null)
        {
            foreach (
                ActiveQuestSave src in
                data.active)
            {
                if (src == null ||
                    active.Count >=
                        MAX_ACTIVE_QUESTS)
                    continue;

                AOQuestDatabaseV150.QuestDef quest =
                    AOQuestDatabaseV150.Get(
                        src.questId);

                if (quest == null)
                    continue;

                ActiveQuestSave state =
                    new ActiveQuestSave {
                        questId =
                            src.questId,
                        npcKills =
                            src.npcKills == null
                            ? new int[0]
                            : (int[])src.npcKills.Clone(),
                        npcTargets =
                            src.npcTargets == null
                            ? new int[0]
                            : (int[])src.npcTargets.Clone()
                    };

                EnsureProgressArrays(
                    state,
                    quest);

                active.Add(
                    state);
            }
        }

        if (IsActive(
                data.trackedQuestId))
        {
            trackedQuestId =
                data.trackedQuestId;
        }
        else if (active.Count > 0)
        {
            trackedQuestId =
                active[0].questId;
        }
    }

    public void ClearAllForNewGame()
    {
        active.Clear();
        completed.Clear();
        completedTimes.Clear();
        trackedQuestId = 0;
    }

    ActiveQuestSave FindActive(
        int questId)
    {
        foreach (
            ActiveQuestSave state in
            active)
        {
            if (state.questId ==
                questId)
                return state;
        }

        return null;
    }

    static void EnsureProgressArrays(
        ActiveQuestSave state,
        AOQuestDatabaseV150.QuestDef quest)
    {
        int kills =
            quest.requiredNpcs == null
            ? 0
            : quest.requiredNpcs.Length;

        if (state.npcKills == null ||
            state.npcKills.Length !=
                kills)
        {
            int[] old =
                state.npcKills;

            state.npcKills =
                new int[kills];

            if (old != null)
            {
                Array.Copy(
                    old,
                    state.npcKills,
                    Mathf.Min(
                        old.Length,
                        state.npcKills.Length));
            }
        }

        int targets =
            quest.requiredTargetNpcs == null
            ? 0
            : quest.requiredTargetNpcs.Length;

        if (state.npcTargets == null ||
            state.npcTargets.Length !=
                targets)
        {
            int[] old =
                state.npcTargets;

            state.npcTargets =
                new int[targets];

            if (old != null)
            {
                Array.Copy(
                    old,
                    state.npcTargets,
                    Mathf.Min(
                        old.Length,
                        state.npcTargets.Length));
            }
        }
    }

    void SaveAfterChange()
    {
        FindReferences();

        if (save != null &&
            AOMainMenuV140.SessionActive)
        {
            save.SaveGame(
                false);
        }
    }
}
