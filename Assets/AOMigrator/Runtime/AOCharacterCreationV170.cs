using System;
using UnityEngine;

[DisallowMultipleComponent]
public class AOCharacterCreationV170 : MonoBehaviour
{
    static AOCharacterCreationV170 instance;

    public static bool ModalOpen =>
        instance != null &&
        instance.visible;

#if UNITY_EDITOR
    public void HideForVisualQA()
    {
        returnToMenuNextFrame = false;
        startSessionNextFrame = false;
        visible = false;
        enabled = false;
    }
#endif

    bool visible;

    AOSaveGameV140 save;

    string characterName = "";

    int raceId = 1;
    int genderId = 1;
    int classId = 3;
    int headId;

    bool overwriteWarning;
    bool overwriteConfirmed;

    string message = "";

    bool returnToMenuNextFrame;
    bool startSessionNextFrame;

    Vector2 classScroll;

    Rect window =
        new Rect(
            0f,
            0f,
            920f,
            670f);

    GUIStyle titleStyle;
    GUIStyle centerStyle;
    GUIStyle previewBox;

    void Awake()
    {
        instance = this;

        save =
            GetComponent
                <AOSaveGameV140>();

        ResetSelection();
    }

    void Update()
    {
        if (returnToMenuNextFrame)
        {
            returnToMenuNextFrame =
                false;

            visible = false;

            AOMainMenuV140
                .ShowFromCreator();

            return;
        }

        if (startSessionNextFrame)
        {
            startSessionNextFrame =
                false;

            visible = false;

            AOMainMenuV140
                .StartSessionFromCreator();
        }
    }

    public void Open(
        bool existingSave)
    {
        if (save == null)
            save =
                GetComponent
                    <AOSaveGameV140>();

        returnToMenuNextFrame = false;
        startSessionNextFrame = false;
        visible = true;
        overwriteWarning =
            existingSave;

        overwriteConfirmed =
            !existingSave;

        message = "";

        ResetSelection();

        Time.timeScale = 0f;
    }

    public void Close()
    {
        // No cambiamos de modal durante OnGUI.
        // La transición ocurre en Update en el siguiente frame.
        message = "";
        returnToMenuNextFrame = true;
    }

    void ResetSelection()
    {
        raceId = 1;
        genderId = 1;
        classId = 3;

        headId =
            AOCharacterVisualDatabaseV111
                .DefaultHead(
                    raceId,
                    genderId);

        if (headId <= 0)
        {
            int[] heads =
                AOCharacterVisualDatabaseV111
                    .ValidHeads(
                        raceId,
                        genderId);

            if (heads.Length > 0)
                headId = heads[0];
        }

        characterName = "";
    }

    void OnGUI()
    {
        if (!visible)
            return;

        EnsureStyles();

        window.width =
            Mathf.Min(
                920f,
                Screen.width - 20f);

        window.height =
            Mathf.Min(
                670f,
                Screen.height - 20f);

        window.x =
            (Screen.width -
             window.width) *
            0.5f;

        window.y =
            (Screen.height -
             window.height) *
            0.5f;

        window =
            GUI.ModalWindow(
                170170,
                window,
                Draw,
                "Crear personaje");
    }

    void Draw(
        int id)
    {
        bool transitionPending =
            returnToMenuNextFrame ||
            startSessionNextFrame;

        GUILayout.BeginVertical();

        GUILayout.Label(
            "CREACIÓN DE PERSONAJE",
            titleStyle,
            GUILayout.Height(
                34));

        GUILayout.BeginHorizontal();

        DrawLeftColumn();

        GUILayout.Space(
            10);

        DrawCenterColumn();

        GUILayout.Space(
            10);

        DrawRightColumn();

        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();

        if (!string.IsNullOrEmpty(
                message))
        {
            GUILayout.Box(
                message,
                GUILayout.Height(
                    38));
        }

        if (overwriteWarning &&
            !overwriteConfirmed)
        {
            GUILayout.Box(
                "Ya existe una partida guardada. " +
                "Crear un personaje nuevo reemplazará el slot actual.",
                GUILayout.Height(
                    42));
        }

        GUILayout.BeginHorizontal();

        GUI.enabled =
            !transitionPending;

        if (GUILayout.Button(
                "Volver",
                GUILayout.Height(
                    38),
                GUILayout.Width(
                    130)))
        {
            Close();
        }

        GUILayout.FlexibleSpace();

        string createLabel =
            overwriteWarning &&
            !overwriteConfirmed
            ? "Reemplazar partida"
            : "Crear personaje";

        if (GUILayout.Button(
                createLabel,
                GUILayout.Height(
                    42),
                GUILayout.Width(
                    190)))
        {
            if (overwriteWarning &&
                !overwriteConfirmed)
            {
                overwriteConfirmed =
                    true;

                message =
                    "Confirmá nuevamente para crear el personaje.";
            }
            else
            {
                CreateCharacter();
            }
        }

        GUI.enabled = true;

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUI.DragWindow(
            new Rect(
                0f,
                0f,
                window.width,
                24f));
    }

    void DrawLeftColumn()
    {
        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(
                265));

        GUILayout.Label(
            "Identidad");

        GUILayout.Label(
            "Nombre");

        characterName =
            GUILayout.TextField(
                characterName ?? "",
                18,
                GUILayout.Height(
                    28));

        GUILayout.Space(
            8);

        GUILayout.Label(
            "Género");

        GUILayout.BeginHorizontal();

        if (ToggleButton(
                "Hombre",
                genderId == 1))
        {
            if (genderId != 1)
            {
                genderId = 1;
                ResetHead();
            }
        }

        if (ToggleButton(
                "Mujer",
                genderId == 2))
        {
            if (genderId != 2)
            {
                genderId = 2;
                ResetHead();
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(
            8);

        GUILayout.Label(
            "Raza");

        for (int row = 0;
             row < 2;
             row++)
        {
            GUILayout.BeginHorizontal();

            for (int col = 0;
                 col < 3;
                 col++)
            {
                int id =
                    row * 3 +
                    col +
                    1;

                AORPGDatabaseV11.RaceDef race =
                    AORPGDatabaseV11.GetRace(
                        id);

                string name =
                    race == null
                    ? "Raza " +
                      id
                    : race.name;

                if (ToggleButton(
                        name,
                        raceId == id))
                {
                    if (raceId != id)
                    {
                        raceId = id;
                        ResetHead();
                    }
                }
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.Space(
            8);

        AORPGDatabaseV11.RaceDef selectedRace =
            AORPGDatabaseV11.GetRace(
                raceId);

        if (selectedRace != null)
        {
            GUILayout.Box(
                "FUE " +
                (18 +
                 selectedRace.strength) +
                "  AGI " +
                (18 +
                 selectedRace.agility) +
                "\nINT " +
                (18 +
                 selectedRace.intelligence) +
                "  CON " +
                (18 +
                 selectedRace.constitution) +
                "  CAR " +
                (18 +
                 selectedRace.charisma));
        }

        GUILayout.Space(
            8);

        GUILayout.Label(
            "Ciudad inicial");

        GUILayout.Box(
            "Ullathorpe\nMapa 1 — 57,44");

        GUILayout.EndVertical();
    }

    void DrawCenterColumn()
    {
        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(
                280));

        GUILayout.Label(
            "Apariencia");

        Rect preview =
            GUILayoutUtility.GetRect(
                250f,
                310f);

        GUI.Box(
            preview,
            GUIContent.none,
            previewBox);

        DrawPreview(
            preview);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "<",
                GUILayout.Width(
                    45),
                GUILayout.Height(
                    32)))
        {
            CycleHead(
                -1);
        }

        GUILayout.FlexibleSpace();

        GUILayout.Label(
            "Cabeza " +
            headId,
            centerStyle,
            GUILayout.Width(
                130));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(
                ">",
                GUILayout.Width(
                    45),
                GUILayout.Height(
                    32)))
        {
            CycleHead(
                1);
        }

        GUILayout.EndHorizontal();

        int[] heads =
            AOCharacterVisualDatabaseV111
                .ValidHeads(
                    raceId,
                    genderId);

        GUILayout.Label(
            heads.Length +
            " cabezas válidas para esta combinación.",
            centerStyle);

        GUILayout.EndVertical();
    }

    void DrawRightColumn()
    {
        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(
                315));

        GUILayout.Label(
            "Clase");

        classScroll =
            GUILayout.BeginScrollView(
                classScroll,
                GUILayout.Height(
                    300));

        for (int id = 1;
             id <= 12;
             id++)
        {
            AORPGDatabaseV11.ClassDef cls =
                AORPGDatabaseV11.GetClass(
                    id);

            string label =
                cls == null
                ? "Clase " +
                  id
                : cls.name;

            if (GUILayout.Button(
                    (classId == id
                        ? "▶ "
                        : "") +
                    label,
                    GUILayout.Height(
                        30)))
            {
                classId = id;
            }
        }

        GUILayout.EndScrollView();

        AORPGDatabaseV11.ClassDef selectedClass =
            AORPGDatabaseV11.GetClass(
                classId);

        if (selectedClass != null)
        {
            GUILayout.Box(
                "Vida " +
                selectedClass.life
                    .ToString(
                        "0.##") +
                " | Mana inicial " +
                selectedClass.initialMana
                    .ToString(
                        "0.##") +
                "\nAtaque armas " +
                selectedClass.attackWeapons
                    .ToString(
                        "0.##") +
                " | Proyectiles " +
                selectedClass.attackProjectiles
                    .ToString(
                        "0.##"));
        }

        GUILayout.Label(
            "Equipo inicial");

        GUILayout.Box(
            AOInitialLoadoutV170
                .Describe(
                    classId),
            GUILayout.MinHeight(
                120));

        GUILayout.EndVertical();
    }

    void DrawPreview(
        Rect rect)
    {
        if (!AOCharacterVisualDatabaseV111
            .TryBuildBase(
                raceId,
                genderId,
                headId,
                out AOCharacterRenderer.DirectionVisual[]
                    dirs,
                out float headX,
                out float headY,
                out float bodyX))
        {
            GUI.Label(
                rect,
                "Preview no disponible.",
                centerStyle);
            return;
        }

        AOCharacterRenderer.DirectionVisual south =
            null;

        foreach (
            AOCharacterRenderer.DirectionVisual dir
            in dirs)
        {
            if (dir != null &&
                dir.heading ==
                    AOGridMap.SOUTH)
            {
                south = dir;
                break;
            }
        }

        if (south == null)
            return;

        Sprite body =
            south.body != null &&
            south.body.Length > 0
            ? south.body[0]
            : null;

        Sprite head =
            south.head != null &&
            south.head.Length > 0
            ? south.head[0]
            : null;

        float scale = 2.7f;

        Vector2 basePoint =
            new Vector2(
                rect.center.x +
                bodyX *
                32f *
                scale,
                rect.yMax -
                28f);

        DrawSprite(
            body,
            basePoint,
            scale);

        Vector2 headPoint =
            new Vector2(
                basePoint.x +
                headX *
                32f *
                scale,
                basePoint.y -
                headY *
                32f *
                scale);

        DrawSprite(
            head,
            headPoint,
            scale);
    }

    static void DrawSprite(
        Sprite sprite,
        Vector2 bottomCenter,
        float scale)
    {
        if (sprite == null ||
            sprite.texture == null)
            return;

        Rect tr =
            sprite.textureRect;

        float width =
            tr.width *
            scale;

        float height =
            tr.height *
            scale;

        Rect target =
            new Rect(
                bottomCenter.x -
                width *
                0.5f,
                bottomCenter.y -
                height,
                width,
                height);

        Rect uv =
            new Rect(
                tr.x /
                sprite.texture.width,
                tr.y /
                sprite.texture.height,
                tr.width /
                sprite.texture.width,
                tr.height /
                sprite.texture.height);

        GUI.DrawTextureWithTexCoords(
            target,
            sprite.texture,
            uv,
            true);
    }

    bool ToggleButton(
        string text,
        bool selected)
    {
        return GUILayout.Button(
            (selected
                ? "▶ "
                : "") +
            text,
            GUILayout.Height(
                30));
    }

    void ResetHead()
    {
        headId =
            AOCharacterVisualDatabaseV111
                .DefaultHead(
                    raceId,
                    genderId);

        if (headId <= 0)
        {
            int[] heads =
                AOCharacterVisualDatabaseV111
                    .ValidHeads(
                        raceId,
                        genderId);

            if (heads.Length > 0)
                headId = heads[0];
        }
    }

    void CycleHead(
        int delta)
    {
        int[] heads =
            AOCharacterVisualDatabaseV111
                .ValidHeads(
                    raceId,
                    genderId);

        if (heads == null ||
            heads.Length == 0)
            return;

        int current = 0;

        for (int i = 0;
             i < heads.Length;
             i++)
        {
            if (heads[i] ==
                headId)
            {
                current = i;
                break;
            }
        }

        current =
            (current +
             delta +
             heads.Length) %
            heads.Length;

        headId =
            heads[current];
    }

    void CreateCharacter()
    {
        message = "";

        if (!ValidateName(
                characterName,
                out string cleanName,
                out string reason))
        {
            message =
                reason;
            return;
        }

        if (!AOCharacterVisualDatabaseV111
            .IsValidHead(
                raceId,
                genderId,
                headId))
        {
            message =
                "La cabeza elegida no es válida para la raza/género.";
            return;
        }

        if (save == null)
            save =
                GetComponent
                    <AOSaveGameV140>();

        if (save == null)
        {
            message =
                "No encuentro el sistema Save/Load.";
            return;
        }

        if (!save.NewGameWithCharacter(
                cleanName,
                raceId,
                genderId,
                classId,
                headId))
        {
            message =
                "No pude crear el personaje. Revisá la Console.";
            return;
        }

        // Evita destruir/cambiar el ModalWindow mientras
        // Unity todavía está procesando su grupo GUILayout.
        startSessionNextFrame = true;
    }

    public static bool ValidateName(
        string raw,
        out string clean,
        out string reason)
    {
        clean =
            (raw ?? "")
                .Trim();

        reason = "";

        if (clean.Length < 3 ||
            clean.Length > 18)
        {
            reason =
                "El nombre debe tener entre 3 y 18 caracteres.";
            return false;
        }

        bool lastSpace = false;

        for (int i = 0;
             i < clean.Length;
             i++)
        {
            char c =
                char.ToUpperInvariant(
                    clean[i]);

            bool isLetter =
                c >= 'A' &&
                c <= 'Z';

            bool isSpace =
                c == ' ';

            if (!isLetter &&
                !isSpace)
            {
                reason =
                    "Usá sólo letras A-Z y espacios.";
                return false;
            }

            if (isSpace &&
                lastSpace)
            {
                reason =
                    "No uses dos espacios seguidos.";
                return false;
            }

            lastSpace =
                isSpace;
        }

        return true;
    }

    void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle =
            new GUIStyle(
                GUI.skin.label);

        titleStyle.alignment =
            TextAnchor.MiddleCenter;

        titleStyle.fontSize = 18;
        titleStyle.fontStyle =
            FontStyle.Bold;

        centerStyle =
            new GUIStyle(
                GUI.skin.label);

        centerStyle.alignment =
            TextAnchor.MiddleCenter;

        centerStyle.wordWrap =
            true;

        previewBox =
            new GUIStyle(
                GUI.skin.box);
    }
}
