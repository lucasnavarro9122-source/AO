using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AOInterfaceV0102RepairMenu
{
    [MenuItem(
        "AO Migrador/Reparar interfaz v0.10.2 - auto")]
    public static void Repair()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "AO UI v0.10.2",
                "Salí de Play y ejecutá la reparación de nuevo.",
                "OK");
            return;
        }

        AOInterfaceV0101 ui =
            Object.FindFirstObjectByType
                <AOInterfaceV0101>();

        bool created = false;

        if (ui == null)
        {
            GameObject go =
                new GameObject(
                    "AO Interface v0.10.2");

            Undo.RegisterCreatedObjectUndo(
                go,
                "Crear interfaz AO");

            ui =
                Undo.AddComponent
                    <AOInterfaceV0101>(go);

            created = true;
        }

        AOTestPlayer player =
            Object.FindFirstObjectByType
                <AOTestPlayer>();

        AOPlayerCombatV09 combat =
            player != null
            ? player.GetComponent
                <AOPlayerCombatV09>()
            : null;

        AOInventoryV10 inventory =
            player != null
            ? player.GetComponent
                <AOInventoryV10>()
            : null;

        AOWorldManagerV07 world =
            Object.FindFirstObjectByType
                <AOWorldManagerV07>();

        AOCameraFollow follow =
            Object.FindFirstObjectByType
                <AOCameraFollow>();

        Camera cam =
            follow != null
            ? follow.GetComponent<Camera>()
            : Camera.main;

        ui.gameObject.SetActive(true);
        ui.enabled = true;

        ui.Configure(
            player,
            combat,
            inventory,
            world,
            cam);

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager
                .GetActiveScene());

        EditorUtility.DisplayDialog(
            "AO UI v0.10.2",
            "Reparación lista.\n\n" +
            "Interfaz creada: " + created +
            "\nPlayer: " + (player != null) +
            "\nCombat: " + (combat != null) +
            "\nInventory: " + (inventory != null) +
            "\nWorld: " + (world != null) +
            "\nCamera: " + (cam != null) +
            "\n\nGuardá Ctrl+S y probá Play.",
            "OK");
    }
}
