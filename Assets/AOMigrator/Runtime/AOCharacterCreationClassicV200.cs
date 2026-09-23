using System.Collections.Generic;
using UnityEngine;

public partial class AOCharacterCreationV170
{
    const float ClassicWidth = 1180f;
    const float ClassicHeight = 520f;

    static readonly string[] RaceIcons =
        { "Human", "Elf", "Drow", "Gnome", "Dwarf", "Orc" };
    static readonly string[] ClassIcons =
        { "Mage", "Cleric", "Warrior", "Assasin", "Bard", "Druid",
          "Paladin", "Hunter", "Worker", "Pirate", "Thief", "Bandit" };

    readonly Dictionary<string, Texture2D> classicTextures =
        new Dictionary<string, Texture2D>();

    GUIStyle classicTitle;
    GUIStyle classicSection;
    GUIStyle classicLabel;
    GUIStyle classicSmall;
    GUIStyle classicButton;
    GUIStyle classicSelected;
    GUIStyle classicName;

    Texture2D ClassicTexture(string name)
    {
        if (!classicTextures.TryGetValue(name, out Texture2D texture))
        {
            texture = Resources.Load<Texture2D>("AOMigrator/ClassicUI/" + name);
            classicTextures[name] = texture;
        }
        return texture;
    }

    void EnsureClassicStyles()
    {
        if (classicTitle != null)
            return;

        Font font = Resources.Load<Font>("AOMigrator/ClassicUI/Cardo-Regular");
        classicTitle = new GUIStyle(GUI.skin.label);
        classicTitle.font = font;
        classicTitle.fontSize = 27;
        classicTitle.fontStyle = FontStyle.Bold;
        classicTitle.alignment = TextAnchor.MiddleCenter;
        classicTitle.normal.textColor = new Color(0.91f, 0.78f, 0.52f);

        classicSection = new GUIStyle(classicTitle);
        classicSection.fontSize = 18;
        classicSection.alignment = TextAnchor.MiddleLeft;

        classicLabel = new GUIStyle(GUI.skin.label);
        classicLabel.font = font;
        classicLabel.fontSize = 14;
        classicLabel.wordWrap = true;
        classicLabel.normal.textColor = new Color(0.92f, 0.87f, 0.75f);

        classicSmall = new GUIStyle(classicLabel);
        classicSmall.fontSize = 11;
        classicSmall.alignment = TextAnchor.MiddleCenter;

        classicButton = new GUIStyle(GUI.skin.button);
        classicButton.font = font;
        classicButton.fontSize = 14;
        classicButton.alignment = TextAnchor.MiddleCenter;
        classicButton.normal.background = ClassicTexture("button_gray_default");
        classicButton.hover.background = ClassicTexture("button_gray_over");
        classicButton.active.background = ClassicTexture("button_gray_over");
        classicButton.normal.textColor = Color.white;
        classicButton.hover.textColor = Color.white;
        classicButton.active.textColor = Color.white;
        classicButton.border = new RectOffset(12, 12, 10, 10);

        classicSelected = new GUIStyle(classicButton);
        classicSelected.normal.background = ClassicTexture("button_red_default");
        classicSelected.hover.background = ClassicTexture("button_red_over");
        classicSelected.active.background = ClassicTexture("button_red_over");

        classicName = new GUIStyle(GUI.skin.textField);
        classicName.font = font;
        classicName.fontSize = 17;
        classicName.alignment = TextAnchor.MiddleLeft;
        classicName.normal.textColor = Color.white;
        classicName.focused.textColor = Color.white;
    }

    void DrawClassic()
    {
        EnsureClassicStyles();

        Matrix4x4 oldMatrix = GUI.matrix;
        int oldDepth = GUI.depth;
        bool oldEnabled = GUI.enabled;
        Color oldColor = GUI.color;

        GUI.depth = -100;
        GUI.color = new Color(0.025f, 0.021f, 0.018f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;

        float fit = Mathf.Min((Screen.width - 16f) / ClassicWidth,
                              (Screen.height - 16f) / ClassicHeight);
        float uiScale = Mathf.Max(0.25f, Mathf.Min(1f, fit));
        float left = (Screen.width - ClassicWidth * uiScale) * 0.5f;
        float top = (Screen.height - ClassicHeight * uiScale) * 0.5f;
        GUI.matrix = Matrix4x4.TRS(new Vector3(left, top, 0f),
                                   Quaternion.identity,
                                   new Vector3(uiScale, uiScale, 1f));

        DrawClassicPanel(new Rect(0, 0, ClassicWidth, ClassicHeight));
        GUI.Label(new Rect(18, 9, 1144, 30), "CREACIÓN DE PERSONAJE",
                  classicTitle);
        DrawClassicLeft();
        DrawClassicCenter();
        DrawClassicRight();
        DrawClassicFooter();

        GUI.enabled = oldEnabled;
        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
        GUI.depth = oldDepth;
    }

    void DrawClassicPanel(Rect rect)
    {
        Texture2D texture = ClassicTexture("frame");
        if (texture != null)
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        else
            GUI.Box(rect, GUIContent.none);
    }

    bool ClassicButton(Rect rect, string label, bool selected = false)
    {
        return AOAudioV190.Clicked(GUI.Button(rect, label,
                    selected ? classicSelected : classicButton));
    }

    bool ClassicIconButton(Rect rect, string textureName, string label,
                           bool selected)
    {
        bool clicked = AOAudioV190.Clicked(GUI.Button(rect,
            new GUIContent("", label), selected ? classicSelected : classicButton));
        Texture2D icon = ClassicTexture(textureName);
        if (icon != null)
            GUI.DrawTexture(new Rect(rect.center.x - 16f, rect.y + 5f, 32f, 32f),
                            icon, ScaleMode.ScaleToFit, true);
        GUI.Label(new Rect(rect.x + 2f, rect.yMax - 17f, rect.width - 4f, 15f),
                  label, classicSmall);
        return clicked;
    }

    void DrawClassicLeft()
    {
        DrawClassicPanel(new Rect(12, 44, 320, 420));
        GUI.Label(new Rect(32, 55, 270, 23), "GÉNERO", classicSection);
        if (ClassicButton(new Rect(32, 82, 130, 29), "Hombre", genderId == 1))
        {
            genderId = 1;
            ResetHead();
        }
        if (ClassicButton(new Rect(170, 82, 130, 29), "Mujer", genderId == 2))
        {
            genderId = 2;
            ResetHead();
        }

        GUI.Label(new Rect(32, 119, 270, 23), "RAZA", classicSection);
        for (int i = 0; i < 6; i++)
        {
            AORPGDatabaseV11.RaceDef race = AORPGDatabaseV11.GetRace(i + 1);
            string label = race == null ? RaceIcons[i] : race.name;
            Rect rect = new Rect(32 + (i % 3) * 94, 145 + (i / 3) * 59, 85, 55);
            string icon = "race_" + RaceIcons[i] + (genderId == 1 ? "0" : "1");
            if (ClassicIconButton(rect, icon, label, raceId == i + 1))
            {
                raceId = i + 1;
                ResetHead();
            }
        }

        GUI.Label(new Rect(32, 266, 270, 23), "ATRIBUTOS", classicSection);
        AORPGDatabaseV11.RaceDef chosen = AORPGDatabaseV11.GetRace(raceId);
        if (chosen != null)
        {
            string[] names = { "Fuerza", "Agilidad", "Inteligencia",
                               "Constitución", "Carisma" };
            int[] values = { chosen.strength, chosen.agility,
                             chosen.intelligence, chosen.constitution,
                             chosen.charisma };
            for (int i = 0; i < names.Length; i++)
                GUI.Label(new Rect(36, 291 + i * 18, 260, 20),
                          names[i] + "     " + (18 + values[i]), classicLabel);
        }

        GUI.Label(new Rect(32, 392, 270, 23), "NOMBRE", classicSection);
        characterName = GUI.TextField(new Rect(32, 416, 270, 28),
                                      characterName ?? "", 18, classicName);
        GUI.Label(new Rect(32, 445, 270, 14),
                  "3 a 18 letras. Sin números ni símbolos.", classicSmall);
    }

    void DrawClassicCenter()
    {
        DrawClassicPanel(new Rect(340, 44, 500, 420));
        GUI.Label(new Rect(360, 55, 460, 24), "APARIENCIA", classicSection);
        GUI.Box(new Rect(360, 83, 460, 310), GUIContent.none);
        DrawPreview(new Rect(365, 88, 450, 300));
        if (ClassicButton(new Rect(364, 400, 55, 30), "<")) CycleHead(-1);
        GUI.Label(new Rect(425, 400, 330, 30), "Cabeza " + headId,
                  classicSmall);
        if (ClassicButton(new Rect(761, 400, 55, 30), ">")) CycleHead(1);
        int[] heads = AOCharacterVisualDatabaseV111.ValidHeads(raceId, genderId);
        GUI.Label(new Rect(360, 432, 460, 23),
                  heads.Length + " cabezas disponibles", classicSmall);
    }

    void DrawClassicRight()
    {
        DrawClassicPanel(new Rect(848, 44, 320, 420));
        GUI.Label(new Rect(868, 55, 280, 23), "CLASE", classicSection);
        for (int i = 0; i < 12; i++)
        {
            AORPGDatabaseV11.ClassDef cls = AORPGDatabaseV11.GetClass(i + 1);
            string label = cls == null ? ClassIcons[i] : cls.name;
            Rect rect = new Rect(868 + (i % 3) * 96,
                                 83 + (i / 3) * 46, 90, 44);
            if (ClassicIconButton(rect, "class_" + ClassIcons[i], label,
                                  classId == i + 1))
                classId = i + 1;
        }

        AORPGDatabaseV11.ClassDef selected = AORPGDatabaseV11.GetClass(classId);
        if (selected != null)
            GUI.Label(new Rect(868, 268, 280, 33),
                selected.name + "  ·  Vida " + selected.life.ToString("0.##") +
                "  ·  Mana " + selected.initialMana.ToString("0.##"), classicSmall);

        GUI.Label(new Rect(868, 305, 280, 23), "CIUDAD INICIAL", classicSection);
        for (int i = 0; i < AOHomeCityV200.Names.Length; i++)
        {
            Rect rect = new Rect(868 + (i % 3) * 96,
                                 330 + (i / 3) * 45, 90, 42);
            string city = AOHomeCityV200.Names[i];
            if (ClassicIconButton(rect, "city_" + city, city,
                                  homeCityId == i + 1))
                homeCityId = i + 1;
        }

        if (AOHomeCityV200.TryGet(homeCityId, out string name, out int map,
                                  out int x, out int y))
            GUI.Label(new Rect(868, 421, 280, 20),
                      name + "  ·  Mapa " + map + "  ·  " + x + "," + y,
                      classicSmall);
    }

    void DrawClassicFooter()
    {
        GUI.enabled = !returnToMenuNextFrame && !startSessionNextFrame;
        if (ClassicButton(new Rect(22, 474, 160, 36), "Volver"))
            Close();

        string createLabel = overwriteWarning && !overwriteConfirmed
            ? "Reemplazar partida" : "Crear personaje";
        if (ClassicButton(new Rect(970, 474, 188, 36), createLabel))
        {
            if (overwriteWarning && !overwriteConfirmed)
            {
                overwriteConfirmed = true;
                message = "Confirmá nuevamente para reemplazar la partida.";
            }
            else
                CreateCharacter();
        }
        GUI.enabled = true;

        if (!string.IsNullOrEmpty(message))
            GUI.Label(new Rect(200, 473, 750, 39), message, classicLabel);
        else if (overwriteWarning && !overwriteConfirmed)
            GUI.Label(new Rect(200, 473, 750, 39),
                      "Ya existe una partida. Crear otra reemplaza ese slot.",
                      classicLabel);
    }
}
