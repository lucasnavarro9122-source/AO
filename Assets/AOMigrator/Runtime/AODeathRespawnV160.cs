using UnityEngine;

[DisallowMultipleComponent]
public class AODeathRespawnV160 : MonoBehaviour
{
    public const int DEAD_BODY_ID = 829;

    public const int HOME_MAP = 1;
    public const int HOME_X = 57;
    public const int HOME_Y = 44;

    public const float HOME_SECONDS = 105f;

    AOPlayerCombatV09 combat;
    AOPlayerRPGV11 rpg;
    AOInventoryV10 inventory;
    AOPlayerMagicV120 magic;
    AOCharacterProfileVisualV111 profile;
    AOCharacterRenderer rendererVisual;
    AOTestPlayer movement;
    AOWorldManagerV07 world;

    bool returningHome;
    float homeArrivalAt;

    GUIStyle ghostStyle;

    public bool ReturningHome =>
        returningHome;

    public float HomeSecondsRemaining =>
        !returningHome
        ? 0f
        : Mathf.Max(
            0f,
            homeArrivalAt -
            Time.time);

    void Awake()
    {
        FindReferences();
    }

    void Update()
    {
        FindReferences();

        if (!returningHome)
            return;

        if (combat == null ||
            !combat.IsDead)
        {
            CancelHome();
            return;
        }

        if (Time.time <
            homeArrivalAt)
            return;

        returningHome = false;

        if (world == null)
        {
            AOInterfaceV0101.PushMessage(
                "No encuentro el World Manager para volver a casa.");
            return;
        }

        if (world.MagicTeleport(
                HOME_MAP,
                HOME_X,
                HOME_Y,
                out string result))
        {
            AOInterfaceV0101.PushMessage(
                "Has regresado a tu ciudad de origen. " +
                "Seguís muerto: buscá un sacerdote.");
        }
        else
        {
            AOInterfaceV0101.PushMessage(
                "No pude volver a Ullathorpe: " +
                result);
        }
    }

    void FindReferences()
    {
        if (combat == null)
            combat =
                GetComponent
                    <AOPlayerCombatV09>();

        if (rpg == null)
            rpg =
                GetComponent
                    <AOPlayerRPGV11>();

        if (inventory == null)
            inventory =
                GetComponent
                    <AOInventoryV10>();

        if (magic == null)
            magic =
                GetComponent
                    <AOPlayerMagicV120>();

        if (profile == null)
        {
            profile =
                GetComponent
                    <AOCharacterProfileVisualV111>();

            if (profile == null)
            {
                profile =
                    GetComponentInChildren
                        <AOCharacterProfileVisualV111>(true);
            }
        }

        if (rendererVisual == null)
        {
            rendererVisual =
                GetComponentInChildren
                    <AOCharacterRenderer>(true);
        }

        if (movement == null)
            movement =
                GetComponent
                    <AOTestPlayer>();

        if (world == null)
        {
            world =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOWorldManagerV07>();
        }
    }

    public void OnDeathStarted()
    {
        FindReferences();

        CancelHome();

        if (rpg != null)
            rpg.OnPlayerDeath();

        if (inventory != null)
            inventory.UnequipAllForDeath();

        // Decision 18: original death drop on demo dungeon floors (offline; online the server does it).
        AODeathDrop.OnPlayerDeath(gameObject);

        if (magic != null)
            magic.ResetRuntimeForLoad();

        AOCityUIV130 cityUI =
            GetComponent<AOCityUIV130>();

        if (cityUI != null &&
            AOCityUIV130.ModalOpen)
        {
            cityUI.Close();
        }

        AOQuestUIV150 questUI =
            GetComponent<AOQuestUIV150>();

        if (questUI != null &&
            AOQuestUIV150.ModalOpen)
        {
            questUI.Close();
        }
    }

    public void EnterGhostState()
    {
        FindReferences();

        bool applied = false;

        if (profile != null)
        {
            profile.SetDeadVisual(
                true);
            applied = true;
        }

        if (!applied &&
            rendererVisual != null)
        {
            applied =
                AODeathVisualV160
                    .TryApplyToRenderer(
                        rendererVisual);
        }

        if (!applied)
        {
            Debug.LogWarning(
                "[AO v0.16.1] No pude aplicar el fantasma BODY829. Revisá si existe AOCharacterProfileVisualV111 o AOCharacterRenderer.");
        }

        if (movement != null)
            movement.enabled = true;
    }

    public void ApplyLoadedDeadState()
    {
        FindReferences();

        CancelHome();

        if (rpg != null)
            rpg.OnPlayerDeath();

        if (inventory != null)
            inventory.UnequipAllForDeath();

        if (magic != null)
            magic.ResetRuntimeForLoad();

        EnterGhostState();
    }

    public void OnResurrected()
    {
        FindReferences();

        CancelHome();

        if (profile != null)
        {
            profile.SetDeadVisual(
                false);
        }
        else if (rendererVisual != null)
        {
            // Si no hay profile, al menos refrescamos el render vivo en el próximo flujo normal.
            rendererVisual.ForceRefreshVisuals();
        }

        if (movement != null)
            movement.enabled = true;
    }

    public bool TryGoHome(
        out string message)
    {
        FindReferences();

        if (combat == null ||
            !combat.IsDead)
        {
            message =
                "Debés estar muerto para utilizar /HOGAR.";
            return false;
        }

        if (returningHome)
        {
            message =
                "Ya estás regresando a tu hogar. Faltan " +
                Mathf.CeilToInt(
                    HomeSecondsRemaining) +
                " s.";
            return false;
        }

        returningHome = true;

        homeArrivalAt =
            Time.time +
            HOME_SECONDS;

        message =
            "Volverás a Ullathorpe en " +
            Mathf.RoundToInt(
                HOME_SECONDS) +
            " segundos.";

        return true;
    }

    public void CancelHome()
    {
        returningHome = false;
        homeArrivalAt = 0f;
    }

    void OnGUI()
    {
        if (AOOnlineClientV240.InputBlocked) return;
        if (!AOMainMenuV140.SessionActive ||
            combat == null ||
            !combat.IsDead)
            return;

        if (ghostStyle == null)
        {
            ghostStyle =
                new GUIStyle(
                    GUI.skin.box);

            ghostStyle.alignment =
                TextAnchor.MiddleCenter;

            ghostStyle.wordWrap =
                true;

            ghostStyle.fontSize = 12;
        }

        string homeText =
            returningHome
            ? "Regreso a Ullathorpe: " +
              Mathf.CeilToInt(
                  HomeSecondsRemaining) +
              " s"
            : "/HOGAR o botón Hogar: " +
              Mathf.RoundToInt(
                  HOME_SECONDS) +
              " s";

        // Anchored to the game viewport so it does not cover the chat.
        Camera gameCamera =
            AOActionBarV260.GameCamera;

        Rect view =
            gameCamera != null
            ? gameCamera.pixelRect
            : new Rect(0f, 0f, Screen.width, Screen.height);

        GUI.Box(
            new Rect(
                view.center.x - 180f,
                Screen.height - view.yMax + 8f,
                360f,
                56f),
            "ESPÍRITU\n" +
            homeText,
            ghostStyle);
    }
}
