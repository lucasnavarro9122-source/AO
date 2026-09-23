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

    void OnGUI()
    {
        if (!visible)
            return;

        FindReferences();

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
