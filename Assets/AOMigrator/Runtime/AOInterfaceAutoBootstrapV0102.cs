using UnityEngine;

public static class AOInterfaceAutoBootstrapV0102
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInterface()
    {
        Texture2D frame =
            Resources.Load<Texture2D>(
                "AOMigrator/InterfaceV0101/hud_frame");

        if (frame == null)
        {
            Debug.LogError(
                "[AO UI v0.10.3] No encuentro hud_frame. " +
                "Revisá Assets/Resources/AOMigrator/InterfaceV0101.");
            return;
        }

        AOInterfaceV0101 ui =
            FindSceneInterface();

        if (ui == null)
        {
            GameObject go =
                new GameObject(
                    "AO Interface v0.10.3 AUTO");

            ui =
                go.AddComponent<AOInterfaceV0101>();

            Debug.Log(
                "[AO UI v0.10.3] Interfaz creada automáticamente.");
        }
        else
        {
            if (!ui.gameObject.activeSelf)
                ui.gameObject.SetActive(true);

            if (!ui.enabled)
                ui.enabled = true;

            Debug.Log(
                "[AO UI v0.10.3] Interfaz existente reactivada.");
        }

        AOTestPlayer player =
            Object.FindFirstObjectByType<AOTestPlayer>();

        AOPlayerCombatV09 combat =
            player != null
            ? player.GetComponent<AOPlayerCombatV09>()
            : null;

        AOInventoryV10 inventory =
            player != null
            ? player.GetComponent<AOInventoryV10>()
            : null;

        AOWorldManagerV07 world =
            Object.FindFirstObjectByType<AOWorldManagerV07>();

        AOCameraFollow follow =
            Object.FindFirstObjectByType<AOCameraFollow>();

        Camera cam =
            follow != null
            ? follow.GetComponent<Camera>()
            : Camera.main;

        ui.Configure(
            player,
            combat,
            inventory,
            world,
            cam);

        Debug.Log(
            "[AO UI v0.10.3] Bootstrap completo. " +
            "player=" + (player != null) +
            " combat=" + (combat != null) +
            " inventory=" + (inventory != null) +
            " world=" + (world != null) +
            " camera=" + (cam != null));
    }

    static AOInterfaceV0101 FindSceneInterface()
    {
        AOInterfaceV0101[] all =
            Resources.FindObjectsOfTypeAll
                <AOInterfaceV0101>();

        foreach (AOInterfaceV0101 ui in all)
        {
            if (ui == null)
                continue;

            if (!ui.gameObject.scene.IsValid())
                continue;

            return ui;
        }

        return null;
    }
}
