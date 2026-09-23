// Journal overlay for the local Unity client.
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class AOQuestUIV150 : MonoBehaviour
{
    enum Mode
    {
        None,
        Journal,
        NPC
    }

    static AOQuestUIV150 instance;

#if UNITY_EDITOR
    public static bool VisualQAOverride;
    public static int VisualQADrawCalls;
#endif

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        instance = null;
#if UNITY_EDITOR
        VisualQAOverride = false;
        VisualQADrawCalls = 0;
#endif
    }

    public static bool ModalOpen =>
        instance != null &&
        instance.mode !=
            Mode.None;

    Mode mode;

    AOQuestSystemV150 quests;

    AOCityNPCDatabaseV130.NPCDef
        currentNpc;

    readonly List<int>
        npcQuestIds =
            new List<int>();

    int selectedQuestId;

    Vector2 listScroll;
    Vector2 detailScroll;

    string message = "";
    bool closeNextFrame;

    Rect window =
        new Rect(
            80,
            60,
            880,
            620);

    GUIStyle trackerStyle;

    void Awake()
    {
        instance = this;

        quests =
            GetComponent
                <AOQuestSystemV150>();
    }

    void Update()
    {
        if (closeNextFrame)
        {
            closeNextFrame = false;
            Close();
            return;
        }

        if (!AOMainMenuV140.SessionActive)
            return;

        if (PressedQuestJournal() &&
            !AOInterfaceV0101.InputCaptured)
        {
            if (mode ==
                Mode.Journal)
            {
                Close();
            }
            else
            {
                OpenJournal();
            }
        }

        if (mode !=
                Mode.None &&
            PressedEscape())
        {
            Close();
        }
    }

    public void OpenNpcQuests(
        AOCityNPCDatabaseV130.NPCDef npc,
        int[] offered,
        int[] turnIns)
    {
        if (quests == null)
            quests =
                GetComponent
                    <AOQuestSystemV150>();

        currentNpc = npc;
        mode = Mode.NPC;

        npcQuestIds.Clear();

        AddQuestIds(
            turnIns);

        AddQuestIds(
            offered);

        selectedQuestId =
            npcQuestIds.Count > 0
            ? npcQuestIds[0]
            : 0;

        message =
            npc == null
            ? ""
            : npc.description;

        listScroll = Vector2.zero;
        detailScroll = Vector2.zero;
    }

    public void OpenJournal()
    {
        if (quests == null)
            quests =
                GetComponent
                    <AOQuestSystemV150>();

        currentNpc = null;
        mode = Mode.Journal;

        selectedQuestId =
            quests != null &&
            quests.ActiveCount > 0
            ? quests.GetActiveQuestAt(
                0).id
            : 0;

        message =
            "Misiones activas: " +
            (quests == null
                ? 0
                : quests.ActiveCount) +
            "/" +
            AOQuestSystemV150
                .MAX_ACTIVE_QUESTS;

        listScroll = Vector2.zero;
        detailScroll = Vector2.zero;
    }

    void AddQuestIds(
        int[] ids)
    {
        if (ids == null)
            return;

        foreach (int id in ids)
        {
            if (id > 0 &&
                !npcQuestIds.Contains(
                    id))
            {
                npcQuestIds.Add(
                    id);
            }
        }
    }

    public void Close()
    {
        mode = Mode.None;
        currentNpc = null;
        npcQuestIds.Clear();
        selectedQuestId = 0;
        message = "";
    }

    void OnGUI()
    {
        if (!AOMainMenuV140.SessionActive
#if UNITY_EDITOR
            && !VisualQAOverride
#endif
           )
            return;

#if UNITY_EDITOR
        if (VisualQAOverride && mode != Mode.None)
            VisualQADrawCalls++;
#endif

        if (mode !=
            Mode.None)
        {
            DrawModal();
        }
        else
        {
            DrawTracker();
        }
    }

    void DrawModal()
    {
        GUISkin previousSkin = GUI.skin;
        int previousDepth = GUI.depth;
        GUI.depth = -90;
        GUI.skin = AOClassicSkinV200.Get(previousSkin);

        window.width =
            Mathf.Min(
                880f,
                Screen.width - 20f);

        window.height =
            Mathf.Min(
                620f,
                Screen.height - 20f);

        window.x =
            Mathf.Clamp(
                window.x,
                10f,
                Screen.width -
                    window.width -
                    10f);

        window.y =
            Mathf.Clamp(
                window.y,
                10f,
                Screen.height -
                    window.height -
                    10f);

        window =
            GUI.ModalWindow(
                150150,
                window,
                DrawWindow,
                mode == Mode.NPC &&
                currentNpc != null
                ? currentNpc.name +
                  " — Misiones"
                : "Misiones");
        GUI.skin = previousSkin;
        GUI.depth = previousDepth;
    }

    void DrawWindow(
        int id)
    {
        if (quests == null)
        {
            GUILayout.Label(
                "AOQuestSystemV150 no encontrado.");
            return;
        }

        GUILayout.BeginVertical();

        if (!string.IsNullOrWhiteSpace(
                message))
        {
            GUILayout.Box(
                message,
                GUILayout.Height(
                    48));
        }

        GUILayout.BeginHorizontal();

        DrawQuestList();

        GUILayout.Space(
            8);

        DrawQuestDetails();

        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();

        GUILayout.Label(
            "Q = diario | Esc = cerrar");

        GUILayout.FlexibleSpace();

        if (AOAudioV190.Clicked(GUILayout.Button(
                "Cerrar",
                GUILayout.Width(
                    110))))
        {
            closeNextFrame = true;
        }

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUI.DragWindow(
            new Rect(
                0,
                0,
                window.width,
                26));
    }

    void DrawQuestList()
    {
        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(
                330));

        GUILayout.Label(
            mode == Mode.NPC
            ? "Misiones de este NPC"
            : "Activas");

        listScroll =
            GUILayout.BeginScrollView(
                listScroll,
                GUILayout.Height(
                    465));

        if (mode ==
            Mode.NPC)
        {
            foreach (
                int questId in
                npcQuestIds)
            {
                DrawQuestButton(
                    questId);
            }
        }
        else
        {
            for (int i = 0;
                 i < quests.ActiveCount;
                 i++)
            {
                AOQuestDatabaseV150.QuestDef quest =
                    quests.GetActiveQuestAt(
                        i);

                if (quest != null)
                {
                    DrawQuestButton(
                        quest.id);
                }
            }

            GUILayout.Space(
                12);

            GUILayout.Label(
                "Completadas: " +
                quests.CompletedCount);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    void DrawQuestButton(
        int questId)
    {
        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                questId);

        if (quest == null)
            return;

        string state =
            quests.GetAvailabilityText(
                questId);

        bool selected =
            selectedQuestId ==
            questId;

        if (AOAudioV190.Clicked(GUILayout.Button(
                (selected
                    ? "▶ "
                    : "") +
                quest.name +
                "\n" +
                state,
                GUILayout.Height(
                    48))))
        {
            selectedQuestId =
                questId;

            message = "";
        }
    }

    void DrawQuestDetails()
    {
        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(
                510));

        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                selectedQuestId);

        if (quest == null)
        {
            GUILayout.Label(
                "Seleccioná una misión.");
            GUILayout.EndVertical();
            return;
        }

        GUILayout.Label(
            quest.name);

        GUILayout.Label(
            "Quest #" +
            quest.id +
            (quest.repeatable
                ? " | Repetible"
                : "") +
            (quest.requiredLevel > 0
                ? " | Nv " +
                  quest.requiredLevel +
                  "+"
                : ""));

        detailScroll =
            GUILayout.BeginScrollView(
                detailScroll,
                GUILayout.Height(
                    365));

        GUILayout.Box(
            string.IsNullOrWhiteSpace(
                quest.description)
            ? "(Sin descripción)"
            : quest.description);

        GUILayout.Space(
            8);

        GUILayout.Label(
            "Objetivos");

        GUILayout.Box(
            quests.GetProgressText(
                quest.id));

        GUILayout.Space(
            8);

        GUILayout.Label(
            "Recompensas");

        GUILayout.Box(
            quests.GetRewardsText(
                quest.id));

        if (quest.talkTo > 0)
        {
            AOCityNPCDatabaseV130.NPCDef target =
                AOCityNPCDatabaseV130.Get(
                    quest.talkTo);

            GUILayout.Label(
                "Entrega: " +
                (target == null
                    ? "NPC " +
                      quest.talkTo
                    : target.name));
        }

        if (quest.positionMap > 0)
        {
            GUILayout.Label(
                "Mapa indicado: " +
                quest.positionMap);
        }

        GUILayout.EndScrollView();

        string status =
            quests.GetAvailabilityText(
                quest.id);

        GUILayout.Box(
            status,
            GUILayout.Height(
                32));

        GUILayout.BeginHorizontal();

        if (!quests.IsActive(
                quest.id))
        {
            GUI.enabled =
                quests.CanAccept(
                    quest.id,
                    out _);

            if (AOAudioV190.Clicked(GUILayout.Button(
                    "Aceptar",
                    GUILayout.Height(
                        36))))
            {
                if (quests.AcceptQuest(
                        quest.id,
                        out string result))
                {
                    message =
                        result;
                }
                else
                {
                    message =
                        result;
                }
            }
        }
        else
        {
            if (AOAudioV190.Clicked(GUILayout.Button(
                    quests.TrackedQuestId ==
                        quest.id
                    ? "Siguiendo"
                    : "Seguir",
                    GUILayout.Height(
                        36))))
            {
                quests.TrackQuest(
                    quest.id);
            }

            bool correctTurnInNpc =
                mode == Mode.NPC &&
                currentNpc != null &&
                (
                    quest.talkTo ==
                        currentNpc.npcIndex ||
                    NpcOffersQuest(
                        currentNpc,
                        quest.id)
                );

            GUI.enabled =
                correctTurnInNpc &&
                quests.CanTurnIn(
                    quest.id,
                    out _);

            if (AOAudioV190.Clicked(GUILayout.Button(
                    "Entregar",
                    GUILayout.Height(
                        36))))
            {
                if (quests.TurnInQuest(
                        quest.id,
                        out string result))
                {
                    message =
                        result;
                }
                else
                {
                    message =
                        result;
                }
            }
        }

        GUI.enabled = true;

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    static bool NpcOffersQuest(
        AOCityNPCDatabaseV130.NPCDef npc,
        int questId)
    {
        if (npc == null ||
            npc.questNumbers == null)
            return false;

        foreach (
            int id in
            npc.questNumbers)
        {
            if (id ==
                questId)
                return true;
        }

        return false;
    }

    void DrawTracker()
    {
        if (quests == null)
            quests =
                GetComponent
                    <AOQuestSystemV150>();

        if (quests == null ||
            quests.TrackedQuestId <= 0)
            return;

        AOQuestDatabaseV150.QuestDef quest =
            AOQuestDatabaseV150.Get(
                quests.TrackedQuestId);

        if (quest == null)
            return;

        if (trackerStyle == null)
        {
            trackerStyle =
                new GUIStyle(
                    GUI.skin.box);

            trackerStyle.alignment =
                TextAnchor.UpperLeft;

            trackerStyle.wordWrap =
                true;

            trackerStyle.fontSize =
                11;
        }

        string progress =
            quests.GetProgressText(
                quest.id);

        Rect rect =
            new Rect(
                18f,
                Screen.height -
                    225f,
                300f,
                100f);

        GUI.Box(
            rect,
            quest.name +
            "\n" +
            progress,
            trackerStyle);
    }

    bool PressedQuestJournal()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.qKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(
            KeyCode.Q);
#endif
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
