using UnityEngine;

[DisallowMultipleComponent]
public class AOMainMenuV140 : MonoBehaviour
{
    static AOMainMenuV140 instance;

    public static bool SessionActive
    {
        get;
        private set;
    }

    public static bool ModalOpen =>
        instance != null &&
        instance.visible;

#if UNITY_EDITOR
    public void HideForVisualQA()
    {
        openCreatorNextFrame = false;
        visible = false;
        SessionActive = false;
        enabled = false;
    }
#endif

    bool visible = true;

    AOSaveGameV140 save;
    AOCharacterCreationV170 creator;

    string status = "";

    Texture2D characterSelectArt;
    GUIStyle selectionTitle;
    GUIStyle selectionDetails;
    GUIStyle selectionHint;
    string selectionSummary = "";
    bool selectionHasSave;
    Sprite selectionBody;
    Sprite selectionHead;
    float selectionHeadX;
    float selectionHeadY;
    float selectionBodyX;

    bool openCreatorNextFrame;
    bool continueNextFrame;
    bool exitNextFrame;

    Rect window =
        new Rect(
            0,
            0,
            460,
            460);

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        instance = null;
        SessionActive = false;
    }

    void Awake()
    {
        instance = this;

        FindReferences();

        visible = true;
        SessionActive = false;

        RefreshSelection();

        Time.timeScale = 0f;
    }

    void Update()
    {
        if (exitNextFrame)
        {
            exitNextFrame = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        if (continueNextFrame)
        {
            continueNextFrame = false;
            FindReferences();
            if (save != null && save.LoadGame(false))
                StartSession();
            else
                status = "No pude cargar la partida.";
            return;
        }

        if (!openCreatorNextFrame)
            return;

        openCreatorNextFrame = false;
        visible = false;

        FindReferences();

        if (creator == null)
        {
            visible = true;
            status =
                "No encuentro AOCharacterCreationV170.";
            return;
        }

        creator.Open(
            save != null &&
            save.HasSave);
    }

    void FindReferences()
    {
        if (save == null)
            save =
                GetComponent
                    <AOSaveGameV140>();

        if (creator == null)
            creator =
                GetComponent
                    <AOCharacterCreationV170>();
    }

    void RefreshSelection()
    {
        FindReferences();
        selectionHasSave = save != null && save.HasSave;
        selectionSummary = selectionHasSave
            ? save.GetSummary().Split('\n')[0].Trim()
            : "No hay personajes guardados.";
        selectionBody = null;
        selectionHead = null;
        status = "";

        if (!selectionHasSave ||
            !save.TryGetCharacterVisual(
                out int raceId, out int genderId, out int headId) ||
            !AOCharacterVisualDatabaseV111.TryBuildBase(
                raceId, genderId, headId,
                out AOCharacterRenderer.DirectionVisual[] directions,
                out selectionHeadX, out selectionHeadY,
                out selectionBodyX))
            return;

        foreach (AOCharacterRenderer.DirectionVisual direction
                 in directions)
        {
            if (direction == null || direction.heading != AOGridMap.SOUTH)
                continue;

            selectionBody = direction.body != null && direction.body.Length > 0
                ? direction.body[0] : null;
            selectionHead = direction.head != null && direction.head.Length > 0
                ? direction.head[0] : null;
            break;
        }
    }

    void OnGUI()
    {
        if (!visible)
            return;

        FindReferences();

        if (characterSelectArt == null)
            characterSelectArt = Resources.Load<Texture2D>(
                "AOMigrator/ClassicUI/character_select");

        if (characterSelectArt != null)
        {
            DrawCharacterSelection();
            return;
        }

        GUISkin previousSkin = GUI.skin;
        GUI.skin = AOClassicSkinV200.Get(previousSkin);

        window.x =
            (Screen.width -
             window.width) *
            0.5f;

        window.y =
            (Screen.height -
             window.height) *
            0.5f;

        GUI.ModalWindow(
            140140,
            window,
            Draw,
            "Argentum Online");
        GUI.skin = previousSkin;
    }

    void EnsureSelectionStyles()
    {
        if (selectionTitle != null)
            return;

        Font font = Resources.Load<Font>(
            "AOMigrator/ClassicUI/Cardo-Regular");

        selectionTitle = new GUIStyle(GUI.skin.label);
        selectionTitle.font = font;
        selectionTitle.fontSize = 23;
        selectionTitle.fontStyle = FontStyle.Bold;
        selectionTitle.alignment = TextAnchor.MiddleCenter;
        selectionTitle.normal.textColor = new Color(0.94f, 0.8f, 0.52f);

        selectionDetails = new GUIStyle(selectionTitle);
        selectionDetails.fontSize = 15;
        selectionDetails.fontStyle = FontStyle.Normal;
        selectionDetails.wordWrap = true;
        selectionDetails.normal.textColor = new Color(0.93f, 0.88f, 0.77f);

        selectionHint = new GUIStyle(selectionDetails);
        selectionHint.fontSize = 13;
        selectionHint.normal.textColor = new Color(0.75f, 0.72f, 0.65f);
    }

    void DrawCharacterSelection()
    {
        EnsureSelectionStyles();

        Matrix4x4 oldMatrix = GUI.matrix;
        int oldDepth = GUI.depth;
        bool oldEnabled = GUI.enabled;
        Color oldColor = GUI.color;

        GUI.depth = -100;
        GUI.color = new Color(0.07f, 0.06f, 0.05f, 0.96f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;

        const float width = 1024f;
        const float height = 768f;
        float scale = Mathf.Min(Screen.width / width, Screen.height / height);
        float left = (Screen.width - width * scale) * 0.5f;
        float top = (Screen.height - height * scale) * 0.5f;
        GUI.matrix = Matrix4x4.TRS(new Vector3(left, top, 0f),
                                   Quaternion.identity,
                                   new Vector3(scale, scale, 1f));

        // Grh 3839 usa sólo los 768 píxeles superiores de la imagen 238.
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, width, height),
            characterSelectArt, new Rect(0f, 0.25f, 1f, 0.75f), true);

        if (selectionHasSave)
        {
            const float spriteScale = 1.8f;
            Vector2 basePoint = new Vector2(
                248f + selectionBodyX * 32f * spriteScale, 339f);
            DrawSelectionSprite(selectionBody, basePoint, spriteScale);
            DrawSelectionSprite(selectionHead,
                new Vector2(basePoint.x + selectionHeadX * 32f * spriteScale,
                    basePoint.y - selectionHeadY * 32f * spriteScale),
                spriteScale);

            if (AOAudioV190.Clicked(GUI.Button(new Rect(191, 233, 113, 130),
                    GUIContent.none, GUIStyle.none)))
                status = "Personaje seleccionado.";

            GUI.Label(new Rect(386, 568, 254, 26),
                      "PERSONAJE LOCAL", selectionTitle);
            GUI.Label(new Rect(390, 605, 246, 62),
                      selectionSummary, selectionDetails);
        }
        else
        {
            GUI.Label(new Rect(385, 573, 254, 24),
                      "SIN PERSONAJES", selectionTitle);
            GUI.Label(new Rect(391, 606, 242, 45),
                      "Elegí CREAR para empezar.", selectionDetails);
        }

        GUI.Label(new Rect(213, 668, 600, 29),
            string.IsNullOrEmpty(status)
                ? "Modo local: un espacio disponible."
                : status,
            selectionHint);

        // Zonas de clic de los botones dibujados en la gráfica original.
        if (AOAudioV190.Clicked(GUI.Button(new Rect(246, 699, 188, 51),
                GUIContent.none, GUIStyle.none)))
            openCreatorNextFrame = true;

        GUI.enabled = selectionHasSave;
        if (AOAudioV190.Clicked(GUI.Button(new Rect(593, 699, 188, 51),
                GUIContent.none, GUIStyle.none)))
            continueNextFrame = true;
        GUI.enabled = oldEnabled;

        if (AOAudioV190.Clicked(GUI.Button(new Rect(966, 12, 49, 48),
                GUIContent.none, GUIStyle.none)) ||
            AOAudioV190.Clicked(GUI.Button(new Rect(10, 12, 49, 48),
                GUIContent.none, GUIStyle.none)))
            exitNextFrame = true;

        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
        GUI.depth = oldDepth;
        GUI.enabled = oldEnabled;
    }

    static void DrawSelectionSprite(
        Sprite sprite,
        Vector2 bottomCenter,
        float scale)
    {
        if (sprite == null || sprite.texture == null)
            return;

        Rect source = sprite.textureRect;
        Rect target = new Rect(
            bottomCenter.x - source.width * scale * 0.5f,
            bottomCenter.y - source.height * scale,
            source.width * scale,
            source.height * scale);
        Rect uv = new Rect(
            source.x / sprite.texture.width,
            source.y / sprite.texture.height,
            source.width / sprite.texture.width,
            source.height / sprite.texture.height);
        GUI.DrawTextureWithTexCoords(target, sprite.texture, uv, true);
    }

    void Draw(
        int id)
    {
        GUILayout.Space(
            12);

        GUILayout.Label(
            "ARGENTUM ONLINE",
            GUI.skin
                .GetStyle(
                    "box"),
            GUILayout.Height(
                42));

        GUILayout.Space(
            8);

        if (save != null &&
            save.HasSave)
        {
            GUILayout.Box(
                "Última partida\n" +
                save.GetSummary(),
                GUILayout.Height(
                    82));
        }
        else
        {
            GUILayout.Box(
                "No hay partida guardada.",
                GUILayout.Height(
                    55));
        }

        GUILayout.Space(
            12);

        GUI.enabled =
            save != null &&
            save.HasSave;

        if (AOAudioV190.Clicked(GUILayout.Button(
                "Continuar",
                GUILayout.Height(
                    44))))
        {
            continueNextFrame = true;
        }

        GUI.enabled = true;

        if (AOAudioV190.Clicked(GUILayout.Button(
                "Nueva partida",
                GUILayout.Height(
                    44))))
        {
            // IMGUI no permite activar otro ModalWindow dentro
            // del mismo evento que está dibujando este modal.
            // Lo diferimos a Update para el siguiente frame.
            openCreatorNextFrame = true;
        }

        GUILayout.Space(
            10);

        GUILayout.Label(
            "Durante la partida:\n" +
            "F1 = Guardado rápido\n" +
            "F3 = Carga rápida\n" +
            "Q = Diario de quests\n" +
            "Autoguardado cada 60 segundos.");

        if (!string.IsNullOrEmpty(
                status))
        {
            GUILayout.Box(
                status);
        }

        GUILayout.FlexibleSpace();

        if (AOAudioV190.Clicked(GUILayout.Button(
                "Salir",
                GUILayout.Height(
                    32))))
        {
            exitNextFrame = true;
        }
    }

    public static void ShowFromCreator()
    {
        if (instance == null)
            return;

        instance.openCreatorNextFrame = false;
        instance.visible = true;
        SessionActive = false;

        instance.RefreshSelection();

        Time.timeScale = 0f;
    }

    public static void StartSessionFromCreator()
    {
        if (instance == null)
            return;

        instance.StartSession();
    }

    void StartSession()
    {
        openCreatorNextFrame = false;
        visible = false;
        SessionActive = true;

        Time.timeScale = 1f;

        AOInterfaceV0101.PushMessage(
            "Bienvenido. F1 guardar | F3 cargar | Q misiones");
    }

    void OnDisable()
    {
        if (Time.timeScale == 0f)
            Time.timeScale = 1f;
    }
}
